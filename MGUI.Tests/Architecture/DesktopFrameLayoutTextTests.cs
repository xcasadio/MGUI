using MGUI.Core.UI;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit.Abstractions;
using Fixture = MGUI.Tests.Architecture.DesktopFrameAllocationMeasurements.Fixture;
using Harness = MGUI.Tests.Architecture.DesktopFrameAllocationMeasurements.Harness;

namespace MGUI.Tests.Architecture;

/// <summary>The texts of the layout proofs: 50 texts of 50 different lengths (5 to 84 characters, cut from one sentence of words), so that the text block of the
/// fixtures changes size and wraps on a different number of lines from one frame to the next, and the layout of its parents changes with it.</summary>
internal static class LayoutTexts
{
    public const int Count = 50;

    private const string Source = "alpha beta gamma delta epsilon zeta eta theta iota kappa lambda mu nu xi omicron pi rho sigma tau upsilon phi chi psi omega";

    private static readonly string[] Texts = Create();

    private static string[] Create()
    {
        string[] texts = new string[Count];
        for (int i = 0; i < Count; i++)
        {
            texts[i] = Source.Substring(0, 5 + (i * 13) % 80);
        }

        return texts;
    }

    public static string Get(int index) => Texts[index % Count];
}

/// <summary>
/// T2.1: a desktop frame after a change of the text of the <see cref="MGTextBlock"/> of the <see cref="Fixture.Full"/> fixtures, which lays out the parents of the
/// text block (measure and arrange of the stack panel, the window and, in the other fixtures, the grid and the dock panel).<para/>
/// The characterisation tests freeze, frame by frame, the <see cref="MGElement.LayoutBounds"/> of every element of the fixture, the layout events they raise and the
/// draw journal, written on the code before the allocation-free work and kept after it. The zero tests assert that <c>MGDesktop.Update()</c> then
/// <c>MGDesktop.Draw(IUIDrawTransaction)</c> allocate nothing after such a change.
/// </summary>
public class DesktopFrameLayoutTextTests
{
    private static readonly Point Outside = new(600, 340);

    private readonly ITestOutputHelper Output;

    public DesktopFrameLayoutTextTests(ITestOutputHelper output)
    {
        Output = output;
    }

    private static void TextFrame(Harness harness, int index)
    {
        harness.TextBlock.SetText(LayoutTexts.Get(index).AsSpan());
        harness.Update(Outside, ButtonState.Released);
        harness.Draw();
    }

    //  ---- Characterisation -------------------------------------------------------------------------------------------------------------------

    private static string Rect(Rectangle r) => $"[{r.X},{r.Y},{r.Width},{r.Height}]";

    private static void Collect(MGElement element, string path, List<string> lines)
    {
        lines.Add($"{path} {element.GetType().Name} layout={Rect(element.LayoutBounds)} allocated={Rect(element.AllocatedBounds)}");
        if (element is MGGrid grid)
        {
            for (int r = 0; r < grid.Rows.Count; r++)
            {
                for (int c = 0; c < grid.Columns.Count; c++)
                {
                    lines.Add($"{path} cell[{r},{c}] {(grid.CellBounds.TryGetValue(new GridCell(grid.Rows[r], grid.Columns[c]), out Rectangle cell) ? Rect(cell) : "none")} " +
                              $"column=({grid.Columns[c].Left},{grid.Columns[c].Width}) row=({grid.Rows[r].Top},{grid.Rows[r].Height})");
                }
            }
        }

        IReadOnlyList<MGElement> children = element.GetVisualTreeChildren(true, true);
        for (int i = 0; i < children.Count; i++)
        {
            Collect(children[i], $"{path}.{i}", lines);
        }
    }

    private static void Subscribe(MGElement element, string path, Action<string> log)
    {
        element.OnLayoutUpdated += (s, e) => log($"{path} {element.GetType().Name} layoutUpdated");
        element.OnLayoutBoundsChanged += (s, e) => log($"{path} {element.GetType().Name} boundsChanged {Rect(e.PreviousValue)} -> {Rect(e.NewValue)}");
        IReadOnlyList<MGElement> children = element.GetVisualTreeChildren(true, true);
        for (int i = 0; i < children.Count; i++)
        {
            Subscribe(children[i], $"{path}.{i}", log);
        }
    }

    /// <summary>For each of the 50 texts: the text is set, then <c>Update</c>; the journal holds the layout events of that update and the bounds of every element.</summary>
    private static List<string> LayoutJournal(Fixture fixture)
    {
        Harness harness = Harness.Create(fixture);
        List<string> journal = new();
        int frame = 0;
        Subscribe(harness.Window, "w", line => journal.Add($"f{frame} {line}"));
        for (frame = 0; frame < LayoutTexts.Count; frame++)
        {
            harness.TextBlock.SetText(LayoutTexts.Get(frame).AsSpan());
            harness.Update(Outside, ButtonState.Released);
            Collect(harness.Window, "w", journal);
            harness.Draw();
            journal.Add("--");
        }

        return journal;
    }

