using System;

namespace MGUI.Core.UI
{
    public sealed class MGColorFieldModel
    {
        private ColorValue? _Value;
        private ColorValue? _DefaultValue;
        private bool _AllowNull;
        private bool _IsMixed;

        public ColorValue? Value => _Value;
        public ColorValue? DefaultValue
        {
            get => _DefaultValue;
            set => _DefaultValue = value;
        }

        public bool AllowNull
        {
            get => _AllowNull;
            set
            {
                _AllowNull = value;
                if (!_AllowNull && _Value == null)
                {
                    SetValue(DefaultValue ?? new ColorValue(0f, 0f, 0f, 1f));
                }
            }
        }

        public bool IsMixed
        {
            get => _IsMixed;
            set => _IsMixed = value;
        }

        public bool IsReadOnly { get; set; }

        public event EventHandler<ColorFieldValueChangedEventArgs> ValueChanged;

        public MGColorFieldModel()
            : this(new ColorValue(1f, 1f, 1f, 1f))
        {
        }

        public MGColorFieldModel(ColorValue? value)
        {
            _AllowNull = value == null;
            _Value = value;
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
            ColorValue? previous = _Value;
            _Value = value;
            _IsMixed = false;
            if (previous != _Value)
            {
                ValueChanged?.Invoke(this, new ColorFieldValueChangedEventArgs(previous, _Value));
            }
        }
    }
}