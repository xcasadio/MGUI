using System;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.XAML;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using Rectangle = Microsoft.Xna.Framework.Rectangle;
using Thickness = MonoGame.Extended.Thickness;

namespace MGUI.Tests.Animation;

/// <summary>Slice Y2 (Docs/decisions/0011-animation-v5.md, "Defilement fluide"): the two registered scroll-offset paths
/// (<see cref="UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset"/>/<see cref="UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset"/>),
/// <see cref="MGScrollViewer.ScrollTo"/>, the external-write cancellation rule, the mouse wheel and keyboard sites driven through
/// <see cref="MGScrollViewer.PendingVerticalOffset"/>/<see cref="MGScrollViewer.PendingHorizontalOffset"/>, the cost budget and the XAML
/// attributes.</summary>
public class ScrollAnimationTests
{
    private const int FrameMilliseconds = 16;
    private const int ItemHeight = 40;
    private const int ItemCount = 30;
    private const int ViewportHeight = 200;
    private const int ViewportWidth = 200;

    /// <summary>A headless desktop with one window holding an <see cref="MGScrollViewer"/> (fixed <see cref="ViewportWidth"/> x
    /// <see cref="ViewportHeight"/> viewport) over a tall-and-wide <see cref="MGStackPanel"/> of fixed-size borders, driven frame by frame.
    /// <see cref="ScrollWheel"/> raises a real mouse wheel event through the same input path a person scrolling uses.</summary>
    private sealed class Scene
    {
        public GraphTestRuntime Runtime { get; private init; }
        public MGDesktop Desktop { get; private init; }
        public MGWindow Window { get; private init; }
        public MGScrollViewer ScrollViewer { get; private init; }
        public MGStackPanel Panel { get; private init; }
        public MGElement[] Items { get; private init; }
        public int FrameIndex { get; private set; }
        public int ScrollWheelValue { get; private set; }
        public Point Mouse { get; set; } = new(ViewportWidth / 2, ViewportHeight / 2);

