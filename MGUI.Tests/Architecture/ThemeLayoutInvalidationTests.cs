using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
/// Backlog task 7 (styling-theme-tasks.md), scenario <c>SCN-THEME-001</c>: a real theme change invalidates the layout of the controls whose
/// layout-affecting values it changes, through the invalidation the control template catalog stamps on every value it applies and through the
/// <see cref="MGElement.GetThemeInvalidation"/> overrides (evaluated before the theme callbacks), while a render-only theme change leaves every
/// laid out element valid. Classification: <see cref="UIThemeValueInvalidation"/>, inventoried by <see cref="ThemeValueInvalidationInventoryTests"/>.
/// </summary>
public class ThemeLayoutInvalidationTests
{
    private const UIInvalidationKind LayoutAndDraw = UIInvalidationKind.Draw | UIInvalidationKind.Measure | UIInvalidationKind.Arrange;
    private static readonly Color Tint = new(201, 64, 150);

    private static readonly FieldInfo AppliedTemplateDefaultsField =
        typeof(MGElement).GetField("_AppliedTemplateDefaults", BindingFlags.Instance | BindingFlags.NonPublic)!;

    [Fact]
    public void A_Text_Block_Requests_Layout_Only_When_The_Theme_Changes_The_Font_It_Follows()
    {
        Harness harness = Harness.Create();
        MGTextBlock text = new(harness.Window, "Themed text");
        harness.Show(text);
        MGTheme theme = harness.Window.GetTheme();

        MGTheme recolored = theme.Copy();
        recolored.TextBlockFallbackForeground.Value = new VisualStateColorBrush(Tint);
        recolored.FontSettings.DefaultFontShadowColor = Tint;
        MGTheme resized = theme.Copy();
        resized.FontSettings.DefaultFontSize = theme.FontSettings.DefaultFontSize + 4;
        MGTheme renamed = theme.Copy();
        renamed.FontSettings.DefaultFontFamily = text.FontFamily + " Condensed";

        Assert.Equal(UIInvalidationKind.Draw, text.GetThemeInvalidation(theme, recolored));
        Assert.Equal(LayoutAndDraw, text.GetThemeInvalidation(theme, resized));
        Assert.Equal(LayoutAndDraw, text.GetThemeInvalidation(theme, renamed));

        // A local font size: the text block no longer follows the default size of the theme.
        Assert.True(text.TrySetFontSize(theme.FontSettings.DefaultFontSize + 2));
        Assert.Equal(UIInvalidationKind.Draw, text.GetThemeInvalidation(theme, resized));
    }

    [Fact]
    public void A_Theme_Change_Invalidates_Text_Layout_Only_When_It_Changes_The_Default_Font()
    {
        Harness harness = Harness.Create();
        MGTextBlock text = new(harness.Window, "Themed text");
        harness.Show(text);
        MGTheme theme = harness.Window.GetTheme();
        Assert.True(text.IsLayoutValid);

        MGTheme recolored = theme.Copy();
        recolored.TextBlockFallbackForeground.Value = new VisualStateColorBrush(Tint);
        harness.SwitchTheme(recolored);
        Assert.True(text.IsLayoutValid);

        MGTheme resized = recolored.Copy();
        resized.FontSettings.DefaultFontSize = theme.FontSettings.DefaultFontSize + 4;
        harness.SwitchTheme(resized);
        Assert.Equal(resized.FontSettings.DefaultFontSize, text.FontSize);
        Assert.False(text.IsLayoutValid);
    }

    [Fact]
    public void A_Check_Box_Resizes_Only_When_The_Theme_Changes_Its_Component_Size()
    {
        Harness harness = Harness.Create();
        MGCheckBox checkBox = new(harness.Window);
        harness.Show(checkBox);
        MGTheme theme = harness.Window.GetTheme();
        int size = theme.CheckBoxComponentSize;

        MGTheme recolored = theme.Copy();
        recolored.CheckMarkColor = Tint;
        recolored.CheckBoxCheckedIndicatorStyle = theme.CheckBoxCheckedIndicatorStyle == CheckIndicatorStyle.CheckMark ? CheckIndicatorStyle.FilledSquare : CheckIndicatorStyle.CheckMark;
        MGTheme resized = theme.Copy();
        resized.CheckBoxComponentSize = size + 8;

        Assert.Equal(UIInvalidationKind.Draw, checkBox.GetThemeInvalidation(theme, recolored));
        Assert.Equal(LayoutAndDraw, checkBox.GetThemeInvalidation(theme, resized));

        harness.SwitchTheme(recolored);
        Assert.Equal(Tint, checkBox.CheckMarkColor);
        Assert.True(checkBox.IsLayoutValid);
        Assert.True(checkBox.ButtonElement.IsLayoutValid);

        harness.SwitchTheme(resized);
        Assert.False(checkBox.IsLayoutValid);
        harness.Frame(3);
        harness.Frame(4);
        Assert.Equal(size + 8, checkBox.ButtonElement.LayoutBounds.Width);
    }

