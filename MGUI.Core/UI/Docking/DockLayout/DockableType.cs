namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// Defines the type of a dockable panel, which influences where it can be placed.
/// </summary>
public enum DockableType
{
    /// <summary>
    /// A tool window (e.g. Solution Explorer, Properties). Typically docked on the sides or bottom.
    /// </summary>
    Tool,

    /// <summary>
    /// A document editor (e.g. code file, designer). Typically occupies the central document area.
    /// </summary>
    Document
}
