using System;
using System.Linq;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Docking.Controls;

namespace MGUI.Core.UI.Styling
{
    public static class MGControlTemplateCatalog
    {
        public const string WindowTemplateName = "Window.Default";
        public const string OverlayTemplateName = "Overlay.Default";
        public const string ContextMenuTemplateName = "ContextMenu.Default";
        public const string ContextMenuItemTemplateName = "ContextMenuItem.Default";
        public const string ListBoxTemplateName = "ListBox.Default";
        public const string ListViewTemplateName = "ListView.Default";
        public const string ComboBoxTemplateName = "ComboBox.Default";
        public const string TreeViewTemplateName = "TreeView.Default";
        public const string TabControlTemplateName = "TabControl.Default";
        public const string DockTabItemTemplateName = "Dock.TabItem.Default";
        public const string DockAutoHideDrawerTemplateName = "Dock.AutoHideDrawer.Default";
        public const string DockAutoHideStripTemplateName = "Dock.AutoHideStrip.Default";
        public const string DockSplitterTemplateName = "Dock.Splitter.Default";
        public const string DockDropIndicatorsTemplateName = "Dock.DropIndicators.Default";

        public static void RegisterDefaults(MGResources Resources)
        {
            if (Resources == null)
            {
                return;
            }

            Register(Resources, WindowTemplateName, ApplyWindowTemplate);
            Register(Resources, OverlayTemplateName, ApplyOverlayTemplate);
            Register(Resources, ContextMenuTemplateName, ApplyContextMenuTemplate);
            Register(Resources, ContextMenuItemTemplateName, ApplyContextMenuItemTemplate);
            Register(Resources, ListBoxTemplateName, ApplyListBoxTemplate);
            Register(Resources, ListViewTemplateName, ApplyListViewTemplate);
            Register(Resources, ComboBoxTemplateName, ApplyComboBoxTemplate);
            Register(Resources, TreeViewTemplateName, ApplyTreeViewTemplate);
            Register(Resources, TabControlTemplateName, ApplyTabControlTemplate);
            Register(Resources, DockTabItemTemplateName, ApplyDockTabItemTemplate);
            Register(Resources, DockAutoHideDrawerTemplateName, ApplyDockAutoHideDrawerTemplate);
            Register(Resources, DockAutoHideStripTemplateName, ApplyDockAutoHideStripTemplate);
            Register(Resources, DockSplitterTemplateName, ApplyDockSplitterTemplate);
            Register(Resources, DockDropIndicatorsTemplateName, ApplyDockDropIndicatorsTemplate);
        }

        private static bool IsGenericControl(MGElement Owner, Type GenericDefinition)
            => Owner?.GetType().IsGenericType == true && Owner.GetType().GetGenericTypeDefinition() == GenericDefinition;

        private static void Register(MGResources Resources, string Name, Action<MGControlTemplateContext> Apply)
        {
            if (!Resources.TryGetControlTemplate(Name, out _))
            {
                Resources.AddControlTemplate(new(Name, Apply));
            }
        }

        private static void ApplyWindowTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGWindow Window)
            {
                return;
            }

            MGTheme Theme = Window.GetTheme();
            MGBorder Border = Context.GetRequiredPart<MGBorder>(MGWindow.BorderPartName);
            MGDockPanel TitleBar = Context.GetRequiredPart<MGDockPanel>(MGWindow.TitleBarPartName);
            MGTextBlock TitleText = Context.GetRequiredPart<MGTextBlock>(MGWindow.TitleBarTextPartName);
            MGButton CloseButton = Context.GetRequiredPart<MGButton>(MGWindow.CloseButtonPartName);

            Context.ApplyThemeDefault("Window.Padding", Theme.Window.Padding, () => Window.Padding, value => Window.Padding = value);
            Context.ApplyThemeDefault("Window.BorderThickness", Theme.Window.BorderThickness, () => Border.BorderThickness, value => Border.BorderThickness = value);
            Context.ApplyThemeDefault("Window.TitleBarPadding", Theme.Window.TitleBarPadding, () => TitleBar.Padding, value => TitleBar.Padding = value);
            Context.ApplyThemeDefault("Window.TitleBarMinHeight", Theme.Window.TitleBarMinHeight, () => TitleBar.MinHeight ?? 0, value => TitleBar.MinHeight = value);

