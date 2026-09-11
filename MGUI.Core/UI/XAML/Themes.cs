using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Data_Binding;
using System;
using System.Collections.Generic;

#if UseWPF
using System.Windows.Markup;
#else
using Portable.Xaml.Markup;
#endif

namespace MGUI.Core.UI.XAML
{
    [ContentProperty(nameof(Themes))]
    public class ThemeDefinitionsDocument : XAMLBindableBase
    {
        public List<ThemeDefinition> Themes { get; set; } = new();
    }

    public class ThemeDefinition : XAMLBindableBase
    {
        public string Name { get; set; }
        public string BasedOn { get; set; }
        public bool IsBuiltIn { get; set; }

        public ThemeFontSettingsDefinition FontSettings { get; set; } = new();
        public ThemeWindowSettingsDefinition Window { get; set; } = new();
        public ThemeOverlaySettingsDefinition Overlay { get; set; } = new();
        public ThemeContextMenuSettingsDefinition ContextMenu { get; set; } = new();
        public ThemeContextMenuItemSettingsDefinition ContextMenuItem { get; set; } = new();
        public ThemeListBoxSettingsDefinition ListBox { get; set; } = new();
        public ThemeListViewSettingsDefinition ListView { get; set; } = new();
        public ThemePropertyGridSettingsDefinition PropertyGrid { get; set; } = new();
        public ThemeComboBoxSettingsDefinition ComboBox { get; set; } = new();
        public ThemeTreeViewTemplateSettingsDefinition TreeViewTemplate { get; set; } = new();
        public ThemeTabControlSettingsDefinition TabControl { get; set; } = new();
        public ThemeGraphSettingsDefinition Graph { get; set; } = new();
        public ThemeDockingSettingsDefinition Docking { get; set; } = new();

        public List<ThemeBackgroundDefinition> Backgrounds { get; set; } = new();
        public List<ThemeControlTemplateDefinition> ControlTemplates { get; set; } = new();
        public List<ThemePropertyDefinition> Properties { get; set; } = new();
    }

    public class ThemeControlTemplateDefinition : XAMLBindableBase
    {
        public MGElementType? ElementType { get; set; }
        public string ControlTypeName { get; set; }
        public string TemplateName { get; set; }
    }

    public class ThemeBackgroundDefinition : XAMLBindableBase
    {
        public MGElementType ElementType { get; set; }
        public ThemeVisualStateFillBrushDefinition Value { get; set; }
    }

    public enum ThemePropertyTarget
    {
        ComboBoxDropdownBackground,
        ComboBoxDropdownItemBackground,
        CheckBoxComponentSize,
        CheckMarkColor,
        CheckBoxCheckedIndicatorStyle,
        DropdownArrowColor,
        GridSplitterForeground,
        ListBoxItemBackground,
        ListBoxItemAlternatingRowBackgrounds,
        TreeViewSelectionBackground,
        TreeViewSelectionForeground,
        TreeViewExpanderArrowColor,
        TreeViewBorderBrush,
        TreeViewBorderThickness,
        TreeViewIndentSize,
        TreeViewExpanderButtonSize,
        ProgressButtonForeground,
        ProgressBarCompletedBrush,
        ProgressBarIncompleteBrush,
        RadioButtonBubbleBackground,
        RadioButtonCheckedFillColor,
        ResizeGripForeground,
        ScrollBarOuterBrush,
        ScrollBarInnerBrush,
        SliderForeground,
        SliderThumbFillBrush,
        SliderOverlay,
        SpoilerUnspoiledBackground,
        SelectedTabHeaderBackground,
        UnselectedTabHeaderBackground,
        TextBoxFocusedSelectionForeground,
        TextBoxFocusedSelectionBackground,
        TextBoxUnfocusedSelectionForeground,
        TextBoxUnfocusedSelectionBackground,
        TitleBackground,
        TextBlockFallbackForeground,
        DefaultTextBlockWrapText,
        DefaultTextBlockAutoWidthFromContent,
        DefaultButtonAutoWidthFromContent,
        DefaultComboBoxAutoWidthFromContent,
        ToolTipOffset,
        ToolTipTextForeground,
    }

