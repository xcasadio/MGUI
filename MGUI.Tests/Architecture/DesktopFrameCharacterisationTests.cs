using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Characterisation of what a desktop frame does, written on the code before the allocation-free work and kept after it: the draw journal of
/// the fixtures (every call of the draw transaction, in order, see <see cref="OrderedRecordingDrawTransaction"/>) and, for <c>Update</c>, the
/// order of the window updates, the order of the events a window raises (internal handlers before the ones a user subscribed), the hovered,
/// pressed and focused elements after a fixed mouse scenario, the order of the nested windows and the children of an overlay host.
/// </summary>
public class DesktopFrameCharacterisationTests
{
    private static readonly Point Outside = new(600, 340);

    private static DesktopFrameAllocationMeasurements.Harness CreateHarness(DesktopFrameAllocationMeasurements.Fixture fixture)
        => DesktopFrameAllocationMeasurements.Harness.Create(fixture);

    private static OrderedRecordingDrawTransaction DrawJournal(DesktopFrameAllocationMeasurements.Harness harness)
    {
        OrderedRecordingDrawTransaction transaction = new(harness.GetRuntime(), DrawSettings.Default);
        harness.Draw(transaction);
        return transaction;
    }

    //  ---- Draw: the journal of the fixtures ------------------------------------------------------------------------------------------

    [Fact]
    public void TheDrawJournalOfTheDesktopWithoutWindow_IdleAndWithTheMouseMoving()
    {
        DesktopFrameAllocationMeasurements.Harness harness = CreateHarness(DesktopFrameAllocationMeasurements.Fixture.NoWindow);
        List<string> journal = new();
        foreach (Point position in new[] { Outside, Outside, new Point(100, 100), new Point(120, 110), new Point(100, 100) })
        {
            harness.Update(position, ButtonState.Released);
            journal.AddRange(DrawJournal(harness).Lines);
            journal.Add("--");
        }

        FrozenJournal.Assert("NoWindow draw journal", 15, "D0A748987D1601A3", journal);
    }

    private static List<string> WindowDrawJournal(DesktopFrameAllocationMeasurements.Fixture fixture, params Point[] positions)
    {
        DesktopFrameAllocationMeasurements.Harness harness = CreateHarness(fixture);
        List<string> journal = new();
        foreach (Point position in positions)
        {
            harness.Update(position, ButtonState.Released);
            journal.AddRange(DrawJournal(harness).Lines);
            journal.Add("--");
        }

        return journal;
    }

    [Fact]
    public void TheDrawJournalOfABareWindow_IdleAndWithTheMouseStillOverIt()
    {
        List<string> journal = WindowDrawJournal(DesktopFrameAllocationMeasurements.Fixture.BareWindow,
            Outside, Outside, new Point(150, 100), new Point(150, 100), Outside);

        FrozenJournal.Assert("BareWindow draw journal", 130, "7465221FAD2D49C8", journal);
    }

    [Fact]
    public void TheDrawJournalOfADefaultWindow_IdleAndWithTheMouseOverTheTitleBarAndOverTheCloseButton()
    {
        DesktopFrameAllocationMeasurements.Harness harness = CreateHarness(DesktopFrameAllocationMeasurements.Fixture.DefaultWindow);
        (Point title, Point close) = harness.FindTitleBarPoints();
        List<string> journal = new();
        foreach (Point position in new[] { Outside, Outside, title, title, close, close, Outside })
        {
            harness.Update(position, ButtonState.Released);
            journal.AddRange(DrawJournal(harness).Lines);
            journal.Add("--");
        }

        FrozenJournal.Assert("DefaultWindow draw journal", 322, "CA21F30ACC8E2699", journal);
    }

    [Fact]
    public void TheDrawJournalOfAWindowWithAStackPanelOfTwoChildren_IdleAndWithTheMouseStillOverIt()
    {
        List<string> journal = WindowDrawJournal(DesktopFrameAllocationMeasurements.Fixture.Full,
            Outside, Outside, new Point(150, 100), new Point(150, 100), Outside);

        FrozenJournal.Assert("Full draw journal", 340, "4EA6EED126EC08FA", journal);
    }

