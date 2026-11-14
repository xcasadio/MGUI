using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit.Abstractions;

namespace MGUI.Tests.Text;

/// <summary>
/// Zero allocation of the plain text pipeline of <see cref="MGTextBlock"/> (<see cref="MGTextBlock.AllowsInlineFormatting"/> false, no explicit runs), step by step: the
/// measure and the lines after a change of text, the local text invalidation modes, then the draw. Each test warms up, then counts the bytes allocated on its thread
/// over a measured loop (<see cref="AllocationWindow"/>), like the other zero allocation tests of this project.
/// </summary>
public class PlainTextAllocationTests
{
    private const int WarmupIterations = 1000;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper Output;

    public PlainTextAllocationTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private sealed class CountingTextBlock : MGTextBlock
    {
        public int LayoutChangedCallCount { get; private set; }

        public CountingTextBlock(MGWindow window, string text) : base(window, text, Color.White, 12, false) { }

        public void ResetCount() => LayoutChangedCallCount = 0;

        protected override void LayoutChanged(MGElement Source, bool NotifyParent)
        {
            LayoutChangedCallCount++;
            base.LayoutChanged(Source, NotifyParent);
        }
    }

    /// <summary>A desktop with one window and a plain <see cref="CountingTextBlock"/> built like <see cref="TextBlockFixture"/> builds its block, laid out by two frames.</summary>
    private sealed class Harness
    {
        private int Elapsed;

        public TextBlockCorpusEntry Entry { get; }
        public GraphTestRuntime Runtime { get; }
        public MGDesktop Desktop { get; }
        public CountingTextBlock TextBlock { get; }
        public NonRecordingDrawTransaction Transaction { get; }
        public Size AvailableSize { get; }

