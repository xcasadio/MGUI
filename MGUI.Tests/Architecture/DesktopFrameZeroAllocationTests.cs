using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit.Abstractions;
using Fixture = MGUI.Tests.Architecture.DesktopFrameAllocationMeasurements.Fixture;
using Harness = MGUI.Tests.Architecture.DesktopFrameAllocationMeasurements.Harness;

namespace MGUI.Tests.Architecture;

/// <summary>
/// The fixtures of <see cref="DesktopFrameAllocationMeasurements"/> that a frame of the desktop (<c>MGDesktop.Update()</c> then
/// <c>MGDesktop.Draw(IUIDrawTransaction)</c> into a <see cref="NonRecordingDrawTransaction"/>) allocates nothing for, one test per state and per method.
/// The transaction does not track the clip: the cost of the clip scopes of a real backend is not part of this proof (see its remarks).
/// </summary>
public class DesktopFrameZeroAllocationTests
{
    private static readonly Point Outside = new(600, 340);
    private static readonly Point MovingA = new(100, 100);
    private static readonly Point MovingB = new(120, 110);

    private readonly ITestOutputHelper Output;

    public DesktopFrameZeroAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private long MeasureUpdate(Fixture fixture, params Point[] positions) => MeasureUpdate(Harness.Create(fixture), fixture, positions);

    private long MeasureUpdate(Harness harness, Fixture fixture, Point[] positions)
    {
        int frame = 0;
        long bytes = DesktopFrameAllocationMeasurements.Measure(() => harness.Update(positions[frame++ % positions.Length], ButtonState.Released));
        Output.WriteLine($"{fixture} Update over {positions.Length} position(s): {bytes} B/frame");
        return bytes;
    }

    /// <summary>The bytes of the draw alone: each frame updates (not counted), then the allocation counter is read around the draw.</summary>
    private long MeasureDraw(Fixture fixture, params Point[] positions) => MeasureDraw(Harness.Create(fixture), fixture, positions);

    private long MeasureDraw(Harness harness, Fixture fixture, Point[] positions)
    {
        int frame = 0;
        for (int i = 0; i < 500; i++)
        {
            harness.Update(positions[frame++ % positions.Length], ButtonState.Released);
            harness.Draw();
        }

        long total = 0;
        AllocationWindow.Start();
        for (int i = 0; i < 500; i++)
        {
            harness.Update(positions[frame++ % positions.Length], ButtonState.Released);
            long before = GC.GetAllocatedBytesForCurrentThread();
            harness.Draw();
            total += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        long bytes = total / 500;
        Output.WriteLine($"{fixture} Draw over {positions.Length} position(s): {bytes} B/frame, {harness.Transaction.CallCount} calls received");
        return bytes;
    }

    //  ---- T1.2: the desktop without a window of the caller (its overlay window and nothing else) -----------------------------------------

    [Fact]
    public void NoWindow_Update_WithTheMouseStill_AllocatesNothing() => Assert.Equal(0, MeasureUpdate(Fixture.NoWindow, Outside));

    [Fact]
    public void NoWindow_Update_WithTheMouseMoving_AllocatesNothing() => Assert.Equal(0, MeasureUpdate(Fixture.NoWindow, MovingA, MovingB));

    [Fact]
    public void NoWindow_Draw_WithTheMouseStill_AllocatesNothing() => Assert.Equal(0, MeasureDraw(Fixture.NoWindow, Outside));

    [Fact]
    public void NoWindow_Draw_WithTheMouseMoving_AllocatesNothing() => Assert.Equal(0, MeasureDraw(Fixture.NoWindow, MovingA, MovingB));

    //  ---- T1.3: a bare window (no title bar, no content) ---------------------------------------------------------------------------------

    private static readonly Point OverTheWindow = new(150, 100);

    [Fact]
    public void BareWindow_Update_WithTheMouseOutside_AllocatesNothing() => Assert.Equal(0, MeasureUpdate(Fixture.BareWindow, Outside));

    [Fact]
    public void BareWindow_Update_WithTheMouseStillOverTheWindow_AllocatesNothing() => Assert.Equal(0, MeasureUpdate(Fixture.BareWindow, OverTheWindow));

    [Fact]
    public void BareWindow_Draw_WithTheMouseOutside_AllocatesNothing() => Assert.Equal(0, MeasureDraw(Fixture.BareWindow, Outside));

    [Fact]
    public void BareWindow_Draw_WithTheMouseStillOverTheWindow_AllocatesNothing() => Assert.Equal(0, MeasureDraw(Fixture.BareWindow, OverTheWindow));

    //  ---- T1.4: the default window, its title bar and its close button (the hovered positions are found by hit-test) -----------------------

    private (Harness Harness, Point Position) DefaultWindowAt(string place)
    {
        Harness harness = Harness.Create(Fixture.DefaultWindow);
        (Point title, Point close) = harness.FindTitleBarPoints();
        return (harness, place == "title" ? title : place == "close" ? close : Outside);
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("title")]
    [InlineData("close")]
    public void DefaultWindow_Update_AllocatesNothing(string place)
    {
        (Harness harness, Point position) = DefaultWindowAt(place);
        Assert.Equal(0, MeasureUpdate(harness, Fixture.DefaultWindow, new[] { position }));
    }

    [Theory]
    [InlineData("outside")]
    [InlineData("title")]
    [InlineData("close")]
    public void DefaultWindow_Draw_AllocatesNothing(string place)
    {
        (Harness harness, Point position) = DefaultWindowAt(place);
        Assert.Equal(0, MeasureDraw(harness, Fixture.DefaultWindow, new[] { position }));
    }

    //  ---- T1.5: the slider and the containers that hold it, with the mouse still and moving over the window --------------------------------

    private static readonly Point OverTheSlider = new(150, 100);

    [Theory]
    [InlineData(Fixture.WindowWithStackPanel)]
    [InlineData(Fixture.WindowWithStackPanelAndTextBlock)]
    [InlineData(Fixture.WindowWithStackPanelAndSlider)]
    [InlineData(Fixture.Full)]
    [InlineData(Fixture.FullInGrid)]
    [InlineData(Fixture.FullInDockPanel)]
    [InlineData(Fixture.FullInDockHost)]
    public void TheContainersOfTheSlider_Update_AllocatesNothing(Fixture fixture)
    {
        Assert.Equal(0, MeasureUpdate(fixture, Outside));
        Assert.Equal(0, MeasureUpdate(fixture, OverTheSlider));
        Assert.Equal(0, MeasureUpdate(fixture, MovingA, MovingB));
    }

    [Theory]
    [InlineData(Fixture.WindowWithStackPanel)]
    [InlineData(Fixture.WindowWithStackPanelAndTextBlock)]
    [InlineData(Fixture.WindowWithStackPanelAndSlider)]
    [InlineData(Fixture.Full)]
    [InlineData(Fixture.FullInGrid)]
    [InlineData(Fixture.FullInDockPanel)]
    [InlineData(Fixture.FullInDockHost)]
    public void TheContainersOfTheSlider_Draw_AllocatesNothing(Fixture fixture)
    {
        Assert.Equal(0, MeasureDraw(fixture, Outside));
        Assert.Equal(0, MeasureDraw(fixture, OverTheSlider));
        Assert.Equal(0, MeasureDraw(fixture, MovingA, MovingB));
    }
}
