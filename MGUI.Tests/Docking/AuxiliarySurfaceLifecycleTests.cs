using System;
using System.Collections.Generic;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Docking;

/// <summary>
/// Backlog task 12 (styling-theme-tasks.md), scenario <c>SCN-THEME-001</c>: the auxiliary surfaces that a control opens as nested windows of its window are
/// attached when opened and detached when closed, whichever way they close. Covers <see cref="MGFloatingDockWindow"/>, owned by <see cref="MGDockHost"/>,
/// and <see cref="MGColorPickerPopup"/>, owned by a window.
/// </summary>
public class AuxiliarySurfaceLifecycleTests
{
    [Fact]
    public void A_Floating_Dock_Window_Is_Attached_When_Floated_And_Detached_When_The_Host_Closes_It()
    {
        DockHarness harness = DockHarness.Create();

        MGFloatingDockWindow floating = harness.Float(harness.PanelA);

        Assert.Contains(floating, harness.Host.FloatingWindows);
        Assert.Contains(floating, harness.MainWindow.NestedWindows);
        Assert.Same(harness.MainWindow, floating.ParentWindow);
        Assert.Same(harness.PanelA, Assert.Single(floating.GroupNode.Panels));

        harness.Host.CloseFloatingWindow(floating);

        Assert.DoesNotContain(floating, harness.Host.FloatingWindows);
        Assert.DoesNotContain(floating, harness.MainWindow.NestedWindows);
    }

    [Fact]
    public void A_Floating_Dock_Window_Closed_From_Its_Title_Bar_Leaves_Its_Host()
    {
        DockHarness harness = DockHarness.Create();
        MGFloatingDockWindow floating = harness.Float(harness.PanelA);
        List<DockPanelNode> removedPanels = new();
        harness.Host.PanelRemoved += (_, panel) => removedPanels.Add(panel);

        // The close button of the window template calls TryCloseWindow.
        Assert.True(floating.IsCloseButtonVisible);
        Assert.True(floating.TryCloseWindow());

        Assert.DoesNotContain(floating, harness.MainWindow.NestedWindows);
        Assert.DoesNotContain(floating, harness.Host.FloatingWindows);
        Assert.Equal(new[] { harness.PanelA }, removedPanels);

        // Closing it again through the host is a no-op.
        harness.Host.CloseFloatingWindow(floating);
        Assert.Equal(new[] { harness.PanelA }, removedPanels);
        Assert.Empty(harness.Host.FloatingWindows);
    }

    [Fact]
    public void The_Color_Picker_Popup_Attaches_Its_Window_When_Opened_And_Detaches_It_When_Closed()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };
        MGBorder anchor = new(window) { PreferredWidth = 120, PreferredHeight = 24 };
        window.SetContent(anchor);
        desktop.Windows.Add(window);
        AdvanceFrame(runtime, desktop, 0);

        MGColorPickerPopup popup = new(window);
        int opened = 0, closed = 0, committed = 0, cancelled = 0;
        popup.PopupOpened += (_, _) => opened++;
        popup.PopupClosed += (_, _) => closed++;
        popup.EditCommitted += (_, _) => committed++;
        popup.EditCancelled += (_, _) => cancelled++;
        Assert.False(popup.PopupWindow.ActivatesOnClick);
        Assert.DoesNotContain(popup.PopupWindow, window.NestedWindows);

        popup.OpenRelativeTo(anchor, popup.Value);
        AdvanceFrame(runtime, desktop, 16);

        Assert.True(popup.IsOpen);
        Assert.Contains(popup.PopupWindow, window.NestedWindows);
        Assert.Same(window, popup.PopupWindow.ParentWindow);
        Assert.Equal(1, opened);

        Assert.True(popup.CancelAndClose());

        Assert.False(popup.IsOpen);
        Assert.DoesNotContain(popup.PopupWindow, window.NestedWindows);
        Assert.Equal((1, 1, 0), (closed, cancelled, committed));
        Assert.False(popup.CancelAndClose());

        popup.OpenRelativeTo(anchor, popup.Value);
        Assert.Contains(popup.PopupWindow, window.NestedWindows);
        popup.CommitAndClose();

        Assert.False(popup.IsOpen);
        Assert.DoesNotContain(popup.PopupWindow, window.NestedWindows);
        Assert.Equal((2, 2, 1, 1), (opened, closed, cancelled, committed));
    }

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs)
    {
        MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(totalElapsedMs), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    /// <summary>A main window hosting a docking host with the panels "A" and "B" in two tab groups, laid out.</summary>
    private sealed class DockHarness
    {
        private int _elapsedMs;

        public GraphTestRuntime Runtime { get; private init; }
        public MGDesktop Desktop { get; private init; }
        public MGWindow MainWindow { get; private init; }
        public MGDockHost Host { get; private init; }
        public DockPanelNode PanelA { get; private init; }
        public DockPanelNode PanelB { get; private init; }

        public static DockHarness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
            MGDesktop desktop = new(runtime);
            MGWindow mainWindow = new(desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };
            DockPanelNode panelA = new() { Title = "A", ContentFactory = () => new MGTextBlock(mainWindow, "Content A") };
            DockPanelNode panelB = new() { Title = "B", ContentFactory = () => new MGTextBlock(mainWindow, "Content B") };

            DockTabGroupNode groupA = new();
            groupA.AddPanel(panelA, -1);
            DockTabGroupNode groupB = new();
            groupB.AddPanel(panelB, -1);

            MGDockHost host = new(mainWindow)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                LayoutModel = new DockLayoutModel(new DockSplitNode
                {
                    Orientation = Orientation.Horizontal,
                    SplitRatio = 0.5f,
                    MinFirstSize = 0,
                    MinSecondSize = 0,
                    FirstChild = groupA,
                    SecondChild = groupB,
                }),
            };
            mainWindow.SetContent(host);
            desktop.Windows.Add(mainWindow);

            DockHarness harness = new() { Runtime = runtime, Desktop = desktop, MainWindow = mainWindow, Host = host, PanelA = panelA, PanelB = panelB };
            harness.Frame();
            harness.Frame();
            return harness;
        }

        public MGFloatingDockWindow Float(DockPanelNode panel)
        {
            MGFloatingDockWindow floating = Host.DetachToFloating(panel, new Point(600, 450));
            Frame();
            Frame();
            return floating;
        }

        public void Frame()
        {
            AdvanceFrame(Runtime, Desktop, _elapsedMs);
            _elapsedMs += 16;
        }
    }
}
