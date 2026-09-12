using System;
using System.Collections.Generic;
using System.IO;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
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
/// (<c>ThemeDefinition</c> based on <c>Dark</c>) that applies to live controls by a theme change on their scope, without control code. The values the
/// preset cannot reach declaratively are pinned here, so that the list of limits documented by the task stays true.
/// </summary>
public class EditorCompactPresetTests
{
    private const string PresetPath = @"d:\development\repo\MGUI\MGUI.Samples\Features\EditorCompact.Themes.xaml";
    private const string CatalogPath = @"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs";

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

        // Everything the preset does not declare comes from Dark.
        Assert.Equal(dark.DropdownArrowColor, compact.DropdownArrowColor);
        Assert.Equal(dark.Docking.TabGroupIconColor, compact.Docking.TabGroupIconColor);
        Assert.Equal(dark.ComboBox.DropdownBorderThickness, compact.ComboBox.DropdownBorderThickness);
    }

    [Fact]
    public void The_Preset_Applies_To_Live_Controls_By_A_Theme_Change_On_Their_Scope()
    {
        Harness harness = Harness.Create();
        MGTheme compact = harness.Window.GetResources().LoadThemesFromXaml(XamlDocumentSource.FromFile(PresetPath))["EditorCompact"];
        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = dark;

        MGComboBox<string> comboBox = new(harness.Window);
        MGListBox<string> listBox = new(harness.Window);
        MGTextBlock textBlock = new(harness.Window, "Label");
        MGStackPanel panel = new(harness.Window, Orientation.Vertical);
        panel.TryAddChild(comboBox);
        panel.TryAddChild(listBox);
        panel.TryAddChild(textBlock);
        harness.Show(panel);
        AssertDensity(dark, comboBox, listBox, textBlock);

        harness.Window.GetResources().DefaultTheme = compact;
        harness.Frame(2);
        AssertDensity(compact, comboBox, listBox, textBlock);

        harness.Window.GetResources().DefaultTheme = dark;
        harness.Frame(3);
        AssertDensity(dark, comboBox, listBox, textBlock);
    }

    [Fact]
    public void The_Values_The_Preset_Cannot_Reach_Keep_Their_Code_Defaults()
    {
        Harness harness = Harness.Create();
        MGTheme compact = harness.Window.GetResources().LoadThemesFromXaml(XamlDocumentSource.FromFile(PresetPath))["EditorCompact"];
        harness.Window.GetResources().DefaultTheme = compact;

        MGTextBox textBox = new(harness.Window);
        MGDockTabGroup tabGroup = new(harness.Window);
        MGListBox<string> listBox = new(harness.Window);
        MGStackPanel panel = new(harness.Window, Orientation.Vertical);
        panel.TryAddChild(textBox);
        panel.TryAddChild(tabGroup);
        panel.TryAddChild(listBox);
        harness.Show(panel);

        // No theme setting reaches the text box density, the docking sizes, or the item paddings of lists and dropdowns.
        Assert.Equal(new Thickness(6, 1, 6, 1), textBox.Padding);
        Assert.Equal(24, textBox.MinHeight);
        Assert.Equal(30, tabGroup.TabHeaderHeight);

        // The theme declares a list box minimum height, but a list box on the Dark.ListBox variant receives none (gap recorded as task 14).
        Assert.Equal(18, compact.ListBox.MinHeight);
        Assert.Equal("Dark.ListBox", listBox.AppliedControlTemplateName);
        Assert.Null(listBox.MinHeight);
        Assert.Equal(24, MGDockAutoHideStrip.StripThickness);
        Assert.Equal(new Thickness(6, 4), MGControlTemplateCatalog.DefaultListBoxItemPadding);
        Assert.Equal(new Thickness(8, 5, 8, 5), MGControlTemplateCatalog.DefaultComboBoxDropdownItemPadding);

        // The tooltip chrome and the tab header paddings are catalogue literals, not theme values.
        string catalogSource = File.ReadAllText(CatalogPath);
        Assert.Contains("Context.ApplyOwnerThemeDefault(\"ToolTip.Padding\", new Thickness(6, 3)", catalogSource);
        Assert.Contains("Context.ApplyOwnerThemeDefault(\"ToolTip.BorderThickness\", new Thickness(2)", catalogSource);
        Assert.Contains("IsSelected ? new Thickness(8, 5, 8, 5) : new Thickness(8, 3, 8, 3)", catalogSource);
        Assert.Contains("Context.ApplyTemplateValue(\"DockTabItem.TitleText.Padding\", new Thickness(8, 4, 4, 4)", catalogSource);
    }

    private static void AssertDensity(MGTheme theme, MGComboBox<string> comboBox, MGListBox<string> listBox, MGTextBlock textBlock)
    {
        Assert.Equal(theme.ComboBox.Padding, comboBox.Padding);
        Assert.Equal(theme.ComboBox.MinHeight, comboBox.MinHeight);
        Assert.Equal(theme.FontSettings.DefaultFontSize, textBlock.FontSize);
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
