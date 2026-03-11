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

    [Fact]
    public void MainRenderer_ExposesViewsCollectionAndRegistrationMethods()
    {
        Assert.NotNull(typeof(MainRenderer).GetProperty(nameof(MainRenderer.Views), BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(typeof(MainRenderer).GetMethod(nameof(MainRenderer.RegisterView), BindingFlags.Instance | BindingFlags.Public));
        Assert.NotNull(typeof(MainRenderer).GetMethod(nameof(MainRenderer.UnregisterView), BindingFlags.Instance | BindingFlags.Public));
    }

    [Fact]
    public void MGDesktop_ValidScreenBounds_UsesViewSurfaceWhenAvailable()
    {
        string desktopSource = System.IO.File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGDesktop.cs");

        Assert.Contains("View?.Surface.GetBounds() ?? Renderer.Surface.GetBounds()", desktopSource);
    }
}