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
        GraphViewportTransform viewport = GraphView.ViewportTransform;
        float baseWorldStep = Math.Max(1.0f, viewport.GridSize);
        float zoom = Math.Max(0.01f, viewport.Zoom);
        int stepMultiplier = Math.Max(1, (int)MathF.Ceiling(MinimumGridPixelSpacing / (baseWorldStep * zoom)));
        float worldStep = baseWorldStep * stepMultiplier;
        Color backgroundColor = ResolveVisualBrushColor(GraphView.NodesCanvas?.BackgroundBrush, new Color(18, 22, 26)) * DA.Opacity;
        Color minorColor = ResolveBrushColor(GraphView.GridLineBrush, Color.White * 0.08f) * DA.Opacity;
        if (minorColor == Color.Transparent)
        {
            return;
        }

        Color majorColor = GraphView.MajorGridLineBrush == null
            ? Color.Lerp(minorColor, backgroundColor, 0.55f)
            : ResolveBrushColor(GraphView.MajorGridLineBrush, Color.Transparent) * DA.Opacity;
        Rectangle viewportBounds = GetViewportLocalBounds(layoutBounds);
        Vector2 topLeftWorld = viewport.LayoutToWorld(new Vector2(viewportBounds.Left, viewportBounds.Top));
        Vector2 bottomRightWorld = viewport.LayoutToWorld(new Vector2(viewportBounds.Right, viewportBounds.Bottom));
        float worldLeft = Math.Min(topLeftWorld.X, bottomRightWorld.X);
        float worldRight = Math.Max(topLeftWorld.X, bottomRightWorld.X);
        float worldTop = Math.Min(topLeftWorld.Y, bottomRightWorld.Y);
        float worldBottom = Math.Max(topLeftWorld.Y, bottomRightWorld.Y);
        float firstWorldX = MathF.Floor(worldLeft / worldStep) * worldStep;
        float firstWorldY = MathF.Floor(worldTop / worldStep) * worldStep;
        Vector2 origin = DA.Offset.ToVector2();

        for (float worldX = firstWorldX; worldX <= worldRight + worldStep * 0.5f; worldX += worldStep)
        {
            float layoutX = ViewportToLayout(viewport.WorldToLayout(new Vector2(worldX, 0.0f))).X;
            Color color = IsMajorGridLine(worldX, baseWorldStep, stepMultiplier) ? majorColor : minorColor;
            DA.DT.StrokeLineSegment(origin, new Vector2(layoutX, layoutBounds.Top), new Vector2(layoutX, layoutBounds.Bottom), color, 1.0f);
        }

        for (float worldY = firstWorldY; worldY <= worldBottom + worldStep * 0.5f; worldY += worldStep)
        {
            float layoutY = ViewportToLayout(viewport.WorldToLayout(new Vector2(0.0f, worldY))).Y;
            Color color = IsMajorGridLine(worldY, baseWorldStep, stepMultiplier) ? majorColor : minorColor;
            DA.DT.StrokeLineSegment(origin, new Vector2(layoutBounds.Left, layoutY), new Vector2(layoutBounds.Right, layoutY), color, 1.0f);
        }
    }

    private bool IsMajorGridLine(float worldCoordinate, float baseWorldStep, int stepMultiplier)
    {
        int majorFrequency = Math.Max(1, GraphView.MajorGridLineFrequency);
        int gridIndex = (int)MathF.Round(worldCoordinate / baseWorldStep);
        return gridIndex % Math.Max(majorFrequency, stepMultiplier) == 0;
    }

    private void DrawEdges(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        GraphDocument document = GraphView.Document;
        if (document == null)
        {
            return;
        }

        Color edgeColor = ResolveBrushColor(GraphView.EdgeBrush, new Color(128, 180, 255)) * DA.Opacity;
        if (edgeColor == Color.Transparent)
        {
            return;
        }

        Color selectedEdgeColor = GraphView.GetTheme()?.Graph?.SelectedEdgeBrush is IFillBrush selectedEdgeBrush
            ? ResolveBrushColor(selectedEdgeBrush, Color.Yellow) * DA.Opacity
            : Color.Lerp(edgeColor, Color.White, 0.35f);

        float thickness = Math.Max(0.1f, GraphView.EdgeThickness);
        int segmentCount = GraphView.ViewportTransform.Zoom < 0.35f ? 8 : GraphBezierGeometry.DefaultSegmentCount;
        Vector2 origin = DA.Offset.ToVector2();
        RectangleF worldViewport = GraphView.GetCullingWorldViewport(GetViewportLocalBounds(layoutBounds));
        int edgesVisible = 0;
        int edgesCulled = 0;

        for (int edgeIndex = 0; edgeIndex < document.Edges.Count; edgeIndex++)
        {
            GraphEdgeModel edge = document.Edges[edgeIndex];
            if (edge == null)
            {
                continue;
            }

            if (!TryGetPortAnchor(edge.SourcePortId, out Vector2 start) || !TryGetPortAnchor(edge.TargetPortId, out Vector2 end))
            {
                continue;
            }

            if (GraphView.EnableViewportCulling)
            {
                Vector2 startWorld = GraphView.ViewportTransform.LayoutToWorld(LayoutToViewport(start));
                Vector2 endWorld = GraphView.ViewportTransform.LayoutToWorld(LayoutToViewport(end));
                if (!GraphView.CullingService.ShouldDrawEdge(edge, startWorld, endWorld, worldViewport, GraphView.SelectedEdgeIds))
                {
                    edgesCulled++;
                    continue;
                }
            }

            edgesVisible++;
            bool isSelected = GraphView.SelectedEdgeIds.Contains(edge.Id);
            float edgeThickness = isSelected ? thickness + 1.0f : thickness;
            Color currentEdgeColor = isSelected ? selectedEdgeColor : edgeColor;
            IReadOnlyList<Vector2> points = GraphView.EdgeGeometryCache.GetOrCreate(edge.Id, start, end, edgeThickness, GraphView.ViewportTransform.Zoom, segmentCount);
            for (int pointIndex = 1; pointIndex < points.Count; pointIndex++)
            {
                DA.DT.StrokeLineSegment(origin, points[pointIndex - 1], points[pointIndex], currentEdgeColor, edgeThickness);
            }
        }

        GraphView.SetEdgeCullingDiagnostics(edgesVisible, edgesCulled);

        DrawTemporaryConnection(DA, origin, edgeColor, thickness);
    }

    private void DrawTemporaryConnection(ElementDrawArgs DA, Vector2 origin, Color defaultEdgeColor, float thickness)
    {
        GraphConnectionController controller = GraphView.ConnectionController;
        if (controller?.IsDragging != true)
        {
            return;
        }

        Color previewColor = controller.HoverPortId.HasValue && controller.PreviewValidationResult?.IsValid == false
            ? Color.OrangeRed * DA.Opacity
            : defaultEdgeColor;
        GraphBezierGeometry.BuildDefaultEdge(controller.StartViewportPoint, controller.CurrentViewportPoint, TemporaryEdgePoints, 16);
        for (int pointIndex = 1; pointIndex < TemporaryEdgePoints.Count; pointIndex++)
        {
            DA.DT.StrokeLineSegment(origin, TemporaryEdgePoints[pointIndex - 1], TemporaryEdgePoints[pointIndex], previewColor, thickness);
        }
    }

    private bool TryGetPortAnchor(Guid portId, out Vector2 layoutAnchor)
    {
        if (GraphView.TryGetPortControl(portId, out MGGraphPort port) && IsRuntimePortAnchorUsable(port))
        {
            layoutAnchor = port.GetLayoutAnchor();
            return true;
        }

        return TryGetFallbackPortAnchor(portId, out layoutAnchor);
    }

    private bool TryGetFallbackPortAnchor(Guid portId, out Vector2 layoutAnchor)
    {
        if (GraphPortAnchorResolver.TryGetPortLayoutAnchor(GraphView.Document, GraphView.ViewportTransform, portId, out Vector2 viewportAnchor))
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
        Vector2 viewportTopLeft = LayoutToViewport(new Vector2(layoutBounds.Left, layoutBounds.Top));
        return new Rectangle((int)MathF.Round(viewportTopLeft.X), (int)MathF.Round(viewportTopLeft.Y), layoutBounds.Width, layoutBounds.Height);
    }

    private Vector2 GetViewportLayoutOrigin()
    {
        Rectangle bounds = GraphView.NodesCanvas?.AlignedContentBounds ?? GraphView.NodesCanvas?.LayoutBounds ?? Rectangle.Empty;
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

        return GraphView.TryGetNodeControl(port.Model.NodeId, out MGGraphNode node) && node.Visibility == Visibility.Visible && HasVisibleActualBounds(node);
    }

    private static bool HasVisibleActualBounds(MGElement element)
        => element != null && element.ActualLayoutBounds.Width > 0 && element.ActualLayoutBounds.Height > 0;

    private static Color ResolveBrushColor(IFillBrush brush, Color fallback)
        => brush is MGSolidFillBrush solid ? solid.Color : fallback;
}