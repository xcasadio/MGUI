using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MonoGame.Extended.Triangulation;
using Xunit.Abstractions;
using static MGUI.Tests.Architecture.ShapeDrawAllocationTests;

namespace MGUI.Tests.Architecture;

/// <summary>
/// T1.2 of the plan "end of the zero allocation of the drawing": <see cref="MGEllipse"/>, <see cref="MGPolygon"/> and <see cref="MGPathLite"/> keep the triangulated geometry of their clips and the
/// <see cref="ClipDefinition"/> that holds it, one entry per use (the fill brush, the shape itself, and for a path one per filled closed figure), instead of building them at each draw.
/// <para/>
/// Three kinds of proof. (1) The journals of the draw (every call to the transaction, with the geometry of every clip as values, bit for bit) were frozen on the code that built a new geometry
/// and a new definition at each draw, through a sequence of states (at rest, drawn twice, moved, each data changed, stroke thickness changed): the code that keeps them must write the same.
/// (2) After each change of state, the geometry that the next draw gives to the clip is what a fresh <see cref="MGVectorShapeHelper.CreateTriangulatedClipGeometry"/> returns for the new state.
/// (3) At rest the same definition object is given to the clip at each frame, one per use, and a definition that has been given to the clip is never rewritten when the shape changes
/// (the clip pipeline keeps a definition from its push to its pop).
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class ShapeClipGeometryTests
{
    private readonly ITestOutputHelper Output;

    public ShapeClipGeometryTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static OrderedRecordingDrawTransaction DrawRecorded(ControlBench bench)
    {
        OrderedRecordingDrawTransaction transaction = new(bench.Runtime, DrawSettings.Default);
        bench.Update();
        bench.Desktop.Draw(transaction);
        return transaction;
    }

    private static string Bits(Vector2 v) => $"{BitConverter.SingleToInt32Bits(v.X):X8}:{BitConverter.SingleToInt32Bits(v.Y):X8}";

    /// <summary>The journal of a draw, then the geometry of every clip that has one, as bit patterns of the vertices and as indices.</summary>
    private static List<string> Snapshot(string step, OrderedRecordingDrawTransaction transaction)
    {
        List<string> lines = new() { "== " + step };
        lines.AddRange(transaction.Lines);
        for (int i = 0; i < transaction.PushedDefinitions.Count; i++)
        {
            ClipDefinition definition = transaction.PushedDefinitions[i];
            if (definition == null || !definition.Shape.Geometry.HasValue || definition.Shape.Geometry.Value.IsEmpty)
            {
                continue;
            }

            ClipGeometry geometry = definition.Shape.Geometry.Value;
            Rectangle b = definition.Shape.Bounds;
            lines.Add($"geometry {definition.DebugName} [{b.X},{b.Y},{b.Width},{b.Height}] v={string.Join(';', geometry.Vertices.Select(Bits))} i={string.Join(',', geometry.Indices)}");
        }

        return lines;
    }

    private static Vector2[] LShape() => new Vector2[] { new(0, 0), new(50, 0), new(50, 20), new(20, 20), new(20, 50), new(0, 50) };

    /// <summary>A polygon of the same extents as <see cref="LShape"/> (0 to 50 on both axes), not the same polygon: a change of points that moves neither the placement nor the target bounds.</summary>
    private static Vector2[] MirroredLShape() => new Vector2[] { new(0, 0), new(50, 0), new(50, 50), new(30, 50), new(30, 20), new(0, 20) };

    private static MGPathLiteCommand[] LShapeCommands(bool twoFigures, bool mirrored = false)
    {
        Vector2[] l = mirrored ? MirroredLShape() : LShape();
        List<MGPathLiteCommand> commands = new() { MGPathLiteCommand.MoveTo(l[0]) };
        for (int i = 1; i < l.Length; i++)
        {
            commands.Add(MGPathLiteCommand.LineTo(l[i]));
        }

        commands.Add(MGPathLiteCommand.Close());
        if (twoFigures)
        {
            commands.Add(MGPathLiteCommand.MoveTo(new Vector2(70, 5)));
            commands.Add(MGPathLiteCommand.LineTo(new Vector2(100, 5)));
            commands.Add(MGPathLiteCommand.LineTo(new Vector2(100, 35)));
            commands.Add(MGPathLiteCommand.LineTo(new Vector2(70, 35)));
            commands.Add(MGPathLiteCommand.Close());
        }

        return commands.ToArray();
    }

    /// <summary>The changes of state that the proofs go through, in order: each applies on top of the previous ones.</summary>
    private static List<(string Name, Action Apply)> Steps(Scene scene)
    {
        MGShapeElementBase shape = scene.Shape;
        List<(string Name, Action Apply)> steps = new()
        {
            ("initial", () => { }),
            ("second frame", () => { }),
            ("moved", () => { shape.Margin = new Thickness(11, 5, 0, 0); }),
        };

        switch (shape)
        {
            case MGEllipse ellipse:
                steps.Add(("width", () => { ellipse.Width = 100; }));
                steps.Add(("height", () => { ellipse.Height = 60; }));
                steps.Add(("segment count", () => { ellipse.SegmentCount = 20; }));
                break;
            case MGPolygon polygon:
                steps.Add(("points", () => { polygon.Points = LShape(); }));
                steps.Add(("points of the same extents", () => { polygon.Points = MirroredLShape(); }));
                break;
            case MGPathLite path:
                steps.Add(("commands", () => { path.Commands = LShapeCommands(scene.FilledFigures == 2); }));
                steps.Add(("commands of the same extents", () => { path.Commands = LShapeCommands(scene.FilledFigures == 2, mirrored: true); }));
                break;
        }

        steps.Add(("stroke thickness", () => { shape.StrokeThickness = 4f; }));
        steps.Add(("moved again", () => { shape.Margin = new Thickness(0, 9, 3, 0); }));
        return steps;
    }

    private static readonly string[] ShapeNames = { nameof(MGEllipse), nameof(MGPolygon), nameof(MGPathLite) };

    public static IEnumerable<object[]> FrozenCases()
    {
        foreach (string shape in ShapeNames)
        {
            foreach (string caseName in new[] { BrushFill, SelfClip, BrushFillAndSelfClip })
            {
                yield return new object[] { shape, caseName };
            }
        }

        yield return new object[] { nameof(MGPathLite), TwoFiguresBrushFill };
    }

    private static readonly Dictionary<string, (int Lines, string Hash)> Frozen = new()
    {
        //  Frozen on the code that built the geometry and the definition of a clip at each draw (before T1.2).
        [$"{nameof(MGEllipse)}|{BrushFillAndSelfClip}"] = (408, "023948BC97A70CC4"),
        [$"{nameof(MGEllipse)}|{BrushFill}"] = (384, "D92CCA55B6852132"),
        [$"{nameof(MGEllipse)}|{SelfClip}"] = (384, "2B135C4BC5DF5FFC"),
        [$"{nameof(MGPathLite)}|{BrushFillAndSelfClip}"] = (357, "95A2A0F76C654EA5"),
        [$"{nameof(MGPathLite)}|{BrushFill}"] = (336, "618949F7D7855854"),
        [$"{nameof(MGPathLite)}|{SelfClip}"] = (336, "9B8CCBA1D4C1E2D9"),
        [$"{nameof(MGPathLite)}|{TwoFiguresBrushFill}"] = (364, "45F957132CE87BB2"),
        [$"{nameof(MGPolygon)}|{BrushFillAndSelfClip}"] = (357, "82409F590818C99F"),
        [$"{nameof(MGPolygon)}|{BrushFill}"] = (336, "0BA578D025567B2C"),
        [$"{nameof(MGPolygon)}|{SelfClip}"] = (336, "396257C1827A1EAC"),
    };

    [Theory]
    [MemberData(nameof(FrozenCases))]
    public void DrawJournal_AndClipGeometry_AreTheOnesOfTheCodeThatBuiltThemAtEachDraw(string shapeName, string caseName)
    {
        Scene scene = CreateScene(shapeName, caseName);
        List<string> lines = new();
        foreach ((string name, Action apply) in Steps(scene))
        {
            apply();
            scene.Bench.Frames(2);
            lines.AddRange(Snapshot(name, DrawRecorded(scene.Bench)));
        }

        string key = $"{shapeName}|{caseName}";
        string hash = FrozenJournal.Hash(string.Join('\n', lines));
        Assert.True(Frozen.TryGetValue(key, out (int Lines, string Hash) expected), $"NOT FROZEN {key}: {lines.Count} lines, hash {hash}");
        Assert.True(lines.Count == expected.Lines && hash == expected.Hash, $"{key}: expected {expected.Lines} lines, hash {expected.Hash}; got {lines.Count} lines, hash {hash}.\nJournal:\n{string.Join('\n', lines)}");
    }

    private static readonly BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

    private static Vector2[] EllipseVertices(MGEllipse ellipse) => (Vector2[])typeof(MGEllipse).GetField("_cachedVertices", Instance).GetValue(ellipse);

    private static Vector2[] PolygonPoints(MGPolygon polygon) => (Vector2[])typeof(MGPolyline).GetField("NormalizedPoints", Instance).GetValue(polygon);

    private static MGPathLiteFigure[] PathFigures(MGPathLite path) => (MGPathLiteFigure[])typeof(MGPathLite).GetField("_Figures", Instance).GetValue(path);

    /// <summary>The offset of the draw arguments of the shape: the desktop draws its windows with a zero offset (<c>MGWindow.Draw</c>), and the shape is the content of the window, with no scroll viewer
    /// (the only element that moves the offset) between them.</summary>
    private static readonly Point DrawOffset = Point.Zero;

    /// <summary>The target bounds of a clip of the shape, from the bounds the shape draws in and the settings the transaction draws with: the expression of <c>MGElement.TransformClipBounds</c>.</summary>
    private static Rectangle ExpectedTargetBounds(Rectangle shapeBounds)
        => shapeBounds.GetTranslated(DrawOffset).CreateTransformedBoundsF(DrawSettings.Default.Transform).RoundUp();

    /// <summary>What a fresh <see cref="MGVectorShapeHelper.CreateTriangulatedClipGeometry"/> returns for the state of the shape, for every geometry clip of a draw, in the order of the draw. The
    /// translation and the target bounds expected are the ones the shape computes from its own layout and the draw arguments (placement, <see cref="DrawOffset"/>, the transform of the settings), not
    /// read back from the definition under test: a definition kept by mistake after a move does not match them.</summary>
    private static void AssertEveryClipGeometryIsAFreshOne(Scene scene, OrderedRecordingDrawTransaction transaction, string step)
    {
        MGShapeElementBase shape = scene.Shape;
        int fillIndex = 0;
        int checkedClips = 0;
        for (int i = 0; i < transaction.PushedDefinitions.Count; i++)
        {
            ClipDefinition definition = transaction.PushedDefinitions[i];
            if (definition == null || definition.Kind != ClipKind.ArbitraryGeometry)
            {
                continue;
            }

            bool isFill = definition.DebugName.EndsWith(".Fill", StringComparison.Ordinal);
            IReadOnlyList<Vector2> source;
            Vector2 translation;
            Rectangle targetBounds;
            switch (shape)
            {
                case MGEllipse ellipse:
                {
                    Rectangle bounds = MGElement.ApplyAlignment(ellipse.LayoutBounds, ellipse.HorizontalAlignment, ellipse.VerticalAlignment, new Size(ellipse.Width, ellipse.Height));
                    source = EllipseVertices(ellipse);
                    translation = DrawOffset.ToVector2() + bounds.Location.ToVector2();
                    targetBounds = ExpectedTargetBounds(bounds);
                    break;
                }
                case MGPolygon polygon:
                {
                    MGPointShapePlacement placement = polygon.GetPlacement(polygon.LayoutBounds);
                    source = PolygonPoints(polygon);
                    translation = DrawOffset.ToVector2() + placement.GeometryOrigin;
                    targetBounds = ExpectedTargetBounds(placement.Bounds);
                    break;
                }
                default:
                {
                    MGPathLite path = (MGPathLite)shape;
                    MGPointShapePlacement placement = path.GetPlacement(path.LayoutBounds);
                    MGPathLiteFigure[] figures = PathFigures(path);
                    source = null;
                    if (isFill)
                    {
                        int seen = 0;
                        for (int f = 0; f < figures.Length && source == null; f++)
                        {
                            if (figures[f].IsClosed && figures[f].Points.Count >= 3 && seen++ == fillIndex)
                            {
                                source = figures[f].Points;
                            }
                        }
                    }
                    else
                    {
                        source = figures[0].Points;
                    }

                    Assert.NotNull(source);
                    translation = DrawOffset.ToVector2() + placement.GeometryOrigin;
                    targetBounds = ExpectedTargetBounds(placement.Bounds);
                    break;
                }
            }

            if (isFill)
            {
                fillIndex++;
            }

            ClipGeometry expected = MGVectorShapeHelper.CreateTriangulatedClipGeometry(source, translation);
            ClipGeometry actual = definition.Shape.Geometry.Value;
            Assert.True(targetBounds == definition.Shape.Bounds, $"{step}, {definition.DebugName}: the target bounds of the clip are {definition.Shape.Bounds}, the shape draws in {targetBounds}");
            Assert.True(expected.Vertices.Select(Bits).SequenceEqual(actual.Vertices.Select(Bits)), $"{step}, {definition.DebugName}: the vertices of the clip are not the ones of a fresh geometry");
            Assert.True(expected.Indices.SequenceEqual(actual.Indices), $"{step}, {definition.DebugName}: the indices of the clip are not the ones of a fresh geometry");
            checkedClips++;
        }

        int expectedClips = (scene.HasBrushFill ? scene.FilledFigures : 0) + (scene.HasSelfClip ? 1 : 0);
        Assert.Equal(expectedClips, checkedClips);
    }

    [Theory]
    [MemberData(nameof(FrozenCases))]
    public void ClipGeometry_AfterEachChangeOfState_IsAFreshOne(string shapeName, string caseName)
    {
        Scene scene = CreateScene(shapeName, caseName);
        foreach ((string name, Action apply) in Steps(scene))
        {
            apply();
            scene.Bench.Frames(2);
            AssertEveryClipGeometryIsAFreshOne(scene, DrawRecorded(scene.Bench), $"{shapeName}, {caseName}, after {name}");
        }
    }

    private static List<ClipDefinition> GeometryDefinitions(OrderedRecordingDrawTransaction transaction)
        => transaction.PushedDefinitions.Where(x => x != null && x.Kind == ClipKind.ArbitraryGeometry).ToList();

    private static ClipDefinition ByName(List<ClipDefinition> definitions, string suffix)
        => definitions.Single(x => x.DebugName.EndsWith(suffix, StringComparison.Ordinal));

    [Theory]
    [InlineData(nameof(MGEllipse))]
    [InlineData(nameof(MGPolygon))]
    [InlineData(nameof(MGPathLite))]
    public void AtRest_EachUseKeepsItsDefinition_AndTheTwoUsesDoNotShareOne(string shapeName)
    {
        Scene scene = CreateScene(shapeName, BrushFillAndSelfClip);
        List<ClipDefinition> first = GeometryDefinitions(DrawRecorded(scene.Bench));
        ClipDefinition fill = ByName(first, ".Fill");
        ClipDefinition self = ByName(first, ".Self");
        Assert.NotSame(fill, self);
        Assert.NotSame(fill.Shape.Geometry.Value.Vertices, self.Shape.Geometry.Value.Vertices);

        for (int frame = 0; frame < 3; frame++)
        {
            List<ClipDefinition> next = GeometryDefinitions(DrawRecorded(scene.Bench));
            Assert.Equal(2, next.Count);
            Assert.Same(fill, ByName(next, ".Fill"));
            Assert.Same(self, ByName(next, ".Self"));
        }
    }

    [Fact]
    public void AtRest_EachFilledFigureOfAPathKeepsItsDefinition()
    {
        Scene scene = CreateScene(nameof(MGPathLite), TwoFiguresBrushFill);
        List<ClipDefinition> first = GeometryDefinitions(DrawRecorded(scene.Bench));
        Assert.Equal(2, first.Count);
        Assert.NotSame(first[0], first[1]);
        for (int frame = 0; frame < 3; frame++)
        {
            List<ClipDefinition> next = GeometryDefinitions(DrawRecorded(scene.Bench));
            Assert.Equal(2, next.Count);
            Assert.Same(first[0], next[0]);
            Assert.Same(first[1], next[1]);
        }
    }

    [Theory]
    [InlineData(nameof(MGEllipse))]
    [InlineData(nameof(MGPolygon))]
    [InlineData(nameof(MGPathLite))]
    public void AnimatedOpacity_KeepsTheDefinitions(string shapeName)
    {
        Scene scene = CreateScene(shapeName, BrushFillAndSelfClip);
        List<ClipDefinition> first = GeometryDefinitions(DrawRecorded(scene.Bench));
        for (int frame = 0; frame < 4; frame++)
        {
            scene.Shape.Opacity = 0.5f + frame * 0.1f;
            List<ClipDefinition> next = GeometryDefinitions(DrawRecorded(scene.Bench));
            Assert.Same(ByName(first, ".Fill"), ByName(next, ".Fill"));
            Assert.Same(ByName(first, ".Self"), ByName(next, ".Self"));
        }
    }

    [Theory]
    [InlineData(nameof(MGEllipse))]
    [InlineData(nameof(MGPolygon))]
    [InlineData(nameof(MGPathLite))]
    public void AfterAMove_TheDefinitionsAreNewOnes_AndTheOldOnesAreNotRewritten(string shapeName)
    {
        Scene scene = CreateScene(shapeName, BrushFillAndSelfClip);
        List<ClipDefinition> before = GeometryDefinitions(DrawRecorded(scene.Bench));
        ClipDefinition fill = ByName(before, ".Fill");
        ClipDefinition self = ByName(before, ".Self");
        Vector2[] fillVertices = fill.Shape.Geometry.Value.Vertices.ToArray();
        int[] fillIndices = fill.Shape.Geometry.Value.Indices.ToArray();
        Vector2[] selfVertices = self.Shape.Geometry.Value.Vertices.ToArray();
        Rectangle fillBounds = fill.Shape.Bounds;

        scene.Shape.Margin = new Thickness(13, 7, 0, 0);
        scene.Bench.Frames(2);
        List<ClipDefinition> after = GeometryDefinitions(DrawRecorded(scene.Bench));
        Assert.NotSame(fill, ByName(after, ".Fill"));
        Assert.NotSame(self, ByName(after, ".Self"));
        Assert.NotEqual(fillBounds, ByName(after, ".Fill").Shape.Bounds);

        Assert.Equal(fillBounds, fill.Shape.Bounds);
        Assert.True(fillVertices.SequenceEqual(fill.Shape.Geometry.Value.Vertices));
        Assert.True(fillIndices.SequenceEqual(fill.Shape.Geometry.Value.Indices));
        Assert.True(selfVertices.SequenceEqual(self.Shape.Geometry.Value.Vertices));
    }

    /// <summary>The entry triangulates with <c>PolygonTriangulator</c> where <see cref="MGVectorShapeHelper.CreateTriangulatedClipGeometry"/> uses <c>Triangulator</c>: on the whole corpus of
    /// polygons of the triangulation proofs, with a fractional translation, the vertices (bit for bit) and the indices are the same.</summary>
    [Fact]
    public void Entry_GivesTheGeometryOfCreateTriangulatedClipGeometry_OnTheCorpusOfPolygons()
    {
        MGTriangulatedClipEntry entry = new("corpus", true);
        Vector2 translation = new(13.37f, -4.25f);
        Rectangle bounds = new(1, 2, 3, 4);
        int compared = 0;
        foreach (MGUI.Tests.Rendering.PolygonCorpus.Case polygon in MGUI.Tests.Rendering.PolygonCorpus.BuildCorpus())
        {
            ClipGeometry expected = MGVectorShapeHelper.CreateTriangulatedClipGeometry(polygon.Vertices, translation);
            ClipDefinition definition = entry.GetDefinition(polygon.Vertices, translation, bounds);
            ClipGeometry actual = definition.Shape.Geometry.Value;
            Assert.True(expected.Vertices.Select(Bits).SequenceEqual(actual.Vertices.Select(Bits)), $"{polygon.Category} / {polygon.Name}: vertices differ");
            Assert.True(expected.Indices.SequenceEqual(actual.Indices), $"{polygon.Category} / {polygon.Name}: indices differ");
            Assert.Equal(bounds, definition.Shape.Bounds);
            Assert.Equal("corpus", definition.DebugName);
            Assert.True(definition.AllowRectangleFallback);
            Assert.True(definition.IntersectWithCurrentClip);
            Assert.Same(definition, entry.GetDefinition(polygon.Vertices, translation, bounds));
            compared++;
        }

        Assert.True(compared > 1000, $"{compared} polygons compared");
    }

    [Fact]
    public void Entry_RebuildsWhenTheSourceTheTranslationOrTheTargetBoundsChange_AndNotOtherwise()
    {
        Vector2[] source = LShape();
        MGTriangulatedClipEntry entry = new("entry", false);
        ClipDefinition first = entry.GetDefinition(source, new Vector2(1, 2), new Rectangle(0, 0, 10, 10));
        Assert.Same(first, entry.GetDefinition(source, new Vector2(1, 2), new Rectangle(0, 0, 10, 10)));
        Assert.NotSame(first, entry.GetDefinition(source, new Vector2(1.5f, 2), new Rectangle(0, 0, 10, 10)));
        ClipDefinition second = entry.GetDefinition(source, new Vector2(1.5f, 2), new Rectangle(0, 0, 10, 10));
        Assert.NotSame(second, entry.GetDefinition(source, new Vector2(1.5f, 2), new Rectangle(0, 0, 10, 11)));
        ClipDefinition third = entry.GetDefinition(source, new Vector2(1.5f, 2), new Rectangle(0, 0, 10, 11));
        Assert.NotSame(third, entry.GetDefinition(LShape(), new Vector2(1.5f, 2), new Rectangle(0, 0, 10, 11)));
        Assert.False(first.AllowRectangleFallback);
    }

    //  ---------------------------------------------------------------------------------------------------------------------------------
    //  Polygons whose winding sum is greater than 0 for the polygon and for its reversal
    //  ---------------------------------------------------------------------------------------------------------------------------------
    //  CreateTriangulatedClipGeometry puts the polygon counter-clockwise (Triangulator.EnsureWindingOrder), then triangulates the result, and Triangulator.Triangulate tests the winding AGAIN on its
    //  input. When the float sum of DetermineWindingOrder is greater than 0 both for a polygon and for its reversal (a signed area about 0: the terms are negated exactly, but added in another
    //  order), the first step reverses and the second reverses back: the triangulated polygon is the ORIGINAL order. A single test of the winding (PolygonTriangulator.Triangulate alone) triangulates
    //  the reversed polygon: other ordered vertices, other indices and, for a self-intersecting polygon, other covered pixels. These polygons are kept here and not in PolygonCorpus: the corpus
    //  is frozen by the fingerprints and the counts of the tests of the backends.

    /// <summary>The polygon of the review (the fourth hit of the reviewer's search), already normalized: a self-intersecting pentagon whose two windings both sum positive.</summary>
    private static Vector2[] ReviewedPolygon() => new Vector2[]
    {
        new(23.770000457763672f, 41.960227966308594f), new(37.09000015258789f, 33.16023254394531f), new(38.45000076293945f, 11.60023021697998f), new(0.0f, 44.73023223876953f), new(38.63999938964844f, 0.0f)
    };

    /// <summary>The other polygons of the reviewer's search (the values of its <c>hits.json</c>, exact float32 values).</summary>
    private static Vector2[][] ReviewersOtherHits() => new[]
    {
        new Vector2[] { new(18.779998779296875f, 4.91796350479126f), new(30.459999084472656f, 12.917964935302734f), new(31.23999786376953f, 25.147964477539062f), new(0.0f, 22.37796401977539f), new(37.15999984741211f, 19.867963790893555f), new(21.59000015258789f, 0.0f) },
        new Vector2[] { new(0.0f, 23.639999389648438f), new(16.240001678466797f, 5.049999713897705f), new(1.8499994277954102f, 44.34000015258789f), new(30.240001678466797f, 31.73999786376953f), new(35.84000015258789f, 0.0f), new(25.880001068115234f, 41.324092864990234f) },
        new Vector2[] { new(0.0f, 19.010000228881836f), new(16.130001068115234f, 0.2999992370605469f), new(25.369998931884766f, 7.809999465942383f), new(25.540000915527344f, 16.159997940063477f), new(5.880001068115234f, 0.0f), new(0.9200000762939453f, 9.402524948120117f) },
        new Vector2[] { new(24.400001525878906f, 2.9200000762939453f), new(28.5f, 8.59000015258789f), new(27.15999984741211f, 16.809999465942383f), new(26.970001220703125f, 22.260000228881836f), new(0.0f, 0.0f), new(1.5200004577636719f, 21.631591796875f) },
        new Vector2[] { new(35.67000198364258f, 20.57000160217285f), new(37.810001373291016f, 3.960000991821289f), new(24.15999984741211f, 0.0f), new(39.68000030517578f, 6.25f), new(17.09000015258789f, 19.57000160217285f), new(0.0f, 4.76603889465332f) },
        new Vector2[] { new(42.59000015258789f, 1.9859447479248047f), new(0.0f, 9.895944595336914f), new(3.119999885559082f, 3.8659439086914062f), new(35.13999938964844f, 22.26594352722168f), new(38.790000915527344f, 21.51594352722168f), new(11.089999198913574f, 0.0f) },
        new Vector2[] { new(0.0f, 20.682199478149414f), new(11.690000534057617f, 29.772201538085938f), new(1.4099998474121094f, 14.322198867797852f), new(20.690000534057617f, 22.35219955444336f), new(9.790000915527344f, 7.852199554443359f), new(34.099998474121094f, 0.0f) },
        new Vector2[] { new(11.90000057220459f, 31.390426635742188f), new(5.720000267028809f, 40.67042541503906f), new(32.72999954223633f, 14.390425682067871f), new(20.770000457763672f, 20.650426864624023f), new(0.0f, 20.91042709350586f), new(12.699999809265137f, 0.0f) },
        new Vector2[] { new(19.400001525878906f, 0.03000020980834961f), new(1.8299999237060547f, 0.0f), new(22.360000610351562f, 11.879999160766602f), new(4.989999771118164f, 17.619998931884766f), new(0.0f, 24.731950759887695f) },
    };

    /// <summary>The test that both windings sum positive: <c>DetermineWindingOrder</c> of MonoGame.Extended (the float sum itself) says clockwise for the polygon and for the polygon reversed the way
    /// <c>ReverseWindingOrder</c> reverses it (the first vertex kept).</summary>
    private static bool BothWindingsSumPositive(Vector2[] polygon)
        => Triangulator.DetermineWindingOrder(polygon) == WindingOrder.Clockwise
        && Triangulator.DetermineWindingOrder(Triangulator.ReverseWindingOrder(polygon)) == WindingOrder.Clockwise;

    private static float Shifted(float value, int ulps)
    {
        for (; ulps > 0; ulps--)
        {
            value = MathF.BitIncrement(value);
        }

        for (; ulps < 0; ulps++)
        {
            value = MathF.BitDecrement(value);
        }

        return value;
    }

    private static Vector2[] Normalized(Vector2[] polygon)
    {
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        for (int i = 0; i < polygon.Length; i++)
        {
            minX = Math.Min(minX, polygon[i].X);
            minY = Math.Min(minY, polygon[i].Y);
        }

        Vector2[] result = new Vector2[polygon.Length];
        for (int i = 0; i < polygon.Length; i++)
        {
            result[i] = new Vector2(polygon[i].X - minX, polygon[i].Y - minY);
        }

        return result;
    }

    private static double SignedSum(Vector2[] polygon)
    {
        double sum = 0;
        Vector2 previous = polygon[^1];
        for (int i = 0; i < polygon.Length; i++)
        {
            sum += ((double)polygon[i].X - previous.X) * ((double)polygon[i].Y + previous.Y);
            previous = polygon[i];
        }

        return sum;
    }

    private static bool SegmentsCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        static double Cross(Vector2 o, Vector2 p, Vector2 q) => ((double)p.X - o.X) * ((double)q.Y - o.Y) - ((double)p.Y - o.Y) * ((double)q.X - o.X);
        double d1 = Cross(c, d, a);
        double d2 = Cross(c, d, b);
        double d3 = Cross(a, b, c);
        double d4 = Cross(a, b, d);
        return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
    }

    private static bool IsSelfIntersecting(Vector2[] polygon)
    {
        int n = polygon.Length;
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 2; j < n; j++)
            {
                if (i == 0 && j == n - 1)
                {
                    continue;
                }

                if (SegmentsCross(polygon[i], polygon[(i + 1) % n], polygon[j], polygon[(j + 1) % n]))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The polygons found by a fixed-seed search: for each trial, points on a grid of hundredths, the y of the last one solved for a signed area of 0 and moved by up to 40 ulps either way,
    /// the polygon moved to the origin; the first one of the trial whose two windings both sum positive is kept. <paramref name="collinear"/>: the points are on a line (a nearly collinear polygon,
    /// in a random order) instead of anywhere (a bow-tie like polygon).</summary>
    private static List<Vector2[]> FindBothWindingsPositive(int seed, int wanted, bool collinear)
    {
        Random random = new(seed);
        List<Vector2[]> found = new();
        for (int trial = 0; trial < 400000 && found.Count < wanted; trial++)
        {
            int n = 4 + random.Next(4);
            Vector2[] points = new Vector2[n];
            double slope = random.NextDouble() * 4 - 2;
            double intercept = random.NextDouble() * 20;
            for (int i = 0; i < n; i++)
            {
                float x = (float)Math.Round(random.NextDouble() * 50, 2);
                float y = collinear ? (float)Math.Round(slope * x + intercept, 2) : (float)Math.Round(random.NextDouble() * 50, 2);
                points[i] = new Vector2(x, y);
            }

            //  The last y that makes the signed area 0 (the area is linear in it); collinear points are already about there.
            float last = points[n - 1].Y;
            if (!collinear)
            {
                points[n - 1].Y = 0f;
                double a0 = SignedSum(points);
                points[n - 1].Y = 1f;
                double a1 = SignedSum(points);
                if (a1 == a0)
                {
                    continue;
                }

                double solved = -a0 / (a1 - a0);
                if (solved < 0 || solved > 50)
                {
                    continue;
                }

                last = (float)solved;
            }

            for (int ulps = -40; ulps <= 40; ulps++)
            {
                Vector2[] candidate = (Vector2[])points.Clone();
                candidate[n - 1].Y = Shifted(last, ulps);
                candidate = Normalized(candidate);
                if (BothWindingsSumPositive(candidate))
                {
                    found.Add(candidate);
                    break;
                }
            }
        }

        return found;
    }

    private static List<(string Name, Vector2[] Polygon)> BothWindingsPositivePolygons()
    {
        List<(string Name, Vector2[] Polygon)> polygons = new() { ("the polygon of the review", ReviewedPolygon()) };
        Vector2[][] others = ReviewersOtherHits();
        for (int i = 0; i < others.Length; i++)
        {
            polygons.Add(($"hit {i} of the review", others[i]));
        }

        List<Vector2[]> bowTies = FindBothWindingsPositive(20261117, 100, collinear: false);
        for (int i = 0; i < bowTies.Count; i++)
        {
            polygons.Add(($"zero-area polygon {i}", bowTies[i]));
        }

        List<Vector2[]> collinear = FindBothWindingsPositive(1117, 40, collinear: true);
        for (int i = 0; i < collinear.Count; i++)
        {
            polygons.Add(($"nearly collinear polygon {i}", collinear[i]));
        }

        return polygons;
    }

    private static bool SameGeometry(ClipGeometry expected, ClipGeometry actual)
        => expected.Vertices.Select(Bits).SequenceEqual(actual.Vertices.Select(Bits)) && expected.Indices.SequenceEqual(actual.Indices);

    private static void AssertSameGeometry(string name, ClipGeometry expected, ClipGeometry actual, string what)
    {
        Assert.True(expected.Vertices.Select(Bits).SequenceEqual(actual.Vertices.Select(Bits)),
            $"{name}: the ordered vertices of {what} differ: expected [{string.Join(' ', expected.Vertices)}], got [{string.Join(' ', actual.Vertices)}]");
        Assert.True(expected.Indices.SequenceEqual(actual.Indices),
            $"{name}: the indices of {what} differ: expected [{string.Join(',', expected.Indices)}], got [{string.Join(',', actual.Indices)}]");
    }

    /// <summary>The polygon of the review, with the values the reviewer gave: the old sequence triangulates the polygon in its original order, a single test of the winding the reversed one.</summary>
    [Fact]
    public void Entry_OnThePolygonOfTheReview_TriangulatesTheOriginalOrder_AsCreateTriangulatedClipGeometryDoes()
    {
        Vector2[] polygon = ReviewedPolygon();
        Assert.True(BothWindingsSumPositive(polygon));

        ClipGeometry old = MGVectorShapeHelper.CreateTriangulatedClipGeometry(polygon, Vector2.Zero);
        Assert.True(polygon.Select(Bits).SequenceEqual(old.Vertices.Select(Bits)), "the old path keeps the original order");
        Assert.Equal(new[] { 3, 4, 2, 4, 0, 2, 0, 1, 2 }, old.Indices.ToArray());

        //  What a single test of the winding gives (PolygonTriangulator alone, proven identical to Triangulator.Triangulate): what the entry must NOT give.
        PolygonTriangulator single = new();
        single.Triangulate(polygon);
        Assert.Equal(new[] { polygon[0], polygon[4], polygon[3], polygon[2], polygon[1] }.Select(Bits), single.Vertices.ToArray().Select(Bits));
        Assert.Equal(new[] { 0, 1, 4, 3, 4, 2, 1, 2, 4 }, single.Indices.ToArray());

        ClipDefinition definition = new MGTriangulatedClipEntry("review", true).GetDefinition(polygon, Vector2.Zero, new Rectangle(0, 0, 40, 45));
        AssertSameGeometry("the polygon of the review", old, definition.Shape.Geometry.Value, "the entry");
    }

    [Fact]
    public void Entry_GivesTheGeometryOfCreateTriangulatedClipGeometry_OnPolygonsWhoseTwoWindingsSumPositive()
    {
        List<(string Name, Vector2[] Polygon)> polygons = BothWindingsPositivePolygons();
        Vector2 translation = new(13.37f, -4.25f);
        int selfIntersecting = 0;
        int differingFromASingleTest = 0;
        int nearlyCollinear = 0;
        List<string> differingFromTheEntry = new();
        PolygonTriangulator single = new();
        for (int i = 0; i < polygons.Count; i++)
        {
            (string name, Vector2[] polygon) = polygons[i];
            Assert.True(BothWindingsSumPositive(polygon), $"{name}: not a polygon whose two windings sum positive");
            if (IsSelfIntersecting(polygon))
            {
                selfIntersecting++;
            }

            if (name.StartsWith("nearly collinear", StringComparison.Ordinal))
            {
                nearlyCollinear++;
            }

            ClipGeometry expected = MGVectorShapeHelper.CreateTriangulatedClipGeometry(polygon, translation);

            //  The family discriminates: a single test of the winding gives other geometry for it.
            single.Triangulate(polygon);
            if (!single.Vertices.ToArray().Select(v => Bits(v + translation)).SequenceEqual(expected.Vertices.Select(Bits)) || !single.Indices.ToArray().SequenceEqual(expected.Indices))
            {
                differingFromASingleTest++;
            }

            MGTriangulatedClipEntry entry = new("both windings", true);
            ClipDefinition definition = entry.GetDefinition(polygon, translation, new Rectangle(0, 0, 60, 60));
            if (!SameGeometry(expected, definition.Shape.Geometry.Value))
            {
                differingFromTheEntry.Add(name);
            }

            Assert.Same(definition, entry.GetDefinition(polygon, translation, new Rectangle(0, 0, 60, 60)));
        }

        Output.WriteLine($"{polygons.Count} polygons whose two windings sum positive ({selfIntersecting} self-intersecting, {nearlyCollinear} nearly collinear); a single test of the winding gives another geometry for {differingFromASingleTest}; the entry differs from CreateTriangulatedClipGeometry for {differingFromTheEntry.Count}");
        Assert.True(differingFromTheEntry.Count == 0, $"the entry gives another geometry than CreateTriangulatedClipGeometry for {differingFromTheEntry.Count} of {polygons.Count} polygons, among them: {string.Join(", ", differingFromTheEntry.Take(5))}");
        Assert.True(polygons.Count >= 150, $"{polygons.Count} polygons");
        Assert.True(selfIntersecting >= 150, $"{selfIntersecting} self-intersecting polygons");
        Assert.True(nearlyCollinear >= 40, $"{nearlyCollinear} nearly collinear polygons");
        Assert.True(differingFromASingleTest >= 150, $"{differingFromASingleTest} polygons for which a single test of the winding gives another geometry");
    }
}
