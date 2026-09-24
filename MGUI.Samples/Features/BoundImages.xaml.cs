using System;
using System.Diagnostics;
using MGUI.Core.UI;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    /// <summary>Demonstrates ADR-0016 ("Host-resolved and animated image sources, bindable canvas coordinates, and
    /// allocation-free binding pushes"): 1) bindable <c>CanvasLeft</c>/<c>CanvasTop</c> driving a sprite bounced
    /// across a <c>Canvas</c> by a per-tick timer; 2) a static image whose <c>SourceName</c> is resolved by
    /// <see cref="BoundImagesAssetProvider"/> (see <see cref="MGUI.Samples.Game1"/>'s <c>Initialize</c>, which wraps
    /// the app's own runtime in <see cref="BoundImagesRuntime"/>) rather than <c>MGResources.AddTexture</c>; 3) an
    /// animated image resolved the same way, with a bindable start offset and playing flag.</summary>
    public class BoundImagesSample : SampleBase
    {
        public BoundImagesViewModel ViewModel { get; } = new();

        //  The moving sprite's travel range within "MovementCanvas" (420x110), sized for its own 24x24 image.
        private const int MinLeft = 4;
        private const int MaxLeft = 420 - 24 - 4;
        private const int MinTop = 4;
        private const int MaxTop = 110 - 24 - 4;
        private const double SweepSeconds = 2.5;

        private readonly Stopwatch _movementClock = Stopwatch.StartNew();
        private TimeSpan _lastElapsed = TimeSpan.Zero;
        private double _horizontalRatio;
        private bool _movingRight = true;

        public BoundImagesSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, "Features", "BoundImages.xaml")
        {
            ViewModel.SpriteLeft = MinLeft;
            ViewModel.SpriteTop = (MinTop + MaxTop) / 2;
            Window.WindowDataContext = ViewModel;

            WireMovement();
            WireAnimationControls();
        }

        /// <summary>1. Bindable canvas coordinates: bounces "MovementImage" left/right across "MovementCanvas" by
        /// writing <see cref="BoundImagesViewModel.SpriteLeft"/> every tick -- the binding on <c>CanvasLeft</c> (an
        /// <c>int?</c>, matching the view model's own property type) then applies it without a converter.</summary>
        private void WireMovement()
        {
            Window.OnEndUpdate += (sender, e) =>
            {
                TimeSpan now = _movementClock.Elapsed;
                TimeSpan delta = now - _lastElapsed;
                _lastElapsed = now;

                double step = delta.TotalSeconds / SweepSeconds;
                if (_movingRight)
                {
                    _horizontalRatio += step;
                    if (_horizontalRatio >= 1.0)
                    {
                        _horizontalRatio = 1.0;
                        _movingRight = false;
                    }
                }
                else
                {
                    _horizontalRatio -= step;
                    if (_horizontalRatio <= 0.0)
                    {
                        _horizontalRatio = 0.0;
                        _movingRight = true;
                    }
                }

                ViewModel.SpriteLeft = MinLeft + (int)Math.Round(_horizontalRatio * (MaxLeft - MinLeft));
            };
        }

        /// <summary>3. Restarts "AnimatedSpriteImage"'s animation at a different offset by writing
        /// <see cref="BoundImagesViewModel.AnimationStartOffset"/>, bound (OneWay) to
        /// <see cref="MGImage.AnimationStartOffset"/>. The "Playing" toggle is bound TwoWay directly in XAML.</summary>
        private void WireAnimationControls()
        {
            Window.GetElementByName<MGButton>("RestartAtZeroButton").AddCommandHandler((btn, e) => ViewModel.AnimationStartOffset = TimeSpan.Zero);
            Window.GetElementByName<MGButton>("RestartAtOffsetButton").AddCommandHandler((btn, e) => ViewModel.AnimationStartOffset = TimeSpan.FromSeconds(0.4));
        }
    }
}
