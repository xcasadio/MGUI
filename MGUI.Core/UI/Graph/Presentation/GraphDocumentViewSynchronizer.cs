using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Graph;

internal sealed class GraphDocumentViewSynchronizer
{
    private static readonly Size AutoMeasureSize = new(4096, 4096);
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

        var worldViewport = GraphView.GetCullingWorldViewport();
        GraphCullingDiagnostics diagnostics = new();

        for (var commentIndex = 0; commentIndex < GraphView.Document.Comments.Count; commentIndex++)
        {
            var comment = GraphView.Document.Comments[commentIndex];
            var isVisible = !GraphView.EnableViewportCulling || GraphView.CullingService.IsCommentVisible(comment, worldViewport);
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

        for (var nodeIndex = 0; nodeIndex < GraphView.Document.Nodes.Count; nodeIndex++)
        {
            var node = GraphView.Document.Nodes[nodeIndex];
            var isVisible = !GraphView.EnableViewportCulling || GraphView.CullingService.IsNodeVisible(node, worldViewport);
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
        var missingNodeIds = NodesById.Keys.Where(nodeId => !modelNodeIds.Contains(nodeId)).ToList();
        if (missingNodeIds.Count == 0)
        {
            return;
        }

        using (GraphView.NodesCanvas.AllowChangingContentTemporarily())
        {
            for (var missingIndex = 0; missingIndex < missingNodeIds.Count; missingIndex++)
            {
                var nodeId = missingNodeIds[missingIndex];
                if (NodesById.TryGetValue(nodeId, out var node))
                {
                    GraphView.NodesCanvas.TryRemoveChild(node);
                    NodesById.Remove(nodeId);
                }
            }
        }

        foreach (var portId in PortsById.Where(item => !NodesById.ContainsKey(item.Value.Model?.NodeId ?? Guid.Empty)).Select(item => item.Key).ToList())
        {
            PortsById.Remove(portId);
        }
    }

    private void RemoveMissingComments(HashSet<Guid> modelCommentIds)
    {
        var missingCommentIds = CommentsById.Keys.Where(commentId => !modelCommentIds.Contains(commentId)).ToList();
        if (missingCommentIds.Count == 0)
        {
            return;
        }

        using (GraphView.NodesCanvas.AllowChangingContentTemporarily())
        {
            for (var missingIndex = 0; missingIndex < missingCommentIds.Count; missingIndex++)
            {
                var commentId = missingCommentIds[missingIndex];
                if (CommentsById.TryGetValue(commentId, out var commentBox))
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

        if (!CommentsById.TryGetValue(model.Id, out var commentBox))
        {
            commentBox = new MGGraphCommentBox(GraphView.SelfOrParentWindow, model);
            CommentsById[model.Id] = commentBox;
            GraphView.RegisterGraphComment(commentBox);
            using (GraphView.NodesCanvas.AllowChangingContentTemporarily())
            {
                GraphView.NodesCanvas.TryAddChild(commentBox);
            }
        }

        commentBox.CommentId = model.Id;
        commentBox.Visibility = Visibility.Visible;
        var isEditingActiveComment = GraphView.EditingCommentId == model.Id && commentBox.IsEditing;
        commentBox.Title = model.Title;
        commentBox.Text = !string.IsNullOrWhiteSpace(model.Text) ? model.Text : model.Title ?? string.Empty;
        commentBox.IsSelected = GraphView.SelectedCommentIds.Contains(model.Id);
        commentBox.ApplySelectionVisual();
        commentBox.ApplyZoomScale(GraphView.ViewportTransform.Zoom);
        var normalizedBounds = isEditingActiveComment ? model.Bounds : GraphView.NormalizeCommentBoundsToContent(model, commentBox);
        if (normalizedBounds != model.Bounds)
        {
            model.Bounds = normalizedBounds;
        }

        commentBox.PreferredWidth = Math.Max(1, (int)MathF.Round(normalizedBounds.Width * GraphView.ViewportTransform.Zoom));
        commentBox.PreferredHeight = Math.Max(1, (int)MathF.Round(normalizedBounds.Height * GraphView.ViewportTransform.Zoom));

        var layoutPosition = GraphView.ViewportTransform.WorldToLayout(new Vector2(normalizedBounds.X, normalizedBounds.Y));
        MGCanvas.SetLeft(commentBox, (int)MathF.Round(layoutPosition.X));
        MGCanvas.SetTop(commentBox, (int)MathF.Round(layoutPosition.Y));
    }

    private void SynchronizeNode(GraphNodeModel model)
    {
        if (model == null || model.Id == Guid.Empty)
        {
            return;
        }

        if (!NodesById.TryGetValue(model.Id, out var node))
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

        SynchronizePorts(node, model);

        var zoom = Math.Max(0.01f, GraphView.ViewportTransform.Zoom);
        node.ApplyZoomScale(zoom);

        if (model.Size.HasValue)
        {
            GraphSelectionManager.ClearAutoMeasuredWorldSize(model);
            node.PreferredWidth = Math.Max(1, (int)MathF.Round(model.Size.Value.X * zoom));
            node.PreferredHeight = Math.Max(1, (int)MathF.Round(model.Size.Value.Y * zoom));
        }
        else
        {
            node.PreferredWidth = null;
            node.PreferredHeight = null;
            node.UpdateMeasurement(AutoMeasureSize, out _, out var fullSize, out _, out _);
            var desiredWidth = Math.Max(1, fullSize.Width);
            var desiredHeight = Math.Max(1, fullSize.Height);
            node.PreferredWidth = desiredWidth;
            node.PreferredHeight = desiredHeight;
            GraphSelectionManager.SetAutoMeasuredWorldSize(model, new Vector2(desiredWidth / zoom, desiredHeight / zoom));
        }

        GraphView.RegisterGraphNode(node);

        var layoutPosition = GraphView.ViewportTransform.WorldToLayout(model.Position);
        MGCanvas.SetLeft(node, (int)MathF.Round(layoutPosition.X));
        MGCanvas.SetTop(node, (int)MathF.Round(layoutPosition.Y));
    }

    private void SynchronizePorts(MGGraphNode node, GraphNodeModel model)
    {
        HashSet<Guid> modelPortIds = new(model.Ports.Select(port => port.Id));
        var removedPortIds = PortsById
            .Where(item => item.Value?.Model?.NodeId == model.Id && !modelPortIds.Contains(item.Key))
            .Select(item => item.Key)
            .ToList();

        List<GraphPortModel> inputPorts = new();
        List<GraphPortModel> outputPorts = new();

        using (node.PortsPanel.AllowChangingContentTemporarily())
        {
            for (var removeIndex = 0; removeIndex < removedPortIds.Count; removeIndex++)
            {
                PortsById.Remove(removedPortIds[removeIndex]);
            }

            node.PortsPanel.TryRemoveAll();
            while (node.PortsPanel.Rows.Count > 0)
            {
                node.PortsPanel.RemoveRow(node.PortsPanel.Rows[^1]);
            }

            for (var portIndex = 0; portIndex < model.Ports.Count; portIndex++)
            {
                var portModel = model.Ports[portIndex];
                if (portModel == null || portModel.Id == Guid.Empty)
                {
                    continue;
                }

                if (!PortsById.TryGetValue(portModel.Id, out var port))
                {
                    port = new MGGraphPort(GraphView.SelfOrParentWindow, portModel);
                    PortsById[portModel.Id] = port;
                }

                port.PortId = portModel.Id;
                port.PortName = portModel.Name;
                port.Direction = portModel.Direction;
                port.ValueType = portModel.ValueType;
                port.IsRequired = portModel.IsRequired;
                port.IsConnected = IsPortConnected(portModel.Id);
                port.ApplyZoomScale(GraphView.ViewportTransform.Zoom);
                GraphView.RegisterGraphPort(port);

                if (portModel.Direction == GraphPortDirection.Input)
                {
                    inputPorts.Add(portModel);
                }
                else
                {
                    outputPorts.Add(portModel);
                }
            }

            var rowCount = Math.Max(inputPorts.Count, outputPorts.Count);
            for (var rowIndex = 0; rowIndex < rowCount; rowIndex++)
            {
                var row = node.PortsPanel.AddRow(GridLength.Auto);
                if (rowIndex < inputPorts.Count && PortsById.TryGetValue(inputPorts[rowIndex].Id, out var inputPort))
                {
                    node.PortsPanel.TryAddChild(row, node.PortsPanel.Columns[0], inputPort);
                }

                if (rowIndex < outputPorts.Count && PortsById.TryGetValue(outputPorts[rowIndex].Id, out var outputPort))
                {
                    node.PortsPanel.TryAddChild(row, node.PortsPanel.Columns[1], outputPort);
                }
            }
        }
    }

    private void SetNodeVisibility(Guid nodeId, Visibility visibility)
    {
        if (nodeId != Guid.Empty && NodesById.TryGetValue(nodeId, out var node))
        {
            node.Visibility = visibility;
        }
    }

    private void SetCommentVisibility(Guid commentId, Visibility visibility)
    {
        if (commentId != Guid.Empty && CommentsById.TryGetValue(commentId, out var commentBox))
        {
            commentBox.Visibility = visibility;
        }
    }

    private bool IsPortConnected(Guid portId)
    {
        for (var edgeIndex = 0; edgeIndex < GraphView.Document.Edges.Count; edgeIndex++)
        {
            var edge = GraphView.Document.Edges[edgeIndex];
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

        for (var keyIndex = 0; keyIndex < keys.Length; keyIndex++)
        {
            if (metadata.TryGetValue(keys[keyIndex], out var value))
            {
                return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }
}