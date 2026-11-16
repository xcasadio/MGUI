using MGUI.Core.UI;
using MGUI.Tests.Architecture;

namespace MGUI.Tests.Text;

/// <summary>
/// Zero allocation of the reveal that <see cref="MGTextBlock.TextCharactersPerSecond"/> drives (the animation engine): the frames of a running reveal, and the frames that restart it
/// (a text change, a change of speed, a write of the progress), each counted as the setter, <see cref="MGDesktop.Update"/> and the draw of the desktop, after a warm-up, with two
/// strings made once that alternate. The restart reuses the run of the text block instead of building a new one.
/// </summary>
[Collection(AllocationMeasurementsCollection.Name)]
public class TextRevealRestartAllocationTests
{
    private const string Sentence = "The quick brown fox jumps over the lazy dog";
    private const string LongerSentence = "The quick brown fox jumps over the lazy dog again";

    private static readonly string[] Texts = { Sentence, LongerSentence };

    private static TextBlockFixture CreateFixture(bool allowsInlineFormatting, double charactersPerSecond)
    {
        TextBlockFixture fixture = new(TextBlockCorpusId.OneLineSentence, allowsInlineFormatting);
        fixture.TextBlock.TextCharactersPerSecond = charactersPerSecond;
        fixture.TextBlock.Text = Texts[1];
        fixture.Frame();
        fixture.Frame();
        Assert.True(fixture.TextBlock.TextProgress > 0.0 && fixture.TextBlock.TextProgress < 1.0, $"the reveal is not running (progress {fixture.TextBlock.TextProgress})");
        return fixture;
    }

    private static FrameBytes Measure(TextBlockFixture fixture, Action<int> prepare)
    {
        long callsBefore = fixture.Transaction.CallCount;
        FrameBytes bytes = FrameAllocationMeter.Measure(prepare, fixture.Update, () => fixture.Desktop.Draw(fixture.Transaction));
        Assert.True(fixture.Transaction.CallCount > callsBefore, "the measured frames drew nothing");
        return bytes;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheFramesOfARunningReveal_AllocateNothing(bool allowsInlineFormatting)
    {
        //  1 character per second: 49 characters take 49 s, far longer than the 1000 frames (16 s) of the measure.
        TextBlockFixture fixture = CreateFixture(allowsInlineFormatting, 1);

        FrameBytes bytes = Measure(fixture, null);

        Assert.Equal(0, bytes.UpdateTotal);
        Assert.Equal(0, bytes.DrawTotal);
        Assert.True(fixture.TextBlock.TextProgress > 0.0 && fixture.TextBlock.TextProgress < 1.0, "the reveal ended during the measure");
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 100)]
    [InlineData(true, 1)]
    [InlineData(true, 100)]
    public void TheFramesThatChangeTheText_AllocateNothing_SetterUpdateAndDraw(bool allowsInlineFormatting, int framesBetweenChanges)
    {
        TextBlockFixture fixture = CreateFixture(allowsInlineFormatting, 20);
        MGTextBlock textBlock = fixture.TextBlock;
        int changes = 0;

        FrameBytes bytes = Measure(fixture, frame =>
        {
            if (frame % framesBetweenChanges == 0)
            {
                textBlock.Text = Texts[(frame / framesBetweenChanges) & 1];
                changes++;
            }
        });

        Assert.True(changes >= FrameAllocationMeter.MeasuredFrames / framesBetweenChanges, $"{changes} text changes");
        Assert.Equal(0, bytes.PrepareTotal);
        Assert.Equal(0, bytes.UpdateTotal);
        Assert.Equal(0, bytes.DrawTotal);
        Assert.True(textBlock.TextProgress.HasValue);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheFramesThatChangeTheSpeed_OrWriteTheProgress_AllocateNothing(bool allowsInlineFormatting)
    {
        TextBlockFixture fixture = CreateFixture(allowsInlineFormatting, 5);
        MGTextBlock textBlock = fixture.TextBlock;

        FrameBytes bytes = Measure(fixture, frame =>
        {
            //  A speed change keeps the progress and retargets the run, a write of 0 replays the reveal, a write of a fraction continues it from there.
            switch (frame % 3)
            {
                case 0:
                    textBlock.TextCharactersPerSecond = (frame & 1) == 0 ? 5 : 7;
                    break;
                case 1:
                    textBlock.TextProgress = 0.0;
                    break;
                default:
                    textBlock.TextProgress = 0.25;
                    break;
            }
        });

        Assert.Equal(0, bytes.PrepareTotal);
        Assert.Equal(0, bytes.UpdateTotal);
        Assert.Equal(0, bytes.DrawTotal);
        Assert.True(textBlock.Animations.IsAnimating(MGUI.Core.UI.Animation.Targets.UIBuiltInAnimationTargets.Paths.TextBlockTextProgress));
    }
}
