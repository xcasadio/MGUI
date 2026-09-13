namespace MGUI.Core.UI.Graph;

public enum GraphValidationSeverity
{
    Warning,
    Error,
}

public sealed class GraphValidationIssue
{
    public GraphValidationSeverity Severity { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? NodeId { get; set; }
    public Guid? PortId { get; set; }
    public Guid? EdgeId { get; set; }

    public GraphValidationIssue()
    {
    }

    public GraphValidationIssue(GraphValidationSeverity severity, string code, string message, Guid? nodeId = null, Guid? portId = null, Guid? edgeId = null)
    {
        Severity = severity;
        Code = code ?? string.Empty;
        Message = message ?? string.Empty;
        NodeId = nodeId;
        PortId = portId;
        EdgeId = edgeId;
    }
}

public sealed class GraphValidationResult
{
    public List<GraphValidationIssue> Issues { get; } = new();

    public bool IsValid
    {
        get
        {
            for (var i = 0; i < Issues.Count; i++)
            {
                if (Issues[i].Severity == GraphValidationSeverity.Error)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public void Add(GraphValidationIssue issue)
    {
        if (issue != null)
        {
            Issues.Add(issue);
        }
    }
}

public interface IGraphValidator
{
    GraphValidationResult Validate(GraphDocument document);
}