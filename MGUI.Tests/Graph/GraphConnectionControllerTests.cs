using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphConnectionControllerTests
{
    [Fact]
    public void GraphConnection_CreatesCompatibleOutputToInputEdgeAndSupportsUndoRedo()
    {
        MGGraphView graphView = CreateGraphView();
        (Guid sourceNodeId, Guid sourcePortId, Guid targetNodeId, Guid targetPortId) = AddCompatiblePorts(graphView.Document);

        Assert.True(graphView.ConnectionController.TryCreateConnection(sourcePortId, targetPortId));

        GraphEdgeModel edge = Assert.Single(graphView.Document.Edges);
        Assert.Equal(sourceNodeId, edge.SourceNodeId);
        Assert.Equal(sourcePortId, edge.SourcePortId);
        Assert.Equal(targetNodeId, edge.TargetNodeId);
        Assert.Equal(targetPortId, edge.TargetPortId);
        Assert.True(graphView.Commands.Undo(graphView.Document));
        Assert.Empty(graphView.Document.Edges);
        Assert.True(graphView.Commands.Redo(graphView.Document));
        Assert.Single(graphView.Document.Edges);
    }

    [Fact]
    public void GraphConnection_NormalizesInputToOutputDrag()
    {
        MGGraphView graphView = CreateGraphView();
        (Guid sourceNodeId, Guid sourcePortId, Guid targetNodeId, Guid targetPortId) = AddCompatiblePorts(graphView.Document);

        Assert.True(graphView.ConnectionController.TryCreateConnection(targetPortId, sourcePortId));

        GraphEdgeModel edge = Assert.Single(graphView.Document.Edges);
        Assert.Equal(sourceNodeId, edge.SourceNodeId);
        Assert.Equal(sourcePortId, edge.SourcePortId);
        Assert.Equal(targetNodeId, edge.TargetNodeId);
        Assert.Equal(targetPortId, edge.TargetPortId);
    }

    [Fact]
    public void GraphConnection_IncompatiblePortsDoNotAddEdgeAndExposeDiagnostic()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "Source", Vector2.Zero).Id;
        Guid targetNodeId = document.AddNode(Guid.NewGuid(), "Value", "Target", new Vector2(200, 0)).Id;
        Guid sourcePortId = document.AddPort(sourceNodeId, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.String).Id;
        Guid targetPortId = document.AddPort(targetNodeId, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Float).Id;

        Assert.False(graphView.ConnectionController.TryCreateConnection(sourcePortId, targetPortId));

        Assert.Empty(document.Edges);
        Assert.Equal(GraphTypeCompatibilityService.IncompatibleTypes, graphView.ConnectionController.PreviewValidationResult.Code);
        Assert.False(string.IsNullOrWhiteSpace(graphView.ConnectionController.LastDiagnostic));
    }

    [Fact]
    public void GraphConnection_SingleCardinalityRefusesSecondConnection()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid firstSourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "SourceA", Vector2.Zero).Id;
        Guid secondSourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "SourceB", new Vector2(0, 100)).Id;
        Guid targetNodeId = document.AddNode(Guid.NewGuid(), "Value", "Target", new Vector2(200, 0)).Id;
        Guid firstSourcePortId = document.AddPort(firstSourceNodeId, Guid.NewGuid(), "OutA", GraphPortDirection.Output, GraphValueType.Float).Id;
        Guid secondSourcePortId = document.AddPort(secondSourceNodeId, Guid.NewGuid(), "OutB", GraphPortDirection.Output, GraphValueType.Float).Id;
        Guid targetPortId = document.AddPort(targetNodeId, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Float, GraphPortCardinality.Single).Id;

        Assert.True(graphView.ConnectionController.TryCreateConnection(firstSourcePortId, targetPortId));
        Assert.False(graphView.ConnectionController.TryCreateConnection(secondSourcePortId, targetPortId));

        Assert.Single(document.Edges);
        Assert.Equal(GraphTypeCompatibilityService.TargetCardinalitySingle, graphView.ConnectionController.PreviewValidationResult.Code);
    }

    [Fact]
    public void GraphConnection_CancelDragLeavesDocumentUnchangedAndClearsPortFeedback()
    {
        MGGraphView graphView = CreateGraphView();
        (_, Guid sourcePortId, _, Guid targetPortId) = AddCompatiblePorts(graphView.Document);
        Assert.True(graphView.TryGetPortControl(sourcePortId, out MGGraphPort sourcePort));
        Assert.True(graphView.TryGetPortControl(targetPortId, out MGGraphPort targetPort));

        Assert.True(graphView.ConnectionController.BeginDrag(sourcePortId, new Vector2(10, 10)));
        graphView.ConnectionController.UpdateDrag(new Vector2(40, 40), targetPortId);
        graphView.ConnectionController.Cancel();

        Assert.Empty(graphView.Document.Edges);
        Assert.False(sourcePort.IsConnectionDragSource);
        Assert.False(targetPort.IsConnectionDragTarget);
    }

    [Fact]
    public void GraphConnection_DeleteSelectedEdgesUsesDisconnectCommand()
    {
        MGGraphView graphView = CreateGraphView();
        (_, Guid sourcePortId, _, Guid targetPortId) = AddCompatiblePorts(graphView.Document);
        Assert.True(graphView.ConnectionController.TryCreateConnection(sourcePortId, targetPortId));
        Guid edgeId = Assert.Single(graphView.Document.Edges).Id;
        graphView.SelectedEdgeIds.Add(edgeId);

        Assert.True(graphView.ConnectionController.DeleteSelectedEdges());

        Assert.Empty(graphView.Document.Edges);
        Assert.Empty(graphView.SelectedEdgeIds);
        Assert.True(graphView.Commands.Undo(graphView.Document));
        Assert.Single(graphView.Document.Edges);
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