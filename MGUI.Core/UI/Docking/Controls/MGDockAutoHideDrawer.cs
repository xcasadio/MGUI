using MGUI.Core.UI.Brushes.FillBrushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// An overlay panel that slides out from a host edge to show the content of an
/// auto-hidden panel.  Positioned and sized by <see cref="MGDockHost"/> as a Component.<para/>
/// Its eight parts (border, title bar and title text, pin and close buttons and icons, resize grip) are created by the
/// <c>Dock.AutoHideDrawer.Default</c> control template, which also applies the drawer's theme colors.
/// </summary>
public class MGDockAutoHideDrawer : MGElement
{
    public const string BorderPartName = "PART_Border";
    public const string TitleBarPartName = "PART_TitleBar";
    public const string TitleBarTextPartName = "PART_TitleBarText";

    /// <summary>Obsolete alias for <see cref="TitleBarPartName"/>.</summary>
    [Obsolete("Use TitleBarPartName")]
    public const string HeaderPartName = TitleBarPartName;

    /// <summary>Obsolete alias for <see cref="TitleBarTextPartName"/>.</summary>
    [Obsolete("Use TitleBarTextPartName")]
    public const string TitleLabelPartName = TitleBarTextPartName;

    public const string PinButtonPartName = "PART_PinButton";
    public const string CloseButtonPartName = "PART_CloseButton";
    public const string PinIconPartName = "PART_PinIcon";
    public const string CloseIconPartName = "PART_CloseIcon";
    public const string ResizeGripPartName = "PART_ResizeGrip";

    // ── Constants ─────────────────────────────────────────────────────
    private const int ResizeGripSize  = 4; // thin drag handle on the inner edge

