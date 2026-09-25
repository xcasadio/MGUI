using System;
using System.Collections.Generic;
using System.ComponentModel;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.DataBinding;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>ADR-0016, "Animated image sources" coverage: an <see cref="MGImage"/> whose <see cref="MGImage.SourceName"/>
/// names an animation (as decided by the host's <see cref="IUIAssetProvider.TryCreateAnimatedImage"/>) plays it -- one
/// <see cref="IUIAnimatedImage"/> instance per image, advanced every frame by <c>UA.BA.FrameElapsed</c>, drawn at its
/// current frame's image/source-rectangle/draw-offset, with a bindable start offset and playing flag.</summary>
public class AnimatedImageTests
{
    private static readonly TimeSpan FrameDuration = TimeSpan.FromMilliseconds(200);
    private const int FrameCount = 4;

    private sealed class FakeImageResource : IUIImageResource
    {
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed => false;

        public FakeImageResource(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    /// <summary>4 frames of 200ms each: distinct <see cref="IUIImageResource"/> per frame, and frame 2 carries a
    /// non-zero draw offset (a different pivot). Loops forever. <see cref="Advance"/>/<see cref="Restart"/> just
    /// accumulate/replace elapsed time -- deterministic and allocation-free, matching what a real animation clock does.</summary>
    private sealed class FakeAnimatedImage : IUIAnimatedImage
    {
        // All frames share the same 16x16 render size (MGTextureData.RenderSize), so switching frames never invalidates
        // this MGImage's layout -- the frame-timing tests below are about the animation clock, not the layout system.
        public static readonly IUIImageResource[] Frames =
        {
            new FakeImageResource(16, 16),
            new FakeImageResource(16, 16),
            new FakeImageResource(16, 16),
            new FakeImageResource(16, 16),
        };
        public static readonly Rectangle?[] SourceRects =
        {
            new Rectangle(0, 0, 16, 16),
            new Rectangle(1, 0, 16, 16),
            new Rectangle(2, 0, 16, 16),
            new Rectangle(3, 0, 16, 16),
        };
        public static readonly Point[] DrawOffsets =
        {
            Point.Zero,
            Point.Zero,
            new Point(3, 5), // the pivot-shifted frame
            Point.Zero,
        };

        public TimeSpan Elapsed { get; private set; }
        public bool IsDisposed { get; private set; }
        public int AdvanceCallCount { get; private set; }

        public static int FrameIndexAt(TimeSpan elapsed)
        {
            long ticksIntoLoop = elapsed.Ticks % (FrameDuration.Ticks * FrameCount);
            if (ticksIntoLoop < 0)
            {
                ticksIntoLoop += FrameDuration.Ticks * FrameCount;
            }
            return (int)(ticksIntoLoop / FrameDuration.Ticks);
        }

        public int CurrentFrameIndex => FrameIndexAt(Elapsed);
        public IUIImageResource CurrentImage => Frames[CurrentFrameIndex];
        public Rectangle? CurrentSourceRect => SourceRects[CurrentFrameIndex];
        public Point CurrentDrawOffset => DrawOffsets[CurrentFrameIndex];

        public void Advance(TimeSpan elapsed)
        {
            AdvanceCallCount++;
            Elapsed += elapsed;
        }

        public void Restart(TimeSpan startOffset) => Elapsed = startOffset;

        public void Dispose() => IsDisposed = true;
    }

    private sealed class FakeAnimatedImageProvider : IUIAssetProvider
    {
        public readonly List<FakeAnimatedImage> Created = new();
        private readonly Dictionary<string, int> _askCounts = new();

        public int AskCount(string name) => _askCounts.GetValueOrDefault(name);

        public IUIImageResource LoadImage(string assetName) => throw new NotSupportedException("Not used by these tests.");

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = null!;
            return false;
        }

        public bool TryCreateAnimatedImage(string name, out IUIAnimatedImage animatedImage)
        {
            _askCounts[name] = _askCounts.GetValueOrDefault(name) + 1;

            if (name == "anim")
            {
                FakeAnimatedImage created = new();
                Created.Add(created);
                animatedImage = created;
                return true;
            }

            animatedImage = null!;
            return false;
        }
    }

