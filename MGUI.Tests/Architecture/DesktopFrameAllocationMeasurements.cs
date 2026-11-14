using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit.Abstractions;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Measurement bench of a desktop frame: how many bytes <see cref="MGDesktop.Update"/> and <see cref="MGDesktop.Draw(IUIDrawTransaction, float)"/>
/// allocate per frame, on fixtures that grow one element at a time, so the difference between two rows is the cost of the element added.<para/>
/// These tests assert no number: they write the measurements to the test output, and the plan of the allocation-free desktop frame copies them.
/// The draw goes through a <see cref="NonRecordingDrawTransaction"/>, which allocates nothing of its own; the recording transaction that
/// <see cref="GraphTestRuntime.CreateDrawTransaction"/> creates on every draw would count as desktop allocations.
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
        Full
    }

    [Theory]
    [InlineData(Fixture.NoWindow)]
    [InlineData(Fixture.BareWindow)]
    [InlineData(Fixture.DefaultWindow)]
    [InlineData(Fixture.WindowWithStackPanel)]
    [InlineData(Fixture.WindowWithStackPanelAndTextBlock)]
    [InlineData(Fixture.WindowWithStackPanelAndSlider)]
    [InlineData(Fixture.Full)]
    public void AnIdleFrame(Fixture fixture)
    {
        Harness harness = Harness.Create(fixture);
        Point outside = new(600, 340);

        long update = Measure(() => harness.Update(outside, ButtonState.Released));
        long draw = Measure(() => harness.Draw());

        Assert.True(fixture == Fixture.NoWindow || harness.Transaction.CallCount > 0);
        Output.WriteLine($"idle {fixture}: Update {update} B/frame, Draw {draw} B/frame");
    }

    [Fact]
    public void ADragFrame_OnTheFullFixture()
    {
        Harness harness = Harness.Create(Fixture.Full);
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
        Output.WriteLine($"drag {Fixture.Full}: Update {update} B/frame, Draw {draw} B/frame");
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
    }

    private static long Measure(Action action)
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

    private sealed class Harness
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
                    MGStackPanel panel = new(window, Orientation.Vertical);
                    harness.Panel = panel;
                    if (fixture == Fixture.WindowWithStackPanelAndTextBlock || fixture == Fixture.Full)
                    {
                        harness.TextBlock = new MGTextBlock(window, "Value");
                        panel.TryAddChild(harness.TextBlock);
                    }

                    if (fixture == Fixture.WindowWithStackPanelAndSlider || fixture == Fixture.Full)
                    {
                        harness.Slider = new MGSlider(window, 0, 1, 0.5f) { ShowValueLabel = true, ValueLabelFormat = "F0" };
                        panel.TryAddChild(harness.Slider);
                    }

                    window.SetContent(panel);
                }
            }

            harness.Frame(new Point(600, 340), ButtonState.Released);
            harness.Frame(new Point(600, 340), ButtonState.Released);
            return harness;
        }

        public void Update(Point position, ButtonState left)
        {
            MouseState mouse = new(position.X, position.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
            ElapsedMilliseconds += 16;
        }

        public void Draw() => Desktop.Draw(Transaction);

        public void Frame(Point position, ButtonState left)
        {
            Update(position, left);
            Draw();
        }
    }
}
