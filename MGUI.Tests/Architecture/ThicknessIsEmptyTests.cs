using MGUI.Shared.Helpers;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>
/// <see cref="ThicknessUtils.IsEmpty(Thickness)"/> runs for every uniform border drawn, every frame
/// (<see cref="MGUI.Core.UI.Brushes.BorderBrushes.MGUniformBorderBrush"/>): it reads the four sides directly instead of enumerating them with
/// LINQ, and allocates nothing.
/// </summary>
public class ThicknessIsEmptyTests
{
    [Theory]
    [InlineData(0, 0, 0, 0, true)]
    [InlineData(1, 0, 0, 0, false)]
    [InlineData(0, 1, 0, 0, false)]
    [InlineData(0, 0, 1, 0, false)]
    [InlineData(0, 0, 0, 1, false)]
    [InlineData(-1, 0, 0, 0, false)]
    [InlineData(0, 0, 0, -3, false)]
    [InlineData(2, 2, 2, 2, false)]
    public void IsEmpty_IsTrueOnlyWhenAllFourSidesAreZero(int Left, int Top, int Right, int Bottom, bool Expected)
    {
        Assert.Equal(Expected, new Thickness(Left, Top, Right, Bottom).IsEmpty());
    }

    [Fact]
    public void IsEmpty_AllocatesNothing()
    {
        Thickness empty = new(0);
        Thickness border = new(1, 0, 1, 0);
        bool any = false;

        for (int i = 0; i < 2000; i++)
        {
            any |= empty.IsEmpty() ^ border.IsEmpty();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            any |= empty.IsEmpty() ^ border.IsEmpty();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.True(any);
    }
}
