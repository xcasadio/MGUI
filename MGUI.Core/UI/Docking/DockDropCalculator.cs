using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking
{
    /// <summary>
    /// Static utility class for calculating drop zones during dock panel drag operations.
    /// </summary>
    public static class DockDropCalculator
    {
        /// <summary>
        /// Default margin size as a percentage of the smallest dimension (0.25 = 25%).
        /// </summary>
        public const float DefaultMarginPercent = 0.25f;

        /// <summary>
        /// Calculates all possible drop zones for a tab group.
        /// Returns 5 zones: Left, Right, Top, Bottom, and Center.
        /// </summary>
        /// <param name="tabGroup">The tab group that would receive the drop.</param>
        /// <param name="groupBounds">The screen-space bounds of the tab group.</param>
        /// <param name="marginPercent">The percentage of the smallest dimension to use for edge margins (default 0.25).</param>
        /// <returns>List of drop targets representing the 5 possible drop zones.</returns>
        public static List<DockDropTarget> CalculateDropZones(
            MGDockTabGroup tabGroup, 
            Rectangle groupBounds, 
            float marginPercent = DefaultMarginPercent)
        {
            var zones = new List<DockDropTarget>();

            if (tabGroup?.GroupNode == null || groupBounds.Width <= 0 || groupBounds.Height <= 0)
                return zones;

            // Calculate margin size (25% of the smallest dimension by default)
            int marginSize = (int)(Math.Min(groupBounds.Width, groupBounds.Height) * marginPercent);
            marginSize = Math.Max(marginSize, 30); // Minimum 30px for usability
            marginSize = Math.Min(marginSize, Math.Min(groupBounds.Width, groupBounds.Height) / 3); // Max 33% to leave room for center

            // LEFT ZONE
            zones.Add(new DockDropTarget
            {
                TargetNode = tabGroup.GroupNode,
                Zone = DockZone.Left,
                HitRect = new Rectangle(
                    groupBounds.X,
                    groupBounds.Y,
                    marginSize,
                    groupBounds.Height
                ),
                PreviewRect = new Rectangle(
                    groupBounds.X,
                    groupBounds.Y,
                    groupBounds.Width / 2,
                    groupBounds.Height
                )
            });

            // RIGHT ZONE
            zones.Add(new DockDropTarget
            {
                TargetNode = tabGroup.GroupNode,
                Zone = DockZone.Right,
                HitRect = new Rectangle(
                    groupBounds.Right - marginSize,
                    groupBounds.Y,
                    marginSize,
                    groupBounds.Height
                ),
                PreviewRect = new Rectangle(
                    groupBounds.X + groupBounds.Width / 2,
                    groupBounds.Y,
                    groupBounds.Width / 2,
                    groupBounds.Height
                )
            });

            // TOP ZONE
            zones.Add(new DockDropTarget
            {
                TargetNode = tabGroup.GroupNode,
                Zone = DockZone.Top,
                HitRect = new Rectangle(
                    groupBounds.X,
                    groupBounds.Y,
                    groupBounds.Width,
                    marginSize
                ),
                PreviewRect = new Rectangle(
                    groupBounds.X,
                    groupBounds.Y,
                    groupBounds.Width,
                    groupBounds.Height / 2
                )
            });

            // BOTTOM ZONE
            zones.Add(new DockDropTarget
            {
                TargetNode = tabGroup.GroupNode,
                Zone = DockZone.Bottom,
                HitRect = new Rectangle(
                    groupBounds.X,
                    groupBounds.Bottom - marginSize,
                    groupBounds.Width,
                    marginSize
                ),
                PreviewRect = new Rectangle(
                    groupBounds.X,
                    groupBounds.Y + groupBounds.Height / 2,
                    groupBounds.Width,
                    groupBounds.Height / 2
                )
            });

            // CENTER ZONE (takes the remaining middle area)
            zones.Add(new DockDropTarget
            {
                TargetNode = tabGroup.GroupNode,
                Zone = DockZone.Center,
                HitRect = new Rectangle(
                    groupBounds.X + marginSize,
                    groupBounds.Y + marginSize,
                    groupBounds.Width - 2 * marginSize,
                    groupBounds.Height - 2 * marginSize
                ),
                PreviewRect = groupBounds // Center doesn't change the size, just adds a tab
            });

            return zones;
        }

        /// <summary>
        /// Calculates drop zones for a dock node based on its bounds.
        /// Convenience method that creates a temporary representation.
        /// </summary>
        /// <param name="targetNode">The target dock node.</param>
        /// <param name="nodeBounds">The screen-space bounds of the node.</param>
        /// <param name="marginPercent">The percentage of the smallest dimension to use for edge margins.</param>
        /// <returns>List of drop targets.</returns>
        public static List<DockDropTarget> CalculateDropZones(
            DockNode targetNode,
            Rectangle nodeBounds,
            float marginPercent = DefaultMarginPercent)
        {
            var zones = new List<DockDropTarget>();

            if (targetNode == null || nodeBounds.Width <= 0 || nodeBounds.Height <= 0)
                return zones;

            int marginSize = (int)(Math.Min(nodeBounds.Width, nodeBounds.Height) * marginPercent);
            marginSize = Math.Max(marginSize, 30);
            marginSize = Math.Min(marginSize, Math.Min(nodeBounds.Width, nodeBounds.Height) / 3);

            // LEFT ZONE
            zones.Add(new DockDropTarget(
                targetNode,
                DockZone.Left,
                new Rectangle(nodeBounds.X, nodeBounds.Y, marginSize, nodeBounds.Height),
                new Rectangle(nodeBounds.X, nodeBounds.Y, nodeBounds.Width / 2, nodeBounds.Height)
            ));

            // RIGHT ZONE
            zones.Add(new DockDropTarget(
                targetNode,
                DockZone.Right,
                new Rectangle(nodeBounds.Right - marginSize, nodeBounds.Y, marginSize, nodeBounds.Height),
                new Rectangle(nodeBounds.X + nodeBounds.Width / 2, nodeBounds.Y, nodeBounds.Width / 2, nodeBounds.Height)
            ));

            // TOP ZONE
            zones.Add(new DockDropTarget(
                targetNode,
                DockZone.Top,
                new Rectangle(nodeBounds.X, nodeBounds.Y, nodeBounds.Width, marginSize),
                new Rectangle(nodeBounds.X, nodeBounds.Y, nodeBounds.Width, nodeBounds.Height / 2)
            ));

            // BOTTOM ZONE
            zones.Add(new DockDropTarget(
                targetNode,
                DockZone.Bottom,
                new Rectangle(nodeBounds.X, nodeBounds.Bottom - marginSize, nodeBounds.Width, marginSize),
                new Rectangle(nodeBounds.X, nodeBounds.Y + nodeBounds.Height / 2, nodeBounds.Width, nodeBounds.Height / 2)
            ));

            // CENTER ZONE
            zones.Add(new DockDropTarget(
                targetNode,
                DockZone.Center,
                new Rectangle(nodeBounds.X + marginSize, nodeBounds.Y + marginSize, 
                    nodeBounds.Width - 2 * marginSize, nodeBounds.Height - 2 * marginSize),
                nodeBounds
            ));

            return zones;
        }

        /// <summary>
        /// Finds the drop target at the specified screen position.
        /// Checks edge zones (Left, Right, Top, Bottom) first, then center.
        /// </summary>
        /// <param name="dropZones">The list of drop zones to check.</param>
        /// <param name="screenPosition">The screen position to test.</param>
        /// <returns>The matching drop target, or null if none found.</returns>
        public static DockDropTarget GetDropTargetAtPosition(List<DockDropTarget> dropZones, Point screenPosition)
        {
            if (dropZones == null || dropZones.Count == 0)
                return null;

            // Check edge zones first (they have priority)
            // Order: Left, Right, Top, Bottom, then Center
            foreach (var zone in dropZones)
            {
                if (zone.Zone != DockZone.Center && zone.HitRect.Contains(screenPosition))
                    return zone;
            }

            // Check center zone last
            foreach (var zone in dropZones)
            {
                if (zone.Zone == DockZone.Center && zone.HitRect.Contains(screenPosition))
                    return zone;
            }

            return null;
        }
    }
}
