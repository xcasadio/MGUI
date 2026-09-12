using System.Globalization;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;

namespace MGUI.Core.UI.XAML
{
    /// <summary>
    /// XAML declaration of a <see cref="UITransition"/> (S7; ADR-0006), inside <c>&lt;Button.Transitions&gt;</c>:
    /// <code>
    /// &lt;Transition Property="Background" Duration="0.15" Easing="CubicOut" /&gt;
    /// &lt;Transition Property="Opacity" Duration="200ms" Delay="0:0:0.05" /&gt;
    /// </code>
    /// <see cref="Property"/> must be a registered animation path (<see cref="UIAnimationTargets.Paths"/>), <see cref="Duration"/> and
    /// <see cref="Delay"/> accept seconds (<c>0.15</c>), milliseconds (<c>150ms</c>) or the <see cref="TimeSpan"/> format (<c>0:0:0.15</c>),
    /// <see cref="Easing"/> is a name known to <see cref="UIEasing"/>. Each setter validates its value, so an error surfaces as a loader diagnostic
    /// at parse time.
    /// </summary>
    public class Transition
    {
        private string _Property;
        public string Property
        {
            get => _Property;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException("A Transition needs a Property path.");
                }

                if (!UIAnimationTargets.IsRegistered(value))
                {
                    throw new InvalidOperationException(
                        $"Cannot convert '{value}' to an animation transition property: unknown path. Registered paths: {string.Join(", ", UIAnimationTargets.Paths)}.");
                }

                _Property = value;
            }
        }

        private string _Duration;
        public string Duration
        {
            get => _Duration;
            set
            {
                if (!AnimationXamlParser.TryParseDuration(value, out _))
                {
                    throw new InvalidOperationException($"Cannot convert '{value}' to a transition duration: use seconds (0.15), milliseconds (150ms) or a TimeSpan (0:0:0.15).");
                }

                _Duration = value;
            }
        }

        private string _Delay;
        public string Delay
        {
            get => _Delay;
            set
            {
                if (!AnimationXamlParser.TryParseDuration(value, out _))
                {
                    throw new InvalidOperationException($"Cannot convert '{value}' to a transition delay: use seconds (0.15), milliseconds (150ms) or a TimeSpan (0:0:0.15).");
                }

                _Delay = value;
            }
        }

        private string _Easing;
        public string Easing
        {
            get => _Easing;
            set
            {
                if (!string.IsNullOrWhiteSpace(value) && !UIEasing.TryGet(value, out _))
                {
                    throw new InvalidOperationException($"Cannot convert '{value}' to an easing function. Known names: {string.Join(", ", UIEasing.Names)}.");
                }

                _Easing = value;
            }
        }

        /// <summary>Builds the runtime transition.</summary>
        public UITransition ToTransition()
        {
            if (string.IsNullOrWhiteSpace(_Property))
            {
                throw new InvalidOperationException("A Transition needs a Property path.");
            }

            AnimationXamlParser.TryParseDuration(_Duration, out TimeSpan duration);
            AnimationXamlParser.TryParseDuration(_Delay, out TimeSpan delay);
            IUIEasingFunction easing = null;
            if (!string.IsNullOrWhiteSpace(_Easing))
            {
                UIEasing.TryGet(_Easing, out easing);
            }

            return UITransition.Create(_Property, duration, delay, easing);
        }
    }

    /// <summary>
    /// XAML declaration of an element's <see cref="UIRenderTransform"/> (S7; ADR-0006), inside <c>&lt;Button.RenderTransform&gt;</c>:
    /// <code>&lt;RenderTransform Scale="1.05" Origin="0.5,0.5" Rotation="10" Translation="4,0" /&gt;</code>
    /// Vectors are <c>x,y</c> or a single number applied to both components; <see cref="Rotation"/> is in degrees.
    /// </summary>
    public class RenderTransform
    {
        private string _Translation;
        public string Translation
        {
            get => _Translation;
            set => _Translation = AnimationXamlParser.ValidateVector2(value, nameof(Translation));
        }

        private string _Scale;
        public string Scale
        {
            get => _Scale;
            set => _Scale = AnimationXamlParser.ValidateVector2(value, nameof(Scale));
        }

        public float? Rotation { get; set; }

        private string _Origin;
        public string Origin
        {
            get => _Origin;
            set => _Origin = AnimationXamlParser.ValidateVector2(value, nameof(Origin));
        }

        /// <summary>Writes the declared components onto <paramref name="target"/> (undeclared components are left alone).</summary>
        public void ApplyTo(UIRenderTransform target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (_Translation != null)
            {
                target.Translation = AnimationXamlParser.ParseVector2(_Translation);
            }

            if (_Scale != null)
            {
                target.Scale = AnimationXamlParser.ParseVector2(_Scale);
            }

            if (Rotation.HasValue)
            {
                target.Rotation = Rotation.Value;
            }

            if (_Origin != null)
            {
                target.Origin = AnimationXamlParser.ParseVector2(_Origin);
            }
        }
    }

    /// <summary>String conversions of the animation XAML layer.</summary>
    public static class AnimationXamlParser
    {
        /// <summary>Parses <c>0.15</c> (seconds), <c>150ms</c> (milliseconds) or <c>0:0:0.15</c> (<see cref="TimeSpan"/>), invariant culture.
        /// Null or empty means zero.</summary>
        public static bool TryParseDuration(string value, out TimeSpan duration)
        {
            duration = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(value))
            {
                return true;
            }

            string text = value.Trim();
            if (text.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
            {
                if (double.TryParse(text[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out double milliseconds) && milliseconds >= 0)
                {
                    duration = TimeSpan.FromMilliseconds(milliseconds);
                    return true;
                }

                return false;
            }

            if (text.EndsWith("s", StringComparison.OrdinalIgnoreCase))
            {
                text = text[..^1];
            }

            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double seconds) && seconds >= 0)
            {
                duration = TimeSpan.FromSeconds(seconds);
                return true;
            }

            if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out TimeSpan parsed) && parsed >= TimeSpan.Zero)
            {
                duration = parsed;
                return true;
            }

            return false;
        }

        /// <summary>Parses <c>x,y</c> (or <c>x y</c>) or a single number applied to both components, invariant culture.</summary>
        public static Vector2 ParseVector2(string value)
        {
            if (!TryParseVector2(value, out Vector2 vector))
            {
                throw new FormatException($"Cannot convert '{value}' to a vector: use 'x,y' or a single number.");
            }

            return vector;
        }

        public static bool TryParseVector2(string value, out Vector2 vector)
        {
            vector = default;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            string[] parts = value.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float uniform))
            {
                vector = new Vector2(uniform, uniform);
                return true;
            }

            if (parts.Length == 2
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            {
                vector = new Vector2(x, y);
                return true;
            }

            return false;
        }

        internal static string ValidateVector2(string value, string propertyName)
        {
            if (value != null && !TryParseVector2(value, out _))
            {
                throw new InvalidOperationException($"Cannot convert '{value}' to RenderTransform.{propertyName}: use 'x,y' or a single number.");
            }

            return value;
        }
    }
}
