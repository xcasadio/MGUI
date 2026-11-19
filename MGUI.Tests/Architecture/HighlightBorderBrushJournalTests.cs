using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Config = MGUI.Tests.Architecture.HighlightBorderBrushJournalTests.Config;

namespace MGUI.Tests.Architecture;

/// <summary>
/// T1.4 of the plan "end of the zero allocation of the drawing": the <c>Progress</c> and <c>Scan</c> animations of <see cref="MGHighlightBorderBrush"/> built lists, dictionaries,
/// <c>Distinct().ToList()</c>, a reversed enumeration and arrays at every draw, on the rectangle path (the overload that takes a <see cref="Rectangle"/> and a <see cref="Thickness"/>,
/// reached directly and through the contract overload for a box with square corners or without a ring mesh) and on the ring path (<c>DrawProgressOnRing</c>, <c>DrawScanOnRing</c>, for a box with
/// rounded corners whose geometry has a ring mesh). They now work in buffers that are kept (one set per thread), and must make exactly the same calls, with the same arguments, in the same order,
/// and throw the same exceptions, as the code that allocated.<para/>
/// Two independent proofs, over the same scenes:<list type="bullet">
/// <item><see cref="Frozen"/>: the line count and the SHA-256 (first 16 hex digits) of the journal of each scene, written by an <see cref="OrderedRecordingDrawTransaction"/> (floats at their
/// round-trip form, so a vertex is compared bit for bit), frozen on the code of <c>HEAD</c> before T1.4. A scene is one box (bounds, border thickness, corner radii) and one animation, drawn
/// with every option of the animation (both flow directions and several sizes for <c>Progress</c>; both orientations, both senses and several sizes for <c>Scan</c>), at a few hundred instants:
/// a sweep that puts the head of the progress, and the center of the scan, on every position of the perimeter, one after the other (so exactly on a corner, one before and one after it), the
/// vertices of the contour for the ring, and the instants 0, a value just above 0, a value just below 1, 1. The scenes cross different thicknesses per side, a zero thickness side, equal
/// thicknesses, negative ones, a thickness larger than the box, boxes smaller than the size of the animation, a box whose perimeter is zero, boxes where two corner rectangles are equal
/// (the dictionary of the old code threw), with and without an underlay.</item>
/// <item><see cref="OldHighlightBrushReference"/>: a literal copy of the old code, which every scene (and random inputs, degenerate ones included) is compared with, call by call.</item></list>
/// </summary>
public class HighlightBorderBrushJournalTests
{
    internal enum Kind
    {
        /// <summary>The rectangle overload, called directly: the thickness is used as it is.</summary>
        Bounds,
        /// <summary>The contract overload on a box with square corners: forwards to the rectangle overload.</summary>
        Square,
        /// <summary>The contract overload on a box with rounded corners whose geometry has a ring mesh.</summary>
        Ring,
        /// <summary>The contract overload on a box with rounded corners but no ring mesh (the border consumes the box): forwards to the rectangle overload.</summary>
        Residual
    }

    internal sealed record Scene(Kind Kind, HighlightAnimation Animation, Rectangle Bounds, Thickness Thickness, MGCornerRadius Radius, bool Underlay)
    {
        public string Name => string.Create(CultureInfo.InvariantCulture,
            $"{Kind} {Animation}{(Underlay ? " +underlay" : "")} box=({Bounds.X},{Bounds.Y},{Bounds.Width},{Bounds.Height}) t=({Thickness.Left},{Thickness.Top},{Thickness.Right},{Thickness.Bottom}) r=({Radius.TopLeft},{Radius.TopRight},{Radius.BottomRight},{Radius.BottomLeft})");

        public MGBoxShape Shape => new(Bounds, Thickness, Radius);
    }

    /// <summary>The settings of one animation, read both by a brush and by the reference.</summary>
    internal sealed record Config(HighlightFlowDirection Flow, double ProgressSize, Orientation Orientation, bool Reversed, double ScanSize)
    {
        public Color Color => Color.Red;

        public string Label => string.Create(CultureInfo.InvariantCulture, $"{Flow} {ProgressSize:R} {Orientation} {Reversed} {ScanSize:R}");
    }

    private static readonly Rectangle[] RectangleBoxes =
    {
        new(10, 20, 100, 60), new(0, 0, 64, 64), new(3, 4, 17, 9), new(0, 0, 5, 3), new(7, 7, 2, 2), new(0, 0, 4, 20)
    };

    private static readonly Thickness[] Thicknesses =
    {
        new(4, 6, 8, 10), new(3, 3, 3, 3), new(0, 5, 2, 0), new(0, 0, 0, 0), new(4, 2, 4, 2), new(30, 30, 30, 30), new(-2, 3, 4, -1)
    };

    private static readonly MGCornerRadius NoRadius = new(0);

    private static readonly List<Scene> AllScenes = BuildScenes();

    private static List<Scene> BuildScenes()
    {
        List<Scene> scenes = new();
        foreach (HighlightAnimation animation in new[] { HighlightAnimation.Progress, HighlightAnimation.Scan })
        {
            foreach (Rectangle box in RectangleBoxes)
            {
                foreach (Thickness thickness in Thicknesses)
                {
                    scenes.Add(new Scene(Kind.Bounds, animation, box, thickness, NoRadius, false));
                }
            }

            //  A box with no width and one pixel high has a perimeter of zero (an integer modulo by zero in the old code); one with no size at all has a negative one.
            scenes.Add(new Scene(Kind.Bounds, animation, new Rectangle(5, 5, 0, 1), new Thickness(4, 6, 8, 10), NoRadius, false));
            scenes.Add(new Scene(Kind.Bounds, animation, new Rectangle(5, 5, 0, 1), new Thickness(0, 0, 0, 0), NoRadius, false));
            scenes.Add(new Scene(Kind.Bounds, animation, new Rectangle(0, 0, 0, 0), new Thickness(0, 0, 0, 0), NoRadius, false));
            scenes.Add(new Scene(Kind.Bounds, animation, new Rectangle(5, 5, 0, 8), new Thickness(0, 0, 0, 0), NoRadius, false));
            scenes.Add(new Scene(Kind.Bounds, animation, new Rectangle(0, 0, 1, 1), new Thickness(1, 1, 1, 1), NoRadius, false));

            foreach (Rectangle box in new[] { RectangleBoxes[0], RectangleBoxes[1], RectangleBoxes[2], RectangleBoxes[3] })
            {
                foreach (Thickness thickness in new[] { Thicknesses[0], Thicknesses[1], Thicknesses[2], Thicknesses[5] })
                {
                    scenes.Add(new Scene(Kind.Square, animation, box, thickness, NoRadius, false));
                }
            }

            scenes.Add(new Scene(Kind.Square, animation, new Rectangle(0, 0, 80, 40), new Thickness(40, 40, 40, 40), NoRadius, false));

            foreach ((Rectangle box, Thickness thickness, MGCornerRadius radius) in RingBoxes())
            {
                scenes.Add(new Scene(Kind.Ring, animation, box, thickness, radius, false));
            }

            scenes.Add(new Scene(Kind.Residual, animation, new Rectangle(0, 0, 80, 40), new Thickness(40, 40, 40, 40), new MGCornerRadius(12), false));
            scenes.Add(new Scene(Kind.Residual, animation, new Rectangle(0, 0, 30, 30), new Thickness(15, 15, 15, 15), new MGCornerRadius(5), false));

            scenes.Add(new Scene(Kind.Bounds, animation, RectangleBoxes[0], Thicknesses[0], NoRadius, true));
            scenes.Add(new Scene(Kind.Square, animation, RectangleBoxes[0], Thicknesses[0], NoRadius, true));
            scenes.Add(new Scene(Kind.Ring, animation, new Rectangle(10, 20, 100, 60), new Thickness(4, 6, 8, 10), new MGCornerRadius(12), true));
        }

        return scenes;
    }

    internal static IEnumerable<(Rectangle Box, Thickness Thickness, MGCornerRadius Radius)> RingBoxes()
    {
        yield return (new Rectangle(0, 0, 100, 60), new Thickness(8, 8, 8, 8), new MGCornerRadius(12));
        yield return (new Rectangle(10, 20, 100, 60), new Thickness(4, 6, 8, 10), new MGCornerRadius(12));
        yield return (new Rectangle(0, 0, 100, 60), new Thickness(3, 3, 3, 3), new MGCornerRadius(30));
        yield return (new Rectangle(0, 0, 64, 64), new Thickness(0, 5, 2, 0), new MGCornerRadius(8));
        yield return (new Rectangle(0, 0, 20, 20), new Thickness(2, 2, 2, 2), new MGCornerRadius(8));
        yield return (new Rectangle(0, 0, 6, 6), new Thickness(1, 1, 1, 1), new MGCornerRadius(2));
        yield return (new Rectangle(0, 0, 40, 30), new Thickness(1, 2, 3, 4), new MGCornerRadius(3, 6, 9, 12));
        yield return (new Rectangle(0, 0, 50, 50), new Thickness(5, 5, 5, 5), new MGCornerRadius(25));
    }

    public static IEnumerable<object[]> SceneNames()
    {
        foreach (Scene scene in AllScenes)
        {
            yield return new object[] { scene.Name };
        }
    }

    internal static Scene Find(string name) => AllScenes.Single(x => x.Name == name);

    internal static IReadOnlyList<Scene> Scenes => AllScenes;

    internal static void AssertKind(Scene scene)
    {
        if (scene.Kind == Kind.Bounds)
        {
            return;
        }

        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(scene.Shape);
        switch (scene.Kind)
        {
            case Kind.Square:
                Assert.True(geometry.UsesRectangleFastPath, scene.Name);
                break;
            case Kind.Ring:
                Assert.False(geometry.UsesRectangleFastPath, scene.Name);
                Assert.True(geometry.HasBorderRingMesh, scene.Name);
                break;
            case Kind.Residual:
                Assert.False(geometry.UsesRectangleFastPath, scene.Name);
                Assert.False(geometry.HasBorderRingMesh, scene.Name);
                break;
        }
    }

    #region Options and instants

    internal static IEnumerable<Config> Options(Scene scene)
    {
        if (scene.Animation == HighlightAnimation.Progress)
        {
            foreach (HighlightFlowDirection flow in new[] { HighlightFlowDirection.Clockwise, HighlightFlowDirection.CounterClockwise })
            {
                foreach (double size in new[] { 0.0, 0.05, 0.25, 0.6, 1.0, 1.3 })
                {
                    yield return new Config(flow, size, Orientation.Horizontal, false, 0.2);
                }
            }
        }
        else
        {
            foreach (Orientation orientation in new[] { Orientation.Horizontal, Orientation.Vertical })
            {
                foreach (bool reversed in new[] { false, true })
                {
                    foreach (double size in new[] { 0.0, 0.2, 0.5, 1.0, 1.7 })
                    {
                        yield return new Config(HighlightFlowDirection.Clockwise, 0.25, orientation, reversed, size);
                    }
                }
            }
        }
    }

    private static double Wrap(double x) => x - Math.Floor(x);

    private static readonly double[] SpecialInstants = { 0.0, 1e-9, 0.125, 0.5, 1.0 - 1e-9, 1.0 - 1e-12, 1.0, 2.0 };

