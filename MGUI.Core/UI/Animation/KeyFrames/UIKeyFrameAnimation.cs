using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Interpolation;

namespace MGUI.Core.UI.Animation.KeyFrames;

/// <summary>
/// A property animation driven by a <see cref="UIKeyFrameTrack{T}"/> (ADR-0007, decision 2): the value at a progress is interpolated between
/// the two keys around it, with the easing of the key the segment ends on; the global <see cref="UIAnimation{T}.Easing"/> is ignored.
/// <code>
/// new UIKeyFrameAnimation&lt;float&gt;("Opacity") { Duration = TimeSpan.FromSeconds(1), Track = { { 0f, 0f }, { 0.5f, 1f }, { 1f, 0f } } }
/// </code>
/// <see cref="UIAnimation{T}.From"/> and <see cref="UIAnimation{T}.To"/> are taken from the track: the first key (or the current value when
/// the track does not start at 0) and the last key. Everything else (delay, repeat, auto-reverse, fill and cancel behaviours, ownership,
/// conflicts) is the engine's.
/// </summary>
public class UIKeyFrameAnimation<T> : UIPropertyAnimation<T>
{
    private IUIInterpolator<T> _TrackInterpolator;
    private IUIEasingFunction[] _Easings = Array.Empty<IUIEasingFunction>();

    public UIKeyFrameAnimation() { }

    public UIKeyFrameAnimation(string property)
        : base(property)
    {
    }

    /// <summary>The keys. Validated when the animation starts.</summary>
    public UIKeyFrameTrack<T> Track { get; set; } = new();

    protected internal override void OnStarting(object inheritedBase)
    {
        var track = Track ?? throw new InvalidOperationException($"{nameof(UIKeyFrameAnimation<T>)} needs a {nameof(Track)}.");
        track.Validate();

        if (track.StartsAtZero)
        {
            From = track.Frames[0].Value;
        }
        else
        {
            ClearFrom();
        }

        To = track.Frames[^1].Value;
        _TrackInterpolator = Interpolator ?? UIInterpolators.Get<T>();
        if (_Easings.Length != track.Count)
        {
            _Easings = new IUIEasingFunction[track.Count];
        }

        for (var i = 0; i < track.Count; i++)
        {
            _Easings[i] = track.Frames[i].ResolveEasing();
        }

        base.OnStarting(inheritedBase);
    }

    protected internal override void ApplyProgress(float progress)
    {
        var track = Track;
        track.FindSegment(progress, out var fromIndex, out var toIndex, out var local);
        var from = fromIndex >= 0 ? track.Frames[fromIndex].Value : StartValue;
        var to = track.Frames[toIndex].Value;
        var eased = _Easings[toIndex].Ease(local);
        CurrentValue = _TrackInterpolator.Lerp(from, to, eased);
        WriteValue(CurrentValue);
    }
}