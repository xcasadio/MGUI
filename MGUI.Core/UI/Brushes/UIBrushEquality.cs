using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;

namespace MGUI.Core.UI.Brushes;

/// <summary>Value-equality helper for paints (ADR-0009, W1). Equality on a brush stays reference equality by default (as in WPF: none of the
/// sixteen current brush types overrides <see cref="object.Equals(object)"/> except implicitly through the ten value <see langword="struct"/>s'
/// built-in memberwise comparison), but two guards still need to tell "the same value, a distinct instance" apart from "an actual change":
/// <see cref="VisualStateSetting{TDataType}"/>'s slot setters and <see cref="MGUI.Core.UI.Styling.MGControlTemplate"/>'s
/// theme-refresh re-application guard. Both go through <see cref="ForGuards{T}"/> instead of <see cref="EqualityComparer{T}.Default"/>
/// directly, so their observable behaviour does not change when a later slice turns a value brush into a reference type.<para/>
/// These comparers exist for the two guards above only: never use them to key a dictionary or a hash set (their <c>GetHashCode</c> is
/// intentionally a constant, see the comparer classes below), and never use them as a substitute for a brush's own equality once a slice
/// gives one of these types a real <c>Equals</c>/<c>GetHashCode</c> override.</summary>
public static class UIBrushEquality
{
    /// <summary>Null-safe value equality for two <see cref="IFillBrush"/>: <see langword="null"/> equals only <see langword="null"/>,
    /// a reference-equal pair short-circuits to true, otherwise defers to <see cref="IFillBrush.ValueEquals(IFillBrush)"/> (today, the
    /// default interface member <c>Equals(other)</c>: memberwise comparison for the value structs, reference comparison for the six
    /// mutable composite/highlight classes, recursive through any nested <see cref="IFillBrush"/>/<see cref="IBorderBrush"/> field carried
    /// by a struct such as <see cref="MGUniformBorderBrush"/>).</summary>
    public static bool ValueEquals(IFillBrush a, IFillBrush b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.ValueEquals(b);
    }

    /// <summary>Null-safe value equality for two <see cref="IBorderBrush"/>. See <see cref="ValueEquals(IFillBrush, IFillBrush)"/>.</summary>
    public static bool ValueEquals(IBorderBrush a, IBorderBrush b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return a.ValueEquals(b);
    }

    /// <summary>Value equality for a whole <see cref="VisualStateFillBrush"/> container: every slot
    /// (<see cref="VisualStateSetting{TDataType}.NormalValue"/>/<c>SelectedValue</c>/<c>FocusedValue</c>/<c>DisabledValue</c>,
    /// plus the code-only <c>CheckedValue</c>/<c>HasCheckedValue</c> pair) compared by <see cref="ValueEquals(IFillBrush, IFillBrush)"/>,
    /// and every scalar that the container's own copy constructor treats as part of its identity
    /// (<c>FocusedColor</c>, <c>PressedModifierType</c>, <c>PressedModifier</c>, <c>OverlayOpacity</c>). The Hovered/Pressed overlay
    /// brushes are derived from those scalars, not independent state, so they are not compared separately.</summary>
    public static bool ValueEquals(VisualStateFillBrush a, VisualStateFillBrush b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return ValueEquals(a.NormalValue, b.NormalValue)
            && ValueEquals(a.SelectedValue, b.SelectedValue)
            && ValueEquals(a.FocusedValue, b.FocusedValue)
            && ValueEquals(a.DisabledValue, b.DisabledValue)
            && a.HasCheckedValue == b.HasCheckedValue
            && (!a.HasCheckedValue || ValueEquals(a.CheckedValue, b.CheckedValue))
            && a.FocusedColor == b.FocusedColor
            && a.PressedModifierType == b.PressedModifierType
            && a.PressedModifier == b.PressedModifier
            && a.OverlayOpacity == b.OverlayOpacity;
    }

    /// <summary>Comparer wrapping <see cref="ValueEquals(IFillBrush, IFillBrush)"/> for guard use (<see cref="ForGuards{T}"/>).
    /// <see cref="GetHashCode(IFillBrush)"/> is intentionally constant: these comparers back an equality CHECK on read (a guard comparing
    /// exactly two values), never a hash-based collection, so a real hash would only cost allocation/CPU for no benefit.</summary>
    public sealed class FillBrushEqualityComparer : IEqualityComparer<IFillBrush>
    {
        public bool Equals(IFillBrush a, IFillBrush b) => ValueEquals(a, b);
        public int GetHashCode(IFillBrush obj) => 0;
    }

