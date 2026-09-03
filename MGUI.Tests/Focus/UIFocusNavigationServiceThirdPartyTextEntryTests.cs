using System;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Semantic;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Focus;

/// <summary>
/// Task 4 (input-activation-tasks.md): the last two <c>is MGTextBox</c> sites in
/// <see cref="MGUI.Core.UI.Navigation.UIFocusNavigationService"/> (the raw keyboard path's <c>isTextEntryFocused</c>
/// flag at the top of <c>TryDispatchNavigationAction(BaseKeyPressedEventArgs)</c>, and the semantic/raw-shared
/// <c>ShouldPreserveTextEntryKey(Keys?)</c> guard that used to call the <c>internal</c>
/// <c>MGTextBox.ShouldPreserveTextEntryKey</c> directly) are now driven by the public <see cref="ITextEntryHost"/>
/// interface. These tests prove both migrated sites with a third-party double that is NOT an <see cref="MGTextBox"/>
/// and lives outside <c>MGUI.Core</c>'s text-editing implementation, mirroring the real-instance style already used
/// by <c>ITextEntryHostThirdPartyTests</c> and <c>SemanticNavigationTextEntryPreservationTests</c>.
/// </summary>
public class UIFocusNavigationServiceThirdPartyTextEntryTests
{
    [Fact]
    public void RawNavigateLeft_WithFocusedThirdPartyHostReservingKey_PreservesKey_DoesNotDispatchNavigation()
    {
        Harness h = Harness.CreateHorizontal(out MGButton leftButton, out ThirdPartyTextEntryHost host, reservedKey: Keys.Left);

        // Sanity check: the button must actually be reachable by MoveLeft from the host (its layout center
        // strictly to the left of the host's), otherwise this test would pass even without the
        // ShouldPreserveTextEntryKey guard, since MoveLeft would have nowhere to move focus regardless.
        Assert.True(leftButton.ActualLayoutBounds.Center.X < host.ActualLayoutBounds.Center.X);

        h.SetFocusedKeyboardHandler(host);

        // Raw keyboard path: MGDesktop.HighPriorityKeyboardHandler.Pressed -> UIFocusNavigationService
        // .TryDispatchNavigationAction(BaseKeyPressedEventArgs), which computes isTextEntryFocused at :216.
        h.KeyFrame(Keys.Left);

        Assert.Same(host, h.Desktop.FocusedKeyboardHandler);
        Assert.NotSame(leftButton, h.Desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void RawNavigateTab_WithFocusedThirdPartyHostNotReservingKey_MovesFocusToNextElement()
    {
        Harness h = Harness.CreateVertical(out ThirdPartyTextEntryHost host, out MGButton nextButton, reservedKey: Keys.Left);

        h.SetFocusedKeyboardHandler(host);

        // Control case: Tab is not the reserved key for this double, so the raw path must navigate normally
        // instead of blanket-blocking every key while an ITextEntryHost is focused.
        h.KeyFrame(Keys.Tab);

        Assert.Same(nextButton, h.Desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void SemanticNavigateLeft_WithFocusedThirdPartyHostReservingKey_PreservesKey_DoesNotDispatchNavigation()
    {
        Harness h = Harness.CreateHorizontal(out MGButton leftButton, out ThirdPartyTextEntryHost host, reservedKey: Keys.Left);
        Assert.True(leftButton.ActualLayoutBounds.Center.X < host.ActualLayoutBounds.Center.X);

        h.SetFocusedKeyboardHandler(host);

        // Semantic path: MGDesktop.TryHandleInputAction -> UIFocusNavigationService.TryDispatchNavigationAction
        // (action, source, key), which calls the private ShouldPreserveTextEntryKey(Keys?) at :454 directly
        // (no isTextEntryFocused flag involved on this path).
        InputActionEvent actionEvent = new(InputAction.NavigateLeft,
            new InputActionContext(InputActionSource.Keyboard, InputActionPhase.Pressed, TimeSpan.Zero, Key: Keys.Left));

        bool handled = h.Desktop.TryHandleInputAction(actionEvent);
        h.Desktop.Update();

        Assert.False(handled);
        Assert.Same(host, h.Desktop.FocusedKeyboardHandler);
        Assert.NotSame(leftButton, h.Desktop.FocusedKeyboardHandler);
    }

    private sealed class ThirdPartyTextEntryHost : MGButton, ITextEntryHost
    {
        private readonly Keys _reservedKey;

        public ThirdPartyTextEntryHost(MGWindow window, Keys reservedKey) : base(window)
        {
            _reservedKey = reservedKey;
        }

        public bool IsReadonly => false;

        public event EventHandler<bool> ReadonlyChanged { add { } remove { } }

        public bool ShouldPreserveTextEntryKey(Keys key) => key == _reservedKey;
    }

    private sealed class Harness
    {
        public GraphTestRuntime Runtime { get; }
        public MGDesktop Desktop { get; }
        private int _frame;

        private Harness(GraphTestRuntime runtime, MGDesktop desktop)
        {
            Runtime = runtime;
            Desktop = desktop;
        }

        public static Harness CreateHorizontal(out MGButton leftButton, out ThirdPartyTextEntryHost host, Keys reservedKey)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 400, 200) { WindowStyle = WindowStyle.None };
            leftButton = new MGButton(window);
            ThirdPartyTextEntryHost hostLocal = new(window, reservedKey);
            host = hostLocal;
            MGStackPanel panel = new(window, Orientation.Horizontal);
            panel.TryAddChild(leftButton);
            panel.TryAddChild(hostLocal);
            window.SetContent(panel);
            desktop.Windows.Add(window);

            Harness h = new(runtime, desktop);
            h.KeyFrame();
            h.KeyFrame();
            return h;
        }

        public static Harness CreateVertical(out ThirdPartyTextEntryHost host, out MGButton nextButton, Keys reservedKey)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 400, 200) { WindowStyle = WindowStyle.None };
            ThirdPartyTextEntryHost hostLocal = new(window, reservedKey);
            host = hostLocal;
            nextButton = new MGButton(window);
            MGStackPanel panel = new(window, Orientation.Vertical);
            panel.TryAddChild(hostLocal);
            panel.TryAddChild(nextButton);
            window.SetContent(panel);
            desktop.Windows.Add(window);

            Harness h = new(runtime, desktop);
            h.KeyFrame();
            h.KeyFrame();
            return h;
        }

        public void SetFocusedKeyboardHandler(MGElement element)
        {
            MethodInfo setter = typeof(MGDesktop).GetProperty(nameof(MGDesktop.FocusedKeyboardHandler), BindingFlags.Public | BindingFlags.Instance)!.GetSetMethod(true)!;
            setter.Invoke(Desktop, new object[] { element });
        }

        public void KeyFrame(params Keys[] pressedKeys)
        {
            _frame++;
            MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * _frame), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState(pressedKeys)));
            Desktop.Update();
        }
    }
}
