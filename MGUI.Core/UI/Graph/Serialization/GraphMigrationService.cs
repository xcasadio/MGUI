namespace MGUI.Core.UI.Graph;

public sealed class GraphMigrationService
{
    public GraphDocument Migrate(GraphDocument document, ICollection<string> diagnostics = null)
    {
        if (document == null)
        {
            diagnostics?.Add("Graph document is missing.");
            return null;
        }

        if (document.Version <= 0)
        {
            diagnostics?.Add("Graph document version was missing; assuming version 1.");
            document.Version = GraphDocument.CurrentVersion;
        }

        if (document.Version > GraphDocument.CurrentVersion)
        {
            diagnostics?.Add($"Graph document version {document.Version} is newer than supported version {GraphDocument.CurrentVersion}.");
        }

        return document;
    }
}