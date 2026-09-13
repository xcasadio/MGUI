using System.Globalization;

namespace MGUI.Core.UI;

public static class ColorParser
{
    public static bool TryParse(string text, out ColorValue value)
        => TryParse(text, ColorValueFormat.HexRgba, out value);

    public static bool TryParse(string text, ColorValueFormat eightDigitHexFormat, out ColorValue value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        string trimmed = text.Trim();
        if (trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return TryParseHex(trimmed, eightDigitHexFormat, out value);
        }

        if (TryParseFunction(trimmed, "rgba", 4, out value))
        {
            return true;
        }

        if (TryParseFunction(trimmed, "rgb", 3, out value))
        {
            return true;
        }

        if (TryParseVector(trimmed, "Vector4", 4, out value))
        {
            return true;
        }

        if (TryParseVector(trimmed, "Vector3", 3, out value))
        {
            return true;
        }

        return false;
    }

    public static bool TryParseHex(string text, ColorValueFormat eightDigitHexFormat, out ColorValue value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text) || text[0] != '#')
        {
            return false;
        }

        string hex = text.Substring(1);
        return hex.Length switch
        {
            3 => TryParseRgbHex(hex, false, ColorValueFormat.HexRgb, out value),
            4 => TryParseRgbHex(hex, true, ColorValueFormat.HexRgba, out value),
            6 => TryParseRgbHex(hex, false, ColorValueFormat.HexRgb, out value),
            8 => TryParseRgbHex(hex, false, eightDigitHexFormat, out value),
            _ => false,
        };
    }

    private static bool TryParseRgbHex(string hex, bool shortAlpha, ColorValueFormat format, out ColorValue value)
    {
        value = default;
        if (format != ColorValueFormat.HexRgb && format != ColorValueFormat.HexArgb && format != ColorValueFormat.HexRgba)
        {
            return false;
        }

        if (hex.Length is 3 or 4)
        {
            if (!TryParseNibble(hex[0], out byte c0)
                || !TryParseNibble(hex[1], out byte c1)
                || !TryParseNibble(hex[2], out byte c2)
                || (shortAlpha && !TryParseNibble(hex[3], out _)))
            {
                return false;
            }

            byte r = ExpandNibble(c0);
            byte g = ExpandNibble(c1);
            byte b = ExpandNibble(c2);
            byte a = byte.MaxValue;
            if (shortAlpha)
            {
                _ = TryParseNibble(hex[3], out byte alphaNibble);
                a = ExpandNibble(alphaNibble);
            }

            value = FromBytes(r, g, b, a);
            return true;
        }

        if (hex.Length == 6 && TryParseByte(hex, 0, out byte r6) && TryParseByte(hex, 2, out byte g6) && TryParseByte(hex, 4, out byte b6))
        {
            value = FromBytes(r6, g6, b6, byte.MaxValue);
            return true;
        }

        if (hex.Length == 8)
        {
            if (!TryParseByte(hex, 0, out byte c0)
                || !TryParseByte(hex, 2, out byte c1)
                || !TryParseByte(hex, 4, out byte c2)
                || !TryParseByte(hex, 6, out byte c3))
            {
                return false;
            }

            if (format == ColorValueFormat.HexArgb)
            {
                value = FromBytes(c1, c2, c3, c0);
                return true;
            }

            if (format == ColorValueFormat.HexRgba)
            {
                value = FromBytes(c0, c1, c2, c3);
                return true;
            }
        }

        return false;
    }

    private static bool TryParseFunction(string text, string name, int expectedCount, out ColorValue value)
    {
        value = default;
        if (!TryGetArguments(text, name, expectedCount, out float[] components))
        {
            return false;
        }

        bool byteBased = components[0] > 1f || components[1] > 1f || components[2] > 1f;
        float r = byteBased ? components[0] / 255f : components[0];
        float g = byteBased ? components[1] / 255f : components[1];
        float b = byteBased ? components[2] / 255f : components[2];
        float a = 1f;
        if (expectedCount == 4)
        {
            a = components[3] > 1f ? components[3] / 255f : components[3];
        }

        value = new ColorValue(r, g, b, a);
        return true;
    }

    private static bool TryParseVector(string text, string name, int expectedCount, out ColorValue value)
    {
        value = default;
        if (!TryGetArguments(text, name, expectedCount, out float[] components))
        {
            return false;
        }

        float a = expectedCount == 4 ? components[3] : 1f;
        value = new ColorValue(components[0], components[1], components[2], a);
        return true;
    }

    private static bool TryGetArguments(string text, string name, int expectedCount, out float[] components)
    {
        components = null;
        if (!text.StartsWith(name, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int open = text.IndexOf('(');
        int close = text.LastIndexOf(')');
        if (open != name.Length || close <= open || close != text.Length - 1)
        {
            return false;
        }

        string[] parts = text.Substring(open + 1, close - open - 1).Split(',');
        if (parts.Length != expectedCount)
        {
            return false;
        }

        components = new float[expectedCount];
        for (int i = 0; i < parts.Length; i++)
        {
            if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out components[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static ColorValue FromBytes(byte r, byte g, byte b, byte a)
        => new(r / 255f, g / 255f, b / 255f, a / 255f);

    private static bool TryParseByte(string text, int start, out byte value)
    {
        value = 0;
        if (start + 2 > text.Length)
        {
            return false;
        }

        if (!byte.TryParse(text.Substring(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseNibble(char c, out byte value)
    {
        value = 0;
        int digit = c switch
        {
            >= '0' and <= '9' => c - '0',
            >= 'a' and <= 'f' => c - 'a' + 10,
            >= 'A' and <= 'F' => c - 'A' + 10,
            _ => -1,
        };

        if (digit < 0)
        {
            return false;
        }

        value = (byte)digit;
        return true;
    }

    private static byte ExpandNibble(byte value)
        => (byte)((value << 4) | value);
}