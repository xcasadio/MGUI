using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace MGUI.Core.UI.Shapes
{
    /// <summary>Shared Sutherland-Hodgman clipping, fan-triangulation, and UV-interpolation helpers used when mapping textured paints onto
    /// box/ring geometry (see <see cref="MGBoxGeometry"/> and <c>MGTexturedBorderBrush</c>). Every list-producing method clears and writes into
    /// caller-supplied lists so the hot path (one call per ring quad per layout region) stays allocation-free.</summary>
    internal static class MGConvexPolygonClipper
    {
        /// <summary>Clips <paramref name="convexPolygon"/> against the 4 axis-aligned half-planes of <paramref name="rect"/>
        /// (Sutherland-Hodgman). The result is written to <paramref name="output"/> (cleared first); <paramref name="scratch"/> is reusable
        /// working storage.</summary>
        public static void ClipToRectangle(IReadOnlyList<Vector2> convexPolygon, Rectangle rect, List<Vector2> output, List<Vector2> scratch)
        {
            output.Clear();
            output.AddRange(convexPolygon);
            ClipAgainstBound(output, scratch, axisX: true, bound: rect.Left, keepGreater: true);
            ClipAgainstBound(output, scratch, axisX: true, bound: rect.Right, keepGreater: false);
            ClipAgainstBound(output, scratch, axisX: false, bound: rect.Top, keepGreater: true);
            ClipAgainstBound(output, scratch, axisX: false, bound: rect.Bottom, keepGreater: false);
        }

        /// <summary>Clips <paramref name="subject"/> against the convex polygon <paramref name="convexClip"/>, one half-plane per clip edge
        /// (Sutherland-Hodgman). The clip polygon's winding is determined once, so either winding (clockwise or counter-clockwise) works. The
        /// result is written to <paramref name="output"/> (cleared first); <paramref name="scratch"/> is reusable working storage.</summary>
        public static void ClipToConvexPolygon(IReadOnlyList<Vector2> subject, IReadOnlyList<Vector2> convexClip, List<Vector2> output, List<Vector2> scratch)
        {
            output.Clear();

            if (convexClip.Count < 3)
            {
                return;
            }

            output.AddRange(subject);

            //  Clip polygon winding decides which side of each directed edge counts as "inside".
            bool clockwise = SignedArea(convexClip) < 0;

            for (int i = 0; i < convexClip.Count && output.Count > 0; i++)
            {
                Vector2 edgeStart = convexClip[i];
                Vector2 edgeEnd = convexClip[(i + 1) % convexClip.Count];
                ClipAgainstEdge(output, scratch, edgeStart, edgeEnd, clockwise);
            }
        }

        /// <summary>Appends a triangle fan over <paramref name="vertexCount"/> consecutive vertex-buffer indices starting at
        /// <paramref name="firstVertexIndex"/> (that first index is the fan's pivot, shared by every triangle). No-op below 3 vertices.</summary>
        public static void AppendFanTriangles(int firstVertexIndex, int vertexCount, List<int> indices)
        {
            for (int k = 1; k + 1 < vertexCount; k++)
            {
                indices.Add(firstVertexIndex);
                indices.Add(firstVertexIndex + k);
                indices.Add(firstVertexIndex + k + 1);
            }
        }

        /// <summary>Axis-aligned rect-to-rect interpolation: maps <paramref name="point"/> (clamped to <paramref name="sourceRect"/> first) from
        /// <paramref name="sourceRect"/> onto the UV range [<paramref name="uvTopLeft"/>, <paramref name="uvBottomRight"/>] - the destination
        /// top-left maps to <paramref name="uvTopLeft"/>, the destination bottom-right to <paramref name="uvBottomRight"/>.</summary>
        public static Vector2 InterpolateRectUV(Vector2 point, Rectangle sourceRect, Vector2 uvTopLeft, Vector2 uvBottomRight)
        {
            float clampedX = MathHelper.Clamp(point.X, sourceRect.Left, sourceRect.Right);
            float clampedY = MathHelper.Clamp(point.Y, sourceRect.Top, sourceRect.Bottom);
            float tx = sourceRect.Width > 0 ? (clampedX - sourceRect.Left) / sourceRect.Width : 0f;
            float ty = sourceRect.Height > 0 ? (clampedY - sourceRect.Top) / sourceRect.Height : 0f;
            return new Vector2(
                uvTopLeft.X + tx * (uvBottomRight.X - uvTopLeft.X),
                uvTopLeft.Y + ty * (uvBottomRight.Y - uvTopLeft.Y));
        }

        private static void ClipAgainstBound(List<Vector2> polygon, List<Vector2> scratch, bool axisX, float bound, bool keepGreater)
        {
            if (polygon.Count == 0)
            {
                return;
            }

            scratch.Clear();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                float currentValue = axisX ? current.X : current.Y;
                float previousValue = axisX ? previous.X : previous.Y;
                bool currentInside = keepGreater ? currentValue >= bound : currentValue <= bound;
                bool previousInside = keepGreater ? previousValue >= bound : previousValue <= bound;

                if (currentInside)
                {
                    if (!previousInside)
                    {
                        scratch.Add(IntersectBound(previous, current, previousValue, currentValue, bound));
                    }

                    scratch.Add(current);
                }
                else if (previousInside)
                {
                    scratch.Add(IntersectBound(previous, current, previousValue, currentValue, bound));
                }
            }

            polygon.Clear();
            polygon.AddRange(scratch);
        }

        private static void ClipAgainstEdge(List<Vector2> polygon, List<Vector2> scratch, Vector2 edgeStart, Vector2 edgeEnd, bool clockwise)
        {
            if (polygon.Count == 0)
            {
                return;
            }

            Vector2 edge = edgeEnd - edgeStart;
            scratch.Clear();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 current = polygon[i];
                Vector2 previous = polygon[(i + polygon.Count - 1) % polygon.Count];
                float currentSide = Cross(edge, current - edgeStart);
                float previousSide = Cross(edge, previous - edgeStart);
                //  Clockwise clip polygon: inside is the non-positive side of each directed edge; counter-clockwise: the non-negative side.
                bool currentInside = clockwise ? currentSide <= 0 : currentSide >= 0;
                bool previousInside = clockwise ? previousSide <= 0 : previousSide >= 0;

                if (currentInside)
                {
                    if (!previousInside)
                    {
                        scratch.Add(IntersectEdge(previous, current, previousSide, currentSide));
                    }

                    scratch.Add(current);
                }
                else if (previousInside)
                {
                    scratch.Add(IntersectEdge(previous, current, previousSide, currentSide));
                }
            }

            polygon.Clear();
            polygon.AddRange(scratch);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

        private static Vector2 IntersectBound(Vector2 from, Vector2 to, float fromValue, float toValue, float bound)
        {
            //  Endpoints lie on different sides of the bound, so the denominator is never zero.
            float t = (bound - fromValue) / (toValue - fromValue);
            return from + (to - from) * t;
        }

        private static Vector2 IntersectEdge(Vector2 from, Vector2 to, float fromSide, float toSide)
        {
            //  Endpoints lie on different sides of the edge line, so the denominator is never zero.
            float t = fromSide / (fromSide - toSide);
            return from + (to - from) * t;
        }

        private static float SignedArea(IReadOnlyList<Vector2> polygon)
        {
            float area = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Count];
                area += a.X * b.Y - b.X * a.Y;
            }

            return area / 2f;
        }
    }
}
