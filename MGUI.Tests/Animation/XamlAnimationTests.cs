using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using XamlElement = MGUI.Core.UI.XAML.Element;
using XamlWindow = MGUI.Core.UI.XAML.Window;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Animation;

/// <summary>Slice S7: transitions and render transforms declared in XAML.</summary>
public class XamlAnimationTests
{
    private const string Xmlns = "xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\"";

    private static MGWindow Load(string windowContent)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        string xaml = $"<Window {Xmlns} Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\" WindowStyle=\"None\">{windowContent}</Window>";
        return XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml), XamlLoaderMode.Strict, false, true);
    }

    [Fact]
    public void Transitions_AreAttached_WithDurationDelayAndEasing()
    {
        MGWindow window = Load(
            "<Button Name=\"B\" Content=\"Hi\">" +
            "<Button.Transitions>" +
            "<Transition Property=\"Opacity\" Duration=\"0.2\" Easing=\"CubicOut\" />" +
            "<Transition Property=\"Background\" Duration=\"150ms\" Delay=\"0:0:0.05\" Easing=\"quadin\" />" +
            "</Button.Transitions>" +
            "</Button>");

        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.Equal(2, button.Transitions.Count);

        UITransition opacity = button.Transitions["Opacity"];
        Assert.IsType<UITransition<float>>(opacity);
        Assert.Equal(TimeSpan.FromMilliseconds(200), opacity.Duration);
        Assert.Same(UIEasing.CubicOut, opacity.Easing);
        Assert.Same(button, opacity.Owner);

        UITransition background = button.Transitions["Background"];
        Assert.IsType<UITransition<Color>>(background);
        Assert.Equal(TimeSpan.FromMilliseconds(150), background.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(50), background.Delay);
        Assert.Same(UIEasing.QuadIn, background.Easing);
    }

    [Fact]
    public void DeclaredTransition_InterpolatesALaterWrite()
    {
        MGWindow window = Load(
            "<Button Name=\"B\" Content=\"Hi\" Opacity=\"1\">" +
            "<Button.Transitions><Transition Property=\"Opacity\" Duration=\"160ms\" /></Button.Transitions>" +
            "</Button>");
        MGButton button = window.GetElementByName<MGButton>("B");
        MGDesktop desktop = window.Desktop;
        desktop.Windows.Add(window);

        button.Opacity = 0f;

        Assert.Equal(1f, button.Opacity, 1e-4f);
        for (int i = 1; i <= 5; i++)
        {
            desktop.Animations.Update(TimeSpan.FromMilliseconds(16));
        }

        Assert.Equal(0.5f, button.Opacity, 1e-4f);
    }

    [Fact]
    public void RenderTransform_IsDeclared_WithVectorsAndDegrees()
    {
        MGWindow window = Load(
            "<Button Name=\"B\" Content=\"Hi\">" +
            "<Button.RenderTransform><RenderTransform Scale=\"1.2\" Origin=\"0.5,0.5\" Rotation=\"10\" Translation=\"4, -2\" /></Button.RenderTransform>" +
            "</Button>");

        MGButton button = window.GetElementByName<MGButton>("B");

        Assert.Equal(new Vector2(1.2f, 1.2f), button.RenderTransform.Scale);
        Assert.Equal(new Vector2(0.5f, 0.5f), button.RenderTransform.Origin);
        Assert.Equal(10f, button.RenderTransform.Rotation);
        Assert.Equal(new Vector2(4f, -2f), button.RenderTransform.Translation);
        Assert.True(button.HasActiveRenderTransform);
    }

    [Fact]
    public void RenderScaleAttribute_AndRenderTransform_Coexist()
    {
        MGWindow window = Load(
            "<Button Name=\"B\" Content=\"Hi\" RenderScale=\"1.05\">" +
            "<Button.RenderTransform><RenderTransform Rotation=\"5\" /></Button.RenderTransform>" +
            "</Button>");

        MGButton button = window.GetElementByName<MGButton>("B");

        Assert.Equal(1.05f, button.RenderScale.Value.HoveredScale);
        Assert.Equal(5f, button.RenderTransform.Rotation);
        Assert.Equal(Vector2.One, button.RenderTransform.Scale);
    }

    [Fact]
    public void UnknownTransitionProperty_IsALoaderDiagnostic()
    {
        XamlLoaderException error = Assert.Throws<XamlLoaderException>(() => Load(
            "<Button Content=\"Hi\"><Button.Transitions><Transition Property=\"Nope\" Duration=\"0.1\" /></Button.Transitions></Button>"));

        Assert.Contains("Nope", error.Message);
        Assert.Contains("Opacity", error.Message);
    }

    [Fact]
    public void UnknownEasing_AndBadDuration_AreLoaderDiagnostics()
    {
        XamlLoaderException easing = Assert.Throws<XamlLoaderException>(() => Load(
            "<Button Content=\"Hi\"><Button.Transitions><Transition Property=\"Opacity\" Duration=\"0.1\" Easing=\"Wobble\" /></Button.Transitions></Button>"));
        Assert.Contains("Wobble", easing.Message);
        Assert.Equal(XamlLoaderDiagnosticCode.InvalidValueConversion, easing.Diagnostic.Code);

        XamlLoaderException duration = Assert.Throws<XamlLoaderException>(() => Load(
            "<Button Content=\"Hi\"><Button.Transitions><Transition Property=\"Opacity\" Duration=\"fast\" /></Button.Transitions></Button>"));
        Assert.Contains("fast", duration.Message);
        Assert.Equal(XamlLoaderDiagnosticCode.InvalidValueConversion, duration.Diagnostic.Code);
    }

    [Theory]
    [InlineData("0.15", 150)]
    [InlineData("150ms", 150)]
    [InlineData("0:0:0.15", 150)]
    [InlineData("2s", 2000)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    public void Durations_AcceptSecondsMillisecondsAndTimeSpans(string text, int expectedMilliseconds)
    {
        Assert.True(AnimationXamlParser.TryParseDuration(text, out TimeSpan duration));
        Assert.Equal(TimeSpan.FromMilliseconds(expectedMilliseconds), duration);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("fast")]
    [InlineData("1,5")]
    public void Durations_RejectInvalidText(string text)
    {
        Assert.False(AnimationXamlParser.TryParseDuration(text, out _));
    }

    [Theory]
    [InlineData("1.5", 1.5f, 1.5f)]
    [InlineData("0.5,0.25", 0.5f, 0.25f)]
    [InlineData("4 -2", 4f, -2f)]
    public void Vectors_AcceptPairsAndUniformValues(string text, float x, float y)
    {
        Assert.Equal(new Vector2(x, y), AnimationXamlParser.ParseVector2(text));
    }

    [Fact]
    public void Vectors_RejectInvalidText()
    {
        Assert.Throws<FormatException>(() => AnimationXamlParser.ParseVector2("a,b"));
        Assert.Throws<FormatException>(() => AnimationXamlParser.ParseVector2("1,2,3"));
    }
}