    /// <summary>The instants at which a scene draws with an option. The positions are worked out here from the geometry of the box, not read from the brush.</summary>
    internal static List<double> Instants(Scene scene, Config config)
    {
        List<double> instants = new(SpecialInstants);
        if (scene.Animation == HighlightAnimation.Scan)
        {
            int dimension = config.Orientation == Orientation.Horizontal ? scene.Bounds.Height : scene.Bounds.Width;
            int steps = Math.Clamp(Math.Abs(dimension), 2, 400);
            for (int q = 0; q <= steps; q++)
            {
                instants.Add((q + 0.5) / steps);
                instants.Add(q / (double)steps);
            }

            return instants;
        }

        double half = config.ProgressSize / 2.0;
        if (scene.Kind == Kind.Ring)
        {
            MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(scene.Shape);
            for (int k = 0; k < 60; k++)
            {
                instants.Add((k + 0.5) / 60);
            }

            double[] fractions = ArcFractions(geometry, config.Flow);
            for (int k = 0; k < fractions.Length; k++)
            {
                instants.Add(Wrap(fractions[k] + half));
                instants.Add(Wrap(fractions[k] - half));
            }

            return instants;
        }

        //  Rectangle path: the head, then the tail, on every integer position of the outer perimeter, one after the other.
        int perimeter = scene.Bounds.Width * 2 + (scene.Bounds.Height - 1) * 2;
        int positions = Math.Clamp(Math.Abs(perimeter), 4, 400);
        for (int q = 0; q < positions; q++)
        {
            double x = (q + 0.5) / positions;
            instants.Add(Wrap(x - half));
            instants.Add(Wrap(x + half));
        }

        return instants;
    }

    /// <summary>The cumulative arc length at each vertex of the outer contour, from the top-most then left-most vertex, in the sweep order of the flow direction, as a fraction of the total.</summary>
    private static double[] ArcFractions(MGBoxGeometry geometry, HighlightFlowDirection flow)
    {
        IReadOnlyList<Vector2> outer = geometry.OuterContour;
        int n = outer.Count;
        int anchor = OldHighlightBrushReference.FindTopLeftAnchorIndex(outer);
        double[] arc = new double[n + 1];
        for (int k = 1; k <= n; k++)
        {
            int from = flow == HighlightFlowDirection.CounterClockwise ? ((anchor - (k - 1)) % n + n) % n : (anchor + (k - 1)) % n;
            int to = flow == HighlightFlowDirection.CounterClockwise ? ((anchor - k) % n + n) % n : (anchor + k) % n;
            arc[k] = arc[k - 1] + Vector2.Distance(outer[from], outer[to]);
        }

        double[] fractions = new double[n + 1];
        for (int k = 0; k <= n; k++)
        {
            fractions[k] = arc[k] / arc[n];
        }

        return fractions;
    }

    #endregion

    #region Drawing

    /// <summary>A recording transaction, and the lines of one draw (the lines the transaction wrote, then the exception the draw threw, if any).</summary>
    internal sealed class Rig
    {
        private readonly OrderedRecordingDrawTransaction Transaction = new(new GraphTestRuntime(new Rectangle(0, 0, 640, 360)), DrawSettings.Default);
        private readonly List<string> Lines = new();

        public ElementDrawArgs Args => new(new DrawBaseArgs(TimeSpan.Zero, Transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);

        public List<string> Run(Action<ElementDrawArgs> draw)
        {
            Transaction.ClearJournal();
            Lines.Clear();
            string thrown = null;
            try
            {
                draw(Args);
            }
            catch (Exception exception)
            {
                thrown = $"throws {exception.GetType().Name}: {exception.Message}";
            }

            for (int i = 0; i < Transaction.Lines.Count; i++)
            {
                Lines.Add(Transaction.Lines[i]);
            }

            if (thrown != null)
            {
                Lines.Add(thrown);
            }

            return Lines;
        }
    }

    internal static IBorderBrush UnderlayOf(Scene scene) => scene.Underlay ? new MGUniformBorderBrush(Color.Blue) : null;

    internal static MGHighlightBorderBrush MakeBrush(Scene scene, Config config)
        => new(UnderlayOf(scene), config.Color, scene.Animation)
        {
            ProgressFlowDirection = config.Flow,
            ProgressSize = config.ProgressSize,
            ScanOrientation = config.Orientation,
            ScanIsReversed = config.Reversed,
            ScanSize = config.ScanSize
        };

    internal static void DrawWithBrush(ElementDrawArgs args, MGHighlightBorderBrush brush, Scene scene, MGBoxGeometry geometry)
    {
        if (scene.Kind == Kind.Bounds)
        {
            brush.Draw(args, null, scene.Bounds, scene.Thickness);
        }
        else
        {
            brush.Draw(args, null, scene.Shape, geometry);
        }
    }

    internal static MGBoxGeometry GeometryOf(Scene scene) => scene.Kind == Kind.Bounds ? default : MGBoxGeometryBuilder.Build(scene.Shape);

    /// <summary>The old code: the underlay, then the animation, with the path the old code chose (on the rectangle path reached through the contract overload, the underlay is drawn twice: by each overload).</summary>
    internal static void DrawWithReference(ElementDrawArgs args, Scene scene, Config config, MGBoxGeometry geometry, double animationProgress)
    {
        IBorderBrush underlay = UnderlayOf(scene);
        double progress = OldHighlightBrushReference.ActualAnimationProgress(animationProgress);
        if (scene.Kind == Kind.Bounds)
        {
            underlay?.Draw(args, null, scene.Bounds, scene.Thickness);
            DrawRectanglePath(args, scene, config, progress, scene.Bounds, scene.Thickness);
            return;
        }

        MGBoxShape shape = scene.Shape;
        underlay?.Draw(args, null, shape, geometry);
        if (geometry.UsesRectangleFastPath || !geometry.HasBorderRingMesh)
        {
            //  The old code forwards to the rectangle overload, which draws the underlay a second time, with the rectangle overload of the underlay.
            underlay?.Draw(args, null, shape.OuterBounds, shape.NormalizedBorderThickness);
            DrawRectanglePath(args, scene, config, progress, shape.OuterBounds, shape.NormalizedBorderThickness);
        }
        else if (scene.Animation == HighlightAnimation.Progress)
        {
            OldHighlightBrushReference.DrawProgressOnRing(args, config, geometry, progress);
        }
        else
        {
            OldHighlightBrushReference.DrawScanOnRing(args, config, geometry, progress);
        }
    }

    private static void DrawRectanglePath(ElementDrawArgs args, Scene scene, Config config, double progress, Rectangle bounds, Thickness thickness)
    {
        if (scene.Animation == HighlightAnimation.Progress)
        {
            OldHighlightBrushReference.DrawProgressRectangle(args, config, progress, bounds, thickness);
        }
        else
        {
            OldHighlightBrushReference.DrawScanRectangle(args, null, config, progress, bounds, thickness);
        }
    }

    #endregion

    #region Frozen journals

