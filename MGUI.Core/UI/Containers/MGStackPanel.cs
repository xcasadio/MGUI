using MGUI.Core.UI.Brushes.BorderBrushes;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes.FillBrushes;

namespace MGUI.Core.UI.Containers;

public class MGStackPanel : MGMultiContentHost
{
    private readonly List<Thickness> _measuredChildSizes = new();

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

    private Orientation _Orientation;
    public Orientation Orientation
    {
        get => _Orientation;
        set
        {
            if (_Orientation != value)
            {
                _Orientation = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(Orientation));
            }
        }
    }

    /*private FlowDirection _FlowDirection;
    public FlowDirection FlowDirection
    {
        get => _FlowDirection;
        set
        {
            if (_FlowDirection != value)
            {
                _FlowDirection = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(FlowDirection));
            }
        }
    }*/

    private int _Spacing;
    /// <summary>A padding amount, in pixels, between each consecutive non-collapsed child in this <see cref="MGStackPanel"/>.<br/>
    /// Spacing is ignored between children where <see cref="MGElement.IsVisibilityCollapsed"/> is true.<para/>
    /// Default value: 0</summary>
    public int Spacing
    {
        get => _Spacing;
        set
        {
            if (_Spacing != value)
            {
                _Spacing = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(Spacing));
            }
        }
    }

    /// <summary>The effective spacing used during measure/arrange, after applying <see cref="MGElement.ResponsiveSpacingScaleFactor"/> to <see cref="Spacing"/>.<br/>
    /// Equals <see cref="Spacing"/> when responsive spacing scaling doesn't apply (e.g. outside a responsive subtree, or at scale factor 1.0).</summary>
    internal int ResolvedSpacing => ResolveOwnedSpacing(_Spacing);

    /// <returns>True if the given <paramref name="Item"/> was successfully added.<br/>
    /// False otherwise, such as if <see cref="MGContentHost.CanChangeContent"/> is false.</returns>
    public bool TryAddChild(MGElement Item)
    {
        if (!CanChangeContent)
        {
            return false;
        }

        _Children.Add(Item);
        return true;
    }

    /// <returns>True if the given <paramref name="Item"/> was successfully inserted.<br/>
    /// False otherwise, such as if <see cref="MGContentHost.CanChangeContent"/> is false.</returns>
    public bool TryInsertChild(int Index, MGElement Item)
    {
        if (!CanChangeContent || Index < 0 || Index > _Children.Count)
        {
            return false;
        }

        if (Index == _Children.Count)
        {
            return TryAddChild(Item);
        }
        else
        {
            _Children.Insert(Index, Item);
            return true;
        }
    }

    /// <returns>True if the given <paramref name="Item"/> was found in <see cref="MGMultiContentHost.Children"/> and was successfully removed.<br/>
    /// False otherwise, such as if <see cref="MGContentHost.CanChangeContent"/> is false.</returns>
    public bool TryRemoveChild(MGElement Item)
    {
        if (!CanChangeContent)
        {
            return false;
        }

        return _Children.Remove(Item);
    }

    /// <returns>True if the given <paramref name="Old"/> item was found in <see cref="MGMultiContentHost.Children"/> and was successfully replaced with <paramref name="New"/>.<br/>
    /// False otherwise, such as if <see cref="MGContentHost.CanChangeContent"/> is false.</returns>
    public bool TryReplaceChild(MGElement Old, MGElement New)
    {
        if (!CanChangeContent)
        {
            return false;
        }

        var Index = _Children.IndexOf(Old);
        if (Index < 0)
        {
            return false;
        }

        _Children[Index] = New;
        return true;
    }

    /// <summary>Removes all elements from every row/column of this grid</summary>
    public bool TryRemoveAll()
    {
        if (!CanChangeContent)
        {
            return false;
        }

        _Children.ClearOneByOne();
        return true;
    }

    public MGStackPanel(MGWindow Window, Orientation Orientation)
        : base(Window, MGElementType.StackPanel)
    {
        using (BeginInitializing())
        {
            BorderElement = new(Window, 0, null as IFillBrush);
            BorderComponent = MGComponentBase.Create(BorderElement);
            AddComponent(BorderComponent);
            BorderElement.OnBorderBrushChanged += (sender, e) => { NotifyPropertyChanged(nameof(BorderBrush)); };
            BorderElement.OnBorderThicknessChanged += (sender, e) => { NotifyPropertyChanged(nameof(BorderThickness)); };
            BorderElement.OnCornerRadiusChanged += (sender, e) => { NotifyPropertyChanged(nameof(CornerRadius)); };

            this.Orientation = Orientation;
            //this.FlowDirection = FlowDirection.LeftToRight;
            Spacing = 0;
        }
    }

    private void EnsureMeasuredChildSizeCapacity(int count)
    {
        while (_measuredChildSizes.Count < count)
        {
            _measuredChildSizes.Add(default);
        }
    }

    private void UpdateChildLayoutIfNeeded(MGElement child, Rectangle childBounds)
    {
        if (!child.IsLayoutValid || child.AllocatedBounds != childBounds)
        {
            child.UpdateLayout(childBounds);
        }
    }

    private Thickness MeasureVerticalChildren(Size availableSize, out int nonCollapsedChildrenCount)
    {
        EnsureMeasuredChildSizeCapacity(Children.Count);

        var resolvedSpacing = ResolvedSpacing;
        var remainingSize = availableSize;
        var maxWidth = 0;
        var totalHeight = 0;
        nonCollapsedChildrenCount = 0;

        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.UpdateMeasurement(remainingSize, out _, out var fullSize, out _, out _);
            _measuredChildSizes[i] = fullSize;

            if (fullSize.Width > maxWidth)
            {
                maxWidth = fullSize.Width;
            }

            totalHeight += fullSize.Height;

            if (!child.IsVisibilityCollapsed)
            {
                nonCollapsedChildrenCount++;
            }

            var consumedHeight = fullSize.Height + (child.IsVisibilityCollapsed ? 0 : resolvedSpacing);
            remainingSize = remainingSize.Subtract(new Size(0, consumedHeight), 0, 0);
        }

        if (nonCollapsedChildrenCount > 1)
        {
            totalHeight += resolvedSpacing * (nonCollapsedChildrenCount - 1);
        }

        return new Thickness(maxWidth, totalHeight, 0, 0);
    }

    private Thickness MeasureHorizontalChildren(Size availableSize, out int nonCollapsedChildrenCount)
    {
        EnsureMeasuredChildSizeCapacity(Children.Count);

        var resolvedSpacing = ResolvedSpacing;
        var remainingSize = availableSize;
        var totalWidth = 0;
        var maxHeight = 0;
        nonCollapsedChildrenCount = 0;

        for (var i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            child.UpdateMeasurement(remainingSize, out _, out var fullSize, out _, out _);
            _measuredChildSizes[i] = fullSize;

            totalWidth += fullSize.Width;
            if (fullSize.Height > maxHeight)
            {
                maxHeight = fullSize.Height;
            }

            if (!child.IsVisibilityCollapsed)
            {
                nonCollapsedChildrenCount++;
            }

            var consumedWidth = fullSize.Width + (child.IsVisibilityCollapsed ? 0 : resolvedSpacing);
            remainingSize = remainingSize.Subtract(new Size(consumedWidth, 0), 0, 0);
        }

        if (nonCollapsedChildrenCount > 1)
        {
            totalWidth += resolvedSpacing * (nonCollapsedChildrenCount - 1);
        }

        return new Thickness(totalWidth, maxHeight, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        if (!HasContent)
        {
            return;
        }

        Size AvailableSize = new(Bounds.Width, Bounds.Height);

        if (Orientation == Orientation.Vertical)
        {
            var totalContentSize = MeasureVerticalChildren(AvailableSize, out _);

            //  Account for content alignment
            var ConsumedWidth = HorizontalContentAlignment == HorizontalAlignment.Stretch ? AvailableSize.Width : Math.Min(AvailableSize.Width, totalContentSize.Width);
            Size ConsumedContentSize = new(ConsumedWidth, totalContentSize.Height);
            var AlignedBounds = ApplyAlignment(Bounds, HorizontalContentAlignment, VerticalContentAlignment, ConsumedContentSize);

            //  Allocate space for each child
            var resolvedSpacing = ResolvedSpacing;
            var CurrentY = AlignedBounds.Top;
            for (var i = 0; i < Children.Count; i++)
            {
                var Child = Children[i];
                var Height = _measuredChildSizes[i].Height;
                Rectangle ChildBounds = new(AlignedBounds.Left, CurrentY, AlignedBounds.Width, Height);
                UpdateChildLayoutIfNeeded(Child, ChildBounds);
                CurrentY += Height + (Child.IsVisibilityCollapsed ? 0 : resolvedSpacing);
            }
        }
        else if (Orientation == Orientation.Horizontal)
        {
            var totalContentSize = MeasureHorizontalChildren(AvailableSize, out _);

            //  Account for content alignment
            var ConsumedHeight = VerticalContentAlignment == VerticalAlignment.Stretch ? AvailableSize.Height : Math.Min(AvailableSize.Height, totalContentSize.Height);
            Size ConsumedContentSize = new(totalContentSize.Width, ConsumedHeight);
            var AlignedBounds = ApplyAlignment(Bounds, HorizontalContentAlignment, VerticalContentAlignment, ConsumedContentSize);

            //  Allocate space for each child
            var resolvedSpacing = ResolvedSpacing;
            var CurrentX = AlignedBounds.Left;
            for (var i = 0; i < Children.Count; i++)
            {
                var Child = Children[i];
                var Width = _measuredChildSizes[i].Width;
                Rectangle ChildBounds = new(CurrentX, AlignedBounds.Top, Width, AlignedBounds.Height);
                UpdateChildLayoutIfNeeded(Child, ChildBounds);
                CurrentX += Width + (Child.IsVisibilityCollapsed ? 0 : resolvedSpacing);
            }
        }
        else
        {
            throw new NotImplementedException($"Unrecognized {nameof(Orientation)}: {Orientation}");
        }
    }

    protected override Thickness UpdateContentMeasurement(Size AvailableSize)
    {
        if (HasContent)
        {
            if (Orientation == Orientation.Vertical)
            {
                return MeasureVerticalChildren(AvailableSize, out _);
            }
            else if (Orientation == Orientation.Horizontal)
            {
                return MeasureHorizontalChildren(AvailableSize, out _);
            }
            else
            {
                throw new NotImplementedException($"Unrecognized {nameof(Orientation)}: {Orientation}");
            }
        }
        else
        {
            return UpdateContentMeasurementBaseImplementation(AvailableSize);
        }
    }
}