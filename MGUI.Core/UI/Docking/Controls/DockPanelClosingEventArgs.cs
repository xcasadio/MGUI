using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking.Controls;

/// <summary>
/// The arguments <see cref="MGDockHost.PanelClosing"/> is raised with (ADR-0018). The event keeps its
/// <see cref="CancelEventArgs{T}"/> type; a subscriber that needs to know why the panel is closing casts to this type.
/// </summary>
public class DockPanelClosingEventArgs : CancelEventArgs<DockPanelNode>
{
    public DockPanelClosingEventArgs(DockPanelNode panel, MGFloatingDockWindow closingFloatingWindow)
        : base(panel)
    {
        ClosingFloatingWindow = closingFloatingWindow;
    }

    /// <summary>
    /// The floating window being closed as a whole (<see cref="MGWindow.TryCloseWindow"/>: its close button or
    /// application code) when that close raised this event, or <see langword="null"/> for every other close path.<para/>
    /// Such a close stops at the first refusal and cancels the window close, so a subscriber that refuses in order to ask a
    /// question asynchronously can call <see cref="MGWindow.TryCloseWindow"/> on this window again once it has its answer:
    /// the panels the window still holds are then asked again, in order.
    /// </summary>
    public MGFloatingDockWindow ClosingFloatingWindow { get; }
}
