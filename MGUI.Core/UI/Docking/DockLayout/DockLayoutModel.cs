using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace MGUI.Core.UI.Docking.DockLayout
{
    /// <summary>
    /// Root model for the docking layout system.
    /// Manages the tree structure of docking nodes and provides operations on the layout.
    /// </summary>
    public class DockLayoutModel : INotifyPropertyChanged
    {
        private DockNode _rootNode;
        /// <summary>
        /// Root node of the docking layout tree.
        /// Can be a DockSplitNode or DockTabGroupNode.
        /// </summary>
        public DockNode RootNode
        {
            get => _rootNode;
            set
            {
                if (_rootNode != value)
                {
                    // Unsubscribe from old root if exists
                    if (_rootNode != null)
                    {
                        UnsubscribeFromNodeTree(_rootNode);
                    }

                    _rootNode = value;

                    // Subscribe to new root
                    if (_rootNode != null)
                    {
                        SubscribeToNodeTree(_rootNode);
                        _rootNode.Parent = null; // Root has no parent
                    }

                    OnPropertyChanged(nameof(RootNode));
                    LayoutChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Event raised when the layout structure changes significantly.
        /// </summary>
        public event EventHandler LayoutChanged;

        /// <summary>
        /// Event raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Creates a new empty DockLayoutModel.
        /// </summary>
        public DockLayoutModel()
        {
        }

        /// <summary>
        /// Creates a new DockLayoutModel with specified root node.
        /// </summary>
        /// <param name="rootNode">Initial root node.</param>
        public DockLayoutModel(DockNode rootNode)
        {
            RootNode = rootNode;
        }

        /// <summary>
        /// Finds a node by its ID anywhere in the layout tree.
        /// </summary>
        /// <param name="id">The ID to search for.</param>
        /// <returns>The node with matching ID, or null if not found.</returns>
        public DockNode FindNodeById(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            return RootNode?.FindNodeById(id);
        }

        /// <summary>
        /// Finds a panel node by its ID.
        /// </summary>
        /// <param name="id">The panel ID to search for.</param>
        /// <returns>The DockPanelNode with matching ID, or null if not found.</returns>
        public DockPanelNode FindPanelById(string id)
        {
            return FindNodeById(id) as DockPanelNode;
        }

        /// <summary>
        /// Gets all panel nodes in the layout tree.
        /// </summary>
        /// <returns>Collection of all DockPanelNode instances.</returns>
        public IEnumerable<DockPanelNode> GetAllPanels()
        {
            if (RootNode == null)
                return Enumerable.Empty<DockPanelNode>();

            return GetAllPanelsRecursive(RootNode);
        }

        private IEnumerable<DockPanelNode> GetAllPanelsRecursive(DockNode node)
        {
            if (node is DockPanelNode panel)
            {
                yield return panel;
            }
            else
            {
                foreach (var child in node.GetChildren())
                {
                    if (child != null)
                    {
                        foreach (var childPanel in GetAllPanelsRecursive(child))
                        {
                            yield return childPanel;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Gets all tab group nodes in the layout tree.
        /// </summary>
        /// <returns>Collection of all DockTabGroupNode instances.</returns>
        public IEnumerable<DockTabGroupNode> GetAllTabGroups()
        {
            if (RootNode == null)
                return Enumerable.Empty<DockTabGroupNode>();

            return GetAllTabGroupsRecursive(RootNode);
        }

        private IEnumerable<DockTabGroupNode> GetAllTabGroupsRecursive(DockNode node)
        {
            if (node is DockTabGroupNode tabGroup)
            {
                yield return tabGroup;
            }

            foreach (var child in node.GetChildren())
            {
                if (child != null)
                {
                    foreach (var childGroup in GetAllTabGroupsRecursive(child))
                    {
                        yield return childGroup;
                    }
                }
            }
        }

        /// <summary>
        /// Validates the integrity of the layout tree.
        /// Checks for cycles, orphaned nodes, and invalid parent references.
        /// </summary>
        /// <returns>True if the tree is valid, false if issues were detected.</returns>
        public bool ValidateTree()
        {
            if (RootNode == null)
                return true; // Empty tree is valid

            var visited = new HashSet<string>();
            return ValidateNodeRecursive(RootNode, null, visited);
        }

        private bool ValidateNodeRecursive(DockNode node, DockNode expectedParent, HashSet<string> visited)
        {
            if (node == null)
                return true;

            // Check for cycles
            if (visited.Contains(node.Id))
                return false;

            visited.Add(node.Id);

            // Validate parent reference
            if (node.Parent != expectedParent)
                return false;

            // Validate children
            foreach (var child in node.GetChildren())
            {
                if (child != null)
                {
                    if (!ValidateNodeRecursive(child, node, visited))
                        return false;
                }
            }

            // Additional validation for SplitNode
            if (node is DockSplitNode splitNode)
            {
                // FirstChild and SecondChild might be temporarily null during construction
            }

            return true;
        }

        /// <summary>
        /// Subscribes to PropertyChanged events for all nodes in the subtree.
        /// Used to propagate layout changes.
        /// </summary>
        private void SubscribeToNodeTree(DockNode node)
        {
            if (node == null)
                return;

            node.PropertyChanged += OnNodePropertyChanged;

            foreach (var child in node.GetChildren())
            {
                if (child != null)
                    SubscribeToNodeTree(child);
            }
        }

        /// <summary>
        /// Unsubscribes from PropertyChanged events for all nodes in the subtree.
        /// </summary>
        private void UnsubscribeFromNodeTree(DockNode node)
        {
            if (node == null)
                return;

            node.PropertyChanged -= OnNodePropertyChanged;

            foreach (var child in node.GetChildren())
            {
                if (child != null)
                    UnsubscribeFromNodeTree(child);
            }
        }

        private void OnNodePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Propagate layout change notification
            // This allows the view layer to know when to rebuild
            LayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Clears the entire layout tree.
        /// </summary>
        public void Clear()
        {
            RootNode = null;
        }

        public override string ToString()
        {
            int panelCount = GetAllPanels().Count();
            int groupCount = GetAllTabGroups().Count();
            return $"DockLayoutModel (Panels: {panelCount}, Groups: {groupCount})";
        }
    }
}
