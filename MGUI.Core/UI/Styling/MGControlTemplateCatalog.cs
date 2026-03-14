using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using Thickness = MonoGame.Extended.Thickness;

namespace MGUI.Core.UI.Styling
{
    public static class MGControlTemplateCatalog
    {
        private const string BuiltInControlTemplatesResourceName = "MGUI.Core.UI.Templates.BuiltInControlTemplates.xaml";
        private static readonly Lazy<IReadOnlyDictionary<string, ControlTemplateDefinition>> BuiltInXamlTemplateDefinitions = new(LoadBuiltInXamlTemplateDefinitions);

        public const string WindowTemplateName = "Window.Default";
        public const string OverlayTemplateName = "Overlay.Default";
        public const string ContextMenuTemplateName = "ContextMenu.Default";
        public const string ContextMenuItemTemplateName = "ContextMenuItem.Default";
        public const string ListBoxTemplateName = "ListBox.Default";
        public const string ListViewTemplateName = "ListView.Default";
        public const string ComboBoxTemplateName = "ComboBox.Default";
        public const string TreeViewTemplateName = "TreeView.Default";
        public const string TextBoxTemplateName = "TextBox.Default";
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

            Register(Resources, CreateWindowTemplate());
            Register(Resources, CreateOverlayTemplate());
            Register(Resources, ContextMenuTemplateName, ApplyContextMenuTemplate);
            Register(Resources, ContextMenuItemTemplateName, ApplyContextMenuItemTemplate);
            Register(Resources, CreateBuiltInXamlTemplate(ListBoxTemplateName, ApplyListBoxTemplate));
            Register(Resources, CreateBuiltInXamlTemplate(ListViewTemplateName, ApplyListViewTemplate));
            Register(Resources, CreateComboBoxTemplate());
            Register(Resources, CreateTreeViewTemplate());
            Register(Resources, CreateTextBoxTemplate());
            Register(Resources, CreateTabControlTemplate());
            Register(Resources, DockTabItemTemplateName, ApplyDockTabItemTemplate);
            Register(Resources, DockAutoHideDrawerTemplateName, ApplyDockAutoHideDrawerTemplate);
            Register(Resources, DockAutoHideStripTemplateName, ApplyDockAutoHideStripTemplate);
            Register(Resources, DockSplitterTemplateName, ApplyDockSplitterTemplate);
            Register(Resources, DockDropIndicatorsTemplateName, ApplyDockDropIndicatorsTemplate);
        }

        private static bool IsGenericControl(MGElement Owner, Type GenericDefinition)
            => Owner?.GetType().IsGenericType == true && Owner.GetType().GetGenericTypeDefinition() == GenericDefinition;

        private static IReadOnlyDictionary<string, ControlTemplateDefinition> LoadBuiltInXamlTemplateDefinitions()
        {
            string markup = GeneralUtils.ReadEmbeddedResourceAsString(Assembly.GetExecutingAssembly(), BuiltInControlTemplatesResourceName);
            return ControlTemplateLoader.ParseDefinitions(XamlDocumentSource.FromString(markup, BuiltInControlTemplatesResourceName))
                .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
                .ToDictionary(x => x.Name, StringComparer.Ordinal);
        }

        private static MGControlTemplate CreateBuiltInXamlTemplate(string name,
            Action<MGControlTemplateContext> applyDefaults)
        {
            if (!BuiltInXamlTemplateDefinitions.Value.TryGetValue(name, out ControlTemplateDefinition definition))
            {
                throw new InvalidOperationException($"Missing built-in control template asset definition '{name}'.");
            }

            return ControlTemplateLoader.CreateTemplate(definition, applyDefaults);
        }

        private static MGControlTemplate CreateWindowTemplate()
            => new(WindowTemplateName, CreateWindowTemplateStructure, null, ApplyWindowTemplate);

        private static MGControlTemplate CreateOverlayTemplate()
            => new(OverlayTemplateName, CreateOverlayTemplateStructure, null, ApplyOverlayTemplate);

        private static MGControlTemplate CreateComboBoxTemplate()
            => new(ComboBoxTemplateName, CreateComboBoxTemplateStructure, null, ApplyComboBoxTemplate);

        private static MGControlTemplate CreateTreeViewTemplate()
            => new(TreeViewTemplateName, CreateTreeViewTemplateStructure, null, ApplyTreeViewTemplate);

        private static MGControlTemplate CreateTextBoxTemplate()
            => new(TextBoxTemplateName, CreateTextBoxTemplateStructure, null, ApplyTextBoxTemplate);

        private static MGControlTemplate CreateTabControlTemplate()
            => new(TabControlTemplateName, CreateTabControlTemplateStructure, null, ApplyTabControlTemplate);