    /// <summary>The line count and the hash of everything a scene draws, option after option, instant after instant. A marker line precedes each draw.</summary>
    internal static (int Lines, string Hash) Fingerprint(Scene scene)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        int count = 0;
        void Add(string line)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(line));
            hash.AppendData(NewLine);
            count++;
        }

        Rig rig = new();
        MGBoxGeometry geometry = GeometryOf(scene);
        foreach (Config config in Options(scene))
        {
            MGHighlightBorderBrush highlight = MakeBrush(scene, config);
            foreach (double instant in Instants(scene, config))
            {
                Add($"@ {config.Label} {instant.ToString("R", CultureInfo.InvariantCulture)}");
                highlight.AnimationProgress = instant;
                foreach (string line in rig.Run(args => DrawWithBrush(args, highlight, scene, geometry)))
                {
                    Add(line);
                }
            }
        }

        return (count, Convert.ToHexString(hash.GetHashAndReset())[..16]);
    }

    private static readonly byte[] NewLine = { (byte)'\n' };

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void TheJournal_IsTheOneOfTheOldCode(string name)
    {
        Scene scene = Find(name);
        AssertKind(scene);
        (int lines, string hash) = Fingerprint(scene);
        Assert.True(Frozen.TryGetValue(name, out (int Lines, string Hash) expected), $"no frozen journal for {name}");
        Assert.True(expected.Lines == lines && expected.Hash == hash, $"{name}: expected {expected.Lines} lines, hash {expected.Hash}; got {lines} lines, hash {hash}");
    }

    #endregion

    #region Differential

    internal static void AssertSameAsReference(Scene scene)
    {
        AssertKind(scene);
        Rig brushRig = new();
        Rig referenceRig = new();
        MGBoxGeometry geometry = GeometryOf(scene);
        int draws = 0;
        foreach (Config config in Options(scene))
        {
            MGHighlightBorderBrush brush = MakeBrush(scene, config);
            foreach (double instant in Instants(scene, config))
            {
                brush.AnimationProgress = instant;
                List<string> actual = brushRig.Run(args => DrawWithBrush(args, brush, scene, geometry));
                List<string> expected = referenceRig.Run(args => DrawWithReference(args, scene, config, geometry, instant));
                draws++;
                if (!actual.SequenceEqual(expected))
                {
                    int at = 0;
                    while (at < actual.Count && at < expected.Count && actual[at] == expected[at])
                    {
                        at++;
                    }

                    Assert.Fail($"{scene.Name}, option {config.Label}, instant {instant:R}: line {at} differs\nnew: {(at < actual.Count ? actual[at] : "(end)")}\nold: {(at < expected.Count ? expected[at] : "(end)")}\nnew has {actual.Count} line(s), old has {expected.Count}");
                }
            }
        }

        Assert.True(draws > 0);
    }

    [Theory]
    [MemberData(nameof(SceneNames))]
    public void TheJournal_IsTheOneOfTheOldCodeCopiedLiterally(string name) => AssertSameAsReference(Find(name));

    #endregion

    #region Random inputs

    private static double PickSize(Random random) => random.Next(5) switch
    {
        0 => 0.0,
        1 => 1.0,
        2 => 0.05,
        3 => random.NextDouble() * 1.5,
        _ => 0.25
    };

    private static double PickInstant(Random random)
        => random.Next(6) == 0 ? SpecialInstants[random.Next(SpecialInstants.Length)] : random.NextDouble();

    private static Config RandomConfig(Random random)
        => new(random.Next(2) == 0 ? HighlightFlowDirection.Clockwise : HighlightFlowDirection.CounterClockwise, PickSize(random),
            random.Next(2) == 0 ? Orientation.Horizontal : Orientation.Vertical, random.Next(2) == 0, PickSize(random));

    /// <summary>Random boxes and thicknesses (zero and negative sizes, thicknesses larger than the box) on the rectangle overload: the new code and the old one draw the same, or throw the same exception.</summary>
    [Fact]
    public void RandomInputs_OnTheRectangleOverload_GiveWhatTheOldCodeGives()
    {
        Random random = new(20261117);
        Rig brushRig = new();
        Rig referenceRig = new();
        int thrown = 0;
        int compared = 0;
        for (int i = 0; i < 6000; i++)
        {
            Rectangle box = new(random.Next(-5, 30), random.Next(-5, 30), random.Next(0, 70), random.Next(0, 50));
            Thickness thickness = new(random.Next(-2, 12), random.Next(-2, 12), random.Next(-2, 12), random.Next(-2, 12));
            switch (random.Next(8))
            {
                case 0:
                    //  Left and right as thick as the box is wide: the top-left and top-right corner rectangles are equal.
                    thickness = new Thickness(box.Width, thickness.Top, box.Width, thickness.Bottom);
                    break;
                case 1:
                    thickness = new Thickness(thickness.Left, box.Height, thickness.Right, box.Height);
                    break;
                case 2:
                    box.Width = 0;
                    thickness = new Thickness(0, thickness.Top, 0, thickness.Bottom);
                    break;
            }

            Scene scene = new(Kind.Bounds, random.Next(2) == 0 ? HighlightAnimation.Progress : HighlightAnimation.Scan, box, thickness, NoRadius, random.Next(4) == 0);
            Config config = RandomConfig(random);
            double instant = PickInstant(random);
            MGHighlightBorderBrush brush = MakeBrush(scene, config);
            brush.AnimationProgress = instant;
            List<string> actual = brushRig.Run(args => DrawWithBrush(args, brush, scene, default));
            List<string> expected = referenceRig.Run(args => DrawWithReference(args, scene, config, default, instant));
            Assert.True(actual.SequenceEqual(expected), $"{scene.Name}, {config.Label}, instant {instant:R}\nnew:\n{string.Join('\n', actual)}\nold:\n{string.Join('\n', expected)}");
            compared++;
            thrown += expected.Count > 0 && expected[^1].StartsWith("throws ", StringComparison.Ordinal) ? 1 : 0;
        }

        //  The inputs reach both the draws and the exceptions of the old code.
        Assert.True(thrown > 200 && compared - thrown > 1000, $"{thrown} of {compared} draws threw");
    }

    /// <summary>Random boxes with random corner radii on the contract overload: the square-corner box, the residual box and the ring each take their path, as the old code did.</summary>
    [Fact]
    public void RandomInputs_OnTheContractOverload_GiveWhatTheOldCodeGives()
    {
        Random random = new(20261118);
        Rig brushRig = new();
        Rig referenceRig = new();
        int rings = 0;
        int fastPaths = 0;
        int residuals = 0;
        for (int i = 0; i < 500; i++)
        {
            Rectangle box = new(random.Next(-5, 30), random.Next(-5, 30), random.Next(4, 90), random.Next(4, 70));
            Thickness thickness = new(random.Next(0, 10), random.Next(0, 10), random.Next(0, 10), random.Next(0, 10));
            MGCornerRadius radius = random.Next(5) == 0 ? new MGCornerRadius(0) : new MGCornerRadius(random.Next(0, 36), random.Next(0, 36), random.Next(0, 36), random.Next(0, 36));
            Scene scene = new(Kind.Ring, random.Next(2) == 0 ? HighlightAnimation.Progress : HighlightAnimation.Scan, box, thickness, radius, random.Next(4) == 0);
            MGBoxGeometry geometry = GeometryOf(scene);
            if (geometry.UsesRectangleFastPath)
            {
                fastPaths++;
            }
            else if (geometry.HasBorderRingMesh)
            {
                rings++;
            }
            else
            {
                residuals++;
            }

            for (int k = 0; k < 8; k++)
            {
                Config config = RandomConfig(random);
                double instant = PickInstant(random);
                MGHighlightBorderBrush brush = MakeBrush(scene, config);
                brush.AnimationProgress = instant;
                List<string> actual = brushRig.Run(args => DrawWithBrush(args, brush, scene, geometry));
                List<string> expected = referenceRig.Run(args => DrawWithReference(args, scene, config, geometry, instant));
                Assert.True(actual.SequenceEqual(expected), $"{scene.Name}, {config.Label}, instant {instant:R}\nnew:\n{string.Join('\n', actual)}\nold:\n{string.Join('\n', expected)}");
            }
        }

        Assert.True(rings > 100 && fastPaths > 20, $"{rings} rings, {fastPaths} square-corner boxes, {residuals} residual boxes");
    }

    #endregion

    #region Degenerate inputs

    /// <summary>The old code kept the four corner rectangles in a dictionary built from literals: two equal rectangles made it throw (with a thickness as wide as the box, or on a box without width).
    /// The expectation is the exception of a dictionary built the same way, in the test.</summary>
    [Theory]
    [InlineData(0, 0, 4, 20, 4, 2, 4, 2)]
    [InlineData(5, 5, 0, 8, 0, 0, 0, 0)]
    [InlineData(2, 3, 20, 6, 3, 6, 5, 6)]
    public void TwoEqualCornerRectangles_ThrowTheExceptionOfTheDictionaryOfTheOldCode(int x, int y, int width, int height, int left, int top, int right, int bottom)
    {
        Rectangle bounds = new(x, y, width, height);
        Rectangle topLeft = new(bounds.Left, bounds.Top, left, top);
        Rectangle topRight = new(bounds.Right - right, bounds.Top, right, top);
        Rectangle bottomRight = new(bounds.Right - right, bounds.Bottom - bottom, right, bottom);
        Rectangle bottomLeft = new(bounds.Left, bounds.Bottom - bottom, left, bottom);
        Rectangle[] corners = { topLeft, topRight, bottomRight, bottomLeft };
        bool found = false;
        for (int i = 0; i < corners.Length && !found; i++)
        {
            for (int j = i + 1; j < corners.Length && !found; j++)
            {
                if (corners[i] == corners[j])
                {
                    found = true;
                }
            }
        }

        Assert.True(found, "the case does not make two corner rectangles equal");

        //  What a Dictionary does when its initializer adds the same key twice, worked out by a dictionary.
        string expectedMessage = null;
        try
        {
            Dictionary<Rectangle, Point> dictionary = new() { { topLeft, Point.Zero }, { topRight, Point.Zero }, { bottomRight, Point.Zero }, { bottomLeft, Point.Zero } };
            Assert.Fail("a dictionary accepted a duplicate key");
        }
        catch (ArgumentException exception)
        {
            expectedMessage = exception.Message;
        }

        Scene scene = new(Kind.Bounds, HighlightAnimation.Progress, bounds, new Thickness(left, top, right, bottom), NoRadius, true);
        Rig rig = new();
        int thrown = 0;
        foreach (HighlightFlowDirection flow in new[] { HighlightFlowDirection.Clockwise, HighlightFlowDirection.CounterClockwise })
        {
            foreach (double instant in new[] { 0.0, 0.3, 0.55, 0.9 })
            {
                MGHighlightBorderBrush brush = MakeBrush(scene, new Config(flow, 0.25, Orientation.Horizontal, false, 0.2));
                brush.AnimationProgress = instant;
                List<string> lines = rig.Run(args => DrawWithBrush(args, brush, scene, default));
                if (lines[^1].StartsWith("throws ", StringComparison.Ordinal))
                {
                    thrown++;
                    Assert.Equal($"throws ArgumentException: {expectedMessage}", lines[^1]);
                    //  Nothing of the highlight was drawn: only the underlay.
                    Assert.DoesNotContain(lines, x => x.StartsWith("FillPolygon/3 ", StringComparison.Ordinal));
                }
            }
        }

        //  A flow that reaches the dictionary draws nothing and throws, as before; a span of the perimeter that throws earlier (a modulo by zero) is the old behaviour too (the frozen scenes).
        Assert.True(thrown > 0 || (bounds.Width == 0 && bounds.Height == 1), $"no draw threw for {bounds}");
    }

    #endregion

    /// <summary>A flow direction or an orientation that is not one of the two the enumeration names: the old code threw only when the progress passed a corner (a flow direction) or when the scan
    /// had an edge to draw on (an orientation), and not at all on the ring for a flow direction. The same draws throw, with the same message.</summary>
    [Fact]
    public void AnUnrecognizedFlowDirectionOrOrientation_ThrowsWhatTheOldCodeThrows()
    {
        Rig brushRig = new();
        Rig referenceRig = new();
        int thrown = 0;
        int drawn = 0;
        (Rectangle Box, Thickness Thickness, MGCornerRadius Radius)[] rings = RingBoxes().ToArray();
        foreach (Kind kind in new[] { Kind.Bounds, Kind.Square, Kind.Ring })
        {
            foreach (HighlightAnimation animation in new[] { HighlightAnimation.Progress, HighlightAnimation.Scan })
            {
                Scene scene = kind == Kind.Ring
                    ? new Scene(kind, animation, rings[0].Box, rings[0].Thickness, rings[0].Radius, false)
                    : new Scene(kind, animation, RectangleBoxes[0], Thicknesses[0], NoRadius, false);
                Config config = animation == HighlightAnimation.Progress
                    ? new Config((HighlightFlowDirection)7, 0.25, Orientation.Horizontal, false, 0.2)
                    : new Config(HighlightFlowDirection.Clockwise, 0.25, (Orientation)9, false, 0.2);
                MGBoxGeometry geometry = GeometryOf(scene);
                MGHighlightBorderBrush brush = MakeBrush(scene, config);
                foreach (double instant in new[] { 0.0, 0.1, 0.3, 0.55, 0.8, 0.95 })
                {
                    brush.AnimationProgress = instant;
                    List<string> actual = brushRig.Run(args => DrawWithBrush(args, brush, scene, geometry));
                    List<string> expected = referenceRig.Run(args => DrawWithReference(args, scene, config, geometry, instant));
                    Assert.True(actual.SequenceEqual(expected), $"{scene.Name}, {config.Label}, instant {instant:R}\nnew:\n{string.Join('\n', actual)}\nold:\n{string.Join('\n', expected)}");
                    if (expected.Count > 0 && expected[^1].StartsWith("throws NotImplementedException: ", StringComparison.Ordinal))
                    {
                        thrown++;
                    }
                    else
                    {
                        drawn++;
                    }
                }
            }
        }

        Assert.True(thrown > 0 && drawn > 0, $"{thrown} draws threw, {drawn} did not");
    }

    /// <summary>A box without width and one pixel high has an outer perimeter of zero: the integer modulo of the old code divided by zero, at every instant, before anything was drawn.</summary>
    [Fact]
    public void ABoxWithAPerimeterOfZero_ThrowsTheDivisionByZeroOfTheOldCode()
    {
        int zero = 0;
        string expectedMessage = Assert.Throws<DivideByZeroException>(() => 5 % zero).Message;
        Scene scene = new(Kind.Bounds, HighlightAnimation.Progress, new Rectangle(5, 5, 0, 1), new Thickness(4, 6, 8, 10), NoRadius, true);
        Rig rig = new();
        foreach (double instant in new[] { 0.0, 0.3, 0.7, 1.0 })
        {
            MGHighlightBorderBrush brush = MakeBrush(scene, new Config(HighlightFlowDirection.Clockwise, 0.25, Orientation.Horizontal, false, 0.2));
            brush.AnimationProgress = instant;
            List<string> lines = rig.Run(args => DrawWithBrush(args, brush, scene, default));
            Assert.Equal($"throws DivideByZeroException: {expectedMessage}", lines[^1]);
            Assert.DoesNotContain(lines, x => x.StartsWith("FillPolygon/3 ", StringComparison.Ordinal));
        }
    }

    #region One brush, several boxes

    private static IEnumerable<(string Name, Scene First, Scene Second)> Pairs()
    {
        Scene Make(Kind kind, HighlightAnimation animation, Rectangle box, Thickness thickness, MGCornerRadius radius) => new(kind, animation, box, thickness, radius, false);
        (Rectangle Box, Thickness Thickness, MGCornerRadius Radius)[] rings = RingBoxes().ToArray();
        foreach (HighlightAnimation animation in new[] { HighlightAnimation.Progress, HighlightAnimation.Scan })
        {
            yield return ($"{animation}: big then small rectangle", Make(Kind.Bounds, animation, RectangleBoxes[0], Thicknesses[0], NoRadius), Make(Kind.Bounds, animation, RectangleBoxes[2], Thicknesses[2], NoRadius));
            yield return ($"{animation}: two rectangles of the same size and different thicknesses", Make(Kind.Bounds, animation, RectangleBoxes[1], Thicknesses[1], NoRadius), Make(Kind.Bounds, animation, RectangleBoxes[1], Thicknesses[0], NoRadius));
            yield return ($"{animation}: a rectangle and a box that makes the dictionary throw", Make(Kind.Bounds, animation, RectangleBoxes[0], Thicknesses[0], NoRadius), Make(Kind.Bounds, animation, RectangleBoxes[5], Thicknesses[4], NoRadius));
            yield return ($"{animation}: big then small ring", Make(Kind.Ring, animation, rings[0].Box, rings[0].Thickness, rings[0].Radius), Make(Kind.Ring, animation, rings[5].Box, rings[5].Thickness, rings[5].Radius));
            yield return ($"{animation}: two rings of different contours", Make(Kind.Ring, animation, rings[1].Box, rings[1].Thickness, rings[1].Radius), Make(Kind.Ring, animation, rings[3].Box, rings[3].Thickness, rings[3].Radius));
            yield return ($"{animation}: a ring then a square-corner box", Make(Kind.Ring, animation, rings[0].Box, rings[0].Thickness, rings[0].Radius), Make(Kind.Square, animation, RectangleBoxes[0], Thicknesses[0], NoRadius));
            yield return ($"{animation}: a square-corner box then a ring", Make(Kind.Square, animation, RectangleBoxes[1], Thicknesses[1], NoRadius), Make(Kind.Ring, animation, rings[7].Box, rings[7].Thickness, rings[7].Radius));
        }
    }

    public static IEnumerable<object[]> PairNames()
    {
        foreach ((string name, Scene _, Scene _) in Pairs())
        {
            yield return new object[] { name };
        }
    }

    private static List<string> DrawAlone(Scene scene, Config config, double instant)
    {
        MGHighlightBorderBrush brush = MakeBrush(scene, config);
        brush.AnimationProgress = instant;
        MGBoxGeometry geometry = GeometryOf(scene);
        return new List<string>(new Rig().Run(args => DrawWithBrush(args, brush, scene, geometry)));
    }

    /// <summary>The buffers are refilled at each draw: one brush that draws two different boxes in turn gives, for each box, the journal that a brush gives when it draws that box alone.</summary>
    [Theory]
    [MemberData(nameof(PairNames))]
    public void OneBrushDrawingTwoBoxesInTurn_GivesEachBoxTheJournalItGivesAlone(string name)
    {
        (string _, Scene first, Scene second) = Pairs().Single(x => x.Name == name);
        Rig rig = new();
        MGBoxGeometry firstGeometry = GeometryOf(first);
        MGBoxGeometry secondGeometry = GeometryOf(second);
        List<Config> configs = Options(first).ToList();
        int compared = 0;
        for (int c = 0; c < configs.Count; c += 3)
        {
            Config config = configs[c];
            MGHighlightBorderBrush shared = MakeBrush(first, config);
            List<double> instants = Instants(first, config);
            for (int i = 0; i < instants.Count; i += 5)
            {
                double instant = instants[i];
                shared.AnimationProgress = instant;
                List<string> aloneFirst = DrawAlone(first, config, instant);
                List<string> aloneSecond = DrawAlone(second, config, instant);

                //  first, second, second, first: both transitions, and a box drawn twice in a row.
                Assert.Equal(aloneFirst, new List<string>(rig.Run(args => DrawWithBrush(args, shared, first, firstGeometry))));
                Assert.Equal(aloneSecond, new List<string>(rig.Run(args => DrawWithBrush(args, shared, second, secondGeometry))));
                Assert.Equal(aloneSecond, new List<string>(rig.Run(args => DrawWithBrush(args, shared, second, secondGeometry))));
                Assert.Equal(aloneFirst, new List<string>(rig.Run(args => DrawWithBrush(args, shared, first, firstGeometry))));
                compared++;
            }
        }

        Assert.True(compared > 20);
    }

    /// <summary>A copy of the brush, and the brush, drawn in turn on two boxes: nothing kept by one reaches the other.</summary>
    [Theory]
    [MemberData(nameof(PairNames))]
    public void ABrushAndItsCopy_DrawingInTurn_GiveTheJournalsTheyGiveAlone(string name)
    {
        (string _, Scene first, Scene second) = Pairs().Single(x => x.Name == name);
        Rig rig = new();
        MGBoxGeometry firstGeometry = GeometryOf(first);
        MGBoxGeometry secondGeometry = GeometryOf(second);
        List<Config> configs = Options(first).ToList();
        for (int c = 1; c < configs.Count; c += 4)
        {
            Config config = configs[c];
            MGHighlightBorderBrush original = MakeBrush(first, config);
            List<double> instants = Instants(first, config);
            for (int i = 1; i < instants.Count; i += 7)
            {
                double instant = instants[i];
                original.AnimationProgress = instant;
                MGHighlightBorderBrush copy = (MGHighlightBorderBrush)original.Copy();
                Assert.NotSame(original, copy);
                List<string> aloneFirst = DrawAlone(first, config, instant);
                List<string> aloneSecond = DrawAlone(second, config, instant);
                Assert.Equal(aloneFirst, new List<string>(rig.Run(args => DrawWithBrush(args, original, first, firstGeometry))));
                Assert.Equal(aloneSecond, new List<string>(rig.Run(args => DrawWithBrush(args, copy, second, secondGeometry))));
                Assert.Equal(aloneSecond, new List<string>(rig.Run(args => DrawWithBrush(args, original, second, secondGeometry))));
                Assert.Equal(aloneFirst, new List<string>(rig.Run(args => DrawWithBrush(args, copy, first, firstGeometry))));
            }
        }
    }

    /// <summary>A frozen brush is shared by the elements that use it: two threads that draw two boxes with it at the same time each get the journal they get alone (the buffers are kept per thread).</summary>
    [Theory]
    [MemberData(nameof(PairNames))]
    public void AFrozenBrush_DrawnByTwoThreadsAtOnce_GivesEachBoxItsJournal(string name)
    {
        (string _, Scene first, Scene second) = Pairs().Single(x => x.Name == name);
        Config config = Options(first).ElementAt(Options(first).Count() / 2);
        double instant = 0.37;
        List<string> aloneFirst = DrawAlone(first, config, instant);
        List<string> aloneSecond = DrawAlone(second, config, instant);
        MGHighlightBorderBrush shared = MakeBrush(first, config);
        shared.AnimationProgress = instant;
        shared.Freeze();
        MGBoxGeometry firstGeometry = GeometryOf(first);
        MGBoxGeometry secondGeometry = GeometryOf(second);

        List<string> failures = new();
        using Barrier barrier = new(2);
        void Run(Scene scene, MGBoxGeometry geometry, List<string> expected)
        {
            //  An exception on a worker thread would take the test host down, and a draw that never ends would hang the suite: both become a failure of this test.
            try
            {
                Rig rig = new();
                if (!barrier.SignalAndWait(TimeSpan.FromSeconds(30)))
                {
                    lock (failures)
                    {
                        failures.Add($"{scene.Name}: the other thread did not start");
                    }

                    return;
                }

                for (int i = 0; i < 400; i++)
                {
                    List<string> lines = rig.Run(args => DrawWithBrush(args, shared, scene, geometry));
                    if (!lines.SequenceEqual(expected))
                    {
                        lock (failures)
                        {
                            failures.Add($"{scene.Name}, draw {i}: {lines.Count} line(s), expected {expected.Count}");
                        }

                        return;
                    }
                }
            }
            catch (Exception exception)
            {
                lock (failures)
                {
                    failures.Add($"{scene.Name}: {exception.GetType().Name}: {exception.Message}");
                }
            }
        }

        //  Background threads: one that is stuck in a draw does not keep the test host alive after the test has failed.
        Thread a = new(() => Run(first, firstGeometry, aloneFirst)) { IsBackground = true };
        Thread b = new(() => Run(second, secondGeometry, aloneSecond)) { IsBackground = true };
        a.Start();
        b.Start();
        bool firstEnded = a.Join(TimeSpan.FromSeconds(120));
        bool secondEnded = b.Join(TimeSpan.FromSeconds(120));
        Assert.True(firstEnded && secondEnded, "a thread was still drawing after 120 seconds: the draws of two threads disturb each other");
        lock (failures)
        {
            Assert.True(failures.Count == 0, string.Join("; ", failures));
        }
    }

    #endregion


    private static readonly Dictionary<string, (int Lines, string Hash)> Frozen = new()
    {
        ["Bounds Progress box=(10,20,100,60) t=(4,6,8,10) r=(0,0,0,0)"] = (15456, "A134ED8EFF8535A3"),
        ["Bounds Progress box=(10,20,100,60) t=(3,3,3,3) r=(0,0,0,0)"] = (15456, "E1E993EF623E4A2A"),
        ["Bounds Progress box=(10,20,100,60) t=(0,5,2,0) r=(0,0,0,0)"] = (15456, "348D0B9CE9F292AD"),
        ["Bounds Progress box=(10,20,100,60) t=(0,0,0,0) r=(0,0,0,0)"] = (15456, "AE0EBD9C8048B8DC"),
        ["Bounds Progress box=(10,20,100,60) t=(4,2,4,2) r=(0,0,0,0)"] = (15456, "EC5A09EF47C0BD78"),
        ["Bounds Progress box=(10,20,100,60) t=(30,30,30,30) r=(0,0,0,0)"] = (15456, "AA3AD5B45784BCA4"),
        ["Bounds Progress box=(10,20,100,60) t=(-2,3,4,-1) r=(0,0,0,0)"] = (15456, "7D2CC916289FCB87"),
        ["Bounds Progress box=(0,0,64,64) t=(4,6,8,10) r=(0,0,0,0)"] = (12384, "93EB9CAEAF8642CD"),
        ["Bounds Progress box=(0,0,64,64) t=(3,3,3,3) r=(0,0,0,0)"] = (12384, "CBCF5B488D1C370C"),
        ["Bounds Progress box=(0,0,64,64) t=(0,5,2,0) r=(0,0,0,0)"] = (12384, "56E77986605FC5AD"),
        ["Bounds Progress box=(0,0,64,64) t=(0,0,0,0) r=(0,0,0,0)"] = (12384, "386E047647079C65"),
        ["Bounds Progress box=(0,0,64,64) t=(4,2,4,2) r=(0,0,0,0)"] = (12384, "747D5291D3B32518"),
        ["Bounds Progress box=(0,0,64,64) t=(30,30,30,30) r=(0,0,0,0)"] = (12384, "A058F60E15E20397"),
        ["Bounds Progress box=(0,0,64,64) t=(-2,3,4,-1) r=(0,0,0,0)"] = (12384, "E2B143879156BE96"),
        ["Bounds Progress box=(3,4,17,9) t=(4,6,8,10) r=(0,0,0,0)"] = (2592, "A8A3DB47C6AC695C"),
        ["Bounds Progress box=(3,4,17,9) t=(3,3,3,3) r=(0,0,0,0)"] = (2592, "BC8EB6D5D687F754"),
        ["Bounds Progress box=(3,4,17,9) t=(0,5,2,0) r=(0,0,0,0)"] = (2592, "B0474B0B657F95E8"),
        ["Bounds Progress box=(3,4,17,9) t=(0,0,0,0) r=(0,0,0,0)"] = (2592, "7BCC66ACE577C3FF"),
        ["Bounds Progress box=(3,4,17,9) t=(4,2,4,2) r=(0,0,0,0)"] = (2592, "1A60B6C66B922D4A"),
        ["Bounds Progress box=(3,4,17,9) t=(30,30,30,30) r=(0,0,0,0)"] = (2592, "26A25DEBFA03174D"),
        ["Bounds Progress box=(3,4,17,9) t=(-2,3,4,-1) r=(0,0,0,0)"] = (2592, "E3633A5D5AB80293"),
        ["Bounds Progress box=(0,0,5,3) t=(4,6,8,10) r=(0,0,0,0)"] = (864, "BCEF45502247E8B0"),
        ["Bounds Progress box=(0,0,5,3) t=(3,3,3,3) r=(0,0,0,0)"] = (864, "AF6426CF9C3DB5B5"),
        ["Bounds Progress box=(0,0,5,3) t=(0,5,2,0) r=(0,0,0,0)"] = (864, "A510B77589179C3A"),
        ["Bounds Progress box=(0,0,5,3) t=(0,0,0,0) r=(0,0,0,0)"] = (864, "DE7D7558FA9BE0BF"),
        ["Bounds Progress box=(0,0,5,3) t=(4,2,4,2) r=(0,0,0,0)"] = (864, "2E7A0531472CCCDA"),
        ["Bounds Progress box=(0,0,5,3) t=(30,30,30,30) r=(0,0,0,0)"] = (864, "1FCFD316DE94449B"),
        ["Bounds Progress box=(0,0,5,3) t=(-2,3,4,-1) r=(0,0,0,0)"] = (864, "045C5424F10CD192"),
        ["Bounds Progress box=(7,7,2,2) t=(4,6,8,10) r=(0,0,0,0)"] = (480, "68C3A08572977599"),
        ["Bounds Progress box=(7,7,2,2) t=(3,3,3,3) r=(0,0,0,0)"] = (480, "32B4F574AFA8B834"),
        ["Bounds Progress box=(7,7,2,2) t=(0,5,2,0) r=(0,0,0,0)"] = (480, "E9488A32B89A9607"),
        ["Bounds Progress box=(7,7,2,2) t=(0,0,0,0) r=(0,0,0,0)"] = (480, "1DC375248DA7029E"),
        ["Bounds Progress box=(7,7,2,2) t=(4,2,4,2) r=(0,0,0,0)"] = (480, "F1AB869D8AA0C29D"),
        ["Bounds Progress box=(7,7,2,2) t=(30,30,30,30) r=(0,0,0,0)"] = (480, "419B0C852574F3F3"),
        ["Bounds Progress box=(7,7,2,2) t=(-2,3,4,-1) r=(0,0,0,0)"] = (480, "C3E09FEC8FAAB328"),
        ["Bounds Progress box=(0,0,4,20) t=(4,6,8,10) r=(0,0,0,0)"] = (2400, "14D11EF93542419C"),
        ["Bounds Progress box=(0,0,4,20) t=(3,3,3,3) r=(0,0,0,0)"] = (2400, "E078AC940864727D"),
        ["Bounds Progress box=(0,0,4,20) t=(0,5,2,0) r=(0,0,0,0)"] = (2400, "18B2AFC003910562"),
        ["Bounds Progress box=(0,0,4,20) t=(0,0,0,0) r=(0,0,0,0)"] = (2400, "FD524A4CA4D2047B"),
        ["Bounds Progress box=(0,0,4,20) t=(4,2,4,2) r=(0,0,0,0)"] = (2400, "60F6EB45A9518722"),
        ["Bounds Progress box=(0,0,4,20) t=(30,30,30,30) r=(0,0,0,0)"] = (2400, "1BABF933FA01A7E3"),
        ["Bounds Progress box=(0,0,4,20) t=(-2,3,4,-1) r=(0,0,0,0)"] = (2400, "E3E188F4F9D09EF8"),
        ["Bounds Progress box=(5,5,0,1) t=(4,6,8,10) r=(0,0,0,0)"] = (384, "D95E69CEE4574B52"),
        ["Bounds Progress box=(5,5,0,1) t=(0,0,0,0) r=(0,0,0,0)"] = (384, "D95E69CEE4574B52"),
        ["Bounds Progress box=(0,0,0,0) t=(0,0,0,0) r=(0,0,0,0)"] = (384, "246A0EAA2A9FF766"),
        ["Bounds Progress box=(5,5,0,8) t=(0,0,0,0) r=(0,0,0,0)"] = (864, "6E6365BCAB53E113"),
        ["Bounds Progress box=(0,0,1,1) t=(1,1,1,1) r=(0,0,0,0)"] = (384, "99B4D70B30081691"),
        ["Square Progress box=(10,20,100,60) t=(4,6,8,10) r=(0,0,0,0)"] = (15456, "A134ED8EFF8535A3"),
        ["Square Progress box=(10,20,100,60) t=(3,3,3,3) r=(0,0,0,0)"] = (15456, "E1E993EF623E4A2A"),
        ["Square Progress box=(10,20,100,60) t=(0,5,2,0) r=(0,0,0,0)"] = (15456, "348D0B9CE9F292AD"),
        ["Square Progress box=(10,20,100,60) t=(30,30,30,30) r=(0,0,0,0)"] = (15456, "AA3AD5B45784BCA4"),
        ["Square Progress box=(0,0,64,64) t=(4,6,8,10) r=(0,0,0,0)"] = (12384, "93EB9CAEAF8642CD"),
        ["Square Progress box=(0,0,64,64) t=(3,3,3,3) r=(0,0,0,0)"] = (12384, "CBCF5B488D1C370C"),
        ["Square Progress box=(0,0,64,64) t=(0,5,2,0) r=(0,0,0,0)"] = (12384, "56E77986605FC5AD"),
        ["Square Progress box=(0,0,64,64) t=(30,30,30,30) r=(0,0,0,0)"] = (12384, "A058F60E15E20397"),
        ["Square Progress box=(3,4,17,9) t=(4,6,8,10) r=(0,0,0,0)"] = (2592, "2326906733762AE1"),
        ["Square Progress box=(3,4,17,9) t=(3,3,3,3) r=(0,0,0,0)"] = (2592, "BC8EB6D5D687F754"),
        ["Square Progress box=(3,4,17,9) t=(0,5,2,0) r=(0,0,0,0)"] = (2592, "B0474B0B657F95E8"),
        ["Square Progress box=(3,4,17,9) t=(30,30,30,30) r=(0,0,0,0)"] = (2592, "7B6880A25151ED26"),
        ["Square Progress box=(0,0,5,3) t=(4,6,8,10) r=(0,0,0,0)"] = (864, "B70B7E651CD149BE"),
        ["Square Progress box=(0,0,5,3) t=(3,3,3,3) r=(0,0,0,0)"] = (864, "1361CED8DCE20D53"),
        ["Square Progress box=(0,0,5,3) t=(0,5,2,0) r=(0,0,0,0)"] = (864, "B12FC43DDDFDC3CF"),
        ["Square Progress box=(0,0,5,3) t=(30,30,30,30) r=(0,0,0,0)"] = (864, "7B472C9DCD3E61F4"),
        ["Square Progress box=(0,0,80,40) t=(40,40,40,40) r=(0,0,0,0)"] = (11616, "136C9552CB4873EB"),
        ["Ring Progress box=(0,0,100,60) t=(8,8,8,8) r=(12,12,12,12)"] = (68964, "8DF7962A3958B9B3"),
        ["Ring Progress box=(10,20,100,60) t=(4,6,8,10) r=(12,12,12,12)"] = (68964, "C02F65C98030FD02"),
        ["Ring Progress box=(0,0,100,60) t=(3,3,3,3) r=(30,30,30,30)"] = (63804, "D3D28D2EB506C744"),
        ["Ring Progress box=(0,0,64,64) t=(0,5,2,0) r=(8,8,8,8)"] = (69658, "C40E664B9A6C64B1"),
        ["Ring Progress box=(0,0,20,20) t=(2,2,2,2) r=(8,8,8,8)"] = (68222, "F9067179D584847C"),
        ["Ring Progress box=(0,0,6,6) t=(1,1,1,1) r=(2,2,2,2)"] = (68224, "B48E5C450016FBEB"),
        ["Ring Progress box=(0,0,40,30) t=(1,2,3,4) r=(3,6,9,12)"] = (69008, "EBB2B32D678553BD"),
        ["Ring Progress box=(0,0,50,50) t=(5,5,5,5) r=(25,25,25,25)"] = (58332, "C584ED5927C1C6A8"),
        ["Residual Progress box=(0,0,80,40) t=(40,40,40,40) r=(12,12,12,12)"] = (11616, "136C9552CB4873EB"),
        ["Residual Progress box=(0,0,30,30) t=(15,15,15,15) r=(5,5,5,5)"] = (5856, "34C2D94AE91DE423"),
        ["Bounds Progress +underlay box=(10,20,100,60) t=(4,6,8,10) r=(0,0,0,0)"] = (46368, "F138A9955311669D"),
        ["Square Progress +underlay box=(10,20,100,60) t=(4,6,8,10) r=(0,0,0,0)"] = (77280, "8E15B0A33E76663F"),
        ["Ring Progress +underlay box=(10,20,100,60) t=(4,6,8,10) r=(12,12,12,12)"] = (191652, "2B32968F5A481C1A"),
        ["Bounds Scan box=(10,20,100,60) t=(4,6,8,10) r=(0,0,0,0)"] = (17616, "6966A9A5FBB7AD66"),
        ["Bounds Scan box=(10,20,100,60) t=(3,3,3,3) r=(0,0,0,0)"] = (17532, "55D4748261C85AC1"),
        ["Bounds Scan box=(10,20,100,60) t=(0,5,2,0) r=(0,0,0,0)"] = (10571, "39280E016C75703E"),
        ["Bounds Scan box=(10,20,100,60) t=(0,0,0,0) r=(0,0,0,0)"] = (3400, "486820EFA4794C6D"),
        ["Bounds Scan box=(10,20,100,60) t=(4,2,4,2) r=(0,0,0,0)"] = (17454, "1F8A9BCA2C7DCC4F"),
        ["Bounds Scan box=(10,20,100,60) t=(30,30,30,30) r=(0,0,0,0)"] = (17422, "D14EF6BA3A754F4D"),
        ["Bounds Scan box=(10,20,100,60) t=(-2,3,4,-1) r=(0,0,0,0)"] = (10533, "9057615D3C0CAE16"),
        ["Bounds Scan box=(0,0,64,64) t=(4,6,8,10) r=(0,0,0,0)"] = (14266, "43821B6B0298810B"),
        ["Bounds Scan box=(0,0,64,64) t=(3,3,3,3) r=(0,0,0,0)"] = (14212, "84071092F0A6E911"),
        ["Bounds Scan box=(0,0,64,64) t=(0,5,2,0) r=(0,0,0,0)"] = (8592, "6BDCB0CDC386BA3B"),
        ["Bounds Scan box=(0,0,64,64) t=(0,0,0,0) r=(0,0,0,0)"] = (2760, "218FFA9350B770BC"),
        ["Bounds Scan box=(0,0,64,64) t=(4,2,4,2) r=(0,0,0,0)"] = (14131, "9BF4130AA044DE21"),
        ["Bounds Scan box=(0,0,64,64) t=(30,30,30,30) r=(0,0,0,0)"] = (14096, "34EBCDFE5ED96EA8"),
        ["Bounds Scan box=(0,0,64,64) t=(-2,3,4,-1) r=(0,0,0,0)"] = (8552, "646692DD995A9259"),
        ["Bounds Scan box=(3,4,17,9) t=(4,6,8,10) r=(0,0,0,0)"] = (3641, "D67ECE637396063F"),
        ["Bounds Scan box=(3,4,17,9) t=(3,3,3,3) r=(0,0,0,0)"] = (3556, "58632B6069CF9C9D"),
        ["Bounds Scan box=(3,4,17,9) t=(0,5,2,0) r=(0,0,0,0)"] = (2281, "C2DDF1D17C98394E"),
        ["Bounds Scan box=(3,4,17,9) t=(0,0,0,0) r=(0,0,0,0)"] = (720, "23BEAF56DB3014D6"),
        ["Bounds Scan box=(3,4,17,9) t=(4,2,4,2) r=(0,0,0,0)"] = (3424, "1653123B071EC1AE"),
        ["Bounds Scan box=(3,4,17,9) t=(30,30,30,30) r=(0,0,0,0)"] = (2848, "1AD03473D0D89AF6"),
        ["Bounds Scan box=(3,4,17,9) t=(-2,3,4,-1) r=(0,0,0,0)"] = (2208, "52074EAA4DF7D4C1"),
        ["Bounds Scan box=(0,0,5,3) t=(4,6,8,10) r=(0,0,0,0)"] = (1230, "CFC341E4538B5E07"),
        ["Bounds Scan box=(0,0,5,3) t=(3,3,3,3) r=(0,0,0,0)"] = (1592, "8496DED2A453C1E5"),
        ["Bounds Scan box=(0,0,5,3) t=(0,5,2,0) r=(0,0,0,0)"] = (1097, "9977609951CEB5EF"),
        ["Bounds Scan box=(0,0,5,3) t=(0,0,0,0) r=(0,0,0,0)"] = (360, "0F16E2C337AA60B2"),
        ["Bounds Scan box=(0,0,5,3) t=(4,2,4,2) r=(0,0,0,0)"] = (1490, "BE4109727D87A304"),
        ["Bounds Scan box=(0,0,5,3) t=(30,30,30,30) r=(0,0,0,0)"] = (1290, "BCE9063F0F1BF00E"),
        ["Bounds Scan box=(0,0,5,3) t=(-2,3,4,-1) r=(0,0,0,0)"] = (1076, "B3CAEDEBB6DF49BD"),
        ["Bounds Scan box=(7,7,2,2) t=(4,6,8,10) r=(0,0,0,0)"] = (903, "E5F997F8D8E4B1B1"),
        ["Bounds Scan box=(7,7,2,2) t=(3,3,3,3) r=(0,0,0,0)"] = (903, "E5F997F8D8E4B1B1"),
        ["Bounds Scan box=(7,7,2,2) t=(0,5,2,0) r=(0,0,0,0)"] = (578, "788E7C15E2F51719"),
        ["Bounds Scan box=(7,7,2,2) t=(0,0,0,0) r=(0,0,0,0)"] = (280, "584B17B27D4CDCA3"),
        ["Bounds Scan box=(7,7,2,2) t=(4,2,4,2) r=(0,0,0,0)"] = (903, "E5F997F8D8E4B1B1"),
        ["Bounds Scan box=(7,7,2,2) t=(30,30,30,30) r=(0,0,0,0)"] = (903, "E5F997F8D8E4B1B1"),
        ["Bounds Scan box=(7,7,2,2) t=(-2,3,4,-1) r=(0,0,0,0)"] = (578, "788E7C15E2F51719"),
        ["Bounds Scan box=(0,0,4,20) t=(4,6,8,10) r=(0,0,0,0)"] = (2684, "2F55C57860AE4174"),
        ["Bounds Scan box=(0,0,4,20) t=(3,3,3,3) r=(0,0,0,0)"] = (3298, "4EB304047F0F4622"),
        ["Bounds Scan box=(0,0,4,20) t=(0,5,2,0) r=(0,0,0,0)"] = (2106, "D4FE51C4A4FD6B0E"),
        ["Bounds Scan box=(0,0,4,20) t=(0,0,0,0) r=(0,0,0,0)"] = (680, "275B94A0DBAEF299"),
        ["Bounds Scan box=(0,0,4,20) t=(4,2,4,2) r=(0,0,0,0)"] = (2684, "2F55C57860AE4174"),
        ["Bounds Scan box=(0,0,4,20) t=(30,30,30,30) r=(0,0,0,0)"] = (2702, "9206DC3F79828122"),
        ["Bounds Scan box=(0,0,4,20) t=(-2,3,4,-1) r=(0,0,0,0)"] = (1682, "3C714C3301A3FC86"),
        ["Bounds Scan box=(5,5,0,1) t=(4,6,8,10) r=(0,0,0,0)"] = (420, "582C08F6D9831EF8"),
        ["Bounds Scan box=(5,5,0,1) t=(0,0,0,0) r=(0,0,0,0)"] = (280, "584B17B27D4CDCA3"),
        ["Bounds Scan box=(0,0,0,0) t=(0,0,0,0) r=(0,0,0,0)"] = (280, "584B17B27D4CDCA3"),
        ["Bounds Scan box=(5,5,0,8) t=(0,0,0,0) r=(0,0,0,0)"] = (400, "A9B3070124360A44"),
        ["Bounds Scan box=(0,0,1,1) t=(1,1,1,1) r=(0,0,0,0)"] = (560, "6C3D62778FEC603B"),
        ["Square Scan box=(10,20,100,60) t=(4,6,8,10) r=(0,0,0,0)"] = (17616, "6966A9A5FBB7AD66"),
        ["Square Scan box=(10,20,100,60) t=(3,3,3,3) r=(0,0,0,0)"] = (17532, "55D4748261C85AC1"),
        ["Square Scan box=(10,20,100,60) t=(0,5,2,0) r=(0,0,0,0)"] = (10571, "39280E016C75703E"),
        ["Square Scan box=(10,20,100,60) t=(30,30,30,30) r=(0,0,0,0)"] = (17422, "D14EF6BA3A754F4D"),
        ["Square Scan box=(0,0,64,64) t=(4,6,8,10) r=(0,0,0,0)"] = (14266, "43821B6B0298810B"),
        ["Square Scan box=(0,0,64,64) t=(3,3,3,3) r=(0,0,0,0)"] = (14212, "84071092F0A6E911"),
        ["Square Scan box=(0,0,64,64) t=(0,5,2,0) r=(0,0,0,0)"] = (8592, "6BDCB0CDC386BA3B"),
        ["Square Scan box=(0,0,64,64) t=(30,30,30,30) r=(0,0,0,0)"] = (14096, "34EBCDFE5ED96EA8"),
        ["Square Scan box=(3,4,17,9) t=(4,6,8,10) r=(0,0,0,0)"] = (3448, "C2CB62AD502D8770"),
        ["Square Scan box=(3,4,17,9) t=(3,3,3,3) r=(0,0,0,0)"] = (3556, "58632B6069CF9C9D"),
        ["Square Scan box=(3,4,17,9) t=(0,5,2,0) r=(0,0,0,0)"] = (2281, "C2DDF1D17C98394E"),
        ["Square Scan box=(3,4,17,9) t=(30,30,30,30) r=(0,0,0,0)"] = (1796, "AD8DF8F328D7A9E9"),
        ["Square Scan box=(0,0,5,3) t=(4,6,8,10) r=(0,0,0,0)"] = (1356, "EF4E5E07CB7D14B3"),
        ["Square Scan box=(0,0,5,3) t=(3,3,3,3) r=(0,0,0,0)"] = (1342, "75F2C0DEDA69A7C5"),
        ["Square Scan box=(0,0,5,3) t=(0,5,2,0) r=(0,0,0,0)"] = (1079, "87D7CEEE27DF0869"),
        ["Square Scan box=(0,0,5,3) t=(30,30,30,30) r=(0,0,0,0)"] = (826, "06A3AD13713B315A"),
        ["Square Scan box=(0,0,80,40) t=(40,40,40,40) r=(0,0,0,0)"] = (11080, "C50D275C47DF3033"),
        ["Ring Scan box=(0,0,100,60) t=(8,8,8,8) r=(12,12,12,12)"] = (165902, "232F7FD75720E29B"),
        ["Ring Scan box=(10,20,100,60) t=(4,6,8,10) r=(12,12,12,12)"] = (164308, "A0219BE24EDA3FA5"),
        ["Ring Scan box=(0,0,100,60) t=(3,3,3,3) r=(30,30,30,30)"] = (152362, "12CAA2E98A16F113"),
        ["Ring Scan box=(0,0,64,64) t=(0,5,2,0) r=(8,8,8,8)"] = (128978, "C8EA756A571DF70E"),
        ["Ring Scan box=(0,0,20,20) t=(2,2,2,2) r=(8,8,8,8)"] = (49392, "81E08D30E48C1FFC"),
        ["Ring Scan box=(0,0,6,6) t=(1,1,1,1) r=(2,2,2,2)"] = (23816, "B122C6E079BFB899"),
        ["Ring Scan box=(0,0,40,30) t=(1,2,3,4) r=(3,6,9,12)"] = (78086, "3D44CE999FAC220B"),
        ["Ring Scan box=(0,0,50,50) t=(5,5,5,5) r=(25,25,25,25)"] = (99288, "A3D3B81D90DAC598"),
        ["Residual Scan box=(0,0,80,40) t=(40,40,40,40) r=(12,12,12,12)"] = (11080, "C50D275C47DF3033"),
        ["Residual Scan box=(0,0,30,30) t=(15,15,15,15) r=(5,5,5,5)"] = (6976, "F8CB85CFBA85E789"),
        ["Bounds Scan +underlay box=(10,20,100,60) t=(4,6,8,10) r=(0,0,0,0)"] = (31216, "30FBDB82E84E871A"),
        ["Square Scan +underlay box=(10,20,100,60) t=(4,6,8,10) r=(0,0,0,0)"] = (44816, "4C09E5C49CC38C23"),
        ["Ring Scan +underlay box=(10,20,100,60) t=(4,6,8,10) r=(12,12,12,12)"] = (409108, "E4E27A8B0F4878A2"),
    };
}

