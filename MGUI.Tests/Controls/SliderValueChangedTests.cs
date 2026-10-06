using MGUI.Core.UI;
using MGUI.Tests.Graph;
using System.Collections.Generic;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Controls;

/// <summary>
/// A value change of <see cref="MGSlider"/> allocates nothing of its own.<para/>
/// <see cref="MGSlider.ValueChanged"/> keeps its <see cref="MGUI.Shared.Helpers.EventArgs{TProperty}"/> and is raised first;
/// <see cref="MGSlider.ValueChangedNonAlloc"/> follows with the same values passed by value. The slider no longer subscribes to its own
/// event to refresh its value label, so the legacy event allocates only for its own subscribers. The value label is formatted into a
/// reused buffer and rebuilt only when the displayed text changes.
/// </summary>
public class SliderValueChangedTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    [Fact]
    public void SetValue_RaisesValueChangedThenValueChangedNonAlloc_OnceEach_WithPreviousAndNewValues()
    {
        MGSlider slider = CreateSlider(0, 10, 2);
        List<(string Event, float Previous, float New)> calls = new();
        slider.ValueChanged += (_, e) => calls.Add(("ValueChanged", e.PreviousValue, e.NewValue));
        slider.ValueChangedNonAlloc += (_, e) => calls.Add(("ValueChangedNonAlloc", e.PreviousValue, e.NewValue));

        float actual = slider.SetValue(7);

        Assert.Equal(7f, actual);
        Assert.Equal(new[] { ("ValueChanged", 2f, 7f), ("ValueChangedNonAlloc", 2f, 7f) }, calls);
    }

    [Fact]
    public void SetValue_ReportsTheClampedValue()
    {
        MGSlider slider = CreateSlider(0, 10, 2);
        List<(float Previous, float New)> legacy = new();
        List<(float Previous, float New)> nonAlloc = new();
        slider.ValueChanged += (_, e) => legacy.Add((e.PreviousValue, e.NewValue));
        slider.ValueChangedNonAlloc += (_, e) => nonAlloc.Add(e);

        slider.SetValue(25);

        Assert.Equal(new[] { (2f, 10f) }, legacy);
        Assert.Equal(new[] { (2f, 10f) }, nonAlloc);
    }

    [Fact]
    public void SetValue_ReportsTheDiscreteValue()
    {
        MGSlider slider = CreateSlider(0, 10, 2);
        slider.DiscreteValueInterval = 0.5f;
        slider.UseDiscreteValues = true;
        List<(float Previous, float New)> legacy = new();
        List<(float Previous, float New)> nonAlloc = new();
        slider.ValueChanged += (_, e) => legacy.Add((e.PreviousValue, e.NewValue));
        slider.ValueChangedNonAlloc += (_, e) => nonAlloc.Add(e);

        slider.SetValue(3.3f);

        Assert.Equal(new[] { (2f, 3.5f) }, legacy);
        Assert.Equal(new[] { (2f, 3.5f) }, nonAlloc);
    }

    [Fact]
    public void SetValue_ToTheCurrentActualValue_RaisesNothing()
    {
        MGSlider slider = CreateSlider(0, 10, 2);
        slider.DiscreteValueInterval = 0.5f;
        slider.UseDiscreteValues = true;
        int raised = 0;
        slider.ValueChanged += (_, _) => raised++;
        slider.ValueChangedNonAlloc += (_, _) => raised++;

        slider.SetValue(2);
        slider.SetValue(2.1f);

        Assert.Equal(0, raised);
    }

    [Fact]
    public void ValueLabel_FollowsTheValueTheFormatAndShowValueLabel()
    {
        MGSlider slider = CreateSlider(0, 10, 2);
        MGTextBlock label = slider.ValueLabelComponent.Element;
        string hiddenText = label.Text;

        slider.SetValue(3.25f);
        Assert.Equal(hiddenText, label.Text);

        slider.ShowValueLabel = true;
        Assert.Equal(3.25f.ToString("F0"), label.Text);

        slider.ValueLabelFormat = "F2";
        Assert.Equal(3.25f.ToString("F2"), label.Text);

        slider.SetValue(6.5f);
        Assert.Equal(6.5f.ToString("F2"), label.Text);

        slider.ValueLabelFormat = null;
        Assert.Equal(6.5f.ToString("F0"), label.Text);
    }

    [Fact]
    public void ValueLabel_IsAlreadyUpToDate_WhenValueChangedSubscribersRun()
    {
        MGSlider slider = CreateSlider(0, 10, 2);
        slider.ShowValueLabel = true;
        slider.ValueLabelFormat = "F2";
        MGTextBlock label = slider.ValueLabelComponent.Element;
        string seenByLegacy = null;
        string seenByNonAlloc = null;
        slider.ValueChanged += (_, _) => seenByLegacy = label.Text;
        slider.ValueChangedNonAlloc += (_, _) => seenByNonAlloc = label.Text;

        slider.SetValue(4.75f);

        Assert.Equal(4.75f.ToString("F2"), seenByLegacy);
        Assert.Equal(4.75f.ToString("F2"), seenByNonAlloc);
    }

    [Fact]
    public void RepeatedValueChanges_WithoutSubscriber_AllocateNothing()
    {
        MGSlider slider = CreateSlider(0, 10, 2);

        Assert.Equal(0, MeasureAlternatingChanges(slider, 3f, 7f));
    }

    [Fact]
    public void RepeatedValueChanges_WithAValueChangedNonAllocSubscriber_AllocateNothing()
    {
        MGSlider slider = CreateSlider(0, 10, 2);
        float sum = 0;
        slider.ValueChangedNonAlloc += (_, e) => sum += e.NewValue;

        Assert.Equal(0, MeasureAlternatingChanges(slider, 3f, 7f));
        Assert.True(sum > 0);
    }

    [Fact]
    public void RepeatedValueChanges_WhoseDisplayedTextDoesNotChange_AllocateNothing()
    {
        MGSlider slider = CreateSlider(0, 10, 2);
        slider.ShowValueLabel = true;
        slider.ValueLabelFormat = "F0";
        slider.SetValue(3.2f);
        string displayed = slider.ValueLabelComponent.Element.Text;

        Assert.Equal(0, MeasureAlternatingChanges(slider, 3.2f, 3.4f));
        Assert.Equal(3.4f.ToString("F0"), slider.ValueLabelComponent.Element.Text);
        Assert.Same(displayed, slider.ValueLabelComponent.Element.Text);
    }

    /// <summary>Alternates the slider between <paramref name="First"/> and <paramref name="Second"/> (every call is a real change), warms the
    /// path up, then returns the bytes this thread allocated over <see cref="MeasuredIterations"/> changes.</summary>
    private static long MeasureAlternatingChanges(MGSlider Slider, float First, float Second)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            Slider.SetValue(i % 2 == 0 ? First : Second);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            Slider.SetValue(i % 2 == 0 ? First : Second);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        return after - before;
    }

    private static MGSlider CreateSlider(float Minimum, float Maximum, float Value)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 240, 200);
        desktop.Windows.Add(window);

        MGSlider slider = new(window, Minimum, Maximum, Value);
        window.SetContent(slider);
        return slider;
    }
}
