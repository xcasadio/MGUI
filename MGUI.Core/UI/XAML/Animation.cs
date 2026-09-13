#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif
using MGUI.Core.UI.Animation.States;
using System.Collections.ObjectModel;
using System.Globalization;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Brushes.FillBrushes;

namespace MGUI.Core.UI.XAML;

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
    private string _property;
    public string Property
    {
        get => _property;
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

            _property = value;
        }
    }

    private string _duration;
    public string Duration
    {
        get => _duration;
        set
        {
            if (!AnimationXamlParser.TryParseDuration(value, out _))
            {
                throw new InvalidOperationException($"Cannot convert '{value}' to a transition duration: use seconds (0.15), milliseconds (150ms) or a TimeSpan (0:0:0.15).");
            }

            _duration = value;
        }
    }

    private string _delay;
    public string Delay
    {
        get => _delay;
        set
        {
            if (!AnimationXamlParser.TryParseDuration(value, out _))
            {
                throw new InvalidOperationException($"Cannot convert '{value}' to a transition delay: use seconds (0.15), milliseconds (150ms) or a TimeSpan (0:0:0.15).");
            }

            _delay = value;
        }
    }

    private string _easing;
    public string Easing
    {
        get => _easing;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && !UIEasing.TryGet(value, out _))
            {
                throw new InvalidOperationException($"Cannot convert '{value}' to an easing function. Known names: {string.Join(", ", UIEasing.Names)}.");
            }

            _easing = value;
        }
    }

    /// <summary>Builds the runtime transition.</summary>
    public UITransition ToTransition()
    {
        if (string.IsNullOrWhiteSpace(_property))
        {
            throw new InvalidOperationException("A Transition needs a Property path.");
        }

        AnimationXamlParser.TryParseDuration(_duration, out var duration);
        AnimationXamlParser.TryParseDuration(_delay, out var delay);
        IUIEasingFunction easing = null;
        if (!string.IsNullOrWhiteSpace(_easing))
        {
            UIEasing.TryGet(_easing, out easing);
        }

        return UITransition.Create(_property, duration, delay, easing);
    }
}

/// <summary>
/// XAML declaration of a named visual state (ADR-0007, decision 3 and 5), inside <c>&lt;Button.VisualStates&gt;</c> or <c>&lt;Style.VisualStates&gt;</c>:
/// <code>&lt;VisualStateDefinition Name="Hover"&gt;&lt;Setter Property="RenderTransform.Scale" Value="1.05" /&gt;&lt;/VisualStateDefinition&gt;</code>
/// A setter's <see cref="Setter.Property"/> is a registered animation target path and its <see cref="Setter.Value"/> is converted by the target's value
/// type when the setter is added (float, int, <c>x,y</c> vectors, colours, thicknesses), so an unknown path or a bad value is a loader diagnostic.
/// </summary>
[ContentProperty(nameof(Setters))]
public class VisualStateDefinition
{
    private string _name;

    /// <summary>The state name (<c>Hover</c>, <c>Pressed</c>, <c>Checked</c>...); validated when set, so a nameless state in a style nobody uses is still a loader diagnostic.</summary>
    public string Name
    {
        get => _name;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException("A VisualStateDefinition needs a Name.");
            }

            _name = value;
        }
    }

    public VisualStateSetterCollection Setters { get; } = new();

    /// <summary>Builds the runtime state (a new instance each call: a style shares one definition between its elements).</summary>
    public UIVisualState ToVisualState()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("A VisualStateDefinition needs a Name.");
        }

        UIVisualState state = new(Name);
        foreach (var setter in Setters)
        {
            state.Add(setter.Property, setter.Value);
        }

        return state;
    }
}

/// <summary>The setters of a <see cref="VisualStateDefinition"/>: each one is validated and its value converted when it is added.</summary>
public sealed class VisualStateSetterCollection : Collection<Setter>
{
    protected override void InsertItem(int index, Setter item)
    {
        Convert(item);
        base.InsertItem(index, item);
    }

    protected override void SetItem(int index, Setter item)
    {
        Convert(item);
        base.SetItem(index, item);
    }

    private static void Convert(Setter item)
    {
        if (item == null)
        {
            throw new ArgumentNullException(nameof(item));
        }

        item.Value = AnimationXamlParser.ConvertStateValue(item.Property, item.Value);
    }
}

