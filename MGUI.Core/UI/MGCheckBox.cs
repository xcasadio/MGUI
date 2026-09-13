using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using System.Diagnostics;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI;

public class MGCheckBox : MGSingleContentHost, Animation.States.IUICheckable
{
    internal static bool? GetNextCheckedState(bool? isChecked, bool isThreeState)
    {
        if (isThreeState)
        {
            return isChecked.HasValue && isChecked.Value ? null : !isChecked.HasValue ? false : true;
        }

        return !isChecked.HasValue || isChecked.Value ? false : true;
    }

    /// <summary>The default width/height of the checkable part of an <see cref="MGCheckBox"/></summary>
    public const int DefaultCheckBoxSize = 16;
    /// <summary>The default empty width between the checkable part of an <see cref="MGCheckBox"/> and its <see cref="MGSingleContentHost.Content"/></summary>
    public const int DefaultCheckBoxSpacingWidth = 5;

    /// <summary>Provides direct access to the button component that appears to the left of this checkbox's content.<para/>
    /// See also: <see cref="ButtonElement"/></summary>
    public MGComponent<MGButton> ButtonComponent { get; }
    /// <summary>The checkable button portion of this <see cref="MGCheckBox"/></summary>
    public MGButton ButtonElement { get; }
    private MGComponent<MGCheckStateIcon> CheckStateIconComponent { get; }
    private MGCheckStateIcon CheckStateIcon { get; }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private int _CheckBoxComponentSize;
    /// <summary>The dimensions of the checkable part of this <see cref="MGCheckBox"/>.<para/>
    /// See also: <see cref="DefaultCheckBoxSize"/></summary>
    public int CheckBoxComponentSize
    {
        get => _CheckBoxComponentSize;
        set
        {
            if (_CheckBoxComponentSize != value)
            {
                _CheckBoxComponentSize = value;

                Size ButtonSize = new(CheckBoxComponentSize, CheckBoxComponentSize);
                ButtonElement.MinWidth = ButtonSize.Width;
                ButtonElement.SetMinHeight(ButtonSize.Height, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                ButtonElement.PreferredWidth = ButtonSize.Width;
                ButtonElement.PreferredHeight = ButtonSize.Height;

                NotifyPropertyChanged(nameof(CheckBoxComponentSize));
            }
        }
    }

    /// <summary>The reserved empty width between the checkable part of this <see cref="MGCheckBox"/> and its <see cref="MGSingleContentHost.Content"/>.<para/>
    /// See also: <see cref="DefaultCheckBoxSpacingWidth"/>.<para/>
    /// This value is functionally equivalent to <see cref="ButtonElement"/>'s right <see cref="MGElement.Margin"/></summary>
    public int SpacingWidth
    {
        get => ButtonElement.Margin.Right;
        set
        {
            if (ButtonElement.Margin.Right != value)
            {
                ButtonElement.SetMargin(ButtonElement.Margin.ChangeRight(value), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
                NotifyPropertyChanged(nameof(SpacingWidth));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Color _CheckMarkColor;
    /// <summary>The <see cref="Color"/> to use when stroking the check mark if <see cref="IsChecked"/> is true.<para/>
    /// Default value: <see cref="MGTheme.CheckMarkColor"/><para/>
    /// See also:<br/><see cref="MGWindow.Theme"/><br/><see cref="MGDesktop.Theme"/></summary>
    public Color CheckMarkColor
    {
        get => _CheckMarkColor;
        set
        {
            if (_CheckMarkColor != value)
            {
                _CheckMarkColor = value;
                if (CheckStateIcon != null)
                {
                    CheckStateIcon.MarkColor = value;
                }
                NotifyPropertyChanged(nameof(CheckMarkColor));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private CheckIndicatorStyle _CheckedIndicatorStyle;
    public CheckIndicatorStyle CheckedIndicatorStyle
    {
        get => _CheckedIndicatorStyle;
        set
        {
            if (_CheckedIndicatorStyle != value)
            {
                _CheckedIndicatorStyle = value;
                if (CheckStateIcon != null)
                {
                    CheckStateIcon.CheckedIndicatorStyle = value;
                }

                NotifyPropertyChanged(nameof(CheckedIndicatorStyle));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _IsCheckMarkShadowed;
    /// <summary>If true, the graphics of the <see cref="ButtonElement"/> will be drawn an extra time
    /// with Color=<see cref="CheckMarkShadowColor"/> and using Offset=<see cref="CheckMarkShadowOffset"/><para/>
    /// Default value: false</summary>
    public bool IsCheckMarkShadowed
    {
        get => _IsCheckMarkShadowed;
        set
        {
            if (_IsCheckMarkShadowed != value)
            {
                _IsCheckMarkShadowed = value;
                if (CheckStateIcon != null)
                {
                    CheckStateIcon.IsShadowed = value;
                }
                NotifyPropertyChanged(nameof(IsCheckMarkShadowed));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Color _CheckMarkShadowColor;
    public Color CheckMarkShadowColor
    {
        get => _CheckMarkShadowColor;
        set
        {
            if (_CheckMarkShadowColor != value)
            {
                _CheckMarkShadowColor = value;
                if (CheckStateIcon != null)
                {
                    CheckStateIcon.ShadowColor = value;
                }
                NotifyPropertyChanged(nameof(CheckMarkShadowColor));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Point _CheckMarkShadowOffset;
    public Point CheckMarkShadowOffset
    {
        get => _CheckMarkShadowOffset;
        set
        {
            if (_CheckMarkShadowOffset != value)
            {
                _CheckMarkShadowOffset = value;
                if (CheckStateIcon != null)
                {
                    CheckStateIcon.ShadowOffset = value;
                }
                NotifyPropertyChanged(nameof(CheckMarkShadowOffset));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _IsThreeState;
    /// <summary>True if 'null' is a valid value for <see cref="IsChecked"/><para/>
    /// Default value: false</summary>
    public bool IsThreeState
    {
        get => _IsThreeState;
        set
        {
            if (_IsThreeState != value)
            {
                _IsThreeState = value;
                if (!IsThreeState && !IsChecked.HasValue)
                {
                    IsChecked = false;
                }

                NotifyPropertyChanged(nameof(IsThreeState));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool? _IsChecked;
    /// <summary>If <see cref="IsThreeState"/> is false, this value should not be set to null.</summary>
    public bool? IsChecked
    {
        get => _IsChecked;
        set
        {
            if (_IsChecked != value)
            {
                if (!IsThreeState && !value.HasValue)
                {
                    throw new InvalidOperationException($"{nameof(MGCheckBox)}.{nameof(IsChecked)} can only be set to 'null' if {nameof(IsThreeState)} is true.");
                }

                var Previous = IsChecked;
                _IsChecked = value;
                if (CheckStateIcon != null)
                {
                    CheckStateIcon.CheckState = value;
                }
                NotifyPropertyChanged(nameof(IsChecked));
                OnCheckStateChanged?.Invoke(this, new(Previous, IsChecked));

                if (IsChecked.HasValue)
                {
                    if (IsChecked.Value)
                    {
                        OnChecked?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        OnUnchecked?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _IsReadonly;
    /// <summary>If true, the user will be unable to modify <see cref="IsChecked"/> by manually clicking this checkbox.<para/>
    /// Default value: false</summary>
    public bool IsReadonly
    {
        get => _IsReadonly;
        set
        {
            if (_IsReadonly != value)
            {
                _IsReadonly = value;
                NotifyPropertyChanged(nameof(IsReadonly));
            }
        }
    }

    /// <summary>Note: This event is invoked before <see cref="OnChecked"/> / <see cref="OnUnchecked"/></summary>
    public event EventHandler<EventArgs<bool?>> OnCheckStateChanged;
    public event EventHandler<EventArgs> OnChecked;
    public event EventHandler<EventArgs> OnUnchecked;

    private BaseMousePressedEventArgs PressedArgs { get; set; }

    private bool TryToggleCheckedState()
    {
        if (IsReadonly)
        {
            return false;
        }

        IsChecked = GetNextCheckedState(IsChecked, IsThreeState);
        return true;
    }

    public MGCheckBox(MGWindow Window, bool? IsChecked = false)
        : base(Window, MGElementType.CheckBox)
    {
        using (BeginInitializing())
        {
            IsFocusable = true;

            MouseHandler.LMBPressedInside += (sender, e) =>
            {
                PressedArgs = e;
            };
            MouseHandler.LMBReleasedInside += (sender, e) =>
            {
                if (PressedArgs != null)
                {
                    if (!e.IsHandled && TryToggleCheckedState())
                    {
                        e.SetHandledBy(this, false);
                    }

                    PressedArgs = null;
                }
            };
            MouseHandler.ReleasedOutside += (sender, e) =>
            {
                if (PressedArgs != null)
                {
                    PressedArgs = null;
                }
            };

            ButtonElement = new(Window, new(1), MGUniformBorderBrush.Black, x =>
            {
                PressedArgs = null;
                TryToggleCheckedState();
            });
            ButtonElement.IsFocusable = false;
            ButtonElement.MinWidth = 12;
            ButtonElement.SetMinHeight(12, UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            ButtonElement.SetPadding(new(0), UIValueResolutionSource.LocalValue(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

            CheckStateIcon = new(Window) { ManagedParent = this };

            ButtonComponent = new(ButtonElement, false, true, true, true, false, false, false,
                (AvailableBounds, ComponentSize) => ApplyAlignment(AvailableBounds, HorizontalAlignment.Left, VerticalAlignment.Center, ComponentSize.Size));
            CheckStateIconComponent = new(CheckStateIcon, false, true, true, true, false, false, false,
                (AvailableBounds, ComponentSize) => ButtonElement.LayoutBounds.GetCompressed(ButtonElement.BorderThickness));

            AddComponent(ButtonComponent);
            AddComponent(CheckStateIconComponent);

            CheckBoxComponentSize = GetTheme().CheckBoxComponentSize;
            SpacingWidth = DefaultCheckBoxSpacingWidth;
            CheckMarkColor = GetTheme().CheckMarkColor;
            CheckedIndicatorStyle = GetTheme().CheckBoxCheckedIndicatorStyle;
            IsCheckMarkShadowed = false;
            CheckMarkShadowColor = Color.Black;
            CheckMarkShadowOffset = new(0, 1);

            IsThreeState = !IsChecked.HasValue;
            this.IsChecked = IsChecked;
            IsReadonly = false;
        }
    }

    public override bool TryHandleNavigationAction(UINavigationAction action)
    {
        if (action != UINavigationAction.Submit || IsReadonly)
        {
            return false;
        }

        TryToggleCheckedState();
        return true;
    }

    /// <summary><see cref="MGTheme.CheckBoxComponentSize"/> is layout-affecting and <see cref="OnThemeChanged"/> copies it into <see cref="CheckBoxComponentSize"/>
    /// while the size still equals the previous theme's: request a layout pass only when that copy changes the size (backlog task 7).
    /// <see cref="MGTheme.CheckMarkColor"/> and <see cref="MGTheme.CheckBoxCheckedIndicatorStyle"/> are render-only.</summary>
    protected internal override UIInvalidationKind GetThemeInvalidation(MGTheme PreviousTheme, MGTheme CurrentTheme)
        => PreviousTheme == null || CheckBoxComponentSize == PreviousTheme.CheckBoxComponentSize
            ? UIThemeValueInvalidation.ForChange(nameof(MGTheme.CheckBoxComponentSize), CheckBoxComponentSize, (CurrentTheme ?? GetTheme()).CheckBoxComponentSize)
            : UIInvalidationKind.Draw;

    protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
    {
        base.OnThemeChanged(PreviousTheme, CurrentTheme);

        // Follow the theme only for the values still equal to the previous theme's, so that a value set in code or in XAML survives the change.
        if (PreviousTheme == null || CheckBoxComponentSize == PreviousTheme.CheckBoxComponentSize)
        {
            CheckBoxComponentSize = GetTheme().CheckBoxComponentSize;
        }

        if (PreviousTheme == null || CheckMarkColor == PreviousTheme.CheckMarkColor)
        {
            CheckMarkColor = GetTheme().CheckMarkColor;
        }

        if (PreviousTheme == null || CheckedIndicatorStyle == PreviousTheme.CheckBoxCheckedIndicatorStyle)
        {
            CheckedIndicatorStyle = GetTheme().CheckBoxCheckedIndicatorStyle;
        }
    }

    public static void DrawCheckMark(MGDesktop desktop, Rectangle bounds, IUIDrawContext drawContext, float opacity, Point offset, Color color)
    {
        UISymbolDrawing.DrawCheckMark(drawContext, offset.ToVector2(), bounds, color * opacity);
    }

    public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
    {

    }
}