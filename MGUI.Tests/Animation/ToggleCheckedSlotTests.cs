using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation.States;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Animation;

/// <summary>Slice U5 of Docs/Tasks/animation-v3-tasks.md: <see cref="VisualStateBrush{TDataType}.CheckedValue"/>/<see cref="VisualStateBrush{TDataType}.HasCheckedValue"/>
/// (ADR-0008) and <see cref="MGToggleButton.CheckedBackgroundBrush"/>/<see cref="MGToggleButton.CheckedTextForeground"/>, the real slots backed by it.<para/>
/// <see cref="MGCheckBox"/> and <see cref="MGRadioButton"/> are untouched by this slice (their checked look is drawn by child elements, not
/// <see cref="MGElement.BackgroundBrush"/>/<see cref="MGElement.DefaultTextForeground"/>): their existing test suites stay green with no new
/// coverage needed here.</summary>
public class ToggleCheckedSlotTests
{
    private static readonly UpdateBaseArgs OneFrame = new(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default);

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 300));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
            return new(runtime, desktop, window);
        }

        /// <summary>Adds the toggle as the window's content, shows the window and runs two warm-up frames so layout and theme application are settled.</summary>
        public MGToggleButton ShowToggle(MGToggleButton toggle)
        {
            Window.SetContent(toggle);
            Desktop.Windows.Add(Window);
            Frame();
            Frame();
            return toggle;
        }

        public void Frame(int milliseconds = 16)
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(milliseconds), TimeSpan.FromMilliseconds(milliseconds), default, default));
            Desktop.Update();
        }

        public GraphNoOpDrawTransaction Draw()
        {
            GraphNoOpDrawTransaction transaction = new(Runtime, DrawSettings.Default);
            Desktop.Draw(transaction);
            return transaction;
        }
    }

    /// <summary>The last <see cref="GraphFillRectangleCall"/> whose colour is one of <paramref name="candidates"/> (the test's own,
    /// otherwise-unused colours) -- isolates the toggle's background fill from the window's own background/border draws.</summary>
    private static Color LastMatchingFillColor(GraphNoOpDrawTransaction transaction, params Color[] candidates)
        => transaction.FillRectangleCalls.Last(call => candidates.Contains(call.Color)).Color;

    #region CheckedBackgroundBrush: drawn underlay

    [Fact]
    public void CheckedBackgroundBrush_IsDrawnWhileChecked_AndTheNormalBrushWhileUnchecked_AcrossFiveToggles()
    {
        Harness harness = Harness.Create();
        Color normalColor = Color.MediumBlue, selectedColor = Color.Goldenrod, disabledColor = Color.DimGray, checkedColor = Color.Crimson;
        Color[] candidates = { normalColor, selectedColor, disabledColor, checkedColor };

        MGToggleButton toggle = harness.ShowToggle(new MGToggleButton(harness.Window) { PreferredWidth = 40, PreferredHeight = 20, BorderThickness = new Thickness(0) });
        toggle.BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(normalColor), new MGSolidFillBrush(selectedColor), new MGSolidFillBrush(disabledColor),
            null, PressedModifierType.Darken, 0f);
        toggle.CheckedBackgroundBrush = new MGSolidFillBrush(checkedColor);
        harness.Frame();

        for (int i = 0; i < 5; i++)
        {
            toggle.IsChecked = true;
            harness.Frame();
            Assert.Equal(checkedColor, LastMatchingFillColor(harness.Draw(), candidates));

            toggle.IsChecked = false;
            harness.Frame();
            Assert.Equal(normalColor, LastMatchingFillColor(harness.Draw(), candidates));
        }

        // Clearing the checked brush while checked falls back to SelectedValue on the next draw.
        toggle.IsChecked = true;
        harness.Frame();
        Assert.Equal(checkedColor, LastMatchingFillColor(harness.Draw(), candidates));

        toggle.CheckedBackgroundBrush = null;
        Assert.False(toggle.BackgroundBrush.HasCheckedValue);
        harness.Frame();
        Assert.Equal(selectedColor, LastMatchingFillColor(harness.Draw(), candidates));
    }

    [Fact]
    public void WithOnlySelectedValueSet_ACheckedToggleDrawsExactlyWhatGetUnderlayOfSelectedReturns_AsBeforeThisSlice()
    {
        Harness harness = Harness.Create();
        Color normalColor = Color.SeaGreen, selectedColor = Color.OrangeRed, disabledColor = Color.Black;
        Color[] candidates = { normalColor, selectedColor, disabledColor };

        MGToggleButton toggle = harness.ShowToggle(new MGToggleButton(harness.Window) { PreferredWidth = 40, PreferredHeight = 20, BorderThickness = new Thickness(0) });
        toggle.BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(normalColor), new MGSolidFillBrush(selectedColor), new MGSolidFillBrush(disabledColor),
            null, PressedModifierType.Darken, 0f);
        harness.Frame();

        Assert.False(toggle.BackgroundBrush.HasCheckedValue);
        MGSolidFillBrush expected = (MGSolidFillBrush)toggle.BackgroundBrush.GetUnderlay(PrimaryVisualState.Selected);

        toggle.IsChecked = true;
        harness.Frame();
        Assert.Equal(expected.Color, LastMatchingFillColor(harness.Draw(), candidates));
        Assert.Equal(selectedColor, expected.Color);
    }

    #endregion

    #region IsChecked / IsSelected / named state Checked

    [Fact]
    public void IsChecked_StillSetsIsSelected_AndTheNamedStateCheckedStillResolves()
    {
        Harness harness = Harness.Create();
        MGToggleButton toggle = harness.ShowToggle(new MGToggleButton(harness.Window));
        toggle.VisualStates.Add(new UIVisualState(UIVisualStateNames.Checked) { { UIColorAnimationTargets.Paths.Background, Color.Green } });
        harness.Frame();

        toggle.IsChecked = true;
        harness.Frame();
        Assert.True(toggle.IsSelected);
        Assert.Equal(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);

        toggle.IsChecked = false;
        harness.Frame();
        Assert.False(toggle.IsSelected);
        Assert.NotEqual(UIVisualStateNames.Checked, toggle.CurrentVisualStateName);
    }

    #endregion

    #region CheckedTextForeground

    [Fact]
    public void CheckedTextForeground_Set_MakesTheResolvedSelectedSlotThatColour_Unset_RestoresTheThemesSelectedColour()
    {
        Harness harness = Harness.Create();
        MGToggleButton toggle = harness.ShowToggle(new MGToggleButton(harness.Window));
        Color? themeColor = toggle.DefaultTextForeground.SelectedValue;

        Color checkedColor = Color.HotPink;
        toggle.CheckedTextForeground = checkedColor;

        Assert.Equal(checkedColor, toggle.DefaultTextForeground.SelectedValue);
        Assert.True(toggle.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Selected, out UIResolvedValue<Color?> resolved));
        Assert.Equal(checkedColor, resolved.Value);

        toggle.CheckedTextForeground = null;
        Assert.Equal(themeColor, toggle.DefaultTextForeground.SelectedValue);
    }

    #endregion

    #region Theme change keeps a code-set checked value

    [Fact]
    public void RealThemeChange_KeepsACodeSetCheckedBackgroundBrush_AndACodeSetCheckedTextForeground()
    {
        Harness harness = Harness.Create();
        MGToggleButton toggle = harness.ShowToggle(new MGToggleButton(harness.Window));

        MGSolidFillBrush checkedBrush = new(Color.Fuchsia);
        Color checkedText = Color.Fuchsia;
        toggle.CheckedBackgroundBrush = checkedBrush;
        toggle.CheckedTextForeground = checkedText;
        harness.Frame();

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100);
        harness.Frame();

        Assert.True(toggle.BackgroundBrush.HasCheckedValue);
        Assert.Equal(checkedBrush.Color, ((MGSolidFillBrush)toggle.BackgroundBrush.CheckedValue).Color);
        Assert.Equal(checkedText, toggle.CheckedTextForeground);
        Assert.Equal(checkedText, toggle.DefaultTextForeground.SelectedValue);
    }

    [Fact]
    public void RealThemeChange_ATogglesWithoutCodeSetValues_TakesTheNewThemesSelectedColour()
    {
        Harness harness = Harness.Create();
        MGToggleButton toggle = harness.ShowToggle(new MGToggleButton(harness.Window));

        Color? beforeThemeSelected = toggle.DefaultTextForeground.SelectedValue;

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100);
        harness.Frame();

        Assert.False(toggle.BackgroundBrush.HasCheckedValue);
        // The un-overridden checked text colour tracks whatever the theme's selected colour is (a Dark theme fallback
        // foreground need not literally differ from Dark_Blue's, so this only asserts the toggle followed the theme,
        // not the theme's own value): the property still resolves to DefaultTextForeground.SelectedValue.
        Assert.Equal(toggle.DefaultTextForeground.SelectedValue, toggle.CheckedTextForeground);
        Assert.Equal(toggle.GetTheme().TextBlockFallbackForeground.GetValue(true).NormalValue, toggle.DefaultTextForeground.SelectedValue);
    }

    #endregion

    #region VisualStateFillBrush/VisualStateColorBrush copy carries CheckedValue

    [Fact]
    public void VisualStateFillBrush_Copy_CarriesCheckedValueAndHasCheckedValue()
    {
        VisualStateFillBrush brush = new(new MGSolidFillBrush(Color.Blue), new MGSolidFillBrush(Color.Yellow), new MGSolidFillBrush(Color.Gray),
            null, PressedModifierType.Darken, 0f);
        Assert.False(brush.Copy().HasCheckedValue);

        brush.CheckedValue = new MGSolidFillBrush(Color.Red);
        VisualStateFillBrush copy = brush.Copy();

        Assert.True(copy.HasCheckedValue);
        Assert.Equal(Color.Red, ((MGSolidFillBrush)copy.CheckedValue).Color);
        Assert.NotSame(brush.CheckedValue, copy.CheckedValue); // deep-copied like the other four slots, not aliased

        brush.ClearCheckedValue();
        Assert.False(brush.HasCheckedValue);
        Assert.True(copy.HasCheckedValue); // the copy is unaffected by clearing the original
    }

    [Fact]
    public void VisualStateColorBrush_Copy_CarriesCheckedValueAndHasCheckedValue()
    {
        VisualStateColorBrush brush = new(Color.Blue, Color.Yellow, Color.Gray, null, PressedModifierType.Darken, 0f);
        Assert.False(brush.Copy().HasCheckedValue);

        brush.CheckedValue = Color.Red;
        VisualStateColorBrush copy = brush.Copy();

        Assert.True(copy.HasCheckedValue);
        Assert.Equal(Color.Red, copy.CheckedValue);

        brush.ClearCheckedValue();
        Assert.False(brush.HasCheckedValue);
        Assert.True(copy.HasCheckedValue);
    }

    [Fact]
    public void GetValue_WithIsChecked_FallsBackToSelectedValue_WhenNoCheckedValueIsSet_OrWhenNotChecked()
    {
        VisualStateColorBrush brush = new(Color.Blue, Color.Yellow, Color.Gray, null, PressedModifierType.Darken, 0f);

        Assert.Equal(Color.Yellow, brush.GetValue(PrimaryVisualState.Selected, true));
        Assert.Equal(Color.Blue, brush.GetValue(PrimaryVisualState.Normal, true));
        Assert.Equal(Color.Blue, brush.GetValue(PrimaryVisualState.Normal, false));

        brush.CheckedValue = Color.Red;
        Assert.Equal(Color.Red, brush.GetValue(PrimaryVisualState.Normal, true));
        Assert.Equal(Color.Blue, brush.GetValue(PrimaryVisualState.Normal, false));

        VisualState state = new(PrimaryVisualState.Normal, SecondaryVisualState.None);
        Assert.Equal(Color.Red, brush.GetValue(state, true));
    }

    #endregion

    #region No per-frame allocation

    /// <summary>Same warm-up-then-measure shape as <c>VisualStatesTests</c>'s allocation guard: isolates the per-call cost of the new
    /// checked-aware read path (<see cref="VisualStateBrush{TDataType}.GetValue(PrimaryVisualState, bool)"/>, what <see cref="MGToggleButton"/>'s
    /// <c>ResolveBackgroundUnderlay</c> override calls every frame) from unrelated desktop-update noise. A full <c>Desktop.Draw</c> pass is not
    /// used here because its allocation profile is dominated by unrelated framework work this slice does not touch.</summary>
    [Fact]
    public void GetValue_WithIsChecked_AllocatesNothingPerCall()
    {
        VisualStateFillBrush brush = new(new MGSolidFillBrush(Color.Blue), new MGSolidFillBrush(Color.Yellow), new MGSolidFillBrush(Color.Gray),
            null, PressedModifierType.Darken, 0f);
        brush.CheckedValue = new MGSolidFillBrush(Color.Red);

        for (int i = 0; i < 5; i++)
        {
            _ = brush.GetValue(PrimaryVisualState.Normal, true);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            _ = brush.GetValue(PrimaryVisualState.Normal, true);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    #endregion

    #region Fix round 1: whole-container BackgroundBrush swap after CheckedBackgroundBrush is set

    /// <summary>Verifier-refuted P2, fix round 1: reproduces the exact reported scenario -- replacing the whole <see cref="MGElement.BackgroundBrush"/>
    /// container (as <see cref="MGExpander.ExpanderButtonBackgroundBrush"/>'s setter or a pilot/style Whole-slot resolution would) after a code-set
    /// <see cref="MGToggleButton.CheckedBackgroundBrush"/> is in effect. Before the fix, the checked value was silently dropped from the fresh container
    /// while the property getter kept returning the stale one, so the drawn colour and the property disagreed. After the fix
    /// (<see cref="MGElement.OnBackgroundBrushContainerReplaced"/>, overridden by the toggle to re-apply the toggle-level field on every swap), both
    /// the drawn colour and the property must reflect the checked brush.</summary>
    [Fact]
    public void ReassigningBackgroundBrush_AfterCheckedBrushIsSet_KeepsTheCheckedBrushDrawnAndReported()
    {
        Harness harness = Harness.Create();
        Color checkedColor = Color.Crimson, normalColor = Color.MediumBlue, selectedColor = Color.Goldenrod, disabledColor = Color.DimGray;
        Color[] candidates = { checkedColor, normalColor, selectedColor, disabledColor };

        MGToggleButton toggle = harness.ShowToggle(new MGToggleButton(harness.Window) { PreferredWidth = 40, PreferredHeight = 20, BorderThickness = new Thickness(0) });
        toggle.CheckedBackgroundBrush = new MGSolidFillBrush(checkedColor);
        toggle.IsChecked = true;

        // Whole-container swap: the same public API MGExpander.ExpanderButtonBackgroundBrush's setter and the pilot/style Whole-slot
        // resolution use (MGElement.BackgroundBrush's public setter), independent of MGToggleButton's own CheckedBackgroundBrush setter.
        toggle.BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(normalColor), new MGSolidFillBrush(selectedColor), new MGSolidFillBrush(disabledColor),
            null, PressedModifierType.Darken, 0f);
        harness.Frame();

        Color drawn = LastMatchingFillColor(harness.Draw(), candidates);
        Assert.True(toggle.BackgroundBrush.HasCheckedValue);
        Assert.Equal(checkedColor, ((MGSolidFillBrush)toggle.CheckedBackgroundBrush).Color);
        Assert.Equal(checkedColor, drawn);
    }

    #endregion

    #region Fix round 2: CheckedTextForeground precedence, and a null BackgroundBrush must not throw

    /// <summary>Verifier-refuted P1, fix round 2: the reverse ordering of <see cref="RealThemeChange_KeepsACodeSetCheckedBackgroundBrush_AndACodeSetCheckedTextForeground"/> --
    /// a real theme change happens FIRST (recording a <see cref="UIValueSourceKind.Theme"/> contribution on the Selected sub-slot, the only
    /// writer of that contribution for a toggle), THEN <see cref="MGToggleButton.CheckedTextForeground"/> is set. Before the fix, the code-set
    /// write was recorded at <see cref="UIValueResolutionSource.Default"/>'s precedence (lower than <see cref="UIValueResolutionSource.Theme"/>'s),
    /// so it could never become the resolved winner: the getter (backed by the toggle field) reported the code colour while the resolved/physical
    /// Selected slot -- and therefore the drawn text -- stayed the theme's colour. After the fix (a code-set value is written at
    /// <see cref="UIValuePrecedence.LocalValue"/>), both must agree regardless of write order.</summary>
    [Fact]
    public void RealThemeChange_AfterCheckedTextForegroundIsSetFirst_TheCodeColourStillWins()
    {
        Harness harness = Harness.Create();
        MGToggleButton toggle = harness.ShowToggle(new MGToggleButton(harness.Window));

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100);
        harness.Frame();

        Color checkedColor = Color.HotPink;
        toggle.CheckedTextForeground = checkedColor;
        harness.Frame();

        Assert.Equal(checkedColor, toggle.CheckedTextForeground);
        Assert.Equal(checkedColor, toggle.DefaultTextForeground.SelectedValue);
        Assert.True(toggle.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Selected, out UIResolvedValue<Color?> resolved));
        Assert.Equal(checkedColor, resolved.Value);

        // Unsetting still restores the theme's fallback colour -- the leftover LocalValue contribution must not stick around as the winner.
        Color? themeSelected = toggle.GetTheme().TextBlockFallbackForeground.GetValue(true).NormalValue;
        toggle.CheckedTextForeground = null;
        Assert.Equal(themeSelected, toggle.DefaultTextForeground.SelectedValue);
    }

    /// <summary>Verifier-refuted P2, fix round 2: reproduces the exact reported crash -- assigning <c>BackgroundBrush = null</c> on any
    /// <see cref="MGToggleButton"/> threw a <see cref="NullReferenceException"/> from <c>SyncCheckedBackgroundValue</c> (invoked unconditionally
    /// by <see cref="MGElement.OnBackgroundBrushContainerReplaced"/> on every whole-container swap, including one that resolves to null), even
    /// for a toggle that never used <see cref="MGToggleButton.CheckedBackgroundBrush"/>.</summary>
    [Fact]
    public void BackgroundBrush_SetToNull_DoesNotThrow_EvenAfterCheckedBackgroundBrushWasUsed()
    {
        Harness harness = Harness.Create();
        MGToggleButton toggle = harness.ShowToggle(new MGToggleButton(harness.Window));

        toggle.BackgroundBrush = null;
        harness.Frame();
        Assert.Null(toggle.BackgroundBrush);

        toggle.BackgroundBrush = new VisualStateFillBrush(new MGSolidFillBrush(Color.MediumBlue), new MGSolidFillBrush(Color.Goldenrod),
            new MGSolidFillBrush(Color.DimGray), null, PressedModifierType.Darken, 0f);
        toggle.CheckedBackgroundBrush = new MGSolidFillBrush(Color.Crimson);
        harness.Frame();

        toggle.BackgroundBrush = null;
        harness.Frame();
        Assert.Null(toggle.BackgroundBrush);
    }

    #endregion
}
