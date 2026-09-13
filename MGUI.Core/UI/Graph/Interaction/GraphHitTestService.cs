using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph;

public sealed class GraphHitTestService
{
    public bool HitTestEdge(IReadOnlyList<Vector2> points, Vector2 point, float tolerance)
    {
        if (points == null || points.Count < 2)
        {
            return false;
        }

        var toleranceSquared = Math.Max(0.0f, tolerance) * Math.Max(0.0f, tolerance);
        for (var i = 0; i < points.Count - 1; i++)
        {
            if (DistanceSquaredToSegment(point, points[i], points[i + 1]) <= toleranceSquared)
            {
                return true;
            }
        }

        return false;
    }

    public GraphNodeModel HitTestNode(IReadOnlyList<GraphNodeModel> nodes, Vector2 worldPoint, Vector2 fallbackSize)
    {
        if (nodes == null)
        {
            return null;
        }

        for (var i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];
            var size = node.Size ?? fallbackSize;
            if (worldPoint.X >= node.Position.X && worldPoint.X <= node.Position.X + size.X &&
                worldPoint.Y >= node.Position.Y && worldPoint.Y <= node.Position.Y + size.Y)
            {
                return node;
            }
        }

        return null;
    }

    public static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon)
        {
            return Vector2.DistanceSquared(point, start);
        }

        var t = Vector2.Dot(point - start, segment) / lengthSquared;
        t = Math.Clamp(t, 0.0f, 1.0f);
        var projection = start + segment * t;
        return Vector2.DistanceSquared(point, projection);
    }
}