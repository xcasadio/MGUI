using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph
{
    public class GraphDocument
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;
        public List<GraphNodeModel> Nodes { get; set; } = new();
        public List<GraphEdgeModel> Edges { get; set; } = new();
        public List<GraphCommentModel> Comments { get; set; } = new();
        public Dictionary<string, string> EditorMetadata { get; set; } = new(StringComparer.Ordinal);
        public bool DisallowCycles { get; set; }

        public event EventHandler GraphChanged;

        public GraphNodeModel AddNode(Guid id, string nodeType, string title, Vector2 position)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Node id must not be empty.", nameof(id));
            }

            if (TryGetNode(id) != null)
            {
                throw new InvalidOperationException($"Graph already contains node '{id}'.");
            }

            GraphNodeModel node = new(id, nodeType, title, position);
            Nodes.Add(node);
            OnGraphChanged();
            return node;
        }

        public GraphNodeModel AddNode(GraphNodeModel node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (node.Id == Guid.Empty)
            {
                throw new ArgumentException("Node id must not be empty.", nameof(node));
            }

            if (TryGetNode(node.Id) != null)
            {
                throw new InvalidOperationException($"Graph already contains node '{node.Id}'.");
            }

            Nodes.Add(node);
            OnGraphChanged();
            return node;
        }

        public bool RemoveNode(Guid nodeId)
        {
            GraphNodeModel node = TryGetNode(nodeId);
            if (node == null)
            {
                return false;
            }

            for (int i = Edges.Count - 1; i >= 0; i--)
            {
                GraphEdgeModel edge = Edges[i];
                if (edge.SourceNodeId == nodeId || edge.TargetNodeId == nodeId)
                {
                    Edges.RemoveAt(i);
                }
            }

            bool removed = Nodes.Remove(node);
            if (removed)
            {
                OnGraphChanged();
            }

            return removed;
        }

        public GraphNodeModel TryGetNode(Guid nodeId)
        {
            for (int i = 0; i < Nodes.Count; i++)
            {
                if (Nodes[i].Id == nodeId)
                {
                    return Nodes[i];
                }
            }

            return null;
        }

        public GraphPortModel AddPort(Guid nodeId, Guid portId, string name, GraphPortDirection direction, GraphValueType valueType,
            GraphPortCardinality cardinality = GraphPortCardinality.Single, bool isRequired = false)
        {
            if (portId == Guid.Empty)
            {
                throw new ArgumentException("Port id must not be empty.", nameof(portId));
            }

            GraphNodeModel node = TryGetNode(nodeId) ?? throw new InvalidOperationException($"Cannot add port to missing node '{nodeId}'.");
            if (TryGetPort(nodeId, portId) != null)
            {
                throw new InvalidOperationException($"Node '{nodeId}' already contains port '{portId}'.");
            }

            GraphPortModel port = new(nodeId, portId, name, direction, valueType, cardinality, isRequired);
            node.Ports.Add(port);
            OnGraphChanged();
            return port;
        }

        public GraphPortModel AddPort(Guid nodeId, GraphPortModel port)
        {
            if (port == null)
            {
                throw new ArgumentNullException(nameof(port));
            }

            GraphNodeModel node = TryGetNode(nodeId) ?? throw new InvalidOperationException($"Cannot add port to missing node '{nodeId}'.");
            port.NodeId = nodeId;
            if (TryGetPort(nodeId, port.Id) != null)
            {
                throw new InvalidOperationException($"Node '{nodeId}' already contains port '{port.Id}'.");
            }

            node.Ports.Add(port);
            OnGraphChanged();
            return port;
        }

        public GraphPortModel TryGetPort(Guid nodeId, Guid portId)
        {
            GraphNodeModel node = TryGetNode(nodeId);
            if (node == null)
            {
                return null;
            }

            for (int i = 0; i < node.Ports.Count; i++)
            {
                if (node.Ports[i].Id == portId)
                {
                    return node.Ports[i];
                }
            }

            return null;
        }

        public GraphPortModel TryGetPort(Guid portId)
        {
            for (int nodeIndex = 0; nodeIndex < Nodes.Count; nodeIndex++)
            {
                List<GraphPortModel> ports = Nodes[nodeIndex].Ports;
                for (int portIndex = 0; portIndex < ports.Count; portIndex++)
                {
                    if (ports[portIndex].Id == portId)
                    {
                        return ports[portIndex];
                    }
                }
            }

            return null;
        }

        public GraphEdgeModel Connect(Guid edgeId, Guid sourceNodeId, Guid sourcePortId, Guid targetNodeId, Guid targetPortId)
        {
            if (edgeId == Guid.Empty)
            {
                throw new ArgumentException("Edge id must not be empty.", nameof(edgeId));
            }

            if (TryGetEdge(edgeId) != null)
            {
                throw new InvalidOperationException($"Graph already contains edge '{edgeId}'.");
            }

            GraphPortModel sourcePort = TryGetPort(sourceNodeId, sourcePortId)
                ?? throw new InvalidOperationException($"Missing source port '{sourcePortId}' on node '{sourceNodeId}'.");
            GraphPortModel targetPort = TryGetPort(targetNodeId, targetPortId)
                ?? throw new InvalidOperationException($"Missing target port '{targetPortId}' on node '{targetNodeId}'.");

            if (sourcePort.Direction != GraphPortDirection.Output)
            {
                throw new InvalidOperationException("Source port must be an output port.");
            }

            if (targetPort.Direction != GraphPortDirection.Input)
            {
                throw new InvalidOperationException("Target port must be an input port.");
            }

            for (int i = 0; i < Edges.Count; i++)
            {
                GraphEdgeModel edge = Edges[i];
                if (edge.SourceNodeId == sourceNodeId && edge.SourcePortId == sourcePortId &&
                    edge.TargetNodeId == targetNodeId && edge.TargetPortId == targetPortId)
                {
                    throw new InvalidOperationException("Graph already contains this connection.");
                }

                if (targetPort.Cardinality == GraphPortCardinality.Single && edge.TargetNodeId == targetNodeId && edge.TargetPortId == targetPortId)
                {
                    throw new InvalidOperationException("Target port cardinality is single and already has a connection.");
                }
            }

            GraphEdgeModel newEdge = new(edgeId, sourceNodeId, sourcePortId, targetNodeId, targetPortId);
            Edges.Add(newEdge);
            OnGraphChanged();
            return newEdge;
        }

        public bool Disconnect(Guid edgeId)
        {
            GraphEdgeModel edge = TryGetEdge(edgeId);
            if (edge == null)
            {
                return false;
            }

            bool removed = Edges.Remove(edge);
            if (removed)
            {
                OnGraphChanged();
            }

            return removed;
        }

        public GraphEdgeModel TryGetEdge(Guid edgeId)
        {
            for (int i = 0; i < Edges.Count; i++)
            {
                if (Edges[i].Id == edgeId)
                {
                    return Edges[i];
                }
            }

            return null;
        }

        public List<GraphEdgeModel> GetEdgesForNode(Guid nodeId)
        {
            List<GraphEdgeModel> result = new();
            for (int i = 0; i < Edges.Count; i++)
            {
                GraphEdgeModel edge = Edges[i];
                if (edge.SourceNodeId == nodeId || edge.TargetNodeId == nodeId)
                {
                    result.Add(edge);
                }
            }

            return result;
        }

        public GraphCommentModel AddComment(Guid id, Rectangle bounds, string title, string text)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("Comment id must not be empty.", nameof(id));
            }

            if (TryGetComment(id) != null)
            {
                throw new InvalidOperationException($"Graph already contains comment '{id}'.");
            }

            GraphCommentModel comment = new(id, bounds, title, text);
            Comments.Add(comment);
            OnGraphChanged();
            return comment;
        }

        public bool RemoveComment(Guid commentId)
        {
            GraphCommentModel comment = TryGetComment(commentId);
            if (comment == null)
            {
                return false;
            }

            bool removed = Comments.Remove(comment);
            if (removed)
            {
                OnGraphChanged();
            }

            return removed;
        }

        public GraphCommentModel TryGetComment(Guid commentId)
        {
            for (int i = 0; i < Comments.Count; i++)
            {
                if (Comments[i].Id == commentId)
                {
                    return Comments[i];
                }
            }

            return null;
        }

        internal void NotifyGraphChanged() => OnGraphChanged();

        private void OnGraphChanged() => GraphChanged?.Invoke(this, EventArgs.Empty);
    }
}