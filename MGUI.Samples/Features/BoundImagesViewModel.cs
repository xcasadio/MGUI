using System;
using MGUI.Shared.Helpers;

namespace MGUI.Samples.Features
{
    /// <summary>The <see cref="BoundImagesSample"/> window's <c>WindowDataContext</c>: the moving sprite's canvas
    /// coordinates (ADR-0016, "Bindable canvas coordinates"), and the animated sprite's start offset and playing flag
    /// (ADR-0016, "Animated image sources"). Every property matches the type of the <see cref="MGUI.Core.UI.MGElement"/>
    /// property it is bound to (<c>int?</c>, <see cref="TimeSpan"/>, <see cref="bool"/>), so each binding push takes
    /// the allocation-free typed-copy path rather than the converter/string-format path.</summary>
    public sealed class BoundImagesViewModel : ViewModelBase
    {
        private int? _spriteLeft;
        /// <summary>Bound (OneWay) to the moving sprite's <see cref="MGUI.Core.UI.MGElement.CanvasLeft"/>.</summary>
        public int? SpriteLeft
        {
            get => _spriteLeft;
            set
            {
                if (_spriteLeft != value)
                {
                    _spriteLeft = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private int? _spriteTop;
        /// <summary>Bound (OneWay) to the moving sprite's <see cref="MGUI.Core.UI.MGElement.CanvasTop"/>.</summary>
        public int? SpriteTop
        {
            get => _spriteTop;
            set
            {
                if (_spriteTop != value)
                {
                    _spriteTop = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private TimeSpan _animationStartOffset = TimeSpan.Zero;
        /// <summary>Bound (OneWay) to the animated sprite's <see cref="MGUI.Core.UI.MGImage.AnimationStartOffset"/>.
        /// Set by the "Restart at 0s"/"Restart at 0.4s" buttons.</summary>
        public TimeSpan AnimationStartOffset
        {
            get => _animationStartOffset;
            set
            {
                if (_animationStartOffset != value)
                {
                    _animationStartOffset = value;
                    NotifyPropertyChanged();
                }
            }
        }

        private bool _isAnimationPlaying = true;
        /// <summary>Bound TwoWay to the "Play/Pause" <c>ToggleButton</c> and OneWay to the animated sprite's
        /// <see cref="MGUI.Core.UI.MGImage.IsAnimationPlaying"/>.</summary>
        public bool IsAnimationPlaying
        {
            get => _isAnimationPlaying;
            set
            {
                if (_isAnimationPlaying != value)
                {
                    _isAnimationPlaying = value;
                    NotifyPropertyChanged();
                }
            }
        }
    }
}
