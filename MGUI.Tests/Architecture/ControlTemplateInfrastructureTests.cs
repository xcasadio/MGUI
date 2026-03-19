using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Helpers;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace MGUI.Tests.Architecture;

public class ControlTemplateInfrastructureTests
{
    [Fact]
    public void Child_Scope_Falls_Back_To_Parent_Control_Template()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        MGControlTemplate template = new("Overlay.Chrome", _ => { });
        desktop.AddControlTemplate(template);

        MGResources subtree = new(desktop, UIResourceScope.Subtree);

        Assert.True(subtree.TryGetControlTemplate("Overlay.Chrome", out MGControlTemplate resolved));
        Assert.Same(template, resolved);
        Assert.Same(desktop.ControlTemplates, desktop.Definitions.ControlTemplates);
    }

    [Fact]
    public void Child_Scope_Can_Override_Parent_Control_Template()
    {
        MGResources desktop = new(new MGTheme("Arial"));
        MGControlTemplate parentTemplate = new("Overlay.Chrome", _ => { });
        MGControlTemplate childTemplate = new("Overlay.Chrome", _ => { });
        desktop.AddControlTemplate(parentTemplate);

        MGResources subtree = new(desktop, UIResourceScope.Subtree);
        subtree.AddControlTemplate(childTemplate);

        Assert.True(subtree.TryGetControlTemplate("Overlay.Chrome", out MGControlTemplate resolved));
        Assert.Same(childTemplate, resolved);
    }

    [Fact]
    public void Control_Template_Can_Apply_Without_Live_Element_Instance()
    {
        bool wasApplied = false;
        MGControlTemplate template = new("ContextMenu.Chrome", _ => wasApplied = true);

        template.Apply(new MGControlTemplateContext(null));

        Assert.True(wasApplied);
    }

    [Fact]
    public void Control_Template_Can_Expose_Structure_And_Defaults_As_Separate_Phases()
    {
        MGControlTemplateStructure attachedStructure = null;
        int applyDefaultsCount = 0;

        MGControlTemplate template = new(
            "Window.Structured",
            context =>
            {
                return new MGControlTemplateStructure(null);
            },
            (_, structure) => attachedStructure = structure,
            _ => applyDefaultsCount++);

        MGControlTemplateContext context = new(null);

        MGControlTemplateStructure structure = template.CreateStructure(context);
        template.AttachStructure(context, structure);
        template.ApplyDefaults(context);

        Assert.True(template.SupportsStructure);
        Assert.True(template.SupportsAttachment);
        Assert.Null(structure.Root);
        Assert.Same(structure, attachedStructure);
        Assert.Equal(1, applyDefaultsCount);
    }

    [Fact]
#pragma warning disable SYSLIB0050
    public void Control_Template_Structure_Can_Record_Detached_Roots()
