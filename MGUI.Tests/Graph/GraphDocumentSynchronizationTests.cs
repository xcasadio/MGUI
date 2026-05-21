using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphDocumentSynchronizationTests
{
    [Fact]
    public void GraphView_CreatesVisibleNodeAndPortsWhenDocumentChanges()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid nodeId = Guid.NewGuid();
        Guid inputPortId = Guid.NewGuid();
        Guid outputPortId = Guid.NewGuid();

        GraphNodeModel nodeModel = document.AddNode(nodeId, "Math/Add", "Add", new Vector2(32, 48));
        nodeModel.EditorMetadata["HasWarning"] = "true";
        document.AddPort(nodeId, inputPortId, "A", GraphPortDirection.Input, GraphValueType.Float, isRequired: true);
        document.AddPort(nodeId, outputPortId, "Result", GraphPortDirection.Output, GraphValueType.Float);
        graphView.SelectedNodeIds.Add(nodeId);
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode node));
        Assert.Contains(node, graphView.NodesCanvas.Children);
        Assert.Equal("Add", node.Title);
        Assert.True(node.IsSelected);
        Assert.True(node.HasWarning);
        Assert.Equal(32, MGCanvas.GetLeft(node));
        Assert.Equal(48, MGCanvas.GetTop(node));
        Assert.True(graphView.TryGetPortControl(inputPortId, out MGGraphPort inputPort));
        Assert.True(graphView.TryGetPortControl(outputPortId, out MGGraphPort outputPort));
        Assert.Contains(inputPort, node.PortsPanel.Children);
        Assert.Contains(outputPort, node.PortsPanel.Children);
        Assert.True(inputPort.IsRequired);
    }

    [Fact]
    public void GraphView_RemovesNodeVisualAndPortsWhenNodeIsRemoved()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid nodeId = Guid.NewGuid();
        Guid portId = Guid.NewGuid();
        document.AddNode(nodeId, "Value", "Value", Vector2.Zero);
        document.AddPort(nodeId, portId, "Out", GraphPortDirection.Output, GraphValueType.Float);
        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode node));
        Assert.True(graphView.TryGetPortControl(portId, out _));

        document.RemoveNode(nodeId);

        Assert.False(graphView.TryGetNodeControl(nodeId, out _));
        Assert.False(graphView.TryGetPortControl(portId, out _));
        Assert.DoesNotContain(node, graphView.NodesCanvas.Children);
    }

    [Fact]
    public void GraphView_UpdatesNodePositionWithoutRecreatingOtherNodes()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid firstNodeId = Guid.NewGuid();
        Guid secondNodeId = Guid.NewGuid();
        GraphNodeModel firstModel = document.AddNode(firstNodeId, "Value", "First", new Vector2(10, 20));
        document.AddNode(secondNodeId, "Value", "Second", new Vector2(200, 220));
        Assert.True(graphView.TryGetNodeControl(firstNodeId, out MGGraphNode firstNodeBefore));
        Assert.True(graphView.TryGetNodeControl(secondNodeId, out MGGraphNode secondNodeBefore));

        firstModel.Position = new Vector2(80, 96);
        graphView.ViewportTransform.Pan = new Vector2(4, 8);
        graphView.ViewportTransform.Zoom = 2.0f;
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetNodeControl(firstNodeId, out MGGraphNode firstNodeAfter));
        Assert.True(graphView.TryGetNodeControl(secondNodeId, out MGGraphNode secondNodeAfter));
        Assert.Same(firstNodeBefore, firstNodeAfter);
        Assert.Same(secondNodeBefore, secondNodeAfter);
        Assert.Equal(164, MGCanvas.GetLeft(firstNodeAfter));
        Assert.Equal(200, MGCanvas.GetTop(firstNodeAfter));
    }

    [Fact]
    public void GraphView_ScalesDefaultNodeBoundsWithViewportZoom()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid nodeId = Guid.NewGuid();
        document.AddNode(nodeId, "Value", "Scaled", new Vector2(20, 30));

        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode nodeAtOne));
        int widthAtOne = nodeAtOne.PreferredWidth ?? 0;
        int heightAtOne = nodeAtOne.PreferredHeight ?? 0;

        graphView.ViewportTransform.Zoom = 2.0f;
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode node));
        Assert.True(node.PreferredWidth > widthAtOne);
        Assert.True(node.PreferredHeight > heightAtOne);
        Assert.Equal(40, MGCanvas.GetLeft(node));
        Assert.Equal(60, MGCanvas.GetTop(node));
    }

    [Fact]
    public void GraphView_AutoSizesNodesFromContent()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid shortNodeId = Guid.NewGuid();
        Guid longNodeId = Guid.NewGuid();
        document.AddNode(shortNodeId, "Value", "A", Vector2.Zero);
        document.AddNode(longNodeId, "Value", "A much longer node title", new Vector2(200, 0));
        document.AddPort(longNodeId, Guid.NewGuid(), "Output", GraphPortDirection.Output, GraphValueType.Float);
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetNodeControl(shortNodeId, out MGGraphNode shortNode));
        Assert.True(graphView.TryGetNodeControl(longNodeId, out MGGraphNode longNode));
        Assert.True(longNode.PreferredWidth > shortNode.PreferredWidth);
        Assert.True(longNode.PreferredHeight >= shortNode.PreferredHeight);
    }

    [Fact]
    public void GraphView_SynchronizesPortConnectionStateWithoutMutatingModel()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = Guid.NewGuid();
        Guid targetNodeId = Guid.NewGuid();
        Guid sourcePortId = Guid.NewGuid();
        Guid targetPortId = Guid.NewGuid();
        document.AddNode(sourceNodeId, "Value", "Source", Vector2.Zero);
        document.AddNode(targetNodeId, "Value", "Target", new Vector2(200, 0));
        GraphPortModel sourcePort = document.AddPort(sourceNodeId, sourcePortId, "Out", GraphPortDirection.Output, GraphValueType.Float);
        GraphPortModel targetPort = document.AddPort(targetNodeId, targetPortId, "In", GraphPortDirection.Input, GraphValueType.Float);

        document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);

        Assert.True(graphView.TryGetPortControl(sourcePortId, out MGGraphPort sourcePortControl));
        Assert.True(graphView.TryGetPortControl(targetPortId, out MGGraphPort targetPortControl));
        Assert.True(sourcePortControl.IsConnected);
        Assert.True(targetPortControl.IsConnected);
        Assert.Same(sourcePort, document.TryGetPort(sourcePortId));
        Assert.Same(targetPort, document.TryGetPort(targetPortId));
        Assert.Equal(sourceNodeId, sourcePort.NodeId);
        Assert.Equal(targetNodeId, targetPort.NodeId);
    }

    private static MGGraphView CreateGraphView()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
        };
        return new MGGraphView(window);
    }
}