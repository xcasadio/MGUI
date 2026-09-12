using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;

namespace MGUI.Tests.Animation;

/// <summary>Slice S3: state machine, timing and events of one <see cref="UIAnimation"/> driven by the desktop frames (16 ms).</summary>
public class AnimationLifecycleTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Play_WithoutOwner_Throws()
    {
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(animation.Play);

        Assert.Contains("Animations.Start", error.Message);
        Assert.Equal(UIAnimationState.Stopped, animation.State);
    }

    [Fact]
    public void Start_WritesTheStartValueImmediately_AndRaisesStarted()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0.25f, 1f, 160);
        int started = 0, updated = 0;
        animation.Started += (_, _) => started++;
        animation.Updated += (_, _) => updated++;

        scene.Top.Animations.Start(animation);

        Assert.Equal(UIAnimationState.Running, animation.State);
        Assert.Same(scene.Top, animation.Owner);
        Assert.Equal(0.25f, scene.Top.Opacity, Tolerance);
        Assert.Equal(1, started);
        Assert.Equal(1, updated);
        Assert.Equal(1, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Advances_Linearly_AndCompletes_HoldingTheEndValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        int completed = 0, updated = 0;
        animation.Completed += (_, _) => completed++;
        animation.Updated += (_, _) => updated++;
        scene.Top.Animations.Start(animation);

        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0.5f, animation.Progress, Tolerance);
        Assert.Equal(UIAnimationState.Running, animation.State);

        scene.Frames(5);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(UIAnimationState.Completed, animation.State);
        Assert.Equal(1, completed);
        Assert.Equal(11, updated);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
        Assert.False(animation.IsHeld);

        scene.Frames(3);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Easing_ShapesTheProgress()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        animation.Easing = UIEasing.QuadIn;
        scene.Top.Animations.Start(animation);

        scene.Frames(5);

        Assert.Equal(0.25f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void From_DefaultsToTheCurrentValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Opacity = 0.6f;
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(null, 1f, 160);
        scene.Top.Animations.Start(animation);

        Assert.False(animation.HasFrom);
        Assert.Equal(0.6f, animation.StartValue, Tolerance);
        Assert.Equal(0.6f, animation.BaseValue, Tolerance);
        scene.Frames(5);
        Assert.Equal(0.8f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Delay_PostponesTheFirstWrite()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        animation.Delay = TimeSpan.FromMilliseconds(48);
        int started = 0;
        animation.Started += (_, _) => started++;
        scene.Top.Animations.Start(animation);

        Assert.Equal(UIAnimationState.Delayed, animation.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        scene.Frames(2);
        Assert.Equal(UIAnimationState.Delayed, animation.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, started);

        scene.Frames(1);
        Assert.Equal(UIAnimationState.Running, animation.State);
        Assert.Equal(1, started);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);

        scene.Frames(10);
        Assert.Equal(UIAnimationState.Completed, animation.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void RestoreBaseValue_OnCompletion_PutsTheBaseBack()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.5f, 160);
        animation.FillBehavior = UIAnimationFillBehavior.RestoreBaseValue;
        scene.Top.Animations.Start(animation);

        scene.Frames(10);

        Assert.Equal(UIAnimationState.Completed, animation.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Cancel_RestoresTheBase_ByDefault()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.5f, 160);
        int cancelled = 0, completed = 0;
        animation.Cancelled += (_, _) => cancelled++;
        animation.Completed += (_, _) => completed++;
        scene.Top.Animations.Start(animation);
        scene.Frames(5);

        animation.Cancel();

        Assert.Equal(UIAnimationState.Cancelled, animation.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(1, cancelled);
        Assert.Equal(0, completed);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);

        animation.Cancel();
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public void Cancel_KeepCurrent_LeavesTheAnimatedValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        animation.CancelBehavior = UIAnimationCancelBehavior.KeepCurrent;
        scene.Top.Animations.Start(animation);
        scene.Frames(5);

        animation.Cancel();

        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Pause_FreezesTheValue_AndResume_Continues()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        scene.Top.Animations.Start(animation);
        scene.Frames(2);

        animation.Pause();
        scene.Frames(3);
        Assert.Equal(UIAnimationState.Paused, animation.State);
        Assert.Equal(0.2f, scene.Top.Opacity, Tolerance);
        Assert.Equal(1, scene.Desktop.Animations.ActiveCount);

        animation.Resume();
        scene.Frames(3);
        Assert.Equal(UIAnimationState.Running, animation.State);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        animation.Pause();
        animation.Play();
        Assert.Equal(UIAnimationState.Running, animation.State);
    }

    [Fact]
    public void Repeat_RaisesRepeated_AndCompletesAfterTheLastIteration()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 32);
        animation.RepeatCount = 2;
        int repeated = 0;
        animation.Repeated += (_, _) => repeated++;
        scene.Top.Animations.Start(animation);

        scene.Frames(3);
        Assert.Equal(1, animation.Iteration);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Equal(UIAnimationState.Running, animation.State);

        scene.Frames(3);
        Assert.Equal(UIAnimationState.Completed, animation.State);
        Assert.Equal(2, repeated);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void AutoReverse_GoesBack_AndEndsOnTheStartValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 64);
        animation.AutoReverse = true;
        int reversed = 0;
        animation.Reversed += (_, _) => reversed++;
        scene.Top.Animations.Start(animation);

        scene.Frames(4);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.False(animation.IsReversing);

        scene.Frames(2);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.True(animation.IsReversing);
        Assert.Equal(1, reversed);

        scene.Frames(2);
        Assert.Equal(UIAnimationState.Completed, animation.State);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void RepeatForever_RunsUntilCancelled()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 32);
        animation.RepeatForever = true;
        animation.AutoReverse = true;
        scene.Top.Animations.Start(animation);

        scene.Frames(100);

        Assert.Equal(UIAnimationState.Running, animation.State);
        Assert.Equal(25, animation.Iteration);
        animation.Cancel();
        Assert.Equal(UIAnimationState.Cancelled, animation.State);
    }

    [Fact]
    public void ZeroDuration_CompletesOnTheFirstTick_WithTheEndValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.3f, 0);
        scene.Top.Animations.Start(animation);

        scene.Frames(1);

        Assert.Equal(UIAnimationState.Completed, animation.State);
        Assert.Equal(0.3f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void ClockTimeScale_SlowsEveryAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Desktop.Animations.Clock.TimeScale = 0.5f;
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        scene.Top.Animations.Start(animation);

        scene.Frames(10);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        scene.Frames(10);
        Assert.Equal(UIAnimationState.Completed, animation.State);
    }

    [Fact]
    public void ClockPause_FreezesEveryAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        scene.Top.Animations.Start(animation);
        scene.Frames(2);

        scene.Desktop.Animations.Clock.IsPaused = true;
        scene.Frames(5);
        Assert.Equal(0.2f, scene.Top.Opacity, Tolerance);
        Assert.Equal(UIAnimationState.Running, animation.State);

        scene.Desktop.Animations.Clock.IsPaused = false;
        scene.Frames(8);
        Assert.Equal(UIAnimationState.Completed, animation.State);
    }

    [Fact]
    public void Play_AfterCompletion_StartsOver()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        scene.Top.Animations.Start(animation);
        scene.Frames(10);
        Assert.Equal(UIAnimationState.Completed, animation.State);

        animation.Play();

        Assert.Equal(UIAnimationState.Running, animation.State);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
        Assert.Equal(TimeSpan.Zero, animation.Elapsed);
        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Restart_WhileRunning_StartsOver_WithoutCancelling()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        int cancelled = 0;
        animation.Cancelled += (_, _) => cancelled++;
        scene.Top.Animations.Start(animation);
        scene.Frames(5);

        animation.Restart();

        Assert.Equal(0, cancelled);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
        Assert.Equal(1, scene.Desktop.Animations.ActiveCount);
        scene.Frames(10);
        Assert.Equal(UIAnimationState.Completed, animation.State);
    }

    [Fact]
    public void Settings_RejectNegativeValues()
    {
        UIPropertyAnimation<float> animation = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => animation.Duration = TimeSpan.FromMilliseconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => animation.Delay = TimeSpan.FromMilliseconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => animation.RepeatCount = -1);
    }
}
