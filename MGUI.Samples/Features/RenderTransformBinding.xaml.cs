using System;
using MGUI.Core.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;

namespace MGUI.Samples.Features
{
    /// <summary>Demonstrates ADR-0020 ("Bindable render transform", G9 of the CasaEngine XAML-screens audit): a small
    /// image flying and growing from the canvas' top-left corner, driven by a view model whose <see cref="Vector2"/>
    /// translation and scale bind straight onto <see cref="MGUI.Core.UI.Animation.UIRenderTransform"/> through
    /// <c>RenderTransformTranslation</c>/<c>RenderTransformScale</c> -- no layout invalidation, no allocation per
    /// push (render-only transform, ADR-0006).</summary>
    public class RenderTransformBindingSample : SampleBase
    {
        public RenderTransformBindingViewModel ViewModel { get; } = new();

        //  The flight sweeps from the canvas' top-left corner (0,0) to a point well inside "FlightCanvas" (420x140),
        //  growing the 24x24 image from half size back to its original size, then holds before restarting.
        private static readonly Vector2 StartTranslation = Vector2.Zero;
        private static readonly Vector2 EndTranslation = new(360f, 90f);
        private static readonly Vector2 StartScale = new(0.5f, 0.5f);
        private static readonly Vector2 EndScale = Vector2.One;
        private const double SweepSeconds = 2.5;
        private const double HoldSeconds = 0.6;

        private readonly TimeSpan _cycleDuration = TimeSpan.FromSeconds(SweepSeconds + HoldSeconds);
        private TimeSpan _elapsed = TimeSpan.Zero;

        public RenderTransformBindingSample(ContentManager Content, MGDesktop Desktop)
            : base(Content, Desktop, "Features", "RenderTransformBinding.xaml")
        {
            ViewModel.FlyingTranslation = StartTranslation;
            ViewModel.FlyingScale = StartScale;
            Window.WindowDataContext = ViewModel;

            Window.OnEndUpdate += (sender, e) => Advance(e.UA.BA.FrameElapsed);
        }

        /// <summary>Advances the flight by one tick: a linear ramp over <see cref="SweepSeconds"/>, then a hold over
        /// <see cref="HoldSeconds"/> before restarting, written into <see cref="ViewModel"/> every tick.</summary>
        private void Advance(TimeSpan frameElapsed)
        {
            _elapsed += frameElapsed;
            if (_elapsed >= _cycleDuration)
            {
                _elapsed -= _cycleDuration;
            }

            double ratio = Math.Clamp(_elapsed.TotalSeconds / SweepSeconds, 0.0, 1.0);
            ViewModel.FlyingTranslation = Vector2.Lerp(StartTranslation, EndTranslation, (float)ratio);
            ViewModel.FlyingScale = Vector2.Lerp(StartScale, EndScale, (float)ratio);
        }
    }
}
