using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Container that displays a horizontal strip of tabs and the content of the active panel.
/// Bound to a DockTabGroupNode model.
/// </summary>
public class MGDockTabGroup : MGElement
{
    private DockTabGroupNode _groupNode;
    /// <summary>
    /// The tab group node model this control is bound to.
    /// </summary>
    public DockTabGroupNode GroupNode
    {
        get => _groupNode;
        set
        {
            if (_groupNode != value)
            {
                // Unsubscribe from old node
                if (_groupNode != null)
                {
                    _groupNode.Panels.CollectionChanged -= OnPanelsCollectionChanged;
                    _groupNode.PropertyChanged -= OnGroupNodePropertyChanged;
                }

                _groupNode = value;

                // Subscribe to new node
                if (_groupNode != null)
                {
                    _groupNode.Panels.CollectionChanged += OnPanelsCollectionChanged;
                    _groupNode.PropertyChanged += OnGroupNodePropertyChanged;
                }

                RebuildTabHeaders();
                UpdateActiveContent();
                NPC(nameof(GroupNode));
            }
        }
    }

    private MGStackPanel _tabHeadersPanel;
    private MGElement _activeContentContainer;
    private readonly Dictionary<string, MGDockTabItem> _tabItems = new Dictionary<string, MGDockTabItem>();

    // ── Overflow / scroll state ────────────────────────────────────────
    private int _tabScrollIndex = 0;
    private bool _isOverflowing = false;
    private int _visibleTabCount = 0;

    // Compact strip buttons created in the constructor
    private MGBorder _scrollLeftBtn;
    private MGBorder _scrollRightBtn;
    private MGBorder _dropdownBtn;

    private const int ScrollBtnWidth   = 22;
    private const int DropdownBtnWidth = 22;

    private int _tabHeaderHeight = 30;
    /// <summary>
    /// Height of the tab header area in pixels.
    /// </summary>
    public int TabHeaderHeight
    {
        get => _tabHeaderHeight;
        set
        {
            if (_tabHeaderHeight != value)
            {
                _tabHeaderHeight = value;
                LayoutChanged(this, true);
                NPC(nameof(TabHeaderHeight));
            }
        }
    }

    /// <summary>
    /// The layout bounds of the tab headers area.
    /// Updated during layout pass.
    /// </summary>
    public Rectangle TabHeadersBounds { get; private set; }

    /// <summary>
    /// Event raised when the active panel changes.
    /// </summary>
    public event EventHandler<DockPanelNode> ActivePanelChanged;

    /// <summary>
    /// Event raised when a panel close is requested.
    /// </summary>
    public event EventHandler<DockPanelNode> PanelCloseRequested;

    /// <summary>
    /// Creates a new MGDockTabGroup.
    /// </summary>
    /// <param name="window">The parent window.</param>
    /// <param name="groupNode">The tab group node model.</param>
    public MGDockTabGroup(MGWindow window, DockTabGroupNode groupNode = null) : base(window, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            // Create tab headers panel
            _tabHeadersPanel = new MGStackPanel(window, Orientation.Horizontal)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 0
            };
            _tabHeadersPanel.SetParent(this);

            // Create empty content container
            _activeContentContainer = CreateEmptyContent();
            _activeContentContainer.SetParent(this);

            // Set group node (will trigger rebuild)
            if (groupNode != null)
            {
                GroupNode = groupNode;
            }

            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;

            // ── Overflow buttons (always present, collapsed until needed) ─────
            _scrollLeftBtn = CreateCompactButton(window, "‹", () => ScrollLeft());
            _scrollLeftBtn.Visibility = Visibility.Collapsed;
            _scrollLeftBtn.SetParent(this);

            _scrollRightBtn = CreateCompactButton(window, "›", () => ScrollRight());
            _scrollRightBtn.Visibility = Visibility.Collapsed;
            _scrollRightBtn.SetParent(this);

