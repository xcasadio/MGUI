using MGUI.Core.UI;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Animation;

/// <summary>Slice Y5 (Docs/decisions/0011-animation-v5.md, "Transition de layout"): the XAML <see cref="Element"/> DTO attributes
/// <c>LayoutTransitionDuration</c>, <c>LayoutTransitionEasing</c> and <c>LayoutTransitionAnimatesSize</c>.</summary>
public class LayoutTransitionXamlTests
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
    public void Attributes_SetTheThreeSettings()
    {
        MGWindow window = Load(
            "<Button Name=\"B\" Content=\"Hi\" LayoutTransitionDuration=\"0.25\" LayoutTransitionEasing=\"CubicOut\" LayoutTransitionAnimatesSize=\"True\" />");

        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.NotNull(button.LayoutTransition);
        Assert.Equal(TimeSpan.FromMilliseconds(250), button.LayoutTransition.Duration);
        Assert.Same(UIEasing.CubicOut, button.LayoutTransition.Easing);
        Assert.True(button.LayoutTransition.AnimateSize);
    }

    [Fact]
    public void ImplicitStyle_Setter_OptsEveryButtonOfThatType()
    {
        MGWindow window = Load(
            "<Window.Styles><Style TargetType=\"Button\">" +
            "<Setter Property=\"LayoutTransitionDuration\" Value=\"0.2\" />" +
            "</Style></Window.Styles>" +
            "<StackPanel><Button Name=\"A\" Content=\"Hi\" /><Button Name=\"B\" Content=\"Hi\" /></StackPanel>");

        MGButton a = window.GetElementByName<MGButton>("A");
        MGButton b = window.GetElementByName<MGButton>("B");
        Assert.NotNull(a.LayoutTransition);
        Assert.Equal(TimeSpan.FromMilliseconds(200), a.LayoutTransition.Duration);
        Assert.NotNull(b.LayoutTransition);
        Assert.Equal(TimeSpan.FromMilliseconds(200), b.LayoutTransition.Duration);
    }

    [Fact]
    public void LocalAttribute_OverridesTheStylesDuration()
    {
        MGWindow window = Load(
            "<Window.Styles><Style TargetType=\"Button\">" +
            "<Setter Property=\"LayoutTransitionDuration\" Value=\"0.2\" />" +
            "</Style></Window.Styles>" +
            "<Button Name=\"B\" Content=\"Hi\" LayoutTransitionDuration=\"0.4\" />");

        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.NotNull(button.LayoutTransition);
        Assert.Equal(TimeSpan.FromMilliseconds(400), button.LayoutTransition.Duration);
    }

    [Fact]
    public void ElementWithoutAttributes_KeepsLayoutTransitionNull()
    {
        MGWindow window = Load("<Button Name=\"B\" Content=\"Hi\" />");

        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.Null(button.LayoutTransition);
    }

    [Fact]
    public void ZeroDuration_LeavesLayoutTransitionNull()
    {
        MGWindow window = Load(
            "<Button Name=\"B\" Content=\"Hi\" LayoutTransitionDuration=\"0\" LayoutTransitionEasing=\"CubicOut\" LayoutTransitionAnimatesSize=\"True\" />");

        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.Null(button.LayoutTransition);
    }

    [Fact]
    public void EasingOrSizeFlagAlone_DoNothing()
    {
        MGWindow window = Load(
            "<Button Name=\"B\" Content=\"Hi\" LayoutTransitionEasing=\"CubicOut\" LayoutTransitionAnimatesSize=\"True\" />");

        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.Null(button.LayoutTransition);
    }

    [Fact]
    public void InvalidDuration_IsALoaderError()
    {
        XamlLoaderException error = Assert.Throws<XamlLoaderException>(() => Load(
            "<Button Content=\"Hi\" LayoutTransitionDuration=\"fast\" />"));
        Assert.Contains("fast", error.Message);
    }

    [Fact]
    public void InvalidEasing_IsALoaderError()
    {
        XamlLoaderException error = Assert.Throws<XamlLoaderException>(() => Load(
            "<Button Content=\"Hi\" LayoutTransitionEasing=\"Wobble\" />"));
        Assert.Contains("Wobble", error.Message);
    }
}