    public class ThemePropertyDefinition : XAMLBindableBase
    {
        public ThemePropertyTarget Target { get; set; }

        public XAMLColor? Color { get; set; }
        public int? Integer { get; set; }
        public float? Float { get; set; }
        public bool? Boolean { get; set; }
        public string String { get; set; }
        public Thickness? Thickness { get; set; }
        public ThemePointDefinition Point { get; set; }
        public FillBrush FillBrush { get; set; }
        public BorderBrush BorderBrush { get; set; }
        public ThemeVisualStateFillBrushDefinition VisualStateFillBrush { get; set; }
        public ThemeVisualStateColorBrushDefinition VisualStateColorBrush { get; set; }
        public List<FillBrush> FillBrushes { get; set; } = new();
    }

    public class ThemePointDefinition : XAMLBindableBase
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class ThemeVisualStateColorSettingDefinition : XAMLBindableBase
    {
        public XAMLColor? NormalValue { get; set; }
        public XAMLColor? SelectedValue { get; set; }
        public XAMLColor? FocusedValue { get; set; }
        public XAMLColor? DisabledValue { get; set; }
    }

    public class ThemeVisualStateFillBrushDefinition : XAMLBindableBase
    {
        public FillBrush NormalValue { get; set; }
        public FillBrush SelectedValue { get; set; }
        public FillBrush FocusedValue { get; set; }
        public FillBrush DisabledValue { get; set; }
        public XAMLColor? FocusedColor { get; set; }
        public PressedModifierType? PressedModifierType { get; set; }
        public float? PressedModifier { get; set; }
    }

    public class ThemeVisualStateColorBrushDefinition : XAMLBindableBase
    {
        public XAMLColor? NormalValue { get; set; }
        public XAMLColor? SelectedValue { get; set; }
        public XAMLColor? FocusedValue { get; set; }
        public XAMLColor? DisabledValue { get; set; }
        public XAMLColor? FocusedColor { get; set; }
        public PressedModifierType? PressedModifierType { get; set; }
        public float? PressedModifier { get; set; }
    }

    public class ThemeFontSettingsDefinition : XAMLBindableBase
    {
        public int? ContextMenuFontSize { get; set; }
        public int? SmallFontSize { get; set; }
        public int? MediumFontSize { get; set; }
        public int? LargeFontSize { get; set; }
        public int? DefaultFontSize { get; set; }
        public bool? UseExactScale { get; set; }
        public string DefaultFontFamily { get; set; }
        public ThemePointDefinition DefaultFontShadowOffset { get; set; }
        public XAMLColor? DefaultFontShadowColor { get; set; }
    }

    public class ThemeWindowSettingsDefinition : XAMLBindableBase
    {
        public Thickness? Padding { get; set; }
        public Thickness? BorderThickness { get; set; }
        public Thickness? ChromelessPadding { get; set; }
        public Thickness? ChromelessBorderThickness { get; set; }
        public BorderBrush BorderBrush { get; set; }
        public Thickness? TitleBarPadding { get; set; }
        public int? TitleBarMinHeight { get; set; }
        public ThemeVisualStateFillBrushDefinition CloseButtonBackground { get; set; }
        public BorderBrush CloseButtonBorderBrush { get; set; }
        public Thickness? CloseButtonBorderThickness { get; set; }
        public Thickness? CloseButtonMargin { get; set; }
        public Thickness? CloseButtonPadding { get; set; }
        public int? CloseButtonMinWidth { get; set; }
        public int? CloseButtonMinHeight { get; set; }
        public Thickness? TitleTextMargin { get; set; }
        public Thickness? TitleTextPadding { get; set; }
        public ThemeVisualStateColorSettingDefinition TitleTextForeground { get; set; }
    }

