using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using XamlDocumentSource = MGUI.Core.UI.XAML.XamlDocumentSource;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Backlog task 13 (styling-theme-tasks.md), scenario <c>SCN-THEME-001</c>: the "Editor Compact" preset of <c>MGUI.Samples</c> is a declarative theme
/// (<c>ThemeDefinition</c> based on <c>Dark</c>) that applies to live controls by a theme change on their scope, without control code. Task 14 made
/// the densities the preset could not reach (tooltip, text boxes, item paddings, tab headers, docking sizes) theme settings; the few values that stay
/// out of reach are pinned here, so that the list of limits documented by the task stays true.
/// </summary>
public class EditorCompactPresetTests
{
    private const string PresetPath = @"d:\development\repo\MGUI\MGUI.Samples\Features\EditorCompact.Themes.xaml";
    private const string CatalogPath = @"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs";
    private const string TabGroupPath = @"d:\development\repo\MGUI\MGUI.Core\UI\Docking\Controls\MGDockTabGroup.cs";
    private const string TabGroupNodePath = @"d:\development\repo\MGUI\MGUI.Core\UI\Docking\DockLayout\DockTabGroupNode.cs";

    [Fact]
    public void The_Preset_Is_A_Declarative_Theme_Based_On_Dark()
    {
        MGResources resources = new(MGTheme.CreateEmpty("Arial"));
        IReadOnlyDictionary<string, MGTheme> themes = resources.LoadThemesFromXaml(XamlDocumentSource.FromFile(PresetPath));
        MGTheme compact = Assert.Single(themes, pair => pair.Key == "EditorCompact").Value;
        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, "Arial");

        Assert.Equal(10, compact.FontSettings.DefaultFontSize);
        Assert.True(compact.FontSettings.DefaultFontSize < dark.FontSettings.DefaultFontSize);
        Assert.Equal(new Thickness(4, 1), compact.ComboBox.Padding);
        Assert.Equal(18, compact.ComboBox.MinHeight);
        Assert.Equal(18, compact.ListBox.MinHeight);
        Assert.Equal(new Thickness(4, 1), compact.ListBox.TitlePadding);
        Assert.Equal(0, compact.TabControl.HeadersSpacing);
        Assert.Equal(12, compact.TreeViewIndentSize);

        // Backlog task 14: the densities that used to be catalog literals, static fields or docking constants.
        Assert.Equal(new Thickness(4, 2), compact.ToolTip.Padding);
        Assert.Equal(new Thickness(1), compact.ToolTip.BorderThickness);
        Assert.Equal(8, compact.ToolTip.MinWidth);
        Assert.Equal(8, compact.ToolTip.MinHeight);
        Assert.Equal(new Thickness(4, 0), compact.TextBox.Padding);
        Assert.Equal(20, compact.TextBox.MinHeight);
        Assert.Equal(new Thickness(4, 1), compact.NumericUpDown.Padding);
        Assert.Equal(22, compact.NumericUpDown.MinHeight);
        Assert.Equal(18, compact.NumericUpDown.SpinnerWidth);
        Assert.Equal(16, compact.NumericUpDown.SpinnerMinWidth);
        Assert.Equal(new Thickness(4, 2), compact.ListBox.ItemPadding);
        Assert.Equal(new Thickness(2, 0), compact.ListBox.ItemContentPadding);
        Assert.Equal(new Thickness(6, 3), compact.ComboBox.DropdownItemPadding);
        Assert.Equal(new Thickness(6, 3), compact.TabControl.SelectedHeaderPadding);
        Assert.Equal(new Thickness(6, 2), compact.TabControl.UnselectedHeaderPadding);
        Assert.Equal(new Thickness(4, 3), compact.TabControl.SideHeaderPadding);
        Assert.Equal(24, compact.Docking.TabHeaderHeight);
        Assert.Equal(18, compact.Docking.TabButtonSize);
        Assert.Equal(new Thickness(6, 2, 2, 2), compact.Docking.TabTitlePadding);
        Assert.Equal(22, compact.Docking.AutoHideDrawerHeaderHeight);
        Assert.Equal(18, compact.Docking.AutoHideDrawerButtonSize);
        Assert.Equal(20, compact.Docking.AutoHideStripThickness);
        Assert.Equal(32, compact.Docking.DropIndicatorZoneSize);

