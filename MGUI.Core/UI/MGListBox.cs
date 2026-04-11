using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MGUI.Shared.Input.Keyboard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Thickness = MonoGame.Extended.Thickness;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using MGUI.Shared.Helpers;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI
{
    public enum ListBoxSelectionMode
    {
        /// <summary>Items cannot be selected</summary>
        None,
        /// <summary>A single item may be selected at a time, by left-clicking it</summary>
        Single,
        /// <summary>A set of consecutive items may be selected at once. Left-click to replace selection with a single item.<br/>
        /// Shift+Left-click to select all consecutive items between the current selection source and the clicked item.</summary>
        Contiguous,
        /// <summary>Any number of items may be selected at once. Left-click to replace selection with a single item.<br/>
        /// Ctrl+Left-click to toggle the selection state of the clicked item.</summary>
        Multiple
    }

    /// <typeparam name="TItemType">The type that the ItemsSource will be bound to.</typeparam>
    public class MGListBox<TItemType> : MGElement, INavigationTargetVisibilityHandler
    {
        public const string OuterBorderPartName = "PART_OuterBorder";
        public const string InnerBorderPartName = "PART_InnerBorder";
        public const string TitleBorderPartName = "PART_TitleBorder";
        public const string TitlePresenterPartName = "PART_TitlePresenter";
        public const string ScrollViewerPartName = "PART_ScrollViewer";
        public const string ItemsPanelPartName = "PART_ItemsPanel";

        protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
        {
            yield return new(OuterBorderPartName, typeof(MGBorder));
            yield return new(InnerBorderPartName, typeof(MGBorder));
            yield return new(TitleBorderPartName, typeof(MGBorder));
            yield return new(TitlePresenterPartName, typeof(MGContentPresenter));
            yield return new(ScrollViewerPartName, typeof(MGScrollViewer));
            yield return new(ItemsPanelPartName, typeof(MGStackPanel));
        }

        internal static int GetNextNavigationIndex(int currentIndex, int count, UINavigationAction action, int pageSize = 10)
        {
            if (count <= 0)
            {
                return -1;
            }

            int normalizedIndex = currentIndex < 0 ? 0 : currentIndex;
            return action switch
            {
                UINavigationAction.MoveUp => Math.Max(0, normalizedIndex - 1),
                UINavigationAction.MoveDown => Math.Min(count - 1, normalizedIndex + 1),
                UINavigationAction.Home => 0,
                UINavigationAction.End => count - 1,
                UINavigationAction.PageUp => Math.Max(0, normalizedIndex - pageSize),
                UINavigationAction.PageDown => Math.Min(count - 1, normalizedIndex + pageSize),
                _ => normalizedIndex
            };
        }

        internal static float GetVisibleVerticalOffsetForIndex(float currentOffset, float contentTop, float viewportHeight, float maxOffset, int itemIndex, int itemHeight)
        {
            if (itemIndex < 0 || itemHeight <= 0)
            {
                return Math.Clamp(currentOffset, 0, maxOffset);
            }

            float itemTop = contentTop + (itemIndex * itemHeight);
            float itemBottom = itemTop + itemHeight;
            return MGScrollViewer.GetVisibleOffset(currentOffset, contentTop + currentOffset, viewportHeight, maxOffset, itemTop, itemBottom);
        }

        #region Outer Border
        private MGComponent<MGBorder> OuterBorderComponent { get; set; }
        /// <summary><see cref="MGListBox{TItemType}"/>es contain 3 borders:<para/>
        /// 1. <see cref="OuterBorder"/>: Wrapped around the entire <see cref="MGListBox{TItemType}"/><br/>
        /// 2. <see cref="InnerBorder"/>: Wrapped around the <see cref="ItemsPanel"/>, but not the <see cref="TitleComponent"/><br/>
        /// 3. <see cref="TitleBorder"/>: Wrapped around the <see cref="TitleComponent"/></summary>
        public MGBorder OuterBorder { get; private set; }
        public override MGBorder GetBorder() => OuterBorder;

        public IBorderBrush OuterBorderBrush
        {
            get => OuterBorder.BorderBrush;
            set
            {
                if (OuterBorderBrush != value)
                {
                    OuterBorder.BorderBrush = value;
                    NPC(nameof(OuterBorderBrush));
                }
            }
        }

        public Thickness OuterBorderThickness
        {
            get => OuterBorder.BorderThickness;
            set
            {
                if (!OuterBorderThickness.Equals(value))
                {
                    OuterBorder.BorderThickness = value;
                    NPC(nameof(OuterBorderThickness));
                }
            }
        }
        #endregion Outer Border

        #region Inner Border
        private MGComponent<MGBorder> InnerBorderComponent { get; set; }
        /// <summary><see cref="MGListBox{TItemType}"/>es contain 3 borders:<para/>
        /// 1. <see cref="OuterBorder"/>: Wrapped around the entire <see cref="MGListBox{TItemType}"/><br/>
        /// 2. <see cref="InnerBorder"/>: Wrapped around the <see cref="ItemsPanel"/>, but not the <see cref="TitleComponent"/><br/>
        /// 3. <see cref="TitleBorder"/>: Wrapped around the <see cref="TitleComponent"/></summary>
        public MGBorder InnerBorder { get; private set; }

        public IBorderBrush InnerBorderBrush
        {
            get => InnerBorder.BorderBrush;
            set
            {
                if (InnerBorderBrush != value)
                {
                    InnerBorder.BorderBrush = value;
                    NPC(nameof(InnerBorderBrush));
                }
            }
        }

        public Thickness InnerBorderThickness
        {
            get => InnerBorder.BorderThickness;
            set
            {
                if (!InnerBorderThickness.Equals(value))
                {
                    InnerBorder.BorderThickness = value;
                    NPC(nameof(InnerBorderThickness));
                }
            }
        }
        #endregion Inner Border

        #region Title
        private MGComponent<MGBorder> TitleComponent { get; set; }
        /// <summary><see cref="MGListBox{TItemType}"/>es contain 3 borders:<para/>
        /// 1. <see cref="OuterBorder"/>: Wrapped around the entire <see cref="MGListBox{TItemType}"/><br/>
        /// 2. <see cref="InnerBorder"/>: Wrapped around the <see cref="ItemsPanel"/>, but not the <see cref="TitleComponent"/><br/>
        /// 3. <see cref="TitleBorder"/>: Wrapped around the <see cref="TitleComponent"/></summary>
        public MGBorder TitleBorder { get; private set; }
        public MGContentPresenter TitlePresenter { get; private set; }

        public IBorderBrush TitleBorderBrush
        {
            get => TitleBorder.BorderBrush;
            set
            {
                if (TitleBorder.BorderBrush != value)
                {
                    TitleBorder.BorderBrush = value;
                    NPC(nameof(TitleBorderBrush));
                }
            }
        }

        public Thickness TitleBorderThickness
        {
            get => TitleBorder.BorderThickness;
            set
            {
                if (!TitleBorder.BorderThickness.Equals(value))
                {
                    TitleBorder.BorderThickness = value;
                    NPC(nameof(TitleBorderThickness));
                }
            }
        }

        public bool IsTitleVisible
        {
            get => TitleBorder.Visibility == Visibility.Visible;
            set => SetIsTitleVisible(value, false);
        }

        private bool AutoManageTitleVisibility { get; set; } = true;

        private void SetIsTitleVisible(bool value, bool isAutomatic)
        {
            if (!isAutomatic)
            {
                AutoManageTitleVisibility = false;
            }

            if (TitleBorder != null && IsTitleVisible != value)
            {
                TitleBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                NPC(nameof(IsTitleVisible));
            }
        }
        #endregion Title

        #region Items Source
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ObservableCollection<MGListBoxItem<TItemType>> _InternalItems;
        private ObservableCollection<MGListBoxItem<TItemType>> InternalItems
        {
            get => _InternalItems;
            set
            {
                if (_InternalItems != value)
                {
                    if (InternalItems != null)
                    {
                        HandleTemplatedContentRemoved(InternalItems.Select(x => x.Content));
                        InternalItems.CollectionChanged -= ListBoxItems_CollectionChanged;
                        foreach (MGListBoxItem<TItemType> Removed in InternalItems)
                        {
                            OnListBoxItemRemoved?.Invoke(this, Removed);
                        }
                    }
                    _InternalItems = value;
                    if (InternalItems != null)
                    {
                        InternalItems.CollectionChanged += ListBoxItems_CollectionChanged;
                        foreach (MGListBoxItem<TItemType> Added in InternalItems)
                        {
                            OnListBoxItemAdded?.Invoke(this, Added);
                        }
                    }

                    using (ItemsPanel.AllowChangingContentTemporarily())
                    {
                        //  Clear all ListBoxItems
                        _ = ItemsPanel.TryRemoveAll();

                        //  Add the new ListBoxItems to the ItemsPanel (skipped when virtualizing: VSP manages its own children)
                        if (!IsVirtualizing && InternalItems != null)
                        {
                            foreach (MGListBoxItem<TItemType> LBI in InternalItems)
                            {
                                _ = ItemsPanel.TryAddChild(LBI.ContentPresenter);
                            }
                        }
                    }

                    // When virtualizing, update the VSP item count.
                    // _logicalItemsList takes precedence over InternalItems (InternalItems is null in the recycling path).
                    if (IsVirtualizing && _virtualizingPanel != null)
                    {
                        _virtualizingPanel.TotalItemCount = _logicalItemsList?.Count ?? InternalItems?.Count ?? 0;
                    }

                    ClearSelection();
                    RefreshRowBackgrounds();

                    NPC(nameof(ListBoxItems));
                }
            }
        }
        public IReadOnlyList<MGListBoxItem<TItemType>> ListBoxItems => InternalItems;

        private static void HandleTemplatedContentRemoved(IEnumerable<MGElement> Items)
        {
            if (Items != null)
            {
                foreach (var Item in Items)
                {
                    Item.RemoveDataBindings(true);
                }
            }
        }

        private void ListBoxItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsVirtualizing)
            {
                // When virtualizing, only keep the VSP total count in sync; the VSP manages its own children
                if (_virtualizingPanel != null)
                {
                    _virtualizingPanel.TotalItemCount = _logicalItemsList?.Count ?? InternalItems?.Count ?? 0;
                }

                HashSet<MGListBoxItem<TItemType>> virtualRemoved = new();
                if (e.Action is NotifyCollectionChangedAction.Reset)
                {
                    ClearSelection();
                }
                else if (e.Action is NotifyCollectionChangedAction.Remove && e.OldItems != null)
                {
                    foreach (MGListBoxItem<TItemType> item in e.OldItems)
                    {
                        virtualRemoved.Add(item);
                    }
                }

                // Clean up selection for removed items
                if (virtualRemoved.Count > 0 && SelectedItems != null)
                {
                    List<MGListBoxItem<TItemType>> newSel = SelectedItems.Where(x => !virtualRemoved.Contains(x)).ToList();
                    if (newSel.Count != SelectedItems.Count)
                    {
                        SelectedItems = newSel.AsReadOnly();
                    }
                }
                if (SelectionSourceItem != null && virtualRemoved.Contains(SelectionSourceItem))
                {
                    SelectionSourceItem = null;
                }

                if (virtualRemoved.Count > 0)
                {
                    HandleTemplatedContentRemoved(virtualRemoved.Select(x => x.Content));
                }

                return;
            }

            using (ItemsPanel.AllowChangingContentTemporarily())
            {
                HashSet<MGListBoxItem<TItemType>> Removed = new();

                if (e.Action is NotifyCollectionChangedAction.Reset)
                {
                    _ = ItemsPanel.TryRemoveAll();
                    ClearSelection();
                }
                else if (e.Action is NotifyCollectionChangedAction.Add && e.NewItems != null)
                {
                    int Index = e.NewStartingIndex;
                    foreach (MGListBoxItem<TItemType> Item in e.NewItems)
                    {
                        ItemsPanel.TryInsertChild(Index, Item.ContentPresenter);
                        OnListBoxItemAdded?.Invoke(this, Item);
                        Index++;
                    }

                    RefreshRowBackgrounds();
                }
                else if (e.Action is NotifyCollectionChangedAction.Remove && e.OldItems != null)
                {
                    foreach (MGListBoxItem<TItemType> Item in e.OldItems)
                    {
                        if (ItemsPanel.TryRemoveChild(Item.ContentPresenter))
                        {
                            Removed.Add(Item);
                            OnListBoxItemRemoved?.Invoke(this, Item);
                        }
                    }

                    RefreshRowBackgrounds();
                }
                else if (e.Action is NotifyCollectionChangedAction.Replace)
                {
                    List<MGListBoxItem<TItemType>> Old = e.OldItems.Cast<MGListBoxItem<TItemType>>().ToList();
                    List<MGListBoxItem<TItemType>> New = e.NewItems.Cast<MGListBoxItem<TItemType>>().ToList();
                    for (int i = 0; i < Old.Count; i++)
                    {
                        if (ItemsPanel.TryReplaceChild(Old[i].ContentPresenter, New[i].ContentPresenter))
                        {
                            Removed.Add(Old[i]);

                            if (AlternatingRowBackgrounds?.Any() == true)
                            {
                                New[i].ContentPresenter.BackgroundBrush.NormalValue = Old[i].ContentPresenter.BackgroundBrush.NormalValue;
                            }
                        }

                        OnListBoxItemRemoved?.Invoke(this, Old[i]);
                        OnListBoxItemAdded?.Invoke(this, New[i]);
                    }
                }
                else if (e.Action is NotifyCollectionChangedAction.Move)
                {
                    throw new NotImplementedException();
                }

                //  Ensure none of the removed items are Selected
                if (SelectedItems != null)
                {
                    List<MGListBoxItem<TItemType>> NewSelectedItems = SelectedItems.Where(x => !Removed.Contains(x)).ToList();
                    if (NewSelectedItems.Count != SelectedItems.Count || !NewSelectedItems.SequenceEqual(SelectedItems))
                    {
                        SelectedItems = NewSelectedItems.AsReadOnly();
                    }
                }
                if (SelectionSourceItem != null && Removed.Contains(SelectionSourceItem))
                {
                    SelectionSourceItem = null;
                }

                HandleTemplatedContentRemoved(Removed.Select(x => x.Content));
            }
        }

        /// <summary>Invoked when a new item is added to <see cref="ListBoxItems"/></summary>
        public event EventHandler<MGListBoxItem<TItemType>> OnListBoxItemAdded;
        /// <summary>Invoked when an item is removed from <see cref="ListBoxItems"/> either by removal or replacement.<para/>
        /// Warning - This event is NOT invoked if <see cref="ListBoxItems"/> is cleared via <see cref="ICollection{T}.Clear"/></summary>
        public event EventHandler<MGListBoxItem<TItemType>> OnListBoxItemRemoved;

