using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace MGUI.Core.UI.Shapes
{
    public static class MGBoxGeometryBuilder
    {
        private readonly record struct CacheKey(MGBoxShape Shape, int CornerSegmentCount, float Scale);

        private static readonly ConcurrentDictionary<CacheKey, MGBoxGeometry> Cache = new();

        public static int CachedGeometryCount => Cache.Count;

        public static void ClearCache() => Cache.Clear();

        public static MGBoxGeometry Build(MGBoxShape shape, int cornerSegmentCount = 8, float scale = 1.0f)
        {
            MGBoxShape normalized = shape.Normalize();
            int actualCornerSegmentCount = Math.Max(1, cornerSegmentCount);
            CacheKey key = new(normalized, actualCornerSegmentCount, scale);

            if (Cache.TryGetValue(key, out MGBoxGeometry cachedGeometry))
            {
                return cachedGeometry;
            }

            MGBoxGeometry geometry = BuildUncached(normalized, actualCornerSegmentCount);
            Cache[key] = geometry;
            return geometry;
        }

        private static MGBoxGeometry BuildUncached(MGBoxShape normalized, int actualCornerSegmentCount)
        {
            bool usesRectangleFastPath = !normalized.HasRoundedCorners;

            Vector2[] outerContour = BuildContour(normalized.OuterBounds, normalized.NormalizedCornerRadius, actualCornerSegmentCount);
            Vector2[] innerContour = normalized.HasBorder
                ? BuildContour(normalized.InnerBounds, normalized.InnerCornerRadius, actualCornerSegmentCount)
                : Array.Empty<Vector2>();

            Vector2[] vertices = CombineVertices(outerContour, innerContour);
            int[] fillIndices = BuildFillIndices(outerContour.Length);
            int[] borderRingIndices = BuildBorderRingIndices(outerContour.Length, innerContour.Length);

            return new MGBoxGeometry(
                normalized,
                outerContour,
                innerContour,
                vertices,
                fillIndices,
                borderRingIndices,
                actualCornerSegmentCount,
                usesRectangleFastPath);
        }

        private static Vector2[] CombineVertices(Vector2[] outerContour, Vector2[] innerContour)
        {
            Vector2[] result = new Vector2[outerContour.Length + innerContour.Length];
            Array.Copy(outerContour, 0, result, 0, outerContour.Length);
            Array.Copy(innerContour, 0, result, outerContour.Length, innerContour.Length);
            return result;
        }

        private static int[] BuildFillIndices(int outerContourCount)
        {
            if (outerContourCount < 3)
            {
                return Array.Empty<int>();
            }

            List<int> indices = new((outerContourCount - 2) * 3);
            for (int i = 1; i < outerContourCount - 1; i++)
            {
                indices.Add(0);
                indices.Add(i);
                indices.Add(i + 1);
            }

            return indices.ToArray();
        }

        private static int[] BuildBorderRingIndices(int outerContourCount, int innerContourCount)
        {
            if (outerContourCount < 2 || innerContourCount != outerContourCount)
            {
                return Array.Empty<int>();
            }

            List<int> indices = new(outerContourCount * 6);
            int innerOffset = outerContourCount;
            for (int i = 0; i < outerContourCount; i++)
            {
                int next = (i + 1) % outerContourCount;
                int outerCurrent = i;
                int outerNext = next;
                int innerCurrent = innerOffset + i;
                int innerNext = innerOffset + next;

                indices.Add(outerCurrent);
                indices.Add(outerNext);
                indices.Add(innerNext);

                indices.Add(innerNext);
                indices.Add(innerCurrent);
                indices.Add(outerCurrent);
            }

            return indices.ToArray();
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