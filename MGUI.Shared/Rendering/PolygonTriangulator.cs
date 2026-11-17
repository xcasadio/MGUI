using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MGUI.Shared.Rendering
{
    /// <summary>Triangulates a polygon without allocating, and gives, entry for entry, what <c>Triangulator.Triangulate(vertices, WindingOrder.CounterClockwise, ...)</c>
    /// of MonoGame.Extended 6.1.1 gives: the same ordered vertices and the same indices, including for a degenerate polygon where that ear clipping stops early and
    /// fills only a part (ADR-0038).<para/>
    /// The instance keeps its buffers: they grow to the biggest polygon seen and never shrink, so a call allocates only when its polygon is bigger than every one
    /// before it. Nothing is static: two instances never share anything, and an instance is for one thread at a time.<para/>
    /// <b>Validity of the result.</b> <see cref="Vertices"/> and <see cref="Indices"/> view the buffers of the instance: they are valid until the next call to
    /// <c>Triangulate</c> on the same instance, which overwrites them (and may replace the buffers, so a span read before that call must not be read after it).
    /// Read them during the same operation, or copy them.<para/>
    /// Not supported: a vertex with a NaN coordinate (the original does not find such a vertex again in its own list).</summary>
    public sealed class PolygonTriangulator
    {
        private const byte Reflex = 0;
        private const byte Convex = 1;

        //  The result: the ordered vertices (the polygon, or the polygon reversed when it is clockwise) and the indices of the triangles in them.
        private Vector2[] _Vertices = Array.Empty<Vector2>();
        private int _VertexCount;
        private int[] _Indices = Array.Empty<int>();
        private int _IndexCount;

        //  The working lists, one entry per vertex. The ring of the vertices that are not clipped yet is _Previous/_Next (the cyclic list of the original, by index).
        //  _Kind is the membership of the convex and of the reflex list of the original; _Reflex is its reflex list (only the set of its entries matters, see ADR-0038);
        //  the ears are a doubly linked list by vertex (the original's ear list, where the first one is clipped next, a new ear goes first, a revalidated one is removed).
        private int[] _Previous = Array.Empty<int>();
        private int[] _Next = Array.Empty<int>();
        private byte[] _Kind = Array.Empty<byte>();
        private int[] _Reflex = Array.Empty<int>();
        private int _ReflexCount;
        private int[] _EarPrevious = Array.Empty<int>();
        private int[] _EarNext = Array.Empty<int>();
        private bool[] _IsEar = Array.Empty<bool>();
        private int _EarFirst;
        private int _EarLast;
        private int _EarCount;
        private int _RingAnchor;

        /// <summary>How many triangles the last call gave: <c>Indices.Length / 3</c>.</summary>
        public int TriangleCount => _IndexCount / 3;

        /// <summary>The vertices of the last polygon in the order the indices refer to: the polygon itself when it is counter-clockwise, otherwise the polygon with its
        /// first vertex kept and the others reversed (<c>Triangulator.ReverseWindingOrder</c>). Valid until the next call to <c>Triangulate</c>.</summary>
        public ReadOnlySpan<Vector2> Vertices => new ReadOnlySpan<Vector2>(_Vertices, 0, _VertexCount);

        /// <summary>The triangles of the last polygon, three indices into <see cref="Vertices"/> for each, in the order of <c>Triangulator.Triangulate</c> with a
        /// counter-clockwise output. Valid until the next call to <c>Triangulate</c>.</summary>
        public ReadOnlySpan<int> Indices => new ReadOnlySpan<int>(_Indices, 0, _IndexCount);

        /// <summary>Test seam (ADR-0038): when false, a convex polygon goes through the general ear clipping too.</summary>
        internal bool ConvexFastPathEnabled { get; set; } = true;

        /// <summary>Test seam: whether the last call took the convex fast path.</summary>
        internal bool UsedConvexFastPath { get; private set; }

        /// <summary>Triangulates <paramref name="vertices"/>; returns the number of triangles. Fewer than three vertices give no triangle.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="vertices"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="vertices"/> is empty.</exception>
        public int Triangulate(Vector2[] vertices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            int count = vertices.Length;
            if (count == 0)
            {
                throw new ArgumentException("A polygon needs at least one vertex.", nameof(vertices));
            }

            Reserve(count);
            Array.Copy(vertices, _Vertices, count);
            return TriangulateCopied(count);
        }

        /// <summary>Triangulates <paramref name="vertices"/>, read through its indexer (no enumerator); returns the number of triangles. Fewer than three vertices give no triangle.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="vertices"/> is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="vertices"/> is empty.</exception>
        public int Triangulate(IReadOnlyList<Vector2> vertices)
        {
            if (vertices == null)
            {
                throw new ArgumentNullException(nameof(vertices));
            }

            int count = vertices.Count;
            if (count == 0)
            {
                throw new ArgumentException("A polygon needs at least one vertex.", nameof(vertices));
            }

            Reserve(count);
            for (int i = 0; i < count; i++)
            {
                _Vertices[i] = vertices[i];
            }

            return TriangulateCopied(count);
        }

        /// <summary>Makes every buffer hold <paramref name="count"/> vertices; they only grow.</summary>
        private void Reserve(int count)
        {
            if (_Vertices.Length < count)
            {
                _Vertices = new Vector2[count];
                _Previous = new int[count];
                _Next = new int[count];
                _Kind = new byte[count];
                _Reflex = new int[count];
                _EarPrevious = new int[count];
                _EarNext = new int[count];
                _IsEar = new bool[count];
            }

            //  3 * (count - 2) is the most triangles a polygon of count vertices can give.
            int indexCapacity = Math.Max(3, (count - 2) * 3);
            if (_Indices.Length < indexCapacity)
            {
                _Indices = new int[indexCapacity];
            }
        }

        /// <summary>The polygon is in <c>_Vertices[0..count)</c>: puts it counter-clockwise, then triangulates it.</summary>
        private int TriangulateCopied(int count)
        {
            _VertexCount = count;
            _IndexCount = 0;
            UsedConvexFastPath = false;

            //  Triangulator.DetermineWindingOrder, then ReverseWindingOrder: the first vertex is kept, the others are reversed.
            if (IsClockwise(count))
            {
                Array.Reverse(_Vertices, 1, count - 1);
            }

            if (count >= 3)
            {
                ClipEars(count);
            }

            return _IndexCount / 3;
        }

        /// <summary>The same sum, in the same order and in <c>float</c>, as <c>Triangulator.DetermineWindingOrder</c>.</summary>
        private bool IsClockwise(int count)
        {
            float sum = 0f;
            Vector2 previous = _Vertices[count - 1];
            for (int i = 0; i < count; i++)
            {
                Vector2 current = _Vertices[i];
                sum += (current.X - previous.X) * (current.Y + previous.Y);
                previous = current;
            }

            return sum > 0f;
        }

        private void ClipEars(int count)
        {
            //  The ring: every vertex is in it, in the order of the polygon.
            for (int i = 0; i < count; i++)
            {
                _Previous[i] = i == 0 ? count - 1 : i - 1;
                _Next[i] = i == count - 1 ? 0 : i + 1;
            }

            //  Triangulator.FindConvexAndReflexVertices: every vertex is classified before any ear is searched.
            _ReflexCount = 0;
            for (int i = 0; i < count; i++)
            {
                if (IsConvex(i))
                {
                    _Kind[i] = Convex;
                }
                else
                {
                    _Kind[i] = Reflex;
                    _Reflex[_ReflexCount] = i;
                    _ReflexCount++;
                }
            }

            if (_ReflexCount == 0 && ConvexFastPathEnabled)
            {
                //  No reflex vertex: IsEar is true for every vertex and stays true, so the original clips the vertices 0, 1, ... in turn, each time with the last vertex as
                //  the previous one and the next vertex of the polygon as the next one: the triangle (k, k + 1, last), and the three vertices left at the end are
                //  (count - 3, count - 2, count - 1), which is the same fan for k = count - 3.
                UsedConvexFastPath = true;
                int last = count - 1;
                for (int k = 0; k < count - 2; k++)
                {
                    AddTriangle(k, k + 1, last);
                }

                return;
            }

            //  Triangulator.FindEarVertices: the convex vertices in the order of the polygon.
            _EarFirst = -1;
            _EarLast = -1;
            _EarCount = 0;
            for (int i = 0; i < count; i++)
            {
                _IsEar[i] = false;
            }

            for (int i = 0; i < count; i++)
            {
                if (_Kind[i] == Convex && IsEar(i))
                {
                    AddEarLast(i);
                }
            }

            _RingAnchor = 0;
            int remaining = count;
            while (remaining > 3 && _EarCount > 0)
            {
                ClipNextEar();
                remaining--;
            }

            if (remaining == 3)
            {
                //  The original lists the vertices that are left by ascending index: the first, second and third of its list.
                int a = _RingAnchor;
                int b = _Next[a];
                int c = _Next[b];
                int swap;
                if (a > b)
                {
                    swap = a;
                    a = b;
                    b = swap;
                }

                if (b > c)
                {
                    swap = b;
                    b = c;
                    c = swap;
                }

                if (a > b)
                {
                    swap = a;
                    a = b;
                    b = swap;
                }

                AddTriangle(a, b, c);
            }
        }

        private void AddTriangle(int a, int b, int c)
        {
            _Indices[_IndexCount] = a;
            _Indices[_IndexCount + 1] = b;
            _Indices[_IndexCount + 2] = c;
            _IndexCount += 3;
        }

        /// <summary>Triangulator.ClipNextEar: the first ear is cut with its next and previous vertices, and the two neighbours are validated again, previous first.</summary>
        private void ClipNextEar()
        {
            int ear = _EarFirst;
            int previous = _Previous[ear];
            int next = _Next[ear];
            AddTriangle(ear, next, previous);
            RemoveEar(ear);
            _Next[previous] = next;
            _Previous[next] = previous;
            _RingAnchor = previous;
            ValidateAdjacentVertex(previous);
            ValidateAdjacentVertex(next);
        }

        private void ValidateAdjacentVertex(int vertex)
        {
            if (_Kind[vertex] == Reflex && IsConvex(vertex))
            {
                RemoveReflex(vertex);
                _Kind[vertex] = Convex;
            }

            if (_Kind[vertex] == Convex)
            {
                bool wasEar = _IsEar[vertex];
                bool isEar = IsEar(vertex);
                if (wasEar && !isEar)
                {
                    RemoveEar(vertex);
                }
                else if (!wasEar && isEar)
                {
                    AddEarFirst(vertex);
                }
            }
        }

        /// <summary>Triangulator.IsConvex: the same two normalised directions and the same dot product with the left normal of the second.</summary>
        private bool IsConvex(int vertex)
        {
            Vector2 previous = _Vertices[_Previous[vertex]];
            Vector2 current = _Vertices[vertex];
            Vector2 next = _Vertices[_Next[vertex]];
            Vector2 incoming = Vector2.Normalize(current - previous);
            Vector2 outgoing = Vector2.Normalize(next - current);
            Vector2 normal = new Vector2(0f - outgoing.Y, outgoing.X);
            return Vector2.Dot(incoming, normal) <= 0f;
        }

        /// <summary>Triangulator.IsEar: no reflex vertex other than the vertex and its two neighbours is in the triangle (previous, vertex, next). The original's
        /// first test of <c>Triangle.ContainsPoint</c> (the point is one of the three vertices) is the exclusion of the three here: an index is a vertex of the original.</summary>
        private bool IsEar(int vertex)
        {
            int previous = _Previous[vertex];
            int next = _Next[vertex];
            Vector2 a = _Vertices[previous];
            Vector2 b = _Vertices[vertex];
            Vector2 c = _Vertices[next];
            for (int i = 0; i < _ReflexCount; i++)
            {
                int reflex = _Reflex[i];
                if (reflex != previous && reflex != vertex && reflex != next && ContainsPoint(a, b, c, _Vertices[reflex]))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary><c>Triangle.ContainsPoint</c> without its test of identity: the parity of the segments (c, a), (a, b), (b, c) that a ray from the point crosses.</summary>
        private static bool ContainsPoint(Vector2 a, Vector2 b, Vector2 c, Vector2 point)
        {
            bool inside = false;
            if (CheckPointToSegment(c, a, point))
            {
                inside = !inside;
            }

            if (CheckPointToSegment(a, b, point))
            {
                inside = !inside;
            }

            if (CheckPointToSegment(b, c, point))
            {
                inside = !inside;
            }

            return inside;
        }

        private static bool CheckPointToSegment(Vector2 sA, Vector2 sB, Vector2 point)
        {
            if (((sA.Y < point.Y && sB.Y >= point.Y) || (sB.Y < point.Y && sA.Y >= point.Y))
                && sA.X + (point.Y - sA.Y) / (sB.Y - sA.Y) * (sB.X - sA.X) < point.X)
            {
                return true;
            }

            return false;
        }

        /// <summary>List.Remove of the reflex list: the first entry that is the vertex goes, the following ones move up.</summary>
        private void RemoveReflex(int vertex)
        {
            for (int i = 0; i < _ReflexCount; i++)
            {
                if (_Reflex[i] == vertex)
                {
                    for (int j = i + 1; j < _ReflexCount; j++)
                    {
                        _Reflex[j - 1] = _Reflex[j];
                    }

                    _ReflexCount--;
                    return;
                }
            }
        }

        private void AddEarLast(int vertex)
        {
            _EarPrevious[vertex] = _EarLast;
            _EarNext[vertex] = -1;
            if (_EarLast >= 0)
            {
                _EarNext[_EarLast] = vertex;
            }
            else
            {
                _EarFirst = vertex;
            }

            _EarLast = vertex;
            _IsEar[vertex] = true;
            _EarCount++;
        }

        private void AddEarFirst(int vertex)
        {
            _EarPrevious[vertex] = -1;
            _EarNext[vertex] = _EarFirst;
            if (_EarFirst >= 0)
            {
                _EarPrevious[_EarFirst] = vertex;
            }
            else
            {
                _EarLast = vertex;
            }

            _EarFirst = vertex;
            _IsEar[vertex] = true;
            _EarCount++;
        }

        private void RemoveEar(int vertex)
        {
            int previous = _EarPrevious[vertex];
            int next = _EarNext[vertex];
            if (previous >= 0)
            {
                _EarNext[previous] = next;
            }
            else
            {
                _EarFirst = next;
            }

            if (next >= 0)
            {
                _EarPrevious[next] = previous;
            }
            else
            {
                _EarLast = previous;
            }

            _IsEar[vertex] = false;
            _EarCount--;
        }
    }
}
