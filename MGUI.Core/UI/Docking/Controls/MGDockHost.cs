using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Root container for the docking system. Manages the DockLayoutModel and renders the visual tree.
/// </summary>
public class MGDockHost : MGSingleContentHost
{
    private DockLayoutModel _layoutModel;
    /// <summary>
    /// The layout model that defines the docking structure.
    /// </summary>
    public DockLayoutModel LayoutModel
    {
        get => _layoutModel;
        set
        {
            if (_layoutModel != value)
            {
                // Unsubscribe from old model
                if (_layoutModel != null)
                {
                    _layoutModel.LayoutChanged -= OnLayoutModelChanged;
                }

                _layoutModel = value;

                // Subscribe to new model
                if (_layoutModel != null)
                {
                    _layoutModel.LayoutChanged += OnLayoutModelChanged;
                }

                // Re-sync node subscriptions (OnTabGroupPropertyChanged etc.)
                SyncNodeSubscriptions();

                NPC(nameof(LayoutModel));
                RebuildVisualTree();
            }
        }
    }

    /// <summary>
    /// Registry of all panels by their ID for quick lookup.
    /// </summary>
    private readonly Dictionary<string, DockPanelNode> _panelRegistry = new Dictionary<string, DockPanelNode>();

    private DockableRegistry _dockableRegistry;
    /// <summary>
    /// Optional registry of <see cref="DockableDefinition"/>s.
    /// When set, the host will automatically notify the registry of lifecycle events
    /// (shown, hidden, closed, activated) for any registered dockable.
    /// </summary>
    public DockableRegistry DockableRegistry
    {
        get => _dockableRegistry;
        set
        {
            if (_dockableRegistry != value)
            {
                _dockableRegistry = value;
                // Sync current visibility to the new registry
                if (_dockableRegistry != null)
                {
                    SyncRegistryVisibility();
                }
            }
        }
    }

    /// <summary>
    /// Event raised when a panel is added to the layout.
    /// </summary>
    public event EventHandler<DockPanelNode> PanelAdded;

    /// <summary>
    /// Event raised when a panel is removed from the layout.
    /// </summary>
    public event EventHandler<DockPanelNode> PanelRemoved;

    /// <summary>
    /// Event raised when the active panel in any tab group changes.
    /// </summary>
    public event EventHandler<DockPanelNode> ActivePanelChanged;

    /// <summary>
    /// Event raised when the layout structure changes.
    /// </summary>
    public event EventHandler DockLayoutChanged;

    #region Maximize / Restore

    /// <summary>
    /// Stack of group node IDs that have been maximized.
    /// The top of the stack is the currently maximized group.
    /// Empty when the layout is in normal (non-maximized) state.
    /// </summary>
    private readonly Stack<string> _maximizeStack = new Stack<string>();

    /// <summary>The ID of the currently maximized group, or null when not maximized.</summary>
    private string CurrentMaximizedGroupId =>
        _maximizeStack.Count > 0 ? _maximizeStack.Peek() : null;

    /// <summary>
    /// Maximizes <paramref name="groupNode"/> so it fills the entire host.
    /// The previous layout is restored via <see cref="RestoreLayout"/>.
    /// Multiple maximize calls stack (inner group on top of outer).
    /// </summary>
    public void MaximizeGroup(DockTabGroupNode groupNode)
    {
        if (groupNode == null)
        {
            return;
        }

        _maximizeStack.Push(groupNode.Id);
        RebuildVisualTree();
    }

    /// <summary>
    /// Pops the maximize stack, restoring the layout to the state before the most recent maximize.
    /// Does nothing if the layout is not currently maximized.
    /// </summary>
    public void RestoreLayout()
    {
        if (_maximizeStack.Count == 0)
        {
            return;
        }

        _maximizeStack.Pop();
        RebuildVisualTree();
    }

    /// <summary>Whether the layout is currently in a maximized state.</summary>
    public bool IsMaximized => _maximizeStack.Count > 0;

    #endregion Maximize / Restore

    #region Floating Windows

    private readonly List<MGFloatingDockWindow> _floatingWindows = new();

    /// <summary>
    /// Read-only view of all currently open floating dock windows managed by this host.
    /// </summary>
    public IReadOnlyList<MGFloatingDockWindow> FloatingWindows => _floatingWindows;

    #endregion Floating Windows

    /// <summary>
    /// All <see cref="MGDockTabGroup"/> visuals that are currently mounted in the docked layout.
    /// Tracked so that <see cref="RebuildVisualTree"/> can <see cref="MGDockTabGroup.Detach"/> them
    /// before replacing the visual tree, preventing model→visual reference leaks.
    /// </summary>
    private readonly List<MGDockTabGroup> _activeTabGroupVisuals = new();


    #region Drag & Drop State

    private DockDragData _currentDrag;
    /// <summary>
    /// The current drag operation data, or null if no drag is in progress.
    /// </summary>
    public DockDragData CurrentDrag
    {
        get => _currentDrag;
        private set
        {
            if (_currentDrag != value)
            {
                _currentDrag = value;
                NPC(nameof(CurrentDrag));
                NPC(nameof(IsDragging));
            }
        }
    }

    /// <summary>
    /// True if a drag operation is currently in progress.
    /// </summary>
    public bool IsDragging => CurrentDrag != null;

    /// <summary>
    /// Last mouse position where preview was calculated.
    /// Used to avoid recalculating drop targets too frequently.
    /// </summary>
    private Point _lastPreviewCalculation;

    /// <summary>
    /// Last known mouse position during a drag operation.
    /// Updated every update tick while dragging; used by <see cref="PerformDrop"/> to decide
    /// whether the drop occurred inside or outside the host bounds.
    /// </summary>
    private Point _lastMousePosition;

    private int _dragThreshold = 5;
    /// <summary>
    /// Distance in pixels the mouse must move before drag visuals activate.
    /// Default: 5 pixels.
    /// </summary>
    public int DragThreshold
    {
        get => _dragThreshold;
        set
        {
            if (_dragThreshold != value)
            {
                _dragThreshold = value;
                NPC(nameof(DragThreshold));
            }
        }
    }

    // ─── 14.1 Active dockable tracking ──────────────────────────────────────
    private DockPanelNode _activeDockable;
    /// <summary>
    /// The panel that was most recently activated in the host.
    /// Updated automatically whenever <see cref="ActivePanelChanged"/> fires.
    /// The group that contains this panel displays an accent border via
    /// <see cref="MGDockTabGroup.IsActiveGroup"/>.
    /// </summary>
    public DockPanelNode ActiveDockable
    {
        get => _activeDockable;
        private set
        {
            if (_activeDockable != value)
            {
                _activeDockable = value;
                NPC(nameof(ActiveDockable));
                RefreshActiveGroupHighlight();
            }
        }
    }

    // ─── 13.1 Proximity docking ──────────────────────────────────────────────
    /// <summary>
    /// When true, moving the mouse cursor within <see cref="ProximityBandWidth"/> pixels of any
    /// docked panel edge automatically activates a split-dock zone — no joystick hover required.
    /// Default: true.
    /// </summary>
    public bool ProximityDockingEnabled { get; set; } = true;

    /// <summary>
    /// Width (in logical pixels, scaled by <see cref="UIScale"/>) of the edge band that triggers
    /// automatic split-zone activation during a drag when <see cref="ProximityDockingEnabled"/> is true.
    /// Default: 30 pixels.
    /// </summary>
    public int ProximityBandWidth { get; set; } = 30;

    // ─── 14.5 DPI / UI scale ────────────────────────────────────────────────
    private float _uiScale = 1f;
    /// <summary>
    /// Scaling factor applied to drag thresholds and proximity detection distances.
    /// Set this to match the application's content-scale / DPI factor.
    /// Default: 1.0 (no scaling, logical-pixel distances are used as-is).
    /// </summary>
    public float UIScale
    {
        get => _uiScale;
        set
        {
            float clamped = Math.Clamp(value, 0.25f, 4f);
            if (Math.Abs(_uiScale - clamped) > 1e-6f)
            {
                _uiScale = clamped;
                NPC(nameof(UIScale));
            }
        }
    }

    #endregion Drag & Drop State

