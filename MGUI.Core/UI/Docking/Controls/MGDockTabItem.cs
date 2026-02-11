using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI.Docking.Controls
{
    /// <summary>
    /// Represents a single tab item in a dock tab group.
    /// Displays the panel title and provides click/close functionality.
    /// </summary>
    public class MGDockTabItem : MGElement
    {
        private DockPanelNode _panel;
        /// <summary>
        /// The dock panel node this tab represents.
        /// </summary>
        public DockPanelNode Panel
        {
            get => _panel;
            set
            {
                if (_panel != value)
                {
                    _panel = value;
                    UpdateVisuals();
                    NPC(nameof(Panel));
                }
            }
        }

        private bool _isActive;
        /// <summary>
        /// Whether this tab is currently active (selected).
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    UpdateVisuals();
                    NPC(nameof(IsActive));
                }
            }
        }

        private IFillBrush _normalBrush;
        /// <summary>
        /// Background brush when tab is not active.
        /// </summary>
        public IFillBrush NormalBrush
        {
            get => _normalBrush;
            set
            {
                if (_normalBrush != value)
                {
                    _normalBrush = value;
                    NPC(nameof(NormalBrush));
                }
            }
        }

        private IFillBrush _hoverBrush;
        /// <summary>
        /// Background brush when tab is hovered.
        /// </summary>
        public IFillBrush HoverBrush
        {
            get => _hoverBrush;
            set
            {
                if (_hoverBrush != value)
                {
                    _hoverBrush = value;
                    NPC(nameof(HoverBrush));
                }
            }
        }

        private IFillBrush _activeBrush;
        /// <summary>
        /// Background brush when tab is active.
        /// </summary>
        public IFillBrush ActiveBrush
        {
            get => _activeBrush;
            set
            {
                if (_activeBrush != value)
                {
                    _activeBrush = value;
                    NPC(nameof(ActiveBrush));
                }
            }
        }

        private MGTextBlock _titleText;
        private MGButton _closeButton;

        private int _tabHeight = 30;
        /// <summary>
        /// Height of the tab in pixels.
        /// </summary>
        public int TabHeight
        {
            get => _tabHeight;
            set
            {
                if (_tabHeight != value)
                {
                    _tabHeight = value;
                    LayoutChanged(this, true);
                    NPC(nameof(TabHeight));
                }
            }
        }

        private int _minTabWidth = 80;
        /// <summary>
        /// Minimum width of the tab in pixels.
        /// </summary>
        public int MinTabWidth
        {
            get => _minTabWidth;
            set
            {
                if (_minTabWidth != value)
                {
                    _minTabWidth = value;
                    LayoutChanged(this, true);
                    NPC(nameof(MinTabWidth));
                }
            }
        }

        /// <summary>
        /// Event raised when the tab is clicked.
        /// </summary>
        public event EventHandler<DockPanelNode> TabClicked;

        /// <summary>
        /// Event raised when the close button is clicked.
        /// </summary>
        public event EventHandler<DockPanelNode> CloseRequested;

        /// <summary>
        /// Creates a new MGDockTabItem.
        /// </summary>
        /// <param name="window">The parent window.</param>
        /// <param name="panel">The panel node this tab represents.</param>
        public MGDockTabItem(MGWindow window, DockPanelNode panel) : base(window, MGElementType.Custom)
        {
            using (BeginInitializing())
            {
                _panel = panel;

                // Set default brushes
                NormalBrush = new MGSolidFillBrush(new Color(45, 45, 48));
                HoverBrush = new MGSolidFillBrush(new Color(62, 62, 66));
                ActiveBrush = new MGSolidFillBrush(new Color(0, 122, 204));

                // Create title text
                _titleText = new MGTextBlock(window, panel?.Title ?? "Tab")
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new XAML.Thickness(8, 4, 4, 4).ToThickness()
                };
                _titleText.SetParent(this);

                // Create close button
                _closeButton = new MGButton(window, btn =>
                {
                    CloseRequested?.Invoke(this, Panel);
                })
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Padding = new XAML.Thickness(4, 2, 4, 2).ToThickness(),
                    MinWidth = 20,
                    MinHeight = 20
                };
                _closeButton.SetContent(new MGTextBlock(window, "×") 
                { 
                    FontSize = 16,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                });
                _closeButton.SetParent(this);

                // Subscribe to mouse click
                MouseHandler.LMBReleasedInside += (sender, e) =>
                {
                    if (!e.IsHandled)
                    {
                        TabClicked?.Invoke(this, Panel);
                    }
                };

                // Subscribe to drag start
                MouseHandler.DragStart += OnDragStart;

                HorizontalAlignment = HorizontalAlignment.Left;
                VerticalAlignment = VerticalAlignment.Top;
            }
        }

        /// <summary>
        /// Updates the visual appearance based on panel data and active state.
        /// </summary>
        private void UpdateVisuals()
        {
            if (_titleText != null && Panel != null)
            {
                _titleText.SetText(Panel.Title);
            }
        }

        /// <summary>
        /// Handles the start of a drag operation on this tab item.
        /// </summary>
        private void OnDragStart(object sender, BaseMouseDragStartEventArgs e)
        {
            // Only handle left mouse button drag
            if (!e.IsLMB)
                return;

            // Find parent MGDockHost
            var dockHost = FindAncestor<MGDockHost>();
            if (dockHost == null)
                return;

            // Find parent MGDockTabGroup
            var tabGroup = FindAncestor<MGDockTabGroup>();
            if (tabGroup == null)
                return;

            // Begin drag operation
            dockHost.BeginDrag(Panel, tabGroup.GroupNode, e.Position, this);

            // Mark event as handled to prevent default behavior
            e.SetHandledBy(this, false);
        }

        /// <summary>
        /// Finds the first ancestor element of the specified type.
        /// </summary>
        private T FindAncestor<T>() where T : MGElement
        {
            var current = this.Parent;
            while (current != null)
            {
                if (current is T result)
                    return result;
                current = current.Parent;
            }
            return null;
        }

        public override System.Collections.Generic.IEnumerable<MGElement> GetChildren()
        {
            if (_titleText != null)
                yield return _titleText;
            if (_closeButton != null && Panel?.CanClose == true)
                yield return _closeButton;
        }

        public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
        {
            SharedSize = new Thickness(0);
            
            // MGDockTabItem has no padding/borders/margins of its own
            // The content (title + close button) is measured in UpdateContentMeasurement()
            return new Thickness(0);
        }

        protected override Thickness UpdateContentMeasurement(Size AvailableSize)
        {
            // Content size is the area needed for title + close button
            int titleWidth = 0;
            if (_titleText != null)
            {
                _titleText.UpdateMeasurement(AvailableSize, out _, out Thickness titleFullSize, out _, out _);
                titleWidth = titleFullSize.Width;
            }

            int closeWidth = 0;
            if (Panel?.CanClose == true && _closeButton != null)
            {
                _closeButton.UpdateMeasurement(AvailableSize, out _, out Thickness closeFullSize, out _, out _);
                closeWidth = closeFullSize.Width;
            }

            int totalWidth = Math.Max(MinTabWidth, titleWidth + closeWidth + 8);
            return new Thickness(totalWidth, TabHeight, 0, 0);
        }

        protected override void UpdateContentLayout(Rectangle Bounds)
        {
            if (_titleText == null)
                return;

            // Layout title text (takes most of the space)
            int closeWidth = (Panel?.CanClose == true && _closeButton != null) ? 20 : 0;
            Rectangle titleBounds = new Rectangle(
                Bounds.X,
                Bounds.Y,
                Bounds.Width - closeWidth,
                Bounds.Height
            );
            _titleText.UpdateLayout(titleBounds);

            // Layout close button (right side)
            if (Panel?.CanClose == true && _closeButton != null)
            {
                Rectangle closeBounds = new Rectangle(
                    Bounds.Right - closeWidth,
                    Bounds.Y,
                    closeWidth,
                    Bounds.Height
                );
                _closeButton.UpdateLayout(closeBounds);
            }
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            // Choose background brush based on state
            IFillBrush backgroundBrush;
            if (IsActive)
                backgroundBrush = ActiveBrush;
            else if (IsHovered)
                backgroundBrush = HoverBrush;
            else
                backgroundBrush = NormalBrush;

            // Draw background
            backgroundBrush?.Draw(DA, this, LayoutBounds);

            DrawSelfBaseImplementation(DA, LayoutBounds);
        }

        protected override void DrawContents(ElementDrawArgs DA)
        {
            // Draw all children (title text and close button)
            foreach (var child in GetChildren())
            {
                child?.Draw(DA);
            }
        }
    }
}
