using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorSpaceConverterTests
{
    [Theory]
    [InlineData(0f, 0f)]
    [InlineData(0.04045f, 0.003130805f)]
    [InlineData(1f, 1f)]
    public void SrgbChannelToLinear_MatchesKnownValues(float srgb, float expectedLinear)
    {
        float actual = ColorSpaceConverter.SrgbChannelToLinear(srgb);

        Assert.InRange(MathF.Abs(actual - expectedLinear), 0f, 0.00001f);
    }

    [Fact]
    public void SrgbLinearRoundtrip_PreservesColorAndAlpha()
    {
        ColorValue srgb = new(0.25f, 0.5f, 0.75f, 0.4f, ColorSpaceMode.Srgb);

        ColorValue roundtrip = ColorSpaceConverter.LinearToSrgb(ColorSpaceConverter.SrgbToLinear(srgb));

        Assert.Equal(ColorSpaceMode.Srgb, roundtrip.ColorSpace);
        AssertClose(srgb.R, roundtrip.R);
        AssertClose(srgb.G, roundtrip.G);
        AssertClose(srgb.B, roundtrip.B);
        Assert.Equal(srgb.A, roundtrip.A);
    }

    [Theory]
    [InlineData(1f, 0f, 0f, 0f, 1f, 1f)]
    [InlineData(0f, 1f, 0f, 120f, 1f, 1f)]
    [InlineData(0f, 0f, 1f, 240f, 1f, 1f)]
    [InlineData(1f, 1f, 1f, 0f, 0f, 1f)]
    [InlineData(0.5f, 0.5f, 0.5f, 0f, 0f, 0.5f)]
    public void RgbToHsv_MatchesExpectedValues(float r, float g, float b, float h, float s, float v)
    {
        HsvColor hsv = ColorSpaceConverter.RgbToHsv(new ColorValue(r, g, b, 0.25f));

        AssertClose(h, hsv.H);
        AssertClose(s, hsv.S);
        AssertClose(v, hsv.V);
        Assert.Equal(0.25f, hsv.A);
    }

    [Theory]
    [InlineData(0.25f, 0.5f, 0.75f)]
    [InlineData(0.9f, 0.2f, 0.1f)]
    [InlineData(0.1f, 0.8f, 0.4f)]
    public void HsvRoundtrip_PreservesRgb(float r, float g, float b)
    {
        ColorValue original = new(r, g, b, 0.6f, ColorSpaceMode.Linear);
        ColorValue roundtrip = ColorSpaceConverter.HsvToRgb(ColorSpaceConverter.RgbToHsv(original), ColorSpaceMode.Linear);

        AssertClose(original.R, roundtrip.R);
        AssertClose(original.G, roundtrip.G);
        AssertClose(original.B, roundtrip.B);
        Assert.Equal(original.A, roundtrip.A);
        Assert.Equal(ColorSpaceMode.Linear, roundtrip.ColorSpace);
    }

    [Theory]
    [InlineData(1f, 0f, 0f, 0f, 1f, 0.5f)]
    [InlineData(0f, 1f, 0f, 120f, 1f, 0.5f)]
    [InlineData(0f, 0f, 1f, 240f, 1f, 0.5f)]
    [InlineData(1f, 1f, 1f, 0f, 0f, 1f)]
    [InlineData(0.5f, 0.5f, 0.5f, 0f, 0f, 0.5f)]
    public void RgbToHsl_MatchesExpectedValues(float r, float g, float b, float h, float s, float l)
    {
        HslColor hsl = ColorSpaceConverter.RgbToHsl(new ColorValue(r, g, b, 0.75f));

        AssertClose(h, hsl.H);
        AssertClose(s, hsl.S);
        AssertClose(l, hsl.L);
        Assert.Equal(0.75f, hsl.A);
    }

    [Theory]
    [InlineData(0.25f, 0.5f, 0.75f)]
    [InlineData(0.9f, 0.2f, 0.1f)]
    [InlineData(0.1f, 0.8f, 0.4f)]
    public void HslRoundtrip_PreservesRgb(float r, float g, float b)
    {
        ColorValue original = new(r, g, b, 0.3f);
        ColorValue roundtrip = ColorSpaceConverter.HslToRgb(ColorSpaceConverter.RgbToHsl(original));

        AssertClose(original.R, roundtrip.R);
        AssertClose(original.G, roundtrip.G);
        AssertClose(original.B, roundtrip.B);
        Assert.Equal(original.A, roundtrip.A);
    }

    [Theory]
    [InlineData(float.NaN, 0f)]
    [InlineData(-30f, 330f)]
    [InlineData(390f, 30f)]
    public void NormalizeHue_ReturnsStableDegrees(float hue, float expected)
    {
        Assert.Equal(expected, ColorSpaceConverter.NormalizeHue(hue));
    }

    private static void AssertClose(float expected, float actual)
        => Assert.InRange(MathF.Abs(expected - actual), 0f, 0.0001f);
}