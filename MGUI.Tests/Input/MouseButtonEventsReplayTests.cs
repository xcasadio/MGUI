using System.IO;
using System.IO.Compression;
using System.Text;

namespace MGUI.Tests.Input;

/// <summary>
/// The mouse button events delivered to six handlers over a scripted session (<see cref="MouseButtonEventsScenario"/>: presses, releases, clicks, double, triple and fourth clicks of the three buttons,
/// a click of another button between two clicks, a press while another button is held, drags by the three drag start conditions, releases outside, a click handled by the parent before the child,
/// a handler that always handles, the double click of a button on an element and then on another one), frame by frame, against the journal frozen in <see cref="MouseButtonEventsGolden"/>
/// before the events were reused: the same events, with the same values, to the same handlers, with the same handled state at the time of each one.
/// </summary>
public class MouseButtonEventsReplayTests
{
    private static List<string> Golden()
    {
        byte[] compressed = Convert.FromBase64String(MouseButtonEventsGolden.GzipBase64);
        using GZipStream gzip = new(new MemoryStream(compressed), CompressionMode.Decompress);
        using StreamReader reader = new(gzip, Encoding.UTF8);
        return reader.ReadToEnd().Split('\n').ToList();
    }

    [Fact]
    public void TheScriptedSession_IsFrameByFrameTheFrozenJournal()
    {
        List<string> expected = Golden();
        List<string> actual = MouseButtonEventsScenario.Run();

        for (int i = 0; i < Math.Min(expected.Count, actual.Count); i++)
        {
            Assert.True(expected[i] == actual[i], $"line {i}: expected \"{expected[i]}\", got \"{actual[i]}\"");
        }

        Assert.Equal(expected.Count, actual.Count);
        Assert.True(actual.Count > 2000, $"{actual.Count} lines");
    }
}
