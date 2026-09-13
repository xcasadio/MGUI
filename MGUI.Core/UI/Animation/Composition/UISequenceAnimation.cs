namespace MGUI.Core.UI.Animation.Composition;

/// <summary>
/// A sequential group (ADR-0007, decision 1): the children play one after the other, each starting at the time offset where the previous one
/// ends on the sequence's own timeline (delay and repeats of each child included), so the schedule is deterministic even when a child is
/// replaced early by a conflicting animation. The sequence completes when the last child's slot ends.
/// <code>
/// var sequence = new UISequenceAnimation().Append(fadeIn).AppendDelay(TimeSpan.FromMilliseconds(200)).Append(move).Append(fadeOut);
/// element.Animations.Start(sequence);
/// </code>
/// A child that repeats forever is refused: repeat the sequence instead.
/// </summary>
public sealed class UISequenceAnimation : UIAnimationGroup, IEnumerable<UIAnimation>
{
    private readonly List<TimeSpan> _StartOffsets = new();
    private int _NextChild;

    public UISequenceAnimation()
    {
        Repeated += (_, _) => _NextChild = 0;
    }

    /// <summary>Adds a child at the end of the sequence.</summary>
    public UISequenceAnimation Append(UIAnimation child)
    {
        Add(child);
        return this;
    }

    /// <summary>Adds a pause at the end of the sequence.</summary>
    public UISequenceAnimation AppendDelay(TimeSpan delay)
    {
        Add(new UIDelayAnimation(delay));
        return this;
    }

    /// <summary>The time offset, on the sequence's timeline, at which the child at <paramref name="index"/> starts.</summary>
    public TimeSpan GetStartOffset(int index) => _StartOffsets[index];

    protected override TimeSpan ComputeDuration()
    {
        _StartOffsets.Clear();
        var cursor = TimeSpan.Zero;
        foreach (var child in Children)
        {
            var length = LengthOf(child);
            if (length == TimeSpan.MaxValue)
            {
                throw new InvalidOperationException($"A child of a {nameof(UISequenceAnimation)} cannot repeat forever ({child}); set {nameof(RepeatForever)} on the sequence instead.");
            }

            _StartOffsets.Add(cursor);
            cursor += length;
        }

        _NextChild = 0;
        return cursor;
    }

    protected override void StartDueChildren(float progress)
    {
        // The exact tick position on the timeline (not a round-trip through the float progress), so a child scheduled at an offset
        // starts on the very frame the timeline reaches it.
        var timeline = IterationElapsed;
        while (_NextChild < Children.Count && _StartOffsets[_NextChild] <= timeline)
        {
            StartChild(Children[_NextChild]);
            _NextChild++;
        }
    }

    public IEnumerator<UIAnimation> GetEnumerator() => Children.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}