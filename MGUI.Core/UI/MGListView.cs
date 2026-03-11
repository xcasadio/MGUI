using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Input.Keyboard;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MGUI.Shared.Helpers;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MonoGame.Extended;
using Thickness = MonoGame.Extended.Thickness;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using RowDefinition = MGUI.Core.UI.Containers.Grids.RowDefinition;
using ColumnDefinition = MGUI.Core.UI.Containers.Grids.ColumnDefinition;

namespace MGUI.Core.UI
{
    /// <typeparam name="TItemType">The type that the ItemsSource will be bound to.</typeparam>
    public class MGListView<TItemType> : MGSingleContentHost, INavigationTargetVisibilityHandler
    {
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

        #region Items Source
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ObservableCollection<MGListViewItem<TItemType>> _InternalRowItems;
        private ObservableCollection<MGListViewItem<TItemType>> InternalRowItems
        {
            get => _InternalRowItems;
            set
            {
                if (_InternalRowItems != value)
                {
                    if (InternalRowItems != null)
                    {
                        // Collect and clean up all templated content from existing rows before clearing
                        IEnumerable<MGElement> OldElements = InternalRowItems
                            .SelectMany(x => x.GetRowContents().Values);
                        HandleTemplatedContentRemoved(OldElements);
                        InternalRowItems.CollectionChanged -= RowItems_CollectionChanged;  // Fix: was incorrectly += causing duplicate subscriptions
                    }
                    _InternalRowItems = value;
                    if (InternalRowItems != null)
                    {
                        InternalRowItems.CollectionChanged += RowItems_CollectionChanged;
                    }

                    foreach (MGListViewColumn<TItemType> LVIColumn in _Columns)
                    {
                        LVIColumn.RefreshColumnContent();
                    }
                }
            }
        }
        public IReadOnlyList<MGListViewItem<TItemType>> RowItems => InternalRowItems;

        // Cleans up DataBindings on elements that were auto-created via a column's CellTemplate and are
        // no longer in use, preventing memory leaks from old bindings subscribed to PropertyChanged.
        internal void HandleTemplatedContentRemoved(IEnumerable<MGElement> Items)
        {
            if (Items != null)
            {
                int count = 0;
                foreach (MGElement Item in Items)
                {
                    Item.RemoveDataBindings(true);
                    count++;
                }
                if (count > 0)
                {
                    Debug.WriteLine($"[MGListView] Cleaned up {count} DataBindings");
                }
            }
        }

        private void RowItems_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            using (DataGrid.AllowChangingContentTemporarily())
            {
                if (e.Action is NotifyCollectionChangedAction.Reset)
                {
                    // Clean up all templated content before clearing
                    if (InternalRowItems != null)
                    {
                        IEnumerable<MGElement> OldElements = InternalRowItems
                            .SelectMany(x => x.GetRowContents().Values);
                        HandleTemplatedContentRemoved(OldElements);
                    }
                    _ = DataGrid.TryRemoveAll();
                }
                else if (e.Action is NotifyCollectionChangedAction.Add && e.NewItems != null)
                {
                    foreach (MGListViewItem<TItemType> Item in e.NewItems)
                    {
                        Item.RefreshRowContent();
                    }
                }
                else if (e.Action is NotifyCollectionChangedAction.Remove && e.OldItems != null)
                {
                    foreach (MGListViewItem<TItemType> Item in e.OldItems)
                    {
                        // Clean up templated content before removing the row
                        HandleTemplatedContentRemoved(Item.GetRowContents().Values);
                        DataGrid.RemoveRow(Item.DataRow);
                    }
                }
                else if (e.Action is NotifyCollectionChangedAction.Replace)
                {
                    List<MGListViewItem<TItemType>> Old = e.OldItems.Cast<MGListViewItem<TItemType>>().ToList();
                    List<MGListViewItem<TItemType>> New = e.NewItems.Cast<MGListViewItem<TItemType>>().ToList();
                    for (int i = 0; i < Old.Count; i++)
                    {
                        // Clean up old templated content before replacing
                        HandleTemplatedContentRemoved(Old[i].GetRowContents().Values);
                        New[i].RefreshRowContent();
                    }
                }
                else if (e.Action is NotifyCollectionChangedAction.Move)
                {
                    throw new NotImplementedException();
                }
            }
        }

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
                    {
                        ItemsSource.CollectionChanged -= ItemsSource_CollectionChanged;
                    }

