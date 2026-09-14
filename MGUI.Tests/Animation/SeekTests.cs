using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Animation;

/// <summary><see cref="UIAnimationPreview.Attach{TAnimation}"/> and <see cref="UIAnimation.Seek"/>.</summary>
public class SeekTests
{
    private const float Tolerance = 1e-4f;

    private static UIPropertyAnimation<float> Fade(float from, float to, int milliseconds)
        => new() { Target = AnimationTestScene.OpacityTarget, From = from, To = to, Duration = TimeSpan.FromMilliseconds(milliseconds) };

    private static UIPropertyAnimation<float> Spin(float to, int milliseconds)
        => new(UIBuiltInAnimationTargets.Paths.RenderTransformRotation) { From = 0f, To = to, Duration = TimeSpan.FromMilliseconds(milliseconds) };

    [Theory]
    [InlineData(0f)]
    [InlineData(0.25f)]
    [InlineData(0.5f)]
    [InlineData(0.999f)]
    [InlineData(1f)]
    public void ForwardSeek_MatchesALiveTwin_AdvancedFrameByFrame(float fraction)
    {
        AnimationTestScene liveScene = AnimationTestScene.Build();
        UIPropertyAnimation<float> live = Fade(0f, 1f, 1000);
        live.Easing = UIEasing.CubicOut;
        liveScene.Top.Animations.Start(live);
        int totalMs = (int)(1000 * fraction);
        int frames = totalMs / AnimationTestScene.FrameMilliseconds;
        if (frames > 0)
        {
            liveScene.Frames(frames);
        }

        AnimationTestScene previewScene = AnimationTestScene.Build();
        UIPropertyAnimation<float> preview = Fade(0f, 1f, 1000);
        preview.Easing = UIEasing.CubicOut;
        UIAnimationPreview.Attach(previewScene.Top, preview);
        preview.Seek(TimeSpan.FromMilliseconds(frames * AnimationTestScene.FrameMilliseconds));

        Assert.Equal(live.Progress, preview.Progress, Tolerance);
        Assert.Equal(live.CurrentValue, preview.CurrentValue, Tolerance);
        Assert.Equal(liveScene.Top.Opacity, previewScene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void WithDelayRepeatAndAutoReverse_MatchesALiveTwin_AtKeyPoints()
    {
        // Delay 50ms, Duration 100ms, AutoReverse, RepeatCount 2 (3 iterations): one iteration is 200ms, total length 50 + 600 = 650ms.
        UIPropertyAnimation<float> Build() => new()
        {
            Target = AnimationTestScene.OpacityTarget,
            From = 0f,
            To = 1f,
            Duration = TimeSpan.FromMilliseconds(100),
            Delay = TimeSpan.FromMilliseconds(50),
            AutoReverse = true,
            RepeatCount = 2,
        };

        // Inside the delay: progress 0, the From value.
        AssertMatchesLiveTwin(Build, Build, TimeSpan.FromMilliseconds(20), 10);

        // Mid second iteration, reversing (elapsed-delay = 350ms: iteration 1, within-iteration 150ms of 200ms -> progress 0.5, reversing).
        AssertMatchesLiveTwin(Build, Build, TimeSpan.FromMilliseconds(400), 10);

        // Beyond the total length: clamped, final pose (canonical held pose of the last iteration's own end).
        AssertMatchesLiveTwin(Build, Build, TimeSpan.FromMilliseconds(1000), 10);
    }

    private static void AssertMatchesLiveTwin(Func<UIPropertyAnimation<float>> buildLive, Func<UIPropertyAnimation<float>> buildPreview, TimeSpan at, int frameMilliseconds)
    {
        AnimationTestScene liveScene = AnimationTestScene.Build();
        UIPropertyAnimation<float> live = buildLive();
        liveScene.Top.Animations.Start(live);
        var frame = TimeSpan.FromMilliseconds(frameMilliseconds);
        var elapsed = TimeSpan.Zero;
        while (elapsed < at && live.IsActive)
        {
            liveScene.Desktop.Animations.Update(frame);
            elapsed += frame;
        }

        AnimationTestScene previewScene = AnimationTestScene.Build();
        UIPropertyAnimation<float> preview = buildPreview();
        UIAnimationPreview.Attach(previewScene.Top, preview);
        preview.Seek(at);

        Assert.Equal(live.Progress, preview.Progress, Tolerance);
        Assert.Equal(live.Iteration, preview.Iteration);
        Assert.Equal(live.IsReversing, preview.IsReversing);
    }

    [Fact]
    public void BackwardSeek_ValuesFollow_StateStaysRunning_NoEventRaised()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> preview = Fade(0f, 1f, 1000);
        int started = 0, updated = 0, repeated = 0, reversed = 0, completed = 0, cancelled = 0;
        preview.Started += (_, _) => started++;
        preview.Updated += (_, _) => updated++;
        preview.Repeated += (_, _) => repeated++;
        preview.Reversed += (_, _) => reversed++;
        preview.Completed += (_, _) => completed++;
        preview.Cancelled += (_, _) => cancelled++;
        UIAnimationPreview.Attach(scene.Top, preview);

        preview.Seek(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(1f, preview.Progress, Tolerance);
        Assert.Equal(UIAnimationState.Running, preview.State);

        preview.Seek(TimeSpan.FromMilliseconds(500));
        Assert.Equal(0.5f, preview.Progress, Tolerance);
        Assert.Equal(UIAnimationState.Running, preview.State);

        preview.Seek(TimeSpan.Zero);
        Assert.Equal(0f, preview.Progress, Tolerance);
        Assert.Equal(UIAnimationState.Running, preview.State);

        Assert.Equal(0, started);
        Assert.Equal(0, updated);
        Assert.Equal(0, repeated);
        Assert.Equal(0, reversed);
        Assert.Equal(0, completed);
        Assert.Equal(0, cancelled);
    }

    [Fact]
    public void SeekingPastTheEnd_AndBackAndForth_RaisesNoEvent_ThenCancelRaisesExactlyOne()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> preview = Fade(0f, 1f, 300);
        preview.RepeatCount = 1;
        preview.AutoReverse = true;
        int events = 0;
        preview.Started += (_, _) => events++;
        preview.Updated += (_, _) => events++;
        preview.Repeated += (_, _) => events++;
        preview.Reversed += (_, _) => events++;
        preview.Completed += (_, _) => events++;
        int cancelled = 0;
        preview.Cancelled += (_, _) => cancelled++;
        UIAnimationPreview.Attach(scene.Top, preview);

        var random = new Random(1234);
        for (int i = 0; i < 50; i++)
        {
            preview.Seek(TimeSpan.FromMilliseconds(random.Next(-100, 2000)));
        }

        Assert.Equal(0, events);
        Assert.Equal(0, cancelled);

        preview.Cancel();
        Assert.Equal(1, cancelled);
    }

    [Fact]
    public void Storyboard_PreviewPositionsBothChildren_ByElapsedTime()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = Fade(0f, 1f, 200);
        UIPropertyAnimation<float> spin = Spin(90f, 400);
        UIStoryboard storyboard = new() { fade, spin };

        UIAnimationPreview.Attach(scene.Top, storyboard);
        Assert.Equal(TimeSpan.FromMilliseconds(400), storyboard.Duration);

        storyboard.Seek(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Equal(22.5f, scene.Top.RenderTransform.Rotation, Tolerance);

        storyboard.Seek(TimeSpan.FromMilliseconds(1000));
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(90f, scene.Top.RenderTransform.Rotation, Tolerance);

        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Sequence_PreviewPositionsChildren_BeforeOffsetAndAfterEnd()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = Fade(0f, 1f, 150);
        UIPropertyAnimation<float> spin = Spin(90f, 150);
        UISequenceAnimation sequence = new UISequenceAnimation().Append(fade).AppendDelay(TimeSpan.FromMilliseconds(50)).Append(spin);

        UIAnimationPreview.Attach(scene.Top, sequence);
        Assert.Equal(TimeSpan.FromMilliseconds(350), sequence.Duration);

        // Before spin's own offset (200ms): spin sits at its initial pose.
        sequence.Seek(TimeSpan.FromMilliseconds(50));
        Assert.Equal(50f / 150f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0f, scene.Top.RenderTransform.Rotation, Tolerance);

        // Past the sequence's own end: both children at their final pose.
        sequence.Seek(TimeSpan.FromMilliseconds(350));
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(90f, scene.Top.RenderTransform.Rotation, Tolerance);

        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void NestedStoryboard_PreviewPositionsTheGrandchild_ByElapsedTime()
    {
        // A storyboard containing a nested storyboard containing a 200ms fade. The outer's own Duration is
        // computed (UIAnimationGroup.OnStarting -> ComputeDuration -> LengthOf(child)) from the nested group's Duration, which is
        // itself only known once the nested group's own OnStarting has run; BeginPreview now attaches children (recursively) before
        // computing its own Duration, so the nested child's Duration is populated by the time the outer computes its own.
        AnimationTestScene liveScene = AnimationTestScene.Build();
        UIPropertyAnimation<float> liveFade = Fade(0f, 1f, 200);
        UIStoryboard liveInner = new() { liveFade };
        UIStoryboard liveOuter = new() { liveInner };
        liveScene.Top.Animations.Start(liveOuter);

        AnimationTestScene previewScene = AnimationTestScene.Build();
        UIPropertyAnimation<float> previewFade = Fade(0f, 1f, 200);
        UIStoryboard previewInner = new() { previewFade };
        UIStoryboard previewOuter = new() { previewInner };
        UIAnimationPreview.Attach(previewScene.Top, previewOuter);

        Assert.Equal(TimeSpan.FromMilliseconds(200), previewInner.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(200), previewOuter.Duration);

        var frame = TimeSpan.FromMilliseconds(10);
        var elapsed = TimeSpan.Zero;
        foreach (var ms in new[] { 50, 100, 150 })
        {
            var target = TimeSpan.FromMilliseconds(ms);
            while (elapsed < target)
            {
                liveScene.Desktop.Animations.Update(frame);
                elapsed += frame;
            }

            previewOuter.Seek(target);
            Assert.Equal(liveFade.Progress, previewFade.Progress, Tolerance);
            Assert.Equal(liveScene.Top.Opacity, previewScene.Top.Opacity, Tolerance);
        }
    }

    [Fact]
    public void Attach_FailedGroupValidation_LeavesTheInstanceReusable()
    {
        // OnStarting's AutoReverse validation (UIAnimationGroup.OnStarting) runs, and can throw, before
        // IsPreview/State are set (BeginPreview), so a rejected attach leaves the instance at its original Stopped/non-preview state,
        // reusable exactly like a live Begin that throws in OnStarting leaves State == Stopped.
        AnimationTestScene scene = AnimationTestScene.Build();
        UIStoryboard invalid = new() { Fade(0f, 1f, 200) };
        invalid.AutoReverse = true;

        Assert.Throws<ArgumentException>(() => UIAnimationPreview.Attach(scene.Top, invalid));
        Assert.Equal(UIAnimationState.Stopped, invalid.State);
        Assert.False(invalid.IsActive);

        invalid.AutoReverse = false;
        UIAnimationPreview.Attach(scene.Top, invalid);
        Assert.Equal(UIAnimationState.Running, invalid.State);
    }

    [Fact]
    public void Storyboard_ChildPresetOnAnotherElement_PreviewsThere_WithoutStartThenCancel()
    {
        // ADR-0008 decision 8: UIAnimation.PresetOwner replaces the Start-then-Cancel idiom CompositionTests used to bind a child to a
        // second element (start it for real, then Cancel it, leaving Owner set but State at Cancelled).
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fadeOnTop = Fade(0f, 1f, 200);
        UIPropertyAnimation<float> spinOnBottom = Spin(90f, 200);
        spinOnBottom.PresetOwner(scene.Bottom);
        UIStoryboard storyboard = new() { fadeOnTop, spinOnBottom };

        UIAnimationPreview.Attach(scene.Top, storyboard);
        Assert.Same(scene.Top, fadeOnTop.Owner);
        Assert.Same(scene.Bottom, spinOnBottom.Owner);

        storyboard.Seek(TimeSpan.FromMilliseconds(100));
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Equal(45f, scene.Bottom.RenderTransform.Rotation, Tolerance);

        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void PresetOwner_WhileActive_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> live = Fade(0f, 1f, 200);
        scene.Top.Animations.Start(live);

        Assert.Throws<InvalidOperationException>(() => live.PresetOwner(scene.Bottom));
    }

    [Fact]
    public void Attach_FailedGroupValidation_RollsBackChildrenAlreadyAttachedAsPreviews_AndACorrectedRetrySucceeds()
    {
        // OnPreviewAttached had already begun
        // both children as previews (Running, IsPreview) by the time OnStarting's AutoReverse check threw and the group itself fell back
        // to Stopped; nothing used to roll the children back, so they stayed stuck reporting Running/preview forever.
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = Fade(0f, 1f, 200);
        UIPropertyAnimation<float> spin = Spin(90f, 200);
        UIStoryboard invalid = new() { fade, spin };
        invalid.AutoReverse = true;

        Assert.Throws<ArgumentException>(() => UIAnimationPreview.Attach(scene.Top, invalid));

        Assert.Equal(UIAnimationState.Stopped, invalid.State);
        Assert.False(invalid.IsActive);
        Assert.Equal(UIAnimationState.Stopped, fade.State);
        Assert.False(fade.IsPreview);
        Assert.Equal(UIAnimationState.Stopped, spin.State);
        Assert.False(spin.IsPreview);

        invalid.AutoReverse = false;
        UIAnimationPreview.Attach(scene.Top, invalid);
        Assert.Equal(UIAnimationState.Running, invalid.State);
        Assert.Equal(UIAnimationState.Running, fade.State);
        Assert.Equal(UIAnimationState.Running, spin.State);
    }

    [Fact]
    public void Seek_OnALiveAnimation_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> live = Fade(0f, 1f, 200);
        scene.Top.Animations.Start(live);

        Assert.Throws<InvalidOperationException>(() => live.Seek(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void Seek_OnAStoppedAnimation_Throws()
    {
        UIPropertyAnimation<float> unattached = Fade(0f, 1f, 200);
        Assert.Throws<InvalidOperationException>(() => unattached.Seek(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void Seek_AfterCancel_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> preview = Fade(0f, 1f, 200);
        UIAnimationPreview.Attach(scene.Top, preview);
        preview.Cancel();

        Assert.Throws<InvalidOperationException>(() => preview.Seek(TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void Attach_Null_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        Assert.Throws<ArgumentNullException>(() => UIAnimationPreview.Attach(null, Fade(0f, 1f, 200)));
        Assert.Throws<ArgumentNullException>(() => UIAnimationPreview.Attach<UIPropertyAnimation<float>>(scene.Top, null));
    }

    [Fact]
    public void Attach_Twice_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> preview = Fade(0f, 1f, 200);
        UIAnimationPreview.Attach(scene.Top, preview);

        Assert.Throws<InvalidOperationException>(() => UIAnimationPreview.Attach(scene.Top, preview));
    }

    [Fact]
    public void Attach_ARegisteredAnimation_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> live = Fade(0f, 1f, 200);
        scene.Top.Animations.Start(live);

        Assert.Throws<InvalidOperationException>(() => UIAnimationPreview.Attach(scene.Bottom, live));
    }

    [Fact]
    public void Attach_WhenALiveAnimationAlreadyOccupiesThePath_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> live = Fade(0f, 1f, 200);
        scene.Top.Animations.Start(live);

        Assert.Throws<InvalidOperationException>(() => UIAnimationPreview.Attach(scene.Top, Fade(0f, 1f, 200)));
        Assert.Empty(scene.Top.Animations.Held);
    }

    [Fact]
    public void StartingALiveAnimation_AfterAPreviewWasAttached_ProceedsObliviously_TheyFightOverTheValue()
    {
        // Documented limit: the manager has no notion of a preview, so it never refuses in this direction; whichever ticks or
        // seeks last on a given frame wins the physical value until the preview is cancelled.
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> preview = Fade(0f, 1f, 200);
        UIAnimationPreview.Attach(scene.Top, preview);
        preview.Seek(TimeSpan.FromMilliseconds(200));
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);

        UIPropertyAnimation<float> live = Fade(1f, 0f, 160);
        scene.Top.Animations.Start(live);
        Assert.Equal(UIAnimationState.Running, live.State);
        Assert.Equal(1, scene.Desktop.Animations.ActiveCount);

        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        // The preview seeking again overwrites the live animation's write for that instant.
        preview.Seek(TimeSpan.Zero);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Preview_NeverRegisteredWithTheManager_AdvancingTheSceneDoesNotMoveIt_DetachingTheElementDoesNotCancelIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        float original = scene.Top.Opacity;
        UIPropertyAnimation<float> preview = Fade(0f, 1f, 200);
        UIAnimationPreview.Attach(scene.Top, preview);
        preview.Seek(TimeSpan.FromMilliseconds(100));

        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
        Assert.Empty(scene.Top.Animations.Active);

        scene.Frames(10);
        Assert.Equal(0.5f, preview.Progress, Tolerance);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        Assert.True(scene.Panel.TryRemoveChild(scene.Top));
        Assert.Equal(UIAnimationState.Running, preview.State);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        preview.Cancel();
        Assert.Equal(UIAnimationState.Cancelled, preview.State);
        Assert.Equal(original, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Cancel_KeepCurrent_KeepsTheSeekedValue_RestoreBaseValue_RestoresTheOriginal()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        float original = scene.Top.Opacity;

        UIPropertyAnimation<float> keep = Fade(0f, 1f, 200);
        keep.CancelBehavior = UIAnimationCancelBehavior.KeepCurrent;
        UIAnimationPreview.Attach(scene.Top, keep);
        keep.Seek(TimeSpan.FromMilliseconds(50));
        keep.Cancel();
        Assert.Equal(0.25f, scene.Top.Opacity, Tolerance);

        scene.Top.Opacity = original;
        UIPropertyAnimation<float> restore = Fade(0f, 1f, 200);
        restore.CancelBehavior = UIAnimationCancelBehavior.RestoreBaseValue;
        UIAnimationPreview.Attach(scene.Top, restore);
        restore.Seek(TimeSpan.FromMilliseconds(50));
        restore.Cancel();
        Assert.Equal(original, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void OnAStorePilot_WritesTheAnimationContribution_AndCancelRestoreRemovesIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        UIPropertyAnimation<Color> preview = new(UIColorAnimationTargets.Paths.Background) { To = Color.Blue, Duration = TimeSpan.FromMilliseconds(200) };

        UIAnimationPreview.Attach(scene.Top, preview);
        preview.Seek(TimeSpan.FromMilliseconds(100));

        var contributions = scene.Top.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal);
        Assert.Contains(contributions, x => x.Kind == UIValueSourceKind.Animation);
        MGSolidFillBrush during = Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Lerp(Color.Gray, Color.Blue, 0.5f), during.Color);

        preview.Cancel();

        var afterCancel = scene.Top.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal);
        Assert.DoesNotContain(afterCancel, x => x.Kind == UIValueSourceKind.Animation);
        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void Seek_AllocatesNothing_OnAPropertyAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> preview = Fade(0f, 1f, 100000);
        UIAnimationPreview.Attach(scene.Top, preview);

        for (int i = 0; i < 20; i++)
        {
            preview.Seek(TimeSpan.FromMilliseconds(i * 37));
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            preview.Seek(TimeSpan.FromMilliseconds(i * 37));
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0, after - before);
    }

    [Fact]
    public void Seek_AllocatesNothing_OnAStoryboard()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIStoryboard storyboard = new() { Fade(0f, 1f, 100000), Spin(90f, 100000) };
        UIAnimationPreview.Attach(scene.Top, storyboard);

        for (int i = 0; i < 20; i++)
        {
            storyboard.Seek(TimeSpan.FromMilliseconds(i * 41));
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            storyboard.Seek(TimeSpan.FromMilliseconds(i * 41));
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.Equal(0, after - before);
    }
}
