using System.ComponentModel;

namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// Root model for the docking layout system.
/// Manages the tree structure of docking nodes and provides operations on the layout.
/// </summary>
public class DockLayoutModel : INotifyPropertyChanged
{
    private DockNode _rootNode;
    /// <summary>
    /// Root node of the docking layout tree.
    /// Can be a DockSplitNode or DockTabGroupNode.
    /// </summary>
    public DockNode RootNode
    {
        get => _rootNode;
        set
        {
            if (_rootNode != value)
            {
                // Unsubscribe from old root if exists
                if (_rootNode != null)
                {
                    UnsubscribeFromNodeTree(_rootNode);
                }

                _rootNode = value;

                // Subscribe to new root
                if (_rootNode != null)
                {
                    SubscribeToNodeTree(_rootNode);
                    _rootNode.Parent = null; // Root has no parent
                }

                OnPropertyChanged(nameof(RootNode));
                LayoutChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Event raised when the layout structure changes significantly.
    /// </summary>
    public event EventHandler LayoutChanged;

    /// <summary>
    /// Event raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    #region Auto-Hide Store

    /// <summary>
    /// Panels that are currently auto-hidden (unpinned), grouped by the edge they appear on.
    /// The order within each list is the order they appear in the strip.
    /// </summary>
    private readonly Dictionary<AutoHideSide, List<DockPanelNode>> _autoHideStore
        = new Dictionary<AutoHideSide, List<DockPanelNode>>
        {
            { AutoHideSide.Left,   new List<DockPanelNode>() },
            { AutoHideSide.Right,  new List<DockPanelNode>() },
            { AutoHideSide.Top,    new List<DockPanelNode>() },
            { AutoHideSide.Bottom, new List<DockPanelNode>() },
        };

    /// <summary>
    /// Returns a read-only view of the auto-hidden panels on the specified side.
    /// </summary>
    public IReadOnlyList<DockPanelNode> GetAutoHidePanels(AutoHideSide side)
        => _autoHideStore[side].AsReadOnly();

    /// <summary>
    /// Returns all auto-hidden panels across all sides.
    /// </summary>
    public IEnumerable<DockPanelNode> GetAllAutoHidePanels()
        => _autoHideStore.Values.SelectMany(l => l);

    /// <summary>
    /// Adds <paramref name="panel"/> to the auto-hide store on <paramref name="side"/>.
    /// Marks the panel as unpinned.  Does nothing if the panel is already in the store.
    /// </summary>
    public void AddToAutoHide(DockPanelNode panel, AutoHideSide side)
    {
        if (panel == null)
        {
            return;
        }

        // Remove from any existing side first (safety)
        RemoveFromAutoHide(panel);
        panel.IsPinned = false;
        panel.AutoHideSide = side;
        _autoHideStore[side].Add(panel);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes <paramref name="panel"/> from the auto-hide store (regardless of which side it is on).
    /// Marks the panel as pinned again.  Returns true if the panel was found and removed.
    /// </summary>
    public bool RemoveFromAutoHide(DockPanelNode panel)
    {
        if (panel == null)
        {
            return false;
        }

        foreach (var list in _autoHideStore.Values)
        {
            if (list.Remove(panel))
            {
                panel.IsPinned = true;
                LayoutChanged?.Invoke(this, EventArgs.Empty);
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether any panel is currently auto-hidden on <paramref name="side"/>.</summary>
    public bool HasAutoHidePanels(AutoHideSide side) => _autoHideStore[side].Count > 0;

    /// <summary>Whether any panel anywhere is currently auto-hidden.</summary>
    public bool HasAnyAutoHidePanels() => _autoHideStore.Values.Any(l => l.Count > 0);

    #endregion Auto-Hide Store

    #region Panel Placements

    /// <summary>
    /// Remembered places of panels that left the layout tree (floated, auto-hidden or closed),
    /// indexed by panel id. Shared by all three cases; see <see cref="DockPanelPlacement"/>.
    /// </summary>
    private readonly Dictionary<string, DockPanelPlacement> _placements = new Dictionary<string, DockPanelPlacement>();

    /// <summary>
    /// Read-only view of the remembered placements, indexed by panel id.
    /// </summary>
    internal IReadOnlyDictionary<string, DockPanelPlacement> Placements => _placements;

    /// <summary>
    /// Attempts to retrieve the remembered placement of <paramref name="panelId"/>.
    /// </summary>
    /// <param name="panelId">Id of the panel to look up.</param>
    /// <param name="placement">The remembered placement, or null when none is stored.</param>
    /// <returns>True when a placement was found.</returns>
    internal bool TryGetPlacement(string panelId, out DockPanelPlacement placement)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            placement = null;
            return false;
        }

        return _placements.TryGetValue(panelId, out placement);
    }

    /// <summary>
    /// Remembers <paramref name="placement"/> as the place <paramref name="panelId"/> left.
    /// Overwrites any previously remembered placement for that panel.
    /// </summary>
    /// <param name="panelId">Id of the panel the placement belongs to.</param>
    /// <param name="placement">The placement to remember.</param>
    internal void SetPlacement(string panelId, DockPanelPlacement placement)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            throw new ArgumentException("Panel ID cannot be null or empty.", nameof(panelId));
        }

        if (placement == null)
        {
            throw new ArgumentNullException(nameof(placement));
        }

        _placements[panelId] = placement;
    }

    /// <summary>
    /// Forgets the remembered placement of <paramref name="panelId"/>, if any.
    /// </summary>
    /// <param name="panelId">Id of the panel whose placement should be forgotten.</param>
    /// <returns>True when a placement was found and removed.</returns>
    internal bool RemovePlacement(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            return false;
        }

        return _placements.Remove(panelId);
    }

    /// <summary>
    /// Whether any remembered placement still references <paramref name="group"/>. A referenced
    /// tab group is kept in the tree as a hidden placeholder even while empty.
    /// </summary>
    /// <param name="group">The tab group to check.</param>
    internal bool IsPlaceholderReferenced(DockTabGroupNode group)
    {
        if (group == null)
        {
            return false;
        }

        return _placements.Values.Any(p => p.GroupId == group.Id);
    }

    #endregion Panel Placements

    #region Floating Store

    /// <summary>
    /// Tab groups currently shown in floating windows.
    /// </summary>
    private readonly List<DockFloatingGroup> _floatingGroups = new List<DockFloatingGroup>();

    /// <summary>
    /// Read-only view of the floating groups currently in the model.
    /// </summary>
    public IReadOnlyList<DockFloatingGroup> FloatingGroups => _floatingGroups.AsReadOnly();

    /// <summary>
    /// Adds <paramref name="floatingGroup"/> to the floating store and subscribes to its node
    /// tree so its structural changes raise <see cref="LayoutChanged"/>, as for the auto-hide store.
    /// </summary>
    /// <param name="floatingGroup">The floating group to add.</param>
    public void AddFloatingGroup(DockFloatingGroup floatingGroup)
    {
        if (floatingGroup == null)
        {
            throw new ArgumentNullException(nameof(floatingGroup));
        }

        if (_floatingGroups.Contains(floatingGroup))
        {
            throw new InvalidOperationException("This floating group has already been added to the model.");
        }

        _floatingGroups.Add(floatingGroup);
        SubscribeToNodeTree(floatingGroup.Group);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Removes <paramref name="floatingGroup"/> from the floating store and unsubscribes from its
    /// node tree. Returns false, without raising <see cref="LayoutChanged"/>, when it was not found.
    /// </summary>
    /// <param name="floatingGroup">The floating group to remove.</param>
    /// <returns>True when the floating group was found and removed.</returns>
    public bool RemoveFloatingGroup(DockFloatingGroup floatingGroup)
    {
        if (floatingGroup == null)
        {
            return false;
        }

        if (!_floatingGroups.Remove(floatingGroup))
        {
            return false;
        }

        UnsubscribeFromNodeTree(floatingGroup.Group);
        LayoutChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    /// <summary>
    /// Finds the floating group currently holding the panel with id <paramref name="panelId"/>.
    /// </summary>
    /// <param name="panelId">Id of the panel to look for.</param>
    /// <returns>The floating group holding the panel, or null when it is not floating.</returns>
    public DockFloatingGroup FindFloatingGroupOf(string panelId)
    {
        if (string.IsNullOrEmpty(panelId))
        {
            return null;
        }

        return _floatingGroups.FirstOrDefault(fg => fg.Group.Panels.Any(p => p.Id == panelId));
    }

    #endregion Floating Store

    /// <summary>
    /// Creates a new empty DockLayoutModel.
    /// </summary>
    public DockLayoutModel()
    {
    }

    /// <summary>
    /// Creates a new DockLayoutModel with specified root node.
    /// </summary>
    /// <param name="rootNode">Initial root node.</param>
    public DockLayoutModel(DockNode rootNode)
    {
        RootNode = rootNode;
    }

    /// <summary>
    /// Finds a node by its ID anywhere in the layout tree.
    /// </summary>
    /// <param name="id">The ID to search for.</param>
    /// <returns>The node with matching ID, or null if not found.</returns>
    public DockNode FindNodeById(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        return RootNode?.FindNodeById(id);
    }

    /// <summary>
    /// Finds a panel node by its ID.
    /// </summary>
    /// <param name="id">The panel ID to search for.</param>
    /// <returns>The DockPanelNode with matching ID, or null if not found.</returns>
    public DockPanelNode FindPanelById(string id)
    {
        return FindNodeById(id) as DockPanelNode;
    }

    /// <summary>
    /// Gets all panel nodes in the layout tree.
    /// </summary>
    /// <returns>Collection of all DockPanelNode instances.</returns>
    public IEnumerable<DockPanelNode> GetAllPanels()
    {
        if (RootNode == null)
        {
            return Enumerable.Empty<DockPanelNode>();
        }

        return GetAllPanelsRecursive(RootNode);
    }

    private IEnumerable<DockPanelNode> GetAllPanelsRecursive(DockNode node)
    {
        if (node is DockPanelNode panel)
        {
            yield return panel;
        }
        else
        {
            foreach (var child in node.GetChildren())
            {
                if (child != null)
                {
                    foreach (var childPanel in GetAllPanelsRecursive(child))
                    {
                        yield return childPanel;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets all tab group nodes in the layout tree.
    /// </summary>
    /// <returns>Collection of all DockTabGroupNode instances.</returns>
    public IEnumerable<DockTabGroupNode> GetAllTabGroups()
    {
        if (RootNode == null)
        {
            return Enumerable.Empty<DockTabGroupNode>();
        }

        return GetAllTabGroupsRecursive(RootNode);
    }

    private IEnumerable<DockTabGroupNode> GetAllTabGroupsRecursive(DockNode node)
    {
        if (node is DockTabGroupNode tabGroup)
        {
            yield return tabGroup;
        }

        foreach (var child in node.GetChildren())
        {
            if (child != null)
            {
                foreach (var childGroup in GetAllTabGroupsRecursive(child))
                {
                    yield return childGroup;
                }
            }
        }
    }

    /// <summary>
    /// Validates the integrity of the layout tree.
    /// Checks for cycles, orphaned nodes, and invalid parent references.
    /// </summary>
    /// <returns>True if the tree is valid, false if issues were detected.</returns>
    public bool ValidateTree()
    {
        if (RootNode == null)
        {
            return true; // Empty tree is valid
        }

        var visited = new HashSet<string>();
        return ValidateNodeRecursive(RootNode, null, visited);
    }

    private bool ValidateNodeRecursive(DockNode node, DockNode expectedParent, HashSet<string> visited)
    {
        if (node == null)
        {
            return true;
        }

        // Check for cycles
        if (visited.Contains(node.Id))
        {
            return false;
        }

        visited.Add(node.Id);

        // Validate parent reference
        if (node.Parent != expectedParent)
        {
            return false;
        }

        // Validate children
        foreach (var child in node.GetChildren())
        {
            if (child != null)
            {
                if (!ValidateNodeRecursive(child, node, visited))
                {
                    return false;
                }
            }
        }

        // Additional validation for SplitNode
        if (node is DockSplitNode splitNode)
        {
            // FirstChild and SecondChild might be temporarily null during construction
        }

        return true;
    }

    /// <summary>
    /// Subscribes to PropertyChanged events for all nodes in the subtree.
    /// Used to propagate layout changes. Idempotent: a node that is already subscribed
    /// (e.g. a panel that was removed from the tree via <see cref="DockOperation.RemovePanel"/>
    /// without an explicit unsubscribe, then re-added to a floating group) is not subscribed
    /// twice, so a single structural change on it still raises <see cref="LayoutChanged"/> once.
    /// </summary>
    private void SubscribeToNodeTree(DockNode node)
    {
        if (node == null)
        {
            return;
        }

        node.PropertyChanged -= OnNodePropertyChanged;
        node.PropertyChanged += OnNodePropertyChanged;

        foreach (var child in node.GetChildren())
        {
            if (child != null)
            {
                SubscribeToNodeTree(child);
            }
        }
    }

    /// <summary>
    /// Unsubscribes from PropertyChanged events for all nodes in the subtree.
    /// </summary>
    private void UnsubscribeFromNodeTree(DockNode node)
    {
        if (node == null)
        {
            return;
        }

        node.PropertyChanged -= OnNodePropertyChanged;

        foreach (var child in node.GetChildren())
        {
            if (child != null)
            {
                UnsubscribeFromNodeTree(child);
            }
        }
    }

    private void OnNodePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        // ActivePanelId / ActivePanel changes are purely visual state (which tab is selected),
        // not structural layout changes.  Firing LayoutChanged here would cause a full
        // visual-tree rebuild on every tab switch — including during Ctrl+Tab cycling.
        if (sender is DockTabGroupNode &&
            (e.PropertyName == nameof(DockTabGroupNode.ActivePanelId) ||
             e.PropertyName == nameof(DockTabGroupNode.ActivePanel)))
        {
            return;
        }

        // Propagate layout change notification
        // This allows the view layer to know when to rebuild
        LayoutChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    /// <summary>
    /// Clears the entire layout tree including all auto-hidden panels.
    /// Unsubscribes from auto-hidden panels before clearing them so that
    /// their subsequent property changes do not trigger spurious LayoutChanged events.
    /// </summary>
    public void Clear()
    {
        // Unsubscribe from auto-hidden panels (they're NOT in the layout tree,
        // so UnsubscribeFromNodeTree in the RootNode setter doesn't cover them).
        foreach (var list in _autoHideStore.Values)
        {
            foreach (var panel in list)
            {
                panel.PropertyChanged -= OnNodePropertyChanged;
            }

            list.Clear();
        }

        // Unsubscribe from floating groups (they're NOT in the layout tree either).
        foreach (var floatingGroup in _floatingGroups)
        {
            UnsubscribeFromNodeTree(floatingGroup.Group);
        }

        _floatingGroups.Clear();
        _placements.Clear();

        RootNode = null;
    }

    public override string ToString()
    {
        var panelCount = GetAllPanels().Count();
        var groupCount = GetAllTabGroups().Count();
        return $"DockLayoutModel (Panels: {panelCount}, Groups: {groupCount})";
    }
}