    private int _headerHeight = 28;
    /// <summary>Height of the header, in pixels. Default: the theme's <see cref="MGThemeDockingSettings.AutoHideDrawerHeaderHeight"/>, applied by the
    /// <c>Dock.AutoHideDrawer.Default</c> template (backlog task 14).</summary>
    public int HeaderHeight
    {
        get => _headerHeight;
        set
        {
            if (_headerHeight != value)
            {
                _headerHeight = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(HeaderHeight));
            }
        }
    }

    private int _headerButtonSize = 22;
    /// <summary>Size of the square, icon-only pin and close buttons of the header, in pixels. Default: the theme's
    /// <see cref="MGThemeDockingSettings.AutoHideDrawerButtonSize"/>, applied by the <c>Dock.AutoHideDrawer.Default</c> template (backlog task 14).</summary>
    public int HeaderButtonSize
    {
        get => _headerButtonSize;
        set
        {
            if (_headerButtonSize != value)
            {
                _headerButtonSize = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(HeaderButtonSize));
            }
        }
    }

    // ── State ──────────────────────────────────────────────────────────
    private AutoHideSide _side;
    /// <summary>Which edge this drawer is anchored to.</summary>
    public AutoHideSide Side
    {
        get => _side;
        set
        {
            if (_side != value) { _side = value; LayoutChanged(this, true); }
        }
    }

    private DockPanelNode _activePanel;
    /// <summary>The panel currently shown in the drawer, or null.</summary>
    public DockPanelNode ActivePanel
    {
        get => _activePanel;
        set
        {
            if (_activePanel != value)
            {
                _activePanel = value;
                RefreshContent();
                NotifyPropertyChanged(nameof(ActivePanel));
            }
        }
    }

    // ── Template parts (see AttachControlTemplateStructure) ────────────
    private MGBorder      _border;
    private MGBorder      _header;
    private MGTextBlock   _titleLabel;
    private MGBorder      _pinBtn;
    private MGBorder      _closeBtn;
    private MGDockPinIcon _pinIcon;
    private MGCloseIcon   _closeIcon;
    private MGBorder      _resizeGrip;
    private MGElement     _content; // replaced when ActivePanel changes

    private Color _headerTextColor = Color.White;
    public Color HeaderTextColor
    {
        get => _headerTextColor;
        set
        {
            if (_headerTextColor != value)
            {
                _headerTextColor = value;
                _titleLabel?.SetDefaultTextForegroundSlot(UIValueSlot.Normal, value, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
                NotifyPropertyChanged(nameof(HeaderTextColor));
            }
        }
    }

    private Color _iconColor = new Color(200, 200, 200);
    public Color IconColor
    {
        get => _iconColor;
        set
        {
            if (_iconColor != value)
            {
                _iconColor = value;
                ApplyThemeVisuals();
                NotifyPropertyChanged(nameof(IconColor));
            }
        }
    }

    private Color _borderColor = new Color(80, 80, 85);
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor != value)
            {
                _borderColor = value;
                ApplyThemeVisuals();
                NotifyPropertyChanged(nameof(BorderColor));
            }
        }
    }

    private Color _resizeGripColor = new Color(100, 100, 110);
    public Color ResizeGripColor
    {
        get => _resizeGripColor;
        set
        {
            if (_resizeGripColor != value)
            {
                _resizeGripColor = value;
                ApplyThemeVisuals();
                NotifyPropertyChanged(nameof(ResizeGripColor));
            }
        }
    }

    // ── Events ─────────────────────────────────────────────────────────
    /// <summary>Fired when the user clicks the Pin button — requests the panel be returned to the layout.</summary>
    public event EventHandler<DockPanelNode> PinRequested;

    /// <summary>Fired when the user clicks the close (×) button — closes the panel entirely (removes it from the layout).</summary>
    public event EventHandler<DockPanelNode> PanelCloseRequested;

    /// <summary>Fired to close the drawer without affecting the panel (e.g. click-outside dismissal).</summary>
    public event EventHandler CloseRequested;

    /// <summary>Fired when the user drags the resize grip, with the new drawer size in pixels.</summary>
    public event EventHandler<int> DrawerSizeChanged;

    // ── Resize grip state ──────────────────────────────────────────────
    private bool _isResizing;
    private Point _resizeDragStart;
    private int   _resizeStartSize;

    // ── Constructor ───────────────────────────────────────────────────
    public MGDockAutoHideDrawer(MGWindow window) : base(window, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment   = VerticalAlignment.Stretch;

            // The eight parts and the drawer's colors come from the control template, see AttachControlTemplateStructure.
            DefaultControlTemplateName = MGControlTemplateCatalog.DockAutoHideDrawerTemplateName;

            _content = CreatePlaceholder();
            _content.SetParent(this);
            ApplyThemeVisuals();
        }
    }

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        yield return new(BorderPartName, typeof(MGBorder));
        yield return new(TitleBarPartName, typeof(MGBorder));
        yield return new(TitleBarTextPartName, typeof(MGTextBlock));
        yield return new(PinButtonPartName, typeof(MGBorder));
        yield return new(CloseButtonPartName, typeof(MGBorder));
        yield return new(PinIconPartName, typeof(MGDockPinIcon));
        yield return new(CloseIconPartName, typeof(MGCloseIcon));
        yield return new(ResizeGripPartName, typeof(MGBorder));
    }

    /// <summary>Binds the eight parts created by the control template (<c>Dock.AutoHideDrawer.Default</c>) as children of this drawer, laid out by
    /// <see cref="UpdateContentLayout"/>. The pin and close clicks, the hit-test transparency of the decorative parts, the title of the active panel
    /// and the colors kept in <see cref="HeaderTextColor"/>, <see cref="IconColor"/>, <see cref="BorderColor"/> and <see cref="ResizeGripColor"/> stay
    /// on this drawer and are pushed to the parts. A structure replaced by another template detaches the parts it replaces.</summary>
    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
    {
        var pinButton = (MGBorder)Structure.Parts[PinButtonPartName];
        var closeButton = (MGBorder)Structure.Parts[CloseButtonPartName];
        if (!ReferenceEquals(_pinBtn, pinButton))
        {
            if (_pinBtn != null)
            {
                _pinBtn.MouseHandler.LMBReleasedInside -= OnPinButtonReleased;
            }

            pinButton.MouseHandler.LMBReleasedInside += OnPinButtonReleased;
        }

        if (!ReferenceEquals(_closeBtn, closeButton))
        {
            if (_closeBtn != null)
            {
                _closeBtn.MouseHandler.LMBReleasedInside -= OnCloseButtonReleased;
            }

            closeButton.MouseHandler.LMBReleasedInside += OnCloseButtonReleased;
        }

        _border = Reparent(_border, (MGBorder)Structure.Parts[BorderPartName]);
        _header = Reparent(_header, (MGBorder)Structure.Parts[TitleBarPartName]);
        _titleLabel = Reparent(_titleLabel, (MGTextBlock)Structure.Parts[TitleBarTextPartName]);
        _pinBtn = Reparent(_pinBtn, pinButton);
        _closeBtn = Reparent(_closeBtn, closeButton);
        _pinIcon = Reparent(_pinIcon, (MGDockPinIcon)Structure.Parts[PinIconPartName]);
        _closeIcon = Reparent(_closeIcon, (MGCloseIcon)Structure.Parts[CloseIconPartName]);
        _resizeGrip = Reparent(_resizeGrip, (MGBorder)Structure.Parts[ResizeGripPartName]);

        // Decorative parts let the mouse through to the drawer, whose UpdateSelf handles the resize drag.
        _border.ManagedParent = this;
        _border.IsHitTestVisible = false;
        _titleLabel.IsHitTestVisible = false;
        _pinIcon.ManagedParent = this;
        _closeIcon.ManagedParent = this;
        _resizeGrip.ManagedParent = this;
        _resizeGrip.IsHitTestVisible = false;

        _titleLabel.SetText(_activePanel?.Title ?? "");
        _titleLabel.SetDefaultTextForegroundSlot(UIValueSlot.Normal, HeaderTextColor, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        ApplyThemeVisuals();
        LayoutChanged(this, true);
    }

    /// <summary>Parents <paramref name="part"/> to this drawer and detaches the part it replaces, if a new structure brings another element.</summary>
    private T Reparent<T>(T previous, T part) where T : MGElement
    {
        if (previous != null && !ReferenceEquals(previous, part))
        {
            previous.SetParent(null);
        }

        part.SetParent(this);
        return part;
    }

    private void OnPinButtonReleased(object sender, BaseMouseReleasedEventArgs e)
    {
        if (!e.IsHandled)
        {
            if (_activePanel != null)
            {
                PinRequested?.Invoke(this, _activePanel);
            }

            e.SetHandledBy(_pinBtn, false);
        }
    }

    private void OnCloseButtonReleased(object sender, BaseMouseReleasedEventArgs e)
    {
        if (!e.IsHandled)
        {
            if (_activePanel != null)
            {
                PanelCloseRequested?.Invoke(this, _activePanel);
            }
            else
            {
                CloseRequested?.Invoke(this, EventArgs.Empty);
            }

            e.SetHandledBy(_closeBtn, false);
        }
    }

    private void ApplyThemeVisuals()
    {
        // The parts are attached together; none exists until a control template supplies them.
        if (_border == null)
        {
            return;
        }

        _pinIcon.Color = IconColor;
        _closeIcon.Color = IconColor;
        _border.SetBorderBrush(BorderColor.AsFillBrush().AsUniformBorderBrush(), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        _border.SetBorderThickness(new MonoGame.Extended.Thickness(1), UIValueResolutionSource.Theme(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        _resizeGrip.SetBackgroundSlot(UIValueSlot.Normal, ResizeGripColor.AsFillBrush(), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
    }

    // ── Content swapping ──────────────────────────────────────────────
    private void RefreshContent()
    {
        if (_content != null)
        {
            _content.SetParent(null);
        }

        if (_activePanel != null)
        {
            _titleLabel?.SetText(_activePanel.Title);
            var c = _activePanel.GetOrCreateContent();
            _content = c ?? CreatePlaceholder(_activePanel.Title);
        }
        else
        {
            _titleLabel?.SetText("");
            _content = CreatePlaceholder();
        }

        _content.SetParent(this);
        LayoutChanged(this, true);
    }

    private MGElement CreatePlaceholder(string title = null)
    {
        return new MGTextBlock(ParentWindow, string.IsNullOrEmpty(title) ? "" : $"[{title}]")
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center
        };
    }

    // ── Children ──────────────────────────────────────────────────────
    public override IEnumerable<MGElement> GetChildren()
    {
        // The parts are attached together; none exists until a control template supplies them.
        if (_border != null)
        {
            yield return _border;
            yield return _header;
            yield return _titleLabel;
            yield return _pinBtn;
            yield return _closeBtn;
        }

        if (_content != null)
        {
            yield return _content;
        }

        if (_border != null)
        {
            yield return _pinIcon;
            yield return _closeIcon;
            yield return _resizeGrip;
        }
    }

    // ── Resize grip helpers ────────────────────────────────────────────
    private Rectangle GetResizeGripRect(Rectangle lb)
    {
        return _side switch
        {
            AutoHideSide.Left   => new Rectangle(lb.Right - ResizeGripSize, lb.Y, ResizeGripSize, lb.Height),
            AutoHideSide.Right  => new Rectangle(lb.X, lb.Y, ResizeGripSize, lb.Height),
            AutoHideSide.Top    => new Rectangle(lb.X, lb.Bottom - ResizeGripSize, lb.Width, ResizeGripSize),
            AutoHideSide.Bottom => new Rectangle(lb.X, lb.Y, lb.Width, ResizeGripSize),
            _                   => Rectangle.Empty
        };
    }

    private int ComputeResizeDelta(Point current)
    {
        return _side switch
        {
            AutoHideSide.Left   =>  (current.X - _resizeDragStart.X),
            AutoHideSide.Right  => -(current.X - _resizeDragStart.X),
            AutoHideSide.Top    =>  (current.Y - _resizeDragStart.Y),
            AutoHideSide.Bottom => -(current.Y - _resizeDragStart.Y),
            _                   => 0
        };
    }

    // ── Layout ────────────────────────────────────────────────────────
    protected override Thickness UpdateContentMeasurement(Size AvailableSize)
    {
        // Header
        _header?.UpdateMeasurement(new Size(AvailableSize.Width, HeaderHeight), out _, out _, out _, out _);
        _titleLabel?.UpdateMeasurement(new Size(Math.Max(0, AvailableSize.Width - HeaderButtonSize * 2), HeaderHeight), out _, out _, out _, out _);
        _pinBtn?.UpdateMeasurement( new Size(HeaderButtonSize, HeaderButtonSize), out _, out _, out _, out _);
        _closeBtn?.UpdateMeasurement(new Size(HeaderButtonSize, HeaderButtonSize), out _, out _, out _, out _);
        // Content
        var contentHeight = Math.Max(0, AvailableSize.Height - HeaderHeight);
        _content?.UpdateMeasurement(new Size(AvailableSize.Width, contentHeight), out _, out _, out _, out _);
        return new Thickness(AvailableSize.Width, AvailableSize.Height, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        // Content below header
        var contentY = Bounds.Y + HeaderHeight;
        var contentH = Math.Max(0, Bounds.Height - HeaderHeight);
        _content?.UpdateLayout(new Rectangle(Bounds.X, contentY, Bounds.Width, contentH));

        // The parts are attached together; none exists until a control template supplies them.
        if (_border == null)
        {
            return;
        }

        _border.UpdateLayout(Bounds);
        var headerW = Bounds.Width;
        _header.UpdateLayout(new Rectangle(Bounds.X, Bounds.Y, headerW, HeaderHeight));

        // Title takes up remaining space after the two icon buttons
        var titleW = Math.Max(0, headerW - HeaderButtonSize * 2 - 2);
        var btnY   = Bounds.Y + (HeaderHeight - HeaderButtonSize) / 2;
        _titleLabel.UpdateLayout(new Rectangle(Bounds.X, Bounds.Y, titleW, HeaderHeight));
        _pinBtn.UpdateLayout(new Rectangle(Bounds.X + titleW, btnY, HeaderButtonSize, HeaderButtonSize));
        _closeBtn.UpdateLayout(new Rectangle(Bounds.X + titleW + HeaderButtonSize, btnY, HeaderButtonSize, HeaderButtonSize));
        _pinIcon.UpdateLayout(GetCenteredIconBounds(_pinBtn.LayoutBounds, 14));
        _closeIcon.UpdateLayout(GetCenteredIconBounds(_closeBtn.LayoutBounds, 12));

        var gripBounds = GetResizeGripRect(Bounds);
        _resizeGrip.Visibility = gripBounds.Width > 0 && gripBounds.Height > 0 ? Visibility.Visible : Visibility.Collapsed;
        if (_resizeGrip.Visibility == Visibility.Visible)
        {
            _resizeGrip.UpdateLayout(gripBounds);
        }
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        foreach (var child in GetChildren())
        {
            child?.Draw(DA);
        }
    }

    private static Rectangle GetCenteredIconBounds(Rectangle buttonBounds, int iconSize)
    {
        return new Rectangle(
            buttonBounds.X + (buttonBounds.Width - iconSize) / 2,
            buttonBounds.Y + (buttonBounds.Height - iconSize) / 2,
            iconSize,
            iconSize);
    }

    // ── Resize drag handling ──────────────────────────────────────────
    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        base.UpdateSelf(UA);

        var mouse    = ParentWindow.Desktop.InputTracker.Mouse;
        var mp     = mouse.CurrentPosition;
        var lmbDown = mouse.CurrentState.LeftButton  == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        var wasDown = mouse.PreviousState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

        if (!_isResizing && lmbDown && !wasDown)
        {
            var grip = GetResizeGripRect(LayoutBounds);
            if (grip.Width > 0 && grip.Contains(mp))
            {
                _isResizing     = true;
                _resizeDragStart = mp;
                _resizeStartSize = ActivePanel?.DrawerSize ?? 200;
            }
        }
        else if (_isResizing && lmbDown)
        {
            var newSize = Math.Max(60, _resizeStartSize + ComputeResizeDelta(mp));
            if (ActivePanel != null && ActivePanel.DrawerSize != newSize)
            {
                ActivePanel.DrawerSize = newSize;
                DrawerSizeChanged?.Invoke(this, newSize);
            }
        }
        else if (_isResizing && !lmbDown)
        {
            _isResizing = false;
        }
    }

}
