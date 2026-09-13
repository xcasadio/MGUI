using System.Globalization;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Interpolation;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Animation;

/// <summary>Slice U8 of Docs/Tasks/animation-v3-tasks.md: whole-tree serialisation (<see cref="UIAnimationSerializer"/>), the <see cref="UIAnimation.PresetOwner"/>
/// mechanism (4a) and the <see cref="UIAnimationPreview"/> preview-attach rollback (4c, exercised in <see cref="SeekTests"/>) it needed.</summary>
public class AnimationSerializerTests
{
    private const float Tolerance = 1e-4f;

    private static string MinimalPropertyJson()
    {
        UIPropertyAnimation<float> animation = new(UIBuiltInAnimationTargets.Paths.Opacity) { To = 1f, Duration = TimeSpan.FromMilliseconds(100) };
        return UIAnimationSerializer.Serialize(animation, _ => null);
    }

    // ---- Round trip and playback --------------------------------------------------------------------------------

    private static UIStoryboard BuildTree()
    {
        UIPropertyAnimation<float> fade = new(UIBuiltInAnimationTargets.Paths.Opacity)
        {
            From = 0f,
            To = 1f,
            Duration = TimeSpan.FromMilliseconds(300),
            Delay = TimeSpan.FromMilliseconds(50),
            RepeatCount = 2,
            AutoReverse = false,
            FillBehavior = UIAnimationFillBehavior.HoldEnd,
            CancelBehavior = UIAnimationCancelBehavior.KeepCurrent,
            Name = "fade",
            Easing = UIEasing.CubicOut,
        };

        UIDelayAnimation pause = new(TimeSpan.FromMilliseconds(80));

        UIKeyFrameAnimation<Vector2> scale = new(UIBuiltInAnimationTargets.Paths.RenderTransformScale)
        {
            Duration = TimeSpan.FromMilliseconds(400),
            Track =
            {
                { 0f, new Vector2(0.5f) },
                { 0.5f, new Vector2(1.2f), "cubic-bezier(0.25,0.1,0.25,1)" },
                { 1f, Vector2.One },
            },
        };

        UIPropertyAnimation<float> rotationOnSecondElement = new(UIBuiltInAnimationTargets.Paths.RenderTransformRotation)
        {
            From = 0f,
            To = 90f,
            Duration = TimeSpan.FromMilliseconds(150),
        };

        UIPropertyAnimation<Thickness> margin = new(UIBuiltInAnimationTargets.Paths.Margin)
        {
            From = new Thickness(0),
            To = new Thickness(8, 8, 8, 8),
            Duration = TimeSpan.FromMilliseconds(120),
        };

        UISequenceAnimation sequence = new UISequenceAnimation()
            .Append(rotationOnSecondElement)
            .AppendDelay(TimeSpan.FromMilliseconds(40))
            .Append(margin);

        return new UIStoryboard { fade, pause, scale, sequence };
    }

    [Fact]
    public void RoundTrip_StoryboardWithPropertyDelayKeyFramesAndNestedSequence_ProducesIdenticalJson_AndPlaysIdentically()
    {
        AnimationTestScene originalScene = AnimationTestScene.Build();
        UIStoryboard original = BuildTree();
        UIPropertyAnimation<float> originalRotation = (UIPropertyAnimation<float>)((UISequenceAnimation)original.Children[3]).Children[0];
        originalRotation.PresetOwner(originalScene.Bottom);

        string firstJson = UIAnimationSerializer.Serialize(original, e => ReferenceEquals(e, originalScene.Bottom) ? "bottom" : null);

        // A document has no memory of the scene it was produced from: a resolver that cannot name "bottom" refuses, proving Serialize did
        // emit an element name for the preset child (rather than silently dropping it).
        Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize(firstJson, _ => null));

        AnimationTestScene restoredScene = AnimationTestScene.Build();
        UIStoryboard restoredForPlayback = (UIStoryboard)UIAnimationSerializer.Deserialize(firstJson, name => name == "bottom" ? restoredScene.Bottom : null);
        string secondJson = UIAnimationSerializer.Serialize(restoredForPlayback, e => ReferenceEquals(e, restoredScene.Bottom) ? "bottom" : null);
        Assert.Equal(firstJson, secondJson);

