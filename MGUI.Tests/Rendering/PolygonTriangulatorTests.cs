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
    private const int Seed = 20261117;
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper _output;

    public PolygonTriangulatorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private sealed class Case
    {
        public Case(string category, string name, Vector2[] vertices)
        {
            Category = category;
            Name = name;
            Vertices = vertices;
        }

        public string Category { get; }
        public string Name { get; }
        public Vector2[] Vertices { get; }
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
    //  The corpus
    //  --------------------------------------------------------------------------------------------------------------------------------------

    private static Vector2[] Reversed(Vector2[] v)
    {
        Vector2[] copy = (Vector2[])v.Clone();
        Array.Reverse(copy);
        return copy;
    }

    /// <summary>Adds the polygon in the order given and in the opposite order.</summary>
    private static void AddBothWindings(List<Case> corpus, string category, string name, params Vector2[] v)
    {
        corpus.Add(new Case(category, name, v));
        corpus.Add(new Case(category, name + " (reversed)", Reversed(v)));
    }

    private static Vector2[] V(params float[] xy)
    {
        Vector2[] v = new Vector2[xy.Length / 2];
        for (int i = 0; i < v.Length; i++)
        {
            v[i] = new Vector2(xy[i * 2], xy[i * 2 + 1]);
        }

        return v;
    }

    private static void AddBasicShapes(List<Case> corpus)
    {
        AddBothWindings(corpus, "triangle and rectangle", "triangle", V(10, 10, 70, 20, 30, 70));
        AddBothWindings(corpus, "triangle and rectangle", "right triangle", V(0, 0, 10, 0, 0, 10));
        AddBothWindings(corpus, "triangle and rectangle", "rectangle", V(0, 0, 40, 0, 40, 20, 0, 20));
        AddBothWindings(corpus, "triangle and rectangle", "rectangle from a corner other than the first", V(40, 20, 0, 20, 0, 0, 40, 0));
        AddBothWindings(corpus, "triangle and rectangle", "square", V(5, 5, 6, 5, 6, 6, 5, 6));
        AddBothWindings(corpus, "triangle and rectangle", "fractional rectangle", V(0.25f, 0.5f, 7.125f, 0.5f, 7.125f, 3.75f, 0.25f, 3.75f));
        AddBothWindings(corpus, "triangle and rectangle", "negative coordinates", V(-5, -5, 5, -5, 5, 5, -5, 5));
        AddBothWindings(corpus, "triangle and rectangle", "pentagon", V(10, 0, 20, 7, 16, 19, 4, 19, 0, 7));
        AddBothWindings(corpus, "triangle and rectangle", "trapezoid", V(0, 0, 30, 0, 24, 10, 6, 10));
    }

    private static void AddUISymbolArrows(List<Case> corpus)
    {
        Rectangle[] bounds =
        {
            new(0, 0, 8, 8), new(3, 5, 9, 7), new(10, 10, 16, 16), new(0, 0, 5, 5), new(1, 2, 6, 4), new(0, 0, 7, 12), new(20, 30, 11, 11),
            new(0, 0, 1, 1), new(0, 0, 2, 9), new(4, 4, 24, 24), new(-3, -4, 10, 6), new(0, 0, 100, 40),
        };

        foreach (UITriangleArrowDirection direction in Enum.GetValues<UITriangleArrowDirection>())
        {
            foreach (Rectangle b in bounds)
            {
                Vector2[] v = UISymbolDrawing.GetTriangleArrowVertices(b, direction).Select(p => p.ToVector2()).ToArray();
                AddBothWindings(corpus, "UISymbolDrawing arrows", $"arrow {direction} in {b}", v);
            }
        }
    }

    private static void AddRatingShapes(List<Case> corpus)
    {
        FieldInfo field = typeof(MGRatingControl).GetField("ShapeVertices256", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(field);
        IDictionary shapes = (IDictionary)field.GetValue(null);
        Assert.NotNull(shapes);
        Assert.Contains(shapes.Keys.Cast<object>(), k => k.ToString() == "Star");
        Assert.Contains(shapes.Keys.Cast<object>(), k => k.ToString() == "Diamond");

        foreach (DictionaryEntry entry in shapes)
        {
            IReadOnlyList<Vector2> source = (IReadOnlyList<Vector2>)entry.Value;
            //  The 256 pixel shape, and the shape scaled as MGRatingControl.GetScaledVertices scales it for the sizes of an item.
            foreach (int itemSize in new[] { 256, 96, 32, 24, 20, 16, 12, 9 })
            {
                float scale = itemSize / 256.0f;
                Vector2[] v = new Vector2[source.Count];
                for (int i = 0; i < v.Length; i++)
                {
                    v[i] = source[i] * scale;
                }

                AddBothWindings(corpus, "MGRatingControl shapes", $"{entry.Key} at item size {itemSize}", v);
            }
        }
    }

    private static void AddEllipses(List<Case> corpus)
    {
        (int Width, int Height)[] sizes = { (20, 20), (40, 24), (101, 57), (8, 8), (64, 64), (200, 12), (3, 3) };
        float[] strokes = { 0f, 1f, 2f, 3.5f, 10f };
        int[] segments = { 32, 3, 4, 5, 6, 7, 8, 12, 16, 24, 48, 64 };
        foreach (int s in segments)
        {
            foreach ((int w, int h) in sizes)
            {
                foreach (float stroke in strokes)
                {
                    Vector2[] v = MGVectorShapeHelper.CreateEllipseVertices(w, h, stroke, s);
                    if (v.Length == 0)
                    {
                        continue;
                    }

                    //  32 segments in every size and stroke, the other counts in a few of them: the corpus stays a few thousand polygons.
                    if (s != 32 && (w, h) != (40, 24) && (w, h) != (8, 8) && stroke != 2f)
                    {
                        continue;
                    }

                    AddBothWindings(corpus, "ellipses", $"ellipse {w}x{h}, stroke {stroke.ToString(CultureInfo.InvariantCulture)}, {s} segments", v);
                }
            }
        }
    }

    private static void AddConcaveShapes(List<Case> corpus)
    {
        AddBothWindings(corpus, "concave", "concave of FillPolygonGpuTests", V(10, 10, 70, 10, 70, 30, 30, 30, 30, 50, 70, 50, 70, 70, 10, 70));
        AddBothWindings(corpus, "concave", "arrow of FillPolygonGpuTests", V(5, 30, 45, 30, 45, 15, 80, 40, 45, 65, 45, 50, 5, 50));
        AddBothWindings(corpus, "concave", "L", V(0, 0, 20, 0, 20, 10, 10, 10, 10, 30, 0, 30));
        AddBothWindings(corpus, "concave", "U", V(0, 0, 10, 0, 10, 20, 20, 20, 20, 0, 30, 0, 30, 30, 0, 30));
        AddBothWindings(corpus, "concave", "T", V(0, 0, 30, 0, 30, 10, 20, 10, 20, 30, 10, 30, 10, 10, 0, 10));
        AddBothWindings(corpus, "concave", "dart", V(0, 0, 20, 10, 0, 20, 5, 10));
        AddBothWindings(corpus, "concave", "comb", V(0, 0, 50, 0, 50, 20, 45, 20, 45, 5, 35, 5, 35, 20, 30, 20, 30, 5, 20, 5, 20, 20, 15, 20, 15, 5, 5, 5, 5, 20, 0, 20));
        AddBothWindings(corpus, "concave", "plus", V(10, 0, 20, 0, 20, 10, 30, 10, 30, 20, 20, 20, 20, 30, 10, 30, 10, 20, 0, 20, 0, 10, 10, 10));
        AddBothWindings(corpus, "concave", "spike inside", V(0, 0, 40, 0, 40, 40, 22, 40, 20, 10, 18, 40, 0, 40));
        AddBothWindings(corpus, "concave", "zigzag", V(0, 0, 10, 10, 20, 0, 30, 10, 40, 0, 40, 20, 0, 20));
        AddBothWindings(corpus, "concave", "spiral", V(0, 0, 40, 0, 40, 40, 10, 40, 10, 15, 30, 15, 30, 30, 20, 30, 20, 25, 25, 25, 25, 20, 15, 20, 15, 35, 35, 35, 35, 5, 5, 5, 5, 40, 0, 40));

        //  A 5-point star with a ten-vertex outline, as MGRatingControl draws its star.
        List<Vector2> star = new();
        for (int i = 0; i < 10; i++)
        {
            float radius = i % 2 == 0 ? 50f : 20f;
            double angle = -Math.PI / 2d + i * Math.PI / 5d;
            star.Add(new Vector2(60 + radius * (float)Math.Cos(angle), 60 + radius * (float)Math.Sin(angle)));
        }

        AddBothWindings(corpus, "concave", "star", star.ToArray());
    }

    private static void AddAlignedVertices(List<Case> corpus)
    {
        AddBothWindings(corpus, "collinear vertices", "rectangle with the middle of each side", V(0, 0, 20, 0, 40, 0, 40, 10, 40, 20, 20, 20, 0, 20, 0, 10));
        AddBothWindings(corpus, "collinear vertices", "triangle with a vertex on each side", V(0, 0, 10, 0, 20, 0, 10, 10, 0, 20, 0, 10));
        AddBothWindings(corpus, "collinear vertices", "square with two vertices on the same side", V(0, 0, 10, 0, 20, 0, 30, 0, 30, 30, 0, 30));
        AddBothWindings(corpus, "collinear vertices", "L with aligned vertices", V(0, 0, 10, 0, 20, 0, 20, 10, 10, 10, 10, 20, 10, 30, 0, 30, 0, 15));
        AddBothWindings(corpus, "collinear vertices", "three aligned vertices", V(0, 0, 10, 0, 20, 0));
        AddBothWindings(corpus, "collinear vertices", "all vertices on a diagonal", V(0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5));
        AddBothWindings(corpus, "collinear vertices", "all vertices on a horizontal line, out of order", V(0, 5, 30, 5, 10, 5, 40, 5, 20, 5));
        AddBothWindings(corpus, "collinear vertices", "back and forth on a line", V(0, 0, 10, 0, 5, 0, 15, 0));
        AddBothWindings(corpus, "collinear vertices", "aligned vertices that are not exact in float", V(0, 0, 0.1f, 0.1f, 0.2f, 0.2f, 0.3f, 0.3f, 0.3f, 1, 0, 1));
        AddBothWindings(corpus, "collinear vertices", "almost aligned", V(0, 0, 10, 0.0001f, 20, 0, 20, 10, 0, 10));
        AddBothWindings(corpus, "collinear vertices", "fan of aligned vertices on a circle arc and a chord",
            V(0, 0, 10, 3, 20, 5, 30, 6, 40, 6, 50, 5, 60, 3, 70, 0));
    }

    private static void AddCoincidentVertices(List<Case> corpus)
    {
        AddBothWindings(corpus, "coincident consecutive vertices", "triangle with the first vertex twice", V(10, 10, 10, 10, 70, 20, 30, 70));
        AddBothWindings(corpus, "coincident consecutive vertices", "triangle with the last vertex twice", V(10, 10, 70, 20, 30, 70, 30, 70));
        AddBothWindings(corpus, "coincident consecutive vertices", "square with a corner twice", V(0, 0, 10, 0, 10, 0, 10, 10, 0, 10));
        AddBothWindings(corpus, "coincident consecutive vertices", "square closed by repeating its first vertex", V(0, 0, 10, 0, 10, 10, 0, 10, 0, 0));
        AddBothWindings(corpus, "coincident consecutive vertices", "every vertex twice", V(0, 0, 0, 0, 10, 0, 10, 0, 10, 10, 10, 10, 0, 10, 0, 10));
        AddBothWindings(corpus, "coincident consecutive vertices", "three vertices at the same point then a triangle", V(5, 5, 5, 5, 5, 5, 20, 5, 12, 20));
        AddBothWindings(corpus, "coincident consecutive vertices", "all vertices at the same point", V(3, 3, 3, 3, 3, 3, 3, 3));
        AddBothWindings(corpus, "coincident consecutive vertices", "three vertices at the same point", V(3, 3, 3, 3, 3, 3));
        AddBothWindings(corpus, "coincident consecutive vertices", "concave with a repeated reflex vertex", V(10, 10, 70, 10, 70, 30, 30, 30, 30, 30, 30, 50, 70, 50, 70, 70, 10, 70));
        AddBothWindings(corpus, "coincident consecutive vertices", "ellipse with a vertex twice", AddTwice(MGVectorShapeHelper.CreateEllipseVertices(40, 24, 2f, 16), 5));
        AddBothWindings(corpus, "coincident consecutive vertices", "ellipse with its first vertex at the end", AddTwice(MGVectorShapeHelper.CreateEllipseVertices(40, 24, 2f, 16), 0, true));
        AddBothWindings(corpus, "coincident consecutive vertices", "non consecutive vertices at the same point", V(0, 0, 10, 0, 10, 10, 0, 0, 0, 10, 10, 0));
    }

    private static Vector2[] AddTwice(Vector2[] v, int index, bool atTheEnd = false)
    {
        List<Vector2> list = new(v);
        if (atTheEnd)
        {
            list.Add(v[index]);
        }
        else
        {
            list.Insert(index, v[index]);
        }

        return list.ToArray();
    }

    private static void AddSelfIntersecting(List<Case> corpus)
    {
        AddBothWindings(corpus, "self-intersecting", "bow-tie", V(0, 0, 10, 10, 10, 0, 0, 10));
        AddBothWindings(corpus, "self-intersecting", "bow-tie, other vertex first", V(10, 10, 10, 0, 0, 10, 0, 0));
        AddBothWindings(corpus, "self-intersecting", "unequal bow-tie", V(0, 0, 30, 20, 30, 0, 0, 40));
        AddBothWindings(corpus, "self-intersecting", "figure eight", V(0, 0, 20, 0, 20, 10, 10, 10, 10, 20, 30, 20, 30, 30, 0, 30, 0, 10, 10, 10, 10, 0));

        //  Stars made of one stroke: every vertex has the same kind of turn.
        foreach ((int points, int step) in new[] { (5, 2), (7, 2), (7, 3), (8, 3), (9, 4) })
        {
            Vector2[] v = new Vector2[points];
            for (int i = 0; i < points; i++)
            {
                double angle = -Math.PI / 2d + 2d * Math.PI * (i * step % points) / points;
                v[i] = new Vector2(50 + 40 * (float)Math.Cos(angle), 50 + 40 * (float)Math.Sin(angle));
            }

            AddBothWindings(corpus, "self-intersecting", $"star {points}/{step} in one stroke", v);
        }
    }

    /// <summary>Every polygon of 3 to 5 vertices on a 3x3 grid (all the vertices of the grid in any order, repeats included): coincident vertices, aligned vertices,
    /// bow-ties, polygons with no ear, in every number and order.</summary>
    private static void AddExhaustiveSmallGrid(List<Case> corpus)
    {
        for (int n = 3; n <= 5; n++)
        {
            int total = 1;
            for (int i = 0; i < n; i++)
            {
                total *= 9;
            }

            for (int code = 0; code < total; code++)
            {
                Vector2[] v = new Vector2[n];
                int rest = code;
                for (int i = 0; i < n; i++)
                {
                    int cell = rest % 9;
                    rest /= 9;
                    v[i] = new Vector2(cell % 3, cell / 3);
                }

                corpus.Add(new Case($"exhaustive {n} vertices on a 3x3 grid", code.ToString(CultureInfo.InvariantCulture), v));
            }
        }
    }

    private static void AddSeededRandom(List<Case> corpus)
    {
        Random random = new(Seed);

        //  Points anywhere: mostly non-simple polygons, with every kind of turn.
        for (int k = 0; k < 1500; k++)
        {
            int n = random.Next(3, 65);
            Vector2[] v = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector2((float)(random.NextDouble() * 100), (float)(random.NextDouble() * 100));
            }

            corpus.Add(new Case("random, any points", $"#{k} ({n} vertices)", v));
        }

        //  Whole coordinates on a small grid: many aligned and coincident vertices.
        for (int k = 0; k < 1500; k++)
        {
            int n = random.Next(3, 65);
            int extent = random.Next(2, 9);
            Vector2[] v = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector2(random.Next(0, extent), random.Next(0, extent));
            }

            corpus.Add(new Case("random, small grid", $"#{k} ({n} vertices, grid {extent})", v));
        }

        //  Simple polygons: vertices around a centre by ascending angle, a random radius each (star-shaped), in a random direction.
        for (int k = 0; k < 1500; k++)
        {
            int n = random.Next(3, 65);
            double[] angles = new double[n];
            for (int i = 0; i < n; i++)
            {
                angles[i] = random.NextDouble() * 2 * Math.PI;
            }

            Array.Sort(angles);
            Vector2[] v = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                double radius = 5 + random.NextDouble() * 45;
                v[i] = new Vector2(60 + (float)(radius * Math.Cos(angles[i])), 60 + (float)(radius * Math.Sin(angles[i])));
            }

            corpus.Add(new Case("random, star-shaped", $"#{k} ({n} vertices)", random.Next(2) == 0 ? v : Reversed(v)));
        }

        //  Convex polygons: vertices of an ellipse by ascending angle (rounding makes some nearly aligned), whole coordinates (real aligned vertices) and
        //  an axis-aligned rectangle with extra vertices on its sides.
        for (int k = 0; k < 600; k++)
        {
            int n = random.Next(3, 65);
            double[] angles = new double[n];
            for (int i = 0; i < n; i++)
            {
                angles[i] = random.NextDouble() * 2 * Math.PI;
            }

            Array.Sort(angles);
            double rx = 5 + random.NextDouble() * 60;
            double ry = 5 + random.NextDouble() * 60;
            Vector2[] v = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector2(70 + (float)(rx * Math.Cos(angles[i])), 70 + (float)(ry * Math.Sin(angles[i])));
            }

            corpus.Add(new Case("random, convex", $"ellipse #{k} ({n} vertices)", random.Next(2) == 0 ? v : Reversed(v)));
        }

        for (int k = 0; k < 400; k++)
        {
            int n = random.Next(3, 65);
            double[] angles = new double[n];
            for (int i = 0; i < n; i++)
            {
                angles[i] = random.NextDouble() * 2 * Math.PI;
            }

            Array.Sort(angles);
            Vector2[] v = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                v[i] = new Vector2((float)Math.Round(70 + 60 * Math.Cos(angles[i])), (float)Math.Round(70 + 60 * Math.Sin(angles[i])));
            }

            corpus.Add(new Case("random, convex", $"whole coordinates #{k} ({n} vertices)", random.Next(2) == 0 ? v : Reversed(v)));
        }

        for (int k = 0; k < 400; k++)
        {
            int w = random.Next(2, 40);
            int h = random.Next(2, 40);
            List<Vector2> v = new();
            for (int x = 0; x < w; x += random.Next(1, 10))
            {
                v.Add(new Vector2(x, 0));
            }

            for (int y = 0; y < h; y += random.Next(1, 10))
            {
                v.Add(new Vector2(w, y));
            }

            for (int x = w; x > 0; x -= random.Next(1, 10))
            {
                v.Add(new Vector2(x, h));
            }

            for (int y = h; y > 0; y -= random.Next(1, 10))
            {
                v.Add(new Vector2(0, y));
            }

            if (v.Count < 3)
            {
                continue;
            }

            //  Some vertices twice in a row.
            for (int i = v.Count - 1; i >= 0; i--)
            {
                if (random.Next(10) == 0)
                {
                    v.Insert(i, v[i]);
                }
            }

            Vector2[] array = v.Count > 64 ? v.Take(64).ToArray() : v.ToArray();
            corpus.Add(new Case("random, convex", $"rectangle with vertices on its sides #{k} ({array.Length} vertices)", random.Next(2) == 0 ? array : Reversed(array)));
        }
    }

    private static List<Case> BuildCorpus(bool withExhaustive = true)
    {
        List<Case> corpus = new();
        AddBasicShapes(corpus);
        AddUISymbolArrows(corpus);
        AddRatingShapes(corpus);
        AddEllipses(corpus);
        AddConcaveShapes(corpus);
        AddAlignedVertices(corpus);
        AddCoincidentVertices(corpus);
        AddSelfIntersecting(corpus);
        if (withExhaustive)
        {
            AddExhaustiveSmallGrid(corpus);
        }

        AddSeededRandom(corpus);
        return corpus;
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
