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

/// <summary>Slice S6: transitions on property changes and on the visual state (state-driven scale).</summary>
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
