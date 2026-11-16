using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit.Abstractions;

namespace MGUI.Tests.Architecture;

/// <summary>
/// The control families of <see cref="ControlFamilyAllocationMeasurements"/> that a frame of the desktop (<c>MGDesktop.Update()</c> then <c>MGDesktop.Draw(IUIDrawTransaction)</c>
/// into a <see cref="NonRecordingDrawTransaction"/>) allocates nothing for: exact total of 0 bytes over <see cref="FrameAllocationMeter.MeasuredFrames"/> frames after
/// <see cref="FrameAllocationMeter.WarmupFrames"/> warm-up frames. They run in the collection of the measurements: a measurement counts the bytes of its own thread
/// (see <see cref="AllocationMeasurementsCollection"/>).
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class ControlFamilyZeroAllocationTests
{
    private readonly ITestOutputHelper Output;

    public ControlFamilyZeroAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    // ---- MGScrollViewer, content taller than the viewport ----

    /// <summary>A scroll viewer that fills the window, over a vertical stack of 100 text blocks: its content is much taller than its viewport.</summary>
    private static MGScrollViewer CreateTallScrollViewer(ControlBench bench)
    {
        MGScrollViewer scrollViewer = new(bench.Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        MGStackPanel panel = new(bench.Window, Orientation.Vertical);
        for (int i = 0; i < 100; i++)
        {
            Assert.True(panel.TryAddChild(new MGTextBlock(bench.Window, $"Row {i}")));
        }

        scrollViewer.SetContent(panel);
        bench.Show(scrollViewer);
        Assert.True(scrollViewer.MaxVerticalOffset > 0f);
        return scrollViewer;
    }

    /// <summary>The wheel turns up for four frames, then down for four frames, one notch per frame: the offset moves every frame and never rests on a limit.</summary>
    private static Action<int> StartWheel(ControlBench bench, MGScrollViewer scrollViewer)
    {
        bench.Mouse = scrollViewer.LayoutBounds.Center;
        for (int i = 0; i < 6; i++)
        {
            bench.Wheel -= 120;
            bench.Frame();
        }

        return frame => bench.Wheel += (frame / 4) % 2 == 0 ? 120 : -120;
    }

    private FrameBytes MeasureScrollViewerAtRest()
    {
        ControlBench bench = new();
        MGScrollViewer scrollViewer = CreateTallScrollViewer(bench);
        long callsBefore = bench.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(null, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, "the measured frames drew nothing");
        Output.WriteLine($"MGScrollViewer at rest: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    private FrameBytes MeasureScrollViewerWithTheWheel()
    {
        ControlBench bench = new();
        MGScrollViewer scrollViewer = CreateTallScrollViewer(bench);
        Action<int> wheel = StartWheel(bench, scrollViewer);
        long callsBefore = bench.Transaction.CallCount;
        float minOffset = float.MaxValue;
        float maxOffset = float.MinValue;
        FrameBytes bytes = FrameAllocationMeter.Measure(frame =>
        {
            wheel(frame);
            minOffset = Math.Min(minOffset, scrollViewer.VerticalOffset);
            maxOffset = Math.Max(maxOffset, scrollViewer.VerticalOffset);
        }, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, "the measured frames drew nothing");

        //  The offset read before each update is the one of the previous frame: it moved, and stayed between the limits.
        Assert.True(maxOffset > minOffset, "the wheel does not move the content");
        Assert.True(minOffset > 0f && maxOffset < scrollViewer.MaxVerticalOffset, "the offset reached a limit");
        Output.WriteLine($"MGScrollViewer with the wheel: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    [Fact]
    public void ScrollViewerWithTallContent_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureScrollViewerAtRest().UpdateTotal);

    [Fact]
    public void ScrollViewerWithTallContent_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureScrollViewerAtRest().DrawTotal);

    /// <summary>The update of a frame whose wheel moved allocates the one <see cref="BaseMouseScrolledEventArgs"/> that <c>MouseTracker.Update</c> creates for the wheel, and nothing else:
    /// that object lives in MGUI.Shared (public class of the input, whose instance a handler may keep), outside the files of the scroll viewer work. When the tracker reuses
    /// it, as it does for the move event, the expected value below becomes 0 and this test becomes the zero test of the wheel update.</summary>
    [Fact]
    public void ScrollViewerWithTallContent_Update_WithTheMouseWheel_AllocatesOnlyTheScrollEventArgs()
    {
        MouseTracker tracker = new InputTracker().Mouse;
        GC.KeepAlive(new BaseMouseScrolledEventArgs(tracker, 120, Point.Zero));
        AllocationWindow.Start();
        long before = GC.GetAllocatedBytesForCurrentThread();
        BaseMouseScrolledEventArgs scrolled = new(tracker, 120, Point.Zero);
        long scrolledEventBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(scrolled);
        Assert.True(scrolledEventBytes > 0);

        //  Every measured frame moves the wheel (MeasureScrollViewerWithTheWheel), so every one creates one scroll event.
        FrameBytes bytes = MeasureScrollViewerWithTheWheel();
        Assert.Equal(scrolledEventBytes * bytes.Iterations, bytes.UpdateTotal);
    }

    [Fact]
    public void ScrollViewerWithTallContent_Draw_WithTheMouseWheel_AllocatesNothing() => Assert.Equal(0, MeasureScrollViewerWithTheWheel().DrawTotal);

    /// <summary>The draw journal of the scroll viewer (every call of the transaction, with the clips and their names): one frame at rest, then four notches down,
    /// one notch up and two frames at rest. Frozen on the code before the allocation-free work, kept after it.</summary>
    [Fact]
    public void ScrollViewerWithTallContent_DrawJournal_AtRestAndAfterWheelNotches()
    {
        ControlBench bench = new();
        MGScrollViewer scrollViewer = CreateTallScrollViewer(bench);
        OrderedRecordingDrawTransaction transaction = new(bench.Runtime, DrawSettings.Default);

        List<string> journal = new();
        void Record(string label)
        {
            transaction.ClearJournal();
            bench.Update();
            bench.Desktop.Draw(transaction);
            journal.Add($"== {label} offset={scrollViewer.VerticalOffset} max={scrollViewer.MaxVerticalOffset}");
            journal.AddRange(transaction.Lines);
        }

        Record("rest");
        bench.Mouse = scrollViewer.LayoutBounds.Center;
        for (int i = 0; i < 4; i++)
        {
            bench.Wheel -= 120;
            Record($"notch down {i}");
        }

        bench.Wheel += 120;
        Record("notch up");
        Record("rest after the wheel");
        Record("rest after the wheel, again");

        FrozenJournal.Assert("scroll viewer draw journal", 2912, "DDC79E6AEAE75E01", journal);
    }

    // ---- MGListBox and MGListView of 50 items ----

    /// <summary>A list control of 50 items in the window, with two adjacent items for the mouse to alternate between (the centers of two rows).</summary>
    private sealed record ListScene(ControlBench Bench, Point First, Point Second)
    {
        public Action<int> Alternate => frame => Bench.Mouse = (frame & 1) == 0 ? First : Second;
    }

    private static List<string> Items(int count)
    {
        List<string> items = new(count);
        for (int i = 0; i < count; i++)
        {
            items.Add($"Item {i}");
        }

        return items;
    }

    private static ListScene CreateListBoxScene()
    {
        ControlBench bench = new();
        MGListBox<string> listBox = new(bench.Window);
        listBox.SetItemsSource(Items(50));
        bench.Show(listBox);
        MGBorder firstItem = listBox.ListBoxItems[1].ContentPresenter;
        MGBorder secondItem = listBox.ListBoxItems[2].ContentPresenter;
        ListScene scene = new(bench, firstItem.ActualLayoutBounds.Center, secondItem.ActualLayoutBounds.Center);
        Assert.NotEqual(scene.First, scene.Second);

        //  The list box tracks its hovered item itself: the item under the mouse draws its hovered background.
        bench.Mouse = scene.First;
        bench.Frames(2);
        Assert.True(firstItem.SpoofIsHoveredWhileDrawingBackground);
        Assert.False(secondItem.SpoofIsHoveredWhileDrawingBackground);
        bench.Mouse = scene.Second;
        bench.Frames(2);
        Assert.False(firstItem.SpoofIsHoveredWhileDrawingBackground);
        Assert.True(secondItem.SpoofIsHoveredWhileDrawingBackground);
        bench.Mouse = ControlBench.OutsideTheWindow;
        bench.Frames(2);
        Assert.False(secondItem.SpoofIsHoveredWhileDrawingBackground);
        return scene;
    }

    private static ListScene CreateListViewScene()
    {
        ControlBench bench = new();
        MGListView<string> listView = new(bench.Window);
        MGListViewColumn<string> nameColumn = listView.AddColumn(new ListViewColumnWidth(150), new MGTextBlock(bench.Window, "Name"), item => new MGTextBlock(bench.Window, item));
        listView.AddColumn(new ListViewColumnWidth(1.0), new MGTextBlock(bench.Window, "Length"), item => new MGTextBlock(bench.Window, item.Length.ToString()));
        listView.SetItemsSource(Items(50));
        bench.Show(listView);
        MGElement firstCell = listView.RowItems[1].GetRowContents()[nameColumn];
        MGElement secondCell = listView.RowItems[2].GetRowContents()[nameColumn];
        ListScene scene = new(bench, firstCell.ActualLayoutBounds.Center, secondCell.ActualLayoutBounds.Center);
        Assert.NotEqual(scene.First, scene.Second);
        bench.AssertHovers(bench.Window, firstCell, scene.First);
        bench.AssertHovers(bench.Window, secondCell, scene.Second);
        bench.Mouse = ControlBench.OutsideTheWindow;
        bench.Frames(2);
        return scene;
    }

    private FrameBytes MeasureList(string label, ListScene scene, bool hover)
    {
        ControlBench bench = scene.Bench;
        long callsBefore = bench.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(hover ? scene.Alternate : null, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, "the measured frames drew nothing");
        Output.WriteLine($"{label}{(hover ? ", mouse alternating between two adjacent items" : " at rest")}: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    [Fact]
    public void ListBoxOf50Items_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGListBox", CreateListBoxScene(), false).UpdateTotal);

    [Fact]
    public void ListBoxOf50Items_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGListBox", CreateListBoxScene(), false).DrawTotal);

    [Fact]
    public void ListBoxOf50Items_Update_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGListBox", CreateListBoxScene(), true).UpdateTotal);

    [Fact]
    public void ListBoxOf50Items_Draw_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGListBox", CreateListBoxScene(), true).DrawTotal);

    [Fact]
    public void ListViewOf50Items_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGListView", CreateListViewScene(), false).UpdateTotal);

    [Fact]
    public void ListViewOf50Items_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGListView", CreateListViewScene(), false).DrawTotal);

    [Fact]
    public void ListViewOf50Items_Update_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGListView", CreateListViewScene(), true).UpdateTotal);

    [Fact]
    public void ListViewOf50Items_Draw_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGListView", CreateListViewScene(), true).DrawTotal);

    /// <summary>The draw journal of a list control: two frames at rest, then two frames with the mouse on the first item, on the second, and on the first again.
    /// Frozen on the code before the allocation-free work, kept after it.</summary>
    private static List<string> ListDrawJournal(ListScene scene)
    {
        ControlBench bench = scene.Bench;
        OrderedRecordingDrawTransaction transaction = new(bench.Runtime, DrawSettings.Default);
        List<string> journal = new();
        void Record(string label, Point mouse)
        {
            bench.Mouse = mouse;
            for (int frame = 0; frame < 2; frame++)
            {
                transaction.ClearJournal();
                bench.Update();
                bench.Desktop.Draw(transaction);
                journal.Add($"== {label}, frame {frame}");
                journal.AddRange(transaction.Lines);
            }
        }

        Record("rest", ControlBench.OutsideTheWindow);
        Record("first item", scene.First);
        Record("second item", scene.Second);
        Record("first item again", scene.First);
        Record("rest again", ControlBench.OutsideTheWindow);
        return journal;
    }

    [Fact]
    public void ListBoxOf50Items_DrawJournal_AtRestAndOnHover()
        => FrozenJournal.Assert("list box draw journal", 5876, "4F799554548B59F5", ListDrawJournal(CreateListBoxScene()));

    [Fact]
    public void ListViewOf50Items_DrawJournal_AtRestAndOnHover()
        => FrozenJournal.Assert("list view draw journal", 4430, "C44BDC8BCB28886C", ListDrawJournal(CreateListViewScene()));

    // ---- MGTreeView of 14 nodes, three levels expanded ----

    private static ListScene CreateTreeViewScene()
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
        ListScene scene = new(bench, firstNode.HeaderContent.ActualLayoutBounds.Center, secondNode.HeaderContent.ActualLayoutBounds.Center);
        Assert.NotEqual(scene.First, scene.Second);
        bench.AssertHovers(bench.Window, firstNode.HeaderContent, scene.First);
        bench.AssertHovers(bench.Window, secondNode.HeaderContent, scene.Second);
        bench.Mouse = ControlBench.OutsideTheWindow;
        bench.Frames(2);
        return scene;
    }

    [Fact]
    public void TreeViewOfThreeLevels_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGTreeView", CreateTreeViewScene(), false).UpdateTotal);

    [Fact]
    public void TreeViewOfThreeLevels_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGTreeView", CreateTreeViewScene(), false).DrawTotal);

    [Fact]
    public void TreeViewOfThreeLevels_Update_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGTreeView", CreateTreeViewScene(), true).UpdateTotal);

    [Fact]
    public void TreeViewOfThreeLevels_Draw_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGTreeView", CreateTreeViewScene(), true).DrawTotal);

    [Fact]
    public void TreeViewOfThreeLevels_DrawJournal_AtRestAndOnHover()
        => FrozenJournal.Assert("tree view draw journal", 4280, "0F2EE6FCC35D9D5F", ListDrawJournal(CreateTreeViewScene()));

    // ---- MGTabControl with three tabs ----

    private static ListScene CreateTabControlScene()
    {
        ControlBench bench = new();
        MGTabControl tabControl = new(bench.Window);
        MGTabItem[] tabs =
        {
            tabControl.AddTab("One", new MGTextBlock(bench.Window, "first content")),
            tabControl.AddTab("Two", new MGTextBlock(bench.Window, "second content")),
            tabControl.AddTab("Three", new MGTextBlock(bench.Window, "third content"))
        };
        bench.Show(tabControl);
        ListScene scene = new(bench, tabs[0].Header.ActualLayoutBounds.Center, tabs[1].Header.ActualLayoutBounds.Center);
        Assert.NotEqual(scene.First, scene.Second);
        bench.AssertHovers(bench.Window, tabs[0].Header, scene.First);
        bench.AssertHovers(bench.Window, tabs[1].Header, scene.Second);
        bench.Mouse = ControlBench.OutsideTheWindow;
        bench.Frames(2);
        return scene;
    }

    [Fact]
    public void TabControlWithThreeTabs_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGTabControl", CreateTabControlScene(), false).UpdateTotal);

    [Fact]
    public void TabControlWithThreeTabs_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGTabControl", CreateTabControlScene(), false).DrawTotal);

    [Fact]
    public void TabControlWithThreeTabs_Update_OnHeaderHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGTabControl", CreateTabControlScene(), true).UpdateTotal);

    [Fact]
    public void TabControlWithThreeTabs_Draw_OnHeaderHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGTabControl", CreateTabControlScene(), true).DrawTotal);

    [Fact]
    public void TabControlWithThreeTabs_DrawJournal_AtRestAndOnHeaderHover()
        => FrozenJournal.Assert("tab control draw journal", 1100, "C3124B89E3FC9739", ListDrawJournal(CreateTabControlScene()));

    /// <summary>The visual tree children of the tab control are kept between calls, and follow the tabs and the selection: a list that was returned is not changed by a later selection.</summary>
    [Fact]
    public void TabControlWithThreeTabs_VisualTreeChildren_FollowTheSelection()
    {
        ControlBench bench = new();
        MGTabControl tabControl = new(bench.Window);
        MGTabItem one = tabControl.AddTab("One", new MGTextBlock(bench.Window, "first content"));
        MGTabItem two = tabControl.AddTab("Two", new MGTextBlock(bench.Window, "second content"));
        MGTabItem three = tabControl.AddTab("Three", new MGTextBlock(bench.Window, "third content"));
        bench.Show(tabControl);
        Assert.Same(one, tabControl.SelectedTab);

        IReadOnlyList<MGElement> all = tabControl.GetVisualTreeChildren(true, true);
        IReadOnlyList<MGElement> active = tabControl.GetVisualTreeChildren(false, true);
        Assert.Same(all, tabControl.GetVisualTreeChildren(true, true));
        Assert.Equal(new MGElement[] { two, three, one }, all);
        Assert.Equal(new MGElement[] { one }, active);
        Assert.Equal(new MGElement[] { two, three }, tabControl.GetVisualTreeChildren(true, false));
        Assert.Empty(tabControl.GetVisualTreeChildren(false, false));

        Assert.True(tabControl.TrySelectTab(two));
        Assert.Equal(new MGElement[] { one, three, two }, tabControl.GetVisualTreeChildren(true, true));
        Assert.Equal(new MGElement[] { two }, tabControl.GetVisualTreeChildren(false, true));
        Assert.Equal(new MGElement[] { one, three }, tabControl.GetVisualTreeChildren(true, false));

        //  The lists returned before the selection changed still hold what they held.
        Assert.Equal(new MGElement[] { two, three, one }, all);
        Assert.Equal(new MGElement[] { one }, active);
    }
}
