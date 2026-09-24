using System.ComponentModel;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.XAML;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Root container for the docking system. Manages the DockLayoutModel and renders the visual tree.
/// </summary>
public class MGDockHost : MGSingleContentHost
{
    public const string PreviewOverlayPartName = "PART_PreviewOverlay";
    public const string DropIndicatorsPartName = "PART_DropIndicators";
    public const string LeftAutoHideStripPartName = "PART_LeftAutoHideStrip";
    public const string RightAutoHideStripPartName = "PART_RightAutoHideStrip";
    public const string TopAutoHideStripPartName = "PART_TopAutoHideStrip";
    public const string BottomAutoHideStripPartName = "PART_BottomAutoHideStrip";
    public const string AutoHideDrawerPartName = "PART_AutoHideDrawer";

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

                // Close the old model's floating windows (no panel close reported) and open the
                // new model's ones (P10); _panelRegistry is deliberately left untouched here, see
                // SyncFloatingWindows and the "LayoutModel then RegisterPanel" pattern (P10).
                SyncFloatingWindows();

                NotifyPropertyChanged(nameof(LayoutModel));
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
    /// Raised BEFORE a panel is removed by a USER close action, so a subscriber can veto the close by
    /// setting <see cref="CancelEventArgs{T}.Cancel"/> (T4.5, D17). A cancelled panel is left exactly as
    /// it was: no removal happens, and nothing else this close action would have done (e.g. closing the
    /// other tabs of a "Close Others"/"Close All") is undone either.
    /// <para>
    /// Raised, one panel at a time, in this order, before the removal each path decides: a tab's close
    /// button, "Close Others" and "Close All" (each closes its remaining panels one by one even if an
    /// earlier one in the same batch was cancelled — <see cref="MGDockTabGroup"/>'s
    /// <c>PanelCloseRequested</c>), a floating window's tab close (<see cref="MGFloatingDockWindow"/>,
    /// both the model-backed branch through <see cref="CloseFloatingPanel"/> and the standalone branch),
    /// the auto-hide drawer's close button (<see cref="CloseAutoHidePanel"/>), and closing an entire
    /// floating window (<see cref="MGWindow.TryCloseWindow"/>, its close button or application code):
    /// raised once per panel the window still holds, in order, and the window close itself is cancelled
    /// at the first refusal — panels already asked earlier in that same pass keep whatever their own
    /// subscriber did (there is no rollback across panels).
    /// </para>
    /// <para>
    /// NOT raised for a programmatic removal: <see cref="RemovePanel"/> or <see cref="CloseFloatingWindow"/>
    /// called directly by application code.
    /// </para>
    /// </summary>
    public event EventHandler<CancelEventArgs<DockPanelNode>> PanelClosing;

