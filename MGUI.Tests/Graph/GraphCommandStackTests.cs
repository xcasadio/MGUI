using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphCommandStackTests
{
    [Fact]
    public void ExecuteUndoRedo_CreateNode_PreservesStableId()
    {
        GraphDocument document = new();
        GraphCommandStack stack = new();
        Guid nodeId = Guid.NewGuid();
        GraphNodeModel node = new(nodeId, "DialogueLine", "Line", new Vector2(10, 20));

        Assert.True(stack.Execute(document, new CreateNodeCommand(node)));
        Assert.NotNull(document.TryGetNode(nodeId));
        Assert.True(stack.CanUndo);

        Assert.True(stack.Undo(document));
        Assert.Null(document.TryGetNode(nodeId));
        Assert.True(stack.CanRedo);

        Assert.True(stack.Redo(document));
        Assert.Equal(nodeId, document.TryGetNode(nodeId)!.Id);
    }

    [Fact]
    public void MoveNodeCommand_RestoresOldAndNewPositions()
    {
        GraphDocument document = new();
        GraphNodeModel node = document.AddNode(Guid.NewGuid(), "Line", "Line", new Vector2(1, 2));
        GraphCommandStack stack = new();

        Assert.True(stack.Execute(document, new MoveNodeCommand(node.Id, node.Position, new Vector2(30, 40))));
        Assert.Equal(new Vector2(30, 40), node.Position);

        Assert.True(stack.Undo(document));
        Assert.Equal(new Vector2(1, 2), node.Position);

        Assert.True(stack.Redo(document));
        Assert.Equal(new Vector2(30, 40), node.Position);
    }

    [Fact]
    public void ConnectAndDisconnectCommands_AreUndoable()
    {
        GraphDocument document = CreateConnectionDocument(out GraphPortModel sourcePort, out GraphPortModel targetPort);
        GraphCommandStack stack = new();
        GraphEdgeModel edge = new(Guid.NewGuid(), sourcePort.NodeId, sourcePort.Id, targetPort.NodeId, targetPort.Id);

        Assert.True(stack.Execute(document, new ConnectPortsCommand(edge)));
        Assert.Single(document.Edges);

        Assert.True(stack.Undo(document));
        Assert.Empty(document.Edges);

        Assert.True(stack.Redo(document));
        Assert.Single(document.Edges);

        Assert.True(stack.Execute(document, new DisconnectPortsCommand(edge.Id)));
        Assert.Empty(document.Edges);

        Assert.True(stack.Undo(document));
        Assert.Single(document.Edges);
    }

    [Fact]
    public void ExecutingNewCommand_ClearsRedoStack()
    {
        GraphDocument document = new();
        GraphCommandStack stack = new();
        GraphNodeModel first = new(Guid.NewGuid(), "Line", "First", Vector2.Zero);
        GraphNodeModel second = new(Guid.NewGuid(), "Line", "Second", Vector2.Zero);

        stack.Execute(document, new CreateNodeCommand(first));
        stack.Undo(document);
        Assert.True(stack.CanRedo);

        stack.Execute(document, new CreateNodeCommand(second));

        Assert.False(stack.CanRedo);
        Assert.NotNull(document.TryGetNode(second.Id));
        Assert.Null(document.TryGetNode(first.Id));
    }

    [Fact]
    public void DeleteNodeCommand_RestoresDependentEdgesOnUndo()
    {
        GraphDocument document = CreateConnectionDocument(out GraphPortModel sourcePort, out GraphPortModel targetPort);
        GraphEdgeModel edge = document.Connect(Guid.NewGuid(), sourcePort.NodeId, sourcePort.Id, targetPort.NodeId, targetPort.Id);
        GraphCommandStack stack = new();

        Assert.True(stack.Execute(document, new DeleteNodeCommand(sourcePort.NodeId)));
        Assert.Null(document.TryGetNode(sourcePort.NodeId));
        Assert.Empty(document.Edges);

        Assert.True(stack.Undo(document));
        Assert.NotNull(document.TryGetNode(sourcePort.NodeId));
        Assert.Equal(edge.Id, Assert.Single(document.Edges).Id);
    }

    private static GraphDocument CreateConnectionDocument(out GraphPortModel sourcePort, out GraphPortModel targetPort)
    {
        GraphDocument document = new();
        GraphNodeModel source = document.AddNode(Guid.NewGuid(), "Start", "Start", Vector2.Zero);
        GraphNodeModel target = document.AddNode(Guid.NewGuid(), "Line", "Line", Vector2.Zero);
        sourcePort = document.AddPort(source.Id, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple, false);
        targetPort = document.AddPort(target.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, false);
        return document;
    }
}