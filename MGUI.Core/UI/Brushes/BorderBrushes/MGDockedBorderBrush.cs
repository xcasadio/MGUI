using MGUI.Core.UI.Brushes.FillBrushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Core.UI.Shapes;

namespace MGUI.Core.UI.Brushes.BorderBrushes;

/// <summary>An <see cref="IBorderBrush"/> that uses separate <see cref="IFillBrush"/>es for each side: Left, Top, Right, Bottom<para/>
/// See also: <see cref="MGUniformBorderBrush"/>, <see cref="MGBandedBorderBrush"/>, <see cref="MGTexturedBorderBrush"/>, <see cref="MGHighlightBorderBrush"/>, <see cref="MGCompositedBorderBrush"/></summary>
public sealed class MGDockedBorderBrush : UIFreezableBrush, IBorderBrush
{
    private IFillBrush _Left;
    /// <summary>Setter uses <see cref="UIBrushEquality.ForSlots{T}"/> (ADR-0009): see
    /// <see cref="MGUI.Core.UI.Brushes.FillBrushes.MGPaddedFillBrush.Brush"/> for the rationale. <see cref="UIFreezableBrush.ThrowIfFrozen"/>
    /// is called explicitly first so a frozen instance reports the frozen error instead of <see cref="ArgumentNullException"/>
    /// when the caller passes <see langword="null"/>.</summary>
    public IFillBrush Left
    {
        get => _Left;
        set { ThrowIfFrozen(); if (SetProperty(ref _Left, value ?? throw new ArgumentNullException(nameof(value)), UIBrushEquality.ForSlots<IFillBrush>())) { RecomputeIsSolidColorsOnly(); } }
    }

    private IFillBrush _Top;
    /// <summary>See <see cref="Left"/> for the setter's rationale.</summary>
    public IFillBrush Top
    {
        get => _Top;
        set { ThrowIfFrozen(); if (SetProperty(ref _Top, value ?? throw new ArgumentNullException(nameof(value)), UIBrushEquality.ForSlots<IFillBrush>())) { RecomputeIsSolidColorsOnly(); } }
    }

    private IFillBrush _Right;
    /// <summary>See <see cref="Left"/> for the setter's rationale.</summary>
    public IFillBrush Right
    {
        get => _Right;
        set { ThrowIfFrozen(); if (SetProperty(ref _Right, value ?? throw new ArgumentNullException(nameof(value)), UIBrushEquality.ForSlots<IFillBrush>())) { RecomputeIsSolidColorsOnly(); } }
    }

    private IFillBrush _Bottom;
    /// <summary>See <see cref="Left"/> for the setter's rationale.</summary>
    public IFillBrush Bottom
    {
        get => _Bottom;
        set { ThrowIfFrozen(); if (SetProperty(ref _Bottom, value ?? throw new ArgumentNullException(nameof(value)), UIBrushEquality.ForSlots<IFillBrush>())) { RecomputeIsSolidColorsOnly(); } }
    }

    /// <summary>Cached flag (ADR-0009: was computed once in the constructor of the previous <see langword="readonly struct"/>, now
    /// recomputed by <see cref="RecomputeIsSolidColorsOnly"/> whenever any side changes) preserving the fast path used by
    /// <see cref="Draw(ElementDrawArgs, MGElement, Rectangle, Thickness)"/> and <see cref="Draw(ElementDrawArgs, MGElement, MGBoxShape, MGBoxGeometry)"/>.</summary>
    private bool IsSolidColorsOnly { get; set; }

    private void RecomputeIsSolidColorsOnly() => IsSolidColorsOnly = _Left is MGSolidFillBrush && _Top is MGSolidFillBrush && _Right is MGSolidFillBrush && _Bottom is MGSolidFillBrush;

    /// <summary>False when any side cannot itself freeze (ADR-0009).</summary>
    public override bool CanFreeze => (_Left is not IUIFreezable l || l.CanFreeze) && (_Top is not IUIFreezable t || t.CanFreeze)
        && (_Right is not IUIFreezable r || r.CanFreeze) && (_Bottom is not IUIFreezable b || b.CanFreeze);

    /// <summary>Freezes every side, deduplicated by reference the same way <see cref="Update"/> is (ADR-0009).</summary>
    protected override void OnFreeze()
    {
        HashSet<IFillBrush> frozen = new(System.Collections.Generic.ReferenceEqualityComparer.Instance);
        foreach (var side in new[] { _Left, _Top, _Right, _Bottom })
        {
            if (side is IUIFreezable freezable && frozen.Add(side))
            {
                freezable.Freeze();
            }
        }
    }

    /// <summary>Uses an <see cref="MGSolidFillBrush"/> from the given Colors for each side.</summary>
    public MGDockedBorderBrush(Color Left, Color Top, Color Right, Color Bottom)
    {
        _Left = new MGSolidFillBrush(Left);
        _Top = new MGSolidFillBrush(Top);
        _Right = new MGSolidFillBrush(Right);
        _Bottom = new MGSolidFillBrush(Bottom);

        IsSolidColorsOnly = true;
    }

