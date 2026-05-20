using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Graph
{
    internal sealed class GraphDocumentViewSynchronizer
    {
        private readonly MGGraphView GraphView;
        private readonly Dictionary<Guid, MGGraphNode> NodesById = new();
        private readonly Dictionary<Guid, MGGraphPort> PortsById = new();
        private readonly Dictionary<Guid, MGGraphCommentBox> CommentsById = new();

        public GraphDocumentViewSynchronizer(MGGraphView graphView)
        {
            GraphView = graphView ?? throw new ArgumentNullException(nameof(graphView));
        }

        public bool TryGetNode(Guid nodeId, out MGGraphNode node) => NodesById.TryGetValue(nodeId, out node);

        public bool TryGetPort(Guid portId, out MGGraphPort port) => PortsById.TryGetValue(portId, out port);

        public bool TryGetComment(Guid commentId, out MGGraphCommentBox commentBox) => CommentsById.TryGetValue(commentId, out commentBox);

        public void Synchronize()
        {
            if (GraphView.NodesCanvas == null || GraphView.Document == null)
            {
                return;
            }

            HashSet<Guid> modelNodeIds = new(GraphView.Document.Nodes.Select(node => node.Id));
            HashSet<Guid> modelCommentIds = new(GraphView.Document.Comments.Select(comment => comment.Id));
            RemoveMissingNodes(modelNodeIds);
            RemoveMissingComments(modelCommentIds);

            RectangleF worldViewport = GraphView.GetCullingWorldViewport();
            GraphCullingDiagnostics diagnostics = new();

            for (int commentIndex = 0; commentIndex < GraphView.Document.Comments.Count; commentIndex++)
            {
                GraphCommentModel comment = GraphView.Document.Comments[commentIndex];
                bool isVisible = !GraphView.EnableViewportCulling || GraphView.CullingService.IsCommentVisible(comment, worldViewport);
                if (isVisible)
                {
                    diagnostics.CommentsVisible++;
                    SynchronizeComment(comment);
                }
                else
                {
                    diagnostics.CommentsCulled++;
                    SetCommentVisibility(comment?.Id ?? Guid.Empty, Visibility.Collapsed);
                }
            }

            for (int nodeIndex = 0; nodeIndex < GraphView.Document.Nodes.Count; nodeIndex++)
            {
                GraphNodeModel node = GraphView.Document.Nodes[nodeIndex];
                bool isVisible = !GraphView.EnableViewportCulling || GraphView.CullingService.IsNodeVisible(node, worldViewport);
                if (isVisible)
                {
                    diagnostics.NodesVisible++;
                    SynchronizeNode(node);
                }
                else
                {
                    diagnostics.NodesCulled++;
                    SetNodeVisibility(node?.Id ?? Guid.Empty, Visibility.Collapsed);
                }
            }

            GraphView.SetNodeCommentCullingDiagnostics(diagnostics);
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

        private void RemoveMissingComments(HashSet<Guid> modelCommentIds)
        {
            List<Guid> missingCommentIds = CommentsById.Keys.Where(commentId => !modelCommentIds.Contains(commentId)).ToList();
            if (missingCommentIds.Count == 0)
            {
                return;
            }

            using (GraphView.NodesCanvas.AllowChangingContentTemporarily())
            {
                for (int missingIndex = 0; missingIndex < missingCommentIds.Count; missingIndex++)
                {
                    Guid commentId = missingCommentIds[missingIndex];
                    if (CommentsById.TryGetValue(commentId, out MGGraphCommentBox commentBox))
                    {
                        GraphView.NodesCanvas.TryRemoveChild(commentBox);
                        CommentsById.Remove(commentId);
                    }
                }
            }
        }

        private void SynchronizeComment(GraphCommentModel model)
        {
            if (model == null || model.Id == Guid.Empty)
            {
                return;
            }

            if (!CommentsById.TryGetValue(model.Id, out MGGraphCommentBox commentBox))
            {
                commentBox = new MGGraphCommentBox(GraphView.SelfOrParentWindow, model);
                CommentsById[model.Id] = commentBox;
                using (GraphView.NodesCanvas.AllowChangingContentTemporarily())
                {
                    GraphView.NodesCanvas.TryAddChild(commentBox);
                }
            }

            commentBox.CommentId = model.Id;
            commentBox.Visibility = Visibility.Visible;
            commentBox.Title = model.Title;
            commentBox.Text = model.Text;
            commentBox.IsSelected = GraphView.SelectedCommentIds.Contains(model.Id);
            commentBox.ApplySelectionVisual();
            commentBox.PreferredWidth = Math.Max(1, (int)MathF.Round(model.Bounds.Width * GraphView.ViewportTransform.Zoom));
            commentBox.PreferredHeight = Math.Max(1, (int)MathF.Round(model.Bounds.Height * GraphView.ViewportTransform.Zoom));

            Vector2 layoutPosition = GraphView.ViewportTransform.WorldToLayout(new Vector2(model.Bounds.X, model.Bounds.Y));
            MGCanvas.SetLeft(commentBox, (int)MathF.Round(layoutPosition.X));
            MGCanvas.SetTop(commentBox, (int)MathF.Round(layoutPosition.Y));
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
            node.Visibility = Visibility.Visible;
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

        private void SetNodeVisibility(Guid nodeId, Visibility visibility)
        {
            if (nodeId != Guid.Empty && NodesById.TryGetValue(nodeId, out MGGraphNode node))
            {
                node.Visibility = visibility;
            }
        }

        private void SetCommentVisibility(Guid commentId, Visibility visibility)
        {
            if (commentId != Guid.Empty && CommentsById.TryGetValue(commentId, out MGGraphCommentBox commentBox))
            {
                commentBox.Visibility = visibility;
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