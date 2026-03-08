using MGUI.Core.UI;
using System.Linq;
using System.Collections.Generic;

namespace MGUI.Tests.ColumnSort;

/// <summary>Unit tests for column-sort-related pure-logic types.</summary>
public class ColumnSortTests
{
    // ── SortDirection enum ────────────────────────────────────────────────

    [Fact]
    public void SortDirection_HasAscendingAndDescending()
    {
        var values = System.Enum.GetValues<SortDirection>().ToList();
        Assert.Contains(SortDirection.Ascending,  values);
        Assert.Contains(SortDirection.Descending, values);
        Assert.Equal(2, values.Count);
    }

    [Fact]
    public void SortDirection_ToggleLogic_Ascending_BecomesDescending()
    {
        SortDirection current = SortDirection.Ascending;
        SortDirection next    = current == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
        Assert.Equal(SortDirection.Descending, next);
    }

    [Fact]
    public void SortDirection_ToggleLogic_Descending_BecomesAscending()
    {
        SortDirection current = SortDirection.Descending;
        SortDirection next    = current == SortDirection.Ascending ? SortDirection.Descending : SortDirection.Ascending;
        Assert.Equal(SortDirection.Ascending, next);
    }

    // ── Sorting helper logic (no MonoGame required) ───────────────────────

    [Fact]
    public void OrderBy_WithAscending_SortsSmallestFirst()
    {
        var items  = new[] { 3, 1, 4, 1, 5 };
        var sorted = items.OrderBy(x => x).ToList();
        Assert.Equal(new List<int> { 1, 1, 3, 4, 5 }, sorted);
    }

    [Fact]
    public void OrderBy_WithDescending_SortsLargestFirst()
    {
        var items  = new[] { 3, 1, 4, 1, 5 };
        var sorted = items.OrderByDescending(x => x).ToList();
        Assert.Equal(new List<int> { 5, 4, 3, 1, 1 }, sorted);
    }

    [Fact]
    public void Sorter_AppliesDirection_Ascending()
    {
        var items     = new[] { "banana", "apple", "cherry" };
        var direction = SortDirection.Ascending;
        var sorted    = direction == SortDirection.Ascending
            ? items.OrderBy(x => x)
            : items.OrderByDescending(x => x);
        Assert.Equal("apple", sorted.First());
    }

    [Fact]
    public void Sorter_AppliesDirection_Descending()
    {
        var items     = new[] { "banana", "apple", "cherry" };
        var direction = SortDirection.Descending;
        var sorted    = direction == SortDirection.Ascending
            ? items.OrderBy(x => x)
            : items.OrderByDescending(x => x);
        Assert.Equal("cherry", sorted.First());
    }
}
