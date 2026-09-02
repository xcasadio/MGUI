using System.Collections.Generic;
using MGUI.Core.UI.Shapes;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>Unit tests for <see cref="MGConvexPolygonClipper"/>, the Sutherland-Hodgman clipping / fan-triangulation / UV-interpolation helper
/// extracted from <c>MGTexturedBorderBrush</c>'s rounded-ring rendering (see <see cref="TexturedPaintProjectionTests"/> for the regression
/// coverage proving the brush's output is unchanged by the extraction).</summary>
public class MGConvexPolygonClipperTests
{
    #region ClipToRectangle

    [Fact]
    public void ClipToRectangle_RectangleInsideLargerRectangle_IsUnchangedWithSameVertexOrderAndCornerIdentity()
    {
        List<Vector2> subject = new() { new(10, 10), new(20, 10), new(20, 30), new(10, 30) };
        Rectangle largerRect = new(0, 0, 100, 100);
        List<Vector2> output = new();
        List<Vector2> scratch = new();

        MGConvexPolygonClipper.ClipToRectangle(subject, largerRect, output, scratch);

        Assert.Equal(subject, output);
    }

    [Fact]
    public void ClipToRectangle_DisjointRectangle_IsEmpty()
    {
        List<Vector2> subject = new() { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };
        Rectangle disjointRect = new(50, 50, 20, 20);
        List<Vector2> output = new();
        List<Vector2> scratch = new();

        MGConvexPolygonClipper.ClipToRectangle(subject, disjointRect, output, scratch);

        Assert.Empty(output);
    }

    [Fact]
    public void ClipToRectangle_PartialOverlap_CutsToTheClipBounds()
    {
        //  A 0..10 square clipped to x in [5, 100) keeps only the right half: x in [5, 10].
        List<Vector2> subject = new() { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };
        Rectangle clipRect = new(5, 0, 100, 10);
        List<Vector2> output = new();
        List<Vector2> scratch = new();

        MGConvexPolygonClipper.ClipToRectangle(subject, clipRect, output, scratch);

        Assert.All(output, v => Assert.True(v.X >= 5f - 1e-4f));
        Assert.Contains(output, v => System.Math.Abs(v.X - 5f) < 1e-4f);
        Assert.Contains(output, v => System.Math.Abs(v.X - 10f) < 1e-4f);
    }

    #endregion

    #region ClipToConvexPolygon

    [Fact]
    public void ClipToConvexPolygon_RectangleClippedByRoundedContour_LosesItsCornerRegion()
    {
        //  Octagon approximating a rounded rectangle: the corner at (0,0) is cut off by the diagonal edge between (0,3) and (3,0).
        List<Vector2> roundedContour = new()
        {
            new(3, 0), new(17, 0), new(20, 3), new(20, 17),
            new(17, 20), new(3, 20), new(0, 17), new(0, 3)
        };
        //  A small square sitting exactly in the sharp corner that the rounding removes.
        List<Vector2> cornerSquare = new() { new(0, 0), new(3, 0), new(3, 3), new(0, 3) };
        List<Vector2> output = new();
        List<Vector2> scratch = new();

        MGConvexPolygonClipper.ClipToConvexPolygon(cornerSquare, roundedContour, output, scratch);

        Assert.DoesNotContain(output, v => System.Math.Abs(v.X) < 1e-4f && System.Math.Abs(v.Y) < 1e-4f);
        Assert.True(PolygonArea(output) < PolygonArea(cornerSquare) - 1e-3f, "the rounded contour must cut away part of the sharp corner");
    }

    [Fact]
    public void ClipToConvexPolygon_WorksForBothClipWindings()
    {
        List<Vector2> subject = new() { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };
        List<Vector2> clockwiseClip = new() { new(-5, -5), new(-5, 5), new(5, 5), new(5, -5) };
        List<Vector2> counterClockwiseClip = new() { new(-5, -5), new(5, -5), new(5, 5), new(-5, 5) };
        List<Vector2> outputCw = new();
        List<Vector2> outputCcw = new();
        List<Vector2> scratch = new();

        MGConvexPolygonClipper.ClipToConvexPolygon(subject, clockwiseClip, outputCw, scratch);
        MGConvexPolygonClipper.ClipToConvexPolygon(subject, counterClockwiseClip, outputCcw, scratch);

        Assert.True(PolygonArea(outputCw) > 0);
        Assert.Equal(PolygonArea(outputCw), PolygonArea(outputCcw), 3);
    }

