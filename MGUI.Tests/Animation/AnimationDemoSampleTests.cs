using System.IO;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Animation;

/// <summary>Scenario SCN-ANIM-001: the demo XAML (MGUI.Samples/Features/AnimationDemo.xaml) loads through the strict loader and its declared
/// transitions and transform are in place. The code-behind is compiled by the samples build; its buttons are exercised by hand in the sample.</summary>
public class AnimationDemoSampleTests
{
    private static readonly string SamplePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MGUI.Samples", "Features", "AnimationDemo.xaml"));

    [Fact]
    public void DemoXaml_LoadsInStrictMode_WithItsTransitionsAndTransform()
    {
        Assert.True(File.Exists(SamplePath), SamplePath);
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 800));
        MGDesktop desktop = new(runtime);

        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromFile(SamplePath), XamlLoaderMode.Strict, false, true);
        desktop.Windows.Add(window);

        MGButton hover = window.GetElementByName<MGButton>("HoverButton");
        Assert.Equal(2, hover.Transitions.Count);
        Assert.IsType<UITransition<float>>(hover.Transitions["RenderScale"]);
        Assert.IsType<UITransition<Color>>(hover.Transitions["Background"]);
        Assert.Equal(new Vector2(0.5f, 0.5f), hover.RenderTransform.Origin);
        Assert.Equal(1.06f, hover.RenderScale.Value.HoveredScale);

        foreach (string name in new[] { "ColorButton", "FadeButton", "SlideButton", "SpinButton", "PulseButton", "MarginButton", "PopupButton", "PauseButton", "HalfSpeedButton", "NormalSpeedButton" })
        {
            Assert.NotNull(window.GetElementByName<MGButton>(name));
        }

        Assert.NotNull(window.GetElementByName<MGBorder>("Target"));
        Assert.NotNull(window.GetElementByName<MGBorder>("MarginTarget"));
        Assert.NotNull(window.GetElementByName<MGTextBlock>("ActiveCountText"));
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }
}
