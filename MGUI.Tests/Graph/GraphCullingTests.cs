using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Graph;

public class GraphCullingTests
{
    [Fact]
    public void GraphCulling_WorldViewportUsesTransformAndPadding()
    {
        GraphViewportTransform viewport = new()
        {
            Zoom = 2.0f,
            Pan = new Vector2(10, 20),
        };
        GraphCullingService service = new();

        RectangleF worldViewport = service.CreateWorldViewport(viewport, new Rectangle(10, 20, 200, 100), padding: 5);

        Assert.Equal(-5, worldViewport.Left);
        Assert.Equal(-5, worldViewport.Top);
        Assert.Equal(110, worldViewport.Width);
        Assert.Equal(60, worldViewport.Height);
    }

    [Fact]
    public void GraphCulling_FiltersNodesCommentsAndEdgesByWorldViewport()
    {
        GraphDocument document = new();
        GraphCullingService service = new();
        RectangleF viewport = new(0, 0, 400, 300);
        GraphNodeModel visibleNode = document.AddNode(Guid.NewGuid(), "Value", "Visible", new Vector2(20, 30));
        GraphNodeModel farNode = document.AddNode(Guid.NewGuid(), "Value", "Far", new Vector2(5000, 5000));
        GraphCommentModel visibleComment = document.AddComment(Guid.NewGuid(), new Rectangle(100, 100, 160, 80), "Visible", "");
        GraphCommentModel farComment = document.AddComment(Guid.NewGuid(), new Rectangle(6000, 6000, 160, 80), "Far", "");
        GraphPortModel visibleOut = document.AddPort(visibleNode.Id, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Exec);
        GraphPortModel visibleIn = document.AddPort(farNode.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec);
        GraphEdgeModel farEdge = document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), visibleNode.Id, visibleOut.Id, farNode.Id, visibleIn.Id), validate: false);

        Assert.True(service.IsNodeVisible(visibleNode, viewport));
        Assert.False(service.IsNodeVisible(farNode, viewport));
        Assert.True(service.IsCommentVisible(visibleComment, viewport));
        Assert.False(service.IsCommentVisible(farComment, viewport));
        Assert.True(service.ShouldDrawEdge(document, farEdge, viewport, selectedEdgeIds: null));

        RectangleF tinyViewport = new(-1000, -1000, 100, 100);
        Assert.False(service.ShouldDrawEdge(document, farEdge, tinyViewport, selectedEdgeIds: null));
        Assert.True(service.ShouldDrawEdge(document, farEdge, tinyViewport, new HashSet<Guid> { farEdge.Id }));
    }

    [Fact]
    public void GraphCulling_UsesResolvedNodeWorldSizeForPortAnchors()
    {
        GraphDocument document = new();
        GraphCullingService service = new();
        GraphNodeModel node = document.AddNode(Guid.NewGuid(), "Value", "AutoSized", new Vector2(10, 15));
        GraphSelectionManager.SetAutoMeasuredWorldSize(node, new Vector2(280, 120));
        GraphPortModel port = document.AddPort(node.Id, Guid.NewGuid(), "Out", GraphPortDirection.Output, GraphValueType.Float);

        Assert.True(service.TryGetPortWorldAnchor(document, port.Id, out Vector2 anchor));
        Assert.Equal(new Vector2(290, 51), anchor);
    }

    [Fact]
    public void GraphCulling_AlignsInputAndOutputAnchorsByDirectionalIndex()
    {
        GraphDocument document = new();
        GraphCullingService service = new();
        GraphNodeModel node = document.AddNode(Guid.NewGuid(), "Choice", "Ask", new Vector2(100, 50));
        GraphSelectionManager.SetAutoMeasuredWorldSize(node, new Vector2(220, 120));
        GraphPortModel inputA = document.AddPort(node.Id, Guid.NewGuid(), "In", GraphPortDirection.Input, GraphValueType.Exec);
        GraphPortModel inputB = document.AddPort(node.Id, Guid.NewGuid(), "Condition", GraphPortDirection.Input, GraphValueType.Bool);
        GraphPortModel outputA = document.AddPort(node.Id, Guid.NewGuid(), "Then", GraphPortDirection.Output, GraphValueType.Exec);
        GraphPortModel outputB = document.AddPort(node.Id, Guid.NewGuid(), "Result", GraphPortDirection.Output, GraphValueType.String);

        Assert.True(service.TryGetPortWorldAnchor(document, inputA.Id, out Vector2 inputAAnchor));
        Assert.True(service.TryGetPortWorldAnchor(document, outputA.Id, out Vector2 outputAAnchor));
        Assert.True(service.TryGetPortWorldAnchor(document, inputB.Id, out Vector2 inputBAnchor));
        Assert.True(service.TryGetPortWorldAnchor(document, outputB.Id, out Vector2 outputBAnchor));

        Assert.Equal(inputAAnchor.Y, outputAAnchor.Y);
        Assert.Equal(inputBAnchor.Y, outputBAnchor.Y);
        Assert.Equal(100, inputAAnchor.X);
        Assert.Equal(320, outputAAnchor.X);
    }

    [Fact]
    public void GraphCulling_GraphViewDoesNotCreateOffscreenNodesUntilTheyEnterViewport()
    {
        MGGraphView graphView = CreateGraphView();
        GraphDocument document = graphView.Document;
        Guid visibleNodeId = document.AddNode(Guid.NewGuid(), "Value", "Visible", new Vector2(20, 20)).Id;
        Guid farNodeId = document.AddNode(Guid.NewGuid(), "Value", "Far", new Vector2(5000, 5000)).Id;
        Guid farCommentId = document.AddComment(Guid.NewGuid(), new Rectangle(5100, 5100, 220, 100), "Far", "").Id;
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetNodeControl(visibleNodeId, out MGGraphNode visibleNode));
        Assert.False(graphView.TryGetNodeControl(farNodeId, out _));
        Assert.False(graphView.TryGetCommentControl(farCommentId, out _));
        Assert.Equal(1, graphView.CullingDiagnostics.NodesVisible);
        Assert.Equal(1, graphView.CullingDiagnostics.NodesCulled);
        Assert.Equal(1, graphView.CullingDiagnostics.CommentsCulled);

        graphView.ViewportTransform.Pan = new Vector2(-5000, -5000);
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetNodeControl(farNodeId, out MGGraphNode farNode));
        Assert.True(graphView.TryGetCommentControl(farCommentId, out MGGraphCommentBox farComment));
        Assert.Equal(Visibility.Collapsed, visibleNode.Visibility);
        Assert.Equal(Visibility.Visible, farNode.Visibility);
        Assert.Equal(Visibility.Visible, farComment.Visibility);
    }

    [Fact]
    public void GraphCulling_ModerateStressScenarioCountsVisibleNodesWithoutRendering()
    {
        GraphDocument document = new();
        GraphCullingService service = new();
        RectangleF viewport = new(0, 0, 800, 600);
        int visibleCount = 0;

        for (int nodeIndex = 0; nodeIndex < 1000; nodeIndex++)
        {
            int column = nodeIndex % 50;
            int row = nodeIndex / 50;
            GraphNodeModel node = document.AddNode(Guid.NewGuid(), "Value", "Node", new Vector2(column * 220, row * 140));
            if (service.IsNodeVisible(node, viewport))
            {
                visibleCount++;
            }
        }

        Assert.InRange(visibleCount, 1, 999);
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