using System.Collections.ObjectModel;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>Covers the measurement of a <see cref="VirtualizingStackPanel"/> taken before its first layout pass.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class VirtualizingStackPanelMeasureTests
{
    /// <summary>A scroll viewer that scrolls horizontally measures its content with an unbounded width. Before the first
    /// layout pass, the panel has no viewport and no bounds to take a width from, so a panel without chrome measures a
    /// width of 0. Its height
    /// (item count times item height) must survive: the scroll viewer caches this measurement, and a zeroed one kept a
    /// list docked in the CasaEngine editor's Logs panel empty, with nothing realized, until something else invalidated
    /// it.</summary>
    [Fact]
    public void Measure_BeforeFirstLayout_WithUnboundedWidth_KeepsTheItemsHeight()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 320, 240));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 240, 200)
        {
            WindowStyle = WindowStyle.None,
        };
        var listBox = new MGListBox<string>(window)
        {
            VirtualizationMode = ListBoxVirtualizationMode.Always,
            ItemTemplate = item => new MGTextBlock(window, item) { PreferredHeight = 20 },
        };
        listBox.ScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        //  Without item borders the panel has no chrome either, so nothing but its content gives it a width.
        listBox.ItemsPanel.BorderThickness = new Thickness(0);
        listBox.SetItemsSource(new ObservableCollection<string>(Enumerable.Range(0, 31).Select(i => $"item {i}")));
        var panel = listBox.VirtualizingPanel;

        panel.UpdateMeasurement(new Size(int.MaxValue, int.MaxValue), out _, out Thickness fullSize, out _, out _);

        Assert.Equal(31, panel.TotalItemCount);
        Assert.True(panel.UniformItemHeight > 0);
        Assert.Equal(31 * panel.UniformItemHeight, fullSize.Height);
    }
}
