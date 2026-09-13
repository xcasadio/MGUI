using System.Reflection;
using System.Text.Json;
using MGUI.Core.UI.Animation.Composition;

namespace MGUI.Core.UI.Animation.KeyFrames;

/// <summary>
/// JSON form of a multi-track keyframe clip (ADR-0008, decision 6): a <see cref="UIStoryboard"/> made only of <see cref="UIKeyFrameAnimation{T}"/>
/// children, one per animated path, played in parallel on one element. The clip's <see cref="UIKeyFrameClipDto.Duration"/> is authoritative:
/// every track must share it exactly (no delay, no repeat, no auto-reverse), unlike a hand-built <see cref="UIStoryboard"/> whose duration is
/// merely the longest child. Time remap of the composite (playing the clip faster or slower than it was authored) is explicitly out of scope.
/// <code>
/// {
///   "version": 1,
///   "duration": "00:00:01",
///   "tracks": [
///     { "property": "Opacity", "valueType": "Single", "frames": [ { "offset": 0, "value": "0" }, { "offset": 1, "value": "1" } ] },
///     { "property": "RenderTransform.Scale", "valueType": "Vector2", "frames": [ { "offset": 0, "value": "0.8,0.8" }, { "offset": 1, "value": "1,1" } ] },
///     { "property": "Background", "valueType": "Color", "frames": [ { "offset": 0, "value": "#00000000" }, { "offset": 1, "value": "#FF0000FF" } ] }
///   ]
/// }
/// </code>
/// The returned storyboard is unbound (it has no <see cref="UIAnimation.Owner"/> yet): the caller plays it on one element with
/// <c>element.Animations.Start(UIKeyFrameClipSerializer.Deserialize(json))</c>, exactly as the author's editor is expected to emit it (one
/// element per clip). <see cref="UIKeyFrameSerializer.Format{T}"/> / <see cref="UIKeyFrameSerializer.Parse{T}"/> and the JSON options are
/// shared with <see cref="UIKeyFrameSerializer"/>, so the two documents use the same value formatting and naming conventions.
/// </summary>
public static class UIKeyFrameClipSerializer
{
    public const int CurrentVersion = 1;

    /// <summary>Serializes <paramref name="storyboard"/> to JSON. Every child must be a <see cref="UIKeyFrameAnimation{T}"/> with no delay, no
    /// repeat, no auto-reverse, a <c>Property</c> path registered in <see cref="UIAnimationTargets"/>, and the same duration as the clip.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="storyboard"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The storyboard has no children, or a child does not meet the requirements above; the
    /// message names the offending child by index, path (when known) and reason.</exception>
    public static string Serialize(UIStoryboard storyboard)
    {
        if (storyboard == null)
        {
            throw new ArgumentNullException(nameof(storyboard));
        }

        var children = storyboard.Children;
        if (children.Count == 0)
        {
            throw new InvalidOperationException("A key frame clip needs at least one track.");
        }

        // The storyboard's own Duration is authoritative once it has been played (UIAnimationGroup.OnStarting recomputes it as the longest
        // child); an unstarted storyboard built for serialisation carries whatever the caller set (zero by default), so fall back to the
        // longest child in that case -- every child is required below to share this exact value anyway.
        var clipDuration = storyboard.Duration != TimeSpan.Zero ? storyboard.Duration : MaxDuration(children);

        List<UIKeyFrameClipTrackDto> tracks = new(children.Count);
        for (var i = 0; i < children.Count; i++)
        {
            tracks.Add(SerializeChild(children[i], i, clipDuration));
        }

        UIKeyFrameClipDto dto = new()
        {
            Version = CurrentVersion,
            Duration = clipDuration,
            Tracks = tracks,
        };
        return JsonSerializer.Serialize(dto, UIKeyFrameSerializer.JsonOptions);
    }