    [Fact]
    public void A_Check_Box_Keeps_The_Size_Color_And_Style_Set_Locally_Across_A_Theme_Change()
    {
        Harness harness = Harness.Create();
        MGCheckBox checkBox = new(harness.Window);
        harness.Show(checkBox);
        MGTheme theme = harness.Window.GetTheme();
        int size = theme.CheckBoxComponentSize + 4;
        CheckIndicatorStyle style = theme.CheckBoxCheckedIndicatorStyle == CheckIndicatorStyle.CheckMark ? CheckIndicatorStyle.FilledSquare : CheckIndicatorStyle.CheckMark;
        checkBox.CheckBoxComponentSize = size;
        checkBox.CheckMarkColor = Color.Teal;
        checkBox.CheckedIndicatorStyle = style;
        harness.Settle();
        Assert.True(checkBox.IsLayoutValid);

        // Only two indicator styles exist: the theme keeps its own, which differs from the local one.
        MGTheme changed = theme.Copy();
        changed.CheckBoxComponentSize = theme.CheckBoxComponentSize + 8;
        changed.CheckMarkColor = Tint;
        Assert.Equal(UIInvalidationKind.Draw, checkBox.GetThemeInvalidation(theme, changed));

        harness.SwitchTheme(changed);
        Assert.Equal(size, checkBox.CheckBoxComponentSize);
        Assert.Equal(Color.Teal, checkBox.CheckMarkColor);
        Assert.Equal(style, checkBox.CheckedIndicatorStyle);
        Assert.True(checkBox.IsLayoutValid);
    }

    [Fact]
    public void A_Check_Box_Follows_The_Theme_For_The_Values_Still_Equal_To_The_Previous_Theme()
    {
        Harness harness = Harness.Create();
        MGCheckBox checkBox = new(harness.Window);
        harness.Show(checkBox);
        MGTheme theme = harness.Window.GetTheme();
        checkBox.CheckMarkColor = Color.Teal;

        MGTheme changed = theme.Copy();
        changed.CheckBoxComponentSize = theme.CheckBoxComponentSize + 8;
        changed.CheckMarkColor = Tint;
        changed.CheckBoxCheckedIndicatorStyle = theme.CheckBoxCheckedIndicatorStyle == CheckIndicatorStyle.CheckMark ? CheckIndicatorStyle.FilledSquare : CheckIndicatorStyle.CheckMark;
        Assert.Equal(LayoutAndDraw, checkBox.GetThemeInvalidation(theme, changed));

        harness.SwitchTheme(changed);
        Assert.Equal(changed.CheckBoxComponentSize, checkBox.CheckBoxComponentSize);
        Assert.Equal(changed.CheckBoxCheckedIndicatorStyle, checkBox.CheckedIndicatorStyle);
        Assert.Equal(Color.Teal, checkBox.CheckMarkColor);
        Assert.False(checkBox.IsLayoutValid);
        harness.Settle();

        // The values the first change applied are now compared with that theme, not with the one the check box was created with.
        MGTheme restored = theme.Copy();
        Assert.Equal(LayoutAndDraw, checkBox.GetThemeInvalidation(changed, restored));

        harness.SwitchTheme(restored);
        Assert.Equal(theme.CheckBoxComponentSize, checkBox.CheckBoxComponentSize);
        Assert.Equal(theme.CheckBoxCheckedIndicatorStyle, checkBox.CheckedIndicatorStyle);
        Assert.Equal(Color.Teal, checkBox.CheckMarkColor);
        Assert.False(checkBox.IsLayoutValid);
    }

