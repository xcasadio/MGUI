using System.Reflection;
using MGUI.Shared.Rendering;

namespace MGUI.Tests.Architecture;

public class SurfaceAbstractionTests
{
    [Fact]
    public void IUISurface_ExposesBoundsAndRenderTarget()
    {
        string[] methodNames = typeof(IUISurface)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(x => x.Name)
            .ToArray();

        Assert.Contains(nameof(IUISurface.GetBounds), methodNames);
        Assert.Contains(nameof(IUISurface.GetRenderTarget), methodNames);
    }

    [Fact]
    public void MainRenderer_ExposesSurfaceProperty()
    {
        PropertyInfo? surfaceProperty = typeof(MainRenderer).GetProperty(nameof(MainRenderer.Surface), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(surfaceProperty);
        Assert.Equal(typeof(IUISurface), surfaceProperty!.PropertyType);
    }
}