    private sealed class PlayingViewModel : INotifyPropertyChanged
    {
        private bool _playing = true;
        public bool Playing
        {
            get => _playing;
            set { if (_playing != value) { _playing = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Playing))); } }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGStackPanel Panel)
    {
        public static Harness Create(IUIAssetProvider provider)
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540), provider);
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 480, 260)
            {
                WindowStyle = WindowStyle.None,
                Padding = new Thickness(0),
            };
            MGStackPanel panel = new(window, Orientation.Vertical);
            window.SetContent(panel);
            desktop.Windows.Add(window);
            desktop.Update();

            return new(runtime, desktop, window, panel);
        }

        public void Frame(TimeSpan frameElapsed)
        {
            MouseState mouse = new(0, 0, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(Runtime.UpdateArgs.TotalElapsed + frameElapsed, frameElapsed, mouse, new KeyboardState()));
            Desktop.Update();
        }
    }

    // (1) Frame cycling: over at least 25 cycles of the 4 frames, each frame change happens within one 16.67ms step
    // of the k*200ms boundary the fake animation defines.

    [Fact]
    public void SourceName_NamingAnAnimation_CyclesFrames_WithinOneStepOfEachBoundary()
    {
        FakeAnimatedImageProvider provider = new();
        Harness harness = Harness.Create(provider);

        MGImage image = new(harness.Window, "anim", Stretch.None);
        harness.Panel.TryAddChild(image);
        harness.Desktop.Update();

        Assert.NotNull(image.ActualSource);
        FakeAnimatedImage fake = provider.Created[0];

        TimeSpan step = TimeSpan.FromTicks(TimeSpan.FromMilliseconds(1000.0 / 60.0).Ticks); // ~16.67ms, a typical frame step
        TimeSpan totalElapsed = TimeSpan.Zero;
        int stepCount = (int)((FrameDuration.Ticks * FrameCount * 25) / step.Ticks) + 1; // at least 25 full cycles

        for (int i = 0; i < stepCount; i++)
        {
            harness.Frame(step);
            totalElapsed += step;

            int expectedFrame = FakeAnimatedImage.FrameIndexAt(totalElapsed);
            Assert.Same(FakeAnimatedImage.Frames[expectedFrame], image.ActualSource!.Value.Image);
        }
    }

    // (2) Two images naming the same animation each get their own instance, are asked for once each, and stay
    // exactly one frame (200ms) apart when given start offsets 0 and 200ms.

    [Fact]
    public void TwoImages_SameAnimationName_GetOwnInstances_AndStayOneFrameApart()
    {
        FakeAnimatedImageProvider provider = new();
        Harness harness = Harness.Create(provider);

        MGImage image1 = new(harness.Window, "anim", Stretch.None) { AnimationStartOffset = TimeSpan.Zero };
        MGImage image2 = new(harness.Window, "anim", Stretch.None) { AnimationStartOffset = FrameDuration };
        harness.Panel.TryAddChild(image1);
        harness.Panel.TryAddChild(image2);
        harness.Desktop.Update();
        harness.Desktop.Update(); // MGUI's layout engine settles over two passes (matches HostImageResolutionTests)

        Assert.Equal(2, provider.Created.Count);
        Assert.NotSame(provider.Created[0], provider.Created[1]);
        Assert.Equal(2, provider.AskCount("anim"));

        TimeSpan step = TimeSpan.FromMilliseconds(16.67);
        for (int i = 0; i < 60; i++)
        {
            harness.Frame(step);

            int frame1 = FakeAnimatedImage.FrameIndexAt(provider.Created[0].Elapsed);
            int frame2 = FakeAnimatedImage.FrameIndexAt(provider.Created[1].Elapsed);
            Assert.Equal((frame1 + 1) % FrameCount, frame2);
        }
    }

    // (3) A collapsed image is never Update()'d (MGElement's own layout-bounds fast path), so its animation does not
    // advance; making it visible again resumes advancing.

    [Fact]
    public void CollapsedImage_DoesNotAdvance_AndResumesWhenVisibleAgain()
    {
        FakeAnimatedImageProvider provider = new();
        Harness harness = Harness.Create(provider);

        MGImage image = new(harness.Window, "anim", Stretch.None);
        harness.Panel.TryAddChild(image);
        harness.Desktop.Update();

        FakeAnimatedImage fake = provider.Created[0];
        int advanceCallsBeforeCollapse = fake.AdvanceCallCount;

        image.Visibility = Visibility.Collapsed;
        harness.Frame(TimeSpan.FromMilliseconds(16.67));
        harness.Frame(TimeSpan.FromMilliseconds(16.67));
        harness.Frame(TimeSpan.FromMilliseconds(16.67));

        Assert.Equal(advanceCallsBeforeCollapse, fake.AdvanceCallCount);

        image.Visibility = Visibility.Visible;
        harness.Frame(TimeSpan.FromMilliseconds(16.67));

        Assert.True(fake.AdvanceCallCount > advanceCallsBeforeCollapse);
    }

    // (4) IsAnimationPlaying=false holds the first frame (at AnimationStartOffset); true resumes from that offset.
    // Both are settable through an {MGBinding}.

    [Fact]
    public void IsAnimationPlaying_False_HoldsFirstFrame_True_ResumesFromStartOffset()
    {
        FakeAnimatedImageProvider provider = new();
        Harness harness = Harness.Create(provider);

        MGImage image = new(harness.Window, "anim", Stretch.None) { AnimationStartOffset = FrameDuration }; // starts on frame 1
        harness.Panel.TryAddChild(image);
        harness.Desktop.Update();

        FakeAnimatedImage fake = provider.Created[0];
        Assert.Same(FakeAnimatedImage.Frames[1], image.ActualSource!.Value.Image);

        image.IsAnimationPlaying = false;
        Assert.Same(FakeAnimatedImage.Frames[1], image.ActualSource!.Value.Image); // held at the restarted offset

        harness.Frame(TimeSpan.FromMilliseconds(16.67));
        harness.Frame(TimeSpan.FromMilliseconds(500));
        Assert.Same(FakeAnimatedImage.Frames[1], image.ActualSource!.Value.Image); // still held, not advancing

        image.IsAnimationPlaying = true;
        Assert.Same(FakeAnimatedImage.Frames[1], image.ActualSource!.Value.Image); // resumed exactly at the start offset

        harness.Frame(TimeSpan.FromMilliseconds(16.67));
        Assert.True(fake.Elapsed > FrameDuration); // advancing again
    }

    [Fact]
    public void IsAnimationPlaying_And_AnimationStartOffset_AreSettableThroughBinding()
    {
        FakeAnimatedImageProvider provider = new();
        Harness harness = Harness.Create(provider);

        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
        xmlns:dataBinding=""clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core""
        Width=""200"" Height=""150"" Padding=""0"">
    <Image Name=""Img"" SourceName=""anim"" AnimationStartOffset=""0:0:0.2"" IsAnimationPlaying=""{dataBinding:MGBinding Path=Playing}"" />
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        PlayingViewModel vm = new() { Playing = false };
        window.WindowDataContext = vm;
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();
        harness.Desktop.Update();

        MGImage image = window.GetElementByName<MGImage>("Img");
        Assert.Equal(FrameDuration, image.AnimationStartOffset);
        Assert.False(image.IsAnimationPlaying);

        vm.Playing = true;
        harness.Desktop.Update();
        Assert.True(image.IsAnimationPlaying);

        harness.Desktop.Windows.Remove(window);
    }

    // (5) Changing SourceName disposes the previous animated instance.

    [Fact]
    public void ChangingSourceName_DisposesPreviousAnimatedInstance()
    {
        FakeAnimatedImageProvider provider = new();
        Harness harness = Harness.Create(provider);

        MGImage image = new(harness.Window, "anim", Stretch.None);
        harness.Panel.TryAddChild(image);
        harness.Desktop.Update();

        FakeAnimatedImage first = provider.Created[0];
        Assert.False(first.IsDisposed);

        image.SourceName = null; // changes away from the animated source
        Assert.True(first.IsDisposed);

        image.SourceName = "anim"; // changes back -> a brand new instance (each resolution creates its own)
        Assert.Equal(2, provider.Created.Count);
        Assert.False(provider.Created[1].IsDisposed);

        image.SourceName = null;
        Assert.True(provider.Created[1].IsDisposed);
    }

    // (6) Zero bytes allocated per frame for the animated image's own per-frame work (Advance + copying the current
    // frame into ActualSource). Measured by calling MGImage.UpdateSelf directly with a pre-built ElementUpdateArgs,
    // bypassing MGDesktop.Update()/the rest of the element tree so unrelated desktop-side allocations (animations,
    // input tracking, focus, etc.) cannot pollute the measurement.

    [Fact]
    public void PerFrameAnimationWork_AllocatesNothing()
    {
        FakeAnimatedImageProvider provider = new();
        Harness harness = Harness.Create(provider);

        MGImage image = new(harness.Window, "anim", Stretch.None);
        harness.Panel.TryAddChild(image);
        harness.Desktop.Update(); // resolves the animated source once

        Assert.NotNull(image.ActualSource);

        TimeSpan frameElapsed = TimeSpan.FromMilliseconds(16.67);
        ElementUpdateArgs ua = new(new UpdateBaseArgs(TimeSpan.Zero, frameElapsed, default, default),
            true, false, true, Point.Zero, image.ActualLayoutBounds);

        void Tick() => image.UpdateSelf(ua);

        for (int i = 0; i < 2000; i++)
        {
            Tick(); // warm-up: JIT the update/advance/ActualSource-copy path
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            Tick();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    // (7) The current frame's draw offset is applied when drawing. GraphNoOpDrawTransaction (the test runtime's
    // IUIDrawTransaction) records every DrawTextureTo call, so this asserts on the recorded destination rectangle
    // directly rather than through a smaller seam.

    [Fact]
    public void DrawSelf_TranslatesDestinationByCurrentFrameDrawOffset()
    {
        FakeAnimatedImageProvider provider = new();
        Harness harness = Harness.Create(provider);

        MGImage image = new(harness.Window, "anim", Stretch.None) // frame 2 (at offset 2*200ms) is the pivot-shifted one
        {
            AnimationStartOffset = FrameDuration * 2,
            // Pin content to the top-left corner so the destination rectangle reflects only the draw offset,
            // not MGImage's own content-alignment centering of the unstretched 16x16 frame within a 40x40 layout box.
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top,
        };
        harness.Panel.TryAddChild(image);
        harness.Desktop.Update();

        Assert.Equal(new Point(3, 5), FakeAnimatedImage.DrawOffsets[2]);
        Assert.Same(FakeAnimatedImage.Frames[2], image.ActualSource!.Value.Image);

        GraphNoOpDrawTransaction transaction = new(harness.Runtime, DrawSettings.Default);
        DrawBaseArgs ba = new(TimeSpan.Zero, transaction, 1f);
        ElementDrawArgs da = new(ba, new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
        Rectangle layoutBounds = new(100, 200, 40, 40);

        image.DrawSelf(da, layoutBounds);

        Assert.Single(transaction.DrawTextureToCalls);
        var call = transaction.DrawTextureToCalls[0];
        Assert.Same(FakeAnimatedImage.Frames[2], call.Texture);
        Assert.Equal(3, call.Destination.X - layoutBounds.X);
        Assert.Equal(5, call.Destination.Y - layoutBounds.Y);
    }
}
