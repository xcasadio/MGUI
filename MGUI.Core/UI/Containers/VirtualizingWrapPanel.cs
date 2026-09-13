using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using System.Diagnostics;
using MGUI.Core.UI.Brushes.BorderBrushes;

namespace MGUI.Core.UI.Containers;

internal readonly record struct VirtualizingWrapPanelVisibleRange(int FirstIndex, int LastIndex)
{
    public bool IsEmpty => FirstIndex < 0 || LastIndex < FirstIndex;
}

internal static class VirtualizingWrapPanelLayout
{
    private static bool IsUnbounded(int value) => value >= int.MaxValue / 4;

    public static int GetColumnCount(int availableWidth, int itemWidth, int spacing)
    {
        if (itemWidth <= 0)
        {
            return 1;
        }

        if (IsUnbounded(availableWidth))
        {
            return int.MaxValue;
        }

        int pitch = Math.Max(1, itemWidth + Math.Max(0, spacing));
        return Math.Max(1, (availableWidth + Math.Max(0, spacing)) / pitch);
    }

    public static int GetRowCount(int totalItemCount, int columns)
    {
        if (totalItemCount <= 0)
        {
            return 0;
        }

        columns = Math.Max(1, columns);
        return (totalItemCount + columns - 1) / columns;
    }

    public static Size Measure(int totalItemCount, Size availableSize, int itemWidth, int itemHeight, int spacing)
    {
        if (totalItemCount <= 0 || itemWidth <= 0 || itemHeight <= 0)
        {
            return Size.Empty;
        }

        int columns = GetColumnCount(availableSize.Width, itemWidth, spacing);
        if (columns == int.MaxValue)
        {
            columns = totalItemCount;
        }

        int rows = GetRowCount(totalItemCount, columns);
        int actualSpacing = Math.Max(0, spacing);
        int width = columns * itemWidth + Math.Max(0, columns - 1) * actualSpacing;
        int height = rows * itemHeight + Math.Max(0, rows - 1) * actualSpacing;
        return new Size(width, height);
    }

    public static VirtualizingWrapPanelVisibleRange GetVisibleRange(
        int totalItemCount,
        int columns,
        int itemHeight,
        int spacing,
        int verticalOffset,
        int viewportHeight,
        int bufferRows)
    {
        if (totalItemCount <= 0 || columns <= 0 || itemHeight <= 0)
        {
            return new VirtualizingWrapPanelVisibleRange(-1, -1);
        }

        int rowPitch = Math.Max(1, itemHeight + Math.Max(0, spacing));
        int totalRows = GetRowCount(totalItemCount, columns);
        int firstRow = Math.Max(0, verticalOffset / rowPitch - Math.Max(0, bufferRows));
        int lastRow = Math.Min(
            totalRows - 1,
            Math.Max(0, (verticalOffset + Math.Max(1, viewportHeight) - 1) / rowPitch) + Math.Max(0, bufferRows));

        int firstIndex = firstRow * columns;
        int lastIndex = Math.Min(totalItemCount - 1, ((lastRow + 1) * columns) - 1);
        return new VirtualizingWrapPanelVisibleRange(firstIndex, lastIndex);
    }

    public static Rectangle GetItemBounds(int index, int columns, Rectangle bounds, int itemWidth, int itemHeight, int spacing)
    {
        columns = Math.Max(1, columns);
        int actualSpacing = Math.Max(0, spacing);
        int row = index / columns;
        int column = index % columns;
        return new Rectangle(
            bounds.Left + column * (itemWidth + actualSpacing),
            bounds.Top + row * (itemHeight + actualSpacing),
            itemWidth,
            itemHeight);
    }
}