/// <summary>The code of <see cref="MGHighlightBorderBrush"/> for <c>Progress</c> and <c>Scan</c> as it was before T1.4 (it allocated), copied literally from <c>HEAD</c>, so that the tests prove the buffers
/// give the same calls as the lists, dictionaries and arrays they replace. It reads the settings of the brush from a <see cref="Config"/>.</summary>
internal static class OldHighlightBrushReference
{
    /// <summary>Only for <c>nameof(Draw)</c> in the message of an exception of the old code.</summary>
    private const string Draw = "Draw";

    private static int PositiveModulo(int x, int m)
    {
        var r = x % m;
        return r < 0 ? r + m : r;
    }

    private static double PositiveModulo(double x, double m)
    {
        var r = x % m;
        return r < 0 ? r + m : r;
    }

    /// <summary><see cref="MGHighlightBorderBrush.ActualAnimationProgress"/>, as it was.</summary>
    public static double ActualAnimationProgress(double animationProgress)
        => animationProgress == (int)animationProgress && animationProgress != 0.0 ? 1.0 : animationProgress - Math.Truncate(animationProgress);

    //  --- Begin literal copy of the code of MGHighlightBorderBrush before T1.4 (rectangle overload: Progress and Scan cases; DrawProgressOnRing; FindTopLeftAnchorIndex; DrawScanOnRing).
    //  Only the declarations of the brush settings as locals, and the replacement of the kept polygon buffer by a local one, differ from the old text.
    public static void DrawProgressRectangle(ElementDrawArgs DA, Config c, double Progress, Rectangle Bounds, Thickness BT)
    {
        var HighlightColor = c.Color;
        var ProgressFlowDirection = c.Flow;
        var ProgressSize = c.ProgressSize;
        List<Vector2> PolygonVerticesBuffer = null;
        var Edges = new List<Rectangle>();
        if (BT.Left > 0)
        {
            Edges.Add(new(Bounds.Left, Bounds.Top, BT.Left, Bounds.Height));
        }

        if (BT.Right > 0)
        {
            Edges.Add(new(Bounds.Right - BT.Right, Bounds.Top, BT.Right, Bounds.Height));
        }

        if (BT.Top > 0)
        {
            Edges.Add(new(Bounds.Left + BT.Left, Bounds.Top, Bounds.Width - BT.Width, BT.Top));
        }

        if (BT.Bottom > 0)
        {
            Edges.Add(new(Bounds.Left + BT.Left, Bounds.Bottom - BT.Bottom, Bounds.Width - BT.Width, BT.Bottom));
        }

        var OuterPerimeterLength = Bounds.Width * 2 + (Bounds.Height - 1) * 2;
        var InnerPerimeterLength = OuterPerimeterLength - BT.Width * 2 - BT.Height * 2;
        var AvgPerimeterLength = (OuterPerimeterLength + InnerPerimeterLength) / 2;

        //  Returns a point along the outer perimeter of the Rectangle bounds, where the point represents travelling along the perimeter by the given percent
        //  0.0 = topleft corner, moves clockwise (or counterclockwise if IsReversed=true) along the perimeter
        Point GetPerimeterPosition(double Percent, bool IsReversed, out int EdgeIndex)
        {
            var LinearPosition = PositiveModulo((int)(Percent * OuterPerimeterLength), OuterPerimeterLength);

            if (!IsReversed)
            {
                // Top edge
                if (LinearPosition <= Bounds.Width)
                {
                    EdgeIndex = 0;
                    return Bounds.TopLeft() + new Point(LinearPosition, 0);
                }
                // Right edge
                else if (LinearPosition <= Bounds.Width + Bounds.Height - 2)
                {
                    EdgeIndex = 1;
                    return Bounds.TopRight() + new Point(0, LinearPosition - Bounds.Width);
                }
                // Bottom edge
                else if (LinearPosition <= Bounds.Width + Bounds.Height - 2 + Bounds.Width)
                {
                    EdgeIndex = 2;
                    return Bounds.BottomRight() - new Point(LinearPosition - Bounds.Width - Bounds.Height + 1, 0);
                }
                // Left edge
                else
                {
                    EdgeIndex = 3;
                    return Bounds.BottomLeft() - new Point(0, LinearPosition - Bounds.Width - Bounds.Height + 2 - Bounds.Width);
                }
            }
            else
            {
                // Top edge
                if (LinearPosition == 0)
                {
                    EdgeIndex = 0;
                    return Bounds.TopLeft();
                }
                //  Left edge
                else if (LinearPosition <= Bounds.Height - 1)
                {
                    EdgeIndex = 3;
                    return Bounds.TopLeft() + new Point(0, LinearPosition);
                }
                //  Bottom edge
                else if (LinearPosition <= Bounds.Height - 1 + Bounds.Width)
                {
                    EdgeIndex = 2;
                    return Bounds.BottomLeft() + new Point(LinearPosition - Bounds.Height - 1, 0);
                }
                //  Right edge
                else if (LinearPosition <= Bounds.Height - 1 + Bounds.Width + Bounds.Height - 2)
                {
                    EdgeIndex = 1;
                    return Bounds.BottomRight() - new Point(0, LinearPosition - Bounds.Height + 1 - Bounds.Width);
                }
                //  Top edge
                else
                {
                    EdgeIndex = 0;
                    return Bounds.TopRight() - new Point(LinearPosition - Bounds.Height + 1 - Bounds.Width - Bounds.Height + 1, 0);
                }
            }
        }

        var StartPosition = GetPerimeterPosition(Progress - ProgressSize / 2.0, ProgressFlowDirection == HighlightFlowDirection.CounterClockwise, out var StartEdge);
        var EndPosition = GetPerimeterPosition(Progress + ProgressSize / 2.0, ProgressFlowDirection == HighlightFlowDirection.CounterClockwise, out var EndEdge);

        //  Create a list of vertices representing the outer bounds of the polygon we want to fill in
        List<Point> OuterVertices = new() { StartPosition };
        if (StartEdge != EndEdge) // If the start and end are on different edges of the bounds, append all the corners that they pass through
        {
            IReadOnlyDictionary<int, Point> OuterCornerLookup = new Dictionary<int, Point>()
            {
                { 0, Bounds.TopLeft() },
                { 1, Bounds.TopRight() },
                { 2, Bounds.BottomRight() },
                { 3, Bounds.BottomLeft() }
            };

            const int NumEdges = 4;
            var CurrentEdge = StartEdge;
            if (ProgressFlowDirection == HighlightFlowDirection.Clockwise)
            {
                while (CurrentEdge != EndEdge)
                {
                    CurrentEdge = (CurrentEdge + 1) % NumEdges;
                    OuterVertices.Add(OuterCornerLookup[CurrentEdge]);
                }
            }
            else if (ProgressFlowDirection == HighlightFlowDirection.CounterClockwise)
            {
                while (CurrentEdge != EndEdge)
                {
                    OuterVertices.Add(OuterCornerLookup[CurrentEdge]);
                    CurrentEdge = PositiveModulo(CurrentEdge - 1, NumEdges);
                }
            }
            else
            {
                throw new NotImplementedException($"Unrecognized {nameof(HighlightFlowDirection)}: {ProgressFlowDirection}");
            }
        }
        OuterVertices.Add(EndPosition);
        OuterVertices = OuterVertices.Distinct().ToList(); // There will be duplicate vertices if a start or end point was on a corner

        //  Key = Bounds of a corner, Value = the inner corner point
        var CornerBounds = new Dictionary<Rectangle, Point>()
        {
            { new Rectangle(Bounds.Left, Bounds.Top, BT.Left, BT.Top), Bounds.TopLeft() + new Point(BT.Left, BT.Top) },
            { new Rectangle(Bounds.Right - BT.Right, Bounds.Top, BT.Right, BT.Top), Bounds.TopRight() + new Point(-BT.Right, BT.Top) },
            { new Rectangle(Bounds.Right - BT.Right, Bounds.Bottom - BT.Bottom, BT.Right, BT.Bottom), Bounds.BottomRight() + new Point(-BT.Right, -BT.Bottom) },
            { new Rectangle(Bounds.Left, Bounds.Bottom - BT.Bottom, BT.Left, BT.Bottom), Bounds.BottomLeft() + new Point(BT.Left, -BT.Bottom) }
        };

        List<Point> Polygon = new(OuterVertices);
        foreach (var OuterVertex in OuterVertices.AsEnumerable().Reverse())
        {
            //  Compute the inner vertex that opposes this outer vertex
            var InnerVertex = Point.Zero;

            //  First check if the vertex is within a corner region and if so use the inner corner vertex
            var IsCorner = false;
            foreach (var KVP in CornerBounds)
            {
                if (KVP.Key.ContainsInclusive(OuterVertex))
                {
                    InnerVertex = KVP.Value;
                    IsCorner = true;
                    break;
                }
            }

            //  Otherwise just move the vertex inwards by the corresponding edge's BorderThickness
            if (!IsCorner)
            {
                if (OuterVertex.Y == Bounds.Top) // Top Edge
                {
                    InnerVertex = OuterVertex + new Point(0, BT.Top);
                }
                else if (OuterVertex.Y == Bounds.Bottom) // Bottom Edge
                {
                    InnerVertex = OuterVertex + new Point(0, -BT.Bottom);
                }
                else if (OuterVertex.X == Bounds.Right) // Right Edge
                {
                    InnerVertex = OuterVertex + new Point(-BT.Right, 0);
                }
                else if (OuterVertex.X == Bounds.Left) // Left Edge
                {
                    InnerVertex = OuterVertex + new Point(BT.Left, 0);
                }
                else
                {
                    throw new InvalidOperationException($"Flawed logic in {nameof(MGHighlightBorderBrush)}.{nameof(Draw)}: Vertex was not along the outer edge of the bounds.");
                }
            }

            Polygon.Add(InnerVertex);
        }
        Polygon = Polygon.Distinct().ToList();

        //  The vertices go into a list kept per thread, which FillPolygon reads during the call: a Select iterator at every draw would be garbage at every frame.
        List<Vector2> PolygonVertices = PolygonVerticesBuffer ??= new List<Vector2>();
        PolygonVertices.Clear();
        for (int i = 0; i < Polygon.Count; i++)
        {
            PolygonVertices.Add(Polygon[i].ToVector2());
        }

        DA.DT.FillPolygon(DA.Offset.ToVector2(), PolygonVertices, HighlightColor);
    }

