using Microsoft.Xna.Framework.Input;

namespace MGUI.Core.UI;

internal static class FocusInputPolicy
{
    internal static bool IsKeyboardInputEligible(bool canHandleKeyboardInput, bool canReceiveKeyboardInput, bool isBlockedByModalOrOverlay)
        => canHandleKeyboardInput && canReceiveKeyboardInput && !isBlockedByModalOrOverlay;

    internal static bool ShouldClearFocusedElement(bool canHandleKeyboardInput, bool canReceiveKeyboardInput, bool isBlockedByModalOrOverlay)
        => !IsKeyboardInputEligible(canHandleKeyboardInput, canReceiveKeyboardInput, isBlockedByModalOrOverlay);

    /// <summary>Ctrl+Tab (and Ctrl+Shift+Tab, which the Shift flag turns into <see cref="UINavigationAction.MovePrevious"/>)
    /// navigates even when the focused text entry host reserves Tab for indentation. This is the WPF convention, and it
    /// is what keeps a control that legitimately sets <c>AcceptsTab = true</c> - <see cref="MGRichTextBox"/>, the
    /// <see cref="MGGraphControls"/> comment box, the editable markup box of <see cref="MGXAMLDesigner"/> - from being a
    /// keyboard trap now that text entry hosts are navigation targets.<para/>
    /// ONLY Tab is released this way. Ctrl with any other reserved key keeps its text-editing meaning, so Ctrl+Left and
    /// Ctrl+Right still move the caret by word rather than moving focus.
    /// See Docs/decisions/0014-keyboard-focus-navigation-single-rule.md.</summary>
    internal static bool IsTextEntryNavigationEscape(Keys key, bool isControlDown)
        => isControlDown && key == Keys.Tab;

    internal static bool TryGetNavigationAction(Keys key, bool isShiftDown, bool isControlDown, bool isTextEntryFocused, bool shouldPreserveTextEntryKey, out UINavigationAction action)
    {
        if (isTextEntryFocused && shouldPreserveTextEntryKey && !IsTextEntryNavigationEscape(key, isControlDown))
        {
            action = default;
            return false;
        }

        return MGDesktop.TryMapNavigationAction(key, isShiftDown, out action);
    }

    internal static bool ShouldProcessWindowInputs(bool isOverlayWindow, bool hasActiveOverlay, bool overlayIsModal)
        => (isOverlayWindow && hasActiveOverlay) || (!isOverlayWindow && (!hasActiveOverlay || !overlayIsModal));

    internal static bool ShouldInvokeCompositeKeyboardHandler(bool hasKeyboardFocus, bool isHandled, bool invokeEvenIfHandled)
        => hasKeyboardFocus && (invokeEvenIfHandled || !isHandled);
}