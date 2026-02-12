using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;

namespace MGUI.Core.UI.Docking;

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
    /// <param name="isDraggingFromSameGroup">True if dragging a tab from this same group (for reordering).</param>
    /// <returns>List of drop targets representing the 5 possible drop zones.</returns>
    public static List<DockDropTarget> CalculateDropZones(
        MGDockTabGroup tabGroup,
        Rectangle groupBounds,
        float marginPercent = DefaultMarginPercent,
        bool isDraggingFromSameGroup = false)
    {
        var zones = new List<DockDropTarget>();

        if (tabGroup?.GroupNode == null || groupBounds.Width <= 0 || groupBounds.Height <= 0)
        {
            return zones;
        }

        // Calculate margin size (25% of the smallest dimension by default)
        int marginSize = (int)(Math.Min(groupBounds.Width, groupBounds.Height) * marginPercent);
        marginSize = Math.Max(marginSize, 30); // Minimum 30px for usability
        marginSize =
            Math.Min(marginSize,
                Math.Min(groupBounds.Width, groupBounds.Height) / 3); // Max 33% to leave room for center

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

        // CENTER ZONE (takes the remaining middle area for merging tabs)
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
        {
            return zones;
        }

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
            new Rectangle(nodeBounds.X + nodeBounds.Width / 2, nodeBounds.Y, nodeBounds.Width / 2,
                nodeBounds.Height)
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
            new Rectangle(nodeBounds.X, nodeBounds.Y + nodeBounds.Height / 2, nodeBounds.Width,
                nodeBounds.Height / 2)
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
        {
            return null;
        }

        // Check edge zones first (they have priority)
        // Order: Left, Right, Top, Bottom, then Center
        foreach (var zone in dropZones)
        {
            if (zone.Zone != DockZone.Center && zone.HitRect.Contains(screenPosition))
            {
                return zone;
            }
        }

        // Check center zone last
        foreach (var zone in dropZones)
        {
            if (zone.Zone == DockZone.Center && zone.HitRect.Contains(screenPosition))
            {
                return zone;
            }
        }

        return null;
    }

    /// <summary>
    /// Calculates the target tab index based on mouse position over the tab header area.
    /// Used for tab reordering within the same group.
    /// </summary>
    /// <param name="tabGroup">The tab group containing the tabs.</param>
    /// <param name="mouseX">The X screen coordinate of the mouse.</param>
    /// <param name="draggedPanel">The panel being dragged (to exclude from calculation).</param>
    /// <returns>The target index where the tab should be inserted, or -1 for end.</returns>
    public static int CalculateTabIndex(MGDockTabGroup tabGroup, int mouseX, DockPanelNode draggedPanel)
    {
        if (tabGroup?.GroupNode == null || tabGroup.GroupNode.Panels.Count <= 1)
        {
            return -1;
        }

        // We need to find which tab the mouse is over
        // Iterate through visible tab items and check their bounds
        var tabHeadersPanel = tabGroup.GetChildren().FirstOrDefault() as MGStackPanel;
        if (tabHeadersPanel == null)
        {
            return -1;
        }

        var tabItems = tabHeadersPanel.GetChildren().OfType<MGDockTabItem>().ToList();
        if (tabItems.Count == 0)
        {
            return -1;
        }

        // Find current index of dragged panel
        int draggedIndex = tabGroup.GroupNode.IndexOf(draggedPanel);
        if (draggedIndex < 0)
        {
            return -1; // Panel not in this group
        }

        // Find which tab position the mouse is closest to
        int targetIndex = 0;
        bool foundPosition = false;
        
        for (int i = 0; i < tabItems.Count; i++)
        {
            var tabItem = tabItems[i];
            var bounds = tabItem.LayoutBounds;
            int tabMidPoint = bounds.X + bounds.Width / 2;

            if (mouseX < tabMidPoint)
            {
                // Mouse is before the midpoint of this tab - insert before it
                targetIndex = i;
                foundPosition = true;
                break;
            }
        }

        // If not found, mouse is after all tabs - append at end
        if (!foundPosition)
        {
            targetIndex = tabItems.Count;
        }

        // Adjust for removal of dragged panel:
        // If we're dragging from before the target position, the target shifts left by 1
        if (draggedIndex < targetIndex)
        {
            targetIndex--;
        }

        // Clamp to valid range
        if (targetIndex < 0)
        {
            targetIndex = 0;
        }

        if (targetIndex >= tabGroup.GroupNode.Panels.Count)
        {
            targetIndex = tabGroup.GroupNode.Panels.Count - 1;
        }

        System.Diagnostics.Debug.WriteLine($"[CalculateTabIndex] mouseX={mouseX}, draggedIndex={draggedIndex}, targetIndex={targetIndex}, panelCount={tabGroup.GroupNode.Panels.Count}");

        return targetIndex;
    }

    /// <summary>
    /// Calculates a preview rectangle showing where a tab will be inserted during reordering.
    /// Returns a vertical line indicator between tabs.
    /// </summary>
    /// <param name="tabGroup">The tab group containing the tabs.</param>
    /// <param name="targetIndex">The final index where the tab will be inserted (after removal).</param>
    /// <param name="draggedPanel">The panel being dragged (to calculate visual position offset).</param>
    /// <returns>A rectangle representing the insertion point, or the full group bounds if calculation fails.</returns>
    public static Rectangle CalculateTabReorderPreviewRect(MGDockTabGroup tabGroup, int targetIndex, DockPanelNode draggedPanel)
    {
        if (tabGroup?.GroupNode == null)
        {
            return Rectangle.Empty;
        }

        // Get tab headers panel
        var tabHeadersPanel = tabGroup.GetChildren().FirstOrDefault() as MGStackPanel;
        if (tabHeadersPanel == null)
        {
            return tabGroup.LayoutBounds; // Fallback to full group
        }

        var tabItems = tabHeadersPanel.GetChildren().OfType<MGDockTabItem>().ToList();
        if (tabItems.Count == 0)
        {
            return tabGroup.LayoutBounds; // Fallback to full group
        }

        // Find current visual index of dragged panel
        int draggedIndex = -1;
        if (draggedPanel != null)
        {
            draggedIndex = tabGroup.GroupNode.IndexOf(draggedPanel);
        }

        // Convert final index to VISUAL index (compensate for dragged panel still being visible)
        int visualIndex = targetIndex;
        if (draggedIndex >= 0 && draggedIndex <= targetIndex)
        {
            // If dragged panel is BEFORE or AT the target position,
            // the visual position is one ahead (dragged panel not yet removed visually)
            visualIndex = targetIndex + 1;
        }

        System.Diagnostics.Debug.WriteLine($"[CalculateTabReorderPreviewRect] targetIndex={targetIndex}, draggedIndex={draggedIndex}, visualIndex={visualIndex}");

        // Calculate insertion position using VISUAL index
        int insertX;
        if (visualIndex <= 0)
        {
            // Insert at the beginning (left of first tab)
            insertX = tabItems[0].LayoutBounds.Left;
        }
        else if (visualIndex >= tabItems.Count)
        {
            // Insert at the end (right of last tab)
            insertX = tabItems[tabItems.Count - 1].LayoutBounds.Right;
        }
        else
        {
            // Insert between tabs (use left edge of visual target tab)
            insertX = tabItems[visualIndex].LayoutBounds.Left;
        }

        // Create a vertical line indicator (3px wide, full height of tab area)
        var headerBounds = tabGroup.TabHeadersBounds;
        int lineWidth = 3;
        Rectangle previewRect = new Rectangle(
            insertX - lineWidth / 2,
            headerBounds.Top,
            lineWidth,
            headerBounds.Height
        );

        System.Diagnostics.Debug.WriteLine($"[CalculateTabReorderPreviewRect] targetIndex={targetIndex}, insertX={insertX}, previewRect={previewRect}");

        return previewRect;
    }
}