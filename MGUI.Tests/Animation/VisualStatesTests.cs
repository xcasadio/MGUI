using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.States;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Animation;

/// <summary>Slice T4 of Docs/Tasks/animation-v2-tasks.md: named visual states per element, Checked, priority, transitions on state setters.</summary>
public class VisualStatesTests
{
    private const float Tolerance = 1e-4f;

    private static readonly Point Away = new(390, 290);

    private static void DefineScaleStates(MGElement element)
    {
        element.VisualStates.Add(new UIVisualState(UIVisualStateNames.Normal) { { UIBuiltInAnimationTargets.Paths.RenderTransformScale, Vector2.One } });
        element.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { UIBuiltInAnimationTargets.Paths.RenderTransformScale, new Vector2(1.05f) } });
        element.VisualStates.Add(new UIVisualState(UIVisualStateNames.Pressed) { { UIBuiltInAnimationTargets.Paths.RenderTransformScale, new Vector2(0.96f) } });
    }

    [Fact]
    public void HoverThenPressThenLeave_ApplyAndRestoreTheStateValues()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Mouse = Away;
        scene.Frames(2);
        DefineScaleStates(scene.Top);
        scene.Top.RenderTransform.Origin = new Vector2(0.5f, 0.5f);
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Normal, scene.Top.CurrentVisualStateName);
        Assert.Equal(Vector2.One, scene.Top.RenderTransform.Scale);

        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Hover, scene.Top.CurrentVisualStateName);
        Assert.Equal(new Vector2(1.05f), scene.Top.RenderTransform.Scale);

        scene.Frames(1, leftPressed: true);
        Assert.Equal(UIVisualStateNames.Pressed, scene.Top.CurrentVisualStateName);
        Assert.Equal(new Vector2(0.96f), scene.Top.RenderTransform.Scale);

        scene.Mouse = Away;
        scene.Frames(2);
        Assert.Equal(UIVisualStateNames.Normal, scene.Top.CurrentVisualStateName);
        Assert.Equal(Vector2.One, scene.Top.RenderTransform.Scale);
    }

    [Fact]
    public void LeavingAStateWithoutANormalState_RestoresTheBaseValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Mouse = Away;
        scene.Frames(2);
        scene.Top.Opacity = 0.8f;
        scene.Top.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { UIBuiltInAnimationTargets.Paths.Opacity, 0.5f } });

        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(1);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        scene.Mouse = Away;
        scene.Frames(2);
        Assert.Null(scene.Top.CurrentVisualStateName);
        Assert.Equal(0.8f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Checked_OnAToggleButton_WritesThePilotUnderTheVisualStateSource()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        // The base colour is a default (0): a named state (70) outranks defaults, themes, styles and templates, not a local value (90).
        toggle.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(Color.Gray), UIValueResolutionSource.Default(UIInvalidationKind.Draw));
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } });
        scene.Mouse = Away;
        scene.Frames(2);
        Assert.Null(toggle.CurrentVisualStateName);

        toggle.IsChecked = true;
        scene.Frames(1);

        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.VisualState, source.Kind);
        Assert.Equal("visualstate:Checked", source.Name);

        toggle.IsChecked = false;
        scene.Frames(1);

        Assert.Null(toggle.CurrentVisualStateName);
        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource after));
        Assert.Equal(UIValueSourceKind.DefaultValue, after.Kind);
    }

    [Fact]
    public void LeavingAStateOnAThemeBackground_PutsTheThemeColourBack()
    {
        // The realistic case: the container was written whole by the theme (OnThemeChanged), the Normal slot has no contribution of its own.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.SetBackground(new VisualStateFillBrush(new MGSolidFillBrush(Color.Gray)), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } });
        scene.Mouse = Away;
        scene.Frames(2);
        Assert.Empty(toggle.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal));

        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);

        toggle.IsChecked = false;
        scene.Frames(1);
        Assert.Null(toggle.CurrentVisualStateName);
        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        // Put back as the kept value: no contribution recorded, so a theme refresh still replaces it.
        Assert.Empty(toggle.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal));

        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        toggle.VisualStates.Clear();
        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void LeavingAStateMidTransitionOnAThemeBackground_ComesBackToTheThemeColour()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.SetBackground(new VisualStateFillBrush(new MGSolidFillBrush(Color.Black)), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.White } });
        scene.Mouse = Away;
        scene.Frames(2);
        UITransition<Color> transition = new(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(160));
        toggle.Transitions.Add(transition);

        toggle.IsChecked = true;
        scene.Frames(6);
        Color midRun = Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color;
        Assert.Equal(Color.Lerp(Color.Black, Color.White, 0.5f), midRun);

        // U3: unchecked while the run holds the pilot -- the base recorded by the state exit (Theme, black) is picked up
        // as soon as the running interpolation ticks again (animations tick at the head of the frame, ADR-0006 decision 7,
        // one tick before the state exit itself is processed within the same frame), so the retarget lands two frames
        // after the exit, not once the whole run has finished: SettledValue already reports black, and the drawn colour
        // has neither jumped to white nor back to black, still sitting between the two.
        toggle.IsChecked = false;
        scene.Frames(2);
        Assert.Null(toggle.CurrentVisualStateName);
        Assert.Equal(Color.Black, transition.SettledValue);
        Assert.True(transition.IsRunning);
        Color justRetargeted = Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color;
        Assert.InRange(justRetargeted.R, (byte)1, (byte)254);

        // The very next frame already moves toward black (no jump, no lingering at the retarget's start value).
        scene.Frames(1);
        Color afterOneMoreFrame = Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color;
        Assert.True(afterOneMoreFrame.R < justRetargeted.R, $"{afterOneMoreFrame} should be closer to black than {justRetargeted}");

        scene.Frames(20);
        Assert.Equal(Color.Black, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.False(transition.IsRunning);

        // Re-entering and leaving again, after the run: the base is still the theme colour, not an in-flight or state value.
        toggle.IsChecked = true;
        scene.Frames(12);
        Assert.Equal(Color.White, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        toggle.IsChecked = false;
        scene.Frames(12);
        Assert.Equal(Color.Black, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void EnteringAStateMidTransitionStartedByAWholeContainerSwap_ComesBackToTheSwappedInColour()
    {
        // Fix round 1 (U3 regression): the transition run here is started by a Whole-slot container swap (a theme
        // change), not by a slot-level write, so when the Checked state is entered mid-run the Normal sub-slot's ONLY
        // contribution is the transition's own Animation entry -- there is nothing non-Animation to read "below" it.
        // CaptureBase must not surface the in-flight animated value as the base in that configuration.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.SetBackground(new VisualStateFillBrush(new MGSolidFillBrush(Color.Black)), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Red } });
        scene.Mouse = Away;
        scene.Frames(2);
        UITransition<Color> transition = new(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(160));
        toggle.Transitions.Add(transition);

        // The Whole swap (not a state, not a slot-level write) starts the run: the Normal sub-slot carries no
        // contribution of its own yet, only the transition's Animation entry once ticking begins.
        toggle.SetBackground(new VisualStateFillBrush(new MGSolidFillBrush(Color.White)), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        scene.Frames(6);
        Color midRun = Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color;
        Assert.NotEqual(Color.Black, midRun);
        Assert.NotEqual(Color.White, midRun);

        // Entering Checked records a Red contribution at VisualState precedence (70), but the run's own Animation
        // contribution (100) still outranks it, so the state stays dormant behind the run -- the physical value
        // keeps following the interpolation, untouched by the state entry. What matters here is the BASE captured
        // by Apply() at this moment (bases[_Path]), not the physical value, which is why it is not asserted here.
        toggle.IsChecked = true;
        scene.Frames(6);
        Color stillAnimating = Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color;
        Assert.NotEqual(Color.Red, stillAnimating);

        // Exiting the state while the run still holds the pilot: Restore() writes the captured base back under the
        // container's own (Theme) source, dormant beneath the still-running Animation contribution. Once the run
        // ends and clears its Animation contribution, that restored base becomes the winner -- it must be White
        // (the value the Whole swap headed to), never the in-flight value CaptureBase saw when Checked was entered.
        toggle.IsChecked = false;
        scene.Frames(30);
        Assert.Null(toggle.CurrentVisualStateName);
        Assert.False(transition.IsRunning);
        Assert.Equal(Color.White, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void ReenteringAStateMidTransition_KeepsTheTrueBase()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Mouse = Away;
        scene.Frames(2);
        scene.Top.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { UIBuiltInAnimationTargets.Paths.Opacity, 0.5f } });
        scene.Top.Transitions.Add(new UITransition<float>(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160)));

        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(11);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        scene.Mouse = Away;
        scene.Frames(6);
        Assert.Equal(0.75f, scene.Top.Opacity, Tolerance);

        // Back in while the fade-back is mid-flight: the base must stay 1, not the 0.75 the property shows right now.
        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(12);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        scene.Mouse = Away;
        scene.Frames(12);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void ALocalValue_ShadowsANamedStateOnAPilot()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } });
        scene.Mouse = Away;
        scene.Frames(2);

        toggle.IsChecked = true;
        scene.Frames(1);

        // The state is current and recorded (VisualState, 70), but the local value (90) keeps winning: documented limit of ADR-0007 decision 3.
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);
        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.LocalValue, source.Kind);
        Assert.Contains(toggle.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal), x => x.Source.Kind == UIValueSourceKind.VisualState);
    }

    [Fact]
    public void Disabled_OutranksChecked()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Checked) { { UIBuiltInAnimationTargets.Paths.Opacity, 0.9f } });
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Disabled) { { UIBuiltInAnimationTargets.Paths.Opacity, 0.4f } });
        scene.Mouse = Away;
        toggle.IsChecked = true;
        scene.Frames(2);
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);

        toggle.IsEnabled = false;
        scene.Frames(1);

        Assert.Equal(UIVisualStateNames.Disabled, toggle.CurrentVisualStateName);
        Assert.Equal(0.4f, toggle.Opacity, Tolerance);
    }

    [Fact]
    public void PressedWithoutAPressedState_FallsBackToHover()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Mouse = Away;
        scene.Frames(2);
        scene.Top.VisualStates.Add(new UIVisualState(UIVisualStateNames.Normal) { { UIBuiltInAnimationTargets.Paths.Opacity, 1f } });
        scene.Top.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { UIBuiltInAnimationTargets.Paths.Opacity, 0.7f } });
        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(1);

        scene.Frames(1, leftPressed: true);

        Assert.True(scene.Top.VisualState.IsPressed);
        Assert.Equal(UIVisualStateNames.Hover, scene.Top.CurrentVisualStateName);
        Assert.Equal(0.7f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void ATransitionOnASetterPath_InterpolatesTheStateChange()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Mouse = Away;
        scene.Frames(2);
        scene.Top.VisualStates.Add(new UIVisualState(UIVisualStateNames.Normal) { { UIBuiltInAnimationTargets.Paths.Opacity, 1f } });
        scene.Top.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { UIBuiltInAnimationTargets.Paths.Opacity, 0.5f } });
        scene.Frames(1);
        scene.Top.Transitions.Add(new UITransition<float>(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160)));

        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Hover, scene.Top.CurrentVisualStateName);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(0.75f, scene.Top.Opacity, Tolerance);
        scene.Frames(6);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        scene.Mouse = Away;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Normal, scene.Top.CurrentVisualStateName);
        scene.Frames(5);
        Assert.Equal(0.75f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void ATransitionOnAPilotSetter_InterpolatesAboveTheVisualStateSource()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(Color.Black), UIValueResolutionSource.Default(UIInvalidationKind.Draw));
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.White } });
        scene.Mouse = Away;
        scene.Frames(2);
        toggle.Transitions.Add(new UITransition<Color>(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(160)));

        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource during));
        Assert.Equal(UIValueSourceKind.Animation, during.Kind);
        scene.Frames(5);
        Assert.Equal(Color.Lerp(Color.Black, Color.White, 0.5f), Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        scene.Frames(6);

        Assert.Equal(Color.White, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource after));
        Assert.Equal(UIValueSourceKind.VisualState, after.Kind);
    }

    [Fact]
    public void ElementsWithoutStates_AreUntouched_AndSettersAreValidated()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Frames(2);
        Assert.Null(scene.Top.CurrentVisualStateName);
        Assert.Null(scene.Top.AnimationSlotOrNull);

        Assert.Throws<ArgumentException>(() => new UIVisualState(UIVisualStateNames.Hover).Add("Nope", 1f));
        Assert.Throws<ArgumentException>(() => new UIVisualState(UIVisualStateNames.Hover).Add(UIBuiltInAnimationTargets.Paths.Opacity, "1"));
        Assert.Throws<ArgumentException>(() => new UIVisualState(" "));

        UIVisualState state = new(UIVisualStateNames.Hover) { { UIBuiltInAnimationTargets.Paths.Opacity, 0.5f }, { UIBuiltInAnimationTargets.Paths.Opacity, 0.6f } };
        Assert.Single(state.Setters);
        Assert.Equal(0.6f, state.Setters[0].Value);
    }

    [Fact]
    public void RemovingTheCurrentState_RestoresIt_AndDisabling_Freezes()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Mouse = Away;
        scene.Frames(2);
        scene.Top.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { UIBuiltInAnimationTargets.Paths.Opacity, 0.5f } });
        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(1);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        scene.Top.VisualStates.IsEnabled = false;
        scene.Mouse = Away;
        scene.Frames(2);
        Assert.Equal(UIVisualStateNames.Hover, scene.Top.CurrentVisualStateName);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);

        Assert.True(scene.Top.VisualStates.Remove(UIVisualStateNames.Hover));
        Assert.Null(scene.Top.CurrentVisualStateName);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0, scene.Top.VisualStates.Count);
    }

    // U4 (ADR-0008, decision 4): UIVisualState.OverridesLocalValue, precedence VisualStateOverride (95).

    [Fact]
    public void AnOverridingState_WinsOverALocalValue_AndTheShadowedLocalValueReappearsOnExit()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        UIVisualState checkedState = new(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } };
        checkedState.OverridesLocalValue = true;
        toggle.VisualStates.Add(checkedState);
        scene.Mouse = Away;
        scene.Frames(2);

        toggle.IsChecked = true;
        scene.Frames(1);

        // The overriding state (95) now wins over the local value (90), unlike the plain state of ALocalValue_ShadowsANamedStateOnAPilot.
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.VisualState, source.Kind);
        Assert.Equal(UIValuePrecedence.VisualStateOverride, source.Precedence);

        // A local value written while the override is active stays underneath it (95 still wins). A TAGGED write is used here
        // (SetBackgroundSlot), not the raw NormalValue facade: an untagged sub-slot write is a documented limitation that always
        // applies physically regardless of precedence (MGElement.HandleBackgroundBrushContainerPropertyChanged), so it would not
        // exercise the precedence gate this test is about.
        toggle.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(Color.Blue), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        scene.Frames(1);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);

        // Leaving the state clears the 95 contribution and the local value written underneath shows.
        toggle.IsChecked = false;
        scene.Frames(1);
        Assert.Null(toggle.CurrentVisualStateName);
        Assert.Equal(Color.Blue, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.DoesNotContain(toggle.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal), x => x.Source.Kind == UIValueSourceKind.VisualState);
    }

    [Fact]
    public void AnOverridingState_WinsOverALocalBinding()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(Color.Gray), UIValueResolutionSource.LocalBinding(UIInvalidationKind.Draw));
        UIVisualState checkedState = new(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } };
        checkedState.OverridesLocalValue = true;
        toggle.VisualStates.Add(checkedState);
        scene.Mouse = Away;
        scene.Frames(2);

        toggle.IsChecked = true;
        scene.Frames(1);

        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.VisualState, source.Kind);
        Assert.Equal(UIValuePrecedence.VisualStateOverride, source.Precedence);
    }

    [Fact]
    public void LeavingAnOverridingStateForAPlainStateOnTheSamePath_NeverLeavesTheSeventyAboveALocalValue()
    {
        // Fix round 1 regression coverage: Apply() does not Restore a setter whose path the next state also sets
        // (UIVisualStateCollection.Apply), so a transition from an overriding state (95) straight to a plain state
        // (70) on the SAME path replaces the 95 contribution in place instead of going through Unset. Before the
        // fix, UIResolvedPropertyStore.Set overwrote that slot without re-sorting, so the 70 write inherited the
        // 95 write's position above LocalValue (90) -- this test drives exactly that transition and checks the
        // sort order survives it in both directions.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        UIVisualState checkedState = new(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } };
        checkedState.OverridesLocalValue = true;
        UIVisualState hoverState = new(UIVisualStateNames.Hover) { { UIColorAnimationTargets.Paths.Background, Color.Gray } };
        toggle.VisualStates.Add(checkedState);
        toggle.VisualStates.Add(hoverState);
        scene.Mouse = Away;
        scene.Frames(2);

        // Enter the overriding Checked state: it wins over the local value below it (95 > 90).
        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);

        // A tagged local write lands underneath the 95 contribution while Checked stays active.
        toggle.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(Color.Blue), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        scene.Frames(1);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);

        // Hovering while still checked changes nothing: Checked outranks Hover in Resolve()'s priority order.
        scene.Mouse = toggle.LayoutBounds.Center;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);

        // Leaving Checked for Hover -- both declare Background, so Apply() goes straight from one setter to the
        // other on the same path without an intervening Restore/Unset. The plain Hover write (70) must sort BELOW
        // the tagged local value (90): the local value, not Hover's grey, must show.
        toggle.IsChecked = false;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Hover, toggle.CurrentVisualStateName);
        Assert.Equal(Color.Blue, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource afterTransition));
        Assert.Equal(UIValueSourceKind.LocalValue, afterTransition.Kind);

        var contributions = toggle.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal);
        Assert.Equal(2, contributions.Count);
        Assert.Equal(UIValueSourceKind.LocalValue, contributions[0].Source.Kind);
        Assert.Equal(UIValueSourceKind.VisualState, contributions[1].Source.Kind);
        Assert.Equal(UIValuePrecedence.VisualState, contributions[1].Source.Precedence);

        // Leaving Hover restores it; re-entering the overriding Checked state on top of the same local value must
        // land the 95 contribution back above it (the reverse direction of the same in-place-replacement bug).
        scene.Mouse = Away;
        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource reentered));
        Assert.Equal(UIValueSourceKind.VisualState, reentered.Kind);
        Assert.Equal(UIValuePrecedence.VisualStateOverride, reentered.Precedence);
    }

    [Fact]
    public void TheWinnerRewritingItsOwnUnchangedValue_StillResyncsAStaleUntaggedSubField()
    {
        // Fix round 2 regression coverage: the four tagged sub-slot physical-write gates (SetBackgroundSlot,
        // SetBackgroundFocusedColor, SetDefaultTextForegroundSlot, MGTextBlock.SetForegroundSlot) gated the physical
        // write on `effectiveChanged` alone (fix round 1). That is not a strict superset of the pre-U4 kind-match
        // gate: when the current winner rewrites its own, value-equal, contribution (e.g. re-applying an animation's
        // resting value), effectiveChanged is false, so a stale value written by an UNTAGGED facade call in between
        // (which applies physically but records a dormant contribution under the current winner) never gets
        // corrected. Using the value-compared FocusedColor sub-slot (Color?), which is not shielded by
        // ReferenceEqualityComparer the way brush sub-slots mostly are.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);

        // Physical FocusedColor = Red, tagged as an Animation-kind contribution.
        toggle.SetBackgroundFocusedColor(Color.Red, UIValueResolutionSource.Animation(UIInvalidationKind.Draw));
        Assert.Equal(Color.Red, toggle.BackgroundBrush.FocusedColor);

        // An untagged facade write applies Blue physically and records a dormant LocalValue contribution underneath
        // the still-winning Animation contribution.
        toggle.BackgroundBrush.FocusedColor = Color.Blue;
        Assert.Equal(Color.Blue, toggle.BackgroundBrush.FocusedColor);

        // The winning Animation kind rewrites its own, unchanged, value: effectiveChanged is false, but the
        // physical sub-field must still be re-synced to the applicable winner (Red), not left on the stale Blue.
        toggle.SetBackgroundFocusedColor(Color.Red, UIValueResolutionSource.Animation(UIInvalidationKind.Draw));
        Assert.Equal(Color.Red, toggle.BackgroundBrush.FocusedColor);
    }

    [Fact]
    public void WithoutTheFlag_ThePrecedenceReportedStaysSeventy()
    {
        // Extends ALocalValue_ShadowsANamedStateOnAPilot with the precedence-level assertion: unchanged (70) when the flag is not set.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } });
        scene.Mouse = Away;
        scene.Frames(2);

        toggle.IsChecked = true;
        scene.Frames(1);

        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.VisualState, source.Kind);
        Assert.Equal(UIValuePrecedence.VisualState, source.Precedence);
    }

    [Fact]
    public void AnAnimation_StillOutranksAnOverridingState_AndTheStatesColourShowsAfterRestore()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        UIVisualState checkedState = new(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } };
        checkedState.OverridesLocalValue = true;
        toggle.VisualStates.Add(checkedState);
        scene.Mouse = Away;
        scene.Frames(2);

        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);

        UIPropertyAnimation<Color> flash = new(UIColorAnimationTargets.Paths.Background)
        {
            To = Color.Red,
            Duration = TimeSpan.FromMilliseconds(160),
            FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
            Name = "flash",
        };
        toggle.Animations.Start(flash);
        scene.Frames(1);

        // Animation (100) still outranks a VisualStateOverride (95) contribution.
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource during));
        Assert.Equal(UIValueSourceKind.Animation, during.Kind);

        scene.Frames(12); // 160 ms at 16 ms/frame: enough for RestoreBaseValue to clear the Animation contribution.

        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource after));
        Assert.Equal(UIValueSourceKind.VisualState, after.Kind);
        Assert.Equal(UIValuePrecedence.VisualStateOverride, after.Precedence);
    }

    [Fact]
    public void TogglingTheFlagBetweenTwoApplications_ChangesThePrecedenceWritten()
    {
        // The applier caches its source keyed by state name reference; since re-entering the SAME UIVisualState instance keeps that
        // reference identical, the cache must also invalidate on the flag itself (ACCEPTANCE probe 3).
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        UIVisualState checkedState = new(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } };
        toggle.VisualStates.Add(checkedState);
        scene.Mouse = Away;
        scene.Frames(2);

        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource first));
        Assert.Equal(UIValuePrecedence.VisualState, first.Precedence);

        toggle.IsChecked = false;
        scene.Frames(1);
        checkedState.OverridesLocalValue = true;
        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource second));
        Assert.Equal(UIValuePrecedence.VisualStateOverride, second.Precedence);

        toggle.IsChecked = false;
        scene.Frames(1);
        checkedState.OverridesLocalValue = false;
        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.True(UIToolingService.TryGetResolvedValueSource(toggle, "Background", out UIValueResolutionSource third));
        Assert.Equal(UIValuePrecedence.VisualState, third.Precedence);
    }

    [Fact]
    public void EnteringAndLeavingAnOverridingStateRepeatedly_LeavesNoGrowthInTheContributions()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        toggle.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        UIVisualState checkedState = new(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } };
        checkedState.OverridesLocalValue = true;
        toggle.VisualStates.Add(checkedState);
        scene.Mouse = Away;
        scene.Frames(2);

        for (var i = 0; i < 10; i++)
        {
            toggle.IsChecked = true;
            scene.Frames(1);
            Assert.Equal(2, toggle.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal).Count); // LocalValue + VisualState(95)

            toggle.IsChecked = false;
            scene.Frames(1);
            Assert.Equal(1, toggle.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal).Count); // LocalValue only
        }
    }

    [Fact]
    public void AnOverridingStateActiveAndIdle_AllocatesNothingPerRefresh()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGToggleButton toggle = new(scene.Window);
        scene.Panel.TryAddChild(toggle);
        UIVisualState checkedState = new(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } };
        checkedState.OverridesLocalValue = true;
        toggle.VisualStates.Add(checkedState);
        scene.Mouse = Away;
        scene.Frames(2);

        toggle.IsChecked = true;
        scene.Frames(1);
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);

        // Warm up, then refresh the collection directly (no state change: the name comparison in Refresh short-circuits before Apply),
        // isolating the per-frame cost of an idle overriding state from unrelated desktop-update noise.
        for (var i = 0; i < 5; i++)
        {
            toggle.VisualStates.Refresh();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 200; i++)
        {
            toggle.VisualStates.Refresh();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);
    }

    [Fact]
    public void XamlRoundTrip_TransfersTheFlag_OnAnElementAndOnAStyle_DefaultingToFalse()
    {
        const string xmlns = "xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\"";
        GraphTestRuntime runtime = new(new Microsoft.Xna.Framework.Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        string xaml = $"<Window {xmlns} Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\" WindowStyle=\"None\">" +
            "<Window.Styles><Style TargetType=\"Button\" Name=\"Overriding\">" +
            "<Style.VisualStates><VisualStateDefinition Name=\"Checked\" OverridesLocalValue=\"True\"><Setter Property=\"Background\" Value=\"Green\" /></VisualStateDefinition></Style.VisualStates>" +
            "</Style></Window.Styles>" +
            "<StackPanel>" +
            "<Button Name=\"Flagged\" Content=\"a\"><Button.VisualStates><VisualStateDefinition Name=\"Checked\" OverridesLocalValue=\"True\"><Setter Property=\"Background\" Value=\"Green\" /></VisualStateDefinition></Button.VisualStates></Button>" +
            "<Button Name=\"Plain\" Content=\"b\"><Button.VisualStates><VisualStateDefinition Name=\"Checked\"><Setter Property=\"Background\" Value=\"Green\" /></VisualStateDefinition></Button.VisualStates></Button>" +
            "<Button Name=\"Styled\" Content=\"c\" StyleNames=\"Overriding\" />" +
            "</StackPanel></Window>";
        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml), XamlLoaderMode.Strict, false, true);

        Assert.True(window.GetElementByName<MGButton>("Flagged").VisualStates["Checked"].OverridesLocalValue);
        Assert.False(window.GetElementByName<MGButton>("Plain").VisualStates["Checked"].OverridesLocalValue);
        Assert.True(window.GetElementByName<MGButton>("Styled").VisualStates["Checked"].OverridesLocalValue);

        // A typo'd attribute is rejected in strict mode exactly like any other unknown attribute (StyleAndThemeAnimationTests.InvalidStateSetters_AreLoaderDiagnostics).
        string typo = $"<Window {xmlns} Left=\"0\" Top=\"0\" Width=\"400\" Height=\"300\" WindowStyle=\"None\">" +
            "<Button Content=\"a\"><Button.VisualStates><VisualStateDefinition Name=\"Checked\" OverridesLocalValues=\"True\">" +
            "<Setter Property=\"Background\" Value=\"Green\" /></VisualStateDefinition></Button.VisualStates></Button></Window>";
        XamlLoaderException error = Assert.Throws<XamlLoaderException>(
            () => XAMLParser.LoadRootWindow(new MGDesktop(new GraphTestRuntime(new Microsoft.Xna.Framework.Rectangle(0, 0, 800, 600))), XamlDocumentSource.FromString(typo), XamlLoaderMode.Strict, false, true));
        Assert.Contains("OverridesLocalValues", error.Message);
    }
}
