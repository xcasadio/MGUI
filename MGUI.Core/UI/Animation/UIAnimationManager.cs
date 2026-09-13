namespace MGUI.Core.UI.Animation;

/// <summary>
/// The animation engine of one <see cref="MGDesktop"/> (<c>MGDesktop.Animations</c>; S3, ADR-0006 decision 7): owns the
/// <see cref="UIAnimationClock"/>, ticks every active animation once per frame at the head of <c>MGDesktop.Update</c> (whatever the visibility
/// of the owner elements), enforces the conflict rule (one active animation per owner element and property path, decision 11) and cancels
/// the animations of an element that leaves the tree or of a window that closes (decision 9).<para/>
/// A frame allocates nothing: the active list is walked by index and finished animations are swept after the walk.
/// </summary>
public sealed class UIAnimationManager
{
    private readonly List<UIAnimation> _Active = new();
    private readonly Dictionary<(MGElement Owner, string Path), UIAnimation> _ByTarget = new(TargetKeyComparer.Instance);
    private bool _IsTicking;
    private bool _SweepNeeded;

    /// <summary>The time source shared by every animation of the desktop.</summary>
    public UIAnimationClock Clock { get; } = new();

    /// <summary>Number of animations currently active (delayed, running or paused).</summary>
    public int ActiveCount => _Active.Count;

    /// <summary>The active animations, in start order (a live list: do not keep it across frames).</summary>
    public IReadOnlyList<UIAnimation> ActiveAnimations => _Active;

    /// <summary>Advances the clock by one frame and ticks every active animation. Called once per frame by <c>MGDesktop.Update</c>.</summary>
    public void Update(TimeSpan frameElapsed)
    {
        Clock.Advance(frameElapsed);
        TimeSpan delta = Clock.DeltaTime;
        if (delta <= TimeSpan.Zero)
        {
            return;
        }

        _IsTicking = true;
        try
        {
            //  An animation started during this tick (a child of a group, a transition run) is advanced from the next frame: its first
            //  frame is the one where it was started, so a child scheduled at an offset of a sequence stays aligned with that timeline.
            int count = _Active.Count;
            for (int i = 0; i < count; i++)
            {
                UIAnimation animation = _Active[i];
                if (animation.IsActive)
                {
                    animation.Advance(delta);
                }
            }
        }
        finally
        {
            _IsTicking = false;
        }

        if (_SweepNeeded)
        {
            _SweepNeeded = false;
            for (int i = _Active.Count - 1; i >= 0; i--)
            {
                if (!_Active[i].IsActive)
                {
                    RemoveFinished(_Active[i]);
                }
            }
        }
    }

    /// <summary>Pauses every active animation (each keeps its own state; the clock is untouched, see <see cref="UIAnimationClock.IsPaused"/> for a global freeze).</summary>
    public void PauseAll()
    {
        for (int i = 0; i < _Active.Count; i++)
        {
            _Active[i].Pause();
        }
    }

    /// <summary>Resumes every paused animation.</summary>
    public void ResumeAll()
    {
        for (int i = 0; i < _Active.Count; i++)
        {
            _Active[i].Resume();
        }
    }

    /// <summary>Cancels every active animation according to its own <see cref="UIAnimation.CancelBehavior"/>.</summary>
    public void CancelAll()
    {
        for (int i = _Active.Count - 1; i >= 0; i--)
        {
            _Active[i].Cancel();
        }
    }

    /// <summary>Starts <paramref name="animation"/> on <paramref name="owner"/>: the animation active on the same (owner, path) is cancelled
    /// with <see cref="UIAnimationCancelBehavior.KeepCurrent"/> and hands its base value over, so the new one starts from the current animated
    /// value and still restores the true base. Restarting the animation that is already active on the path keeps it registered.</summary>
    internal void Start(MGElement owner, UIAnimation animation)
    {
        if (owner == null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        if (animation == null)
        {
            throw new ArgumentNullException(nameof(animation));
        }

        string path = animation.TargetKey;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"{animation.GetType().Name} has no target path.");
        }

        (MGElement Owner, string Path) key = (owner, path);
        if (animation.RegisteredKey is (MGElement Owner, string Path) registered && !TargetKeyComparer.Instance.Equals(registered, key))
        {
            // The same instance is started again on another owner or path: leave its previous slot cleanly (restore that owner's base,
            // release a held contribution) so no stale entry can cancel an unrelated animation later.
            if (animation.IsActive)
            {
                animation.CancelCore(UIAnimationCancelBehavior.RestoreBaseValue);
            }
            else if (animation.IsHeld)
            {
                animation.IsHeld = false;
                animation.OnReleaseHold();
            }

            if (_ByTarget.TryGetValue(registered, out UIAnimation staleEntry) && ReferenceEquals(staleEntry, animation))
            {
                _ByTarget.Remove(registered);
            }

            animation.RegisteredKey = null;
        }

        object inheritedBase = null;
        if (_ByTarget.TryGetValue(key, out UIAnimation previous))
        {
            if (ReferenceEquals(previous, animation))
            {
                if (previous.IsActive)
                {
                    inheritedBase = previous.BaseValueBoxed;
                }
            }
            else if (previous.IsActive)
            {
                inheritedBase = animation.InheritsBaseValue ? previous.BaseValueBoxed : null;
                previous.CancelCore(UIAnimationCancelBehavior.KeepCurrent);
            }
            else if (previous.IsHeld)
            {
                // The new animation's first write replaces the held contribution; nothing to release.
                previous.IsHeld = false;
                previous.RegisteredKey = null;
                _ByTarget.Remove(key);
            }
        }

