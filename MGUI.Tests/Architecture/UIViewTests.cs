using System.Reflection;
using MGUI.Core.UI;
using MGUI.Shared.Rendering;

namespace MGUI.Tests.Architecture;

public class UIViewTests
{
    [Fact]
    public void UIView_ImplementsIUIView()
    {
        Assert.Contains(typeof(IUIView), typeof(UIView).GetInterfaces());
    }

    [Fact]
    public void UIView_ExposesSurfaceProperty()
    {
        PropertyInfo? surfaceProperty = typeof(UIView).GetProperty(nameof(UIView.Surface), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(surfaceProperty);
        Assert.Equal(typeof(IUISurface), surfaceProperty!.PropertyType);
    }

    [Fact]
    public void MGDesktop_ExposesViewProperty()
    {
        PropertyInfo? viewProperty = typeof(MGDesktop).GetProperty(nameof(MGDesktop.View), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(viewProperty);
        Assert.Equal(typeof(UIView), viewProperty!.PropertyType);
    }
}