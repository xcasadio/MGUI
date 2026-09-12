using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;

namespace MGUI.Samples.Features
{
    /// <summary>Scenario SCN-ANIM-001 (Docs/scenario-validation-index.md): the animation system V1 (ADR-0006), XAML transitions and explicit animations.</summary>
    public class AnimationDemoSample : SampleBase
    {
        private static readonly Color[] BackgroundCycle = { new(0x3D, 0x6C, 0x9E), new(0x8E, 0x44, 0xAD), new(0x16, 0xA0, 0x85), new(0xD3, 0x54, 0x00) };

        private int _colorIndex;
        private MGWindow _popup;
        private MGBorder _popupRoot;

        public AnimationDemoSample(ContentManager content, MGDesktop desktop)
            : base(content, desktop, "Features", "AnimationDemo.xaml")
        {
            MGButton hoverButton = Window.GetElementByName<MGButton>("HoverButton");
            MGBorder target = Window.GetElementByName<MGBorder>("Target");
            MGBorder marginTarget = Window.GetElementByName<MGBorder>("MarginTarget");
            MGTextBlock activeCount = Window.GetElementByName<MGTextBlock>("ActiveCountText");
            target.RenderTransform.Origin = new Vector2(0.5f, 0.5f);

            //  The XAML transition on Background picks this local write up and fades from the previous colour.
            Window.GetElementByName<MGButton>("ColorButton").AddCommandHandler((btn, e) =>
            {
                _colorIndex = (_colorIndex + 1) % BackgroundCycle.Length;
                hoverButton.BackgroundBrush.NormalValue = new MGSolidFillBrush(BackgroundCycle[_colorIndex]);
            });

            Window.GetElementByName<MGButton>("FadeButton").AddCommandHandler((btn, e) =>
                target.Animations.Start(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity)
                {
                    From = 0f,
                    To = 1f,
                    Duration = TimeSpan.FromMilliseconds(450),
                    Easing = UIEasing.CubicOut,
                    Name = "fade",
                }));

            Window.GetElementByName<MGButton>("SlideButton").AddCommandHandler((btn, e) =>
                target.Animations.Start(new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformTranslation)
                {
                    From = Vector2.Zero,
                    To = new Vector2(120f, 0f),
                    Duration = TimeSpan.FromMilliseconds(350),
                    Easing = UIEasing.CubicInOut,
                    AutoReverse = true,
                    Name = "slide",
                }));

            Window.GetElementByName<MGButton>("SpinButton").AddCommandHandler((btn, e) =>
                target.Animations.Start(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.RenderTransformRotation)
                {
                    From = 0f,
                    To = 360f,
                    Duration = TimeSpan.FromMilliseconds(700),
                    Easing = UIEasing.QuadInOut,
                    FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
                    Name = "spin",
                }));

            //  Attention pulse: the state-driven scale override, repeated forth and back; a second click stops it.
            Window.GetElementByName<MGButton>("PulseButton").AddCommandHandler((btn, e) =>
            {
                if (target.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.RenderScale))
                {
                    target.Animations.Clear();
                    return;
                }

                target.Animations.Start(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.RenderScale)
                {
                    To = 1.15f,
                    Duration = TimeSpan.FromMilliseconds(250),
                    Easing = UIEasing.SineInOut,
                    AutoReverse = true,
                    RepeatForever = true,
                    FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
                    Name = "pulse",
                });
            });

            //  A layout pilot: every tick writes Margin with the Animation source and re-lays the row out.
            Window.GetElementByName<MGButton>("MarginButton").AddCommandHandler((btn, e) =>
                marginTarget.Animations.Start(new UIPropertyAnimation<Thickness>(UIBuiltInAnimationTargets.Paths.Margin)
                {
                    To = new Thickness(60, 0, 0, 0),
                    Duration = TimeSpan.FromMilliseconds(400),
                    Easing = UIEasing.BackOut,
                    AutoReverse = true,
                    FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
                    Name = "margin",
                }));

            Window.GetElementByName<MGButton>("PopupButton").AddCommandHandler((btn, e) => OpenPopup());

            MGButton pauseButton = Window.GetElementByName<MGButton>("PauseButton");
            pauseButton.AddCommandHandler((btn, e) =>
            {
                UIAnimationClock clock = Desktop.Animations.Clock;
                clock.IsPaused = !clock.IsPaused;
                pauseButton.SetContent(clock.IsPaused ? "Resume clock" : "Pause clock");
            });
            Window.GetElementByName<MGButton>("HalfSpeedButton").AddCommandHandler((btn, e) => Desktop.Animations.Clock.TimeScale = 0.5f);
            Window.GetElementByName<MGButton>("NormalSpeedButton").AddCommandHandler((btn, e) => Desktop.Animations.Clock.TimeScale = 1f);

            Window.OnEndUpdate += (sender, e) => activeCount.SetText($"Active animations: {Desktop.Animations.ActiveCount}");
        }

        /// <summary>Opens (or re-opens) a nested popup whose content fades and scales in from its centre, the "window opening" example of the specification.</summary>
        private void OpenPopup()
        {
            if (_popup == null)
            {
                _popup = new MGWindow(Window, 0, 0, 280, 130) { WindowStyle = WindowStyle.None, ActivatesOnClick = false };
                _popupRoot = new MGBorder(Window) { Padding = new Thickness(12), CornerRadius = new MGCornerRadius(8) };
                _popupRoot.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(0x34, 0x49, 0x5E));
                _popupRoot.RenderTransform.Origin = new Vector2(0.5f, 0.5f);
                MGStackPanel content = new(Window, Orientation.Vertical) { Spacing = 8 };
                content.TryAddChild(new MGTextBlock(Window, "A popup opening with opacity and scale.", Color.White));
                MGButton close = new(Window);
                close.SetContent("Close");
                close.AddCommandHandler((btn, e) => ClosePopup());
                content.TryAddChild(close);
                _popupRoot.SetContent(content);
                _popup.SetContent(_popupRoot);
            }

            _popup.Left = Window.Left + (Window.WindowWidth - _popup.WindowWidth) / 2;
            _popup.Top = Window.Top + (Window.WindowHeight - _popup.WindowHeight) / 2;
            if (!Window.NestedWindows.Contains(_popup))
            {
                Window.AddNestedWindow(_popup);
            }

            _popupRoot.Animations.Start(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity)
            {
                From = 0f,
                To = 1f,
                Duration = TimeSpan.FromMilliseconds(200),
                Easing = UIEasing.QuadOut,
                Name = "popup-fade",
            });
            _popupRoot.Animations.Start(new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale)
            {
                From = new Vector2(0.9f, 0.9f),
                To = Vector2.One,
                Duration = TimeSpan.FromMilliseconds(250),
                Easing = UIEasing.BackOut,
                Name = "popup-scale",
            });
        }

        private void ClosePopup()
        {
            if (_popup != null && Window.NestedWindows.Contains(_popup))
            {
                Window.RemoveNestedWindow(_popup);
            }
        }
    }
}
