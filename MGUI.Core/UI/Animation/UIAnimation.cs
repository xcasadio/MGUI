namespace MGUI.Core.UI.Animation;

/// <summary>
/// Base class of every animation of the engine (Docs/Tasks/animation-tasks.md, S3; ADR-0006): playback settings (<see cref="Duration"/>,
/// <see cref="Delay"/>, <see cref="RepeatCount"/> / <see cref="RepeatForever"/>, <see cref="AutoReverse"/>, <see cref="FillBehavior"/>,
/// <see cref="CancelBehavior"/>), the state machine (<see cref="State"/>, <see cref="Play"/>, <see cref="Pause"/>, <see cref="Resume"/>,
/// <see cref="Cancel"/>, <see cref="Restart"/>) and the events.<para/>
/// An animation belongs to the <see cref="MGElement"/> that started it (<see cref="UIAnimationCollection.Start"/>, decision 9): it is ticked
/// by the <see cref="UIAnimationManager"/> of the element's desktop, whatever the element's visibility, and cancelled when the element leaves
/// the tree or its window closes. Timing: <see cref="Delay"/>, then one pass of <see cref="Duration"/> (two with <see cref="AutoReverse"/>,
/// forth and back) repeated <see cref="RepeatCount"/> more times; the value written at each tick is the interpolation of the start and end
/// values at the eased <see cref="Progress"/> (see <see cref="UIAnimation{T}"/>). Event order: <see cref="Started"/> (once the delay has
/// elapsed), <see cref="Updated"/> at every tick, <see cref="Repeated"/> / <see cref="Reversed"/> when a pass begins, then either
/// <see cref="Completed"/> or <see cref="Cancelled"/>.<para/>
/// Ticks allocate nothing; the events use <see cref="EventArgs.Empty"/>.
/// </summary>
public abstract class UIAnimation
{
    private TimeSpan _Duration;
    /// <summary>Length of one pass. Zero completes the animation at its first tick with the end value. Default: zero.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan Duration
    {
        get => _Duration;
        set => _Duration = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(Duration)} cannot be negative.");
    }

    private TimeSpan _Delay;
    /// <summary>Time to wait after <see cref="Play"/> before the first value is written (state <see cref="UIAnimationState.Delayed"/>). Default: zero.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public TimeSpan Delay
    {
        get => _Delay;
        set => _Delay = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(Delay)} cannot be negative.");
    }

    private int _RepeatCount;
    /// <summary>Number of additional iterations after the first one (0 = play once). Ignored when <see cref="RepeatForever"/> is true. Default: 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    public int RepeatCount
    {
        get => _RepeatCount;
        set => _RepeatCount = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(RepeatCount)} cannot be negative.");
    }

    /// <summary>Repeats until cancelled (loading indicators, pulses). Default: false.</summary>
    public bool RepeatForever { get; set; }

    /// <summary>Plays each iteration forth then back (0 -> 1 -> 0), so a completed animation ends on its start value. Default: false.</summary>
    public bool AutoReverse { get; set; }

    /// <summary>What happens to the value at completion. Default: <see cref="UIAnimationFillBehavior.HoldEnd"/> (decision 10).</summary>
    public UIAnimationFillBehavior FillBehavior { get; set; } = UIAnimationFillBehavior.HoldEnd;

    /// <summary>What happens to the value at cancellation. Default: <see cref="UIAnimationCancelBehavior.RestoreBaseValue"/> (decision 10).</summary>
    public UIAnimationCancelBehavior CancelBehavior { get; set; } = UIAnimationCancelBehavior.RestoreBaseValue;

    /// <summary>Optional name, recorded as the source name of store-backed writes and shown by the diagnostics.</summary>
    public string Name { get; set; }

    /// <summary>When true (the default), an animation that replaces an active one on the same path inherits its base value, so a chain of
    /// replacements still restores the true base. A transition run sets it to false: its base is the value it heads to, read at start.</summary>
    public bool InheritsBaseValue { get; set; } = true;

    /// <summary>The current playback state.</summary>
    public UIAnimationState State { get; private set; } = UIAnimationState.Stopped;

    /// <summary>Raw progress of the current pass in [0, 1] (before easing; decreasing while <see cref="IsReversing"/>).</summary>
    public float Progress { get; private set; }

    /// <summary>Zero-based index of the current iteration.</summary>
    public int Iteration { get; private set; }

    /// <summary>True during the backward pass of an <see cref="AutoReverse"/> iteration.</summary>
    public bool IsReversing { get; private set; }

    /// <summary>Scaled time elapsed since <see cref="Play"/>, delay included.</summary>
    public TimeSpan Elapsed { get; private set; }

    /// <summary>Exact time elapsed within the current iteration (delay excluded), the tick-precise counterpart of <see cref="Progress"/>.</summary>
    public TimeSpan IterationElapsed { get; private set; }

    /// <summary>The element that started this animation, null until then.</summary>
    public MGElement Owner { get; private set; }

    /// <summary>The window that displayed <see cref="Owner"/> when the animation started: closing it cancels the animation.</summary>
    internal MGWindow OwnerWindow { get; private set; }

    internal UIAnimationManager Manager { get; private set; }

    /// <summary>The (owner, path) key under which the manager registered this animation, null when it is not registered; lets a restart on
    /// another owner or path clean its previous entry up (review finding, S3).</summary>
    internal (MGElement Owner, string Path)? RegisteredKey { get; set; }

    /// <summary>True while the animation occupies its target: <see cref="UIAnimationState.Delayed"/>, <see cref="UIAnimationState.Running"/> or <see cref="UIAnimationState.Paused"/>.</summary>
    public bool IsActive => State is UIAnimationState.Delayed or UIAnimationState.Running or UIAnimationState.Paused;

    /// <summary>True after a completion with <see cref="UIAnimationFillBehavior.HoldEnd"/> on a store-backed target, while the <c>Animation</c>
    /// contribution is still in the resolved value store (released by the next animation on the same path or by <see cref="UIAnimationCollection.Clear"/>).</summary>
    public bool IsHeld { get; internal set; }

    private UIAnimationState _StateBeforePause;

    /// <summary>Raised once the delay has elapsed and the first value is about to be written.</summary>
    public event EventHandler Started;
    /// <summary>Raised after every value write.</summary>
    public event EventHandler Updated;
    /// <summary>Raised when a new iteration begins (<see cref="RepeatCount"/> / <see cref="RepeatForever"/>).</summary>
    public event EventHandler Repeated;
    /// <summary>Raised when the backward pass of an <see cref="AutoReverse"/> iteration begins.</summary>
    public event EventHandler Reversed;
    /// <summary>Raised when the animation reaches its end, after the fill behaviour has been applied.</summary>
    public event EventHandler Completed;
    /// <summary>Raised when the animation is cancelled, after the cancel behaviour has been applied.</summary>
    public event EventHandler Cancelled;

    /// <summary>Starts the animation on its <see cref="Owner"/> (a stopped, completed or cancelled animation starts over; a paused one resumes;
    /// a running one is left alone). An animation that has never been started has no owner: use <see cref="UIAnimationCollection.Start"/>.</summary>
    /// <exception cref="InvalidOperationException">The animation has no owner yet.</exception>
    public void Play()
    {
        if (State == UIAnimationState.Paused)
        {
            Resume();
            return;
        }

        if (IsActive)
        {
            return;
        }

        RequireOwner().Animations.Start(this);
    }

    /// <summary>Freezes the animation; the current value stays applied.</summary>
    public void Pause()
    {
        if (State is UIAnimationState.Running or UIAnimationState.Delayed)
        {
            _StateBeforePause = State;
            State = UIAnimationState.Paused;
        }
    }

    /// <summary>Resumes a paused animation where it stopped.</summary>
    public void Resume()
    {
        if (State == UIAnimationState.Paused)
        {
            State = _StateBeforePause;
        }
    }

    /// <summary>Cancels the animation according to <see cref="CancelBehavior"/>. Does nothing when it is not active.</summary>
    public void Cancel() => CancelCore(CancelBehavior);

    /// <summary>Starts the animation over from its beginning on its <see cref="Owner"/>, without restoring or snapping the value.</summary>
    /// <exception cref="InvalidOperationException">The animation has no owner yet.</exception>
    public void Restart() => RequireOwner().Animations.Start(this);

    internal void CancelCore(UIAnimationCancelBehavior behavior)
    {
        if (!IsActive)
        {
            return;
        }

        State = UIAnimationState.Cancelled;
        if (behavior == UIAnimationCancelBehavior.RestoreBaseValue)
        {
            OnRestoreBaseValue();
        }

        Cancelled?.Invoke(this, EventArgs.Empty);
        Manager?.NotifyFinished(this);
    }

    internal void RefreshOwnerWindow()
    {
        if (Owner != null)
        {
            OwnerWindow = Owner.DisplayingWindow ?? Owner.SelfOrParentWindow;
        }
    }

    /// <summary>Called by the manager when the animation (re)starts: binds the owner, captures the start and base values, writes the first value.</summary>
    internal void Begin(MGElement owner, UIAnimationManager manager, object inheritedBase)
    {
        Owner = owner;
        OwnerWindow = owner.DisplayingWindow ?? owner.SelfOrParentWindow;
        Manager = manager;
        Elapsed = TimeSpan.Zero;
        IterationElapsed = TimeSpan.Zero;
        Progress = 0f;
        Iteration = 0;
        IsReversing = false;
        IsHeld = false;

        OnStarting(inheritedBase);

        if (Delay > TimeSpan.Zero)
        {
            State = UIAnimationState.Delayed;
            return;
        }

        State = UIAnimationState.Running;
        Started?.Invoke(this, EventArgs.Empty);
        ApplyProgress(0f);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Advances by one scaled frame. Called by the manager.</summary>
    internal void Advance(TimeSpan delta)
    {
        if (State != UIAnimationState.Running && State != UIAnimationState.Delayed)
        {
            return;
        }

        Elapsed += delta;

        if (State == UIAnimationState.Delayed)
        {
            if (Elapsed < Delay)
            {
                return;
            }

            State = UIAnimationState.Running;
            Started?.Invoke(this, EventArgs.Empty);
        }

        long passTicks = Duration.Ticks;
        if (passTicks <= 0)
        {
            Complete(AutoReverse ? 0f : 1f);
            return;
        }

        long localTicks = Math.Max(0L, (Elapsed - Delay).Ticks);
        long iterationTicks = AutoReverse ? passTicks * 2 : passTicks;
        long iteration = localTicks / iterationTicks;
        if (!RepeatForever && iteration > RepeatCount)
        {
            Complete(AutoReverse ? 0f : 1f);
            return;
        }

        long withinIteration = localTicks - iteration * iterationTicks;
        IterationElapsed = TimeSpan.FromTicks(withinIteration);
        float progress = (float)((double)withinIteration / passTicks);
        bool reversing = false;
        if (AutoReverse && progress > 1f)
        {
            progress = 2f - progress;
            reversing = true;
        }

        if (iteration != Iteration)
        {
            Iteration = (int)Math.Min(iteration, int.MaxValue);
            IsReversing = false;
            Repeated?.Invoke(this, EventArgs.Empty);
        }

        if (reversing != IsReversing)
        {
            IsReversing = reversing;
            if (reversing)
            {
                Reversed?.Invoke(this, EventArgs.Empty);
            }
        }

        Progress = progress;
        ApplyProgress(progress);
        Updated?.Invoke(this, EventArgs.Empty);
    }

    private void Complete(float finalProgress)
    {
        Progress = finalProgress;
        ApplyProgress(finalProgress);
        Updated?.Invoke(this, EventArgs.Empty);

        State = UIAnimationState.Completed;
        if (FillBehavior == UIAnimationFillBehavior.RestoreBaseValue)
        {
            OnRestoreBaseValue();
        }
        else if (IsStoreBacked)
        {
            IsHeld = true;
        }

        Completed?.Invoke(this, EventArgs.Empty);
        Manager?.NotifyFinished(this);
    }

    private MGElement RequireOwner()
        => Owner ?? throw new InvalidOperationException(
            $"This animation has no owner yet: start it with element.{nameof(MGElement.Animations)}.{nameof(UIAnimationCollection.Start)}(animation).");

    /// <summary>True when the target writes through the resolved value store (pilot property).</summary>
    protected internal abstract bool IsStoreBacked { get; }

    /// <summary>The property path animated, the key of the conflict rule (one active animation per owner and path).</summary>
    protected internal abstract string TargetKey { get; }

    /// <summary>The base value captured at start, boxed, or null; handed to the animation that replaces this one so the true base survives a chain of replacements.</summary>
    protected internal abstract object BaseValueBoxed { get; }

    /// <summary>Resolves the target, captures the start value (the current value when <c>From</c> is not set) and the base value
    /// (<paramref name="inheritedBase"/> when a replaced animation hands it over, the current value otherwise).</summary>
    protected internal abstract void OnStarting(object inheritedBase);

    /// <summary>Writes the value for the given raw progress (easing is applied here).</summary>
    protected internal abstract void ApplyProgress(float progress);

    /// <summary>Restores the base value (see <see cref="IUIAnimationTarget{T}.RestoreBaseValue"/>).</summary>
    protected internal abstract void OnRestoreBaseValue();

    /// <summary>Releases a held <c>Animation</c> contribution (store-backed targets only).</summary>
    protected internal abstract void OnReleaseHold();

    public override string ToString()
        => $"{GetType().Name}{(string.IsNullOrEmpty(Name) ? "" : " '" + Name + "'")} [{TargetKey}] {State} {Progress:0.00}";
}