                    _ItemsSource = value;
                    if (ItemsSource != null)
                    {
                        ItemsSource.CollectionChanged += ItemsSource_CollectionChanged;
                    }

                    using (DataGrid.AllowChangingContentTemporarily())
                    {
                        _ = DataGrid.TryRemoveAll();

                        //  Make exactly 1 row per item in the collection
                        int RowCount = ItemsSource?.Count ?? 0;
                        while (DataGrid.Rows.Count < RowCount)
                            DataGrid.AddRow(RowLength);
                        while (DataGrid.Rows.Count > RowCount)
                            DataGrid.RemoveRow(DataGrid.Rows[^1]);
                    }

                    if (ItemsSource == null)
                    {
                        InternalRowItems = null;
                    }
                    else
                    {
                        IEnumerable<MGListViewItem<TItemType>> Values = ItemsSource.Select((x, Index) => new MGListViewItem<TItemType>(this, x, DataGrid.Rows[Index]));
                        InternalRowItems = new ObservableCollection<MGListViewItem<TItemType>>(Values);
                    }

                    NPC(nameof(ItemsSource));
                }
            }
        }

        private void ItemsSource_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action is NotifyCollectionChangedAction.Reset)
            {
                InternalRowItems.Clear();
            }
            else if (e.Action is NotifyCollectionChangedAction.Add && e.NewItems != null)
            {
                int CurrentIndex = e.NewStartingIndex;
                foreach (TItemType Item in e.NewItems)
                {
                    RowDefinition NewRow = DataGrid.InsertRow(CurrentIndex, RowLength);
                    MGListViewItem<TItemType> NewRowItem = new(this, Item, NewRow);
                    InternalRowItems.Insert(CurrentIndex, NewRowItem);
                    CurrentIndex++;
                }
            }
            else if (e.Action is NotifyCollectionChangedAction.Remove && e.OldItems != null)
            {
                int CurrentIndex = e.OldStartingIndex;
                foreach (TItemType Item in e.OldItems)
                {
                    InternalRowItems.RemoveAt(CurrentIndex);
                }
            }
            else if (e.Action is NotifyCollectionChangedAction.Replace)
            {
                List<TItemType> Old = e.OldItems.Cast<TItemType>().ToList();
                List<TItemType> New = e.NewItems.Cast<TItemType>().ToList();
                for (int i = 0; i < Old.Count; i++)
                {
                    MGListViewItem<TItemType> OldRowItem = InternalRowItems[i];
                    MGListViewItem<TItemType> NewRowItem = new(this, New[i], OldRowItem.DataRow);
                    InternalRowItems[e.OldStartingIndex + i] = NewRowItem;
                }
            }
            else if (e.Action is NotifyCollectionChangedAction.Move)
            {
                throw new NotImplementedException();
            }
        }

        /// <param name="Value"><see cref="ItemsSource"/> will be set to a copy of this <see cref="ICollection{T}"/> unless the collection is an <see cref="ObservableCollection{T}"/>.<br/>
        /// If you want <see cref="ItemsSource"/> to dynamically update as the collection changes, pass in an <see cref="ObservableCollection{T}"/></param>
        public void SetItemsSource(ICollection<TItemType> Value)
        {
            if (Value is ObservableCollection<TItemType> Observable)
            {
                ItemsSource = Observable;
            }
            else
            {
                ItemsSource = new ObservableCollection<TItemType>(Value.ToList());
            }
        }
        #endregion Items Source

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly List<MGListViewColumn<TItemType>> _Columns;
        public IReadOnlyList<MGListViewColumn<TItemType>> Columns => _Columns;

        public MGListViewColumn<TItemType> AddColumn(ListViewColumnWidth Width, MGElement Header, Func<TItemType, MGElement> ItemTemplate)
        {
            MGListViewColumn<TItemType> Column = new(this, Width, Header, ItemTemplate);
            _Columns.Add(Column);
            return Column;
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int? _RowHeight;
        /// <summary>The height, in pixels, of each row in <see cref="DataGrid"/>. Does not affect the height of the header row.<para/>
        /// If null, row heights will be dynamically sized to their content, which may incur significant performance hit for large collections.<para/>
        /// Default value: null</summary>
        public int? RowHeight
        {
            get => _RowHeight;
            set
            {
                if (_RowHeight != value)
                {
                    _RowHeight = value;
                    RowLength = RowHeight.HasValue ? GridLength.CreatePixelLength(RowHeight.Value) : GridLength.Auto;
                    NPC(nameof(RowHeight));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private GridLength _RowLength;
        internal GridLength RowLength
        {
            get => _RowLength;
            set
            {
                if (_RowLength != value)
                {
                    _RowLength = value;

                    if (DataGrid != null)
                    {
                        foreach (RowDefinition Row in DataGrid.Rows)
                        {
                            Row.Length = RowLength;
                        }
                    }

                    NPC(nameof(RowLength));
                }
            }
        }

        public MGDockPanel DockPanelElement { get; }
        /// <summary>The <see cref="MGGrid"/> that contains the column headers</summary>
        public MGGrid HeaderGrid { get; }
        public MGScrollViewer ScrollViewer { get; }
        /// <summary>The <see cref="MGGrid"/> that contains the rows of data, based on <see cref="ItemsSource"/></summary>
        public MGGrid DataGrid { get; }

        /// <summary>Default value: <see cref="GridSelectionMode.None"/></summary>
        public GridSelectionMode SelectionMode
        {
            get => DataGrid.SelectionMode;
            set
            {
                if (SelectionMode != value)
                {
                    DataGrid.SelectionMode = value;
                    NPC(nameof(SelectionMode));
                }
            }
        }

        /// <summary>To get the underlying <see cref="MGElement"/>s in the selection,<br/>
        /// enumerate <see cref="GridSelection"/>, and for each <see cref="GridCell"/>, call <see cref="GridSelection.Grid"/>'s <see cref="MGGrid.GetCellContent(GridCell)"/></summary>
        public GridSelection? SelectedData => DataGrid.CurrentSelection;

        public event EventHandler<GridSelection?> SelectionChanged;

        public MGListView(MGWindow Window)
            : this(Window, 8, 3) { }

        public MGListView(MGWindow Window, int Spacing, int GridLineMargin)
            : base(Window, MGElementType.ListView)
        {
            using (BeginInitializing())
            {
                IFillBrush GridLineBrush = SolidFillBrushes.Black;

                HeaderGrid = new(Window);
                HeaderGrid.AddRow(GridLength.Auto);
                HeaderGrid.GridLinesVisibility = GridLinesVisibility.All;
                HeaderGrid.RowSpacing = Spacing;
                HeaderGrid.ColumnSpacing = Spacing;
                HeaderGrid.GridLineMargin = GridLineMargin;
                HeaderGrid.HorizontalGridLineBrush = GridLineBrush;
                HeaderGrid.VerticalGridLineBrush = GridLineBrush;
                HeaderGrid.BackgroundBrush = GetTheme().TitleBackground.GetValue(true);
                HeaderGrid.DefaultTextForeground.SetAll(Color.White);
                HeaderGrid.CanChangeContent = false;

                DataGrid = new(Window);
                DataGrid.GridLinesVisibility = GridLinesVisibility.AllVertical | GridLinesVisibility.InnerHorizontal | GridLinesVisibility.BottomEdge; // Don't draw TopEdge gridline since the header grid already has a BottomEdge gridline
                DataGrid.Padding = new(0, GridLineMargin, 0, 0); // Normally the top would already be padded if we were drawing a TopEdge gridline. But since we're not, manually pad it
                DataGrid.RowSpacing = Spacing;
                DataGrid.ColumnSpacing = Spacing;
                DataGrid.GridLineMargin = GridLineMargin;
                DataGrid.HorizontalGridLineBrush = GridLineBrush;
                DataGrid.VerticalGridLineBrush = GridLineBrush;
                DataGrid.CanChangeContent = false;

                DataGrid.SelectionChanged += (sender, e) => { SelectionChanged?.Invoke(this, e); };

                //  Create a content-less element that will be placed to the right of the HeaderGrid, whose width will always match the width of the vertical scrollbar.
                //  If the DataGrid needs to reserve width on the right edge for a vertical scrollbar, this width will also be reserved in the HeaderGrid
                int TopRightCornerBorderThickness = Math.Max(0, Spacing - GridLineMargin * 2);
                MGBorder TopRightCornerPlaceholder = new(Window, new Thickness(0, TopRightCornerBorderThickness, TopRightCornerBorderThickness, TopRightCornerBorderThickness), MGUniformBorderBrush.Black); //null as IBorderBrush);
                TopRightCornerPlaceholder.BackgroundBrush = HeaderGrid.BackgroundBrush;

                ScrollViewer = new(Window, ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled);
                ScrollViewer.SetContent(DataGrid);
                ScrollViewer.CanChangeContent = false;
                ScrollViewer.VerticalScrollBarBoundsChanged += (sender, e) => {
                    TopRightCornerPlaceholder.PreferredWidth = e?.Width ?? 0;
                };

                MGDockPanel HeaderGridWrapper = new(Window);
                HeaderGridWrapper.TryAddChild(TopRightCornerPlaceholder, Dock.Right);
                HeaderGridWrapper.TryAddChild(HeaderGrid, Dock.Left);
                HeaderGridWrapper.CanChangeContent = false;

                DockPanelElement = new(Window);
                DockPanelElement.TryAddChild(HeaderGridWrapper, Dock.Top);
                DockPanelElement.TryAddChild(ScrollViewer, Dock.Bottom);
                //DockPanelElement.VerticalAlignment = VerticalAlignment.Top;
                DockPanelElement.CanChangeContent = false;
                SetContent(DockPanelElement);
                CanChangeContent = false;

                HeaderGrid.ManagedParent = this;
                DataGrid.ManagedParent = this;
                ScrollViewer.ManagedParent = this;
                DockPanelElement.ManagedParent = this;

                VerticalAlignment = VerticalAlignment.Top;

                RowHeight = null;
                RowLength = GridLength.Auto;

                _Columns = new();

                SelectionMode = GridSelectionMode.None;

                DataGrid.SelectionChanged += (sender, e) =>
                {
                    if (e.HasValue && DataGrid.Rows.Count > 0)
                    {
                        FocusedRowIndex = DataGrid.GetRowIndex(e.Value.Cell.Row);
                    }
                };

                IsFocusable = true;
                KeyboardHandler.Pressed += OnListViewKeyPressed;
            }
        }

        #region FocusedRowIndex
        private int _FocusedRowIndex = -1;
        /// <summary>The index of the keyboard-focused row, or -1 if none.</summary>
        public int FocusedRowIndex
        {
            get => _FocusedRowIndex;
            set
            {
                int count = RowItems?.Count ?? 0;
                int clamped = count == 0 ? -1 : Math.Clamp(value, 0, count - 1);
                if (_FocusedRowIndex != clamped) { _FocusedRowIndex = clamped; NPC(nameof(FocusedRowIndex)); }
            }
        }
        #endregion FocusedRowIndex

        private void EnsureFocusedRowVisible()
        {
            if (FocusedRowIndex < 0 || ScrollViewer == null || RowItems == null || FocusedRowIndex >= RowItems.Count)
            {
                return;
            }

            MGElement firstVisibleCell = RowItems[FocusedRowIndex].GetRowContents().Values.FirstOrDefault();
            if (firstVisibleCell != null)
            {
                ScrollViewer.EnsureElementVisible(firstVisibleCell);
            }
        }

        void INavigationTargetVisibilityHandler.EnsureNavigationTargetVisible() => EnsureFocusedRowVisible();

        private void OnListViewKeyPressed(object sender, BaseKeyPressedEventArgs e)
        {
            int count = RowItems?.Count ?? 0;
            if (count == 0)
            {
                return;
            }

            int newIndex = FocusedRowIndex;
            switch (e.Key)
            {
                case Keys.Up:       newIndex = Math.Max(0, FocusedRowIndex <= 0 ? 0 : FocusedRowIndex - 1); break;
                case Keys.Down:     newIndex = Math.Min(count - 1, FocusedRowIndex < 0 ? 0 : FocusedRowIndex + 1); break;
                case Keys.Home:     newIndex = 0; break;
                case Keys.End:      newIndex = count - 1; break;
                case Keys.PageUp:   newIndex = Math.Max(0, FocusedRowIndex - 10); break;
                case Keys.PageDown: newIndex = Math.Min(count - 1, FocusedRowIndex + 10); break;
                default: return;
            }

            if (newIndex != FocusedRowIndex || FocusedRowIndex < 0)
            {
                FocusedRowIndex = newIndex;
                ((INavigationTargetVisibilityHandler)this).EnsureNavigationTargetVisible();
                if (SelectionMode != GridSelectionMode.None && DataGrid.Rows.Count > FocusedRowIndex && DataGrid.Columns.Count > 0)
                {
                    DataGrid.CurrentSelection = new GridSelection(DataGrid, new GridCell(DataGrid.Rows[FocusedRowIndex], DataGrid.Columns[0]), SelectionMode);
                }

                e.SetHandledBy(this, true);
            }
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            int count = RowItems?.Count ?? 0;
            if (count == 0)
            {
                return false;
            }

            if (action is not (UINavigationAction.MoveUp or UINavigationAction.MoveDown or UINavigationAction.Home or UINavigationAction.End or UINavigationAction.PageUp or UINavigationAction.PageDown))
            {
                return false;
            }

            int nextIndex = GetNextNavigationIndex(FocusedRowIndex, count, action);
            if (nextIndex < 0)
            {
                return false;
            }

            FocusedRowIndex = nextIndex;
            if (SelectionMode != GridSelectionMode.None && DataGrid.Rows.Count > FocusedRowIndex && DataGrid.Columns.Count > 0)
            {
                DataGrid.CurrentSelection = new GridSelection(DataGrid, new GridCell(DataGrid.Rows[FocusedRowIndex], DataGrid.Columns[0]), SelectionMode);
            }

            return true;
        }

        public override void DrawBackground(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            base.DrawBackground(DA, HeaderGrid.LayoutBounds);
            base.DrawBackground(DA, DataGrid.LayoutBounds);
        }

        //  This method is invoked via reflection in MGUI.Core.UI.XAML.Lists.ListView.ApplyDerivedSettings.
        //  Do not modify the method signature.
        internal void LoadSettings(ListView Settings, bool IncludeContent)
        {
            Settings.HeaderGrid.ApplySettings(this, HeaderGrid, false);
            Settings.ScrollViewer.ApplySettings(this, ScrollViewer, false);
            Settings.DataGrid.ApplySettings(this, DataGrid, false);

            foreach (ListViewColumn ColumnDefinition in Settings.Columns)
            {
                ListViewColumnWidth Width = ColumnDefinition.Width.ToWidth();
                MGElement Header = ColumnDefinition.Header.ToElement<MGElement>(SelfOrParentWindow, this);

                Func<TItemType, MGElement> CellTemplate;
                if (ColumnDefinition.CellTemplate != null)
                {
                    CellTemplate = (Item) => ColumnDefinition.CellTemplate.GetContent(SelfOrParentWindow, this, Item);
                }
                else
                {
                    CellTemplate = (Item) => new MGTextBlock(SelfOrParentWindow, Item.ToString());
                }

                AddColumn(Width, Header, CellTemplate);
            }

            if (Settings.RowHeight.HasValue)
            {
                RowHeight = Settings.RowHeight.Value;
            }

            if (Settings.SelectionMode.HasValue)
            {
                SelectionMode = Settings.SelectionMode.Value;
            }
        }

        #region Column Sort
        /// <summary>Event arguments for <see cref="ColumnSortChanged"/>.</summary>
        public class ColumnSortChangedEventArgs : EventArgs
        {
            public MGListViewColumn<TItemType> Column { get; }
            public SortDirection? Direction { get; }
            public ColumnSortChangedEventArgs(MGListViewColumn<TItemType> column, SortDirection? direction)
            { Column = column; Direction = direction; }
        }

        /// <summary>Raised whenever the sort column or direction changes (including when sort is cleared).</summary>
        public event EventHandler<ColumnSortChangedEventArgs> ColumnSortChanged;

        /// <summary>The column that is currently used for sorting, or null if no sort is active.</summary>
        public MGListViewColumn<TItemType> ActiveSortColumn { get; private set; }

        private List<TItemType> _presortItems;

        /// <summary>Sort rows by the given column in the given direction.
        /// The column must have <see cref="MGListViewColumn{TItemType}.SortKeySelector"/> set.</summary>
        public void SortByColumn(MGListViewColumn<TItemType> column, SortDirection direction)
        {
            if (column == null)
            {
                throw new ArgumentNullException(nameof(column));
            }

            if (column.SortKeySelector == null)
            {
                return;
            }

            // Save original order on first sort
            if (_presortItems == null && ItemsSource != null)
            {
                _presortItems = ItemsSource.ToList();
            }

            // Update sort state
            if (ActiveSortColumn != null && ActiveSortColumn != column)
            {
                ActiveSortColumn.SetSortDirection(null);
                ActiveSortColumn.UpdateSortIndicator();
            }
            ActiveSortColumn = column;
            column.SetSortDirection(direction);
            column.UpdateSortIndicator();

            ApplySort();
            ColumnSortChanged?.Invoke(this, new ColumnSortChangedEventArgs(column, direction));
        }

        /// <summary>Removes the active column sort and restores the original item order.</summary>
        public void ClearSort()
        {
            if (ActiveSortColumn == null)
            {
                return;
            }

            ActiveSortColumn.SetSortDirection(null);
            ActiveSortColumn.UpdateSortIndicator();
            ActiveSortColumn = null;

            if (_presortItems != null)
            {
                ReorderRows(_presortItems);
                _presortItems = null;
            }
            ColumnSortChanged?.Invoke(this, new ColumnSortChangedEventArgs(null, null));
        }

        private void ApplySort()
        {
            if (ActiveSortColumn?.SortKeySelector == null || ItemsSource == null)
            {
                return;
            }

            var sorted = ActiveSortColumn.CurrentSortDirection == SortDirection.Ascending
                ? ItemsSource.OrderBy(d => ActiveSortColumn.SortKeySelector(d))
                : ItemsSource.OrderByDescending(d => ActiveSortColumn.SortKeySelector(d));
            ReorderRows(sorted);
        }

        private void ReorderRows(IEnumerable<TItemType> orderedItems)
        {
            SetItemsSource(orderedItems.ToList());
        }
        #endregion Column Sort
    }
    
    public class ListViewColumnWidth : ViewModelBase
    {
        public GridLength Length { get; private set; }
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsAbsoluteWidth => Length.IsPixelLength;
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public bool IsWeightedWidth => Length.IsWeightedLength;

        /// <summary>Getter is only valid if <see cref="IsAbsoluteWidth"/> is true</summary>
        public int WidthPixels
        {
            get => Length.Pixels;
            set
            {
                Length = GridLength.CreatePixelLength(value);
                WidthChanged?.Invoke(this, EventArgs.Empty);
                NPC(nameof(WidthPixels));
                NPC(nameof(WidthWeight));
                NPC(nameof(Length));
            }
        }

        /// <summary>Getter is only valid if <see cref="IsWeightedWidth"/> is true</summary>
        public double WidthWeight
        {
            get => Length.Weight;
            set
            {
                Length = GridLength.CreateWeightedLength(value);
                WidthChanged?.Invoke(this, EventArgs.Empty);
                NPC(nameof(WidthWeight));
                NPC(nameof(WidthPixels));
                NPC(nameof(Length));
            }
        }

        public event EventHandler<EventArgs> WidthChanged;

        public ListViewColumnWidth(int WidthPixels)
        {
            this.WidthPixels = WidthPixels;
        }

        public ListViewColumnWidth(double WidthWeight)
        {
            this.WidthWeight = WidthWeight;
        }
    }

    public class MGListViewItem<TItemType>
    {
        public MGListView<TItemType> ListView { get; }

        public MGGrid DataGrid => ListView.DataGrid;
        public RowDefinition DataRow { get; }

        /// <summary>The data object used as a parameter to generate the content of each cell in the <see cref="DataRow"/>.<para/>
        /// See also: <see cref="MGListViewColumn{TItemType}.CellTemplate"/></summary>
        public TItemType Data { get; }

        public Dictionary<MGListViewColumn<TItemType>, MGElement> GetRowContents()
        {
            Dictionary<MGListViewColumn<TItemType>, MGElement> Cells = new();
            IReadOnlyDictionary<ColumnDefinition, IReadOnlyList<MGElement>> RowContentByColumn = DataGrid.GetRowContent(DataRow);
            foreach (MGListViewColumn<TItemType> Column in ListView.Columns)
            {
                if (RowContentByColumn.TryGetValue(Column.DataColumn, out var Elements) && Elements.Count > 0)
                {
                    //  Sanity check
                    if (Elements.Count > 1)
                    {
                        throw new InvalidOperationException($"Each cell in a ListView's internal Grid should have at most 1 child element. " +
                            $"The cell at RowIndex={DataRow.Index},ColumnIndex={Column.DataColumn.Index} contains {Elements.Count} children.");
                    }
                    Cells.Add(Column, Elements.First());
                }
            }
            return Cells;
        }

        internal MGListViewItem(MGListView<TItemType> ListView, TItemType Data, RowDefinition Row)
        {
            this.ListView = ListView ?? throw new ArgumentNullException(nameof(ListView));
            this.Data = Data ?? throw new ArgumentNullException(nameof(Data));
            DataRow = Row ?? throw new ArgumentNullException(nameof(Row));
        }

        internal void RefreshRowContent()
        {
            using (DataGrid.AllowChangingContentTemporarily())
            {
                // Clean up existing templated content before regenerating the row
                ListView.HandleTemplatedContentRemoved(GetRowContents().Values);
                DataGrid.ClearRowContent(DataRow);
                foreach (MGListViewColumn<TItemType> Column in ListView.Columns)
                {
                    if (Column.CellTemplate != null)
                    {
                        MGElement CellContent = Column.CellTemplate(Data);
                        DataGrid.TryAddChild(DataRow, Column.DataColumn, CellContent);
                    }
                }
            }
        }
    }

    public class MGListViewColumn<TItemType> : ViewModelBase
    {
        public MGListView<TItemType> ListView { get; }
        public ListViewColumnWidth Width { get; }

        public MGGrid HeaderGrid => ListView.HeaderGrid;
        public ColumnDefinition HeaderColumn { get; }
        public MGGrid DataGrid => ListView.DataGrid;
        public ColumnDefinition DataColumn { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private MGElement _Header;
        /// <summary>The content to display in the header row of the list view.</summary>
        public MGElement Header
        {
            get => _Header;
            set
            {
                if (_Header != value)
                {
                    // Unsubscribe from old header's sort click
                    if (_Header != null && _headerSortSubscribed)
                    {
                        _Header.MouseHandler.LMBClickedInside -= Header_SortClicked;
                        _headerSortSubscribed = false;
                    }

                    _Header = value;

                    RowDefinition HeaderRow = HeaderGrid.Rows[0];
                    using (HeaderGrid.AllowChangingContentTemporarily())
                    {
                        HeaderGrid.ClearCellContent(HeaderRow, HeaderColumn);
                        if (Header != null)
                        {
                            HeaderGrid.TryAddChild(HeaderRow, HeaderColumn, Header);
                        }
                    }

                    // Re-subscribe to new header if sortable
                    RefreshSortClickSubscription();

                    NPC(nameof(Header));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Func<TItemType, MGElement> _CellTemplate;
        /// <summary>This function is invoked to instantiate the content of each cell in this column</summary>
        public Func<TItemType, MGElement> CellTemplate
        {
            get => _CellTemplate;
            set
            {
                if (_CellTemplate != value)
                {
                    _CellTemplate = value;
                    RefreshColumnContent();
                    NPC(nameof(CellTemplate));
                }
            }
        }

        internal void RefreshColumnContent()
        {
            using (DataGrid.AllowChangingContentTemporarily())
            {
                // Clean up existing templated content in this column before regenerating it
                if (ListView.RowItems != null)
                {
                    IEnumerable<MGElement> OldElements = ListView.RowItems
                        .Select(item => item.GetRowContents())
                        .Where(contents => contents.ContainsKey(this))
                        .Select(contents => contents[this]);
                    ListView.HandleTemplatedContentRemoved(OldElements);
                }
                DataGrid.ClearColumnContent(DataColumn);
                if (ListView.RowItems != null && CellTemplate != null)
                {
                    for (int i = 0; i < ListView.RowItems.Count; i++)
                    {
                        MGListViewItem<TItemType> RowItem = ListView.RowItems[i];
                        MGElement CellContent = CellTemplate(RowItem.Data);
                        DataGrid.TryAddChild(RowItem.DataRow, DataColumn, CellContent);
                    }
                }
            }
        }

        internal MGListViewColumn(MGListView<TItemType> ListView, ListViewColumnWidth Width, MGElement Header, Func<TItemType, MGElement> ItemTemplate)
        {
            this.ListView = ListView;
            this.Width = Width;

            Width.WidthChanged += (sender, e) =>
            {
                HeaderColumn.Length = Width.Length;
                DataColumn.Length = Width.Length;
            };

            HeaderColumn = HeaderGrid.AddColumn(Width.Length);
            DataColumn = DataGrid.AddColumn(Width.Length);

            this.Header = Header;

            CellTemplate = ItemTemplate;
        }

        #region Column Sort
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsSortable;
        /// <summary>When true, clicking the column header will sort the list view by this column.
        /// Requires <see cref="SortKeySelector"/> to be set.</summary>
        public bool IsSortable
        {
            get => _IsSortable;
            set
            {
                if (_IsSortable != value)
                {
                    _IsSortable = value;
                    RefreshSortClickSubscription();
                    NPC(nameof(IsSortable));
                }
            }
        }

        /// <summary>A function that extracts a comparable key from an item, used for sorting.</summary>
        public Func<TItemType, IComparable> SortKeySelector { get; set; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private SortDirection? _CurrentSortDirection;
        /// <summary>The active sort direction for this column, or null if this column is not the active sort column.</summary>
        public SortDirection? CurrentSortDirection
        {
            get => _CurrentSortDirection;
            private set { if (_CurrentSortDirection != value) { _CurrentSortDirection = value; NPC(nameof(CurrentSortDirection)); } }
        }

        internal void SetSortDirection(SortDirection? direction) => CurrentSortDirection = direction;

        private bool _headerSortSubscribed;

        private void RefreshSortClickSubscription()
        {
            if (_Header != null)
            {
                if (IsSortable && !_headerSortSubscribed)
                {
                    _Header.MouseHandler.LMBClickedInside += Header_SortClicked;
                    _headerSortSubscribed = true;
                }
                else if (!IsSortable && _headerSortSubscribed)
                {
                    _Header.MouseHandler.LMBClickedInside -= Header_SortClicked;
                    _headerSortSubscribed = false;
                }
            }
        }

        private void Header_SortClicked(object sender, MGUI.Shared.Input.Mouse.BaseMouseClickedEventArgs e)
        {
            if (!IsSortable || SortKeySelector == null)
            {
                return;
            }

            SortDirection newDir = CurrentSortDirection == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
            ListView.SortByColumn(this, newDir);
        }

        /// <summary>Override to customize the sort indicator displayed on the column header.
        /// By default updates the header's <see cref="MGElement.ToolTip"/> text to show sort direction.</summary>
        public virtual void UpdateSortIndicator()
        {
            // Base implementation is intentionally empty.
            // Consumers may override MGListViewColumn or subscribe to ListView.ColumnSortChanged to update visuals.
        }
        #endregion Column Sort
    }
}
