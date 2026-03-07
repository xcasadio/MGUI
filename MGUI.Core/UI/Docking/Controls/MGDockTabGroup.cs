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
    private MGBorder _dropdownBtn;   // "..." overflow menu — visible only when tabs are hidden
    private MGBorder _maximizeBtn;   // maximize / restore toggle — always visible

    private const int DropdownBtnWidth = 24;
    private const int MaximizeBtnWidth = 24;

    private bool _isMaximized;
    /// <summary>
    /// When true the tab group is currently filling the whole docking host.
    /// The header shows a restore icon (⊡) instead of maximize (□).
    /// </summary>
    public bool IsMaximized
    {
        get => _isMaximized;
        set
        {
            if (_isMaximized != value)
            {
                _isMaximized = value;
                UpdateMaximizeButtonLabel();
                NPC(nameof(IsMaximized));
            }
        }
    }

    /// <summary>Fired when the user clicks the maximize button while the group is in normal state.</summary>
    public event EventHandler<DockTabGroupNode> MaximizeRequested;

    /// <summary>Fired when the user clicks the restore button while the group is maximized.</summary>
    public event EventHandler<DockTabGroupNode> RestoreRequested;

    private bool _isActiveGroup;
    /// <summary>
    /// When true this group contains the currently active (last-focused) panel.
    /// A thin accent stripe is drawn at the top of the tab bar to highlight the active group.
    /// Set by <see cref="MGDockHost"/> whenever <see cref="MGDockHost.ActiveDockable"/> changes.
    /// </summary>
    public bool IsActiveGroup
    {
        get => _isActiveGroup;
        set
        {
            if (_isActiveGroup != value)
            {
                _isActiveGroup = value;
                NPC(nameof(IsActiveGroup));
            }
        }
    }

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
    /// Reference back to the <see cref="MGDockHost"/> that owns this tab group.
    /// Non-null only when this tab group lives inside a <see cref="MGFloatingDockWindow"/>
    /// (where the window is not a visual ancestor of the host).
    /// For tab groups that are direct children of the host's visual tree this is null
    /// because <see cref="MGDockTabItem"/> can find the host via its ancestor chain.
    /// </summary>
    public MGDockHost OwnerDockHost
    {
        get => _ownerDockHost;
        set
        {
            _ownerDockHost = value;
            // Propagate to already-created tab items so OnDragStart can find the host.
            foreach (var ti in _tabItems.Values)
                ti.OwnerDockHost = value;
        }
    }
    private MGDockHost _ownerDockHost;

    /// <summary>
    /// Reference back to the <see cref="MGFloatingDockWindow"/> that contains this tab group,
    /// or null when the tab group is part of the docked (non-floating) layout.
    /// </summary>
    public MGFloatingDockWindow OwnerFloatingWindow
    {
        get => _ownerFloatingWindow;
        set
        {
            _ownerFloatingWindow = value;
            // Propagate to already-created tab items.
            foreach (var ti in _tabItems.Values)
                ti.OwnerFloatingWindow = value;
        }
    }
    private MGFloatingDockWindow _ownerFloatingWindow;

    /// <summary>
    /// Event raised when the active panel changes.
    /// </summary>
    public event EventHandler<DockPanelNode> ActivePanelChanged;

    /// <summary>
    /// Event raised when a panel close is requested.
    /// </summary>
    public event EventHandler<DockPanelNode> PanelCloseRequested;

    /// <summary>
    /// Event raised when the user requests to float (detach) a panel via the context menu.
    /// </summary>
    public event EventHandler<DockPanelNode> PanelFloatRequested;

    /// <summary>
    /// Event raised when the user clicks the pin button or selects Auto-Hide/Pin from a tab's context menu.
    /// </summary>
    public event EventHandler<DockPanelNode> PanelPinToggleRequested;

    /// <summary>
    /// Detaches this visual from its model node, unsubscribing all event handlers.
    /// Must be called before the visual is discarded (e.g. during RebuildVisualTree) to
    /// prevent the model from holding permanent references to orphaned tab group visuals.
    /// After calling Detach() this instance should not be used.
    /// </summary>
    public void Detach()
    {
        GroupNode = null; // setter removes CollectionChanged + PropertyChanged subscriptions
    }

    /// <summary>Updates the maximize / restore button label to match <see cref="IsMaximized"/>.</summary>
    private void UpdateMaximizeButtonLabel()
    {
        // Icon is drawn directly in DrawContents — no text label needed.
    }

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

            // ── Overflow dropdown (visible only when some tabs are hidden) ─────
            _dropdownBtn = CreateCompactButton(window, "", () => ShowDropdown());
            _dropdownBtn.Visibility = Visibility.Collapsed;
            _dropdownBtn.SetParent(this);

            // Maximize / restore toggle — icon is drawn directly in DrawContents
            _maximizeBtn = CreateCompactButton(window, "", () =>
            {
                if (_isMaximized)
                    RestoreRequested?.Invoke(this, GroupNode);
                else
                    MaximizeRequested?.Invoke(this, GroupNode);
            });
            _maximizeBtn.SetParent(this);
        }
    }

    /// <summary>
    /// Creates a small borderless button with a text glyph for the tab header strip.
    /// </summary>
    private static MGBorder CreateCompactButton(MGWindow window, string glyph, Action onClick)
    {
        var body = new MGBorder(window, new XAML.Thickness(0).ToThickness(), (IFillBrush)null)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch
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
                IsActive            = (panel.Id == GroupNode.ActivePanelId),
                OwnerDockHost       = OwnerDockHost,
                OwnerFloatingWindow = OwnerFloatingWindow
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

            // Subscribe to float request
            tabItem.FloatRequested += (sender, panelToFloat) =>
            {
                PanelFloatRequested?.Invoke(this, panelToFloat);
            };

            // Subscribe to pin/unpin toggle
            tabItem.PinToggleRequested += (sender, panelToToggle) =>
            {
                PanelPinToggleRequested?.Invoke(this, panelToToggle);
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
            UpdateActiveContent(); // triggers LayoutChanged -> layout pass enforces active-tab-visible
        }
    }

    public override IEnumerable<MGElement> GetChildren()
    {
        // header-strip elements (tab panel + control buttons)
        if (_tabHeadersPanel != null)
            yield return _tabHeadersPanel;
        if (_dropdownBtn != null)
            yield return _dropdownBtn;
        if (_maximizeBtn != null)
            yield return _maximizeBtn;

        if (_activeContentContainer != null)
            yield return _activeContentContainer;
    }

    protected override Thickness UpdateContentMeasurement(Size AvailableSize)
    {
        // ── Header row: tabs + optional scroll buttons + dropdown + maximize ──────
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
        // Always reserve room for the maximize button on the right edge.
        // The dropdown ("...") button is only shown when overflow occurs.
        int headerAvailableWidth = Bounds.Width - MaximizeBtnWidth;

        // Estimate total tabs width using the per-tab minimum (conservative).
        // On subsequent frames tab LayoutBounds are valid; use the larger value.
        int totalEstimated = 0;
        if (_tabItems.Count > 0)
        {
            foreach (var tab in _tabItems.Values)
            {
                // Use LastMeasuredWidth (desired width from the measurement pass) rather than
                // LayoutBounds.Width (allocated width).  LayoutBounds.Width is clamped by the
                // stack panel when it distributes space, so it under-reports the space actually
                // needed and can prevent overflow from ever being detected.
                totalEstimated += tab.LastMeasuredWidth;
            }
        }

        bool newOverflowing = totalEstimated > headerAvailableWidth;

        // Width available for the tab strip itself (shrink further if dropdown is shown)
        int tabStripWidth = headerAvailableWidth - (newOverflowing ? DropdownBtnWidth : 0);
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
                    int w = tab.LastMeasuredWidth;
                    if (accumulated + w > tabStripWidth && newVisibleCount > 0)
                        break;
                    accumulated += w;
                    newVisibleCount++;
                }
            }
            newVisibleCount = Math.Max(1, newVisibleCount);

            // ── INVARIANT: the active tab header is ALWAYS visible ────────────────────
            // Adjust _tabScrollIndex (using the fresh newVisibleCount, not the stale
            // _visibleTabCount) so that the active panel is within the visible window.
            int activeTabIndex = -1;
            if (GroupNode?.ActivePanelId != null && GroupNode.Panels != null)
            {
                for (int i = 0; i < GroupNode.Panels.Count; i++)
                {
                    if (GroupNode.Panels[i].Id == GroupNode.ActivePanelId)
                    {
                        activeTabIndex = i;
                        break;
                    }
                }
            }

            if (activeTabIndex >= 0)
            {
                if (activeTabIndex < _tabScrollIndex)
                    _tabScrollIndex = activeTabIndex;
                else if (activeTabIndex >= _tabScrollIndex + newVisibleCount)
                    _tabScrollIndex = activeTabIndex - newVisibleCount + 1;
            }

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
        // Order (left → right):  [tabs] [... dropdown (if overflow)] [maximize]
        int x = Bounds.X;

        // Tab strip
        _tabHeadersPanel.UpdateLayout(new Rectangle(x, Bounds.Y, tabStripWidth, TabHeaderHeight));
        x += tabStripWidth;

        // Dropdown button — only visible when overflow
        if (_dropdownBtn != null)
        {
            _dropdownBtn.Visibility = newOverflowing ? Visibility.Visible : Visibility.Collapsed;
            if (newOverflowing)
            {
                _dropdownBtn.UpdateLayout(new Rectangle(x, Bounds.Y, DropdownBtnWidth, TabHeaderHeight));
                x += DropdownBtnWidth;
            }
        }

        // Maximize button — always visible, rightmost
        if (_maximizeBtn != null)
        {
            _maximizeBtn.UpdateLayout(new Rectangle(
                Bounds.Right - MaximizeBtnWidth, Bounds.Y,
                MaximizeBtnWidth, TabHeaderHeight));
        }

        TabHeadersBounds = new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, TabHeaderHeight);

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
        menu.CanContextMenuOpen = true;

        foreach (var panel in GroupNode.Panels)
        {
            var capturedPanel = panel;
            menu.AddButton(capturedPanel.Title, _ =>
            {
                // SetActivePanel -> PropertyChanged -> UpdateActiveContent -> LayoutChanged
                // The next layout pass enforces the active-tab-visible invariant.
                GroupNode.SetActivePanel(capturedPanel.Id);
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

        // Draw active-group accent stripe (top edge of tab header area)
        if (IsActiveGroup)
            DrawActiveGroupAccent(DA);

        // Draw programmatic icons over their respective buttons
        DrawDropdownIcon(DA);
        DrawMaximizeIcon(DA);
    }

    /// <summary>
    /// Draws a 2-pixel accent stripe at the very top of the tab-header bar to indicate
    /// that this group is the currently active (last-focused) group.
    /// </summary>
    private void DrawActiveGroupAccent(ElementDrawArgs DA)
    {
        var lb = LayoutBounds;
        if (lb.Width <= 0 || lb.Height <= 0)
            return;

        const int stripeH = 2;
        DA.DT.FillRectangle(Vector2.Zero,
            new RectangleF(lb.X, lb.Y, lb.Width, stripeH),
            new Color(0, 120, 215));   // Windows accent blue
    }

    /// <summary>
    /// Draws three small dots ("...") centred over the dropdown button.
    /// Only drawn when the button is visible (overflow state).
    /// </summary>
    private void DrawDropdownIcon(ElementDrawArgs DA)
    {
        if (_dropdownBtn == null || _dropdownBtn.Visibility != Visibility.Visible)
            return;

        Rectangle b   = _dropdownBtn.LayoutBounds;
        float     cx  = b.X + b.Width * 0.5f;
        float     cy  = b.Y + b.Height * 0.5f;
        Color     col = new Color(200, 200, 200);

        const float dotRadius = 1.5f;
        const float spacing   = 5f;
        for (int i = -1; i <= 1; i++)
        {
            float dx = cx + i * spacing;
            DA.DT.FillRectangle(Vector2.Zero,
                new RectangleF(dx - dotRadius, cy - dotRadius, dotRadius * 2f, dotRadius * 2f),
                col);
        }
    }

    /// <summary>
    /// Draws the maximize or restore icon centred over <see cref="_maximizeBtn"/>'s layout bounds.
    /// Uses the <c>DockMaximize</c> / <c>DockMinimize</c> texture resources when available;
    /// falls back to programmatic shapes otherwise.
    /// </summary>
    private void DrawMaximizeIcon(ElementDrawArgs DA)
    {
        if (_maximizeBtn == null)
            return;

        Rectangle b        = _maximizeBtn.LayoutBounds;
        const int iconSize = 14;
        Rectangle iconRect = new Rectangle(
            b.X + (b.Width  - iconSize) / 2,
            b.Y + (b.Height - iconSize) / 2,
            iconSize, iconSize);
        Color col      = new Color(200, 200, 200);
        string iconKey = _isMaximized ? "DockMinimize" : "DockMaximize";

        if (!GetResources().TryDrawTexture(DA.DT, iconKey, iconRect, 1f, col))
        {
            // Fallback: programmatic shape
            float cx = b.X + b.Width  * 0.5f;
            float cy = b.Y + b.Height * 0.5f;
            if (_isMaximized)
            {
                // Restore: short horizontal dash  ─
                const float halfW = 5f;
                DA.DT.StrokeLineSegment(Vector2.Zero,
                    new Vector2(cx - halfW, cy),
                    new Vector2(cx + halfW, cy),
                    col, 1.5f);
            }
            else
            {
                // Maximize: hollow square  □
                const float halfS = 5f;
                DA.DT.StrokeRectangle(Vector2.Zero,
                    new RectangleF(cx - halfS, cy - halfS, halfS * 2f, halfS * 2f),
                    col,
                    new Thickness(1),
                    null);
            }
        }
    }
}
