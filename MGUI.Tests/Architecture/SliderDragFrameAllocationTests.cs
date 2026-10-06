using MGUI.Core.UI;
using MGUI.Core.UI.Brushes;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace MGUI.Tests.Architecture;

/// <summary>
/// End to end: one frame of a dragged <see cref="MGSlider"/>, on the slider's own path, allocates nothing. A measured frame runs what a desktop
/// frame runs for that slider: the input update, the mouse and keyboard handlers, the slider's <see cref="MGElement.Update"/> (which updates its
/// own mouse handler, so the drag moves the value and raises <see cref="MGSlider.ValueChangedNonAlloc"/>) and its <see cref="MGSlider.DrawSelf"/>.
/// The rest of a desktop frame (window ordering, window update and draw, layout) is outside this path.
/// </summary>
public class SliderDragFrameAllocationTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADragFrame_OnTheSlidersOwnPath_AllocatesNothing(bool ShowValueLabel)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 200);
        desktop.Windows.Add(window);
        //  Range [0, 1] with "F0": every value below 0.5 shows "0", so a drag between two points of the left half leaves the label text as it is.
        MGSlider slider = new(window, 0, 1, 0.5f) { ShowValueLabel = ShowValueLabel, ValueLabelFormat = "F0" };
        window.SetContent(slider);
        List<float> values = new();
        slider.ValueChangedNonAlloc += (_, e) =>
        {
            if (values.Count < values.Capacity)
            {
                values.Add(e.NewValue);
            }
        };

        //  Real desktop frames until the thumb is pressed: layout, hover, the thumb bounds of a first draw, then the press that starts the drag.
        int elapsed = 0;
        void DesktopFrame(Point position, ButtonState left)
        {
            runtime.ApplyFrame(CreateUpdateArgs(elapsed, position, left));
            desktop.Update();
            desktop.Draw();
            elapsed += 16;
        }

        DesktopFrame(new Point(1, 1), ButtonState.Released);
        DesktopFrame(new Point(1, 1), ButtonState.Released);
        Rectangle bounds = slider.LayoutBounds;
        Point thumb = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, bounds.Center);
        Point first = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 5, bounds.Center.Y));
        Point second = slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 4, bounds.Center.Y));
        DesktopFrame(thumb, ButtonState.Released);
        DesktopFrame(thumb, ButtonState.Released);
        DesktopFrame(thumb, ButtonState.Pressed);
        DesktopFrame(first, ButtonState.Pressed);
        DesktopFrame(second, ButtonState.Pressed);
        Assert.True(slider.IsDraggingThumb);
        string labelText = slider.ValueLabelComponent.Element.Text;

        PaintUpdateRegistry registry = new();
        GraphNoOpDrawTransaction transaction = new(runtime, DrawSettings.Default);
        ElementDrawArgs drawArgs = new(new DrawBaseArgs(TimeSpan.Zero, transaction, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
        UpdateBaseArgs firstArgs = CreateUpdateArgs(0, first, ButtonState.Pressed);
        UpdateBaseArgs secondArgs = CreateUpdateArgs(0, second, ButtonState.Pressed);
        int frame = 0;

        void SliderFrame()
        {
            UpdateBaseArgs args = (frame++ % 2 == 0 ? firstArgs : secondArgs) with { TotalElapsed = TimeSpan.FromMilliseconds(elapsed), PaintRegistry = registry };
            elapsed += 16;
            registry.Clear();
            runtime.ApplyFrame(args);
            runtime.Input.Mouse.UpdateHandlers();
            runtime.Input.Keyboard.UpdateHandlers();
            slider.Update(new ElementUpdateArgs(args, true, false, true, Point.Zero, slider.ActualLayoutBounds));
            slider.DrawSelf(drawArgs, slider.LayoutBounds);
            transaction.FillRectangleCalls.Clear();
            transaction.StrokeAndFillRectangleCalls.Clear();
            transaction.StrokeLineCalls.Clear();
            transaction.FillTriangleCalls.Clear();
            transaction.StrokeAndFillCircleCalls.Clear();
            transaction.DrawTextureToCalls.Clear();
            transaction.DrawTextureAtCalls.Clear();
            transaction.TexturedTriangleListCalls.Clear();
        }

        values.Clear();
        values.Capacity = 8;
        for (int i = 0; i < WarmupIterations; i++)
        {
            SliderFrame();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            SliderFrame();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(8, values.Count);
        Assert.NotEqual(values[0], values[1]);
        Assert.Equal(values[0], values[2]);
        Assert.Same(labelText, slider.ValueLabelComponent.Element.Text);
        Assert.Equal(0, after - before);
    }

    private static UpdateBaseArgs CreateUpdateArgs(int ElapsedMilliseconds, Point Position, ButtonState Left)
        => new(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16),
            new MouseState(Position.X, Position.Y, 0, Left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released),
            new KeyboardState());
}
