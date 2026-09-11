using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Docking;
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
/// Backlog task 8 (styling-theme-tasks.md), scenario <c>SCN-DOCK-001</c>: the docking leaf controls <see cref="MGDockSplitterBar"/>,
/// <see cref="MGDockDropIndicators"/>, <see cref="MGDockPreviewOverlay"/> and <see cref="MGDockAutoHideStrip"/> consume a structural control
/// template at runtime, like TreeView and TextBox: they declare their parts, the catalog template creates them, the control attaches them.
/// </summary>
public class DockLeafStructuralTemplateTests
{
    [Fact]
    public void The_Four_Leaf_Controls_Take_Their_Parts_From_Their_Structural_Template()
    {
        Harness harness = Harness.Create();

        AssertStructuralParts(new MGDockSplitterBar(harness.Window), MGControlTemplateCatalog.DockSplitterTemplateName,
            (MGDockSplitterBar.SurfacePartName, typeof(MGBorder)),
            (MGDockSplitterBar.AccentPartName, typeof(MGBorder)),
            (MGDockSplitterBar.GripPartName, typeof(MGGripDotsIcon)));
        AssertStructuralParts(new MGDockAutoHideStrip(harness.Window, AutoHideSide.Left), MGControlTemplateCatalog.DockAutoHideStripTemplateName,
            (MGDockAutoHideStrip.SeparatorPartName, typeof(MGRectangle)));
        AssertStructuralParts(new MGDockPreviewOverlay(harness.Window), MGControlTemplateCatalog.DockPreviewOverlayTemplateName,
            (MGDockPreviewOverlay.SurfacePartName, typeof(MGBorder)),
            (MGDockPreviewOverlay.BorderPartName, typeof(MGBorder)));
        AssertStructuralParts(new MGDockDropIndicators(harness.Window), MGControlTemplateCatalog.DockDropIndicatorsTemplateName,
            new[]
            {
                MGDockDropIndicators.LeftDropZonePartName, MGDockDropIndicators.RightDropZonePartName, MGDockDropIndicators.TopDropZonePartName,
                MGDockDropIndicators.BottomDropZonePartName, MGDockDropIndicators.CenterDropZonePartName, MGDockDropIndicators.HostLeftDropZonePartName,
                MGDockDropIndicators.HostRightDropZonePartName, MGDockDropIndicators.HostTopDropZonePartName, MGDockDropIndicators.HostBottomDropZonePartName,
            }.Select(name => (name, typeof(MGDockDropZoneIndicator))).ToArray());
    }

    [Fact]
    public void A_Replacement_Template_Supplies_The_Splitter_Parts_And_Reuses_Its_Component_Slots()
    {
        Harness harness = Harness.Create();
        MGDockSplitterBar splitter = new(harness.Window);
        Assert.True(splitter.TryGetTemplatePart(MGDockSplitterBar.SurfacePartName, out MGElement defaultSurface));
        int partCount = ComponentElements(splitter).Length;
        Assert.Equal(3, partCount);
        Assert.Contains(defaultSurface, ComponentElements(splitter));

        MGBorder customSurface = null;
        harness.Desktop.Resources.AddControlTemplate(new MGControlTemplate("Test.DockSplitter", context =>
        {
            MGControlTemplateStructure structure = new(null);
            structure.AddPart(MGDockSplitterBar.SurfacePartName, customSurface = new MGBorder(harness.Window));
            structure.AddPart(MGDockSplitterBar.AccentPartName, new MGBorder(harness.Window));
            structure.AddPart(MGDockSplitterBar.GripPartName, new MGGripDotsIcon(harness.Window));
            return structure;
        }, null, _ => { }));

        splitter.ControlTemplateName = "Test.DockSplitter";

        Assert.Null(splitter.LastControlTemplateError);
        Assert.True(splitter.TryGetTemplatePart(MGDockSplitterBar.SurfacePartName, out MGElement surface));
        Assert.Same(customSurface, surface);
        Assert.Same(splitter, surface.ManagedParent);
        Assert.False(surface.IsHitTestVisible);
        // The components of the replaced parts are released, not accumulated.
        MGElement[] componentElements = ComponentElements(splitter);
        Assert.Equal(partCount, componentElements.Length);
        Assert.Contains(surface, componentElements);
        Assert.DoesNotContain(defaultSurface, componentElements);

        // The brush of the current state is the splitter's own state, pushed again to the new surface.
        IFillBrush currentStateBrush = splitter.IsDragging ? splitter.PressedBrush : splitter.IsHovered ? splitter.HoverBrush : splitter.NormalBrush;
        Assert.Same(currentStateBrush, customSurface.BackgroundBrush.NormalValue);
    }

