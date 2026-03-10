namespace MGUI.Tests.Focus;

/// <summary>Unit tests for the IsFocusable auto-focus-on-click mechanism (pure logic, no MonoGame runtime).</summary>
public class FocusTests
{
    // ── Minimal stub that replicates the IsFocusable / auto-subscribe logic ──────

    /// <summary>
    /// Minimal simulation of the IsFocusable setter logic introduced in Task 1a.
    /// Real code lives in <c>MGElement.IsFocusable</c>.
    /// </summary>
    private sealed class FocusableStub
    {
        // Mirrors the backing fields added to MGElement.
        private bool _isFocusable;
        private bool _autoFocusSubscribed;

        // Counts how many times Focus() was called via the auto-subscription.
        public int AutoFocusCallCount { get; private set; }
        // Counts how many times the handler lambda was registered.
        public int SubscriptionCount  { get; private set; }

        // Simulated MouseHandler LMBPressedInside event.
        private event Action? LMBPressedInside;

        public bool IsFocusable
        {
            get => _isFocusable;
            set
            {
                if (_isFocusable != value)
                {
                    _isFocusable = value;
                    if (value && !_autoFocusSubscribed)
                    {
                        _autoFocusSubscribed = true;
                        SubscriptionCount++;
                        LMBPressedInside += () =>
                        {
                            if (IsFocusable) AutoFocusCallCount++;
                        };
                    }
                }
            }
        }

        /// <summary>Simulates a left-mouse-button press inside the element.</summary>
        public void SimulateClick() => LMBPressedInside?.Invoke();
    }

    // ── Tests ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void IsFocusable_SetTrue_RegistersExactlyOneSubscription()
    {
        var el = new FocusableStub();
        el.IsFocusable = true;
        Assert.Equal(1, el.SubscriptionCount);
    }

    [Fact]
    public void IsFocusable_SetTrueTwice_DoesNotDoubleSubscribe()
    {
        // Setting the same value twice: second assignment is a no-op (value unchanged).
        var el = new FocusableStub();
        el.IsFocusable = true;
        el.IsFocusable = true;
        Assert.Equal(1, el.SubscriptionCount);
    }

    [Fact]
    public void IsFocusable_ToggledFalseAndBackToTrue_DoesNotResubscribe()
    {
        // Once subscribed, toggling off then back on must not create a second subscription.
        var el = new FocusableStub();
        el.IsFocusable = true;
        el.IsFocusable = false;
        el.IsFocusable = true;
        Assert.Equal(1, el.SubscriptionCount);
    }

    [Fact]
    public void Click_WhenFocusable_IncrementsFocusCount()
    {
        var el = new FocusableStub { IsFocusable = true };
        el.SimulateClick();
        Assert.Equal(1, el.AutoFocusCallCount);
    }

    [Fact]
    public void Click_WhenNotFocusable_DoesNotFocus()
    {
        // Handler is registered but checks IsFocusable at call time.
        var el = new FocusableStub();
        el.IsFocusable = true;   // subscribes
        el.IsFocusable = false;  // now disabled
        el.SimulateClick();
        Assert.Equal(0, el.AutoFocusCallCount);
    }

    [Fact]
    public void Click_MultipleTimes_FocusesEachTime()
    {
        var el = new FocusableStub { IsFocusable = true };
        el.SimulateClick();
        el.SimulateClick();
        el.SimulateClick();
        Assert.Equal(3, el.AutoFocusCallCount);
    }

    [Fact]
    public void IsFocusable_NeverSetTrue_NoSubscription()
    {
        var el = new FocusableStub();
        Assert.Equal(0, el.SubscriptionCount);
        el.SimulateClick();
        Assert.Equal(0, el.AutoFocusCallCount);
    }

    [Fact]
    public void VisualStateSetting_FocusedValue_IsReturned()
    {
        var setting = new MGUI.Core.UI.VisualStateSetting<int>(1, 2, 3, 4);

        Assert.Equal(1, setting.GetValue(MGUI.Core.UI.PrimaryVisualState.Normal));
        Assert.Equal(2, setting.GetValue(MGUI.Core.UI.PrimaryVisualState.Selected));
        Assert.Equal(3, setting.GetValue(MGUI.Core.UI.PrimaryVisualState.Focused));
        Assert.Equal(4, setting.GetValue(MGUI.Core.UI.PrimaryVisualState.Disabled));
    }