    public static void DrawScanRectangle(ElementDrawArgs DA, MGElement Element, Config c, double Progress, Rectangle Bounds, Thickness BT)
    {
        var HighlightFillBrush = c.Color.AsFillBrush();
        var ScanOrientation = c.Orientation;
        var ScanIsReversed = c.Reversed;
        var ScanSize = c.ScanSize;
        var Edges = new List<Rectangle>();
        if (BT.Left > 0)
        {
            Edges.Add(new(Bounds.Left, Bounds.Top, BT.Left, Bounds.Height));
        }

        if (BT.Right > 0)
        {
            Edges.Add(new(Bounds.Right - BT.Right, Bounds.Top, BT.Right, Bounds.Height));
        }

        if (BT.Top > 0)
        {
            Edges.Add(new(Bounds.Left + BT.Left, Bounds.Top, Bounds.Width - BT.Width, BT.Top));
        }

        if (BT.Bottom > 0)
        {
            Edges.Add(new(Bounds.Left + BT.Left, Bounds.Bottom - BT.Bottom, Bounds.Width - BT.Width, BT.Bottom));
        }

        if (Edges.Any())
        {
            var Scanlines = new List<Rectangle>();

            switch (ScanOrientation)
            {
                case Orientation.Horizontal:
                {
                    var Left = Bounds.Left;
                    var Right = Bounds.Right;

                    var Width = Bounds.Width;
                    var Height = Math.Max(1, (int)Math.Round(Math.Min(ScanSize, 1.0) * Bounds.Height));

                    var Center = !ScanIsReversed ? Bounds.Top + (int)(Progress * Bounds.Height) : Bounds.Bottom - ((int)(Progress * Bounds.Height));
                    var Top = Center - Height / 2;
                    var Bottom = Center + Height / 2;

                    //  Handle cases where the scanline needs to loop around to opposite side
                    if (Top < Bounds.Top)
                    {
                        var Overflow = Bounds.Top - Top;
                        Scanlines.Add(new Rectangle(Left, Bounds.Top, Width, Height - Overflow));
                        Scanlines.Add(new Rectangle(Left, Bounds.Bottom - Overflow, Width, Overflow));
                    }
                    else if (Bottom > Bounds.Bottom)
                    {
                        var Overflow = Bottom - Bounds.Bottom;
                        Scanlines.Add(new Rectangle(Left, Bounds.Bottom - Height + Overflow, Width, Height - Overflow));
                        Scanlines.Add(new Rectangle(Left, Bounds.Top, Width, Overflow));
                    }
                    else
                    {
                        Scanlines.Add(new Rectangle(Left, Top, Width, Height));
                    }
                }
                    break;
                case Orientation.Vertical:
                {
                    var Top = Bounds.Top;
                    var Bottom = Bounds.Bottom;

                    var Width = Math.Max(1, (int)Math.Round(Math.Min(ScanSize, 1.0) * Bounds.Width));
                    var Height = Bounds.Height;

                    var Center = !ScanIsReversed ? Bounds.Left + (int)(Progress * Bounds.Width) : Bounds.Right - ((int)(Progress * Bounds.Width));
                    var Left = Center - Width / 2;
                    var Right = Center + Width / 2;

                    //  Handle cases where the scanline needs to loop around to opposite side
                    if (Left < Bounds.Left)
                    {
                        var Overflow = Bounds.Left - Left;
                        Scanlines.Add(new Rectangle(Bounds.Left, Top, Width - Overflow, Height));
                        Scanlines.Add(new Rectangle(Bounds.Right - Overflow, Top, Overflow, Height));
                    }
                    else if (Right > Bounds.Right)
                    {
                        var Overflow = Right - Bounds.Right;
                        Scanlines.Add(new Rectangle(Bounds.Right - Width + Overflow, Top, Width - Overflow, Height));
                        Scanlines.Add(new Rectangle(Bounds.Left, Top, Overflow, Height));
                    }
                    else
                    {
                        Scanlines.Add(new Rectangle(Left, Top, Width, Height));
                    }
                }
                    break;
                default: throw new NotImplementedException($"Unrecognized {nameof(Orientation)}: {ScanOrientation}");
            }

            foreach (var Scanline in Scanlines)
            {
                foreach (var Edge in Edges)
                {
                    var Intersection = Rectangle.Intersect(Scanline, Edge);
                    if (!Intersection.IsEmpty)
                    {
                        HighlightFillBrush.Draw(DA, Element, Intersection);
                    }
                }
            }
        }
    }

