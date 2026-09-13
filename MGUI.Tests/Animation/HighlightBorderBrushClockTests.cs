using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Animation;

/// <summary>Slice U10 of Docs/Tasks/animation-v3-tasks.md: <see cref="MGHighlightBorderBrush.Update(UpdateBaseArgs)"/> follows
/// <see cref="UpdateBaseArgs.AnimationDeltaTime"/> (set by <see cref="MGDesktop.Update"/> from <c>MGDesktop.Animations.Clock</c>,
/// scaled and zero while paused) instead of the wall-clock <see cref="UpdateBaseArgs.FrameElapsed"/>, falling back to
/// <see cref="UpdateBaseArgs.FrameElapsed"/> for a host that does not provide it.</summary>
public class HighlightBorderBrushClockTests
{
    private const double Tolerance = 1e-6;

    /// <summary>A brush with a one-second cycle (<see cref="MGHighlightBorderBrush.PulseFadeDuration"/> = 1s,
    /// <see cref="MGHighlightBorderBrush.PulseDelay"/> = 0), so <c>AnimationProgress</c> advances by exactly the elapsed
    /// fraction of a second: no <see cref="MGHighlightBorderBrush.Target"/>, so <see cref="MGHighlightBorderBrush.StopOnMouseOver"/>/
    /// <see cref="MGHighlightBorderBrush.StopOnClick"/> never disable it.</summary>
    private static MGHighlightBorderBrush BuildBrush()
    {
        MGHighlightBorderBrush brush = new(MGUniformBorderBrush.Black, Color.White, HighlightAnimation.Pulse)
        {
            PulseFadeDuration = TimeSpan.FromSeconds(1.0),
            PulseDelay = TimeSpan.Zero,
        };
        return brush;
    }

    private static UpdateBaseArgs BuildArgs(TimeSpan frameElapsed, TimeSpan? animationDeltaTime)
        => new(TimeSpan.Zero, frameElapsed, default, default) { AnimationDeltaTime = animationDeltaTime };

    [Fact]
    public void AnimationDeltaTime_Half_AdvancesByTheScaledAmount()
    {
        MGHighlightBorderBrush brush = BuildBrush();
        TimeSpan frameElapsed = TimeSpan.FromMilliseconds(200);
        UpdateBaseArgs ua = BuildArgs(frameElapsed, TimeSpan.FromMilliseconds(100)); // half of FrameElapsed

        ((IBorderBrush)brush).Update(ua);

        // 100ms over a 1s cycle = 0.1, not 0.2 (which is what FrameElapsed alone would have produced).
        Assert.Equal(0.1, brush.AnimationProgress, Tolerance);
    }

    [Fact]
    public void AnimationDeltaTime_Null_FallsBackToFrameElapsed()
    {
        MGHighlightBorderBrush brush = BuildBrush();
        UpdateBaseArgs ua = BuildArgs(TimeSpan.FromMilliseconds(200), null);

        ((IBorderBrush)brush).Update(ua);

        Assert.Equal(0.2, brush.AnimationProgress, Tolerance);
    }

    [Fact]
    public void AnimationDeltaTime_Zero_DoesNotAdvance()
    {
        MGHighlightBorderBrush brush = BuildBrush();
        UpdateBaseArgs ua = BuildArgs(TimeSpan.FromMilliseconds(200), TimeSpan.Zero); // paused clock

        ((IBorderBrush)brush).Update(ua);

        Assert.Equal(0.0, brush.AnimationProgress, Tolerance);
    }

    [Fact]
    public void ThroughARealDesktopFrame_TimeScaleHalvesTheProgress_AndPauseStopsIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGHighlightBorderBrush brush = BuildBrush();
        MGBorder border = new(scene.Window, new Thickness(1), (IBorderBrush)brush) { PreferredWidth = 40, PreferredHeight = 40 };
        scene.Panel.TryAddChild(border);
        scene.Frames(2);
        brush.AnimationProgress = 0.0;

        scene.Desktop.Animations.Clock.TimeScale = 0.5f;
        int oneRealSecond = 1000 / AnimationTestScene.FrameMilliseconds;
        scene.Frames(oneRealSecond);
        // Half the wall-clock rate: ~0.5 progress over a 1s cycle instead of ~1.0.
        Assert.Equal(0.5, brush.AnimationProgress, 0.05);

        double progressBeforePause = brush.AnimationProgress;
        scene.Desktop.Animations.Clock.IsPaused = true;
        scene.Frames(30);
        Assert.Equal(progressBeforePause, brush.AnimationProgress, Tolerance);
    }
}
