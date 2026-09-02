using MGUI.Core.UI.InputRouting;
using MGUI.Shared.Input.Semantic;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Architecture;

public class InputRoutingIntegrationTests
{
    [Fact]
    public void GameplayContext_Acts_As_Fallback_When_UI_Does_Not_Capture()
    {
        InputRouter router = new();

        router.RegisterContext(new TestContext("UI", 100, actionEvent => false));
        router.RegisterContext(new GameplayInputContext("Gameplay", actionEvent => actionEvent.Action.IsGameplayAction(), 0));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.GameplayPrimary));

        Assert.True(decision.IsHandled);
        Assert.Equal("Gameplay", decision.Result.ContextName);
    }

    [Fact]
    public void GameplayContext_Does_Not_Run_When_UI_Context_Captures_Action()
    {
        InputRouter router = new();
        bool gameplayCalled = false;

        router.RegisterContext(new TestContext("UI", 100, actionEvent => actionEvent.Action == InputAction.GameplayPrimary));
        router.RegisterContext(new GameplayInputContext("Gameplay", actionEvent =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.GameplayPrimary));

        Assert.True(decision.IsHandled);
        Assert.Equal("UI", decision.Result.ContextName);
        Assert.False(gameplayCalled);
    }

    [Fact]
    public void ModalOverlayActive_Blocks_Gameplay_Action_Via_ShouldCaptureGameplayInput()
    {
        InputRouter router = new();
        bool gameplayCalled = false;

        router.RegisterContext(new FakeMGUIContext(
            tryHandleInputAction: _ => false,
            shouldCaptureGameplayInput: () => true));
        router.RegisterContext(new GameplayInputContext("Gameplay", actionEvent =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.GameplayPrimary));

        Assert.True(decision.IsHandled);
        Assert.Equal("MGUI.UI", decision.Result.ContextName);
        Assert.False(string.IsNullOrWhiteSpace(decision.Result.Reason));
        Assert.False(gameplayCalled);
    }

    [Fact]
    public void ContextMenuOpen_Blocks_Gameplay_Action_Via_ShouldCaptureGameplayInput()
    {
        InputRouter router = new();
        bool gameplayCalled = false;
        bool contextMenuOpen = true;

        router.RegisterContext(new FakeMGUIContext(
            tryHandleInputAction: _ => false,
            shouldCaptureGameplayInput: () => contextMenuOpen));
        router.RegisterContext(new GameplayInputContext("Gameplay", actionEvent =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.GameplaySecondary));

        Assert.True(decision.IsHandled);
        Assert.Equal("MGUI.UI", decision.Result.ContextName);
        Assert.Equal("Gameplay blocked by active UI capture state", decision.Result.Reason);
        Assert.False(gameplayCalled);
    }

    [Fact]
    public void NonModalNavigableMenu_Routes_Navigate_Action_To_UI_Never_To_Gameplay()
    {
        InputRouter router = new();
        bool gameplayCalled = false;
        bool navigationDispatched = false;

        // Non-modal: ShouldCaptureGameplayInput() is false (no modal overlay, no context menu), yet a
        // Navigate* action must still never reach gameplay, because it is a UI action: MGUIInputContext
        // intercepts every UI action unconditionally, regardless of the modal-capture flag.
        router.RegisterContext(new FakeMGUIContext(
            tryHandleInputAction: _ =>
            {
                navigationDispatched = true;
                return true;
            },
            shouldCaptureGameplayInput: () => false));
        router.RegisterContext(new GameplayInputContext("Gameplay", actionEvent =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.NavigateNext));

        Assert.True(decision.IsHandled);
        Assert.Equal("MGUI.UI", decision.Result.ContextName);
        Assert.Equal("Handled by MGDesktop", decision.Result.Reason);
        Assert.True(navigationDispatched);
        Assert.False(gameplayCalled);
    }

    [Fact]
    public void FocusedTextEntry_PreservesKey_NeitherNavigationNorGameplayReceivesIt()
    {
        InputRouter router = new();
        bool gameplayCalled = false;
        bool navigationGuardEvaluated = false;

        // Mirrors task 3: MGDesktop.TryHandleInputAction returns false when the text-entry preservation
        // guard triggers (key belongs to the focused MGTextBox), but MGUIInputContext.TryHandle still
        // returns true unconditionally for UI actions, so the action stays reserved by the UI and never
        // falls through to navigation dispatch succeeding, nor to the gameplay context below.
        router.RegisterContext(new FakeMGUIContext(
            tryHandleInputAction: _ =>
            {
                navigationGuardEvaluated = true;
                return false;
            },
            shouldCaptureGameplayInput: () => true));
        router.RegisterContext(new GameplayInputContext("Gameplay", actionEvent =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputActionEvent actionEvent = new(InputAction.NavigateLeft,
            new(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero, Key: Keys.Left));
        InputRouteDecision decision = router.Route(actionEvent);

        Assert.True(decision.IsHandled);
        Assert.Equal("MGUI.UI", decision.Result.ContextName);
        Assert.Equal("Reserved for UI routing", decision.Result.Reason);
        Assert.True(navigationGuardEvaluated);
        Assert.False(gameplayCalled);
    }

    [Fact]
    public void HudContext_UnderPointer_ConsumesAction_GameplayNeverReceivesIt()
    {
        InputRouter router = new();
        bool hudTargeted = true;
        bool gameplayCalled = false;

        router.RegisterContext(new FakeHudContext(actionEvent => true, priority: 50, isActive: () => hudTargeted));
        router.RegisterContext(new GameplayInputContext("Gameplay", actionEvent =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.GameplayPrimary));

        Assert.True(decision.IsHandled);
        Assert.Equal("Test.HUD", decision.Result.ContextName);
        Assert.False(string.IsNullOrWhiteSpace(decision.Result.Reason));
        Assert.False(gameplayCalled);
    }

    [Fact]
    public void HudContext_NotTargeted_ActionFallsThroughToGameplay()
    {
        InputRouter router = new();
        bool hudTargeted = false;
        bool gameplayCalled = false;

        router.RegisterContext(new FakeHudContext(actionEvent => true, priority: 50, isActive: () => hudTargeted));
        router.RegisterContext(new GameplayInputContext("Gameplay", actionEvent =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.GameplayPrimary));

        Assert.True(decision.IsHandled);
        Assert.Equal("Gameplay", decision.Result.ContextName);
        Assert.True(gameplayCalled);
    }

    private static InputActionEvent CreateEvent(InputAction action)
        => new(action, new(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero));

    /// <summary>Reproduces <see cref="MGUIInputContext"/>'s TryHandle logic with plain delegates instead
    /// of a real <see cref="MGUI.Core.UI.MGDesktop"/>, so end-to-end semantic routing scenarios can be
    /// exercised as a pure logic test without a full UI runtime.</summary>
    private sealed class FakeMGUIContext : IInputContext
    {
        private readonly Func<InputActionEvent, bool> _tryHandleInputAction;
        private readonly Func<bool> _shouldCaptureGameplayInput;

        public string Name => "MGUI.UI";
        public int Priority { get; }
        public bool IsActive => true;

        public FakeMGUIContext(Func<InputActionEvent, bool> tryHandleInputAction, Func<bool> shouldCaptureGameplayInput, int priority = 100)
        {
            _tryHandleInputAction = tryHandleInputAction ?? throw new ArgumentNullException(nameof(tryHandleInputAction));
            _shouldCaptureGameplayInput = shouldCaptureGameplayInput ?? throw new ArgumentNullException(nameof(shouldCaptureGameplayInput));
            Priority = priority;
        }

        public bool TryHandle(InputActionEvent actionEvent, out InputCaptureResult result)
        {
            if (actionEvent.Action.IsUIAction())
            {
                bool handled = _tryHandleInputAction(actionEvent);
                result = InputCaptureResult.Handled(Name, handled ? "Handled by MGDesktop" : "Reserved for UI routing");
                return true;
            }

            if (_shouldCaptureGameplayInput())
            {
                result = InputCaptureResult.Handled(Name, "Gameplay blocked by active UI capture state");
                return true;
            }

            result = InputCaptureResult.Ignored(Name, "Gameplay allowed to fall through");
            return false;
        }
    }

    /// <summary>Reproduces <c>MiniGameHudInputContext</c>'s selective-activation shape (see
    /// <c>MGUI.MiniGame/MiniGameHudInputContext.cs</c>): a non-modal HUD context that only consumes
    /// actions while <c>isActive</c> reports the pointer/target is over an actual HUD widget.</summary>
    private sealed class FakeHudContext : IInputContext
    {
        private readonly Func<InputActionEvent, bool> _tryHandle;
        private readonly Func<bool> _isActive;

        public string Name => "Test.HUD";
        public int Priority { get; }
        public bool IsActive => _isActive();

        public FakeHudContext(Func<InputActionEvent, bool> tryHandle, int priority, Func<bool> isActive)
        {
            _tryHandle = tryHandle ?? throw new ArgumentNullException(nameof(tryHandle));
            _isActive = isActive ?? throw new ArgumentNullException(nameof(isActive));
            Priority = priority;
        }

        public bool TryHandle(InputActionEvent actionEvent, out InputCaptureResult result)
        {
            bool handled = _tryHandle(actionEvent);
            result = handled
                ? InputCaptureResult.Handled(Name, "Handled by test HUD context")
                : InputCaptureResult.Ignored(Name, "HUD context ignored the action");
            return handled;
        }
    }

    private sealed class TestContext : IInputContext
    {
        private readonly Func<InputActionEvent, bool> _tryHandle;

        public string Name { get; }
        public int Priority { get; }
        public bool IsActive => true;

        public TestContext(string name, int priority, Func<InputActionEvent, bool> tryHandle)
        {
            Name = name;
            Priority = priority;
            _tryHandle = tryHandle;
        }

        public bool TryHandle(InputActionEvent actionEvent, out InputCaptureResult result)
        {
            bool handled = _tryHandle(actionEvent);
            result = handled
                ? InputCaptureResult.Handled(Name, "Handled by integration test UI context")
                : InputCaptureResult.Ignored(Name, "Ignored by integration test UI context");
            return handled;
        }
    }
}