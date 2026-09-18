using Microsoft.Xna.Framework;

namespace MGUI.Core.UI;

/// <summary>Stores mutable runtime state associated with a <see cref="UIView"/>.</summary>
public class UIViewState
{
    internal MGToolTip ActiveToolTip { get; set; }
    internal MGToolTip QueuedToolTip { get; set; }

    /// <summary>Y8: the tooltip whose exit is playing after it stopped being <see cref="ActiveToolTip"/>, kept drawn (at
    /// <see cref="ExitingToolTipDrawPosition"/>, since a tooltip has no position of its own) until the run ends, then cleared -- never fed
    /// input (never updated, only drawn) and never occluding anything (<see cref="MGWindow.OccludesUnscaledPosition"/> is already false while
    /// an exit is playing, <see cref="MGElement.IsPlayingEnterExitExit"/> -- a tooltip's own exit removes it from this slot, it is never a
    /// <see cref="MGWindow.IsClosing"/> window close). Only one tooltip sits here at a time: a new occupant ends whatever was here at once.</summary>
    internal MGToolTip ExitingToolTip { get; set; }

    /// <summary>Y8: the screen position <see cref="ExitingToolTip"/> is drawn at -- the mouse position plus <see cref="MGToolTip.DrawOffset"/>
    /// captured the instant its exit started, exactly what <see cref="MGToolTip.DrawAtDefaultPosition"/> would have used that frame. Frozen
    /// for the rest of the run: the tooltip no longer follows the mouse once it is exiting.</summary>
    internal Point ExitingToolTipDrawPosition { get; set; }

    internal MGContextMenu ActiveContextMenu { get; set; }

    /// <summary>Y8: the root-level context menu whose exit is playing after it stopped being <see cref="ActiveContextMenu"/>, kept drawn (at
    /// its own position -- unlike a tooltip, a context menu has one) until the run ends, then cleared. Same no-input/no-occlusion and
    /// single-occupant rules as <see cref="ExitingToolTip"/>. A submenu's own exiting slot lives on the <see cref="MGContextMenu"/> instance
    /// that hosts it, one level at a time, not here.</summary>
    internal MGContextMenu ExitingContextMenu { get; set; }

    internal MGElement FocusedKeyboardHandler { get; set; }
    internal Dictionary<MGWindow, MGElement> WindowFocusHistory { get; } = new();
}