using Microsoft.Xna.Framework;
using MonoGame.Extended;
using MGUI.Shared.Input.Mouse;
using System;
using System.Diagnostics;

namespace MGUI.Core.UI
{
    public class MGColorPicker : MGElement
    {
        private enum DragTarget
        {
            None,
            SaturationValue,
            Hue,
            Alpha,
            Intensity,
        }

        private DragTarget ActiveDragTarget = DragTarget.None;
        private IColorPickService _ColorPickService = UnsupportedColorPickService.Instance;

        public MGColorPickerModel Model { get; }
        public MGColorTextInputModel TextInput => Model.TextInput;

        public ColorValue Value
        {
            get => Model.Value;
            set => Model.SetValue(value);
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ColorValue _PreviousValue;
        public ColorValue PreviousValue
        {
            get => _PreviousValue;
            set
            {
                if (_PreviousValue != value)
                {
                    _PreviousValue = value;
                    NPC(nameof(PreviousValue));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _ShowAlpha;
        public bool ShowAlpha
        {
            get => _ShowAlpha;
            set
            {
                if (_ShowAlpha != value)
                {
                    _ShowAlpha = value;
                    LayoutChanged(this, true);
                    NPC(nameof(ShowAlpha));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _ShowTextInput;
        public bool ShowTextInput
        {
            get => _ShowTextInput;
            set
            {
                if (_ShowTextInput != value)
                {
                    _ShowTextInput = value;
                    LayoutChanged(this, true);
                    NPC(nameof(ShowTextInput));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ColorPickerMode _PickerMode;
        public ColorPickerMode PickerMode
        {
            get => _PickerMode;
            set
            {
                if (_PickerMode != value)
                {
                    _PickerMode = value;
                    NPC(nameof(PickerMode));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private ColorValueFormat _DisplayFormat;
        public ColorValueFormat DisplayFormat
        {
            get => _DisplayFormat;
            set
            {
                if (_DisplayFormat != value)
                {
                    _DisplayFormat = value;
                    TextInput.HexFormat = value;
                    TextInput.SetValue(Value);
                    NPC(nameof(DisplayFormat));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _SaturationValueSize;
        public int SaturationValueSize
        {
            get => _SaturationValueSize;
            set
            {
                int actual = Math.Max(0, value);
                if (_SaturationValueSize != actual)
                {
                    _SaturationValueSize = actual;
                    LayoutChanged(this, true);
                    NPC(nameof(SaturationValueSize));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _SliderThickness;
        public int SliderThickness
        {
            get => _SliderThickness;
            set
            {
                int actual = Math.Max(0, value);
                if (_SliderThickness != actual)
                {
                    _SliderThickness = actual;
                    LayoutChanged(this, true);
                    NPC(nameof(SliderThickness));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _PreviewWidth;
        public int PreviewWidth
        {
            get => _PreviewWidth;
            set
            {
                int actual = Math.Max(0, value);
                if (_PreviewWidth != actual)
                {
                    _PreviewWidth = actual;
                    LayoutChanged(this, true);
                    NPC(nameof(PreviewWidth));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _PreviewHeight;
        public int PreviewHeight
        {
            get => _PreviewHeight;
            set
            {
                int actual = Math.Max(0, value);
                if (_PreviewHeight != actual)
                {
                    _PreviewHeight = actual;
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
        private bool _IsHdr;
        public bool IsHdr
        {
            get => _IsHdr;
            set
            {
                if (_IsHdr != value)
                {
                    _IsHdr = value;
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
        private bool _ShowEyeDropper;
        public bool ShowEyeDropper
        {
            get => _ShowEyeDropper;
            set
            {
                if (_ShowEyeDropper != value)
                {
                    _ShowEyeDropper = value;
                    NPC(nameof(ShowEyeDropper));
                }
            }
        }

        public IColorPickService ColorPickService
        {
            get => _ColorPickService;
            set
            {
                IColorPickService actual = value ?? UnsupportedColorPickService.Instance;
                if (ReferenceEquals(_ColorPickService, actual))
                {
                    return;
                }

                _ColorPickService.ColorPicked -= OnColorPicked;
                _ColorPickService.ColorPickCancelled -= OnColorPickCancelled;
                _ColorPickService = actual;
                _ColorPickService.ColorPicked += OnColorPicked;
                _ColorPickService.ColorPickCancelled += OnColorPickCancelled;
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
                        ActiveDragTarget = HitTest(ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position));
                        e.SetHandledBy(this, false);
                    }
                };
                MouseHandler.Dragged += (sender, e) =>
                {
                    if (e.IsLMB && ActiveDragTarget != DragTarget.None)
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

                        ActiveDragTarget = DragTarget.None;
                    }
                };
            }
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            return action switch
            {
                UINavigationAction.Submit when CommitMode == ColorEditCommitMode.ExplicitOkCancel && Model.IsEditing => CommitEdit(),
                UINavigationAction.Cancel when Model.IsEditing => CancelEdit(),
                _ => base.TryHandleNavigationAction(action),
            };
        }

        public override Thickness MeasureSelfOverride(Size AvailableSize, out Thickness SharedSize)
        {
            SharedSize = new(0);
            int spacing = Math.Max(0, ControlSpacing);
            int width = SaturationValueSize + spacing + SliderThickness + spacing + PreviewWidth;
            int height = SaturationValueSize + (ShowAlpha ? spacing + SliderThickness : 0) + (ShowIntensity ? spacing + SliderThickness : 0) + (ShowTextInput ? spacing + TextInputHeight : 0);
            return new(width, height, 0, 0);
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {
            Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, GetDesiredSize());
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
                SaturationValueSize + (ShowAlpha ? spacing + SliderThickness : 0) + (ShowIntensity ? spacing + SliderThickness : 0) + (ShowTextInput ? spacing + TextInputHeight : 0));
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

        private Rectangle GetPreviewBounds(Rectangle bounds)
            => new(bounds.X + SaturationValueSize + Math.Max(0, ControlSpacing) + SliderThickness + Math.Max(0, ControlSpacing), bounds.Y, PreviewWidth, PreviewHeight);

        private Rectangle GetEyeDropperButtonBounds(Rectangle bounds)
            => new(GetPreviewBounds(bounds).X, GetPreviewBounds(bounds).Bottom + Math.Max(0, ControlSpacing), PreviewWidth, SliderThickness);

        private Rectangle GetTextInputBounds(Rectangle bounds)
        {
            int y = bounds.Y + SaturationValueSize + (ShowAlpha ? Math.Max(0, ControlSpacing) + SliderThickness : 0) + (ShowIntensity ? Math.Max(0, ControlSpacing) + SliderThickness : 0) + Math.Max(0, ControlSpacing);
            return new(bounds.X, y, bounds.Width, TextInputHeight);
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

            return DragTarget.None;
        }

        private void SetValueFromScreenPosition(Point screenPosition, bool useActiveTarget)
        {
            Point layoutPoint = ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, screenPosition);
            DragTarget target = useActiveTarget && ActiveDragTarget != DragTarget.None ? ActiveDragTarget : HitTest(layoutPoint);
            Rectangle bounds = ApplyAlignment(LayoutBounds, HorizontalAlignment, VerticalAlignment, GetDesiredSize());
            switch (target)
            {
                case DragTarget.SaturationValue:
                    (float saturation, float value) = GetSaturationValueFromPoint(layoutPoint, GetSaturationValueBounds(bounds));
                    Model.SetSaturationValue(saturation, value);
                    ActiveDragTarget = DragTarget.SaturationValue;
                    break;
                case DragTarget.Hue:
                    float huePercent = MGColorSlider.GetPercentFromPoint(layoutPoint, GetHueSliderBounds(bounds), Orientation.Vertical);
                    Model.SetHue(huePercent * 360f);
                    ActiveDragTarget = DragTarget.Hue;
                    break;
                case DragTarget.Alpha:
                    float alpha = MGColorSlider.GetPercentFromPoint(layoutPoint, GetAlphaSliderBounds(bounds), Orientation.Horizontal);
                    Model.SetAlpha(alpha);
                    ActiveDragTarget = DragTarget.Alpha;
                    break;
                case DragTarget.Intensity:
                    float percent = MGColorSlider.GetPercentFromPoint(layoutPoint, GetIntensitySliderBounds(bounds), Orientation.Horizontal);
                    Model.SetIntensity(GetIntensityFromPercent(percent));
                    ActiveDragTarget = DragTarget.Intensity;
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
            for (int y = content.Top; y < content.Bottom; y++)
            {
                float percent = content.Height <= 1 ? 0f : (y - content.Top) / (float)(content.Height - 1);
                Color color = MGColorSlider.GetGradientColor(ColorSliderChannel.Hue, percent, Value).ToXnaColor() * DA.Opacity;
                DA.DT.FillRectangle(DA.Offset.ToVector2(), new Rectangle(content.Left, y, content.Width, 1), color);
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

        private float GetExposurePercent(float intensity)
        {
            float min = Math.Max(0.0001f, MinIntensity);
            float max = Math.Max(min, MaxIntensity);
            float value = Math.Clamp(intensity, min, max);
            float minExposure = MathF.Log2(min);
            float maxExposure = MathF.Log2(max);
            return maxExposure.Equals(minExposure) ? 0f : Math.Clamp((MathF.Log2(value) - minExposure) / (maxExposure - minExposure), 0f, 1f);
        }

        private void DrawPreview(ElementDrawArgs DA, Rectangle bounds)
        {
            Rectangle content = MGColorPreview.GetContentBounds(bounds, BorderThickness);
            DrawCheckerboard(DA, content);
            Rectangle previousBounds = MGColorPreview.GetPreviousValueBounds(content, true);
            Rectangle currentBounds = MGColorPreview.GetCurrentValueBounds(content, true);
            DA.DT.FillRectangle(DA.Offset.ToVector2(), previousBounds, PreviousValue.ToXnaColor() * DA.Opacity);
            ColorValue currentPreview = ShowToneMappedPreview ? Model.GetToneMappedPreview() : Value;
            DA.DT.FillRectangle(DA.Offset.ToVector2(), currentBounds, currentPreview.ToXnaColor() * DA.Opacity);
            DrawRectangleBorder(DA, bounds, IsHdr && Value.IsHdr ? new Color(255, 180, 0) : BorderColor);
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
}