    public static void DrawProgressOnRing(ElementDrawArgs DA, Config c, MGBoxGeometry Geometry, double progress)
    {
        var HighlightColor = c.Color;
        var ProgressFlowDirection = c.Flow;
        var ProgressSize = c.ProgressSize;
        var outer = Geometry.OuterContour;
        var inner = Geometry.InnerContour;
        var n = outer.Count;
        if (n < 2 || inner.Count != n)
        {
            return;
        }

        var anchorIndex = FindTopLeftAnchorIndex(outer);
        var isReversed = ProgressFlowDirection == HighlightFlowDirection.CounterClockwise;

        //  order[k] = contour index of the k-th vertex visited in sweep order, starting at the anchor.
        //  arcPos[k] = cumulative arc length (along the outer contour) from the anchor to order[k]; arcPos[n] closes the loop.
        var order = new int[n];
        var arcPos = new double[n + 1];
        for (var k = 0; k < n; k++)
        {
            order[k] = isReversed ? PositiveModulo(anchorIndex - k, n) : PositiveModulo(anchorIndex + k, n);
        }
        for (var k = 1; k <= n; k++)
        {
            arcPos[k] = arcPos[k - 1] + Vector2.Distance(outer[order[k - 1]], outer[order[k % n]]);
        }
        var total = arcPos[n];
        if (total <= 0.0)
        {
            return;
        }

        var startPercent = progress - ProgressSize / 2.0;
        var startArc = PositiveModulo(startPercent * total, total);
        var endArc = startArc + ProgressSize * total;

        if (endArc <= total)
        {
            EmitProgressSpan(startArc, endArc);
        }
        else
        {
            EmitProgressSpan(startArc, total);
            EmitProgressSpan(0.0, endArc - total);
        }

        void EmitProgressSpan(double rangeStart, double rangeEnd)
        {
            if (rangeEnd - rangeStart <= 1e-9)
            {
                return;
            }

            var origin = DA.Offset.ToVector2();
            for (var k = 0; k < n; k++)
            {
                var quadStart = arcPos[k];
                var quadEnd = arcPos[k + 1];
                var overlapStart = Math.Max(quadStart, rangeStart);
                var overlapEnd = Math.Min(quadEnd, rangeEnd);
                if (overlapEnd - overlapStart <= 1e-6)
                {
                    continue;
                }

                var quadLength = quadEnd - quadStart;
                var tStart = quadLength > 1e-9 ? (overlapStart - quadStart) / quadLength : 0.0;
                var tEnd = quadLength > 1e-9 ? (overlapEnd - quadStart) / quadLength : 1.0;

                var i0 = order[k];
                var i1 = order[(k + 1) % n];
                var outerA = outer[i0];
                var outerB = outer[i1];
                var innerA = inner[i0];
                var innerB = inner[i1];

                //  Ring quad = outer at tStart, outer at tEnd, inner at tEnd, inner at tStart (BuildBorderRingIndices emits (o, o2, i2) then (i2, i, o)).
                var oStart = Vector2.Lerp(outerA, outerB, (float)tStart);
                var oEnd = Vector2.Lerp(outerA, outerB, (float)tEnd);
                var iStart = Vector2.Lerp(innerA, innerB, (float)tStart);
                var iEnd = Vector2.Lerp(innerA, innerB, (float)tEnd);

                DA.DT.FillTriangle(origin, oStart, HighlightColor, oEnd, HighlightColor, iEnd, HighlightColor);
                DA.DT.FillTriangle(origin, iEnd, HighlightColor, iStart, HighlightColor, oStart, HighlightColor);
            }
        }
    }

