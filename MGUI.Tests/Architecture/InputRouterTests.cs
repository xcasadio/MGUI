using MGUI.Shared.Input.Semantic;

namespace MGUI.Tests.Architecture;

public class InputRouterTests
{
    [Fact]
    public void Route_Uses_Highest_Priority_Active_Context_First()
    {
        InputRouter router = new();
        List<string> calls = new();

        router.RegisterContext(new TestContext("Gameplay", 10, true, calls, (actionEvent, sink) =>
        {
            sink.Add("Gameplay");
            return false;
        }));
        router.RegisterContext(new TestContext("UI", 100, true, calls, (actionEvent, sink) =>
        {
            sink.Add("UI");
            return true;
        }));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.NavigateDown));

        Assert.True(decision.IsHandled);
        Assert.Equal("UI", decision.Result.ContextName);
        Assert.Equal(new[] { "UI" }, calls);
    }

    [Fact]
    public void Route_Preserves_Registration_Order_For_Equal_Priority_Contexts()
    {
        InputRouter router = new();
        List<string> calls = new();

        router.RegisterContext(new TestContext("First", 50, true, calls, (actionEvent, sink) =>
        {
            sink.Add("First");
            return false;
        }));
        router.RegisterContext(new TestContext("Second", 50, true, calls, (actionEvent, sink) =>
        {
            sink.Add("Second");
            return true;
        }));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.NavigateUp));

        Assert.True(decision.IsHandled);
        Assert.Equal("Second", decision.Result.ContextName);
        Assert.Equal(new[] { "First", "Second" }, calls);
    }

    [Fact]
    public void Route_Skips_Inactive_Contexts()
    {
        InputRouter router = new();

        router.RegisterContext(new TestContext("InactiveUI", 100, false, null, (actionEvent, sink) => true));
        router.RegisterContext(new TestContext("Gameplay", 10, true, null, (actionEvent, sink) => true));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.GameplayPrimary));

        Assert.True(decision.IsHandled);
        Assert.Equal("Gameplay", decision.Result.ContextName);
    }

    [Fact]
    public void Route_Returns_Unhandled_When_No_Context_Consumes_Action()
    {
        InputRouter router = new();
        router.RegisterContext(new TestContext("UI", 100, true, null, (actionEvent, sink) => false));

        InputRouteDecision decision = router.Route(CreateEvent(InputAction.GameplaySecondary));

        Assert.False(decision.IsHandled);
        Assert.Equal(string.Empty, decision.Result.ContextName);
    }

    private static InputActionEvent CreateEvent(InputAction action)
        => new(action, new(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero));

    private sealed class TestContext : IInputContext
    {
        private readonly Func<InputActionEvent, List<string>, bool> _handler;
        private readonly List<string> _sink;

        public string Name { get; }
        public int Priority { get; }
        public bool IsActive { get; }

        public TestContext(string name, int priority, bool isActive, List<string>? sink, Func<InputActionEvent, List<string>, bool> handler)
        {
            Name = name;
            Priority = priority;
            IsActive = isActive;
            _handler = handler;
            _sink = sink ?? new();
        }

        public bool TryHandle(InputActionEvent actionEvent, out InputCaptureResult result)
        {
            bool handled = _handler(actionEvent, _sink);
            result = handled
                ? InputCaptureResult.Handled(Name, "Handled by test context")
                : InputCaptureResult.Ignored(Name, "Ignored by test context");
            return handled;
        }
    }
}