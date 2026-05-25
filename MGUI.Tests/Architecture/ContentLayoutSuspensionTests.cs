using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Xunit;

namespace MGUI.Tests.Architecture;

public class ContentLayoutSuspensionTests
{
    [Fact]
    public void SuspendContentLayout_CoalescesChildMutationInvalidations()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 320, 180));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 240, 120)
        {
            WindowStyle = WindowStyle.None,
        };
        var panel = new TrackingStackPanel(window);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        desktop.Update();
        panel.ResetLayoutChangedCount();

        using (panel.SuspendContentLayout())
        {
            Assert.True(panel.TryAddChild(new MGTextBlock(window, "First")));
            Assert.True(panel.TryAddChild(new MGTextBlock(window, "Second")));
            Assert.Equal(0, panel.LayoutChangedCallCount);
        }

        Assert.Equal(1, panel.LayoutChangedCallCount);
    }

    private sealed class TrackingStackPanel : MGStackPanel
    {
        public int LayoutChangedCallCount { get; private set; }

        public TrackingStackPanel(MGWindow window)
            : base(window, Orientation.Vertical)
        {
        }

        public void ResetLayoutChangedCount()
            => LayoutChangedCallCount = 0;

        protected override void LayoutChanged(MGElement source, bool notifyParent)
        {
            LayoutChangedCallCount++;
            base.LayoutChanged(source, notifyParent);
        }
    }
}
