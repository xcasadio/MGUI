using MGUI.Core.UI;
using MGUI.Core.UI.Styling;

namespace MGUI.Tests.Architecture;

public class ThemeScopeInvalidationTests
{
    [Fact]
    public void Child_Scope_Inherits_Default_Theme_Change_From_Parent()
    {
        MGTheme theme1 = new("Arial");
        MGTheme theme2 = new("Consolas");
        MGResources desktop = new(theme1);
        MGResources subtree = new(desktop, UIResourceScope.Subtree);

        (MGTheme PreviousTheme, MGTheme Theme) observed = default;
        int raisedCount = 0;
        subtree.OnDefaultThemeChanged += (_, e) =>
        {
            observed = e;
            raisedCount++;
        };

        desktop.DefaultTheme = theme2;

        Assert.Equal(1, raisedCount);
        Assert.Same(theme1, observed.PreviousTheme);
        Assert.Same(theme2, observed.Theme);
        Assert.Same(theme2, subtree.DefaultTheme);
    }

    [Fact]
    public void Local_Default_Theme_Override_Blocks_Parent_Theme_Propagation()
    {
        MGTheme theme1 = new("Arial");
        MGTheme theme2 = new("Consolas");
        MGTheme theme3 = new("Tahoma");
        MGResources desktop = new(theme1);
        MGResources subtree = new(desktop, UIResourceScope.Subtree)
        {
            DefaultTheme = theme3
        };

        int raisedCount = 0;
        subtree.OnDefaultThemeChanged += (_, _) => raisedCount++;

        desktop.DefaultTheme = theme2;

        Assert.Equal(0, raisedCount);
        Assert.Same(theme3, subtree.DefaultTheme);
    }

    [Fact]
    public void Clearing_Local_Default_Theme_Override_Falls_Back_To_Parent_And_Raises_Event()
    {
        MGTheme theme1 = new("Arial");
        MGTheme theme2 = new("Consolas");
        MGTheme theme3 = new("Tahoma");
        MGResources desktop = new(theme1)
        {
            DefaultTheme = theme2
        };
        MGResources subtree = new(desktop, UIResourceScope.Subtree)
        {
            DefaultTheme = theme3
        };

        (MGTheme PreviousTheme, MGTheme Theme) observed = default;
        int raisedCount = 0;
        subtree.OnDefaultThemeChanged += (_, e) =>
        {
            observed = e;
            raisedCount++;
        };

        subtree.ClearDefaultThemeOverride();

        Assert.Equal(1, raisedCount);
        Assert.Same(theme3, observed.PreviousTheme);
        Assert.Same(theme2, observed.Theme);
        Assert.Same(theme2, subtree.DefaultTheme);
    }
}