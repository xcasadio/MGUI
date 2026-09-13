using System.ComponentModel;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Graph;

public class GraphNodePortControlTests
{
    [Fact]
    public void GraphNode_AppliesDefaultTemplateAndModelState()
    {
        MGWindow window = CreateWindow();
        Guid nodeId = Guid.NewGuid();
        GraphNodeModel model = new(nodeId, "Math/Add", "Add", new Vector2(20, 30))
        {
            IsCollapsed = true,
        };

        MGGraphNode node = new(window, model);

        Assert.Equal(nodeId, node.NodeId);
        Assert.Equal("Add", node.Title);
        Assert.True(node.IsCollapsed);
        Assert.Same(node.OuterBorder, node.TemplateParts[MGGraphNode.OuterBorderPartName]);
        Assert.Same(node.HeaderTextBlock, node.TemplateParts[MGGraphNode.HeaderTextBlockPartName]);
        Assert.Same(node.PortsPanel, node.TemplateParts[MGGraphNode.PortsPanelPartName]);
        Assert.Same(node.BodyPresenter, node.TemplateParts[MGGraphNode.BodyPresenterPartName]);
        Assert.Equal(Visibility.Collapsed, node.PortsPanel.Visibility);
        Assert.Equal(Visibility.Collapsed, node.BodyPresenter.Visibility);
    }

    [Fact]
    public void GraphNode_StatePropertiesRaiseNotifications()
    {
        MGWindow window = CreateWindow();
        MGGraphNode node = new(window);
        List<string> changed = new();
        node.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);
        Guid nodeId = Guid.NewGuid();

        node.NodeId = nodeId;
        node.Title = "Branch";
        node.HasError = true;
        node.HasWarning = true;
        node.IsCollapsed = true;