#if NEVER //LEGACY CODE
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ObservableCollection<TItemType> _ItemsSource;
        /// <summary>To set this value, use <see cref="SetItemsSource(ICollection{TItemType})"/></summary>
        public ObservableCollection<TItemType> ItemsSource
        {
            get => _ItemsSource;
            private set
            {
                if (_ItemsSource != value)
                {
                    if (ItemsSource != null)
                        ItemsSource.CollectionChanged -= ItemsSource_CollectionChanged;
                    _ItemsSource = value;
                    if (ItemsSource != null)
                        ItemsSource.CollectionChanged += ItemsSource_CollectionChanged;

                    if (ItemsSource == null)
                        InternalItems = null;
                    else
                    {
                        IEnumerable<MGListBoxItem<TItemType>> Values = ItemsSource.Select((x, Index) => new MGListBoxItem<TItemType>(this, x));
                        this.InternalItems = new ObservableCollection<MGListBoxItem<TItemType>>(Values);
                    }

                    NPC(nameof(ItemsSource));
                    NPC(nameof(BindableItemsSource));
                }
            }
        }
#else
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ICollection<TItemType> _ItemsSource;
        /// <summary>The data source that <see cref="ListBoxItems"/> are generated from.<br/>If you want the <see cref="ListBoxItems"/> to dynamically update as the collection changes, 
        /// pass in a collection that implements <see cref="INotifyCollectionChanged"/>, such as an <see cref="ObservableCollection{T}"/></summary>
        public ICollection<TItemType> ItemsSource
        {
            get => _ItemsSource;
            private set
            {
                if (_ItemsSource != value)
                {
                    if (ItemsSource != null && ItemsSource is INotifyCollectionChanged Observable)
                    {
                        Observable.CollectionChanged -= ItemsSource_CollectionChanged;
                    }

                    _ItemsSource = value;
                    if (ItemsSource != null && ItemsSource is INotifyCollectionChanged Observable2)
                    {
                        Observable2.CollectionChanged += ItemsSource_CollectionChanged;
                    }

                    if (ItemsSource == null)
                    {
                        bool wasVirtualizing = IsVirtualizing;
                        IsVirtualizing = false;
                        InternalItems = null;
                        _logicalItemsList = null;
                        _realizedItems.Clear();
                        _contentPresToItem.Clear();
                        if (wasVirtualizing && ScrollViewer != null)
                        {
                            using (ScrollViewer.AllowChangingContentTemporarily())
                                ScrollViewer.SetContent(ItemsPanel);
                        }
                    }
                    else
                    {
                        bool newVirtualize = ShouldVirtualize(ItemsSource.Count);
                        bool wasVirtualizing = IsVirtualizing;
                        // Set flag BEFORE InternalItems so InternalItems.set sees the correct mode
                        IsVirtualizing = newVirtualize;

                        if (newVirtualize)
                        {
                            // Virtualised mode: store only the raw data list, never allocate per-item wrappers up-front
                            _logicalItemsList = ItemsSource as IList<TItemType> ?? ItemsSource.ToList();
                            _realizedItems.Clear();
                            _contentPresToItem.Clear();
                            InternalItems = null;   // VSP manages realized elements; InternalItems stays null
                            ConfigureVirtualizingPanel();
                            if (ScrollViewer != null && !wasVirtualizing)
                            {
                                using (ScrollViewer.AllowChangingContentTemporarily())
                                    ScrollViewer.SetContent(_virtualizingPanel);
                            }
                        }
                        else
                        {
                            _logicalItemsList = null;
                            IEnumerable<MGListBoxItem<TItemType>> Values = ItemsSource.Select((x, Index) =>
                                new MGListBoxItem<TItemType>(this, x) { LogicalIndex = Index });
                            InternalItems = new ObservableCollection<MGListBoxItem<TItemType>>(Values);
                            if (wasVirtualizing && ScrollViewer != null)
                            {
                                using (ScrollViewer.AllowChangingContentTemporarily())
                                    ScrollViewer.SetContent(ItemsPanel);
                            }
                        }
                    }

                    NPC(nameof(ItemsSource));
                }
            }
        }
