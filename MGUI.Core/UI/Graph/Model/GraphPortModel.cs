namespace MGUI.Core.UI.Graph;

public class GraphPortModel
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public GraphPortDirection Direction { get; set; }
    public GraphValueType ValueType { get; set; }
    public GraphPortCardinality Cardinality { get; set; } = GraphPortCardinality.Single;
    public bool IsRequired { get; set; }
    public string DefaultValue { get; set; }
    public string CustomTypeName { get; set; }
    public Dictionary<string, string> EditorMetadata { get; set; } = new(StringComparer.Ordinal);

    public GraphPortModel()
    {
    }

    public GraphPortModel(Guid nodeId, Guid id, string name, GraphPortDirection direction, GraphValueType valueType,
        GraphPortCardinality cardinality = GraphPortCardinality.Single, bool isRequired = false)
    {
        NodeId = nodeId;
        Id = id;
        Name = name ?? string.Empty;
        Direction = direction;
        ValueType = valueType;
        Cardinality = cardinality;
        IsRequired = isRequired;
    }
}