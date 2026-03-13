using MGUI.Core.UI;
using MGUI.Core.UI.Styling;

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
}