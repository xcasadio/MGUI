using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphNodePaletteTests
{
    [Fact]
    public void GraphPalette_DefaultDefinitionsAreGenericAndCategorized()
    {
        GraphNodePalette palette = GraphNodePalette.CreateDefault();

        Assert.Contains(palette.Definitions, definition => definition.NodeType == "dialogue/start" && definition.Category == "Dialogue");
        Assert.Contains(palette.Definitions, definition => definition.NodeType == "math/add" && definition.Category == "Math");
        Assert.DoesNotContain(palette.Definitions, definition => definition.NodeType.Contains("Casa", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GraphPalette_FiltersDefinitionsCompatibleWithDraggedOutputPort()
    {
        GraphNodePalette palette = GraphNodePalette.CreateDefault();
        GraphPortModel draggedOutput = new(Guid.NewGuid(), Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Float);

        string[] compatibleNodeTypes = palette.GetDefinitions(draggedOutput).Select(definition => definition.NodeType).ToArray();

        Assert.Contains("math/add", compatibleNodeTypes);
        Assert.DoesNotContain("value/float", compatibleNodeTypes);
        Assert.DoesNotContain("dialogue/line", compatibleNodeTypes);
    }

    [Fact]
    public void GraphPalette_CreateNodeFromDefinitionAddsNodeAtWorldPositionAndSupportsUndo()
    {
        MGGraphView graphView = CreateGraphView();
        GraphNodeDefinition definition = new("custom/float", "Float Node", "Values", new[]
        {
            new GraphPortDefinition("Value", GraphPortDirection.Output, GraphValueType.Float),
        });

        GraphNodeModel node = graphView.CreateNodeFromDefinition(definition, new Vector2(40, 80));

        Assert.NotNull(node);
        Assert.Equal(new Vector2(40, 80), node.Position);
        Assert.Equal("custom/float", node.NodeType);
        Assert.Single(node.Ports);
        Assert.Same(node, graphView.Document.TryGetNode(node.Id));
        Assert.Contains(node.Id, graphView.SelectedNodeIds);
        Assert.True(graphView.Commands.Undo(graphView.Document));
        Assert.Null(graphView.Document.TryGetNode(node.Id));
    }

    [Fact]
    public void GraphPalette_ContextMenuUsesFilteredDefinitionsAndCreationAction()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "Source", Vector2.Zero).Id;
        Guid sourcePortId = document.AddPort(sourceNodeId, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Float).Id;

        MGContextMenu menu = graphView.CreateNodeCreationMenu(new Vector2(100, 120), sourcePortId);
        MGContextMenuButton createAddButton = Assert.Single(menu.Items.OfType<MGContextMenuButton>());

        Assert.Equal("graph.createNode:math/add", createAddButton.CommandId);
        createAddButton.Action(createAddButton);

        GraphNodeModel createdNode = Assert.Single(document.Nodes, node => node.Id != sourceNodeId);
        Assert.Equal(new Vector2(100, 120), createdNode.Position);
        Assert.Single(document.Edges);
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