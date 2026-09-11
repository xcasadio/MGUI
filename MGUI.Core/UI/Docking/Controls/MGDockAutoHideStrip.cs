using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Helpers;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// A thin strip rendered on one edge of <see cref="MGDockHost"/> that contains
/// tab buttons for every panel currently in the auto-hide store for that edge.
/// Clicking a button opens the panel's <see cref="MGDockAutoHideDrawer"/>.
/// </summary>
public class MGDockAutoHideStrip : MGElement
{
    public const string SeparatorPartName = "PART_Separator";

    // ── Constants ─────────────────────────────────────────────────────
    /// <summary>Thickness of the strip perpendicular to its edge (pixels).</summary>
    public const int StripThickness = 24;

    private const int ButtonMinSize = 60; // min width (horizontal) / height (vertical) per button
    private const int ButtonPadding  = 20; // horizontal / leading+trailing padding added to text measurement
    private const int ButtonSpacing  = 1;

    // ── State ─────────────────────────────────────────────────────────
    private AutoHideSide _side;
    /// <summary>Which edge of the host this strip represents.</summary>
    public AutoHideSide Side
    {
        get => _side;
        set
        {
            if (_side != value)
            {
                _side = value;
                SyncSeparatorVisuals();
                NPC(nameof(Side));
            }
        }
    }

    private bool IsHorizontal => _side == AutoHideSide.Top || _side == AutoHideSide.Bottom;

    // Panel buttons — rebuilt whenever the panel list changes
    private readonly List<MGBorder> _buttons = new List<MGBorder>();
    // Mapping from button → panel so we know which one was clicked
    private readonly Dictionary<MGBorder, DockPanelNode> _buttonMap = new Dictionary<MGBorder, DockPanelNode>();
    private MGRectangle SeparatorElement { get; }
    private MGComponent<MGRectangle> SeparatorComponent { get; }

    public VisualStateFillBrush ButtonBackgroundBrush { get; set; } =
        new VisualStateFillBrush(
            new MGSolidFillBrush(new Color(37, 37, 38)),
            new Color(62, 62, 66),
            PressedModifierType.Darken,
            0.10f);

    public Color TextColor { get; set; } = new Color(200, 200, 200);
    public Color SeparatorColor { get; set; } = new Color(60, 60, 65);

    /// <summary>Fired when the user clicks a panel button in the strip.</summary>
    public event EventHandler<DockPanelNode> PanelActivated;

    // ── Constructor ───────────────────────────────────────────────────
    public MGDockAutoHideStrip(MGWindow window, AutoHideSide side) : base(window, MGElementType.Custom)
    {
        using (BeginInitializing())
        {
            _side = side;

            // Background — slightly darker than normal panel background
            SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(new Color(30, 30, 32)), UIValueResolutionSource.Default(UIInvalidationKind.Draw));

            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment   = VerticalAlignment.Stretch;
            DefaultControlTemplateName = MGControlTemplateCatalog.DockAutoHideStripTemplateName;

            SeparatorElement = new(window, 0, 0, Color.Transparent, 0, Color.Transparent)
            {
                ManagedParent = this,
                IsHitTestVisible = false,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            RegisterTemplatePart(SeparatorPartName, SeparatorElement);
            SeparatorComponent = new(SeparatorElement, false, false, false, false, false, false, false,
                (availableBounds, componentSize) => GetSeparatorBounds());
            AddComponent(SeparatorComponent);
            SyncSeparatorVisuals();
        }
    }

    private Rectangle GetSeparatorBounds()
    {
        return _side switch
        {
            AutoHideSide.Left => new Rectangle(LayoutBounds.Right - 1, LayoutBounds.Y, 1, LayoutBounds.Height),
            AutoHideSide.Right => new Rectangle(LayoutBounds.X, LayoutBounds.Y, 1, LayoutBounds.Height),
            AutoHideSide.Top => new Rectangle(LayoutBounds.X, LayoutBounds.Bottom - 1, LayoutBounds.Width, 1),
            AutoHideSide.Bottom => new Rectangle(LayoutBounds.X, LayoutBounds.Y, LayoutBounds.Width, 1),
            _ => Rectangle.Empty
        };
    }

    private void SyncSeparatorVisuals()
    {
        if (SeparatorElement == null)
        {
            return;
        }

        Rectangle bounds = GetSeparatorBounds();
        SeparatorElement.Width = bounds.Width;
        SeparatorElement.Height = bounds.Height;
        SeparatorElement.Fill = SeparatorColor.AsFillBrush();
    }

    // ── Panel list management ─────────────────────────────────────────

    /// <summary>
    /// Rebuilds the button list to match <paramref name="panels"/>.
    /// Call this whenever the auto-hide store changes.
    /// </summary>
    public void Refresh(IReadOnlyList<DockPanelNode> panels)
    {
        // Remove all old buttons
        foreach (var btn in _buttons)
        {
            btn.SetParent(null);
        }

        _buttons.Clear();
        _buttonMap.Clear();

        foreach (var panel in panels)
        {
            var capturedPanel = panel;
            var btn = CreateButton(panel.Title);
            btn.SetParent(this);
            _buttons.Add(btn);
            _buttonMap[btn] = capturedPanel;

            btn.MouseHandler.LMBReleasedInside += (_, e) =>
            {
                if (!e.IsHandled)
                {
                    PanelActivated?.Invoke(this, capturedPanel);
                    e.SetHandledBy(btn, false);
                }
            };
        }

        ApplyThemeVisuals();
        LayoutChanged(this, true);
    }

