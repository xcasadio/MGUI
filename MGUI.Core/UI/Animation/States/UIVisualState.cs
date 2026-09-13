using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI.Animation.States;

/// <summary>Implemented by the controls that expose a checked state (<c>MGToggleButton</c>, <c>MGCheckBox</c>, <c>MGRadioButton</c>), so the
/// named visual state <see cref="UIVisualStateNames.Checked"/> can be resolved without a new primary state (ADR-0007, decision 3).</summary>
public interface IUICheckable
{
    /// <summary>True when checked, false when not, null for the indeterminate state of a three-state check box.</summary>
    bool? IsChecked { get; }
}

/// <summary>The names of the built-in visual states, in priority order (the first whose condition holds and that the element defines wins).</summary>
public static class UIVisualStateNames
{
    public const string Disabled = "Disabled";
    public const string Checked = "Checked";
    public const string Selected = "Selected";
    public const string Pressed = "Pressed";
    public const string Hover = "Hover";
    public const string Focused = "Focused";
    public const string Normal = "Normal";

    /// <summary>Every built-in name, highest priority first.</summary>
    public static readonly IReadOnlyList<string> Priority = new[] { Disabled, Checked, Selected, Pressed, Hover, Focused, Normal };
}

/// <summary>
/// A named visual state of one element (ADR-0007, decision 3): a name and typed setters keyed by animation target path
/// (<see cref="UIAnimationTargets"/>). Entering the state writes every setter (a pilot with the <c>VisualState</c> source of the store, a plain
/// property directly); leaving it restores what the state changed. A transition on a setter's path interpolates the change.
/// <code>
/// button.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { "RenderTransform.Scale", new Vector2(1.05f) } });
/// </code>
/// </summary>
public sealed class UIVisualState : IEnumerable<UIVisualStateSetter>
{
    private readonly List<UIVisualStateSetter> _Setters = new();

    public UIVisualState(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A visual state needs a name.", nameof(name));
        }

