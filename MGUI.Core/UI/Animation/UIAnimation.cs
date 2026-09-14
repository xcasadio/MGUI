namespace MGUI.Core.UI.Animation;

/// <summary>
/// Base class of every animation of the engine (ADR-0006): playback settings (<see cref="Duration"/>,
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

    /// <summary>Presets <see cref="Owner"/> before this instance ever starts (ADR-0008 decision 8): lets a <see cref="Composition.UIAnimationGroup"/>
    /// child destined for another element be positioned there without the Start-then-Cancel idiom <c>CompositionTests</c> used before this
    /// (start it for real on the other element, then <see cref="Cancel"/> it, leaving <see cref="Owner"/> set but <see cref="State"/> at
    /// <see cref="UIAnimationState.Cancelled"/>) -- used by <see cref="KeyFrames.UIAnimationSerializer.Deserialize"/> and directly by an
    /// application composing a tree by hand. Both <see cref="Composition.UIAnimationGroup.StartChild"/> and
    /// <see cref="Composition.UIAnimationGroup.OnPreviewAttached"/> already read <c>child.Owner ?? root</c>, so presetting here is enough
    /// for both live playback and a preview attach.</summary>
    /// <exception cref="InvalidOperationException">This instance is not <see cref="UIAnimationState.Stopped"/>: preset the owner only
    /// before the animation starts.</exception>
    internal void PresetOwner(MGElement owner)
    {
        if (State != UIAnimationState.Stopped)
        {
            throw new InvalidOperationException(
                $"{GetType().Name} cannot preset its owner while its state is {State}: preset the owner only before the animation starts.");
        }

        Owner = owner;
    }

    /// <summary>The window that displayed <see cref="Owner"/> when the animation started: closing it cancels the animation.</summary>
    internal MGWindow OwnerWindow { get; private set; }

    internal UIAnimationManager Manager { get; private set; }

    /// <summary>True once <see cref="BeginPreview"/> has begun this instance (<see cref="UIAnimationPreview.Attach{TAnimation}"/>):
    /// a preview is never registered with a <see cref="UIAnimationManager"/> (<see cref="Manager"/> stays null) and is the only kind
    /// of instance <see cref="Seek"/> accepts. Cleared by <see cref="Begin"/>, so an instance that is later started for real through
    /// <see cref="UIAnimationCollection.Start"/> stops being a preview.</summary>
    internal bool IsPreview { get; private set; }

    /// <summary>The (owner, path) key under which the manager registered this animation, null when it is not registered; lets a restart on
    /// another owner or path clean its previous entry up (review finding).</summary>
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
        IsPreview = false;

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

    /// <summary>Called by <see cref="UIAnimationPreview.Attach{TAnimation}"/>: begins this instance as a preview, without a manager
    /// (<see cref="Manager"/> stays null) and forced straight to <see cref="UIAnimationState.Running"/> (a preview has no
    /// <see cref="UIAnimationState.Delayed"/> state of its own: <see cref="Seek"/> treats the delay purely through its maths). Raises no
    /// event and writes no value: the caller seeks to the initial pose right
    /// after (<see cref="UIAnimationPreview.Attach{TAnimation}"/> calls <c>Seek(TimeSpan.Zero)</c>).<para/>
    /// <see cref="OnPreviewAttached"/> runs before <see cref="OnStarting"/>: a <see cref="Composition.UIAnimationGroup"/>
    /// computes its own <see cref="Duration"/> from its children's <see cref="Duration"/> inside <see cref="OnStarting"/>
    /// (<see cref="Composition.UIAnimationGroup.ComputeDuration"/> reading <see cref="Composition.UIAnimationGroup.LengthOf"/>), and a
    /// child that is itself a group only has a real <see cref="Duration"/> once its own <see cref="OnStarting"/> has run -- which
    /// <see cref="OnPreviewAttached"/> triggers recursively through <see cref="BeginPreview"/> on every descendant. Computing duration
    /// first (the previous order) always saw a nested group child's <see cref="Duration"/> at zero, so a storyboard or sequence containing
    /// a nested group previewed with its own total length clamped to zero and every <see cref="Seek"/> pinned at the initial pose.<para/>
    /// <see cref="IsPreview"/> and <see cref="State"/> are set only after both hooks return successfully (the same pattern
    /// as <see cref="Begin"/>, which sets <see cref="State"/> only after <see cref="OnStarting"/>): a hook that throws (an invalid
    /// <see cref="Composition.UIAnimationGroup"/> configuration, or a group whose child has no owner and no root) leaves this instance at
    /// its original <see cref="UIAnimationState.Stopped"/> and <see cref="IsPreview"/> false, so a corrected retry can attach it again,
    /// instead of stranding it as a half-attached preview that reports <see cref="UIAnimationState.Running"/> forever.<para/>
    /// Fix (ADR-0008): that same throw used to leave
    /// every CHILD that <see cref="OnPreviewAttached"/> had already begun stuck at <see cref="UIAnimationState.Running"/>/<see cref="IsPreview"/>
    /// forever too, since nothing rolled them back when the group's own <see cref="OnStarting"/> failed afterwards. Both hooks now run
    /// inside a try/catch that calls <see cref="RollbackFailedPreviewAttach"/> on a throw, before rethrowing: safe with no value to
    /// restore, since <see cref="OnPreviewAttached"/> writes nothing by itself (only the caller's <see cref="Seek"/> right after a
    /// successful <see cref="BeginPreview"/> ever does).</summary>
    internal void BeginPreview(MGElement owner)
    {
        Owner = owner;
        OwnerWindow = owner.DisplayingWindow ?? owner.SelfOrParentWindow;
        Manager = null;
        Elapsed = TimeSpan.Zero;
        IterationElapsed = TimeSpan.Zero;
        Progress = 0f;
        Iteration = 0;
        IsReversing = false;
        IsHeld = false;

        try
        {
            OnPreviewAttached(owner);
            OnStarting(null);
        }
        catch
        {
            RollbackFailedPreviewAttach();
            throw;
        }

        IsPreview = true;
        State = UIAnimationState.Running;
    }

    /// <summary>Called on this instance when its own <see cref="OnPreviewAttached"/> or <see cref="OnStarting"/> throws inside
    /// <see cref="BeginPreview"/>, walking every descendant already attached as a preview (<see cref="IsPreview"/>) back to
    /// <see cref="UIAnimationState.Stopped"/> (ADR-0008 decision 8). Default: a no-op (a leaf animation has no children to roll back);
    /// sealed-overridden by <see cref="Composition.UIAnimationGroup"/> to roll back every one of its own children that made it into a
    /// preview before the failure, recursively (a child that is itself a group rolls its own children back the same way).</summary>
    protected internal virtual void OnPreviewAttachFailed() { }

    /// <summary>Resets this instance -- and, through <see cref="OnPreviewAttachFailed"/>, every descendant already attached as a preview --
    /// back to <see cref="UIAnimationState.Stopped"/> and non-preview, without raising <see cref="Cancelled"/> (a failed attach was never a
    /// real run to cancel, and nothing was ever written for it to undo: see <see cref="BeginPreview"/>'s doc).</summary>
    internal void RollbackFailedPreviewAttach()
    {
        OnPreviewAttachFailed();

        OwnerWindow = null;
        Manager = null;
        Elapsed = TimeSpan.Zero;
        IterationElapsed = TimeSpan.Zero;
        Progress = 0f;
        Iteration = 0;
        IsReversing = false;
        IsHeld = false;
        IsPreview = false;
        State = UIAnimationState.Stopped;
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

        var completed = ComputeProgress(Elapsed - Delay, out var iteration, out var reversing, out var iterationElapsed, out var progress);
        if (completed)
        {
            Complete(progress);
            return;
        }

        IterationElapsed = iterationElapsed;

        if (iteration != Iteration)
        {
            Iteration = iteration;
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

    /// <summary>Positions a preview instance (<see cref="IsPreview"/>) at <paramref name="elapsed"/> since it began, forward or backward,
    /// clamped to [0, the animation's total length] (<see cref="Delay"/> plus every repeated pass; unbounded with
    /// <see cref="RepeatForever"/>): writes the value for that instant (<see cref="OnSeek"/>) with no event, no state change, and never
    /// reaching <see cref="UIAnimationState.Completed"/> (a preview past its end holds the final pose -- 1, or 0 after an even number of
    /// <see cref="AutoReverse"/> passes -- until <see cref="Cancel"/>). Within the delay,
    /// the pose is progress 0 (<see cref="UIAnimation{T}.From"/> or the start value), so a scrubber shows the initial pose. A composite
    /// (<see cref="Composition.UIAnimationGroup"/>) positions every child the same way, at its own elapsed time relative to its offset.
    /// Zero allocation.</summary>
    /// <exception cref="InvalidOperationException">This instance is not an active preview: it was never attached with
    /// <see cref="UIAnimationPreview.Attach{TAnimation}"/>, it is registered with a live <see cref="UIAnimationManager"/>, or its preview
    /// was already ended with <see cref="Cancel"/>.</exception>
    public void Seek(TimeSpan elapsed)
    {
        if (!IsPreview || !IsActive)
        {
            throw new InvalidOperationException(
                $"{nameof(Seek)} is reserved to a preview instance attached with {nameof(UIAnimationPreview)}.{nameof(UIAnimationPreview.Attach)}, " +
                $"while it is still active: this instance is {(IsPreview ? "no longer active (cancelled)" : State == UIAnimationState.Stopped ? "not attached" : "registered with a live manager")}.");
        }

        if (elapsed < TimeSpan.Zero)
        {
            elapsed = TimeSpan.Zero;
        }

        var maxElapsed = TotalLength();
        if (elapsed > maxElapsed)
        {
            elapsed = maxElapsed;
        }

        Elapsed = elapsed;

        if (elapsed < Delay)
        {
            Iteration = 0;
            IsReversing = false;
            IterationElapsed = TimeSpan.Zero;
            Progress = 0f;
            OnSeek(0f);
            return;
        }

        ComputeProgress(elapsed - Delay, out var iteration, out var reversing, out var iterationElapsed, out var progress);
        Iteration = iteration;
        IsReversing = reversing;
        IterationElapsed = iterationElapsed;
        Progress = progress;
        OnSeek(progress);
    }

    /// <summary>The maths shared by <see cref="Advance"/> (incremental, tick-based) and <see cref="Seek"/> (absolute): resolves
    /// <paramref name="elapsedSinceDelay"/> (<see cref="Elapsed"/> minus <see cref="Delay"/>, clamped to non-negative) into the raw
    /// iteration index, reversing flag, in-iteration elapsed time and eased-free progress. Returns true once
    /// <paramref name="elapsedSinceDelay"/> reaches or passes the animation's total length (ignored by <see cref="RepeatForever"/>): the
    /// out values then describe the canonical held pose of the last iteration's own end (its forward end for a plain pass, the tail of
    /// its backward leg with <see cref="AutoReverse"/>) rather than a value depending on how far past the end
    /// <paramref name="elapsedSinceDelay"/> reaches -- <see cref="Advance"/> ignores them in that case (it calls <see cref="Complete"/>
    /// instead, leaving <see cref="Iteration"/> and <see cref="IsReversing"/> at whatever the previous real tick left them, unchanged
    /// since before this extraction); <see cref="Seek"/> uses them as the frozen pose past the end.</summary>
    private bool ComputeProgress(TimeSpan elapsedSinceDelay, out int iteration, out bool reversing, out TimeSpan iterationElapsed, out float progress)
    {
        var passTicks = Duration.Ticks;
        if (passTicks <= 0)
        {
            iteration = 0;
            reversing = false;
            iterationElapsed = TimeSpan.Zero;
            progress = AutoReverse ? 0f : 1f;
            return true;
        }

        var localTicks = Math.Max(0L, elapsedSinceDelay.Ticks);
        var iterationTicks = AutoReverse ? passTicks * 2 : passTicks;
        var iterationIndex = localTicks / iterationTicks;
        if (!RepeatForever && iterationIndex > RepeatCount)
        {
            iteration = RepeatCount;
            iterationElapsed = TimeSpan.FromTicks(iterationTicks);
            reversing = AutoReverse;
            progress = AutoReverse ? 0f : 1f;
            return true;
        }

        var withinIteration = localTicks - iterationIndex * iterationTicks;
        iterationElapsed = TimeSpan.FromTicks(withinIteration);
        progress = (float)((double)withinIteration / passTicks);
        reversing = false;
        if (AutoReverse && progress > 1f)
        {
            progress = 2f - progress;
            reversing = true;
        }

        iteration = (int)Math.Min(iterationIndex, int.MaxValue);
        return false;
    }

    /// <summary>The full length of this animation's own timeline: <see cref="Delay"/> plus every pass (<see cref="AutoReverse"/> doubling
    /// each one) of every iteration (<see cref="RepeatCount"/> plus the first); <see cref="TimeSpan.MaxValue"/> with
    /// <see cref="RepeatForever"/>, matching <see cref="Composition.UIAnimationGroup.LengthOf"/>'s formula for a child.</summary>
    private TimeSpan TotalLength()
    {
        if (RepeatForever)
        {
            return TimeSpan.MaxValue;
        }

        var passTicks = Duration.Ticks;
        if (passTicks <= 0)
        {
            return Delay;
        }

        var pass = passTicks * (AutoReverse ? 2 : 1);
        var total = Delay.Ticks + pass * (RepeatCount + 1L);
        return total >= TimeSpan.MaxValue.Ticks ? TimeSpan.MaxValue : TimeSpan.FromTicks(total);
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

    /// <summary>Writes the value (or positions the children) for the given raw progress during a <see cref="Seek"/>. Default: the same as
    /// <see cref="ApplyProgress"/> (a leaf animation seeks exactly like it ticks); overridden by <see cref="Composition.UIAnimationGroup"/>
    /// to position each child by elapsed time instead of starting it.</summary>
    protected internal virtual void OnSeek(float progress) => ApplyProgress(progress);

    /// <summary>Called once, right after <see cref="BeginPreview"/> binds <see cref="Owner"/>, before any value is written. Default: a
    /// no-op (a leaf animation has nothing to attach); overridden by <see cref="Composition.UIAnimationGroup"/> to begin every child as a
    /// preview too, recursively, on <c>child.Owner ?? root</c>.</summary>
    protected internal virtual void OnPreviewAttached(MGElement root) { }

    /// <summary>Restores the base value (see <see cref="IUIAnimationTarget{T}.RestoreBaseValue"/>).</summary>
    protected internal abstract void OnRestoreBaseValue();

    /// <summary>Releases a held <c>Animation</c> contribution (store-backed targets only).</summary>
    protected internal abstract void OnReleaseHold();

    public override string ToString()
        => $"{GetType().Name}{(string.IsNullOrEmpty(Name) ? "" : " '" + Name + "'")} [{TargetKey}] {State} {Progress:0.00}";
}