using System.IO;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Xaml;

/// <summary>
/// The Window sample demonstrates screen placement, and it loads.
/// <para/>
/// Its code-behind (<c>WindowSamples</c>) moves the sample's own window by setting
/// <see cref="MGWindow.ScreenHorizontalAlignment"/> and <see cref="MGWindow.ScreenVerticalAlignment"/> from
/// five buttons. MGUI.Tests carries no reference to MGUI.Samples, so this loads the same markup from disk
/// through the strict loader and pins the five names that code-behind looks up -- the sample would otherwise
/// only break when somebody ran it and clicked.
/// </summary>
public class WindowSampleScreenPlacementTests
{
    private static readonly string SamplePath = TestRepository.Combine("MGUI.Samples", "Controls", "Window.xaml");

    [Theory]
    [InlineData("btnPlaceTopLeft")]
    [InlineData("btnPlaceCenter")]
    [InlineData("btnPlaceBottomRight")]
    [InlineData("btnPlaceStretch")]
    [InlineData("btnPlaceNone")]
    public void TheSample_DeclaresEveryButtonItsCodeBehindWires(string buttonName)
    {
        MGWindow window = LoadSample();

        Assert.True(window.TryGetElementByName(buttonName, out MGButton _), $"'{buttonName}' is not declared in the Window sample.");
    }

    [Fact]
    public void TheSample_LoadsInStrictMode()
    {
        Assert.NotNull(LoadSample());
    }

    private static MGWindow LoadSample()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1600, 900));
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();

        Assert.True(File.Exists(SamplePath), $"missing {SamplePath}");
        return XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromFile(SamplePath), XamlLoaderMode.Strict, false, true);
    }
}
