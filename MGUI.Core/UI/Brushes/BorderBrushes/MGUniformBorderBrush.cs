using MGUI.Core.UI.Brushes.FillBrushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;

namespace MGUI.Core.UI.Brushes.BorderBrushes;

/// <summary>An <see cref="IBorderBrush"/> that uses the same <see cref="IFillBrush"/> for each side: Left, Top, Right, Bottom<para/>
/// See also: <see cref="MGDockedBorderBrush"/>, <see cref="MGBandedBorderBrush"/>, <see cref="MGTexturedBorderBrush"/>, <see cref="MGHighlightBorderBrush"/>, <see cref="MGCompositedBorderBrush"/></summary>
public sealed class MGUniformBorderBrush : UIFreezableBrush, IBorderBrush
{
    /// <summary>Frozen (ADR-0009, by the static constructor below) shared instances: safe to reference from a theme or a static
    /// resource now that <see cref="MGUniformBorderBrush"/> is mutable.</summary>
    public static readonly MGUniformBorderBrush Transparent = new(SolidFillBrushes.Transparent);
    public static readonly MGUniformBorderBrush White = new(SolidFillBrushes.White);
    public static readonly MGUniformBorderBrush LightGray = new(SolidFillBrushes.LightGray);
    public static readonly MGUniformBorderBrush Gray = new(SolidFillBrushes.Gray);
    public static readonly MGUniformBorderBrush DarkGray = new(SolidFillBrushes.DarkGray);
    public static readonly MGUniformBorderBrush Black = new(SolidFillBrushes.Black);

    static MGUniformBorderBrush()
    {
        Transparent.Freeze();
        White.Freeze();
        LightGray.Freeze();
        Gray.Freeze();
        DarkGray.Freeze();
        Black.Freeze();
    }

    private IFillBrush _Brush;
    /// <summary>Kept read-only (no public setter): the previous <see langword="readonly struct"/> exposed no way to replace the inner
    /// brush after construction, and this design (ADR-0009) does not introduce new public API beyond the freezable contract.</summary>
    public IFillBrush Brush => _Brush ?? SolidFillBrushes.Transparent;

    /// <summary>False when the inner <see cref="Brush"/> cannot itself freeze (ADR-0009).</summary>
    public override bool CanFreeze => _Brush is not IUIFreezable freezable || freezable.CanFreeze;

    /// <summary>Freezes the inner <see cref="Brush"/> (ADR-0009).</summary>
    protected override void OnFreeze()
    {
        if (_Brush is IUIFreezable freezable)
        {
            freezable.Freeze();
        }
    }

    /// <summary>Uses an <see cref="MGSolidFillBrush"/> from the given <paramref name="Color"/> for each side.</summary>
    /// <param name="Color"></param>
    public MGUniformBorderBrush(Color Color)
    {
        _Brush = new MGSolidFillBrush(Color);
    }

    public MGUniformBorderBrush(IFillBrush Brush)
    {
        _Brush = Brush ?? throw new ArgumentNullException(nameof(Brush));
    }

    /// <summary>Forwards the per-frame lifecycle call to the nested <see cref="Brush"/> via <see cref="PaintLifecycle"/>, deduplicated by
    /// reference against every other slot/element that references it for the frame.
    /// Required because <see cref="MGBorder"/> and <see cref="IFillBrush.AsUniformBorderBrush"/> wrap any fill brush (possibly a stateful composite) in this brush.</summary>
    public void Update(UpdateBaseArgs UA) => PaintLifecycle.Update(_Brush, UA);

    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT)
    {
        if (BT.IsEmpty())
        {
            return;
        }

        if (BT.Left > 0)
        {
            Brush.Draw(DA, Element, new(Bounds.Left, Bounds.Top, BT.Left, Bounds.Height));
        }

        if (BT.Right > 0)
        {
            Brush.Draw(DA, Element, new(Bounds.Right - BT.Right, Bounds.Top, BT.Right, Bounds.Height));
        }

        if (BT.Top > 0)
        {
            Brush.Draw(DA, Element, new(Bounds.Left + BT.Left, Bounds.Top, Bounds.Width - BT.Width, BT.Top));
        }

        if (BT.Bottom > 0)
        {
            Brush.Draw(DA, Element, new(Bounds.Left + BT.Left, Bounds.Bottom - BT.Bottom, Bounds.Width - BT.Width, BT.Bottom));
        }
    }

    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        var borderThickness = Shape.NormalizedBorderThickness;
        if (borderThickness.IsEmpty())
        {
            return;
        }

        if (!Shape.HasRoundedCorners || Brush is not MGSolidFillBrush solidFillBrush)
        {
            Draw(DA, Element, Shape.OuterBounds, borderThickness);
            return;
        }

        DA.Context.DrawBorderRing(DA.Offset.ToVector2(), Geometry, solidFillBrush.Color * DA.Opacity);
    }

    public IBorderBrush Copy() => new MGUniformBorderBrush(Brush.Copy());

    /// <summary>Value equality (ADR-0009): forwards to the inner <see cref="Brush"/>'s value equality, so a resolved
    /// <see cref="MGUniformBorderBrush"/> compares equal to another one wrapping an equal-valued fill brush, regardless of frozen state
    /// or instance identity.</summary>
    public bool ValueEquals(IBorderBrush other) => other is MGUniformBorderBrush u && UIBrushEquality.ValueEquals(u.Brush, Brush);

    public override bool Equals(object obj) => ValueEquals(obj as IBorderBrush);
    public override int GetHashCode() => Brush?.GetHashCode() ?? 0;

    public static explicit operator MGUniformBorderBrush(Color color) => new(color);
}