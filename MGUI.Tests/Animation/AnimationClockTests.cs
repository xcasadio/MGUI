using MGUI.Core.UI.Animation;

namespace MGUI.Tests.Animation;

public class AnimationClockTests
{
    [Fact]
    public void Advance_AccumulatesScaledTime()
    {
        UIAnimationClock clock = new();

        clock.Advance(TimeSpan.FromMilliseconds(16));
        clock.Advance(TimeSpan.FromMilliseconds(16));

        Assert.Equal(TimeSpan.FromMilliseconds(16), clock.DeltaTime);
        Assert.Equal(TimeSpan.FromMilliseconds(32), clock.Time);
    }

    [Fact]
    public void TimeScale_ScalesTheDelta()
    {
        UIAnimationClock clock = new() { TimeScale = 0.5f };

        clock.Advance(TimeSpan.FromMilliseconds(100));

        Assert.Equal(TimeSpan.FromMilliseconds(50), clock.DeltaTime);
        Assert.Equal(TimeSpan.FromMilliseconds(50), clock.Time);
    }

    [Fact]
    public void Pause_ProducesAZeroDelta_AndKeepsTime()
    {
        UIAnimationClock clock = new();
        clock.Advance(TimeSpan.FromMilliseconds(16));

        clock.IsPaused = true;
        clock.Advance(TimeSpan.FromMilliseconds(16));

        Assert.Equal(TimeSpan.Zero, clock.DeltaTime);
        Assert.Equal(TimeSpan.FromMilliseconds(16), clock.Time);
    }

    [Fact]
    public void Advance_ClampsNegativeFrames()
    {
        UIAnimationClock clock = new();

        clock.Advance(TimeSpan.FromMilliseconds(-16));

        Assert.Equal(TimeSpan.Zero, clock.DeltaTime);
        Assert.Equal(TimeSpan.Zero, clock.Time);
    }

    [Fact]
    public void TimeScale_RejectsNegativeAndNaN()
    {
        UIAnimationClock clock = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.TimeScale = -1f);
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.TimeScale = float.NaN);
        clock.TimeScale = 0f;
        clock.Advance(TimeSpan.FromMilliseconds(16));
        Assert.Equal(TimeSpan.Zero, clock.DeltaTime);
    }
}
