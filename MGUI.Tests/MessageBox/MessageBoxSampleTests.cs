using System.IO;
using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.MessageBox;

/// <summary>
/// The MessageBox sample loads, and declares every element its code-behind (<c>MessageBoxSamples</c>) looks up.
/// MGUI.Tests carries no reference to MGUI.Samples, so this loads the same markup from disk through the strict loader,
/// as <c>WindowSampleScreenPlacementTests</c> does for the Window sample.
/// </summary>
public class MessageBoxSampleTests
{
    private static readonly string SamplePath = TestRepository.Combine("MGUI.Samples", "Controls", "MessageBox.xaml");

    [Theory]
    [InlineData("ShowOkButton")]
    [InlineData("AskYesNoButton")]
    [InlineData("AskSaveButton")]
    [InlineData("AskTwiceButton")]
    public void TheSample_DeclaresEveryButtonItsCodeBehindWires(string buttonName)
    {
        MGWindow window = LoadSample();

        Assert.True(window.TryGetElementByName(buttonName, out MGButton _), $"'{buttonName}' is not declared in the MessageBox sample.");
    }

    [Fact]
    public void TheSample_DeclaresItsResultText()
    {
        MGWindow window = LoadSample();

        Assert.True(window.TryGetElementByName("ResultText", out MGTextBlock _), "'ResultText' is not declared in the MessageBox sample.");
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
