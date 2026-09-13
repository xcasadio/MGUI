namespace MGUI.Core.UI;

public static class ColorTemperatureConverter
{
    public const float DefaultMinKelvin = 1000f;
    public const float DefaultMaxKelvin = 12000f;

    public static ColorValue KelvinToRgb(float kelvin, ColorSpaceMode outputColorSpace = ColorSpaceMode.Srgb, float minKelvin = DefaultMinKelvin, float maxKelvin = DefaultMaxKelvin)
    {
        float actualKelvin = ClampKelvin(kelvin, minKelvin, maxKelvin);
        float temperature = actualKelvin / 100f;

        float red = temperature <= 66f ? 255f : 329.698727446f * MathF.Pow(temperature - 60f, -0.1332047592f);
        float green = temperature <= 66f
            ? 99.4708025861f * MathF.Log(temperature) - 161.1195681661f
            : 288.1221695283f * MathF.Pow(temperature - 60f, -0.0755148492f);
        float blue = temperature >= 66f ? 255f : temperature <= 19f ? 0f : 138.5177312231f * MathF.Log(temperature - 10f) - 305.0447927307f;

        ColorValue srgb = new(ClampByte(red) / 255f, ClampByte(green) / 255f, ClampByte(blue) / 255f, 1f, ColorSpaceMode.Srgb);
        return ColorSpaceConverter.Convert(srgb, outputColorSpace);
    }

    public static float ClampKelvin(float kelvin, float minKelvin = DefaultMinKelvin, float maxKelvin = DefaultMaxKelvin)
    {
        if (minKelvin > maxKelvin)
        {
            (minKelvin, maxKelvin) = (maxKelvin, minKelvin);
        }

        return Math.Clamp(kelvin, minKelvin, maxKelvin);
    }

    private static float ClampByte(float value)
        => Math.Clamp(value, 0f, 255f);
}