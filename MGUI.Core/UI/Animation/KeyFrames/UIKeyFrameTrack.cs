using MGUI.Core.UI.Animation.Easing;

namespace MGUI.Core.UI.Animation.KeyFrames;

/// <summary>One key of a <see cref="UIKeyFrameTrack{T}"/> (ADR-0007, decision 2): a pure value, no reference to any element.</summary>
/// <param name="Offset">Position on the animation, in [0, 1] of its duration.</param>
/// <param name="Value">The value reached at <paramref name="Offset"/>.</param>
/// <param name="Easing">Name of the easing applied to the segment that ENDS on this key (see <see cref="UIEasing.TryGet"/>), null for linear.</param>
public readonly record struct UIKeyFrame<T>(float Offset, T Value, string Easing = null)
{
    /// <summary>The easing function named by <see cref="Easing"/>, <see cref="UIEasing.Linear"/> when null or unknown.</summary>
    public IUIEasingFunction ResolveEasing()
        => Easing != null && UIEasing.TryGet(Easing, out IUIEasingFunction function) ? function : UIEasing.Linear;
}

/// <summary>
/// The sorted keys of a keyframe animation (ADR-0007, decision 2): a plain data model, serializable by <see cref="UIKeyFrameSerializer"/>,
/// with no reference to any element, so an external editor (the author's game engine) can produce it.<para/>
/// Rules, checked by <see cref="Validate"/>: at least one key, strictly increasing offsets in [0, 1], the last key at offset 1. A track without a
/// key at offset 0 starts from the value the animated property has when the animation starts (the engine's "start from the current value").
/// </summary>
public sealed class UIKeyFrameTrack<T> : IEnumerable<UIKeyFrame<T>>
{
    private readonly List<UIKeyFrame<T>> _Frames = new();

    public UIKeyFrameTrack() { }

    public UIKeyFrameTrack(IEnumerable<UIKeyFrame<T>> frames)
    {
        foreach (UIKeyFrame<T> frame in frames ?? throw new ArgumentNullException(nameof(frames)))
        {
            Add(frame);
        }
    }

    /// <summary>The keys, sorted by offset.</summary>
    public IReadOnlyList<UIKeyFrame<T>> Frames => _Frames;

    public int Count => _Frames.Count;

    /// <summary>Adds a key, keeping the keys sorted by offset. A key at an existing offset replaces it.</summary>
    public UIKeyFrameTrack<T> Add(UIKeyFrame<T> frame)
    {
        if (float.IsNaN(frame.Offset) || frame.Offset < 0f || frame.Offset > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(frame), frame.Offset, "A key frame offset lies in [0, 1].");
        }

        int index = _Frames.FindIndex(x => x.Offset >= frame.Offset);
        if (index < 0)
        {
            _Frames.Add(frame);
        }
        else if (_Frames[index].Offset == frame.Offset)
        {
            _Frames[index] = frame;
        }
        else
        {
            _Frames.Insert(index, frame);
        }

        return this;
    }

    /// <summary>Adds a key from its parts (collection-initializer friendly).</summary>
    public UIKeyFrameTrack<T> Add(float offset, T value, string easing = null) => Add(new UIKeyFrame<T>(offset, value, easing));

    /// <summary>True when the first key is at offset 0 (the track then never reads the current value).</summary>
    public bool StartsAtZero => _Frames.Count > 0 && _Frames[0].Offset == 0f;

    /// <summary>Throws when the track cannot drive an animation: empty, or last key not at offset 1.</summary>
    public void Validate()
    {
        if (_Frames.Count == 0)
        {
            throw new InvalidOperationException("A key frame track needs at least one key.");
        }

        if (_Frames[^1].Offset != 1f)
        {
            throw new InvalidOperationException($"The last key frame must be at offset 1 (found {_Frames[^1].Offset}).");
        }
    }

    /// <summary>Finds the segment containing <paramref name="progress"/>: the key it starts from (index -1 for the implicit start value when the
    /// track does not start at 0), the key it ends on, and the local progress within the segment.</summary>
    public void FindSegment(float progress, out int fromIndex, out int toIndex, out float localProgress)
    {
        // Binary search for the first key whose offset is >= progress.
        int low = 0, high = _Frames.Count - 1;
        while (low < high)
        {
            int mid = (low + high) >> 1;
            if (_Frames[mid].Offset < progress)
            {
                low = mid + 1;
            }
            else
            {
                high = mid;
            }
        }

        toIndex = low;
        fromIndex = toIndex - 1;
        float fromOffset = fromIndex >= 0 ? _Frames[fromIndex].Offset : 0f;
        float toOffset = _Frames[toIndex].Offset;
        float span = toOffset - fromOffset;
        localProgress = span <= 0f ? 1f : Math.Clamp((progress - fromOffset) / span, 0f, 1f);
    }

    public IEnumerator<UIKeyFrame<T>> GetEnumerator() => _Frames.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}