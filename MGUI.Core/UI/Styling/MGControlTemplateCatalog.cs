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
using MGUI.Core.UI.Graph;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using Thickness = MonoGame.Extended.Thickness;

namespace MGUI.Core.UI.Styling
{
    public static class MGControlTemplateCatalog
    {
        private const string BuiltInControlTemplatesResourceName = "MGUI.Core.UI.Templates.BuiltInControlTemplates.xaml";
        private static readonly Lazy<IReadOnlyDictionary<string, ControlTemplateDefinition>> BuiltInXamlTemplateDefinitions = new(LoadBuiltInXamlTemplateDefinitions);
        private static readonly StringComparer TemplateNameComparer = StringComparer.Ordinal;

        public const string WindowTemplateName = "Window.Default";
        public const string ToolTipTemplateName = "ToolTip.Default";
        public const string OverlayTemplateName = "Overlay.Default";
        public const string ContextMenuTemplateName = "ContextMenu.Default";
        public const string ContextMenuItemTemplateName = "ContextMenuItem.Default";
        public const string ListBoxTemplateName = "ListBox.Default";
        public const string ListViewTemplateName = "ListView.Default";
        public const string PropertyGridTemplateName = "PropertyGrid.Default";
        public const string GraphViewTemplateName = "GraphView.Default";
        public const string GraphNodeTemplateName = "GraphNode.Default";
        public const string GraphPortTemplateName = "GraphPort.Default";
        public const string GraphCommentBoxTemplateName = "GraphCommentBox.Default";
        public const string ComboBoxTemplateName = "ComboBox.Default";
        public const string ComboBoxDropdownItemTemplateName = "ComboBox.DropdownItem.Default";
        public const string TreeViewTemplateName = "TreeView.Default";
        public const string TextBoxTemplateName = "TextBox.Default";
        public const string NumericUpDownTemplateName = "NumericUpDown.Default";
        public const string TabControlTemplateName = "TabControl.Default";
        public const string SelectedTabHeaderTemplateName = "TabControl.Header.Selected";
        public const string UnselectedTabHeaderTemplateName = "TabControl.Header.Unselected";
        public const string DockTabItemTemplateName = "Dock.TabItem.Default";
        public const string DockAutoHideDrawerTemplateName = "Dock.AutoHideDrawer.Default";
        public const string DockAutoHideStripTemplateName = "Dock.AutoHideStrip.Default";
        public const string DockSplitterTemplateName = "Dock.Splitter.Default";
        public const string DockDropIndicatorsTemplateName = "Dock.DropIndicators.Default";

        public static readonly Thickness DefaultListBoxItemBorderThickness = new(0, 1);
        public static readonly Thickness DefaultListBoxItemPadding = new(6, 4);
        public static readonly Thickness DefaultListBoxItemContentPadding = new(1, 0);
        public static readonly Thickness DefaultComboBoxDropdownItemPadding = new(8, 5, 8, 5);
        public const int DefaultTreeViewIndentSize = 20;

        public static MGUniformBorderBrush CreateDefaultListBoxItemBorderBrush()
            => new MGSolidFillBrush(Color.Black * 0.35f).AsUniformBorderBrush();

        public static MGElement CreateDefaultListBoxItemContent<TItemType>(MGWindow Window, TItemType Item)
        {
            MGTextBlock content = new(Window, Item?.ToString());
            content.SetPadding(DefaultListBoxItemContentPadding, UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "ListBox.ItemContent.Padding"));
            return content;
        }

        internal static MGTextBlock CreateDefaultControlTextBlock(MGWindow window, string text, bool wrapText, bool isCompact, bool reserveOneLine)
        {
            MGTextBlock textBlock = new(window, text ?? string.Empty)
            {
                WrapText = wrapText,
                TextAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            textBlock.SetMargin(new Thickness(0), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "ControlTextBlock.Margin"));

            if (reserveOneLine)
            {
                textBlock.MinLines = Math.Max(1, textBlock.MinLines);
            }

            if (isCompact)
            {
                textBlock.SetPadding(new Thickness(0), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "ControlTextBlock.CompactPadding"));
                textBlock.MinLines = 1;
                textBlock.MaxLines = 1;
                textBlock.LinePadding = 0;
            }

