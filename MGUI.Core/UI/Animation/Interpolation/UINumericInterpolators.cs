namespace MGUI.Core.UI.Animation.Interpolation
{
    /// <summary>Linear interpolation of <see cref="float"/>. Does not clamp <c>amount</c>.</summary>
    public sealed class UIFloatInterpolator : IUIInterpolator<float>
    {
        public static readonly UIFloatInterpolator Instance = new();

        public float Lerp(float from, float to, float amount) => from + (to - from) * amount;
    }

    /// <summary>Linear interpolation of <see cref="double"/>. Does not clamp <c>amount</c>.</summary>
    public sealed class UIDoubleInterpolator : IUIInterpolator<double>
    {
        public static readonly UIDoubleInterpolator Instance = new();

        public double Lerp(double from, double to, double amount) => from + (to - from) * amount;

        public double Lerp(double from, double to, float amount) => Lerp(from, to, (double)amount);
    }

    /// <summary>Linear interpolation of <see cref="int"/>, rounded away from zero like
    /// <see cref="Responsive.UIResponsiveMath.ScaleInt"/>. Does not clamp <c>amount</c>.</summary>
    public sealed class UIIntInterpolator : IUIInterpolator<int>
    {
        public static readonly UIIntInterpolator Instance = new();

        public int Lerp(int from, int to, float amount) => UIInterpolationMath.RoundAwayFromZero(from + (to - from) * amount);
    }

    /// <summary>Interpolation of <see cref="Nullable{T}"/> <see cref="int"/> (for example <c>MGElement.MinHeight</c>).
    /// Two non-null values interpolate like <see cref="UIIntInterpolator"/>; when either side is null there is nothing
    /// to interpolate, so the value switches from <c>from</c> to <c>to</c> at the midpoint (<c>amount &gt;= 0.5</c>).</summary>
    public sealed class UINullableIntInterpolator : IUIInterpolator<int?>
    {
        public static readonly UINullableIntInterpolator Instance = new();

        public int? Lerp(int? from, int? to, float amount)
        {
            if (from.HasValue && to.HasValue)
            {
                return UIIntInterpolator.Instance.Lerp(from.Value, to.Value, amount);
            }

            return amount >= 0.5f ? to : from;
        }
    }

    /// <summary>Shared rounding rule of the integer-based interpolators.</summary>
    public static class UIInterpolationMath
    {
        /// <summary>Rounds half away from zero, the rule used by the responsive layout (<see cref="Responsive.UIResponsiveMath.ScaleInt"/>).</summary>
        public static int RoundAwayFromZero(float value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