    [Fact]
    public void A_Replaced_Drop_Indicator_Structure_Detaches_The_Previous_Zones()
    {
        Harness harness = Harness.Create();
        MGDockDropIndicators indicators = new(harness.Window);
        Assert.True(indicators.TryGetTemplatePart(MGDockDropIndicators.CenterDropZonePartName, out MGElement defaultCenter));
        Assert.Same(indicators, defaultCenter.Parent);

        harness.Desktop.Resources.AddControlTemplate(new MGControlTemplate("Test.DockDropIndicators", context =>
        {
            MGControlTemplateStructure structure = new(null);
            foreach (string partName in indicators.GetRequiredControlTemplateParts().Select(part => part.Name))
                structure.AddPart(partName, new MGDockDropZoneIndicator(harness.Window));
            return structure;
        }, null, _ => { }));

        indicators.ControlTemplateName = "Test.DockDropIndicators";

        Assert.Null(indicators.LastControlTemplateError);
        Assert.True(indicators.TryGetTemplatePart(MGDockDropIndicators.CenterDropZonePartName, out MGElement center));
        Assert.NotSame(defaultCenter, center);
        Assert.Null(defaultCenter.Parent);
        Assert.Same(indicators, center.Parent);
        Assert.Equal(DockZone.Center, ((MGDockDropZoneIndicator)center).Zone);
        Assert.Equal(9, indicators.GetChildren().Count());
        Assert.DoesNotContain(defaultCenter, indicators.GetChildren());
    }

    [Fact]
    public void Drop_Indicators_Keep_Their_Zone_Layout_States_And_Hit_Testing()
    {
        Harness harness = Harness.Create();
        MGDockDropIndicators indicators = new(harness.Window);
        Rectangle target = new(100, 100, 200, 160);
        Point center = target.Center;

        indicators.Show(target);
        indicators.SetDisabledZones(new[] { DockZone.Left });
        indicators.UpdateActiveZone(center);

        Assert.Equal(DockZone.Center, indicators.ActiveZone);
        Assert.True(indicators.TryGetTemplatePart(MGDockDropIndicators.CenterDropZonePartName, out MGElement centerPart));
        MGDockDropZoneIndicator centerZone = (MGDockDropZoneIndicator)centerPart;
        Assert.Equal(Visibility.Visible, centerZone.Visibility);
        Assert.Equal(new Rectangle(center.X - 20, center.Y - 20, 40, 40), centerZone.LayoutBounds);
        Assert.True(centerZone.IsActive);
        Assert.True(indicators.TryGetTemplatePart(MGDockDropIndicators.LeftDropZonePartName, out MGElement leftPart));
        Assert.True(((MGDockDropZoneIndicator)leftPart).IsDisabled);
        Assert.Equal(DockZone.None, indicators.GetZoneAtPosition(leftPart.LayoutBounds.Center));

        indicators.Hide();
        Assert.Equal(Visibility.Collapsed, centerZone.Visibility);
    }

