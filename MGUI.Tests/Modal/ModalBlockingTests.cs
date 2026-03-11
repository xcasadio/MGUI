namespace MGUI.Tests.Modal;

using MGUI.Core.UI;
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
        bool hasModalWindow)
    {
        // Phase 1 — identical to the cache-block calculation in MGElement.Update().
        bool baseCanReceive = isVisible && isEnabled && isHitTestVisible;
        bool result         = baseCanReceive && parentCanMouse;

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
}
