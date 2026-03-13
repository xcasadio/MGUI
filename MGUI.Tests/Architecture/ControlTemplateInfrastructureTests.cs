using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;

namespace MGUI.Tests.Architecture;

public class ControlTemplateInfrastructureTests
{
    [Fact]
    public void Child_Scope_Falls_Back_To_Parent_Control_Template()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        MGControlTemplate template = new("Overlay.Chrome", _ => { });
        desktop.AddControlTemplate(template);

        MGResources subtree = new(desktop, UIResourceScope.Subtree);

        Assert.True(subtree.TryGetControlTemplate("Overlay.Chrome", out MGControlTemplate resolved));
        Assert.Same(template, resolved);
        Assert.Same(desktop.ControlTemplates, desktop.Definitions.ControlTemplates);
    }

    [Fact]
    public void Child_Scope_Can_Override_Parent_Control_Template()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        MGControlTemplate parentTemplate = new("Overlay.Chrome", _ => { });
        MGControlTemplate childTemplate = new("Overlay.Chrome", _ => { });
        desktop.AddControlTemplate(parentTemplate);

        MGResources subtree = new(desktop, UIResourceScope.Subtree);
        subtree.AddControlTemplate(childTemplate);

        Assert.True(subtree.TryGetControlTemplate("Overlay.Chrome", out MGControlTemplate resolved));
        Assert.Same(childTemplate, resolved);
    }

    [Fact]
    public void Control_Template_Can_Apply_Without_Live_Element_Instance()
    {
        bool wasApplied = false;
        MGControlTemplate template = new("ContextMenu.Chrome", _ => wasApplied = true);

        template.Apply(new MGControlTemplateContext(null));

        Assert.True(wasApplied);
    }

    [Fact]
    public void Visual_State_Projection_Maps_State_Transitions_To_Target_Action()
    {
        bool highlighted = false;
        using MGVisualStateProjection projection = new(null, (_, current) =>
        {
            highlighted = current.IsPressedOrHovered || current.IsSelected;
        }, ApplyImmediately: false);

        projection.Apply(new(PrimaryVisualState.Normal, SecondaryVisualState.None), new(PrimaryVisualState.Normal, SecondaryVisualState.Hovered));
        Assert.True(highlighted);

        projection.Apply(new(PrimaryVisualState.Normal, SecondaryVisualState.Hovered), new(PrimaryVisualState.Normal, SecondaryVisualState.None));
        Assert.False(highlighted);

        projection.Apply(new(PrimaryVisualState.Normal, SecondaryVisualState.None), new(PrimaryVisualState.Selected, SecondaryVisualState.None));
        Assert.True(highlighted);
    }

    [Fact]
    public void Default_Catalog_Registers_Priority_And_Selection_Control_Templates()
    {
        MGResources resources = new(new MGTheme("Arial"));

        MGControlTemplateCatalog.RegisterDefaults(resources);

        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.WindowTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.OverlayTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ContextMenuTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ContextMenuItemTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ListBoxTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ListViewTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ComboBoxTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.TreeViewTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.TabControlTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockTabItemTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockAutoHideDrawerTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockAutoHideStripTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockSplitterTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockDropIndicatorsTemplateName, out _));
    }

    [Fact]
    public void Xaml_Element_Exposes_Control_Template_Name()
    {
        Assert.Equal(typeof(string), typeof(Element).GetProperty(nameof(Element.ControlTemplate))?.PropertyType);
    }

    [Fact]
    public void Control_Template_Context_Flags_Theme_Refresh()
    {
        bool? observedFlag = null;
        MGControlTemplate template = new("Window.Chrome", context => observedFlag = context.IsThemeRefresh);

        template.Apply(new MGControlTemplateContext(null, true));

        Assert.True(observedFlag);
    }

    [Fact]
    public void Theme_Exposes_Window_Chrome_Defaults()
    {
        MGTheme theme = new("Arial");

        Assert.Equal(new MonoGame.Extended.Thickness(5), theme.Window.Padding);
        Assert.Equal(new MonoGame.Extended.Thickness(2), theme.Window.BorderThickness);
        Assert.Equal(new MonoGame.Extended.Thickness(2), theme.Window.TitleBarPadding);
        Assert.Equal(24, theme.Window.TitleBarMinHeight);
    }
}