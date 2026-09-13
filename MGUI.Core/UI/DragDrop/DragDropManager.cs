using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.DragDrop;

/// <summary>Manages an ongoing drag-and-drop operation for the desktop.
/// Call <see cref="DoDragDrop"/> from an element's mouse-pressed handler to initiate a drag.</summary>
public class DragDropManager
{
    /// <summary>The desktop this manager belongs to.</summary>
    public MGDesktop Desktop { get; }

    /// <summary>The current drag data, or null if no drag is in progress.</summary>
    public DragDropData ActiveDrag { get; private set; }

    /// <summary>The element that initiated the current drag, or null if no drag is in progress.</summary>
    public MGElement DragSource { get; private set; }

    /// <summary>The element currently under the cursor that is accepting drag events.</summary>
    public MGElement CurrentDropTarget { get; private set; }

    /// <summary>Whether a drag is currently in progress.</summary>
    public bool IsDragging => ActiveDrag != null;

    /// <summary>Raised when a drag operation starts.</summary>
    public event EventHandler<DragDropData> DragStarted;
    /// <summary>Raised when a drag operation ends (drop or cancel).</summary>
    public event EventHandler<DragDropData> DragEnded;

    public DragDropManager(MGDesktop desktop)
    {
        Desktop = desktop ?? throw new ArgumentNullException(nameof(desktop));
    }

    /// <summary>Starts a drag-and-drop operation originating from <paramref name="source"/>.</summary>
    public void DoDragDrop(MGElement source, DragDropData data)
    {
        if (IsDragging)
        {
            CancelDrag();
        }

        DragSource = source;
        ActiveDrag = data;
        DragStarted?.Invoke(this, data);
    }

    /// <summary>Called by elements whose <see cref="MGElement.AllowDrop"/> is true when the mouse
    /// enters the element during an active drag. Fires <see cref="MGElement.DragEnter"/>.</summary>
    public void NotifyDragEnter(MGElement target, Point position)
    {
        if (!IsDragging || target == null)
        {
            return;
        }

        if (CurrentDropTarget == target)
        {
            return;
        }

        // Leave previous target
        if (CurrentDropTarget != null)
        {
            CurrentDropTarget.RaiseDragLeave(new DragLeaveEventArgs(ActiveDrag, DragSource, position));
        }

        CurrentDropTarget = target;
        target.RaiseDragEnter(new DragEnterEventArgs(ActiveDrag, DragSource, position));
    }

    /// <summary>Called by elements whose <see cref="MGElement.AllowDrop"/> is true when the mouse
    /// moves inside the element during an active drag. Fires <see cref="MGElement.DragOver"/>.</summary>
    public void NotifyDragOver(MGElement target, Point position)
    {
        if (!IsDragging || target == null)
        {
            return;
        }

        if (CurrentDropTarget != target)
        {
            NotifyDragEnter(target, position);
        }

        target.RaiseDragOver(new DragOverEventArgs(ActiveDrag, DragSource, position));
    }

    /// <summary>Called by elements whose <see cref="MGElement.AllowDrop"/> is true when the mouse
    /// leaves the element during an active drag. Fires <see cref="MGElement.DragLeave"/>.</summary>
    public void NotifyDragLeave(MGElement target, Point position)
    {
        if (!IsDragging || target == null)
        {
            return;
        }

        if (CurrentDropTarget == target)
        {
            target.RaiseDragLeave(new DragLeaveEventArgs(ActiveDrag, DragSource, position));
            CurrentDropTarget = null;
        }
    }

    /// <summary>Called when the mouse button is released. If dropped on a valid target, fires <see cref="MGElement.Drop"/>.</summary>
    public void NotifyDrop(MGElement target, Point position)
    {
        if (!IsDragging)
        {
            return;
        }

        if (target != null && target.AllowDrop)
        {
            if (CurrentDropTarget != target)
            {
                NotifyDragEnter(target, position);
            }

            target.RaiseDrop(new DropEventArgs(ActiveDrag, DragSource, position));
        }
        EndDrag();
    }

    /// <summary>Cancels the active drag without dropping.</summary>
    public void CancelDrag()
    {
        if (!IsDragging)
        {
            return;
        }

        if (CurrentDropTarget != null)
        {
            CurrentDropTarget.RaiseDragLeave(new DragLeaveEventArgs(ActiveDrag, DragSource, Point.Zero));
            CurrentDropTarget = null;
        }
        EndDrag();
    }

    private void EndDrag()
    {
        var drag = ActiveDrag;
        ActiveDrag    = null;
        DragSource    = null;
        CurrentDropTarget = null;
        DragEnded?.Invoke(this, drag);
    }
}