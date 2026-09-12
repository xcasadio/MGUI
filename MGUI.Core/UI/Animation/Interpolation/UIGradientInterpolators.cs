using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Animation.Interpolation
{
    /// <summary>The four corner colours of an <c>MGGradientFillBrush</c>, the value type of the <c>Background.Gradient</c> animation target (ADR-0007, decision 8).</summary>
    public readonly record struct UIGradientColors(Color TopLeft, Color TopRight, Color BottomRight, Color BottomLeft);

    /// <summary>The two colours and the corner of the first one of an <c>MGDiagonalGradientFillBrush</c>, the value type of the
    /// <c>Background.DiagonalGradient</c> animation target (ADR-0007, decision 8).</summary>
    public readonly record struct UIDiagonalGradientColors(Color Color1, Color Color2, CornerType Color1Position);

    /// <summary>Interpolates the four corners colour by colour (<see cref="Color.Lerp(Color, Color, float)"/>, amount clamped to [0, 1]).</summary>
    public sealed class UIGradientColorsInterpolator : IUIInterpolator<UIGradientColors>
    {
        public static readonly UIGradientColorsInterpolator Instance = new();

        public UIGradientColors Lerp(UIGradientColors from, UIGradientColors to, float amount) => new(
            Color.Lerp(from.TopLeft, to.TopLeft, amount),
            Color.Lerp(from.TopRight, to.TopRight, amount),
            Color.Lerp(from.BottomRight, to.BottomRight, amount),
            Color.Lerp(from.BottomLeft, to.BottomLeft, amount));
    }

    /// <summary>Interpolates the two colours; the corner switches from <c>from</c> to <c>to</c> at the midpoint when they differ.</summary>
    public sealed class UIDiagonalGradientColorsInterpolator : IUIInterpolator<UIDiagonalGradientColors>
    {
        public static readonly UIDiagonalGradientColorsInterpolator Instance = new();

        public UIDiagonalGradientColors Lerp(UIDiagonalGradientColors from, UIDiagonalGradientColors to, float amount) => new(
            Color.Lerp(from.Color1, to.Color1, amount),
            Color.Lerp(from.Color2, to.Color2, amount),
            amount >= 0.5f ? to.Color1Position : from.Color1Position);
    }
}
