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
    }

    protected override T ReadCurrentValue() => _ResolvedTarget.GetValue(Owner);

    protected override void WriteValue(T value) => _ResolvedTarget.SetValue(Owner, value, Name);

    protected override void RestoreBaseValueCore(T baseValue) => _ResolvedTarget.RestoreBaseValue(Owner, baseValue);

    protected override void ReleaseHoldCore()
    {
        if (_ResolvedTarget != null && _ResolvedTarget.IsStoreBacked)
        {
            _ResolvedTarget.RestoreBaseValue(Owner, default);
        }
    }
}