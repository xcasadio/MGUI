using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Responsive;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>Covers Docs/Tasks/layout-tasks.md task 2 ("Completer la couverture de tests de regression layout sous scale responsive"),
/// alongside <see cref="ResponsiveTextAndViewportRegressionTests"/>. This file covers:<list type="bullet">
/// <item>a container matrix: <see cref="MGElement.ResolvedMargin"/>/<see cref="MGElement.ResolvedPadding"/>/<see cref="MGElement.ResolvedPreferredWidth"/>/
/// <see cref="MGElement.ResolvedPreferredHeight"/> applied to live <see cref="MGStackPanel"/> and <see cref="MGGrid"/> instances, at
/// <c>UIScaleFactor</c> &lt; 1 and &gt; 1, plus the resulting child positions;</item>
/// <item>non-regression for trees that never opt into responsive layout, with a positive control proving the assertion isn't vacuous.</item>
/// </list>
/// This intentionally does NOT re-cover <see cref="MGStackPanel.Spacing"/> / <see cref="MGGrid.RowSpacing"/> / <see cref="MGGrid.ColumnSpacing"/> /
/// <see cref="MGGrid.GridLineMargin"/> under scale: that container-owned-spacing matrix already exists in
/// <see cref="ResponsiveContainerSpacingTests"/> (Docs/Tasks/layout-tasks.md task 1). See Docs/layout-architecture.md.</summary>
public class ResponsiveLayoutRegressionTests
{
    private const int DesignWidth = 1920;
    private const int DesignHeight = 1080;
    private static readonly UIDesignResolution Design = new(DesignWidth, DesignHeight);

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGResponsiveRoot Root);

    /// <summary>Builds a desktop/window/<see cref="MGResponsiveRoot"/> harness whose <see cref="MGDesktop.ResponsiveMetrics"/>.UIScaleFactor
    /// is EXACTLY <paramref name="uiScaleFactor"/>: the min/max clamps are pinned to that exact value, so the result is deterministic and
    /// doesn't depend on (and can't be silently rescued by) the default 0.5/4.0 clamps. Matches the pattern of
    /// <see cref="ResponsiveContainerSpacingTests"/>'s own harness.</summary>
    private static Harness CreateHarness(int viewportWidth, int viewportHeight, float uiScaleFactor)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, viewportWidth, viewportHeight));
        MGDesktop desktop = new(runtime);
        desktop.ResponsiveSettings = new UIResponsiveSettings(Design, minUIScaleFactor: uiScaleFactor, maxUIScaleFactor: uiScaleFactor);

        MGWindow window = new(desktop, 0, 0, viewportWidth, viewportHeight)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
        };
        desktop.Windows.Add(window);

        MGResponsiveRoot root = new(window);
        window.SetContent(root);

        return new Harness(runtime, desktop, window, root);
    }

    private static MGBorder FixedSizeChild(MGWindow window, int width, int height) => new(window)
    {
        PreferredWidth = width,
        PreferredHeight = height,
        ScaleDimensionsWithResponsive = false, // container-owned/child dimensions are out of scope (task 3); isolate margin/padding behavior
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    #region 1. Containers matrix: MGStackPanel and MGGrid resolve Margin/Padding/PreferredSize like MGElement's formulas

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void StackPanel_ResolvedMarginPaddingPreferredSize_MatchElementFormulas(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);

        Thickness designMargin = new(12, 6, 18, 9);
        Thickness designPadding = new(5, 3, 7, 4);

        MGStackPanel panel = new(h.Window, Orientation.Vertical)
        {
            Spacing = 0,
            Margin = designMargin,
            Padding = designPadding,
            PreferredWidth = 200,
            PreferredHeight = 100,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        MGBorder child = FixedSizeChild(h.Window, 20, 10);
        panel.TryAddChild(child);
        h.Root.TryAddChild(panel);

        h.Desktop.Update();
        h.Desktop.Update();

        Thickness expectedMargin = UIResponsiveMath.ScaleThickness(designMargin, uiScaleFactor);
        Thickness expectedPadding = UIResponsiveMath.ScaleThickness(designPadding, uiScaleFactor);
        int expectedWidth = UIResponsiveMath.ScaleInt(200, uiScaleFactor);
        int expectedHeight = UIResponsiveMath.ScaleInt(100, uiScaleFactor);

        Assert.NotEqual(designMargin, expectedMargin);
        Assert.NotEqual(designPadding, expectedPadding);
        Assert.Equal(expectedMargin, panel.ResolvedMargin);
        Assert.Equal(expectedPadding, panel.ResolvedPadding);
        Assert.Equal(expectedWidth, panel.ResolvedPreferredWidth);
        Assert.Equal(expectedHeight, panel.ResolvedPreferredHeight);

        //  Relationships, not hard-coded rectangles: the panel is Left/Top-aligned, so its own RenderBounds.Left/.Top coincide with the
        //  raw AllocatedBounds it receives from the root (MGElement.UpdateLayout); LayoutBounds is that RenderBounds inset by
        //  ResolvedMargin. The single child then sits at the panel's AlignedContentBounds, which (MGElement.MeasureSelf) is RenderBounds
        //  further inset by ResolvedMargin + ResolvedPadding combined - i.e. exactly `expectedMargin` then `expectedPadding` away from
        //  the root's own content bounds.
        Assert.Equal(h.Root.AlignedContentBounds.Left + expectedMargin.Left, panel.LayoutBounds.Left);
        Assert.Equal(h.Root.AlignedContentBounds.Top + expectedMargin.Top, panel.LayoutBounds.Top);
        Assert.Equal(h.Root.AlignedContentBounds.Left + expectedMargin.Left + expectedPadding.Left, child.AllocatedBounds.Left);
        Assert.Equal(h.Root.AlignedContentBounds.Top + expectedMargin.Top + expectedPadding.Top, child.AllocatedBounds.Top);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void Grid_ResolvedMarginPaddingPreferredSize_MatchElementFormulas(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);

        Thickness designMargin = new(10, 5, 15, 8);
        Thickness designPadding = new(4, 2, 6, 3);

        MGGrid grid = new(h.Window)
        {
            RowSpacing = 0,
            ColumnSpacing = 0,
            Margin = designMargin,
            Padding = designPadding,
            PreferredWidth = 220,
            PreferredHeight = 120,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        grid.AddColumns(new[] { GridLength.CreatePixelLength(40) });
        grid.AddRows(new[] { GridLength.CreatePixelLength(30) });
        MGBorder cell = FixedSizeChild(h.Window, 20, 10);
        grid.TryAddChild(0, 0, cell); // MGGrid.HasContent requires at least one cell to hold an element before it measures/arranges

        h.Root.TryAddChild(grid);

        h.Desktop.Update();
        h.Desktop.Update();

        Thickness expectedMargin = UIResponsiveMath.ScaleThickness(designMargin, uiScaleFactor);
        Thickness expectedPadding = UIResponsiveMath.ScaleThickness(designPadding, uiScaleFactor);
        int expectedWidth = UIResponsiveMath.ScaleInt(220, uiScaleFactor);
        int expectedHeight = UIResponsiveMath.ScaleInt(120, uiScaleFactor);

        Assert.NotEqual(designMargin, expectedMargin);
        Assert.NotEqual(designPadding, expectedPadding);
        Assert.Equal(expectedMargin, grid.ResolvedMargin);
        Assert.Equal(expectedPadding, grid.ResolvedPadding);
        Assert.Equal(expectedWidth, grid.ResolvedPreferredWidth);
        Assert.Equal(expectedHeight, grid.ResolvedPreferredHeight);

        //  See the analogous comment in StackPanel_ResolvedMarginPaddingPreferredSize_MatchElementFormulas: LayoutBounds reflects the
        //  grid's own ResolvedMargin, and the cell sits ResolvedMargin + ResolvedPadding away from the root's own content bounds.
        Assert.Equal(h.Root.AlignedContentBounds.Left + expectedMargin.Left, grid.LayoutBounds.Left);
        Assert.Equal(h.Root.AlignedContentBounds.Top + expectedMargin.Top, grid.LayoutBounds.Top);
        Assert.Equal(h.Root.AlignedContentBounds.Left + expectedMargin.Left + expectedPadding.Left, cell.AllocatedBounds.Left);
        Assert.Equal(h.Root.AlignedContentBounds.Top + expectedMargin.Top + expectedPadding.Top, cell.AllocatedBounds.Top);
    }

    #endregion

    #region 2. Non-opt-in regression: bounds are unaffected by ResponsiveSettings without opt-in, with a positive control

    /// <summary>Builds a tree without any responsive opt-in (no <see cref="MGResponsiveRoot"/>, <see cref="MGElement.UseResponsiveLayout"/> left
    /// unset everywhere) and one wrapped in an <see cref="MGResponsiveRoot"/>, sharing the same <see cref="MGDesktop"/> (and therefore the same
    /// <see cref="MGDesktop.ResponsiveSettings"/>/<see cref="MGDesktop.ResponsiveMetrics"/>). Toggling the desktop's settings must move only the
    /// second tree.</summary>
    [Fact]
    public void NonOptInTree_BoundsAreUnaffectedByResponsiveSettings_WhilePositiveControlChanges()
    {
        const int viewportWidth = 960;
        const int viewportHeight = 540;

        GraphTestRuntime runtime = new(new Rectangle(0, 0, viewportWidth, viewportHeight));
        MGDesktop desktop = new(runtime);
        //  Identity baseline: design resolution equals the viewport, so UIScaleFactor starts at 1.0.
        desktop.ResponsiveSettings = new UIResponsiveSettings(new UIDesignResolution(viewportWidth, viewportHeight));

        MGWindow plainWindow = new(desktop, 0, 0, viewportWidth, viewportHeight)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
        };
        Thickness designMargin = new(12);
        MGStackPanel plainPanel = new(plainWindow, Orientation.Vertical)
        {
            Spacing = 8,
            Margin = designMargin,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            // UseResponsiveLayout is intentionally left unset (null): with no MGResponsiveRoot ancestor, IsResponsiveLayoutEnabled stays false.
        };
        MGBorder plainChild0 = FixedSizeChild(plainWindow, 30, 20);
        MGBorder plainChild1 = FixedSizeChild(plainWindow, 30, 20);
        plainPanel.TryAddChild(plainChild0);
        plainPanel.TryAddChild(plainChild1);
        plainWindow.SetContent(plainPanel);
        desktop.Windows.Add(plainWindow);

        //  Positive control: the same tree shape, but wrapped in an MGResponsiveRoot so it DOES opt in.
        MGWindow controlWindow = new(desktop, 0, 0, viewportWidth, viewportHeight)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
        };
        MGResponsiveRoot controlRoot = new(controlWindow);
        MGStackPanel controlPanel = new(controlWindow, Orientation.Vertical)
        {
            Spacing = 8,
            Margin = designMargin,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        MGBorder controlChild0 = FixedSizeChild(controlWindow, 30, 20);
        MGBorder controlChild1 = FixedSizeChild(controlWindow, 30, 20);
        controlPanel.TryAddChild(controlChild0);
        controlPanel.TryAddChild(controlChild1);
        controlRoot.TryAddChild(controlPanel);
        controlWindow.SetContent(controlRoot);
        desktop.Windows.Add(controlWindow);

        desktop.Update();
        desktop.Update();

        //  Note: AllocatedBounds is the raw Bounds an element receives from its parent's arrange pass (MGElement.UpdateLayout) - for the
        //  panels themselves that's unaffected by their own Margin (only LayoutBounds is). The children, however, are positioned by
        //  MGStackPanel.UpdateContentLayout relative to the panel's AlignedContentBounds, which DOES fold in ResolvedMargin (and
        //  ResolvedPadding) via MGElement.MeasureSelf - so the children's own AllocatedBounds is the sensitive, meaningful signal here.
        Rectangle plainPanelLayoutBefore = plainPanel.LayoutBounds;
        Rectangle plainChild0Before = plainChild0.AllocatedBounds;
        Rectangle plainChild1Before = plainChild1.AllocatedBounds;
        Rectangle controlChild0Before = controlChild0.AllocatedBounds;

        //  Assign non-identity ResponsiveSettings: the design resolution now differs sharply from the (unchanged) viewport.
        desktop.ResponsiveSettings = new UIResponsiveSettings(Design);
        Assert.NotEqual(1.0f, desktop.ResponsiveMetrics.UIScaleFactor);

        desktop.Update();
        desktop.Update();

        //  Non-opt-in tree: strictly unchanged, regardless of the new (non-identity) ResponsiveSettings.
        Assert.Equal(designMargin, plainPanel.ResolvedMargin);
        Assert.Equal(plainPanelLayoutBefore, plainPanel.LayoutBounds);
        Assert.Equal(plainChild0Before, plainChild0.AllocatedBounds);
        Assert.Equal(plainChild1Before, plainChild1.AllocatedBounds);

        //  Positive control: the equivalent responsive-opted-in tree DID move, so the assertions above are not vacuously true
        //  (e.g. from a harness bug that never actually applies the new settings).
        Assert.NotEqual(controlChild0Before, controlChild0.AllocatedBounds);
    }

    #endregion
}
