using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MGUI.Core.UI.Docking.DockLayout
{
    /// <summary>
    /// Base class for all nodes in the docking layout tree.
    /// Represents a single element in the hierarchical docking structure.
    /// </summary>
    public abstract class DockNode : INotifyPropertyChanged
    {
        /// <summary>
        /// Unique identifier for this node. Generated as GUID by default.
        /// </summary>
        public string Id { get; }

        private DockNode _parent;
        /// <summary>
        /// Parent node in the docking tree. Null if this is the root node.
        /// </summary>
        public DockNode Parent
        {
            get => _parent;
            internal set
            {
                if (_parent != value)
                {
                    _parent = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Event raised when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        protected DockNode()
        {
            Id = Guid.NewGuid().ToString();
        }

        protected DockNode(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Node ID cannot be null or empty.", nameof(id));
            
            Id = id;
        }

        /// <summary>
        /// Gets all immediate children of this node.
        /// </summary>
        /// <returns>Collection of child nodes. Returns empty collection if no children.</returns>
        public abstract IEnumerable<DockNode> GetChildren();

        /// <summary>
        /// Removes the specified child node from this node.
        /// </summary>
        /// <param name="child">The child node to remove.</param>
        public abstract void RemoveChild(DockNode child);

        /// <summary>
        /// Raises the PropertyChanged event.
        /// </summary>
        /// <param name="propertyName">Name of the property that changed. Auto-populated by compiler.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Recursively searches for a node with the specified ID in this subtree.
        /// </summary>
        /// <param name="id">The ID to search for.</param>
        /// <returns>The node with matching ID, or null if not found.</returns>
        public DockNode FindNodeById(string id)
        {
            if (Id == id)
                return this;

            foreach (var child in GetChildren())
            {
                if (child == null)
                    continue;

                var result = child.FindNodeById(id);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Sets the parent of a child node. Used internally to maintain bidirectional relationships.
        /// </summary>
        /// <param name="child">The child node.</param>
        /// <param name="newParent">The new parent (typically 'this').</param>
        protected void SetChildParent(DockNode child, DockNode newParent)
        {
            if (child != null)
                child.Parent = newParent;
        }

        public override string ToString()
        {
            return $"{GetType().Name} (Id: {Id})";
        }
    }
}
