using System.Globalization;

namespace MGUI.Core.UI;

public static class ColorFormatter
{
    public static string Format(ColorValue value, ColorValueFormat format)
    {
        return format switch
        {
            ColorValueFormat.HexRgb => FormatHexRgb(value),
            ColorValueFormat.HexArgb => FormatHexArgb(value),
            ColorValueFormat.HexRgba => FormatHexRgba(value),
            ColorValueFormat.RgbByte => FormatRgbByte(value),
            ColorValueFormat.RgbaByte => FormatRgbaByte(value),
            ColorValueFormat.RgbFloat => FormatRgbFloat(value),
            ColorValueFormat.RgbaFloat => FormatRgbaFloat(value),
            ColorValueFormat.Vector3 => FormatVector3(value),
            ColorValueFormat.Vector4 => FormatVector4(value),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    public static string ToHex(ColorValue value, ColorValueFormat format)
    {
        return format switch
        {
            ColorValueFormat.HexRgb => FormatHexRgb(value),
            ColorValueFormat.HexArgb => FormatHexArgb(value),
            ColorValueFormat.HexRgba => FormatHexRgba(value),
            _ => throw new ArgumentException($"{nameof(format)} must be a hex format.", nameof(format)),
        };
    }

    private static string FormatHexRgb(ColorValue value)
    {
        (byte r, byte g, byte b, _) = ToBytes(value);
        return FormattableString.Invariant($"#{r:X2}{g:X2}{b:X2}");
    }

    private static string FormatHexArgb(ColorValue value)
    {
        (byte r, byte g, byte b, byte a) = ToBytes(value);
        return FormattableString.Invariant($"#{a:X2}{r:X2}{g:X2}{b:X2}");
    }

    private static string FormatHexRgba(ColorValue value)
    {
        (byte r, byte g, byte b, byte a) = ToBytes(value);
        return FormattableString.Invariant($"#{r:X2}{g:X2}{b:X2}{a:X2}");
    }

    private static string FormatRgbByte(ColorValue value)
    {
        (byte r, byte g, byte b, _) = ToBytes(value);
        return FormattableString.Invariant($"rgb({r}, {g}, {b})");
    }

    private static string FormatRgbaByte(ColorValue value)
    {
        (byte r, byte g, byte b, byte a) = ToBytes(value);
        return FormattableString.Invariant($"rgba({r}, {g}, {b}, {a})");
    }

    private static string FormatRgbFloat(ColorValue value)
        => string.Create(CultureInfo.InvariantCulture, $"rgb({value.R:R}, {value.G:R}, {value.B:R})");

    private static string FormatRgbaFloat(ColorValue value)
        => string.Create(CultureInfo.InvariantCulture, $"rgba({value.R:R}, {value.G:R}, {value.B:R}, {value.A:R})");

    private static string FormatVector3(ColorValue value)
        => string.Create(CultureInfo.InvariantCulture, $"Vector3({value.R:R}, {value.G:R}, {value.B:R})");

    private static string FormatVector4(ColorValue value)
        => string.Create(CultureInfo.InvariantCulture, $"Vector4({value.R:R}, {value.G:R}, {value.B:R}, {value.A:R})");

    private static (byte R, byte G, byte B, byte A) ToBytes(ColorValue value)
    {
        ColorValue clamped = value.ClampLdr();
        return (ToByte(clamped.R), ToByte(clamped.G), ToByte(clamped.B), ToByte(clamped.A));
    }

    private static byte ToByte(float value)
        => (byte)Math.Clamp((int)MathF.Round(value * byte.MaxValue), byte.MinValue, byte.MaxValue);
}