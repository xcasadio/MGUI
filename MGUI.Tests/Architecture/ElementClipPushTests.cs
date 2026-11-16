using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>
/// ADR-0035 (decision D16): the clips of an element that are rectangles are pushed with <see cref="IUIRenderContext.PushRectangleClip"/>, whatever their bounds, and no
/// <see cref="ClipDefinition"/> is made for them; rounded and geometry clips keep <see cref="IUIRenderContext.PushClipTemporary"/>. The frozen journals of the desktop
/// frame and of the control families prove that the same clips are pushed with the same bounds, in the same order (see <see cref="ElementClipJournal"/>); these tests
/// pin the rule itself.
/// </summary>
public class ElementClipPushTests
{
    private static readonly Rectangle Surface = new(0, 0, 640, 360);

    [Fact]
    public void TheNormalFormTurnsTheRectangleClipsOfAnElementIntoPushRectangleClipLines()
    {
        string[] before =
        {
            "SetDrawSettings/1 settings(t=I,Solid,Deferred,AlphaBlend,PointClamp,None,effect=null,ref=0,read=255,write=255)",
            "PushClipTemporary/1 scope=3 clip(Rectangle,[10,20,30,40],radius=(0,0,0,0),intersect=True,pref=Default,fallback=False,name=MGBorder.Self) bounds=[10,20,30,40]",
            "PushClipTemporary/1 scope=4 clip(Rectangle,[1,2,3,4],radius=(0,0,0,0),intersect=True,pref=Default,fallback=False,name=MGScrollViewer.Viewport) bounds=[1,2,3,4]",
            "PushClipTemporary/1 scope=5 clip(Rectangle,[5,6,7,8],radius=(0,0,0,0),intersect=True,pref=Default,fallback=False,name=MGTextBox.Contents) bounds=[5,6,7,8]",
            "DisposeClip scope=5 restored=[1,2,3,4]"
        };

        IReadOnlyList<string> after = ElementClipJournal.Normalise(before);

        Assert.Equal(before.Length, after.Count);
        Assert.Equal(before[0], after[0]);
        Assert.Equal("PushRectangleClip/2 scope=3 [10,20,30,40] intersect=True bounds=[10,20,30,40]", after[1]);
        Assert.Equal("PushRectangleClip/2 scope=4 [1,2,3,4] intersect=True bounds=[1,2,3,4]", after[2]);
        Assert.Equal("PushRectangleClip/2 scope=5 [5,6,7,8] intersect=True bounds=[5,6,7,8]", after[3]);
        Assert.Equal(before[4], after[4]);
    }

    [Fact]
    public void TheNormalFormKeepsEveryOtherClipAsItIs()
    {
        string[] kept =
        {
            //  The swatch clip of MGGridColorPicker is pushed by the control itself, as a definition, and stays one.
            "PushClipTemporary/1 scope=1 clip(Rectangle,[1,2,3,4],radius=(0,0,0,0),intersect=True,pref=Default,fallback=False,name=MGGridColorPicker.Swatches) bounds=[1,2,3,4]",
            "PushClipTemporary/1 scope=2 clip(RoundedRectangle,[1,2,3,4],radius=(4,4,4,4),geometry=12v/30i,intersect=True,pref=Default,fallback=True,name=MGBorder.Contents) bounds=[1,2,3,4]",
            "PushClipTemporary/1 scope=3 clip(ArbitraryGeometry,[1,2,3,4],radius=(0,0,0,0),geometry=3v/3i,intersect=True,pref=Default,fallback=False,name=MGPolygon.Self) bounds=[1,2,3,4]",
            "PushClipTemporary/1 scope=4 clip(Rectangle,[1,2,3,4],radius=(0,0,0,0),intersect=True,pref=Default,fallback=False,name=null) bounds=[1,2,3,4]",
            "PushRectangleClip/2 scope=5 [1,2,3,4] intersect=True bounds=[1,2,3,4]"
        };

        Assert.Equal(kept, ElementClipJournal.Normalise(kept));
    }

