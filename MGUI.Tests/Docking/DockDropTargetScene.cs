using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Tests.Architecture;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Docking;

/// <summary>
/// The dock host of the drop target tests: group A on the left with two tabs (A1, A2), and on the right group B above group C (one tab each), a split that runs
/// left to right and a split that runs top to bottom, so that both kinds of splitter bar exist. The drag of the tests is the one of A1, a tab of a group that has a
/// second tab (so that a reorder of the tabs of its own group has an index to compute).
/// </summary>
internal sealed class DockDropTargetScene
{
    /// <summary>The side of the square of a joystick indicator (<c>MGThemeDockingSettings.DropIndicatorZoneSize</c>, 40) and the gap between two squares.</summary>
    private const int ZoneSize = 40;
    private const int ZoneSpacing = 4;

    public ControlBench Bench { get; } = new(600, 340);
    public MGDockHost Host { get; private set; }
    public DockPanelNode PanelA1 { get; private set; }
    public DockPanelNode PanelA2 { get; private set; }
    public DockTabGroupNode GroupA { get; private set; }
    public DockTabGroupNode GroupB { get; private set; }
    public DockTabGroupNode GroupC { get; private set; }

    public static DockDropTargetScene Create()
    {
        DockDropTargetScene scene = new();
        MGWindow window = scene.Bench.Window;
        DockPanelNode Panel(string title) => new() { Title = title, ContentFactory = () => new MGTextBlock(window, "Content " + title) };

        scene.PanelA1 = Panel("A1");
        scene.PanelA2 = Panel("A2");
        scene.GroupA = new DockTabGroupNode();
        scene.GroupA.AddPanel(scene.PanelA1, -1);
        scene.GroupA.AddPanel(scene.PanelA2, -1);
        scene.GroupB = new DockTabGroupNode();
        scene.GroupB.AddPanel(Panel("B1"), -1);
        scene.GroupC = new DockTabGroupNode();
        scene.GroupC.AddPanel(Panel("C1"), -1);

        DockSplitNode right = new()
        {
            Orientation = Orientation.Vertical,
            SplitRatio = 0.5f,
            MinFirstSize = 0,
            MinSecondSize = 0,
            FirstChild = scene.GroupB,
            SecondChild = scene.GroupC
        };
        DockSplitNode root = new()
        {
            Orientation = Orientation.Horizontal,
            SplitRatio = 0.5f,
            MinFirstSize = 0,
            MinSecondSize = 0,
            FirstChild = scene.GroupA,
            SecondChild = right
        };
        scene.Host = new MGDockHost(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(root)
        };
        foreach (DockPanelNode node in scene.Host.LayoutModel.GetAllPanels())
        {
            scene.Host.RegisterPanel(node);
        }

        scene.Bench.Show(scene.Host);
        return scene;
    }

    public MGDockTabGroup VisualGroup(DockTabGroupNode node)
    {
        foreach (MGDockTabGroup group in Host.GetAllVisibleTabGroups())
        {
            if (ReferenceEquals(group.GroupNode, node))
            {
                return group;
            }
        }

        throw new InvalidOperationException("the tab group is not visible");
    }

    public Rectangle Bounds(DockTabGroupNode node) => VisualGroup(node).LayoutBounds;

    /// <summary>The centre of the tab of A1: where the press that starts the drag is made.</summary>
    public Point TabA1 => TabCenter(GroupA, 0);

    /// <summary>The centre of the tab of A2 (the second tab of group A): a position over the tab headers of the group of the dragged tab.</summary>
    public Point TabA2 => TabCenter(GroupA, 1);

    private Point TabCenter(DockTabGroupNode node, int index)
    {
        int i = 0;
        foreach (MGDockTabItem item in VisualGroup(node).TraverseVisualTree<MGDockTabItem>(IncludeSelf: false))
        {
            if (i++ == index)
            {
                return item.LayoutBounds.Center;
            }
        }

        throw new InvalidOperationException("the tab does not exist");
    }

