namespace MGUI.Tests.Focus;

/// <summary>Unit tests for the IsFocusable auto-focus-on-click mechanism (pure logic, no MonoGame runtime).</summary>
public class FocusTests
{
    private sealed class ScopeNode
    {
        public ScopeNode? Parent { get; init; }
    }

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
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Left, false, false, false, true)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Home, true, false, false, true)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Space, false, false, false, true)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Space, true, false, false, false)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Enter, false, true, false, true)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Enter, false, false, false, false)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Enter, true, true, false, false)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Tab, false, false, true, true)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.Tab, false, false, false, false)]
    [InlineData(Microsoft.Xna.Framework.Input.Keys.PageDown, false, false, false, false)]
    public void TextBox_ShouldPreserveTextEntryKey_ReturnsExpectedValue(Microsoft.Xna.Framework.Input.Keys key, bool isReadonly, bool acceptsReturn, bool acceptsTab, bool expected)
    {
        bool actual = MGUI.Core.UI.MGTextBox.ShouldPreserveTextEntryKey(key, isReadonly, acceptsReturn, acceptsTab);

        Assert.Equal(expected, actual);
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

    [Theory]
    [InlineData(40f, 100f, 300f, 10f, 30f, 10f)]
    [InlineData(40f, 100f, 300f, 120f, 180f, 80f)]
    [InlineData(40f, 100f, 300f, 50f, 90f, 40f)]
    [InlineData(280f, 100f, 300f, 350f, 420f, 300f)]
    public void ScrollViewer_GetVisibleOffset_ReturnsExpectedOffset(float currentOffset, float viewportSize, float maxOffset, float elementStart, float elementEnd, float expected)
    {
        float actual = MGUI.Core.UI.MGScrollViewer.GetVisibleOffset(currentOffset, viewportSize, maxOffset, elementStart, elementEnd);

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

    [Fact]
    public void GetActiveFocusScopeRoot_ReturnsLastScope()
    {
        string? actual = MGUI.Core.UI.MGDesktop.GetActiveFocusScopeRoot(new[] { "window", "dropdown", "submenu" });

        Assert.Equal("submenu", actual);
    }

    [Fact]
    public void ResolveNavigationRoot_PrefersActiveScope()
    {
        string? actual = MGUI.Core.UI.MGDesktop.ResolveNavigationRoot("submenu", "window", "top-window");

        Assert.Equal("submenu", actual);
    }

    [Fact]
    public void ResolveNavigationRoot_FallsBackToFocusedWindowThenTopWindow()
    {
        string? actualWithFocusedWindow = MGUI.Core.UI.MGDesktop.ResolveNavigationRoot<string>(null!, "window", "top-window");
        string? actualWithTopWindow = MGUI.Core.UI.MGDesktop.ResolveNavigationRoot<string>(null!, null!, "top-window");

        Assert.Equal("window", actualWithFocusedWindow);
        Assert.Equal("top-window", actualWithTopWindow);
    }

    [Fact]
    public void IsWithinFocusScope_ReturnsTrueForDescendant()
    {
        var scopeRoot = new ScopeNode();
        var descendant = new ScopeNode { Parent = new ScopeNode { Parent = scopeRoot } };

        bool actual = MGUI.Core.UI.MGDesktop.IsWithinFocusScope(scopeRoot, descendant, node => node.Parent!);

        Assert.True(actual);
    }

    [Fact]
    public void IsWithinFocusScope_ReturnsFalseForDifferentBranch()
    {
        var scopeRoot = new ScopeNode();
        var otherRoot = new ScopeNode();
        var descendant = new ScopeNode { Parent = otherRoot };

        bool actual = MGUI.Core.UI.MGDesktop.IsWithinFocusScope(scopeRoot, descendant, node => node.Parent!);

        Assert.False(actual);
    }

    [Theory]
    [InlineData(0f, 10f, false, null, 1f)]
    [InlineData(0f, 10f, true, 2f, 2f)]
    [InlineData(3f, 3f, false, null, 1f)]
    public void Slider_GetNavigationStep_ReturnsExpectedStep(float minimum, float maximum, bool useDiscreteValues, float? discreteInterval, float expected)
    {
        float actual = MGUI.Core.UI.MGSlider.GetNavigationStep(minimum, maximum, useDiscreteValues, discreteInterval);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(true, 0.5f, 0.5f)]
    [InlineData(false, null, 1f)]
    public void RatingControl_GetNavigationStep_ReturnsExpectedStep(bool useDiscreteValues, float? discreteInterval, float expected)
    {
        float actual = MGUI.Core.UI.MGRatingControl.GetNavigationStep(useDiscreteValues, discreteInterval);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(0, 4, 9, MGUI.Core.UI.NavigationDirection.Right, 1)]
    [InlineData(1, 4, 9, MGUI.Core.UI.NavigationDirection.Down, 5)]
    [InlineData(8, 4, 9, MGUI.Core.UI.NavigationDirection.Right, 8)]
    [InlineData(8, 4, 9, MGUI.Core.UI.NavigationDirection.Up, 4)]
    public void GridColorPicker_GetAdjacentColorIndex_ReturnsExpectedIndex(int currentIndex, int columns, int colorCount, MGUI.Core.UI.NavigationDirection direction, int expected)
    {
        int? actual = MGUI.Core.UI.MGGridColorPicker.GetAdjacentColorIndex(currentIndex, columns, colorCount, direction);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(2, 10, MGUI.Core.UI.UINavigationAction.MoveUp, 1)]
    [InlineData(2, 10, MGUI.Core.UI.UINavigationAction.MoveDown, 3)]
    [InlineData(2, 10, MGUI.Core.UI.UINavigationAction.Home, 0)]
    [InlineData(2, 10, MGUI.Core.UI.UINavigationAction.End, 9)]
    public void ComboBox_GetNextNavigationIndex_ReturnsExpectedIndex(int currentIndex, int itemCount, MGUI.Core.UI.UINavigationAction action, int expected)
    {
        int actual = MGUI.Core.UI.MGComboBox<string>.GetNextNavigationIndex(currentIndex, itemCount, action);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-1, 5, MGUI.Core.UI.UINavigationAction.MoveDown, 1)]
    [InlineData(2, 5, MGUI.Core.UI.UINavigationAction.MoveUp, 1)]
    [InlineData(2, 5, MGUI.Core.UI.UINavigationAction.End, 4)]
    public void ListBox_GetNextNavigationIndex_ReturnsExpectedIndex(int currentIndex, int count, MGUI.Core.UI.UINavigationAction action, int expected)
    {
        int actual = MGUI.Core.UI.MGListBox<string>.GetNextNavigationIndex(currentIndex, count, action);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-1, 5, MGUI.Core.UI.UINavigationAction.MoveDown, 1)]
    [InlineData(3, 5, MGUI.Core.UI.UINavigationAction.PageUp, 0)]
    [InlineData(1, 5, MGUI.Core.UI.UINavigationAction.End, 4)]
    public void ListView_GetNextNavigationIndex_ReturnsExpectedIndex(int currentIndex, int count, MGUI.Core.UI.UINavigationAction action, int expected)
    {
        int actual = MGUI.Core.UI.MGListView<string>.GetNextNavigationIndex(currentIndex, count, action);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-1, 5, MGUI.Core.UI.UINavigationAction.MoveDown, 1)]
    [InlineData(2, 5, MGUI.Core.UI.UINavigationAction.MoveUp, 1)]
    [InlineData(2, 5, MGUI.Core.UI.UINavigationAction.Home, 0)]
    [InlineData(2, 5, MGUI.Core.UI.UINavigationAction.End, 4)]
    public void TreeView_GetNextVisibleNavigationIndex_ReturnsExpectedIndex(int currentIndex, int count, MGUI.Core.UI.UINavigationAction action, int expected)
    {
        int actual = MGUI.Core.UI.MGTreeView.GetNextVisibleNavigationIndex(currentIndex, count, action);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-1, 4, MGUI.Core.UI.UINavigationAction.MoveRight, 1)]
    [InlineData(2, 4, MGUI.Core.UI.UINavigationAction.MoveLeft, 1)]
    [InlineData(2, 4, MGUI.Core.UI.UINavigationAction.End, 3)]
    public void MenuBar_GetAdjacentItemIndex_ReturnsExpectedIndex(int currentIndex, int count, MGUI.Core.UI.UINavigationAction action, int expected)
    {
        int actual = MGUI.Core.UI.MGMenuBar.GetAdjacentItemIndex(currentIndex, count, action);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-1, 5, MGUI.Core.UI.UINavigationAction.MoveRight, 1)]
    [InlineData(2, 5, MGUI.Core.UI.UINavigationAction.MoveLeft, 1)]
    [InlineData(2, 5, MGUI.Core.UI.UINavigationAction.End, 4)]
    [InlineData(2, 5, MGUI.Core.UI.UINavigationAction.ShoulderNext, 3)]
    public void TabControl_GetAdjacentTabIndex_ReturnsExpectedIndex(int currentIndex, int count, MGUI.Core.UI.UINavigationAction action, int expected)
    {
        int actual = MGUI.Core.UI.MGTabControl.GetAdjacentTabIndex(currentIndex, count, action);

        Assert.Equal(expected, actual);
    }
}
