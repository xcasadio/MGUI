namespace MGUI.Core.UI.Animation
{
    /// <summary>What happens to the animated value when a <see cref="UIAnimation"/> completes (ADR-0006, decision 10: the default is <see cref="HoldEnd"/>).</summary>
    public enum UIAnimationFillBehavior
    {
        /// <summary>The property goes back to its base value: a pilot property drops its <c>Animation</c> contribution from the resolved value store,
        /// a non-pilot property is written back with the value read when the animation started.</summary>
        RestoreBaseValue,

        /// <summary>The end value stays. For a pilot property the <c>Animation</c> contribution is kept in the store until the next animation on the
        /// same path or <see cref="UIAnimationCollection.Clear"/>, so a later local write is shadowed (the diagnostics report the <c>Animation</c> source);
        /// for a non-pilot property the end value simply remains the CLR value and a later local write wins (documented asymmetry).</summary>
        HoldEnd,
    }
}
