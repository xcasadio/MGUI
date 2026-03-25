using MGUI.Core.UI.NumericUpDown;

namespace MGUI.Tests.Architecture;

public class NumericUpDownModelTests
{
    [Fact]
    public void Constructor_CoercesInitialValueIntoRange()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 99, increment: 1, decimalPlaces: 0);

        Assert.Equal(10, model.Value);
    }

    [Fact]
    public void Constructor_RoundsInitialValueToConfiguredDecimalPlaces()
    {
        MGNumericUpDownModel model = new(minimum: -10, maximum: 10, value: 1.236, increment: 0.1, decimalPlaces: 2);

        Assert.Equal(1.24, model.Value);
    }

    [Fact]
    public void SetRange_CoercesExistingValue()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 7, increment: 1, decimalPlaces: 0);

        bool changed = model.SetRange(0, 5);

        Assert.True(changed);
        Assert.Equal(5, model.Value);
    }

    [Fact]
    public void SetRange_InvalidRange_Throws()
    {
        MGNumericUpDownModel model = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => model.SetRange(10, 5));
    }

    [Fact]
    public void SetIncrement_RejectsNonPositiveValues()
    {
        MGNumericUpDownModel model = new();

        Assert.Throws<ArgumentOutOfRangeException>(() => model.SetIncrement(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => model.SetIncrement(-1));
    }

    [Fact]
    public void SetDecimalPlaces_RoundsCurrentValue()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 1.234, increment: 0.1, decimalPlaces: 3);

        bool changed = model.SetDecimalPlaces(1);

        Assert.True(changed);
        Assert.Equal(1.2, model.Value);
    }

    [Fact]
    public void SetValue_CoercesAndRounds()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 0, increment: 1, decimalPlaces: 1);

        bool changed = model.SetValue(5.55);

        Assert.True(changed);
        Assert.Equal(5.6, model.Value);
    }

    [Fact]
    public void TryStep_UsesIncrementAndClampsAtMaximum()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 2, value: 1.5, increment: 0.5, decimalPlaces: 1);

        bool firstChanged = model.TryStep(1);
        bool secondChanged = model.TryStep(1);

        Assert.True(firstChanged);
        Assert.False(secondChanged);
        Assert.Equal(2.0, model.Value);
    }

    [Fact]
    public void TryParseText_ReturnsFalseForInvalidInput()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 4, increment: 1, decimalPlaces: 0);

        bool parsed = model.TryParseText("abc", out double parsedValue);

        Assert.False(parsed);
        Assert.Equal(0, parsedValue);
        Assert.Equal(4, model.Value);
    }

    [Fact]
    public void TryParseText_ParsesInvariantNumericText()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 0, increment: 0.5, decimalPlaces: 2);

        bool parsed = model.TryParseText("1.236", out double parsedValue);

        Assert.True(parsed);
        Assert.Equal(1.24, parsedValue);
    }

    [Fact]
    public void TryApplyText_UpdatesValueWhenParsingSucceeds()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 2, increment: 1, decimalPlaces: 1);

        bool applied = model.TryApplyText("3.44", out double parsedValue);

        Assert.True(applied);
        Assert.Equal(3.4, parsedValue);
        Assert.Equal(3.4, model.Value);
    }

    [Fact]
    public void TryApplyText_LeavesValueUnchangedWhenParsingFails()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 2, increment: 1, decimalPlaces: 1);

        bool applied = model.TryApplyText("-", out double parsedValue);

        Assert.False(applied);
        Assert.Equal(2, parsedValue);
        Assert.Equal(2, model.Value);
    }

    [Fact]
    public void FormatValue_UsesDecimalPlacesByDefault()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 1.2, increment: 1, decimalPlaces: 3);

        string formatted = model.FormatValue();

        Assert.Equal("1.200", formatted);
    }

    [Fact]
    public void FormatValue_UsesExplicitFormatStringWhenProvided()
    {
        MGNumericUpDownModel model = new(minimum: 0, maximum: 10, value: 1.2, increment: 1, decimalPlaces: 3, formatString: "0.##");

        string formatted = model.FormatValue();

        Assert.Equal("1.2", formatted);
    }
}