using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MonoGame.Extended;

namespace MGUI.Tests.Animation;

/// <summary>Slice T6 of Docs/Tasks/animation-v2-tasks.md: MGProgressButton.Duration runs on the animation engine.</summary>
public class ProgressButtonAnimationTests
{
    private const float Tolerance = 1e-3f;

    private static (AnimationTestScene Scene, MGProgressButton Button) Build(int durationMilliseconds = 800)
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGProgressButton button = new(scene.Window, new Thickness(1), MGUniformBorderBrush.Black) { PreferredWidth = 120, PreferredHeight = 30 };
        scene.Panel.TryAddChild(button);
        scene.Mouse = new(390, 290);
        scene.Frames(2);
        Assert.True(button.IsPaused);
        Assert.Equal(0f, button.Value);
        button.Duration = TimeSpan.FromMilliseconds(durationMilliseconds);
        return (scene, button);
    }

    [Fact]
    public void Duration_DrivesTheValue_OnTheEngine()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build();
        Assert.False(button.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.ProgressButtonValue));

        button.IsPaused = false;
        Assert.True(button.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.ProgressButtonValue));
        Assert.Equal(TimeSpan.FromMilliseconds(800), button.RemainingDuration);
        UIElementDebugView view = UIToolingService.CaptureElementDebugView(button);
        Assert.Contains(view.Animations, x => x.Path == UIBuiltInAnimationTargets.Paths.ProgressButtonValue && x.Name == MGProgressButton.DurationAnimationName);

        // 800 ms = 50 frames of 16 ms: the run is created when IsPaused flips, then advances on each tick.
        scene.Frames(25);
        Assert.Equal(50f, button.Value, Tolerance);
        Assert.Equal(TimeSpan.FromMilliseconds(400), button.RemainingDuration);

        int completed = 0;
        button.OnCompleted += (_, _) => completed++;
        scene.Frames(25);
        Assert.Equal(100f, button.Value, Tolerance);
        Assert.True(button.IsCompleted);
        Assert.Equal(1, completed);
        // ActionOnCompleted defaults to Pause: the completion pauses the button, and nothing runs any more.
        Assert.True(button.IsPaused);
        scene.Frames(2);
        Assert.False(button.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.ProgressButtonValue));
        Assert.Equal(100f, button.Value, Tolerance);
    }

    [Fact]
    public void Pause_HoldsTheValue_AndResume_ContinuesFromIt()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build();
        button.IsPaused = false;
        scene.Frames(25);
        Assert.Equal(50f, button.Value, Tolerance);

        button.IsPaused = true;
        Assert.False(button.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.ProgressButtonValue));
        scene.Frames(10);
        Assert.Equal(50f, button.Value, Tolerance);

        button.IsPaused = false;
        scene.Frames(10);
        Assert.Equal(70f, button.Value, Tolerance);
        scene.Frames(15);
        Assert.Equal(100f, button.Value, Tolerance);
    }

    [Fact]
    public void ChangingTheDuration_MidRun_RetargetsTheRemainingShare()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build();
        button.IsPaused = false;
        scene.Frames(25);
        Assert.Equal(50f, button.Value, Tolerance);

        // Half remains: over 1600 ms that is 800 ms, so 25 frames later the value is at 75.
        button.Duration = TimeSpan.FromMilliseconds(1600);
        Assert.Equal(TimeSpan.FromMilliseconds(800), button.RemainingDuration);
        scene.Frames(25);
        Assert.Equal(75f, button.Value, Tolerance);
        scene.Frames(25);
        Assert.Equal(100f, button.Value, Tolerance);
    }

    [Fact]
    public void AValueWrittenByTheApplication_MidRun_RetargetsFromIt()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build();
        button.IsPaused = false;
        scene.Frames(25);
        Assert.Equal(50f, button.Value, Tolerance);

        button.Value = 80f;
        Assert.Equal(80f, button.Value, Tolerance);
        Assert.Equal(TimeSpan.FromMilliseconds(160), button.RemainingDuration);
        scene.Frames(5);
        Assert.Equal(90f, button.Value, Tolerance);
        scene.Frames(5);
        Assert.Equal(100f, button.Value, Tolerance);

        // Reset while completed and paused: the value goes back, nothing runs until the button resumes.
        button.ResetProgress();
        Assert.Equal(0f, button.Value, Tolerance);
        scene.Frames(5);
        Assert.Equal(0f, button.Value, Tolerance);
        button.Resume();
        scene.Frames(25);
        Assert.Equal(50f, button.Value, Tolerance);
    }

    [Fact]
    public void ClearingTheDuration_CancelsTheRun_AndKeepsTheValue()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build();
        button.IsPaused = false;
        scene.Frames(25);
        Assert.Equal(50f, button.Value, Tolerance);

        button.Duration = null;
        Assert.False(button.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.ProgressButtonValue));
        Assert.Null(button.RemainingDuration);
        scene.Frames(10);
        Assert.Equal(50f, button.Value, Tolerance);
        Assert.False(button.IsPaused);

        button.Duration = TimeSpan.FromMilliseconds(160);
        scene.Frames(10);
        Assert.Equal(100f, button.Value, Tolerance);
    }

    [Fact]
    public void ResetAndResume_OnCompletion_RestartsFromMinimum_OnTheNextFrame()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build(160);
        button.ActionOnCompleted = ProgressButtonActionType.ResetAndResume;
        int completed = 0;
        button.OnCompleted += (_, _) => completed++;

        button.IsPaused = false;
        scene.Frames(10);
        // The run reached 100 inside its own write: the completion action reset the value there, and the element's update of the same frame
        // (after the manager's tick) started the next run, which advances from the next frame on.
        Assert.Equal(1, completed);
        Assert.Equal(0f, button.Value, Tolerance);
        Assert.False(button.IsPaused);
        Assert.True(button.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.ProgressButtonValue));

        scene.Frames(5);
        Assert.Equal(50f, button.Value, Tolerance);
        scene.Frames(5);
        Assert.Equal(2, completed);
        Assert.Equal(0f, button.Value, Tolerance);
    }

    [Fact]
    public void ARangeChange_MidRun_Retargets()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build();
        button.IsPaused = false;
        scene.Frames(25);
        Assert.Equal(50f, button.Value, Tolerance);

        // 50 of [0, 200] over 800 ms: 150 remain, 600 ms, 37.5 frames; 25 frames later the value is at 150.
        button.SetRange(0, 200);
        Assert.Equal(TimeSpan.FromMilliseconds(600), button.RemainingDuration);
        scene.Frames(25);
        Assert.Equal(150f, button.Value, Tolerance);
    }

    [Fact]
    public void DetachingTheButton_KeepsTheValue_AndReattaching_Resumes()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build();
        button.IsPaused = false;
        scene.Frames(25);
        Assert.Equal(50f, button.Value, Tolerance);

        // Leaving the tree cancels the owner's animations with a forced restore: the Duration run keeps the value instead of rewinding it.
        Assert.True(scene.Panel.TryRemoveChild(button));
        Assert.Equal(50f, button.Value, Tolerance);
        Assert.False(button.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.ProgressButtonValue));
        Assert.False(button.IsPaused);
        scene.Frames(5);
        Assert.Equal(50f, button.Value, Tolerance);

        Assert.True(scene.Panel.TryAddChild(button));
        Assert.True(button.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.ProgressButtonValue));
        scene.Frames(25);
        Assert.Equal(100f, button.Value, Tolerance);
    }

    [Fact]
    public void AValueBelowMinimum_IsNotClampedByTheRun_ItJustTakesLonger()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build();
        button.IsPaused = false;
        scene.Frames(25);

        button.Value = -50f;
        Assert.Equal(-50f, button.Value, Tolerance);
        // 150 to cover over 1.5 x 800 ms: 400 ms later the value is at 0.
        scene.Frames(25);
        Assert.Equal(0f, button.Value, Tolerance);

        button.Minimum = 60f;
        Assert.Equal(0f, button.Value, Tolerance);
    }

    [Fact]
    public void AZeroDuration_CompletesAtTheFirstTick()
    {
        (AnimationTestScene scene, MGProgressButton button) = Build(0);
        button.IsPaused = false;
        scene.Frames(1);
        Assert.Equal(100f, button.Value, Tolerance);
        Assert.True(button.IsCompleted);
    }

    [Fact]
    public void ATransitionOnThePath_IsRefused()
    {
        (AnimationTestScene _, MGProgressButton button) = Build();
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => button.Transitions.Add(new UITransition<float>(UIBuiltInAnimationTargets.Paths.ProgressButtonValue, TimeSpan.FromMilliseconds(100))));
        Assert.Contains("not observable", error.Message);
    }

    [Fact]
    public void TheTarget_IsRegistered_AndRefusesOtherElements()
    {
        Assert.Equal(typeof(float), UIAnimationTargets.GetValueType(UIBuiltInAnimationTargets.Paths.ProgressButtonValue));

        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = new(UIBuiltInAnimationTargets.Paths.ProgressButtonValue) { To = 1f, Duration = TimeSpan.FromMilliseconds(100) };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(animation));
        Assert.Contains(nameof(MGProgressButton), error.Message);
    }
}
