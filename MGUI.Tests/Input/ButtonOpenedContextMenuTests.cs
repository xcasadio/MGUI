using System;
using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Input;

/// <summary>
/// The "open a menu from a button" pattern used by MGUI.Samples/Features/StyleThemeRefactor and NativeDarkThemePreview: the menu is the
/// button's <see cref="MGElement.ContextMenu"/> (never a panel child, which the panel's next layout pass would move back into its slot) and
/// is opened from the button's left-button release at a screen-space point just below the button. Driven by real mouse frames.
/// </summary>
public class ButtonOpenedContextMenuTests
{
    [Fact]
    public void LeftClick_Opens_The_Buttons_ContextMenu_Below_It_And_It_Stays_Open_While_The_Pointer_Moves_Into_It()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        MGDesktop desktop = new(runtime);
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Left=""20"" Top=""20"" Width=""420"" Height=""320"">
    <ScrollViewer VerticalScrollBarVisibility=""Auto"">
        <StackPanel Orientation=""Vertical"" Spacing=""8"">
            <TextBlock Text=""Composite Controls"" />
            <Button Name=""OpenContextMenuButton"" Content=""Open Sample Context Menu"" HorizontalAlignment=""Left"">
                <Button.ContextMenu>
                    <ContextMenu Name=""SampleContextMenu"" TitleText=""Actions"" ControlTemplate=""ContextMenu.Default"">
                        <ContextMenuButton Content=""Open"" />
                        <ContextMenuButton Content=""Duplicate"" />
                        <ContextMenuButton Content=""Delete"" />
                    </ContextMenu>
                </Button.ContextMenu>
            </Button>
        </StackPanel>
    </ScrollViewer>
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(desktop, xaml, false, true);
        desktop.Windows.Add(window);
        MGButton button = window.GetElementByName<MGButton>("OpenContextMenuButton");
        MGContextMenu menu = window.GetElementByName<MGContextMenu>("SampleContextMenu");
        Assert.Null(menu.Parent);

        // Same handler as the samples.
        button.MouseHandler.LMBReleasedInside += (_, e) =>
        {
            Rectangle buttonScreenBounds = button.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, button.LayoutBounds);
            if (menu.TryOpenContextMenu(new Point(buttonScreenBounds.Left, buttonScreenBounds.Bottom)))
            {
                e.SetHandledBy(menu, false);
            }
        };

        Frame(runtime, desktop, 0, Point.Zero);
        Frame(runtime, desktop, 1, Point.Zero);
        Rectangle buttonScreen = button.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, button.LayoutBounds);

        // Click on the left part of the button: the original sample anchored the menu on the button's right edge, so such a click
        // was far enough from the menu for the auto-close distance to trigger on the first pointer move.
        Point click = new(buttonScreen.Left + 8, buttonScreen.Center.Y);
        Frame(runtime, desktop, 2, click);
        Frame(runtime, desktop, 3, click, leftPressed: true);
        Frame(runtime, desktop, 4, click);

        Assert.Same(menu, desktop.ActiveContextMenu);
        Point popupTopLeft = menu.LayoutBounds.Location;
        Assert.InRange(popupTopLeft.X, buttonScreen.Left, buttonScreen.Left + 1);
        Assert.Equal(buttonScreen.Bottom, popupTopLeft.Y);

        Point target = menu.LayoutBounds.Center;
        for (int step = 1; step <= 4; step++)
        {
            Point pointer = new(click.X + (target.X - click.X) * step / 4, click.Y + (target.Y - click.Y) * step / 4);
            Frame(runtime, desktop, 4 + step, pointer);

            Assert.Same(menu, desktop.ActiveContextMenu);
            Assert.Equal(popupTopLeft, menu.LayoutBounds.Location);
        }
    }

    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex, Point mousePosition, bool leftPressed = false)
    {
        ButtonState left = leftPressed ? ButtonState.Pressed : ButtonState.Released;
        MouseState mouse = new(mousePosition.X, mousePosition.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }
}
