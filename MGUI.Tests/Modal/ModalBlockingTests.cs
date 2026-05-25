namespace MGUI.Tests.Modal;

using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Tests.Graph;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System.Reflection;

/// <summary>
/// Unit tests for the centralised modal-window input-blocking mechanism introduced in Task 3a
/// (pure logic, no MonoGame runtime required).
/// </summary>
public class ModalBlockingTests
{
    // ── Minimal stub replicating the _CanReceiveMouseInput computation ────────────

    /// <summary>
    /// Replicates the two-phase <c>_CanReceiveMouseInput</c> evaluation from
    /// <c>MGElement.Update()</c>:
    /// <list type="number">
    ///   <item>Cache block: <c>BaseCanReceiveInput &amp;&amp; parentCanMouse</c></item>
    ///   <item>Modal override (always applied): <c>&amp;= !SelfOrParentWindow.HasModalWindow</c></item>
    /// </list>
    /// </summary>
    private static bool ComputeCanReceiveMouse(
        bool isVisible,
        bool isEnabled,
        bool isHitTestVisible,
        bool parentCanMouse,
        bool hasModalWindow,
        bool isModalWindow = false)
    {
        // Phase 1 — identical to the cache-block calculation in MGElement.Update().
        bool baseCanReceive = isVisible && isEnabled && isHitTestVisible;
        bool effectiveParentCanMouse = isModalWindow || parentCanMouse;
        bool result         = baseCanReceive && effectiveParentCanMouse;

        // Phase 2 — modal override, applied every tick outside the cache block.
        result &= !hasModalWindow;

        return result;
    }

    // ── No modal ──────────────────────────────────────────────────────────────────

