using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>
/// S4 of the resolved-value-engine program (ADR-0005): every test in this class proves precedence through a REAL
/// theme change -- <c>MGResources.DefaultTheme</c> is reassigned on a live desktop, which synchronously raises
/// <c>OnDefaultThemeChanged</c>, which the owning window's subscription (<c>MGElement.cs</c>, around line 526)
/// turns into <see cref="MGElement.NotifyThemeChanged"/>: <c>OnThemeChanged(previous, current)</c> runs first,
/// then <c>ApplyControlTemplate(true)</c> (a template refresh gated by the has-previous/equals guard: a template
/// only rewrites a key if the element's current value still equals the last value that same template applied),
/// then the same notification recurses into every visual-tree child (and component) that has no local resource
/// scope of its own. No test here reads a pilot value written directly by test code without going through this
/// callback chain -- <see cref="ResolvedTemplatePilotsTests"/> and <see cref="ResolvedScalarPilotsTests"/> already
/// cover the callback-free cases.
/// </summary>
public class ResolvedThemeChangeTests
{
    [Fact]
    public void TreeView_Expander_Local_Padding_Survives_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        MGTreeView treeView = new(harness.Window);
        MGTreeViewItem item = new(harness.Window) { Header = "Item A" };
        item.AddItem(new MGTreeViewItem(harness.Window) { Header = "Child" });
        treeView.AddItem(item);
        harness.Show(treeView);

        MGToggleButton expander = item.EnumerateVisualTree(true).OfType<MGToggleButton>().First();

