using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph;

public class GraphNodeModel
{
    public Guid Id { get; set; }
    public string NodeType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Vector2 Position { get; set; }
    public Vector2? Size { get; set; }
    public bool IsCollapsed { get; set; }
    public List<GraphPortModel> Ports { get; set; } = new();
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, string> EditorMetadata { get; set; } = new(StringComparer.Ordinal);

    public GraphNodeModel()
    {
    }

    public GraphNodeModel(Guid id, string nodeType, string title, Vector2 position)
    {
        Id = id;
        NodeType = nodeType ?? string.Empty;
        Title = title ?? string.Empty;
        Position = position;
    }
}