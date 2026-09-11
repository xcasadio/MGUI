using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Docking;

/// <summary>
/// Backlog task 9 (styling-theme-tasks.md), scenario <c>SCN-DOCK-001</c>: the docking tab item, tab group, auto-hide drawer and host consume a structural
/// control template at runtime. They declare their frozen parts (ADR-0002), the catalog template creates them, the control attaches them and keeps its
/// behaviour (clicks, drag, docking orchestration). The tab group's compact buttons are not parts; their hover and icon colors come from the theme.
/// </summary>
public class DockCompositeStructuralTemplateTests
{
    [Fact]
    public void The_Tab_Item_Group_Drawer_And_Host_Take_Their_Parts_From_Their_Structural_Template()
    {
        Harness harness = Harness.Create();

        AssertStructuralParts(new MGDockTabItem(harness.Window, new DockPanelNode { Title = "A" }), MGControlTemplateCatalog.DockTabItemTemplateName,
            (MGDockTabItem.SurfacePartName, typeof(MGBorder)),
            (MGDockTabItem.AccentPartName, typeof(MGRectangle)),
            (MGDockTabItem.TitleTextPartName, typeof(MGTextBlock)),
            (MGDockTabItem.CloseButtonPartName, typeof(MGBorder)),
            (MGDockTabItem.CloseIconPartName, typeof(MGCloseIcon)),
            (MGDockTabItem.PinButtonPartName, typeof(MGBorder)),
            (MGDockTabItem.PinIconPartName, typeof(MGDockPinIcon)));
        AssertStructuralParts(new MGDockTabGroup(harness.Window), MGControlTemplateCatalog.DockTabGroupTemplateName,
            (MGDockTabGroup.HeadersPanelPartName, typeof(MGStackPanel)),
            (MGDockTabGroup.AccentPartName, typeof(MGRectangle)),
            (MGDockTabGroup.DropdownIconPartName, typeof(MGEllipsisIcon)),
            (MGDockTabGroup.WindowStateIconPartName, typeof(MGWindowStateIcon)));
        AssertStructuralParts(new MGDockAutoHideDrawer(harness.Window), MGControlTemplateCatalog.DockAutoHideDrawerTemplateName,
            (MGDockAutoHideDrawer.BorderPartName, typeof(MGBorder)),
            (MGDockAutoHideDrawer.TitleBarPartName, typeof(MGBorder)),
            (MGDockAutoHideDrawer.TitleBarTextPartName, typeof(MGTextBlock)),
            (MGDockAutoHideDrawer.PinButtonPartName, typeof(MGBorder)),
            (MGDockAutoHideDrawer.CloseButtonPartName, typeof(MGBorder)),
            (MGDockAutoHideDrawer.PinIconPartName, typeof(MGDockPinIcon)),
            (MGDockAutoHideDrawer.CloseIconPartName, typeof(MGCloseIcon)),
            (MGDockAutoHideDrawer.ResizeGripPartName, typeof(MGBorder)));
        AssertStructuralParts(new MGDockHost(harness.Window), MGControlTemplateCatalog.DockHostTemplateName,
            (MGDockHost.PreviewOverlayPartName, typeof(MGDockPreviewOverlay)),
            (MGDockHost.DropIndicatorsPartName, typeof(MGDockDropIndicators)),
            (MGDockHost.LeftAutoHideStripPartName, typeof(MGDockAutoHideStrip)),
            (MGDockHost.RightAutoHideStripPartName, typeof(MGDockAutoHideStrip)),
            (MGDockHost.TopAutoHideStripPartName, typeof(MGDockAutoHideStrip)),
            (MGDockHost.BottomAutoHideStripPartName, typeof(MGDockAutoHideStrip)),
            (MGDockHost.AutoHideDrawerPartName, typeof(MGDockAutoHideDrawer)));
    }

