using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;

namespace MGUI.Tests.Animation;

/// <summary>Slice S3: conflict rule, ownership (detachment, window closing), bookkeeping and the zero-cost paths of the <see cref="UIAnimationManager"/>.</summary>
public class AnimationManagerTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void SecondAnimationOnTheSamePath_ReplacesTheFirst_FromTheCurrentValue_AndInheritsTheBase()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> first = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        int cancelled = 0;
        first.Cancelled += (_, _) => cancelled++;
        scene.Top.Animations.Start(first);
        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        UIPropertyAnimation<float> second = AnimationTestScene.OpacityAnimation(null, 0f, 160);
        scene.Top.Animations.Start(second);

        Assert.Equal(UIAnimationState.Cancelled, first.State);
        Assert.Equal(1, cancelled);
        Assert.Equal(0.5f, second.StartValue, Tolerance);
        Assert.Equal(1f, second.BaseValue, Tolerance);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Equal(1, scene.Desktop.Animations.ActiveCount);

        scene.Frames(10);
        Assert.Equal(UIAnimationState.Completed, second.State);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);

        // A plain (non store-backed) target holds nothing after completion: Clear has nothing to release and the end value stays (ADR-0006, HoldEnd asymmetry).
        scene.Top.Animations.Clear();
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
        Assert.Empty(scene.Top.Animations.Held);
    }

    [Fact]
    public void AnimationsOnDifferentPaths_Coexist()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIDelegateAnimationTarget<float> other = new("Test.Other", _ => 0f, (_, _) => { });
        UIPropertyAnimation<float> opacity = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        UIPropertyAnimation<float> second = new() { Target = other, To = 1f, Duration = TimeSpan.FromMilliseconds(160) };

        scene.Top.Animations.Start(opacity);
        scene.Top.Animations.Start(second);

        Assert.Equal(UIAnimationState.Running, opacity.State);
        Assert.Equal(UIAnimationState.Running, second.State);
        Assert.Equal(2, scene.Top.Animations.Count);
        Assert.True(scene.Top.Animations.IsAnimating("Test.Opacity"));
        Assert.True(scene.Top.Animations.IsAnimating("test.other"));
        Assert.False(scene.Bottom.Animations.IsAnimating("Test.Opacity"));
    }

    [Fact]
    public void SameAnimationOnTwoElements_RunsOnTheLastOwner()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> shared = AnimationTestScene.OpacityAnimation(0f, 1f, 160);

        scene.Top.Animations.Start(shared);
        scene.Bottom.Animations.Start(shared);

        Assert.Same(scene.Bottom, shared.Owner);
        Assert.Equal(1, scene.Desktop.Animations.ActiveCount);
        Assert.Equal(0, scene.Top.Animations.Count);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);

        // The previous registration is gone: a new animation on Top must not cancel the one now running on Bottom (review finding).
        scene.Top.Animations.Start(AnimationTestScene.OpacityAnimation(0f, 1f, 160));
        Assert.Equal(UIAnimationState.Running, shared.State);
        Assert.Equal(2, scene.Desktop.Animations.ActiveCount);
        scene.Frames(5);
        Assert.Equal(0.5f, scene.Bottom.Opacity, Tolerance);
    }

    [Fact]
    public void ClosingAWindow_AlsoClearsTheAnimationsOfItsNestedWindows()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGWindow nested = new(scene.Window, 50, 50, 200, 100) { WindowStyle = WindowStyle.None };
        MGButton inner = new(nested) { PreferredWidth = 80, PreferredHeight = 30 };
        nested.SetContent(inner);
        scene.Window.AddNestedWindow(nested);
        scene.Frames(2);
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.5f, 160);
        inner.Animations.Start(animation);
        scene.Frames(5);

        Assert.True(scene.Window.TryCloseWindow());

        Assert.Equal(UIAnimationState.Cancelled, animation.State);
        Assert.Equal(1f, inner.Opacity, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Detachment_ClearsTheAnimations_AndRestoresTheBase()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.5f, 160);
        scene.Top.Animations.Start(animation);
        scene.Frames(5);

        Assert.True(scene.Panel.TryRemoveChild(scene.Top));

        Assert.Null(scene.Top.Parent);
        Assert.Equal(UIAnimationState.Cancelled, animation.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void WindowClose_ClearsTheAnimationsOfItsElements()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.5f, 160);
        scene.Top.Animations.Start(animation);
        scene.Frames(5);

        Assert.True(scene.Window.TryCloseWindow());

        Assert.Equal(UIAnimationState.Cancelled, animation.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);

        // A re-shown window starts clean and can animate again.
        scene.Desktop.Windows.Add(scene.Window);
        scene.Frames(2);
        scene.Top.Animations.Start(AnimationTestScene.OpacityAnimation(0f, 1f, 32));
        scene.Frames(2);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void HiddenElement_IsStillAnimated()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Visibility = Visibility.Collapsed;
        scene.Frames(2);
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        scene.Top.Animations.Start(animation);

        scene.Frames(5);

        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void CancelAll_UsesEachCancelBehavior_AndClear_RestoresEverything()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> keep = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        keep.CancelBehavior = UIAnimationCancelBehavior.KeepCurrent;
        scene.Top.Animations.Start(keep);
        scene.Frames(5);

        scene.Top.Animations.CancelAll();
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, scene.Top.Animations.Count);

        scene.Top.Opacity = 1f;
        scene.Top.Animations.Start(AnimationTestScene.OpacityAnimation(0f, 1f, 160));
        scene.Frames(5);
        scene.Top.Animations.Clear();
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void ManagerCancelAll_PauseAll_ResumeAll_AffectEveryAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> top = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        UIPropertyAnimation<float> bottom = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        scene.Top.Animations.Start(top);
        scene.Bottom.Animations.Start(bottom);

        scene.Desktop.Animations.PauseAll();
        scene.Frames(5);
        Assert.Equal(UIAnimationState.Paused, top.State);
        Assert.Equal(0f, scene.Bottom.Opacity, Tolerance);

        scene.Desktop.Animations.ResumeAll();
        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        scene.Desktop.Animations.CancelAll();
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
        Assert.Equal(UIAnimationState.Cancelled, bottom.State);
    }

    [Fact]
    public void UnknownPropertyPath_FailsAtStart_ListingTheKnownPaths()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIAnimationTargets.Register(new UIDelegateAnimationTarget<float>("Test.Registered", e => e.Opacity, (e, v) => e.Opacity = v));
        UIPropertyAnimation<float> animation = new("Test.Nope") { To = 1f, Duration = TimeSpan.FromMilliseconds(16) };

        ArgumentException error = Assert.Throws<ArgumentException>(() => scene.Top.Animations.Start(animation));

        Assert.Contains("Test.Nope", error.Message);
        Assert.Contains("Test.Registered", error.Message);
        Assert.Equal(UIAnimationState.Stopped, animation.State);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void RegisteredPath_IsResolvedCaseInsensitively()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIAnimationTargets.Register(new UIDelegateAnimationTarget<float>("Test.Registered", e => e.Opacity, (e, v) => e.Opacity = v));
        UIPropertyAnimation<float> animation = new("test.registered") { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(160) };

        scene.Top.Animations.Start(animation);
        scene.Frames(5);

        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Throws<InvalidOperationException>(() => UIAnimationTargets.TryGet<int>("Test.Registered", out _));
    }

    [Fact]
    public void CancelFromAnUpdatedHandler_DuringATick_IsSweptSafely()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        animation.Updated += (sender, _) =>
        {
            if (animation.Progress >= 0.3f)
            {
                animation.Cancel();
            }
        };
        scene.Top.Animations.Start(animation);

        scene.Frames(10);

        Assert.Equal(UIAnimationState.Cancelled, animation.State);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void UntouchedElements_CarryNoAnimationSlot()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Frames(3);
        scene.Draw();

        Assert.Null(SlotField(scene.Top));
        Assert.Null(SlotField(scene.Bottom));
        Assert.Null(SlotField(scene.Panel));
        Assert.Null(SlotField(scene.Window));
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void ARunningAnimation_AllocatesNothingPerFrame()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        animation.RepeatForever = true;
        animation.AutoReverse = true;
        scene.Top.Animations.Start(animation);
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
        Assert.Equal(UIAnimationState.Running, animation.State);
    }

    private static object SlotField(MGElement element)
        => typeof(MGElement).GetField("_AnimationSlot", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(element);
}
