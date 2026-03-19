using Microsoft.Xna.Framework.Input;

namespace MGUI.Core.UI
{
    internal static class FocusInputPolicy
    {
        internal static bool IsKeyboardInputEligible(bool canHandleKeyboardInput, bool canReceiveKeyboardInput, bool isBlockedByModalOrOverlay)
            => canHandleKeyboardInput && canReceiveKeyboardInput && !isBlockedByModalOrOverlay;

        internal static bool ShouldClearFocusedElement(bool canHandleKeyboardInput, bool canReceiveKeyboardInput, bool isBlockedByModalOrOverlay)
            => !IsKeyboardInputEligible(canHandleKeyboardInput, canReceiveKeyboardInput, isBlockedByModalOrOverlay);

        internal static bool TryGetNavigationAction(Keys key, bool isShiftDown, bool isTextEntryFocused, bool shouldPreserveTextEntryKey, out UINavigationAction action)
        {
            if (isTextEntryFocused && shouldPreserveTextEntryKey)
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
}