using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph
{
    public sealed class GraphEdgeGeometryCache
    {
        private readonly Dictionary<Guid, CachedEdgeGeometry> Cache = new();

        public int CacheHits { get; private set; }
        public int CacheMisses { get; private set; }

        public IReadOnlyList<Vector2> GetOrCreate(Guid edgeId, Vector2 start, Vector2 end, float thickness, float zoom, int segmentCount = GraphBezierGeometry.DefaultSegmentCount)
        {
            if (Cache.TryGetValue(edgeId, out CachedEdgeGeometry cached) && cached.Matches(start, end, thickness, zoom, segmentCount))
            {
                CacheHits++;
                return cached.Points;
            }

            if (cached == null)
            {
                cached = new CachedEdgeGeometry();
                Cache[edgeId] = cached;
            }

            cached.Update(start, end, thickness, zoom, segmentCount);
            CacheMisses++;
            return cached.Points;
        }

        public void Remove(Guid edgeId) => Cache.Remove(edgeId);

        public void RetainEdges(IReadOnlyList<GraphEdgeModel> edges)
        {
            if (edges == null || Cache.Count == 0)
            {
                return;
            }

            List<Guid> missingEdgeIds = null;
            foreach (Guid edgeId in Cache.Keys)
            {
                if (!ContainsEdge(edges, edgeId))
                {
                    missingEdgeIds ??= new List<Guid>();
                    missingEdgeIds.Add(edgeId);
                }
            }

            if (missingEdgeIds == null)
            {
                return;
            }

            for (int edgeIndex = 0; edgeIndex < missingEdgeIds.Count; edgeIndex++)
            {
                Cache.Remove(missingEdgeIds[edgeIndex]);
            }
        }

        public void Clear()
        {
            Cache.Clear();
            CacheHits = 0;
            CacheMisses = 0;
        }

        private static bool ContainsEdge(IReadOnlyList<GraphEdgeModel> edges, Guid edgeId)
        {
            for (int edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
            {
                if (edges[edgeIndex]?.Id == edgeId)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class CachedEdgeGeometry
        {
            public List<Vector2> Points { get; } = new();
            private Vector2 Start;
            private Vector2 End;
            private float Thickness;
            private float Zoom;
            private int SegmentCount;

            public bool Matches(Vector2 start, Vector2 end, float thickness, float zoom, int segmentCount)
                => Start == start && End == end && Thickness.Equals(thickness) && Zoom.Equals(zoom) && SegmentCount == segmentCount;

            public void Update(Vector2 start, Vector2 end, float thickness, float zoom, int segmentCount)
            {
                Start = start;
                End = end;
                Thickness = thickness;
                Zoom = zoom;
                SegmentCount = Math.Max(1, segmentCount);
                GraphBezierGeometry.BuildDefaultEdge(start, end, Points, SegmentCount);
            }
        }
    }
}