        public static Scene Build(Orientation orientation = Orientation.Vertical)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, ViewportWidth, ViewportHeight) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };

            bool isVertical = orientation == Orientation.Vertical;
            MGScrollViewer scrollViewer = new(window,
                isVertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled,
                isVertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Padding = new Thickness(0),
            };

            MGStackPanel panel = new(window, orientation) { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            MGElement[] items = new MGElement[ItemCount];
            for (int i = 0; i < ItemCount; i++)
            {
                MGBorder border = new(window) { PreferredWidth = isVertical ? ViewportWidth : ItemHeight, PreferredHeight = isVertical ? ItemHeight : ViewportHeight };
                panel.TryAddChild(border);
                items[i] = border;
            }

            scrollViewer.SetContent(panel);
            window.SetContent(scrollViewer);
            desktop.Windows.Add(window);

            Scene scene = new() { Runtime = runtime, Desktop = desktop, Window = window, ScrollViewer = scrollViewer, Panel = panel, Items = items };
            scene.Frames(2);
            return scene;
        }

        public void Frames(int count, int milliseconds = FrameMilliseconds)
        {
            for (int i = 0; i < count; i++)
            {
                FrameIndex++;
                MouseState state = new(Mouse.X, Mouse.Y, ScrollWheelValue,
                    ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds((double)milliseconds * FrameIndex), TimeSpan.FromMilliseconds(milliseconds), state, new KeyboardState()));
                Desktop.Update();
            }
        }

        /// <summary>Raises one wheel notch (positive scrolls up: <c>ScrollWheelDelta &gt; 0</c>) over <see cref="Mouse"/>, then advances one frame.</summary>
        public void ScrollWheel(int delta, int frames = 1)
        {
            ScrollWheelValue += delta;
            Frames(frames);
        }
    }

    // ---- Targets: registration, applicability, serialisation --------------------------------------------------

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExplicitAnimation_ReachesTheDestination_OnBothPaths(bool vertical)
    {
        Scene scene = Scene.Build(vertical ? Orientation.Vertical : Orientation.Horizontal);
        string path = vertical ? UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset : UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset;
        UIPropertyAnimation<float> animation = new(path) { To = 100f, Duration = TimeSpan.FromMilliseconds(160) };
        scene.ScrollViewer.Animations.Start(animation);
        scene.Frames(11); // ~176 ms

        float value = vertical ? scene.ScrollViewer.VerticalOffset : scene.ScrollViewer.HorizontalOffset;
        Assert.Equal(100f, value, 1);
        Assert.Equal(UIAnimationState.Completed, animation.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Transition_ReachesTheDestination_OnBothPaths(bool vertical)
    {
        Scene scene = Scene.Build(vertical ? Orientation.Vertical : Orientation.Horizontal);
        string path = vertical ? UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset : UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset;
        scene.ScrollViewer.Transitions.Add(new UITransition<float>(path, TimeSpan.FromMilliseconds(100)));

        if (vertical)
        {
            scene.ScrollViewer.VerticalOffset = 80f;
        }
        else
        {
            scene.ScrollViewer.HorizontalOffset = 80f;
        }

        scene.Frames(7); // ~112 ms

        float value = vertical ? scene.ScrollViewer.VerticalOffset : scene.ScrollViewer.HorizontalOffset;
        Assert.Equal(80f, value, 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Path_IsRefused_OnAnElementThatIsNotAScrollViewer(bool vertical)
    {
        Scene scene = Scene.Build();
        string path = vertical ? UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset : UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset;
        IUIAnimationTarget<float> target = UIAnimationTargets.Resolve<float>(path);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => target.GetValue(scene.Panel));
        Assert.Contains(nameof(MGScrollViewer), error.Message);

        // No exception: the right owner type is accepted.
        target.GetValue(scene.ScrollViewer);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GetOwnerType_ReturnsMGScrollViewer_OnBothPaths(bool vertical)
    {
        string path = vertical ? UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset : UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset;
        Assert.Equal(typeof(MGScrollViewer), UIAnimationTargets.GetOwnerType(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Serializer_RoundTrips_APropertyAnimationNode_OnBothPaths(bool vertical)
    {
        string path = vertical ? UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset : UIExtraAnimationTargets.Paths.ScrollViewerHorizontalOffset;
        UIPropertyAnimation<float> original = new(path) { From = 0f, To = 240f, Duration = TimeSpan.FromMilliseconds(250), Easing = UIEasing.CubicOut, Name = "scroll-test" };

        string json = MGUI.Core.UI.Animation.KeyFrames.UIAnimationSerializer.Serialize(original, _ => null);
        UIAnimation restored = MGUI.Core.UI.Animation.KeyFrames.UIAnimationSerializer.Deserialize(json, _ => null);

        Scene scene = Scene.Build(vertical ? Orientation.Vertical : Orientation.Horizontal);
        scene.ScrollViewer.Animations.Start(restored);
        scene.Frames(16); // 250 ms

        float value = vertical ? scene.ScrollViewer.VerticalOffset : scene.ScrollViewer.HorizontalOffset;
        Assert.Equal(240f, value, 1);
    }

    // ---- ScrollTo: destination, clamping, shrinking content, zero duration ----------------------------------------

    [Fact]
    public void ScrollTo_ReachesTheClampedDestination()
    {
        Scene scene = Scene.Build();
        scene.ScrollViewer.ScrollTo(null, 150f, TimeSpan.FromMilliseconds(160));
        scene.Frames(11);
        Assert.Equal(150f, scene.ScrollViewer.VerticalOffset, 1);
    }

    [Fact]
    public void ScrollTo_ADestinationBeyondTheBounds_StopsAtTheBound()
    {
        Scene scene = Scene.Build();
        float max = scene.ScrollViewer.MaxVerticalOffset;
        scene.ScrollViewer.ScrollTo(null, max + 5000f, TimeSpan.FromMilliseconds(80));
        scene.Frames(6);
        Assert.Equal(max, scene.ScrollViewer.VerticalOffset, 1);
    }

    [Fact]
    public void ScrollTo_ZeroDuration_WritesImmediately()
    {
        Scene scene = Scene.Build();
        scene.ScrollViewer.ScrollTo(null, 90f, TimeSpan.Zero);
        Assert.Equal(90f, scene.ScrollViewer.VerticalOffset);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void ContentShrinkingDuringARun_EndsAtTheNewBound_WithoutCancellingEarly()
    {
        Scene scene = Scene.Build();
        float originalMax = scene.ScrollViewer.MaxVerticalOffset;
        scene.ScrollViewer.ScrollTo(null, originalMax, TimeSpan.FromMilliseconds(320));
        scene.Frames(3); // partway through the run

        Assert.True(scene.ScrollViewer.VerticalOffset > 0 && scene.ScrollViewer.VerticalOffset < originalMax);

        // Shrink the content: remove most items so MaxVerticalOffset drops below the run's destination.
        for (int i = scene.Items.Length - 1; i >= 5; i--)
        {
            scene.Panel.TryRemoveChild(scene.Items[i]);
        }

        scene.Frames(2);
        float newMax = scene.ScrollViewer.MaxVerticalOffset;
        Assert.True(newMax < originalMax);
        Assert.Equal(newMax, scene.ScrollViewer.VerticalOffset, 1);

        // The run itself was not cancelled by the re-clamp: it keeps ticking (and ends exactly at the new bound).
        scene.Frames(20);
        Assert.Equal(newMax, scene.ScrollViewer.VerticalOffset, 1);
    }

    // ---- External write cancels the run; a transition keeps retargeting ---------------------------------------

    [Theory]
    [InlineData("setter", 10f)]
    [InlineData("scrollToTop", 0f)]
    [InlineData("drag", 10f)]
    [InlineData("zeroDurationScrollTo", 10f)]
    public void ExternalWrite_DuringARun_CancelsIt_AndKeepsTheWrittenValue(string kind, float expected)
    {
        Scene scene = Scene.Build();
        scene.ScrollViewer.ScrollTo(null, scene.ScrollViewer.MaxVerticalOffset, TimeSpan.FromMilliseconds(500));
        scene.Frames(3);
        Assert.True(scene.ScrollViewer.VerticalOffset > 0);
        Assert.Equal(scene.ScrollViewer.MaxVerticalOffset, scene.ScrollViewer.PendingVerticalOffset, 1);

        switch (kind)
        {
            case "setter":
                scene.ScrollViewer.VerticalOffset = expected;
                break;
            case "scrollToTop":
                scene.ScrollViewer.ScrollToTop();
                break;
            case "drag":
                // The content-drag site writes VerticalOffset directly too (MGScrollViewer.cs, MouseHandler.Dragged): same external-write path.
                scene.ScrollViewer.VerticalOffset = expected;
                break;
            case "zeroDurationScrollTo":
                scene.ScrollViewer.ScrollTo(null, expected, TimeSpan.Zero);
                break;
        }

        Assert.Equal(expected, scene.ScrollViewer.VerticalOffset);
        Assert.Equal(expected, scene.ScrollViewer.PendingVerticalOffset);
        Assert.False(scene.ScrollViewer.Animations.IsAnimating(UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset));

        scene.Frames(10);
        Assert.Equal(expected, scene.ScrollViewer.VerticalOffset);
    }

    [Fact]
    public void MaxVerticalOffsetReClamp_DoesNotCancelARunningScrollTo()
    {
        Scene scene = Scene.Build();
        float originalMax = scene.ScrollViewer.MaxVerticalOffset;
        scene.ScrollViewer.ScrollTo(null, originalMax, TimeSpan.FromMilliseconds(320));
        scene.Frames(3);

        for (int i = scene.Items.Length - 1; i >= 5; i--)
        {
            scene.Panel.TryRemoveChild(scene.Items[i]);
        }
        scene.Frames(1);

        Assert.True(scene.ScrollViewer.Animations.IsAnimating(UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset));
    }

    [Fact]
    public void TransitionAttachedToTheVerticalPath_RetargetsFromTheCurrentAnimatedValue_NoSnap()
    {
        Scene scene = Scene.Build();
        scene.ScrollViewer.Transitions.Add(new UITransition<float>(UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset, TimeSpan.FromMilliseconds(300)));

        scene.ScrollViewer.VerticalOffset = 200f;
        scene.Frames(5); // partway through the interpolation
        float midValue = scene.ScrollViewer.VerticalOffset;
        Assert.True(midValue > 0f && midValue < 200f);

        // A second application write mid-interpolation retargets smoothly: the very next frame must not jump backward past midValue,
        // nor snap straight to the new destination.
        scene.ScrollViewer.VerticalOffset = 40f;
        scene.Frames(1);
        float afterRetarget = scene.ScrollViewer.VerticalOffset;
        Assert.True(afterRetarget <= midValue + 1f, $"Expected no upward snap: mid={midValue}, after={afterRetarget}");

        scene.Frames(30);
        Assert.Equal(40f, scene.ScrollViewer.VerticalOffset, 1);
    }

    [Fact]
    public void TransitionAttachedToTheVerticalPath_ContentShrinkingMidRun_EndsAtTheNewBound_WithoutException()
    {
        Scene scene = Scene.Build();
        scene.ScrollViewer.Transitions.Add(new UITransition<float>(UIExtraAnimationTargets.Paths.ScrollViewerVerticalOffset, TimeSpan.FromMilliseconds(300)));

        float originalMax = scene.ScrollViewer.MaxVerticalOffset;
        scene.ScrollViewer.VerticalOffset = originalMax;
        scene.Frames(3); // partway through the transition's own interpolation

        for (int i = scene.Items.Length - 1; i >= 5; i--)
        {
            scene.Panel.TryRemoveChild(scene.Items[i]);
        }

        scene.Frames(30);
        float newMax = scene.ScrollViewer.MaxVerticalOffset;
        Assert.True(newMax < originalMax);
        Assert.Equal(newMax, scene.ScrollViewer.VerticalOffset, 1);
    }

    // ---- Mouse wheel ------------------------------------------------------------------------------------------

    [Fact]
    public void Wheel_ZeroDuration_IsUnchangedFromHead()
    {
        Scene scene = Scene.Build();
        Assert.Equal(TimeSpan.Zero, scene.ScrollViewer.ScrollAnimationDuration);

        scene.ScrollWheel(-120); // scroll down one notch
        Assert.Equal(MGScrollViewer.VerticalScrollInterval, scene.ScrollViewer.VerticalOffset);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);

        scene.ScrollWheel(120); // scroll up one notch
        Assert.Equal(0f, scene.ScrollViewer.VerticalOffset);
    }

    [Fact]
    public void Wheel_NonZeroDuration_ThreeNotches_EndAtThreeIntervals_Clamped()
    {
        Scene scene = Scene.Build();
        scene.ScrollViewer.ScrollAnimationDuration = TimeSpan.FromMilliseconds(120);
        scene.ScrollViewer.ScrollAnimationEasing = UIEasing.CubicOut;

        scene.ScrollWheel(-120);
        scene.ScrollWheel(-120);
        scene.ScrollWheel(-120);

        float expected = Math.Min(3 * MGScrollViewer.VerticalScrollInterval, scene.ScrollViewer.MaxVerticalOffset);
        Assert.Equal(expected, scene.ScrollViewer.PendingVerticalOffset, 1);

        scene.Frames(20); // let the run finish
        Assert.Equal(expected, scene.ScrollViewer.VerticalOffset, 1);
    }

    [Fact]
    public void Wheel_NestedViewers_NonZeroDuration_OuterDoesNotScroll_WhileInnerDestinationIsNotAtItsBound()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, ViewportWidth, ViewportHeight) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };

        MGScrollViewer outer = new(window, ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            ScrollAnimationDuration = TimeSpan.FromMilliseconds(120),
        };

        MGScrollViewer inner = new(window, ScrollBarVisibility.Auto, ScrollBarVisibility.Disabled)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            PreferredHeight = ViewportHeight / 2,
            ScrollAnimationDuration = TimeSpan.FromMilliseconds(120),
        };

        MGStackPanel innerPanel = new(window, Orientation.Vertical) { Spacing = 0 };
        for (int i = 0; i < ItemCount; i++)
        {
            innerPanel.TryAddChild(new MGBorder(window) { PreferredWidth = ViewportWidth, PreferredHeight = ItemHeight });
        }
        inner.SetContent(innerPanel);

        MGStackPanel outerPanel = new(window, Orientation.Vertical) { Spacing = 0 };
        outerPanel.TryAddChild(inner);
        for (int i = 0; i < ItemCount; i++)
        {
            outerPanel.TryAddChild(new MGBorder(window) { PreferredWidth = ViewportWidth, PreferredHeight = ItemHeight });
        }
        outer.SetContent(outerPanel);

        window.SetContent(outer);
        desktop.Windows.Add(window);

        int frameIndex = 0;
        void Frames(int count, int scrollWheelValue)
        {
            for (int i = 0; i < count; i++)
            {
                frameIndex++;
                MouseState state = new(ViewportWidth / 2, inner.PreferredHeight.Value / 2, scrollWheelValue,
                    ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
                runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
                desktop.Update();
            }
        }

        Frames(2, 0);
        Assert.True(inner.MaxVerticalOffset > 0, "The inner viewer needs its own scrollable content for this test to be meaningful.");

        // One notch over the inner viewer: it must be the one consuming it (same as with a zero duration), not the outer.
        Frames(1, -120);
        Assert.True(inner.PendingVerticalOffset > 0);
        Assert.Equal(0f, outer.VerticalOffset);
        Assert.Equal(0f, outer.PendingVerticalOffset);

        // While the inner viewer's destination has not yet reached its own bound, further frames must not bleed into the outer viewer.
        Frames(10, -120);
        Assert.Equal(0f, outer.VerticalOffset);
    }

    // ---- Cost ---------------------------------------------------------------------------------------------------

    private static object AnimationSlot(MGElement element)
        => typeof(MGElement).GetField("_animationSlot", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(element);

    [Fact]
    public void AViewerThatAnimatesNothing_AllocatesNoAnimationSlot_AfterWheelSetterAndEnsureElementVisible()
    {
        Scene scene = Scene.Build();
        Assert.Null(AnimationSlot(scene.ScrollViewer));

        scene.ScrollWheel(-120);
        scene.ScrollViewer.VerticalOffset = 5f;
        scene.ScrollViewer.EnsureElementVisible(scene.Items[^1]);

        Assert.Null(AnimationSlot(scene.ScrollViewer));
    }

    [Fact]
    public void AnActiveScrollToRun_AllocatesNothingPerTick_AfterWarmUp()
    {
        Scene scene = Scene.Build();
        scene.ScrollViewer.ScrollTo(null, scene.ScrollViewer.MaxVerticalOffset, TimeSpan.FromSeconds(100));

        UIAnimationManager manager = scene.Desktop.Animations;
        TimeSpan frame = TimeSpan.FromMilliseconds(FrameMilliseconds);
        for (int i = 0; i < 20; i++)
        {
            manager.Update(frame);
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 200; i++)
        {
            manager.Update(frame);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    // ---- Keyboard: virtualized MGListBox and MGTreeView --------------------------------------------------------

    /// <summary>Advances <paramref name="runtime"/>/<paramref name="desktop"/> by one real frame: <see cref="MGDesktop.Update()"/> alone never
    /// advances <see cref="MGDesktop.Animations"/>'s clock (it reads the elapsed time <see cref="GraphTestRuntime.ApplyFrame"/> last supplied),
    /// so every keyboard-scroll test below must go through this, not a bare <c>desktop.Update()</c> loop.</summary>
    private static void KeyboardFrames(GraphTestRuntime runtime, MGDesktop desktop, ref int frameIndex, int count)
    {
        for (int i = 0; i < count; i++)
        {
            frameIndex++;
            MouseState state = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(FrameMilliseconds * (double)frameIndex), TimeSpan.FromMilliseconds(FrameMilliseconds), state, new KeyboardState()));
            desktop.Update();
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    public void VirtualizedMGListBox_FocusedItem_ScrollsSmoothlyOrInstantly(int durationMilliseconds)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, ViewportWidth, ViewportHeight) { WindowStyle = WindowStyle.None };
        MGListBox<int> listBox = new(window)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            VirtualizationMode = ListBoxVirtualizationMode.Always,
        };
        listBox.ItemTemplate = item => new MGTextBlock(window, item.ToString()) { PreferredHeight = ItemHeight };
        window.SetContent(listBox);
        desktop.Windows.Add(window);
        int frameIndex = 0;
        KeyboardFrames(runtime, desktop, ref frameIndex, 2);

        listBox.SetItemsSource(Enumerable.Range(0, ItemCount).ToList());
        KeyboardFrames(runtime, desktop, ref frameIndex, 2);

        listBox.ScrollViewer.ScrollAnimationDuration = TimeSpan.FromMilliseconds(durationMilliseconds);

        void FocusAndReveal(int index)
        {
            listBox.FocusedIndex = index;
            ((INavigationTargetVisibilityHandler)listBox).EnsureNavigationTargetVisible();
        }

        FocusAndReveal(ItemCount - 1);

        if (durationMilliseconds == 0)
        {
            Assert.Equal(0, desktop.Animations.ActiveCount);
        }
        else
        {
            KeyboardFrames(runtime, desktop, ref frameIndex, 1);
            float afterOneFrame = listBox.ScrollViewer.VerticalOffset;
            Assert.True(afterOneFrame >= 0f && afterOneFrame < listBox.ScrollViewer.PendingVerticalOffset,
                $"Expected a smooth in-flight value strictly below the destination, got {afterOneFrame} (pending {listBox.ScrollViewer.PendingVerticalOffset}).");

            // Several key presses during the same run: re-targeting the same item while it is still in flight.
            FocusAndReveal(ItemCount - 1);

            KeyboardFrames(runtime, desktop, ref frameIndex, 40);
            Assert.Equal(0, desktop.Animations.ActiveCount);
        }

        // The focused item is fully visible once the run ends: a repeat call moves it by at most a rounding pixel.
        float settled = listBox.ScrollViewer.VerticalOffset;
        FocusAndReveal(ItemCount - 1);
        KeyboardFrames(runtime, desktop, ref frameIndex, 1);
        Assert.True(Math.Abs(settled - listBox.ScrollViewer.VerticalOffset) <= 1f,
            $"Expected the repeat call to be a near no-op: settled={settled}, after={listBox.ScrollViewer.VerticalOffset}.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    public void MGTreeView_ScrollIntoView_ScrollsSmoothlyOrInstantly(int durationMilliseconds)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, ViewportWidth, ViewportHeight) { WindowStyle = WindowStyle.None };
        MGTreeView treeView = new(window) { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        MGTreeViewItem[] treeItems = new MGTreeViewItem[ItemCount];
        for (int i = 0; i < ItemCount; i++)
        {
            treeItems[i] = new MGTreeViewItem(window) { Header = $"Item {i}" };
            treeView.AddItem(treeItems[i]);
        }

        window.SetContent(treeView);
        desktop.Windows.Add(window);
        int frameIndex = 0;
        KeyboardFrames(runtime, desktop, ref frameIndex, 2);

        Assert.True(treeView.ScrollViewer.MaxVerticalOffset > 0, "The tree needs scrollable content for this test to be meaningful.");
        treeView.ScrollViewer.ScrollAnimationDuration = TimeSpan.FromMilliseconds(durationMilliseconds);

        MGTreeViewItem lastItem = treeItems[^1];
        treeView.ScrollIntoView(lastItem);

        if (durationMilliseconds == 0)
        {
            Assert.Equal(0, desktop.Animations.ActiveCount);
        }
        else
        {
            KeyboardFrames(runtime, desktop, ref frameIndex, 1);
            float afterOneFrame = treeView.ScrollViewer.VerticalOffset;
            Assert.True(afterOneFrame >= 0f && afterOneFrame < treeView.ScrollViewer.PendingVerticalOffset,
                $"Expected a smooth in-flight value strictly below the destination, got {afterOneFrame} (pending {treeView.ScrollViewer.PendingVerticalOffset}).");

            // Several calls during the same run (repeated key presses navigating toward the same item).
            treeView.ScrollIntoView(lastItem);

            KeyboardFrames(runtime, desktop, ref frameIndex, 40);
            Assert.Equal(0, desktop.Animations.ActiveCount);
        }

        // The focused item is fully visible once the run ends: a repeat call is a no-op (idempotent).
        float settled = treeView.ScrollViewer.VerticalOffset;
        treeView.ScrollIntoView(lastItem);
        KeyboardFrames(runtime, desktop, ref frameIndex, 1);
        Assert.Equal(settled, treeView.ScrollViewer.VerticalOffset, 1);
    }

    // ---- XAML ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Xaml_StrictLoad_SetsScrollAnimationDurationAndEasing()
    {
        const string xaml = "<Window xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" Left=\"0\" Top=\"0\" Width=\"200\" Height=\"200\">" +
            "<ScrollViewer Name=\"SUT\" ScrollAnimationDuration=\"0.2\" ScrollAnimationEasing=\"CubicOut\" />" +
            "</Window>";

        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml), XamlLoaderMode.Strict, false, true);
        desktop.Windows.Add(window);

        MGScrollViewer scrollViewer = window.GetElementByName<MGScrollViewer>("SUT");
        Assert.Equal(TimeSpan.FromSeconds(0.2), scrollViewer.ScrollAnimationDuration);
        Assert.Same(UIEasing.CubicOut, scrollViewer.ScrollAnimationEasing);
    }

    [Fact]
    public void Xaml_InvalidScrollAnimationDuration_IsALoaderError()
    {
        const string xaml = "<Window xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" Left=\"0\" Top=\"0\" Width=\"200\" Height=\"200\">" +
            "<ScrollViewer Name=\"SUT\" ScrollAnimationDuration=\"not-a-duration\" />" +
            "</Window>";

        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        Assert.ThrowsAny<Exception>(() => XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml), XamlLoaderMode.Strict, false, true));
    }

    [Fact]
    public void Xaml_InvalidScrollAnimationEasing_IsALoaderError()
    {
        const string xaml = "<Window xmlns=\"clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core\" Left=\"0\" Top=\"0\" Width=\"200\" Height=\"200\">" +
            "<ScrollViewer Name=\"SUT\" ScrollAnimationEasing=\"NotAnEasing\" />" +
            "</Window>";

        GraphTestRuntime runtime = new(new Rectangle(0, 0, 400, 400));
        MGDesktop desktop = new(runtime);
        Assert.ThrowsAny<Exception>(() => XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(xaml), XamlLoaderMode.Strict, false, true));
    }
}
