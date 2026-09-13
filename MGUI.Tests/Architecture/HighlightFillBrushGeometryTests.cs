using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Shapes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>Shape-aware overload of <see cref="MGHighlightFillBrush"/>: on a rounded <see cref="MGBoxShape"/>, the focused/unfocused rectangles
/// computed by the rectangle path are clipped against <see cref="MGBoxGeometry.OuterContour"/> instead of being painted straight over the
/// silhouette (see <see cref="MGConvexPolygonClipper.ClipToConvexPolygon(IReadOnlyList{Vector2}, IReadOnlyList{Vector2}, List{Vector2}, List{Vector2})"/>).</summary>
public class HighlightFillBrushGeometryTests
{
    [Fact]
    public void RoundedShape_UnfocusedFill_StaysInsideSilhouette_AndExcludesFocusedRect()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(100, 60, 0, 20);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.False(geometry.UsesRectangleFastPath);

        Rectangle focusedRect = new(30, 22, 20, 16);
        MGHighlightFillBrush brush = new(FillFocusedRegion: false, FillUnfocusedRegion: true)
        {
            FocusedBounds = new List<Rectangle> { focusedRect }
        };

        brush.Draw(recorder.Args(), null!, shape, geometry);

        List<GraphFillTriangleCall> calls = recorder.Transaction.FillTriangleCalls;
        Assert.NotEmpty(calls);

        //  No overflow beyond the rounded silhouette: every emitted vertex, pulled slightly toward the centre, is inside the shape
        //  (mirrors the tolerance used for the arc vertices in TexturedPaintProjectionTests).
        Vector2 center = shape.OuterBounds.Center.ToVector2();
        foreach (GraphFillTriangleCall call in calls)
        {
            foreach (Vector2 vertex in new[] { call.V0, call.V1, call.V2 })
            {
                Vector2 inward = vertex == center ? vertex : vertex + Vector2.Normalize(center - vertex) * 0.25f;
                Assert.True(shape.Contains(inward), $"vertex {vertex} lies outside the rounded shape");
            }
        }

        //  The focused rectangle stays excluded from the unfocused fill: no emitted triangle covers its centre.
        Vector2 focusedCenter = focusedRect.Center.ToVector2();
        Assert.DoesNotContain(calls, call => PointInTriangle(focusedCenter, call.V0, call.V1, call.V2));
    }

    [Fact]
    public void RoundedShape_FocusedRectFullyInsideShape_EmitsSameRectanglePolygonInSameOrder()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(100, 60, 0, 15);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.False(geometry.UsesRectangleFastPath);

        //  Well clear of all 4 corner quadrants (radius 15), so the clip leaves this rectangle untouched.
        Rectangle focusedRect = new(25, 20, 20, 15);
        Color focusedColor = Color.White * 0.5f;
        MGHighlightFillBrush brush = new(FillFocusedRegion: true, FocusedOverlayColor: focusedColor, FillUnfocusedRegion: false)
        {
            FocusedBounds = new List<Rectangle> { focusedRect }
        };

        brush.Draw(recorder.Args(), null!, shape, geometry);

        List<GraphFillTriangleCall> calls = recorder.Transaction.FillTriangleCalls;
        Assert.Equal(2, calls.Count);

        Vector2 topLeft = new(focusedRect.Left, focusedRect.Top);
        Vector2 topRight = new(focusedRect.Right, focusedRect.Top);
        Vector2 bottomRight = new(focusedRect.Right, focusedRect.Bottom);
        Vector2 bottomLeft = new(focusedRect.Left, focusedRect.Bottom);

        //  Fan triangulation, pivoted at the first vertex (TopLeft/TopRight/BottomRight/BottomLeft), same order the rectangle path would imply.
        Assert.Equal((topLeft, topRight, bottomRight), (calls[0].V0, calls[0].V1, calls[0].V2));
        Assert.Equal((topLeft, bottomRight, bottomLeft), (calls[1].V0, calls[1].V1, calls[1].V2));
        Assert.All(calls, call => Assert.Equal(focusedColor, call.C0));
    }

    [Fact]
    public void RectangleFastPath_KeepsRectangleDrawCalls()
    {
        Recorder recorder = Recorder.Create();
        MGBoxShape shape = RoundedShape(100, 60, 0, 0);
        MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape);
        Assert.True(geometry.UsesRectangleFastPath);

        Rectangle focusedRect = new(30, 20, 20, 15);
        MGHighlightFillBrush brush = new(FillFocusedRegion: true, FillUnfocusedRegion: true)
        {
            FocusedBounds = new List<Rectangle> { focusedRect }
        };

        brush.Draw(recorder.Args(), null!, shape, geometry);

        Assert.Empty(recorder.Transaction.FillTriangleCalls);
        Assert.NotEmpty(recorder.Transaction.FillRectangleCalls);
        Assert.Contains(recorder.Transaction.FillRectangleCalls, call => call.Destination == (RectangleF)focusedRect);
    }

    #region Helpers

    private static MGBoxShape RoundedShape(int width, int height, int thickness, int radius)
        => new(new Rectangle(0, 0, width, height), new Thickness(thickness), new MGCornerRadius(radius));

    private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p - a, b - a);
        float d2 = Cross(p - b, c - b);
        float d3 = Cross(p - c, a - c);

        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }

    private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private readonly record struct Recorder(GraphTestRuntime Runtime, GraphNoOpDrawTransaction Transaction)
    {
        public static Recorder Create(GraphTestRuntime? runtime = null)
        {
            runtime ??= new GraphTestRuntime(new Rectangle(0, 0, 960, 540));
            return new(runtime, new GraphNoOpDrawTransaction(runtime, DrawSettings.Default));
        }

        public ElementDrawArgs Args() => Args(Point.Zero);

        public ElementDrawArgs Args(Point offset)
            => new(new DrawBaseArgs(TimeSpan.Zero, Transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), offset);
    }

    #endregion
}
