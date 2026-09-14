using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.BorderBrushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Animation;

/// <summary>ADR-0009: <see cref="MGHighlightBorderBrush"/> no longer advances its own
/// <see cref="MGHighlightBorderBrush.AnimationProgress"/> (see the deleted <c>HighlightBorderBrushClockTests</c>) -- an engine run hosted by
/// <see cref="MGElement"/> (<c>SyncBorderHighlightRun</c>, called from <see cref="MGElement.Update"/>) does, on the element's own run-owned
/// clone of the effective highlight border brush, so a shared frozen brush is never mutated. <see cref="HighlightBorderBrushRingAnimationTests"/>
/// (geometry) is untouched by this slice.</summary>
public class HighlightBorderBrushEngineTests
{
    private const double Tolerance = 1e-6;

    private static MGHighlightBorderBrush BuildFrozenBrush(bool autoStart = true, bool stopOnMouseOver = false, bool stopOnClick = false)
    {
        MGHighlightBorderBrush brush = new(MGUniformBorderBrush.Black, Color.White, HighlightAnimation.Pulse)
        {
            PulseFadeDuration = TimeSpan.FromSeconds(1.0),
            PulseDelay = TimeSpan.Zero,
            AutoStart = autoStart,
            StopOnMouseOver = stopOnMouseOver,
            StopOnClick = stopOnClick,
        };
        brush.Freeze();
        return brush;
    }

    private static MGHighlightBorderBrush EffectiveHighlight(MGElement element) => element.GetBorder()?.BorderBrush as MGHighlightBorderBrush;

    [Fact]
    public void OnAttach_AutoStartsARun_AndAdvancesTheElementsOwnClone_LeavingTheFrozenBaseUntouched()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGHighlightBorderBrush shared = BuildFrozenBrush();
        scene.Top.BorderBrush = shared;
        scene.Bottom.BorderBrush = shared;

        scene.Frames(60);

