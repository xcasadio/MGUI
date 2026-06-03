using System;
using MGUI.Core.UI;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Architecture;

public class MeasurementCacheReuseTests
{
    [Fact]
    public void UpdateMeasurement_ReusesFullMeasurementCache_AfterArrangeOnlyInvalidation()
    {
        var runtime = new GraphTestRuntime(new Rectangle(0, 0, 320, 180));
        var desktop = new MGDesktop(runtime);
        var window = new MGWindow(desktop, 0, 0, 240, 120)
        {
            WindowStyle = WindowStyle.None,
        };

        var element = new CountingElement(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        window.SetContent(element);
        desktop.Windows.Add(window);

        desktop.Update();
        desktop.Update();

        Assert.True(element.IsLayoutValid);
        Assert.True(element.ContentMeasurementCallCount > 0);

        element.ResetCounts();
        element.TriggerArrangeOnlyInvalidation();

        Assert.False(element.IsLayoutValid);

        element.MeasureForTest(new Size(element.ActualLayoutBounds.Width, element.ActualLayoutBounds.Height));

        Assert.Equal(0, element.ContentMeasurementCallCount);
    }

    private sealed class CountingElement : MGElement
    {
        public int ContentMeasurementCallCount { get; private set; }

        public CountingElement(MGWindow window)
            : base(window, MGElementType.Custom)
        {
        }

        public void ResetCounts()
            => ContentMeasurementCallCount = 0;

        public void TriggerArrangeOnlyInvalidation()
            => ArrangeChanged(this, false);

        public void MeasureForTest(Size availableSize)
            => UpdateMeasurement(availableSize, out _, out _, out _, out _);

        protected override Thickness UpdateContentMeasurement(Size availableSize)
        {
            ContentMeasurementCallCount++;
            return new Thickness(Math.Min(availableSize.Width, 40), Math.Min(availableSize.Height, 20), 0, 0);
        }
    }
}