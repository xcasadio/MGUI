using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
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
    public class MGListBox<TItemType> : MGElement
    {
        #region Outer Border
        private MGComponent<MGBorder> OuterBorderComponent { get; }
        /// <summary><see cref="MGListBox{TItemType}"/>es contain 3 borders:<para/>
        /// 1. <see cref="OuterBorder"/>: Wrapped around the entire <see cref="MGListBox{TItemType}"/><br/>
        /// 2. <see cref="InnerBorder"/>: Wrapped around the <see cref="ItemsPanel"/>, but not the <see cref="TitleComponent"/><br/>
        /// 3. <see cref="TitleBorder"/>: Wrapped around the <see cref="TitleComponent"/></summary>
        public MGBorder OuterBorder { get; }
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
        private MGComponent<MGBorder> InnerBorderComponent { get; }
        /// <summary><see cref="MGListBox{TItemType}"/>es contain 3 borders:<para/>
        /// 1. <see cref="OuterBorder"/>: Wrapped around the entire <see cref="MGListBox{TItemType}"/><br/>
        /// 2. <see cref="InnerBorder"/>: Wrapped around the <see cref="ItemsPanel"/>, but not the <see cref="TitleComponent"/><br/>
        /// 3. <see cref="TitleBorder"/>: Wrapped around the <see cref="TitleComponent"/></summary>
        public MGBorder InnerBorder { get; }

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
        private MGComponent<MGBorder> TitleComponent { get; }
        /// <summary><see cref="MGListBox{TItemType}"/>es contain 3 borders:<para/>
        /// 1. <see cref="OuterBorder"/>: Wrapped around the entire <see cref="MGListBox{TItemType}"/><br/>
        /// 2. <see cref="InnerBorder"/>: Wrapped around the <see cref="ItemsPanel"/>, but not the <see cref="TitleComponent"/><br/>
        /// 3. <see cref="TitleBorder"/>: Wrapped around the <see cref="TitleComponent"/></summary>
        public MGBorder TitleBorder { get; }
        public MGContentPresenter TitlePresenter { get; }

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
            set
            {
                if (IsTitleVisible != value)
                {
                    TitleBorder.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
                    NPC(nameof(IsTitleVisible));
                }
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
                            OnListBoxItemRemoved?.Invoke(this, Removed);
                    }
                    _InternalItems = value;
                    if (InternalItems != null)
                    {
                        InternalItems.CollectionChanged += ListBoxItems_CollectionChanged;
                        foreach (MGListBoxItem<TItemType> Added in InternalItems)
                            OnListBoxItemAdded?.Invoke(this, Added);
                    }

                    using (ItemsPanel.AllowChangingContentTemporarily())
                    {
                        //  Clear all ListBoxItems
                        _ = ItemsPanel.TryRemoveAll();

                        //  Add the new ListBoxItems to the ItemsPanel (skipped when virtualizing: VSP manages its own children)
                        if (!IsVirtualizing && InternalItems != null)
                        {
                            foreach (MGListBoxItem<TItemType> LBI in InternalItems)
                                _ = ItemsPanel.TryAddChild(LBI.ContentPresenter);
                        }
                    }

                    // When virtualizing, update the VSP item count after the InternalItems collection is assigned
                    if (IsVirtualizing && _virtualizingPanel != null)
                        _virtualizingPanel.TotalItemCount = InternalItems?.Count ?? 0;

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
                    Item.RemoveDataBindings(true);
            }
        }

        private void ListBoxItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (IsVirtualizing)
            {
                // When virtualizing, only keep the VSP total count in sync; the VSP manages its own children
                if (_virtualizingPanel != null)
                    _virtualizingPanel.TotalItemCount = InternalItems?.Count ?? 0;

                HashSet<MGListBoxItem<TItemType>> virtualRemoved = new();
                if (e.Action is NotifyCollectionChangedAction.Reset)
                {
                    ClearSelection();
                }
                else if (e.Action is NotifyCollectionChangedAction.Remove && e.OldItems != null)
                {
                    foreach (MGListBoxItem<TItemType> item in e.OldItems)
                        virtualRemoved.Add(item);
                }

                // Clean up selection for removed items
                if (virtualRemoved.Count > 0 && SelectedItems != null)
                {
                    List<MGListBoxItem<TItemType>> newSel = SelectedItems.Where(x => !virtualRemoved.Contains(x)).ToList();
                    if (newSel.Count != SelectedItems.Count)
                        SelectedItems = newSel.AsReadOnly();
                }
                if (SelectionSourceItem != null && virtualRemoved.Contains(SelectionSourceItem))
                    SelectionSourceItem = null;

                if (virtualRemoved.Count > 0)
                    HandleTemplatedContentRemoved(virtualRemoved.Select(x => x.Content));
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
                                New[i].ContentPresenter.BackgroundBrush.NormalValue = Old[i].ContentPresenter.BackgroundBrush.NormalValue;
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
                        SelectedItems = NewSelectedItems.AsReadOnly();
                }
                if (SelectionSourceItem != null && Removed.Contains(SelectionSourceItem))
                    SelectionSourceItem = null;

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
                        Observable.CollectionChanged -= ItemsSource_CollectionChanged;
                    _ItemsSource = value;
                    if (ItemsSource != null && ItemsSource is INotifyCollectionChanged Observable2)
                        Observable2.CollectionChanged += ItemsSource_CollectionChanged;

                    if (ItemsSource == null)
                    {
                        bool wasVirtualizing = IsVirtualizing;
                        IsVirtualizing = false;
                        InternalItems = null;
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

                        IEnumerable<MGListBoxItem<TItemType>> Values = ItemsSource.Select((x, Index) => new MGListBoxItem<TItemType>(this, x));
                        InternalItems = new ObservableCollection<MGListBoxItem<TItemType>>(Values);

                        if (newVirtualize && ScrollViewer != null)
                        {
                            ConfigureVirtualizingPanel();
                            if (!wasVirtualizing)
                            {
                                using (ScrollViewer.AllowChangingContentTemporarily())
                                    ScrollViewer.SetContent(_virtualizingPanel);
                            }
                        }
                        else if (!newVirtualize && wasVirtualizing && ScrollViewer != null)
                        {
                            using (ScrollViewer.AllowChangingContentTemporarily())
                                ScrollViewer.SetContent(ItemsPanel);
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
                    yield break;
                IList<TItemType> asList = ItemsSource as IList<TItemType> ?? ItemsSource.ToList();
                foreach (int idx in _selectedIndices)
                    if (idx >= 0 && idx < asList.Count)
                        yield return asList[idx];
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
                            Item.ContentPresenter.IsSelected = false;
                    }

                    _SelectedItems = value ?? new List<MGListBoxItem<TItemType>>().AsReadOnly();

                    if (SelectedItems.Any(x => x == null))
                        throw new ArgumentNullException($"{nameof(MGListBoxItem<object>)}.{nameof(SelectedItems)} cannnot contain null items.");

                    foreach (MGListBoxItem<TItemType> Item in SelectedItems)
                        Item.ContentPresenter.IsSelected = true;

                    // Maintain index-based selection set in sync
                    _selectedIndices.Clear();
                    if (InternalItems != null)
                    {
                        foreach (MGListBoxItem<TItemType> item in SelectedItems)
                        {
                            int idx = InternalItems.IndexOf(item);
                            if (idx >= 0)
                                _selectedIndices.Add(idx);
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
                return;

            //  Find ListBoxItem that wraps the Item data
            foreach (MGListBoxItem<TItemType> LBI in ListBoxItems)
            {
                if (EqualityComparer.Equals(LBI.Data, Item))
                {
                    //  Select it
                    SelectedItems = new List<MGListBoxItem<TItemType>>() { LBI }.AsReadOnly();
                    return;
                }
            }

            if (DeselectAllIfNotFound)
                ClearSelection();
        }

        public void ClearSelection()
        {
            SelectionSourceItem = null;
            if (SelectedItems?.Count != 0)
                SelectedItems = new List<MGListBoxItem<TItemType>>().AsReadOnly();
        }

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
                return;
            if (_hoveredItem != null)
                _hoveredItem.ContentPresenter.SpoofIsHoveredWhileDrawingBackground = false;
            _hoveredItem = newItem;
            if (_hoveredItem != null)
                _hoveredItem.ContentPresenter.SpoofIsHoveredWhileDrawingBackground = true;
        }
        #endregion Hover / Pressed visual spoof

        /// <summary>Returns the <see cref="MGListBoxItem{TItemType}"/> at the given mouse position.<para/>
        /// First tries an O(1) mathematical lookup for uniform-height items, then falls back to an O(n) scan using <see cref="MGElement.ActualLayoutBounds"/>.<br/>
        /// This avoids the per-item <see cref="MGElement.IsHovered"/> check (which allocates a coordinate conversion per item).</summary>
        /// <param name="screenPos">Mouse position in scaled screen space.</param>
        private MGListBoxItem<TItemType> GetItemAtMousePosition(Microsoft.Xna.Framework.Point screenPos)
        {
            if (InternalItems == null || InternalItems.Count == 0)
                return null;

            // Convert screen → unscaled screen once
            Vector2 unscaledPos = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.UnscaledScreen, screenPos.ToVector2());

            // Select the actual items panel (regular or virtualizing)
            MGElement activePanel = IsVirtualizing ? (MGElement)_virtualizingPanel : ItemsPanel;

            // Quick reject: mouse must be within the active panel's visible area
            if (activePanel == null || activePanel.ActualLayoutBounds.IsEmpty || !activePanel.ActualLayoutBounds.ContainsInclusive(unscaledPos))
                return null;

            // Fast path: O(1) for uniform-height items
            // localY = position within the panel's content area (accounting for scroll)
            int itemCount = InternalItems.Count;

            // Use UniformItemHeight from VSP directly, otherwise read from first two items
            int uniformH = 0;
            if (IsVirtualizing && _virtualizingPanel != null && _virtualizingPanel.UniformItemHeight > 0)
            {
                uniformH = _virtualizingPanel.UniformItemHeight;
            }
            else if (itemCount >= 1)
            {
                int firstItemH = InternalItems[0].ContentPresenter.AllocatedBounds.Height;
                if (itemCount < 2 || InternalItems[1].ContentPresenter.AllocatedBounds.Height == firstItemH)
                    uniformH = firstItemH;
            }

            if (uniformH > 0)
            {
                float localY = unscaledPos.Y + activePanel.Origin.Y - activePanel.AlignedContentBounds.Y;
                int index = (int)(localY / uniformH);
                if (index >= 0 && index < itemCount)
                {
                    var candidate = InternalItems[index].ContentPresenter;
                    if (!candidate.ActualLayoutBounds.IsEmpty && candidate.ActualLayoutBounds.ContainsInclusive(unscaledPos))
                        return InternalItems[index];
                    // For virtualized mode, trust the mathematical index even if ContentPresenter hasn't been updated yet
                    if (IsVirtualizing)
                        return InternalItems[index];
                }
            }

            // Fallback: O(n) scan using ActualLayoutBounds (fast rect check, no per-item coordinate conversion)
            // For virtualized mode this only checks realized items (non-empty ActualLayoutBounds)
            for (int i = 0; i < itemCount; i++)
            {
                Rectangle bounds = InternalItems[i].ContentPresenter.ActualLayoutBounds;
                if (!bounds.IsEmpty && bounds.ContainsInclusive(unscaledPos))
                    return InternalItems[i];
            }
            return null;
        }

        public MGScrollViewer ScrollViewer { get; }
        public MGStackPanel ItemsPanel { get; }

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
                _virtualizingPanel.BorderThickness = DefaultItemBorderThickness;
                _virtualizingPanel.BorderBrush = DefaultItemBorderBrush;
            }
            _virtualizingPanel.TotalItemCount = InternalItems?.Count ?? 0;
            _virtualizingPanel.UniformItemHeight = EstimateItemHeight();
            _virtualizingPanel.ItemGenerator = (idx) =>
            {
                var cp = InternalItems[idx].ContentPresenter;
                // Restore correct selection state when an item is realized
                cp.IsSelected = _selectedIndices.Contains(idx);
                return cp;
            };
            _virtualizingPanel.ItemRecycler = (idx, element) =>
            {
                // Clear any spoof states so they are fresh for the next occupant
                if (element is MGBorder cp)
                {
                    cp.SpoofIsHoveredWhileDrawingBackground = false;
                    cp.SpoofIsPressedWhileDrawingBackground = false;
                }
            };
        }

        /// <summary>Estimates the pixel height of individual items for the <see cref="VirtualizingStackPanel"/>.<br/>
        /// Uses the first realized item's measured height, or a theme-based fallback.</summary>
        private int EstimateItemHeight()
        {
            if (InternalItems?.Count > 0)
            {
                int h = InternalItems[0].ContentPresenter.AllocatedBounds.Height;
                if (h > 0) return h;
                h = InternalItems[0].ContentPresenter.LayoutBounds.Height;
                if (h > 0) return h;
            }
            return 26; // Fallback: ~6 px padding top + 14 px text + 6 px padding bottom
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
                    using (TitlePresenter.AllowChangingContentTemporarily())
                    {
                        TitlePresenter.SetContent(Header);
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

        public readonly MGUniformBorderBrush DefaultItemBorderBrush = new MGSolidFillBrush(Color.Black * 0.35f).AsUniformBorderBrush();
        public readonly Thickness DefaultItemBorderThickness = new(0, 1);

        public void ApplyDefaultItemContainerStyle(MGBorder Item)
        {
            Item.BorderBrush = DefaultItemBorderBrush;
            Item.BorderThickness = DefaultItemBorderThickness;
            Item.Padding = new(6, 4);
            Item.BackgroundBrush = GetTheme().ListBoxItemBackground.GetValue(true);
        }

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
                //  Create the outer border
                OuterBorder = new(ParentWindow, 0, SolidFillBrushes.Black);
                OuterBorderComponent = MGComponentBase.Create(OuterBorder);
                AddComponent(OuterBorderComponent);
                OuterBorder.OnBorderBrushChanged += (sender, e) => { NPC(nameof(OuterBorderBrush)); };
                OuterBorder.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(OuterBorderThickness)); };

                //  Create the title bar
                TitleBorder = new(ParentWindow);
                TitleBorder.Padding = new(6, 3);
                TitleBorder.BackgroundBrush = GetTheme().TitleBackground.GetValue(true);
                TitleBorder.DefaultTextForeground.SetAll(Color.White);
                TitleBorder.OnBorderBrushChanged += (sender, e) => { NPC(nameof(TitleBorderBrush)); };
                TitleBorder.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(TitleBorderThickness)); };
                TitlePresenter = new(ParentWindow);
                TitlePresenter.VerticalAlignment = VerticalAlignment.Center;
                TitleBorder.SetContent(TitlePresenter);
                TitleBorder.CanChangeContent = false;
                TitlePresenter.CanChangeContent = false;
                TitleComponent = new(TitleBorder, true, false, false, true, false, false, false,
                    (AvailableBounds, ComponentSize) => ApplyAlignment(AvailableBounds, HorizontalAlignment.Stretch, VerticalAlignment.Top, ComponentSize.Size));
                AddComponent(TitleComponent);

                //  Create the inner border
                InnerBorder = new(ParentWindow);
                InnerBorderComponent = new(InnerBorder, true, false, true, true, false, false, false,
                    (AvailableBounds, ComponentSize) => ApplyAlignment(AvailableBounds, HorizontalAlignment.Stretch, VerticalAlignment.Stretch, ComponentSize.Size));
                AddComponent(InnerBorderComponent);
                InnerBorder.OnBorderBrushChanged += (sender, e) => { NPC(nameof(InnerBorderBrush)); };
                InnerBorder.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(InnerBorderThickness)); };

                //  Create the scrollviewer and itemspanel
                ItemsPanel = new(ParentWindow, Orientation.Vertical);
                ItemsPanel.VerticalAlignment = VerticalAlignment.Top;
                ItemsPanel.CanChangeContent = false;
                ScrollViewer = new(ParentWindow);
                ScrollViewer.Padding = new(0, 0);
                ScrollViewer.SetContent(ItemsPanel);
                ScrollViewer.CanChangeContent = false;
                InnerBorder.SetContent(ScrollViewer);
                InnerBorder.CanChangeContent = false;

                SetTitleAndContentBorder(SolidFillBrushes.Black, 1);
                
                MinHeight = 30;

                AlternatingRowBackgrounds = GetTheme().ListBoxItemAlternatingRowBackgrounds.Select(x => x.GetValue(true)).ToList().AsReadOnly();

                ItemsPanel.BorderThickness = DefaultItemBorderThickness;
                ItemsPanel.BorderBrush = DefaultItemBorderBrush;

                ItemContainerStyle = ApplyDefaultItemContainerStyle;
                ItemTemplate = (item) => new MGTextBlock(ParentWindow, item.ToString()) { Padding = new(1,0) };

                SelectedItems = new List<MGListBoxItem<TItemType>>().AsReadOnly();
                SelectionMode = ListBoxSelectionMode.Single;
                CanDeselectByClickingSelectedItem = true;

                GetDesktop().Renderer.Host.EndUpdate += (sender, e) =>
                {
                    //  Reset PressedItem to null at the end of an update tick, rather than immediately when the mouse button is released,
                    //  because other input handlers with lower priority still need a chance to read the data before it is modified.
                    if (IsPressedItemInvalidationPending)
                    {
                        IsPressedItemInvalidationPending = false;
                        if (PressedItem != null)
                            PressedItem.ContentPresenter.SpoofIsPressedWhileDrawingBackground = false;
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
                        PressedItem.ContentPresenter.SpoofIsPressedWhileDrawingBackground = false;
                    PressedItem = GetItemAtMousePosition(e.Position);
                    if (PressedItem != null)
                        PressedItem.ContentPresenter.SpoofIsPressedWhileDrawingBackground = true;
                };

                MouseHandler.ReleasedOutside += (sender, e) =>
                {
                    if (e.IsLMB)
                        IsPressedItemInvalidationPending = true;
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
                            if (!IsShiftDown || SelectionSourceItem == null || !InternalItems.Contains(SelectionSourceItem))
                            {
                                SelectSingle();
                            }
                            else
                            {
                                int SourceIndex = InternalItems.IndexOf(SelectionSourceItem);
                                int PressedIndex = InternalItems.IndexOf(ReleasedItem);

                                int StartIndex = Math.Min(SourceIndex, PressedIndex);
                                int EndIndex = Math.Max(SourceIndex, PressedIndex);

                                SelectedItems = InternalItems.Skip(StartIndex).Take(EndIndex - StartIndex + 1).ToList().AsReadOnly();
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
                                        SelectedItems = SelectedItems.Where(x => x != ReleasedItem).ToList().AsReadOnly();
                                    else
                                        SelectedItems = SelectedItems.Append(ReleasedItem).ToList().AsReadOnly();
                                }
                                else
                                    SelectContiguous();
                                break;
                            default: throw new NotImplementedException($"Unrecognized {nameof(ListBoxSelectionMode)}: {nameof(SelectionMode)}");
                        }
                    }
                };
            }
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
                Header = Settings.Header.ToElement<MGElement>(SelfOrParentWindow, this);

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
                CanDeselectByClickingSelectedItem = Settings.CanDeselectByClickingSelectedItem.Value;
            if (Settings.SelectionMode.HasValue)
                SelectionMode = Settings.SelectionMode.Value;

            if (Settings.SelectedValue is TItemType SelectedT)
                SelectedValue = SelectedT;

            if (Settings.AlternatingRowBackgrounds != null && Settings.AlternatingRowBackgrounds.Any())
                AlternatingRowBackgrounds = Settings.AlternatingRowBackgrounds.Select(x => x.ToFillBrush(Desktop, this)).ToList().AsReadOnly();
            else
                AlternatingRowBackgrounds = new List<IFillBrush>().AsReadOnly();

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
        public TItemType Data { get; }

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
    }
}
