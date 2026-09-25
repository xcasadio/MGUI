using System.Reflection;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Targets;

namespace MGUI.Tests.Animation;

/// <summary><see cref="MGTextBlock.TextCharactersPerSecond"/> drives the typewriter
/// reveal on the animation engine (<c>TextBlock.TextProgress</c>) instead of accumulating <c>FrameElapsed</c> in <c>UpdateSelf</c>.
/// A direct <see cref="MGTextBlock.TextProgress"/> write seeks the reveal instead of only
/// cancelling it, keeping the original public contract.<para/>
/// <see cref="RevealedCharacters"/> mirrors the private budget computed by <see cref="MGTextBlock.DrawSelf"/> at draw time
/// (<c>(int)(TextProgress * NumCharacters)</c>, `MGTextBlock.cs:1552`): the test harness does not capture drawn text
/// (<c>GraphNoOpDrawTransaction.DrawTextViaEngine</c> is a no-op), so this is the same formula computed from the public
/// <see cref="MGTextBlock.TextProgress"/> instead of a captured draw call.</summary>
public class TextProgressAnimationTests
{
    private const double Tolerance = 1e-3;
    /// <summary>16 ms frames (<see cref="AnimationTestScene.FrameMilliseconds"/>) do not divide every duration used below exactly;
    /// a completion check made from a frame count derived from a non-exact halfway point (itself only approximately reached) tolerates
    /// up to roughly one extra frame's worth of remaining progress (brief ACCEPTANCE 2: "±1 frame").</summary>
    private const double CompletionTolerance = 0.02;

    private static (AnimationTestScene Scene, MGTextBlock TextBlock) Build(string text, bool attach = true)
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGTextBlock textBlock = new(scene.Window, text) { PreferredWidth = 300 };
        if (attach)
        {
            scene.Panel.TryAddChild(textBlock);
            scene.Frames(2);
        }

