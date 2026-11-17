using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using Microsoft.Xna.Framework;
using MonoGame.Extended.Triangulation;
using MonoGame.Extended;

namespace MGUI.Core.UI.Shapes;

internal readonly record struct MGPointShapePlacement(Rectangle Bounds, Vector2 GeometryOrigin);

internal readonly record struct MGPathLiteFigure(IReadOnlyList<Vector2> Points, bool IsClosed);

/// <summary>The triangulated geometry of the clip of one use of a shape (its fill brush, or the shape itself) and the <see cref="ClipDefinition"/> that holds it, kept while what they were
/// built from is the same: the array of source vertices (by reference: a shape replaces such an array when its geometry changes, and never writes in one), the translation and the
/// bounds of the clip target. A change of any of them builds a new geometry and a new definition: a definition that was given out is never rewritten, because the clip pipeline keeps
/// a definition from its push to its pop (a stencil clip draws its geometry again on release). What it builds is what <see cref="MGVectorShapeHelper.CreateTriangulatedClipGeometry"/>
/// and <see cref="ClipDefinition.ArbitraryGeometry"/> build, with the same arguments as <c>MGElement.CreateGeometryClipDefinition</c>. An entry is for one use: two uses of a shape
/// (their translations and their names differ) have an entry each. Not for several threads.</summary>
internal sealed class MGTriangulatedClipEntry
{
    private readonly string _DebugName;
    private readonly bool _AllowRectangleFallback;
    private readonly PolygonTriangulator _Triangulator = new();
    private IReadOnlyList<Vector2> _Source;
    private Vector2 _Translation;
    private Rectangle _TargetBounds;
    private ClipDefinition _Definition;

    /// <param name="debugName">The name of the clip, built once by the owner.</param>
    public MGTriangulatedClipEntry(string debugName, bool allowRectangleFallback)
    {
        _DebugName = debugName;
        _AllowRectangleFallback = allowRectangleFallback;
    }

    /// <summary>The definition of the clip of the polygon <paramref name="source"/> moved by <paramref name="translation"/>, for the clip target <paramref name="targetBounds"/>:
    /// the one given last time while the three are the same, a new one otherwise.</summary>
    public ClipDefinition GetDefinition(IReadOnlyList<Vector2> source, Vector2 translation, Rectangle targetBounds)
    {
        ClipDefinition definition = _Definition;
        if (definition == null || !ReferenceEquals(_Source, source) || _Translation != translation || _TargetBounds != targetBounds)
        {
            definition = ClipDefinition.ArbitraryGeometry(targetBounds, CreateGeometry(source, translation), intersectWithCurrentClip: true,
                allowRectangleFallback: _AllowRectangleFallback, debugName: _DebugName);
            _Source = source;
            _Translation = translation;
            _TargetBounds = targetBounds;
            _Definition = definition;
        }

        return definition;
    }

    private ClipGeometry CreateGeometry(IReadOnlyList<Vector2> source, Vector2 translation)
    {
        if (source == null || source.Count < 3)
        {
            return new ClipGeometry(Array.Empty<Vector2>(), Array.Empty<int>());
        }

        //  The two steps of CreateTriangulatedClipGeometry, in its order: the polygon is put counter-clockwise (EnsureWindingOrder: the float sum of the winding, the reversal that keeps the first
        //  vertex), then that polygon is triangulated, and the triangulation tests the winding of its input again and reverses it again when it finds it clockwise. The two tests can disagree
        //  (a signed area about 0 sums positive for the polygon and for its reversal): a single test would triangulate another polygon. A rebuild is one-off, the copy is the one array it owns.
        Vector2[] copy = new Vector2[source.Count];
        for (int i = 0; i < copy.Length; i++)
        {
            copy[i] = source[i];
        }

        _Triangulator.Triangulate(Triangulator.EnsureWindingOrder(copy, WindingOrder.CounterClockwise));
        ReadOnlySpan<Vector2> ordered = _Triangulator.Vertices;
        Vector2[] vertices = new Vector2[ordered.Length];
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = ordered[i] + translation;
        }

        return new ClipGeometry(vertices, _Triangulator.Indices.ToArray());
    }
}

internal static class MGVectorShapeHelper
{
    public static Size GetDesiredSize(Size geometrySize, float strokeThickness)
    {
        var padding = GetStrokePadding(strokeThickness);
        var width = Math.Max(0, (int)Math.Ceiling(geometrySize.Width + padding * 2f));
        var height = Math.Max(0, (int)Math.Ceiling(geometrySize.Height + padding * 2f));
        return new Size(width, height);
    }

    public static MGPointShapePlacement CreatePlacement(Rectangle layoutBounds, HorizontalAlignment horizontalAlignment,
        VerticalAlignment verticalAlignment, Size geometrySize, float strokeThickness)
    {
        var desiredSize = GetDesiredSize(geometrySize, strokeThickness);
        var bounds = MGElement.ApplyAlignment(layoutBounds, horizontalAlignment, verticalAlignment, desiredSize);
        var padding = GetStrokePadding(strokeThickness);
        Vector2 geometryOrigin = new(bounds.Left + padding, bounds.Top + padding);
        return new(bounds, geometryOrigin);
    }