    /// <summary>
    /// Creates a new MGDockHost.
    /// </summary>
    /// <param name="window">The parent window.</param>
    public MGDockHost(MGWindow window) : base(window, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            // Initialize with empty layout model
            _layoutModel = new DockLayoutModel();
            _layoutModel.LayoutChanged += OnLayoutModelChanged;

            // Initialize preview overlay
            _previewOverlay = new MGDockPreviewOverlay(window);
            _previewOverlayComponent = new MGComponent<MGDockPreviewOverlay>(
                _previewOverlay,
                ComponentUpdatePriority.AfterContents,
                ComponentDrawPriority.AfterContents,
                true, true, false, false, false, false, false,
                (AvailableBounds, ComponentSize) => AvailableBounds);
            AddComponent(_previewOverlayComponent);

            // Initialize drop indicators overlay
            _dropIndicators = new MGDockDropIndicators(window);
            _dropIndicatorsComponent = new MGComponent<MGDockDropIndicators>(
                _dropIndicators,
                ComponentUpdatePriority.AfterContents,
                ComponentDrawPriority.AfterContents,
                true, true, false, false, false, false, false,
                (AvailableBounds, ComponentSize) => AvailableBounds);
            AddComponent(_dropIndicatorsComponent);

            // Set default styling
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;

            // ── Auto-hide strips (one per edge) ────────────────────────────────
            foreach (AutoHideSide side in System.Enum.GetValues(typeof(AutoHideSide)))
            {
                var strip = new MGDockAutoHideStrip(window, side);
                strip.Visibility = Visibility.Collapsed;
                strip.PanelActivated += (_, panel) => ShowAutoHideDrawer(panel);
                _autoHideStrips[side] = strip;

                AutoHideSide capturedSide = side;
                var comp = new MGComponent<MGDockAutoHideStrip>(
                    strip,
                    ComponentUpdatePriority.AfterContents,
                    ComponentDrawPriority.AfterContents,
                    true, true, false, false, false, false, false,
                    (avail, _) => GetStripBounds(capturedSide, avail));
                AddComponent(comp);
            }

            // ── Auto-hide drawer overlay ───────────────────────────────────
            _autoHideDrawer = new MGDockAutoHideDrawer(window);
            _autoHideDrawer.Visibility  = Visibility.Collapsed;
            _autoHideDrawer.PinRequested         += (_, panel) => RepinPanel(panel);
            _autoHideDrawer.PanelCloseRequested   += (_, panel) => CloseAutoHidePanel(panel);
            _autoHideDrawer.CloseRequested         += (_, _)     => HideAutoHideDrawer();
            _autoHideDrawer.DrawerSizeChanged      += (_, _)     => InvalidateLayout();
            var drawerComp = new MGComponent<MGDockAutoHideDrawer>(
                _autoHideDrawer,
                ComponentUpdatePriority.AfterContents,
                ComponentDrawPriority.AfterContents,
                true, true, false, false, false, false, false,
                (avail, _) => GetDrawerBounds(avail));
            AddComponent(drawerComp);
        }
    }

    /// <summary>
    /// Shrinks the content bounds to leave space for any visible auto-hide strips.
    /// </summary>
    protected override void UpdateContentLayout(Microsoft.Xna.Framework.Rectangle Bounds)
    {
        base.UpdateContentLayout(ComputeInnerBounds(Bounds));
    }

    /// <summary>
    /// Returns the content rectangle with insets reserved for active auto-hide strips.
    /// </summary>
    private Microsoft.Xna.Framework.Rectangle ComputeInnerBounds(Microsoft.Xna.Framework.Rectangle bounds)
    {
        int left   = LayoutModel?.HasAutoHidePanels(AutoHideSide.Left)   == true ? _autoHideStripThickness : 0;
        int right  = LayoutModel?.HasAutoHidePanels(AutoHideSide.Right)  == true ? _autoHideStripThickness : 0;
        int top    = LayoutModel?.HasAutoHidePanels(AutoHideSide.Top)    == true ? _autoHideStripThickness : 0;
        int bottom = LayoutModel?.HasAutoHidePanels(AutoHideSide.Bottom) == true ? _autoHideStripThickness : 0;
        return new Microsoft.Xna.Framework.Rectangle(
            bounds.X + left,
            bounds.Y + top,
            Math.Max(0, bounds.Width  - left - right),
            Math.Max(0, bounds.Height - top  - bottom));
    }

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);

        // ── Close auto-hide drawer on click outside ──────────────────────────────
        if (_autoHideDrawer?.Visibility == Visibility.Visible)
        {
            var mouseState = ParentWindow.Desktop.InputTracker.Mouse;
            bool lmbPressed = mouseState.CurrentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
                           && mouseState.PreviousState.LeftButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            if (lmbPressed)
            {
                Point mp = mouseState.CurrentPosition;
                bool insideDrawer = _autoHideDrawer.LayoutBounds.Contains(mp);
                bool insideStrip  = false;
                foreach (var strip in _autoHideStrips.Values)
                {
                    if (strip.Visibility == Visibility.Visible && strip.LayoutBounds.Contains(mp))
                    { insideStrip = true; break; }
                }
                if (!insideDrawer && !insideStrip)
                {
                    HideAutoHideDrawer();
                }
            }
        }

        // ── 14.3 Ctrl+Tab panel switcher ─────────────────────────────────────────
        if (!IsDragging)
        {
            var kb = ParentWindow.Desktop.InputTracker.Keyboard;
            bool ctrlHeld       = kb.IsControlDown;
            bool shiftHeld      = kb.IsShiftDown;
            bool tabJustPressed = kb.CurrentKeyPressedEvents[Microsoft.Xna.Framework.Input.Keys.Tab] != null;

            if (ctrlHeld && tabJustPressed)
            {
                CyclePanel(forward: !shiftHeld);
            }
        }

        // Handle drag operation via polling
        if (IsDragging)
        {
            // Check for ESC key to cancel drag
            if (ParentWindow.Desktop.InputTracker.Keyboard.CurrentState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape))
            {
                CancelDrag();
                return;
            }

            // Check if mouse button is still pressed
            bool isStillPressed = ParentWindow.Desktop.InputTracker.Mouse.CurrentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            if (!isStillPressed)
            {
                // Mouse button released - end drag and perform drop
                PerformDrop();
                return;
            }

            // Get current mouse position
            Point currentMousePosition = ParentWindow.Desktop.InputTracker.Mouse.CurrentPosition;

            // Check if drag threshold has been exceeded
            if (!CurrentDrag.HasExceededThreshold)
            {
                double distance = Math.Sqrt(
                    Math.Pow(currentMousePosition.X - CurrentDrag.DragStartPosition.X, 2) +
                    Math.Pow(currentMousePosition.Y - CurrentDrag.DragStartPosition.Y, 2));

                if (distance >= DragThreshold)
                {
                    // Threshold exceeded - activate drag visuals
                    CurrentDrag.HasExceededThreshold = true;
                    if (CurrentDrag.SourceTabItem != null)
                    {
                        CurrentDrag.SourceTabItem.Opacity = 0.5f;
                    }
                }
                else
                {
                    // Not yet moved enough, don't show preview
                    return;
                }
            }

            // Remember mouse position for PerformDrop (outside-host detection)
            _lastMousePosition = currentMousePosition;

            // Update drop preview
            UpdateDragPreview(currentMousePosition);
        }
    }

    /// <summary>
    /// Updates the drop preview during a drag operation.
    /// </summary>
    private void UpdateDragPreview(Point mousePosition)
    {
        // Optimization: skip if mouse hasn't moved significantly
        double distance = Math.Sqrt(
            Math.Pow(mousePosition.X - _lastPreviewCalculation.X, 2) +
            Math.Pow(mousePosition.Y - _lastPreviewCalculation.Y, 2));

        if (distance < 5)
        {
            return;
        }

        _lastPreviewCalculation = mousePosition;

        // ── Keep host-edge indicators visible and up-to-date throughout the drag ──
        // Use the inner bounds (excluding auto-hide strips) so the indicators are
        // positioned inside the strips, not hidden behind them.
        var innerBounds = ComputeInnerBounds(LayoutBounds);
        if (innerBounds.Width > 0 && innerBounds.Height > 0)
        {
            _dropIndicators.ShowHostEdge(innerBounds);
        }

        // ── Find which panel group the mouse is hovering over ───────────────────
        MGDockTabGroup hoveredGroup = null;
        foreach (var tabGroup in GetAllVisibleTabGroups())
        {
            if (tabGroup != null && tabGroup.LayoutBounds.Contains(mousePosition))
            {
                hoveredGroup = tabGroup;
                break;
            }
        }

        // Show/hide per-panel joystick indicators based on hovered group
        if (hoveredGroup != null && hoveredGroup != _lastHoveredGroup)
        {
            // Show indicators for the new group
            _dropIndicators.Show(hoveredGroup.LayoutBounds);
            _lastHoveredGroup = hoveredGroup;
        }
        else if (hoveredGroup == null && _lastHoveredGroup != null)
        {
            // Mouse left all groups - hide joystick
            _dropIndicators.Hide();
            _lastHoveredGroup = null;
        }

        // Tell the joystick which zones are forbidden given current drag + target group
        if (hoveredGroup != null && CurrentDrag?.DraggedPanel != null)
        {
            _dropIndicators.SetDisabledZones(
                GetForbiddenZones(CurrentDrag.DraggedPanel, hoveredGroup.GroupNode));
        }

        // ── PRIORITY 1: per-panel joystick ───────────────────────────────────────
        _dropIndicators.UpdateActiveZone(mousePosition);
        DockZone panelJoystickZone = _dropIndicators.GetZoneAtPosition(mousePosition);

        if (panelJoystickZone != DockZone.None && hoveredGroup != null)
        {
            // Panel joystick wins — suppress host-edge highlight
            _dropIndicators.UpdateHostEdgeActiveZone(new Point(-1, -1));

            var dropTarget = GetDropTargetForZone(hoveredGroup, panelJoystickZone, mousePosition);
            CurrentDropTarget = dropTarget;

            if (dropTarget != null && dropTarget.PreviewRect != default(Microsoft.Xna.Framework.Rectangle))
            {
                ShowPreview(dropTarget.PreviewRect);
            }
            else
            {
                HidePreview();
            }

            return;
        }

        // ── PRIORITY 2: host-edge indicator zones ───────────────────────────────
        _dropIndicators.UpdateHostEdgeActiveZone(mousePosition);
        DockZone hostEdgeZone = _dropIndicators.GetHostEdgeZoneAtPosition(mousePosition);

        if (hostEdgeZone != DockZone.None)
        {
            // Compute (or reuse) host-edge drop targets for the current host bounds
            var hostEdgeTargets = DockDropCalculator.CalculateHostEdgeZones(LayoutBounds);
            DockDropTarget hostTarget = null;
            foreach (var t in hostEdgeTargets)
            {
                if (t.Zone == hostEdgeZone)
                {
                    hostTarget = t;
                    break;
                }
            }

            CurrentDropTarget = hostTarget;

            if (hostTarget != null)
            {
                ShowPreview(hostTarget.PreviewRect);
            }
            else
            {
                HidePreview();
            }

            return;
        }

        // ── PRIORITY 3: splitter-bar drop ────────────────────────────────────────
        // When the mouse is hovering over any split container's splitter bar we offer a
        // "insert between" drop target.  hoveredGroup is typically null here because the
        // splitter bar lies in the gap between two groups.
        foreach (var sc in GetAllSplitContainers())
        {
            var splitterBounds = sc.SplitterBarLayoutBounds;
            if (splitterBounds == Microsoft.Xna.Framework.Rectangle.Empty)
            {
                continue;
            }

            // Expand the hit area a bit so the thin bar is easier to target.
            const int hitExpand = 4;
            var hitRect = new Microsoft.Xna.Framework.Rectangle(
                splitterBounds.X - hitExpand,
                splitterBounds.Y - hitExpand,
                splitterBounds.Width  + hitExpand * 2,
                splitterBounds.Height + hitExpand * 2);

            if (!hitRect.Contains(mousePosition))
            {
                continue;
            }

            // Determine which child to dock next to, and with which zone,
            // based on which half of the splitter bar the mouse is over.
            DockNode targetChildNode;
            DockZone splitterZone;

            if (sc.Orientation == Orientation.Horizontal)
            {
                // Horizontal split (left | right) — the bar is vertical.
                // Press left half  → add to the right of FirstChild.
                // Press right half → add to the left of SecondChild.
                bool leftHalf = mousePosition.X < splitterBounds.X + splitterBounds.Width / 2;
                if (leftHalf)
                {
                    targetChildNode = sc.ModelNode?.FirstChild;
                    splitterZone    = DockZone.Right;
                }
                else
                {
                    targetChildNode = sc.ModelNode?.SecondChild;
                    splitterZone    = DockZone.Left;
                }
            }
            else
            {
                // Vertical split (top / bottom) — the bar is horizontal.
                // Press top half    → add below FirstChild.
                // Press bottom half → add above SecondChild.
                bool topHalf = mousePosition.Y < splitterBounds.Y + splitterBounds.Height / 2;
                if (topHalf)
                {
                    targetChildNode = sc.ModelNode?.FirstChild;
                    splitterZone    = DockZone.Bottom;
                }
                else
                {
                    targetChildNode = sc.ModelNode?.SecondChild;
                    splitterZone    = DockZone.Top;
                }
            }

            // Resolve the child node to its first leaf tab group
            var leafGroup = targetChildNode != null ? FindFirstLeafTabGroup(targetChildNode) : null;
            if (leafGroup == null)
            {
                continue;
            }

            // Build the drop target reusing the normal zone calculator
            var leafVisual = GetAllVisibleTabGroups().FirstOrDefault(tg => tg.GroupNode == leafGroup);
            if (leafVisual == null)
            {
                continue;
            }

            var splitterDropTarget = GetDropTargetForZone(leafVisual, splitterZone, mousePosition);
            if (splitterDropTarget == null)
            {
                continue;
            }

            splitterDropTarget.IsSplitterDrop = true;
            splitterDropTarget.SplitterNode   = sc.ModelNode;

            CurrentDropTarget = splitterDropTarget;
            ShowPreview(splitterDropTarget.PreviewRect);
            return;
        }

        // ── PRIORITY 4: proximity docking ───────────────────────────────────────
        // When the mouse enters the edge band of a group (but the joystick centre was
        // not used), automatically activate the appropriate split zone.
        if (ProximityDockingEnabled && hoveredGroup != null)
        {
            int band = (int)(ProximityBandWidth * _uiScale);
            var gb   = hoveredGroup.LayoutBounds;

            DockZone proximityZone = DockZone.None;
            if      (mousePosition.X - gb.X      < band)
            {
                proximityZone = DockZone.Left;
            }
            else if (gb.Right - mousePosition.X   < band)
            {
                proximityZone = DockZone.Right;
            }
            else if (mousePosition.Y - gb.Y       < band)
            {
                proximityZone = DockZone.Top;
            }
            else if (gb.Bottom - mousePosition.Y  < band)
            {
                proximityZone = DockZone.Bottom;
            }

            if (proximityZone != DockZone.None)
            {
                var forbidden = CurrentDrag?.DraggedPanel != null && hoveredGroup.GroupNode != null
                    ? GetForbiddenZones(CurrentDrag.DraggedPanel, hoveredGroup.GroupNode)
                    : System.Linq.Enumerable.Empty<DockZone>();

                if (!forbidden.Contains(proximityZone))
                {
                    var proximityTarget = GetDropTargetForZone(hoveredGroup, proximityZone, mousePosition);
                    if (proximityTarget != null)
                    {
                        CurrentDropTarget = proximityTarget;
                        ShowPreview(proximityTarget.PreviewRect);
                        return;
                    }
                }
            }
        }

        // ── No active drop target ────────────────────────────────────────────────
        CurrentDropTarget = null;
        HidePreview();
    }

    /// <summary>
    /// Performs the drop operation at the end of a drag.
    /// </summary>
    private void PerformDrop()
    {
        if (!IsDragging)
        {
            return;
        }

        try
        {
            // Use the CurrentDropTarget that was calculated during the last drag update
            // Don't recalculate here as the layout may have changed
            var dropTarget = CurrentDropTarget;

            if (dropTarget != null && dropTarget.Zone != DockZone.None)
            {
                ExecuteDrop(CurrentDrag, dropTarget);
            }
            else if (CurrentDrag.HasExceededThreshold
                     && CurrentDrag.SourceFloatingWindow == null  // not already floating
                     && CurrentDrag.DraggedPanel?.CanFloat == true
                     && !LayoutBounds.Contains(_lastMousePosition))
            {
                // Mouse released outside the host with no valid drop target → detach to floating
                DetachToFloating(CurrentDrag.DraggedPanel, _lastMousePosition);
            }
            // Otherwise: the tab stays in its source group (e.g. released inside host but off-indicator,
            // or is already a floating panel with no new drop target).
        }
        finally
        {
            // Always cleanup drag state via public EndDrag method
            EndDrag();
        }
    }

    /// <summary>
    /// Begins a drag operation for the specified panel.
    /// </summary>
    /// <param name="panel">The panel being dragged.</param>
    /// <param name="sourceGroup">The tab group from which the panel is being dragged.</param>
    /// <param name="startPos">The screen-space position where the drag started.</param>
    /// <param name="sourceItem">The visual tab item that initiated the drag.</param>
    /// <param name="sourceFloatingWindow">
    /// The floating window that contains the panel, or null when dragging from the docked layout.
    /// When non-null, a successful drop will move the panel back into the host and
    /// close the floating window if it becomes empty.
    /// </param>
    public void BeginDrag(
        DockPanelNode panel,
        DockTabGroupNode sourceGroup,
        Point startPos,
        MGDockTabItem sourceItem,
        MGFloatingDockWindow sourceFloatingWindow = null)
    {
        if (panel == null)
        {
            throw new ArgumentNullException(nameof(panel));
        }

        if (sourceGroup == null)
        {
            throw new ArgumentNullException(nameof(sourceGroup));
        }

        if (sourceItem == null)
        {
            throw new ArgumentNullException(nameof(sourceItem));
        }

        // Create drag data (visuals will activate after threshold is exceeded)
        CurrentDrag = new DockDragData(panel, sourceGroup, startPos, sourceItem)
        {
            HasExceededThreshold    = false,
            SourceFloatingWindow    = sourceFloatingWindow
        };

        // Don't provide visual feedback yet - wait for threshold
        // sourceItem.Opacity will be set when threshold is exceeded

        // Initialize preview calculation and mouse-position tracking
        _lastPreviewCalculation = startPos;
        _lastMousePosition      = startPos;
    }



    /// <summary>
    /// Ends the current drag operation and performs the drop.
    /// </summary>
    public void EndDrag()
    {
        if (CurrentDrag == null)
        {
            return;
        }

        // Restore visual state
        if (CurrentDrag.SourceTabItem != null)
        {
            CurrentDrag.SourceTabItem.Opacity = 1.0f;
        }

        // Hide preview
        HidePreview();

        // Hide drop indicators (joystick + host-edge)
        _dropIndicators.Hide();
        _dropIndicators.HideHostEdge();
        _lastHoveredGroup = null;

        // Clear drag state
        CurrentDrag = null;
        CurrentDropTarget = null;
    }

    /// <summary>
    /// Cancels the current drag operation without performing a drop.
    /// </summary>
    public void CancelDrag()
    {
        if (CurrentDrag == null)
        {
            return;
        }

        // Restore visual state
        if (CurrentDrag.SourceTabItem != null)
        {
            CurrentDrag.SourceTabItem.Opacity = 1.0f;
        }

        // Hide preview
        HidePreview();

        // Hide drop indicators (joystick + host-edge)
        _dropIndicators.Hide();
        _dropIndicators.HideHostEdge();
        _lastHoveredGroup = null;

        // Clear drag state
        CurrentDrag = null;
        CurrentDropTarget = null;
    }

    /// <summary>
    /// Executes the drop operation by applying the appropriate docking operation.
    /// </summary>
    /// <param name="drag">The drag data containing the dragged panel.</param>
    /// <param name="target">The drop target containing the target node and zone.</param>
    private void ExecuteDrop(DockDragData drag, DockDropTarget target)
    {
        if (drag == null || target == null)
        {
            return;
        }

        var panel      = drag.DraggedPanel;
        var targetNode = target.TargetNode;

        // ── If panel comes from a floating window, detach it first ──────────────
        if (drag.SourceFloatingWindow != null)
        {
            var floatingSource = drag.SourceFloatingWindow;

            // Remove the panel from the floating window's model group.
            // This must happen BEFORE any DockOperation call so that DockOperation's
            // "remove from current parent" logic does not try to clean up a group that
            // is outside the host's LayoutModel.
            floatingSource.GroupNode.RemovePanelById(panel.Id);

            // Close the floating window if it is now empty
            if (floatingSource.GroupNode.IsEmpty)
            {
                CloseFloatingWindow(floatingSource);
            }

            // Register the panel back in the host (it was never in _panelRegistry
            // while floating, so there is no duplicate-key issue).
            if (!_panelRegistry.ContainsKey(panel.Id))
            {
                _panelRegistry[panel.Id] = panel;
                PanelAdded?.Invoke(this, panel);
            }
        }

        switch (target.Zone)
        {
            case DockZone.Center:
                // Dock as tab
                if (targetNode is DockTabGroupNode targetGroup)
                {
                    // Check if this is a reorder operation (same group, source in host)
                    if (drag.SourceGroup == targetGroup && drag.SourceFloatingWindow == null)
                    {
                        // Reorder within the same group
                        DockOperation.ReorderTab(LayoutModel, panel, targetGroup, target.TabIndex);
                    }
                    else
                    {
                        // Move to different group (or from floating → host)
                        int insertIndex = target.TabIndex >= 0 ? target.TabIndex : -1;
                        DockOperation.DockAsTab(LayoutModel, panel, targetGroup, insertIndex);
                    }
                }
                break;

            case DockZone.Left:
            case DockZone.Right:
            case DockZone.Top:
            case DockZone.Bottom:
                // Split dock: remove from source and create split
                if (target.IsHostEdge)
                {
                    // Host-edge drop: insert a new root-level split
                    DockOperation.SplitDockAtRoot(LayoutModel, panel, target.Zone,
                        DockDropCalculator.HostEdgePreviewRatio);
                }
                else
                {
                    DockOperation.SplitDock(LayoutModel, panel, targetNode, target.Zone);
                }
                break;

            case DockZone.None:
            default:
                // Invalid zone, do nothing
                break;
        }

        // Rebuild visual tree to reflect changes
        RebuildVisualTree();
    }

    /// <summary>
    /// Registers a panel for use in the docking system.
    /// The panel can then be added to the layout via DockOperation methods.
    /// </summary>
    /// <param name="panel">The panel to register.</param>
    public void RegisterPanel(DockPanelNode panel)
    {
        if (panel == null)
        {
            throw new ArgumentNullException(nameof(panel));
        }

        if (_panelRegistry.ContainsKey(panel.Id))
        {
            throw new InvalidOperationException($"A panel with ID '{panel.Id}' is already registered.");
        }

        _panelRegistry[panel.Id] = panel;
        PanelAdded?.Invoke(this, panel);
        _dockableRegistry?.NotifyShown(panel.Id);
    }

    /// <summary>
    /// Removes a panel from the docking system by ID.
    /// </summary>
    /// <param name="panelId">The ID of the panel to remove.</param>
    /// <returns>True if the panel was removed, false if not found.</returns>
    public bool RemovePanel(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            return false;
        }

        if (!_panelRegistry.TryGetValue(panelId, out var panel))
        {
            return false;
        }

        // Remove from layout model
        DockOperation.RemovePanel(LayoutModel, panel);

        // Remove from registry
        _panelRegistry.Remove(panelId);
            
        PanelRemoved?.Invoke(this, panel);
        // Treat removal as close (user explicitly removed the panel)
        _dockableRegistry?.NotifyClosed(panelId);

        return true;
    }

    /// <summary>
    /// Finds a registered panel by ID.
    /// </summary>
    /// <param name="id">The panel ID.</param>
    /// <returns>The panel node, or null if not found.</returns>
    public DockPanelNode FindPanel(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        _panelRegistry.TryGetValue(id, out var panel);
        return panel;
    }

    /// <summary>
    /// Gets all registered panels.
    /// </summary>
    /// <returns>Collection of all registered panels.</returns>
    public IEnumerable<DockPanelNode> GetAllPanels()
    {
        return _panelRegistry.Values;
    }

    // ─── 14.3 Ctrl+Tab panel switcher ───────────────────────────────────────

    /// <summary>
    /// Cycles the active panel forward (Ctrl+Tab) or backward (Ctrl+Shift+Tab) through
    /// all visible panels: docked tab groups, auto-hidden panels, and floating panels.
    /// </summary>
    /// <param name="forward">True to move to the next panel, false to move to the previous.</param>
    private void CyclePanel(bool forward)
    {
        // Collect all reachable panels in a deterministic order:
        // 1. Docked panels (tab-group tree order, panel order within each group)
        // 2. Auto-hidden panels (Left, Top, Right, Bottom)
        // 3. Floating panels
        var allPanels = new List<DockPanelNode>();

        // Docked panels
        allPanels.AddRange(GetAllTabGroups().SelectMany(g => g.Panels));

        // Auto-hidden panels (by side)
        if (LayoutModel != null)
        {
            foreach (AutoHideSide side in new[] { AutoHideSide.Left, AutoHideSide.Top, AutoHideSide.Right, AutoHideSide.Bottom })
            {
                allPanels.AddRange(LayoutModel.GetAutoHidePanels(side));
            }
        }

        // Floating panels
        foreach (var floatWin in _floatingWindows)
        {
            allPanels.AddRange(floatWin.GroupNode.Panels);
        }

        if (allPanels.Count <= 1)
        {
            return;
        }

        var currentId = ActiveDockable?.Id;
        int currentIndex = currentId != null ? allPanels.FindIndex(p => p.Id == currentId) : -1;

        int nextIndex;
        if (currentIndex < 0)
        {
            nextIndex = forward ? 0 : allPanels.Count - 1;
        }
        else if (forward)
        {
            nextIndex = (currentIndex + 1) % allPanels.Count;
        }
        else
        {
            nextIndex = (currentIndex - 1 + allPanels.Count) % allPanels.Count;
        }

        var nextPanel = allPanels[nextIndex];
        ActivatePanel(nextPanel);

        // Always update ActiveDockable directly.  If nextPanel was already the active panel
        // in its tab group, SetActivePanel() is a no-op and fires no PropertyChanged, so
        // OnTabGroupPropertyChanged would never update ActiveDockable — causing every
        // subsequent Ctrl+Tab call to compute the same currentIndex and stay stuck.
        ActiveDockable = nextPanel;
    }

    /// <summary>
    /// Activates a panel regardless of its current location (docked, auto-hidden, or floating).
    /// </summary>
    private void ActivatePanel(DockPanelNode panel)
    {
        if (panel == null)
        {
            return;
        }

        // Check if panel is in a docked tab group
        if (panel.Parent is DockTabGroupNode parentGroup
            && GetAllTabGroups().Contains(parentGroup))
        {
            parentGroup.SetActivePanel(panel.Id);
            return;
        }

        // Check if panel is auto-hidden
        if (LayoutModel != null)
        {
            foreach (AutoHideSide side in new[] { AutoHideSide.Left, AutoHideSide.Top, AutoHideSide.Right, AutoHideSide.Bottom })
            {
                if (LayoutModel.GetAutoHidePanels(side).Any(p => p.Id == panel.Id))
                {
                    ShowAutoHideDrawer(panel);
                    return;
                }
            }
        }

        // Check if panel is in a floating window
        foreach (var floatWin in _floatingWindows)
        {
            if (floatWin.GroupNode.Panels.Any(p => p.Id == panel.Id))
            {
                floatWin.GroupNode.SetActivePanel(panel.Id);
                return;
            }
        }
    }

    /// <summary>
    /// Opens a context-menu pop-up that lists every visible docked panel, allowing
    /// the user to jump to any panel with a single click.
    /// </summary>
    public void ShowCtrlTabSwitcher()
    {
        var allPanels = GetAllPanels().ToList();
        if (allPanels.Count == 0)
        {
            return;
        }

        var menu = new MGContextMenu(ParentWindow, "");
        menu.CanContextMenuOpen = true;

        foreach (var panel in allPanels)
        {
            var capturedId    = panel.Id;
            var capturedTitle = panel.Title ?? capturedId;
            menu.AddButton(capturedTitle, _ =>
            {
                ShowDockable(capturedId);
            });
        }

        var lb = LayoutBounds;
        int cx = lb.X + lb.Width  / 2;
        int cy = lb.Y + lb.Height / 2;
        ParentWindow.Desktop.TryOpenContextMenu(menu,
            new Microsoft.Xna.Framework.Rectangle(cx, cy, 1, 1));
    }

    /// <summary>
    /// Shows a registered dockable by its ID. If it is already visible, activates its tab.
    /// If it is hidden, creates it and adds it to the layout next to the most recently used tab group,
    /// or at the root if no group exists.
    /// </summary>
    /// <param name="dockableId">The ID of the dockable to show.</param>
    /// <returns>True if the dockable was shown or activated, false if not found in the registry.</returns>
    public bool ShowDockable(string dockableId)
    {
        if (string.IsNullOrWhiteSpace(dockableId))
        {
            return false;
        }

        // Check if it is already visible
        if (_panelRegistry.TryGetValue(dockableId, out var existingPanel))
        {
            // Already in layout → activate it
            var parentGroup = existingPanel.Parent as DockTabGroupNode;
            if (parentGroup != null)
            {
                parentGroup.SetActivePanel(dockableId);
            }
            return true;
        }

        // Not visible → need to find definition and add it
        if (_dockableRegistry == null || !_dockableRegistry.TryGetById(dockableId, out var definition))
        {
            return false;
        }

        var panel = definition.CreatePanelNode();

        // Find a suitable tab group to add the panel into
        var allGroups = GetAllTabGroups().ToList();
        DockTabGroupNode targetGroup = allGroups.FirstOrDefault();

        if (targetGroup != null)
        {
            DockOperation.DockAsTab(LayoutModel, panel, targetGroup);
        }
        else if (LayoutModel.RootNode == null)
        {
            // Empty layout, start fresh with this panel
            var newGroup = new DockTabGroupNode();
            newGroup.AddPanel(panel, -1);
            LayoutModel.RootNode = newGroup;
        }
        else
        {
            // Layout exists but no tab group found → split the root
            DockOperation.SplitDock(LayoutModel, panel, LayoutModel.RootNode, DockZone.Right);
        }

        RegisterPanel(panel);
        return true;
    }

    #region Floating Windows — management

    /// <summary>
    /// Detaches <paramref name="panel"/> from the docked layout and wraps it in a new
    /// <see cref="MGFloatingDockWindow"/> centred on <paramref name="dropPosition"/>.
    /// The method removes the panel from the host's layout model and panel registry,
    /// then creates the floating window and registers it as a nested window of the
    /// host's parent <see cref="MGWindow"/>.
    /// </summary>
    /// <param name="panel">The panel to detach.</param>
    /// <param name="dropPosition">
    /// The screen-space position where the user released the mouse (used to position the window).
    /// </param>
    public MGFloatingDockWindow DetachToFloating(DockPanelNode panel, Point dropPosition)
    {
        if (panel == null)
        {
            throw new ArgumentNullException(nameof(panel));
        }

        // Remove from the host panel registry first (before model cleanup)
        _panelRegistry.Remove(panel.Id);

        // Remove from the docked layout model (triggers RebuildVisualTree via LayoutChanged)
        DockOperation.RemovePanel(LayoutModel, panel);

        const int defaultFloatWidth  = 320;
        const int defaultFloatHeight = 260;
        int left = dropPosition.X - defaultFloatWidth  / 2;
        int top  = dropPosition.Y - defaultFloatHeight / 2;

        var floatWin = new MGFloatingDockWindow(this, panel, left, top, defaultFloatWidth, defaultFloatHeight);
        _floatingWindows.Add(floatWin);
        ParentWindow.AddNestedWindow(floatWin);

        // Re-sync: the floating panel should still appear "visible" to the DockableRegistry
        SyncRegistryVisibility();

        return floatWin;
    }

    /// <summary>
    /// Creates a floating window for <paramref name="panel"/> at the given position without
    /// removing the panel from the layout model first (used when the panel is already outside
    /// the layout, e.g. the source is a floating window moving to a new floating position — 
    /// reserved for future use).  Normal detach from the docked layout should use
    /// <see cref="DetachToFloating"/>.
    /// </summary>
    public MGFloatingDockWindow CreateFloatingWindow(DockPanelNode panel, int left, int top, int width = 320, int height = 260)
    {
        if (panel == null)
        {
            throw new ArgumentNullException(nameof(panel));
        }

        var floatWin = new MGFloatingDockWindow(this, panel, left, top, width, height);
        _floatingWindows.Add(floatWin);
        ParentWindow.AddNestedWindow(floatWin);
        SyncRegistryVisibility();
        return floatWin;
    }

    /// <summary>
    /// Closes a floating window: removes it from the floating list and from the parent
    /// window's nested windows list.
    /// </summary>
    public void CloseFloatingWindow(MGFloatingDockWindow window)
    {
        if (window == null)
        {
            return;
        }

        _floatingWindows.Remove(window);
        ParentWindow.RemoveNestedWindow(window);
        SyncRegistryVisibility();
    }

    /// <summary>
    /// Called by <see cref="MGFloatingDockWindow"/> when the user closes a panel inside it
    /// via the close button.  Notifies the dockable registry that the panel was closed.
    /// </summary>
    internal void NotifyFloatingPanelClosed(DockPanelNode panel)
    {
        if (panel == null)
        {
            return;
        }

        PanelRemoved?.Invoke(this, panel);
        _dockableRegistry?.NotifyClosed(panel.Id);
        SyncRegistryVisibility();
    }

    #endregion Floating Windows — management

    #region Auto-Hide

    private const int _autoHideStripThickness = MGDockAutoHideStrip.StripThickness;

    // ── Layout helpers ─────────────────────────────────────────────────

    private Microsoft.Xna.Framework.Rectangle GetStripBounds(AutoHideSide side, Microsoft.Xna.Framework.Rectangle avail)
    {
        if (LayoutModel == null || !LayoutModel.HasAutoHidePanels(side))
        {
            return new Microsoft.Xna.Framework.Rectangle(avail.X, -10000, 0, 0);
        }

        // Top / Bottom strips take the full width.
        // Left / Right strips are inset vertically by any active Top/Bottom strip to avoid
        // corner overlaps when multiple sides are active simultaneously.
        int topInset    = LayoutModel.HasAutoHidePanels(AutoHideSide.Top)    ? _autoHideStripThickness : 0;
        int bottomInset = LayoutModel.HasAutoHidePanels(AutoHideSide.Bottom) ? _autoHideStripThickness : 0;

        return side switch
        {
            AutoHideSide.Left   => new Microsoft.Xna.Framework.Rectangle(avail.X, avail.Y + topInset, _autoHideStripThickness, avail.Height - topInset - bottomInset),
            AutoHideSide.Right  => new Microsoft.Xna.Framework.Rectangle(avail.Right - _autoHideStripThickness, avail.Y + topInset, _autoHideStripThickness, avail.Height - topInset - bottomInset),
            AutoHideSide.Top    => new Microsoft.Xna.Framework.Rectangle(avail.X, avail.Y, avail.Width, _autoHideStripThickness),
            AutoHideSide.Bottom => new Microsoft.Xna.Framework.Rectangle(avail.X, avail.Bottom - _autoHideStripThickness, avail.Width, _autoHideStripThickness),
            _                   => new Microsoft.Xna.Framework.Rectangle(avail.X, -10000, 0, 0)
        };
    }

    private Microsoft.Xna.Framework.Rectangle GetDrawerBounds(Microsoft.Xna.Framework.Rectangle avail)
    {
        var panel = _autoHideDrawer?.ActivePanel;
        if (panel == null)
        {
            return new Microsoft.Xna.Framework.Rectangle(avail.X, -10000, 0, 0);
        }

        int ds = panel.DrawerSize;
        int st = _autoHideStripThickness;
        return panel.AutoHideSide switch
        {
            AutoHideSide.Left   => new Microsoft.Xna.Framework.Rectangle(avail.X + st,           avail.Y,              ds, avail.Height),
            AutoHideSide.Right  => new Microsoft.Xna.Framework.Rectangle(avail.Right - st - ds,  avail.Y,              ds, avail.Height),
            AutoHideSide.Top    => new Microsoft.Xna.Framework.Rectangle(avail.X,                avail.Y + st,          avail.Width, ds),
            AutoHideSide.Bottom => new Microsoft.Xna.Framework.Rectangle(avail.X,                avail.Bottom - st - ds, avail.Width, ds),
            _                   => new Microsoft.Xna.Framework.Rectangle(avail.X, -10000, 0, 0)
        };
    }

    // ── Public API ─────────────────────────────────────────────────────

    /// <summary>
    /// Removes <paramref name="panel"/> from the docked layout and places it in the auto-hide
    /// strip on the appropriate edge (inferred from its current position).
    /// </summary>
    public void UnpinPanel(DockPanelNode panel)
    {
        if (panel == null || !panel.CanAutoHide)
        {
            return;
        }

        AutoHideSide side = InferAutoHideSide(panel);

        // Snapshot the parent group NOW while panel.Parent is still set.
        // DockOperation.RemovePanel clears it, so we must do this before that call.
        panel.AutoHideReturnGroup = panel.Parent as DockTabGroupNode;

        // Also snapshot the exact split position so we can restore it faithfully when
        // the original group no longer exists after the panel (alone in its group) is removed.
        if (panel.AutoHideReturnGroup?.Parent is DockSplitNode splitParent)
        {
            bool isFirst = splitParent.FirstChild == panel.AutoHideReturnGroup;
            panel.AutoHideReturnZone = splitParent.Orientation == MGUI.Core.UI.Orientation.Horizontal
                ? (isFirst ? DockZone.Left  : DockZone.Right)
                : (isFirst ? DockZone.Top   : DockZone.Bottom);
            // Fraction of the split this child occupied: SplitRatio = firstChild share.
            panel.AutoHideReturnSplitRatio = isFirst ? splitParent.SplitRatio : 1f - splitParent.SplitRatio;
        }
        else
        {
            panel.AutoHideReturnZone        = DockZone.None;
            panel.AutoHideReturnSplitRatio  = null;
        }

        // Suspend model-change events so we get exactly one visual-tree rebuild at the end
        _layoutModel.LayoutChanged -= OnLayoutModelChanged;
        try
        {
            DockOperation.RemovePanel(LayoutModel, panel);
            LayoutModel.AddToAutoHide(panel, side);
        }
        finally
        {
            _layoutModel.LayoutChanged += OnLayoutModelChanged;
        }

        RefreshAutoHideStrips();
        RebuildVisualTree();
        DockLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Moves <paramref name="panel"/> from the auto-hide store back into the docked layout,
    /// placing it in the first available tab group (or creating a new one).
    /// </summary>
    public void RepinPanel(DockPanelNode panel)
    {
        if (panel == null)
        {
            return;
        }

        HideAutoHideDrawer();

        _layoutModel.LayoutChanged -= OnLayoutModelChanged;
        try
        {
            LayoutModel.RemoveFromAutoHide(panel);

            // The panel recorded its own original group when it was unpinned.
            var returnGroup = panel.AutoHideReturnGroup;
            panel.AutoHideReturnGroup = null;   // clear — no longer needed

            bool restoredToOriginal = false;
            if (returnGroup != null && GetAllTabGroups().Contains(returnGroup))
            {
                // Original group still exists — slip back in as a tab.
                DockOperation.DockAsTab(LayoutModel, panel, returnGroup);
                restoredToOriginal = true;
            }

            if (!restoredToOriginal)
            {
                // Fall back: recreate the split using the snapshotted zone and ratio.
                // Map AutoHideReturnZone first; if it wasn't set, infer from AutoHideSide.
                DockZone fallbackZone = panel.AutoHideReturnZone != DockZone.None
                    ? panel.AutoHideReturnZone
                    : panel.AutoHideSide switch
                    {
                        AutoHideSide.Left   => DockZone.Left,
                        AutoHideSide.Right  => DockZone.Right,
                        AutoHideSide.Top    => DockZone.Top,
                        AutoHideSide.Bottom => DockZone.Bottom,
                        _                   => DockZone.Right
                    };
                float fallbackRatio = panel.AutoHideReturnSplitRatio ?? DockDropCalculator.HostEdgePreviewRatio;

                // Clear saved position metadata
                panel.AutoHideReturnZone       = DockZone.None;
                panel.AutoHideReturnSplitRatio = null;

                if (LayoutModel.RootNode == null)
                {
                    var newGroup = new DockTabGroupNode();
                    newGroup.AddPanel(panel, -1);
                    LayoutModel.RootNode = newGroup;
                }
                else
                {
                    DockOperation.SplitDockAtRoot(LayoutModel, panel, fallbackZone, fallbackRatio);
                }
            }
        }
        finally
        {
            _layoutModel.LayoutChanged += OnLayoutModelChanged;
        }

        RefreshAutoHideStrips();
        RebuildVisualTree();
        DockLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Shows the auto-hide drawer for the given panel.
    /// Calling this while the same panel is already open closes the drawer (toggle).
    /// </summary>
    public void ShowAutoHideDrawer(DockPanelNode panel)
    {
        if (panel == null)
        {
            return;
        }

        // Toggle: clicking the same languette again closes it
        if (_autoHideDrawer.ActivePanel == panel && _autoHideDrawer.Visibility == Visibility.Visible)
        {
            HideAutoHideDrawer();
            return;
        }

        _autoHideDrawer.Side       = panel.AutoHideSide;
        _autoHideDrawer.ActivePanel = panel;
        _autoHideDrawer.Visibility  = Visibility.Visible;
        InvalidateLayout();
    }

    /// <summary>Closes the auto-hide drawer without re-pinning the panel.</summary>
    public void HideAutoHideDrawer()
    {
        if (_autoHideDrawer?.Visibility == Visibility.Collapsed)
        {
            return;
        }

        _autoHideDrawer.ActivePanel = null;
        _autoHideDrawer.Visibility  = Visibility.Collapsed;
        InvalidateLayout();
    }

    // ── Private helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Infers which edge of the host <paramref name="panel"/> should auto-hide to, based on the
    /// current position of its tab group relative to the host centre.
    /// </summary>
    private AutoHideSide InferAutoHideSide(DockPanelNode panel)
    {
        foreach (var tabGroup in GetAllVisibleTabGroups())
        {
            if (tabGroup.GroupNode?.Panels.Any(p => p.Id == panel.Id) == true)
            {
                var gb = tabGroup.LayoutBounds;
                var hb = LayoutBounds;
                if (hb.Width == 0 || hb.Height == 0)
                {
                    return AutoHideSide.Left;
                }

                float cx = (gb.X + gb.Width  * 0.5f - hb.X) / hb.Width;
                float cy = (gb.Y + gb.Height * 0.5f - hb.Y) / hb.Height;

                float dL = cx;
                float dR = 1f - cx;
                float dT = cy;
                float dB = 1f - cy;
                float min = Math.Min(Math.Min(dL, dR), Math.Min(dT, dB));
                if (min == dL)
                {
                    return AutoHideSide.Left;
                }

                if (min == dR)
                {
                    return AutoHideSide.Right;
                }

                if (min == dT)
                {
                    return AutoHideSide.Top;
                }

                return AutoHideSide.Bottom;
            }
        }
        return AutoHideSide.Left;
    }

    /// <summary>Rebuilds every strip's button list from the current auto-hide store.</summary>
    private void RefreshAutoHideStrips()
    {
        if (LayoutModel == null)
        {
            return;
        }

        foreach (AutoHideSide side in _autoHideStrips.Keys)
        {
            var panels = LayoutModel.GetAutoHidePanels(side);
            _autoHideStrips[side].Refresh(panels);
            _autoHideStrips[side].Visibility = panels.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        InvalidateLayout();
    }

    /// <summary>
    /// Permanently closes an auto-hidden panel: removes it from the auto-hide store,
    /// the panel registry, and notifies the dockable registry.
    /// </summary>
    private void CloseAutoHidePanel(DockPanelNode panel)
    {
        if (panel == null)
        {
            return;
        }

        HideAutoHideDrawer();
        panel.AutoHideReturnGroup = null;  // not going back to layout
        LayoutModel?.RemoveFromAutoHide(panel);
        _panelRegistry.Remove(panel.Id);
        PanelRemoved?.Invoke(this, panel);
        _dockableRegistry?.NotifyClosed(panel.Id);
        RefreshAutoHideStrips();
        SyncRegistryVisibility();
    }

    #endregion Auto-Hide

    /// <returns>Collection of all DockTabGroupNode instances in the visual tree.</returns>
    public IEnumerable<DockTabGroupNode> GetAllTabGroups()
    {
        return LayoutModel?.GetAllTabGroups() ?? Enumerable.Empty<DockTabGroupNode>();
    }

    /// <summary>
    /// Returns the tab group designated as the Document Area, or null if none is set.
    /// </summary>
    public DockTabGroupNode GetDocumentArea()
    {
        return GetAllTabGroups().FirstOrDefault(g => g.IsDocumentArea);
    }

    /// <summary>
    /// Designates a specific tab group as the Document Area.
    /// Clears the flag from any previous Document Area group.
    /// </summary>
    /// <param name="group">The group to designate. Pass null to clear without assigning a new one.</param>
    public void SetDocumentArea(DockTabGroupNode group)
    {
        // Clear old designation
        foreach (var g in GetAllTabGroups())
        {
            if (g.IsDocumentArea && g != group)
            {
                g.IsDocumentArea = false;
            }
        }
        if (group != null)
        {
            group.IsDocumentArea = true;
        }
    }

    /// <summary>
    /// Checks whether a panel of the given type can be docked into the specified group,
    /// taking into account Document Area rules.
    /// Rules:
    /// - Documents can only tab-dock into the Document Area (if one exists).
    /// - Tools cannot be tab-docked into the Document Area.
    /// Split-docking is always allowed regardless of type.
    /// </summary>
    public bool CanDockIntoGroup(DockableType panelType, DockTabGroupNode targetGroup, DockZone zone)
    {
        if (zone != DockZone.Center)
        {
            return true; // Split docks are always allowed
        }

        var documentArea = GetDocumentArea();
        if (documentArea == null)
        {
            return true; // No document area defined → no type restriction
        }

        if (panelType == DockableType.Document)
        {
            return targetGroup == documentArea; // Documents only go to DocumentArea
        }

        if (panelType == DockableType.Tool)
        {
            return targetGroup != documentArea; // Tools cannot go to DocumentArea
        }

        return true;
    }

    // All zones checked by docking rules
    private static readonly DockZone[] _allDropZones =
    {
        DockZone.Left, DockZone.Right, DockZone.Top, DockZone.Bottom, DockZone.Center
    };

    /// <summary>
    /// Checks all docking rules for the given panel dropped into <paramref name="targetGroup"/>
    /// at <paramref name="zone"/>:
    /// <list type="bullet">
    ///   <item>Document / Tool area restrictions (<see cref="CanDockIntoGroup"/>).</item>
    ///   <item><see cref="DockPanelNode.AllowedZones"/> allow-list.</item>
    ///   <item><see cref="DockPanelNode.Family"/> same-family restriction for tab-docking.</item>
    /// </list>
    /// Returns <c>true</c> when the drop is permitted.
    /// </summary>
    public bool CanDockTo(DockPanelNode panel, DockTabGroupNode targetGroup, DockZone zone)
    {
        if (panel == null || targetGroup == null)
        {
            return true;
        }

        // 1. Document / Tool area rules (existing)
        if (!CanDockIntoGroup(panel.DockableType, targetGroup, zone))
        {
            return false;
        }

        // 2. AllowedZones restriction
        var allowedZones = panel.AllowedZones;
        if (allowedZones != null && !allowedZones.Contains(zone))
        {
            return false;
        }

        // 3. Family restriction — only for tab-docking (Center)
        if (zone == DockZone.Center)
        {
            string family = panel.Family;
            if (family != null)
            {
                foreach (var p in targetGroup.Panels)
                {
                    if (p.Id == panel.Id)
                    {
                        continue; // skip self
                    }

                    if (p.Family != null && p.Family != family)
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Returns the set of <see cref="DockZone"/>s that are forbidden for
    /// <paramref name="panel"/> when hovering <paramref name="targetGroup"/>.
    /// Used to disable visual indicators for illegal zones.
    /// </summary>
    private HashSet<DockZone> GetForbiddenZones(DockPanelNode panel, DockTabGroupNode targetGroup)
    {
        var forbidden = new HashSet<DockZone>();
        if (panel == null || targetGroup == null)
        {
            return forbidden;
        }

        foreach (var z in _allDropZones)
        {
            if (!CanDockTo(panel, targetGroup, z))
            {
                forbidden.Add(z);
            }
        }

        return forbidden;
    }

    /// <summary>
    /// Rebuilds the entire visual tree from the layout model.
    /// Call this after making structural changes to the layout.
    /// </summary>
    public void RebuildVisualTree()
    {
        // Detach all existing tab group visuals from their model nodes before replacing the
        // visual tree.  Without this, each rebuild leaves the old MGDockTabGroup instances
        // permanently subscribed to model events, causing a growing chain of orphaned handlers.
        foreach (var oldVisual in _activeTabGroupVisuals)
        {
            oldVisual.Detach();
        }

        _activeTabGroupVisuals.Clear();

        if (LayoutModel?.RootNode == null)
        {
            // No layout defined, show placeholder or empty content
            SetContent(CreateEmptyPlaceholder());
            SyncRegistryVisibility();
            return;
        }

        try
        {
            // ── Maximize mode: show only the maximized group ───────────────
            string maximizedId = CurrentMaximizedGroupId;
            if (maximizedId != null)
            {
                // Find the tab group node with the matching ID
                var maximizedGroup = LayoutModel.GetAllTabGroups()
                    .FirstOrDefault(g => g.Id == maximizedId);

                if (maximizedGroup != null)
                {
                    var fullscreenVisual = BuildTabGroup(maximizedGroup, isMaximized: true);
                    SetContent(fullscreenVisual);
                    SyncRegistryVisibility();
                    return;
                }

                // Maximized group no longer exists — pop and fall through to normal rebuild
                _maximizeStack.Pop();
            }

            // ── Normal mode ────────────────────────────────────────────────
            MGElement visualRoot = BuildVisualTree(LayoutModel.RootNode);
            SetContent(visualRoot);
            SyncRegistryVisibility();
        }
        catch (Exception ex)
        {
            // If building fails, show error placeholder
            SetContent(CreateErrorPlaceholder(ex.Message));
        }
    }

    /// <summary>
    /// Collects all panel IDs currently present in the docked layout AND in floating windows,
    /// then syncs them to the registry so they are all reported as "visible".
    /// Floating panels are still considered visible (just not docked).
    /// </summary>
    private void SyncRegistryVisibility()
    {
        if (_dockableRegistry == null)
        {
            return;
        }

        var floatingIds = _floatingWindows
            .SelectMany(w => w.GroupNode.Panels)
            .Select(p => p.Id);

        // Auto-hidden panels are still "visible" to the registry (just folded away)
        var autoHideIds = LayoutModel?.GetAllAutoHidePanels().Select(p => p.Id)
                          ?? Enumerable.Empty<string>();

        _dockableRegistry.SyncVisibility(_panelRegistry.Keys.Concat(floatingIds).Concat(autoHideIds));
    }

    /// <summary>
    /// Recursively builds the visual tree from a dock node.
    /// </summary>
    /// <param name="node">The node to build visuals for.</param>
    /// <returns>The MGElement representing this node and its children.</returns>
    private MGElement BuildVisualTree(DockNode node)
    {
        if (node == null)
        {
            return CreateEmptyPlaceholder();
        }

        switch (node)
        {
            case DockSplitNode splitNode:
                return BuildSplitContainer(splitNode);

            case DockTabGroupNode tabGroupNode:
                return BuildTabGroup(tabGroupNode);

            case DockPanelNode panelNode:
                // A panel node shouldn't be directly in the tree, it should be in a tab group
                // Create a tab group with just this panel
                var tempGroup = new DockTabGroupNode();
                tempGroup.AddPanel(panelNode, -1);
                return BuildTabGroup(tempGroup);

            default:
                return CreateErrorPlaceholder($"Unknown node type: {node.GetType().Name}");
        }
    }

    /// <summary>
    /// Builds a split container visual from a DockSplitNode.
    /// </summary>
    private MGElement BuildSplitContainer(DockSplitNode splitNode)
    {
        var splitContainer = new MGDockSplitContainer(ParentWindow, splitNode.Orientation)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            SplitRatio = splitNode.SplitRatio,
            MinFirstSize = splitNode.MinFirstSize,
            MinSecondSize = splitNode.MinSecondSize,
            ModelNode = splitNode // Link to model for recursive constraints
        };

        // Subscribe to split ratio changes to update the model
        splitContainer.SplitRatioChanged += (sender, newRatio) =>
        {
            if (splitNode.SplitRatio != newRatio)
            {
                splitNode.SplitRatio = newRatio;
            }
        };

        // Recursively build children
        if (splitNode.FirstChild != null)
        {
            splitContainer.FirstChild = BuildVisualTree(splitNode.FirstChild);
        }

        if (splitNode.SecondChild != null)
        {
            splitContainer.SecondChild = BuildVisualTree(splitNode.SecondChild);
        }

        return splitContainer;
    }

    /// <summary>
    /// Builds a tab group visual from a DockTabGroupNode.
    /// </summary>
    /// <param name="tabGroupNode">The model node to bind the visual to.</param>
    /// <param name="isMaximized">
    /// True when this tab group is being built in maximize mode (fills the whole host).
    /// The visual will display a restore button instead of a maximize button.
    /// </param>
    private MGElement BuildTabGroup(DockTabGroupNode tabGroupNode, bool isMaximized = false)
    {
        var tabGroup = new MGDockTabGroup(ParentWindow, tabGroupNode)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
            IsMaximized         = isMaximized
        };

        // Track this visual so RebuildVisualTree can Detach() it later.
        _activeTabGroupVisuals.Add(tabGroup);

        // Subscribe to panel close requests
        tabGroup.PanelCloseRequested += (sender, panelToClose) =>
        {
            if (panelToClose != null && LayoutModel != null)
            {
                // Remove from panel registry BEFORE removing from model,
                // otherwise the registry becomes stale for any caller that
                // inspects it synchronously in a LayoutChanged handler.
                _panelRegistry.Remove(panelToClose.Id);

                // Remove panel from layout model
                DockOperation.RemovePanel(LayoutModel, panelToClose);

                // Notify event subscribers
                PanelRemoved?.Invoke(this, panelToClose);
                _dockableRegistry?.NotifyClosed(panelToClose.Id);

                RebuildVisualTree();
            }
        };

        // Subscribe to float (detach) requests from the context menu
        tabGroup.PanelFloatRequested += (sender, panelToFloat) =>
        {
            if (panelToFloat == null)
            {
                return;
            }

            // Position the floating window roughly at the centre of the host
            var pos = new Microsoft.Xna.Framework.Point(
                LayoutBounds.X + LayoutBounds.Width  / 2,
                LayoutBounds.Y + LayoutBounds.Height / 2);
            DetachToFloating(panelToFloat, pos);
        };

        // Subscribe to pin/unpin toggle requests from the context menu or pin button
        tabGroup.PanelPinToggleRequested += (sender, panelToToggle) =>
        {
            if (panelToToggle == null)
            {
                return;
            }

            if (panelToToggle.IsPinned)
            {
                UnpinPanel(panelToToggle);
            }
            else
            {
                RepinPanel(panelToToggle);
            }
        };

        // Subscribe to maximize / restore requests
        tabGroup.MaximizeRequested += (sender, groupNode) =>
        {
            if (groupNode != null)
            {
                MaximizeGroup(groupNode);
            }
        };

        tabGroup.RestoreRequested += (sender, _) => RestoreLayout();

        return tabGroup;
    }

    /// <summary>
    /// Creates a placeholder element for empty layouts.
    /// </summary>
    private MGElement CreateEmptyPlaceholder()
    {
        var textBlock = new MGTextBlock(ParentWindow, "Empty Docking Layout\n\nAdd panels via RegisterPanel() and DockOperation methods.")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(20).ToThickness()
        };

        return textBlock;
    }

    /// <summary>
    /// Creates an error placeholder element.
    /// </summary>
    private MGElement CreateErrorPlaceholder(string errorMessage)
    {
        var textBlock = new MGTextBlock(ParentWindow, $"Error building docking layout:\n{errorMessage}")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(20).ToThickness()
        };

        return textBlock;
    }

    /// <summary>
    /// Handles layout model changes by rebuilding the visual tree and re-syncing node subscriptions.
    /// </summary>
    private void OnLayoutModelChanged(object sender, EventArgs e)
    {
        SyncNodeSubscriptions();
        RebuildVisualTree();
        DockLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Last root node whose tab-group active-panel changes we subscribed to.</summary>
    private DockNode _subscribedRootNode;

    /// <summary>
    /// Unsubscribes from the previously subscribed root tree and subscribes to the current one.
    /// Only tab-group active-panel changes are tracked here. Structural/layout-affecting model
    /// changes already flow through DockLayoutModel.LayoutChanged, so duplicating the generic
    /// PropertyChanged subscription here would rebuild the host twice for the same mutation.
    /// </summary>
    private void SyncNodeSubscriptions()
    {
        if (_subscribedRootNode != null)
        {
            UnsubscribeFromNode(_subscribedRootNode);
            _subscribedRootNode = null;
        }

        var newRoot = LayoutModel?.RootNode;
        if (newRoot != null)
        {
            SubscribeToNode(newRoot);
            _subscribedRootNode = newRoot;
        }
    }

    /// <summary>
    /// Subscribes to tab-group active-panel changes for a node and its children.
    /// </summary>
    private void SubscribeToNode(DockNode node)
    {
        if (node == null)
        {
            return;
        }

        // Subscribe to children
        foreach (var child in node.GetChildren())
        {
            if (child != null)
            {
                SubscribeToNode(child);
            }
        }

        // Special handling for TabGroupNode to track active panel changes
        if (node is DockTabGroupNode tabGroup)
        {
            tabGroup.PropertyChanged += OnTabGroupPropertyChanged;
        }
    }

    /// <summary>
    /// Unsubscribes from tab-group active-panel changes for a node and its children.
    /// </summary>
    private void UnsubscribeFromNode(DockNode node)
    {
        if (node == null)
        {
            return;
        }

        // Unsubscribe from children
        foreach (var child in node.GetChildren())
        {
            if (child != null)
            {
                UnsubscribeFromNode(child);
            }
        }

        if (node is DockTabGroupNode tabGroup)
        {
            tabGroup.PropertyChanged -= OnTabGroupPropertyChanged;
        }
    }

    private void OnTabGroupPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockTabGroupNode.ActivePanelId) || 
            e.PropertyName == nameof(DockTabGroupNode.ActivePanel))
        {
            if (sender is DockTabGroupNode tabGroup && tabGroup.ActivePanel != null)
            {
                ActivePanelChanged?.Invoke(this, tabGroup.ActivePanel);
                ActiveDockable = tabGroup.ActivePanel;  // 14.1 — track active panel
                _dockableRegistry?.NotifyActivated(tabGroup.ActivePanel.Id);
            }
        }
    }

    #region Drop Zone Calculation

    private DockDropTarget _currentDropTarget;
    private MGDockPreviewOverlay _previewOverlay;
    private MGComponentBase _previewOverlayComponent;
    private MGDockDropIndicators _dropIndicators;
    private MGComponentBase _dropIndicatorsComponent;
    private MGDockTabGroup _lastHoveredGroup; // Track which group we're hovering for indicators

    // Auto-hide strips (one per edge) and drawer overlay
    private readonly Dictionary<AutoHideSide, MGDockAutoHideStrip> _autoHideStrips
        = new Dictionary<AutoHideSide, MGDockAutoHideStrip>();
    private MGDockAutoHideDrawer _autoHideDrawer;

    /// <summary>
    /// The current drop target based on the last mouse position.
    /// Updated by calling GetDropTarget().
    /// </summary>
    public DockDropTarget CurrentDropTarget
    {
        get => _currentDropTarget;
        private set
        {
            if (_currentDropTarget != value)
            {
                _currentDropTarget = value;
                NPC(nameof(CurrentDropTarget));
            }
        }
    }

    /// <summary>
    /// Calculates and returns the drop target at the specified screen position.
    /// Also updates CurrentDropTarget property.
    /// </summary>
    /// <param name="screenPosition">The screen position to test.</param>
    /// <returns>The drop target at the position, or null if none found.</returns>
    public DockDropTarget GetDropTarget(Point screenPosition)
    {
        DockDropTarget bestTarget = null;

        // Get all visible tab groups
        foreach (var tabGroup in GetAllVisibleTabGroups())
        {
            if (tabGroup == null || tabGroup.Visibility != Visibility.Visible)
            {
                continue;
            }

            // Check if we're dragging from this same group (for tab reordering)
            bool isDraggingFromSameGroup = IsDragging && CurrentDrag != null && 
                                           CurrentDrag.SourceGroup == tabGroup.GroupNode;

            // PRIORITY: If dragging from same group and mouse is over tab headers -> REORDER MODE
            if (isDraggingFromSameGroup && 
                tabGroup.TabHeadersBounds.Width > 0 && 
                tabGroup.TabHeadersBounds.Height > 0 &&
                tabGroup.TabHeadersBounds.Contains(screenPosition))
            {
                var reorderTarget = new DockDropTarget
                {
                    TargetNode = tabGroup.GroupNode,
                    Zone = DockZone.Center,
                    HitRect = tabGroup.TabHeadersBounds,
                    PreviewRect = tabGroup.TabHeadersBounds
                };
                
                // Calculate tab index and preview rect
                reorderTarget.TabIndex = DockDropCalculator.CalculateTabIndex(
                    tabGroup, 
                    screenPosition.X, 
                    CurrentDrag.DraggedPanel);
                
                if (reorderTarget.TabIndex >= 0)
                {
                    reorderTarget.PreviewRect = DockDropCalculator.CalculateTabReorderPreviewRect(
                        tabGroup, 
                        reorderTarget.TabIndex,
                        CurrentDrag.DraggedPanel);
                }
                
                bestTarget = reorderTarget;
                break;
            }

            // Normal drop zones (all split zones + center for merging tabs)
            var zones = DockDropCalculator.CalculateDropZones(tabGroup, tabGroup.LayoutBounds, 
                                                               DockDropCalculator.DefaultMarginPercent);

            // Find matching zone at screen position
            var target = DockDropCalculator.GetDropTargetAtPosition(zones, screenPosition);
            if (target != null)
            {
                // If dropping in center zone, calculate tab index for positioning
                if (target.Zone == DockZone.Center && IsDragging && CurrentDrag != null)
                {
                    target.TabIndex = DockDropCalculator.CalculateTabIndex(
                        tabGroup, 
                        screenPosition.X, 
                        CurrentDrag.DraggedPanel);
                }

                bestTarget = target;
                break; // Use first match (could be refined with Z-order)
            }
        }

        CurrentDropTarget = bestTarget;
        return bestTarget;
    }

    /// <summary>
    /// Calculates a drop target for a specific zone on a specific tab group.
    /// Used when the zone is already determined by indicator hover (VS-style).
    /// </summary>
    /// <param name="targetGroup">The tab group to dock to.</param>
    /// <param name="zone">The zone to dock in (determined by indicator hover).</param>
    /// <param name="mousePosition">The current mouse position.</param>
    /// <returns>A DockDropTarget with preview rectangle, or null if invalid.</returns>
    private DockDropTarget GetDropTargetForZone(MGDockTabGroup targetGroup, DockZone zone, Point mousePosition)
    {
        if (targetGroup == null || zone == DockZone.None)
        {
            return null;
        }

        // Respect docking rules: AllowedZones, Family, Document/Tool area
        if (IsDragging && CurrentDrag?.DraggedPanel != null &&
            !CanDockTo(CurrentDrag.DraggedPanel, targetGroup.GroupNode, zone))
        {
            return null;
        }

        bool isDraggingFromSameGroup = IsDragging && CurrentDrag != null && 
                                       CurrentDrag.SourceGroup == targetGroup.GroupNode;

        // Handle Center zone (tab merge or reorder)
        if (zone == DockZone.Center)
        {
            var centerTarget = new DockDropTarget
            {
                TargetNode = targetGroup.GroupNode,
                Zone = DockZone.Center,
                HitRect = targetGroup.LayoutBounds,
                PreviewRect = targetGroup.LayoutBounds
            };

            // Calculate tab index for positioning
            if (IsDragging && CurrentDrag != null)
            {
                centerTarget.TabIndex = DockDropCalculator.CalculateTabIndex(
                    targetGroup, 
                    mousePosition.X, 
                    CurrentDrag.DraggedPanel);

                // If reordering in same group, calculate preview line position
                if (isDraggingFromSameGroup && centerTarget.TabIndex >= 0)
                {
                    centerTarget.PreviewRect = DockDropCalculator.CalculateTabReorderPreviewRect(
                        targetGroup, 
                        centerTarget.TabIndex,
                        CurrentDrag.DraggedPanel);
                }
            }

            return centerTarget;
        }

        // Handle split zones (Left, Right, Top, Bottom)
        // Calculate all drop zones and find the one matching the specified zone
        var zones = DockDropCalculator.CalculateDropZones(
            targetGroup, 
            targetGroup.LayoutBounds, 
            DockDropCalculator.DefaultMarginPercent);

        // Find the target matching the specified zone
        var matchingTarget = zones.FirstOrDefault(z => z.Zone == zone);
        
        return matchingTarget;
    }

    /// <summary>
    /// Recursively finds all visible MGDockTabGroup UI elements in the visual tree.
    /// </summary>
    /// <returns>Enumerable of all visible tab group controls.</returns>
    public IEnumerable<MGDockTabGroup> GetAllVisibleTabGroups()
    {
        if (Content == null)
        {
            yield break;
        }

        // Recursively search the visual tree
        foreach (var tabGroup in FindTabGroupsRecursive(Content))
        {
            yield return tabGroup;
        }
    }

    /// <summary>
    /// Shows the drag preview overlay at the specified bounds.
    /// </summary>
    /// <param name="bounds">The screen-space rectangle to display the preview.</param>
    public void ShowPreview(Microsoft.Xna.Framework.Rectangle bounds)
    {
        _previewOverlay.Show(bounds);
    }

    /// <summary>
    /// Hides the drag preview overlay.
    /// </summary>
    public void HidePreview()
    {
        _previewOverlay.Hide();
    }

    /// <summary>
    /// Helper method to recursively find tab groups in element hierarchy.
    /// </summary>
    private IEnumerable<MGDockTabGroup> FindTabGroupsRecursive(MGElement element)
    {
        if (element == null)
        {
            yield break;
        }

        // If this element is a tab group, return it
        if (element is MGDockTabGroup tabGroup)
        {
            yield return tabGroup;
        }

        // Recursively check children
        foreach (var child in element.GetChildren())
        {
            foreach (var childTabGroup in FindTabGroupsRecursive(child))
            {
                yield return childTabGroup;
            }
        }
    }

    // ─── 14.1 Active group highlight ────────────────────────────────────────

    /// <summary>
    /// Updates the <see cref="MGDockTabGroup.IsActiveGroup"/> flag on every visible tab group
    /// so only the one that contains <see cref="ActiveDockable"/> shows the accent border.
    /// </summary>
    private void RefreshActiveGroupHighlight()
    {
        string activePanelId = _activeDockable?.Id;
        foreach (var tg in GetAllVisibleTabGroups())
        {
            bool contains = activePanelId != null
                && tg.GroupNode?.Panels.Any(p => p.Id == activePanelId) == true;
            tg.IsActiveGroup = contains;
        }
    }

    // ─── 13.2 Splitter-bar discovery helpers ────────────────────────────────

    /// <summary>
    /// Returns all <see cref="MGDockSplitContainer"/> elements currently in the visual tree.
    /// </summary>
    private IEnumerable<MGDockSplitContainer> GetAllSplitContainers()
    {
        if (Content == null)
        {
            yield break;
        }

        foreach (var sc in FindSplitContainersRecursive(Content))
        {
            yield return sc;
        }
    }

    private IEnumerable<MGDockSplitContainer> FindSplitContainersRecursive(MGElement element)
    {
        if (element == null)
        {
            yield break;
        }

        if (element is MGDockSplitContainer splitContainer)
        {
            yield return splitContainer;
        }

        foreach (var child in element.GetChildren())
        {
            foreach (var sc in FindSplitContainersRecursive(child))
            {
                yield return sc;
            }
        }
    }

    /// <summary>
    /// Finds the first <see cref="DockTabGroupNode"/> leaf within a node subtree.
    /// Used when resolving a splitter-bar drop to a concrete target group.
    /// </summary>
    private DockTabGroupNode FindFirstLeafTabGroup(DockNode node)
    {
        if (node is DockTabGroupNode tg)
        {
            return tg;
        }

        if (node is DockSplitNode split)
        {
            var fromFirst = FindFirstLeafTabGroup(split.FirstChild);
            if (fromFirst != null)
            {
                return fromFirst;
            }

            return FindFirstLeafTabGroup(split.SecondChild);
        }
        return null;
    }

    #endregion Drop Zone Calculation
}