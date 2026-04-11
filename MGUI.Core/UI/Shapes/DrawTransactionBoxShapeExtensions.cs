using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Shapes
{
    public static class DrawTransactionBoxShapeExtensions
    {
        public static void FillRoundedRectangle(this IUIDrawContext drawContext, Vector2 origin, MGBoxShape shape, Color color,
            int cornerSegmentCount = 8, DrawContext? preferredContext = null)
        {
            MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, cornerSegmentCount);
            FillRoundedRectangle(drawContext, origin, geometry, color, preferredContext);
        }

        public static void FillRoundedRectangle(this IUIDrawContext drawContext, Vector2 origin, MGBoxGeometry geometry, Color color,
            DrawContext? preferredContext = null)
        {
            if (geometry.UsesRectangleFastPath)
            {
                drawContext.FillRectangle(origin, geometry.Shape.OuterBounds, color, preferredContext);
                return;
            }

            DrawTriangleList(drawContext, origin, geometry.Vertices, geometry.FillIndices, color);
        }

        public static void StrokeRoundedRectangle(this IUIDrawContext drawContext, Vector2 origin, MGBoxShape shape, Color color,
            int cornerSegmentCount = 8, DrawContext? preferredContext = null)
        {
            MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, cornerSegmentCount);
            StrokeRoundedRectangle(drawContext, origin, geometry, color, preferredContext);
        }

        public static void StrokeRoundedRectangle(this IUIDrawContext drawContext, Vector2 origin, MGBoxGeometry geometry, Color color,
            DrawContext? preferredContext = null)
        {
            if (geometry.UsesRectangleFastPath)
            {
                drawContext.StrokeRectangle(origin, geometry.Shape.OuterBounds, color, geometry.Shape.NormalizedBorderThickness, preferredContext);
                return;
            }

            drawContext.DrawBorderRing(origin, geometry, color, preferredContext);
        }

        public static void DrawBorderRing(this IUIDrawContext drawContext, Vector2 origin, MGBoxShape shape, Color color,
            int cornerSegmentCount = 8, DrawContext? preferredContext = null)
        {
            MGBoxGeometry geometry = MGBoxGeometryBuilder.Build(shape, cornerSegmentCount);
            drawContext.DrawBorderRing(origin, geometry, color, preferredContext);
        }

        public static void DrawBorderRing(this IUIDrawContext drawContext, Vector2 origin, MGBoxGeometry geometry, Color color,
            DrawContext? preferredContext = null)
        {
            if (!geometry.Shape.HasBorder)
            {
                return;
            }

            if (geometry.UsesRectangleFastPath)
            {
                drawContext.StrokeRectangle(origin, geometry.Shape.OuterBounds, color, geometry.Shape.NormalizedBorderThickness, preferredContext);
                return;
            }

            DrawTriangleList(drawContext, origin, geometry.Vertices, geometry.BorderRingIndices, color);
        }

        private static void DrawTriangleList(IUIDrawContext drawContext, Vector2 origin, System.Collections.Generic.IReadOnlyList<Vector2> vertices,
            System.Collections.Generic.IReadOnlyList<int> indices, Color color)
        {
            for (int i = 0; i + 2 < indices.Count; i += 3)
            {
                Vector2 v0 = vertices[indices[i]];
                Vector2 v1 = vertices[indices[i + 1]];
                Vector2 v2 = vertices[indices[i + 2]];
                drawContext.FillTriangle(origin, v0, color, v1, color, v2, color);
            }
        }
    }
}