using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Graph;

public struct GraphCullingDiagnostics
{
    public int NodesVisible { get; set; }
    public int NodesCulled { get; set; }
    public int CommentsVisible { get; set; }
    public int CommentsCulled { get; set; }
    public int EdgesVisible { get; set; }
    public int EdgesCulled { get; set; }
    public int EdgeCacheHits { get; set; }
    public int EdgeCacheMisses { get; set; }
}

public sealed class GraphCullingService
{
    public RectangleF CreateWorldViewport(GraphViewportTransform viewport, Rectangle layoutBounds, float padding)
    {
        if (viewport == null || layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return new RectangleF(float.MinValue * 0.25f, float.MinValue * 0.25f, float.MaxValue * 0.5f, float.MaxValue * 0.5f);
        }

        Vector2 topLeft = viewport.LayoutToWorld(new Vector2(layoutBounds.Left, layoutBounds.Top));
        Vector2 bottomRight = viewport.LayoutToWorld(new Vector2(layoutBounds.Right, layoutBounds.Bottom));
        float left = Math.Min(topLeft.X, bottomRight.X) - padding;
        float top = Math.Min(topLeft.Y, bottomRight.Y) - padding;
        float right = Math.Max(topLeft.X, bottomRight.X) + padding;
        float bottom = Math.Max(topLeft.Y, bottomRight.Y) + padding;
        return new RectangleF(left, top, Math.Max(1.0f, right - left), Math.Max(1.0f, bottom - top));
    }

    public bool IsNodeVisible(GraphNodeModel node, RectangleF worldViewport)
        => node != null && Intersects(GraphSelectionManager.GetNodeWorldBounds(node), worldViewport);

    public bool IsCommentVisible(GraphCommentModel comment, RectangleF worldViewport)
        => comment != null && Intersects(ToRectangleF(comment.Bounds), worldViewport);

    public bool ShouldDrawEdge(GraphDocument document, GraphEdgeModel edge, RectangleF worldViewport, ISet<Guid> selectedEdgeIds)
    {
        if (edge == null)
        {
            return false;
        }

        if (selectedEdgeIds != null && selectedEdgeIds.Contains(edge.Id))
        {
            return true;
        }

        return TryGetEdgeWorldBounds(document, edge, out RectangleF bounds) && Intersects(bounds, worldViewport);
    }

    public bool ShouldDrawEdge(GraphEdgeModel edge, Vector2 startWorld, Vector2 endWorld, RectangleF worldViewport, ISet<Guid> selectedEdgeIds)
    {
        if (edge == null)
        {
            return false;
        }

        if (selectedEdgeIds != null && selectedEdgeIds.Contains(edge.Id))
        {
            return true;
        }

        RectangleF bounds = CreateBounds(startWorld, endWorld, 48.0f);
        return Intersects(bounds, worldViewport);
    }

    public bool TryGetEdgeWorldBounds(GraphDocument document, GraphEdgeModel edge, out RectangleF bounds)
    {
        bounds = default;
        if (document == null || edge == null ||
            !TryGetPortWorldAnchor(document, edge.SourcePortId, out Vector2 start) ||
            !TryGetPortWorldAnchor(document, edge.TargetPortId, out Vector2 end))
        {
            return false;
        }

        bounds = CreateBounds(start, end, 48.0f);
        return true;
    }

    public bool TryGetPortWorldAnchor(GraphDocument document, Guid portId, out Vector2 worldAnchor)
        => GraphPortAnchorResolver.TryGetPortWorldAnchor(document, portId, out worldAnchor);

    private static RectangleF CreateBounds(Vector2 first, Vector2 second, float padding)
    {
        float left = Math.Min(first.X, second.X) - padding;
        float top = Math.Min(first.Y, second.Y) - padding;
        float right = Math.Max(first.X, second.X) + padding;
        float bottom = Math.Max(first.Y, second.Y) + padding;
        return new RectangleF(left, top, Math.Max(1.0f, right - left), Math.Max(1.0f, bottom - top));
    }

    private static RectangleF ToRectangleF(Rectangle rectangle)
        => new(rectangle.X, rectangle.Y, Math.Max(1, rectangle.Width), Math.Max(1, rectangle.Height));

    private static bool Intersects(RectangleF first, RectangleF second)
        => first.Left < second.Right && first.Right > second.Left && first.Top < second.Bottom && first.Bottom > second.Top;
}