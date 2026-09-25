using System.Threading;
using System.Threading.Tasks;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Targets;

namespace MGUI.Tests.Animation;

/// <summary>Slice Y1 (ADR-0011, decision 2): <see cref="UIAnimationCollection.StartAsync"/>, <see cref="UIAnimationBuilder.PlayAsync"/> and
/// <see cref="UIAnimationAsyncExtensions.PlayAsync"/>. Every test reads <see cref="Task{TResult}.IsCompleted"/> / <c>Result</c> synchronously
/// right after driving <see cref="AnimationTestScene"/> frame by frame: nothing here blocks the update thread on the task, matching how the
/// engine itself resolves it (inline, from the event that ends the run).</summary>
public class AnimationAsyncTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void NormalEnd_ResolvesTrue_AndNotBeforeTheEnd()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 48); // 3 frames of 16 ms
        Task<bool> task = scene.Top.Animations.StartAsync(animation);

        scene.Frames(2);
        Assert.False(task.IsCompleted);
        Assert.Equal(UIAnimationState.Running, animation.State);

        scene.Frames(1);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(task.Result);
        Assert.Equal(UIAnimationState.Completed, animation.State);
    }

    [Fact]
    public void ReplacementOnTheSamePath_ResolvesFalse_WithoutException()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> first = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        Task<bool> task = scene.Top.Animations.StartAsync(first);
        scene.Frames(2);

        scene.Top.Animations.Start(AnimationTestScene.OpacityAnimation(null, 0f, 160));

        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
        Assert.Equal(UIAnimationState.Cancelled, first.State);
    }

    [Fact]
    public void ClearingTheElement_ResolvesFalse_WithoutException()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.5f, 160);
        Task<bool> task = scene.Top.Animations.StartAsync(animation);
        scene.Frames(2);

        scene.Top.Animations.Clear();

        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
    }

    [Fact]
    public void DetachingTheElement_ResolvesFalse_WithoutException()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.5f, 160);
        Task<bool> task = scene.Top.Animations.StartAsync(animation);
        scene.Frames(2);

        Assert.True(scene.Panel.TryRemoveChild(scene.Top));

        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
    }

    [Fact]
    public void ClosingTheWindow_ResolvesFalse_WithoutException()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.5f, 160);
        Task<bool> task = scene.Top.Animations.StartAsync(animation);
        scene.Frames(2);

        Assert.True(scene.Window.TryCloseWindow());

        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
    }

    [Fact]
    public void ExplicitCancel_ResolvesFalse_WithoutException()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 0.5f, 160);
        Task<bool> task = scene.Top.Animations.StartAsync(animation);
        scene.Frames(2);

        animation.Cancel();

        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
    }

    [Fact]
    public void AlreadyCancelledToken_ResolvesFalseAtOnce_NothingStarted()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        using CancellationTokenSource cts = new();
        cts.Cancel();
        int activeBefore = scene.Desktop.Animations.ActiveCount;
        float opacityBefore = scene.Top.Opacity;

        Task<bool> task = scene.Top.Animations.StartAsync(AnimationTestScene.OpacityAnimation(0f, 1f, 160), cts.Token);

        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
        Assert.Equal(activeBefore, scene.Desktop.Animations.ActiveCount);
        Assert.Equal(opacityBefore, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void TokenCancelled_FromAnotherThread_DuringATick_DoesNotAlterTheEngineThatFrame_AndResolvesFalseAfterTheNextFrame()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        using CancellationTokenSource cts = new();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        Task<bool> task = scene.Top.Animations.StartAsync(animation, cts.Token);

        bool cancelledOnce = false;
        animation.Updated += (_, _) =>
        {
            // Fires only from a real tick (Begin's own Started/Updated already ran before this subscription): cancel from another
            // thread, synchronously, right in the middle of this tick's Advance.
            if (!cancelledOnce)
            {
                cancelledOnce = true;
                Task.Run(() => cts.Cancel()).Wait();
            }
        };

        int activeBefore = scene.Desktop.Animations.ActiveCount;
        scene.Frames(1);

        Assert.True(cancelledOnce);
        Assert.Equal(activeBefore, scene.Desktop.Animations.ActiveCount);
        Assert.Equal(UIAnimationState.Running, animation.State);
        Assert.False(task.IsCompleted);

        scene.Frames(1);
        Assert.Equal(UIAnimationState.Cancelled, animation.State);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
    }

    [Fact]
    public void TokenCancelled_BetweenFrames_ResolvesFalse_AfterTheNextFrame()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        using CancellationTokenSource cts = new();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        Task<bool> task = scene.Top.Animations.StartAsync(animation, cts.Token);
        scene.Frames(1);

        cts.Cancel();
        Assert.Equal(UIAnimationState.Running, animation.State);
        Assert.False(task.IsCompleted);

        scene.Frames(1);
        Assert.Equal(UIAnimationState.Cancelled, animation.State);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
    }

    [Fact]
    public void TokenCancelled_WhileTheClockIsPaused_ResolvesFalse_AfterTheNextFrame_ClockStaysPaused()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        using CancellationTokenSource cts = new();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        Task<bool> task = scene.Top.Animations.StartAsync(animation, cts.Token);

        scene.Desktop.Animations.Clock.IsPaused = true;
        cts.Cancel();
        scene.Frames(1);

        Assert.Equal(UIAnimationState.Cancelled, animation.State);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
        Assert.True(scene.Desktop.Animations.Clock.IsPaused);
    }

    [Fact]
    public void LateTokenCancellation_AfterCompletion_DoesNotCancelTheLaterStartAsyncRun()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 32); // 2 frames
        using CancellationTokenSource firstToken = new();
        Task<bool> firstRun = scene.Top.Animations.StartAsync(animation, firstToken.Token);
        scene.Frames(2);
        Assert.True(firstRun.IsCompletedSuccessfully);
        Assert.True(firstRun.Result);
        Assert.Equal(UIAnimationState.Completed, animation.State);

        using CancellationTokenSource secondToken = new();
        Task<bool> secondRun = scene.Top.Animations.StartAsync(animation, secondToken.Token);

        // A late cancellation of the token that watched the FIRST (already resolved) run: its registration was released the moment that
        // run resolved, so this must never touch the second run.
        firstToken.Cancel();
        scene.Frames(1);
        Assert.Equal(UIAnimationState.Running, animation.State);
        Assert.False(secondRun.IsCompleted);

        scene.Frames(1);
        Assert.True(secondRun.IsCompletedSuccessfully);
        Assert.True(secondRun.Result);
    }

    [Fact]
    public void Restart_DuringTheWait_ResolvesAtTheRestartedEnd_NotTheOriginalEnd()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 64); // 4 frames
        Task<bool> task = scene.Top.Animations.StartAsync(animation);
        scene.Frames(2); // halfway

        animation.Restart(); // same instance, same (owner, path): no Cancelled, the task keeps following this instance

        scene.Frames(2); // as many frames since the restart as the original run would have needed in total: not yet done
        Assert.False(task.IsCompleted);
        Assert.Equal(UIAnimationState.Running, animation.State);

        scene.Frames(2); // 4 frames since the restart: the restarted run's own end
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(task.Result);
    }

    [Fact]
    public void FluentChainEndingWithWait_IsAwaitable_ResolvesTrue_AtTheEndOfTheSequence()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        Task<bool> task = scene.Top.Animate(UIBuiltInAnimationTargets.Paths.Opacity, 0f, 1f, 0.032)
            .Then(UIBuiltInAnimationTargets.Paths.RenderTransformRotation, 0f, 90f, 0.032)
            .Wait(0.032)
            .PlayAsync();

        scene.Frames(3); // 48 ms: short of the 96 ms whole chain
        Assert.False(task.IsCompleted);

        scene.Frames(10);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(task.Result);
    }

    [Fact]
    public void StoryboardPlayAsync_ResolvesAtTheEndOfTheComposite_AChildReplacedDuringTheRun_DoesNotChangeTheResult()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> childA = new(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(32) };
        UIPropertyAnimation<float> childB = new(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(96) };
        childB.PresetOwner(scene.Bottom);
        UIStoryboard storyboard = new() { childA, childB };

        Task<bool> task = storyboard.PlayAsync(scene.Top);
        scene.Frames(2); // childA (32 ms) is done, the storyboard (96 ms, the longest child) is not
        Assert.False(task.IsCompleted);

        // An unrelated animation replaces childB on its own path mid-run: the storyboard keeps its own timeline regardless.
        scene.Bottom.Animations.Start(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { To = 0.2f, Duration = TimeSpan.FromMilliseconds(16) });
        scene.Frames(1);
        Assert.False(task.IsCompleted);

        scene.Frames(10); // past 96 ms total
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(task.Result);
    }

    [Fact]
    public void SequencePlayAsync_ResolvesAtTheEndOfTheComposite()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> stepA = new(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(32) };
        UIPropertyAnimation<float> stepB = new(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(32) };
        stepB.PresetOwner(scene.Bottom);
        UISequenceAnimation sequence = new UISequenceAnimation().Append(stepA).Append(stepB);

        Task<bool> task = sequence.PlayAsync(scene.Top);
        scene.Frames(2); // stepA (32 ms) done, stepB not started yet on the sequence's own timeline
        Assert.False(task.IsCompleted);

        scene.Frames(2); // stepB's own 32 ms
        Assert.True(task.IsCompletedSuccessfully);
        Assert.True(task.Result);
        Assert.Equal(1f, scene.Bottom.Opacity, Tolerance);
    }

    [Fact]
    public void StartAsync_OnAPreviewInstance_ThrowsInvalidOperationException()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> preview = UIAnimationPreview.Attach(scene.Top, AnimationTestScene.OpacityAnimation(0f, 1f, 100));

        void StartPreview() => scene.Top.Animations.StartAsync(preview);
        Assert.Throws<InvalidOperationException>(StartPreview);

        UIAnimationPreview.Detach(preview);
    }

    [Fact]
    public void StartAsync_OnAnAlreadyActiveAnimation_ThrowsInvalidOperationException()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        scene.Top.Animations.Start(animation);

        void StartAgain() => scene.Top.Animations.StartAsync(animation);
        Assert.Throws<InvalidOperationException>(StartAgain);
    }

    [Fact]
    public void StartAsync_NullAnimation_ThrowsArgumentNullException()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        void StartNull() => scene.Top.Animations.StartAsync(null);
        Assert.Throws<ArgumentNullException>(StartNull);
    }

    [Fact]
    public void PlayAsyncExtension_NullArguments_ThrowArgumentNullException()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIAnimation nullAnimation = null;
        void PlayNullAnimation() => nullAnimation.PlayAsync(scene.Top);
        Assert.Throws<ArgumentNullException>(PlayNullAnimation);

        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 32);
        void PlayOnNullOwner() => animation.PlayAsync(null);
        Assert.Throws<ArgumentNullException>(PlayOnNullOwner);
    }

    [Fact]
    public void Await_ContinuationRunsInline_OnTheUpdateThread_AndTheSecondAnimationAdvancesFromTheNextFrame()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        SynchronizationContext originalContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(null);
        try
        {
            int testThreadId = Environment.CurrentManagedThreadId;
            int? continuationThreadId = null;

            async Task RunAsync()
            {
                bool result = await scene.Top.Animations.StartAsync(AnimationTestScene.OpacityAnimation(0f, 1f, 32));
                Assert.True(result);
                continuationThreadId = Environment.CurrentManagedThreadId;
                scene.Bottom.Animations.Start(AnimationTestScene.OpacityAnimation(0f, 1f, 32));
            }

            Task task = RunAsync();
            scene.Frames(2); // completes the first animation (32 ms): the continuation runs inline from inside this call

            Assert.True(task.IsCompleted);
            Assert.Equal(testThreadId, continuationThreadId);
            // Started during this tick: advances only from the next one (the manager's own frame rule).
            Assert.Equal(0f, scene.Bottom.Opacity, Tolerance);

            scene.Frames(1);
            Assert.True(scene.Bottom.Opacity > 0f);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(originalContext);
        }
    }

    [Fact]
    public void AwaitedRepeatForeverRun_WithACancellableToken_AllocatesNothingPerFrame()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> animation = AnimationTestScene.OpacityAnimation(0f, 1f, 160);
        animation.RepeatForever = true;
        animation.AutoReverse = true;
        using CancellationTokenSource cts = new();
        Task<bool> task = scene.Top.Animations.StartAsync(animation, cts.Token);
        UIAnimationManager manager = scene.Desktop.Animations;
        TimeSpan frame = TimeSpan.FromMilliseconds(16);
        for (int i = 0; i < 20; i++)
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
        Assert.False(task.IsCompleted);

        cts.Cancel();
        manager.Update(frame);
        Assert.True(task.IsCompletedSuccessfully);
        Assert.False(task.Result);
    }
}
