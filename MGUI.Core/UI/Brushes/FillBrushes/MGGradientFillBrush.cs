using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>An <see cref="IFillBrush"/> that fills its bounds with a gradient. Each corner has a specified <see cref="Color"/> that the gradient linearly interpolates to.
/// Freezable (ADR-0009, W2): a sealed mutable class deriving from <see cref="UIFreezableBrush"/> whose corner setters throw once frozen; <see cref="Copy"/> always
/// returns an unfrozen instance.<para/>
/// For a simpler version, use <see cref="MGDiagonalGradientFillBrush"/></summary>
public sealed class MGGradientFillBrush : UIFreezableBrush, IFillBrush
{
    private Color _TopLeftColor;
    public Color TopLeftColor
    {
        get => _TopLeftColor;
        set => SetProperty(ref _TopLeftColor, value);
    }

    private Color _TopRightColor;
    public Color TopRightColor
    {
        get => _TopRightColor;
        set => SetProperty(ref _TopRightColor, value);
    }

    private Color _BottomLeftColor;
    public Color BottomLeftColor
    {
        get => _BottomLeftColor;
        set => SetProperty(ref _BottomLeftColor, value);
    }

    private Color _BottomRightColor;
    public Color BottomRightColor
    {
        get => _BottomRightColor;
        set => SetProperty(ref _BottomRightColor, value);
    }

    public MGGradientFillBrush(Color TopLeft, Color TopRight, Color BottomRight, Color BottomLeft)
    {
        _TopLeftColor = TopLeft;
        _TopRightColor = TopRight;
        _BottomLeftColor = BottomLeft;
        _BottomRightColor = BottomRight;
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        var opacity = DA.Opacity;
        if (opacity > 0 && !opacity.IsAlmostZero())
        {
            DA.DT.FillQuadrilateralLinearClamp(DA.Offset.ToVector2(),
                Bounds.TopLeft().ToVector2(), TopLeftColor * opacity,
                Bounds.TopRight().ToVector2(), TopRightColor * opacity,
                Bounds.BottomRight().ToVector2(), BottomRightColor * opacity,
                Bounds.BottomLeft().ToVector2(), BottomLeftColor * opacity);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        var opacity = DA.Opacity;
        if (opacity <= 0 || opacity.IsAlmostZero())
        {
            return;
        }

        if (Geometry.UsesRectangleFastPath)
        {
            Draw(DA, Element, Shape.OuterBounds);
            return;
        }

        var origin = DA.Offset.ToVector2();
        var bounds = Shape.OuterBounds;
        for (var i = 0; i + 2 < Geometry.FillIndices.Count; i += 3)
        {
            var v0 = Geometry.Vertices[Geometry.FillIndices[i]];
            var v1 = Geometry.Vertices[Geometry.FillIndices[i + 1]];
            var v2 = Geometry.Vertices[Geometry.FillIndices[i + 2]];

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

        var horizontal = Math.Clamp((point.X - bounds.Left) / bounds.Width, 0f, 1f);
        var vertical = Math.Clamp((point.Y - bounds.Top) / bounds.Height, 0f, 1f);

        var top = Color.Lerp(TopLeftColor, TopRightColor, horizontal);
        var bottom = Color.Lerp(BottomLeftColor, BottomRightColor, horizontal);
        return Color.Lerp(top, bottom, vertical);
    }

    public IFillBrush Copy() => new MGGradientFillBrush(TopLeftColor, TopRightColor, BottomRightColor, BottomLeftColor);

    /// <summary>Value equality (ADR-0009, W2): two gradient brushes are equal when their four corner colours match, regardless of frozen state
    /// or instance identity.</summary>
    public bool ValueEquals(IFillBrush other) => other is MGGradientFillBrush g
        && g.TopLeftColor == TopLeftColor && g.TopRightColor == TopRightColor
        && g.BottomLeftColor == BottomLeftColor && g.BottomRightColor == BottomRightColor;

    /// <summary>Decision taken during delivery (ADR-0009, W2): see <see cref="MGSolidFillBrush.Equals(object)"/> for the rationale
    /// (by-value <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/>, applied consistently to every converted fill brush).</summary>
    public override bool Equals(object obj) => ValueEquals(obj as IFillBrush);
    public override int GetHashCode() => HashCode.Combine(TopLeftColor, TopRightColor, BottomLeftColor, BottomRightColor);
}

/// <summary>A simplified version of <see cref="MGGradientFillBrush"/> that only requires 2 <see cref="Color"/>s for opposite corners of the bounds.
/// Freezable (ADR-0009, W2): a sealed mutable class deriving from <see cref="UIFreezableBrush"/> whose setters throw once frozen; <see cref="Copy"/>
/// always returns an unfrozen instance.</summary>
public sealed class MGDiagonalGradientFillBrush : UIFreezableBrush, IFillBrush
{
    private Color _Color1;
    public Color Color1
    {
        get => _Color1;
        set => SetProperty(ref _Color1, value);
    }

    private Color _Color2;
    public Color Color2
    {
        get => _Color2;
        set => SetProperty(ref _Color2, value);
    }

    private CornerType _Color1Position;
    public CornerType Color1Position
    {
        get => _Color1Position;
        set => SetProperty(ref _Color1Position, value);
    }

    public CornerType Color2Position => OppositeCorners[Color1Position];

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
        _Color1 = Color1;
        _Color2 = Color2;
        _Color1Position = Color1Position;
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        var Opacity = DA.Opacity;
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

    /// <summary>Value equality (ADR-0009, W2): two diagonal-gradient brushes are equal when <see cref="Color1"/>, <see cref="Color2"/> and
    /// <see cref="Color1Position"/> match, regardless of frozen state or instance identity.</summary>
    public bool ValueEquals(IFillBrush other) => other is MGDiagonalGradientFillBrush d
        && d.Color1 == Color1 && d.Color2 == Color2 && d.Color1Position == Color1Position;

    /// <summary>Decision taken during delivery (ADR-0009, W2): see <see cref="MGSolidFillBrush.Equals(object)"/> for the rationale
    /// (by-value <see cref="object.Equals(object)"/>/<see cref="GetHashCode"/>, applied consistently to every converted fill brush).</summary>
    public override bool Equals(object obj) => ValueEquals(obj as IFillBrush);
    public override int GetHashCode() => HashCode.Combine(Color1, Color2, Color1Position);
}