    [Fact]
    public void A_Replaced_Tab_Item_Structure_Detaches_Its_Previous_Children_And_Keeps_The_Title()
    {
        Harness harness = Harness.Create();
        MGDockTabItem tab = new(harness.Window, new DockPanelNode { Title = "Alpha", CanClose = true, CanAutoHide = true });
        MGElement defaultTitle = tab.TemplateParts[MGDockTabItem.TitleTextPartName];
        MGElement defaultCloseButton = tab.TemplateParts[MGDockTabItem.CloseButtonPartName];
        MGElement defaultSurface = tab.TemplateParts[MGDockTabItem.SurfacePartName];
        int componentCount = ComponentElements(tab).Length;

        harness.Desktop.Resources.AddControlTemplate(new MGControlTemplate("Test.DockTabItem", context =>
        {
            MGControlTemplateStructure structure = new(null);
            structure.AddPart(MGDockTabItem.SurfacePartName, new MGBorder(harness.Window));
            structure.AddPart(MGDockTabItem.AccentPartName, new MGRectangle(harness.Window, 0, 0, Color.Transparent, 0, Color.Transparent));
            structure.AddPart(MGDockTabItem.TitleTextPartName, new MGTextBlock(harness.Window, ""));
            structure.AddPart(MGDockTabItem.CloseButtonPartName, new MGBorder(harness.Window));
            structure.AddPart(MGDockTabItem.CloseIconPartName, new MGCloseIcon(harness.Window));
            structure.AddPart(MGDockTabItem.PinButtonPartName, new MGBorder(harness.Window));
            structure.AddPart(MGDockTabItem.PinIconPartName, new MGDockPinIcon(harness.Window));
            return structure;
        }, null, _ => { }));

        tab.ControlTemplateName = "Test.DockTabItem";

        Assert.Null(tab.LastControlTemplateError);
        MGTextBlock title = Assert.IsType<MGTextBlock>(tab.TemplateParts[MGDockTabItem.TitleTextPartName]);
        MGElement closeButton = tab.TemplateParts[MGDockTabItem.CloseButtonPartName];
        Assert.NotSame(defaultTitle, title);
        Assert.Null(defaultTitle.Parent);
        Assert.Null(defaultCloseButton.Parent);
        Assert.Same(tab, title.Parent);
        Assert.Same(tab, closeButton.Parent);
        Assert.False(title.IsHitTestVisible);
        Assert.False(tab.TemplateParts[MGDockTabItem.PinButtonPartName].IsHitTestVisible);
        Assert.Equal("Alpha", title.Text);
        Assert.Contains(title, tab.GetChildren());
        Assert.DoesNotContain(defaultTitle, tab.GetChildren());
        Assert.Equal(componentCount, ComponentElements(tab).Length);
        Assert.DoesNotContain(defaultSurface, ComponentElements(tab));
    }

