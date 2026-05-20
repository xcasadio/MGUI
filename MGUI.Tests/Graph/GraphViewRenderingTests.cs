using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Graph;

public class GraphViewRenderingTests
{
    [Fact]
    public void GraphView_DrawsGridThroughRenderTransaction()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out GraphTestRuntime runtime);
        graphView.ShowGrid = true;
        graphView.GridLineBrush = new MGSolidFillBrush(Color.LimeGreen * 0.45f);
        graphView.ViewportTransform.GridSize = 16.0f;
        graphView.ViewportTransform.Pan = new Vector2(5, 7);
        graphView.ViewportTransform.Zoom = 1.25f;

        desktop.Update();
        using GraphNoOpDrawTransaction transaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(transaction, 1.0f);

        Assert.Contains(transaction.StrokeLineCalls, call => call.Color == Color.Lerp(Color.LimeGreen * 0.45f, Color.White, 0.22f) || call.Color == Color.LimeGreen * 0.45f);
        Assert.True(transaction.StrokeLineCalls.Count > 8);
    }

    [Fact]
    public void GraphView_DrawsEdgesBehindNodesUsingGeometryCache()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out GraphTestRuntime runtime);
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = Guid.NewGuid();
        Guid targetNodeId = Guid.NewGuid();
        Guid sourcePortId = Guid.NewGuid();
        Guid targetPortId = Guid.NewGuid();
        graphView.ShowGrid = false;
        graphView.EdgeBrush = new MGSolidFillBrush(Color.HotPink);
        graphView.EdgeThickness = 3.0f;

        GraphNodeModel sourceNode = document.AddNode(sourceNodeId, "Value", "Source", new Vector2(20, 40));
        GraphNodeModel targetNode = document.AddNode(targetNodeId, "Value", "Target", new Vector2(260, 120));
        sourceNode.Size = new Vector2(140, 80);
        targetNode.Size = new Vector2(140, 80);
        document.AddPort(sourceNodeId, sourcePortId, "Out", GraphPortDirection.Output, GraphValueType.Float);
        document.AddPort(targetNodeId, targetPortId, "In", GraphPortDirection.Input, GraphValueType.Float);
        document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);

        desktop.Update();
        using GraphNoOpDrawTransaction firstTransaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(firstTransaction, 1.0f);
        using GraphNoOpDrawTransaction secondTransaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(secondTransaction, 1.0f);

        Assert.True(firstTransaction.StrokeLineCalls.Count >= GraphBezierGeometry.DefaultSegmentCount);
        Assert.All(firstTransaction.StrokeLineCalls, call => Assert.Equal(Color.HotPink, call.Color));
        Assert.All(firstTransaction.StrokeLineCalls, call => Assert.Equal(3.0f, call.Thickness));
        Assert.True(graphView.EdgeGeometryCache.CacheMisses >= 1);
        Assert.True(graphView.EdgeGeometryCache.CacheHits >= 1);
    }

    private static MGGraphView CreateGraphView(out MGDesktop desktop, out GraphTestRuntime runtime)
    {
        runtime = new(new Rectangle(0, 0, 800, 600));
        desktop = new MGDesktop(runtime);
        MGWindow window = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new MonoGame.Extended.Thickness(0),
        };
        MGGraphView graphView = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        window.SetContent(graphView);
        desktop.Windows.Add(window);
        return graphView;
    }
}