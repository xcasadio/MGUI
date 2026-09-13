using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Represents a single tab item in a dock tab group.
/// Displays the panel title and provides click/close functionality.
/// </summary>
public class MGDockTabItem : MGElement
{
    public const string SurfacePartName = "PART_Surface";
    public const string AccentPartName = "PART_Accent";
    public const string CloseIconPartName = "PART_CloseIcon";
    public const string PinIconPartName = "PART_PinIcon";
    public const string TitleTextPartName = "PART_TitleText";
    public const string CloseButtonPartName = "PART_CloseButton";
    public const string PinButtonPartName = "PART_PinButton";

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

    public Color ActiveAccentColor { get; set; } = new Color(0, 180, 255);
    public Color HoverAccentColor { get; set; } = new Color(100, 150, 200, 180);
    public Color ActiveTextColor { get; set; } = Color.White;
    public Color InactiveTextColor { get; set; } = new Color(200, 200, 200);
    public Color ActiveIconColor { get; set; } = Color.White;
    public Color InactiveIconColor { get; set; } = new Color(180, 180, 180);

    private MGBorder _surfaceElement;
    private MGComponent<MGBorder> _surfaceComponent;
    private MGRectangle _accentElement;
    private MGComponent<MGRectangle> _accentComponent;
    private MGTextBlock _titleText;
    private MGBorder _closeButton;
    private MGCloseIcon _closeIconElement;
    private MGComponent<MGCloseIcon> _closeIconComponent;
    private MGBorder _pinButton;
    private MGDockPinIcon _pinIconElement;
    private MGComponent<MGDockPinIcon> _pinIconComponent;

    private int _buttonSize = 22;
    /// <summary>Pixel width reserved for each of the close and pin buttons (icon area + padding). Default: the theme's
    /// <see cref="MGThemeDockingSettings.TabButtonSize"/>, applied by the <c>Dock.TabItem.Default</c> template (backlog task 14).</summary>
    public int ButtonSize
    {
        get => _buttonSize;
        set
        {
            if (_buttonSize != value)
            {
                _buttonSize = value;
                LayoutChanged(this, true);
                NPC(nameof(ButtonSize));
            }
        }
    }

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
    /// The desired (natural) width of this tab as computed during the last measurement pass.
    /// Set by <see cref="UpdateContentMeasurement"/> and used by the parent
    /// <see cref="MGDockTabGroup"/> for overflow detection.
    /// Defaults to <see cref="MinTabWidth"/> until the first measurement pass.
    /// </summary>
    internal int LastMeasuredWidth => _lastMeasuredWidth;
    private int _lastMeasuredWidth = 80; // matches default MinTabWidth

    /// <summary>
    /// Event raised when the tab is clicked.
    /// </summary>
    public event EventHandler<DockPanelNode> TabClicked;

    /// <summary>
    /// Event raised when the close button is clicked.
    /// </summary>
    public event EventHandler<DockPanelNode> CloseRequested;

    /// <summary>
    /// Event raised when the user selects "Float" from the context menu.
    /// </summary>
    public event EventHandler<DockPanelNode> FloatRequested;

    /// <summary>
    /// Event raised when the user selects "Dock" from the context menu of a tab that lives inside
    /// a <see cref="MGFloatingDockWindow"/> (see <see cref="OwnerFloatingWindow"/>). The host should
    /// re-dock the panel back into the docked layout.
    /// </summary>
    public event EventHandler<DockPanelNode> DockRequested;

    /// <summary>
    /// Event raised when the user selects "Close Others" from the context menu.
    /// </summary>
    public event EventHandler<DockPanelNode> CloseOthersRequested;

    /// <summary>
    /// Event raised when the user selects "Close All" from the context menu.
    /// </summary>
    public event EventHandler<DockPanelNode> CloseAllRequested;

    /// <summary>
    /// Event raised when the user clicks the pin button or selects "Auto-Hide" / "Pin" from the
    /// context menu.  The host should toggle <see cref="DockPanelNode.IsPinned"/> accordingly.
    /// </summary>
    public event EventHandler<DockPanelNode> PinToggleRequested;

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
            DefaultControlTemplateName = MGControlTemplateCatalog.DockTabItemTemplateName;



