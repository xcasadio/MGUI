using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI.Docking.Controls
{
    /// <summary>
    /// Root container for the docking system. Manages the DockLayoutModel and renders the visual tree.
    /// </summary>
    public class MGDockHost : MGSingleContentHost
    {
        private DockLayoutModel _layoutModel;
        /// <summary>
        /// The layout model that defines the docking structure.
        /// </summary>
        public DockLayoutModel LayoutModel
        {
            get => _layoutModel;
            set
            {
                if (_layoutModel != value)
                {
                    // Unsubscribe from old model
                    if (_layoutModel != null)
                    {
                        _layoutModel.LayoutChanged -= OnLayoutModelChanged;
                        if (_layoutModel.RootNode != null)
                        {
                            UnsubscribeFromNode(_layoutModel.RootNode);
                        }
                    }

                    _layoutModel = value;

                    // Subscribe to new model
                    if (_layoutModel != null)
                    {
                        _layoutModel.LayoutChanged += OnLayoutModelChanged;
                        if (_layoutModel.RootNode != null)
                        {
                            SubscribeToNode(_layoutModel.RootNode);
                        }
                    }

                    NPC(nameof(LayoutModel));
                    RebuildVisualTree();
                }
            }
        }

        /// <summary>
        /// Registry of all panels by their ID for quick lookup.
        /// </summary>
        private readonly Dictionary<string, DockPanelNode> _panelRegistry = new Dictionary<string, DockPanelNode>();

        /// <summary>
        /// Event raised when a panel is added to the layout.
        /// </summary>
        public event EventHandler<DockPanelNode> PanelAdded;

        /// <summary>
        /// Event raised when a panel is removed from the layout.
        /// </summary>
        public event EventHandler<DockPanelNode> PanelRemoved;

        /// <summary>
        /// Event raised when the active panel in any tab group changes.
        /// </summary>
        public event EventHandler<DockPanelNode> ActivePanelChanged;

        /// <summary>
        /// Event raised when the layout structure changes.
        /// </summary>
        public event EventHandler DockLayoutChanged;

        #region Drag & Drop State

        private DockDragData _currentDrag;
        /// <summary>
        /// The current drag operation data, or null if no drag is in progress.
        /// </summary>
        public DockDragData CurrentDrag
        {
            get => _currentDrag;
            private set
            {
                if (_currentDrag != value)
                {
                    _currentDrag = value;
                    NPC(nameof(CurrentDrag));
                    NPC(nameof(IsDragging));
                }
            }
        }

        /// <summary>
        /// True if a drag operation is currently in progress.
        /// </summary>
        public bool IsDragging => CurrentDrag != null;

        /// <summary>
        /// Last mouse position where preview was calculated.
        /// Used to avoid recalculating drop targets too frequently.
        /// </summary>
        private Point _lastPreviewCalculation;

        #endregion Drag & Drop State

        /// <summary>
        /// Creates a new MGDockHost.
        /// </summary>
        /// <param name="window">The parent window.</param>
        public MGDockHost(MGWindow window) : base(window, MGElementType.Custom)
        {
            using (BeginInitializing())
            {
                // Initialize with empty layout model
                _layoutModel = new DockLayoutModel();
                _layoutModel.LayoutChanged += OnLayoutModelChanged;

                // Initialize preview overlay
                _previewOverlay = new MGDockPreviewOverlay(window);
                _previewOverlayComponent = new MGComponent<MGDockPreviewOverlay>(
                    _previewOverlay,
                    ComponentUpdatePriority.AfterContents,
                    ComponentDrawPriority.AfterContents,
                    true, true, false, false, false, false, false,
                    (AvailableBounds, ComponentSize) => AvailableBounds);
                AddComponent(_previewOverlayComponent);

                // Set default styling
                HorizontalAlignment = HorizontalAlignment.Stretch;
                VerticalAlignment = VerticalAlignment.Stretch;
            }
        }

        public override void UpdateSelf(ElementUpdateArgs UA)
        {
            base.UpdateSelf(UA);

            // Handle drag operation via polling
            if (IsDragging)
            {
                // Check if mouse button is still pressed
                bool isStillPressed = ParentWindow.Desktop.InputTracker.Mouse.CurrentState.LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed;

                if (!isStillPressed)
                {
                    // Mouse button released - end drag and perform drop
                    PerformDrop();
                    return;
                }

                // Get current mouse position
                Point currentMousePosition = ParentWindow.Desktop.InputTracker.Mouse.CurrentPosition;

                // Update drop preview
                UpdateDragPreview(currentMousePosition);
            }
        }

        /// <summary>
        /// Updates the drop preview during a drag operation.
        /// </summary>
        private void UpdateDragPreview(Point mousePosition)
        {
            // Optimization: skip if mouse hasn't moved significantly
            double distance = Math.Sqrt(
                Math.Pow(mousePosition.X - _lastPreviewCalculation.X, 2) +
                Math.Pow(mousePosition.Y - _lastPreviewCalculation.Y, 2));

            if (distance < 5)
                return;

            _lastPreviewCalculation = mousePosition;

            // Calculate drop target at current mouse position
            var dropTarget = GetDropTarget(mousePosition);

            if (dropTarget != null && dropTarget.Zone != DockZone.None)
            {
                // Valid drop target found - show preview
                ShowPreview(dropTarget.PreviewRect);
            }
            else
            {
                // No valid drop target - hide preview
                HidePreview();
            }
        }

        /// <summary>
        /// Performs the drop operation at the end of a drag.
        /// </summary>
        private void PerformDrop()
        {
            if (!IsDragging)
                return;

            try
            {
                // Get final mouse position
                Point mousePosition = ParentWindow.Desktop.InputTracker.Mouse.CurrentPosition;

                // Calculate final drop target
                var dropTarget = GetDropTarget(mousePosition);

                // Check if there's a valid drop target
                if (dropTarget != null && dropTarget.Zone != DockZone.None)
                {
                    ExecuteDrop(CurrentDrag, dropTarget);
                }
                // If no valid drop target, the tab remains in its source group (no action needed)
            }
            finally
            {
                // Always cleanup drag state via public EndDrag method
                EndDrag();
            }
        }

        /// <summary>
        /// Begins a drag operation for the specified panel.
        /// </summary>
        /// <param name="panel">The panel being dragged.</param>
        /// <param name="sourceGroup">The tab group from which the panel is being dragged.</param>
        /// <param name="startPos">The screen-space position where the drag started.</param>
        /// <param name="sourceItem">The visual tab item that initiated the drag.</param>
        public void BeginDrag(DockPanelNode panel, DockTabGroupNode sourceGroup, Point startPos, MGDockTabItem sourceItem)
        {
            if (panel == null)
                throw new ArgumentNullException(nameof(panel));
            if (sourceGroup == null)
                throw new ArgumentNullException(nameof(sourceGroup));
            if (sourceItem == null)
                throw new ArgumentNullException(nameof(sourceItem));

            // Create drag data
            CurrentDrag = new DockDragData(panel, sourceGroup, startPos, sourceItem);

            // Provide visual feedback on the source tab item
            sourceItem.Opacity = 0.5f;

            // Initialize preview calculation position
            _lastPreviewCalculation = startPos;
        }



        /// <summary>
        /// Ends the current drag operation and performs the drop.
        /// </summary>
        public void EndDrag()
        {
            if (CurrentDrag == null)
                return;

            // Restore visual state
            if (CurrentDrag.SourceTabItem != null)
            {
                CurrentDrag.SourceTabItem.Opacity = 1.0f;
            }

            // Hide preview
            HidePreview();

            // Clear drag state
            CurrentDrag = null;
            CurrentDropTarget = null;
        }

        /// <summary>
        /// Cancels the current drag operation without performing a drop.
        /// </summary>
        public void CancelDrag()
        {
            if (CurrentDrag == null)
                return;

            // Restore visual state
            if (CurrentDrag.SourceTabItem != null)
            {
                CurrentDrag.SourceTabItem.Opacity = 1.0f;
            }

            // Hide preview
            HidePreview();

            // Clear drag state
            CurrentDrag = null;
            CurrentDropTarget = null;
        }

        /// <summary>
        /// Executes the drop operation by applying the appropriate docking operation.
        /// </summary>
        /// <param name="drag">The drag data containing the dragged panel.</param>
        /// <param name="target">The drop target containing the target node and zone.</param>
        private void ExecuteDrop(DockDragData drag, DockDropTarget target)
        {
            if (drag == null || target == null)
                return;

            var panel = drag.DraggedPanel;
            var targetNode = target.TargetNode;

            switch (target.Zone)
            {
                case DockZone.Center:
                    // Dock as tab
                    if (targetNode is DockTabGroupNode targetGroup)
                    {
                        // Only move if source and target are different
                        if (drag.SourceGroup != targetGroup)
                        {
                            DockOperation.MoveTab(LayoutModel, panel, targetGroup, -1);
                        }
                        // If same group, it's a reorder operation (Phase 2 feature, skip for now)
                    }
                    break;

                case DockZone.Left:
                case DockZone.Right:
                case DockZone.Top:
                case DockZone.Bottom:
                    // Split dock: remove from source and create split
                    DockOperation.SplitDock(LayoutModel, panel, targetNode, target.Zone);
                    break;

                case DockZone.None:
                default:
                    // Invalid zone, do nothing
                    break;
            }

            // Rebuild visual tree to reflect changes
            RebuildVisualTree();
        }

        /// <summary>
        /// Registers a panel for use in the docking system.
        /// The panel can then be added to the layout via DockOperation methods.
        /// </summary>
        /// <param name="panel">The panel to register.</param>
        public void RegisterPanel(DockPanelNode panel)
        {
            if (panel == null)
                throw new ArgumentNullException(nameof(panel));

            if (_panelRegistry.ContainsKey(panel.Id))
                throw new InvalidOperationException($"A panel with ID '{panel.Id}' is already registered.");

            _panelRegistry[panel.Id] = panel;
            PanelAdded?.Invoke(this, panel);
        }

        /// <summary>
        /// Removes a panel from the docking system by ID.
        /// </summary>
        /// <param name="panelId">The ID of the panel to remove.</param>
        /// <returns>True if the panel was removed, false if not found.</returns>
        public bool RemovePanel(string panelId)
        {
            if (string.IsNullOrEmpty(panelId))
                return false;

            if (!_panelRegistry.TryGetValue(panelId, out var panel))
                return false;

            // Remove from layout model
            DockOperation.RemovePanel(LayoutModel, panel);

            // Remove from registry
            _panelRegistry.Remove(panelId);
            
            PanelRemoved?.Invoke(this, panel);

            return true;
        }

        /// <summary>
        /// Finds a registered panel by ID.
        /// </summary>
        /// <param name="id">The panel ID.</param>
        /// <returns>The panel node, or null if not found.</returns>
        public DockPanelNode FindPanel(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            _panelRegistry.TryGetValue(id, out var panel);
            return panel;
        }

        /// <summary>
        /// Gets all registered panels.
        /// </summary>
        /// <returns>Collection of all registered panels.</returns>
        public IEnumerable<DockPanelNode> GetAllPanels()
        {
            return _panelRegistry.Values;
        }

        /// <summary>
        /// Gets all tab groups currently in the layout.
        /// </summary>
        /// <returns>Collection of all DockTabGroupNode instances in the visual tree.</returns>
        public IEnumerable<DockTabGroupNode> GetAllTabGroups()
        {
            return LayoutModel?.GetAllTabGroups() ?? Enumerable.Empty<DockTabGroupNode>();
        }

        /// <summary>
        /// Rebuilds the entire visual tree from the layout model.
        /// Call this after making structural changes to the layout.
        /// </summary>
        public void RebuildVisualTree()
        {
            if (LayoutModel?.RootNode == null)
            {
                // No layout defined, show placeholder or empty content
                SetContent(CreateEmptyPlaceholder());
                return;
            }

            try
            {
                // Build visual tree from model
                MGElement visualRoot = BuildVisualTree(LayoutModel.RootNode);
                SetContent(visualRoot);
            }
            catch (Exception ex)
            {
                // If building fails, show error placeholder
                SetContent(CreateErrorPlaceholder(ex.Message));
            }
        }

        /// <summary>
        /// Recursively builds the visual tree from a dock node.
        /// </summary>
        /// <param name="node">The node to build visuals for.</param>
        /// <returns>The MGElement representing this node and its children.</returns>
        private MGElement BuildVisualTree(DockNode node)
        {
            if (node == null)
                return CreateEmptyPlaceholder();

            switch (node)
            {
                case DockSplitNode splitNode:
                    return BuildSplitContainer(splitNode);

                case DockTabGroupNode tabGroupNode:
                    return BuildTabGroup(tabGroupNode);

                case DockPanelNode panelNode:
                    // A panel node shouldn't be directly in the tree, it should be in a tab group
                    // Create a tab group with just this panel
                    var tempGroup = new DockTabGroupNode();
                    tempGroup.AddPanel(panelNode, -1);
                    return BuildTabGroup(tempGroup);

                default:
                    return CreateErrorPlaceholder($"Unknown node type: {node.GetType().Name}");
            }
        }

        /// <summary>
        /// Builds a split container visual from a DockSplitNode.
        /// </summary>
        private MGElement BuildSplitContainer(DockSplitNode splitNode)
        {
            var splitContainer = new MGDockSplitContainer(ParentWindow, splitNode.Orientation)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                SplitRatio = splitNode.SplitRatio,
                MinFirstSize = splitNode.MinFirstSize,
                MinSecondSize = splitNode.MinSecondSize
            };

            // Subscribe to split ratio changes to update the model
            splitContainer.SplitRatioChanged += (sender, newRatio) =>
            {
                if (splitNode.SplitRatio != newRatio)
                {
                    splitNode.SplitRatio = newRatio;
                }
            };

            // Recursively build children
            if (splitNode.FirstChild != null)
            {
                splitContainer.FirstChild = BuildVisualTree(splitNode.FirstChild);
            }

            if (splitNode.SecondChild != null)
            {
                splitContainer.SecondChild = BuildVisualTree(splitNode.SecondChild);
            }

            return splitContainer;
        }

        /// <summary>
        /// Builds a tab group visual from a DockTabGroupNode.
        /// </summary>
        private MGElement BuildTabGroup(DockTabGroupNode tabGroupNode)
        {
            var tabGroup = new MGDockTabGroup(ParentWindow, tabGroupNode)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            // Subscribe to panel close requests
            tabGroup.PanelCloseRequested += (sender, panelToClose) =>
            {
                if (panelToClose != null && LayoutModel != null)
                {
                    // Remove panel from layout model
                    DockOperation.RemovePanel(LayoutModel, panelToClose);
                    
                    // Rebuild visual tree to reflect changes
                    RebuildVisualTree();
                }
            };

            return tabGroup;
        }

        /// <summary>
        /// Creates a placeholder element for empty layouts.
        /// </summary>
        private MGElement CreateEmptyPlaceholder()
        {
            var textBlock = new MGTextBlock(ParentWindow, "Empty Docking Layout\n\nAdd panels via RegisterPanel() and DockOperation methods.")
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(20).ToThickness()
            };

            return textBlock;
        }

        /// <summary>
        /// Creates an error placeholder element.
        /// </summary>
        private MGElement CreateErrorPlaceholder(string errorMessage)
        {
            var textBlock = new MGTextBlock(ParentWindow, $"Error building docking layout:\n{errorMessage}")
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(20).ToThickness()
            };

            return textBlock;
        }

        /// <summary>
        /// Handles layout model changes by rebuilding the visual tree.
        /// </summary>
        private void OnLayoutModelChanged(object sender, EventArgs e)
        {
            RebuildVisualTree();
            DockLayoutChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Subscribes to PropertyChanged events for a node and its children.
        /// </summary>
        private void SubscribeToNode(DockNode node)
        {
            if (node == null)
                return;

            if (node is INotifyPropertyChanged notifyNode)
            {
                notifyNode.PropertyChanged += OnNodePropertyChanged;
            }

            // Subscribe to children
            foreach (var child in node.GetChildren())
            {
                if (child != null)
                    SubscribeToNode(child);
            }

            // Special handling for TabGroupNode to track active panel changes
            if (node is DockTabGroupNode tabGroup)
            {
                tabGroup.PropertyChanged += OnTabGroupPropertyChanged;
            }
        }

        /// <summary>
        /// Unsubscribes from PropertyChanged events for a node and its children.
        /// </summary>
        private void UnsubscribeFromNode(DockNode node)
        {
            if (node == null)
                return;

            if (node is INotifyPropertyChanged notifyNode)
            {
                notifyNode.PropertyChanged -= OnNodePropertyChanged;
            }

            // Unsubscribe from children
            foreach (var child in node.GetChildren())
            {
                if (child != null)
                    UnsubscribeFromNode(child);
            }

            if (node is DockTabGroupNode tabGroup)
            {
                tabGroup.PropertyChanged -= OnTabGroupPropertyChanged;
            }
        }

        private void OnNodePropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Node property changed, may need to rebuild
            // For now, just trigger a full rebuild
            // TODO: Optimize to do partial updates
            RebuildVisualTree();
        }

        private void OnTabGroupPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DockTabGroupNode.ActivePanelId) || 
                e.PropertyName == nameof(DockTabGroupNode.ActivePanel))
            {
                if (sender is DockTabGroupNode tabGroup && tabGroup.ActivePanel != null)
                {
                    ActivePanelChanged?.Invoke(this, tabGroup.ActivePanel);
                }
            }
        }

        #region Drop Zone Calculation

        private DockDropTarget _currentDropTarget;
        private MGDockPreviewOverlay _previewOverlay;
        private MGComponentBase _previewOverlayComponent;

        /// <summary>
        /// The current drop target based on the last mouse position.
        /// Updated by calling GetDropTarget().
        /// </summary>
        public DockDropTarget CurrentDropTarget
        {
            get => _currentDropTarget;
            private set
            {
                if (_currentDropTarget != value)
                {
                    _currentDropTarget = value;
                    NPC(nameof(CurrentDropTarget));
                }
            }
        }

        /// <summary>
        /// Calculates and returns the drop target at the specified screen position.
        /// Also updates CurrentDropTarget property.
        /// </summary>
        /// <param name="screenPosition">The screen position to test.</param>
        /// <returns>The drop target at the position, or null if none found.</returns>
        public DockDropTarget GetDropTarget(Point screenPosition)
        {
            DockDropTarget bestTarget = null;

            // Get all visible tab groups
            foreach (var tabGroup in GetAllVisibleTabGroups())
            {
                if (tabGroup == null || tabGroup.Visibility != Visibility.Visible)
                    continue;

                // Calculate drop zones for this tab group
                var zones = DockDropCalculator.CalculateDropZones(tabGroup, tabGroup.LayoutBounds);

                // Find matching zone at screen position
                var target = DockDropCalculator.GetDropTargetAtPosition(zones, screenPosition);
                if (target != null)
                {
                    bestTarget = target;
                    break; // Use first match (could be refined with Z-order)
                }
            }

            CurrentDropTarget = bestTarget;
            return bestTarget;
        }

        /// <summary>
        /// Recursively finds all visible MGDockTabGroup UI elements in the visual tree.
        /// </summary>
        /// <returns>Enumerable of all visible tab group controls.</returns>
        public IEnumerable<MGDockTabGroup> GetAllVisibleTabGroups()
        {
            if (Content == null)
                yield break;

            // Recursively search the visual tree
            foreach (var tabGroup in FindTabGroupsRecursive(Content))
            {
                yield return tabGroup;
            }
        }

        /// <summary>
        /// Shows the drag preview overlay at the specified bounds.
        /// </summary>
        /// <param name="bounds">The screen-space rectangle to display the preview.</param>
        public void ShowPreview(Microsoft.Xna.Framework.Rectangle bounds)
        {
            _previewOverlay.Show(bounds);
        }

        /// <summary>
        /// Hides the drag preview overlay.
        /// </summary>
        public void HidePreview()
        {
            _previewOverlay.Hide();
        }

        /// <summary>
        /// Helper method to recursively find tab groups in element hierarchy.
        /// </summary>
        private IEnumerable<MGDockTabGroup> FindTabGroupsRecursive(MGElement element)
        {
            if (element == null)
                yield break;

            // If this element is a tab group, return it
            if (element is MGDockTabGroup tabGroup)
            {
                yield return tabGroup;
            }

            // Recursively check children
            foreach (var child in element.GetChildren())
            {
                foreach (var childTabGroup in FindTabGroupsRecursive(child))
                {
                    yield return childTabGroup;
                }
            }
        }

        #endregion Drop Zone Calculation
    }
}
