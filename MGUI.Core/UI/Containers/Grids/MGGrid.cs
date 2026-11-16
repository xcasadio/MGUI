using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Helpers;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Shared.Input.Mouse;

namespace MGUI.Core.UI.Containers.Grids;

[Flags]
public enum GridLinesVisibility : byte
{
    None = 0b_0000_0000,

    /// <summary>Indicates that horizontal gridlines should be rendered. Does not include horizontal gridlines at the top or bottom edge of the grid's border</summary>
    InnerHorizontal = 0b_0000_0001,
    /// <summary>Indicates that a horizontal gridline should be rendered at the top edge of the grid's border</summary>
    TopEdge = 0b_0000_0010,
    /// <summary>Indicates that a horizontal gridline should be rendered at the bottom edge of the grid's border</summary>
    BottomEdge = 0b_0000_0100,

    /// <summary>Indicates that vertical gridlines should be rendered. Does not include vertical gridlines at the left or right edge of the grid's border</summary>
    InnerVertical = 0b_0000_1000,
    /// <summary>Indicates that a vertical gridline should be rendered at the left edge of the grid's border</summary>
    LeftEdge = 0b_0001_0000,
    /// <summary>Indicates that a vertical gridline should be rendered at the right edge of the grid's border</summary>
    RightEdge = 0b_0010_0000,

    All = InnerHorizontal | TopEdge | BottomEdge | InnerVertical | LeftEdge | RightEdge,

    AllHorizontal = InnerHorizontal | TopEdge | BottomEdge,
    AllVertical = InnerVertical | LeftEdge | RightEdge,
}

public enum GridLineIntersection : byte
{
#if NEVER // Not Implemented
        /// <summary>Indicates that the intersections of the horizontal and vertical gridlines should not be filled with either gridline brush</summary>
        None,
        /// <summary>Indicates that the intersections of the horizontal and vertical gridlines should only be filled with the horizontal gridline brush</summary>
        HorizontalOnly,
        /// <summary>Indicates that the intersections of the horizontal and vertical gridlines should only be filled with the vertical gridline brush</summary>
        VerticalOnly,
#endif
    /// <summary>Indicates that horizontal gridlines should be rendered first, then vertical gridlines.<br/>
    /// This will result in the vertical gridline brush being drawn overtop of the horizontal gridline brush at their intersection points.</summary>
    HorizontalThenVertical,
    /// <summary>Indicates that vertical gridlines should be rendered first, then horizontal gridlines.<br/>
    /// This will result in the horizontal gridline brush being drawn overtop of the vertical gridline brush at their intersection points.</summary>
    VerticalThenHorizontal
}

public enum GridSelectionMode
{
    /// <summary>Indicates that nothing in an <see cref="MGGrid"/> can be selected via left-clicking.</summary>
    None,
    /// <summary>Indicates that an entire row of content in an <see cref="MGGrid"/> can be selected by left-clicking on any cell in the <see cref="RowDefinition"/></summary>
    Row,
    /// <summary>Indicates that an entire column of content in an <see cref="MGGrid"/> can be selected by left-clicking on any cell in the <see cref="ColumnDefinition"/></summary>
    Column,
    /// <summary>Indicates that a cell in an <see cref="MGGrid"/> can be selected by left-clicking on it</summary>
    Cell
}

public readonly record struct GridCell(RowDefinition Row, ColumnDefinition Column)
{
    public RowDefinition Row { get; init; } = Row ?? throw new ArgumentNullException(nameof(Row));
    public ColumnDefinition Column { get; init; } = Column ?? throw new ArgumentNullException(nameof(Column));
}

public readonly record struct GridSelection(MGGrid Grid, GridCell Cell, GridSelectionMode SelectionMode)
    : IEnumerable<GridCell>
{
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IEnumerator<GridCell> GetEnumerator() => GetCells().GetEnumerator();

    public MGGrid Grid { get; init; } = Grid ?? throw new ArgumentNullException(nameof(Grid));

    /// <summary>The cells of the selection, in the order of <see cref="GetEnumerator"/>, for a <c>foreach</c> that allocates nothing (the interface implementation above makes an iterator at each enumeration).
    /// The same rules as <see cref="GetCells"/>: a row selection gives the cell of each column of the row (if the row is still in the grid), and so on.</summary>
    internal CellEnumerable Cells => new(this);

    internal readonly struct CellEnumerable
    {
        private readonly GridSelection Selection;

        public CellEnumerable(GridSelection Selection)
        {
            this.Selection = Selection;
        }

        public CellEnumerator GetEnumerator() => new(Selection);
    }

    internal struct CellEnumerator
    {
        private readonly GridSelection Selection;
        private bool IsStarted;
        private bool IsInGrid;
        private int Next;

        public GridCell Current { get; private set; }

        public CellEnumerator(GridSelection Selection)
        {
            this.Selection = Selection;
            IsStarted = false;
            IsInGrid = false;
            Next = 0;
            Current = default;
        }

        public bool MoveNext()
        {
            var Grid = Selection.Grid;
            var Cell = Selection.Cell;
            switch (Selection.SelectionMode)
            {
                case GridSelectionMode.None:
                    return false;
                case GridSelectionMode.Row:
                    if (!IsStarted)
                    {
                        IsStarted = true;
                        IsInGrid = Grid.Rows.Contains(Cell.Row);
                    }

                    if (IsInGrid && Next < Grid.Columns.Count)
                    {
                        Current = new GridCell(Cell.Row, Grid.Columns[Next++]);
                        return true;
                    }

                    return false;
                case GridSelectionMode.Column:
                    if (!IsStarted)
                    {
                        IsStarted = true;
                        IsInGrid = Grid.Columns.Contains(Cell.Column);
                    }

                    if (IsInGrid && Next < Grid.Rows.Count)
                    {
                        Current = new GridCell(Grid.Rows[Next++], Cell.Column);
                        return true;
                    }

                    return false;
                case GridSelectionMode.Cell:
                    if (!IsStarted)
                    {
                        IsStarted = true;
                        IsInGrid = Grid.Rows.Contains(Cell.Row) && Grid.Columns.Contains(Cell.Column);
                        if (IsInGrid)
                        {
                            Current = Cell;
                            return true;
                        }
                    }

                    return false;
                default:
                    throw new NotImplementedException($"Unrecognized {nameof(GridSelectionMode)}: {Selection.SelectionMode}");
            }
        }
    }

    private IEnumerable<GridCell> GetCells()
    {
        switch (SelectionMode)
        {
            case GridSelectionMode.None:
                yield break;
            case GridSelectionMode.Row:
                if (Grid.Rows.Contains(Cell.Row))
                {
                    foreach (var Column in Grid.Columns)
                    {
                        yield return new GridCell(Cell.Row, Column);
                    }
                }
                yield break;
            case GridSelectionMode.Column:
                if (Grid.Columns.Contains(Cell.Column))
                {
                    foreach (var Row in Grid.Rows)
                    {
                        yield return new GridCell(Row, Cell.Column);
                    }
                }
                yield break;
            case GridSelectionMode.Cell:
                if (Grid.Rows.Contains(Cell.Row) && Grid.Columns.Contains(Cell.Column))
                {
                    yield return Cell;
                }

                yield break;
            default:
                throw new NotImplementedException($"Unrecognized {nameof(GridSelectionMode)}: {SelectionMode}");
        }
    }
}

public readonly record struct GridSpan(int RowSpan = 1, int ColumnSpan = 1, bool AffectsMeasure = true)
{
    public static readonly GridSpan Default = new(1, 1, true);

    public int RowSpan { get; init; } = RowSpan > 0 ? RowSpan : throw new ArgumentOutOfRangeException(nameof(RowSpan));
    public int ColumnSpan { get; init; } = ColumnSpan > 0 ? ColumnSpan : throw new ArgumentOutOfRangeException(nameof(ColumnSpan));

    public GridSpan() : this(Default.RowSpan, Default.ColumnSpan) { }
}

public class MGGrid : MGMultiContentHost
{
    #region Rows / Columns
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ObservableCollection<ColumnDefinition> _Columns { get; }
    public IReadOnlyList<ColumnDefinition> Columns => _Columns;
    public int GetColumnIndex(ColumnDefinition Column) => _Columns.IndexOf(Column);

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ObservableCollection<RowDefinition> _Rows { get; }
    public IReadOnlyList<RowDefinition> Rows => _Rows;
    public int GetRowIndex(RowDefinition Row) => _Rows.IndexOf(Row);

    private bool SuppressDimensionChanged = false;
    /// <summary>Converts all weighted row/column lengths to integral values that are clamped between the Min/Max size of the row/column.<para/>
    /// For example, if the columns are:<para/>
    /// <code>
    /// Index       Length      MinWidth        MaxWidth        ActualWidth
    /// 0           <b>1*</b>          20              50              50
    /// 1           25px        null            null            25
    /// 2           <b>1.2*</b>        30              100             80
    /// 
    /// Grid total width: 155
    /// </code>
    /// Normalizing the weights would convert the columns to:<para/>
    /// <code>
    /// Index       Length      MinWidth        MaxWidth        ActualWidth
    /// 0           <b>50*</b>         20              50              50
    /// 1           25px        null            null            25
    /// 2           <b>80*</b>         30              100             80
    /// 
    /// Grid total width: 155
    /// </code><para/>
    /// This method is typically only used when simplifying the row/column definitions before applying resizing logic, such as in <see cref="MGGridSplitter"/>.</summary>
    public void NormalizeWeightedLengths()
    {
        var WasSuppressingDimensionChanged = SuppressDimensionChanged;
        try
        {
            SuppressDimensionChanged = true;

            foreach (var Column in _Columns)
            {
                if (Column.Length.IsWeightedLength)
                {
                    var NormalizedWeight = Math.Clamp(Column.Width, Column.MinWidth ?? 0, Column.MaxWidth ?? int.MaxValue);
                    Column.Length = GridLength.CreateWeightedLength(NormalizedWeight);
                }
            }

            foreach (var Row in _Rows)
            {
                if (Row.Length.IsWeightedLength)
                {
                    var NormalizedWeight = Math.Clamp(Row.Height, Row.MinHeight ?? 0, Row.MaxHeight ?? int.MaxValue);
                    Row.Length = GridLength.CreateWeightedLength(NormalizedWeight);
                }
            }
        }
        finally { SuppressDimensionChanged = WasSuppressingDimensionChanged; }
    }

