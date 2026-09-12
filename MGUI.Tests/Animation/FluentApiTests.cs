using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Animation;

/// <summary>Slice T7 of Docs/Tasks/animation-v2-tasks.md: the fluent Animate API, sugar over UIPropertyAnimation and UISequenceAnimation.</summary>
public class FluentApiTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Animate_Play_StartsAPropertyAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        UIAnimation animation = scene.Top.Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0f, 1f, 0.16).Ease(UIEasing.Linear).Named("fade").Play();

        UIPropertyAnimation<float> fade = Assert.IsType<UIPropertyAnimation<float>>(animation);
        Assert.Equal("fade", fade.Name);
        Assert.Same(UIEasing.Linear, fade.Easing);
        Assert.Equal(TimeSpan.FromMilliseconds(160), fade.Duration);
        Assert.True(fade.HasFrom);
        Assert.Same(scene.Top, fade.Owner);
        Assert.True(scene.Top.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.Opacity));
        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Animate_WithoutFrom_StartsFromTheCurrentValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Opacity = 0.5f;

        UIAnimationBuilder<float> builder = scene.Top.Animate(UIBuiltInAnimationTargets.Paths.Opacity, 1f, TimeSpan.FromMilliseconds(160));
        Assert.False(builder.Animation.HasFrom);
        builder.Play();
        scene.Frames(5);
        Assert.Equal(0.75f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Then_ChainsSteps_IntoASequence()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        UIAnimation animation = scene.Top
            .Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0f, 1f, 0.16).Ease(UIEasing.Linear).Named("intro")
            .Then(UIBuiltInAnimationTargets.Paths.RenderTransformRotation, 0f, 90f, 0.16).Ease(UIEasing.Linear)
            .Play();

        UISequenceAnimation sequence = Assert.IsType<UISequenceAnimation>(animation);
        Assert.Equal(2, sequence.Children.Count);
        Assert.Equal("intro", sequence.Name);
        Assert.Equal(TimeSpan.FromMilliseconds(320), sequence.Duration);

        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0f, scene.Top.RenderTransform.Rotation, Tolerance);
        scene.Frames(10);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(45f, scene.Top.RenderTransform.Rotation, Tolerance);
        scene.Frames(5);
        Assert.Equal(90f, scene.Top.RenderTransform.Rotation, Tolerance);
        Assert.Equal(UIAnimationState.Completed, sequence.State);
    }

    [Fact]
    public void Wait_InsertsADelayStep_AndThen_AcceptsABuiltAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIKeyFrameAnimation<float> pop = new(UIBuiltInAnimationTargets.Paths.RenderTransformRotation)
        {
            Duration = TimeSpan.FromMilliseconds(160),
            Track = { { 0f, 0f }, { 1f, 90f } },
        };

        UIAnimationBuilder chain = scene.Top.Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0f, 1f, 0.16).Wait(0.08).Then(pop);
        UISequenceAnimation sequence = Assert.IsType<UISequenceAnimation>(chain.Build());
        Assert.Equal(3, sequence.Children.Count);
        Assert.IsType<UIDelayAnimation>(sequence.Children[1]);
        Assert.Same(pop, sequence.Children[2]);
        Assert.False(scene.Top.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.Opacity));

        Assert.Same(sequence, chain.Play());
        // A group derives its duration from its children when it starts.
        Assert.Equal(TimeSpan.FromMilliseconds(400), sequence.Duration);
        scene.Frames(15);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0f, scene.Top.RenderTransform.Rotation, Tolerance);
        scene.Frames(5);
        Assert.Equal(45f, scene.Top.RenderTransform.Rotation, Tolerance);
    }

    [Fact]
    public void Options_MapToTheAnimationProperties()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        UIAnimationBuilder<Vector2> builder = scene.Top.Animate(UIBuiltInAnimationTargets.Paths.RenderTransformScale, Vector2.One, new Vector2(1.2f), 0.25)
            .Ease("BackOut")
            .Delay(0.05)
            .Repeat(3)
            .AutoReverse()
            .Fill(UIAnimationFillBehavior.RestoreBaseValue)
            .OnCancel(UIAnimationCancelBehavior.KeepCurrent)
            .Named("pulse")
            .Configure(a => a.InheritsBaseValue = false);

        UIPropertyAnimation<Vector2> animation = builder.Animation;
        Assert.Same(UIEasing.BackOut, animation.Easing);
        Assert.Equal(TimeSpan.FromMilliseconds(50), animation.Delay);
        Assert.Equal(3, animation.RepeatCount);
        Assert.True(animation.AutoReverse);
        Assert.Equal(UIAnimationFillBehavior.RestoreBaseValue, animation.FillBehavior);
        Assert.Equal(UIAnimationCancelBehavior.KeepCurrent, animation.CancelBehavior);
        Assert.Equal("pulse", animation.Name);
        Assert.False(animation.InheritsBaseValue);
        Assert.Same(animation, builder.Build());

        builder.RepeatForever();
        Assert.True(animation.RepeatForever);
    }

    [Fact]
    public void AWrongPathOrType_FailsWhereTheChainIsWritten()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        ArgumentException unknown = Assert.Throws<ArgumentException>(() => scene.Top.Animate("Nope", 0f, 1f, 0.1));
        Assert.Contains("Nope", unknown.Message);
        InvalidOperationException mismatch = Assert.Throws<InvalidOperationException>(() => scene.Top.Animate(UIBuiltInAnimationTargets.Paths.Opacity, Vector2.Zero, Vector2.One, 0.1));
        Assert.Contains("Single", mismatch.Message);
        Assert.Throws<ArgumentException>(() => scene.Top.Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0f, 1f, 0.1).Ease("Wobbly"));
        Assert.Equal(0, scene.Top.Animations.Count);
    }
}
