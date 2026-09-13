using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>An <see cref="IFillBrush"/> that fills its bounds with a gradient. Each corner has a specified <see cref="Color"/> that the gradient linearly interpolates to.<para/>
/// For a simpler version, use <see cref="MGDiagonalGradientFillBrush"/></summary>
public readonly struct MGGradientFillBrush : IFillBrush
{
    public readonly Color TopLeftColor;
    public readonly Color TopRightColor;
    public readonly Color BottomLeftColor;
    public readonly Color BottomRightColor;

    public MGGradientFillBrush(Color TopLeft, Color TopRight, Color BottomRight, Color BottomLeft)
    {
        TopLeftColor = TopLeft;
        TopRightColor = TopRight;
        BottomLeftColor = BottomLeft;
        BottomRightColor = BottomRight;
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        float Opacity = DA.Opacity;
        if (Opacity > 0 && !Opacity.IsAlmostZero())
        {
            DA.DT.FillQuadrilateralLinearClamp(DA.Offset.ToVector2(),
                Bounds.TopLeft().ToVector2(), TopLeftColor * Opacity,
                Bounds.TopRight().ToVector2(), TopRightColor * Opacity,
                Bounds.BottomRight().ToVector2(), BottomRightColor * Opacity,
                Bounds.BottomLeft().ToVector2(), BottomLeftColor * Opacity);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        float opacity = DA.Opacity;
        if (opacity <= 0 || opacity.IsAlmostZero())
        {
            return;
        }

        if (Geometry.UsesRectangleFastPath)
        {
            Draw(DA, Element, Shape.OuterBounds);
            return;
        }

        Vector2 origin = DA.Offset.ToVector2();
        Rectangle bounds = Shape.OuterBounds;
        for (int i = 0; i + 2 < Geometry.FillIndices.Count; i += 3)
        {
            Vector2 v0 = Geometry.Vertices[Geometry.FillIndices[i]];
            Vector2 v1 = Geometry.Vertices[Geometry.FillIndices[i + 1]];
            Vector2 v2 = Geometry.Vertices[Geometry.FillIndices[i + 2]];

            DA.DT.FillTriangle(
                origin,
                v0, GetColorAt(v0, bounds) * opacity,
                v1, GetColorAt(v1, bounds) * opacity,
                v2, GetColorAt(v2, bounds) * opacity);
        }
    }

    private Color GetColorAt(Vector2 point, Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return TopLeftColor;
        }

        float horizontal = Math.Clamp((point.X - bounds.Left) / bounds.Width, 0f, 1f);
        float vertical = Math.Clamp((point.Y - bounds.Top) / bounds.Height, 0f, 1f);

        Color top = Color.Lerp(TopLeftColor, TopRightColor, horizontal);
        Color bottom = Color.Lerp(BottomLeftColor, BottomRightColor, horizontal);
        return Color.Lerp(top, bottom, vertical);
    }

    public IFillBrush Copy() => new MGGradientFillBrush(TopLeftColor, TopRightColor, BottomRightColor, BottomLeftColor);
}

/// <summary>A simplified version of <see cref="MGGradientFillBrush"/> that only requires 2 <see cref="Color"/>s for opposite corners of the bounds.</summary>
public readonly struct MGDiagonalGradientFillBrush : IFillBrush
{
    public readonly Color Color1;
    public readonly Color Color2;
    public readonly CornerType Color1Position;
    public readonly CornerType Color2Position => OppositeCorners[Color1Position];

    public Color GetColor(CornerType Corner)
    {
        if (Corner == Color1Position)
        {
            return Color1;
        }
        else if (Corner == Color2Position)
        {
            return Color2;
        }
        else
        {
            return Color.Lerp(Color1, Color2, 0.5f);
        }
    }

    private static readonly Dictionary<CornerType, CornerType> OppositeCorners = new Dictionary<CornerType, CornerType>()
    {
        { CornerType.TopLeft, CornerType.BottomRight },
        { CornerType.TopRight, CornerType.BottomLeft },
        { CornerType.BottomRight, CornerType.TopLeft },
        { CornerType.BottomLeft, CornerType.TopRight }
    };

    /// <param name="Color1Position">The corner that <paramref name="Color1"/> is associated with. <see cref="Color2"/> will be at the opposite corner.</param>
    public MGDiagonalGradientFillBrush(Color Color1, Color Color2, CornerType Color1Position)
    {
        this.Color1 = Color1;
        this.Color2 = Color2;
        this.Color1Position = Color1Position;
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        float Opacity = DA.Opacity;
        if (Opacity > 0 && !Opacity.IsAlmostZero())
        {
            DA.DT.FillQuadrilateralLinearClamp(DA.Offset.ToVector2(),
                Bounds.TopLeft().ToVector2(), GetColor(CornerType.TopLeft) * Opacity,
                Bounds.TopRight().ToVector2(), GetColor(CornerType.TopRight) * Opacity,
                Bounds.BottomRight().ToVector2(), GetColor(CornerType.BottomRight) * Opacity,
                Bounds.BottomLeft().ToVector2(), GetColor(CornerType.BottomLeft) * Opacity);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
        => new MGGradientFillBrush(
            GetColor(CornerType.TopLeft),
            GetColor(CornerType.TopRight),
            GetColor(CornerType.BottomRight),
            GetColor(CornerType.BottomLeft)).Draw(DA, Element, Shape, Geometry);

    public IFillBrush Copy() => new MGDiagonalGradientFillBrush(Color1, Color2, Color1Position);
}