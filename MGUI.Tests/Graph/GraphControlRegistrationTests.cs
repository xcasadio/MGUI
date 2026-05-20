using MGUI.Core.UI;
using MGUI.Core.UI.Graph;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using XamlElement = MGUI.Core.UI.XAML.Element;
using XamlGraphPort = MGUI.Core.UI.XAML.GraphPort;
using XamlGraphView = MGUI.Core.UI.XAML.GraphView;
using XamlThickness = MGUI.Core.UI.XAML.Thickness;

namespace MGUI.Tests.Graph;

public class GraphControlRegistrationTests
{
    [Fact]
    public void GraphElementTypes_AreRegisteredInElementVocabulary()
    {
        Assert.True(Enum.IsDefined(MGElementType.GraphView));
        Assert.True(Enum.IsDefined(MGElementType.GraphNode));
        Assert.True(Enum.IsDefined(MGElementType.GraphPort));
        Assert.True(Enum.IsDefined(MGElementType.GraphCommentBox));
    }

    [Fact]
    public void ControlTemplateCatalog_RegistersGraphTemplates()
    {
        MGResources resources = new(new MGTheme("Arial"));

        MGControlTemplateCatalog.RegisterDefaults(resources);

        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.GraphViewTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.GraphNodeTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.GraphPortTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.GraphCommentBoxTemplateName, out _));
    }

    [Fact]
    public void XamlParser_ParsesGraphControls()
    {
        XamlElement graphView = XAMLParser.ParseElementDefinition(
            XamlDocumentSource.FromString("<GraphView Width=\"320\" Height=\"200\" ShowGrid=\"False\" AllowZoom=\"True\" AllowPan=\"True\" SnapToGrid=\"True\" />"),
            null,
            true,
            true);

        XamlElement graphPort = XAMLParser.ParseElementDefinition(
            XamlDocumentSource.FromString("<GraphPort PortName=\"Result\" Direction=\"Output\" ValueType=\"Float\" />"),
            null,
            true,
            true);

        XamlGraphView parsedView = Assert.IsType<XamlGraphView>(graphView);
        Assert.False(parsedView.ShowGrid.GetValueOrDefault());
        Assert.True(parsedView.AllowZoom.GetValueOrDefault());
        Assert.True(parsedView.AllowPan.GetValueOrDefault());
        Assert.True(parsedView.SnapToGrid.GetValueOrDefault());
        XamlGraphPort parsedPort = Assert.IsType<XamlGraphPort>(graphPort);
        Assert.Equal("Result", parsedPort.PortName);
        Assert.Equal(GraphPortDirection.Output, parsedPort.Direction);
        Assert.Equal(GraphValueType.Float, parsedPort.ValueType);
    }

    [Fact]
    public void ThemeDefinitionBuilder_AppliesGraphSettings()
    {
        ThemeDefinition definition = new()
        {
            Graph = new ThemeGraphSettingsDefinition
            {
                Padding = new XamlThickness(4),
                BorderThickness = new XamlThickness(2),
            }
        };

        MGTheme theme = ThemeDefinitionBuilder.Build(definition, "Arial", MGTheme.CreateEmpty("Arial"));

        Assert.Equal(new MonoGame.Extended.Thickness(4), theme.Graph.Padding);
        Assert.Equal(new MonoGame.Extended.Thickness(2), theme.Graph.BorderThickness);
        Assert.NotNull(theme.Graph.EdgeBrush);
    }

    [Fact]
    public void BuiltInThemes_ProvideGraphSettings()
    {
        foreach (MGTheme.BuiltInTheme builtInTheme in Enum.GetValues<MGTheme.BuiltInTheme>())
        {
            MGTheme theme = new(builtInTheme, "Arial");

            Assert.NotNull(theme.Graph.CanvasBackground.GetUnderlay(PrimaryVisualState.Normal));
            Assert.NotNull(theme.Graph.EdgeBrush);
            Assert.NotNull(theme.Graph.NodeBorderBrush);
        }
    }
}