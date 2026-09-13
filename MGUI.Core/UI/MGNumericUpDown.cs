using MGUI.Core.UI.Containers.Grids;
using MGUI.Core.UI.NumericUpDown;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input.Keyboard;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Core.UI;

public class MGNumericUpDown : MGTextBox
{
    internal enum NumericAdjustmentAction
    {
        None,
        Increase,
        Decrease,
        IncreaseLarge,
        DecreaseLarge,
        SetMinimum,
        SetMaximum,
        CommitText
    }

    public const string SpinnerHostPartName = "PART_SpinnerHost";
    public const string IncreaseButtonPartName = "PART_IncreaseButton";
    public const string DecreaseButtonPartName = "PART_DecreaseButton";

    private const int DefaultLargeStepMultiplier = 10;

    protected internal override IEnumerable<MGControlTemplatePartRequirement> GetRequiredControlTemplateParts()
    {
        foreach (var requirement in base.GetRequiredControlTemplateParts())
        {
            yield return requirement;
        }

        if (Model == null)
        {
            yield break;
        }

        yield return new(SpinnerHostPartName, typeof(MGGrid));
        yield return new(IncreaseButtonPartName, typeof(MGButton));
        yield return new(DecreaseButtonPartName, typeof(MGButton));
    }

    public MGComponent<MGGrid> SpinnerHostComponent { get; private set; }
    private MGGrid SpinnerHostElement { get; set; }

    public MGButton IncreaseButtonElement { get; private set; }
    public MGButton DecreaseButtonElement { get; private set; }

    private MGButton WiredIncreaseButtonElement { get; set; }
    private MGButton WiredDecreaseButtonElement { get; set; }

    private readonly MGNumericUpDownModel Model;
    private bool IsSynchronizingText { get; set; }
    private bool HasPendingTextValidationError { get; set; }

    internal static NumericAdjustmentAction GetKeyboardAdjustmentAction(Keys key)
        => key switch
        {
            Keys.Up => NumericAdjustmentAction.Increase,
            Keys.Down => NumericAdjustmentAction.Decrease,
            Keys.PageUp => NumericAdjustmentAction.IncreaseLarge,
            Keys.PageDown => NumericAdjustmentAction.DecreaseLarge,
            Keys.Home => NumericAdjustmentAction.SetMinimum,
            Keys.End => NumericAdjustmentAction.SetMaximum,
            Keys.Enter => NumericAdjustmentAction.CommitText,
            _ => NumericAdjustmentAction.None,
        };

    internal static NumericAdjustmentAction GetNavigationAdjustmentAction(UINavigationAction action)
        => action switch
        {
            UINavigationAction.MoveUp => NumericAdjustmentAction.Increase,
            UINavigationAction.MoveDown => NumericAdjustmentAction.Decrease,
            UINavigationAction.Increment => NumericAdjustmentAction.Increase,
            UINavigationAction.Decrement => NumericAdjustmentAction.Decrease,
            UINavigationAction.PageUp => NumericAdjustmentAction.IncreaseLarge,
            UINavigationAction.PageDown => NumericAdjustmentAction.DecreaseLarge,
            UINavigationAction.Home => NumericAdjustmentAction.SetMinimum,
            UINavigationAction.End => NumericAdjustmentAction.SetMaximum,
            UINavigationAction.Submit => NumericAdjustmentAction.CommitText,
            _ => NumericAdjustmentAction.None,
        };

    public double Minimum
    {
        get => Model.Minimum;
        set
        {
            var previousValue = Value;
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
            var previousValue = Value;
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
            var previousValue = Value;
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

            TextChanged += (sender, e) => HandleTextChanged();
            ReadonlyChanged += (sender, e) => UpdateSpinnerState();
            KeyboardHandler.Pressed += (sender, e) => HandleKeyboardPressed(e);
        }
    }

    protected internal override void AttachControlTemplateStructure(MGControlTemplateStructure structure)
    {
        base.AttachControlTemplateStructure(structure);

        structure.Parts.TryGetValue(SpinnerHostPartName, out var spinnerHostPart);
        structure.Parts.TryGetValue(IncreaseButtonPartName, out var increaseButtonPart);
        structure.Parts.TryGetValue(DecreaseButtonPartName, out var decreaseButtonPart);

        SpinnerHostElement = spinnerHostPart as MGGrid;
        IncreaseButtonElement = increaseButtonPart as MGButton;
        DecreaseButtonElement = decreaseButtonPart as MGButton;

        if (SpinnerHostElement == null || IncreaseButtonElement == null || DecreaseButtonElement == null)
        {
            return;
        }

        EnsureComponentBinding(() => SpinnerHostComponent, value => SpinnerHostComponent = value, SpinnerHostElement,
            element => new(element, ComponentUpdatePriority.BeforeContents, ComponentDrawPriority.AfterContents,
                false, true, false, false, true, false, false,
                (availableBounds, componentSize) =>
                {
                    var width = Math.Min(componentSize.Width, availableBounds.Width);
                    return new Rectangle(availableBounds.Right - width, availableBounds.Top, width, availableBounds.Height);
                }));

        ReorderTemplateComponents();
        WireSpinnerButtons();
        UpdateSpinnerState();
        SyncTextFromValue();
    }

