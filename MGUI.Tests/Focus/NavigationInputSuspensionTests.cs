using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.GamePad;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;
using GamePadButton = MGUI.Shared.Input.GamePad.GamePadButton;

namespace MGUI.Tests.Focus;

/// <summary>
/// <see cref="MGDesktop.IsNavigationInputSuspended"/> pauses the raw navigation path, and what was held meanwhile stays
/// silent afterwards (ADR-0025). A frame is 16 ms, so a button repeats after 22 of them and then every 8.
/// </summary>
public class NavigationInputSuspensionTests
{
    private const int FramesPastTheFirstRepeat = 40;

    private static readonly GamePadState NoButton = new(Vector2.Zero, Vector2.Zero, 0f, 0f, Array.Empty<Buttons>());
    private static readonly GamePadState HoldingA = new(Vector2.Zero, Vector2.Zero, 0f, 0f, Buttons.A);
    private static readonly GamePadState HoldingDown = new(Vector2.Zero, Vector2.Zero, 0f, 0f, Buttons.DPadDown);

    private readonly GraphTestRuntime _runtime = new(new Rectangle(0, 0, 800, 600));
    private readonly MGDesktop _desktop;
    private readonly MGStackPanel _panel;
    private readonly MGButton _first;
    private readonly MGButton _second;
    private int _elapsedMs;
    private int _submits;

    public NavigationInputSuspensionTests()
    {
        _desktop = new MGDesktop(_runtime);
        MGWindow window = new(_desktop, 0, 0, 200, 200);
        _panel = new MGStackPanel(window, Orientation.Vertical);
        _first = new MGButton(window, _ => _submits++);
        _second = new MGButton(window);
        _panel.TryAddChild(_first);
        _panel.TryAddChild(_second);
        window.SetContent(_panel);
        _desktop.Windows.Add(window);

        Frame(NoButton);
        Frame(NoButton);
        _first.Focus(KeyboardFocusSource.Programmatic);
        Frame(NoButton);
        Frame(NoButton);
        Assert.Same(_first, _desktop.FocusedKeyboardHandler);
    }

    private GamePadTracker Tracker => _desktop.InputTracker.GamePad;

    [Fact]
    public void AnInjectedState_IsRead_AsAConnectedGamepad()
    {
        Frame(HoldingA);

        Assert.True(Tracker.IsConnected);
        Assert.True(Tracker.WasTriggered(GamePadButton.A));
    }

    [Fact]
    public void AHeldButton_Repeats_ThisIsWhatTheSuspensionHasToSilence()
    {
        Frames(HoldingA, FramesPastTheFirstRepeat);

        Assert.True(_submits > 1, $"{_submits} submits");
    }

    [Fact]
    public void FalseWrittenEveryFrame_LeavesAHeldButtonRepeating()
    {
        Frames(HoldingA, FramesPastTheFirstRepeat);
        int submitsWithNoWrite = _submits;
        Assert.True(submitsWithNoWrite > 1, $"{submitsWithNoWrite} submits");
        Frame(NoButton);
        _submits = 0;

        for (int i = 0; i < FramesPastTheFirstRepeat; i++)
        {
            Frame(HoldingA);

            //  Where a host that works the value out each frame writes it, between two calls to Update. Only the end of a suspension forgets a button.
            _desktop.IsNavigationInputSuspended = false;
        }

        Assert.Equal(submitsWithNoWrite, _submits);
    }

    [Fact]
    public void WhileSuspended_NoButtonReachesTheFocus_AndTheFocusStays()
    {
        _desktop.IsNavigationInputSuspended = true;

        Frames(HoldingA, FramesPastTheFirstRepeat);
        Frame(NoButton);
        Frames(HoldingDown, FramesPastTheFirstRepeat);

        Assert.Equal(0, _submits);
        Assert.Same(_first, _desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void WhileSuspended_NoKeyReachesTheFocus()
    {
        _desktop.IsNavigationInputSuspended = true;

        Frame(NoButton, Keys.Enter);
        Frame(NoButton);
        Frame(NoButton, Keys.Down);
        Frame(NoButton);

        Assert.Equal(0, _submits);
        Assert.Same(_first, _desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void AButtonHeldThroughTheSuspension_FiresNothingAfterIt_UntilItIsPressedAgain()
    {
        _desktop.IsNavigationInputSuspended = true;
        Frames(HoldingA, FramesPastTheFirstRepeat);

        _desktop.IsNavigationInputSuspended = false;
        Frames(HoldingA, FramesPastTheFirstRepeat);
        Assert.Equal(0, _submits);

        Frame(NoButton);
        Frame(HoldingA);
        Assert.Equal(1, _submits);
    }

    [Fact]
    public void ADirectionHeldThroughTheSuspension_DoesNotMoveTheFocusAfterIt()
    {
        _desktop.IsNavigationInputSuspended = true;
        Frames(HoldingDown, FramesPastTheFirstRepeat);

        _desktop.IsNavigationInputSuspended = false;
        Frames(HoldingDown, FramesPastTheFirstRepeat);

        Assert.Same(_first, _desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void AButtonPressedOnTheFrameAfterTheSuspension_Fires()
    {
        _desktop.IsNavigationInputSuspended = true;
        Frames(NoButton, 3);
        _desktop.IsNavigationInputSuspended = false;

        Frame(HoldingA);

        Assert.Equal(1, _submits);
    }

    [Fact]
    public void AKeyHeldThroughTheSuspension_FiresNothingAfterIt_UntilItIsPressedAgain()
    {
        _desktop.IsNavigationInputSuspended = true;
        Frames(NoButton, FramesPastTheFirstRepeat, Keys.Enter);

        _desktop.IsNavigationInputSuspended = false;
        Frames(NoButton, FramesPastTheFirstRepeat, Keys.Enter);
        Assert.Equal(0, _submits);

        Frame(NoButton);
        Frame(NoButton, Keys.Enter);
        Frame(NoButton);
        Assert.Equal(1, _submits);
    }

    [Fact]
    public void Suspending_LeavesUseRawNavigationInputAlone()
    {
        _desktop.UseRawNavigationInput = false;

        _desktop.IsNavigationInputSuspended = true;
        _desktop.IsNavigationInputSuspended = false;

        Assert.False(_desktop.UseRawNavigationInput);
    }

    [Fact]
    public void ALiftedSuspension_LeavesARawPathTurnedOffStillOff()
    {
        _desktop.UseRawNavigationInput = false;
        _desktop.IsNavigationInputSuspended = true;
        Frame(NoButton);
        _desktop.IsNavigationInputSuspended = false;

        Frame(HoldingA);

        Assert.Equal(0, _submits);
    }

    [Fact]
    public void ForgetHeldButtons_ClearsWhatTheFrameAlreadyTriggered()
    {
        Frame(HoldingA);
        Assert.True(Tracker.WasTriggered(GamePadButton.A));

        Tracker.ForgetHeldButtons();

        Assert.False(Tracker.WasTriggered(GamePadButton.A));
        Assert.False(Tracker.HasActivity());
    }

    private void Frames(GamePadState gamePad, int count, params Keys[] keysDown)
    {
        for (int i = 0; i < count; i++)
        {
            Frame(gamePad, keysDown);
        }
    }

    private void Frame(GamePadState gamePad, params Keys[] keysDown)
    {
        Tracker.StateOverride = gamePad;
        _runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(_elapsedMs),
            TimeSpan.FromMilliseconds(16),
            default,
            new KeyboardState(keysDown)));
        _desktop.Update();
        _elapsedMs += 16;
    }
}
