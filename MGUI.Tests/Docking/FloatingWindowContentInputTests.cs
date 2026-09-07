using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Docking;

/// <summary>
/// Covers task 1 of the docking-bugs slice: <see cref="MGFloatingDockWindow"/> now builds its
/// <see cref="MGDockTabGroup"/> content with itself (<c>this</c>) as the owning window instead of
/// <c>ownerHost.ParentWindow</c>. Framework convention: a window's content belongs to that window
/// (see <c>MGContextMenu</c>, which builds its items with <c>this</c>). Before the fix, the tab
/// items inside a floating window had their <see cref="MGElement.ParentWindow"/> set to the docked
/// layout's main window, so <see cref="MGElement.IsInside"/> considered them occluded by their own
/// floating window (a nested window of that main window) and hover/click/right-click/drag never reached
/// them. After the fix, the tabs' own window is the floating window, which does not occlude itself.
/// </summary>
public class FloatingWindowContentInputTests
{
    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;
        public DockPanelNode PanelA;
        public DockPanelNode PanelB;
    }

    private static Harness CreateHarness()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };

        DockPanelNode panelA = new() { Title = "A", ContentFactory = () => new MGBorder(mainWindow) };
        DockPanelNode panelB = new() { Title = "B", ContentFactory = () => new MGBorder(mainWindow) };

        DockTabGroupNode groupA = new();
        groupA.AddPanel(panelA, -1);
        DockTabGroupNode groupB = new();
        groupB.AddPanel(panelB, -1);

        DockSplitNode rootSplit = new()
        {
            Orientation = Orientation.Horizontal,
            SplitRatio = 0.5f,
            MinFirstSize = 0,
            MinSecondSize = 0,
            FirstChild = groupA,
            SecondChild = groupB,
        };

        MGDockHost host = new(mainWindow)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(rootSplit),
        };

        mainWindow.SetContent(host);
        desktop.Windows.Add(mainWindow);

        return new Harness
        {
            Runtime = runtime,
            Desktop = desktop,
            MainWindow = mainWindow,
            Host = host,
            PanelA = panelA,
            PanelB = panelB,
        };
    }

    private static MGFloatingDockWindow FloatPanelA(Harness harness)
    {
        AdvanceFrame(harness.Runtime, harness.Desktop, 0, Point.Zero);
        AdvanceFrame(harness.Runtime, harness.Desktop, 16, Point.Zero);

        // Float panel A away from the host's occupied area so the floating window doesn't overlap panel B.
        MGFloatingDockWindow floatingWindow = harness.Host.DetachToFloating(harness.PanelA, new Point(600, 500));

        AdvanceFrame(harness.Runtime, harness.Desktop, 32, Point.Zero);
        AdvanceFrame(harness.Runtime, harness.Desktop, 48, Point.Zero);

        return floatingWindow;
    }

    private static MGDockTabItem GetSoleTabItem(MGFloatingDockWindow floatingWindow)
        => Assert.Single(floatingWindow.TabGroup.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false));

    [Fact]
    public void FloatedTab_SelfOrParentWindow_IsTheFloatingWindow()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = FloatPanelA(harness);

        MGDockTabItem tab = GetSoleTabItem(floatingWindow);

        Assert.Same(floatingWindow, tab.SelfOrParentWindow);
        Assert.Same(floatingWindow, tab.ParentWindow);
    }

    [Fact]
    public void HoveringFloatedTab_IsHovered_IsTrue()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = FloatPanelA(harness);
        MGDockTabItem tab = GetSoleTabItem(floatingWindow);

        Point onTab = tab.LayoutBounds.Center;
        AdvanceFrame(harness.Runtime, harness.Desktop, 64, onTab);
        AdvanceFrame(harness.Runtime, harness.Desktop, 80, onTab);

        Assert.True(tab.IsHovered);
    }

    [Fact]
    public void RightClickFloatedTab_OpensContextMenu()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = FloatPanelA(harness);
        MGDockTabItem tab = GetSoleTabItem(floatingWindow);

        Point onTab = tab.LayoutBounds.Center;
        AdvanceFrame(harness.Runtime, harness.Desktop, 64, onTab, MouseButton.Right);
        AdvanceFrame(harness.Runtime, harness.Desktop, 80, onTab, MouseButton.Right);
        AdvanceFrame(harness.Runtime, harness.Desktop, 96, onTab);

        Assert.NotNull(harness.Desktop.ActiveContextMenu);
    }

    [Fact]
    public void LeftPressAndDragFloatedTab_ReachesHost_BeginsDrag()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = FloatPanelA(harness);
        MGDockTabItem tab = GetSoleTabItem(floatingWindow);

        Point pressPoint = tab.LayoutBounds.Center;
        Point dragPoint = new(pressPoint.X + 20, pressPoint.Y);

        AdvanceFrame(harness.Runtime, harness.Desktop, 64, pressPoint, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 80, dragPoint, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 96, dragPoint, MouseButton.Left);

        Assert.NotNull(harness.Host.CurrentDrag);
        Assert.Same(floatingWindow, harness.Host.CurrentDrag.SourceFloatingWindow);

        // Release to leave the drag state clean.
        AdvanceFrame(harness.Runtime, harness.Desktop, 112, dragPoint);
    }

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, Point position, MouseButton? pressedButton = null)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(position, pressedButton),
            new KeyboardState()));
        desktop.Update();
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton)
        => new(
            position.X,
            position.Y,
            0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);
}
