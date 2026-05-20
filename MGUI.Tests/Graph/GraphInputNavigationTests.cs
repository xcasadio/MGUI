using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Graph;

public class GraphInputNavigationTests
{
    [Fact]
    public void GraphInput_ZoomAtViewportPointKeepsWorldPointUnderCursor()
    {
        MGGraphView graphView = CreateGraphView();
        graphView.ViewportTransform.Pan = new Vector2(12, -8);
        graphView.ViewportTransform.Zoom = 1.0f;
        Vector2 viewportPoint = new(160, 90);
        Vector2 worldBefore = graphView.ViewportTransform.ViewportToWorld(viewportPoint);

        Assert.True(graphView.ZoomAtViewportPoint(viewportPoint, 120));

        Vector2 worldAfter = graphView.ViewportTransform.ViewportToWorld(viewportPoint);
        Assert.True(Vector2.Distance(worldBefore, worldAfter) < 0.001f);
    }

    [Fact]
    public void GraphInput_PanDoesNotMutateNodeWorldPositions()
    {
        MGGraphView graphView = CreateGraphView();
        GraphNodeModel node = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Value", new Vector2(40, 72));
        Vector2 positionBefore = node.Position;

        Assert.True(graphView.PanViewportBy(new Vector2(24, -12)));

        Assert.Equal(positionBefore, node.Position);
        Assert.Equal(new Vector2(24, -12), graphView.ViewportTransform.Pan);
    }

    [Fact]
    public void GraphInput_AllowFlagsDisablePanAndZoomCommands()
    {
        MGGraphView graphView = CreateGraphView();
        graphView.AllowPan = false;
        graphView.AllowZoom = false;

        Assert.False(graphView.PanViewportBy(new Vector2(10, 10)));
        Assert.False(graphView.ZoomAtViewportPoint(new Vector2(100, 100), 120));
        Assert.Equal(Vector2.Zero, graphView.ViewportTransform.Pan);
        Assert.Equal(1.0f, graphView.ViewportTransform.Zoom);
    }

    [Fact]
    public void GraphInput_FrameAllUsesDocumentNodeBounds()
    {
        MGGraphView graphView = CreateGraphView();
        GraphNodeModel first = graphView.Document.AddNode(Guid.NewGuid(), "Value", "First", new Vector2(0, 0));
        GraphNodeModel second = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Second", new Vector2(220, 120));
        first.Size = new Vector2(100, 60);
        second.Size = new Vector2(100, 60);

        Assert.True(graphView.FrameAll(new Rectangle(0, 0, 400, 300), 20));

        Vector2 worldCenter = new(160, 90);
        Vector2 viewportCenter = graphView.ViewportTransform.WorldToViewport(worldCenter);
        Assert.True(Vector2.Distance(new Vector2(200, 150), viewportCenter) < 0.01f);
    }

    [Fact]
    public void GraphInput_FrameSelectionUsesSelectedNodesBeforeFullDocument()
    {
        MGGraphView graphView = CreateGraphView();
        GraphNodeModel selected = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Selected", new Vector2(20, 40));
        GraphNodeModel far = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Far", new Vector2(1000, 1000));
        selected.Size = new Vector2(120, 80);
        far.Size = new Vector2(120, 80);
        graphView.SelectedNodeIds.Add(selected.Id);

        Assert.True(graphView.FrameSelection(new Rectangle(0, 0, 500, 300), 30));

        Vector2 selectedCenter = graphView.ViewportTransform.WorldToViewport(new Vector2(80, 80));
        Assert.True(Vector2.Distance(new Vector2(250, 150), selectedCenter) < 0.01f);
    }

    [Fact]
    public void GraphInput_HandleGraphShortcutInvokesFrameCommands()
    {
        MGGraphView graphView = CreateGraphView();
        GraphNodeModel node = graphView.Document.AddNode(Guid.NewGuid(), "Value", "Value", new Vector2(200, 200));
        node.Size = new Vector2(100, 80);
        graphView.ViewportTransform.Pan = new Vector2(25, 35);
        graphView.ViewportTransform.Zoom = 2.0f;

        Assert.True(graphView.HandleGraphShortcut(Keys.Home));
        Assert.Equal(1.0f, graphView.ViewportTransform.Zoom);
        Assert.True(graphView.HandleGraphShortcut(Keys.A));
        Assert.False(graphView.HandleGraphShortcut(Keys.B));
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