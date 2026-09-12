using System.IO;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Animation;

/// <summary>Scenarios SCN-ANIM-001 and SCN-ANIM-002: the demo XAML (MGUI.Samples/Features/AnimationDemo.xaml) loads through the strict loader and its
/// declared transitions, transform, style, visual states and engine-driven progress button are in place. The code-behind is compiled by the samples
/// build; its buttons are exercised by hand in the sample.</summary>
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

    [Fact]
    public void DemoXaml_V2Section_DeclaresStatesStyleAndTheEngineDrivenProgress()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 800));
        MGDesktop desktop = new(runtime);
        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromFile(SamplePath), XamlLoaderMode.Strict, false, true);
        desktop.Windows.Add(window);

        //  The implicit style of the window gives every plain button the hover-scale transition; HoverButton keeps its own (same path, element wins).
        MGButton fade = window.GetElementByName<MGButton>("FadeButton");
        Assert.Equal(TimeSpan.FromMilliseconds(100), fade.Transitions["RenderScale"].Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(120), window.GetElementByName<MGButton>("HoverButton").Transitions["RenderScale"].Duration);

        MGToggleButton toggle = window.GetElementByName<MGToggleButton>("StateToggle");
        Assert.Equal(3, toggle.VisualStates.Count);
        Assert.Equal(new Color(0x27, 0xAE, 0x60), toggle.VisualStates["Checked"].Setters[0].Value);
        Assert.Equal(new Vector2(1.05f), toggle.VisualStates["Hover"].Setters[0].Value);
        Assert.IsType<UITransition<Color>>(toggle.Transitions["Background"]);
        Assert.IsType<UITransition<Vector2>>(toggle.Transitions["RenderTransform.Scale"]);

        MGProgressButton progress = window.GetElementByName<MGProgressButton>("EngineProgress");
        Assert.Equal(TimeSpan.FromSeconds(3), progress.Duration);
        Assert.True(progress.IsPaused);
        Assert.False(progress.Animations.IsAnimating("ProgressButton.Value"));

        foreach (string name in new[] { "StoryboardButton", "SequenceButton", "PopButton", "ThemeAnimationButton" })
        {
            Assert.NotNull(window.GetElementByName<MGButton>(name));
        }

        Assert.NotNull(window.GetElementByName<MGBorder>("V2Target"));
        Assert.NotNull(window.GetElementByName<MGTextBlock>("StateText"));
    }
}
