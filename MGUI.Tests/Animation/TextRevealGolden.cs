namespace MGUI.Tests.Animation;

/// <summary>
/// The journal of <see cref="TextRevealScenario"/> (897 lines), GZip then Base64, taken from the code as it was before the reveal run was reused from one restart to the next
/// (a new run for each resynchronisation). A difference in any frame of the reveal, in the notifications or in the events of the run is a change of behaviour.
/// </summary>
internal static class TextRevealGolden
{
    public const string GzipBase64 =
        "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" +        "";
}
