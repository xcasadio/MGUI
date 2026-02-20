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
                    if (_layoutModel.RootNode != null)
                    {
                        UnsubscribeFromNode(_layoutModel.RootNode);
                    }
                }

                _layoutModel = value;

                // Subscribe to new model
                if (_layoutModel != null)
                {
                    _layoutModel.LayoutChanged += OnLayoutModelChanged;
                    if (_layoutModel.RootNode != null)
                    {
                        SubscribeToNode(_layoutModel.RootNode);
                    }
                }

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
                    SyncRegistryVisibility();
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
            return;

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
            return;

        _maximizeStack.Pop();
        RebuildVisualTree();
    }

    /// <summary>Whether the layout is currently in a maximized state.</summary>
    public bool IsMaximized => _maximizeStack.Count > 0;

    #endregion Maximize / Restore

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
        }
    }

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);

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
        // They are pinned to the host's four edges regardless of where the mouse is.
        if (LayoutBounds.Width > 0 && LayoutBounds.Height > 0)
        {
            _dropIndicators.ShowHostEdge(LayoutBounds);
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
            // If no valid drop target, the tab remains in its source group (no action needed)
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
    public void BeginDrag(DockPanelNode panel, DockTabGroupNode sourceGroup, Point startPos, MGDockTabItem sourceItem)
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
            HasExceededThreshold = false
        };

        // Don't provide visual feedback yet - wait for threshold
        // sourceItem.Opacity will be set when threshold is exceeded

        // Initialize preview calculation position
        _lastPreviewCalculation = startPos;
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

        var panel = drag.DraggedPanel;
        var targetNode = target.TargetNode;

        switch (target.Zone)
        {
            case DockZone.Center:
                // Dock as tab
                if (targetNode is DockTabGroupNode targetGroup)
                {
                    // Check if this is a reorder operation (same group)
                    if (drag.SourceGroup == targetGroup)
                    {
                        // Reorder within the same group
                        DockOperation.ReorderTab(LayoutModel, panel, targetGroup, target.TabIndex);
                    }
                    else
                    {
                        // Move to different group
                        // Use the calculated TabIndex if available, otherwise append at end (-1)
                        int insertIndex = target.TabIndex >= 0 ? target.TabIndex : -1;
                        DockOperation.MoveTab(LayoutModel, panel, targetGroup, insertIndex);
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
            return false;

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
            return false;

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

    /// <summary>
    /// Gets all tab groups currently in the layout.
    /// </summary>
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
                g.IsDocumentArea = false;
        }
        if (group != null)
            group.IsDocumentArea = true;
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
            return true; // Split docks are always allowed

        var documentArea = GetDocumentArea();
        if (documentArea == null)
            return true; // No document area defined → no type restriction

        if (panelType == DockableType.Document)
            return targetGroup == documentArea; // Documents only go to DocumentArea

        if (panelType == DockableType.Tool)
            return targetGroup != documentArea; // Tools cannot go to DocumentArea

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
            return true;

        // 1. Document / Tool area rules (existing)
        if (!CanDockIntoGroup(panel.DockableType, targetGroup, zone))
            return false;

        // 2. AllowedZones restriction
        var allowedZones = panel.AllowedZones;
        if (allowedZones != null && !allowedZones.Contains(zone))
            return false;

        // 3. Family restriction — only for tab-docking (Center)
        if (zone == DockZone.Center)
        {
            string family = panel.Family;
            if (family != null)
            {
                foreach (var p in targetGroup.Panels)
                {
                    if (p.Id == panel.Id)
                        continue; // skip self
                    if (p.Family != null && p.Family != family)
                        return false;
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
            return forbidden;

        foreach (var z in _allDropZones)
        {
            if (!CanDockTo(panel, targetGroup, z))
                forbidden.Add(z);
        }

        return forbidden;
    }

    /// <summary>
    /// Rebuilds the entire visual tree from the layout model.
    /// Call this after making structural changes to the layout.
    /// </summary>
    public void RebuildVisualTree()
    {
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
    /// Collects all panel IDs currently present in the layout and syncs them to the registry.
    /// </summary>
    private void SyncRegistryVisibility()
    {
        _dockableRegistry?.SyncVisibility(_panelRegistry.Keys);
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

        // Subscribe to panel close requests
        tabGroup.PanelCloseRequested += (sender, panelToClose) =>
        {
            if (panelToClose != null && LayoutModel != null)
            {
                // Remove panel from layout model
                DockOperation.RemovePanel(LayoutModel, panelToClose);
                RebuildVisualTree();
            }
        };

        // Subscribe to maximize / restore requests
        tabGroup.MaximizeRequested += (sender, groupNode) =>
        {
            if (groupNode != null)
                MaximizeGroup(groupNode);
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
    /// Handles layout model changes by rebuilding the visual tree.
    /// </summary>
    private void OnLayoutModelChanged(object sender, EventArgs e)
    {
        RebuildVisualTree();
        DockLayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Subscribes to PropertyChanged events for a node and its children.
    /// </summary>
    private void SubscribeToNode(DockNode node)
    {
        if (node == null)
        {
            return;
        }

        if (node is INotifyPropertyChanged notifyNode)
        {
            notifyNode.PropertyChanged += OnNodePropertyChanged;
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
    /// Unsubscribes from PropertyChanged events for a node and its children.
    /// </summary>
    private void UnsubscribeFromNode(DockNode node)
    {
        if (node == null)
        {
            return;
        }

        if (node is INotifyPropertyChanged notifyNode)
        {
            notifyNode.PropertyChanged -= OnNodePropertyChanged;
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

    private void OnNodePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // ActivePanelId / ActivePanel changes on a tab group do NOT require a visual-tree
        // rebuild: the tab strip already handles them in OnGroupNodePropertyChanged and
        // OnTabGroupPropertyChanged above.  Triggering a full rebuild here would recreate
        // every MGDockSplitContainer from the model — resetting any split ratio that was
        // still in-flight (e.g. if CommitRatioToModel fired its own PropertyChanged before
        // the model write completed).
        if (sender is DockTabGroupNode &&
            (e.PropertyName == nameof(DockTabGroupNode.ActivePanelId) ||
             e.PropertyName == nameof(DockTabGroupNode.ActivePanel)))
        {
            return;
        }

        // TODO: Replace the full rebuild with targeted partial updates for other changes.
        RebuildVisualTree();
    }

    private void OnTabGroupPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockTabGroupNode.ActivePanelId) || 
            e.PropertyName == nameof(DockTabGroupNode.ActivePanel))
        {
            if (sender is DockTabGroupNode tabGroup && tabGroup.ActivePanel != null)
            {
                ActivePanelChanged?.Invoke(this, tabGroup.ActivePanel);
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

    #endregion Drop Zone Calculation
}