        animation.Begin(owner, this, inheritedBase);
        if (!animation.IsActive)
        {
            // Cancelled from a Started/Updated handler during Begin: nothing to register.
            return;
        }

        if (!_Active.Contains(animation))
        {
            _Active.Add(animation);
        }

        _ByTarget[key] = animation;
        animation.RegisteredKey = key;
    }

    internal void NotifyFinished(UIAnimation animation)
    {
        if (_IsTicking)
        {
            _SweepNeeded = true;
            return;
        }

        RemoveFinished(animation);
    }

    private void RemoveFinished(UIAnimation animation)
    {
        _Active.Remove(animation);
        if (animation.IsHeld)
        {
            return;
        }

        if (animation.RegisteredKey is (MGElement Owner, string Path) key)
        {
            if (_ByTarget.TryGetValue(key, out UIAnimation current) && ReferenceEquals(current, animation))
            {
                _ByTarget.Remove(key);
            }

            animation.RegisteredKey = null;
        }
    }

    /// <summary>Cancels the animations owned by <paramref name="owner"/>; with <paramref name="restoreBaseValue"/> the base value is restored
    /// whatever their <see cref="UIAnimation.CancelBehavior"/>, and with <paramref name="releaseHolds"/> the held contributions are released too.</summary>
    internal void CancelOwnedBy(MGElement owner, bool restoreBaseValue, bool releaseHolds)
        => CancelWhere(x => ReferenceEquals(x.Owner, owner), restoreBaseValue, releaseHolds);

    /// <summary>Cancels, restores and releases every animation started while <paramref name="window"/> displayed its owner (window closed).</summary>
    internal void CancelOwnedByWindow(MGWindow window)
        => CancelWhere(x => IsWindowOrAncestorWindow(window, x.OwnerWindow), true, true);

    /// <summary>True when <paramref name="candidate"/> is <paramref name="window"/> or one of its <see cref="MGElement.ParentWindow"/> ancestors,
    /// so that closing a window also cancels the animations of its nested windows, tooltips and popups (review finding, S3).</summary>
    private static bool IsWindowOrAncestorWindow(MGWindow candidate, MGWindow window)
    {
        for (MGWindow current = window; current != null; current = current.ParentWindow)
        {
            if (ReferenceEquals(current, candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Re-captures the displaying window of the animations owned by <paramref name="owner"/> after it was re-parented (review finding, S3).</summary>
    internal void RefreshOwnerWindow(MGElement owner)
    {
        for (int i = 0; i < _Active.Count; i++)
        {
            UIAnimation animation = _Active[i];
            if (ReferenceEquals(animation.Owner, owner))
            {
                animation.RefreshOwnerWindow();
            }
        }
    }

    private void CancelWhere(Func<UIAnimation, bool> predicate, bool restoreBaseValue, bool releaseHolds)
    {
        //  A restore can notify a transition that starts a new run on the same owner during this pass (review finding, S6): re-scan until
        //  no matching animation is left, with a bound against a pathological ping-pong.
        for (int pass = 0; pass < 4; pass++)
        {
            bool cancelledAny = false;
            for (int i = _Active.Count - 1; i >= 0; i--)
            {
                if (i >= _Active.Count)
                {
                    // Cancelling a group removed its children below this index.
                    continue;
                }

                UIAnimation animation = _Active[i];
                if (animation.IsActive && predicate(animation))
                {
                    animation.CancelCore(restoreBaseValue ? UIAnimationCancelBehavior.RestoreBaseValue : animation.CancelBehavior);
                    cancelledAny = true;
                }
            }

            if (!cancelledAny)
            {
                break;
            }
        }

        if (!releaseHolds)
        {
            return;
        }

        List<(MGElement Owner, string Path)> released = null;
        foreach (KeyValuePair<(MGElement Owner, string Path), UIAnimation> entry in _ByTarget)
        {
            if (entry.Value.IsHeld && predicate(entry.Value))
            {
                (released ??= new()).Add(entry.Key);
            }
        }

        if (released == null)
        {
            return;
        }

        foreach ((MGElement Owner, string Path) key in released)
        {
            UIAnimation held = _ByTarget[key];
            _ByTarget.Remove(key);
            held.IsHeld = false;
            held.RegisteredKey = null;
            held.OnReleaseHold();
        }
    }

    internal int CountOwnedBy(MGElement owner)
    {
        int count = 0;
        for (int i = 0; i < _Active.Count; i++)
        {
            if (ReferenceEquals(_Active[i].Owner, owner))
            {
                count++;
            }
        }

        return count;
    }

    internal bool IsAnimating(MGElement owner, string path)
        => path != null && _ByTarget.TryGetValue((owner, path), out UIAnimation animation) && animation.IsActive;

    internal IEnumerable<UIAnimation> EnumerateOwnedBy(MGElement owner)
        => _Active.Where(x => ReferenceEquals(x.Owner, owner)).ToArray();

    internal IEnumerable<UIAnimation> EnumerateHeldBy(MGElement owner)
        => _ByTarget.Values.Where(x => x.IsHeld && ReferenceEquals(x.Owner, owner)).ToArray();

    private sealed class TargetKeyComparer : IEqualityComparer<(MGElement Owner, string Path)>
    {
        public static readonly TargetKeyComparer Instance = new();

        public bool Equals((MGElement Owner, string Path) x, (MGElement Owner, string Path) y)
            => ReferenceEquals(x.Owner, y.Owner) && string.Equals(x.Path, y.Path, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((MGElement Owner, string Path) obj)
            => HashCode.Combine(obj.Owner == null ? 0 : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj.Owner),
                obj.Path == null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Path));
    }
}