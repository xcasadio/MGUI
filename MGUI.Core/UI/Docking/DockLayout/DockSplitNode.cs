using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI.Docking.DockLayout
{
    /// <summary>
    /// Represents a split container in the docking layout that divides space between two child nodes.
    /// Can be oriented horizontally (left/right) or vertically (top/bottom).
    /// </summary>
    public class DockSplitNode : DockNode
    {
        private Orientation _orientation;
        /// <summary>
        /// Orientation of the split: Horizontal (left/right) or Vertical (top/bottom).
        /// </summary>
        public Orientation Orientation
        {
            get => _orientation;
            set
            {
                if (_orientation != value)
                {
                    _orientation = value;
                    OnPropertyChanged();
                }
            }
        }

        private DockNode _firstChild;
        /// <summary>
        /// First child node (left child for Horizontal, top child for Vertical).
        /// </summary>
        public DockNode FirstChild
        {
            get => _firstChild;
            set
            {
                if (_firstChild != value)
                {
                    SetChildParent(_firstChild, null);
                    _firstChild = value;
                    SetChildParent(_firstChild, this);
                    OnPropertyChanged();
                }
            }
        }

        private DockNode _secondChild;
        /// <summary>
        /// Second child node (right child for Horizontal, bottom child for Vertical).
        /// </summary>
        public DockNode SecondChild
        {
            get => _secondChild;
            set
            {
                if (_secondChild != value)
                {
                    SetChildParent(_secondChild, null);
                    _secondChild = value;
                    SetChildParent(_secondChild, this);
                    OnPropertyChanged();
                }
            }
        }

        private float _splitRatio;
        /// <summary>
        /// Split ratio determining how much space is allocated to the FirstChild (0.0 to 1.0).
        /// For example, 0.3 means FirstChild gets 30% of available space, SecondChild gets 70%.
        /// </summary>
        public float SplitRatio
        {
            get => _splitRatio;
            set
            {
                // Clamp to valid range
                float clampedValue = Math.Clamp(value, 0.0f, 1.0f);
                
                if (Math.Abs(_splitRatio - clampedValue) > 0.001f) // Avoid floating point comparison issues
                {
                    _splitRatio = clampedValue;
                    OnPropertyChanged();
                }
            }
        }

        private int _minFirstSize;
        /// <summary>
        /// Minimum size in pixels for the FirstChild. Used to prevent collapse during resize.
        /// Default: 100 pixels.
        /// </summary>
        public int MinFirstSize
        {
            get => _minFirstSize;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(MinFirstSize), "Minimum size cannot be negative.");
                
                if (_minFirstSize != value)
                {
                    _minFirstSize = value;
                    OnPropertyChanged();
                }
            }
        }

        private int _minSecondSize;
        /// <summary>
        /// Minimum size in pixels for the SecondChild. Used to prevent collapse during resize.
        /// Default: 100 pixels.
        /// </summary>
        public int MinSecondSize
        {
            get => _minSecondSize;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(MinSecondSize), "Minimum size cannot be negative.");
                
                if (_minSecondSize != value)
                {
                    _minSecondSize = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Creates a new DockSplitNode with default settings (50/50 split, horizontal orientation).
        /// </summary>
        public DockSplitNode() : base()
        {
            _orientation = Orientation.Horizontal;
            _splitRatio = 0.5f;
            _minFirstSize = 100;
            _minSecondSize = 100;
        }

        /// <summary>
        /// Creates a new DockSplitNode with specified ID and default settings.
        /// </summary>
        /// <param name="id">Unique identifier for this node.</param>
        public DockSplitNode(string id) : base(id)
        {
            _orientation = Orientation.Horizontal;
            _splitRatio = 0.5f;
            _minFirstSize = 100;
            _minSecondSize = 100;
        }

        /// <summary>
        /// Gets all immediate children (FirstChild and SecondChild, excluding nulls).
        /// </summary>
        public override IEnumerable<DockNode> GetChildren()
        {
            if (FirstChild != null)
                yield return FirstChild;
            if (SecondChild != null)
                yield return SecondChild;
        }

        /// <summary>
        /// Removes the specified child node. Sets the child reference to null.
        /// </summary>
        /// <param name="child">The child node to remove.</param>
        public override void RemoveChild(DockNode child)
        {
            if (child == null)
                return;

            if (FirstChild == child)
                FirstChild = null;
            else if (SecondChild == child)
                SecondChild = null;
        }

        /// <summary>
        /// Validates the current state of the split node.
        /// </summary>
        /// <returns>True if valid (both children present), false otherwise.</returns>
        public bool IsValid()
        {
            return FirstChild != null && SecondChild != null;
        }

        /// <summary>
        /// Gets the sibling of the specified child node.
        /// </summary>
        /// <param name="child">The child whose sibling to retrieve.</param>
        /// <returns>The sibling node, or null if child not found or no sibling.</returns>
        public DockNode GetSibling(DockNode child)
        {
            if (child == null)
                return null;

            if (FirstChild == child)
                return SecondChild;
            if (SecondChild == child)
                return FirstChild;

            return null;
        }

        public override string ToString()
        {
            return $"SplitNode (Id: {Id}, Orientation: {Orientation}, Ratio: {SplitRatio:F2})";
        }
    }
}