        Name = name;
    }

    public string Name { get; }

    /// <summary>The setters, in the order they were added; a second setter for a path replaces the first.</summary>
    public IReadOnlyList<UIVisualStateSetter> Setters => _Setters;

    /// <summary>Adds a setter: <paramref name="value"/> must be of the value type of the registered target at <paramref name="path"/>.</summary>
    /// <exception cref="ArgumentException">The path is unknown, or the value has the wrong type.</exception>
    public UIVisualState Add(string path, object value) => Add(new UIVisualStateSetter(path, value));

    public UIVisualState Add(UIVisualStateSetter setter)
    {
        if (setter == null)
        {
            throw new ArgumentNullException(nameof(setter));
        }

        int existing = _Setters.FindIndex(x => string.Equals(x.Path, setter.Path, StringComparison.OrdinalIgnoreCase));
        if (existing >= 0)
        {
            _Setters[existing] = setter;
        }
        else
        {
            _Setters.Add(setter);
        }

        return this;
    }

    /// <summary>True when the state has a setter for <paramref name="path"/>.</summary>
    public bool HasPath(string path)
    {
        for (int i = 0; i < _Setters.Count; i++)
        {
            if (string.Equals(_Setters[i].Path, path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public IEnumerator<UIVisualStateSetter> GetEnumerator() => _Setters.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

    public override string ToString() => $"{nameof(UIVisualState)} {Name} ({_Setters.Count} setters)";
}

/// <summary>One setter of a <see cref="UIVisualState"/>: a target path and its value, bound once to the typed target (no reflection per frame).</summary>
public sealed class UIVisualStateSetter
{
    /// <exception cref="ArgumentException">The path is not a registered animation target, or the value is not of its value type.</exception>
    public UIVisualStateSetter(string path, object value)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A setter needs a target path.", nameof(path));
        }

        Type valueType = UIAnimationTargets.GetValueType(path) ?? throw new ArgumentException(
            $"Unknown animation target '{path}'. Registered paths: {string.Join(", ", UIAnimationTargets.Paths)}.", nameof(path));
        Path = path;
        Value = value;
        Applier = UIVisualStateApplier.Create(valueType, path, value);
    }

    public string Path { get; }

    public object Value { get; }

    internal UIVisualStateApplier Applier { get; }

    public override string ToString() => $"{Path} = {Value}";
}

/// <summary>The typed writer behind a setter. Built once through reflection when the setter is created; applying and restoring are plain calls.</summary>
internal abstract class UIVisualStateApplier
{
    public static UIVisualStateApplier Create(Type valueType, string path, object value)
    {
        Type applierType = typeof(UIVisualStateApplier<>).MakeGenericType(valueType);
        try
        {
            return (UIVisualStateApplier)Activator.CreateInstance(applierType, path, value);
        }
        catch (System.Reflection.TargetInvocationException error) when (error.InnerException != null)
        {
            throw error.InnerException;
        }
    }

    /// <summary>Writes the setter's value on <paramref name="element"/> for the state <paramref name="stateName"/>.</summary>
    public abstract void Apply(MGElement element, string stateName, Dictionary<string, object> bases);

    /// <summary>Undoes the setter on <paramref name="element"/>: clears the <c>VisualState</c> contribution of a pilot, writes the memorised base of a plain property.</summary>
    public abstract void Restore(MGElement element, Dictionary<string, object> bases);
}

internal sealed class UIVisualStateApplier<T> : UIVisualStateApplier
{
    private readonly string _Path;
    private readonly T _Value;
    private readonly IUIAnimationTarget<T> _Target;
    private readonly IUIStoreBackedAnimationTarget<T> _StoreTarget;
    private readonly IUIObservableAnimationTarget<T> _Observable;
    private string _LastStateName;
    private UIValueResolutionSource _LastSource;

    public UIVisualStateApplier(string path, object value)
    {
        _Path = path;
        if (value is not T typed)
        {
            throw new ArgumentException(
                $"The visual state setter for '{path}' needs a value of type '{typeof(T).Name}', got {(value == null ? "null" : "'" + value.GetType().Name + "'")}.", nameof(value));
        }

        _Value = typed;
        _Target = UIAnimationTargets.Resolve<T>(path);
        _StoreTarget = _Target as IUIStoreBackedAnimationTarget<T>;
        _Observable = _Target as IUIObservableAnimationTarget<T>;
    }

    public override void Apply(MGElement element, string stateName, Dictionary<string, object> bases)
    {
        if (!bases.ContainsKey(_Path))
        {
            bases[_Path] = CaptureBase(element);
        }

        if (_StoreTarget != null)
        {
            if (!ReferenceEquals(stateName, _LastStateName))
            {
                _LastStateName = stateName;
                _LastSource = UIValueResolutionSource.VisualState(UIPilotPropertyResolver.KindOf(_StoreTarget.Pilot), "visualstate:" + stateName);
            }

            _StoreTarget.SetValue(element, _Value, _LastSource);
            return;
        }

        _Target.SetValue(element, _Value, "visualstate:" + stateName);
    }

    /// <summary>The value the path rests at before the state writes it. A transition mid-run holds the property at an in-flight value (a plain
    /// property is written directly, a pilot's physical value is the animated one), so the base is read from the transition, which keeps the value
    /// it heads to; otherwise from the target.</summary>
    private T CaptureBase(MGElement element)
    {
        if (element.Transitions[_Path] is UITransition<T> transition)
        {
            return transition.SettledValue;
        }

        return _Observable != null ? _Observable.GetUnderlyingValue(element) : _Target.GetValue(element);
    }

    public override void Restore(MGElement element, Dictionary<string, object> bases)
    {
        if (!bases.TryGetValue(_Path, out object baseValue))
        {
            return;
        }

        if (_StoreTarget != null)
        {
            // The base is kept while a run still holds the pilot: re-entering the state mid-run must not re-read the animated value as its base.
            if (_LastStateName != null && _StoreTarget.ClearContribution(element, _LastSource, (T)baseValue))
            {
                bases.Remove(_Path);
            }

            return;
        }

        bases.Remove(_Path);
        _Target.RestoreBaseValue(element, (T)baseValue);
    }
}