namespace MGUI.Core.UI.Docking.DockLayout;

/// <summary>
/// A tab group shown in a floating window, together with the window's bounds.
/// Lives in <see cref="DockLayoutModel"/>'s floating store; the host reflects it as an actual window.
/// </summary>
public sealed class DockFloatingGroup
{
    /// <summary>
    /// The tab group displayed in the floating window.
    /// </summary>
    public DockTabGroupNode Group { get; }

    /// <summary>
    /// Left position of the floating window, in pixels.
    /// </summary>
    public int Left { get; set; }

    /// <summary>
    /// Top position of the floating window, in pixels.
    /// </summary>
    public int Top { get; set; }

    /// <summary>
    /// Width of the floating window, in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Height of the floating window, in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Creates a new floating group for <paramref name="group"/> with the given window bounds.
    /// </summary>
    /// <param name="group">The tab group displayed in the floating window.</param>
    /// <param name="left">Left position of the floating window, in pixels.</param>
    /// <param name="top">Top position of the floating window, in pixels.</param>
    /// <param name="width">Width of the floating window, in pixels.</param>
    /// <param name="height">Height of the floating window, in pixels.</param>
    public DockFloatingGroup(DockTabGroupNode group, int left, int top, int width, int height)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        Group = group;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
    }
}
