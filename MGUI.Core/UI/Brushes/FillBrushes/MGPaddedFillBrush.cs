using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Brushes.FillBrushes;

/// <summary>A wrapper class that allows you to manipulate the rectangular region that the nested <see cref="IFillBrush"/> is applied to.
/// Freezable (ADR-0009, W3): a sealed mutable class deriving from <see cref="UIFreezableBrush"/> whose setters throw once frozen;
/// <see cref="Freeze"/> also freezes the nested <see cref="Brush"/>; <see cref="Copy"/> always returns an unfrozen instance with an
/// unfrozen deep copy of <see cref="Brush"/>.</summary>
public sealed class MGPaddedFillBrush : UIFreezableBrush, IFillBrush
{
    private IFillBrush _Brush;
    /// <summary>Setter uses <see cref="UIBrushEquality.ForSlots{T}"/> (fix round, ADR-0009 W3-fix), not
    /// <see cref="EqualityComparer{T}.Default"/>: a distinct-but-value-equal <see cref="IFillBrush"/> is still a real change, since the
    /// caller may mutate that distinct instance afterwards (an element-owned brush, W4).</summary>
    public IFillBrush Brush
    {
        get => _Brush;
        set => SetProperty(ref _Brush, value, UIBrushEquality.ForSlots<IFillBrush>());
    }

    private Thickness _Padding;
    public Thickness Padding
    {
        get => _Padding;
        set => SetProperty(ref _Padding, value);
    }

    private float? _Scale;
    public float? Scale
    {
        get => _Scale;
        set => SetProperty(ref _Scale, value);
    }

    private int? _MinWidth;
    public int? MinWidth
    {
        get => _MinWidth;
        set => SetProperty(ref _MinWidth, value);
    }

    private int? _MinHeight;
    public int? MinHeight
    {
        get => _MinHeight;
        set => SetProperty(ref _MinHeight, value);
    }

    private int? _MaxWidth;
    public int? MaxWidth
    {
        get => _MaxWidth;
        set => SetProperty(ref _MaxWidth, value);
    }

    private int? _MaxHeight;
    public int? MaxHeight
    {
        get => _MaxHeight;
        set => SetProperty(ref _MaxHeight, value);
    }

    private HorizontalAlignment? _HorizontalAlignment;
    public HorizontalAlignment? HorizontalAlignment
    {
        get => _HorizontalAlignment;
        set => SetProperty(ref _HorizontalAlignment, value);
    }

    private VerticalAlignment? _VerticalAlignment;
    public VerticalAlignment? VerticalAlignment
    {
        get => _VerticalAlignment;
        set => SetProperty(ref _VerticalAlignment, value);
    }

    /// <summary>False when the nested <see cref="Brush"/> cannot itself freeze (ADR-0009, W3).</summary>
    public override bool CanFreeze => _Brush is not IUIFreezable freezable || freezable.CanFreeze;

    /// <summary>Freezes the nested <see cref="Brush"/> (ADR-0009, W3).</summary>
    protected override void OnFreeze()
    {
        if (_Brush is IUIFreezable freezable)
        {
            freezable.Freeze();
        }
    }

    /// <summary>Forwards the per-frame lifecycle call to the nested <see cref="Brush"/> via <see cref="PaintLifecycle"/>,
    /// deduplicated by reference against every other slot/element that references it for the frame.</summary>
    public void Update(UpdateBaseArgs UA) => PaintLifecycle.Update(Brush, UA);

    public MGPaddedFillBrush(IFillBrush Brush, Thickness Padding, float? Scale = null, int? MinWidth = null, int? MinHeight = null, int? MaxWidth = null, int? MaxHeight = null,
        HorizontalAlignment? HorizontalAlignment = null, VerticalAlignment? VerticalAlignment = null)
    {
        _Brush = Brush;
        _Padding = Padding;

        _Scale = Scale;

        _MinWidth = MinWidth;
        _MinHeight = MinHeight;
        _MaxWidth = MaxWidth;
        _MaxHeight = MaxHeight;

        _HorizontalAlignment = HorizontalAlignment;
        _VerticalAlignment = VerticalAlignment;
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

    /// <summary>Value equality (ADR-0009, W3): two padded brushes are equal when the nested <see cref="Brush"/> is equal by value
    /// (<see cref="UIBrushEquality.ValueEquals(IFillBrush, IFillBrush)"/>) and every scalar matches, regardless of frozen state or
    /// instance identity.</summary>
    public bool ValueEquals(IFillBrush other) => other is MGPaddedFillBrush p
        && UIBrushEquality.ValueEquals(p.Brush, Brush) && p.Padding.Equals(Padding) && p.Scale == Scale
        && p.MinWidth == MinWidth && p.MinHeight == MinHeight && p.MaxWidth == MaxWidth && p.MaxHeight == MaxHeight
        && p.HorizontalAlignment == HorizontalAlignment && p.VerticalAlignment == VerticalAlignment;

    public override bool Equals(object obj) => ValueEquals(obj as IFillBrush);
    public override int GetHashCode() => HashCode.Combine(Brush, Padding, Scale, HashCode.Combine(MinWidth, MinHeight, MaxWidth, MaxHeight), HorizontalAlignment, VerticalAlignment);
}