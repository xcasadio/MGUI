using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// Extension methods for MGDockHost to support layout persistence (Save/Load).
/// </summary>
public static class MGDockHostExtensions
{
    /// <summary>
    /// Saves the current layout to a JSON string.
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

        return DockLayoutSerializer.ToJson(dockHost.LayoutModel, indented);
    }

    /// <summary>
    /// Loads a layout from a JSON string.
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
        dockHost.LayoutModel = newLayoutModel;
    }
}
