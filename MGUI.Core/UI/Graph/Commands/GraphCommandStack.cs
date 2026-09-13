namespace MGUI.Core.UI.Graph;

public class GraphCommandStack
{
    private readonly List<IGraphCommand> UndoStack = new();
    private readonly List<IGraphCommand> RedoStack = new();

    public int Capacity { get; }
    public bool CanUndo => UndoStack.Count > 0;
    public bool CanRedo => RedoStack.Count > 0;
    public int UndoCount => UndoStack.Count;
    public int RedoCount => RedoStack.Count;

    public GraphCommandStack(int capacity = 100)
    {
        Capacity = Math.Max(1, capacity);
    }

    public bool Execute(GraphDocument document, IGraphCommand command)
    {
        if (document == null || command == null || !command.Execute(document))
        {
            return false;
        }

        UndoStack.Add(command);
        if (UndoStack.Count > Capacity)
        {
            UndoStack.RemoveAt(0);
        }

        RedoStack.Clear();
        return true;
    }

    public bool Undo(GraphDocument document)
    {
        if (document == null || UndoStack.Count == 0)
        {
            return false;
        }

        int index = UndoStack.Count - 1;
        IGraphCommand command = UndoStack[index];
        UndoStack.RemoveAt(index);
        if (!command.Undo(document))
        {
            return false;
        }

        RedoStack.Add(command);
        return true;
    }

    public bool Redo(GraphDocument document)
    {
        if (document == null || RedoStack.Count == 0)
        {
            return false;
        }

        int index = RedoStack.Count - 1;
        IGraphCommand command = RedoStack[index];
        RedoStack.RemoveAt(index);
        if (!command.Execute(document))
        {
            return false;
        }

        UndoStack.Add(command);
        return true;
    }

    public void Clear()
    {
        UndoStack.Clear();
        RedoStack.Clear();
    }
}