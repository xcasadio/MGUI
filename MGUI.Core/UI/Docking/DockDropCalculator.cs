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
    /// Width in pixels of the edge detection band for host-level edge drops.
    /// </summary>
    public const int HostEdgeBandWidth = 40;

    /// <summary>
    /// Default fraction of the host dimension to reserve for a host-edge-docked panel.
    /// </summary>
    public const float HostEdgePreviewRatio = 0.25f;

    /// <summary>
    /// Calculates host-level drop zones along the four edges of the docking host.
    /// Each zone covers a <paramref name="edgeBandWidth"/>-pixel band and produces a
    /// preview rectangle that is <paramref name="previewRatio"/> of the host dimension.
    /// The returned targets have <see cref="DockDropTarget.IsHostEdge"/> = true and
    /// <see cref="DockDropTarget.TargetNode"/> = null (the existing root will be wrapped).
    /// </summary>
    /// <param name="hostBounds">Screen-space bounds of the MGDockHost.</param>
    /// <param name="edgeBandWidth">Pixel width of the hit-test band at each edge.</param>
    /// <param name="previewRatio">Fraction of the host to occupy in the drop preview.</param>
    /// <returns>Four <see cref="DockDropTarget"/> objects (Left, Right, Top, Bottom).</returns>
    public static List<DockDropTarget> CalculateHostEdgeZones(
        Rectangle hostBounds,
        int edgeBandWidth = HostEdgeBandWidth,
        float previewRatio = HostEdgePreviewRatio)
    {
        var targets = new List<DockDropTarget>(4);
        CalculateHostEdgeZonesInto(targets, hostBounds, edgeBandWidth, previewRatio);
        return targets;
    }

    /// <summary>
    /// <see cref="CalculateHostEdgeZones"/> into a list that its owner keeps: the targets that <paramref name="targets"/> already holds are filled again (all eight
    /// fields, see <see cref="DockDropTarget.Set"/>), missing ones are created, and the list ends with the zones of the call (no target when the host has no area).
    /// Allocates nothing once the list holds four targets.
    /// </summary>
    internal static void CalculateHostEdgeZonesInto(
        List<DockDropTarget> targets,
        Rectangle hostBounds,
        int edgeBandWidth = HostEdgeBandWidth,
        float previewRatio = HostEdgePreviewRatio)
    {
        if (hostBounds.Width <= 0 || hostBounds.Height <= 0)
        {
            Trim(targets, 0);
            return;
        }

        var halfW = (int)(hostBounds.Width  * previewRatio);
        var halfH = (int)(hostBounds.Height * previewRatio);

        // Left edge
        SetHostEdge(Slot(targets, 0), DockZone.Left,
            hitRect:     new Rectangle(hostBounds.X, hostBounds.Y, edgeBandWidth, hostBounds.Height),
            previewRect: new Rectangle(hostBounds.X, hostBounds.Y, halfW, hostBounds.Height));

        // Right edge
        SetHostEdge(Slot(targets, 1), DockZone.Right,
            hitRect:     new Rectangle(hostBounds.Right - edgeBandWidth, hostBounds.Y, edgeBandWidth, hostBounds.Height),
            previewRect: new Rectangle(hostBounds.Right - halfW, hostBounds.Y, halfW, hostBounds.Height));

        // Top edge
        SetHostEdge(Slot(targets, 2), DockZone.Top,
            hitRect:     new Rectangle(hostBounds.X, hostBounds.Y, hostBounds.Width, edgeBandWidth),
            previewRect: new Rectangle(hostBounds.X, hostBounds.Y, hostBounds.Width, halfH));

        // Bottom edge
        SetHostEdge(Slot(targets, 3), DockZone.Bottom,
            hitRect:     new Rectangle(hostBounds.X, hostBounds.Bottom - edgeBandWidth, hostBounds.Width, edgeBandWidth),
            previewRect: new Rectangle(hostBounds.X, hostBounds.Bottom - halfH, hostBounds.Width, halfH));

        Trim(targets, 4);
    }

    private static void SetHostEdge(DockDropTarget target, DockZone zone, Rectangle hitRect, Rectangle previewRect)
    {
        target.Set(null, zone, hitRect, previewRect);
        target.IsHostEdge = true;
    }

    /// <summary>The target at <paramref name="index"/> of a list that is filled in order: the one it holds, or a new one added at the end.</summary>
    private static DockDropTarget Slot(List<DockDropTarget> targets, int index)
    {
        if (index == targets.Count)
        {
            targets.Add(new DockDropTarget());
        }

        return targets[index];
    }

    /// <summary>Removes the targets of a kept list that the last call did not fill.</summary>
    private static void Trim(List<DockDropTarget> targets, int count)
    {
        if (targets.Count > count)
        {
            targets.RemoveRange(count, targets.Count - count);
        }
    }

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
        CalculateDropZonesInto(zones, tabGroup, groupBounds, marginPercent);
        return zones;
    }

    /// <summary>
    /// <see cref="CalculateDropZones(MGDockTabGroup, Rectangle, float, bool)"/> into a list that its owner keeps: the targets that <paramref name="zones"/> already holds
    /// are filled again (all eight fields, see <see cref="DockDropTarget.Set"/>), missing ones are created, and the list ends with the zones of the call (no zone when
    /// the group has no node or no area). Allocates nothing once the list holds five targets.
    /// </summary>
    internal static void CalculateDropZonesInto(
        List<DockDropTarget> zones,
        MGDockTabGroup tabGroup,
        Rectangle groupBounds,
        float marginPercent = DefaultMarginPercent)
    {
        if (tabGroup?.GroupNode == null || groupBounds.Width <= 0 || groupBounds.Height <= 0)
        {
            Trim(zones, 0);
            return;
        }

        // Calculate margin size (25% of the smallest dimension by default)
        var marginSize = (int)(Math.Min(groupBounds.Width, groupBounds.Height) * marginPercent);
        marginSize = Math.Max(marginSize, 30); // Minimum 30px for usability
        marginSize =
            Math.Min(marginSize,
                Math.Min(groupBounds.Width, groupBounds.Height) / 3); // Max 33% to leave room for center

        // LEFT ZONE
        Slot(zones, 0).Set(
            tabGroup.GroupNode,
            DockZone.Left,
            new Rectangle(
                groupBounds.X,
                groupBounds.Y,
                marginSize,
                groupBounds.Height
            ),
            new Rectangle(
                groupBounds.X,
                groupBounds.Y,
                groupBounds.Width / 2,
                groupBounds.Height
            ));

        // RIGHT ZONE
        Slot(zones, 1).Set(
            tabGroup.GroupNode,
            DockZone.Right,
            new Rectangle(
                groupBounds.Right - marginSize,
                groupBounds.Y,
                marginSize,
                groupBounds.Height
            ),
            new Rectangle(
                groupBounds.X + groupBounds.Width / 2,
                groupBounds.Y,
                groupBounds.Width / 2,
                groupBounds.Height
            ));

        // TOP ZONE
        Slot(zones, 2).Set(
            tabGroup.GroupNode,
            DockZone.Top,
            new Rectangle(
                groupBounds.X,
                groupBounds.Y,
                groupBounds.Width,
                marginSize
            ),
            new Rectangle(
                groupBounds.X,
                groupBounds.Y,
                groupBounds.Width,
                groupBounds.Height / 2
            ));

        // BOTTOM ZONE
        Slot(zones, 3).Set(
            tabGroup.GroupNode,
            DockZone.Bottom,
            new Rectangle(
                groupBounds.X,
                groupBounds.Bottom - marginSize,
                groupBounds.Width,
                marginSize
            ),
            new Rectangle(
                groupBounds.X,
                groupBounds.Y + groupBounds.Height / 2,
                groupBounds.Width,
                groupBounds.Height / 2
            ));

        // CENTER ZONE (takes the remaining middle area for merging tabs)
        Slot(zones, 4).Set(
            tabGroup.GroupNode,
            DockZone.Center,
            new Rectangle(
                groupBounds.X + marginSize,
                groupBounds.Y + marginSize,
                groupBounds.Width - 2 * marginSize,
                groupBounds.Height - 2 * marginSize
            ),
            groupBounds); // Center doesn't change the size, just adds a tab

        Trim(zones, 5);
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

        var marginSize = (int)(Math.Min(nodeBounds.Width, nodeBounds.Height) * marginPercent);
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
        return CalculateTabIndex(tabGroup, mouseX, draggedPanel, new List<MGElement>());
    }

    /// <summary>
    /// <see cref="CalculateTabIndex(MGDockTabGroup, int, DockPanelNode)"/> with a list that the caller keeps for the walk of the tab items (cleared and filled here, so that
    /// the call allocates nothing).
    /// </summary>
    internal static int CalculateTabIndex(MGDockTabGroup tabGroup, int mouseX, DockPanelNode draggedPanel, List<MGElement> tabItems)
    {
        if (tabGroup?.GroupNode == null || tabGroup.GroupNode.Panels.Count <= 1)
        {
            return -1;
        }

        // We need to find which tab the mouse is over
        // Iterate through visible tab items and check their bounds
        CollectTabItems(tabGroup, tabItems);
        if (tabItems.Count == 0)
        {
            return -1;
        }

        // Find current index of dragged panel
        var draggedIndex = tabGroup.GroupNode.IndexOf(draggedPanel);
        if (draggedIndex < 0)
        {
            return -1; // Panel not in this group
        }

        // Find which tab position the mouse is closest to
        var targetIndex = 0;
        var foundPosition = false;
        
        for (var i = 0; i < tabItems.Count; i++)
        {
            var tabItem = (MGDockTabItem)tabItems[i];
            var bounds = tabItem.LayoutBounds;
            var tabMidPoint = bounds.X + bounds.Width / 2;

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
        return CalculateTabReorderPreviewRect(tabGroup, targetIndex, draggedPanel, new List<MGElement>());
    }

    /// <summary>
    /// <see cref="CalculateTabReorderPreviewRect(MGDockTabGroup, int, DockPanelNode)"/> with a list that the caller keeps for the walk of the tab items (cleared and filled
    /// here, so that the call allocates nothing).
    /// </summary>
    internal static Rectangle CalculateTabReorderPreviewRect(MGDockTabGroup tabGroup, int targetIndex, DockPanelNode draggedPanel, List<MGElement> tabItems)
    {
        if (tabGroup?.GroupNode == null)
        {
            return Rectangle.Empty;
        }

        // Get the tab items of the tab headers panel
        CollectTabItems(tabGroup, tabItems);
        if (tabItems.Count == 0)
        {
            return tabGroup.LayoutBounds; // Fallback to full group
        }

        // Find current visual index of dragged panel
        var draggedIndex = -1;
        if (draggedPanel != null)
        {
            draggedIndex = tabGroup.GroupNode.IndexOf(draggedPanel);
        }

        // Convert final index to VISUAL index (compensate for dragged panel still being visible)
        var visualIndex = targetIndex;
        if (draggedIndex >= 0 && draggedIndex <= targetIndex)
        {
            // If dragged panel is BEFORE or AT the target position,
            // the visual position is one ahead (dragged panel not yet removed visually)
            visualIndex = targetIndex + 1;
        }

        // Calculate insertion position using VISUAL index
        int insertX;
        if (visualIndex <= 0)
        {
            insertX = tabItems[0].LayoutBounds.Left;
        }
        else if (visualIndex >= tabItems.Count)
        {
            insertX = tabItems[tabItems.Count - 1].LayoutBounds.Right;
        }
        else
        {
            insertX = tabItems[visualIndex].LayoutBounds.Left;
        }

        // Create a vertical line indicator (3px wide, full height of tab area)
        var headerBounds = tabGroup.TabHeadersBounds;
        var lineWidth = 3;
        var previewRect = new Rectangle(
            insertX - lineWidth / 2,
            headerBounds.Top,
            lineWidth,
            headerBounds.Height
        );

        return previewRect;
    }

    /// <summary>
    /// Fills <paramref name="tabItems"/> (cleared first) with the <see cref="MGDockTabItem"/> children of the tab headers panel of <paramref name="tabGroup"/> (the first
    /// child of the group when it is a stack panel), in order. Left empty when the group has no such panel. The same walk as
    /// <c>GetChildren().FirstOrDefault()</c> then <c>GetChildren().OfType&lt;MGDockTabItem&gt;()</c>, without the iterators and lists it allocates.
    /// </summary>
    private static void CollectTabItems(MGDockTabGroup tabGroup, List<MGElement> tabItems)
    {
        tabItems.Clear();
        tabGroup.AddChildrenTo(tabItems);
        var tabHeadersPanel = tabItems.Count > 0 ? tabItems[0] as MGStackPanel : null;
        tabItems.Clear();
        if (tabHeadersPanel == null)
        {
            return;
        }

        tabHeadersPanel.AddChildrenTo(tabItems);
        for (var i = tabItems.Count - 1; i >= 0; i--)
        {
            if (tabItems[i] is not MGDockTabItem)
            {
                tabItems.RemoveAt(i);
            }
        }
    }
}
