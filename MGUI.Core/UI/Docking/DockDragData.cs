using Microsoft.Xna.Framework;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking
{
    /// <summary>
    /// Contains information about an active drag operation of a dock panel tab.
    /// Used to track the dragged panel, its source location, and drag state.
    /// </summary>
    public class DockDragData
    {
        /// <summary>
        /// The panel being dragged.
        /// </summary>
        public DockPanelNode DraggedPanel { get; set; }

        /// <summary>
        /// The tab group from which the panel is being dragged.
        /// </summary>
        public DockTabGroupNode SourceGroup { get; set; }

        /// <summary>
        /// The screen-space position where the drag operation started.
        /// </summary>
        public Point DragStartPosition { get; set; }

        /// <summary>
        /// The visual tab item element that initiated the drag.
        /// </summary>
        public MGDockTabItem SourceTabItem { get; set; }

        /// <summary>
        /// Whether the drag threshold has been exceeded.
        /// Drag visuals only activate after moving beyond the threshold distance.
        /// </summary>
        public bool HasExceededThreshold { get; set; }

        /// <summary>
        /// Creates a new DockDragData.
        /// </summary>
        public DockDragData()
        {
        }

        /// <summary>
        /// Creates a new DockDragData with specified values.
        /// </summary>
        /// <param name="draggedPanel">The panel being dragged.</param>
        /// <param name="sourceGroup">The source tab group.</param>
        /// <param name="dragStartPosition">Start position of the drag.</param>
        /// <param name="sourceTabItem">The source tab item.</param>
        public DockDragData(DockPanelNode draggedPanel, DockTabGroupNode sourceGroup, 
                            Point dragStartPosition, MGDockTabItem sourceTabItem)
        {
            DraggedPanel = draggedPanel;
            SourceGroup = sourceGroup;
            DragStartPosition = dragStartPosition;
            SourceTabItem = sourceTabItem;
        }
    }
}
