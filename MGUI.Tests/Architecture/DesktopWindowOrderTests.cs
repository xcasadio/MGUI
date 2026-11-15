using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit.Abstractions;

namespace MGUI.Tests.Architecture;

/// <summary>
/// The order in which a desktop updates and draws its windows: topmost windows are updated first and drawn last, and inside a group the later
/// window of <see cref="MGDesktop.Windows"/> is updated first and drawn last. Both loops walk a copy, so a window opened or closed during the
/// loop does not change the walk in progress, and a window may update or draw its desktop again.
/// </summary>
public class DesktopWindowOrderTests
{
    private readonly ITestOutputHelper Output;

    public DesktopWindowOrderTests(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>A window that writes its name to a shared log when it is updated and when it is drawn, and may run an action first.</summary>
    private sealed class LoggedWindow : MGWindow
    {
        private readonly List<string> Log;
        public string Tag { get; }
        public Action<LoggedWindow> OnUpdate { get; set; }
        public Action<LoggedWindow> OnDraw { get; set; }

        public LoggedWindow(MGDesktop desktop, string tag, bool isTopmost, List<string> log)
            : base(desktop, 0, 0, 50, 50)
        {
            Tag = tag;
            Log = log;
            IsTopmost = isTopmost;
        }

        public override void UpdateSelf(ElementUpdateArgs UA)
        {
            Log.Add("update " + Tag);
            OnUpdate?.Invoke(this);
            base.UpdateSelf(UA);
        }

        public override void Draw(ElementDrawArgs DA)
        {
            Log.Add("draw " + Tag);
            OnDraw?.Invoke(this);
            base.Draw(DA);
        }
    }

    private sealed class Scene
    {
        public readonly List<string> Log = new();
        public readonly DesktopFrameAllocationMeasurements.Harness Harness = DesktopFrameAllocationMeasurements.Harness.Create(DesktopFrameAllocationMeasurements.Fixture.NoWindow);
        public readonly Dictionary<string, LoggedWindow> Windows = new();

        public MGDesktop Desktop => Harness.GetDesktop();

        /// <summary>Windows added in the order given: A and C are normal windows, B and D are topmost.</summary>
        public Scene(string tags = "ABCD", string topmost = "BD")
        {
            foreach (char tag in tags)
            {
                LoggedWindow window = new(Desktop, tag.ToString(), topmost.Contains(tag), Log);
                Windows[window.Tag] = window;
                Desktop.Windows.Add(window);
            }

            Harness.Frame(new Point(600, 340), ButtonState.Released);
            Harness.Frame(new Point(600, 340), ButtonState.Released);
            Log.Clear();
        }

        public string[] UpdateOnce()
        {
            Log.Clear();
            Harness.Update(new Point(600, 340), ButtonState.Released);
            return Log.ToArray();
        }

        public string[] DrawOnce()
        {
            Log.Clear();
            Harness.Draw(new NonRecordingDrawTransaction(Harness.GetRuntime(), DrawSettings.Default));
            return Log.ToArray();
        }
    }

    [Fact]
    public void FourMixedWindowsAreUpdatedTopmostFirstAndLastWindowFirst()
    {
        Scene scene = new();
        Assert.Equal(new[] { "update D", "update B", "update C", "update A" }, scene.UpdateOnce());
    }

    [Fact]
    public void FourMixedWindowsAreDrawnTopmostLastAndLastWindowLast()
    {
        Scene scene = new();
        Assert.Equal(new[] { "draw A", "draw C", "draw B", "draw D" }, scene.DrawOnce());
    }

    [Fact]
    public void AWindowClosedDuringItsOwnUpdateDoesNotChangeTheWalkInProgress()
    {
        Scene scene = new();
        //  C closes itself, B closes A (updated after B): the loop walks a copy, so both are still updated this frame and gone the next.
        scene.Windows["C"].OnUpdate = w => scene.Desktop.Windows.Remove(w);
        scene.Windows["B"].OnUpdate = _ => scene.Desktop.Windows.Remove(scene.Windows["A"]);

        Assert.Equal(new[] { "update D", "update B", "update C", "update A" }, scene.UpdateOnce());
        Assert.Equal(new[] { "update D", "update B" }, scene.UpdateOnce());
    }

    [Fact]
    public void AWindowClosedDuringItsOwnDrawDoesNotChangeTheWalkInProgress()
    {
        Scene scene = new();
        scene.Windows["A"].OnDraw = w => scene.Desktop.Windows.Remove(scene.Windows["C"]);
        scene.Windows["B"].OnDraw = w => scene.Desktop.Windows.Remove(w);

        Assert.Equal(new[] { "draw A", "draw C", "draw B", "draw D" }, scene.DrawOnce());
        Assert.Equal(new[] { "draw A", "draw D" }, scene.DrawOnce());
    }

    [Fact]
    public void AWindowOpenedDuringTheWalkIsNotWalkedBeforeTheNextFrame()
    {
        Scene scene = new();
        LoggedWindow late = new(scene.Desktop, "E", false, scene.Log);
        scene.Windows["C"].OnUpdate = _ => scene.Desktop.Windows.Add(late);
        scene.Windows["A"].OnDraw = _ => scene.Desktop.Windows.Add(new LoggedWindow(scene.Desktop, "F", true, scene.Log));

        Assert.Equal(new[] { "update D", "update B", "update C", "update A" }, scene.UpdateOnce());
        scene.Windows["C"].OnUpdate = null;
        Assert.Equal(new[] { "draw A", "draw C", "draw E", "draw B", "draw D" }, scene.DrawOnce());
        scene.Windows["A"].OnDraw = null;
        Assert.Equal(new[] { "update F", "update D", "update B", "update E", "update C", "update A" }, scene.UpdateOnce());
    }

    [Fact]
    public void AnUpdateRequestedByAWindowDuringTheUpdateWalksTheWindowsAgain()
    {
        Scene scene = new();
        bool entered = false;
        scene.Windows["B"].OnUpdate = _ =>
        {
            if (!entered)
            {
                entered = true;
                scene.Harness.Update(new Point(600, 340), ButtonState.Released);
            }
        };

        Assert.Equal(new[]
        {
            "update D", "update B",
            "update D", "update B", "update C", "update A",
            "update C", "update A"
        }, scene.UpdateOnce());
    }

    [Fact]
    public void ADrawRequestedByAWindowDuringTheDrawWalksTheWindowsAgain()
    {
        Scene scene = new();
        bool entered = false;
        scene.Windows["C"].OnDraw = _ =>
        {
            if (!entered)
            {
                entered = true;
                scene.Harness.Draw(new NonRecordingDrawTransaction(scene.Harness.GetRuntime(), DrawSettings.Default));
            }
        };

        Assert.Equal(new[]
        {
            "draw A", "draw C",
            "draw A", "draw C", "draw B", "draw D",
            "draw B", "draw D"
        }, scene.DrawOnce());
    }

    [Fact]
    public void BothOrdersEqualTheLinqExpressionTheyReplace_OnRandomLists()
    {
        Scene scene = new("", "");
        List<MGWindow> pool = new();
        for (int i = 0; i < 12; i++)
        {
            pool.Add(new MGWindow(scene.Desktop, 0, 0, 10, 10));
        }

        Random random = new(20261115);
        List<MGWindow> result = new();
        for (int iteration = 0; iteration < 2000; iteration++)
        {
            List<MGWindow> windows = pool.OrderBy(_ => random.Next()).Take(random.Next(0, 13)).ToList();
            foreach (MGWindow window in windows)
            {
                window.IsTopmost = random.Next(2) == 0;
            }

            MGDesktop.FillUpdateOrder(windows, result);
            Assert.True(windows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost).SequenceEqual(result), "Update order, iteration " + iteration);

            MGDesktop.FillDrawOrder(windows, result);
            Assert.True(windows.OrderBy(x => x.IsTopmost).SequenceEqual(result), "Draw order, iteration " + iteration);
        }
    }