        public Harness(TextBlockCorpusId id, bool stableFootprint, bool legacy = false)
        {
            Entry = TextBlockCorpus.Get(id);
            Runtime = new GraphTestRuntime(new Rectangle(0, 0, 640, 360));
            Desktop = new MGDesktop(Runtime);
            Transaction = new NonRecordingDrawTransaction(Runtime, DrawSettings.Default);
            MGWindow window = new(Desktop, 0, 0, Entry.WindowWidth, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
            TextBlock = new CountingTextBlock(window, Entry.Text)
            {
                WrapText = Entry.WrapText,
                HasStableTextFootprint = stableFootprint,
                ForceLegacyTextPath = legacy
            };
            window.SetContent(TextBlock);
            Desktop.Windows.Add(window);
            AvailableSize = new Size(Entry.WindowWidth, 200);
            Frame();
            Frame();
        }

        public void Frame()
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(Elapsed), TimeSpan.FromMilliseconds(16), default, default));
            Desktop.Update();
            Desktop.Draw(Transaction);
            Elapsed += 16;
        }

        public ElementDrawArgs CreateDrawArgs()
            => new(new DrawBaseArgs(TimeSpan.Zero, Transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
    }

    /// <summary>The text of an entry and the one it alternates with, as spans over arrays made once.</summary>
    private static (char[] First, char[] Second) Alternation(TextBlockCorpusEntry entry, bool sameSize)
        => (entry.Text.ToCharArray(), (sameSize ? entry.SameLengthText : entry.DifferentLengthText).ToCharArray());

    public static IEnumerable<object[]> MeasureCases() => new[]
    {
        new object[] { TextBlockCorpusId.ShortNumber, false },
        new object[] { TextBlockCorpusId.ShortNumber, true },
        new object[] { TextBlockCorpusId.OneLineSentence, false },
        new object[] { TextBlockCorpusId.MultiLine, false },
        new object[] { TextBlockCorpusId.MultiLine, true },
        new object[] { TextBlockCorpusId.WrappedSentence, false },
        new object[] { TextBlockCorpusId.WrappedSentence, true },
    };

    /// <summary>The setter (mode RelayoutParent), then the measure and the lines a layout would run: a text that changed every iteration is changed, measured and cut in lines.</summary>
    private static long ChangeMeasureAndWrap(Harness harness, bool sameSize)
    {
        MGTextBlock textBlock = harness.TextBlock;
        (char[] first, char[] second) = Alternation(harness.Entry, sameSize);
        int index = 0;
        Size available = harness.AvailableSize;

        void Step()
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second);
            Thickness measured = textBlock.MeasureSelfOverride(available, out _);
            if (measured.Left < 0)
            {
                throw new InvalidOperationException();
            }

            textBlock.UpdateLines();
        }

        for (int i = 0; i < WarmupIterations; i++)
        {
            Step();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            Step();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Theory]
    [MemberData(nameof(MeasureCases))]
    public void TheMeasureAndTheLines_AfterAChangeOfPlainText_AllocateNothing(TextBlockCorpusId id, bool sameSize)
    {
        Harness harness = new(id, false);

        long allocated = ChangeMeasureAndWrap(harness, sameSize);

        Assert.Equal(0, allocated);
        //  The measure and the lines did their work on the last text.
        Assert.True(harness.TextBlock.Lines.Count >= 1);
    }

    [Fact]
    public void TheMeasureAndTheLines_OfALegacyPlainText_Allocate()
    {
        //  Counter-test: the same loop on the legacy pipeline (runs and MGTextLine objects) is far from zero.
        Harness harness = new(TextBlockCorpusId.WrappedSentence, false, legacy: true);

        long allocated = ChangeMeasureAndWrap(harness, false);

        Output.WriteLine($"legacy pipeline: {allocated} B over {MeasuredIterations} iterations ({allocated / MeasuredIterations} B per change, measure and lines)");
        Assert.True(allocated > 0, $"allocated {allocated} bytes");
    }

    public static IEnumerable<object[]> LocalCases() => new[]
    {
        new object[] { TextBlockCorpusId.ShortNumber, MGTextInvalidationMode.ContentOnly },
        new object[] { TextBlockCorpusId.ShortNumber, MGTextInvalidationMode.ReflowLocal },
        new object[] { TextBlockCorpusId.OneLineSentence, MGTextInvalidationMode.ContentOnly },
        new object[] { TextBlockCorpusId.MultiLine, MGTextInvalidationMode.ReflowLocal },
        new object[] { TextBlockCorpusId.WrappedSentence, MGTextInvalidationMode.ContentOnly },
        new object[] { TextBlockCorpusId.WrappedSentence, MGTextInvalidationMode.ReflowLocal },
    };

    private static long LocalChange(Harness harness, MGTextInvalidationMode mode)
    {
        MGTextBlock textBlock = harness.TextBlock;
        (char[] first, char[] second) = Alternation(harness.Entry, true);
        int index = 0;

        for (int i = 0; i < WarmupIterations; i++)
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second, mode);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            textBlock.SetText((index++ & 1) == 0 ? first : second, mode);
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Theory]
    [MemberData(nameof(LocalCases))]
    public void ALocalChangeOfPlainText_OnAStableFootprint_AllocatesNothing_AndKeepsTheParentLayout(TextBlockCorpusId id, MGTextInvalidationMode mode)
    {
        Harness harness = new(id, true);
        //  There is a cached measurement to compare against, as after any layout: the local branch re-measures each of them.
        Assert.NotNull(harness.TextBlock.Lines);
        harness.TextBlock.MeasureSelfOverride(harness.AvailableSize, out _);
        harness.TextBlock.ResetCount();

        long allocated = LocalChange(harness, mode);

        Assert.Equal(0, allocated);
        //  The local branch was taken: the lines were computed in the setter (the last text is in them) and the parent layout was not requested.
        Assert.Equal(0, harness.TextBlock.LayoutChangedCallCount);
        string lastText = harness.TextBlock.Text;
        Assert.Equal(lastText.Replace("\n", ""), string.Concat(harness.TextBlock.Lines.SelectMany(x => x.Runs.OfType<MGTextRunText>()).Select(x => x.Text)));
    }

    [Fact]
    public void ALocalChangeOfLegacyPlainText_Allocates()
    {
        //  Counter-test: the same local change on the legacy pipeline.
        Harness harness = new(TextBlockCorpusId.ShortNumber, true, legacy: true);
        harness.TextBlock.MeasureSelfOverride(harness.AvailableSize, out _);

        long allocated = LocalChange(harness, MGTextInvalidationMode.ContentOnly);

        Output.WriteLine($"legacy pipeline: {allocated} B over {MeasuredIterations} iterations ({allocated / MeasuredIterations} B per local change)");
        Assert.True(allocated > 0, $"allocated {allocated} bytes");
    }

    [Fact]
    public void ALocalChange_ThatChangesTheSize_AsksForTheParentLayout()
    {
        //  The other side of the local branch: a text of another size is not kept local (so the local zero test above is not vacuous).
        Harness harness = new(TextBlockCorpusId.ShortNumber, true);
        harness.TextBlock.ResetCount();

        harness.TextBlock.SetText("a much longer text now".AsSpan(), MGTextInvalidationMode.ContentOnly);

        Assert.True(harness.TextBlock.LayoutChangedCallCount > 0);
    }
}
