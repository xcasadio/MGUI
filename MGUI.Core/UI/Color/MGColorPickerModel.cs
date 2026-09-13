namespace MGUI.Core.UI;

public sealed class MGColorPickerModel
{
    private ColorValue _editStartValue;
    private ColorSpaceMode _displayColorSpace = ColorSpaceMode.Srgb;

    public ColorValue Value { get; private set; }

    public ColorValue CommittedValue { get; private set; }

    public HsvColor HsvValue { get; private set; }

    public ColorValue DisplayValue => GetDisplayValue();
    public ColorSpaceMode StorageColorSpace => Value.ColorSpace;
    public float? TemperatureKelvin { get; private set; }
    public ColorSpaceMode DisplayColorSpace
    {
        get => _displayColorSpace;
        set
        {
            if (_displayColorSpace != value)
            {
                _displayColorSpace = value;
                RefreshDisplayState();
            }
        }
    }

    public bool StoreAsLinear
    {
        get => StorageColorSpace == ColorSpaceMode.Linear;
        set
        {
            var target = value ? ColorSpaceMode.Linear : ColorSpaceMode.Srgb;
            if (StorageColorSpace != target)
            {
                SetValue(ColorSpaceConverter.Convert(Value, target));
            }
        }
    }

    public bool DisplayAsSrgb
    {
        get => DisplayColorSpace == ColorSpaceMode.Srgb;
        set => DisplayColorSpace = value ? ColorSpaceMode.Srgb : StorageColorSpace;
    }

    public bool IsDisplayDifferentFromStorage => DisplayColorSpace != StorageColorSpace;
    public float Intensity => ColorHdrHelper.GetIntensity(Value);
    public ColorValue BaseColor => ColorHdrHelper.GetNormalizedBaseColor(Value);
    public ColorPickerConstraints Constraints { get; }
    public MGColorTextInputModel TextInput { get; }
    public bool IsEditing { get; private set; }
    public ColorEditCommitMode CommitMode { get; set; } = ColorEditCommitMode.Live;
    public IColorEditTransaction EditTransaction { get; set; } = NoOpColorEditTransaction.Instance;

    public event EventHandler<ColorValueChangingEventArgs> ValueChanging;
    public event EventHandler<ColorValueChangedEventArgs> ValueChanged;
    public event EventHandler EditStarted;
    public event EventHandler<ColorValueChangedEventArgs> EditCommitted;
    public event EventHandler EditCancelled;

    public MGColorPickerModel()
        : this(new ColorValue(1f, 1f, 1f, 1f), new ColorPickerConstraints())
    {
    }

    public MGColorPickerModel(ColorValue value, ColorPickerConstraints constraints, IColorEditTransaction editTransaction = null)
    {
        Constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
        EditTransaction = editTransaction ?? NoOpColorEditTransaction.Instance;
        TextInput = new MGColorTextInputModel();
        SetValue(value, raiseEvent: false);
    }

