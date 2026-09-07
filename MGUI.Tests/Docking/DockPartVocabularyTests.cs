using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Input;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Docking;

/// <summary>
/// Pins the frozen docking part vocabulary (task 3 of Docs/Tasks/styling-theme-tasks.md,
/// "Converger le vocabulaire des parts docking") so that a future rename of a
/// <c>PART_*</c> constant is a deliberate, reviewed test change rather than a silent drift.
/// </summary>
public class DockPartVocabularyTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static MGWindow CreateWindow()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 480, 320));
        var desktop = new MGDesktop(runtime);
        return new MGWindow(desktop, 0, 0, 400, 240)
        {
            WindowStyle = WindowStyle.None,
        };
    }

    // ── (a) Vocabulary pin ──────────────────────────────────────────────

    [Fact]
    public void MGDockAutoHideDrawer_Declares_Frozen_Part_Vocabulary()
    {
        Assert.Equal("PART_Border", MGDockAutoHideDrawer.BorderPartName);
        Assert.Equal("PART_TitleBar", MGDockAutoHideDrawer.TitleBarPartName);
        Assert.Equal("PART_TitleBarText", MGDockAutoHideDrawer.TitleBarTextPartName);
        Assert.Equal("PART_PinButton", MGDockAutoHideDrawer.PinButtonPartName);
        Assert.Equal("PART_CloseButton", MGDockAutoHideDrawer.CloseButtonPartName);
        Assert.Equal("PART_PinIcon", MGDockAutoHideDrawer.PinIconPartName);
        Assert.Equal("PART_CloseIcon", MGDockAutoHideDrawer.CloseIconPartName);
        Assert.Equal("PART_ResizeGrip", MGDockAutoHideDrawer.ResizeGripPartName);
    }

    [Fact]
    public void MGDockTabItem_Declares_Frozen_Part_Vocabulary()
    {
        Assert.Equal("PART_Surface", MGDockTabItem.SurfacePartName);
        Assert.Equal("PART_Accent", MGDockTabItem.AccentPartName);
        Assert.Equal("PART_CloseIcon", MGDockTabItem.CloseIconPartName);
        Assert.Equal("PART_PinIcon", MGDockTabItem.PinIconPartName);
        Assert.Equal("PART_TitleText", MGDockTabItem.TitleTextPartName);
        Assert.Equal("PART_CloseButton", MGDockTabItem.CloseButtonPartName);
        Assert.Equal("PART_PinButton", MGDockTabItem.PinButtonPartName);
    }

    [Fact]
    public void MGDockTabGroup_Declares_Frozen_Part_Vocabulary()
    {
        Assert.Equal("PART_Accent", MGDockTabGroup.AccentPartName);
        Assert.Equal("PART_DropdownIcon", MGDockTabGroup.DropdownIconPartName);
        Assert.Equal("PART_WindowStateIcon", MGDockTabGroup.WindowStateIconPartName);
        Assert.Equal("PART_HeadersPanel", MGDockTabGroup.HeadersPanelPartName);
    }

    [Fact]
    public void MGDockDropIndicators_Declares_Frozen_Part_Vocabulary()
    {
        Assert.Equal("PART_LeftDropZone", MGDockDropIndicators.LeftDropZonePartName);
        Assert.Equal("PART_RightDropZone", MGDockDropIndicators.RightDropZonePartName);
        Assert.Equal("PART_TopDropZone", MGDockDropIndicators.TopDropZonePartName);
        Assert.Equal("PART_BottomDropZone", MGDockDropIndicators.BottomDropZonePartName);
        Assert.Equal("PART_CenterDropZone", MGDockDropIndicators.CenterDropZonePartName);
        Assert.Equal("PART_HostLeftDropZone", MGDockDropIndicators.HostLeftDropZonePartName);
        Assert.Equal("PART_HostRightDropZone", MGDockDropIndicators.HostRightDropZonePartName);
        Assert.Equal("PART_HostTopDropZone", MGDockDropIndicators.HostTopDropZonePartName);
        Assert.Equal("PART_HostBottomDropZone", MGDockDropIndicators.HostBottomDropZonePartName);
    }

    [Fact]
    public void Untouched_Docking_Controls_Keep_Their_Existing_Part_Vocabulary()
    {
        Assert.Equal("PART_Surface", MGDockSplitterBar.SurfacePartName);
        Assert.Equal("PART_Accent", MGDockSplitterBar.AccentPartName);
        Assert.Equal("PART_Grip", MGDockSplitterBar.GripPartName);

        Assert.Equal("PART_Surface", MGDockPreviewOverlay.SurfacePartName);
        Assert.Equal("PART_Border", MGDockPreviewOverlay.BorderPartName);

        Assert.Equal("PART_Separator", MGDockAutoHideStrip.SeparatorPartName);

        Assert.Equal("PART_PreviewOverlay", MGDockHost.PreviewOverlayPartName);
        Assert.Equal("PART_DropIndicators", MGDockHost.DropIndicatorsPartName);
        Assert.Equal("PART_LeftAutoHideStrip", MGDockHost.LeftAutoHideStripPartName);
        Assert.Equal("PART_RightAutoHideStrip", MGDockHost.RightAutoHideStripPartName);
        Assert.Equal("PART_TopAutoHideStrip", MGDockHost.TopAutoHideStripPartName);
        Assert.Equal("PART_BottomAutoHideStrip", MGDockHost.BottomAutoHideStripPartName);
        Assert.Equal("PART_AutoHideDrawer", MGDockHost.AutoHideDrawerPartName);
    }

    // ── (c) Backward-compatible aliases ──────────────────────────────────

    [Fact]
