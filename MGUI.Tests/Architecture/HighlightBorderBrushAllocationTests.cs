using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using MonoGame.Extended;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit.Abstractions;

namespace MGUI.Tests.Architecture;

/// <summary>
/// T0.1 of the plan "end of the zero allocation of the drawing", for T1.4: what <see cref="MGHighlightBorderBrush.Draw(ElementDrawArgs, MGElement, Rectangle, Thickness)"/> and
/// <see cref="MGHighlightBorderBrush.Draw(ElementDrawArgs, MGElement, MGBoxShape, MGBoxGeometry)"/> allocate for each of the four animations, at six animation instants. The brush is drawn
/// directly (<see cref="FrameAllocationMeter"/> with a draw step only): the animation engine that advances <see cref="MGHighlightBorderBrush.AnimationProgress"/> in an element is not what is
/// measured, the draw at a given progress is. The instant is set before the measure (the setter notifies, and is not part of the draw).<para/>
/// Three geometries, each with edges of different thicknesses (4, 6, 8, 10):<list type="bullet">
/// <item><c>bounds</c>: the rectangle overload, called directly.</item>
/// <item><c>square</c>: the contract overload on a box with square corners (<see cref="MGBoxGeometry.UsesRectangleFastPath"/>): <c>Progress</c> and <c>Scan</c> forward to the rectangle overload.</item>
/// <item><c>ring</c>: the contract overload on a box with rounded corners whose geometry has a ring mesh (<see cref="MGBoxGeometry.HasBorderRingMesh"/>): <c>Progress</c> and <c>Scan</c> take
/// <c>DrawProgressOnRing</c> and <c>DrawScanOnRing</c>.</item></list>
/// Each case proves the path with a draw into an <see cref="OrderedRecordingDrawTransaction"/> at every instant: the rectangle path of <c>Progress</c> fills one polygon, the rectangle path of
/// <c>Scan</c> fills rectangles, the ring paths fill triangles and nothing else; <c>Pulse</c> and <c>Flash</c> draw only while their phase says so.<para/>
/// Before T1.4 <c>Pulse</c> and <c>Flash</c> measured 0, <c>Progress</c> on the rectangle path 2248 to 2472 bytes per draw, <c>Scan</c> on the rectangle path 240, <c>Progress</c> on the ring 488 and
/// <c>Scan</c> on the ring 448. Every row now asserts 0 over <see cref="FrameAllocationMeter.MeasuredFrames"/> draws at each instant, for four boxes (the one above, equal thicknesses, a zero thickness side, a small box).
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class HighlightBorderBrushAllocationTests
{
    private static readonly double[] Instants = { 0.0, 0.1, 0.3, 0.55, 0.8, 0.95 };
    private static readonly Rectangle Bounds = new(10, 20, 100, 60);
    private static readonly Thickness BorderThickness = new(4, 6, 8, 10);

    private readonly ITestOutputHelper Output;

    public HighlightBorderBrushAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static MGHighlightBorderBrush CreateBrush(HighlightAnimation animation, string option)
    {
        MGHighlightBorderBrush brush = new(null, Color.Red, animation);
        switch (option)
        {
            case "default":
            case "clockwise":
                break;
            case "counter-clockwise":
                brush.ProgressFlowDirection = HighlightFlowDirection.CounterClockwise;
                break;
            case "horizontal":
                brush.ScanOrientation = Orientation.Horizontal;
                break;
            case "horizontal reversed":
                brush.ScanOrientation = Orientation.Horizontal;
                brush.ScanIsReversed = true;
                break;
            case "vertical":
                brush.ScanOrientation = Orientation.Vertical;
                break;
            case "vertical reversed":
                brush.ScanOrientation = Orientation.Vertical;
                brush.ScanIsReversed = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(option), option, null);
        }

        return brush;
    }

    private static ElementDrawArgs Args(IUIDrawTransaction transaction)
        => new(new DrawBaseArgs(TimeSpan.Zero, transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);

    private static int Count(IReadOnlyList<string> lines, string prefix)
    {
        int count = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            count += lines[i].StartsWith(prefix, StringComparison.Ordinal) ? 1 : 0;
        }

        return count;
    }

    /// <summary>Whether the animation draws something at the instant: <c>Pulse</c> fades during the first 2.0 s of its 2.2 s cycle, <c>Flash</c> shows during the first half of its cycle.</summary>
    private static bool IsDrawnAt(HighlightAnimation animation, double instant) => animation switch
    {
        HighlightAnimation.Pulse => instant < MGHighlightBorderBrush.DefaultPulseFadeDuration / (MGHighlightBorderBrush.DefaultPulseFadeDuration + MGHighlightBorderBrush.DefaultPulseDelay),
        HighlightAnimation.Flash => instant <= MGHighlightBorderBrush.DefaultFlashShowDuration / (MGHighlightBorderBrush.DefaultFlashShowDuration + MGHighlightBorderBrush.DefaultFlashHideDuration),
        _ => true
    };

    private static readonly (string Name, Rectangle Bounds, Thickness Thickness, int Radius)[] Boxes =
    {
        ("default", Bounds, BorderThickness, 12),
        ("equal thickness", new Rectangle(0, 0, 90, 50), new Thickness(3), 10),
        ("zero side", new Rectangle(5, 5, 64, 64), new Thickness(0, 5, 2, 0), 8),
        ("small box", new Rectangle(3, 4, 20, 20), new Thickness(2), 8)
    };

    public static IEnumerable<object[]> Rows()
    {
        foreach ((string boxName, _, _, _) in Boxes)
        {
            foreach (string geometryName in new[] { "bounds", "square", "ring" })
            {
                yield return new object[] { boxName, geometryName, HighlightAnimation.Pulse, "default" };
                yield return new object[] { boxName, geometryName, HighlightAnimation.Flash, "default" };
                foreach (string option in new[] { "clockwise", "counter-clockwise" })
                {
                    yield return new object[] { boxName, geometryName, HighlightAnimation.Progress, option };
                }

                foreach (string option in new[] { "horizontal", "horizontal reversed", "vertical", "vertical reversed" })
                {
                    yield return new object[] { boxName, geometryName, HighlightAnimation.Scan, option };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(Rows))]
    public void HighlightBorderBrush_Draw_AllocatesNothingAtEachInstant(string boxName, string geometryName, HighlightAnimation animation, string option)
    {
        (_, Rectangle bounds, Thickness borderThickness, int radius) = Boxes.Single(x => x.Name == boxName);
        MGBoxShape shape = new(bounds, borderThickness, new MGCornerRadius(geometryName == "ring" ? radius : 0));
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        switch (geometryName)
        {
            case "square":
                Assert.True(geometry.UsesRectangleFastPath);
                break;
            case "ring":
                Assert.False(geometry.UsesRectangleFastPath);
                Assert.True(geometry.HasBorderRingMesh);
                break;
        }

        MGHighlightBorderBrush brush = CreateBrush(animation, option);
        NonRecordingDrawTransaction measured = new(new GraphTestRuntime(new Rectangle(0, 0, 640, 360)), DrawSettings.Default);
        ElementDrawArgs measuredArgs = Args(measured);
        Action draw = geometryName == "bounds"
            ? () => brush.Draw(measuredArgs, null, bounds, borderThickness)
            : () => brush.Draw(measuredArgs, null, shape, geometry);

        long[] totals = new long[Instants.Length];
        List<string> failures = new();
        for (int i = 0; i < Instants.Length; i++)
        {
            brush.AnimationProgress = Instants[i];
            long callsBefore = measured.CallCount;
            FrameBytes bytes = FrameAllocationMeter.Measure(null, static () => { }, draw);
            totals[i] = bytes.DrawTotal;
            bool drawn = IsDrawnAt(animation, Instants[i]);
            Assert.Equal(drawn, measured.CallCount > callsBefore);

            //  The path, from one draw into a recording transaction.
            OrderedRecordingDrawTransaction recording = new(new GraphTestRuntime(new Rectangle(0, 0, 640, 360)), DrawSettings.Default);
            if (geometryName == "bounds")
            {
                brush.Draw(Args(recording), null, bounds, borderThickness);
            }
            else
            {
                brush.Draw(Args(recording), null, shape, geometry);
            }

            IReadOnlyList<string> journal = recording.Lines;
            int triangles = Count(journal, "FillTriangle/7");
            int polygons = Count(journal, "FillPolygon/3");
            int rectangles = Count(journal, "FillRectangle/3");
            Assert.Equal(drawn, journal.Count > 0);
            string counts = $"{triangles} triangle(s), {polygons} polygon(s), {rectangles} rectangle(s)";
            if (animation == HighlightAnimation.Progress)
            {
                Assert.True(geometryName == "ring" ? triangles > 0 && polygons == 0 && rectangles == 0 : polygons == 1 && triangles == 0 && rectangles == 0,
                    $"Progress on {geometryName} at {Instants[i]}: {counts}");
            }
            else if (animation == HighlightAnimation.Scan)
            {
                Assert.True(geometryName == "ring" ? triangles > 0 && polygons == 0 && rectangles == 0 : rectangles > 0 && triangles == 0 && polygons == 0,
                    $"Scan on {geometryName} at {Instants[i]}: {counts}");
            }

            Output.WriteLine($"{boxName}, {geometryName}, {animation}, {option}, progress {Instants[i]}: {bytes.Draw} B/draw ({bytes.DrawTotal} B over {bytes.Iterations} draws); journal {journal.Count} line(s): {counts}");
            if (bytes.DrawTotal != 0)
            {
                failures.Add($"progress {Instants[i]}: {bytes.DrawTotal} B over {bytes.Iterations} draws, expected 0");
            }
        }

        Assert.True(failures.Count == 0, $"{boxName}, {geometryName}, {animation}, {option}: measured {string.Join(", ", totals)}; {string.Join("; ", failures)}");
    }
}