    [Fact]
    public void ResolveInputMode_MouseActivity_Wins()
    {
        var result = MGUI.Core.UI.MGDesktop.ResolveInputMode(hasMouseActivity: true, hasKeyboardActivity: true, isTextEntryFocused: true, currentMode: MGUI.Core.UI.UIInputMode.Navigation);

        Assert.Equal(MGUI.Core.UI.UIInputMode.Pointer, result);
    }

    [Fact]
    public void ResolveInputMode_KeyboardActivity_OnTextEntry_ReturnsTextEntry()
    {
        var result = MGUI.Core.UI.MGDesktop.ResolveInputMode(hasMouseActivity: false, hasKeyboardActivity: true, isTextEntryFocused: true, currentMode: MGUI.Core.UI.UIInputMode.Pointer);

        Assert.Equal(MGUI.Core.UI.UIInputMode.TextEntry, result);
    }

    [Fact]
    public void ResolveInputMode_NoActivity_KeepsCurrentMode()
    {
        var result = MGUI.Core.UI.MGDesktop.ResolveInputMode(hasMouseActivity: false, hasKeyboardActivity: false, isTextEntryFocused: false, currentMode: MGUI.Core.UI.UIInputMode.Navigation);

        Assert.Equal(MGUI.Core.UI.UIInputMode.Navigation, result);
    }

    [Fact]
    public void ResolvePrimaryVisualState_Focused_WhenEnabledAndNavigationVisible()
    {
        var result = MGUI.Core.UI.MGElement.ResolvePrimaryVisualState(isEnabled: true, isSelected: false, hasKeyboardFocus: true, shouldDisplayFocusedState: true);

        Assert.Equal(MGUI.Core.UI.PrimaryVisualState.Focused, result);
    }

    [Fact]
    public void ResolvePrimaryVisualState_Selected_HasPriorityOverFocused()
    {
        var result = MGUI.Core.UI.MGElement.ResolvePrimaryVisualState(isEnabled: true, isSelected: true, hasKeyboardFocus: true, shouldDisplayFocusedState: true);

        Assert.Equal(MGUI.Core.UI.PrimaryVisualState.Selected, result);
    }