    [Fact]
    public void BothOrdersAllocateNothing_WhereTheLinqExpressionsDo()
    {
        Scene scene = new("", "");
        List<MGWindow> windows = new();
        for (int i = 0; i < 8; i++)
        {
            windows.Add(new MGWindow(scene.Desktop, 0, 0, 10, 10) { IsTopmost = i % 3 == 0 });
        }

        List<MGWindow> result = new(16);
        long updateOrder = MeasureCall(() => MGDesktop.FillUpdateOrder(windows, result));
        long drawOrder = MeasureCall(() => MGDesktop.FillDrawOrder(windows, result));
        //  The expressions the two methods replaced, as the counter-test: the same measurement on them allocates.
        long linqUpdate = MeasureCall(() => result = windows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost).ToList());
        long linqDraw = MeasureCall(() =>
        {
            foreach (MGWindow window in windows.OrderBy(x => x.IsTopmost))
            {
                _ = window;
            }
        });

        Output.WriteLine($"FillUpdateOrder {updateOrder} B/call, FillDrawOrder {drawOrder} B/call; LINQ Update expression {linqUpdate} B/call, LINQ Draw expression {linqDraw} B/call");
        Assert.True(linqUpdate > 0 && linqDraw > 0, "The LINQ expressions were expected to allocate");
        Assert.Equal(0, updateOrder);
        Assert.Equal(0, drawOrder);
    }

    private static long MeasureCall(Action action)
    {
        for (int i = 0; i < 500; i++)
        {
            action();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 500; i++)
        {
            action();
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        return (after - before) / 500;
    }
}
