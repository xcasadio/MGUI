namespace MGUI.Core.UI.Animation
{
    /// <summary>
    /// The time source of a <see cref="UIAnimationManager"/> (ADR-0006, decision 7): advanced once per frame from
    /// <c>UpdateBaseArgs.FrameElapsed</c>, never from <c>GameTime</c>, so that tests and editors drive it explicitly.<para/>
    /// <see cref="TimeScale"/> slows down or speeds up every animation of the desktop; <see cref="IsPaused"/> freezes them. Neither affects the
    /// tooltip delay, <c>MGTimer</c> or <c>MGStopWatch</c>, which keep their own time. Large frame deltas are not clamped: after a long pause the
    /// running animations complete on the next frame, deterministically.
    /// </summary>
    public sealed class UIAnimationClock
    {
        /// <summary>Scaled time accumulated since the clock was created (paused frames add nothing).</summary>
        public TimeSpan Time { get; private set; }

        /// <summary>Scaled duration of the current frame: the amount every active animation advances by this frame. Zero while <see cref="IsPaused"/>.</summary>
        public TimeSpan DeltaTime { get; private set; }

        private float _TimeScale = 1.0f;
        /// <summary>Multiplier applied to the frame time. 1 = real time, 0.5 = half speed, 0 = frozen. Default: 1.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is negative or not a number.</exception>
        public float TimeScale
        {
            get => _TimeScale;
            set
            {
                if (float.IsNaN(value) || value < 0f)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(TimeScale)} must be a finite value greater than or equal to zero.");
                }

                _TimeScale = value;
            }
        }

        /// <summary>While true, <see cref="Advance"/> produces a zero <see cref="DeltaTime"/>: every animation of the desktop is frozen in place.</summary>
        public bool IsPaused { get; set; }

        /// <summary>Advances the clock by one frame. Called by <see cref="UIAnimationManager.Update"/>; a test may call it through the manager.</summary>
        public void Advance(TimeSpan frameElapsed)
        {
            if (frameElapsed < TimeSpan.Zero)
            {
                frameElapsed = TimeSpan.Zero;
            }

            DeltaTime = IsPaused ? TimeSpan.Zero : Scale(frameElapsed);
            Time += DeltaTime;
        }

        private TimeSpan Scale(TimeSpan value)
            => _TimeScale == 1.0f ? value : TimeSpan.FromTicks((long)(value.Ticks * (double)_TimeScale));
    }
}
