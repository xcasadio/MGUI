using System;
using System.Collections.Generic;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.DataBinding;
using MGUI.Shared.Rendering;
using XamlDocumentSource = MGUI.Core.UI.XAML.XamlDocumentSource;
using XamlLoaderMode = MGUI.Core.UI.XAML.XamlLoaderMode;
using MGUI.Shared.Assets;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Architecture;

/// <summary>ADR-0039 coverage: an <see cref="MGVideo"/> whose <see cref="MGImage.SourceName"/> names a video (as decided by the host's
/// <see cref="IUIAssetProvider.TryCreateVideo"/>) advances its own <see cref="IUIVideo"/> in <see cref="MGVideo.UpdateSelf"/>, draws its current frame,
/// reports its end or its skip once (event and named command), and disposes the video when the source changes or the element is disposed.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class VideoElementTests
{
    private static readonly TimeSpan FramePeriod = TimeSpan.FromMilliseconds(100);
    private const int FrameCount = 5;

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

    /// <summary>Plays <see cref="FrameCount"/> frames of <see cref="FramePeriod"/> once. One image for every frame, as a host that updates a texture in place
    /// would return; the source rectangle moves with the frame, at a constant size.</summary>
    private sealed class FakeVideo : IUIVideo
    {
        public static readonly IUIImageResource Image = new FakeImageResource(32, 32);

        public TimeSpan Elapsed { get; private set; }
        public bool IsDisposed { get; private set; }
        public int AdvanceCallCount { get; private set; }
        public int RestartCallCount { get; private set; }

        public int FrameIndex => (int)Math.Min(FrameCount - 1, Elapsed.Ticks / FramePeriod.Ticks);

        public IUIImageResource CurrentImage => Image;
        public Rectangle? CurrentSourceRect => new Rectangle(FrameIndex, 0, 16, 16);
        public bool IsEnded => Elapsed.Ticks >= FramePeriod.Ticks * FrameCount;

        public void Advance(TimeSpan elapsed)
        {
            AdvanceCallCount++;
            Elapsed += elapsed;
        }

        public void Restart()
        {
            RestartCallCount++;
            Elapsed = TimeSpan.Zero;
        }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class FakeVideoProvider : IUIAssetProvider
    {
        public readonly List<FakeVideo> Created = new();

        public IUIImageResource LoadImage(string assetName) => new FakeImageResource(8, 8); // the default resources load their icons

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = null!;
            return false;
        }

        public bool TryCreateVideo(string name, out IUIVideo video)
        {
            if (name == "clip")
            {
                FakeVideo created = new();
                Created.Add(created);
                video = created;
                return true;
            }

            video = null!;
            return false;
        }
    }

    /// <summary>Does not override <see cref="IUIAssetProvider.TryCreateVideo"/>: proves the default implementation.</summary>
    private sealed class VideolessProvider : IUIAssetProvider
    {
        public IUIImageResource LoadImage(string assetName) => throw new NotSupportedException();

        public bool TryLoadImage(string assetName, out IUIImageResource image)
        {
            image = null!;
            return false;
        }
    }

    private sealed class CountingVideo : MGVideo
    {
        public int LayoutChangedCallCount { get; private set; }

        public CountingVideo(MGWindow window, string sourceName) : base(window, sourceName, Stretch.None) { }

        protected override void LayoutChanged(MGElement source, bool notifyParent)
        {
            LayoutChangedCallCount++;
            base.LayoutChanged(source, notifyParent);
        }
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

    private static (Harness Harness, FakeVideoProvider Provider, MGVideo Video, FakeVideo Fake) CreatePlayingVideo(bool skippable = false)
    {
        FakeVideoProvider provider = new();
        Harness harness = Harness.Create(provider);
        MGVideo video = new(harness.Window, "clip", Stretch.None) { IsSkippable = skippable };
        harness.Panel.TryAddChild(video);
        harness.Desktop.Update();
        return (harness, provider, video, provider.Created[0]);
    }

    [Fact]
    public void SourceName_NamingAVideo_AdvancesItEveryFrame_AndDrawsItsCurrentFrame()
    {
        var (harness, provider, video, fake) = CreatePlayingVideo();

        Assert.Single(provider.Created);
        Assert.Equal(MGElementType.Video, video.ElementType);
        Assert.Same(FakeVideo.Image, video.ActualSource!.Value.Image);

        int advancesBefore = fake.AdvanceCallCount;
        harness.Frame(FramePeriod);
        harness.Frame(FramePeriod);

        Assert.Equal(advancesBefore + 2, fake.AdvanceCallCount);
        Assert.Equal(2, fake.FrameIndex);
        Assert.Equal(new Rectangle(2, 0, 16, 16), video.ActualSource!.Value.SourceRect);
        Assert.True(video.IsPlaying);
    }

    [Fact]
    public void ReachingTheEnd_RaisesEndedOnce_AndRunsItsCommandOnce_AndKeepsTheLastFrame()
    {
        var (harness, _, video, fake) = CreatePlayingVideo();
        int endedEvents = 0;
        int endedCommands = 0;
        int otherCommands = 0;
        video.Ended += (_, _) => endedEvents++;
        harness.Desktop.Resources.AddCommand("on_end", element =>
        {
            Assert.Same(video, element);
            endedCommands++;
        });
        harness.Desktop.Resources.AddCommand("on_skip", _ => otherCommands++);
        video.EndedCommandName = "on_end";
        video.SkippedCommandName = "on_skip";

        for (int i = 0; i < FrameCount - 1; i++)
        {
            harness.Frame(FramePeriod);
        }
        Assert.Equal(0, endedEvents);
        Assert.True(video.IsPlaying);

        harness.Frame(FramePeriod);
        Assert.Equal(1, endedEvents);
        Assert.Equal(1, endedCommands);
        Assert.False(video.IsPlaying);

        int advancesAtEnd = fake.AdvanceCallCount;
        harness.Frame(FramePeriod);
        harness.Frame(FramePeriod);

        Assert.Equal(1, endedEvents);
        Assert.Equal(1, endedCommands);
        Assert.Equal(0, otherCommands);
        Assert.Equal(advancesAtEnd, fake.AdvanceCallCount); // the ended video is no longer advanced
        Assert.Equal(new Rectangle(FrameCount - 1, 0, 16, 16), video.ActualSource!.Value.SourceRect);
    }

    [Fact]
    public void EndedCommandName_NamingNoCommand_DoesNothingButTheEvent()
    {
        var (harness, _, video, _) = CreatePlayingVideo();
        int endedEvents = 0;
        video.Ended += (_, _) => endedEvents++;
        video.EndedCommandName = "undeclared";

        for (int i = 0; i < FrameCount; i++)
        {
            harness.Frame(FramePeriod);
        }

        Assert.Equal(1, endedEvents);
    }

    [Fact]
    public void Skip_OnASkippableVideo_RaisesSkippedOnce_AndRunsItsCommand_AndNeverRaisesEnded()
    {
        var (harness, _, video, fake) = CreatePlayingVideo(skippable: true);
        int skippedEvents = 0;
        int endedEvents = 0;
        int skippedCommands = 0;
        video.Skipped += (_, _) => skippedEvents++;
        video.Ended += (_, _) => endedEvents++;
        harness.Desktop.Resources.AddCommand("on_skip", _ => skippedCommands++);
        video.SkippedCommandName = "on_skip";

        harness.Frame(FramePeriod);

        Assert.True(video.Skip());
        Assert.False(video.Skip());
        Assert.Equal(1, skippedEvents);
        Assert.Equal(1, skippedCommands);
        Assert.False(video.IsPlaying);

        int advancesAfterSkip = fake.AdvanceCallCount;
        for (int i = 0; i < FrameCount + 2; i++)
        {
            harness.Frame(FramePeriod);
        }

        Assert.Equal(advancesAfterSkip, fake.AdvanceCallCount);
        Assert.Equal(0, endedEvents);
        Assert.Equal(1, skippedEvents);
    }

    [Fact]
    public void Skip_OnAVideoThatIsNotSkippable_DoesNothing()
    {
        var (harness, _, video, fake) = CreatePlayingVideo(skippable: false);
        int skippedEvents = 0;
        int skippedCommands = 0;
        video.Skipped += (_, _) => skippedEvents++;
        harness.Desktop.Resources.AddCommand("on_skip", _ => skippedCommands++);
        video.SkippedCommandName = "on_skip";

        Assert.False(video.Skip());
        int advancesBefore = fake.AdvanceCallCount;
        harness.Frame(FramePeriod);

        Assert.Equal(0, skippedEvents);
        Assert.Equal(0, skippedCommands);
        Assert.True(video.IsPlaying);
        Assert.Equal(advancesBefore + 1, fake.AdvanceCallCount);
    }

    [Fact]
    public void Skip_AfterTheVideoEnded_DoesNothing()
    {
        var (harness, _, video, _) = CreatePlayingVideo(skippable: true);
        int skippedEvents = 0;
        video.Skipped += (_, _) => skippedEvents++;

        for (int i = 0; i < FrameCount; i++)
        {
            harness.Frame(FramePeriod);
        }

        Assert.False(video.Skip());
        Assert.Equal(0, skippedEvents);
    }

    [Fact]
    public void Restart_PlaysAgain_AndRaisesEndedAgain()
    {
        var (harness, _, video, fake) = CreatePlayingVideo();
        int endedEvents = 0;
        video.Ended += (_, _) => endedEvents++;

        for (int i = 0; i < FrameCount; i++)
        {
            harness.Frame(FramePeriod);
        }
        Assert.Equal(1, endedEvents);

        video.Restart();
        Assert.Equal(1, fake.RestartCallCount);
        Assert.True(video.IsPlaying);
        Assert.Equal(new Rectangle(0, 0, 16, 16), video.ActualSource!.Value.SourceRect);

        for (int i = 0; i < FrameCount; i++)
        {
            harness.Frame(FramePeriod);
        }
        Assert.Equal(2, endedEvents);
    }

    [Fact]
    public void ACollapsedVideo_DoesNotAdvance_AndResumesWhenVisibleAgain()
    {
        var (harness, _, video, fake) = CreatePlayingVideo();
        int advancesBeforeCollapse = fake.AdvanceCallCount;

        video.Visibility = Visibility.Collapsed;
        harness.Frame(FramePeriod);
        harness.Frame(FramePeriod);
        harness.Frame(FramePeriod);

        Assert.Equal(advancesBeforeCollapse, fake.AdvanceCallCount);

        video.Visibility = Visibility.Visible;
        harness.Frame(FramePeriod);

        Assert.True(fake.AdvanceCallCount > advancesBeforeCollapse);
    }

    [Fact]
    public void TheVideo_IsDisposed_WhenTheSourceChanges_AndWhenTheElementIsDisposed()
    {
        var (_, provider, video, first) = CreatePlayingVideo();
        Assert.False(first.IsDisposed);

        video.SourceName = null;
        Assert.True(first.IsDisposed);
        Assert.Null(video.ActualSource);
        Assert.False(video.IsPlaying);

        video.SourceName = "clip"; // a brand new instance: each resolution creates its own
        Assert.Equal(2, provider.Created.Count);
        FakeVideo second = provider.Created[1];
        Assert.False(second.IsDisposed);
        Assert.NotNull(video.ActualSource);

        video.Dispose();
        Assert.True(second.IsDisposed);
        Assert.Null(video.ActualSource);
        Assert.False(video.IsPlaying);

        video.SourceName = null;
        video.SourceName = "clip"; // a disposed element creates nothing any more
        Assert.Equal(2, provider.Created.Count);
        video.Dispose(); // twice is harmless
    }

    [Fact]
    public void SettingSource_HasNoEffect_AndDoesNotReplaceOrRestartTheVideo()
    {
        var (harness, provider, video, fake) = CreatePlayingVideo();
        harness.Frame(FramePeriod);
        TimeSpan elapsed = fake.Elapsed;

        video.Source = new MGTextureData(new FakeImageResource(4, 4), null);

        Assert.Single(provider.Created);
        Assert.False(fake.IsDisposed);
        Assert.Equal(elapsed, fake.Elapsed);
        Assert.Same(FakeVideo.Image, video.ActualSource!.Value.Image);
        Assert.True(video.IsPlaying);
    }

    [Fact]
    public void ANameTheHostDoesNotKnow_PlaysNothing()
    {
        FakeVideoProvider provider = new();
        Harness harness = Harness.Create(provider);
        MGVideo video = new(harness.Window, "unknown");
        harness.Panel.TryAddChild(video);

        harness.Frame(FramePeriod);

        Assert.Null(video.ActualSource);
        Assert.False(video.IsPlaying);
        Assert.False(video.Skip());
    }

    [Fact]
    public void AProviderWithoutTheMember_KeepsCompiling_AndCreatesNothing()
    {
        IUIAssetProvider provider = new VideolessProvider();

        Assert.False(provider.TryCreateVideo("clip", out IUIVideo video));
        Assert.Null(video);
    }

    [Fact]
    public void ChangingTheFrame_DoesNotRelayout_WhileTheSizeIsConstant()
    {
        FakeVideoProvider provider = new();
        Harness harness = Harness.Create(provider);
        CountingVideo video = new(harness.Window, "clip");
        harness.Panel.TryAddChild(video);
        harness.Desktop.Update();
        harness.Desktop.Update();

        int before = video.LayoutChangedCallCount;
        for (int i = 0; i < FrameCount - 1; i++)
        {
            harness.Frame(FramePeriod);
        }

        Assert.Equal(FrameCount - 1, provider.Created[0].FrameIndex);
        Assert.Equal(before, video.LayoutChangedCallCount);
    }

    [Fact]
    public void PerFrameWork_AllocatesNothing()
    {
        var (_, _, video, fake) = CreatePlayingVideo();

        // One tick per call keeps the fake video from ending during the measurement.
        ElementUpdateArgs ua = new(new UpdateBaseArgs(TimeSpan.Zero, TimeSpan.FromTicks(1), default, default),
            true, false, true, Point.Zero, video.ActualLayoutBounds);

        void Tick() => video.UpdateSelf(ua);

        for (int i = 0; i < 2000; i++)
        {
            Tick();
        }

        int advancesBefore = fake.AdvanceCallCount;
        long before = AllocationWindow.Start();
        for (int i = 0; i < 1000; i++)
        {
            Tick();
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
        Assert.Equal(advancesBefore + 1000, fake.AdvanceCallCount);
        Assert.True(video.IsPlaying);
    }

    [Fact]
    public void TheXamlTag_IsReadWithItsAttributes()
    {
        FakeVideoProvider provider = new();
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540), provider);
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();
        int endedCommands = 0;
        int skippedCommands = 0;
        desktop.Resources.AddCommand("intro_done", _ => endedCommands++);
        desktop.Resources.AddCommand("intro_skipped", _ => skippedCommands++);

        const string markup = """
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="400" Height="200">
              <Video Name="vid" SourceName="clip" Stretch="Fill" IsSkippable="True"
                     EndedCommandName="intro_done" SkippedCommandName="intro_skipped" />
            </Window>
            """;

        MGWindow window = MGUIXamlParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(markup, "video-test"), XamlLoaderMode.Strict);
        Assert.True(window.TryGetElementByName("vid", out MGVideo video));

        Assert.Equal("clip", video.SourceName);
        Assert.Equal(Stretch.Fill, video.Stretch);
        Assert.True(video.IsSkippable);
        Assert.Equal("intro_done", video.EndedCommandName);
        Assert.Equal("intro_skipped", video.SkippedCommandName);
        Assert.Single(provider.Created);
        Assert.NotNull(video.ActualSource);

        Assert.True(video.Skip());
        Assert.Equal(1, skippedCommands);
        Assert.Equal(0, endedCommands);
    }

    [Fact]
    public void TheXamlTag_WithNoOptionalAttribute_KeepsTheDefaults()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540), new FakeVideoProvider());
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();

        const string markup = """
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" Left="0" Top="0" Width="400" Height="200">
              <Video Name="vid" SourceName="clip" />
            </Window>
            """;

        MGWindow window = MGUIXamlParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(markup, "video-test"), XamlLoaderMode.Strict);
        Assert.True(window.TryGetElementByName("vid", out MGVideo video));

        Assert.False(video.IsSkippable);
        Assert.Null(video.EndedCommandName);
        Assert.Null(video.SkippedCommandName);
        Assert.Equal(Stretch.Uniform, video.Stretch);
    }

    [Fact]
    public void TheXamlTag_LoadsThroughTheSanitizedPath_WithNoNamespaceDeclared()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540), new FakeVideoProvider());
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();

        const string markup = """
            <Window Left="0" Top="0" Width="400" Height="200">
              <Video Name="vid" SourceName="clip" IsSkippable="True" />
            </Window>
            """;

        MGWindow window = MGUIXamlParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(markup, "video-test"), XamlLoaderMode.Strict, SanitizeXAMLString: true);
        Assert.True(window.TryGetElementByName("vid", out MGVideo video));
        Assert.Equal("clip", video.SourceName);
        Assert.True(video.IsSkippable);
    }
}
