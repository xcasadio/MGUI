namespace MGUI.Core.UI.Graph;

public sealed class GraphSerializationResult
{
    public bool Success { get; set; }
    public string Json { get; set; } = string.Empty;
    public GraphDocument Document { get; set; }
    public List<string> Diagnostics { get; } = new();

    public static GraphSerializationResult FromJson(string json)
        => new() { Success = true, Json = json ?? string.Empty };

    public static GraphSerializationResult FromDocument(GraphDocument document)
        => new() { Success = document != null, Document = document };

    public static GraphSerializationResult Failed(string diagnostic)
    {
        GraphSerializationResult result = new() { Success = false };
        result.Diagnostics.Add(diagnostic ?? "Graph serialization failed.");
        return result;
    }
}