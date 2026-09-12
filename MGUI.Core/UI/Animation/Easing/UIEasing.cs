using System.Collections.Concurrent;

namespace MGUI.Core.UI.Animation.Easing
{
    /// <summary>
    /// Built-in easing functions of the animation engine (formulas of easings.net, the same family as WPF's and NoesisGUI's
    /// <c>QuadraticEase</c>, <c>CubicEase</c>, <c>SineEase</c>, <c>BackEase</c>, <c>BounceEase</c>, <c>ElasticEase</c>) and the
    /// name registry used by the XAML layer (<c>Easing="CubicOut"</c>).<para/>
    /// Names are matched case-insensitively by <see cref="TryGet"/>. An application registers its own named function with
    /// <see cref="Register"/> (the last registration for a name wins).
    /// </summary>
    public static class UIEasing
    {
        public static readonly IUIEasingFunction Linear = new DelegateEasing(nameof(Linear), static t => t);

        public static readonly IUIEasingFunction QuadIn = new DelegateEasing(nameof(QuadIn), static t => t * t);
        public static readonly IUIEasingFunction QuadOut = new DelegateEasing(nameof(QuadOut), static t => 1f - (1f - t) * (1f - t));
        public static readonly IUIEasingFunction QuadInOut = new DelegateEasing(nameof(QuadInOut), static t =>
            t < 0.5f ? 2f * t * t : 1f - (-2f * t + 2f) * (-2f * t + 2f) / 2f);

        public static readonly IUIEasingFunction CubicIn = new DelegateEasing(nameof(CubicIn), static t => t * t * t);
        public static readonly IUIEasingFunction CubicOut = new DelegateEasing(nameof(CubicOut), static t => 1f - (1f - t) * (1f - t) * (1f - t));
        public static readonly IUIEasingFunction CubicInOut = new DelegateEasing(nameof(CubicInOut), static t =>
            t < 0.5f ? 4f * t * t * t : 1f - (-2f * t + 2f) * (-2f * t + 2f) * (-2f * t + 2f) / 2f);

        public static readonly IUIEasingFunction SineIn = new DelegateEasing(nameof(SineIn), static t => 1f - MathF.Cos(t * MathF.PI / 2f));
        public static readonly IUIEasingFunction SineOut = new DelegateEasing(nameof(SineOut), static t => MathF.Sin(t * MathF.PI / 2f));
        public static readonly IUIEasingFunction SineInOut = new DelegateEasing(nameof(SineInOut), static t => -(MathF.Cos(MathF.PI * t) - 1f) / 2f);

        private const float BackOvershoot = 1.70158f;
        private const float BackOvershootPlusOne = BackOvershoot + 1f;
        private const float BackInOutOvershoot = BackOvershoot * 1.525f;

        public static readonly IUIEasingFunction BackIn = new DelegateEasing(nameof(BackIn), static t =>
            BackOvershootPlusOne * t * t * t - BackOvershoot * t * t);
        public static readonly IUIEasingFunction BackOut = new DelegateEasing(nameof(BackOut), static t =>
            1f + BackOvershootPlusOne * (t - 1f) * (t - 1f) * (t - 1f) + BackOvershoot * (t - 1f) * (t - 1f));
        public static readonly IUIEasingFunction BackInOut = new DelegateEasing(nameof(BackInOut), static t =>
            t < 0.5f
                ? (2f * t) * (2f * t) * ((BackInOutOvershoot + 1f) * 2f * t - BackInOutOvershoot) / 2f
                : ((2f * t - 2f) * (2f * t - 2f) * ((BackInOutOvershoot + 1f) * (2f * t - 2f) + BackInOutOvershoot) + 2f) / 2f);

        public static readonly IUIEasingFunction BounceIn = new DelegateEasing(nameof(BounceIn), static t => 1f - BounceOutCore(1f - t));
        public static readonly IUIEasingFunction BounceOut = new DelegateEasing(nameof(BounceOut), BounceOutCore);
        public static readonly IUIEasingFunction BounceInOut = new DelegateEasing(nameof(BounceInOut), static t =>
            t < 0.5f ? (1f - BounceOutCore(1f - 2f * t)) / 2f : (1f + BounceOutCore(2f * t - 1f)) / 2f);

