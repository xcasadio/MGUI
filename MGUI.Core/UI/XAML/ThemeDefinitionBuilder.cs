using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace MGUI.Core.UI.XAML
{
    public static class ThemeDefinitionBuilder
    {
        private const PressedModifierType DefaultPressedModifierType = PressedModifierType.Darken;
        private const float DefaultPressedModifier = 0.06f;

        public static MGTheme Build(ThemeDefinition Definition, string DefaultFontFamily, MGTheme BaseTheme = null)
        {
            if (Definition == null)
            {
                throw new ArgumentNullException(nameof(Definition));
            }

            string FontFamily = Definition.FontSettings?.DefaultFontFamily ?? BaseTheme?.FontSettings?.DefaultFontFamily ?? DefaultFontFamily;
            if (string.IsNullOrWhiteSpace(FontFamily))
            {
                throw new InvalidOperationException($"A {nameof(ThemeDefinition)} requires a default font family either from the definition, the base theme, or the caller.");
            }

            MGTheme Result = BaseTheme?.Copy() ?? MGTheme.CreateEmpty(FontFamily);
            Apply(Result, Definition, FontFamily);
            return Result;
        }

        public static void Apply(MGTheme Theme, ThemeDefinition Definition, string DefaultFontFamily = null)
        {
            if (Theme == null)
            {
                throw new ArgumentNullException(nameof(Theme));
            }

            if (Definition == null)
            {
                throw new ArgumentNullException(nameof(Definition));
            }

            ApplyFontSettings(Theme, Definition.FontSettings, DefaultFontFamily);
            ApplyBackgrounds(Theme, Definition.Backgrounds);
            ApplyControlTemplates(Theme, Definition.ControlTemplates);
            ApplyWindow(Theme.Window, Definition.Window);
            ApplyOverlay(Theme.Overlay, Definition.Overlay);
            ApplyContextMenu(Theme.ContextMenu, Definition.ContextMenu);
            ApplyContextMenuItem(Theme.ContextMenuItem, Definition.ContextMenuItem);
            ApplyListBox(Theme.ListBox, Definition.ListBox);
            ApplyListView(Theme.ListView, Definition.ListView);
            ApplyComboBox(Theme.ComboBox, Definition.ComboBox);
            ApplyTreeViewTemplate(Theme.TreeViewTemplate, Definition.TreeViewTemplate);
            ApplyTabControl(Theme.TabControl, Definition.TabControl);
            ApplyDocking(Theme.Docking, Definition.Docking);
            ApplyProperties(Theme, Definition.Properties);
        }

        private static void ApplyControlTemplates(MGTheme Theme, IEnumerable<ThemeControlTemplateDefinition> Definitions)
        {
            if (Definitions == null)
            {
                return;
            }

            foreach (ThemeControlTemplateDefinition Definition in Definitions)
            {
                if (Definition == null || string.IsNullOrWhiteSpace(Definition.TemplateName))
                {
                    continue;
                }

                bool hasTarget = false;

                if (Definition.ElementType.HasValue)
                {
                    Theme.SetControlTemplateMapping(Definition.ElementType.Value, Definition.TemplateName);
                    hasTarget = true;
                }

                if (!string.IsNullOrWhiteSpace(Definition.ControlTypeName))
                {
                    Theme.SetControlTemplateMapping(ResolveControlType(Definition.ControlTypeName), Definition.TemplateName);
                    hasTarget = true;
                }

                if (!hasTarget)
                {
                    throw new InvalidOperationException($"{nameof(ThemeControlTemplateDefinition)} requires either {nameof(ThemeControlTemplateDefinition.ElementType)} or {nameof(ThemeControlTemplateDefinition.ControlTypeName)}.");
                }
            }
        }

        private static Type ResolveControlType(string controlTypeName)
        {
            Type exactType = Type.GetType(controlTypeName, throwOnError: false);
            if (exactType != null)
            {
                ValidateControlType(exactType, controlTypeName);
                return exactType;
            }

            List<Type> matches = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(type => type != null
                    && !type.IsAbstract
                    && typeof(MGElement).IsAssignableFrom(type)
                    && (string.Equals(type.FullName, controlTypeName, StringComparison.Ordinal)
                        || string.Equals(type.Name, controlTypeName, StringComparison.Ordinal)))
                .Distinct()
                .ToList();

            if (matches.Count == 1)
            {
                return matches[0];
            }

            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"Multiple control types matched '{controlTypeName}': {string.Join(", ", matches.Select(x => x.FullName))}.");
            }

            throw new InvalidOperationException($"No control type named '{controlTypeName}' could be resolved for {nameof(ThemeControlTemplateDefinition)}.");
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(x => x != null);
            }
        }

        private static void ValidateControlType(Type controlType, string controlTypeName)
        {
            if (!typeof(MGElement).IsAssignableFrom(controlType))
            {
                throw new InvalidOperationException($"Resolved control type '{controlTypeName}' does not derive from {nameof(MGElement)}.");
            }
        }

        private static void ApplyFontSettings(MGTheme Theme, ThemeFontSettingsDefinition Definition, string DefaultFontFamily)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.ContextMenuFontSize.HasValue)
                Theme.FontSettings.ContextMenuFontSize = Definition.ContextMenuFontSize.Value;
            if (Definition.SmallFontSize.HasValue)
                Theme.FontSettings.SmallFontSize = Definition.SmallFontSize.Value;
            if (Definition.MediumFontSize.HasValue)
                Theme.FontSettings.MediumFontSize = Definition.MediumFontSize.Value;
            if (Definition.LargeFontSize.HasValue)
                Theme.FontSettings.LargeFontSize = Definition.LargeFontSize.Value;
            if (Definition.DefaultFontSize.HasValue)
                Theme.FontSettings.DefaultFontSize = Definition.DefaultFontSize.Value;
            if (Definition.UseExactScale.HasValue)
                Theme.FontSettings.UseExactScale = Definition.UseExactScale.Value;

            string FontFamily = Definition.DefaultFontFamily ?? DefaultFontFamily;
            if (!string.IsNullOrWhiteSpace(FontFamily))
            {
                Theme.FontSettings.DefaultFontFamily = FontFamily;
            }

            if (Definition.DefaultFontShadowOffset != null)
            {
                Theme.FontSettings.DefaultFontShadowOffset = new Point(Definition.DefaultFontShadowOffset.X, Definition.DefaultFontShadowOffset.Y);
            }

            if (Definition.DefaultFontShadowColor.HasValue)
            {
                Theme.FontSettings.DefaultFontShadowColor = Definition.DefaultFontShadowColor.Value.ToXNAColor();
            }
        }

        private static void ApplyBackgrounds(MGTheme Theme, IEnumerable<ThemeBackgroundDefinition> Definitions)
        {
            if (Definitions == null)
            {
                return;
            }

            foreach (ThemeBackgroundDefinition Definition in Definitions)
            {
                if (Definition?.Value == null)
                {
                    continue;
                }

                VisualStateFillBrush Current = Theme.GetBackgroundBrush(Definition.ElementType);
                Theme.SetBackgroundBrush(Definition.ElementType, ApplyVisualStateFillBrush(Definition.Value, Current));
            }
        }

        private static void ApplyWindow(MGThemeWindowSettings Target, ThemeWindowSettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.Padding.HasValue) Target.Padding = Definition.Padding.Value.ToThickness();
            if (Definition.BorderThickness.HasValue) Target.BorderThickness = Definition.BorderThickness.Value.ToThickness();
            if (Definition.BorderBrush != null) Target.BorderBrush = ToBorderBrush(Definition.BorderBrush);
            if (Definition.TitleBarPadding.HasValue) Target.TitleBarPadding = Definition.TitleBarPadding.Value.ToThickness();
            if (Definition.TitleBarMinHeight.HasValue) Target.TitleBarMinHeight = Definition.TitleBarMinHeight.Value;
            if (Definition.CloseButtonBackground != null) Target.CloseButtonBackground = ApplyVisualStateFillBrush(Definition.CloseButtonBackground, Target.CloseButtonBackground);
            if (Definition.CloseButtonBorderBrush != null) Target.CloseButtonBorderBrush = ToBorderBrush(Definition.CloseButtonBorderBrush);
            if (Definition.CloseButtonBorderThickness.HasValue) Target.CloseButtonBorderThickness = Definition.CloseButtonBorderThickness.Value.ToThickness();
            if (Definition.CloseButtonMargin.HasValue) Target.CloseButtonMargin = Definition.CloseButtonMargin.Value.ToThickness();
            if (Definition.CloseButtonPadding.HasValue) Target.CloseButtonPadding = Definition.CloseButtonPadding.Value.ToThickness();
            if (Definition.CloseButtonMinWidth.HasValue) Target.CloseButtonMinWidth = Definition.CloseButtonMinWidth.Value;
            if (Definition.CloseButtonMinHeight.HasValue) Target.CloseButtonMinHeight = Definition.CloseButtonMinHeight.Value;
            if (Definition.TitleTextMargin.HasValue) Target.TitleTextMargin = Definition.TitleTextMargin.Value.ToThickness();
            if (Definition.TitleTextPadding.HasValue) Target.TitleTextPadding = Definition.TitleTextPadding.Value.ToThickness();
            if (Definition.TitleTextForeground != null) Target.TitleTextForeground = ApplyColorSetting(Definition.TitleTextForeground, Target.TitleTextForeground);
        }

        private static void ApplyOverlay(MGThemeOverlaySettings Target, ThemeOverlaySettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.HostPadding.HasValue) Target.HostPadding = Definition.HostPadding.Value.ToThickness();
            if (Definition.Padding.HasValue) Target.Padding = Definition.Padding.Value.ToThickness();
            if (Definition.BorderThickness.HasValue) Target.BorderThickness = Definition.BorderThickness.Value.ToThickness();
            if (Definition.BorderBrush != null) Target.BorderBrush = ToBorderBrush(Definition.BorderBrush);
            if (Definition.CloseButtonBackground != null) Target.CloseButtonBackground = ApplyVisualStateFillBrush(Definition.CloseButtonBackground, Target.CloseButtonBackground);
            if (Definition.CloseButtonBorderBrush != null) Target.CloseButtonBorderBrush = ToBorderBrush(Definition.CloseButtonBorderBrush);
            if (Definition.CloseButtonBorderThickness.HasValue) Target.CloseButtonBorderThickness = Definition.CloseButtonBorderThickness.Value.ToThickness();
            if (Definition.CloseButtonPadding.HasValue) Target.CloseButtonPadding = Definition.CloseButtonPadding.Value.ToThickness();
            if (Definition.CloseButtonMinWidth.HasValue) Target.CloseButtonMinWidth = Definition.CloseButtonMinWidth.Value;
            if (Definition.CloseButtonMinHeight.HasValue) Target.CloseButtonMinHeight = Definition.CloseButtonMinHeight.Value;
        }

        private static void ApplyContextMenu(MGThemeContextMenuSettings Target, ThemeContextMenuSettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.Padding.HasValue) Target.Padding = Definition.Padding.Value.ToThickness();
            if (Definition.BorderBrush != null) Target.BorderBrush = ToBorderBrush(Definition.BorderBrush);
            if (Definition.BorderThickness.HasValue) Target.BorderThickness = Definition.BorderThickness.Value.ToThickness();
        }

        private static void ApplyContextMenuItem(MGThemeContextMenuItemSettings Target, ThemeContextMenuItemSettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.HeaderMargin.HasValue) Target.HeaderMargin = Definition.HeaderMargin.Value.ToThickness();
            if (Definition.HeaderBackground != null) Target.HeaderBackground = ApplyVisualStateFillBrush(Definition.HeaderBackground, Target.HeaderBackground);
            if (Definition.ShortcutMargin.HasValue) Target.ShortcutMargin = Definition.ShortcutMargin.Value.ToThickness();
            if (Definition.ShortcutForeground != null) Target.ShortcutForeground = ApplyColorSetting(Definition.ShortcutForeground, Target.ShortcutForeground);
            if (Definition.SubmenuArrowMargin.HasValue) Target.SubmenuArrowMargin = Definition.SubmenuArrowMargin.Value.ToThickness();
        }

        private static void ApplyListBox(MGThemeListBoxSettings Target, ThemeListBoxSettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.OuterBackground != null) Target.OuterBackground = ApplyVisualStateFillBrush(Definition.OuterBackground, Target.OuterBackground);
            if (Definition.TitlePadding.HasValue) Target.TitlePadding = Definition.TitlePadding.Value.ToThickness();
            if (Definition.TitleForeground != null) Target.TitleForeground = ApplyColorSetting(Definition.TitleForeground, Target.TitleForeground);
            if (Definition.TitleBorderBrush != null) Target.TitleBorderBrush = ToBorderBrush(Definition.TitleBorderBrush);
            if (Definition.TitleBorderThickness.HasValue) Target.TitleBorderThickness = Definition.TitleBorderThickness.Value.ToThickness();
            if (Definition.InnerBorderBrush != null) Target.InnerBorderBrush = ToBorderBrush(Definition.InnerBorderBrush);
            if (Definition.InnerBorderThickness.HasValue) Target.InnerBorderThickness = Definition.InnerBorderThickness.Value.ToThickness();
            if (Definition.ScrollViewerPadding.HasValue) Target.ScrollViewerPadding = Definition.ScrollViewerPadding.Value.ToThickness();
            if (Definition.ItemsPanelBorderBrush != null) Target.ItemsPanelBorderBrush = ToBorderBrush(Definition.ItemsPanelBorderBrush);
            if (Definition.ItemsPanelBorderThickness.HasValue) Target.ItemsPanelBorderThickness = Definition.ItemsPanelBorderThickness.Value.ToThickness();
        }

        private static void ApplyListView(MGThemeListViewSettings Target, ThemeListViewSettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.HeaderForeground != null) Target.HeaderForeground = ApplyColorSetting(Definition.HeaderForeground, Target.HeaderForeground);
            if (Definition.GridLineBrush != null) Target.GridLineBrush = ToFillBrush(Definition.GridLineBrush);
        }

        private static void ApplyComboBox(MGThemeComboBoxSettings Target, ThemeComboBoxSettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.Padding.HasValue) Target.Padding = Definition.Padding.Value.ToThickness();
            if (Definition.MinHeight.HasValue) Target.MinHeight = Definition.MinHeight.Value;
            if (Definition.BorderBrush != null) Target.BorderBrush = ToBorderBrush(Definition.BorderBrush);
            if (Definition.DropdownArrowMargin.HasValue) Target.DropdownArrowMargin = Definition.DropdownArrowMargin.Value.ToThickness();
            if (Definition.DropdownMinWidth.HasValue) Target.DropdownMinWidth = Definition.DropdownMinWidth.Value;
            if (Definition.DropdownBorderThickness.HasValue) Target.DropdownBorderThickness = Definition.DropdownBorderThickness.Value.ToThickness();
            if (Definition.DropdownBorderBrush != null) Target.DropdownBorderBrush = ToBorderBrush(Definition.DropdownBorderBrush);
            if (Definition.DropdownPadding.HasValue) Target.DropdownPadding = Definition.DropdownPadding.Value.ToThickness();
            if (Definition.DropdownScrollViewerPadding.HasValue) Target.DropdownScrollViewerPadding = Definition.DropdownScrollViewerPadding.Value.ToThickness();
            if (Definition.DropdownItemsSpacing.HasValue) Target.DropdownItemsSpacing = Definition.DropdownItemsSpacing.Value;
        }

        private static void ApplyTreeViewTemplate(MGThemeTreeViewTemplateSettings Target, ThemeTreeViewTemplateSettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.ScrollViewerPadding.HasValue) Target.ScrollViewerPadding = Definition.ScrollViewerPadding.Value.ToThickness();
            if (Definition.ItemsPanelPadding.HasValue) Target.ItemsPanelPadding = Definition.ItemsPanelPadding.Value.ToThickness();
            if (Definition.ItemsPanelSpacing.HasValue) Target.ItemsPanelSpacing = Definition.ItemsPanelSpacing.Value;
        }

        private static void ApplyTabControl(MGThemeTabControlSettings Target, ThemeTabControlSettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.Padding.HasValue) Target.Padding = Definition.Padding.Value.ToThickness();
            if (Definition.BorderBrush != null) Target.BorderBrush = ToBorderBrush(Definition.BorderBrush);
            if (Definition.BorderThickness.HasValue) Target.BorderThickness = Definition.BorderThickness.Value.ToThickness();
            if (Definition.HeadersSpacing.HasValue) Target.HeadersSpacing = Definition.HeadersSpacing.Value;
        }

        private static void ApplyDocking(MGThemeDockingSettings Target, ThemeDockingSettingsDefinition Definition)
        {
            if (Definition == null)
            {
                return;
            }

            if (Definition.TabNormalBackground != null) Target.TabNormalBackground = ToFillBrush(Definition.TabNormalBackground);
            if (Definition.TabHoverBackground != null) Target.TabHoverBackground = ToFillBrush(Definition.TabHoverBackground);
            if (Definition.TabActiveBackground != null) Target.TabActiveBackground = ToFillBrush(Definition.TabActiveBackground);
            if (Definition.TabActiveAccentColor.HasValue) Target.TabActiveAccentColor = Definition.TabActiveAccentColor.Value.ToXNAColor();
            if (Definition.TabHoverAccentColor.HasValue) Target.TabHoverAccentColor = Definition.TabHoverAccentColor.Value.ToXNAColor();
            if (Definition.TabActiveTextColor.HasValue) Target.TabActiveTextColor = Definition.TabActiveTextColor.Value.ToXNAColor();
            if (Definition.TabInactiveTextColor.HasValue) Target.TabInactiveTextColor = Definition.TabInactiveTextColor.Value.ToXNAColor();
            if (Definition.TabActiveIconColor.HasValue) Target.TabActiveIconColor = Definition.TabActiveIconColor.Value.ToXNAColor();
            if (Definition.TabInactiveIconColor.HasValue) Target.TabInactiveIconColor = Definition.TabInactiveIconColor.Value.ToXNAColor();

            if (Definition.AutoHideDrawerBackground != null) Target.AutoHideDrawerBackground = ToFillBrush(Definition.AutoHideDrawerBackground);
            if (Definition.AutoHideDrawerHeaderBackground != null) Target.AutoHideDrawerHeaderBackground = ToFillBrush(Definition.AutoHideDrawerHeaderBackground);
            if (Definition.AutoHideButtonBackground != null) Target.AutoHideButtonBackground = ApplyVisualStateFillBrush(Definition.AutoHideButtonBackground, Target.AutoHideButtonBackground);
            if (Definition.AutoHideHeaderTextColor.HasValue) Target.AutoHideHeaderTextColor = Definition.AutoHideHeaderTextColor.Value.ToXNAColor();
            if (Definition.AutoHideIconColor.HasValue) Target.AutoHideIconColor = Definition.AutoHideIconColor.Value.ToXNAColor();
            if (Definition.AutoHideBorderColor.HasValue) Target.AutoHideBorderColor = Definition.AutoHideBorderColor.Value.ToXNAColor();
            if (Definition.AutoHideGripColor.HasValue) Target.AutoHideGripColor = Definition.AutoHideGripColor.Value.ToXNAColor();

            if (Definition.AutoHideStripBackground != null) Target.AutoHideStripBackground = ToFillBrush(Definition.AutoHideStripBackground);
            if (Definition.AutoHideStripButtonBackground != null) Target.AutoHideStripButtonBackground = ApplyVisualStateFillBrush(Definition.AutoHideStripButtonBackground, Target.AutoHideStripButtonBackground);
            if (Definition.AutoHideStripTextColor.HasValue) Target.AutoHideStripTextColor = Definition.AutoHideStripTextColor.Value.ToXNAColor();
            if (Definition.AutoHideStripSeparatorColor.HasValue) Target.AutoHideStripSeparatorColor = Definition.AutoHideStripSeparatorColor.Value.ToXNAColor();

            if (Definition.SplitterNormalBrush != null) Target.SplitterNormalBrush = ToFillBrush(Definition.SplitterNormalBrush);
            if (Definition.SplitterHoverBrush != null) Target.SplitterHoverBrush = ToFillBrush(Definition.SplitterHoverBrush);
            if (Definition.SplitterPressedBrush != null) Target.SplitterPressedBrush = ToFillBrush(Definition.SplitterPressedBrush);
            if (Definition.SplitterHoverOverlayColor.HasValue) Target.SplitterHoverOverlayColor = Definition.SplitterHoverOverlayColor.Value.ToXNAColor();
            if (Definition.SplitterPressedOverlayColor.HasValue) Target.SplitterPressedOverlayColor = Definition.SplitterPressedOverlayColor.Value.ToXNAColor();

            if (Definition.DropIndicatorInactiveColor.HasValue) Target.DropIndicatorInactiveColor = Definition.DropIndicatorInactiveColor.Value.ToXNAColor();
            if (Definition.DropIndicatorActiveColor.HasValue) Target.DropIndicatorActiveColor = Definition.DropIndicatorActiveColor.Value.ToXNAColor();
            if (Definition.DropIndicatorBorderColor.HasValue) Target.DropIndicatorBorderColor = Definition.DropIndicatorBorderColor.Value.ToXNAColor();
            if (Definition.DropIndicatorHostInactiveColor.HasValue) Target.DropIndicatorHostInactiveColor = Definition.DropIndicatorHostInactiveColor.Value.ToXNAColor();
            if (Definition.DropIndicatorHostActiveColor.HasValue) Target.DropIndicatorHostActiveColor = Definition.DropIndicatorHostActiveColor.Value.ToXNAColor();
            if (Definition.DropIndicatorDisabledColor.HasValue) Target.DropIndicatorDisabledColor = Definition.DropIndicatorDisabledColor.Value.ToXNAColor();
            if (Definition.DropIndicatorDisabledBorderColor.HasValue) Target.DropIndicatorDisabledBorderColor = Definition.DropIndicatorDisabledBorderColor.Value.ToXNAColor();
            if (Definition.DropIndicatorSymbolColor.HasValue) Target.DropIndicatorSymbolColor = Definition.DropIndicatorSymbolColor.Value.ToXNAColor();
            if (Definition.DropIndicatorDisabledSymbolColor.HasValue) Target.DropIndicatorDisabledSymbolColor = Definition.DropIndicatorDisabledSymbolColor.Value.ToXNAColor();
        }

        private static void ApplyProperties(MGTheme Theme, IEnumerable<ThemePropertyDefinition> Definitions)
        {
            if (Definitions == null)
            {
                return;
            }

            foreach (ThemePropertyDefinition Definition in Definitions)
            {
                if (Definition == null)
                {
                    continue;
                }

                switch (Definition.Target)
                {
                    case ThemePropertyTarget.ComboBoxDropdownBackground:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.ComboBoxDropdownBackground.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.ComboBoxDropdownBackground.GetValue(true));
                        break;
                    case ThemePropertyTarget.ComboBoxDropdownItemBackground:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.ComboBoxDropdownItemBackground.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.ComboBoxDropdownItemBackground.GetValue(true));
                        break;
                    case ThemePropertyTarget.CheckMarkColor:
                        if (Definition.Color.HasValue)
                            Theme.CheckMarkColor = Definition.Color.Value.ToXNAColor();
                        break;
                    case ThemePropertyTarget.DropdownArrowColor:
                        if (Definition.Color.HasValue)
                            Theme.DropdownArrowColor = Definition.Color.Value.ToXNAColor();
                        break;
                    case ThemePropertyTarget.GridSplitterForeground:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.GridSplitterForeground.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.GridSplitterForeground.GetValue(true));
                        break;
                    case ThemePropertyTarget.ListBoxItemBackground:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.ListBoxItemBackground.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.ListBoxItemBackground.GetValue(true));
                        break;
                    case ThemePropertyTarget.ListBoxItemAlternatingRowBackgrounds:
                        if (Definition.FillBrushes?.Count > 0)
                        {
                            Theme.ListBoxItemAlternatingRowBackgrounds.Clear();
                            foreach (FillBrush Brush in Definition.FillBrushes)
                            {
                                Theme.ListBoxItemAlternatingRowBackgrounds.Add(new ThemeManagedFillBrush(ToFillBrush(Brush)));
                            }
                        }
                        break;
                    case ThemePropertyTarget.TreeViewSelectionBackground:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.TreeViewSelectionBackground.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.TreeViewSelectionBackground.GetValue(true));
                        break;
                    case ThemePropertyTarget.TreeViewSelectionForeground:
                        if (Definition.Color.HasValue)
                            Theme.TreeViewSelectionForeground = Definition.Color.Value.ToXNAColor();
                        break;
                    case ThemePropertyTarget.TreeViewExpanderArrowColor:
                        if (Definition.Color.HasValue)
                            Theme.TreeViewExpanderArrowColor = Definition.Color.Value.ToXNAColor();
                        break;
                    case ThemePropertyTarget.TreeViewBorderBrush:
                        if (Definition.BorderBrush != null)
                            Theme.TreeViewBorderBrush = ToBorderBrush(Definition.BorderBrush);
                        break;
                    case ThemePropertyTarget.TreeViewBorderThickness:
                        if (Definition.Thickness.HasValue)
                            Theme.TreeViewBorderThickness = Definition.Thickness.Value.ToThickness();
                        break;
                    case ThemePropertyTarget.TreeViewIndentSize:
                        if (Definition.Integer.HasValue)
                            Theme.TreeViewIndentSize = Definition.Integer.Value;
                        break;
                    case ThemePropertyTarget.TreeViewExpanderButtonSize:
                        if (Definition.Integer.HasValue)
                            Theme.TreeViewExpanderButtonSize = Definition.Integer.Value;
                        break;
                    case ThemePropertyTarget.ProgressButtonForeground:
                        if (Definition.FillBrush != null)
                            Theme.ProgressButtonForeground.Value = ToFillBrush(Definition.FillBrush);
                        break;
                    case ThemePropertyTarget.ProgressBarCompletedBrush:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.ProgressBarCompletedBrush.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.ProgressBarCompletedBrush.GetValue(true));
                        break;
                    case ThemePropertyTarget.ProgressBarIncompleteBrush:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.ProgressBarIncompleteBrush.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.ProgressBarIncompleteBrush.GetValue(true));
                        break;
                    case ThemePropertyTarget.RadioButtonBubbleBackground:
                        if (Definition.VisualStateColorBrush != null)
                            Theme.RadioButtonBubbleBackground = new ThemeManagedVisualStateColorBrush(ApplyVisualStateColorBrush(Definition.VisualStateColorBrush, Theme.RadioButtonBubbleBackground?.GetValue(true)));
                        break;
                    case ThemePropertyTarget.RadioButtonCheckedFillColor:
                        if (Definition.Color.HasValue)
                            Theme.RadioButtonCheckedFillColor = Definition.Color.Value.ToXNAColor();
                        break;
                    case ThemePropertyTarget.ResizeGripForeground:
                        if (Definition.VisualStateColorBrush != null)
                            Theme.ResizeGripForeground.Value = ApplyVisualStateColorBrush(Definition.VisualStateColorBrush, Theme.ResizeGripForeground.GetValue(true));
                        break;
                    case ThemePropertyTarget.ScrollBarOuterBrush:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.ScrollBarOuterBrush.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.ScrollBarOuterBrush.GetValue(true));
                        break;
                    case ThemePropertyTarget.ScrollBarInnerBrush:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.ScrollBarInnerBrush.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.ScrollBarInnerBrush.GetValue(true));
                        break;
                    case ThemePropertyTarget.SliderForeground:
                        if (Definition.FillBrush != null)
                            Theme.SliderForeground.Value = ToFillBrush(Definition.FillBrush);
                        break;
                    case ThemePropertyTarget.SliderThumbFillBrush:
                        if (Definition.FillBrush != null)
                            Theme.SliderThumbFillBrush.Value = ToFillBrush(Definition.FillBrush);
                        break;
                    case ThemePropertyTarget.SliderOverlay:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.SliderOverlay.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.SliderOverlay.GetValue(true));
                        break;
                    case ThemePropertyTarget.SpoilerUnspoiledBackground:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.SpoilerUnspoiledBackground.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.SpoilerUnspoiledBackground.GetValue(true));
                        break;
                    case ThemePropertyTarget.SelectedTabHeaderBackground:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.SelectedTabHeaderBackground.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.SelectedTabHeaderBackground.GetValue(true));
                        break;
                    case ThemePropertyTarget.UnselectedTabHeaderBackground:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.UnselectedTabHeaderBackground.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.UnselectedTabHeaderBackground.GetValue(true));
                        break;
                    case ThemePropertyTarget.TextBoxFocusedSelectionForeground:
                        if (Definition.Color.HasValue)
                            Theme.TextBoxFocusedSelectionForeground = Definition.Color.Value.ToXNAColor();
                        break;
                    case ThemePropertyTarget.TextBoxFocusedSelectionBackground:
                        if (Definition.Color.HasValue)
                            Theme.TextBoxFocusedSelectionBackground = Definition.Color.Value.ToXNAColor();
                        break;
                    case ThemePropertyTarget.TextBoxUnfocusedSelectionForeground:
                        if (Definition.Color.HasValue)
                            Theme.TextBoxUnfocusedSelectionForeground = Definition.Color.Value.ToXNAColor();
                        break;
                    case ThemePropertyTarget.TextBoxUnfocusedSelectionBackground:
                        if (Definition.Color.HasValue)
                            Theme.TextBoxUnfocusedSelectionBackground = Definition.Color.Value.ToXNAColor();
                        break;
                    case ThemePropertyTarget.TitleBackground:
                        if (Definition.VisualStateFillBrush != null)
                            Theme.TitleBackground.Value = ApplyVisualStateFillBrush(Definition.VisualStateFillBrush, Theme.TitleBackground.GetValue(true));
                        break;
                    case ThemePropertyTarget.TextBlockFallbackForeground:
                        if (Definition.VisualStateColorBrush != null)
                            Theme.TextBlockFallbackForeground.Value = ApplyVisualStateColorBrush(Definition.VisualStateColorBrush, Theme.TextBlockFallbackForeground.GetValue(true));
                        break;
                    case ThemePropertyTarget.ToolTipOffset:
                        if (Definition.Point != null)
                            Theme.ToolTipOffset = new Point(Definition.Point.X, Definition.Point.Y);
                        break;
                    case ThemePropertyTarget.ToolTipTextForeground:
                        if (Definition.VisualStateColorBrush != null)
                            Theme.ToolTipTextForeground = ApplyColorBrushSetting(Definition.VisualStateColorBrush, Theme.ToolTipTextForeground);
                        break;
                    default:
                        throw new NotImplementedException($"Unsupported {nameof(ThemePropertyTarget)}: {Definition.Target}");
                }
            }
        }

        private static VisualStateSetting<Color?> ApplyColorBrushSetting(ThemeVisualStateColorBrushDefinition Definition, VisualStateSetting<Color?> Current)
        {
            if (Definition == null)
            {
                return Current;
            }

            VisualStateSetting<Color?> Result = Current?.GetCopy() ?? new VisualStateSetting<Color?>(null, null, null, null);
            if (Current == null && Definition.NormalValue.HasValue)
            {
                Result.SetAll(Definition.NormalValue.Value.ToXNAColor());
            }
            if (Definition.NormalValue.HasValue) Result.NormalValue = Definition.NormalValue.Value.ToXNAColor();
            if (Definition.SelectedValue.HasValue) Result.SelectedValue = Definition.SelectedValue.Value.ToXNAColor();
            if (Definition.FocusedValue.HasValue) Result.FocusedValue = Definition.FocusedValue.Value.ToXNAColor();
            if (Definition.DisabledValue.HasValue) Result.DisabledValue = Definition.DisabledValue.Value.ToXNAColor();
            return Result;
        }

        private static VisualStateSetting<Color?> ApplyColorSetting(ThemeVisualStateColorSettingDefinition Definition, VisualStateSetting<Color?> Current)
        {
            VisualStateSetting<Color?> Result = Current?.GetCopy() ?? new VisualStateSetting<Color?>(null, null, null, null);
            if (Current == null && Definition.NormalValue.HasValue)
            {
                Result.SetAll(Definition.NormalValue.Value.ToXNAColor());
            }
            if (Definition.NormalValue.HasValue) Result.NormalValue = Definition.NormalValue.Value.ToXNAColor();
            if (Definition.SelectedValue.HasValue) Result.SelectedValue = Definition.SelectedValue.Value.ToXNAColor();
            if (Definition.FocusedValue.HasValue) Result.FocusedValue = Definition.FocusedValue.Value.ToXNAColor();
            if (Definition.DisabledValue.HasValue) Result.DisabledValue = Definition.DisabledValue.Value.ToXNAColor();
            return Result;
        }

        private static VisualStateFillBrush ApplyVisualStateFillBrush(ThemeVisualStateFillBrushDefinition Definition, VisualStateFillBrush Current)
        {
            VisualStateFillBrush Result = Current?.Copy() ?? new VisualStateFillBrush((IFillBrush)null);
            if (Current == null && Definition.NormalValue != null)
            {
                IFillBrush NormalBrush = ToFillBrush(Definition.NormalValue);
                Result.SetAll(NormalBrush);
            }
            if (Definition.NormalValue != null) Result.NormalValue = ToFillBrush(Definition.NormalValue);
            if (Definition.SelectedValue != null) Result.SelectedValue = ToFillBrush(Definition.SelectedValue);
            if (Definition.FocusedValue != null) Result.FocusedValue = ToFillBrush(Definition.FocusedValue);
            if (Definition.DisabledValue != null) Result.DisabledValue = ToFillBrush(Definition.DisabledValue);
            if (Definition.FocusedColor.HasValue) Result.FocusedColor = Definition.FocusedColor.Value.ToXNAColor();
            if (Definition.PressedModifierType.HasValue) Result.PressedModifierType = Definition.PressedModifierType.Value;
            if (Definition.PressedModifier.HasValue) Result.PressedModifier = Definition.PressedModifier.Value;
            else if (Current == null) Result.PressedModifier = DefaultPressedModifier;
            if (!Definition.PressedModifierType.HasValue && Current == null) Result.PressedModifierType = DefaultPressedModifierType;
            return Result;
        }

        private static VisualStateColorBrush ApplyVisualStateColorBrush(ThemeVisualStateColorBrushDefinition Definition, VisualStateColorBrush Current)
        {
            VisualStateColorBrush Result = Current?.Copy() ?? new VisualStateColorBrush(default(Color));
            if (Current == null && Definition.NormalValue.HasValue)
            {
                Result.SetAll(Definition.NormalValue.Value.ToXNAColor());
            }
            if (Definition.NormalValue.HasValue) Result.NormalValue = Definition.NormalValue.Value.ToXNAColor();
            if (Definition.SelectedValue.HasValue) Result.SelectedValue = Definition.SelectedValue.Value.ToXNAColor();
            if (Definition.FocusedValue.HasValue) Result.FocusedValue = Definition.FocusedValue.Value.ToXNAColor();
            if (Definition.DisabledValue.HasValue) Result.DisabledValue = Definition.DisabledValue.Value.ToXNAColor();
            if (Definition.FocusedColor.HasValue) Result.FocusedColor = Definition.FocusedColor.Value.ToXNAColor();
            if (Definition.PressedModifierType.HasValue) Result.PressedModifierType = Definition.PressedModifierType.Value;
            if (Definition.PressedModifier.HasValue) Result.PressedModifier = Definition.PressedModifier.Value;
            else if (Current == null) Result.PressedModifier = DefaultPressedModifier;
            if (!Definition.PressedModifierType.HasValue && Current == null) Result.PressedModifierType = DefaultPressedModifierType;
            return Result;
        }

        private static IFillBrush ToFillBrush(FillBrush Brush)
            => Brush?.ToFillBrush(null, null);

        private static IBorderBrush ToBorderBrush(BorderBrush Brush)
            => Brush?.ToBorderBrush(null, null);
    }
}