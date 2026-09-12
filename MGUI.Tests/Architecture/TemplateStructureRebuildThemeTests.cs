using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>
/// A real theme change that swaps a control's template structure (the <c>Dark</c> theme maps <see cref="MGWindow"/> to <c>Dark.Window</c> and
/// <see cref="MGComboBox{TItemType}"/> to <c>Dark.ComboBox</c>, both declaring their own <c>DetachedRoots</c>) instantiates new parts. Their template
/// values, the part initialisation done once per structure and the owner-level values held by a replaced part must end up as if the controls had been
/// built under the new theme, and a later change of theme keeps them.
/// </summary>
public class TemplateStructureRebuildThemeTests
{
    [Fact]
    public void Window_And_ComboBox_Chrome_Rebuilt_By_A_Theme_Change_Match_The_Controls_Built_Under_That_Theme()
    {
        Harness builtUnderDark = Harness.Create(dark: true);
        MGComboBox<string> expectedComboBox = CreateComboBox(builtUnderDark);
        Assert.Equal("Dark.Window", builtUnderDark.Window.AppliedControlTemplateName);
        Assert.Equal("Dark.ComboBox", expectedComboBox.AppliedControlTemplateName);
        IReadOnlyDictionary<string, string> expected = DescribeChrome(builtUnderDark.Window, expectedComboBox);

        Harness harness = Harness.Create(dark: false);
        MGComboBox<string> comboBox = CreateComboBox(harness);
        Assert.Equal(MGControlTemplateCatalog.WindowTemplateName, harness.Window.AppliedControlTemplateName);
        Assert.Equal(MGControlTemplateCatalog.ComboBoxTemplateName, comboBox.AppliedControlTemplateName);
        MGWindow codeDropdown = comboBox.Dropdown;
        Assert.True(harness.Window.TryGetTemplatePart(MGWindow.TitleBarPartName, out MGElement codeTitleBar));

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(3, Point.Zero);

        // The structures were rebuilt from the XAML templates.
        Assert.NotSame(codeDropdown, comboBox.Dropdown);
        Assert.True(harness.Window.TryGetTemplatePart(MGWindow.TitleBarPartName, out MGElement xamlTitleBar));
        Assert.NotSame(codeTitleBar, xamlTitleBar);
        AssertSameChrome(expected, DescribeChrome(harness.Window, comboBox));

        // Another Dark instance keeps the structures: the values stay those of Dark.
        MGWindow xamlDropdown = comboBox.Dropdown;
        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(4, Point.Zero);
        Assert.Same(xamlDropdown, comboBox.Dropdown);
        AssertSameChrome(expected, DescribeChrome(harness.Window, comboBox));
    }

