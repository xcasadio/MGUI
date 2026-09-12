using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Core.UI.Animation.KeyFrames
{
    /// <summary>
    /// JSON form of a <see cref="UIKeyFrameTrack{T}"/> (ADR-0007, decision 2), the target format of the author's animation editor:
    /// <code>
    /// { "version": 1, "valueType": "Single", "frames": [ { "offset": 0, "value": "0" }, { "offset": 1, "value": "1", "easing": "CubicOut" } ] }
    /// </code>
    /// Values are invariant-culture strings: <c>float</c> / <c>double</c> / <c>int</c> as numbers, <c>Vector2</c> / <c>Vector3</c> / <c>Vector4</c>
    /// as <c>x,y[,z[,w]]</c>, <c>Color</c> as <c>#RRGGBBAA</c>, <c>Thickness</c> as <c>l,t,r,b</c>. Unknown value types and versions fail explicitly;
    /// the value type of the JSON must match the requested type argument on read.
    /// </summary>
    public static class UIKeyFrameSerializer
    {
        public const int CurrentVersion = 1;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>Serializes <paramref name="track"/> to JSON.</summary>
        public static string Serialize<T>(UIKeyFrameTrack<T> track)
        {
            if (track == null)
            {
                throw new ArgumentNullException(nameof(track));
            }

            UIKeyFrameTrackDto dto = new()
            {
                Version = CurrentVersion,
                ValueType = TypeName<T>(),
                Frames = track.Frames.Select(x => new UIKeyFrameDto { Offset = x.Offset, Value = Format(x.Value), Easing = x.Easing }).ToList(),
            };
            return JsonSerializer.Serialize(dto, JsonOptions);
        }

        /// <summary>Deserializes a track of <typeparamref name="T"/>; the JSON's value type must match.</summary>
        public static UIKeyFrameTrack<T> Deserialize<T>(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new ArgumentException("Empty JSON.", nameof(json));
            }

            UIKeyFrameTrackDto dto = JsonSerializer.Deserialize<UIKeyFrameTrackDto>(json, JsonOptions)
                ?? throw new InvalidOperationException("The JSON does not describe a key frame track.");
            if (dto.Version != CurrentVersion)
            {
                throw new InvalidOperationException($"Unsupported key frame track version {dto.Version} (supported: {CurrentVersion}).");
            }

            string expected = TypeName<T>();
            if (!string.Equals(dto.ValueType, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The key frame track holds values of type '{dto.ValueType}', not '{expected}'.");
            }

            UIKeyFrameTrack<T> track = new();
            foreach (UIKeyFrameDto frame in dto.Frames ?? new List<UIKeyFrameDto>())
            {
                track.Add(new UIKeyFrame<T>(frame.Offset, Parse<T>(frame.Value), frame.Easing));
            }

            return track;
        }

        /// <summary>The value type name stored in a JSON track, without reading its frames.</summary>
        public static string ReadValueType(string json)
        {
            UIKeyFrameTrackDto dto = JsonSerializer.Deserialize<UIKeyFrameTrackDto>(json, JsonOptions)
                ?? throw new InvalidOperationException("The JSON does not describe a key frame track.");
            return dto.ValueType;
        }

        private static string TypeName<T>() => typeof(T).Name;

        private static string Format<T>(T value) => value switch
        {
            float f => f.ToString("R", CultureInfo.InvariantCulture),
            double d => d.ToString("R", CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),
            Vector2 v => Join(v.X, v.Y),
            Vector3 v => Join(v.X, v.Y, v.Z),
            Vector4 v => Join(v.X, v.Y, v.Z, v.W),
            Color c => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}",
            Thickness t => string.Join(",", new[] { t.Left, t.Top, t.Right, t.Bottom }.Select(x => x.ToString(CultureInfo.InvariantCulture))),
            _ => throw new NotSupportedException($"Key frame values of type '{typeof(T).Name}' cannot be serialized. Supported: Single, Double, Int32, Vector2, Vector3, Vector4, Color, Thickness."),
        };

        private static T Parse<T>(string text)
        {
            if (text == null)
            {
                throw new InvalidOperationException("A key frame has no value.");
            }

            object value;
            Type type = typeof(T);
            if (type == typeof(float)) value = float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            else if (type == typeof(double)) value = double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
            else if (type == typeof(int)) value = int.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            else if (type == typeof(Vector2)) { float[] p = Floats(text, 2); value = new Vector2(p[0], p[1]); }
            else if (type == typeof(Vector3)) { float[] p = Floats(text, 3); value = new Vector3(p[0], p[1], p[2]); }
            else if (type == typeof(Vector4)) { float[] p = Floats(text, 4); value = new Vector4(p[0], p[1], p[2], p[3]); }
            else if (type == typeof(Color)) value = ParseColor(text);
            else if (type == typeof(Thickness)) { int[] p = Ints(text, 4); value = new Thickness(p[0], p[1], p[2], p[3]); }
            else throw new NotSupportedException($"Key frame values of type '{type.Name}' cannot be deserialized.");

            return (T)value;
        }

        private static string Join(params float[] values) => string.Join(",", values.Select(x => x.ToString("R", CultureInfo.InvariantCulture)));

        private static float[] Floats(string text, int count)
        {
            string[] parts = text.Split(',');
            if (parts.Length != count)
            {
                throw new FormatException($"Expected {count} comma-separated numbers, got '{text}'.");
            }

            return parts.Select(x => float.Parse(x.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture)).ToArray();
        }

        private static int[] Ints(string text, int count)
        {
            string[] parts = text.Split(',');
            if (parts.Length != count)
            {
                throw new FormatException($"Expected {count} comma-separated integers, got '{text}'.");
            }

            return parts.Select(x => int.Parse(x.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture)).ToArray();
        }

        private static Color ParseColor(string text)
        {
            string hex = text.Trim().TrimStart('#');
            if (hex.Length != 6 && hex.Length != 8)
            {
                throw new FormatException($"Expected a colour as #RRGGBB or #RRGGBBAA, got '{text}'.");
            }

            byte r = byte.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte g = byte.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte b = byte.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            byte a = hex.Length == 8 ? byte.Parse(hex[6..8], NumberStyles.HexNumber, CultureInfo.InvariantCulture) : (byte)255;
            return new Color(r, g, b, a);
        }

        /// <summary>The JSON document (version 1).</summary>
        public sealed class UIKeyFrameTrackDto
        {
            public int Version { get; set; }
            public string ValueType { get; set; }
            public List<UIKeyFrameDto> Frames { get; set; }
        }

        /// <summary>One key of the JSON document.</summary>
        public sealed class UIKeyFrameDto
        {
            public float Offset { get; set; }
            public string Value { get; set; }
            public string Easing { get; set; }
        }
    }
}
