using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Tests.Architecture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Input;

/// <summary>
/// <see cref="MGButton"/>, <see cref="MGCheckBox"/>, <see cref="MGRadioButton"/> and <see cref="MGToggleButton"/> keep the press that began a click from its delivery to the release (ADR-0034:
/// <see cref="MGUI.Shared.Input.Mouse.MouseTracker"/> keeps one press per button, valid until the next press of the button, so while the button is held and during its release). A press, held
/// for several frames, acts only at the release; a release outside does not act and clears the kept press, so that the next click acts; a repeat button reads its kept press on every
/// frame of the hold.
/// </summary>
public class KeptPressedEventTests
{
    private const int HeldFrames = 6;

    private static void Hover(ControlBench bench, Point at)
    {
        bench.Mouse = at;
        bench.Frames(2);
    }

    /// <summary>Press, hold for <see cref="HeldFrames"/> frames and release at <paramref name="releaseAt"/>; <paramref name="duringTheHold"/> runs after each held frame.</summary>
    private static void PressHoldRelease(ControlBench bench, Point at, Point releaseAt, Action duringTheHold = null)
    {
        Hover(bench, at);
        bench.Left = ButtonState.Pressed;
        bench.Frame();
        duringTheHold?.Invoke();
        for (int i = 0; i < HeldFrames; i++)
        {
            bench.Frame();
            duringTheHold?.Invoke();
        }

        bench.Mouse = releaseAt;
        bench.Left = ButtonState.Released;
        bench.Frame();
        bench.Frames(2);
    }

    private static Point Center(MGElement element) => element.ActualLayoutBounds.Center;

    [Fact]
    public void AButton_ClicksAtTheRelease_AfterAPressHeldForSeveralFrames()
    {
        ControlBench bench = new();
        MGButton button = new(bench.Window) { PreferredWidth = 100, PreferredHeight = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        int clicks = 0;
        button.OnLeftClicked += (_, _) => clicks++;
        bench.Show(button);
        Point inside = Center(button);

        PressHoldRelease(bench, inside, inside, () => Assert.Equal(0, clicks));
        Assert.Equal(1, clicks);

        PressHoldRelease(bench, inside, inside, () => Assert.Equal(1, clicks));
        Assert.Equal(2, clicks);
    }

    [Fact]
    public void AButton_DoesNotClick_WhenTheReleaseIsOutside_AndClicksAtTheNextRelease()
    {
        ControlBench bench = new();
        MGButton button = new(bench.Window) { PreferredWidth = 100, PreferredHeight = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        int clicks = 0;
        button.OnLeftClicked += (_, _) => clicks++;
        bench.Show(button);
        Point inside = Center(button);
        Point outside = new(button.ActualLayoutBounds.Right + 60, button.ActualLayoutBounds.Bottom + 60);

        PressHoldRelease(bench, inside, outside);
        Assert.Equal(0, clicks);
        PressHoldRelease(bench, inside, inside);
        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ARepeatButton_ReadsItsKeptPressAtEachFrameOfTheHold_AndDoesNotRepeatAfterTheRelease()
    {
        ControlBench bench = new();
        int commands = 0;
        MGButton button = new(bench.Window) { PreferredWidth = 100, PreferredHeight = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, IsRepeatButton = true, InitialRepeatInterval = TimeSpan.Zero, RepeatInterval = TimeSpan.Zero };
        button.Command = _ =>
        {
            commands++;
            return true;
        };
        bench.Show(button);
        Point inside = Center(button);
        int commandsOfTheLastHeldFrame = 0;

        PressHoldRelease(bench, inside, inside, () => commandsOfTheLastHeldFrame = commands);

        //  The press of a repeat button is handled by the button when it is delivered, and the repeat reads that kept press at each frame: the commands go on during the whole hold.
        Assert.True(commandsOfTheLastHeldFrame >= HeldFrames - 2, $"{commandsOfTheLastHeldFrame} commands over {HeldFrames} held frames");
        int afterTheRelease = commands;
        bench.Frames(5);
        Assert.Equal(afterTheRelease, commands);

        //  A second hold repeats again: the press of the first one is not what the button reads.
        int beforeTheSecondHold = commands;
        PressHoldRelease(bench, inside, inside);
        Assert.True(commands - beforeTheSecondHold >= HeldFrames - 2, $"{commands - beforeTheSecondHold} commands over {HeldFrames} held frames");
    }

    [Fact]
    public void ACheckBox_TogglesAtTheRelease_AfterAPressHeldForSeveralFrames()
    {
        ControlBench bench = new();
        MGCheckBox checkBox = new(bench.Window) { PreferredWidth = 100, PreferredHeight = 30, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        bench.Show(checkBox);
        Point inside = Center(checkBox);

        PressHoldRelease(bench, inside, inside, () => Assert.Equal(false, checkBox.IsChecked));
        Assert.Equal(true, checkBox.IsChecked);
        PressHoldRelease(bench, inside, inside, () => Assert.Equal(true, checkBox.IsChecked));
        Assert.Equal(false, checkBox.IsChecked);

        PressHoldRelease(bench, inside, new Point(inside.X + 250, inside.Y + 150));
        Assert.Equal(false, checkBox.IsChecked);
        PressHoldRelease(bench, inside, inside);
        Assert.Equal(true, checkBox.IsChecked);
    }

    [Fact]
    public void ARadioButton_ChecksAtTheRelease_AfterAPressHeldForSeveralFrames()
    {
        ControlBench bench = new();
        //  The first radio button of a group is the checked one: the click is on the second.
        MGStackPanel panel = new(bench.Window, Orientation.Vertical) { HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGRadioButton first = new(bench.Window, "group") { PreferredWidth = 100, PreferredHeight = 30 };
        MGRadioButton second = new(bench.Window, "group") { PreferredWidth = 100, PreferredHeight = 30 };
        Assert.True(panel.TryAddChild(first));
        Assert.True(panel.TryAddChild(second));
        bench.Show(panel);
        Assert.True(first.IsChecked);
        Point inside = Center(second);

        PressHoldRelease(bench, inside, inside, () => Assert.False(second.IsChecked));
        Assert.True(second.IsChecked);
        Assert.False(first.IsChecked);
    }

    [Fact]
    public void AToggleButton_TogglesAtTheRelease_AfterAPressHeldForSeveralFrames()
    {
        ControlBench bench = new();
        MGToggleButton toggleButton = new(bench.Window) { PreferredWidth = 100, PreferredHeight = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        bench.Show(toggleButton);
        Point inside = Center(toggleButton);

        PressHoldRelease(bench, inside, inside, () => Assert.False(toggleButton.IsChecked));
        Assert.True(toggleButton.IsChecked);
        PressHoldRelease(bench, inside, inside, () => Assert.True(toggleButton.IsChecked));
        Assert.False(toggleButton.IsChecked);

        PressHoldRelease(bench, inside, new Point(inside.X + 250, inside.Y + 150));
        Assert.False(toggleButton.IsChecked);
        PressHoldRelease(bench, inside, inside);
        Assert.True(toggleButton.IsChecked);
    }
}
