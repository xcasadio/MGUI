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
    private int _Finished;
    private int _Started;
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
    protected int FinishedChildren => _Finished;

    /// <summary>Number of children started during the current iteration.</summary>
    protected int StartedChildren => _Started;

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
        _Finished = 0;
        _Started = 0;
    }

    /// <summary>Called by the base <see cref="UIAnimation"/> at every tick with the raw progress of the group's own timeline; the group
    /// starts the children that are due (<see cref="StartDueChildren"/>) and completes when the base timeline ends.</summary>
    protected internal sealed override void ApplyProgress(float progress)
    {
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

    // A group holds no value of its own: cancelling it, whatever its cancel behaviour, cancels the children still running, each according to its own.
    protected internal sealed override void OnRestoreBaseValue() => CancelChildren();

    private void CancelChildren()
    {
        for (int i = 0; i < _Children.Count; i++)
        {
            UIAnimation child = _Children[i];
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
        MGElement owner = child.Owner ?? Owner ?? throw new InvalidOperationException("The group has no owner.");
        _Started++;
        child.Completed += HandleChildFinished;
        child.Cancelled += HandleChildFinished;
        owner.Animations.Start(child);
    }

    private void HandleChildFinished(object sender, EventArgs e)
    {
        UIAnimation child = (UIAnimation)sender;
        child.Completed -= HandleChildFinished;
        child.Cancelled -= HandleChildFinished;
        _Finished++;
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

        long pass = child.Duration.Ticks * (child.AutoReverse ? 2 : 1);
        long total = child.Delay.Ticks + pass * (child.RepeatCount + 1L);
        return total >= TimeSpan.MaxValue.Ticks ? TimeSpan.MaxValue : TimeSpan.FromTicks(total);
    }
}