using MGUI.Core.UI;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.ColorPicker;

public class ColorValueTests
{
    [Fact]
    public void FromXnaColor_MapsBytesToUnitChannels()
    {
        ColorValue value = ColorValue.FromXnaColor(Microsoft.Xna.Framework.Color.White);

        Assert.Equal(1f, value.R);
        Assert.Equal(1f, value.G);
        Assert.Equal(1f, value.B);
        Assert.Equal(1f, value.A);
        Assert.Equal(ColorSpaceMode.Srgb, value.ColorSpace);
        Assert.False(value.IsHdr);
    }

    [Fact]
    public void ToXnaColor_ClampsHdrAndAlphaToLdrBytes()
    {
        ColorValue value = new(2.5f, -0.25f, 0.5f, 1.5f, ColorSpaceMode.Linear, true);

        Microsoft.Xna.Framework.Color color = value.ToXnaColor();

        Assert.Equal(byte.MaxValue, color.R);
        Assert.Equal(byte.MinValue, color.G);
        Assert.Equal(128, color.B);
        Assert.Equal(byte.MaxValue, color.A);
    }

    [Fact]
    public void VectorFactories_PreserveChannelsAndAlpha()
    {
        ColorValue xnaVector = ColorValue.FromVector4(new Vector4(0.25f, 0.5f, 0.75f, 0.4f), ColorSpaceMode.Linear);
        ColorValue systemVector = ColorValue.FromVector3(new System.Numerics.Vector3(0.1f, 0.2f, 0.3f));

        Assert.Equal(new ColorValue(0.25f, 0.5f, 0.75f, 0.4f, ColorSpaceMode.Linear), xnaVector);
        Assert.Equal(new ColorValue(0.1f, 0.2f, 0.3f, 1f), systemVector);
    }

    [Fact]
    public void WithAlpha_And_ClampLdr_ReturnAdjustedCopies()
    {
        ColorValue hdr = new(4f, 0.5f, 0.25f, 0.75f, ColorSpaceMode.Linear);

        ColorValue changedAlpha = hdr.WithAlpha(0.2f);
        ColorValue clamped = changedAlpha.ClampLdr();

        Assert.True(hdr.IsHdr);
        Assert.Equal(0.2f, changedAlpha.A);
        Assert.True(changedAlpha.IsHdr);
        Assert.Equal(new ColorValue(1f, 0.5f, 0.25f, 0.2f, ColorSpaceMode.Linear), clamped);
        Assert.False(clamped.IsHdr);
    }
}