using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit.Abstractions;

namespace MGUI.Tests.Architecture;

/// <summary>
/// T0.1 of the plan "end of the zero allocation of the drawing": what a frame of the desktop allocates for <see cref="MGEllipse"/>, <see cref="MGPolygon"/> and <see cref="MGPathLite"/>,
/// in the cases that reach the code the plan tightens (T1.1 and T1.2). Each case is measured as the other control families are: a desktop with one window and one shape, driven frame by frame
/// (<see cref="ControlBench"/>), <see cref="FrameAllocationMeter.WarmupFrames"/> warm-up frames then <see cref="FrameAllocationMeter.MeasuredFrames"/> measured ones, the update and the draw told apart.
/// <para/>
/// The numbers asserted are the remainder as it is measured now (on the code before T1.1 and T1.2): the task that removes a remainder replaces its number by 0 here. The bench draws
/// through a <see cref="NonRecordingDrawTransaction"/>, which allocates nothing and does not run a backend, so what is measured is what <c>MGUI.Core</c> allocates before the calls to the
/// transaction: the contour of a polygon (<c>StrokePolygon</c>) is measured in the backends instead (<c>StrokePolygonGpuTests</c> of both test projects), and the geometry of the clips
/// is measured here (the definitions are built, then handed to a transaction that ignores them).<para/>
/// Each case proves that it ran the path it is named for: after the measurement, one frame is drawn into an <see cref="OrderedRecordingDrawTransaction"/> and the journal must hold the calls the
/// case expects (the geometry clip of the fill brush, the geometry clip of the shape itself, the polygon calls), in the number the case expects.
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class ShapeDrawAllocationTests
{
    private readonly ITestOutputHelper Output;

    public ShapeDrawAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private const string SolidFillAndStroke = "solid fill and stroke";
    private const string SolidFillOnly = "solid fill only";
    private const string StrokeOnly = "stroke only";
    private const string BrushFill = "brush fill";
    private const string SelfClip = "self clip";
    private const string BrushFillAndSelfClip = "brush fill and self clip";
    private const string BrushFillAndSelfClipOpacityAnimated = "brush fill and self clip, opacity animated";
    private const string TwoFiguresBrushFill = "two closed figures, brush fill";

    //  A seven-vertex arrow (concave), the polygon of the polygon and of the figure of the path.
    private static Vector2[] Arrow() => new Vector2[] { new(0, 10), new(40, 10), new(40, 0), new(70, 20), new(40, 40), new(40, 30), new(0, 30) };

    private static MGPathLiteCommand[] ArrowCommands()
    {
        Vector2[] arrow = Arrow();
        List<MGPathLiteCommand> commands = new() { MGPathLiteCommand.MoveTo(arrow[0]) };
        for (int i = 1; i < arrow.Length; i++)
        {
            commands.Add(MGPathLiteCommand.LineTo(arrow[i]));
        }

        commands.Add(MGPathLiteCommand.Close());
        return commands.ToArray();
    }

    /// <summary>Two closed triangles, side by side: two figures that a brush fills one clip each.</summary>
    private static MGPathLiteCommand[] TwoTrianglesCommands() => new[]
    {
        MGPathLiteCommand.MoveTo(new Vector2(0, 0)), MGPathLiteCommand.LineTo(new Vector2(40, 0)), MGPathLiteCommand.LineTo(new Vector2(20, 40)), MGPathLiteCommand.Close(),
        MGPathLiteCommand.MoveTo(new Vector2(60, 0)), MGPathLiteCommand.LineTo(new Vector2(100, 0)), MGPathLiteCommand.LineTo(new Vector2(80, 40)), MGPathLiteCommand.Close(),
    };

    /// <summary>A brush that is not a solid one: <c>HasVisibleFill</c> is true and <c>TryGetSolidFillColor</c> false, which is what makes a shape draw its fill through the clip of its geometry.</summary>
    private static MGGradientFillBrush Gradient() => new(Color.Red, Color.Green, Color.Blue, Color.Yellow);

    private sealed record Scene(ControlBench Bench, MGShapeElementBase Shape, Action<int> Prepare, int FilledFigures, bool HasBrushFill, bool HasSelfClip, string Fill, string Contour);

    private static Scene CreateScene(string shapeName, string caseName)
    {
        ControlBench bench = new();
        MGShapeElementBase shape = shapeName switch
        {
            nameof(MGEllipse) => new MGEllipse(bench.Window, 120, 80),
            nameof(MGPolygon) => new MGPolygon(bench.Window, Arrow(), Color.White, 1f, Color.Transparent),
            nameof(MGPathLite) => new MGPathLite(bench.Window, caseName == TwoFiguresBrushFill ? TwoTrianglesCommands() : ArrowCommands(), Color.White, 1f, Color.Transparent),
            _ => throw new ArgumentOutOfRangeException(nameof(shapeName), shapeName, null)
        };

        bool brush = caseName is BrushFill or BrushFillAndSelfClip or BrushFillAndSelfClipOpacityAnimated or TwoFiguresBrushFill;
        bool selfClip = caseName is SelfClip or BrushFillAndSelfClip or BrushFillAndSelfClipOpacityAnimated;
        bool stroke = caseName is SolidFillAndStroke or StrokeOnly or SelfClip;
        bool solidFill = caseName is SolidFillAndStroke or SolidFillOnly or SelfClip;

        shape.Stroke = stroke ? Color.White : Color.Transparent;
        shape.StrokeThickness = 2f;
        if (brush)
        {
            shape.FillBrush = Gradient();
        }
        else
        {
            shape.Fill = solidFill ? Color.Red : Color.Transparent;
        }

        shape.ClipToBounds = selfClip;
        Action<int> prepare = null;
        if (caseName == BrushFillAndSelfClipOpacityAnimated)
        {
            //  The opacity moves every frame and the shape does not: the colours change, the geometry and the bounds do not.
            prepare = frame => shape.Opacity = 0.5f + (frame % 8) * 0.0625f;
        }

        bench.Show(shape);
        int filledFigures = caseName == TwoFiguresBrushFill ? 2 : 1;
        string fill = brush ? "brush" : solidFill ? "solid" : "none";
        string contour = solidFill && stroke ? "polygon" : solidFill ? "fill" : stroke ? "lines" : "none";
        return new Scene(bench, shape, prepare, filledFigures, brush, selfClip, fill, contour);
    }

    private static int Count(IReadOnlyList<string> lines, string prefix, string contains = null)
    {
        int count = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].StartsWith(prefix, StringComparison.Ordinal) && (contains == null || lines[i].Contains(contains, StringComparison.Ordinal)))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>One frame into a recording transaction: the journal proves which path the case runs.</summary>
    private static List<string> DrawJournal(ControlBench bench)
    {
        OrderedRecordingDrawTransaction transaction = new(bench.Runtime, DrawSettings.Default);
        bench.Update();
        bench.Desktop.Draw(transaction);
        return transaction.Lines.ToList();
    }

    /// <summary>The journal of the same window without the shape: what the window itself draws (its border lines), which the journal of a case contains too.</summary>
    private static List<string> WindowOnlyJournal()
    {
        ControlBench bench = new();
        bench.Show(new MGStackPanel(bench.Window, Orientation.Vertical));
        return DrawJournal(bench);
    }

    private static void AssertThePathThatTheCaseNamesRan(Scene scene, string name)
    {
        List<string> journal = DrawJournal(scene.Bench);
        List<string> window = WindowOnlyJournal();
        string elementType = scene.Shape.ElementType.ToString();
        int fillClips = Count(journal, "PushClipTemporary/1", $"name={elementType}.Fill)");
        int selfClips = Count(journal, "PushClipTemporary/1", $"name={elementType}.Self)");
        int arbitraryClips = Count(journal, "PushClipTemporary/1", "clip(ArbitraryGeometry,");

        //  The geometry clip of the fill brush: one per closed figure that the brush fills, and only for a brush fill.
        Assert.True(fillClips == (scene.HasBrushFill ? scene.FilledFigures : 0), $"{name}: {fillClips} geometry clip(s) of the fill, expected {(scene.HasBrushFill ? scene.FilledFigures : 0)}\n{string.Join('\n', journal)}");

        //  The geometry clip of the shape itself (the .Self hook of ClipToBounds): one, and only when ClipToBounds is set on a shape whose clip is a geometry.
        Assert.True(selfClips == (scene.HasSelfClip ? 1 : 0), $"{name}: {selfClips} geometry clip(s) of the shape itself, expected {(scene.HasSelfClip ? 1 : 0)}\n{string.Join('\n', journal)}");
        Assert.Equal(fillClips + selfClips, arbitraryClips);
        Assert.Equal(fillClips + selfClips, Count(journal, "PushClipTemporary/1", ",geometry="));

        //  The polygon calls of the case.
        int strokeAndFill = Count(journal, "StrokeAndFillPolygon/5") - Count(window, "StrokeAndFillPolygon/5");
        int fillPolygon = Count(journal, "FillPolygon/3") - Count(window, "FillPolygon/3");
        int lines = Count(journal, "StrokeLineSegment/5") - Count(window, "StrokeLineSegment/5");
        int quadrilaterals = Count(journal, "FillQuadrilateralLinearClamp/9") - Count(window, "FillQuadrilateralLinearClamp/9");
        switch (scene.Contour)
        {
            case "polygon":
                Assert.Equal(scene.FilledFigures, strokeAndFill);
                Assert.Equal(0, fillPolygon);
                Assert.Equal(0, lines);
                break;
            case "fill":
                Assert.Equal(0, strokeAndFill);
                Assert.Equal(scene.FilledFigures, fillPolygon);
                Assert.Equal(0, lines);
                break;
            case "lines":
                Assert.Equal(0, strokeAndFill);
                Assert.Equal(0, fillPolygon);
                Assert.True(lines > 0, $"{name}: the stroke only draws no line");
                break;
            default:
                Assert.Equal(0, strokeAndFill);
                Assert.Equal(0, fillPolygon);
                Assert.Equal(0, lines);
                break;
        }

        //  The brush of a brush fill is drawn once per filled figure (a gradient is one quadrilateral).
        Assert.Equal(scene.HasBrushFill ? scene.FilledFigures : 0, quadrilaterals);
    }

    private FrameBytes Measure(string label, Scene scene)
    {
        ControlBench bench = scene.Bench;
        long callsBefore = bench.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(scene.Prepare, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, $"{label}: the measured frames drew nothing");
        Output.WriteLine($"{label}: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames; prepare {bytes.PrepareTotal} B)");
        return bytes;
    }

    /// <summary>The totals over <see cref="FrameAllocationMeter.MeasuredFrames"/> frames, as measured on the code before T1.1 and T1.2 (T1.1 and T1.2 replace them by 0 where they remove the remainder).
    /// Each non-zero total is <see cref="FrameAllocationMeter.MeasuredFrames"/> times the bytes of one frame, plus 4872 B that the first measured frame allocates: the name of a clip
    /// (<c>$"{ElementType}.Fill"</c>, <c>$"{ElementType}.Self"</c>) is built at each draw with the <c>ToString</c> of an enum, whose cache of names the collection that opens the measure window
    /// (<see cref="AllocationWindow.Start"/>) empties, and the first draw builds it again (checked: the same 4872 B follow every collection, <c>GC.GetTotalAllocatedBytes(true)</c> reports them as the thread counter
    /// does). Bytes of one frame: <see cref="MGEllipse"/> 4392 per geometry clip, <see cref="MGPolygon"/> and <see cref="MGPathLite"/> 1152 per geometry clip (two triangles of
    /// <c>TwoFiguresBrushFill</c>: 1136 for both).</summary>
    [Theory]
    [InlineData(nameof(MGEllipse), SolidFillAndStroke, 0L, 0L)]
    [InlineData(nameof(MGEllipse), SolidFillOnly, 0L, 0L)]
    [InlineData(nameof(MGEllipse), StrokeOnly, 0L, 0L)]
    [InlineData(nameof(MGEllipse), BrushFill, 0L, 2200872L)]
    [InlineData(nameof(MGEllipse), SelfClip, 0L, 2200872L)]
    [InlineData(nameof(MGEllipse), BrushFillAndSelfClip, 0L, 4396872L)]
    [InlineData(nameof(MGEllipse), BrushFillAndSelfClipOpacityAnimated, 0L, 4396872L)]
    [InlineData(nameof(MGPolygon), SolidFillAndStroke, 0L, 0L)]
    [InlineData(nameof(MGPolygon), SolidFillOnly, 0L, 0L)]
    [InlineData(nameof(MGPolygon), StrokeOnly, 0L, 0L)]
    [InlineData(nameof(MGPolygon), BrushFill, 0L, 580872L)]
    [InlineData(nameof(MGPolygon), SelfClip, 0L, 580872L)]
    [InlineData(nameof(MGPolygon), BrushFillAndSelfClip, 0L, 1156872L)]
    [InlineData(nameof(MGPolygon), BrushFillAndSelfClipOpacityAnimated, 0L, 1156872L)]
    [InlineData(nameof(MGPathLite), SolidFillAndStroke, 0L, 0L)]
    [InlineData(nameof(MGPathLite), SolidFillOnly, 0L, 0L)]
    [InlineData(nameof(MGPathLite), StrokeOnly, 0L, 0L)]
    [InlineData(nameof(MGPathLite), BrushFill, 0L, 580872L)]
    [InlineData(nameof(MGPathLite), SelfClip, 0L, 580872L)]
    [InlineData(nameof(MGPathLite), BrushFillAndSelfClip, 0L, 1156872L)]
    [InlineData(nameof(MGPathLite), BrushFillAndSelfClipOpacityAnimated, 0L, 1156872L)]
    [InlineData(nameof(MGPathLite), TwoFiguresBrushFill, 0L, 572872L)]
    public void Shape_UpdateAndDraw_AllocateTheMeasuredRemainder(string shapeName, string caseName, long expectedUpdateTotal, long expectedDrawTotal)
    {
        Scene scene = CreateScene(shapeName, caseName);
        string label = $"{shapeName}, {caseName}";
        FrameBytes bytes = Measure(label, scene);
        AssertThePathThatTheCaseNamesRan(scene, label);
        Assert.True(bytes.UpdateTotal == expectedUpdateTotal && bytes.DrawTotal == expectedDrawTotal,
            $"{label}: Update {bytes.UpdateTotal} B and Draw {bytes.DrawTotal} B over {bytes.Iterations} frames, expected {expectedUpdateTotal} and {expectedDrawTotal}");
    }
}
