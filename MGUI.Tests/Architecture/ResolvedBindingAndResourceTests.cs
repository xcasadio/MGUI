using System;
using System.ComponentModel;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.DataBinding;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Behavioural coverage of the S8 wiring of ADR-0005's remaining two write paths onto the pilot properties:
/// <see cref="MGUI.Core.UI.Styling.UIResourceReferenceApplicator"/> ({DynamicResource}/{StaticResource}) and
/// <see cref="DataBinding"/> ({MGBinding}). Both now route a pilot-targeting write through
/// <see cref="MGUI.Core.UI.Styling.UIPilotPropertyResolver"/> instead of plain reflection, so the resulting
/// contribution is tagged <see cref="UIValueSourceKind.DynamicResource"/> or <see cref="UIValueSourceKind.LocalBinding"/>
/// and participates in the store's usual precedence instead of unconditionally overwriting whatever the CLR
/// property currently holds.
/// </summary>
public class ResolvedBindingAndResourceTests
{
    /// <summary>(1) A DynamicResource targeting a scalar pilot (<c>Border.Padding</c>): wins as
    /// <see cref="UIValueSourceKind.DynamicResource"/> (30), tracks resource updates, is beaten by a subsequent
    /// LocalValue write (the S8-accepted behavior change -- a dynamic resource update no longer overwrites a later
    /// local write), and re-emerges once the LocalValue contribution is cleared.</summary>
    [Fact]
    public void DynamicResource_Padding_Wins_Tracks_Updates_Then_Loses_To_LocalValue_Then_Reemerges_On_Clear()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Border Name=""B"" Padding=""{DynamicResource P}"" />
</Window>";
        harness.Desktop.Resources.AddStaticResource("P", new Thickness(4));

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder b = window.GetElementByName<MGBorder>("B");
        Assert.Equal(new Thickness(4), b.Padding);
        Assert.True(b.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner1));
        Assert.Equal(UIValueSourceKind.DynamicResource, winner1.Source.Kind);

        harness.Desktop.Resources.SetStaticResource("P", new Thickness(6));
        Assert.Equal(new Thickness(6), b.Padding);

        b.Padding = new Thickness(9); // LocalValue(90) beats DynamicResource(30)
        Assert.Equal(new Thickness(9), b.Padding);
        Assert.True(b.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner2));
        Assert.Equal(UIValueSourceKind.LocalValue, winner2.Source.Kind);

        // S8-accepted behavior change: a later DynamicResource update no longer clobbers the local write.
        harness.Desktop.Resources.SetStaticResource("P", new Thickness(7));
        Assert.Equal(new Thickness(9), b.Padding);
        Assert.True(b.TryGetResolvedContribution<Thickness>(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.DynamicResource, out UIResolvedValue<Thickness> dynamicContribution));
        Assert.Equal(new Thickness(7), dynamicContribution.Value);

        b.ClearPilotSource(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.LocalValue);
        Assert.Equal(new Thickness(7), b.Padding);
        Assert.True(b.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner3));
        Assert.Equal(UIValueSourceKind.DynamicResource, winner3.Source.Kind);
    }

    /// <summary>(2) S8-accepted behavior change: removing the resource behind a DynamicResource falls back to the
    /// next-highest-precedence contribution instead of leaving a stale winning value. Uses <c>TextBox</c>, whose
    /// own catalogue default (ADR-0005/S7a) is tagged <see cref="UIValueSourceKind.Theme"/> (20), so the fallback
    /// is observable and distinct from the resource's value.</summary>
    [Fact]
    public void DynamicResource_Removal_FallsBackToTheme()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <TextBox Name=""T"" Padding=""{DynamicResource P}"" />
