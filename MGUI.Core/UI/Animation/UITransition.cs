using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Interpolation;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// Non-generic base of <see cref="UITransition{T}"/>: the declaration (<see cref="Property"/>, <see cref="Duration"/>, <see cref="Delay"/>,
/// <see cref="Easing"/>) and the attachment to an element through <see cref="UITransitionCollection"/>. <see cref="Create"/> builds the
/// typed transition for a registered path, which the XAML layer (S7) relies on.
/// </summary>
public abstract class UITransition
{
    private TimeSpan _Duration;
    /// <summary>Length of the interpolation from the previous value to the new one.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan Duration
    {
        get => _Duration;
        set => _Duration = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(Duration)} cannot be negative.");
    }

    private TimeSpan _Delay;
    /// <summary>Time to wait after the change before the interpolation starts. Default: zero.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan Delay
    {
        get => _Delay;
        set => _Delay = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(Delay)} cannot be negative.");
    }

    /// <summary>The easing function. Null means <see cref="UIEasing.Linear"/>.</summary>
    public IUIEasingFunction Easing { get; set; }

    /// <summary>The animated property path, resolved in <see cref="UIAnimationTargets"/> when the transition is added to an element.</summary>
    public abstract string Property { get; }

    /// <summary>The element this transition is attached to, null until it is added to a <see cref="UITransitionCollection"/>.</summary>
    public MGElement Owner { get; private set; }

    /// <summary>True while the transition is interpolating.</summary>
    public abstract bool IsRunning { get; }

    /// <summary>The value type of the animated property.</summary>
    public abstract Type ValueType { get; }

    /// <summary>Raw progress of the running interpolation in [0, 1], null while idle (diagnostics).</summary>
    public abstract float? RunningProgress { get; }

    /// <summary>Creates the typed transition for a registered path (<c>UITransition&lt;T&gt;</c> where <c>T</c> is the target's value type).</summary>
    /// <exception cref="ArgumentException">The path is unknown.</exception>
    public static UITransition Create(string property, TimeSpan duration, TimeSpan delay = default, IUIEasingFunction easing = null)
    {
        Type valueType = UIAnimationTargets.GetValueType(property) ?? throw new ArgumentException(
            $"Unknown animation target '{property}'. Registered paths: {string.Join(", ", UIAnimationTargets.Paths)}.", nameof(property));
        UITransition transition = (UITransition)Activator.CreateInstance(typeof(UITransition<>).MakeGenericType(valueType), property);
        transition.Duration = duration;
        transition.Delay = delay;
        transition.Easing = easing;
        return transition;
    }

    internal void Attach(MGElement owner)
    {
        if (Owner != null)
        {
            throw new InvalidOperationException($"This transition is already attached to {Owner.GetType().Name}; a transition belongs to one element.");
        }

        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        OnAttached();
    }

    internal void Detach()
    {
        if (Owner == null)
        {
            return;
        }

        OnDetached();
        Owner = null;
    }

    /// <summary>Resolves the target and subscribes to its changes.</summary>
    protected abstract void OnAttached();

    /// <summary>Unsubscribes and cancels the running interpolation, keeping the current value.</summary>
    protected abstract void OnDetached();

    public override string ToString() => $"{GetType().Name} [{Property}] {Duration.TotalMilliseconds:0}ms{(IsRunning ? " running" : "")}";
}

/// <summary>
/// Interpolates automatically every change of a property (S6; ADR-0006 decisions 4 and 11):
/// <code>button.Transitions.Add(new UITransition&lt;Color&gt;("Background") { Duration = TimeSpan.FromMilliseconds(150), Easing = UIEasing.CubicOut });</code>
/// then assigning a new solid blue brush to the background's Normal slot fades from the previous colour instead of snapping.
/// A transition on <c>RenderScale</c> reacts to <see cref="MGElement.VisualStateChanged"/>: hover in and out interpolate the state-driven
/// scale from the current animated value, never from the other end.<para/>
/// Rules: a change during the interpolation retargets it from the current animated value (no snap); an explicit animation started on the same
/// path replaces the interpolation (conflict rule) and the transition stays quiet until it is over; removing the transition keeps the
/// current value. The interpolation is a <see cref="UIPropertyAnimation{T}"/> registered like any other (restoring the base at the end, so
/// a pilot goes back to its local value and a plain property simply stays on the new value).<para/>
/// Limit: a local write to a pilot while its transition runs is not notified (the animation contribution shadows it), so it is animated
/// only once the running interpolation ends.
/// </summary>
public sealed class UITransition<T> : UITransition
{
    private readonly string _Property;
    private IUIObservableAnimationTarget<T> _Target;
    private IDisposable _Subscription;
    private UIPropertyAnimation<T> _Animation;
    private T _SettledValue;
    private bool _HasSettledValue;
    private readonly EqualityComparer<T> _Comparer = EqualityComparer<T>.Default;