    [Fact]
    public void ClipToConvexPolygon_DisjointPolygons_IsEmpty()
    {
        List<Vector2> subject = new() { new(0, 0), new(10, 0), new(10, 10), new(0, 10) };
        List<Vector2> disjointClip = new() { new(50, 50), new(60, 50), new(60, 60), new(50, 60) };
        List<Vector2> output = new();
        List<Vector2> scratch = new();

        MGConvexPolygonClipper.ClipToConvexPolygon(subject, disjointClip, output, scratch);

        Assert.Empty(output);
    }

    #endregion

    #region AppendFanTriangles

    [Theory]
    [InlineData(3, 1)]
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(8, 6)]
    public void AppendFanTriangles_ProducesExpectedTriangleCount(int vertexCount, int expectedTriangleCount)
    {
        List<int> indices = new();

        MGConvexPolygonClipper.AppendFanTriangles(firstVertexIndex: 100, vertexCount, indices);

        Assert.Equal(expectedTriangleCount * 3, indices.Count);
    }

    [Fact]
    public void AppendFanTriangles_EveryTriangleSharesThePivotVertex()
    {
        List<int> indices = new();

        MGConvexPolygonClipper.AppendFanTriangles(firstVertexIndex: 5, vertexCount: 6, indices);

        for (int i = 0; i < indices.Count; i += 3)
        {
            Assert.Equal(5, indices[i]);
        }
        Assert.Equal(new[] { 5, 6, 7, 5, 7, 8, 5, 8, 9, 5, 9, 10 }, indices);
    }

    [Fact]
    public void AppendFanTriangles_FewerThanThreeVertices_AppendsNothing()
    {
        List<int> indices = new();

        MGConvexPolygonClipper.AppendFanTriangles(firstVertexIndex: 0, vertexCount: 2, indices);

        Assert.Empty(indices);
    }

    [Fact]
    public void AppendFanTriangles_AppendsToExistingIndices()
    {
        List<int> indices = new() { 1, 2, 3 };

        MGConvexPolygonClipper.AppendFanTriangles(firstVertexIndex: 10, vertexCount: 4, indices);

        Assert.Equal(new[] { 1, 2, 3, 10, 11, 12, 10, 12, 13 }, indices);
    }

    #endregion

    #region InterpolateRectUV

    [Fact]
    public void InterpolateRectUV_MapsDestTopLeftAndBottomRightToTheGivenUVs()
    {
        Rectangle sourceRect = new(10, 20, 100, 50);
        Vector2 uvTopLeft = new(0.25f, 0.5f);
        Vector2 uvBottomRight = new(0.75f, 1f);

        Vector2 topLeft = MGConvexPolygonClipper.InterpolateRectUV(new Vector2(10, 20), sourceRect, uvTopLeft, uvBottomRight);
        Vector2 bottomRight = MGConvexPolygonClipper.InterpolateRectUV(new Vector2(110, 70), sourceRect, uvTopLeft, uvBottomRight);
        Vector2 center = MGConvexPolygonClipper.InterpolateRectUV(new Vector2(60, 45), sourceRect, uvTopLeft, uvBottomRight);

        Assert.Equal(uvTopLeft.X, topLeft.X, 5);
        Assert.Equal(uvTopLeft.Y, topLeft.Y, 5);
        Assert.Equal(uvBottomRight.X, bottomRight.X, 5);
        Assert.Equal(uvBottomRight.Y, bottomRight.Y, 5);
        Assert.Equal((uvTopLeft.X + uvBottomRight.X) / 2f, center.X, 5);
        Assert.Equal((uvTopLeft.Y + uvBottomRight.Y) / 2f, center.Y, 5);
    }

    [Fact]
    public void InterpolateRectUV_PointOutsideSourceRect_IsClamped()
    {
        Rectangle sourceRect = new(0, 0, 10, 10);
        Vector2 uvTopLeft = Vector2.Zero;
        Vector2 uvBottomRight = Vector2.One;

        Vector2 beyondBottomRight = MGConvexPolygonClipper.InterpolateRectUV(new Vector2(100, 100), sourceRect, uvTopLeft, uvBottomRight);
        Vector2 beyondTopLeft = MGConvexPolygonClipper.InterpolateRectUV(new Vector2(-100, -100), sourceRect, uvTopLeft, uvBottomRight);

        Assert.Equal(1f, beyondBottomRight.X, 5);
        Assert.Equal(1f, beyondBottomRight.Y, 5);
        Assert.Equal(0f, beyondTopLeft.X, 5);
        Assert.Equal(0f, beyondTopLeft.Y, 5);
    }

    #endregion

    private static float PolygonArea(IReadOnlyList<Vector2> polygon)
    {
        if (polygon.Count < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];
            area += a.X * b.Y - b.X * a.Y;
        }

        return System.Math.Abs(area) / 2f;
    }
}
