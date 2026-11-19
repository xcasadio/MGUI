using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit.Abstractions;

namespace MGUI.Tests.Architecture;

/// <summary>
/// T0.1 and T1.3 of the plan "end of the zero allocation of the drawing": what a frame of the desktop allocates for an <see cref="MGRatingControl"/> of the
/// <see cref="RatingItemShape.Triangle"/> shape (four items), fully filled and partially filled, with and without stroke, horizontal and vertical. The star and the diamond
/// (T5.6 of the previous plan) are not in this table: they are the shapes that T1.3 gives the triangle the way of.<para/>
/// Before T1.3 the triangle scaled its vertices into a new list at each draw of each item (<c>ShapeVertices256[Triangle].Select(...).ToList()</c>), took the bounds of its partial clip with
/// <c>Min</c> and <c>Max</c> lambdas and its stroke from <c>SelectConsecutivePairs(true)</c> (1792, 4096, 1712 and 3728 bytes per frame, horizontal and vertical alike); it now draws from the
/// buffer that the star and the diamond use, by index. The stroke is measured here: the bench draws through a <see cref="NonRecordingDrawTransaction"/> that
/// counts the calls without running a backend. The journal of the calls is frozen in <see cref="RatingTriangleJournalTests"/>.<para/>
/// Each case proves its path with one frame drawn into an <see cref="OrderedRecordingDrawTransaction"/>: the number of triangles filled (an item draws its unfilled triangle, then the filled part),
/// of line segments of the stroke (three per triangle that has a stroke), and of rectangle clips right before a triangle (the clip of the control itself, plus one for a partially filled item).
/// The totals asserted are 0 over <see cref="FrameAllocationMeter.MeasuredFrames"/> frames, for Update and for Draw.
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class RatingTriangleAllocationTests
{
    private const int Items = 4;

    private readonly ITestOutputHelper Output;

    public RatingTriangleAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static ControlBench CreateBench(Orientation orientation, float value, bool stroke, out MGRatingControl rating)
    {
        ControlBench bench = new();
        rating = new MGRatingControl(bench.Window, 0, Items, 24, false, null, RatingItemShape.Triangle) { Orientation = orientation };
        rating.UnfilledShapeStrokeThickness = stroke ? 2 : 0;
        rating.FilledShapeStrokeThickness = stroke ? 2 : 0;
        rating.SetValue(value);
        Assert.Equal(value, rating.Value);
        bench.Show(rating);
        return bench;
    }

    private static int Count(IReadOnlyList<string> lines, string prefix)
    {
        int count = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            count += lines[i].StartsWith(prefix, StringComparison.Ordinal) ? 1 : 0;
        }

        return count;
    }

    private static List<string> DrawJournal(ControlBench bench)
    {
        OrderedRecordingDrawTransaction transaction = new(bench.Runtime, DrawSettings.Default);
        bench.Update();
        bench.Desktop.Draw(transaction);
        return transaction.Lines.ToList();
    }

    [Theory]
    [InlineData(Orientation.Horizontal, 4.0f, false, 0L, 0L)]
    [InlineData(Orientation.Horizontal, 4.0f, true, 0L, 0L)]
    [InlineData(Orientation.Horizontal, 2.4f, false, 0L, 0L)]
    [InlineData(Orientation.Horizontal, 2.4f, true, 0L, 0L)]
    [InlineData(Orientation.Vertical, 4.0f, false, 0L, 0L)]
    [InlineData(Orientation.Vertical, 4.0f, true, 0L, 0L)]
    [InlineData(Orientation.Vertical, 2.4f, false, 0L, 0L)]
    [InlineData(Orientation.Vertical, 2.4f, true, 0L, 0L)]
    public void RatingTriangle_UpdateAndDraw_AllocateNothing(Orientation orientation, float value, bool stroke, long expectedUpdateTotal, long expectedDrawTotal)
    {
        ControlBench bench = CreateBench(orientation, value, stroke, out MGRatingControl rating);
        string label = $"MGRatingControl triangle, {orientation}, value {value}, {(stroke ? "with" : "without")} stroke";

        long callsBefore = bench.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(null, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, $"{label}: the measured frames drew nothing");
        Output.WriteLine($"{label}: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");

        //  The path: a window without the rating control draws no triangle and no line segment of its own that this count would mistake (the baseline is subtracted).
        ControlBench baselineBench = new();
        baselineBench.Show(new MGStackPanel(baselineBench.Window, Orientation.Vertical));
        List<string> baseline = DrawJournal(baselineBench);
        List<string> journal = DrawJournal(bench);
        bool partial = value % 1 != 0;
        int filledItems = (int)Math.Floor(value) + (partial ? 1 : 0);
        int triangles = Count(journal, "FillTriangle/7") - Count(baseline, "FillTriangle/7");
        int segments = Count(journal, "StrokeLineSegment/5") - Count(baseline, "StrokeLineSegment/5");
        int partialClips = 0;
        for (int i = 0; i + 1 < journal.Count; i++)
        {
            partialClips += journal[i].StartsWith("PushRectangleClip/2 ", StringComparison.Ordinal) && journal[i + 1].StartsWith("FillTriangle/7", StringComparison.Ordinal) ? 1 : 0;
        }

        Assert.Equal(Items + filledItems, triangles);
        Assert.Equal(stroke ? 3 * (Items + filledItems) : 0, segments);
        //  The clip of the control itself (ClipToBounds) is a rectangle clip right before its first triangle, and the partial item pushes one more.
        Assert.Equal(partial ? 2 : 1, partialClips);
        Assert.Equal(0, Count(journal, "FillPolygon/3") - Count(baseline, "FillPolygon/3"));
        Assert.Equal(0, Count(journal, "StrokeAndFillPolygon/5") - Count(baseline, "StrokeAndFillPolygon/5"));

        Assert.True(bytes.UpdateTotal == expectedUpdateTotal && bytes.DrawTotal == expectedDrawTotal,
            $"{label}: Update {bytes.UpdateTotal} B and Draw {bytes.DrawTotal} B over {bytes.Iterations} frames, expected {expectedUpdateTotal} and {expectedDrawTotal}");
    }
}
