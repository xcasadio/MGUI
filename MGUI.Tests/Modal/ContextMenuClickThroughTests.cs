namespace MGUI.Tests.Modal;

using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Tests.Graph;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

public class ContextMenuClickThroughTests
{
    [Fact]
    public void ClickingContextMenuItem_DoesNotFallThroughToElementUnderneath()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 800, 600)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        MGCanvas root = new(window);
        window.SetContent(root);
        desktop.Windows.Add(window);

        //  A big "card" that behaves like the content browser grid items: selection happens on LMB pressed
        MGBorder card = new(window, new Thickness(0), MGUniformBorderBrush.Black)
        {
            PreferredWidth = 400,
            PreferredHeight = 400,
            MinWidth = 400,
            MinHeight = 400,
        };
        using (root.AllowChangingContentTemporarily())
        {
            root.TryAddChild(card);
        }
        MGCanvas.SetLeft(card, 100);
        MGCanvas.SetTop(card, 100);

        int cardPressedCount = 0;
        int cardReleasedCount = 0;
        card.MouseHandler.LMBPressedInside += (_, _) => cardPressedCount++;
        card.MouseHandler.LMBReleasedInside += (_, _) => cardReleasedCount++;

        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        int menuActionCount = 0;
        MGContextMenu menu = new(window, "");
        menu.AddButton("Item A", _ => menuActionCount++);

        //  Open the menu overtop of the card
        Assert.True(desktop.TryOpenContextMenu(menu, new Point(150, 150)));

        AdvanceFrame(runtime, desktop, 32, new Point(150, 150));
        AdvanceFrame(runtime, desktop, 48, new Point(150, 150));

        MGContextMenuItem item = menu.Items[0];
        Point itemCenter = item.ActualLayoutBounds.Center;
        Assert.True(card.ActualLayoutBounds.Contains(itemCenter), $"Test setup: the menu item ({item.ActualLayoutBounds}) must overlap the card ({card.ActualLayoutBounds}).");

        //  Move over the item, then click it
        AdvanceFrame(runtime, desktop, 64, itemCenter);
        AdvanceFrame(runtime, desktop, 80, itemCenter, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 96, itemCenter);

        Assert.Equal(1, menuActionCount);
        Assert.Equal(0, cardPressedCount);
        Assert.Equal(0, cardReleasedCount);
    }

    /// <summary>Reproduces the content-browser sequence: right-click a card to open a freshly built
    /// context menu anchored at the cursor, then left-click one of its items.</summary>
    [Fact]
    public void RightClickOpenedContextMenu_ItemClick_DoesNotFallThroughToCardUnderneath()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 800, 600)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        MGScrollViewer scrollViewer = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        MGStackPanel itemsPanel = new(window, Orientation.Vertical);
        scrollViewer.SetContent(itemsPanel);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        int backgroundPressedCount = 0;
        scrollViewer.MouseHandler.LMBPressedInside += (_, _) => backgroundPressedCount++;

        int cardPressedCount = 0;
        int menuActionCount = 0;
        MGContextMenu openedMenu = null;

        MGBorder[] cards = new MGBorder[3];
        for (int i = 0; i < cards.Length; i++)
        {
            MGBorder card = new(window, new Thickness(0), MGUniformBorderBrush.Black)
            {
                PreferredWidth = 400,
                PreferredHeight = 150,
                MinWidth = 400,
                MinHeight = 150,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
            };
            cards[i] = card;
            itemsPanel.TryAddChild(card);

            card.MouseHandler.LMBPressedInside += (_, _) => cardPressedCount++;
            card.MouseHandler.RMBReleasedInside += (_, e) =>
            {
                //  Same shape as ContentBrowserPanel.OnAssetListRightClick: build a brand new menu each time
                MGContextMenu menu = new(window, null);
                menu.AddButton("Open", _ => menuActionCount++);
                menu.AddButton("Rename", _ => menuActionCount++);
                menu.AddButton("Delete", _ => menuActionCount++);
                openedMenu = menu;
                desktop.TryOpenContextMenu(menu, e.Position);
            };
        }

        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Point cardCenter = cards[0].ActualLayoutBounds.Center;

        //  Right-click the first card to open the menu
        AdvanceFrame(runtime, desktop, 32, cardCenter);
        AdvanceFrame(runtime, desktop, 48, cardCenter, MouseButton.Right);
        AdvanceFrame(runtime, desktop, 64, cardCenter);

        Assert.NotNull(openedMenu);
        Assert.Same(openedMenu, desktop.ActiveContextMenu);

        AdvanceFrame(runtime, desktop, 80, cardCenter);

        Point itemCenter = openedMenu.Items[1].ActualLayoutBounds.Center;
        Assert.True(itemCenter.Y > 0, "Menu item must have been laid out.");

        cardPressedCount = 0;
        backgroundPressedCount = 0;

        //  Move onto the second menu item, then left-click it
        AdvanceFrame(runtime, desktop, 96, itemCenter);
        AdvanceFrame(runtime, desktop, 112, itemCenter, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 128, itemCenter);

        Assert.Equal(1, menuActionCount);
        Assert.Equal(0, cardPressedCount);
        Assert.Equal(0, backgroundPressedCount);
    }

    /// <summary>An <see cref="MGGrid"/> with an active <see cref="GridSelectionMode"/> must not change its
    /// selection when the click was already consumed by something outside of the grid, such as an
    /// <see cref="MGContextMenu"/> drawn overtop of it.</summary>
    [Fact]
    public void ContextMenuItemClick_DoesNotChangeGridSelectionUnderneath()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 800, 600)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        MGGrid grid = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            SelectionMode = GridSelectionMode.Row,
        };
        grid.AddColumn(GridLength.CreateWeightedLength(1));
        for (int i = 0; i < 6; i++)
        {
            grid.AddRow(GridLength.CreatePixelLength(80));
            MGTextBlock cell = new(window, $"Row {i}");
            grid.TryAddChild(i, 0, cell);
        }
        window.SetContent(grid);
        desktop.Windows.Add(window);

        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        //  Select row 0 the normal way
        Point row0 = new(40, 40);
        AdvanceFrame(runtime, desktop, 32, row0);
        AdvanceFrame(runtime, desktop, 48, row0, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, row0);

        Assert.True(grid.HasSelection);
        Assert.Equal(0, RowIndexOf(grid, grid.CurrentSelection!.Value.Cell.Row));

        int menuActionCount = 0;
        MGContextMenu menu = new(window, "");
        menu.AddButton("Item A", _ => menuActionCount++);
        menu.AddButton("Item B", _ => menuActionCount++);
        menu.AddButton("Item C", _ => menuActionCount++);
        Assert.True(desktop.TryOpenContextMenu(menu, row0));

        AdvanceFrame(runtime, desktop, 80, row0);
        AdvanceFrame(runtime, desktop, 96, row0);

        Point itemCenter = menu.Items[2].ActualLayoutBounds.Center;
        Assert.True(itemCenter.Y >= 80, $"Test setup: the menu item ({menu.Items[2].ActualLayoutBounds}) must sit over a different grid row than row 0.");

        AdvanceFrame(runtime, desktop, 112, itemCenter);
        AdvanceFrame(runtime, desktop, 128, itemCenter, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 144, itemCenter);

        Assert.Equal(1, menuActionCount);
        Assert.True(grid.HasSelection);
        Assert.Equal(0, RowIndexOf(grid, grid.CurrentSelection!.Value.Cell.Row));
    }

    /// <summary>Same defect seen through <see cref="MGListView{TItemType}"/>, which is what the editor's
    /// content browser "Details" view uses (its inner DataGrid is an <see cref="MGGrid"/> with row selection).</summary>
    [Fact]
    public void ContextMenuItemClick_DoesNotChangeListViewSelectionUnderneath()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 800, 600)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        MGListView<string> listView = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            RowHeight = 40,
            SelectionMode = GridSelectionMode.Row,
        };
        listView.AddColumn(new ListViewColumnWidth(300), new MGTextBlock(window, "Name"), item => new MGTextBlock(window, item));
        listView.SetItemsSource(new List<string> { "Row 0", "Row 1", "Row 2", "Row 3", "Row 4", "Row 5", "Row 6" });
        window.SetContent(listView);
        desktop.Windows.Add(window);

        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        MGElement firstRowCell = listView.DataGrid.GetCellContent(listView.DataGrid.Rows[0], listView.DataGrid.Columns[0]).First();
        Point firstRowCenter = firstRowCell.ActualLayoutBounds.Center;

        AdvanceFrame(runtime, desktop, 32, firstRowCenter);
        AdvanceFrame(runtime, desktop, 48, firstRowCenter, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, firstRowCenter);

        Assert.NotNull(listView.SelectedData);
        RowDefinition selectedRowBefore = listView.SelectedData!.Value.Cell.Row;
        Assert.Same(listView.DataGrid.Rows[0], selectedRowBefore);

        int menuActionCount = 0;
        MGContextMenu menu = new(window, "");
        menu.AddButton("Item A", _ => menuActionCount++);
        menu.AddButton("Item B", _ => menuActionCount++);
        menu.AddButton("Item C", _ => menuActionCount++);
        Assert.True(desktop.TryOpenContextMenu(menu, firstRowCenter));

        AdvanceFrame(runtime, desktop, 80, firstRowCenter);
        AdvanceFrame(runtime, desktop, 96, firstRowCenter);

        Point itemCenter = menu.Items[2].ActualLayoutBounds.Center;
        Assert.True(itemCenter.Y > firstRowCenter.Y + 40, "Test setup: the menu item must sit over a different list row.");

        AdvanceFrame(runtime, desktop, 112, itemCenter);
        AdvanceFrame(runtime, desktop, 128, itemCenter, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 144, itemCenter);

        Assert.Equal(1, menuActionCount);
        Assert.NotNull(listView.SelectedData);
        Assert.Same(selectedRowBefore, listView.SelectedData!.Value.Cell.Row);
    }

    /// <summary>Regression guard for <see cref="HandledInputPolicy.Descendant"/>.<para/>
    /// <see cref="MGGrid.CanDeselectByClickingSelectedCell"/> needs both the press <em>and</em> the release to reach
    /// the selection handler. An <see cref="MGButton"/> filling a cell consumes the release, so without
    /// <see cref="HandledInputPolicy.Descendant"/> the grid never sees it and re-clicking a selected row stops
    /// deselecting it.</summary>
    [Fact]
    public void ReClickingSelectedCellThroughItsButton_StillDeselects()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 800, 600)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        MGGrid grid = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            SelectionMode = GridSelectionMode.Row,
        };
        Assert.True(grid.CanDeselectByClickingSelectedCell);
        grid.AddColumn(GridLength.CreateWeightedLength(1));
        int buttonClickCount = 0;
        for (int i = 0; i < 3; i++)
        {
            grid.AddRow(GridLength.CreatePixelLength(80));
            MGButton cellButton = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            cellButton.AddCommandHandler((_, _) => buttonClickCount++);
            grid.TryAddChild(i, 0, cellButton);
        }
        window.SetContent(grid);
        desktop.Windows.Add(window);

        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Point row1 = new(40, 120);

        //  First click on the button: the row becomes selected
        AdvanceFrame(runtime, desktop, 32, row1);
        AdvanceFrame(runtime, desktop, 48, row1, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, row1);

        Assert.Equal(1, buttonClickCount);
        Assert.True(grid.HasSelection);
        Assert.Equal(1, RowIndexOf(grid, grid.CurrentSelection!.Value.Cell.Row));

        //  Second click on the same button: the already-selected row is deselected
        AdvanceFrame(runtime, desktop, 700, row1, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 716, row1);

        Assert.Equal(2, buttonClickCount);
        Assert.False(grid.HasSelection);
    }

    /// <summary>A click consumed by an element <em>inside</em> a cell must still move the grid selection.</summary>
    [Fact]
    public void ClickOnButtonInsideCell_StillUpdatesGridSelection()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 800, 600)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        MGGrid grid = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            SelectionMode = GridSelectionMode.Row,
        };
        grid.AddColumn(GridLength.CreateWeightedLength(1));
        int buttonClickCount = 0;
        for (int i = 0; i < 3; i++)
        {
            grid.AddRow(GridLength.CreatePixelLength(80));
            MGButton cellButton = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
            };
            cellButton.AddCommandHandler((_, _) => buttonClickCount++);
            grid.TryAddChild(i, 0, cellButton);
        }
        window.SetContent(grid);
        desktop.Windows.Add(window);

        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        Point row1 = new(40, 120);
        AdvanceFrame(runtime, desktop, 32, row1);
        AdvanceFrame(runtime, desktop, 48, row1, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, row1);

        Assert.Equal(1, buttonClickCount);
        Assert.True(grid.HasSelection);
        Assert.Equal(1, RowIndexOf(grid, grid.CurrentSelection!.Value.Cell.Row));
    }

    private static int RowIndexOf(MGGrid grid, RowDefinition row)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (ReferenceEquals(grid.Rows[i], row))
            {
                return i;
            }
        }

        return -1;
    }

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, Point position, MouseButton? pressedButton = null)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(position, pressedButton),
            new KeyboardState()));
        desktop.Update();
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton = null, int scrollWheel = 0)
        => new(
            position.X,
            position.Y,
            scrollWheel,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
}
