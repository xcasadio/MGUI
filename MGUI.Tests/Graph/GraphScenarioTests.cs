using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Graph;

public class GraphScenarioTests
{
    [Fact]
    public void Scenario_CreateConnectMoveUndoRedoSerializeDeserializeAndValidate()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        GraphNodeModel start = CreateStartNode(out GraphPortModel startOut);
        GraphNodeModel line = CreateLineNode(out GraphPortModel lineIn, out _);
        GraphEdgeModel edge = new(Guid.NewGuid(), start.Id, startOut.Id, line.Id, lineIn.Id);
        Vector2 initialLinePosition = line.Position;
        Vector2 movedLinePosition = new(420, 96);

        Assert.True(graphView.Commands.Execute(document, new CreateNodeCommand(start)));
        Assert.True(graphView.Commands.Execute(document, new CreateNodeCommand(line)));
        Assert.True(graphView.Commands.Execute(document, new ConnectPortsCommand(edge)));
        Assert.True(graphView.Commands.Execute(document, new MoveNodeCommand(line.Id, initialLinePosition, movedLinePosition)));
        Assert.True(graphView.FrameAll(new Rectangle(0, 0, 640, 360)));

        Assert.Equal(movedLinePosition, document.TryGetNode(line.Id)!.Position);
        Assert.True(new GraphDocumentValidator().Validate(document).IsValid);

        Assert.True(graphView.Commands.Undo(document));
        Assert.Equal(initialLinePosition, document.TryGetNode(line.Id)!.Position);

        Assert.True(graphView.Commands.Redo(document));
        Assert.Equal(movedLinePosition, document.TryGetNode(line.Id)!.Position);

        GraphSerializer serializer = new();
        GraphSerializationResult serialized = serializer.Serialize(document);
        GraphSerializationResult deserialized = serializer.Deserialize(serialized.Json);

        Assert.True(serialized.Success);
        Assert.True(deserialized.Success);
        GraphDocument roundTrip = deserialized.Document;
        GraphValidationResult validation = new GraphDocumentValidator().Validate(roundTrip);
        Assert.True(validation.IsValid);
        Assert.Equal(2, roundTrip.Nodes.Count);
        Assert.Single(roundTrip.Edges);
        Assert.Equal(movedLinePosition, roundTrip.TryGetNode(line.Id)!.Position);

        graphView.Document = roundTrip;
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetNodeControl(start.Id, out _));
        Assert.True(graphView.TryGetNodeControl(line.Id, out _));
        Assert.True(graphView.TryGetPortControl(startOut.Id, out MGGraphPort startOutControl));
        Assert.True(startOutControl.IsConnected);
    }

    [Fact]
    public void Scenario_DeleteConnectedNodeRemovesEdgesAndUndoRestoresGraph()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        AddConnectedPair(document, out GraphNodeModel start, out GraphNodeModel line, out GraphEdgeModel edge);
        graphView.SelectedNodeIds.Add(start.Id);

        Assert.True(graphView.DeleteSelection());

        Assert.Null(document.TryGetNode(start.Id));
        Assert.NotNull(document.TryGetNode(line.Id));
        Assert.Empty(document.Edges);
        Assert.Empty(graphView.SelectedNodeIds);

        Assert.True(graphView.Commands.Undo(document));

        Assert.NotNull(document.TryGetNode(start.Id));
        Assert.NotNull(document.TryGetNode(line.Id));
        Assert.Equal(edge.Id, Assert.Single(document.Edges).Id);
        Assert.True(new GraphDocumentValidator().Validate(document).IsValid);
    }

    [Fact]
    public void Scenario_LoadInvalidEdgeDocumentAndReportValidationIssue()
    {
        GraphDocument document = new();
        GraphNodeModel source = document.AddNode(Guid.NewGuid(), "dialogue/start", "Start", Vector2.Zero);
        GraphPortModel sourceOut = document.AddPort(source.Id, Guid.NewGuid(), "Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple);
        GraphEdgeModel invalidEdge = new(Guid.NewGuid(), source.Id, sourceOut.Id, Guid.NewGuid(), Guid.NewGuid());
        document.AddEdge(invalidEdge, validate: false);
        GraphSerializer serializer = new();

        GraphSerializationResult loaded = serializer.Deserialize(serializer.Serialize(document).Json);

        Assert.True(loaded.Success);
        GraphValidationResult validation = new GraphDocumentValidator().Validate(loaded.Document);
        Assert.False(validation.IsValid);
        GraphValidationIssue issue = Assert.Single(validation.Issues);
        Assert.Equal(GraphTypeCompatibilityService.MissingTargetNode, issue.Code);
        Assert.Equal(invalidEdge.Id, issue.EdgeId);
    }

    [Fact]
    public void Scenario_MinimalThemeAndTemplateAttachRequiredGraphParts()
    {
        MGResources resources = new(new MGTheme("Arial"));

        MGControlTemplateCatalog.RegisterDefaults(resources);

        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.GraphViewTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.GraphNodeTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.GraphPortTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.GraphCommentBoxTemplateName, out _));
        Assert.NotNull(resources.DefaultTheme.Graph.CanvasBackground.GetUnderlay(PrimaryVisualState.Normal));
        Assert.NotNull(resources.DefaultTheme.Graph.EdgeBrush);

        MGWindow window = CreateWindow(out MGDesktop desktop);
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""320"" Height=""180"" WindowStyle=""None"">
  <GraphView Name=""Graph"" ShowGrid=""True"" AllowPan=""True"" AllowZoom=""True"" SnapToGrid=""True"" />
