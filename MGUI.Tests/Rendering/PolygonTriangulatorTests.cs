using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using MGUI.Core.UI;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Integration;
using static MGUI.Tests.Rendering.PolygonCorpus;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Triangulation;
using Xunit.Abstractions;

namespace MGUI.Tests.Rendering;

/// <summary>
/// <see cref="PolygonTriangulator"/> gives, entry for entry, the ordered vertices and the indices of
/// <c>Triangulator.Triangulate(vertices, WindingOrder.CounterClockwise, ...)</c> of MonoGame.Extended 6.1.1 (ADR-0038), degenerate polygons that stop
/// early included, and allocates nothing once its buffers have grown.<para/>
/// <c>Triangulator</c> keeps its working lists in static fields shared by every caller: a test that calls it, directly or through
/// <c>PrimitiveDrawing.DrawSolidPolygon</c>, must not run at the same time as another one. This class joins the collection of the real-GPU tests, which
/// <c>FillPolygonGpuTests</c> (a caller of <c>DrawSolidPolygon</c>) is in, and which xunit never runs in parallel with any other test: nothing here needs the GPU,
/// the collection is only the non-parallel one of this assembly.
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class PolygonTriangulatorTests
{
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper _output;

    public PolygonTriangulatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  The reference and the comparison
    //  --------------------------------------------------------------------------------------------------------------------------------------

    private static void Reference(Vector2[] polygon, out Vector2[] vertices, out int[] indices)
        => Triangulator.Triangulate((Vector2[])polygon.Clone(), WindingOrder.CounterClockwise, out vertices, out indices);

    private static string Format(IEnumerable<Vector2> vertices)
        => string.Join(" ", vertices.Select(v => string.Create(CultureInfo.InvariantCulture, $"({v.X:R},{v.Y:R})")));

    private static string Format(IEnumerable<int> indices) => string.Join(",", indices);

    /// <summary>Null when the triangulator gives what <c>Triangulator.Triangulate</c> gives; otherwise the polygon and both results.</summary>
    private static string Compare(PolygonTriangulator triangulator, Case c, string via)
    {
        Reference(c.Vertices, out Vector2[] expectedVertices, out int[] expectedIndices);

        int count;
        switch (via)
        {
            case "array":
                count = triangulator.Triangulate(c.Vertices);
                break;
            case "list":
                count = triangulator.Triangulate((IReadOnlyList<Vector2>)new List<Vector2>(c.Vertices));
                break;
            default:
                throw new ArgumentException(via);
        }

        ReadOnlySpan<Vector2> vertices = triangulator.Vertices;
        ReadOnlySpan<int> indices = triangulator.Indices;
        bool same = count * 3 == expectedIndices.Length
            && triangulator.TriangleCount == count
            && vertices.Length == expectedVertices.Length
            && indices.Length == expectedIndices.Length;
        for (int i = 0; same && i < vertices.Length; i++)
        {
            //  Bit for bit: -0 and 0 are two different results.
            same = BitConverter.SingleToInt32Bits(vertices[i].X) == BitConverter.SingleToInt32Bits(expectedVertices[i].X)
                && BitConverter.SingleToInt32Bits(vertices[i].Y) == BitConverter.SingleToInt32Bits(expectedVertices[i].Y);
        }

        for (int i = 0; same && i < indices.Length; i++)
        {
            same = indices[i] == expectedIndices[i];
        }

        if (same)
        {
            return null;
        }

        return $"[{c.Category}] {c.Name} (via {via}), polygon {Format(c.Vertices)}{Environment.NewLine}"
            + $"  expected vertices {Format(expectedVertices)}{Environment.NewLine}"
            + $"  expected indices  {Format(expectedIndices)}{Environment.NewLine}"
            + $"  actual vertices   {Format(vertices.ToArray())}{Environment.NewLine}"
            + $"  actual indices    {Format(indices.ToArray())}";
    }

    private static void AssertNoDifference(List<string> failures, int compared)
    {
        Assert.True(failures.Count == 0,
            $"{failures.Count} of {compared} comparisons differ from Triangulator.Triangulate. First differences:{Environment.NewLine}"
            + string.Join(Environment.NewLine, failures.Take(3)));
    }

    /// <summary>Compares every case through the array and through the list entry points, once with one triangulator kept for the whole corpus (the buffers of a previous
    /// polygon must not leak into the next one) and once with a new triangulator per polygon.</summary>
    private static int CompareCorpus(IReadOnlyList<Case> corpus, bool convexFastPath, List<string> failures)
    {
        int compared = 0;
        PolygonTriangulator shared = new() { ConvexFastPathEnabled = convexFastPath };
        foreach (Case c in corpus)
        {
            foreach (string via in new[] { "array", "list" })
            {
                string sharedResult = Compare(shared, c, via);
                if (sharedResult != null)
                {
                    failures.Add("(shared triangulator) " + sharedResult);
                }

                PolygonTriangulator fresh = new() { ConvexFastPathEnabled = convexFastPath };
                string freshResult = Compare(fresh, c, via);
                if (freshResult != null)
                {
                    failures.Add("(new triangulator) " + freshResult);
                }

                compared += 2;
            }
        }

        return compared;
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  The differential
    //  --------------------------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Corpus_HasTheShapesThePlanNames_AndReachesTheDegenerateCases()
    {
        List<Case> corpus = BuildCorpus();
        foreach (IGrouping<string, Case> group in corpus.GroupBy(c => c.Category))
        {
            _output.WriteLine($"{group.Key}: {group.Count()} polygons");
        }

        _output.WriteLine($"total: {corpus.Count} polygons, seed {Seed}");

        string[] required =
        {
            "triangle and rectangle", "UISymbolDrawing arrows", "MGRatingControl shapes", "ellipses", "concave", "collinear vertices",
            "coincident consecutive vertices", "self-intersecting", "random, any points", "random, small grid", "random, star-shaped", "random, convex",
        };
        foreach (string category in required)
        {
            Assert.Contains(corpus, c => c.Category == category);
        }

        //  The degenerate cases are really in the corpus: polygons where the original stops early and fills only a part.
        int partial = 0;
        int noTriangle = 0;
        foreach (Case c in corpus)
        {
            Reference(c.Vertices, out _, out int[] indices);
            if (indices.Length < (c.Vertices.Length - 2) * 3)
            {
                partial++;
                if (indices.Length == 0)
                {
                    noTriangle++;
                }
            }
        }

        _output.WriteLine($"polygons that the original fills only in part: {partial} (of which {noTriangle} with no triangle at all)");
        Assert.True(partial > 0, "no polygon of the corpus stops the original early.");
        Assert.True(noTriangle > 0, "no polygon of the corpus gives no triangle at all with 3 or more vertices.");
    }

    [Fact]
    public void Triangulate_GivesTheVerticesAndIndicesOfTriangulator_OnTheWholeCorpus()
    {
        List<Case> corpus = BuildCorpus();
        List<string> failures = new();
        int compared = CompareCorpus(corpus, convexFastPath: true, failures);
        _output.WriteLine($"{corpus.Count} polygons, {compared} comparisons (array and list, one triangulator kept and one per polygon), {failures.Count} differences");
        AssertNoDifference(failures, compared);
    }

    [Fact]
    public void Triangulate_GivesTheSameWithoutTheConvexFastPath()
    {
        List<Case> corpus = BuildCorpus();
        List<string> failures = new();
        int compared = CompareCorpus(corpus, convexFastPath: false, failures);
        _output.WriteLine($"{corpus.Count} polygons, {compared} comparisons without the convex fast path, {failures.Count} differences");
        AssertNoDifference(failures, compared);
    }

    [Fact]
    public void ConvexFastPath_IsTakenForConvexPolygons_OfBothWindings_AndGivesWhatTheOriginalGives()
    {
        List<Case> corpus = BuildCorpus();
        PolygonTriangulator triangulator = new();
        List<string> failures = new();
        int taken = 0;
        int takenWithAlignedVertices = 0;
        int takenWithCoincidentVertices = 0;
        int notTaken = 0;
        Dictionary<string, int> takenByCategory = new();
        foreach (Case c in corpus)
        {
            string difference = Compare(triangulator, c, "array");
            if (difference != null)
            {
                failures.Add(difference);
            }

            if (triangulator.UsedConvexFastPath)
            {
                taken++;
                takenByCategory[c.Category] = takenByCategory.GetValueOrDefault(c.Category) + 1;
                if (c.Category.StartsWith("collinear", StringComparison.Ordinal) || c.Name.Contains("aligned", StringComparison.Ordinal) || c.Name.Contains("sides", StringComparison.Ordinal))
                {
                    takenWithAlignedVertices++;
                }

                if (c.Category.StartsWith("coincident", StringComparison.Ordinal))
                {
                    takenWithCoincidentVertices++;
                }
            }
            else
            {
                notTaken++;
            }
        }

        _output.WriteLine($"fast path taken for {taken} polygons of {corpus.Count} ({takenWithAlignedVertices} among those that have aligned vertices, {takenWithCoincidentVertices} among those that have coincident ones), not taken for {notTaken}");
        foreach (KeyValuePair<string, int> pair in takenByCategory.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            _output.WriteLine($"  fast path taken in '{pair.Key}': {pair.Value} of {corpus.Count(c => c.Category == pair.Key)}");
        }

        AssertNoDifference(failures, corpus.Count);
        Assert.True(takenByCategory.GetValueOrDefault("random, convex") > 500, "the convex fast path is not exercised by the random convex polygons.");
        Assert.True(taken > 100, "the convex fast path is not exercised by the corpus.");
        Assert.True(takenWithAlignedVertices > 0, "the convex fast path is not exercised with aligned vertices.");

        //  The path is for convex polygons only: a concave polygon, and polygons with a reflex vertex of the original (coincident vertices give a reflex one), do not take it.
        foreach (Case c in BuildCorpus(withExhaustive: false).Where(c => c.Category == "concave"))
        {
            triangulator.Triangulate(c.Vertices);
            Assert.False(triangulator.UsedConvexFastPath, $"{c.Name} took the convex fast path.");
        }

        triangulator.Triangulate(V(0, 0, 10, 0, 10, 0, 10, 10, 0, 10));
        Assert.False(triangulator.UsedConvexFastPath);
        triangulator.Triangulate(V(0, 0, 10, 0, 10, 10, 0, 10));
        Assert.True(triangulator.UsedConvexFastPath);
        triangulator.Triangulate(V(0, 0, 10, 0, 10, 10, 0, 10, 0, 0));
        Assert.False(triangulator.UsedConvexFastPath);
    }

    [Fact]
    public void Triangulate_OfAClockwisePolygon_ReturnsTheVerticesInTheOrderOfTheOriginal()
    {
        PolygonTriangulator triangulator = new();
        Vector2[] clockwise = V(0, 0, 0, 10, 10, 10, 10, 0);
        Assert.Equal(WindingOrder.Clockwise, Triangulator.DetermineWindingOrder(clockwise));

        triangulator.Triangulate(clockwise);

        //  The first vertex is kept, the others are reversed (Triangulator.ReverseWindingOrder).
        Assert.Equal(new[] { clockwise[0], clockwise[3], clockwise[2], clockwise[1] }, triangulator.Vertices.ToArray());
        Reference(clockwise, out Vector2[] expectedVertices, out int[] expectedIndices);
        Assert.Equal(expectedVertices, triangulator.Vertices.ToArray());
        Assert.Equal(expectedIndices, triangulator.Indices.ToArray());
    }

    [Fact]
    public void Triangulate_DoesNotModifyTheArrayOfTheCaller_AndDoesNotKeepIt()
    {
        PolygonTriangulator triangulator = new();
        Vector2[] clockwise = V(0, 0, 0, 10, 10, 10, 10, 0);
        Vector2[] before = (Vector2[])clockwise.Clone();

        triangulator.Triangulate(clockwise);
        Assert.Equal(before, clockwise);

        //  The result is a copy: a later change of the array of the caller does not reach it.
        Vector2[] kept = triangulator.Vertices.ToArray();
        clockwise[1] = new Vector2(99, 99);
        Assert.Equal(kept, triangulator.Vertices.ToArray());
    }

    [Fact]
    public void Triangulate_AfterABigPolygon_GivesTheResultOfASmallOne()
    {
        //  The buffers keep the size of the biggest polygon seen (P5): what is left in them from a previous polygon must not show.
        PolygonTriangulator triangulator = new();
        Vector2[] big = MGVectorShapeHelper.CreateEllipseVertices(100, 60, 2f, 64);
        triangulator.Triangulate(big);
        Assert.Equal(62, triangulator.TriangleCount);

        foreach (Case c in BuildCorpus(withExhaustive: false).Take(400))
        {
            triangulator.Triangulate(big);
            string difference = Compare(triangulator, c, "array");
            Assert.True(difference == null, difference);
        }
    }

    [Fact]
    public void Triangulate_OfAnEmptyOrNullPolygon_Throws_AndOfFewerThanThreeVertices_GivesNoTriangle()
    {
        PolygonTriangulator triangulator = new();
        Assert.Throws<ArgumentNullException>(() => triangulator.Triangulate((Vector2[])null));
        Assert.Throws<ArgumentNullException>(() => triangulator.Triangulate((IReadOnlyList<Vector2>)null));
        Assert.Throws<ArgumentException>(() => triangulator.Triangulate(Array.Empty<Vector2>()));
        Assert.Throws<ArgumentException>(() => triangulator.Triangulate((IReadOnlyList<Vector2>)new List<Vector2>()));

        foreach (Vector2[] v in new[] { V(4, 5), V(4, 5, 8, 9), V(4, 5, 4, 5) })
        {
            Assert.Equal(0, triangulator.Triangulate(v));
            Assert.Equal(0, triangulator.TriangleCount);
            Assert.True(triangulator.Indices.IsEmpty);
            Reference(v, out Vector2[] expectedVertices, out int[] expectedIndices);
            Assert.Empty(expectedIndices);
            Assert.Equal(expectedVertices, triangulator.Vertices.ToArray());
        }
    }

    //  --------------------------------------------------------------------------------------------------------------------------------------
    //  No allocation
    //  --------------------------------------------------------------------------------------------------------------------------------------

    private static IEnumerable<(string Name, Vector2[] Vertices)> ZeroAllocationShapes()
    {
        Vector2[] triangle = V(10, 10, 70, 20, 30, 70);
        Vector2[] arrow = V(5, 30, 45, 30, 45, 15, 80, 40, 45, 65, 45, 50, 5, 50);
        Vector2[] concave = V(10, 10, 70, 10, 70, 30, 30, 30, 30, 50, 70, 50, 70, 70, 10, 70);
        Vector2[] ellipse = MGVectorShapeHelper.CreateEllipseVertices(64, 40, 2f, 32);
        yield return ("triangle", triangle);
        yield return ("triangle (reversed)", Reversed(triangle));
        yield return ("arrow", arrow);
        yield return ("arrow (reversed)", Reversed(arrow));
        yield return ("concave", concave);
        yield return ("concave (reversed)", Reversed(concave));
        yield return ("ellipse of 32 segments", ellipse);
        yield return ("ellipse of 32 segments (reversed)", Reversed(ellipse));
    }

    private static long MeasureNewTriangulator(PolygonTriangulator triangulator, Vector2[] vertices)
    {
        int sink = 0;
        for (int i = 0; i < WarmupIterations; i++)
        {
            sink += triangulator.Triangulate(vertices);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            sink += triangulator.Triangulate(vertices);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(sink.ToString());
        return after - before;
    }

    private static long MeasureNewTriangulatorOfAList(PolygonTriangulator triangulator, IReadOnlyList<Vector2> vertices)
    {
        int sink = 0;
        for (int i = 0; i < WarmupIterations; i++)
        {
            sink += triangulator.Triangulate(vertices);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            sink += triangulator.Triangulate(vertices);
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(sink.ToString());
        return after - before;
    }

    /// <summary>Total bytes of 500 calls to the original, after 500 of warm-up.</summary>
    private static long MeasureOriginal(Vector2[] vertices)
    {
        int sink = 0;
        for (int i = 0; i < WarmupIterations; i++)
        {
            Triangulator.Triangulate(vertices, WindingOrder.CounterClockwise, out Vector2[] o, out int[] indices);
            sink += indices.Length + o.Length;
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            Triangulator.Triangulate(vertices, WindingOrder.CounterClockwise, out Vector2[] o, out int[] indices);
            sink += indices.Length + o.Length;
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        GC.KeepAlive(sink.ToString());
        return after - before;
    }

    [Fact]
    public void Triangulate_AllocatesNothing_AfterWarmUp_AndTheOriginalDoes()
    {
        List<string> failures = new();
        foreach ((string name, Vector2[] vertices) in ZeroAllocationShapes())
        {
            PolygonTriangulator triangulator = new();
            List<Vector2> list = new(vertices);
            long original = MeasureOriginal(vertices);
            long fromArray = MeasureNewTriangulator(triangulator, vertices);
            long fromList = MeasureNewTriangulatorOfAList(triangulator, list);
            _output.WriteLine($"{name} ({vertices.Length} vertices): Triangulator.Triangulate {(original + MeasuredIterations - 1) / MeasuredIterations} B/call ({original} B over {MeasuredIterations} calls), "
                + $"PolygonTriangulator of an array {(fromArray + MeasuredIterations - 1) / MeasuredIterations} B/call ({fromArray} B), of a list {(fromList + MeasuredIterations - 1) / MeasuredIterations} B/call ({fromList} B)");
            if (fromArray != 0)
            {
                failures.Add($"{name}: {fromArray} B over {MeasuredIterations} calls with an array");
            }

            if (fromList != 0)
            {
                failures.Add($"{name}: {fromList} B over {MeasuredIterations} calls with a list");
            }

            Assert.True(original > 0, $"{name}: the original allocates nothing, the measure is wrong.");
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    [Fact]
    public void Triangulate_AllocatesNothing_OnASmallPolygon_AfterABigOne()
    {
        //  The buffers never shrink (P5): the triangulator that has seen 64 vertices does not allocate again for a triangle, and a bigger polygon allocates once.
        PolygonTriangulator triangulator = new();
        Vector2[] big = MGVectorShapeHelper.CreateEllipseVertices(100, 60, 2f, 64);
        Vector2[] triangle = V(10, 10, 70, 20, 30, 70);
        triangulator.Triangulate(big);
        Assert.Equal(0, MeasureNewTriangulator(triangulator, triangle));
        Assert.Equal(0, MeasureNewTriangulator(triangulator, big));
    }

    [Fact]
    public void Triangulate_OfAConcaveShapeWithManyReflexVertices_AllocatesNothing_AndKeepsItsResultUntilTheNextCall()
    {
        PolygonTriangulator triangulator = new();
        Vector2[] comb = V(0, 0, 50, 0, 50, 20, 45, 20, 45, 5, 35, 5, 35, 20, 30, 20, 30, 5, 20, 5, 20, 20, 15, 20, 15, 5, 5, 5, 5, 20, 0, 20);
        Assert.Equal(0, MeasureNewTriangulator(triangulator, comb));

        //  The result is read in place, and stays the same until the next call.
        Vector2[] first = triangulator.Vertices.ToArray();
        int[] firstIndices = triangulator.Indices.ToArray();
        Assert.Equal(first, triangulator.Vertices.ToArray());
        Assert.Equal(firstIndices, triangulator.Indices.ToArray());
        Assert.Equal(comb.Length - 2, triangulator.TriangleCount);
    }
}
