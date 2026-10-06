using MGUI.Core.UI;
using MGUI.Core.UI.Brushes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Architecture;

/// <summary>
/// One <see cref="MGElement.Update"/> of an idle element allocates nothing: the update event args are only created for subscribers, and
/// the brushes the element ticks are collected into reused lists (<see cref="MGElement"/>'s <c>CollectBorderBrushes</c>,
/// <c>CollectVisualStateFillBrushes</c> and <c>CollectFillBrushes</c>) instead of iterators. Each frame starts with a cleared paint
/// registry, as <see cref="MGDesktop.Update"/> does, so every brush is really ticked.
/// </summary>
public class ElementUpdateAllocationTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    [Fact]
    public void Update_OfAnIdleSlider_AllocatesNothing()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 200);
        desktop.Windows.Add(window);
        MGSlider slider = new(window, 0, 100, 50);
        window.SetContent(slider);
        desktop.Update();
        desktop.Update();

        PaintUpdateRegistry registry = new();
        ElementUpdateArgs ua = new(new UpdateBaseArgs(TimeSpan.Zero, TimeSpan.FromMilliseconds(16), default, default) { PaintRegistry = registry },
            true, false, true, Point.Zero, slider.ActualLayoutBounds);

        void Tick()
        {
            registry.Clear();
            slider.Update(ua);
        }

        for (int i = 0; i < WarmupIterations; i++)
        {
            Tick();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            Tick();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }
}
