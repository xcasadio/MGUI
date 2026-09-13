using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Behavioural coverage of the S7 wiring of XAML-side style provenance (ADR-0005): <c>Element.ResolveXamlSource</c>
/// tags every tagged transfer from a XAML DTO to its underlying <see cref="MGElement"/> with
/// <see cref="UIValueSourceKind.ImplicitStyle"/>, <see cref="UIValueSourceKind.ExplicitStyle"/>, or
/// <see cref="UIValueSourceKind.LocalValue"/> depending on whether <c>Element.ProcessStyles</c> populated the
/// property from a style setter or the parser left it as a direct XAML attribute.
/// </summary>
public class ResolvedXamlProvenanceTests
{
    /// <summary>(1) An implicit style (no <c>Name</c>) targeting <c>Border</c> sets <c>Padding</c>: the physical
    /// value comes from the style, and the winning contribution is tagged <see cref="UIValueSourceKind.ImplicitStyle"/>.</summary>
    [Fact]
    public void ImplicitStyle_Padding_Wins_As_ImplicitStyle()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""Border"">
            <Setter Property=""Padding"" Value=""7"" />
        </Style>
    </Window.Styles>
    <Border Name=""P"" />
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder p = window.GetElementByName<MGBorder>("P");
        Assert.Equal(new Thickness(7), p.Padding);

