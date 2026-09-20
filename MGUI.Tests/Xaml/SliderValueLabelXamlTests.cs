using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Xaml;

/// <summary>
/// A slider's value label is declarable.
/// <para/>
/// <see cref="MGSlider.ShowValueLabel"/> and <see cref="MGSlider.ValueLabelFormat"/> had no counterpart on
/// the XAML <see cref="Slider"/> DTO, so a declared slider could not show its value and every caller had to
/// reach back into C# for the format string alone, away from the range and starting value it goes with.
/// <see cref="ProgressBar"/>, the neighbouring control, exposed its own value display all along.
/// </summary>
public class SliderValueLabelXamlTests
{
    [Fact]
    public void ADeclaredSlider_ShowsItsValueInTheDeclaredFormat()
    {
        MGSlider slider = LoadSlider("""
            <Slider Name="sld" Minimum="0" Maximum="1" Value="0.25"
                    ShowValueLabel="True" ValueLabelFormat="F2" />
            """);

        Assert.True(slider.ShowValueLabel);
        Assert.Equal("F2", slider.ValueLabelFormat);
    }

    [Fact]
    public void ADeclaredSlider_CanTurnItsValueLabelOff()
    {
        MGSlider slider = LoadSlider("""<Slider Name="sld" ShowValueLabel="False" />""");

        Assert.False(slider.ShowValueLabel);
    }

    [Fact]
    public void ASliderThatDeclaresNeither_KeepsWhateverItHad()
    {
        // Both properties are optional, so a document that says nothing about the label must not disturb it.
        MGSlider untouched = LoadSlider("""<Slider Name="sld" Minimum="0" Maximum="10" />""");
        MGSlider reference = new(untouched.ParentWindow, 0, 10, 0);

        Assert.Equal(reference.ShowValueLabel, untouched.ShowValueLabel);
        Assert.Equal(reference.ValueLabelFormat, untouched.ValueLabelFormat);
    }

    private static MGSlider LoadSlider(string sliderMarkup)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();

        string markup = $"""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="400" Height="200">
              {sliderMarkup}
            </Window>
            """;

        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(markup, "slider-test"), XamlLoaderMode.Strict);
        Assert.True(window.TryGetElementByName("sld", out MGSlider slider));
        return slider;
    }
}
