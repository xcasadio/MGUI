using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Responsive;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>Covers Docs/Tasks/layout-tasks.md task 1: container-owned spacing (<see cref="MGStackPanel.Spacing"/>,
/// <see cref="MGGrid.RowSpacing"/>/<see cref="MGGrid.ColumnSpacing"/>/<see cref="MGGrid.GridLineMargin"/>,
/// <see cref="MGUniformGrid.RowSpacing"/>/<see cref="MGUniformGrid.ColumnSpacing"/>/<see cref="MGUniformGrid.GridLineMargin"/>,
/// <see cref="MGWrapPanel.Spacing"/>, <see cref="VirtualizingWrapPanel.Spacing"/>) scales under a responsive subtree the same way
/// <see cref="MGElement.ResolvedMargin"/>/<see cref="MGElement.ResolvedPadding"/> already do. See Docs/layout-architecture.md.</summary>
public class ResponsiveContainerSpacingTests
{
    private const int DesignWidth = 1920;
    private const int DesignHeight = 1080;
    private static readonly UIDesignResolution Design = new(DesignWidth, DesignHeight);

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGResponsiveRoot Root);

    /// <summary>Builds a desktop/window/<see cref="MGResponsiveRoot"/> harness whose <see cref="MGDesktop.ResponsiveMetrics"/>.UIScaleFactor
    /// is EXACTLY <paramref name="uiScaleFactor"/>: the min/max clamps are pinned to that exact value, so the result is deterministic and
    /// doesn't depend on (and can't be silently rescued by) the default 0.5/4.0 clamps.</summary>
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

    /// <summary>Builds a bare (non-responsive) desktop/window harness: no <see cref="MGResponsiveRoot"/>, <see cref="MGElement.UseResponsiveLayout"/>
    /// left unset everywhere, so <see cref="MGElement.IsResponsiveLayoutEnabled"/> is false regardless of <see cref="MGDesktop.ResponsiveSettings"/>.</summary>
    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window) CreateNonResponsiveHarness(int viewportWidth, int viewportHeight)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, viewportWidth, viewportHeight));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, viewportWidth, viewportHeight)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
        };
        desktop.Windows.Add(window);
        return (runtime, desktop, window);
    }

    private static MGBorder FixedSizeChild(MGWindow window, int width, int height) => new(window)
    {
        PreferredWidth = width,
        PreferredHeight = height,
        ScaleDimensionsWithResponsive = false, // dimensions are out of scope for this task; isolate the spacing behavior under test
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
    };

    #region 1. MGStackPanel

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void StackPanel_Vertical_ChildPositions_UseResolvedSpacing(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);
        MGStackPanel panel = new(h.Window, Orientation.Vertical)
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        MGBorder child0 = FixedSizeChild(h.Window, 40, 30);
        MGBorder child1 = FixedSizeChild(h.Window, 40, 30);
        MGBorder child2 = FixedSizeChild(h.Window, 40, 30);
        panel.TryAddChild(child0);
        panel.TryAddChild(child1);
        panel.TryAddChild(child2);
        h.Root.TryAddChild(panel);

        h.Desktop.Update();
        h.Desktop.Update();

        int expectedSpacing = UIResponsiveMath.ScaleSpacing(8, uiScaleFactor);
        Assert.NotEqual(8, expectedSpacing);
        Assert.Equal(expectedSpacing, panel.ResolvedSpacing);
        Assert.Equal(child0.AllocatedBounds.Bottom + expectedSpacing, child1.AllocatedBounds.Top);
        Assert.Equal(child1.AllocatedBounds.Bottom + expectedSpacing, child2.AllocatedBounds.Top);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void StackPanel_Horizontal_ChildPositions_UseResolvedSpacing(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);
        MGStackPanel panel = new(h.Window, Orientation.Horizontal)
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        MGBorder child0 = FixedSizeChild(h.Window, 40, 30);
        MGBorder child1 = FixedSizeChild(h.Window, 40, 30);
        panel.TryAddChild(child0);
        panel.TryAddChild(child1);
        h.Root.TryAddChild(panel);

        h.Desktop.Update();
        h.Desktop.Update();

        int expectedSpacing = UIResponsiveMath.ScaleSpacing(8, uiScaleFactor);
        Assert.Equal(expectedSpacing, panel.ResolvedSpacing);
        Assert.Equal(child0.AllocatedBounds.Right + expectedSpacing, child1.AllocatedBounds.Left);
    }

    #endregion

    #region 2. MGWrapPanel

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void WrapPanel_ChildPositions_UseResolvedSpacing(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);
        MGWrapPanel panel = new(h.Window, Orientation.Horizontal)
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        MGBorder child0 = FixedSizeChild(h.Window, 40, 30);
        MGBorder child1 = FixedSizeChild(h.Window, 40, 30);
        panel.TryAddChild(child0);
        panel.TryAddChild(child1);
        h.Root.TryAddChild(panel);

        h.Desktop.Update();
        h.Desktop.Update();

        int expectedSpacing = UIResponsiveMath.ScaleSpacing(10, uiScaleFactor);
        Assert.Equal(expectedSpacing, panel.ResolvedSpacing);
        Assert.Equal(child0.AllocatedBounds.Right + expectedSpacing, child1.AllocatedBounds.Left);
    }

    #endregion

    #region 3. VirtualizingWrapPanel

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void VirtualizingWrapPanel_RealizedItemPitch_UsesResolvedSpacing(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);

        MGScrollViewer scrollViewer = new(h.Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        VirtualizingWrapPanel panel = new(h.Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ItemWidth = 96,
            ItemHeight = 80,
            Spacing = 8,
            BufferRows = 1,
            TotalItemCount = 200,
            ItemGenerator = _ => new MGBorder(h.Window),
            ItemRecycler = static (_, _) => { },
        };

        scrollViewer.SetContent(panel);
        h.Root.TryAddChild(scrollViewer);

        h.Desktop.Update();
        h.Desktop.Update();

        int expectedSpacing = UIResponsiveMath.ScaleSpacing(8, uiScaleFactor);
        Assert.Equal(expectedSpacing, panel.ResolvedSpacing);

        int columns = panel.CurrentColumnCount;
        Assert.True(columns >= 2, "Test requires at least 2 realized columns to check the horizontal pitch.");

        //  GetColumnCount must have been fed the resolved (not raw) spacing.
        int expectedColumns = VirtualizingWrapPanelLayout.GetColumnCount(panel.LayoutBounds.Width, panel.ItemWidth, expectedSpacing);
        Assert.Equal(expectedColumns, columns);

        Assert.True(panel.TryGetRealizedElement(0, out MGElement item00));
        Assert.True(panel.TryGetRealizedElement(1, out MGElement item01));
        Assert.Equal(item00.AllocatedBounds.Right + expectedSpacing, item01.AllocatedBounds.Left);

        Assert.True(panel.TryGetRealizedElement(columns, out MGElement item10));
        Assert.Equal(item00.AllocatedBounds.Top + panel.ItemHeight + expectedSpacing, item10.AllocatedBounds.Top);

        //  EnsureIndexVisible's row-pitch math must stay consistent with the arrange pass's resolved spacing:
        //  scrolling to a deep index must actually realize it, near the max offset.
        int deepIndex = panel.TotalItemCount - 1;
        panel.EnsureIndexVisible(deepIndex);
        h.Desktop.Update();
        h.Desktop.Update();

        Assert.True(scrollViewer.VerticalOffset > 0f);
        Assert.InRange(scrollViewer.MaxVerticalOffset - scrollViewer.VerticalOffset, 0f, panel.ItemHeight + expectedSpacing);
        Assert.True(panel.TryGetRealizedElement(deepIndex, out MGElement deepItem));
        Assert.NotEqual(Rectangle.Empty, deepItem.AllocatedBounds);
    }

    #endregion

    #region 4. MGGrid / MGUniformGrid - cell pitch and edge offsets

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void Grid_CellPitch_UsesResolvedSpacing(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);

        MGGrid grid = new(h.Window)
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0),
        };
        grid.AddColumns(new[] { GridLength.CreatePixelLength(40), GridLength.CreatePixelLength(40) });
        grid.AddRows(new[] { GridLength.CreatePixelLength(30), GridLength.CreatePixelLength(30) });
        grid.TryAddChild(0, 0, new MGBorder(h.Window)); // MGGrid.HasContent requires at least one cell to hold an element before it measures/arranges rows and columns
        h.Root.TryAddChild(grid);

        h.Desktop.Update();
        h.Desktop.Update();

        int expectedSpacing = UIResponsiveMath.ScaleSpacing(8, uiScaleFactor);
        Assert.Equal(expectedSpacing, grid.ResolvedColumnSpacing);
        Assert.Equal(expectedSpacing, grid.ResolvedRowSpacing);

        Assert.Equal(grid.Columns[0].Width + expectedSpacing, grid.Columns[1].Left - grid.Columns[0].Left);
        Assert.Equal(grid.Rows[0].Height + expectedSpacing, grid.Rows[1].Top - grid.Rows[0].Top);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void Grid_EdgeOffsets_UseResolvedSpacingAndMargin(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);

        MGGrid BuildGrid(GridLinesVisibility visibility) => new(h.Window)
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            GridLineMargin = 3,
            GridLinesVisibility = visibility,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0),
        };

        MGGrid gridNoEdges = BuildGrid(GridLinesVisibility.None);
        gridNoEdges.AddColumns(new[] { GridLength.CreatePixelLength(40), GridLength.CreatePixelLength(40) });
        gridNoEdges.AddRows(new[] { GridLength.CreatePixelLength(30), GridLength.CreatePixelLength(30) });
        gridNoEdges.TryAddChild(0, 0, new MGBorder(h.Window)); // MGGrid.HasContent requires at least one cell to hold an element

        MGGrid gridWithEdges = BuildGrid(GridLinesVisibility.LeftEdge | GridLinesVisibility.TopEdge);
        gridWithEdges.AddColumns(new[] { GridLength.CreatePixelLength(40), GridLength.CreatePixelLength(40) });
        gridWithEdges.AddRows(new[] { GridLength.CreatePixelLength(30), GridLength.CreatePixelLength(30) });
        gridWithEdges.TryAddChild(0, 0, new MGBorder(h.Window));

        //  Both grids are Left/Top-aligned (not stretched) inside the same MGResponsiveRoot, so both anchor to the
        //  same top-left position regardless of their own (possibly different) desired size.
        h.Root.TryAddChild(gridNoEdges);
        h.Root.TryAddChild(gridWithEdges);

        h.Desktop.Update();
        h.Desktop.Update();

        int expectedColumnSpacing = UIResponsiveMath.ScaleSpacing(8, uiScaleFactor);
        int expectedRowSpacing = expectedColumnSpacing;
        int expectedColumnMargin = UIResponsiveMath.ScaleGridLineMargin(3, 8, expectedColumnSpacing, uiScaleFactor);
        int expectedRowMargin = UIResponsiveMath.ScaleGridLineMargin(3, 8, expectedRowSpacing, uiScaleFactor);
        Assert.Equal(expectedColumnMargin, gridWithEdges.ResolvedColumnGridLineMargin);
        Assert.Equal(expectedRowMargin, gridWithEdges.ResolvedRowGridLineMargin);

        int expectedLeftOffset = System.Math.Max(0, expectedColumnSpacing - expectedColumnMargin);
        int expectedTopOffset = System.Math.Max(0, expectedRowSpacing - expectedRowMargin);

        Assert.Equal(gridNoEdges.Columns[0].Left + expectedLeftOffset, gridWithEdges.Columns[0].Left);
        Assert.Equal(gridNoEdges.Rows[0].Top + expectedTopOffset, gridWithEdges.Rows[0].Top);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void UniformGrid_CellPitch_UsesResolvedSpacing(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);

        MGUniformGrid grid = new(h.Window, 2, 2, new Size(40, 30))
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0),
        };
        h.Root.TryAddChild(grid);

        h.Desktop.Update();
        h.Desktop.Update();

        int expectedSpacing = UIResponsiveMath.ScaleSpacing(8, uiScaleFactor);
        Assert.Equal(expectedSpacing, grid.ResolvedColumnSpacing);
        Assert.Equal(expectedSpacing, grid.ResolvedRowSpacing);

        Rectangle cell00 = grid.CellBounds[new GridCellIndex(0, 0)];
        Rectangle cell01 = grid.CellBounds[new GridCellIndex(0, 1)];
        Rectangle cell10 = grid.CellBounds[new GridCellIndex(1, 0)];
        Assert.Equal(cell00.Width + expectedSpacing, cell01.Left - cell00.Left);
        Assert.Equal(cell00.Height + expectedSpacing, cell10.Top - cell00.Top);
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void UniformGrid_EdgeOffsets_UseResolvedSpacingAndMargin(float uiScaleFactor)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);

        MGUniformGrid BuildGrid(GridLinesVisibility visibility) => new(h.Window, 2, 2, new Size(40, 30))
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            GridLineMargin = 3,
            GridLinesVisibility = visibility,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0),
        };

        MGUniformGrid gridNoEdges = BuildGrid(GridLinesVisibility.None);
        MGUniformGrid gridWithEdges = BuildGrid(GridLinesVisibility.LeftEdge | GridLinesVisibility.TopEdge);

        h.Root.TryAddChild(gridNoEdges);
        h.Root.TryAddChild(gridWithEdges);

        h.Desktop.Update();
        h.Desktop.Update();

        int expectedSpacing = UIResponsiveMath.ScaleSpacing(8, uiScaleFactor);
        int expectedMargin = UIResponsiveMath.ScaleGridLineMargin(3, 8, expectedSpacing, uiScaleFactor);
        int expectedOffset = System.Math.Max(0, expectedSpacing - expectedMargin);

        Rectangle cellNoEdges = gridNoEdges.CellBounds[new GridCellIndex(0, 0)];
        Rectangle cellWithEdges = gridWithEdges.CellBounds[new GridCellIndex(0, 0)];
        Assert.Equal(cellNoEdges.Left + expectedOffset, cellWithEdges.Left);
        Assert.Equal(cellNoEdges.Top + expectedOffset, cellWithEdges.Top);
    }

    #endregion

    #region 5. Exact gridline thickness at theme values

    [Theory]
    [InlineData(0.5f, 2f)]
    [InlineData(2.0f, 4f)]
    public void Grid_GridLineThickness_IsExact_AtThemeValues(float uiScaleFactor, float expectedThicknessPx)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);

        Color horizontalColor = new(11, 22, 33, 255);
        Color verticalColor = new(44, 55, 66, 255);

        MGGrid grid = new(h.Window)
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            GridLineMargin = 3,
            GridLinesVisibility = GridLinesVisibility.All,
            HorizontalGridLineBrush = horizontalColor.AsFillBrush(),
            VerticalGridLineBrush = verticalColor.AsFillBrush(),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0),
        };
        grid.AddColumns(new[] { GridLength.CreatePixelLength(40), GridLength.CreatePixelLength(40), GridLength.CreatePixelLength(40) });
        grid.AddRows(new[] { GridLength.CreatePixelLength(30), GridLength.CreatePixelLength(30), GridLength.CreatePixelLength(30) });
        grid.TryAddChild(0, 0, new MGBorder(h.Window)); // MGGrid.HasContent requires at least one cell to hold an element
        h.Root.TryAddChild(grid);

        h.Desktop.Update();
        h.Desktop.Update();

        GraphNoOpDrawTransaction transaction = new(h.Runtime, DrawSettings.Default);
        h.Desktop.Draw(transaction, 1.0f);

        List<GraphFillRectangleCall> horizontalCalls = transaction.FillRectangleCalls.Where(c => c.Color == horizontalColor).ToList();
        List<GraphFillRectangleCall> verticalCalls = transaction.FillRectangleCalls.Where(c => c.Color == verticalColor).ToList();

        Assert.NotEmpty(horizontalCalls);
        Assert.NotEmpty(verticalCalls);
        Assert.All(horizontalCalls, c => Assert.Equal(expectedThicknessPx, c.Destination.Height));
        Assert.All(verticalCalls, c => Assert.Equal(expectedThicknessPx, c.Destination.Width));
    }

    [Theory]
    [InlineData(0.5f, 2f)]
    [InlineData(2.0f, 4f)]
    public void UniformGrid_GridLineThickness_IsExact_AtThemeValues(float uiScaleFactor, float expectedThicknessPx)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor);

        Color horizontalColor = new(77, 11, 22, 255);
        Color verticalColor = new(33, 77, 44, 255);

        MGUniformGrid grid = new(h.Window, 3, 3, new Size(40, 30))
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            GridLineMargin = 3,
            GridLinesVisibility = GridLinesVisibility.All,
            HorizontalGridLineBrush = horizontalColor.AsFillBrush(),
            VerticalGridLineBrush = verticalColor.AsFillBrush(),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0),
        };
        h.Root.TryAddChild(grid);

        h.Desktop.Update();
        h.Desktop.Update();

        GraphNoOpDrawTransaction transaction = new(h.Runtime, DrawSettings.Default);
        h.Desktop.Draw(transaction, 1.0f);

        List<GraphFillRectangleCall> horizontalCalls = transaction.FillRectangleCalls.Where(c => c.Color == horizontalColor).ToList();
        List<GraphFillRectangleCall> verticalCalls = transaction.FillRectangleCalls.Where(c => c.Color == verticalColor).ToList();

        Assert.NotEmpty(horizontalCalls);
        Assert.NotEmpty(verticalCalls);
        Assert.All(horizontalCalls, c => Assert.Equal(expectedThicknessPx, c.Destination.Height));
        Assert.All(verticalCalls, c => Assert.Equal(expectedThicknessPx, c.Destination.Width));
    }

    #endregion

    #region 6. designFill <= 0: fills clamp to >= 0, no negative-size rectangle reaches the brush

    [Fact]
    public void Grid_GridLineFill_IsClampedNonNegative_WhenDesignInvariantIsAlreadyBroken()
    {
        Harness h = CreateHarness(960, 540, 0.5f);

        Color lineColor = new(99, 88, 77, 255);

        MGGrid grid = new(h.Window)
        {
            RowSpacing = 4,
            ColumnSpacing = 4,
            GridLineMargin = 3, // breaks the documented invariant (GridLineMargin should be < half of RowSpacing/ColumnSpacing): designFill = 4 - 2*3 = -2 <= 0
            GridLinesVisibility = GridLinesVisibility.All,
            HorizontalGridLineBrush = lineColor.AsFillBrush(),
            VerticalGridLineBrush = lineColor.AsFillBrush(),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Padding = new Thickness(0),
        };
        grid.AddColumns(new[] { GridLength.CreatePixelLength(40), GridLength.CreatePixelLength(40) });
        grid.AddRows(new[] { GridLength.CreatePixelLength(30), GridLength.CreatePixelLength(30) });
        grid.TryAddChild(0, 0, new MGBorder(h.Window)); // MGGrid.HasContent requires at least one cell to hold an element
        h.Root.TryAddChild(grid);

        h.Desktop.Update();
        h.Desktop.Update();

        //  designFill = 4 - 2*3 = -2 <= 0, so the margin is NOT capped: resolvedMargin = ScaleInt(3, 0.5) = 2,
        //  resolvedSpacing = ScaleSpacing(4, 0.5) = 2, so the fill (2 - 2*2 = -2) must be clamped to 0.
        Assert.Equal(2, grid.ResolvedRowGridLineMargin);
        Assert.Equal(2, grid.ResolvedRowSpacing);

        GraphNoOpDrawTransaction transaction = new(h.Runtime, DrawSettings.Default);
        h.Desktop.Draw(transaction, 1.0f);

        List<GraphFillRectangleCall> lineCalls = transaction.FillRectangleCalls.Where(c => c.Color == lineColor).ToList();
        Assert.NotEmpty(lineCalls);
        Assert.All(lineCalls, c => Assert.True(c.Destination.Width >= 0f && c.Destination.Height >= 0f,
            $"Negative-size gridline rectangle reached the brush: {c.Destination}"));
        //  The invariant is broken on both axes identically in this scenario, so every fill (horizontal band height,
        //  vertical band width) must have collapsed to exactly 0, not merely "happens to be non-negative".
        Assert.All(lineCalls, c => Assert.True(c.Destination.Width == 0f || c.Destination.Height == 0f));
    }

    #endregion

    #region 7. ScaleSpacingWithResponsive = false restores raw pixels; sibling still scales

    [Fact]
    public void StackPanel_ScaleSpacingWithResponsiveFalse_RestoresRawSpacing_WhileSiblingStillScales()
    {
        Harness h = CreateHarness(960, 540, 0.5f);

        MGStackPanel scaledPanel = new(h.Window, Orientation.Vertical) { Spacing = 8 };
        MGStackPanel rawPanel = new(h.Window, Orientation.Vertical) { Spacing = 8, ScaleSpacingWithResponsive = false };
        h.Root.TryAddChild(scaledPanel);
        h.Root.TryAddChild(rawPanel);

        h.Desktop.Update();
        h.Desktop.Update();

        Assert.Equal(UIResponsiveMath.ScaleSpacing(8, 0.5f), scaledPanel.ResolvedSpacing);
        Assert.Equal(8, rawPanel.ResolvedSpacing);
    }

    #endregion

    #region 8. Non-responsive tree: identical bounds regardless of ResponsiveSettings

    [Fact]
    public void NonResponsiveTree_ProducesIdenticalBounds_RegardlessOfResponsiveSettings()
    {
        (GraphTestRuntime _, MGDesktop desktop, MGWindow window) = CreateNonResponsiveHarness(640, 480);

        MGStackPanel panel = new(window, Orientation.Vertical)
        {
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        MGBorder child0 = FixedSizeChild(window, 40, 30);
        MGBorder child1 = FixedSizeChild(window, 40, 30);
        panel.TryAddChild(child0);
        panel.TryAddChild(child1);
        window.SetContent(panel);

        desktop.Update();
        desktop.Update();

        Rectangle beforeChild0 = child0.AllocatedBounds;
        Rectangle beforeChild1 = child1.AllocatedBounds;
        Assert.Equal(8, panel.ResolvedSpacing);

        desktop.ResponsiveSettings = new UIResponsiveSettings(new UIDesignResolution(200, 100), minUIScaleFactor: 3f, maxUIScaleFactor: 3f);
        desktop.Update();
        desktop.Update();

        Assert.Equal(beforeChild0, child0.AllocatedBounds);
        Assert.Equal(beforeChild1, child1.AllocatedBounds);
        Assert.Equal(8, panel.ResolvedSpacing);
    }

    #endregion

    #region 9. Floor: non-zero spacing keeps >= 1px, zero spacing stays zero

    [Fact]
    public void ScaleSpacing_FloorsNonZeroSpacingToAtLeastOnePixel_ButZeroStaysZero()
    {
        Assert.Equal(1, UIResponsiveMath.ScaleSpacing(1, 0.5f));
        Assert.Equal(0, UIResponsiveMath.ScaleSpacing(0, 0.5f));
    }

    [Fact]
    public void StackPanel_ResolvedSpacing_Floors_NonZeroSpacing_UnderExtremeDownscale()
    {
        Harness h = CreateHarness(960, 540, 0.5f);
        MGStackPanel panelWithSpacing = new(h.Window, Orientation.Vertical) { Spacing = 1 };
        MGStackPanel panelWithZeroSpacing = new(h.Window, Orientation.Vertical) { Spacing = 0 };
        h.Root.TryAddChild(panelWithSpacing);
        h.Root.TryAddChild(panelWithZeroSpacing);

        h.Desktop.Update();
        h.Desktop.Update();

        Assert.Equal(1, panelWithSpacing.ResolvedSpacing);
        Assert.Equal(0, panelWithZeroSpacing.ResolvedSpacing);
    }

    #endregion

    #region 10. Identity at factor 1.0

    [Fact]
    public void AllContainers_ResolvedMembers_EqualRawValues_AtIdentityScale()
    {
        Harness h = CreateHarness(DesignWidth, DesignHeight, 1.0f);
        Assert.Equal(1.0f, h.Desktop.ResponsiveMetrics.UIScaleFactor);

        MGStackPanel stack = new(h.Window, Orientation.Vertical) { Spacing = 8 };
        MGWrapPanel wrap = new(h.Window, Orientation.Horizontal) { Spacing = 10 };
        VirtualizingWrapPanel vwrap = new(h.Window)
        {
            Spacing = 6,
            ItemWidth = 40,
            ItemHeight = 30,
            TotalItemCount = 4,
            ItemGenerator = _ => new MGBorder(h.Window),
            ItemRecycler = static (_, _) => { },
        };
        MGGrid grid = new(h.Window) { RowSpacing = 8, ColumnSpacing = 8, GridLineMargin = 3 };
        grid.AddColumns(new[] { GridLength.CreatePixelLength(40) });
        grid.AddRows(new[] { GridLength.CreatePixelLength(30) });
        MGUniformGrid uniform = new(h.Window, 1, 1, new Size(40, 30))
        {
            RowSpacing = 8,
            ColumnSpacing = 8,
            GridLineMargin = 3,
        };

        h.Root.TryAddChild(stack);
        h.Root.TryAddChild(wrap);
        h.Root.TryAddChild(vwrap);
        h.Root.TryAddChild(grid);
        h.Root.TryAddChild(uniform);

        h.Desktop.Update();
        h.Desktop.Update();

        Assert.Equal(8, stack.ResolvedSpacing);
        Assert.Equal(10, wrap.ResolvedSpacing);
        Assert.Equal(6, vwrap.ResolvedSpacing);
        Assert.Equal(8, grid.ResolvedRowSpacing);
        Assert.Equal(8, grid.ResolvedColumnSpacing);
        Assert.Equal(3, grid.ResolvedRowGridLineMargin);
        Assert.Equal(3, grid.ResolvedColumnGridLineMargin);
        Assert.Equal(8, uniform.ResolvedRowSpacing);
        Assert.Equal(8, uniform.ResolvedColumnSpacing);
        Assert.Equal(3, uniform.ResolvedRowGridLineMargin);
        Assert.Equal(3, uniform.ResolvedColumnGridLineMargin);
    }

    #endregion
}
