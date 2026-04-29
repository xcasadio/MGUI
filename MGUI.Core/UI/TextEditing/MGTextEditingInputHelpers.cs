using System;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Core.UI.TextEditing
{
    internal static class MGTextEditingInputHelpers
    {
        public static bool ShouldPreserveTextEntryKey(Keys key, bool isReadonly, bool acceptsReturn, bool acceptsTab)
            => key switch
            {
                Keys.Left or Keys.Right or Keys.Up or Keys.Down or Keys.Home or Keys.End => true,
                Keys.Space => !isReadonly,
                Keys.Enter => !isReadonly && acceptsReturn,
                Keys.Tab => !isReadonly && acceptsTab,
                _ => false
            };

        public static bool ShouldProcessRepeatedKey(bool isHeldKeyRepeated, bool isControlDown, bool isPrintableKey, Keys key)
            => isHeldKeyRepeated && !isControlDown && (isPrintableKey || key is Keys.Back or Keys.Delete or Keys.Left or Keys.Right or Keys.Up or Keys.Down);

        public static bool ShouldHandleRepeatedKey(bool hasKeyboardFocus, bool isHeldKeyRepeated, bool isControlDown, bool isPrintableKey, Keys key,
            bool streamStartedAsControlShortcut = false)
            => hasKeyboardFocus && !streamStartedAsControlShortcut && ShouldProcessRepeatedKey(isHeldKeyRepeated, isControlDown, isPrintableKey, key);

        public static bool IsControlShortcutKey(Keys key)
            => key is Keys.X or Keys.C or Keys.V or Keys.Z or Keys.Y or Keys.A or Keys.D;

        public static int NormalizeEditableCaretIndex(int indexInOriginalText, int textLength)
            => Math.Clamp(indexInOriginalText, 0, Math.Max(0, textLength));
    }
}