using Microsoft.Xna.Framework;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking;

/// <summary>
/// Represents a potential drop target for a dragged panel.
/// Contains information about where the panel would be docked and how to preview it.
/// The one that <see cref="Controls.MGDockHost.CurrentDropTarget"/> gives is kept by the host and written again at each computation (ADR-0036):
/// read its fields when needed, and copy them to keep them.
/// </summary>
public class DockDropTarget
{
    /// <summary>
    /// The dock node that would receive the dropped panel.
    /// Typically a DockTabGroupNode.
    /// </summary>
    public DockNode TargetNode { get; set; }

    /// <summary>
    /// The zone relative to the target node where the drop would occur.
    /// </summary>
    public DockZone Zone { get; set; }

    /// <summary>
    /// The hit-test rectangle in screen space.
    /// Used to determine if the mouse is over this drop target.
    /// </summary>
    public Rectangle HitRect { get; set; }

    /// <summary>
    /// The preview rectangle in screen space.
    /// Shows where the panel will be positioned after docking.
    /// </summary>
    public Rectangle PreviewRect { get; set; }

    /// <summary>
    /// The target tab index for tab reordering operations.
    /// Only used when dropping in the Center zone of the same group.
    /// -1 means append at the end.
    /// </summary>
    public int TabIndex { get; set; } = -1;

    /// <summary>
    /// When true, this target represents a host-level edge drop (at the root of the layout).
    /// The panel will be inserted as a new root split rather than relative to a specific node.
    /// <see cref="TargetNode"/> is null for host-edge targets.
    /// </summary>
    public bool IsHostEdge { get; set; }

    /// <summary>
    /// When true, the user dropped the panel onto a splitter bar between two existing children.
    /// <see cref="SplitterNode"/> carries the model-level split whose bar was hit, and
    /// <see cref="Zone"/> indicates which child the panel will be inserted next to.
    /// </summary>
    public bool IsSplitterDrop { get; set; }

    /// <summary>
    /// The split node whose splitter bar was targeted.
    /// Only valid when <see cref="IsSplitterDrop"/> is true.
    /// </summary>
    public DockSplitNode SplitterNode { get; set; }

    /// <summary>
    /// Creates a new DockDropTarget.
    /// </summary>
    public DockDropTarget()
    {
    }

    /// <summary>
    /// Creates a new DockDropTarget with specified values.
    /// </summary>
    public DockDropTarget(DockNode targetNode, DockZone zone, Rectangle hitRect, Rectangle previewRect)
    {
        TargetNode = targetNode;
        Zone = zone;
        HitRect = hitRect;
        PreviewRect = previewRect;
    }

    /// <summary>
    /// Writes the eight fields as a new object made with the four values would have them: <see cref="TabIndex"/> at -1, <see cref="IsHostEdge"/> and
    /// <see cref="IsSplitterDrop"/> false, <see cref="SplitterNode"/> null. A target that its owner keeps and fills again (ADR-0036) is filled with this method, so that
    /// a field written after the last fill (the index of a tab, the splitter of a splitter drop) never survives into the next one.
    /// </summary>
    internal void Set(DockNode targetNode, DockZone zone, Rectangle hitRect, Rectangle previewRect)
    {
        TargetNode = targetNode;
        Zone = zone;
        HitRect = hitRect;
        PreviewRect = previewRect;
        TabIndex = -1;
        IsHostEdge = false;
        IsSplitterDrop = false;
        SplitterNode = null;
    }

    /// <summary>Writes the eight fields of <paramref name="source"/> into this target.</summary>
    internal void CopyFrom(DockDropTarget source)
    {
        TargetNode = source.TargetNode;
        Zone = source.Zone;
        HitRect = source.HitRect;
        PreviewRect = source.PreviewRect;
        TabIndex = source.TabIndex;
        IsHostEdge = source.IsHostEdge;
        IsSplitterDrop = source.IsSplitterDrop;
        SplitterNode = source.SplitterNode;
    }

    /// <summary>Whether the eight fields of <paramref name="other"/> are the ones of this target (nodes by reference).</summary>
    internal bool HasSameValues(DockDropTarget other)
    {
        return ReferenceEquals(TargetNode, other.TargetNode)
            && Zone == other.Zone
            && HitRect == other.HitRect
            && PreviewRect == other.PreviewRect
            && TabIndex == other.TabIndex
            && IsHostEdge == other.IsHostEdge
            && IsSplitterDrop == other.IsSplitterDrop
            && ReferenceEquals(SplitterNode, other.SplitterNode);
    }

    public override string ToString()
    {
        return $"DockDropTarget: Zone={Zone}, HitRect={HitRect}, PreviewRect={PreviewRect}";
    }
}