    [Fact]
    public void A_Property_Grid_Requests_Layout_Only_When_The_Theme_Changes_Its_Row_Metrics()
    {
        Harness harness = Harness.Create();
        MGPropertyGrid propertyGrid = new(harness.Window) { SelectedObject = new PropertyGridSample() };
        harness.Show(propertyGrid);
        MGTheme theme = harness.Window.GetTheme();
        IFillBrush separator = theme.PropertyGrid.RowSeparatorBrush;

        MGTheme recolored = theme.Copy();
        recolored.PropertyGrid.CategoryArrowColor = Tint;
        recolored.PropertyGrid.CategoryHeaderBackground = new VisualStateFillBrush(new MGSolidFillBrush(Tint));
        recolored.PropertyGrid.RowSeparatorBrush = separator == null ? null : new MGSolidFillBrush(Tint);
        Assert.Equal(UIInvalidationKind.Draw, propertyGrid.GetThemeInvalidation(theme, recolored));

        Action<MGThemePropertyGridSettings>[] layoutChanges =
        {
            settings => settings.CategoryHeaderPadding = Grow(settings.CategoryHeaderPadding),
            settings => settings.CategoryHeaderMinHeight += 6,
            settings => settings.RowsSpacing += 2,
            settings => settings.RowPadding = Grow(settings.RowPadding),
            settings => settings.RowSeparatorBrush = separator == null ? new MGSolidFillBrush(Tint) : null,
        };
        foreach (Action<MGThemePropertyGridSettings> change in layoutChanges)
        {
            MGTheme changed = theme.Copy();
            change(changed.PropertyGrid);
            Assert.Equal(LayoutAndDraw, propertyGrid.GetThemeInvalidation(theme, changed));
        }
    }

    [Fact]
    public void A_Graph_Node_Requests_Layout_Only_When_The_Theme_Changes_The_Border_Thickness_Of_Its_Selection_State()
    {
        Harness harness = Harness.Create();
        MGGraphNode node = new(harness.Window);
        MGTheme theme = harness.Window.GetTheme();

        MGTheme thickerSelection = theme.Copy();
        thickerSelection.Graph.NodeSelectedBorderThickness = Grow(theme.Graph.NodeSelectedBorderThickness);
        MGTheme recolored = theme.Copy();
        recolored.Graph.NodeBorderBrush = new MGSolidFillBrush(Tint).AsUniformBorderBrush();
        recolored.Graph.NodeSelectedBorderBrush = new MGSolidFillBrush(Tint).AsUniformBorderBrush();

        Assert.Equal(UIInvalidationKind.Draw, node.GetThemeInvalidation(theme, thickerSelection));
        Assert.Equal(UIInvalidationKind.Draw, node.GetThemeInvalidation(theme, recolored));

        node.IsSelected = true;
        Assert.Equal(LayoutAndDraw, node.GetThemeInvalidation(theme, thickerSelection));
        Assert.Equal(UIInvalidationKind.Draw, node.GetThemeInvalidation(theme, recolored));
    }

    [Fact]
    public void Theme_Invalidation_Is_Evaluated_Before_The_Callback_And_Reaches_The_Parents()
    {
        // The probe copies CheckBoxComponentSize into a plain property that no setter invalidates: only the invalidation it declares can invalidate its layout.
        Harness harness = Harness.Create();
        ThemeCallbackProbe probe = new(harness.Window);
        MGStackPanel panel = new(harness.Window, Orientation.Vertical);
        panel.TryAddChild(probe);
        harness.Show(panel);
        MGTheme theme = harness.Window.GetTheme();
        Assert.True(probe.IsLayoutValid);
        Assert.True(panel.IsLayoutValid);

        MGTheme recolored = theme.Copy();
        recolored.CheckMarkColor = Tint;
        harness.SwitchTheme(recolored);
        Assert.Equal(new[] { "GetThemeInvalidation", "OnThemeChanged" }, probe.Calls);
        Assert.True(probe.IsLayoutValid);
        Assert.True(panel.IsLayoutValid);

        MGTheme resized = recolored.Copy();
        resized.CheckBoxComponentSize = theme.CheckBoxComponentSize + 8;
        harness.SwitchTheme(resized);
        Assert.Equal(resized.CheckBoxComponentSize, probe.ComponentSize);
        Assert.False(probe.IsLayoutValid);
        Assert.False(panel.IsLayoutValid);
    }

