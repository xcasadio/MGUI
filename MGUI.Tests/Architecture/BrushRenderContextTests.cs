namespace MGUI.Tests.Architecture;

public class BrushRenderContextTests
{
    [Fact]
    public void RepresentativeBrushes_UseElementDrawContext()
    {
        string solidBrush = System.IO.File.ReadAllText(TestRepository.Combine("MGUI.Core", "UI", "Brushes", "FillBrushes", "MGSolidFillBrush.cs"));
        string textureBrush = System.IO.File.ReadAllText(TestRepository.Combine("MGUI.Core", "UI", "Brushes", "FillBrushes", "MGTextureFillBrush.cs"));

        Assert.Contains("DA.Context", solidBrush);
        Assert.Contains("DA.Context", textureBrush);
    }
}