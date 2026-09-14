using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// A store-backed <see cref="IUIAnimationTarget{T}"/> (a pilot property of ADR-0005) that can also be written under another
/// <see cref="UIValueResolutionSource"/> than <c>Animation</c>: the named visual states (ADR-0007, decision 3) write their setters with the
/// <c>VisualState</c> source (70), below an animation (100) and above a template, so a transition interpolating the change still wins while it
/// runs and the state value shows once it ends.
/// </summary>
public interface IUIStoreBackedAnimationTarget<T> : IUIAnimationTarget<T>
{
    /// <summary>The pilot property this target writes.</summary>
    UIPilotProperty Pilot { get; }

    /// <summary>Writes <paramref name="value"/> under <paramref name="source"/>.</summary>
    void SetValue(MGElement element, T value, UIValueResolutionSource source);

    /// <summary>
    /// Removes the contribution of the kind of <paramref name="source"/> from the slot this target writes, letting the next source take over. When the
    /// slot then has no contribution left (the container was written whole by a constructor, a theme or a style, and its field only lived inside the
    /// object), the value would stay frozen on the state's value (the store keeps the CLR value when the last contribution of a slot is removed,
    /// ADR-0005): <paramref name="baseValue"/> is then written back as that kept value, or recorded under the container's source when an animation
    /// still holds the slot, so the run restores it at its end. Returns true when the slot rests (no animation in flight) and the base is consumed.
    /// </summary>
    bool ClearContribution(MGElement element, UIValueResolutionSource source, T baseValue);

    /// <summary>
    /// Reads the winner for the pilot slot this target writes as if this target's own <see cref="UIValueSourceKind.Animation"/>
    /// contribution did not exist -- a <see cref="UITransition{T}"/> uses this (falling back to <see cref="IUIObservableAnimationTarget{T}.GetUnderlyingValue"/>
    /// when it returns false) to retarget immediately when a local write or a named-state exit changes the value below the run, instead of only
    /// seeing it once the run ends. False when nothing but the target's own Animation contribution is recorded for that slot.
    /// </summary>
    bool TryGetValueBelowAnimation(MGElement element, out T value);
}

/// <summary>
/// A store-backed target whose animated value is a brush (ADR-0009: the five brush-valued targets -- <c>Background</c> and its Selected,
/// Disabled and Focused slots, <c>Background.Gradient</c>, <c>Background.DiagonalGradient</c> and <c>BorderBrush</c>). Chosen over adding these
/// members to <see cref="IUIStoreBackedAnimationTarget{T}"/> itself (decision recorded in ADR-0009): the other two implementors of that
/// interface (<c>ForegroundTarget</c>, <c>TextForegroundTarget</c>) write a <c>Color?</c>, not a brush, and would gain three members they can
/// never use; a dedicated interface only the five brush targets implement, checked once by <see cref="UIPropertyAnimation{T}"/> with an
/// <see langword="is"/> pattern, is the smaller change.<para/>
/// The run-owned clone replaces the "allocate a new brush every tick" cost the earlier targets accepted: <see cref="BeginAnimatedValue"/> runs once,
/// when the run starts (<see cref="UIPropertyAnimation{T}.OnStarting"/>, after the target is resolved and the start/base values are read),
/// <see cref="ApplyAnimatedValue"/> runs every tick with no store write and no allocation, and <see cref="EndAnimatedValue"/> runs once on every
/// path that stops the run and must make the base reappear (<see cref="UIAnimation.OnRestoreBaseValue"/>, <see cref="UIAnimation.OnReleaseHold"/>).
/// A run replaced by a new one on the same path (the manager's conflict rule, <see cref="UIAnimationCancelBehavior.KeepCurrent"/>) calls neither:
/// its clone is simply dropped, and the new run's own <see cref="BeginAnimatedValue"/> creates a fresh one.
/// </summary>
public interface IUIBrushAnimationTarget<T> : IUIStoreBackedAnimationTarget<T>
{
    /// <summary>Creates the run-owned clone and writes it once into the element's <c>Animation</c> contribution: a <c>Copy()</c> of the brush
    /// that would apply below the animation (read the same way <see cref="IUIStoreBackedAnimationTarget{T}.TryGetValueBelowAnimation"/> does,
    /// before this call's own write records anything) when it is of the expected concrete type, or a freshly constructed brush of that type
    /// otherwise (a base of another type was already refused earlier, by <see cref="IUIAnimationTarget{T}.GetValue"/> at
    /// <see cref="UIAnimation{T}.OnStarting"/>'s own read; reaching this fallback means no exact base could be recovered because a replaced
    /// run already occupied the path, not that the true base was ever wrong). Returns an opaque handle carrying both the clone (for
    /// <see cref="ApplyAnimatedValue"/>) and the recovered original, if any (for <see cref="EndAnimatedValue"/>): the original cannot be
    /// re-read later, once the clone itself is the only thing recorded under <c>Animation</c>.</summary>
    object BeginAnimatedValue(MGElement element, string animationName);

