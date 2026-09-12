using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Targets;

namespace MGUI.Tests.Animation;

/// <summary>Slice T1 of Docs/Tasks/animation-v2-tasks.md: storyboards (parallel), sequences and delays.</summary>
public class CompositionTests
{
    private const float Tolerance = 1e-4f;

    private static UIPropertyAnimation<float> Fade(float from, float to, int milliseconds, string name = null)
        => new(UIBuiltInAnimationTargets.Paths.Opacity) { From = from, To = to, Duration = TimeSpan.FromMilliseconds(milliseconds), Name = name };

    private static UIPropertyAnimation<float> Spin(float to, int milliseconds, string name = null)
        => new(UIBuiltInAnimationTargets.Paths.RenderTransformRotation) { From = 0f, To = to, Duration = TimeSpan.FromMilliseconds(milliseconds), Name = name };

    [Fact]
    public void Storyboard_StartsEveryChild_AndCompletesWithTheLongest()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = Fade(0f, 1f, 160, "fade");
        UIPropertyAnimation<float> spin = Spin(90f, 320, "spin");
        scene.Bottom.Animations.Start(spin);
        spin.Cancel();
        UIStoryboard storyboard = new() { fade, spin };

        scene.Top.Animations.Start(storyboard);

        Assert.Equal(UIAnimationState.Running, storyboard.State);
        Assert.Equal(TimeSpan.FromMilliseconds(320), storyboard.Duration);
        Assert.Same(scene.Top, fade.Owner);
        Assert.Same(scene.Bottom, spin.Owner);
        Assert.Equal(3, scene.Desktop.Animations.ActiveCount);
        Assert.Equal(2, scene.Top.Animations.Count);
        Assert.Equal(1, scene.Bottom.Animations.Count);

        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Equal(22.5f, scene.Bottom.RenderTransform.Rotation, Tolerance);
        Assert.Equal(0.25f, storyboard.Progress, Tolerance);

        scene.Frames(5);
        Assert.Equal(UIAnimationState.Completed, fade.State);
        Assert.Equal(UIAnimationState.Running, storyboard.State);