#pragma warning restore SYSLIB0050
    {
        MGElement detachedRoot = (MGElement)FormatterServices.GetUninitializedObject(typeof(MGTextBlock));

        MGControlTemplateStructure structure = new(null, null, new[] { detachedRoot });

        Assert.Single(structure.DetachedRoots);
        Assert.Same(detachedRoot, structure.DetachedRoots[0]);
    }

    [Fact]
    public void Legacy_Control_Template_Apply_Remains_Defaults_Only()
    {
        bool applyDefaultsCalled = false;
        MGControlTemplate template = new("Legacy", _ => applyDefaultsCalled = true);

        template.Apply(new MGControlTemplateContext(null));

        Assert.False(template.SupportsStructure);
        Assert.False(template.SupportsAttachment);
        Assert.True(applyDefaultsCalled);
        Assert.Null(template.CreateStructure(new MGControlTemplateContext(null)));
    }

    [Fact]
    public void Visual_State_Projection_Maps_State_Transitions_To_Target_Action()
    {
        bool highlighted = false;
        using MGVisualStateProjection projection = new(null, (_, current) =>
        {
            highlighted = current.IsPressedOrHovered || current.IsSelected;
        }, ApplyImmediately: false);

        projection.Apply(new(PrimaryVisualState.Normal, SecondaryVisualState.None), new(PrimaryVisualState.Normal, SecondaryVisualState.Hovered));
        Assert.True(highlighted);

        projection.Apply(new(PrimaryVisualState.Normal, SecondaryVisualState.Hovered), new(PrimaryVisualState.Normal, SecondaryVisualState.None));
        Assert.False(highlighted);

        projection.Apply(new(PrimaryVisualState.Normal, SecondaryVisualState.None), new(PrimaryVisualState.Selected, SecondaryVisualState.None));
        Assert.True(highlighted);
    }

    [Fact]
    public void Default_Catalog_Registers_Priority_And_Selection_Control_Templates()
    {
        MGResources resources = new(new MGTheme("Arial"));

        MGControlTemplateCatalog.RegisterDefaults(resources);

        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.WindowTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ToolTipTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.OverlayTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ContextMenuTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ContextMenuItemTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ListBoxTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ListViewTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ComboBoxTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ComboBoxDropdownItemTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.TreeViewTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.TextBoxTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.TabControlTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.SelectedTabHeaderTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.UnselectedTabHeaderTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockTabItemTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockAutoHideDrawerTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockAutoHideStripTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockSplitterTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.DockDropIndicatorsTemplateName, out _));
        Assert.True(resources.ControlTemplates[MGControlTemplateCatalog.WindowTemplateName].SupportsStructure);
        Assert.True(resources.ControlTemplates[MGControlTemplateCatalog.OverlayTemplateName].SupportsStructure);
        Assert.True(resources.ControlTemplates[MGControlTemplateCatalog.ListBoxTemplateName].SupportsStructure);
        Assert.True(resources.ControlTemplates[MGControlTemplateCatalog.ListViewTemplateName].SupportsStructure);
        Assert.True(resources.ControlTemplates[MGControlTemplateCatalog.ComboBoxTemplateName].SupportsStructure);
        Assert.True(resources.ControlTemplates[MGControlTemplateCatalog.TreeViewTemplateName].SupportsStructure);
        Assert.True(resources.ControlTemplates[MGControlTemplateCatalog.TextBoxTemplateName].SupportsStructure);
        Assert.True(resources.ControlTemplates[MGControlTemplateCatalog.TabControlTemplateName].SupportsStructure);
    }

    [Fact]
    public void Xaml_Element_Exposes_Control_Template_Name()
    {
        Assert.Equal(typeof(string), typeof(Element).GetProperty(nameof(Element.ControlTemplate))?.PropertyType);
    }

    [Fact]
    public void MGElement_Exposes_Default_Control_Template_Fallback()
    {
        Assert.Equal(typeof(string), typeof(MGElement).GetProperty(nameof(MGElement.DefaultControlTemplateName))?.PropertyType);
    }

    [Fact]
    public void Control_Template_Context_Flags_Theme_Refresh()
    {
        bool? observedFlag = null;
        MGControlTemplate template = new("Window.Chrome", context => observedFlag = context.IsThemeRefresh);

        template.Apply(new MGControlTemplateContext(null, true));

        Assert.True(observedFlag);
    }

    [Fact]
    public void Theme_Exposes_Window_Chrome_Defaults()
    {
        MGTheme theme = new("Arial");

        Assert.Equal(new MonoGame.Extended.Thickness(5), theme.Window.Padding);
        Assert.Equal(new MonoGame.Extended.Thickness(2), theme.Window.BorderThickness);
        Assert.Equal(new MonoGame.Extended.Thickness(2), theme.Window.TitleBarPadding);
        Assert.Equal(24, theme.Window.TitleBarMinHeight);
    }

    [Fact]
    public void Theme_Exposes_Composite_Control_Default_Groups()
    {
        MGTheme theme = new("Arial");

        Assert.NotNull(theme.Overlay);
        Assert.NotNull(theme.ContextMenu);
        Assert.NotNull(theme.ContextMenuItem);
        Assert.NotNull(theme.ListBox);
        Assert.NotNull(theme.ListView);
        Assert.NotNull(theme.ComboBox);
        Assert.NotNull(theme.TreeViewTemplate);
        Assert.NotNull(theme.TabControl);
    }

    [Fact]
    public void MGElement_Exposes_Runtime_Attach_Hook_For_Structural_Control_Templates()
    {
        Assert.NotNull(typeof(MGElement).GetMethod("AttachControlTemplateStructure", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public));
    }

    [Fact]
    public void MGElement_Runtime_Path_Separates_Structure_From_Theme_Refresh()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGElement.cs");

        Assert.Contains("Template.CreateStructure", source);
        Assert.Contains("AttachControlTemplateStructure(Structure)", source);
        Assert.Contains("ResolveControlTemplateName()", source);
        Assert.Contains("_AppliedTemplateStructure == null || TemplateChanged", source);
    }

    [Fact]
    public void MGElement_Resolves_Control_Templates_In_Local_Theme_Default_Order()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGElement.cs");

        int localIndex = source.IndexOf("return ControlTemplateName;", StringComparison.Ordinal);
        int runtimeTypeThemeIndex = source.IndexOf("Theme.TryGetControlTemplateMapping(GetType()", StringComparison.Ordinal);
        int themeIndex = source.IndexOf("Theme.TryGetControlTemplateMapping(ElementType", StringComparison.Ordinal);
        int fallbackIndex = source.IndexOf("return DefaultControlTemplateName;", StringComparison.Ordinal);

        Assert.True(localIndex >= 0);
        Assert.True(runtimeTypeThemeIndex > localIndex);
        Assert.True(themeIndex > runtimeTypeThemeIndex);
        Assert.True(fallbackIndex > themeIndex);
    }

    [Fact]
    public void Composite_Controls_Rebind_Components_When_Template_Structure_Changes()
    {
        string tabControlSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTabControl.cs");
        string listBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGListBox.cs");
        string comboBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGComboBox.cs");
        string textBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTextBox.cs");
        string windowSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGWindow.cs");
        string overlaySource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGOverlay.cs");

        Assert.Contains("EnsureComponentBinding", tabControlSource);
        Assert.Contains("EnsureComponentBinding", listBoxSource);
        Assert.Contains("EnsureComponentBinding", comboBoxSource);
        Assert.Contains("EnsureComponentBinding", textBoxSource);
        Assert.Contains("EnsureComponentBinding", windowSource);
        Assert.Contains("EnsureComponentBinding", overlaySource);
    }

    [Fact]
    public void Migrated_Controls_Use_Default_Control_Template_Fallback_Instead_Of_Explicit_Local_Name()
    {
        string[] paths = new[]
        {
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGWindow.cs",
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGOverlay.cs",
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGContextMenu.cs",
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGListBox.cs",
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGListView.cs",
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGTreeView.cs",
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGTextBox.cs",
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGTabControl.cs",
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGComboBox.cs",
            @"d:\development\repo\MGUI\MGUI.Core\UI\MGToolTip.cs",
        };

        foreach (string path in paths)
        {
            string source = File.ReadAllText(path);
            Assert.Contains("DefaultControlTemplateName = MGControlTemplateCatalog.", source);
        }
    }

    [Fact]
    public void MGTabControl_Template_Attach_Binds_Headers_Panel_Directly_To_HeaderPresenter()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTabControl.cs");

        Assert.Contains("HeaderPresenter.SetContent(HeadersPanelElement);", source);
        Assert.Contains("HeadersPanelElement.TryRemoveAll();", source);
    }

    [Fact]
    public void MGTabControl_Default_Header_Wrappers_Use_Control_Template_Names()
    {
        string tabControlSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTabControl.cs");
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");

        Assert.Contains("SelectedTabHeaderControlTemplateName", tabControlSource);
        Assert.Contains("UnselectedTabHeaderControlTemplateName", tabControlSource);
        Assert.Contains("HeaderWrapper.ControlTemplateName", tabControlSource);
        Assert.Contains("DefaultHeaderWrapperMetadataKey", tabControlSource);
        Assert.Contains("ApplyHeaderWrapperTemplate(OldHeaderWrapper, Tab.IsTabSelected);", tabControlSource);
        Assert.Contains("OldHeaderWrapper.InvalidateLayoutTree();", tabControlSource);
        Assert.Contains("if (!UsesCustomHeaderFactories && IsDefaultHeaderWrapper(OldHeaderWrapper))", tabControlSource);
        Assert.Contains("MGButton NewHeaderWrapper = CreateHeaderWrapper(Tab);", tabControlSource);
        Assert.Contains("ApplyTabControlHeadersPanelSettings", catalogSource);
        Assert.Contains("SelectedTabHeaderTemplateName = \"TabControl.Header.Selected\"", catalogSource);
        Assert.Contains("UnselectedTabHeaderTemplateName = \"TabControl.Header.Unselected\"", catalogSource);
    }

    [Fact]
    public void MGTabControl_Side_Header_Defaults_Differentiate_Selected_And_Unselected_Offsets()
    {
        string tabControlSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTabControl.cs");
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");

        Assert.Contains("ApplyTemplateValue(IsSelected ? \"TabHeader.Selected.Padding.Left\" : \"TabHeader.Unselected.Padding.Left\", new Thickness(6, 5, 6, 5)", catalogSource);
        Assert.Contains("ApplyTemplateValue(IsSelected ? \"TabHeader.Selected.Padding.Right\" : \"TabHeader.Unselected.Padding.Right\", new Thickness(6, 5, 6, 5)", catalogSource);
        Assert.Contains("ApplyTemplateValue(IsSelected ? \"TabHeader.Selected.HorizontalAlignment.Left\" : \"TabHeader.Unselected.HorizontalAlignment.Left\", HorizontalAlignment.Right", catalogSource);
        Assert.Contains("ApplyTemplateValue(IsSelected ? \"TabHeader.Selected.HorizontalAlignment.Right\" : \"TabHeader.Unselected.HorizontalAlignment.Right\", HorizontalAlignment.Left", catalogSource);
        Assert.Contains("UIInvalidationKind.Measure | UIInvalidationKind.Arrange", catalogSource);
        Assert.Contains("ApplyTemplateValue(IsSelected ? \"TabHeader.Selected.Padding.Left\"", catalogSource);
        Assert.Contains("MGControlTemplateCatalog.ApplyTabControlHeadersPanelSettings(this, HeadersPanelElement);", tabControlSource);
        Assert.DoesNotContain("HeadersPanelElement.Orientation = Orientation.Vertical;", tabControlSource);
        Assert.DoesNotContain("HeadersPanelElement.HorizontalAlignment = HorizontalAlignment.Right;", tabControlSource);
        Assert.Contains("UpdateHeadersPanelPreferredSize()", tabControlSource);
        Assert.Contains("HeadersPanelElement.PreferredWidth = maxWidth > 0 ? maxWidth : null;", tabControlSource);
        Assert.Contains("HeadersPanelElement.PreferredHeight = maxHeight > 0 ? maxHeight : null;", tabControlSource);
    }

    [Fact]
    public void Focus_Scope_Restore_Uses_Pointer_Focus_To_Avoid_Context_Menu_Autoscroll()
    {
        string navigationServiceSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Navigation\UIFocusNavigationService.cs");

        Assert.Contains("entry.RestoreFocusTarget.Focus(KeyboardFocusSource.Pointer);", navigationServiceSource);
    }

    [Fact]
    public void MGDesktop_Does_Not_Scroll_Ancestor_Viewports_For_Floating_Context_Menu_Focus()
    {
        string contextMenuSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGContextMenu.cs");

        Assert.Contains("initialFocusTarget?.Focus(KeyboardFocusSource.Pointer);", contextMenuSource);
    }

    [Fact]
    public void MGScrollViewer_Reapplies_Theme_Scrollbar_Brushes_On_Theme_Change()
    {
        string scrollViewerSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGScrollViewer.cs");

        Assert.Contains("protected internal override void OnThemeChanged", scrollViewerSource);
        Assert.Contains("ScrollBarOuterBrush = CurrentTheme.ScrollBarOuterBrush.GetValue(true);", scrollViewerSource);
        Assert.Contains("ScrollBarInnerBrush = CurrentTheme.ScrollBarInnerBrush.GetValue(true);", scrollViewerSource);
    }

    [Fact]
    public void MGTabControl_Custom_Header_Factories_Are_Not_ReTemplated_After_Xaml_Factory_Application()
    {
        string tabControlSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTabControl.cs");
        string xamlControlsSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\XAML\Controls.cs");

        Assert.Contains("if (UsesCustomHeaderFactories)", tabControlSource);
        Assert.Contains("HeaderWrapper.IsSelected = IsSelected;", tabControlSource);
        Assert.Contains("return;", tabControlSource);
        Assert.Contains("HeadersPanelElement?.InvalidateLayoutTree();", tabControlSource);
        Assert.Contains("Button.ApplyControlTemplate(false);", xamlControlsSource);
        Assert.Contains("SelectedTabHeaderTemplate.ApplySettings(TabItem, Button, true);", xamlControlsSource);
        Assert.Contains("UnselectedTabHeaderTemplate.ApplySettings(TabItem, Button, true);", xamlControlsSource);
    }

    [Fact]
    public void Control_Template_Requirement_Metadata_Is_Declared_For_Migrating_Controls()
    {
        Assert.Equal(typeof(MGWindow), typeof(MGWindow).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGContextMenu), typeof(MGContextMenu).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGOverlay), typeof(MGOverlay).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGListBox<>), typeof(MGListBox<>).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGListView<>), typeof(MGListView<>).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGTreeView), typeof(MGTreeView).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGTextBox), typeof(MGTextBox).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGTabControl), typeof(MGTabControl).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGComboBox<>), typeof(MGComboBox<>).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
    }

    [Fact]
    public void TreeView_And_TextBox_Default_Templates_Are_Structural()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");
        string treeViewSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTreeView.cs");
        string textBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTextBox.cs");

        Assert.Contains("CreateTreeViewTemplate()", source);
        Assert.Contains("CreateTextBoxTemplate()", source);
        Assert.Contains("new(TreeViewTemplateName, CreateTreeViewTemplateStructure", source);
        Assert.Contains("new(TextBoxTemplateName, CreateTextBoxTemplateStructure", source);
        Assert.DoesNotContain("ApplyDefaultStyles();", treeViewSource);
        Assert.DoesNotContain("FocusedSelectionForegroundColor = Theme.TextBoxFocusedSelectionForeground;", textBoxSource);
    }

    [Fact]
    public void Control_Template_Value_Application_Uses_Template_Source_Metadata()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplate.cs");
        string elementSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGElement.cs");

        Assert.Contains("UIValueResolutionSource.Template", source);
        Assert.Contains("Owner.InvalidateTemplateValue(Invalidation);", source);
        Assert.Contains("internal void InvalidateTemplateValue(UIInvalidationKind invalidation)", elementSource);
        Assert.Contains("LayoutChanged(this, true);", elementSource);
    }

    [Fact]
    public void Parent_And_Selected_State_Changes_Invalidate_Layout_Subtrees()
    {
        string elementSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGElement.cs");

        Assert.Contains("protected internal void SetParent(MGElement Value)", elementSource);
        Assert.Contains("InvalidateLayoutTree();", elementSource);
        Assert.Contains("public bool IsSelected", elementSource);
        Assert.Contains("LayoutChanged(this, true);", elementSource);
    }

    [Fact]
