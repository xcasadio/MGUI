using System;

namespace MGUI.Core.UI
{
    public sealed class MGColorPickerModel
    {
        private ColorValue _Value;
        private ColorValue _CommittedValue;
        private ColorValue _EditStartValue;
        private HsvColor _HsvValue;

        public ColorValue Value => _Value;
        public ColorValue CommittedValue => _CommittedValue;
        public HsvColor HsvValue => _HsvValue;
        public ColorPickerConstraints Constraints { get; }
        public MGColorTextInputModel TextInput { get; }
        public bool IsEditing { get; private set; }
        public ColorEditCommitMode CommitMode { get; set; } = ColorEditCommitMode.Live;

        public event EventHandler<ColorValueChangingEventArgs> ValueChanging;
        public event EventHandler<ColorValueChangedEventArgs> ValueChanged;
        public event EventHandler EditStarted;
        public event EventHandler<ColorValueChangedEventArgs> EditCommitted;
        public event EventHandler EditCancelled;

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
        {
            ColorValue previous = _CommittedValue;
            ColorValue actual = ApplyValue(value);
            _CommittedValue = actual;
            _EditStartValue = actual;
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
                Math.Clamp(hsv.V, 0f, 1f),
                Math.Clamp(hsv.A, 0f, 1f));

            PreviewValue(ColorSpaceConverter.HsvToRgb(actualHsv, Value.ColorSpace));
        }

        public void SetHue(float hue)
            => SetHsv(new HsvColor(hue, HsvValue.S, HsvValue.V, Value.A));

        public void SetSaturationValue(float saturation, float value)
            => SetHsv(new HsvColor(HsvValue.H, saturation, value, Value.A));

        public void SetAlpha(float alpha)
            => PreviewValue(Value.WithAlpha(Math.Clamp(alpha, 0f, 1f)));

        public void BeginEdit()
        {
            if (IsEditing)
            {
                return;
            }

            _EditStartValue = _CommittedValue;
            IsEditing = true;
            EditStarted?.Invoke(this, EventArgs.Empty);
        }

        public void PreviewValue(ColorValue value)
        {
            if (!IsEditing)
            {
                BeginEdit();
            }

            ColorValue previousPreview = _Value;
            ColorValue actual = ApplyValue(value);
            if (previousPreview == actual)
            {
                return;
            }

            ValueChanging?.Invoke(this, new ColorValueChangingEventArgs(_EditStartValue, actual));
            if (CommitMode == ColorEditCommitMode.Live)
            {
                ColorValue previousCommit = _CommittedValue;
                _CommittedValue = actual;
                ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(previousCommit, actual));
            }
        }

        public bool CommitEdit()
        {
            if (!IsEditing)
            {
                return false;
            }

            ColorValue previousCommit = _CommittedValue;
            ColorValue finalValue = _Value;
            if (CommitMode != ColorEditCommitMode.Live && previousCommit != finalValue)
            {
                _CommittedValue = finalValue;
                ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(previousCommit, finalValue));
            }

            IsEditing = false;
            EditCommitted?.Invoke(this, new ColorValueChangedEventArgs(_EditStartValue, finalValue));
            _EditStartValue = finalValue;
            return true;
        }

        public bool CancelEdit()
        {
            if (!IsEditing)
            {
                return false;
            }

            ColorValue currentValue = _Value;
            ApplyValue(_EditStartValue);
            if (CommitMode == ColorEditCommitMode.Live && currentValue != _EditStartValue)
            {
                _CommittedValue = _EditStartValue;
                ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(currentValue, _EditStartValue));
            }

            IsEditing = false;
            EditCancelled?.Invoke(this, EventArgs.Empty);
            return true;
        }

        private void SetValue(ColorValue value, bool raiseEvent)
        {
            ColorValue previous = _Value;
            ColorValue actual = ApplyValue(value);
            _CommittedValue = actual;
            _EditStartValue = actual;

            if (raiseEvent && previous != actual)
            {
                ValueChanged?.Invoke(this, new ColorValueChangedEventArgs(previous, actual));
            }
        }

        private ColorValue ApplyValue(ColorValue value)
        {
            ColorValue actual = Constraints.Apply(value);
            _Value = actual;
            _HsvValue = ColorSpaceConverter.RgbToHsv(actual);
            TextInput.SetValue(actual);
            return actual;
        }
    }
}