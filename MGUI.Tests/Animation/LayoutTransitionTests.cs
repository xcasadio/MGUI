using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Thickness = MonoGame.Extended.Thickness;

namespace MGUI.Tests.Animation;

/// <summary>Slice Y4 (Docs/decisions/0011-animation-v5.md, "Transition de layout: mouvement"): <see cref="UILayoutTransition"/>,
/// <see cref="MGElement.LayoutTransition"/>, the internal unregistered run, the ambient pass stack (<see cref="MGDesktop"/>), the trigger
/// conditions and the cost budget.</summary>
public class LayoutTransitionTests
{
    private const int FrameMilliseconds = 16;

    private static UILayoutTransition Transition(int milliseconds = 200) => new() { Duration = TimeSpan.FromMilliseconds(milliseconds) };

    [Fact]
    public void Duration_RefusesNegativeValues()
    {
        UILayoutTransition settings = new();
        Assert.Throws<ArgumentOutOfRangeException>(() => settings.Duration = TimeSpan.FromMilliseconds(-1));
    }

    [Fact]
    public void Duration_ZeroMeansNoTransition_LikeANullSettingsObject()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        top.LayoutTransition = new UILayoutTransition { Duration = TimeSpan.Zero };
        scene.AddButton();
        scene.Frames(2);

        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 });
        scene.Frames(1);

        Assert.Equal(Vector2.Zero, top.LayoutOffset);
        Assert.False(top.HasActiveLayoutOffset);
    }

    [Fact]
    public void ReadingOrWritingAnAnimationTarget_NeverOptsAnElementIn()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        scene.AddButton();
        scene.Frames(2);

        // Touching Opacity (a registered target) must never raise the desktop's sticky flag by itself.
        top.Animations.Start(new UIPropertyAnimation<float>("Opacity") { To = 0.5f, Duration = TimeSpan.FromMilliseconds(50) });
        scene.Frames(1);

        Assert.False(scene.Desktop.HasLayoutTransitions);
        Assert.Null(top.AnimationSlotOrNull.LayoutTransformOrNull);
    }

    // ---- Insertion / removal / reorder in an MGStackPanel -------------------------------------------------------

    [Fact]
    public void Insertion_GlidesTheOptedNeighbourFromItsOldPosition_AndEndsAtIdentity()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        top.LayoutTransition = Transition();
        MGButton bottom = scene.AddButton();
        scene.Frames(2);

        Point previousTopLocation = top.LayoutBounds.Location;
        MGButton inserted = new(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 };
        // The freshly inserted element opts in too: it must never play on the very pass that attaches it.
        inserted.LayoutTransition = Transition();
        scene.Panel.TryInsertChild(0, inserted);
        scene.Frames(1);

        Point newTopLocation = top.LayoutBounds.Location;
        Assert.NotEqual(previousTopLocation, newTopLocation);
        Vector2 expectedOffset = new(previousTopLocation.X - newTopLocation.X, previousTopLocation.Y - newTopLocation.Y);

        // LayoutBounds are the new ones immediately, on the very first frame.
        Assert.Equal(newTopLocation, top.LayoutBounds.Location);
        Assert.Equal(expectedOffset, top.LayoutOffset);
        Assert.True(top.HasActiveLayoutOffset);

        // The inserted element itself plays nothing (never laid out under this parent before).
        Assert.Equal(Vector2.Zero, inserted.LayoutOffset);
        Assert.False(inserted.HasActiveLayoutOffset);

        // A sibling that never moved (bottom, unaffected by an insertion above top) plays nothing either.
        Assert.False(bottom.HasActiveLayoutOffset);

        scene.Frames(20);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);
        Assert.False(top.HasActiveLayoutOffset);
        Assert.Equal(0, scene.Desktop.ActiveRenderTransformCount);
    }

    [Fact]
    public void Removal_GlidesTheRemainingOptedNeighbour_AndTheRemovedElementItselfPlaysNothing()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        MGButton middle = scene.AddButton();
        middle.LayoutTransition = Transition();
        MGButton bottom = scene.AddButton();
        bottom.LayoutTransition = Transition();
        scene.Frames(2);

        Point previousBottomLocation = bottom.LayoutBounds.Location;
        scene.Panel.TryRemoveChild(middle);
        scene.Frames(1);

        Point newBottomLocation = bottom.LayoutBounds.Location;
        Vector2 expectedOffset = new(previousBottomLocation.X - newBottomLocation.X, previousBottomLocation.Y - newBottomLocation.Y);
        Assert.Equal(expectedOffset, bottom.LayoutOffset);
        Assert.True(bottom.HasActiveLayoutOffset);

        // The removed element is detached: its animation slot is cleared and it plays nothing.
        Assert.False(middle.HasActiveLayoutOffset);
        Assert.Null(top.AnimationSlotOrNull);
    }

    [Fact]
    public void Reorder_RemoveThenInsert_PlaysNothingOnTheMovedElement()
    {
        Scene scene = Scene.Build();
        MGButton first = scene.AddButton();
        first.LayoutTransition = Transition();
        MGButton second = scene.AddButton();
        scene.Frames(2);

        // A reorder is a removal followed by an insertion (MGStackPanel has no reorder API, ADR-0011 context).
        scene.Panel.TryRemoveChild(first);
        scene.Panel.TryInsertChild(1, first);
        scene.Frames(1);

        Assert.False(first.HasActiveLayoutOffset);
        Assert.Equal(Vector2.Zero, first.LayoutOffset);
    }

    // ---- MGExpander -------------------------------------------------------------------------------------------

    [Fact]
    public void ExpandingAnExpander_GlidesTheOptedNeighbourBelow_TheExpanderItselfPlaysNothing()
    {
        Scene scene = Scene.Build();
        MGExpander expander = new(scene.Window, IsExpanded: false) { PreferredWidth = 120 };
        expander.LayoutTransition = Transition();
        expander.SetContent(new MGButton(scene.Window) { PreferredWidth = 100, PreferredHeight = 80 });
        scene.Panel.TryAddChild(expander);
        MGButton below = scene.AddButton();
        below.LayoutTransition = Transition();
        scene.Frames(2);

        Point previousExpanderLocation = expander.LayoutBounds.Location;
        Point previousBelowLocation = below.LayoutBounds.Location;

        expander.IsExpanded = true;
        scene.Frames(1);

        // The expander grows in place: its own Location is unchanged, so it never starts a run of its own.
        Assert.Equal(previousExpanderLocation, expander.LayoutBounds.Location);
        Assert.False(expander.HasActiveLayoutOffset);

        Point newBelowLocation = below.LayoutBounds.Location;
        Assert.NotEqual(previousBelowLocation, newBelowLocation);
        Vector2 expectedOffset = new(previousBelowLocation.X - newBelowLocation.X, previousBelowLocation.Y - newBelowLocation.Y);
        Assert.Equal(expectedOffset, below.LayoutOffset);
        Assert.True(below.HasActiveLayoutOffset);
    }

    // ---- No-run conditions --------------------------------------------------------------------------------------

    [Fact]
    public void NoRun_OnTheFirstLayout()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        top.LayoutTransition = Transition();
        scene.Frames(2);

        Assert.False(top.HasActiveLayoutOffset);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);
    }

    [Fact]
    public void NoRun_OnAWindowMove()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        top.LayoutTransition = Transition();
        scene.AddButton();
        scene.Frames(2);

        scene.Window.Left += 50;
        scene.Window.Top += 30;
        scene.Frames(1);

        Assert.False(top.HasActiveLayoutOffset);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void NoRun_OnScrolling()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 200, 100) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGScrollViewer scrollViewer = new(window, ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0),
        };
        MGStackPanel panel = new(window, Orientation.Vertical) { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGButton opted = null;
        for (int i = 0; i < 10; i++)
        {
            MGButton item = new(window) { PreferredWidth = 200, PreferredHeight = 40 };
            if (i == 3)
            {
                opted = item;
                item.LayoutTransition = Transition();
            }
            panel.TryAddChild(item);
        }
        scrollViewer.SetContent(panel);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        int frameIndex = 0;
        void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                frameIndex++;
                MouseState state = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                desktop.Update();
            }
        }
        Frames(2);

        Rectangle before = opted!.LayoutBounds;
        scrollViewer.VerticalOffset = 100f;
        Frames(1);

        Assert.Equal(before, opted.LayoutBounds);
        Assert.False(opted.HasActiveLayoutOffset);
    }

    [Fact]
    public void NoRun_AfterReattachmentToAnotherParent()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGStackPanel outer = new(window, Orientation.Horizontal) { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGStackPanel left = new(window, Orientation.Vertical) { Spacing = 0, VerticalAlignment = VerticalAlignment.Top };
        MGStackPanel right = new(window, Orientation.Vertical) { Spacing = 0, VerticalAlignment = VerticalAlignment.Top };
        outer.TryAddChild(left);
        outer.TryAddChild(right);
        window.SetContent(outer);
        desktop.Windows.Add(window);

        // A filler keeps "left" at a non-zero width once "element" moves out of it, so the move actually changes X.
        left.TryAddChild(new MGButton(window) { PreferredWidth = 60, PreferredHeight = 40 });
        MGButton element = new(window) { PreferredWidth = 100, PreferredHeight = 40 };
        element.LayoutTransition = Transition();
        left.TryAddChild(element);

        int frameIndex = 0;
        void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                frameIndex++;
                MouseState state = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                desktop.Update();
            }
        }
        Frames(2);

        Point previousLocation = element.LayoutBounds.Location;
        left.TryRemoveChild(element);
        right.TryAddChild(element);
        Frames(1);

        Assert.NotEqual(previousLocation, element.LayoutBounds.Location);
        Assert.False(element.HasActiveLayoutOffset);
        Assert.Equal(Vector2.Zero, element.LayoutOffset);
    }

    [Fact]
    public void NoRun_DuringAWindowResize()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 200, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGButton anchored = new(window) { PreferredWidth = 80, PreferredHeight = 40, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom };
        anchored.LayoutTransition = Transition();
        window.SetContent(anchored);
        desktop.Windows.Add(window);

        int frameIndex = 0;
        void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                frameIndex++;
                MouseState state = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                desktop.Update();
            }
        }
        Frames(2);

        Point previousLocation = anchored.LayoutBounds.Location;
        window.WindowHeight = 300;
        Frames(1);

        // Bottom-anchored: growing the window really does move it, but the resize guard suppresses the run.
        Assert.NotEqual(previousLocation, anchored.LayoutBounds.Location);
        Assert.False(anchored.HasActiveLayoutOffset);
    }

    /// <summary>Fix round 1 (P2): a window with a non-zero <see cref="MGElement.Margin"/> has a <see cref="MGElement.LayoutBounds"/>
    /// smaller than its allocated <see cref="Rectangle"/> (the margin is subtracted), so comparing the two -- as the original
    /// <c>WindowResized</c> check did -- always found a size mismatch and suppressed every layout transition in that window,
    /// window resize or not. Comparing the previous ALLOCATED size instead lets a real, non-resizing layout pass play normally.</summary>
    [Fact]
    public void TransitionPlays_InAWindowWithAMargin_WhenItIsNotBeingResized()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None, Margin = new Thickness(5) };
        MGStackPanel panel = new(window, Orientation.Vertical) { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGButton top = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
        top.LayoutTransition = Transition();
        panel.TryAddChild(top);
        panel.TryAddChild(new MGButton(window) { PreferredWidth = 120, PreferredHeight = 40 });
        window.SetContent(panel);
        desktop.Windows.Add(window);

        int frameIndex = 0;
        void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                frameIndex++;
                MouseState state = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                desktop.Update();
            }
        }
        Frames(2);

        Point previousLocation = top.LayoutBounds.Location;
        panel.TryInsertChild(0, new MGButton(window) { PreferredWidth = 120, PreferredHeight = 20 });
        Frames(1);

        Assert.NotEqual(previousLocation, top.LayoutBounds.Location);
        Assert.True(top.HasActiveLayoutOffset);
    }

    [Fact]
    public void NoRun_OnARecycledContainerOfAVirtualizedPanel_ScrolledSoContainersAreReused()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 100, 120) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGScrollViewer scrollViewer = new(window, ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0),
        };
        VirtualizingStackPanel panel = new(window) { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        const int itemHeight = 20;
        const int itemCount = 60;
        panel.UniformItemHeight = itemHeight;
        panel.TotalItemCount = itemCount;
        panel.ItemGenerator = idx =>
        {
            MGElement element;
            if (panel.TryDequeueRecycledElement(out var recycled))
            {
                element = recycled;
            }
            else
            {
                MGButton button = new(window) { PreferredHeight = itemHeight, HorizontalAlignment = HorizontalAlignment.Stretch };
                button.LayoutTransition = Transition();
                element = button;
            }
            return element;
        };
        scrollViewer.SetContent(panel);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        int frameIndex = 0;
        void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                frameIndex++;
                MouseState state = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                desktop.Update();
            }
        }
        Frames(2);

        // Scroll far enough that every currently realized container is recycled and rebound to a new index/position.
        scrollViewer.VerticalOffset = scrollViewer.MaxVerticalOffset;
        Frames(3);

        Assert.Equal(0, desktop.Animations.ActiveCount);
        foreach (MGElement element in panel.Children)
        {
            Assert.False(element.HasActiveLayoutOffset);
        }
    }

    /// <summary>Fix round 1 (P1): the container itself is never opted in (the normal <see cref="MGListBox"/> shape, whose
    /// ContentPresenter border is internal), only its inner content is. Recycling reuses the SAME container border, whose
    /// <c>SetParent</c> fires once per recycle/re-realize -- but until fix round 1 that only lowered the flag on the border
    /// itself, leaving the inner opted button's stale flag up so it played a false run off its now-meaningless previous position.</summary>
    [Fact]
    public void NoRun_OnARecycledContainerOfAVirtualizedPanel_OptedInnerContent()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 100, 120) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGScrollViewer scrollViewer = new(window, ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Padding = new Thickness(0),
        };
        VirtualizingStackPanel panel = new(window) { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Top };
        const int itemHeight = 20;
        const int itemCount = 60;
        panel.UniformItemHeight = itemHeight;
        panel.TotalItemCount = itemCount;
        panel.ItemGenerator = idx =>
        {
            MGBorder container;
            if (panel.TryDequeueRecycledElement(out var recycled))
            {
                container = (MGBorder)recycled;
            }
            else
            {
                container = new MGBorder(window) { PreferredHeight = itemHeight, HorizontalAlignment = HorizontalAlignment.Stretch };
                MGButton inner = new(window) { PreferredHeight = itemHeight, HorizontalAlignment = HorizontalAlignment.Stretch };
                inner.LayoutTransition = Transition();
                container.SetContent(inner);
            }
            return container;
        };
        scrollViewer.SetContent(panel);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        int frameIndex = 0;
        void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                frameIndex++;
                MouseState state = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                desktop.Update();
            }
        }
        Frames(2);

        // Scroll far enough that every currently realized container is recycled and rebound to a new index/position.
        scrollViewer.VerticalOffset = scrollViewer.MaxVerticalOffset;
        Frames(3);

        Assert.Equal(0, desktop.Animations.ActiveCount);
        foreach (MGElement container in panel.Children)
        {
            MGButton inner = Assert.IsType<MGButton>(((MGBorder)container).Content);
            Assert.False(inner.HasActiveLayoutOffset);
        }
    }

    /// <summary>Fix round 1 (P1): a reorder (remove then insert) of a container that itself opted in, holding an opted child.
    /// The container correctly plays nothing (its own flag is lowered by its own <c>SetParent</c>), but until fix round 1 the
    /// child's flag stayed up (only <c>SetParent</c>'s own element was reset), so the child played its whole raw delta.</summary>
    [Fact]
    public void Reorder_OfAContainerWithAnOptedChild_PlaysNothingOnEither()
    {
        Scene scene = Scene.Build();
        MGButton child = new(scene.Window) { PreferredWidth = 80, PreferredHeight = 30 };
        child.LayoutTransition = Transition();
        MGBorder container = new(scene.Window) { PreferredWidth = 100, PreferredHeight = 40 };
        container.LayoutTransition = Transition();
        container.SetContent(child);
        scene.Panel.TryAddChild(container);
        scene.AddButton();
        scene.Frames(2);

        scene.Panel.TryRemoveChild(container);
        scene.Panel.TryInsertChild(1, container);
        scene.Frames(1);

        Assert.False(container.HasActiveLayoutOffset);
        Assert.Equal(Vector2.Zero, container.LayoutOffset);
        Assert.False(child.HasActiveLayoutOffset);
        Assert.Equal(Vector2.Zero, child.LayoutOffset);
    }

    // ---- Restart continuity --------------------------------------------------------------------------------------

    [Fact]
    public void SecondChangeDuringTheRun_RestartsFromTheCurrentVisualPosition_WithoutAJump()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        top.LayoutTransition = Transition(200);
        scene.AddButton();
        scene.Frames(2);

        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 });
        scene.Frames(3);
        Vector2 offsetBeforeSecondChange = top.LayoutOffset;
        Assert.True(offsetBeforeSecondChange != Vector2.Zero);
        Vector2 absoluteVisualBeforeSecondChange = new Vector2(top.LayoutBounds.Location.X, top.LayoutBounds.Location.Y) + offsetBeforeSecondChange;

        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 15 });
        scene.Frames(1);

        // No jump: the drawn (absolute) position right after the restart is a near-continuation of where it already was,
        // one frame's worth of natural decay away -- not a snap back to the pre-change value plus the new raw delta.
        Vector2 absoluteVisualAfterSecondChange = new Vector2(top.LayoutBounds.Location.X, top.LayoutBounds.Location.Y) + top.LayoutOffset;
        float jump = Vector2.Distance(absoluteVisualBeforeSecondChange, absoluteVisualAfterSecondChange);
        Assert.True(jump < 5f, $"Expected a near-continuous visual position, moved by {jump}px ({absoluteVisualBeforeSecondChange} -> {absoluteVisualAfterSecondChange}).");

        scene.Frames(30);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);
        Assert.False(top.HasActiveLayoutOffset);
    }

    // ---- AnimateSize (Y5) ----------------------------------------------------------------------------------------

    private static UILayoutTransition SizeTransition(int milliseconds = 200) => new() { Duration = TimeSpan.FromMilliseconds(milliseconds), AnimateSize = true };

    [Fact]
    public void AnimateSize_WidthGrows_ScalesXAroundNewTopLeft_AndEndsAtIdentity()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton(width: 100);
        top.LayoutTransition = SizeTransition();
        scene.Frames(2);

        Point previousTopLeft = top.LayoutBounds.Location;
        top.PreferredWidth = 200;
        scene.Frames(1);

        Assert.Equal(previousTopLeft, top.LayoutBounds.Location);
        Assert.True(top.HasActiveLayoutOffset);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);

        GraphNoOpDrawTransaction transaction = scene.Draw();
        Matrix push = Assert.Single(transaction.TransformPushes);
        // Scaling X by 0.5 (100/200) around the new top-left maps the new right edge (200) back onto the old one (100).
        Rectangle bounds = top.LayoutBounds;
        Vector2 newRightEdge = new(bounds.Right, bounds.Top);
        Vector2 mapped = Vector2.Transform(newRightEdge, push);
        Assert.Equal(bounds.Left + 100, mapped.X, 2);
        Assert.Equal(0.5f, push.M11, 3);

        scene.Frames(30);
        Assert.Equal(Vector2.One, top.LayoutScale);
        Assert.False(top.HasActiveLayoutOffset);
        Assert.Equal(0, scene.Desktop.ActiveRenderTransformCount);
    }

    [Fact]
    public void AnimateSize_CombinedWithAMove_BothAnimateTogether_AndEndAtIdentity()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton(width: 100);
        top.LayoutTransition = SizeTransition();
        scene.AddButton();
        scene.Frames(2);

        Point previousLocation = top.LayoutBounds.Location;
        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 });
        top.PreferredWidth = 200;
        scene.Frames(1);

        Assert.NotEqual(previousLocation, top.LayoutBounds.Location);
        Assert.NotEqual(Vector2.Zero, top.LayoutOffset);
        Assert.NotEqual(Vector2.One, top.LayoutScale);
        Assert.True(top.HasActiveLayoutOffset);

        scene.Frames(30);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);
        Assert.Equal(Vector2.One, top.LayoutScale);
        Assert.False(top.HasActiveLayoutOffset);
        Assert.Equal(0, scene.Desktop.ActiveRenderTransformCount);
    }

    [Fact]
    public void AnimateSize_HitTestFollowsTheDrawnScale_DuringTheRun()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton(width: 100, height: 40);
        top.LayoutTransition = SizeTransition(200);
        scene.Frames(2);

        top.PreferredWidth = 200;
        scene.Frames(1);

        // Right after the change, the visual width is still ~100 (scaled by ~0.5): a point near the new right edge (200) misses,
        // while the centre still hits.
        Rectangle bounds = top.LayoutBounds;
        Point nearNewRightEdge = new(bounds.Right - 2, bounds.Top + bounds.Height / 2);
        scene.Frames(1, nearNewRightEdge);
        Assert.False(top.IsHovered);

        Point centre = bounds.Center;
        scene.Frames(1, centre);
        Assert.True(top.IsHovered);
    }

    [Fact]
    public void AnimateSize_SecondSizeChangeDuringTheRun_RestartsFromTheCurrentVisualScale_WithoutAJump()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton(width: 100);
        top.LayoutTransition = SizeTransition(200);
        scene.Frames(2);

        top.PreferredWidth = 200;
        scene.Frames(3);
        Vector2 scaleBeforeSecondChange = top.LayoutScale;
        Assert.NotEqual(Vector2.One, scaleBeforeSecondChange);
        float visualWidthBeforeSecondChange = top.LayoutBounds.Width * scaleBeforeSecondChange.X;

        top.PreferredWidth = 300;
        scene.Frames(1);

        // No jump: the drawn (visual) width right after the restart is a near-continuation of where it already was.
        float visualWidthAfterSecondChange = top.LayoutBounds.Width * top.LayoutScale.X;
        float jump = Math.Abs(visualWidthBeforeSecondChange - visualWidthAfterSecondChange);
        Assert.True(jump < 15f, $"Expected a near-continuous visual width, moved by {jump}px ({visualWidthBeforeSecondChange} -> {visualWidthAfterSecondChange}).");

        scene.Frames(30);
        Assert.Equal(Vector2.One, top.LayoutScale);
        Assert.False(top.HasActiveLayoutOffset);
    }

    [Fact]
    public void AnimateSizeFalse_ASizeChangeAlonePlaysNothing_LikeY4()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton(width: 100);
        top.LayoutTransition = Transition();
        scene.Frames(2);

        top.PreferredWidth = 200;
        scene.Frames(1);

        Assert.False(top.HasActiveLayoutOffset);
        Assert.Equal(Vector2.One, top.LayoutScale);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);
    }

    [Fact]
    public void AnimateSize_ActiveRenderTransformCount_ReturnsToItsInitialValue_AfterDetachingMidRun()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton(width: 100);
        top.LayoutTransition = SizeTransition(500);
        scene.Frames(2);
        int initial = scene.Desktop.ActiveRenderTransformCount;

        top.PreferredWidth = 200;
        scene.Frames(1);
        Assert.True(scene.Desktop.ActiveRenderTransformCount > initial);

        scene.Panel.TryRemoveChild(top);
        Assert.Equal(initial, scene.Desktop.ActiveRenderTransformCount);
        Assert.Equal(Vector2.One, top.LayoutScale);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);
    }

    [Fact]
    public void AnimateSize_ActiveRenderTransformCount_ReturnsToItsInitialValue_AfterClosingTheWindowMidRun()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGStackPanel panel = new(window, Orientation.Vertical) { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGButton top = new(window) { PreferredWidth = 100, PreferredHeight = 40 };
        top.LayoutTransition = SizeTransition(500);
        panel.TryAddChild(top);
        window.SetContent(panel);
        desktop.Windows.Add(window);

        int frameIndex = 0;
        void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                frameIndex++;
                MouseState state = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                desktop.Update();
            }
        }
        Frames(2);
        int initial = desktop.ActiveRenderTransformCount;

        top.PreferredWidth = 200;
        Frames(1);
        Assert.True(desktop.ActiveRenderTransformCount > initial);

        Assert.True(window.TryCloseWindow());
        Assert.Equal(initial, desktop.ActiveRenderTransformCount);
        Assert.Equal(Vector2.One, top.LayoutScale);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);
    }

    [Fact]
    public void AnimateSize_ActiveRun_AllocatesNothingPerTick_AfterWarmUp()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton(width: 100);
        top.LayoutTransition = SizeTransition(100_000);
        scene.Frames(2);

        top.PreferredWidth = 200;
        scene.Frames(1);
        Assert.True(top.HasActiveLayoutOffset);

        UIAnimationManager manager = scene.Desktop.Animations;
        TimeSpan frame = TimeSpan.FromMilliseconds(FrameMilliseconds);
        for (int i = 0; i < 20; i++)
        {
            manager.Update(frame);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            manager.Update(frame);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    // ---- Nesting --------------------------------------------------------------------------------------------------

    [Fact]
    public void Nesting_ParentAndChildBothOpted_TheChildHasNoOwnRun_AndIsDrawnOnlyWithTheParent()
    {
        Scene scene = Scene.Build();
        MGButton child = new(scene.Window) { PreferredWidth = 80, PreferredHeight = 30 };
        child.LayoutTransition = Transition();
        MGBorder parent = new(scene.Window) { PreferredWidth = 100, PreferredHeight = 40 };
        parent.LayoutTransition = Transition();
        parent.SetContent(child);
        scene.Panel.TryAddChild(parent);
        scene.AddButton();
        scene.Frames(2);

        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 });
        scene.Frames(1);

        Assert.True(parent.HasActiveLayoutOffset);
        Assert.False(child.HasActiveLayoutOffset);
        Assert.Equal(Vector2.Zero, child.LayoutOffset);

        GraphNoOpDrawTransaction transaction = scene.Draw();
        Matrix push = Assert.Single(transaction.TransformPushes);
        Vector2 offset = parent.LayoutOffset;
        Assert.Equal(offset.X, push.M41, 3);
        Assert.Equal(offset.Y, push.M42, 3);
    }

    // ---- Cohabitation with an application render-transform animation, hit test -------------------------------------

    [Fact]
    public void Cohabitation_WithARenderTransformTranslation_ComposesBoth_AndHitTestFollowsTheVisualPosition()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        top.LayoutTransition = Transition(200);
        top.RenderTransform.Translation = new Vector2(30f, 0f);
        scene.AddButton();
        scene.Frames(2, new Point(1, 1));

        Point previousLocation = top.LayoutBounds.Location;
        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 });
        scene.Frames(1, new Point(1, 1));

        Point newLocation = top.LayoutBounds.Location;
        Vector2 layoutOffset = top.LayoutOffset;
        Assert.NotEqual(Vector2.Zero, layoutOffset);

        GraphNoOpDrawTransaction transaction = scene.Draw();
        Matrix push = Assert.Single(transaction.TransformPushes);
        // Composed: RenderTransform.Translation applied first, then the layout offset, both visible in the pushed matrix.
        Assert.Equal(30f + layoutOffset.X, push.M41, 3);
        Assert.Equal(layoutOffset.Y, push.M42, 3);

        // The visual position (new layout position + both translations) is where the element hit-tests.
        Point visualPosition = new(newLocation.X + top.LayoutBounds.Width / 2 + (int)(30f + layoutOffset.X), newLocation.Y + top.LayoutBounds.Height / 2 + (int)layoutOffset.Y);
        scene.Frames(2, visualPosition);
        Assert.True(top.IsHovered);

        // A second shift restarts the run: right after that layout pass, the new layout position is not yet visually
        // reached (the offset is fresh again), so the new position itself misses on this very frame.
        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 });
        scene.Frames(1, visualPosition);
        Point stillNotVisuallyThere = top.LayoutBounds.Center;
        scene.Frames(1, stillNotVisuallyThere);
        Assert.False(top.IsHovered);
    }

    // ---- ActiveRenderTransformCount ----------------------------------------------------------------------------

    [Fact]
    public void ActiveRenderTransformCount_ReturnsToItsInitialValue_AtTheEndOfARun()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        top.LayoutTransition = Transition(64);
        scene.AddButton();
        scene.Frames(2);
        int initial = scene.Desktop.ActiveRenderTransformCount;

        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 });
        scene.Frames(1);
        Assert.True(scene.Desktop.ActiveRenderTransformCount > initial);

        scene.Frames(10);
        Assert.Equal(initial, scene.Desktop.ActiveRenderTransformCount);
    }

    [Fact]
    public void ActiveRenderTransformCount_ReturnsToItsInitialValue_AfterDetachingMidRun()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        top.LayoutTransition = Transition(500);
        scene.AddButton();
        scene.Frames(2);
        int initial = scene.Desktop.ActiveRenderTransformCount;

        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 });
        scene.Frames(1);
        Assert.True(scene.Desktop.ActiveRenderTransformCount > initial);

        scene.Panel.TryRemoveChild(top);
        Assert.Equal(initial, scene.Desktop.ActiveRenderTransformCount);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);
    }

    [Fact]
    public void ActiveRenderTransformCount_ReturnsToItsInitialValue_AfterClosingTheWindowMidRun()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGStackPanel panel = new(window, Orientation.Vertical) { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGButton top = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
        top.LayoutTransition = Transition(500);
        panel.TryAddChild(top);
        panel.TryAddChild(new MGButton(window) { PreferredWidth = 120, PreferredHeight = 40 });
        window.SetContent(panel);
        desktop.Windows.Add(window);

        int frameIndex = 0;
        void Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                frameIndex++;
                MouseState state = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                desktop.Update();
            }
        }
        Frames(2);
        int initial = desktop.ActiveRenderTransformCount;

        panel.TryInsertChild(0, new MGButton(window) { PreferredWidth = 120, PreferredHeight = 25 });
        Frames(1);
        Assert.True(desktop.ActiveRenderTransformCount > initial);

        Assert.True(window.TryCloseWindow());
        Assert.Equal(initial, desktop.ActiveRenderTransformCount);
        Assert.Equal(Vector2.Zero, top.LayoutOffset);
    }

    // ---- Cost -----------------------------------------------------------------------------------------------------

    [Fact]
    public void RepeatedLayoutPasses_AllocateNothing_AfterWarmUp_WhetherOrNotAnElementOptedIn()
    {
        AssertNoAllocationAtRest(optIn: false);
        AssertNoAllocationAtRest(optIn: true);
    }

    private static void AssertNoAllocationAtRest(bool optIn)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGStackPanel panel = new(window, Orientation.Vertical) { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        MGButton a = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
        MGButton b = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
        if (optIn)
        {
            a.LayoutTransition = Transition();
        }
        panel.TryAddChild(a);
        panel.TryAddChild(b);
        window.SetContent(panel);
        desktop.Windows.Add(window);
        desktop.Update();

        // Forced layout passes, direct (bypassing Update's mouse/hover bookkeeping, which is out of scope here):
        // one push/pop of the ambient stack per element per pass when optIn is true, nothing but a flag read otherwise.
        Rectangle bounds = new(window.Left, window.Top, window.WindowWidth, window.WindowHeight);
        // Warm-up: caches populated, the ambient stack's backing array grown to its final size, so the loops below run at rest.
        for (int i = 0; i < 50; i++)
        {
            window.UpdateLayout(bounds);
        }

        // The measurement is the MINIMUM over several identical loops, not the value of a single one.
        //
        // Why a single loop was not enough: this assertion failed intermittently, only ever under the full suite's parallel load
        // and never in isolation. What lands in the measured window then is a ONE-OFF charge on this thread -- a cost the process
        // pays once, not one the layout pass pays per call. The note this replaces blamed tiered JIT compilation and raised the
        // warm-up from 10 to 50; that is not the cause, and a longer warm-up only moves the window instead of closing it. Pinning
        // the whole test to tier-0 (DOTNET_TC_CallCountThreshold / DOTNET_TC_OnStackReplacement_InitialCounter) reproduces
        // nothing, and 1440 consecutive measured loops -- under three concurrent suites and under a fully saturated CPU, including
        // loops spanning gen2 collections -- each came back at exactly 0.
        //
        // Why the minimum is exactly as strict: a layout pass that allocated k > 0 bytes at rest would charge every loop at least
        // PassesPerLoop * k, so every loop, and therefore the minimum, would be non-zero. Only a cost that is NOT paid per pass can
        // land in one loop and leave another at 0. Nothing is weakened: the test still fails on any per-pass allocation.
        const int PassesPerLoop = 200;
        const int MeasurementLoops = 5;
        long[] perLoopBytes = new long[MeasurementLoops]; // allocated up front, never inside a measured window
        long fewestBytes = long.MaxValue;
        for (int loop = 0; loop < MeasurementLoops && fewestBytes != 0; loop++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < PassesPerLoop; i++)
            {
                window.UpdateLayout(bounds);
            }
            perLoopBytes[loop] = GC.GetAllocatedBytesForCurrentThread() - before;
            fewestBytes = Math.Min(fewestBytes, perLoopBytes[loop]);
        }

        // A clean first loop stops the run there, so the common case costs what it always did. Reaching the assertion with a
        // non-zero minimum means every loop ran, so the report below is complete: all loops dirty is a real per-pass regression,
        // while a single dirty loop among clean ones would mean the minimum caught a one-off after all.
        Assert.True(fewestBytes == 0,
            $"optIn={optIn}: no loop of {PassesPerLoop} layout passes at rest was allocation-free; bytes per loop = [{string.Join(", ", perLoopBytes)}]");
    }

    [Fact]
    public void AnActiveRun_AllocatesNothingPerTick_AfterWarmUp()
    {
        Scene scene = Scene.Build();
        MGButton top = scene.AddButton();
        top.LayoutTransition = Transition(100_000);
        scene.AddButton();
        scene.Frames(2);

        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 25 });
        scene.Frames(1);
        Assert.True(top.HasActiveLayoutOffset);

        UIAnimationManager manager = scene.Desktop.Animations;
        TimeSpan frame = TimeSpan.FromMilliseconds(FrameMilliseconds);
        for (int i = 0; i < 20; i++)
        {
            manager.Update(frame);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 200; i++)
        {
            manager.Update(frame);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    [Fact]
    public void AnElementThatNeverOptsIn_NeverAllocatesAnAnimationSlot_EvenOnADesktopWithLayoutTransitions()
    {
        Scene scene = Scene.Build();
        MGButton opted = scene.AddButton();
        opted.LayoutTransition = Transition();
        MGButton never = scene.AddButton();
        scene.Frames(2);

        for (int i = 0; i < 5; i++)
        {
            scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 10 });
            scene.Frames(1);
        }

        Assert.True(scene.Desktop.HasLayoutTransitions);
        Assert.Null(never.AnimationSlotOrNull);
    }

    // ---- Ambient stack exception safety -------------------------------------------------------------------------

    /// <summary>A border whose content layout can be made to throw once, to probe that the ambient pass stack's push and pop
    /// (<see cref="MGDesktop.PushLayoutTransitionEntry"/>/<see cref="MGDesktop.PopLayoutTransitionEntry"/>) stay paired across an
    /// exception thrown by a descendant's layout (ACCEPTANCE 3.a).</summary>
    private sealed class ThrowingBorder : MGBorder
    {
        public bool ThrowOnNextContentLayout;
        public int CallCount;
        public ThrowingBorder(MGWindow window) : base(window) { }
        protected override void UpdateContentLayout(Rectangle bounds)
        {
            CallCount++;
            if (ThrowOnNextContentLayout)
            {
                ThrowOnNextContentLayout = false;
                throw new InvalidOperationException("probe");
            }
            base.UpdateContentLayout(bounds);
        }
    }

    [Fact]
    public void PushPopStaysBalanced_WhenADescendantsLayoutThrows()
    {
        Scene scene = Scene.Build();
        MGButton opted = scene.AddButton();
        opted.LayoutTransition = Transition();
        ThrowingBorder thrower = new(scene.Window) { PreferredWidth = 50, PreferredHeight = 50 };
        thrower.SetContent(new MGButton(scene.Window) { PreferredWidth = 30, PreferredHeight = 30 });
        scene.Panel.TryAddChild(thrower);
        scene.Frames(2);

        thrower.ThrowOnNextContentLayout = true;
        thrower.InvalidateLayout();
        int before = thrower.CallCount;
        Assert.Throws<InvalidOperationException>(() => scene.Window.UpdateLayout(new Rectangle(scene.Window.Left, scene.Window.Top, scene.Window.WindowWidth, scene.Window.WindowHeight)));
        Assert.True(thrower.CallCount > before, $"UpdateContentLayout was never called (before={before}, after={thrower.CallCount}).");

        // A normal pass right after must behave exactly as before: the stack was correctly popped despite the throw.
        scene.Panel.TryInsertChild(0, new MGButton(scene.Window) { PreferredWidth = 120, PreferredHeight = 20 });
        scene.Frames(1);
        Assert.True(opted.HasActiveLayoutOffset);
    }

    // ---- Scene ------------------------------------------------------------------------------------------------

    /// <summary>A 400x300 window holding a content-sized vertical <see cref="MGStackPanel"/>, driven frame by frame.</summary>
    private sealed class Scene
    {
        public GraphTestRuntime Runtime { get; private init; }
        public MGDesktop Desktop { get; private init; }
        public MGWindow Window { get; private init; }
        public MGStackPanel Panel { get; private init; }
        private int _frame;

        public static Scene Build()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
            MGStackPanel panel = new(window, Orientation.Vertical)
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Spacing = 0,
            };
            window.SetContent(panel);
            desktop.Windows.Add(window);

            return new Scene { Runtime = runtime, Desktop = desktop, Window = window, Panel = panel };
        }

        public MGButton AddButton(int width = 120, int height = 40)
        {
            MGButton button = new(Window) { PreferredWidth = width, PreferredHeight = height };
            Panel.TryAddChild(button);
            return button;
        }

        public void Frames(int count) => Frames(count, new Point(1, 1));

        public void Frames(int count, Point mouse)
        {
            for (int i = 0; i < count; i++)
            {
                _frame++;
                MouseState state = new(mouse.X, mouse.Y, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)_frame), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                Desktop.Update();
            }
        }

        public GraphNoOpDrawTransaction Draw()
        {
            GraphNoOpDrawTransaction transaction = new(Runtime, DrawSettings.Default);
            Desktop.Draw(transaction);
            return transaction;
        }
    }
}
