using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;

namespace MGUI.Tests.Architecture;

public class ControlTemplateLoaderTests
{
    [Fact]
    public void Resources_Can_Load_Control_Template_From_Xaml()
    {
        MGResources resources = new(new MGTheme("Arial"));

        string xaml = @"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""Overlay.Chrome""
                 TargetType=""Overlay"">
  <Border Name=""OverlayRoot"" />
  <ControlTemplateDefinition.Parts>
    <TemplatePart Name=""PART_Border"" ElementName=""OverlayRoot"" />
  </ControlTemplateDefinition.Parts>
</ControlTemplate>
";

        IReadOnlyDictionary<string, MGUI.Core.UI.Styling.MGControlTemplate> templates = resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(xaml));

        Assert.Single(templates);
        Assert.True(resources.TryGetControlTemplate("Overlay.Chrome", out MGUI.Core.UI.Styling.MGControlTemplate resolved));
        Assert.Same(templates["Overlay.Chrome"], resolved);
        Assert.True(resolved.SupportsStructure);
    }

    [Fact]
    public void Child_Scope_Can_Override_Parent_Xaml_Control_Template()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        desktop.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(@"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""Window.Chrome""
                 TargetType=""Window""><Border Name=""DesktopRoot"" /></ControlTemplate>"));

        MGResources subtree = new(desktop, UIResourceScope.Subtree);
        subtree.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(@"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""Window.Chrome""
                 TargetType=""Window""><Border Name=""SubtreeRoot"" /></ControlTemplate>"));

        Assert.True(subtree.TryGetControlTemplate("Window.Chrome", out MGUI.Core.UI.Styling.MGControlTemplate resolved));
        Assert.NotSame(desktop.ControlTemplates["Window.Chrome"], resolved);
        Assert.Same(subtree.ControlTemplates["Window.Chrome"], resolved);
    }

  [Fact]
  public void Xaml_Control_Template_BasedOn_Reuses_Base_Template_Defaults()
  {
    MGResources resources = new(new MGTheme("Arial"));

    int applyCount = 0;
    resources.AddControlTemplate(new MGControlTemplate("Window.Base", _ => applyCount++));

    string xaml = @"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
         Name=""Window.Chrome""
         TargetType=""Window""
         BasedOn=""Window.Base"">
  <Border Name=""ChromeRoot"" />
</ControlTemplate>
";

    IReadOnlyDictionary<string, MGUI.Core.UI.Styling.MGControlTemplate> templates = resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(xaml));

    Assert.True(resources.TryGetControlTemplate("Window.Chrome", out MGUI.Core.UI.Styling.MGControlTemplate resolved));

    resolved.Apply(new MGControlTemplateContext(null));

    Assert.Equal(1, applyCount);
    Assert.Same(templates["Window.Chrome"], resolved);
  }

    [Fact]
    public void Xaml_Control_Template_BasedOn_Without_Root_Shares_The_Base_Structure_Template()
    {
        MGResources resources = new(new MGTheme("Arial"));
        MGControlTemplate baseTemplate = new("Overlay.Base", _ => new MGControlTemplateStructure(null), null, _ => { });
        resources.AddControlTemplate(baseTemplate);

        resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(@"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""Overlay.Bare""
                 TargetType=""Overlay""
                 BasedOn=""Overlay.Base"" />"));
        resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(@"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""Overlay.BareOfBare""
                 TargetType=""Overlay""
                 BasedOn=""Overlay.Bare"" />"));
        resources.LoadControlTemplatesFromXaml(XamlDocumentSource.FromString(@"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
                 Name=""Overlay.Rooted""
                 TargetType=""Overlay""
                 BasedOn=""Overlay.Base""><Border Name=""RootedRoot"" /></ControlTemplate>"));

        // An element switches between templates that share a structure template without rebuilding its parts (MGElement.ApplyControlTemplate).
        Assert.Same(baseTemplate, baseTemplate.StructureTemplate);
        Assert.True(resources.TryGetControlTemplate("Overlay.Bare", out MGControlTemplate bare));
        Assert.True(bare.SupportsStructure);
        Assert.Same(baseTemplate, bare.StructureTemplate);
        Assert.True(resources.TryGetControlTemplate("Overlay.BareOfBare", out MGControlTemplate bareOfBare));
        Assert.Same(baseTemplate, bareOfBare.StructureTemplate);
        Assert.True(resources.TryGetControlTemplate("Overlay.Rooted", out MGControlTemplate rooted));
        Assert.Same(rooted, rooted.StructureTemplate);
    }
}