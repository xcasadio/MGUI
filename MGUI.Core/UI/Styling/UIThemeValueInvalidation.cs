using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MGUI.Core.UI.Styling
{
    /// <summary>Backlog task 7 (styling-theme-tasks.md): the explicit inventory of every value an <see cref="MGTheme"/> carries, classified by what a change
    /// of that value invalidates.<para/>
    /// <see cref="RenderOnly"/> values (brushes, colors, draw offsets, the text draw scale mode) only need a redraw. <see cref="LayoutAffecting"/> values (paddings,
    /// margins, border thicknesses, min sizes, spacings, indents, font sizes and families, wrapping and auto-width defaults) change measured sizes or arrangement.
    /// <see cref="Structural"/> values (control template mappings) can replace a control's visual structure. The classification is not inferred from the CLR
    /// type: <c>PropertyGrid.RowSeparatorBrush</c> is a brush, but its presence adds or removes the separator border of every row.<para/>
    /// Paths are the names of <see cref="MGTheme"/>'s properties (<c>"CheckBoxComponentSize"</c>) or, for a settings group, the group property then the
    /// setting (<c>"Window.CloseButtonMinWidth"</c>, <c>"FontSettings.DefaultFontSize"</c>); <see cref="BackgroundsPath"/> names the per element type
    /// backgrounds of <see cref="MGTheme.GetBackgroundBrush(MGElementType)"/>. <c>MGUI.Tests/Architecture/ThemeValueInvalidationInventoryTests.cs</c> keeps
    /// the inventory complete and checks that a value shaped like a resolved value store pilot (a thickness, a size, a brush or a color) carries the
    /// invalidation the store already attaches to that pilot.<para/>
    /// Consumers: the control template catalog stamps each value it applies with its invalidation
    /// (<see cref="MGControlTemplateContext.ApplyTemplateValue{T}(string, T, Func{T}, Action{T}, UIInvalidationKind, IEqualityComparer{T})"/>), and a control
    /// whose <see cref="MGElement.OnThemeChanged"/> reads a layout-affecting value overrides <see cref="MGElement.GetThemeInvalidation"/> with <see cref="ForChange{T}"/>.</summary>
    public static class UIThemeValueInvalidation
    {
        /// <summary>A change only needs a redraw.</summary>
        public const UIInvalidationKind RenderOnly = UIInvalidationKind.Draw;

        /// <summary>A change can alter measured sizes or arrangement: the invalidation the resolved value store attaches to the Margin, Padding,
        /// MinHeight and BorderThickness pilots.</summary>
        public const UIInvalidationKind LayoutAffecting = UIInvalidationKind.Measure | UIInvalidationKind.Arrange;

        /// <summary>A change can replace a control's visual structure, then its layout.</summary>
        public const UIInvalidationKind Structural = UIInvalidationKind.Structure | UIInvalidationKind.Measure | UIInvalidationKind.Arrange;

        /// <summary>Path of the per <see cref="MGElementType"/> backgrounds (<see cref="MGTheme.GetBackgroundBrush(MGElementType)"/>), the only theme values
        /// that no property exposes.</summary>
        public const string BackgroundsPath = "Backgrounds";

        private static readonly IReadOnlyDictionary<string, UIInvalidationKind> _Entries = new ReadOnlyDictionary<string, UIInvalidationKind>(
            new Dictionary<string, UIInvalidationKind>(StringComparer.Ordinal)
            {
                // MGTheme
                [BackgroundsPath] = RenderOnly,
                ["ControlTemplateMappings"] = Structural,
                ["ControlTemplateTypeMappings"] = Structural,
                ["ComboBoxDropdownBackground"] = RenderOnly,
                ["ComboBoxDropdownItemBackground"] = RenderOnly,
                ["CheckBoxComponentSize"] = LayoutAffecting,
                ["CheckMarkColor"] = RenderOnly,
                ["CheckBoxCheckedIndicatorStyle"] = RenderOnly,
                ["DropdownArrowColor"] = RenderOnly,
                ["GridSplitterForeground"] = RenderOnly,
                ["ListBoxItemBackground"] = RenderOnly,
                ["ListBoxItemAlternatingRowBackgrounds"] = RenderOnly,
                ["TreeViewSelectionBackground"] = RenderOnly,
                ["TreeViewSelectionForeground"] = RenderOnly,
                ["TreeViewExpanderArrowColor"] = RenderOnly,
                ["TreeViewBorderBrush"] = RenderOnly,
                ["TreeViewBorderThickness"] = LayoutAffecting,
                ["TreeViewIndentSize"] = LayoutAffecting,
                ["TreeViewExpanderButtonSize"] = LayoutAffecting, // not read by any control yet
                ["ProgressButtonForeground"] = RenderOnly,
                ["ProgressBarCompletedBrush"] = RenderOnly,
                ["ProgressBarIncompleteBrush"] = RenderOnly,
                ["RadioButtonBubbleBackground"] = RenderOnly,
                ["RadioButtonCheckedFillColor"] = RenderOnly,
                ["ResizeGripForeground"] = RenderOnly,
                ["ScrollBarOuterBrush"] = RenderOnly,
                ["ScrollBarInnerBrush"] = RenderOnly,
                ["SliderForeground"] = RenderOnly,
                ["SliderThumbFillBrush"] = RenderOnly,
                ["SliderOverlay"] = RenderOnly,
                ["SpoilerUnspoiledBackground"] = RenderOnly,
                ["SelectedTabHeaderBackground"] = RenderOnly,
                ["UnselectedTabHeaderBackground"] = RenderOnly,
                ["TextBoxFocusedSelectionForeground"] = RenderOnly,
                ["TextBoxFocusedSelectionBackground"] = RenderOnly,
                ["TextBoxUnfocusedSelectionForeground"] = RenderOnly,
                ["TextBoxUnfocusedSelectionBackground"] = RenderOnly,
                ["TitleBackground"] = RenderOnly,
                ["TextBlockFallbackForeground"] = RenderOnly,
                ["DefaultTextBlockWrapText"] = LayoutAffecting,
                ["DefaultTextBlockAutoWidthFromContent"] = LayoutAffecting,
                ["DefaultButtonAutoWidthFromContent"] = LayoutAffecting,
                ["DefaultComboBoxAutoWidthFromContent"] = LayoutAffecting,
                ["ToolTipOffset"] = RenderOnly, // MGToolTip.DrawOffset: where the tooltip is drawn relative to the mouse
                ["ToolTipTextForeground"] = RenderOnly,

                // MGTheme.ToolTip, MGTheme.TextBox and MGTheme.NumericUpDown (backlog task 14)
                ["ToolTip.Padding"] = LayoutAffecting,
                ["ToolTip.BorderThickness"] = LayoutAffecting,
                ["ToolTip.BorderBrush"] = RenderOnly,
                ["ToolTip.MinWidth"] = LayoutAffecting,
                ["ToolTip.MinHeight"] = LayoutAffecting,
                ["TextBox.Padding"] = LayoutAffecting,
                ["TextBox.MinHeight"] = LayoutAffecting,
                ["NumericUpDown.Padding"] = LayoutAffecting,
                ["NumericUpDown.MinHeight"] = LayoutAffecting,
                ["NumericUpDown.SpinnerWidth"] = LayoutAffecting,
                ["NumericUpDown.SpinnerMinWidth"] = LayoutAffecting,

                // MGTheme.FontSettings
                ["FontSettings.ContextMenuFontSize"] = LayoutAffecting,
                ["FontSettings.SmallFontSize"] = LayoutAffecting,
                ["FontSettings.MediumFontSize"] = LayoutAffecting,
                ["FontSettings.LargeFontSize"] = LayoutAffecting,
                ["FontSettings.DefaultFontSize"] = LayoutAffecting,
                ["FontSettings.UseExactScale"] = RenderOnly, // the scale text is drawn with; text measurement does not read it
                ["FontSettings.DefaultFontFamily"] = LayoutAffecting,
                ["FontSettings.DefaultFontShadowOffset"] = RenderOnly, // a shadow does not affect a text block's layout bounds
                ["FontSettings.DefaultFontShadowColor"] = RenderOnly,

                // MGTheme.Window
                ["Window.Padding"] = LayoutAffecting,
                ["Window.BorderThickness"] = LayoutAffecting,
                ["Window.ChromelessPadding"] = LayoutAffecting,
                ["Window.ChromelessBorderThickness"] = LayoutAffecting,
                ["Window.BorderBrush"] = RenderOnly,
                ["Window.TitleBarPadding"] = LayoutAffecting,
                ["Window.TitleBarMinHeight"] = LayoutAffecting,
                ["Window.CloseButtonBackground"] = RenderOnly,
                ["Window.CloseButtonBorderBrush"] = RenderOnly,
                ["Window.CloseButtonBorderThickness"] = LayoutAffecting,
                ["Window.CloseButtonMargin"] = LayoutAffecting,
                ["Window.CloseButtonPadding"] = LayoutAffecting,
                ["Window.CloseButtonMinWidth"] = LayoutAffecting,
                ["Window.CloseButtonMinHeight"] = LayoutAffecting,
                ["Window.TitleTextMargin"] = LayoutAffecting,
                ["Window.TitleTextPadding"] = LayoutAffecting,
                ["Window.TitleTextForeground"] = RenderOnly,

                // MGTheme.Overlay
                ["Overlay.HostPadding"] = LayoutAffecting,
                ["Overlay.Padding"] = LayoutAffecting,
                ["Overlay.BorderThickness"] = LayoutAffecting,
                ["Overlay.BorderBrush"] = RenderOnly,
                ["Overlay.CloseButtonBackground"] = RenderOnly,
                ["Overlay.CloseButtonBorderBrush"] = RenderOnly,
                ["Overlay.CloseButtonBorderThickness"] = LayoutAffecting,
                ["Overlay.CloseButtonPadding"] = LayoutAffecting,
                ["Overlay.CloseButtonMinWidth"] = LayoutAffecting,
                ["Overlay.CloseButtonMinHeight"] = LayoutAffecting,

                // MGTheme.ContextMenu and MGTheme.ContextMenuItem
                ["ContextMenu.Padding"] = LayoutAffecting,
                ["ContextMenu.BorderBrush"] = RenderOnly,
                ["ContextMenu.BorderThickness"] = LayoutAffecting,
                ["ContextMenuItem.HeaderMargin"] = LayoutAffecting,
                ["ContextMenuItem.HeaderBackground"] = RenderOnly,
                ["ContextMenuItem.ShortcutMargin"] = LayoutAffecting,
                ["ContextMenuItem.ShortcutForeground"] = RenderOnly,
                ["ContextMenuItem.SubmenuArrowMargin"] = LayoutAffecting,

                // MGTheme.ListBox and MGTheme.ListView
                ["ListBox.MinHeight"] = LayoutAffecting,
                ["ListBox.OuterBackground"] = RenderOnly,
                ["ListBox.TitlePadding"] = LayoutAffecting,
                ["ListBox.TitleForeground"] = RenderOnly,
                ["ListBox.TitleBorderBrush"] = RenderOnly,
                ["ListBox.TitleBorderThickness"] = LayoutAffecting,
                ["ListBox.InnerBorderBrush"] = RenderOnly,
                ["ListBox.InnerBorderThickness"] = LayoutAffecting,
                ["ListBox.ScrollViewerPadding"] = LayoutAffecting,
                ["ListBox.ItemsPanelBorderBrush"] = RenderOnly,
                ["ListBox.ItemsPanelBorderThickness"] = LayoutAffecting,
                ["ListBox.ItemPadding"] = LayoutAffecting,
                ["ListBox.ItemContentPadding"] = LayoutAffecting,
                ["ListView.HeaderForeground"] = RenderOnly,
                ["ListView.GridLineBrush"] = RenderOnly,

                // MGTheme.PropertyGrid
                ["PropertyGrid.Padding"] = LayoutAffecting,
                ["PropertyGrid.BorderBrush"] = RenderOnly,
                ["PropertyGrid.BorderThickness"] = LayoutAffecting,
                ["PropertyGrid.ScrollViewerPadding"] = LayoutAffecting,
                ["PropertyGrid.CategoriesSpacing"] = LayoutAffecting,
                ["PropertyGrid.CategoryHeaderBackground"] = RenderOnly,
                ["PropertyGrid.CategoryHeaderForeground"] = RenderOnly,
                ["PropertyGrid.CategoryHeaderPadding"] = LayoutAffecting,
                ["PropertyGrid.CategoryHeaderMinHeight"] = LayoutAffecting,
                ["PropertyGrid.CategoryArrowColor"] = RenderOnly,
                ["PropertyGrid.RowPadding"] = LayoutAffecting,
                ["PropertyGrid.RowsSpacing"] = LayoutAffecting,
                ["PropertyGrid.RowSeparatorBrush"] = LayoutAffecting, // a brush, but its presence adds or removes the 1 px separator border of every row
                ["PropertyGrid.InvalidEditorBorderBrush"] = RenderOnly,

                // MGTheme.ComboBox
                ["ComboBox.Padding"] = LayoutAffecting,
                ["ComboBox.MinHeight"] = LayoutAffecting,
                ["ComboBox.BorderBrush"] = RenderOnly,
                ["ComboBox.DropdownArrowMargin"] = LayoutAffecting,
                ["ComboBox.DropdownMinWidth"] = LayoutAffecting,
                ["ComboBox.DropdownBorderThickness"] = LayoutAffecting,
                ["ComboBox.DropdownBorderBrush"] = RenderOnly,
                ["ComboBox.DropdownPadding"] = LayoutAffecting,
                ["ComboBox.DropdownScrollViewerPadding"] = LayoutAffecting,
                ["ComboBox.DropdownItemsSpacing"] = LayoutAffecting,
                ["ComboBox.DropdownItemPadding"] = LayoutAffecting,

                // MGTheme.TreeViewTemplate and MGTheme.TabControl
                ["TreeViewTemplate.ScrollViewerPadding"] = LayoutAffecting,
                ["TreeViewTemplate.ItemsPanelPadding"] = LayoutAffecting,
                ["TreeViewTemplate.ItemsPanelSpacing"] = LayoutAffecting,
                ["TabControl.Padding"] = LayoutAffecting,
                ["TabControl.BorderBrush"] = RenderOnly,
                ["TabControl.BorderThickness"] = LayoutAffecting,
                ["TabControl.HeadersSpacing"] = LayoutAffecting,
                ["TabControl.SelectedHeaderPadding"] = LayoutAffecting,
                ["TabControl.UnselectedHeaderPadding"] = LayoutAffecting,
                ["TabControl.SideHeaderPadding"] = LayoutAffecting,

                // MGTheme.Graph
                ["Graph.Padding"] = LayoutAffecting,
                ["Graph.BorderBrush"] = RenderOnly,
                ["Graph.BorderThickness"] = LayoutAffecting,
                ["Graph.CanvasBackground"] = RenderOnly,
                ["Graph.GridLineBrush"] = RenderOnly,
                ["Graph.MajorGridLineBrush"] = RenderOnly,
                ["Graph.EdgeBrush"] = RenderOnly,
                ["Graph.SelectedEdgeBrush"] = RenderOnly,
                ["Graph.NodeBorderBrush"] = RenderOnly,
                ["Graph.NodeBorderThickness"] = LayoutAffecting,
                ["Graph.NodeSelectedBorderBrush"] = RenderOnly,
                ["Graph.NodeSelectedBorderThickness"] = LayoutAffecting,
                ["Graph.NodeHeaderBackground"] = RenderOnly,
                ["Graph.NodeHeaderForeground"] = RenderOnly,
                ["Graph.NodeBodyBackground"] = RenderOnly,
                ["Graph.PortBackground"] = RenderOnly,
                ["Graph.PortForeground"] = RenderOnly,
                ["Graph.CommentBackground"] = RenderOnly,
                ["Graph.CommentBorderBrush"] = RenderOnly,

                // MGTheme.Docking
                ["Docking.TabNormalBackground"] = RenderOnly,
                ["Docking.TabHoverBackground"] = RenderOnly,
                ["Docking.TabActiveBackground"] = RenderOnly,
                ["Docking.TabActiveAccentColor"] = RenderOnly,
                ["Docking.TabHoverAccentColor"] = RenderOnly,
                ["Docking.TabActiveTextColor"] = RenderOnly,
                ["Docking.TabInactiveTextColor"] = RenderOnly,
                ["Docking.TabActiveIconColor"] = RenderOnly,
                ["Docking.TabInactiveIconColor"] = RenderOnly,
                ["Docking.AutoHideDrawerBackground"] = RenderOnly,
                ["Docking.AutoHideDrawerHeaderBackground"] = RenderOnly,
                ["Docking.AutoHideButtonBackground"] = RenderOnly,
                ["Docking.AutoHideHeaderTextColor"] = RenderOnly,
                ["Docking.AutoHideIconColor"] = RenderOnly,
                ["Docking.AutoHideBorderColor"] = RenderOnly,
                ["Docking.AutoHideGripColor"] = RenderOnly,
                ["Docking.AutoHideStripBackground"] = RenderOnly,
                ["Docking.AutoHideStripButtonBackground"] = RenderOnly,
                ["Docking.AutoHideStripTextColor"] = RenderOnly,
                ["Docking.AutoHideStripSeparatorColor"] = RenderOnly,
                ["Docking.SplitterNormalBrush"] = RenderOnly,
                ["Docking.SplitterHoverBrush"] = RenderOnly,
                ["Docking.SplitterPressedBrush"] = RenderOnly,
                ["Docking.SplitterHoverOverlayColor"] = RenderOnly,
                ["Docking.SplitterPressedOverlayColor"] = RenderOnly,
                ["Docking.DropIndicatorInactiveColor"] = RenderOnly,
                ["Docking.DropIndicatorActiveColor"] = RenderOnly,
                ["Docking.DropIndicatorBorderColor"] = RenderOnly,
                ["Docking.DropIndicatorHostInactiveColor"] = RenderOnly,
                ["Docking.DropIndicatorHostActiveColor"] = RenderOnly,
                ["Docking.DropIndicatorDisabledColor"] = RenderOnly,
                ["Docking.DropIndicatorDisabledBorderColor"] = RenderOnly,
                ["Docking.DropIndicatorSymbolColor"] = RenderOnly,
                ["Docking.DropIndicatorDisabledSymbolColor"] = RenderOnly,
                ["Docking.PreviewOverlayFillColor"] = RenderOnly,
                ["Docking.PreviewOverlayBorderColor"] = RenderOnly,
                ["Docking.TabGroupButtonHoverColor"] = RenderOnly,
                ["Docking.TabGroupIconColor"] = RenderOnly,
                ["Docking.TabHeaderHeight"] = LayoutAffecting,
                ["Docking.TabButtonSize"] = LayoutAffecting,
                ["Docking.TabTitlePadding"] = LayoutAffecting,
                ["Docking.AutoHideDrawerHeaderHeight"] = LayoutAffecting,
                ["Docking.AutoHideDrawerButtonSize"] = LayoutAffecting,
                ["Docking.AutoHideStripThickness"] = LayoutAffecting,
                ["Docking.DropIndicatorZoneSize"] = LayoutAffecting,
            });

        /// <summary>Every classified theme value, keyed by path.</summary>
        public static IReadOnlyDictionary<string, UIInvalidationKind> Entries => _Entries;

        public static bool TryGetInvalidation(string themeValuePath, out UIInvalidationKind invalidation)
        {
            if (themeValuePath != null && _Entries.TryGetValue(themeValuePath, out invalidation))
            {
                return true;
            }

            invalidation = default;
            return false;
        }

        /// <exception cref="ArgumentException"><paramref name="themeValuePath"/> is not a classified theme value.</exception>
        public static UIInvalidationKind GetInvalidation(string themeValuePath)
            => TryGetInvalidation(themeValuePath, out UIInvalidationKind invalidation)
                ? invalidation
                : throw new ArgumentException($"'{themeValuePath}' is not a classified {nameof(MGTheme)} value.", nameof(themeValuePath));

        public static bool IsLayoutAffecting(string themeValuePath)
            => (GetInvalidation(themeValuePath) & LayoutAffecting) != 0;

        /// <summary>The invalidation needed when a theme change moves the value at <paramref name="themeValuePath"/> from <paramref name="previousValue"/>
        /// to <paramref name="currentValue"/>: <see cref="RenderOnly"/> when both are equal, otherwise <see cref="RenderOnly"/> combined with the classification
        /// of that value. Meant for <see cref="MGElement.GetThemeInvalidation"/> overrides, which compare what their theme callback reads.</summary>
        public static UIInvalidationKind ForChange<T>(string themeValuePath, T previousValue, T currentValue, IEqualityComparer<T> comparer = null)
        {
            UIInvalidationKind invalidation = GetInvalidation(themeValuePath);
            return (comparer ?? EqualityComparer<T>.Default).Equals(previousValue, currentValue)
                ? RenderOnly
                : RenderOnly | invalidation;
        }
    }
}