    /// <summary>Mutates the fields of <paramref name="handle"/>'s clone to <paramref name="value"/>, through the clone's own notifying setters
    /// (ADR-0009, "Decisions taken during delivery": a setter that skipped notification broke the existing self-retargeting that
    /// <c>UITransition{T}.HandleChanged</c> relies on -- a running transition on <c>Background</c>/<c>BorderBrush</c> is subscribed to the
    /// container/border's <c>PropertyChanged</c>, and that per-tick relay is what lets it notice a base changed underneath it, e.g. a named
    /// state exiting mid-run, within the following couple of ticks; kept deliberately, since the setter's own equality check does not box a
    /// <see cref="Microsoft.Xna.Framework.Color"/> and the notification reuses a cached <see cref="System.ComponentModel.PropertyChangedEventArgs"/>).
    /// No store write, no allocation either way.</summary>
    void ApplyAnimatedValue(object handle, T value);

    /// <summary>Ends the run: removes the <c>Animation</c> contribution and writes back <paramref name="handle"/>'s original brush instance when
    /// <see cref="BeginAnimatedValue"/> recovered one (so the base reappears as the exact same instance, frozen or not, shared or not), or a
    /// freshly built brush from <paramref name="baseValue"/> otherwise (the same fallback the earlier targets always used).</summary>
    void EndAnimatedValue(MGElement element, object handle, T baseValue);
}

/// <summary>The run-owned state a <see cref="IUIBrushAnimationTarget{T}"/> hands its caller (ADR-0009): <see cref="Clone"/> is what
/// <see cref="IUIBrushAnimationTarget{T}.ApplyAnimatedValue"/> mutates every tick; <see cref="Original"/> is the exact brush instance recovered
/// below the animation the moment the run started, captured once because it cannot be recovered later (once <see cref="Clone"/> is the only
/// thing recorded under the element's <c>Animation</c> contribution, reading "the value below the animation" always reports nothing left, the
/// same reason <see cref="IUIStoreBackedAnimationTarget{T}.TryGetValueBelowAnimation"/> already documents) -- null when a replaced run already
/// occupied the path when this one started.</summary>
internal sealed class UIBrushAnimationHandle<TBrush>
{
    public UIBrushAnimationHandle(TBrush clone, TBrush original)
    {
        Clone = clone;
        Original = original;
    }

    public TBrush Clone { get; }

    public TBrush Original { get; }
}

/// <summary>The slot bookkeeping shared by the store-backed targets (see <see cref="IUIStoreBackedAnimationTarget{T}.ClearContribution"/>).</summary>
internal static class UIStoreBackedTargets
{
    /// <summary>The source of the whole container of <paramref name="pilot"/> on <paramref name="owner"/>, re-targeted to the pilot's invalidation,
    /// or Default when none was recorded (or when an animation is the only thing written there).</summary>
    internal static UIValueResolutionSource WholeSource(MGElement owner, UIPilotProperty pilot)
        => owner.TryGetResolvedValueSource(pilot, UIValueSlot.Whole, out var whole) && whole.Kind != UIValueSourceKind.Animation
            ? new UIValueResolutionSource(whole.Kind, whole.Precedence, UIPilotPropertyResolver.KindOf(pilot), whole.Name)
            : UIValueResolutionSource.Default(UIPilotPropertyResolver.KindOf(pilot));

    /// <summary>Clears the <c>Animation</c> contribution of a slot and, when nothing else was ever recorded for it, writes the base back under the
    /// source of the whole container, so the value does not stay frozen on the last animated value.</summary>
    internal static void RestoreAnimation(MGElement owner, UIPilotProperty pilot, UIValueSlot slot, Action<UIValueResolutionSource> writeBase)
    {
        owner.ClearPilotSource(pilot, slot, UIValueSourceKind.Animation);
        if (owner.EnumerateResolvedContributions(pilot, slot).Count == 0)
        {
            writeBase(WholeSource(owner, pilot));
        }
    }

    /// <summary>The body of <see cref="IUIStoreBackedAnimationTarget{T}.ClearContribution"/>: <paramref name="writeBase"/> writes the base under the source it is given.</summary>
    internal static bool Restore(MGElement owner, UIPilotProperty pilot, UIValueSlot slot, UIValueResolutionSource source, Action<UIValueResolutionSource> writeBase)
    {
        owner.ClearPilotSource(pilot, slot, source.Kind);
        var remaining = owner.EnumerateResolvedContributions(pilot, slot);
        var animating = false;
        var other = false;
        for (var i = 0; i < remaining.Count; i++)
        {
            if (remaining[i].Kind == UIValueSourceKind.Animation)
            {
                animating = true;
            }
            else
            {
                other = true;
            }
        }

        if (other)
        {
            return !animating;
        }

        if (animating)
        {
            // A run holds the slot: record the base under the container's source, the run's end restore lands on it (delayed, like any write under a run).
            writeBase(WholeSource(owner, pilot));
            return false;
        }

        // Nothing left: put the base back as the kept CLR value, without a contribution (a theme refresh still replaces it).
        writeBase(source);
        owner.ClearPilotSource(pilot, slot, source.Kind);
        return true;
    }
}