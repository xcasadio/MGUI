using Microsoft.Xna.Framework;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

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

        internal static MGBoxGeometry BuildInteriorFillGeometry(MGBoxGeometry borderGeometry, float overlapPixels = 0.0f)
        {
            MGBoxShape fillShape = new MGBoxShape(borderGeometry.Shape.InnerBounds, new MonoGame.Extended.Thickness(0), borderGeometry.Shape.InnerCornerRadius).Normalize();
            if (!borderGeometry.HasInnerContour)
            {
                return Build(fillShape, borderGeometry.CornerSegmentCount);
            }

            Vector2[] contour = overlapPixels > 0
                ? ExpandContourTowardOuter(borderGeometry.OuterContour, borderGeometry.InnerContour, overlapPixels)
                : borderGeometry.InnerContour.ToArray();
            int[] fillIndices = BuildFillIndices(contour.Length);
            return new MGBoxGeometry(
                fillShape,
                contour,
                Array.Empty<Vector2>(),
                contour,
                fillIndices,
                Array.Empty<int>(),
                borderGeometry.CornerSegmentCount,
                !fillShape.HasRoundedCorners);
        }

        private static Vector2[] ExpandContourTowardOuter(IReadOnlyList<Vector2> outerContour, IReadOnlyList<Vector2> innerContour, float overlapPixels)
        {
            Vector2[] expanded = new Vector2[innerContour.Count];
            for (int i = 0; i < innerContour.Count; i++)
            {
                Vector2 inner = innerContour[i];
                Vector2 outer = outerContour[i];
                Vector2 delta = outer - inner;
                float distance = delta.Length();
                if (distance <= 0.001f)
                {
                    expanded[i] = inner;
                    continue;
                }

                float t = Math.Min(1.0f, overlapPixels / distance);
                expanded[i] = inner + delta * t;
            }

            return expanded;
        }

        private static MGBoxGeometry BuildUncached(MGBoxShape normalized, int actualCornerSegmentCount)
        {
            bool usesRectangleFastPath = !normalized.HasRoundedCorners;

            Vector2[] outerContour = BuildContour(normalized.OuterBounds, normalized.NormalizedCornerRadius, actualCornerSegmentCount, out int[] outerCornerPointCounts);
            Vector2[] innerContour = BuildInnerContour(normalized, actualCornerSegmentCount, outerCornerPointCounts);

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

        /// <summary>Builds the inner contour so <see cref="BuildBorderRingIndices"/> always sees matching outer/inner point counts, except for the
        /// residual case where the border thickness consumes the whole box (empty <see cref="MGBoxShape.InnerBounds"/>): that case still yields an
        /// empty inner contour and no border ring mesh, unchanged (Docs/drawing-architecture.md, Limites connues). When a corner's border thickness reaches
        /// (or exceeds) its radius, <see cref="MGBoxShape.InnerCornerRadius"/> collapses that corner to 0 for the inner side; instead of the arc
        /// collapsing to a single point (which used to make the inner contour shorter than the outer one and break the ring), the collapsed corner
        /// repeats its single point <paramref name="outerCornerPointCounts"/> times, matching the outer corner's point count with degenerate
        /// (zero-area) ring triangles there.</summary>
        private static Vector2[] BuildInnerContour(MGBoxShape normalized, int cornerSegmentCount, int[] outerCornerPointCounts)
        {
            if (!normalized.HasBorder)
            {
                return Array.Empty<Vector2>();
            }

            Rectangle innerBounds = normalized.InnerBounds;
            if (innerBounds.Width <= 0 || innerBounds.Height <= 0 || outerCornerPointCounts.Length < 4)
            {
                //  Residual case: the border thickness consumes the whole box. No inner contour, no border ring mesh (see Docs/drawing-architecture.md).
                return Array.Empty<Vector2>();
            }

            return BuildMatchingInnerContour(innerBounds, normalized.InnerCornerRadius, cornerSegmentCount, outerCornerPointCounts);
        }

        private static Vector2[] BuildMatchingInnerContour(Rectangle bounds, MGCornerRadius cornerRadius, int cornerSegmentCount, int[] outerCornerPointCounts)
        {
            List<Vector2> points = new();
            AppendMatchingCorner(points, bounds.Left + cornerRadius.TopLeft, bounds.Top + cornerRadius.TopLeft, cornerRadius.TopLeft,
                (float)Math.PI, 1.5f * (float)Math.PI, outerCornerPointCounts[0]);
            AppendMatchingCorner(points, bounds.Right - cornerRadius.TopRight, bounds.Top + cornerRadius.TopRight, cornerRadius.TopRight,
                1.5f * (float)Math.PI, 2.0f * (float)Math.PI, outerCornerPointCounts[1]);
            AppendMatchingCorner(points, bounds.Right - cornerRadius.BottomRight, bounds.Bottom - cornerRadius.BottomRight, cornerRadius.BottomRight,
                0.0f, 0.5f * (float)Math.PI, outerCornerPointCounts[2]);
            AppendMatchingCorner(points, bounds.Left + cornerRadius.BottomLeft, bounds.Bottom - cornerRadius.BottomLeft, cornerRadius.BottomLeft,
                0.5f * (float)Math.PI, (float)Math.PI, outerCornerPointCounts[3]);
            return points.ToArray();
        }

        /// <summary>Emits exactly <paramref name="pointCount"/> points for one inner corner (the same count the matching outer corner emitted), with
        /// no coincident-point suppression: repeated points are accepted here on purpose, so the inner contour never falls short of the outer one.
        /// A zero (or negative) radius repeats the single collapsed corner point; otherwise the points are sampled evenly across the same angle
        /// range the outer corner used.</summary>
        private static void AppendMatchingCorner(List<Vector2> points, float centerX, float centerY, int radius, float startAngle, float endAngle, int pointCount)
        {
            if (pointCount <= 0)
            {
                return;
            }

            if (radius <= 0)
            {
                Vector2 point = new(centerX, centerY);
                for (int i = 0; i < pointCount; i++)
                {
                    points.Add(point);
                }

                return;
            }

            for (int i = 0; i < pointCount; i++)
            {
                float progress = pointCount == 1 ? 0f : i / (float)(pointCount - 1);
                float angle = startAngle + (endAngle - startAngle) * progress;
                points.Add(new Vector2(
                    centerX + (float)Math.Cos(angle) * radius,
                    centerY + (float)Math.Sin(angle) * radius));
            }
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
            => BuildContour(bounds, cornerRadius, cornerSegmentCount, out _);

        /// <summary><paramref name="cornerPointCounts"/> receives the number of points actually emitted for each of the 4 corners (TopLeft, TopRight,
        /// BottomRight, BottomLeft, in that order; a bare rectangle reports 1 per corner; an empty-bounds contour reports an empty array), so a caller
        /// building a matching inner contour (see <see cref="BuildMatchingInnerContour"/>) can reproduce the same per-corner point counts without
        /// changing this method's own output (coincident-point suppression, the bare-rectangle case, byte-identical to before this parameter existed).</summary>
        private static Vector2[] BuildContour(Rectangle bounds, MGCornerRadius cornerRadius, int cornerSegmentCount, out int[] cornerPointCounts)
        {
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                cornerPointCounts = Array.Empty<int>();
                return Array.Empty<Vector2>();
            }

            if (cornerRadius.IsZero)
            {
                cornerPointCounts = new[] { 1, 1, 1, 1 };
                return new[]
                {
                    new Vector2(bounds.Left, bounds.Top),
                    new Vector2(bounds.Right, bounds.Top),
                    new Vector2(bounds.Right, bounds.Bottom),
                    new Vector2(bounds.Left, bounds.Bottom)
                };
            }

            List<Vector2> points = new();
            cornerPointCounts = new int[4];
            int before;

            before = points.Count;
            AppendCorner(points, bounds.Left + cornerRadius.TopLeft, bounds.Top + cornerRadius.TopLeft, cornerRadius.TopLeft, (float)Math.PI, 1.5f * (float)Math.PI, cornerSegmentCount);
            cornerPointCounts[0] = points.Count - before;

            before = points.Count;
            AppendCorner(points, bounds.Right - cornerRadius.TopRight, bounds.Top + cornerRadius.TopRight, cornerRadius.TopRight, 1.5f * (float)Math.PI, 2.0f * (float)Math.PI, cornerSegmentCount);
            cornerPointCounts[1] = points.Count - before;

            before = points.Count;
            AppendCorner(points, bounds.Right - cornerRadius.BottomRight, bounds.Bottom - cornerRadius.BottomRight, cornerRadius.BottomRight, 0.0f, 0.5f * (float)Math.PI, cornerSegmentCount);
            cornerPointCounts[2] = points.Count - before;

            before = points.Count;
            AppendCorner(points, bounds.Left + cornerRadius.BottomLeft, bounds.Bottom - cornerRadius.BottomLeft, cornerRadius.BottomLeft, 0.5f * (float)Math.PI, (float)Math.PI, cornerSegmentCount);
            cornerPointCounts[3] = points.Count - before;

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