        // Everything the preset does not declare comes from Dark, whose new settings hold the former code defaults.
        Assert.Equal(dark.DropdownArrowColor, compact.DropdownArrowColor);
        Assert.Equal(dark.Docking.TabGroupIconColor, compact.Docking.TabGroupIconColor);
        Assert.Equal(dark.ComboBox.DropdownBorderThickness, compact.ComboBox.DropdownBorderThickness);
        Assert.Equal(new Thickness(6, 3), dark.ToolTip.Padding);
        Assert.Equal(new Thickness(6, 1, 6, 1), dark.TextBox.Padding);
        Assert.Equal(MGControlTemplateCatalog.DefaultListBoxItemPadding, dark.ListBox.ItemPadding);
        Assert.Equal(MGControlTemplateCatalog.DefaultComboBoxDropdownItemPadding, dark.ComboBox.DropdownItemPadding);
        Assert.Equal(MGDockAutoHideStrip.DefaultStripThickness, dark.Docking.AutoHideStripThickness);
        Assert.Equal(30, dark.Docking.TabHeaderHeight);
    }

    [Fact]
    public void The_Preset_Applies_To_Live_Controls_By_A_Theme_Change_On_Their_Scope()
    {
        Harness harness = Harness.Create();
        MGTheme compact = harness.Window.GetResources().LoadThemesFromXaml(XamlDocumentSource.FromFile(PresetPath))["EditorCompact"];
        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = dark;

        Scene scene = Scene.Build(harness);
        AssertDensity(dark, scene);

        harness.Window.GetResources().DefaultTheme = compact;
        harness.Frame(2);
        AssertDensity(compact, scene);

        harness.Window.GetResources().DefaultTheme = dark;
        harness.Frame(3);
        AssertDensity(dark, scene);
    }

    [Fact]
    public void The_Values_Out_Of_The_Preset_Reach_Keep_Their_Code_Defaults()
    {
        Harness harness = Harness.Create();
        MGTheme compact = harness.Window.GetResources().LoadThemesFromXaml(XamlDocumentSource.FromFile(PresetPath))["EditorCompact"];
        harness.Window.GetResources().DefaultTheme = compact;

        MGTabControl tabControl = new(harness.Window);
        tabControl.AddTab("First", new MGTextBlock(harness.Window, "1"));
        tabControl.AddTab("Second", new MGTextBlock(harness.Window, "2"));
        harness.Show(tabControl);

        // The tab header border thicknesses encode which edge of the header touches the content, not a density: they stay template values.
        MGButton[] headers = tabControl.HeadersPanelElement.Children.OfType<MGButton>().ToArray();
        Assert.Equal(2, headers.Length);
        Assert.All(headers, header => Assert.Equal(new Thickness(1, 1, 1, 0), header.BorderThickness));

        // The compact buttons of a tab group (overflow, maximize) and the minimum-height heuristic of the layout model stay constants.
        string tabGroupSource = File.ReadAllText(TabGroupPath);
        Assert.Contains("private const int DropdownBtnWidth = 24;", tabGroupSource);
        Assert.Contains("private const int MaximizeBtnWidth = 24;", tabGroupSource);
        Assert.Contains("const int TabHeaderHeight = 30; // Approximate height of tab headers", File.ReadAllText(TabGroupNodePath));

        // The code defaults are the values the theme settings start from.
        Assert.Equal(24, MGDockAutoHideStrip.DefaultStripThickness);
        Assert.Equal(new Thickness(6, 4), MGControlTemplateCatalog.DefaultListBoxItemPadding);
        Assert.Equal(new Thickness(8, 5, 8, 5), MGControlTemplateCatalog.DefaultComboBoxDropdownItemPadding);
        string catalogSource = File.ReadAllText(CatalogPath);
        Assert.Contains("Context.ApplyOwnerThemeDefault(\"ToolTip.Padding\", Theme.ToolTip.Padding", catalogSource);
        Assert.Contains("Context.ApplyThemeDefault(\"DockTabItem.TitleText.Padding\", Docking?.TabTitlePadding ?? new Thickness(8, 4, 4, 4)", catalogSource);
    }

    [Fact]
    public void The_Sample_Window_And_Its_Context_Menu_Repaint_When_The_Preset_Is_Applied()
    {
        const string SamplePath = @"d:\development\repo\MGUI\MGUI.Samples\Features\EditorCompactPreset.xaml";
        Harness harness = Harness.Create();
        MGWindow window = MGUI.Core.UI.XAML.XAMLParser.LoadRootWindow(harness.Desktop, File.ReadAllText(SamplePath), false, true);
        harness.Desktop.Windows.Add(window);
        MGTheme compact = window.GetResources().LoadThemesFromXaml(XamlDocumentSource.FromFile(PresetPath))["EditorCompact"];
        MGContextMenu menu = window.GetElementByName<MGContextMenu>("SampleContextMenu");
        Assert.NotEqual(BackgroundColor(compact, MGElementType.ContextMenu), SolidColor(menu.BackgroundBrush));

        // As the sample does: the preset is applied after the XAML was parsed under the desktop theme.
        window.GetResources().DefaultTheme = compact;
        Assert.True(harness.Desktop.TryOpenContextMenu(menu, Point.Zero));

        Assert.Equal(BackgroundColor(compact, MGElementType.ContextMenu), SolidColor(menu.BackgroundBrush));
        Assert.Equal(BackgroundColor(compact, MGElementType.Window), SolidColor(window.BackgroundBrush));
    }

    private static Color BackgroundColor(MGTheme theme, MGElementType type) => SolidColor(theme.GetBackgroundBrush(type));

    private static Color SolidColor(MGUI.Core.UI.VisualStateFillBrush brush) => SolidColor(brush.NormalValue);

    private static Color SolidColor(IFillBrush brush)
        => Assert.IsType<MGSolidFillBrush>(brush).Color;

    /// <summary>Every control whose density the preset drives, shown together under the harness window.</summary>
    private sealed record Scene(MGComboBox<string> ComboBox, MGListBox<string> ListBox, MGListBox<string> VirtualizedListBox, MGTextBlock TextBlock, MGTextBox TextBox,
        MGNumericUpDown NumericUpDown, MGTabControl TabControl, MGTabControl SideTabControl, MGToolTip ToolTip, MGDockTabGroup TabGroup, MGDockAutoHideStrip Strip,
        MGDockAutoHideDrawer Drawer, MGDockDropIndicators Indicators)
    {
        public static Scene Build(Harness harness)
        {
            MGWindow window = harness.Window;
            MGComboBox<string> comboBox = new(window);
            comboBox.SetItemsSource(new List<string> { "Low", "High" });
            MGListBox<string> listBox = new(window);
            listBox.SetItemsSource(new List<string> { "A", "B" });
            MGListBox<string> virtualizedListBox = new(window) { VirtualizationMode = ListBoxVirtualizationMode.Always, PreferredHeight = 120 };
            virtualizedListBox.SetItemsSource(new List<string> { "V1", "V2", "V3" });
            MGTextBlock textBlock = new(window, "Label");
            MGTextBox textBox = new(window);
            MGNumericUpDown numericUpDown = new(window);
            MGTabControl tabControl = new(window);
            tabControl.AddTab("First", new MGTextBlock(window, "1"));
            tabControl.AddTab("Second", new MGTextBlock(window, "2"));
            MGTabControl sideTabControl = new(window) { TabHeaderPosition = Dock.Left };
            sideTabControl.AddTab("First", new MGTextBlock(window, "1"));
            sideTabControl.AddTab("Second", new MGTextBlock(window, "2"));
            MGToolTip toolTip = new(window, textBlock, 120, 40);
            MGDockTabGroup tabGroup = new(window);
            MGDockAutoHideStrip strip = new(window, AutoHideSide.Left);
            MGDockAutoHideDrawer drawer = new(window);
            MGDockDropIndicators indicators = new(window);

            MGStackPanel panel = new(window, Orientation.Vertical);
            foreach (MGElement element in new MGElement[] { comboBox, listBox, virtualizedListBox, textBlock, textBox, numericUpDown, tabControl, sideTabControl, tabGroup, strip, drawer, indicators })
            {
                panel.TryAddChild(element);
            }

            harness.Show(panel);
            // The dropdown rows are only added to the dropdown when it opens; they then stay and follow the theme of the dropdown window's scope.
            comboBox.IsDropdownOpen = true;
            harness.Frame(1);
            return new(comboBox, listBox, virtualizedListBox, textBlock, textBox, numericUpDown, tabControl, sideTabControl, toolTip, tabGroup, strip, drawer, indicators);
        }
    }

    private static void AssertDensity(MGTheme theme, Scene scene)
    {
        Assert.Equal(theme.ComboBox.Padding, scene.ComboBox.Padding);
        Assert.Equal(theme.ComboBox.MinHeight, scene.ComboBox.MinHeight);
        Assert.Equal(theme.FontSettings.DefaultFontSize, scene.TextBlock.FontSize);

        // Backlog task 14.
        MGButton[] dropdownRows = scene.ComboBox.DropdownStackPanel.Children.OfType<MGButton>().ToArray();
        Assert.Equal(2, dropdownRows.Length);
        Assert.All(dropdownRows, row => Assert.Equal(theme.ComboBox.DropdownItemPadding, row.Padding));

        // The list box resolves Dark.ListBox and now receives the ListBox.* defaults of the catalog (the task 13 gap).
        Assert.Equal("Dark.ListBox", scene.ListBox.AppliedControlTemplateName);
        Assert.Equal(theme.ListBox.MinHeight, scene.ListBox.MinHeight);
        Assert.Equal(2, scene.ListBox.ListBoxItems.Count);
        Assert.All(scene.ListBox.ListBoxItems, item => Assert.Equal(theme.ListBox.ItemPadding, item.ContentPresenter.Padding));
        Assert.All(scene.ListBox.ListBoxItems, item => Assert.Equal(theme.ListBox.ItemContentPadding, item.Content.Padding));
        // A virtualized list box holds its realized wrappers in its virtualizing panel; the theme callback reaches them through the recycle pool.
        Assert.True(scene.VirtualizedListBox.IsVirtualizing);
        MGBorder[] realizedWrappers = scene.VirtualizedListBox.VirtualizingPanel.Children.OfType<MGBorder>().ToArray();
        Assert.NotEmpty(realizedWrappers);
        Assert.All(realizedWrappers, wrapper => Assert.Equal(theme.ListBox.ItemPadding, wrapper.Padding));
        Assert.All(realizedWrappers, wrapper => Assert.Equal(theme.ListBox.ItemContentPadding, wrapper.Content.Padding));

        Assert.Equal(theme.TextBox.Padding, scene.TextBox.Padding);
        Assert.Equal(theme.TextBox.MinHeight, scene.TextBox.MinHeight);
        Assert.Equal(theme.NumericUpDown.Padding, scene.NumericUpDown.Padding);
        Assert.Equal(theme.NumericUpDown.MinHeight, scene.NumericUpDown.MinHeight);
        Assert.True(scene.NumericUpDown.TryGetTemplatePart(MGNumericUpDown.SpinnerHostPartName, out MGElement spinnerHost));
        Assert.Equal(theme.NumericUpDown.SpinnerWidth, spinnerHost.PreferredWidth);
        Assert.True(scene.NumericUpDown.TryGetTemplatePart(MGNumericUpDown.IncreaseButtonPartName, out MGElement increaseButton));
        Assert.Equal(theme.NumericUpDown.SpinnerMinWidth, increaseButton.MinWidth);

        MGButton[] headers = scene.TabControl.HeadersPanelElement.Children.OfType<MGButton>().ToArray();
        Assert.Equal(2, headers.Length);
        Assert.Single(headers, header => header.IsSelected);
        Assert.All(headers, header => Assert.Equal(header.IsSelected ? theme.TabControl.SelectedHeaderPadding : theme.TabControl.UnselectedHeaderPadding, header.Padding));
        MGButton[] sideHeaders = scene.SideTabControl.HeadersPanelElement.Children.OfType<MGButton>().ToArray();
        Assert.Equal(2, sideHeaders.Length);
        Assert.All(sideHeaders, header => Assert.Equal(theme.TabControl.SideHeaderPadding, header.Padding));

        Assert.Equal(theme.ToolTip.Padding, scene.ToolTip.Padding);
        Assert.Equal(theme.ToolTip.BorderThickness, scene.ToolTip.BorderThickness);
        Assert.Equal(SolidColor(Assert.IsType<MGUniformBorderBrush>(theme.ToolTip.BorderBrush).Brush),
            SolidColor(Assert.IsType<MGUniformBorderBrush>(scene.ToolTip.BorderBrush).Brush));
        Assert.Equal(theme.ToolTip.MinWidth, scene.ToolTip.MinWidth);
        Assert.Equal(theme.ToolTip.MinHeight, scene.ToolTip.MinHeight);

        Assert.Equal(theme.Docking.TabHeaderHeight, scene.TabGroup.TabHeaderHeight);
        Assert.Equal(theme.Docking.AutoHideStripThickness, scene.Strip.StripThickness);
        Assert.Equal(theme.Docking.AutoHideDrawerHeaderHeight, scene.Drawer.HeaderHeight);
        Assert.Equal(theme.Docking.AutoHideDrawerButtonSize, scene.Drawer.HeaderButtonSize);
        Assert.Equal(theme.Docking.DropIndicatorZoneSize, scene.Indicators.ZoneSize);
    }

    private sealed record Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 640, 480);
            desktop.Windows.Add(window);
            Harness harness = new(runtime, desktop, window);
            harness.Frame(0);
            return harness;
        }

        public void Show(MGElement content)
        {
            Window.SetContent(content);
            Frame(0);
            Frame(1);
        }

        public void Frame(int frameIndex)
        {
            MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
