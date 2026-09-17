using System.Threading;
using System.Threading.Tasks;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// The animations owned by one <see cref="MGElement"/> (<c>element.Animations</c>; ADR-0006 decision 9). Starting an animation here
/// binds it to the element and registers it with the desktop's <see cref="UIAnimationManager"/>; the element's animations are cancelled
/// (base values restored, held contributions released) when the element leaves the tree or its displaying window closes.
/// Allocated on first access: an element that never animates carries no collection.
/// </summary>
public sealed class UIAnimationCollection
{
    internal UIAnimationCollection(MGElement owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>The element that owns these animations.</summary>
    public MGElement Owner { get; }

    private UIAnimationManager Manager => Owner.SelfOrParentWindow?.Desktop?.Animations;

    /// <summary>Starts (or restarts) <paramref name="animation"/> on <see cref="Owner"/>. See <see cref="UIAnimationManager.Start"/> for the conflict rule.</summary>
    /// <exception cref="InvalidOperationException">The element has no desktop.</exception>
    public void Start(UIAnimation animation)
    {
        if (animation == null)
        {
            throw new ArgumentNullException(nameof(animation));
        }

        var manager = Manager ?? throw new InvalidOperationException(
            $"The element has no desktop: an animation is ticked by the {nameof(UIAnimationManager)} of the desktop of its window.");
        manager.Start(Owner, animation);
    }

    /// <summary>A completed, cached <see cref="Task{TResult}"/> of <see langword="false"/>, returned by <see cref="StartAsync"/> for a
    /// <see cref="CancellationToken"/> that is already cancelled at the call.</summary>
    private static readonly Task<bool> AlreadyCancelledTask = Task.FromResult(false);

    /// <summary>Starts (or restarts) <paramref name="animation"/> on <see cref="Owner"/>, like <see cref="Start"/>, and returns a task that
    /// resolves <see langword="true"/> once the run reaches <see cref="UIAnimationState.Completed"/> and <see langword="false"/> when it is
    /// cancelled for any reason (a replacement on the same (element, path), the element leaving the tree, its window closing,
    /// <see cref="UIAnimation.Cancel"/>, or <paramref name="cancellationToken"/>) -- never throwing for a cancellation, since one is routine
    /// in a UI and an exception escaping an <c>async void</c> handler would bring the game down (ADR-0011 decision 2). For a composite
    /// (<see cref="Composition.UIAnimationGroup"/>) the result is that of the composite itself, whatever happens to its children. Restarting
    /// this same instance on the same (element, path) (<see cref="UIAnimation.Restart"/>) raises no <see cref="UIAnimation.Cancelled"/> (see
    /// <see cref="UIAnimationManager.Start"/>): the task keeps following the restarted run and resolves at its own end, not at the original
    /// end time. Restarting it on another element or path cancels this run's previous slot and resolves this task <see langword="false"/>.
    /// A <see cref="UIAnimation.RepeatForever"/> run only ever resolves through a cancellation.<para/>
    /// <paramref name="cancellationToken"/>'s callback runs on whatever thread cancels it and never touches the engine directly: it queues a
    /// cancellation request that <see cref="UIAnimationManager.Update"/> drains at the very top of the next tick, before the clock advances
    /// and therefore even while <see cref="UIAnimationClock.IsPaused"/>; a token already cancelled at this call resolves
    /// <see langword="false"/> at once, without starting anything. Resolution always happens on the update thread; the
    /// <see cref="TaskCompletionSource{TResult}"/> backing the task is created without <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>,
    /// so with no <see cref="SynchronizationContext"/> installed (a MonoGame DesktopGL loop) the <see langword="await"/> continuation runs
    /// inline on that thread, during the tick that resolves the run, like another <see cref="UIAnimation.Completed"/> subscriber would; the
    /// task stays pending while its desktop is not updated.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="animation"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The element has no desktop; <paramref name="animation"/> is a preview instance (a preview
    /// is driven by <see cref="UIAnimation.Seek"/>, never awaited); or <paramref name="animation"/> is already active (await the run that
    /// started it, or cancel it, first).</exception>
    public Task<bool> StartAsync(UIAnimation animation, CancellationToken cancellationToken = default)
    {
        if (animation == null)
        {
            throw new ArgumentNullException(nameof(animation));
        }

        if (animation.IsPreview)
        {
            throw new InvalidOperationException($"{nameof(StartAsync)} cannot await a preview instance: a preview is driven by {nameof(UIAnimation.Seek)}, never awaited.");
        }

        if (animation.IsActive)
        {
            throw new InvalidOperationException($"{nameof(StartAsync)} cannot start an already active animation: await the run that started it, or cancel it, first.");
        }

        var manager = Manager ?? throw new InvalidOperationException(
            $"The element has no desktop: an animation is ticked by the {nameof(UIAnimationManager)} of the desktop of its window.");

        if (cancellationToken.IsCancellationRequested)
        {
            return AlreadyCancelledTask;
        }

        UIAnimationCompletion completion = new(animation);
        try
        {
            manager.Start(Owner, animation);
        }
        catch
        {
            completion.Unsubscribe();
            throw;
        }

        if (!completion.IsResolved && cancellationToken.CanBeCanceled)
        {
            completion.Register(cancellationToken);
        }

        return completion.Task;
    }

    /// <summary>Number of active animations owned by the element.</summary>
    public int Count => Manager?.CountOwnedBy(Owner) ?? 0;

    /// <summary>True while an animation is active on <paramref name="path"/>.</summary>
    public bool IsAnimating(string path) => Manager?.IsAnimating(Owner, path) ?? false;

    /// <summary>The active animations owned by the element (a snapshot).</summary>
    public IEnumerable<UIAnimation> Active => Manager?.EnumerateOwnedBy(Owner) ?? Enumerable.Empty<UIAnimation>();

    /// <summary>The completed animations still holding their <c>Animation</c> contribution in the resolved value store (a snapshot).</summary>
    public IEnumerable<UIAnimation> Held => Manager?.EnumerateHeldBy(Owner) ?? Enumerable.Empty<UIAnimation>();

    /// <summary>Cancels every active animation of the element according to its own <see cref="UIAnimation.CancelBehavior"/>. Held contributions stay.</summary>
    public void CancelAll() => Manager?.CancelOwnedBy(Owner, restoreBaseValue: false, releaseHolds: false);

    /// <summary>Cancels every active animation of the element, restores every base value and releases the held contributions:
    /// the element is left as if it had never been animated. This is what detachment and window closing do.</summary>
    public void Clear() => Manager?.CancelOwnedBy(Owner, restoreBaseValue: true, releaseHolds: true);
}