    public static Size GetBoundsSize(IReadOnlyList<Vector2> points)
    {
        var bounds = GetPointBounds(points);
        return new Size(Math.Max(0, (int)Math.Ceiling(bounds.Width)), Math.Max(0, (int)Math.Ceiling(bounds.Height)));
    }

    public static RectangleF GetPointBounds(IReadOnlyList<Vector2> points)
    {
        if (points == null || points.Count == 0)
        {
            return RectangleF.Empty;
        }

        var minX = points[0].X;
        var minY = points[0].Y;
        var maxX = points[0].X;
        var maxY = points[0].Y;

        for (var i = 1; i < points.Count; i++)
        {
            var point = points[i];
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

        var normalized = new Vector2[points.Count];
        Vector2 offset = new(rawBounds.X, rawBounds.Y);
        for (var i = 0; i < points.Count; i++)
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

        var sides = Math.Max(3, segmentCount);
        var halfStroke = GetStrokePadding(strokeThickness);
        var radiusX = Math.Max(0f, width / 2f - halfStroke);
        var radiusY = Math.Max(0f, height / 2f - halfStroke);
        Vector2 center = new(width / 2f, height / 2f);

        var vertices = new Vector2[sides];
        var angleStep = Math.PI * 2d / sides;
        var angle = -Math.PI / 2d;
        for (var i = 0; i < sides; i++, angle += angleStep)
        {
            var x = center.X + radiusX * (float)Math.Cos(angle);
            var y = center.Y + radiusY * (float)Math.Sin(angle);
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

        var outerRadiusX = width / 2f;
        var outerRadiusY = height / 2f;
        if (outerRadiusX <= 0f || outerRadiusY <= 0f)
        {
            return false;
        }

        Vector2 center = new(width / 2f, height / 2f);
        var dx = localPoint.X - center.X;
        var dy = localPoint.Y - center.Y;
        var outer = dx * dx / (outerRadiusX * outerRadiusX) + dy * dy / (outerRadiusY * outerRadiusY);

        if (hasFill && outer <= 1f)
        {
            return true;
        }

        if (!hasStroke || strokeThickness <= 0f || outer > 1f)
        {
            return false;
        }

        var innerRadiusX = Math.Max(outerRadiusX - strokeThickness, 0f);
        var innerRadiusY = Math.Max(outerRadiusY - strokeThickness, 0f);
        if (innerRadiusX <= 0f || innerRadiusY <= 0f)
        {
            return true;
        }

        var inner = dx * dx / (innerRadiusX * innerRadiusX) + dy * dy / (innerRadiusY * innerRadiusY);
        return inner >= 1f;
    }

    public static bool IsPointInPolygon(IReadOnlyList<Vector2> points, Vector2 point)
    {
        if (points == null || points.Count < 3)
        {
            return false;
        }

        var isInside = false;
        var previous = points.Count - 1;
        for (var current = 0; current < points.Count; current++)
        {
            var a = points[current];
            var b = points[previous];
            var intersects = ((a.Y > point.Y) != (b.Y > point.Y)) &&
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

        var maxDistanceSquared = GetStrokePadding(strokeThickness);
        maxDistanceSquared *= maxDistanceSquared;

        var segmentCount = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < segmentCount; i++)
        {
            var start = points[i];
            var end = points[(i + 1) % points.Count];
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

        var previousCross = 0f;
        for (var i = 0; i < points.Count; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % points.Count];
            var c = points[(i + 2) % points.Count];
            var cross = CrossProduct(b - a, c - b);
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
        var vertices = new Vector2[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            vertices[i] = points[i] + translation;
        }

        var indices = new int[(points.Count - 2) * 3];
        var index = 0;
        for (var i = 1; i < points.Count - 1; i++)
        {
            indices[index++] = 0;
            indices[index++] = i;
            indices[index++] = i + 1;
        }

        return new ClipGeometry(vertices, indices);
    }

    public static ClipGeometry CreateTriangulatedClipGeometry(IReadOnlyList<Vector2> points, Vector2 translation)
    {
        if (points == null || points.Count < 3)
        {
            return new ClipGeometry(Array.Empty<Vector2>(), Array.Empty<int>());
        }

        var sourceVertices = new Vector2[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            sourceVertices[i] = points[i];
        }

        var orderedVertices = Triangulator.EnsureWindingOrder(sourceVertices, WindingOrder.CounterClockwise);
        Triangulator.Triangulate(orderedVertices, WindingOrder.CounterClockwise, out var triangulatedVertices, out var indices);

        for (var i = 0; i < triangulatedVertices.Length; i++)
        {
            triangulatedVertices[i] += translation;
        }

        return new ClipGeometry(triangulatedVertices, indices);
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= float.Epsilon)
        {
            return Vector2.DistanceSquared(point, start);
        }

        var t = Vector2.Dot(point - start, segment) / lengthSquared;
        t = Math.Clamp(t, 0f, 1f);
        var projection = start + segment * t;
        return Vector2.DistanceSquared(point, projection);
    }

    private static float CrossProduct(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

    private static float GetStrokePadding(float strokeThickness) => Math.Max(0f, strokeThickness) / 2f;
}