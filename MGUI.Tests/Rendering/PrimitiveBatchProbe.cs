using System;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.Triangulation;
using MonoGame.Extended.VectorDraw;
using Xunit;

namespace MGUI.Tests.Rendering;

/// <summary>Reads the vertices that a <see cref="PrimitiveBatch"/> of MonoGame.Extended 6.1.1 has buffered, by reflection on its private fields
/// <c>_triangleVertices</c> and <c>_triangleVertsCount</c> (T1.2, step 3): the one mechanism that proves what <c>FillPolygon</c> hands to the batch is what
/// <c>PrimitiveDrawing.DrawSolidPolygon</c> hands to it. Compiled in <c>MGUI.Tests</c> and, as a link, in <c>CasaEngine.Tests</c>.<para/>
/// Each comparison starts the buffer from a given count (the buffer is the batch's own: setting the count to <c>offset</c> gives a batch that holds
/// <c>offset</c> vertices, and the vertices before the count are never read again until the batch draws them), runs the old call, copies what it wrote, sets
/// the count back, runs the new call and copies what it wrote. The counts after each call must be the count before plus three per triangle: no flush
/// happened during a call, so the written range is the whole output of the call.</summary>
internal static class PrimitiveBatchProbe
{
    private static readonly FieldInfo VerticesField = typeof(PrimitiveBatch).GetField("_triangleVertices", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo CountField = typeof(PrimitiveBatch).GetField("_triangleVertsCount", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Open point O3 of the plan: if the fields are missing or of another type, the proof of the vertices cannot be made and T1.2 stops.</summary>
    public static void AssertReadable(PrimitiveBatch batch)
    {
        Assert.True(VerticesField != null && CountField != null, "O3: PrimitiveBatch has no field _triangleVertices / _triangleVertsCount.");
        Assert.True(VerticesField.FieldType == typeof(VertexPositionColor[]), $"O3: _triangleVertices is a {VerticesField.FieldType}.");
        Assert.True(CountField.FieldType == typeof(int), $"O3: _triangleVertsCount is a {CountField.FieldType}.");
        Assert.NotNull(VerticesField.GetValue(batch));
    }

    public static int GetCount(PrimitiveBatch batch) => (int)CountField.GetValue(batch);

    public static void SetCount(PrimitiveBatch batch, int count) => CountField.SetValue(batch, count);

    /// <summary>The usable size of the buffer: a call that would need more than the room left flushes first.</summary>
    public static int Capacity(PrimitiveBatch batch) => ((VertexPositionColor[])VerticesField.GetValue(batch)).Length;

    private static VertexPositionColor[] Copy(PrimitiveBatch batch, int from, int to)
    {
        VertexPositionColor[] buffer = (VertexPositionColor[])VerticesField.GetValue(batch);
        VertexPositionColor[] copy = new VertexPositionColor[to - from];
        Array.Copy(buffer, from, copy, 0, copy.Length);
        return copy;
    }

    /// <summary>The number of triangles that <c>DrawSolidPolygon</c> sends for this polygon: the one of the original <c>Triangulator</c>.</summary>
    public static int ReferenceTriangleCount(Vector2[] polygon)
    {
        Triangulator.Triangulate((Vector2[])polygon.Clone(), WindingOrder.CounterClockwise, out _, out int[] indices);
        return indices.Length / 3;
    }

    /// <summary>Runs <paramref name="oldCall"/> then <paramref name="newCall"/> from the same count <paramref name="offset"/>; returns null when both wrote, between
    /// the count before and the count after, the same vertices (position with Z, colour, order) and three per triangle, otherwise the first difference.</summary>
    public static string Compare(PrimitiveBatch batch, int offset, int expectedTriangles, Action oldCall, Action newCall)
    {
        int expectedEnd = offset + 3 * expectedTriangles;
        if (expectedEnd > Capacity(batch))
        {
            return $"the polygon needs {expectedEnd} of {Capacity(batch)} vertices: a flush would happen during the call";
        }

        SetCount(batch, offset);
        oldCall();
        int oldEnd = GetCount(batch);
        VertexPositionColor[] expected = Copy(batch, offset, oldEnd);

        SetCount(batch, offset);
        newCall();
        int newEnd = GetCount(batch);
        VertexPositionColor[] actual = Copy(batch, offset, newEnd);

        SetCount(batch, 0);

        if (oldEnd != expectedEnd)
        {
            return $"DrawSolidPolygon left the count at {oldEnd}, expected {expectedEnd} (offset {offset}, {expectedTriangles} triangles)";
        }

        if (newEnd != expectedEnd)
        {
            return $"the new path left the count at {newEnd}, expected {expectedEnd} (offset {offset}, {expectedTriangles} triangles)";
        }

        for (int i = 0; i < expected.Length; i++)
        {
            VertexPositionColor e = expected[i];
            VertexPositionColor a = actual[i];
            if (BitConverter.SingleToInt32Bits(e.Position.X) != BitConverter.SingleToInt32Bits(a.Position.X)
                || BitConverter.SingleToInt32Bits(e.Position.Y) != BitConverter.SingleToInt32Bits(a.Position.Y)
                || BitConverter.SingleToInt32Bits(e.Position.Z) != BitConverter.SingleToInt32Bits(a.Position.Z)
                || e.Color != a.Color)
            {
                return $"vertex {i} (triangle {i / 3}): DrawSolidPolygon wrote {e.Position} {e.Color}, the new path wrote {a.Position} {a.Color}";
            }
        }

        return null;
    }
}
