using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>Tache 4 item 5 (<see cref="MGHighlightBorderBrush"/> <see cref="HighlightAnimation.Progress"/> / <see cref="HighlightAnimation.Scan"/>
/// on the rounded ring, see Docs/drawing-architecture.md and Docs/Tasks/drawing-tasks.md): the shape-aware overload now parameterizes both
/// animations along <see cref="MGBoxGeometry.OuterContour"/> / <see cref="MGBoxGeometry.InnerContour"/> instead of falling back to the
/// rectangular perimeter, except for the rectangle fast path and the residual case where the border has no ring mesh at all.</summary>
public class HighlightBorderBrushRingAnimationTests
{
    #region Progress

    /// <summary>Percentage 0 is anchored at the contour vertex that is top-most then left-most (the end of the top-left arc / start of the top
    /// edge). With <see cref="MGHighlightBorderBrush.ProgressSize"/> = 0.25 and <see cref="MGHighlightBorderBrush.AnimationProgress"/> = 0.125,
    /// the highlighted span is exactly [0, 0.25] of the contour's arc length: it starts at the anchor and, since a quarter of this shape's
    /// perimeter (~74.85px) is shorter than its straight top edge (76px), stays entirely on the top edge for the default
    /// (<see cref="HighlightFlowDirection.Clockwise"/>) flow direction - sweeping right, never reaching the top-right arc.</summary>
    [Fact]
    public void Progress_DefaultFlowDirection_StartsAtAnchorAndSweepsRightAlongTopEdge()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(100, 60, 8, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.True(geometry.HasBorderRingMesh);
        Assert.False(geometry.UsesRectangleFastPath);

        Vector2 anchor = FindAnchor(geometry.OuterContour);
        Assert.Equal(shape.OuterBounds.Top, anchor.Y);

        MGHighlightBorderBrush brush = new(null, Color.Red, HighlightAnimation.Progress)
        {
            ProgressSize = 0.25,
            AnimationProgress = 0.125
        };

        brush.Draw(recorder.Args(), null, shape, geometry);

        List<GraphFillTriangleCall> calls = recorder.Transaction.FillTriangleCalls;
        Assert.NotEmpty(calls);

        //  Only the top edge's ring quad overlaps [0, 0.25] of the (~299px) perimeter, so exactly one quad (2 triangles) is emitted.
        Assert.Equal(2, calls.Count);

        //  First vertex in sweep order is the anchor itself.
        Assert.Equal(anchor, calls[0].V0);

        //  Every emitted vertex stays on the straight top edge (outer Y = OuterBounds.Top, inner Y = InnerBounds.Top), between the anchor and
        //  the top-right arc (never reaching x = OuterBounds.Right - radius = 88).
        Rectangle outerBounds = shape.OuterBounds;
        Rectangle innerBounds = shape.InnerBounds;
        foreach (GraphFillTriangleCall call in calls)
        {
            foreach (Vector2 vertex in new[] { call.V0, call.V1, call.V2 })
            {
                Assert.True(vertex.Y == outerBounds.Top || vertex.Y == innerBounds.Top, $"vertex {vertex} left the top edge");
                Assert.InRange(vertex.X, anchor.X - 0.01f, 88f + 0.01f);
            }
        }
    }

    /// <summary><see cref="HighlightFlowDirection.CounterClockwise"/> sweeps the opposite way from the same anchor: instead of moving right
    /// along the top edge, it curves back into the top-left arc (x decreases below the anchor's x).</summary>
    [Fact]
    public void Progress_MirroredFlowDirection_SweepsOppositeWayFromAnchor()
    {
        Recorder forwardRecorder = Recorder.Create();
        Recorder reversedRecorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(100, 60, 8, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Vector2 anchor = FindAnchor(geometry.OuterContour);

        MGHighlightBorderBrush forward = new(null, Color.Red, HighlightAnimation.Progress) { ProgressSize = 0.05, AnimationProgress = 0.025 };
        MGHighlightBorderBrush reversed = new(null, Color.Red, HighlightAnimation.Progress)
        {
            ProgressSize = 0.05,
            AnimationProgress = 0.025,
            ProgressFlowDirection = HighlightFlowDirection.CounterClockwise
        };

        forward.Draw(forwardRecorder.Args(), null, shape, geometry);
        reversed.Draw(reversedRecorder.Args(), null, shape, geometry);

        GraphFillTriangleCall forwardFirst = Assert.Single(forwardRecorder.Transaction.FillTriangleCalls.Where(c => c.V0 == anchor));
        GraphFillTriangleCall reversedFirst = Assert.Single(reversedRecorder.Transaction.FillTriangleCalls.Where(c => c.V0 == anchor));

        //  Both start at the anchor, but the very next vertex along the outer contour moves in opposite directions.
        Assert.True(forwardFirst.V1.X > anchor.X, $"clockwise flow should move right from the anchor, got {forwardFirst.V1}");
        Assert.True(reversedFirst.V1.X < anchor.X, $"counter-clockwise flow should move left from the anchor, got {reversedFirst.V1}");
    }

    /// <summary>A span centered exactly on the anchor (progress 0%, size 20%) straddles the seam where the contour wraps back to index 0: it
    /// must be emitted as two separate sub-spans, one ending at the anchor and one starting at it, rather than being silently dropped or clamped.</summary>
    [Fact]
    public void Progress_SpanWrappingPastAnchor_EmitsBothParts()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(100, 60, 8, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Vector2 anchor = FindAnchor(geometry.OuterContour);

        MGHighlightBorderBrush brush = new(null, Color.Red, HighlightAnimation.Progress) { ProgressSize = 0.2, AnimationProgress = 0.0 };
        brush.Draw(recorder.Args(), null, shape, geometry);

        List<GraphFillTriangleCall> calls = recorder.Transaction.FillTriangleCalls;
        Assert.NotEmpty(calls);

        //  Part 2 (arc [0, 0.1*total]) starts exactly at the anchor and immediately moves right (same shape as the previous test).
        Assert.Contains(calls, c => c.V0 == anchor && c.V1.X > anchor.X);

        //  Part 1 (arc [0.9*total, total]) ends exactly at the anchor, arriving from within the top-left arc (x < anchor.x just before it).
        Assert.Contains(calls, c => c.V1 == anchor && c.V0.X < anchor.X);
    }

    /// <summary>The rectangle fast path (no rounded corners) keeps calling the legacy rectangle overload, unchanged.</summary>
    [Fact]
    public void Progress_RectangleFastPath_KeepsLegacyRectanglePath()
    {
        Recorder shapeRecorder = Recorder.Create();
        Recorder legacyRecorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(100, 60, 8, 0);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.True(geometry.UsesRectangleFastPath);

        MGHighlightBorderBrush shapeBrush = new(null, Color.Red, HighlightAnimation.Progress) { AnimationProgress = 0.25 };
        MGHighlightBorderBrush legacyBrush = new(null, Color.Red, HighlightAnimation.Progress) { AnimationProgress = 0.25 };

        shapeBrush.Draw(shapeRecorder.Args(), null, shape, geometry);
        legacyBrush.Draw(legacyRecorder.Args(), null, shape.OuterBounds, shape.NormalizedBorderThickness);

        //  Never touches the ring-quad code path (which only emits FillTriangle calls).
        Assert.Empty(shapeRecorder.Transaction.FillTriangleCalls);
        Assert.Empty(legacyRecorder.Transaction.FillTriangleCalls);
    }

    /// <summary>Tache 4 item 7's residual case (border thickness consumes the whole box, so there is no ring mesh): the shape-aware overload
    /// keeps routing to the legacy rectangle overload, byte-for-byte the same call as invoking it directly.</summary>
    [Fact]
    public void Progress_ResidualNoRingMeshCase_MatchesLegacyRectangleOverload()
    {
        Recorder shapeRecorder = Recorder.Create();
        Recorder legacyRecorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(80, 40, 40, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.False(geometry.UsesRectangleFastPath);
        Assert.False(geometry.HasBorderRingMesh);

        MGHighlightBorderBrush shapeBrush = new(null, Color.Red, HighlightAnimation.Progress) { AnimationProgress = 0.4 };
        MGHighlightBorderBrush legacyBrush = new(null, Color.Red, HighlightAnimation.Progress) { AnimationProgress = 0.4 };

        shapeBrush.Draw(shapeRecorder.Args(), null, shape, geometry);
        legacyBrush.Draw(legacyRecorder.Args(), null, shape.OuterBounds, shape.NormalizedBorderThickness);

        Assert.Equal(legacyRecorder.Transaction.FillTriangleCalls, shapeRecorder.Transaction.FillTriangleCalls);
        Assert.Equal(legacyRecorder.Transaction.FillRectangleCalls, shapeRecorder.Transaction.FillRectangleCalls);
    }

    #endregion

    #region Scan

    /// <summary>Scan clips every ring quad against the same band rectangle the rectangle path would compute, so its output only ever covers the
    /// ring (outer minus inner contour) inside that band. At mid-height on this shape, the band sits well clear of both rounded corners, so the
    /// ring reduces to the left and right border strips: every emitted vertex stays within [0, thickness] or [width - thickness, width] on X.</summary>
    [Fact]
    public void Scan_Horizontal_OnlyEmitsInsideBandAndInsideRing()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(100, 60, 8, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);

        MGHighlightBorderBrush brush = new(null, Color.Red, HighlightAnimation.Scan)
        {
            ScanOrientation = Orientation.Horizontal,
            ScanSize = 0.2,
            AnimationProgress = 0.5
        };

        brush.Draw(recorder.Args(), null, shape, geometry);

        List<GraphFillTriangleCall> calls = recorder.Transaction.FillTriangleCalls;
        Assert.NotEmpty(calls);

        //  Band: height = round(0.2*60) = 12, center = 0 + (int)(0.5*60) = 30 -> Rectangle(0, 24, 100, 12).
        Rectangle band = new(0, 24, 100, 12);

        foreach (GraphFillTriangleCall call in calls)
        {
            foreach (Vector2 vertex in new[] { call.V0, call.V1, call.V2 })
            {
                Assert.InRange(vertex.Y, band.Top - 0.01f, band.Bottom + 0.01f);
                Assert.InRange(vertex.X, band.Left - 0.01f, band.Right + 0.01f);
                //  Ring only (never the fill interior): this far from both corners, that means the left or right border strip.
                Assert.True(vertex.X <= 8f + 0.01f || vertex.X >= 92f - 0.01f, $"vertex {vertex} fell inside the fill interior, not the ring");
            }
        }
    }

    /// <summary>The rectangle fast path keeps calling the legacy rectangle overload, unchanged.</summary>
    [Fact]
    public void Scan_RectangleFastPath_KeepsLegacyRectanglePath()
    {
        Recorder shapeRecorder = Recorder.Create();
        Recorder legacyRecorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(100, 60, 8, 0);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.True(geometry.UsesRectangleFastPath);

        MGHighlightBorderBrush shapeBrush = new(null, Color.Red, HighlightAnimation.Scan) { AnimationProgress = 0.5 };
        MGHighlightBorderBrush legacyBrush = new(null, Color.Red, HighlightAnimation.Scan) { AnimationProgress = 0.5 };

        shapeBrush.Draw(shapeRecorder.Args(), null, shape, geometry);
        legacyBrush.Draw(legacyRecorder.Args(), null, shape.OuterBounds, shape.NormalizedBorderThickness);

        Assert.Empty(shapeRecorder.Transaction.FillTriangleCalls);
        Assert.Equal(legacyRecorder.Transaction.FillRectangleCalls, shapeRecorder.Transaction.FillRectangleCalls);
        Assert.NotEmpty(shapeRecorder.Transaction.FillRectangleCalls);
    }

    /// <summary>Tache 4 item 7's residual case (no ring mesh): the shape-aware overload keeps routing to the legacy rectangle overload.</summary>
    [Fact]
    public void Scan_ResidualNoRingMeshCase_MatchesLegacyRectangleOverload()
    {
        Recorder shapeRecorder = Recorder.Create();
        Recorder legacyRecorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(80, 40, 40, 12);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.False(geometry.UsesRectangleFastPath);
        Assert.False(geometry.HasBorderRingMesh);

        MGHighlightBorderBrush shapeBrush = new(null, Color.Red, HighlightAnimation.Scan) { AnimationProgress = 0.5 };
        MGHighlightBorderBrush legacyBrush = new(null, Color.Red, HighlightAnimation.Scan) { AnimationProgress = 0.5 };

        shapeBrush.Draw(shapeRecorder.Args(), null, shape, geometry);
        legacyBrush.Draw(legacyRecorder.Args(), null, shape.OuterBounds, shape.NormalizedBorderThickness);

        Assert.Equal(legacyRecorder.Transaction.FillTriangleCalls, shapeRecorder.Transaction.FillTriangleCalls);
        Assert.Equal(legacyRecorder.Transaction.FillRectangleCalls, shapeRecorder.Transaction.FillRectangleCalls);
        Assert.NotEmpty(shapeRecorder.Transaction.FillRectangleCalls);
    }

    #endregion

    #region Helpers

    private static MGBoxShape RoundedShape(int width, int height, int thickness, int radius)
        => new(new Rectangle(0, 0, width, height), new Thickness(thickness), new MGCornerRadius(radius));

    /// <summary>Independently reimplements the "top-most then left-most" anchor rule described in
    /// Docs/Tasks/drawing-tasks.md (Progress on the rounded ring) directly over the public <see cref="MGBoxGeometry.OuterContour"/>, so this test
    /// verifies the documented contract rather than the production code's own helper.</summary>
    private static Vector2 FindAnchor(IReadOnlyList<Vector2> outerContour)
    {
        Vector2 anchor = outerContour[0];
        foreach (Vector2 candidate in outerContour)
        {
            if (candidate.Y < anchor.Y || (candidate.Y == anchor.Y && candidate.X < anchor.X))
            {
                anchor = candidate;
            }
        }

        return anchor;
    }

    private readonly record struct Recorder(GraphTestRuntime Runtime, GraphNoOpDrawTransaction Transaction)
    {
        public static Recorder Create(GraphTestRuntime runtime = null)
        {
            runtime ??= new GraphTestRuntime(new Rectangle(0, 0, 960, 540));
            return new(runtime, new GraphNoOpDrawTransaction(runtime, DrawSettings.Default));
        }

        public ElementDrawArgs Args()
            => new(new DrawBaseArgs(TimeSpan.Zero, Transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
    }

    #endregion
}
