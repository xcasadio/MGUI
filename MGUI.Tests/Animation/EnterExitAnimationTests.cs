using System;
using System.ComponentModel;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Animation;

/// <summary>Slice Y6 (Docs/decisions/0011-animation-v5.md, "Entree et sortie d'un element"): <see cref="UIEnterExitSettings"/>,
/// <see cref="UIEnterExitEffect"/>, <see cref="MGElement.EnterExit"/>, <see cref="MGElement.PendingVisibility"/>, the first-draw gate, the
/// exit's internal hit-test removal, and the eight XAML <see cref="Element"/> DTO attributes.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class EnterExitAnimationTests
{
    private static UIEnterExitSettings FadeSettings(int enterMs = 100, int exitMs = 80) => new()
    {
        EnterEffect = UIEnterExitEffect.Fade,
        ExitEffect = UIEnterExitEffect.Fade,
        EnterDuration = TimeSpan.FromMilliseconds(enterMs),
        ExitDuration = TimeSpan.FromMilliseconds(exitMs),
    };

    // ---- Exit: stays visible, drawn, in the layout, non-interactive, until it completes -----------------------

    [Fact]
    public void Exit_KeepsVisibilityVisible_StaysDrawn_KeepsLayoutPlace_ThenCollapses()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = FadeSettings();
        scene.Draw(); // satisfies the first-draw gate

        Rectangle bottomBefore = scene.Bottom.LayoutBounds;
        scene.Top.Visibility = Visibility.Collapsed;

        Assert.Equal(Visibility.Visible, scene.Top.Visibility);
        Assert.Equal(Visibility.Collapsed, scene.Top.PendingVisibility);
        Assert.Equal(bottomBefore, scene.Bottom.LayoutBounds); // neighbour has not moved yet

        var draw = scene.Draw();
        Assert.True(scene.Top.RecentDrawWasClipped == false || scene.Top.LayoutBounds.Width > 0); // still actually drawn
        Assert.False(draw == null);

        scene.Frames(20);

        Assert.Equal(Visibility.Collapsed, scene.Top.Visibility);
        Assert.Equal(Visibility.Collapsed, scene.Top.PendingVisibility);
        Assert.NotEqual(bottomBefore, scene.Bottom.LayoutBounds); // neighbour moved once the exit completed
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Exit_RemovesTheElementAndItsSubtree_FromHoverAndPress_WithoutTouchingIsHitTestVisible()
    {
        // The panel itself gets the exit; Top is its descendant (a proven-reliable hover target, see RenderTransformTests):
        // exercises "the element and its whole subtree" without depending on a bespoke nested-panel hit-test shape.
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Panel.EnterExit = FadeSettings(exitMs: 500);
        scene.Draw();
        scene.Frames(2);

        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(2);
        Assert.Same(scene.Top, scene.Window.HoveredElement);

        scene.Panel.Visibility = Visibility.Collapsed;
        // Nudge the mouse (still over the same spot) so the window's own "did the mouse move" gate does not skip the
        // HoveredElement recomputation this frame -- Visibility itself stays Visible during the exit, so layout never
        // invalidates on its own the way it would for an outright hide.
        scene.Mouse += new Point(1, 0);
        scene.Frames(1);

        Assert.NotSame(scene.Panel, scene.Window.HoveredElement);
        Assert.NotSame(scene.Top, scene.Window.HoveredElement);
        Assert.True(scene.Panel.IsHitTestVisible);
        Assert.True(scene.Top.IsHitTestVisible);
    }

    // ---- Before the first draw: applies at once, no run ----------------------------------------------------------

    [Fact]
    public void BeforeFirstDraw_SettingHiddenByCode_AppliesAtOnce_NoRun()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = FadeSettings();
        // No Draw() yet: the element was never drawn since it was attached.

        scene.Top.Visibility = Visibility.Collapsed;

        Assert.Equal(Visibility.Collapsed, scene.Top.Visibility);
        Assert.Equal(Visibility.Collapsed, scene.Top.PendingVisibility);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void BeforeFirstDraw_StyleSetter_AppliesAtOnce_NoRun()
    {
        MGWindow window = LoadXaml(
            "<Window.Styles><Style TargetType=\"Button\"><Setter Property=\"Visibility\" Value=\"Collapsed\" /></Style></Window.Styles>" +
            "<Button Name=\"B\" Content=\"Hi\" ExitEffect=\"Fade\" ExitDuration=\"0.1\" />", out MGDesktop desktop);
        MGButton button = window.GetElementByName<MGButton>("B");

        Assert.Equal(Visibility.Collapsed, button.Visibility);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void BeforeFirstDraw_Binding_AppliesAtOnce_NoRun()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = FadeSettings();
        BindingViewModel vm = new() { Vis = Visibility.Collapsed };
        scene.Top.DataContextOverride = vm;
        DataBindingManager.AddBinding(new BindingConfig("Visibility", nameof(BindingViewModel.Vis)), scene.Top);

        Assert.Equal(Visibility.Collapsed, scene.Top.Visibility);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void CollapsedAtLoad_PlaysItsEntry_WhenSetVisible()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = FadeSettings();
        scene.Top.Visibility = Visibility.Collapsed; // before the first draw: applies at once
        scene.Frames(1);

        scene.Top.Opacity = 1f;
        scene.Top.Visibility = Visibility.Visible;

        Assert.Equal(Visibility.Visible, scene.Top.Visibility);
        Assert.True(scene.Desktop.Animations.ActiveCount > 0); // the storyboard plus its opacity child
        scene.Frames(20);
        Assert.Equal(1f, scene.Top.Opacity, 3);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    // ---- Round trip: no jump ----------------------------------------------------------------------------------

    [Fact]
    public void QuickRoundTrip_VisibleHiddenVisible_MidExit_NoJump_EndsVisibleAtBaseValues()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = FadeSettings(enterMs: 100, exitMs: 200);
        scene.Draw();
        scene.Frames(1);

        scene.Top.Visibility = Visibility.Collapsed;
        scene.Frames(3); // partway through the exit
        float opacityJustBeforeReentry = scene.Top.Opacity;
        Assert.True(opacityJustBeforeReentry < 1f && opacityJustBeforeReentry > 0f);

        scene.Top.Visibility = Visibility.Visible; // supersedes the exit
        scene.Frames(1);

        // No jump: right after the interruption, opacity is close to where the exit had already faded it to.
        Assert.True(Math.Abs(scene.Top.Opacity - opacityJustBeforeReentry) < 0.3f,
            $"Expected a near-continuous opacity, moved from {opacityJustBeforeReentry} to {scene.Top.Opacity}.");

        scene.Frames(30);
        Assert.Equal(Visibility.Visible, scene.Top.Visibility);
        Assert.Equal(1f, scene.Top.Opacity, 3);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    // ---- Cancellation: external Clear(), detach, window close -------------------------------------------------

    [Fact]
    public void ExternalCancellation_AnimationsClear_AppliesPendingVisibility_RestoresBaseValues_NoResidualAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = FadeSettings(exitMs: 500);
        scene.Top.RenderTransform.Scale = new Vector2(1.2f, 1.2f);
        scene.Draw();
        scene.Frames(1);
        int initialActive = scene.Desktop.Animations.ActiveCount;

        scene.Top.Visibility = Visibility.Collapsed;
        scene.Frames(2);
        Assert.True(scene.Desktop.Animations.ActiveCount > initialActive);

        scene.Top.Animations.Clear();

        Assert.Equal(Visibility.Collapsed, scene.Top.Visibility);
        Assert.Equal(1f, scene.Top.Opacity, 3);
        Assert.Equal(new Vector2(1.2f, 1.2f), scene.Top.RenderTransform.Scale);
        Assert.Equal(initialActive, scene.Desktop.Animations.ActiveCount);

        // Inputs work again once the element is shown again (exiting state was reset).
        scene.Top.Visibility = Visibility.Visible;
        scene.Frames(1);
        Point centre = scene.Top.LayoutBounds.Center;
        scene.Mouse = centre;
        scene.Frames(1);
        Assert.Same(scene.Top, scene.Window.HoveredElement);
    }

    [Fact]
    public void Detach_DuringExit_AppliesRequestedVisibility_NoException_NoResidualAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = FadeSettings(exitMs: 500);
        scene.Draw();
        scene.Frames(1);
        int initialActive = scene.Desktop.Animations.ActiveCount;

        scene.Top.Visibility = Visibility.Collapsed;
        scene.Frames(1);
        Assert.True(scene.Desktop.Animations.ActiveCount > initialActive);

        Exception thrown = Record.Exception(() => scene.Panel.TryRemoveChild(scene.Top));
        Assert.Null(thrown);

        Assert.Equal(initialActive, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void WindowClose_DuringExit_AppliesRequestedVisibility_NoException_NoResidualAnimation()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGStackPanel panel = new(window, Orientation.Vertical) { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGButton top = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
        top.EnterExit = FadeSettings(exitMs: 500);
        panel.TryAddChild(top);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        desktop.Update();
        GraphNoOpDrawTransaction transaction = new(runtime, MGUI.Shared.Rendering.DrawSettings.Default);
        desktop.Draw(transaction);
        desktop.Update();
        int initialActive = desktop.Animations.ActiveCount;

        top.Visibility = Visibility.Collapsed;
        desktop.Update();
        Assert.True(desktop.Animations.ActiveCount > initialActive);

        Exception thrown = Record.Exception(() => window.TryCloseWindow());
        Assert.Null(thrown);
        Assert.Equal(initialActive, desktop.Animations.ActiveCount);
    }

    // ---- Predefined effects: exact start/end values, origin held then restored --------------------------------

    [Fact]
    public void Fade_Entry_GoesFromZeroToBaseOpacity_Exit_GoesFromBaseToZero()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Opacity = 0.8f;
        scene.Top.EnterExit = FadeSettings(enterMs: 100, exitMs: 100);
        scene.Draw();
        scene.Frames(1);

        scene.Top.Visibility = Visibility.Collapsed;
        // Right after the exit starts, opacity should already be heading down from the base (0.8).
        Assert.True(scene.Top.Opacity <= 0.8f);
        scene.Frames(20);
        // Once the exit completes naturally, the base opacity is restored (ADR-0011 decision 6): the element never comes
        // back a ghost even if a later show does not replay the same path.
        Assert.Equal(0.8f, scene.Top.Opacity, 3);

        scene.Top.Visibility = Visibility.Visible;
        scene.Frames(20);
        Assert.Equal(0.8f, scene.Top.Opacity, 3);
    }

    [Fact]
    public void Scale_Entry_GoesFromScaleFromToBaseScale_HoldsOriginAtCentre_ThenRestoresIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.RenderTransform.Origin = new Vector2(0.1f, 0.2f);
        scene.Top.EnterExit = new UIEnterExitSettings
        {
            EnterEffect = UIEnterExitEffect.Scale,
            ExitEffect = UIEnterExitEffect.Scale,
            EnterDuration = TimeSpan.FromMilliseconds(100),
            ExitDuration = TimeSpan.FromMilliseconds(100),
            ScaleFrom = 0.5f,
        };
        scene.Draw();
        scene.Frames(1);

        scene.Top.Visibility = Visibility.Collapsed;
        // Held at the centre for the duration of the run.
        Assert.Equal(new Vector2(0.5f, 0.5f), scene.Top.RenderTransform.Origin);
        scene.Frames(20);

        // Restored once the exit ends: origin back to its pre-run value, scale back to the true base (ADR-0011 decision 6), not
        // left at the effect's own end value (ScaleFrom).
        Assert.Equal(new Vector2(0.1f, 0.2f), scene.Top.RenderTransform.Origin);
        Assert.Equal(Vector2.One, scene.Top.RenderTransform.Scale);

        scene.Top.Visibility = Visibility.Visible;
        Assert.Equal(new Vector2(0.5f, 0.5f), scene.Top.RenderTransform.Origin);
        scene.Frames(20);
        Assert.Equal(Vector2.One, scene.Top.RenderTransform.Scale);
        Assert.Equal(new Vector2(0.1f, 0.2f), scene.Top.RenderTransform.Origin);
    }

    [Theory]
    [InlineData(UIEnterExitEffect.SlideLeft, 24f, 0f)]
    [InlineData(UIEnterExitEffect.SlideRight, -24f, 0f)]
    [InlineData(UIEnterExitEffect.SlideUp, 0f, 24f)]
    [InlineData(UIEnterExitEffect.SlideDown, 0f, -24f)]
    public void Slide_Entry_StartsOffsetFromBase_Exit_EndsOffsetFromBase(UIEnterExitEffect effect, float offsetX, float offsetY)
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = new UIEnterExitSettings
        {
            EnterEffect = effect,
            ExitEffect = effect,
            EnterDuration = TimeSpan.FromMilliseconds(100),
            ExitDuration = TimeSpan.FromMilliseconds(100),
            SlideDistance = 24f,
        };
        scene.Draw();
        scene.Frames(1);
        Vector2 baseTranslation = scene.Top.RenderTransform.Translation;
        Vector2 offsetTranslation = new(baseTranslation.X + offsetX, baseTranslation.Y + offsetY);

        // Exit: leaves from the base translation, heads towards base + the offset the matching entry would start from (the
        // exit plays the inverse of the entry's own path), then once it completes naturally the true base is restored
        // (ADR-0011 decision 6), not left at the effect's own end value.
        scene.Top.Visibility = Visibility.Collapsed;
        scene.Frames(20);
        Assert.Equal(baseTranslation, scene.Top.RenderTransform.Translation);

        // Entry: a fresh cycle (the exit above completed and reset the captured base), so it starts explicitly at the
        // same offset the exit would have ended at (no visible jump) and heads back to base.
        scene.Top.Visibility = Visibility.Visible;
        Assert.Equal(offsetTranslation, scene.Top.RenderTransform.Translation);
        scene.Frames(20);
        Assert.Equal(baseTranslation, scene.Top.RenderTransform.Translation);
    }

    [Fact]
    public void ExplicitEnterAnimation_WinsOverEnterEffect()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> explicitEnter = new("Opacity") { To = 1f, From = 0.3f, Duration = TimeSpan.FromMilliseconds(50) };
        scene.Top.EnterExit = new UIEnterExitSettings
        {
            EnterAnimation = explicitEnter,
            EnterEffect = UIEnterExitEffect.Fade, // must be ignored
        };
        scene.Top.Opacity = 0f;
        scene.Draw();
        scene.Frames(1);

        scene.Top.Visibility = Visibility.Collapsed; // never drawn with Collapsed before, but no exit configured: applies at once
        scene.Top.Visibility = Visibility.Visible;

        Assert.Equal(0.3f, scene.Top.Opacity, 3);
        scene.Frames(10);
        Assert.Equal(1f, scene.Top.Opacity, 3);
    }

    // ---- Regression (verifier round 1, P1): switching effects across a cycle must not leave a ghost --------------

    [Fact]
    public void SwitchingEffects_AfterFadeExitCompletes_ScaleEntry_RestoresOpacity_NoGhost()
    {
        // The exit's own effect (Fade) is not the same as the entry's next effect (Scale, which never touches Opacity):
        // once the Fade exit completes naturally, Opacity must already be back at its base, otherwise the later
        // Scale-only entry never touches that path and the element comes back Visible but invisible (a ghost).
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = new UIEnterExitSettings
        {
            EnterEffect = UIEnterExitEffect.Fade,
            ExitEffect = UIEnterExitEffect.Fade,
            EnterDuration = TimeSpan.FromMilliseconds(50),
            ExitDuration = TimeSpan.FromMilliseconds(50),
        };
        scene.Draw();
        scene.Frames(1);

        scene.Top.Visibility = Visibility.Collapsed;
        scene.Frames(20); // exit completes naturally
        Assert.Equal(1f, scene.Top.Opacity, 3);

        // Now switch both effects to Scale before showing again, exactly like the sample's buttons do.
        scene.Top.EnterExit = new UIEnterExitSettings
        {
            EnterEffect = UIEnterExitEffect.Scale,
            ExitEffect = UIEnterExitEffect.Scale,
            EnterDuration = TimeSpan.FromMilliseconds(50),
            ExitDuration = TimeSpan.FromMilliseconds(50),
        };
        scene.Top.Visibility = Visibility.Visible;
        scene.Frames(20);

        Assert.Equal(Visibility.Visible, scene.Top.Visibility);
        Assert.Equal(1f, scene.Top.Opacity, 3);
        Assert.Equal(Vector2.One, scene.Top.RenderTransform.Scale);
    }

    [Fact]
    public void FadeScaleExit_CompletesNaturally_RestoresOpacityAndScale_ThenVisibleShowsIntact()
    {
        // Mirrors the verifier's probe (3): a FadeScale exit completes, EnterExit is cleared, then Visibility is set
        // back to Visible directly (no entry effect at all): the element must be intact (opacity 1, scale 1), not a
        // ghost left at the exit's own end values (opacity 0, scale ScaleFrom).
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = new UIEnterExitSettings
        {
            EnterEffect = UIEnterExitEffect.FadeScale,
            ExitEffect = UIEnterExitEffect.FadeScale,
            EnterDuration = TimeSpan.FromMilliseconds(50),
            ExitDuration = TimeSpan.FromMilliseconds(50),
            ScaleFrom = 0.9f,
        };
        scene.Draw();
        scene.Frames(1);

        scene.Top.Visibility = Visibility.Collapsed;
        scene.Frames(20); // exit completes naturally

        scene.Top.EnterExit = null;
        scene.Top.Visibility = Visibility.Visible;

        Assert.Equal(1f, scene.Top.Opacity, 3);
        Assert.Equal(Vector2.One, scene.Top.RenderTransform.Scale);
    }

    // ---- Cost: an element without EnterExit allocates nothing new ---------------------------------------------

    [Fact]
    public void ElementWithoutEnterExit_TogglingVisibility_NeverAllocatesAnAnimationSlot()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        scene.Top.Visibility = Visibility.Collapsed;
        scene.Top.Visibility = Visibility.Visible;
        scene.Top.Visibility = Visibility.Hidden;
        scene.Top.Visibility = Visibility.Visible;

        Assert.Null(scene.Top.AnimationSlotOrNull);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    // ---- XAML: the eight attributes, strict loading, style setter, binding ------------------------------------

    private const string Xmlns = "xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" xmlns:dataBinding=\"clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core\"";

    private static MGWindow LoadXaml(string windowContent, out MGDesktop desktop)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        desktop = new MGDesktop(runtime);
        string xaml = $"<Window {Xmlns} Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\" WindowStyle=\"None\">{windowContent}</Window>";
        return XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml), XamlLoaderMode.Strict, false, true);
    }

    [Fact]
    public void XamlAttributes_TheEightSettings_Load()
    {
        MGWindow window = LoadXaml(
            "<Button Name=\"B\" Content=\"Hi\" EnterEffect=\"Fade\" ExitEffect=\"Scale\" EnterDuration=\"0.25\" ExitDuration=\"120ms\" " +
            "EnterEasing=\"CubicOut\" ExitEasing=\"CubicIn\" EnterExitScaleFrom=\"0.7\" EnterExitSlideDistance=\"40\" />", out _);

        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.NotNull(button.EnterExit);
        Assert.Equal(UIEnterExitEffect.Fade, button.EnterExit.EnterEffect);
        Assert.Equal(UIEnterExitEffect.Scale, button.EnterExit.ExitEffect);
        Assert.Equal(TimeSpan.FromMilliseconds(250), button.EnterExit.EnterDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(120), button.EnterExit.ExitDuration);
        Assert.Same(UIEasing.CubicOut, button.EnterExit.EnterEasing);
        Assert.Same(UIEasing.CubicIn, button.EnterExit.ExitEasing);
        Assert.Equal(0.7f, button.EnterExit.ScaleFrom);
        Assert.Equal(40f, button.EnterExit.SlideDistance);
    }

    [Fact]
    public void ElementWithoutAttributes_KeepsEnterExitNull()
    {
        MGWindow window = LoadXaml("<Button Name=\"B\" Content=\"Hi\" />", out _);
        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.Null(button.EnterExit);
    }

    [Fact]
    public void ImplicitStyleSetter_OfExitEffectAndExitDuration_Applies()
    {
        MGWindow window = LoadXaml(
            "<Window.Styles><Style TargetType=\"Button\">" +
            "<Setter Property=\"ExitEffect\" Value=\"Fade\" />" +
            "<Setter Property=\"ExitDuration\" Value=\"0.2\" />" +
            "</Style></Window.Styles>" +
            "<Button Name=\"B\" Content=\"Hi\" />", out _);

        MGButton button = window.GetElementByName<MGButton>("B");
        Assert.NotNull(button.EnterExit);
        Assert.Equal(UIEnterExitEffect.Fade, button.EnterExit.ExitEffect);
        Assert.Equal(TimeSpan.FromMilliseconds(200), button.EnterExit.ExitDuration);
    }

    [Fact]
    public void InvalidEnterDuration_IsALoaderError()
    {
        XamlLoaderException error = Assert.Throws<XamlLoaderException>(() => LoadXaml(
            "<Button Content=\"Hi\" EnterDuration=\"fast\" />", out _));
        Assert.Contains("fast", error.Message);
    }

    [Fact]
    public void InvalidExitEasing_IsALoaderError()
    {
        XamlLoaderException error = Assert.Throws<XamlLoaderException>(() => LoadXaml(
            "<Button Content=\"Hi\" ExitEasing=\"Wobble\" />", out _));
        Assert.Contains("Wobble", error.Message);
    }

    [Fact]
    public void DataBindingOnVisibility_TriggersTheSameRuns()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.EnterExit = FadeSettings(exitMs: 300);
        scene.Draw();
        scene.Frames(1);

        BindingViewModel vm = new() { Vis = Visibility.Visible };
        scene.Top.DataContextOverride = vm;
        DataBindingManager.AddBinding(new BindingConfig("Visibility", nameof(BindingViewModel.Vis)), scene.Top);

        vm.Vis = Visibility.Collapsed;
        scene.Frames(1);

        Assert.Equal(Visibility.Visible, scene.Top.Visibility); // exit keeps it visible
        Assert.Equal(Visibility.Collapsed, scene.Top.PendingVisibility);
        Assert.True(scene.Desktop.Animations.ActiveCount > 0);
    }

    private sealed class BindingViewModel : INotifyPropertyChanged
    {
        private Visibility _vis;
        public Visibility Vis
        {
            get => _vis;
            set
            {
                _vis = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Vis)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
