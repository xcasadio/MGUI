namespace MGUI.Tests.Architecture;

public class BrushRenderContextTests
{
    [Fact]
    public void RepresentativeBrushes_UseElementDrawContext()
    {
        string solidBrush = System.IO.File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Brushes\Fill Brushes\MGSolidFillBrush.cs");
        string textureBrush = System.IO.File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Brushes\Fill Brushes\MGTextureFillBrush.cs");

        Assert.Contains("DA.Context", solidBrush);
        Assert.Contains("DA.Context", textureBrush);
    }
}