        private static void Register(MGResources Resources, string Name, Action<MGControlTemplateContext> Apply)
        {
            if (!Resources.TryGetControlTemplate(Name, out _))
            {
                Resources.AddControlTemplate(new(Name, Apply));
            }
        }

        private static void Register(MGResources Resources, MGControlTemplate Template)
        {
            if (!Resources.TryGetControlTemplate(Template.Name, out _))
            {
                Resources.AddControlTemplate(Template);
            }
        }

        private static MGControlTemplateStructure CreateWindowTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGWindow Window)
            {
                return null;
            }

            MGBorder border = new(Window, new Thickness(2), MGUniformBorderBrush.Black);
            MGDockPanel titleBar = new(Window)
            {
                Padding = new(2),
                MinHeight = 24,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                BackgroundBrush = Window.GetTheme().TitleBackground.GetValue(true),
                DrawBackgroundEnabled = false,
                CanChangeContent = false,
            };

            MGButton closeButton = new(Window, _ => Window.TryCloseWindow())
            {
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            MGTextBlock titleText = new(Window, null, Color.White, Window.GetTheme().FontSettings.SmallFontSize)
            {
                Margin = new(4, 0),
                Padding = new(0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = HorizontalAlignment.Left,
                DefaultTextForeground = new VisualStateSetting<Color?>(Color.White, Color.White, Color.White)
            };
            MGResizeGrip resizeGrip = new(Window);

            titleBar.TryAddChild(closeButton, Dock.Right);
            titleBar.TryAddChild(titleText, Dock.Left);

            MGControlTemplateStructure structure = new(titleBar);
            structure.AddPart(MGWindow.BorderPartName, border);
            structure.AddPart(MGWindow.TitleBarPartName, titleBar);
            structure.AddPart(MGWindow.CloseButtonPartName, closeButton);
            structure.AddPart(MGWindow.TitleBarTextPartName, titleText);
            structure.AddPart(MGWindow.ResizeGripPartName, resizeGrip);
            return structure;
        }

        private static MGControlTemplateStructure CreateOverlayTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGOverlay Overlay)
            {
                return null;
            }

            MGBorder border = new(Overlay.SelfOrParentWindow, new(1), Color.Black.AsFillBrush());
            MGButton closeButton = new(Overlay.SelfOrParentWindow, _ => Overlay.IsOpen = false)
            {
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };

            MGControlTemplateStructure structure = new(closeButton);
            structure.AddPart(MGOverlay.BorderPartName, border);
            structure.AddPart(MGOverlay.CloseButtonPartName, closeButton);
            return structure;
        }

        private static MGControlTemplateStructure CreateComboBoxTemplateStructure(MGControlTemplateContext Context)
        {
            if (!IsGenericControl(Context.Owner, typeof(MGComboBox<>)) || Context.Owner is not MGSingleContentHost owner)
            {
                return null;
            }

            MGWindow window = owner.SelfOrParentWindow;
            MGBorder border = new(window, new Thickness(1), MGUniformBorderBrush.Black);
            MGContentPresenter dropdownArrow = new(window) { PreferredWidth = MGComboBox<object>.DropdownArrowPaddedWidth, PreferredHeight = MGComboBox<object>.DropdownArrowPaddedHeight };
            MGWindow dropdown = new(window, 0, 0, 100, 300, window.Theme)
            {
                ManagedParent = owner,
                IsUserResizable = false,
                IsTitleBarVisible = false,
                Scale = window.Scale,
            };
            MGContentPresenter dropdownHeaderPresenter = new(dropdown);
            MGContentPresenter dropdownFooterPresenter = new(dropdown);
            MGStackPanel dropdownStackPanel = new(dropdown, Orientation.Vertical) { Spacing = 0, ManagedParent = dropdown };
            MGScrollViewer dropdownScrollViewer = new(dropdown, ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled) { Padding = new(0), ManagedParent = dropdown };
            MGDockPanel dropdownDockPanel = new(dropdown, true);

            dropdownArrow.Margin = new(MGComboBox<object>.DefaultDropdownArrowLeftMargin, 0, MGComboBox<object>.DefaultDropdownArrowRightMargin, 0);
            dropdown.BorderThickness = new(1);
            dropdown.BorderBrush = MGUniformBorderBrush.Gray;
            dropdown.BackgroundBrush = owner.GetTheme().ComboBoxDropdownBackground.GetValue(true);
            dropdown.Padding = new(0);

            dropdownScrollViewer.SetContent(dropdownStackPanel);
            dropdownDockPanel.TryAddChild(dropdownHeaderPresenter, Dock.Top);
            dropdownDockPanel.TryAddChild(dropdownFooterPresenter, Dock.Bottom);
            dropdownDockPanel.TryAddChild(dropdownScrollViewer, Dock.Top);
            dropdown.SetContent(dropdownDockPanel);
            dropdownHeaderPresenter.CanChangeContent = false;
            dropdownFooterPresenter.CanChangeContent = false;
            dropdownStackPanel.CanChangeContent = false;
            dropdownScrollViewer.CanChangeContent = false;
            dropdownDockPanel.CanChangeContent = false;
            dropdown.CanChangeContent = false;

            MGControlTemplateStructure structure = new(dropdownDockPanel);
            structure.AddPart(MGComboBox<object>.BorderPartName, border);
            structure.AddPart(MGComboBox<object>.DropdownArrowPartName, dropdownArrow);
            structure.AddPart(MGComboBox<object>.DropdownWindowPartName, dropdown);
            structure.AddPart(MGComboBox<object>.DropdownHeaderPresenterPartName, dropdownHeaderPresenter);
            structure.AddPart(MGComboBox<object>.DropdownFooterPresenterPartName, dropdownFooterPresenter);
            structure.AddPart(MGComboBox<object>.DropdownItemsPanelPartName, dropdownStackPanel);
            structure.AddPart(MGComboBox<object>.DropdownScrollViewerPartName, dropdownScrollViewer);
            structure.AddPart(MGComboBox<object>.DropdownDockPanelPartName, dropdownDockPanel);
            return structure;
        }

        private static MGControlTemplateStructure CreateTabControlTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGTabControl TabControl)
            {
                return null;
            }

            MGBorder border = new(TabControl.SelfOrParentWindow);
            MGStackPanel headersPanel = new(TabControl.SelfOrParentWindow, Orientation.Horizontal)
            {
                CanChangeContent = false,
                Spacing = 0,
            };

            MGControlTemplateStructure structure = new(headersPanel);
            structure.AddPart(MGTabControl.BorderPartName, border);
            structure.AddPart(MGTabControl.HeadersPanelPartName, headersPanel);
            return structure;
        }

        private static MGControlTemplateStructure CreateTreeViewTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGTreeView treeView)
            {
                return null;
            }

            MGWindow window = treeView.SelfOrParentWindow;
            MGBorder outerBorder = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0),
                Padding = new Thickness(0),
            };
            MGScrollViewer scrollViewer = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0),
                Padding = new Thickness(0),
            };
            MGStackPanel itemsPanel = new(window, Orientation.Vertical)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0),
                Padding = new Thickness(0),
                CanChangeContent = false,
            };

            MGControlTemplateStructure structure = new(outerBorder);
            structure.AddPart(MGTreeView.OuterBorderPartName, outerBorder);
            structure.AddPart(MGTreeView.ScrollViewerPartName, scrollViewer);
            structure.AddPart(MGTreeView.ItemsPanelPartName, itemsPanel);
            return structure;
        }

        private static MGControlTemplateStructure CreateTextBoxTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGTextBox textBox)
            {
                return null;
            }

            MGWindow window = textBox.SelfOrParentWindow;
            MGBorder border = new(window);
            MGResizeGrip resizeGrip = new(window);
            MGTextBlock placeholder = new(window, string.Empty)
            {
                Visibility = Visibility.Collapsed,
            };
            MGTextBlock characterCount = new(window, "0")
            {
                Margin = new(0, 0, 8, 4),
            };
            _ = characterCount.TrySetFont(window.Desktop.FontManager.DefaultFontFamily, 9);
            MGTextBlock textBlock = new(window, string.Empty)
            {
                ClipToBounds = false,
            };

            MGControlTemplateStructure structure = new(null);
            structure.AddPart(MGTextBox.BorderPartName, border);
            structure.AddPart(MGTextBox.ResizeGripPartName, resizeGrip);
            structure.AddPart(MGTextBox.PlaceholderTextBlockPartName, placeholder);
            structure.AddPart(MGTextBox.CharacterCountPartName, characterCount);
            structure.AddPart(MGTextBox.TextBlockPartName, textBlock);
            return structure;
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
            Context.ApplyThemeDefault("Window.BorderBrush", Theme.Window.BorderBrush, () => Border.BorderBrush, value => Border.BorderBrush = value);
            Context.ApplyThemeDefault("Window.TitleBarPadding", Theme.Window.TitleBarPadding, () => TitleBar.Padding, value => TitleBar.Padding = value);
            Context.ApplyThemeDefault("Window.TitleBarMinHeight", Theme.Window.TitleBarMinHeight, () => TitleBar.MinHeight ?? 0, value => TitleBar.MinHeight = value);
            Context.ApplyThemeDefault("Window.TitleBarBackground", Theme.TitleBackground.GetValue(true), () => TitleBar.BackgroundBrush, value => TitleBar.BackgroundBrush = value);

            Context.ApplyThemeDefault("Window.CloseButtonMinWidth", Theme.Window.CloseButtonMinWidth, () => CloseButton.MinWidth ?? 0, value => CloseButton.MinWidth = value);
            Context.ApplyThemeDefault("Window.CloseButtonMinHeight", Theme.Window.CloseButtonMinHeight, () => CloseButton.MinHeight ?? 0, value => CloseButton.MinHeight = value);
            Context.ApplyThemeDefault("Window.CloseButtonBackground", Theme.Window.CloseButtonBackground, () => CloseButton.BackgroundBrush, value => CloseButton.BackgroundBrush = value);
            Context.ApplyThemeDefault("Window.CloseButtonBorderBrush", Theme.Window.CloseButtonBorderBrush, () => CloseButton.BorderBrush, value => CloseButton.BorderBrush = value);
            Context.ApplyThemeDefault("Window.CloseButtonBorderThickness", Theme.Window.CloseButtonBorderThickness, () => CloseButton.BorderThickness, value => CloseButton.BorderThickness = value);
            Context.ApplyThemeDefault("Window.CloseButtonMargin", Theme.Window.CloseButtonMargin, () => CloseButton.Margin, value => CloseButton.Margin = value);
            Context.ApplyThemeDefault("Window.CloseButtonPadding", Theme.Window.CloseButtonPadding, () => CloseButton.Padding, value => CloseButton.Padding = value);
            Context.ApplyThemeDefault("Window.TitleTextMargin", Theme.Window.TitleTextMargin, () => TitleText.Margin, value => TitleText.Margin = value);
            Context.ApplyThemeDefault("Window.TitleTextPadding", Theme.Window.TitleTextPadding, () => TitleText.Padding, value => TitleText.Padding = value);
            Context.ApplyThemeDefault("Window.TitleTextForeground", Theme.Window.TitleTextForeground, () => TitleText.DefaultTextForeground, value => TitleText.DefaultTextForeground = value);

            if (!Context.IsThemeRefresh && CloseButton.Content == null)
            {
                CloseButton.VerticalAlignment = VerticalAlignment.Center;
                CloseButton.VerticalContentAlignment = VerticalAlignment.Center;
                CloseButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                TitleText.HorizontalAlignment = HorizontalAlignment.Stretch;
                TitleText.VerticalAlignment = VerticalAlignment.Center;
                TitleText.TextAlignment = HorizontalAlignment.Left;
                CloseButton.SetContent(new MGTextBlock(Window, "[b][shadow=Black 1 1]x[/shadow][/b]", Color.White));
            }

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

            MGTheme Theme = Overlay.GetTheme();
            Context.ApplyThemeDefault("Overlay.Padding", Theme.Overlay.Padding, () => Overlay.Padding, value => Overlay.Padding = value);
            Context.ApplyThemeDefault("Overlay.BorderThickness", Theme.Overlay.BorderThickness, () => Border.BorderThickness, value => Border.BorderThickness = value);
            Context.ApplyThemeDefault("Overlay.BorderBrush", Theme.Overlay.BorderBrush, () => Border.BorderBrush, value => Border.BorderBrush = value);
            Context.ApplyThemeDefault("Overlay.CloseButtonMinWidth", Theme.Overlay.CloseButtonMinWidth, () => CloseButton.MinWidth ?? 0, value => CloseButton.MinWidth = value);
            Context.ApplyThemeDefault("Overlay.CloseButtonMinHeight", Theme.Overlay.CloseButtonMinHeight, () => CloseButton.MinHeight ?? 0, value => CloseButton.MinHeight = value);
            Context.ApplyThemeDefault("Overlay.CloseButtonBackground", Theme.Overlay.CloseButtonBackground, () => CloseButton.BackgroundBrush, value => CloseButton.BackgroundBrush = value);
            Context.ApplyThemeDefault("Overlay.CloseButtonBorderBrush", Theme.Overlay.CloseButtonBorderBrush, () => CloseButton.BorderBrush, value => CloseButton.BorderBrush = value);
            Context.ApplyThemeDefault("Overlay.CloseButtonBorderThickness", Theme.Overlay.CloseButtonBorderThickness, () => CloseButton.BorderThickness, value => CloseButton.BorderThickness = value);
            Context.ApplyThemeDefault("Overlay.CloseButtonPadding", Theme.Overlay.CloseButtonPadding, () => CloseButton.Padding, value => CloseButton.Padding = value);

            if (!Context.IsThemeRefresh && CloseButton.Content == null)
            {
                CloseButton.SetContent(new MGTextBlock(Overlay.Host.ParentWindow, "[b][shadow=Black 1 1]x[/shadow][/b]", Color.White));
            }
        }

        private static void ApplyContextMenuTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGContextMenu Menu)
            {
                return;
            }

            MGTheme Theme = Menu.GetTheme();
            Context.ApplyThemeDefault("ContextMenu.Padding", Theme.ContextMenu.Padding, () => Menu.Padding, value => Menu.Padding = value);
            Context.ApplyThemeDefault("ContextMenu.BorderBrush", Theme.ContextMenu.BorderBrush, () => Menu.BorderBrush, value => Menu.BorderBrush = value);
            Context.ApplyThemeDefault("ContextMenu.BorderThickness", Theme.ContextMenu.BorderThickness, () => Menu.BorderThickness, value => Menu.BorderThickness = value);
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
            MGTheme Theme = Context.Owner.GetTheme();
            Context.ApplyThemeDefault("ContextMenuItem.HeaderMargin", Theme.ContextMenuItem.HeaderMargin, () => HeaderPresenter.Margin, value => HeaderPresenter.Margin = value);
            Context.ApplyThemeDefault("ContextMenuItem.HeaderBackground", Theme.ContextMenuItem.HeaderBackground, () => HeaderPresenter.BackgroundBrush, value => HeaderPresenter.BackgroundBrush = value);
            Context.ApplyThemeDefault("ContextMenuItem.ShortcutMargin", Theme.ContextMenuItem.ShortcutMargin, () => ShortcutText.Margin, value => ShortcutText.Margin = value);
            Context.ApplyThemeDefault("ContextMenuItem.ShortcutForeground", Theme.ContextMenuItem.ShortcutForeground, () => ShortcutText.Foreground, value => ShortcutText.Foreground = value);
            Context.ApplyThemeDefault("ContextMenuItem.SubmenuArrowMargin", Theme.ContextMenuItem.SubmenuArrowMargin, () => Arrow.Margin, value => Arrow.Margin = value);
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

            Context.ApplyThemeDefault("ListBox.OuterBackground", Theme.ListBox.OuterBackground, () => OuterBorder.BackgroundBrush, value => OuterBorder.BackgroundBrush = value);
            Context.ApplyThemeDefault("ListBox.TitlePadding", Theme.ListBox.TitlePadding, () => TitleBorder.Padding, value => TitleBorder.Padding = value);
            Context.ApplyThemeDefault("ListBox.TitleBackground", Theme.TitleBackground.GetValue(true), () => TitleBorder.BackgroundBrush, value => TitleBorder.BackgroundBrush = value);
            Context.ApplyThemeDefault("ListBox.TitleForeground", Theme.ListBox.TitleForeground, () => TitleBorder.DefaultTextForeground, value => TitleBorder.DefaultTextForeground = value);
            Context.ApplyThemeDefault("ListBox.TitleBorderBrush", Theme.ListBox.TitleBorderBrush, () => TitleBorder.BorderBrush, value => TitleBorder.BorderBrush = value);
            Context.ApplyThemeDefault("ListBox.TitleBorderThickness", Theme.ListBox.TitleBorderThickness, () => TitleBorder.BorderThickness, value => TitleBorder.BorderThickness = value);
            Context.ApplyThemeDefault("ListBox.InnerBorderBrush", Theme.ListBox.InnerBorderBrush, () => InnerBorder.BorderBrush, value => InnerBorder.BorderBrush = value);
            Context.ApplyThemeDefault("ListBox.InnerBorderThickness", Theme.ListBox.InnerBorderThickness, () => InnerBorder.BorderThickness, value => InnerBorder.BorderThickness = value);
            Context.ApplyThemeDefault("ListBox.ScrollViewerPadding", Theme.ListBox.ScrollViewerPadding, () => ScrollViewer.Padding, value => ScrollViewer.Padding = value);
            Context.ApplyThemeDefault("ListBox.ItemsPanelBorderBrush", Theme.ListBox.ItemsPanelBorderBrush, () => ItemsPanel.BorderBrush, value => ItemsPanel.BorderBrush = value);
            Context.ApplyThemeDefault("ListBox.ItemsPanelBorderThickness", Theme.ListBox.ItemsPanelBorderThickness, () => ItemsPanel.BorderThickness, value => ItemsPanel.BorderThickness = value);

            if (!Context.IsThemeRefresh)
            {
                ItemsPanel.VerticalAlignment = VerticalAlignment.Top;
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

            Context.ApplyThemeDefault("ListView.HeaderBackground", Theme.TitleBackground.GetValue(true), () => HeaderGrid.BackgroundBrush, value => HeaderGrid.BackgroundBrush = value);
            Context.ApplyThemeDefault("ListView.HeaderForeground", Theme.ListView.HeaderForeground, () => HeaderGrid.DefaultTextForeground, value => HeaderGrid.DefaultTextForeground = value);
            Context.ApplyThemeDefault("ListView.HeaderHorizontalGridLineBrush", Theme.ListView.GridLineBrush, () => HeaderGrid.HorizontalGridLineBrush, value => HeaderGrid.HorizontalGridLineBrush = value);
            Context.ApplyThemeDefault("ListView.HeaderVerticalGridLineBrush", Theme.ListView.GridLineBrush, () => HeaderGrid.VerticalGridLineBrush, value => HeaderGrid.VerticalGridLineBrush = value);
            Context.ApplyThemeDefault("ListView.DataHorizontalGridLineBrush", Theme.ListView.GridLineBrush, () => DataGrid.HorizontalGridLineBrush, value => DataGrid.HorizontalGridLineBrush = value);
            Context.ApplyThemeDefault("ListView.DataVerticalGridLineBrush", Theme.ListView.GridLineBrush, () => DataGrid.VerticalGridLineBrush, value => DataGrid.VerticalGridLineBrush = value);
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

            Context.ApplyThemeDefault("ComboBox.Padding", Theme.ComboBox.Padding, () => Context.Owner.Padding, value => Context.Owner.Padding = value);
            Context.ApplyThemeDefault("ComboBox.MinHeight", Theme.ComboBox.MinHeight, () => Context.Owner.MinHeight ?? 0, value => Context.Owner.MinHeight = value);
            Context.ApplyThemeDefault("ComboBox.BorderBrush", Theme.ComboBox.BorderBrush, () => Border.BorderBrush, value => Border.BorderBrush = value);
            Context.ApplyThemeDefault("ComboBox.DropdownArrowMargin", Theme.ComboBox.DropdownArrowMargin, () => DropdownArrow.Margin, value => DropdownArrow.Margin = value);
            Context.ApplyThemeDefault("ComboBox.DropdownBorderThickness", Theme.ComboBox.DropdownBorderThickness, () => Dropdown.BorderThickness, value => Dropdown.BorderThickness = value);
            Context.ApplyThemeDefault("ComboBox.DropdownBorderBrush", Theme.ComboBox.DropdownBorderBrush, () => Dropdown.BorderBrush, value => Dropdown.BorderBrush = value);
            Context.ApplyThemeDefault("ComboBox.DropdownBackground", Theme.ComboBoxDropdownBackground.GetValue(true), () => Dropdown.BackgroundBrush, value => Dropdown.BackgroundBrush = value);
            Context.ApplyThemeDefault("ComboBox.DropdownPadding", Theme.ComboBox.DropdownPadding, () => Dropdown.Padding, value => Dropdown.Padding = value);
            Context.ApplyThemeDefault("ComboBox.DropdownScrollPadding", Theme.ComboBox.DropdownScrollViewerPadding, () => DropdownScrollViewer.Padding, value => DropdownScrollViewer.Padding = value);
            Context.ApplyThemeDefault("ComboBox.DropdownItemsSpacing", Theme.ComboBox.DropdownItemsSpacing, () => DropdownItemsPanel.Spacing, value => DropdownItemsPanel.Spacing = value);

            if (!Context.IsThemeRefresh)
            {
                Dropdown.PreferredWidth = Math.Max(Dropdown.PreferredWidth ?? 0, Theme.ComboBox.DropdownMinWidth);
            }
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

            Context.ApplyThemeDefault("TreeView.BorderBrush", Theme?.TreeViewBorderBrush ?? MGUniformBorderBrush.Black, () => OuterBorder.BorderBrush, value => OuterBorder.BorderBrush = value);
            Context.ApplyThemeDefault("TreeView.BorderThickness", Theme?.TreeViewBorderThickness ?? new Thickness(1), () => OuterBorder.BorderThickness, value => OuterBorder.BorderThickness = value);
            Context.ApplyThemeDefault("TreeView.ScrollViewerPadding", Theme.TreeViewTemplate.ScrollViewerPadding, () => ScrollViewer.Padding, value => ScrollViewer.Padding = value);
            Context.ApplyThemeDefault("TreeView.ItemsPanelPadding", Theme.TreeViewTemplate.ItemsPanelPadding, () => ItemsPanel.Padding, value => ItemsPanel.Padding = value);
            Context.ApplyThemeDefault("TreeView.ItemsPanelSpacing", Theme.TreeViewTemplate.ItemsPanelSpacing, () => ItemsPanel.Spacing, value => ItemsPanel.Spacing = value);
            VisualStateFillBrush SelectionBrush = Theme?.TreeViewSelectionBackground?.GetValue(true);
            Context.ApplyThemeDefault("TreeView.SelectionBackgroundBrush", SelectionBrush ?? new VisualStateFillBrush(new MGSolidFillBrush(Color.LightBlue)), () => TreeView.SelectionBackgroundBrush, value => TreeView.SelectionBackgroundBrush = value);
            Context.ApplyThemeDefault("TreeView.SelectionForeground", Theme?.TreeViewSelectionForeground ?? Color.Black, () => TreeView.SelectionForeground, value => TreeView.SelectionForeground = value);
            Context.ApplyThemeDefault("TreeView.IndentSize", Theme?.TreeViewIndentSize ?? TreeView.IndentSize, () => TreeView.IndentSize, value => TreeView.IndentSize = value);
        }

        private static void ApplyTextBoxTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGTextBox textBox)
            {
                return;
            }

            MGTheme theme = textBox.GetTheme();
            Context.ApplyThemeDefault("TextBox.FocusedSelectionForeground", theme.TextBoxFocusedSelectionForeground, () => textBox.FocusedSelectionForegroundColor, value => textBox.FocusedSelectionForegroundColor = value);
            Context.ApplyThemeDefault("TextBox.FocusedSelectionBackground", theme.TextBoxFocusedSelectionBackground, () => textBox.FocusedSelectionBackgroundColor, value => textBox.FocusedSelectionBackgroundColor = value);
            Context.ApplyThemeDefault("TextBox.UnfocusedSelectionForeground", theme.TextBoxUnfocusedSelectionForeground, () => textBox.UnfocusedSelectionForegroundColor, value => textBox.UnfocusedSelectionForegroundColor = value);
            Context.ApplyThemeDefault("TextBox.UnfocusedSelectionBackground", theme.TextBoxUnfocusedSelectionBackground, () => textBox.UnfocusedSelectionBackgroundColor, value => textBox.UnfocusedSelectionBackgroundColor = value);
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

            Context.ApplyThemeDefault("TabControl.Padding", Theme.TabControl.Padding, () => TabControl.Padding, value => TabControl.Padding = value);
            Context.ApplyThemeDefault("TabControl.BorderBrush", Theme.TabControl.BorderBrush, () => Border.BorderBrush, value => Border.BorderBrush = value);
            Context.ApplyThemeDefault("TabControl.BorderThickness", Theme.TabControl.BorderThickness, () => Border.BorderThickness, value => Border.BorderThickness = value);
            Context.ApplyThemeDefault("TabControl.HeadersSpacing", Theme.TabControl.HeadersSpacing, () => HeadersPanel.Spacing, value => HeadersPanel.Spacing = value);
            Context.ApplyThemeDefault("TabControl.HeadersBackground", Theme.TitleBackground.GetValue(true), () => HeadersPanel.BackgroundBrush, value => HeadersPanel.BackgroundBrush = value);

            if (!Context.IsThemeRefresh)
            {
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

            Context.ApplyThemeDefault("DockTabItem.NormalBrush", Docking.TabNormalBackground, () => TabItem.NormalBrush, value => TabItem.NormalBrush = value);
            Context.ApplyThemeDefault("DockTabItem.HoverBrush", Docking.TabHoverBackground, () => TabItem.HoverBrush, value => TabItem.HoverBrush = value);
            Context.ApplyThemeDefault("DockTabItem.ActiveBrush", Docking.TabActiveBackground, () => TabItem.ActiveBrush, value => TabItem.ActiveBrush = value);
            Context.ApplyThemeDefault("DockTabItem.ActiveAccentColor", Docking.TabActiveAccentColor, () => TabItem.ActiveAccentColor, value => TabItem.ActiveAccentColor = value);
            Context.ApplyThemeDefault("DockTabItem.HoverAccentColor", Docking.TabHoverAccentColor, () => TabItem.HoverAccentColor, value => TabItem.HoverAccentColor = value);
            Context.ApplyThemeDefault("DockTabItem.ActiveTextColor", Docking.TabActiveTextColor, () => TabItem.ActiveTextColor, value => TabItem.ActiveTextColor = value);
            Context.ApplyThemeDefault("DockTabItem.InactiveTextColor", Docking.TabInactiveTextColor, () => TabItem.InactiveTextColor, value => TabItem.InactiveTextColor = value);
            Context.ApplyThemeDefault("DockTabItem.ActiveIconColor", Docking.TabActiveIconColor, () => TabItem.ActiveIconColor, value => TabItem.ActiveIconColor = value);
            Context.ApplyThemeDefault("DockTabItem.InactiveIconColor", Docking.TabInactiveIconColor, () => TabItem.InactiveIconColor, value => TabItem.InactiveIconColor = value);
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

            Context.ApplyThemeDefault("DockDrawer.BackgroundBrush", new VisualStateFillBrush(Docking.AutoHideDrawerBackground), () => Drawer.BackgroundBrush, value => Drawer.BackgroundBrush = value);
            Context.ApplyThemeDefault("DockDrawer.HeaderBackgroundBrush", new VisualStateFillBrush(Docking.AutoHideDrawerHeaderBackground), () => Header.BackgroundBrush, value => Header.BackgroundBrush = value);
            Context.ApplyThemeDefault("DockDrawer.PinButtonBackground", Docking.AutoHideButtonBackground, () => PinButton.BackgroundBrush, value => PinButton.BackgroundBrush = value);
            Context.ApplyThemeDefault("DockDrawer.CloseButtonBackground", Docking.AutoHideButtonBackground, () => CloseButton.BackgroundBrush, value => CloseButton.BackgroundBrush = value);
            Context.ApplyThemeDefault("DockDrawer.HeaderTextColor", Docking.AutoHideHeaderTextColor, () => Drawer.HeaderTextColor, value => Drawer.HeaderTextColor = value);
            Context.ApplyThemeDefault("DockDrawer.IconColor", Docking.AutoHideIconColor, () => Drawer.IconColor, value => Drawer.IconColor = value);
            Context.ApplyThemeDefault("DockDrawer.BorderColor", Docking.AutoHideBorderColor, () => Drawer.BorderColor, value => Drawer.BorderColor = value);
            Context.ApplyThemeDefault("DockDrawer.ResizeGripColor", Docking.AutoHideGripColor, () => Drawer.ResizeGripColor, value => Drawer.ResizeGripColor = value);
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

            Context.ApplyThemeDefault("DockStrip.BackgroundBrush", new VisualStateFillBrush(Docking.AutoHideStripBackground), () => Strip.BackgroundBrush, value => Strip.BackgroundBrush = value);
            Context.ApplyThemeDefault("DockStrip.ButtonBackgroundBrush", Docking.AutoHideStripButtonBackground, () => Strip.ButtonBackgroundBrush, value => Strip.ButtonBackgroundBrush = value);
            Context.ApplyThemeDefault("DockStrip.TextColor", Docking.AutoHideStripTextColor, () => Strip.TextColor, value => Strip.TextColor = value);
            Context.ApplyThemeDefault("DockStrip.SeparatorColor", Docking.AutoHideStripSeparatorColor, () => Strip.SeparatorColor, value => Strip.SeparatorColor = value);
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

            Context.ApplyThemeDefault("DockSplitter.NormalBrush", Docking.SplitterNormalBrush, () => Splitter.NormalBrush, value => Splitter.NormalBrush = value);
            Context.ApplyThemeDefault("DockSplitter.HoverBrush", Docking.SplitterHoverBrush, () => Splitter.HoverBrush, value => Splitter.HoverBrush = value);
            Context.ApplyThemeDefault("DockSplitter.PressedBrush", Docking.SplitterPressedBrush, () => Splitter.PressedBrush, value => Splitter.PressedBrush = value);
            Context.ApplyThemeDefault("DockSplitter.HoverOverlayColor", Docking.SplitterHoverOverlayColor, () => Splitter.HoverOverlayColor, value => Splitter.HoverOverlayColor = value);
            Context.ApplyThemeDefault("DockSplitter.PressedOverlayColor", Docking.SplitterPressedOverlayColor, () => Splitter.PressedOverlayColor, value => Splitter.PressedOverlayColor = value);
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

            Context.ApplyThemeDefault("DockIndicators.InactiveColor", Docking.DropIndicatorInactiveColor, () => Indicators.InactiveColor, value => Indicators.InactiveColor = value);
            Context.ApplyThemeDefault("DockIndicators.ActiveColor", Docking.DropIndicatorActiveColor, () => Indicators.ActiveColor, value => Indicators.ActiveColor = value);
            Context.ApplyThemeDefault("DockIndicators.BorderColor", Docking.DropIndicatorBorderColor, () => Indicators.BorderColor, value => Indicators.BorderColor = value);
            Context.ApplyThemeDefault("DockIndicators.HostInactiveColor", Docking.DropIndicatorHostInactiveColor, () => Indicators.HostInactiveColor, value => Indicators.HostInactiveColor = value);
            Context.ApplyThemeDefault("DockIndicators.HostActiveColor", Docking.DropIndicatorHostActiveColor, () => Indicators.HostActiveColor, value => Indicators.HostActiveColor = value);
            Context.ApplyThemeDefault("DockIndicators.DisabledColor", Docking.DropIndicatorDisabledColor, () => Indicators.DisabledColor, value => Indicators.DisabledColor = value);
            Context.ApplyThemeDefault("DockIndicators.DisabledBorderColor", Docking.DropIndicatorDisabledBorderColor, () => Indicators.DisabledBorderColor, value => Indicators.DisabledBorderColor = value);
            Context.ApplyThemeDefault("DockIndicators.SymbolColor", Docking.DropIndicatorSymbolColor, () => Indicators.SymbolColor, value => Indicators.SymbolColor = value);
            Context.ApplyThemeDefault("DockIndicators.DisabledSymbolColor", Docking.DropIndicatorDisabledSymbolColor, () => Indicators.DisabledSymbolColor, value => Indicators.DisabledSymbolColor = value);
        }
    }
}