            // The seven parts come from the control template, see AttachControlTemplateStructure.
                

            // Subscribe to mouse click
            // NOTE: pin button is not hit-testable, so its area routes here too.
            MouseHandler.LMBReleasedInside += (sender, e) =>
            {
                if (!e.IsHandled)
                {
                    // Check if the release was within the pin button bounds (transparent to hit-test).
                    // Gated on reveal: a hidden pin (not hovered/active) must behave like a plain tab click.
                    if (IsPinAccessoryRevealed && _pinButton != null
                        && _pinButton.LayoutBounds.Width > 0
                        && _pinButton.LayoutBounds.Contains(e.Position))
                    {
                        PinToggleRequested?.Invoke(this, Panel);
                        e.SetHandledBy(this, false);
                        return;
                    }
                    TabClicked?.Invoke(this, Panel);
                }
            };


            // Subscribe to drag start
            MouseHandler.DragStart += OnDragStart;

            // ── Context menu (right-click) ─────────────────────────────────
            BuildContextMenu(window);

            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            UpdateVisuals();
        }
    }

    protected internal override System.Collections.Generic.IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(SurfacePartName, typeof(MGBorder));
        yield return new(AccentPartName, typeof(MGRectangle));
        yield return new(TitleTextPartName, typeof(MGTextBlock));
        yield return new(CloseButtonPartName, typeof(MGBorder));
        yield return new(CloseIconPartName, typeof(MGCloseIcon));
        yield return new(PinButtonPartName, typeof(MGBorder));
        yield return new(PinIconPartName, typeof(MGDockPinIcon));
    }

    /// <summary>Binds the seven parts created by the control template (<c>Dock.TabItem.Default</c>). The surface, the accent and the two icons
    /// become components; the title and the two accessory buttons become children laid out by <see cref="UpdateContentLayout"/>. The close click,
    /// the hit-test transparency of the title and of the pin button, and every state-driven visual (<see cref="UpdateVisuals"/>) stay on this tab.
    /// A structure replaced by another template releases the components, children and close handler of the parts it replaces.</summary>
    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
    {
        MGTextBlock titleText = (MGTextBlock)Structure.Parts[TitleTextPartName];
        MGBorder closeButton = (MGBorder)Structure.Parts[CloseButtonPartName];
        MGBorder pinButton = (MGBorder)Structure.Parts[PinButtonPartName];

        if (_titleText != null && !ReferenceEquals(_titleText, titleText))
        {
            _titleText.SetParent(null);
        }

        if (_pinButton != null && !ReferenceEquals(_pinButton, pinButton))
        {
            _pinButton.SetParent(null);
        }

        if (!ReferenceEquals(_closeButton, closeButton))
        {
            if (_closeButton != null)
            {
                _closeButton.MouseHandler.LMBReleasedInside -= OnCloseButtonReleased;
                _closeButton.SetParent(null);
            }

            closeButton.MouseHandler.LMBReleasedInside += OnCloseButtonReleased;
        }

        _surfaceElement = (MGBorder)Structure.Parts[SurfacePartName];
        _accentElement = (MGRectangle)Structure.Parts[AccentPartName];
        _closeIconElement = (MGCloseIcon)Structure.Parts[CloseIconPartName];
        _pinIconElement = (MGDockPinIcon)Structure.Parts[PinIconPartName];
        _titleText = titleText;
        _closeButton = closeButton;
        _pinButton = pinButton;

        _surfaceElement.ManagedParent = this;
        _surfaceElement.IsHitTestVisible = false;
        _accentElement.ManagedParent = this;
        _accentElement.IsHitTestVisible = false;
        _closeIconElement.ManagedParent = this;
        _pinIconElement.ManagedParent = this;

        EnsureComponentBinding(() => _surfaceComponent, value => _surfaceComponent = value, _surfaceElement,
            element => new(element, false, false, false, false, false, false, false, (availableBounds, componentSize) => LayoutBounds));
        EnsureComponentBinding(() => _accentComponent, value => _accentComponent = value, _accentElement,
            element => new(element, false, false, false, false, false, false, false, (availableBounds, componentSize) => GetAccentBounds()));
        EnsureComponentBinding(() => _closeIconComponent, value => _closeIconComponent = value, _closeIconElement,
            element => new(element, ComponentUpdatePriority.AfterContents, ComponentDrawPriority.AfterContents,
                false, false, false, false, false, false, false, (availableBounds, componentSize) => GetCloseIconBounds()));
        EnsureComponentBinding(() => _pinIconComponent, value => _pinIconComponent = value, _pinIconElement,
            element => new(element, ComponentUpdatePriority.AfterContents, ComponentDrawPriority.AfterContents,
                false, false, false, false, false, false, false, (availableBounds, componentSize) => GetPinIconBounds()));

        _titleText.IsHitTestVisible = false;
        _titleText.SetParent(this);
        // The pin button is purely visual: its mouse events pass through to this tab item (see the constructor).
        _pinButton.IsHitTestVisible = false;
        _pinButton.SetParent(this);
        _closeButton.SetParent(this);

        UpdateVisuals();
    }

    /// <summary>Gated on reveal, so that a hidden (not hovered, not active) close button never closes the panel, even if a release event
    /// somehow still reaches it.</summary>
    private void OnCloseButtonReleased(object sender, BaseMouseReleasedEventArgs e)
    {
        if (!e.IsHandled && IsCloseAccessoryRevealed)
        {
            CloseRequested?.Invoke(this, Panel);
            e.SetHandledBy(_closeButton, false);
        }
    }

    /// <summary>
    /// Whether the close accessory (button + icon) should currently be shown: the panel allows
    /// closing, and this tab is active or hovered. IDE-style reveal-on-hover/active — the reserved
    /// layout space is unaffected (see <see cref="GetCloseWidth"/>), only visibility/interactivity change.
    /// </summary>
    private bool IsCloseAccessoryRevealed => Panel?.CanClose == true && (IsActive || IsHovered);

    /// <summary>
    /// Whether the pin accessory (button + icon) should currently be shown: the panel allows
    /// auto-hide, and this tab is active or hovered. See <see cref="IsCloseAccessoryRevealed"/>.
    /// </summary>
    private bool IsPinAccessoryRevealed => Panel?.CanAutoHide == true && (IsActive || IsHovered);

    private Rectangle GetAccentBounds()
    {
        int accentHeight = IsActive ? 3 : 2;
        return new Rectangle(LayoutBounds.X, LayoutBounds.Bottom - accentHeight, LayoutBounds.Width, accentHeight);
    }

    /// <summary>Reserved width for the close button, or 0 when it is not shown for this panel.</summary>
    private int GetCloseWidth() => (Panel?.CanClose == true && _closeButton != null) ? ButtonSize : 0;

    /// <summary>Reserved width for the pin button, or 0 when it is not shown for this panel.</summary>
    private int GetPinWidth() => (Panel?.CanAutoHide == true && _pinButton != null) ? ButtonSize : 0;

    /// <summary>
    /// Computes the close button's rectangle from this tab item's OWN <see cref="MGElement.LayoutBounds"/>,
    /// using the same flush-right arithmetic as <see cref="UpdateContentLayout"/>. Must not read
    /// <c>_closeButton.LayoutBounds</c>: components are arranged before <see cref="UpdateContentLayout"/>
    /// positions the buttons, so that would read the previous pass's bounds.
    /// </summary>
    private Rectangle GetCloseButtonBounds()
    {
        int closeWidth = GetCloseWidth();
        if (closeWidth <= 0)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(LayoutBounds.Right - closeWidth, LayoutBounds.Y, ButtonSize, LayoutBounds.Height);
    }

    /// <summary>
    /// Computes the pin button's rectangle from this tab item's OWN <see cref="MGElement.LayoutBounds"/>,
    /// using the same flush-right arithmetic as <see cref="UpdateContentLayout"/>. See remarks on
    /// <see cref="GetCloseButtonBounds"/> for why sibling <c>LayoutBounds</c> must not be used.
    /// </summary>
    private Rectangle GetPinButtonBounds()
    {
        int pinWidth = GetPinWidth();
        if (pinWidth <= 0)
        {
            return Rectangle.Empty;
        }

        int buttonsWidth = pinWidth + GetCloseWidth();
        return new Rectangle(LayoutBounds.Right - buttonsWidth, LayoutBounds.Y, ButtonSize, LayoutBounds.Height);
    }

    /// <summary>Centres a square icon of <paramref name="iconSize"/> pixels within <paramref name="bounds"/>.</summary>
    private static Rectangle CenterIcon(Rectangle bounds, int iconSize)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return Rectangle.Empty;
        }

        return new Rectangle(
            bounds.X + (bounds.Width - iconSize) / 2,
            bounds.Y + (bounds.Height - iconSize) / 2,
            iconSize,
            iconSize);
    }

    private Rectangle GetCloseIconBounds() => CenterIcon(GetCloseButtonBounds(), 12);

    private Rectangle GetPinIconBounds() => CenterIcon(GetPinButtonBounds(), 12);

    private static void SyncAccessoryButtonBackground(MGBorder button, IFillBrush background)
    {
        if (button?.BackgroundBrush == null)
        {
            return;
        }

        button.SetBackgroundAll(background, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));
    }

    /// <summary>
    /// Creates and assigns the right-click context menu for this tab.
    /// A fresh menu is built on every right-click so items never accumulate.
    /// </summary>
    private void BuildContextMenu(MGWindow window)
    {
        // Build a brand-new menu on every RMB release so the item list is
        // always exactly right — no stale state, no duplicate-items bug.
        MouseHandler.RMBReleasedInside += (_, e) =>
        {
            if (e.IsHandled)
            {
                return;
            }

            var menu = new MGContextMenu(window, "");
            menu.CanContextMenuOpen = true;

            // Snapshot the current panel so lambdas capture the right value
            var panel = Panel;

            // Close — only if the panel allows it
            if (panel?.CanClose == true)
            {
                menu.AddButton("Close", _ => CloseRequested?.Invoke(this, panel));
                menu.AddSeparator();
            }

            // Dock — only for a tab that lives inside a floating window (it cannot float again).
            // Float — only for a docked tab whose panel allows floating.
            if (OwnerFloatingWindow != null)
            {
                menu.AddButton("Dock", _ => DockRequested?.Invoke(this, panel));
                menu.AddSeparator();
            }
            else if (panel?.CanFloat == true)
            {
                menu.AddButton("Float", _ => FloatRequested?.Invoke(this, panel));
                menu.AddSeparator();
            }

            // Pin / Auto-Hide — only if the panel allows auto-hide
            if (panel?.CanAutoHide == true)
            {
                string pinLabel = (panel.IsPinned) ? "Auto-Hide" : "Pin (restore)";
                menu.AddButton(pinLabel, _ => PinToggleRequested?.Invoke(this, panel));
                menu.AddSeparator();
            }

            menu.AddButton("Close Others", _ => CloseOthersRequested?.Invoke(this, panel));
            menu.AddButton("Close All",    _ => CloseAllRequested?.Invoke(this, panel));

            window.Desktop.TryOpenContextMenu(menu, e.Position);
            e.SetHandledBy(this, false);
        };
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
            _titleText.SetDefaultTextForegroundSlot(UIValueSlot.Normal, IsActive ? ActiveTextColor : InactiveTextColor, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));
        }

        if (_surfaceElement != null)
        {
            IFillBrush effectiveBackground = IsActive
                ? ActiveBrush
                : IsHovered
                    ? HoverBrush
                    : NormalBrush;
            _surfaceElement.SetBackgroundSlot(UIValueSlot.Normal, effectiveBackground, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));
            SyncAccessoryButtonBackground(_closeButton, effectiveBackground);
            SyncAccessoryButtonBackground(_pinButton, effectiveBackground);
        }

        if (_accentElement != null)
        {
            Color accentColor = IsActive ? ActiveAccentColor : HoverAccentColor;
            bool showAccent = (IsActive || IsHovered) && accentColor.A > 0;
            // Hidden, not Collapsed: this is IsHovered-dependent and is now refreshed once per tick
            // (see UpdateSelf) — toggling to/from Collapsed calls LayoutChanged on every hover change,
            // which would invalidate layout merely from hovering. See remarks below on the close/pin
            // accessories for the same reasoning.
            _accentElement.Visibility = showAccent ? Visibility.Visible : Visibility.Hidden;
            _accentElement.Width = LayoutBounds.Width;
            _accentElement.Height = IsActive ? 3 : 2;
            _accentElement.Fill = accentColor.AsFillBrush();
        }

        // Reveal on hover/active: the close/pin accessories (button + icon) are shown only while
        // the tab is active or hovered (and only when the panel allows the action). Hidden state
        // uses Visibility.Hidden rather than Collapsed: Hidden keeps the reserved layout space and
        // does not invalidate layout, whereas toggling to/from Collapsed would (see MGElement.Visibility).
        bool closeRevealed = IsCloseAccessoryRevealed;
        if (_closeButton != null)
        {
            _closeButton.Visibility = closeRevealed ? Visibility.Visible : Visibility.Hidden;
        }

        if (_closeIconElement != null)
        {
            _closeIconElement.Visibility = closeRevealed ? Visibility.Visible : Visibility.Hidden;
            _closeIconElement.Color = IsActive ? ActiveIconColor : InactiveIconColor;
        }

        bool pinRevealed = IsPinAccessoryRevealed;
        if (_pinButton != null)
        {
            _pinButton.Visibility = pinRevealed ? Visibility.Visible : Visibility.Hidden;
        }

        if (_pinIconElement != null)
        {
            _pinIconElement.Visibility = pinRevealed ? Visibility.Visible : Visibility.Hidden;
            _pinIconElement.Color = InactiveIconColor;
            _pinIconElement.IsPinned = Panel?.IsPinned == true;
        }
    }

    public void RefreshThemeVisuals() => UpdateVisuals();

    private bool _lastIsHovered;

    /// <summary>
    /// Per-tick refresh hook (see <see cref="MGElement.UpdateSelf"/>, the same hook
    /// <see cref="MGDockHost"/> uses for its own per-tick polling). Layout only runs on
    /// invalidation, so <see cref="MGElement.IsHovered"/>-dependent visuals (surface background, accent,
    /// and the close/pin reveal) would otherwise go stale between clicks. Refreshes only when
    /// <see cref="MGElement.IsHovered"/> actually changed, and never invalidates layout.
    /// </summary>
    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);

        bool hovered = IsHovered;
        if (hovered != _lastIsHovered)
        {
            _lastIsHovered = hovered;
            UpdateVisuals();
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

        if (_pinButton != null && Panel?.CanAutoHide == true)
        {
            yield return _pinButton;
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

        // Close and pin buttons occupy fixed reserved areas.
        int closeWidth = (Panel?.CanClose == true) ? ButtonSize : 0;
        int pinWidth   = (Panel?.CanAutoHide == true) ? ButtonSize : 0;

        int totalWidth = Math.Max(MinTabWidth, titleWidth + pinWidth + closeWidth);
        _lastMeasuredWidth = totalWidth; // expose desired width for overflow detection in MGDockTabGroup
        return new Thickness(totalWidth, TabHeight, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        if (_titleText == null)
        {
            return;
        }

        // Layout title text — occupies everything left of pin + close buttons
        int closeWidth = GetCloseWidth();
        int pinWidth   = GetPinWidth();
        int buttonsWidth = pinWidth + closeWidth;

        Rectangle titleBounds = new Rectangle(
            Bounds.X,
            Bounds.Y,
            Bounds.Width - buttonsWidth,
            Bounds.Height
        );
        _titleText.UpdateLayout(titleBounds);

        // Pin button — left of close button
        if (pinWidth > 0)
        {
            _pinButton.UpdateLayout(GetPinButtonBounds());
        }

        // Close button — flush right
        if (closeWidth > 0)
        {
            _closeButton.UpdateLayout(GetCloseButtonBounds());
        }

        UpdateVisuals();
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        foreach (var child in GetChildren())
        {
            child?.Draw(DA);
        }
    }
}
