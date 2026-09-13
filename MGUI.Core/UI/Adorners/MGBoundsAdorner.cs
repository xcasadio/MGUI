using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Adorners;

public class MGBoundsAdorner : MGAdorner
{
    private Color _fillColor = Color.Transparent;
    public Color FillColor
    {
        get => _fillColor;
        set
        {
            if (_fillColor != value)
            {
                _fillColor = value;
                NPC(nameof(FillColor));
            }
        }
    }

    private Color _borderColor = Color.White;
    public Color BorderColor
    {
        get => _borderColor;
        set
        {
            if (_borderColor != value)
            {
                _borderColor = value;
                NPC(nameof(BorderColor));
            }
        }
    }

    private int _borderThickness = 1;
    public int BorderThickness
    {
        get => _borderThickness;
        set
        {
            var clamped = Math.Max(0, value);
            if (_borderThickness != clamped)
            {
                _borderThickness = clamped;
                NPC(nameof(BorderThickness));
            }
        }
    }

    public MGBoundsAdorner(MGWindow window)
        : base(window)
    {
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        if (!TryGetAdornedBounds(out var adornedBounds))
        {
            return;
        }

        if (FillColor.A > 0)
        {
            DA.DT.FillRectangle(Vector2.Zero, new RectangleF(adornedBounds.X, adornedBounds.Y, adornedBounds.Width, adornedBounds.Height), FillColor * DA.Opacity);
        }

        if (BorderThickness > 0)
        {
            DrawBorder(DA, adornedBounds, BorderColor * DA.Opacity, BorderThickness);
        }
    }

    internal static void DrawBorder(ElementDrawArgs DA, Rectangle bounds, Color color, int thickness)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || thickness <= 0 || color.A <= 0)
        {
            return;
        }

        var clamped = Math.Min(thickness, Math.Min(bounds.Width, bounds.Height));
        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(bounds.X, bounds.Y, bounds.Width, clamped), color);
        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(bounds.X, bounds.Bottom - clamped, bounds.Width, clamped), color);
        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(bounds.X, bounds.Y, clamped, bounds.Height), color);
        DA.DT.FillRectangle(Vector2.Zero, new RectangleF(bounds.Right - clamped, bounds.Y, clamped, bounds.Height), color);
    }
}