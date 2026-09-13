namespace MGUI.Core.UI;

public static class ColorSpaceConverter
{
    public static float SrgbChannelToLinear(float value)
    {
        if (value <= 0.04045f)
        {
            return value / 12.92f;
        }

        return MathF.Pow((value + 0.055f) / 1.055f, 2.4f);
    }

    public static float LinearChannelToSrgb(float value)
    {
        if (value <= 0.0031308f)
        {
            return value * 12.92f;
        }

        return 1.055f * MathF.Pow(value, 1f / 2.4f) - 0.055f;
    }

    public static ColorValue SrgbToLinear(ColorValue value)
    {
        if (value.ColorSpace == ColorSpaceMode.Linear)
        {
            return value;
        }

        return new ColorValue(
            SrgbChannelToLinear(value.R),
            SrgbChannelToLinear(value.G),
            SrgbChannelToLinear(value.B),
            value.A,
            ColorSpaceMode.Linear,
            value.IsHdr);
    }

    public static ColorValue LinearToSrgb(ColorValue value)
    {
        if (value.ColorSpace == ColorSpaceMode.Srgb)
        {
            return value;
        }

        return new ColorValue(
            LinearChannelToSrgb(value.R),
            LinearChannelToSrgb(value.G),
            LinearChannelToSrgb(value.B),
            value.A,
            ColorSpaceMode.Srgb,
            value.IsHdr);
    }

    public static ColorValue Convert(ColorValue value, ColorSpaceMode targetColorSpace)
    {
        if (value.ColorSpace == targetColorSpace)
        {
            return value;
        }

        return targetColorSpace switch
        {
            ColorSpaceMode.Srgb => LinearToSrgb(value),
            ColorSpaceMode.Linear => SrgbToLinear(value),
            _ => value.WithColorSpace(targetColorSpace),
        };
    }

    public static HsvColor RgbToHsv(ColorValue value)
    {
        float max = MathF.Max(value.R, MathF.Max(value.G, value.B));
        float min = MathF.Min(value.R, MathF.Min(value.G, value.B));
        float delta = max - min;

        float hue = 0f;
        if (delta > 0f)
        {
            if (max.Equals(value.R))
            {
                hue = 60f * (((value.G - value.B) / delta) % 6f);
            }
            else if (max.Equals(value.G))
            {
                hue = 60f * (((value.B - value.R) / delta) + 2f);
            }
            else
            {
                hue = 60f * (((value.R - value.G) / delta) + 4f);
            }
        }

        if (hue < 0f)
        {
            hue += 360f;
        }

        float saturation = max <= 0f ? 0f : delta / max;
        return new HsvColor(NormalizeHue(hue), saturation, max, value.A);
    }

    public static ColorValue HsvToRgb(HsvColor value)
        => HsvToRgb(value, ColorSpaceMode.Srgb);

    public static ColorValue HsvToRgb(HsvColor value, ColorSpaceMode colorSpace)
    {
        float hue = NormalizeHue(value.H);
        float saturation = Math.Clamp(value.S, 0f, 1f);
        float chroma = value.V * saturation;
        float huePrime = hue / 60f;
        float x = chroma * (1f - MathF.Abs((huePrime % 2f) - 1f));
        float m = value.V - chroma;

        (float r, float g, float b) = huePrime switch
        {
            >= 0f and < 1f => (chroma, x, 0f),
            >= 1f and < 2f => (x, chroma, 0f),
            >= 2f and < 3f => (0f, chroma, x),
            >= 3f and < 4f => (0f, x, chroma),
            >= 4f and < 5f => (x, 0f, chroma),
            _ => (chroma, 0f, x),
        };

        return new ColorValue(r + m, g + m, b + m, value.A, colorSpace);
    }

    public static HslColor RgbToHsl(ColorValue value)
    {
        float max = MathF.Max(value.R, MathF.Max(value.G, value.B));
        float min = MathF.Min(value.R, MathF.Min(value.G, value.B));
        float delta = max - min;
        float lightness = (max + min) * 0.5f;

        float hue = 0f;
        float saturation = 0f;
        if (delta > 0f)
        {
            saturation = delta / (1f - MathF.Abs(2f * lightness - 1f));

            if (max.Equals(value.R))
            {
                hue = 60f * (((value.G - value.B) / delta) % 6f);
            }
            else if (max.Equals(value.G))
            {
                hue = 60f * (((value.B - value.R) / delta) + 2f);
            }
            else
            {
                hue = 60f * (((value.R - value.G) / delta) + 4f);
            }
        }

        if (hue < 0f)
        {
            hue += 360f;
        }

        return new HslColor(NormalizeHue(hue), saturation, lightness, value.A);
    }

    public static ColorValue HslToRgb(HslColor value)
        => HslToRgb(value, ColorSpaceMode.Srgb);

    public static ColorValue HslToRgb(HslColor value, ColorSpaceMode colorSpace)
    {
        float hue = NormalizeHue(value.H);
        float saturation = Math.Clamp(value.S, 0f, 1f);
        float lightness = value.L;
        float chroma = (1f - MathF.Abs(2f * lightness - 1f)) * saturation;
        float huePrime = hue / 60f;
        float x = chroma * (1f - MathF.Abs((huePrime % 2f) - 1f));
        float m = lightness - chroma * 0.5f;

        (float r, float g, float b) = huePrime switch
        {
            >= 0f and < 1f => (chroma, x, 0f),
            >= 1f and < 2f => (x, chroma, 0f),
            >= 2f and < 3f => (0f, chroma, x),
            >= 3f and < 4f => (0f, x, chroma),
            >= 4f and < 5f => (x, 0f, chroma),
            _ => (chroma, 0f, x),
        };

        return new ColorValue(r + m, g + m, b + m, value.A, colorSpace);
    }

    public static float NormalizeHue(float hue)
    {
        if (float.IsNaN(hue) || float.IsInfinity(hue))
        {
            return 0f;
        }

        float result = hue % 360f;
        return result < 0f ? result + 360f : result;
    }
}