using MGUI.Core.UI.Containers.Grids;
using Microsoft.Xna.Framework;
using System;
using System.Linq;
using MonoGame.Extended;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI
{
    public sealed class MGDataGridSelectionChangedEventArgs<TItemType> : EventArgs
    {
        public bool HasSelection { get; }
        public int SelectedRowIndex { get; }
        public TItemType SelectedItem { get; }

        internal MGDataGridSelectionChangedEventArgs(bool hasSelection, int selectedRowIndex, TItemType selectedItem)
        {
            HasSelection = hasSelection;
            SelectedRowIndex = selectedRowIndex;
            SelectedItem = selectedItem;
        }
    }

    public class MGDataGrid<TItemType> : MGListView<TItemType>
    {
        public int SelectedRowIndex => SelectedData.HasValue ? DataGrid.GetRowIndex(SelectedData.Value.Cell.Row) : -1;
        public TItemType SelectedItem => TryGetSelectedItem(out TItemType item) ? item : default!;

        public event EventHandler<MGDataGridSelectionChangedEventArgs<TItemType>> SelectedItemChanged;

        public MGDataGrid(MGWindow window)
            : base(window)
        {
            SelectionMode = GridSelectionMode.Row;
            RowHeight = 26;
            SelectionChanged += HandleSelectionChanged;
        }

        public MGListViewColumn<TItemType> AddTextColumn(ListViewColumnWidth width, string headerText,
            Func<TItemType, object> valueSelector, Func<TItemType, IComparable> sortKeySelector = null,
            HorizontalAlignment textAlignment = HorizontalAlignment.Left, Func<TItemType, Color?> foregroundSelector = null)
        {
            if (valueSelector == null)
            {
                throw new ArgumentNullException(nameof(valueSelector));
            }

            return AddTemplateColumn(width, headerText,
                item =>
                {
                    MGTextBlock textBlock = new(SelfOrParentWindow, valueSelector(item)?.ToString() ?? string.Empty, foregroundSelector?.Invoke(item))
                    {
                        HorizontalAlignment = HorizontalAlignment.Stretch,
                        TextAlignment = textAlignment,
                    };
                    textBlock.SetMargin(new Thickness(4, 0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                    return textBlock;
                },
                sortKeySelector,
                headerTextAlignment: textAlignment);
        }

        public MGListViewColumn<TItemType> AddTemplateColumn(ListViewColumnWidth width, string headerText,
            Func<TItemType, MGElement> cellTemplate, Func<TItemType, IComparable> sortKeySelector = null,
            HorizontalAlignment headerTextAlignment = HorizontalAlignment.Left)
        {
            if (width == null)
            {
                throw new ArgumentNullException(nameof(width));
            }
            if (cellTemplate == null)
            {
                throw new ArgumentNullException(nameof(cellTemplate));
            }

            MGTextBlock header = CreateHeaderTextBlock(headerText, headerTextAlignment);
            MGDataGridTextColumn<TItemType> column = new(this, width, header, cellTemplate);
            AddColumnCore(column);

            column.SortKeySelector = sortKeySelector;
            column.IsSortable = sortKeySelector != null;
            column.UpdateSortIndicator();
            return column;
        }

        public bool TryGetSelectedItem(out TItemType item)
        {
            int selectedRowIndex = SelectedRowIndex;
            if (selectedRowIndex >= 0 && RowItems != null && selectedRowIndex < RowItems.Count)
            {
                item = RowItems[selectedRowIndex].Data;
                return true;
            }

            item = default!;
            return false;
        }

        public bool SelectRow(int rowIndex, bool ensureVisible = true)
        {
            if (rowIndex < 0 || RowItems == null || rowIndex >= RowItems.Count || DataGrid.Rows.Count <= rowIndex || DataGrid.Columns.Count == 0)
            {
                return false;
            }

            SelectionMode = GridSelectionMode.Row;
            FocusedRowIndex = rowIndex;
            DataGrid.CurrentSelection = new GridSelection(DataGrid, new GridCell(DataGrid.Rows[rowIndex], DataGrid.Columns[0]), GridSelectionMode.Row);
            if (ensureVisible)
            {
                EnsureRowVisible(rowIndex);
            }

            return true;
        }

        public bool EnsureRowVisible(int rowIndex)
        {
            if (rowIndex < 0 || RowItems == null || rowIndex >= RowItems.Count || ScrollViewer == null)
            {
                return false;
            }

            MGElement firstVisibleCell = RowItems[rowIndex].GetRowContents().Values.FirstOrDefault();
            if (firstVisibleCell == null)
            {
                return false;
            }

            ScrollViewer.EnsureElementVisible(firstVisibleCell);
            return true;
        }

        public bool ScrollToItem(TItemType item)
        {
            if (RowItems == null)
            {
                return false;
            }

            int rowIndex = RowItems.Select((row, index) => new { row, index })
                .FirstOrDefault(x => Equals(x.row.Data, item))?.index ?? -1;
            return EnsureRowVisible(rowIndex);
        }

        public bool ResizeColumnPixels(int columnIndex, int widthPixels)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
            {
                return false;
            }

            Columns[columnIndex].Width.WidthPixels = widthPixels;
            return true;
        }

        public bool ResizeColumnWeight(int columnIndex, double widthWeight)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
            {
                return false;
            }

            Columns[columnIndex].Width.WidthWeight = widthWeight;
            return true;
        }

        public bool SortByColumnIndex(int columnIndex, SortDirection direction)
        {
            if (columnIndex < 0 || columnIndex >= Columns.Count)
            {
                return false;
            }

            SortByColumn(Columns[columnIndex], direction);
            return true;
        }

        private void HandleSelectionChanged(object sender, Containers.Grids.GridSelection? selection)
        {
            bool hasSelection = TryGetSelectedItem(out TItemType item);
            SelectedItemChanged?.Invoke(this, new MGDataGridSelectionChangedEventArgs<TItemType>(hasSelection, SelectedRowIndex, item));
        }

        private MGTextBlock CreateHeaderTextBlock(string headerText, HorizontalAlignment headerTextAlignment)
        {
            MGTextBlock headerTextBlock = new MGTextBlock(SelfOrParentWindow, headerText ?? string.Empty)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                TextAlignment = headerTextAlignment,
                IsBold = true,
            };
            headerTextBlock.SetMargin(new Thickness(4, 0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            return headerTextBlock;
        }
    }

    internal sealed class MGDataGridTextColumn<TItemType> : MGListViewColumn<TItemType>
    {
        private readonly string _BaseHeaderText;
        private readonly MGTextBlock _HeaderTextBlock;

        internal MGDataGridTextColumn(MGListView<TItemType> listView, ListViewColumnWidth width, MGTextBlock headerTextBlock,
            Func<TItemType, MGElement> itemTemplate)
            : base(listView, width, headerTextBlock, itemTemplate)
        {
            _BaseHeaderText = headerTextBlock?.Text ?? string.Empty;
            _HeaderTextBlock = headerTextBlock;
        }

        public override void UpdateSortIndicator()
        {
            string suffix = CurrentSortDirection switch
            {
                SortDirection.Ascending => " ▲",
                SortDirection.Descending => " ▼",
                _ => string.Empty,
            };
            _HeaderTextBlock.Text = _BaseHeaderText + suffix;
        }
    }
}