        scene.Frames(10);
        Assert.Equal(UIAnimationState.Completed, spin.State);
        Assert.Equal(UIAnimationState.Completed, storyboard.State);
        Assert.Equal(90f, scene.Bottom.RenderTransform.Rotation, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Sequence_PlaysChildrenOneAfterTheOther_WithADelay()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = Fade(1f, 0f, 160);
        UIPropertyAnimation<float> spin = Spin(90f, 160);
        UISequenceAnimation sequence = new UISequenceAnimation().Append(fade).AppendDelay(TimeSpan.FromMilliseconds(32)).Append(spin);
        int completed = 0;
        sequence.Completed += (_, _) => completed++;

        scene.Top.Animations.Start(sequence);

        Assert.Equal(TimeSpan.FromMilliseconds(352), sequence.Duration);
        Assert.Equal(TimeSpan.Zero, sequence.GetStartOffset(0));
        Assert.Equal(TimeSpan.FromMilliseconds(160), sequence.GetStartOffset(1));
        Assert.Equal(TimeSpan.FromMilliseconds(192), sequence.GetStartOffset(2));
        Assert.Equal(UIAnimationState.Running, fade.State);
        Assert.Equal(UIAnimationState.Stopped, spin.State);

        scene.Frames(10);
        Assert.Equal(UIAnimationState.Completed, fade.State);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
        Assert.Equal(UIAnimationState.Stopped, spin.State);

        scene.Frames(2);
        Assert.Equal(UIAnimationState.Running, spin.State);
        Assert.Equal(0f, scene.Top.RenderTransform.Rotation, Tolerance);

        scene.Frames(5);
        Assert.Equal(45f, scene.Top.RenderTransform.Rotation, Tolerance);

        scene.Frames(5);
        Assert.Equal(UIAnimationState.Completed, spin.State);
        Assert.Equal(UIAnimationState.Completed, sequence.State);
        Assert.Equal(1, completed);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void CancellingTheGroup_CancelsTheRunningChildren_EachWithItsOwnBehaviour()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> restore = Fade(0f, 1f, 160);
        UIPropertyAnimation<float> keep = Spin(90f, 160);
        keep.CancelBehavior = UIAnimationCancelBehavior.KeepCurrent;
        UIStoryboard storyboard = new() { restore, keep };
        scene.Top.Animations.Start(storyboard);
        scene.Frames(5);

        storyboard.Cancel();

        Assert.Equal(UIAnimationState.Cancelled, storyboard.State);
        Assert.Equal(UIAnimationState.Cancelled, restore.State);
        Assert.Equal(UIAnimationState.Cancelled, keep.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(45f, scene.Top.RenderTransform.Rotation, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);

        storyboard.CancelBehavior = UIAnimationCancelBehavior.KeepCurrent;
        scene.Top.Animations.Start(storyboard);
        scene.Frames(2);
        storyboard.Cancel();
        Assert.Equal(UIAnimationState.Cancelled, restore.State);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void DetachingTheRoot_CancelsTheGroup_AndItsChildrenOnOtherElements()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> onRoot = Fade(0f, 1f, 160);
        UIPropertyAnimation<float> onBottom = Spin(90f, 160);
        scene.Bottom.Animations.Start(onBottom);
        onBottom.Cancel();
        UIStoryboard storyboard = new() { onRoot, onBottom };
        scene.Top.Animations.Start(storyboard);
        scene.Frames(5);

        Assert.True(scene.Panel.TryRemoveChild(scene.Top));

        Assert.Equal(UIAnimationState.Cancelled, storyboard.State);
        Assert.Equal(UIAnimationState.Cancelled, onRoot.State);
        Assert.Equal(UIAnimationState.Cancelled, onBottom.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0f, scene.Bottom.RenderTransform.Rotation, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void ClosingTheWindow_CancelsTheGroup()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIStoryboard storyboard = new() { Fade(0f, 1f, 160), Spin(90f, 160) };
        scene.Top.Animations.Start(storyboard);
        scene.Frames(5);

        Assert.True(scene.Window.TryCloseWindow());

        Assert.Equal(UIAnimationState.Cancelled, storyboard.State);
        Assert.All(storyboard.Children, child => Assert.Equal(UIAnimationState.Cancelled, child.State));
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void AnExplicitAnimation_ReplacesAChild_AndTheGroupGoesOn()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = Fade(0f, 1f, 160);
        UIPropertyAnimation<float> spin = Spin(90f, 320);
        UIStoryboard storyboard = new() { fade, spin };
        scene.Top.Animations.Start(storyboard);
        scene.Frames(5);

        UIPropertyAnimation<float> explicitFade = Fade(0f, 0f, 32);
        scene.Top.Animations.Start(explicitFade);

        Assert.Equal(UIAnimationState.Cancelled, fade.State);
        Assert.Equal(UIAnimationState.Running, storyboard.State);
        Assert.Equal(UIAnimationState.Running, spin.State);
        scene.Frames(15);
        Assert.Equal(UIAnimationState.Completed, storyboard.State);
        Assert.Equal(UIAnimationState.Completed, spin.State);
    }

    [Fact]
    public void Sequence_Repeats_RestartingTheChildren()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = Fade(0f, 1f, 32);
        UISequenceAnimation sequence = new UISequenceAnimation().Append(fade).AppendDelay(TimeSpan.FromMilliseconds(32));
        sequence.RepeatCount = 1;
        int repeated = 0, fadeStarted = 0;
        sequence.Repeated += (_, _) => repeated++;
        fade.Started += (_, _) => fadeStarted++;

        scene.Top.Animations.Start(sequence);
        scene.Frames(4);
        Assert.Equal(1, repeated);
        Assert.Equal(2, fadeStarted);
        Assert.Equal(UIAnimationState.Running, fade.State);

        scene.Frames(4);
        Assert.Equal(UIAnimationState.Completed, sequence.State);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void SingleChildGroup_RepeatingAtTheChildsOwnLength_RestartsItWithoutDoubleAdvance()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = Fade(0f, 1f, 32);
        UIStoryboard storyboard = new() { fade };
        storyboard.RepeatCount = 2;
        int fadeCompleted = 0, fadeStarted = 0, repeated = 0;
        fade.Completed += (_, _) => fadeCompleted++;
        fade.Started += (_, _) => fadeStarted++;
        storyboard.Repeated += (_, _) => repeated++;

        scene.Top.Animations.Start(storyboard);
        scene.Frames(1);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        scene.Frames(1);
        Assert.Equal(1, repeated);
        Assert.Equal(2, fadeStarted);
        Assert.Equal(UIAnimationState.Running, fade.State);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
        scene.Frames(1);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        scene.Frames(3);
        // A child started inside the group's Begin precedes the group in the manager's list: at every iteration boundary it completes
        // first, then the group's Repeated restarts it, so each iteration ends with the child's own Completed and no double advance.
        Assert.Equal(UIAnimationState.Completed, storyboard.State);
        Assert.Equal(UIAnimationState.Completed, fade.State);
        Assert.Equal(3, fadeStarted);
        Assert.Equal(3, fadeCompleted);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void AutoReverse_AndForeverChildren_AreRefused()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIStoryboard reversing = new() { Fade(0f, 1f, 160) };
        reversing.AutoReverse = true;
        Assert.Throws<ArgumentException>(() => scene.Top.Animations.Start(reversing));
        Assert.Equal(UIAnimationState.Stopped, reversing.State);

        UIPropertyAnimation<float> forever = Fade(0f, 1f, 160);
        forever.RepeatForever = true;
        UIStoryboard withForever = new() { forever };
        Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(withForever));
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void EmptyGroup_CompletesOnItsFirstTick()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIStoryboard empty = new();

        scene.Top.Animations.Start(empty);
        scene.Frames(1);

        Assert.Equal(UIAnimationState.Completed, empty.State);
    }

    [Fact]
    public void Group_AppearsInTheDebugView_WithASyntheticPath()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIStoryboard storyboard = new() { Fade(0f, 1f, 160, "fade") };
        storyboard.Name = "open";
        scene.Top.Animations.Start(storyboard);

        UIElementDebugView view = UIToolingService.CaptureElementDebugView(scene.Top);

        Assert.Contains(view.Animations, x => x.Kind == "animation" && x.Path.StartsWith("UIStoryboard#", StringComparison.Ordinal) && x.Name == "open");
        Assert.Contains(view.Animations, x => x.Kind == "animation" && x.Path == UIBuiltInAnimationTargets.Paths.Opacity && x.Name == "fade");
    }

    [Fact]
    public void ARunningGroup_AllocatesNothingPerTick()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIStoryboard storyboard = new() { Fade(0f, 1f, 100000), Spin(90f, 100000) };
        scene.Top.Animations.Start(storyboard);
        UIAnimationManager manager = scene.Desktop.Animations;
        TimeSpan frame = TimeSpan.FromMilliseconds(16);
        for (int i = 0; i < 20; i++)
        {
            manager.Update(frame);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            manager.Update(frame);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(UIAnimationState.Running, storyboard.State);
    }
}
