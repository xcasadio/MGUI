using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Graph;

public class GraphViewShellTests
{
    [Fact]
    public void GraphView_AppliesDefaultTemplateAndClipsViewport()
    {
        MGWindow window = CreateWindow(out _);
        MGGraphView graphView = new(window);

        Assert.Equal(MGControlTemplateCatalog.GraphViewTemplateName, graphView.DefaultControlTemplateName);
        Assert.Same(graphView.OuterBorder, graphView.TemplateParts[MGGraphView.OuterBorderPartName]);
        Assert.Same(graphView.ViewportHost, graphView.TemplateParts[MGGraphView.ViewportHostPartName]);
        Assert.Same(graphView.NodesCanvas, graphView.TemplateParts[MGGraphView.NodesCanvasPartName]);
        Assert.Same(graphView.OverlayPanel, graphView.TemplateParts[MGGraphView.OverlayPanelPartName]);
        Assert.Same(graphView.NodesCanvas, graphView.Surface);
        Assert.True(graphView.ViewportHost.ClipToBounds);
        Assert.False(graphView.ViewportHost.CanChangeContent);
        Assert.False(graphView.NodesCanvas.CanChangeContent);
        Assert.False(graphView.OverlayPanel.CanChangeContent);
        Assert.Contains(graphView.NodesCanvas, graphView.ViewportHost.Children);
        Assert.Contains(graphView.OverlayPanel, graphView.ViewportHost.Children);
    }

    [Fact]
    public void GraphView_ExposesDocumentViewportAndSelectionState()
    {
        MGWindow window = CreateWindow(out _);
        MGGraphView graphView = new(window);
        List<string> changed = new();
        graphView.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);
        GraphDocument document = new();
        Guid nodeId = Guid.NewGuid();
        Guid edgeId = Guid.NewGuid();

        graphView.Document = document;
        graphView.ViewportTransform.Pan = new Vector2(10, 12);
        graphView.SelectedNodeIds.Add(nodeId);
        graphView.SelectedEdgeIds.Add(edgeId);

        Assert.Same(document, graphView.Document);
        Assert.Same(graphView.ViewportTransform, graphView.Viewport);
        Assert.Contains(nodeId, graphView.SelectedNodeIds);
        Assert.Contains(edgeId, graphView.SelectedEdgeIds);
        Assert.Contains(nameof(MGGraphView.Document), changed);
    }

    [Fact]
    public void GraphView_LoadsFromXamlWithTemplateAttached()
    {
        MGWindow window = CreateWindow(out MGDesktop desktop);
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""320"" Height=""180""
                          WindowStyle=""None"">
  <GraphView Name=""Graph"" ShowGrid=""False"" AllowZoom=""False"" AllowPan=""True"" SnapToGrid=""True"" />
</Window>";

        MGWindow loadedWindow = MGUIXamlParser.LoadRootWindow(desktop, xaml, false, true);
        MGGraphView graphView = loadedWindow.GetElementByName<MGGraphView>("Graph");

        Assert.NotNull(window);
        Assert.NotNull(graphView);
        Assert.False(graphView.ShowGrid);
        Assert.False(graphView.AllowZoom);
        Assert.True(graphView.AllowPan);
        Assert.True(graphView.SnapToGrid);
        Assert.True(graphView.ViewportHost.ClipToBounds);
    }

    private static MGWindow CreateWindow(out MGDesktop desktop)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        desktop = new MGDesktop(runtime);
        return new MGWindow(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
        };
    }
}