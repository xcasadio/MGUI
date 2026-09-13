using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI;

internal sealed class MGGraphSurfaceCanvas : MGCanvas
{
    private const float MinimumGridPixelSpacing = 8.0f;
    private readonly MGGraphView GraphView;
    private readonly List<Vector2> TemporaryEdgePoints = new();

    public MGGraphSurfaceCanvas(MGWindow window, MGGraphView graphView)
        : base(window)
    {
        GraphView = graphView ?? throw new ArgumentNullException(nameof(graphView));
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        base.DrawSelf(DA, layoutBounds);

        if (DA.Opacity <= 0.0f || layoutBounds.Width <= 0 || layoutBounds.Height <= 0)
        {
            return;
        }

        if (GraphView.ShowGrid)
        {
            DrawGrid(DA, layoutBounds);
        }

        DrawEdges(DA, layoutBounds);
    }

    private void DrawGrid(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        var viewport = GraphView.ViewportTransform;
        var baseWorldStep = Math.Max(1.0f, viewport.GridSize);
        var zoom = Math.Max(0.01f, viewport.Zoom);
        var stepMultiplier = Math.Max(1, (int)MathF.Ceiling(MinimumGridPixelSpacing / (baseWorldStep * zoom)));
        var worldStep = baseWorldStep * stepMultiplier;
        var backgroundColor = ResolveVisualBrushColor(GraphView.NodesCanvas?.BackgroundBrush, new Color(18, 22, 26)) * DA.Opacity;
        var minorColor = ResolveBrushColor(GraphView.GridLineBrush, Color.White * 0.08f) * DA.Opacity;
        if (minorColor == Color.Transparent)
        {
            return;
        }

        var majorColor = GraphView.MajorGridLineBrush == null
            ? Color.Lerp(minorColor, backgroundColor, 0.55f)
            : ResolveBrushColor(GraphView.MajorGridLineBrush, Color.Transparent) * DA.Opacity;
        var viewportBounds = GetViewportLocalBounds(layoutBounds);
        var topLeftWorld = viewport.LayoutToWorld(new Vector2(viewportBounds.Left, viewportBounds.Top));
        var bottomRightWorld = viewport.LayoutToWorld(new Vector2(viewportBounds.Right, viewportBounds.Bottom));
        var worldLeft = Math.Min(topLeftWorld.X, bottomRightWorld.X);
        var worldRight = Math.Max(topLeftWorld.X, bottomRightWorld.X);
        var worldTop = Math.Min(topLeftWorld.Y, bottomRightWorld.Y);
        var worldBottom = Math.Max(topLeftWorld.Y, bottomRightWorld.Y);
        var firstWorldX = MathF.Floor(worldLeft / worldStep) * worldStep;
        var firstWorldY = MathF.Floor(worldTop / worldStep) * worldStep;
        var origin = DA.Offset.ToVector2();

        for (var worldX = firstWorldX; worldX <= worldRight + worldStep * 0.5f; worldX += worldStep)
        {
            var layoutX = ViewportToLayout(viewport.WorldToLayout(new Vector2(worldX, 0.0f))).X;
            var color = IsMajorGridLine(worldX, baseWorldStep, stepMultiplier) ? majorColor : minorColor;
            DA.DT.StrokeLineSegment(origin, new Vector2(layoutX, layoutBounds.Top), new Vector2(layoutX, layoutBounds.Bottom), color, 1.0f);
        }

        for (var worldY = firstWorldY; worldY <= worldBottom + worldStep * 0.5f; worldY += worldStep)
        {
            var layoutY = ViewportToLayout(viewport.WorldToLayout(new Vector2(0.0f, worldY))).Y;
            var color = IsMajorGridLine(worldY, baseWorldStep, stepMultiplier) ? majorColor : minorColor;
            DA.DT.StrokeLineSegment(origin, new Vector2(layoutBounds.Left, layoutY), new Vector2(layoutBounds.Right, layoutY), color, 1.0f);
        }
    }

    private bool IsMajorGridLine(float worldCoordinate, float baseWorldStep, int stepMultiplier)
    {
        var majorFrequency = Math.Max(1, GraphView.MajorGridLineFrequency);
        var gridIndex = (int)MathF.Round(worldCoordinate / baseWorldStep);
        return gridIndex % Math.Max(majorFrequency, stepMultiplier) == 0;
    }