    /// <summary>A rectangle clip and the clip definition of the same rectangle resolve to the same scissor: the same bounds, intersecting with the clip in force. This is
    /// what makes <c>PushRectangleClip(bounds, true)</c> a replacement for <c>PushClipTemporary(ClipDefinition.Rectangle(bounds, true))</c>.</summary>
    [Fact]
    public void ARectangleDefinitionResolvesToTheScissorOfPushRectangleClip()
    {
        Rectangle bounds = new(12, 34, 56, 78);

        ClipResolveResult resolution = ClipStrategyResolver.Resolve(ClipDefinition.Rectangle(bounds, true), ClipBackendCapabilities.Default);

        Assert.Equal(ClipStrategy.Scissor, resolution.Strategy);
        Assert.False(resolution.UsedFallback);
        Assert.Equal(bounds, resolution.Effective.Shape.Bounds);
        Assert.True(resolution.Effective.IntersectWithCurrentClip);
    }

    private static List<string> DrawJournal(Action<MGWindow> fill)
    {
        GraphTestRuntime runtime = new(Surface);
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 20, 30, 300, 220) { WindowStyle = WindowStyle.None };
        fill(window);
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

    [Fact]
    public void AScrollViewerPushesItsViewportAsARectangleAndNeverAsADefinition()
    {
        MGScrollViewer scrollViewer = null;
        List<string> journal = DrawJournal(window =>
        {
            scrollViewer = new(window) { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            scrollViewer.SetContent(new MGBorder(window) { PreferredWidth = 400, PreferredHeight = 400 });
            window.SetContent(scrollViewer);
        });

        Rectangle viewport = scrollViewer.ContentViewport.GetTranslated(scrollViewer.Origin);
        string viewportLine = $"[{viewport.X},{viewport.Y},{viewport.Width},{viewport.Height}]";
        Assert.Contains(journal, x => x.StartsWith("PushRectangleClip/2 ") && x.Contains(viewportLine + " intersect=True"));
        Assert.DoesNotContain(journal, x => x.StartsWith("PushClipTemporary/1 ") && x.Contains("clip(Rectangle,"));
    }

    [Fact]
    public void ARoundedContentsClipKeepsPushClipTemporary()
    {
        List<string> journal = DrawJournal(window =>
        {
            MGBorder border = new(window)
            {
                BorderThickness = new Thickness(2),
                BorderBrush = Color.Black.AsFillBrush().AsUniformBorderBrush(),
                CornerRadius = new MGCornerRadius(20),
                ClipToBounds = true,
                PreferredWidth = 120,
                PreferredHeight = 80
            };
            border.SetContent(new MGBorder(window) { PreferredWidth = 200, PreferredHeight = 200 });
            window.SetContent(border);
        });

        Assert.True(journal.Any(x => x.StartsWith("PushClipTemporary/1 ") && x.Contains("clip(RoundedRectangle,") && x.Contains("name=Border.Contents")),
            string.Join(Environment.NewLine, journal.Where(x => x.Contains("Clip"))));
    }

    /// <summary>The clip of every draw is pushed and released: a rectangle pushed by an element is released like any other clip.</summary>
    [Fact]
    public void EveryClipPushedByAnElementIsReleased()
    {
        List<string> journal = DrawJournal(window =>
        {
            MGScrollViewer scrollViewer = new(window) { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
            scrollViewer.SetContent(new MGBorder(window) { PreferredWidth = 400, PreferredHeight = 400 });
            window.SetContent(scrollViewer);
        });

        int pushes = journal.Count(x => x.StartsWith("PushRectangleClip/2 ") || x.StartsWith("PushClipTemporary/1 "));
        int releases = journal.Count(x => x.StartsWith("DisposeClip "));
        Assert.True(pushes > 0);
        Assert.Equal(pushes, releases);
    }
}
