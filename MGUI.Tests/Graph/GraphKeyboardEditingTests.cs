using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Graph;

public class GraphKeyboardEditingTests
{
    [Fact]
    public void GraphKeyboard_DeleteSelectionRemovesSelectedNodeAndUndoRestoresIt()
    {
        MGGraphView graphView = CreateGraphView();
        Guid nodeId = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Value", Vector2.Zero).Id;
        graphView.SelectNode(nodeId);

        Assert.True(graphView.HandleGraphShortcut(Keys.Delete, controlDown: false));

        Assert.Null(graphView.Document.TryGetNode(nodeId));
        Assert.Empty(graphView.SelectedNodeIds);
        Assert.True(graphView.Commands.Undo(graphView.Document));
        Assert.NotNull(graphView.Document.TryGetNode(nodeId));
    }

    [Fact]
    public void GraphKeyboard_DeleteSelectionRemovesSelectedEdgeAndUndoRestoresIt()
    {
        MGGraphView graphView = CreateGraphView();
        (Guid sourceNodeId, Guid sourcePortId, Guid targetNodeId, Guid targetPortId) = AddCompatiblePorts(graphView.Document);
        GraphEdgeModel edge = graphView.Document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId));
        graphView.SelectedEdgeIds.Add(edge.Id);

        Assert.True(graphView.DeleteSelection());

        Assert.Empty(graphView.Document.Edges);
        Assert.True(graphView.Commands.Undo(graphView.Document));
        Assert.Single(graphView.Document.Edges);
    }

    [Fact]
    public void GraphKeyboard_ControlZAndControlYUseGraphCommandStack()
    {
        MGGraphView graphView = CreateGraphView();
        Guid nodeId = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Value", Vector2.Zero).Id;
        graphView.SelectNode(nodeId);
        Assert.True(graphView.MoveSelectedNodesBy(new Vector2(20, 0)));

        Assert.True(graphView.HandleGraphShortcut(Keys.Z, controlDown: true));
        Assert.Equal(Vector2.Zero, graphView.Document.TryGetNode(nodeId).Position);
        Assert.True(graphView.HandleGraphShortcut(Keys.Y, controlDown: true));
        Assert.Equal(new Vector2(20, 0), graphView.Document.TryGetNode(nodeId).Position);
    }

    [Fact]
    public void GraphKeyboard_EscapeCancelsActiveConnectionDrag()
    {
        MGGraphView graphView = CreateGraphView();
        (_, Guid sourcePortId, _, Guid targetPortId) = AddCompatiblePorts(graphView.Document);
        Assert.True(graphView.ConnectionController.BeginDrag(sourcePortId, new Vector2(0, 0)));
        graphView.ConnectionController.UpdateDrag(new Vector2(40, 20), targetPortId);

        Assert.True(graphView.HandleGraphShortcut(Keys.Escape, controlDown: false));

        Assert.False(graphView.ConnectionController.IsDragging);
        Assert.Empty(graphView.Document.Edges);
    }

    [Fact]
    public void GraphKeyboard_ControlDIsReservedForV2AndNotHandled()
    {
        MGGraphView graphView = CreateGraphView();

        Assert.False(graphView.HandleGraphShortcut(Keys.D, controlDown: true));
    }

    private static (Guid SourceNodeId, Guid SourcePortId, Guid TargetNodeId, Guid TargetPortId) AddCompatiblePorts(GraphDocument document)
    {
        Guid sourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "Source", Vector2.Zero).Id;
        Guid targetNodeId = document.AddNode(Guid.NewGuid(), "Value", "Target", new Vector2(200, 0)).Id;
        Guid sourcePortId = document.AddPort(sourceNodeId, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Float).Id;
        Guid targetPortId = document.AddPort(targetNodeId, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Float).Id;
        return (sourceNodeId, sourcePortId, targetNodeId, targetPortId);
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