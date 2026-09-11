using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// An overlay panel that slides out from a host edge to show the content of an
/// auto-hidden panel.  Positioned and sized by <see cref="MGDockHost"/> as a Component.
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
    private const int HeaderHeight    = 28;
    private const int HeaderBtnSize   = 22; // square icon-only buttons
    private const int ResizeGripSize  = 4; // thin drag handle on the inner edge

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
                NPC(nameof(ActivePanel));
            }
        }
    }

    // ── Child elements ─────────────────────────────────────────────────
    private readonly MGBorder     _border;
    private readonly MGBorder     _header;
    private readonly MGTextBlock  _titleLabel;
    private readonly MGBorder     _pinBtn;
    private readonly MGBorder     _closeBtn;
    private readonly MGDockPinIcon _pinIcon;
    private readonly MGCloseIcon   _closeIcon;
    private readonly MGBorder      _resizeGrip;
    private MGElement             _content; // replaced when ActivePanel changes

    private Color _headerTextColor = Color.White;
    public Color HeaderTextColor
    {
        get => _headerTextColor;
        set
        {
            if (_headerTextColor != value)
            {
                _headerTextColor = value;
                _titleLabel.SetDefaultTextForegroundSlot(UIValueSlot.Normal, value, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
                NPC(nameof(HeaderTextColor));
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
                NPC(nameof(IconColor));
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
                NPC(nameof(BorderColor));
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
                NPC(nameof(ResizeGripColor));
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

            // Background
            SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(new Color(37, 37, 38)), UIValueResolutionSource.Default(UIInvalidationKind.Draw));

            _border = new MGBorder(window, new XAML.Thickness(1).ToThickness(), (IFillBrush)null)
            {
                ManagedParent = this,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            RegisterTemplatePart(BorderPartName, _border);
            _border.SetParent(this);

            // ── Header ────────────────────────────────────────────────
            _header = new MGBorder(window, new XAML.Thickness(0).ToThickness(), (IFillBrush)null)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Top
            };
            RegisterTemplatePart(TitleBarPartName, _header);
            _header.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(new Color(45, 45, 48)), UIValueResolutionSource.Default(UIInvalidationKind.Draw));

            _titleLabel = new MGTextBlock(window, "")
            {
                FontSize            = 12,
                WrapText            = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Center,
                IsHitTestVisible    = false,
            };
            _titleLabel.SetPadding(new XAML.Thickness(6, 2, 4, 2).ToThickness(), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            RegisterTemplatePart(TitleBarTextPartName, _titleLabel);
            _titleLabel.SetDefaultTextForegroundSlot(UIValueSlot.Normal, Color.White, UIValueResolutionSource.Default(UIInvalidationKind.Draw));

            _pinBtn = CreateHeaderButton(window, () =>
            {
                if (_activePanel != null)
                {
                    PinRequested?.Invoke(this, _activePanel);
                }
            });
            RegisterTemplatePart(PinButtonPartName, _pinBtn);

            _pinIcon = new(window)
            {
                ManagedParent = this,
                IsPinned = false,
            };
            RegisterTemplatePart(PinIconPartName, _pinIcon);
            _pinIcon.SetParent(this);

            _closeBtn = CreateHeaderButton(window, () =>
            {
                if (_activePanel != null)
                {
                    PanelCloseRequested?.Invoke(this, _activePanel);
                }
                else
                {
                    CloseRequested?.Invoke(this, EventArgs.Empty);
                }
            });
            RegisterTemplatePart(CloseButtonPartName, _closeBtn);

            _closeIcon = new(window)
            {
                ManagedParent = this,
            };
            RegisterTemplatePart(CloseIconPartName, _closeIcon);
            _closeIcon.SetParent(this);

            _resizeGrip = new MGBorder(window, new XAML.Thickness(0).ToThickness(), (IFillBrush)null)
            {
                ManagedParent = this,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            RegisterTemplatePart(ResizeGripPartName, _resizeGrip);
            _resizeGrip.SetParent(this);

            _header.SetParent(this);

            _content = CreatePlaceholder();
            _content.SetParent(this);

            _titleLabel.SetParent(this);
            _pinBtn.SetParent(this);
            _closeBtn.SetParent(this);
            DefaultControlTemplateName = MGControlTemplateCatalog.DockAutoHideDrawerTemplateName;
            ApplyThemeVisuals();
        }
    }

    private void ApplyThemeVisuals()
    {
        _pinIcon.Color = IconColor;
        _closeIcon.Color = IconColor;
        _border.SetBorderBrush(BorderColor.AsFillBrush().AsUniformBorderBrush(), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        _border.SetBorderThickness(new MonoGame.Extended.Thickness(1), UIValueResolutionSource.Theme(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        _resizeGrip.SetBackgroundSlot(UIValueSlot.Normal, ResizeGripColor.AsFillBrush(), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
    }

    // ── Header-button factory ─────────────────────────────────────────
    /// <summary>Creates a square icon-only button for the drawer header.</summary>
    private static MGBorder CreateHeaderButton(MGWindow window, Action onClick)
    {
        var body = new MGBorder(window, new XAML.Thickness(0).ToThickness(), (IFillBrush)null)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch
        };
        body.SetBackground(new VisualStateFillBrush(
            (IFillBrush)null,
            new Color(62, 62, 66),
            PressedModifierType.Darken, 0.10f), UIValueResolutionSource.Default(UIInvalidationKind.Draw));
        // No text child — icon is drawn directly in DrawContents
        body.MouseHandler.LMBReleasedInside += (_, e) =>
        {
            if (!e.IsHandled) { onClick(); e.SetHandledBy(body, false); }
        };
        return body;
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
            _titleLabel.SetText(_activePanel.Title);
            var c = _activePanel.GetOrCreateContent();
            _content = c ?? CreatePlaceholder(_activePanel.Title);
        }
        else
        {
            _titleLabel.SetText("");
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
        yield return _border;
        yield return _header;
        yield return _titleLabel;
        yield return _pinBtn;
        yield return _closeBtn;
        if (_content != null)
        {
            yield return _content;
        }
        yield return _pinIcon;
        yield return _closeIcon;
        yield return _resizeGrip;
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
        _header.UpdateMeasurement(new Size(AvailableSize.Width, HeaderHeight), out _, out _, out _, out _);
        _titleLabel.UpdateMeasurement(new Size(Math.Max(0, AvailableSize.Width - HeaderBtnSize * 2), HeaderHeight), out _, out _, out _, out _);
        _pinBtn.UpdateMeasurement( new Size(HeaderBtnSize, HeaderBtnSize), out _, out _, out _, out _);
        _closeBtn.UpdateMeasurement(new Size(HeaderBtnSize, HeaderBtnSize), out _, out _, out _, out _);
        // Content
        int contentHeight = Math.Max(0, AvailableSize.Height - HeaderHeight);
        _content?.UpdateMeasurement(new Size(AvailableSize.Width, contentHeight), out _, out _, out _, out _);
        return new Thickness(AvailableSize.Width, AvailableSize.Height, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        _border.UpdateLayout(Bounds);
        int headerW = Bounds.Width;
        _header.UpdateLayout(new Rectangle(Bounds.X, Bounds.Y, headerW, HeaderHeight));

        // Title takes up remaining space after the two icon buttons
        int titleW = Math.Max(0, headerW - HeaderBtnSize * 2 - 2);
        int btnY   = Bounds.Y + (HeaderHeight - HeaderBtnSize) / 2;
        _titleLabel.UpdateLayout(new Rectangle(Bounds.X, Bounds.Y, titleW, HeaderHeight));
        _pinBtn.UpdateLayout(new Rectangle(Bounds.X + titleW, btnY, HeaderBtnSize, HeaderBtnSize));
        _closeBtn.UpdateLayout(new Rectangle(Bounds.X + titleW + HeaderBtnSize, btnY, HeaderBtnSize, HeaderBtnSize));
        _pinIcon.UpdateLayout(GetCenteredIconBounds(_pinBtn.LayoutBounds, 14));
        _closeIcon.UpdateLayout(GetCenteredIconBounds(_closeBtn.LayoutBounds, 12));

        // Content below header
        int contentY = Bounds.Y + HeaderHeight;
        int contentH = Math.Max(0, Bounds.Height - HeaderHeight);
        _content?.UpdateLayout(new Rectangle(Bounds.X, contentY, Bounds.Width, contentH));

        Rectangle gripBounds = GetResizeGripRect(Bounds);
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
        Point mp     = mouse.CurrentPosition;
        bool lmbDown = mouse.CurrentState.LeftButton  == Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        bool wasDown = mouse.PreviousState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

        if (!_isResizing && lmbDown && !wasDown)
        {
            Rectangle grip = GetResizeGripRect(LayoutBounds);
            if (grip.Width > 0 && grip.Contains(mp))
            {
                _isResizing     = true;
                _resizeDragStart = mp;
                _resizeStartSize = ActivePanel?.DrawerSize ?? 200;
            }
        }
        else if (_isResizing && lmbDown)
        {
            int newSize = Math.Max(60, _resizeStartSize + ComputeResizeDelta(mp));
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
