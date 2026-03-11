namespace MGUI.Tests.Architecture;

public class DesktopBootstrapTests
{
    [Fact]
    public void SampleGame_ExplicitlyLoadsDefaultDesktopResources()
    {
        string game1Source = System.IO.File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Samples\Game1.cs");

        Assert.Contains("Desktop.LoadDefaultResources();", game1Source);
    }
}