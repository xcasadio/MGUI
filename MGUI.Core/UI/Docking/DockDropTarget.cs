using Microsoft.Xna.Framework;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking
{
    /// <summary>
    /// Represents a potential drop target for a dragged panel.
    /// Contains information about where the panel would be docked and how to preview it.
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

        public override string ToString()
        {
            return $"DockDropTarget: Zone={Zone}, HitRect={HitRect}, PreviewRect={PreviewRect}";
        }
    }
}