</Window>";
        harness.Desktop.Resources.AddStaticResource("P", new Thickness(4));

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGTextBox t = window.GetElementByName<MGTextBox>("T");
        Assert.Equal(new Thickness(4), t.Padding);
        Assert.True(t.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.DynamicResource, winner.Source.Kind);

        Assert.True(t.TryGetResolvedContribution<Thickness>(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<Thickness> themeContribution));
        Thickness themeDefault = themeContribution.Value;
        Assert.NotEqual(new Thickness(4), themeDefault);

        Assert.True(harness.Desktop.Resources.RemoveStaticResource("P"));

        Assert.Equal(themeDefault, t.Padding);
        Assert.True(t.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winnerAfterRemoval));
        Assert.Equal(UIValueSourceKind.Theme, winnerAfterRemoval.Source.Kind);
        Assert.False(t.TryGetResolvedContribution<Thickness>(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.DynamicResource, out _));
    }

    /// <summary>(3) A data binding targeting a scalar pilot (<c>Border.Padding</c>): wins as
    /// <see cref="UIValueSourceKind.LocalBinding"/> (80), is beaten by a subsequent LocalValue(90) write (documented
    /// here, unlike DynamicResource this ordering was already correct pre-S8 since LocalBinding already outranked
    /// nothing above it), keeps updating its own contribution when the bound source property changes even while it
    /// isn't winning, and re-emerges once the LocalValue contribution is cleared.</summary>
    [Fact]
    public void Binding_Padding_Wins_As_LocalBinding_Loses_To_LocalValue_Keeps_Updating_Then_Reemerges_On_Clear()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        BindingViewModel vm = new() { Pad = new Thickness(3) };
        border.DataContextOverride = vm;

        DataBindingManager.AddBinding(new BindingConfig("Padding", nameof(BindingViewModel.Pad)), border);

        Assert.Equal(new Thickness(3), border.Padding);
        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner1));
        Assert.Equal(UIValueSourceKind.LocalBinding, winner1.Source.Kind);

        border.Padding = new Thickness(9); // LocalValue(90) beats LocalBinding(80)
        Assert.Equal(new Thickness(9), border.Padding);
        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner2));
        Assert.Equal(UIValueSourceKind.LocalValue, winner2.Source.Kind);

        vm.Pad = new Thickness(5); // still updates the LocalBinding contribution even though it isn't winning
        Assert.Equal(new Thickness(9), border.Padding);
        Assert.True(border.TryGetResolvedContribution<Thickness>(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.LocalBinding, out UIResolvedValue<Thickness> bindingContribution));
        Assert.Equal(new Thickness(5), bindingContribution.Value);

        border.ClearPilotSource(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.LocalValue);
        Assert.Equal(new Thickness(5), border.Padding);
        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner3));
        Assert.Equal(UIValueSourceKind.LocalBinding, winner3.Source.Kind);
    }

    /// <summary>(4) A container sub-slot (<c>Button.Background</c>, mapped to <c>BackgroundBrush.NormalValue</c>)
    /// driven by a DynamicResource: wins the (Background, Normal) slot as <see cref="UIValueSourceKind.DynamicResource"/>,
    /// and survives a real theme change the same way an ImplicitStyle contribution does (R2 re-application onto the
    /// new container the theme swaps in, since DynamicResource(30) outranks the Theme(20) write).</summary>
    [Fact]
    public void DynamicResource_Background_SubSlot_Wins_And_Survives_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Button Name=""B"" Background=""{DynamicResource Br}"" />
</Window>";
        harness.Desktop.Resources.AddStaticResource("Br", new MGSolidFillBrush(Color.Lime));

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGButton b = window.GetElementByName<MGButton>("B");
        Assert.IsType<MGSolidFillBrush>(b.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Lime, ((MGSolidFillBrush)b.BackgroundBrush.NormalValue).Color);

        Assert.True(b.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> winner));
        Assert.Equal(UIValueSourceKind.DynamicResource, winner.Source.Kind);

        window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Desktop.Update();

        Assert.IsType<MGSolidFillBrush>(b.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Lime, ((MGSolidFillBrush)b.BackgroundBrush.NormalValue).Color);
    }

    /// <summary>(5) A StaticResource ({StaticResource}, not dynamic) targeting a scalar pilot resolves as
    /// <see cref="UIValueSourceKind.LocalValue"/> -- matching <c>UIPilotPropertyResolver.TrySetTagged</c>'s
    /// non-dynamic branch and the pre-S8 behavior where a static reference is just a one-time value assignment.</summary>
    [Fact]
    public void StaticResource_Margin_Resolves_As_LocalValue()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Border Name=""B"" Margin=""{StaticResource M}"" />
</Window>";
        harness.Desktop.Resources.AddStaticResource("M", new Thickness(2));

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder b = window.GetElementByName<MGBorder>("B");
        Assert.Equal(new Thickness(2), b.Margin);
        Assert.True(b.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.LocalValue, winner.Source.Kind);
    }

    /// <summary>(6) A DynamicResource targeting a non-pilot property (<c>Opacity</c>) is entirely unaffected by S8:
    /// <see cref="MGUI.Core.UI.Styling.UIPilotPropertyResolver.TryResolve"/> returns false for that path, so the
    /// applicator falls back to its pre-existing reflection write and keeps tracking updates.</summary>
    [Fact]
    public void DynamicResource_On_NonPilot_Property_Still_Uses_Reflection_And_Tracks_Updates()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Border Name=""B"" Opacity=""{DynamicResource O}"" />
</Window>";
        harness.Desktop.Resources.AddStaticResource("O", 0.5f);

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder b = window.GetElementByName<MGBorder>("B");
        Assert.Equal(0.5f, b.Opacity);

        harness.Desktop.Resources.SetStaticResource("O", 0.25f);
        Assert.Equal(0.25f, b.Opacity);
    }

    private sealed class BindingViewModel : INotifyPropertyChanged
    {
        private Thickness _pad;
        public Thickness Pad
        {
            get => _pad;
            set
            {
                _pad = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Pad)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 480, 260)
            {
                WindowStyle = WindowStyle.None,
                Padding = new Thickness(0),
            };
            Harness harness = new(runtime, desktop, window);
            harness.Frame(0, Point.Zero);
            return harness;
        }

        public void Frame(int frameIndex, Point mousePosition)
        {
            MouseState mouse = new(mousePosition.X, mousePosition.Y, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
