using System;

namespace MGUI.Core.UI
{
    public sealed class MGColorPickerModel
    {
        private ColorValue _Value;
        private HsvColor _HsvValue;

        public ColorValue Value => _Value;
        public HsvColor HsvValue => _HsvValue;
        public ColorPickerConstraints Constraints { get; }
        public MGColorTextInputModel TextInput { get; }

        public event EventHandler<ColorValueChangedEventArgs> ValueChanged;

        public MGColorPickerModel()
            : this(new ColorValue(1f, 1f, 1f, 1f), new ColorPickerConstraints())
        {
        }

        public MGColorPickerModel(ColorValue value, ColorPickerConstraints constraints)
        {
            Constraints = constraints ?? throw new ArgumentNullException(nameof(constraints));
            TextInput = new MGColorTextInputModel();
            SetValue(value, raiseEvent: false);
        }

        public void SetValue(ColorValue value)
            => SetValue(value, raiseEvent: true);

        public void SetHsv(float hue, float saturation, float value)
            => SetHsv(new HsvColor(hue, saturation, value, Value.A));

        public void SetHsv(HsvColor hsv)
        {
            HsvColor actualHsv = new(
                ColorSpaceConverter.NormalizeHue(hsv.H),
                Math.Clamp(hsv.S, 0f, 1f),
                Math.Clamp(hsv.V, 0f, 1f),
                Math.Clamp(hsv.A, 0f, 1f));

            SetValue(ColorSpaceConverter.HsvToRgb(actualHsv, Value.ColorSpace));
        }

        public void SetHue(float hue)
            => SetHsv(new HsvColor(hue, HsvValue.S, HsvValue.V, Value.A));

        public void SetSaturationValue(float saturation, float value)
            => SetHsv(new HsvColor(HsvValue.H, saturation, value, Value.A));

        public void SetAlpha(float alpha)
            => SetValue(Value.WithAlpha(Math.Clamp(alpha, 0f, 1f)));

        private void SetValue(ColorValue value, bool raiseEvent)
        {
            ColorValue previous = _Value;
            ColorValue actual = Constraints.Apply(value);
            _Value = actual;
            _HsvValue = ColorSpaceConverter.RgbToHsv(actual);
            TextInput.SetValue(actual);

            if (raiseEvent && previous != actual)
            {
                ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(previous, actual));
            }
        }
    }
}