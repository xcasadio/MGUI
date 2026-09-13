using System.Globalization;

namespace MGUI.Core.UI;

public sealed class MGColorTextInputModel
{
    public ColorValue Value { get; private set; }
    public ColorValueFormat HexFormat { get; set; } = ColorValueFormat.HexRgba;
    public bool HasValidationError { get; private set; }
    public string HexText { get; private set; }
    public string RedText { get; private set; }
    public string GreenText { get; private set; }
    public string BlueText { get; private set; }
    public string AlphaText { get; private set; }
    public string HueText { get; private set; }
    public string SaturationText { get; private set; }
    public string ValueText { get; private set; }

    public event EventHandler<ColorValueChangedEventArgs> ValueChanged;

    public MGColorTextInputModel()
        : this(new ColorValue(1f, 1f, 1f, 1f))
    {
    }

    public MGColorTextInputModel(ColorValue value)
    {
        SetValue(value, raiseEvent: false);
    }

    public void SetValue(ColorValue value)
        => SetValue(value, raiseEvent: true);

    public bool TrySetHexText(string text)
    {
        HexText = text ?? string.Empty;
        if (!ColorParser.TryParse(HexText, HexFormat, out ColorValue parsed))
        {
            HasValidationError = true;
            return false;
        }

        SetValue(parsed);
        return true;
    }

    public bool TrySetRgbByteText(string red, string green, string blue, string alpha)
    {
        if (!TryParseByteComponent(red, out float r)
            || !TryParseByteComponent(green, out float g)
            || !TryParseByteComponent(blue, out float b)
            || !TryParseOptionalByteComponent(alpha, Value.A, out float a))
        {
            HasValidationError = true;
            return false;
        }

        SetValue(new ColorValue(r, g, b, a, Value.ColorSpace));
        return true;
    }

    public bool TrySetRgbFloatText(string red, string green, string blue, string alpha)
    {
        if (!TryParseFloatComponent(red, out float r)
            || !TryParseFloatComponent(green, out float g)
            || !TryParseFloatComponent(blue, out float b)
            || !TryParseOptionalFloatComponent(alpha, Value.A, out float a))
        {
            HasValidationError = true;
            return false;
        }

        SetValue(new ColorValue(r, g, b, a, Value.ColorSpace));
        return true;
    }

    public bool TrySetHsvText(string hue, string saturation, string value, string alpha)
    {
        if (!TryParseFloatComponent(hue, out float h)
            || !TryParseFloatComponent(saturation, out float s)
            || !TryParseFloatComponent(value, out float v)
            || !TryParseOptionalFloatComponent(alpha, Value.A, out float a))
        {
            HasValidationError = true;
            return false;
        }

        SetValue(ColorSpaceConverter.HsvToRgb(new HsvColor(h, s, v, a), Value.ColorSpace));
        return true;
    }

    public void SetInvalidText(string hexText)
    {
        HexText = hexText ?? string.Empty;
        HasValidationError = true;
    }

    private void SetValue(ColorValue value, bool raiseEvent)
    {
        ColorValue previous = Value;
        Value = value;
        HasValidationError = false;
        RefreshText();
        if (raiseEvent && previous != Value)
        {
            ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(previous, Value));
        }
    }

    private void RefreshText()
    {
        HexText = ColorFormatter.Format(Value, HexFormat);
        RedText = ToByteText(Value.R);
        GreenText = ToByteText(Value.G);
        BlueText = ToByteText(Value.B);
        AlphaText = ToFloatText(Value.A);

        HsvColor hsv = ColorSpaceConverter.RgbToHsv(Value);
        HueText = ToFloatText(hsv.H);
        SaturationText = ToFloatText(hsv.S);
        ValueText = ToFloatText(hsv.V);
    }

    private static string ToByteText(float value)
        => Math.Clamp((int)MathF.Round(Math.Clamp(value, 0f, 1f) * byte.MaxValue), byte.MinValue, byte.MaxValue).ToString(CultureInfo.InvariantCulture);

    private static string ToFloatText(float value)
        => value.ToString("R", CultureInfo.InvariantCulture);

    private static bool TryParseByteComponent(string text, out float value)
    {
        value = 0f;
        if (!float.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
        {
            return false;
        }

        value = Math.Clamp(parsed, byte.MinValue, byte.MaxValue) / byte.MaxValue;
        return true;
    }

    private static bool TryParseOptionalByteComponent(string text, float fallback, out float value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = fallback;
            return true;
        }

        return TryParseByteComponent(text, out value);
    }

    private static bool TryParseFloatComponent(string text, out float value)
        => float.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    private static bool TryParseOptionalFloatComponent(string text, float fallback, out float value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = fallback;
            return true;
        }

        return TryParseFloatComponent(text, out value);
    }
}