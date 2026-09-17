namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// Remembers the tab group a panel left, and its tab index at that moment, so the panel can be
/// returned to the same place later (after being floated, auto-hidden or closed).
/// </summary>
/// <param name="GroupId">Id of the tab group the panel was removed from.</param>
/// <param name="TabIndex">Index of the panel within that group's tabs at the moment it was removed.</param>
internal sealed record DockPanelPlacement(string GroupId, int TabIndex);
