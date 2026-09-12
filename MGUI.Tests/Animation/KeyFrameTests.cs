using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Animation;

/// <summary>Slice T2 of Docs/Tasks/animation-v2-tasks.md: keyframe tracks, keyframe animations and their JSON model.</summary>
public class KeyFrameTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void Track_KeepsKeysSorted_AndReplacesAnExistingOffset()
    {
        UIKeyFrameTrack<float> track = new();
        track.Add(1f, 10f).Add(0f, 0f).Add(0.5f, 5f).Add(0.5f, 6f, "QuadOut");

        Assert.Equal(new[] { 0f, 0.5f, 1f }, track.Frames.Select(x => x.Offset));
        Assert.Equal(6f, track.Frames[1].Value);
        Assert.Same(UIEasing.QuadOut, track.Frames[1].ResolveEasing());
        Assert.Same(UIEasing.Linear, track.Frames[0].ResolveEasing());
        Assert.True(track.StartsAtZero);
        Assert.Throws<ArgumentOutOfRangeException>(() => track.Add(1.5f, 0f));
    }

    [Fact]
    public void Track_Validate_RejectsEmptyAndUnfinishedTracks()
    {
        Assert.Throws<InvalidOperationException>(() => new UIKeyFrameTrack<float>().Validate());
        Assert.Throws<InvalidOperationException>(() => new UIKeyFrameTrack<float> { { 0f, 0f }, { 0.5f, 1f } }.Validate());
        new UIKeyFrameTrack<float> { { 1f, 1f } }.Validate();
    }

    [Theory]
    [InlineData(0f, -1, 0, 1f)]
    [InlineData(0.25f, 0, 1, 0.5f)]
    [InlineData(0.5f, 0, 1, 1f)]
    [InlineData(0.75f, 1, 2, 0.5f)]
    [InlineData(1f, 1, 2, 1f)]
    public void Track_FindSegment(float progress, int expectedFrom, int expectedTo, float expectedLocal)
    {
        UIKeyFrameTrack<float> track = new() { { 0f, 0f }, { 0.5f, 1f }, { 1f, 0f } };

        track.FindSegment(progress, out int from, out int to, out float local);

        Assert.Equal(expectedFrom, from);
        Assert.Equal(expectedTo, to);
        Assert.Equal(expectedLocal, local, Tolerance);
    }

    [Fact]
    public void Animation_FollowsTheKeys_UpThenDown()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIKeyFrameAnimation<float> blink = new(UIBuiltInAnimationTargets.Paths.Opacity)
        {
            Duration = TimeSpan.FromMilliseconds(320),
            Track = { { 0f, 0f }, { 0.5f, 1f }, { 1f, 0f } },
        };

        scene.Top.Animations.Start(blink);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(0.5f, scene.Top.Opacity, Tolerance);
        scene.Frames(5);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);
        Assert.Equal(UIAnimationState.Completed, blink.State);
    }

    [Fact]
    public void Animation_AppliesEachKeysEasing_ToItsSegment()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIKeyFrameAnimation<Vector2> pop = new(UIBuiltInAnimationTargets.Paths.RenderTransformScale)
        {
            Duration = TimeSpan.FromMilliseconds(160),
            Easing = UIEasing.BounceOut,
            Track = { { 0f, new Vector2(0.8f) }, { 0.6f, new Vector2(1.1f), "QuadIn" }, { 1f, new Vector2(1f) } },
        };

        scene.Top.Animations.Start(pop);
        scene.Frames(3);

        // 48 ms of 160 = 0.3 = half of the first segment, QuadIn(0.5) = 0.25 -> 0.8 + 0.3 * 0.25.
        Assert.Equal(0.875f, scene.Top.RenderTransform.Scale.X, Tolerance);
        scene.Frames(7);
        Assert.Equal(1f, scene.Top.RenderTransform.Scale.X, Tolerance);
        Assert.Equal(UIAnimationState.Completed, pop.State);
    }

    [Fact]
    public void Animation_WithoutAKeyAtZero_StartsFromTheCurrentValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Opacity = 0.4f;
        UIKeyFrameAnimation<float> fade = new(UIBuiltInAnimationTargets.Paths.Opacity)
        {
            Duration = TimeSpan.FromMilliseconds(160),
            Track = { { 1f, 1f } },
        };

        scene.Top.Animations.Start(fade);

        Assert.False(fade.HasFrom);
        Assert.Equal(0.4f, fade.StartValue, Tolerance);
        scene.Frames(5);
        Assert.Equal(0.7f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void Animation_RejectsAnInvalidTrack_AtStart()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIKeyFrameAnimation<float> broken = new(UIBuiltInAnimationTargets.Paths.Opacity) { Duration = TimeSpan.FromMilliseconds(16), Track = { { 0.5f, 1f } } };

        Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(broken));
        Assert.Equal(UIAnimationState.Stopped, broken.State);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void Animation_RestoreAndRepeat_UseTheEngine()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIKeyFrameAnimation<float> pulse = new(UIBuiltInAnimationTargets.Paths.RenderScale)
        {
            Duration = TimeSpan.FromMilliseconds(64),
            RepeatCount = 1,
            FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
            Track = { { 0f, 1f }, { 0.5f, 1.2f }, { 1f, 1f } },
        };

        scene.Top.Animations.Start(pulse);
        scene.Frames(2);
        Assert.True(scene.Top.TryGetEffectiveStateScale(out float peak));
        Assert.Equal(1.2f, peak, Tolerance);
        scene.Frames(6);

        Assert.Equal(UIAnimationState.Completed, pulse.State);
        Assert.False(scene.Top.TryGetEffectiveStateScale(out _));
    }

    [Fact]
    public void Serializer_RoundTrips_EveryBuiltInValueType()
    {
        AssertRoundTrip(new UIKeyFrameTrack<float> { { 0f, 0f }, { 0.5f, 1.25f, "CubicOut" }, { 1f, 0f } });
        AssertRoundTrip(new UIKeyFrameTrack<int> { { 0f, 3 }, { 1f, 42 } });
        AssertRoundTrip(new UIKeyFrameTrack<Vector2> { { 0f, new Vector2(0.8f, 0.9f) }, { 1f, Vector2.One, "BackOut" } });
        AssertRoundTrip(new UIKeyFrameTrack<Color> { { 0f, new Color(10, 20, 30, 40) }, { 1f, Color.CornflowerBlue } });
        AssertRoundTrip(new UIKeyFrameTrack<Thickness> { { 0f, new Thickness(1, 2, 3, 4) }, { 1f, new Thickness(0) } });
    }

    [Fact]
    public void Serializer_Json_IsVersionedAndReadable()
    {
        string json = UIKeyFrameSerializer.Serialize(new UIKeyFrameTrack<Color> { { 0f, new Color(255, 0, 0, 255) }, { 1f, new Color(0, 0, 255, 128), "QuadOut" } });

        Assert.Contains("\"version\": 1", json);
        Assert.Contains("\"valueType\": \"Color\"", json);
        Assert.Contains("\"#FF0000FF\"", json);
        Assert.Contains("\"#0000FF80\"", json);
        Assert.Contains("\"easing\": \"QuadOut\"", json);
        Assert.Equal("Color", UIKeyFrameSerializer.ReadValueType(json));
    }

    [Fact]
    public void Serializer_RejectsUnknownVersion_TypeMismatch_AndUnsupportedTypes()
    {
        string json = UIKeyFrameSerializer.Serialize(new UIKeyFrameTrack<float> { { 1f, 1f } });

        Assert.Throws<InvalidOperationException>(() => UIKeyFrameSerializer.Deserialize<float>(json.Replace("\"version\": 1", "\"version\": 2")));
        Assert.Throws<InvalidOperationException>(() => UIKeyFrameSerializer.Deserialize<int>(json));
        Assert.Throws<NotSupportedException>(() => UIKeyFrameSerializer.Serialize(new UIKeyFrameTrack<decimal> { { 1f, 1m } }));
        Assert.Throws<ArgumentException>(() => UIKeyFrameSerializer.Deserialize<float>(" "));
    }

    [Fact]
    public void Animation_AllocatesNothingPerTick()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIKeyFrameAnimation<float> loop = new(UIBuiltInAnimationTargets.Paths.Opacity)
        {
            Duration = TimeSpan.FromMilliseconds(100000),
            RepeatForever = true,
            Track = { { 0f, 0f }, { 0.3f, 1f, "CubicOut" }, { 1f, 0f, "SineIn" } },
        };
        scene.Top.Animations.Start(loop);
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
    }

    private static void AssertRoundTrip<T>(UIKeyFrameTrack<T> track)
    {
        string json = UIKeyFrameSerializer.Serialize(track);
        UIKeyFrameTrack<T> restored = UIKeyFrameSerializer.Deserialize<T>(json);

        Assert.Equal(track.Count, restored.Count);
        for (int i = 0; i < track.Count; i++)
        {
            Assert.Equal(track.Frames[i].Offset, restored.Frames[i].Offset);
            Assert.Equal(track.Frames[i].Value, restored.Frames[i].Value);
            Assert.Equal(track.Frames[i].Easing, restored.Frames[i].Easing);
        }
    }
}
