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
}
