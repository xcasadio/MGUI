namespace MGUI.Core.UI.Animation.Composition;

/// <summary>
/// Base of the composite animations (ADR-0007, decision 1; Docs/Tasks/animation-v2-tasks.md T1): a <see cref="UIAnimation"/> owned by one root
/// element (<c>root.Animations.Start(group)</c>) that starts its children through the manager, each child on its own owner (the child's
/// <see cref="UIAnimation.Owner"/> when it already has one, the root otherwise), so every child keeps its own conflict key, appears in its
/// element's diagnostics and is cancelled when its element leaves the tree.<para/>
/// A group writes no property: its <see cref="UIAnimation.TargetKey"/> is a synthetic path (<c>Group#n</c>) that never conflicts with a
/// property animation. Cancelling the group cancels its active children according to their own <see cref="UIAnimation.CancelBehavior"/>;
/// the group's <see cref="UIAnimation.CancelBehavior"/> is irrelevant, and its <see cref="UIAnimation.FillBehavior"/> must stay <see cref="UIAnimationFillBehavior.HoldEnd"/>
/// (a restore at completion would cancel a child that outlives the group). A forced restore of the root (<see cref="UIAnimationCollection.Clear"/>,
/// detachment, window close) cancels the children on other elements with their own behaviour, not with a forced restore. <see cref="UIAnimation.RepeatCount"/>
/// and <see cref="UIAnimation.RepeatForever"/> restart the children; <see cref="UIAnimation.AutoReverse"/> is refused (children are not rewound).
/// </summary>
public abstract class UIAnimationGroup : UIAnimation
{
    private static int _NextId;

    private readonly List<UIAnimation> _Children = new();
    private readonly string _Key;
    private bool _IsDriving;

    protected UIAnimationGroup()
    {
        _Key = $"{GetType().Name}#{Interlocked.Increment(ref _NextId)}";
        Cancelled += (_, _) => CancelChildren();
    }

    /// <summary>The children, in the order they were added.</summary>
    public IReadOnlyList<UIAnimation> Children => _Children;

    /// <summary>Adds a child. A child may be any animation, including another group; it cannot be added twice or while the group is active.</summary>
    public void Add(UIAnimation child)
    {
        if (child == null)
        {
            throw new ArgumentNullException(nameof(child));
        }

        if (ReferenceEquals(child, this) || _Children.Contains(child))
        {
            throw new ArgumentException("A child is added once, and a group cannot contain itself.", nameof(child));
        }

        if (IsActive)
        {
            throw new InvalidOperationException("Children cannot be added while the group is active.");
        }

        _Children.Add(child);
        OnChildAdded(child);
    }

    /// <summary>Number of children that have finished (completed or cancelled) during the current iteration.</summary>
    protected int FinishedChildren { get; private set; }

    /// <summary>Number of children started during the current iteration.</summary>
    protected int StartedChildren { get; private set; }

    protected internal sealed override bool IsStoreBacked => false;

    protected internal sealed override string TargetKey => _Key;

    protected internal sealed override object BaseValueBoxed => null;

    protected internal override void OnStarting(object inheritedBase)
    {
        if (AutoReverse)
        {
            throw new ArgumentException($"{GetType().Name} does not support {nameof(AutoReverse)}: children are not rewound. Reverse the children instead.", nameof(AutoReverse));
        }

        if (FillBehavior != UIAnimationFillBehavior.HoldEnd)
        {
            throw new ArgumentException($"{GetType().Name} keeps {nameof(FillBehavior)} = {nameof(UIAnimationFillBehavior.HoldEnd)}: a group holds no value, and a restore at completion would cancel its children.", nameof(FillBehavior));
        }

        Duration = ComputeDuration();
        FinishedChildren = 0;
        StartedChildren = 0;
    }

    /// <summary>Called by the base <see cref="UIAnimation"/> at every tick with the raw progress of the group's own timeline; the group
    /// starts the children that are due (<see cref="StartDueChildren"/>) and completes when the base timeline ends.</summary>
    protected internal sealed override void ApplyProgress(float progress)
    {
        if (IsPreview)
        {
            // A preview is never ticked by the manager (Begin never calls ApplyProgress for one, see BeginPreview): reached only if a
            // caller mis-drives a preview instance directly, in which case starting children through the manager would be wrong anyway.
            return;
        }

        if (_IsDriving)
        {
            return;
        }

        _IsDriving = true;
        try
        {
            StartDueChildren(progress);
        }
        finally
        {
            _IsDriving = false;
        }
    }