    public void ApplyThemeVisuals()
    {
        foreach (var btn in _buttons)
        {
            // ADR-0005/S5: ButtonBackgroundBrush is one shared instance for the whole strip -- every button in
            // _buttons would otherwise subscribe to the SAME container, accumulating subscribers indefinitely
            // (see the Window.CloseButtonBackground comment in MGControlTemplateCatalog.cs). Copy per button.
            btn.SetBackground(ButtonBackgroundBrush?.Copy(), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
            if (btn.Content is MGTextBlock label)
            {
                label.DefaultTextForeground.NormalValue = TextColor;
            }
            else if (btn.Content is MGRotatedTextLabel rotatedLabel)
            {
                rotatedLabel.TextColor = TextColor;
            }
        }

        SyncSeparatorVisuals();
    }

    // ── Text-measurement helper ───────────────────────────────────────
    /// <summary>
    /// Returns the button's variable dimension in pixels (width for horizontal strips,
    /// height for vertical strips), sized to fit <paramref name="title"/> with padding.
    /// </summary>
    private int MeasureButtonSizePx(string title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return ButtonMinSize;
        }

        ITextMeasurementEngine textEngine = ParentWindow.Desktop.TextEngine;
        ResolvedFont resolved = textEngine.ResolveFont(new FontSpec(ParentWindow.Desktop.DefaultFontFamily, 11, CustomFontStyles.Normal));
        if (!resolved.IsAvailable)
        {
            return ButtonMinSize;
        }

        float textWidth = textEngine.MeasureText(resolved, title).X;
        if (!resolved.ExactScale.IsAlmostZero())
        {
            textWidth = textWidth / resolved.ExactScale * resolved.SuggestedScale;
        }

        int textPx = (int)Math.Ceiling(textWidth);
        return Math.Max(ButtonMinSize, textPx + ButtonPadding);
    }

    // ── Button factory ────────────────────────────────────────────────
    private MGBorder CreateButton(string title)
    {
        var body = new MGBorder(ParentWindow, new XAML.Thickness(0).ToThickness(), (IFillBrush)null)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch
        };
        // ADR-0005/S5: see the ApplyThemeVisuals comment above -- ButtonBackgroundBrush is shared across every
        // button this strip creates, so each new button needs its own copy.
        body.SetBackground(ButtonBackgroundBrush?.Copy(), UIValueResolutionSource.Default(UIInvalidationKind.Draw));

        if (IsHorizontal)
        {
            var label = new MGTextBlock(ParentWindow, title)
            {
                FontSize            = 11,
                WrapText            = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                IsHitTestVisible    = false,
            };
            label.SetPadding(new XAML.Thickness(4, 2, 4, 2).ToThickness(), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            label.DefaultTextForeground.NormalValue = TextColor;
            body.SetContent(label);
        }
        else
        {
            var label = new MGRotatedTextLabel(ParentWindow, title)
            {
                TextColor = TextColor,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            body.SetContent(label);
        }
        return body;
    }

    // ── Layout ────────────────────────────────────────────────────────
    public override IEnumerable<MGElement> GetChildren()
    {
        foreach (var btn in _buttons)
        {
            yield return btn;
        }
    }

    protected override Thickness UpdateContentMeasurement(Size AvailableSize)
    {
        if (IsHorizontal)
        {
            // Top / Bottom: fixed height = StripThickness, width = text-measured per button
            int totalW = 0;
            foreach (var btn in _buttons)
            {
                string title = _buttonMap.TryGetValue(btn, out var p) ? p.Title : "";
                int btnW = MeasureButtonSizePx(title);
                btn.UpdateMeasurement(new Size(btnW, StripThickness), out _, out _, out _, out _);
                totalW += btnW + ButtonSpacing;
            }
            return new Thickness(totalW, StripThickness, 0, 0);
        }
        else
        {
            // Left / Right: fixed width = StripThickness, height = text-measured per button
            // (text is rotated 90°, so text WIDTH → button HEIGHT)
            int totalH = 0;
            foreach (var btn in _buttons)
            {
                string title = _buttonMap.TryGetValue(btn, out var p) ? p.Title : "";
                int btnH = MeasureButtonSizePx(title);
                btn.UpdateMeasurement(new Size(StripThickness, btnH), out _, out _, out _, out _);
                totalH += btnH + ButtonSpacing;
            }
            return new Thickness(StripThickness, totalH, 0, 0);
        }
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        if (IsHorizontal)
        {
            int x = Bounds.X;
            foreach (var btn in _buttons)
            {
                string title = _buttonMap.TryGetValue(btn, out var p) ? p.Title : "";
                int w = MeasureButtonSizePx(title);
                btn.UpdateLayout(new Rectangle(x, Bounds.Y, w, Bounds.Height));
                x += w + ButtonSpacing;
            }
        }
        else
        {
            int y = Bounds.Y;
            foreach (var btn in _buttons)
            {
                string title = _buttonMap.TryGetValue(btn, out var p) ? p.Title : "";
                int h = MeasureButtonSizePx(title);
                btn.UpdateLayout(new Rectangle(Bounds.X, y, Bounds.Width, h));
                y += h + ButtonSpacing;
            }
        }
        SyncSeparatorVisuals();
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        foreach (var child in GetChildren())
        {
            child?.Draw(DA);
        }
    }
}
