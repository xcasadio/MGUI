namespace MGUI.Core.UI;

public sealed class MGColorFieldModel
{
    private ColorValue? _defaultValue;
    private bool _allowNull;
    private bool _isMixed;

    public ColorValue? Value { get; private set; }

    public ColorValue? DefaultValue
    {
        get => _defaultValue;
        set => _defaultValue = value;
    }

    public bool AllowNull
    {
        get => _allowNull;
        set
        {
            _allowNull = value;
            if (!_allowNull && Value == null)
            {
                SetValue(DefaultValue ?? new ColorValue(0f, 0f, 0f, 1f));
            }
        }
    }

    public bool IsMixed
    {
        get => _isMixed;
        set => _isMixed = value;
    }

    public bool IsReadOnly { get; set; }

    public event EventHandler<ColorFieldValueChangedEventArgs> ValueChanged;

    public MGColorFieldModel()
        : this(new ColorValue(1f, 1f, 1f, 1f))
    {
    }

    public MGColorFieldModel(ColorValue? value)
    {
        _allowNull = value == null;
        Value = value;
    }

    public bool TrySetValue(ColorValue? value)
    {
        if (IsReadOnly)
        {
            return false;
        }

        if (value == null && !AllowNull)
        {
            return false;
        }

        SetValue(value);
        return true;
    }

    public void SetMixedValue(ColorValue? displayedValue = null)
    {
        Value = displayedValue;
        _isMixed = true;
    }

    public void ClearMixedValue()
        => _isMixed = false;

    public void SetValueFromSource(ColorValue? value)
        => SetValue(value);

    public bool ResetToDefault()
    {
        if (DefaultValue == null || IsReadOnly)
        {
            return false;
        }

        SetValue(DefaultValue.Value);
        return true;
    }

    public string GetDisplayText(ColorValueFormat format)
    {
        if (IsMixed)
        {
            return "Mixed";
        }

        return Value.HasValue ? ColorFormatter.Format(Value.Value, format) : "Null";
    }

    private void SetValue(ColorValue? value)
    {
        ColorValue? previous = Value;
        bool wasMixed = _isMixed;
        Value = value;
        _isMixed = false;
        if (wasMixed || previous != Value)
        {
            ValueChanged?.Invoke(this, new ColorFieldValueChangedEventArgs(previous, Value));
        }
    }
}