namespace MGUI.Core.UI.Animation.Composition;

/// <summary>A pause: an animation with a <see cref="UIAnimation.Duration"/> and no target (ADR-0007, decision 1), the <c>Wait</c> of a
/// <see cref="UISequenceAnimation"/> (<see cref="UISequenceAnimation.AppendDelay"/>). Its <see cref="UIAnimation.TargetKey"/> is synthetic.</summary>
public sealed class UIDelayAnimation : UIAnimation
{
    private static int _NextId;
    private readonly string _Key;

    public UIDelayAnimation()
    {
        _Key = $"Delay#{Interlocked.Increment(ref _NextId)}";
    }

    public UIDelayAnimation(TimeSpan duration)
        : this()
    {
        Duration = duration;
    }

    protected internal override bool IsStoreBacked => false;

    protected internal override string TargetKey => _Key;

    protected internal override object BaseValueBoxed => null;

    protected internal override void OnStarting(object inheritedBase) { }

    protected internal override void ApplyProgress(float progress) { }

    protected internal override void OnRestoreBaseValue() { }

    protected internal override void OnReleaseHold() { }
}