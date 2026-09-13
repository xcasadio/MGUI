using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.Input;

/// <summary>The active context menu occludes the windows below it, but never its own content: its items must stay hoverable and clickable.
/// Regression guard for the z-order-aware hit test (<c>MGDesktop.IsUnscaledPositionOccludedAbove</c>).</summary>
public class ContextMenuHoverOcclusionTests
{
    [Fact]
    public void ContextMenuItem_IsHoveredOverItsOwnMenu_AndCardUnderneathIsNot()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGCanvas root = new(window);
        window.SetContent(root);
        desktop.Windows.Add(window);

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

        Frame(runtime, desktop, 0, new Point(1, 1));
        Frame(runtime, desktop, 16, new Point(1, 1));

        MGContextMenu menu = new(window, "");
        menu.AddButton("Item A", _ => { });
        Assert.True(desktop.TryOpenContextMenu(menu, new Point(150, 150)));
        Frame(runtime, desktop, 32, new Point(150, 150));
        Frame(runtime, desktop, 48, new Point(150, 150));

        MGContextMenuItem item = menu.Items[0];
        Point itemCenter = item.ActualLayoutBounds.Center;
        Assert.True(card.ActualLayoutBounds.Contains(itemCenter), "test setup: the menu item must overlap the card");

        //  Over the item: the menu's own content is not occluded by the menu, the card underneath is.
        Frame(runtime, desktop, 64, itemCenter);
        Assert.True(item.IsHovered, "a context menu item must be hoverable over its own menu");
        Assert.False(card.IsHovered, "the card underneath the open context menu must be occluded");
        Assert.True(item.IsSelfOrAncestorOf(menu.HoveredElement) || ReferenceEquals(menu.HoveredElement, item));

        //  Beside the menu, still over the card: the card is hovered again.
        Point besideMenu = new(card.ActualLayoutBounds.Right - 10, card.ActualLayoutBounds.Bottom - 10);
        Assert.False(menu.ActualLayoutBounds.Contains(besideMenu), "test setup: the probe point must be outside the menu");
        Frame(runtime, desktop, 80, besideMenu);
        Assert.False(item.IsHovered);
        Assert.True(card.IsHovered);
    }

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, Point position)
    {
        MouseState mouse = new(position.X, position.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(totalElapsedMs), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }
}
