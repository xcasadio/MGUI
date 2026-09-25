using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Animation;

/// <summary>Transitions on property changes and on the visual state (state-driven scale).</summary>
public class TransitionTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Opacity_LocalWrite_IsInterpolated_FromThePreviousValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UITransition<float> transition = new(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160));
        scene.Top.Transitions.Add(transition);
        Assert.Equal(1f, transition.SettledValue, Tolerance);

        scene.Top.Opacity = 0.3f;

        Assert.True(transition.IsRunning);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(0.65f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(0.3f, scene.Top.Opacity, Tolerance);
        Assert.False(transition.IsRunning);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
        Assert.Equal(0.3f, transition.SettledValue, Tolerance);
    }

    [Fact]
    public void Easing_AndDelay_AreHonoured()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Transitions.Add(new UITransition<float>(UIBuiltInAnimationTargets.Paths.Opacity)
        {
            Duration = TimeSpan.FromMilliseconds(160),
            Delay = TimeSpan.FromMilliseconds(32),
            Easing = UIEasing.QuadIn,
        });

        scene.Top.Opacity = 0f;

        scene.Frames(2);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(1f - UIEasing.QuadIn.Ease(0.5f), scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void LocalWriteDuringTheTransition_RetargetsFromTheCurrentValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Transitions.Add(new UITransition<float>(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160)));
        scene.Top.Opacity = 0f;
        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        scene.Top.Opacity = 1f;

        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(0.75f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void RenderScale_HoverInThenOut_NeverSnaps()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Mouse = new Point(390, 290);
        scene.Frames(2);
        scene.Top.RenderScale = new ConditionalScaleTransform(0.96f, 1.05f);
        UITransition<float> transition = new(UIBuiltInAnimationTargets.Paths.RenderScale, TimeSpan.FromMilliseconds(96));
        scene.Top.Transitions.Add(transition);
        Assert.Equal(1f, transition.SettledValue, Tolerance);

        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(1);
        Assert.True(scene.Top.VisualState.IsHovered);
        Assert.True(transition.IsRunning);
        scene.Frames(3);
        Assert.True(scene.Top.TryGetEffectiveStateScale(out float halfway));
        Assert.Equal(1.025f, halfway, Tolerance);

        scene.Mouse = new Point(390, 290);
        scene.Frames(1);
        Assert.False(scene.Top.VisualState.IsHovered);
        Assert.True(scene.Top.TryGetEffectiveStateScale(out float afterLeave));
        // The manager ticks at the head of the frame, before the visual state changes: the hover-in run advances one last step (1.025 -> 1.033)
        // and the hover-out run starts exactly there, never from 1.05.
        Assert.InRange(afterLeave, 1.025f, 1.05f - Tolerance);
        Assert.Equal(afterLeave, transition.Animation.StartValue, Tolerance);
        Assert.Equal(1f, transition.Animation.To, Tolerance);
        scene.Frames(1);
        Assert.True(scene.Top.TryGetEffectiveStateScale(out float goingBack));
        Assert.True(goingBack < afterLeave, $"scale {goingBack} should decrease from {afterLeave}");

        scene.Frames(8);
        Assert.False(transition.IsRunning);
        Assert.False(scene.Top.TryGetEffectiveStateScale(out _));
        Assert.Empty(scene.Draw().TransformPushes);
    }

    [Fact]
    public void Background_LocalWrite_IsInterpolated_ThenRestoresToTheLocalValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        scene.Top.Transitions.Add(new UITransition<Color>(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(160), UIEasing.Linear));

        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Blue);

        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
        scene.Frames(5);
        Assert.Equal(Color.Lerp(Color.Gray, Color.Blue, 0.5f), Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Top, "Background", out UIValueResolutionSource during));
        Assert.Equal(UIValueSourceKind.Animation, during.Kind);

        scene.Frames(6);
        Assert.Equal(Color.Blue, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Top, "Background", out UIValueResolutionSource after));
        Assert.Equal(UIValueSourceKind.LocalValue, after.Kind);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Margin_LocalWrite_IsInterpolated_ThroughTheStore()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Margin = new Thickness(0);
        scene.Top.Transitions.Add(new UITransition<Thickness>(UIBuiltInAnimationTargets.Paths.Margin, TimeSpan.FromMilliseconds(160)));

        scene.Top.Margin = new Thickness(20);

        Assert.Equal(0, scene.Top.Margin.Left);
        scene.Frames(5);
        Assert.Equal(10, scene.Top.Margin.Left);
        scene.Frames(6);
        Assert.Equal(20, scene.Top.Margin.Left);
    }

    [Fact]
    public void ExplicitAnimation_ReplacesTheTransition_AndSilencesIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UITransition<float> transition = new(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160));
        scene.Top.Transitions.Add(transition);
        scene.Top.Opacity = 0f;
        scene.Frames(5);
        UIPropertyAnimation<float> transitionRun = transition.Animation;

        UIPropertyAnimation<float> explicitFade = new(UIBuiltInAnimationTargets.Paths.Opacity) { To = 1f, Duration = TimeSpan.FromMilliseconds(160) };
        scene.Top.Animations.Start(explicitFade);

        Assert.Equal(UIAnimationState.Cancelled, transitionRun.State);
        Assert.Equal(0.5f, explicitFade.StartValue, Tolerance);
        Assert.False(transition.IsRunning);
        scene.Frames(5);
        Assert.Equal(0.75f, scene.Top.Opacity, Tolerance);
        Assert.False(transition.IsRunning);
        Assert.Equal(1, scene.Desktop.Animations.ActiveCount);

        scene.Frames(6);
        Assert.Equal(UIAnimationState.Completed, explicitFade.State);
        Assert.Equal(1f, transition.SettledValue, Tolerance);

        scene.Top.Opacity = 0.5f;
        Assert.True(transition.IsRunning);
        Assert.Equal(1f, transition.Animation.StartValue, Tolerance);
    }

    [Fact]
    public void Remove_KeepsTheCurrentValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UITransition<float> transition = new(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160));
        scene.Top.Transitions.Add(transition);
        scene.Top.Opacity = 0f;
        scene.Frames(5);

        Assert.True(scene.Top.Transitions.Remove(transition));

        Assert.Null(transition.Owner);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        scene.Top.Opacity = 1f;
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, scene.Top.Transitions.Count);
    }

    [Fact]
    public void AddingASecondTransitionForThePath_ReplacesTheFirst()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UITransition<float> first = new(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160));
        UITransition<float> second = new(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(32));
        scene.Top.Transitions.Add(first);

        scene.Top.Transitions.Add(second);

        Assert.Equal(1, scene.Top.Transitions.Count);
        Assert.Null(first.Owner);
        Assert.Same(second, scene.Top.Transitions["opacity"]);
        scene.Top.Opacity = 0f;
        scene.Frames(3);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Create_BuildsTheTypedTransition_ForARegisteredPath()
    {
        UITransition transition = UITransition.Create(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(10), UIEasing.CubicOut);

        Assert.IsType<UITransition<Color>>(transition);
        Assert.Equal(typeof(Color), transition.ValueType);
        Assert.Equal(TimeSpan.FromMilliseconds(150), transition.Duration);
        Assert.Same(UIEasing.CubicOut, transition.Easing);
        Assert.Throws<ArgumentException>(() => UITransition.Create("Nope", TimeSpan.Zero));
    }

    [Fact]
    public void UnobservableTarget_IsRejected()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIAnimationTargets.Register(new UIDelegateAnimationTarget<float>("Test.Plain", e => e.Opacity, (e, v) => e.Opacity = v));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => scene.Top.Transitions.Add(new UITransition<float>("Test.Plain", TimeSpan.FromMilliseconds(16))));

        Assert.Contains("not observable", error.Message);
        Assert.Equal(0, scene.Top.Transitions.Count);
    }

    [Fact]
    public void Background_LocalWriteDuringTheTransition_Retargets_WithoutJumping()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Black);
        UITransition<Color> transition = new(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(160));
        scene.Top.Transitions.Add(transition);

        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.White);
        scene.Frames(5);
        Color midRun = Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color;
        Assert.Equal(Color.Lerp(Color.Black, Color.White, 0.5f), midRun);

        // A non-tagged container write (the store still enforces precedence physically -- the field itself does not
        // move until the run's own next tick sees the new LocalValue winner below it) targets red mid-run: retargeted at
        // once (SettledValue already reports red), no jump (the drawn colour is unaffected by the write itself).
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Red);
        Assert.Equal(Color.Red, transition.SettledValue);
        Assert.Equal(midRun, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);

        Color previous = midRun;
        for (int i = 0; i < 9; i++)
        {
            scene.Frames(1);
            Color current = Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color;
            Assert.True(ColorDistance(current, Color.Red) < ColorDistance(previous, Color.Red), $"{current} should be closer to red than {previous}");
            Assert.NotEqual(Color.Black, current);
            Assert.NotEqual(Color.White, current);
            previous = current;
        }

        scene.Frames(5);
        Assert.Equal(Color.Red, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
        Assert.False(transition.IsRunning);
    }

    [Fact]
    public void NamedStateBackground_ExitedMidRun_RetargetsToTheBaseColour_SeenWithinTheNextTicks()
    {
        // Same scenario as VisualStatesTests.LeavingAStateMidTransitionOnAThemeBackground_ComesBackToTheThemeColour,
        // asserted here on the transition's own SettledValue/IsRunning rather than the drawn background.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.SetBackground(new MGUI.Core.UI.VisualStateFillBrush(new MGSolidFillBrush(Color.Black)), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        toggle.VisualStates.Add(new MGUI.Core.UI.Animation.States.UIVisualState(MGUI.Core.UI.Animation.States.UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.White } });
        UITransition<Color> transition = new(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(160));
        toggle.Transitions.Add(transition);
        scene.Frames(2);

        toggle.IsChecked = true;
        scene.Frames(6);
        Assert.Equal(Color.White, transition.Animation.To);

        // Exiting mid-run: the base (black) is recorded on the exit frame and picked up by the run's own next tick
        // (ADR-0006 decision 7: animations tick at the head of the frame, one tick before the exit is processed) --
        // within the following couple of ticks the run has already retargeted, well before it would otherwise finish.
        toggle.IsChecked = false;
        scene.Frames(2);
        Assert.Equal(Color.Black, transition.SettledValue);
        Assert.Equal(Color.Black, transition.Animation.To);
        Assert.True(transition.IsRunning);

        scene.Frames(20);
        Assert.False(transition.IsRunning);
        Assert.Equal(Color.Black, transition.SettledValue);
    }

    [Fact]
    public void Margin_LocalWriteDuringTheTransition_Retargets_WithoutJumping()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Margin = new Thickness(0);
        UITransition<Thickness> transition = new(UIBuiltInAnimationTargets.Paths.Margin, TimeSpan.FromMilliseconds(160));
        scene.Top.Transitions.Add(transition);

        scene.Top.Margin = new Thickness(100);
        scene.Frames(5);
        int midRun = scene.Top.Margin.Left;
        Assert.Equal(50, midRun);

        // A tagged pilot write: the store gates the physical field by precedence (Animation still wins), so nothing
        // moves and no notification fires from this write itself -- the run's own next tick discovers the new
        // LocalValue winner below it and retargets there.
        scene.Top.Margin = new Thickness(20);
        Assert.Equal(midRun, scene.Top.Margin.Left);
        Assert.Equal(100, transition.SettledValue.Left); // not retargeted yet: no notification has fired since the write

        // This tick is the OLD run's own last step before it notices the retarget (it still advances one increment
        // toward 100 first -- animations tick at the head of the frame, ADR-0006 decision 7), so the drawn value at
        // this exact frame is the freshly retargeted run's own "from" (not yet moved toward 20); it starts decreasing
        // on the very next frame.
        scene.Frames(1);
        Assert.Equal(20, transition.SettledValue.Left);
        Assert.True(transition.IsRunning);
        int retargetFrom = scene.Top.Margin.Left;
        Assert.Equal(transition.Animation.StartValue.Left, retargetFrom);
        Assert.NotEqual(0, retargetFrom);
        Assert.NotEqual(100, retargetFrom);

        scene.Frames(1);
        int afterRetarget = scene.Top.Margin.Left;
        Assert.True(afterRetarget < retargetFrom, $"{afterRetarget} should be heading toward 20 from {retargetFrom}");

        scene.Frames(10);
        Assert.Equal(20, scene.Top.Margin.Left);
        Assert.False(transition.IsRunning);
    }

    [Fact]
    public void ExplicitAnimation_StartedWhileATransitionRuns_StartsFromTheAnimatedValue()
    {
        // Non-regression (ADR-0006): UIPropertyAnimation.ReadCurrentValue is untouched by a transition -- an explicit animation
        // still starts from the physical (animated) value, never from the value below the transition's run.
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Black);
        UITransition<Color> transition = new(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(160));
        scene.Top.Transitions.Add(transition);
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.White);
        scene.Frames(5);
        Color animatedValue = Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color;
        Assert.Equal(Color.Lerp(Color.Black, Color.White, 0.5f), animatedValue);

        UIPropertyAnimation<Color> explicitAnimation = new(UIColorAnimationTargets.Paths.Background) { To = Color.Blue, Duration = TimeSpan.FromMilliseconds(160) };
        scene.Top.Animations.Start(explicitAnimation);

        Assert.Equal(animatedValue, explicitAnimation.StartValue);
        Assert.False(transition.IsRunning);
    }

    [Fact]
    public void RunningTransition_NeverRetargets_FromItsOwnPerTickWrites()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Margin = new Thickness(0);
        UITransition<Thickness> transition = new(UIBuiltInAnimationTargets.Paths.Margin, TimeSpan.FromMilliseconds(320));
        scene.Top.Transitions.Add(transition);

        scene.Top.Margin = new Thickness(100);
        scene.Frames(1);
        UIPropertyAnimation<Thickness> run = transition.Animation;
        Assert.NotNull(run);

        float? previousProgress = null;
        for (int i = 0; i < 9; i++)
        {
            scene.Frames(1);
            Assert.Same(run, transition.Animation); // no new run was started by the transition's own writes
            Assert.True(transition.IsRunning);
            Assert.Single(scene.Desktop.Animations.ActiveAnimations, a => a.Name == "transition:" + UIBuiltInAnimationTargets.Paths.Margin);
            Assert.Equal(1, scene.Desktop.Animations.ActiveCount);
            var progress = transition.RunningProgress;
            if (previousProgress.HasValue && progress.HasValue)
            {
                Assert.True(progress.Value > previousProgress.Value, $"progress should increase monotonically ({progress} after {previousProgress})");
            }
            previousProgress = progress;
        }
    }

    [Fact]
    public void RunningPilotTransition_AllocatesNothingPerTick_AfterWarmUp()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Margin = new Thickness(0);
        scene.Top.Transitions.Add(new UITransition<Thickness>(UIBuiltInAnimationTargets.Paths.Margin, TimeSpan.FromMilliseconds(100000)));
        scene.Top.Margin = new Thickness(100);
        TimeSpan frame = TimeSpan.FromMilliseconds(FrameMilliseconds);
        UIAnimationManager manager = scene.Desktop.Animations;
        for (int i = 0; i < 5; i++)
        {
            manager.Update(frame);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 200; i++)
        {
            manager.Update(frame);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    private const int FrameMilliseconds = AnimationTestScene.FrameMilliseconds;

    private static float ColorDistance(Color a, Color b)
        => Vector3.Distance(a.ToVector3(), b.ToVector3());

    [Fact]
    public void Detachment_AfterARetarget_LeavesNoOrphanRun()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UITransition<float> transition = new(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160));
        scene.Top.Transitions.Add(transition);
        scene.Top.Opacity = 0f;
        scene.Frames(5);
        scene.Top.Opacity = 1f;
        scene.Frames(2);
        float beforeDetach = scene.Top.Opacity;

        Assert.True(scene.Panel.TryRemoveChild(scene.Top));

        Assert.False(transition.IsRunning);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
        Assert.Empty(scene.Desktop.Animations.ActiveAnimations);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);

        // A change on the detached element is followed silently, never animated.
        scene.Top.Opacity = 0.25f;
        Assert.False(transition.IsRunning);
        Assert.Equal(0.25f, transition.SettledValue, Tolerance);
        Assert.True(beforeDetach > 0.5f && beforeDetach < 1f);
    }

    [Fact]
    public void Detachment_CancelsARunningTransition_AndTheElementStaysClean()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UITransition<float> transition = new(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160));
        scene.Top.Transitions.Add(transition);
        scene.Top.Opacity = 0f;
        scene.Frames(5);

        Assert.True(scene.Panel.TryRemoveChild(scene.Top));

        Assert.False(transition.IsRunning);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }
}
