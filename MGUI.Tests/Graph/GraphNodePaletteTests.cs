using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
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
        GraphPortModel sourcePort = document.TryGetPort(sourcePortId);
        Assert.NotNull(sourcePort);
        Assert.True(graphView.NodePalette.TryFindCompatiblePort(createdNode, sourcePort, out GraphPortModel compatiblePort));
        Assert.True(GraphPortAnchorResolver.TryGetPortWorldAnchor(createdNode, compatiblePort, out Vector2 compatibleAnchor));
        Assert.Equal(new Vector2(100, 120), compatibleAnchor);
        Assert.True(createdNode.Position.X <= compatibleAnchor.X);
        Assert.True(createdNode.Position.Y < compatibleAnchor.Y);
        Assert.Single(document.Edges);
    }

    [Fact]
    public void GraphPalette_ContextMenuCreationPreservesCompatiblePortAnchorWhenSnapToGridEnabled()
    {
        MGGraphView graphView = CreateGraphView();
        graphView.SnapToGrid = true;
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "Source", Vector2.Zero).Id;
        Guid sourcePortId = document.AddPort(sourceNodeId, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Float).Id;

        MGContextMenu menu = graphView.CreateNodeCreationMenu(new Vector2(103, 119), sourcePortId);
        MGContextMenuButton createAddButton = Assert.Single(menu.Items.OfType<MGContextMenuButton>());

        createAddButton.Action(createAddButton);

        GraphNodeModel createdNode = Assert.Single(document.Nodes, node => node.Id != sourceNodeId);
        GraphPortModel sourcePort = document.TryGetPort(sourcePortId);
        Assert.NotNull(sourcePort);
        Assert.True(graphView.NodePalette.TryFindCompatiblePort(createdNode, sourcePort, out GraphPortModel compatiblePort));
        Assert.True(GraphPortAnchorResolver.TryGetPortWorldAnchor(createdNode, compatiblePort, out Vector2 compatibleAnchor));
        Assert.Equal(new Vector2(103, 119), compatibleAnchor);
    }

    [Fact]
    public void GraphPalette_ScreenToWorldConversionAccountsForViewportOffsetDuringContextMenuCreation()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new MonoGame.Extended.Thickness(0),
        };
        MGCanvas root = new(window);
        MGButton toolbarButton = new(window)
        {
            PreferredWidth = 120,
            PreferredHeight = 48,
        };
        MGGraphView graphView = new(window)
        {
            PreferredWidth = 640,
            PreferredHeight = 280,
        };

        window.SetContent(root);
        desktop.Windows.Add(window);
        using (root.AllowChangingContentTemporarily())
        {
            root.TryAddChild(toolbarButton);
            root.TryAddChild(graphView);
        }

        MGCanvas.SetLeft(toolbarButton, 0);
        MGCanvas.SetTop(toolbarButton, 0);
        MGCanvas.SetLeft(graphView, 0);
        MGCanvas.SetTop(graphView, 72);

        GraphDocument document = graphView.Document;
        Guid sourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "Source", Vector2.Zero).Id;
        Guid sourcePortId = document.AddPort(sourceNodeId, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Float).Id;

        desktop.Update();
        graphView.SynchronizeDocument();
        desktop.Update();

        Point screenPosition = new(graphView.NodesCanvas.AlignedContentBounds.Left + 103, graphView.NodesCanvas.AlignedContentBounds.Top + 119);
        Vector2 worldPosition = graphView.GetWorldPointFromScreenPosition(screenPosition);

        Assert.Equal(new Vector2(103, 119), worldPosition);

        MGContextMenu menu = graphView.CreateNodeCreationMenu(worldPosition, sourcePortId);
        MGContextMenuButton createAddButton = Assert.Single(menu.Items.OfType<MGContextMenuButton>());

        createAddButton.Action(createAddButton);

        GraphNodeModel createdNode = Assert.Single(document.Nodes, node => node.Id != sourceNodeId);
        GraphPortModel sourcePort = document.TryGetPort(sourcePortId);
        Assert.NotNull(sourcePort);
        Assert.True(graphView.NodePalette.TryFindCompatiblePort(createdNode, sourcePort, out GraphPortModel compatiblePort));
        Assert.True(GraphPortAnchorResolver.TryGetPortWorldAnchor(createdNode, compatiblePort, out Vector2 compatibleAnchor));
        Assert.Equal(new Vector2(103, 119), compatibleAnchor);
    }

    [Fact]
    public void GraphPalette_GraphContextMenuExposesCopyPasteSeparatelyFromDraggedLinkCreationMenu()
    {
        MGGraphView graphView = CreateGraphView();
        string clipboardText = string.Empty;
        graphView.ClipboardTextReader = () => clipboardText;
        graphView.ClipboardTextWriter = value => clipboardText = value;
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = document.AddNode(Guid.NewGuid(), "Value", "Source", Vector2.Zero).Id;
        Guid sourcePortId = document.AddPort(sourceNodeId, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Float).Id;

        graphView.SelectNode(sourceNodeId);
        Assert.True(graphView.CopySelectionToClipboard());

        MGContextMenu graphMenu = graphView.CreateGraphContextMenu(new Vector2(100, 120));
        MGContextMenuButton[] graphButtons = graphMenu.Items.OfType<MGContextMenuButton>().ToArray();

        Assert.Contains(graphButtons, button => button.CommandId == "graph.copy");
        Assert.Contains(graphButtons, button => button.CommandId == "graph.paste");

        MGContextMenu dragMenu = graphView.CreateNodeCreationMenu(new Vector2(100, 120), sourcePortId);
        MGContextMenuButton[] dragButtons = dragMenu.Items.OfType<MGContextMenuButton>().ToArray();

        Assert.DoesNotContain(dragButtons, button => button.CommandId == "graph.copy");
        Assert.DoesNotContain(dragButtons, button => button.CommandId == "graph.paste");
        Assert.Contains(dragButtons, button => button.CommandId == "graph.createNode:math/add");
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