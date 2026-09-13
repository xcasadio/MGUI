using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Behavioural coverage of the S7a wiring of <see cref="MGControlTemplateContext.ApplyOwnerThemeDefault{T}"/>
/// (ADR-0005): a control's own chrome default (its own Padding/Margin/MinHeight, or the border its public
/// <c>BorderBrush</c>/<c>BorderThickness</c> facade writes) is now recorded as <see cref="UIValueSourceKind.Theme"/>
/// (precedence 20) instead of <see cref="UIValueSourceKind.Template"/> (precedence 60), so that a style
/// (<see cref="UIValueSourceKind.ImplicitStyle"/>/<see cref="UIValueSourceKind.ExplicitStyle"/>, precedence 40/50)
/// can outrank it even though the control's template constructor runs (and applies its own defaults) before any
/// XAML attribute or style has had a chance to run. A template PART (anything other than the owner itself, or the
/// border returned by the owner's own <c>GetBorder()</c>) is unaffected and stays Template(60).
/// </summary>
public class ResolvedOwnerThemeDefaultTests
{
    [Fact]
    public void Freshly_Templated_Window_Reports_Theme_For_Its_Own_Padding_But_Template_For_The_TitleBar_Part()
    {
        Harness harness = Harness.Create();
        MGWindow window = new(harness.Desktop, 0, 0, 400, 300);
        harness.Show(window);

        // The control itself: Window.SetPadding is called on the owner directly.
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> ownPadding));
        Assert.Equal(UIValueSourceKind.Theme, ownPadding.Source.Kind);
        Assert.Equal(window.GetTheme().Window.Padding, ownPadding.Value);

        // A part (the title bar's own Padding is set through TitleBar.SetPadding, not Window.SetPadding): unchanged.
        Assert.True(window.TryGetTemplatePart(MGWindow.TitleBarPartName, out MGElement titleBarPart));
        Assert.True(titleBarPart.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> titleBarPadding));
        Assert.Equal(UIValueSourceKind.Template, titleBarPadding.Source.Kind);
        Assert.Equal(window.GetTheme().Window.TitleBarPadding, titleBarPadding.Value);
    }

    [Fact]
    public void ContextMenu_Padding_Is_Theme_But_An_Items_HeaderPresenter_Margin_Is_Still_Template()
    {
        Harness harness = Harness.Create();
        MGContextMenu menu = new(harness.Window, "");
        MGContextMenuButton item = menu.AddButton("Item A", _ => { });
        harness.Show(menu);

        // The control itself: ContextMenu.Padding is written by Menu.SetPadding, where Menu is the owner.
        Assert.True(menu.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> menuPadding));
        Assert.Equal(UIValueSourceKind.Theme, menuPadding.Source.Kind);
        Assert.Equal(menu.GetTheme().ContextMenu.Padding, menuPadding.Value);

        // A part: the item's HeaderPresenter Margin is written by HeaderPresenter.SetMargin, not the menu item itself.
        Assert.True(item.TryGetTemplatePart(MGWrappedContextMenuItem.HeaderPresenterPartName, out MGElement headerPart));
        Assert.True(headerPart.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> headerMargin));
        Assert.Equal(UIValueSourceKind.Template, headerMargin.Source.Kind);
    }

    [Fact]
    public void PropertyGrid_OuterBorder_Defaults_Are_Theme_Because_Its_Xaml_Dto_Writes_That_Border()
    {
        Harness harness = Harness.Create();
        MGPropertyGrid propertyGrid = new(harness.Window);
        harness.Show(propertyGrid);

        // MGPropertyGrid has no GetBorder() override, but its XAML DTO applies the nested Border DTO to
        // propertyGrid.OuterBorder (Controls.cs, PropertyGrid.ApplyDerivedSettings): a XAML attribute or style
        // writing BorderBrush/BorderThickness lands on the very border the catalogue's PropertyGrid.BorderBrush /
        // PropertyGrid.BorderThickness defaults write, so those defaults are the control's own chrome (Theme).
        Assert.NotNull(propertyGrid.OuterBorder);
        Assert.True(propertyGrid.OuterBorder.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> borderThickness));
        Assert.Equal(UIValueSourceKind.Theme, borderThickness.Source.Kind);
        Assert.Equal(propertyGrid.GetTheme().PropertyGrid.BorderThickness, borderThickness.Value);
        Assert.True(propertyGrid.OuterBorder.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> borderBrush));
        Assert.Equal(UIValueSourceKind.Theme, borderBrush.Source.Kind);

        // The grid's own Padding is written on the owner directly and is Theme as well.
        Assert.True(propertyGrid.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> padding));
        Assert.Equal(UIValueSourceKind.Theme, padding.Source.Kind);
    }

    [Fact]
    public void TextBox_Background_Is_Theme_By_Default_An_ImplicitStyle_Container_Wins_And_Survives_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        MGTextBox textBox = new(harness.Window);
        harness.Show(textBox);

        // (1) Fresh control: TextBox.Background is written by textBox.SetBackground, where textBox is the owner.
        Assert.True(textBox.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> defaultWinner));
        Assert.Equal(UIValueSourceKind.Theme, defaultWinner.Source.Kind);

        // (2) A style-level Whole write (precedence 40) outranks the owner's own Theme(20) default: it becomes the
        // effective container.
        VisualStateFillBrush styleBrush = new(new MGSolidFillBrush(Color.Red));
        textBox.SetBackground(styleBrush, UIValueResolutionSource.ImplicitStyle(UIInvalidationKind.Draw));
        Assert.Same(styleBrush, textBox.BackgroundBrush);
        Assert.True(textBox.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> styleWinner));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, styleWinner.Source.Kind);
        Assert.Same(styleBrush, styleWinner.Value);

        Assert.True(textBox.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<VisualStateFillBrush> themeContributionBefore));

        // (3) A REAL theme change asks ApplyOwnerThemeDefault to re-issue the owner's Theme(20) contribution
        // (through ApplyControlTemplate), but the style's higher precedence keeps the effective container unchanged.
        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        Assert.Same(styleBrush, textBox.BackgroundBrush); // still effective
        Assert.True(textBox.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> winnerAfterRefresh));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, winnerAfterRefresh.Source.Kind);
        Assert.Same(styleBrush, winnerAfterRefresh.Value);

        // The pre-existing has-previous/equals refresh guard (shared by every ApplyTemplateValueCore overload,
        // ApplyOwnerThemeDefault included) bails before overwriting the Theme contribution: GetCurrentValue reads
        // textBox.BackgroundBrush, which is the style's container (the effective winner), not the value the guard
        // stored after its own last write -- so the two are unequal and the write is skipped. The Theme
        // contribution is therefore retained as the SAME instance, exactly like the Window.Padding case in
        // ResolvedTemplatePilotsTests' "Local_Padding_Survives_A_Theme_Refresh..." test.
        Assert.True(textBox.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<VisualStateFillBrush> themeContributionAfter));
        Assert.Same(themeContributionBefore.Value, themeContributionAfter.Value);
    }

    [Fact]
    public void Window_Padding_ImplicitStyle_Beats_The_Theme_Default_And_Survives_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        MGWindow window = new(harness.Desktop, 0, 0, 400, 300);
        harness.Show(window);

        Thickness stylePadding = new(21);
        window.SetPadding(stylePadding, UIValueResolutionSource.ImplicitStyle(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        Assert.Equal(stylePadding, window.Padding);
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winnerBefore));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, winnerBefore.Source.Kind);

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        // The style's precedence (40) still outranks the owner's Theme(20) default, re-issued by the theme refresh.
        Assert.Equal(stylePadding, window.Padding);
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winnerAfter));
        Assert.Equal(UIValueSourceKind.ImplicitStyle, winnerAfter.Source.Kind);
        Assert.Equal(stylePadding, winnerAfter.Value);
    }

    [Fact]
    public void Separator_Construction_Margin_Is_DefaultValue_And_A_Style_Can_Override_It_Until_Orientation_Changes()
    {
        Harness harness = Harness.Create();
        // Vertical (1), not the enum's default value Horizontal (0): the backing field _Orientation already equals
        // Horizontal before the constructor body runs, so passing Horizontal here would make the OLD (pre-S7a)
        // buggy code path's "if (_Orientation != value)" guard a no-op regardless of whether the constructor goes
        // through the public setter or assigns the field directly -- Vertical is required to actually exercise the
        // difference between the two (and so for mutation M2 in the S7a report to turn this test red).
        MGSeparator separator = new(harness.Window, Orientation.Vertical);
        harness.Show(separator);

        // (1) Fresh construction: no LocalValue contribution (the ADR-0005/S7a fix stopped routing the
        // constructor's initial margin through the public Orientation setter, which would have recorded LocalValue).
        Assert.False(separator.TryGetResolvedContribution<Thickness>(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.LocalValue, out _));
        Assert.True(separator.TryGetResolvedContribution(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<Thickness> defaultContribution));
        Thickness verticalAutoMargin = separator.Margin;
        Assert.Equal(verticalAutoMargin, defaultContribution.Value);
        Assert.True(separator.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> winnerAtConstruction));
        Assert.Equal(UIValueSourceKind.DefaultValue, winnerAtConstruction.Source.Kind);

        // (2) A style can now freely override that construction-time margin, since it is only DefaultValue(0).
        separator.SetMargin(new Thickness(0), UIValueResolutionSource.ImplicitStyle(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        Assert.Equal(new Thickness(0), separator.Margin);

        // (3) Changing Orientation afterwards goes through the public setter, which reacts with a LocalValue(90)
        // write -- a genuine "the caller just changed a property" case, so it must win outright, including over
        // the style set in (2).
        int amount = verticalAutoMargin.Left;
        Thickness expectedHorizontalAutoMargin = new(0, amount, 0, amount);
        separator.Orientation = Orientation.Horizontal;
        Assert.Equal(expectedHorizontalAutoMargin, separator.Margin);
        Assert.True(separator.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> winnerAfterOrientationChange));
        Assert.Equal(UIValueSourceKind.LocalValue, winnerAfterOrientationChange.Source.Kind);
        Assert.Equal(expectedHorizontalAutoMargin, winnerAfterOrientationChange.Value);
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

        /// <summary>Adds the element to the window, shows the window and runs two warm-up frames so layout is settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            if (!Desktop.Windows.Contains(Window))
                Desktop.Windows.Add(Window);
            Frame(0, Point.Zero);
            Frame(1, Point.Zero);
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
