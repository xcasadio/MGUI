using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphGeometryTests
{
    [Fact]
    public void BezierSampling_IncludesStartAndEndWithExpectedSegmentCount()
    {
        List<Vector2> points = new();

        GraphBezierGeometry.SampleCubic(Vector2.Zero, new Vector2(10, 0), new Vector2(10, 10), new Vector2(20, 10), points, 4);

        Assert.Equal(5, points.Count);
        Assert.Equal(Vector2.Zero, points[0]);
        Assert.Equal(new Vector2(20, 10), points[^1]);
    }

    [Fact]
    public void EdgeGeometryCache_ReusesCachedPointsUntilInputsChange()
    {
        GraphEdgeGeometryCache cache = new();
        Guid edgeId = Guid.NewGuid();

        IReadOnlyList<Vector2> first = cache.GetOrCreate(edgeId, Vector2.Zero, new Vector2(100, 0), 2.0f, 1.0f, 8);
        IReadOnlyList<Vector2> second = cache.GetOrCreate(edgeId, Vector2.Zero, new Vector2(100, 0), 2.0f, 1.0f, 8);
        IReadOnlyList<Vector2> third = cache.GetOrCreate(edgeId, Vector2.Zero, new Vector2(120, 0), 2.0f, 1.0f, 8);

        Assert.Same(first, second);
        Assert.Same(first, third);
        Assert.Equal(1, cache.CacheHits);
        Assert.Equal(2, cache.CacheMisses);
        Assert.Equal(new Vector2(120, 0), third[^1]);
    }

    [Fact]
    public void HitTestEdge_UsesToleranceAroundPolylineSegments()
    {
        GraphHitTestService hitTest = new();
        List<Vector2> points = new() { new Vector2(0, 0), new Vector2(100, 0), new Vector2(150, 50) };

        Assert.True(hitTest.HitTestEdge(points, new Vector2(50, 4), 5.0f));
        Assert.False(hitTest.HitTestEdge(points, new Vector2(50, 12), 5.0f));
    }

    [Fact]
    public void HitTestNode_ReturnsTopMostNodeContainingWorldPoint()
    {
        GraphNodeModel bottom = new(Guid.NewGuid(), "Line", "Bottom", new Vector2(0, 0)) { Size = new Vector2(100, 100) };
        GraphNodeModel top = new(Guid.NewGuid(), "Line", "Top", new Vector2(20, 20)) { Size = new Vector2(100, 100) };
        GraphHitTestService hitTest = new();

        GraphNodeModel result = hitTest.HitTestNode(new[] { bottom, top }, new Vector2(30, 30), new Vector2(160, 100));

        Assert.Same(top, result);
    }
}