    [Fact]
    public void A_Theme_Change_Of_A_Template_Size_Invalidates_The_Controls_It_Resizes()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = new(harness.Window);
        comboBox.SetItemsSource(new List<string> { "Alpha", "Beta" });
        MGTreeView treeView = new(harness.Window);
        MGTreeViewItem parent = new(harness.Window) { Header = "Parent" };
        parent.AddItem(new MGTreeViewItem(harness.Window) { Header = "Child" });
        treeView.AddItem(parent);
        MGTabControl tabControl = new(harness.Window);
        tabControl.AddTab("First", new MGTextBlock(harness.Window, "First tab"));
        tabControl.AddTab("Second", new MGTextBlock(harness.Window, "Second tab"));
        MGStackPanel panel = new(harness.Window, Orientation.Vertical);
        panel.TryAddChild(comboBox);
        panel.TryAddChild(treeView);
        panel.TryAddChild(tabControl);
        harness.Show(panel);

        AssertLayoutStamp(harness.Window, "Window.CloseButtonMinWidth");
        AssertLayoutStamp(comboBox, "ComboBox.DropdownItemsSpacing");
        AssertLayoutStamp(treeView, "TreeView.ItemsPanelSpacing");
        AssertLayoutStamp(treeView, "TreeView.IndentSize");
        AssertLayoutStamp(tabControl, "TabControl.HeadersSpacing");

        // The dropdown items panel lives in the dropdown window, outside the tree of the combo box: only the stamp reaches the combo box.
        MGTheme spacedDropdown = harness.Window.GetTheme().Copy();
        spacedDropdown.ComboBox.DropdownItemsSpacing += 3;
        harness.SwitchTheme(spacedDropdown);
        Assert.True(comboBox.TryGetAppliedTemplateDefault("ComboBox.DropdownItemsSpacing", out UIResolvedValue<int> spacing));
        Assert.Equal(spacedDropdown.ComboBox.DropdownItemsSpacing, spacing.Value);
        Assert.False(comboBox.IsLayoutValid);
        harness.Settle();
        Assert.True(comboBox.IsLayoutValid);

        MGTheme indented = harness.Window.GetTheme().Copy();
        indented.TreeViewIndentSize += 5;
        harness.SwitchTheme(indented);
        Assert.Equal(indented.TreeViewIndentSize, treeView.IndentSize);
        Assert.False(treeView.IsLayoutValid);
        harness.Settle();

        MGTheme spacedHeaders = harness.Window.GetTheme().Copy();
        spacedHeaders.TabControl.HeadersSpacing += 4;
        harness.SwitchTheme(spacedHeaders);
        Assert.False(tabControl.IsLayoutValid);
        harness.Settle();

