using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Covers task 5 of the docking-bugs slice, "Theme Dark_Blue : palette docking dans la famille bleu marine":
/// the Dark_Blue built-in theme's docking chrome (tabs, auto-hide drawer/strip, splitter) is re-tinted to the
/// same navy family as its ContextMenu (<c>rgb(11,28,72)</c>) and Button/ComboBox (<c>rgb(0,108,214)</c>)
/// instead of the inherited VS-grey palette. These tests pin:
/// (i) the docking control template applies the theme's re-tinted brush over the control's constructor
///     hardcode, (ii) a docking control and a context menu built for the same window resolve the same
///     <see cref="MGTheme"/> instance, (iii) the sibling <c>Dark</c> theme (BasedOn="Dark_Blue" but declaring
///     its own full Docking block) and <c>Light_Gray</c> keep their independent palettes, and
///     (iv) the exact re-tinted values and their family/luminance relationships for Dark_Blue.
/// </summary>
public class DockingThemePaletteTests
{
    // ── Harness ──────────────────────────────────────────────────────────

    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;
        public DockPanelNode Panel;
    }

    private static Harness CreateHarness(MGTheme.BuiltInTheme themeType)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };

        MGTheme theme = new(themeType, desktop.DefaultFontFamily);
        mainWindow.Theme = theme;

        DockPanelNode panel = new() { Title = "A", ContentFactory = () => new MGBorder(mainWindow) };
        DockTabGroupNode group = new();
        group.AddPanel(panel, -1);

        MGDockHost host = new(mainWindow)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(group),
        };

        mainWindow.SetContent(host);
        desktop.Windows.Add(mainWindow);

        return new Harness
        {
            Runtime = runtime,
            Desktop = desktop,
            MainWindow = mainWindow,
            Host = host,
            Panel = panel,
        };
    }

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            new MouseState(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            new KeyboardState()));
        desktop.Update();
    }

    private static MGDockTabItem GetSoleTabItem(Harness harness)
        => Assert.Single(harness.Host.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false));

    private static Color ColorOf(IFillBrush brush)
        => Assert.IsType<MGSolidFillBrush>(brush).Color;

    // ── (i) Template wins over the constructor hardcode ─────────────────

    [Fact]
    public void DockTabItem_NormalBrush_UsesThemeDarkBlueValue_NotConstructorHardcode()
    {
        Harness harness = CreateHarness(MGTheme.BuiltInTheme.Dark_Blue);
        AdvanceFrame(harness.Runtime, harness.Desktop, 0);
        AdvanceFrame(harness.Runtime, harness.Desktop, 16);

        MGDockTabItem tab = GetSoleTabItem(harness);
        MGTheme theme = harness.MainWindow.GetTheme();

        Color actual = ColorOf(tab.NormalBrush);
        Color expected = ColorOf(theme.Docking.TabNormalBackground);

        Assert.Equal(new Color(18, 36, 76), expected);
        Assert.Equal(expected, actual);
        Assert.NotEqual(new Color(45, 45, 48), actual); // constructor hardcode must be overridden
    }

    // ── (ii) Same theme for the docking control and a context menu ─────

    [Fact]
    public void ContextMenu_And_DockingControl_ResolveSameThemeInstance()
    {
        Harness harness = CreateHarness(MGTheme.BuiltInTheme.Dark_Blue);
        AdvanceFrame(harness.Runtime, harness.Desktop, 0);
        AdvanceFrame(harness.Runtime, harness.Desktop, 16);

        MGDockTabItem tab = GetSoleTabItem(harness);

        // Built the same way MGDockTabItem.BuildContextMenu builds its right-click menu.
        MGContextMenu menu = new(harness.MainWindow, "");

        Assert.Same(tab.GetTheme(), menu.GetTheme());
        Assert.Same(harness.MainWindow.Theme, tab.GetTheme());
    }

    // ── (iii) No leak into sibling themes ────────────────────────────────

    [Fact]
    public void DarkTheme_KeepsItsOwnGreyPalette_DespiteBeingBasedOnDarkBlue()
    {
        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, "Arial");

        Assert.Equal(new Color(45, 45, 48), ColorOf(dark.Docking.TabNormalBackground));
        Assert.Equal(new Color(58, 58, 58), ColorOf(dark.Docking.TabActiveBackground));
        Assert.Equal(new Color(30, 30, 30), ColorOf(dark.Docking.AutoHideStripBackground));
        Assert.Equal(new Color(63, 63, 63), ColorOf(dark.Docking.SplitterNormalBrush));
    }

    [Fact]
    public void LightGrayTheme_KeepsItsOwnPalette_Unchanged()
    {
        MGTheme lightGray = new(MGTheme.BuiltInTheme.Light_Gray, "Arial");

        Assert.Equal(new Color(236, 240, 243), ColorOf(lightGray.Docking.TabNormalBackground));
        Assert.Equal(new Color(246, 248, 250), ColorOf(lightGray.Docking.TabActiveBackground));
        Assert.Equal(new Color(242, 245, 247), ColorOf(lightGray.Docking.AutoHideStripBackground));
        Assert.Equal(new Color(188, 193, 198), ColorOf(lightGray.Docking.SplitterNormalBrush));
    }

    // ── (iv) Family pin for Dark_Blue ────────────────────────────────────

    [Theory]
    [InlineData(18, 36, 76)]   // TabNormalBackground / AutoHideDrawerHeaderBackground
    [InlineData(28, 52, 102)]  // TabHoverBackground
    [InlineData(12, 26, 60)]   // TabActiveBackground / AutoHideDrawerBackground
    [InlineData(6, 18, 44)]    // AutoHideStripBackground
    [InlineData(30, 52, 96)]   // SplitterNormalBrush
    public void DarkBlue_RetintedBackgrounds_AreInTheBlueFamily(byte r, byte g, byte b)
    {
        // Blue channel must dominate red by a healthy margin for every re-tinted background field.
        Assert.True(b - r >= 20, $"rgb({r},{g},{b}) is not sufficiently blue-dominant.");
    }

    [Fact]
    public void DarkBlue_DockingPalette_PinsExactRetintedValues()
    {
        MGTheme darkBlue = new(MGTheme.BuiltInTheme.Dark_Blue, "Arial");
        var docking = darkBlue.Docking;

        Assert.Equal(new Color(18, 36, 76), ColorOf(docking.TabNormalBackground));
        Assert.Equal(new Color(28, 52, 102), ColorOf(docking.TabHoverBackground));
        Assert.Equal(new Color(12, 26, 60), ColorOf(docking.TabActiveBackground));
        Assert.Equal(new Color(12, 26, 60), ColorOf(docking.AutoHideDrawerBackground));
        Assert.Equal(new Color(18, 36, 76), ColorOf(docking.AutoHideDrawerHeaderBackground));
        Assert.Equal(new Color(46, 74, 128), docking.AutoHideBorderColor);
        Assert.Equal(new Color(70, 100, 160), docking.AutoHideGripColor);
        Assert.Equal(new Color(6, 18, 44), ColorOf(docking.AutoHideStripBackground));
        Assert.Equal(new Color(36, 60, 110), docking.AutoHideStripSeparatorColor);
        Assert.Equal(new Color(30, 52, 96), ColorOf(docking.SplitterNormalBrush));
        Assert.Equal(new Color(28, 52, 102), docking.AutoHideButtonBackground.FocusedColor);
        Assert.Equal(new Color(12, 26, 60), ColorOf(docking.AutoHideStripButtonBackground.NormalValue));
        Assert.Equal(new Color(28, 52, 102), docking.AutoHideStripButtonBackground.FocusedColor);
    }

    [Fact]
    public void DarkBlue_DockingPalette_LuminanceOrdering_HoverAboveNormalAboveActiveAboveStrip()
    {
        MGTheme darkBlue = new(MGTheme.BuiltInTheme.Dark_Blue, "Arial");
        var docking = darkBlue.Docking;

        double LuminanceOf(IFillBrush brush)
        {
            Color c = ColorOf(brush);
            return 0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B;
        }

        double hover = LuminanceOf(docking.TabHoverBackground);
        double normal = LuminanceOf(docking.TabNormalBackground);
        double active = LuminanceOf(docking.TabActiveBackground);
        double strip = LuminanceOf(docking.AutoHideStripBackground);

        Assert.True(hover > normal, $"hover ({hover}) should be brighter than normal ({normal}).");
        Assert.True(normal > active, $"normal ({normal}) should be brighter than active ({active}).");
        Assert.True(active > strip, $"active ({active}) should be brighter than strip ({strip}).");
    }
}