    [Fact]
    public void The_Tab_Group_Header_Colors_Come_From_The_Theme_And_Follow_A_Theme_Change()
    {
        string fontFamily = Harness.Create().Desktop.DefaultFontFamily;
        Assert.Equal(new Color(28, 52, 102), new MGTheme(MGTheme.BuiltInTheme.Dark_Blue, fontFamily).Docking.TabGroupButtonHoverColor);
        Assert.Equal(new Color(200, 200, 200), new MGTheme(MGTheme.BuiltInTheme.Dark_Blue, fontFamily).Docking.TabGroupIconColor);
        Assert.Equal(new Color(62, 62, 66), new MGTheme(MGTheme.BuiltInTheme.Dark, fontFamily).Docking.TabGroupButtonHoverColor);
        Assert.Equal(new Color(170, 170, 170), new MGTheme(MGTheme.BuiltInTheme.Dark, fontFamily).Docking.TabGroupIconColor);
        Assert.Equal(new Color(188, 202, 218, 190), new MGTheme(MGTheme.BuiltInTheme.Light_Gray, fontFamily).Docking.TabGroupButtonHoverColor);
        Assert.Equal(new Color(64, 64, 64), new MGTheme(MGTheme.BuiltInTheme.Light_Gray, fontFamily).Docking.TabGroupIconColor);

        DockHarness harness = DockHarness.Create();
        MGDockTabGroup group = harness.TabGroup;
        AssertHeaderColors(group, harness.Window.GetTheme());

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, fontFamily);
        Assert.Equal(dark.Docking.TabGroupIconColor, dark.Copy().Docking.TabGroupIconColor);
        harness.Window.GetResources().DefaultTheme = dark;
        AssertHeaderColors(group, dark);
    }

    [Fact]
    public void A_Replaced_Tab_Group_Headers_Panel_Receives_The_Tabs()
    {
        DockHarness harness = DockHarness.Create();
        MGDockTabGroup group = harness.TabGroup;
        MGStackPanel defaultPanel = Assert.IsType<MGStackPanel>(group.TemplateParts[MGDockTabGroup.HeadersPanelPartName]);
        Assert.Equal(2, defaultPanel.GetChildren().OfType<MGDockTabItem>().Count());

        MGStackPanel customPanel = null;
        harness.Desktop.Resources.AddControlTemplate(new MGControlTemplate("Test.DockTabGroup", context =>
        {
            MGControlTemplateStructure structure = new(null);
            structure.AddPart(MGDockTabGroup.HeadersPanelPartName, customPanel = new MGStackPanel(harness.Window, Orientation.Horizontal));
            structure.AddPart(MGDockTabGroup.AccentPartName, new MGRectangle(harness.Window, 0, 0, Color.Transparent, 0, Color.Transparent));
            structure.AddPart(MGDockTabGroup.DropdownIconPartName, new MGEllipsisIcon(harness.Window));
            structure.AddPart(MGDockTabGroup.WindowStateIconPartName, new MGWindowStateIcon(harness.Window));
            return structure;
        }, null, _ => { }));

        group.ControlTemplateName = "Test.DockTabGroup";

        Assert.Null(group.LastControlTemplateError);
        Assert.Same(customPanel, group.TemplateParts[MGDockTabGroup.HeadersPanelPartName]);
        Assert.Same(group, customPanel.Parent);
        Assert.Null(defaultPanel.Parent);
        Assert.Empty(defaultPanel.GetChildren());
        Assert.Equal(new[] { "A", "B" }, customPanel.GetChildren().OfType<MGDockTabItem>().Select(tab => tab.Panel.Title));
        Assert.Contains(customPanel, group.GetChildren());
    }

    [Fact]
    public void A_Replaced_Drawer_Structure_Detaches_Its_Previous_Parts_And_Keeps_The_Active_Title()
    {
        Harness harness = Harness.Create();
        MGDockAutoHideDrawer drawer = new(harness.Window) { ActivePanel = new DockPanelNode { Title = "Output" } };
        MGElement defaultTitleBar = drawer.TemplateParts[MGDockAutoHideDrawer.TitleBarPartName];
        MGElement defaultPinButton = drawer.TemplateParts[MGDockAutoHideDrawer.PinButtonPartName];

        harness.Desktop.Resources.AddControlTemplate(new MGControlTemplate("Test.DockAutoHideDrawer", context =>
        {
            MGControlTemplateStructure structure = new(null);
            structure.AddPart(MGDockAutoHideDrawer.BorderPartName, new MGBorder(harness.Window));
            structure.AddPart(MGDockAutoHideDrawer.TitleBarPartName, new MGBorder(harness.Window));
            structure.AddPart(MGDockAutoHideDrawer.TitleBarTextPartName, new MGTextBlock(harness.Window, ""));
            structure.AddPart(MGDockAutoHideDrawer.PinButtonPartName, new MGBorder(harness.Window));
            structure.AddPart(MGDockAutoHideDrawer.CloseButtonPartName, new MGBorder(harness.Window));
            structure.AddPart(MGDockAutoHideDrawer.PinIconPartName, new MGDockPinIcon(harness.Window));
            structure.AddPart(MGDockAutoHideDrawer.CloseIconPartName, new MGCloseIcon(harness.Window));
            structure.AddPart(MGDockAutoHideDrawer.ResizeGripPartName, new MGBorder(harness.Window));
            return structure;
        }, null, _ => { }));

        drawer.ControlTemplateName = "Test.DockAutoHideDrawer";

        Assert.Null(drawer.LastControlTemplateError);
        Assert.Null(defaultTitleBar.Parent);
        Assert.Null(defaultPinButton.Parent);
        MGTextBlock title = Assert.IsType<MGTextBlock>(drawer.TemplateParts[MGDockAutoHideDrawer.TitleBarTextPartName]);
        Assert.Same(drawer, title.Parent);
        Assert.False(title.IsHitTestVisible);
        Assert.Equal("Output", title.Text);
        Assert.Same(drawer, drawer.TemplateParts[MGDockAutoHideDrawer.ResizeGripPartName].ManagedParent);
        Assert.Equal(9, drawer.GetChildren().Count());
        Assert.DoesNotContain(defaultTitleBar, drawer.GetChildren());
    }

    [Fact]
    public void A_Replaced_Host_Structure_Rebinds_Every_Surface()
    {
        Harness harness = Harness.Create();
        MGDockHost host = new(harness.Window);
        MGElement defaultOverlay = host.TemplateParts[MGDockHost.PreviewOverlayPartName];
        MGElement defaultDrawer = host.TemplateParts[MGDockHost.AutoHideDrawerPartName];
        int surfaceCount = ComponentElements(host).Count(IsHostSurface);
        Assert.Equal(7, surfaceCount);

        harness.Desktop.Resources.AddControlTemplate(new MGControlTemplate("Test.DockHost", context =>
        {
            // Every strip is created for the left edge: the host gives each strip the side of its part.
            MGControlTemplateStructure structure = new(null);
            structure.AddPart(MGDockHost.PreviewOverlayPartName, new MGDockPreviewOverlay(harness.Window));
            structure.AddPart(MGDockHost.DropIndicatorsPartName, new MGDockDropIndicators(harness.Window));
            structure.AddPart(MGDockHost.LeftAutoHideStripPartName, new MGDockAutoHideStrip(harness.Window, AutoHideSide.Left));
            structure.AddPart(MGDockHost.RightAutoHideStripPartName, new MGDockAutoHideStrip(harness.Window, AutoHideSide.Left));
            structure.AddPart(MGDockHost.TopAutoHideStripPartName, new MGDockAutoHideStrip(harness.Window, AutoHideSide.Left));
            structure.AddPart(MGDockHost.BottomAutoHideStripPartName, new MGDockAutoHideStrip(harness.Window, AutoHideSide.Left));
            structure.AddPart(MGDockHost.AutoHideDrawerPartName, new MGDockAutoHideDrawer(harness.Window));
            return structure;
        }, null, _ => { }));

        host.ControlTemplateName = "Test.DockHost";

        Assert.Null(host.LastControlTemplateError);
        MGElement[] surfaces = ComponentElements(host).Where(IsHostSurface).ToArray();
        Assert.Equal(surfaceCount, surfaces.Length);
        Assert.DoesNotContain(defaultOverlay, surfaces);
        Assert.DoesNotContain(defaultDrawer, surfaces);
        Assert.Contains(host.TemplateParts[MGDockHost.PreviewOverlayPartName], surfaces);
        Assert.Equal(AutoHideSide.Right, ((MGDockAutoHideStrip)host.TemplateParts[MGDockHost.RightAutoHideStripPartName]).Side);
        Assert.Equal(AutoHideSide.Top, ((MGDockAutoHideStrip)host.TemplateParts[MGDockHost.TopAutoHideStripPartName]).Side);
        Assert.Equal(AutoHideSide.Bottom, ((MGDockAutoHideStrip)host.TemplateParts[MGDockHost.BottomAutoHideStripPartName]).Side);
        Assert.Equal(Visibility.Collapsed, host.TemplateParts[MGDockHost.AutoHideDrawerPartName].Visibility);
        Assert.Equal(Visibility.Collapsed, host.TemplateParts[MGDockHost.LeftAutoHideStripPartName].Visibility);
    }

    [Fact]
    public void The_Dark_Composite_Variants_Keep_The_Structure_Of_Their_Base_Template()
    {
        Harness harness = Harness.Create();
        (MGElement Control, string Variant, string PartName)[] cases =
        {
            (new MGDockTabItem(harness.Window, new DockPanelNode { Title = "A" }), "Dark.DockTabItem", MGDockTabItem.TitleTextPartName),
            (new MGDockTabGroup(harness.Window), "Dark.DockTabGroup", MGDockTabGroup.HeadersPanelPartName),
            (new MGDockAutoHideDrawer(harness.Window), "Dark.DockAutoHideDrawer", MGDockAutoHideDrawer.TitleBarPartName),
            (new MGDockHost(harness.Window), "Dark.DockHost", MGDockHost.DropIndicatorsPartName),
        };

        foreach ((MGElement control, string variant, string partName) in cases)
        {
            Assert.True(control.TryGetTemplatePart(partName, out MGElement defaultPart));

            control.ControlTemplateName = variant;

            Assert.Null(control.LastControlTemplateError);
            Assert.Equal(variant, control.AppliedControlTemplateName);
            Assert.True(control.TryGetTemplatePart(partName, out MGElement variantPart), $"{variant} did not provide {partName}");
            // The variant reuses the structure of its base template: switching to it keeps the instantiated parts.
            Assert.Same(defaultPart, variantPart);
        }
    }

    [Fact]
    public void A_Theme_Switch_To_The_Dark_Variants_Keeps_The_Instantiated_Parts()
    {
        // The Dark theme maps these controls to bare Dark.Dock* variants, which share the structure template of their base.
        (Func<MGWindow, MGElement> Create, string Variant, string PartName)[] cases =
        {
            (window => new MGDockTabItem(window, new DockPanelNode { Title = "A" }), "Dark.DockTabItem", MGDockTabItem.TitleTextPartName),
            (window => new MGDockAutoHideDrawer(window), "Dark.DockAutoHideDrawer", MGDockAutoHideDrawer.BorderPartName),
            (window => new MGDockAutoHideStrip(window, AutoHideSide.Left), "Dark.DockAutoHideStrip", MGDockAutoHideStrip.SeparatorPartName),
            (window => new MGDockSplitterBar(window), "Dark.DockSplitter", MGDockSplitterBar.GripPartName),
            (window => new MGDockDropIndicators(window), "Dark.DockDropIndicators", MGDockDropIndicators.CenterDropZonePartName),
        };

        foreach ((Func<MGWindow, MGElement> create, string variant, string partName) in cases)
        {
            Harness harness = Harness.Create();
            MGElement control = create(harness.Window);
            harness.Window.SetContent(control);
            harness.Desktop.Windows.Add(harness.Window);
            Assert.NotEqual(variant, control.AppliedControlTemplateName);
            Assert.True(control.TryGetTemplatePart(partName, out MGElement part));

            harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);

            Assert.Null(control.LastControlTemplateError);
            Assert.Equal(variant, control.AppliedControlTemplateName);
            Assert.True(control.TryGetTemplatePart(partName, out MGElement partAfterSwitch));
            Assert.Same(part, partAfterSwitch);
            Assert.Contains(part, control.GetChildren().Concat(ComponentElements(control)));
        }
    }

    [Fact]
    public void A_Drawer_Created_Under_The_Dark_Theme_Takes_Its_Parts_From_The_Dark_Variant()
    {
        Harness harness = Harness.Create();
        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);

        MGDockAutoHideDrawer drawer = new(harness.Window);

        Assert.Null(drawer.LastControlTemplateError);
        Assert.Equal("Dark.DockAutoHideDrawer", drawer.AppliedControlTemplateName);
        Assert.Same(drawer, drawer.TemplateParts[MGDockAutoHideDrawer.TitleBarPartName].Parent);
        Assert.Equal(9, drawer.GetChildren().Count());
    }

    private static void AssertHeaderColors(MGDockTabGroup group, MGTheme theme)
    {
        Assert.Equal(theme.Docking.TabGroupIconColor, group.IconColor);
        Assert.Equal(theme.Docking.TabGroupButtonHoverColor, group.CompactButtonHoverColor);
        Assert.Equal(group.IconColor, ((MGEllipsisIcon)group.TemplateParts[MGDockTabGroup.DropdownIconPartName]).Color);
        Assert.Equal(group.IconColor, ((MGWindowStateIcon)group.TemplateParts[MGDockTabGroup.WindowStateIconPartName]).Color);

        // The overflow and maximize/restore buttons are the two borders among the group's children.
        MGBorder[] compactButtons = group.GetChildren().OfType<MGBorder>().ToArray();
        Assert.Equal(2, compactButtons.Length);
        Assert.All(compactButtons, button => Assert.Equal(theme.Docking.TabGroupButtonHoverColor, button.BackgroundBrush.FocusedColor));
    }

    private static bool IsHostSurface(MGElement element)
        => element is MGDockPreviewOverlay or MGDockDropIndicators or MGDockAutoHideStrip or MGDockAutoHideDrawer;

    /// <summary>The elements the visual tree reaches through <paramref name="owner"/>'s components and their descendants:
    /// <see cref="MGElement.EnumerateVisualTree"/> only includes components together with the element itself, which is then left out.</summary>
    private static MGElement[] ComponentElements(MGElement owner)
        => owner.EnumerateVisualTree(true).Where(element => !ReferenceEquals(element, owner)).ToArray();

    private static void AssertStructuralParts(MGElement control, string templateName, params (string Name, Type Type)[] parts)
    {
        Assert.Null(control.LastControlTemplateError);
        Assert.Equal(templateName, control.AppliedControlTemplateName);
        Assert.True(control.GetResources().TryGetControlTemplate(templateName, out MGControlTemplate template));
        Assert.True(template.SupportsStructure);
        Assert.Equal(parts.Select(part => part.Name).OrderBy(name => name, StringComparer.Ordinal),
            control.GetRequiredControlTemplateParts().Select(requirement => requirement.Name).OrderBy(name => name, StringComparer.Ordinal));

        foreach ((string name, Type type) in parts)
        {
            Assert.True(control.TryGetTemplatePart(name, out MGElement part), $"{control.GetType().Name} has no {name}");
            Assert.IsType(type, part);
        }
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 640, 480) { WindowStyle = WindowStyle.None };
            return new(runtime, desktop, window);
        }
    }

    /// <summary>A host showing one tab group with the panels "A" and "B", laid out.</summary>
    private sealed record DockHarness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGDockHost Host, MGDockTabGroup TabGroup)
    {
        public static DockHarness Create()
        {
            Harness harness = Harness.Create();
            DockTabGroupNode group = new();
            // Text content, so that the only borders among the tab group's children are its two compact buttons.
            group.AddPanel(new DockPanelNode { Title = "A", ContentFactory = () => new MGTextBlock(harness.Window, "Content A") }, -1);
            group.AddPanel(new DockPanelNode { Title = "B", ContentFactory = () => new MGTextBlock(harness.Window, "Content B") }, -1);
            MGDockHost host = new(harness.Window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                LayoutModel = new DockLayoutModel(group),
            };
            harness.Window.SetContent(host);
            harness.Desktop.Windows.Add(harness.Window);
            for (int frame = 0; frame < 2; frame++)
            {
                MouseState mouse = new(2000, 2000, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                harness.Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frame + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
                harness.Desktop.Update();
            }

            MGDockTabGroup tabGroup = host.TraverseVisualTree<MGDockTabGroup>(IncludeSelf: false).Single();
            return new(harness.Runtime, harness.Desktop, harness.Window, host, tabGroup);
        }
    }
}