        Assert.True(scene.Top.Animations.IsAnimating("BorderBrush.Highlight.Progress") ||
                    EffectiveHighlight(scene.Top)?.AnimationProgress > 0.0);
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress > 0.0);
        Assert.True(EffectiveHighlight(scene.Bottom).AnimationProgress > 0.0);

        //  Both elements got their own clone: not the same instance, and the shared frozen brush's own progress never moved.
        Assert.NotSame(EffectiveHighlight(scene.Top), EffectiveHighlight(scene.Bottom));
        Assert.NotSame(shared, EffectiveHighlight(scene.Top));
        Assert.Equal(0.0, shared.AnimationProgress, Tolerance);
    }

    [Fact]
    public void StopOnMouseOver_StopsOnlyTheHoveredElement_AndResumeRestartsIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGHighlightBorderBrush shared = BuildFrozenBrush(stopOnMouseOver: true);
        scene.Top.BorderBrush = shared;
        scene.Bottom.BorderBrush = shared;
        scene.Frames(10);

        scene.Mouse = new Point(10, 10); // inside Top
        scene.Frames(5);

        Assert.False(EffectiveHighlight(scene.Top).IsEnabled);
        Assert.True(EffectiveHighlight(scene.Bottom).IsEnabled);

        double topProgressWhenStopped = EffectiveHighlight(scene.Top).AnimationProgress;
        scene.Frames(20);
        Assert.Equal(topProgressWhenStopped, EffectiveHighlight(scene.Top).AnimationProgress, Tolerance);
        Assert.True(EffectiveHighlight(scene.Bottom).AnimationProgress > topProgressWhenStopped);

        //  Moving the mouse away does not, by itself, resume it (matches the documented behaviour).
        scene.Mouse = new Point(-100, -100);
        scene.Frames(5);
        Assert.False(EffectiveHighlight(scene.Top).IsEnabled);

        scene.Top.ResumeBorderHighlight();
        scene.Frames(5);
        Assert.True(EffectiveHighlight(scene.Top).IsEnabled);
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress > topProgressWhenStopped);
    }

    [Fact]
    public void StopOnClick_StopsOnlyThePressedElement_AndResumeRestartsIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGHighlightBorderBrush shared = BuildFrozenBrush(stopOnClick: true);
        scene.Top.BorderBrush = shared;
        scene.Bottom.BorderBrush = shared;
        scene.Frames(10);

        scene.Mouse = new Point(10, 10); // inside Top
        scene.Frames(5, leftPressed: true);

        Assert.False(EffectiveHighlight(scene.Top).IsEnabled);
        Assert.True(EffectiveHighlight(scene.Bottom).IsEnabled);

        double topProgressWhenStopped = EffectiveHighlight(scene.Top).AnimationProgress;
        scene.Frames(20, leftPressed: true);
        Assert.Equal(topProgressWhenStopped, EffectiveHighlight(scene.Top).AnimationProgress, Tolerance);
        Assert.True(EffectiveHighlight(scene.Bottom).AnimationProgress > topProgressWhenStopped);

        //  Releasing the mouse does not, by itself, resume it (matches StopOnMouseOver's own behaviour).
        scene.Frames(5); // released, still hovered
        Assert.False(EffectiveHighlight(scene.Top).IsEnabled);

        scene.Top.ResumeBorderHighlight();
        scene.Frames(5);
        Assert.True(EffectiveHighlight(scene.Top).IsEnabled);
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress > topProgressWhenStopped);
    }

    [Fact]
    public void IsEnabledFalse_OnTheLiveBase_StopsTheRun_AndTrueRestartsIt()
    {
        //  SyncBorderHighlightRun checks StopOnMouseOver/StopOnClick while a run is active, and re-reads
        //  baseHighlight.IsEnabled -- so toggling the checkbox TwoWay-bound to it in IBorderBrush.xaml.cs (a live, unfrozen brush) takes
        //  effect while highlighting. Unlike StopOnMouseOver/StopOnClick, this does not need ResumeBorderHighlight: setting IsEnabled
        //  back to true restarts it on its own, next frame, matching the Update() gate of "if (IsEnabled)".
        AnimationTestScene scene = AnimationTestScene.Build();
        MGHighlightBorderBrush live = new(MGUniformBorderBrush.Black, Color.White, HighlightAnimation.Pulse)
        {
            PulseFadeDuration = TimeSpan.FromSeconds(1.0),
            PulseDelay = TimeSpan.Zero,
        };
        scene.Top.BorderBrush = live;
        scene.Frames(10);
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress > 0.0);

        live.IsEnabled = false;
        scene.Frames(2);
        Assert.False(EffectiveHighlight(scene.Top).IsEnabled);
        double progressWhenDisabled = EffectiveHighlight(scene.Top).AnimationProgress;

        scene.Frames(20);
        Assert.Equal(progressWhenDisabled, EffectiveHighlight(scene.Top).AnimationProgress, Tolerance);

        live.IsEnabled = true;
        scene.Frames(10);
        Assert.True(EffectiveHighlight(scene.Top).IsEnabled);
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress > progressWhenDisabled);
    }

    [Fact]
    public void CycleDurationChange_WhileActive_RestartsFromCurrentProgress_WithTheNewDuration()
    {
        //  StartBorderHighlightRun captures baseHighlight.CycleDuration once, and SyncBorderHighlightRun compares
        //  it against the live run's own Duration every frame while active, so a runtime duration edit is picked up rather than
        //  silently dropped until the run is torn down.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGHighlightBorderBrush live = new(MGUniformBorderBrush.Black, Color.White, HighlightAnimation.Pulse)
        {
            PulseFadeDuration = TimeSpan.FromSeconds(1.0),
            PulseDelay = TimeSpan.Zero,
        };
        scene.Top.BorderBrush = live;
        scene.Frames(5);
        double progressAtSwitch = EffectiveHighlight(scene.Top).AnimationProgress;

        live.PulseFadeDuration = TimeSpan.FromSeconds(4.0);
        int oneRealSecond = 1000 / AnimationTestScene.FrameMilliseconds;
        scene.Frames(oneRealSecond);

        double delta = EffectiveHighlight(scene.Top).AnimationProgress - progressAtSwitch;
        //  A dropped edit (still a 1s cycle) would wrap a full cycle over this window (delta ~ 0); the fixed 4s cycle advances ~0.25.
        Assert.True(delta is > 0.15 and < 0.35, $"expected ~0.25 progress over ~1s on a 4s cycle, got delta={delta}");
    }

    [Fact]
    public void Detach_KeepsThePose_AndReattachResumes()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BorderBrush = BuildFrozenBrush();
        scene.Frames(10);

        double progressBeforeDetach = EffectiveHighlight(scene.Top).AnimationProgress;
        Assert.True(progressBeforeDetach > 0.0);

        scene.Panel.TryRemoveChild(scene.Top);
        scene.Frames(5);
        Assert.Equal(progressBeforeDetach, EffectiveHighlight(scene.Top).AnimationProgress, Tolerance);

        scene.Panel.TryAddChild(scene.Top);
        scene.Frames(5);
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress >= progressBeforeDetach);
    }

    [Fact]
    public void AutoStartFalse_StaysStill_ThenAnAppDrivenAnimationDrivesIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BorderBrush = BuildFrozenBrush(autoStart: false);
        scene.Frames(30);

        Assert.False(scene.Top.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.BorderBrushHighlightProgress));
        Assert.False(scene.Top.Animations.IsAnimating("BorderBrush"));
        Assert.Equal(0.0, ((MGHighlightBorderBrush)scene.Top.BorderBrush).AnimationProgress, Tolerance);

        scene.Top.Animate(UIBuiltInAnimationTargets.Paths.BorderBrushHighlightProgress, 0.0, 1.0, 1.0).RepeatForever().Play();
        scene.Frames(30); // ~0.48s of a 1s cycle
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress > 0.0);
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress < 1.0);
    }

    [Fact]
    public void PreviewSeek_PositionsTheClone()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BorderBrush = BuildFrozenBrush(autoStart: false);
        scene.Frames(2);

        UIPropertyAnimation<double> preview = new(UIBuiltInAnimationTargets.Paths.BorderBrushHighlightProgress) { From = 0.0, To = 1.0, Duration = TimeSpan.FromSeconds(1.0) };
        UIAnimationPreview.Attach(scene.Top, preview);
        preview.Seek(TimeSpan.FromMilliseconds(500));

        Assert.Equal(0.5, EffectiveHighlight(scene.Top).AnimationProgress, 0.01);
        preview.Cancel();
    }

    [Fact]
    public void PauseAndTimeScale_OfTheDesktopClock_ApplyToTheHostRun()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BorderBrush = BuildFrozenBrush();
        scene.Frames(2);

        scene.Desktop.Animations.Clock.TimeScale = 0.5f;
        int oneRealSecond = 1000 / AnimationTestScene.FrameMilliseconds;
        scene.Frames(oneRealSecond);
        Assert.Equal(0.5, EffectiveHighlight(scene.Top).AnimationProgress, 0.05);

        double progressBeforePause = EffectiveHighlight(scene.Top).AnimationProgress;
        scene.Desktop.Animations.Clock.IsPaused = true;
        scene.Frames(30);
        Assert.Equal(progressBeforePause, EffectiveHighlight(scene.Top).AnimationProgress, Tolerance);
    }

    [Fact]
    public void ZeroAllocation_AfterWarmup_Over200Ticks()
    {
        //  Matches BrushAnimationTargetsTests' own convention (ADR-0009): scene.Frames drives the full Desktop.Update (input,
        //  hover resolution, tooltips, ...), which is not itself claimed allocation-free; only the animation TICK is. Warm-up uses
        //  scene.Frames so SyncBorderHighlightRun starts the run once (attach), then the probe ticks only the manager directly,
        //  isolating the clone's per-tick write.
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BorderBrush = BuildFrozenBrush();
        scene.Frames(10); // warm-up: clone creation, first store write

        TimeSpan frame = TimeSpan.FromMilliseconds(AnimationTestScene.FrameMilliseconds);
        UIAnimationManager manager = scene.Desktop.Animations;

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            manager.Update(frame);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void ColourAnimationOnBorderBrush_WhileHighlighting_IsRefused_NotSwapped()
    {
        //  Documented decision (ADR-0009, MGElement.HighlightRun's own doc): the host run does NOT share a conflict key with a
        //  colour animation on "BorderBrush". Both ultimately write the same Whole/Animation slot through a run-owned clone, but the
        //  pre-existing BorderBrushTarget.GetValue reads the CURRENT effective border brush at
        //  OnStarting, not the value below the animation, and a highlight brush is never a uniform-over-solid brush -- so sharing a key
        //  would only replace the exception's message, not remove it (the manager's KeepCurrent cancel leaves the losing side's clone as
        //  the effective value, which is never the type the winning side's own OnStarting read expects). Refusing, with the type check's
        //  own clear message, is what this slice can safely deliver: the highlight keeps running, untouched.
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BorderBrush = BuildFrozenBrush();
        scene.Frames(10);
        double progressBeforeAttempt = EffectiveHighlight(scene.Top).AnimationProgress;

        UIPropertyAnimation<Color> colour = new("BorderBrush") { From = Color.White, To = Color.Red, Duration = TimeSpan.FromSeconds(1) };
        Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(colour));

        scene.Frames(5);
        Assert.NotNull(EffectiveHighlight(scene.Top));
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress > progressBeforeAttempt);
    }

    [Fact]
    public void ColourAnimationOnBorderBrush_StartedFirst_ThenAHighlightBase_IsRefused_WithoutThrowingFromUpdate()
    {
        //  The reverse order of ColourAnimationOnBorderBrush_WhileHighlighting_IsRefused_NotSwapped above -- a colour
        //  animation on "BorderBrush" already owns the Whole/Animation slot (its clone is the effective border brush) when the base is
        //  then set to a highlight brush. SyncBorderHighlightRun auto-starts the host run with no application call to catch a refusal,
        //  and refuses silently: the colour animation keeps running untouched, and the host run starts only once that animation ends
        //  and releases the slot (rather than StartBorderHighlightRun's Animations.Start(run) reaching
        //  BorderBrushHighlightProgressTarget.RequireCurrent through UIAnimation.Begin -> ReadCurrentValue and throwing, unhandled,
        //  every frame, from inside MGDesktop.Update() itself).
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Animate("BorderBrush", Color.White, Color.Red, 2.0).Play();
        scene.Frames(5);

        scene.Top.BorderBrush = BuildFrozenBrush();
        scene.Frames(10); // must not throw out of Desktop.Update()

        Assert.True(scene.Top.Animations.IsAnimating("BorderBrush"));
        Assert.Null(EffectiveHighlight(scene.Top));

        //  Releasing the colour animation's hold on the slot (ACCEPTANCE parity with the reverse order's own resolution): the host
        //  run, retried every frame, then starts on its own.
        scene.Top.Animations.Clear();
        scene.Frames(5);
        Assert.NotNull(EffectiveHighlight(scene.Top));
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress > 0.0);
    }

    [Fact]
    public void ReplacingTheBorderBrushWithAUniformBrush_CancelsTheRun_AndReleasesTheContribution()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BorderBrush = BuildFrozenBrush();
        scene.Frames(10);
        Assert.NotNull(EffectiveHighlight(scene.Top));

        scene.Top.BorderBrush = MGUniformBorderBrush.Black;
        scene.Frames(2);

        Assert.IsType<MGUniformBorderBrush>(scene.Top.BorderBrush);
        Assert.False(scene.Top.Animations.IsAnimating("BorderBrush"));
    }

    [Fact]
    public void BaseSwapToAnotherHighlightBrush_WhileActive_IsPickedUpOnTheClone()
    {
        //  EnsureClone (BorderBrushHighlightProgressTarget) deliberately keeps reusing the same run-owned clone
        //  instance across ticks once one exists, so a base swap made while the run is active would otherwise be silently ignored -- the
        //  clone would keep drawing the FIRST base's configuration forever. SyncBorderHighlightRun instead copies the live base's configuration
        //  (everything except the run-owned AnimationProgress/IsEnabled) onto the clone every frame.
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BorderBrush = BuildFrozenBrush();
        scene.Frames(10);
        Assert.Equal(HighlightAnimation.Pulse, EffectiveHighlight(scene.Top).AnimationType);
        Assert.Equal(Color.White, EffectiveHighlight(scene.Top).HighlightColor);

        MGHighlightBorderBrush newBase = new(MGUniformBorderBrush.Black, Color.Blue, HighlightAnimation.Progress)
        {
            ProgressDuration = TimeSpan.FromSeconds(4.0),
        };
        newBase.Freeze();
        scene.Top.BorderBrush = newBase;
        scene.Frames(10);

        Assert.Equal(HighlightAnimation.Progress, EffectiveHighlight(scene.Top).AnimationType);
        Assert.Equal(Color.Blue, EffectiveHighlight(scene.Top).HighlightColor);
        //  The shared/frozen bases are never mutated by the host.
        Assert.Equal(HighlightAnimation.Progress, newBase.AnimationType);
        Assert.Equal(0.0, newBase.AnimationProgress, Tolerance);
    }

    [Fact]
    public void LiveHighlightColourEdit_WhileActive_IsPickedUpOnTheClone()
    {
        //  Every runtime edit of an unfrozen highlight base except IsEnabled reaches the clone even once a run is active.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGHighlightBorderBrush live = new(MGUniformBorderBrush.Black, Color.White, HighlightAnimation.Pulse)
        {
            PulseFadeDuration = TimeSpan.FromSeconds(1.0),
            PulseDelay = TimeSpan.Zero,
        };
        scene.Top.BorderBrush = live;
        scene.Frames(10);
        Assert.Equal(Color.White, EffectiveHighlight(scene.Top).HighlightColor);

        live.HighlightColor = Color.Lime;
        scene.Frames(5);

        Assert.Equal(Color.Lime, EffectiveHighlight(scene.Top).HighlightColor);
        Assert.True(EffectiveHighlight(scene.Top).AnimationProgress > 0.0);
    }

    [Fact]
    public void LivePulseDelayEdit_WhileActive_IsPickedUpOnTheClone_AndKeepsTheCycleConsistent()
    {
        //  The run's Duration follows a live PulseFadeDuration/PulseDelay edit, and configuration reaches the clone before the
        //  duration mismatch is evaluated, so the clone never draws with an internally inconsistent (pulse shape vs. cycle length) split.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGHighlightBorderBrush live = new(MGUniformBorderBrush.Black, Color.White, HighlightAnimation.Pulse)
        {
            PulseFadeDuration = TimeSpan.FromSeconds(1.0),
            PulseDelay = TimeSpan.Zero,
        };
        scene.Top.BorderBrush = live;
        scene.Frames(5);
        Assert.Equal(TimeSpan.Zero, EffectiveHighlight(scene.Top).PulseDelay);

        live.PulseDelay = TimeSpan.FromSeconds(3.0);
        scene.Frames(5);

        Assert.Equal(TimeSpan.FromSeconds(3.0), EffectiveHighlight(scene.Top).PulseDelay);
        Assert.Equal(TimeSpan.FromSeconds(3.0), live.PulseDelay);
    }

    [Fact]
    public void ElementWithoutAHighlightBorderBrush_NeverAllocatesAnAnimationSlot()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Frames(5);

        Assert.Null(scene.Top.AnimationSlotOrNull);
        Assert.Null(scene.Bottom.AnimationSlotOrNull);
    }
}