#endif

        private void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                HandleTemplatedContentRemoved(InternalItems.Select(x => x.Content));
                InternalItems.Clear();
            }
            else if (e.Action is NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                int CurrentIndex = e.NewStartingIndex;
                foreach (TItemType Item in e.NewItems)
                {
                    MGListBoxItem<TItemType> NewRowItem = new(this, Item);
                    InternalItems.Insert(CurrentIndex, NewRowItem);
                    CurrentIndex++;
                }
            }
            else if (e.Action is NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                int CurrentIndex = e.OldStartingIndex;
                foreach (TItemType Item in e.OldItems)
                {
                    InternalItems.RemoveAt(CurrentIndex);
                    CurrentIndex++;
                }
            }
            else if (e.Action is NotifyCollectionChangedAction.Replace)
            {
                List<TItemType> Old = e.OldItems.Cast<TItemType>().ToList();
                List<TItemType> New = e.NewItems.Cast<TItemType>().ToList();
                for (int i = 0; i < Old.Count; i++)
                {
                    MGListBoxItem<TItemType> OldRowItem = InternalItems[i];
                    MGListBoxItem<TItemType> NewRowItem = new(this, New[i]);
                    InternalItems[e.OldStartingIndex + i] = NewRowItem;
                }
            }
            else if (e.Action is NotifyCollectionChangedAction.Move)
            {
                throw new NotImplementedException();
            }
        }

#if NEVER //LEGACY CODE
        /// <param name="Value"><see cref="ItemsSource"/> will be set to a copy of this <see cref="ICollection{T}"/> unless the collection is an <see cref="ObservableCollection{T}"/>.<br/>
        /// If you want <see cref="ItemsSource"/> to dynamically update as the collection changes, pass in an <see cref="ObservableCollection{T}"/></param>
        public void SetItemsSource(ICollection<TItemType> Value)
        {
            if (Value is ObservableCollection<TItemType> Observable)
                this.ItemsSource = Observable;
            else
                this.ItemsSource = new ObservableCollection<TItemType>(Value.ToList());
        }
#else
        /// <summary>Deprecated. Set <see cref="ItemsSource"/> directly.</summary>
        public void SetItemsSource(ICollection<TItemType> Value) => ItemsSource = Value;
