namespace MGUI.Core.UI.Animation.Composition;

/// <summary>
/// A parallel group (ADR-0007, decision 1): every child starts when the storyboard starts and the storyboard completes when its own
/// timeline, the longest child (delay and repeats included), ends.
/// <code>
/// var storyboard = new UIStoryboard { fade, scale };
/// popup.Animations.Start(storyboard);
/// </code>
/// A child that repeats forever is refused: repeat the storyboard instead.
/// </summary>
public sealed class UIStoryboard : UIAnimationGroup, IEnumerable<UIAnimation>
{
    private bool _Started;

    public UIStoryboard()
    {
        Repeated += (_, _) => _Started = false;
    }

    protected override TimeSpan ComputeDuration()
    {
        var longest = TimeSpan.Zero;
        foreach (var child in Children)
        {
            var length = LengthOf(child);
            if (length == TimeSpan.MaxValue)
            {
                throw new InvalidOperationException($"A child of a {nameof(UIStoryboard)} cannot repeat forever ({child}); set {nameof(RepeatForever)} on the storyboard instead.");
            }

            if (length > longest)
            {
                longest = length;
            }
        }

        _Started = false;
        return longest;
    }

    protected override void StartDueChildren(float progress)
    {
        if (_Started)
        {
            return;
        }

        _Started = true;
        for (var i = 0; i < Children.Count; i++)
        {
            StartChild(Children[i]);
        }
    }

    public IEnumerator<UIAnimation> GetEnumerator() => Children.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}