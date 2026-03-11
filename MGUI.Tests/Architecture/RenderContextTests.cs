using MGUI.Shared.Rendering;

namespace MGUI.Tests.Architecture;

public class RenderContextTests
{
    [Fact]
    public void DrawTransaction_ImplementsIUIRenderContext()
    {
        Assert.Contains(typeof(IUIRenderContext), typeof(DrawTransaction).GetInterfaces());
    }
}