namespace MGUI.Core.UI.Animation
{
    /// <summary>
    /// A typed accessor the animation engine animates on an <see cref="MGElement"/> (ADR-0006, decision 5: a closed registry of typed accessors,
    /// <see cref="UIAnimationTargets"/>, no reflection). Implementations are stateless: one instance serves every element.<para/>
    /// Two families exist. A store-backed target (<see cref="IsStoreBacked"/> true) writes a pilot property through its tagged setter with the
    /// <c>Animation</c> source of the resolved value store (ADR-0005), and <see cref="RestoreBaseValue"/> clears that contribution so the next source
    /// takes over: the engine never has to know the base. A plain target writes a CLR property (Opacity, RenderTransform components) and the engine
    /// keeps the base value it read when the animation started, handing it back to <see cref="RestoreBaseValue"/>.
    /// </summary>
    public interface IUIAnimationTarget<T>
    {
        /// <summary>The property path this target animates, the key of <see cref="UIAnimationTargets"/> and of the conflict rule
        /// (one active animation per element and path). Examples: <c>Opacity</c>, <c>RenderTransform.Scale</c>, <c>Background</c>.</summary>
        string Path { get; }

        /// <summary>True when the target writes through the resolved value store (a pilot property).</summary>
        bool IsStoreBacked { get; }

        /// <summary>The current effective value: the animated value while an animation runs, the base value otherwise.</summary>
        T GetValue(MGElement element);

        /// <summary>Writes the animated value. <paramref name="animationName"/> is the name of the writing animation, recorded as the source name
        /// by store-backed targets for the diagnostics.</summary>
        void SetValue(MGElement element, T value, string animationName);

        /// <summary>Restores the base value: a store-backed target removes its <c>Animation</c> contribution (and ignores <paramref name="baseValue"/>),
        /// a plain target writes <paramref name="baseValue"/> back.</summary>
        void RestoreBaseValue(MGElement element, T baseValue);
    }
}
