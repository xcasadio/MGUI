using Microsoft.Xna.Framework;

namespace MGUI.Core.UI;

/// <summary>Event args for <see cref="MGElement.ContextMenuRequested"/>.
/// Allows subscribers to provide or replace the <see cref="MGContextMenu"/> that will be opened
/// when the user right-clicks the element, without needing to assign <see cref="MGElement.ContextMenu"/>.</summary>
public class ContextMenuRequestedEventArgs : EventArgs
{
    /// <summary>The context menu that will be opened, initially equal to the element's
    /// <see cref="MGElement.ContextMenu"/> property. Set this to a different instance to
    /// replace the menu, or to <see langword="null"/> to suppress opening entirely.</summary>
    public MGContextMenu Menu { get; set; }

    /// <summary>Mouse position in screen space at the moment of the right-click.</summary>
    public Point Position { get; }

    /// <summary>Set to <see langword="true"/> to suppress opening the menu entirely.
    /// If <see langword="false"/> (default), the menu referenced by <see cref="Menu"/> will be opened.</summary>
    public bool Handled { get; set; }

    public ContextMenuRequestedEventArgs(MGContextMenu InitialMenu, Point position)
    {
        Menu = InitialMenu;
        this.Position = position;
        Handled = false;
    }
}