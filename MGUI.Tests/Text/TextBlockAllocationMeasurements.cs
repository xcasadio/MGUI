using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// Measurement bench of the <see cref="MGTextBlock"/> text pipeline, step by step: how many bytes each step allocates per operation, for the texts
/// of <see cref="TextBlockCorpus"/> with <see cref="MGTextBlock.AllowsInlineFormatting"/> on and off.<para/>
/// These tests assert no number: they write the measurements to the test output, and the plan of the allocation-free text pipeline copies them.<para/>
/// Steps: the <see cref="MGTextBlock.Text"/> setter (same length and different length alternation), <see cref="MGTextBlock.SetText(string, MGTextInvalidationMode)"/>
/// in <see cref="MGTextInvalidationMode.ContentOnly"/> with a stable footprint, the measure step (<see cref="MGTextBlock.MeasureSelfOverride"/>) and
/// <see cref="MGTextBlock.UpdateLines"/> after a text change, <see cref="MGTextBlock.DrawSelf"/> of an unchanged text, and a full cycle (set, update, draw).<para/>
/// Not reachable on its own: <c>UpdateRuns</c> is private and runs inside every setter row, so its cost is part of the setter rows. Steps that need a
/// text change before them (measure, lines) are measured around the step only: the change is made outside the measured window, and the bytes of the step
/// are summed over the iterations. The draw goes through a <see cref="NonRecordingDrawTransaction"/>, which allocates nothing of its own.
/// </summary>
public class TextBlockAllocationMeasurements
{
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper Output;

    public TextBlockAllocationMeasurements(ITestOutputHelper output)
    {
        Output = output;
    }

    [Theory]
    [InlineData(TextBlockCorpusId.ShortNumber, true)]
    [InlineData(TextBlockCorpusId.ShortNumber, false)]
    [InlineData(TextBlockCorpusId.OneLineSentence, true)]
    [InlineData(TextBlockCorpusId.OneLineSentence, false)]
    [InlineData(TextBlockCorpusId.MultiLine, true)]
    [InlineData(TextBlockCorpusId.MultiLine, false)]
    [InlineData(TextBlockCorpusId.WrappedSentence, true)]
    [InlineData(TextBlockCorpusId.WrappedSentence, false)]
    [InlineData(TextBlockCorpusId.Formatted, true)]
    [InlineData(TextBlockCorpusId.Formatted, false)]
    public void EachStepOfTheTextPipeline(TextBlockCorpusId id, bool allowsInlineFormatting)
    {
        TextBlockCorpusEntry entry = TextBlockCorpus.Get(id);
        string[] sameLength = { entry.Text, entry.SameLengthText };
        string[] differentLength = { entry.Text, entry.DifferentLengthText };
        string label = $"{id} inline={allowsInlineFormatting}";

        //  Text setter, same length then different length (mode RelayoutParent: the parent layout is only requested, it runs in the next desktop update).
        {
            TextBlockFixture fixture = new(id, allowsInlineFormatting);
            MGTextBlock textBlock = fixture.TextBlock;
            int i = 0;
            long sameLengthSetter = Measure(() => textBlock.Text = sameLength[i++ & 1]);
            fixture = new(id, allowsInlineFormatting);
            textBlock = fixture.TextBlock;
            i = 0;
            long differentLengthSetter = Measure(() => textBlock.Text = differentLength[i++ & 1]);
            Output.WriteLine($"{label}: Text setter same length {sameLengthSetter} B, different length {differentLengthSetter} B");
        }

        //  SetText in ContentOnly mode on a stable footprint: UpdateLines and the measure comparison run inside the setter.
        {
            TextBlockFixture fixture = new(id, allowsInlineFormatting);
            MGTextBlock textBlock = fixture.TextBlock;
            textBlock.HasStableTextFootprint = true;
            int i = 0;
            long sameLengthContentOnly = Measure(() => textBlock.SetText(sameLength[i++ & 1], MGTextInvalidationMode.ContentOnly));
            fixture = new(id, allowsInlineFormatting);
            textBlock = fixture.TextBlock;
            textBlock.HasStableTextFootprint = true;
            i = 0;
            long differentLengthContentOnly = Measure(() => textBlock.SetText(differentLength[i++ & 1], MGTextInvalidationMode.ContentOnly));
            Output.WriteLine($"{label}: SetText ContentOnly (stable footprint) same length {sameLengthContentOnly} B, different length {differentLengthContentOnly} B");
        }

        //  Measure step after a text change: the Text setter clears the measurement cache (LayoutChanged), so MeasureSelfOverride measures again.
        //  UpdateMeasurement is protected: MeasureSelfOverride is the smallest public step that parses the lines for the measure.
        {
            TextBlockFixture fixture = new(id, allowsInlineFormatting);
            MGTextBlock textBlock = fixture.TextBlock;
            int i = 0;
            long measure = MeasureStep(() => textBlock.Text = differentLength[i++ & 1], () => textBlock.MeasureSelfOverride(fixture.AvailableSize, out _));

            //  UpdateLines after a text change: LayoutChanged marks the lines dirty, so UpdateLines parses them again.
            fixture = new(id, allowsInlineFormatting);
            textBlock = fixture.TextBlock;
            i = 0;
            long lines = MeasureStep(() => textBlock.Text = differentLength[i++ & 1], () => textBlock.UpdateLines());
            Output.WriteLine($"{label}: after a text change, MeasureSelfOverride {measure} B, UpdateLines {lines} B");
        }

        //  DrawSelf of an unchanged text.
        {
            TextBlockFixture fixture = new(id, allowsInlineFormatting);
            MGTextBlock textBlock = fixture.TextBlock;
            ElementDrawArgs drawArgs = fixture.CreateDrawArgs(fixture.Transaction);
            long callsBefore = fixture.Transaction.CallCount;
            long draw = Measure(() => textBlock.DrawSelf(drawArgs, textBlock.LayoutBounds));
            Assert.True(fixture.Transaction.CallCount > callsBefore);
            Output.WriteLine($"{label}: DrawSelf unchanged text {draw} B");
        }

        //  Full cycle: change the text, update the desktop (layout, measure), draw the desktop.
        {
            TextBlockFixture fixture = new(id, allowsInlineFormatting);
            int i = 0;
            long sameLengthCycle = Measure(() => FullCycle(fixture, sameLength[i++ & 1]));
            fixture = new(id, allowsInlineFormatting);
            i = 0;
            long differentLengthCycle = Measure(() => FullCycle(fixture, differentLength[i++ & 1]));
            fixture = new(id, allowsInlineFormatting);
            long unchangedFrame = Measure(() => fixture.Frame());
            Output.WriteLine($"{label}: full cycle (set, Update, Draw) same length {sameLengthCycle} B, different length {differentLengthCycle} B; frame without change {unchangedFrame} B");
        }
    }

    private static void FullCycle(TextBlockFixture fixture, string text)
    {
        fixture.TextBlock.Text = text;
        fixture.Frame();
    }

    private static long Measure(Action action)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            action();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            action();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        //  Rounded up: a total under one byte per iteration must not read as zero.
        return (after - before + MeasuredIterations - 1) / MeasuredIterations;
    }

    /// <summary>Runs <paramref name="prepare"/> outside the measured window, then <paramref name="step"/> inside it, and averages the bytes of the step alone.</summary>
    private static long MeasureStep(Action prepare, Action step)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            prepare();
            step();
        }

        long total = 0;
        AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            prepare();
            long before = GC.GetAllocatedBytesForCurrentThread();
            step();
            total += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        return (total + MeasuredIterations - 1) / MeasuredIterations;
    }
}
