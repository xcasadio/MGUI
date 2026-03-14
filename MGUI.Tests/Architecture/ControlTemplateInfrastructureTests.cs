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
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.OverlayTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ContextMenuTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ContextMenuItemTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ListBoxTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ListViewTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.ComboBoxTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.TreeViewTemplateName, out _));
        Assert.True(resources.TryGetControlTemplate(MGControlTemplateCatalog.TabControlTemplateName, out _));
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
        Assert.True(resources.ControlTemplates[MGControlTemplateCatalog.TabControlTemplateName].SupportsStructure);
    }

    [Fact]
    public void Xaml_Element_Exposes_Control_Template_Name()
    {
        Assert.Equal(typeof(string), typeof(Element).GetProperty(nameof(Element.ControlTemplate))?.PropertyType);
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
        Assert.Contains("!IsThemeRefresh", source);
        Assert.Contains("AttachControlTemplateStructure(Structure)", source);
    }

    [Fact]
    public void Control_Template_Requirement_Metadata_Is_Declared_For_Migrating_Controls()
    {
        Assert.Equal(typeof(MGWindow), typeof(MGWindow).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGContextMenu), typeof(MGContextMenu).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGOverlay), typeof(MGOverlay).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGListBox<>), typeof(MGListBox<>).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGListView<>), typeof(MGListView<>).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGTabControl), typeof(MGTabControl).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
        Assert.Equal(typeof(MGComboBox<>), typeof(MGComboBox<>).GetMethod("GetRequiredControlTemplateParts", BindingFlags.Instance | BindingFlags.NonPublic)?.DeclaringType);
    }

    [Fact]
    public void Control_Template_Value_Application_Uses_Template_Source_Metadata()
    {
        string source = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Styling\MGControlTemplate.cs");

        Assert.Contains("UIValueResolutionSource.Template", source);
        Assert.Contains("Owner.InvalidateLayout();", source);
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
    public void Built_In_Control_Template_Xaml_Defines_ListBox_And_ListView_Structures()
    {
        const string resourceName = "MGUI.Core.UI.Templates.BuiltInControlTemplates.xaml";
        string markup = GeneralUtils.ReadEmbeddedResourceAsString(typeof(MGControlTemplateCatalog).Assembly, resourceName);

        IReadOnlyList<ControlTemplateDefinition> definitions = ControlTemplateLoader.ParseDefinitions(XamlDocumentSource.FromString(markup, resourceName));

        Assert.Contains(definitions, x => x.Name == MGControlTemplateCatalog.ListBoxTemplateName);
        Assert.Contains(definitions, x => x.Name == MGControlTemplateCatalog.ListViewTemplateName);
        Assert.Contains(definitions.Single(x => x.Name == MGControlTemplateCatalog.ListBoxTemplateName).Parts, x => x.Name == MGListBox<object>.ItemsPanelPartName);
        Assert.Contains(definitions.Single(x => x.Name == MGControlTemplateCatalog.ListViewTemplateName).Parts, x => x.Name == MGListView<object>.DataGridPartName);
    }
}