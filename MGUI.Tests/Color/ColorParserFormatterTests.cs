using MGUI.Core.UI;

namespace MGUI.Tests.ColorPicker;

public class ColorParserFormatterTests
{
    [Theory]
    [InlineData("#fff", 1f, 1f, 1f, 1f)]
    [InlineData("#0f08", 0f, 1f, 0f, 0.53333336f)]
    [InlineData("#3366CC", 0.2f, 0.4f, 0.8f, 1f)]
    [InlineData("#3366CC80", 0.2f, 0.4f, 0.8f, 0.5019608f)]
    public void TryParse_DefaultHex_ParsesRgbaForms(string text, float r, float g, float b, float a)
    {
        Assert.True(ColorParser.TryParse(text, out ColorValue value));

        AssertClose(r, value.R);
        AssertClose(g, value.G);
        AssertClose(b, value.B);
        AssertClose(a, value.A);
    }

    [Fact]
    public void TryParseHex_EightDigits_CanBeArgbOrRgba()
    {
        Assert.True(ColorParser.TryParse("#803366CC", ColorValueFormat.HexArgb, out ColorValue argb));
        Assert.True(ColorParser.TryParse("#3366CC80", ColorValueFormat.HexRgba, out ColorValue rgba));

        AssertClose(rgba.R, argb.R);
        AssertClose(rgba.G, argb.G);
        AssertClose(rgba.B, argb.B);
        AssertClose(rgba.A, argb.A);
    }

    [Theory]
    [InlineData("rgb(255, 128, 0)", 1f, 0.5019608f, 0f, 1f)]
    [InlineData("rgba(255, 128, 0, 0.5)", 1f, 0.5019608f, 0f, 0.5f)]
    [InlineData("rgba(1, 0.5, 0, 0.25)", 1f, 0.5f, 0f, 0.25f)]
    [InlineData("Vector3(1, 0.5, 0)", 1f, 0.5f, 0f, 1f)]
    [InlineData("Vector4(1, 0.5, 0, 0.25)", 1f, 0.5f, 0f, 0.25f)]
    public void TryParse_FunctionFormats_ParsesInvariantValues(string text, float r, float g, float b, float a)
    {
        Assert.True(ColorValue.TryParse(text, out ColorValue value));

        AssertClose(r, value.R);
        AssertClose(g, value.G);
        AssertClose(b, value.B);
        AssertClose(a, value.A);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#ggg")]
    [InlineData("rgb(1, 2)")]
    [InlineData("rgba(1, 2, 3)")]
    [InlineData("Vector4(1, 2, 3)")]
    public void TryParse_InvalidText_ReturnsFalse(string text)
    {
        Assert.False(ColorParser.TryParse(text, out _));
    }

    [Theory]
    [InlineData(ColorValueFormat.HexRgb, "#3366CC")]
    [InlineData(ColorValueFormat.HexArgb, "#803366CC")]
    [InlineData(ColorValueFormat.HexRgba, "#3366CC80")]
    [InlineData(ColorValueFormat.RgbByte, "rgb(51, 102, 204)")]
    [InlineData(ColorValueFormat.RgbaByte, "rgba(51, 102, 204, 128)")]
    [InlineData(ColorValueFormat.RgbFloat, "rgb(0.2, 0.4, 0.8)")]
    [InlineData(ColorValueFormat.RgbaFloat, "rgba(0.2, 0.4, 0.8, 0.5019608)")]
    [InlineData(ColorValueFormat.Vector3, "Vector3(0.2, 0.4, 0.8)")]
    [InlineData(ColorValueFormat.Vector4, "Vector4(0.2, 0.4, 0.8, 0.5019608)")]
    public void Format_ProducesStableText(ColorValueFormat format, string expected)
    {
        ColorValue value = new(0.2f, 0.4f, 0.8f, 128f / 255f);

        Assert.Equal(expected, ColorFormatter.Format(value, format));
    }

    [Fact]
    public void ToHex_RejectsNonHexFormats()
    {
        ColorValue value = new(1f, 1f, 1f, 1f);

        Assert.Throws<ArgumentException>(() => value.ToHex(ColorValueFormat.RgbByte));
    }

    private static void AssertClose(float expected, float actual)
        => Assert.InRange(MathF.Abs(expected - actual), 0f, 0.0001f);
}