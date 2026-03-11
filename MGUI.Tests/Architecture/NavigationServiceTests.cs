using MGUI.Core.UI;
using MGUI.Core.UI.Navigation;
using System.Reflection;

namespace MGUI.Tests.Architecture;

public class NavigationServiceTests
{
    [Fact]
    public void MGDesktop_ExposesNavigationService()
    {
        PropertyInfo? navigationServiceProperty = typeof(MGDesktop).GetProperty(nameof(MGDesktop.NavigationService), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(navigationServiceProperty);
        Assert.Equal(typeof(UIFocusNavigationService), navigationServiceProperty!.PropertyType);
    }

    [Fact]
    public void NavigationService_ContainsFocusNavigationOperations()
    {
        Assert.NotNull(typeof(UIFocusNavigationService).GetMethod(nameof(UIFocusNavigationService.GetFocusableElements), BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(typeof(UIFocusNavigationService).GetMethod(nameof(UIFocusNavigationService.MoveFocusNext), new[] { typeof(KeyboardFocusSource) }));
        Assert.NotNull(typeof(UIFocusNavigationService).GetMethod(nameof(UIFocusNavigationService.MoveFocusPrevious), new[] { typeof(KeyboardFocusSource) }));
        Assert.NotNull(typeof(UIFocusNavigationService).GetMethod(nameof(UIFocusNavigationService.NavigateTo), new[] { typeof(NavigationDirection), typeof(KeyboardFocusSource) }));
    }
}