using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Animation;

/// <summary>Slice Y3 (ADR-0011, decision C4): <see cref="MGSpriteSheetGrid"/>'s own geometry, <see cref="MGTextureFillBrush.FrameGrid"/>/
/// <see cref="MGTextureFillBrush.FrameIndex"/> across the drawing paths, and the <see cref="UIExtraAnimationTargets.Paths.BackgroundTextureFrame"/>
/// animation target.</summary>
public class TextureFrameAnimationTests
{
    private static int _imageCounter;

    private static GraphTestImageResource Image(int width, int height) => new($"frame-tex-{System.Threading.Interlocked.Increment(ref _imageCounter)}", width, height);

    private static (GraphTestRuntime Runtime, GraphNoOpDrawTransaction Transaction) Recorder()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        return (runtime, new GraphNoOpDrawTransaction(runtime, DrawSettings.Default));
    }

    private static ElementDrawArgs Args(GraphNoOpDrawTransaction transaction)
        => new(new DrawBaseArgs(TimeSpan.Zero, transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);

    #region MGSpriteSheetGrid geometry

    [Fact]
    public void GetFrameRectangle_WithSpacingAndMarginInsideAnAtlasOffset_IsExact()
    {
        //  4 columns x 3 rows, 10x8 cells, spacing (1,2), margin (3,6), inside an atlas region offset by (100, 50).
        MGSpriteSheetGrid grid = new(4, 3, new Point(10, 8), new Point(1, 2), new Point(3, 6));
        Rectangle region = new(100, 50, 200, 200);

        //  Frame 0: (col 0, row 0).
        Assert.Equal(new Rectangle(100 + 3, 50 + 6, 10, 8), grid.GetFrameRectangle(0, region));
        //  Frame 5: index 5 -> column 1, row 1 (5 % 4 = 1, 5 / 4 = 1).
        Rectangle frame5 = grid.GetFrameRectangle(5, region);
        Assert.Equal(new Rectangle(103 + 1 * (10 + 1), 56 + 1 * (8 + 2), 10, 8), frame5);
        //  Frame 11 (last, index 11 -> column 3, row 2): 4*3-1 = 11.
        Rectangle frame11 = grid.GetFrameRectangle(11, region);
        Assert.Equal(new Rectangle(103 + 3 * (10 + 1), 56 + 2 * (8 + 2), 10, 8), frame11);
    }

    [Fact]
    public void GetFrameRectangle_OnAWholeImageRegion_WithNoSpacingOrMargin_TilesContiguously()
    {
        MGSpriteSheetGrid grid = new(3, 1, new Point(16, 16));
        Rectangle region = new(0, 0, 48, 16);

        Assert.Equal(new Rectangle(0, 0, 16, 16), grid.GetFrameRectangle(0, region));
        Assert.Equal(new Rectangle(16, 0, 16, 16), grid.GetFrameRectangle(1, region));
        Assert.Equal(new Rectangle(32, 0, 16, 16), grid.GetFrameRectangle(2, region));
    }

    [Fact]
    public void GetFrameRectangle_ClampsAnOutOfRangeIndex_ToTheLastFrame()
    {
        MGSpriteSheetGrid grid = new(2, 2, new Point(4, 4));
        Rectangle region = new(0, 0, 8, 8);

        Assert.Equal(grid.GetFrameRectangle(3, region), grid.GetFrameRectangle(99, region));
        Assert.Equal(grid.GetFrameRectangle(0, region), grid.GetFrameRectangle(-5, region));
    }

    [Fact]
    public void EffectiveFrameCount_IsColumnsTimesRows_UnlessFrameCountIsSet()
    {
        Assert.Equal(6, new MGSpriteSheetGrid(3, 2, new Point(4, 4)).EffectiveFrameCount);
        Assert.Equal(5, new MGSpriteSheetGrid(3, 2, new Point(4, 4), FrameCount: 5).EffectiveFrameCount);
    }

    [Theory]
    [InlineData(0, 1, 4, 4, 0, 0, 0, 0, null)]
    [InlineData(1, 0, 4, 4, 0, 0, 0, 0, null)]
    [InlineData(2, 2, 0, 4, 0, 0, 0, 0, null)]
    [InlineData(2, 2, 4, 0, 0, 0, 0, 0, null)]
    [InlineData(2, 2, 4, 4, -1, 0, 0, 0, null)]
    [InlineData(2, 2, 4, 4, 0, -1, 0, 0, null)]
    [InlineData(2, 2, 4, 4, 0, 0, -1, 0, null)]
    [InlineData(2, 2, 4, 4, 0, 0, 0, -1, null)]
    [InlineData(2, 2, 4, 4, 0, 0, 0, 0, 0)]
    [InlineData(2, 2, 4, 4, 0, 0, 0, 0, 5)]
    public void Validate_RefusesAnInvalidGrid(int columns, int rows, int cellW, int cellH, int spacingX, int spacingY, int marginX, int marginY, int? frameCount)
    {
        MGSpriteSheetGrid grid = new(columns, rows, new Point(cellW, cellH), new Point(spacingX, spacingY), new Point(marginX, marginY), frameCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.Validate());
    }

    [Fact]
    public void Validate_AcceptsAValidGrid()
    {
        MGSpriteSheetGrid grid = new(2, 2, new Point(16, 16), new Point(1, 1), new Point(0, 6), 4);
        grid.Validate(); // does not throw
    }

    #endregion

    #region MGTextureFillBrush + FrameGrid drawing

    [Fact]
    public void FrameGrid_Setter_ValidatesAndRejectsAnInvalidGrid()
    {
        var (_, transaction) = Recorder();
        MGTextureFillBrush brush = new(new MGTextureData(Image(64, 64)));

        Assert.Throws<ArgumentOutOfRangeException>(() => brush.FrameGrid = new MGSpriteSheetGrid(0, 1, new Point(4, 4)));
        Assert.Null(brush.FrameGrid);
        _ = transaction;
    }

    [Fact]
    public void FrameIndex_Setter_RejectsNegative()
    {
        MGTextureFillBrush brush = new(new MGTextureData(Image(64, 64)));
        Assert.Throws<ArgumentOutOfRangeException>(() => brush.FrameIndex = -1);
    }

    [Fact]
    public void NoGrid_RectangleDraw_IsUnchanged_RegressionGuard()
    {
        var (_, withGridField) = Recorder();
        var (_, plain) = Recorder();
        GraphTestImageResource image = Image(64, 64);
        MGTextureData source = new(image, new Rectangle(4, 4, 32, 32));

        new MGTextureFillBrush(source, Stretch.Fill) { Tile = false }.Draw(Args(plain), null, new Rectangle(0, 0, 80, 40));
        new MGTextureFillBrush(source, Stretch.Fill) { Tile = false }.Draw(Args(withGridField), null, new Rectangle(0, 0, 80, 40));

        Assert.Equal(plain.DrawTextureToCalls, withGridField.DrawTextureToCalls);
        GraphDrawTextureToCall call = Assert.Single(plain.DrawTextureToCalls);
        Assert.Equal(new Rectangle(4, 4, 32, 32), call.Source.Value);
    }

    [Fact]
    public void WithFrameGrid_RectangleDraw_UsesTheFrameCellAsSourceAndNaturalSize()
    {
        var (_, transaction) = Recorder();
        GraphTestImageResource image = Image(64, 64);
        MGSpriteSheetGrid grid = new(4, 1, new Point(16, 16), new Point(0, 0), new Point(0, 0));
        MGTextureFillBrush brush = new(new MGTextureData(image), Stretch.None) { FrameGrid = grid, FrameIndex = 2 };

        brush.Draw(Args(transaction), null, new Rectangle(0, 0, 100, 100));

        GraphDrawTextureToCall call = Assert.Single(transaction.DrawTextureToCalls);
        Assert.Equal(new Rectangle(32, 0, 16, 16), call.Source.Value);
        //  Stretch.None: destination size = cell size (16x16), centered in the 100x100 bounds.
        Assert.Equal(16, call.Destination.Width);
        Assert.Equal(16, call.Destination.Height);
    }

    [Theory]
    [InlineData(Stretch.Uniform)]
    [InlineData(Stretch.UniformToFill)]
    public void WithFrameGrid_Uniform_And_UniformToFill_UseTheCellAspectRatio(Stretch stretch)
    {
        var (_, transaction) = Recorder();
        //  A wide 32x16 cell (aspect ratio 2:1) inside a square 200x200 destination.
        MGSpriteSheetGrid grid = new(1, 1, new Point(32, 16));
        MGTextureFillBrush brush = new(new MGTextureData(Image(64, 64)), stretch) { FrameGrid = grid };

        brush.Draw(Args(transaction), null, new Rectangle(0, 0, 200, 200));

        GraphDrawTextureToCall call = Assert.Single(transaction.DrawTextureToCalls);
        double aspect = call.Destination.Width / (double)call.Destination.Height;
        Assert.Equal(2.0, aspect, 2);
    }

    [Fact]
    public void WithFrameGrid_Fill_IgnoresTheCellSize_AndFillsTheBounds()
    {
        var (_, transaction) = Recorder();
        MGSpriteSheetGrid grid = new(1, 1, new Point(16, 16));
        MGTextureFillBrush brush = new(new MGTextureData(Image(64, 64)), Stretch.Fill) { FrameGrid = grid };
        Rectangle bounds = new(0, 0, 90, 40);

        brush.Draw(Args(transaction), null, bounds);

        GraphDrawTextureToCall call = Assert.Single(transaction.DrawTextureToCalls);
        Assert.Equal(bounds, call.Destination);
    }

    [Fact]
    public void WithFrameGrid_RenderSizeOverride_WinsOverTheCellSize()
    {
        var (_, transaction) = Recorder();
        MGSpriteSheetGrid grid = new(1, 1, new Point(16, 16));
        MGTextureData source = new(Image(64, 64), null, 1f, new Size(40, 40));
        MGTextureFillBrush brush = new(source, Stretch.None) { FrameGrid = grid };

        brush.Draw(Args(transaction), null, new Rectangle(0, 0, 100, 100));

        GraphDrawTextureToCall call = Assert.Single(transaction.DrawTextureToCalls);
        Assert.Equal(40, call.Destination.Width);
        Assert.Equal(40, call.Destination.Height);
    }

    [Fact]
    public void WithFrameGrid_Tile_OnTheRectanglePath_UsesCellSizeAsTileStep_AndCropsThePartialEdgeTileFromTheCellsTopLeft()
    {
        var (_, transaction) = Recorder();
        //  One row of two 10x10 cells; bounds 24 wide -> two full tiles (0..10, 10..20) then a partial 4px tile (20..24).
        MGSpriteSheetGrid grid = new(2, 1, new Point(10, 10));
        MGTextureFillBrush brush = new(new MGTextureData(Image(64, 64)), Stretch.Fill) { FrameGrid = grid, FrameIndex = 1, Tile = true };

        brush.Draw(Args(transaction), null, new Rectangle(0, 0, 24, 10));

        Assert.Equal(3, transaction.DrawTextureToCalls.Count);
        Rectangle cellRect = grid.GetFrameRectangle(1, new Rectangle(0, 0, 64, 64));
        Assert.Equal(cellRect, transaction.DrawTextureToCalls[0].Source.Value);
        Assert.Equal(cellRect, transaction.DrawTextureToCalls[1].Source.Value);
        //  Partial tile: cropped from the cell's own top-left corner, width clamped to 4.
        GraphDrawTextureToCall partial = transaction.DrawTextureToCalls[2];
        Assert.Equal(cellRect.X, partial.Source.Value.X);
        Assert.Equal(cellRect.Y, partial.Source.Value.Y);
        Assert.Equal(4, partial.Source.Value.Width);
    }

    #endregion

    #region Background.Texture.Frame animation target

    /// <summary>A texture brush with a 1-row grid of <paramref name="frameCount"/> cells (columns = frameCount), so
    /// <see cref="MGSpriteSheetGrid.EffectiveFrameCount"/> is exactly <paramref name="frameCount"/> without needing an explicit
    /// <see cref="MGSpriteSheetGrid.FrameCount"/>.</summary>
    private static MGTextureFillBrush GridBrush(int frameCount = 5)
        => new(new MGTextureData(Image(64, 64)), Stretch.Fill) { FrameGrid = new MGSpriteSheetGrid(frameCount, 1, new Point(8, 8)) };

    private static void AssertAllocatesNothingPerTick(AnimationTestScene scene, int warmUpTicks = 20, int probeTicks = 200)
    {
        TimeSpan frame = TimeSpan.FromMilliseconds(AnimationTestScene.FrameMilliseconds);
        UIAnimationManager manager = scene.Desktop.Animations;
        for (int i = 0; i < warmUpTicks; i++)
        {
            manager.Update(frame);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < probeTicks; i++)
        {
            manager.Update(frame);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    private static UIPropertyAnimation<float> LinearFrames(int toFrameCount, int milliseconds, UIAnimationFillBehavior fill = UIAnimationFillBehavior.HoldEnd)
        => new(UIExtraAnimationTargets.Paths.BackgroundTextureFrame) { From = 0f, To = toFrameCount, Duration = TimeSpan.FromMilliseconds(milliseconds), FillBehavior = fill, Name = "frames" };

    [Fact]
    public void GetOwnerType_IsNull_AnyElementAccepts()
        => Assert.Null(UIAnimationTargets.GetOwnerType(UIExtraAnimationTargets.Paths.BackgroundTextureFrame));

    [Fact]
    public void GetValue_RefusesASolidBackground()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Red);
        var target = UIAnimationTargets.Resolve<float>(UIExtraAnimationTargets.Paths.BackgroundTextureFrame);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => target.GetValue(scene.Top));
        Assert.Contains(nameof(MGSolidFillBrush), error.Message);
    }

    [Fact]
    public void GetValue_RefusesATextureBrushWithoutAGrid()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGTextureFillBrush(new MGTextureData(Image(16, 16)));
        var target = UIAnimationTargets.Resolve<float>(UIExtraAnimationTargets.Paths.BackgroundTextureFrame);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => target.GetValue(scene.Top));
        Assert.Contains(nameof(MGTextureFillBrush.FrameGrid), error.Message);
    }

    [Fact]
    public void GetValue_ReadsTheCurrentFrameIndex_OfATextureBrushWithAGrid()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGTextureFillBrush brush = GridBrush();
        brush.FrameIndex = 3;
        scene.Top.BackgroundBrush.NormalValue = brush;
        var target = UIAnimationTargets.Resolve<float>(UIExtraAnimationTargets.Paths.BackgroundTextureFrame);

        Assert.Equal(3f, target.GetValue(scene.Top));
    }

    [Fact]
    public void StartingAnAnimation_OnASolidBackground_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Red);

        Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(LinearFrames(4, 1000)));
    }

    [Fact]
    public void StartingAnAnimation_OnATextureBrushWithoutAGrid_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGTextureFillBrush(new MGTextureData(Image(16, 16)));

        Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(LinearFrames(4, 1000)));
    }

    [Fact]
    public void NamedVisualStateSetter_OnAnInvalidBase_Throws()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Red);
        var target = (IUIStoreBackedAnimationTarget<float>)UIAnimationTargets.Resolve<float>(UIExtraAnimationTargets.Paths.BackgroundTextureFrame);

        Assert.Throws<InvalidOperationException>(() => target.SetValue(scene.Top, 2f, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw)));
    }

    [Fact]
    public void NamedVisualStateSetter_OnAGridBrush_CopiesItAndSetsTheMappedFrameIndex_WithoutMutatingTheOriginal()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGTextureFillBrush original = GridBrush();
        //  Below VisualState precedence (a plain assignment to NormalValue records a LocalValue contribution, which would outrank it,
        //  and the setter would then never win the resolution -- see UIValuePrecedence).
        scene.Top.SetBackgroundSlot(UIValueSlot.Normal, original, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        var target = (IUIStoreBackedAnimationTarget<float>)UIAnimationTargets.Resolve<float>(UIExtraAnimationTargets.Paths.BackgroundTextureFrame);

        target.SetValue(scene.Top, 2.9f, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));

        MGTextureFillBrush applied = Assert.IsType<MGTextureFillBrush>(scene.Top.BackgroundBrush.NormalValue);
        Assert.NotSame(original, applied);
        Assert.Equal(2, applied.FrameIndex);
        Assert.Equal(0, original.FrameIndex);
    }

    [Fact]
    public void Transition_OnThisPath_IsRefused_NotObservable()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = GridBrush();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(
            () => scene.Top.Transitions.Add(new UITransition<float>(UIExtraAnimationTargets.Paths.BackgroundTextureFrame, TimeSpan.FromMilliseconds(50))));
        Assert.Contains("not observable", error.Message);
    }

    [Theory]
    [InlineData(-0.5f, 0)]
    [InlineData(0f, 0)]
    [InlineData(0.999f, 0)]
    [InlineData(1f, 1)]
    [InlineData(4.999f, 4)] // EffectiveFrameCount = 5 -> max index 4
    [InlineData(5f, 4)] // clamped
    [InlineData(8f, 4)] // clamped (N + 3)
    public void FloorAndClampMapping_MatchesTheDocumentedTable(float value, int expectedFrameIndex)
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.SetBackgroundSlot(UIValueSlot.Normal, GridBrush(), UIValueResolutionSource.Theme(UIInvalidationKind.Draw)); // EffectiveFrameCount = 5, below VisualState precedence
        var target = (IUIStoreBackedAnimationTarget<float>)UIAnimationTargets.Resolve<float>(UIExtraAnimationTargets.Paths.BackgroundTextureFrame);

        target.SetValue(scene.Top, value, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));

        Assert.Equal(expectedFrameIndex, ((MGTextureFillBrush)scene.Top.BackgroundBrush.NormalValue).FrameIndex);
    }

    [Fact]
    public void FrozenSharedGridBrush_AnimatingOneElement_LeavesTheOtherIntact_AndRestoreReturnsTheOriginalInstance()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGTextureFillBrush shared = GridBrush();
        shared.Freeze();
        scene.Top.BackgroundBrush.NormalValue = shared;
        scene.Bottom.BackgroundBrush.NormalValue = shared;

        UIPropertyAnimation<float> animation = LinearFrames(4, 100000, UIAnimationFillBehavior.RestoreBaseValue);
        scene.Top.Animations.Start(animation);
        scene.Frames(5);

        Assert.NotSame(shared, scene.Top.BackgroundBrush.NormalValue);
        Assert.Same(shared, scene.Bottom.BackgroundBrush.NormalValue);
        Assert.Equal(0, shared.FrameIndex);
        Assert.True(shared.IsFrozen);

        animation.Cancel();

        Assert.Same(shared, scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(0, shared.FrameIndex);
    }

    [Fact]
    public void ExplicitAnimation_AllocatesNothingPerTick_AfterWarmUp()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = GridBrush();
        scene.Top.Animations.Start(LinearFrames(4, 100000));

        AssertAllocatesNothingPerTick(scene);
    }

    [Fact]
    public void LinearRun_HoldsEachFrameIndexForRoughlyEqualTicks_AndHoldsTheLastFrameAfterCompletion()
    {
        const int frameCount = 4;
        const int msPerFrame = 100;
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = GridBrush(frameCount: frameCount);
        scene.Top.Animations.Start(LinearFrames(frameCount, frameCount * msPerFrame));

        var samples = new System.Collections.Generic.List<int>();
        int totalTicks = frameCount * msPerFrame / AnimationTestScene.FrameMilliseconds + 2;
        for (int i = 0; i < totalTicks; i++)
        {
            scene.Frames(1);
            samples.Add(((MGTextureFillBrush)scene.Top.BackgroundBrush.NormalValue).FrameIndex);
        }

        // Group consecutive equal samples: one run per distinct frame index reached, in increasing order, ending at frameCount - 1.
        var runs = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (int sample in samples)
        {
            if (runs.Count > 0 && runs[^1].Index == sample)
            {
                runs[^1] = (sample, runs[^1].Length + 1);
            }
            else
            {
                runs.Add((sample, 1));
            }
        }

        Assert.Equal(frameCount - 1, runs[^1].Index);
        // Every observed index in [0, frameCount - 2] appears for a comparable number of ticks (expected ~= msPerFrame / FrameMilliseconds, +/-1),
        // ignoring the final held run which is intentionally open-ended (HoldEnd).
        double expectedTicksPerFrame = msPerFrame / (double)AnimationTestScene.FrameMilliseconds;
        foreach (var run in runs.Take(runs.Count - 1))
        {
            Assert.InRange(run.Length, Math.Max(1, (int)Math.Floor(expectedTicksPerFrame) - 1), (int)Math.Ceiling(expectedTicksPerFrame) + 1);
        }

        // Held at the end: further ticks keep reporting the last frame.
        scene.Frames(10);
        Assert.Equal(frameCount - 1, ((MGTextureFillBrush)scene.Top.BackgroundBrush.NormalValue).FrameIndex);
    }

    [Fact]
    public void RepeatForever_NeverShowsTheFrameCountIndex_OrANegativeIndex_AndLoopsBackToZero()
    {
        const int frameCount = 3;
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = GridBrush(frameCount: frameCount);

        scene.Top.Animate(UIExtraAnimationTargets.Paths.BackgroundTextureFrame, 0f, (float)frameCount, 0.15)
            .RepeatForever().Named("frames-loop").Play();

        bool sawZeroAfterLooping = false;
        int previous = -1;
        for (int i = 0; i < 40; i++)
        {
            scene.Frames(1);
            int index = ((MGTextureFillBrush)scene.Top.BackgroundBrush.NormalValue).FrameIndex;
            Assert.InRange(index, 0, frameCount - 1);
            if (previous == frameCount - 1 && index == 0)
            {
                sawZeroAfterLooping = true;
            }

            previous = index;
        }

        Assert.True(sawZeroAfterLooping, "the run must have looped back to frame 0 at least once within 40 ticks.");
    }

    [Fact]
    public void KeyFrameAnimation_WithExplicitOffsets_GivesEachSegmentItsOwnDuration()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = GridBrush(frameCount: 4);

        UIKeyFrameAnimation<float> animation = new(UIExtraAnimationTargets.Paths.BackgroundTextureFrame)
        {
            Duration = TimeSpan.FromMilliseconds(400),
            Track = { { 0f, 0f }, { 0.25f, 1f }, { 1f, 3f } },
            Name = "frames-keyed",
        };
        scene.Top.Animations.Start(animation);

        // Segment 0 -> 0.25 (100ms of the 400ms total) reaches value 1 quickly; well before the midpoint of the whole run.
        scene.Frames(6); // ~96ms
        Assert.True(((MGTextureFillBrush)scene.Top.BackgroundBrush.NormalValue).FrameIndex <= 1);

        scene.Frames(19); // ~400ms total
        Assert.Equal(3, ((MGTextureFillBrush)scene.Top.BackgroundBrush.NormalValue).FrameIndex);
    }

    [Fact]
    public void UIAnimationSerializer_RoundTripsAnExplicitAnimationOnThisPath()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = GridBrush(frameCount: 4);

        UIPropertyAnimation<float> original = LinearFrames(4, 300);
        string json = UIAnimationSerializer.Serialize(original, _ => null);
        UIAnimation deserialized = UIAnimationSerializer.Deserialize(json, _ => null);

        scene.Top.Animations.Start(deserialized);
        scene.Frames(19); // ~300ms
        Assert.Equal(3, ((MGTextureFillBrush)scene.Top.BackgroundBrush.NormalValue).FrameIndex);
    }

    [Fact]
    public void UIKeyFrameClipSerializer_RoundTripsAClipContainingThisPath()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = GridBrush(frameCount: 4);

        UIKeyFrameAnimation<float> track = new(UIExtraAnimationTargets.Paths.BackgroundTextureFrame)
        {
            Duration = TimeSpan.FromMilliseconds(300),
            Track = { { 0f, 0f }, { 1f, 3f } },
        };
        UIStoryboard storyboard = new() { track };
        string json = UIKeyFrameClipSerializer.Serialize(storyboard);
        UIStoryboard deserialized = UIKeyFrameClipSerializer.Deserialize(json);

        scene.Top.Animations.Start(deserialized);
        scene.Frames(19); // ~300ms
        Assert.Equal(3, ((MGTextureFillBrush)scene.Top.BackgroundBrush.NormalValue).FrameIndex);
    }

    #endregion
}
