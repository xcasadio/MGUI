using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Graph;

public class GraphSelectionTests
{
    [Fact]
    public void GraphSelection_SelectNodeSupportsSingleAndToggleSelection()
    {
        GraphSelectionManager selection = new();
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();

        Assert.True(selection.SelectNode(first));
        Assert.True(selection.IsNodeSelected(first));
        Assert.True(selection.SelectNode(second, additive: true));
        Assert.Contains(first, selection.SelectedNodeIds);
        Assert.Contains(second, selection.SelectedNodeIds);
        Assert.True(selection.SelectNode(first, additive: true, toggle: true));
        Assert.DoesNotContain(first, selection.SelectedNodeIds);
        Assert.Contains(second, selection.SelectedNodeIds);
    }

    [Fact]
    public void GraphSelection_RectangleSelectsOnlyIntersectingNodes()
    {
        GraphDocument document = new();
        Guid inside = Guid.NewGuid();
        Guid outside = Guid.NewGuid();
        GraphNodeModel insideNode = document.AddNode(inside, "Value", "Inside", new Vector2(10, 20));
        GraphNodeModel outsideNode = document.AddNode(outside, "Value", "Outside", new Vector2(300, 300));
        insideNode.Size = new Vector2(80, 40);
        outsideNode.Size = new Vector2(80, 40);
        GraphSelectionManager selection = new();

        Assert.True(selection.SelectNodesInRectangle(document, new RectangleF(0, 0, 120, 120)));

        Assert.Contains(inside, selection.SelectedNodeIds);
        Assert.DoesNotContain(outside, selection.SelectedNodeIds);
    }

    [Fact]
    public void GraphSelection_GraphViewSelectionUpdatesVisibleNodeState()
    {
        MGGraphView graphView = CreateGraphView();
        Guid nodeId = Guid.NewGuid();
        graphView.Document.AddNode(nodeId, "Value", "Value", Vector2.Zero);

        Assert.True(graphView.SelectNode(nodeId));

        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode node));
        Assert.True(node.IsSelected);
        Assert.Contains(nodeId, graphView.SelectedNodeIds);
    }

    [Fact]
    public void GraphSelection_MoveSelectedNodesUsesSingleUndoableCommand()
    {
        MGGraphView graphView = CreateGraphView();
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        GraphNodeModel first = graphView.Document.AddNode(firstId, "Value", "First", new Vector2(10, 20));
        GraphNodeModel second = graphView.Document.AddNode(secondId, "Value", "Second", new Vector2(50, 70));
        graphView.SelectNode(firstId);
        graphView.SelectNode(secondId, additive: true);

        Assert.True(graphView.MoveSelectedNodesBy(new Vector2(16, -8)));

        Assert.Equal(new Vector2(26, 12), first.Position);
        Assert.Equal(new Vector2(66, 62), second.Position);
        Assert.Equal(1, graphView.Commands.UndoCount);
        Assert.True(graphView.Commands.Undo(graphView.Document));
        Assert.Equal(new Vector2(10, 20), first.Position);
        Assert.Equal(new Vector2(50, 70), second.Position);
    }

    [Fact]
    public void GraphSelection_MoveSelectedNodesAppliesGridSnapWhenEnabled()
    {
        MGGraphView graphView = CreateGraphView();
        Guid nodeId = Guid.NewGuid();
        GraphNodeModel node = graphView.Document.AddNode(nodeId, "Value", "Value", new Vector2(10, 10));
        graphView.ViewportTransform.GridSize = 16;
        graphView.SnapToGrid = true;
        graphView.SelectNode(nodeId);

        Assert.True(graphView.MoveSelectedNodesBy(new Vector2(9, 9)));

        Assert.Equal(new Vector2(16, 16), node.Position);
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