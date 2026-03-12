using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;

namespace MGUI.Core.UI.Responsive
{
    public static class UIResponsiveMath
    {
        public static int ScaleInt(int value, float scale)
            => (int)Math.Round(value * scale, MidpointRounding.AwayFromZero);

        public static int? ScaleNullableInt(int? value, float scale)
            => value.HasValue ? ScaleInt(value.Value, scale) : null;

        public static Thickness ScaleThickness(Thickness value, float scale)
            => new(
                ScaleInt(value.Left, scale),
                ScaleInt(value.Top, scale),
                ScaleInt(value.Right, scale),
                ScaleInt(value.Bottom, scale));

        public static Size ScaleSize(Size value, float scale)
            => new(ScaleInt(value.Width, scale), ScaleInt(value.Height, scale));

        public static float ClampPositive(float value, float min, float max)
            => Math.Clamp(value, min, max);
    }
}