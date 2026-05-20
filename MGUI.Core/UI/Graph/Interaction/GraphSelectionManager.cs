using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Graph
{
    public sealed class GraphSelectionManager
    {
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

        public static RectangleF GetNodeWorldBounds(GraphNodeModel node)
        {
            if (node == null)
            {
                return new RectangleF();
            }

            Vector2 size = node.Size ?? new Vector2(160.0f, 100.0f);
            return new RectangleF(node.Position.X, node.Position.Y, Math.Max(1.0f, size.X), Math.Max(1.0f, size.Y));
        }

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