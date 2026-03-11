using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.Shapes
{
    public static class MGBoxGeometryBuilder
    {
        public static MGBoxGeometry Build(MGBoxShape shape, int cornerSegmentCount = 8)
        {
            MGBoxShape normalized = shape.Normalize();
            int actualCornerSegmentCount = Math.Max(1, cornerSegmentCount);
            bool usesRectangleFastPath = !normalized.HasRoundedCorners;

            Vector2[] outerContour = BuildContour(normalized.OuterBounds, normalized.NormalizedCornerRadius, actualCornerSegmentCount);
            Vector2[] innerContour = normalized.HasBorder
                ? BuildContour(normalized.InnerBounds, normalized.InnerCornerRadius, actualCornerSegmentCount)
                : Array.Empty<Vector2>();

            return new MGBoxGeometry(normalized, outerContour, innerContour, actualCornerSegmentCount, usesRectangleFastPath);
        }

        private static Vector2[] BuildContour(Rectangle bounds, MGCornerRadius cornerRadius, int cornerSegmentCount)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return Array.Empty<Vector2>();
            }

            if (cornerRadius.IsZero)
            {
                return new[]
                {
                    new Vector2(bounds.Left, bounds.Top),
                    new Vector2(bounds.Right, bounds.Top),
                    new Vector2(bounds.Right, bounds.Bottom),
                    new Vector2(bounds.Left, bounds.Bottom)
                };
            }

            List<Vector2> points = new();
            AppendCorner(points, bounds.Left + cornerRadius.TopLeft, bounds.Top + cornerRadius.TopLeft, cornerRadius.TopLeft, (float)Math.PI, 1.5f * (float)Math.PI, cornerSegmentCount);
            AppendCorner(points, bounds.Right - cornerRadius.TopRight, bounds.Top + cornerRadius.TopRight, cornerRadius.TopRight, 1.5f * (float)Math.PI, 2.0f * (float)Math.PI, cornerSegmentCount);
            AppendCorner(points, bounds.Right - cornerRadius.BottomRight, bounds.Bottom - cornerRadius.BottomRight, cornerRadius.BottomRight, 0.0f, 0.5f * (float)Math.PI, cornerSegmentCount);
            AppendCorner(points, bounds.Left + cornerRadius.BottomLeft, bounds.Bottom - cornerRadius.BottomLeft, cornerRadius.BottomLeft, 0.5f * (float)Math.PI, (float)Math.PI, cornerSegmentCount);
            return points.ToArray();
        }

        private static void AppendCorner(List<Vector2> points, float centerX, float centerY, int radius, float startAngle, float endAngle, int cornerSegmentCount)
        {
            if (radius <= 0)
            {
                Vector2 point = new(centerX, centerY);
                if (points.Count == 0 || points[^1] != point)
                {
                    points.Add(point);
                }

                return;
            }

            for (int i = 0; i <= cornerSegmentCount; i++)
            {
                float progress = i / (float)cornerSegmentCount;
                float angle = startAngle + (endAngle - startAngle) * progress;
                Vector2 point = new(
                    centerX + (float)Math.Cos(angle) * radius,
                    centerY + (float)Math.Sin(angle) * radius);

                if (points.Count == 0 || Vector2.DistanceSquared(points[^1], point) > 0.001f)
                {
                    points.Add(point);
                }
            }
        }
    }
}