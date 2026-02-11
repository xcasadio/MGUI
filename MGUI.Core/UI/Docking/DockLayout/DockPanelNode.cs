using System;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Core.UI.Docking.DockLayout
{
    /// <summary>
    /// Represents a leaf node in the docking layout - an individual panel (tool window or document).
    /// </summary>
    public class DockPanelNode : DockNode
    {
        private string _title;
        /// <summary>
        /// Display title of the panel (shown in tab header).
        /// </summary>
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }

        private object _icon;
        /// <summary>
        /// Icon for the panel. Can be a Texture2D, string path, or any other representation.
        /// Interpretation is left to the view layer.
        /// </summary>
        public object Icon
        {
            get => _icon;
            set
            {
                if (_icon != value)
                {
                    _icon = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Factory function to create the panel's content on-demand.
        /// Allows lazy initialization of panel content.
        /// </summary>
        public Func<MGElement> ContentFactory { get; set; }

        private bool _canClose;
        /// <summary>
        /// Indicates whether the panel can be closed by the user.
        /// Default: true.
        /// </summary>
        public bool CanClose
        {
            get => _canClose;
            set
            {
                if (_canClose != value)
                {
                    _canClose = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _canFloat;
        /// <summary>
        /// Indicates whether the panel can be detached into a floating window.
        /// Phase 2 feature. Default: true.
        /// </summary>
        public bool CanFloat
        {
            get => _canFloat;
            set
            {
                if (_canFloat != value)
                {
                    _canFloat = value;
                    OnPropertyChanged();
                }
            }
        }

        private bool _isPinned;
        /// <summary>
        /// Indicates whether the panel is pinned (always visible) or can auto-hide.
        /// Phase 2 feature. Default: true (pinned).
        /// </summary>
        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned != value)
                {
                    _isPinned = value;
                    OnPropertyChanged();
                }
            }
        }

        // Cache for content instance
        private MGElement _cachedContent;

        /// <summary>
        /// Creates a new DockPanelNode with default settings.
        /// </summary>
        public DockPanelNode() : base()
        {
            _title = "Untitled";
            _canClose = true;
            _canFloat = true;
            _isPinned = true;
        }

        /// <summary>
        /// Creates a new DockPanelNode with specified ID and default settings.
        /// </summary>
        /// <param name="id">Unique identifier for this node.</param>
        public DockPanelNode(string id) : base(id)
        {
            _title = "Untitled";
            _canClose = true;
            _canFloat = true;
            _isPinned = true;
        }

        /// <summary>
        /// Gets or creates the content for this panel.
        /// Content is created on first access using ContentFactory and then cached.
        /// </summary>
        /// <returns>The MGElement content, or null if ContentFactory is not set.</returns>
        public MGElement GetOrCreateContent()
        {
            if (_cachedContent == null && ContentFactory != null)
            {
                _cachedContent = ContentFactory();
            }
            return _cachedContent;
        }

        /// <summary>
        /// Clears the cached content, forcing recreation on next GetOrCreateContent() call.
        /// Useful for refreshing panel content or freeing resources.
        /// </summary>
        public void ClearCachedContent()
        {
            // Note: Consider calling RemoveDataBindings() on cached content if it exists
            // to prevent memory leaks from event subscriptions
            if (_cachedContent != null)
            {
                try
                {
                    // Attempt to clean up data bindings if method exists
                    _cachedContent.RemoveDataBindings(true);
                }
                catch
                {
                    // Ignore if method not available or fails
                }
            }
            
            _cachedContent = null;
        }

        /// <summary>
        /// Gets the currently cached content without creating it if it doesn't exist.
        /// </summary>
        /// <returns>The cached content, or null if not yet created.</returns>
        public MGElement GetCachedContent()
        {
            return _cachedContent;
        }

        /// <summary>
        /// Checks if the content has been created and cached.
        /// </summary>
        public bool IsContentCreated => _cachedContent != null;

        /// <summary>
        /// Panel nodes are leaf nodes and have no children.
        /// </summary>
        public override IEnumerable<DockNode> GetChildren()
        {
            return Enumerable.Empty<DockNode>();
        }

        /// <summary>
        /// Panel nodes have no children, so this is a no-op.
        /// </summary>
        public override void RemoveChild(DockNode child)
        {
            // Panel nodes don't have children
        }

        public override string ToString()
        {
            return $"Panel (Id: {Id}, Title: '{Title}', Content: {(IsContentCreated ? "Created" : "Not Created")})";
        }
    }
}