    [Fact]
    public void NoModal_NormalElement_CanReceiveMouse()
    {
        bool can = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: false);
        Assert.True(can);
    }

    [Fact]
    public void NoModal_DisabledElement_CannotReceiveMouse()
    {
        bool can = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: false, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: false);
        Assert.False(can);
    }

    [Fact]
    public void NoModal_HiddenElement_CannotReceiveMouse()
    {
        bool can = ComputeCanReceiveMouse(
            isVisible: false, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: false);
        Assert.False(can);
    }

    [Fact]
    public void NoModal_ParentBlockedMouse_CannotReceiveMouse()
    {
        bool can = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: false, hasModalWindow: false);
        Assert.False(can);
    }

    // ── Modal active on containing window ─────────────────────────────────────────

    [Fact]
    public void ModalActive_NormalElement_Blocked()
    {
        // Background-window element: SelfOrParentWindow.HasModalWindow == true → blocked.
        bool can = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: true);
        Assert.False(can, "element inside a window that has a modal must not receive mouse input");
    }

    [Fact]
    public void ModalActive_DisabledElement_StillBlocked()
    {
        // Both modal and disabled — still false.
        bool can = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: false, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: true);
        Assert.False(can);
    }

    // ── Modal window's own content ────────────────────────────────────────────────

    [Fact]
    public void ModalWindowSelf_HasModalWindowFalse_CanReceiveMouse()
    {
        // Elements inside the modal window:
        //   SelfOrParentWindow = the modal window itself,
        //   which has HasModalWindow == false (it IS the modal; no second modal exists).
        bool can = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: false); // the modal window's HasModalWindow
        Assert.True(can, "elements inside the modal window itself must still receive mouse input");
    }

    [Fact]
    public void ModalWindowRoot_ParentBlockedByOwnedModal_CanReceiveMouse()
    {
        bool ownerCan = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: true);

        bool modalCan = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: ownerCan, hasModalWindow: false, isModalWindow: true);

        Assert.False(ownerCan, "owner window must be blocked while its modal is active");
        Assert.True(modalCan, "modal window must not inherit the owner window's modal-blocked mouse state");
    }

    [Fact]
    public void ModalWindowChild_ReceivesMouseClick_WhenOwnerWindowIsBlocked()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow ownerWindow = new(desktop, 0, 0, 640, 360)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        MGCanvas ownerRoot = new(ownerWindow);
        MGButton ownerButton = new(ownerWindow)
        {
            PreferredWidth = 120,
            PreferredHeight = 40,
        };

        ownerWindow.SetContent(ownerRoot);
        desktop.Windows.Add(ownerWindow);
        using (ownerRoot.AllowChangingContentTemporarily())
        {
            ownerRoot.TryAddChild(ownerButton);
        }

        MGCanvas.SetLeft(ownerButton, 108);
        MGCanvas.SetTop(ownerButton, 108);

        MGWindow modalWindow = new(ownerWindow, 100, 100, 240, 120)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        MGCanvas modalRoot = new(modalWindow);
        MGButton modalButton = new(modalWindow)
        {
            PreferredWidth = 120,
            PreferredHeight = 40,
        };

        modalWindow.SetContent(modalRoot);
        using (modalRoot.AllowChangingContentTemporarily())
        {
            modalRoot.TryAddChild(modalButton);
        }

        MGCanvas.SetLeft(modalButton, 8);
        MGCanvas.SetTop(modalButton, 8);
        ownerWindow.PushModalWindow(modalWindow);

        AdvanceFrame(runtime, desktop, 0, new Point(1, 1));
        AdvanceFrame(runtime, desktop, 16, new Point(1, 1));

        int ownerClickCount = 0;
        int modalClickCount = 0;
        ownerButton.MouseHandler.LMBReleasedInside += (_, _) => ownerClickCount++;
        modalButton.MouseHandler.LMBReleasedInside += (_, _) => modalClickCount++;

        Point modalButtonCenter = modalButton.ActualLayoutBounds.Center;
        AdvanceFrame(runtime, desktop, 32, modalButtonCenter);
        AdvanceFrame(runtime, desktop, 48, modalButtonCenter, MouseButton.Left);
        AdvanceFrame(runtime, desktop, 64, modalButtonCenter);

        Assert.False(((IMouseHandlerHost)ownerButton).CanReceiveMouseInput());
        Assert.True(((IMouseHandlerHost)modalWindow).CanReceiveMouseInput());
        Assert.True(((IMouseHandlerHost)modalButton).CanReceiveMouseInput());
        Assert.Equal(0, ownerClickCount);
        Assert.Equal(1, modalClickCount);
    }

    // ── Propagation through parent ────────────────────────────────────────────────

    [Fact]
    public void ModalActive_PropagatesViaParentCanMouse()
    {
        // Simulate: parent element is inside a modal-blocked window.
        // Parent's _CanReceiveMouseInput = false  (blocked by modal OR disabled).
        // Child reads parentCanMouse = false → also blocked.
        bool parentBlocked = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: true); // parent is blocked by modal

        bool childCan = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: parentBlocked,  // child inherits parent's final value
            hasModalWindow: true);          // child is in same (blocked) window

        Assert.False(parentBlocked, "parent in blocked window should be blocked");
        Assert.False(childCan,      "child should also be blocked when parent is blocked");
    }

    [Fact]
    public void NoModal_ModalThenCleared_ElementRecoversInput()
    {
        // Simulates the tick when modal opens: result is false.
        bool blockedTick = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: true);

        // Simulates the tick after the modal closes: result is true again.
        bool recoveredTick = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: true,
            parentCanMouse: true, hasModalWindow: false);

        Assert.False(blockedTick,    "input blocked while modal is active");
        Assert.True(recoveredTick,   "input restored once modal is dismissed");
    }

    // ── Edge: HitTestVisible toggle interacts correctly with modal ────────────────

    [Fact]
    public void HitTestInvisible_WithModalActive_BothBlock()
    {
        bool can = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: false,
            parentCanMouse: true, hasModalWindow: true);
        Assert.False(can);
    }

    [Fact]
    public void HitTestInvisible_WithNoModal_StillBlocked()
    {
        bool can = ComputeCanReceiveMouse(
            isVisible: true, isEnabled: true, isHitTestVisible: false,
            parentCanMouse: true, hasModalWindow: false);
        Assert.False(can);
    }

    [Fact]
    public void MGWindow_ExposesStackableModalCollection()
    {
        PropertyInfo? modalWindowsProperty = typeof(MGWindow).GetProperty(nameof(MGWindow.ModalWindows), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(modalWindowsProperty);
        Assert.Equal(typeof(IReadOnlyList<MGWindow>), modalWindowsProperty!.PropertyType);
    }

    [Fact]
    public void MGDesktop_ExposesActiveModalWindowStack()
    {
        PropertyInfo? activeModalWindowsProperty = typeof(MGDesktop).GetProperty(nameof(MGDesktop.ActiveModalWindows), BindingFlags.Instance | BindingFlags.Public);

        Assert.NotNull(activeModalWindowsProperty);
        Assert.Equal(typeof(IReadOnlyList<MGWindow>), activeModalWindowsProperty!.PropertyType);
    }

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, Point position, MouseButton? pressedButton = null)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(position, pressedButton),
            new KeyboardState()));
        desktop.Update();
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton = null, int scrollWheel = 0)
        => new(
            position.X,
            position.Y,
            scrollWheel,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
}
