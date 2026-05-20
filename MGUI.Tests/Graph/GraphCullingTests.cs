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