    /// <summary>
    /// Raises <see cref="PanelClosing"/> for <paramref name="panel"/> and returns whether a subscriber
    /// cancelled it. No-ops (returns <see langword="false"/>) when <paramref name="panel"/> is null or
    /// nothing is subscribed.
    /// </summary>
    internal bool RaisePanelClosingVetoed(DockPanelNode panel)
    {
        if (panel == null || PanelClosing == null)
        {
            return false;
        }

        CancelEventArgs<DockPanelNode> args = new(panel);
        PanelClosing.Invoke(this, args);
        return args.Cancel;
    }

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
                NotifyPropertyChanged(nameof(CurrentDrag));
                NotifyPropertyChanged(nameof(IsDragging));
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
                NotifyPropertyChanged(nameof(DragThreshold));
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
                NotifyPropertyChanged(nameof(ActiveDockable));
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
            var clamped = Math.Clamp(value, 0.25f, 4f);
            if (Math.Abs(_uiScale - clamped) > 1e-6f)
            {
                _uiScale = clamped;
                NotifyPropertyChanged(nameof(UIScale));
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

            // The preview overlay, the drop indicators, the four auto-hide strips and the drawer come from the control template,
            // see AttachControlTemplateStructure.
            DefaultControlTemplateName = MGControlTemplateCatalog.DockHostTemplateName;

            // Set default styling
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;

            // ── Auto-hide strips (one per edge) ────────────────────────────────
            // Attached from the control template, see AttachControlTemplateStructure.

            // ── Auto-hide drawer overlay ───────────────────────────────────
            // Attached from the control template, see AttachControlTemplateStructure.
        }
    }

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(PreviewOverlayPartName, typeof(MGDockPreviewOverlay));
        yield return new(DropIndicatorsPartName, typeof(MGDockDropIndicators));
        yield return new(LeftAutoHideStripPartName, typeof(MGDockAutoHideStrip));
        yield return new(RightAutoHideStripPartName, typeof(MGDockAutoHideStrip));
        yield return new(TopAutoHideStripPartName, typeof(MGDockAutoHideStrip));
        yield return new(BottomAutoHideStripPartName, typeof(MGDockAutoHideStrip));
        yield return new(AutoHideDrawerPartName, typeof(MGDockAutoHideDrawer));
    }

    /// <summary>Binds the seven surfaces created by the control template (<c>Dock.Host.Default</c>) as components drawn over the docked layout: the
    /// preview overlay and the drop indicators cover the host, each strip runs along the edge of its part name and the drawer opens from the strip of
    /// its panel. Their events, their positions and the whole docking orchestration stay on this host. A surface replaced by another template is
    /// released; the strips and the drawer of a new structure start from the current auto-hide store, drawer closed.</summary>
    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
    {
        _previewOverlay = (MGDockPreviewOverlay)Structure.Parts[PreviewOverlayPartName];
        _dropIndicators = (MGDockDropIndicators)Structure.Parts[DropIndicatorsPartName];
        EnsureComponentBinding(() => _previewOverlayComponent, value => _previewOverlayComponent = value, _previewOverlay,
            element => new(element, ComponentUpdatePriority.AfterContents, ComponentDrawPriority.AfterContents,
                true, true, false, false, false, false, false, (AvailableBounds, ComponentSize) => AvailableBounds));
        EnsureComponentBinding(() => _dropIndicatorsComponent, value => _dropIndicatorsComponent = value, _dropIndicators,
            element => new(element, ComponentUpdatePriority.AfterContents, ComponentDrawPriority.AfterContents,
                true, true, false, false, false, false, false, (AvailableBounds, ComponentSize) => AvailableBounds));

        var stripsChanged = false;
        foreach (AutoHideSide side in System.Enum.GetValues(typeof(AutoHideSide)))
        {
            var strip = (MGDockAutoHideStrip)Structure.Parts[GetAutoHideStripPartName(side)];
            _autoHideStrips.TryGetValue(side, out var previousStrip);
            if (!ReferenceEquals(previousStrip, strip))
            {
                if (previousStrip != null)
                {
                    previousStrip.PanelActivated -= OnAutoHideStripPanelActivated;
                    previousStrip.Detach();
                }

                strip.Side = side;
                strip.Visibility = Visibility.Collapsed;
                //  A strip of a rebuilt structure takes the inset the host reserves for it (backlog task 14); the host's template sets both from the theme.
                strip.StripThickness = _autoHideStripThickness;
                strip.PanelActivated += OnAutoHideStripPanelActivated;
                _autoHideStrips[side] = strip;
                stripsChanged = true;
            }

            _autoHideStripComponents.TryGetValue(side, out var stripComponent);
            var capturedSide = side;
            EnsureComponentBinding(() => stripComponent, value => stripComponent = value, strip,
                element => new(element, ComponentUpdatePriority.AfterContents, ComponentDrawPriority.AfterContents,
                    true, true, false, false, false, false, false, (avail, _) => GetStripBounds(capturedSide, avail)));
            _autoHideStripComponents[side] = stripComponent;
        }

        var drawer = (MGDockAutoHideDrawer)Structure.Parts[AutoHideDrawerPartName];
        if (!ReferenceEquals(_autoHideDrawer, drawer))
        {
            if (_autoHideDrawer != null)
            {
                _autoHideDrawer.PinRequested -= OnAutoHideDrawerPinRequested;
                _autoHideDrawer.PanelCloseRequested -= OnAutoHideDrawerPanelCloseRequested;
                _autoHideDrawer.CloseRequested -= OnAutoHideDrawerCloseRequested;
                _autoHideDrawer.DrawerSizeChanged -= OnAutoHideDrawerSizeChanged;
            }

            drawer.Visibility = Visibility.Collapsed;
            drawer.PinRequested += OnAutoHideDrawerPinRequested;
            drawer.PanelCloseRequested += OnAutoHideDrawerPanelCloseRequested;
            drawer.CloseRequested += OnAutoHideDrawerCloseRequested;
            drawer.DrawerSizeChanged += OnAutoHideDrawerSizeChanged;
            _autoHideDrawer = drawer;
        }

        EnsureComponentBinding(() => _autoHideDrawerComponent, value => _autoHideDrawerComponent = value, _autoHideDrawer,
            element => new(element, ComponentUpdatePriority.AfterContents, ComponentDrawPriority.AfterContents,
                true, true, false, false, false, false, false, (avail, _) => GetDrawerBounds(avail)));

        if (stripsChanged)
        {
            RefreshAutoHideStrips();
        }
    }

    private void OnAutoHideStripPanelActivated(object sender, DockPanelNode panel) => ShowAutoHideDrawer(panel);

    private void OnAutoHideDrawerPinRequested(object sender, DockPanelNode panel) => RepinPanel(panel);

    private void OnAutoHideDrawerPanelCloseRequested(object sender, DockPanelNode panel)
    {
        if (RaisePanelClosingVetoed(panel))
        {
            return;
        }

        CloseAutoHidePanel(panel);
    }

    private void OnAutoHideDrawerCloseRequested(object sender, EventArgs e) => HideAutoHideDrawer();

    private void OnAutoHideDrawerSizeChanged(object sender, int size) => InvalidateLayout();

    private static string GetAutoHideStripPartName(AutoHideSide side)
    {
        return side switch
        {
            AutoHideSide.Left => LeftAutoHideStripPartName,
            AutoHideSide.Right => RightAutoHideStripPartName,
            AutoHideSide.Top => TopAutoHideStripPartName,
            AutoHideSide.Bottom => BottomAutoHideStripPartName,
            _ => throw new ArgumentOutOfRangeException(nameof(side), side, null)
        };
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
        var left   = LayoutModel?.HasAutoHidePanels(AutoHideSide.Left)   == true ? _autoHideStripThickness : 0;
        var right  = LayoutModel?.HasAutoHidePanels(AutoHideSide.Right)  == true ? _autoHideStripThickness : 0;
        var top    = LayoutModel?.HasAutoHidePanels(AutoHideSide.Top)    == true ? _autoHideStripThickness : 0;
        var bottom = LayoutModel?.HasAutoHidePanels(AutoHideSide.Bottom) == true ? _autoHideStripThickness : 0;
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
            var lmbPressed = mouseState.CurrentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
                             && mouseState.PreviousState.LeftButton != Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            if (lmbPressed)
            {
                var mp = mouseState.CurrentPosition;
                var insideDrawer = _autoHideDrawer.LayoutBounds.Contains(mp);
                var insideStrip  = false;
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
            var ctrlHeld       = kb.IsControlDown;
            var shiftHeld      = kb.IsShiftDown;
            var tabJustPressed = kb.CurrentKeyPressedEvents[Microsoft.Xna.Framework.Input.Keys.Tab] != null;

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
            var isStillPressed = ParentWindow.Desktop.InputTracker.Mouse.CurrentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

            if (!isStillPressed)
            {
                // Mouse button released - end drag and perform drop
                PerformDrop();
                return;
            }

            // Get current mouse position
            var currentMousePosition = ParentWindow.Desktop.InputTracker.Mouse.CurrentPosition;

            // Check if drag threshold has been exceeded
            if (!CurrentDrag.HasExceededThreshold)
            {
                var distance = Math.Sqrt(
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
        var distance = Math.Sqrt(
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
                var leftHalf = mousePosition.X < splitterBounds.X + splitterBounds.Width / 2;
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
                var topHalf = mousePosition.Y < splitterBounds.Y + splitterBounds.Height / 2;
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
            var band = (int)(ProximityBandWidth * _uiScale);
            var gb   = hoveredGroup.LayoutBounds;

            var proximityZone = DockZone.None;
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

        var panel        = drag.DraggedPanel;
        var targetNode   = target.TargetNode;
        var fromFloating = drag.SourceFloatingWindow != null;

        void PerformDropOperation()
        {
            // ── If panel comes from a floating window, detach it first ──────────────
            if (fromFloating)
            {
                DetachFromFloatingWindow(drag.SourceFloatingWindow, panel);
            }

            switch (target.Zone)
            {
                case DockZone.Center:
                    // Dock as tab
                    if (targetNode is DockTabGroupNode targetGroup)
                    {
                        // Check if this is a reorder operation (same group, source in host)
                        if (drag.SourceGroup == targetGroup && !fromFloating)
                        {
                            // Reorder within the same group
                            DockOperation.ReorderTab(LayoutModel, panel, targetGroup, target.TabIndex);
                        }
                        else
                        {
                            // Move to different group (or from floating → host)
                            var insertIndex = target.TabIndex >= 0 ? target.TabIndex : -1;
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

            if (fromFloating)
            {
                // An explicit drop replaces whatever place the panel remembered (P3).
                DockOperation.ForgetPlacement(LayoutModel, panel.Id);
            }
        }

        if (fromFloating)
        {
            // One explicit rebuild for this operation (P12): also syncs floating windows so a
            // floating group left empty by the detach above closes its window.
            MutateModelSuspended(PerformDropOperation);
        }
        else
        {
            PerformDropOperation();
            RebuildVisualTree();
        }
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

        // An auto-hidden panel has no parent here but is still tracked by the registry; its
        // strip button and, if it is the one currently displayed, its drawer must be refreshed
        // too, exactly as CloseAutoHidePanel does — otherwise the strip keeps a stale, clickable
        // button for a panel the model no longer holds.
        var wasAutoHidden = !panel.IsPinned;
        if (wasAutoHidden)
        {
            HideAutoHideDrawer();
        }

        MutateModelSuspended(() =>
        {
            // Remove from registry
            _panelRegistry.Remove(panelId);

            // Remove from layout model, remembering its place when the DockableRegistry can
            // recreate it later (P4).
            DockOperation.ClosePanel(LayoutModel, panel, rememberPlacement: ShouldRememberPlace(panelId));

            PanelRemoved?.Invoke(this, panel);
            // Treat removal as close (user explicitly removed the panel)
            _dockableRegistry?.NotifyClosed(panelId);

            if (wasAutoHidden)
            {
                RefreshAutoHideStrips();
            }
        });

        SyncRegistryVisibility();
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

    /// <summary>
    /// Applies <paramref name="model"/>, freshly loaded from a saved layout, to this host (P10).
    /// Used by both <see cref="MGDockHostExtensions.LoadLayoutFromJson"/> and
    /// <see cref="MGDockHostExtensions.TryLoadLayoutFromJson"/>. Unlike a plain assignment to
    /// <see cref="LayoutModel"/> (which deliberately leaves <see cref="_panelRegistry"/> alone, so
    /// the "assign <see cref="LayoutModel"/> then call <see cref="RegisterPanel"/> for every panel"
    /// pattern keeps working), a load replaces the registry outright: the loaded model's docked and
    /// auto-hidden panels become the new registry, each raising exactly one <see cref="PanelAdded"/>,
    /// so <see cref="FindPanel"/> and <see cref="ShowDockable"/> see them immediately.
    /// </summary>
    /// <param name="model">The freshly deserialized model to apply.</param>
    internal void ApplyLoadedLayoutModel(DockLayoutModel model)
    {
        if (model == null)
        {
            throw new ArgumentNullException(nameof(model));
        }

        // Bring every floating window's bounds back inside the desktop before the windows are
        // created by the LayoutModel setter below, so each title bar stays reachable (P9).
        ClampFloatingGroupBoundsToScreen(model);

        // Closes the previous model's floating windows (no panel close reported) and opens this
        // model's ones; does not touch _panelRegistry (P10).
        LayoutModel = model;

        // A load, unlike a plain LayoutModel replacement, must leave the registry reflecting the
        // loaded model: otherwise FindPanel/ShowDockable would not see the loaded panels, and
        // ShowDockable could create a duplicate of one already sitting in the model.
        _panelRegistry.Clear();
        foreach (var panel in model.GetAllPanels().Concat(model.GetAllAutoHidePanels()))
        {
            _panelRegistry[panel.Id] = panel;
            PanelAdded?.Invoke(this, panel);
        }

        RefreshAutoHideStrips();
        HideAutoHideDrawer();
        SyncRegistryVisibility();
    }

    /// <summary>
    /// Clamps every <see cref="DockFloatingGroup"/> in <paramref name="model"/> so its top-left
    /// corner (where the title bar sits) stays inside this host's desktop's
    /// <see cref="MGDesktop.ValidScreenBounds"/> (P9), the same bounds
    /// <see cref="MGFloatingDockWindow.MaximizeWindow"/> reads.
    /// </summary>
    private void ClampFloatingGroupBoundsToScreen(DockLayoutModel model)
    {
        var screen = GetDesktop()?.ValidScreenBounds;
        if (screen == null)
        {
            return;
        }

        const int MinVisible = 40;
        var bounds = screen.Value;
        var maxLeft = Math.Max(bounds.X, bounds.Right - MinVisible);
        var maxTop = Math.Max(bounds.Y, bounds.Bottom - MinVisible);

        foreach (var floatingGroup in model.FloatingGroups)
        {
            floatingGroup.Left = Math.Clamp(floatingGroup.Left, bounds.X, maxLeft);
            floatingGroup.Top = Math.Clamp(floatingGroup.Top, bounds.Y, maxTop);
        }
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
            foreach (var side in new[] { AutoHideSide.Left, AutoHideSide.Top, AutoHideSide.Right, AutoHideSide.Bottom })
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
        var currentIndex = currentId != null ? allPanels.FindIndex(p => p.Id == currentId) : -1;

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
            foreach (var side in new[] { AutoHideSide.Left, AutoHideSide.Top, AutoHideSide.Right, AutoHideSide.Bottom })
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
        var cx = lb.X + lb.Width  / 2;
        var cy = lb.Y + lb.Height / 2;
        ParentWindow.Desktop.TryOpenContextMenu(menu,
            new Microsoft.Xna.Framework.Rectangle(cx, cy, 1, 1));
    }

    /// <summary>
    /// Shows a registered dockable by its ID. Resolves in this order (T4), so a panel is never
    /// duplicated: already docked or auto-hidden (a) → activate it; already floating (b) → activate
    /// its tab and bring its window to the front, without touching its remembered place; already
    /// auto-hidden (c) → already visible as a tab in its strip, nothing to do; otherwise (d) → the
    /// registry's definition creates the panel, which is returned to its remembered place when one
    /// still resolves (D5), or added next to the most recently used tab group, or at the root when
    /// no group exists (P5/P6).
    /// </summary>
    /// <param name="dockableId">The ID of the dockable to show.</param>
    /// <returns>True if the dockable was shown or activated, false if not found in the registry.</returns>
    public bool ShowDockable(string dockableId)
    {
        if (string.IsNullOrWhiteSpace(dockableId))
        {
            return false;
        }

        // (a) Already docked or auto-hidden in the host's own registry → activate its tab, if it
        // has one (an auto-hidden panel has no parent group here; its strip already shows it).
        if (_panelRegistry.TryGetValue(dockableId, out var existingPanel))
        {
            var parentGroup = existingPanel.Parent as DockTabGroupNode;
            if (parentGroup != null)
            {
                parentGroup.SetActivePanel(dockableId);
            }
            return true;
        }

        // (b) Already floating → make it the active tab of its floating group and bring the
        // window to the front. Its remembered place is left untouched and no node is created.
        var floatingGroup = LayoutModel?.FindFloatingGroupOf(dockableId);
        if (floatingGroup != null)
        {
            floatingGroup.Group.SetActivePanel(dockableId);
            _floatingWindows.FirstOrDefault(w => w.FloatingGroup == floatingGroup)?.BringToFront();
            return true;
        }

        // (c) Already auto-hidden → already visible as a tab in its strip.
        if (LayoutModel != null && LayoutModel.GetAllAutoHidePanels().Any(p => p.Id == dockableId))
        {
            return true;
        }

        // (d) Not visible anywhere → need the registry's definition to recreate it.
        if (_dockableRegistry == null || !_dockableRegistry.TryGetById(dockableId, out var definition))
        {
            return false;
        }

        var panel = definition.CreatePanelNode();

        MutateModelSuspended(() =>
        {
            if (DockOperation.RestoreToPlacement(LayoutModel, panel))
            {
                return;
            }

            // No remembered place (or it no longer resolves): today's fallback, a suitable,
            // non-hidden tab group (P6: a hidden placeholder group is skipped, exactly like a
            // missing one), else start fresh at the root, else split the root.
            var allGroups = GetAllTabGroups().Where(g => !g.IsHiddenInLayout).ToList();
            var targetGroup = allGroups.FirstOrDefault();

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

            // The remembered place — now pointing nowhere useful — is forgotten (P5), mirroring
            // RedockPanel's and RepinPanel's fallback: otherwise a stale Placements entry survives
            // pointing at a group the panel is no longer waiting to return to, while the panel is
            // simultaneously anchored elsewhere in the tree.
            DockOperation.ForgetPlacement(LayoutModel, panel.Id);
        });

        RegisterPanel(panel);
        return true;
    }

    /// <summary>
    /// Whether closing the panel with <paramref name="panelId"/> should remember its place (P4):
    /// only when the <see cref="DockableRegistry"/> knows the id can <see cref="ShowDockable"/>
    /// recreate the panel later; otherwise nothing could ever reopen it and its placeholder group
    /// would never be released.
    /// </summary>
    /// <param name="panelId">Id of the panel about to be closed.</param>
    private bool ShouldRememberPlace(string panelId)
        => _dockableRegistry != null && _dockableRegistry.TryGetById(panelId, out _);

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

        const int defaultFloatWidth  = 320;
        const int defaultFloatHeight = 260;
        var left = dropPosition.X - defaultFloatWidth  / 2;
        var top  = dropPosition.Y - defaultFloatHeight / 2;

        DockFloatingGroup floatingGroup = null;
        MutateModelSuspended(() =>
        {
            // Remove from the host panel registry first (before model cleanup)
            _panelRegistry.Remove(panel.Id);

            // Moves the panel into a new floating group in the model, remembering its place (P2)
            // so the source group survives, hidden, until the panel returns (D2/D3). The commit
            // point (CommitModelChange, via MutateModelSuspended) reflects the new floating group
            // as an actual window (SyncFloatingWindows).
            floatingGroup = DockOperation.FloatPanel(LayoutModel, panel, left, top, defaultFloatWidth, defaultFloatHeight);
        });

        // Re-sync: the floating panel should still appear "visible" to the DockableRegistry
        SyncRegistryVisibility();

        return _floatingWindows.FirstOrDefault(w => w.FloatingGroup == floatingGroup);
    }

    /// <summary>
    /// Tracks <paramref name="floatWin"/> and attaches it as a nested window of the host's parent window. The host owns the lifecycle of the
    /// floating window whichever way it closes: through <see cref="CloseFloatingWindow"/>, or through <see cref="MGWindow.TryCloseWindow"/> (the
    /// close button of the window template, or application code), which <see cref="OnFloatingWindowClosed"/> observes. The host also subscribes
    /// to the window's cancelable <see cref="MGWindow.WindowClosing"/> (T4.5, D17), so a whole-window close can be vetoed panel by panel through
    /// <see cref="PanelClosing"/> before <see cref="MGWindow.TryCloseWindow"/> removes anything.
    /// </summary>
    private void AttachFloatingWindow(MGFloatingDockWindow floatWin)
    {
        _floatingWindows.Add(floatWin);
        floatWin.WindowClosing += OnFloatingWindowClosing;
        floatWin.WindowClosed += OnFloatingWindowClosed;
        ParentWindow.AddNestedWindow(floatWin);
    }

    /// <summary>
    /// Raises <see cref="PanelClosing"/> for every panel <paramref name="sender"/> (a floating window being closed as a whole through
    /// <see cref="MGWindow.TryCloseWindow"/>) still holds, in order, and cancels the window close at the first refusal. Panels already asked
    /// earlier in this same pass keep whatever their subscriber did (there is no rollback across panels) — only the window close itself, and
    /// any panel not yet asked, is cancelled.
    /// </summary>
    private void OnFloatingWindowClosing(object sender, CancelEventArgs e)
    {
        if (sender is not MGFloatingDockWindow floatWin)
        {
            return;
        }

        foreach (var panel in floatWin.GroupNode.Panels.ToList())
        {
            if (RaisePanelClosingVetoed(panel))
            {
                e.Cancel = true;
                return;
            }
        }
    }

    /// <summary>
    /// A floating window closed itself (<see cref="MGWindow.TryCloseWindow"/> already removed it from the parent window's nested windows): the
    /// host stops tracking it and closes every panel it still held, as closing those panels one by one would — a model-backed window inside one
    /// suspended mutation (one <see cref="PanelRemoved"/> per panel, place remembered when the <see cref="DockableRegistry"/> can recreate the
    /// panel, P4), a standalone window the same way it always has.
    /// </summary>
    private void OnFloatingWindowClosed(object sender, EventArgs e)
    {
        if (sender is not MGFloatingDockWindow floatWin || !_floatingWindows.Remove(floatWin))
        {
            return;
        }

        floatWin.WindowClosing -= OnFloatingWindowClosing;
        floatWin.WindowClosed -= OnFloatingWindowClosed;

        if (floatWin.FloatingGroup != null)
        {
            var panels = floatWin.GroupNode.Panels.ToList();
            MutateModelSuspended(() =>
            {
                foreach (var panel in panels)
                {
                    DockOperation.ClosePanel(LayoutModel, panel, rememberPlacement: ShouldRememberPlace(panel.Id));
                    PanelRemoved?.Invoke(this, panel);
                    _dockableRegistry?.NotifyClosed(panel.Id);
                }
            });
        }
        else
        {
            foreach (var panel in floatWin.GroupNode.Panels.ToList())
            {
                NotifyFloatingPanelClosed(panel);
            }
        }

        // The host stopped tracking floatWin above (_floatingWindows.Remove): release whatever its tab
        // group still holds (ADR-0015 decision 3, P5). For a model-backed window (the "if" branch) the
        // content was already released through the model mutation above; here this only drops the
        // visual's model subscriptions. For a standalone window (the "else" branch) this is the only release.
        floatWin.TabGroup.Detach();

        SyncRegistryVisibility();
    }

    /// <summary>
    /// Creates a model-backed floating window for <paramref name="panel"/> at the given position.
    /// When the panel currently sits in a tab group of the docked layout, its place is remembered
    /// (D2/D3) exactly as <see cref="DetachToFloating"/> does; a panel with no parent (already
    /// outside the layout) records no placement. Like <see cref="DetachToFloating"/>, an
    /// already-registered (docked) panel is removed from the host's panel registry first, so it is
    /// not left dangling there once it moves into the model's floating store.
    /// </summary>
    public MGFloatingDockWindow CreateFloatingWindow(DockPanelNode panel, int left, int top, int width = 320, int height = 260)
    {
        if (panel == null)
        {
            throw new ArgumentNullException(nameof(panel));
        }

        DockFloatingGroup floatingGroup = null;
        MutateModelSuspended(() =>
        {
            _panelRegistry.Remove(panel.Id);
            floatingGroup = DockOperation.FloatPanel(LayoutModel, panel, left, top, width, height);
        });

        SyncRegistryVisibility();
        return _floatingWindows.FirstOrDefault(w => w.FloatingGroup == floatingGroup);
    }

    /// <summary>
    /// Closes a floating window. For a window whose <see cref="MGFloatingDockWindow.FloatingGroup"/>
    /// is still live in <see cref="LayoutModel"/>'s floating store — this API called directly rather
    /// than through <see cref="MGWindow.TryCloseWindow"/> (<see cref="OnFloatingWindowClosed"/>) —
    /// every panel it still holds is closed in the model first (place remembered when the
    /// <see cref="DockableRegistry"/> can recreate it, P4, as <see cref="OnFloatingWindowClosed"/>
    /// does), and reported through <see cref="PanelRemoved"/>;
    /// otherwise <see cref="LayoutModel"/>.<see cref="DockLayoutModel.FloatingGroups"/> would keep the
    /// orphaned <see cref="DockFloatingGroup"/> and <see cref="SyncFloatingWindows"/> would resurrect
    /// it as a new window on the next model commit (P1). Removing those panels re-enters this method
    /// through <see cref="CommitModelChange"/> / <see cref="SyncFloatingWindows"/> once the group has
    /// left the model, and that re-entrant call takes the plain bookkeeping path below (removing the
    /// window from the floating list and from the parent window's nested windows) — so a window whose
    /// group is already gone, or that was never model-backed, is only ever bookkept, never double-closed.
    /// </summary>
    public void CloseFloatingWindow(MGFloatingDockWindow window)
    {
        if (window == null)
        {
            return;
        }

        if (window.FloatingGroup != null && (LayoutModel?.FloatingGroups.Contains(window.FloatingGroup) ?? false))
        {
            var panels = window.GroupNode.Panels.ToList();
            MutateModelSuspended(() =>
            {
                foreach (var panel in panels)
                {
                    DockOperation.ClosePanel(LayoutModel, panel, rememberPlacement: ShouldRememberPlace(panel.Id));
                    PanelRemoved?.Invoke(this, panel);
                    _dockableRegistry?.NotifyClosed(panel.Id);
                }
            });

            // Idempotent with the Detach() below the re-entrant bookkeeping branch (SyncFloatingWindows
            // closes this same window once its group has left the model, ADR-0015 decision 3, P5):
            // GroupNode is already null there, so Detach() no-ops when reached a second time.
            window.TabGroup.Detach();

            SyncRegistryVisibility();
            return;
        }

        window.WindowClosing -= OnFloatingWindowClosing;
        window.WindowClosed -= OnFloatingWindowClosed;
        _floatingWindows.Remove(window);
        ParentWindow.RemoveNestedWindow(window);
        // The host stops tracking this (non model-backed) window here: its tab group is the only
        // holder of its content, so it must release it now (ADR-0015 decision 3, P5), or the closed
        // window would keep resolving names of elements it no longer displays.
        window.TabGroup.Detach();
        SyncRegistryVisibility();
    }

    /// <summary>
    /// Called by <see cref="MGFloatingDockWindow"/> when the user closes a panel inside a standalone
    /// (non model-backed) floating window via the close button. Notifies the dockable registry that
    /// the panel was closed. A model-backed window's panel close goes through <see cref="CloseFloatingPanel"/> instead.
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

    /// <summary>
    /// Closes <paramref name="panel"/> from a model-backed floating window's tab close button or
    /// context menu (<see cref="MGFloatingDockWindow.OnPanelCloseRequested"/>). Goes through
    /// <see cref="DockOperation.ClosePanel"/>, remembering the place only when the
    /// <see cref="DockableRegistry"/> can recreate the panel later (P4), so <c>ShowDockable</c> can
    /// reopen it there. The floating window itself is synced away by <see cref="SyncFloatingWindows"/>
    /// (part of the commit point) once its <see cref="DockFloatingGroup"/> is removed from the model.
    /// </summary>
    /// <param name="panel">The panel to close.</param>
    internal void CloseFloatingPanel(DockPanelNode panel)
    {
        if (panel == null)
        {
            return;
        }

        MutateModelSuspended(() =>
        {
            DockOperation.ClosePanel(LayoutModel, panel, rememberPlacement: ShouldRememberPlace(panel.Id));
            PanelRemoved?.Invoke(this, panel);
            _dockableRegistry?.NotifyClosed(panel.Id);
        });

        SyncRegistryVisibility();
    }

    /// <summary>
    /// Detaches <paramref name="panel"/> from the floating window <paramref name="floatingSource"/> holding
    /// it — the model's floating store for a model-backed window (<see cref="MGFloatingDockWindow.FloatingGroup"/>
    /// non-null), or the window's own standalone group otherwise — and re-registers it in this host's panel
    /// registry. A standalone window that becomes empty as a result is closed immediately; a model-backed window
    /// is closed later by <see cref="SyncFloatingWindows"/>, once its <see cref="DockFloatingGroup"/> is removed
    /// from the model (which <see cref="DockOperation.DetachFromFloatingGroup"/> does when it empties).
    /// Shared by <see cref="ExecuteDrop"/> (dragging a floated tab back into the host) and
    /// <see cref="RedockPanel"/> (the tab context menu's "Dock" command).
    /// </summary>
    private void DetachFromFloatingWindow(MGFloatingDockWindow floatingSource, DockPanelNode panel)
    {
        if (floatingSource.FloatingGroup != null)
        {
            DockOperation.DetachFromFloatingGroup(LayoutModel, panel);
        }
        else
        {
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
        }

        // Register the panel back in the host (it was never in _panelRegistry
        // while floating, so there is no duplicate-key issue).
        if (!_panelRegistry.ContainsKey(panel.Id))
        {
            _panelRegistry[panel.Id] = panel;
            PanelAdded?.Invoke(this, panel);
        }
    }

    /// <summary>
    /// Re-docks <paramref name="panel"/> from the floating window <paramref name="source"/> back into
    /// the docked layout, in response to the tab context menu's "Dock" command (see
    /// <see cref="MGDockTabItem.DockRequested"/>). The panel returns to the exact tab group and index
    /// its remembered placement (P2, D3) points to when that group still exists in <see cref="LayoutModel"/>
    /// (<see cref="DockOperation.RestoreToPlacement"/>); otherwise it is docked into the first visible
    /// tab group, and the placement — now pointing nowhere useful — is forgotten (P5). Leaves the panel
    /// floating (no-op) if the host has no visible tab group at all to dock into.
    /// </summary>
    /// <param name="panel">The panel to re-dock.</param>
    /// <param name="source">The floating window currently hosting the panel.</param>
    public void RedockPanel(DockPanelNode panel, MGFloatingDockWindow source)
    {
        if (panel == null || source == null || LayoutModel == null)
        {
            return;
        }

        // Resolve the destination BEFORE touching the floating window: if there is nowhere to
        // dock the panel, leave it floating untouched rather than detaching it into limbo.
        var placementGroup = DockOperation.ResolvePlacementGroup(LayoutModel, panel.Id);
        var fallback = placementGroup == null ? GetAllVisibleTabGroups().FirstOrDefault()?.GroupNode : null;

        if (placementGroup == null && fallback == null)
        {
            return;
        }

        MutateModelSuspended(() =>
        {
            DetachFromFloatingWindow(source, panel);

            if (placementGroup != null)
            {
                DockOperation.RestoreToPlacement(LayoutModel, panel);
            }
            else
            {
                DockOperation.DockAsTab(LayoutModel, panel, fallback, -1);
                DockOperation.ForgetPlacement(LayoutModel, panel.Id);
            }
        });

        SyncRegistryVisibility();
    }

    #endregion Floating Windows — management

    #region Auto-Hide

    private int _autoHideStripThickness = MGDockAutoHideStrip.DefaultStripThickness;
    /// <summary>The inset reserved on each edge that has auto-hidden panels, and the thickness pushed onto the four auto-hide strips. Default: the theme's
    /// <see cref="MGThemeDockingSettings.AutoHideStripThickness"/>, applied by the <c>Dock.Host.Default</c> template (backlog task 14).</summary>
    public int AutoHideStripThickness
    {
        get => _autoHideStripThickness;
        set
        {
            if (_autoHideStripThickness != value)
            {
                var previous = _autoHideStripThickness;
                _autoHideStripThickness = value;
                //  The strips follow the host's inset; a strip whose thickness the application set on its own no longer follows it.
                foreach (var strip in _autoHideStrips.Values)
                {
                    if (strip.StripThickness == previous)
                    {
                        strip.StripThickness = value;
                    }
                }

                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(AutoHideStripThickness));
            }
        }
    }

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
        var topInset    = LayoutModel.HasAutoHidePanels(AutoHideSide.Top)    ? _autoHideStripThickness : 0;
        var bottomInset = LayoutModel.HasAutoHidePanels(AutoHideSide.Bottom) ? _autoHideStripThickness : 0;

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

        var ds = panel.DrawerSize;
        var st = _autoHideStripThickness;
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
    /// strip on the appropriate edge (inferred from its current position). Its place (source
    /// group and tab index) is remembered (D2/D3/D4) exactly as <see cref="DetachToFloating"/>
    /// remembers a floated panel's place, so the source group survives, hidden, as a placeholder
    /// until <see cref="RepinPanel"/> brings the panel back to it.
    /// </summary>
    public void UnpinPanel(DockPanelNode panel)
    {
        // Idempotent no-op when the panel cannot auto-hide or is already auto-hidden (not
        // pinned): DockOperation.AutoHidePanel requires the panel to currently sit in a tab
        // group in the layout tree and throws otherwise, so a repeated call must stop here.
        if (panel == null || !panel.CanAutoHide || !panel.IsPinned)
        {
            return;
        }

        // Read the current visuals BEFORE mutating the model: AutoHidePanel removes the panel
        // from the layout tree, which would leave nothing for InferAutoHideSide to measure.
        var side = InferAutoHideSide(panel);

        MutateModelSuspended(() =>
        {
            DockOperation.AutoHidePanel(LayoutModel, panel, side);
            RefreshAutoHideStrips();
        });
    }

    /// <summary>
    /// Moves <paramref name="panel"/> from the auto-hide store back into the docked layout, at
    /// its remembered place (D3/D4): the same tab group, at its original tab index, whatever the
    /// order in which several auto-hidden panels return. When that place is gone (the application
    /// replaced the root while the panel was hidden), it falls back to splitting the root on the
    /// panel's <see cref="DockPanelNode.AutoHideSide"/> (P5), or — when the layout is entirely
    /// empty — becomes the new root itself.
    /// </summary>
    public void RepinPanel(DockPanelNode panel)
    {
        // Idempotent no-op when the panel is not currently auto-hidden (already pinned):
        // DockOperation.RestoreToPlacement requires the panel to have no parent and throws
        // otherwise, so a repeated call must stop here.
        if (panel == null || panel.IsPinned)
        {
            return;
        }

        HideAutoHideDrawer();

        MutateModelSuspended(() =>
        {
            LayoutModel.RemoveFromAutoHide(panel);

            if (!DockOperation.RestoreToPlacement(LayoutModel, panel))
            {
                var fallbackZone = panel.AutoHideSide switch
                {
                    AutoHideSide.Left   => DockZone.Left,
                    AutoHideSide.Right  => DockZone.Right,
                    AutoHideSide.Top    => DockZone.Top,
                    AutoHideSide.Bottom => DockZone.Bottom,
                    _                   => DockZone.Right
                };

                if (LayoutModel.RootNode == null)
                {
                    var newGroup = new DockTabGroupNode();
                    newGroup.AddPanel(panel, -1);
                    LayoutModel.RootNode = newGroup;
                }
                else
                {
                    DockOperation.SplitDockAtRoot(LayoutModel, panel, fallbackZone, DockDropCalculator.HostEdgePreviewRatio);
                }

                // The place is gone (the application replaced the root); forgetting it also
                // collects any placeholder it was the last reference to.
                DockOperation.ForgetPlacement(LayoutModel, panel.Id);
            }

            RefreshAutoHideStrips();
        });
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

                var cx = (gb.X + gb.Width  * 0.5f - hb.X) / hb.Width;
                var cy = (gb.Y + gb.Height * 0.5f - hb.Y) / hb.Height;

                var dL = cx;
                var dR = 1f - cx;
                var dT = cy;
                var dB = 1f - cy;
                var min = Math.Min(Math.Min(dL, dR), Math.Min(dT, dB));
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

        foreach (var side in _autoHideStrips.Keys)
        {
            var panels = LayoutModel.GetAutoHidePanels(side);
            _autoHideStrips[side].Refresh(panels);
            _autoHideStrips[side].Visibility = panels.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        InvalidateLayout();
    }

    /// <summary>
    /// Permanently closes an auto-hidden panel: removes it from the auto-hide store, remembering
    /// its place (so <c>ShowDockable</c> can reopen it there, D5) only when the
    /// <see cref="DockableRegistry"/> can recreate it (P4) — otherwise forgets it, so an
    /// unreferenced placeholder can collapse exactly as a plain close would — the panel registry,
    /// and notifies the dockable registry. Internal (rather than private) so tests can drive the
    /// drawer's close path without the drawer's own button wiring, which is out of scope here.
    /// </summary>
    internal void CloseAutoHidePanel(DockPanelNode panel)
    {
        if (panel == null)
        {
            return;
        }

        HideAutoHideDrawer();

        MutateModelSuspended(() =>
        {
            // Remove from the panel registry before the model mutation and the DockLayoutChanged
            // it triggers (via CommitModelChange), otherwise the registry is stale for any caller
            // that inspects it synchronously from a DockLayoutChanged handler — mirroring
            // PanelCloseRequested and DetachToFloating, which remove the registry entry before
            // mutating the model.
            _panelRegistry.Remove(panel.Id);

            DockOperation.ClosePanel(LayoutModel, panel, rememberPlacement: ShouldRememberPlace(panel.Id));

            PanelRemoved?.Invoke(this, panel);
            _dockableRegistry?.NotifyClosed(panel.Id);

            RefreshAutoHideStrips();
        });

        SyncRegistryVisibility();
    }

    #endregion Auto-Hide

    /// <returns>Collection of all DockTabGroupNode instances in the visual tree.</returns>
    public IEnumerable<DockTabGroupNode> GetAllTabGroups()
    {
        return LayoutModel?.GetAllTabGroups() ?? Enumerable.Empty<DockTabGroupNode>();
    }

    /// <summary>
    /// Returns the tab group designated as the Document Area, or null if none is set or it is
    /// currently a hidden placeholder (P6: a hidden document area counts as absent).
    /// </summary>
    public DockTabGroupNode GetDocumentArea()
    {
        return GetAllTabGroups().FirstOrDefault(g => g.IsDocumentArea && !g.IsHiddenInLayout);
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
            var family = panel.Family;
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
            var maximizedId = CurrentMaximizedGroupId;
            if (maximizedId != null)
            {
                // Find the tab group node with the matching ID. A hidden placeholder group (P6)
                // is treated exactly like a missing one: it cannot be the maximized group.
                var maximizedGroup = LayoutModel.GetAllTabGroups()
                    .FirstOrDefault(g => g.Id == maximizedId && !g.IsHiddenInLayout);

                if (maximizedGroup != null)
                {
                    var fullscreenVisual = BuildTabGroup(maximizedGroup, isMaximized: true);
                    SetContent(fullscreenVisual);
                    SyncRegistryVisibility();
                    return;
                }

                // Maximized group no longer exists (or is now hidden) — pop and fall through to normal rebuild
                _maximizeStack.Pop();
            }

            // ── Normal mode ──────────────────────────────────────────────
            // An entirely collapsed tree (every group hidden, P1) shows the empty placeholder;
            // host-edge drop zones stay available regardless (they don't depend on any group).
            if (LayoutModel.RootNode.IsHiddenInLayout)
            {
                SetContent(CreateEmptyPlaceholder());
                SyncRegistryVisibility();
                return;
            }

            var visualRoot = BuildVisualTree(LayoutModel.RootNode);
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
                return BuildSplitNodeVisual(splitNode);

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
    /// Builds the visual for a split node, skipping a hidden child (P1). When exactly one child is
    /// hidden (or null), only the visible child's visual is built, without creating an
    /// <see cref="MGDockSplitContainer"/> or a separator. A split with both children hidden is
    /// itself <see cref="DockNode.IsHiddenInLayout"/>, so its parent never recurses into it here
    /// (see <see cref="RebuildVisualTree"/> for the case where the whole tree collapses to that).
    /// </summary>
    private MGElement BuildSplitNodeVisual(DockSplitNode splitNode)
    {
        var firstHidden = splitNode.FirstChild == null || splitNode.FirstChild.IsHiddenInLayout;
        var secondHidden = splitNode.SecondChild == null || splitNode.SecondChild.IsHiddenInLayout;

        if (firstHidden && !secondHidden)
        {
            return BuildVisualTree(splitNode.SecondChild);
        }

        if (secondHidden && !firstHidden)
        {
            return BuildVisualTree(splitNode.FirstChild);
        }

        return BuildSplitContainer(splitNode);
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

        // Subscribe to panel close requests (also covers "Close Others" and "Close All", which
        // MGDockTabGroup raises through this same event, one panel at a time).
        tabGroup.PanelCloseRequested += (sender, panelToClose) =>
        {
            if (panelToClose != null && LayoutModel != null)
            {
                if (RaisePanelClosingVetoed(panelToClose))
                {
                    return;
                }

                MutateModelSuspended(() =>
                {
                    // Remove from panel registry BEFORE removing from model,
                    // otherwise the registry becomes stale for any caller that
                    // inspects it synchronously in a LayoutChanged handler.
                    _panelRegistry.Remove(panelToClose.Id);

                    // Remove panel from layout model, remembering its place when the
                    // DockableRegistry can recreate it later (P4).
                    DockOperation.ClosePanel(LayoutModel, panelToClose, rememberPlacement: ShouldRememberPlace(panelToClose.Id));

                    // Notify event subscribers
                    PanelRemoved?.Invoke(this, panelToClose);
                    _dockableRegistry?.NotifyClosed(panelToClose.Id);
                });

                SyncRegistryVisibility();
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
        };
        textBlock.SetPadding(new Thickness(20).ToThickness(), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

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
        };
        textBlock.SetPadding(new Thickness(20).ToThickness(), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

        return textBlock;
    }

    /// <summary>
    /// Handles layout model changes by re-syncing node subscriptions, floating windows and the
    /// visual tree (see <see cref="CommitModelChange"/>).
    /// </summary>
    private void OnLayoutModelChanged(object sender, EventArgs e) => CommitModelChange();

    /// <summary>
    /// The single point (P12) through which every host operation that touches the layout model
    /// (directly or via <see cref="DockOperation"/>) settles its side effects: node subscriptions,
    /// floating windows and the visual tree. Called once per operation, either directly (see
    /// <see cref="MutateModelSuspended"/>) or through <see cref="DockLayoutModel.LayoutChanged"/>
    /// (<see cref="OnLayoutModelChanged"/>) for a mutation that was not run through
    /// <see cref="MutateModelSuspended"/>.
    /// </summary>
    private void CommitModelChange()
    {
        SyncNodeSubscriptions();
        SyncFloatingWindows();
        RebuildVisualTree();
        DockLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Runs <paramref name="mutate"/> against <see cref="LayoutModel"/> with this host's
    /// <see cref="DockLayoutModel.LayoutChanged"/> subscription suspended, then performs exactly one
    /// <see cref="CommitModelChange"/> (P12) — mirroring <see cref="UnpinPanel"/> / <see cref="RepinPanel"/>.
    /// Needed because adding or removing a panel does not reliably raise <see cref="DockLayoutModel.LayoutChanged"/>
    /// (only nodes already subscribed at root-assignment time do, and <c>ActivePanelId</c> is filtered out), and a
    /// hidden placeholder group has no visual to refresh itself when it empties or refills.
    /// </summary>
    /// <param name="mutate">The model mutation to run.</param>
    private void MutateModelSuspended(Action mutate)
    {
        _layoutModel.LayoutChanged -= OnLayoutModelChanged;
        try
        {
            mutate();
        }
        finally
        {
            _layoutModel.LayoutChanged += OnLayoutModelChanged;
        }

        CommitModelChange();
    }

    /// <summary>
    /// Creates a floating window for each <see cref="DockLayoutModel.FloatingGroups"/> entry of
    /// <see cref="LayoutModel"/> that does not have one yet (via the internal, model-backed
    /// <see cref="MGFloatingDockWindow"/> constructor), and closes (<see cref="CloseFloatingWindow"/>,
    /// no panel close reported) every tracked window whose <see cref="MGFloatingDockWindow.FloatingGroup"/>
    /// is non-null and is no longer in the model's floating store. A window with a null
    /// <see cref="MGFloatingDockWindow.FloatingGroup"/> (created through the public constructor,
    /// outside any model) is left alone.
    /// </summary>
    private void SyncFloatingWindows()
    {
        var currentGroups = LayoutModel?.FloatingGroups ?? (IReadOnlyList<DockFloatingGroup>)Array.Empty<DockFloatingGroup>();

        foreach (var window in _floatingWindows.ToList())
        {
            if (window.FloatingGroup != null && !currentGroups.Contains(window.FloatingGroup))
            {
                CloseFloatingWindow(window);
            }
        }

        foreach (var floatingGroup in currentGroups)
        {
            if (_floatingWindows.Any(w => w.FloatingGroup == floatingGroup))
            {
                continue;
            }

            AttachFloatingWindow(new MGFloatingDockWindow(this, floatingGroup));
        }
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
    private MGComponent<MGDockPreviewOverlay> _previewOverlayComponent;
    private MGDockDropIndicators _dropIndicators;
    private MGComponent<MGDockDropIndicators> _dropIndicatorsComponent;
    private MGDockTabGroup _lastHoveredGroup; // Track which group we're hovering for indicators

    // Auto-hide strips (one per edge) and drawer overlay, attached from the control template (see AttachControlTemplateStructure)
    private readonly Dictionary<AutoHideSide, MGDockAutoHideStrip> _autoHideStrips
        = new Dictionary<AutoHideSide, MGDockAutoHideStrip>();
    private readonly Dictionary<AutoHideSide, MGComponent<MGDockAutoHideStrip>> _autoHideStripComponents
        = new Dictionary<AutoHideSide, MGComponent<MGDockAutoHideStrip>>();
    private MGDockAutoHideDrawer _autoHideDrawer;
    private MGComponent<MGDockAutoHideDrawer> _autoHideDrawerComponent;

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
                NotifyPropertyChanged(nameof(CurrentDropTarget));
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
            var isDraggingFromSameGroup = IsDragging && CurrentDrag != null && 
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

        var isDraggingFromSameGroup = IsDragging && CurrentDrag != null && 
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
        var activePanelId = _activeDockable?.Id;
        foreach (var tg in GetAllVisibleTabGroups())
        {
            var contains = activePanelId != null
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
        // A hidden placeholder group (P6) is never resolved to — treated as if absent.
        if (node is DockTabGroupNode tg)
        {
            return tg.IsHiddenInLayout ? null : tg;
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