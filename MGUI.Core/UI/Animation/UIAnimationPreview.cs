using MGUI.Core.UI.Animation.Composition;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// Attaches a <see cref="UIAnimation"/> to an element as a preview instance for an external host (a timeline editor's scrubber; S3/V3,
/// ADR-0008 decision 5b; Docs/Tasks/animation-v3-tasks.md U7): begun without a <see cref="UIAnimationManager"/> (<see cref="UIAnimation.Manager"/>
/// stays null), so it is never registered, ticked, swept, or cancelled by element detachment or window close -- the host owns the
/// element's and the animation's lifetime entirely. <see cref="UIAnimation.Seek"/> positions it forward or backward at any time, writing
/// the value with no event and never reaching <see cref="UIAnimationState.Completed"/> (past its end it holds the final pose until
/// <see cref="Detach"/>). A composite (<see cref="UIAnimationGroup"/>) attaches every child the same way, recursively, on
/// <c>child.Owner ?? element</c>.<para/>
/// Limit (documented, checked only at attach time): a preview and a live animation must never coexist on the same (element, path) --
/// both would write the same target through the same <see cref="UIAnimation{T}.ApplyProgress"/>/<see cref="IUIAnimationTarget{T}"/>
/// codepath, so whichever ticks or seeks last on a given frame wins that frame, an unstable fight neither side can detect.
/// <see cref="Attach{TAnimation}"/> refuses when a live animation already occupies the path (<see cref="UIAnimationCollection.IsAnimating"/>,
/// an existing cheap dictionary lookup, no new state); the reverse is not guarded (and cannot be, without teaching
/// <see cref="UIAnimationManager"/> about previews, which would defeat the point of a preview being invisible to it): starting a live
/// animation on the same path after a preview was attached proceeds exactly as if the preview did not exist, and the two then fight over
/// the value every frame until the preview is detached.
/// </summary>
public static class UIAnimationPreview
{
    /// <summary>Attaches <paramref name="animation"/> to <paramref name="element"/> as a preview instance, positioned at its initial pose
    /// (a <c>Seek(TimeSpan.Zero)</c> right after the attach: progress 0, the <c>From</c>/start value). Sets
    /// <see cref="UIAnimation.InheritsBaseValue"/> to false (the run's base is the value it is previewing over, read now, never inherited
    /// from a replaced run -- there is none, a preview is never replaced). Returns <paramref name="animation"/>, for chaining.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="element"/> or <paramref name="animation"/> is null.</exception>
    /// <exception cref="InvalidOperationException"><paramref name="animation"/> is already attached (as a preview or for real) or
    /// registered with a live <see cref="UIAnimationManager"/>, or a live animation is already active on <paramref name="element"/> at
    /// <paramref name="animation"/>'s path (or, for a composite, at a child's path on its own owner).</exception>
    public static TAnimation Attach<TAnimation>(MGElement element, TAnimation animation) where TAnimation : UIAnimation
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (animation == null)
        {
            throw new ArgumentNullException(nameof(animation));
        }

        if (animation.Manager != null)
        {
            throw new InvalidOperationException(
                $"{animation.GetType().Name} is registered with a live {nameof(UIAnimationManager)}: a preview instance must never coexist with a live one.");
        }

        if (animation.State != UIAnimationState.Stopped)
        {
            throw new InvalidOperationException($"{animation.GetType().Name} is already attached (state {animation.State}): a preview instance is attached once.");
        }

        EnsureNoLiveConflict(element, animation);

        animation.InheritsBaseValue = false;
        animation.BeginPreview(element);
        animation.Seek(TimeSpan.Zero);
        return animation;
    }

    /// <summary>Ends a preview: a convenience alias for <see cref="UIAnimation.Cancel"/>, which applies
    /// <see cref="UIAnimation.CancelBehavior"/> (<see cref="UIAnimationCancelBehavior.RestoreBaseValue"/> by default) and raises
    /// <see cref="UIAnimation.Cancelled"/> exactly as it does for a live animation.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="animation"/> is null.</exception>
    public static void Detach(UIAnimation animation)
        => (animation ?? throw new ArgumentNullException(nameof(animation))).Cancel();

    private static void EnsureNoLiveConflict(MGElement element, UIAnimation animation)
    {
        var path = animation.TargetKey;
        if (!string.IsNullOrWhiteSpace(path) && element.Animations.IsAnimating(path))
        {
            throw new InvalidOperationException(
                $"A live animation is already active on {element.GetType().Name}.{path}: a preview instance must never coexist with a live one on the same path.");
        }

        if (animation is UIAnimationGroup group)
        {
            foreach (var child in group.Children)
            {
                EnsureNoLiveConflict(child.Owner ?? element, child);
            }
        }
    }
}
