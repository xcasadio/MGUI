using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;
using System.Linq;
using Xunit;

namespace MGUI.Tests.Graph;

public class GraphViewRenderingTests
{
    [Fact]
    public void GraphView_DrawsGridThroughRenderTransaction()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out GraphTestRuntime runtime);
        graphView.ShowGrid = true;
        graphView.GridLineBrush = new MGSolidFillBrush(Color.LimeGreen * 0.45f);
        graphView.MajorGridLineBrush = new MGSolidFillBrush(Color.DarkGreen * 0.8f);
        graphView.ViewportTransform.GridSize = 16.0f;
        graphView.ViewportTransform.Pan = new Vector2(5, 7);
        graphView.ViewportTransform.Zoom = 1.25f;

        desktop.Update();
        using GraphNoOpDrawTransaction transaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(transaction, 1.0f);

        Assert.Contains(transaction.StrokeLineCalls, call => call.Color == Color.LimeGreen * 0.45f);
        Assert.Contains(transaction.StrokeLineCalls, call => call.Color == Color.DarkGreen * 0.8f);
        Assert.True(transaction.StrokeLineCalls.Count > 8);
    }

    [Fact]
    public void GraphView_RefreshesGraphThemeWhenWindowThemeChanges()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out _);
        MGWindow window = graphView.SelfOrParentWindow;
        MGTheme darkBlueTheme = CreateGraphTheme(MGTheme.BuiltInTheme.Dark_Blue, desktop.DefaultFontFamily,
            new Color(95, 100, 109), new Color(181, 189, 201) * 0.28f, new Color(74, 79, 88) * 0.84f);
        MGTheme darkTheme = CreateGraphTheme(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily,
            new Color(47, 47, 47), new Color(112, 112, 112) * 0.34f, new Color(29, 29, 29) * 0.82f);

        window.Theme = darkBlueTheme;
        desktop.Update();

        Assert.Equal(((MGSolidFillBrush)darkBlueTheme.Graph.GridLineBrush).Color, ((MGSolidFillBrush)graphView.GridLineBrush).Color);
        Assert.Equal(((MGSolidFillBrush)darkBlueTheme.Graph.MajorGridLineBrush).Color, ((MGSolidFillBrush)graphView.MajorGridLineBrush).Color);
        Assert.Equal(((MGSolidFillBrush)darkBlueTheme.Graph.CanvasBackground.NormalValue).Color, ((MGSolidFillBrush)graphView.NodesCanvas.BackgroundBrush.NormalValue).Color);

        window.Theme = darkTheme;
        desktop.Update();

        Assert.Equal(((MGSolidFillBrush)darkTheme.Graph.GridLineBrush).Color, ((MGSolidFillBrush)graphView.GridLineBrush).Color);
        Assert.Equal(((MGSolidFillBrush)darkTheme.Graph.MajorGridLineBrush).Color, ((MGSolidFillBrush)graphView.MajorGridLineBrush).Color);
        Assert.Equal(((MGSolidFillBrush)darkTheme.Graph.CanvasBackground.NormalValue).Color, ((MGSolidFillBrush)graphView.NodesCanvas.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void GraphView_RefreshesNodeThemeWhenWindowThemeChanges()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out _);
        MGWindow window = graphView.SelfOrParentWindow;
        MGTheme darkBlueTheme = CreateGraphTheme(MGTheme.BuiltInTheme.Dark_Blue, desktop.DefaultFontFamily,
            new Color(95, 100, 109), new Color(181, 189, 201) * 0.28f, new Color(74, 79, 88) * 0.84f,
            new Color(79, 87, 101), new Color(64, 73, 88));
        MGTheme darkTheme = CreateGraphTheme(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily,
            new Color(47, 47, 47), new Color(112, 112, 112) * 0.34f, new Color(29, 29, 29) * 0.82f,
            new Color(36, 36, 36), new Color(28, 28, 28));
        Guid nodeId = Guid.NewGuid();

        window.Theme = darkBlueTheme;
        graphView.Document.AddNode(nodeId, "Value", "Theme Node", Vector2.Zero);
        desktop.Update();
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode node));
        Assert.Equal(((MGSolidFillBrush)darkBlueTheme.Graph.NodeBodyBackground.NormalValue).Color, ((MGSolidFillBrush)node.OuterBorder.BackgroundBrush.NormalValue).Color);
        Assert.Equal(((MGSolidFillBrush)darkBlueTheme.Graph.NodeHeaderBackground.NormalValue).Color, ((MGSolidFillBrush)node.HeaderTextBlock.BackgroundBrush.NormalValue).Color);

        window.Theme = darkTheme;
        desktop.Update();

        Assert.Equal(((MGSolidFillBrush)darkTheme.Graph.NodeBodyBackground.NormalValue).Color, ((MGSolidFillBrush)node.OuterBorder.BackgroundBrush.NormalValue).Color);
        Assert.Equal(((MGSolidFillBrush)darkTheme.Graph.NodeHeaderBackground.NormalValue).Color, ((MGSolidFillBrush)node.HeaderTextBlock.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void GraphView_RefreshesExistingNodesWhenThemeChangesAfterInitialSynchronization()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out _);
        MGWindow window = graphView.SelfOrParentWindow;
        Guid nodeId = Guid.NewGuid();
        MGTheme darkTheme = CreateGraphTheme(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily,
            new Color(47, 47, 47), new Color(112, 112, 112) * 0.34f, new Color(29, 29, 29) * 0.82f,
            new Color(36, 36, 36), new Color(28, 28, 28));

        graphView.Document.AddNode(nodeId, "Value", "Existing Node", Vector2.Zero);
        desktop.Update();
        graphView.SynchronizeDocument();

        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode nodeBefore));

        window.Theme = darkTheme;
        desktop.Update();

        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode nodeAfter));
        Assert.Same(nodeBefore, nodeAfter);
        Assert.Equal(((MGSolidFillBrush)darkTheme.Graph.NodeBodyBackground.NormalValue).Color, ((MGSolidFillBrush)nodeAfter.OuterBorder.BackgroundBrush.NormalValue).Color);
        Assert.Equal(((MGSolidFillBrush)darkTheme.Graph.NodeHeaderBackground.NormalValue).Color, ((MGSolidFillBrush)nodeAfter.HeaderTextBlock.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void GraphView_RenderOutputChangesColorWhenWindowThemeChanges()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out GraphTestRuntime runtime);
        MGWindow window = graphView.SelfOrParentWindow;
        Guid sourceNodeId = Guid.NewGuid();
        Guid targetNodeId = Guid.NewGuid();
        Guid sourcePortId = Guid.NewGuid();
        Guid targetPortId = Guid.NewGuid();
        Color darkBlueCanvas = new(95, 100, 109);
        Color darkBlueMinor = new Color(181, 189, 201) * 0.28f;
        Color darkBlueMajor = new Color(74, 79, 88) * 0.84f;
        Color darkBlueEdge = new(150, 206, 255);
        Color darkBluePort = new(112, 146, 203, 220);
        Color darkCanvas = new(47, 47, 47);
        Color darkMinor = new Color(112, 112, 112) * 0.34f;
        Color darkMajor = new Color(29, 29, 29) * 0.82f;
        Color darkEdge = new(145, 186, 255);
        Color darkPort = new(105, 134, 186, 220);
        MGTheme darkBlueTheme = CreateGraphTheme(MGTheme.BuiltInTheme.Dark_Blue, desktop.DefaultFontFamily,
            darkBlueCanvas, darkBlueMinor, darkBlueMajor, new Color(73, 83, 97, 214), new Color(56, 66, 82, 236), darkBlueEdge, darkBluePort);
        MGTheme darkTheme = CreateGraphTheme(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily,
            darkCanvas, darkMinor, darkMajor, new Color(36, 36, 36, 214), new Color(28, 28, 28, 236), darkEdge, darkPort);

        GraphNodeModel sourceNode = graphView.Document.AddNode(sourceNodeId, "Value", "Source", new Vector2(20, 40));
        GraphNodeModel targetNode = graphView.Document.AddNode(targetNodeId, "Value", "Target", new Vector2(260, 120));
        sourceNode.Size = new Vector2(140, 80);
        targetNode.Size = new Vector2(140, 80);
        graphView.Document.AddPort(sourceNodeId, sourcePortId, "Speaker", GraphPortDirection.Output, GraphValueType.String);
        graphView.Document.AddPort(targetNodeId, targetPortId, "In", GraphPortDirection.Input, GraphValueType.String);
        graphView.Document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);

        window.Theme = darkBlueTheme;
        desktop.Update();
        graphView.SynchronizeDocument();
        desktop.Update();
        GraphRenderColorSnapshot darkBlueSnapshot = CaptureRenderColors(desktop, runtime);

        window.Theme = darkTheme;
        desktop.Update();
        graphView.SynchronizeDocument();
        desktop.Update();
        GraphRenderColorSnapshot darkSnapshot = CaptureRenderColors(desktop, runtime);

        Assert.Contains(darkBlueCanvas, darkBlueSnapshot.FillColors);
        Assert.Contains(darkCanvas, darkSnapshot.FillColors);
        Assert.Contains(darkBlueMinor, darkBlueSnapshot.LineColors);
        Assert.Contains(darkBlueMajor, darkBlueSnapshot.LineColors);
        Assert.Contains(darkBlueEdge, darkBlueSnapshot.LineColors);
        Assert.Contains(darkMinor, darkSnapshot.LineColors);
        Assert.Contains(darkMajor, darkSnapshot.LineColors);
        Assert.Contains(darkEdge, darkSnapshot.LineColors);
        Assert.Contains(darkBluePort, darkBlueSnapshot.FillColors);
        Assert.Contains(darkPort, darkSnapshot.FillColors);
        Assert.NotEqual(darkBlueSnapshot, darkSnapshot);
    }

    [Fact]
    public void GraphView_StretchesOutputPortsToTheNodeRightEdge()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out _);
        GraphDocument document = graphView.Document;
        Guid nodeId = Guid.NewGuid();
        Guid inputPortId = Guid.NewGuid();
        Guid outputPortId = Guid.NewGuid();

        document.AddNode(nodeId, "Dialogue/Line", "Line: Welcome", Vector2.Zero);
        document.AddPort(nodeId, inputPortId, "In", GraphPortDirection.Input, GraphValueType.Float);
        document.AddPort(nodeId, outputPortId, "Speaker", GraphPortDirection.Output, GraphValueType.String);

        desktop.Update();
        graphView.SynchronizeDocument();
        desktop.Update();

        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode node));
        Assert.True(graphView.TryGetPortControl(inputPortId, out MGGraphPort inputPort));
        Assert.True(graphView.TryGetPortControl(outputPortId, out MGGraphPort outputPort));
        Rectangle inputConnectorSlotBounds = GetUsableBounds(inputPort.LeadingIconPresenter);
        Rectangle inputConnectorBounds = GetUsableBounds(inputPort.LeadingIconPresenter.Content);
        Rectangle outputConnectorSlotBounds = GetUsableBounds(outputPort.TrailingIconPresenter);
        Rectangle outputConnectorBounds = GetUsableBounds(outputPort.TrailingIconPresenter.Content);

        Assert.True(outputPort.LayoutBounds.Right > inputPort.LayoutBounds.Right);
        Assert.True(inputConnectorSlotBounds.Right <= inputPort.Label.LayoutBounds.Left);
        Assert.True(outputPort.Label.LayoutBounds.Right <= outputConnectorSlotBounds.Left);
        Assert.True(inputConnectorBounds.Width > 0);
        Assert.Equal(Visibility.Visible, outputPort.TrailingIconPresenter.Visibility);
        Assert.True(outputConnectorBounds.Width > 0);
        Assert.True(outputPort.GetLayoutAnchor().X > node.LayoutBounds.Center.X);
        Assert.Equal(outputConnectorBounds.Right, (int)MathF.Round(outputPort.GetLayoutAnchor().X));
    }

    [Fact]
    public void GraphView_AnchorsExecOutputsToTrailingConnectorGeometry()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out _);
        GraphDocument document = graphView.Document;
        Guid nodeId = Guid.NewGuid();
        Guid outputPortId = Guid.NewGuid();

        document.AddNode(nodeId, "Flow/Start", "Start", Vector2.Zero);
        document.AddPort(nodeId, outputPortId, "Next", GraphPortDirection.Output, GraphValueType.Exec);

        desktop.Update();
        graphView.SynchronizeDocument();
        desktop.Update();

        Assert.True(graphView.TryGetNodeControl(nodeId, out MGGraphNode node));
        Assert.True(graphView.TryGetPortControl(outputPortId, out MGGraphPort outputPort));

        Rectangle presenterBounds = GetUsableBounds(outputPort.TrailingIconPresenter);
        Rectangle connectorBounds = GetUsableBounds(outputPort.TrailingIconPresenter.Content);

        Assert.Equal(Visibility.Collapsed, outputPort.Label.Visibility);
        Assert.Equal(Visibility.Visible, outputPort.TrailingIconPresenter.Visibility);
        Assert.True(presenterBounds.Width > 0);
        Assert.True(connectorBounds.Width > 0);
        Assert.True(connectorBounds.Left >= presenterBounds.Left);
        Assert.True(connectorBounds.Right <= presenterBounds.Right);
        Assert.True(outputPort.GetLayoutAnchor().X > node.LayoutBounds.Center.X);
        Assert.Equal(connectorBounds.Right, (int)MathF.Round(outputPort.GetLayoutAnchor().X));
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

    [Fact]
    public void GraphView_DrawsOffscreenEdgeEndpointOutsideViewportAfterPan()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out GraphTestRuntime runtime);
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = Guid.NewGuid();
        Guid targetNodeId = Guid.NewGuid();
        Guid sourcePortId = Guid.NewGuid();
        Guid targetPortId = Guid.NewGuid();
        graphView.ShowGrid = false;
        graphView.EdgeBrush = new MGSolidFillBrush(Color.HotPink);
        graphView.CullingPadding = 0.0f;

        GraphNodeModel sourceNode = document.AddNode(sourceNodeId, "Value", "Source", new Vector2(20, 40));
        GraphNodeModel targetNode = document.AddNode(targetNodeId, "Value", "Target", new Vector2(620, 120));
        sourceNode.Size = new Vector2(140, 80);
        targetNode.Size = new Vector2(140, 80);
        document.AddPort(sourceNodeId, sourcePortId, "Out", GraphPortDirection.Output, GraphValueType.Float);
        document.AddPort(targetNodeId, targetPortId, "In", GraphPortDirection.Input, GraphValueType.Float);
        document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);

        desktop.Update();
        graphView.SynchronizeDocument();
        desktop.Update();

        graphView.ViewportTransform.Pan = new Vector2(-300, 0);
        graphView.SynchronizeDocument();
        desktop.Update();

        Assert.True(graphView.TryGetNodeControl(sourceNodeId, out MGGraphNode sourceNodeControl));
        Assert.Equal(Visibility.Collapsed, sourceNodeControl.Visibility);

        using GraphNoOpDrawTransaction transaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(transaction, 1.0f);
        GraphStrokeLineCall[] edgeCalls = transaction.StrokeLineCalls.Where(call => call.Color == Color.HotPink).ToArray();

        Assert.NotEmpty(edgeCalls);
        Assert.Contains(edgeCalls, call => call.Start.X < 0 || call.End.X < 0);
        Assert.Contains(edgeCalls, call => call.Start.X > 0 || call.End.X > 0);
    }

    [Fact]
    public void GraphView_DrawsSelectedEdgesWithHighlightThicknessAndColor()
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
        GraphEdgeModel edge = document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);
        graphView.SelectedEdgeIds.Add(edge.Id);

        desktop.Update();
        using GraphNoOpDrawTransaction transaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(transaction, 1.0f);
        GraphStrokeLineCall[] edgeCalls = transaction.StrokeLineCalls.ToArray();

        Assert.NotEmpty(edgeCalls);
        Assert.All(edgeCalls, call => Assert.Equal(4.0f, call.Thickness));
        Assert.All(edgeCalls, call => Assert.Equal(Color.Lerp(Color.HotPink, Color.White, 0.35f), call.Color));
    }

    [Fact]
    public void GraphView_DarkThemeRendersWhiteEdgesAndYellowSelectedEdges()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out GraphTestRuntime runtime);
        MGWindow window = graphView.SelfOrParentWindow;
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = Guid.NewGuid();
        Guid targetNodeId = Guid.NewGuid();
        Guid sourcePortId = Guid.NewGuid();
        Guid targetPortId = Guid.NewGuid();
        graphView.ShowGrid = false;
        graphView.EdgeThickness = 3.0f;

        GraphNodeModel sourceNode = document.AddNode(sourceNodeId, "Value", "Source", new Vector2(20, 40));
        GraphNodeModel targetNode = document.AddNode(targetNodeId, "Value", "Target", new Vector2(260, 120));
        sourceNode.Size = new Vector2(140, 80);
        targetNode.Size = new Vector2(140, 80);
        document.AddPort(sourceNodeId, sourcePortId, "Out", GraphPortDirection.Output, GraphValueType.Float);
        document.AddPort(targetNodeId, targetPortId, "In", GraphPortDirection.Input, GraphValueType.Float);
        GraphEdgeModel edge = document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);

        window.Theme = new MGTheme(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily);
        desktop.Update();
        graphView.SynchronizeDocument();
        desktop.Update();

        using GraphNoOpDrawTransaction unselectedTransaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(unselectedTransaction, 1.0f);
        GraphStrokeLineCall[] unselectedEdgeCalls = unselectedTransaction.StrokeLineCalls.ToArray();

        Assert.NotEmpty(unselectedEdgeCalls);
        Assert.All(unselectedEdgeCalls, call => Assert.Equal(Color.White, call.Color));
        Assert.All(unselectedEdgeCalls, call => Assert.Equal(3.0f, call.Thickness));

        graphView.SelectedEdgeIds.Add(edge.Id);

        using GraphNoOpDrawTransaction selectedTransaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(selectedTransaction, 1.0f);
        GraphStrokeLineCall[] selectedEdgeCalls = selectedTransaction.StrokeLineCalls.ToArray();

        Assert.NotEmpty(selectedEdgeCalls);
        Assert.All(selectedEdgeCalls, call => Assert.Equal(Color.Yellow, call.Color));
        Assert.All(selectedEdgeCalls, call => Assert.Equal(4.0f, call.Thickness));
    }

    [Fact]
    public void GraphView_DrawsPartiallyOffscreenOutputEndpointBeyondViewportBounds()
    {
        MGGraphView graphView = CreateGraphView(out MGDesktop desktop, out GraphTestRuntime runtime);
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = Guid.NewGuid();
        Guid targetNodeId = Guid.NewGuid();
        Guid sourcePortId = Guid.NewGuid();
        Guid targetPortId = Guid.NewGuid();
        graphView.ShowGrid = false;
        graphView.EdgeBrush = new MGSolidFillBrush(Color.HotPink);

        GraphNodeModel sourceNode = document.AddNode(sourceNodeId, "Value", "Source", new Vector2(560, 120));
        GraphNodeModel targetNode = document.AddNode(targetNodeId, "Value", "Target", new Vector2(180, 120));
        sourceNode.Size = new Vector2(140, 80);
        targetNode.Size = new Vector2(140, 80);
        document.AddPort(sourceNodeId, sourcePortId, "Out", GraphPortDirection.Output, GraphValueType.Float);
        document.AddPort(targetNodeId, targetPortId, "In", GraphPortDirection.Input, GraphValueType.Float);
        document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);

        desktop.Update();
        graphView.SynchronizeDocument();
        desktop.Update();

        Assert.True(graphView.TryGetNodeControl(sourceNodeId, out MGGraphNode sourceNodeControl));
        Assert.Equal(Visibility.Visible, sourceNodeControl.Visibility);
        Assert.True(graphView.TryGetPortControl(sourcePortId, out MGGraphPort sourcePortControl));
        Assert.True(sourcePortControl.GetLayoutAnchor().X > graphView.NodesCanvas.ActualLayoutBounds.Right);

        using GraphNoOpDrawTransaction transaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(transaction, 1.0f);
        GraphStrokeLineCall[] edgeCalls = transaction.StrokeLineCalls.Where(call => call.Color == Color.HotPink).ToArray();

        Assert.NotEmpty(edgeCalls);
        int viewportRight = graphView.NodesCanvas.ActualLayoutBounds.Right;
        Assert.Contains(edgeCalls, call =>
            call.Origin.X + call.Start.X > viewportRight
            || call.Origin.X + call.End.X > viewportRight);
    }

    [Fact]
    public void GraphView_UsesCanvasOffsetForCulledEndpointFallbackAnchors()
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
        GraphDocument document = graphView.Document;
        Guid sourceNodeId = Guid.NewGuid();
        Guid targetNodeId = Guid.NewGuid();
        Guid sourcePortId = Guid.NewGuid();
        Guid targetPortId = Guid.NewGuid();
        graphView.ShowGrid = false;
        graphView.EdgeBrush = new MGSolidFillBrush(Color.HotPink);
        graphView.CullingPadding = 0.0f;

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

        GraphNodeModel sourceNode = document.AddNode(sourceNodeId, "Value", "Source", new Vector2(160, 80));
        GraphNodeModel targetNode = document.AddNode(targetNodeId, "Value", "Target", new Vector2(760, 80));
        sourceNode.Size = new Vector2(140, 80);
        targetNode.Size = new Vector2(140, 80);
        document.AddPort(sourceNodeId, sourcePortId, "Out", GraphPortDirection.Output, GraphValueType.Float);
        document.AddPort(targetNodeId, targetPortId, "In", GraphPortDirection.Input, GraphValueType.Float);
        document.AddEdge(new GraphEdgeModel(Guid.NewGuid(), sourceNodeId, sourcePortId, targetNodeId, targetPortId), validate: false);

        desktop.Update();
        graphView.SynchronizeDocument();
        desktop.Update();

        if (graphView.TryGetNodeControl(targetNodeId, out MGGraphNode targetNodeControl))
        {
            targetNodeControl.Visibility = Visibility.Collapsed;
        }

        Assert.True(GraphPortAnchorResolver.TryGetPortLayoutAnchor(document, graphView.ViewportTransform, targetPortId, out Vector2 targetViewportAnchor));

        using GraphNoOpDrawTransaction transaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(transaction, 1.0f);
        GraphStrokeLineCall[] edgeCalls = transaction.StrokeLineCalls.Where(call => call.Color == Color.HotPink).ToArray();

        Assert.NotEmpty(edgeCalls);
        Vector2 expectedTargetLayoutAnchor = targetViewportAnchor + graphView.NodesCanvas.AlignedContentBounds.Location.ToVector2();
        Assert.True(Vector2.Distance(expectedTargetLayoutAnchor, edgeCalls[^1].End) < 0.01f);
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

    private static MGTheme CreateGraphTheme(MGTheme.BuiltInTheme builtInTheme, string defaultFontFamily, Color canvasColor, Color minorLineColor, Color majorLineColor, Color? nodeBodyColor = null, Color? nodeHeaderColor = null, Color? edgeColor = null, Color? portColor = null)
    {
        MGTheme theme = new(builtInTheme, defaultFontFamily);
        theme.Graph.CanvasBackground = new(new MGSolidFillBrush(canvasColor));
        theme.Graph.GridLineBrush = new MGSolidFillBrush(minorLineColor);
        theme.Graph.MajorGridLineBrush = new MGSolidFillBrush(majorLineColor);
        if (edgeColor.HasValue)
        {
            theme.Graph.EdgeBrush = new MGSolidFillBrush(edgeColor.Value);
        }

        if (portColor.HasValue)
        {
            theme.Graph.PortBackground = new MGSolidFillBrush(portColor.Value);
        }

        if (nodeBodyColor.HasValue)
        {
            theme.Graph.NodeBodyBackground = new(new MGSolidFillBrush(nodeBodyColor.Value));
        }

        if (nodeHeaderColor.HasValue)
        {
            theme.Graph.NodeHeaderBackground = new(new MGSolidFillBrush(nodeHeaderColor.Value));
        }

        return theme;
    }

    private static Rectangle GetUsableBounds(MGElement element)
        => element.ActualLayoutBounds.Width > 0 || element.ActualLayoutBounds.Height > 0 ? element.ActualLayoutBounds : element.LayoutBounds;

    private static GraphRenderColorSnapshot CaptureRenderColors(MGDesktop desktop, GraphTestRuntime runtime)
    {
        using GraphNoOpDrawTransaction transaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.View.Draw(transaction, 1.0f);
        HashSet<Color> lineColors = transaction.StrokeLineCalls.Select(call => call.Color).ToHashSet();
        HashSet<Color> fillColors = transaction.FillRectangleCalls.Select(call => call.Color)
            .Concat(transaction.StrokeAndFillRectangleCalls.Select(call => call.FillColor))
            .Concat(transaction.StrokeAndFillCircleCalls.Select(call => call.FillColor))
            .Concat(transaction.FillTriangleCalls.SelectMany(call => new[] { call.C0, call.C1, call.C2 }))
            .ToHashSet();
        return new GraphRenderColorSnapshot(lineColors, fillColors);
    }

    private sealed record GraphRenderColorSnapshot(HashSet<Color> LineColors, HashSet<Color> FillColors);
}