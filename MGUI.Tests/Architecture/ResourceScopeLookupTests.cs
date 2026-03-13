using MGUI.Core.UI;
using MGUI.Core.UI.Styling;

namespace MGUI.Tests.Architecture;

public class ResourceScopeLookupTests
{
    [Fact]
    public void Child_Scope_Falls_Back_To_Parent_Static_Resources()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        desktop.AddStaticResource("Accent", 42);

        MGResources window = new(desktop, UIResourceScope.Window);

        Assert.True(window.TryGetStaticResource("Accent", out object value));
        Assert.Equal(42, value);
    }

    [Fact]
    public void Child_Scope_Can_Override_Parent_Static_Resources()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        desktop.AddStaticResource("Accent", 42);

        MGResources window = new(desktop, UIResourceScope.Window);
        window.AddStaticResource("Accent", 84);

        Assert.True(window.TryGetStaticResource("Accent", out object value));
        Assert.Equal(84, value);
    }

    [Fact]
    public void Child_Scope_Inherits_Theme_And_Runtime_Cache_Metadata()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        MGResources subtree = new(desktop, UIResourceScope.Subtree);

        Assert.Same(desktop.DefaultTheme, subtree.DefaultTheme);
        Assert.Same(desktop.AssetProvider, subtree.AssetProvider);
        Assert.Equal(UIResourceScope.Subtree, subtree.Scope);
    }

    [Fact]
    public void Child_Scope_Falls_Back_To_Parent_Element_Templates()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        MGElementTemplate template = new("DummyTemplate", false, window => new MGSpacer(window, 1, 1));
        desktop.AddElementTemplate(template);

        MGResources window = new(desktop, UIResourceScope.Window);

        Assert.True(window.TryGetElementTemplate("DummyTemplate", out MGElementTemplate resolved));
        Assert.Same(template, resolved);
    }
}