using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Graph;

public class GraphViewportTransform
{
    private float _Zoom = 1.0f;
    private float _MinZoom = 0.1f;
    private float _MaxZoom = 4.0f;
    private float _GridSize = 16.0f;

    public Vector2 Pan { get; set; }

    public float Zoom
    {
        get => _Zoom;
        set => _Zoom = Math.Clamp(value, MinZoom, MaxZoom);
    }

    public float MinZoom
    {
        get => _MinZoom;
        set
        {
            _MinZoom = Math.Max(0.01f, value);
            if (_MaxZoom < _MinZoom)
            {
                _MaxZoom = _MinZoom;
            }

            Zoom = _Zoom;
        }
    }

    public float MaxZoom
    {
        get => _MaxZoom;
        set
        {
            _MaxZoom = Math.Max(MinZoom, value);
            Zoom = _Zoom;
        }
    }

    public float GridSize
    {
        get => _GridSize;
        set => _GridSize = Math.Max(1.0f, value);
    }

    public Vector2 WorldToViewport(Vector2 worldPoint) => worldPoint * Zoom + Pan;

    public Vector2 ViewportToWorld(Vector2 viewportPoint) => (viewportPoint - Pan) / Zoom;

    public Vector2 WorldToLayout(Vector2 worldPoint) => WorldToViewport(worldPoint);

    public Vector2 LayoutToWorld(Vector2 layoutPoint) => ViewportToWorld(layoutPoint);

    public void PanBy(Vector2 delta) => Pan += delta;

    public void ZoomAt(Vector2 viewportPoint, float zoomFactor)
    {
        if (zoomFactor <= 0.0f)
        {
            return;
        }

        var worldUnderCursor = ViewportToWorld(viewportPoint);
        Zoom = Zoom * zoomFactor;
        Pan = viewportPoint - worldUnderCursor * Zoom;
    }

    public Vector2 SnapPoint(Vector2 worldPoint)
    {
        var gridSize = GridSize;
        return new Vector2(
            MathF.Round(worldPoint.X / gridSize) * gridSize,
            MathF.Round(worldPoint.Y / gridSize) * gridSize);
    }

    public void FrameOrigin(Rectangle viewportBounds)
    {
        Zoom = 1.0f;
        Pan = new Vector2(viewportBounds.X + viewportBounds.Width * 0.5f, viewportBounds.Y + viewportBounds.Height * 0.5f);
    }

    public void FrameBounds(RectangleF worldBounds, Rectangle viewportBounds, float padding)
    {
        if (worldBounds.Width <= 0.0f || worldBounds.Height <= 0.0f || viewportBounds.Width <= 0 || viewportBounds.Height <= 0)
        {
            FrameOrigin(viewportBounds);
            return;
        }

        var availableWidth = Math.Max(1.0f, viewportBounds.Width - padding * 2.0f);
        var availableHeight = Math.Max(1.0f, viewportBounds.Height - padding * 2.0f);
        Zoom = Math.Min(availableWidth / worldBounds.Width, availableHeight / worldBounds.Height);

        Vector2 viewportCenter = new(viewportBounds.X + viewportBounds.Width * 0.5f, viewportBounds.Y + viewportBounds.Height * 0.5f);
        Vector2 worldCenter = new(worldBounds.X + worldBounds.Width * 0.5f, worldBounds.Y + worldBounds.Height * 0.5f);
        Pan = viewportCenter - worldCenter * Zoom;
    }

    public void FrameAll(IEnumerable<GraphNodeModel> nodes, Rectangle viewportBounds, float padding)
    {
        if (!TryCreateNodeBounds(nodes, out var bounds))
        {
            FrameOrigin(viewportBounds);
            return;
        }

        FrameBounds(bounds, viewportBounds, padding);
    }

    public bool TryCreateNodeBounds(IEnumerable<GraphNodeModel> nodes, out RectangleF bounds)
    {
        bounds = default;
        if (nodes == null)
        {
            return false;
        }

        var hasAny = false;
        var left = 0.0f;
        var top = 0.0f;
        var right = 0.0f;
        var bottom = 0.0f;

        foreach (var node in nodes)
        {
            if (node == null)
            {
                continue;
            }

            var size = GraphSelectionManager.GetNodeWorldSize(node);
            var nodeLeft = node.Position.X;
            var nodeTop = node.Position.Y;
            var nodeRight = node.Position.X + Math.Max(1.0f, size.X);
            var nodeBottom = node.Position.Y + Math.Max(1.0f, size.Y);

            if (!hasAny)
            {
                left = nodeLeft;
                top = nodeTop;
                right = nodeRight;
                bottom = nodeBottom;
                hasAny = true;
            }
            else
            {
                left = Math.Min(left, nodeLeft);
                top = Math.Min(top, nodeTop);
                right = Math.Max(right, nodeRight);
                bottom = Math.Max(bottom, nodeBottom);
            }
        }

        if (!hasAny)
        {
            return false;
        }

        bounds = new RectangleF(left, top, right - left, bottom - top);
        return true;
    }
}