#pragma warning disable SYSLIB0050
    public void MGWindow_Chrome_Properties_Can_Be_Set_Before_Template_Parts_Attach()
#pragma warning restore SYSLIB0050
    {
        MGWindow window = (MGWindow)FormatterServices.GetUninitializedObject(typeof(MGWindow));

        window.TitleText = "Docking System Demo";
        window.IsTitleBarVisible = true;
        window.IsCloseButtonVisible = true;
        window.IsUserResizable = true;

        Assert.Equal("Docking System Demo", window.TitleText);
        Assert.True(window.IsTitleBarVisible);
        Assert.True(window.IsCloseButtonVisible);
        Assert.True(window.IsUserResizable);
    }

    [Fact]
    public void MGContextMenu_Requirements_Handle_Base_Window_Template_During_Construction()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGContextMenu.cs");

        Assert.Contains("MGControlTemplateCatalog.WindowTemplateName", source);
        Assert.Contains("base.GetRequiredControlTemplateParts()", source);
    }

    [Fact]
    public void ToolTip_Uses_Dedicated_Control_Template_Defaults()
    {
        string toolTipSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGToolTip.cs");
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");

        Assert.Contains("ControlTemplateName = MGControlTemplateCatalog.ToolTipTemplateName;", toolTipSource);
        Assert.Contains("ToolTipTemplateName = \"ToolTip.Default\"", catalogSource);
        Assert.Contains("ApplyToolTipTemplate", catalogSource);
    }

    [Fact]
