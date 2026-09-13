namespace MGUI.Core.UI;

public sealed class ColorPickerConstraints
{
    public bool AllowAlpha { get; set; } = true;
    public bool AllowHdr { get; set; } = false;
    public bool AllowNull { get; set; } = false;
    public float MinChannelValue { get; set; } = 0f;
    public float MaxChannelValue { get; set; } = 1f;
    public float MinIntensity { get; set; } = 0f;
    public float MaxIntensity { get; set; } = 16f;

    public ColorValue Apply(ColorValue value)
    {
        var maxChannel = AllowHdr ? MaxChannelValue : Math.Min(MaxChannelValue, 1f);
        var r = Clamp(value.R, MinChannelValue, maxChannel);
        var g = Clamp(value.G, MinChannelValue, maxChannel);
        var b = Clamp(value.B, MinChannelValue, maxChannel);
        var a = AllowAlpha ? Clamp(value.A, 0f, 1f) : 1f;
        return new ColorValue(r, g, b, a, value.ColorSpace, AllowHdr && value.IsHdr);
    }

    public float ClampIntensity(float intensity)
        => Clamp(intensity, MinIntensity, MaxIntensity);

    private static float Clamp(float value, float min, float max)
    {
        if (min > max)
        {
            (min, max) = (max, min);
        }

        return Math.Clamp(value, min, max);
    }
}