    private void ReorderTemplateComponents()
    {
        MGComponentBase[] orderedComponents =
        {
            BorderComponent,
            SpinnerHostComponent,
            ResizeGripComponent,
            PlaceholderTextBlockComponent,
            CharacterCountComponent,
            TextBlockComponent,
        };

        var hasAnyComponent = false;
        for (var i = 0; i < orderedComponents.Length; i++)
        {
            if (orderedComponents[i] != null)
            {
                hasAnyComponent = true;
                RemoveComponent(orderedComponents[i]);
            }
        }

        if (!hasAnyComponent)
        {
            return;
        }

        for (var i = 0; i < orderedComponents.Length; i++)
        {
            if (orderedComponents[i] != null)
            {
                AddComponent(orderedComponents[i]);
            }
        }
    }

    private void WireSpinnerButtons()
    {
        if (IncreaseButtonElement != null && !ReferenceEquals(WiredIncreaseButtonElement, IncreaseButtonElement))
        {
            WiredIncreaseButtonElement = IncreaseButtonElement;
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

        if (DecreaseButtonElement != null && !ReferenceEquals(WiredDecreaseButtonElement, DecreaseButtonElement))
        {
            WiredDecreaseButtonElement = DecreaseButtonElement;
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
        var previousValue = Value;
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
            HasPendingTextValidationError = false;
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

    private void HandleTextChanged()
    {
        if (IsSynchronizingText)
        {
            return;
        }

        if (Model.TryParseText(Text, out var parsedValue))
        {
            HasPendingTextValidationError = false;
            SetValueCore(parsedValue, false);
            UpdateSpinnerState();
        }
        else
        {
            HasPendingTextValidationError = !string.IsNullOrWhiteSpace(Text);
        }
    }

    /// <summary>Commits any pending, uncommitted text edit when this control loses keyboard focus. Gaining focus does not commit.</summary>
    protected internal override void OnKeyboardFocusChanged(bool gained)
    {
        base.OnKeyboardFocusChanged(gained);
        if (!gained)
        {
            CommitPendingText(false);
        }
    }

    private void HandleKeyboardPressed(BaseKeyPressedEventArgs e)
    {
        if (GetDesktop().FocusedKeyboardHandler != this)
        {
            return;
        }

        var action = GetKeyboardAdjustmentAction(e.Key);
        if (action == NumericAdjustmentAction.None)
        {
            return;
        }

        if (ApplyAdjustmentAction(action))
        {
            e.SetHandledBy(this, false);
        }
    }

    private bool CommitPendingText(bool selectAll)
    {
        if (IsSynchronizingText)
        {
            return false;
        }

        var valueChanged = false;
        if (Model.TryApplyText(Text, out var parsedValue))
        {
            valueChanged = SetValueCore(parsedValue, true);
        }
        else if (HasPendingTextValidationError)
        {
            SyncTextFromValue();
            valueChanged = true;
        }
        else
        {
            SyncTextFromValue();
        }

        if (selectAll)
        {
            SelectAll();
        }

        return valueChanged || HasPendingTextValidationError == false;
    }

    private bool ApplyAdjustmentAction(NumericAdjustmentAction action)
    {
        return action switch
        {
            NumericAdjustmentAction.Increase => TryIncrease(),
            NumericAdjustmentAction.Decrease => TryDecrease(),
            NumericAdjustmentAction.IncreaseLarge => TryIncreaseLarge(),
            NumericAdjustmentAction.DecreaseLarge => TryDecreaseLarge(),
            NumericAdjustmentAction.SetMinimum => TrySetMinimum(),
            NumericAdjustmentAction.SetMaximum => TrySetMaximum(),
            NumericAdjustmentAction.CommitText => CommitPendingText(true),
            _ => false,
        };
    }

    public bool TryIncrease() => IsReadonly ? false : SetValueCore(Value + Increment, true);
    public bool TryDecrease() => IsReadonly ? false : SetValueCore(Value - Increment, true);
    public bool TryIncreaseLarge() => IsReadonly ? false : SetValueCore(Value + Increment * DefaultLargeStepMultiplier, true);
    public bool TryDecreaseLarge() => IsReadonly ? false : SetValueCore(Value - Increment * DefaultLargeStepMultiplier, true);
    public bool TrySetMinimum() => IsReadonly ? false : SetValueCore(Minimum, true);
    public bool TrySetMaximum() => IsReadonly ? false : SetValueCore(Maximum, true);

    public override bool TryHandleNavigationAction(UINavigationAction action)
    {
        var mappedAction = GetNavigationAdjustmentAction(action);
        if (mappedAction == NumericAdjustmentAction.None)
        {
            return base.TryHandleNavigationAction(action);
        }

        return ApplyAdjustmentAction(mappedAction);
    }
}