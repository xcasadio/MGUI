namespace MGUI.Core.Tooling;

public static class UIDiagnosticAssertions
{
    public static void ExpectFocusedElement(UIDesktopDiagnosticSnapshot snapshot, string expectedDiagnosticId, string artifact = null, string stepName = null)
    {
        if (!string.Equals(snapshot?.FocusedElementDiagnosticId, expectedDiagnosticId, StringComparison.Ordinal))
        {
            throw CreateAssertionException($"Expected focused element '{expectedDiagnosticId}' but found '{snapshot?.FocusedElementDiagnosticId ?? "<none>"}'.", artifact, stepName);
        }
    }

    public static void ExpectActiveOverlay(UIDesktopDiagnosticSnapshot snapshot, string expectedDiagnosticId, string artifact = null, string stepName = null)
    {
        if (!string.Equals(snapshot?.ActiveOverlayDiagnosticId, expectedDiagnosticId, StringComparison.Ordinal))
        {
            throw CreateAssertionException($"Expected active overlay '{expectedDiagnosticId}' but found '{snapshot?.ActiveOverlayDiagnosticId ?? "<none>"}'.", artifact, stepName);
        }
    }

    private static InvalidOperationException CreateAssertionException(string message, string artifact, string stepName)
    {
        var prefix = string.IsNullOrWhiteSpace(stepName) ? string.Empty : $"[{stepName}] ";
        if (string.IsNullOrWhiteSpace(artifact))
        {
            return new InvalidOperationException(prefix + message);
        }

        return new InvalidOperationException(prefix + message + Environment.NewLine + Environment.NewLine + artifact);
    }
}