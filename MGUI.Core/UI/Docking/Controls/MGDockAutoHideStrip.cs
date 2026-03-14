using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Text;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// A thin strip rendered on one edge of <see cref="MGDockHost"/> that contains
/// tab buttons for every panel currently in the auto-hide store for that edge.
/// Clicking a button opens the panel's <see cref="MGDockAutoHideDrawer"/>.
/// </summary>
public class MGDockAutoHideStrip : MGElement
{
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
                NPC(nameof(Side));
            }
        }
    }

    private bool IsHorizontal => _side == AutoHideSide.Top || _side == AutoHideSide.Bottom;

    // Panel buttons — rebuilt whenever the panel list changes
    private readonly List<MGBorder> _buttons = new List<MGBorder>();
    // Mapping from button → panel so we know which one was clicked
    private readonly Dictionary<MGBorder, DockPanelNode> _buttonMap = new Dictionary<MGBorder, DockPanelNode>();

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
            BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(30, 30, 32));

            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment   = VerticalAlignment.Stretch;
            ControlTemplateName = MGControlTemplateCatalog.DockAutoHideStripTemplateName;
        }
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
            btn.BackgroundBrush = ButtonBackgroundBrush;
            if (btn.Content is MGTextBlock label)
            {
                label.DefaultTextForeground.NormalValue = TextColor;
            }
        }
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

        string family = ParentWindow.Desktop.FontManager.DefaultFontFamily;
        if (!ParentWindow.Desktop.FontManager.TryGetFont(family, CustomFontStyles.Normal, 11, true,
            out _, out SpriteFont sf, out _, out _, out float scale))
        {
            return ButtonMinSize;
        }

        int textPx = (int)Math.Ceiling(sf.MeasureString(title).X * scale);
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
        body.BackgroundBrush = ButtonBackgroundBrush;

        // Only add a label for horizontal strips; vertical strips use rotated DrawContents text
        if (IsHorizontal)
        {
            var label = new MGTextBlock(ParentWindow, title)
            {
                FontSize            = 11,
                WrapText            = false,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
                Padding             = new XAML.Thickness(4, 2, 4, 2).ToThickness(),
                IsHitTestVisible    = false,
            };
            label.DefaultTextForeground.NormalValue = TextColor;
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
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        // Draw button children
        foreach (var child in GetChildren())
        {
            child?.Draw(DA);
        }

        // For Left / Right strips, draw the title text rotated 90° via the FSS text engine
        // (vector rendering — stays crisp at any angle, unlike scaled SpriteFonts).
        if (!IsHorizontal)
        {
            string family = ParentWindow.Desktop.FontManager.DefaultFontFamily;
            var resolved  = DA.DT.TextEngine.ResolveFont(new FontSpec(family, 11, CustomFontStyles.Normal));
            if (resolved?.NativeFont != null)
            {
                foreach (var (btn, panel) in _buttonMap)
                {
                    string title = panel.Title;
                    if (string.IsNullOrEmpty(title))
                    {
                        continue;
                    }

                    float   scale    = resolved.SuggestedScale;
                    Vector2 textSize = DA.DT.TextEngine.MeasureText(resolved, title); // unscaled
                    Vector2 origin   = textSize / 2f;                                 // pivot at text centre
                    Vector2 pos      = new Vector2(
                        btn.LayoutBounds.X + btn.LayoutBounds.Width  / 2f,
                        btn.LayoutBounds.Y + btn.LayoutBounds.Height / 2f);
                    Color textColor  = TextColor * DA.Opacity;
                    DA.DT.DrawTextViaEngine(resolved, title, pos, textColor, origin, scale, -MathF.PI / 2f);
                }
            }
        }

        var LayoutBounds = this.LayoutBounds;
        // Draw a thin separator line on the inner edge
        var sepColor = SeparatorColor * DA.Opacity;
        switch (_side)
        {
            case AutoHideSide.Left:
                DA.DT.FillRectangle(Microsoft.Xna.Framework.Vector2.Zero, new RectangleF(LayoutBounds.Right - 1, LayoutBounds.Y, 1, LayoutBounds.Height), sepColor);
                break;
            case AutoHideSide.Right:
                DA.DT.FillRectangle(Microsoft.Xna.Framework.Vector2.Zero, new RectangleF(LayoutBounds.X, LayoutBounds.Y, 1, LayoutBounds.Height), sepColor);
                break;
            case AutoHideSide.Top:
                DA.DT.FillRectangle(Microsoft.Xna.Framework.Vector2.Zero, new RectangleF(LayoutBounds.X, LayoutBounds.Bottom - 1, LayoutBounds.Width, 1), sepColor);
                break;
            case AutoHideSide.Bottom:
                DA.DT.FillRectangle(Microsoft.Xna.Framework.Vector2.Zero, new RectangleF(LayoutBounds.X, LayoutBounds.Y, LayoutBounds.Width, 1), sepColor);
                break;
        }
    }
}
