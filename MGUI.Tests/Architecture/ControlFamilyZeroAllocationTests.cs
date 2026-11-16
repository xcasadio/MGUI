using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework.Input;
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

    /// <summary>The update of a frame whose wheel moved allocates nothing: <c>MouseTracker</c> refills one <see cref="BaseMouseScrolledEventArgs"/> at each wheel tick (ADR-0034),
    /// as it does for the move event.</summary>
    [Fact]
    public void ScrollViewerWithTallContent_Update_WithTheMouseWheel_AllocatesNothing() => Assert.Equal(0, MeasureScrollViewerWithTheWheel().UpdateTotal);

    [Fact]
    public void ScrollViewerWithTallContent_Draw_WithTheMouseWheel_AllocatesNothing() => Assert.Equal(0, MeasureScrollViewerWithTheWheel().DrawTotal);

    /// <summary>A continuous scroll: every frame, before the update, the offset of the viewer is one pixel further down than at the previous frame, so that no offset repeats
    /// over the <see cref="FrameAllocationMeter.WarmupFrames"/> warm-up frames and the <see cref="FrameAllocationMeter.MeasuredFrames"/> measured ones. Each offset gives the content
    /// bounds that no frame had before, and so two clips of new bounds (the viewport clip does not move, the clips of the content and of its children do). The prepare step
    /// that writes the offset is measured on its own and is not part of the draw.</summary>
    private FrameBytes MeasureScrollViewerOnFreshOffsets()
    {
        ControlBench bench = new();
        MGScrollViewer scrollViewer = CreateTallScrollViewer(bench);
        int frames = FrameAllocationMeter.WarmupFrames + FrameAllocationMeter.MeasuredFrames;
        Assert.True(scrollViewer.MaxVerticalOffset >= frames, $"the content scrolls over {scrollViewer.MaxVerticalOffset} pixels, less than the {frames} frames");

        long callsBefore = bench.Transaction.CallCount;
        float previousOffset = -1f;
        int differentOffsets = 0;
        FrameBytes bytes = FrameAllocationMeter.Measure(frame =>
        {
            scrollViewer.VerticalOffset = frame;
            if (frame >= FrameAllocationMeter.WarmupFrames && scrollViewer.VerticalOffset != previousOffset)
            {
                differentOffsets++;
            }

            previousOffset = scrollViewer.VerticalOffset;
        }, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, "the measured frames drew nothing");
        Assert.Equal(FrameAllocationMeter.MeasuredFrames, differentOffsets);
        Output.WriteLine($"MGScrollViewer over {FrameAllocationMeter.MeasuredFrames} offsets that are all different: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    [Fact]
    public void ScrollViewerWithTallContent_Draw_OverOffsetsThatAreAllDifferent_AllocatesNothing() => Assert.Equal(0, MeasureScrollViewerOnFreshOffsets().DrawTotal);

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

        FrozenJournal.Assert("scroll viewer draw journal", 2912, "9E62836A6CD2B516", journal);
    }

    // ---- MGListBox and MGListView of 50 items ----

    /// <summary>A list control of 50 items in the window, with two adjacent items for the mouse to alternate between (the centers of two rows).</summary>
    private sealed record ListScene(ControlBench Bench, Point First, Point Second, Point? Rest = null)
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

        Record("rest", scene.Rest ?? ControlBench.OutsideTheWindow);
        Record("first item", scene.First);
        Record("second item", scene.Second);
        Record("first item again", scene.First);
        Record("rest again", scene.Rest ?? ControlBench.OutsideTheWindow);
        return journal;
    }

    [Fact]
    public void ListBoxOf50Items_DrawJournal_AtRestAndOnHover()
        => FrozenJournal.Assert("list box draw journal", 5876, "BC458FAB8AE994E8", ListDrawJournal(CreateListBoxScene()));

    [Fact]
    public void ListViewOf50Items_DrawJournal_AtRestAndOnHover()
        => FrozenJournal.Assert("list view draw journal", 4430, "AD29804048F6D65C", ListDrawJournal(CreateListViewScene()));

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
        => FrozenJournal.Assert("tree view draw journal", 4280, "7593866D83B716B1", ListDrawJournal(CreateTreeViewScene()));

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
        => FrozenJournal.Assert("tab control draw journal", 1100, "4A61ACA0BF83E614", ListDrawJournal(CreateTabControlScene()));

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

    // ---- MGContextMenu and MGMenuBar ----

    private static MGContextMenu CreateMenu(MGWindow window, int itemCount)
    {
        MGContextMenu menu = new(window, "");
        for (int i = 0; i < itemCount; i++)
        {
            menu.AddButton($"Action {i}", _ => { });
        }

        return menu;
    }

    private static ListScene CreateContextMenuScene(bool checkHover = false)
    {
        ControlBench bench = new();
        bench.Show(new MGTextBlock(bench.Window, "under the menu"));
        MGContextMenu menu = CreateMenu(bench.Window, 6);
        Assert.True(bench.Desktop.TryOpenContextMenu(menu, new Point(60, 60)));
        bench.Frames(3);
        Assert.True(menu.IsContextMenuOpen);
        ListScene scene = new(bench, menu.Items[1].ActualLayoutBounds.Center, menu.Items[2].ActualLayoutBounds.Center);
        Assert.NotEqual(scene.First, scene.Second);
        if (checkHover)
        {
            //  Hovering an item then leaving the menu closes it: the states that measure the mouse outside do not hover first.
            bench.AssertHovers(menu, menu.Items[1], scene.First);
            bench.AssertHovers(menu, menu.Items[2], scene.Second);
            Assert.True(menu.IsContextMenuOpen);
        }

        return scene;
    }

    private static ListScene CreateMenuBarScene()
    {
        ControlBench bench = new();
        MGMenuBar menuBar = new(bench.Window) { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        MGMenuBarItem file = menuBar.AddItem("File", item => item.Submenu = CreateMenu(bench.Window, 4));
        MGMenuBarItem edit = menuBar.AddItem("Edit", item => item.Submenu = CreateMenu(bench.Window, 4));
        menuBar.AddItem("View", item => item.Submenu = CreateMenu(bench.Window, 4));
        bench.Show(menuBar);
        ListScene scene = new(bench, file.ActualLayoutBounds.Center, edit.ActualLayoutBounds.Center);
        Assert.NotEqual(scene.First, scene.Second);
        bench.AssertHovers(bench.Window, file, scene.First);
        bench.AssertHovers(bench.Window, edit, scene.Second);
        bench.Mouse = ControlBench.OutsideTheWindow;
        bench.Frames(2);
        Assert.False(menuBar.IsMenuActive);
        return scene;
    }

    private static ListScene CreateMenuBarDropdownScene()
    {
        ControlBench bench = new();
        MGMenuBar menuBar = new(bench.Window) { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        MGMenuBarItem file = menuBar.AddItem("File", item => item.Submenu = CreateMenu(bench.Window, 4));
        MGMenuBarItem edit = menuBar.AddItem("Edit", item => item.Submenu = CreateMenu(bench.Window, 4));
        menuBar.AddItem("View", item => item.Submenu = CreateMenu(bench.Window, 4));
        bench.Show(menuBar);

        //  The dropdown of the first item open (clicked), mouse over two of its entries.
        bench.Mouse = file.ActualLayoutBounds.Center;
        bench.Left = Microsoft.Xna.Framework.Input.ButtonState.Pressed;
        bench.Frame();
        bench.Left = Microsoft.Xna.Framework.Input.ButtonState.Released;
        bench.Frames(3);
        Assert.True(menuBar.IsMenuActive);
        MGContextMenu dropdown = file.Submenu;
        Assert.True(dropdown.IsContextMenuOpen);
        ListScene scene = new(bench, dropdown.Items[1].ActualLayoutBounds.Center, dropdown.Items[2].ActualLayoutBounds.Center, dropdown.Items[1].ActualLayoutBounds.Center);
        Assert.NotEqual(scene.First, scene.Second);
        bench.AssertHovers(dropdown, dropdown.Items[1], scene.First);
        bench.AssertHovers(dropdown, dropdown.Items[2], scene.Second);
        Assert.True(menuBar.IsMenuActive);
        return scene;
    }

    //  Context menu open, the mouse outside it (the menu stays open).
    [Fact]
    public void ContextMenuOpen_Update_WithTheMouseOutside_AllocatesNothing() => Assert.Equal(0, MeasureList("MGContextMenu", CreateContextMenuScene(), false).UpdateTotal);

    [Fact]
    public void ContextMenuOpen_Draw_WithTheMouseOutside_AllocatesNothing() => Assert.Equal(0, MeasureList("MGContextMenu", CreateContextMenuScene(), false).DrawTotal);

    [Fact]
    public void ContextMenuOpen_Update_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGContextMenu", CreateContextMenuScene(true), true).UpdateTotal);

    [Fact]
    public void ContextMenuOpen_Draw_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGContextMenu", CreateContextMenuScene(true), true).DrawTotal);

    [Fact]
    public void MenuBarClosed_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGMenuBar", CreateMenuBarScene(), false).UpdateTotal);

    [Fact]
    public void MenuBarClosed_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureList("MGMenuBar", CreateMenuBarScene(), false).DrawTotal);

    [Fact]
    public void MenuBarClosed_Update_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGMenuBar", CreateMenuBarScene(), true).UpdateTotal);

    [Fact]
    public void MenuBarClosed_Draw_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGMenuBar", CreateMenuBarScene(), true).DrawTotal);

    [Fact]
    public void MenuBarDropdownOpen_Update_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGMenuBar dropdown open", CreateMenuBarDropdownScene(), true).UpdateTotal);

    [Fact]
    public void MenuBarDropdownOpen_Draw_OnHover_AllocatesNothing() => Assert.Equal(0, MeasureList("MGMenuBar dropdown open", CreateMenuBarDropdownScene(), true).DrawTotal);

    [Fact]
    public void ContextMenuOpen_DrawJournal_OutsideAndOnHover()
        => FrozenJournal.Assert("context menu draw journal", 1440, "9266ECA7AE7728A6", ListDrawJournal(CreateContextMenuScene()));

    [Fact]
    public void MenuBarClosed_DrawJournal_AtRestAndOnHover()
        => FrozenJournal.Assert("menu bar draw journal", 1111, "41CFD489AC31D6E0", ListDrawJournal(CreateMenuBarScene()));

    [Fact]
    public void MenuBarDropdownOpen_DrawJournal_OnHover()
        => FrozenJournal.Assert("menu bar dropdown draw journal", 1983, "CDFDC93B0E0E8ED2", ListDrawJournal(CreateMenuBarDropdownScene()));

    // ---- The kept arguments and lists that the menu fixes rely on ----

    /// <summary>The visual tree children of a single content host follow its content: the list that was returned before the content changed is not changed by it.</summary>
    [Fact]
    public void SingleContentHost_VisualTreeChildren_FollowTheContent()
    {
        ControlBench bench = new();
        MGButton button = new(bench.Window);
        Assert.Empty(button.GetVisualTreeChildren(true, true));
        Assert.Empty(button.GetVisualTreeChildren(false, true));

        MGTextBlock first = new(bench.Window, "first");
        button.SetContent(first);
        IReadOnlyList<MGElement> withFirst = button.GetVisualTreeChildren(true, true);
        Assert.Same(first, Assert.Single(withFirst));
        Assert.Same(withFirst, button.GetVisualTreeChildren(true, true));
        Assert.Same(first, Assert.Single(button.GetVisualTreeChildren(false, true)));
        Assert.Empty(button.GetVisualTreeChildren(true, false));

        MGTextBlock second = new(bench.Window, "second");
        button.SetContent(second);
        Assert.Same(second, Assert.Single(button.GetVisualTreeChildren(true, true)));
        Assert.Same(first, Assert.Single(withFirst));
    }

    /// <summary>The arguments of VisualStateChanged hold the two states of the change, whatever instance carries them.</summary>
    [Fact]
    public void VisualStateChanged_ArgumentsHoldTheTwoStates()
    {
        ControlBench bench = new();
        MGButton button = new(bench.Window);
        button.SetContent("button");
        bench.Show(button);
        List<(VisualState Previous, VisualState New)> changes = new();
        button.VisualStateChanged += (sender, e) =>
        {
            Assert.Same(button, sender);
            changes.Add((e.PreviousValue, e.NewValue));
        };

        Point inside = button.ActualLayoutBounds.Center;
        for (int i = 0; i < 3; i++)
        {
            bench.Mouse = inside;
            bench.Frames(2);
            bench.Mouse = ControlBench.OutsideTheWindow;
            bench.Frames(2);
        }

        Assert.Equal(6, changes.Count);
        for (int i = 0; i < changes.Count; i += 2)
        {
            Assert.Equal(SecondaryVisualState.None, changes[i].Previous.Secondary);
            Assert.Equal(SecondaryVisualState.Hovered, changes[i].New.Secondary);
            Assert.Equal(changes[i].New, changes[i + 1].Previous);
            Assert.Equal(SecondaryVisualState.None, changes[i + 1].New.Secondary);
        }
    }


    // ---- MGGrid 3x3 with a selected cell ----

    /// <summary>A 3x3 grid in cell selection mode, with the cell of the second row and second column selected by a real click and the mouse moved away.</summary>
    private sealed record GridScene(ControlBench Bench, MGGrid Grid, Point First, Point Second)
    {
        /// <summary>The click of the measurement: a press then a release on one cell, then on the other, one click every two frames.</summary>
        public Action<int> Click => frame =>
        {
            int step = frame & 3;
            Bench.Mouse = step < 2 ? Second : First;
            Bench.Left = (step & 1) == 0 ? ButtonState.Pressed : ButtonState.Released;
        };
    }

    private static GridScene CreateGridScene(GridSelectionMode mode = GridSelectionMode.Cell)
    {
        ControlBench bench = new();
        MGGrid grid = new(bench.Window) { SelectionMode = mode };
        for (int i = 0; i < 3; i++)
        {
            grid.AddRow(GridLength.CreateWeightedLength(1));
            grid.AddColumn(GridLength.CreateWeightedLength(1));
        }

        MGTextBlock[,] cells = new MGTextBlock[3, 3];
        for (int row = 0; row < 3; row++)
        {
            for (int column = 0; column < 3; column++)
            {
                cells[row, column] = new MGTextBlock(bench.Window, $"R{row}C{column}");
                Assert.True(grid.TryAddChild(row, column, cells[row, column]));
            }
        }

        bench.Show(grid);
        GridScene scene = new(bench, grid, cells[1, 1].ActualLayoutBounds.Center, cells[0, 0].ActualLayoutBounds.Center);

        bench.Mouse = scene.First;
        bench.Left = ButtonState.Pressed;
        bench.Frame();
        bench.Left = ButtonState.Released;
        bench.Frames(2);
        bench.Mouse = ControlBench.OutsideTheWindow;
        bench.Frames(2);
        if (mode == GridSelectionMode.Cell)
        {
            Assert.True(grid.HasSelection);
            Assert.Same(grid.Rows[1], grid.CurrentSelection.Value.Cell.Row);
        }

        return scene;
    }

    private FrameBytes MeasureGrid(bool click, GridSelectionMode mode = GridSelectionMode.Cell)
    {
        GridScene scene = CreateGridScene(mode);
        ControlBench bench = scene.Bench;
        if (click && mode == GridSelectionMode.Cell)
        {
            //  Warm the click path and check that the selection really moves.
            for (int frame = 0; frame < 8; frame++)
            {
                scene.Click(frame);
                bench.Frame();
                if (frame == 5)
                {
                    Assert.Same(scene.Grid.Rows[0], scene.Grid.CurrentSelection.Value.Cell.Row);
                }
                else if (frame == 7)
                {
                    Assert.Same(scene.Grid.Rows[1], scene.Grid.CurrentSelection.Value.Cell.Row);
                }
            }
        }

        long callsBefore = bench.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(click ? scene.Click : null, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, "the measured frames drew nothing");
        Assert.True(scene.Grid.HasSelection);
        if (click)
        {
            Assert.NotNull(bench.Desktop.InputTracker.Mouse.RecentButtonClickedEvents[MGUI.Shared.Input.Mouse.MouseButton.Left]);
        }

        bench.Left = ButtonState.Released;
        Output.WriteLine($"MGGrid,{(mode == GridSelectionMode.Cell ? "a cell selected" : "no selection mode")}{(click ? ", selection moved by a click every 2 frames" : ", at rest")}: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    [Fact]
    public void GridWithASelection_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureGrid(false).UpdateTotal);

    [Fact]
    public void GridWithASelection_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureGrid(false).DrawTotal);
    /// <summary>A click is a press and a release: the mouse tracker of MGUI.Shared refills the press, release, click, drag start and drag end instances of the button and its click sequence
    /// (ADR-0034), so the update of the clicked grid allocates nothing, whether the click moves a selection or not (the same clicks on a grid without selection mode, the same handlers). The
    /// clicks are real: <see cref="MeasureGrid"/> checks that the selection moves, and that the tracker holds the latest click.</summary>
    [Fact]
    public void GridWithASelection_Update_WithTheSelectionMovedByClicks_AllocatesNothing()
    {
        Assert.Equal(0, MeasureGrid(true).UpdateTotal);
        Assert.Equal(0, MeasureGrid(true, GridSelectionMode.None).UpdateTotal);
    }

    [Fact]
    public void GridWithASelection_Draw_WithTheSelectionMovedByClicks_AllocatesNothing() => Assert.Equal(0, MeasureGrid(true).DrawTotal);

    /// <summary>The draw journal of the grid: two frames at rest with the cell selected, then 16 frames of the click pattern (the selection moves between the two cells every
    /// 4 frames, with the press and the release on their own frames), then two frames at rest. Frozen on the code before the allocation-free work, kept after it.</summary>
    [Fact]
    public void GridWithASelection_DrawJournal_AtRestAndAcrossSelectionMoves()
    {
        GridScene scene = CreateGridScene();
        ControlBench bench = scene.Bench;
        OrderedRecordingDrawTransaction transaction = new(bench.Runtime, DrawSettings.Default);
        List<string> journal = new();
        void Record(string label)
        {
            transaction.ClearJournal();
            bench.Update();
            bench.Desktop.Draw(transaction);
            journal.Add($"== {label} selection={(scene.Grid.CurrentSelection.HasValue ? scene.Grid.CurrentSelection.Value.Cell.Row.Top + "," + scene.Grid.CurrentSelection.Value.Cell.Column.Left : "none")}");
            journal.AddRange(transaction.Lines);
        }

        Record("rest, selected");
        Record("rest, selected, again");
        for (int frame = 0; frame < 16; frame++)
        {
            scene.Click(frame);
            Record($"click frame {frame}");
        }

        bench.Left = ButtonState.Released;
        bench.Mouse = ControlBench.OutsideTheWindow;
        Record("rest after the clicks");
        Record("rest after the clicks, again");
        FrozenJournal.Assert("grid draw journal", 1500, "1517D35FB852D0F5", journal);
    }

    /// <summary>The allocation-free cells of a selection are the cells that a foreach over the selection gives, in the same order, for each mode, and none for a cell that is not in the grid.</summary>
    [Fact]
    public void GridSelection_Cells_AreTheCellsOfTheEnumeration()
    {
        ControlBench bench = new();
        MGGrid grid = new(bench.Window);
        MGGrid other = new(bench.Window);
        for (int i = 0; i < 3; i++)
        {
            grid.AddRow(GridLength.CreateWeightedLength(1));
            grid.AddColumn(GridLength.CreateWeightedLength(1));
            other.AddRow(GridLength.CreateWeightedLength(1));
            other.AddColumn(GridLength.CreateWeightedLength(1));
        }

        GridCell inside = new(grid.Rows[1], grid.Columns[2]);
        GridCell outside = new(other.Rows[1], grid.Columns[2]);
        foreach (GridSelectionMode mode in new[] { GridSelectionMode.None, GridSelectionMode.Row, GridSelectionMode.Column, GridSelectionMode.Cell })
        {
            foreach (GridCell cell in new[] { inside, outside })
            {
                GridSelection selection = new(grid, cell, mode);
                List<GridCell> expected = new();
                foreach (GridCell expectedCell in (IEnumerable<GridCell>)selection)
                {
                    expected.Add(expectedCell);
                }

                List<GridCell> actual = new();
                foreach (GridCell actualCell in selection.Cells)
                {
                    actual.Add(actualCell);
                }

                Assert.Equal(expected, actual);
                //  The row of the outside cell belongs to another grid, its column to this one.
                int expectedCount = mode switch
                {
                    GridSelectionMode.Row => cell == inside ? 3 : 0,
                    GridSelectionMode.Column => 3,
                    GridSelectionMode.Cell => cell == inside ? 1 : 0,
                    _ => 0
                };
                Assert.Equal(expectedCount, actual.Count);
            }
        }
    }

    // ---- MGDockHost ----

    /// <summary>Two tab groups separated by a splitter: A in the first, B in the second; C is created but only docked, as a second tab of B, when panelCInGroupB is true.</summary>
    private sealed class DockScene
    {
        public ControlBench Bench { get; } = new();
        public MGDockHost Host { get; private set; }
        public DockPanelNode PanelA { get; private set; }
        public DockPanelNode PanelC { get; private set; }
        public DockTabGroupNode GroupA { get; private set; }
        public DockTabGroupNode GroupB { get; private set; }

        public static DockScene Create(bool panelCInGroupB)
        {
            DockScene scene = new();
            MGWindow window = scene.Bench.Window;
            scene.PanelA = new DockPanelNode { Title = "A", ContentFactory = () => new MGTextBlock(window, "Panel A content") };
            DockPanelNode panelB = new() { Title = "B", ContentFactory = () => new MGTextBlock(window, "Panel B content") };
            scene.PanelC = new DockPanelNode { Title = "C", ContentFactory = () => new MGTextBlock(window, "Panel C content") };
            scene.GroupA = new DockTabGroupNode();
            scene.GroupA.AddPanel(scene.PanelA, -1);
            scene.GroupB = new DockTabGroupNode();
            scene.GroupB.AddPanel(panelB, -1);
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

    private static DockScene CreateTwoGroupsScene()
    {
        DockScene scene = DockScene.Create(false);
        Assert.Equal(2, scene.Host.GetAllVisibleTabGroups().Count());
        return scene;
    }

    private static DockScene CreateAutoHiddenScene()
    {
        //  "Hidden" is read as auto-hidden: the panel leaves the layout tree for the auto-hide strip (UnpinPanel), the drawer stays closed.
        DockScene scene = DockScene.Create(true);
        scene.Host.UnpinPanel(scene.PanelC);
        scene.Bench.Frames(3);
        Assert.False(scene.PanelC.IsPinned);
        Assert.Contains(scene.PanelC, scene.Host.LayoutModel.GetAllAutoHidePanels());
        Assert.Equal(2, scene.Host.GetAllVisibleTabGroups().Count());
        return scene;
    }

    private static DockScene CreateTabBehindTheActiveTabScene()
    {
        DockScene scene = DockScene.Create(true);
        Assert.Equal(2, scene.GroupB.Panels.Count);
        return scene;
    }

    /// <summary>The drag of the measurement: a press on the tab of A, the mouse moved to the group B (the drag starts, then the drop preview shows), and then held down.</summary>
    private sealed class DockDrag
    {
        public DockScene Scene { get; } = DockScene.Create(false);
        public Point PressPoint { get; }
        public Point Midway { get; }
        public Point First { get; }
        public Point Second { get; }

        public DockDrag()
        {
            MGDockTabItem tab = Scene.VisualGroupOf(Scene.GroupA).TraverseVisualTree<MGDockTabItem>(IncludeSelf: false).First();
            PressPoint = tab.LayoutBounds.Center;
            Point groupCenter = Scene.VisualGroupOf(Scene.GroupB).LayoutBounds.Center;
            Midway = new((PressPoint.X + groupCenter.X) / 2, (PressPoint.Y + groupCenter.Y) / 2);
            First = groupCenter;
            Second = new(groupCenter.X + 8, groupCenter.Y + 6);
        }

        public Action<int> Alternate => frame => Scene.Bench.Mouse = (frame & 1) == 0 ? First : Second;

        /// <summary>Presses, moves, and reaches the group B: the drop preview shows. Checks the drag and its drop target.</summary>
        public void Start()
        {
            ControlBench bench = Scene.Bench;
            MGDockHost host = Scene.Host;
            bench.Mouse = PressPoint;
            bench.Frames(2);
            bench.Left = ButtonState.Pressed;
            bench.Frame();
            bench.Mouse = Midway;
            bench.Frame();
            bench.Mouse = First;
            bench.Frames(3);
            Assert.NotNull(host.CurrentDrag);
            Assert.True(host.CurrentDrag.HasExceededThreshold);
            Assert.NotNull(host.CurrentDropTarget);
            DockZone zone = host.CurrentDropTarget.Zone;
            bench.Mouse = Second;
            bench.Frames(3);
            Assert.NotNull(host.CurrentDropTarget);
            Assert.Equal(zone, host.CurrentDropTarget.Zone);
            bench.Mouse = First;
            bench.Frames(3);
        }
    }

    private FrameBytes MeasureDock(string label, DockScene scene, Action<int> prepare = null)
    {
        ControlBench bench = scene.Bench;
        long callsBefore = bench.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(prepare, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, "the measured frames drew nothing");
        Output.WriteLine($"MGDockHost {label}: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    private FrameBytes MeasureDockDrag()
    {
        DockDrag drag = new();
        drag.Start();
        FrameBytes bytes = MeasureDock("tab drag, drop preview shown, mouse alternating between two positions", drag.Scene, drag.Alternate);
        Assert.NotNull(drag.Scene.Host.CurrentDrag);
        Assert.NotNull(drag.Scene.Host.CurrentDropTarget);
        drag.Scene.Bench.Left = ButtonState.Released;
        drag.Scene.Bench.Frames(2);
        return bytes;
    }

    [Fact]
    public void DockHostWithTwoTabGroups_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureDock("two tab groups, at rest", CreateTwoGroupsScene()).UpdateTotal);

    [Fact]
    public void DockHostWithTwoTabGroups_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureDock("two tab groups, at rest", CreateTwoGroupsScene()).DrawTotal);

    [Fact]
    public void DockHostWithAPanelAutoHidden_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureDock("one panel auto-hidden, at rest", CreateAutoHiddenScene()).UpdateTotal);

    [Fact]
    public void DockHostWithAPanelAutoHidden_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureDock("one panel auto-hidden, at rest", CreateAutoHiddenScene()).DrawTotal);

    [Fact]
    public void DockHostWithATabBehindTheActiveTab_Update_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureDock("one tab behind the active tab, at rest", CreateTabBehindTheActiveTabScene()).UpdateTotal);

    [Fact]
    public void DockHostWithATabBehindTheActiveTab_Draw_AtRest_AllocatesNothing() => Assert.Equal(0, MeasureDock("one tab behind the active tab, at rest", CreateTabBehindTheActiveTabScene()).DrawTotal);

    /// <summary>The update of a frame of the drag allocates the one <see cref="DockDropTarget"/> that the drop target computation makes for the position of the mouse, and nothing else:
    /// <c>MGDockHost.CurrentDropTarget</c> is a public property of a public mutable class that notifies when its reference changes, so the object is the result of the computation and is
    /// kept as it is (reusing one would stop the notification at each frame). Every other part of the computation (the walks of the visual tree, the forbidden zones, the docking rules,
    /// the zone search) allocates nothing.</summary>
    [Fact]
    public void DockHostDuringATabDrag_Update_AllocatesOnlyTheDropTarget()
    {
        GC.KeepAlive(new DockDropTarget());
        AllocationWindow.Start();
        long before = GC.GetAllocatedBytesForCurrentThread();
        DockDropTarget target = new();
        long dropTargetBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(target);
        Assert.True(dropTargetBytes > 0);

        //  Every measured frame moves the mouse by 10 pixels (between the two positions), more than the 5 that make the host compute the target again.
        FrameBytes bytes = MeasureDockDrag();
        Assert.Equal(dropTargetBytes * bytes.Iterations, bytes.UpdateTotal);
    }

    [Fact]
    public void DockHostDuringATabDrag_Draw_AllocatesNothing() => Assert.Equal(0, MeasureDockDrag().DrawTotal);

    private static List<string> DockDrawJournal(DockScene scene, Action<Action<string>> script)
    {
        ControlBench bench = scene.Bench;
        OrderedRecordingDrawTransaction transaction = new(bench.Runtime, DrawSettings.Default);
        List<string> journal = new();
        void Record(string label)
        {
            transaction.ClearJournal();
            bench.Update();
            bench.Desktop.Draw(transaction);
            journal.Add($"== {label}");
            journal.AddRange(transaction.Lines);
        }

        script(Record);
        return journal;
    }

    private static List<string> DockRestJournal(DockScene scene)
        => DockDrawJournal(scene, record =>
        {
            for (int frame = 0; frame < 4; frame++)
            {
                record($"rest {frame}");
            }
        });

    [Fact]
    public void DockHostWithTwoTabGroups_DrawJournal_AtRest()
        => FrozenJournal.Assert("dock two groups draw journal", 632, "D83BEE98675FFA9B", DockRestJournal(CreateTwoGroupsScene()));

    [Fact]
    public void DockHostWithAPanelAutoHidden_DrawJournal_AtRest()
        => FrozenJournal.Assert("dock auto-hidden draw journal", 688, "5C0A025A62A967D5", DockRestJournal(CreateAutoHiddenScene()));

    [Fact]
    public void DockHostWithATabBehindTheActiveTab_DrawJournal_AtRest()
        => FrozenJournal.Assert("dock tab behind draw journal", 672, "33C33BE1B0090BDF", DockRestJournal(CreateTabBehindTheActiveTabScene()));

    /// <summary>The draw journal of a tab drag: two frames before it, the press, the first moves, six frames of the mouse alternating between the two positions with the drop
    /// preview shown, then the release (the drop) and two frames after it.</summary>
    [Fact]
    public void DockHostDuringATabDrag_DrawJournal_BeforeDuringAndAfterTheDrop()
    {
        DockDrag drag = new();
        ControlBench bench = drag.Scene.Bench;
        List<string> journal = DockDrawJournal(drag.Scene, record =>
        {
            record("before the drag 0");
            record("before the drag 1");
            bench.Mouse = drag.PressPoint;
            record("mouse on the tab");
            bench.Left = ButtonState.Pressed;
            record("pressed");
            bench.Mouse = drag.Midway;
            record("moved midway");
            bench.Mouse = drag.First;
            record("on the group 0");
            record("on the group 1");
            for (int frame = 0; frame < 6; frame++)
            {
                drag.Alternate(frame);
                record($"alternating {frame}");
            }

            bench.Left = ButtonState.Released;
            record("released");
            record("after the drop 0");
            record("after the drop 1");
        });
        FrozenJournal.Assert("dock drag draw journal", 3100, "BA04E47B44FF69B8", journal);
    }

    /// <summary>AddChildrenTo gives, for every element of the dock scenes, the children that GetChildren yields, in the same order.</summary>
    [Fact]
    public void AddChildrenTo_GivesTheChildrenOfGetChildren_ForTheElementsOfTheDockScenes()
    {
        foreach (DockScene scene in new[] { CreateTwoGroupsScene(), CreateAutoHiddenScene(), CreateTabBehindTheActiveTabScene() })
        {
            int visited = 0;
            foreach (MGElement element in scene.Bench.Window.TraverseVisualTree(true, true, true, true, MGElement.TreeTraversalMode.Preorder))
            {
                List<MGElement> expected = new(element.GetChildren());
                List<MGElement> actual = new();
                element.AddChildrenTo(actual);
                Assert.Equal(expected, actual);
                visited++;
            }

            Assert.True(visited > 20, "the walk visited too few elements");
        }
    }

    /// <summary>The visible tab groups and split containers that the drag collects are the ones that the public enumeration gives, in the same order, whatever the host contains.</summary>
    [Fact]
    public void DockHost_CollectedTreeWalks_AreTheEnumerations()
    {
        foreach (DockScene scene in new[] { CreateTwoGroupsScene(), CreateAutoHiddenScene(), CreateTabBehindTheActiveTabScene() })
        {
            List<MGDockTabGroup> expectedGroups = scene.Host.GetAllVisibleTabGroups().ToList();
            Assert.Equal(2, expectedGroups.Count);
            Assert.Equal(expectedGroups, scene.Host.CollectVisibleTabGroups());
            Assert.Single(scene.Host.CollectSplitContainers());
        }
    }

    // ---- MGTextBox with the keyboard focus ----

    /// <summary>A text box of a window, focused by a real click, empty, with the caret placed. The caret blinks on the wall clock (<c>MGTextCaret.SecondsShown</c> reads DateTime.Now): the default blink
    /// is measured as it is, and a steady caret (a blink rate of zero) is measured for the frames where the caret is always drawn.</summary>
    private sealed record TextBoxScene(ControlBench Bench, MGTextBox TextBox)
    {
        private static readonly KeyboardState KeyA = new(Keys.A);
        private static readonly KeyboardState KeyBack = new(Keys.Back);

        /// <summary>One key per frame: A then Backspace, so that the text alternates between "a" and "" and stays bounded. Each frame presses a key the previous frame did not.</summary>
        public Action<int> Type => frame => Bench.Keyboard = (frame & 1) == 0 ? KeyA : KeyBack;
    }

    private static TextBoxScene CreateTextBoxScene(bool steadyCaret)
    {
        ControlBench bench = new();
        MGTextBox textBox = new(bench.Window) { PreferredWidth = 200 };
        bench.Show(textBox);
        Point inside = textBox.LayoutBounds.Center;
        bench.Mouse = inside;
        bench.Left = ButtonState.Pressed;
        bench.Frame();
        bench.Left = ButtonState.Released;
        bench.Frames(2);
        Assert.Same(textBox, bench.Desktop.FocusedKeyboardHandler);
        Assert.True(textBox.Caret.HasPosition);
        if (steadyCaret)
        {
            textBox.Caret.BlinkRate = TimeSpan.Zero;
            bench.Frame();
            Assert.True(textBox.Caret.IsCurrentlyVisible);
        }

        return new TextBoxScene(bench, textBox);
    }

    private FrameBytes MeasureTextBox(bool steadyCaret, bool typing)
    {
        TextBoxScene scene = CreateTextBoxScene(steadyCaret);
        ControlBench bench = scene.Bench;
        if (typing)
        {
            scene.Type(0);
            bench.Frame();
            Assert.Equal("a", scene.TextBox.Text);
            scene.Type(1);
            bench.Frame();
            Assert.Equal("", scene.TextBox.Text);
        }

        long callsBefore = bench.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(typing ? scene.Type : null, bench.Update, bench.Draw);
        Assert.True(bench.Transaction.CallCount > callsBefore, "the measured frames drew nothing");
        Assert.Same(scene.TextBox, bench.Desktop.FocusedKeyboardHandler);
        bench.Keyboard = default;
        Output.WriteLine($"MGTextBox focused, {(typing ? "one key per frame" : "at rest")}, {(steadyCaret ? "steady caret" : "default caret blink")}: Update {bytes.Update} B/frame, Draw {bytes.Draw} B/frame (totals {bytes.UpdateTotal} and {bytes.DrawTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    [Fact]
    public void TextBoxWithTheFocus_Update_AtRest_WithTheDefaultBlink_AllocatesNothing() => Assert.Equal(0, MeasureTextBox(false, false).UpdateTotal);

    [Fact]
    public void TextBoxWithTheFocus_Draw_AtRest_WithTheDefaultBlink_AllocatesNothing() => Assert.Equal(0, MeasureTextBox(false, false).DrawTotal);

    [Fact]
    public void TextBoxWithTheFocus_Update_AtRest_WithASteadyCaret_AllocatesNothing() => Assert.Equal(0, MeasureTextBox(true, false).UpdateTotal);

    [Fact]
    public void TextBoxWithTheFocus_Draw_AtRest_WithASteadyCaret_AllocatesNothing() => Assert.Equal(0, MeasureTextBox(true, false).DrawTotal);

    [Fact]
    public void TextBoxWithTheFocus_Draw_WhileTyping_WithTheDefaultBlink_AllocatesNothing() => Assert.Equal(0, MeasureTextBox(false, true).DrawTotal);

    [Fact]
    public void TextBoxWithTheFocus_Draw_WhileTyping_WithASteadyCaret_AllocatesNothing() => Assert.Equal(0, MeasureTextBox(true, true).DrawTotal);

    /// <summary>The draw journal of a focused text box with a steady caret: three frames at rest, then ten frames of the keys (A, Backspace, A, ...), then three frames at rest.
    /// Frozen on the code before the allocation-free work, kept after it.</summary>
    [Fact]
    public void TextBoxWithTheFocus_DrawJournal_AtRestAndWhileTyping()
    {
        TextBoxScene scene = CreateTextBoxScene(true);
        ControlBench bench = scene.Bench;
        OrderedRecordingDrawTransaction transaction = new(bench.Runtime, DrawSettings.Default);
        List<string> journal = new();
        void Record(string label)
        {
            transaction.ClearJournal();
            bench.Update();
            bench.Desktop.Draw(transaction);
            journal.Add($"== {label} text=\"{scene.TextBox.Text}\" caret={scene.TextBox.Caret.HasPosition}");
            journal.AddRange(transaction.Lines);
        }

        for (int frame = 0; frame < 3; frame++)
        {
            Record($"rest {frame}");
        }

        for (int frame = 0; frame < 10; frame++)
        {
            scene.Type(frame);
            Record($"key {frame}");
        }

        bench.Keyboard = default;
        for (int frame = 0; frame < 3; frame++)
        {
            Record($"rest after the keys {frame}");
        }

        FrozenJournal.Assert("text box draw journal", 936, "A006940EB631CE11", journal);
    }

    /// <summary>The update of a frame in which a key is pressed in a focused text box that ignores it (F1, then F2, one per frame): the keyboard input of MGUI.Shared refills the press, release, click and
    /// stream instances of the key (ADR-0034), the text box ignores the key, so nothing is allocated. The keys are real: the tracker holds F1 or F2 at the end.</summary>
    private FrameBytes MeasureTextBoxWithIgnoredKeys()
    {
        TextBoxScene scene = CreateTextBoxScene(true);
        ControlBench bench = scene.Bench;
        KeyboardState f1 = new(Keys.F1);
        KeyboardState f2 = new(Keys.F2);
        FrameBytes bytes = FrameAllocationMeter.Measure(frame => bench.Keyboard = (frame & 1) == 0 ? f1 : f2, bench.Update, bench.Draw);
        Assert.Same(scene.TextBox, bench.Desktop.FocusedKeyboardHandler);
        Assert.Equal("", scene.TextBox.Text);
        Assert.Equal(0, bytes.DrawTotal);
        Assert.True(bench.Desktop.InputTracker.Keyboard.IsPressed(Keys.F1) || bench.Desktop.InputTracker.Keyboard.IsPressed(Keys.F2), "the measured frames press no key");
        Output.WriteLine($"MGTextBox focused, one ignored key per frame (F1, F2): Update {bytes.Update} B/frame (total {bytes.UpdateTotal} B over {bytes.Iterations} frames)");
        return bytes;
    }

    [Fact]
    public void TextBoxWithTheFocus_Update_WithIgnoredKeys_AllocatesNothing() => Assert.Equal(0, MeasureTextBoxWithIgnoredKeys().UpdateTotal);

    /// <summary>What the update of a typing frame (the text alternates between "a" and "") allocates, over the 500 measured frames, all of it beyond the keyboard input of MGUI.Shared, which allocates
    /// nothing since ADR-0034. Frozen: the new text
    /// string and the objects that its change makes (the text changed notification, the layout of the text block, the geometry of the characters that the caret reads) are results of the change,
    /// and the lines of the text block that <c>TextRenderInfo</c> reads, public <c>MGTextLine</c> objects that <c>MGTextBlock.Lines</c> builds again with the legacy line parser at each
    /// change, are 1.6 KB of the 2.6 KB of each key. The legacy parser cannot be skipped without the layouts of the text block giving the original index of each character (the internal layouts do not
    /// compute it), and the objects are not reused because the caret keeps references to them. When the caret geometry no longer goes through <c>MGTextBlock.Lines</c>, this value drops: update it with the
    /// value that the failure message gives.</summary>
    private const long TypingBytes = 1300000;

    [Fact]
    public void TextBoxWithTheFocus_Update_WhileTyping_AllocatesOnlyTheNewTextLayout()
    {
        foreach (bool steadyCaret in new[] { false, true })
        {
            FrameBytes typing = MeasureTextBox(steadyCaret, true);
            Assert.True(typing.UpdateTotal == TypingBytes,
                $"typing allocates {typing.UpdateTotal} B over {typing.Iterations} frames, expected {TypingBytes}");
        }
    }
}
