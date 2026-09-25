namespace MGUI.Tests.MessageBox;

using MGUI.Core.UI;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Input.Semantic;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

/// <summary>
/// Headless tests of <see cref="MGMessageBox"/> (ADR-0018): each way to answer reports one index once, the box blocks the
/// rest of the desktop while it is open, leaves the overlay host when answered, and a box opened from the callback of the
/// previous one is the one that receives the next input - never the input that closed the previous one.
/// </summary>
public class MGMessageBoxTests
{
    private static readonly string[] ThreeLabels = { "Save", "Don't Save", "Cancel" };

    private sealed class Harness
    {
        public GraphTestRuntime Runtime { get; } = new(new Rectangle(0, 0, 800, 600));
        public MGDesktop Desktop { get; }
        public int ElapsedMs { get; private set; }
        public Point MousePosition { get; set; } = new(1, 1);

        public Harness()
        {
            Desktop = new MGDesktop(Runtime);
        }

        /// <summary>Advances one 16 ms frame with the given mouse button and keys held down.</summary>
        public void Frame(MouseButton? pressedButton = null, params Keys[] keysDown)
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(
                TimeSpan.FromMilliseconds(ElapsedMs),
                TimeSpan.FromMilliseconds(16),
                CreateMouseState(MousePosition, pressedButton),
                new KeyboardState(keysDown)));
            Desktop.Update();
            ElapsedMs += 16;
        }

        public void Settle()
        {
            Frame();
            Frame();
        }

        /// <summary>Moves to <paramref name="position"/>, presses and releases the left button there (three frames).</summary>
        public void Click(Point position)
        {
            MousePosition = position;
            Frame();
            Frame(MouseButton.Left);
            Frame();
        }

        /// <summary>Presses and releases <paramref name="key"/> (two frames).</summary>
        public void Tap(Keys key)
        {
            Frame(null, key);
            Frame();
        }
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton)
        => new(
            position.X,
            position.Y,
            0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);

    private static Point CenterOf(MGElement element) => element.ActualLayoutBounds.Center;

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ClickingAButton_ReportsItsIndexOnce_AndRemovesTheBox(int clickedIndex)
    {
        Harness harness = new();
        List<int> answers = new();
        MGMessageBox box = MGMessageBox.Show(harness.Desktop, "Close Screen", "Save changes?", ThreeLabels, 0, 2, answers.Add);
        harness.Settle();
        Assert.Same(box.Overlay, harness.Desktop.OverlayHost.ActiveOverlay);

        harness.Click(CenterOf(box.Buttons[clickedIndex]));
        harness.Settle();

        Assert.Equal(new[] { clickedIndex }, answers);
        Assert.False(box.IsOpen);
        Assert.DoesNotContain(box.Overlay, harness.Desktop.OverlayHost.Overlays);
        Assert.Empty(harness.Desktop.OverlayHost.OpenOverlays);
        Assert.Null(harness.Desktop.OverlayHost.ActiveOverlay);
        Assert.False(harness.Desktop.ShouldCaptureGameplayInput());
    }

    [Fact]
    public void Enter_ChoosesTheDefaultButton()
    {
        Harness harness = new();
        List<int> answers = new();
        MGMessageBox.Show(harness.Desktop, "Quit", "Save changes?", ThreeLabels, 1, 2, answers.Add);
        harness.Settle();

        harness.Tap(Keys.Enter);

        Assert.Equal(new[] { 1 }, answers);
    }

    [Fact]
    public void Escape_ChoosesTheCancelButton()
    {
        Harness harness = new();
        List<int> answers = new();
        MGMessageBox.Show(harness.Desktop, "Quit", "Save changes?", ThreeLabels, 0, 2, answers.Add);
        harness.Settle();

        harness.Tap(Keys.Escape);

        Assert.Equal(new[] { 2 }, answers);
    }

    [Fact]
    public void Enter_StillReachesTheBox_AfterAClickOnItsMessage()
    {
        Harness harness = new();
        List<int> answers = new();
        MGMessageBox box = MGMessageBox.Show(harness.Desktop, "Delete", "Delete 'HudScreen.xaml'?", new[] { "Yes", "No" }, 0, 1, answers.Add);
        harness.Settle();

        //  Top-left corner of the box, inside its padding: on the box, on no button.
        Rectangle boxBounds = box.Overlay.ActualLayoutBounds;
        harness.Click(new Point(boxBounds.Left + 4, boxBounds.Top + 4));
        harness.Settle();
        Assert.Empty(answers);

        harness.Tap(Keys.Enter);

        Assert.Equal(new[] { 0 }, answers);
    }

    [Fact]
    public void EscapeAndAClickInTheSameFrame_ReportOnlyOnce()
    {
        Harness harness = new();
        List<int> answers = new();
        MGMessageBox box = MGMessageBox.Show(harness.Desktop, "Quit", "Save changes?", ThreeLabels, 0, 2, answers.Add);
        harness.Settle();

        harness.MousePosition = CenterOf(box.Buttons[0]);
        harness.Frame();
        harness.Frame(MouseButton.Left);
        harness.Frame(null, Keys.Escape); // the release of the click on button 0 and Escape arrive together
        harness.Settle();

        Assert.Single(answers);
    }

    [Fact]
    public void AButtonOfAnAnsweredBox_DoesNothing()
    {
        Harness harness = new();
        List<int> answers = new();
        MGMessageBox box = MGMessageBox.Show(harness.Desktop, "Delete", "Delete 2 items?", new[] { "Yes", "No" }, 0, 1, answers.Add);
        harness.Settle();
        harness.Tap(Keys.Enter);
        Assert.Equal(new[] { 0 }, answers);

        //  A host holding on to the box after its answer (a stale reference) must not get a second callback.
        bool handledSubmit = box.Buttons[1].TryHandleNavigationAction(UINavigationAction.Submit);
        bool handledCancel = box.Buttons[0].TryHandleNavigationAction(UINavigationAction.Cancel);

        Assert.False(handledCancel);
        Assert.Equal(new[] { 0 }, answers);
        Assert.False(box.IsOpen);
        _ = handledSubmit; // MGButton reports a Submit as handled whenever it has a click handler; only the callback count matters here.
    }

    [Fact]
    public void SemanticSubmitAndCancel_ReachTheBox_WhenTheHostRoutesInputActions()
    {
        Harness harness = new();
        harness.Desktop.UseRawNavigationInput = false;
        List<int> answers = new();
        MGMessageBox.Show(harness.Desktop, "Quit", "Save changes?", ThreeLabels, 1, 2, answers.Add);
        harness.Settle();

        bool handledSubmit = harness.Desktop.TryHandleInputAction(new InputActionEvent(InputAction.Submit,
            new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero, Key: Keys.Enter)));
        harness.Settle();

        MGMessageBox.Show(harness.Desktop, "Quit", "Save changes?", ThreeLabels, 0, 2, answers.Add);
        harness.Settle();

        bool handledCancel = harness.Desktop.TryHandleInputAction(new InputActionEvent(InputAction.Cancel,
            new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero, Key: Keys.Escape)));

        Assert.True(handledSubmit);
        Assert.True(handledCancel);
        Assert.Equal(new[] { 1, 2 }, answers);
    }

    [Fact]
    public void WhileOpen_TheBoxBlocksEveryOtherWindow_AndCapturesGameplayInput()
    {
        Harness harness = new();

        MGWindow rootWindow = new(harness.Desktop, 0, 0, 200, 150) { WindowStyle = WindowStyle.None };
        int rootClicks = 0;
        MGButton rootButton = new(rootWindow, _ => rootClicks++);
        rootWindow.SetContent(rootButton);
        harness.Desktop.Windows.Add(rootWindow);

        //  A nested window, as the docking host adds its floating windows (MGDockHost: ParentWindow.AddNestedWindow).
        MGWindow nestedWindow = new(rootWindow, 580, 440, 200, 150) { WindowStyle = WindowStyle.None };
        int nestedClicks = 0;
        MGButton nestedButton = new(nestedWindow, _ => nestedClicks++);
        nestedWindow.SetContent(nestedButton);
        rootWindow.AddNestedWindow(nestedWindow);
        harness.Settle();

        //  Sanity: both buttons take clicks before the box opens.
        harness.Click(CenterOf(rootButton));
        harness.Click(CenterOf(nestedButton));
        Assert.Equal(1, rootClicks);
        Assert.Equal(1, nestedClicks);

        List<int> answers = new();
        MGMessageBox box = MGMessageBox.Show(harness.Desktop, "Delete", "Delete 2 items?", new[] { "Yes", "No" }, 0, 1, answers.Add);
        harness.Settle();
        Assert.False(box.Overlay.ActualLayoutBounds.Intersects(rootButton.ActualLayoutBounds));
        Assert.False(box.Overlay.ActualLayoutBounds.Intersects(nestedButton.ActualLayoutBounds));

        harness.Click(CenterOf(rootButton));
        harness.Click(CenterOf(nestedButton));

        Assert.Equal(1, rootClicks);
        Assert.Equal(1, nestedClicks);
        Assert.True(box.IsOpen);
        Assert.Empty(answers);
        Assert.True(harness.Desktop.ShouldCaptureGameplayInput());
    }

    /// <summary>
    /// The frame that answers a box also removes its overlay, and the desktop updates the other windows after the overlay
    /// window in that same frame: the release that answered must not reach what lies under the box's button.
    /// </summary>
    [Fact]
    public void TheClickThatAnswersABox_DoesNotReachWhatLiesUnderIt()
    {
        Harness harness = new();
        MGWindow rootWindow = new(harness.Desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };
        int underneathReleases = 0;
        int underneathClicks = 0;
        MGButton underneath = new(rootWindow, _ => underneathClicks++);
        underneath.MouseHandler.ReleasedInside += (_, _) => underneathReleases++;
        rootWindow.SetContent(underneath); // fills the whole window, so it lies under every button of the box
        harness.Desktop.Windows.Add(rootWindow);
        harness.Settle();

        List<int> answers = new();
        MGMessageBox box = MGMessageBox.Show(harness.Desktop, "Delete", "Delete 2 items?", new[] { "Yes", "No" }, 0, 1, answers.Add);
        harness.Settle();
        Assert.True(underneath.ActualLayoutBounds.Contains(CenterOf(box.Buttons[1])));

        harness.Click(CenterOf(box.Buttons[1]));
        harness.Settle();

        Assert.Equal(new[] { 1 }, answers);
        Assert.Equal(0, underneathReleases);
        Assert.Equal(0, underneathClicks);
    }

    [Fact]
    public void ABoxShownOverAnotherOverlay_BecomesActive_AndHandsBackWhenAnswered()
    {
        Harness harness = new();
        MGButton otherContent = new(harness.Desktop.OverlayHost.ParentWindow);
        MGOverlay otherOverlay = harness.Desktop.OverlayHost.AddOverlay(otherContent);
        otherOverlay.IsOpen = true;
        harness.Settle();
        Assert.Same(otherOverlay, harness.Desktop.OverlayHost.ActiveOverlay);

        List<int> answers = new();
        MGMessageBox box = MGMessageBox.Show(harness.Desktop, "Error", "Failed to open project.", new[] { "OK" }, 0, 0, answers.Add);
        harness.Settle();
        Assert.Same(box.Overlay, harness.Desktop.OverlayHost.ActiveOverlay);

        harness.Tap(Keys.Enter);

        Assert.Equal(new[] { 0 }, answers);
        Assert.Same(otherOverlay, harness.Desktop.OverlayHost.ActiveOverlay);
    }

    public enum ClosingInput
    {
        Click,
        Enter,
        Escape,
    }

    /// <summary>
    /// The editor's queue opens its next box from the callback of the one that just closed, while MGUI is still dispatching
    /// the click or key that closed it (plan T1.1, D8). The next box must be the active overlay, must not be answered by
    /// that same input - not even while Enter stays held down - and must then answer its own input.
    /// </summary>
    [Theory]
    [InlineData(ClosingInput.Click)]
    [InlineData(ClosingInput.Enter)]
    [InlineData(ClosingInput.Escape)]
    public void ABoxOpenedFromTheCallback_IsActive_AndIgnoresTheInputThatClosedThePreviousOne(ClosingInput closingInput)
    {
        Harness harness = new();
        MGOverlayHost host = harness.Desktop.OverlayHost;
        List<int> firstAnswers = new();
        List<int> secondAnswers = new();
        MGMessageBox first = null;
        MGMessageBox second = null;
        bool firstHadLeftTheHostWhenCalledBack = false;

        first = MGMessageBox.Show(harness.Desktop, "Close Screen", "Save changes to 'A'?", ThreeLabels, 0, 2, index =>
        {
            firstAnswers.Add(index);
            firstHadLeftTheHostWhenCalledBack = !host.OpenOverlays.Contains(first.Overlay)
                && !host.Overlays.Contains(first.Overlay)
                && host.ActiveOverlay == null;
            second = MGMessageBox.Show(harness.Desktop, "Close Screen", "Save changes to 'B'?", ThreeLabels, 0, 2, secondAnswers.Add);
        });
        harness.Settle();

        switch (closingInput)
        {
            case ClosingInput.Click:
                harness.MousePosition = CenterOf(first.Buttons[1]);
                harness.Frame();
                harness.Frame(MouseButton.Left);
                harness.Frame(); // release: the first box closes and the second opens during this update
                break;
            case ClosingInput.Enter:
                harness.Frame(null, Keys.Enter); // the first box closes and the second opens during this update
                break;
            case ClosingInput.Escape:
                harness.Frame(null, Keys.Escape);
                break;
        }

        int expectedFirstAnswer = closingInput switch
        {
            ClosingInput.Click => 1,
            ClosingInput.Enter => 0,
            _ => 2,
        };
        Assert.Equal(new[] { expectedFirstAnswer }, firstAnswers);
        Assert.True(firstHadLeftTheHostWhenCalledBack);
        Assert.NotNull(second);
        Assert.Same(second.Overlay, host.ActiveOverlay);
        Assert.Empty(secondAnswers);
        Assert.True(harness.Desktop.ShouldCaptureGameplayInput());

        //  Keep the closing key held past MGUI's key-repeat delay: a held key must not answer the second box either.
        Keys[] held = closingInput switch
        {
            ClosingInput.Enter => new[] { Keys.Enter },
            ClosingInput.Escape => new[] { Keys.Escape },
            _ => Array.Empty<Keys>(),
        };
        for (int i = 0; i < 60; i++)
        {
            harness.Frame(null, held);
        }

        harness.Settle();
        Assert.Empty(secondAnswers);
        Assert.True(second.IsOpen);
        Assert.Same(second.Overlay, host.ActiveOverlay);

        //  The second box then answers its own input: Enter reaches its default button.
        harness.Tap(Keys.Enter);

        Assert.Equal(new[] { 0 }, secondAnswers);
        Assert.Equal(new[] { expectedFirstAnswer }, firstAnswers);
        Assert.Null(host.ActiveOverlay);
    }

    [Fact]
    public void InvalidArguments_FailEarly()
    {
        Harness harness = new();
        MGDesktop desktop = harness.Desktop;
        Action<int> ignore = _ => { };

        Assert.Throws<ArgumentNullException>(() => MGMessageBox.Show(null, "t", "m", new[] { "OK" }, 0, 0, ignore));
        Assert.Throws<ArgumentNullException>(() => MGMessageBox.Show(desktop, "t", "m", null, 0, 0, ignore));
        Assert.Throws<ArgumentNullException>(() => MGMessageBox.Show(desktop, "t", "m", new[] { "OK" }, 0, 0, null));
        Assert.Throws<ArgumentException>(() => MGMessageBox.Show(desktop, "t", "m", Array.Empty<string>(), 0, 0, ignore));
        Assert.Throws<ArgumentException>(() => MGMessageBox.Show(desktop, "t", "m", new[] { "1", "2", "3", "4" }, 0, 0, ignore));
        Assert.Throws<ArgumentException>(() => MGMessageBox.Show(desktop, "t", "m", new[] { "OK", null }, 0, 0, ignore));
        Assert.Throws<ArgumentOutOfRangeException>(() => MGMessageBox.Show(desktop, "t", "m", new[] { "OK" }, 1, 0, ignore));
        Assert.Throws<ArgumentOutOfRangeException>(() => MGMessageBox.Show(desktop, "t", "m", new[] { "OK" }, 0, -1, ignore));

        desktop.OverlayHost.IsModal = false;
        Assert.Throws<InvalidOperationException>(() => MGMessageBox.Show(desktop, "t", "m", new[] { "OK" }, 0, 0, ignore));

        Assert.Empty(desktop.OverlayHost.Overlays);
    }
}