/// <summary>
/// A vertical virtualizing wrap panel for uniform item cards.
/// It keeps only the rows intersecting the scroll viewport realized while exposing the full logical content size.
/// </summary>
public class VirtualizingWrapPanel : MGMultiContentHost
{
    #region Border
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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _totalItemCount;
    public int TotalItemCount
    {
        get => _totalItemCount;
        set
        {
            int clamped = Math.Max(0, value);
            if (_totalItemCount != clamped)
            {
                _totalItemCount = clamped;
                LayoutChanged(this, true);
                NPC(nameof(TotalItemCount));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _itemWidth = 64;
    public int ItemWidth
    {
        get => _itemWidth;
        set
        {
            int clamped = Math.Max(1, value);
            if (_itemWidth != clamped)
            {
                _itemWidth = clamped;
                RecycleAllItems();
                LayoutChanged(this, true);
                NPC(nameof(ItemWidth));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _itemHeight = 64;
    public int ItemHeight
    {
        get => _itemHeight;
        set
        {
            int clamped = Math.Max(1, value);
            if (_itemHeight != clamped)
            {
                _itemHeight = clamped;
                RecycleAllItems();
                LayoutChanged(this, true);
                NPC(nameof(ItemHeight));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _spacing;
    public int Spacing
    {
        get => _spacing;
        set
        {
            int clamped = Math.Max(0, value);
            if (_spacing != clamped)
            {
                _spacing = clamped;
                LayoutChanged(this, true);
                NPC(nameof(Spacing));
            }
        }
    }

    /// <summary>The effective spacing used during measure/arrange/virtualization math, after applying <see cref="MGElement.ResponsiveSpacingScaleFactor"/> to <see cref="Spacing"/>.<br/>
    /// Equals <see cref="Spacing"/> when responsive spacing scaling doesn't apply (e.g. outside a responsive subtree, or at scale factor 1.0).</summary>
    internal int ResolvedSpacing => ResolveOwnedSpacing(_spacing);

    public int BufferRows { get; set; } = 1;

    public Func<int, MGElement> ItemGenerator { get; set; }

    public Action<int, MGElement> ItemRecycler { get; set; }

    private readonly Dictionary<int, MGElement> _realizedItems = new();
    private readonly Queue<MGElement> _recyclePool = new();
    private readonly List<int> _itemsToRecycle = new();

    private MGScrollViewer _parentScrollViewer;
    private EventHandler<EventArgs<float>> _parentVerticalOffsetChangedHandler;

    private int _cachedFirstNeeded = -1;
    private int _cachedLastNeeded = -1;

    public int FirstRealizedIndex { get; private set; } = -1;
    public int LastRealizedIndex { get; private set; } = -1;

    public int CurrentColumnCount { get; private set; } = -1;

    public bool HasAttachedScrollViewer => _parentScrollViewer != null;

    public bool TryDequeueRecycledElement(out MGElement element)
        => _recyclePool.TryDequeue(out element);

    public bool TryGetRealizedElement(int index, out MGElement element)
        => _realizedItems.TryGetValue(index, out element);

    public VirtualizingWrapPanel(MGWindow window)
        : base(window, MGElementType.VirtualizingWrapPanel)
    {
        using (BeginInitializing())
        {
            BorderElement = new MGBorder(window);
            BorderComponent = MGComponentBase.Create(BorderElement);
            AddComponent(BorderComponent);
            BorderElement.OnCornerRadiusChanged += (sender, e) => { NPC(nameof(CornerRadius)); };

            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Top;
            CanChangeContent = false;
        }
    }

    public void InvalidateData()
    {
        RecycleAllItems();
        _recyclePool.Clear();
        LayoutChanged(this, true);
    }

    public void EnsureIndexVisible(int index)
    {
        if (index < 0 || index >= TotalItemCount)
        {
            return;
        }

        EnsureScrollViewerAttached();
        if (_parentScrollViewer == null || _parentScrollViewer.ContentViewport.Height <= 0)
        {
            return;
        }

        int resolvedSpacing = ResolvedSpacing;
        int columns = GetColumnsForWidth(Math.Max(1, LayoutBounds.Width), resolvedSpacing);
        int row = index / columns;
        int rowPitch = ItemHeight + resolvedSpacing;
        int elementStart = LayoutBounds.Top + row * rowPitch;
        int elementEnd = elementStart + ItemHeight;
        float viewportStart = LayoutBounds.Top + _parentScrollViewer.VerticalOffset;
        float newOffset = MGScrollViewer.GetVisibleOffset(
            _parentScrollViewer.VerticalOffset,
            viewportStart,
            _parentScrollViewer.ContentViewport.Height,
            _parentScrollViewer.MaxVerticalOffset,
            elementStart,
            elementEnd);

        if (Math.Abs(newOffset - _parentScrollViewer.VerticalOffset) > 0.5f)
        {
            _parentScrollViewer.VerticalOffset = newOffset;
        }
    }

    protected override Thickness UpdateContentMeasurement(Size availableSize)
    {
        int resolvedSpacing = ResolvedSpacing;
        Size desiredSize = VirtualizingWrapPanelLayout.Measure(TotalItemCount, availableSize, ItemWidth, ItemHeight, resolvedSpacing);
        return new Thickness(desiredSize.Width, desiredSize.Height, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle bounds)
    {
        if (TotalItemCount <= 0 || ItemGenerator == null || ItemWidth <= 0 || ItemHeight <= 0)
        {
            RecycleAllItems();
            return;
        }

        EnsureScrollViewerAttached();

        int resolvedSpacing = ResolvedSpacing;
        int columns = GetColumnsForWidth(bounds.Width, resolvedSpacing);
        int verticalOffset = 0;
        int viewportHeight = bounds.Height;
        if (_parentScrollViewer != null)
        {
            verticalOffset = (int)_parentScrollViewer.VerticalOffset;
            viewportHeight = _parentScrollViewer.ContentViewport.Height;
            if (viewportHeight <= 0)
            {
                viewportHeight = bounds.Height;
            }
        }

        VirtualizingWrapPanelVisibleRange range = VirtualizingWrapPanelLayout.GetVisibleRange(
            TotalItemCount,
            columns,
            ItemHeight,
            resolvedSpacing,
            verticalOffset,
            viewportHeight,
            BufferRows);

        bool rangeChanged = range.FirstIndex != _cachedFirstNeeded || range.LastIndex != _cachedLastNeeded || columns != CurrentColumnCount;
        if (rangeChanged)
        {
            _cachedFirstNeeded = range.FirstIndex;
            _cachedLastNeeded = range.LastIndex;
            CurrentColumnCount = columns;

            if (_realizedItems.Count > 0)
            {
                _itemsToRecycle.Clear();
                foreach (int index in _realizedItems.Keys)
                {
                    if (range.IsEmpty || index < range.FirstIndex || index > range.LastIndex)
                    {
                        _itemsToRecycle.Add(index);
                    }
                }

                for (int i = 0; i < _itemsToRecycle.Count; i++)
                {
                    RecycleItem(_itemsToRecycle[i]);
                }
            }

            if (!range.IsEmpty)
            {
                for (int index = range.FirstIndex; index <= range.LastIndex; index++)
                {
                    if (!_realizedItems.ContainsKey(index))
                    {
                        RealizeItem(index);
                    }
                }
            }

            FirstRealizedIndex = _realizedItems.Count > 0 && !range.IsEmpty ? range.FirstIndex : -1;
            LastRealizedIndex = _realizedItems.Count > 0 && !range.IsEmpty ? range.LastIndex : -1;
        }

        foreach (var pair in _realizedItems)
        {
            Rectangle itemBounds = VirtualizingWrapPanelLayout.GetItemBounds(pair.Key, columns, bounds, ItemWidth, ItemHeight, resolvedSpacing);
            MGElement element = pair.Value;
            if (!element.IsLayoutValid || element.AllocatedBounds != itemBounds)
            {
                element.UpdateLayout(itemBounds);
            }
        }
    }

    protected override void DrawContents(ElementDrawArgs DA)
    {
        if (_realizedItems.Count == 0)
        {
            return;
        }

        foreach (var pair in _realizedItems)
        {
            if (!pair.Value.ActualLayoutBounds.IsEmpty)
            {
                pair.Value.Draw(DA);
            }
        }
    }

    private int GetColumnsForWidth(int width, int resolvedSpacing)
        => Math.Min(Math.Max(1, TotalItemCount), VirtualizingWrapPanelLayout.GetColumnCount(width, ItemWidth, resolvedSpacing));

    private void EnsureScrollViewerAttached()
    {
        if (_parentScrollViewer != null)
        {
            return;
        }

        if (TryFindParentOfType<MGScrollViewer>(out MGScrollViewer scrollViewer))
        {
            _parentScrollViewer = scrollViewer;
            _parentVerticalOffsetChangedHandler ??= (_, _) => RequestVirtualizationLayoutRefresh();
            _parentScrollViewer.VerticalOffsetChanged += _parentVerticalOffsetChangedHandler;
        }
    }

    private void RequestVirtualizationLayoutRefresh()
    {
        LayoutChanged(this, true);
    }

    private void RealizeItem(int index)
    {
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

    private void RecycleAllItems()
    {
        if (_realizedItems.Count > 0)
        {
            _itemsToRecycle.Clear();
            foreach (int index in _realizedItems.Keys)
            {
                _itemsToRecycle.Add(index);
            }

            for (int i = 0; i < _itemsToRecycle.Count; i++)
            {
                RecycleItem(_itemsToRecycle[i]);
            }
        }

        FirstRealizedIndex = -1;
        LastRealizedIndex = -1;
        _cachedFirstNeeded = -1;
        _cachedLastNeeded = -1;
        CurrentColumnCount = -1;
    }
}