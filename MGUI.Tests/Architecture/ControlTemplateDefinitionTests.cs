using MGUI.Core.UI.XAML;

namespace MGUI.Tests.Architecture;

public class ControlTemplateDefinitionTests
{
    [Fact]
    public void ControlTemplateDefinition_Can_Be_Parsed_From_Xaml()
    {
        string xaml = @"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""Window.Chrome""
                 TargetType=""Window""
                 Notes=""Initial structural template support"">
  <Border Name=""ChromeRoot"" Padding=""5"">
    <DockPanel Name=""TitleBarHost"" />
  </Border>
  <ControlTemplateDefinition.Parts>
    <TemplatePart Name=""PART_Border"" ElementName=""ChromeRoot"" />
    <TemplatePart Name=""PART_TitleBar"" ElementName=""TitleBarHost"" />
  </ControlTemplateDefinition.Parts>
</ControlTemplate>
";

        ControlTemplateDefinition definition = XAMLParser.ParseObjectDefinition<ControlTemplateDefinition>(XamlDocumentSource.FromString(xaml));

        Assert.Equal("Window.Chrome", definition.Name);
        Assert.Equal("Window", definition.TargetType);
        Assert.Equal("Initial structural template support", definition.Notes);
        Assert.NotNull(definition.Root);
        Assert.Equal("ChromeRoot", definition.Root.Name);
        Assert.Equal(2, definition.Parts.Count);
        Assert.Equal("PART_Border", definition.Parts[0].Name);
        Assert.Equal("ChromeRoot", definition.Parts[0].ElementName);
    }

    [Fact]
    public void ControlTemplatesDocument_Can_Parse_Multiple_Templates()
    {
        string xaml = @"
<ControlTemplatesDocument xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"">
  <ControlTemplate Name=""Overlay.Chrome"" TargetType=""Overlay"">
    <Border Name=""OverlayRoot"" />
  </ControlTemplate>
  <ControlTemplate Name=""TabControl.Chrome"" TargetType=""TabControl"">
    <DockPanel Name=""TabsRoot"" />
  </ControlTemplate>
</ControlTemplatesDocument>
";

        ControlTemplatesDocument document = XAMLParser.ParseObjectDefinition<ControlTemplatesDocument>(XamlDocumentSource.FromString(xaml));

        Assert.Equal(2, document.Templates.Count);
        Assert.Equal("Overlay.Chrome", document.Templates[0].Name);
        Assert.Equal("TabControl.Chrome", document.Templates[1].Name);
    }
}