    /// <summary>Preview seek (U7): positions every child at its own elapsed time on the group's timeline (<see cref="UIAnimation.IterationElapsed"/>,
    /// already set by the base <see cref="UIAnimation.Seek"/> before this runs), relative to its offset (<see cref="GetChildOffset"/>, zero for
    /// a storyboard, <see cref="UISequenceAnimation.GetStartOffset"/> for a sequence); a child before its offset seeks to zero (its initial
    /// pose). Never starts a child through <see cref="StartChild"/> and never touches <see cref="StartedChildren"/> / <see cref="FinishedChildren"/>:
    /// a preview group's children were attached as previews once, by <see cref="OnPreviewAttached"/>.</summary>
    protected internal sealed override void OnSeek(float progress)
    {
        var timeline = IterationElapsed;
        for (var i = 0; i < _Children.Count; i++)
        {
            var offset = GetChildOffset(i);
            var childElapsed = timeline - offset;
            if (childElapsed < TimeSpan.Zero)
            {
                childElapsed = TimeSpan.Zero;
            }

            _Children[i].Seek(childElapsed);
        }
    }

    /// <summary>The time offset, on the group's own timeline, at which the child at <paramref name="index"/> starts. Zero for a storyboard
    /// (every child starts with the group); overridden by <see cref="UISequenceAnimation"/>.</summary>
    protected virtual TimeSpan GetChildOffset(int index) => TimeSpan.Zero;

    /// <summary>Preview attach (U7): begins every child as a preview too, recursively (a child that is itself a group attaches its own
    /// children the same way through its own override), on <c>child.Owner ?? root</c> -- never through <see cref="StartChild"/>, so a
    /// preview child is never registered with a manager and never raises <see cref="UIAnimation.Started"/>/<see cref="UIAnimation.Updated"/>.</summary>
    protected internal sealed override void OnPreviewAttached(MGElement root)
    {
        for (var i = 0; i < _Children.Count; i++)
        {
            var child = _Children[i];
            var owner = child.Owner ?? root ?? throw new InvalidOperationException("The group has no owner.");
            child.BeginPreview(owner);
        }
    }

    /// <summary>U8 fix (ADR-0008): rolls back every child this group's <see cref="OnPreviewAttached"/> already began as a preview
    /// (<see cref="UIAnimation.IsPreview"/>) when the group's own <see cref="UIAnimation.BeginPreview"/> fails afterwards (an invalid
    /// <see cref="UIAnimation.AutoReverse"/> or <see cref="UIAnimation.FillBehavior"/> caught by <see cref="OnStarting"/>) -- recursively,
    /// since a child that is itself a group rolls its own children back the same way through its own override. A child never reached by
    /// the failed <see cref="OnPreviewAttached"/> loop (still not a preview) is left untouched.</summary>
    protected internal sealed override void OnPreviewAttachFailed()
    {
        for (var i = 0; i < _Children.Count; i++)
        {
            var child = _Children[i];
            if (child.IsPreview)
            {
                child.RollbackFailedPreviewAttach();
            }
        }
    }

    // A group holds no value of its own: cancelling it, whatever its cancel behaviour, cancels the children still running, each according to its own.
    protected internal sealed override void OnRestoreBaseValue() => CancelChildren();

    private void CancelChildren()
    {
        for (var i = 0; i < _Children.Count; i++)
        {
            var child = _Children[i];
            if (child.IsActive)
            {
                child.CancelCore(child.CancelBehavior);
            }
        }
    }

    protected internal sealed override void OnReleaseHold() { }

    /// <summary>The length of the group's own timeline, derived from the children (delay and repeats included).</summary>
    protected abstract TimeSpan ComputeDuration();

    /// <summary>Starts the children due at <paramref name="progress"/> of the timeline.</summary>
    protected abstract void StartDueChildren(float progress);

    protected virtual void OnChildAdded(UIAnimation child) { }

    /// <summary>Starts <paramref name="child"/> on its owner (or the group's owner) through the manager.</summary>
    protected void StartChild(UIAnimation child)
    {
        var owner = child.Owner ?? Owner ?? throw new InvalidOperationException("The group has no owner.");
        StartedChildren++;
        child.Completed += HandleChildFinished;
        child.Cancelled += HandleChildFinished;
        owner.Animations.Start(child);
    }

    private void HandleChildFinished(object sender, EventArgs e)
    {
        var child = (UIAnimation)sender;
        child.Completed -= HandleChildFinished;
        child.Cancelled -= HandleChildFinished;
        FinishedChildren++;
        OnChildFinished(child);
    }

    /// <summary>Called when a started child completes or is cancelled.</summary>
    protected virtual void OnChildFinished(UIAnimation child) { }

    /// <summary>The time a child occupies on a timeline: delay plus its passes.</summary>
    protected static TimeSpan LengthOf(UIAnimation child)
    {
        if (child.RepeatForever)
        {
            return TimeSpan.MaxValue;
        }

        var pass = child.Duration.Ticks * (child.AutoReverse ? 2 : 1);
        var total = child.Delay.Ticks + pass * (child.RepeatCount + 1L);
        return total >= TimeSpan.MaxValue.Ticks ? TimeSpan.MaxValue : TimeSpan.FromTicks(total);
    }
}