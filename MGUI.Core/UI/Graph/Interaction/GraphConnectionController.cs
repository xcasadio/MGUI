using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph;

public sealed class GraphConnectionController
{
    private readonly MGGraphView GraphView;
    private readonly GraphTypeCompatibilityService CompatibilityService = new();

    public bool IsDragging { get; private set; }
    public Guid StartPortId { get; private set; }
    public Guid? HoverPortId { get; private set; }
    public Vector2 StartViewportPoint { get; private set; }
    public Vector2 CurrentViewportPoint { get; private set; }
    public GraphConnectionValidationResult PreviewValidationResult { get; private set; } = GraphConnectionValidationResult.Valid;
    public string LastDiagnostic { get; private set; } = string.Empty;

    public GraphConnectionController(MGGraphView graphView)
    {
        GraphView = graphView ?? throw new ArgumentNullException(nameof(graphView));
    }

    public bool BeginDrag(Guid startPortId, Vector2 startViewportPoint)
    {
        if (GraphView.Document?.TryGetPort(startPortId) == null)
        {
            LastDiagnostic = $"Missing start port '{startPortId}'.";
            return false;
        }

        IsDragging = true;
        StartPortId = startPortId;
        HoverPortId = null;
        StartViewportPoint = startViewportPoint;
        CurrentViewportPoint = startViewportPoint;
        PreviewValidationResult = GraphConnectionValidationResult.Valid;
        LastDiagnostic = string.Empty;
        GraphView.SetPortConnectionFeedback(startPortId, isSource: true, isTarget: false, isCompatible: true);
        return true;
    }

    public void UpdateDrag(Vector2 currentViewportPoint, Guid? hoverPortId)
    {
        if (!IsDragging)
        {
            return;
        }

        CurrentViewportPoint = currentViewportPoint;
        if (HoverPortId.HasValue && HoverPortId.Value != hoverPortId)
        {
            GraphView.SetPortConnectionFeedback(HoverPortId.Value, isSource: false, isTarget: false, isCompatible: false);
        }

        HoverPortId = hoverPortId;
        if (hoverPortId.HasValue)
        {
            PreviewValidationResult = Validate(StartPortId, hoverPortId.Value);
            GraphView.SetPortConnectionFeedback(hoverPortId.Value, isSource: false, isTarget: true, isCompatible: PreviewValidationResult.IsValid);
            LastDiagnostic = PreviewValidationResult.Message;
        }
        else
        {
            PreviewValidationResult = GraphConnectionValidationResult.Valid;
            LastDiagnostic = string.Empty;
        }
    }

    public bool CompleteDrag(Guid targetPortId)
    {
        if (!IsDragging)
        {
            return false;
        }

        var connected = TryCreateConnection(StartPortId, targetPortId);
        Cancel();
        return connected;
    }

    public void Cancel()
    {
        if (StartPortId != Guid.Empty)
        {
            GraphView.SetPortConnectionFeedback(StartPortId, isSource: false, isTarget: false, isCompatible: false);
        }

        if (HoverPortId.HasValue)
        {
            GraphView.SetPortConnectionFeedback(HoverPortId.Value, isSource: false, isTarget: false, isCompatible: false);
        }

        IsDragging = false;
        StartPortId = Guid.Empty;
        HoverPortId = null;
        StartViewportPoint = Vector2.Zero;
        CurrentViewportPoint = Vector2.Zero;
        PreviewValidationResult = GraphConnectionValidationResult.Valid;
    }

    public GraphConnectionValidationResult Validate(Guid firstPortId, Guid secondPortId)
    {
        if (!TryNormalizeConnection(firstPortId, secondPortId, out var edge, out var diagnostic))
        {
            return GraphConnectionValidationResult.Invalid(nameof(GraphConnectionController), diagnostic);
        }

        return CompatibilityService.ValidateConnection(GraphView.Document, edge.SourceNodeId, edge.SourcePortId, edge.TargetNodeId, edge.TargetPortId);
    }

    public bool TryCreateConnection(Guid firstPortId, Guid secondPortId)
    {
        if (!TryNormalizeConnection(firstPortId, secondPortId, out var edge, out var diagnostic))
        {
            LastDiagnostic = diagnostic;
            return false;
        }

        var validation = CompatibilityService.ValidateConnection(GraphView.Document, edge.SourceNodeId, edge.SourcePortId, edge.TargetNodeId, edge.TargetPortId);
        PreviewValidationResult = validation;
        LastDiagnostic = validation.Message;
        if (!validation.IsValid)
        {
            return false;
        }

        return GraphView.Commands.Execute(GraphView.Document, new ConnectPortsCommand(edge));
    }

    public bool DeleteSelectedEdges()
    {
        if (GraphView.SelectedEdgeIds.Count == 0)
        {
            return false;
        }

        var changed = false;
        foreach (var edgeId in new System.Collections.Generic.List<Guid>(GraphView.SelectedEdgeIds))
        {
            changed |= GraphView.Commands.Execute(GraphView.Document, new DisconnectPortsCommand(edgeId));
        }

        if (changed)
        {
            GraphView.SelectedEdgeIds.Clear();
        }

        return changed;
    }

    private bool TryNormalizeConnection(Guid firstPortId, Guid secondPortId, out GraphEdgeModel edge, out string diagnostic)
    {
        edge = null;
        diagnostic = string.Empty;
        var first = GraphView.Document?.TryGetPort(firstPortId);
        var second = GraphView.Document?.TryGetPort(secondPortId);
        if (first == null || second == null)
        {
            diagnostic = "Both ports must exist.";
            return false;
        }

        if (first.Id == second.Id)
        {
            diagnostic = "Cannot connect a port to itself.";
            return false;
        }

        GraphPortModel source;
        GraphPortModel target;
        if (first.Direction == GraphPortDirection.Output && second.Direction == GraphPortDirection.Input)
        {
            source = first;
            target = second;
        }
        else if (first.Direction == GraphPortDirection.Input && second.Direction == GraphPortDirection.Output)
        {
            source = second;
            target = first;
        }
        else
        {
            diagnostic = "Connections require one output port and one input port.";
            return false;
        }

        edge = new GraphEdgeModel(Guid.NewGuid(), source.NodeId, source.Id, target.NodeId, target.Id);
        return true;
    }
}