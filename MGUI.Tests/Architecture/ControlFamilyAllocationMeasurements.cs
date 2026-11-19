using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit.Abstractions;

namespace MGUI.Tests.Architecture;

/// <summary>The bytes allocated by the three steps of a frame, summed over <see cref="Iterations"/> frames.</summary>
/// <param name="PrepareTotal">The step that comes before the desktop update (a text change, a mouse move): measured on its own.</param>
internal readonly record struct FrameBytes(long PrepareTotal, long UpdateTotal, long DrawTotal, int Iterations)
{
    public long Prepare => PerFrame(PrepareTotal);
    public long Update => PerFrame(UpdateTotal);
    public long Draw => PerFrame(DrawTotal);

    //  Rounded up: a total under one byte per frame must not read as zero.
    private long PerFrame(long total) => (total + Iterations - 1) / Iterations;
}

/// <summary>Measures a frame as three separate steps, prepare, <see cref="MGDesktop.Update"/> and <see cref="MGDesktop.Draw(IUIDrawTransaction, float)"/>,
/// each one with the allocation counter read around it, so that the bytes of the three steps are told apart in one run.</summary>
internal static class FrameAllocationMeter
{
    public const int WarmupFrames = 500;
    public const int MeasuredFrames = 500;

    private static readonly Action<int> NoPrepare = static _ => { };

    /// <param name="prepare">Called with the frame index before each update, or null.</param>
    public static FrameBytes Measure(Action<int> prepare, Action update, Action draw)
    {
        prepare ??= NoPrepare;
        int frame = 0;
        for (int i = 0; i < WarmupFrames; i++)
        {
            prepare(frame++);
            update();
            draw();
        }

        AllocationWindow.Start();
        long prepareTotal = 0;
        long updateTotal = 0;
        long drawTotal = 0;
        for (int i = 0; i < MeasuredFrames; i++)
        {
            long start = GC.GetAllocatedBytesForCurrentThread();
            prepare(frame++);
            long afterPrepare = GC.GetAllocatedBytesForCurrentThread();
            update();
            long afterUpdate = GC.GetAllocatedBytesForCurrentThread();
            draw();
            long afterDraw = GC.GetAllocatedBytesForCurrentThread();
            prepareTotal += afterPrepare - start;
            updateTotal += afterUpdate - afterPrepare;
            drawTotal += afterDraw - afterUpdate;
        }

        return new FrameBytes(prepareTotal, updateTotal, drawTotal, MeasuredFrames);
    }
}

/// <summary>A desktop of 640x360 on a <see cref="GraphTestRuntime"/> with one default window, driven frame by frame with the mouse, the wheel and the keyboard
/// the test sets. Time advances 16 ms per update. The draw goes through a <see cref="NonRecordingDrawTransaction"/>.</summary>
internal sealed class ControlBench
{
    public static readonly Point OutsideTheWindow = new(630, 350);

    private int ElapsedMilliseconds;

    public GraphTestRuntime Runtime { get; }
    public MGDesktop Desktop { get; }
    public MGWindow Window { get; }
    public NonRecordingDrawTransaction Transaction { get; }

    public Point Mouse { get; set; } = OutsideTheWindow;
    public ButtonState Left { get; set; } = ButtonState.Released;
    public int Wheel { get; set; }
    public KeyboardState Keyboard { get; set; }

    public ControlBench(int width = 400, int height = 300)
    {
        Runtime = new GraphTestRuntime(new Rectangle(0, 0, 640, 360));
        Desktop = new MGDesktop(Runtime);
        Transaction = new NonRecordingDrawTransaction(Runtime, DrawSettings.Default);
        Window = new MGWindow(Desktop, 0, 0, width, height);
    }

    /// <summary>Puts <paramref name="content"/> in the window, shows it and runs enough frames for the layout to settle.</summary>
    public void Show(MGElement content)
    {
        Window.SetContent(content);
        Desktop.Windows.Add(Window);
        Frames(3);
    }

