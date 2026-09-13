using System.Reflection;
using System.Text.Json;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;

namespace MGUI.Core.UI.Animation.KeyFrames;

/// <summary>
/// JSON form of a whole animation tree -- a <see cref="UIStoryboard"/>, a <see cref="UISequenceAnimation"/>, a <see cref="UIDelayAnimation"/>,
/// a <see cref="UIPropertyAnimation{T}"/> or a <see cref="UIKeyFrameAnimation{T}"/>, nested to any depth -- with no reference to any control
/// instance (ADR-0008, decision 8; precedent <see cref="MGUI.Core.UI.Graph.GraphSerializer"/>). <see cref="Serialize"/> walks the tree with a
/// host-provided <c>nameOf</c> delegate for the (rare) child preset on another element than the root; <see cref="Deserialize"/> walks the
/// document back with a host-provided <c>resolveElement</c> delegate, using <see cref="UIAnimation.PresetOwner"/> (U8) for such a child.
/// <code>
/// {
///   "version": 1,
///   "root": {
///     "kind": "Storyboard",
///     "children": [
///       { "kind": "Property", "path": "Opacity", "valueType": "Single", "duration": "00:00:00.3", "to": "1", "easing": "CubicOut" },
///       { "kind": "Delay", "duration": "00:00:00.1" },
///       {
///         "kind": "KeyFrames", "path": "RenderTransform.Scale", "valueType": "Vector2", "duration": "00:00:00.4",
///         "track": [ { "offset": 0, "value": "0.8,0.8" }, { "offset": 1, "value": "1,1" } ]
///       },
///       {
///         "kind": "Sequence", "element": "popup",
///         "children": [
///           { "kind": "Property", "path": "Opacity", "valueType": "Single", "duration": "00:00:00.2", "to": "0" }
///         ]
///       }
///     ]
///   }
/// }
/// </code>
/// <see cref="UIKeyFrameSerializer.Format{T}"/> / <see cref="UIKeyFrameSerializer.Parse{T}"/> (and its <see cref="UIKeyFrameSerializer.RegisterValueFormat{T}"/>
/// extension point) and the JSON options are shared with <see cref="UIKeyFrameSerializer"/> and <see cref="UIKeyFrameClipSerializer"/>, so
/// every document in the framework uses the same value formatting, naming and versioning conventions. A clip (<see cref="UIKeyFrameClipSerializer"/>)
/// is the single-element, keyframes-only subset of what this class can express: a <see cref="UIStoryboard"/> whose children are all
/// <see cref="UIKeyFrameAnimation{T}"/>, none preset on another element.<para/>
/// The document returned by <see cref="Deserialize"/> is unbound (no <see cref="UIAnimation.Owner"/> on the root, unless the root's own
/// <c>element</c> was set): play it with <c>element.Animations.Start(root)</c>, or with <c>root.Play()</c> when the root's <c>element</c>
/// was resolved and preset.
/// </summary>
public static class UIAnimationSerializer
{
    public const int CurrentVersion = 1;

    /// <summary>Serializes <paramref name="animation"/> and every descendant to JSON.</summary>
    /// <param name="animation">The root of the tree. Must be <see cref="UIAnimationState.Stopped"/>, like every descendant.</param>
    /// <param name="nameOf">Names an element referenced by a node preset on another element than the root (<see cref="UIAnimation.Owner"/>
    /// set through <see cref="UIAnimation.PresetOwner"/> or the Start-then-Cancel idiom); never called for a node with no preset owner.</param>
    /// <exception cref="ArgumentNullException"><paramref name="animation"/> or <paramref name="nameOf"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A node (the root or a descendant) is active (<see cref="UIAnimation.State"/> other than
    /// <see cref="UIAnimationState.Stopped"/>); is not a <see cref="UIStoryboard"/>, <see cref="UISequenceAnimation"/>,
    /// <see cref="UIDelayAnimation"/>, <see cref="UIPropertyAnimation{T}"/> or <see cref="UIKeyFrameAnimation{T}"/>; is a property or
    /// key-frame node whose <c>Property</c> is blank or not registered in <see cref="UIAnimationTargets"/>; is a key-frame node with an
    /// empty <see cref="UIKeyFrameAnimation{T}.Track"/>; carries a value type without a <see cref="UIKeyFrameSerializer.Format{T}"/>
    /// (built-in or <see cref="UIKeyFrameSerializer.RegisterValueFormat{T}"/>-registered); carries an <see cref="IUIEasingFunction"/> with
    /// no resolvable name (not a registered <see cref="UIEasing"/> name, not a <see cref="UICubicBezierEasing"/>); or has a preset owner
    /// for which <paramref name="nameOf"/> returned null or an empty string. The message names the offending node by its path in the tree
    /// (<c>root</c>, <c>root/0</c>, <c>root/0/1</c>, ...) and the reason.</exception>
    public static string Serialize(UIAnimation animation, Func<MGElement, string> nameOf)
    {
        if (animation == null)
        {
            throw new ArgumentNullException(nameof(animation));
        }

        if (nameOf == null)
        {
            throw new ArgumentNullException(nameof(nameOf));
        }

        UIAnimationDocumentDto dto = new()
        {
            Version = CurrentVersion,
            Root = SerializeNode(animation, nameOf, "root"),
        };
        return JsonSerializer.Serialize(dto, UIKeyFrameSerializer.JsonOptions);
    }

