using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Harness = MGUI.Tests.Architecture.DesktopFrameAllocationMeasurements.Harness;
using Fixture = MGUI.Tests.Architecture.DesktopFrameAllocationMeasurements.Fixture;

namespace MGUI.Tests.Architecture;

/// <summary>
/// How a content host draws its children: a host that overrides <c>GetChildren()</c> is drawn through its own <c>GetChildren()</c> (set and order of the
/// children drawn), a host that does not draws the children it holds directly, in the same order.
/// </summary>
public class ContentHostDrawContentsTests
{
    private static readonly Point Outside = new(600, 340);

    private sealed class ExtraChildStackPanel : MGStackPanel
    {
        private readonly MGElement Extra;

        public ExtraChildStackPanel(MGWindow window, MGElement extra)
            : base(window, Orientation.Vertical)
        {
            Extra = extra;
        }

        public override IEnumerable<MGElement> GetChildren()
        {
            yield return Extra;
            foreach (MGElement child in Children.Reverse())
            {
                yield return child;
            }
        }
    }

    private sealed class PlainStackPanel : MGStackPanel
    {
        public PlainStackPanel(MGWindow window)
            : base(window, Orientation.Vertical) { }
    }

    private sealed class ExtraChildBorder : MGBorder
    {
        private readonly MGElement Extra;

        public ExtraChildBorder(MGWindow window, MGElement extra)
            : base(window)
        {
            Extra = extra;
        }

        public override IEnumerable<MGElement> GetChildren()
        {
            yield return Extra;
            if (Content != null)
            {
                yield return Content;
            }
        }
    }

    private sealed class PlainBorder : MGBorder
    {
        public PlainBorder(MGWindow window)
            : base(window) { }
    }

    private sealed class ExtraChildWindow : MGWindow
    {
        private readonly MGElement Extra;

        public ExtraChildWindow(MGDesktop desktop)
            : base(desktop, 0, 0, 300, 200)
        {
            Extra = new MGButton(this) { Name = "Extra" };
        }

        public override IEnumerable<MGElement> GetChildren()
        {
            yield return Extra;
            if (Content != null)
            {
                yield return Content;
            }
        }
    }

    private static MGButton Named(MGWindow window, string name, List<string> log)
    {
        MGButton button = new(window) { Name = name };
        button.OnBeginDraw += (s, e) => log.Add(name);
        return button;
    }

    private static void DrawOnce(Harness harness, MGWindow window)
    {
        harness.GetDesktop().Windows.Add(window);
        harness.Frame(Outside, ButtonState.Released);
    }

    private static Harness NewHarness() => Harness.Create(Fixture.NoWindow);

    [Fact]
    public void AMultiHostThatOverridesGetChildren_IsDrawnThroughIt()
    {
        Harness harness = NewHarness();
        List<string> log = new();
        MGWindow window = new(harness.GetDesktop(), 0, 0, 300, 200);
        MGButton extra = Named(window, "X", log);
        ExtraChildStackPanel panel = new(window, extra);
        foreach (string name in new[] { "A", "B", "C" })
        {
            Assert.True(panel.TryAddChild(Named(window, name, log)));
        }

        window.SetContent(panel);
        DrawOnce(harness, window);
        log.Clear();
        harness.Draw();

        Assert.Equal(new[] { "X", "C", "B", "A" }, log.ToArray());
    }

    [Fact]
    public void AMultiHostThatDoesNotOverrideGetChildren_DrawsItsChildrenInOrder()
    {
        Harness harness = NewHarness();
        List<string> log = new();
        MGWindow window = new(harness.GetDesktop(), 0, 0, 300, 200);
        PlainStackPanel panel = new(window);
        foreach (string name in new[] { "A", "B", "C" })
        {
            Assert.True(panel.TryAddChild(Named(window, name, log)));
        }

        window.SetContent(panel);
        DrawOnce(harness, window);
        log.Clear();
        harness.Draw();

        Assert.Equal(new[] { "A", "B", "C" }, log.ToArray());
    }

    [Fact]
    public void ASingleHostThatOverridesGetChildren_IsDrawnThroughIt()
    {
        Harness harness = NewHarness();
        List<string> log = new();
        MGWindow window = new(harness.GetDesktop(), 0, 0, 300, 200);
        ExtraChildBorder border = new(window, Named(window, "X", log));
        border.SetContent(Named(window, "Content", log));
        window.SetContent(border);
        DrawOnce(harness, window);
        log.Clear();
        harness.Draw();

        Assert.Equal(new[] { "X", "Content" }, log.ToArray());
    }

    [Fact]
    public void ASingleHostThatDoesNotOverrideGetChildren_DrawsItsContent()
    {
        Harness harness = NewHarness();
        List<string> log = new();
        MGWindow window = new(harness.GetDesktop(), 0, 0, 300, 200);
        PlainBorder border = new(window);
        border.SetContent(Named(window, "Content", log));
        window.SetContent(border);
        DrawOnce(harness, window);
        log.Clear();
        harness.Draw();

        Assert.Equal(new[] { "Content" }, log.ToArray());
    }

    [Fact]
    public void AWindowThatOverridesGetChildren_IsDrawnThroughIt()
    {
        Harness harness = NewHarness();
        List<string> log = new();
        ExtraChildWindow window = new(harness.GetDesktop());
        MGElement extra = window.GetChildren().First();
        extra.OnBeginDraw += (s, e) => log.Add("X");
        MGButton content = Named(window, "Content", log);
        window.SetContent(content);
        DrawOnce(harness, window);
        log.Clear();
        harness.Draw();

        Assert.Equal(new[] { "X", "Content" }, log.ToArray());
    }

    [Fact]
    public void AMultiHostWithNoChild_DrawsNothingAndDoesNotThrow()
    {
        Harness harness = NewHarness();
        MGWindow window = new(harness.GetDesktop(), 0, 0, 300, 200);
        window.SetContent(new PlainStackPanel(window));
        DrawOnce(harness, window);
        harness.Draw();
    }
}
