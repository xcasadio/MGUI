using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI;

public sealed class MGColorPickerPopup
{
    private readonly MGWindow OwnerWindow;
    private bool IsClosingFromPickerEvent;

    public MGWindow PopupWindow { get; }
    public MGColorPicker Picker { get; }
    public bool IsOpen { get; private set; }
    public ColorValue Value { get; private set; }
    public ColorValue InitialValue { get; private set; }
    public int PopupWidth { get; set; } = 280;
    public int PopupHeight { get; set; } = 220;
    public bool CloseOnOutsideRelease { get; set; } = true;

    public event EventHandler PopupOpened;
    public event EventHandler PopupClosed;
    public event EventHandler<ColorValueChangedEventArgs> EditCommitted;
    public event EventHandler EditCancelled;

    public MGColorPickerPopup(MGWindow ownerWindow)
        : this(ownerWindow, new ColorPickerOptions())
    {
    }

    public MGColorPickerPopup(MGWindow ownerWindow, ColorPickerOptions options)
    {
        OwnerWindow = ownerWindow ?? throw new ArgumentNullException(nameof(ownerWindow));
        ColorPickerOptions popupOptions = CreatePopupOptions(options);
        PopupWidth = 280;
        PopupHeight = 220;

        //  No theme argument: the popup's Window scope inherits the owner window's scope, so it follows that scope's theme changes.
        PopupWindow = new MGWindow(ownerWindow, 0, 0, PopupWidth, PopupHeight)
        {
            IsTitleBarVisible = false,
            IsCloseButtonVisible = false,
        };
        //  Excluded from window click-activation (decision utilisateur, Docs/input-window-activation-design.md section 3.a):
        //  the color picker popup is a nested popup window; clicking inside it must not reorder nested windows.
        PopupWindow.ActivatesOnClick = false;

        Picker = new MGColorPicker(PopupWindow, popupOptions)
        {
            CommitMode = ColorEditCommitMode.ExplicitOkCancel,
        };
        Picker.EditCommitted += OnPickerEditCommitted;
        Picker.EditCancelled += OnPickerEditCancelled;

        PopupWindow.SetContent(Picker);
        PopupWindow.CanChangeContent = false;
        PopupWindow.WindowMouseHandler.ReleasedOutside += OnPopupReleasedOutside;
        Value = popupOptions.InitialValue;
        InitialValue = popupOptions.InitialValue;
    }

