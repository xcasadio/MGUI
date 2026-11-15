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
    public void ASingleHostDrawsDirectlyOnlyWhenItDoesNotOverrideGetChildren()
    {
        Assert.True(MGContentHost.DrawsChildrenDirectly(typeof(MGBorder), typeof(MGSingleContentHost)));
        Assert.True(MGContentHost.DrawsChildrenDirectly(typeof(PlainBorder), typeof(MGSingleContentHost)));
        Assert.True(MGContentHost.DrawsChildrenDirectly(typeof(MGWindow), typeof(MGSingleContentHost)));
        Assert.False(MGContentHost.DrawsChildrenDirectly(typeof(ExtraChildBorder), typeof(MGSingleContentHost)));
        Assert.False(MGContentHost.DrawsChildrenDirectly(typeof(ExtraChildWindow), typeof(MGSingleContentHost)));
    }
}
