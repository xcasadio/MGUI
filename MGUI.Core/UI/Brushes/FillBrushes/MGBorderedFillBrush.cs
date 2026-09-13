using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>An <see cref="IFillBrush"/> that combines the functionality of an <see cref="IFillBrush"/> and an <see cref="IBorderBrush"/></summary>
public class MGBorderedFillBrush : IFillBrush
{
    public Thickness BorderThickness { get; set; }
    public IBorderBrush BorderBrush { get; set; }
    public IFillBrush FillBrush { get; set; }
    /// <summary>If true, <see cref="FillBrush"/> will not be drawn to the entire bounds and will instead be compressed by the <see cref="BorderThickness"/>,<br/>
    /// only filling the portion of the bounds that don't intersect the border.</summary>
    public bool PadFillBoundsByBorderThickness { get; set; }

    /// <param name="PadFillBoundsByBorderThickness">If true, <paramref name="FillBrush"/> will not be drawn to the entire bounds and will instead be compressed by the <paramref name="BorderThickness"/>,<br/>
    /// only filling the portion of the bounds that don't intersect the border.</param>
    public MGBorderedFillBrush(Thickness BorderThickness, IBorderBrush BorderBrush, IFillBrush FillBrush, bool PadFillBoundsByBorderThickness)
    {
        this.BorderThickness = BorderThickness;
        this.BorderBrush = BorderBrush;
        this.FillBrush = FillBrush;
        this.PadFillBoundsByBorderThickness = PadFillBoundsByBorderThickness;
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
}