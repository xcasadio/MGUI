using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MGUI.Core.UI;

public static class MGColorPaletteSerializer
{
    public const int CurrentVersion = 1;

    public static string ToJson(MGColorPalette palette, bool indented = true)
    {
        if (palette == null)
        {
            throw new ArgumentNullException(nameof(palette));
        }

        PaletteDto dto = new()
        {
            Version = CurrentVersion,
            Name = palette.Name ?? string.Empty,
        };

        foreach (var swatch in palette.Swatches)
        {
            dto.Swatches.Add(new SwatchDto
            {
                Name = swatch.Name ?? string.Empty,
                Value = ColorValueDto.FromColorValue(swatch.Value),
                Space = swatch.Value.ColorSpace.ToString(),
                IsHdr = swatch.Value.IsHdr,
                Metadata = swatch.Metadata.Count == 0 ? null : new Dictionary<string, string>(swatch.Metadata),
            });
        }

        return JsonSerializer.Serialize(dto, new JsonSerializerOptions
        {
            WriteIndented = indented,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
    }

    public static MGColorPaletteSerializationResult FromJson(string json)
    {
        List<string> diagnostics = new();
        if (string.IsNullOrWhiteSpace(json))
        {
            diagnostics.Add("Palette JSON is empty.");
            return new MGColorPaletteSerializationResult(false, null, diagnostics);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                diagnostics.Add("Palette JSON root must be an object.");
                return new MGColorPaletteSerializationResult(false, null, diagnostics);
            }

            var root = document.RootElement;
            var name = GetOptionalString(root, "name");
            MGColorPalette palette = new(string.IsNullOrWhiteSpace(name) ? "Palette" : name.Trim());
            if (!root.TryGetProperty("swatches", out var swatchesElement) || swatchesElement.ValueKind != JsonValueKind.Array)
            {
                diagnostics.Add("Palette JSON has no swatches array.");
                return new MGColorPaletteSerializationResult(true, palette, diagnostics);
            }

            HashSet<string> usedNames = new(StringComparer.OrdinalIgnoreCase);
            var index = 0;
            foreach (var swatchElement in swatchesElement.EnumerateArray())
            {
                index++;
                if (swatchElement.ValueKind != JsonValueKind.Object)
                {
                    diagnostics.Add($"Swatch {index} was skipped because it is not an object.");
                    continue;
                }

                if (!TryReadSwatch(swatchElement, index, usedNames, diagnostics, out var swatch))
                {
                    continue;
                }

                palette.Swatches.Add(swatch);
            }

            return new MGColorPaletteSerializationResult(true, palette, diagnostics);
        }
        catch (JsonException ex)
        {
            diagnostics.Add($"Invalid palette JSON: {ex.Message}");
            return new MGColorPaletteSerializationResult(false, null, diagnostics);
        }
    }

    public static bool TryFromJson(string json, out MGColorPalette palette, out IReadOnlyList<string> diagnostics)
    {
        var result = FromJson(json);
        palette = result.Palette;
        diagnostics = result.Diagnostics;
        return result.Success;
    }

    private static bool TryReadSwatch(JsonElement swatchElement, int index, HashSet<string> usedNames, List<string> diagnostics, out MGColorSwatch swatch)
    {
        swatch = null;
        var name = GetOptionalString(swatchElement, "name");
        name = MakeUniqueName(string.IsNullOrWhiteSpace(name) ? $"Color {index}" : name.Trim(), usedNames);

        var colorSpace = ColorSpaceMode.Srgb;
        var space = GetOptionalString(swatchElement, "space");
        if (!string.IsNullOrWhiteSpace(space) && !Enum.TryParse(space, ignoreCase: true, out colorSpace))
        {
            diagnostics.Add($"Swatch {index} has unknown color space '{space}', using sRGB.");
            colorSpace = ColorSpaceMode.Srgb;
        }

        var isHdr = TryGetBool(swatchElement, "isHdr", out var hdr) && hdr;
        if (!swatchElement.TryGetProperty("value", out var valueElement) || !TryReadColorValue(valueElement, colorSpace, isHdr, out var value))
        {
            diagnostics.Add($"Swatch {index} was skipped because its value is invalid.");
            return false;
        }

        swatch = new MGColorSwatch(name, value);
        if (swatchElement.TryGetProperty("metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in metadataElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    swatch.Metadata[property.Name] = property.Value.GetString() ?? string.Empty;
                }
            }
        }

        return true;
    }

    private static bool TryReadColorValue(JsonElement valueElement, ColorSpaceMode colorSpace, bool isHdr, out ColorValue value)
    {
        value = default;
        if (valueElement.ValueKind == JsonValueKind.String)
        {
            var text = valueElement.GetString();
            if (!ColorParser.TryParse(text, out value))
            {
                return false;
            }

            value = ColorSpaceConverter.Convert(value, colorSpace);
            return true;
        }

        if (valueElement.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (!TryGetFloat(valueElement, "r", out var r) || !TryGetFloat(valueElement, "g", out var g) || !TryGetFloat(valueElement, "b", out var b))
        {
            return false;
        }

        var a = TryGetFloat(valueElement, "a", out var alpha) ? alpha : 1f;
        value = new ColorValue(r, g, b, Math.Clamp(a, 0f, 1f), colorSpace, isHdr || r > 1f || g > 1f || b > 1f);
        return true;
    }

    private static string MakeUniqueName(string baseName, HashSet<string> usedNames)
    {
        var actual = baseName;
        var suffix = 2;
        while (!usedNames.Add(actual))
        {
            actual = string.Create(CultureInfo.InvariantCulture, $"{baseName} ({suffix++})");
        }

        return actual;
    }

    private static string GetOptionalString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String ? property.GetString() : null;

    private static bool TryGetBool(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property) || (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryGetFloat(JsonElement element, string propertyName, out float value)
    {
        value = 0f;
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
        {
            return false;
        }

        return property.TryGetSingle(out value);
    }

    private sealed class PaletteDto
    {
        [JsonPropertyName("version")]
        public int Version { get; set; }
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("swatches")]
        public List<SwatchDto> Swatches { get; } = new();
    }

    private sealed class SwatchDto
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
        [JsonPropertyName("value")]
        public ColorValueDto Value { get; set; }
        [JsonPropertyName("space")]
        public string Space { get; set; }
        [JsonPropertyName("isHdr")]
        public bool IsHdr { get; set; }
        [JsonPropertyName("metadata")]
        public Dictionary<string, string> Metadata { get; set; }
    }

    private sealed class ColorValueDto
    {
        [JsonPropertyName("r")]
        public float R { get; set; }
        [JsonPropertyName("g")]
        public float G { get; set; }
        [JsonPropertyName("b")]
        public float B { get; set; }
        [JsonPropertyName("a")]
        public float A { get; set; }

        public static ColorValueDto FromColorValue(ColorValue value)
            => new() { R = value.R, G = value.G, B = value.B, A = value.A };
    }
}