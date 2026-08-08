using System;
using System.Reflection;
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

    [Fact]
    public void QueueScrollToBottom_BurstOfCalls_AppliesExactlyOnce()
    {
        (MGDesktop desktop, MGScrollViewer scrollViewer, VirtualizingStackPanel panel) = CreateVirtualizedScrollViewer(50);

        //  Growing the content invalidates the layout, so every QueueScrollToBottom below is deferred.
        panel.TotalItemCount = 5000;
        for (int i = 0; i < 500; i++)
        {
            scrollViewer.QueueScrollToBottom();
        }

        Assert.True(scrollViewer.IsScrollToBottomQueued);

        //  The request must be a latch. It used to be deferred by subscribing to MaxVerticalOffsetChanged on every
        //  call, so a burst of N calls cost N delegate-list copies (O(N^2) overall) before the layout pass drained them.
        Assert.Null(GetEventSubscribers(scrollViewer, nameof(MGScrollViewer.MaxVerticalOffsetChanged)));

        desktop.Update();
        desktop.Update();

        Assert.False(scrollViewer.IsScrollToBottomQueued);
        Assert.True(scrollViewer.MaxVerticalOffset > 0f);
        Assert.Equal(scrollViewer.MaxVerticalOffset, scrollViewer.VerticalOffset);

        //  No residual pending scroll: once the user scrolls away, later content growth must not snap back.
        scrollViewer.VerticalOffset = 0f;
        panel.TotalItemCount = 10000;

        desktop.Update();
        desktop.Update();

        Assert.Equal(0f, scrollViewer.VerticalOffset);
    }

    /// <summary>Reads the backing delegate of a field-like event, to assert that a code path does not subscribe to it.</summary>
    private static Delegate GetEventSubscribers(object instance, string eventName)
    {
        FieldInfo field = instance.GetType().GetField(eventName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No backing field found for event '{eventName}'.");
        return (Delegate)field.GetValue(instance);
    }

    [Fact]
    public void QueueScrollToBottom_WhenLayoutIsValid_AppliesImmediately()
    {
        (MGDesktop desktop, MGScrollViewer scrollViewer, _) = CreateVirtualizedScrollViewer(5000);

        Assert.Equal(0f, scrollViewer.VerticalOffset);

        scrollViewer.QueueScrollToBottom();

        Assert.False(scrollViewer.IsScrollToBottomQueued);
        Assert.Equal(scrollViewer.MaxVerticalOffset, scrollViewer.VerticalOffset);
    }

    private static (MGDesktop Desktop, MGScrollViewer ScrollViewer, VirtualizingStackPanel Panel) CreateVirtualizedScrollViewer(int itemCount)
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

        var panel = new VirtualizingStackPanel(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            UniformItemHeight = 20,
            BufferCount = 1,
            TotalItemCount = itemCount,
            ItemGenerator = _ => new MGBorder(window),
            ItemRecycler = static (_, _) => { },
        };

        scrollViewer.SetContent(panel);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        return (desktop, scrollViewer, panel);
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