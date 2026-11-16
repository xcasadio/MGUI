using System.IO;
using System.IO.Compression;
using System.Text;

namespace MGUI.Tests.Input;

/// <summary>
/// The keyboard events delivered to four handlers over a scripted session (<see cref="KeyboardEventsScenario"/>: single keys, a key held for several repeat periods, two keys at once, modifiers,
/// native text input, a focus that changes while a key is held, a held key whose repeat goes to two handlers of which one marks it handled, the release of a key in the frame of a native text
/// input of the same key, a key handled by A and pressed again after the focus moved to B), frame by frame, against the journal frozen in <see cref="KeyboardEventsGolden"/> before the events
/// were reused: the same events, with the same values, the same streams and owners, to the same handlers, with the same handled state at the time of each one.
/// </summary>
public class KeyboardEventsReplayTests
{
    private static List<string> Golden()
    {
        byte[] compressed = Convert.FromBase64String(KeyboardEventsGolden.GzipBase64);
        using GZipStream gzip = new(new MemoryStream(compressed), CompressionMode.Decompress);
        using StreamReader reader = new(gzip, Encoding.UTF8);
        return reader.ReadToEnd().Split('\n').ToList();
    }

    [Fact]
    public void TheScriptedSession_IsFrameByFrameTheFrozenJournal()
    {
        List<string> expected = Golden();
        List<string> actual = KeyboardEventsScenario.Run();

        for (int i = 0; i < Math.Min(expected.Count, actual.Count); i++)
        {
            Assert.True(expected[i] == actual[i], $"line {i}: expected \"{expected[i]}\", got \"{actual[i]}\"");
        }

        Assert.Equal(expected.Count, actual.Count);
        Assert.True(actual.Count > 1000, $"{actual.Count} lines");
    }
}
