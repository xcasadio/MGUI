using MGUI.Core.UI.Brushes.FillBrushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Rendering;
using MGUI.Core.UI.Shapes;

namespace MGUI.Core.UI.Brushes.BorderBrushes;

/// <summary>See also:<br/><see cref="MGUniformBorderBrush"/><br/><see cref="MGDockedBorderBrush"/><br/><see cref="MGBandedBorderBrush"/><br/><see cref="MGTexturedBorderBrush"/><br/><see cref="MGHighlightBorderBrush"/><br/><see cref="MGCompositedFillBrush"/></summary>
public interface IBorderBrush : ICloneable, IUIFreezable
{
    /// <summary>Transitional default implementation (ADR-0009): a value brush that is still a <see langword="readonly struct"/> today
    /// cannot mutate in place, so it is always frozen. Every concrete <see cref="IBorderBrush"/> overrides this once it derives from
    /// <see cref="UIFreezableBrush"/>.</summary>
    bool IUIFreezable.IsFrozen => true;

    /// <summary>Transitional default implementation (ADR-0009). See <see cref="IUIFreezable.IsFrozen"/>.</summary>
    bool IUIFreezable.CanFreeze => true;

    /// <summary>Transitional default implementation (ADR-0009): a no-op, since a brush that has not yet adopted
    /// <see cref="UIFreezableBrush"/> is always already frozen.</summary>
    void IUIFreezable.Freeze() { }

    /// <summary>Value equality used by the guards in <see cref="UIBrushEquality"/> (ADR-0009). Transitional default implementation
    /// defers to <see cref="object.Equals(object)"/>: memberwise comparison for a value <see langword="struct"/> brush today (correctly
    /// recursive through any nested <see cref="IFillBrush"/>/<see cref="IBorderBrush"/> field), reference comparison for the mutable
    /// composite/highlight classes. Every concrete brush overrides this once it derives from <see cref="UIFreezableBrush"/>.</summary>
    bool ValueEquals(IBorderBrush other) => Equals(other);

    public void Update(UpdateBaseArgs UA) { }
    public void Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT);
    public void Draw(ElementDrawArgs DA, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
        => Draw(DA, Element, Shape.OuterBounds, Shape.NormalizedBorderThickness);

    public IBorderBrush Copy();
    object ICloneable.Clone() => Copy();
}