            _dropdownBtn = CreateCompactButton(window, "▾", () => ShowDropdown());
            _dropdownBtn.SetParent(this);
        }
    }

    /// <summary>
    /// Creates a small borderless button with a text glyph for the tab header strip.
    /// </summary>
    private static MGBorder CreateCompactButton(MGWindow window, string glyph, Action onClick)
    {
        var body = new MGBorder(window, new XAML.Thickness(0).ToThickness(), (IFillBrush)null)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment   = VerticalAlignment.Top
        };

        // Use the visual-state brush system for hover / press highlighting
        body.BackgroundBrush = new VisualStateFillBrush(
            (IFillBrush)null,
            new Color(70, 70, 74),
            PressedModifierType.Darken,
            0.10f);

        var label = new MGTextBlock(window, glyph)
        {
            FontSize             = 14,
            HorizontalAlignment  = HorizontalAlignment.Center,
            VerticalAlignment    = VerticalAlignment.Center,
            Padding              = new XAML.Thickness(4, 2, 4, 2).ToThickness()
        };

        body.MouseHandler.LMBReleasedInside += (_, e) =>
        {
            if (!e.IsHandled)
            {
                onClick();
                e.SetHandledBy(body, false);
            }
        };

        body.SetContent(label);
        return body;
    }

    /// <summary>
    /// Rebuilds all tab headers from the current panels in the group node.
    /// </summary>
    private void RebuildTabHeaders()
    {
        // Clear existing tabs
        _tabHeadersPanel.TryRemoveAll();
        _tabItems.Clear();

        if (GroupNode == null || GroupNode.IsEmpty)
        {
            return;
        }

        // Create tab item for each panel (all are added; overflow logic controls visibility)
        foreach (var panel in GroupNode.Panels)
        {
            var tabItem = new MGDockTabItem(ParentWindow, panel)
            {
                IsActive = (panel.Id == GroupNode.ActivePanelId)
            };

            // Subscribe to tab click
            tabItem.TabClicked += (sender, clickedPanel) =>
            {
                if (GroupNode != null && clickedPanel != null)
                {
                    GroupNode.SetActivePanel(clickedPanel.Id);
                }
            };

            // Subscribe to close request
            tabItem.CloseRequested += (sender, panelToClose) =>
            {
                PanelCloseRequested?.Invoke(this, panelToClose);
            };

            // Context-menu extra events
            tabItem.CloseOthersRequested += (sender, panelToKeep) => OnCloseOthers(panelToKeep);
            tabItem.CloseAllRequested    += (sender, _)           => OnCloseAll();

            _tabHeadersPanel.TryAddChild(tabItem);
            _tabItems[panel.Id] = tabItem;
        }

        // Clamp scroll after panels changed
        ClampScrollIndex();

        // Force layout update for tab headers panel
        _tabHeadersPanel.InvalidateLayout();
    }

    /// <summary>Handles "Close Others" from a tab context-menu.</summary>
    private void OnCloseOthers(DockPanelNode panelToKeep)
    {
        if (GroupNode == null)
            return;

        var toClose = GroupNode.Panels
            .Where(p => p.Id != panelToKeep.Id && p.CanClose)
            .ToList(); // snapshot

        foreach (var p in toClose)
            PanelCloseRequested?.Invoke(this, p);
    }

    /// <summary>Handles "Close All" from a tab context-menu.</summary>
    private void OnCloseAll()
    {
        if (GroupNode == null)
            return;

        var toClose = GroupNode.Panels
            .Where(p => p.CanClose)
            .ToList(); // snapshot

        foreach (var p in toClose)
            PanelCloseRequested?.Invoke(this, p);
    }

    /// <summary>
    /// Updates the active content based on the group node's active panel.
    /// </summary>
    private void UpdateActiveContent()
    {
        // Remove old content
        if (_activeContentContainer != null && _activeContentContainer.Parent == this)
        {
            _activeContentContainer.SetParent(null);
        }

        if (GroupNode == null || GroupNode.IsEmpty)
        {
            _activeContentContainer = CreateEmptyContent();
            _activeContentContainer.SetParent(this);
            LayoutChanged(this, true);
            return;
        }

        // Get active panel
        var activePanel = GroupNode.ActivePanel;
        if (activePanel == null)
        {
            _activeContentContainer = CreateEmptyContent();
            _activeContentContainer.SetParent(this);
            LayoutChanged(this, true);
            return;
        }
            
        // Get or create panel content
        MGElement content = activePanel.GetOrCreateContent();
            
        if (content == null)
        {
            _activeContentContainer = CreatePlaceholderContent(activePanel.Title);
        }
        else
        {
            _activeContentContainer = content;
        }

        _activeContentContainer.SetParent(this);
        LayoutChanged(this, true);

        // Notify
        ActivePanelChanged?.Invoke(this, activePanel);
    }

    /// <summary>
    /// Updates the active state of tab items to match the current active panel.
    /// </summary>
    private void UpdateTabActiveStates()
    {
        if (GroupNode == null)
        {
            return;
        }

        foreach (var kvp in _tabItems)
        {
            kvp.Value.IsActive = (kvp.Key == GroupNode.ActivePanelId);
        }
    }

    /// <summary>
    /// Creates an empty content placeholder.
    /// </summary>
    private MGElement CreateEmptyContent()
    {
        return new MGTextBlock(ParentWindow, "Empty Tab Group\n\nNo panels to display.")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new XAML.Thickness(20).ToThickness()
        };
    }

    /// <summary>
    /// Creates a placeholder content element.
    /// </summary>
    private MGElement CreatePlaceholderContent(string title)
    {
        return new MGTextBlock(ParentWindow, $"Panel: {title}\n\n(No content factory defined)")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new XAML.Thickness(20).ToThickness()
        };
    }

    private void OnPanelsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Rebuild tabs when collection changes
        RebuildTabHeaders();
        UpdateActiveContent();
    }

    private void OnGroupNodePropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DockTabGroupNode.ActivePanelId) ||
            e.PropertyName == nameof(DockTabGroupNode.ActivePanel))
        {
            UpdateTabActiveStates();
            UpdateActiveContent();

            // Ensure the newly active tab is inside the visible scroll window
            if (GroupNode?.ActivePanelId != null)
                EnsureTabVisible(GroupNode.ActivePanelId);
        }
    }

    public override IEnumerable<MGElement> GetChildren()
    {
        // header-strip elements (tab panel + chromeless buttons)
        if (_tabHeadersPanel != null)
            yield return _tabHeadersPanel;
        if (_scrollLeftBtn  != null)
            yield return _scrollLeftBtn;
        if (_scrollRightBtn != null)
            yield return _scrollRightBtn;
        if (_dropdownBtn    != null)
            yield return _dropdownBtn;

        if (_activeContentContainer != null)
            yield return _activeContentContainer;
    }

    protected override Thickness UpdateContentMeasurement(Size AvailableSize)
    {
        // ── Header row: tabs + optional scroll buttons + dropdown ──────────
        int headerWidth  = AvailableSize.Width;
        int headerHeight = TabHeaderHeight;

        // Measure tab-strip (collapsed tabs contribute 0 width)
        if (_tabHeadersPanel != null)
        {
            _tabHeadersPanel.UpdateMeasurement(
                new Size(headerWidth, headerHeight),
                out _, out _, out _, out _);
        }

        // Measure content
        Thickness contentSize = new Thickness(0);
        if (_activeContentContainer != null)
        {
            Size contentAvailableSize = new Size(headerWidth, Math.Max(0, AvailableSize.Height - headerHeight));
            _activeContentContainer.UpdateMeasurement(contentAvailableSize, out _, out contentSize, out _, out _);
        }

        int maxWidth   = Math.Max(headerWidth, contentSize.Width);
        int totalHeight = headerHeight + contentSize.Height;
        return new Thickness(maxWidth, totalHeight, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        if (_tabHeadersPanel == null)
            return;

        int panelCount = GroupNode?.Panels.Count ?? 0;

        // ── Determine overflow ────────────────────────────────────────────
        // Reserve room for the dropdown button on the right edge at all times.
        int headerAvailableWidth = Bounds.Width - DropdownBtnWidth;

        // Estimate total tabs width using the per-tab minimum (conservative).
        // On subsequent frames tab LayoutBounds are valid; use the larger value.
        int totalEstimated = 0;
        if (_tabItems.Count > 0)
        {
            foreach (var tab in _tabItems.Values)
            {
                int w = Math.Max(tab.MinTabWidth, tab.LayoutBounds.Width);
                totalEstimated += w;
            }
        }

        bool newOverflowing = totalEstimated > headerAvailableWidth;

        // Width available for the tab strip itself
        int tabStripWidth = headerAvailableWidth - (newOverflowing ? ScrollBtnWidth * 2 : 0);
        tabStripWidth = Math.Max(0, tabStripWidth);

        // How many tabs fit inside tabStripWidth?
        int newVisibleCount;
        if (!newOverflowing)
        {
            newVisibleCount = panelCount;
        }
        else
        {
            newVisibleCount = 0;
            int accumulated = 0;
            var panels = GroupNode?.Panels;
            if (panels != null)
            {
                for (int i = _tabScrollIndex; i < panels.Count; i++)
                {
                    if (!_tabItems.TryGetValue(panels[i].Id, out var tab))
                        continue;
                    int w = Math.Max(tab.MinTabWidth, tab.LayoutBounds.Width);
                    if (accumulated + w > tabStripWidth && newVisibleCount > 0)
                        break;
                    accumulated += w;
                    newVisibleCount++;
                }
            }
            newVisibleCount = Math.Max(1, newVisibleCount);
            // Clamp scroll so the last valid window is used
            ClampScrollIndex(panelCount, newVisibleCount);
        }

        // Apply visibility to each tab item
        if (GroupNode?.Panels != null)
        {
            for (int i = 0; i < GroupNode.Panels.Count; i++)
            {
                if (_tabItems.TryGetValue(GroupNode.Panels[i].Id, out var tab))
                {
                    bool show = !newOverflowing || (i >= _tabScrollIndex && i < _tabScrollIndex + newVisibleCount);
                    tab.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }

        _isOverflowing    = newOverflowing;
        _visibleTabCount  = newVisibleCount;

        // ── Layout header row ─────────────────────────────────────────────
        int x = Bounds.X;

        // Scroll-left button
        if (_scrollLeftBtn != null)
        {
            _scrollLeftBtn.Visibility = newOverflowing ? Visibility.Visible : Visibility.Collapsed;
            if (newOverflowing)
            {
                _scrollLeftBtn.UpdateLayout(new Rectangle(x, Bounds.Y, ScrollBtnWidth, TabHeaderHeight));
                x += ScrollBtnWidth;
            }
        }

        // Tab strip
        _tabHeadersPanel.UpdateLayout(new Rectangle(x, Bounds.Y, tabStripWidth, TabHeaderHeight));
        x += tabStripWidth;

        // Scroll-right button
        if (_scrollRightBtn != null)
        {
            _scrollRightBtn.Visibility = newOverflowing ? Visibility.Visible : Visibility.Collapsed;
            if (newOverflowing)
            {
                _scrollRightBtn.UpdateLayout(new Rectangle(x, Bounds.Y, ScrollBtnWidth, TabHeaderHeight));
                x += ScrollBtnWidth;
            }
        }

        // Dropdown button (always visible, right-aligned)
        if (_dropdownBtn != null)
        {
            _dropdownBtn.UpdateLayout(new Rectangle(
                Bounds.Right - DropdownBtnWidth, Bounds.Y,
                DropdownBtnWidth, TabHeaderHeight));
        }

        TabHeadersBounds = new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, TabHeaderHeight);
        System.Diagnostics.Debug.WriteLine(
            $"[MGDockTabGroup] header={TabHeadersBounds}, overflow={_isOverflowing}, scrollIdx={_tabScrollIndex}");

        // ── Layout content area ───────────────────────────────────────────
        if (_activeContentContainer != null)
        {
            Rectangle contentBounds = new Rectangle(
                Bounds.X,
                Bounds.Y + TabHeaderHeight,
                Bounds.Width,
                Math.Max(0, Bounds.Height - TabHeaderHeight));
            _activeContentContainer.UpdateLayout(contentBounds);
        }
    }

    // ── Scroll / dropdown helpers ─────────────────────────────────────────

    /// <summary>Scroll the visible tab window one step to the left.</summary>
    public void ScrollLeft()
    {
        if (_tabScrollIndex > 0)
        {
            _tabScrollIndex--;
            InvalidateLayout();
        }
    }

    /// <summary>Scroll the visible tab window one step to the right.</summary>
    public void ScrollRight()
    {
        int panelCount = GroupNode?.Panels.Count ?? 0;
        int maxIndex   = Math.Max(0, panelCount - _visibleTabCount);
        if (_tabScrollIndex < maxIndex)
        {
            _tabScrollIndex++;
            InvalidateLayout();
        }
    }

    /// <summary>Open a dropdown listing all panels so the user can jump to any tab.</summary>
    public void ShowDropdown()
    {
        if (GroupNode == null || GroupNode.IsEmpty)
            return;

        // Build a context menu containing one button per panel
        var menu = new MGContextMenu(ParentWindow, "");

        foreach (var panel in GroupNode.Panels)
        {
            var capturedPanel = panel;
            var item = menu.AddButton(capturedPanel.Title, _ =>
            {
                GroupNode.SetActivePanel(capturedPanel.Id);
                // Ensure the tab is visible by scrolling to it
                EnsureTabVisible(capturedPanel.Id);
            });
        }

        // Open the menu anchored below the dropdown button
        if (_dropdownBtn != null)
        {
            var anchor = _dropdownBtn.LayoutBounds;
            ParentWindow.Desktop.TryOpenContextMenu(menu, new Rectangle(
                anchor.X, anchor.Bottom, anchor.Width, 1));
        }
    }

    /// <summary>
    /// Adjusts <see cref="_tabScrollIndex"/> so that the panel with the given id is in the visible range.
    /// </summary>
    public void EnsureTabVisible(string panelId)
    {
        if (GroupNode == null)
            return;

        int idx = GroupNode.Panels.IndexOf(GroupNode.Panels.FirstOrDefault(p => p.Id == panelId));
        if (idx < 0)
            return;

        if (idx < _tabScrollIndex)
        {
            _tabScrollIndex = idx;
            InvalidateLayout();
        }
        else if (idx >= _tabScrollIndex + Math.Max(1, _visibleTabCount))
        {
            _tabScrollIndex = Math.Max(0, idx - _visibleTabCount + 1);
            InvalidateLayout();
        }
    }

    private void ClampScrollIndex(int panelCount = -1, int visibleCount = -1)
    {
        if (panelCount < 0)  panelCount  = GroupNode?.Panels.Count ?? 0;
        if (visibleCount < 0) visibleCount = Math.Max(1, _visibleTabCount);
        int maxIndex = Math.Max(0, panelCount - visibleCount);
        _tabScrollIndex = Math.Clamp(_tabScrollIndex, 0, maxIndex);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        // No self rendering - children handle their own drawing
        DrawSelfBaseImplementation(DA, LayoutBounds);
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        // Draw all children
        foreach (var child in GetChildren())
        {
            child?.Draw(DA);
        }
    }
}