        return (scene, textBlock);
    }

    /// <summary>Same formula as the private local in <see cref="MGTextBlock.DrawSelf"/> for a plain, unformatted string (its whole
    /// length is <c>NumCharacters</c>).</summary>
    private static int RevealedCharacters(MGTextBlock textBlock, int numCharacters)
        => textBlock.TextProgress.HasValue ? (int)(textBlock.TextProgress.Value * numCharacters) : numCharacters;

    private static object SlotField(MGElement element)
        => typeof(MGElement).GetField("_animationSlot", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(element);

    [Fact]
    public void ARevealCompletes_AtTheExpectedFrame_AndHoldsAtOne()
    {
        // 7 characters at 3.5 chars/s = 2 seconds (ACCEPTANCE 2).
        (AnimationTestScene scene, MGTextBlock textBlock) = Build("1234567");
        textBlock.TextCharactersPerSecond = 3.5;

        Assert.True(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));
        UIElementDebugView view = UIToolingService.CaptureElementDebugView(textBlock);
        Assert.Contains(view.Animations, x => x.Path == UIBuiltInAnimationTargets.Paths.TextBlockTextProgress);

        int totalFrames = 2000 / AnimationTestScene.FrameMilliseconds;
        double lastRevealed = RevealedCharacters(textBlock, 7);
        for (int i = 0; i < totalFrames - 1; i++)
        {
            scene.Frames(1);
            double revealed = RevealedCharacters(textBlock, 7);
            Assert.True(revealed >= lastRevealed, "RevealedCharacters must never decrease while revealing.");
            lastRevealed = revealed;
        }

        Assert.True(textBlock.TextProgress < 1.0);

        scene.Frames(1);
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
        Assert.Equal(7, RevealedCharacters(textBlock, 7));

        // HoldEnd: the run stays held on the path, TextProgress does not move any more.
        scene.Frames(5);
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
    }

    [Fact]
    public void ASpeedChange_MidReveal_RetargetsWithoutAJump()
    {
        // 32 characters at 10 chars/s = 3200 ms = 200 frames of 16 ms exactly, so the halfway point (100 frames) lands on an
        // exact 0.5 with no frame-rounding slack.
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 32));
        textBlock.TextCharactersPerSecond = 10;

        scene.Frames(100);
        double progressBefore = textBlock.TextProgress.Value;
        Assert.Equal(0.5, progressBefore, Tolerance);

        // The remaining half (16 characters) at 40 chars/s takes 0.4 s = a quarter of the original remaining time (25 frames exactly).
        textBlock.TextCharactersPerSecond = 40;
        Assert.Equal(progressBefore, textBlock.TextProgress.Value, Tolerance); // no jump

        scene.Frames(25);
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
    }

    [Fact]
    public void ATextChange_MidReveal_RestartsAtZero_WithTheNewLength()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 10));
        textBlock.TextCharactersPerSecond = 10; // 1 second total

        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds / 2); // halfway
        Assert.True(textBlock.TextProgress > 0.0 && textBlock.TextProgress < 1.0);

        textBlock.Text = new string('b', 20);
        Assert.Equal(0.0, textBlock.TextProgress.Value, Tolerance);

        // 20 characters at 10 chars/s = 2 seconds.
        scene.Frames(2000 / AnimationTestScene.FrameMilliseconds - 1);
        Assert.True(textBlock.TextProgress < 1.0);
        scene.Frames(1);
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
    }

    [Fact]
    public void SettingTheIdenticalText_MidReveal_DoesNotRestart()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 10));
        textBlock.TextCharactersPerSecond = 10;

        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds / 2);
        double progress = textBlock.TextProgress.Value;
        Assert.True(progress > 0.0);

        textBlock.Text = new string('a', 10); // identical string: SetTextCore's branch never runs
        Assert.Equal(progress, textBlock.TextProgress.Value, Tolerance);
    }

    [Fact]
    public void ClearingTheSpeed_MidReveal_CancelsTheRun_AndShowsTheWholeText()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 10));
        textBlock.TextCharactersPerSecond = 10;
        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds / 2);
        Assert.True(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        textBlock.TextCharactersPerSecond = null;
        Assert.Null(textBlock.TextProgress);
        Assert.False(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        scene.Frames(10);
        Assert.Null(textBlock.TextProgress);
    }

    [Fact]
    public void DetachingTheTextBlock_KeepsProgress_AndReattaching_Resumes()
    {
        // Same exact-frame-count numbers as ASpeedChange_MidReveal_RetargetsWithoutAJump.
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 32));
        textBlock.TextCharactersPerSecond = 10;

        scene.Frames(100); // halfway
        double progress = textBlock.TextProgress.Value;
        Assert.Equal(0.5, progress, Tolerance);

        Assert.True(scene.Panel.TryRemoveChild(textBlock));
        Assert.Equal(progress, textBlock.TextProgress.Value, Tolerance);
        Assert.False(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        scene.Frames(10);
        Assert.Equal(progress, textBlock.TextProgress.Value, Tolerance);

        Assert.True(scene.Panel.TryAddChild(textBlock));
        Assert.True(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));
        scene.Frames(100); // the remaining half, at the same speed, takes as long as the first half.
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
    }

    [Fact]
    public void ThePauseOfTheDesktopClock_FreezesTheReveal_AndTimeScale_HalvesTheRemainingTime()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 7));
        textBlock.TextCharactersPerSecond = 3.5; // 2 seconds total

        int oneSecond = 1000 / AnimationTestScene.FrameMilliseconds;
        scene.Frames(oneSecond);
        double progressAtOneSecond = textBlock.TextProgress.Value;
        Assert.Equal(0.5, progressAtOneSecond, 0.05);

        scene.Desktop.Animations.Clock.IsPaused = true;
        scene.Frames(30);
        Assert.Equal(progressAtOneSecond, textBlock.TextProgress.Value, Tolerance);

        scene.Desktop.Animations.Clock.IsPaused = false;
        scene.Frames(oneSecond + 1); // +1: 1000 ms is not an exact multiple of the 16 ms frame, see CompletionTolerance.
        Assert.Equal(1.0, textBlock.TextProgress.Value, CompletionTolerance);
    }

    [Fact]
    public void ATimeScaleOfZero_BehavesLikePause()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 7));
        textBlock.TextCharactersPerSecond = 3.5;

        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds);
        double progress = textBlock.TextProgress.Value;

        scene.Desktop.Animations.Clock.TimeScale = 0f;
        scene.Frames(30);
        Assert.Equal(progress, textBlock.TextProgress.Value, Tolerance);

        scene.Desktop.Animations.Clock.TimeScale = 1f;
        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds + 1); // +1: see CompletionTolerance.
        Assert.Equal(1.0, textBlock.TextProgress.Value, CompletionTolerance);
    }

    [Fact]
    public void ATextBlockNeverGivenASpeed_CarriesNoAnimationSlot()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build("Hello");
        scene.Frames(3);
        scene.Draw();

        Assert.Null(SlotField(textBlock));
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void ATextBlockOutsideTheTree_WithASpeed_AllocatesNoSlot_AndStartsOnAttach()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build("Hello", attach: false);
        textBlock.TextCharactersPerSecond = 5;

        // Not in a tree: nothing started, no exception, no slot (TextProgress itself is still set to 0.0 by the setter, like
        // before this slice: only the animation run needs a desktop to start on).
        Assert.Null(SlotField(textBlock));
        Assert.Equal(0.0, textBlock.TextProgress.Value, Tolerance);
    }

    [Fact]
    public void ATextBlockOutsideTheTree_WithASpeedThenAttached_StartsTheReveal()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build("Hello", attach: false);
        textBlock.TextCharactersPerSecond = 5;

        Assert.True(scene.Panel.TryAddChild(textBlock));
        scene.Frames(2);
        Assert.True(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));
    }

    [Fact]
    public void ARunningReveal_AllocatesNothingPerTick_AfterWarmUp()
    {
        // Same pattern as TransitionTests.RunningPilotTransition_AllocatesNothingPerTick_AfterWarmUp: tick the manager
        // directly (bypassing the full desktop frame, which has unrelated per-frame costs of its own).
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 200));
        textBlock.TextCharactersPerSecond = 10; // 20 seconds, plenty of frames to measure mid-run

        TimeSpan frame = TimeSpan.FromMilliseconds(AnimationTestScene.FrameMilliseconds);
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
        Assert.True(textBlock.TextProgress < 1.0);
    }

    [Fact]
    public void ADirectWrite_ToTextProgress_SeeksTheRunInstead_OfCancellingIt()
    {
        // 20 characters at 10 chars/s = 2 seconds. A direct write mid-run retargets the remaining share instead of leaving
        // the value frozen: writing 0.9 keeps the run alive and it still completes shortly after.
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 20));
        textBlock.TextCharactersPerSecond = 10;
        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds / 2);
        Assert.True(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        textBlock.TextProgress = 0.9;
        Assert.True(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        // Remaining 10% of 20 characters = 2 characters at 10 chars/s = 0.2 s.
        scene.Frames(200 / AnimationTestScene.FrameMilliseconds + 1); // +1: see CompletionTolerance.
        Assert.Equal(1.0, textBlock.TextProgress.Value, CompletionTolerance);
    }

    [Fact]
    public void ADirectWrite_OfZero_AfterCompletion_ReplaysTheReveal()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 10));
        textBlock.TextCharactersPerSecond = 10; // 1 second total

        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds + 1);
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
        Assert.False(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        textBlock.TextProgress = 0.0;
        Assert.True(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        int oneSecond = 1000 / AnimationTestScene.FrameMilliseconds;
        scene.Frames(oneSecond - 1);
        Assert.True(textBlock.TextProgress < 1.0);
        scene.Frames(2); // +1 over the exact 1 s mark: see CompletionTolerance.
        Assert.Equal(1.0, textBlock.TextProgress.Value, CompletionTolerance);
    }

    [Fact]
    public void ADirectWrite_OfAFraction_MidRun_ContinuesFromThere_WithNoBackwardJump()
    {
        // 32 characters at 10 chars/s: halfway (100 frames) lands on an exact 0.5, same as ASpeedChange_MidReveal_RetargetsWithoutAJump.
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 32));
        textBlock.TextCharactersPerSecond = 10;

        scene.Frames(50); // a quarter of the way (0.25)
        double before = textBlock.TextProgress.Value;
        Assert.True(before < 0.5);

        textBlock.TextProgress = 0.5; // seeks forward, past the current run position
        Assert.Equal(0.5, textBlock.TextProgress.Value, Tolerance);

        // The remaining half (16 characters) at 10 chars/s takes 1.6 s = 100 frames.
        scene.Frames(99);
        Assert.True(textBlock.TextProgress.Value >= 0.5 && textBlock.TextProgress.Value < 1.0); // no backward jump
        scene.Frames(1);
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
    }

    [Fact]
    public void ADirectWrite_OfNull_MidRun_CancelsTheRun_AndShowsTheWholeText()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 10));
        textBlock.TextCharactersPerSecond = 10;
        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds / 2);
        Assert.True(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));
        int activeCountBefore = scene.Desktop.Animations.ActiveCount;

        textBlock.TextProgress = null;
        Assert.Null(textBlock.TextProgress);
        Assert.False(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));
        Assert.Equal(activeCountBefore - 1, scene.Desktop.Animations.ActiveCount);

        scene.Frames(10);
        Assert.Null(textBlock.TextProgress);
    }

    [Fact]
    public void ADirectWrite_OfOne_MidRun_CancelsTheRun_AndHoldsComplete()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 10));
        textBlock.TextCharactersPerSecond = 10;
        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds / 2);
        Assert.True(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        textBlock.TextProgress = 1.0;
        Assert.False(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        scene.Frames(10);
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
    }

    [Fact]
    public void ASpeedChange_AfterCompletion_KeepsProgressAtOne()
    {
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 10));
        textBlock.TextCharactersPerSecond = 10; // 1 second total

        scene.Frames(1000 / AnimationTestScene.FrameMilliseconds + 1);
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
        Assert.False(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        textBlock.TextCharactersPerSecond = 20; // documented behaviour: a speed change after completion does not restart
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
        Assert.False(textBlock.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        scene.Frames(10);
        Assert.Equal(1.0, textBlock.TextProgress.Value, Tolerance);
    }

    [Fact]
    public void TheRunsOwnWrites_NeverRestartIt_OverManyFrames()
    {
        // Guard against a per-tick restart loop: ApplyAnimatedTextProgress's own writes must go through the
        // _IsApplyingAnimatedTextProgress branch and never call SyncTextProgressAnimation, which would otherwise
        // replace (and so "start") a new run on the manager every single frame.
        (AnimationTestScene scene, MGTextBlock textBlock) = Build(new string('a', 60));
        textBlock.TextCharactersPerSecond = 10; // 6 seconds, plenty of frames to observe

        int startCount = 0;
        UIElementDebugView view = UIToolingService.CaptureElementDebugView(textBlock);
        UIAnimation reveal = textBlock.Animations.Active.Single(x => x.Name == "TextBlock.TextReveal");
        reveal.Started += (sender, e) => startCount++;

        for (int i = 0; i < 60; i++)
        {
            scene.Frames(1);
        }

        Assert.True(textBlock.TextProgress is > 0.0 and < 1.0);
        Assert.Equal(0, startCount); // the initial Start() above is not counted by the handler attached after it
        Assert.Equal(1, textBlock.Animations.Active.Count(x => x.Name == "TextBlock.TextReveal"));
    }

    [Fact]
    public void ATransitionOnThePath_IsRefused()
    {
        (AnimationTestScene _, MGTextBlock textBlock) = Build("Hello");
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => textBlock.Transitions.Add(new UITransition<double>(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress, TimeSpan.FromMilliseconds(100))));
        Assert.Contains("not observable", error.Message);
    }

    [Fact]
    public void TheTarget_IsRegistered_AndRefusesOtherElements()
    {
        Assert.Equal(typeof(double), UIAnimationTargets.GetValueType(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));

        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<double> animation = new(UIBuiltInAnimationTargets.Paths.TextBlockTextProgress) { To = 1.0, Duration = TimeSpan.FromMilliseconds(100) };
        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(animation));
        Assert.Contains(nameof(MGTextBlock), error.Message);
    }
}
