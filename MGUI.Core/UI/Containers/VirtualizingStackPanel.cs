using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MGUI.Core.UI.Containers
{
    /// <summary>A vertical stack panel that only creates and maintains UI elements for items currently visible in the viewport,
    /// dramatically reducing memory and CPU cost for large lists.<para/>
    /// Items outside the viewport are recycled and returned to a pool. Items entering the viewport are either dequeued from
    /// the pool or freshly created via <see cref="ItemGenerator"/>.<para/>
    /// Only supports a vertical (top-to-bottom) orientation with a uniform item height (<see cref="UniformItemHeight"/>).</summary>
    public class VirtualizingStackPanel : MGMultiContentHost
    {
        #region Border
        /// <summary>Provides direct access to this element's border.</summary>
        public MGComponent<MGBorder> BorderComponent { get; }
        private MGBorder BorderElement { get; }
        public override MGBorder GetBorder() => BorderElement;

        public IBorderBrush BorderBrush
        {
            get => BorderElement.BorderBrush;
            set => BorderElement.BorderBrush = value;
        }

        public Thickness BorderThickness
        {
            get => BorderElement.BorderThickness;
            set => BorderElement.BorderThickness = value;
        }

        public MGCornerRadius CornerRadius
        {
            get => BorderElement.CornerRadius;
            set => BorderElement.CornerRadius = value;
        }
        #endregion Border

        #region Virtual data
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _totalItemCount;
        /// <summary>The total number of logical items in the virtual list (including those not yet created as UI elements).</summary>
        public int TotalItemCount
        {
            get => _totalItemCount;
            set
            {
                if (_totalItemCount != value)
                {
                    _totalItemCount = Math.Max(0, value);
                    LayoutChanged(this, true);
                    NPC(nameof(TotalItemCount));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _uniformItemHeight = 24;
        /// <summary>The height (in pixels) of each item. All items must have the same height.<para/>
        /// Default value: 24</summary>
        public int UniformItemHeight
        {
            get => _uniformItemHeight;
            set
            {
                if (_uniformItemHeight != value)
                {
                    _uniformItemHeight = Math.Max(1, value);
                    // Recycle all currently realized items so they will be re-laid-out with the new height
                    RecycleAllItems();
                    LayoutChanged(this, true);
                    NPC(nameof(UniformItemHeight));
                }
            }
        }

        /// <summary>The number of additional items to realize above and below the visible viewport.
        /// A small buffer prevents flickering during fast scrolling.<para/>
        /// Default value: 2</summary>
        public int BufferCount { get; set; } = 2;
        #endregion Virtual data

        #region Callbacks
        /// <summary>Invoked when a logical item needs to be presented in the viewport.<para/>
        /// The returned <see cref="MGElement"/> will be added as a child of this panel and positioned at the correct layout bounds.<br/>
        /// The caller may check <see cref="TryDequeueRecycledElement"/> to obtain a previously-recycled element instead of
        /// creating a new one, then update its content to reflect the new logical item.</summary>
        public Func<int, MGElement> ItemGenerator { get; set; }

        /// <summary>Invoked just before a realized item is removed from the visual tree and placed back in the recycle pool.
        /// The caller should unbind data, reset state, etc.</summary>
        public Action<int, MGElement> ItemRecycler { get; set; }

        /// <summary>Tries to dequeue a previously-used element from the internal recycle pool.<br/>
        /// Call this from inside <see cref="ItemGenerator"/> to reuse an existing element rather than allocating a new one.</summary>
        public bool TryDequeueRecycledElement(out MGElement element)
            => _recyclePool.TryDequeue(out element);
        #endregion Callbacks

        #region Realized / recycled items
        /// <summary>Map from logical index → realized <see cref="MGElement"/>.</summary>
        private readonly Dictionary<int, MGElement> _realizedItems = new();
        private readonly Queue<MGElement> _recyclePool = new();

        /// <summary>The first (lowest) logical index that is currently realized, or -1 if nothing is realized.</summary>
        public int FirstRealizedIndex { get; private set; } = -1;
        /// <summary>The last (highest) logical index that is currently realized, or -1 if nothing is realized.</summary>
        public int LastRealizedIndex { get; private set; } = -1;

        // Cached visible range from the last UpdateContentLayout call.
        // When the range hasn't changed we skip all realize/recycle work.
        private int _cachedFirstNeeded = -1;
        private int _cachedLastNeeded  = -1;

        private MGScrollViewer _parentScrollViewer;

        private void EnsureScrollViewerAttached()
        {
            if (_parentScrollViewer != null)
            {
                return;
            }

            if (TryFindParentOfType<MGScrollViewer>(out MGScrollViewer sv))
            {
                _parentScrollViewer = sv;
                _parentScrollViewer.VerticalOffsetChanged += (s, e) => LayoutChanged(this, true);
            }
        }

        private void RecycleAllItems()
        {
            // Collect indices first to avoid modifying the dictionary during iteration
            int[] indices = new int[_realizedItems.Count];
            _realizedItems.Keys.CopyTo(indices, 0);
            foreach (int idx in indices)
            {
                RecycleItem(idx);
            }

            FirstRealizedIndex    = -1;
            LastRealizedIndex     = -1;
            _cachedFirstNeeded    = -1;
            _cachedLastNeeded     = -1;
        }

        /// <summary>Forces all currently realized items to be recycled and re-created on the next layout pass.
        /// Call this after replacing the data source (i.e. after reassigning <see cref="ItemGenerator"/> and <see cref="ItemRecycler"/>)
        /// to discard stale rendered items whose cached range would otherwise prevent re-rendering.</summary>
        public void InvalidateData()
        {
            RecycleAllItems();
            LayoutChanged(this, true);
        }

        private void RealizeItem(int index)
        {
            if (ItemGenerator == null)
            {
                return;
            }

            MGElement element = ItemGenerator(index);
            if (element == null)
            {
                return;
            }

            if (element.Parent != this)
            {
                _Children.Add(element);
            }

            _realizedItems[index] = element;
        }

        private void RecycleItem(int index)
        {
            if (!_realizedItems.TryGetValue(index, out MGElement element))
            {
                return;
            }

            _realizedItems.Remove(index);
            ItemRecycler?.Invoke(index, element);
            _Children.Remove(element);
            _recyclePool.Enqueue(element);
        }
        #endregion Realized / recycled items

        public VirtualizingStackPanel(MGWindow window)
            : base(window, MGElementType.VirtualizingStackPanel)
        {
            using (BeginInitializing())
            {
                BorderElement = new MGBorder(window);
                BorderComponent = MGComponentBase.Create(BorderElement);
                AddComponent(BorderComponent);
                BorderElement.OnCornerRadiusChanged += (sender, e) => { NPC(nameof(CornerRadius)); };

                VerticalAlignment = VerticalAlignment.Top;
                CanChangeContent = false;
            }
        }

        protected override Thickness UpdateContentMeasurement(Size availableSize)
        {
            if (TotalItemCount <= 0 || UniformItemHeight <= 0)
            {
                return new Thickness(0);
            }

            return new Thickness(availableSize.Width, TotalItemCount * UniformItemHeight, 0, 0);
        }

        protected override void UpdateContentLayout(Rectangle bounds)
        {
            if (TotalItemCount <= 0 || UniformItemHeight <= 0 || ItemGenerator == null)
            {
                RecycleAllItems();
                return;
            }

            EnsureScrollViewerAttached();

            // Determine visible range from scroll viewer
            int scrollOffset = 0;
            int viewportHeight = bounds.Height; // fallback: full virtual height
            if (_parentScrollViewer != null)
            {
                scrollOffset = (int)_parentScrollViewer.VerticalOffset;
                viewportHeight = _parentScrollViewer.ContentViewport.Height;
                if (viewportHeight <= 0)
                {
                    viewportHeight = bounds.Height;
                }
            }

            int firstNeeded = Math.Max(0, (int)(scrollOffset / (double)UniformItemHeight) - BufferCount);
            int lastNeeded  = Math.Min(TotalItemCount - 1,
                                       (int)((scrollOffset + viewportHeight - 1) / (double)UniformItemHeight) + BufferCount);

            bool rangeChanged = firstNeeded != _cachedFirstNeeded || lastNeeded != _cachedLastNeeded;

            if (rangeChanged)
            {
                _cachedFirstNeeded = firstNeeded;
                _cachedLastNeeded  = lastNeeded;

                // Recycle items that fell outside the new range (no LINQ, avoids allocations)
                if (_realizedItems.Count > 0)
                {
                    int[] toRecycle = new int[_realizedItems.Count];
                    int count = 0;
                    foreach (int k in _realizedItems.Keys)
                    {
                        if (k < firstNeeded || k > lastNeeded)
                        {
                            toRecycle[count++] = k;
                        }
                    }
                    for (int i = 0; i < count; i++)
                        RecycleItem(toRecycle[i]);
                }

                // Realize items newly in range
                for (int i = firstNeeded; i <= lastNeeded; i++)
                {
                    if (!_realizedItems.ContainsKey(i))
                    {
                        RealizeItem(i);
                    }
                }

                // Update tracking indices incrementally
                FirstRealizedIndex = _realizedItems.Count > 0 ? firstNeeded : -1;
                LastRealizedIndex  = _realizedItems.Count > 0 ? lastNeeded  : -1;
            }

            // Always reposition realized items (bounds may have changed even when the range did not)
            foreach (var (idx, element) in _realizedItems)
            {
                int y = bounds.Top + idx * UniformItemHeight;
                element.UpdateLayout(new Rectangle(bounds.Left, y, bounds.Width, UniformItemHeight));
            }
        }

        /// <summary>CPU-side culling: only draw realized items whose <see cref="MGElement.ActualLayoutBounds"/>
        /// are non-empty (i.e. intersect the scissor viewport computed during <see cref="MGElement.Update"/>).
        /// Items in the recycle buffer above/below the visible area will have an empty <see cref="MGElement.ActualLayoutBounds"/>
        /// and are skipped here without entering their <see cref="MGElement.Draw"/> method, saving per-call overhead.</summary>
        protected override void DrawContents(ElementDrawArgs DA)
        {
            if (_realizedItems.Count == 0)
            {
                return;
            }

            foreach (var (_, element) in _realizedItems)
            {
                // ActualLayoutBounds is empty when the element is fully clipped (outside the scroll viewport).
                // Skipping the Draw() call avoids computing TargetBounds, scissor checks, and delegate invocations.
                if (!element.ActualLayoutBounds.IsEmpty)
                {
                    element.Draw(DA);
                }
            }
        }
    }
}
