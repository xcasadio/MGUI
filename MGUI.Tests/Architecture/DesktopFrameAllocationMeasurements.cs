using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit.Abstractions;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Measurement bench of a desktop frame: how many bytes <see cref="MGDesktop.Update"/> and <see cref="MGDesktop.Draw(IUIDrawTransaction, float)"/>
/// allocate per frame, on fixtures that grow one element at a time, so the difference between two rows is the cost of the element added.<para/>
/// These tests write their measurements to the test output and assert that each one is zero.
/// The draw goes through a <see cref="NonRecordingDrawTransaction"/>, which allocates nothing of its own; the recording transaction that
/// <see cref="GraphTestRuntime.BeginDrawTransaction"/> creates on every draw would count as desktop allocations.
/// </summary>
public class DesktopFrameAllocationMeasurements
{
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper Output;

    public DesktopFrameAllocationMeasurements(ITestOutputHelper output)
    {
        Output = output;
    }

    public enum Fixture
    {
        NoWindow,
        BareWindow,
        DefaultWindow,
        WindowWithStackPanel,
        WindowWithStackPanelAndTextBlock,
        WindowWithStackPanelAndSlider,
        Full,
        /// <summary>The <see cref="Full"/> content in the second row and second column of a 2 rows by 2 columns <see cref="MGGrid"/>.</summary>
        FullInGrid,
        /// <summary>The <see cref="Full"/> content in an <see cref="MGDockPanel"/>.</summary>
        FullInDockPanel,
        /// <summary>The <see cref="Full"/> content as the only panel of the only tab group of an <see cref="MGDockHost"/>.</summary>
        FullInDockHost
    }

