using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking.Controls
{
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
            }
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
                return;

            // Create tab item for each panel
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

                _tabHeadersPanel.TryAddChild(tabItem);
                _tabItems[panel.Id] = tabItem;
            }
            
            // Force layout update for tab headers panel
            _tabHeadersPanel.InvalidateLayout();
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
                return;

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
            }
        }

        public override IEnumerable<MGElement> GetChildren()
        {
            if (_tabHeadersPanel != null)
                yield return _tabHeadersPanel;
            if (_activeContentContainer != null)
                yield return _activeContentContainer;
        }

        protected override Thickness UpdateContentMeasurement(Size AvailableSize)
        {
            
            Thickness headerSize = new Thickness(0);
            Thickness contentSize = new Thickness(0);

            // Measure tab headers
            if (_tabHeadersPanel != null)
            {
                Size headerAvailableSize = new Size(AvailableSize.Width, TabHeaderHeight);
                _tabHeadersPanel.UpdateMeasurement(headerAvailableSize, out _, out headerSize, out _, out _);
            }

            // Measure active content
            if (_activeContentContainer != null)
            {
                Size contentAvailableSize = new Size(AvailableSize.Width, Math.Max(0, AvailableSize.Height - TabHeaderHeight));
                _activeContentContainer.UpdateMeasurement(contentAvailableSize, out _, out contentSize, out _, out _);
            }

            // Total size: headers on top, content below
            int maxWidth = Math.Max(headerSize.Width, contentSize.Width);
            int totalHeight = headerSize.Height + contentSize.Height;
            
            return new Thickness(maxWidth, totalHeight, 0, 0);
        }

        protected override void UpdateContentLayout(Rectangle Bounds)
        {
            
            if (_tabHeadersPanel == null)
                return;

            // Layout tab headers at the top
            Rectangle headerBounds = new Rectangle(
                Bounds.X,
                Bounds.Y,
                Bounds.Width,
                TabHeaderHeight
            );
            System.Diagnostics.Debug.WriteLine($"[MGDockTabGroup] Calling _tabHeadersPanel.UpdateLayout with bounds: {headerBounds}");
            _tabHeadersPanel.UpdateLayout(headerBounds);

            // Layout active content below headers
            if (_activeContentContainer != null)
            {
                Rectangle contentBounds = new Rectangle(
                    Bounds.X,
                    Bounds.Y + TabHeaderHeight,
                    Bounds.Width,
                    Math.Max(0, Bounds.Height - TabHeaderHeight)
                );
                System.Diagnostics.Debug.WriteLine($"[MGDockTabGroup] Content bounds: {contentBounds}, ActiveContent type: {_activeContentContainer?.GetType().Name}");
                _activeContentContainer.UpdateLayout(contentBounds);
            }
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
}
