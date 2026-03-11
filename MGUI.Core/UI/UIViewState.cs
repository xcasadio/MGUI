using System.Collections.Generic;

namespace MGUI.Core.UI
{
    /// <summary>Stores mutable runtime state associated with a <see cref="UIView"/>.</summary>
    public class UIViewState
    {
        internal MGToolTip ActiveToolTip { get; set; }
        internal MGToolTip QueuedToolTip { get; set; }
        internal MGContextMenu ActiveContextMenu { get; set; }
        internal MGElement FocusedKeyboardHandler { get; set; }
        internal Dictionary<MGWindow, MGElement> WindowFocusHistory { get; } = new();
    }
}