using MGUI.Core.UI.InputRouting;
using MGUI.Shared.Input.Semantic;

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

    private static InputActionEvent CreateEvent(InputAction action)
        => new(action, new(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero));

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