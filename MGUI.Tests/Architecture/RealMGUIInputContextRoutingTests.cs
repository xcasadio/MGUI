using System;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.InputRouting;
using MGUI.Shared.Input.Semantic;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Companion to <see cref="InputRoutingIntegrationTests"/>, which exercises a hand-written double
/// (<c>FakeMGUIContext</c>) that copies <see cref="MGUIInputContext"/>'s <c>TryHandle</c> logic line for
/// line. That double already drifted once from production (its <c>IsActive</c> is a hardcoded
/// <see langword="true"/> versus the real <c>Desktop != null</c>), so it cannot catch a future regression
/// in the real context's reason strings or branching. These tests instantiate the real
/// <see cref="MGUIInputContext"/> against a minimal, real <see cref="MGDesktop"/> built on the
/// <see cref="GraphTestRuntime"/> headless harness (the same pattern used by
/// <c>SemanticNavigationTextEntryPreservationTests.CreateDesktopWithSingleWindow</c>), and assert the
/// exact strings the production class returns.<para/>
/// The double-based tests in <see cref="InputRoutingIntegrationTests"/> are kept as-is (user decision):
/// these tests are additive, not a replacement.
/// </summary>
public class RealMGUIInputContextRoutingTests
{
    [Fact]
    public void RealContext_IsActive_ReflectsNonNullDesktop()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out _);
        MGUIInputContext mguiContext = new(desktop);

        Assert.True(mguiContext.IsActive);
        Assert.Throws<ArgumentNullException>(() => new MGUIInputContext(null));
    }

    [Fact]
    public void RealContext_UIAction_HandledByDesktop_IsReservedByMGUIContext_ViaRouter()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGButton button = new(window);
        window.SetContent(button);
        desktop.Update();

        Assert.Null(desktop.FocusedKeyboardHandler);

        InputRouter router = new();
        bool gameplayCalled = false;
        MGUIInputContext mguiContext = new(desktop, 100);
        router.RegisterContext(mguiContext);
        router.RegisterContext(new GameplayInputContext("Gameplay", _ =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputActionEvent actionEvent = new(InputAction.NavigateNext,
            new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero, Key: Keys.Tab));
        InputRouteDecision decision = router.Route(actionEvent);

        Assert.True(decision.IsHandled);
        Assert.Equal("MGUI.UI", decision.Result.ContextName);
        Assert.Equal("Handled by MGDesktop", decision.Result.Reason);
        Assert.False(gameplayCalled);
        Assert.Same(button, desktop.FocusedKeyboardHandler);
        Assert.True(mguiContext.IsActive);
    }

    [Fact]
    public void RealContext_ModalOverlayActive_BlocksGameplay_ViaRouter()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out _);

        MGButton overlayContent = new(desktop.OverlayHost.SelfOrParentWindow);
        MGOverlay overlay = desktop.OverlayHost.AddOverlay(overlayContent);
        overlay.IsOpen = true;
        Assert.True(overlay.IsOpen);
        Assert.True(desktop.OverlayHost.IsModal);
        Assert.Same(overlay, desktop.OverlayHost.ActiveOverlay);
        desktop.Update();

        AssertGameplayBlocked(desktop);
    }

    [Fact]
    public void RealContext_ContextMenuOpen_BlocksGameplay_ViaRouter()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out _);

        MGContextMenu menu = new(desktop, "Test Menu");
        Assert.True(desktop.TryOpenContextMenu(menu, new Point(50, 50)));
        Assert.Same(menu, desktop.ActiveContextMenu);
        desktop.Update();

        AssertGameplayBlocked(desktop);
    }

    [Fact]
    public void RealContext_FocusedNonReadonlyTextBox_BlocksGameplay_ViaRouter()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        MGTextBox textBox = new(window);
        window.SetContent(textBox);
        desktop.Update();

        Assert.False(textBox.IsReadonly);
        SetFocusedKeyboardHandler(desktop, textBox);
        Assert.Same(textBox, desktop.FocusedKeyboardHandler);

        AssertGameplayBlocked(desktop);
    }

    [Fact]
    public void RealContext_NoUICapture_GameplayPassesThrough_ViaRouterAndDirectCall()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out _);

        Assert.Null(desktop.OverlayHost.ActiveOverlay);
        Assert.Null(desktop.ActiveContextMenu);
        Assert.Null(desktop.FocusedKeyboardHandler);
        Assert.False(desktop.ShouldCaptureGameplayInput());

        InputRouter router = new();
        bool gameplayCalled = false;
        MGUIInputContext mguiContext = new(desktop, 100);
        router.RegisterContext(mguiContext);
        router.RegisterContext(new GameplayInputContext("Gameplay", _ =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputActionEvent actionEvent = CreateGameplayEvent();
        InputRouteDecision decision = router.Route(actionEvent);

        Assert.True(decision.IsHandled);
        Assert.Equal("Gameplay", decision.Result.ContextName);
        Assert.True(gameplayCalled);

        // InputRouter discards Ignored results (it only surfaces the winning context), so the
        // "Gameplay allowed to fall through" reason produced by MGUIInputContext itself can only be
        // observed by calling it directly.
        bool handledDirectly = mguiContext.TryHandle(actionEvent, out InputCaptureResult result);
        Assert.False(handledDirectly);
        Assert.False(result.IsHandled);
        Assert.Equal("MGUI.UI", result.ContextName);
        Assert.Equal("Gameplay allowed to fall through", result.Reason);
    }

    /// <summary>Shared assertion for the three <see cref="MGDesktop.ShouldCaptureGameplayInput"/> triggers:
    /// routes a gameplay action through a real <see cref="InputRouter"/> with a real
    /// <see cref="MGUIInputContext"/> plus a lower-priority <see cref="GameplayInputContext"/>, and asserts
    /// the gameplay context is never invoked.</summary>
    private static void AssertGameplayBlocked(MGDesktop desktop)
    {
        Assert.True(desktop.ShouldCaptureGameplayInput());

        InputRouter router = new();
        bool gameplayCalled = false;
        MGUIInputContext mguiContext = new(desktop, 100);
        router.RegisterContext(mguiContext);
        router.RegisterContext(new GameplayInputContext("Gameplay", _ =>
        {
            gameplayCalled = true;
            return true;
        }, 0));

        InputRouteDecision decision = router.Route(CreateGameplayEvent());

        Assert.True(decision.IsHandled);
        Assert.Equal("MGUI.UI", decision.Result.ContextName);
        Assert.Equal("Gameplay blocked by active UI capture state", decision.Result.Reason);
        Assert.False(gameplayCalled);
        Assert.True(mguiContext.IsActive);
    }

    private static InputActionEvent CreateGameplayEvent()
        => new(InputAction.GameplayPrimary, new(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero));

    private static MGDesktop CreateDesktopWithSingleWindow(out MGWindow window)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        window = new MGWindow(desktop, 0, 0, 400, 400) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();
        return desktop;
    }

    private static void SetFocusedKeyboardHandler(MGDesktop desktop, MGElement element)
    {
        MethodInfo setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
        setter.Invoke(desktop, new object[] { element });
    }
}
