using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Input.Mouse;
using System.Diagnostics;

namespace MGUI.Core.UI;

public class MGColorPicker : MGElement
{
    private enum DragTarget
    {
        None,
        SaturationValue,
        Hue,
        Alpha,
        Intensity,
        Temperature,
    }

    internal enum KeyboardNavigationTarget
    {
        SaturationValue,
        Hue,
        Alpha,
        Intensity,
        Temperature,
    }

    private DragTarget _activeDragTarget = DragTarget.None;
    private KeyboardNavigationTarget _activeKeyboardTarget = KeyboardNavigationTarget.SaturationValue;
    private IColorPickService _colorPickService = UnsupportedColorPickService.Instance;
    private Color[] _hueSliderCache;
    private int _hueSliderCacheLength;
    private ColorSpaceMode _hueSliderCacheColorSpace;
    private Color[] TemperatureSliderCache;
    private int TemperatureSliderCacheLength;
    private float TemperatureSliderCacheMinKelvin;
    private float TemperatureSliderCacheMaxKelvin;
    private ColorSpaceMode TemperatureSliderCacheColorSpace;

    public MGColorPickerModel Model { get; }
    public MGColorTextInputModel TextInput => Model.TextInput;

    public ColorValue Value
    {
        get => Model.Value;
        set => Model.SetValue(value);
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorValue _previousValue;
    public ColorValue PreviousValue
    {
        get => _previousValue;
        set
        {
            if (_previousValue != value)
            {
                _previousValue = value;
                NPC(nameof(PreviousValue));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _showAlpha;
    public bool ShowAlpha
    {
        get => _showAlpha;
        set
        {
            if (_showAlpha != value)
            {
                _showAlpha = value;
                LayoutChanged(this, true);
                NPC(nameof(ShowAlpha));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _showTextInput;
    public bool ShowTextInput
    {
        get => _showTextInput;
        set
        {
            if (_showTextInput != value)
            {
                _showTextInput = value;
                LayoutChanged(this, true);
                NPC(nameof(ShowTextInput));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorPickerMode _pickerMode;
    public ColorPickerMode PickerMode
    {
        get => _pickerMode;
        set
        {
            if (_pickerMode != value)
            {
                _pickerMode = value;
                NPC(nameof(PickerMode));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private ColorValueFormat _displayFormat;
    public ColorValueFormat DisplayFormat
    {
        get => _displayFormat;
        set
        {
            if (_displayFormat != value)
            {
                _displayFormat = value;
                TextInput.HexFormat = value;
                TextInput.SetValue(Value);
                NPC(nameof(DisplayFormat));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _saturationValueSize;
    public int SaturationValueSize
    {
        get => _saturationValueSize;
        set
        {
            int actual = Math.Max(0, value);
            if (_saturationValueSize != actual)
            {
                _saturationValueSize = actual;
                LayoutChanged(this, true);
                NPC(nameof(SaturationValueSize));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _sliderThickness;
    public int SliderThickness
    {
        get => _sliderThickness;
        set
        {
            int actual = Math.Max(0, value);
            if (_sliderThickness != actual)
            {
                _sliderThickness = actual;
                LayoutChanged(this, true);
                NPC(nameof(SliderThickness));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _previewWidth;
    public int PreviewWidth
    {
        get => _previewWidth;
        set
        {
            int actual = Math.Max(0, value);
            if (_previewWidth != actual)
            {
                _previewWidth = actual;
                LayoutChanged(this, true);
                NPC(nameof(PreviewWidth));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _previewHeight;
    public int PreviewHeight
    {
        get => _previewHeight;
        set
        {
            int actual = Math.Max(0, value);
            if (_previewHeight != actual)
            {
                _previewHeight = actual;
                LayoutChanged(this, true);
                NPC(nameof(PreviewHeight));
            }
        }
    }

    public ColorPickerConstraints Constraints => Model.Constraints;

    public ColorEditCommitMode CommitMode
    {
        get => Model.CommitMode;
        set
        {
            if (Model.CommitMode != value)
            {
                Model.CommitMode = value;
                NPC(nameof(CommitMode));
            }
        }
    }

    public ColorSpaceMode DisplayColorSpace
    {
        get => Model.DisplayColorSpace;
        set
        {
            if (Model.DisplayColorSpace != value)
            {
                Model.DisplayColorSpace = value;
                NPC(nameof(DisplayColorSpace));
                NPC(nameof(DisplayAsSrgb));
                NPC(nameof(IsDisplayDifferentFromStorage));
            }
        }
    }

    public bool StoreAsLinear
    {
        get => Model.StoreAsLinear;
        set
        {
            if (Model.StoreAsLinear != value)
            {
                Model.StoreAsLinear = value;
                NPC(nameof(StoreAsLinear));
                NPC(nameof(IsDisplayDifferentFromStorage));
            }
        }
    }

    public bool DisplayAsSrgb
    {
        get => Model.DisplayAsSrgb;
        set
        {
            if (Model.DisplayAsSrgb != value)
            {
                Model.DisplayAsSrgb = value;
                NPC(nameof(DisplayAsSrgb));
                NPC(nameof(DisplayColorSpace));
                NPC(nameof(IsDisplayDifferentFromStorage));
            }
        }
    }

    public bool IsDisplayDifferentFromStorage => Model.IsDisplayDifferentFromStorage;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _isHdr;
    public bool IsHdr
    {
        get => _isHdr;
        set
        {
            if (_isHdr != value)
            {
                _isHdr = value;
                Model.Constraints.AllowHdr = value;
                if (value)
                {
                    Model.Constraints.MaxChannelValue = Math.Max(Model.Constraints.MaxChannelValue, MaxIntensity);
                }

                NPC(nameof(IsHdr));
            }
        }
    }

    public bool ShowIntensity { get; set; }
    public bool UseExposureSlider { get; set; }
    public bool ShowToneMappedPreview { get; set; }
    public bool ShowTemperature { get; set; }
    public bool ShowLightDarkPreview { get; set; }
    public bool ShowContrastWarning { get; set; }
    public ColorValue ContrastTextColor { get; set; } = new(0f, 0f, 0f, 1f);
    public float MinimumContrastRatio { get; set; } = ColorContrastHelper.DefaultMinimumTextContrastRatio;
    public float MinKelvin { get; set; } = ColorTemperatureConverter.DefaultMinKelvin;
    public float MaxKelvin { get; set; } = ColorTemperatureConverter.DefaultMaxKelvin;
    public float MinIntensity
    {
        get => Model.Constraints.MinIntensity;
        set => Model.Constraints.MinIntensity = value;
    }

    public float MaxIntensity
    {
        get => Model.Constraints.MaxIntensity;
        set
        {
            Model.Constraints.MaxIntensity = value;
            if (IsHdr)
            {
                Model.Constraints.MaxChannelValue = Math.Max(Model.Constraints.MaxChannelValue, value);
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _showEyeDropper;
    public bool ShowEyeDropper
    {
        get => _showEyeDropper;
        set
        {
            if (_showEyeDropper != value)
            {
                _showEyeDropper = value;
                NPC(nameof(ShowEyeDropper));
            }
        }
    }

    public IColorPickService ColorPickService
    {
        get => _colorPickService;
        set
        {
            IColorPickService actual = value ?? UnsupportedColorPickService.Instance;
            if (ReferenceEquals(_colorPickService, actual))
            {
                return;
            }

            _colorPickService.ColorPicked -= OnColorPicked;
            _colorPickService.ColorPickCancelled -= OnColorPickCancelled;
            _colorPickService = actual;
            _colorPickService.ColorPicked += OnColorPicked;
            _colorPickService.ColorPickCancelled += OnColorPickCancelled;
            NPC(nameof(ColorPickService));
            NPC(nameof(IsEyeDropperAvailable));
        }
    }

    public bool IsEyeDropperAvailable => ShowEyeDropper && ColorPickService.IsSupported;

    public int ControlSpacing { get; set; } = 8;
    public int TextInputHeight { get; set; } = 22;
    public int BorderThickness { get; set; } = 1;
    public int CheckerboardCellSize { get; set; } = 4;
    public Color BorderColor { get; set; } = Color.Black;
    public Color ThumbColor { get; set; } = Color.White;
    public Color ThumbBorderColor { get; set; } = Color.Black;
    public Color CheckerboardLightColor { get; set; } = new(210, 210, 210);
    public Color CheckerboardDarkColor { get; set; } = new(130, 130, 130);

    public event EventHandler<ColorValueChangedEventArgs> ValueChanged;
    public event EventHandler<ColorValueChangingEventArgs> ValueChanging;
    public event EventHandler EditStarted;
    public event EventHandler<ColorValueChangedEventArgs> EditCommitted;
    public event EventHandler EditCancelled;

    public MGColorPicker(MGWindow window)
        : this(window, new ColorPickerOptions())
    {
    }

    public MGColorPicker(MGWindow window, ColorPickerOptions options)
        : base(window, MGElementType.ColorPicker)
    {
        options ??= new ColorPickerOptions();
        ColorPickerConstraints constraints = options.Constraints ?? new ColorPickerConstraints();
        constraints.AllowHdr = constraints.AllowHdr || options.IsHdr;
        constraints.MinIntensity = options.MinIntensity;
        constraints.MaxIntensity = options.MaxIntensity;
        if (options.IsHdr)
        {
            constraints.MaxChannelValue = Math.Max(constraints.MaxChannelValue, options.MaxIntensity);
        }

        ColorValue initialValue = ColorSpaceConverter.Convert(options.InitialValue, options.StorageColorSpace);
        Model = new MGColorPickerModel(initialValue, constraints, options.EditTransaction)
        {
            DisplayColorSpace = options.DisplayColorSpace,
        };
        using (BeginInitializing())
        {
            PreviousValue = options.InitialValue;
            ShowAlpha = options.ShowAlpha;
            IsHdr = options.IsHdr;
            ShowIntensity = options.ShowIntensity;
            UseExposureSlider = options.UseExposureSlider;
            ShowToneMappedPreview = options.ShowToneMappedPreview;
            ShowTemperature = options.ShowTemperature;
            ShowLightDarkPreview = options.ShowLightDarkPreview;
            ShowContrastWarning = options.ShowContrastWarning;
            ContrastTextColor = options.ContrastTextColor;
            MinimumContrastRatio = options.MinimumContrastRatio;
            MinKelvin = options.MinKelvin;
            MaxKelvin = options.MaxKelvin;
            ShowEyeDropper = options.ShowEyeDropper;
            ColorPickService = options.ColorPickService;
            ShowTextInput = options.ShowTextInput;
            PickerMode = options.PickerMode;
            CommitMode = options.CommitMode;
            DisplayFormat = options.DisplayFormat;
            SaturationValueSize = 160;
            SliderThickness = 18;
            PreviewWidth = 72;
            PreviewHeight = 36;
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            IsFocusable = true;

            Model.ValueChanging += (sender, e) =>
            {
                NPC(nameof(Value));
                ValueChanging?.Invoke(this, e);
            };
            Model.ValueChanged += (sender, e) =>
            {
                NPC(nameof(Value));
                if (!Model.IsEditing)
                {
                    PreviousValue = e.NewValue;
                }

                ValueChanged?.Invoke(this, e);
            };
            Model.EditStarted += (sender, e) => EditStarted?.Invoke(this, e);
            Model.EditCommitted += (sender, e) =>
            {
                PreviousValue = e.NewValue;
                EditCommitted?.Invoke(this, e);
            };
            Model.EditCancelled += (sender, e) => EditCancelled?.Invoke(this, e);

            MouseHandler.DragStartCondition = DragStartCondition.MousePressed;
            MouseHandler.LMBPressedInside += (sender, e) =>
            {
                e.SetHandledBy(this, false);
                Point layoutPoint = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
                Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, GetDesiredSize());
                if (IsEyeDropperAvailable && GetEyeDropperButtonBounds(bounds).Contains(layoutPoint))
                {
                    _ = BeginEyeDropperPick();
                    return;
                }

                Model.BeginEdit();
                SetValueFromScreenPosition(e.Position, false);
            };
            MouseHandler.DragStart += (sender, e) =>
            {
                if (e.IsLMB)
                {
                    _activeDragTarget = HitTest(ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position));
                    e.SetHandledBy(this, false);
                }
            };
            MouseHandler.Dragged += (sender, e) =>
            {
                if (e.IsLMB && _activeDragTarget != DragTarget.None)
                {
                    SetValueFromScreenPosition(e.Position, true);
                }
            };
            MouseHandler.DragEnd += (sender, e) =>
            {
                if (e.IsLMB)
                {
                    if (CommitMode == ColorEditCommitMode.Live || CommitMode == ColorEditCommitMode.OnMouseRelease)
                    {
                        CommitEdit();
                    }

                    _activeDragTarget = DragTarget.None;
                }
            };
        }
    }

    public override bool TryHandleNavigationAction(UINavigationAction action)
    {
        if (action is UINavigationAction.MoveNext or UINavigationAction.MovePrevious)
        {
            _activeKeyboardTarget = GetNextKeyboardNavigationTarget(_activeKeyboardTarget, ShowAlpha, ShowIntensity, ShowTemperature, action);
            return true;
        }

        if (TryApplyKeyboardNavigation(action, _activeKeyboardTarget, ShowAlpha, ShowIntensity, ShowTemperature))
        {
            return true;
        }

        return action switch
        {
            UINavigationAction.Submit when CommitMode == ColorEditCommitMode.ExplicitOkCancel && Model.IsEditing => CommitEdit(),
            UINavigationAction.Cancel when Model.IsEditing => CancelEdit(),
            _ => base.TryHandleNavigationAction(action),
        };
    }

    internal static KeyboardNavigationTarget GetNextKeyboardNavigationTarget(KeyboardNavigationTarget current, bool showAlpha, bool showIntensity, bool showTemperature, UINavigationAction action)
    {
        KeyboardNavigationTarget[] targets = GetAvailableKeyboardTargets(showAlpha, showIntensity, showTemperature);
        int currentIndex = Array.IndexOf(targets, current);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        int delta = action == UINavigationAction.MovePrevious ? -1 : 1;
        int nextIndex = (currentIndex + delta + targets.Length) % targets.Length;
        return targets[nextIndex];
    }

    internal static bool TryApplyKeyboardNavigation(MGColorPickerModel model, KeyboardNavigationTarget target, UINavigationAction action, bool showAlpha, bool showIntensity, bool showTemperature, bool useExposureSlider, float minIntensity, float maxIntensity, float minKelvin, float maxKelvin)
    {
        if (model == null || !IsKeyboardTargetAvailable(target, showAlpha, showIntensity, showTemperature))
        {
            return false;
        }

        const float SmallPercentStep = 0.01f;
        const float LargePercentStep = 0.1f;
        return target switch
        {
            KeyboardNavigationTarget.SaturationValue => TryAdjustSaturationValue(model, action, SmallPercentStep, LargePercentStep),
            KeyboardNavigationTarget.Hue => TryAdjustHue(model, action),
            KeyboardNavigationTarget.Alpha => TryAdjustAlpha(model, action, SmallPercentStep, LargePercentStep),
            KeyboardNavigationTarget.Intensity => TryAdjustIntensity(model, action, useExposureSlider, minIntensity, maxIntensity, SmallPercentStep, LargePercentStep),
            KeyboardNavigationTarget.Temperature => TryAdjustTemperature(model, action, minKelvin, maxKelvin, SmallPercentStep, LargePercentStep),
            _ => false,
        };
    }

    public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness sharedSize)
    {
        sharedSize = new(0);
        int spacing = Math.Max(0, ControlSpacing);
        int width = SaturationValueSize + spacing + SliderThickness + spacing + PreviewWidth;
        int height = SaturationValueSize + (ShowAlpha ? spacing + SliderThickness : 0) + (ShowIntensity ? spacing + SliderThickness : 0) + (ShowTemperature ? spacing + SliderThickness : 0) + (ShowTextInput ? spacing + TextInputHeight : 0);
        return new(width, height, 0, 0);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle layoutBounds)
    {
        Rectangle bounds = ApplyAlignment(layoutBounds, HorizontalAlignment, VerticalAlignment, GetDesiredSize());
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        DrawSaturationValueSquare(DA, GetSaturationValueBounds(bounds));
        DrawHueSlider(DA, GetHueSliderBounds(bounds));
        if (ShowAlpha)
        {
            DrawAlphaSlider(DA, GetAlphaSliderBounds(bounds));
        }

        if (ShowIntensity)
        {
            DrawIntensitySlider(DA, GetIntensitySliderBounds(bounds));
        }

        if (ShowTemperature)
        {
            DrawTemperatureSlider(DA, GetTemperatureSliderBounds(bounds));
        }

        DrawPreview(DA, GetPreviewBounds(bounds));
        if (ShowEyeDropper)
        {
            DrawEyeDropperButton(DA, GetEyeDropperButtonBounds(bounds));
        }

        if (ShowTextInput)
        {
            DrawTextInputStrip(DA, GetTextInputBounds(bounds));
        }
    }

    public void SetHsv(float hue, float saturation, float value)
        => Model.SetHsv(hue, saturation, value);

    public void SetSaturationValue(float saturation, float value)
        => Model.SetSaturationValue(saturation, value);

    public void SetHue(float hue)
        => Model.SetHue(hue);

    public void SetAlpha(float alpha)
        => Model.SetAlpha(alpha);

    public void SetIntensity(float intensity)
        => Model.SetIntensity(intensity);

    public void SetTemperatureKelvin(float kelvin)
        => Model.SetTemperatureKelvin(kelvin, MinKelvin, MaxKelvin);

    public bool BeginEdit()
    {
        bool wasEditing = Model.IsEditing;
        Model.BeginEdit();
        return !wasEditing;
    }

    public bool CommitEdit()
        => Model.CommitEdit();

    public bool CancelEdit()
        => Model.CancelEdit();

    public bool BeginEyeDropperPick()
    {
        if (!IsEyeDropperAvailable)
        {
            return false;
        }

        return ColorPickService.BeginPick(new ColorPickRequest
        {
            PreserveAlpha = !ShowAlpha,
            PickFromMGUIOnly = true,
            PickFromScreen = false,
            OutputColorSpace = Value.ColorSpace,
            CurrentValue = Value,
        });
    }

    internal static ColorValue GetSaturationValueColor(float hue, float saturation, float value, ColorSpaceMode colorSpace)
        => ColorSpaceConverter.HsvToRgb(new HsvColor(hue, Math.Clamp(saturation, 0f, 1f), Math.Clamp(value, 0f, 1f), 1f), colorSpace);

    internal static (float Saturation, float Value) GetSaturationValueFromPoint(Point point, Rectangle bounds)
    {
        float saturation = bounds.Width <= 0 ? 0f : Math.Clamp((point.X - bounds.Left) / (float)bounds.Width, 0f, 1f);
        float value = bounds.Height <= 0 ? 0f : 1f - Math.Clamp((point.Y - bounds.Top) / (float)bounds.Height, 0f, 1f);
        return (saturation, value);
    }

    private Size GetDesiredSize()
    {
        int spacing = Math.Max(0, ControlSpacing);
        return new(
            SaturationValueSize + spacing + SliderThickness + spacing + PreviewWidth,
            SaturationValueSize + (ShowAlpha ? spacing + SliderThickness : 0) + (ShowIntensity ? spacing + SliderThickness : 0) + (ShowTemperature ? spacing + SliderThickness : 0) + (ShowTextInput ? spacing + TextInputHeight : 0));
    }

    private Rectangle GetSaturationValueBounds(Rectangle bounds)
        => new(bounds.X, bounds.Y, SaturationValueSize, SaturationValueSize);

    private Rectangle GetHueSliderBounds(Rectangle bounds)
        => new(bounds.X + SaturationValueSize + Math.Max(0, ControlSpacing), bounds.Y, SliderThickness, SaturationValueSize);

    private Rectangle GetAlphaSliderBounds(Rectangle bounds)
        => new(bounds.X, bounds.Y + SaturationValueSize + Math.Max(0, ControlSpacing), SaturationValueSize, SliderThickness);

    private Rectangle GetIntensitySliderBounds(Rectangle bounds)
    {
        int y = bounds.Y + SaturationValueSize + Math.Max(0, ControlSpacing);
        if (ShowAlpha)
        {
            y += SliderThickness + Math.Max(0, ControlSpacing);
        }

        return new(bounds.X, y, SaturationValueSize, SliderThickness);
    }

    private Rectangle GetTemperatureSliderBounds(Rectangle bounds)
    {
        int y = bounds.Y + SaturationValueSize + Math.Max(0, ControlSpacing);
        if (ShowAlpha)
        {
            y += SliderThickness + Math.Max(0, ControlSpacing);
        }

        if (ShowIntensity)
        {
            y += SliderThickness + Math.Max(0, ControlSpacing);
        }

        return new(bounds.X, y, SaturationValueSize, SliderThickness);
    }

    private Rectangle GetPreviewBounds(Rectangle bounds)
        => new(bounds.X + SaturationValueSize + Math.Max(0, ControlSpacing) + SliderThickness + Math.Max(0, ControlSpacing), bounds.Y, PreviewWidth, PreviewHeight);

    private Rectangle GetEyeDropperButtonBounds(Rectangle bounds)
        => new(GetPreviewBounds(bounds).X, GetPreviewBounds(bounds).Bottom + Math.Max(0, ControlSpacing), PreviewWidth, SliderThickness);

    private Rectangle GetTextInputBounds(Rectangle bounds)
    {
        int y = bounds.Y + SaturationValueSize + (ShowAlpha ? Math.Max(0, ControlSpacing) + SliderThickness : 0) + (ShowIntensity ? Math.Max(0, ControlSpacing) + SliderThickness : 0) + (ShowTemperature ? Math.Max(0, ControlSpacing) + SliderThickness : 0) + Math.Max(0, ControlSpacing);
        return new(bounds.X, y, bounds.Width, TextInputHeight);
    }

    private bool TryApplyKeyboardNavigation(UINavigationAction action, KeyboardNavigationTarget target, bool showAlpha, bool showIntensity, bool showTemperature)
    {
        ColorValue previous = Value;
        bool handled = TryApplyKeyboardNavigation(Model, target, action, showAlpha, showIntensity, showTemperature, UseExposureSlider, MinIntensity, MaxIntensity, MinKelvin, MaxKelvin);
        if (handled && CommitMode != ColorEditCommitMode.ExplicitOkCancel && Value != previous)
        {
            CommitEdit();
        }

        return handled;
    }

    private static KeyboardNavigationTarget[] GetAvailableKeyboardTargets(bool showAlpha, bool showIntensity, bool showTemperature)
    {
        KeyboardNavigationTarget[] all = new[] { KeyboardNavigationTarget.SaturationValue, KeyboardNavigationTarget.Hue, KeyboardNavigationTarget.Alpha, KeyboardNavigationTarget.Intensity, KeyboardNavigationTarget.Temperature };
        int count = 2 + (showAlpha ? 1 : 0) + (showIntensity ? 1 : 0) + (showTemperature ? 1 : 0);
        KeyboardNavigationTarget[] targets = new KeyboardNavigationTarget[count];
        int index = 0;
        foreach (KeyboardNavigationTarget target in all)
        {
            if (IsKeyboardTargetAvailable(target, showAlpha, showIntensity, showTemperature))
            {
                targets[index++] = target;
            }
        }

        return targets;
    }

    private static bool IsKeyboardTargetAvailable(KeyboardNavigationTarget target, bool showAlpha, bool showIntensity, bool showTemperature)
        => target switch
        {
            KeyboardNavigationTarget.Alpha => showAlpha,
            KeyboardNavigationTarget.Intensity => showIntensity,
            KeyboardNavigationTarget.Temperature => showTemperature,
            _ => true,
        };

    private static bool TryAdjustSaturationValue(MGColorPickerModel model, UINavigationAction action, float smallStep, float largeStep)
    {
        float saturation = model.HsvValue.S;
        float value = model.HsvValue.V;
        switch (action)
        {
            case UINavigationAction.MoveLeft:
            case UINavigationAction.Decrement:
                saturation -= smallStep;
                break;
            case UINavigationAction.MoveRight:
            case UINavigationAction.Increment:
                saturation += smallStep;
                break;
            case UINavigationAction.MoveUp:
                value += smallStep;
                break;
            case UINavigationAction.MoveDown:
                value -= smallStep;
                break;
            case UINavigationAction.PageUp:
                value += largeStep;
                break;
            case UINavigationAction.PageDown:
                value -= largeStep;
                break;
            case UINavigationAction.Home:
                saturation = 0f;
                value = 0f;
                break;
            case UINavigationAction.End:
                saturation = 1f;
                value = 1f;
                break;
            default:
                return false;
        }

        model.SetSaturationValue(saturation, value);
        return true;
    }

    private static bool TryAdjustHue(MGColorPickerModel model, UINavigationAction action)
    {
        float hue = model.HsvValue.H;
        switch (action)
        {
            case UINavigationAction.MoveLeft:
            case UINavigationAction.MoveDown:
            case UINavigationAction.Decrement:
                hue -= 1f;
                break;
            case UINavigationAction.MoveRight:
            case UINavigationAction.MoveUp:
            case UINavigationAction.Increment:
                hue += 1f;
                break;
            case UINavigationAction.PageUp:
                hue += 15f;
                break;
            case UINavigationAction.PageDown:
                hue -= 15f;
                break;
            case UINavigationAction.Home:
                hue = 0f;
                break;
            case UINavigationAction.End:
                hue = 360f;
                break;
            default:
                return false;
        }

        model.SetHue(hue);
        return true;
    }

    private static bool TryAdjustAlpha(MGColorPickerModel model, UINavigationAction action, float smallStep, float largeStep)
    {
        if (!TryGetPercentAdjustment(action, smallStep, largeStep, out float? value, out float delta))
        {
            return false;
        }

        model.SetAlpha(value ?? model.Value.A + delta);
        return true;
    }

    private static bool TryAdjustIntensity(MGColorPickerModel model, UINavigationAction action, bool useExposureSlider, float minIntensity, float maxIntensity, float smallStep, float largeStep)
    {
        float min = Math.Min(minIntensity, maxIntensity);
        float max = Math.Max(minIntensity, maxIntensity);
        if (!TryGetPercentAdjustment(action, smallStep, largeStep, out float? percentValue, out float percentDelta))
        {
            return false;
        }

        if (percentValue.HasValue)
        {
            model.SetIntensity(MGColorSlider.GetValueFromPercent(percentValue.Value, min, max));
            return true;
        }

        float currentPercent = useExposureSlider ? GetExposurePercent(model.Intensity, min, max) : MGColorSlider.GetPercentFromValue(model.Intensity, min, max);
        float nextPercent = Math.Clamp(currentPercent + percentDelta, 0f, 1f);
        float nextIntensity = useExposureSlider ? GetIntensityFromExposurePercent(nextPercent, min, max) : MGColorSlider.GetValueFromPercent(nextPercent, min, max);
        model.SetIntensity(nextIntensity);
        return true;
    }

    private static bool TryAdjustTemperature(MGColorPickerModel model, UINavigationAction action, float minKelvin, float maxKelvin, float smallStep, float largeStep)
    {
        if (!TryGetPercentAdjustment(action, smallStep, largeStep, out float? percentValue, out float percentDelta))
        {
            return false;
        }

        float min = Math.Min(minKelvin, maxKelvin);
        float max = Math.Max(minKelvin, maxKelvin);
        float startPercent = percentValue ?? (model.TemperatureKelvin.HasValue ? MGColorSlider.GetPercentFromValue(model.TemperatureKelvin.Value, min, max) : 0.5f);
        float nextPercent = Math.Clamp(startPercent + percentDelta, 0f, 1f);
        model.SetTemperatureKelvin(MGColorSlider.GetValueFromPercent(nextPercent, min, max), min, max);
        return true;
    }

    private static bool TryGetPercentAdjustment(UINavigationAction action, float smallStep, float largeStep, out float? value, out float delta)
    {
        value = null;
        delta = 0f;
        switch (action)
        {
            case UINavigationAction.MoveLeft:
            case UINavigationAction.MoveDown:
            case UINavigationAction.Decrement:
                delta = -smallStep;
                return true;
            case UINavigationAction.MoveRight:
            case UINavigationAction.MoveUp:
            case UINavigationAction.Increment:
                delta = smallStep;
                return true;
            case UINavigationAction.PageDown:
                delta = -largeStep;
                return true;
            case UINavigationAction.PageUp:
                delta = largeStep;
                return true;
            case UINavigationAction.Home:
                value = 0f;
                return true;
            case UINavigationAction.End:
                value = 1f;
                return true;
            default:
                return false;
        }
    }

    private DragTarget HitTest(Point layoutPoint)
    {
        Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, GetDesiredSize());
        if (IsEyeDropperAvailable && GetEyeDropperButtonBounds(bounds).Contains(layoutPoint))
        {
            return DragTarget.None;
        }

        if (GetSaturationValueBounds(bounds).Contains(layoutPoint))
        {
            return DragTarget.SaturationValue;
        }

        if (GetHueSliderBounds(bounds).Contains(layoutPoint))
        {
            return DragTarget.Hue;
        }

        if (ShowAlpha && GetAlphaSliderBounds(bounds).Contains(layoutPoint))
        {
            return DragTarget.Alpha;
        }

        if (ShowIntensity && GetIntensitySliderBounds(bounds).Contains(layoutPoint))
        {
            return DragTarget.Intensity;
        }

        if (ShowTemperature && GetTemperatureSliderBounds(bounds).Contains(layoutPoint))
        {
            return DragTarget.Temperature;
        }

        return DragTarget.None;
    }

    private void SetValueFromScreenPosition(Point screenPosition, bool useActiveTarget)
    {
        Point layoutPoint = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, screenPosition);
        DragTarget target = useActiveTarget && _activeDragTarget != DragTarget.None ? _activeDragTarget : HitTest(layoutPoint);
        Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, GetDesiredSize());
        switch (target)
        {
            case DragTarget.SaturationValue:
                (float saturation, float value) = GetSaturationValueFromPoint(layoutPoint, GetSaturationValueBounds(bounds));
                Model.SetSaturationValue(saturation, value);
                _activeDragTarget = DragTarget.SaturationValue;
                break;
            case DragTarget.Hue:
                float huePercent = MGColorSlider.GetPercentFromPoint(layoutPoint, GetHueSliderBounds(bounds), Orientation.Vertical);
                Model.SetHue(huePercent * 360f);
                _activeDragTarget = DragTarget.Hue;
                break;
            case DragTarget.Alpha:
                float alpha = MGColorSlider.GetPercentFromPoint(layoutPoint, GetAlphaSliderBounds(bounds), Orientation.Horizontal);
                Model.SetAlpha(alpha);
                _activeDragTarget = DragTarget.Alpha;
                break;
            case DragTarget.Intensity:
                float percent = MGColorSlider.GetPercentFromPoint(layoutPoint, GetIntensitySliderBounds(bounds), Orientation.Horizontal);
                Model.SetIntensity(GetIntensityFromPercent(percent));
                _activeDragTarget = DragTarget.Intensity;
                break;
            case DragTarget.Temperature:
                float kelvinPercent = MGColorSlider.GetPercentFromPoint(layoutPoint, GetTemperatureSliderBounds(bounds), Orientation.Horizontal);
                Model.SetTemperatureKelvin(MGColorSlider.GetValueFromPercent(kelvinPercent, MinKelvin, MaxKelvin), MinKelvin, MaxKelvin);
                _activeDragTarget = DragTarget.Temperature;
                break;
        }
    }

    private float GetIntensityFromPercent(float percent)
    {
        float p = Math.Clamp(percent, 0f, 1f);
        if (!UseExposureSlider)
        {
            return MGColorSlider.GetValueFromPercent(p, MinIntensity, MaxIntensity);
        }

        float min = Math.Max(0.0001f, MinIntensity);
        float max = Math.Max(min, MaxIntensity);
        float minExposure = MathF.Log2(min);
        float maxExposure = MathF.Log2(max);
        return MathF.Pow(2f, minExposure + p * (maxExposure - minExposure));
    }

    private void DrawSaturationValueSquare(ElementDrawArgs DA, Rectangle bounds)
    {
        Rectangle content = MGColorPreview.GetContentBounds(bounds, BorderThickness);
        int step = Math.Max(1, Math.Min(4, content.Width));
        for (int y = content.Top; y < content.Bottom; y += step)
        {
            int height = Math.Min(step, content.Bottom - y);
            float value = content.Height <= 1 ? 0f : 1f - (y - content.Top) / (float)(content.Height - 1);
            for (int x = content.Left; x < content.Right; x += step)
            {
                int width = Math.Min(step, content.Right - x);
                float saturation = content.Width <= 1 ? 0f : (x - content.Left) / (float)(content.Width - 1);
                Color color = GetSaturationValueColor(Model.HsvValue.H, saturation, value, Value.ColorSpace).ToXnaColor() * DA.Opacity;
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, y, width, height), color);
            }
        }

        DrawSaturationValueThumb(DA, content);
        DrawRectangleBorder(DA, bounds, BorderColor);
    }

    private void DrawHueSlider(ElementDrawArgs DA, Rectangle bounds)
    {
        Rectangle content = MGColorPreview.GetContentBounds(bounds, BorderThickness);
        Color[] cache = GetHueSliderCache(content.Height);
        for (int y = content.Top; y < content.Bottom; y++)
        {
            DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(content.Left, y, content.Width, 1), cache[y - content.Top] * DA.Opacity);
        }

        DrawVerticalSliderThumb(DA, content, MGColorSlider.GetPercentFromValue(Model.HsvValue.H, 0f, 360f));
        DrawRectangleBorder(DA, bounds, BorderColor);
    }

    private void DrawAlphaSlider(ElementDrawArgs DA, Rectangle bounds)
    {
        Rectangle content = MGColorPreview.GetContentBounds(bounds, BorderThickness);
        DrawCheckerboard(DA, content);
        ColorValue opaqueBase = Value.WithAlpha(1f);
        for (int x = content.Left; x < content.Right; x++)
        {
            float percent = content.Width <= 1 ? 0f : (x - content.Left) / (float)(content.Width - 1);
            Color color = MGColorSlider.GetGradientColor(ColorSliderChannel.Alpha, percent, opaqueBase).ToXnaColor() * DA.Opacity;
            DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, content.Top, 1, content.Height), color);
        }

        DrawHorizontalSliderThumb(DA, content, Value.A);
        DrawRectangleBorder(DA, bounds, BorderColor);
    }

    private void DrawIntensitySlider(ElementDrawArgs DA, Rectangle bounds)
    {
        Rectangle content = MGColorPreview.GetContentBounds(bounds, BorderThickness);
        ColorValue baseColor = Model.BaseColor.WithAlpha(1f);
        for (int x = content.Left; x < content.Right; x++)
        {
            float percent = content.Width <= 1 ? 0f : (x - content.Left) / (float)(content.Width - 1);
            ColorValue hdr = ColorHdrHelper.WithIntensity(baseColor, GetIntensityFromPercent(percent));
            ColorValue preview = ShowToneMappedPreview ? ColorHdrHelper.ToneMapReinhard(hdr) : hdr.ClampLdr();
            DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, content.Top, 1, content.Height), preview.ToXnaColor() * DA.Opacity);
        }

        float currentPercent = UseExposureSlider ? GetExposurePercent(Model.Intensity) : MGColorSlider.GetPercentFromValue(Model.Intensity, MinIntensity, MaxIntensity);
        DrawHorizontalSliderThumb(DA, content, currentPercent);
        DrawRectangleBorder(DA, bounds, IsHdr && Value.IsHdr ? new Color(255, 180, 0) : BorderColor);
    }

    private void DrawTemperatureSlider(ElementDrawArgs DA, Rectangle bounds)
    {
        Rectangle content = MGColorPreview.GetContentBounds(bounds, BorderThickness);
        Color[] cache = GetTemperatureSliderCache(content.Width);
        for (int x = content.Left; x < content.Right; x++)
        {
            DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, content.Top, 1, content.Height), cache[x - content.Left] * DA.Opacity);
        }

        DrawRectangleBorder(DA, bounds, BorderColor);
    }

    private float GetExposurePercent(float intensity)
        => GetExposurePercent(intensity, MinIntensity, MaxIntensity);

    private static float GetExposurePercent(float intensity, float minIntensity, float maxIntensity)
    {
        float min = Math.Max(0.0001f, minIntensity);
        float max = Math.Max(min, maxIntensity);
        float value = Math.Clamp(intensity, min, max);
        float minExposure = MathF.Log2(min);
        float maxExposure = MathF.Log2(max);
        return maxExposure.Equals(minExposure) ? 0f : Math.Clamp((MathF.Log2(value) - minExposure) / (maxExposure - minExposure), 0f, 1f);
    }

    private static float GetIntensityFromExposurePercent(float percent, float minIntensity, float maxIntensity)
    {
        float min = Math.Max(0.0001f, minIntensity);
        float max = Math.Max(min, maxIntensity);
        float minExposure = MathF.Log2(min);
        float maxExposure = MathF.Log2(max);
        return MathF.Pow(2f, minExposure + Math.Clamp(percent, 0f, 1f) * (maxExposure - minExposure));
    }

    private Color[] GetHueSliderCache(int length)
    {
        int actualLength = Math.Max(0, length);
        if (_hueSliderCache == null || _hueSliderCacheLength != actualLength || _hueSliderCacheColorSpace != Value.ColorSpace)
        {
            _hueSliderCacheLength = actualLength;
            _hueSliderCacheColorSpace = Value.ColorSpace;
            _hueSliderCache = new Color[actualLength];
            for (int index = 0; index < _hueSliderCache.Length; index++)
            {
                float percent = _hueSliderCache.Length <= 1 ? 0f : index / (float)(_hueSliderCache.Length - 1);
                _hueSliderCache[index] = MGColorSlider.GetGradientColor(ColorSliderChannel.Hue, percent, Value).ToXnaColor();
            }
        }

        return _hueSliderCache;
    }

    private Color[] GetTemperatureSliderCache(int length)
    {
        int actualLength = Math.Max(0, length);
        if (TemperatureSliderCache == null
            || TemperatureSliderCacheLength != actualLength
            || !TemperatureSliderCacheMinKelvin.Equals(MinKelvin)
            || !TemperatureSliderCacheMaxKelvin.Equals(MaxKelvin)
            || TemperatureSliderCacheColorSpace != DisplayColorSpace)
        {
            TemperatureSliderCacheLength = actualLength;
            TemperatureSliderCacheMinKelvin = MinKelvin;
            TemperatureSliderCacheMaxKelvin = MaxKelvin;
            TemperatureSliderCacheColorSpace = DisplayColorSpace;
            TemperatureSliderCache = new Color[actualLength];
            for (int index = 0; index < TemperatureSliderCache.Length; index++)
            {
                float percent = TemperatureSliderCache.Length <= 1 ? 0f : index / (float)(TemperatureSliderCache.Length - 1);
                float kelvin = MGColorSlider.GetValueFromPercent(percent, MinKelvin, MaxKelvin);
                TemperatureSliderCache[index] = ColorTemperatureConverter.KelvinToRgb(kelvin, DisplayColorSpace, MinKelvin, MaxKelvin).ToXnaColor();
            }
        }

        return TemperatureSliderCache;
    }

    private void DrawPreview(ElementDrawArgs DA, Rectangle bounds)
    {
        Rectangle content = MGColorPreview.GetContentBounds(bounds, BorderThickness);
        Rectangle previousBounds = MGColorPreview.GetPreviousValueBounds(content, true);
        Rectangle currentBounds = MGColorPreview.GetCurrentValueBounds(content, true);
        ColorValue currentPreview = ShowToneMappedPreview ? Model.GetToneMappedPreview() : Value;
        if (ShowLightDarkPreview)
        {
            DrawLightDarkPreview(DA, previousBounds, PreviousValue);
            DrawLightDarkPreview(DA, currentBounds, currentPreview);
        }
        else
        {
            DrawCheckerboard(DA, content);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), previousBounds, PreviousValue.ToXnaColor() * DA.Opacity);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), currentBounds, currentPreview.ToXnaColor() * DA.Opacity);
        }

        Color border = GetPreviewBorderColor(currentPreview);
        DrawRectangleBorder(DA, bounds, border);
    }

    private void DrawLightDarkPreview(ElementDrawArgs DA, Rectangle bounds, ColorValue value)
    {
        int topHeight = Math.Max(1, bounds.Height / 2);
        Rectangle lightBounds = new(bounds.X, bounds.Y, bounds.Width, topHeight);
        Rectangle darkBounds = new(bounds.X, bounds.Y + topHeight, bounds.Width, Math.Max(0, bounds.Height - topHeight));
        DA.DT.FillRectangle(DA.Offset.ToVector2(), lightBounds, Color.White * DA.Opacity);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), darkBounds, Color.Black * DA.Opacity);
        Color overlay = value.ToXnaColor() * DA.Opacity;
        DA.DT.FillRectangle(DA.Offset.ToVector2(), lightBounds, overlay);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), darkBounds, overlay);
    }

    private Color GetPreviewBorderColor(ColorValue currentPreview)
    {
        if (ShowContrastWarning && !ColorContrastHelper.MeetsContrast(ContrastTextColor, currentPreview, MinimumContrastRatio))
        {
            return Color.Red;
        }

        return IsHdr && Value.IsHdr ? new Color(255, 180, 0) : BorderColor;
    }

    private void DrawEyeDropperButton(ElementDrawArgs DA, Rectangle bounds)
    {
        Color fill = IsEyeDropperAvailable ? new Color(235, 235, 235) : new Color(140, 140, 140);
        Color stroke = IsEyeDropperAvailable ? BorderColor : new Color(90, 90, 90);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, fill * DA.Opacity);
        int midY = bounds.Center.Y;
        Rectangle stem = new(bounds.X + 8, midY - 1, Math.Max(0, bounds.Width - 16), 2);
        Rectangle bulb = new(bounds.X + 6, midY - 4, 6, 6);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), stem, stroke * DA.Opacity);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), bulb, stroke * DA.Opacity);
        DrawRectangleBorder(DA, bounds, stroke);
    }