    //  ---- Draw: the journal of the containers of the slider (T1.5), idle and while the slider is dragged ------------------------------

    private static List<string> IdleJournal(DesktopFrameAllocationMeasurements.Fixture fixture)
        => WindowDrawJournal(fixture, Outside, Outside, new Point(150, 100), new Point(150, 100), Outside);

    /// <summary>The draw journal of the slider of the fixture while it is dragged (press on the thumb, two moves, a stay, release, mouse away).</summary>
    private static List<string> DragJournal(DesktopFrameAllocationMeasurements.Fixture fixture)
    {
        DesktopFrameAllocationMeasurements.Harness harness = CreateHarness(fixture);
        MGSlider slider = harness.Slider;
        Rectangle bounds = slider.LayoutBounds;
        Point thumb = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, bounds.Center);
        Point first = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 5, bounds.Center.Y));
        Point second = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 4, bounds.Center.Y));
        List<string> journal = new();
        foreach ((Point position, ButtonState left) in new[]
                 {
                     (Outside, ButtonState.Released), (thumb, ButtonState.Released), (thumb, ButtonState.Released), (thumb, ButtonState.Pressed),
                     (first, ButtonState.Pressed), (second, ButtonState.Pressed), (second, ButtonState.Pressed), (first, ButtonState.Pressed),
                     (first, ButtonState.Released), (Outside, ButtonState.Released)
                 })
        {
            harness.Update(position, left);
            journal.AddRange(DrawJournal(harness).Lines);
            journal.Add("--");
        }

        Assert.True(slider.Value > 0f);
        return journal;
    }

    [Fact]
    public void TheDrawJournalOfTheFullFixture_WhileTheSliderIsDragged()
        => FrozenJournal.Assert("Full drag journal", 800, "68E93B778DFA70A0", DragJournal(DesktopFrameAllocationMeasurements.Fixture.Full));

    [Fact]
    public void TheDrawJournalOfTheFullFixtureInAGrid_Idle()
        => FrozenJournal.Assert("FullInGrid idle journal", 350, "E313EED065EDC31C", IdleJournal(DesktopFrameAllocationMeasurements.Fixture.FullInGrid));

    [Fact]
    public void TheDrawJournalOfTheFullFixtureInAGrid_WhileTheSliderIsDragged()
        => FrozenJournal.Assert("FullInGrid drag journal", 820, "FEB8EB37392C2D41", DragJournal(DesktopFrameAllocationMeasurements.Fixture.FullInGrid));

    [Fact]
    public void TheDrawJournalOfTheFullFixtureInADockPanel_Idle()
        => FrozenJournal.Assert("FullInDockPanel idle journal", 350, "02355343A2F879CC", IdleJournal(DesktopFrameAllocationMeasurements.Fixture.FullInDockPanel));

    [Fact]
    public void TheDrawJournalOfTheFullFixtureInADockPanel_WhileTheSliderIsDragged()
        => FrozenJournal.Assert("FullInDockPanel drag journal", 820, "03BDAA5F77FFA1B3", DragJournal(DesktopFrameAllocationMeasurements.Fixture.FullInDockPanel));

    [Fact]
    public void TheDrawJournalOfTheFullFixtureInADockHost_Idle()
        => FrozenJournal.Assert("FullInDockHost idle journal", 575, "C2490D7D7541C9AD", IdleJournal(DesktopFrameAllocationMeasurements.Fixture.FullInDockHost));

    [Fact]
    public void TheDrawJournalOfTheFullFixtureInADockHost_WhileTheSliderIsDragged()
        => FrozenJournal.Assert("FullInDockHost drag journal", 1270, "52957F04D8CFA4E5", DragJournal(DesktopFrameAllocationMeasurements.Fixture.FullInDockHost));

    //  ---- Update: a window, its events and the mouse -----------------------------------------------------------------------------------

    private sealed class ScenarioLog
    {
        public readonly List<string> Lines = new();
        public int Frame;

        public void Add(string text) => Lines.Add($"f{Frame} {text}");
    }

    private static string Name(MGElement element) => element == null ? "null" : (string.IsNullOrEmpty(element.Name) ? element.GetType().Name : element.Name);

    /// <summary>A window holding a button, watched through every update event and the hover and press events; a scripted mouse path over it.</summary>
    private static List<string> RunMouseScenario(bool withFocus)
    {
        DesktopFrameAllocationMeasurements.Harness harness = CreateHarness(DesktopFrameAllocationMeasurements.Fixture.NoWindow);
        MGDesktop desktop = harness.GetDesktop();
        ScenarioLog log = new();

        MGWindow window = new(desktop, 0, 0, 300, 200) { Name = "W" };
        MGButton button = new(window, b => log.Add("clicked")) { Name = "Btn" };
        window.SetContent(button);
        desktop.Windows.Add(window);

        window.OnBeginUpdate += (s, e) => log.Add($"W.begin hovered={Name(window.HoveredElement)} pressed={Name(window.PressedElement)}");
        window.OnBeginUpdateContents += (s, e) => log.Add($"W.beginContents hovered={Name(window.HoveredElement)}");
        window.OnEndUpdateContents += (s, e) => log.Add($"W.endContents hovered={Name(window.HoveredElement)}");
        window.OnEndUpdate += (s, e) => log.Add($"W.end hovered={Name(window.HoveredElement)} pressed={Name(window.PressedElement)}");
        window.HoveredElementChanged += (s, e) => log.Add($"W.hoveredChanged {Name(window.HoveredElement)}");
        window.PressedElementChanged += (s, e) => log.Add($"W.pressedChanged {Name(window.PressedElement)}");
        button.OnBeginUpdate += (s, e) => log.Add($"Btn.begin state={button.VisualState}");
        button.OnEndUpdate += (s, e) => log.Add($"Btn.end state={button.VisualState}");

        harness.Frame(Outside, ButtonState.Released);
        harness.Frame(Outside, ButtonState.Released);
        if (withFocus)
        {
            button.Focus(KeyboardFocusSource.Programmatic);
        }

        Point onButton = new(150, 110);
        Point onTitle = new(150, 12);
        (Point, ButtonState)[] script =
        {
            (Outside, ButtonState.Released),
            (onButton, ButtonState.Released),
            (onButton, ButtonState.Released),
            (onTitle, ButtonState.Released),
            (onButton, ButtonState.Released),
            (onButton, ButtonState.Pressed),
            (onButton, ButtonState.Pressed),
            (Outside, ButtonState.Pressed),
            (Outside, ButtonState.Released),
            (onButton, ButtonState.Released),
            (onButton, ButtonState.Pressed),
            (onButton, ButtonState.Released),
            (Outside, ButtonState.Released),
        };

        for (int i = 0; i < script.Length; i++)
        {
            log.Frame = i;
            harness.Update(script[i].Item1, script[i].Item2);
            log.Add($"after hovered={Name(window.HoveredElement)} pressed={Name(window.PressedElement)} focus={Name(desktop.FocusedKeyboardHandler as MGElement)} " +
                    $"window={window.VisualState} button={button.VisualState}");
            foreach (string line in DrawJournal(harness).Lines.Where(x => x.StartsWith("DrawTextViaEngine") || x.StartsWith("FillRectangle")).Take(3))
            {
                log.Add("draw " + line);
            }
        }

        return log.Lines;
    }

    [Fact]
    public void AWindowWithAButtonUnderAScriptedMouse_RaisesTheSameEventsInTheSameOrder()
    {
        FrozenJournal.Assert("mouse scenario", 151, "8C09C6261BBB9C3B", RunMouseScenario(false));
    }

    [Fact]
    public void AWindowWithAFocusedButtonUnderAScriptedMouse_RaisesTheSameEventsInTheSameOrder()
    {
        FrozenJournal.Assert("mouse scenario with focus", 151, "7FAD4FBC3C5C75D3", RunMouseScenario(true));
    }

    //  ---- Update: nested windows -------------------------------------------------------------------------------------------------------

    private sealed class LoggedNestedWindow : MGWindow
    {
        private readonly List<string> Log;
        private readonly string Tag;

        public LoggedNestedWindow(MGWindow parent, string tag, bool isTopmost, List<string> log)
            : base(parent, 0, 0, 40, 40)
        {
            Tag = tag;
            Log = log;
            IsTopmost = isTopmost;
        }

        public override void UpdateSelf(ElementUpdateArgs UA)
        {
            Log.Add("update " + Tag);
            base.UpdateSelf(UA);
        }
    }

    [Fact]
    public void TheNestedWindowsOfAWindowAreUpdatedModalFirstThenTopmostFirstAndLastAddedFirst()
    {
        DesktopFrameAllocationMeasurements.Harness harness = CreateHarness(DesktopFrameAllocationMeasurements.Fixture.NoWindow);
        MGDesktop desktop = harness.GetDesktop();
        List<string> log = new();
        MGWindow parent = new(desktop, 0, 0, 300, 200);
        desktop.Windows.Add(parent);
        foreach ((string tag, bool topmost) in new[] { ("A", false), ("B", true), ("C", false), ("D", true) })
        {
            parent.AddNestedWindow(new LoggedNestedWindow(parent, tag, topmost, log));
        }

        parent.ModalWindow = new LoggedNestedWindow(parent, "M", false, log);
        harness.Frame(Outside, ButtonState.Released);
        log.Clear();
        harness.Update(Outside, ButtonState.Released);

        Assert.Equal(new[] { "update M", "update D", "update B", "update C", "update A" }, log.ToArray());
    }

    //  ---- Update: an overlay host ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnActiveOverlayTakesTheFocusAwayFromWhatIsBehindItOnlyWhenModal(bool isModal)
    {
        DesktopFrameAllocationMeasurements.Harness harness = CreateHarness(DesktopFrameAllocationMeasurements.Fixture.NoWindow);
        MGDesktop desktop = harness.GetDesktop();
        MGWindow window = new(desktop, 0, 0, 300, 200);
        MGButton behind = new(window);
        window.SetContent(behind);
        desktop.Windows.Add(window);
        harness.Frame(Outside, ButtonState.Released);
        harness.Frame(Outside, ButtonState.Released);
        behind.Focus(KeyboardFocusSource.Programmatic);
        harness.Frame(Outside, ButtonState.Released);
        harness.Frame(Outside, ButtonState.Released);
        Assert.Same(behind, desktop.FocusedKeyboardHandler);

        desktop.OverlayHost.IsModal = isModal;
        MGOverlay overlay = desktop.OverlayHost.AddOverlay(new MGButton(desktop.OverlayHost.ParentWindow));
        Assert.True(desktop.OverlayHost.TryOpen(overlay));
        harness.Frame(Outside, ButtonState.Released);
        harness.Frame(Outside, ButtonState.Released);

        Assert.Equal(isModal ? null : behind, desktop.FocusedKeyboardHandler);
        Assert.Same(overlay, desktop.OverlayHost.ActiveOverlay);
    }

    [Fact]
    public void TheVisualTreeChildrenOfAnOverlayHost_WithAndWithoutInactiveOverlays()
    {
        DesktopFrameAllocationMeasurements.Harness harness = CreateHarness(DesktopFrameAllocationMeasurements.Fixture.NoWindow);
        MGOverlayHost host = harness.GetDesktop().OverlayHost;
        harness.Frame(Outside, ButtonState.Released);

        List<string> log = new();
        void Snapshot(string label)
        {
            foreach ((bool inactive, bool active) in new[] { (false, true), (true, false), (true, true), (false, false) })
            {
                log.Add($"{label} inactive={inactive} active={active}: " + string.Join(",", host.GetVisualTreeChildren(inactive, active).Select(Name)));
            }
        }

        Snapshot("none");
        MGOverlay first = host.AddOverlay(new MGButton(host.ParentWindow) { Name = "ContentA" });
        first.Name = "OverlayA";
        MGOverlay second = host.AddOverlay(new MGButton(host.ParentWindow) { Name = "ContentB" });
        second.Name = "OverlayB";
        Snapshot("two closed");
        Assert.True(host.TryOpen(second));
        Snapshot("B open");
        Assert.True(host.TryOpen(first));
        Snapshot("both open");

        FrozenJournal.Assert("overlay host children", 16, "46011CD2D097837E", log);
    }
}
