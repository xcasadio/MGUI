using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.FillBrushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Animation;

/// <summary>Slice U6 of Docs/Tasks/animation-v3-tasks.md: the multi-track keyframe clip format (<see cref="UIKeyFrameClipSerializer"/>).</summary>
public class KeyFrameClipTests
{
    private const float Tolerance = 1e-4f;

    private static Color BackgroundOf(AnimationTestScene scene)
        => ((MGSolidFillBrush)scene.Top.BackgroundBrush.NormalValue).Color;

    private static UIStoryboard BuildClip(TimeSpan duration)
    {
        UIStoryboard storyboard = new()
        {
            new UIKeyFrameAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity)
            {
                Duration = duration,
                Track = { { 0f, 0f }, { 1f, 1f } },
            },
            new UIKeyFrameAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale)
            {
                Duration = duration,
                Track = { { 0f, new Vector2(0.8f) }, { 1f, new Vector2(1f) } },
            },
            new UIKeyFrameAnimation<Color>(UIColorAnimationTargets.Paths.Background)
            {
                Duration = duration,
                Track = { { 0f, new Color(0, 0, 0, 0) }, { 1f, new Color(255, 0, 0, 255) } },
            },
        };
        storyboard.Duration = duration;
        return storyboard;
    }

    [Fact]
    public void Deserialize_PlaysTheThreeTracksInParallel_LikeTheSampleStoryboard()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(0, 0, 0, 0));
        string json = UIKeyFrameClipSerializer.Serialize(BuildClip(TimeSpan.FromMilliseconds(160)));

        UIStoryboard clip = UIKeyFrameClipSerializer.Deserialize(json);
        scene.Top.Animations.Start(clip);

        Assert.Equal(UIAnimationState.Running, clip.State);
        Assert.Equal(TimeSpan.FromMilliseconds(160), clip.Duration);

        scene.Frames(5); // 80 / 160 = 0.5
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        Assert.Equal(0.9f, scene.Top.RenderTransform.Scale.X, Tolerance);
        Assert.Equal((byte)127, BackgroundOf(scene).A);

        scene.Frames(5); // 160 / 160 = 1
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(1f, scene.Top.RenderTransform.Scale.X, Tolerance);
        Assert.Equal(UIAnimationState.Completed, clip.State);
        Assert.All(clip.Children, child => Assert.Equal(UIAnimationState.Completed, child.State));
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void RoundTrip_BuildSerializeDeserializeSerialize_ProducesIdenticalJson()
    {
        UIStoryboard original = BuildClip(TimeSpan.FromMilliseconds(250));

        string firstJson = UIKeyFrameClipSerializer.Serialize(original);
        UIStoryboard restored = UIKeyFrameClipSerializer.Deserialize(firstJson);
        string secondJson = UIKeyFrameClipSerializer.Serialize(restored);

        Assert.Equal(firstJson, secondJson);
        Assert.Equal(3, restored.Children.Count);
        Assert.All(restored.Children, child => Assert.Equal(TimeSpan.FromMilliseconds(250), child.Duration));
        Assert.Equal(UIBuiltInAnimationTargets.Paths.Opacity, ((UIKeyFrameAnimation<float>)restored.Children[0]).Property);
        Assert.Equal(UIBuiltInAnimationTargets.Paths.RenderTransformScale, ((UIKeyFrameAnimation<Vector2>)restored.Children[1]).Property);
        Assert.Equal(UIColorAnimationTargets.Paths.Background, ((UIKeyFrameAnimation<Color>)restored.Children[2]).Property);
    }

    [Fact]
    public void RoundTrip_ToleratesWhitespaceAndReorderedProperties_AndCanonicalizes()
    {
        string handWritten = """
        {
          "tracks":   [
            {   "frames": [ { "value": "0",   "offset": 0 }, { "easing": null, "offset": 1, "value": "1" } ], "valueType": "Single", "property": "Opacity" }
          ],
          "duration": "00:00:00.5000000",
          "version": 1
        }
        """;

        UIStoryboard clip = UIKeyFrameClipSerializer.Deserialize(handWritten);
        string canonical = UIKeyFrameClipSerializer.Serialize(clip);
        UIStoryboard reparsed = UIKeyFrameClipSerializer.Deserialize(canonical);
        string reserialized = UIKeyFrameClipSerializer.Serialize(reparsed);

        Assert.Equal(canonical, reserialized);
        Assert.Equal(TimeSpan.FromMilliseconds(500), clip.Duration);
    }

    [Fact]
    public void RoundTrip_AColorFrameWithAlpha_AndAThicknessTrack_PlayAndRoundTrip()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(new Color(10, 20, 30, 40));
        TimeSpan duration = TimeSpan.FromMilliseconds(100);
        UIStoryboard clip = new()
        {
            new UIKeyFrameAnimation<Color>(UIColorAnimationTargets.Paths.Background)
            {
                Duration = duration,
                Track = { { 0f, new Color(10, 20, 30, 40) }, { 1f, new Color(200, 150, 100, 250) } },
            },
            new UIKeyFrameAnimation<Thickness>(UIBuiltInAnimationTargets.Paths.Margin)
            {
                Duration = duration,
                Track = { { 0f, new Thickness(0, 0, 0, 0) }, { 1f, new Thickness(4, 8, 12, 16) } },
            },
        };
        clip.Duration = duration;

        string json = UIKeyFrameClipSerializer.Serialize(clip);
        UIStoryboard restored = UIKeyFrameClipSerializer.Deserialize(json);
        string reserialized = UIKeyFrameClipSerializer.Serialize(restored);
        Assert.Equal(json, reserialized);

        scene.Top.Animations.Start(restored);
        scene.Frames(20); // full duration elapsed

        Assert.Equal(new Color(200, 150, 100, 250), BackgroundOf(scene));
        Assert.Equal(new Thickness(4, 8, 12, 16), scene.Top.Margin);
        Assert.All(restored.Children, child => Assert.Equal(UIAnimationState.Completed, child.State));
    }

    [Fact]
    public void Deserialize_RejectsAnUnknownPath_ListingTheKnownPaths()
    {
        string json = UIKeyFrameClipSerializer.Serialize(BuildClip(TimeSpan.FromMilliseconds(100)))
            .Replace("\"Opacity\"", "\"Nope.Unknown\"");

        var ex = Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Deserialize(json));
        Assert.Contains("Nope.Unknown", ex.Message);
        Assert.Contains(UIBuiltInAnimationTargets.Paths.Opacity, ex.Message);
    }

    [Fact]
    public void Deserialize_RejectsAValueTypeMismatch()
    {
        string json = UIKeyFrameClipSerializer.Serialize(BuildClip(TimeSpan.FromMilliseconds(100)))
            .Replace("\"valueType\": \"Single\"", "\"valueType\": \"Int32\"");

        var ex = Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Deserialize(json));
        Assert.Contains("Int32", ex.Message);
        Assert.Contains("Single", ex.Message);
    }

    [Fact]
    public void Deserialize_RejectsAnUnsupportedTypeName()
    {
        // MinHeight targets int? (see UIBuiltInAnimationTargets.MinHeightTarget), a value type Format/Parse do not support (only plain int).
        string valueType = UIAnimationTargets.GetValueType(UIBuiltInAnimationTargets.Paths.MinHeight).Name;
        string json = $$"""{ "version": 1, "duration": "00:00:00.1000000", "tracks": [ { "property": "MinHeight", "valueType": "{{valueType}}", "frames": [ { "offset": 0, "value": "0" }, { "offset": 1, "value": "1" } ] } ] }""";

        Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_RejectsAnUnknownVersion()
    {
        string json = UIKeyFrameClipSerializer.Serialize(BuildClip(TimeSpan.FromMilliseconds(100)))
            .Replace("\"version\": 1", "\"version\": 2");

        Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_RejectsEmptyTracksAndEmptyFrames()
    {
        Assert.Throws<ArgumentException>(() => UIKeyFrameClipSerializer.Deserialize(""));
        Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Deserialize("{}"));
        Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Deserialize("""{ "version": 1, "duration": "00:00:00.1000000", "tracks": [] }"""));
        Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Deserialize(
            """{ "version": 1, "duration": "00:00:00.1000000", "tracks": [ { "property": "Opacity", "valueType": "Single", "frames": [] } ] }"""));
    }

    [Fact]
    public void Deserialize_RejectsFramesNotEndingAtOffsetOne()
    {
        string json = """{ "version": 1, "duration": "00:00:00.1000000", "tracks": [ { "property": "Opacity", "valueType": "Single", "frames": [ { "offset": 0, "value": "0" }, { "offset": 0.5, "value": "1" } ] } ] }""";

        Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Deserialize(json));
    }

    [Fact]
    public void Serialize_RejectsANonKeyFrameChild()
    {
        TimeSpan duration = TimeSpan.FromMilliseconds(100);
        UIStoryboard storyboard = new()
        {
            new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = duration },
        };
        storyboard.Duration = duration;

        var ex = Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Serialize(storyboard));
        Assert.Contains("Track 0", ex.Message);
    }

    [Fact]
    public void Serialize_RejectsADelayedChild()
    {
        TimeSpan duration = TimeSpan.FromMilliseconds(100);
        UIStoryboard storyboard = new()
        {
            new UIKeyFrameAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity)
            {
                Duration = duration,
                Delay = TimeSpan.FromMilliseconds(10),
                Track = { { 0f, 0f }, { 1f, 1f } },
            },
        };
        storyboard.Duration = duration;

        var ex = Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Serialize(storyboard));
        Assert.Contains("Delay", ex.Message);
    }

    [Fact]
    public void Serialize_RejectsAChildWithADifferentDuration()
    {
        UIStoryboard storyboard = new()
        {
            new UIKeyFrameAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity)
            {
                Duration = TimeSpan.FromMilliseconds(100),
                Track = { { 0f, 0f }, { 1f, 1f } },
            },
            new UIKeyFrameAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale)
            {
                Duration = TimeSpan.FromMilliseconds(50),
                Track = { { 0f, Vector2.Zero }, { 1f, Vector2.One } },
            },
        };
        storyboard.Duration = TimeSpan.FromMilliseconds(100);

        var ex = Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Serialize(storyboard));
        Assert.Contains("Track 1", ex.Message);
        Assert.Contains("Duration", ex.Message);
    }

    [Fact]
    public void Serialize_RejectsAnUnknownPath_AsInvalidOperationException_NotTargetInvocationException()
    {
        // Regression (U6 fix round 1): SerializeChild invokes the generic SerializeTrack<T> via reflection; without an unwrapping
        // try/catch, this refusal (raised deep inside SerializeTrack) used to escape as TargetInvocationException instead of the
        // documented InvalidOperationException, so a caller catching InvalidOperationException per the XML doc never caught it.
        TimeSpan duration = TimeSpan.FromMilliseconds(100);
        UIStoryboard storyboard = new()
        {
            new UIKeyFrameAnimation<float>("SomeCustomPathTheEditorInvented")
            {
                Duration = duration,
                Track = { { 0f, 0f }, { 1f, 1f } },
            },
        };
        storyboard.Duration = duration;

        var ex = Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Serialize(storyboard));
        Assert.Contains("SomeCustomPathTheEditorInvented", ex.Message);
        Assert.Contains(UIBuiltInAnimationTargets.Paths.Opacity, ex.Message);
    }

    [Fact]
    public void Serialize_RejectsAChildWithNoPropertyPath_AsInvalidOperationException()
    {
        TimeSpan duration = TimeSpan.FromMilliseconds(100);
        UIStoryboard storyboard = new()
        {
            new UIKeyFrameAnimation<float>
            {
                Duration = duration,
                Track = { { 0f, 0f }, { 1f, 1f } },
            },
        };
        storyboard.Duration = duration;

        var ex = Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Serialize(storyboard));
        Assert.Contains("Track 0", ex.Message);
        Assert.Contains("Property", ex.Message);
    }

    [Fact]
    public void Serialize_RejectsAnUnsupportedValueType_AsNotSupportedException_NotTargetInvocationException()
    {
        // Format<T> has no case for bool: NotSupportedException raised inside SerializeTrack<T> must unwrap to that same exception
        // type (matching what a direct call to SerializeTrack<T> would have thrown), not escape as TargetInvocationException. The
        // Property path itself only needs to be registered (Serialize does not cross-check the track's value type against the
        // target's own value type; that check is Deserialize's job), so a bool track on the (float-typed) Opacity path suffices.
        TimeSpan duration = TimeSpan.FromMilliseconds(100);
        UIStoryboard storyboard = new()
        {
            new UIKeyFrameAnimation<bool>(UIBuiltInAnimationTargets.Paths.Opacity)
            {
                Duration = duration,
                Track = { { 0f, false }, { 1f, true } },
            },
        };
        storyboard.Duration = duration;

        Assert.Throws<NotSupportedException>(() => UIKeyFrameClipSerializer.Serialize(storyboard));
    }

    [Fact]
    public void Serialize_RejectsAChildWithAnEmptyTrack_AsInvalidOperationException()
    {
        // U6 P3 ("Revue finale"), closed here (U8): Serialize used to accept a child with an empty Track and produce "frames": [], which
        // Deserialize then refused right back ("has no frames") -- SerializeTrack<T> now refuses it up front, naming the track.
        TimeSpan duration = TimeSpan.FromMilliseconds(100);
        UIStoryboard storyboard = new()
        {
            new UIKeyFrameAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { Duration = duration, Track = new UIKeyFrameTrack<float>() },
        };
        storyboard.Duration = duration;

        var ex = Assert.Throws<InvalidOperationException>(() => UIKeyFrameClipSerializer.Serialize(storyboard));
        Assert.Contains("Track 0", ex.Message);
        Assert.Contains("frames", ex.Message);
    }

    [Fact]
    public void Deserialized_TracksWithTheSamePath_StartInFileOrder_AndTheLastOneWins()
    {
        // Not refused: the engine's existing conflict rule (one active animation per owner and path) applies deterministically, since the
        // storyboard starts its children in Children order and the later one wins the conflict.
        AnimationTestScene scene = AnimationTestScene.Build();
        string json = """
        {
          "version": 1,
          "duration": "00:00:00.1000000",
          "tracks": [
            { "property": "Opacity", "valueType": "Single", "frames": [ { "offset": 0, "value": "0" }, { "offset": 1, "value": "1" } ] },
            { "property": "Opacity", "valueType": "Single", "frames": [ { "offset": 0, "value": "1" }, { "offset": 1, "value": "0" } ] }
          ]
        }
        """;

        UIStoryboard clip = UIKeyFrameClipSerializer.Deserialize(json);
        scene.Top.Animations.Start(clip);
        scene.Frames(10);

        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
        Assert.Equal(UIAnimationState.Cancelled, clip.Children[0].State);
        Assert.Equal(UIAnimationState.Completed, clip.Children[1].State);
    }

    [Fact]
    public void DeserializedClip_AllocatesNothingPerTick()
    {
        // Opacity (plain CLR write) and Margin (tagged pilot write of a struct) are the framework's known zero-allocation targets
        // (KeyFrameTests.Animation_AllocatesNothingPerTick, TransitionTests.RunningPilotTransition_AllocatesNothingPerTick_AfterWarmUp);
        // Background now joins them (ADR-0009, W5): the run's clone is created once when the track starts and mutated in place every
        // tick, so the allocation this comment used to document (a new MGSolidFillBrush per tick) no longer happens.
        AnimationTestScene scene = AnimationTestScene.Build();
        TimeSpan duration = TimeSpan.FromMilliseconds(100000);
        UIStoryboard source = new()
        {
            new UIKeyFrameAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { Duration = duration, Track = { { 0f, 0f }, { 1f, 1f } } },
            new UIKeyFrameAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale) { Duration = duration, Track = { { 0f, Vector2.Zero }, { 1f, Vector2.One } } },
            new UIKeyFrameAnimation<Thickness>(UIBuiltInAnimationTargets.Paths.Margin) { Duration = duration, Track = { { 0f, new Thickness(0) }, { 1f, new Thickness(10) } } },
            new UIKeyFrameAnimation<Color>(UIColorAnimationTargets.Paths.Background) { Duration = duration, Track = { { 0f, Color.Black }, { 1f, Color.White } } },
        };
        source.Duration = duration;
        string json = UIKeyFrameClipSerializer.Serialize(source);
        UIStoryboard clip = UIKeyFrameClipSerializer.Deserialize(json);
        scene.Top.Animations.Start(clip);

        UIAnimationManager manager = scene.Desktop.Animations;
        TimeSpan frame = TimeSpan.FromMilliseconds(16);
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
        Assert.Equal(UIAnimationState.Running, clip.State);
    }
}