        // Tree shape.
        Assert.Equal(4, restoredForPlayback.Children.Count);
        UIPropertyAnimation<float> restoredFade = (UIPropertyAnimation<float>)restoredForPlayback.Children[0];
        Assert.Equal(UIBuiltInAnimationTargets.Paths.Opacity, restoredFade.Property);
        Assert.Equal(0f, restoredFade.From);
        Assert.Equal(1f, restoredFade.To);
        Assert.Equal(TimeSpan.FromMilliseconds(300), restoredFade.Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(50), restoredFade.Delay);
        Assert.Equal(2, restoredFade.RepeatCount);
        Assert.False(restoredFade.AutoReverse);
        Assert.Equal(UIAnimationFillBehavior.HoldEnd, restoredFade.FillBehavior);
        Assert.Equal(UIAnimationCancelBehavior.KeepCurrent, restoredFade.CancelBehavior);
        Assert.Equal("fade", restoredFade.Name);
        Assert.Same(UIEasing.CubicOut, restoredFade.Easing);

        Assert.IsType<UIDelayAnimation>(restoredForPlayback.Children[1]);
        Assert.Equal(TimeSpan.FromMilliseconds(80), restoredForPlayback.Children[1].Duration);

        UIKeyFrameAnimation<Vector2> restoredScale = (UIKeyFrameAnimation<Vector2>)restoredForPlayback.Children[2];
        Assert.Equal(3, restoredScale.Track.Count);
        Assert.Equal("cubic-bezier(0.25,0.1,0.25,1)", restoredScale.Track.Frames[1].Easing);

        UISequenceAnimation restoredSequence = (UISequenceAnimation)restoredForPlayback.Children[3];
        Assert.Equal(3, restoredSequence.Children.Count);
        UIPropertyAnimation<float> restoredRotation = (UIPropertyAnimation<float>)restoredSequence.Children[0];
        Assert.Same(restoredScene.Bottom, restoredRotation.Owner);
        UIPropertyAnimation<Thickness> restoredMargin = (UIPropertyAnimation<Thickness>)restoredSequence.Children[2];
        Assert.Equal(new Thickness(0), restoredMargin.From);
        Assert.Equal(new Thickness(8, 8, 8, 8), restoredMargin.To);

        // Playback: the restored tree, started on the restored scene, matches the original tree started on the original scene, frame by frame.
        originalScene.Top.Animations.Start(original);
        restoredScene.Top.Animations.Start(restoredForPlayback);

        originalScene.Frames(10);
        restoredScene.Frames(10);
        Assert.Equal(originalScene.Top.Opacity, restoredScene.Top.Opacity, Tolerance);
        Assert.Equal(originalScene.Top.RenderTransform.Scale.X, restoredScene.Top.RenderTransform.Scale.X, Tolerance);
        Assert.Equal(originalScene.Bottom.RenderTransform.Rotation, restoredScene.Bottom.RenderTransform.Rotation, Tolerance);

