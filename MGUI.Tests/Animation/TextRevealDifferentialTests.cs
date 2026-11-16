using System.IO;
using System.IO.Compression;
using System.Text;

namespace MGUI.Tests.Animation;

/// <summary>
/// The reveal driven by <see cref="MGUI.Core.UI.MGTextBlock.TextCharactersPerSecond"/>, frame by frame, against the journal frozen in <see cref="TextRevealGolden"/>: the progress and the
/// revealed characters at every frame, before, at and after a text change, a speed change, a hand write of the progress, a detach, a pause of the clock, a change made from the
/// callback of another animation, with the notifications of the text block and the events of the run, in order.
/// </summary>
public class TextRevealDifferentialTests
{
    private static List<string> Golden()
    {
        byte[] compressed = Convert.FromBase64String(TextRevealGolden.GzipBase64);
        using GZipStream gzip = new(new MemoryStream(compressed), CompressionMode.Decompress);
        using StreamReader reader = new(gzip, Encoding.UTF8);
        return reader.ReadToEnd().Split('\n').ToList();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheScriptedReveal_IsFrameByFrameTheFrozenJournal(bool allowsInlineFormatting)
    {
        List<string> expected = Golden();
        List<string> actual = TextRevealScenario.Run(allowsInlineFormatting);

        for (int i = 0; i < Math.Min(expected.Count, actual.Count); i++)
        {
            Assert.True(expected[i] == actual[i], $"line {i}: expected \"{expected[i]}\", got \"{actual[i]}\"");
        }

        Assert.Equal(expected.Count, actual.Count);
        Assert.True(actual.Count > 800, $"{actual.Count} lines");
    }
}