#pragma warning disable CS0618 // Testing the obsolete alias intentionally.
    public void MGDockAutoHideDrawer_Obsolete_Aliases_Match_New_Part_Names()
    {
        Assert.Equal(MGDockAutoHideDrawer.TitleBarPartName, MGDockAutoHideDrawer.HeaderPartName);
        Assert.Equal(MGDockAutoHideDrawer.TitleBarTextPartName, MGDockAutoHideDrawer.TitleLabelPartName);
    }
#pragma warning restore CS0618

    // ── (b) Registration ──────────────────────────────────────────────────

    [Fact]
    public void MGDockAutoHideDrawer_Registers_TitleBar_Parts_Under_New_Names_Only()
    {
        MGWindow window = CreateWindow();
        var drawer = new MGDockAutoHideDrawer(window);

        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.TitleBarPartName, out MGElement titleBar));
        Assert.IsType<MGBorder>(titleBar);

        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.TitleBarTextPartName, out MGElement titleBarText));
        Assert.IsType<MGTextBlock>(titleBarText);

        Assert.False(drawer.TemplateParts.ContainsKey("PART_Header"));
        Assert.False(drawer.TemplateParts.ContainsKey("PART_TitleLabel"));

        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.BorderPartName, out MGElement border));
        Assert.IsType<MGBorder>(border);
        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.PinButtonPartName, out MGElement pinButton));
        Assert.IsType<MGBorder>(pinButton);
        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.CloseButtonPartName, out MGElement closeButton));
        Assert.IsType<MGBorder>(closeButton);
        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.PinIconPartName, out MGElement pinIcon));
        Assert.IsType<MGDockPinIcon>(pinIcon);
        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.CloseIconPartName, out MGElement closeIcon));
        Assert.IsType<MGCloseIcon>(closeIcon);
        Assert.True(drawer.TryGetTemplatePart(MGDockAutoHideDrawer.ResizeGripPartName, out MGElement resizeGrip));
        Assert.IsType<MGBorder>(resizeGrip);
    }

    [Fact]
    public void MGDockTabItem_Registers_All_Seven_Parts()
    {
        MGWindow window = CreateWindow();
        var panel = new DockPanelNode { Title = "Tab" };
        var tabItem = new MGDockTabItem(window, panel);

        Assert.True(tabItem.TryGetTemplatePart(MGDockTabItem.SurfacePartName, out MGElement surface));
        Assert.IsType<MGBorder>(surface);
        Assert.True(tabItem.TryGetTemplatePart(MGDockTabItem.AccentPartName, out MGElement accent));
        Assert.IsType<MGRectangle>(accent);
        Assert.True(tabItem.TryGetTemplatePart(MGDockTabItem.CloseIconPartName, out MGElement closeIcon));
        Assert.IsType<MGCloseIcon>(closeIcon);
        Assert.True(tabItem.TryGetTemplatePart(MGDockTabItem.PinIconPartName, out MGElement pinIcon));
        Assert.IsType<MGDockPinIcon>(pinIcon);
        Assert.True(tabItem.TryGetTemplatePart(MGDockTabItem.TitleTextPartName, out MGElement titleText));
        Assert.IsType<MGTextBlock>(titleText);
        Assert.True(tabItem.TryGetTemplatePart(MGDockTabItem.CloseButtonPartName, out MGElement closeButton));
        Assert.IsType<MGBorder>(closeButton);
        Assert.True(tabItem.TryGetTemplatePart(MGDockTabItem.PinButtonPartName, out MGElement pinButton));
        Assert.IsType<MGBorder>(pinButton);
    }

    [Fact]
    public void MGDockDropIndicators_Registers_All_Nine_Zone_Parts()
    {
        MGWindow window = CreateWindow();
        var indicators = new MGDockDropIndicators(window);

        string[] names =
        {
            MGDockDropIndicators.LeftDropZonePartName,
            MGDockDropIndicators.RightDropZonePartName,
            MGDockDropIndicators.TopDropZonePartName,
            MGDockDropIndicators.BottomDropZonePartName,
            MGDockDropIndicators.CenterDropZonePartName,
            MGDockDropIndicators.HostLeftDropZonePartName,
            MGDockDropIndicators.HostRightDropZonePartName,
            MGDockDropIndicators.HostTopDropZonePartName,
            MGDockDropIndicators.HostBottomDropZonePartName,
        };

        foreach (string name in names)
        {
            Assert.True(indicators.TryGetTemplatePart(name, out MGElement part));
            Assert.IsType<MGDockDropZoneIndicator>(part);
        }
    }

    [Fact]
    public void MGDockTabGroup_Registers_All_Four_Parts()
    {
        MGWindow window = CreateWindow();
        var tabGroup = new MGDockTabGroup(window);

        Assert.True(tabGroup.TryGetTemplatePart(MGDockTabGroup.AccentPartName, out MGElement accent));
        Assert.IsType<MGRectangle>(accent);
        Assert.True(tabGroup.TryGetTemplatePart(MGDockTabGroup.DropdownIconPartName, out MGElement dropdownIcon));
        Assert.IsType<MGEllipsisIcon>(dropdownIcon);
        Assert.True(tabGroup.TryGetTemplatePart(MGDockTabGroup.WindowStateIconPartName, out MGElement windowStateIcon));
        Assert.IsType<MGWindowStateIcon>(windowStateIcon);
        Assert.True(tabGroup.TryGetTemplatePart(MGDockTabGroup.HeadersPanelPartName, out MGElement headersPanel));
        Assert.IsType<MGStackPanel>(headersPanel);
    }
}
