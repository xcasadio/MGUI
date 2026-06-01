using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;

namespace MGUI.Tests.Architecture;

public class VirtualizingWrapPanelLayoutTests
{
    [Fact]
    public void Measure_ComputesRowsFromAvailableWidth()
    {
        Size result = VirtualizingWrapPanelLayout.Measure(
            totalItemCount: 10,
            availableSize: new Size(250, int.MaxValue),
            itemWidth: 80,
            itemHeight: 40,
            spacing: 5);

        Assert.Equal(new Size(250, 175), result);
    }

    [Fact]
    public void VisibleRange_ReturnsBufferedRows()
    {
        VirtualizingWrapPanelVisibleRange result = VirtualizingWrapPanelLayout.GetVisibleRange(
            totalItemCount: 100,
            columns: 4,
            itemHeight: 20,
            spacing: 5,
            verticalOffset: 50,
            viewportHeight: 60,
            bufferRows: 1);

        Assert.Equal(4, result.FirstIndex);
        Assert.Equal(23, result.LastIndex);
    }

    [Fact]
    public void ItemBounds_UsesRowAndColumnPitch()
    {
        Rectangle result = VirtualizingWrapPanelLayout.GetItemBounds(
            index: 5,
            columns: 3,
            bounds: new Rectangle(10, 20, 400, 300),
            itemWidth: 80,
            itemHeight: 40,
            spacing: 6);

        Assert.Equal(new Rectangle(182, 66, 80, 40), result);
    }
}