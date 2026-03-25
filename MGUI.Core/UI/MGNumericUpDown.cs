using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.NumericUpDown;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Helpers;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System;
using System.Collections.Generic;

namespace MGUI.Core.UI
{
    public class MGNumericUpDown : MGTextBox
    {
        public const string SpinnerHostPartName = "PART_SpinnerHost";
        public const string IncreaseButtonPartName = "PART_IncreaseButton";
        public const string DecreaseButtonPartName = "PART_DecreaseButton";

        private const int DefaultLargeStepMultiplier = 10;

        protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
        {
            foreach (MGControlTemplatePartRequirement requirement in base.GetRequiredControlTemplateParts())
            {
                yield return requirement;
            }

            yield return new(SpinnerHostPartName, typeof(MGGrid));
            yield return new(IncreaseButtonPartName, typeof(MGButton));
            yield return new(DecreaseButtonPartName, typeof(MGButton));
        }

        public MGComponent<MGGrid> SpinnerHostComponent { get; private set; }
        private MGGrid SpinnerHostElement { get; set; }

        public MGButton IncreaseButtonElement { get; private set; }
        public MGButton DecreaseButtonElement { get; private set; }

        private readonly MGNumericUpDownModel Model;
        private bool IsSynchronizingText { get; set; }

        public double Minimum
        {
            get => Model.Minimum;
            set
            {
                double previousValue = Value;
                if (!Model.SetRange(value, Maximum))
                {
                    return;
                }

                NPC(nameof(Minimum));
                HandleModelValueMutation(previousValue, true);
            }
        }

        public double Maximum
        {
            get => Model.Maximum;
            set
            {
                double previousValue = Value;
                if (!Model.SetRange(Minimum, value))
                {
                    return;
                }

                NPC(nameof(Maximum));
                HandleModelValueMutation(previousValue, true);
            }
        }

        public double Value
        {
            get => Model.Value;
            set => SetValueCore(value, true);
        }

        public double Increment
        {
            get => Model.Increment;
            set
            {
                if (Model.SetIncrement(value))
                {
                    NPC(nameof(Increment));
                    UpdateSpinnerState();
                }
            }
        }

        public int DecimalPlaces
        {
            get => Model.DecimalPlaces;
            set
            {
                double previousValue = Value;
                if (!Model.SetDecimalPlaces(value))
                {
                    return;
                }

                NPC(nameof(DecimalPlaces));
                HandleModelValueMutation(previousValue, true);
            }
        }

        public string FormatString
        {
            get => Model.FormatString;
            set
            {
                if (Model.SetFormatString(value))
                {
                    NPC(nameof(FormatString));
                    SyncTextFromValue();
                }
            }
        }

        public event EventHandler<EventArgs<double>> ValueChanged;

        public MGNumericUpDown(MGWindow window, double minimum = 0, double maximum = 100, double value = 0,
            double increment = 1, int decimalPlaces = 0, string formatString = null)
            : base(window, MGElementType.NumericUpDown, 64, false, false)
        {
            using (BeginInitializing())
            {
                Model = new(minimum, maximum, value, increment, decimalPlaces, formatString);
                DefaultControlTemplateName = MGControlTemplateCatalog.NumericUpDownTemplateName;
                AcceptsReturn = false;
                AcceptsTab = false;
                MinLines = 1;
                MaxLines = 1;
                WrapText = false;
                EnableScrolling = true;
                SyncTextFromValue();
            }
        }

        protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
        {
            base.AttachControlTemplateStructure(structure);

            SpinnerHostElement = structure.Parts[SpinnerHostPartName] as MGGrid;
            IncreaseButtonElement = structure.Parts[IncreaseButtonPartName] as MGButton;
            DecreaseButtonElement = structure.Parts[DecreaseButtonPartName] as MGButton;

            EnsureComponentBinding(() => SpinnerHostComponent, value => SpinnerHostComponent = value, SpinnerHostElement,
                element => new(element, ComponentUpdatePriority.BeforeContents, ComponentDrawPriority.AfterContents,
                    false, true, false, false, true, false, false,
                    (availableBounds, componentSize) =>
                    {
                        int width = Math.Min(componentSize.Width, availableBounds.Width);
                        return new Rectangle(availableBounds.Right - width, availableBounds.Top, width, availableBounds.Height);
                    }));

            WireSpinnerButtons();
            UpdateSpinnerState();
            SyncTextFromValue();
        }

        private void WireSpinnerButtons()
        {
            if (IncreaseButtonElement != null)
            {
                IncreaseButtonElement.OnLeftClicked += (sender, e) =>
                {
                    if (TryIncrease())
                    {
                        RequestFocus();
                        SelectAll();
                    }

                    e.SetHandledBy(this, false);
                };
            }

            if (DecreaseButtonElement != null)
            {
                DecreaseButtonElement.OnLeftClicked += (sender, e) =>
                {
                    if (TryDecrease())
                    {
                        RequestFocus();
                        SelectAll();
                    }

                    e.SetHandledBy(this, false);
                };
            }
        }

        private void HandleModelValueMutation(double previousValue, bool syncText)
        {
            if (syncText)
            {
                SyncTextFromValue();
            }

            UpdateSpinnerState();

            if (!previousValue.Equals(Value))
            {
                NPC(nameof(Value));
                ValueChanged?.Invoke(this, new(previousValue, Value));
            }
        }

        private bool SetValueCore(double value, bool syncText)
        {
            double previousValue = Value;
            if (!Model.SetValue(value))
            {
                if (syncText)
                {
                    SyncTextFromValue();
                }

                return false;
            }

            HandleModelValueMutation(previousValue, syncText);
            return true;
        }

        private void SyncTextFromValue()
        {
            IsSynchronizingText = true;
            try
            {
                base.SetText(Model.FormatValue());
            }
            finally
            {
                IsSynchronizingText = false;
            }
        }

        private void UpdateSpinnerState()
        {
            if (IncreaseButtonElement != null)
            {
                IncreaseButtonElement.IsEnabled = !IsReadonly && !Model.IsAtMaximum;
            }

            if (DecreaseButtonElement != null)
            {
                DecreaseButtonElement.IsEnabled = !IsReadonly && !Model.IsAtMinimum;
            }
        }

        public bool TryIncrease() => IsReadonly ? false : SetValueCore(Value + Increment, true);
        public bool TryDecrease() => IsReadonly ? false : SetValueCore(Value - Increment, true);
        public bool TryIncreaseLarge() => IsReadonly ? false : SetValueCore(Value + Increment * DefaultLargeStepMultiplier, true);
        public bool TryDecreaseLarge() => IsReadonly ? false : SetValueCore(Value - Increment * DefaultLargeStepMultiplier, true);
        public bool TrySetMinimum() => IsReadonly ? false : SetValueCore(Minimum, true);
        public bool TrySetMaximum() => IsReadonly ? false : SetValueCore(Maximum, true);
    }
}