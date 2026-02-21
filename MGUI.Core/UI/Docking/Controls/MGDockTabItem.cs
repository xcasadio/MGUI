using System;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI.Docking.Controls;

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
    private MGBorder _closeButton;
    private MGTextBlock _closeButtonText;

    /// <summary>Fixed pixel width reserved for the close button (icon area + padding).</summary>
    private const int CloseButtonSize = 22;

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
    /// Event raised when the user selects "Close Others" from the context menu.
    /// </summary>
    public event EventHandler<DockPanelNode> CloseOthersRequested;

    /// <summary>
    /// Event raised when the user selects "Close All" from the context menu.
    /// </summary>
    public event EventHandler<DockPanelNode> CloseAllRequested;

    /// <summary>
    /// Fallback reference to the owning <see cref="MGDockHost"/> used when this tab item lives
    /// inside a <see cref="MGFloatingDockWindow"/> and the host is not reachable via the element
    /// ancestor chain.  Set by <see cref="MGDockTabGroup"/> when it creates this item so that
    /// drag operations initiated from a floating window are still forwarded to the correct host.
    /// </summary>
    public MGDockHost OwnerDockHost { get; set; }

    /// <summary>
    /// Reference to the <see cref="MGFloatingDockWindow"/> that contains this tab item,
    /// or null when the tab group is part of the docked (non-floating) layout.
    /// Set by <see cref="MGDockTabGroup"/> together with <see cref="OwnerDockHost"/>.
    /// </summary>
    public MGFloatingDockWindow OwnerFloatingWindow { get; set; }

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

            // Set default brushes with better visual distinction
            NormalBrush = new MGSolidFillBrush(new Color(45, 45, 48));      // Dark gray (inactive)
            HoverBrush = new MGSolidFillBrush(new Color(62, 62, 66));       // Lighter gray (hover)
            ActiveBrush = new MGSolidFillBrush(new Color(37, 37, 38));      // Slightly darker but will have bright accent line

            // Create title text — single-line only; the tab width adapts to its content
            _titleText = new MGTextBlock(window, panel?.Title ?? "Tab")
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                WrapText = false,
                Padding = new XAML.Thickness(8, 4, 4, 4).ToThickness()
            };
            _titleText.SetParent(this);

            // Create simple close button element — Stretch fills the full reserved area so
            // that the drawn X cross is centred correctly and the hit-test rect is correct.
            _closeButton = new MGBorder(window, new XAML.Thickness(0).ToThickness(), (IFillBrush)null)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch
            };
                
            _closeButtonText = new MGTextBlock(window, "")
            {
                FontSize = 14,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                // Empty text — the X cross is drawn directly in DrawContents
                Padding = new XAML.Thickness(4, 2, 4, 2).ToThickness()
            };
            _closeButton.SetContent(_closeButtonText);
                
            // Handle close button click
            _closeButton.MouseHandler.LMBReleasedInside += (sender, e) =>
            {
                if (!e.IsHandled)
                {
                    CloseRequested?.Invoke(this, Panel);
                    e.SetHandledBy(_closeButton, false);
                }
            };
                
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

            // ── Context menu (right-click) ─────────────────────────────────
            BuildContextMenu(window);

            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
        }
    }

    /// <summary>
    /// Creates and assigns the right-click context menu for this tab.
    /// Called once from the constructor; the menu is rebuilt lazily on open
    /// so it always reflects the current CanClose / CanFloat state.
    /// </summary>
    private void BuildContextMenu(MGWindow window)
    {
        var menu = new MGContextMenu(window, "");

        menu.CanContextMenuOpen = true;

        // We rebuild items every time the menu opens so they are always fresh.
        menu.ContextMenuOpening += (_, __) =>
        {
            // Clear existing items and rebuild
            menu.Items.Clear();

            // Close — only shown if the panel allows it
            if (Panel?.CanClose == true)
            {
                menu.AddButton("Close", _ => CloseRequested?.Invoke(this, Panel));
                menu.AddSeparator();
            }

            menu.AddButton("Close Others", _ => CloseOthersRequested?.Invoke(this, Panel));
            menu.AddButton("Close All",    _ => CloseAllRequested?.Invoke(this, Panel));
        };

        ContextMenu = menu;
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

        // Update text color based on active state for better readability
        if (_titleText != null)
        {
            // Active tabs get brighter text
            _titleText.DefaultTextForeground.NormalValue = IsActive
                ? Color.White               // Bright white for active
                : new Color(200, 200, 200); // Slightly dimmed for inactive
        }

        // Update close button text color
        if (_closeButtonText != null)
        {
            _closeButtonText.DefaultTextForeground.NormalValue = IsActive
                ? Color.White
                : new Color(180, 180, 180);
        }
    }

    /// <summary>
    /// Handles the start of a drag operation on this tab item.
    /// </summary>
    private void OnDragStart(object sender, BaseMouseDragStartEventArgs e)
    {
        // Only handle left mouse button drag
        if (!e.IsLMB)
        {
            return;
        }

        // Find parent MGDockTabGroup
        var tabGroup = FindAncestor<MGDockTabGroup>();
        if (tabGroup == null)
        {
            return;
        }

        // Find parent MGDockHost — either as a real ancestor (normal case) or
        // via OwnerDockHost set by the containing MGFloatingDockWindow (floating case).
        var dockHost = FindAncestor<MGDockHost>() ?? OwnerDockHost;
        if (dockHost == null)
        {
            return;
        }

        // Begin drag operation, passing the source floating window if applicable
        dockHost.BeginDrag(Panel, tabGroup.GroupNode, e.Position, this, OwnerFloatingWindow);

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
            {
                return result;
            }

            current = current.Parent;
        }
        return null;
    }

    public override System.Collections.Generic.IEnumerable<MGElement> GetChildren()
    {
        if (_titleText != null)
        {
            yield return _titleText;
        }

        if (_closeButton != null && Panel?.CanClose == true)
        {
            yield return _closeButton;
        }
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
        // Measure title text with unlimited width so it reports its natural single-line size.
        int titleWidth = 0;
        if (_titleText != null)
        {
            var unlimitedSize = new Size(int.MaxValue / 2, AvailableSize.Height);
            _titleText.UpdateMeasurement(unlimitedSize, out _, out Thickness titleFullSize, out _, out _);
            titleWidth = titleFullSize.Width;
        }

        // Close button occupies a fixed reserved area — no need to measure the child element.
        int closeWidth = (Panel?.CanClose == true) ? CloseButtonSize : 0;

        int totalWidth = Math.Max(MinTabWidth, titleWidth + closeWidth);
        return new Thickness(totalWidth, TabHeight, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        if (_titleText == null)
        {
            return;
        }

        // Layout title text — occupies everything left of the close button
        int closeWidth = (Panel?.CanClose == true && _closeButton != null) ? CloseButtonSize : 0;

        Rectangle titleBounds = new Rectangle(
            Bounds.X,
            Bounds.Y,
            Bounds.Width - closeWidth,
            Bounds.Height
        );
        _titleText.UpdateLayout(titleBounds);

        // Layout close button — flush to the right, vertically centred, exactly CloseButtonSize wide
        if (Panel?.CanClose == true && _closeButton != null)
        {
            Rectangle closeBounds = new Rectangle(
                Bounds.Right - CloseButtonSize,
                Bounds.Y,
                CloseButtonSize,
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
        {
            backgroundBrush = ActiveBrush;
        }
        else if (IsHovered)
        {
            backgroundBrush = HoverBrush;
        }
        else
        {
            backgroundBrush = NormalBrush;
        }

        // Draw background
        backgroundBrush?.Draw(DA, this, LayoutBounds);

        // Draw visual accent for active tab
        if (IsActive)
        {
            // Draw a bright accent line at the bottom of active tab
            const int accentHeight = 3;
            Rectangle accentBounds = new Rectangle(
                LayoutBounds.X,
                LayoutBounds.Bottom - accentHeight,
                LayoutBounds.Width,
                accentHeight
            );
                
            Color accentColor = new Color(0, 180, 255); // Bright blue accent
            DA.DT.FillRectangle(Vector2.Zero, 
                new RectangleF(accentBounds.X, accentBounds.Y, accentBounds.Width, accentBounds.Height),
                accentColor);
        }
        // Draw subtle hover indicator for inactive tabs
        else if (IsHovered)
        {
            // Draw a thin line at the bottom when hovered (but not active)
            const int hoverLineHeight = 2;
            Rectangle hoverBounds = new Rectangle(
                LayoutBounds.X,
                LayoutBounds.Bottom - hoverLineHeight,
                LayoutBounds.Width,
                hoverLineHeight
            );
                
            Color hoverLineColor = new Color(100, 150, 200, 180); // Semi-transparent blue
            DA.DT.FillRectangle(Vector2.Zero,
                new RectangleF(hoverBounds.X, hoverBounds.Y, hoverBounds.Width, hoverBounds.Height),
                hoverLineColor);
        }

        DrawSelfBaseImplementation(DA, LayoutBounds);
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        // Draw all children (title text and close button background)
        foreach (var child in GetChildren())
        {
            child?.Draw(DA);
        }

        // Draw X cross icon on the close button (drawn directly, not via font glyph)
        if (Panel?.CanClose == true && _closeButton != null)
        {
            Rectangle cb = _closeButton.LayoutBounds;
            float cx = cb.X + cb.Width * 0.5f;
            float cy = cb.Y + cb.Height * 0.5f;
            const float half = 4.5f;
            Color crossColor = IsActive ? Color.White : new Color(180, 180, 180);

            DA.DT.StrokeLineSegment(Vector2.Zero,
                new Vector2(cx - half, cy - half), new Vector2(cx + half, cy + half),
                crossColor, 1.5f);
            DA.DT.StrokeLineSegment(Vector2.Zero,
                new Vector2(cx + half, cy - half), new Vector2(cx - half, cy + half),
                crossColor, 1.5f);
        }
    }
}
