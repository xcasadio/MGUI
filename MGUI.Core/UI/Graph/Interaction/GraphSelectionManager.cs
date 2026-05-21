using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Graph
{
    public sealed class GraphSelectionManager
    {
        public const string AutoMeasuredWidthMetadataKey = "__GraphAutoWidth";
        public const string AutoMeasuredHeightMetadataKey = "__GraphAutoHeight";

        public HashSet<Guid> SelectedNodeIds { get; }
        public HashSet<Guid> SelectedEdgeIds { get; }

        public GraphSelectionManager()
            : this(new HashSet<Guid>(), new HashSet<Guid>()) { }

        public GraphSelectionManager(HashSet<Guid> selectedNodeIds, HashSet<Guid> selectedEdgeIds)
        {
            SelectedNodeIds = selectedNodeIds ?? throw new ArgumentNullException(nameof(selectedNodeIds));
            SelectedEdgeIds = selectedEdgeIds ?? throw new ArgumentNullException(nameof(selectedEdgeIds));
        }

        public bool Clear()
        {
            bool changed = SelectedNodeIds.Count > 0 || SelectedEdgeIds.Count > 0;
            SelectedNodeIds.Clear();
            SelectedEdgeIds.Clear();
            return changed;
        }

        public bool SelectNode(Guid nodeId, bool additive = false, bool toggle = false)
        {
            if (nodeId == Guid.Empty)
            {
                return false;
            }

            bool changed = false;
            if (!additive)
            {
                changed |= ClearExceptNode(nodeId);
            }

            if (toggle && SelectedNodeIds.Contains(nodeId))
            {
                SelectedNodeIds.Remove(nodeId);
                return true;
            }

            changed |= SelectedNodeIds.Add(nodeId);
            SelectedEdgeIds.Clear();
            return changed;
        }

        public bool SelectNodes(IEnumerable<Guid> nodeIds, bool additive = false)
        {
            bool changed = false;
            if (!additive)
            {
                changed |= Clear();
            }

            if (nodeIds == null)
            {
                return changed;
            }

            foreach (Guid nodeId in nodeIds)
            {
                if (nodeId != Guid.Empty)
                {
                    changed |= SelectedNodeIds.Add(nodeId);
                }
            }

            return changed;
        }

        public bool SelectNodesInRectangle(GraphDocument document, RectangleF worldRectangle, bool additive = false)
        {
            bool changed = false;
            if (!additive)
            {
                changed |= Clear();
            }

            if (document == null || worldRectangle.Width <= 0.0f || worldRectangle.Height <= 0.0f)
            {
                return changed;
            }

            for (int nodeIndex = 0; nodeIndex < document.Nodes.Count; nodeIndex++)
            {
                GraphNodeModel node = document.Nodes[nodeIndex];
                if (node != null && Intersects(worldRectangle, GetNodeWorldBounds(node)))
                {
                    changed |= SelectedNodeIds.Add(node.Id);
                }
            }

            return changed;
        }

        public bool IsNodeSelected(Guid nodeId) => SelectedNodeIds.Contains(nodeId);

        public static Vector2 GetNodeWorldSize(GraphNodeModel node)
        {
            if (node == null)
            {
                return new Vector2(1.0f, 1.0f);
            }

            if (node.Size.HasValue)
            {
                return ClampSize(node.Size.Value);
            }

            return TryGetAutoMeasuredWorldSize(node, out Vector2 measured)
                ? measured
                : new Vector2(160.0f, 100.0f);
        }

        public static void SetAutoMeasuredWorldSize(GraphNodeModel node, Vector2 size)
        {
            if (node?.EditorMetadata == null)
            {
                return;
            }

            Vector2 clamped = ClampSize(size);
            node.EditorMetadata[AutoMeasuredWidthMetadataKey] = clamped.X.ToString(CultureInfo.InvariantCulture);
            node.EditorMetadata[AutoMeasuredHeightMetadataKey] = clamped.Y.ToString(CultureInfo.InvariantCulture);
        }

        public static void ClearAutoMeasuredWorldSize(GraphNodeModel node)
        {
            if (node?.EditorMetadata == null)
            {
                return;
            }

            node.EditorMetadata.Remove(AutoMeasuredWidthMetadataKey);
            node.EditorMetadata.Remove(AutoMeasuredHeightMetadataKey);
        }

        public static RectangleF GetNodeWorldBounds(GraphNodeModel node)
        {
            if (node == null)
            {
                return new RectangleF();
            }

            Vector2 size = GetNodeWorldSize(node);
            return new RectangleF(node.Position.X, node.Position.Y, Math.Max(1.0f, size.X), Math.Max(1.0f, size.Y));
        }

        private static bool TryGetAutoMeasuredWorldSize(GraphNodeModel node, out Vector2 size)
        {
            size = default;
            if (node?.EditorMetadata == null)
            {
                return false;
            }

            if (!node.EditorMetadata.TryGetValue(AutoMeasuredWidthMetadataKey, out string widthText)
                || !node.EditorMetadata.TryGetValue(AutoMeasuredHeightMetadataKey, out string heightText)
                || !float.TryParse(widthText, NumberStyles.Float, CultureInfo.InvariantCulture, out float width)
                || !float.TryParse(heightText, NumberStyles.Float, CultureInfo.InvariantCulture, out float height))
            {
                return false;
            }

            size = ClampSize(new Vector2(width, height));
            return true;
        }

        private static Vector2 ClampSize(Vector2 size)
            => new(Math.Max(1.0f, size.X), Math.Max(1.0f, size.Y));

        private bool ClearExceptNode(Guid nodeId)
        {
            bool changed = false;
            if (SelectedEdgeIds.Count > 0)
            {
                SelectedEdgeIds.Clear();
                changed = true;
            }

            if (SelectedNodeIds.Count == 1 && SelectedNodeIds.Contains(nodeId))
            {
                return changed;
            }

            if (SelectedNodeIds.Count > 0)
            {
                SelectedNodeIds.Clear();
                changed = true;
            }

            return changed;
        }

        private static bool Intersects(RectangleF first, RectangleF second)
            => first.Left < second.Right && first.Right > second.Left && first.Top < second.Bottom && first.Bottom > second.Top;
    }
}