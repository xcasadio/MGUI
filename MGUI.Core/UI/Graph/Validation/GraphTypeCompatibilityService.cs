namespace MGUI.Core.UI.Graph;

public class GraphTypeCompatibilityService
{
    public const string MissingSourceNode = nameof(MissingSourceNode);
    public const string MissingTargetNode = nameof(MissingTargetNode);
    public const string MissingSourcePort = nameof(MissingSourcePort);
    public const string MissingTargetPort = nameof(MissingTargetPort);
    public const string SourceMustBeOutput = nameof(SourceMustBeOutput);
    public const string TargetMustBeInput = nameof(TargetMustBeInput);
    public const string DuplicateConnection = nameof(DuplicateConnection);
    public const string TargetCardinalitySingle = nameof(TargetCardinalitySingle);
    public const string IncompatibleTypes = nameof(IncompatibleTypes);
    public const string CycleDetected = nameof(CycleDetected);

    public GraphConnectionValidationResult ValidateConnection(GraphDocument document, Guid sourceNodeId, Guid sourcePortId,
        Guid targetNodeId, Guid targetPortId, Guid? ignoredEdgeId = null)
    {
        if (document == null)
        {
            return GraphConnectionValidationResult.Invalid(nameof(GraphDocument), "Graph document is required.");
        }

        var sourceNode = document.TryGetNode(sourceNodeId);
        if (sourceNode == null)
        {
            return GraphConnectionValidationResult.Invalid(MissingSourceNode, $"Missing source node '{sourceNodeId}'.");
        }

        var targetNode = document.TryGetNode(targetNodeId);
        if (targetNode == null)
        {
            return GraphConnectionValidationResult.Invalid(MissingTargetNode, $"Missing target node '{targetNodeId}'.");
        }

        var sourcePort = document.TryGetPort(sourceNodeId, sourcePortId);
        if (sourcePort == null)
        {
            return GraphConnectionValidationResult.Invalid(MissingSourcePort, $"Missing source port '{sourcePortId}'.");
        }

        var targetPort = document.TryGetPort(targetNodeId, targetPortId);
        if (targetPort == null)
        {
            return GraphConnectionValidationResult.Invalid(MissingTargetPort, $"Missing target port '{targetPortId}'.");
        }

        if (sourcePort.Direction != GraphPortDirection.Output)
        {
            return GraphConnectionValidationResult.Invalid(SourceMustBeOutput, "Source port must be an output port.");
        }

        if (targetPort.Direction != GraphPortDirection.Input)
        {
            return GraphConnectionValidationResult.Invalid(TargetMustBeInput, "Target port must be an input port.");
        }

        if (!AreTypesCompatible(sourcePort, targetPort))
        {
            return GraphConnectionValidationResult.Invalid(IncompatibleTypes, $"Cannot connect '{sourcePort.ValueType}' to '{targetPort.ValueType}'.");
        }

        for (var i = 0; i < document.Edges.Count; i++)
        {
            var edge = document.Edges[i];
            if (ignoredEdgeId.HasValue && edge.Id == ignoredEdgeId.Value)
            {
                continue;
            }

            if (edge.SourceNodeId == sourceNodeId && edge.SourcePortId == sourcePortId &&
                edge.TargetNodeId == targetNodeId && edge.TargetPortId == targetPortId)
            {
                return GraphConnectionValidationResult.Invalid(DuplicateConnection, "Graph already contains this connection.");
            }

            if (targetPort.Cardinality == GraphPortCardinality.Single && edge.TargetNodeId == targetNodeId && edge.TargetPortId == targetPortId)
            {
                return GraphConnectionValidationResult.Invalid(TargetCardinalitySingle, "Target port cardinality is single and already has a connection.");
            }
        }

        if (document.DisallowCycles && WouldCreateCycle(document, sourceNodeId, targetNodeId, ignoredEdgeId))
        {
            return GraphConnectionValidationResult.Invalid(CycleDetected, "Connection would create a cycle.");
        }

        return GraphConnectionValidationResult.Valid;
    }

    public bool AreTypesCompatible(GraphPortModel sourcePort, GraphPortModel targetPort)
    {
        if (sourcePort == null || targetPort == null)
        {
            return false;
        }

        if (sourcePort.ValueType == GraphValueType.Wildcard || targetPort.ValueType == GraphValueType.Wildcard)
        {
            return true;
        }

        if (sourcePort.ValueType == targetPort.ValueType)
        {
            if (sourcePort.ValueType != GraphValueType.Custom)
            {
                return true;
            }

            return string.Equals(sourcePort.CustomTypeName, targetPort.CustomTypeName, StringComparison.Ordinal);
        }

        if (sourcePort.ValueType == GraphValueType.Int && targetPort.ValueType == GraphValueType.Float)
        {
            return true;
        }

        return false;
    }

    private static bool WouldCreateCycle(GraphDocument document, Guid sourceNodeId, Guid targetNodeId, Guid? ignoredEdgeId)
    {
        if (sourceNodeId == targetNodeId)
        {
            return true;
        }

        Stack<Guid> stack = new();
        HashSet<Guid> visited = new();
        stack.Push(targetNodeId);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            for (var i = 0; i < document.Edges.Count; i++)
            {
                var edge = document.Edges[i];
                if (ignoredEdgeId.HasValue && edge.Id == ignoredEdgeId.Value)
                {
                    continue;
                }

                if (edge.SourceNodeId != current)
                {
                    continue;
                }

                if (edge.TargetNodeId == sourceNodeId)
                {
                    return true;
                }

                stack.Push(edge.TargetNodeId);
            }
        }

        return false;
    }
}