    [Theory]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Tab, false, MGUI.Core.UI.UINavigationAction.MoveNext)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Tab, true, MGUI.Core.UI.UINavigationAction.MovePrevious)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Enter, false, MGUI.Core.UI.UINavigationAction.Submit)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Space, false, MGUI.Core.UI.UINavigationAction.Submit)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Escape, false, MGUI.Core.UI.UINavigationAction.Cancel)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Up, false, MGUI.Core.UI.UINavigationAction.MoveUp)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Down, false, MGUI.Core.UI.UINavigationAction.MoveDown)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Left, false, MGUI.Core.UI.UINavigationAction.MoveLeft)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Right, false, MGUI.Core.UI.UINavigationAction.MoveRight)]
    public void TryMapNavigationAction_MapsExpectedKeys(Microsoft.Xna.Framework.Input.Keys key, bool isShiftDown, MGUI.Core.UI.UINavigationAction expected)
    {
        bool mapped = MGUI.Core.UI.MGDesktop.TryMapNavigationAction(key, isShiftDown, out var actual);

        Assert.True(mapped);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryMapNavigationAction_UnknownKey_ReturnsFalse()
    {
        bool mapped = MGUI.Core.UI.MGDesktop.TryMapNavigationAction(Microsoft.Xna.Framework.Input.Keys.A, false, out _);

        Assert.False(mapped);
    }

    [Fact]
    public void TryDispatchNavigationAction_HandlerConsumesAction_ReturnsTrue()
    {
        int callCount = 0;
        bool handled = MGUI.Core.UI.MGDesktop.TryDispatchNavigationAction(MGUI.Core.UI.UINavigationAction.MoveLeft, action =>
        {
            callCount++;
            return action == MGUI.Core.UI.UINavigationAction.MoveLeft;
        });

        Assert.True(handled);
        Assert.Equal(1, callCount);
    }

    [Fact]
    public void TryDispatchNavigationAction_HandlerRejectsAction_ReturnsFalse()
    {
        bool handled = MGUI.Core.UI.MGDesktop.TryDispatchNavigationAction(MGUI.Core.UI.UINavigationAction.MoveLeft, _ => false);

        Assert.False(handled);
    }

    [Fact]
    public void TryDispatchNavigationAction_NullHandler_ReturnsFalse()
    {
        bool handled = MGUI.Core.UI.MGDesktop.TryDispatchNavigationAction(MGUI.Core.UI.UINavigationAction.MoveLeft, null);

        Assert.False(handled);
    }

    [Theory]
    [InlineData(0, -1, true, -1)]
    [InlineData(3, -1, true, 0)]
    [InlineData(3, -1, false, 2)]
    [InlineData(3, 0, true, 1)]
    [InlineData(3, 2, true, 0)]
    [InlineData(3, 0, false, 2)]
    public void GetWrappedFocusIndex_ReturnsExpectedIndex(int count, int currentIndex, bool moveNext, int expected)
    {
        int actual = MGUI.Core.UI.MGDesktop.GetWrappedFocusIndex(count, currentIndex, moveNext);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void FindDirectionalNavigationTarget_FindsNearestDownCandidate()
    {
        var current = new Microsoft.Xna.Framework.Rectangle(100, 100, 20, 20);
        var candidates = new[]
        {
            new Microsoft.Xna.Framework.Rectangle(90, 160, 20, 20),
            new Microsoft.Xna.Framework.Rectangle(300, 300, 20, 20),
            new Microsoft.Xna.Framework.Rectangle(100, 10, 20, 20)
        };

        int actual = MGUI.Core.UI.MGDesktop.FindDirectionalNavigationTarget(current, candidates, MGUI.Core.UI.NavigationDirection.Down);

        Assert.Equal(0, actual);
    }

    [Fact]
    public void FindDirectionalNavigationTarget_IgnoresWrongDirection()
    {
        var current = new Microsoft.Xna.Framework.Rectangle(100, 100, 20, 20);
        var candidates = new[]
        {
            new Microsoft.Xna.Framework.Rectangle(100, 10, 20, 20),
            new Microsoft.Xna.Framework.Rectangle(10, 100, 20, 20)
        };

        int actual = MGUI.Core.UI.MGDesktop.FindDirectionalNavigationTarget(current, candidates, MGUI.Core.UI.NavigationDirection.Right);

        Assert.Equal(-1, actual);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void ToggleButton_GetNextCheckedState_TogglesValue(bool current, bool expected)
    {
        bool actual = MGUI.Core.UI.MGToggleButton.GetNextCheckedState(current);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(null, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, true, null)]
    [InlineData(null, true, false)]
    public void CheckBox_GetNextCheckedState_ReturnsExpectedValue(bool? current, bool isThreeState, bool? expected)
    {
        bool? actual = MGUI.Core.UI.MGCheckBox.GetNextCheckedState(current, isThreeState);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true, MGUI.Core.UI.ProgressButtonActionType.ResetAndResume, MGUI.Core.UI.ProgressButtonActionType.Toggle, MGUI.Core.UI.ProgressButtonActionType.ResetAndResume)]
    [InlineData(false, MGUI.Core.UI.ProgressButtonActionType.ResetAndResume, MGUI.Core.UI.ProgressButtonActionType.Toggle, MGUI.Core.UI.ProgressButtonActionType.Toggle)]
    public void ProgressButton_GetSubmitAction_ReturnsExpectedAction(bool isPaused, MGUI.Core.UI.ProgressButtonActionType pausedAction, MGUI.Core.UI.ProgressButtonActionType processingAction, MGUI.Core.UI.ProgressButtonActionType expected)
    {
        var actual = MGUI.Core.UI.MGProgressButton.GetSubmitAction(isPaused, pausedAction, processingAction);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Button_CreateNavigationReleasedEventArgs_CreatesSyntheticLeftClick()
    {
        var actual = MGUI.Core.UI.MGButton.CreateNavigationReleasedEventArgs();

        Assert.True(actual.IsLMB);
        Assert.Equal(Microsoft.Xna.Framework.Point.Zero, actual.Position);
        Assert.NotNull(actual.PressedArgs);
        Assert.True(actual.PressedArgs.IsLMB);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Spoiler_CanRevealFromSubmit_OnlyWhenHidden(bool isRevealed, bool expected)
    {
        bool actual = MGUI.Core.UI.MGSpoiler.CanRevealFromSubmit(isRevealed);

        Assert.Equal(expected, actual);
    }
}
