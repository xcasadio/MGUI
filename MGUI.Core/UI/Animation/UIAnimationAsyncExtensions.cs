using System.Threading;
using System.Threading.Tasks;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// Awaitable sugar over <see cref="UIAnimationCollection.StartAsync"/> (ADR-0011 decision 2), for an animation that is not built through the
/// fluent API (<see cref="UIAnimationBuilder.PlayAsync"/> covers that case): a storyboard, a sequence, or any <see cref="UIAnimation"/> built
/// by hand.
/// </summary>
public static class UIAnimationAsyncExtensions
{
    /// <summary>Starts <paramref name="animation"/> on <paramref name="owner"/> (like <c>owner.Animations.Start(animation)</c>) and returns a
    /// task that resolves <see langword="true"/> when the run reaches <see cref="UIAnimationState.Completed"/> and <see langword="false"/>
    /// when it is cancelled for any reason, never throwing for a cancellation. See <see cref="UIAnimationCollection.StartAsync"/> for the
    /// full contract (refusals, the cancellation token, restart behaviour, the thread of the continuation).</summary>
    /// <exception cref="ArgumentNullException"><paramref name="animation"/> or <paramref name="owner"/> is null.</exception>
    public static Task<bool> PlayAsync(this UIAnimation animation, MGElement owner, CancellationToken cancellationToken = default)
    {
        if (animation == null)
        {
            throw new ArgumentNullException(nameof(animation));
        }

        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        return owner.Animations.StartAsync(animation, cancellationToken);
    }
}

/// <summary>
/// The per-call state backing one <see cref="UIAnimationCollection.StartAsync"/> run (ADR-0011 decision 2): subscribes to the animation's
/// <see cref="UIAnimation.Completed"/> and <see cref="UIAnimation.Cancelled"/> events before it starts, optionally registers a
/// <see cref="CancellationToken"/> once the run is confirmed still unresolved, and resolves its <see cref="TaskCompletionSource{TResult}"/>
/// exactly once, always on the update thread (from those two event handlers, or from <see cref="UIAnimationManager"/> draining a queued
/// cancellation request at the head of a tick).<para/>
/// The <see cref="TaskCompletionSource{TResult}"/> is created without <see cref="TaskCreationOptions.RunContinuationsAsynchronously"/>: with
/// no <see cref="SynchronizationContext"/> installed (a MonoGame DesktopGL loop), the <see langword="await"/> continuation therefore runs
/// inline, on the update thread, during the same tick as the resolving event -- exactly like another subscriber of that event would.
/// </summary>
internal sealed class UIAnimationCompletion
{
    private readonly UIAnimation _Animation;
    private readonly TaskCompletionSource<bool> _Source = new();
    private CancellationTokenRegistration _Registration;
    private bool _Resolved;

    public UIAnimationCompletion(UIAnimation animation)
    {
        _Animation = animation;
        animation.Completed += OnCompleted;
        animation.Cancelled += OnCancelled;
    }

    /// <summary>The animation this completion follows.</summary>
    public UIAnimation Animation => _Animation;

    /// <summary>The task handed to the caller of <see cref="UIAnimationCollection.StartAsync"/>.</summary>
    public Task<bool> Task => _Source.Task;

    /// <summary>True once <see cref="Resolve"/> has run: a later event or a stale queued cancellation request is then a no-op.</summary>
    public bool IsResolved => _Resolved;

    /// <summary>Registers <paramref name="cancellationToken"/>: its callback runs on whatever thread cancels it and only enqueues this
    /// completion on the animation's <see cref="UIAnimationManager"/> (<see cref="UIAnimationManager.RequestCancellation"/>), never touching
    /// the engine itself. Called only once the caller has confirmed the run is still unresolved right after starting it.</summary>
    public void Register(CancellationToken cancellationToken)
        => _Registration = cancellationToken.Register(static state => ((UIAnimationCompletion)state).EnqueueCancellationRequest(), this);

    private void EnqueueCancellationRequest() => _Animation.Manager?.RequestCancellation(this);

    private void OnCompleted(object sender, EventArgs e) => Resolve(true);

    private void OnCancelled(object sender, EventArgs e) => Resolve(false);

    /// <summary>Unsubscribes both events without resolving anything: used when <see cref="UIAnimationManager.Start"/> throws right after
    /// this instance subscribed, before anything started.</summary>
    public void Unsubscribe()
    {
        _Animation.Completed -= OnCompleted;
        _Animation.Cancelled -= OnCancelled;
    }

    /// <summary>Idempotent: unsubscribes both events, releases the token registration with <see cref="CancellationTokenRegistration.Unregister"/>
    /// (non-blocking; never <see cref="IDisposable.Dispose"/>, which would wait for a callback already running), marks this instance
    /// resolved, THEN sets the task's result last -- a continuation may run inline from that call and start a new animation.</summary>
    public void Resolve(bool result)
    {
        if (_Resolved)
        {
            return;
        }

        Unsubscribe();
        _Registration.Unregister();
        _Resolved = true;
        _Source.TrySetResult(result);
    }
}
