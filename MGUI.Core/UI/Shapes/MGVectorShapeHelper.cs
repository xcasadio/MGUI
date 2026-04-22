using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.Shapes
{
    internal readonly record struct MGPointShapePlacement(Rectangle Bounds, Vector2 GeometryOrigin);

    internal readonly record struct MGPathLiteFigure(IReadOnlyList<Vector2> Points, bool IsClosed);

    internal static class MGVectorShapeHelper
    {
        public static Size GetDesiredSize(Size geometrySize, float strokeThickness)
        {
            float padding = GetStrokePadding(strokeThickness);
            int width = Math.Max(0, (int)Math.Ceiling(geometrySize.Width + padding * 2f));
            int height = Math.Max(0, (int)Math.Ceiling(geometrySize.Height + padding * 2f));
            return new Size(width, height);
        }

        public static MGPointShapePlacement CreatePlacement(Rectangle layoutBounds, HorizontalAlignment horizontalAlignment,
            VerticalAlignment verticalAlignment, Size geometrySize, float strokeThickness)
        {
            Size desiredSize = GetDesiredSize(geometrySize, strokeThickness);
            Rectangle bounds = MGElement.ApplyAlignment(layoutBounds, horizontalAlignment, verticalAlignment, desiredSize);
            float padding = GetStrokePadding(strokeThickness);
            Vector2 geometryOrigin = new(bounds.Left + padding, bounds.Top + padding);
            return new(bounds, geometryOrigin);
        }

        public static Size GetBoundsSize(IReadOnlyList<Vector2> points)
        {
            RectangleF bounds = GetPointBounds(points);
            return new Size(Math.Max(0, (int)Math.Ceiling(bounds.Width)), Math.Max(0, (int)Math.Ceiling(bounds.Height)));
        }

        public static RectangleF GetPointBounds(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count == 0)
            {
                return RectangleF.Empty;
            }

            float minX = points[0].X;
            float minY = points[0].Y;
            float maxX = points[0].X;
            float maxY = points[0].Y;

            for (int i = 1; i < points.Count; i++)
            {
                Vector2 point = points[i];
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }

            return new RectangleF(minX, minY, maxX - minX, maxY - minY);
        }

        public static Vector2[] NormalizePoints(IReadOnlyList<Vector2> points, out RectangleF rawBounds)
        {
            rawBounds = GetPointBounds(points);
            if (points == null || points.Count == 0)
            {
                return Array.Empty<Vector2>();
            }

            Vector2[] normalized = new Vector2[points.Count];
            Vector2 offset = new(rawBounds.X, rawBounds.Y);
            for (int i = 0; i < points.Count; i++)
            {
                normalized[i] = points[i] - offset;
            }

            return normalized;
        }

        public static Vector2[] CreateEllipseVertices(int width, int height, float strokeThickness, int segmentCount)
        {
            if (width <= 0 || height <= 0)
            {
                return Array.Empty<Vector2>();
            }

            int sides = Math.Max(3, segmentCount);
            float halfStroke = GetStrokePadding(strokeThickness);
            float radiusX = Math.Max(0f, width / 2f - halfStroke);
            float radiusY = Math.Max(0f, height / 2f - halfStroke);
            Vector2 center = new(width / 2f, height / 2f);

            Vector2[] vertices = new Vector2[sides];
            double angleStep = Math.PI * 2d / sides;
            double angle = -Math.PI / 2d;
            for (int i = 0; i < sides; i++, angle += angleStep)
            {
                float x = center.X + radiusX * (float)Math.Cos(angle);
                float y = center.Y + radiusY * (float)Math.Sin(angle);
                vertices[i] = new Vector2(x, y);
            }

            return vertices;
        }

        public static bool ContainsEllipse(int width, int height, Vector2 localPoint, bool hasFill, bool hasStroke, float strokeThickness)
        {
            if (width <= 0 || height <= 0)
            {
                return false;
            }

            float outerRadiusX = width / 2f;
            float outerRadiusY = height / 2f;
            if (outerRadiusX <= 0f || outerRadiusY <= 0f)
            {
                return false;
            }

            Vector2 center = new(width / 2f, height / 2f);
            float dx = localPoint.X - center.X;
            float dy = localPoint.Y - center.Y;
            float outer = dx * dx / (outerRadiusX * outerRadiusX) + dy * dy / (outerRadiusY * outerRadiusY);

            if (hasFill && outer <= 1f)
            {
                return true;
            }

            if (!hasStroke || strokeThickness <= 0f || outer > 1f)
            {
                return false;
            }

            float innerRadiusX = Math.Max(outerRadiusX - strokeThickness, 0f);
            float innerRadiusY = Math.Max(outerRadiusY - strokeThickness, 0f);
            if (innerRadiusX <= 0f || innerRadiusY <= 0f)
            {
                return true;
            }

            float inner = dx * dx / (innerRadiusX * innerRadiusX) + dy * dy / (innerRadiusY * innerRadiusY);
            return inner >= 1f;
        }

        public static bool IsPointInPolygon(IReadOnlyList<Vector2> points, Vector2 point)
        {
            if (points == null || points.Count < 3)
            {
                return false;
            }

            bool isInside = false;
            int previous = points.Count - 1;
            for (int current = 0; current < points.Count; current++)
            {
                Vector2 a = points[current];
                Vector2 b = points[previous];
                bool intersects = ((a.Y > point.Y) != (b.Y > point.Y)) &&
                    (point.X < (b.X - a.X) * (point.Y - a.Y) / (b.Y - a.Y) + a.X);
                if (intersects)
                {
                    isInside = !isInside;
                }

                previous = current;
            }

            return isInside;
        }

        public static bool IsPointNearPolyline(IReadOnlyList<Vector2> points, Vector2 point, float strokeThickness, bool closed)
        {
            if (points == null || points.Count < 2 || strokeThickness <= 0f)
            {
                return false;
            }

            float maxDistanceSquared = GetStrokePadding(strokeThickness);
            maxDistanceSquared *= maxDistanceSquared;

            int segmentCount = closed ? points.Count : points.Count - 1;
            for (int i = 0; i < segmentCount; i++)
            {
                Vector2 start = points[i];
                Vector2 end = points[(i + 1) % points.Count];
                if (DistanceSquaredToSegment(point, start, end) <= maxDistanceSquared)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsConvexPolygon(IReadOnlyList<Vector2> points)
        {
            if (points == null || points.Count < 3)
            {
                return false;
            }

            float previousCross = 0f;
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 a = points[i];
                Vector2 b = points[(i + 1) % points.Count];
                Vector2 c = points[(i + 2) % points.Count];
                float cross = CrossProduct(b - a, c - b);
                if (Math.Abs(cross) <= float.Epsilon)
                {
                    continue;
                }

                if (previousCross != 0f && Math.Sign(cross) != Math.Sign(previousCross))
                {
                    return false;
                }

                previousCross = cross;
            }

            return previousCross != 0f;
        }

        public static ClipGeometry CreateTriangleFanClipGeometry(IReadOnlyList<Vector2> points, Vector2 translation)
        {
            Vector2[] vertices = new Vector2[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                vertices[i] = points[i] + translation;
            }

            int[] indices = new int[(points.Count - 2) * 3];
            int index = 0;
            for (int i = 1; i < points.Count - 1; i++)
            {
                indices[index++] = 0;
                indices[index++] = i;
                indices[index++] = i + 1;
            }

            return new ClipGeometry(vertices, indices);
        }

        private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.LengthSquared();
            if (lengthSquared <= float.Epsilon)
            {
                return Vector2.DistanceSquared(point, start);
            }

            float t = Vector2.Dot(point - start, segment) / lengthSquared;
            t = Math.Clamp(t, 0f, 1f);
            Vector2 projection = start + segment * t;
            return Vector2.DistanceSquared(point, projection);
        }

        private static float CrossProduct(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        private static float GetStrokePadding(float strokeThickness) => Math.Max(0f, strokeThickness) / 2f;
    }
}