    public void Update()
    {
        MouseState mouse = new(Mouse.X, Mouse.Y, Wheel, Left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16), mouse, Keyboard));
        Desktop.Update();
        ElapsedMilliseconds += 16;
    }

    public void Draw() => Desktop.Draw(Transaction);

    public void Frame()
    {
        Update();
        Draw();
    }

    public void Frames(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Frame();
        }
    }

    /// <summary>Moves the mouse to <paramref name="position"/>, runs two frames, and checks that the hovered element of <paramref name="owner"/> is inside <paramref name="target"/>.</summary>
    public void AssertHovers(MGWindow owner, MGElement target, Point position)
    {
        Mouse = position;
        Frames(2);
        Assert.NotNull(owner.HoveredElement);
        Assert.True(target.IsSelfOrAncestorOf(owner.HoveredElement), $"the position {position} is over {owner.HoveredElement.GetType().Name}, not over {target.GetType().Name}");
    }
}

/// <summary>The measurement classes of <see cref="ControlFamilyAllocationMeasurements"/> and <c>TextCaseAllocationMeasurements</c> join this collection, whose tests
/// xunit never runs in parallel with any other test: a measurement counts the bytes of its own thread, and a collection of another thread that allocates at the same
/// time (and so triggers background collections) would add bytes that were never allocated by the code under test (see <see cref="AllocationWindow"/>).</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AllocationMeasurementsCollection
{
    public const string Name = "AllocationMeasurements";
}