    public static int FindTopLeftAnchorIndex(IReadOnlyList<Vector2> outer)
    {
        var anchor = 0;
        for (var i = 1; i < outer.Count; i++)
        {
            var candidate = outer[i];
            var current = outer[anchor];
            if (candidate.Y < current.Y || (candidate.Y == current.Y && candidate.X < current.X))
            {
                anchor = i;
            }
        }
        return anchor;
    }

    public static void DrawScanOnRing(ElementDrawArgs DA, Config c, MGBoxGeometry Geometry, double progress)
    {
        var HighlightColor = c.Color;
        var ScanOrientation = c.Orientation;
        var ScanIsReversed = c.Reversed;
        var ScanSize = c.ScanSize;
        var outer = Geometry.OuterContour;
        var inner = Geometry.InnerContour;
        var n = outer.Count;
        if (n < 2 || inner.Count != n)
        {
            return;
        }

        var bounds = Geometry.Shape.OuterBounds;
        List<Rectangle> scanlines = new();

        switch (ScanOrientation)
        {
            case Orientation.Horizontal:
            {
                var width = bounds.Width;
                var height = Math.Max(1, (int)Math.Round(Math.Min(ScanSize, 1.0) * bounds.Height));
                var center = !ScanIsReversed ? bounds.Top + (int)(progress * bounds.Height) : bounds.Bottom - (int)(progress * bounds.Height);
                var top = center - height / 2;
                var bottom = top + height;

                if (top < bounds.Top)
                {
                    var overflow = bounds.Top - top;
                    scanlines.Add(new Rectangle(bounds.Left, bounds.Top, width, height - overflow));
                    scanlines.Add(new Rectangle(bounds.Left, bounds.Bottom - overflow, width, overflow));
                }
                else if (bottom > bounds.Bottom)
                {
                    var overflow = bottom - bounds.Bottom;
                    scanlines.Add(new Rectangle(bounds.Left, bounds.Bottom - height + overflow, width, height - overflow));
                    scanlines.Add(new Rectangle(bounds.Left, bounds.Top, width, overflow));
                }
                else
                {
                    scanlines.Add(new Rectangle(bounds.Left, top, width, height));
                }
            }
                break;
            case Orientation.Vertical:
            {
                var height = bounds.Height;
                var width = Math.Max(1, (int)Math.Round(Math.Min(ScanSize, 1.0) * bounds.Width));
                var center = !ScanIsReversed ? bounds.Left + (int)(progress * bounds.Width) : bounds.Right - (int)(progress * bounds.Width);
                var left = center - width / 2;
                var right = left + width;

                if (left < bounds.Left)
                {
                    var overflow = bounds.Left - left;
                    scanlines.Add(new Rectangle(bounds.Left, bounds.Top, width - overflow, height));
                    scanlines.Add(new Rectangle(bounds.Right - overflow, bounds.Top, overflow, height));
                }
                else if (right > bounds.Right)
                {
                    var overflow = right - bounds.Right;
                    scanlines.Add(new Rectangle(bounds.Right - width + overflow, bounds.Top, width - overflow, height));
                    scanlines.Add(new Rectangle(bounds.Left, bounds.Top, overflow, height));
                }
                else
                {
                    scanlines.Add(new Rectangle(left, bounds.Top, width, height));
                }
            }
                break;
            default: throw new NotImplementedException($"Unrecognized {nameof(Orientation)}: {ScanOrientation}");
        }

        var origin = DA.Offset.ToVector2();
        List<Vector2> quadPolygon = new(4);
        List<Vector2> clipped = new(8);
        List<Vector2> scratch = new(8);

        foreach (var scanline in scanlines)
        {
            for (var i = 0; i < n; i++)
            {
                var next = (i + 1) % n;

                //  Ring quad i = outer i, outer i+1, inner i+1, inner i (BuildBorderRingIndices emits (o, o2, i2) then (i2, i, o)).
                quadPolygon.Clear();
                quadPolygon.Add(outer[i]);
                quadPolygon.Add(outer[next]);
                quadPolygon.Add(inner[next]);
                quadPolygon.Add(inner[i]);

                MGConvexPolygonClipper.ClipToRectangle(quadPolygon, scanline, clipped, scratch);
                if (clipped.Count < 3)
                {
                    continue;
                }

                for (var t = 1; t + 1 < clipped.Count; t++)
                {
                    DA.DT.FillTriangle(origin, clipped[0], HighlightColor, clipped[t], HighlightColor, clipped[t + 1], HighlightColor);
                }
            }
        }
    }
    //  --- End of the literal copy.
}
