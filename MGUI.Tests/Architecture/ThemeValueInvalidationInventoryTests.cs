using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Backlog task 7 (styling-theme-tasks.md): the render-only / layout-affecting inventory of <see cref="MGTheme"/> values,
/// <see cref="UIThemeValueInvalidation"/>, is complete, agrees with the invalidation the resolved value store attaches to its pilots, and every
/// theme callback that reads a layout-affecting value declares it through <see cref="MGElement.GetThemeInvalidation"/>.
/// Behaviour on a real theme change: <see cref="ThemeLayoutInvalidationTests"/>.
/// </summary>
public class ThemeValueInvalidationInventoryTests
{
    [Fact]
    public void Every_Theme_Value_Is_Classified_And_Every_Entry_Names_A_Theme_Value()
    {
        HashSet<string> themeValuePaths = EnumerateThemeValues().Select(x => x.Path).ToHashSet(StringComparer.Ordinal);
        themeValuePaths.Add(UIThemeValueInvalidation.BackgroundsPath);

        Assert.Empty(themeValuePaths.Except(UIThemeValueInvalidation.Entries.Keys, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Empty(UIThemeValueInvalidation.Entries.Keys.Except(themeValuePaths, StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Fact]
    public void Entries_Use_Only_The_Three_Documented_Kinds()
    {
        Assert.Equal(UIInvalidationKind.Draw, UIThemeValueInvalidation.RenderOnly);
        Assert.Equal(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, UIThemeValueInvalidation.LayoutAffecting);
        Assert.Equal(UIInvalidationKind.Structure | UIInvalidationKind.Measure | UIInvalidationKind.Arrange, UIThemeValueInvalidation.Structural);

        UIInvalidationKind[] kinds = { UIThemeValueInvalidation.RenderOnly, UIThemeValueInvalidation.LayoutAffecting, UIThemeValueInvalidation.Structural };
        Assert.All(UIThemeValueInvalidation.Entries, entry => Assert.Contains(entry.Value, kinds));
    }

    /// <summary>A value shaped like a pilot of the resolved value store carries the invalidation the store attaches to that pilot
    /// (<see cref="UIPilotPropertyResolver.KindOf"/>): a thickness feeds Margin, Padding or BorderThickness; a size (min size, spacing, indent, font size)
    /// changes measurement like MinHeight; a brush feeds Background or BorderBrush; a color feeds a text foreground. The one exception is explicit.</summary>
    [Fact]
    public void Pilot_Shaped_Values_Carry_The_Invalidation_Of_Their_Pilot()
    {
        Assert.Equal(UIPilotPropertyResolver.KindOf(UIPilotProperty.Padding), UIPilotPropertyResolver.KindOf(UIPilotProperty.Margin));
        Assert.Equal(UIPilotPropertyResolver.KindOf(UIPilotProperty.Padding), UIPilotPropertyResolver.KindOf(UIPilotProperty.BorderThickness));
        Assert.Equal(UIPilotPropertyResolver.KindOf(UIPilotProperty.Background), UIPilotPropertyResolver.KindOf(UIPilotProperty.DefaultTextForeground));

        Dictionary<string, string> exceptions = new(StringComparer.Ordinal)
        {
            ["PropertyGrid.RowSeparatorBrush"] = "its presence adds or removes the separator border of every row",
        };

        List<string> mismatches = new();
        foreach ((string path, PropertyInfo property) in EnumerateThemeValues())
        {
            UIPilotProperty? pilot = PilotShapeOf(property.PropertyType);
            if (pilot == null || exceptions.ContainsKey(path) || !UIThemeValueInvalidation.TryGetInvalidation(path, out UIInvalidationKind invalidation))
                continue;

            UIInvalidationKind pilotInvalidation = UIPilotPropertyResolver.KindOf(pilot.Value);
            if (invalidation != pilotInvalidation)
                mismatches.Add($"{path} ({property.PropertyType.Name}) is shaped like {pilot.Value}: {invalidation} instead of {pilotInvalidation}");
        }

        Assert.Empty(mismatches);
        Assert.All(exceptions.Keys, path => Assert.Equal(UIThemeValueInvalidation.LayoutAffecting, UIThemeValueInvalidation.GetInvalidation(path)));
    }

    /// <summary>A value whose type does not tell what it invalidates is pinned one by one.</summary>
    [Fact]
    public void Values_Without_A_Pilot_Shape_Are_Pinned()
    {
        Dictionary<string, UIInvalidationKind> pinned = new(StringComparer.Ordinal)
        {
            [UIThemeValueInvalidation.BackgroundsPath] = UIThemeValueInvalidation.RenderOnly,
            ["ControlTemplateMappings"] = UIThemeValueInvalidation.Structural,
            ["ControlTemplateTypeMappings"] = UIThemeValueInvalidation.Structural,
            ["CheckBoxCheckedIndicatorStyle"] = UIThemeValueInvalidation.RenderOnly,
            ["DefaultTextBlockWrapText"] = UIThemeValueInvalidation.LayoutAffecting,
            ["DefaultTextBlockAutoWidthFromContent"] = UIThemeValueInvalidation.LayoutAffecting,
            ["DefaultButtonAutoWidthFromContent"] = UIThemeValueInvalidation.LayoutAffecting,
            ["DefaultComboBoxAutoWidthFromContent"] = UIThemeValueInvalidation.LayoutAffecting,
            ["ToolTipOffset"] = UIThemeValueInvalidation.RenderOnly,
            ["FontSettings.UseExactScale"] = UIThemeValueInvalidation.RenderOnly,
            ["FontSettings.DefaultFontFamily"] = UIThemeValueInvalidation.LayoutAffecting,
            ["FontSettings.DefaultFontShadowOffset"] = UIThemeValueInvalidation.RenderOnly,
        };

        IEnumerable<string> valuesWithoutPilotShape = EnumerateThemeValues()
            .Where(x => PilotShapeOf(x.Property.PropertyType) == null)
            .Select(x => x.Path)
            .Append(UIThemeValueInvalidation.BackgroundsPath);

        Assert.Equal(pinned.Keys.OrderBy(x => x, StringComparer.Ordinal), valuesWithoutPilotShape.OrderBy(x => x, StringComparer.Ordinal));
        Assert.All(pinned, pin => Assert.Equal(pin.Value, UIThemeValueInvalidation.GetInvalidation(pin.Key)));
    }

    [Fact]
    public void ForChange_Adds_The_Classification_Only_When_The_Value_Changes()
    {
        UIInvalidationKind layoutAndDraw = UIInvalidationKind.Draw | UIInvalidationKind.Measure | UIInvalidationKind.Arrange;

        Assert.Equal(UIInvalidationKind.Draw, UIThemeValueInvalidation.ForChange("CheckBoxComponentSize", 16, 16));
        Assert.Equal(layoutAndDraw, UIThemeValueInvalidation.ForChange("CheckBoxComponentSize", 16, 20));
        Assert.Equal(UIInvalidationKind.Draw, UIThemeValueInvalidation.ForChange("CheckMarkColor", Color.White, Color.Red));
        Assert.Equal(UIInvalidationKind.Draw, UIThemeValueInvalidation.ForChange("FontSettings.DefaultFontFamily", "Arial", "ARIAL", StringComparer.OrdinalIgnoreCase));
        Assert.Equal(UIInvalidationKind.Draw | UIThemeValueInvalidation.Structural, UIThemeValueInvalidation.ForChange("ControlTemplateMappings", "A", "B"));

        Assert.True(UIThemeValueInvalidation.IsLayoutAffecting("Window.CloseButtonMinWidth"));
        Assert.False(UIThemeValueInvalidation.IsLayoutAffecting("Window.BorderBrush"));
        Assert.False(UIThemeValueInvalidation.TryGetInvalidation("Window.Missing", out _));
        Assert.False(UIThemeValueInvalidation.TryGetInvalidation(null, out _));
        Assert.Throws<ArgumentException>(() => UIThemeValueInvalidation.ForChange("Window.Missing", 1, 2));
    }

    /// <summary>Every <see cref="MGElement.OnThemeChanged"/> override is inventoried. A control whose callback reads a layout-affecting theme value
    /// overrides <see cref="MGElement.GetThemeInvalidation"/>; the others read only render-only values or constants (a control that rebuilds parts
    /// gets their layout values from templates, which stamp their own invalidation).</summary>
    [Fact]
    public void Theme_Callbacks_That_Read_Layout_Affecting_Values_Declare_Their_Invalidation()
    {
        Dictionary<string, string> callbacksReadingLayoutValues = new(StringComparer.Ordinal)
        {
            [nameof(MGTextBlock)] = "FontSettings.DefaultFontFamily, FontSettings.DefaultFontSize",
            [nameof(MGCheckBox)] = "CheckBoxComponentSize",
            [nameof(MGPropertyGrid)] = "PropertyGrid.CategoryHeaderPadding, CategoryHeaderMinHeight, RowsSpacing, RowPadding, RowSeparatorBrush",
            [nameof(MGGraphNode)] = "Graph.NodeBorderThickness, Graph.NodeSelectedBorderThickness",
            ["MGListBox`1"] = "ListBox.ItemPadding, ListBox.ItemContentPadding (backlog task 14); also ListBoxItemAlternatingRowBackgrounds and the default item container brushes",
        };
        Dictionary<string, string> callbacksReadingNoLayoutValue = new(StringComparer.Ordinal)
        {
            [nameof(MGButton)] = "Backgrounds",
            [nameof(MGToggleButton)] = "Backgrounds, TextBlockFallbackForeground",
            [nameof(MGMenuBarItem)] = "Backgrounds, TextBlockFallbackForeground",
            [nameof(MGExpander)] = "DropdownArrowColor",
            [nameof(MGScrollViewer)] = "ScrollBarOuterBrush, ScrollBarInnerBrush",
            [nameof(MGTreeViewItem)] = "neutral expander chrome constants, TreeViewSelectionBackground, TreeViewSelectionForeground",
            ["TreeViewExpanderToggleButton"] = "neutral chrome constants",
            [nameof(MGContextMenu)] = "rebuilds its default item wrappers, whose layout values come from templates",
            [nameof(MGGraphView)] = "re-synchronizes its node controls and selection visuals",
            [nameof(MGGraphPort)] = "rebuilds its connector icons, sized by constants and zoom",
            [nameof(MGGraphCommentBox)] = "resets its captured zoom metrics",
        };

        Type[] elementTypes = typeof(MGElement).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(MGElement))).ToArray();

        Assert.Equal(
            callbacksReadingLayoutValues.Keys.Concat(callbacksReadingNoLayoutValue.Keys).OrderBy(x => x, StringComparer.Ordinal),
            elementTypes.Where(type => DeclaresOverride(type, nameof(MGElement.OnThemeChanged))).Select(type => type.Name).OrderBy(x => x, StringComparer.Ordinal));
        Assert.Equal(
            callbacksReadingLayoutValues.Keys.OrderBy(x => x, StringComparer.Ordinal),
            elementTypes.Where(type => DeclaresOverride(type, nameof(MGElement.GetThemeInvalidation))).Select(type => type.Name).OrderBy(x => x, StringComparer.Ordinal));
    }

    private static bool DeclaresOverride(Type type, string methodName)
        => type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
            .Any(method => method.Name == methodName && method.GetBaseDefinition().DeclaringType == typeof(MGElement));

    /// <summary>Every public property of <see cref="MGTheme"/>, each settings group expanded into its own properties (<c>"Group.Setting"</c>).</summary>
    private static IEnumerable<(string Path, PropertyInfo Property)> EnumerateThemeValues()
    {
        foreach (PropertyInfo property in typeof(MGTheme).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (IsSettingsGroup(property.PropertyType))
            {
                foreach (PropertyInfo setting in property.PropertyType.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                    yield return ($"{property.Name}.{setting.Name}", setting);
            }
            else
            {
                yield return (property.Name, property);
            }
        }
    }

    private static bool IsSettingsGroup(Type type)
        => type.IsClass && type.Assembly == typeof(MGTheme).Assembly && type.Name.EndsWith("Settings", StringComparison.Ordinal);

    private static UIPilotProperty? PilotShapeOf(Type type)
    {
        if (type == typeof(Thickness))
            return UIPilotProperty.Padding;
        if (type == typeof(int))
            return UIPilotProperty.MinHeight;
        if (typeof(IBorderBrush).IsAssignableFrom(type))
            return UIPilotProperty.BorderBrush;
        if (typeof(IFillBrush).IsAssignableFrom(type) || type == typeof(VisualStateFillBrush) || type == typeof(ThemeManagedFillBrush)
            || type == typeof(ThemeManagedVisualStateFillBrush) || type == typeof(List<ThemeManagedFillBrush>))
            return UIPilotProperty.Background;
        if (type == typeof(Color) || type == typeof(VisualStateColorBrush) || type == typeof(ThemeManagedVisualStateColorBrush)
            || type == typeof(VisualStateSetting<Color?>))
            return UIPilotProperty.DefaultTextForeground;
        return null;
    }
}
