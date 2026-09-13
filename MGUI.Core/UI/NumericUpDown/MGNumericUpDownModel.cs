using System.Globalization;

namespace MGUI.Core.UI.NumericUpDown;

internal sealed class MGNumericUpDownModel
{
    public double Minimum { get; private set; }
    public double Maximum { get; private set; }
    public double Value { get; private set; }
    public double Increment { get; private set; }
    public int DecimalPlaces { get; private set; }
    public string FormatString { get; private set; }
    public bool IsAtMinimum => Value <= Minimum;
    public bool IsAtMaximum => Value >= Maximum;

    public MGNumericUpDownModel(double minimum = 0, double maximum = 100, double value = 0, double increment = 1,
        int decimalPlaces = 0, string formatString = null)
    {
        ValidateRange(minimum, maximum);
        ValidateIncrement(increment);
        ValidateDecimalPlaces(decimalPlaces);

        Minimum = minimum;
        Maximum = maximum;
        Increment = increment;
        DecimalPlaces = decimalPlaces;
        FormatString = formatString;
        Value = CoerceValue(value, Minimum, Maximum, DecimalPlaces);
    }

    public static void ValidateRange(double minimum, double maximum)
    {
        if (minimum > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(minimum), minimum, $"{nameof(minimum)} cannot be greater than {nameof(maximum)}.");
        }
    }

    public static void ValidateIncrement(double increment)
    {
        if (increment <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(increment), increment, $"{nameof(increment)} must be greater than zero.");
        }
    }

    public static void ValidateDecimalPlaces(int decimalPlaces)
    {
        if (decimalPlaces < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(decimalPlaces), decimalPlaces, $"{nameof(decimalPlaces)} cannot be negative.");
        }
    }

    public static double RoundValue(double value, int decimalPlaces)
    {
        ValidateDecimalPlaces(decimalPlaces);
        return Math.Round(value, decimalPlaces, MidpointRounding.AwayFromZero);
    }

    public static double CoerceValue(double value, double minimum, double maximum, int decimalPlaces)
    {
        ValidateRange(minimum, maximum);
        double rounded = RoundValue(value, decimalPlaces);
        return Math.Clamp(rounded, minimum, maximum);
    }

    public bool SetRange(double minimum, double maximum)
    {
        ValidateRange(minimum, maximum);
        if (Minimum == minimum && Maximum == maximum)
        {
            return false;
        }

        Minimum = minimum;
        Maximum = maximum;
        Value = CoerceValue(Value, Minimum, Maximum, DecimalPlaces);
        return true;
    }

    public bool SetIncrement(double increment)
    {
        ValidateIncrement(increment);
        if (Increment == increment)
        {
            return false;
        }

        Increment = increment;
        return true;
    }

    public bool SetDecimalPlaces(int decimalPlaces)
    {
        ValidateDecimalPlaces(decimalPlaces);
        if (DecimalPlaces == decimalPlaces)
        {
            return false;
        }

        DecimalPlaces = decimalPlaces;
        Value = CoerceValue(Value, Minimum, Maximum, DecimalPlaces);
        return true;
    }

    public bool SetFormatString(string formatString)
    {
        if (FormatString == formatString)
        {
            return false;
        }

        FormatString = formatString;
        return true;
    }

    public bool SetValue(double value)
    {
        double coerced = CoerceValue(value, Minimum, Maximum, DecimalPlaces);
        if (Value == coerced)
        {
            return false;
        }

        Value = coerced;
        return true;
    }

    public bool TryStep(int direction)
    {
        if (direction == 0)
        {
            return false;
        }

        return SetValue(Value + Increment * direction);
    }

    public bool TryIncrease() => TryStep(1);
    public bool TryDecrease() => TryStep(-1);

    public bool TryParseText(string text, out double value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = default;
            return false;
        }

        bool parsed = double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out double parsedValue);
        value = parsed ? CoerceValue(parsedValue, Minimum, Maximum, DecimalPlaces) : default;
        return parsed;
    }

    public bool TryApplyText(string text, out double value)
    {
        if (!TryParseText(text, out double parsedValue))
        {
            value = Value;
            return false;
        }

        value = parsedValue;
        SetValue(parsedValue);
        return true;
    }

    public string FormatValue()
    {
        if (!string.IsNullOrWhiteSpace(FormatString))
        {
            return Value.ToString(FormatString, CultureInfo.InvariantCulture);
        }

        return Value.ToString($"F{DecimalPlaces}", CultureInfo.InvariantCulture);
    }

    public string FormatValue(double value)
    {
        double previousValue = Value;
        try
        {
            Value = CoerceValue(value, Minimum, Maximum, DecimalPlaces);
            return FormatValue();
        }
        finally
        {
            Value = previousValue;
        }
    }
}