        MGTheme widerCloseButton = harness.Window.GetTheme().Copy();
        widerCloseButton.Window.CloseButtonMinWidth += 6;
        harness.SwitchTheme(widerCloseButton);
        Assert.False(harness.Window.IsLayoutValid);
    }

    [Fact]
    public void A_Theme_Refresh_That_Reapplies_Unchanged_Layout_Values_Keeps_The_Owner_Valid()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = new(harness.Window);
        harness.Show(comboBox);
        Assert.True(comboBox.IsLayoutValid);
        Assert.True(harness.Window.IsLayoutValid);

        harness.SwitchTheme(harness.Window.GetTheme().Copy());

        Assert.True(comboBox.IsLayoutValid);
        Assert.True(harness.Window.IsLayoutValid);
    }

    [Fact]
    public void A_Render_Only_Theme_Change_Keeps_Every_Laid_Out_Element_Valid()
    {
        Harness harness = Harness.Create();
        Scene scene = Scene.Build(harness);
        List<MGElement> laidOut = scene.Elements().Where(element => element.IsLayoutValid).ToList();
        Assert.Contains(scene.CheckBox, laidOut);
        Assert.Contains(scene.ComboBox, laidOut);
        Assert.Contains(scene.PropertyGrid, laidOut);

        harness.SwitchTheme(CopyWithRenderOnlyChanges(harness.Window.GetTheme()));

        Assert.Equal(Tint, scene.CheckBox.CheckMarkColor); // the refresh reached the controls
        Assert.Empty(laidOut.Where(element => !element.IsLayoutValid).Select(Describe));
    }

    [Fact]
    public void Every_Template_Value_Carries_The_Invalidation_Its_Value_Type_Requires()
    {
        Harness harness = Harness.Create();
        Scene scene = Scene.Build(harness);

        List<string> violations = new();
        HashSet<string> keys = new(StringComparer.Ordinal);
        foreach (MGElement owner in scene.TemplateOwners())
        {
            foreach ((string key, Type valueType, UIValueResolutionSource source) in AppliedTemplateValues(owner))
            {
                keys.Add(key);
                UIInvalidationKind? expected = ExpectedTemplateInvalidation(key, valueType);
                if (expected != source.Invalidation)
                    violations.Add($"{key} ({valueType.Name}) on {owner.GetType().Name}: {source.Invalidation}, expected {expected?.ToString() ?? "a classified value type"}");
            }
        }

        Assert.Empty(violations.Distinct());
        Assert.Superset(new HashSet<string>(StringComparer.Ordinal)
        {
            "Window.CloseButtonMinWidth", "ToolTip.MinWidth", "ToolTip.DrawOffset", "ListBox.ItemsPanelVerticalAlignment", "ListView.HeaderGridLinesVisibility",
            "ListView.DataGridSpacing", "ListView.DataGridLineMargin", "ComboBox.DropdownItemsSpacing", "ComboBox.DropdownItem.HorizontalAlignment",
            "TreeView.ItemsPanelSpacing", "TreeView.IndentSize", "PropertyGrid.CategoriesSpacing", "PropertyGrid.CategoriesPanelVerticalAlignment",
            "NumericUpDown.SpinnerWidth", "NumericUpDown.SpinnerMinWidth", "TabControl.HeadersSpacing", "TabControl.SelectedHeaderTemplate",
            "TextBox.HorizontalContentAlignment", "TextBox.CharacterCount.Margin", "TextBox.CharacterCount.FontSize", "TextBox.CharacterCount.VerticalAlignment",
            "TextBox.LimitedCharacterCountFormatString",
        }, keys);
    }

    /// <summary>The invalidation a template value must carry, from the CLR type the catalog applies it with.</summary>
    private static UIInvalidationKind? ExpectedTemplateInvalidation(string key, Type valueType)
    {
        // Paddings, margins, border thicknesses, min sizes, spacings, indents, alignments, and grid lines, whose edges add outer padding.
        if (valueType == typeof(Thickness) || valueType == typeof(int) || valueType == typeof(HorizontalAlignment) || valueType == typeof(VerticalAlignment)
            || valueType == typeof(GridLinesVisibility))
            return UIThemeValueInvalidation.LayoutAffecting;

        // The markup of a text box's character count texts (backlog task 11) changes the measured text; any other string is a control template name.
        if (valueType == typeof(string))
            return key.EndsWith("CharacterCountFormatString", StringComparison.Ordinal) ? UIThemeValueInvalidation.LayoutAffecting : UIThemeValueInvalidation.Structural;

        // Brushes, colors, draw offsets and clipping.
        if (typeof(IFillBrush).IsAssignableFrom(valueType) || typeof(IBorderBrush).IsAssignableFrom(valueType) || valueType == typeof(VisualStateFillBrush)
            || valueType == typeof(VisualStateSetting<Color?>) || valueType == typeof(Color) || valueType == typeof(Color?) || valueType == typeof(Point)
            || valueType == typeof(bool))
            return UIThemeValueInvalidation.RenderOnly;

        return null;
    }

    private static void AssertLayoutStamp(MGElement owner, string key)
    {
        Assert.True(owner.TryGetAppliedTemplateDefault(key, out UIResolvedValue<int> applied), $"{key} was not applied to {owner.GetType().Name}");
        Assert.Equal(UIThemeValueInvalidation.LayoutAffecting, applied.Source.Invalidation);
    }

    private static IEnumerable<(string Key, Type ValueType, UIValueResolutionSource Source)> AppliedTemplateValues(MGElement owner)
    {
        Dictionary<string, object> appliedDefaults = (Dictionary<string, object>)AppliedTemplateDefaultsField.GetValue(owner)!;
        foreach (KeyValuePair<string, object> entry in appliedDefaults.ToList())
        {
            Type resolvedType = entry.Value.GetType();
            UIValueResolutionSource source = (UIValueResolutionSource)resolvedType.GetProperty(nameof(UIResolvedValue<int>.Source))!.GetValue(entry.Value)!;
            yield return (entry.Key, resolvedType.GetGenericArguments()[0], source);
        }
    }

    /// <summary>A copy of <paramref name="source"/> in which every value <see cref="UIThemeValueInvalidation"/> classifies as render-only is changed.</summary>
    private static MGTheme CopyWithRenderOnlyChanges(MGTheme source)
    {
        MGTheme copy = source.Copy();
        foreach ((string path, UIInvalidationKind invalidation) in UIThemeValueInvalidation.Entries)
        {
            if (invalidation != UIThemeValueInvalidation.RenderOnly || path == UIThemeValueInvalidation.BackgroundsPath)
                continue;

            string[] segments = path.Split('.');
            object target = copy;
            PropertyInfo property = typeof(MGTheme).GetProperty(segments[0])!;
            if (segments.Length == 2)
            {
                target = property.GetValue(copy)!;
                property = target.GetType().GetProperty(segments[1])!;
            }

            ChangeRenderOnlyValue(target, property);
        }

        foreach (MGElementType elementType in Enum.GetValues<MGElementType>())
            copy.SetBackgroundBrush(elementType, new VisualStateFillBrush(new MGSolidFillBrush(Tint)));

        return copy;
    }

    private static void ChangeRenderOnlyValue(object target, PropertyInfo property)
    {
        object value = property.GetValue(target);
        switch (value)
        {
            case ThemeManagedVisualStateFillBrush managedFill:
                managedFill.Value = new VisualStateFillBrush(new MGSolidFillBrush(Tint));
                return;
            case ThemeManagedVisualStateColorBrush managedColor:
                managedColor.Value = new VisualStateColorBrush(Tint);
                return;
            case ThemeManagedFillBrush managedBrush:
                managedBrush.Value = new MGSolidFillBrush(Tint);
                return;
            case List<ThemeManagedFillBrush> alternatingRows:
                alternatingRows.Clear();
                alternatingRows.Add(new ThemeManagedFillBrush(new MGSolidFillBrush(Tint)));
                alternatingRows.Add(new ThemeManagedFillBrush(new MGSolidFillBrush(Color.Teal)));
                return;
        }

        Type type = property.PropertyType;
        object changed =
            type == typeof(Color) ? Tint :
            type == typeof(IFillBrush) ? new MGSolidFillBrush(Tint) :
            type == typeof(IBorderBrush) ? new MGSolidFillBrush(Tint).AsUniformBorderBrush() :
            type == typeof(VisualStateFillBrush) ? new VisualStateFillBrush(new MGSolidFillBrush(Tint)) :
            type == typeof(VisualStateColorBrush) ? new VisualStateColorBrush(Tint) :
            type == typeof(VisualStateSetting<Color?>) ? new VisualStateSetting<Color?>(Tint, Tint, Tint) :
            type == typeof(Point) ? new Point(((Point)value!).X + 3, ((Point)value).Y + 2) :
            type == typeof(bool) ? !(bool)value! :
            type.IsEnum ? Enum.GetValues(type).Cast<object>().First(candidate => !candidate.Equals(value)) :
            throw new InvalidOperationException($"No render-only change is defined for {property.DeclaringType!.Name}.{property.Name} ({type}).");
        property.SetValue(target, changed);
    }

    private static Thickness Grow(Thickness thickness)
        => new(thickness.Left + 3, thickness.Top + 1, thickness.Right + 3, thickness.Bottom + 1);

    private static string Describe(MGElement element)
        => $"{element.GetType().Name}{(string.IsNullOrEmpty(element.Name) ? "" : $" '{element.Name}'")} under {element.Parent?.GetType().Name ?? "<no parent>"}";

    /// <summary>Copies <see cref="MGTheme.CheckBoxComponentSize"/> into a plain property in its theme callback, declares that copy in
    /// <see cref="GetThemeInvalidation"/> like <see cref="MGCheckBox"/> does, and records the order of the two calls.</summary>
    private sealed class ThemeCallbackProbe : MGElement
    {
        public ThemeCallbackProbe(MGWindow window)
            : base(window, MGElementType.Custom)
        {
            ComponentSize = GetTheme().CheckBoxComponentSize;
        }

        public int ComponentSize { get; private set; }

        public List<string> Calls { get; } = new();

        protected internal override UIInvalidationKind GetThemeInvalidation(MGTheme PreviousTheme, MGTheme CurrentTheme)
        {
            Calls.Add(nameof(GetThemeInvalidation));
            return UIThemeValueInvalidation.ForChange(nameof(MGTheme.CheckBoxComponentSize), ComponentSize, CurrentTheme.CheckBoxComponentSize);
        }

        protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
        {
            Calls.Add(nameof(OnThemeChanged));
            ComponentSize = CurrentTheme.CheckBoxComponentSize;
        }
    }

    private sealed class PropertyGridSample
    {
        public string Name { get; set; } = "Sample";
        public int Count { get; set; } = 3;
        public bool Enabled { get; set; } = true;
    }

    /// <summary>A window holding one of most themed controls, laid out.</summary>
    private sealed record Scene(Harness Harness, MGCheckBox CheckBox, MGComboBox<string> ComboBox, MGToolTip ToolTip, MGPropertyGrid PropertyGrid)
    {
        public static Scene Build(Harness harness)
        {
            MGWindow window = harness.Window;
            MGButton button = new(window);
            button.SetContent(new MGTextBlock(window, "Button"));
            MGToolTip toolTip = new(window, button, 120, 40);
            button.ToolTip = toolTip;
            MGToggleButton toggleButton = new(window);
            toggleButton.SetContent(new MGTextBlock(window, "Toggle"));
            MGCheckBox checkBox = new(window, true);
            MGComboBox<string> comboBox = new(window);
            comboBox.SetItemsSource(new List<string> { "Alpha", "Beta" });
            MGListBox<string> listBox = new(window);
            listBox.SetItemsSource(new List<string> { "One", "Two", "Three" });
            MGTreeView treeView = new(window);
            MGTreeViewItem parent = new(window) { Header = "Parent" };
            parent.AddItem(new MGTreeViewItem(window) { Header = "Child" });
            treeView.AddItem(parent);
            MGTabControl tabControl = new(window);
            tabControl.AddTab("First", new MGTextBlock(window, "First tab"));
            tabControl.AddTab("Second", new MGTextBlock(window, "Second tab"));
            MGScrollViewer scrollViewer = new(window);
            scrollViewer.SetContent(new MGTextBlock(window, "Scrolled content"));
            MGPropertyGrid propertyGrid = new(window) { SelectedObject = new PropertyGridSample() };

            MGStackPanel root = new(window, Orientation.Vertical);
            foreach (MGElement child in new MGElement[]
            {
                new MGTextBlock(window, "Themed text"), button, toggleButton, checkBox, new MGRadioButton(window, "Group"), comboBox, listBox,
                new MGListView<string>(window), treeView, tabControl, new MGExpander(window), scrollViewer, new MGTextBox(window),
                new MGNumericUpDown(window), propertyGrid, new MGSlider(window, 0, 100, 40), new MGProgressBar(window, 0, 100, 60),
            })
            {
                root.TryAddChild(child);
            }

            harness.Show(root);
            harness.Settle();
            return new(harness, checkBox, comboBox, toolTip, propertyGrid);
        }

        public IEnumerable<MGElement> Elements() => Harness.Window.EnumerateVisualTree(true).Distinct();

        /// <summary><see cref="Elements"/>, plus the templated elements outside the tree of the window: the tooltip, the dropdown window and the dropdown items.</summary>
        public IEnumerable<MGElement> TemplateOwners()
        {
            IEnumerable<MGElement> dropdownItems = ((IEnumerable)typeof(MGComboBox<string>).GetProperty("TemplatedItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(ComboBox)!)
                .Cast<object>()
                .Select(item => (MGElement)item.GetType().GetProperty("Element")!.GetValue(item)!);
            IEnumerable<MGElement> dropdownWindow = ComboBox.Dropdown?.EnumerateVisualTree(true) ?? Enumerable.Empty<MGElement>();
            return Elements().Concat(dropdownItems).Concat(dropdownWindow).Append(ToolTip).Distinct();
        }
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 1024));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 10, 10, 1100, 1000);
            Harness harness = new(runtime, desktop, window);
            harness.Frame(0);
            return harness;
        }

        /// <summary>Adds the element to the window, shows the window and runs two frames so layout is settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            if (!Desktop.Windows.Contains(Window))
                Desktop.Windows.Add(Window);
            Frame(1);
            Frame(2);
        }

        public void Settle()
        {
            Frame(3);
            Frame(4);
        }

        /// <summary>Switches the theme of the window's resource scope, which notifies the whole window synchronously.</summary>
        public void SwitchTheme(MGTheme theme) => Window.GetResources().DefaultTheme = theme;

        public void Frame(int frameIndex)
        {
            MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
