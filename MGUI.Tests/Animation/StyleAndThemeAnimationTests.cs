using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.States;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using XamlStyle = MGUI.Core.UI.XAML.Style;

namespace MGUI.Tests.Animation;

/// <summary>Slice T5 of Docs/Tasks/animation-v2-tasks.md: transitions and visual states declared in styles, the theme animation group and the buttons that opt in.</summary>
public class StyleAndThemeAnimationTests
{
    private const string Xmlns = "xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\"";

    private static MGDesktop NewDesktop() => new(new GraphTestRuntime(new Rectangle(0, 0, 800, 600)));

    private static MGWindow Load(MGDesktop desktop, string windowContent)
    {
        string xaml = $"<Window {Xmlns} Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\" WindowStyle=\"None\">{windowContent}</Window>";
        return XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml), XamlLoaderMode.Strict, false, true);
    }

    private static MGWindow Load(string windowContent) => Load(NewDesktop(), windowContent);

    private const string ButtonStyle =
        "<Window.Styles><Style TargetType=\"Button\">" +
        "<Style.Transitions><Transition Property=\"Opacity\" Duration=\"0.2\" Easing=\"CubicOut\" /></Style.Transitions>" +
        "<Style.VisualStates><VisualStateDefinition Name=\"Hover\"><Setter Property=\"RenderTransform.Scale\" Value=\"1.05\" /></VisualStateDefinition></Style.VisualStates>" +
        "</Style></Window.Styles>";

    [Fact]
    public void AnImplicitStyle_GivesItsTransitionsAndStates_ToAButton()
    {
        // The style has no setter: it must still be indexed and applied.
        MGWindow window = Load(ButtonStyle + "<StackPanel><Button Name=\"B\" Content=\"Hi\" /><TextBlock Name=\"T\" Text=\"x\" /></StackPanel>");

        MGButton button = window.GetElementByName<MGButton>("B");
        UITransition opacity = button.Transitions["Opacity"];
        Assert.IsType<UITransition<float>>(opacity);
        Assert.Equal(TimeSpan.FromMilliseconds(200), opacity.Duration);
        Assert.Same(UIEasing.CubicOut, opacity.Easing);

        UIVisualState hover = button.VisualStates["Hover"];
        Assert.NotNull(hover);
        UIVisualStateSetter setter = Assert.Single(hover.Setters);
        Assert.Equal(UIBuiltInAnimationTargets.Paths.RenderTransformScale, setter.Path);
        Assert.Equal(new Vector2(1.05f), setter.Value);

        MGTextBlock text = window.GetElementByName<MGTextBlock>("T");
        Assert.Null(text.Transitions["Opacity"]);
        Assert.Null(text.CurrentVisualStateName);
    }

    [Fact]
    public void ANamedStyle_ReplacesTheTransitionOfAPath_AndAddsItsOwnState()
    {
        MGWindow window = Load(
            "<Window.Styles>" +
            "<Style TargetType=\"Button\"><Style.Transitions><Transition Property=\"Opacity\" Duration=\"0.2\" /></Style.Transitions></Style>" +
            "<Style TargetType=\"Button\" Name=\"Fast\">" +
            "<Style.Transitions><Transition Property=\"Opacity\" Duration=\"50ms\" /></Style.Transitions>" +
            "<Style.VisualStates><VisualStateDefinition Name=\"Pressed\"><Setter Property=\"Opacity\" Value=\"0.5\" /></VisualStateDefinition></Style.VisualStates>" +
            "</Style>" +
            "</Window.Styles>" +
            "<StackPanel><Button Name=\"Plain\" Content=\"a\" /><Button Name=\"Fast\" Content=\"b\" StyleNames=\"Fast\" /></StackPanel>");

        Assert.Equal(TimeSpan.FromMilliseconds(200), window.GetElementByName<MGButton>("Plain").Transitions["Opacity"].Duration);
        MGButton fast = window.GetElementByName<MGButton>("Fast");
        Assert.Equal(TimeSpan.FromMilliseconds(50), fast.Transitions["Opacity"].Duration);
        Assert.Equal(1, fast.VisualStates.Count);
        Assert.Equal(0.5f, fast.VisualStates["Pressed"].Setters[0].Value);
        Assert.Equal(0, window.GetElementByName<MGButton>("Plain").VisualStates.Count);
    }

    [Fact]
    public void TheElementsOwnDeclarations_WinOverTheStyles()
    {
        MGWindow window = Load(ButtonStyle +
            "<Button Name=\"B\" Content=\"Hi\">" +
            "<Button.Transitions><Transition Property=\"Opacity\" Duration=\"1\" /></Button.Transitions>" +
            "<Button.VisualStates><VisualStateDefinition Name=\"Hover\"><Setter Property=\"Opacity\" Value=\"0.25\" /></VisualStateDefinition></Button.VisualStates>" +
            "</Button>");

        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.Equal(TimeSpan.FromSeconds(1), button.Transitions["Opacity"].Duration);
        Assert.Equal(1, button.VisualStates.Count);
        UIVisualStateSetter setter = Assert.Single(button.VisualStates["Hover"].Setters);
        Assert.Equal(UIBuiltInAnimationTargets.Paths.Opacity, setter.Path);
        Assert.Equal(0.25f, setter.Value);
    }

    [Fact]
    public void StateSetterValues_AreConvertedByTheTargetType()
    {
        MGWindow window = Load(
            "<Button Name=\"B\" Content=\"Hi\"><Button.VisualStates><VisualStateDefinition Name=\"Hover\">" +
            "<Setter Property=\"Background\" Value=\"Green\" />" +
            "<Setter Property=\"Margin\" Value=\"2,4\" />" +
            "<Setter Property=\"PreferredWidth\" Value=\"120\" />" +
            "<Setter Property=\"RenderTransform.Translation\" Value=\"3,-1\" />" +
            "<Setter Property=\"Opacity\" Value=\"0.5\" />" +
            "</VisualStateDefinition></Button.VisualStates></Button>");

        UIVisualState hover = window.GetElementByName<MGButton>("B").VisualStates["Hover"];
        Assert.Equal(Color.Green, hover.Setters[0].Value);
        Assert.Equal(new MonoGame.Extended.Thickness(2, 4), hover.Setters[1].Value);
        Assert.Equal(120, hover.Setters[2].Value);
        Assert.Equal(new Vector2(3, -1), hover.Setters[3].Value);
        Assert.Equal(0.5f, hover.Setters[4].Value);
    }

    [Fact]
    public void InvalidStateSetters_AreLoaderDiagnostics()
    {
        XamlLoaderException unknownPath = Assert.Throws<XamlLoaderException>(() => Load(
            "<Button Content=\"Hi\"><Button.VisualStates><VisualStateDefinition Name=\"Hover\"><Setter Property=\"Nope\" Value=\"1\" /></VisualStateDefinition></Button.VisualStates></Button>"));
        Assert.Contains("Nope", unknownPath.Message);

        XamlLoaderException badValue = Assert.Throws<XamlLoaderException>(() => Load(
            "<Button Content=\"Hi\"><Button.VisualStates><VisualStateDefinition Name=\"Hover\"><Setter Property=\"Opacity\" Value=\"half\" /></VisualStateDefinition></Button.VisualStates></Button>"));
        Assert.Contains("half", badValue.Message);

        XamlLoaderException noXamlForm = Assert.Throws<XamlLoaderException>(() => Load(
            "<Button Content=\"Hi\"><Button.VisualStates><VisualStateDefinition Name=\"Hover\"><Setter Property=\"Background.Gradient\" Value=\"Red\" /></VisualStateDefinition></Button.VisualStates></Button>"));
        Assert.Contains("Background.Gradient", noXamlForm.Message);
    }

    [Fact]
    public void ADesktopImplicitStyle_WithAnimationOnly_IsMergedAndApplied()
    {
        MGDesktop desktop = NewDesktop();
        desktop.Resources.AddImplicitStyle(new XamlStyle { TargetType = MGElementType.Button, Transitions = { new Transition { Property = "Opacity", Duration = "0.3" } } });
        desktop.Resources.AddImplicitStyle(new XamlStyle
        {
            TargetType = MGElementType.Button,
            Transitions = { new Transition { Property = "opacity", Duration = "0.4" } },
            VisualStates = { new VisualStateDefinition { Name = "Hover", Setters = { new Setter { Property = "Opacity", Value = "0.5" } } } },
        });

        MGWindow window = Load(desktop, "<Button Name=\"B\" Content=\"Hi\" />");
        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.Equal(TimeSpan.FromMilliseconds(400), button.Transitions["Opacity"].Duration);
        Assert.Equal(0.5f, button.VisualStates["Hover"].Setters[0].Value);
    }

    [Fact]
    public void ADeclaredStateAndTransition_AnimateTheHover()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = Load(desktop, ButtonStyle +
            "<Button Name=\"B\" Content=\"Hi\" PreferredWidth=\"120\" PreferredHeight=\"40\" HorizontalAlignment=\"Left\" VerticalAlignment=\"Top\">" +
            "<Button.Transitions><Transition Property=\"RenderTransform.Scale\" Duration=\"160ms\" /></Button.Transitions>" +
            "</Button>");
        MGButton button = window.GetElementByName<MGButton>("B");
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        scene.Mouse = new Point(390, 290);
        scene.Frames(2);
        Assert.Equal(Vector2.One, button.RenderTransform.Scale);

        scene.Mouse = button.LayoutBounds.Center;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Hover, button.CurrentVisualStateName);
        scene.Frames(5);
        Assert.Equal(1.025f, button.RenderTransform.Scale.X, 1e-4f);
        scene.Frames(6);
        Assert.Equal(1.05f, button.RenderTransform.Scale.X, 1e-4f);
    }

    private static MGTheme EnabledTheme(MGTheme source)
    {
        MGTheme theme = source.Copy();
        theme.Animation.Enabled = true;
        return theme;
    }

    [Fact]
    public void TheTheme_InstallsTheHoverAndPressTransitions_OnButtons_AndUpdatesThemInPlace()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        // Off by default: an untouched button carries no animation slot (ADR-0006 cost principle).
        Assert.False(scene.Desktop.Theme.Animation.Enabled);
        Assert.Equal(0, scene.Top.Transitions.Count);

        scene.Desktop.Resources.DefaultTheme = EnabledTheme(scene.Desktop.Theme);
        UITransition hover = scene.Top.Transitions[UIBuiltInAnimationTargets.Paths.RenderScale];
        UITransition overlay = scene.Top.Transitions[UIExtraAnimationTargets.Paths.BackgroundOverlay];
        Assert.NotNull(hover);
        Assert.NotNull(overlay);
        Assert.Equal(TimeSpan.FromMilliseconds(120), hover.Duration);
        Assert.Same(UIEasing.CubicOut, hover.Easing);
        Assert.Equal(TimeSpan.FromMilliseconds(80), overlay.Duration);

        MGToggleButton toggle = new(scene.Window);
        Assert.NotNull(toggle.Transitions[UIBuiltInAnimationTargets.Paths.RenderScale]);
        Assert.NotNull(toggle.Transitions[UIExtraAnimationTargets.Paths.BackgroundOverlay]);

        MGTheme slower = scene.Desktop.Theme.Copy();
        Assert.Equal(TimeSpan.FromMilliseconds(120), slower.Animation.HoverDuration);
        slower.Animation.HoverDuration = TimeSpan.FromMilliseconds(250);
        slower.Animation.HoverEasing = "QuadIn";
        scene.Desktop.Resources.DefaultTheme = slower;

        Assert.Same(hover, scene.Top.Transitions[UIBuiltInAnimationTargets.Paths.RenderScale]);
        Assert.Equal(TimeSpan.FromMilliseconds(250), hover.Duration);
        Assert.Same(UIEasing.QuadIn, hover.Easing);
        Assert.Same(overlay, scene.Top.Transitions[UIExtraAnimationTargets.Paths.BackgroundOverlay]);
    }

    [Fact]
    public void TheTheme_RemovesItsTransitions_WhenDisabled_AndNeverTouchesTheApplicationsOwn()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UITransition<float> mine = new(UIBuiltInAnimationTargets.Paths.RenderScale, TimeSpan.FromMilliseconds(500));
        scene.Top.Transitions.Add(mine);
        scene.Desktop.Resources.DefaultTheme = EnabledTheme(scene.Desktop.Theme);
        Assert.Same(mine, scene.Top.Transitions[UIBuiltInAnimationTargets.Paths.RenderScale]);
        Assert.NotNull(scene.Top.Transitions[UIExtraAnimationTargets.Paths.BackgroundOverlay]);
        Assert.Equal(2, scene.Bottom.Transitions.Count);

        MGTheme disabled = scene.Desktop.Theme.Copy();
        disabled.Animation.Enabled = false;
        scene.Desktop.Resources.DefaultTheme = disabled;

        Assert.Same(mine, scene.Top.Transitions[UIBuiltInAnimationTargets.Paths.RenderScale]);
        Assert.Null(scene.Top.Transitions[UIExtraAnimationTargets.Paths.BackgroundOverlay]);
        Assert.Null(scene.Bottom.Transitions[UIBuiltInAnimationTargets.Paths.RenderScale]);
        Assert.Null(scene.Bottom.Transitions[UIExtraAnimationTargets.Paths.BackgroundOverlay]);
        Assert.Equal(0, scene.Bottom.Transitions.Count);

        MGTheme enabled = disabled.Copy();
        enabled.Animation.Enabled = true;
        scene.Desktop.Resources.DefaultTheme = enabled;

        Assert.Same(mine, scene.Top.Transitions[UIBuiltInAnimationTargets.Paths.RenderScale]);
        Assert.NotNull(scene.Top.Transitions[UIExtraAnimationTargets.Paths.BackgroundOverlay]);
        Assert.NotNull(scene.Bottom.Transitions[UIBuiltInAnimationTargets.Paths.RenderScale]);
    }

    [Fact]
    public void TheThemeOverlayTransition_FadesTheHoverOverlayIn()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Desktop.Resources.DefaultTheme = EnabledTheme(scene.Desktop.Theme);
        scene.Mouse = new Point(390, 290);
        scene.Frames(2);

        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(1);
        // The run applies its start value when it is created: no full-opacity overlay on the frame the hover is detected.
        Assert.Equal(0f, scene.Top.BackgroundBrush.OverlayOpacity, 1e-4f);
        scene.Frames(5);
        Assert.Equal(1f, scene.Top.BackgroundBrush.OverlayOpacity, 1e-4f);
    }

    [Fact]
    public void AThemeDefinition_DeclaresTheAnimationGroup_AndValidatesIt()
    {
        MGTheme theme = ThemeDefinitionBuilder.Build(new ThemeDefinition
        {
            Name = "Quick",
            Animation = new ThemeAnimationSettingsDefinition { HoverDuration = "0.25", PressDuration = "40ms", HoverEasing = "QuadIn", Enabled = true },
        }, "Arial");

        Assert.True(theme.Animation.Enabled);
        Assert.Equal(TimeSpan.FromMilliseconds(250), theme.Animation.HoverDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(40), theme.Animation.PressDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(120), theme.Animation.FocusDuration);
        Assert.Equal("QuadIn", theme.Animation.HoverEasing);
        Assert.Equal("CubicOut", theme.Animation.PressEasing);

        MGTheme derived = ThemeDefinitionBuilder.Build(new ThemeDefinition { Name = "Derived", Animation = new ThemeAnimationSettingsDefinition { PressEasing = "SineOut" } }, "Arial", theme);
        Assert.True(derived.Animation.Enabled);
        Assert.Equal(TimeSpan.FromMilliseconds(250), derived.Animation.HoverDuration);
        Assert.Equal("SineOut", derived.Animation.PressEasing);

        MGTheme copy = derived.Copy();
        Assert.Equal(derived.Animation.HoverDuration, copy.Animation.HoverDuration);
        Assert.Equal(derived.Animation.PressEasing, copy.Animation.PressEasing);
        Assert.True(copy.Animation.Enabled);
        Assert.False(ThemeDefinitionBuilder.Build(new ThemeDefinition { Name = "Plain" }, "Arial").Animation.Enabled);

        InvalidOperationException badEasing = Assert.Throws<InvalidOperationException>(() => ThemeDefinitionBuilder.Build(
            new ThemeDefinition { Name = "Bad", Animation = new ThemeAnimationSettingsDefinition { HoverEasing = "Wobbly" } }, "Arial"));
        Assert.Contains("Wobbly", badEasing.Message);
        InvalidOperationException badDuration = Assert.Throws<InvalidOperationException>(() => ThemeDefinitionBuilder.Build(
            new ThemeDefinition { Name = "Bad", Animation = new ThemeAnimationSettingsDefinition { PressDuration = "soon" } }, "Arial"));
        Assert.Contains("soon", badDuration.Message);

        Assert.Equal(UIThemeValueInvalidation.RenderOnly, UIThemeValueInvalidation.GetInvalidation("Animation.HoverDuration"));
        Assert.Equal(UIThemeValueInvalidation.RenderOnly, UIThemeValueInvalidation.GetInvalidation("Animation.Enabled"));
    }
}
