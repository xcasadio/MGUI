using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Extension methods for MGDockHost to support layout persistence (Save/Load).
/// </summary>
public static class MGDockHostExtensions
{
    /// <summary>
    /// Saves the current layout to a JSON string (format 2.0: tree, floating windows, auto-hidden
    /// panels and remembered placements). Flushes every live floating window's current bounds into
    /// its <see cref="DockFloatingGroup"/> first (P9): the window's position/size change events are
    /// not immediate, so without this the saved bounds could lag behind what is on screen. A
    /// currently maximized window is left untouched, so its <see cref="DockFloatingGroup"/> keeps the
    /// bounds it had before being maximized.
    /// </summary>
    /// <param name="dockHost">The MGDockHost instance.</param>
    /// <param name="indented">Whether to format the JSON with indentation (default: true).</param>
    /// <returns>JSON string representation of the current layout.</returns>
    public static string SaveLayoutToJson(this MGDockHost dockHost, bool indented = true)
    {
        if (dockHost == null)
        {
            throw new ArgumentNullException(nameof(dockHost));
        }

        if (dockHost.LayoutModel == null)
        {
            throw new InvalidOperationException("Cannot save layout: LayoutModel is null.");
        }

        foreach (var window in dockHost.FloatingWindows)
        {
            if (window.FloatingGroup != null && !window.TabGroup.IsMaximized)
            {
                window.FloatingGroup.Left = window.Left;
                window.FloatingGroup.Top = window.Top;
                window.FloatingGroup.Width = window.WindowWidth;
                window.FloatingGroup.Height = window.WindowHeight;
            }
        }

        return DockLayoutSerializer.ToJson(dockHost.LayoutModel, indented);
    }

    /// <summary>
    /// Loads a layout from a JSON string. Throws when the document is invalid (malformed JSON, a
    /// version other than 2.0, or an inconsistent document); see <see cref="TryLoadLayoutFromJson"/>
    /// for a non-throwing equivalent that leaves the host untouched on failure (D7).
    /// </summary>
    /// <param name="dockHost">The MGDockHost instance.</param>
    /// <param name="json">The JSON string containing the layout data.</param>
    /// <param name="panelFactory">Factory function to create panel content. Takes panel ID and returns ContentFactory. If null, panels will be created with null content factories.</param>
    public static void LoadLayoutFromJson(this MGDockHost dockHost, string json, Func<string, Func<MGElement>> panelFactory = null)
    {
        if (dockHost == null)
        {
            throw new ArgumentNullException(nameof(dockHost));
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("JSON string cannot be null or empty.", nameof(json));
        }

        var newLayoutModel = DockLayoutSerializer.FromJson(json, panelFactory);
        dockHost.ApplyLoadedLayoutModel(newLayoutModel);
    }

    /// <summary>
    /// Attempts to load a layout from a JSON string. Never throws: on failure (malformed JSON, a
    /// version other than 2.0, or an inconsistent document) it writes each diagnostic to
    /// <see cref="System.Diagnostics.Debug"/>, returns false, and leaves <paramref name="dockHost"/>
    /// exactly as it was - no floating window closed, no layout change (D7/D8). On success it applies
    /// the loaded model (floating windows recreated, auto-hide strips refreshed, panel registry
    /// rebuilt, P10) and returns true.
    /// </summary>
    /// <param name="dockHost">The MGDockHost instance.</param>
    /// <param name="json">The JSON string containing the layout data.</param>
    /// <param name="panelFactory">Factory function to create panel content. Takes panel ID and returns ContentFactory. If null, panels will be created with null content factories.</param>
    /// <param name="diagnostics">One diagnostic message per problem found. Empty on success.</param>
    /// <returns>True when the layout was loaded and applied.</returns>
    public static bool TryLoadLayoutFromJson(this MGDockHost dockHost, string json, Func<string, Func<MGElement>> panelFactory, out IReadOnlyList<string> diagnostics)
    {
        if (dockHost == null)
        {
            throw new ArgumentNullException(nameof(dockHost));
        }

        if (!DockLayoutSerializer.TryFromJson(json, panelFactory, out var newLayoutModel, out diagnostics))
        {
            foreach (var diagnostic in diagnostics)
            {
                System.Diagnostics.Debug.WriteLine($"[MGDockHostExtensions] TryLoadLayoutFromJson failed: {diagnostic}");
            }

            return false;
        }

        dockHost.ApplyLoadedLayoutModel(newLayoutModel);
        return true;
    }
}