    private static List<string> DrawJournal(Fixture fixture)
    {
        Harness harness = Harness.Create(fixture);
        List<string> journal = new();
        for (int frame = 0; frame < LayoutTexts.Count; frame++)
        {
            harness.TextBlock.SetText(LayoutTexts.Get(frame).AsSpan());
            harness.Update(Outside, ButtonState.Released);
            OrderedRecordingDrawTransaction transaction = new(harness.GetRuntime(), DrawSettings.Default);
            harness.Draw(transaction);
            journal.AddRange(transaction.Lines);
            journal.Add("--");
        }

        return journal;
    }

    [Fact]
    public void TheLayoutOfTheFullFixture_AfterEachOfFiftyTexts() => FrozenJournal.Assert("Full layout journal", 580, "365EFA53674E1445", LayoutJournal(Fixture.Full));

    [Fact]
    public void TheLayoutOfTheFullFixtureInAGrid_AfterEachOfFiftyTexts() => FrozenJournal.Assert("FullInGrid layout journal", 962, "844A5144A1858CF8", LayoutJournal(Fixture.FullInGrid));

    [Fact]
    public void TheLayoutOfTheFullFixtureInADockPanel_AfterEachOfFiftyTexts() => FrozenJournal.Assert("FullInDockPanel layout journal", 730, "8F19D06C4E62E78D", LayoutJournal(Fixture.FullInDockPanel));

    [Fact]
    public void TheLayoutOfTheFullFixtureInADockHost_AfterEachOfFiftyTexts() => FrozenJournal.Assert("FullInDockHost layout journal", 1630, "845AF18D5646C839", LayoutJournal(Fixture.FullInDockHost));

    [Fact]
    public void TheDrawJournalOfTheFullFixture_AfterEachOfFiftyTexts() => FrozenJournal.Assert("Full text draw journal", 3421, "41F2D22E04C15451", DrawJournal(Fixture.Full));

    [Fact]
    public void TheDrawJournalOfTheFullFixtureInAGrid_AfterEachOfFiftyTexts() => FrozenJournal.Assert("FullInGrid text draw journal", 3568, "499E2F3D359CB059", DrawJournal(Fixture.FullInGrid));

    [Fact]
    public void TheDrawJournalOfTheFullFixtureInADockPanel_AfterEachOfFiftyTexts() => FrozenJournal.Assert("FullInDockPanel text draw journal", 3521, "12367A53E24DBF44", DrawJournal(Fixture.FullInDockPanel));

    [Fact]
    public void TheDrawJournalOfTheFullFixtureInADockHost_AfterEachOfFiftyTexts() => FrozenJournal.Assert("FullInDockHost text draw journal", 5771, "B5F7B15D0E65D0AB", DrawJournal(Fixture.FullInDockHost));

    [Fact]
    public void ThePlainTextOfTheFixtureIsTheTextThatWasSet()
    {
        Harness harness = Harness.Create(Fixture.Full);
        for (int i = 0; i < LayoutTexts.Count; i++)
        {
            TextFrame(harness, i);
            Assert.Equal(LayoutTexts.Get(i), harness.TextBlock.Text);
        }
    }


    //  ---- Characterisation of the grid layout beyond the fixtures: Auto, pixel and weighted lengths with size constraints, spans, spacing, grid lines -----------------

    private static MGGrid CreateRichGrid(MGWindow window, bool gridLines, out MGTextBlock[] texts)
    {
        MGGrid grid = new(window);
        grid.AddColumn(GridLength.Auto);
        grid.AddColumn(GridLength.CreateWeightedLength(2));
        grid.AddColumn(new ConstrainedGridLength(GridLength.CreateWeightedLength(1), 30, 60));
        grid.AddColumn(GridLength.CreatePixelLength(50));
        grid.AddColumn(new ConstrainedGridLength(GridLength.Auto, 40, null));
        grid.AddRow(GridLength.Auto);
        grid.AddRow(GridLength.CreateWeightedLength(1));
        grid.AddRow(GridLength.CreatePixelLength(25));
        grid.AddRow(new ConstrainedGridLength(GridLength.CreateWeightedLength(3), null, 40));
        if (gridLines)
        {
            grid.RowSpacing = 3;
            grid.ColumnSpacing = 5;
            grid.GridLineMargin = 1;
            grid.GridLinesVisibility = GridLinesVisibility.All;
        }

        texts = new MGTextBlock[8];
        (int Row, int Column, GridSpan Span)[] cells =
        {
            (0, 0, GridSpan.Default), (0, 1, GridSpan.Default), (0, 2, GridSpan.Default), (1, 0, new GridSpan(2, 2)),
            (1, 3, GridSpan.Default), (2, 4, new GridSpan(1, 1, false)), (3, 1, new GridSpan(1, 3)), (3, 4, GridSpan.Default)
        };
        for (int i = 0; i < cells.Length; i++)
        {
            texts[i] = new MGTextBlock(window, "cell " + i);
            Assert.True(grid.TryAddChild(cells[i].Row, cells[i].Column, cells[i].Span, texts[i]));
        }

        return grid;
    }

