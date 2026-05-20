using System;
using System.Collections.Generic;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Graph;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI
{
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
            Color minorColor = ResolveBrushColor(GraphView.GridLineBrush, Color.White * 0.08f) * DA.Opacity;
            if (minorColor == Color.Transparent)
            {
                return;
            }

            Color majorColor = Color.Lerp(minorColor, Color.White * DA.Opacity, 0.22f);
            Vector2 topLeftWorld = viewport.LayoutToWorld(new Vector2(layoutBounds.Left, layoutBounds.Top));
            Vector2 bottomRightWorld = viewport.LayoutToWorld(new Vector2(layoutBounds.Right, layoutBounds.Bottom));
            float worldLeft = Math.Min(topLeftWorld.X, bottomRightWorld.X);
            float worldRight = Math.Max(topLeftWorld.X, bottomRightWorld.X);
            float worldTop = Math.Min(topLeftWorld.Y, bottomRightWorld.Y);
            float worldBottom = Math.Max(topLeftWorld.Y, bottomRightWorld.Y);
            float firstWorldX = MathF.Floor(worldLeft / worldStep) * worldStep;
            float firstWorldY = MathF.Floor(worldTop / worldStep) * worldStep;
            Vector2 origin = DA.Offset.ToVector2();

            for (float worldX = firstWorldX; worldX <= worldRight + worldStep * 0.5f; worldX += worldStep)
            {
                float layoutX = viewport.WorldToLayout(new Vector2(worldX, 0.0f)).X;
                Color color = IsMajorGridLine(worldX, baseWorldStep, stepMultiplier) ? majorColor : minorColor;
                DA.DT.StrokeLineSegment(origin, new Vector2(layoutX, layoutBounds.Top), new Vector2(layoutX, layoutBounds.Bottom), color, 1.0f);
            }

            for (float worldY = firstWorldY; worldY <= worldBottom + worldStep * 0.5f; worldY += worldStep)
            {
                float layoutY = viewport.WorldToLayout(new Vector2(0.0f, worldY)).Y;
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

            float thickness = Math.Max(0.1f, GraphView.EdgeThickness);
            int segmentCount = GraphView.ViewportTransform.Zoom < 0.35f ? 8 : GraphBezierGeometry.DefaultSegmentCount;
            Vector2 origin = DA.Offset.ToVector2();

            for (int edgeIndex = 0; edgeIndex < document.Edges.Count; edgeIndex++)
            {
                GraphEdgeModel edge = document.Edges[edgeIndex];
                if (edge == null || !TryGetPortAnchor(edge.SourcePortId, out Vector2 start) || !TryGetPortAnchor(edge.TargetPortId, out Vector2 end))
                {
                    continue;
                }

                IReadOnlyList<Vector2> points = GraphView.EdgeGeometryCache.GetOrCreate(edge.Id, start, end, thickness, GraphView.ViewportTransform.Zoom, segmentCount);
                for (int pointIndex = 1; pointIndex < points.Count; pointIndex++)
                {
                    DA.DT.StrokeLineSegment(origin, points[pointIndex - 1], points[pointIndex], edgeColor, thickness);
                }
            }

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
            if (GraphView.TryGetPortControl(portId, out MGGraphPort port) && HasUsableBounds(port))
            {
                layoutAnchor = port.GetLayoutAnchor();
                return true;
            }

            GraphPortModel portModel = GraphView.Document.TryGetPort(portId);
            GraphNodeModel nodeModel = portModel == null ? null : GraphView.Document.TryGetNode(portModel.NodeId);
            if (portModel == null || nodeModel == null)
            {
                layoutAnchor = default;
                return false;
            }

            Vector2 nodeSize = nodeModel.Size ?? new Vector2(160.0f, 100.0f);
            int portIndex = Math.Max(0, nodeModel.Ports.FindIndex(candidate => candidate?.Id == portId));
            float portY = Math.Min(Math.Max(20.0f, 36.0f + portIndex * 24.0f), Math.Max(20.0f, nodeSize.Y - 12.0f));
            float portX = portModel.Direction == GraphPortDirection.Input ? 0.0f : nodeSize.X;
            layoutAnchor = GraphView.ViewportTransform.WorldToLayout(nodeModel.Position + new Vector2(portX, portY));
            return true;
        }

        private static bool HasUsableBounds(MGElement element)
            => element != null && (element.ActualLayoutBounds.Width > 0 || element.ActualLayoutBounds.Height > 0 || element.LayoutBounds.Width > 0 || element.LayoutBounds.Height > 0);

        private static Color ResolveBrushColor(IFillBrush brush, Color fallback)
            => brush is MGSolidFillBrush solid ? solid.Color : fallback;
    }
}