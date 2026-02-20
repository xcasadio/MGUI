using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;

namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// Represents a group of tabbed panels in the docking layout.
/// Contains multiple DockPanelNode instances displayed as tabs.
/// </summary>
public class DockTabGroupNode : DockNode
{
    /// <summary>
    /// Collection of panels in this tab group.
    /// </summary>
    public ObservableCollection<DockPanelNode> Panels { get; }

    private string _activePanelId;
    /// <summary>
    /// ID of the currently active (selected) panel in this group.
    /// </summary>
    public string ActivePanelId
    {
        get => _activePanelId;
        private set
        {
            if (_activePanelId != value)
            {
                _activePanelId = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ActivePanel));
            }
        }
    }

    /// <summary>
    /// Gets the currently active panel, or null if ActivePanelId doesn't match any panel.
    /// </summary>
    public DockPanelNode ActivePanel => Panels.FirstOrDefault(p => p.Id == ActivePanelId);

    private bool _isDocumentArea;
    /// <summary>
    /// When true, this tab group is designated as the central "Document Area".
    /// Document panels (<see cref="DockableType.Document"/>) are preferentially placed here,
    /// and Tool panels are prevented from being added when docking rules are enforced.
    /// Only one group per layout should be the Document Area.
    /// </summary>
    public bool IsDocumentArea
    {
        get => _isDocumentArea;
        set
        {
            if (_isDocumentArea != value)
            {
                _isDocumentArea = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Creates a new empty DockTabGroupNode.
    /// </summary>
    public DockTabGroupNode() : base()
    {
        Panels = new ObservableCollection<DockPanelNode>();
        Panels.CollectionChanged += OnPanelsCollectionChanged;
    }

    /// <summary>
    /// Creates a new DockTabGroupNode with specified ID.
    /// </summary>
    /// <param name="id">Unique identifier for this node.</param>
    public DockTabGroupNode(string id) : base(id)
    {
        Panels = new ObservableCollection<DockPanelNode>();
        Panels.CollectionChanged += OnPanelsCollectionChanged;
    }

    private void OnPanelsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Update parent references for added panels
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (DockPanelNode panel in e.NewItems)
            {
                if (panel != null)
                {
                    SetChildParent(panel, this);
                }
            }
        }

        // Clear parent references for removed panels
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (DockPanelNode panel in e.OldItems)
            {
                if (panel != null)
                {
                    SetChildParent(panel, null);
                }
            }
        }

        // Handle replace
        if (e.Action == NotifyCollectionChangedAction.Replace)
        {
            if (e.OldItems != null)
            {
                foreach (DockPanelNode panel in e.OldItems)
                {
                    if (panel != null)
                    {
                        SetChildParent(panel, null);
                    }
                }
            }
            if (e.NewItems != null)
            {
                foreach (DockPanelNode panel in e.NewItems)
                {
                    if (panel != null)
                    {
                        SetChildParent(panel, this);
                    }
                }
            }
        }

        // Handle reset
        if (e.Action == NotifyCollectionChangedAction.Reset)
        {
            // All panels cleared, update active panel if necessary
            if (!string.IsNullOrEmpty(ActivePanelId) && !Panels.Any(p => p.Id == ActivePanelId))
            {
                SetActivePanel(Panels.FirstOrDefault()?.Id);
            }
        }

        // Auto-update active panel if it was removed
        if (e.Action == NotifyCollectionChangedAction.Remove && e.OldItems != null)
        {
            foreach (DockPanelNode panel in e.OldItems)
            {
                if (panel?.Id == ActivePanelId)
                {
                    // Active panel was removed, select first available or clear
                    SetActivePanel(Panels.FirstOrDefault()?.Id);
                    break;
                }
            }
        }

        // If this was the first panel added, make it active by default
        if (e.Action == NotifyCollectionChangedAction.Add && Panels.Count == 1)
        {
            SetActivePanel(Panels[0].Id);
        }
    }

    /// <summary>
    /// Adds a panel to this tab group at the specified index.
    /// </summary>
    /// <param name="panel">The panel to add.</param>
    /// <param name="index">Index where to insert (-1 to append at end).</param>
    public void AddPanel(DockPanelNode panel, int index = -1)
    {
        if (panel == null)
        {
            throw new ArgumentNullException(nameof(panel));
        }

        if (Panels.Any(p => p.Id == panel.Id))
        {
            throw new InvalidOperationException($"A panel with ID '{panel.Id}' already exists in this group.");
        }

        if (index < 0 || index >= Panels.Count)
        {
            Panels.Add(panel);
        }
        else
        {
            Panels.Insert(index, panel);
        }
    }

    /// <summary>
    /// Removes the specified panel from this tab group.
    /// </summary>
    /// <param name="panel">The panel to remove.</param>
    /// <returns>True if the panel was removed, false if not found.</returns>
    public bool RemovePanel(DockPanelNode panel)
    {
        if (panel == null)
        {
            return false;
        }

        return Panels.Remove(panel);
    }

    /// <summary>
    /// Removes the panel with the specified ID from this tab group.
    /// </summary>
    /// <param name="panelId">ID of the panel to remove.</param>
    /// <returns>True if the panel was removed, false if not found.</returns>
    public bool RemovePanelById(string panelId)
    {
        var panel = Panels.FirstOrDefault(p => p.Id == panelId);
        if (panel != null)
        {
            return Panels.Remove(panel);
        }
        return false;
    }

    /// <summary>
    /// Sets the active (selected) panel by ID.
    /// </summary>
    /// <param name="panelId">ID of the panel to activate. Can be null to clear selection.</param>
    public void SetActivePanel(string panelId)
    {
        // Validate that panel exists if not null
        if (!string.IsNullOrEmpty(panelId) && !Panels.Any(p => p.Id == panelId))
        {
            throw new ArgumentException($"No panel with ID '{panelId}' exists in this group.", nameof(panelId));
        }

        ActivePanelId = panelId;
    }

    /// <summary>
    /// Gets all immediate children (all panels in this group).
    /// </summary>
    public override IEnumerable<DockNode> GetChildren()
    {
        return Panels.Cast<DockNode>();
    }

    /// <summary>
    /// Removes the specified child node (panel) from this group.
    /// </summary>
    /// <param name="child">The child node to remove.</param>
    public override void RemoveChild(DockNode child)
    {
        if (child is DockPanelNode panel)
        {
            Panels.Remove(panel);
        }
    }

    /// <summary>
    /// Checks if this tab group is empty (has no panels).
    /// </summary>
    public bool IsEmpty => Panels.Count == 0;

    /// <summary>
    /// Gets the index of the specified panel in the collection.
    /// </summary>
    /// <param name="panel">The panel to find.</param>
    /// <returns>Zero-based index, or -1 if not found.</returns>
    public int IndexOf(DockPanelNode panel)
    {
        return Panels.IndexOf(panel);
    }

    /// <summary>
    /// Reorders a panel to a new index within this group.
    /// </summary>
    /// <param name="panel">The panel to reorder.</param>
    /// <param name="newIndex">The new zero-based index. Use -1 to move to end.</param>
    /// <returns>True if the panel was reordered, false if not found or index invalid.</returns>
    public bool ReorderPanel(DockPanelNode panel, int newIndex)
    {
        if (panel == null)
        {
            return false;
        }

        int currentIndex = Panels.IndexOf(panel);
        if (currentIndex < 0)
        {
            return false; // Panel not in this group
        }

        // Normalize index
        if (newIndex < 0 || newIndex >= Panels.Count)
        {
            newIndex = Panels.Count - 1;
        }

        // No change needed
        if (currentIndex == newIndex)
        {
            return false;
        }

        Panels.RemoveAt(currentIndex);

        // The newIndex parameter already represents the FINAL desired position
        // after the CalculateTabIndex algorithm's adjustments.
        // We just need to clamp it to the valid range after removal.
        int insertIndex = newIndex;
        
        // After removing one element, the max valid index is Panels.Count
        if (insertIndex > Panels.Count)
        {
            insertIndex = Panels.Count;
        }
        if (insertIndex < 0)
        {
            insertIndex = 0;
        }

        Panels.Insert(insertIndex, panel);

        return true;
    }

    /// <summary>
    /// Calculates the effective minimum width for this tab group.
    /// Returns the maximum minimum width of all panels, since they're stacked.
    /// </summary>
    /// <returns>The minimum width in pixels for this tab group.</returns>
    public override int CalculateEffectiveMinWidth()
    {
        if (Panels.Count == 0)
        {
            return 100; // Default minimum for empty group
        }

        // Tab group width should be at least the widest panel's minimum
        int maxPanelMinWidth = Panels.Max(p => p?.CalculateEffectiveMinWidth() ?? 100);
        
        // Also consider space needed for tab headers (approximate: 100px per tab minimum, but allow overlap)
        // For simplicity, ensure at least 150px to show a few tabs
        return Math.Max(maxPanelMinWidth, 150);
    }

    /// <summary>
    /// Calculates the effective minimum height for this tab group.
    /// Returns the maximum minimum height of all panels plus tab header space.
    /// </summary>
    /// <returns>The minimum height in pixels for this tab group.</returns>
    public override int CalculateEffectiveMinHeight()
    {
        if (Panels.Count == 0)
        {
            return 100; // Default minimum for empty group
        }

        // Tab group height = tab header height + content height
        const int TabHeaderHeight = 30; // Approximate height of tab headers
        
        int maxPanelMinHeight = Panels.Max(p => p?.CalculateEffectiveMinHeight() ?? 100);
        
        return TabHeaderHeight + maxPanelMinHeight;
    }

    public override string ToString()
    {
        return $"TabGroup (Id: {Id}, Panels: {Panels.Count}, Active: {ActivePanelId})";
    }
}