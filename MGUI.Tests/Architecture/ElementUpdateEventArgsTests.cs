using MGUI.Core.UI;
using MGUI.Tests.Graph;
using System.Collections.Generic;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Architecture;

/// <summary>
/// <see cref="MGElement.Update"/> creates its <see cref="MGElement.ElementUpdateEventArgs"/> at the first of
/// <see cref="MGElement.OnBeginUpdate"/>, <see cref="MGElement.OnBeginUpdateContents"/>, <see cref="MGElement.OnEndUpdateContents"/> and
/// <see cref="MGElement.OnEndUpdate"/> that has a subscriber, and shares it with the later ones of the same update, instead of creating it
/// every frame for every element. Subscribers still receive the element and its update arguments.
/// </summary>
public class ElementUpdateEventArgsTests
{
    [Fact]
    public void AllFourUpdateEvents_ReceiveTheElementAndItsUpdateArgs_InOneSharedInstancePerUpdate()
    {
        (MGDesktop desktop, MGSlider element) = CreateElement();
        List<(string Event, MGElement.ElementUpdateEventArgs Args)> calls = new();
        element.OnBeginUpdate += (_, e) => calls.Add(("OnBeginUpdate", e));
        element.OnBeginUpdateContents += (_, e) => calls.Add(("OnBeginUpdateContents", e));
        element.OnEndUpdateContents += (_, e) => calls.Add(("OnEndUpdateContents", e));
        element.OnEndUpdate += (_, e) => calls.Add(("OnEndUpdate", e));

        desktop.Update();

        Assert.Equal(new[] { "OnBeginUpdate", "OnBeginUpdateContents", "OnEndUpdateContents", "OnEndUpdate" }, calls.ConvertAll(x => x.Event));
        MGElement.ElementUpdateEventArgs first = calls[0].Args;
        Assert.Same(element, first.Element);
        Assert.Equal(element.ActualLayoutBounds, first.UA.ActualLayoutBounds);
        Assert.All(calls, x => Assert.Same(first, x.Args));

        calls.Clear();
        desktop.Update();

        Assert.Equal(4, calls.Count);
        Assert.NotSame(first, calls[0].Args);
    }

    [Fact]
    public void ASubscriberOfTheLastUpdateEventOnly_StillReceivesTheElementAndItsUpdateArgs()
    {
        (MGDesktop desktop, MGSlider element) = CreateElement();
        MGElement.ElementUpdateEventArgs received = null;
        element.OnEndUpdate += (_, e) => received = e;

        desktop.Update();

        Assert.NotNull(received);
        Assert.Same(element, received.Element);
        Assert.Equal(element.ActualLayoutBounds, received.UA.ActualLayoutBounds);
    }

    [Fact]
    public void ASubscriberAddedDuringTheUpdate_ReceivesTheSameInstanceInThatUpdate()
    {
        (MGDesktop desktop, MGSlider element) = CreateElement();
        MGElement.ElementUpdateEventArgs atBegin = null;
        MGElement.ElementUpdateEventArgs atEnd = null;
        element.OnBeginUpdate += (_, e) =>
        {
            if (atBegin == null)
            {
                atBegin = e;
                element.OnEndUpdate += (_, end) => atEnd ??= end;
            }
        };

        desktop.Update();

        Assert.NotNull(atBegin);
        Assert.Same(atBegin, atEnd);
    }

    private static (MGDesktop Desktop, MGSlider Element) CreateElement()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 200);
        desktop.Windows.Add(window);

        MGSlider element = new(window, 0, 100, 50);
        window.SetContent(element);
        desktop.Update();
        return (desktop, element);
    }
}