            return textBlock;
        }

        public static MGButton CreateDefaultComboBoxDropdownItem<TItemType>(MGComboBox<TItemType> ComboBox, TItemType Item)
        {
            if (ComboBox == null)
            {
                return null;
            }

            MGButton button = ComboBox.CreateDefaultDropdownButton();
            button.SetContent(CreateCompactSingleLineTextBlock(ComboBox.SelfOrParentWindow, Item?.ToString() ?? string.Empty));
            return button;
        }

        public static MGElement CreateDefaultComboBoxSelectedItemContent<TItemType>(MGWindow Window, TItemType Item)
            => CreateDefaultControlTextBlock(Window, Item?.ToString() ?? string.Empty, false, false, false);

        public static MGElement CreateDefaultTreeViewItemHeaderContent(MGWindow Window, object Header)
            => new MGTextBlock(Window, Header?.ToString() ?? string.Empty);

        public static MGElement CreateDefaultTabHeaderContent(MGWindow Window, string Header)
            => new MGTextBlock(Window, Header ?? string.Empty);

        public static MGElement CreateDefaultListViewCellContent<TItemType>(MGWindow Window, TItemType Item)
            => new MGTextBlock(Window, Item?.ToString() ?? string.Empty);

        public static MGElement CreateDefaultCloseButtonContent(MGWindow Window)
        {
            MGCloseIcon closeIcon = new(Window)
            {
                Color = Color.White,
                PreferredWidth = 10,
                PreferredHeight = 10,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            return closeIcon;
        }

        public static void ApplyListBoxItemContainerDefaults(MGElement Owner, MGBorder Item)
        {
            if (Owner == null || Item == null)
            {
                return;
            }

            // ADR-0005/S3: this is the per-item container's own template default -- applied once, on demand, when
            // MGListBox materializes an item container (MGListBox.cs's ItemTemplate callback), the same role the
            // catalogue's Context.ApplyThemeDefault/ApplyTemplateValue calls play for the rest of the framework,
            // just without an MGControlTemplateContext (there is no per-item ApplyDefaults phase to hook into).
            MGTheme theme = Owner.GetTheme();
            Item.SetBorderBrush(CreateDefaultListBoxItemBorderBrush(), UIValueResolutionSource.Template(UIInvalidationKind.Draw, "ListBox.Item.BorderBrush"));
            Item.SetBorderThickness(DefaultListBoxItemBorderThickness, UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "ListBox.Item.BorderThickness"));
            Item.SetPadding(DefaultListBoxItemPadding, UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "ListBox.Item.Padding"));
            Item.SetBackground(theme.ListBoxItemBackground.GetValue(true), UIValueResolutionSource.Template(UIInvalidationKind.Draw, "ListBox.Item.Background"));
            Item.SetDefaultTextForegroundAll(theme.TextBlockFallbackForeground.GetValue(true).NormalValue, UIValueResolutionSource.Template(UIInvalidationKind.Draw, "ListBox.Item.TextForeground"));
        }

        public static void RegisterDefaults(MGResources Resources)
        {
            if (Resources == null)
            {
                return;
            }

            Register(Resources, CreateWindowTemplate());
            Register(Resources, CreateToolTipTemplate());
            Register(Resources, CreateOverlayTemplate());
            Register(Resources, ContextMenuTemplateName, ApplyContextMenuTemplate);
            Register(Resources, ContextMenuItemTemplateName, ApplyContextMenuItemTemplate);
            Register(Resources, CreateBuiltInXamlTemplate(ListBoxTemplateName, ApplyListBoxTemplate));
            Register(Resources, CreateBuiltInXamlTemplate(ListViewTemplateName, ApplyListViewTemplate));
            Register(Resources, CreatePropertyGridTemplate());
            Register(Resources, CreateGraphViewTemplate());
            Register(Resources, CreateGraphNodeTemplate());
            Register(Resources, CreateGraphPortTemplate());
            Register(Resources, CreateGraphCommentBoxTemplate());
            Register(Resources, CreateComboBoxTemplate());
            Register(Resources, ComboBoxDropdownItemTemplateName, ApplyComboBoxDropdownItemTemplate);
            Register(Resources, CreateTreeViewTemplate());
            Register(Resources, CreateTextBoxTemplate());
            Register(Resources, CreateNumericUpDownTemplate());
            Register(Resources, CreateTabControlTemplate());
            Register(Resources, SelectedTabHeaderTemplateName, ApplySelectedTabHeaderTemplate);
            Register(Resources, UnselectedTabHeaderTemplateName, ApplyUnselectedTabHeaderTemplate);
            Register(Resources, DockTabItemTemplateName, ApplyDockTabItemTemplate);
            Register(Resources, DockAutoHideDrawerTemplateName, ApplyDockAutoHideDrawerTemplate);
            Register(Resources, DockAutoHideStripTemplateName, ApplyDockAutoHideStripTemplate);
            Register(Resources, DockSplitterTemplateName, ApplyDockSplitterTemplate);
            Register(Resources, DockDropIndicatorsTemplateName, ApplyDockDropIndicatorsTemplate);

            IReadOnlyDictionary<string, MGControlTemplate> builtInXamlTemplates = ControlTemplateLoader.BuildTemplates(
                BuiltInXamlTemplateDefinitions.Value.Values,
                name => Resources.TryGetControlTemplate(name, out MGControlTemplate template) ? template : null);

            foreach (KeyValuePair<string, MGControlTemplate> item in builtInXamlTemplates)
            {
                if (!Resources.TryGetControlTemplate(item.Key, out _))
                {
                    Resources.AddControlTemplate(item.Value);
                }
            }
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

        private static MGControlTemplate CreateToolTipTemplate()
            => new(ToolTipTemplateName, CreateWindowTemplateStructure, null, ApplyToolTipTemplate);

        private static MGControlTemplate CreateOverlayTemplate()
            => new(OverlayTemplateName, CreateOverlayTemplateStructure, null, ApplyOverlayTemplate);

        private static MGControlTemplate CreateComboBoxTemplate()
            => new(ComboBoxTemplateName, CreateComboBoxTemplateStructure, null, ApplyComboBoxTemplate);

        private static MGControlTemplate CreatePropertyGridTemplate()
            => new(PropertyGridTemplateName, CreatePropertyGridTemplateStructure, null, ApplyPropertyGridTemplate);

        private static MGControlTemplate CreateGraphViewTemplate()
            => new(GraphViewTemplateName, CreateGraphViewTemplateStructure, null, ApplyGraphViewTemplate);

        private static MGControlTemplate CreateGraphNodeTemplate()
            => new(GraphNodeTemplateName, CreateGraphNodeTemplateStructure, null, ApplyGraphNodeTemplate);

        private static MGControlTemplate CreateGraphPortTemplate()
            => new(GraphPortTemplateName, CreateGraphPortTemplateStructure, null, ApplyGraphPortTemplate);

        private static MGControlTemplate CreateGraphCommentBoxTemplate()
            => new(GraphCommentBoxTemplateName, CreateGraphCommentBoxTemplateStructure, null, ApplyGraphCommentBoxTemplate);

        private static MGControlTemplate CreateTreeViewTemplate()
            => new(TreeViewTemplateName, CreateTreeViewTemplateStructure, null, ApplyTreeViewTemplate);

        private static MGControlTemplate CreateTextBoxTemplate()
            => new(TextBoxTemplateName, CreateTextBoxTemplateStructure, null, ApplyTextBoxTemplate);

        private static MGControlTemplate CreateNumericUpDownTemplate()
            => new(NumericUpDownTemplateName, CreateNumericUpDownTemplateStructure, null, ApplyNumericUpDownTemplate);

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
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch,
                DrawBackgroundEnabled = false,
            };
            titleBar.SetBackground(Window.GetTheme().TitleBackground.GetValue(true), UIValueResolutionSource.Template(UIInvalidationKind.Draw, "Window.TitleBar.Background"));
            titleBar.SetPadding(new(2), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "Window.TitleBar.Padding"));
            titleBar.SetMinHeight(24, UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "Window.TitleBar.MinHeight"));

            MGButton closeButton = new(Window, _ => Window.TryCloseWindow())
            {
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            MGTextBlock titleText = new(Window, null, Color.White, Window.GetTheme().FontSettings.SmallFontSize)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = HorizontalAlignment.Left,
            };
            // ADR-0005/S6: moved out of the object initializer so this is a tagged Template write instead of the
            // public setter's LocalValue, matching the other Template-sourced defaults below.
            titleText.SetDefaultTextForeground(new VisualStateSetting<Color?>(Color.White, Color.White, Color.White), UIValueResolutionSource.Template(UIInvalidationKind.Draw, "Window.TitleBarText.DefaultTextForeground"));
            titleText.SetMargin(new(4, 0), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "Window.TitleBarText.Margin"));
            titleText.SetPadding(new(0), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "Window.TitleBarText.Padding"));
            MGResizeGrip resizeGrip = new(Window);

            titleBar.TryAddChild(closeButton, Dock.Right);
            titleBar.TryAddChild(titleText, Dock.Left);
            titleBar.CanChangeContent = false;

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
            // No theme argument: the dropdown's Window scope inherits the owner window's scope, so it follows that scope's theme changes.
            MGWindow dropdown = new(window, 0, 0, 100, 300)
            {
                ManagedParent = owner,
                IsUserResizable = false,
                IsTitleBarVisible = false,
                Scale = window.Scale,
            };
            MGContentPresenter dropdownHeaderPresenter = new(dropdown);
            MGContentPresenter dropdownFooterPresenter = new(dropdown);
            MGStackPanel dropdownStackPanel = new(dropdown, Orientation.Vertical) { Spacing = 0, ManagedParent = dropdown };
            MGScrollViewer dropdownScrollViewer = new(dropdown, ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled) { ManagedParent = dropdown };
            dropdownScrollViewer.SetPadding(new(0), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "ComboBox.DropdownScrollViewer.Padding"));
            MGDockPanel dropdownDockPanel = new(dropdown, true);

            dropdown.SetBorderThicknessTagged(new(1), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "ComboBox.Dropdown.BorderThickness"));
            dropdown.SetBorderBrushTagged(MGUniformBorderBrush.Gray, UIValueResolutionSource.Template(UIInvalidationKind.Draw, "ComboBox.Dropdown.BorderBrush"));
            dropdown.SetBackground(owner.GetTheme().ComboBoxDropdownBackground.GetValue(true), UIValueResolutionSource.Template(UIInvalidationKind.Draw, "ComboBox.Dropdown.Background"));
            dropdown.SetPadding(new(0), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "ComboBox.Dropdown.Padding"));

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
            };
            MGScrollViewer scrollViewer = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            MGStackPanel itemsPanel = new(window, Orientation.Vertical)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                CanChangeContent = false,
            };
            UIInvalidationKind treeViewStructureInvalidation = UIInvalidationKind.Measure | UIInvalidationKind.Arrange;
            outerBorder.SetMargin(new Thickness(0), UIValueResolutionSource.Template(treeViewStructureInvalidation, "TreeView.OuterBorder.Margin"));
            outerBorder.SetPadding(new Thickness(0), UIValueResolutionSource.Template(treeViewStructureInvalidation, "TreeView.OuterBorder.Padding"));
            scrollViewer.SetMargin(new Thickness(0), UIValueResolutionSource.Template(treeViewStructureInvalidation, "TreeView.ScrollViewer.Margin"));
            scrollViewer.SetPadding(new Thickness(0), UIValueResolutionSource.Template(treeViewStructureInvalidation, "TreeView.ScrollViewer.Padding"));
            itemsPanel.SetMargin(new Thickness(0), UIValueResolutionSource.Template(treeViewStructureInvalidation, "TreeView.ItemsPanel.Margin"));
            itemsPanel.SetPadding(new Thickness(0), UIValueResolutionSource.Template(treeViewStructureInvalidation, "TreeView.ItemsPanel.Padding"));

            MGControlTemplateStructure structure = new(outerBorder);
            structure.AddPart(MGTreeView.OuterBorderPartName, outerBorder);
            structure.AddPart(MGTreeView.ScrollViewerPartName, scrollViewer);
            structure.AddPart(MGTreeView.ItemsPanelPartName, itemsPanel);
            return structure;
        }

        private static MGControlTemplateStructure CreatePropertyGridTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGPropertyGrid propertyGrid)
            {
                return null;
            }

            MGWindow window = propertyGrid.SelfOrParentWindow;
            MGBorder outerBorder = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            MGScrollViewer scrollViewer = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            MGStackPanel categoriesPanel = new(window, Orientation.Vertical)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Top,
                CanChangeContent = false,
            };
            UIInvalidationKind propertyGridStructureInvalidation = UIInvalidationKind.Measure | UIInvalidationKind.Arrange;
            outerBorder.SetMargin(new Thickness(0), UIValueResolutionSource.Template(propertyGridStructureInvalidation, "PropertyGrid.OuterBorder.Margin"));
            outerBorder.SetPadding(new Thickness(0), UIValueResolutionSource.Template(propertyGridStructureInvalidation, "PropertyGrid.OuterBorder.Padding"));
            scrollViewer.SetMargin(new Thickness(0), UIValueResolutionSource.Template(propertyGridStructureInvalidation, "PropertyGrid.ScrollViewer.Margin"));
            scrollViewer.SetPadding(new Thickness(0), UIValueResolutionSource.Template(propertyGridStructureInvalidation, "PropertyGrid.ScrollViewer.Padding"));
            categoriesPanel.SetMargin(new Thickness(0), UIValueResolutionSource.Template(propertyGridStructureInvalidation, "PropertyGrid.CategoriesPanel.Margin"));
            categoriesPanel.SetPadding(new Thickness(0), UIValueResolutionSource.Template(propertyGridStructureInvalidation, "PropertyGrid.CategoriesPanel.Padding"));

            MGControlTemplateStructure structure = new(outerBorder);
            structure.AddPart(MGPropertyGrid.OuterBorderPartName, outerBorder);
            structure.AddPart(MGPropertyGrid.ScrollViewerPartName, scrollViewer);
            structure.AddPart(MGPropertyGrid.CategoriesPanelPartName, categoriesPanel);
            return structure;
        }

        private static MGControlTemplateStructure CreateGraphViewTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGGraphView graphView)
            {
                return null;
            }

            MGWindow window = graphView.SelfOrParentWindow;
            MGBorder outerBorder = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            MGOverlayPanel viewportHost = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                ClipToBounds = true,
            };
            MGCanvas nodesCanvas = new MGGraphSurfaceCanvas(window, graphView)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            MGOverlayPanel overlayPanel = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            UIInvalidationKind graphViewStructureInvalidation = UIInvalidationKind.Measure | UIInvalidationKind.Arrange;
            outerBorder.SetMargin(new Thickness(0), UIValueResolutionSource.Template(graphViewStructureInvalidation, "GraphView.OuterBorder.Margin"));
            outerBorder.SetPadding(new Thickness(0), UIValueResolutionSource.Template(graphViewStructureInvalidation, "GraphView.OuterBorder.Padding"));
            viewportHost.SetMargin(new Thickness(0), UIValueResolutionSource.Template(graphViewStructureInvalidation, "GraphView.ViewportHost.Margin"));
            viewportHost.SetPadding(new Thickness(0), UIValueResolutionSource.Template(graphViewStructureInvalidation, "GraphView.ViewportHost.Padding"));
            nodesCanvas.SetMargin(new Thickness(0), UIValueResolutionSource.Template(graphViewStructureInvalidation, "GraphView.NodesCanvas.Margin"));
            nodesCanvas.SetPadding(new Thickness(0), UIValueResolutionSource.Template(graphViewStructureInvalidation, "GraphView.NodesCanvas.Padding"));
            overlayPanel.SetMargin(new Thickness(0), UIValueResolutionSource.Template(graphViewStructureInvalidation, "GraphView.OverlayPanel.Margin"));
            overlayPanel.SetPadding(new Thickness(0), UIValueResolutionSource.Template(graphViewStructureInvalidation, "GraphView.OverlayPanel.Padding"));

            MGControlTemplateStructure structure = new(outerBorder);
            structure.AddPart(MGGraphView.OuterBorderPartName, outerBorder);
            structure.AddPart(MGGraphView.ViewportHostPartName, viewportHost);
            structure.AddPart(MGGraphView.NodesCanvasPartName, nodesCanvas);
            structure.AddPart(MGGraphView.OverlayPanelPartName, overlayPanel);
            return structure;
        }

        private static MGControlTemplateStructure CreateGraphNodeTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGGraphNode graphNode)
            {
                return null;
            }

            MGWindow window = graphNode.SelfOrParentWindow;
            MGBorder outerBorder = new(window);
            MGStackPanel stack = new(window, Orientation.Vertical)
            {
                Spacing = 0,
                CanChangeContent = true,
            };
            MGTextBlock header = CreateDefaultControlTextBlock(window, graphNode.Title, false, true, true);
            MGGrid portsPanel = new(window)
            {
                CanChangeContent = false,
            };
            portsPanel.SetPadding(new Thickness(10, 6, 10, 8), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "GraphNode.PortsPanel.Padding"));
            portsPanel.AddColumn(GridLength.Auto);
            portsPanel.AddColumn(GridLength.CreateWeightedLength(1));
            MGContentPresenter bodyPresenter = new(window);
            bodyPresenter.SetPadding(new Thickness(10, 4, 10, 8), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "GraphNode.BodyPresenter.Padding"));

            stack.TryAddChild(header);
            stack.TryAddChild(portsPanel);
            stack.TryAddChild(bodyPresenter);
            stack.CanChangeContent = false;
            outerBorder.SetContent(stack);

            MGControlTemplateStructure structure = new(outerBorder);
            structure.AddPart(MGGraphNode.OuterBorderPartName, outerBorder);
            structure.AddPart(MGGraphNode.HeaderTextBlockPartName, header);
            structure.AddPart(MGGraphNode.PortsPanelPartName, portsPanel);
            structure.AddPart(MGGraphNode.BodyPresenterPartName, bodyPresenter);
            return structure;
        }

        private static MGControlTemplateStructure CreateGraphPortTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGGraphPort graphPort)
            {
                return null;
            }

            MGWindow window = graphPort.SelfOrParentWindow;
            MGBorder outerBorder = new(window);
            MGGrid layout = new(window)
            {
                ColumnSpacing = 0,
            };
            layout.AddRow(GridLength.Auto);
            layout.AddColumn(GridLength.CreatePixelLength(MGGraphPort.ConnectorSlotWidth));
            layout.AddColumn(GridLength.Auto);
            layout.AddColumn(GridLength.CreatePixelLength(MGGraphPort.ConnectorSlotWidth));
            MGContentPresenter leadingIconPresenter = new(window)
            {
                PreferredWidth = MGGraphPort.ConnectorSlotWidth,
                PreferredHeight = MGGraphPort.ConnectorIconSize,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false,
            };
            leadingIconPresenter.SetMargin(new Thickness(0), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "GraphPort.LeadingIconPresenter.Margin"));
            MGContentPresenter trailingIconPresenter = new(window)
            {
                PreferredWidth = MGGraphPort.ConnectorSlotWidth,
                PreferredHeight = MGGraphPort.ConnectorIconSize,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                IsHitTestVisible = false,
            };
            trailingIconPresenter.SetMargin(new Thickness(0), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "GraphPort.TrailingIconPresenter.Margin"));
            MGTextBlock label = CreateDefaultControlTextBlock(window, graphPort.PortName, false, true, true);
            label.IsHitTestVisible = false;
            layout.TryAddChild(0, 0, leadingIconPresenter);
            layout.TryAddChild(0, 1, label);
            layout.TryAddChild(0, 2, trailingIconPresenter);
            layout.CanChangeContent = false;
            outerBorder.SetContent(layout);

            MGControlTemplateStructure structure = new(outerBorder);
            structure.AddPart(MGGraphPort.OuterBorderPartName, outerBorder);
            structure.AddPart(MGGraphPort.LeadingIconPresenterPartName, leadingIconPresenter);
            structure.AddPart(MGGraphPort.TrailingIconPresenterPartName, trailingIconPresenter);
            structure.AddPart(MGGraphPort.LabelPartName, label);
            return structure;
        }

        private static MGControlTemplateStructure CreateGraphCommentBoxTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGGraphCommentBox commentBox)
            {
                return null;
            }

            MGWindow window = commentBox.SelfOrParentWindow;
            MGBorder outerBorder = new(window);
            MGStackPanel stack = new(window, Orientation.Vertical)
            {
                Spacing = 0,
                CanChangeContent = true,
            };
            MGTextBox title = new(window, 512);
            MGTextBox body = new(window, null);
            stack.TryAddChild(title);
            stack.TryAddChild(body);
            stack.CanChangeContent = false;
            outerBorder.SetContent(stack);

            MGControlTemplateStructure structure = new(outerBorder);
            structure.AddPart(MGGraphCommentBox.OuterBorderPartName, outerBorder);
            structure.AddPart(MGGraphCommentBox.TitleTextBoxPartName, title);
            structure.AddPart(MGGraphCommentBox.BodyTextBoxPartName, body);
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
            MGTextBlock placeholder = CreateDefaultControlTextBlock(window, string.Empty, true, false, true);
            placeholder.Visibility = Visibility.Collapsed;
            MGTextBlock characterCount = new(window, "0");
            characterCount.SetMargin(new(0, 0, 8, 4), UIValueResolutionSource.Template(UIInvalidationKind.Measure | UIInvalidationKind.Arrange, "TextBox.CharacterCount.Margin"));
            _ = characterCount.TrySetFont(window.Desktop.DefaultFontFamily, 9);
            MGTextBlock textBlock = CreateDefaultControlTextBlock(window, string.Empty, true, false, true);
            textBlock.ClipToBounds = false;

            MGControlTemplateStructure structure = new(null);
            structure.AddPart(MGTextBox.BorderPartName, border);
            structure.AddPart(MGTextBox.ResizeGripPartName, resizeGrip);
            structure.AddPart(MGTextBox.PlaceholderTextBlockPartName, placeholder);
            structure.AddPart(MGTextBox.CharacterCountPartName, characterCount);
            structure.AddPart(MGTextBox.TextBlockPartName, textBlock);
            return structure;
        }

        private static MGControlTemplateStructure CreateNumericUpDownTemplateStructure(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGNumericUpDown numericUpDown)
            {
                return null;
            }

            MGWindow window = numericUpDown.SelfOrParentWindow;
            MGBorder border = new(window);
            MGResizeGrip resizeGrip = new(window)
            {
                Visibility = Visibility.Collapsed,
            };
            MGTextBlock placeholder = CreateDefaultControlTextBlock(window, string.Empty, true, false, true);
            placeholder.Visibility = Visibility.Collapsed;
            MGTextBlock characterCount = new(window, "0")
            {
                Visibility = Visibility.Collapsed,
            };
            MGTextBlock textBlock = CreateDefaultControlTextBlock(window, string.Empty, true, false, true);
            textBlock.ClipToBounds = false;

            MGGrid spinnerHost = new(window);
            UIInvalidationKind numericUpDownStructureInvalidation = UIInvalidationKind.Measure | UIInvalidationKind.Arrange;
            spinnerHost.SetMargin(new Thickness(0), UIValueResolutionSource.Template(numericUpDownStructureInvalidation, "NumericUpDown.SpinnerHost.Margin"));
            spinnerHost.SetPadding(new Thickness(0), UIValueResolutionSource.Template(numericUpDownStructureInvalidation, "NumericUpDown.SpinnerHost.Padding"));
            MGUI.Core.UI.Containers.Grids.RowDefinition topRow = spinnerHost.AddRow(GridLength.CreateWeightedLength(1));
            MGUI.Core.UI.Containers.Grids.RowDefinition bottomRow = spinnerHost.AddRow(GridLength.CreateWeightedLength(1));
            MGUI.Core.UI.Containers.Grids.ColumnDefinition column = spinnerHost.AddColumn(GridLength.Auto);

            MGButton increaseButton = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            increaseButton.SetPadding(new Thickness(2, 0), UIValueResolutionSource.Template(numericUpDownStructureInvalidation, "NumericUpDown.IncreaseButton.Padding"));
            MGButton decreaseButton = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
            };
            decreaseButton.SetPadding(new Thickness(2, 0), UIValueResolutionSource.Template(numericUpDownStructureInvalidation, "NumericUpDown.DecreaseButton.Padding"));

            spinnerHost.TryAddChild(topRow, column, increaseButton);
            spinnerHost.TryAddChild(bottomRow, column, decreaseButton);

            MGControlTemplateStructure structure = new(null);
            structure.AddPart(MGTextBox.BorderPartName, border);
            structure.AddPart(MGTextBox.ResizeGripPartName, resizeGrip);
            structure.AddPart(MGTextBox.PlaceholderTextBlockPartName, placeholder);
            structure.AddPart(MGTextBox.CharacterCountPartName, characterCount);
            structure.AddPart(MGTextBox.TextBlockPartName, textBlock);
            structure.AddPart(MGNumericUpDown.SpinnerHostPartName, spinnerHost);
            structure.AddPart(MGNumericUpDown.IncreaseButtonPartName, increaseButton);
            structure.AddPart(MGNumericUpDown.DecreaseButtonPartName, decreaseButton);
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

            Context.ApplyOwnerThemeDefault("Window.Padding", Theme.Window.Padding, () => Window.Padding, (value, source) => Window.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("Window.BorderThickness", Theme.Window.BorderThickness, () => Border.BorderThickness, (value, source) => Border.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("Window.BorderBrush", Theme.Window.BorderBrush, () => Border.BorderBrush, (value, source) => Border.SetBorderBrush(value, source));
            Context.ApplyThemeDefault("Window.TitleBarPadding", Theme.Window.TitleBarPadding, () => TitleBar.Padding, (value, source) => TitleBar.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("Window.TitleBarMinHeight", Theme.Window.TitleBarMinHeight, () => TitleBar.MinHeight ?? 0, (value, source) => TitleBar.SetMinHeight(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("Window.TitleBarBackground", Theme.TitleBackground.GetValue(true), () => TitleBar.BackgroundBrush, (value, source) => TitleBar.SetBackground(value, source));

            Context.ApplyThemeDefault("Window.CloseButtonMinWidth", Theme.Window.CloseButtonMinWidth, () => CloseButton.MinWidth ?? 0, value => CloseButton.MinWidth = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("Window.CloseButtonMinHeight", Theme.Window.CloseButtonMinHeight, () => CloseButton.MinHeight ?? 0, (value, source) => CloseButton.SetMinHeight(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            // ADR-0005/S5: Theme.Window.CloseButtonBackground is a raw (un-cloned) theme-owned instance, unlike
            // GetBackgroundBrush's per-call clone -- SetBackground now subscribes to its container's PropertyChanged,
            // so handing every window's close button the SAME instance would accumulate one subscriber per window
            // ever created on this theme object, rooting them all. Copy it, like every other Background default here.
            Context.ApplyThemeDefault("Window.CloseButtonBackground", Theme.Window.CloseButtonBackground?.Copy(), () => CloseButton.BackgroundBrush, (value, source) => CloseButton.SetBackground(value, source));
            Context.ApplyThemeDefault("Window.CloseButtonBorderBrush", Theme.Window.CloseButtonBorderBrush, () => CloseButton.BorderBrush, (value, source) => CloseButton.SetBorderBrushTagged(value, source));
            Context.ApplyThemeDefault("Window.CloseButtonBorderThickness", Theme.Window.CloseButtonBorderThickness, () => CloseButton.BorderThickness, (value, source) => CloseButton.SetBorderThicknessTagged(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("Window.CloseButtonMargin", Theme.Window.CloseButtonMargin, () => CloseButton.Margin, (value, source) => CloseButton.SetMargin(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("Window.CloseButtonPadding", Theme.Window.CloseButtonPadding, () => CloseButton.Padding, (value, source) => CloseButton.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("Window.TitleTextMargin", Theme.Window.TitleTextMargin, () => TitleText.Margin, (value, source) => TitleText.SetMargin(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("Window.TitleTextPadding", Theme.Window.TitleTextPadding, () => TitleText.Padding, (value, source) => TitleText.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            // ADR-0005/S5-style copy: Theme.Window.TitleTextForeground is a raw (un-cloned) theme-owned instance --
            // see the Window.CloseButtonBackground comment above for why every subscriber needs its own copy.
            Context.ApplyThemeDefault("Window.TitleTextForeground", Theme.Window.TitleTextForeground?.GetCopy(), () => TitleText.DefaultTextForeground, (value, source) => TitleText.SetDefaultTextForeground(value, source));

            if (!Context.IsThemeRefresh && CloseButton.Content == null)
            {
                CloseButton.VerticalAlignment = VerticalAlignment.Center;
                CloseButton.VerticalContentAlignment = VerticalAlignment.Center;
                CloseButton.HorizontalContentAlignment = HorizontalAlignment.Center;
                TitleText.HorizontalAlignment = HorizontalAlignment.Stretch;
                TitleText.VerticalAlignment = VerticalAlignment.Center;
                TitleText.TextAlignment = HorizontalAlignment.Left;
                CloseButton.SetContent(CreateDefaultCloseButtonContent(Window));
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
            Context.ApplyOwnerThemeDefault("Overlay.Padding", Theme.Overlay.Padding, () => Overlay.Padding, (value, source) => Overlay.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("Overlay.BorderThickness", Theme.Overlay.BorderThickness, () => Border.BorderThickness, (value, source) => Border.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("Overlay.BorderBrush", Theme.Overlay.BorderBrush, () => Border.BorderBrush, (value, source) => Border.SetBorderBrush(value, source));
            Context.ApplyThemeDefault("Overlay.CloseButtonMinWidth", Theme.Overlay.CloseButtonMinWidth, () => CloseButton.MinWidth ?? 0, value => CloseButton.MinWidth = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("Overlay.CloseButtonMinHeight", Theme.Overlay.CloseButtonMinHeight, () => CloseButton.MinHeight ?? 0, (value, source) => CloseButton.SetMinHeight(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            // ADR-0005/S5: see the identical comment on "Window.CloseButtonBackground" above -- Theme.Overlay.CloseButtonBackground
            // is likewise raw/shared and must be copied per subscriber.
            Context.ApplyThemeDefault("Overlay.CloseButtonBackground", Theme.Overlay.CloseButtonBackground?.Copy(), () => CloseButton.BackgroundBrush, (value, source) => CloseButton.SetBackground(value, source));
            Context.ApplyThemeDefault("Overlay.CloseButtonBorderBrush", Theme.Overlay.CloseButtonBorderBrush, () => CloseButton.BorderBrush, (value, source) => CloseButton.SetBorderBrushTagged(value, source));
            Context.ApplyThemeDefault("Overlay.CloseButtonBorderThickness", Theme.Overlay.CloseButtonBorderThickness, () => CloseButton.BorderThickness, (value, source) => CloseButton.SetBorderThicknessTagged(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("Overlay.CloseButtonPadding", Theme.Overlay.CloseButtonPadding, () => CloseButton.Padding, (value, source) => CloseButton.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);

            if (!Context.IsThemeRefresh && CloseButton.Content == null)
            {
                CloseButton.SetContent(CreateDefaultCloseButtonContent(Overlay.Host.ParentWindow));
            }
        }

        private static void ApplyToolTipTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGToolTip ToolTip)
            {
                return;
            }

            ApplyWindowTemplate(Context);

            MGBorder Border = Context.GetRequiredPart<MGBorder>(MGWindow.BorderPartName);
            MGTheme Theme = ToolTip.GetTheme();

            Context.ApplyOwnerThemeDefault("ToolTip.BorderBrush", Color.Black.AsFillBrush().AsUniformBorderBrush(), () => Border.BorderBrush, (value, source) => Border.SetBorderBrush(value, source));
            Context.ApplyOwnerThemeDefault("ToolTip.BorderThickness", new Thickness(2), () => Border.BorderThickness, (value, source) => Border.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("ToolTip.Padding", new Thickness(6, 3), () => ToolTip.Padding, (value, source) => ToolTip.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ToolTip.DrawOffset", Theme.ToolTipOffset, () => ToolTip.DrawOffset, value => ToolTip.DrawOffset = value);
            Context.ApplyOwnerThemeDefault("ToolTip.TextForeground", Theme.ToolTipTextForeground.GetCopy(), () => ToolTip.DefaultTextForeground, (value, source) => ToolTip.SetDefaultTextForeground(value, source));
            Context.ApplyThemeDefault("ToolTip.MinWidth", 10, () => ToolTip.MinWidth ?? 0, value => ToolTip.MinWidth = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("ToolTip.MinHeight", 10, () => ToolTip.MinHeight ?? 0, (value, source) => ToolTip.SetMinHeight(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);

            if (!Context.IsThemeRefresh)
            {
                ToolTip.IsUserResizable = false;
                ToolTip.IsTitleBarVisible = false;
                ToolTip.IsCloseButtonVisible = false;
            }
        }

        private static void ApplyContextMenuTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGContextMenu Menu)
            {
                return;
            }

            MGTheme Theme = Menu.GetTheme();
            Context.ApplyOwnerThemeDefault("ContextMenu.Padding", Theme.ContextMenu.Padding, () => Menu.Padding, (value, source) => Menu.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("ContextMenu.BorderBrush", Theme.ContextMenu.BorderBrush, () => Menu.BorderBrush, (value, source) => Menu.SetBorderBrushTagged(value, source));
            Context.ApplyOwnerThemeDefault("ContextMenu.BorderThickness", Theme.ContextMenu.BorderThickness, () => Menu.BorderThickness, (value, source) => Menu.SetBorderThicknessTagged(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
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
            Context.ApplyThemeDefault("ContextMenuItem.HeaderMargin", Theme.ContextMenuItem.HeaderMargin, () => HeaderPresenter.Margin, (value, source) => HeaderPresenter.SetMargin(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            // PART_HeaderPresenter (icon / check-mark column) deliberately keeps its empty construction background: the row wrapper
            // (MGContextMenu.CreateDefaultDropdownButton) already paints ContextMenuItem.HeaderBackground behind it, so applying the same
            // brush here would stack a second highlight layer on the column.
            Context.ApplyThemeDefault("ContextMenuItem.ShortcutMargin", Theme.ContextMenuItem.ShortcutMargin, () => ShortcutText.Margin, (value, source) => ShortcutText.SetMargin(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            // ADR-0005/S5-style copy: Theme.ContextMenuItem.ShortcutForeground is raw/shared -- see the
            // Window.CloseButtonBackground comment above.
            Context.ApplyThemeDefault("ContextMenuItem.ShortcutForeground", Theme.ContextMenuItem.ShortcutForeground?.GetCopy(), () => ShortcutText.Foreground, (value, source) => ShortcutText.SetForeground(value, source));
            Context.ApplyThemeDefault("ContextMenuItem.SubmenuArrowMargin", Theme.ContextMenuItem.SubmenuArrowMargin, () => Arrow.Margin, (value, source) => Arrow.SetMargin(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
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

            Context.ApplyOwnerThemeDefault("ListBox.MinHeight", Theme.ListBox.MinHeight, () => Context.Owner.MinHeight ?? 0, (value, source) => Context.Owner.SetMinHeight(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            // ADR-0005/S5: Theme.ListBox.OuterBackground is raw/shared -- copy per subscriber (see the
            // Window.CloseButtonBackground comment above).
            Context.ApplyThemeDefault("ListBox.OuterBackground", Theme.ListBox.OuterBackground?.Copy(), () => OuterBorder.BackgroundBrush, (value, source) => OuterBorder.SetBackground(value, source));
            Context.ApplyThemeDefault("ListBox.TitlePadding", Theme.ListBox.TitlePadding, () => TitleBorder.Padding, (value, source) => TitleBorder.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ListBox.TitleBackground", Theme.TitleBackground.GetValue(true), () => TitleBorder.BackgroundBrush, (value, source) => TitleBorder.SetBackground(value, source));
            // ADR-0005/S5-style copy: Theme.ListBox.TitleForeground is raw/shared -- see the
            // Window.CloseButtonBackground comment above.
            Context.ApplyThemeDefault("ListBox.TitleForeground", Theme.ListBox.TitleForeground?.GetCopy(), () => TitleBorder.DefaultTextForeground, (value, source) => TitleBorder.SetDefaultTextForeground(value, source));
            Context.ApplyThemeDefault("ListBox.TitleBorderBrush", Theme.ListBox.TitleBorderBrush, () => TitleBorder.BorderBrush, (value, source) => TitleBorder.SetBorderBrush(value, source));
            Context.ApplyThemeDefault("ListBox.TitleBorderThickness", Theme.ListBox.TitleBorderThickness, () => TitleBorder.BorderThickness, (value, source) => TitleBorder.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ListBox.InnerBorderBrush", Theme.ListBox.InnerBorderBrush, () => InnerBorder.BorderBrush, (value, source) => InnerBorder.SetBorderBrush(value, source));
            Context.ApplyThemeDefault("ListBox.InnerBorderThickness", Theme.ListBox.InnerBorderThickness, () => InnerBorder.BorderThickness, (value, source) => InnerBorder.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ListBox.ScrollViewerPadding", Theme.ListBox.ScrollViewerPadding, () => ScrollViewer.Padding, (value, source) => ScrollViewer.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ListBox.ItemsPanelBorderBrush", Theme.ListBox.ItemsPanelBorderBrush, () => ItemsPanel.BorderBrush, (value, source) => ItemsPanel.SetBorderBrushTagged(value, source));
            Context.ApplyThemeDefault("ListBox.ItemsPanelBorderThickness", Theme.ListBox.ItemsPanelBorderThickness, () => ItemsPanel.BorderThickness, (value, source) => ItemsPanel.SetBorderThicknessTagged(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("ListBox.ItemsPanelVerticalAlignment", VerticalAlignment.Top, () => ItemsPanel.VerticalAlignment, value => ItemsPanel.VerticalAlignment = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("ListBox.TitlePresenterVerticalAlignment", VerticalAlignment.Center, () => TitlePresenter.VerticalAlignment, value => TitlePresenter.VerticalAlignment = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
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
            MGBorder HeaderSpacer = Context.TryGetPart(MGListView<object>.HeaderSpacerPartName, out MGElement headerSpacerPart) ? headerSpacerPart as MGBorder : null;
            IMGListViewTemplateDefaults defaults = Context.Owner as IMGListViewTemplateDefaults;
            int spacing = defaults?.TemplateDefaultSpacing ?? 8;
            int gridLineMargin = defaults?.TemplateDefaultGridLineMargin ?? 3;

            Context.ApplyTemplateValue("ListView.HeaderGridLinesVisibility", GridLinesVisibility.All, () => HeaderGrid.GridLinesVisibility, value => HeaderGrid.GridLinesVisibility = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("ListView.HeaderGridSpacing", spacing, () => HeaderGrid.RowSpacing, value =>
            {
                HeaderGrid.RowSpacing = value;
                HeaderGrid.ColumnSpacing = value;
            }, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("ListView.HeaderGridLineMargin", gridLineMargin, () => HeaderGrid.GridLineMargin, value => HeaderGrid.GridLineMargin = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("ListView.DataGridLinesVisibility", GridLinesVisibility.AllVertical | GridLinesVisibility.InnerHorizontal | GridLinesVisibility.BottomEdge,
                () => DataGrid.GridLinesVisibility, value => DataGrid.GridLinesVisibility = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("ListView.DataGridPadding", new Thickness(0, gridLineMargin, 0, 0), () => DataGrid.Padding, (value, source) => DataGrid.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("ListView.DataGridSpacing", spacing, () => DataGrid.RowSpacing, value =>
            {
                DataGrid.RowSpacing = value;
                DataGrid.ColumnSpacing = value;
            }, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("ListView.DataGridLineMargin", gridLineMargin, () => DataGrid.GridLineMargin, value => DataGrid.GridLineMargin = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ListView.HeaderBackground", Theme.TitleBackground.GetValue(true), () => HeaderGrid.BackgroundBrush, (value, source) => HeaderGrid.SetBackground(value, source));
            // ADR-0005/S5-style copy: Theme.ListView.HeaderForeground is raw/shared -- see the
            // Window.CloseButtonBackground comment above.
            Context.ApplyThemeDefault("ListView.HeaderForeground", Theme.ListView.HeaderForeground?.GetCopy(), () => HeaderGrid.DefaultTextForeground, (value, source) => HeaderGrid.SetDefaultTextForeground(value, source));
            Context.ApplyThemeDefault("ListView.HeaderHorizontalGridLineBrush", Theme.ListView.GridLineBrush, () => HeaderGrid.HorizontalGridLineBrush, value => HeaderGrid.HorizontalGridLineBrush = value);
            Context.ApplyThemeDefault("ListView.HeaderVerticalGridLineBrush", Theme.ListView.GridLineBrush, () => HeaderGrid.VerticalGridLineBrush, value => HeaderGrid.VerticalGridLineBrush = value);
            Context.ApplyThemeDefault("ListView.DataHorizontalGridLineBrush", Theme.ListView.GridLineBrush, () => DataGrid.HorizontalGridLineBrush, value => DataGrid.HorizontalGridLineBrush = value);
            Context.ApplyThemeDefault("ListView.DataVerticalGridLineBrush", Theme.ListView.GridLineBrush, () => DataGrid.VerticalGridLineBrush, value => DataGrid.VerticalGridLineBrush = value);

            if (HeaderSpacer != null)
            {
                int borderThickness = Math.Max(0, spacing - gridLineMargin * 2);
                Context.ApplyTemplateValue("ListView.HeaderSpacerBorderThickness", new Thickness(0, borderThickness, borderThickness, borderThickness),
                    () => HeaderSpacer.BorderThickness, (value, source) => HeaderSpacer.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
                Context.ApplyThemeDefault("ListView.HeaderSpacerBorderBrush", Theme.ListView.GridLineBrush.AsUniformBorderBrush(), () => HeaderSpacer.BorderBrush, (value, source) => HeaderSpacer.SetBorderBrush(value, source));
                Context.ApplyThemeDefault("ListView.HeaderSpacerBackground", Theme.TitleBackground.GetValue(true), () => HeaderSpacer.BackgroundBrush, (value, source) => HeaderSpacer.SetBackground(value, source));
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

            Context.ApplyOwnerThemeDefault("ComboBox.Background", Theme.GetBackgroundBrush(MGElementType.ComboBox), () => Context.Owner.BackgroundBrush, (value, source) => Context.Owner.SetBackground(value, source));
            Context.ApplyOwnerThemeDefault("ComboBox.Padding", Theme.ComboBox.Padding, () => Context.Owner.Padding, (value, source) => Context.Owner.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("ComboBox.MinHeight", Theme.ComboBox.MinHeight, () => Context.Owner.MinHeight ?? 0, (value, source) => Context.Owner.SetMinHeight(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownArrowColor", Theme.DropdownArrowColor,
                () => (Color)Context.Owner.GetType().GetProperty(nameof(MGComboBox<object>.DropdownArrowColor)).GetValue(Context.Owner),
                value => Context.Owner.GetType().GetProperty(nameof(MGComboBox<object>.DropdownArrowColor)).SetValue(Context.Owner, value));
            Context.ApplyOwnerThemeDefault("ComboBox.BorderBrush", Theme.ComboBox.BorderBrush, () => Border.BorderBrush, (value, source) => Border.SetBorderBrush(value, source));
            Context.ApplyThemeDefault("ComboBox.DropdownArrowMargin", Theme.ComboBox.DropdownArrowMargin, () => DropdownArrow.Margin, (value, source) => DropdownArrow.SetMargin(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownBorderThickness", Theme.ComboBox.DropdownBorderThickness, () => Dropdown.BorderThickness, (value, source) => Dropdown.SetBorderThicknessTagged(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownBorderBrush", Theme.ComboBox.DropdownBorderBrush, () => Dropdown.BorderBrush, (value, source) => Dropdown.SetBorderBrushTagged(value, source));
            Context.ApplyThemeDefault("ComboBox.DropdownBackground", Theme.ComboBoxDropdownBackground.GetValue(true), () => Dropdown.BackgroundBrush, (value, source) => Dropdown.SetBackground(value, source));
            Context.ApplyThemeDefault("ComboBox.DropdownPadding", Theme.ComboBox.DropdownPadding, () => Dropdown.Padding, (value, source) => Dropdown.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownScrollPadding", Theme.ComboBox.DropdownScrollViewerPadding, () => DropdownScrollViewer.Padding, (value, source) => DropdownScrollViewer.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownItemsSpacing", Theme.ComboBox.DropdownItemsSpacing, () => DropdownItemsPanel.Spacing, value => DropdownItemsPanel.Spacing = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);

            if (!Context.IsThemeRefresh)
            {
                Dropdown.PreferredWidth = Math.Max(Dropdown.PreferredWidth ?? 0, Theme.ComboBox.DropdownMinWidth);
            }
        }

        private static bool TryGetComboBoxDropdownItemOwner(MGButton Button, out MGElement Owner)
        {
            if (Button?.Metadata?.TryGetValue(MGComboBox<object>.DropdownItemTemplateOwnerMetadataKey, out object owner) == true
                && owner is MGElement typedOwner)
            {
                Owner = typedOwner;
                return true;
            }

            Owner = null;
            return false;
        }

        private static void ApplyComboBoxDropdownItemTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGButton Button || !TryGetComboBoxDropdownItemOwner(Button, out MGElement Owner))
            {
                return;
            }

            MGTheme theme = Owner.GetTheme();
            if (theme == null)
            {
                return;
            }

            Context.ApplyThemeDefault("ComboBox.DropdownItem.Padding", DefaultComboBoxDropdownItemPadding, () => Button.Padding, (value, source) => Button.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownItem.Margin", new Thickness(0), () => Button.Margin, (value, source) => Button.SetMargin(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownItem.Background", theme.ComboBoxDropdownItemBackground.GetValue(true), () => Button.BackgroundBrush, (value, source) => Button.SetBackground(value, source));
            Context.ApplyThemeDefault("ComboBox.DropdownItem.Foreground", theme.TextBlockFallbackForeground.GetValue(true).NormalValue, () => Button.DefaultTextForeground.NormalValue, (value, source) => Button.SetDefaultTextForegroundAll(value, source));
            Context.ApplyThemeDefault("ComboBox.DropdownItem.HorizontalAlignment", HorizontalAlignment.Stretch, () => Button.HorizontalAlignment, value => Button.HorizontalAlignment = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownItem.HorizontalContentAlignment", HorizontalAlignment.Left, () => Button.HorizontalContentAlignment, value => Button.HorizontalContentAlignment = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownItem.VerticalAlignment", VerticalAlignment.Stretch, () => Button.VerticalAlignment, value => Button.VerticalAlignment = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("ComboBox.DropdownItem.VerticalContentAlignment", VerticalAlignment.Center, () => Button.VerticalContentAlignment, value => Button.VerticalContentAlignment = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
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

            Context.ApplyOwnerThemeDefault("TreeView.BorderBrush", Theme?.TreeViewBorderBrush ?? MGUniformBorderBrush.Black, () => OuterBorder.BorderBrush, (value, source) => OuterBorder.SetBorderBrushTagged(value, source));
            Context.ApplyOwnerThemeDefault("TreeView.BorderThickness", Theme?.TreeViewBorderThickness ?? new Thickness(1), () => OuterBorder.BorderThickness, (value, source) => OuterBorder.SetBorderThicknessTagged(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("TreeView.ScrollViewerPadding", Theme.TreeViewTemplate.ScrollViewerPadding, () => ScrollViewer.Padding, (value, source) => ScrollViewer.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("TreeView.ItemsPanelPadding", Theme.TreeViewTemplate.ItemsPanelPadding, () => ItemsPanel.Padding, (value, source) => ItemsPanel.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("TreeView.ItemsPanelSpacing", Theme.TreeViewTemplate.ItemsPanelSpacing, () => ItemsPanel.Spacing, value => ItemsPanel.Spacing = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            VisualStateFillBrush SelectionBrush = Theme?.TreeViewSelectionBackground?.GetValue(true);
            Context.ApplyThemeDefault("TreeView.SelectionBackgroundBrush", SelectionBrush ?? new VisualStateFillBrush(new MGSolidFillBrush(Color.LightBlue)), () => TreeView.SelectionBackgroundBrush, value => TreeView.SelectionBackgroundBrush = value);
            Context.ApplyThemeDefault("TreeView.SelectionForeground", Theme?.TreeViewSelectionForeground ?? Color.Black, () => TreeView.SelectionForeground, value => TreeView.SelectionForeground = value);
            Context.ApplyThemeDefault("TreeView.IndentSize", Theme?.TreeViewIndentSize ?? DefaultTreeViewIndentSize, () => TreeView.IndentSize, value => TreeView.IndentSize = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
        }

        private static void ApplyPropertyGridTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGPropertyGrid propertyGrid)
            {
                return;
            }

            MGTheme theme = propertyGrid.GetTheme();
            MGBorder outerBorder = Context.GetRequiredPart<MGBorder>(MGPropertyGrid.OuterBorderPartName);
            MGScrollViewer scrollViewer = Context.GetRequiredPart<MGScrollViewer>(MGPropertyGrid.ScrollViewerPartName);
            MGStackPanel categoriesPanel = Context.GetRequiredPart<MGStackPanel>(MGPropertyGrid.CategoriesPanelPartName);

            Context.ApplyOwnerThemeDefault("PropertyGrid.Background", theme.GetBackgroundBrush(MGElementType.PropertyGrid), () => propertyGrid.BackgroundBrush, (value, source) => propertyGrid.SetBackground(value, source));
            Context.ApplyOwnerThemeDefault("PropertyGrid.Padding", theme.PropertyGrid.Padding, () => propertyGrid.Padding, (value, source) => propertyGrid.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            // ADR-0005/S7a: the PropertyGrid XAML DTO applies its nested Border DTO to propertyGrid.OuterBorder, so a
            // XAML attribute or style writing BorderBrush/BorderThickness lands on this very border: these two
            // defaults are the control's own chrome (Theme), not a part's (Template), even though MGPropertyGrid has
            // no GetBorder() override.
            Context.ApplyOwnerThemeDefault("PropertyGrid.BorderBrush", theme.PropertyGrid.BorderBrush, () => outerBorder.BorderBrush, (value, source) => outerBorder.SetBorderBrush(value, source));
            Context.ApplyOwnerThemeDefault("PropertyGrid.BorderThickness", theme.PropertyGrid.BorderThickness, () => outerBorder.BorderThickness, (value, source) => outerBorder.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("PropertyGrid.ScrollViewerPadding", theme.PropertyGrid.ScrollViewerPadding, () => scrollViewer.Padding, (value, source) => scrollViewer.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("PropertyGrid.CategoriesSpacing", theme.PropertyGrid.CategoriesSpacing, () => categoriesPanel.Spacing, value => categoriesPanel.Spacing = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("PropertyGrid.CategoriesPanelVerticalAlignment", VerticalAlignment.Top, () => categoriesPanel.VerticalAlignment, value => categoriesPanel.VerticalAlignment = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
        }

        private static void ApplyGraphViewTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGGraphView graphView)
            {
                return;
            }

            MGTheme theme = graphView.GetTheme();
            MGBorder outerBorder = Context.GetRequiredPart<MGBorder>(MGGraphView.OuterBorderPartName);
            MGOverlayPanel viewportHost = Context.GetRequiredPart<MGOverlayPanel>(MGGraphView.ViewportHostPartName);
            MGCanvas nodesCanvas = Context.GetRequiredPart<MGCanvas>(MGGraphView.NodesCanvasPartName);

            Context.ApplyOwnerThemeDefault("GraphView.Padding", theme.Graph.Padding, () => graphView.Padding, (value, source) => graphView.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("GraphView.BorderBrush", theme.Graph.BorderBrush, () => outerBorder.BorderBrush, (value, source) => outerBorder.SetBorderBrush(value, source));
            Context.ApplyThemeDefault("GraphView.BorderThickness", theme.Graph.BorderThickness, () => outerBorder.BorderThickness, (value, source) => outerBorder.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("GraphView.ViewportClipToBounds", true, () => viewportHost.ClipToBounds, value => viewportHost.ClipToBounds = value);
            // ADR-0005/S5: theme.Graph.CanvasBackground is raw/shared -- copy per subscriber (see the
            // Window.CloseButtonBackground comment above).
            Context.ApplyThemeDefault("GraphView.NodesCanvasBackground", theme.Graph.CanvasBackground?.Copy(), () => nodesCanvas.BackgroundBrush, (value, source) => nodesCanvas.SetBackground(value, source));
            Context.ApplyThemeDefault("GraphView.GridLineBrush", theme.Graph.GridLineBrush, () => graphView.GridLineBrush, value => graphView.GridLineBrush = value);
            Context.ApplyThemeDefault("GraphView.MajorGridLineBrush", theme.Graph.MajorGridLineBrush, () => graphView.MajorGridLineBrush, value => graphView.MajorGridLineBrush = value);
            Context.ApplyThemeDefault("GraphView.EdgeBrush", theme.Graph.EdgeBrush, () => graphView.EdgeBrush, value => graphView.EdgeBrush = value);
        }

        private static void ApplyGraphNodeTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGGraphNode graphNode)
            {
                return;
            }

            MGTheme theme = graphNode.GetTheme();
            MGBorder outerBorder = Context.GetRequiredPart<MGBorder>(MGGraphNode.OuterBorderPartName);
            MGTextBlock header = Context.GetRequiredPart<MGTextBlock>(MGGraphNode.HeaderTextBlockPartName);
            MGContentPresenter bodyPresenter = Context.GetRequiredPart<MGContentPresenter>(MGGraphNode.BodyPresenterPartName);

            // ADR-0005/S5: theme.Graph.NodeBodyBackground is raw/shared and read here for TWO different elements
            // (outerBorder, bodyPresenter) -- each needs its own copy (see the Window.CloseButtonBackground comment above).
            Context.ApplyThemeDefault("GraphNode.Background", theme.Graph.NodeBodyBackground?.Copy(), () => outerBorder.BackgroundBrush, (value, source) => outerBorder.SetBackground(value, source));
            Context.ApplyThemeDefault("GraphNode.BorderBrush", theme.Graph.NodeBorderBrush, () => outerBorder.BorderBrush, (value, source) => outerBorder.SetBorderBrush(value, source));
            Context.ApplyThemeDefault("GraphNode.BorderThickness", theme.Graph.NodeBorderThickness, () => outerBorder.BorderThickness, (value, source) => outerBorder.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("GraphNode.CornerRadius", new MGCornerRadius(8), () => outerBorder.CornerRadius, value => outerBorder.CornerRadius = value);
            Context.ApplyTemplateValue("GraphNode.ClipToBounds", true, () => outerBorder.ClipToBounds, value => outerBorder.ClipToBounds = value);
            Context.ApplyTemplateValue("GraphNode.Padding", new Thickness(0), () => outerBorder.Padding, (value, source) => outerBorder.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            // ADR-0005/S5: theme.Graph.NodeHeaderBackground is raw/shared -- copy per subscriber (see the
            // Window.CloseButtonBackground comment above).
            Context.ApplyThemeDefault("GraphNode.HeaderBackground", theme.Graph.NodeHeaderBackground?.Copy(), () => header.BackgroundBrush, (value, source) => header.SetBackground(value, source));
            Context.ApplyThemeDefault("GraphNode.HeaderForeground", ToTextForeground(theme.Graph.NodeHeaderForeground), () => header.DefaultTextForeground, (value, source) => header.SetDefaultTextForeground(value, source));
            Context.ApplyThemeDefault("GraphNode.HeaderPadding", new Thickness(10, 6), () => header.Padding, (value, source) => header.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("GraphNode.BodyBackground", theme.Graph.NodeBodyBackground?.Copy(), () => bodyPresenter.BackgroundBrush, (value, source) => bodyPresenter.SetBackground(value, source));
        }

        private static void ApplyGraphPortTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGGraphPort graphPort)
            {
                return;
            }

            MGTheme theme = graphPort.GetTheme();
            MGBorder outerBorder = Context.GetRequiredPart<MGBorder>(MGGraphPort.OuterBorderPartName);
            MGTextBlock label = Context.GetRequiredPart<MGTextBlock>(MGGraphPort.LabelPartName);

            Context.ApplyThemeDefault("GraphPort.Background", new VisualStateFillBrush(Color.Transparent.AsFillBrush()), () => outerBorder.BackgroundBrush, (value, source) => outerBorder.SetBackground(value, source));
            Context.ApplyThemeDefault("GraphPort.BorderBrush", theme.Graph.NodeBorderBrush, () => outerBorder.BorderBrush, (value, source) => outerBorder.SetBorderBrush(value, source));
            Context.ApplyThemeDefault("GraphPort.BorderThickness", new Thickness(0), () => outerBorder.BorderThickness, (value, source) => outerBorder.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("GraphPort.Padding", new Thickness(0, 2), () => outerBorder.Padding, (value, source) => outerBorder.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("GraphPort.Foreground", ToTextForeground(theme.Graph.PortForeground), () => label.DefaultTextForeground, (value, source) => label.SetDefaultTextForeground(value, source));
            Context.ApplyTemplateValue("GraphPort.HorizontalAlignment", HorizontalAlignment.Stretch, () => graphPort.HorizontalAlignment, value => graphPort.HorizontalAlignment = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
        }

        private static void ApplyGraphCommentBoxTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGGraphCommentBox commentBox)
            {
                return;
            }

            MGTheme theme = commentBox.GetTheme();
            MGBorder outerBorder = Context.GetRequiredPart<MGBorder>(MGGraphCommentBox.OuterBorderPartName);
            MGTextBox title = Context.GetRequiredPart<MGTextBox>(MGGraphCommentBox.TitleTextBoxPartName);
            MGTextBox body = Context.GetRequiredPart<MGTextBox>(MGGraphCommentBox.BodyTextBoxPartName);

            // ADR-0005/S5: theme.Graph.CommentBackground is raw/shared -- copy per subscriber (see the
            // Window.CloseButtonBackground comment above).
            Context.ApplyThemeDefault("GraphCommentBox.Background", theme.Graph.CommentBackground?.Copy(), () => outerBorder.BackgroundBrush, (value, source) => outerBorder.SetBackground(value, source));
            Context.ApplyThemeDefault("GraphCommentBox.BorderBrush", theme.Graph.CommentBorderBrush, () => outerBorder.BorderBrush, (value, source) => outerBorder.SetBorderBrush(value, source));
            Context.ApplyThemeDefault("GraphCommentBox.BorderThickness", new Thickness(1), () => outerBorder.BorderThickness, (value, source) => outerBorder.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("GraphCommentBox.Padding", new Thickness(8, 6), () => outerBorder.Padding, (value, source) => outerBorder.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("GraphCommentBox.TitleForeground", ToTextForeground(theme.Graph.NodeHeaderForeground), () => title.DefaultTextForeground, (value, source) => title.SetDefaultTextForeground(value, source));
            Context.ApplyThemeDefault("GraphCommentBox.BodyForeground", ToTextForeground(theme.Graph.PortForeground), () => body.DefaultTextForeground, (value, source) => body.SetDefaultTextForeground(value, source));
        }

        private static VisualStateSetting<Color?> ToTextForeground(VisualStateColorBrush brush)
        {
            if (brush == null)
            {
                return new VisualStateSetting<Color?>(null, null, null, null);
            }

            return new VisualStateSetting<Color?>(brush.NormalValue, brush.SelectedValue, brush.FocusedValue, brush.DisabledValue);
        }

        private static void ApplyTextBoxTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGTextBox textBox)
            {
                return;
            }

            MGTheme theme = textBox.GetTheme();
            Context.ApplyOwnerThemeDefault("TextBox.Background", theme.GetBackgroundBrush(MGElementType.TextBox), () => textBox.BackgroundBrush, (value, source) => textBox.SetBackground(value, source));
            Context.ApplyOwnerThemeDefault("TextBox.Padding", new Thickness(6, 1, 6, 1), () => textBox.Padding, (value, source) => textBox.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("TextBox.MinHeight", 24, () => textBox.MinHeight ?? 0, (value, source) => textBox.SetMinHeight(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("TextBox.FocusedSelectionForeground", theme.TextBoxFocusedSelectionForeground, () => textBox.FocusedSelectionForegroundColor, value => textBox.FocusedSelectionForegroundColor = value);
            Context.ApplyThemeDefault("TextBox.FocusedSelectionBackground", theme.TextBoxFocusedSelectionBackground, () => textBox.FocusedSelectionBackgroundColor, value => textBox.FocusedSelectionBackgroundColor = value);
            Context.ApplyThemeDefault("TextBox.UnfocusedSelectionForeground", theme.TextBoxUnfocusedSelectionForeground, () => textBox.UnfocusedSelectionForegroundColor, value => textBox.UnfocusedSelectionForegroundColor = value);
            Context.ApplyThemeDefault("TextBox.UnfocusedSelectionBackground", theme.TextBoxUnfocusedSelectionBackground, () => textBox.UnfocusedSelectionBackgroundColor, value => textBox.UnfocusedSelectionBackgroundColor = value);
        }

        private static void ApplyNumericUpDownTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGNumericUpDown numericUpDown)
            {
                return;
            }

            ApplyTextBoxTemplate(Context);

            MGGrid spinnerHost = Context.GetRequiredPart<MGGrid>(MGNumericUpDown.SpinnerHostPartName);
            MGButton increaseButton = Context.GetRequiredPart<MGButton>(MGNumericUpDown.IncreaseButtonPartName);
            MGButton decreaseButton = Context.GetRequiredPart<MGButton>(MGNumericUpDown.DecreaseButtonPartName);

            Context.ApplyOwnerThemeDefault("NumericUpDown.Padding", new Thickness(6, 2, 6, 2), () => numericUpDown.Padding, (value, source) => numericUpDown.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("NumericUpDown.MinHeight", 28, () => numericUpDown.MinHeight ?? 0, (value, source) => numericUpDown.SetMinHeight(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("NumericUpDown.SpinnerWidth", 24, () => spinnerHost.PreferredWidth ?? 0, value => spinnerHost.PreferredWidth = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("NumericUpDown.SpinnerMinWidth", 22, () => increaseButton.MinWidth ?? 0, value =>
            {
                increaseButton.MinWidth = value;
                decreaseButton.MinWidth = value;
            }, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyTemplateValue("NumericUpDown.SpinnerButtonPadding", new Thickness(0), () => increaseButton.Padding, (value, source) =>
            {
                increaseButton.SetPadding(value, source);
                decreaseButton.SetPadding(value, source);
            }, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);

            if (!Context.IsThemeRefresh)
            {
                if (increaseButton.Content == null)
                {
                    increaseButton.SetContent(CreateNumericSpinnerGlyph(Context.Window, UITriangleArrowDirection.Up));
                }

                if (decreaseButton.Content == null)
                {
                    decreaseButton.SetContent(CreateNumericSpinnerGlyph(Context.Window, UITriangleArrowDirection.Down));
                }
            }

            if (increaseButton.Content is MGTriangleArrowIcon increaseIcon)
            {
                increaseIcon.Color = numericUpDown.GetTheme().DropdownArrowColor;
            }

            if (decreaseButton.Content is MGTriangleArrowIcon decreaseIcon)
            {
                decreaseIcon.Color = numericUpDown.GetTheme().DropdownArrowColor;
            }
        }

        private static MGTriangleArrowIcon CreateNumericSpinnerGlyph(MGWindow window, UITriangleArrowDirection direction)
        {
            Color glyphColor = window.GetTheme()?.DropdownArrowColor ?? Color.White;

            return new(window)
            {
                Direction = direction,
                Color = glyphColor,
                PreferredWidth = 8,
                PreferredHeight = 5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
        }

        private static MGTextBlock CreateCompactSingleLineTextBlock(MGWindow window, string text)
            => CreateDefaultControlTextBlock(window, text, false, true, true);

        private static void ApplyTabControlTemplate(MGControlTemplateContext Context)
        {
            if (Context.Owner is not MGTabControl TabControl)
            {
                return;
            }

            MGTheme Theme = TabControl.GetTheme();
            MGBorder Border = Context.GetRequiredPart<MGBorder>(MGTabControl.BorderPartName);
            MGStackPanel HeadersPanel = Context.GetRequiredPart<MGStackPanel>(MGTabControl.HeadersPanelPartName);

            Context.ApplyOwnerThemeDefault("TabControl.Background", Theme.GetBackgroundBrush(MGElementType.TabControl), () => TabControl.BackgroundBrush, (value, source) => TabControl.SetBackground(value, source));
            Context.ApplyOwnerThemeDefault("TabControl.Padding", Theme.TabControl.Padding, () => TabControl.Padding, (value, source) => TabControl.SetPadding(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyOwnerThemeDefault("TabControl.BorderBrush", Theme.TabControl.BorderBrush, () => Border.BorderBrush, (value, source) => Border.SetBorderBrush(value, source));
            Context.ApplyOwnerThemeDefault("TabControl.BorderThickness", Theme.TabControl.BorderThickness, () => Border.BorderThickness, (value, source) => Border.SetBorderThickness(value, source), UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("TabControl.HeadersSpacing", Theme.TabControl.HeadersSpacing, () => HeadersPanel.Spacing, value => HeadersPanel.Spacing = value, UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("TabControl.HeadersBackground", Theme.TitleBackground.GetValue(true), () => HeadersPanel.BackgroundBrush, (value, source) => HeadersPanel.SetBackground(value, source));
            Context.ApplyThemeDefault("TabControl.SelectedHeaderTemplate", SelectedTabHeaderTemplateName,
                () => TabControl.SelectedTabHeaderControlTemplateName, value => TabControl.SelectedTabHeaderControlTemplateName = value, UIInvalidationKind.Structure | UIInvalidationKind.Measure | UIInvalidationKind.Arrange);
            Context.ApplyThemeDefault("TabControl.UnselectedHeaderTemplate", UnselectedTabHeaderTemplateName,
                () => TabControl.UnselectedTabHeaderControlTemplateName, value => TabControl.UnselectedTabHeaderControlTemplateName = value, UIInvalidationKind.Structure | UIInvalidationKind.Measure | UIInvalidationKind.Arrange);

            if (!Context.IsThemeRefresh)
            {
                ApplyTabControlHeadersPanelSettings(TabControl, HeadersPanel);
            }
        }

        internal static void ApplyTabControlHeadersPanelSettings(MGTabControl TabControl, MGStackPanel HeadersPanel)
        {
            if (TabControl == null || HeadersPanel == null)
            {
                return;
            }

            switch (TabControl.TabHeaderPosition)
            {
                case Dock.Left:
                    HeadersPanel.Orientation = Orientation.Vertical;
                    HeadersPanel.HorizontalAlignment = HorizontalAlignment.Right;
                    HeadersPanel.VerticalAlignment = VerticalAlignment.Stretch;
                    HeadersPanel.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    HeadersPanel.VerticalContentAlignment = VerticalAlignment.Top;
                    break;
                case Dock.Top:
                    HeadersPanel.Orientation = Orientation.Horizontal;
                    HeadersPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    HeadersPanel.VerticalAlignment = VerticalAlignment.Bottom;
                    HeadersPanel.HorizontalContentAlignment = HorizontalAlignment.Left;
                    HeadersPanel.VerticalContentAlignment = VerticalAlignment.Stretch;
                    break;
                case Dock.Right:
                    HeadersPanel.Orientation = Orientation.Vertical;
                    HeadersPanel.HorizontalAlignment = HorizontalAlignment.Left;
                    HeadersPanel.VerticalAlignment = VerticalAlignment.Stretch;
                    HeadersPanel.HorizontalContentAlignment = HorizontalAlignment.Stretch;
                    HeadersPanel.VerticalContentAlignment = VerticalAlignment.Top;
                    break;
                case Dock.Bottom:
                    HeadersPanel.Orientation = Orientation.Horizontal;
                    HeadersPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                    HeadersPanel.VerticalAlignment = VerticalAlignment.Top;
                    HeadersPanel.HorizontalContentAlignment = HorizontalAlignment.Left;
                    HeadersPanel.VerticalContentAlignment = VerticalAlignment.Stretch;
                    break;
                default:
                    throw new NotImplementedException($"Unrecognized {nameof(Dock)}: {TabControl.TabHeaderPosition}");
            }
        }

        private static bool TryGetOwningTabControl(MGButton Button, out MGTabControl TabControl)
        {
            if (Button?.Metadata?.TryGetValue(MGTabControl.HeaderTemplateOwnerMetadataKey, out object owner) == true
                && owner is MGTabControl typedOwner)
            {
                TabControl = typedOwner;
                return true;
            }

            TabControl = null;
            return false;
        }

        private static void ApplyTabHeaderTemplate(MGControlTemplateContext Context, bool IsSelected)
        {
            if (Context.Owner is not MGButton Button || !TryGetOwningTabControl(Button, out MGTabControl TabControl))
            {
                return;
            }

            MGTheme theme = TabControl.GetTheme();
            if (theme == null)
            {
                return;
            }

            UIInvalidationKind headerLayoutInvalidation = UIInvalidationKind.Measure | UIInvalidationKind.Arrange;

            Context.ApplyThemeDefault(IsSelected ? "TabHeader.Selected.BorderBrush" : "TabHeader.Unselected.BorderBrush",
                IsSelected ? MGUniformBorderBrush.Black : MGUniformBorderBrush.Gray,
                () => Button.BorderBrush,
                (value, source) => Button.SetBorderBrushTagged(value, source));
            Context.ApplyThemeDefault(IsSelected ? "TabHeader.Selected.Background" : "TabHeader.Unselected.Background",
                IsSelected ? theme.SelectedTabHeaderBackground.GetValue(true) : theme.UnselectedTabHeaderBackground.GetValue(true),
                () => Button.BackgroundBrush,
                (value, source) => Button.SetBackground(value, source));
            Context.ApplyThemeDefault(IsSelected ? "TabHeader.Selected.Foreground" : "TabHeader.Unselected.Foreground",
                theme.TextBlockFallbackForeground.GetValue(true).NormalValue,
                () => Button.DefaultTextForeground.NormalValue,
                (value, source) => Button.SetDefaultTextForegroundAll(value, source));

            switch (TabControl.TabHeaderPosition)
            {
                case Dock.Left:
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.Padding.Left" : "TabHeader.Unselected.Padding.Left", new Thickness(6, 5, 6, 5), () => Button.Padding, (value, source) => Button.SetPadding(value, source), headerLayoutInvalidation);
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.BorderThickness.Left" : "TabHeader.Unselected.BorderThickness.Left", new Thickness(1, 1, 0, 1), () => Button.BorderThickness, (value, source) => Button.SetBorderThicknessTagged(value, source), headerLayoutInvalidation);
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.HorizontalAlignment.Left" : "TabHeader.Unselected.HorizontalAlignment.Left", HorizontalAlignment.Right, () => Button.HorizontalAlignment, value => Button.HorizontalAlignment = value, headerLayoutInvalidation);
                    break;
                case Dock.Top:
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.Padding.Top" : "TabHeader.Unselected.Padding.Top", IsSelected ? new Thickness(8, 5, 8, 5) : new Thickness(8, 3, 8, 3), () => Button.Padding, (value, source) => Button.SetPadding(value, source), headerLayoutInvalidation);
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.BorderThickness.Top" : "TabHeader.Unselected.BorderThickness.Top", new Thickness(1, 1, 1, 0), () => Button.BorderThickness, (value, source) => Button.SetBorderThicknessTagged(value, source), headerLayoutInvalidation);
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.VerticalAlignment.Top" : "TabHeader.Unselected.VerticalAlignment.Top", VerticalAlignment.Bottom, () => Button.VerticalAlignment, value => Button.VerticalAlignment = value, headerLayoutInvalidation);
                    break;
                case Dock.Right:
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.Padding.Right" : "TabHeader.Unselected.Padding.Right", new Thickness(6, 5, 6, 5), () => Button.Padding, (value, source) => Button.SetPadding(value, source), headerLayoutInvalidation);
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.BorderThickness.Right" : "TabHeader.Unselected.BorderThickness.Right", new Thickness(0, 1, 1, 1), () => Button.BorderThickness, (value, source) => Button.SetBorderThicknessTagged(value, source), headerLayoutInvalidation);
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.HorizontalAlignment.Right" : "TabHeader.Unselected.HorizontalAlignment.Right", HorizontalAlignment.Left, () => Button.HorizontalAlignment, value => Button.HorizontalAlignment = value, headerLayoutInvalidation);
                    break;
                case Dock.Bottom:
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.Padding.Bottom" : "TabHeader.Unselected.Padding.Bottom", IsSelected ? new Thickness(8, 5, 8, 5) : new Thickness(8, 3, 8, 3), () => Button.Padding, (value, source) => Button.SetPadding(value, source), headerLayoutInvalidation);
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.BorderThickness.Bottom" : "TabHeader.Unselected.BorderThickness.Bottom", new Thickness(1, 0, 1, 1), () => Button.BorderThickness, (value, source) => Button.SetBorderThicknessTagged(value, source), headerLayoutInvalidation);
                    Context.ApplyTemplateValue(IsSelected ? "TabHeader.Selected.VerticalAlignment.Bottom" : "TabHeader.Unselected.VerticalAlignment.Bottom", VerticalAlignment.Top, () => Button.VerticalAlignment, value => Button.VerticalAlignment = value, headerLayoutInvalidation);
                    break;
                default:
                    throw new NotImplementedException($"Unrecognized {nameof(Dock)}: {TabControl.TabHeaderPosition}");
            }
        }

        private static void ApplySelectedTabHeaderTemplate(MGControlTemplateContext Context)
            => ApplyTabHeaderTemplate(Context, true);

        private static void ApplyUnselectedTabHeaderTemplate(MGControlTemplateContext Context)
            => ApplyTabHeaderTemplate(Context, false);

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

            MGBorder Header = Context.GetRequiredPart<MGBorder>(MGDockAutoHideDrawer.TitleBarPartName);
            MGBorder PinButton = Context.GetRequiredPart<MGBorder>(MGDockAutoHideDrawer.PinButtonPartName);
            MGBorder CloseButton = Context.GetRequiredPart<MGBorder>(MGDockAutoHideDrawer.CloseButtonPartName);

            Context.ApplyOwnerThemeDefault("DockDrawer.BackgroundBrush", new VisualStateFillBrush(Docking.AutoHideDrawerBackground), () => Drawer.BackgroundBrush, (value, source) => Drawer.SetBackground(value, source));
            Context.ApplyThemeDefault("DockDrawer.HeaderBackgroundBrush", new VisualStateFillBrush(Docking.AutoHideDrawerHeaderBackground), () => Header.BackgroundBrush, (value, source) => Header.SetBackground(value, source));
            // ADR-0005/S5: Docking.AutoHideButtonBackground is raw/shared and read here for TWO different buttons
            // (PinButton, CloseButton) -- each needs its own copy (see the Window.CloseButtonBackground comment above).
            Context.ApplyThemeDefault("DockDrawer.PinButtonBackground", Docking.AutoHideButtonBackground?.Copy(), () => PinButton.BackgroundBrush, (value, source) => PinButton.SetBackground(value, source));
            Context.ApplyThemeDefault("DockDrawer.CloseButtonBackground", Docking.AutoHideButtonBackground?.Copy(), () => CloseButton.BackgroundBrush, (value, source) => CloseButton.SetBackground(value, source));
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

            Context.ApplyOwnerThemeDefault("DockStrip.BackgroundBrush", new VisualStateFillBrush(Docking.AutoHideStripBackground), () => Strip.BackgroundBrush, (value, source) => Strip.SetBackground(value, source));
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