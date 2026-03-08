namespace MGUI.Tests.KeyboardNav;

/// <summary>Unit tests for keyboard navigation helpers (pure logic, no MonoGame runtime required).</summary>
public class KeyboardNavTests
{
    // ── FocusedIndex clamping ─────────────────────────────────────────────

    // Replicates the FocusedIndex clamping logic from MGListBox / MGListView
    private static int ClampFocusedIndex(int requestedValue, int count)
    {
        return count == 0 ? -1 : System.Math.Clamp(requestedValue, 0, count - 1);
    }

    [Fact]
    public void ClampFocusedIndex_EmptyList_ReturnsNegativeOne()
    {
        Assert.Equal(-1, ClampFocusedIndex(0, 0));
    }

    [Fact]
    public void ClampFocusedIndex_ValidIndex_Unchanged()
    {
        Assert.Equal(2, ClampFocusedIndex(2, 5));
    }

    [Fact]
    public void ClampFocusedIndex_NegativeInput_ClampsToZero()
    {
        Assert.Equal(0, ClampFocusedIndex(-3, 5));
    }

    [Fact]
    public void ClampFocusedIndex_TooLargeInput_ClampsToLastIndex()
    {
        Assert.Equal(4, ClampFocusedIndex(99, 5));
    }

    // ── Page navigation step ──────────────────────────────────────────────

    [Fact]
    public void PageDown_AdvancesBy10_Clamped()
    {
        int currentIndex = 3;
        int count        = 8;
        int newIndex     = System.Math.Min(count - 1, currentIndex + 10);
        Assert.Equal(count - 1, newIndex);
    }

    [Fact]
    public void PageDown_AdvancesBy10_NotClamped()
    {
        int currentIndex = 3;
        int count        = 100;
        int newIndex     = System.Math.Min(count - 1, currentIndex + 10);
        Assert.Equal(13, newIndex);
    }

    [Fact]
    public void PageUp_MovesBackBy10_Clamped()
    {
        int currentIndex = 3;
        int newIndex     = System.Math.Max(0, currentIndex - 10);
        Assert.Equal(0, newIndex);
    }

    [Fact]
    public void PageUp_MovesBackBy10_NotClamped()
    {
        int currentIndex = 25;
        int newIndex     = System.Math.Max(0, currentIndex - 10);
        Assert.Equal(15, newIndex);
    }

    // ── Navigation direction helpers ──────────────────────────────────────

    [Fact]
    public void Up_From_Zero_StaysAtZero()
    {
        int currentIndex = 0;
        int newIndex = System.Math.Max(0, currentIndex <= 0 ? 0 : currentIndex - 1);
        Assert.Equal(0, newIndex);
    }

    [Fact]
    public void Down_From_LastIndex_StaysAtLast()
    {
        int currentIndex = 4;
        int count = 5;
        int newIndex = System.Math.Min(count - 1, currentIndex < 0 ? 0 : currentIndex + 1);
        Assert.Equal(4, newIndex);
    }

    [Fact]
    public void Down_From_NoSelection_MovesToFirst()
    {
        int currentIndex = -1; // no selection
        int count = 5;
        int newIndex = System.Math.Min(count - 1, currentIndex < 0 ? 0 : currentIndex + 1);
        Assert.Equal(0, newIndex);
    }
}
