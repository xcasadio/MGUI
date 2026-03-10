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
}
