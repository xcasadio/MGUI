using System.Security.Cryptography;
using System.Text;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>
/// T5.6 (decision D17): the callers of <c>FillPolygon</c> that built their vertices with LINQ at each draw (<see cref="MGRatingControl"/>,
/// <see cref="MGHighlightBorderBrush"/>, <see cref="MGDockedBorderBrush"/>) give them from a buffer that is kept, and must send the same vertices in the
/// same order, and push the same partial clips (<see cref="MGRatingControl"/> computes the bounds of its shape for them). The polygon calls and the rectangle
/// clips of each scene are written by an <see cref="OrderedRecordingDrawTransaction"/>, which copies the vertices at the call, and the SHA-256 of those lines
/// was frozen on the code that used LINQ. None of the frozen journals of the desktop frame and of the control families reaches these three callers, and the tests
/// that draw the highlight and the docked brushes elsewhere do not compare their polygons: these are the tests that do.
/// </summary>
public class FillPolygonCallerJournalTests
{
    private static readonly Rectangle Surface = new(0, 0, 640, 360);

    private static bool IsPolygonLine(string line)
        => line.StartsWith("FillPolygon/3 ", StringComparison.Ordinal)
        || line.StartsWith("StrokeAndFillPolygon/5 ", StringComparison.Ordinal);

    /// <summary>The polygon lines, and the rectangle clips that are pushed right before one (the partial fill of a rating item): the other clips of the frame belong
    /// to other controls and are not what this test freezes.</summary>
    private static (string Hash, int Polygons, int Clips) Fingerprint(IReadOnlyList<string> lines)
    {
        List<string> kept = new();
        int polygons = 0;
        int clips = 0;
        for (int i = 0; i < lines.Count; i++)
        {
            if (IsPolygonLine(lines[i]))
            {
                kept.Add(lines[i]);
                polygons++;
            }
            else if (polygons > 0 && lines[i].StartsWith("PushRectangleClip/2 ", StringComparison.Ordinal) && i + 1 < lines.Count && IsPolygonLine(lines[i + 1]))
            {
                kept.Add(lines[i]);
                clips++;
            }
        }

        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join((char)10, kept))))[..16];
        return (hash, polygons, clips);
    }

    private static ElementDrawArgs Args(OrderedRecordingDrawTransaction transaction)
        => new(new DrawBaseArgs(TimeSpan.Zero, transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);

    private static OrderedRecordingDrawTransaction NewTransaction()
        => new(new GraphTestRuntime(Surface), DrawSettings.Default);

    [Fact]
    public void TheDockedBorderBrush_FillsTheSameCornerTriangles()
    {
        OrderedRecordingDrawTransaction transaction = NewTransaction();
        MGDockedBorderBrush brush = new(Color.Red, Color.Green, Color.Blue, Color.White);

        brush.Draw(Args(transaction), null, new Rectangle(10, 20, 80, 60), new Thickness(4, 6, 8, 10));
        brush.Draw(Args(transaction), null, new Rectangle(0, 0, 50, 50), new Thickness(3));

        (string hash, int polygons, int clips) = Fingerprint(transaction.Lines.ToList());
        Assert.Equal(16, polygons);
        Assert.Equal(0, clips);
        Assert.Equal("142C9AA03F0BE6B8", hash);
    }

    [Fact]
    public void TheHighlightBorderBrush_Progress_FillsTheSamePolygons()
    {
        OrderedRecordingDrawTransaction transaction = NewTransaction();
        foreach (HighlightFlowDirection direction in new[] { HighlightFlowDirection.Clockwise, HighlightFlowDirection.CounterClockwise })
        {
            foreach (double progress in new[] { 0.05, 0.25, 0.4, 0.55, 0.8, 0.95 })
            {
                MGHighlightBorderBrush brush = new(null, Color.Red, HighlightAnimation.Progress) { ProgressFlowDirection = direction, AnimationProgress = progress };
                brush.Draw(Args(transaction), null, new Rectangle(10, 20, 100, 60), new Thickness(4, 6, 8, 10));
            }
        }

        (string hash, int polygons, int clips) = Fingerprint(transaction.Lines.ToList());
        Assert.True(polygons > 0);
        Assert.Equal(0, clips);
        Assert.Equal("312B20A23D37E5DA", hash);
    }

    private static List<string> DrawRatingJournal(RatingItemShape shape, Orientation orientation, float value)
    {
        GraphTestRuntime runtime = new(Surface);
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 20, 30, 300, 220) { WindowStyle = WindowStyle.None };
        MGRatingControl rating = new(window, 0, 4, 24, false, null, shape) { Orientation = orientation };
        rating.SetValue(value);
        window.SetContent(rating);
        desktop.Windows.Add(window);
        for (int i = 1; i <= 2; i++)
        {
            runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * i), TimeSpan.FromMilliseconds(16), default, new KeyboardState()));
            desktop.Update();
        }

        OrderedRecordingDrawTransaction transaction = new(runtime, DrawSettings.Default);
        desktop.Draw(transaction);
        return transaction.Lines.ToList();
    }

    [Theory]
    [InlineData(RatingItemShape.Star, Orientation.Horizontal, 2.0f, "F01AC16D00F48BE6")]
    [InlineData(RatingItemShape.Star, Orientation.Horizontal, 2.4f, "C72DADEDB6B61AD8")]
    [InlineData(RatingItemShape.Star, Orientation.Vertical, 1.7f, "29523A1C3600D925")]
    [InlineData(RatingItemShape.Diamond, Orientation.Horizontal, 3.25f, "D0AB8A50C16EBB5D")]
    [InlineData(RatingItemShape.Diamond, Orientation.Vertical, 0.5f, "6850BA344FC6C94B")]
    public void TheRatingControl_FillsTheSamePolygons_AndPushesTheSamePartialClips(RatingItemShape shape, Orientation orientation, float value, string expectedHash)
    {
        (string hash, int polygons, int clips) = Fingerprint(DrawRatingJournal(shape, orientation, value));

        //  Four items drawn unfilled, plus the filled part of the items that the value reaches, and one partial clip when an item is partly filled.
        Assert.True(polygons >= 4);
        Assert.Equal(value % 1 == 0 ? 0 : 1, clips);
        Assert.Equal(expectedHash, hash);
    }
}
