using MGUI.Shared.Rendering;

namespace MGUI.Tests.Architecture;

public class RenderContextTests
{
    [Fact]
    public void DrawTransaction_ImplementsIUIRenderContext()
    {
        Assert.Contains(typeof(IUIRenderContext), typeof(DrawTransaction).GetInterfaces());
    }

    [Fact]
    public void DrawBaseArgs_ExposesContextProperty()
    {
        Assert.NotNull(typeof(DrawBaseArgs).GetProperty(nameof(DrawBaseArgs.Context)));
    }

    [Fact]
    public void ElementDrawArgs_ExposesContextProperty()
    {
        Assert.NotNull(typeof(MGUI.Core.UI.ElementDrawArgs).GetProperty(nameof(MGUI.Core.UI.ElementDrawArgs.Context)));
    }
}