    private void DrawEdges(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        var document = GraphView.Document;
        if (document == null)
        {
            return;
        }

        var edgeColor = ResolveBrushColor(GraphView.EdgeBrush, new Color(128, 180, 255)) * DA.Opacity;
        if (edgeColor == Color.Transparent)
        {
            return;
        }

        var selectedEdgeColor = GraphView.GetTheme()?.Graph?.SelectedEdgeBrush is IFillBrush selectedEdgeBrush
            ? ResolveBrushColor(selectedEdgeBrush, Color.Yellow) * DA.Opacity
            : Color.Lerp(edgeColor, Color.White, 0.35f);

        var thickness = Math.Max(0.1f, GraphView.EdgeThickness);
        var segmentCount = GraphView.ViewportTransform.Zoom < 0.35f ? 8 : GraphBezierGeometry.DefaultSegmentCount;
        var origin = DA.Offset.ToVector2();
        var worldViewport = GraphView.GetCullingWorldViewport(GetViewportLocalBounds(layoutBounds));
        var edgesVisible = 0;
        var edgesCulled = 0;

        for (var edgeIndex = 0; edgeIndex < document.Edges.Count; edgeIndex++)
        {
            var edge = document.Edges[edgeIndex];
            if (edge == null)
            {
                continue;
            }

            if (!TryGetPortAnchor(edge.SourcePortId, out var start) || !TryGetPortAnchor(edge.TargetPortId, out var end))
            {
                continue;
            }

            if (GraphView.EnableViewportCulling)
            {
                var startWorld = GraphView.ViewportTransform.LayoutToWorld(LayoutToViewport(start));
                var endWorld = GraphView.ViewportTransform.LayoutToWorld(LayoutToViewport(end));
                if (!GraphView.CullingService.ShouldDrawEdge(edge, startWorld, endWorld, worldViewport, GraphView.SelectedEdgeIds))
                {
                    edgesCulled++;
                    continue;
                }
            }

            edgesVisible++;
            var isSelected = GraphView.SelectedEdgeIds.Contains(edge.Id);
            var edgeThickness = isSelected ? thickness + 1.0f : thickness;
            var currentEdgeColor = isSelected ? selectedEdgeColor : edgeColor;
            var points = GraphView.EdgeGeometryCache.GetOrCreate(edge.Id, start, end, edgeThickness, GraphView.ViewportTransform.Zoom, segmentCount);
            for (var pointIndex = 1; pointIndex < points.Count; pointIndex++)
            {
                DA.DT.StrokeLineSegment(origin, points[pointIndex - 1], points[pointIndex], currentEdgeColor, edgeThickness);
            }
        }

        GraphView.SetEdgeCullingDiagnostics(edgesVisible, edgesCulled);

        DrawTemporaryConnection(DA, origin, edgeColor, thickness);
    }

    private void DrawTemporaryConnection(ElementDrawArgs DA, Vector2 origin, Color defaultEdgeColor, float thickness)
    {
        var controller = GraphView.ConnectionController;
        if (controller?.IsDragging != true)
        {
            return;
        }

        var previewColor = controller.HoverPortId.HasValue && controller.PreviewValidationResult?.IsValid == false
            ? Color.OrangeRed * DA.Opacity
            : defaultEdgeColor;
        GraphBezierGeometry.BuildDefaultEdge(controller.StartViewportPoint, controller.CurrentViewportPoint, TemporaryEdgePoints, 16);
        for (var pointIndex = 1; pointIndex < TemporaryEdgePoints.Count; pointIndex++)
        {
            DA.DT.StrokeLineSegment(origin, TemporaryEdgePoints[pointIndex - 1], TemporaryEdgePoints[pointIndex], previewColor, thickness);
        }
    }

    private bool TryGetPortAnchor(Guid portId, out Vector2 layoutAnchor)
    {
        if (GraphView.TryGetPortControl(portId, out var port) && IsRuntimePortAnchorUsable(port))
        {
            layoutAnchor = port.GetLayoutAnchor();
            return true;
        }

        return TryGetFallbackPortAnchor(portId, out layoutAnchor);
    }

    private bool TryGetFallbackPortAnchor(Guid portId, out Vector2 layoutAnchor)
    {
        if (GraphPortAnchorResolver.TryGetPortLayoutAnchor(GraphView.Document, GraphView.ViewportTransform, portId, out var viewportAnchor))
        {
            layoutAnchor = ViewportToLayout(viewportAnchor);
            return true;
        }

        layoutAnchor = default;
        return false;
    }

    private Vector2 LayoutToViewport(Vector2 layoutPoint)
        => layoutPoint - GetViewportLayoutOrigin();

    private Vector2 ViewportToLayout(Vector2 viewportPoint)
        => viewportPoint + GetViewportLayoutOrigin();

    private Rectangle GetViewportLocalBounds(Rectangle layoutBounds)
    {
        var viewportTopLeft = LayoutToViewport(new Vector2(layoutBounds.Left, layoutBounds.Top));
        return new Rectangle((int)MathF.Round(viewportTopLeft.X), (int)MathF.Round(viewportTopLeft.Y), layoutBounds.Width, layoutBounds.Height);
    }

    private Vector2 GetViewportLayoutOrigin()
    {
        var bounds = GraphView.NodesCanvas?.AlignedContentBounds ?? GraphView.NodesCanvas?.LayoutBounds ?? Rectangle.Empty;
        return new Vector2(bounds.Left, bounds.Top);
    }

    private static Color ResolveVisualBrushColor(VisualStateFillBrush brush, Color fallback)
        => brush?.NormalValue is MGSolidFillBrush solid ? solid.Color : fallback;

    private bool IsRuntimePortAnchorUsable(MGGraphPort port)
    {
        if (port == null || port.Visibility != Visibility.Visible || !HasVisibleActualBounds(port))
        {
            return false;
        }

        if (port.Model == null || port.Model.NodeId == Guid.Empty)
        {
            return true;
        }

        return GraphView.TryGetNodeControl(port.Model.NodeId, out var node) && node.Visibility == Visibility.Visible && HasVisibleActualBounds(node);
    }

    private static bool HasVisibleActualBounds(MGElement element)
        => element != null && element.ActualLayoutBounds.Width > 0 && element.ActualLayoutBounds.Height > 0;

    private static Color ResolveBrushColor(IFillBrush brush, Color fallback)
        => brush is MGSolidFillBrush solid ? solid.Color : fallback;
}