    /// <summary>The position of a square of the joystick of <paramref name="node"/>: the centre square for <see cref="DockZone.Center"/>, the one next to it for the other zones.</summary>
    public Point Joystick(DockTabGroupNode node, DockZone zone)
    {
        Point c = Bounds(node).Center;
        int step = ZoneSize + ZoneSpacing;
        return zone switch
        {
            DockZone.Left => new Point(c.X - step, c.Y),
            DockZone.Right => new Point(c.X + step, c.Y),
            DockZone.Top => new Point(c.X, c.Y - step),
            DockZone.Bottom => new Point(c.X, c.Y + step),
            _ => c
        };
    }

    /// <summary>The centre of the square of the host-edge indicator of <paramref name="zone"/>.</summary>
    public Point HostEdge(DockZone zone)
    {
        Rectangle b = Host.LayoutBounds;
        return zone switch
        {
            DockZone.Left => new Point(b.X + ZoneSize / 2, b.Center.Y),
            DockZone.Right => new Point(b.Right - ZoneSize / 2, b.Center.Y),
            DockZone.Top => new Point(b.Center.X, b.Y + ZoneSize / 2),
            _ => new Point(b.Center.X, b.Bottom - ZoneSize / 2)
        };
    }

    /// <summary>The split container whose model node is the root split (<paramref name="inner"/> false) or the split of B above C (true).</summary>
    private MGDockSplitContainer Container(bool inner)
    {
        DockNode wanted = inner ? ((DockSplitNode)Host.LayoutModel.RootNode).SecondChild : Host.LayoutModel.RootNode;
        foreach (MGDockSplitContainer container in Host.TraverseVisualTree<MGDockSplitContainer>(IncludeSelf: false))
        {
            if (ReferenceEquals(container.ModelNode, wanted))
            {
                return container;
            }
        }

        throw new InvalidOperationException("the split container is not visible");
    }

    /// <summary>A position on the splitter bar of the root split (the bar between A and the groups B and C), on its first half (<paramref name="firstHalf"/>: next to A) or its second half.</summary>
    public Point RootBar(bool firstHalf) => OnBar(Container(false).SplitterBarLayoutBounds, vertical: true, firstHalf);

    /// <summary>A position on the splitter bar between B and C, on its first half (next to B) or its second half (next to C).</summary>
    public Point InnerBar(bool firstHalf) => OnBar(Container(true).SplitterBarLayoutBounds, vertical: false, firstHalf);

    private static Point OnBar(Rectangle bar, bool vertical, bool firstHalf)
    {
        //  The half is read as "before the middle of the bar" by the host: the first pixel is in the first half, the last pixel is in the second.
        return vertical
            ? new Point(firstHalf ? bar.X : bar.Right - 1, bar.Center.Y)
            : new Point(bar.Center.X, firstHalf ? bar.Y : bar.Bottom - 1);
    }

    /// <summary>Presses on the tab of A1, then moves the mouse towards the middle of the host until the drag starts. Frames: press, moved.</summary>
    public void PressAndStartTheDrag()
    {
        Bench.Mouse = TabA1;
        Bench.Frames(2);
        Bench.Left = ButtonState.Pressed;
        Bench.Frame();
        Bench.Mouse = new Point((TabA1.X + Bounds(GroupB).Center.X) / 2, (TabA1.Y + Bounds(GroupB).Center.Y) / 2);
        Bench.Frame();
    }

    /// <summary>The layout of the model as text: a split is <c>S(orientation,first,second)</c>, a group <c>G[titles]</c>.</summary>
    public static string Describe(DockNode node) => node switch
    {
        null => "-",
        DockTabGroupNode group => "G[" + string.Join(",", group.Panels.Select(p => p.Title)) + "]",
        DockSplitNode split => $"S({split.Orientation},{Describe(split.FirstChild)},{Describe(split.SecondChild)})",
        DockPanelNode panel => "P" + panel.Title,
        _ => node.GetType().Name
    };

    public string DescribeLayout() => Describe(Host.LayoutModel.RootNode) + $" floating={Host.FloatingWindows.Count}";
}
