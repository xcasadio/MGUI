using System;
using System.Linq;

namespace MGUI.Core.UI.Docking.DockLayout
{
    /// <summary>
    /// Static class providing operations to modify the docking layout tree.
    /// All operations maintain tree integrity and parent references.
    /// </summary>
    public static class DockOperation
    {
        /// <summary>
        /// Docks a panel as a tab in an existing tab group.
        /// </summary>
        /// <param name="model">The layout model to operate on.</param>
        /// <param name="panel">The panel to dock.</param>
        /// <param name="targetGroup">The target tab group.</param>
        /// <param name="index">Index where to insert the panel (-1 to append at end).</param>
        public static void DockAsTab(DockLayoutModel model, DockPanelNode panel, 
                                      DockTabGroupNode targetGroup, int index = -1)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (panel == null)
                throw new ArgumentNullException(nameof(panel));
            if (targetGroup == null)
                throw new ArgumentNullException(nameof(targetGroup));

            // Remove panel from its current parent if it has one
            if (panel.Parent is DockTabGroupNode currentGroup && currentGroup != targetGroup)
            {
                currentGroup.RemovePanel(panel);
                
                // Cleanup if the source group is now empty
                if (currentGroup.IsEmpty)
                {
                    CleanupEmptyTabGroup(model, currentGroup);
                }
            }

            // Add panel to target group
            targetGroup.AddPanel(panel, index);
            
            // Set as active panel
            targetGroup.SetActivePanel(panel.Id);
        }

        /// <summary>
        /// Docks a panel by splitting the target node in the specified direction.
        /// Creates a new split node and tab group for the panel.
        /// </summary>
        /// <param name="model">The layout model to operate on.</param>
        /// <param name="panel">The panel to dock.</param>
        /// <param name="targetNode">The node to split.</param>
        /// <param name="zone">The docking zone (Left/Right/Top/Bottom/Center).</param>
        public static void SplitDock(DockLayoutModel model, DockPanelNode panel, 
                                      DockNode targetNode, DockZone zone)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (panel == null)
                throw new ArgumentNullException(nameof(panel));
            if (targetNode == null)
                throw new ArgumentNullException(nameof(targetNode));

            // If docking to center, just add as tab
            if (zone == DockZone.Center)
            {
                if (targetNode is DockTabGroupNode tabGroup)
                {
                    DockAsTab(model, panel, tabGroup, -1);
                    return;
                }
                else
                {
                    throw new ArgumentException("Cannot dock to center of a non-TabGroup node.", nameof(targetNode));
                }
            }

            // Remove panel from current parent if it has one
            DockTabGroupNode emptyGroupToCleanup = null;
            
            if (panel.Parent is DockTabGroupNode currentGroup)
            {
                currentGroup.RemovePanel(panel);
                
                // Mark for cleanup later, after the new split is inserted
                if (currentGroup.IsEmpty && currentGroup != targetNode)
                {
                    emptyGroupToCleanup = currentGroup;
                }
            }

            // Create new tab group containing the panel
            var newTabGroup = new DockTabGroupNode();
            newTabGroup.AddPanel(panel, -1);

            // Determine split orientation and ratio based on zone
            Orientation orientation;
            float splitRatio;
            DockNode firstChild, secondChild;

            switch (zone)
            {
                case DockZone.Left:
                    orientation = Orientation.Horizontal;
                    splitRatio = 0.3f;
                    firstChild = newTabGroup;
                    secondChild = targetNode;
                    break;

                case DockZone.Right:
                    orientation = Orientation.Horizontal;
                    splitRatio = 0.7f;
                    firstChild = targetNode;
                    secondChild = newTabGroup;
                    break;

                case DockZone.Top:
                    orientation = Orientation.Vertical;
                    splitRatio = 0.3f;
                    firstChild = newTabGroup;
                    secondChild = targetNode;
                    break;

                case DockZone.Bottom:
                    orientation = Orientation.Vertical;
                    splitRatio = 0.7f;
                    firstChild = targetNode;
                    secondChild = newTabGroup;
                    break;

                default:
                    throw new ArgumentException($"Invalid zone for splitting: {zone}", nameof(zone));
            }

