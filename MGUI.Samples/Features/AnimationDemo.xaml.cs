using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Interpolation;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Core.Tooling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MGUI.Samples.Features
{
    /// <summary>One page per engine capability, built on the same skeleton as the control sample pages
    /// (<c>MGUI.Samples/Controls/CheckBox.xaml</c>). Each section wires its own demonstration; see the corresponding
    /// <c>WireXxx</c> method below for the capability it covers. Covers the "animation" validation scenario of
    /// Docs/scenario-validation-index.md.</summary>
    public class AnimationDemoSample : SampleBase
    {
        private static readonly Color[] BackgroundCycle = { new(0x3D, 0x6C, 0x9E), new(0x8E, 0x44, 0xAD), new(0x16, 0xA0, 0x85), new(0xD3, 0x54, 0x00) };
        private int _backgroundCycleIndex;

        /// <summary>The "Preview and seek" section's preview instance: attached once to <c>PreviewTarget</c>, driven by <c>PreviewSlider</c>
        /// and ended by <c>PreviewDetachButton</c>. Never registered with the live <see cref="UIAnimationManager"/>.</summary>
        private UIStoryboard _previewAnimation;

        /// <summary>The "Awaitable animations" section's current run, cancelled and replaced by every "Play" click, and cancelled alone by
        /// "Cancel". Null until the first click.</summary>
        private CancellationTokenSource _awaitCancellation;

        public AnimationDemoSample(ContentManager content, MGDesktop desktop)
            : base(content, desktop, "Features", "AnimationDemo.xaml", () => RegisterSharedStyle(desktop))
        {
            WireTransitions();
            WireFluent();
            WireTransform();
            WireClock();
            WireComposition();
            WireKeyFrames();
            WireStates();
            WireStyles();
            WireBrushes();
            WireHighlight();
            WirePreview();
            WireSerialization();
            WireControls();
            WireDiagnostics();
            WireAwait();
            WireScrolling();
            WireFrames();
            WireLayout();
        }

        /// <summary>Registers the named style the "Styles and theme" section starts from, before the XAML parses (so <c>StyleNames</c> can
        /// resolve it immediately): a resource style (<see cref="MGResources.AddStyle"/>), not an inline <c>&lt;Window.Styles&gt;</c> one, so
        /// <see cref="WireStyles"/> can fetch and mutate the same instance later with <see cref="MGResources.TryGetStyle"/>.</summary>
        private static void RegisterSharedStyle(MGDesktop desktop)
        {
            desktop.Resources.AddStyle("StylesSharedStyle", new Style
            {
                TargetType = MGElementType.Button,
                Transitions = { new Transition { Property = "RenderTransform.Scale", Duration = "0.12", Easing = "CubicOut" } },
                VisualStates = { new VisualStateDefinition { Name = "Hover", Setters = { new Setter { Property = "RenderTransform.Scale", Value = new Vector2(1.08f) } } } },
            });
        }

        /// <summary>1. Transitions: <c>TransitionsHoverButton</c> interpolates <c>RenderScale</c> (a named easing) and <c>Background</c> (a
        /// Bezier literal with a <c>Delay</c>), both declared in XAML; <c>TransitionsRetargetButton</c> supplies the local write that the
        /// Background transition retargets to mid-run.</summary>
        private void WireTransitions()
        {
            MGButton hoverButton = Window.GetElementByName<MGButton>("TransitionsHoverButton");
            Window.GetElementByName<MGButton>("TransitionsRetargetButton").AddCommandHandler((btn, e) =>
            {
                _backgroundCycleIndex = (_backgroundCycleIndex + 1) % BackgroundCycle.Length;
                hoverButton.BackgroundBrush.NormalValue = new MGSolidFillBrush(BackgroundCycle[_backgroundCycleIndex]);
            });
        }

        /// <summary>2. Explicit animations and the fluent API: Fade (a plain <see cref="UIPropertyAnimation{T}"/>), Bounce
        /// (<c>Animate(...).Ease("BackOut").AutoReverse().Repeat(3)</c>), Sequence (<c>Then</c>/<c>Wait</c>), Cancel
        /// (<see cref="UIAnimationCollection.Clear"/>), all on <c>FluentTarget</c>.</summary>
        private void WireFluent()
        {
            MGBorder target = Window.GetElementByName<MGBorder>("FluentTarget");

            Window.GetElementByName<MGButton>("FluentFadeButton").AddCommandHandler((btn, e) =>
                target.Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0f, 1f, 0.35).Ease(UIEasing.CubicOut).Named("fade").Play());

            Window.GetElementByName<MGButton>("FluentBounceButton").AddCommandHandler((btn, e) =>
                target.Animate(UIBuiltInAnimationTargets.Paths.RenderTransformScale, new Vector2(1.25f), 0.25).Ease("BackOut")
                    .AutoReverse().Repeat(3).Fill(UIAnimationFillBehavior.RestoreBaseValue).Named("bounce").Play());

            Window.GetElementByName<MGButton>("FluentSequenceButton").AddCommandHandler((btn, e) =>
                target.Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0f, 1f, 0.2).Ease(UIEasing.QuadOut).Named("sequence")
                    .Then(UIBuiltInAnimationTargets.Paths.RenderTransformRotation, 0f, 360f, 0.5).Ease("CubicInOut").Fill(UIAnimationFillBehavior.RestoreBaseValue)
                    .Wait(0.1)
                    .Then(AnimationDemoAssets.CreatePop())
                    .Play());

            Window.GetElementByName<MGButton>("FluentCancelButton").AddCommandHandler((btn, e) => target.Animations.Clear());
        }

        /// <summary>3. Render transform and state scale: two sliders write <c>TransformTarget.RenderTransform.Rotation</c> and
        /// <c>.Scale</c> directly (both relative to <c>Origin="0.5,0.5"</c>); <c>TransformHoverButton</c> demonstrates the separate,
        /// state-driven <c>RenderScale</c> override, always centred regardless of <c>Origin</c>.</summary>
        private void WireTransform()
        {
            MGBorder target = Window.GetElementByName<MGBorder>("TransformTarget");
            Window.GetElementByName<MGSlider>("TransformRotationSlider").ValueChanged += (sender, e) => target.RenderTransform.Rotation = e.NewValue;
            Window.GetElementByName<MGSlider>("TransformScaleSlider").ValueChanged += (sender, e) => target.RenderTransform.Scale = new Vector2(e.NewValue);
        }

        /// <summary>4. Clock: <c>ClockPauseToggle</c> and <c>ClockTimeScaleSlider</c> write <see cref="UIAnimationClock.IsPaused"/> and
        /// <see cref="UIAnimationClock.TimeScale"/> on <see cref="MGDesktop.Animations"/>'s clock directly; <c>ClockActiveCountText</c>
        /// reads <see cref="UIAnimationManager.ActiveCount"/> once per frame.</summary>
        private void WireClock()
        {
            MGToggleButton pauseToggle = Window.GetElementByName<MGToggleButton>("ClockPauseToggle");
            pauseToggle.OnCheckStateChanged += (sender, e) => Desktop.Animations.Clock.IsPaused = pauseToggle.IsChecked;
            Window.GetElementByName<MGSlider>("ClockTimeScaleSlider").ValueChanged += (sender, e) => Desktop.Animations.Clock.TimeScale = e.NewValue;

            MGTextBlock activeCount = Window.GetElementByName<MGTextBlock>("ClockActiveCountText");
            Window.OnEndUpdate += (sender, e) => activeCount.SetText($"Active animations: {Desktop.Animations.ActiveCount}");
        }

        /// <summary>5. Composition: "Play storyboard" starts a <see cref="UIStoryboard"/> whose two children run in parallel, one on each of
        /// <c>CompositionTargetA</c> and <c>CompositionTargetB</c>; "Play sequence" starts a <see cref="UISequenceAnimation"/> (child, delay,
        /// child) across the same two elements. A child that is not started on its own element first (the start-then-cancel idiom below)
        /// runs on the root the composite is started on instead, per <see cref="UIAnimation.PresetOwner"/>'s own documented rule.</summary>
        private void WireComposition()
        {
            MGBorder targetA = Window.GetElementByName<MGBorder>("CompositionTargetA");
            MGBorder targetB = Window.GetElementByName<MGBorder>("CompositionTargetB");

            Window.GetElementByName<MGButton>("CompositionStoryboardButton").AddCommandHandler((btn, e) =>
            {
                UIPropertyAnimation<Vector2> scaleB = new(UIBuiltInAnimationTargets.Paths.RenderTransformScale) { From = new Vector2(0.8f), To = Vector2.One, Duration = TimeSpan.FromMilliseconds(350), Easing = UIEasing.BackOut, Name = "storyboard-scale-b" };
                PresetOwner(targetB, scaleB);
                UIStoryboard storyboard = new()
                {
                    new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0.3f, To = 1f, Duration = TimeSpan.FromMilliseconds(350), Easing = UIEasing.QuadOut, Name = "storyboard-fade-a" },
                    scaleB,
                };
                storyboard.Name = "storyboard";
                targetA.Animations.Start(storyboard);
            });

            Window.GetElementByName<MGButton>("CompositionSequenceButton").AddCommandHandler((btn, e) =>
            {
                UIPropertyAnimation<float> fadeB = new(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0.3f, To = 1f, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.QuadOut, Name = "sequence-fade-b" };
                PresetOwner(targetB, fadeB);
                targetA.Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0.3f, 1f, 0.3).Ease(UIEasing.QuadOut).Named("sequence-fade-a")
                    .Wait(0.15)
                    .Then(fadeB)
                    .Play();
            });
        }

        /// <summary>Presets <paramref name="animation"/>'s <see cref="UIAnimation.Owner"/> to <paramref name="element"/> without playing it:
        /// starting it (which calls the internal <c>PresetOwner</c>) then immediately cancelling leaves it <c>Stopped</c> again with its
        /// owner still set, per <see cref="UIAnimationSerializer"/>'s own documented "Start-then-Cancel idiom" for presetting a child's owner
        /// from outside <c>MGUI.Core</c> (<c>PresetOwner</c> itself is internal).</summary>
        private static void PresetOwner(MGElement element, UIAnimation animation)
        {
            element.Animations.Start(animation);
            animation.Cancel();
        }

        /// <summary>6. Keyframes and clip: "Play keyframes" starts <see cref="AnimationDemoAssets.CreatePop"/> on <c>KeyframesTarget</c>;
        /// "Load clip" deserialises the embedded JSON clip (<see cref="AnimationDemoAssets.ClipJson"/>, shown read-only in
        /// <c>ClipJsonText</c>) with <see cref="UIKeyFrameClipSerializer.Deserialize"/> and plays it on <c>ClipTarget</c>.</summary>
        private void WireKeyFrames()
        {
            MGBorder keyframesTarget = Window.GetElementByName<MGBorder>("KeyframesTarget");
            Window.GetElementByName<MGButton>("KeyframesPopButton").AddCommandHandler((btn, e) => keyframesTarget.Animations.Start(AnimationDemoAssets.CreatePop()));

            MGBorder clipTarget = Window.GetElementByName<MGBorder>("ClipTarget");
            Window.GetElementByName<MGButton>("ClipPlayButton").AddCommandHandler((btn, e) =>
                clipTarget.Animations.Start(UIKeyFrameClipSerializer.Deserialize(AnimationDemoAssets.ClipJson)));
            Window.GetElementByName<MGTextBox>("ClipJsonText").Text = AnimationDemoAssets.ClipJson;
        }

        /// <summary>7. Named visual states: <c>StatesToggle</c> declares <c>Hover</c>/<c>Pressed</c>/<c>Checked</c> in XAML;
        /// <c>StatesOverridingPanel</c> (<c>OverridesLocalValue="True"</c>) wins over its local <c>Background</c> on hover,
        /// <c>StatesPlainPanel</c> (same local value, same Hover setter, no override) does not; <c>StatesCheckedToggle</c> gets a code-only
        /// <see cref="MGToggleButton.CheckedBackgroundBrush"/>, distinct from the theme's Selected look that <c>StatesPlainToggle</c> falls
        /// back to when checked.</summary>
        private void WireStates() =>
            Window.GetElementByName<MGToggleButton>("StatesCheckedToggle").CheckedBackgroundBrush = new MGSolidFillBrush(new Color(0xE6, 0x7E, 0x22));

        /// <summary>8. Styles and theme: <c>StylesButtonA</c>/<c>StylesButtonB</c> share the resource style registered by
        /// <see cref="RegisterSharedStyle"/> (a <see cref="Style.Transitions"/> entry and a <see cref="Style.VisualStates"/> entry);
        /// "Refresh styles" mutates that same <see cref="Style"/> instance (toggles the Hover scale between two values) and calls
        /// <see cref="MGElement.RefreshStyles"/> on both buttons, per <see cref="MGResources.TryGetStyle"/>. The theme animation button
        /// mirrors the well-known <c>MGTheme.Animation</c> toggle: a copy of the desktop theme with <c>Animation.Enabled</c> flipped,
        /// applied as <see cref="MGResources.DefaultTheme"/> so every Button/ToggleButton of the desktop follows.</summary>
        private void WireStyles()
        {
            MGButton buttonA = Window.GetElementByName<MGButton>("StylesButtonA");
            MGButton buttonB = Window.GetElementByName<MGButton>("StylesButtonB");
            bool wideHoverScale = false;
            Window.GetElementByName<MGButton>("StylesRefreshButton").AddCommandHandler((btn, e) =>
            {
                if (Desktop.Resources.TryGetStyle("StylesSharedStyle", out Style style))
                {
                    wideHoverScale = !wideHoverScale;
                    style.VisualStates[0].Setters[0].Value = new Vector2(wideHoverScale ? 1.2f : 1.08f);
                    buttonA.RefreshStyles();
                    buttonB.RefreshStyles();
                }
            });

            MGButton themeButton = Window.GetElementByName<MGButton>("StylesThemeAnimationButton");
            themeButton.SetContent(Desktop.Theme.Animation.Enabled ? "Theme animation: on" : "Theme animation: off");
            themeButton.AddCommandHandler((btn, e) =>
            {
                MGTheme theme = Desktop.Theme.Copy();
                theme.Animation.Enabled = !theme.Animation.Enabled;
                Desktop.Resources.DefaultTheme = theme;
                themeButton.SetContent(theme.Animation.Enabled ? "Theme animation: on" : "Theme animation: off");
            });
        }

        /// <summary>9. Animatable brushes: <c>BrushesSharedLeftButton</c>/<c>BrushesSharedRightButton</c> share one frozen palette brush
        /// (<see cref="SolidFillBrushes.CornflowerBlue"/>); "Animate left only" clones and animates only the left button's <c>Background</c>,
        /// leaving the frozen brush and the right button untouched. <c>BrushesInlineSlider</c> mutates an unfrozen, element-owned
        /// <see cref="MGSolidFillBrush"/> in place (no engine involved). "Animate gradient"/"Animate border" clone-and-mutate a
        /// <see cref="MGGradientFillBrush"/> and a <see cref="MGUniformBorderBrush"/> through the engine's <c>Background.Gradient</c> and
        /// <c>BorderBrush</c> targets.</summary>
        private void WireBrushes()
        {
            MGButton sharedLeft = Window.GetElementByName<MGButton>("BrushesSharedLeftButton");
            MGButton sharedRight = Window.GetElementByName<MGButton>("BrushesSharedRightButton");
            MGSolidFillBrush shared = SolidFillBrushes.CornflowerBlue;
            sharedLeft.BackgroundBrush.NormalValue = shared;
            sharedRight.BackgroundBrush.NormalValue = shared;
            Window.GetElementByName<MGButton>("BrushesAnimateLeftButton").AddCommandHandler((btn, e) =>
                sharedLeft.Animate(UIColorAnimationTargets.Paths.Background, Color.OrangeRed, 0.6).AutoReverse().Named("brush-left").Play());

            MGBorder inlineTarget = Window.GetElementByName<MGBorder>("BrushesInlineTarget");
            MGSolidFillBrush inlineBrush = new(Color.DimGray);
            inlineTarget.BackgroundBrush.NormalValue = inlineBrush;
            Window.GetElementByName<MGSlider>("BrushesInlineSlider").ValueChanged += (sender, e) => inlineBrush.Color = Color.Lerp(Color.DimGray, Color.LightGreen, e.NewValue);

            MGBorder gradientTarget = Window.GetElementByName<MGBorder>("BrushesGradientTarget");
            gradientTarget.BackgroundBrush.NormalValue = new MGGradientFillBrush(Color.DarkRed, Color.DarkOrange, Color.DarkRed, Color.DarkOrange);
            Window.GetElementByName<MGButton>("BrushesGradientButton").AddCommandHandler((btn, e) =>
                gradientTarget.Animate(UIExtraAnimationTargets.Paths.BackgroundGradient, new UIGradientColors(Color.MediumPurple, Color.MediumBlue, Color.MediumPurple, Color.MediumBlue), 1.0)
                    .AutoReverse().Named("brush-gradient").Play());

            MGBorder borderTarget = Window.GetElementByName<MGBorder>("BrushesBorderTarget");
            borderTarget.BorderBrush = new MGUniformBorderBrush(new MGSolidFillBrush(Color.White));
            Window.GetElementByName<MGButton>("BrushesBorderButton").AddCommandHandler((btn, e) =>
                borderTarget.Animate(UIColorAnimationTargets.Paths.BorderBrush, Color.Crimson, 0.6).AutoReverse().Named("brush-border").Play());
        }

        /// <summary>10. Highlight: <c>HighlightAutoBorder</c> (default <see cref="MGHighlightBorderBrush.AutoStart"/> = true) starts its own
        /// <c>Pulse</c> run as soon as it becomes the effective border; <c>HighlightManualBorder</c> (<c>AutoStart = false</c>) stays still
        /// until <c>HighlightStartStopButton</c> drives <c>BorderBrush.Highlight.Progress</c> itself; <c>HighlightStopOnHoverBorder</c>
        /// (<c>StopOnMouseOver = true</c>) stops on hover and needs <c>HighlightResumeButton</c> (<see cref="MGElement.ResumeBorderHighlight"/>)
        /// to restart, since a suppressed run never resumes on its own.</summary>
        private void WireHighlight()
        {
            MGBorder autoBorder = Window.GetElementByName<MGBorder>("HighlightAutoBorder");
            autoBorder.BorderBrush = new MGHighlightBorderBrush(autoBorder.BorderBrush, new Color(0xF1, 0xC4, 0x0F), HighlightAnimation.Pulse);

            MGBorder manualBorder = Window.GetElementByName<MGBorder>("HighlightManualBorder");
            manualBorder.BorderBrush = new MGHighlightBorderBrush(manualBorder.BorderBrush, new Color(0x3D, 0xC6, 0xE0), HighlightAnimation.Scan) { AutoStart = false };
            Window.GetElementByName<MGButton>("HighlightStartStopButton").AddCommandHandler((btn, e) =>
            {
                if (manualBorder.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.BorderBrushHighlightProgress))
                {
                    manualBorder.Animations.Clear();
                }
                else
                {
                    manualBorder.Animate(UIBuiltInAnimationTargets.Paths.BorderBrushHighlightProgress, 0.0, 1.0, 1.5).RepeatForever().Named("highlight-manual").Play();
                }
            });

            MGBorder stopOnHoverBorder = Window.GetElementByName<MGBorder>("HighlightStopOnHoverBorder");
            stopOnHoverBorder.BorderBrush = new MGHighlightBorderBrush(stopOnHoverBorder.BorderBrush, new Color(0x27, 0xAE, 0x60), HighlightAnimation.Flash) { StopOnMouseOver = true };
            Window.GetElementByName<MGButton>("HighlightResumeButton").AddCommandHandler((btn, e) => stopOnHoverBorder.ResumeBorderHighlight());
        }

        /// <summary>11. Preview and seek: a fresh deserialisation of the embedded clip attached once as a preview on <c>PreviewTarget</c>
        /// (never registered with <see cref="MGDesktop.Animations"/>), driven by <c>PreviewSlider</c>'s 0..100 % and ended by
        /// <c>PreviewDetachButton</c> (<see cref="UIAnimationPreview.Detach"/>).</summary>
        private void WirePreview()
        {
            MGBorder previewTarget = Window.GetElementByName<MGBorder>("PreviewTarget");
            _previewAnimation = UIAnimationPreview.Attach(previewTarget, UIKeyFrameClipSerializer.Deserialize(AnimationDemoAssets.ClipJson));
            TimeSpan previewDuration = TimeSpan.FromSeconds(1);
            Window.GetElementByName<MGSlider>("PreviewSlider").ValueChanged += (sender, e) =>
            {
                // Once "Detach" has cancelled the preview, it is no longer active (Seek would throw): the slider becomes a no-op
                // until a fresh preview is attached again.
                if (_previewAnimation.IsActive)
                {
                    _previewAnimation.Seek(TimeSpan.FromTicks((long)(previewDuration.Ticks * (e.NewValue / 100f))));
                }
            };
            Window.GetElementByName<MGButton>("PreviewDetachButton").AddCommandHandler((btn, e) => UIAnimationPreview.Detach(_previewAnimation));
        }

        /// <summary>12. Serialisation: "Save" serialises <see cref="AnimationDemoAssets.CreateDemoStoryboard"/> (built fresh, never started,
        /// so every node's Owner is unset) to JSON with <see cref="UIAnimationSerializer.Serialize"/> into the read-only
        /// <c>SerializationJsonText</c>; "Load + play" rebuilds an independent copy from that text with
        /// <see cref="UIAnimationSerializer.Deserialize"/> and starts it on <c>SerializationTarget</c>. <c>namedElements</c> is the small
        /// name-to-element dictionary a <c>resolveElement</c> delegate needs whenever a serialised node names an element other than the one
        /// it is played on (none of them do here, since every node's Owner is unset, but the wiring is the same either way).</summary>
        private void WireSerialization()
        {
            MGBorder serializationTarget = Window.GetElementByName<MGBorder>("SerializationTarget");
            Dictionary<string, MGElement> namedElements = new() { ["SerializationTarget"] = serializationTarget };
            MGTextBox jsonText = Window.GetElementByName<MGTextBox>("SerializationJsonText");

            Window.GetElementByName<MGButton>("SerializationSaveButton").AddCommandHandler((btn, e) =>
                jsonText.Text = UIAnimationSerializer.Serialize(AnimationDemoAssets.CreateDemoStoryboard(), element => namedElements.FirstOrDefault(kv => kv.Value == element).Key));
            Window.GetElementByName<MGButton>("SerializationLoadButton").AddCommandHandler((btn, e) =>
            {
                if (!string.IsNullOrEmpty(jsonText.Text))
                {
                    serializationTarget.Animations.Start(UIAnimationSerializer.Deserialize(jsonText.Text, name => namedElements.TryGetValue(name, out MGElement element) ? element : null));
                }
            });
        }

        /// <summary>13. Controls on the engine: <c>EngineProgressButton.Duration</c> and <c>TypewriterText.TextCharactersPerSecond</c> (set
        /// here, not from a XAML attribute, so this class's absence from a strict-mode XAML-only load -- see
        /// <c>MGUI.Tests/Animation/AnimationDemoSampleTests.cs</c> -- starts no animation by itself) both run on
        /// <see cref="MGDesktop.Animations"/>'s clock; <c>EngineReplayButton</c> seeks the reveal back to the start with
        /// <c>TextProgress = 0</c>.</summary>
        private void WireControls()
        {
            MGTextBlock typewriter = Window.GetElementByName<MGTextBlock>("TypewriterText");
            typewriter.TextCharactersPerSecond = 12;
            Window.GetElementByName<MGButton>("EngineReplayButton").AddCommandHandler((btn, e) => typewriter.TextProgress = 0);
        }

        /// <summary>14. Diagnostics: "Show applicable paths" captures <see cref="UIToolingService.CaptureElementDebugView"/> of
        /// <c>TransitionsHoverButton</c> and lists its <see cref="UIElementDebugView.ApplicablePaths"/> in <c>DiagnosticsPathsText</c>;
        /// "Measure per-tick allocations" starts a colour transition on <c>DiagnosticsTarget</c>, then measures
        /// <see cref="GC.GetAllocatedBytesForCurrentThread"/> over the next 120 desktop update ticks and reports the average.</summary>
        private void WireDiagnostics()
        {
            MGTextBox pathsText = Window.GetElementByName<MGTextBox>("DiagnosticsPathsText");
            Window.GetElementByName<MGButton>("DiagnosticsPathsButton").AddCommandHandler((btn, e) =>
                pathsText.Text = string.Join(", ", UIToolingService.CaptureElementDebugView(Window.GetElementByName<MGButton>("TransitionsHoverButton")).ApplicablePaths));

            MGBorder measureTarget = Window.GetElementByName<MGBorder>("DiagnosticsTarget");
            MGTextBlock allocationText = Window.GetElementByName<MGTextBlock>("DiagnosticsAllocationText");
            int ticksRemaining = 0;
            long bytesAtWindowStart = 0;
            Window.GetElementByName<MGButton>("DiagnosticsMeasureButton").AddCommandHandler((btn, e) =>
            {
                measureTarget.Animate(UIColorAnimationTargets.Paths.Background, measureTarget.BackgroundBrush.NormalValue is MGSolidFillBrush current && current.Color == Color.SlateBlue ? Color.CornflowerBlue : Color.SlateBlue, 4.0)
                    .AutoReverse().RepeatForever().Named("diagnostics-probe").Play();
                ticksRemaining = 120;
            });
            Window.OnEndUpdate += (sender, e) =>
            {
                if (ticksRemaining == 120)
                {
                    bytesAtWindowStart = GC.GetAllocatedBytesForCurrentThread();
                }

                if (ticksRemaining > 0)
                {
                    ticksRemaining--;
                    if (ticksRemaining == 0)
                    {
                        double perTick = (GC.GetAllocatedBytesForCurrentThread() - bytesAtWindowStart) / 120.0;
                        allocationText.SetText($"Allocations per tick: {perTick:0.0} bytes (over 120 ticks)");
                        measureTarget.Animations.Clear();
                    }
                }
            };
        }

        /// <summary>15. Awaitable animations: "Play three steps" creates a fresh <see cref="CancellationTokenSource"/> (cancelling and
        /// disposing the previous one), then an <c>async</c> handler <c>await</c>s a fade, a scale pop and a background colour change on
        /// <c>AwaitTarget</c> in turn, passing the same token to each <see cref="UIAnimationBuilder.PlayAsync"/> call; it stops at the first
        /// step that resolves <see langword="false"/> and writes "Completed" or "Cancelled at step N" into <c>AwaitResultText</c>. "Cancel"
        /// only cancels the current source: nothing here throws, per <see cref="UIAnimationCollection.StartAsync"/>'s contract.</summary>
        private void WireAwait()
        {
            MGBorder target = Window.GetElementByName<MGBorder>("AwaitTarget");
            MGTextBlock resultText = Window.GetElementByName<MGTextBlock>("AwaitResultText");

            Window.GetElementByName<MGButton>("AwaitPlayButton").AddCommandHandler(async (btn, e) =>
            {
                _awaitCancellation?.Cancel();
                _awaitCancellation?.Dispose();
                CancellationTokenSource cancellation = new();
                _awaitCancellation = cancellation;
                CancellationToken token = cancellation.Token;

                resultText.SetText("Running: step 1 (fade)");
                if (!await target.Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0f, 1f, 0.3).Ease(UIEasing.CubicOut).Named("await-fade").PlayAsync(token))
                {
                    resultText.SetText("Cancelled at step 1");
                    return;
                }

                resultText.SetText("Running: step 2 (scale pop)");
                if (!await target.Animate(UIBuiltInAnimationTargets.Paths.RenderTransformScale, Vector2.One, new Vector2(1.25f), 0.2).Ease("BackOut").AutoReverse().Named("await-scale").PlayAsync(token))
                {
                    resultText.SetText("Cancelled at step 2");
                    return;
                }

                resultText.SetText("Running: step 3 (background)");
                if (!await target.Animate(UIColorAnimationTargets.Paths.Background, Color.OrangeRed, 0.3).Named("await-background").PlayAsync(token))
                {
                    resultText.SetText("Cancelled at step 3");
                    return;
                }

                resultText.SetText("Completed");
            });

            Window.GetElementByName<MGButton>("AwaitCancelButton").AddCommandHandler((btn, e) => _awaitCancellation?.Cancel());
        }

        /// <summary>16. Smooth scrolling: <c>ScrollDemoViewer</c>'s <c>ScrollAnimationDuration</c>/<c>ScrollAnimationEasing</c> (set in XAML)
        /// make its mouse wheel and any keyboard scroll smooth already, nothing wired here; "Top"/"Bottom" call
        /// <see cref="MGScrollViewer.ScrollTo"/> directly, with their own duration and easing, which is independent of the viewer's own
        /// wheel/keyboard settings. Nothing starts at load.</summary>
        private void WireScrolling()
        {
            MGScrollViewer viewer = Window.GetElementByName<MGScrollViewer>("ScrollDemoViewer");

            Window.GetElementByName<MGButton>("ScrollDemoTopButton").AddCommandHandler((btn, e)
                => viewer.ScrollTo(null, 0f, TimeSpan.FromSeconds(0.4), UIEasing.CubicInOut));

            Window.GetElementByName<MGButton>("ScrollDemoBottomButton").AddCommandHandler((btn, e)
                => viewer.ScrollTo(null, viewer.MaxVerticalOffset, TimeSpan.FromSeconds(0.4), UIEasing.CubicInOut));
        }

        /// <summary>The "Sprite-sheet frames" section's current run, restarted by every "Play" click and paused/resumed in place by
        /// <c>FramesPauseToggle</c> (<see cref="UIAnimation.Pause"/>/<see cref="UIAnimation.Resume"/>). Null until the first click.</summary>
        private UIAnimation _framesAnimation;

        /// <summary>17. Sprite-sheet frames: <c>FramesTarget</c>'s background (declared in XAML as a <c>TextureFillBrush</c> with a
        /// <c>FrameGrid</c> over the "AngryMeteor" sheet) animates <see cref="UIExtraAnimationTargets.Paths.BackgroundTextureFrame"/> as a
        /// plain float whose integer part selects the cell; "Play" (re)starts a <see cref="UIAnimation.RepeatForever"/> run from 0 to the
        /// grid's own frame count over ~0.6s, and the toggle pauses/resumes that exact run without restarting it. Nothing starts at load.</summary>
        private void WireFrames()
        {
            MGBorder target = Window.GetElementByName<MGBorder>("FramesTarget");
            MGTextureFillBrush frameBrush = (MGTextureFillBrush)target.BackgroundBrush.NormalValue;
            float frameCount = frameBrush.FrameGrid!.Value.EffectiveFrameCount;
            MGToggleButton pauseToggle = Window.GetElementByName<MGToggleButton>("FramesPauseToggle");

            Window.GetElementByName<MGButton>("FramesPlayButton").AddCommandHandler((btn, e) =>
            {
                _framesAnimation = target.Animate(UIExtraAnimationTargets.Paths.BackgroundTextureFrame, 0f, frameCount, 0.6)
                    .RepeatForever().Named("frames-play").Play();
                pauseToggle.IsChecked = false;
            });

            pauseToggle.OnCheckStateChanged += (sender, e) =>
            {
                if (_framesAnimation == null)
                {
                    return;
                }

                if (pauseToggle.IsChecked)
                {
                    _framesAnimation.Pause();
                }
                else
                {
                    _framesAnimation.Resume();
                }
            };
        }

        /// <summary>The "Layout transitions" section's running counter for freshly inserted rows (Y5): every new row gets an increasing label
        /// so insert and move are visually distinguishable across clicks. Starts after the four rows declared in XAML.</summary>
        private int _layoutRowCounter = 4;

        /// <summary>18. Layout transitions: <c>LayoutListPanel</c>'s rows opt in through <c>LayoutTransitionDuration</c>/
        /// <c>LayoutTransitionEasing</c> (the "LayoutRow" style, in XAML) -- insert, remove and move (a removal followed by an insertion,
        /// <c>MGStackPanel</c> has no reorder API) glide the remaining/displaced rows to their new place, never the row that was itself just
        /// attached. <c>LayoutSizeTarget</c> additionally opts into <c>LayoutTransitionAnimatesSize</c> (declared in XAML): "Resize" toggles
        /// its width between two values, stretching its content during the run. A row created here by "Insert at top" gets the same
        /// <see cref="MGElement.LayoutTransition"/> settings as the ones declared in XAML. Nothing starts at load.</summary>
        private void WireLayout()
        {
            MGStackPanel list = Window.GetElementByName<MGStackPanel>("LayoutListPanel");
            MGBorder sizeTarget = Window.GetElementByName<MGBorder>("LayoutSizeTarget");

            MGBorder CreateRow(string text)
            {
                MGBorder row = new(Window) { LayoutTransition = new UILayoutTransition { Duration = TimeSpan.FromSeconds(0.3), Easing = UIEasing.CubicOut } };
                row.SetContent(new MGTextBlock(Window, text));
                return row;
            }

            Window.GetElementByName<MGButton>("LayoutInsertButton").AddCommandHandler((btn, e) =>
            {
                _layoutRowCounter++;
                list.TryInsertChild(0, CreateRow($"Row {_layoutRowCounter}"));
            });

            Window.GetElementByName<MGButton>("LayoutRemoveButton").AddCommandHandler((btn, e) =>
            {
                MGElement first = list.Children.FirstOrDefault();
                if (first != null)
                {
                    list.TryRemoveChild(first);
                }
            });

            Window.GetElementByName<MGButton>("LayoutMoveButton").AddCommandHandler((btn, e) =>
            {
                MGElement last = list.Children.LastOrDefault();
                if (last != null)
                {
                    list.TryRemoveChild(last);
                    list.TryInsertChild(0, last);
                }
            });

            bool sizeIsWide = false;
            Window.GetElementByName<MGButton>("LayoutResizeButton").AddCommandHandler((btn, e) =>
            {
                sizeIsWide = !sizeIsWide;
                sizeTarget.PreferredWidth = sizeIsWide ? 220 : 100;
            });
        }
    }
}