        Assert.Equal(nodeId, node.NodeId);
        Assert.Contains(nameof(MGGraphNode.NodeId), changed);
        Assert.Contains(nameof(MGGraphNode.Title), changed);
        Assert.Contains(nameof(MGGraphNode.HasError), changed);
        Assert.Contains(nameof(MGGraphNode.HasWarning), changed);
        Assert.Contains(nameof(MGGraphNode.IsCollapsed), changed);
    }

    [Fact]
    public void GraphNode_ApplySelectionVisualUsesSelectedThemeBorder()
    {
        MGWindow window = CreateWindow();
        window.Theme = new MGTheme(window.Desktop.DefaultFontFamily);
        MGGraphNode node = new(window);

        node.IsSelected = true;
        node.ApplySelectionVisual();

        Assert.Equal(window.Theme.Graph.NodeSelectedBorderThickness, node.OuterBorder.BorderThickness);
        Assert.Equal(GetSolidBorderColor(window.Theme.Graph.NodeSelectedBorderBrush), GetSolidBorderColor(node.OuterBorder.BorderBrush));

        node.IsSelected = false;
        node.ApplySelectionVisual();

        Assert.Equal(window.Theme.Graph.NodeBorderThickness, node.OuterBorder.BorderThickness);
        Assert.Equal(GetSolidBorderColor(window.Theme.Graph.NodeBorderBrush), GetSolidBorderColor(node.OuterBorder.BorderBrush));
    }

    [Fact]
    public void GraphNode_DarkThemeUsesWhiteSelectedBorder()
    {
        MGWindow window = CreateWindow();
        window.Theme = new MGTheme(MGTheme.BuiltInTheme.Dark, window.Desktop.DefaultFontFamily);
        MGGraphNode node = new(window)
        {
            IsSelected = true,
        };

        node.ApplySelectionVisual();

        Assert.Equal(Color.White, GetSolidBorderColor(window.Theme.Graph.NodeSelectedBorderBrush));
        Assert.Equal(Color.White, GetSolidBorderColor(node.OuterBorder.BorderBrush));
        Assert.Equal(window.Theme.Graph.NodeSelectedBorderThickness, node.OuterBorder.BorderThickness);
    }

    [Fact]
    public void GraphPort_AppliesDefaultTemplateAndModelState()
    {
        MGWindow window = CreateWindow();
        Guid nodeId = Guid.NewGuid();
        Guid portId = Guid.NewGuid();
        GraphPortModel model = new(nodeId, portId, "Value", GraphPortDirection.Output, GraphValueType.Float, isRequired: true);

        MGGraphPort port = new(window, model)
        {
            IsConnected = true,
        };

        Assert.Equal(portId, port.PortId);
        Assert.Equal("Value", port.PortName);
        Assert.Equal(GraphPortDirection.Output, port.Direction);
        Assert.Equal(GraphValueType.Float, port.ValueType);
        Assert.True(port.IsRequired);
        Assert.True(port.IsConnected);
        Assert.Same(port.OuterBorder, port.TemplateParts[MGGraphPort.OuterBorderPartName]);
        Assert.Same(port.LeadingIconPresenter, port.TemplateParts[MGGraphPort.LeadingIconPresenterPartName]);
        Assert.Same(port.TrailingIconPresenter, port.TemplateParts[MGGraphPort.TrailingIconPresenterPartName]);
        Assert.Same(port.Label, port.TemplateParts[MGGraphPort.LabelPartName]);
    }

    [Fact]
    public void GraphPort_RefreshesConnectorVisualsForDirectionAndType()
    {
        MGWindow window = CreateWindow();
        MGGraphPort inputValuePort = new(window, new GraphPortModel(Guid.NewGuid(), Guid.NewGuid(), "Value", GraphPortDirection.Input, GraphValueType.Float));

        Assert.IsType<MGRectangle>(inputValuePort.LeadingIconPresenter.Content);
        Assert.Null(inputValuePort.TrailingIconPresenter.Content);
        Assert.Equal(Visibility.Visible, inputValuePort.Label.Visibility);
        Assert.Equal(HorizontalAlignment.Left, inputValuePort.Label.TextAlignment);

        inputValuePort.Direction = GraphPortDirection.Output;

        Assert.Null(inputValuePort.LeadingIconPresenter.Content);
        Assert.IsType<MGRectangle>(inputValuePort.TrailingIconPresenter.Content);
        Assert.Equal(Visibility.Visible, inputValuePort.TrailingIconPresenter.Visibility);
        Assert.Equal(HorizontalAlignment.Right, inputValuePort.Label.TextAlignment);

        inputValuePort.ValueType = GraphValueType.Exec;

        Assert.Equal(Visibility.Collapsed, inputValuePort.Label.Visibility);
        Assert.IsType<MGTriangleArrowIcon>(inputValuePort.TrailingIconPresenter.Content);
        Assert.Equal(Visibility.Visible, inputValuePort.TrailingIconPresenter.Visibility);
    }

    [Fact]
    public void GraphPort_ScalesConnectorSlotsAndIconsWithZoom()
    {
        MGWindow window = CreateWindow();

        MGGraphPort floatPort = new(window, new GraphPortModel(Guid.NewGuid(), Guid.NewGuid(), "Value", GraphPortDirection.Output, GraphValueType.Float));
        floatPort.ApplyZoomScale(2.0f);

        Assert.Equal(36, floatPort.TrailingIconPresenter.PreferredWidth);
        Assert.Equal(20, floatPort.TrailingIconPresenter.PreferredHeight);
        MGRectangle floatIcon = Assert.IsType<MGRectangle>(floatPort.TrailingIconPresenter.Content);
        Assert.Equal(20, floatIcon.Width);
        Assert.Equal(20, floatIcon.Height);
        Assert.Equal(new MGCornerRadius(10), floatIcon.CornerRadius);

        MGGraphPort execPort = new(window, new GraphPortModel(Guid.NewGuid(), Guid.NewGuid(), "Next", GraphPortDirection.Output, GraphValueType.Exec));
        execPort.ApplyZoomScale(0.5f);

        Assert.Equal(9, execPort.TrailingIconPresenter.PreferredWidth);
        Assert.Equal(5, execPort.TrailingIconPresenter.PreferredHeight);
        MGTriangleArrowIcon execIcon = Assert.IsType<MGTriangleArrowIcon>(execPort.TrailingIconPresenter.Content);
        Assert.Equal(5, execIcon.PreferredWidth);
        Assert.Equal(5, execIcon.PreferredHeight);
    }

    [Fact]
    public void GraphPort_TemplateMeasuresVisibleLabelContent()
    {
        MGWindow window = CreateWindow();
        MGGraphPort port = new(window, new GraphPortModel(Guid.NewGuid(), Guid.NewGuid(), "Speaker", GraphPortDirection.Output, GraphValueType.String));

        port.UpdateMeasurement(new Size(400, 200), out _, out Thickness fullSize, out _, out _);

        Assert.True(fullSize.Width > 20);
        Assert.Equal(Visibility.Visible, port.Label.Visibility);
        Assert.Equal("Speaker", port.Label.Text);
    }

    [Fact]
    public void GraphPort_CalculatesAnchorsFromLayoutBounds()
    {
        MGWindow window = CreateWindow();
        MGGraphPort port = new(window)
        {
            Direction = GraphPortDirection.Input,
        };
        SetLayoutBounds(port, new Rectangle(10, 20, 40, 12));

        Assert.Equal(new Vector2(14, 26), port.GetLayoutAnchor());

        port.Direction = GraphPortDirection.Output;
        Assert.Equal(new Vector2(46, 26), port.GetLayoutAnchor());

        GraphViewportTransform viewport = new()
        {
            Pan = new Vector2(10, 6),
            Zoom = 2.0f,
        };
        Assert.Equal(new Vector2(18, 10), port.GetWorldAnchor(viewport));
    }

    [Fact]
    public void GraphPort_StatePropertiesRaiseNotifications()
    {
        MGWindow window = CreateWindow();
        MGGraphPort port = new(window);
        List<string> changed = new();
        port.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);
        Guid portId = Guid.NewGuid();

        port.PortId = portId;
        port.PortName = "Input";
        port.IsConnected = true;
        port.IsRequired = true;

        Assert.Equal(portId, port.PortId);
        Assert.Contains(nameof(MGGraphPort.PortId), changed);
        Assert.Contains(nameof(MGGraphPort.PortName), changed);
        Assert.Contains(nameof(MGGraphPort.IsConnected), changed);
        Assert.Contains(nameof(MGGraphPort.IsRequired), changed);
    }

    private static MGWindow CreateWindow()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        return new MGWindow(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
        };
    }

    private static void SetLayoutBounds(MGElement element, Rectangle bounds)
    {
        FieldInfo field = typeof(MGElement).GetField("_LayoutBounds", BindingFlags.Instance | BindingFlags.NonPublic)!;
        field.SetValue(element, bounds);
    }

    private static Color GetSolidBorderColor(IBorderBrush brush)
        => Assert.IsType<MGSolidFillBrush>(Assert.IsType<MGUniformBorderBrush>(brush).Brush).Color;
}