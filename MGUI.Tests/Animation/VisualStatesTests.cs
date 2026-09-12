using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.States;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Styling;
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
        toggle.Transitions.Add(new UITransition<Color>(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(160)));

        toggle.IsChecked = true;
        scene.Frames(6);
        Assert.Equal(Color.Lerp(Color.Black, Color.White, 0.5f), Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);

        // Unchecked while the run holds the pilot: the write is not seen until the run ends (known limit), then the run back to black starts.
        toggle.IsChecked = false;
        scene.Frames(5);
        // The run reached white at its end, saw the recorded base and started back: the new run is not advanced on the tick that creates it.
        Assert.Equal(Color.White, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.True(toggle.Transitions[UIColorAnimationTargets.Paths.Background].IsRunning);
        scene.Frames(10);
        Assert.Equal(Color.Black, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        Assert.False(toggle.Transitions[UIColorAnimationTargets.Paths.Background].IsRunning);

        // Re-entering and leaving again, after the run: the base is still the theme colour, not an in-flight or state value.
        toggle.IsChecked = true;
        scene.Frames(12);
        Assert.Equal(Color.White, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
        toggle.IsChecked = false;
        scene.Frames(12);
        Assert.Equal(Color.Black, Assert.IsType<MGSolidFillBrush>(toggle.BackgroundBrush.NormalValue).Color);
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
}
