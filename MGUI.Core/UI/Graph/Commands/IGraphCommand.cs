namespace MGUI.Core.UI.Graph
{
    public interface IGraphCommand
    {
        string Name { get; }
        bool Execute(GraphDocument document);
        bool Undo(GraphDocument document);
    }
}