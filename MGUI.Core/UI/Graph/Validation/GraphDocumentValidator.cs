namespace MGUI.Core.UI.Graph
{
    public class GraphDocumentValidator : IGraphValidator
    {
        private readonly GraphTypeCompatibilityService CompatibilityService;

        public GraphDocumentValidator()
            : this(new GraphTypeCompatibilityService())
        {
        }

        public GraphDocumentValidator(GraphTypeCompatibilityService compatibilityService)
        {
            CompatibilityService = compatibilityService ?? new GraphTypeCompatibilityService();
        }

        public GraphValidationResult Validate(GraphDocument document)
        {
            GraphValidationResult result = new();
            if (document == null)
            {
                result.Add(new(GraphValidationSeverity.Error, nameof(GraphDocument), "Graph document is required."));
                return result;
            }

            ValidateEdges(document, result);
            ValidateRequiredPorts(document, result);
            return result;
        }

        private void ValidateEdges(GraphDocument document, GraphValidationResult result)
        {
            for (int i = 0; i < document.Edges.Count; i++)
            {
                GraphEdgeModel edge = document.Edges[i];
                GraphConnectionValidationResult connection = CompatibilityService.ValidateConnection(
                    document,
                    edge.SourceNodeId,
                    edge.SourcePortId,
                    edge.TargetNodeId,
                    edge.TargetPortId,
                    edge.Id);

                if (!connection.IsValid)
                {
                    result.Add(new(GraphValidationSeverity.Error, connection.Code, connection.Message, edge.TargetNodeId, edge.TargetPortId, edge.Id));
                }
            }
        }

        private static void ValidateRequiredPorts(GraphDocument document, GraphValidationResult result)
        {
            for (int nodeIndex = 0; nodeIndex < document.Nodes.Count; nodeIndex++)
            {
                GraphNodeModel node = document.Nodes[nodeIndex];
                for (int portIndex = 0; portIndex < node.Ports.Count; portIndex++)
                {
                    GraphPortModel port = node.Ports[portIndex];
                    if (!port.IsRequired || port.Direction != GraphPortDirection.Input)
                    {
                        continue;
                    }

                    bool connected = false;
                    for (int edgeIndex = 0; edgeIndex < document.Edges.Count; edgeIndex++)
                    {
                        GraphEdgeModel edge = document.Edges[edgeIndex];
                        if (edge.TargetNodeId == node.Id && edge.TargetPortId == port.Id)
                        {
                            connected = true;
                            break;
                        }
                    }

                    if (!connected)
                    {
                        result.Add(new(GraphValidationSeverity.Error, "RequiredPortUnconnected", $"Required port '{port.Name}' is not connected.", node.Id, port.Id));
                    }
                }
            }
        }
    }
}