    public class ThemeOverlaySettingsDefinition : XAMLBindableBase
    {
        public Thickness? HostPadding { get; set; }
        public Thickness? Padding { get; set; }
        public Thickness? BorderThickness { get; set; }
        public BorderBrush BorderBrush { get; set; }
        public ThemeVisualStateFillBrushDefinition CloseButtonBackground { get; set; }
        public BorderBrush CloseButtonBorderBrush { get; set; }
        public Thickness? CloseButtonBorderThickness { get; set; }
        public Thickness? CloseButtonPadding { get; set; }
        public int? CloseButtonMinWidth { get; set; }
        public int? CloseButtonMinHeight { get; set; }
    }

    public class ThemeContextMenuSettingsDefinition : XAMLBindableBase
    {
        public Thickness? Padding { get; set; }
        public BorderBrush BorderBrush { get; set; }
        public Thickness? BorderThickness { get; set; }
    }

    public class ThemeContextMenuItemSettingsDefinition : XAMLBindableBase
    {
        public Thickness? HeaderMargin { get; set; }
        public ThemeVisualStateFillBrushDefinition HeaderBackground { get; set; }
        public Thickness? ShortcutMargin { get; set; }
        public ThemeVisualStateColorSettingDefinition ShortcutForeground { get; set; }
        public Thickness? SubmenuArrowMargin { get; set; }
    }

    public class ThemeListBoxSettingsDefinition : XAMLBindableBase
    {
        public int? MinHeight { get; set; }
        public ThemeVisualStateFillBrushDefinition OuterBackground { get; set; }
        public Thickness? TitlePadding { get; set; }
        public ThemeVisualStateColorSettingDefinition TitleForeground { get; set; }
        public BorderBrush TitleBorderBrush { get; set; }
        public Thickness? TitleBorderThickness { get; set; }
        public BorderBrush InnerBorderBrush { get; set; }
        public Thickness? InnerBorderThickness { get; set; }
        public Thickness? ScrollViewerPadding { get; set; }
        public BorderBrush ItemsPanelBorderBrush { get; set; }
        public Thickness? ItemsPanelBorderThickness { get; set; }
    }

    public class ThemeListViewSettingsDefinition : XAMLBindableBase
    {
        public ThemeVisualStateColorSettingDefinition HeaderForeground { get; set; }
        public FillBrush GridLineBrush { get; set; }
    }

    public class ThemePropertyGridSettingsDefinition : XAMLBindableBase
    {
        public Thickness? Padding { get; set; }
        public BorderBrush BorderBrush { get; set; }
        public Thickness? BorderThickness { get; set; }
        public Thickness? ScrollViewerPadding { get; set; }
        public int? CategoriesSpacing { get; set; }
        public ThemeVisualStateFillBrushDefinition CategoryHeaderBackground { get; set; }
        public ThemeVisualStateColorBrushDefinition CategoryHeaderForeground { get; set; }
        public Thickness? CategoryHeaderPadding { get; set; }
        public int? CategoryHeaderMinHeight { get; set; }
        public XAMLColor? CategoryArrowColor { get; set; }
        public Thickness? RowPadding { get; set; }
        public int? RowsSpacing { get; set; }
        public FillBrush RowSeparatorBrush { get; set; }
        public BorderBrush InvalidEditorBorderBrush { get; set; }
    }

    public class ThemeGraphSettingsDefinition : XAMLBindableBase
    {
        public Thickness? Padding { get; set; }
        public BorderBrush BorderBrush { get; set; }
        public Thickness? BorderThickness { get; set; }
        public ThemeVisualStateFillBrushDefinition CanvasBackground { get; set; }
        public FillBrush GridLineBrush { get; set; }
        public FillBrush EdgeBrush { get; set; }
        public FillBrush SelectedEdgeBrush { get; set; }
        public BorderBrush NodeBorderBrush { get; set; }
        public Thickness? NodeBorderThickness { get; set; }
        public BorderBrush NodeSelectedBorderBrush { get; set; }
        public Thickness? NodeSelectedBorderThickness { get; set; }
        public ThemeVisualStateFillBrushDefinition NodeHeaderBackground { get; set; }
        public ThemeVisualStateColorBrushDefinition NodeHeaderForeground { get; set; }
        public ThemeVisualStateFillBrushDefinition NodeBodyBackground { get; set; }
        public FillBrush PortBackground { get; set; }
        public ThemeVisualStateColorBrushDefinition PortForeground { get; set; }
        public ThemeVisualStateFillBrushDefinition CommentBackground { get; set; }
        public BorderBrush CommentBorderBrush { get; set; }
    }

