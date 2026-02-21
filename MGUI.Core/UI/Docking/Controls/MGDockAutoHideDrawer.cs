using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// An overlay panel that slides out from a host edge to show the content of an
/// auto-hidden panel.  Positioned and sized by <see cref="MGDockHost"/> as a Component.
/// </summary>
public class MGDockAutoHideDrawer : MGElement
{
    // ── Constants ─────────────────────────────────────────────────────
    private const int HeaderHeight    = 28;
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
    private readonly MGBorder     _header;
    private readonly MGTextBlock  _titleLabel;
    private readonly MGBorder     _pinBtn;
    private readonly MGBorder     _closeBtn;
    private MGElement             _content; // replaced when ActivePanel changes

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
            BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(37, 37, 38));

            // ── Header ────────────────────────────────────────────────
            _header = new MGBorder(window, new XAML.Thickness(0).ToThickness(), (IFillBrush)null)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Top
            };
            _header.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(45, 45, 48));

            _titleLabel = new MGTextBlock(window, "")
            {
                FontSize            = 12,
                WrapText            = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment   = VerticalAlignment.Center,
                Padding             = new XAML.Thickness(6, 2, 4, 2).ToThickness()
            };
            _titleLabel.DefaultTextForeground.NormalValue = Color.White;

            _pinBtn = CreateHeaderButton(window, "📌 Pin", () =>
            {
                if (_activePanel != null)
                    PinRequested?.Invoke(this, _activePanel);
            });

            _closeBtn = CreateHeaderButton(window, "\u00d7", () =>
            {
                if (_activePanel != null)
                    PanelCloseRequested?.Invoke(this, _activePanel);
                else
                    CloseRequested?.Invoke(this, EventArgs.Empty);
            });

            _header.SetParent(this);

            _content = CreatePlaceholder();
            _content.SetParent(this);

            _titleLabel.SetParent(this);
            _pinBtn.SetParent(this);
            _closeBtn.SetParent(this);
        }
    }

    // ── Header-button factory ─────────────────────────────────────────
    private static MGBorder CreateHeaderButton(MGWindow window, string label, Action onClick)
    {
        var body = new MGBorder(window, new XAML.Thickness(0).ToThickness(), (IFillBrush)null)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch
        };
        body.BackgroundBrush = new VisualStateFillBrush(
            (IFillBrush)null,
            new Color(62, 62, 66),
            PressedModifierType.Darken, 0.10f);

        var text = new MGTextBlock(window, label)
        {
            FontSize            = 11,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Padding             = new XAML.Thickness(6, 2, 6, 2).ToThickness()
        };
        text.DefaultTextForeground.NormalValue = new Color(200, 200, 200);
        body.SetContent(text);

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
            _content.SetParent(null);

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
        yield return _header;
        yield return _titleLabel;
        yield return _pinBtn;
        yield return _closeBtn;
        if (_content != null)
            yield return _content;
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
        _titleLabel.UpdateMeasurement(new Size(AvailableSize.Width - 60, HeaderHeight), out _, out _, out _, out _);
        _pinBtn.UpdateMeasurement( new Size(50, HeaderHeight), out _, out _, out _, out _);
        _closeBtn.UpdateMeasurement(new Size(20, HeaderHeight), out _, out _, out _, out _);
        // Content
        int contentHeight = Math.Max(0, AvailableSize.Height - HeaderHeight);
        _content?.UpdateMeasurement(new Size(AvailableSize.Width, contentHeight), out _, out _, out _, out _);
        return new Thickness(AvailableSize.Width, AvailableSize.Height, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        int headerW = Bounds.Width;
        _header.UpdateLayout(new Rectangle(Bounds.X, Bounds.Y, headerW, HeaderHeight));

        // Title takes up remaining space after the two buttons
        int closeBtnW = 20;
        int pinBtnW   = 50;
        int titleW    = Math.Max(0, headerW - pinBtnW - closeBtnW - 2);
        _titleLabel.UpdateLayout(new Rectangle(Bounds.X, Bounds.Y, titleW, HeaderHeight));
        _pinBtn.UpdateLayout(new Rectangle(Bounds.X + titleW, Bounds.Y, pinBtnW, HeaderHeight));
        _closeBtn.UpdateLayout(new Rectangle(Bounds.X + titleW + pinBtnW, Bounds.Y, closeBtnW, HeaderHeight));

        // Content below header
        int contentY = Bounds.Y + HeaderHeight;
        int contentH = Math.Max(0, Bounds.Height - HeaderHeight);
        _content?.UpdateLayout(new Rectangle(Bounds.X, contentY, Bounds.Width, contentH));
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

    // ── Draw shadow/border ─────────────────────────────────────────────
    protected override void DrawContents(ElementDrawArgs DA)
    {
        // Draw all child elements
        foreach (var child in GetChildren())
            child?.Draw(DA);

        var LayoutBounds = this.LayoutBounds;
        // Draw a visible border
        var borderColor = new Color(80, 80, 85) * DA.Opacity;
        // Top
        DA.DT.FillRectangle(Microsoft.Xna.Framework.Vector2.Zero, new RectangleF(LayoutBounds.X, LayoutBounds.Y, LayoutBounds.Width, 1), borderColor);
        // Bottom
        DA.DT.FillRectangle(Microsoft.Xna.Framework.Vector2.Zero, new RectangleF(LayoutBounds.X, LayoutBounds.Bottom - 1, LayoutBounds.Width, 1), borderColor);
        // Left
        DA.DT.FillRectangle(Microsoft.Xna.Framework.Vector2.Zero, new RectangleF(LayoutBounds.X, LayoutBounds.Y, 1, LayoutBounds.Height), borderColor);
        // Right
        DA.DT.FillRectangle(Microsoft.Xna.Framework.Vector2.Zero, new RectangleF(LayoutBounds.Right - 1, LayoutBounds.Y, 1, LayoutBounds.Height), borderColor);

        // Draw resize grip highlight on the inner edge
        Rectangle grip = GetResizeGripRect(LayoutBounds);
        if (grip.Width > 0)
        {
            var gripColor = new Color(100, 100, 110) * DA.Opacity;
            DA.DT.FillRectangle(Microsoft.Xna.Framework.Vector2.Zero, new RectangleF(grip.X, grip.Y, grip.Width, grip.Height), gripColor);
        }
    }
}
