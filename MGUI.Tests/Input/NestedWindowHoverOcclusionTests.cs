using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// Z-order-aware mouse hit-test (MGElement.IsInside): a position covered by a window drawn above an element's window (nested/modal
/// windows, sibling nested windows above, higher desktop windows, active context menu) is not "inside" that element, so movement
/// events (MovedInside/Entered/Exited), IsHovered and press classification follow the visible surface. Reported in the FocusInputReview
/// sample: hovering the open <see cref="MGComboBox{TItemType}"/> dropdown (a nested window) still highlighted the
/// <see cref="MGListBox{TItemType}"/> items beneath it, because the listbox tracks its hovered item through its own MovedInside/Exited events.
/// </summary>
public class NestedWindowHoverOcclusionTests
{
    private static readonly List<string> ManyItems = Enumerable.Range(1, 12).Select(i => $"Item {i}").ToList();

    [Fact]
    public void OpenComboBoxDropdown_HoveringItemOverListBox_ListBoxGetsExitedAndNoMovedInside()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 500) { WindowStyle = WindowStyle.None };
        MGComboBox<string> comboBox = new(window) { PreferredHeight = 30 };
        comboBox.SetItemsSource(new List<string> { "All items", "Focusable controls", "Popup-backed controls", "Overlay blockers" });
        MGListBox<string> listBox = new(window);
        listBox.SetItemsSource(ManyItems);
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(comboBox);
        panel.TryAddChild(listBox);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        Frame(runtime, desktop, 1, new Point(1, 1));
        Frame(runtime, desktop, 2, new Point(1, 1));

        int movedInside = 0;
        int exited = 0;
        listBox.MouseHandler.MovedInside += (_, _) => movedInside++;
        listBox.MouseHandler.Exited += (_, _) => exited++;

        comboBox.IsDropdownOpen = true;
        Frame(runtime, desktop, 3, new Point(1, 1));
        Frame(runtime, desktop, 4, new Point(1, 1));
        MGWindow dropdown = Assert.Single(window.NestedWindows);
        Rectangle overlap = Rectangle.Intersect(dropdown.LayoutBounds, listBox.LayoutBounds);
        Assert.False(overlap.IsEmpty); // the dropdown really covers part of the listbox, as in the sample

        // Point A: on the listbox, below the dropdown (not covered). Point B: on the listbox, under the dropdown.
        Point a = new(listBox.LayoutBounds.Center.X, dropdown.LayoutBounds.Bottom + 10);
        Point b = overlap.Center;
        Assert.True(listBox.LayoutBounds.Contains(a));
        Assert.False(dropdown.LayoutBounds.Contains(a));

        Frame(runtime, desktop, 5, a);
        Frame(runtime, desktop, 6, a);
        Assert.True(listBox.IsHovered);
        Assert.True(movedInside > 0);
        int movedInsideBefore = movedInside;
        int exitedBefore = exited;

        Frame(runtime, desktop, 7, b);
        Frame(runtime, desktop, 8, b);

        Assert.False(listBox.IsHovered);                     // occluded by the dropdown at B
        Assert.Equal(exitedBefore + 1, exited);              // the listbox saw the mouse leave -> clears its hovered item
        Assert.Equal(movedInsideBefore, movedInside);        // no MovedInside while over the dropdown
        Assert.NotNull(dropdown.HoveredElement);
        Assert.Null(window.HoveredElement);

        // Control: dropdown closed, the next movement hovers the listbox again at the same place.
        comboBox.IsDropdownOpen = false;
        Frame(runtime, desktop, 9, b);
        Frame(runtime, desktop, 10, new Point(b.X + 1, b.Y));
        Assert.Empty(window.NestedWindows);
        Assert.True(listBox.IsHovered);
        Assert.True(movedInside > movedInsideBefore);
        Assert.NotNull(window.HoveredElement);
    }

    [Fact]
    public void NestedWindow_HoveredAtMousePos_SuppressesParentHoveredAndPressedElement_OnlyUnderIt()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow parent = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGButton parentButton = new(parent);
        parent.SetContent(parentButton);
        desktop.Windows.Add(parent);
        MGWindow nested = new(parent, 100, 100, 150, 100) { WindowStyle = WindowStyle.None };
        MGButton nestedButton = new(nested);
        nested.SetContent(nestedButton);
        parent.AddNestedWindow(nested);
        Frame(runtime, desktop, 1, new Point(1, 1));
        Frame(runtime, desktop, 2, new Point(1, 1));

        Point inside = nested.LayoutBounds.Center;
        Point outside = new(20, 20);
        Assert.True(parent.LayoutBounds.Contains(outside));
        Assert.False(nested.LayoutBounds.Contains(outside));

        Frame(runtime, desktop, 3, inside);
        Frame(runtime, desktop, 4, inside);
        Assert.NotNull(nested.HoveredElement);
        Assert.Null(parent.HoveredElement);
        Assert.False(parentButton.IsHovered);
        Assert.True(nestedButton.IsHovered);

        Frame(runtime, desktop, 5, inside, leftPressed: true);
        Assert.NotNull(nested.PressedElement);
        Assert.Null(parent.PressedElement);
        Frame(runtime, desktop, 6, inside);

        Frame(runtime, desktop, 7, outside);
        Frame(runtime, desktop, 8, outside);
        Assert.NotNull(parent.HoveredElement);
        Assert.True(IsSelfOrDescendantOf(parent.HoveredElement, parentButton));
        Assert.True(parentButton.IsHovered);
    }

    [Fact]
    public void HigherDesktopWindow_OverListBox_ListBoxGetsExitedAndNoMovedInside()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow backWindow = new(desktop, 0, 0, 300, 400) { WindowStyle = WindowStyle.None };
        MGListBox<string> listBox = new(backWindow);
        listBox.SetItemsSource(ManyItems);
        backWindow.SetContent(listBox);
        desktop.Windows.Add(backWindow);
        // Added after backWindow -> drawn above it, covering the right half of the listbox.
        MGWindow frontWindow = new(desktop, 150, 0, 300, 400) { WindowStyle = WindowStyle.None };
        frontWindow.SetContent(new MGButton(frontWindow));
        desktop.Windows.Add(frontWindow);
        Frame(runtime, desktop, 1, new Point(1, 1));
        Frame(runtime, desktop, 2, new Point(1, 1));

        int movedInside = 0;
        int exited = 0;
        listBox.MouseHandler.MovedInside += (_, _) => movedInside++;
        listBox.MouseHandler.Exited += (_, _) => exited++;

        Point uncovered = new(50, listBox.LayoutBounds.Center.Y);
        Point covered = new(200, listBox.LayoutBounds.Center.Y);
        Assert.True(listBox.LayoutBounds.Contains(covered));
        Assert.True(frontWindow.LayoutBounds.Contains(covered));
        Assert.False(frontWindow.LayoutBounds.Contains(uncovered));

        Frame(runtime, desktop, 3, uncovered);
        Frame(runtime, desktop, 4, uncovered);
        Assert.True(listBox.IsHovered);
        int movedInsideBefore = movedInside;
        int exitedBefore = exited;

        Frame(runtime, desktop, 5, covered);
        Frame(runtime, desktop, 6, covered);

        Assert.False(listBox.IsHovered);
        Assert.Equal(exitedBefore + 1, exited);
        Assert.Equal(movedInsideBefore, movedInside);
    }

    private static bool IsSelfOrDescendantOf(MGElement element, MGElement ancestor)
    {
        for (MGElement? current = element; current != null; current = current.Parent)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex, Point position, bool leftPressed = false)
    {
        MouseState mouse = new(
            position.X,
            position.Y,
            0,
            leftPressed ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }
}
