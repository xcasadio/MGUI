using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Text;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Text;

public class TextBlockInvalidationTests
{
    [Fact]
    public void StableTextUpdate_WithEquivalentWidth_DoesNotRelayoutParent()
    {
        TextInvalidationHarness harness = CreateLaidOutTextBlock("FPS: 001", textBlock =>
        {
            textBlock.HasStableTextFootprint = true;
            textBlock.WrapText = false;
        });

        harness.TextBlock.SetText("FPS: 002", MGTextInvalidationMode.ReflowLocal);

        Assert.Equal(0, harness.TextBlock.LayoutChangedCallCount);
        Assert.Equal("FPS: 002", FlattenText(harness.TextBlock));
    }

    [Fact]
    public void ReflowLocal_RebuildsLinesWithoutRelayout_WhenDesiredSizeIsStable()
    {
        TextInvalidationHarness harness = CreateLaidOutTextBlock("AAAA\nBBBB", textBlock =>
        {
            textBlock.HasStableTextFootprint = true;
            textBlock.WrapText = true;
        });

        harness.TextBlock.SetText("CCCC\nDDDD", MGTextInvalidationMode.ReflowLocal);

        Assert.Equal(0, harness.TextBlock.LayoutChangedCallCount);
        Assert.Equal(2, harness.TextBlock.Lines.Count);
        Assert.Equal("CCCCDDDD", FlattenText(harness.TextBlock));
    }

    [Fact]
    public void ReflowLocal_EscalatesRelayout_WhenWrappedLineChangesDesiredSize()
    {
        TextInvalidationHarness harness = CreateLaidOutTextBlock("short", textBlock =>
        {
            textBlock.HasStableTextFootprint = true;
            textBlock.WrapText = true;
            textBlock.PreferredWidth = 48;
        });

        harness.TextBlock.SetText("short short short short", MGTextInvalidationMode.ReflowLocal);

        Assert.True(harness.TextBlock.LayoutChangedCallCount > 0);
    }

    [Fact]
    public void MinLines_ReservesStableTelemetryFootprint()
    {
        TextInvalidationHarness harness = CreateLaidOutTextBlock("AAAA", textBlock =>
        {
            textBlock.HasStableTextFootprint = true;
            textBlock.MinLines = 2;
            textBlock.WrapText = true;
        });

        harness.TextBlock.SetText("BBBB\nCCCC", MGTextInvalidationMode.ReflowLocal);

        Assert.Equal(0, harness.TextBlock.LayoutChangedCallCount);
        Assert.Equal(2, harness.TextBlock.Lines.Count);
    }

    [Fact]
    public void MaxLines_InlineFormatting_AndExplicitRuns_RemainCoherent()
    {
        TextInvalidationHarness harness = CreateLaidOutTextBlock("AAAA", textBlock =>
        {
            textBlock.HasStableTextFootprint = true;
            textBlock.MaxLines = 1;
            textBlock.WrapText = true;
        });

        harness.TextBlock.SetText("[color=Red]BBBB[/color]\nCCCC", MGTextInvalidationMode.ReflowLocal);

        Assert.Equal(0, harness.TextBlock.LayoutChangedCallCount);
        Assert.Equal("BBBBCCCC", FlattenText(harness.TextBlock));

        harness.TextBlock.SetTextRuns(new MGTextRun[]
        {
            new MGTextRunText("DDDD", new MGTextRunConfig(false), null, null)
        }, MGTextInvalidationMode.ReflowLocal);

        Assert.Equal(0, harness.TextBlock.LayoutChangedCallCount);
        Assert.True(harness.TextBlock.HasExplicitRuns);
        Assert.Equal("DDDD", FlattenText(harness.TextBlock));

        harness.TextBlock.ClearTextRuns(MGTextInvalidationMode.ReflowLocal);

        Assert.Equal(0, harness.TextBlock.LayoutChangedCallCount);
        Assert.False(harness.TextBlock.HasExplicitRuns);
        Assert.Equal("BBBBCCCC", FlattenText(harness.TextBlock));
    }

    [Fact]
    public void StableMode_FallsBackToRelayout_WhenControlHasNoKnownLayoutWidth()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 320, 180));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 240, 120) { WindowStyle = WindowStyle.None };
        TrackingTextBlock textBlock = new(window, "AAAA")
        {
            HasStableTextFootprint = true
        };
        textBlock.ResetLayoutChangedCount();

        textBlock.SetText("BBBB", MGTextInvalidationMode.ReflowLocal);

        Assert.True(textBlock.LayoutChangedCallCount > 0);
    }

    [Fact]
    public void LegacyBoolSuppressesLayoutWhenStable_ButTextPropertyKeepsSafeRelayoutDefault()
    {
        TextInvalidationHarness harness = CreateLaidOutTextBlock("AAAA", textBlock =>
        {
            textBlock.WrapText = false;
        });

        harness.TextBlock.SetText("BBBB", true);

        Assert.Equal(0, harness.TextBlock.LayoutChangedCallCount);

        harness.TextBlock.Text = "CCCCCC";

        Assert.True(harness.TextBlock.LayoutChangedCallCount > 0);
    }

    private static TextInvalidationHarness CreateLaidOutTextBlock(string initialText, Action<TrackingTextBlock> configure)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16), TimeSpan.FromMilliseconds(16), default, default));

        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 24, 24, 480, 260)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0)
        };

        TrackingTextBlock textBlock = new(window, initialText);
        configure(textBlock);
        window.SetContent(textBlock);
        desktop.Windows.Add(window);
        desktop.Update();
        desktop.Update();
        textBlock.ResetLayoutChangedCount();

        return new(runtime, desktop, window, textBlock);
    }

    private static string FlattenText(MGTextBlock textBlock)
        => string.Concat(textBlock.Runs.OfType<MGTextRunText>().Select(x => x.Text));

    private readonly record struct TextInvalidationHarness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, TrackingTextBlock TextBlock);

    private sealed class TrackingTextBlock : MGTextBlock
    {
        public int LayoutChangedCallCount { get; private set; }

        public TrackingTextBlock(MGWindow window, string text)
            : base(window, text, Color.White, 12)
        {
        }

        public void ResetLayoutChangedCount()
            => LayoutChangedCallCount = 0;

        protected override void LayoutChanged(MGElement Source, bool NotifyParent)
        {
            LayoutChangedCallCount++;
            base.LayoutChanged(Source, NotifyParent);
        }
    }
}