    public void SetValue(ColorValue value)
    {
        if (IsEditing)
        {
            EditTransaction.Cancel();
        }

        var previous = CommittedValue;
        var actual = ApplyValue(value);
        CommittedValue = actual;
        _editStartValue = actual;
        IsEditing = false;

        if (previous != actual)
        {
            ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(previous, actual));
        }
    }

    public void SetHsv(float hue, float saturation, float value)
        => SetHsv(new HsvColor(hue, saturation, value, Value.A));

    public void SetHsv(HsvColor hsv)
    {
        HsvColor actualHsv = new(
            ColorSpaceConverter.NormalizeHue(hsv.H),
            Math.Clamp(hsv.S, 0f, 1f),
            Math.Clamp(hsv.V, 0f, Constraints.AllowHdr ? Constraints.MaxIntensity : 1f),
            Math.Clamp(hsv.A, 0f, 1f));

        PreviewDisplayValue(ColorSpaceConverter.HsvToRgb(actualHsv, DisplayColorSpace));
    }

    public void SetHue(float hue)
        => SetHsv(new HsvColor(hue, HsvValue.S, HsvValue.V, Value.A));

    public void SetSaturationValue(float saturation, float value)
        => SetHsv(new HsvColor(HsvValue.H, saturation, value, Value.A));

    public void SetAlpha(float alpha)
        => PreviewValue(Value.WithAlpha(Math.Clamp(alpha, 0f, 1f)));

    public void SetIntensity(float intensity)
        => PreviewValue(ColorHdrHelper.WithIntensity(Value, Constraints.ClampIntensity(intensity)));

    public void SetTemperatureKelvin(float kelvin, float minKelvin = ColorTemperatureConverter.DefaultMinKelvin, float maxKelvin = ColorTemperatureConverter.DefaultMaxKelvin)
    {
        var actualKelvin = ColorTemperatureConverter.ClampKelvin(kelvin, minKelvin, maxKelvin);
        TemperatureKelvin = actualKelvin;
        PreviewDisplayValue(ColorTemperatureConverter.KelvinToRgb(actualKelvin, DisplayColorSpace, minKelvin, maxKelvin).WithAlpha(Value.A));
    }

    public ColorValue GetToneMappedPreview()
        => ColorHdrHelper.ToneMapReinhard(Value);

    public string GetQuickInfoText(ColorValueFormat hexFormat = ColorValueFormat.HexRgba)
    {
        var display = DisplayValue;
        var hsv = ColorSpaceConverter.RgbToHsv(display);
        return $"{ColorFormatter.Format(display, hexFormat)}  {ColorFormatter.Format(display, ColorValueFormat.RgbaFloat)}  HSV({MathF.Round(hsv.H)}, {hsv.S:0.###}, {hsv.V:0.###})";
    }

    public void BeginEdit()
    {
        if (IsEditing)
        {
            return;
        }

        _editStartValue = CommittedValue;
        IsEditing = true;
        EditTransaction.Begin(_editStartValue);
        EditStarted?.Invoke(this, EventArgs.Empty);
    }

    public void PreviewValue(ColorValue value)
    {
        if (!IsEditing)
        {
            BeginEdit();
        }

        var previousPreview = Value;
        var actual = ApplyValue(value);
        if (previousPreview == actual)
        {
            return;
        }

        EditTransaction.Preview(actual);
        ValueChanging?.Invoke(this, new ColorValueChangingEventArgs(_editStartValue, actual));
        if (CommitMode == ColorEditCommitMode.Live)
        {
            var previousCommit = CommittedValue;
            CommittedValue = actual;
            ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(previousCommit, actual));
        }
    }

    public void PreviewDisplayValue(ColorValue displayValue)
        => PreviewValue(ColorSpaceConverter.Convert(displayValue, StorageColorSpace));

    public ColorValue GetDisplayValue()
        => ColorSpaceConverter.Convert(Value, DisplayColorSpace);

    public ColorValue GetValueForColorSpace(ColorSpaceMode colorSpace)
        => ColorSpaceConverter.Convert(Value, colorSpace);

    public string GetDisplayText(ColorValueFormat format, bool includeColorSpace = true)
    {
        var text = ColorFormatter.Format(GetDisplayValue(), format);
        return includeColorSpace ? $"{text} [{DisplayColorSpace}]" : text;
    }

    public bool CommitEdit()
    {
        if (!IsEditing)
        {
            return false;
        }

        var previousCommit = CommittedValue;
        var finalValue = Value;
        if (CommitMode != ColorEditCommitMode.Live && previousCommit != finalValue)
        {
            CommittedValue = finalValue;
            ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(previousCommit, finalValue));
        }

        IsEditing = false;
        EditTransaction.Commit(finalValue);
        EditCommitted?.Invoke(this, new ColorValueChangedEventArgs(_editStartValue, finalValue));
        _editStartValue = finalValue;
        return true;
    }

    public bool CancelEdit()
    {
        if (!IsEditing)
        {
            return false;
        }

        var currentValue = Value;
        ApplyValue(_editStartValue);
        if (CommitMode == ColorEditCommitMode.Live && currentValue != _editStartValue)
        {
            CommittedValue = _editStartValue;
            ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(currentValue, _editStartValue));
        }

        IsEditing = false;
        EditTransaction.Cancel();
        EditCancelled?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void SetValue(ColorValue value, bool raiseEvent)
    {
        var previous = Value;
        var actual = ApplyValue(value);
        CommittedValue = actual;
        _editStartValue = actual;

        if (raiseEvent && previous != actual)
        {
            ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(previous, actual));
        }
    }

    private ColorValue ApplyValue(ColorValue value)
    {
        var actual = Constraints.Apply(value);
        Value = actual;
        RefreshDisplayState();
        return actual;
    }

    private void RefreshDisplayState()
    {
        var displayValue = GetDisplayValue();
        HsvValue = ColorSpaceConverter.RgbToHsv(displayValue);
        TextInput.SetValue(displayValue);
    }
}