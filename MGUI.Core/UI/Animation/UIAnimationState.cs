namespace MGUI.Core.UI.Animation
{
    /// <summary>Playback state of a <see cref="UIAnimation"/> (Docs/Tasks/animation-tasks.md, S3; ADR-0006).</summary>
    public enum UIAnimationState
    {
        /// <summary>Never started, or reset by <see cref="UIAnimation.Restart"/> before its first tick.</summary>
        Stopped,

        /// <summary>Started, waiting for <see cref="UIAnimation.Delay"/> to elapse; no value has been written yet.</summary>
        Delayed,

        /// <summary>Advancing every frame and writing its value.</summary>
        Running,

        /// <summary>Frozen by <see cref="UIAnimation.Pause"/>; the current value stays applied until <see cref="UIAnimation.Resume"/>.</summary>
        Paused,

        /// <summary>Reached its end; the final value is held or the base value restored according to <see cref="UIAnimation.FillBehavior"/>.</summary>
        Completed,

        /// <summary>Stopped before its end by <see cref="UIAnimation.Cancel"/>, by a conflicting animation, or by its owner leaving the tree.</summary>
        Cancelled,
    }
}