    public MGDockedBorderBrush(IFillBrush Left, IFillBrush Top, IFillBrush Right, IFillBrush Bottom)
    {
        _Left = Left ?? throw new ArgumentNullException(nameof(Left));
        _Top = Top ?? throw new ArgumentNullException(nameof(Top));
        _Right = Right ?? throw new ArgumentNullException(nameof(Right));
        _Bottom = Bottom ?? throw new ArgumentNullException(nameof(Bottom));

        RecomputeIsSolidColorsOnly();
    }

    private static IEnumerable<T> AsEnum<T>(params T[] values) => values;

    /// <summary>Forwards the per-frame lifecycle call to <see cref="Left"/>, <see cref="Top"/>, <see cref="Right"/> and <see cref="Bottom"/> via <see cref="PaintLifecycle"/>.
    /// The same instance may be passed for several sides, so instances are deduplicated by reference within this brush (kept regardless of
    /// <see cref="UpdateBaseArgs.PaintRegistry"/>); <see cref="PaintLifecycle"/> additionally dedups against every other slot/element that
    /// references the same instance for the frame.</summary>
    public void Update(UpdateBaseArgs UA)
    {
        IFillBrush left = Left;
        IFillBrush top = Top;
        IFillBrush right = Right;
        IFillBrush bottom = Bottom;

        PaintLifecycle.Update(left, UA);

        if (top != null && !ReferenceEquals(top, left))
        {
            PaintLifecycle.Update(top, UA);
        }

        if (right != null && !ReferenceEquals(right, left) && !ReferenceEquals(right, top))
        {
            PaintLifecycle.Update(right, UA);
        }

        if (bottom != null && !ReferenceEquals(bottom, left) && !ReferenceEquals(bottom, top) && !ReferenceEquals(bottom, right))
        {
            PaintLifecycle.Update(bottom, UA);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT)
    {
        if (BT.IsEmpty())
        {
            return;
        }

        IUIDrawContext DT = DA.DT;
        float Opacity = DA.Opacity;

        if (IsSolidColorsOnly)
        {
            Point TL = Bounds.TopLeft();
            Point TR = Bounds.TopRight();
            Point BL = Bounds.BottomLeft();
            Point BR = Bounds.BottomRight();

            Point InnerTL = TL + new Point(BT.Left, BT.Top);
            Point InnerTR = TR + new Point(-BT.Right, BT.Top);
            Point InnerBL = BL + new Point(BT.Left, -BT.Bottom);
            Point InnerBR = BR + new Point(-BT.Right, -BT.Bottom);

            if (BT.Left > 0)
            {
                Left.Draw(DA, Element, new(Bounds.Left, Bounds.Top + BT.Top, BT.Left, Bounds.Height - BT.Height));
                DT.FillPolygon(DA.Offset.ToVector2(), AsEnum(TL, InnerTL, TL + new Point(0, BT.Top)).Select(x => x.ToVector2()), ((MGSolidFillBrush)Left).Color * Opacity);
                DT.FillPolygon(DA.Offset.ToVector2(), AsEnum(BL, InnerBL, BL + new Point(0, -BT.Bottom)).Select(x => x.ToVector2()), ((MGSolidFillBrush)Left).Color * Opacity);
            }
            if (BT.Top > 0)
            {
                Top.Draw(DA, Element, new(Bounds.Left + BT.Left, Bounds.Top, Bounds.Width - BT.Width, BT.Top));
                DT.FillPolygon(DA.Offset.ToVector2(), AsEnum(TL, InnerTL, TL + new Point(BT.Left, 0)).Select(x => x.ToVector2()), ((MGSolidFillBrush)Top).Color * Opacity);
                DT.FillPolygon(DA.Offset.ToVector2(), AsEnum(TR, InnerTR, TR + new Point(-BT.Right, 0)).Select(x => x.ToVector2()), ((MGSolidFillBrush)Top).Color * Opacity);
            }
            if (BT.Right > 0)
            {
                Right.Draw(DA, Element, new(Bounds.Right - BT.Right, Bounds.Top + BT.Top, BT.Right, Bounds.Height - BT.Height));
                DT.FillPolygon(DA.Offset.ToVector2(), AsEnum(TR, InnerTR, TR + new Point(0, BT.Top)).Select(x => x.ToVector2()), ((MGSolidFillBrush)Right).Color * Opacity);
                DT.FillPolygon(DA.Offset.ToVector2(), AsEnum(BR, InnerBR, BR + new Point(0, -BT.Bottom)).Select(x => x.ToVector2()), ((MGSolidFillBrush)Right).Color * Opacity);
            }
            if (BT.Bottom > 0)
            {
                Bottom.Draw(DA, Element, new(Bounds.Left + BT.Left, Bounds.Bottom - BT.Bottom, Bounds.Width - BT.Width, BT.Bottom));
                DT.FillPolygon(DA.Offset.ToVector2(), AsEnum(BL, InnerBL, BL + new Point(BT.Left, 0)).Select(x => x.ToVector2()), ((MGSolidFillBrush)Bottom).Color * Opacity);
                DT.FillPolygon(DA.Offset.ToVector2(), AsEnum(BR, InnerBR, BR + new Point(-BT.Right, 0)).Select(x => x.ToVector2()), ((MGSolidFillBrush)Bottom).Color * Opacity);
            }
        }
        else
        {
            if (BT.Left > 0)
            {
                Left.Draw(DA, Element, new(Bounds.Left, Bounds.Top, BT.Left, Bounds.Height));
            }

            if (BT.Right > 0)
            {
                Right.Draw(DA, Element, new(Bounds.Right - BT.Right, Bounds.Top, BT.Right, Bounds.Height));
            }

            if (BT.Top > 0)
            {
                Top.Draw(DA, Element, new(Bounds.Left, Bounds.Top, Bounds.Width, BT.Top));
            }

            if (BT.Bottom > 0)
            {
                Bottom.Draw(DA, Element, new(Bounds.Left, Bounds.Bottom - BT.Bottom, Bounds.Width, BT.Bottom));
            }
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        Thickness borderThickness = Shape.NormalizedBorderThickness;
        if (borderThickness.IsEmpty())
        {
            return;
        }

        if (!Shape.HasRoundedCorners)
        {
            Draw(DA, Element, Shape.OuterBounds, borderThickness);
            return;
        }

        if (!IsSolidColorsOnly)
        {
            Draw(DA, Element, Shape.OuterBounds, borderThickness);
            return;
        }

        Rectangle outerBounds = Shape.OuterBounds;
        Rectangle innerBounds = Shape.InnerBounds;
        Vector2 origin = DA.Offset.ToVector2();
        for (int i = 0; i + 2 < Geometry.BorderRingIndices.Count; i += 3)
        {
            Vector2 v0 = Geometry.Vertices[Geometry.BorderRingIndices[i]];
            Vector2 v1 = Geometry.Vertices[Geometry.BorderRingIndices[i + 1]];
            Vector2 v2 = Geometry.Vertices[Geometry.BorderRingIndices[i + 2]];

            DA.DT.FillTriangle(
                origin,
                v0, GetVertexColor(v0, outerBounds, innerBounds) * DA.Opacity,
                v1, GetVertexColor(v1, outerBounds, innerBounds) * DA.Opacity,
                v2, GetVertexColor(v2, outerBounds, innerBounds) * DA.Opacity);
        }
    }

    private Color GetVertexColor(Vector2 vertex, Rectangle outerBounds, Rectangle innerBounds)
    {
        float leftDistance = MathF.Min(Math.Abs(vertex.X - outerBounds.Left), Math.Abs(vertex.X - innerBounds.Left));
        float topDistance = MathF.Min(Math.Abs(vertex.Y - outerBounds.Top), Math.Abs(vertex.Y - innerBounds.Top));
        float rightDistance = MathF.Min(Math.Abs(vertex.X - outerBounds.Right), Math.Abs(vertex.X - innerBounds.Right));
        float bottomDistance = MathF.Min(Math.Abs(vertex.Y - outerBounds.Bottom), Math.Abs(vertex.Y - innerBounds.Bottom));

        float minimumDistance = MathF.Min(MathF.Min(leftDistance, topDistance), MathF.Min(rightDistance, bottomDistance));
        if (minimumDistance == leftDistance)
        {
            return ((MGSolidFillBrush)Left).Color;
        }
        else if (minimumDistance == topDistance)
        {
            return ((MGSolidFillBrush)Top).Color;
        }
        else if (minimumDistance == rightDistance)
        {
            return ((MGSolidFillBrush)Right).Color;
        }
        else
        {
            return ((MGSolidFillBrush)Bottom).Color;
        }
    }

    public IBorderBrush Copy() => new MGDockedBorderBrush(
        Left?.Copy() ?? SolidFillBrushes.Transparent, 
        Top?.Copy() ?? SolidFillBrushes.Transparent, 
        Right?.Copy() ?? SolidFillBrushes.Transparent, 
        Bottom?.Copy() ?? SolidFillBrushes.Transparent);

    public static explicit operator MGDockedBorderBrush(MGUniformBorderBrush uniform) => new(uniform.Brush, uniform.Brush, uniform.Brush, uniform.Brush);

    /// <summary>Value equality (ADR-0009): two docked border brushes are equal when all four sides are equal by value
    /// (<see cref="UIBrushEquality.ValueEquals(IFillBrush, IFillBrush)"/>), regardless of frozen state or instance identity.</summary>
    public bool ValueEquals(IBorderBrush other) => other is MGDockedBorderBrush d
        && UIBrushEquality.ValueEquals(d.Left, Left) && UIBrushEquality.ValueEquals(d.Top, Top)
        && UIBrushEquality.ValueEquals(d.Right, Right) && UIBrushEquality.ValueEquals(d.Bottom, Bottom);

    public override bool Equals(object obj) => ValueEquals(obj as IBorderBrush);
    public override int GetHashCode() => HashCode.Combine(Left, Top, Right, Bottom);
}