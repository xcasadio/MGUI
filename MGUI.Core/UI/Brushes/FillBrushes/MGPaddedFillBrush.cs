using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>A wrapper class that allows you to manipulate the rectangular region that the nested <see cref="IFillBrush"/> is applied to.</summary>
public class MGPaddedFillBrush : IFillBrush
{
    public IFillBrush Brush { get; set; }
    public Thickness Padding { get; set; }

    public float? Scale { get; set; }

    public int? MinWidth { get; set; }
    public int? MinHeight { get; set; }
    public int? MaxWidth { get; set; }
    public int? MaxHeight { get; set; }

    public HorizontalAlignment? HorizontalAlignment { get; set; }
    public VerticalAlignment? VerticalAlignment { get; set; }

    /// <summary>Forwards the per-frame lifecycle call to the nested <see cref="Brush"/> via <see cref="PaintLifecycle"/>,
    /// deduplicated by reference against every other slot/element that references it for the frame.</summary>
    public void Update(UpdateBaseArgs UA) => PaintLifecycle.Update(Brush, UA);

    public MGPaddedFillBrush(IFillBrush Brush, Thickness Padding, float? Scale = null, int? MinWidth = null, int? MinHeight = null, int? MaxWidth = null, int? MaxHeight = null,
        HorizontalAlignment? HorizontalAlignment = null, VerticalAlignment? VerticalAlignment = null)
    {
        this.Brush = Brush;
        this.Padding = Padding;

        this.Scale = Scale;

        this.MinWidth = MinWidth;
        this.MinHeight = MinHeight;
        this.MaxWidth = MaxWidth;
        this.MaxHeight = MaxHeight;

        this.HorizontalAlignment = HorizontalAlignment;
        this.VerticalAlignment = VerticalAlignment;
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)
    {
        var Opacity = DA.Opacity;
        if (Opacity > 0 && !Opacity.IsAlmostZero() && Brush != null)
        {
            var PaddedBounds = Bounds.GetCompressed(Padding);

            if (Scale.HasValue)
            {
                PaddedBounds = PaddedBounds.GetScaledFromCenter(Scale.Value);
            }

            var DesiredWidth = Math.Clamp(PaddedBounds.Width, MinWidth ?? 0, MaxWidth ?? int.MaxValue);
            var DesiredHeight = Math.Clamp(PaddedBounds.Height, MinHeight ?? 0, MaxHeight ?? int.MaxValue);

            //Rectangle ClampedBounds = new(
            //    PaddedBounds.Left - (DesiredWidth - PaddedBounds.Width) / 2, 
            //    PaddedBounds.Top - (DesiredHeight - PaddedBounds.Height) / 2, 
            //    DesiredWidth, DesiredHeight);

            var ActualBounds = MGElement.ApplyAlignment(PaddedBounds, HorizontalAlignment ?? UI.HorizontalAlignment.Stretch, VerticalAlignment ?? UI.VerticalAlignment.Stretch, new Size(DesiredWidth, DesiredHeight));

            Brush.Draw(DA, Element, ActualBounds);
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        var opacity = DA.Opacity;
        if (opacity <= 0 || opacity.IsAlmostZero() || Brush == null)
        {
            return;
        }

        var paddedBounds = Shape.OuterBounds.GetCompressed(Padding);

        if (Scale.HasValue)
        {
            paddedBounds = paddedBounds.GetScaledFromCenter(Scale.Value);
        }

        var desiredWidth = Math.Clamp(paddedBounds.Width, MinWidth ?? 0, MaxWidth ?? int.MaxValue);
        var desiredHeight = Math.Clamp(paddedBounds.Height, MinHeight ?? 0, MaxHeight ?? int.MaxValue);
        var actualBounds = MGElement.ApplyAlignment(
            paddedBounds,
            HorizontalAlignment ?? UI.HorizontalAlignment.Stretch,
            VerticalAlignment ?? UI.VerticalAlignment.Stretch,
            new Size(desiredWidth, desiredHeight));

        var paddedShape = new MGBoxShape(actualBounds, new Thickness(0), Shape.NormalizedCornerRadius).Normalize();
        var paddedGeometry = MGBoxGeometryBuilder.Build(paddedShape, Geometry.CornerSegmentCount);
        Brush.Draw(DA, Element, paddedGeometry.Shape, paddedGeometry);
    }

    public IFillBrush Copy() => new MGPaddedFillBrush(Brush?.Copy(), Padding, Scale, MinWidth, MinHeight, MaxWidth, MaxHeight, HorizontalAlignment, VerticalAlignment);
}