        originalScene.Frames(60);
        restoredScene.Frames(60);
        Assert.Equal(1f, originalScene.Top.Opacity, Tolerance);
        Assert.Equal(originalScene.Top.Opacity, restoredScene.Top.Opacity, Tolerance);
        Assert.Equal(originalScene.Top.RenderTransform.Scale.X, restoredScene.Top.RenderTransform.Scale.X, Tolerance);
        Assert.Equal(originalScene.Top.Margin, restoredScene.Top.Margin);
        Assert.Equal(90f, originalScene.Bottom.RenderTransform.Rotation, Tolerance);
        Assert.Equal(originalScene.Bottom.RenderTransform.Rotation, restoredScene.Bottom.RenderTransform.Rotation, Tolerance);
    }

    [Fact]
    public void RoundTrip_ANestedComposite_StoryboardOfAStoryboard_AndSequenceOfSequences()
    {
        UIStoryboard innerStoryboard = new()
        {
            new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { To = 1f, Duration = TimeSpan.FromMilliseconds(50) },
        };
        UIStoryboard outerStoryboard = new() { innerStoryboard };

        string json = UIAnimationSerializer.Serialize(outerStoryboard, _ => null);
        UIStoryboard restoredOuter = (UIStoryboard)UIAnimationSerializer.Deserialize(json, _ => null);
        Assert.IsType<UIStoryboard>(restoredOuter.Children[0]);
        Assert.Equal(json, UIAnimationSerializer.Serialize(restoredOuter, _ => null));

        UISequenceAnimation innerSequence = new UISequenceAnimation()
            .Append(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { To = 1f, Duration = TimeSpan.FromMilliseconds(50) });
        UISequenceAnimation outerSequence = new UISequenceAnimation().Append(innerSequence);

        string sequenceJson = UIAnimationSerializer.Serialize(outerSequence, _ => null);
        UISequenceAnimation restoredOuterSequence = (UISequenceAnimation)UIAnimationSerializer.Deserialize(sequenceJson, _ => null);
        Assert.IsType<UISequenceAnimation>(restoredOuterSequence.Children[0]);
        Assert.Equal(sequenceJson, UIAnimationSerializer.Serialize(restoredOuterSequence, _ => null));
    }

    [Fact]
    public void RoundTrip_HandEditedDocument_ReorderedPropertiesWhitespaceAndOmittedOptionalFields_Canonicalizes()
    {
        string handWritten = """
        {
          "root":   {
            "duration":  "00:00:00.2000000",
            "to": "1",
            "path": "Opacity",
            "kind": "Property",
            "valueType": "Single"
          },
          "version": 1
        }
        """;

        UIAnimation clip = UIAnimationSerializer.Deserialize(handWritten, _ => null);
        string canonical = UIAnimationSerializer.Serialize(clip, _ => null);
        UIAnimation reparsed = UIAnimationSerializer.Deserialize(canonical, _ => null);
        string reserialized = UIAnimationSerializer.Serialize(reparsed, _ => null);

        Assert.Equal(canonical, reserialized);
        Assert.Equal(TimeSpan.FromMilliseconds(200), clip.Duration);
    }

    // ---- Serialize refusals --------------------------------------------------------------------------------------

    [Fact]
    public void Serialize_RejectsAnActiveAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> live = new(UIBuiltInAnimationTargets.Paths.Opacity) { To = 1f, Duration = TimeSpan.FromMilliseconds(100) };
        scene.Top.Animations.Start(live);

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Serialize(live, _ => null));
        Assert.Contains("root", ex.Message);
        Assert.Contains("active", ex.Message);
    }

    [Fact]
    public void Serialize_RejectsAnUnknownTarget_ListingKnownPaths()
    {
        UIPropertyAnimation<float> animation = new("Nope.Unregistered") { To = 1f, Duration = TimeSpan.FromMilliseconds(100) };

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Serialize(animation, _ => null));
        Assert.Contains("Nope.Unregistered", ex.Message);
        Assert.Contains(UIBuiltInAnimationTargets.Paths.Opacity, ex.Message);
    }

    private sealed class UnnamedEasing : IUIEasingFunction
    {
        public float Ease(float amount) => amount;
    }

    [Fact]
    public void Serialize_RejectsAnUnnamedApplicationEasing()
    {
        UIPropertyAnimation<float> animation = new(UIBuiltInAnimationTargets.Paths.Opacity)
        {
            To = 1f,
            Duration = TimeSpan.FromMilliseconds(100),
            Easing = new UnnamedEasing(),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Serialize(animation, _ => null));
        Assert.Contains(nameof(UIEasing.Register), ex.Message);
    }

    [Fact]
    public void Serialize_RejectsAChildOwnerWithoutAName()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> child = new(UIBuiltInAnimationTargets.Paths.Opacity) { To = 1f, Duration = TimeSpan.FromMilliseconds(100) };
        child.PresetOwner(scene.Bottom);
        UIStoryboard storyboard = new() { child };

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Serialize(storyboard, _ => null));
        Assert.Contains("nameOf", ex.Message);
    }

    [Fact]
    public void Serialize_RejectsAnEmptyKeyFrameTrack()
    {
        UIKeyFrameAnimation<float> animation = new(UIBuiltInAnimationTargets.Paths.Opacity) { Duration = TimeSpan.FromMilliseconds(100), Track = new UIKeyFrameTrack<float>() };

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Serialize(animation, _ => null));
        Assert.Contains("frames", ex.Message);
    }

    [Fact]
    public void Serialize_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => UIAnimationSerializer.Serialize(null, _ => null));
        Assert.Throws<ArgumentNullException>(() => UIAnimationSerializer.Serialize(new UIStoryboard(), null));
    }

    // ---- Deserialize refusals --------------------------------------------------------------------------------------

    [Fact]
    public void Deserialize_RejectsAnUnknownVersion()
    {
        string json = MinimalPropertyJson().Replace("\"version\": 1", "\"version\": 2");
        Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize(json, _ => null));
    }

    [Fact]
    public void Deserialize_RejectsAnUnknownKind_ListingKnownKinds()
    {
        string json = MinimalPropertyJson().Replace("\"Property\"", "\"Loop\"");

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize(json, _ => null));
        Assert.Contains("Loop", ex.Message);
        Assert.Contains("Storyboard", ex.Message);
    }

    [Fact]
    public void Deserialize_RejectsAnUnknownPath_ListingKnownPaths()
    {
        string json = MinimalPropertyJson().Replace("\"Opacity\"", "\"Nope\"");

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize(json, _ => null));
        Assert.Contains("Nope", ex.Message);
        Assert.Contains(UIBuiltInAnimationTargets.Paths.Opacity, ex.Message);
    }

    [Fact]
    public void Deserialize_RejectsAValueTypeMismatch()
    {
        string json = MinimalPropertyJson().Replace("\"valueType\": \"Single\"", "\"valueType\": \"Int32\"");

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize(json, _ => null));
        Assert.Contains("Int32", ex.Message);
        Assert.Contains("Single", ex.Message);
    }

    [Fact]
    public void Deserialize_RejectsAnUnknownElementName()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> child = new(UIBuiltInAnimationTargets.Paths.Opacity) { To = 1f, Duration = TimeSpan.FromMilliseconds(100) };
        child.PresetOwner(scene.Bottom);
        string json = UIAnimationSerializer.Serialize(child, _ => "bottom");

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize(json, _ => null));
        Assert.Contains("bottom", ex.Message);
    }

    [Fact]
    public void Deserialize_RejectsAnUnknownEasingName_ListingKnownEasings()
    {
        UIPropertyAnimation<float> animation = new(UIBuiltInAnimationTargets.Paths.Opacity) { To = 1f, Duration = TimeSpan.FromMilliseconds(100), Easing = UIEasing.CubicOut };
        string json = UIAnimationSerializer.Serialize(animation, _ => null).Replace("CubicOut", "NopeEasing");

        var ex = Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize(json, _ => null));
        Assert.Contains("NopeEasing", ex.Message);
        Assert.Contains(nameof(UIEasing.Linear), ex.Message);
    }

    [Fact]
    public void Deserialize_RejectsAnEmptyKeyFrameTrack()
    {
        string json = """{ "version": 1, "root": { "kind": "KeyFrames", "path": "Opacity", "valueType": "Single", "duration": "00:00:00.1000000", "track": [] } }""";

        Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize(json, _ => null));
    }

    [Fact]
    public void Deserialize_EmptyOrNull_Throws()
    {
        Assert.Throws<ArgumentException>(() => UIAnimationSerializer.Deserialize("", _ => null));
        Assert.Throws<ArgumentNullException>(() => UIAnimationSerializer.Deserialize(MinimalPropertyJson(), null));
        Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize("{}", _ => null));
        Assert.Throws<InvalidOperationException>(() => UIAnimationSerializer.Deserialize("""{ "version": 1 }""", _ => null));
    }

    [Fact]
    public void Deserialize_EmptyGroup_IsAcceptedAndCompletesOnItsFirstTick()
    {
        // Consistent with CompositionTests.EmptyGroup_CompletesOnItsFirstTick: an empty Storyboard/Sequence is a valid, if degenerate, tree.
        string json = """{ "version": 1, "root": { "kind": "Storyboard", "children": [] } }""";

        AnimationTestScene scene = AnimationTestScene.Build();
        UIAnimation empty = UIAnimationSerializer.Deserialize(json, _ => null);
        scene.Top.Animations.Start(empty);
        scene.Frames(1);

        Assert.Equal(UIAnimationState.Completed, empty.State);
    }

    // ---- Application value type extension point --------------------------------------------------------------------------------

    private readonly record struct SerializerCustomValue(float A, float B);

    private sealed class SerializerCustomValueInterpolator : IUIInterpolator<SerializerCustomValue>
    {
        public SerializerCustomValue Lerp(SerializerCustomValue from, SerializerCustomValue to, float amount)
            => new(from.A + (to.A - from.A) * amount, from.B + (to.B - from.B) * amount);
    }

    [Fact]
    public void ApplicationTarget_WithoutRegisteredFormat_IsRefusedExplicitly_ThenRoundTripsOnceRegistered()
    {
        const string path = "Test.AnimationSerializerCustomValue";
        UIInterpolators.Register(new SerializerCustomValueInterpolator());
        UIAnimationTargets.Register(new UIDelegateAnimationTarget<SerializerCustomValue>(path, _ => default, (_, _) => { }));

        UIPropertyAnimation<SerializerCustomValue> animation = new(path) { To = new SerializerCustomValue(1f, 2f), Duration = TimeSpan.FromMilliseconds(100) };

        var ex = Assert.Throws<NotSupportedException>(() => UIAnimationSerializer.Serialize(animation, _ => null));
        Assert.Contains(nameof(UIKeyFrameSerializer.RegisterValueFormat), ex.Message);

        UIKeyFrameSerializer.RegisterValueFormat<SerializerCustomValue>(
            v => $"{v.A.ToString(CultureInfo.InvariantCulture)},{v.B.ToString(CultureInfo.InvariantCulture)}",
            s =>
            {
                var parts = s.Split(',');
                return new SerializerCustomValue(float.Parse(parts[0], CultureInfo.InvariantCulture), float.Parse(parts[1], CultureInfo.InvariantCulture));
            });

        string json = UIAnimationSerializer.Serialize(animation, _ => null);
        UIPropertyAnimation<SerializerCustomValue> restored = (UIPropertyAnimation<SerializerCustomValue>)UIAnimationSerializer.Deserialize(json, _ => null);
        Assert.Equal(new SerializerCustomValue(1f, 2f), restored.To);
        Assert.Equal(json, UIAnimationSerializer.Serialize(restored, _ => null));
    }

    // ---- No control instance in the DTOs --------------------------------------------------------------------------------------

    [Fact]
    public void Dtos_CarryNoElementReference()
    {
        var dtoTypes = typeof(UIAnimationSerializer).GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)
            .Where(t => t.Name.EndsWith("Dto"));

        bool anyChecked = false;
        foreach (var dtoType in dtoTypes)
        {
            foreach (var property in dtoType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                anyChecked = true;
                var propertyType = property.PropertyType;
                var elementType = propertyType.IsGenericType ? propertyType.GetGenericArguments().FirstOrDefault() : propertyType;
                Assert.False(typeof(MGElement).IsAssignableFrom(propertyType), $"{dtoType.Name}.{property.Name}");
                if (elementType != null)
                {
                    Assert.False(typeof(MGElement).IsAssignableFrom(elementType), $"{dtoType.Name}.{property.Name}");
                }
            }
        }

        Assert.True(anyChecked);
    }
}