        Thickness localPadding = new(7);
        expander.Padding = localPadding; // public setter => LocalValue(90)
        Assert.Equal(localPadding, expander.Padding);

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        // The local write still wins over ApplyNeutralChrome's Theme(20) write, run again from OnThemeChanged.
        Assert.Equal(localPadding, expander.Padding);
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.LocalValue, winner.Source.Kind);
        Assert.Equal(localPadding, winner.Value);

        // Named mutation (run by hand): reclassify the five ApplyNeutralChrome writes from
        // UIValueResolutionSource.Theme(...) to UIValueResolutionSource.Default(...) -- this assertion goes red
        // because the Theme(20) contribution is never (re-)recorded, so TryGetResolvedContribution below returns
        // false instead of reporting the neutral-chrome (0,0,0,0) padding.
        Assert.True(expander.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<Thickness> themeContribution));
        Assert.Equal(new Thickness(0), themeContribution.Value);

        expander.ClearPilotSource(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.LocalValue);

        // With the LocalValue(90) contribution cleared, the Theme(20) contribution recorded above becomes the winner.
        Assert.Equal(new Thickness(0), expander.Padding);
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> fallbackWinner));
        Assert.Equal(UIValueSourceKind.Theme, fallbackWinner.Source.Kind);
    }

    [Fact]
    public void TreeView_Expander_Without_Local_Write_Follows_The_Theme_Callback()
    {
        Harness harness = Harness.Create();
        MGTreeView treeView = new(harness.Window);
        MGTreeViewItem item = new(harness.Window) { Header = "Item A" };
        item.AddItem(new MGTreeViewItem(harness.Window) { Header = "Child" });
        treeView.AddItem(item);
        harness.Show(treeView);

        MGToggleButton expander = item.EnumerateVisualTree(true).OfType<MGToggleButton>().First();

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        Assert.Equal(new Thickness(0), expander.Padding);
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> padding));
        Assert.Equal(UIValueSourceKind.Theme, padding.Source.Kind);

        Assert.Equal(new Thickness(0, 0, 4, 0), expander.Margin);
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> margin));
        Assert.Equal(UIValueSourceKind.Theme, margin.Source.Kind);

        Assert.Equal(10, expander.MinHeight);
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.MinHeight, UIValueSlot.Whole, out UIResolvedValue<int?> minHeight));
        Assert.Equal(UIValueSourceKind.Theme, minHeight.Source.Kind);

        // MGToggleButton.GetBorder() returns its BorderElement (never null), so the BorderThickness pilot is
        // reached directly on the button; TryGetResolvedPilotValue delegates internally.
        Assert.NotNull(expander.GetBorder());
        Assert.Equal(new Thickness(0), expander.BorderThickness);
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> borderThickness));
        Assert.Equal(UIValueSourceKind.Theme, borderThickness.Source.Kind);

        // Named mutation (run by hand): reclassify the five ApplyNeutralChrome writes from
        // UIValueResolutionSource.Theme(...) to UIValueResolutionSource.Default(...) -- all four "source == Theme"
        // assertions above go red (the winning source becomes Default instead).
    }

    [Fact]
    public void ContextMenu_Local_Padding_Survives_A_Real_Theme_Change_And_The_Theme_Contribution_Is_Retained()
    {
        Harness harness = Harness.Create();
        MGContextMenu menu = new(harness.Window, "");
        menu.AddButton("Item A", _ => { });
        harness.Show(menu);

        // ADR-0005/S7a: ContextMenu.Padding targets the control itself (Menu.SetPadding), so the catalogue now
        // tags it Theme (20) instead of Template (60).
        Assert.True(menu.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<Thickness> templateBefore));
        Assert.Equal(menu.GetTheme().ContextMenu.Padding, templateBefore.Value);

        Thickness localPadding = new(13);
        menu.Padding = localPadding; // public setter => LocalValue(90)
        Assert.Equal(localPadding, menu.Padding);

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        Assert.Equal(localPadding, menu.Padding);
        Assert.True(menu.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.LocalValue, winner.Source.Kind);
        Assert.Equal(localPadding, winner.Value);

        // The Theme(20) contribution is retained even though it is no longer effective: the refresh guard bails
        // because the current CLR padding (13) no longer equals the last template-applied value.
        Assert.True(menu.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<Thickness> templateAfter));
        Assert.Equal(templateBefore.Value, templateAfter.Value);
    }

    [Fact]
    public void Templated_Window_Without_Local_Write_Takes_The_New_Theme_Padding_From_Its_Template()
    {
        Harness harness = Harness.Create();
        MGWindow window = new(harness.Desktop, 0, 0, 400, 300);
        harness.Show(window);

        Thickness beforePadding = window.Padding;
        Thickness distinctivePadding = new(23);
        Assert.NotEqual(beforePadding, distinctivePadding);

        MGTheme customTheme = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily)
        {
            Window = { Padding = distinctivePadding },
        };
        harness.Window.GetResources().DefaultTheme = customTheme;
        harness.Frame(100, Point.Zero);

        Assert.Equal(distinctivePadding, window.Padding);
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        // ADR-0005/S7a: Window.Padding targets the control itself (Window.SetPadding), so the catalogue now tags
        // it Theme (20) instead of Template (60).
        Assert.Equal(UIValueSourceKind.Theme, winner.Source.Kind);
        Assert.True(winner.HasAnyInvalidation(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
    }

    [Fact]
    public void ContextMenuItem_Submenu_Arrow_Margin_Follows_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        MGContextMenu menu = new(harness.Window, "");
        MGContextMenuButton item = menu.AddButton("Item A", _ => { });
        harness.Show(menu);

        // No simple public API exists to attach a nested submenu without also wiring up its own menu resources, so
        // this test exercises the plain (no-submenu) item: PART_SubmenuArrow is still registered and templated --
        // only its Visibility changes when a submenu is present.
        Thickness customHeaderMargin = new(1, 2, 19, 2);
        Thickness customShortcutMargin = new(37, 0, 0, 0);
        Thickness customArrowMargin = new(2, 3, 29, 3);
        MGTheme customTheme = new(MGTheme.BuiltInTheme.Dark_Blue, harness.Desktop.DefaultFontFamily);
        customTheme.ContextMenuItem.HeaderMargin = customHeaderMargin;
        customTheme.ContextMenuItem.ShortcutMargin = customShortcutMargin;
        customTheme.ContextMenuItem.SubmenuArrowMargin = customArrowMargin;
        harness.Window.GetResources().DefaultTheme = customTheme;
        harness.Frame(100, Point.Zero);

        Assert.True(item.TryGetTemplatePart(MGWrappedContextMenuItem.HeaderPresenterPartName, out MGElement headerPart));
        Assert.Equal(customHeaderMargin, headerPart.Margin);
        Assert.True(headerPart.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> headerMargin));
        Assert.Equal(UIValueSourceKind.Template, headerMargin.Source.Kind);

        Assert.True(item.TryGetTemplatePart(MGWrappedContextMenuItem.ShortcutTextPartName, out MGElement shortcutPart));
        Assert.Equal(customShortcutMargin, shortcutPart.Margin);
        Assert.True(shortcutPart.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> shortcutMargin));
        Assert.Equal(UIValueSourceKind.Template, shortcutMargin.Source.Kind);

        Assert.True(item.TryGetTemplatePart(MGWrappedContextMenuItem.SubmenuArrowPartName, out MGElement arrowPart));
        Assert.Equal(customArrowMargin, arrowPart.Margin);
        Assert.True(arrowPart.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> arrowMargin));
        Assert.Equal(UIValueSourceKind.Template, arrowMargin.Source.Kind);
    }

    [Fact]
    public void DockDrawer_Border_Follows_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        MGDockAutoHideDrawer drawer = new(harness.Window);
        harness.Show(drawer);

        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.BorderPartName, out MGElement borderPart));
        MGBorder border = Assert.IsType<MGBorder>(borderPart);

        Color distinctiveColor = new(9, 99, 199);
        MGTheme customTheme = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        customTheme.Docking.AutoHideBorderColor = distinctiveColor;
        harness.Window.GetResources().DefaultTheme = customTheme;
        harness.Frame(100, Point.Zero);

        // The catalogue's "DockDrawer.BorderColor" template key rewrites Drawer.BorderColor, whose setter calls
        // ApplyThemeVisuals(), which re-issues the border brush/thickness pilot writes as Theme(20) contributions --
        // so the effective winner here is Theme, not Template, even though the theme change reached this element
        // through its template's ApplyThemeDefault callback.
        MGUniformBorderBrush expectedBrush = distinctiveColor.AsFillBrush().AsUniformBorderBrush();
        Assert.Equal(expectedBrush, border.BorderBrush);
        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> brushWinner));
        Assert.Equal(UIValueSourceKind.Theme, brushWinner.Source.Kind);
        Assert.Equal(expectedBrush, brushWinner.Value);

        Assert.Equal(new Thickness(1), border.BorderThickness);
        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> thicknessWinner));
        Assert.Equal(UIValueSourceKind.Theme, thicknessWinner.Source.Kind);
    }

    [Fact]
    public void DockDrawer_Local_Border_Survives_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        MGDockAutoHideDrawer drawer = new(harness.Window);
        harness.Show(drawer);

        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.BorderPartName, out MGElement borderPart));
        MGBorder border = Assert.IsType<MGBorder>(borderPart);

        // MGUniformBorderBrush is a value type: two independently-boxed copies compare structurally equal but
        // are never ReferenceEquals, so this test (like ResolvedScalarPilotsTests) asserts on content equality,
        // using a local color distinct from the theme's distinctive color to tell the two sources apart.
        MGUniformBorderBrush localBrush = MGUniformBorderBrush.Black;
        border.BorderBrush = localBrush; // public setter => LocalValue(90)
        Assert.Equal(localBrush, border.BorderBrush);

        Color distinctiveColor = new(9, 99, 199);
        MGTheme customTheme = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        customTheme.Docking.AutoHideBorderColor = distinctiveColor;
        Assert.NotEqual(MGUniformBorderBrush.Black, distinctiveColor.AsFillBrush().AsUniformBorderBrush());
        harness.Window.GetResources().DefaultTheme = customTheme;
        harness.Frame(100, Point.Zero);

        // The local write outranks Theme(20): at unequal precedence the higher slot always wins regardless of
        // write order, so the local brush is still effective after the callback re-issued its own Theme(20) write.
        Assert.Equal(localBrush, border.BorderBrush);
        Assert.True(border.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> winner));
        Assert.Equal(UIValueSourceKind.LocalValue, winner.Source.Kind);
        Assert.Equal(localBrush, winner.Value);

        MGUniformBorderBrush expectedThemeBrush = distinctiveColor.AsFillBrush().AsUniformBorderBrush();
        Assert.True(border.TryGetResolvedContribution(UIPilotProperty.BorderBrush, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<IBorderBrush> themeContribution));
        Assert.Equal(expectedThemeBrush, themeContribution.Value);

        // Named mutation (run by hand): in MGDockAutoHideDrawer.ApplyThemeVisuals, reclassify
        // _border.SetBorderBrush(..., UIValueResolutionSource.Theme(UIInvalidationKind.Draw)) to
        // UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw) -- this assertion goes red: at equal
        // precedence (90 == 90) the callback becomes the last writer and its brush becomes effective, so
        // border.BorderBrush stops being the local Black brush and becomes the theme's distinctive-color brush.
        Assert.NotEqual(expectedThemeBrush, border.BorderBrush);
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