    /// <summary>Deserializes a document: the root of the tree it describes, unbound unless its own <c>element</c> resolves (then preset
    /// with <see cref="UIAnimation.PresetOwner"/>, so <c>root.Play()</c> works without the caller naming the element again).</summary>
    /// <param name="json">The document.</param>
    /// <param name="resolveElement">Resolves an element by the name a node's <c>element</c> field carries; called for every node whose
    /// <c>element</c> is set (the root included).</param>
    /// <exception cref="ArgumentException"><paramref name="json"/> is empty.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="resolveElement"/> is null.</exception>
    /// <exception cref="InvalidOperationException">An unknown version; a node with no or an unknown <c>kind</c> (the message lists the
    /// known kinds); a property or key-frame node with an unknown <c>path</c> (the message lists the known paths, sorted); a
    /// <c>valueType</c> mismatched with the target's own value type, or unsupported by <see cref="UIKeyFrameSerializer"/> (the message
    /// names <see cref="UIKeyFrameSerializer.RegisterValueFormat{T}"/>); a key-frame node with no or empty <c>track</c>, or an invalid
    /// track (<see cref="UIKeyFrameTrack{T}.Validate"/>); an invalid frame value; an <c>element</c> name <paramref name="resolveElement"/>
    /// cannot resolve; an <c>easing</c> name <see cref="UIEasing.TryGet"/> cannot resolve (the message lists <see cref="UIEasing.Names"/>).
    /// The message names the offending node by its path in the tree.</exception>
    public static UIAnimation Deserialize(string json, Func<string, MGElement> resolveElement)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException("Empty JSON.", nameof(json));
        }

        if (resolveElement == null)
        {
            throw new ArgumentNullException(nameof(resolveElement));
        }

        var dto = JsonSerializer.Deserialize<UIAnimationDocumentDto>(json, UIKeyFrameSerializer.JsonOptions)
                  ?? throw new InvalidOperationException("The JSON does not describe an animation.");
        if (dto.Version != CurrentVersion)
        {
            throw new InvalidOperationException($"Unsupported animation document version {dto.Version} (supported: {CurrentVersion}).");
        }

        if (dto.Root == null)
        {
            throw new InvalidOperationException("The animation document has no root node.");
        }

        return DeserializeNode(dto.Root, resolveElement, "root");
    }

    // ---- Serialize ----------------------------------------------------------------------------------------------

    private static UIAnimationNodeDto SerializeNode(UIAnimation node, Func<MGElement, string> nameOf, string path)
    {
        if (node.State != UIAnimationState.Stopped)
        {
            throw new InvalidOperationException(
                $"{path}: {node.GetType().Name} is active (state {node.State}): serialize only a stopped, unbound tree.");
        }

        UIAnimationNodeDto dto = new()
        {
            Delay = node.Delay,
            RepeatCount = node.RepeatCount,
            RepeatForever = node.RepeatForever,
            AutoReverse = node.AutoReverse,
            FillBehavior = node.FillBehavior.ToString(),
            CancelBehavior = node.CancelBehavior.ToString(),
            Name = node.Name,
        };

        if (node.Owner != null)
        {
            var name = nameOf(node.Owner);
            if (string.IsNullOrEmpty(name))
            {
                throw new InvalidOperationException(
                    $"{path}: {node.GetType().Name} is preset on a {node.Owner.GetType().Name}, but nameOf returned no name for it.");
            }

            dto.Element = name;
        }

        switch (node)
        {
            case UIStoryboard storyboard:
                dto.Kind = "Storyboard";
                dto.Children = SerializeChildren(storyboard.Children, nameOf, path);
                break;
            case UISequenceAnimation sequence:
                dto.Kind = "Sequence";
                dto.Children = SerializeChildren(sequence.Children, nameOf, path);
                break;
            case UIDelayAnimation:
                dto.Kind = "Delay";
                dto.Duration = node.Duration;
                break;
            default:
                SerializeLeaf(node, dto, path);
                dto.Duration = node.Duration;
                break;
        }

        return dto;
    }

    private static List<UIAnimationNodeDto> SerializeChildren(IReadOnlyList<UIAnimation> children, Func<MGElement, string> nameOf, string path)
    {
        List<UIAnimationNodeDto> result = new(children.Count);
        for (var i = 0; i < children.Count; i++)
        {
            result.Add(SerializeNode(children[i], nameOf, $"{path}/{i}"));
        }

        return result;
    }

    private static void SerializeLeaf(UIAnimation node, UIAnimationNodeDto dto, string path)
    {
        var animationType = node.GetType();
        if (animationType.IsGenericType && animationType.GetGenericTypeDefinition() == typeof(UIKeyFrameAnimation<>))
        {
            dto.Kind = "KeyFrames";
            InvokeGeneric(nameof(SerializeKeyFramesNode), animationType.GetGenericArguments()[0], node, dto, path);
            return;
        }

        if (animationType.IsGenericType && animationType.GetGenericTypeDefinition() == typeof(UIPropertyAnimation<>))
        {
            dto.Kind = "Property";
            InvokeGeneric(nameof(SerializePropertyNode), animationType.GetGenericArguments()[0], node, dto, path);
            return;
        }

        throw new InvalidOperationException(
            $"{path}: {animationType.Name} is not a serializable node kind (Storyboard, Sequence, Delay, Property or KeyFrames).");
    }

    private static void InvokeGeneric(string methodName, Type valueType, UIAnimation node, UIAnimationNodeDto dto, string path)
    {
        var method = typeof(UIAnimationSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(valueType);
        try
        {
            method.Invoke(null, new object[] { node, dto, path });
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            // Unwrap the reflection wrapper, matching UIKeyFrameClipSerializer's own convention: the caller sees the exact exception a
            // direct (non-reflected) call would have thrown.
            throw ex.InnerException;
        }
    }

    private static void SerializePropertyNode<T>(UIAnimation node, UIAnimationNodeDto dto, string path)
    {
        var animation = (UIPropertyAnimation<T>)node;
        var property = RequireSerializedPath(animation.Property, path);

        dto.Path = property;
        dto.ValueType = typeof(T).Name;
        dto.From = animation.HasFrom ? UIKeyFrameSerializer.Format(animation.From) : null;
        dto.To = UIKeyFrameSerializer.Format(animation.To);
        dto.Easing = FormatEasing(animation.Easing, path);
    }

    private static void SerializeKeyFramesNode<T>(UIAnimation node, UIAnimationNodeDto dto, string path)
    {
        var animation = (UIKeyFrameAnimation<T>)node;
        var property = RequireSerializedPath(animation.Property, path);

        var track = animation.Track ?? throw new InvalidOperationException($"{path}: has no {nameof(UIKeyFrameAnimation<T>.Track)}.");
        if (track.Count == 0)
        {
            // Same U6 P3 the clip serializer closes (UIKeyFrameClipSerializer.SerializeTrack): an empty track would otherwise serialize as
            // "track": [], which Deserialize refuses right back ("has no frames").
            throw new InvalidOperationException($"{path} ('{property}'): has no frames.");
        }

        dto.Path = property;
        dto.ValueType = typeof(T).Name;
        dto.Track = track.Frames.Select(x => new UIKeyFrameSerializer.UIKeyFrameDto { Offset = x.Offset, Value = UIKeyFrameSerializer.Format(x.Value), Easing = x.Easing }).ToList();
    }

    private static string RequireSerializedPath(string property, string path)
    {
        if (string.IsNullOrWhiteSpace(property))
        {
            throw new InvalidOperationException($"{path}: has no Property path (an explicit Target cannot be serialized).");
        }

        if (!UIAnimationTargets.IsRegistered(property))
        {
            throw new InvalidOperationException(
                $"{path}: references unknown animation target '{property}'. Registered paths: {string.Join(", ", UIAnimationTargets.Paths)}.");
        }

        return property;
    }

    /// <summary>Renders <paramref name="easing"/> as the text <see cref="ResolveEasing"/> can read back: null for the default (linear), the
    /// canonical CSS literal for a <see cref="UICubicBezierEasing"/>, or the name it resolves to in <see cref="UIEasing"/> for anything
    /// else -- refused when none of those hold (an application-defined <see cref="IUIEasingFunction"/> never registered by name).</summary>
    private static string FormatEasing(IUIEasingFunction easing, string path)
    {
        if (easing == null)
        {
            return null;
        }

        if (easing is UICubicBezierEasing bezier)
        {
            return bezier.ToString();
        }

        var name = easing.ToString();
        if (!string.IsNullOrEmpty(name) && UIEasing.TryGet(name, out var resolved) && ReferenceEquals(resolved, easing))
        {
            return name;
        }

        throw new InvalidOperationException(
            $"{path}: the easing '{easing.GetType().Name}' has no resolvable name: register it with {nameof(UIEasing)}.{nameof(UIEasing.Register)}(...) " +
            $"or use a {nameof(UICubicBezierEasing)} instead.");
    }

    // ---- Deserialize ----------------------------------------------------------------------------------------------

    private static UIAnimation DeserializeNode(UIAnimationNodeDto dto, Func<string, MGElement> resolveElement, string path)
    {
        UIAnimation node = dto.Kind switch
        {
            "Storyboard" => DeserializeGroup(new UIStoryboard(), dto, resolveElement, path),
            "Sequence" => DeserializeGroup(new UISequenceAnimation(), dto, resolveElement, path),
            "Delay" => new UIDelayAnimation(),
            "Property" => DeserializeProperty(dto, path),
            "KeyFrames" => DeserializeKeyFrames(dto, path),
            _ => throw new InvalidOperationException($"{path}: unknown node kind '{dto.Kind}'. Known kinds: Storyboard, Sequence, Delay, Property, KeyFrames."),
        };

        ApplyCommon(node, dto);

        if (!string.IsNullOrEmpty(dto.Element))
        {
            var element = resolveElement(dto.Element)
                ?? throw new InvalidOperationException($"{path}: cannot resolve element '{dto.Element}'.");
            node.PresetOwner(element);
        }

        return node;
    }

    private static UIAnimation DeserializeGroup(UIAnimationGroup group, UIAnimationNodeDto dto, Func<string, MGElement> resolveElement, string path)
    {
        var children = dto.Children ?? new List<UIAnimationNodeDto>();
        for (var i = 0; i < children.Count; i++)
        {
            group.Add(DeserializeNode(children[i], resolveElement, $"{path}/{i}"));
        }

        return group;
    }

    private static UIAnimation DeserializeProperty(UIAnimationNodeDto dto, string path)
    {
        var valueType = RequireDeserializedValueType(dto, path);
        return InvokeGenericBuild(nameof(BuildProperty), valueType, dto, path);
    }

    private static UIAnimation DeserializeKeyFrames(UIAnimationNodeDto dto, string path)
    {
        var valueType = RequireDeserializedValueType(dto, path);
        if (dto.Track == null || dto.Track.Count == 0)
        {
            throw new InvalidOperationException($"{path} ('{dto.Path}'): has no frames.");
        }

        return InvokeGenericBuild(nameof(BuildKeyFrames), valueType, dto, path);
    }

    private static Type RequireDeserializedValueType(UIAnimationNodeDto dto, string path)
    {
        if (string.IsNullOrWhiteSpace(dto.Path))
        {
            throw new InvalidOperationException($"{path}: has no Path.");
        }

        if (!UIAnimationTargets.IsRegistered(dto.Path))
        {
            throw new InvalidOperationException(
                $"{path}: references unknown animation target '{dto.Path}'. Registered paths: {string.Join(", ", UIAnimationTargets.Paths)}.");
        }

        var valueType = UIAnimationTargets.GetValueType(dto.Path);
        if (!string.Equals(dto.ValueType, valueType!.Name, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{path} ('{dto.Path}'): holds values of type '{dto.ValueType}', but the target expects '{valueType.Name}'.");
        }

        if (!UIKeyFrameSerializer.IsSupported(valueType))
        {
            throw new InvalidOperationException(
                $"{path} ('{dto.Path}'): value type '{valueType.Name}' is not supported by {nameof(UIKeyFrameSerializer)}. Register one with " +
                $"{nameof(UIKeyFrameSerializer)}.{nameof(UIKeyFrameSerializer.RegisterValueFormat)}<{valueType.Name}>(...).");
        }

        return valueType;
    }

    private static UIAnimation InvokeGenericBuild(string methodName, Type valueType, UIAnimationNodeDto dto, string path)
    {
        var method = typeof(UIAnimationSerializer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(valueType);
        try
        {
            return (UIAnimation)method.Invoke(null, new object[] { dto, path })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            throw ex.InnerException;
        }
    }

    private static UIAnimation BuildProperty<T>(UIAnimationNodeDto dto, string path)
    {
        UIPropertyAnimation<T> animation = new(dto.Path);
        if (dto.From != null)
        {
            animation.From = ParseValue<T>(dto.From, path);
        }

        animation.To = ParseValue<T>(dto.To, path);
        animation.Easing = ResolveEasing(dto.Easing, path);
        return animation;
    }

    private static UIAnimation BuildKeyFrames<T>(UIAnimationNodeDto dto, string path)
    {
        UIKeyFrameTrack<T> track = new();
        foreach (var frame in dto.Track)
        {
            T value;
            try
            {
                value = UIKeyFrameSerializer.Parse<T>(frame.Value);
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                throw new InvalidOperationException($"{path} ('{dto.Path}'): has an invalid frame value '{frame.Value}': {ex.Message}", ex);
            }

            track.Add(new UIKeyFrame<T>(frame.Offset, value, frame.Easing));
        }

        try
        {
            track.Validate();
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException($"{path} ('{dto.Path}'): is invalid: {ex.Message}", ex);
        }

        return new UIKeyFrameAnimation<T>(dto.Path) { Track = track };
    }

    private static T ParseValue<T>(string text, string path)
    {
        try
        {
            return UIKeyFrameSerializer.Parse<T>(text);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new InvalidOperationException($"{path}: has an invalid value '{text}': {ex.Message}", ex);
        }
    }

    private static IUIEasingFunction ResolveEasing(string name, string path)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        if (UIEasing.TryGet(name, out var easing))
        {
            return easing;
        }

        throw new InvalidOperationException(
            $"{path}: unknown easing '{name}'. Known easings: {string.Join(", ", UIEasing.Names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))}.");
    }

    private static void ApplyCommon(UIAnimation node, UIAnimationNodeDto dto)
    {
        // A group's own Duration is recomputed from its children when it starts (UIAnimationGroup.OnStarting -> ComputeDuration): never
        // authoritative for an unstarted tree, so it is neither serialized (SerializeNode) nor applied here for one, matching
        // UIKeyFrameClipSerializer's own reasoning about UIStoryboard.Duration.
        if (node is not UIAnimationGroup)
        {
            node.Duration = dto.Duration;
        }

        node.Delay = dto.Delay;
        node.RepeatCount = dto.RepeatCount;
        node.RepeatForever = dto.RepeatForever;
        node.AutoReverse = dto.AutoReverse;
        node.FillBehavior = ParseEnum<UIAnimationFillBehavior>(dto.FillBehavior) ?? UIAnimationFillBehavior.HoldEnd;
        node.CancelBehavior = ParseEnum<UIAnimationCancelBehavior>(dto.CancelBehavior) ?? UIAnimationCancelBehavior.RestoreBaseValue;
        node.Name = dto.Name;
    }

    private static TEnum? ParseEnum<TEnum>(string text) where TEnum : struct, Enum
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        if (Enum.TryParse<TEnum>(text, ignoreCase: true, out var value))
        {
            return value;
        }

        throw new InvalidOperationException($"Unknown {typeof(TEnum).Name} '{text}'.");
    }

    /// <summary>The JSON document (version 1).</summary>
    public sealed class UIAnimationDocumentDto
    {
        public int Version { get; set; } = CurrentVersion;
        public UIAnimationNodeDto Root { get; set; }
    }

    /// <summary>One node of the tree: a composite (<c>Storyboard</c>, <c>Sequence</c>), a <c>Delay</c>, or a leaf (<c>Property</c>,
    /// <c>KeyFrames</c>). No reference to any control instance.</summary>
    public sealed class UIAnimationNodeDto
    {
        public string Kind { get; set; }
        public string Element { get; set; }
        public string Path { get; set; }
        public string ValueType { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan Delay { get; set; }
        public int RepeatCount { get; set; }
        public bool RepeatForever { get; set; }
        public bool AutoReverse { get; set; }
        public string FillBehavior { get; set; }
        public string CancelBehavior { get; set; }
        public string Name { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Easing { get; set; }
        public List<UIKeyFrameSerializer.UIKeyFrameDto> Track { get; set; }
        public List<UIAnimationNodeDto> Children { get; set; }
    }
}