    /// <summary>Deserializes a clip: an unbound <see cref="UIStoryboard"/> of <see cref="UIKeyFrameAnimation{T}"/> children, in file order,
    /// each with <c>Duration</c> set to the clip's duration.</summary>
    /// <exception cref="ArgumentException"><paramref name="json"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">An unknown version, an unknown or mismatched track, or invalid frames.</exception>
    public static UIStoryboard Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Empty JSON.", nameof(json));
        }

        var dto = JsonSerializer.Deserialize<UIKeyFrameClipDto>(json, UIKeyFrameSerializer.JsonOptions)
                  ?? throw new InvalidOperationException("The JSON does not describe a key frame clip.");
        if (dto.Version != CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported key frame clip version {dto.Version} (supported: {CurrentVersion}).");
        }

        if (dto.Tracks == null || dto.Tracks.Count == 0)
        {
            throw new InvalidOperationException("A key frame clip needs at least one track.");
        }

        UIStoryboard storyboard = new() { Duration = dto.Duration };
        for (var i = 0; i < dto.Tracks.Count; i++)
        {
            storyboard.Add(DeserializeTrack(dto.Tracks[i], i, dto.Duration));
        }

        return storyboard;
    }

    private static TimeSpan MaxDuration(IReadOnlyList<UIAnimation> children)
    {
        var max = TimeSpan.Zero;
        for (var i = 0; i < children.Count; i++)
        {
            if (children[i].Duration > max)
            {
                max = children[i].Duration;
            }
        }

        return max;
    }

    private static UIKeyFrameClipTrackDto SerializeChild(UIAnimation child, int index, TimeSpan clipDuration)
    {
        var animationType = child.GetType();
        if (!animationType.IsGenericType || animationType.GetGenericTypeDefinition() != typeof(UIKeyFrameAnimation<>))
        {
            throw new InvalidOperationException(
                $"Track {index} ({animationType.Name}) is not a {typeof(UIKeyFrameAnimation<>).Name.Split('`')[0]}<T>: a key frame clip only serializes key frame tracks.");
        }

        if (child.Delay != TimeSpan.Zero)
        {
            throw new InvalidOperationException($"Track {index} has a non-zero Delay ({child.Delay}): a key frame clip track cannot be delayed.");
        }

        if (child.RepeatCount != 0 || child.RepeatForever)
        {
            throw new InvalidOperationException($"Track {index} repeats: a key frame clip track plays exactly once.");
        }

        if (child.AutoReverse)
        {
            throw new InvalidOperationException($"Track {index} has AutoReverse set: a key frame clip track does not reverse.");
        }

        if (child.Duration != clipDuration)
        {
            throw new InvalidOperationException(
                $"Track {index} has Duration {child.Duration}, but the clip duration is {clipDuration}: every track must share the clip's duration.");
        }

        var valueType = animationType.GetGenericArguments()[0];
        var method = typeof(UIKeyFrameClipSerializer).GetMethod(nameof(SerializeTrack), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(valueType);
        try
        {
            return (UIKeyFrameClipTrackDto)method.Invoke(null, new object[] { child, index })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Unwrap the reflection wrapper so the caller sees the real exception type and message (InvalidOperationException from an
            // unknown/blank Property path or missing Track, NotSupportedException from Format<T>), matching what a direct call would have
            // thrown and what DeserializeTrack already does below.
            throw ex.InnerException;
        }
    }

    private static UIKeyFrameClipTrackDto SerializeTrack<T>(UIKeyFrameAnimation<T> animation, int index)
    {
        if (string.IsNullOrWhiteSpace(animation.Property))
        {
            throw new InvalidOperationException($"Track {index} has no Property path.");
        }

        if (!UIAnimationTargets.IsRegistered(animation.Property))
        {
            throw new InvalidOperationException(
                $"Track {index} references unknown animation target '{animation.Property}'. Registered paths: {string.Join(", ", UIAnimationTargets.Paths)}.");
        }

        var track = animation.Track ?? throw new InvalidOperationException($"Track {index} ('{animation.Property}') has no {nameof(UIKeyFrameAnimation<T>.Track)}.");
        if (track.Count == 0)
        {
            // U6 P3 (Docs/Tasks/animation-v3-tasks.md, "Revue finale"), closed here (U8): an empty track used to serialize as "frames": [],
            // which Deserialize then refused ("has no frames") -- refuse it here instead, at the point that actually knows the offending
            // track's index and path, matching the wording style of every other refusal in this method.
            throw new InvalidOperationException($"Track {index} ('{animation.Property}') has no frames.");
        }

        return new UIKeyFrameClipTrackDto
        {
            Property = animation.Property,
            ValueType = typeof(T).Name,
            Frames = track.Frames.Select(x => new UIKeyFrameSerializer.UIKeyFrameDto { Offset = x.Offset, Value = UIKeyFrameSerializer.Format(x.Value), Easing = x.Easing }).ToList(),
        };
    }

    private static UIAnimation DeserializeTrack(UIKeyFrameClipTrackDto trackDto, int index, TimeSpan clipDuration)
    {
        if (trackDto == null || string.IsNullOrWhiteSpace(trackDto.Property))
        {
            throw new InvalidOperationException($"Track {index} has no property path.");
        }

        if (!UIAnimationTargets.IsRegistered(trackDto.Property))
        {
            throw new InvalidOperationException(
                $"Track {index} references unknown animation target '{trackDto.Property}'. Registered paths: {string.Join(", ", UIAnimationTargets.Paths)}.");
        }

        var valueType = UIAnimationTargets.GetValueType(trackDto.Property);
        if (!UIKeyFrameSerializer.IsSupported(valueType))
        {
            throw new InvalidOperationException(
                $"Track {index} ('{trackDto.Property}') has value type '{valueType!.Name}', which is not supported by {nameof(UIKeyFrameSerializer)}.");
        }

        if (!string.Equals(trackDto.ValueType, valueType.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Track {index} ('{trackDto.Property}') holds values of type '{trackDto.ValueType}', but the target expects '{valueType.Name}'.");
        }

        if (trackDto.Frames == null || trackDto.Frames.Count == 0)
        {
            throw new InvalidOperationException($"Track {index} ('{trackDto.Property}') has no frames.");
        }

        var method = typeof(UIKeyFrameClipSerializer).GetMethod(nameof(BuildChild), BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(valueType);
        try
        {
            return (UIAnimation)method.Invoke(null, new object[] { trackDto, index, clipDuration })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Unwrap the reflection wrapper so the caller sees the real exception type and message (FormatException from a malformed
            // value, InvalidOperationException from Validate()), matching what a direct call would have thrown.
            throw ex.InnerException;
        }
    }

    private static UIAnimation BuildChild<T>(UIKeyFrameClipTrackDto trackDto, int index, TimeSpan clipDuration)
    {
        UIKeyFrameTrack<T> track = new();
        foreach (var frame in trackDto.Frames)
        {
            T value;
            try
            {
                value = UIKeyFrameSerializer.Parse<T>(frame.Value);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw new InvalidOperationException($"Track {index} ('{trackDto.Property}') has an invalid frame value '{frame.Value}': {ex.Message}", ex);
            }

            track.Add(new UIKeyFrame<T>(frame.Offset, value, frame.Easing));
        }

        try
        {
            track.Validate();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"Track {index} ('{trackDto.Property}') is invalid: {ex.Message}", ex);
        }

        return new UIKeyFrameAnimation<T>(trackDto.Property)
        {
            Track = track,
            Duration = clipDuration,
        };
    }

    /// <summary>The JSON document (version 1).</summary>
    public sealed class UIKeyFrameClipDto
    {
        public int Version { get; set; }
        public TimeSpan Duration { get; set; }
        public List<UIKeyFrameClipTrackDto> Tracks { get; set; }
    }

    /// <summary>One track of the JSON document.</summary>
    public sealed class UIKeyFrameClipTrackDto
    {
        public string Property { get; set; }
        public string ValueType { get; set; }
        public List<UIKeyFrameSerializer.UIKeyFrameDto> Frames { get; set; }
    }
}