#pragma warning disable SYSLIB0050
    public void MGOverlay_Close_Button_Can_Be_Configured_Before_Template_Parts_Attach()
#pragma warning restore SYSLIB0050
    {
        MGOverlay overlay = (MGOverlay)FormatterServices.GetUninitializedObject(typeof(MGOverlay));

        overlay.ShowCloseButton = true;

        Assert.True(overlay.ShowCloseButton);
    }

    [Fact]
    public void ComboBox_Template_Sets_Initial_Content_Before_Locking_Content_Hosts()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");

        int setContentIndex = source.IndexOf("dropdownScrollViewer.SetContent(dropdownStackPanel);", StringComparison.Ordinal);
        int lockIndex = source.IndexOf("dropdownScrollViewer.CanChangeContent = false;", StringComparison.Ordinal);

        Assert.True(setContentIndex >= 0);
        Assert.True(lockIndex > setContentIndex);
    }

    [Fact]
    public void ComboBox_Default_Dropdown_Item_Chrome_Uses_Control_Template_Resources()
    {
        string comboBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGComboBox.cs");
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");

        Assert.Contains("DropdownItemControlTemplateName", comboBoxSource);
        Assert.Contains("Target.ControlTemplateName = DropdownItemControlTemplateName;", comboBoxSource);
        Assert.Contains("ComboBoxDropdownItemTemplateName = \"ComboBox.DropdownItem.Default\"", catalogSource);
        Assert.Contains("ApplyComboBoxDropdownItemTemplate", catalogSource);
    }

    [Fact]
    public void Built_In_Control_Template_Xaml_Defines_ListBox_And_ListView_Structures()
    {
        const string resourceName = "MGUI.Core.UI.Templates.BuiltInControlTemplates.xaml";
        string markup = GeneralUtils.ReadEmbeddedResourceAsString(typeof(MGControlTemplateCatalog).Assembly, resourceName);

        IReadOnlyList<ControlTemplateDefinition> definitions = ControlTemplateLoader.ParseDefinitions(XamlDocumentSource.FromString(markup, resourceName));
        ControlTemplateDefinition listBoxDefinition = definitions.Single(x => x.Name == MGControlTemplateCatalog.ListBoxTemplateName);
        ControlTemplateDefinition listViewDefinition = definitions.Single(x => x.Name == MGControlTemplateCatalog.ListViewTemplateName);

        Assert.Null(listBoxDefinition.Root);
        Assert.Equal(3, listBoxDefinition.DetachedRoots.Count);
        Assert.Contains(listBoxDefinition.Parts, x => x.Name == MGListBox<object>.ItemsPanelPartName);
        Assert.Contains(listViewDefinition.Parts, x => x.Name == MGListView<object>.DataGridPartName);
    }

    [Fact]
    public void TabControl_And_ComboBox_Theme_Dependent_Chrome_Is_Applied_From_Template_Catalog()
    {
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");
        string comboBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGComboBox.cs");

        Assert.Contains("TabControl.Background", catalogSource);
        Assert.Contains("Theme.GetBackgroundBrush(MGElementType.TabControl)", catalogSource);
        Assert.Contains("ComboBox.DropdownArrowColor", catalogSource);
        Assert.Contains("Theme.DropdownArrowColor", catalogSource);
        Assert.Contains("ComboBox.DropdownArrowMargin", catalogSource);
        Assert.Contains("ComboBox.Padding", catalogSource);
        Assert.Contains("ComboBox.MinHeight", catalogSource);
        Assert.DoesNotContain("Padding = new(4, 2, 4, 2);", comboBoxSource);
        Assert.DoesNotContain("MinHeight = 26;", comboBoxSource);
    }

    [Fact]
    public void ComboBox_Default_Template_Structure_Does_Not_Hardcode_Dropdown_Arrow_Margins()
    {
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");

        Assert.DoesNotContain("dropdownArrow.Margin = new(MGComboBox<object>.DefaultDropdownArrowLeftMargin, 0, MGComboBox<object>.DefaultDropdownArrowRightMargin, 0);", catalogSource);
        Assert.Contains("Context.ApplyThemeDefault(\"ComboBox.DropdownArrowMargin\"", catalogSource);
    }

    [Fact]
    public void TabControl_ComboBox_And_TreeView_No_Longer_Project_Theme_Values_In_OnThemeChanged()
    {
        string tabControlSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTabControl.cs");
        string comboBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGComboBox.cs");
        string treeViewSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTreeView.cs");

        Assert.DoesNotContain("protected internal override void OnThemeChanged", tabControlSource);
        Assert.DoesNotContain("protected internal override void OnThemeChanged", comboBoxSource);
        Assert.DoesNotContain("protected internal override void OnThemeChanged", treeViewSource);
        Assert.DoesNotContain("CurrentTheme.GetBackgroundBrush(MGElementType.TabControl)", tabControlSource);
        Assert.DoesNotContain("CurrentTheme.DropdownArrowColor", comboBoxSource);
    }

    [Fact]
    public void TreeView_Selection_Visuals_Refresh_From_Selection_Property_Setters()
    {
        string treeViewSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTreeView.cs");

        Assert.Contains("_SelectionBackgroundBrush = value;", treeViewSource);
        Assert.Contains("SelectedItem?.RefreshSelectionVisual();", treeViewSource);
        Assert.Contains("_SelectionForeground = value;", treeViewSource);
    }

    [Fact]
    public void ListBox_Does_Not_Overwrite_Template_Owned_ItemsPanel_Chrome()
    {
        string listBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGListBox.cs");
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");

        Assert.Contains("private void SyncVirtualizedItemsPanelChrome()", listBoxSource);
        Assert.Contains("_virtualizingPanel.BorderThickness = ItemsPanel?.BorderThickness ?? MGControlTemplateCatalog.DefaultListBoxItemBorderThickness;", listBoxSource);
        Assert.Contains("_virtualizingPanel.BorderBrush = ItemsPanel?.BorderBrush ?? MGControlTemplateCatalog.CreateDefaultListBoxItemBorderBrush();", listBoxSource);
        Assert.Contains("public static void ApplyListBoxItemContainerDefaults", catalogSource);
        Assert.Contains("Item.Padding = DefaultListBoxItemPadding;", catalogSource);
        Assert.Contains("Context.ApplyTemplateValue(\"ListBox.ItemsPanelVerticalAlignment\"", catalogSource);
        Assert.Contains("Context.ApplyTemplateValue(\"ListBox.TitlePresenterVerticalAlignment\"", catalogSource);
        Assert.Contains("=> MGControlTemplateCatalog.ApplyListBoxItemContainerDefaults(this, Item);", listBoxSource);
        Assert.DoesNotContain("ItemsPanel.BorderThickness = DefaultItemBorderThickness;", listBoxSource);
        Assert.DoesNotContain("ItemsPanel.BorderBrush = DefaultItemBorderBrush;", listBoxSource);
        Assert.DoesNotContain("TitlePresenter.VerticalAlignment = VerticalAlignment.Center;", listBoxSource);
        Assert.DoesNotContain("ItemsPanel.VerticalAlignment = VerticalAlignment.Top;", listBoxSource);
        Assert.DoesNotContain("public readonly MGUniformBorderBrush DefaultItemBorderBrush", listBoxSource);
        Assert.DoesNotContain("public readonly Thickness DefaultItemBorderThickness", listBoxSource);
        Assert.DoesNotContain("SetTitleAndContentBorder(SolidFillBrushes.Black, 1);", listBoxSource);
    }

    [Fact]
    public void ListView_Grid_Chrome_And_Header_Spacer_Use_Template_Defaults()
    {
        string listViewSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGListView.cs");
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");

        Assert.Contains("internal interface IMGListViewTemplateDefaults", listViewSource);
        Assert.Contains("IMGListViewTemplateDefaults", catalogSource);
        Assert.DoesNotContain("DefaultGridLineBrush = SolidFillBrushes.Black", listViewSource);
        Assert.DoesNotContain("HeaderGrid.HorizontalGridLineBrush = DefaultGridLineBrush;", listViewSource);
        Assert.DoesNotContain("DataGrid.HorizontalGridLineBrush = DefaultGridLineBrush;", listViewSource);
        Assert.DoesNotContain("HeaderSpacer.BorderBrush = MGUniformBorderBrush.Black;", listViewSource);
        Assert.DoesNotContain("HeaderGrid.RowSpacing = InitialSpacing;", listViewSource);
        Assert.DoesNotContain("DataGrid.Padding = new(0, InitialGridLineMargin, 0, 0);", listViewSource);
        Assert.DoesNotContain("GetType().GetProperty(nameof(MGListView<object>.TemplateDefaultSpacing))", catalogSource);
        Assert.Contains("TemplateDefaultSpacing", listViewSource);
        Assert.Contains("TemplateDefaultGridLineMargin", listViewSource);
        Assert.Contains("ListView.HeaderGridSpacing", catalogSource);
        Assert.Contains("ListView.DataGridPadding", catalogSource);
        Assert.Contains("ListView.HeaderSpacerBorderThickness", catalogSource);
        Assert.Contains("ListView.HeaderSpacerBorderBrush", catalogSource);
        Assert.Contains("ListView.HeaderSpacerBackground", catalogSource);
    }

    [Fact]
    public void Window_And_Overlay_Do_Not_Hardcode_Template_Owned_Padding_In_Constructors()
    {
        string windowSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGWindow.cs");
        string overlaySource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGOverlay.cs");
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");
        string themeSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTheme.cs");
        string themeBuilderSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\XAML\ThemeDefinitionBuilder.cs");

        Assert.Contains("Context.ApplyThemeDefault(\"Window.Padding\"", catalogSource);
        Assert.Contains("Context.ApplyThemeDefault(\"Overlay.Padding\"", catalogSource);
        Assert.DoesNotContain("Padding = new(5);", overlaySource);
        Assert.DoesNotContain("Padding = new(4);", overlaySource);
        Assert.DoesNotContain("Padding = DefaultWindowPadding;", windowSource);
        Assert.Contains("public Thickness HostPadding { get; set; } = new(4);", themeSource);
        Assert.Contains("Padding = GetTheme().Overlay.HostPadding;", overlaySource);
        Assert.Contains("Definition.HostPadding", themeBuilderSource);
        Assert.Contains("Padding = GetTheme().Window.Padding;", windowSource);
        Assert.Contains("BorderThickness = GetTheme().Window.BorderThickness;", windowSource);
    }

    [Fact]
    public void TextBox_Default_Layout_Chrome_Comes_From_Template_Defaults()
    {
        string textBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTextBox.cs");
        string catalogSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplateCatalog.cs");
        int attachIndex = textBoxSource.IndexOf("protected internal override void AttachControlTemplateStructure", StringComparison.Ordinal);
        string attachSlice = attachIndex >= 0 ? textBoxSource.Substring(attachIndex, Math.Min(2500, textBoxSource.Length - attachIndex)) : textBoxSource;

        Assert.Contains("Context.ApplyTemplateValue(\"TextBox.Padding\"", catalogSource);
        Assert.Contains("Context.ApplyTemplateValue(\"TextBox.MinHeight\"", catalogSource);
        Assert.Contains("SyncPlaceholderTextPart();", textBoxSource);
        Assert.Contains("SyncCharacterCountVisibility();", textBoxSource);
        Assert.Contains("SyncResizeGripVisibility();", textBoxSource);
        Assert.DoesNotContain("Padding = new(6, 2, 6, 2);", textBoxSource);
        Assert.DoesNotContain("MinHeight = 26;", textBoxSource);
        Assert.DoesNotContain("PlaceholderTextBlockElement.Visibility = Visibility.Collapsed;", attachSlice);
        Assert.DoesNotContain("CharacterCountElement.Visibility = _ShowCharacterCount ? Visibility.Visible : Visibility.Collapsed;", attachSlice);
        Assert.DoesNotContain("ResizeGripElement.Visibility = IsUserResizable ? Visibility.Visible : Visibility.Collapsed;", attachSlice);
    }

    [Fact]
    public void Shared_Triangle_Arrow_Helper_Is_Used_By_Composite_And_Manual_Controls()
    {
        string helperSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\UISymbolDrawing.cs");
        string comboBoxSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGComboBox.cs");
        string contextMenuItemSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGContextMenuItem.cs");
        string expanderSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGExpander.cs");
        string treeViewItemSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTreeViewItem.cs");

        Assert.Contains("public static class UISymbolDrawing", helperSource);
        Assert.Contains("UITriangleArrowDirection", helperSource);
        Assert.Contains("UISymbolDrawing.DrawFilledTriangleArrow", comboBoxSource);
        Assert.Contains("UISymbolDrawing.DrawFilledTriangleArrow", contextMenuItemSource);
        Assert.Contains("UISymbolDrawing.DrawFilledTriangleArrow", expanderSource);
        Assert.Contains("UISymbolDrawing.DrawFilledTriangleArrow", treeViewItemSource);
        Assert.DoesNotContain("List<Vector2> ArrowVertices", comboBoxSource);
        Assert.DoesNotContain("List<Vector2> ArrowVertices", contextMenuItemSource);
        Assert.DoesNotContain("List<Point> DropdownArrowVertices", expanderSource);
        Assert.DoesNotContain("List<Point> arrowVertices", treeViewItemSource);
    }

    [Fact]
    public void MenuBarItem_Reapplies_MenuBarItem_Background_On_Theme_Change()
    {
        string menuBarSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGMenuBar.cs");

        Assert.Contains("protected internal override void OnThemeChanged", menuBarSource);
        Assert.Contains("VisualStateFillBrush background = GetTheme().GetBackgroundBrush(MGElementType.MenuBarItem);", menuBarSource);
        Assert.Contains("Color? textForeground = GetTheme().TextBlockFallbackForeground.GetValue(true).NormalValue;", menuBarSource);
        Assert.Contains("Button.BackgroundBrush = background;", menuBarSource);
        Assert.Contains("Button.DefaultTextForeground.SetAll(textForeground);", menuBarSource);
        Assert.Contains("VisualStateFillBrush background = CurrentTheme.GetBackgroundBrush(MGElementType.MenuBarItem);", menuBarSource);
        Assert.Contains("Color? textForeground = CurrentTheme.TextBlockFallbackForeground.GetValue(true).NormalValue;", menuBarSource);
        Assert.Contains("ContentWrapper.BackgroundBrush = background;", menuBarSource);
        Assert.Contains("ContentWrapper.DefaultTextForeground.SetAll(textForeground);", menuBarSource);
    }

    [Fact]
    public void MenuBarItem_Projects_Owner_Visual_State_To_Internal_Button_Wrapper()
    {
        string menuBarSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGMenuBar.cs");
        string builtInThemesSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Themes\BuiltInThemes.xaml");

        Assert.Contains("private MGVisualStateProjection OwnerVisualStateProjection", menuBarSource);
        Assert.Contains("private MGVisualStateProjection ContentWrapperVisualStateProjection", menuBarSource);
        Assert.Contains("OwnerVisualStateProjection = new(this, (_, __) => ApplyContentWrapperVisualState());", menuBarSource);
        Assert.Contains("ContentWrapperVisualStateProjection = new(ContentWrapper, (_, __) => ApplyContentWrapperVisualState());", menuBarSource);
        Assert.Contains("bool isPressed = ownerState.IsPressed || wrapperState.IsPressed;", menuBarSource);
        Assert.Contains("bool isHighlighted = ownerState.IsPressedOrHovered || wrapperState.IsPressedOrHovered || Submenu?.IsContextMenuOpen == true;", menuBarSource);
        Assert.Contains("ContentWrapper.IsSelected = isHighlighted;", menuBarSource);
        Assert.Contains("ContentWrapper.SpoofIsHoveredWhileDrawingBackground = isHighlighted && !isPressed;", menuBarSource);
        Assert.Contains("ContentWrapper.SpoofIsPressedWhileDrawingBackground = isPressed;", menuBarSource);
        Assert.Contains("wrapperBorder.IsSelected = isHighlighted;", menuBarSource);
        Assert.Contains("wrapperBorder.SpoofIsHoveredWhileDrawingBackground = isHighlighted && !isPressed;", menuBarSource);
        Assert.Contains("wrapperBorder.SpoofIsPressedWhileDrawingBackground = isPressed;", menuBarSource);
        Assert.Contains("<ThemeDefinition Name=\"Dark_Blue\" IsBuiltIn=\"True\">", builtInThemesSource);
        Assert.Contains("<ThemeBackgroundDefinition ElementType=\"MenuBarItem\">", builtInThemesSource);
        Assert.Contains("SelectedValue=\"rgba(89,159,228,120)\"", builtInThemesSource);
    }

    [Fact]
    public void ContextMenuItem_Projects_Highlight_From_Owner_And_Wrapper_States()
    {
        string contextMenuItemSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGContextMenuItem.cs");
        string contextMenuSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGContextMenu.cs");
        string builtInThemesSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Themes\BuiltInThemes.xaml");

        Assert.Contains("private MGVisualStateProjection OwnerVisualStateProjection", contextMenuItemSource);
        Assert.Contains("OwnerVisualStateProjection = new(this, (_, __) => ApplyProjectedHighlightState());", contextMenuItemSource);
        Assert.Contains("ContentWrapperVisualStateProjection = new(ContentWrapper, (_, __) => ApplyProjectedHighlightState());", contextMenuItemSource);
        Assert.Contains("bool isHighlighted = ownerState.IsPressedOrHovered || wrapperState.IsPressedOrHovered", contextMenuItemSource);
        Assert.Contains("|| ownerState.IsSelected || Submenu?.IsContextMenuOpen == true;", contextMenuItemSource);
        Assert.Contains("Button.GetBorder().BackgroundBrush = background?.Copy();", contextMenuSource);
        Assert.Contains("ThemeContextMenuItemSettingsDefinition.HeaderBackground", builtInThemesSource);
        Assert.Contains("<ThemeDefinition Name=\"Dark_Blue\" IsBuiltIn=\"True\">", builtInThemesSource);
        Assert.Contains("SelectedValue=\"rgba(89,159,228,120)\"", builtInThemesSource);
        Assert.Contains("SelectedValue=\"rgba(188,202,218,190)\"", builtInThemesSource);
        Assert.Contains("FocusedColor=\"rgba(188,202,218,190)\"", builtInThemesSource);
        Assert.Contains("ContentWrapper.IsSelected = isHighlighted;", contextMenuItemSource);
        Assert.Contains("ContentWrapper.GetBorder().IsSelected = isHighlighted;", contextMenuItemSource);
        Assert.Contains("ContentWrapper.GetBorder().SpoofIsHoveredWhileDrawingBackground = isHighlighted && !isPressed;", contextMenuItemSource);
        Assert.Contains("ContentWrapper.GetBorder().SpoofIsPressedWhileDrawingBackground = isPressed;", contextMenuItemSource);
    }

    [Fact]
    public void Expander_Initializes_Arrow_Color_From_Current_Theme()
    {
        string expanderSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGExpander.cs");

        Assert.Contains("ExpanderDropdownArrowColor = GetTheme().DropdownArrowColor;", expanderSource);
        Assert.Contains("ExpanderDropdownArrowColor = CurrentTheme.DropdownArrowColor;", expanderSource);
    }

    [Fact]
    public void Control_Template_Xaml_Can_Declare_Detached_Roots()
    {
        const string markup = @"
<ControlTemplate xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Name=""Detached.Chrome"">
    <ControlTemplate.Root>
        <Border Name=""PART_Root"" />
    </ControlTemplate.Root>
    <ControlTemplate.DetachedRoots>
        <ContentPresenter Name=""PART_FloatingHeader"" />
        <ScrollViewer Name=""PART_FloatingScroller"" />
    </ControlTemplate.DetachedRoots>
    <ControlTemplate.Parts>
        <TemplatePart Name=""PART_Root"" />
        <TemplatePart Name=""PART_FloatingHeader"" />
        <TemplatePart Name=""PART_FloatingScroller"" />
    </ControlTemplate.Parts>
</ControlTemplate>";

        ControlTemplateDefinition definition = ControlTemplateLoader
            .ParseDefinitions(XamlDocumentSource.FromString(markup, "DetachedTemplate.xaml"))
            .Single();

        Assert.NotNull(definition.Root);
        Assert.Equal(2, definition.DetachedRoots.Count);
        Assert.Contains(definition.Parts, x => x.Name == "PART_FloatingHeader");
        Assert.Contains(definition.Parts, x => x.Name == "PART_FloatingScroller");
        }
}