    public UITransition(string property)
    {
        if (string.IsNullOrWhiteSpace(property))
        {
            throw new ArgumentException("A property path is required.", nameof(property));
        }

        _Property = property;
    }

    public UITransition(string property, TimeSpan duration, IUIEasingFunction easing = null)
        : this(property)
    {
        Duration = duration;
        Easing = easing;
    }

    public override string Property => _Property;

    /// <summary>The interpolator. Null means the one registered for <typeparamref name="T"/>.</summary>
    public IUIInterpolator<T> Interpolator { get; set; }

    public override bool IsRunning => _Animation != null && _Animation.IsActive;

    public override Type ValueType => typeof(T);

    public override float? RunningProgress => IsRunning ? _Animation.Progress : null;

    /// <summary>The running interpolation, or the last one; null before the first change.</summary>
    public UIPropertyAnimation<T> Animation => _Animation;

    /// <summary>The value the transition last settled on (initially the value read when it was attached).</summary>
    public T SettledValue => _SettledValue;

    protected override void OnAttached()
    {
        IUIAnimationTarget<T> target = UIAnimationTargets.Resolve<T>(_Property);
        _Target = target as IUIObservableAnimationTarget<T> ?? throw new InvalidOperationException(
            $"The animation target '{_Property}' ({target.GetType().Name}) is not observable: it cannot be used in a transition, only in an explicit animation.");
        _SettledValue = _Target.GetUnderlyingValue(Owner);
        _HasSettledValue = true;
        _Subscription = _Target.Subscribe(Owner, HandleChanged);
    }

    protected override void OnDetached()
    {
        _Subscription?.Dispose();
        _Subscription = null;
        if (_Animation != null && _Animation.IsActive)
        {
            _Animation.CancelCore(UIAnimationCancelBehavior.KeepCurrent);
        }
    }

    private void HandleChanged(MGElement element)
    {
        if (Owner == null || !ReferenceEquals(element, Owner))
        {
            return;
        }

        T underlying = _Target.GetUnderlyingValue(Owner);

        if (Owner.Parent == null && !Owner.IsWindow)
        {
            // An element outside the tree (not yet attached, or detached) is not animated: follow the value silently.
            _SettledValue = underlying;
            _HasSettledValue = true;
            return;
        }

        if (_Animation != null && _Animation.IsActive)
        {
            // Our own tick, or the value we are heading to: nothing new.
            if (_Comparer.Equals(underlying, _Animation.CurrentValue) || _Comparer.Equals(underlying, _Animation.To))
            {
                return;
            }
        }
        else if (Owner.Animations.IsAnimating(_Property))
        {
            // An explicit animation owns the path: stay quiet, but follow its values so the next transition starts from where it left the property.
            _SettledValue = underlying;
            return;
        }

        if (_HasSettledValue && _Comparer.Equals(underlying, _SettledValue) && !(_Animation != null && _Animation.IsActive))
        {
            return;
        }

        T from = _Animation != null && _Animation.IsActive ? _Animation.CurrentValue : (_HasSettledValue ? _SettledValue : underlying);
        _SettledValue = underlying;
        _HasSettledValue = true;

        if (Duration <= TimeSpan.Zero && Delay <= TimeSpan.Zero)
        {
            if (_Animation != null && _Animation.IsActive)
            {
                _Animation.CancelCore(UIAnimationCancelBehavior.KeepCurrent);
            }

            return;
        }

        _Animation = new UIPropertyAnimation<T>
        {
            Target = _Target,
            From = from,
            To = underlying,
            Duration = Duration,
            Delay = Delay,
            Easing = Easing,
            Interpolator = Interpolator,
            //  A pilot (store-backed) falls back to the local value it was heading to once its contribution is cleared; a plain property simply
            //  keeps the end value, which is that local value: never restore a plain property, since the base handed over by the replaced
            //  transition run is the value before the previous change.
            FillBehavior = _Target.IsStoreBacked ? UIAnimationFillBehavior.RestoreBaseValue : UIAnimationFillBehavior.HoldEnd,
            CancelBehavior = UIAnimationCancelBehavior.KeepCurrent,
            //  The base of a run is the value it heads to, never the base inherited from the run it replaces (a forced restore on
            //  detachment would otherwise write a stale value and spawn an orphan run; review finding, S6).
            InheritsBaseValue = false,
            Name = "transition:" + _Property,
        };
        Owner.Animations.Start(_Animation);
    }
}