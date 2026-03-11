using MGUI.Core.UI;

namespace MGUI.Tests.Architecture;

public class UIViewInputRouterTests
{
    [Fact]
    public void Router_CanSetAndClearPreferredView()
    {
        UIViewInputRouter router = new();

        router.SetPreferredInputView(null);
        router.ClearPreferredInputView();

        Assert.Null(router.PreferredInputView);
    }

    [Fact]
    public void Router_ExposesResolutionMethods()
    {
        Assert.NotNull(typeof(UIViewInputRouter).GetMethod(nameof(UIViewInputRouter.ResolveTargetView)));
        Assert.NotNull(typeof(UIViewInputRouter).GetMethod(nameof(UIViewInputRouter.RouteToTargetView)));
    }
}