</Window>";

        MGWindow loadedWindow = MGUIXamlParser.LoadRootWindow(desktop, xaml, false, true);
        MGGraphView graphView = loadedWindow.GetElementByName<MGGraphView>("Graph");

        Assert.NotNull(window);
        Assert.True(graphView.ShowGrid);
        Assert.True(graphView.AllowPan);
        Assert.True(graphView.AllowZoom);
        Assert.True(graphView.SnapToGrid);
        Assert.Same(graphView.OuterBorder, graphView.TemplateParts[MGGraphView.OuterBorderPartName]);
        Assert.Same(graphView.ViewportHost, graphView.TemplateParts[MGGraphView.ViewportHostPartName]);
        Assert.Same(graphView.NodesCanvas, graphView.TemplateParts[MGGraphView.NodesCanvasPartName]);
        Assert.Same(graphView.OverlayPanel, graphView.TemplateParts[MGGraphView.OverlayPanelPartName]);
    }

    private static void AddConnectedPair(GraphDocument document, out GraphNodeModel start, out GraphNodeModel line, out GraphEdgeModel edge)
    {
        start = document.AddNode(CreateStartNode(out GraphPortModel startOut));
        line = document.AddNode(CreateLineNode(out GraphPortModel lineIn, out _));
        edge = document.Connect(Guid.NewGuid(), start.Id, startOut.Id, line.Id, lineIn.Id);
    }

    private static GraphNodeModel CreateStartNode(out GraphPortModel output)
    {
        GraphNodeModel node = new(Guid.NewGuid(), "dialogue/start", "Start", new Vector2(32, 64));
        output = new GraphPortModel(node.Id, Guid.NewGuid(), "Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple);
        node.Ports.Add(output);
        return node;
    }

    private static GraphNodeModel CreateLineNode(out GraphPortModel input, out GraphPortModel output)
    {
        GraphNodeModel node = new(Guid.NewGuid(), "dialogue/line", "Line", new Vector2(240, 72));
        input = new GraphPortModel(node.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec, GraphPortCardinality.Single, true);
        output = new GraphPortModel(node.Id, Guid.NewGuid(), "Next", GraphPortDirection.Output, GraphValueType.Exec, GraphPortCardinality.Multiple);
        node.Ports.Add(input);
        node.Ports.Add(output);
        return node;
    }

    private static MGGraphView CreateGraphView()
    {
        MGWindow window = CreateWindow(out _);
        return new MGGraphView(window);
    }

    private static MGWindow CreateWindow(out MGDesktop desktop)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        desktop = new(runtime);
        return new MGWindow(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
        };
    }
}