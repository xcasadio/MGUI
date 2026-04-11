using System.Reflection;
using MGUI.Shared.Rendering;

namespace MGUI.Tests.Architecture;

public class SurfaceAbstractionTests
{
    [Fact]
    public void IUISurface_ExposesBoundsAndRenderTarget()
    {
        MethodInfo? getBoundsMethod = typeof(IUISurface).GetMethod(nameof(IUISurface.GetBounds), BindingFlags.Instance | BindingFlags.Public);
        MethodInfo? getRenderTargetMethod = typeof(IUISurface).GetMethod(nameof(IUISurface.GetRenderTarget), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(getBoundsMethod);
        Assert.NotNull(getRenderTargetMethod);
        Assert.Equal(typeof(IUIRenderTarget), getRenderTargetMethod!.ReturnType);
    }

    [Fact]
    public void MainRenderer_ExposesSurfaceProperty()
    {
        PropertyInfo? surfaceProperty = typeof(MainRenderer).GetProperty(nameof(MainRenderer.Surface), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(surfaceProperty);
        Assert.Equal(typeof(IUISurface), surfaceProperty!.PropertyType);
    }

    [Fact]
    public void BackBufferSurface_ImplementsIUISurface()
    {
        Assert.Contains(typeof(IUISurface), typeof(BackBufferSurface).GetInterfaces());
    }
}