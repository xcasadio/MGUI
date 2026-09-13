using MonoGame.Extended;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Core.UI.Animation.Interpolation;

/// <summary>Interpolation of <see cref="Rectangle"/>: position and size are interpolated as floats and rounded
/// away from zero, so an animated rectangle advances in whole pixels. Does not clamp <c>amount</c>.</summary>
public sealed class UIRectangleInterpolator : IUIInterpolator<Rectangle>
{
    public static readonly UIRectangleInterpolator Instance = new();

    public Rectangle Lerp(Rectangle from, Rectangle to, float amount) => new(
        UIInterpolationMath.RoundAwayFromZero(from.X + (to.X - from.X) * amount),
        UIInterpolationMath.RoundAwayFromZero(from.Y + (to.Y - from.Y) * amount),
        UIInterpolationMath.RoundAwayFromZero(from.Width + (to.Width - from.Width) * amount),
        UIInterpolationMath.RoundAwayFromZero(from.Height + (to.Height - from.Height) * amount));
}

/// <summary>Interpolation of <see cref="Thickness"/>: each side is interpolated as a float and rounded away from zero
/// (<see cref="Thickness"/> is integer-based, so an animated margin advances in whole pixels, ADR-0006; this is the runtime <c>MonoGame.Extended.Thickness</c> of <c>MGElement.Margin</c>, not the XAML DTO). Does not clamp <c>amount</c>.</summary>
public sealed class UIThicknessInterpolator : IUIInterpolator<Thickness>
{
    public static readonly UIThicknessInterpolator Instance = new();

    public Thickness Lerp(Thickness from, Thickness to, float amount) => new(
        UIInterpolationMath.RoundAwayFromZero(from.Left + (to.Left - from.Left) * amount),
        UIInterpolationMath.RoundAwayFromZero(from.Top + (to.Top - from.Top) * amount),
        UIInterpolationMath.RoundAwayFromZero(from.Right + (to.Right - from.Right) * amount),
        UIInterpolationMath.RoundAwayFromZero(from.Bottom + (to.Bottom - from.Bottom) * amount));
}