    /// <summary>Comparer wrapping <see cref="ValueEquals(IBorderBrush, IBorderBrush)"/>. See <see cref="FillBrushEqualityComparer"/>.</summary>
    public sealed class BorderBrushEqualityComparer : IEqualityComparer<IBorderBrush>
    {
        public bool Equals(IBorderBrush a, IBorderBrush b) => ValueEquals(a, b);
        public int GetHashCode(IBorderBrush obj) => 0;
    }

    /// <summary>Comparer wrapping <see cref="ValueEquals(VisualStateFillBrush, VisualStateFillBrush)"/>.
    /// See <see cref="FillBrushEqualityComparer"/>.</summary>
    public sealed class VisualStateFillBrushEqualityComparer : IEqualityComparer<VisualStateFillBrush>
    {
        public bool Equals(VisualStateFillBrush a, VisualStateFillBrush b) => ValueEquals(a, b);
        public int GetHashCode(VisualStateFillBrush obj) => 0;
    }

    public static readonly IEqualityComparer<IFillBrush> FillBrushComparer = new FillBrushEqualityComparer();
    public static readonly IEqualityComparer<IBorderBrush> BorderBrushComparer = new BorderBrushEqualityComparer();
    public static readonly IEqualityComparer<VisualStateFillBrush> VisualStateFillBrushComparer = new VisualStateFillBrushEqualityComparer();

    /// <summary>Returns the value-equality comparer for a brush-shaped <typeparamref name="T"/> (<see cref="IFillBrush"/>,
    /// <see cref="IBorderBrush"/>, <see cref="VisualStateFillBrush"/>, or a <see cref="VisualStateSetting{TDataType}"/>
    /// whose inner data type is one of the two brush interfaces), and <see cref="EqualityComparer{T}.Default"/> for every
    /// other <typeparamref name="T"/> (<see langword="int"/>, <see cref="MonoGame.Extended.Thickness"/>, <see cref="Microsoft.Xna.Framework.Color"/>?, ...).
    /// The choice is made once per closed <typeparamref name="T"/> by the static constructor of <see cref="GuardComparerCache{T}"/>
    /// (a small amount of reflection only for the <c>VisualStateSetting&lt;&gt;</c> case, and only the first time that particular
    /// <typeparamref name="T"/> is used) and cached in a static field: no per-call reflection, no allocation after that first call.</summary>
    public static IEqualityComparer<T> ForGuards<T>() => GuardComparerCache<T>.Comparer;

    /// <summary>Compares a <see cref="VisualStateSetting{TDataType}"/> as a whole by comparing its four slots with the
    /// value comparer for <typeparamref name="TDataType"/> (obtained through <see cref="ForGuards{T}"/>, so it is itself brush-aware
    /// when <typeparamref name="TDataType"/> is a brush interface). Built through reflection only once, by <see cref="GuardComparerCache{T}"/>,
    /// for a closed <c>T</c> that is a brush-typed <see cref="VisualStateSetting{TDataType}"/>.</summary>
    private sealed class VisualStateSettingBrushComparer<TDataType> : IEqualityComparer<VisualStateSetting<TDataType>>
    {
        private static readonly IEqualityComparer<TDataType> Inner = ForGuards<TDataType>();

        public bool Equals(VisualStateSetting<TDataType> a, VisualStateSetting<TDataType> b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (a is null || b is null)
            {
                return false;
            }

            return Inner.Equals(a.NormalValue, b.NormalValue)
                && Inner.Equals(a.SelectedValue, b.SelectedValue)
                && Inner.Equals(a.FocusedValue, b.FocusedValue)
                && Inner.Equals(a.DisabledValue, b.DisabledValue);
        }

        public int GetHashCode(VisualStateSetting<TDataType> obj) => 0;
    }

    private static class GuardComparerCache<T>
    {
        public static readonly IEqualityComparer<T> Comparer = Build();

        private static IEqualityComparer<T> Build()
        {
            if (typeof(T) == typeof(IFillBrush))
            {
                return (IEqualityComparer<T>)FillBrushComparer;
            }

            if (typeof(T) == typeof(IBorderBrush))
            {
                return (IEqualityComparer<T>)BorderBrushComparer;
            }

            if (typeof(T) == typeof(VisualStateFillBrush))
            {
                return (IEqualityComparer<T>)VisualStateFillBrushComparer;
            }

            Type CandidateType = typeof(T);
            if (CandidateType.IsGenericType && CandidateType.GetGenericTypeDefinition() == typeof(VisualStateSetting<>))
            {
                Type InnerType = CandidateType.GetGenericArguments()[0];
                if (InnerType == typeof(IFillBrush) || InnerType == typeof(IBorderBrush))
                {
                    Type ComparerType = typeof(VisualStateSettingBrushComparer<>).MakeGenericType(InnerType);
                    return (IEqualityComparer<T>)Activator.CreateInstance(ComparerType);
                }
            }

            return EqualityComparer<T>.Default;
        }
    }
}