/// <summary>
/// XAML declaration of an element's <see cref="UIRenderTransform"/> (S7; ADR-0006), inside <c>&lt;Button.RenderTransform&gt;</c>:
/// <code>&lt;RenderTransform Scale="1.05" Origin="0.5,0.5" Rotation="10" Translation="4,0" /&gt;</code>
/// Vectors are <c>x,y</c> or a single number applied to both components; <see cref="Rotation"/> is in degrees.
/// </summary>
public class RenderTransform
{
    private string _translation;
    public string Translation
    {
        get => _translation;
        set => _translation = AnimationXamlParser.ValidateVector2(value, nameof(Translation));
    }

    private string _scale;
    public string Scale
    {
        get => _scale;
        set => _scale = AnimationXamlParser.ValidateVector2(value, nameof(Scale));
    }

    public float? Rotation { get; set; }

    private string _origin;
    public string Origin
    {
        get => _origin;
        set => _origin = AnimationXamlParser.ValidateVector2(value, nameof(Origin));
    }

    /// <summary>Writes the declared components onto <paramref name="target"/> (undeclared components are left alone).</summary>
    public void ApplyTo(UIRenderTransform target)
    {
        if (target == null)
        {
            throw new ArgumentNullException(nameof(target));
        }

        if (_translation != null)
        {
            target.Translation = AnimationXamlParser.ParseVector2(_translation);
        }

        if (_scale != null)
        {
            target.Scale = AnimationXamlParser.ParseVector2(_scale);
        }

        if (Rotation.HasValue)
        {
            target.Rotation = Rotation.Value;
        }

        if (_origin != null)
        {
            target.Origin = AnimationXamlParser.ParseVector2(_origin);
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

        var text = value.Trim();
        if (text.EndsWith("ms", StringComparison.OrdinalIgnoreCase))
        {
            if (double.TryParse(text[..^2], NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds) && milliseconds >= 0)
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

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
        {
            duration = TimeSpan.FromSeconds(seconds);
            return true;
        }

        if (TimeSpan.TryParse(text, CultureInfo.InvariantCulture, out var parsed) && parsed >= TimeSpan.Zero)
        {
            duration = parsed;
            return true;
        }

        return false;
    }

    /// <summary>Parses <c>x,y</c> (or <c>x y</c>) or a single number applied to both components, invariant culture.</summary>
    /// <summary>Converts a visual state setter value to the value type of the animation target at <paramref name="path"/>.</summary>
    /// <exception cref="InvalidOperationException">The path is unknown, the value is missing, cannot be converted, or the target's type has no XAML form.</exception>
    public static object ConvertStateValue(string path, object value)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("A visual state Setter needs a Property path.");
        }

        var type = UIAnimationTargets.GetValueType(path) ?? throw new InvalidOperationException(
            $"Cannot convert '{path}' to an animation target for a visual state Setter: unknown path. Registered paths: {string.Join(", ", UIAnimationTargets.Paths)}.");
        if (value == null)
        {
            throw new InvalidOperationException($"The visual state Setter for '{path}' needs a Value.");
        }

        if (value is not string text)
        {
            if (type.IsInstanceOfType(value))
            {
                return value;
            }

            throw new InvalidOperationException($"The visual state Setter for '{path}' needs a {type.Name}, got a {value.GetType().Name}.");
        }

        text = text.Trim();
        try
        {
            if (type == typeof(float))
            {
                return float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            if (type == typeof(int) || type == typeof(int?))
            {
                return int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            }

            if (type == typeof(Vector2))
            {
                return ParseVector2(text);
            }

            if (type == typeof(Color))
            {
                return XNAColorStringConverter.ParseColor(text);
            }

            if (type == typeof(MonoGame.Extended.Thickness))
            {
                return ((Thickness)new ThicknessStringConverter().ConvertFrom(null, CultureInfo.InvariantCulture, text)).ToThickness();
            }
        }
        catch (Exception error) when (error is FormatException or OverflowException or ArgumentException or InvalidOperationException)
        {
            // No inner exception: the loader reports the innermost message, which must name the value and the path.
            throw new InvalidOperationException($"Cannot convert '{text}' to a {type.Name} for the visual state Setter '{path}': {error.Message}");
        }

        throw new InvalidOperationException(
            $"The visual state Setter '{path}' targets a {type.Name}, which XAML cannot declare: only float, int, Vector2, Color and Thickness targets can be set by a visual state in XAML.");
    }

    public static Vector2 ParseVector2(string value)
    {
        if (!TryParseVector2(value, out var vector))
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

        var parts = value.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1 && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var uniform))
        {
            vector = new Vector2(uniform, uniform);
            return true;
        }

        if (parts.Length == 2
            && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)
            && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y))
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