    [Fact]
    public void The_Dark_Variants_Keep_The_Structure_Of_Their_Base_Template()
    {
        Harness harness = Harness.Create();
        (MGElement Control, string Variant, string PartName)[] cases =
        {
            (new MGDockSplitterBar(harness.Window), "Dark.DockSplitter", MGDockSplitterBar.GripPartName),
            (new MGDockAutoHideStrip(harness.Window, AutoHideSide.Bottom), "Dark.DockAutoHideStrip", MGDockAutoHideStrip.SeparatorPartName),
            (new MGDockDropIndicators(harness.Window), "Dark.DockDropIndicators", MGDockDropIndicators.CenterDropZonePartName),
            (new MGDockPreviewOverlay(harness.Window), "Dark.DockPreviewOverlay", MGDockPreviewOverlay.BorderPartName),
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
    public void The_Preview_Overlay_Colors_Come_From_The_Theme_And_Follow_A_Theme_Change()
    {
        Harness harness = Harness.Create();
        string fontFamily = harness.Desktop.DefaultFontFamily;
        Assert.Equal(new Color(0, 122, 204, 100), new MGTheme(MGTheme.BuiltInTheme.Dark_Blue, fontFamily).Docking.PreviewOverlayFillColor);
        Assert.Equal(new Color(0, 122, 204, 200), new MGTheme(MGTheme.BuiltInTheme.Dark_Blue, fontFamily).Docking.PreviewOverlayBorderColor);
        Assert.Equal(new Color(58, 121, 187, 100), new MGTheme(MGTheme.BuiltInTheme.Dark, fontFamily).Docking.PreviewOverlayFillColor);
        Assert.Equal(new Color(58, 121, 187, 200), new MGTheme(MGTheme.BuiltInTheme.Dark, fontFamily).Docking.PreviewOverlayBorderColor);
        Assert.Equal(new Color(152, 171, 191, 100), new MGTheme(MGTheme.BuiltInTheme.Light_Gray, fontFamily).Docking.PreviewOverlayFillColor);
        Assert.Equal(new Color(152, 171, 191, 200), new MGTheme(MGTheme.BuiltInTheme.Light_Gray, fontFamily).Docking.PreviewOverlayBorderColor);

        MGDockHost host = new(harness.Window);
        harness.Show(host);
        Assert.True(host.TryGetTemplatePart(MGDockHost.PreviewOverlayPartName, out MGElement previewPart));
        MGDockPreviewOverlay preview = (MGDockPreviewOverlay)previewPart;
        MGTheme theme = harness.Window.GetTheme();
        Assert.Equal(theme.Docking.PreviewOverlayFillColor, preview.PreviewColor);
        Assert.Equal(theme.Docking.PreviewOverlayBorderColor, preview.PreviewBorderColor);
        Assert.Equal(2, preview.PreviewBorderThickness);

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, fontFamily);
        Assert.Equal(dark.Docking.PreviewOverlayFillColor, dark.Copy().Docking.PreviewOverlayFillColor);
        harness.Window.GetResources().DefaultTheme = dark;

        Assert.Equal(dark.Docking.PreviewOverlayFillColor, preview.PreviewColor);
        Assert.Equal(dark.Docking.PreviewOverlayBorderColor, preview.PreviewBorderColor);
        Assert.True(preview.TryGetTemplatePart(MGDockPreviewOverlay.SurfacePartName, out MGElement surface));
        Assert.Equal(dark.Docking.PreviewOverlayFillColor, ((MGSolidFillBrush)surface.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void The_Preview_Parts_Are_Laid_Out_On_The_Preview_Bounds()
    {
        Harness harness = Harness.Create();
        MGDockPreviewOverlay preview = new(harness.Window);
        Assert.True(preview.TryGetTemplatePart(MGDockPreviewOverlay.SurfacePartName, out MGElement surface));
        Assert.True(preview.TryGetTemplatePart(MGDockPreviewOverlay.BorderPartName, out MGElement borderPart));
        MGBorder border = (MGBorder)borderPart;
        Assert.Equal(new MGElement[] { surface, border }, preview.GetChildren());
        Assert.Same(preview, surface.Parent);

        preview.Show(new Rectangle(30, 40, 150, 70));
        Assert.Equal(new Rectangle(30, 40, 150, 70), surface.LayoutBounds);
        Assert.Equal(new Rectangle(30, 40, 150, 70), border.LayoutBounds);
        Assert.Equal(new Thickness(2), border.BorderThickness);
        Assert.Equal(preview.PreviewColor, ((MGSolidFillBrush)surface.BackgroundBrush.NormalValue).Color);

        // A drag moves the preview every frame without a layout pass of its host.
        preview.Show(new Rectangle(60, 80, 100, 50));
        Assert.Equal(new Rectangle(60, 80, 100, 50), surface.LayoutBounds);

        preview.PreviewBorderThickness = 4;
        preview.PreviewColor = Color.Orange;
        Assert.Equal(new Thickness(4), border.BorderThickness);
        Assert.Equal(Color.Orange, ((MGSolidFillBrush)surface.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void The_Auto_Hide_Strip_Separator_Runs_Along_The_Inner_Edge()
    {
        Harness harness = Harness.Create();
        MGDockAutoHideStrip strip = new(harness.Window, AutoHideSide.Left);
        harness.Show(strip);

        Assert.True(strip.TryGetTemplatePart(MGDockAutoHideStrip.SeparatorPartName, out MGElement separatorPart));
        MGRectangle separator = (MGRectangle)separatorPart;
        Rectangle stripBounds = strip.LayoutBounds;
        Assert.Equal(new Rectangle(stripBounds.Right - 1, stripBounds.Y, 1, stripBounds.Height), separator.LayoutBounds);
        Assert.Equal(strip.SeparatorColor, ((MGSolidFillBrush)separator.Fill).Color);
    }

    /// <summary>The elements the visual tree reaches through <paramref name="owner"/>'s components: <see cref="MGElement.EnumerateVisualTree"/>
    /// only includes components together with the element itself, which is then left out.</summary>
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
            Assert.Same(control, part.ManagedParent);
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

        /// <summary>Makes the element the content of the window, shows the window and runs two frames so layout is settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            if (!Desktop.Windows.Contains(Window))
                Desktop.Windows.Add(Window);
            Frame(0);
            Frame(1);
        }

        public void Frame(int frameIndex)
        {
            MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
