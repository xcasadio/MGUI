using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Shapes;

namespace MGUI.Tests.Architecture;

public class ShapeAwareBorderBrushApiTests
{
    [Fact]
    public void IBorderBrush_ExposesShapeAwareDrawOverload()
    {
        var method = typeof(IBorderBrush).GetMethod(
            nameof(IBorderBrush.Draw),
            new[] { typeof(MGUI.Core.UI.ElementDrawArgs), typeof(MGUI.Core.UI.MGElement), typeof(MGUI.Core.UI.MGBoxShape), typeof(MGBoxGeometry) });

        Assert.NotNull(method);
    }
}