    /// <param name="Lengths">The lengths of each <see cref="ColumnDefinition"/> to create.<para/>
    /// Consider using <see cref="GridLength.ParseMultiple(string)"/> for a convenient/concise way to generate the values</param>
    /// <returns>The created <see cref="ColumnDefinition"/>s</returns>
    public List<ColumnDefinition> AddColumns(IEnumerable<GridLength> Lengths) => Lengths.Select(x => AddColumn(x)).ToList();

    /// <param name="Lengths">The lengths of each <see cref="ColumnDefinition"/> to create.<para/>
    /// Consider using <see cref="ConstrainedGridLength.ParseMultiple(string)"/> for a convenient/concise way to generate the values</param>
    /// <returns>The created <see cref="ColumnDefinition"/>s</returns>
    public List<ColumnDefinition> AddColumns(IEnumerable<ConstrainedGridLength> Lengths) => Lengths.Select(x => AddColumn(x)).ToList();

    /// <summary>See also: <see cref="AddColumns(IEnumerable{GridLength})"/>, <see cref="GridLength.ParseMultiple(string)"/></summary>
    /// <param name="Length">Warning - <see cref="GridUnitType.Weighted"/> is treated as <see cref="GridUnitType.Auto"/> 
    /// when the column is inside of an <see cref="MGScrollViewer"/> with a horizontal scrollbar</param>
    public ColumnDefinition AddColumn(GridLength Length) => AddColumn(new ConstrainedGridLength(Length, null, null));

    /// <summary>See also: <see cref="AddColumns(IEnumerable{ConstrainedGridLength})"/>, <see cref="ConstrainedGridLength.ParseMultiple(string)"/></summary>
    /// <param name="Length">Warning - <see cref="GridUnitType.Weighted"/> is treated as <see cref="GridUnitType.Auto"/> 
    /// when the column is inside of an <see cref="MGScrollViewer"/> with a horizontal scrollbar</param>
    public ColumnDefinition AddColumn(ConstrainedGridLength Length)
    {
        ColumnDefinition Column = new(this, Length.Length, Length.MinSize, Length.MaxSize);
        _Columns.Add(Column);
        return Column;
    }

    public ColumnDefinition InsertColumn(int Index, GridLength Length) => InsertColumn(Index, new ConstrainedGridLength(Length, null, null));
    public ColumnDefinition InsertColumn(int Index, ConstrainedGridLength Length)
    {
        ColumnDefinition Column = new(this, Length.Length, Length.MinSize, Length.MaxSize);
        _Columns.Insert(Index, Column);
        return Column;
    }

    public void RemoveColumn(ColumnDefinition Column)
    {
        if (!CanChangeContent)
        {
            return;
        }

        ClearColumnContent(Column);
        _Columns.Remove(Column);
    }

    /// <param name="Lengths">The lengths of each <see cref="RowDefinition"/> to create.<para/>
    /// Consider using <see cref="GridLength.ParseMultiple(string)"/> for a convenient/concise way to generate the values</param>
    /// <returns>The created <see cref="RowDefinition"/>s</returns>
    public List<RowDefinition> AddRows(IEnumerable<GridLength> Lengths) => Lengths.Select(x => AddRow(x)).ToList();

    /// <param name="Lengths">The lengths of each <see cref="RowDefinition"/> to create.<para/>
    /// Consider using <see cref="ConstrainedGridLength.ParseMultiple(string)"/> for a convenient/concise way to generate the values</param>
    /// <returns>The created <see cref="RowDefinition"/>s</returns>
    public List<RowDefinition> AddRows(IEnumerable<ConstrainedGridLength> Lengths) => Lengths.Select(x => AddRow(x)).ToList();

    /// <summary>See also: <see cref="AddRows(IEnumerable{GridLength})"/>, <see cref="GridLength.ParseMultiple(string)"/></summary>
    /// <param name="Length">Warning - <see cref="GridUnitType.Weighted"/> is treated as <see cref="GridUnitType.Auto"/> 
    /// when the row is inside of an <see cref="MGScrollViewer"/> with a vertical scrollbar</param>
    public RowDefinition AddRow(GridLength Length) => AddRow(new ConstrainedGridLength(Length, null, null));

    /// <summary>See also: <see cref="AddRows(IEnumerable{ConstrainedGridLength})"/>, <see cref="ConstrainedGridLength.ParseMultiple(string)"/></summary>
    /// <param name="Length">Warning - <see cref="GridUnitType.Weighted"/> is treated as <see cref="GridUnitType.Auto"/> 
    /// when the row is inside of an <see cref="MGScrollViewer"/> with a vertical scrollbar</param>
    public RowDefinition AddRow(ConstrainedGridLength Length)
    {
        RowDefinition Row = new(this, Length.Length, Length.MinSize, Length.MaxSize);
        _Rows.Add(Row);
        return Row;
    }

    public RowDefinition InsertRow(int Index, GridLength Length)
    {
        RowDefinition Row = new(this, Length);
        _Rows.Insert(Index, Row);
        return Row;
    }

    public void RemoveRow(RowDefinition Row)
    {
        if (!CanChangeContent)
        {
            return;
        }

        ClearRowContent(Row);
        _Rows.Remove(Row);
    }

    private Dictionary<GridCell, Rectangle> _CellBounds = new();
    private static readonly IReadOnlyList<MGElement> EmptyCellContent = Array.Empty<MGElement>();
    /// <summary>Warning - the <see cref="Rectangle"/>s in this dictionary do not account for <see cref="MGElement.Origin"/>.<para/>
    /// See also: <see cref="MGElement.ConvertCoordinateSpace(CoordinateSpace, CoordinateSpace, Point)"/></summary>
    public IReadOnlyDictionary<GridCell, Rectangle> CellBounds => _CellBounds;

    private Dictionary<GridCell, Rectangle> GetCellBounds(bool IncludeGridLineMargin)
    {
        Dictionary<GridCell, Rectangle> CellBounds = new();

        //  Snapshot the resolved margins once for this pass - they don't change while iterating cells.
        var resolvedRowGridLineMargin = IncludeGridLineMargin ? 0 : Math.Max(0, ResolvedRowGridLineMargin);
        var resolvedColumnGridLineMargin = IncludeGridLineMargin ? 0 : Math.Max(0, ResolvedColumnGridLineMargin);

        foreach (var Row in _Rows)
        {
            foreach (var Column in _Columns)
            {
                GridCell Cell = new(Row, Column);
                Rectangle PaddedBounds = new(Column.Left, Row.Top, Column.Width, Row.Height);
                var ActualBounds = IncludeGridLineMargin
                    ? PaddedBounds
                    : PaddedBounds.GetExpanded(new Thickness(resolvedColumnGridLineMargin, resolvedRowGridLineMargin, resolvedColumnGridLineMargin, resolvedRowGridLineMargin));
                CellBounds.Add(Cell, ActualBounds);
            }
        }

        return CellBounds;
    }
    #endregion Rows / Columns

    #region Elements
    /// <summary>All content in this <see cref="MGGrid"/>, indexed first by the <see cref="RowDefinition"/> it resides in, then by the <see cref="ColumnDefinition"/> it resides in.</summary>
    private readonly Dictionary<RowDefinition, Dictionary<ColumnDefinition, List<MGElement>>> ChildrenByRC = new();
    /// <summary>A lookup table that returns the <see cref="RowDefinition"/> and <see cref="ColumnDefinition"/> that a given <see cref="MGElement"/> resides in.</summary>
    private readonly Dictionary<MGElement, GridCell> ChildCellLookup = new();
    /// <summary>A lookup table that returns the <see cref="GridSpan"/> settings associated with a given <see cref="MGElement"/></summary>
    private readonly Dictionary<MGElement, GridSpan> ChildSpanLookup = new();

    /// <summary>Retrieves the <see cref="GridCell"/> that the given <paramref name="Element"/> belongs to.</summary>
    public bool TryGetCell(MGElement Element, out GridCell Cell) => ChildCellLookup.TryGetValue(Element, out Cell);

    public IReadOnlyDictionary<ColumnDefinition, IReadOnlyList<MGElement>> GetRowContent(RowDefinition Row)
    {
        if (ChildrenByRC.TryGetValue(Row, out var RowContent))
        {
            return RowContent.ToDictionary(x => x.Key, x => x.Value as IReadOnlyList<MGElement>);
        }
        else
        {
            return new Dictionary<ColumnDefinition, IReadOnlyList<MGElement>>();
        }
    }

    public IReadOnlyDictionary<RowDefinition, IReadOnlyList<MGElement>> GetColumnContent(ColumnDefinition Column)
    {
        Dictionary<RowDefinition, IReadOnlyList<MGElement>> ColumnContent = new();
        foreach (var KVP in ChildrenByRC)
        {
            if (KVP.Value.TryGetValue(Column, out var CellContent) && CellContent.Any())
            {
                ColumnContent.Add(KVP.Key, CellContent);
            }
        }
        return ColumnContent;
    }

    public IReadOnlyList<MGElement> GetCellContent(RowDefinition Row, ColumnDefinition Column) => GetCellContent(new GridCell(Row, Column));
    public IReadOnlyList<MGElement> GetCellContent(GridCell Cell)
    {
        if (TryGetCellContentList(Cell, out var CellContent))
        {
            return CellContent;
        }
        else
        {
            return EmptyCellContent;
        }
    }

    private bool TryGetCellContentList(GridCell cell, out List<MGElement> cellContent)
    {
        if (ChildrenByRC.TryGetValue(cell.Row, out var rowContent)
            && rowContent.TryGetValue(cell.Column, out cellContent))
        {
            return true;
        }

        cellContent = null;
        return false;
    }

