using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>An <see cref="IFillBrush"/> that combines the functionality of an <see cref="IFillBrush"/> and an <see cref="IBorderBrush"/>.
/// Freezable (ADR-0009, W3): a sealed mutable class deriving from <see cref="UIFreezableBrush"/> whose setters throw once frozen;
/// <see cref="Freeze"/> also freezes <see cref="BorderBrush"/> and <see cref="FillBrush"/>; <see cref="Copy"/> always returns an
/// unfrozen instance with unfrozen deep copies of both.</summary>
public sealed class MGBorderedFillBrush : UIFreezableBrush, IFillBrush
{
    private Thickness _BorderThickness;
    public Thickness BorderThickness
    {
        get => _BorderThickness;
        set => SetProperty(ref _BorderThickness, value);
    }

    private IBorderBrush _BorderBrush;
    /// <summary>Setter uses <see cref="UIBrushEquality.ForSlots{T}"/> (fix round, ADR-0009 W3-fix): see
    /// <see cref="MGUI.Core.UI.Brushes.FillBrushes.MGPaddedFillBrush.Brush"/> for the rationale.</summary>
    public IBorderBrush BorderBrush
    {
        get => _BorderBrush;
        set => SetProperty(ref _BorderBrush, value, UIBrushEquality.ForSlots<IBorderBrush>());
    }

    private IFillBrush _FillBrush;
    /// <summary>Setter uses <see cref="UIBrushEquality.ForSlots{T}"/> (fix round, ADR-0009 W3-fix): see
    /// <see cref="MGUI.Core.UI.Brushes.FillBrushes.MGPaddedFillBrush.Brush"/> for the rationale.</summary>
    public IFillBrush FillBrush
    {
        get => _FillBrush;
        set => SetProperty(ref _FillBrush, value, UIBrushEquality.ForSlots<IFillBrush>());
    }

    private bool _PadFillBoundsByBorderThickness;
    /// <summary>If true, <see cref="FillBrush"/> will not be drawn to the entire bounds and will instead be compressed by the <see cref="BorderThickness"/>,<br/>
    /// only filling the portion of the bounds that don't intersect the border.</summary>
    public bool PadFillBoundsByBorderThickness
    {
        get => _PadFillBoundsByBorderThickness;
        set => SetProperty(ref _PadFillBoundsByBorderThickness, value);
    }

    /// <summary>False when <see cref="BorderBrush"/> or <see cref="FillBrush"/> cannot itself freeze (ADR-0009, W3).</summary>
    public override bool CanFreeze => (_BorderBrush is not IUIFreezable borderFreezable || borderFreezable.CanFreeze)
        && (_FillBrush is not IUIFreezable fillFreezable || fillFreezable.CanFreeze);

    /// <summary>Freezes <see cref="BorderBrush"/> and <see cref="FillBrush"/> (ADR-0009, W3).</summary>
    protected override void OnFreeze()
    {
        if (_BorderBrush is IUIFreezable borderFreezable)
        {
            borderFreezable.Freeze();
        }

        if (_FillBrush is IUIFreezable fillFreezable)
        {
            fillFreezable.Freeze();
        }
    }

    /// <param name="PadFillBoundsByBorderThickness">If true, <paramref name="FillBrush"/> will not be drawn to the entire bounds and will instead be compressed by the <paramref name="BorderThickness"/>,<br/>
    /// only filling the portion of the bounds that don't intersect the border.</param>
    public MGBorderedFillBrush(Thickness BorderThickness, IBorderBrush BorderBrush, IFillBrush FillBrush, bool PadFillBoundsByBorderThickness)
    {
        _BorderThickness = BorderThickness;
        _BorderBrush = BorderBrush;
        _FillBrush = FillBrush;
        _PadFillBoundsByBorderThickness = PadFillBoundsByBorderThickness;
    }

    /// <summary>Forwards the per-frame lifecycle call to <see cref="FillBrush"/> and <see cref="BorderBrush"/> via <see cref="PaintLifecycle"/>,
    /// so that stateful paints such as <see cref="MGHighlightBorderBrush"/> keep animating when nested in this brush,
    /// deduplicated by reference against every other slot/element that references them for the frame.</summary>
    public void Update(UpdateBaseArgs UA)
    {
        PaintLifecycle.Update(FillBrush, UA);
        PaintLifecycle.Update(BorderBrush, UA);
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        var Opacity = DA.Opacity;
        if (Opacity > 0 && !Opacity.IsAlmostZero())
        {
            if (FillBrush != null)
            {
                var FillBounds = PadFillBoundsByBorderThickness ? Bounds.GetCompressed(BorderThickness) : Bounds;
                FillBrush.Draw(DA, Element, FillBounds);
            }

            BorderBrush?.Draw(DA, Element, Bounds, BorderThickness);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        var opacity = DA.Opacity;
        if (opacity <= 0 || opacity.IsAlmostZero())
        {
            return;
        }

        if (FillBrush != null)
        {
            var fillShape = PadFillBoundsByBorderThickness
                ? new MGBoxShape(Shape.InnerBounds, new Thickness(0), Shape.InnerCornerRadius).Normalize()
                : new MGBoxShape(Shape.OuterBounds, new Thickness(0), Shape.NormalizedCornerRadius).Normalize();
            var fillGeometry = MGBoxGeometryBuilder.Build(fillShape, Geometry.CornerSegmentCount);
            FillBrush.Draw(DA, Element, fillGeometry.Shape, fillGeometry);
        }

        if (BorderBrush != null)
        {
            var borderShape = new MGBoxShape(Shape.OuterBounds, BorderThickness, Shape.NormalizedCornerRadius).Normalize();
            var borderGeometry = MGBoxGeometryBuilder.Build(borderShape, Geometry.CornerSegmentCount);
            BorderBrush.Draw(DA, Element, borderGeometry.Shape, borderGeometry);
        }
    }

    public IFillBrush Copy() => new MGBorderedFillBrush(BorderThickness, BorderBrush?.Copy(), FillBrush?.Copy(), PadFillBoundsByBorderThickness);

    /// <summary>Value equality (ADR-0009, W3): two bordered-fill brushes are equal when <see cref="BorderThickness"/>,
    /// <see cref="PadFillBoundsByBorderThickness"/> and the nested <see cref="BorderBrush"/>/<see cref="FillBrush"/> (each by value) all
    /// match, regardless of frozen state or instance identity.</summary>
    public bool ValueEquals(IFillBrush other) => other is MGBorderedFillBrush b
        && b.BorderThickness.Equals(BorderThickness) && b.PadFillBoundsByBorderThickness == PadFillBoundsByBorderThickness
        && UIBrushEquality.ValueEquals(b.BorderBrush, BorderBrush) && UIBrushEquality.ValueEquals(b.FillBrush, FillBrush);

    public override bool Equals(object obj) => ValueEquals(obj as IFillBrush);
    public override int GetHashCode() => HashCode.Combine(BorderThickness, BorderBrush, FillBrush, PadFillBoundsByBorderThickness);
}