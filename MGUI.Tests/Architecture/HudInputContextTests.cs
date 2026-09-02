using MGUI.Core.UI.InputRouting;
using MGUI.Shared.Input.Semantic;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Pure policy tests for the reusable <see cref="HudInputContext"/> (task 7 of
/// <c>Docs/Tasks/input-tasks.md</c>): an action that actually targets a HUD widget is consumed,
/// an action that does not (e.g. movement) is never consumed even while the HUD is active, and an
/// inactive HUD falls through completely without its handler ever being invoked. No runtime desktop
/// is instantiated, mirroring the "doubles simples, pas de runtime complet" approach already used by
/// <c>InputRoutingIntegrationTests.cs</c> (task 6).
/// </summary>
public class HudInputContextTests
{
    private static readonly Rectangle HudWidgetBounds = new(0, 0, 100, 100);

    [Fact]
    public void TargetedAction_IsConsumed_ByHudContext()
    {
        InputRouter router = new();
        router.RegisterContext(new HudInputContext("Test.HUD", TryHandleIfInsideHudBounds, isActive: () => true, priority: 50));
        router.RegisterContext(new GameplayInputContext("Gameplay", _ => true, priority: 0));

        InputActionEvent actionEvent = CreateEvent(InputAction.GameplayPrimary, pointerPosition: new Point(10, 10));
        InputRouteDecision decision = router.Route(actionEvent);

        Assert.True(decision.IsHandled);
        Assert.Equal("Test.HUD", decision.Result.ContextName);
        Assert.False(string.IsNullOrWhiteSpace(decision.Result.Reason));
    }

    [Fact]
    public void MovementAction_NeverConsumed_EvenWhileHudActive_FallsThroughToGameplay()
    {
        InputRouter router = new();
        bool gameplayCalled = false;

        router.RegisterContext(new HudInputContext("Test.HUD", TryHandleIfInsideHudBounds, isActive: () => true, priority: 50));
        router.RegisterContext(new GameplayInputContext("Gameplay", _ =>
        {
            gameplayCalled = true;
            return true;
        }, priority: 0));

        // Pointer outside the HUD widget's bounds represents ambient movement, not a HUD target.
        InputActionEvent actionEvent = CreateEvent(InputAction.GameplayPrimary, pointerPosition: new Point(500, 500));
        InputRouteDecision decision = router.Route(actionEvent);

        Assert.True(decision.IsHandled);
        Assert.Equal("Gameplay", decision.Result.ContextName);
        Assert.True(gameplayCalled);
    }

    [Fact]
    public void InactiveHud_FallsThroughCompletely_HandlerNeverInvoked()
    {
        InputRouter router = new();
        bool hudHandlerCalled = false;
        bool gameplayCalled = false;

        router.RegisterContext(new HudInputContext("Test.HUD", actionEvent =>
        {
            hudHandlerCalled = true;
            return true;
        }, isActive: () => false, priority: 50));
        router.RegisterContext(new GameplayInputContext("Gameplay", _ =>
        {
            gameplayCalled = true;
            return true;
        }, priority: 0));

        InputActionEvent actionEvent = CreateEvent(InputAction.GameplayPrimary, pointerPosition: new Point(10, 10));
        InputRouteDecision decision = router.Route(actionEvent);

        Assert.True(decision.IsHandled);
        Assert.Equal("Gameplay", decision.Result.ContextName);
        Assert.True(gameplayCalled);
        Assert.False(hudHandlerCalled);
    }

    [Fact]
    public void Constructor_RequiresExplicitActivationPredicate()
    {
        // Unlike a default-always-active context, HudInputContext requires the selective
        // activation predicate the plan mandates ("predicat d'activation selectif obligatoire").
        Assert.Throws<ArgumentNullException>(() => new HudInputContext("Test.HUD", _ => true, isActive: null!));
    }

    private static bool TryHandleIfInsideHudBounds(InputActionEvent actionEvent)
        => actionEvent.Context.PointerPosition.HasValue && HudWidgetBounds.Contains(actionEvent.Context.PointerPosition.Value);

    private static InputActionEvent CreateEvent(InputAction action, Point? pointerPosition)
        => new(action, new InputActionContext(InputActionSource.Mouse, InputActionPhase.Pressed, TimeSpan.Zero, PointerPosition: pointerPosition));
}
