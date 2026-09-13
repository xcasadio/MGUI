namespace MGUI.Core.UI.Graph;

public class GraphEdgeModel
{
    public Guid Id { get; set; }
    public Guid SourceNodeId { get; set; }
    public Guid SourcePortId { get; set; }
    public Guid TargetNodeId { get; set; }
    public Guid TargetPortId { get; set; }
    public Dictionary<string, string> RenderMetadata { get; set; } = new(StringComparer.Ordinal);

    public GraphEdgeModel()
    {
    }

    public GraphEdgeModel(Guid id, Guid sourceNodeId, Guid sourcePortId, Guid targetNodeId, Guid targetPortId)
    {
        Id = id;
        SourceNodeId = sourceNodeId;
        SourcePortId = sourcePortId;
        TargetNodeId = targetNodeId;
        TargetPortId = targetPortId;
    }
}