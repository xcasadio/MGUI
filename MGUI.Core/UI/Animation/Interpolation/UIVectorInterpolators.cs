using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Animation.Interpolation
{
    /// <summary>Linear interpolation of <see cref="Vector2"/> (<see cref="Vector2.Lerp(Vector2, Vector2, float)"/>, no clamping).</summary>
    public sealed class UIVector2Interpolator : IUIInterpolator<Vector2>
    {
        public static readonly UIVector2Interpolator Instance = new();

        public Vector2 Lerp(Vector2 from, Vector2 to, float amount) => Vector2.Lerp(from, to, amount);
    }

    /// <summary>Linear interpolation of <see cref="Vector3"/> (no clamping).</summary>
    public sealed class UIVector3Interpolator : IUIInterpolator<Vector3>
    {
        public static readonly UIVector3Interpolator Instance = new();

        public Vector3 Lerp(Vector3 from, Vector3 to, float amount) => Vector3.Lerp(from, to, amount);
    }

    /// <summary>Linear interpolation of <see cref="Vector4"/> (no clamping).</summary>
    public sealed class UIVector4Interpolator : IUIInterpolator<Vector4>
    {
        public static readonly UIVector4Interpolator Instance = new();

        public Vector4 Lerp(Vector4 from, Vector4 to, float amount) => Vector4.Lerp(from, to, amount);
    }

    /// <summary>Interpolation of <see cref="Color"/> through <see cref="Color.Lerp(Color, Color, float)"/>: each byte channel
    /// (including alpha) is interpolated and <c>amount</c> is clamped to [0, 1], since a byte channel cannot represent the
    /// overshoot of an elastic or back easing.</summary>
    public sealed class UIColorInterpolator : IUIInterpolator<Color>
    {
        public static readonly UIColorInterpolator Instance = new();

        public Color Lerp(Color from, Color to, float amount) => Color.Lerp(from, to, amount);
    }
}