        private const float ElasticPeriod = 2f * MathF.PI / 3f;
        private const float ElasticInOutPeriod = 2f * MathF.PI / 4.5f;

        public static readonly IUIEasingFunction ElasticIn = new DelegateEasing(nameof(ElasticIn), static t =>
            t == 0f ? 0f : t == 1f ? 1f : -MathF.Pow(2f, 10f * t - 10f) * MathF.Sin((10f * t - 10.75f) * ElasticPeriod));
        public static readonly IUIEasingFunction ElasticOut = new DelegateEasing(nameof(ElasticOut), static t =>
            t == 0f ? 0f : t == 1f ? 1f : MathF.Pow(2f, -10f * t) * MathF.Sin((10f * t - 0.75f) * ElasticPeriod) + 1f);
        public static readonly IUIEasingFunction ElasticInOut = new DelegateEasing(nameof(ElasticInOut), static t =>
            t == 0f ? 0f : t == 1f ? 1f : t < 0.5f
                ? -(MathF.Pow(2f, 20f * t - 10f) * MathF.Sin((20f * t - 11.125f) * ElasticInOutPeriod)) / 2f
                : MathF.Pow(2f, -20f * t + 10f) * MathF.Sin((20f * t - 11.125f) * ElasticInOutPeriod) / 2f + 1f);

        private static readonly ConcurrentDictionary<string, IUIEasingFunction> Registry = new(StringComparer.OrdinalIgnoreCase);

        static UIEasing()
        {
            foreach (IUIEasingFunction function in new[]
            {
                Linear,
                QuadIn, QuadOut, QuadInOut,
                CubicIn, CubicOut, CubicInOut,
                SineIn, SineOut, SineInOut,
                BackIn, BackOut, BackInOut,
                BounceIn, BounceOut, BounceInOut,
                ElasticIn, ElasticOut, ElasticInOut,
            })
            {
                Register(((DelegateEasing)function).Name, function);
            }
        }

        /// <summary>The registered names, built-ins first in declaration order, then application registrations.</summary>
        public static IReadOnlyCollection<string> Names => Registry.Keys.ToArray();

        /// <summary>Registers (or replaces) a named easing function, for <see cref="TryGet"/> and the XAML layer.</summary>
        public static void Register(string name, IUIEasingFunction function)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("An easing name is required.", nameof(name));
            }

            if (function == null)
            {
                throw new ArgumentNullException(nameof(function));
            }

            Registry[name] = function;
        }

        /// <summary>Retrieves an easing function by name (case-insensitive), or false when the name is unknown.</summary>
        public static bool TryGet(string name, out IUIEasingFunction function)
        {
            if (name != null && Registry.TryGetValue(name, out function))
            {
                return true;
            }

            function = null;
            return false;
        }

        private static float BounceOutCore(float t)
        {
            const float n1 = 7.5625f;
            const float d1 = 2.75f;

            if (t < 1f / d1)
            {
                return n1 * t * t;
            }
            else if (t < 2f / d1)
            {
                t -= 1.5f / d1;
                return n1 * t * t + 0.75f;
            }
            else if (t < 2.5f / d1)
            {
                t -= 2.25f / d1;
                return n1 * t * t + 0.9375f;
            }
            else
            {
                t -= 2.625f / d1;
                return n1 * t * t + 0.984375f;
            }
        }

        /// <summary>A named, stateless easing over a static lambda: one indirect call per <see cref="Ease"/>, no allocation.</summary>
        private sealed class DelegateEasing : IUIEasingFunction
        {
            private readonly Func<float, float> _function;

            public DelegateEasing(string name, Func<float, float> function)
            {
                Name = name;
                _function = function;
            }

            public string Name { get; }

            public float Ease(float amount) => _function(amount);

            public override string ToString() => Name;
        }
    }
}