    public bool TryAddChild(int RowIndex, int ColumnIndex, MGElement Item) => TryAddChild(RowIndex, ColumnIndex, GridSpan.Default, Item);
    public bool TryAddChild(int RowIndex, int ColumnIndex, GridSpan Span, MGElement Item)
    {
        if (RowIndex >= 0 && RowIndex < _Rows.Count && ColumnIndex >= 0 && ColumnIndex < _Columns.Count)
        {
            var Row = _Rows[RowIndex];
            var Column = _Columns[ColumnIndex];
            return TryAddChild(Row, Column, Span, Item);
        }
        else
        {
            return false;
        }
    }

    public bool TryAddChild(RowDefinition Row, ColumnDefinition Column, MGElement Item) => TryAddChild(Row, Column, GridSpan.Default, Item);
    public bool TryAddChild(RowDefinition Row, ColumnDefinition Column, GridSpan Span, MGElement Item) => TryAddChild(new GridCell(Row, Column), Span, Item);
    public bool TryAddChild(GridCell Cell, MGElement Item) => TryAddChild(Cell, GridSpan.Default, Item);

    /// <returns>True if the given <paramref name="Item"/> was successfully added.<br/>
    /// False otherwise, such as if <see cref="MGContentHost.CanChangeContent"/> is false.</returns>
    public bool TryAddChild(GridCell Cell, GridSpan Span, MGElement Item)
    {
        if (!CanChangeContent)
        {
            return false;
        }

        if (Item == null)
        {
            throw new ArgumentNullException(nameof(Item));
        }

        if (_Children.Contains(Item))
        {
            throw new InvalidOperationException($"{nameof(MGGrid)} does not support adding the same {nameof(MGElement)} multiple times.");
        }

        if (!ChildrenByRC.TryGetValue(Cell.Row, out var RowContent))
        {
            RowContent = new Dictionary<ColumnDefinition, List<MGElement>>();
            ChildrenByRC.Add(Cell.Row, RowContent);
        }

        if (!RowContent.TryGetValue(Cell.Column, out var CellContent))
        {
            CellContent = new List<MGElement>();
            RowContent.Add(Cell.Column, CellContent);
        }

        CellContent.Add(Item);
        ChildCellLookup[Item] = Cell;
        ChildSpanLookup[Item] = Span;

        _Children.Add(Item);
        return true;
    }