    private static List<string> RichGridJournal()
    {
        List<string> journal = new();
        foreach (bool gridLines in new[] { false, true })
        {
            foreach ((int width, int height, bool sizeToContent) in new[] { (300, 200, false), (420, 260, false), (120, 90, false), (50, 40, false), (300, 200, true) })
            {
                Harness harness = Harness.Create(Fixture.NoWindow);
                MGDesktop desktop = harness.GetDesktop();
                MGWindow window = new(desktop, 0, 0, width, height);
                MGGrid grid = CreateRichGrid(window, gridLines, out MGTextBlock[] texts);
                window.SetContent(grid);
                desktop.Windows.Add(window);
                int frame = 0;
                Subscribe(window, "w", line => journal.Add($"f{frame} {line}"));
                journal.Add($"== gridLines={gridLines} {width}x{height} sizeToContent={sizeToContent}");
                harness.Frame(Outside, ButtonState.Released);
                if (sizeToContent)
                {
                    journal.Add($"size {window.ApplySizeToContent(SizeToContent.WidthAndHeight, 10, 10, 800, 600)}");
                }

                for (frame = 0; frame < 6; frame++)
                {
                    if (frame > 0)
                    {
                        texts[(frame * 3) % texts.Length].SetText(LayoutTexts.Get(frame * 7).AsSpan());
                        texts[(frame * 5) % texts.Length].SetText(LayoutTexts.Get(frame * 11 + 1).AsSpan());
                    }

                    harness.Frame(Outside, ButtonState.Released);
                    Collect(window, "w", journal);
                    journal.Add("--");
                }
            }
        }

        return journal;
    }

    [Fact]
    public void TheLayoutOfAGridWithMixedLengthsSpansAndGridLines_AfterTextChanges()
        => FrozenJournal.Assert("rich grid layout journal", 2806, "DD5522217256FEFA", RichGridJournal());

    //  ---- Zero ---------------------------------------------------------------------------------------------------------------------------------

    /// <summary>The bytes of <c>Update</c> and of <c>Draw</c>, per frame, over <paramref name="frames"/> frames that each set the text of <paramref name="indexOf"/>(frame).</summary>
    private (long Update, long Draw) Measure(Harness harness, int frames, Func<int, int> indexOf)
    {
        long update = 0;
        long draw = 0;
        long start = AllocationWindow.Start();
        for (int i = 0; i < frames; i++)
        {
            harness.TextBlock.SetText(LayoutTexts.Get(indexOf(i)).AsSpan());
            long a = GC.GetAllocatedBytesForCurrentThread();
            harness.Update(Outside, ButtonState.Released);
            long b = GC.GetAllocatedBytesForCurrentThread();
            harness.Draw();
            long c = GC.GetAllocatedBytesForCurrentThread();
            update += b - a;
            draw += c - b;
        }

        return (update, draw);
    }

    public static IEnumerable<object[]> Fixtures()
    {
        yield return new object[] { Fixture.Full };
        yield return new object[] { Fixture.FullInGrid };
        yield return new object[] { Fixture.FullInDockPanel };
    }

    /// <summary>Warm-up over the 50 texts (several passes), then a loop over texts whose lengths were all seen during the warm-up, in another order.</summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void AFrameAfterATextChange_WithLengthsSeenDuringWarmUp_AllocatesNothing(Fixture fixture)
    {
        Harness harness = Harness.Create(fixture);
        for (int i = 0; i < 4 * LayoutTexts.Count; i++)
        {
            TextFrame(harness, i);
        }

        (long update, long draw) = Measure(harness, 500, i => (i * 7 + 3) % LayoutTexts.Count);
        Output.WriteLine($"{fixture}: Update {update / 500.0} B/frame, Draw {draw / 500.0} B/frame");
        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    /// <summary>One warm-up pass over the 50 texts, then the same 50 texts replayed in the same order.</summary>
    [Theory]
    [MemberData(nameof(Fixtures))]
    public void AFrameAfterATextChange_ReplayingFiftyTextsAfterOnePass_AllocatesNothing(Fixture fixture)
    {
        Harness harness = Harness.Create(fixture);
        for (int i = 0; i < LayoutTexts.Count; i++)
        {
            TextFrame(harness, i);
        }

        (long update, long draw) = Measure(harness, LayoutTexts.Count, i => i);
        Output.WriteLine($"{fixture}: Update {update} B, Draw {draw} B over {LayoutTexts.Count} frames");
        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    /// <summary>The dock host fixture is not required to be at zero (the tab group makes a fill brush while it lays out, MGDockTabGroup.SyncHeaderVisuals, and its draw allocates now and then): the test only writes its measure.</summary>
    [Fact]
    public void AFrameAfterATextChange_OnTheDockHost_IsMeasured()
    {
        Harness harness = Harness.Create(Fixture.FullInDockHost);
        for (int i = 0; i < 4 * LayoutTexts.Count; i++)
        {
            TextFrame(harness, i);
        }

        (long update, long draw) = Measure(harness, 500, i => (i * 7 + 3) % LayoutTexts.Count);
        Output.WriteLine($"FullInDockHost: Update {update / 500.0} B/frame, Draw {draw / 500.0} B/frame");
    }
}
