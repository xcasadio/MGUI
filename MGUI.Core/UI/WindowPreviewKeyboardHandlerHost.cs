using MGUI.Shared.Input.Keyboard;

namespace MGUI.Core.UI;

/// <summary>Owner used by <see cref="MGWindow.PreviewKeyboardHandler"/>.<para/>
/// <see cref="MGElement"/> overrides <see cref="IKeyboardHandlerHost.HasKeyboardFocus"/> to require actual desktop
/// keyboard focus (<c>GetDesktop().FocusedKeyboardHandler == this</c>), which is why an <see cref="MGWindow"/> can
/// never observe its own <see cref="MGWindow.WindowKeyboardHandler"/>: a window itself never holds keyboard focus.<para/>
/// This type deliberately does NOT implement <see cref="MGElement"/> and does NOT override
/// <see cref="IKeyboardHandlerHost.HasKeyboardFocus"/>, so it keeps the interface's default implementation
/// (always returns true) - the same trick <see cref="MGDesktop"/> uses for its own
/// <see cref="MGDesktop.HighPriorityKeyboardHandler"/>. This lets a per-window <see cref="KeyboardHandler"/> see
/// every key of the tick regardless of which descendant (if any) currently holds keyboard focus, without touching
/// the global focus invariant.</summary>
internal sealed class WindowPreviewKeyboardHandlerHost : IKeyboardHandlerHost { }