    private void OnColorPicked(object sender, ColorPickedEventArgs e)
    {
        ColorValue actual = ShowAlpha ? e.Value : e.Value.WithAlpha(Value.A);
        BeginEdit();
        Model.PreviewValue(actual);
        if (CommitMode != ColorEditCommitMode.ExplicitOkCancel)
        {
            CommitEdit();
        }
    }

    private void OnColorPickCancelled(object sender, EventArgs e)
    {
    }

    private void DrawTextInputStrip(ElementDrawArgs DA, Rectangle bounds)
    {
        DA.DT.FillRectangle(DA.Offset.ToVector2(), bounds, Color.White * DA.Opacity);
        DrawRectangleBorder(DA, bounds, TextInput.HasValidationError ? Color.Red : BorderColor);
        if (!TextInput.HasValidationError && IsDisplayDifferentFromStorage)
        {
            DrawRectangleBorder(DA, bounds, new Color(255, 180, 0));
        }
    }

    private void DrawSaturationValueThumb(ElementDrawArgs DA, Rectangle bounds)
    {
        int x = bounds.Left + (int)MathF.Round(bounds.Width * Math.Clamp(Model.HsvValue.S, 0f, 1f));
        int y = bounds.Top + (int)MathF.Round(bounds.Height * (1f - Math.Clamp(Model.HsvValue.V, 0f, 1f)));
        Rectangle thumb = new(Math.Clamp(x - 3, bounds.Left, Math.Max(bounds.Left, bounds.Right - 6)), Math.Clamp(y - 3, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - 6)), 6, 6);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), thumb, ThumbColor * DA.Opacity);
        DrawRectangleBorder(DA, thumb, ThumbBorderColor);
    }

    private void DrawVerticalSliderThumb(ElementDrawArgs DA, Rectangle bounds, float percent)
    {
        int y = bounds.Top + (int)MathF.Round(bounds.Height * Math.Clamp(percent, 0f, 1f));
        Rectangle thumb = new(bounds.Left, Math.Clamp(y - 2, bounds.Top, Math.Max(bounds.Top, bounds.Bottom - 4)), bounds.Width, 4);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), thumb, ThumbColor * DA.Opacity);
        DrawRectangleBorder(DA, thumb, ThumbBorderColor);
    }

    private void DrawHorizontalSliderThumb(ElementDrawArgs DA, Rectangle bounds, float percent)
    {
        int x = bounds.Left + (int)MathF.Round(bounds.Width * Math.Clamp(percent, 0f, 1f));
        Rectangle thumb = new(Math.Clamp(x - 2, bounds.Left, Math.Max(bounds.Left, bounds.Right - 4)), bounds.Top, 4, bounds.Height);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), thumb, ThumbColor * DA.Opacity);
        DrawRectangleBorder(DA, thumb, ThumbBorderColor);
    }

    private void DrawCheckerboard(ElementDrawArgs DA, Rectangle bounds)
    {
        int cellSize = Math.Max(1, CheckerboardCellSize);
        for (int y = bounds.Top; y < bounds.Bottom; y += cellSize)
        {
            int height = Math.Min(cellSize, bounds.Bottom - y);
            for (int x = bounds.Left; x < bounds.Right; x += cellSize)
            {
                int width = Math.Min(cellSize, bounds.Right - x);
                bool light = ((x - bounds.Left) / cellSize + (y - bounds.Top) / cellSize) % 2 == 0;
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(x, y, width, height), (light ? CheckerboardLightColor : CheckerboardDarkColor) * DA.Opacity);
            }
        }
    }

    private void DrawRectangleBorder(ElementDrawArgs DA, Rectangle bounds, Color color)
    {
        int thickness = Math.Max(1, BorderThickness);
        Color actual = color * DA.Opacity;
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), actual);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), actual);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), actual);
        DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), actual);
    }
}