/// <summary>
/// Measurement bench of the control families that the allocation-free chantier brings to zero, one family per test: how many bytes <see cref="MGDesktop.Update"/>
/// and <see cref="MGDesktop.Draw(IUIDrawTransaction, float)"/> allocate per frame, at rest and under the input the control reacts to (hover, wheel, drag, typing).<para/>
/// These tests assert no byte count: they write one line per case to the test output. They assert preconditions only (the element is hovered, the drag is active,
/// the draw calls something), so that a measurement cannot silently measure the wrong state. The idle theories of <see cref="DesktopFrameAllocationMeasurements"/>
/// assert zero; the cases here do not join them.
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class ControlFamilyAllocationMeasurements
{
    private const int FrameMilliseconds = 16;

    private readonly ITestOutputHelper Output;

    public ControlFamilyAllocationMeasurements(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>Measures the frames of <paramref name="bench"/> under the input that <paramref name="prepare"/> sets, and writes the line of the case.</summary>
    private FrameBytes Run(string label, ControlBench bench, Action<int> prepare = null)
    {
        long callsBefore = bench.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(prepare, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, $"{label}: the measured frames drew nothing");
        Output.WriteLine($"{label}: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame");
        return bytes;
    }

    private static MGTextBlock Text(MGWindow window, string text) => new(window, text);

    // ---- MGGrid ----

    [Fact]
    public void GridWithASelection()
    {
        ControlBench bench = new();
        MGGrid grid = new(bench.Window) { SelectionMode = GridSelectionMode.Cell };
        RowDefinition[] rows = { grid.AddRow(GridLength.CreateWeightedLength(1)), grid.AddRow(GridLength.CreateWeightedLength(1)), grid.AddRow(GridLength.CreateWeightedLength(1)) };
        ColumnDefinition[] columns = { grid.AddColumn(GridLength.CreateWeightedLength(1)), grid.AddColumn(GridLength.CreateWeightedLength(1)), grid.AddColumn(GridLength.CreateWeightedLength(1)) };
        MGTextBlock[,] cells = new MGTextBlock[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                cells[row, column] = Text(bench.Window, $"R{row}C{column}");
                Assert.True(grid.TryAddChild(row, column, cells[row, column]));
            }
        }

        bench.Show(grid);

        //  Select the cell of the second row and second column with a real click, then move the mouse away.
        Point first = cells[1, 1].ActualLayoutBounds.Center;
        Point second = cells[0, 0].ActualLayoutBounds.Center;
        bench.Mouse = first;
        bench.Left = ButtonState.Pressed;
        bench.Frame();
        bench.Left = ButtonState.Released;
        bench.Frames(2);
        Assert.True(grid.HasSelection);
        Assert.Equal(GridSelectionMode.Cell, grid.CurrentSelection.Value.SelectionMode);
        Assert.Same(rows[1], grid.CurrentSelection.Value.Cell.Row);
        Assert.Same(columns[1], grid.CurrentSelection.Value.Cell.Column);

        bench.Mouse = ControlBench.OutsideTheWindow;
        bench.Frames(2);
        Assert.True(grid.HasSelection);
        Run("MGGrid 3x3, a cell selected, at rest", bench);

        //  The selection only moves on a press or a release (SelectionMouseHandler), so it is moved by clicks: press and release on one cell, then on the other,
        //  and so on, a click every two frames. Two cells that differ, so that no click lands on the cell that is already selected (that would deselect it).
        Action<int> click = frame =>
        {
            int step = frame & 3;
            bench.Mouse = step < 2 ? second : first;
            bench.Left = (step & 1) == 0 ? ButtonState.Pressed : ButtonState.Released;
        };
        for (int frame = 0; frame < 8; frame++)
        {
            click(frame);
            bench.Frame();
            if (frame == 5)
            {
                Assert.Same(rows[0], grid.CurrentSelection.Value.Cell.Row);
            }
            else if (frame == 7)
            {
                Assert.Same(rows[1], grid.CurrentSelection.Value.Cell.Row);
            }
        }

        Run("MGGrid 3x3, selection moved between two cells by a click every 2 frames", bench, click);
        Assert.True(grid.HasSelection);
        bench.Left = ButtonState.Released;
    }

    // ---- MGDockHost ----

    private sealed class DockScene
    {
        public ControlBench Bench { get; } = new();
        public MGDockHost Host { get; private set; }
        public DockPanelNode PanelA { get; private set; }
        public DockPanelNode PanelB { get; private set; }
        public DockPanelNode PanelC { get; private set; }
        public DockTabGroupNode GroupA { get; private set; }
        public DockTabGroupNode GroupB { get; private set; }

        /// <summary>Two tab groups separated by a splitter: A in the first, B in the second; C is created but only docked when <paramref name="panelCInGroupB"/> is true.</summary>
        public static DockScene Create(bool panelCInGroupB)
        {
            DockScene scene = new();
            MGWindow window = scene.Bench.Window;
            scene.PanelA = new DockPanelNode { Title = "A", ContentFactory = () => Text(window, "Panel A content") };
            scene.PanelB = new DockPanelNode { Title = "B", ContentFactory = () => Text(window, "Panel B content") };
            scene.PanelC = new DockPanelNode { Title = "C", ContentFactory = () => Text(window, "Panel C content") };
            scene.GroupA = new DockTabGroupNode();
            scene.GroupA.AddPanel(scene.PanelA, -1);
            scene.GroupB = new DockTabGroupNode();
            scene.GroupB.AddPanel(scene.PanelB, -1);
            if (panelCInGroupB)
            {
                scene.GroupB.AddPanel(scene.PanelC, -1);
            }

            DockSplitNode root = new()
            {
                Orientation = Orientation.Horizontal,
                SplitRatio = 0.5f,
                MinFirstSize = 0,
                MinSecondSize = 0,
                FirstChild = scene.GroupA,
                SecondChild = scene.GroupB
            };
            scene.Host = new MGDockHost(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                LayoutModel = new DockLayoutModel(root)
            };
            foreach (DockPanelNode node in scene.Host.LayoutModel.GetAllPanels())
            {
                scene.Host.RegisterPanel(node);
            }

            scene.Bench.Show(scene.Host);
            return scene;
        }

        public MGDockTabGroup VisualGroupOf(DockTabGroupNode node)
        {
            foreach (MGDockTabGroup group in Host.GetAllVisibleTabGroups())
            {
                if (ReferenceEquals(group.GroupNode, node))
                {
                    return group;
                }
            }

            throw new InvalidOperationException("the tab group is not visible");
        }
    }

    [Fact]
    public void DockHostWithTwoTabGroups()
    {
        DockScene scene = DockScene.Create(false);
        Assert.Equal(2, scene.Host.GetAllVisibleTabGroups().Count());
        Run("MGDockHost two tab groups and a splitter, at rest", scene.Bench);
    }

    [Fact]
    public void DockHostWithAPanelAutoHidden()
    {
        //  "Hidden" is read as auto-hidden here: the panel leaves the layout tree for the auto-hide strip (UnpinPanel), the drawer stays closed.
        DockScene scene = DockScene.Create(true);
        scene.Host.UnpinPanel(scene.PanelC);
        scene.Bench.Frames(3);
        Assert.False(scene.PanelC.IsPinned);
        Assert.Contains(scene.PanelC, scene.Host.LayoutModel.GetAllAutoHidePanels());
        Assert.Equal(2, scene.Host.GetAllVisibleTabGroups().Count());
        Run("MGDockHost two tab groups, one panel auto-hidden (strip shown, drawer closed), at rest", scene.Bench);
    }

    [Fact]
    public void DockHostWithATabBehindTheActiveTab()
    {
        //  The other reading of "hidden": a panel docked as a tab of a group that shows another tab.
        DockScene scene = DockScene.Create(true);
        Assert.Equal(2, scene.GroupB.Panels.Count);
        Run("MGDockHost two tab groups, one group with two tabs (one tab hidden behind the active one), at rest", scene.Bench);
    }

    [Fact]
    public void DockHostDuringATabDrag()
    {
        DockScene scene = DockScene.Create(false);
        ControlBench bench = scene.Bench;
        MGDockHost host = scene.Host;
        MGDockTabItem tab = scene.VisualGroupOf(scene.GroupA).TraverseVisualTree<MGDockTabItem>(IncludeSelf: false).First();
        Point pressPoint = tab.LayoutBounds.Center;
        Point groupCenter = scene.VisualGroupOf(scene.GroupB).LayoutBounds.Center;
        Point midway = new((pressPoint.X + groupCenter.X) / 2, (pressPoint.Y + groupCenter.Y) / 2);
        Point first = groupCenter;
        Point second = new(groupCenter.X + 8, groupCenter.Y + 6);

        //  Press on the tab, move while pressed (the drag starts when the mouse has moved), then reach the other group: the drop preview shows.
        bench.Mouse = pressPoint;
        bench.Frames(2);
        bench.Left = ButtonState.Pressed;
        bench.Frame();
        bench.Mouse = midway;
        bench.Frame();
        bench.Mouse = first;
        bench.Frames(3);
        Assert.NotNull(host.CurrentDrag);
        Assert.True(host.CurrentDrag.HasExceededThreshold);
        Assert.NotNull(host.CurrentDropTarget);
        DockZone zone = host.CurrentDropTarget.Zone;
        bench.Mouse = second;
        bench.Frames(3);
        Assert.NotNull(host.CurrentDropTarget);
        Assert.Equal(zone, host.CurrentDropTarget.Zone);
        bench.Mouse = first;
        bench.Frames(3);

        Run($"MGDockHost tab drag over a group, drop preview shown (zone {zone}), mouse alternating between two positions", bench, frame => bench.Mouse = (frame & 1) == 0 ? first : second);

        Assert.NotNull(host.CurrentDrag);
        Assert.NotNull(host.CurrentDropTarget);
        bench.Left = ButtonState.Released;
        bench.Frames(2);
    }

    [Fact]
    public void DockHostAfterATextChange()
    {
        //  The case the plan knows: one dock host with one tab group and one panel, one text change, then idle frames.
        var outside = new Point(600, 340);

        //  The change made before the warm-up frames.
        {
            DesktopFrameAllocationMeasurements.Harness harness = DesktopFrameAllocationMeasurements.Harness.Create(DesktopFrameAllocationMeasurements.Fixture.FullInDockHost);
            Assert.NotNull(harness.TextBlock);
            harness.TextBlock.Text = "Value changed";
            long update = DesktopFrameAllocationMeasurements.Measure(() => harness.Update(outside, ButtonState.Released));
            long draw = DesktopFrameAllocationMeasurements.Measure(() => harness.Draw());
            Assert.True(harness.Transaction.CallCount > 0);
            Output.WriteLine($"MGDockHost FullInDockHost, one text change before the warm-up, then idle: Update {update} B/frame, Draw {draw} B/frame");
        }

        //  The change made after the warm-up frames, with the idle frames that follow it measured at once, then also the first frames one by one.
        {
            DesktopFrameAllocationMeasurements.Harness harness = DesktopFrameAllocationMeasurements.Harness.Create(DesktopFrameAllocationMeasurements.Fixture.FullInDockHost);
            Assert.NotNull(harness.TextBlock);
            for (int i = 0; i < FrameAllocationMeter.WarmupFrames; i++)
            {
                harness.Frame(outside, ButtonState.Released);
            }

            harness.TextBlock.Text = "Value changed";
            long[] firstFrames = new long[6];
            AllocationWindow.Start();
            for (int i = 0; i < firstFrames.Length; i++)
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                harness.Frame(outside, ButtonState.Released);
                firstFrames[i] = GC.GetAllocatedBytesForCurrentThread() - before;
            }

            long updateTotal = 0;
            long drawTotal = 0;
            AllocationWindow.Start();
            for (int i = 0; i < FrameAllocationMeter.MeasuredFrames; i++)
            {
                long start = GC.GetAllocatedBytesForCurrentThread();
                harness.Update(outside, ButtonState.Released);
                long afterUpdate = GC.GetAllocatedBytesForCurrentThread();
                harness.Draw();
                updateTotal += afterUpdate - start;
                drawTotal += GC.GetAllocatedBytesForCurrentThread() - afterUpdate;
            }

            long update = (updateTotal + FrameAllocationMeter.MeasuredFrames - 1) / FrameAllocationMeter.MeasuredFrames;
            long draw = (drawTotal + FrameAllocationMeter.MeasuredFrames - 1) / FrameAllocationMeter.MeasuredFrames;
            Output.WriteLine($"MGDockHost FullInDockHost, one text change after the warm-up, no warm-up after it: first 6 frames (Update+Draw) {string.Join(", ", firstFrames)} B; "
                + $"then the next {FrameAllocationMeter.MeasuredFrames} idle frames: Update {update} B/frame, Draw {draw} B/frame");
        }
    }

    // ---- MGWindow SizeToContent ----

    [Theory]
    [InlineData(SizeToContent.WidthAndHeight)]
    [InlineData(SizeToContent.Height)]
    public void WindowWithSizeToContent(SizeToContent sizeToContent)
    {
        DesktopFrameAllocationMeasurements.Harness harness = DesktopFrameAllocationMeasurements.Harness.Create(DesktopFrameAllocationMeasurements.Fixture.Full);
        var outside = new Point(600, 340);
        int widthBefore = harness.Window.WindowWidth;
        int heightBefore = harness.Window.WindowHeight;
        harness.Window.ApplySizeToContent(sizeToContent);
        harness.Frame(outside, ButtonState.Released);
        harness.Frame(outside, ButtonState.Released);
        Output.WriteLine($"SizeToContent {sizeToContent}: window {widthBefore}x{heightBefore} before, {harness.Window.WindowWidth}x{harness.Window.WindowHeight} after");
        Assert.True(harness.Window.WindowHeight != heightBefore || harness.Window.WindowWidth != widthBefore);

        long callsBefore = harness.Transaction.CallCount;
        long update = DesktopFrameAllocationMeasurements.Measure(() => harness.Update(outside, ButtonState.Released));
        long draw = DesktopFrameAllocationMeasurements.Measure(() => harness.Draw());
        Assert.True(harness.Transaction.CallCount > callsBefore);
        Output.WriteLine($"MGWindow SizeToContent {sizeToContent} (stack panel with a text block and a slider), at rest: Update {update} B/frame, Draw {draw} B/frame");
    }

    // ---- MGScrollViewer ----

    [Fact]
    public void ScrollViewerWithTallContent()
    {
        ControlBench bench = new();
        MGScrollViewer scrollViewer = new(bench.Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        MGStackPanel panel = new(bench.Window, Orientation.Vertical);
        for (int i = 0; i < 100; i++)
        {
            Assert.True(panel.TryAddChild(Text(bench.Window, $"Row {i}")));
        }

        scrollViewer.SetContent(panel);
        bench.Show(scrollViewer);
        Assert.True(scrollViewer.MaxVerticalOffset > 0f);

        Run("MGScrollViewer, content taller than the viewport, at rest", bench);

        //  The wheel turns up for four frames, then down for four frames, so the offset moves every frame and never rests on a limit. The mouse is over the viewer.
        bench.Mouse = scrollViewer.LayoutBounds.Center;
        for (int i = 0; i < 6; i++)
        {
            bench.Wheel -= 120;
            bench.Frame();
        }

        Action<int> wheel = frame => bench.Wheel += (frame / 4) % 2 == 0 ? 120 : -120;
        float minOffset = float.MaxValue;
        float maxOffset = float.MinValue;
        for (int frame = 0; frame < 16; frame++)
        {
            wheel(frame);
            bench.Frame();
            minOffset = Math.Min(minOffset, scrollViewer.VerticalOffset);
            maxOffset = Math.Max(maxOffset, scrollViewer.VerticalOffset);
        }

        Output.WriteLine($"MGScrollViewer wheel probe: offset between {minOffset} and {maxOffset}, maximum {scrollViewer.MaxVerticalOffset}");
        Assert.True(maxOffset > minOffset, "the wheel does not move the content");
        Assert.True(minOffset > 0f && maxOffset < scrollViewer.MaxVerticalOffset, "the offset reached a limit");

        Run("MGScrollViewer, mouse wheel (4 frames up, 4 frames down, one notch per frame)", bench, wheel);
        Assert.True(scrollViewer.VerticalOffset > 0f && scrollViewer.VerticalOffset < scrollViewer.MaxVerticalOffset);
    }

    // ---- MGListBox, MGListView ----

    private static List<string> Items(int count)
    {
        List<string> items = new(count);
        for (int i = 0; i < count; i++)
        {
            items.Add($"Item {i}");
        }

        return items;
    }

    [Fact]
    public void ListBoxOf50Items()
    {
        ControlBench bench = new();
        MGListBox<string> listBox = new(bench.Window);
        listBox.SetItemsSource(Items(50));
        bench.Show(listBox);
        MGBorder firstItem = listBox.ListBoxItems[1].ContentPresenter;
        MGBorder secondItem = listBox.ListBoxItems[2].ContentPresenter;
        Point first = firstItem.ActualLayoutBounds.Center;
        Point second = secondItem.ActualLayoutBounds.Center;
        Assert.NotEqual(first, second);

        Run("MGListBox 50 items, at rest", bench);

        //  The list box tracks its hovered item itself (a mouse handler on the list box, not the hit-test of the item): the item draws its hovered background.
        bench.Mouse = first;
        bench.Frames(2);
        Assert.True(firstItem.SpoofIsHoveredWhileDrawingBackground);
        Assert.False(secondItem.SpoofIsHoveredWhileDrawingBackground);
        bench.Mouse = second;
        bench.Frames(2);
        Assert.False(firstItem.SpoofIsHoveredWhileDrawingBackground);
        Assert.True(secondItem.SpoofIsHoveredWhileDrawingBackground);
        Run("MGListBox 50 items, mouse alternating between two adjacent items", bench, frame => bench.Mouse = (frame & 1) == 0 ? first : second);
    }

    [Fact]
    public void ListViewOf50Items()
    {
        ControlBench bench = new();
        MGListView<string> listView = new(bench.Window);
        MGListViewColumn<string> nameColumn = listView.AddColumn(new ListViewColumnWidth(150), Text(bench.Window, "Name"), item => Text(bench.Window, item));
        listView.AddColumn(new ListViewColumnWidth(1.0), Text(bench.Window, "Length"), item => Text(bench.Window, item.Length.ToString()));
        listView.SetItemsSource(Items(50));
        bench.Show(listView);
        MGElement firstCell = listView.RowItems[1].GetRowContents()[nameColumn];
        MGElement secondCell = listView.RowItems[2].GetRowContents()[nameColumn];
        Point first = firstCell.ActualLayoutBounds.Center;
        Point second = secondCell.ActualLayoutBounds.Center;
        Assert.NotEqual(first, second);

        Run("MGListView 50 items, 2 columns, at rest", bench);

        bench.AssertHovers(bench.Window, firstCell, first);
        bench.AssertHovers(bench.Window, secondCell, second);
        Run("MGListView 50 items, 2 columns, mouse alternating between two adjacent rows", bench, frame => bench.Mouse = (frame & 1) == 0 ? first : second);
    }

    // ---- MGTreeView ----

    [Fact]
    public void TreeViewOfThreeLevels()
    {
        ControlBench bench = new();
        MGTreeView tree = new(bench.Window);
        MGTreeViewItem[] visible = new MGTreeViewItem[2 + 4 + 8];
        int count = 0;
        for (int root = 0; root < 2; root++)
        {
            MGTreeViewItem rootItem = new(bench.Window) { Header = $"Root {root}", IsExpanded = true };
            visible[count++] = rootItem;
            for (int child = 0; child < 2; child++)
            {
                MGTreeViewItem childItem = new(bench.Window) { Header = $"Child {root}.{child}", IsExpanded = true };
                visible[count++] = childItem;
                rootItem.AddItem(childItem);
                for (int leaf = 0; leaf < 2; leaf++)
                {
                    MGTreeViewItem leafItem = new(bench.Window) { Header = $"Leaf {root}.{child}.{leaf}" };
                    visible[count++] = leafItem;
                    childItem.AddItem(leafItem);
                }
            }

            tree.AddItem(rootItem);
        }

        bench.Show(tree);
        MGTreeViewItem firstNode = visible[2];
        MGTreeViewItem secondNode = visible[3];
        Assert.Equal(2, firstNode.Level);
        Point first = firstNode.HeaderContent.ActualLayoutBounds.Center;
        Point second = secondNode.HeaderContent.ActualLayoutBounds.Center;
        Assert.NotEqual(first, second);

        Run("MGTreeView 14 nodes, three levels expanded, at rest", bench);

        bench.AssertHovers(bench.Window, firstNode.HeaderContent, first);
        bench.AssertHovers(bench.Window, secondNode.HeaderContent, second);
        Run("MGTreeView 14 nodes, three levels expanded, mouse alternating between two nodes", bench, frame => bench.Mouse = (frame & 1) == 0 ? first : second);
    }

    // ---- MGTabControl ----

    [Fact]
    public void TabControlWithThreeTabs()
    {
        ControlBench bench = new();
        MGTabControl tabControl = new(bench.Window);
        MGTabItem[] tabs =
        {
            tabControl.AddTab("One", Text(bench.Window, "first content")),
            tabControl.AddTab("Two", Text(bench.Window, "second content")),
            tabControl.AddTab("Three", Text(bench.Window, "third content"))
        };
        bench.Show(tabControl);
        Point first = tabs[0].Header.ActualLayoutBounds.Center;
        Point second = tabs[1].Header.ActualLayoutBounds.Center;
        Assert.NotEqual(first, second);

        Run("MGTabControl 3 tabs, at rest", bench);

        bench.AssertHovers(bench.Window, tabs[0].Header, first);
        bench.AssertHovers(bench.Window, tabs[1].Header, second);
        Run("MGTabControl 3 tabs, mouse alternating between two headers", bench, frame => bench.Mouse = (frame & 1) == 0 ? first : second);
    }

    // ---- MGContextMenu, MGMenuBar ----

    private static MGContextMenu CreateMenu(MGWindow window, int itemCount)
    {
        MGContextMenu menu = new(window, "");
        for (int i = 0; i < itemCount; i++)
        {
            menu.AddButton($"Action {i}", _ => { });
        }

        return menu;
    }

    [Fact]
    public void ContextMenuOpen()
    {
        ControlBench bench = new();
        bench.Show(Text(bench.Window, "under the menu"));
        MGContextMenu menu = CreateMenu(bench.Window, 6);
        Assert.True(bench.Desktop.TryOpenContextMenu(menu, new Point(60, 60)));
        bench.Frames(3);
        Assert.True(menu.IsContextMenuOpen);
        Point first = menu.Items[1].ActualLayoutBounds.Center;
        Point second = menu.Items[2].ActualLayoutBounds.Center;
        Assert.NotEqual(first, second);

        Run("MGContextMenu 6 items open, mouse outside the menu", bench);

        bench.AssertHovers(menu, menu.Items[1], first);
        bench.AssertHovers(menu, menu.Items[2], second);
        Run("MGContextMenu 6 items open, mouse alternating between two items", bench, frame => bench.Mouse = (frame & 1) == 0 ? first : second);
        Assert.True(menu.IsContextMenuOpen);
    }

    [Fact]
    public void MenuBar()
    {
        ControlBench bench = new();
        MGMenuBar menuBar = new(bench.Window) { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        MGMenuBarItem file = menuBar.AddItem("File", item => item.Submenu = CreateMenu(bench.Window, 4));
        MGMenuBarItem edit = menuBar.AddItem("Edit", item => item.Submenu = CreateMenu(bench.Window, 4));
        menuBar.AddItem("View", item => item.Submenu = CreateMenu(bench.Window, 4));
        bench.Show(menuBar);
        Point first = file.ActualLayoutBounds.Center;
        Point second = edit.ActualLayoutBounds.Center;
        Assert.NotEqual(first, second);

        Run("MGMenuBar 3 items, no menu open, at rest", bench);

        bench.AssertHovers(bench.Window, file, first);
        bench.AssertHovers(bench.Window, edit, second);
        Run("MGMenuBar 3 items, no menu open, mouse alternating between two items", bench, frame => bench.Mouse = (frame & 1) == 0 ? first : second);
        Assert.False(menuBar.IsMenuActive);

        //  The dropdown of the first item open (clicked), mouse over two of its entries.
        bench.Mouse = first;
        bench.Left = ButtonState.Pressed;
        bench.Frame();
        bench.Left = ButtonState.Released;
        bench.Frames(3);
        Assert.True(menuBar.IsMenuActive);
        MGContextMenu dropdown = file.Submenu;
        Assert.True(dropdown.IsContextMenuOpen);
        Point firstEntry = dropdown.Items[1].ActualLayoutBounds.Center;
        Point secondEntry = dropdown.Items[2].ActualLayoutBounds.Center;
        Assert.NotEqual(firstEntry, secondEntry);
        bench.AssertHovers(dropdown, dropdown.Items[1], firstEntry);
        bench.AssertHovers(dropdown, dropdown.Items[2], secondEntry);
        Run("MGMenuBar dropdown of the first item open, mouse alternating between two entries", bench, frame => bench.Mouse = (frame & 1) == 0 ? firstEntry : secondEntry);
        Assert.True(menuBar.IsMenuActive);
    }

    // ---- MGTextBox ----

    [Fact]
    public void TextBoxWithTheKeyboardFocus()
    {
        ControlBench bench = new();
        MGTextBox textBox = new(bench.Window) { PreferredWidth = 200 };
        bench.Show(textBox);

        //  A real click gives the focus and places the caret, as in the native input regression test.
        Point inside = textBox.LayoutBounds.Center;
        bench.Mouse = inside;
        bench.Left = ButtonState.Pressed;
        bench.Frame();
        bench.Left = ButtonState.Released;
        bench.Frames(2);
        Assert.Same(textBox, bench.Desktop.FocusedKeyboardHandler);
        Assert.True(textBox.Caret.HasPosition);

        //  The caret blinks on the wall clock (MGTextCaret.SecondsShown reads DateTime.Now), not on the time of the update: the frames of one measurement are
        //  a few milliseconds apart in real time, so the default blink is not under the control of the test. Both are measured: the default blink (the caret is
        //  shown or hidden depending on the real time at which the frames run) and a steady caret (a non positive blink rate draws it on every frame).
        Run("MGTextBox focused, empty, default caret blink (wall clock), at rest", bench);
        textBox.Caret.BlinkRate = TimeSpan.Zero;
        bench.Frame();
        Assert.True(textBox.Caret.IsCurrentlyVisible);
        Run("MGTextBox focused, empty, steady caret (blink rate 0), at rest", bench);
        textBox.Caret.BlinkRate = TimeSpan.FromSeconds(0.5);

        //  One key per frame: A then Backspace, so that the text alternates between "a" and "" and stays bounded. Each frame presses a key the previous frame did not.
        KeyboardState keyA = new(Keys.A);
        KeyboardState keyBack = new(Keys.Back);
        Action<int> type = frame => bench.Keyboard = (frame & 1) == 0 ? keyA : keyBack;
        type(0);
        bench.Frame();
        Assert.Equal("a", textBox.Text);
        type(1);
        bench.Frame();
        Assert.Equal("", textBox.Text);

        Run("MGTextBox focused, one key per frame (A then Backspace, text alternates between \"a\" and empty), default caret blink", bench, type);
        Assert.Same(textBox, bench.Desktop.FocusedKeyboardHandler);
        textBox.Caret.BlinkRate = TimeSpan.Zero;
        Run("MGTextBox focused, one key per frame (A then Backspace, text alternates between \"a\" and empty), steady caret", bench, type);
        Assert.Same(textBox, bench.Desktop.FocusedKeyboardHandler);
        bench.Keyboard = default;
    }
}
