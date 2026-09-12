using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI.Animation
{
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
    }

    /// <summary>The slot bookkeeping shared by the store-backed targets (see <see cref="IUIStoreBackedAnimationTarget{T}.ClearContribution"/>).</summary>
    internal static class UIStoreBackedTargets
    {
        /// <summary>The source of the whole container of <paramref name="pilot"/> on <paramref name="owner"/>, re-targeted to the pilot's invalidation,
        /// or Default when none was recorded (or when an animation is the only thing written there).</summary>
        internal static UIValueResolutionSource WholeSource(MGElement owner, UIPilotProperty pilot)
            => owner.TryGetResolvedValueSource(pilot, UIValueSlot.Whole, out UIValueResolutionSource whole) && whole.Kind != UIValueSourceKind.Animation
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
            IReadOnlyList<UIResolvedContribution> remaining = owner.EnumerateResolvedContributions(pilot, slot);
            bool animating = false;
            bool other = false;
            for (int i = 0; i < remaining.Count; i++)
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
}
