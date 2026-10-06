using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using System.Reflection;

namespace MGUI.Tests.ColorPicker;

/// <summary>
/// <see cref="MGColorSlider"/> raises <see cref="MGColorSlider.ValueChangedNonAlloc"/> right after <see cref="MGColorSlider.ValueChanged"/>,
/// and <see cref="MGColorSlider.ValueChangingNonAlloc"/> right after <see cref="MGColorSlider.ValueChanging"/>, with the same values passed
/// by value, so that a subscriber of the new events allocates nothing per change.
/// </summary>
public class ColorSliderValueEventsTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    private static readonly MethodInfo SetValueFromScreenPositionMethod =
        typeof(MGColorSlider).GetMethod("SetValueFromScreenPosition", BindingFlags.NonPublic | BindingFlags.Instance);

    [Fact]
    public void SetValue_RaisesValueChangedThenValueChangedNonAlloc_OnceEach_WithPreviousAndNewValues()
    {
        Harness harness = Harness.Create();
        MGColorSlider slider = harness.Slider;
        slider.SetValue(0.25f);
        List<(string Event, float Previous, float New)> calls = new();
        slider.ValueChanged += (_, e) => calls.Add(("ValueChanged", e.PreviousValue, e.NewValue));
        slider.ValueChangedNonAlloc += (_, e) => calls.Add(("ValueChangedNonAlloc", e.PreviousValue, e.NewValue));

        slider.SetValue(0.75f);
        slider.SetValue(0.75f);
        slider.SetValue(3f);

        Assert.Equal(new[]
        {
            ("ValueChanged", 0.25f, 0.75f), ("ValueChangedNonAlloc", 0.25f, 0.75f),
            ("ValueChanged", 0.75f, 1f), ("ValueChangedNonAlloc", 0.75f, 1f),
        }, calls);
    }

    [Fact]
    public void Dragging_RaisesValueChangingThenValueChangingNonAlloc_WithTheSameValues()
    {
        Harness harness = Harness.Create();
        MGColorSlider slider = harness.Slider;
        List<(string Event, float Previous, float New)> calls = new();
        slider.ValueChanging += (_, e) => calls.Add(("ValueChanging", e.PreviousValue, e.NewValue));
        slider.ValueChangingNonAlloc += (_, e) => calls.Add(("ValueChangingNonAlloc", e.PreviousValue, e.NewValue));

        harness.AdvanceFrame(harness.PointAt(0.2f), ButtonState.Pressed);
        float pressedValue = slider.Value;
        harness.AdvanceFrame(harness.PointAt(0.7f), ButtonState.Pressed);
        float draggedValue = slider.Value;

        Assert.NotEqual(pressedValue, draggedValue);
        Assert.Equal(new[] { ("ValueChanging", pressedValue, draggedValue), ("ValueChangingNonAlloc", pressedValue, draggedValue) }, calls);
    }

    [Fact]
    public void RepeatedSetValue_WithAValueChangedNonAllocSubscriber_AllocatesNothing()
    {
        Harness harness = Harness.Create();
        MGColorSlider slider = harness.Slider;
        float sum = 0;
        slider.ValueChangedNonAlloc += (_, e) => sum += e.NewValue;

        for (int i = 0; i < WarmupIterations; i++)
        {
            slider.SetValue(i % 2 == 0 ? 0.25f : 0.75f);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            slider.SetValue(i % 2 == 0 ? 0.25f : 0.75f);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.True(sum > 0);
    }

    [Fact]
    public void RepeatedDragSteps_WithValueChangingNonAllocAndValueChangedNonAllocSubscribers_AllocateNothing()
    {
        Harness harness = Harness.Create();
        MGColorSlider slider = harness.Slider;
        float sum = 0;
        slider.ValueChangedNonAlloc += (_, e) => sum += e.NewValue;
        slider.ValueChangingNonAlloc += (_, e) => sum += e.NewValue;
        //  The drag step itself (SetValueFromScreenPosition, preview: true), built once outside the measured window: a real drag frame
        //  also runs the mouse tracker, measured separately by the input tasks.
        Action<Point, bool> dragStep = (Action<Point, bool>)SetValueFromScreenPositionMethod.CreateDelegate(typeof(Action<Point, bool>), slider);
        Point first = harness.PointAt(0.25f);
        Point second = harness.PointAt(0.75f);

        for (int i = 0; i < WarmupIterations; i++)
        {
            dragStep(i % 2 == 0 ? first : second, true);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            dragStep(i % 2 == 0 ? first : second, true);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.True(sum > 0);
    }

    private sealed class Harness
    {
        private readonly GraphTestRuntime Runtime;
        private readonly MGDesktop Desktop;
        private int ElapsedMilliseconds;

        public MGColorSlider Slider { get; }

        private Harness(GraphTestRuntime runtime, MGDesktop desktop, MGColorSlider slider)
        {
            Runtime = runtime;
            Desktop = desktop;
            Slider = slider;
        }

        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 300, 200);
            desktop.Windows.Add(window);

            MGColorSlider slider = new(window, ColorSliderChannel.Red);
            window.SetContent(slider);

            Harness harness = new(runtime, desktop, slider);
            harness.AdvanceFrame(new Point(1, 1), ButtonState.Released);
            harness.AdvanceFrame(new Point(1, 1), ButtonState.Released);
            return harness;
        }

        /// <summary>The screen position at <paramref name="Fraction"/> of the slider's width, on its vertical centre.</summary>
        public Point PointAt(float Fraction)
        {
            Rectangle bounds = Slider.LayoutBounds;
            Point layoutPoint = new(bounds.Left + (int)(bounds.Width * Fraction), bounds.Center.Y);
            return Slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, layoutPoint);
        }

        public void AdvanceFrame(Point screenPosition, ButtonState leftButton)
        {
            MouseState mouse = new(screenPosition.X, screenPosition.Y, 0,
                leftButton, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
            ElapsedMilliseconds += 16;
        }
    }
}