#endif
#endregion Items Source

        #region Selection
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _CanDeselectByClickingSelectedItem;
        /// <summary>Only relevant if <see cref="SelectionMode"/> is not <see cref="ListBoxSelectionMode.None"/><para/>
        /// If true, allows deselecting a currently-selected item by left-clicking it.<para/>
        /// Note: If <see cref="SelectionMode"/> is <see cref="ListBoxSelectionMode.Multiple"/>, and the Control key is held when clicking an item,<br/>
        /// the user is always able to deselect regardless of this setting.<para/>
        /// Default value: true</summary>
        public bool CanDeselectByClickingSelectedItem
        {
            get => _CanDeselectByClickingSelectedItem;
            set
            {
                if (_CanDeselectByClickingSelectedItem != value)
                {
                    _CanDeselectByClickingSelectedItem = value;
                    NPC(nameof(CanDeselectByClickingSelectedItem));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ListBoxSelectionMode _SelectionMode;
        public ListBoxSelectionMode SelectionMode
        {
            get => _SelectionMode;
            set
            {
                if (_SelectionMode != value)
                {
                    _SelectionMode = value;
                    ClearSelection();
                    NPC(nameof(SelectionMode));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGListBoxItem<TItemType> _SelectionSourceItem;
        /// <summary>Only relevant if <see cref="SelectionMode"/> is <see cref="ListBoxSelectionMode.Contiguous"/>.<para/>
        /// Represents the starting item of the contiguous selection of items.</summary>
        public MGListBoxItem<TItemType> SelectionSourceItem
        {
            get => _SelectionSourceItem;
            private set
            {
                if (_SelectionSourceItem != value)
                {
                    _SelectionSourceItem = value;
                    NPC(nameof(SelectionSourceItem));
                }
            }
        }

        private readonly EqualityComparer<TItemType> EqualityComparer = EqualityComparer<TItemType>.Default;

        /// <summary>Returns the <see cref="MGListBoxItem{TItemType}.Data"/> of the first item in <see cref="SelectedItems"/>, or default(<typeparamref name="TItemType"/>) if no items are selected.<para/>
        /// Setting this value overwrites the <see cref="SelectedItems"/> with the first <see cref="MGListBoxItem{TItemType}"/> containing the matching data.<br/>
        /// If no matching item is found, clears the selection entirely.</summary>
        public TItemType SelectedValue
        {
            get => SelectedItems.Count == 0 ? default(TItemType) : SelectedItems.First().Data;
            set => SelectItem(value, true);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly HashSet<int> _selectedIndices = new();

        /// <summary>The logical indices of the currently-selected items within <see cref="ListBoxItems"/>.<para/>
        /// This is the index-based representation of <see cref="SelectedItems"/>, and is kept in sync whenever
        /// the selection changes. It is particularly useful in virtualized mode to avoid holding references to
        /// <see cref="MGListBoxItem{TItemType}"/> objects that may have been recycled.</summary>
        public IReadOnlySet<int> SelectedIndices => _selectedIndices;

        /// <summary>The data values of the currently-selected items, derived from <see cref="SelectedIndices"/> and <see cref="ItemsSource"/>.</summary>
        public IEnumerable<TItemType> SelectedDataItems
        {
            get
            {
                if (ItemsSource == null || _selectedIndices.Count == 0)
                {
                    yield break;
                }

                IList<TItemType> asList = ItemsSource as IList<TItemType> ?? ItemsSource.ToList();
                foreach (int idx in _selectedIndices)
                {
                    if (idx >= 0 && idx < asList.Count)
                    {
                        yield return asList[idx];
                    }
                }
            }
        }

        private ReadOnlyCollection<MGListBoxItem<TItemType>> _SelectedItems;
        /// <summary>The currently-selected items. This collection is never null: Uses an empty list if setting to null.</summary>
        public ReadOnlyCollection<MGListBoxItem<TItemType>> SelectedItems
        {
            get => _SelectedItems;
            set
            {
                if (_SelectedItems != value)
                {
                    if (SelectedItems != null)
                    {
                        foreach (MGListBoxItem<TItemType> Item in SelectedItems)
                        {
                            Item.ContentPresenter.IsSelected = false;
                        }
                    }

                    _SelectedItems = value ?? new List<MGListBoxItem<TItemType>>().AsReadOnly();

                    if (SelectedItems.Any(x => x == null))
                    {
                        throw new ArgumentNullException($"{nameof(MGListBoxItem<object>)}.{nameof(SelectedItems)} cannnot contain null items.");
                    }

                    foreach (MGListBoxItem<TItemType> Item in SelectedItems)
                    {
                        Item.ContentPresenter.IsSelected = true;
                    }

                    // Maintain index-based selection set in sync via LogicalIndex (works in both normal and virtual modes)
                    _selectedIndices.Clear();
                    foreach (MGListBoxItem<TItemType> item in SelectedItems)
                    {
                        if (item.LogicalIndex >= 0)
                        {
                            _selectedIndices.Add(item.LogicalIndex);
                        }
                    }

                    NPC(nameof(SelectedItems));
                    NPC(nameof(SelectedValue));
                    NPC(nameof(SelectedDataItems));
                    NPC(nameof(SelectedIndices));
                    SelectionChanged?.Invoke(this, SelectedItems);
                }
            }
        }

        public event EventHandler<ReadOnlyCollection<MGListBoxItem<TItemType>>> SelectionChanged;

        /// <summary>Note: This method clears the selection if <see cref="SelectionMode"/> is <see cref="ListBoxSelectionMode.None"/></summary>
        /// <param name="Item">The item to select. Will search for a <see cref="MGListBoxItem{TItemType}"/> whose <see cref="MGListBoxItem{TItemType}.Data"/> matches this value.</param>
        /// <param name="DeselectAllIfNotFound">If true, and if no corresponding <see cref="MGListBoxItem{TItemType}"/> is found that matches the given <paramref name="Item"/>, the selection will be cleared.</param>
        public void SelectItem(TItemType Item, bool DeselectAllIfNotFound)
        {
            if (SelectionMode is ListBoxSelectionMode.None)
            {
                ClearSelection();
                return;
            }

            if (SelectedItems?.Count == 1 && EqualityComparer.Equals(SelectedItems.First().Data, Item))
            {
                return;
            }

            //  Find ListBoxItem that wraps the Item data
            if (IsVirtualizing && _logicalItemsList != null)
            {
                // In virtual mode InternalItems is null — scan the raw data list directly
                for (int i = 0; i < _logicalItemsList.Count; i++)
                {
                    if (EqualityComparer.Equals(_logicalItemsList[i], Item))
                    {
                        if (_realizedItems.TryGetValue(i, out MGListBoxItem<TItemType> realized))
                        {
                            // Item is currently visible — select through normal path so all events fire
                            SelectedItems = new List<MGListBoxItem<TItemType>>() { realized }.AsReadOnly();
                        }
                        else
                        {
                            // Item is scrolled out of view — track via index; visual applied when realized
                            foreach (var kvp in _realizedItems)
                            {
                                kvp.Value.ContentPresenter.IsSelected = false;
                            }

                            _selectedIndices.Clear();
                            _selectedIndices.Add(i);
                            _SelectedItems = new List<MGListBoxItem<TItemType>>().AsReadOnly();
                            NPC(nameof(SelectedItems));
                            NPC(nameof(SelectedValue));
                            NPC(nameof(SelectedDataItems));
                            NPC(nameof(SelectedIndices));
                            SelectionChanged?.Invoke(this, _SelectedItems);
                        }
                        return;
                    }
                }
            }
            else
            {
                foreach (MGListBoxItem<TItemType> LBI in ListBoxItems ?? Enumerable.Empty<MGListBoxItem<TItemType>>())
                {
                    if (EqualityComparer.Equals(LBI.Data, Item))
                    {
                        //  Select it
                        SelectedItems = new List<MGListBoxItem<TItemType>>() { LBI }.AsReadOnly();
                        return;
                    }
                }
            }

            if (DeselectAllIfNotFound)
            {
                ClearSelection();
            }
        }

        public void ClearSelection()
        {
            SelectionSourceItem = null;
            if (SelectedItems?.Count != 0)
            {
                SelectedItems = new List<MGListBoxItem<TItemType>>().AsReadOnly();
            }
        }

        /// <summary>Selects all items. Only meaningful when <see cref="SelectionMode"/> is <see cref="ListBoxSelectionMode.Multiple"/> or <see cref="ListBoxSelectionMode.Contiguous"/>.</summary>
        public void SelectAll()
        {
            if (SelectionMode == ListBoxSelectionMode.None)
            {
                return;
            }

            if (IsVirtualizing)
            {
                if (_logicalItemsList?.Count > 0)
                {
                    // In virtual mode we track by index; realized items will reflect selection
                    _selectedIndices.Clear();
                    for (int i = 0; i < _logicalItemsList.Count; i++)
                    {
                        _selectedIndices.Add(i);
                    }

                    foreach (var kvp in _realizedItems)
                    {
                        kvp.Value.ContentPresenter.IsSelected = _selectedIndices.Contains(kvp.Key);
                    }

                    _SelectedItems = _realizedItems.Values.ToList().AsReadOnly();
                    NPC(nameof(SelectedItems));
                    NPC(nameof(SelectedValue));
                    NPC(nameof(SelectedDataItems));
                    NPC(nameof(SelectedIndices));
                    SelectionChanged?.Invoke(this, _SelectedItems);
                }
            }
            else
            {
                if (ListBoxItems?.Count > 0)
                {
                    SelectedItems = ListBoxItems.ToList().AsReadOnly();
                }
            }
        }

        /// <summary>Returns the <typeparamref name="TItemType"/> item at the given logical index, or default if out of range.</summary>
        private TItemType GetLogicalItemAt(int index)
        {
            if (IsVirtualizing)
            {
                return (_logicalItemsList != null && index >= 0 && index < _logicalItemsList.Count) ? _logicalItemsList[index] : default;
            }

            return (ListBoxItems != null && index >= 0 && index < ListBoxItems.Count) ? ListBoxItems[index].Data : default;
        }

        #region FocusedIndex
        private int _FocusedIndex = -1;
        /// <summary>The index of the keyboard-focused item, or -1 if none. Updated automatically on mouse click and keyboard navigation.</summary>
        public int FocusedIndex
        {
            get => _FocusedIndex;
            set
            {
                int count = IsVirtualizing ? (_logicalItemsList?.Count ?? 0) : (InternalItems?.Count ?? 0);
                int clamped = count == 0 ? -1 : Math.Clamp(value, 0, count - 1);
                if (_FocusedIndex != clamped) { _FocusedIndex = clamped; NPC(nameof(FocusedIndex)); }
            }
        }
        #endregion FocusedIndex

        /// <summary>The <see cref="MGListBoxItem{TItemType}"/> that the mouse was pressed on during the last mouse left-button press, or null if no item is currently pressed.<br/>
        /// This value is set back to <see langword="null"/> at the END of the update tick when the mouse left-button is released (so that other input-handlers have a chance to read the value before it is invalidated).<para/>
        /// This value can be useful for manually implementing drag-drop behavior.<para/>
        /// Note: By default, clicked items are selected during a mouse-RELEASED event, NOT during a mouse-PRESSED event, so this value may differ from <see cref="SelectedItems"/>.<para/>
        /// See also: <see cref="ReleasedItem"/></summary>
        public MGListBoxItem<TItemType> PressedItem { get; private set; }

        private bool IsPressedItemInvalidationPending { get; set; }

        /// <summary>The most recent <see cref="MGListBoxItem{TItemType}"/> that was clicked (set during a mouse left-button released event).<para/>
        /// Warning - This value is not guaranteed to be in the <see cref="ItemsSource"/> if the source items have changed.<br/>
        /// The <see cref="ReleasedItem"/> could refer to an old item that has been removed from the source list.<para/>
        /// See also: <see cref="PressedItem"/></summary>
        public MGListBoxItem<TItemType> ReleasedItem { get; private set; }
        #endregion Selection

        #region Hover / Pressed visual spoof
        /// <summary>The item currently under the mouse cursor (determined by the consolidated <see cref="MGListBox{TItemType}"/> handler).<para/>
        /// Since item <see cref="MGElement.IsHitTestVisible"/> is <see langword="false"/>, hover visuals are applied via
        /// <see cref="MGElement.SpoofIsHoveredWhileDrawingBackground"/> on the item's <see cref="MGListBoxItem{TItemType}.ContentPresenter"/>.</summary>
        private MGListBoxItem<TItemType> _hoveredItem;

        private void SetHoveredItem(MGListBoxItem<TItemType> newItem)
        {
            if (_hoveredItem == newItem)
            {
                return;
            }

            if (_hoveredItem != null)
            {
                _hoveredItem.ContentPresenter.SpoofIsHoveredWhileDrawingBackground = false;
            }

            _hoveredItem = newItem;
            if (_hoveredItem != null)
            {
                _hoveredItem.ContentPresenter.SpoofIsHoveredWhileDrawingBackground = true;
            }
        }
        #endregion Hover / Pressed visual spoof

        /// <summary>Returns the <see cref="MGListBoxItem{TItemType}"/> at the given mouse position.<para/>
        /// First tries an O(1) mathematical lookup for uniform-height items, then falls back to an O(n) scan using <see cref="MGElement.ActualLayoutBounds"/>.<br/>
        /// This avoids the per-item <see cref="MGElement.IsHovered"/> check (which allocates a coordinate conversion per item).</summary>
        /// <param name="screenPos">Mouse position in scaled screen space.</param>
        private MGListBoxItem<TItemType> GetItemAtMousePosition(Microsoft.Xna.Framework.Point screenPos)
        {
            // Choose total item count from the appropriate source for the current mode
            int itemCount = IsVirtualizing ? (_logicalItemsList?.Count ?? 0) : (InternalItems?.Count ?? 0);
            if (itemCount == 0)
            {
                return null;
            }

            // Convert screen → unscaled screen once
            Vector2 unscaledPos = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.UnscaledScreen, screenPos.ToVector2());

            // Select the actual items panel (regular or virtualizing)
            MGElement activePanel = IsVirtualizing ? (MGElement)_virtualizingPanel : ItemsPanel;

            // Quick reject: mouse must be within the active panel's visible area
            if (activePanel == null || activePanel.ActualLayoutBounds.IsEmpty || !activePanel.ActualLayoutBounds.ContainsInclusive(unscaledPos))
            {
                return null;
            }

            // Fast path: O(1) for uniform-height items
            int uniformH = 0;
            if (IsVirtualizing && _virtualizingPanel != null && _virtualizingPanel.UniformItemHeight > 0)
            {
                uniformH = _virtualizingPanel.UniformItemHeight;
            }
            else if (!IsVirtualizing && InternalItems?.Count >= 1)
            {
                int firstItemH = InternalItems[0].ContentPresenter.AllocatedBounds.Height;
                if (InternalItems.Count < 2 || InternalItems[1].ContentPresenter.AllocatedBounds.Height == firstItemH)
                {
                    uniformH = firstItemH;
                }
            }

            if (uniformH > 0)
            {
                float localY = unscaledPos.Y + activePanel.Origin.Y - activePanel.AlignedContentBounds.Y;
                int index = (int)(localY / uniformH);
                if (index >= 0 && index < itemCount)
                {
                    if (IsVirtualizing)
                    {
                        return _realizedItems.GetValueOrDefault(index);
                    }

                    var candidate = InternalItems[index].ContentPresenter;
                    if (!candidate.ActualLayoutBounds.IsEmpty && candidate.ActualLayoutBounds.ContainsInclusive(unscaledPos))
                    {
                        return InternalItems[index];
                    }
                }
            }

            // Fallback: O(n) scan using ActualLayoutBounds
            if (IsVirtualizing)
            {
                foreach (var kvp in _realizedItems)
                {
                    Rectangle bounds = kvp.Value.ContentPresenter.ActualLayoutBounds;
                    if (!bounds.IsEmpty && bounds.ContainsInclusive(unscaledPos))
                    {
                        return kvp.Value;
                    }
                }
            }
            else if (InternalItems != null)
            {
                for (int i = 0; i < InternalItems.Count; i++)
                {
                    Rectangle bounds = InternalItems[i].ContentPresenter.ActualLayoutBounds;
                    if (!bounds.IsEmpty && bounds.ContainsInclusive(unscaledPos))
                    {
                        return InternalItems[i];
                    }
                }
            }
            return null;
        }

        public MGScrollViewer ScrollViewer { get; private set; }
        public MGStackPanel ItemsPanel { get; private set; }

        protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure Structure)
        {
            OuterBorder = Structure.Parts[OuterBorderPartName] as MGBorder;
            TitleBorder = Structure.Parts[TitleBorderPartName] as MGBorder;
            TitlePresenter = Structure.Parts[TitlePresenterPartName] as MGContentPresenter;
            InnerBorder = Structure.Parts[InnerBorderPartName] as MGBorder;
            ScrollViewer = Structure.Parts[ScrollViewerPartName] as MGScrollViewer;
            ItemsPanel = Structure.Parts[ItemsPanelPartName] as MGStackPanel;

            bool needsOuterBorderNotifications = OuterBorderComponent == null || !ReferenceEquals(OuterBorderComponent.Element, OuterBorder);
            EnsureComponentBinding(() => OuterBorderComponent, value => OuterBorderComponent = value, OuterBorder, MGComponentBase.Create);
            if (needsOuterBorderNotifications)
            {
                OuterBorder.OnBorderBrushChanged += (sender, e) => { NPC(nameof(OuterBorderBrush)); };
                OuterBorder.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(OuterBorderThickness)); };
            }

            bool needsTitleNotifications = TitleComponent == null || !ReferenceEquals(TitleComponent.Element, TitleBorder);
            EnsureComponentBinding(() => TitleComponent, value => TitleComponent = value, TitleBorder,
                element => new(element, true, false, false, true, false, false, false,
                    (AvailableBounds, ComponentSize) => ApplyAlignment(AvailableBounds, HorizontalAlignment.Stretch, VerticalAlignment.Top, ComponentSize.Size)));
            if (needsTitleNotifications)
            {
                TitleBorder.OnBorderBrushChanged += (sender, e) => { NPC(nameof(TitleBorderBrush)); };
                TitleBorder.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(TitleBorderThickness)); };
            }

            bool needsInnerBorderNotifications = InnerBorderComponent == null || !ReferenceEquals(InnerBorderComponent.Element, InnerBorder);
            EnsureComponentBinding(() => InnerBorderComponent, value => InnerBorderComponent = value, InnerBorder,
                element => new(element, true, false, true, true, false, false, false,
                    (AvailableBounds, ComponentSize) => ApplyAlignment(AvailableBounds, HorizontalAlignment.Stretch, VerticalAlignment.Stretch, ComponentSize.Size)));
            if (needsInnerBorderNotifications)
            {
                InnerBorder.OnBorderBrushChanged += (sender, e) => { NPC(nameof(InnerBorderBrush)); };
                InnerBorder.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(InnerBorderThickness)); };
            }

            TitleBorder.CanChangeContent = false;
            TitlePresenter.CanChangeContent = false;
            ItemsPanel.ManagedParent = this;
            ItemsPanel.CanChangeContent = false;

            MGElement activeItemsHost = IsVirtualizing && _virtualizingPanel != null ? _virtualizingPanel : ItemsPanel;
            using (TitleBorder.AllowChangingContentTemporarily())
            {
                TitleBorder.SetContent(TitlePresenter);
            }

            if (AutoManageTitleVisibility)
            {
                SetIsTitleVisible(_Header != null, true);
            }
            using (ScrollViewer.AllowChangingContentTemporarily())
            {
                ScrollViewer.SetContent(activeItemsHost);
            }
            using (InnerBorder.AllowChangingContentTemporarily())
            {
                InnerBorder.SetContent(ScrollViewer);
            }

            ScrollViewer.CanChangeContent = false;
            InnerBorder.CanChangeContent = false;

            if (!IsVirtualizing && InternalItems != null)
            {
                using (ItemsPanel.AllowChangingContentTemporarily())
                {
                    _ = ItemsPanel.TryRemoveAll();
                    foreach (MGListBoxItem<TItemType> item in InternalItems)
                    {
                        _ = ItemsPanel.TryAddChild(item.ContentPresenter);
                    }
                }
            }
            else if (IsVirtualizing && _virtualizingPanel != null)
            {
                SyncVirtualizedItemsPanelChrome();
                _virtualizingPanel.InvalidateData();
            }

            if (_Header != null)
            {
                using (TitlePresenter.AllowChangingContentTemporarily())
                {
                    TitlePresenter.SetContent(_Header);
                }
            }
        }

        private void EnsureFocusedItemVisible()
        {
            if (FocusedIndex < 0 || ScrollViewer == null)
            {
                return;
            }

            if (!IsVirtualizing)
            {
                if (InternalItems != null && FocusedIndex < InternalItems.Count)
                {
                    ScrollViewer.EnsureElementVisible(InternalItems[FocusedIndex].ContentPresenter);
                }

                return;
            }

            if (_realizedItems.TryGetValue(FocusedIndex, out MGListBoxItem<TItemType> realizedItem))
            {
                ScrollViewer.EnsureElementVisible(realizedItem.ContentPresenter);
                return;
            }

            if (ScrollViewer.ContentViewport.Height <= 0)
            {
                return;
            }

            int itemHeight = _virtualizingPanel?.UniformItemHeight > 0 ? _virtualizingPanel.UniformItemHeight : MeasureNaturalItemHeight();
            float contentTop = (_virtualizingPanel as MGElement)?.LayoutBounds.Top ?? ItemsPanel.LayoutBounds.Top;
            float newOffset = GetVisibleVerticalOffsetForIndex(ScrollViewer.VerticalOffset, contentTop, ScrollViewer.ContentViewport.Height,
                ScrollViewer.MaxVerticalOffset, FocusedIndex, itemHeight);

            if (Math.Abs(newOffset - ScrollViewer.VerticalOffset) > 0.5f)
            {
                ScrollViewer.VerticalOffset = newOffset;
            }
        }

        void INavigationTargetVisibilityHandler.EnsureNavigationTargetVisible() => EnsureFocusedItemVisible();

        #region Virtualization
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ListBoxVirtualizationMode _VirtualizationMode = ListBoxVirtualizationMode.Auto;
        /// <summary>Controls whether UI virtualization is used for this ListBox.<para/>
        /// In <see cref="ListBoxVirtualizationMode.Auto"/> mode, virtualization activates when the item count reaches
        /// <see cref="VirtualizationThreshold"/>.<para/>
        /// Default value: <see cref="ListBoxVirtualizationMode.Auto"/></summary>
        public ListBoxVirtualizationMode VirtualizationMode
        {
            get => _VirtualizationMode;
            set
            {
                if (_VirtualizationMode != value)
                {
                    _VirtualizationMode = value;
                    NPC(nameof(VirtualizationMode));
                }
            }
        }

        /// <summary>The minimum item count at which <see cref="ListBoxVirtualizationMode.Auto"/> enables UI virtualization.<para/>
        /// Default value: 100</summary>
        public int VirtualizationThreshold { get; set; } = 100;

        /// <summary>When true, this <see cref="MGListBox{TItemType}"/> is rendering via a <see cref="VirtualizingStackPanel"/> —
        /// only the ~<see cref="VirtualizingStackPanel.BufferCount"/> visible items exist as live <see cref="MGElement"/>s.</summary>
        public bool IsVirtualizing { get; private set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private VirtualizingStackPanel _virtualizingPanel;

        /// <summary>The <see cref="VirtualizingStackPanel"/> used when <see cref="IsVirtualizing"/> is true. Null otherwise.</summary>
        public VirtualizingStackPanel VirtualizingPanel => _virtualizingPanel;

        /// <summary>Raw data list used in virtualised mode — allows O(1) index access
        /// without allocating <see cref="MGListBoxItem{TItemType}"/> wrappers per data entry.</summary>
        private IList<TItemType> _logicalItemsList;

        /// <summary>Currently-realised items in virtualised mode, keyed by their logical (data) index.</summary>
        private readonly Dictionary<int, MGListBoxItem<TItemType>> _realizedItems = new();

        /// <summary>Reverse-maps a <see cref="MGBorder"/> ContentPresenter back to its
        /// owning <see cref="MGListBoxItem{TItemType}"/> so the recycle pool can reuse wrappers.</summary>
        private readonly Dictionary<MGBorder, MGListBoxItem<TItemType>> _contentPresToItem = new();

        private bool ShouldVirtualize(int itemCount) =>
            VirtualizationMode == ListBoxVirtualizationMode.Always ||
            (VirtualizationMode == ListBoxVirtualizationMode.Auto && itemCount >= VirtualizationThreshold);

        /// <summary>Creates the <see cref="VirtualizingStackPanel"/> if needed and configures its generator and recycler callbacks.</summary>
        private void ConfigureVirtualizingPanel()
        {
            if (_virtualizingPanel == null)
            {
                _virtualizingPanel = new VirtualizingStackPanel(SelfOrParentWindow);
                _virtualizingPanel.VerticalAlignment = VerticalAlignment.Top;
            }

            SyncVirtualizedItemsPanelChrome();

            int totalCount = _logicalItemsList?.Count ?? InternalItems?.Count ?? 0;
            _virtualizingPanel.TotalItemCount = totalCount;
            _virtualizingPanel.UniformItemHeight = MeasureNaturalItemHeight();
            _virtualizingPanel.ItemGenerator = (idx) =>
            {
                MGListBoxItem<TItemType> item;

                // Try to reuse a recycled wrapper from the pool
                if (_logicalItemsList != null &&
                    _virtualizingPanel.TryDequeueRecycledElement(out MGElement recycledEl) &&
                    recycledEl is MGBorder recycledCp &&
                    _contentPresToItem.TryGetValue(recycledCp, out MGListBoxItem<TItemType> recycledItem))
                {
                    // Rebind the recycled wrapper to the new data index
                    recycledItem.UpdateData(idx, _logicalItemsList[idx]);
                    item = recycledItem;
                }
                else if (_logicalItemsList != null)
                {
                    // Allocate a new wrapper (pool was empty or not yet populated)
                    item = new MGListBoxItem<TItemType>(this, _logicalItemsList[idx]) { LogicalIndex = idx };
                    _contentPresToItem[item.ContentPresenter] = item;
                }
                else
                {
                    // Non-pooled fallback (InternalItems path, should not occur when fully virtualizing)
                    item = InternalItems[idx];
                }

                _realizedItems[idx] = item;
                item.ContentPresenter.IsSelected = _selectedIndices.Contains(idx);
                return item.ContentPresenter;
            };
            _virtualizingPanel.ItemRecycler = (idx, element) =>
            {
                _realizedItems.Remove(idx);
                // Clear transient visual states so the recycled element is clean for its next use
                if (element is MGBorder cp)
                {
                    cp.SpoofIsHoveredWhileDrawingBackground = false;
                    cp.SpoofIsPressedWhileDrawingBackground = false;
                }
            };

            // Discard any stale realized items from the previous data source.
            // Without this, if the visible range [firstNeeded..lastNeeded] happens to be identical
            // to the cached range from the previous source, the VirtualizingStackPanel skips
            // re-realization entirely and keeps showing the old items — breaking filter switches.
            _virtualizingPanel.InvalidateData();
        }

        private void SyncVirtualizedItemsPanelChrome()
        {
            if (_virtualizingPanel == null)
            {
                return;
            }

            _virtualizingPanel.BorderThickness = ItemsPanel?.BorderThickness ?? MGControlTemplateCatalog.DefaultListBoxItemBorderThickness;
            _virtualizingPanel.BorderBrush = ItemsPanel?.BorderBrush ?? MGControlTemplateCatalog.CreateDefaultListBoxItemBorderBrush();
        }

        /// <summary>Estimates the pixel height of individual items for the <see cref="VirtualizingStackPanel"/>.<br/>
        /// Uses the first realized item's measured height, or a theme-based fallback.</summary>
        private int EstimateItemHeight()
        {
            if (InternalItems?.Count > 0)
            {
                int h = InternalItems[0].ContentPresenter.AllocatedBounds.Height;
                if (h > 0)
                {
                    return h;
                }

                h = InternalItems[0].ContentPresenter.LayoutBounds.Height;
                if (h > 0)
                {
                    return h;
                }
            }
            return 26; // Fallback: ~6 px padding top + 14 px text + 6 px padding bottom
        }

        /// <summary>Measures the natural (un-stretched) height of a single item by creating a probe element,
        /// running a layout pass with <see cref="VerticalAlignment.Top"/> so it occupies only its preferred height,
        /// and reading the resulting <see cref="MGElement.LayoutBounds"/>.<para/>
        /// This is necessary in virtualised mode because <see cref="InternalItems"/> is null and there are no
        /// realized elements to read from before the first layout pass.</summary>
        private int MeasureNaturalItemHeight()
        {
            // Fast path: non-virtualizing mode already has realized items we can measure
            if (InternalItems?.Count > 0)
            {
                return EstimateItemHeight();
            }

            if (_logicalItemsList == null || _logicalItemsList.Count == 0 || ItemTemplate == null)
            {
                return 26;
            }

            try
            {
                // Build the item content via the template (same as real items)
                MGElement content = ItemTemplate(_logicalItemsList[0]);
                if (content == null)
                {
                    return 26;
                }

                // Wrap in a border that matches real item container style, but TOP-aligned so it
                // takes its natural height instead of stretching to fill whatever slot we give it.
                var probe = new MGBorder(SelfOrParentWindow)
                {
                    VerticalAlignment = VerticalAlignment.Top,
                };
                ApplyDefaultItemContainerStyle(probe);
                using (probe.AllowChangingContentTemporarily())
                    probe.SetContent(content);

                // Use a wide rect so text wrapping doesn't artificially inflate height,
                // and a tall rect (2000) so VA=Top reports the true natural height.
                probe.UpdateLayout(new Rectangle(0, 0, 1200, 2000));
                int h = probe.LayoutBounds.Height;
                return h > 4 ? h : 26;
            }
            catch
            {
                return 26;
            }
        }
        #endregion Virtualization

        /// <summary>Sets the <see cref="TitleBorderBrush"/> to the given <paramref name="Brush"/> using the given <paramref name="BorderThickness"/>, except with a bottom thickness of 0 to avoid doubled thickness between the title and content.<br/>
        /// Sets the <see cref="InnerBorderBrush"/> to the given <paramref name="Brush"/> using the given <paramref name="BorderThickness"/></summary>
        public void SetTitleAndContentBorder(IFillBrush Brush, int BorderThickness)
        {
            TitleBorderBrush = Brush?.AsUniformBorderBrush();
            TitleBorderThickness = new(BorderThickness, BorderThickness, BorderThickness, 0);

            InnerBorderBrush = Brush?.AsUniformBorderBrush();
            InnerBorderThickness = new(BorderThickness);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _Header;
        /// <summary>Content to display inside the <see cref="TitlePresenter"/>. Only relevant if <see cref="IsTitleVisible"/> is true.</summary>
        public MGElement Header
        {
            get => _Header;
            set
            {
                if (_Header != value)
                {
                    _Header = value;
                    if (TitlePresenter != null)
                    {
                        using (TitlePresenter.AllowChangingContentTemporarily())
                        {
                            TitlePresenter.SetContent(Header);
                        }
                    }

                    if (AutoManageTitleVisibility)
                    {
                        SetIsTitleVisible(Header != null, true);
                    }

                    NPC(nameof(Header));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Func<TItemType, MGElement> _ItemTemplate;
        /// <summary>This function is invoked to instantiate the <see cref="MGListBoxItem{TItemType}.Content"/> of each <see cref="MGListBoxItem{TItemType}"/> in this <see cref="MGListBox{TItemType}"/></summary>
        public Func<TItemType, MGElement> ItemTemplate
        {
            get => _ItemTemplate;
            set
            {
                if (_ItemTemplate != value)
                {
                    _ItemTemplate = value;
                    NPC(nameof(ItemTemplate));
                    ItemTemplateChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler<EventArgs> ItemTemplateChanged;

        public void ApplyDefaultItemContainerStyle(MGBorder Item)
            => MGControlTemplateCatalog.ApplyListBoxItemContainerDefaults(this, Item);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Action<MGBorder> _ItemContainerStyle;
        /// <summary>An action that will be invoked on every <see cref="MGBorder"/> that wraps each <see cref="MGListBoxItem{TItemType}"/>'s content.<para/>
        /// See also: <see cref="MGListBoxItem{TItemType}.ContentPresenter"/><para/>
        /// Default value: <see cref="ApplyDefaultItemContainerStyle(MGBorder)"/></summary>
        public Action<MGBorder> ItemContainerStyle
        {
            get => _ItemContainerStyle;
            set
            {
                if (_ItemContainerStyle != value)
                {
                    _ItemContainerStyle = value;
                    NPC(nameof(ItemContainerStyle));
                    ItemContainerStyleChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public event EventHandler<EventArgs> ItemContainerStyleChanged;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ReadOnlyCollection<IFillBrush> _AlternatingRowBackgrounds;
        /// <summary>If not null/empty, the row items will cycle through these <see cref="IFillBrush"/> for their backgrounds</summary>
        public ReadOnlyCollection<IFillBrush> AlternatingRowBackgrounds
        {
            get => _AlternatingRowBackgrounds;
            set
            {
                if (_AlternatingRowBackgrounds != value)
                {
                    _AlternatingRowBackgrounds = value;
                    RefreshRowBackgrounds();
                    NPC(nameof(AlternatingRowBackgrounds));
                }
            }
        }

        private void RefreshRowBackgrounds()
        {
            if (InternalItems != null)
            {
                for (int i = 0; i < InternalItems.Count; i++)
                {
                    IFillBrush Brush = AlternatingRowBackgrounds?.Any() == true ? AlternatingRowBackgrounds[i % AlternatingRowBackgrounds.Count] : null;
                    InternalItems[i].ContentPresenter.BackgroundBrush.NormalValue = Brush;
                }
            }
        }

        public MGListBox(MGWindow ParentWindow)
            : base(ParentWindow, MGElementType.ListBox)
        {
            using (BeginInitializing())
            {
                AlternatingRowBackgrounds = GetTheme().ListBoxItemAlternatingRowBackgrounds.Select(x => x.GetValue(true)).ToList().AsReadOnly();

                ItemContainerStyle = ApplyDefaultItemContainerStyle;
                ItemTemplate = item => MGControlTemplateCatalog.CreateDefaultListBoxItemContent(ParentWindow, item);

                SelectedItems = new List<MGListBoxItem<TItemType>>().AsReadOnly();
                SelectionMode = ListBoxSelectionMode.Single;
                CanDeselectByClickingSelectedItem = true;
                DefaultControlTemplateName = MGControlTemplateCatalog.ListBoxTemplateName;
                SetIsTitleVisible(false, true);

                GetDesktop().Runtime.EndUpdate += (sender, e) =>
                {
                    //  Reset PressedItem to null at the end of an update tick, rather than immediately when the mouse button is released,
                    //  because other input handlers with lower priority still need a chance to read the data before it is modified.
                    if (IsPressedItemInvalidationPending)
                    {
                        IsPressedItemInvalidationPending = false;
                        if (PressedItem != null)
                        {
                            PressedItem.ContentPresenter.SpoofIsPressedWhileDrawingBackground = false;
                        }

                        PressedItem = null;
                    }
                };

                MouseHandler.MovedInside += (sender, e) =>
                {
                    SetHoveredItem(GetItemAtMousePosition(e.CurrentPosition));
                };

                MouseHandler.Exited += (sender, e) =>
                {
                    SetHoveredItem(null);
                };

                MouseHandler.LMBPressedInside += (sender, e) =>
                {
                    if (PressedItem != null)
                    {
                        PressedItem.ContentPresenter.SpoofIsPressedWhileDrawingBackground = false;
                    }

                    PressedItem = GetItemAtMousePosition(e.Position);
                    if (PressedItem != null)
                    {
                        PressedItem.ContentPresenter.SpoofIsPressedWhileDrawingBackground = true;
                    }
                };

                MouseHandler.ReleasedOutside += (sender, e) =>
                {
                    if (e.IsLMB)
                    {
                        IsPressedItemInvalidationPending = true;
                    }
                };

                MouseHandler.LMBReleasedInside += (sender, e) =>
                {
                    IsPressedItemInvalidationPending = true;
                    ReleasedItem = GetItemAtMousePosition(e.Position);

                    if (ReleasedItem != null)
                    {
                        bool IsReleasedItemAlreadySelected = SelectedItems?.Contains(ReleasedItem) == true;
                        bool IsShiftDown = InputTracker.Keyboard.IsShiftDown;
                        bool IsControlDown = InputTracker.Keyboard.IsControlDown;

                        void SelectSingle()
                        {
                            if (IsReleasedItemAlreadySelected && CanDeselectByClickingSelectedItem && SelectedItems.Count <= 1)
                            {
                                ClearSelection();
                            }
                            else
                            {
                                SelectionSourceItem = ReleasedItem;
                                SelectedItems = new List<MGListBoxItem<TItemType>>() { ReleasedItem }.AsReadOnly();
                            }
                        }

                        void SelectContiguous()
                        {
                            if (!IsShiftDown || SelectionSourceItem == null ||
                                (InternalItems != null && !InternalItems.Contains(SelectionSourceItem)))
                            {
                                SelectSingle();
                            }
                            else if (InternalItems != null)
                            {
                                int SourceIndex = InternalItems.IndexOf(SelectionSourceItem);
                                int PressedIndex = InternalItems.IndexOf(ReleasedItem);

                                int StartIndex = Math.Min(SourceIndex, PressedIndex);
                                int EndIndex = Math.Max(SourceIndex, PressedIndex);

                                SelectedItems = InternalItems.Skip(StartIndex).Take(EndIndex - StartIndex + 1).ToList().AsReadOnly();
                            }
                            else
                            {
                                // Virtual mode: contiguous selection degrades to single selection
                                // (full range would span unrealized items that have no wrapper instances)
                                SelectSingle();
                            }
                        }

                        switch (SelectionMode)
                        {
                            case ListBoxSelectionMode.None:
                                break;
                            case ListBoxSelectionMode.Single:
                                SelectSingle();
                                break;
                            case ListBoxSelectionMode.Contiguous:
                                SelectContiguous();
                                break;
                            case ListBoxSelectionMode.Multiple:
                                if (IsControlDown)
                                {
                                    if (IsReleasedItemAlreadySelected)
                                    {
                                        SelectedItems = SelectedItems.Where(x => x != ReleasedItem).ToList().AsReadOnly();
                                    }
                                    else
                                    {
                                        SelectedItems = SelectedItems.Append(ReleasedItem).ToList().AsReadOnly();
                                    }
                                }
                                else
                                {
                                    SelectContiguous();
                                }

                                break;
                            default: throw new NotImplementedException($"Unrecognized {nameof(ListBoxSelectionMode)}: {nameof(SelectionMode)}");
                        }
                        if (ReleasedItem != null)
                        {
                            int idx = IsVirtualizing
                                ? (_logicalItemsList?.IndexOf(ReleasedItem.Data) ?? -1)
                                : (ListBoxItems != null ? ListBoxItems.ToList().IndexOf(ReleasedItem) : -1);
                            if (idx >= 0)
                            {
                                FocusedIndex = idx;
                            }
                        }
                    }
                };

                IsFocusable = true;
                KeyboardHandler.Pressed += OnListBoxKeyPressed;
            }
        }

        private void OnListBoxKeyPressed(object sender, BaseKeyPressedEventArgs e)
        {
            if (e.IsHandled)
            {
                return;
            }

            int count = IsVirtualizing ? (_logicalItemsList?.Count ?? 0) : (ListBoxItems?.Count ?? 0);
            if (count == 0)
            {
                return;
            }

            bool isCtrlDown = e.Tracker.IsControlDown;

            // Ctrl+A — select all (Multiple/Contiguous mode)
            if (isCtrlDown && e.Key == Keys.A)
            {
                if (SelectionMode == ListBoxSelectionMode.Multiple || SelectionMode == ListBoxSelectionMode.Contiguous)
                {
                    SelectAll();
                    e.SetHandledBy(this, true);
                }
                return;
            }
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            int count = IsVirtualizing ? (_logicalItemsList?.Count ?? 0) : (ListBoxItems?.Count ?? 0);
            if (count == 0)
            {
                return false;
            }

            if (action == UINavigationAction.Submit && FocusedIndex >= 0 && FocusedIndex < count && SelectionMode != ListBoxSelectionMode.None)
            {
                TItemType focusedItem = GetLogicalItemAt(FocusedIndex);
                if (focusedItem != null)
                {
                    SelectItem(focusedItem, true);
                    return true;
                }
            }

            if (action is not (UINavigationAction.MoveUp or UINavigationAction.MoveDown or UINavigationAction.Home or UINavigationAction.End or UINavigationAction.PageUp or UINavigationAction.PageDown))
            {
                return false;
            }

            int nextIndex = GetNextNavigationIndex(FocusedIndex, count, action);
            if (nextIndex < 0)
            {
                return false;
            }

            FocusedIndex = nextIndex;
            if (SelectionMode != ListBoxSelectionMode.None)
            {
                TItemType focusedItem = GetLogicalItemAt(FocusedIndex);
                if (focusedItem != null)
                {
                    SelectItem(focusedItem, true);
                }
            }

            return true;
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            base.DrawSelf(DA, LayoutBounds);
        }

        //  This method is invoked via reflection in MGUI.Core.UI.XAML.Lists.ListBox.ApplyDerivedSettings.
        //  Do not modify the method signature.
        internal void LoadSettings(ListBox Settings, bool IncludeContent)
        {
            MGDesktop Desktop = GetDesktop();

            Settings.OuterBorder.ApplySettings(this, OuterBorder, false);
            Settings.InnerBorder.ApplySettings(this, InnerBorder, false);
            Settings.TitleBorder.ApplySettings(this, TitleBorder, false);
            Settings.TitlePresenter.ApplySettings(this, TitlePresenter, false);
            Settings.ScrollViewer.ApplySettings(this, ScrollViewer, false);
            Settings.ItemsPanel.ApplySettings(this, ItemsPanel, false);

            if (Settings.Header != null)
            {
                Header = Settings.Header.ToElement<MGElement>(SelfOrParentWindow, this);
            }

            if (Settings.IsTitleVisible.HasValue)
            {
                IsTitleVisible = Settings.IsTitleVisible.Value;
                if (!IsTitleVisible)
                {
                    TitleBorderThickness = new(0);
                    InnerBorderThickness = new(0);
                }
            }

            if (Settings.Items?.Any() == true)
            {
                List<TItemType> TempItems = new();
                Type TargetType = typeof(TItemType);
                foreach (object Item in Settings.Items)
                {
                    if (TargetType.IsAssignableFrom(Item.GetType()))
                    {
                        TItemType Value = (TItemType)Item;
                        TempItems.Add(Value);
                    }
                }

                if (TempItems.Any())
                {
                    SetItemsSource(TempItems);
                }
            }

            if (Settings.CanDeselectByClickingSelectedItem.HasValue)
            {
                CanDeselectByClickingSelectedItem = Settings.CanDeselectByClickingSelectedItem.Value;
            }

            if (Settings.SelectionMode.HasValue)
            {
                SelectionMode = Settings.SelectionMode.Value;
            }

            if (Settings.SelectedValue is TItemType SelectedT)
            {
                SelectedValue = SelectedT;
            }

            if (Settings.AlternatingRowBackgrounds != null && Settings.AlternatingRowBackgrounds.Any())
            {
                AlternatingRowBackgrounds = Settings.AlternatingRowBackgrounds.Select(x => x.ToFillBrush(Desktop, this)).ToList().AsReadOnly();
            }
            else
            {
                AlternatingRowBackgrounds = new List<IFillBrush>().AsReadOnly();
            }

            if (Settings.ItemContainerStyle != null)
            {
                ItemContainerStyle = (Border) => { Settings.ItemContainerStyle.ApplySettings(this, Border, false); };
            }

            if (Settings.ItemTemplate != null)
            {
                ItemTemplate = (Item) => Settings.ItemTemplate.GetContent(SelfOrParentWindow, this, Item);
            }
        }
    }

    public class MGListBoxItem<TItemType> : ViewModelBase
    {
        public MGListBox<TItemType> ListBox { get; }

        /// <summary>The data object used as a parameter to generate the content of this item.<para/>
        /// See also: <see cref="MGListBox{TItemType}.ItemTemplate"/></summary>
        public TItemType Data { get; private set; }

        /// <summary>The zero-based index of this item in the logical data source (<see cref="MGListBox{TItemType}.ItemsSource"/>).<para/>
        /// In non-virtualized mode this equals the item's position in <see cref="MGListBox{TItemType}.ListBoxItems"/>.<br/>
        /// In virtualized mode this is updated each time the item is recycled and rebound to a different data entry.</summary>
        public int LogicalIndex { get; internal set; } = -1;

        /// <summary>The wrapper element that hosts this item's content</summary>
        public MGBorder ContentPresenter { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _Content;
        /// <summary>This <see cref="MGElement"/> is automatically generated via <see cref="MGListBox{TItemType}.ItemTemplate"/> using this.<see cref="Data"/> as the parameter.<para/>
        /// See also: <see cref="ContentPresenter"/></summary>
        public MGElement Content
        {
            get => _Content;
            private set
            {
                if (_Content != value)
                {
                    _Content = value;
                    using (ContentPresenter.AllowChangingContentTemporarily())
                    {
                        ContentPresenter.SetContent(Content);
                    }
                    NPC(nameof(Content));
                }
            }
        }

        internal MGListBoxItem(MGListBox<TItemType> ListBox, TItemType Data)
        {
            this.ListBox = ListBox ?? throw new ArgumentNullException(nameof(ListBox));
            this.Data = Data ?? throw new ArgumentNullException(nameof(Data));
            ContentPresenter = new(ListBox.SelfOrParentWindow);
            //  Disable per-item hit-testing: the MGListBox consolidated handler manages all input and
            //  updates hover/pressed visuals via SpoofIsHoveredWhileDrawingBackground / SpoofIsPressedWhileDrawingBackground.
            //  This eliminates ~5800 ManualUpdate() calls per frame when the list is large.
            ContentPresenter.IsHitTestVisible = false;

            ListBox.ItemTemplateChanged += (sender, e) =>
            {
                Content?.RemoveDataBindings(true);
                Content = ListBox.ItemTemplate?.Invoke(this.Data);
            };
            Content = ListBox.ItemTemplate?.Invoke(this.Data);
            ContentPresenter.CanChangeContent = false;

            ListBox.ItemContainerStyleChanged += (sender, e) => { ListBox.ItemContainerStyle?.Invoke(ContentPresenter); };
            ListBox.ItemContainerStyle?.Invoke(ContentPresenter);
        }

        /// <summary>Rebinds this item to a different data entry — used when recycling items in a <see cref="VirtualizingStackPanel"/>.<para/>
        /// Removes old data bindings, updates <see cref="Data"/> and <see cref="LogicalIndex"/>, regenerates <see cref="Content"/>.</summary>
        internal void UpdateData(int logicalIndex, TItemType newData)
        {
            Content?.RemoveDataBindings(true);
            Data = newData;
            LogicalIndex = logicalIndex;
            Content = ListBox.ItemTemplate?.Invoke(newData);
        }
    }
}
