using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using System.Collections.Generic;
using System.Linq;

namespace MGUI.Tests.Controls;

/// <summary>
/// <see cref="MGSlider.DrawSelf"/> draws its hover overlay (the number line pieces beside the thumb, then the thumb) without building a
/// list of the pieces every frame the slider is hovered, pressed or dragged.<para/>
/// The allocation test isolates that overlay code. Its slider is not hit-test visible: the mouse is still over it, so
/// <see cref="MGElement.IsHovered"/> is true and the overlay branch runs, but its <see cref="MGElement.VisualState"/> stays
/// <see cref="SecondaryVisualState.None"/>, so the overlay brushes are null. The slider's own border brushes are removed as well:
/// a border drawn by <see cref="MGUI.Core.UI.Brushes.BorderBrushes.MGUniformBorderBrush"/> allocates in <c>ThicknessUtils.IsEmpty</c>,
/// outside this control.
/// </summary>
public class SliderDrawAllocationTests
{
    private const int WarmupIterations = 2000;
    private const int MeasuredIterations = 1000;

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void DrawSelf_WhileHovered_DrawsTheOverlayOnBothPiecesThenOnTheThumb(Orientation Orientation)
    {
        Harness harness = Harness.Create(Orientation);
        harness.MoveMouseOverSlider();
        harness.MoveMouseOverSlider();
        Assert.Equal(SecondaryVisualState.Hovered, harness.Slider.VisualState.Secondary);
        Color overlayColor = harness.Slider.FocusBrush.GetFillOverlay(SecondaryVisualState.Hovered).Color;

        List<RectangleF> overlays = harness.DrawAndCollectFills()
            .Where(x => x.Color == overlayColor && x.Destination.Width > 1 && x.Destination.Height > 1)
            .Select(x => x.Destination)
            .ToList();

        Assert.Equal(3, overlays.Count);
        RectangleF before = overlays[0];
        RectangleF after = overlays[1];
        RectangleF thumb = overlays[2];
        if (Orientation == Orientation.Horizontal)
        {
            Assert.Equal(thumb.Left, before.Right);
            Assert.Equal(thumb.Right, after.Left);
            Assert.Equal(before.Top, after.Top);
        }
        else
        {
            Assert.Equal(thumb.Top, before.Bottom);
            Assert.Equal(thumb.Bottom, after.Top);
            Assert.Equal(before.Left, after.Left);
        }
    }

    [Theory]
    [InlineData(Orientation.Horizontal)]
    [InlineData(Orientation.Vertical)]
    public void DrawSelf_TheHoverOverlayCode_AllocatesNothing(Orientation Orientation)
    {
        Harness harness = Harness.Create(Orientation);
        harness.Slider.NumberLineBorderBrush = null;
        harness.Slider.ThumbBorderBrush = null;
        harness.Slider.IsHitTestVisible = false;
        harness.MoveMouseOverSlider();
        Assert.True(harness.Slider.IsHovered);
        Assert.Equal(SecondaryVisualState.None, harness.Slider.VisualState.Secondary);

        for (int i = 0; i < WarmupIterations; i++)
        {
            harness.Draw();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            harness.Draw();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    private sealed class Harness
    {
        private readonly GraphTestRuntime Runtime;
        private readonly MGDesktop Desktop;
        private readonly GraphNoOpDrawTransaction Transaction;
        private readonly ElementDrawArgs DrawArgs;
        private int ElapsedMilliseconds;

        public MGSlider Slider { get; }

        private Harness(GraphTestRuntime runtime, MGDesktop desktop, MGSlider slider)
        {
            Runtime = runtime;
            Desktop = desktop;
            Slider = slider;
            Transaction = new GraphNoOpDrawTransaction(runtime, DrawSettings.Default);
            DrawArgs = new ElementDrawArgs(new DrawBaseArgs(TimeSpan.Zero, Transaction, 1f),
                new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
        }

        public static Harness Create(Orientation orientation)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 300, 300);
            desktop.Windows.Add(window);

            MGSlider slider = new(window, 0, 100, 50, false, null, MGUI.Core.UI.Brushes.BorderBrushes.MGUniformBorderBrush.Black, orientation);
            window.SetContent(slider);

            Harness harness = new(runtime, desktop, slider);
            harness.AdvanceFrame(new Point(1, 1));
            harness.AdvanceFrame(new Point(1, 1));
            return harness;
        }

        /// <summary>Runs one desktop update with the mouse over the number line, a quarter of the way along, away from the thumb.</summary>
        public void MoveMouseOverSlider()
        {
            Rectangle bounds = Slider.LayoutBounds;
            Point layoutPoint = Slider.Orientation == Orientation.Horizontal
                ? new Point(bounds.Left + bounds.Width / 4, bounds.Center.Y)
                : new Point(bounds.Center.X, bounds.Top + bounds.Height / 4);
            AdvanceFrame(Slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, layoutPoint));
        }

        private void AdvanceFrame(Point screenPosition)
        {
            MouseState mouse = new(screenPosition.X, screenPosition.Y, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(ElapsedMilliseconds), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
            ElapsedMilliseconds += 16;
        }

        public void Draw()
        {
            Slider.DrawSelf(DrawArgs, Slider.LayoutBounds);
            ClearRecordedCalls();
        }

        public List<GraphFillRectangleCall> DrawAndCollectFills()
        {
            Slider.DrawSelf(DrawArgs, Slider.LayoutBounds);
            List<GraphFillRectangleCall> fills = new(Transaction.FillRectangleCalls);
            ClearRecordedCalls();
            return fills;
        }

        /// <summary>The test transaction records every call; clearing keeps each list's capacity, so a measured draw does not grow them.</summary>
        private void ClearRecordedCalls()
        {
            Transaction.FillRectangleCalls.Clear();
            Transaction.StrokeAndFillRectangleCalls.Clear();
            Transaction.StrokeLineCalls.Clear();
            Transaction.FillTriangleCalls.Clear();
            Transaction.StrokeAndFillCircleCalls.Clear();
            Transaction.DrawTextureToCalls.Clear();
            Transaction.DrawTextureAtCalls.Clear();
            Transaction.TexturedTriangleListCalls.Clear();
        }
    }
}
