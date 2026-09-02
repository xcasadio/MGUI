using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// Nested-window occlusion of the parent window's own content (reported in the FocusInputReview sample: hovering the open
/// <see cref="MGComboBox{TItemType}"/> dropdown also highlighted the <see cref="MGListBox{TItemType}"/> items drawn beneath it).
/// The dropdown is a nested <see cref="MGWindow"/> of the parent window, so the desktop-level cross-window occlusion never applied;
/// <c>MGWindow.OnBeginUpdateContents</c> now reuses its nested-window occlusion predicate to suppress the parent's
/// <see cref="MGWindow.HoveredElement"/>/<see cref="MGWindow.PressedElement"/> while a non-click-through nested window is hovered.
/// </summary>
public class NestedWindowHoverOcclusionTests
{
    [Fact]
    public void OpenComboBoxDropdown_HoveringItemOverListBox_DoesNotHoverListBoxBeneath()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 300) { WindowStyle = WindowStyle.None };
        MGComboBox<string> comboBox = new(window) { PreferredHeight = 30 };
        comboBox.SetItemsSource(new List<string> { "All items", "Focusable controls", "Popup-backed controls", "Overlay blockers" });
        MGListBox<string> listBox = new(window);
        listBox.SetItemsSource(new List<string> { "SearchTextBox", "FilterComboBox", "ResultsListBox", "ContextMenuTextBox", "PopupTextBox" });
        MGStackPanel panel = new(window, Orientation.Vertical);
        panel.TryAddChild(comboBox);
        panel.TryAddChild(listBox);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        Frame(runtime, desktop, 1, new Point(1, 1));
        Frame(runtime, desktop, 2, new Point(1, 1));

        comboBox.IsDropdownOpen = true;
        Frame(runtime, desktop, 3, new Point(1, 1));
        Frame(runtime, desktop, 4, new Point(1, 1));
        MGWindow dropdown = Assert.Single(window.NestedWindows);
        Rectangle overlap = Rectangle.Intersect(dropdown.LayoutBounds, listBox.LayoutBounds);
        Assert.False(overlap.IsEmpty); // the dropdown really covers part of the listbox, as in the sample
        Point hoverPoint = overlap.Center;

        Frame(runtime, desktop, 5, hoverPoint);
        Frame(runtime, desktop, 6, hoverPoint);

        Assert.NotNull(dropdown.HoveredElement);
        Assert.Null(window.HoveredElement);

        // Control: once the dropdown is closed, the same point hovers the listbox normally.
        comboBox.IsDropdownOpen = false;
        Frame(runtime, desktop, 7, hoverPoint);
        Frame(runtime, desktop, 8, hoverPoint);

        Assert.Empty(window.NestedWindows);
        Assert.NotNull(window.HoveredElement);
        Assert.True(IsSelfOrDescendantOf(window.HoveredElement, listBox));
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

        // Hover over the nested window: only the nested window lights up.
        Frame(runtime, desktop, 3, inside);
        Frame(runtime, desktop, 4, inside);
        Assert.NotNull(nested.HoveredElement);
        Assert.Null(parent.HoveredElement);

        // Press over the nested window: the parent content beneath must not enter the pressed state.
        Frame(runtime, desktop, 5, inside, leftPressed: true);
        Assert.NotNull(nested.PressedElement);
        Assert.Null(parent.PressedElement);
        Frame(runtime, desktop, 6, inside);

        // Outside the nested window, the parent hovers its own content normally.
        Frame(runtime, desktop, 7, outside);
        Frame(runtime, desktop, 8, outside);
        Assert.NotNull(parent.HoveredElement);
        Assert.True(IsSelfOrDescendantOf(parent.HoveredElement, parentButton));
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