            // IMPORTANT: Save the parent reference BEFORE creating the split node
            // When we assign FirstChild/SecondChild, the setter will change targetNode.Parent
            var targetNodeParent = targetNode.Parent;
            
            // Create the split node
            var splitNode = new DockSplitNode
            {
                Orientation = orientation,
                SplitRatio = splitRatio,
                FirstChild = firstChild,
                SecondChild = secondChild
            };

            // Replace targetNode with splitNode in the tree using the saved parent
            if (targetNodeParent == null)
            {
                // targetNode was the root
                model.RootNode = splitNode;
            }
            else if (targetNodeParent is DockSplitNode splitParent)
            {
                if (splitParent.FirstChild == targetNode)
                {
                    splitParent.FirstChild = splitNode;
                }
                else if (splitParent.SecondChild == targetNode)
                {
                    splitParent.SecondChild = splitNode;
                }
            }
            
            // NOW cleanup the empty group after the new split is inserted
            if (emptyGroupToCleanup != null)
            {
                CleanupEmptyTabGroup(model, emptyGroupToCleanup);
            }
        }

        /// <summary>
        /// Moves a panel from its current tab group to a target tab group.
        /// </summary>
        /// <param name="model">The layout model to operate on.</param>
        /// <param name="panel">The panel to move.</param>
        /// <param name="targetGroup">The destination tab group.</param>
        /// <param name="index">Index where to insert the panel (-1 to append at end).</param>
        public static void MoveTab(DockLayoutModel model, DockPanelNode panel, 
                                   DockTabGroupNode targetGroup, int index)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (panel == null)
                throw new ArgumentNullException(nameof(panel));
            if (targetGroup == null)
                throw new ArgumentNullException(nameof(targetGroup));

            // Use DockAsTab which handles removal and cleanup automatically
            DockAsTab(model, panel, targetGroup, index);
        }

        /// <summary>
        /// Removes a panel from the layout and cleans up any empty nodes.
        /// </summary>
        /// <param name="model">The layout model to operate on.</param>
        /// <param name="panel">The panel to remove.</param>
        public static void RemovePanel(DockLayoutModel model, DockPanelNode panel)
        {
            if (model == null)
                throw new ArgumentNullException(nameof(model));
            if (panel == null)
                throw new ArgumentNullException(nameof(panel));

            // Find the parent tab group
            if (panel.Parent is not DockTabGroupNode tabGroup)
            {
                // Panel is not in a tab group, cannot remove
                return;
            }

            // Remove the panel
            tabGroup.RemovePanel(panel);

            // Cleanup if the group is now empty
            if (tabGroup.IsEmpty)
            {
                CleanupEmptyTabGroup(model, tabGroup);
            }
        }

        /// <summary>
        /// Cleans up an empty tab group by removing it from the tree and collapsing splits if necessary.
        /// </summary>
        private static void CleanupEmptyTabGroup(DockLayoutModel model, DockTabGroupNode emptyGroup)
        {
            if (emptyGroup == null || !emptyGroup.IsEmpty)
                return;

            var parent = emptyGroup.Parent;

            // If empty group is the root, just clear it or leave it
            if (parent == null)
            {
                if (model.RootNode == emptyGroup)
                {
                    // Root tab group is empty - can either clear or leave as placeholder
                    // For now, we'll leave it as an empty group
                    return;
                }
            }

            // If parent is a split node, collapse the split
            if (parent is DockSplitNode splitNode)
            {
                CollapseSplit(model, splitNode, emptyGroup);
            }
        }

        /// <summary>
        /// Collapses a split node by replacing it with its non-empty child.
        /// </summary>
        private static void CollapseSplit(DockLayoutModel model, DockSplitNode splitNode, DockNode emptyChild)
        {
            if (splitNode == null)
                return;

            // Get the sibling (non-empty child)
            DockNode sibling = splitNode.GetSibling(emptyChild);

            if (sibling == null)
            {
                // Both children are null/empty - this shouldn't happen, but handle it
                // Remove the split node entirely
                if (splitNode.Parent != null)
                {
                    ReplaceNodeInParent(model, splitNode, null);
                }
                else if (model.RootNode == splitNode)
                {
                    model.RootNode = null;
                }
                return;
            }

            // Replace the split node with the sibling in the parent
            ReplaceNodeInParent(model, splitNode, sibling);
        }

        /// <summary>
        /// Replaces a node with a new node in its parent.
        /// Handles root node replacement and parent reference updates.
        /// </summary>
        private static void ReplaceNodeInParent(DockLayoutModel model, DockNode oldNode, DockNode newNode)
        {
            if (oldNode == null)
                return;

            var parent = oldNode.Parent;

            if (parent == null)
            {
                // oldNode is the root
                model.RootNode = newNode;
                return;
            }

            if (parent is DockSplitNode splitParent)
            {
                if (splitParent.FirstChild == oldNode)
                {
                    splitParent.FirstChild = newNode;
                }
                else if (splitParent.SecondChild == oldNode)
                {
                    splitParent.SecondChild = newNode;
                }
            }
            else if (parent is DockTabGroupNode tabGroupParent)
            {
                // This shouldn't happen in normal flow
                // A node can't be directly replaced in a tab group
            }
        }

        /// <summary>
        /// Recursively cleans up empty nodes in the tree.
        /// Removes empty tab groups and collapses unnecessary split nodes.
        /// </summary>
        /// <param name="model">The layout model to clean.</param>
        public static void CleanupEmptyNodes(DockLayoutModel model)
        {
            if (model == null || model.RootNode == null)
                return;

            CleanupNodeRecursive(model, model.RootNode);
        }

        private static bool CleanupNodeRecursive(DockLayoutModel model, DockNode node)
        {
            if (node == null)
                return true; // Node is null, consider it "cleanable"

            // Handle TabGroup
            if (node is DockTabGroupNode tabGroup)
            {
                if (tabGroup.IsEmpty)
                {
                    // This tab group is empty
                    return true; // Signal parent to remove it
                }
                return false; // Tab group has content, keep it
            }

            // Handle SplitNode
            if (node is DockSplitNode splitNode)
            {
                bool firstCleanable = CleanupNodeRecursive(model, splitNode.FirstChild);
                bool secondCleanable = CleanupNodeRecursive(model, splitNode.SecondChild);

                if (firstCleanable && secondCleanable)
                {
                    // Both children are cleanable, this split is empty
                    return true;
                }
                else if (firstCleanable)
                {
                    // First child is cleanable, replace split with second child
                    ReplaceNodeInParent(model, splitNode, splitNode.SecondChild);
                    return false;
                }
                else if (secondCleanable)
                {
                    // Second child is cleanable, replace split with first child
                    ReplaceNodeInParent(model, splitNode, splitNode.FirstChild);
                    return false;
                }

                return false; // Split has valid children, keep it
            }

            // Panel nodes are always kept
            if (node is DockPanelNode)
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Gets the root ancestor of a node (traverses up to root).
        /// </summary>
        private static DockNode GetRoot(DockNode node)
        {
            if (node == null)
                return null;

            while (node.Parent != null)
            {
                node = node.Parent;
            }

            return node;
        }

        /// <summary>
        /// Validates that a dock operation would not create cycles or invalid states.
        /// </summary>
        public static bool ValidateDockOperation(DockPanelNode panel, DockNode targetNode)
        {
            if (panel == null || targetNode == null)
                return false;

            // Cannot dock a node onto itself
            if (panel == targetNode)
                return false;

            // Cannot dock onto a descendant (would create cycle)
            var current = targetNode;
            while (current != null)
            {
                if (current == panel)
                    return false;
                current = current.Parent;
            }

            return true;
        }
    }
}
