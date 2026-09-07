using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
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
/// Covers <see cref="MGElement.DisplayingWindow"/> (see <c>Docs/decisions/0004-hit-test-occlusion-from-displaying-window.md</c>)
/// for application content, not just the docking framework's own tab items (that's <see cref="FloatingWindowContentInputTests"/>,
/// task 1). Panel content here is built by a content factory with the MAIN window (as application code does via
/// <see cref="DockPanelNode.ContentFactory"/>/<see cref="DockPanelNode.GetOrCreateContent"/>), cached, and then re-parented
/// by <see cref="MGDockTabGroup"/> into whichever window currently hosts the panel's tab group - the docked host's main window,
/// or a floating window after <see cref="MGDockHost.DetachToFloating"/>. Before this fix, that application content stayed
/// bound to the main window as its <see cref="MGElement.SelfOrParentWindow"/>, so once floated it was occluded by its own
/// floating window (a nested window of the main window) and never received hover/click.
/// </summary>
public class FloatingWindowRedockTests
{
    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;
        public DockPanelNode PanelA;
        public DockPanelNode PanelB;
        public MGButton ButtonA;
    }

    private static Harness CreateHarness()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };

        MGButton buttonA = null;
        DockPanelNode panelA = new()
        {
            Title = "A",
            ContentFactory = () =>
            {
                // Application content: built with the main window, deep child (panel -> button).
                buttonA = new MGButton(mainWindow) { PreferredWidth = 60, PreferredHeight = 30 };
                MGStackPanel stack = new(mainWindow, Orientation.Vertical);
                stack.TryAddChild(buttonA);
                return stack;
            },
        };
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

        AdvanceFrame(runtime, desktop, 0, Point.Zero);
        AdvanceFrame(runtime, desktop, 16, Point.Zero);

        return new Harness
        {
            Runtime = runtime,
            Desktop = desktop,
            MainWindow = mainWindow,
            Host = host,
            PanelA = panelA,
            PanelB = panelB,
            ButtonA = buttonA,
        };
    }

    private static MGFloatingDockWindow FloatPanelA(Harness harness)
    {
        MGFloatingDockWindow floatingWindow = harness.Host.DetachToFloating(harness.PanelA, new Point(600, 500));
        AdvanceFrame(harness.Runtime, harness.Desktop, 32, Point.Zero);
        AdvanceFrame(harness.Runtime, harness.Desktop, 48, Point.Zero);
        return floatingWindow;
    }

    private static MGDockTabItem GetSoleTabItem(MGFloatingDockWindow floatingWindow)
        => Assert.Single(floatingWindow.TabGroup.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false));

    [Fact]
    public void ApplicationContentInFloatingWindow_IsHoveredAndClickable()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = FloatPanelA(harness);

        MGButton button = harness.ButtonA;
        Assert.NotNull(button);
        Assert.True(floatingWindow.LayoutBounds.Contains(button.LayoutBounds.Center));

        int clicks = 0;
        button.OnLeftClicked += (_, _) => clicks++;

        Point onButton = button.LayoutBounds.Center;
        AdvanceFrame(harness.Runtime, harness.Desktop, 64, onButton);
        AdvanceFrame(harness.Runtime, harness.Desktop, 80, onButton);
        Assert.True(button.IsHovered);
        Assert.Equal(SecondaryVisualState.Hovered, button.VisualState.Secondary);

        AdvanceFrame(harness.Runtime, harness.Desktop, 96, onButton, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 112, onButton, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 128, onButton);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void DraggingFloatedTabOverHost_ShowsDropIndicators_AndReleaseRedocksThePanel()
    {
        Harness harness = CreateHarness();
        MGFloatingDockWindow floatingWindow = FloatPanelA(harness);
        MGDockTabItem tab = GetSoleTabItem(floatingWindow);
        MGDockTabGroup remainingGroup = Assert.Single(harness.Host.GetAllVisibleTabGroups());
        Assert.Same(harness.PanelB, Assert.Single(remainingGroup.GroupNode.Panels));

        Point pressPoint = tab.LayoutBounds.Center;
        Point overHostCenter = remainingGroup.LayoutBounds.Center;

        // Press-and-move (as in FloatingWindowContentInputTests.LeftPressAndDragFloatedTab): the tab item's
        // DragStart event - which calls MGDockHost.BeginDrag - only fires once the mouse has actually moved
        // while pressed, not on the press frame alone.
        Point midway = new((pressPoint.X + overHostCenter.X) / 2, (pressPoint.Y + overHostCenter.Y) / 2);
        AdvanceFrame(harness.Runtime, harness.Desktop, 64, pressPoint, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 80, midway, MouseButton.Left);
        Assert.NotNull(harness.Host.CurrentDrag);
        Assert.Same(floatingWindow, harness.Host.CurrentDrag.SourceFloatingWindow);

        // Move towards the remaining docked group in a few steps so the drag threshold is exceeded
        // and the drop-preview logic (polled every frame in MGDockHost.UpdateSelf) gets to run.
        AdvanceFrame(harness.Runtime, harness.Desktop, 96, overHostCenter, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 112, overHostCenter, MouseButton.Left);

        Assert.NotNull(harness.Host.CurrentDrag);
        Assert.True(harness.Host.CurrentDrag.HasExceededThreshold);
        Assert.NotNull(harness.Host.CurrentDropTarget);
        Assert.Equal(DockZone.Center, harness.Host.CurrentDropTarget.Zone);

        // Release over the drop target: the polled drag in MGDockHost.UpdateSelf performs the drop
        // as soon as it observes the left button no longer pressed.
        AdvanceFrame(harness.Runtime, harness.Desktop, 128, overHostCenter);
        AdvanceFrame(harness.Runtime, harness.Desktop, 144, overHostCenter);

        Assert.Null(harness.Host.CurrentDrag);
        Assert.Empty(harness.Host.FloatingWindows);
        Assert.Empty(harness.MainWindow.NestedWindows);

        var allPanels = harness.Host.LayoutModel.GetAllTabGroups()
            .SelectMany(g => g.Panels)
            .ToList();
        Assert.Contains(allPanels, p => ReferenceEquals(p, harness.PanelA));

        // The re-docked application content is interactive again through the host's main window.
        AdvanceFrame(harness.Runtime, harness.Desktop, 160, Point.Zero);
        AdvanceFrame(harness.Runtime, harness.Desktop, 176, Point.Zero);
        Assert.Same(harness.MainWindow, harness.ButtonA.DisplayingWindow);
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