    [Fact]
    public void Values_Of_The_Owner_Survive_A_Theme_Change_That_Rebuilds_Its_Structure()
    {
        Harness harness = Harness.Create(dark: false);
        MGComboBox<string> comboBox = CreateComboBox(harness);
        Color arrowColor = new(201, 64, 150);
        MGUniformBorderBrush localBorderBrush = new(arrowColor);
        comboBox.DropdownArrowColor = arrowColor;
        comboBox.BorderBrush = localBorderBrush;

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(3, Point.Zero);
        Assert.Equal("Dark.ComboBox", comboBox.AppliedControlTemplateName);

        // A value of the combo box itself, which its template applies without a resolved value store.
        Assert.Equal(arrowColor, comboBox.DropdownArrowColor);

        // The local value written through the facade onto the replaced PART_Border moved to the new part, with the theme default it outranks.
        Assert.Equal(Describe(localBorderBrush), Describe(comboBox.BorderBrush));
        Assert.True(comboBox.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> winner));
        Assert.Equal(UIValueSourceKind.LocalValue, winner.Source.Kind);
        Assert.True(comboBox.GetBorder().TryGetResolvedContribution(UIPilotProperty.BorderBrush, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<IBorderBrush> _));
    }

    [Fact]
    public void A_Window_Border_Follows_The_Theme_Through_A_Rebuilt_Structure_Unless_Set_Locally()
    {
        Harness builtUnderDark = Harness.Create(dark: true);
        MGTheme dark = builtUnderDark.Window.GetTheme();
        MGWindow expected = new(builtUnderDark.Desktop, 0, 0, 300, 200, dark);
        Assert.Equal("Dark.Window", expected.AppliedControlTemplateName);

        Harness harness = Harness.Create(dark: false);
        MGTheme darkBlue = harness.Desktop.Resources.DefaultTheme;
        Assert.NotEqual(Describe(darkBlue.Window.BorderBrush), Describe(dark.Window.BorderBrush));
        MGWindow themed = new(harness.Desktop, 0, 0, 300, 200, darkBlue);
        MGWindow local = new(harness.Desktop, 0, 0, 300, 200, darkBlue);
        Thickness localThickness = new(7);
        local.BorderThickness = localThickness;

        themed.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        local.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);

        // The theme defaults of the window reach the border part of the rebuilt structure.
        Assert.Equal("Dark.Window", themed.AppliedControlTemplateName);
        Assert.Equal(Describe(expected.BorderThickness), Describe(themed.BorderThickness));
        Assert.Equal(Describe(expected.BorderBrush), Describe(themed.BorderBrush));

        // A local border thickness is kept; the border brush, never set locally, still follows the theme.
        Assert.Equal("Dark.Window", local.AppliedControlTemplateName);
        Assert.Equal(Describe(localThickness), Describe(local.BorderThickness));
        Assert.True(local.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> winner));
        Assert.Equal(UIValueSourceKind.LocalValue, winner.Source.Kind);
        Assert.Equal(Describe(expected.BorderBrush), Describe(local.BorderBrush));
    }

    [Fact]
    public void TreeView_Items_Move_To_The_Items_Panel_Of_A_Rebuilt_Structure_And_Follow_The_Theme()
    {
        Harness harness = Harness.Create(dark: false);
        MGTreeView treeView = new(harness.Window);
        ThemeProbeTreeViewItem first = new(harness.Window) { Header = "First" };
        first.AddItem(new MGTreeViewItem(harness.Window) { Header = "Child" });
        ThemeProbeTreeViewItem second = new(harness.Window) { Header = "Second" };
        treeView.AddItem(first);
        treeView.AddItem(second);
        harness.Show(treeView);
        Assert.Equal(MGControlTemplateCatalog.TreeViewTemplateName, treeView.AppliedControlTemplateName);
        MGStackPanel codeItemsPanel = treeView.ItemsPanel;

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = dark;
        harness.Frame(3, Point.Zero);

        Assert.Equal("Dark.TreeView", treeView.AppliedControlTemplateName);
        Assert.NotSame(codeItemsPanel, treeView.ItemsPanel);

        // The root items moved, in order, to the items panel of the new structure; the replaced panel holds none of them.
        Assert.Equal(new MGElement[] { first, second }, treeView.ItemsPanel.Children);
        Assert.Same(treeView.ItemsPanel, first.Parent);
        Assert.Same(treeView.ItemsPanel, second.Parent);
        Assert.Empty(codeItemsPanel.Children);

        // Back in the visual tree, they received the theme change and are laid out.
        Assert.Same(dark, first.LastNotifiedTheme);
        Assert.Same(dark, second.LastNotifiedTheme);
        Assert.True(first.LayoutBounds.Height > 0);

        // An item added later also goes to the new panel.
        MGTreeViewItem third = new(harness.Window) { Header = "Third" };
        treeView.AddItem(third);
        Assert.Same(treeView.ItemsPanel, third.Parent);
    }

    [Fact]
    public void Backgrounds_And_Text_Colors_Set_On_Replaced_Parts_Move_To_The_New_Parts()
    {
        Harness harness = Harness.Create(dark: false);
        MGTabControl tabControl = new(harness.Window);
        VisualStateFillBrush headerAreaBackground = new(new MGSolidFillBrush(Color.Red));
        tabControl.HeaderAreaBackground = headerAreaBackground;
        harness.Show(tabControl);
        MGElement codeHeadersPanel = Part<MGElement>(tabControl, MGTabControl.HeadersPanelPartName);

        MGWindow window = new(harness.Desktop, 0, 0, 300, 200, harness.Desktop.Resources.DefaultTheme);
        MGTextBlock codeTitleText = window.TitleBarTextBlockElement;
        VisualStateSetting<Color?> titleForeground = new(Color.Orange, Color.Orange, Color.Orange);
        codeTitleText.Foreground = titleForeground;
        codeTitleText.DefaultTextForeground.NormalValue = Color.Yellow;

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = dark;
        window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(3, Point.Zero);

        // The header background set through the tab control's facade: the same brush, still local, held by the new headers panel.
        Assert.Equal("Dark.TabControl", tabControl.AppliedControlTemplateName);
        MGElement headersPanel = Part<MGElement>(tabControl, MGTabControl.HeadersPanelPartName);
        Assert.NotSame(codeHeadersPanel, headersPanel);
        Assert.Same(headerAreaBackground, tabControl.HeaderAreaBackground);
        Assert.Equal(Color.Red, Assert.IsType<MGSolidFillBrush>(headersPanel.BackgroundBrush.NormalValue).Color);
        Assert.True(headersPanel.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> headerWinner));
        Assert.Equal(UIValueSourceKind.LocalValue, headerWinner.Source.Kind);

        // The replaced panel let the brush go: a later edit of the brush is recorded by the new panel only.
        Assert.NotSame(headerAreaBackground, codeHeadersPanel.BackgroundBrush);
        headerAreaBackground.NormalValue = new MGSolidFillBrush(Color.Blue);
        Assert.True(headersPanel.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<IFillBrush> _));
        Assert.False(codeHeadersPanel.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<IFillBrush> _));

        // Text colors set on the replaced title text: the whole foreground and one sub-field of the default text foreground.
        Assert.Equal("Dark.Window", window.AppliedControlTemplateName);
        MGTextBlock titleText = window.TitleBarTextBlockElement;
        Assert.NotSame(codeTitleText, titleText);
        Assert.Same(titleForeground, titleText.Foreground);
        Assert.Equal(Color.Yellow, titleText.DefaultTextForeground.NormalValue);
        Assert.True(titleText.TryGetResolvedContribution(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<Color?> _));
        // The sub-fields left alone keep the new theme.
        Assert.Equal(dark.Window.TitleTextForeground.SelectedValue, titleText.DefaultTextForeground.SelectedValue);
    }

    [Fact]
    public void Dark_Variants_Of_The_Xaml_Asset_Templates_Apply_The_Catalog_Defaults()
    {
        // ListBox.Default and ListView.Default are XAML asset templates the catalog registers with its code applicators; Dark.ListBox and
        // Dark.ListView are based on them and must inherit those applicators (backlog task 14: a list box under Dark received no ListBox.* default).
        Harness harness = Harness.Create(dark: true);
        MGTheme dark = harness.Window.GetTheme();
        MGListBox<string> listBox = new(harness.Window);
        listBox.SetItemsSource(new List<string> { "A" });
        MGListView<string> listView = new(harness.Window);
        listView.AddColumn(new ListViewColumnWidth(80), new MGTextBlock(harness.Window, "Name"), item => new MGTextBlock(harness.Window, item));
        MGStackPanel panel = new(harness.Window, Orientation.Vertical);
        panel.TryAddChild(listBox);
        panel.TryAddChild(listView);
        harness.Show(panel);

        Assert.Equal("Dark.ListBox", listBox.AppliedControlTemplateName);
        Assert.Equal(dark.ListBox.MinHeight, listBox.MinHeight);
        Assert.True(listBox.TryGetResolvedContribution(UIPilotProperty.MinHeight, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<int?> minHeight));
        Assert.Equal("ListBox.MinHeight", minHeight.Source.Name);
        Assert.Equal(Describe(dark.ListBox.TitleBorderThickness), Describe(listBox.TitleBorderThickness));

        Assert.Equal("Dark.ListView", listView.AppliedControlTemplateName);
        Assert.Equal(Describe(dark.TitleBackground.GetValue(true)), Describe(listView.HeaderGrid.BackgroundBrush));
        Assert.True(listView.HeaderGrid.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.Template, out UIResolvedValue<VisualStateFillBrush> headerBackground));
        Assert.Equal("ListView.HeaderBackground", headerBackground.Source.Name);
    }

    [Fact]
    public void ListView_Columns_Rows_And_Cells_Move_To_The_Grids_Of_A_Rebuilt_Structure()
    {
        Harness harness = Harness.Create(dark: false);
        MGListView<string> listView = new(harness.Window);
        MGTextBlock nameHeader = new(harness.Window, "Name");
        MGListViewColumn<string> nameColumn = listView.AddColumn(new ListViewColumnWidth(120), nameHeader, item => new MGTextBlock(harness.Window, item));
        MGListViewColumn<string> lengthColumn = listView.AddColumn(new ListViewColumnWidth(1.0), new MGTextBlock(harness.Window, "Length"), item => new MGTextBlock(harness.Window, item.Length.ToString()));
        listView.SetItemsSource(new List<string> { "Alpha", "Beta", "Gamma" });
        listView.SelectionMode = GridSelectionMode.Row;
        harness.Show(listView);
        listView.DataGrid.CurrentSelection = new GridSelection(listView.DataGrid, new GridCell(listView.DataGrid.Rows[1], listView.DataGrid.Columns[0]), GridSelectionMode.Row);
        Assert.Equal(MGControlTemplateCatalog.ListViewTemplateName, listView.AppliedControlTemplateName);
        MGGrid codeHeaderGrid = listView.HeaderGrid;
        MGGrid codeDataGrid = listView.DataGrid;
        MGElement[][] cellsBefore = listView.RowItems.Select(row => new[] { row.GetRowContents()[nameColumn], row.GetRowContents()[lengthColumn] }).ToArray();

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(3, Point.Zero);

        Assert.Equal("Dark.ListView", listView.AppliedControlTemplateName);
        Assert.NotSame(codeHeaderGrid, listView.HeaderGrid);
        Assert.NotSame(codeDataGrid, listView.DataGrid);

        // Columns and rows are definitions of the new grids, and the header and cell elements moved there.
        Assert.Equal(new[] { nameColumn.HeaderColumn, lengthColumn.HeaderColumn }, listView.HeaderGrid.Columns);
        Assert.Equal(new[] { nameColumn.DataColumn, lengthColumn.DataColumn }, listView.DataGrid.Columns);
        Assert.Equal(listView.RowItems.Select(row => row.DataRow), listView.DataGrid.Rows);
        Assert.Same(listView.HeaderGrid, nameHeader.Parent);
        for (int i = 0; i < cellsBefore.Length; i++)
        {
            Dictionary<MGListViewColumn<string>, MGElement> cells = listView.RowItems[i].GetRowContents();
            Assert.Same(cellsBefore[i][0], cells[nameColumn]);
            Assert.Same(cellsBefore[i][1], cells[lengthColumn]);
            Assert.Same(listView.DataGrid, cellsBefore[i][0].Parent);
        }

        Assert.Empty(codeHeaderGrid.Children);
        Assert.Empty(codeDataGrid.Children);

        // The selection mode and the selected row follow.
        Assert.Equal(GridSelectionMode.Row, listView.SelectionMode);
        Assert.Same(listView.DataGrid.Rows[1], listView.SelectedData?.Cell.Row);

        // The grids are laid out, and later changes reach them.
        Assert.True(listView.DataGrid.LayoutBounds.Height > 0);
        listView.ItemsSource.Add("Delta");
        Assert.Equal(4, listView.DataGrid.Rows.Count);
        Assert.Same(listView.DataGrid, listView.RowItems[3].GetRowContents()[nameColumn].Parent);
        lengthColumn.Width.WidthPixels = 50;
        Assert.Equal(50, listView.DataGrid.Columns[1].Length.Pixels);
    }

    private sealed class ThemeProbeTreeViewItem : MGTreeViewItem
    {
        public ThemeProbeTreeViewItem(MGWindow window)
            : base(window)
        {
        }

        public MGTheme LastNotifiedTheme { get; private set; }

        protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
        {
            base.OnThemeChanged(PreviousTheme, CurrentTheme);
            LastNotifiedTheme = CurrentTheme;
        }
    }

    private static MGComboBox<string> CreateComboBox(Harness harness)
    {
        MGComboBox<string> comboBox = new(harness.Window) { PreferredHeight = 30 };
        comboBox.SetItemsSource(new List<string> { "Low", "Medium", "High" });
        comboBox.SelectedIndex = 1;
        harness.Show(comboBox);
        return comboBox;
    }

    private static void AssertSameChrome(IReadOnlyDictionary<string, string> expected, IReadOnlyDictionary<string, string> actual)
    {
        Assert.Equal(expected.Keys.OrderBy(x => x, StringComparer.Ordinal), actual.Keys.OrderBy(x => x, StringComparer.Ordinal));
        string[] mismatches = expected
            .Where(x => !string.Equals(x.Value, actual[x.Key], StringComparison.Ordinal))
            .Select(x => $"{x.Key}: expected {x.Value}, actual {actual[x.Key]}")
            .ToArray();
        Assert.True(mismatches.Length == 0, string.Join(Environment.NewLine, mismatches));
    }

    private static IReadOnlyDictionary<string, string> DescribeChrome(MGWindow window, MGComboBox<string> comboBox)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal);
        DescribeWindow(values, "Window", window);

        values["ComboBox.Padding"] = Describe(comboBox.Padding);
        values["ComboBox.MinHeight"] = comboBox.MinHeight?.ToString() ?? "null";
        values["ComboBox.Background"] = Describe(comboBox.BackgroundBrush);
        values["ComboBox.BorderBrush"] = Describe(comboBox.BorderBrush);
        values["ComboBox.BorderThickness"] = Describe(comboBox.BorderThickness);
        values["ComboBox.DropdownArrowColor"] = comboBox.DropdownArrowColor.ToString();
        values["ComboBox.DropdownArrow.Margin"] = Describe(comboBox.DropdownArrowElement.Margin);
        values["ComboBox.DropdownScrollViewer.Padding"] = Describe(comboBox.DropdownScrollViewer.Padding);
        values["ComboBox.DropdownItemsPanel.Spacing"] = comboBox.DropdownStackPanel.Spacing.ToString();
        values["ComboBox.Dropdown.PreferredWidth"] = comboBox.Dropdown.PreferredWidth?.ToString() ?? "null";
        DescribeWindow(values, "ComboBox.Dropdown", comboBox.Dropdown);
        return values;
    }

    private static void DescribeWindow(Dictionary<string, string> values, string prefix, MGWindow window)
    {
        values[$"{prefix}.Padding"] = Describe(window.Padding);
        values[$"{prefix}.Background"] = Describe(window.BackgroundBrush);
        values[$"{prefix}.BorderBrush"] = Describe(window.BorderBrush);
        values[$"{prefix}.BorderThickness"] = Describe(window.BorderThickness);

        MGDockPanel titleBar = Part<MGDockPanel>(window, MGWindow.TitleBarPartName);
        values[$"{prefix}.TitleBar.Background"] = Describe(titleBar.BackgroundBrush);
        values[$"{prefix}.TitleBar.Padding"] = Describe(titleBar.Padding);
        values[$"{prefix}.TitleBar.MinHeight"] = titleBar.MinHeight?.ToString() ?? "null";

        MGButton closeButton = Part<MGButton>(window, MGWindow.CloseButtonPartName);
        values[$"{prefix}.CloseButton.Background"] = Describe(closeButton.BackgroundBrush);
        values[$"{prefix}.CloseButton.BorderBrush"] = Describe(closeButton.BorderBrush);
        values[$"{prefix}.CloseButton.BorderThickness"] = Describe(closeButton.BorderThickness);
        values[$"{prefix}.CloseButton.Margin"] = Describe(closeButton.Margin);
        values[$"{prefix}.CloseButton.Padding"] = Describe(closeButton.Padding);
        values[$"{prefix}.CloseButton.MinWidth"] = closeButton.MinWidth?.ToString() ?? "null";
        values[$"{prefix}.CloseButton.MinHeight"] = closeButton.MinHeight?.ToString() ?? "null";
        values[$"{prefix}.CloseButton.VerticalAlignment"] = closeButton.VerticalAlignment.ToString();
        values[$"{prefix}.CloseButton.Content"] = closeButton.Content?.GetType().Name ?? "null";

        MGTextBlock titleText = Part<MGTextBlock>(window, MGWindow.TitleBarTextPartName);
        values[$"{prefix}.TitleText.Margin"] = Describe(titleText.Margin);
        values[$"{prefix}.TitleText.Padding"] = Describe(titleText.Padding);
        values[$"{prefix}.TitleText.DefaultTextForeground"] = Describe(titleText.DefaultTextForeground);
        values[$"{prefix}.TitleText.VerticalAlignment"] = titleText.VerticalAlignment.ToString();
    }

    private static T Part<T>(MGElement owner, string name) where T : MGElement
    {
        Assert.True(owner.TryGetTemplatePart(name, out MGElement part), $"{owner.GetType().Name} has no part {name}");
        return Assert.IsAssignableFrom<T>(part);
    }

    private static string Describe(Thickness value) => $"{value.Left},{value.Top},{value.Right},{value.Bottom}";

    private static string Describe(IFillBrush brush) => brush switch
    {
        null => "null",
        MGSolidFillBrush solid => $"solid({solid.Color.R},{solid.Color.G},{solid.Color.B},{solid.Color.A})",
        _ => brush.GetType().Name,
    };

    private static string Describe(VisualStateFillBrush brush)
        => brush == null ? "null" : $"N={Describe(brush.NormalValue)} S={Describe(brush.SelectedValue)} D={Describe(brush.DisabledValue)} F={Describe(brush.FocusedValue)}";

    private static string Describe(IBorderBrush brush) => brush switch
    {
        null => "null",
        MGUniformBorderBrush uniform => $"uniform {Describe(uniform.Brush)}",
        _ => brush.GetType().Name,
    };

    private static string Describe(VisualStateSetting<Color?> setting)
        => setting == null ? "null" : $"N={setting.NormalValue} S={setting.SelectedValue} D={setting.DisabledValue} F={setting.FocusedValue}";

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        /// <summary>A window with an explicit theme, as every XAML root window has: the desktop's default theme (<c>Dark_Blue</c>), or <c>Dark</c>.</summary>
        public static Harness Create(bool dark)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            MGTheme theme = dark ? new MGTheme(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily) : desktop.Resources.DefaultTheme;
            MGWindow window = new(desktop, 24, 24, 480, 260, theme)
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
            Frame(1, Point.Zero);
            Frame(2, Point.Zero);
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
