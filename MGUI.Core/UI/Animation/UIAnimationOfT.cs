using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Interpolation;

namespace MGUI.Core.UI.Animation
{
    /// <summary>
    /// An animation interpolating a value of <typeparamref name="T"/> from a start value to <see cref="To"/> with an easing (S3; ADR-0006).<para/>
    /// <see cref="From"/> is optional (decision 11): when it is not set, the animation starts from the value read when it starts, which is the
    /// current animated value when it replaces another animation on the same path, so a hover-out never snaps back to the hover-in start value.
    /// The base value handed to <see cref="UIAnimationFillBehavior.RestoreBaseValue"/> is read at the same moment, or inherited from the
    /// replaced animation so a chain of replacements keeps the true base.<para/>
    /// Derived types say how the value is read and written (<see cref="UIPropertyAnimation{T}"/> through an <see cref="IUIAnimationTarget{T}"/>).
    /// </summary>
    public abstract class UIAnimation<T> : UIAnimation
    {
        private T _From;
        private T _StartValue;
        private T _BaseValue;
        private bool _HasBaseValue;
        private IUIInterpolator<T> _ActiveInterpolator;

        /// <summary>The start value. Not set by default: the animation then starts from the current value (see <see cref="HasFrom"/>).</summary>
        public T From
        {
            get => _From;
            set
            {
                _From = value;
                HasFrom = true;
            }
        }

        /// <summary>True once <see cref="From"/> has been assigned; <see cref="ClearFrom"/> resets it.</summary>
        public bool HasFrom { get; private set; }

        /// <summary>Forgets <see cref="From"/>: the next start reads the current value.</summary>
        public void ClearFrom()
        {
            _From = default;
            HasFrom = false;
        }

        /// <summary>The end value.</summary>
        public T To { get; set; }

        /// <summary>The easing function. Null means <see cref="UIEasing.Linear"/>.</summary>
        public IUIEasingFunction Easing { get; set; }

        /// <summary>The interpolator. Null means the one registered for <typeparamref name="T"/> in <see cref="UIInterpolators"/>, resolved at start.</summary>
        public IUIInterpolator<T> Interpolator { get; set; }

        /// <summary>The value written by the last tick.</summary>
        public T CurrentValue { get; protected set; }

        /// <summary>The start value of the current run (<see cref="From"/>, or the value read at start).</summary>
        public T StartValue => _StartValue;

        /// <summary>The base value the run restores (read at start or inherited from a replaced animation).</summary>
        public T BaseValue => _BaseValue;

        protected internal override object BaseValueBoxed => _HasBaseValue ? _BaseValue : null;

        protected internal override void OnStarting(object inheritedBase)
        {
            _ActiveInterpolator = Interpolator ?? UIInterpolators.Get<T>();
            T current = ReadCurrentValue();
            _StartValue = HasFrom ? _From : current;
            _BaseValue = inheritedBase is T inherited ? inherited : current;
            _HasBaseValue = true;
            CurrentValue = _StartValue;
        }

        protected internal override void ApplyProgress(float progress)
        {
            float eased = (Easing ?? UIEasing.Linear).Ease(progress);
            CurrentValue = _ActiveInterpolator.Lerp(_StartValue, To, eased);
            WriteValue(CurrentValue);
        }

        protected internal override void OnRestoreBaseValue() => RestoreBaseValueCore(_BaseValue);

        protected internal override void OnReleaseHold() => ReleaseHoldCore();

        /// <summary>Reads the current effective value of the animated property.</summary>
        protected abstract T ReadCurrentValue();

        /// <summary>Writes the animated value.</summary>
        protected abstract void WriteValue(T value);

        /// <summary>Restores the base value (a store-backed target clears its contribution instead).</summary>
        protected abstract void RestoreBaseValueCore(T baseValue);

        /// <summary>Releases a held contribution (store-backed targets only; a no-op otherwise).</summary>
        protected abstract void ReleaseHoldCore();
    }
}