    public void OpenRelativeTo(MGElement anchor, ColorValue value)
    {
        if (anchor == null)
        {
            throw new ArgumentNullException(nameof(anchor));
        }

        Point preferredTopLeft = anchor.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, anchor.LayoutBounds.BottomLeft());
        Open(preferredTopLeft, value);
    }

    public void Open(Point preferredTopLeftScreen, ColorValue value)
    {
        if (IsOpen)
        {
            CancelAndClose();
        }

        InitialValue = value;
        Value = value;
        PopupWindow.WindowWidth = PopupWidth;
        PopupWindow.WindowHeight = PopupHeight;
        Picker.Value = value;
        Picker.PreviousValue = value;
        Picker.CommitMode = ColorEditCommitMode.ExplicitOkCancel;
        Picker.BeginEdit();

        PositionPopup(preferredTopLeftScreen);
        OwnerWindow.AddNestedWindow(PopupWindow);
        OwnerWindow.GetDesktop().PushFocusScope(PopupWindow, OwnerWindow.GetDesktop().FocusedKeyboardHandler);
        Picker.Focus(KeyboardFocusSource.Programmatic);
        IsOpen = true;
        PopupOpened?.Invoke(this, EventArgs.Empty);
    }

    public bool CommitAndClose()
    {
        if (!IsOpen)
        {
            return false;
        }

        IsClosingFromPickerEvent = true;
        try
        {
            return Picker.CommitEdit();
        }
        finally
        {
            IsClosingFromPickerEvent = false;
            if (IsOpen)
            {
                CloseAfterCommit(new ColorValueChangedEventArgs(InitialValue, Picker.Value));
            }
        }
    }

    public bool CancelAndClose()
    {
        if (!IsOpen)
        {
            return false;
        }

        IsClosingFromPickerEvent = true;
        try
        {
            _ = Picker.CancelEdit();
        }
        finally
        {
            IsClosingFromPickerEvent = false;
            if (IsOpen)
            {
                CloseAfterCancel();
            }
        }

        return true;
    }

    public bool TryHandleNavigationAction(UINavigationAction action)
    {
        if (!IsOpen)
        {
            return false;
        }

        return action switch
        {
            UINavigationAction.Submit => CommitAndClose(),
            UINavigationAction.Cancel => CancelAndClose(),
            _ => Picker.TryHandleNavigationAction(action),
        };
    }

    internal static Rectangle GetFittedPopupBounds(Point preferredTopLeft, int width, int height, Rectangle viewport, float scale)
    {
        float actualScale = scale > 0f ? scale : 1f;
        int desiredScreenWidth = Math.Min(viewport.Width, (int)Math.Ceiling(Math.Max(0, width) * actualScale));
        int desiredScreenHeight = Math.Min(viewport.Height, (int)Math.Ceiling(Math.Max(0, height) * actualScale));
        int maxLeft = Math.Max(viewport.Left, viewport.Right - desiredScreenWidth);
        int left = Math.Clamp(preferredTopLeft.X, viewport.Left, maxLeft);

        int top = preferredTopLeft.Y;
        if (top + desiredScreenHeight > viewport.Bottom)
        {
            top = preferredTopLeft.Y - desiredScreenHeight;
        }

        top = Math.Clamp(top, viewport.Top, Math.Max(viewport.Top, viewport.Bottom - desiredScreenHeight));
        return new Rectangle(left, top, width, height);
    }

    private static ColorPickerOptions CreatePopupOptions(ColorPickerOptions options)
    {
        options ??= new ColorPickerOptions();
        return new ColorPickerOptions
        {
            InitialValue = options.InitialValue,
            ShowAlpha = options.ShowAlpha,
            ShowEyeDropper = options.ShowEyeDropper,
            IsHdr = options.IsHdr,
            AllowNull = options.AllowNull,
            ShowTextInput = options.ShowTextInput,
            ShowIntensity = options.ShowIntensity,
            UseExposureSlider = options.UseExposureSlider,
            ShowToneMappedPreview = options.ShowToneMappedPreview,
            ShowTemperature = options.ShowTemperature,
            ShowLightDarkPreview = options.ShowLightDarkPreview,
            ShowContrastWarning = options.ShowContrastWarning,
            ContrastTextColor = options.ContrastTextColor,
            MinimumContrastRatio = options.MinimumContrastRatio,
            MinIntensity = options.MinIntensity,
            MaxIntensity = options.MaxIntensity,
            MinKelvin = options.MinKelvin,
            MaxKelvin = options.MaxKelvin,
            PickerMode = options.PickerMode,
            DisplayFormat = options.DisplayFormat,
            CommitMode = ColorEditCommitMode.ExplicitOkCancel,
            StorageColorSpace = options.StorageColorSpace,
            DisplayColorSpace = options.DisplayColorSpace,
            Constraints = options.Constraints,
            EditTransaction = options.EditTransaction,
            ColorPickService = options.ColorPickService,
        };
    }

    private void PositionPopup(Point preferredTopLeftScreen)
    {
        Rectangle bounds = GetFittedPopupBounds(preferredTopLeftScreen, PopupWidth, PopupHeight, OwnerWindow.GetDesktop().ValidScreenBounds, PopupWindow.Scale);
        PopupWindow.Left = bounds.Left;
        PopupWindow.Top = bounds.Top;
    }

    private void OnPickerEditCommitted(object sender, ColorValueChangedEventArgs e)
    {
        if (IsOpen && !IsClosingFromPickerEvent)
        {
            CloseAfterCommit(e);
        }
    }

    private void OnPickerEditCancelled(object sender, EventArgs e)
    {
        if (IsOpen && !IsClosingFromPickerEvent)
        {
            CloseAfterCancel();
        }
    }

    private void OnPopupReleasedOutside(object sender, MGUI.Shared.Input.Mouse.BaseMouseReleasedEventArgs e)
    {
        if (!IsOpen || !CloseOnOutsideRelease)
        {
            return;
        }

        Point layoutSpacePosition = PopupWindow.ConvertCoordinateSpace(CoordinateSpace.Screen, CoordinateSpace.Layout, e.Position);
        if (!PopupWindow.RenderBounds.ContainsInclusive(layoutSpacePosition))
        {
            _ = CancelAndClose();
            e.SetHandledBy(PopupWindow, false);
        }
    }

    private void CloseAfterCommit(ColorValueChangedEventArgs e)
    {
        Value = e.NewValue;
        CloseWindow();
        EditCommitted?.Invoke(this, e);
    }

    private void CloseAfterCancel()
    {
        Value = InitialValue;
        CloseWindow();
        EditCancelled?.Invoke(this, EventArgs.Empty);
    }

    private void CloseWindow()
    {
        if (!IsOpen)
        {
            return;
        }

        OwnerWindow.RemoveNestedWindow(PopupWindow);
        OwnerWindow.GetDesktop().PopFocusScope(PopupWindow);
        IsOpen = false;
        PopupClosed?.Invoke(this, EventArgs.Empty);
    }
}