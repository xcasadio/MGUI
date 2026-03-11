using MGUI.Core.UI.Shapes;

namespace MGUI.Tests.Architecture;

public class RoundedRectangleRenderingApiTests
{
    [Fact]
    public void BoxShapeRenderingExtensions_ExposeExpectedMethods()
    {
        var methods = typeof(DrawTransactionBoxShapeExtensions).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

        Assert.Contains(methods, m => m.Name == nameof(DrawTransactionBoxShapeExtensions.FillRoundedRectangle));
        Assert.Contains(methods, m => m.Name == nameof(DrawTransactionBoxShapeExtensions.StrokeRoundedRectangle));
        Assert.Contains(methods, m => m.Name == nameof(DrawTransactionBoxShapeExtensions.DrawBorderRing));
    }
}