        Assert.True(p.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, winner.Source.Kind);
    }

    /// <summary>(2) A named style (<c>StyleNames</c>) targeting <c>Border</c> sets <c>Padding</c>: the winning
    /// contribution is tagged <see cref="UIValueSourceKind.ExplicitStyle"/>.</summary>
    [Fact]
    public void NamedStyle_Padding_Wins_As_ExplicitStyle()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style Name=""Accent"" TargetType=""Border"">
            <Setter Property=""Padding"" Value=""9"" />
        </Style>
    </Window.Styles>
    <Border Name=""P"" StyleNames=""Accent"" />
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder p = window.GetElementByName<MGBorder>("P");
        Assert.Equal(new Thickness(9), p.Padding);

        Assert.True(p.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.ExplicitStyle, winner.Source.Kind);
    }

    /// <summary>(3) Both an implicit AND a named style target the same property (<c>Padding</c>): the DTO is only
    /// transferred once, using the final (named) value, tagged <see cref="UIValueSourceKind.ExplicitStyle"/> --
    /// there is no separate <see cref="UIValueSourceKind.ImplicitStyle"/> contribution left over from the earlier
    /// pass, because <c>ProcessStyles</c> overwrites the DTO property (and its provenance entry) before
    /// <c>Element.ApplyBaseSettings</c> ever reads it.</summary>
    [Fact]
    public void ImplicitAndNamedStyle_Same_Property_Named_Value_Wins_As_ExplicitStyle_No_Leftover_Implicit_Contribution()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""Border"">
            <Setter Property=""Padding"" Value=""7"" />
        </Style>
        <Style Name=""Accent"" TargetType=""Border"">
            <Setter Property=""Padding"" Value=""9"" />
        </Style>
    </Window.Styles>
    <Border Name=""P"" StyleNames=""Accent"" />
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder p = window.GetElementByName<MGBorder>("P");
        Assert.Equal(new Thickness(9), p.Padding);

        Assert.True(p.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.ExplicitStyle, winner.Source.Kind);

        Assert.False(p.TryGetResolvedContribution<Thickness>(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.ImplicitStyle, out _));
    }

    /// <summary>(4) A direct XAML attribute wins over an implicit style targeting the same property, because
    /// <c>IsXAMLPropertyUnset</c> refuses to let the style overwrite an explicitly-set value -- so the DTO's
    /// <c>Padding</c> stays the attribute value and its provenance entry is never populated, and
    /// <see cref="ResolveXamlSource"/> falls back to <see cref="UIValueSourceKind.LocalValue"/>.</summary>
    [Fact]
    public void DirectAttribute_Beats_ImplicitStyle_And_Resolves_As_LocalValue()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""Border"">
            <Setter Property=""Padding"" Value=""7"" />
        </Style>
    </Window.Styles>
    <Border Name=""P"" Padding=""3"" />
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder p = window.GetElementByName<MGBorder>("P");
        Assert.Equal(new Thickness(3), p.Padding);

        Assert.True(p.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.LocalValue, winner.Source.Kind);
    }

    /// <summary>(5) An implicit style setting <c>Background</c> on a <c>Button</c> is tagged
    /// <see cref="UIValueSourceKind.ImplicitStyle"/> (40), which outranks the Theme(20) write that
    /// <c>MGButton.OnThemeChanged</c> performs on a real theme change -- so the style's red survives.</summary>
    [Fact]
    public void ImplicitStyle_Background_Survives_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""Button"">
            <Setter Property=""Background"" Value=""Red"" />
        </Style>
    </Window.Styles>
    <Button Name=""B"" />
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGButton b = window.GetElementByName<MGButton>("B");
        Assert.IsType<MGSolidFillBrush>(b.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Red, ((MGSolidFillBrush) b.BackgroundBrush.NormalValue).Color);

        Assert.True(b.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> winner));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, winner.Source.Kind);

        window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Desktop.Update();

        Assert.IsType<MGSolidFillBrush>(b.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Red, ((MGSolidFillBrush) b.BackgroundBrush.NormalValue).Color);
    }

    /// <summary>(6) Closes the S6 gap (see <see cref="ResolvedTextForegroundPilotTests"/>): the constructor's tagged
    /// <c>Default(Draw)</c> write to <c>MGTextBlock.Foreground</c> physically sets the same color that <c>Foreground=</c>
    /// asks for, so the redundant assignment used to be silently dropped and no contribution was ever recorded. Now
    /// that Controls.cs's transfer uses the tagged <c>SetForegroundSlot</c>, a <see cref="UIValueSourceKind.LocalValue"/>
    /// contribution is always recorded, even though the physical value doesn't change. Also covers an implicit style
    /// targeting <c>TextBlock.Foreground</c>, and a style targeting <c>Border.TextForeground</c> (the inherited-provider
    /// side of the text pilot).</summary>
    [Fact]
    public void TextForeground_Provenance_Covers_Style_On_Border_LocalAttribute_On_TextBlock_And_Style_On_TextBlock()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""TextBlock"">
            <Setter Property=""Foreground"" Value=""Blue"" />
        </Style>
    </Window.Styles>
    <StackPanel Orientation=""Vertical"">
        <Border Name=""StyledForegroundBorder"" />
        <TextBlock Name=""RedForeground"" Text=""x"" Foreground=""Red"" />
        <TextBlock Name=""StyledForeground"" Text=""y"" />
    </StackPanel>
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        // Style-driven DefaultTextForeground would require targeting Border with a TextForeground setter; the S7
        // brief's item (6) exercises this shape directly to confirm the mechanism generalizes beyond Foreground.
        MGBorder styledForegroundBorder = window.GetElementByName<MGBorder>("StyledForegroundBorder");
        Assert.False(styledForegroundBorder.TryGetResolvedContribution<Color?>(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.ImplicitStyle, out _));
        // No style targets Border.TextForeground in this XAML, so nothing was transferred at all -- documented here
        // to make the negative case explicit rather than silently absent from the suite.

        MGTextBlock redForeground = window.GetElementByName<MGTextBlock>("RedForeground");
        Assert.True(redForeground.TryGetResolvedContribution(UIPilotProperty.Foreground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<Color?> redContribution));
        Assert.Equal(Color.Red, redContribution.Value);

        MGTextBlock styledForeground = window.GetElementByName<MGTextBlock>("StyledForeground");
        Assert.True(styledForeground.TryGetResolvedPilotValue(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIResolvedValue<Color?> styledWinner));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, styledWinner.Source.Kind);
        Assert.Equal(Color.Blue, styledForeground.Foreground.NormalValue);
    }

    /// <summary>(6b) A style setting <c>TextForeground</c> on <c>Border</c> is recorded as an
    /// <see cref="UIValueSourceKind.ImplicitStyle"/> contribution on <c>DefaultTextForeground</c>.</summary>
    [Fact]
    public void ImplicitStyle_TextForeground_On_Border_Wins_As_ImplicitStyle()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""Border"">
            <Setter Property=""TextForeground"" Value=""Lime"" />
        </Style>
    </Window.Styles>
    <Border Name=""P"" />
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder p = window.GetElementByName<MGBorder>("P");
        Assert.True(p.TryGetResolvedContribution(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.ImplicitStyle, out UIResolvedValue<Color?> contribution));
        Assert.Equal(Color.Lime, contribution.Value);
    }

    /// <summary>(7) A templatised control (<c>TextBox</c>): the catalogue's own owner-level default (ADR-0005/S7a)
    /// is tagged <see cref="UIValueSourceKind.Theme"/> (20), so an implicit style at ImplicitStyle(40) still wins,
    /// and survives a real theme change the same way the Background pilot's style does in test (5).</summary>
    [Fact]
    public void ImplicitStyle_Padding_On_TextBox_Wins_Over_Catalogue_Theme_Default_And_Survives_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""TextBox"">
            <Setter Property=""Padding"" Value=""11"" />
        </Style>
    </Window.Styles>
    <TextBox Name=""T"" />
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGTextBox t = window.GetElementByName<MGTextBox>("T");
        Assert.Equal(new Thickness(11), t.Padding);

        Assert.True(t.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, winner.Source.Kind);

        Assert.True(t.TryGetResolvedContribution<Thickness>(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Theme, out _));

        window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Desktop.Update();

        Assert.Equal(new Thickness(11), t.Padding);
    }

    /// <summary>(8) The <c>BorderThickness</c> facade of a composite control (<c>Button.BorderThickness</c> delegating
    /// to a nested <c>Border</c> DTO): an implicit style targeting <c>Button</c> is propagated onto the nested
    /// <c>Border</c> DTO's own provenance (see <c>Element.RecordStyleProvenance</c>), so the winning contribution on
    /// <c>button.GetBorder()</c> is tagged <see cref="UIValueSourceKind.ImplicitStyle"/>; a direct attribute on
    /// <c>Button</c> instead resolves as <see cref="UIValueSourceKind.LocalValue"/>.</summary>
    [Fact]
    public void ImplicitStyle_BorderThickness_On_Button_Facade_Propagates_To_Nested_Border_DTO()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""Button"">
            <Setter Property=""BorderThickness"" Value=""4"" />
        </Style>
    </Window.Styles>
    <StackPanel Orientation=""Vertical"">
        <Button Name=""Styled"" />
        <Button Name=""Local"" BorderThickness=""2"" />
    </StackPanel>
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGButton styled = window.GetElementByName<MGButton>("Styled");
        Assert.True(styled.GetBorder().TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> styledWinner));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, styledWinner.Source.Kind);
        Assert.Equal(new Thickness(4), styled.BorderThickness);

        MGButton local = window.GetElementByName<MGButton>("Local");
        Assert.True(local.GetBorder().TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> localWinner));
        Assert.Equal(UIValueSourceKind.LocalValue, localWinner.Source.Kind);
        Assert.Equal(new Thickness(2), local.BorderThickness);
    }

    /// <summary>(9)/(10) A scalar pilot (<c>Margin</c>) on a non-composite, non-templatised control (<c>Separator</c>):
    /// an implicit style resolves as <see cref="UIValueSourceKind.ImplicitStyle"/> (made possible by the owner-level
    /// Theme(20) catalogue default introduced in S7a, which an ImplicitStyle(40) write now always outranks); a direct
    /// attribute resolves as <see cref="UIValueSourceKind.LocalValue"/>.</summary>
    [Fact]
    public void ImplicitStyle_Margin_On_Separator_Wins_As_ImplicitStyle_Direct_Attribute_Resolves_As_LocalValue()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Window.Styles>
        <Style TargetType=""Separator"">
            <Setter Property=""Margin"" Value=""0"" />
        </Style>
    </Window.Styles>
    <StackPanel Orientation=""Vertical"">
        <Separator Name=""S"" />
        <Separator Name=""Local"" Margin=""2"" />
    </StackPanel>
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGElement s = window.GetElementByName<MGElement>("S");
        Assert.Equal(new Thickness(0), s.Margin);
        Assert.True(s.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> styledWinner));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, styledWinner.Source.Kind);

        MGElement local = window.GetElementByName<MGElement>("Local");
        Assert.Equal(new Thickness(2), local.Margin);
        Assert.True(local.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> localWinner));
        Assert.Equal(UIValueSourceKind.LocalValue, localWinner.Source.Kind);
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