            if (!Context.IsThemeRefresh)
            {
                Border.BorderBrush = MGUniformBorderBrush.Black;

                CloseButton.MinWidth = 12;
                CloseButton.MinHeight = 12;
                CloseButton.BackgroundBrush = new(Color.Crimson.AsFillBrush() * 0.5f, Color.White * 0.18f, PressedModifierType.Darken, 0.06f);
                CloseButton.BorderBrush = MGUniformBorderBrush.Black;
                CloseButton.BorderThickness = new(1);
                CloseButton.Margin = new(1, 1, 1, 1 + Border.BorderThickness.Bottom);
                CloseButton.Padding = new(4, -1);
                CloseButton.VerticalAlignment = VerticalAlignment.Center;
                CloseButton.VerticalContentAlignment = VerticalAlignment.Center;
                CloseButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                if (CloseButton.Content == null)
                {
                    CloseButton.SetContent(new MGTextBlock(Window, "[b][shadow=Black 1 1]x[/shadow][/b]", Color.White));
                }

                TitleText.Margin = new(4, 0);
                TitleText.Padding = new(0);
                TitleText.HorizontalAlignment = HorizontalAlignment.Stretch;
                TitleText.VerticalAlignment = VerticalAlignment.Center;
                TitleText.TextAlignment = HorizontalAlignment.Left;
                TitleText.DefaultTextForeground = new(Color.White, Color.White, Color.White);
            }

