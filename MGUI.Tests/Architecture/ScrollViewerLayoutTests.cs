using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Architecture;

public class ScrollViewerLayoutTests
{
    [Fact]
    public void MaxVerticalOffset_Recomputes_WhenVirtualizedContentCountGrows()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 320, 240));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 240, 200)
        {
            WindowStyle = WindowStyle.None,
        };

        var scrollViewer = new MGScrollViewer(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var panel = new VirtualizingWrapPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ItemWidth = 96,
            ItemHeight = 80,
            Spacing = 4,
            BufferRows = 1,
            TotalItemCount = 12,
            ItemGenerator = _ => new MGBorder(window),
            ItemRecycler = static (_, _) => { },
        };

        scrollViewer.SetContent(panel);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        float initialMaxVerticalOffset = scrollViewer.MaxVerticalOffset;
        Assert.True(initialMaxVerticalOffset > 0f);

        panel.TotalItemCount = 908;

        desktop.Update();
        desktop.Update();

        Assert.True(scrollViewer.MaxVerticalOffset > initialMaxVerticalOffset);

        panel.EnsureIndexVisible(907);

        Assert.True(scrollViewer.VerticalOffset > initialMaxVerticalOffset);
        Assert.InRange(scrollViewer.MaxVerticalOffset - scrollViewer.VerticalOffset, 0f, panel.ItemHeight + panel.Spacing);
    }

    [Fact]
    public void VisibleRange_Recomputes_WhenViewportHeightChanges()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 320, 320));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 240, 120)
        {
            WindowStyle = WindowStyle.None,
        };

        var scrollViewer = new MGScrollViewer(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var panel = new VirtualizingWrapPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ItemWidth = 96,
            ItemHeight = 80,
            Spacing = 4,
            BufferRows = 1,
            TotalItemCount = 908,
            ItemGenerator = _ => new MGBorder(window),
            ItemRecycler = static (_, _) => { },
        };

        scrollViewer.SetContent(panel);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        int initialLastRealizedIndex = panel.LastRealizedIndex;
        Assert.True(initialLastRealizedIndex >= 0);

        window.WindowHeight = 240;

        desktop.Update();
        desktop.Update();

        Assert.True(panel.LastRealizedIndex > initialLastRealizedIndex);
    }

    [Fact]
    public void LastRealizedIndex_ReachesLastItem_WhenScrolledToBottom()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 320, 240));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 240, 200)
        {
            WindowStyle = WindowStyle.None,
        };

        var scrollViewer = new MGScrollViewer(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var panel = new VirtualizingWrapPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ItemWidth = 96,
            ItemHeight = 80,
            Spacing = 4,
            BufferRows = 1,
            TotalItemCount = 908,
            ItemGenerator = _ => new MGBorder(window),
            ItemRecycler = static (_, _) => { },
        };

        scrollViewer.SetContent(panel);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        Assert.True(scrollViewer.MaxVerticalOffset > 0f);

        scrollViewer.VerticalOffset = scrollViewer.MaxVerticalOffset;

        desktop.Update();
        desktop.Update();

        Assert.Equal(panel.TotalItemCount - 1, panel.LastRealizedIndex);
    }

    [Fact]
    public void LastRealizedIndex_ReachesLastItem_WhenScrolledToBottom_InsideGridHost()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 320, 240));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 240, 200)
        {
            WindowStyle = WindowStyle.None,
        };

        var root = new MGGrid(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        root.AddRow(GridLength.CreateWeightedLength(1));
        root.AddColumn(GridLength.CreateWeightedLength(1));

        var scrollViewer = new MGScrollViewer(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var panel = new VirtualizingWrapPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ItemWidth = 96,
            ItemHeight = 80,
            Spacing = 4,
            BufferRows = 1,
            TotalItemCount = 908,
            ItemGenerator = _ => new MGBorder(window),
            ItemRecycler = static (_, _) => { },
        };

        scrollViewer.SetContent(panel);
        Assert.True(root.TryAddChild(0, 0, scrollViewer));
        window.SetContent(root);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        Assert.True(scrollViewer.MaxVerticalOffset > 0f);

        scrollViewer.VerticalOffset = scrollViewer.MaxVerticalOffset;

        desktop.Update();
        desktop.Update();

        Assert.Equal(panel.TotalItemCount - 1, panel.LastRealizedIndex);
    }

    [Fact]
    public void VirtualizedWrapPanel_RecomputesColumns_WhenVerticalScrollbarShrinksViewport()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 512, 320));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 412, 200)
        {
            WindowStyle = WindowStyle.None,
        };

        var scrollViewer = new MGScrollViewer(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var panel = new VirtualizingWrapPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            ItemWidth = 100,
            ItemHeight = 80,
            Spacing = 4,
            BufferRows = 1,
            TotalItemCount = 40,
            ItemGenerator = _ => new MGBorder(window),
            ItemRecycler = static (_, _) => { },
        };

        scrollViewer.SetContent(panel);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        Assert.True(scrollViewer.VSBBounds.HasValue);
        Assert.Equal(396, scrollViewer.ContentViewport.Width);
        Assert.Equal(3, panel.CurrentColumnCount);
    }

    [Fact]
    public void AutoScrollViewer_ReusesRequestedContentMeasurement_DuringResizeInsideGridHost()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 320, 240));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 240, 200)
        {
            WindowStyle = WindowStyle.None,
        };

        var root = new MGGrid(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        root.AddRow(GridLength.CreateWeightedLength(1));
        root.AddColumn(GridLength.CreateWeightedLength(1));

        var scrollViewer = new MGScrollViewer(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        var content = new CountingMeasuredElement(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            RequestedSize = new Size(220, 480),
        };

        scrollViewer.SetContent(content);
        Assert.True(root.TryAddChild(0, 0, scrollViewer));
        window.SetContent(root);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        content.ResetMeasureCount();
        window.WindowHeight = 160;

        desktop.Update();

        Assert.Equal(0, content.MeasureCount);
    }

    private sealed class CountingMeasuredElement : MGBorder
    {
        public int MeasureCount { get; private set; }
        public Size RequestedSize { get; set; }

        protected override bool CanCacheSelfMeasurement => false;

        public CountingMeasuredElement(MGWindow window)
            : base(window)
        {
            BorderThickness = new Thickness(0);
            DrawBackgroundAndOverlay = false;
        }

        public void ResetMeasureCount() => MeasureCount = 0;

        public override Thickness MeasureSelfOverride(Size availableSize, out Thickness sharedSize)
        {
            MeasureCount++;
            sharedSize = new Thickness(0);
            return new Thickness(0);
        }

        protected override Thickness UpdateContentMeasurement(Size availableSize)
            => new Thickness(RequestedSize.Width, RequestedSize.Height, 0, 0);
    }
}