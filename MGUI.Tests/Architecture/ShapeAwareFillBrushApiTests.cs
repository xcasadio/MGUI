using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Shapes;

namespace MGUI.Tests.Architecture;

public class ShapeAwareFillBrushApiTests
{
    [Fact]
    public void IFillBrush_ExposesShapeAwareDrawOverload()
    {
        var method = typeof(IFillBrush).GetMethod(
            nameof(IFillBrush.Draw),
            new[] { typeof(MGUI.Core.UI.ElementDrawArgs), typeof(MGUI.Core.UI.MGElement), typeof(MGUI.Core.UI.MGBoxShape), typeof(MGBoxGeometry) });

        Assert.NotNull(method);
    }
}