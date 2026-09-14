namespace MGUI.Core.UI.Animation;

/// <summary>
/// Animates a property of its owner element addressed by path (<see cref="Property"/>, resolved in <see cref="UIAnimationTargets"/> when the
/// animation starts) or by an explicit <see cref="Target"/> (decision 5).<para/>
/// <code>
/// button.Animations.Start(new UIPropertyAnimation&lt;float&gt;("Opacity") { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.CubicOut });
/// </code>
/// </summary>
public class UIPropertyAnimation<T> : UIAnimation<T>
{
    private IUIAnimationTarget<T> _ResolvedTarget;

    /// <summary>Non-null exactly when <see cref="_ResolvedTarget"/> is one of the five brush targets (ADR-0009): set once in
    /// <see cref="OnStarting"/>, alongside <see cref="_AnimatedHandle"/>, and checked by <see cref="WriteValue"/>, <see cref="RestoreBaseValueCore"/>
    /// and <see cref="ReleaseHoldCore"/> instead of a per-call type test.</summary>
    private IUIBrushAnimationTarget<T> _BrushTarget;

    /// <summary>The handle <see cref="IUIBrushAnimationTarget{T}.BeginAnimatedValue"/> returned for the run in progress, null once
    /// <see cref="IUIBrushAnimationTarget{T}.EndAnimatedValue"/> has consumed it or when <see cref="_BrushTarget"/> is null.</summary>
    private object _AnimatedHandle;

    public UIPropertyAnimation() { }

    public UIPropertyAnimation(string property)
    {
        Property = property;
    }

    /// <summary>The path of the animated property (<c>Opacity</c>, <c>RenderTransform.Scale</c>, <c>Background</c>, ...), looked up in
    /// <see cref="UIAnimationTargets"/> at start. Ignored when <see cref="Target"/> is set.</summary>
    public string Property { get; set; }

    /// <summary>An explicit target, bypassing the registry.</summary>
    public IUIAnimationTarget<T> Target { get; set; }

    protected internal override string TargetKey => Target?.Path ?? Property;

    protected internal override bool IsStoreBacked => (_ResolvedTarget ?? Target)?.IsStoreBacked ?? false;

    protected internal override void OnStarting(object inheritedBase)
    {
        _ResolvedTarget = Target ?? UIAnimationTargets.Resolve<T>(Property ?? throw new InvalidOperationException(
            $"{nameof(UIPropertyAnimation<T>)} needs a {nameof(Property)} path or an explicit {nameof(Target)}."));
        base.OnStarting(inheritedBase);

        // ADR-0009: the clone is created here, once, after the base class above has read the start and base values (it still reads them
        // through GetValue -- the base brush, untouched at this point) -- not earlier, so the clone never influences its own start value.
        _BrushTarget = _ResolvedTarget as IUIBrushAnimationTarget<T>;
        _AnimatedHandle = _BrushTarget?.BeginAnimatedValue(Owner, Name);
    }

    protected override T ReadCurrentValue() => _ResolvedTarget.GetValue(Owner);

    protected override void WriteValue(T value)
    {
        if (_BrushTarget != null)
        {
            _BrushTarget.ApplyAnimatedValue(_AnimatedHandle, value);
        }
        else
        {
            _ResolvedTarget.SetValue(Owner, value, Name);
        }
    }

    protected override void RestoreBaseValueCore(T baseValue)
    {
        if (_BrushTarget != null)
        {
            _BrushTarget.EndAnimatedValue(Owner, _AnimatedHandle, baseValue);
            _AnimatedHandle = null;
        }
        else
        {
            _ResolvedTarget.RestoreBaseValue(Owner, baseValue);
        }
    }

    protected override void ReleaseHoldCore()
    {
        if (_BrushTarget != null)
        {
            // ADR-0009: BaseValue (the run's own captured base) replaces the previous call's `default` -- the clone's original,
            // recorded once at BeginAnimatedValue, is by far the common case anyway (EndAnimatedValue prefers it over baseValue), but a
            // replaced-run chain with no recoverable original now rebuilds from the true base colour instead of a blank default.
            _BrushTarget.EndAnimatedValue(Owner, _AnimatedHandle, BaseValue);
            _AnimatedHandle = null;
        }
        else if (_ResolvedTarget != null && _ResolvedTarget.IsStoreBacked)
        {
            _ResolvedTarget.RestoreBaseValue(Owner, default);
        }
    }
}