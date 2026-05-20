using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph
{
    internal sealed class GraphDocumentViewSynchronizer
    {
        private readonly MGGraphView GraphView;
        private readonly Dictionary<Guid, MGGraphNode> NodesById = new();
        private readonly Dictionary<Guid, MGGraphPort> PortsById = new();

        public GraphDocumentViewSynchronizer(MGGraphView graphView)
        {
            GraphView = graphView ?? throw new ArgumentNullException(nameof(graphView));
        }

        public bool TryGetNode(Guid nodeId, out MGGraphNode node) => NodesById.TryGetValue(nodeId, out node);

        public bool TryGetPort(Guid portId, out MGGraphPort port) => PortsById.TryGetValue(portId, out port);

        public void Synchronize()
        {
            if (GraphView.NodesCanvas == null || GraphView.Document == null)
            {
                return;
            }

            HashSet<Guid> modelNodeIds = new(GraphView.Document.Nodes.Select(node => node.Id));
            RemoveMissingNodes(modelNodeIds);

            for (int nodeIndex = 0; nodeIndex < GraphView.Document.Nodes.Count; nodeIndex++)
            {
                SynchronizeNode(GraphView.Document.Nodes[nodeIndex]);
            }
        }

        private void RemoveMissingNodes(HashSet<Guid> modelNodeIds)
        {
            List<Guid> missingNodeIds = NodesById.Keys.Where(nodeId => !modelNodeIds.Contains(nodeId)).ToList();
            if (missingNodeIds.Count == 0)
            {
                return;
            }

            using (GraphView.NodesCanvas.AllowChangingContentTemporarily())
            {
                for (int missingIndex = 0; missingIndex < missingNodeIds.Count; missingIndex++)
                {
                    Guid nodeId = missingNodeIds[missingIndex];
                    if (NodesById.TryGetValue(nodeId, out MGGraphNode node))
                    {
                        GraphView.NodesCanvas.TryRemoveChild(node);
                        NodesById.Remove(nodeId);
                    }
                }
            }

            foreach (Guid portId in PortsById.Where(item => !NodesById.ContainsKey(item.Value.Model?.NodeId ?? Guid.Empty)).Select(item => item.Key).ToList())
            {
                PortsById.Remove(portId);
            }
        }

        private void SynchronizeNode(GraphNodeModel model)
        {
            if (model == null || model.Id == Guid.Empty)
            {
                return;
            }

            if (!NodesById.TryGetValue(model.Id, out MGGraphNode node))
            {
                node = new MGGraphNode(GraphView.SelfOrParentWindow, model);
                NodesById[model.Id] = node;
                using (GraphView.NodesCanvas.AllowChangingContentTemporarily())
                {
                    GraphView.NodesCanvas.TryAddChild(node);
                }
            }

            node.NodeId = model.Id;
            node.Title = model.Title;
            node.IsSelected = GraphView.SelectedNodeIds.Contains(model.Id);
            node.IsCollapsed = model.IsCollapsed;
            node.HasError = ReadBooleanMetadata(model.EditorMetadata, "HasError", "Error");
            node.HasWarning = ReadBooleanMetadata(model.EditorMetadata, "HasWarning", "Warning");

            if (model.Size.HasValue)
            {
                node.PreferredWidth = Math.Max(0, (int)MathF.Round(model.Size.Value.X));
                node.PreferredHeight = Math.Max(0, (int)MathF.Round(model.Size.Value.Y));
            }

            Vector2 layoutPosition = GraphView.ViewportTransform.WorldToLayout(model.Position);
            MGCanvas.SetLeft(node, (int)MathF.Round(layoutPosition.X));
            MGCanvas.SetTop(node, (int)MathF.Round(layoutPosition.Y));

            SynchronizePorts(node, model);
        }

        private void SynchronizePorts(MGGraphNode node, GraphNodeModel model)
        {
            HashSet<Guid> modelPortIds = new(model.Ports.Select(port => port.Id));
            List<MGGraphPort> removedPorts = node.PortsPanel.Children
                .OfType<MGGraphPort>()
                .Where(port => !modelPortIds.Contains(port.PortId))
                .ToList();

            using (node.PortsPanel.AllowChangingContentTemporarily())
            {
                for (int removeIndex = 0; removeIndex < removedPorts.Count; removeIndex++)
                {
                    node.PortsPanel.TryRemoveChild(removedPorts[removeIndex]);
                    PortsById.Remove(removedPorts[removeIndex].PortId);
                }

                for (int portIndex = 0; portIndex < model.Ports.Count; portIndex++)
                {
                    GraphPortModel portModel = model.Ports[portIndex];
                    if (portModel == null || portModel.Id == Guid.Empty)
                    {
                        continue;
                    }

                    if (!PortsById.TryGetValue(portModel.Id, out MGGraphPort port))
                    {
                        port = new MGGraphPort(GraphView.SelfOrParentWindow, portModel);
                        PortsById[portModel.Id] = port;
                        node.PortsPanel.TryAddChild(port);
                    }

                    port.PortId = portModel.Id;
                    port.PortName = portModel.Name;
                    port.Direction = portModel.Direction;
                    port.ValueType = portModel.ValueType;
                    port.IsRequired = portModel.IsRequired;
                    port.IsConnected = IsPortConnected(portModel.Id);
                    GraphView.RegisterGraphPort(port);
                }
            }
        }

        private bool IsPortConnected(Guid portId)
        {
            for (int edgeIndex = 0; edgeIndex < GraphView.Document.Edges.Count; edgeIndex++)
            {
                GraphEdgeModel edge = GraphView.Document.Edges[edgeIndex];
                if (edge.SourcePortId == portId || edge.TargetPortId == portId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ReadBooleanMetadata(Dictionary<string, string> metadata, params string[] keys)
        {
            if (metadata == null)
            {
                return false;
            }

            for (int keyIndex = 0; keyIndex < keys.Length; keyIndex++)
            {
                if (metadata.TryGetValue(keys[keyIndex], out string value))
                {
                    return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
                }
            }

            return false;
        }
    }
}