    public class ThemeComboBoxSettingsDefinition : XAMLBindableBase
    {
        public Thickness? Padding { get; set; }
        public int? MinHeight { get; set; }
        public BorderBrush BorderBrush { get; set; }
        public Thickness? DropdownArrowMargin { get; set; }
        public int? DropdownMinWidth { get; set; }
        public Thickness? DropdownBorderThickness { get; set; }
        public BorderBrush DropdownBorderBrush { get; set; }
        public Thickness? DropdownPadding { get; set; }
        public Thickness? DropdownScrollViewerPadding { get; set; }
        public int? DropdownItemsSpacing { get; set; }
    }

    public class ThemeTreeViewTemplateSettingsDefinition : XAMLBindableBase
    {
        public Thickness? ScrollViewerPadding { get; set; }
        public Thickness? ItemsPanelPadding { get; set; }
        public int? ItemsPanelSpacing { get; set; }
    }

    public class ThemeTabControlSettingsDefinition : XAMLBindableBase
    {
        public Thickness? Padding { get; set; }
        public BorderBrush BorderBrush { get; set; }
        public Thickness? BorderThickness { get; set; }
        public int? HeadersSpacing { get; set; }
    }

    public class ThemeDockingSettingsDefinition : XAMLBindableBase
    {
        public FillBrush TabNormalBackground { get; set; }
        public FillBrush TabHoverBackground { get; set; }
        public FillBrush TabActiveBackground { get; set; }
        public XAMLColor? TabActiveAccentColor { get; set; }
        public XAMLColor? TabHoverAccentColor { get; set; }
        public XAMLColor? TabActiveTextColor { get; set; }
        public XAMLColor? TabInactiveTextColor { get; set; }
        public XAMLColor? TabActiveIconColor { get; set; }
        public XAMLColor? TabInactiveIconColor { get; set; }

        public FillBrush AutoHideDrawerBackground { get; set; }
        public FillBrush AutoHideDrawerHeaderBackground { get; set; }
        public ThemeVisualStateFillBrushDefinition AutoHideButtonBackground { get; set; }
        public XAMLColor? AutoHideHeaderTextColor { get; set; }
        public XAMLColor? AutoHideIconColor { get; set; }
        public XAMLColor? AutoHideBorderColor { get; set; }
        public XAMLColor? AutoHideGripColor { get; set; }

        public FillBrush AutoHideStripBackground { get; set; }
        public ThemeVisualStateFillBrushDefinition AutoHideStripButtonBackground { get; set; }
        public XAMLColor? AutoHideStripTextColor { get; set; }
        public XAMLColor? AutoHideStripSeparatorColor { get; set; }

        public FillBrush SplitterNormalBrush { get; set; }
        public FillBrush SplitterHoverBrush { get; set; }
        public FillBrush SplitterPressedBrush { get; set; }
        public XAMLColor? SplitterHoverOverlayColor { get; set; }
        public XAMLColor? SplitterPressedOverlayColor { get; set; }

        public XAMLColor? DropIndicatorInactiveColor { get; set; }
        public XAMLColor? DropIndicatorActiveColor { get; set; }
        public XAMLColor? DropIndicatorBorderColor { get; set; }
        public XAMLColor? DropIndicatorHostInactiveColor { get; set; }
        public XAMLColor? DropIndicatorHostActiveColor { get; set; }
        public XAMLColor? DropIndicatorDisabledColor { get; set; }
        public XAMLColor? DropIndicatorDisabledBorderColor { get; set; }
        public XAMLColor? DropIndicatorSymbolColor { get; set; }
        public XAMLColor? DropIndicatorDisabledSymbolColor { get; set; }

        public XAMLColor? PreviewOverlayFillColor { get; set; }
        public XAMLColor? PreviewOverlayBorderColor { get; set; }

        public XAMLColor? TabGroupButtonHoverColor { get; set; }
        public XAMLColor? TabGroupIconColor { get; set; }
    }
}