            TitleBar.BackgroundBrush = Theme.TitleBackground.GetValue(true);
            TitleBar.DrawBackgroundEnabled = false;
        }

        private static void ApplyOverlayTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGOverlay Overlay)
            {
                return;
            }

            MGBorder Border = Context.GetRequiredPart<MGBorder>(MGOverlay.BorderPartName);
            MGButton CloseButton = Context.GetRequiredPart<MGButton>(MGOverlay.CloseButtonPartName);

            if (!Context.IsThemeRefresh)
            {
                Overlay.Padding = new(5);
                Border.BorderThickness = new(1);
                Border.BorderBrush = MGUniformBorderBrush.Black;

                CloseButton.MinWidth = 12;
                CloseButton.MinHeight = 12;
                CloseButton.BackgroundBrush = new(Color.Crimson.AsFillBrush() * 0.8f, Color.White * 0.18f, PressedModifierType.Darken, 0.06f);
                CloseButton.BorderBrush = MGUniformBorderBrush.Black;
                CloseButton.BorderThickness = new(1);
                CloseButton.Padding = new(4, -1);
                if (CloseButton.Content == null)
                {
                    CloseButton.SetContent(new MGTextBlock(Overlay.Host.ParentWindow, "[b][shadow=Black 1 1]x[/shadow][/b]", Color.White));
                }
            }
        }

        private static void ApplyContextMenuTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGContextMenu Menu)
            {
                return;
            }

            if (!Context.IsThemeRefresh)
            {
                Menu.Padding = new(1);
                Menu.BorderBrush = MGUniformBorderBrush.Gray;
                Menu.BorderThickness = new(1);
            }
        }

        private static void ApplyContextMenuItemTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGWrappedContextMenuItem)
            {
                return;
            }

            MGContentPresenter HeaderPresenter = Context.GetRequiredPart<MGContentPresenter>(MGWrappedContextMenuItem.HeaderPresenterPartName);
            MGTextBlock ShortcutText = Context.GetRequiredPart<MGTextBlock>(MGWrappedContextMenuItem.ShortcutTextPartName);
            MGElement Arrow = Context.GetRequiredPart<MGElement>(MGWrappedContextMenuItem.SubmenuArrowPartName);

            if (!Context.IsThemeRefresh)
            {
                HeaderPresenter.Margin = new(0, 0, 5, 0);
                HeaderPresenter.BackgroundBrush = new(null);
                ShortcutText.Margin = new Thickness(18, 0, 0, 0);
                ShortcutText.Foreground = new(Color.LightGray, Color.LightGray, Color.LightGray);
                Arrow.Margin = new(0, 5, MGWrappedContextMenuItem.DefaultSubmenuArrowRightMargin, 5);
            }
        }

        private static void ApplyListBoxTemplate(MGControlTemplateContext Context)
        {
            if (!IsGenericControl(Context.Owner, typeof(MGListBox<>)))
            {
                return;
            }

            MGTheme Theme = Context.Owner.GetTheme();
            MGBorder OuterBorder = Context.GetRequiredPart<MGBorder>(MGListBox<object>.OuterBorderPartName);
            MGBorder InnerBorder = Context.GetRequiredPart<MGBorder>(MGListBox<object>.InnerBorderPartName);
            MGBorder TitleBorder = Context.GetRequiredPart<MGBorder>(MGListBox<object>.TitleBorderPartName);
            MGContentPresenter TitlePresenter = Context.GetRequiredPart<MGContentPresenter>(MGListBox<object>.TitlePresenterPartName);
            MGScrollViewer ScrollViewer = Context.GetRequiredPart<MGScrollViewer>(MGListBox<object>.ScrollViewerPartName);
            MGStackPanel ItemsPanel = Context.GetRequiredPart<MGStackPanel>(MGListBox<object>.ItemsPanelPartName);

            TitleBorder.BackgroundBrush = Theme.TitleBackground.GetValue(true);
            if (!Context.IsThemeRefresh)
            {
                OuterBorder.BackgroundBrush = new VisualStateFillBrush(SolidFillBrushes.Black);
                TitleBorder.Padding = new(6, 3);
                TitleBorder.DefaultTextForeground.SetAll(Color.White);
                TitleBorder.BorderBrush = SolidFillBrushes.Black.AsUniformBorderBrush();
                TitleBorder.BorderThickness = new(1, 1, 1, 0);
                InnerBorder.BorderBrush = SolidFillBrushes.Black.AsUniformBorderBrush();
                InnerBorder.BorderThickness = new(1);
                ScrollViewer.Padding = new(0);
                ItemsPanel.VerticalAlignment = VerticalAlignment.Top;
                ItemsPanel.BorderThickness = new(1);
                ItemsPanel.BorderBrush = SolidFillBrushes.Black.AsUniformBorderBrush();
                TitlePresenter.VerticalAlignment = VerticalAlignment.Center;
            }
        }

        private static void ApplyListViewTemplate(MGControlTemplateContext Context)
        {
            if (!IsGenericControl(Context.Owner, typeof(MGListView<>)))
            {
                return;
            }

            MGTheme Theme = Context.Owner.GetTheme();
            MGGrid HeaderGrid = Context.GetRequiredPart<MGGrid>(MGListView<object>.HeaderGridPartName);
            MGGrid DataGrid = Context.GetRequiredPart<MGGrid>(MGListView<object>.DataGridPartName);

            HeaderGrid.BackgroundBrush = Theme.TitleBackground.GetValue(true);
            if (!Context.IsThemeRefresh)
            {
                HeaderGrid.DefaultTextForeground.SetAll(Color.White);
                HeaderGrid.HorizontalGridLineBrush = SolidFillBrushes.Black;
                HeaderGrid.VerticalGridLineBrush = SolidFillBrushes.Black;
                DataGrid.HorizontalGridLineBrush = SolidFillBrushes.Black;
                DataGrid.VerticalGridLineBrush = SolidFillBrushes.Black;
            }
        }

        private static void ApplyComboBoxTemplate(MGControlTemplateContext Context)
        {
            if (!IsGenericControl(Context.Owner, typeof(MGComboBox<>)))
            {
                return;
            }

            MGTheme Theme = Context.Owner.GetTheme();
            MGBorder Border = Context.GetRequiredPart<MGBorder>(MGComboBox<object>.BorderPartName);
            MGContentPresenter DropdownArrow = Context.GetRequiredPart<MGContentPresenter>(MGComboBox<object>.DropdownArrowPartName);
            MGWindow Dropdown = Context.GetRequiredPart<MGWindow>(MGComboBox<object>.DropdownWindowPartName);
            MGScrollViewer DropdownScrollViewer = Context.GetRequiredPart<MGScrollViewer>(MGComboBox<object>.DropdownScrollViewerPartName);
            MGStackPanel DropdownItemsPanel = Context.GetRequiredPart<MGStackPanel>(MGComboBox<object>.DropdownItemsPanelPartName);

            if (!Context.IsThemeRefresh)
            {
                Context.Owner.Padding = new(4, 2, 4, 2);
                Context.Owner.MinHeight = 26;
                Border.BorderBrush ??= MGUniformBorderBrush.Black;
                DropdownArrow.Margin = new(MGComboBox<object>.DefaultDropdownArrowLeftMargin, 0, MGComboBox<object>.DefaultDropdownArrowRightMargin, 0);
                Dropdown.PreferredWidth = Math.Max(Dropdown.PreferredWidth ?? 0, 100);
                Dropdown.BorderThickness = new(1);
                Dropdown.BorderBrush = MGUniformBorderBrush.Gray;
                Dropdown.Padding = new(0);
                DropdownScrollViewer.Padding = new(0);
                DropdownItemsPanel.Spacing = 0;
            }
            Dropdown.BackgroundBrush = Theme.ComboBoxDropdownBackground.GetValue(true);
        }

        private static void ApplyTreeViewTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGTreeView TreeView)
            {
                return;
            }

            MGTheme Theme = TreeView.GetTheme();
            MGBorder OuterBorder = Context.GetRequiredPart<MGBorder>(MGTreeView.OuterBorderPartName);
            MGScrollViewer ScrollViewer = Context.GetRequiredPart<MGScrollViewer>(MGTreeView.ScrollViewerPartName);
            MGStackPanel ItemsPanel = Context.GetRequiredPart<MGStackPanel>(MGTreeView.ItemsPanelPartName);

            OuterBorder.BorderBrush = Theme?.TreeViewBorderBrush ?? MGUniformBorderBrush.Black;
            OuterBorder.BorderThickness = Theme?.TreeViewBorderThickness ?? new Thickness(1);
            if (!Context.IsThemeRefresh)
            {
                ScrollViewer.Padding = new(0);
                ItemsPanel.Padding = new(0);
                ItemsPanel.Spacing = 0;
            }
            VisualStateFillBrush SelectionBrush = Theme?.TreeViewSelectionBackground?.GetValue(true);
            TreeView.SelectionBackgroundBrush = SelectionBrush ?? new VisualStateFillBrush(new MGSolidFillBrush(Color.LightBlue));
            TreeView.SelectionForeground = Theme?.TreeViewSelectionForeground ?? Color.Black;
            TreeView.IndentSize = Theme?.TreeViewIndentSize ?? TreeView.IndentSize;
        }

        private static void ApplyTabControlTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGTabControl TabControl)
            {
                return;
            }

            MGTheme Theme = TabControl.GetTheme();
            MGBorder Border = Context.GetRequiredPart<MGBorder>(MGTabControl.BorderPartName);
            MGStackPanel HeadersPanel = Context.GetRequiredPart<MGStackPanel>(MGTabControl.HeadersPanelPartName);

            if (!Context.IsThemeRefresh)
            {
                TabControl.Padding = new(12);
                Border.BorderBrush = MGUniformBorderBrush.Black;
                Border.BorderThickness = new(1);
                HeadersPanel.Spacing = 0;

                TabControl.SelectedTabHeaderTemplate = (MGTabItem TabItem) =>
                {
                    MGButton Button = new(TabItem.SelfOrParentWindow, _ => TabItem.IsTabSelected = true)
                    {
                        IsFocusable = false
                    };
                    TabControl.ApplyDefaultSelectedTabHeaderStyle(Button);
                    return Button;
                };

                TabControl.UnselectedTabHeaderTemplate = (MGTabItem TabItem) =>
                {
                    MGButton Button = new(TabItem.SelfOrParentWindow, _ => TabItem.IsTabSelected = true)
                    {
                        IsFocusable = false
                    };
                    TabControl.ApplyDefaultUnselectedTabHeaderStyle(Button);
                    return Button;
                };
            }
            HeadersPanel.BackgroundBrush = Theme.TitleBackground.GetValue(true);
        }

        private static void ApplyDockTabItemTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGDockTabItem TabItem)
            {
                return;
            }

            MGThemeDockingSettings Docking = TabItem.GetTheme()?.Docking;
            if (Docking == null)
            {
                return;
            }

            TabItem.NormalBrush = Docking.TabNormalBackground;
            TabItem.HoverBrush = Docking.TabHoverBackground;
            TabItem.ActiveBrush = Docking.TabActiveBackground;
            TabItem.ActiveAccentColor = Docking.TabActiveAccentColor;
            TabItem.HoverAccentColor = Docking.TabHoverAccentColor;
            TabItem.ActiveTextColor = Docking.TabActiveTextColor;
            TabItem.InactiveTextColor = Docking.TabInactiveTextColor;
            TabItem.ActiveIconColor = Docking.TabActiveIconColor;
            TabItem.InactiveIconColor = Docking.TabInactiveIconColor;
            TabItem.RefreshThemeVisuals();
        }

        private static void ApplyDockAutoHideDrawerTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGDockAutoHideDrawer Drawer)
            {
                return;
            }

            MGThemeDockingSettings Docking = Drawer.GetTheme()?.Docking;
            if (Docking == null)
            {
                return;
            }

            MGBorder Header = Context.GetRequiredPart<MGBorder>(MGDockAutoHideDrawer.HeaderPartName);
            MGBorder PinButton = Context.GetRequiredPart<MGBorder>(MGDockAutoHideDrawer.PinButtonPartName);
            MGBorder CloseButton = Context.GetRequiredPart<MGBorder>(MGDockAutoHideDrawer.CloseButtonPartName);

            Drawer.BackgroundBrush = new VisualStateFillBrush(Docking.AutoHideDrawerBackground);
            Header.BackgroundBrush = new VisualStateFillBrush(Docking.AutoHideDrawerHeaderBackground);
            PinButton.BackgroundBrush = Docking.AutoHideButtonBackground;
            CloseButton.BackgroundBrush = Docking.AutoHideButtonBackground;
            Drawer.HeaderTextColor = Docking.AutoHideHeaderTextColor;
            Drawer.IconColor = Docking.AutoHideIconColor;
            Drawer.BorderColor = Docking.AutoHideBorderColor;
            Drawer.ResizeGripColor = Docking.AutoHideGripColor;
        }

        private static void ApplyDockAutoHideStripTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGDockAutoHideStrip Strip)
            {
                return;
            }

            MGThemeDockingSettings Docking = Strip.GetTheme()?.Docking;
            if (Docking == null)
            {
                return;
            }

            Strip.BackgroundBrush = new VisualStateFillBrush(Docking.AutoHideStripBackground);
            Strip.ButtonBackgroundBrush = Docking.AutoHideStripButtonBackground;
            Strip.TextColor = Docking.AutoHideStripTextColor;
            Strip.SeparatorColor = Docking.AutoHideStripSeparatorColor;
            Strip.ApplyThemeVisuals();
        }

        private static void ApplyDockSplitterTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGDockSplitterBar Splitter)
            {
                return;
            }

            MGThemeDockingSettings Docking = Splitter.GetTheme()?.Docking;
            if (Docking == null)
            {
                return;
            }

            Splitter.NormalBrush = Docking.SplitterNormalBrush;
            Splitter.HoverBrush = Docking.SplitterHoverBrush;
            Splitter.PressedBrush = Docking.SplitterPressedBrush;
            Splitter.HoverOverlayColor = Docking.SplitterHoverOverlayColor;
            Splitter.PressedOverlayColor = Docking.SplitterPressedOverlayColor;
        }

        private static void ApplyDockDropIndicatorsTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGDockDropIndicators Indicators)
            {
                return;
            }

            MGThemeDockingSettings Docking = Indicators.GetTheme()?.Docking;
            if (Docking == null)
            {
                return;
            }

            Indicators.InactiveColor = Docking.DropIndicatorInactiveColor;
            Indicators.ActiveColor = Docking.DropIndicatorActiveColor;
            Indicators.BorderColor = Docking.DropIndicatorBorderColor;
            Indicators.HostInactiveColor = Docking.DropIndicatorHostInactiveColor;
            Indicators.HostActiveColor = Docking.DropIndicatorHostActiveColor;
            Indicators.DisabledColor = Docking.DropIndicatorDisabledColor;
            Indicators.DisabledBorderColor = Docking.DropIndicatorDisabledBorderColor;
            Indicators.SymbolColor = Docking.DropIndicatorSymbolColor;
            Indicators.DisabledSymbolColor = Docking.DropIndicatorDisabledSymbolColor;
        }
    }
}