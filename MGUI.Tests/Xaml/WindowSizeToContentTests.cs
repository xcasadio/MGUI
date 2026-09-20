using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Xaml;

/// <summary>
/// A window that declares its size can still be asked to fit its content afterwards.
/// <para/>
/// On the XAML <see cref="Window"/> DTO, <c>Width</c> and <c>Height</c> are the inherited aliases of
/// <see cref="MGElement.PreferredWidth"/> and <see cref="MGElement.PreferredHeight"/>, so declaring one
/// pinned the element. <see cref="MGWindow.ApplySizeToContent"/> then measured the pinned size and handed
/// it straight back: both the method and the <c>SizeToContent</c> attribute meant to override the declared
/// size became silent no-ops, with nothing thrown and nothing logged.
/// <para/>
/// Found in CasaEngine's dialogue box, which starts at a minimum height and grows to fit a wrapped line
/// plus however many choice buttons the dialogue offers. It stopped growing, and a choice button fell
/// outside the window.
/// </summary>
public class WindowSizeToContentTests
{
    /// <summary>Nine lines of text: comfortably more than the small declared heights used below.</summary>
    private const string TallContent = """
        <StackPanel Orientation="Vertical">
          <TextBlock Text="one" /><TextBlock Text="two" /><TextBlock Text="three" />
          <TextBlock Text="four" /><TextBlock Text="five" /><TextBlock Text="six" />
          <TextBlock Text="seven" /><TextBlock Text="eight" /><TextBlock Text="nine" />
        </StackPanel>
        """;

    [Fact]
    public void ADeclaredHeight_DoesNotDefeatTheDeclaredSizeToContent()
    {
        MGWindow window = LoadWindow($"""Width="300" Height="40" SizeToContent="Height" """);

        Assert.True(window.WindowHeight > 40,
            $"SizeToContent=\"Height\" is declared, so the window must have grown past its declared 40px (actual: {window.WindowHeight}).");
        Assert.Null(window.PreferredHeight);
    }

    [Fact]
    public void SizingToContentInHeight_LeavesTheWidthAlone()
    {
        MGWindow window = LoadWindow($"""Width="300" Height="40" SizeToContent="Height" """);

        // Only the dimension being sized is released; a declared width is still a width.
        Assert.Equal(300, window.PreferredWidth);
        Assert.Equal(300, window.WindowWidth);
    }

    [Fact]
    public void ARuntimeRequestToFitContent_OverridesADeclaredHeight()
    {
        // The case CasaEngine's dialogue box hits: declared in XAML, grown later from code.
        MGWindow window = LoadWindow($"""Width="300" Height="40" """);
        Assert.Equal(40, window.PreferredHeight);

        window.ApplySizeToContent(SizeToContent.Height, MinHeight: 40, MaxHeight: 1000);

        Assert.True(window.WindowHeight > 40,
            $"the window was asked to fit its content and must have grown (actual: {window.WindowHeight}).");
    }

    [Fact]
    public void SizeToContentManual_KeepsBothPreferredSizes()
    {
        // Manual means "do not size to content", so it must release nothing.
        MGWindow window = LoadWindow($"""Width="300" Height="40" """);

        window.ApplySizeToContent(SizeToContent.Manual, MinWidth: 10, MinHeight: 10);

        Assert.Equal(300, window.PreferredWidth);
        Assert.Equal(40, window.PreferredHeight);
    }

    private static MGWindow LoadWindow(string windowAttributes)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();

        string markup = $"""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" {windowAttributes}>
              {TallContent}
            </Window>
            """;

        return XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(markup, "window-size-test"), XamlLoaderMode.Strict);
    }
}