    /// <returns>True if the given <paramref name="Item"/> was found in <see cref="MGMultiContentHost.Children"/> and was successfully removed.<br/>
    /// False otherwise, such as if <see cref="MGContentHost.CanChangeContent"/> is false.</returns>
    public bool TryRemoveChild(MGElement Item)
    {
        if (!CanChangeContent)
        {
            return false;
        }

        if (_Children.Remove(Item))
        {
            var Cell = ChildCellLookup[Item];
            ChildrenByRC[Cell.Row][Cell.Column].Remove(Item);
            ChildCellLookup.Remove(Item);
            ChildSpanLookup.Remove(Item);
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>Removes all elements from every row/column of this grid</summary>
    public bool TryRemoveAll()
    {
        if (!CanChangeContent)
        {
            return false;
        }

        _Children.ClearOneByOne();
        ChildrenByRC.Clear();
        ChildCellLookup.Clear();
        ChildSpanLookup.Clear();
        return true;
    }

    public List<MGElement> ClearCellContent(int RowIndex, int ColumnIndex)
    {
        if (RowIndex >= 0 && RowIndex < _Rows.Count && ColumnIndex >= 0 && ColumnIndex < _Columns.Count)
        {
            var Row = _Rows[RowIndex];
            var Column = _Columns[ColumnIndex];
            return ClearCellContent(Row, Column);
        }
        else
        {
            return new List<MGElement>();
        }
    }

    public List<MGElement> ClearCellContent(RowDefinition Row, ColumnDefinition Column)
    {
        if (Row == null)
        {
            throw new ArgumentNullException(nameof(Row));
        }

        if (Column == null)
        {
            throw new ArgumentNullException(nameof(Column));
        }

        List<MGElement> Removed = new();
        if (!CanChangeContent)
        {
            return Removed;
        }

        var CellContent = GetCellContent(Row, Column);
        foreach (var Element in CellContent)
        {
            if (_Children.Remove(Element))
            {
                Removed.Add(Element);
                ChildCellLookup.Remove(Element);
                ChildSpanLookup.Remove(Element);
            }
        }

        if (Removed.Any())
        {
            if (Removed.Count == CellContent.Count)
            {
                ChildrenByRC[Row].Remove(Column);
            }
            else
            {
#if DEBUG
                throw new Exception($"{nameof(MGGrid)}.{nameof(ClearCellContent)}: failed to remove all elements in {Row},{Column}");
#else
                    ChildrenByRC[Row][Column].RemoveAll(x => Removed.Contains(x));
#endif
            }
        }

        return Removed;
    }

    public List<MGElement> ClearColumnContent(int ColumnIndex)
    {
        if (ColumnIndex >= 0 && ColumnIndex < _Columns.Count)
        {
            var Column = _Columns[ColumnIndex];
            return ClearColumnContent(Column);
        }
        else
        {
            return new List<MGElement>();
        }
    }

    public List<MGElement> ClearColumnContent(ColumnDefinition Column)
    {
        if (Column == null)
        {
            throw new ArgumentNullException(nameof(Column));
        }

        List<MGElement> Removed = new();
        if (!CanChangeContent)
        {
            return Removed;
        }

        foreach (var Row in Rows)
        {
            if (ChildrenByRC.TryGetValue(Row, out var RowContent) && RowContent.TryGetValue(Column, out var CellContent))
            {
                foreach (var Element in CellContent)
                {
                    if (_Children.Remove(Element))
                    {
                        Removed.Add(Element);
                        ChildCellLookup.Remove(Element);
                        ChildSpanLookup.Remove(Element);
                    }
#if DEBUG
                    else
                    {
                        throw new Exception($"{nameof(MGGrid)}.{nameof(ClearColumnContent)}: failed to remove all elements in {Row},{Column}");
                    }
#endif
                }

                ChildrenByRC[Row].Remove(Column);
            }
        }

        return Removed;
    }

    public List<MGElement> ClearRowContent(int RowIndex)
    {
        if (RowIndex >= 0 && RowIndex < _Rows.Count)
        {
            var Row = _Rows[RowIndex];
            return ClearRowContent(Row);
        }
        else
        {
            return new List<MGElement>();
        }
    }

    public List<MGElement> ClearRowContent(RowDefinition Row)
    {
        if (Row == null)
        {
            throw new ArgumentNullException(nameof(Row));
        }

        List<MGElement> Removed = new();
        if (!CanChangeContent)
        {
            return Removed;
        }

        if (ChildrenByRC.TryGetValue(Row, out var RowContent))
        {
            foreach (var KVP in RowContent)
            {
                var Column = KVP.Key;
                foreach (var Element in KVP.Value)
                {
                    if (_Children.Remove(Element))
                    {
                        Removed.Add(Element);
                        ChildCellLookup.Remove(Element);
                        ChildSpanLookup.Remove(Element);
                    }
#if DEBUG
                    else
                    {
                        throw new Exception($"{nameof(MGGrid)}.{nameof(ClearRowContent)}: failed to remove all elements in {Row},{Column}");
                    }
#endif
                }
            }

            ChildrenByRC.Remove(Row);
        }

        return Removed;
    }
    #endregion Elements

    #region Selection
    private MouseHandler SelectionMouseHandler { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GridSelectionMode _SelectionMode;
    public GridSelectionMode SelectionMode
    {
        get => _SelectionMode;
        set
        {
            if (_SelectionMode != value)
            {
                _SelectionMode = value;
                CurrentSelection = HasSelection ? CurrentSelection.Value with { SelectionMode = SelectionMode } : CurrentSelection;
                NotifyPropertyChanged(nameof(SelectionMode));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _CanDeselectByClickingSelectedCell = true;
    public bool CanDeselectByClickingSelectedCell
    {
        get => _CanDeselectByClickingSelectedCell;
        set
        {
            if (_CanDeselectByClickingSelectedCell != value)
            {
                _CanDeselectByClickingSelectedCell = value;
                NotifyPropertyChanged(nameof(CanDeselectByClickingSelectedCell));
            }
        }
    }

    private GridSelection? SelectionAtStartOfMousePress = null;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GridSelection? _CurrentSelection;
    public GridSelection? CurrentSelection
    {
        get => _CurrentSelection;
        set
        {
            if (_CurrentSelection != value)
            {
                _CurrentSelection = value;
                NotifyPropertyChanged(nameof(CurrentSelection));
                NotifyPropertyChanged(nameof(HasSelection));
                SelectionChanged?.Invoke(this, CurrentSelection);
            }
        }
    }

    public event EventHandler<GridSelection?> SelectionChanged;
    public bool HasSelection => CurrentSelection.HasValue;

    /// <param name="MousePosition">The current mouse position in <see cref="CoordinateSpace.Layout"/><para/>
    /// See also: <see cref="MGElement.ConvertCoordinateSpace(CoordinateSpace, CoordinateSpace, Point)"/></param>
    private void UpdateSelection(Point MousePosition, bool AllowDeselect)
    {
        AllowDeselect = AllowDeselect && CanDeselectByClickingSelectedCell;

        var Viewport = LayoutBounds; // Does this also need to be translated by this.Origin?
        if (TryFindParentOfType(out MGScrollViewer SV, false))
        {
            // Viewport formula for nested ScrollViewers — analysed and confirmed correct.
            // this.Origin  = UA.Offset = accumulated scroll-offset of ALL ancestor SVs.
            // SV.Origin    = UA.Offset = accumulated scroll-offset of SV's own ancestors.
            // Origin - SV.Origin = this SV's own scroll-offset only.
            // Translating SV.ContentViewport by that delta gives the correct visible range
            // in layout coordinates for any level of SV nesting.
            // Mouse events that reach this Grid are already clipped by every ancestor SV,
            // so we only need to check against the immediate parent SV.
            // See: MGUI.Samples/Features/NestedScrollViewerTest for a live validation.
            Viewport = SV.ContentViewport.GetTranslated(Origin - SV.Origin);
        }

        GridCell? Cell = null;
        foreach (var KVP in _CellBounds)
        {
            if (KVP.Value.ContainsInclusive(MousePosition) && Viewport.ContainsInclusive(MousePosition))
            {
                Cell = KVP.Key;
                break;
            }
        }

        if (Cell.HasValue)
        {
            var ClickedExistingSelection = false;
            if (HasSelection && SelectionAtStartOfMousePress.HasValue)
            {
                var PreviousCell = SelectionAtStartOfMousePress.Value.Cell;
                var CurrentCell = CurrentSelection.Value.Cell;
                var ClickedCell = Cell.Value;

                ClickedExistingSelection = SelectionMode switch
                {
                    GridSelectionMode.None => false,
                    GridSelectionMode.Row => PreviousCell.Row == ClickedCell.Row && CurrentCell.Row == ClickedCell.Row,
                    GridSelectionMode.Column => PreviousCell.Column == ClickedCell.Column && CurrentCell.Column == ClickedCell.Column,
                    GridSelectionMode.Cell => PreviousCell == ClickedCell && CurrentCell == ClickedCell,
                    _ => throw new NotImplementedException($"Unrecognized {nameof(GridSelectionMode)}: {SelectionMode}")
                };
            }

            if (AllowDeselect && ClickedExistingSelection)
            {
                CurrentSelection = null;
            }
            else
            {
                CurrentSelection = new GridSelection(this, Cell.Value, SelectionMode);
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IFillBrush _SelectionBackground = SolidFillBrushes.Yellow * 0.5f;
    public IFillBrush SelectionBackground
    {
        get => _SelectionBackground;
        set
        {
            if (_SelectionBackground != value)
            {
                _SelectionBackground = value;
                NotifyPropertyChanged(nameof(SelectionBackground));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IFillBrush _SelectionOverlay = new MGSolidFillBrush(Color.Yellow * 0.25f);
    public IFillBrush SelectionOverlay
    {
        get => _SelectionOverlay;
        set
        {
            if (_SelectionOverlay != value)
            {
                _SelectionOverlay = value;
                NotifyPropertyChanged(nameof(SelectionOverlay));
            }
        }
    }
    #endregion Selection

    #region GridLines
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GridLineIntersection _GridLineIntersectionHandling;
    /// <summary>Default value: <see cref="GridLineIntersection.HorizontalThenVertical"/></summary>
    public GridLineIntersection GridLineIntersectionHandling
    {
        get => _GridLineIntersectionHandling;
        set
        {
            if (_GridLineIntersectionHandling != value)
            {
                _GridLineIntersectionHandling = value;
                NotifyPropertyChanged(nameof(GridLineIntersectionHandling));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private GridLinesVisibility _GridLinesVisibility;
    /// <summary>Default value: <see cref="GridLinesVisibility.None"/></summary>
    public GridLinesVisibility GridLinesVisibility
    {
        get => _GridLinesVisibility;
        set
        {
            if (_GridLinesVisibility != value)
            {
                _GridLinesVisibility = value;
                CheckIfOuterPaddingChanged();
                NotifyPropertyChanged(nameof(GridLinesVisibility));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _GridLineMargin;
    /// <summary>The amount of empty space around each gridline.<br/>
    /// For example: If horizontal gridlines are visible, and <see cref="RowSpacing"/> = 6, and <see cref="GridLineMargin"/> = 1, 
    /// then the horizontal gridline brush would fill the center 4 pixels of the 6 pixels between each row.<para/>
    /// This value should be less than half the value of <see cref="RowSpacing"/>/<see cref="ColumnSpacing"/> or else the corresponding gridline brush won't have any space to fill.</summary>
    public int GridLineMargin
    {
        get => _GridLineMargin;
        set
        {
            if (_GridLineMargin != value)
            {
                _GridLineMargin = value;
                CheckIfOuterPaddingChanged();
                NotifyPropertyChanged(nameof(GridLineMargin));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IFillBrush _HorizontalGridLineBrush;
    public IFillBrush HorizontalGridLineBrush
    {
        get => _HorizontalGridLineBrush;
        set
        {
            if (_HorizontalGridLineBrush != value)
            {
                _HorizontalGridLineBrush = value;
                NotifyPropertyChanged(nameof(HorizontalGridLineBrush));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IFillBrush _VerticalGridLineBrush;
    public IFillBrush VerticalGridLineBrush
    {
        get => _VerticalGridLineBrush;
        set
        {
            if (_VerticalGridLineBrush != value)
            {
                _VerticalGridLineBrush = value;
                NotifyPropertyChanged(nameof(VerticalGridLineBrush));
            }
        }
    }
    #endregion GridLines

    private void CheckIfOuterPaddingChanged()
    {
        var resolvedRowSpacing = ResolvedRowSpacing;
        var resolvedColumnSpacing = ResolvedColumnSpacing;
        if (resolvedRowSpacing > 0 && ResolvedRowGridLineMargin < resolvedRowSpacing &&
            (GridLinesVisibility.HasFlag(GridLinesVisibility.TopEdge) || GridLinesVisibility.HasFlag(GridLinesVisibility.BottomEdge)))
        {
            LayoutChanged(this, true);
        }
        else if (resolvedColumnSpacing > 0 && ResolvedColumnGridLineMargin < resolvedColumnSpacing &&
                 (GridLinesVisibility.HasFlag(GridLinesVisibility.LeftEdge) || GridLinesVisibility.HasFlag(GridLinesVisibility.RightEdge)))
        {
            LayoutChanged(this, true);
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _RowSpacing;
    /// <summary>The amount of padding, in pixels, between each consecutive row.<para/>
    /// Default value: 0<para/>
    /// If horizontal gridlines are visible, this value should be at least more than twice as large as <see cref="GridLineMargin"/></summary>
    public int RowSpacing
    {
        get => _RowSpacing;
        set
        {
            if (_RowSpacing != value)
            {
                _RowSpacing = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(RowSpacing));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _ColumnSpacing;
    /// <summary>The amount of padding, in pixels, between each consecutive column.<para/>
    /// Default value: 0<para/>
    /// If vertical gridlines are visible, this value should be at least more than twice as large as <see cref="GridLineMargin"/></summary>
    public int ColumnSpacing
    {
        get => _ColumnSpacing;
        set
        {
            if (_ColumnSpacing != value)
            {
                _ColumnSpacing = value;
                LayoutChanged(this, true);
                NotifyPropertyChanged(nameof(ColumnSpacing));
            }
        }
    }

    /// <summary>The effective <see cref="RowSpacing"/> used during measure/arrange, after applying <see cref="MGElement.ResponsiveSpacingScaleFactor"/>.<br/>
    /// Equals <see cref="RowSpacing"/> when responsive spacing scaling doesn't apply (e.g. outside a responsive subtree, or at scale factor 1.0).</summary>
    internal int ResolvedRowSpacing => ResolveOwnedSpacing(_RowSpacing);
    /// <summary>The effective <see cref="ColumnSpacing"/> used during measure/arrange, after applying <see cref="MGElement.ResponsiveSpacingScaleFactor"/>.<br/>
    /// Equals <see cref="ColumnSpacing"/> when responsive spacing scaling doesn't apply (e.g. outside a responsive subtree, or at scale factor 1.0).</summary>
    internal int ResolvedColumnSpacing => ResolveOwnedSpacing(_ColumnSpacing);
    /// <summary>The effective <see cref="GridLineMargin"/> used for horizontal gridlines (between rows), capped so that at least 1 pixel of
    /// gutter survives at <see cref="ResolvedRowSpacing"/> whenever a gridline is visible in design space. See <see cref="MGUI.Core.UI.Responsive.UIResponsiveMath.ScaleGridLineMargin(int, int, int, float)"/>.</summary>
    internal int ResolvedRowGridLineMargin => ResolveOwnedGridLineMargin(_GridLineMargin, _RowSpacing, ResolvedRowSpacing);
    /// <summary>The effective <see cref="GridLineMargin"/> used for vertical gridlines (between columns), capped so that at least 1 pixel of
    /// gutter survives at <see cref="ResolvedColumnSpacing"/> whenever a gridline is visible in design space. See <see cref="MGUI.Core.UI.Responsive.UIResponsiveMath.ScaleGridLineMargin(int, int, int, float)"/>.</summary>
    internal int ResolvedColumnGridLineMargin => ResolveOwnedGridLineMargin(_GridLineMargin, _ColumnSpacing, ResolvedColumnSpacing);

    public MGGrid(MGWindow Window)
        : base(Window, MGElementType.Grid)
    {
        using (BeginInitializing())
        {
            _Columns = new();
            _Columns.CollectionChanged += (sender, e) =>
            {
                if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset)
                {
                    LayoutChanged(this, true);
                }

                if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace && e.NewItems != null)
                {
                    foreach (ColumnDefinition Item in e.NewItems)
                    {
                        Item.DimensionsChanged += RowColumn_DimensionsChanged;
                    }
                }

                if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset && e.OldItems != null)
                {
                    foreach (ColumnDefinition Item in e.OldItems)
                    {
                        Item.DimensionsChanged -= RowColumn_DimensionsChanged;
                    }
                }
            };

            _Rows = new();
            _Rows.CollectionChanged += (sender, e) =>
            {
                if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset)
                {
                    LayoutChanged(this, true);
                }

                if (e.Action is NotifyCollectionChangedAction.Add or NotifyCollectionChangedAction.Replace && e.NewItems != null)
                {
                    foreach (RowDefinition Item in e.NewItems)
                    {
                        Item.DimensionsChanged += RowColumn_DimensionsChanged;
                    }
                }

                if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset && e.OldItems != null)
                {
                    foreach (RowDefinition Item in e.OldItems)
                    {
                        Item.DimensionsChanged -= RowColumn_DimensionsChanged;
                    }
                }
            };

            RowSpacing = 0;
            ColumnSpacing = 0;

            GridLineIntersectionHandling = GridLineIntersection.HorizontalThenVertical;

            //  Descendant: clicking an interactive element inside a cell (such as a button or checkbox that consumes
            //  the click) must still update this grid's selection. Deliberately not HandledInputPolicy.Any, which
            //  would also make the grid react to clicks aimed at a context menu or window drawn overtop of it.
            SelectionMouseHandler = InputTracker.Mouse.CreateHandler(this, null, HandledInputPolicy.Self | HandledInputPolicy.Descendant);
            SelectionMouseHandler.LMBPressedInside += (sender, e) =>
            {
                SelectionAtStartOfMousePress = CurrentSelection;
                var Position = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                UpdateSelection(Position, false);
            };
            SelectionMouseHandler.LMBReleasedInside += (sender, e) =>
            {
                var Position = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                UpdateSelection(Position, true);
            };

            SelectionMode = GridSelectionMode.None;
            CurrentSelection = null;

            Window.OnWindowPositionChanged += (sender, e) =>
            {
                Point Offset = new(e.NewValue.Left - e.PreviousValue.Left, e.NewValue.Top - e.PreviousValue.Top);
                foreach (var Column in Columns)
                {
                    Column.Left += Offset.X;
                }

                foreach (var Row in Rows)
                {
                    Row.Top += Offset.Y;
                }

                foreach (var KVP in _CellBounds.ToList())
                {
                    _CellBounds[KVP.Key] = KVP.Value.GetTranslated(Offset);
                }
            };
        }
    }

    /// <inheritdoc/>
    protected override void CollectFillBrushes(List<IFillBrush> Brushes)
    {
        base.CollectFillBrushes(Brushes);
        Brushes.Add(SelectionBackground);
        Brushes.Add(SelectionOverlay);
        Brushes.Add(HorizontalGridLineBrush);
        Brushes.Add(VerticalGridLineBrush);
    }

    private void RowColumn_DimensionsChanged(object sender, EventArgs e)
    {
        if (!SuppressDimensionChanged)
        {
            LayoutChanged(this, true);
        }
    }

    public override void UpdateSelf(ElementUpdateArgs UA)
    {
        if (UA.IsHitTestVisible)
        {
            SelectionMouseHandler.ManualUpdate();
        }

        base.UpdateSelf(UA);
    }

    private readonly record struct GridDimensions(DimensionScratch Scratch, int TotalWidth, int TotalHeight);

    private static void UpdateChildLayoutIfNeeded(MGElement child, Rectangle bounds)
    {
        if (!child.IsLayoutValid || child.AllocatedBounds != bounds)
        {
            child.UpdateLayout(bounds);
        }
    }

    private int GetSpannedColumnWidth(int columnIndex, int columnSpan, int[] columnWidths, int resolvedColumnSpacing)
    {
        var width = 0;
        for (var spanOffset = 0; spanOffset < columnSpan; spanOffset++)
        {
            if (spanOffset > 0)
            {
                width += resolvedColumnSpacing;
            }

            //  Reading the column keeps the exception of a span that goes past the last column (the widths array can be longer than the columns)
            _ = _Columns[columnIndex + spanOffset];
            width += columnWidths[columnIndex + spanOffset];
        }

        return width;
    }

    private Rectangle GetSpannedBounds(int columnIndex, int rowIndex, GridSpan span)
    {
        var bounds = Rectangle.Empty;
        var hasBounds = false;

        for (var columnOffset = 0; columnOffset < span.ColumnSpan; columnOffset++)
        {
            for (var rowOffset = 0; rowOffset < span.RowSpan; rowOffset++)
            {
                GridCell spannedCell = new(_Rows[rowIndex + rowOffset], _Columns[columnIndex + columnOffset]);
                var cellBounds = _CellBounds[spannedCell];
                bounds = hasBounds ? Rectangle.Union(bounds, cellBounds) : cellBounds;
                hasBounds = true;
            }
        }

        return hasBounds ? bounds : Rectangle.Empty;
    }

    /// <param name="IsMeasuring">True if measuring the grid's content (will result in * lengths being treated as Auto, so that this method can compute the minimally-required dimensions to show the content).<br/>
    /// False if the bounds of this element have already been allocated (will allow * lengths to stretch all available space)</param>
    /// <param name="resolvedRowSpacing">The already-resolved (responsive-scaled) <see cref="RowSpacing"/> for this pass. See <see cref="ResolvedRowSpacing"/>.</param>
    /// <param name="resolvedColumnSpacing">The already-resolved (responsive-scaled) <see cref="ColumnSpacing"/> for this pass. See <see cref="ResolvedColumnSpacing"/>.</param>
    /// <param name="resolvedRowGridLineMargin">The already-resolved (responsive-scaled) row <see cref="GridLineMargin"/> for this pass. See <see cref="ResolvedRowGridLineMargin"/>.</param>
    /// <param name="resolvedColumnGridLineMargin">The already-resolved (responsive-scaled) column <see cref="GridLineMargin"/> for this pass. See <see cref="ResolvedColumnGridLineMargin"/>.</param>
    private GridDimensions ComputeDimensions(Size AvailableSize, bool IsMeasuring,
        int resolvedRowSpacing, int resolvedColumnSpacing, int resolvedRowGridLineMargin, int resolvedColumnGridLineMargin)
    {
        var Scratch = AcquireDimensionScratch();
        var Completed = false;
        try
        {
            var Dimensions = ComputeDimensions(Scratch, AvailableSize, IsMeasuring, resolvedRowSpacing, resolvedColumnSpacing, resolvedRowGridLineMargin, resolvedColumnGridLineMargin);
            Completed = true;
            return Dimensions;
        }
        finally
        {
            if (!Completed)
            {
                ReleaseDimensions(Scratch);
            }
        }
    }

    private GridDimensions ComputeDimensions(DimensionScratch Scratch, Size AvailableSize, bool IsMeasuring,
        int resolvedRowSpacing, int resolvedColumnSpacing, int resolvedRowGridLineMargin, int resolvedColumnGridLineMargin)
    {
        var ColumnCount = _Columns.Count;
        var RowCount = _Rows.Count;
        Scratch.Prepare(ColumnCount, RowCount);
        var ColumnWidths = Scratch.ColumnWidths;
        var ColumnKnown = Scratch.ColumnKnown;
        var RowHeights = Scratch.RowHeights;
        var RowKnown = Scratch.RowKnown;

        //  If Width or Height is arbitrarily large, this element is being measured within a ScrollViewer.
        //  Which means we can't just request all the AvailableSize, or we'd end up with infinitely-sized content inside the ScrollViewer.
        var IsPseudoInfiniteWidth = AvailableSize.Width >= 1000000;
        var IsPseduoInfiniteHeight = AvailableSize.Height >= 1000000;

        var TotalColumnWeight = 0.0;
        for (var ColumnIndex = 0; ColumnIndex < ColumnCount; ColumnIndex++)
        {
            var column = _Columns[ColumnIndex];
            if (column.Length.IsWeightedLength)
            {
                TotalColumnWeight += column.Length.Weight;
            }
        }

        var RemainingColumnWeight = TotalColumnWeight;
        var TotalRowWeight = 0.0;
        for (var RowIndex = 0; RowIndex < RowCount; RowIndex++)
        {
            var row = _Rows[RowIndex];
            if (row.Length.IsWeightedLength)
            {
                TotalRowWeight += row.Length.Weight;
            }
        }

        var RemainingRowWeight = TotalRowWeight;

        //  (HasFlag boxes the enum when it is not inlined)
        var TotalColumnSpacingWidth = (ColumnCount - 1) * resolvedColumnSpacing;
        if ((GridLinesVisibility & GridLinesVisibility.LeftEdge) != 0)
        {
            TotalColumnSpacingWidth += Math.Max(0, resolvedColumnSpacing - resolvedColumnGridLineMargin);
        }

        if ((GridLinesVisibility & GridLinesVisibility.RightEdge) != 0)
        {
            TotalColumnSpacingWidth += Math.Max(0, resolvedColumnSpacing - resolvedColumnGridLineMargin);
        }

        var TotalRowSpacingHeight = (RowCount - 1) * resolvedRowSpacing;
        if ((GridLinesVisibility & GridLinesVisibility.TopEdge) != 0)
        {
            TotalRowSpacingHeight += Math.Max(0, resolvedRowSpacing - resolvedRowGridLineMargin);
        }

        if ((GridLinesVisibility & GridLinesVisibility.BottomEdge) != 0)
        {
            TotalRowSpacingHeight += Math.Max(0, resolvedRowSpacing - resolvedRowGridLineMargin);
        }

        //  Fill in the trivial column measurements where we know exactly how wide they are
        var TotalWidth = TotalColumnSpacingWidth;
        var RemainingColumnWidth = Math.Max(0, AvailableSize.Width - TotalColumnSpacingWidth);
        for (var ColumnIndex = 0; ColumnIndex < ColumnCount; ColumnIndex++)
        {
            var Column = _Columns[ColumnIndex];
            int? ColumnWidth = null;
            if (Column.Length.IsPixelLength)
            {
                ColumnWidth = Column.Length.Pixels;
            }
            else if (Column.MinWidth.HasValue && Column.MaxWidth.HasValue && Column.MinWidth == Column.MaxWidth)
            {
                ColumnWidth = Column.MinWidth.Value;
            }

            if (ColumnWidth.HasValue)
            {
                var Width = Math.Min(RemainingColumnWidth, Math.Min(ColumnWidth.Value, Column.MaxWidth ?? int.MaxValue));
                ColumnWidths[ColumnIndex] = Width;
                ColumnKnown[ColumnIndex] = true;
                TotalWidth += Width;
                RemainingColumnWidth -= Width;
            }
        }

        //  Fill in the trival row measurements where we know exactly how tall they are
        var TotalHeight = TotalRowSpacingHeight;
        var RemainingRowHeight = Math.Max(0, AvailableSize.Height - TotalRowSpacingHeight);
        for (var RowIndex = 0; RowIndex < RowCount; RowIndex++)
        {
            var Row = _Rows[RowIndex];
            int? RowHeight = null;
            if (Row.Length.IsPixelLength)
            {
                RowHeight = Row.Length.Pixels;
            }
            else if (Row.MinHeight.HasValue && Row.MaxHeight.HasValue && Row.MinHeight == Row.MaxHeight)
            {
                RowHeight = Row.MinHeight.Value;
            }

            if (RowHeight.HasValue)
            {
                var Height = Math.Min(RemainingRowHeight, Math.Min(RowHeight.Value, Row.MaxHeight ?? int.MaxValue));
                RowHeights[RowIndex] = Height;
                RowKnown[RowIndex] = true;
                TotalHeight += Height;
                RemainingRowHeight -= Height;
            }
        }

        //  Measure every remaining column, starting with Auto-length columns
        var RemainingColumnCount = 0;
        var ColumnOrder = Scratch.ColumnOrder;
        for (var ColumnIndex = 0; ColumnIndex < ColumnCount; ColumnIndex++)
        {
            if (!ColumnKnown[ColumnIndex])
            {
                ColumnOrder[RemainingColumnCount++] = ColumnIndex;
            }
        }

        SortColumns(ColumnOrder, RemainingColumnCount, IsPseudoInfiniteWidth);
        for (var OrderIndex = 0; OrderIndex < RemainingColumnCount; OrderIndex++)
        {
            var CurrentColumnIndex = ColumnOrder[OrderIndex];
            var Column = _Columns[CurrentColumnIndex];
            int ColumnWidth;
            if (RemainingColumnWidth <= 0)
            {
                ColumnWidth = 0;
            }
            else if (Column.MinWidth.HasValue && RemainingColumnWidth <= Column.MinWidth.Value)
            {
                ColumnWidth = Column.MinWidth.Value;
            }
            else
            {
                var IsWeightedWidth = Column.Length.IsWeightedLength && !IsPseudoInfiniteWidth; // If measured inside a scrollviewer, * lengths are treated as Auto
                if (IsWeightedWidth && !IsMeasuring)
                {
                    var ColumnWeight = Column.Length.Weight;
#if DEBUG
                    if (ColumnWeight > RemainingColumnWeight)
                    {
                        throw new InvalidOperationException("Column weight should not exceed remaining weight");
                    }
#endif
                    ColumnWidth = Math.Clamp((int)Math.Round(RemainingColumnWidth * (ColumnWeight / RemainingColumnWeight), MidpointRounding.ToEven), Column.MinWidth ?? 0, Column.MaxWidth ?? int.MaxValue);
                    RemainingColumnWeight -= ColumnWeight;
                }
                else
                {
                    //  Note: If we're measuring the content (pre-processing phase), rather than allocating space to the content, we need to know the minimum dimensions required to show the content.
                    //  So weighted columns are treated like Auto-sized columns (with their width capped at what the weighted width would be)
                    var WeightedWidth = int.MaxValue;
                    if (IsWeightedWidth)
                    {
                        var ColumnWeight = Column.Length.Weight;
                        WeightedWidth = Math.Clamp((int)Math.Round(RemainingColumnWidth * (ColumnWeight / RemainingColumnWeight), MidpointRounding.ToEven), Column.MinWidth ?? 0, Column.MaxWidth ?? int.MaxValue);
                        RemainingColumnWeight -= ColumnWeight;
                    }

                    //  Measure every element in this column
                    var HasChildWidth = false;
                    var MaxChildWidth = 0;
                    var CellAvailableWidth = Math.Min(RemainingColumnWidth, Math.Min(WeightedWidth, Column.MaxWidth ?? int.MaxValue));
                    for (var RowIndex = 0; RowIndex < RowCount; RowIndex++)
                    {
                        GridCell Cell = new(_Rows[RowIndex], Column);
                        var RowHeight = RowKnown[RowIndex] ? RowHeights[RowIndex] : RemainingRowHeight; // Lazy 'solution' because I'm too dumb to come up with the actual correct logic that avoids circular dependencies...

                        Size CellAvailableSize = new(CellAvailableWidth, RowHeight);
                        if (!TryGetCellContentList(Cell, out var cellContent))
                        {
                            continue;
                        }

                        for (var elementIndex = 0; elementIndex < cellContent.Count; elementIndex++)
                        {
                            var Element = cellContent[elementIndex];
                            if (!ChildSpanLookup[Element].AffectsMeasure)
                            {
                                continue;
                            }

                            Element.UpdateMeasurement(CellAvailableSize, out _, out var ElementSize, out _, out _);
                            var ChildWidth = ElementSize.Size.Width;
                            MaxChildWidth = HasChildWidth ? Math.Max(MaxChildWidth, ChildWidth) : ChildWidth;
                            HasChildWidth = true;
                        }
                    }

                    //  Take the maximum width of the row items in this column
                    ColumnWidth = Math.Max(Column.MinWidth ?? 0, HasChildWidth ? MaxChildWidth : 0);
                }
            }

            ColumnWidth = Math.Min(RemainingColumnWidth, Math.Min(ColumnWidth, Column.MaxWidth ?? int.MaxValue));
            ColumnWidths[CurrentColumnIndex] = ColumnWidth;
            ColumnKnown[CurrentColumnIndex] = true;
            TotalWidth += ColumnWidth;
            RemainingColumnWidth -= ColumnWidth;
        }

        //  Measure every remaining row, starting with Auto-length rows
        var RemainingRowCount = 0;
        var RowOrder = Scratch.RowOrder;
        for (var RowIndex = 0; RowIndex < RowCount; RowIndex++)
        {
            if (!RowKnown[RowIndex])
            {
                RowOrder[RemainingRowCount++] = RowIndex;
            }
        }

        SortRows(RowOrder, RemainingRowCount, IsPseduoInfiniteHeight);
        for (var OrderIndex = 0; OrderIndex < RemainingRowCount; OrderIndex++)
        {
            var CurrentRowIndex = RowOrder[OrderIndex];
            var Row = _Rows[CurrentRowIndex];
            int RowHeight;
            if (RemainingRowHeight <= 0)
            {
                RowHeight = 0;
            }
            else if (Row.MinHeight.HasValue && RemainingRowHeight <= Row.MinHeight.Value)
            {
                RowHeight = Row.MinHeight.Value;
            }
            else
            {
                var IsWeightedHeight = Row.Length.IsWeightedLength && !IsPseduoInfiniteHeight; // If measured inside a scrollviewer, * lengths are treated as Auto
                if (IsWeightedHeight && !IsMeasuring)
                {
                    var RowWeight = Row.Length.Weight;
#if DEBUG
                    if (RowWeight > RemainingRowWeight)
                    {
                        throw new InvalidOperationException("Row weight should not exceed remaining weight");
                    }
#endif
                    RowHeight = Math.Clamp((int)Math.Round(RemainingRowHeight * (RowWeight / RemainingRowWeight), MidpointRounding.ToEven), Row.MinHeight ?? 0, Row.MaxHeight ?? int.MaxValue);
                    RemainingRowWeight -= RowWeight;
                }
                else
                {
                    //  Note: If we're measuring the content (pre-processing phase), rather than allocating space to the content, we need to know the minimum dimensions required to show the content.
                    //  So weighted rows are treated like Auto-sized rows (with their height capped at what the weighted height would be)
                    var WeightedHeight = int.MaxValue;
                    if (IsWeightedHeight)
                    {
                        var RowWeight = Row.Length.Weight;
                        WeightedHeight = Math.Clamp((int)Math.Round(RemainingRowHeight * (RowWeight / RemainingRowWeight), MidpointRounding.ToEven), Row.MinHeight ?? 0, Row.MaxHeight ?? int.MaxValue);
                        RemainingRowWeight -= RowWeight;
                    }

                    //  Measure every element in this row
                    var HasChildHeight = false;
                    var MaxChildHeight = 0;
                    var CellAvailableHeight = Math.Min(RemainingRowHeight, Math.Min(WeightedHeight, Row.MaxHeight ?? int.MaxValue));
                    for (var ColumnIndex = 0; ColumnIndex < ColumnCount; ColumnIndex++)
                    {
                        var Column = _Columns[ColumnIndex];
                        GridCell Cell = new(Row, Column);
                        var ColumnWidth = ColumnWidths[ColumnIndex];

                        Size CellAvailableSize = new(ColumnWidth, CellAvailableHeight);
                        if (!TryGetCellContentList(Cell, out var cellContent))
                        {
                            continue;
                        }

                        for (var elementIndex = 0; elementIndex < cellContent.Count; elementIndex++)
                        {
                            var Element = cellContent[elementIndex];
                            if (!ChildSpanLookup[Element].AffectsMeasure)
                            {
                                continue;
                            }

                            var AvailableWidth = CellAvailableSize.Width;

                            //  Measure the element using the total width of the spanned columns
                            var ColumnSpan = ChildSpanLookup[Element].ColumnSpan;
                            if (ColumnSpan != 1)
                            {
                                AvailableWidth = GetSpannedColumnWidth(ColumnIndex, ColumnSpan, ColumnWidths, resolvedColumnSpacing);
                            }

                            Element.UpdateMeasurement(new Size(AvailableWidth, CellAvailableSize.Height), out _, out var ElementSize, out _, out _);
                            var ChildHeight = ElementSize.Size.Height;
                            MaxChildHeight = HasChildHeight ? Math.Max(MaxChildHeight, ChildHeight) : ChildHeight;
                            HasChildHeight = true;
                        }
                    }

                    //  Take the maximum height of the items in this row
                    RowHeight = Math.Max(Row.MinHeight ?? 0, HasChildHeight ? MaxChildHeight : 0);
                }
            }

            RowHeight = Math.Min(RemainingRowHeight, Math.Min(RowHeight, Row.MaxHeight ?? int.MaxValue));
            RowHeights[CurrentRowIndex] = RowHeight;
            RowKnown[CurrentRowIndex] = true;
            TotalHeight += RowHeight;
            RemainingRowHeight -= RowHeight;
        }

        return new GridDimensions(Scratch, TotalWidth, TotalHeight);
    }

    //  The measure and the arrange of the grid keep their column widths and row heights in arrays indexed like Columns and Rows, taken from the
    //  grid once it has been laid out. A pass that starts while another one of the same grid is still reading its arrays gets arrays of its own.
    private DimensionScratch _DimensionScratch;
    private bool _IsDimensionScratchInUse;

    private DimensionScratch AcquireDimensionScratch()
    {
        if (_IsDimensionScratchInUse)
        {
            return new DimensionScratch();
        }

        _IsDimensionScratchInUse = true;
        return _DimensionScratch ??= new DimensionScratch();
    }

    private void ReleaseDimensions(DimensionScratch Scratch)
    {
        if (ReferenceEquals(Scratch, _DimensionScratch))
        {
            _IsDimensionScratchInUse = false;
        }
    }

    private sealed class DimensionScratch
    {
        public int[] ColumnWidths = Array.Empty<int>();
        public bool[] ColumnKnown = Array.Empty<bool>();
        public int[] ColumnOrder = Array.Empty<int>();
        public int[] RowHeights = Array.Empty<int>();
        public bool[] RowKnown = Array.Empty<bool>();
        public int[] RowOrder = Array.Empty<int>();

        /// <summary>Sizes the arrays for the given numbers of columns and rows and marks every column and every row as not measured.</summary>
        public void Prepare(int ColumnCount, int RowCount)
        {
            if (ColumnWidths.Length < ColumnCount)
            {
                ColumnWidths = new int[ColumnCount];
                ColumnKnown = new bool[ColumnCount];
                ColumnOrder = new int[ColumnCount];
            }

            if (RowHeights.Length < RowCount)
            {
                RowHeights = new int[RowCount];
                RowKnown = new bool[RowCount];
                RowOrder = new int[RowCount];
            }

            Array.Clear(ColumnKnown, 0, ColumnCount);
            Array.Clear(RowKnown, 0, RowCount);
        }
    }

    //  The order the remaining columns are measured in (what Where/OrderBy/ThenBy/ThenByDescending gave): Auto lengths first (every length when measured inside a scrollviewer),
    //  then the smaller MaxWidth first because a truncated column might free up more width for the next weighted column to use, then the columns with a MinWidth first because a column
    //  increased upwards to its MinWidth consumes more space than usual, leaving less for the next weighted column. Equal keys keep the order of the columns (a stable insertion sort).
    private void SortColumns(int[] Order, int Count, bool IsPseudoInfiniteWidth)
    {
        for (var i = 1; i < Count; i++)
        {
            var Current = Order[i];
            var j = i - 1;
            while (j >= 0 && CompareColumns(Order[j], Current, IsPseudoInfiniteWidth) > 0)
            {
                Order[j + 1] = Order[j];
                j--;
            }

            Order[j + 1] = Current;
        }
    }

    private int CompareColumns(int FirstIndex, int SecondIndex, bool IsPseudoInfiniteWidth)
    {
        var First = _Columns[FirstIndex];
        var Second = _Columns[SecondIndex];
        var FirstKey = First.Length.IsAutoLength || IsPseudoInfiniteWidth ? 0 : 1;
        var SecondKey = Second.Length.IsAutoLength || IsPseudoInfiniteWidth ? 0 : 1;
        if (FirstKey != SecondKey)
        {
            return FirstKey < SecondKey ? -1 : 1;
        }

        var FirstMax = First.MaxWidth ?? int.MaxValue;
        var SecondMax = Second.MaxWidth ?? int.MaxValue;
        if (FirstMax != SecondMax)
        {
            return FirstMax < SecondMax ? -1 : 1;
        }

        if (First.MinWidth.HasValue != Second.MinWidth.HasValue)
        {
            return First.MinWidth.HasValue ? -1 : 1;
        }

        return 0;
    }

    private void SortRows(int[] Order, int Count, bool IsPseduoInfiniteHeight)
    {
        for (var i = 1; i < Count; i++)
        {
            var Current = Order[i];
            var j = i - 1;
            while (j >= 0 && CompareRows(Order[j], Current, IsPseduoInfiniteHeight) > 0)
            {
                Order[j + 1] = Order[j];
                j--;
            }

            Order[j + 1] = Current;
        }
    }

    private int CompareRows(int FirstIndex, int SecondIndex, bool IsPseduoInfiniteHeight)
    {
        var First = _Rows[FirstIndex];
        var Second = _Rows[SecondIndex];
        var FirstKey = First.Length.IsAutoLength || IsPseduoInfiniteHeight ? 0 : 1;
        var SecondKey = Second.Length.IsAutoLength || IsPseduoInfiniteHeight ? 0 : 1;
        if (FirstKey != SecondKey)
        {
            return FirstKey < SecondKey ? -1 : 1;
        }

        var FirstMax = First.MaxHeight ?? int.MaxValue;
        var SecondMax = Second.MaxHeight ?? int.MaxValue;
        if (FirstMax != SecondMax)
        {
            return FirstMax < SecondMax ? -1 : 1;
        }

        if (First.MinHeight.HasValue != Second.MinHeight.HasValue)
        {
            return First.MinHeight.HasValue ? -1 : 1;
        }

        return 0;
    }

    protected override Thickness UpdateContentMeasurement(Size AvailableSize)
    {
        if (!HasContent)
        {
            return UpdateContentMeasurementBaseImplementation(AvailableSize);
        }

        var resolvedRowSpacing = ResolvedRowSpacing;
        var resolvedColumnSpacing = ResolvedColumnSpacing;
        var resolvedRowGridLineMargin = ResolvedRowGridLineMargin;
        var resolvedColumnGridLineMargin = ResolvedColumnGridLineMargin;
        var Dimensions = ComputeDimensions(AvailableSize, true, resolvedRowSpacing, resolvedColumnSpacing, resolvedRowGridLineMargin, resolvedColumnGridLineMargin);
        ReleaseDimensions(Dimensions.Scratch);
        return new Thickness(Dimensions.TotalWidth, Dimensions.TotalHeight, 0, 0);
    }

    protected override void UpdateContentLayout(Rectangle Bounds)
    {
        Size AvailableSize = new(Bounds.Width, Bounds.Height);

        var resolvedRowSpacing = ResolvedRowSpacing;
        var resolvedColumnSpacing = ResolvedColumnSpacing;
        var resolvedRowGridLineMargin = ResolvedRowGridLineMargin;
        var resolvedColumnGridLineMargin = ResolvedColumnGridLineMargin;

        var Dimensions = ComputeDimensions(AvailableSize, false, resolvedRowSpacing, resolvedColumnSpacing, resolvedRowGridLineMargin, resolvedColumnGridLineMargin);
        try
        {
            ArrangeContent(Bounds, Dimensions, resolvedRowSpacing, resolvedColumnSpacing, resolvedRowGridLineMargin, resolvedColumnGridLineMargin);
        }
        finally
        {
            ReleaseDimensions(Dimensions.Scratch);
        }
    }

    private void ArrangeContent(Rectangle Bounds, GridDimensions Dimensions,
        int resolvedRowSpacing, int resolvedColumnSpacing, int resolvedRowGridLineMargin, int resolvedColumnGridLineMargin)
    {
        Size AvailableSize = new(Bounds.Width, Bounds.Height);
        var ColumnWidths = Dimensions.Scratch.ColumnWidths;
        var RowHeights = Dimensions.Scratch.RowHeights;
        Size TotalContentSize = new(Dimensions.TotalWidth, Dimensions.TotalHeight);

        //  Account for content alignment
        var ConsumedWidth = HorizontalContentAlignment == HorizontalAlignment.Stretch ? AvailableSize.Width : Math.Min(AvailableSize.Width, TotalContentSize.Width);
        Size ConsumedContentSize = new(ConsumedWidth, TotalContentSize.Height);
        var AlignedBounds = ApplyAlignment(Bounds, HorizontalContentAlignment, VerticalContentAlignment, ConsumedContentSize);

        var CurrentX = AlignedBounds.Left;
        if ((GridLinesVisibility & GridLinesVisibility.LeftEdge) != 0)
        {
            CurrentX += Math.Max(0, resolvedColumnSpacing - resolvedColumnGridLineMargin);
        }

        //  Set the bounds of each cell
        for (var ColumnIndex = 0; ColumnIndex < _Columns.Count; ColumnIndex++)
        {
            var Column = _Columns[ColumnIndex];
            var ColumnWidth = ColumnWidths[ColumnIndex];
            Column.Left = CurrentX;
            Column.Width = ColumnWidth;

            var CurrentY = AlignedBounds.Top;
            if ((GridLinesVisibility & GridLinesVisibility.TopEdge) != 0)
            {
                CurrentY += Math.Max(0, resolvedRowSpacing - resolvedRowGridLineMargin);
            }

            for (var RowIndex = 0; RowIndex < _Rows.Count; RowIndex++)
            {
                var Row = _Rows[RowIndex];
                var RowHeight = RowHeights[RowIndex];
                Row.Top = CurrentY;
                Row.Height = RowHeight;

                CurrentY += RowHeight + resolvedRowSpacing;
            }

            CurrentX += ColumnWidth + resolvedColumnSpacing;
        }

        //  The dictionary is replaced only when a cell moved or changed size (the same cells with the same bounds are the same dictionary)
        if (!AreCellBoundsCurrent())
        {
            _CellBounds = GetCellBounds(true);
        }

        //  Allocate space for each child
        for (var ColumnIndex = 0; ColumnIndex < Columns.Count; ColumnIndex++)
        {
            var Column = Columns[ColumnIndex];
            for (var RowIndex = 0; RowIndex < Rows.Count; RowIndex++)
            {
                var Row = Rows[RowIndex];

                GridCell Cell = new(Row, Column);
                if (!TryGetCellContentList(Cell, out var cellContent))
                {
                    continue;
                }

                for (var childIndex = 0; childIndex < cellContent.Count; childIndex++)
                {
                    var Child = cellContent[childIndex];
                    var Span = ChildSpanLookup[Child];
                    var spannedBounds = GetSpannedBounds(ColumnIndex, RowIndex, Span);
                    var ElementBounds = Rectangle.Intersect(Bounds, spannedBounds);
                    UpdateChildLayoutIfNeeded(Child, ElementBounds);
                }
            }
        }
    }

    /// <summary>True if <see cref="_CellBounds"/> has exactly one entry per cell, each with the bounds of its column and its row.</summary>
    private bool AreCellBoundsCurrent()
    {
        if (_CellBounds.Count != _Rows.Count * _Columns.Count)
        {
            return false;
        }

        for (var RowIndex = 0; RowIndex < _Rows.Count; RowIndex++)
        {
            var Row = _Rows[RowIndex];
            for (var ColumnIndex = 0; ColumnIndex < _Columns.Count; ColumnIndex++)
            {
                var Column = _Columns[ColumnIndex];
                if (!_CellBounds.TryGetValue(new GridCell(Row, Column), out var Bounds) ||
                    Bounds != new Rectangle(Column.Left, Row.Top, Column.Width, Row.Height))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Draws the selection overlay, where the grid used to handle its own <see cref="MGElement.OnEndingDraw"/> (a subscriber makes every draw build the event arguments).</summary>
    internal override void EndingDrawInternal(ElementDrawArgs DA)
    {
        //  A selection whose mode is None has no cell, so there is nothing to enumerate (and enumerating it allocates).
        if (HasSelection && CurrentSelection.Value.SelectionMode != GridSelectionMode.None && SelectionOverlay != null)
        {
            var ScissorBounds = DA.DT.CurrentClipBounds;
            foreach (var Cell in CurrentSelection.Value.Cells)
            {
                if (_CellBounds.TryGetValue(Cell, out var Bounds))
                {
                    var ScreenSpaceBounds = ConvertCoordinateSpace(CoordinateSpace.UnscaledScreen, CoordinateSpace.Screen, Bounds.GetTranslated(DA.Offset));
                    if (ScissorBounds.HasValue && ScreenSpaceBounds.Intersects(ScissorBounds.Value))
                    {
                        SelectionOverlay.Draw(DA, this, Bounds);
                    }
                }
            }
        }
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        //  Draw the selection background (a selection whose mode is None has no cell, so there is nothing to enumerate)
        if (HasSelection && CurrentSelection.Value.SelectionMode != GridSelectionMode.None && SelectionBackground != null)
        {
            var ScissorBounds = DA.DT.CurrentClipBounds;
            foreach (var Cell in CurrentSelection.Value.Cells)
            {
                if (_CellBounds.TryGetValue(Cell, out var Bounds))
                {
                    if (ScissorBounds.HasValue && Bounds.GetTranslated(DA.Offset).Intersects(ScissorBounds.Value))
                    {
                        SelectionBackground.Draw(DA, this, Bounds);
                    }
                }
            }
        }

        var HasHorizontalGridLines = (GridLinesVisibility & GridLinesVisibility.AllHorizontal) != 0;
        var HasVerticalGridLines = (GridLinesVisibility & GridLinesVisibility.AllVertical) != 0;

        if (HasHorizontalGridLines && HasVerticalGridLines)
        {
            if (GridLineIntersectionHandling == GridLineIntersection.HorizontalThenVertical)
            {
                DrawHorizontalGridLines(DA, LayoutBounds);
                DrawVerticalGridLines(DA, LayoutBounds);
            }
            else if (GridLineIntersectionHandling == GridLineIntersection.VerticalThenHorizontal)
            {
                DrawVerticalGridLines(DA, LayoutBounds);
                DrawHorizontalGridLines(DA, LayoutBounds);
            }
            else
            {
                throw new NotImplementedException($"Unrecognized {nameof(GridLineIntersection)}: {GridLineIntersectionHandling}");
            }
        }
        else if (HasHorizontalGridLines)
        {
            DrawHorizontalGridLines(DA, LayoutBounds);
        }
        else if (HasVerticalGridLines)
        {
            DrawVerticalGridLines(DA, LayoutBounds);
        }
    }

    /// <summary>Attempts to retrieve the bottom-right <see cref="GridCell"/> whose layout has already been computed at least once.<para/>
    /// Usually this is just (_Rows[^1], _Columns[^1]) but might not be in cases where a new row/column was just added and the layout for it is yet to be calculated.</summary>
    private GridCell GetLastCellWithKnownBounds()
    {
        var LastKnownColumn = _Columns.FirstOrDefault();
        for (var i = _Columns.Count - 1; i >= 0; i--)
        {
            var CD = _Columns[i];
            if (CD.Left != default || CD.Width != default)
            {
                LastKnownColumn = CD;
                break;
            }
        }

        var LastKnownRow = _Rows.FirstOrDefault();
        for (var i = _Rows.Count - 1; i >= 0; i--)
        {
            var RD = _Rows[i];
            if (RD.Top != default || RD.Height != default)
            {
                LastKnownRow = RD;
                break;
            }
        }

        return new(LastKnownRow, LastKnownColumn);
    }

    private void DrawHorizontalGridLines(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        if (DA.Opacity <= 0 || DA.Opacity.IsAlmostZero() || HorizontalGridLineBrush == null || Rows.Count == 0 || Columns.Count == 0)
        {
            return;
        }

        _CellBounds.TryGetValue(new GridCell(_Rows[0], _Columns[0]), out var TopLeftCellBounds);
        _CellBounds.TryGetValue(GetLastCellWithKnownBounds(), out var BottomRightCellBounds);

        //  Snapshot the resolved spacing/margin once for this drawing pass.
        var resolvedRowSpacing = ResolvedRowSpacing;
        var resolvedColumnSpacing = ResolvedColumnSpacing;
        var resolvedRowGridLineMargin = ResolvedRowGridLineMargin;
        var resolvedColumnGridLineMargin = ResolvedColumnGridLineMargin;

        var Left = TopLeftCellBounds.Left - Math.Max(0, resolvedColumnSpacing - resolvedColumnGridLineMargin);
        var Right = BottomRightCellBounds.Right + Math.Max(0, resolvedColumnSpacing - resolvedColumnGridLineMargin);
        var Top = TopLeftCellBounds.Top - Math.Max(0, resolvedRowSpacing - resolvedRowGridLineMargin);
        var Bottom = BottomRightCellBounds.Bottom + Math.Max(0, resolvedRowSpacing - resolvedRowGridLineMargin);

        var FilledHeight = Math.Max(0, resolvedRowSpacing - resolvedRowGridLineMargin * 2);

        if ((GridLinesVisibility & GridLinesVisibility.TopEdge) != 0)
        {
            Rectangle TopGridLineBounds = new(Left, Top, Right - Left, FilledHeight);
            HorizontalGridLineBrush.Draw(DA, this, TopGridLineBounds);
        }

        if ((GridLinesVisibility & GridLinesVisibility.BottomEdge) != 0)
        {
            Rectangle BottomGridLineBounds = new(Left, Bottom - FilledHeight, Right - Left, FilledHeight);
            HorizontalGridLineBrush.Draw(DA, this, BottomGridLineBounds);
        }

        if ((GridLinesVisibility & GridLinesVisibility.InnerHorizontal) != 0)
        {
            for (var i = 0; i < Rows.Count; i++)
            {
                var Row = Rows[i];

                if (i != Rows.Count - 1)
                {
                    Rectangle GridLineBounds = new(Left, Row.Top + Row.Height + resolvedRowGridLineMargin, Right - Left, FilledHeight);
                    HorizontalGridLineBrush.Draw(DA, this, GridLineBounds);
                }
            }
        }
    }

    private void DrawVerticalGridLines(ElementDrawArgs DA, Rectangle LayoutBounds)
    {
        if (DA.Opacity <= 0 || DA.Opacity.IsAlmostZero() || VerticalGridLineBrush == null || Rows.Count == 0 || Columns.Count == 0)
        {
            return;
        }

        _CellBounds.TryGetValue(new GridCell(_Rows[0], _Columns[0]), out var TopLeftCellBounds);
        _CellBounds.TryGetValue(GetLastCellWithKnownBounds(), out var BottomRightCellBounds);

        //  Snapshot the resolved spacing/margin once for this drawing pass.
        var resolvedRowSpacing = ResolvedRowSpacing;
        var resolvedColumnSpacing = ResolvedColumnSpacing;
        var resolvedRowGridLineMargin = ResolvedRowGridLineMargin;
        var resolvedColumnGridLineMargin = ResolvedColumnGridLineMargin;

        var Left = TopLeftCellBounds.Left - Math.Max(0, resolvedColumnSpacing - resolvedColumnGridLineMargin);
        var Right = BottomRightCellBounds.Right + Math.Max(0, resolvedColumnSpacing - resolvedColumnGridLineMargin);
        var Top = TopLeftCellBounds.Top - Math.Max(0, resolvedRowSpacing - resolvedRowGridLineMargin);
        var Bottom = BottomRightCellBounds.Bottom + Math.Max(0, resolvedRowSpacing - resolvedRowGridLineMargin);

        var FilledWidth = Math.Max(0, resolvedColumnSpacing - resolvedColumnGridLineMargin * 2);

        if ((GridLinesVisibility & GridLinesVisibility.LeftEdge) != 0)
        {
            Rectangle LeftGridLineBounds = new(Left, Top, FilledWidth, Bottom - Top);
            VerticalGridLineBrush.Draw(DA, this, LeftGridLineBounds);
        }

        if ((GridLinesVisibility & GridLinesVisibility.RightEdge) != 0)
        {
            Rectangle RightGridLineBounds = new(Right - FilledWidth, Top, FilledWidth, Bottom - Top);
            VerticalGridLineBrush.Draw(DA, this, RightGridLineBounds);
        }

        if ((GridLinesVisibility & GridLinesVisibility.InnerVertical) != 0)
        {
            for (var i = 0; i < Columns.Count; i++)
            {
                var Column = Columns[i];

                if (i != Columns.Count - 1)
                {
                    Rectangle GridLineBounds = new(Column.Left + Column.Width + resolvedColumnGridLineMargin, Top, FilledWidth, Bottom - Top);
                    VerticalGridLineBrush.Draw(DA, this, GridLineBounds);
                }
            }
        }
    }
}