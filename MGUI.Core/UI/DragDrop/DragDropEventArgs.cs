using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.DragDrop;

/// <summary>Base class for drag-and-drop event arguments.</summary>
public abstract class DragDropEventArgs : EventArgs
{
    /// <summary>The data being dragged.</summary>
    public DragDropData Data { get; }

    /// <summary>The current mouse position in screen coordinates.</summary>
    public Point Position { get; }

    /// <summary>The element that initiated the drag.</summary>
    public MGElement Source { get; }

    protected DragDropEventArgs(DragDropData data, MGElement source, Point position)
    {
        Data     = data     ?? throw new ArgumentNullException(nameof(data));
        Source   = source;
        Position = position;
    }
}

public sealed class DragEnterEventArgs  : DragDropEventArgs
{
    public DragEnterEventArgs(DragDropData data, MGElement source, Point position) : base(data, source, position) { }
}

public sealed class DragOverEventArgs   : DragDropEventArgs
{
    public DragOverEventArgs(DragDropData data, MGElement source, Point position) : base(data, source, position) { }
}

public sealed class DragLeaveEventArgs  : DragDropEventArgs
{
    public DragLeaveEventArgs(DragDropData data, MGElement source, Point position) : base(data, source, position) { }
}

public sealed class DropEventArgs       : DragDropEventArgs
{
    public DropEventArgs(DragDropData data, MGElement source, Point position) : base(data, source, position) { }
}