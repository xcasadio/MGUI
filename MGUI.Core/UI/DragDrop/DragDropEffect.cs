namespace MGUI.Core.UI.DragDrop;

/// <summary>Specifies the allowed or resulting effect of a drag-and-drop operation.
/// Values can be combined with bitwise OR to indicate multiple allowed effects.</summary>
[Flags]
public enum DragDropEffect
{
    /// <summary>The drop target does not accept the drag data.</summary>
    None = 0,
    /// <summary>The data is copied to the drop target.</summary>
    Copy = 1,
    /// <summary>The data is moved to the drop target (removed from source).</summary>
    Move = 2,
    /// <summary>The data is linked in the drop target.</summary>
    Link = 4,
    /// <summary>All effects are allowed.</summary>
    All  = Copy | Move | Link
}