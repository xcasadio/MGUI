using System;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Tooling;

/// <summary><see cref="UIToolingService.HitTest"/>, the purely geometric hit test the XAML editor's non-interactive preview
/// uses to resolve a click to an element (slice X4, Docs/Tasks/xaml-editor-tasks.md, section X4; Docs/decisions/0010-xaml-editor-v1.md).
/// One test group per acceptance bullet of that section's hit-test cases.</summary>
public class UIToolingHitTestTests
{
    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create(int width = 960, int height = 540)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, width, height));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, width, height) { WindowStyle = WindowStyle.None };
            desktop.Windows.Add(window);
            Harness harness = new(runtime, desktop, window);
            harness.Frame(0);
            return harness;
        }

        /// <summary>Advances the runtime's clock and runs one desktop update, with the mouse parked off every element (this
        /// class only ever hit-tests through <see cref="UIToolingService.HitTest"/>, never through the real input path, so the
        /// mouse position here does not matter to any of these tests).</summary>
        public void Frame(int frameIndex)
        {
            MouseState mouse = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }

    // -- 1. Deepest element wins, components included --

    [Fact]
    public void HitTest_ReturnsTheDeepestElement_ComponentsIncluded()
    {
        Harness harness = Harness.Create();
        MGNumericUpDown numericUpDown = new(harness.Window)
        {
            PreferredWidth = 120,
            PreferredHeight = 40,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        harness.Window.SetContent(numericUpDown);
        harness.Frame(1);
        harness.Frame(2);

        Assert.True(numericUpDown.TryGetTemplatePart(MGNumericUpDown.IncreaseButtonPartName, out MGElement increaseButton));
        Assert.True(increaseButton.ActualLayoutBounds.Width > 0 && increaseButton.ActualLayoutBounds.Height > 0);

        MGElement hit = UIToolingService.HitTest(harness.Window, increaseButton.ActualLayoutBounds.Center);

        // Deeper than the numeric up/down control itself, and deeper than (or equal to) its increase-button component --
        // that button has its own arrow-icon component, itself included by the same recursive component traversal.
        Assert.NotNull(hit);
        Assert.NotSame(numericUpDown, hit);
        bool reachesIncreaseButton = false;
        for (MGElement current = hit; current != null; current = current.Parent)
        {
            if (current == increaseButton)
            {
                reachesIncreaseButton = true;
                break;
            }
        }
        Assert.True(reachesIncreaseButton, $"Expected the hit element ({hit.GetType().Name}) to be the increase button or one of its own components.");
    }

    // -- 2. Equal depth: the last sibling in the preorder walk wins --

    [Fact]
    public void HitTest_AtEqualDepth_TheLastSiblingWins()
    {
        Harness harness = Harness.Create();
        MGOverlayPanel panel = new(harness.Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        MGBorder first = new(harness.Window) { PreferredWidth = 100, PreferredHeight = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGBorder second = new(harness.Window) { PreferredWidth = 100, PreferredHeight = 100, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        panel.TryAddChild(first);
        panel.TryAddChild(second);
        harness.Window.SetContent(panel);
        harness.Frame(1);
        harness.Frame(2);

        // Fully overlapping siblings, both added at the panel's origin: 'second' is later in document/traversal order.
        Assert.Equal(first.ActualLayoutBounds, second.ActualLayoutBounds);

        MGElement hit = UIToolingService.HitTest(harness.Window, first.ActualLayoutBounds.Center);

        Assert.Same(second, hit);
    }

    // -- 3. A point outside the root returns null; a null root returns null --

    [Fact]
    public void HitTest_PointOutsideRoot_ReturnsNull()
    {
        Harness harness = Harness.Create();
        harness.Frame(1);

        Assert.Null(UIToolingService.HitTest(harness.Window, new Point(-50, -50)));
    }

    [Fact]
    public void HitTest_NullRoot_ReturnsNull()
    {
        Assert.Null(UIToolingService.HitTest(null, Point.Zero));
    }

    // -- 4. Window scale: a point aimed (in screen space) at the visual centre of a child returns that child --

    [Fact]
    public void HitTest_WithAWindowScaleOfTwo_UsesTheScaledPoint()
    {
        Harness harness = Harness.Create();
        harness.Window.Scale = 2f;
        //  Pushed away from the window's top-left corner, which is the scale pivot: a point at the visual centre of a button
        //  sitting on that corner would double onto the button's own unscaled edge, and the setup guard below could not tell
        //  a scaled point from an unscaled one.
        MGBorder button = new(harness.Window)
        {
            PreferredWidth = 100,
            PreferredHeight = 60,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new MonoGame.Extended.Thickness(200, 120, 0, 0),
        };
        harness.Window.SetContent(button);
        harness.Frame(1);
        harness.Frame(2);

        //  The raw point is built by arithmetic, not by the conversion HitTest itself performs: screen space is unscaled space
        //  scaled by Window.Scale about the window's top-left corner (MGWindow.UpdateScaleTransforms). Deriving it with
        //  ConvertCoordinateSpace would be the exact inverse of what HitTest does, so the test would still pass if both
        //  conversions were dropped.
        Rectangle unscaledBounds = button.ActualLayoutBounds;
        Point unscaledCentre = unscaledBounds.Center;
        Point topLeft = harness.Window.TopLeft;
        Point screenPoint = new(topLeft.X + ((unscaledCentre.X - topLeft.X) * 2), topLeft.Y + ((unscaledCentre.Y - topLeft.Y) * 2));

        Assert.False(unscaledBounds.ContainsInclusive(screenPoint),
            "Test setup: at a scale of 2 the raw point must fall outside the unscaled bounds, or a HitTest that skipped the conversion would pass too.");

        MGElement hit = UIToolingService.HitTest(harness.Window, screenPoint);

        Assert.Same(button, hit);

        //  ... and the symmetric negative: a raw point whose unscaled image falls past the button is not the button.
        Point pastTheButton = new(topLeft.X + ((unscaledBounds.Right + 5 - topLeft.X) * 2), topLeft.Y + ((unscaledCentre.Y - topLeft.Y) * 2));
        Assert.NotSame(button, UIToolingService.HitTest(harness.Window, pastTheButton));
    }

    // -- 5. Scrolling and clipping: the point returns the element really visible there; a scrolled-out element is never returned --

    [Fact]
    public void HitTest_InsideAScrolledScrollViewer_ReturnsTheVisibleElement_AndNeverAScrolledOutOne()
    {
        Harness harness = Harness.Create(400, 100);
        MGScrollViewer scrollViewer = new(harness.Window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        MGStackPanel panel = new(harness.Window, Orientation.Vertical)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Spacing = 0,
        };
        MGBorder first = new(harness.Window) { PreferredHeight = 150, HorizontalAlignment = HorizontalAlignment.Stretch };
        MGBorder second = new(harness.Window) { PreferredHeight = 150, HorizontalAlignment = HorizontalAlignment.Stretch };
        panel.TryAddChild(first);
        panel.TryAddChild(second);
        scrollViewer.SetContent(panel);
        harness.Window.SetContent(scrollViewer);
        harness.Frame(1);
        harness.Frame(2);

        Assert.True(scrollViewer.MaxVerticalOffset > 150f, "Test setup: the content must be tall enough to scroll 'first' fully out of view.");
        scrollViewer.VerticalOffset = scrollViewer.MaxVerticalOffset;
        harness.Frame(3);
        harness.Frame(4);

        // 'first' scrolled fully out of the viewport: ActualLayoutBounds collapses to zero size (computed at update, not at draw).
        Assert.True(first.ActualLayoutBounds.Width == 0 || first.ActualLayoutBounds.Height == 0);
        Assert.True(second.ActualLayoutBounds.Width > 0 && second.ActualLayoutBounds.Height > 0);

        MGElement hitOnSecond = UIToolingService.HitTest(harness.Window, second.ActualLayoutBounds.Center);
        Assert.Same(second, hitOnSecond);

        // Aim at the window's own top-left corner, where 'first' used to be laid out before scrolling: it can never be hit now.
        MGElement hitAtFirstsOldPosition = UIToolingService.HitTest(harness.Window, new Point(harness.Window.Left + 10, harness.Window.Top + 5));
        Assert.NotSame(first, hitAtFirstsOldPosition);
    }

    // -- 6. RenderTransform: an element with a translation is reached at its drawn position, not its laid-out one --

    [Fact]
    public void HitTest_WithARenderTransformTranslation_IsReachedAtItsDrawnPosition_NotItsLaidOutOne()
    {
        Harness harness = Harness.Create();
        MGBorder button = new(harness.Window) { PreferredWidth = 100, PreferredHeight = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        harness.Window.SetContent(button);
        harness.Frame(1);
        harness.Frame(2);

        Point laidOutCentre = button.ActualLayoutBounds.Center;
        button.RenderTransform.Translation = new Vector2(150f, 0f);
        harness.Frame(3);
        harness.Frame(4);

        Assert.NotSame(button, UIToolingService.HitTest(harness.Window, laidOutCentre));

        Point drawnCentre = laidOutCentre + new Point(150, 0);
        Assert.Same(button, UIToolingService.HitTest(harness.Window, drawnCentre));
    }

    // -- 7. IsHitTestVisible == false on the root does not stop the hit test (the preview's real situation) --

    [Fact]
    public void HitTest_WithIsHitTestVisibleFalseOnTheRoot_StillHitsTheElement()
    {
        Harness harness = Harness.Create();
        MGBorder button = new(harness.Window) { PreferredWidth = 100, PreferredHeight = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        harness.Window.SetContent(button);
        harness.Window.IsHitTestVisible = false;
        harness.Frame(1);
        harness.Frame(2);

        Assert.False(harness.Window.IsHitTestVisible);

        MGElement hit = UIToolingService.HitTest(harness.Window, button.ActualLayoutBounds.Center);

        Assert.Same(button, hit);
    }
}
