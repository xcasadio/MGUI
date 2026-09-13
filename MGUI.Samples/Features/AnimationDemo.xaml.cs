using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MonoGame.Extended;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Samples.Features
{
    /// <summary>Scenarios SCN-ANIM-001 and SCN-ANIM-002 (Docs/scenario-validation-index.md): the animation system V1 (ADR-0006: XAML transitions and
    /// explicit animations) and V2 (ADR-0007: composition, keyframes, named visual states, style and theme animation, ProgressButton on the engine).</summary>
    public class AnimationDemoSample : SampleBase
    {
        private static readonly Color[] BackgroundCycle = { new(0x3D, 0x6C, 0x9E), new(0x8E, 0x44, 0xAD), new(0x16, 0xA0, 0x85), new(0xD3, 0x54, 0x00) };

        private int _colorIndex;
        private MGWindow _popup;
        private MGBorder _popupRoot;

        /// <summary>The V3 preview instance (U7): attached once, in <see cref="WireV3"/>, to the dedicated <c>V3PreviewTarget</c> element,
        /// driven by <c>V3SeekSlider</c> and ended by <c>V3DetachPreview</c>. Never registered with the live <see cref="UIAnimationManager"/>.</summary>
        private UIStoryboard _V3PreviewAnimation;

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

            WireV2();
            WireV3();

            MGTextBlock stateText = Window.GetElementByName<MGTextBlock>("StateText");
            MGToggleButton stateToggle = Window.GetElementByName<MGToggleButton>("StateToggle");
            Window.OnEndUpdate += (sender, e) =>
            {
                activeCount.SetText($"Active animations: {Desktop.Animations.ActiveCount}");
                stateText.SetText($"state: {stateToggle.CurrentVisualStateName ?? "none"}");
            };
        }

        /// <summary>SCN-ANIM-002: composition, the fluent API, keyframes, the theme timings and the engine-driven progress button.</summary>
        private void WireV2()
        {
            MGBorder v2Target = Window.GetElementByName<MGBorder>("V2Target");

            //  A storyboard: three children in parallel, each a normal registration on its own path.
            Window.GetElementByName<MGButton>("StoryboardButton").AddCommandHandler((btn, e) =>
                v2Target.Animations.Start(new UIStoryboard
                {
                    new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.QuadOut, Name = "open-fade" },
                    new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale) { From = new Vector2(0.85f), To = Vector2.One, Duration = TimeSpan.FromMilliseconds(350), Easing = UIEasing.BackOut, Name = "open-scale" },
                    new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformTranslation) { From = new Vector2(0f, 24f), To = Vector2.Zero, Duration = TimeSpan.FromMilliseconds(350), Easing = UIEasing.CubicOut, Name = "open-slide" },
                }.Named("open")));

            //  The fluent API builds a sequence: fade, then a spin, then a pause, then the keyframe pop.
            Window.GetElementByName<MGButton>("SequenceButton").AddCommandHandler((btn, e) =>
                v2Target
                    .Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0f, 1f, 0.2).Ease(UIEasing.QuadOut).Named("sequence")
                    .Then(UIBuiltInAnimationTargets.Paths.RenderTransformRotation, 0f, 360f, 0.5).Ease("CubicInOut").Fill(UIAnimationFillBehavior.RestoreBaseValue)
                    .Wait(0.1)
                    .Then(CreatePop())
                    .Play());

            Window.GetElementByName<MGButton>("PopButton").AddCommandHandler((btn, e) => v2Target.Animations.Start(CreatePop()));

            //  The theme timings: a copy of the desktop theme with the Animation group toggled; every button of the desktop follows.
            MGButton themeButton = Window.GetElementByName<MGButton>("ThemeAnimationButton");
            themeButton.SetContent(Desktop.Theme.Animation.Enabled ? "Theme animation: on" : "Theme animation: off");
            themeButton.AddCommandHandler((btn, e) =>
            {
                MGTheme theme = Desktop.Theme.Copy();
                theme.Animation.Enabled = !theme.Animation.Enabled;
                Desktop.Resources.DefaultTheme = theme;
                themeButton.SetContent(theme.Animation.Enabled ? "Theme animation: on" : "Theme animation: off");
            });
        }

        /// <summary>SCN-ANIM-003: Bezier easings in XAML, a visual state that overrides a local value, the toggle Checked slot, a keyframe clip
        /// played from embedded JSON, a preview driven by Seek, storyboard save/load, and the clock-driven typewriter/highlight brush.</summary>
        private void WireV3()
        {
            //  U1: the button's own Transition (declared in XAML with a "cubic-bezier(...)" literal) interpolates every local write to
            //  RenderTransform.Translation with a back curve; the click handler here only supplies the local writes to interpolate.
            MGButton bezierButton = Window.GetElementByName<MGButton>("V3BezierButton");
            bool bezierSlideOut = false;
            bezierButton.AddCommandHandler((btn, e) =>
            {
                bezierSlideOut = !bezierSlideOut;
                btn.RenderTransform.Translation = new Vector2(bezierSlideOut ? 60f : 0f, 0f);
            });

            //  U4: V3OverridingPanel/V3PlainPanel are declared entirely in XAML (a local Background attribute plus a Hover
            //  VisualStateDefinition, one with OverridesLocalValue="True"); nothing to wire from code.

            //  U5: CheckedBackgroundBrush is code-only (ADR-0008 decision 5/U5) -- set here so the checked look differs from the theme's
            //  Selected look. V3PlainToggle is left without one, so it falls back to VisualStateBrush<T>.SelectedValue when checked.
            Window.GetElementByName<MGToggleButton>("V3CheckedToggle").CheckedBackgroundBrush = new MGSolidFillBrush(new Color(0xE6, 0x7E, 0x22));

            //  U6: an embedded JSON clip (three tracks: Opacity, RenderTransform.Scale, Background) deserialised into a fresh UIStoryboard
            //  and started on its own target every click (UIKeyFrameClipSerializer.Deserialize allocates a new, unowned storyboard each call).
            MGBorder clipTarget = Window.GetElementByName<MGBorder>("V3ClipTarget");
            Window.GetElementByName<MGButton>("V3PlayClip").AddCommandHandler((btn, e) =>
                clipTarget.Animations.Start(UIKeyFrameClipSerializer.Deserialize(AnimationDemoV3Assets.ClipJson)));

            //  U7: the same clip, attached once as a preview (never started for real, never registered with the manager) on a target that
            //  is never touched by a live animation -- the coexistence limit documented in Docs/animation-architecture.md, "Preview et seek".
            MGBorder previewTarget = Window.GetElementByName<MGBorder>("V3PreviewTarget");
            _V3PreviewAnimation = UIAnimationPreview.Attach(previewTarget, UIKeyFrameClipSerializer.Deserialize(AnimationDemoV3Assets.ClipJson));
            TimeSpan previewDuration = TimeSpan.FromSeconds(1);
            Window.GetElementByName<MGSlider>("V3SeekSlider").ValueChanged += (sender, e) =>
                _V3PreviewAnimation.Seek(TimeSpan.FromTicks((long)(previewDuration.Ticks * (e.NewValue / 100f))));
            Window.GetElementByName<MGButton>("V3DetachPreview").AddCommandHandler((btn, e) => UIAnimationPreview.Detach(_V3PreviewAnimation));

            //  U8: save the demo's own small V3 storyboard (built fresh, never started, so its nodes have no Owner) to JSON with
            //  UIAnimationSerializer.Serialize, then rebuild and play it from the JSON text with Deserialize. namedElements is the small
            //  name -> element dictionary a resolveElement delegate needs whenever a serialised node names an element other than the one
            //  it is played on (none of them do here, since every node's Owner is unset, but the wiring is the same either way).
            Dictionary<string, MGElement> namedElements = new() { ["V3ClipTarget"] = clipTarget };
            MGTextBox storyboardJson = Window.GetElementByName<MGTextBox>("V3StoryboardJson");
            Window.GetElementByName<MGButton>("V3SaveStoryboard").AddCommandHandler((btn, e) =>
            {
                UIStoryboard storyboard = new UIStoryboard
                {
                    new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.QuadOut, Name = "v3-save-fade" },
                    new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale) { From = new Vector2(0.85f), To = Vector2.One, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.BackOut, Name = "v3-save-scale" },
                }.Named("v3-save");
                storyboardJson.Text = UIAnimationSerializer.Serialize(storyboard, element => namedElements.FirstOrDefault(kv => kv.Value == element).Key);
            });
            Window.GetElementByName<MGButton>("V3LoadStoryboard").AddCommandHandler((btn, e) =>
            {
                if (!string.IsNullOrEmpty(storyboardJson.Text))
                {
                    clipTarget.Animations.Start(UIAnimationSerializer.Deserialize(storyboardJson.Text, name => namedElements.TryGetValue(name, out MGElement element) ? element : null));
                }
            });

            //  U10: the typewriter reveal (TextCharactersPerSecond set from code) and the highlight brush both follow
            //  Desktop.Animations.Clock (pause and TimeScale) since U10; TextProgress = 0 replays a completed or in-progress reveal (the U10 fix round).
            //  Set from code, not from a XAML attribute (SamplePath's own strict-mode load, in MGUI.Tests, has no code-behind and must not
            //  start any animation by itself: MGUI.Tests/Animation/AnimationDemoSampleTests.cs pins Desktop.Animations.ActiveCount == 0 there).
            MGTextBlock typewriter = Window.GetElementByName<MGTextBlock>("V3Typewriter");
            typewriter.TextCharactersPerSecond = 12;
            MGToggleButton pauseClockToggle = Window.GetElementByName<MGToggleButton>("V3PauseClock");
            pauseClockToggle.OnCheckStateChanged += (sender, e) => Desktop.Animations.Clock.IsPaused = pauseClockToggle.IsChecked;
            Window.GetElementByName<MGButton>("V3ReplayText").AddCommandHandler((btn, e) => typewriter.TextProgress = 0);

            MGBorder highlight = Window.GetElementByName<MGBorder>("V3Highlight");
            highlight.BorderBrush = new MGHighlightBorderBrush(highlight.BorderBrush, new Color(0xF1, 0xC4, 0x0F), HighlightAnimation.Progress, highlight);
            Window.GetElementByName<MGSlider>("V3TimeScale").ValueChanged += (sender, e) => Desktop.Animations.Clock.TimeScale = e.NewValue;
        }

        /// <summary>A keyframe pop of the scale: 0.8 -> 1.1 (BackOut) -> 1.0 (QuadOut) over 400 ms.</summary>
        private static UIKeyFrameAnimation<Vector2> CreatePop() => new(UIBuiltInAnimationTargets.Paths.RenderTransformScale)
        {
            Duration = TimeSpan.FromMilliseconds(400),
            Track = { { 0f, new Vector2(0.8f) }, { 0.6f, new Vector2(1.1f), "BackOut" }, { 1f, Vector2.One, "QuadOut" } },
            Name = "pop",
        };

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

    internal static class AnimationDemoExtensions
    {
        /// <summary>Names a storyboard inside a collection-initializer expression.</summary>
        public static UIStoryboard Named(this UIStoryboard storyboard, string name)
        {
            storyboard.Name = name;
            return storyboard;
        }
    }
}
