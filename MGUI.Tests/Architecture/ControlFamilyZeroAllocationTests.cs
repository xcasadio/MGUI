using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit.Abstractions;

namespace MGUI.Tests.Architecture;

/// <summary>
/// The control families of <see cref="ControlFamilyAllocationMeasurements"/> that a frame of the desktop (<c>MGDesktop.Update()</c> then <c>MGDesktop.Draw(IUIDrawTransaction)</c>
/// into a <see cref="NonRecordingDrawTransaction"/>) allocates nothing for: exact total of 0 bytes over <see cref="FrameAllocationMeter.MeasuredFrames"/> frames after
/// <see cref="FrameAllocationMeter.WarmupFrames"/> warm-up frames. They run in the collection of the measurements: a measurement counts the bytes of its own thread
/// (see <see cref="AllocationMeasurementsCollection"/>).
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class ControlFamilyZeroAllocationTests
{
    private readonly ITestOutputHelper Output;

    public ControlFamilyZeroAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    // ---- MGScrollViewer, content taller than the viewport ----

    /// <summary>A scroll viewer that fills the window, over a vertical stack of 100 text blocks: its content is much taller than its viewport.</summary>
    private static MGScrollViewer CreateTallScrollViewer(ControlBench bench)
    {
        MGScrollViewer scrollViewer = new(bench.Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        MGStackPanel panel = new(bench.Window, Orientation.Vertical);
        for (int i = 0; i < 100; i++)
        {
            Assert.True(panel.TryAddChild(new MGTextBlock(bench.Window, $"Row {i}")));
        }

        scrollViewer.SetContent(panel);
        bench.Show(scrollViewer);
        Assert.True(scrollViewer.MaxVerticalOffset > 0f);
        return scrollViewer;
    }

    /// <summary>The wheel turns up for four frames, then down for four frames, one notch per frame: the offset moves every frame and never rests on a limit.</summary>
    private static Action<int> StartWheel(ControlBench bench, MGScrollViewer scrollViewer)
    {
        bench.Mouse = scrollViewer.LayoutBounds.Center;
        for (int i = 0; i < 6; i++)
        {
            bench.Wheel -= 120;
            bench.Frame();
        }

        return frame => bench.Wheel += (frame / 4) % 2 == 0 ? 120 : -120;
    }

    private FrameBytes MeasureScrollViewerAtRest()
    {
        ControlBench bench = new();
        MGScrollViewer scrollViewer = CreateTallScrollViewer(bench);
        long callsBefore = bench.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(null, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, "the measured frames drew nothing");
        Output.WriteLine($"MGScrollViewer at rest: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    private FrameBytes MeasureScrollViewerWithTheWheel()
    {
        ControlBench bench = new();
        MGScrollViewer scrollViewer = CreateTallScrollViewer(bench);
        Action<int> wheel = StartWheel(bench, scrollViewer);
        long callsBefore = bench.Transaction.CallCount;
        float minOffset = float.MaxValue;
        float maxOffset = float.MinValue;
        FrameBytes bytes = FrameAllocationMeter.Measure(frame =>
        {
            wheel(frame);
            minOffset = Math.Min(minOffset, scrollViewer.VerticalOffset);
            maxOffset = Math.Max(maxOffset, scrollViewer.VerticalOffset);
        }, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, "the measured frames drew nothing");

        //  The offset read before each update is the one of the previous frame: it moved, and stayed between the limits.
        Assert.True(maxOffset > minOffset, "the wheel does not move the content");
        Assert.True(minOffset > 0f && maxOffset < scrollViewer.MaxVerticalOffset, "the offset reached a limit");
        Output.WriteLine($"MGScrollViewer with the wheel: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    [Fact]
    public void ScrollViewerWithTallContent_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureScrollViewerAtRest().UpdateTotal);

    [Fact]
    public void ScrollViewerWithTallContent_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureScrollViewerAtRest().DrawTotal);

    /// <summary>The update of a frame whose wheel moved allocates the one <see cref="BaseMouseScrolledEventArgs"/> that <c>MouseTracker.Update</c> creates for the wheel, and nothing else:
    /// that object lives in MGUI.Shared (public class of the input, whose instance a handler may keep), outside the files of the scroll viewer work. When the tracker reuses
    /// it, as it does for the move event, the expected value below becomes 0 and this test becomes the zero test of the wheel update.</summary>
    [Fact]
    public void ScrollViewerWithTallContent_Update_WithTheMouseWheel_AllocatesOnlyTheScrollEventArgs()
    {
        MouseTracker tracker = new InputTracker().Mouse;
        GC.KeepAlive(new BaseMouseScrolledEventArgs(tracker, 120, Point.Zero));
        AllocationWindow.Start();
        long before = GC.GetAllocatedBytesForCurrentThread();
        BaseMouseScrolledEventArgs scrolled = new(tracker, 120, Point.Zero);
        long scrolledEventBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(scrolled);
        Assert.True(scrolledEventBytes > 0);

        //  Every measured frame moves the wheel (MeasureScrollViewerWithTheWheel), so every one creates one scroll event.
        FrameBytes bytes = MeasureScrollViewerWithTheWheel();
        Assert.Equal(scrolledEventBytes * bytes.Iterations, bytes.UpdateTotal);
    }

    [Fact]
    public void ScrollViewerWithTallContent_Draw_WithTheMouseWheel_AllocatesNothing() => Assert.Equal(0, MeasureScrollViewerWithTheWheel().DrawTotal);

    /// <summary>The draw journal of the scroll viewer (every call of the transaction, with the clips and their names): one frame at rest, then four notches down,
    /// one notch up and two frames at rest. Frozen on the code before the allocation-free work, kept after it.</summary>
    [Fact]
    public void ScrollViewerWithTallContent_DrawJournal_AtRestAndAfterWheelNotches()
    {
        ControlBench bench = new();
        MGScrollViewer scrollViewer = CreateTallScrollViewer(bench);
        OrderedRecordingDrawTransaction transaction = new(bench.Runtime, DrawSettings.Default);

        List<string> journal = new();
        void Record(string label)
        {
            transaction.ClearJournal();
            bench.Update();
            bench.Desktop.Draw(transaction);
            journal.Add($"== {label} offset={scrollViewer.VerticalOffset} max={scrollViewer.MaxVerticalOffset}");
            journal.AddRange(transaction.Lines);
        }

        Record("rest");
        bench.Mouse = scrollViewer.LayoutBounds.Center;
        for (int i = 0; i < 4; i++)
        {
            bench.Wheel -= 120;
            Record($"notch down {i}");
        }

        bench.Wheel += 120;
        Record("notch up");
        Record("rest after the wheel");
        Record("rest after the wheel, again");

        FrozenJournal.Assert("scroll viewer draw journal", 2912, "DDC79E6AEAE75E01", journal);
    }
}
