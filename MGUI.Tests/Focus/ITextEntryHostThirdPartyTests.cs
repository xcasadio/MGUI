using System;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Focus;

/// <summary>
/// Task 3 (Docs/input-window-activation-design.md, slice 3): <see cref="MGDesktop"/>'s text-entry protections (gameplay input
/// not captured while editing, focus cleaned up when the control becomes readonly) are now driven by the
/// public <see cref="ITextEntryHost"/> interface rather than an <c>is MGTextBox</c> type test. These tests
/// prove that migration by exercising a third-party double that is NOT an <see cref="MGTextBox"/> and
/// lives outside <c>MGUI.Core</c>'s text-editing implementation, mirroring the real-instance style used by
/// <c>RealMGUIInputContextRoutingTests</c>.
/// </summary>
public class ITextEntryHostThirdPartyTests
{
    [Fact]
    public void ThirdPartyHost_FocusedAndNotReadonly_BlocksGameplayInput()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        ThirdPartyTextEntryHost host = new(window);

        Assert.False(host.IsReadonly);
        Assert.False(desktop.ShouldCaptureGameplayInput());

        SetFocusedKeyboardHandler(desktop, host);
        Assert.Same(host, desktop.FocusedKeyboardHandler);

        Assert.True(desktop.ShouldCaptureGameplayInput());
    }

    [Fact]
    public void ThirdPartyHost_FocusedAndReadonly_DoesNotBlockGameplayInput()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        ThirdPartyTextEntryHost host = new(window) { IsReadonly = true };

        SetFocusedKeyboardHandler(desktop, host);
        Assert.Same(host, desktop.FocusedKeyboardHandler);

        // Same guard as MGTextBox: !IsReadonly is required for a focused ITextEntryHost to count as
        // "text entry focused", so a readonly third-party host must not block gameplay input.
        Assert.False(desktop.ShouldCaptureGameplayInput());
    }

    [Fact]
    public void ThirdPartyHost_BecomingReadonlyWhileFocused_ClearsFocusedKeyboardHandler()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        ThirdPartyTextEntryHost host = new(window);

        SetFocusedKeyboardHandler(desktop, host);
        Assert.Same(host, desktop.FocusedKeyboardHandler);

        // Mirrors MGDesktop.TextBox_ReadonlyChanged: the desktop subscribed to this third-party host's
        // ReadonlyChanged via the ITextEntryHost interface when it became FocusedKeyboardHandler, so
        // toggling IsReadonly here must clear focus exactly like it does for a real MGTextBox.
        host.IsReadonly = true;

        Assert.Null(desktop.FocusedKeyboardHandler);
    }

    [Fact]
    public void ThirdPartyHost_BecomingReadonlyWhileNotFocused_DoesNotAffectUnrelatedFocus()
    {
        MGDesktop desktop = CreateDesktopWithSingleWindow(out MGWindow window);
        ThirdPartyTextEntryHost host = new(window);
        MGButton otherFocusTarget = new(window) { IsFocusable = true };

        SetFocusedKeyboardHandler(desktop, otherFocusTarget);
        Assert.Same(otherFocusTarget, desktop.FocusedKeyboardHandler);

        // host was never focused, so the desktop never subscribed to its ReadonlyChanged: toggling it
        // must be a no-op for the desktop's focus state.
        host.IsReadonly = true;

        Assert.Same(otherFocusTarget, desktop.FocusedKeyboardHandler);
    }

    private sealed class ThirdPartyTextEntryHost : MGButton, ITextEntryHost
    {
        public ThirdPartyTextEntryHost(MGWindow window) : base(window) { }

        private bool _IsReadonly;
        public bool IsReadonly
        {
            get => _IsReadonly;
            set
            {
                if (_IsReadonly != value)
                {
                    _IsReadonly = value;
                    ReadonlyChanged?.Invoke(this, _IsReadonly);
                }
            }
        }

        public event EventHandler<bool> ReadonlyChanged;

        public bool ShouldPreserveTextEntryKey(Keys key) => false;
    }

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