    [Theory]
    [InlineData(Fixture.NoWindow)]
    [InlineData(Fixture.BareWindow)]
    [InlineData(Fixture.DefaultWindow)]
    [InlineData(Fixture.WindowWithStackPanel)]
    [InlineData(Fixture.WindowWithStackPanelAndTextBlock)]
    [InlineData(Fixture.WindowWithStackPanelAndSlider)]
    [InlineData(Fixture.Full)]
    [InlineData(Fixture.FullInGrid)]
    [InlineData(Fixture.FullInDockPanel)]
    [InlineData(Fixture.FullInDockHost)]
    public void AnIdleFrame(Fixture fixture)
    {
        Harness harness = Harness.Create(fixture);
        Point outside = new(600, 340);

        long update = Measure(() => harness.Update(outside, ButtonState.Released));
        long draw = Measure(() => harness.Draw());

        Assert.True(fixture == Fixture.NoWindow || harness.Transaction.CallCount > 0);
        Output.WriteLine($"idle {fixture}: Update {update} B/frame, Draw {draw} B/frame");

        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    [Theory]
    [InlineData(Fixture.Full)]
    [InlineData(Fixture.FullInGrid)]
    [InlineData(Fixture.FullInDockPanel)]
    [InlineData(Fixture.FullInDockHost)]
    public void ADragFrame(Fixture fixture)
    {
        Harness harness = Harness.Create(fixture);
        MGSlider slider = harness.Slider;
        //  "F0" on [0, 1]: every value below 0.5 shows "0", so the drag does not change the label text.
        Rectangle bounds = slider.LayoutBounds;
        Point thumb = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, bounds.Center);
        Point first = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 5, bounds.Center.Y));
        Point second = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 4, bounds.Center.Y));
        harness.Frame(thumb, ButtonState.Released);
        harness.Frame(thumb, ButtonState.Released);
        harness.Frame(thumb, ButtonState.Pressed);
        harness.Frame(first, ButtonState.Pressed);
        harness.Frame(second, ButtonState.Pressed);
        Assert.True(slider.IsDraggingThumb);

        int frame = 0;
        long update = Measure(() => harness.Update(frame++ % 2 == 0 ? first : second, ButtonState.Pressed));
        long draw = Measure(() => harness.Draw());

        Assert.True(slider.IsDraggingThumb);
        Output.WriteLine($"drag {fixture}: Update {update} B/frame, Draw {draw} B/frame");
        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    [Fact]
    public void DrawSelf_OfEachElementOfTheFullFixture()
    {
        Harness harness = Harness.Create(Fixture.Full);
        ElementDrawArgs drawArgs = new(new DrawBaseArgs(TimeSpan.Zero, harness.Transaction, 1f),
            new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);

        long panel = Measure(() => harness.Panel.DrawSelf(drawArgs, harness.Panel.LayoutBounds));
        long text = Measure(() => harness.TextBlock.DrawSelf(drawArgs, harness.TextBlock.LayoutBounds));
        long slider = Measure(() => harness.Slider.DrawSelf(drawArgs, harness.Slider.LayoutBounds));
        long window = Measure(() => harness.Window.Draw(drawArgs));

        Output.WriteLine($"DrawSelf: MGStackPanel {panel} B, MGTextBlock {text} B, MGSlider {slider} B; MGWindow.Draw {window} B");
        Assert.Equal(0, panel);
        Assert.Equal(0, text);
        Assert.Equal(0, slider);
        Assert.Equal(0, window);
    }

    internal static long Measure(Action action)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            action();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            action();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        return (after - before) / MeasuredIterations;
    }

    internal sealed class Harness
    {
        private readonly GraphTestRuntime Runtime;
        private readonly MGDesktop Desktop;
        private int ElapsedMilliseconds;

        public NonRecordingDrawTransaction Transaction { get; }
        public MGWindow Window { get; private set; }
        public MGStackPanel Panel { get; private set; }
        public MGTextBlock TextBlock { get; private set; }
        public MGSlider Slider { get; private set; }

        private Harness()
        {
            Runtime = new GraphTestRuntime(new Rectangle(0, 0, 640, 360));
            Desktop = new MGDesktop(Runtime);
            Transaction = new NonRecordingDrawTransaction(Runtime, DrawSettings.Default);
        }

        public static Harness Create(Fixture fixture)
        {
            Harness harness = new();
            if (fixture != Fixture.NoWindow)
            {
                MGWindow window = new(harness.Desktop, 0, 0, 300, 200);
                harness.Window = window;
                if (fixture == Fixture.BareWindow)
                {
                    window.IsTitleBarVisible = false;
                    window.IsCloseButtonVisible = false;
                }

                harness.Desktop.Windows.Add(window);
                if (fixture >= Fixture.WindowWithStackPanel)
                {
                    bool full = fixture >= Fixture.Full;
                    bool withText = fixture == Fixture.WindowWithStackPanelAndTextBlock || full;
                    bool withSlider = fixture == Fixture.WindowWithStackPanelAndSlider || full;
                    if (fixture == Fixture.FullInDockHost)
                    {
                        harness.CreateDockHostContent(window);
                    }
                    else
                    {
                        MGStackPanel panel = harness.CreateStackPanel(window, withText, withSlider);
                        window.SetContent(fixture switch
                        {
                            Fixture.FullInGrid => harness.CreateGrid(window, panel),
                            Fixture.FullInDockPanel => harness.CreateDockPanel(window, panel),
                            _ => panel
                        });
                    }
                }
            }

            harness.Frame(new Point(600, 340), ButtonState.Released);
            harness.Frame(new Point(600, 340), ButtonState.Released);
            return harness;
        }

        private MGStackPanel CreateStackPanel(MGWindow window, bool withText, bool withSlider)
        {
            MGStackPanel panel = new(window, Orientation.Vertical);
            Panel = panel;
            if (withText)
            {
                TextBlock = new MGTextBlock(window, "Value");
                panel.TryAddChild(TextBlock);
            }

            if (withSlider)
            {
                Slider = new MGSlider(window, 0, 1, 0.5f) { ShowValueLabel = true, ValueLabelFormat = "F0" };
                panel.TryAddChild(Slider);
            }

            return panel;
        }

        /// <summary>A grid of 2 rows and 2 columns holding the content in its second row and second column.</summary>
        private MGGrid CreateGrid(MGWindow window, MGElement content)
        {
            MGGrid grid = new(window);
            grid.AddRow(GridLength.CreateWeightedLength(1));
            grid.AddRow(GridLength.CreateWeightedLength(1));
            grid.AddColumn(GridLength.CreateWeightedLength(1));
            grid.AddColumn(GridLength.CreateWeightedLength(1));
            Assert.True(grid.TryAddChild(1, 1, content));
            return grid;
        }

        private MGDockPanel CreateDockPanel(MGWindow window, MGElement content)
        {
            MGDockPanel dockPanel = new(window);
            Assert.True(dockPanel.TryAddChild(content, Dock.Top));
            return dockPanel;
        }

        /// <summary>A dock host with one tab group holding one panel; the host builds the content of the panel when it needs it.</summary>
        private void CreateDockHostContent(MGWindow window)
        {
            DockPanelNode panelNode = new() { Title = "Full", ContentFactory = () => CreateStackPanel(window, true, true) };
            DockTabGroupNode group = new();
            group.AddPanel(panelNode, -1);
            MGDockHost host = new(window)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                LayoutModel = new DockLayoutModel(group)
            };
            foreach (DockPanelNode node in host.LayoutModel.GetAllPanels())
            {
                host.RegisterPanel(node);
            }

            window.SetContent(host);
        }

        public void Update(Point position, ButtonState left)
        {
            MouseState mouse = new(position.X, position.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
            ElapsedMilliseconds += 16;
        }

        public void Draw() => Desktop.Draw(Transaction);

        public void Draw(IUIDrawTransaction transaction) => Desktop.Draw(transaction);

        /// <summary>Two mouse positions of the <see cref="Fixture.DefaultWindow"/> found by hit-test: over the title bar (not over its close button) and over the
        /// close button. Each one is checked by an update: the hovered element of the window is under the position, and the close button is in the hovered state
        /// for the second one only. The harness is left with the mouse outside the window.</summary>
        public (Point OverTheTitleBar, Point OverTheCloseButton) FindTitleBarPoints()
        {
            MGButton close = Window.CloseButtonElement;
            Rectangle closeBounds = close.LayoutBounds;
            Point overTheCloseButton = close.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, closeBounds.Center);
            Point overTheTitleBar = close.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(closeBounds.Left - 20, closeBounds.Center.Y));

            Update(overTheCloseButton, ButtonState.Released);
            Assert.NotNull(Window.HoveredElement);
            Assert.True(close.IsSelfOrAncestorOf(Window.HoveredElement), "the position is not over the close button");
            Assert.Equal(SecondaryVisualState.Hovered, close.VisualState.Secondary);

            Update(overTheTitleBar, ButtonState.Released);
            Assert.NotNull(Window.HoveredElement);
            Assert.False(close.IsSelfOrAncestorOf(Window.HoveredElement), "the position is over the close button");
            Assert.NotSame(Window, Window.HoveredElement);
            Assert.NotEqual(SecondaryVisualState.Hovered, close.VisualState.Secondary);

            Update(new Point(600, 340), ButtonState.Released);
            return (overTheTitleBar, overTheCloseButton);
        }

        public GraphTestRuntime GetRuntime() => Runtime;

        public MGDesktop GetDesktop() => Desktop;

        public void Frame(Point position, ButtonState left)
        {
            Update(position, left);
            Draw();
        }
    }
}
