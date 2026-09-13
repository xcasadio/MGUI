using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Input.Mouse;
using System.Diagnostics;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI;

public class MGToggleButton : MGSingleContentHost, Animation.States.IUICheckable
{
    internal static bool GetNextCheckedState(bool isChecked) => !isChecked;

    #region Border
    /// <summary>Provides direct access to this element's border.</summary>
    public MGComponent<MGBorder> BorderComponent { get; }
    private MGBorder BorderElement { get; }
    public override MGBorder GetBorder() => BorderElement;

    public IBorderBrush BorderBrush
    {
        get => BorderElement.BorderBrush;
        set => BorderElement.BorderBrush = value;
    }

    public Thickness BorderThickness
    {
        get => BorderElement.BorderThickness;
        set => BorderElement.BorderThickness = value;
    }

    public MGCornerRadius CornerRadius
    {
        get => BorderElement.CornerRadius;
        set => BorderElement.CornerRadius = value;
    }
    #endregion Border

    /// <summary>The background brush to use for this <see cref="MGToggleButton"/> when <see cref="IsChecked"/> is true.<para/>
    /// Equivalent to <see cref="MGElement.BackgroundBrush"/>'s <see cref="VisualStateSetting{TDataType}.SelectedValue"/></summary>
    public IFillBrush CheckedBackgroundBrush
    {
        get => BackgroundBrush.SelectedValue;
        set
        {
            if (BackgroundBrush.SelectedValue != value)
            {
                BackgroundBrush.SelectedValue = value;
                NPC(nameof(CheckedBackgroundBrush));
            }
        }
    }

    /// <summary>A foreground color to use on child content of this <see cref="MGToggleButton"/> when <see cref="IsChecked"/> is true.<para/>
    /// Equivalent to <see cref="MGElement.DefaultTextForeground"/>'s <see cref="VisualStateSetting{TDataType}.SelectedValue"/></summary>
    public Color? CheckedTextForeground
    {
        get => DefaultTextForeground.SelectedValue;
        set
        {
            if (DefaultTextForeground.SelectedValue != value)
            {
                DefaultTextForeground.SelectedValue = value;
                NPC(nameof(CheckedTextForeground));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _IsChecked;
    bool? Animation.States.IUICheckable.IsChecked => IsChecked;

    public bool IsChecked
    {
        get => _IsChecked;
        set
        {
            if (_IsChecked != value)
            {
                var Previous = IsChecked;
                _IsChecked = value;
                NPC(nameof(IsChecked));
                OnCheckStateChanged?.Invoke(this, new(Previous, IsChecked));

                IsSelected = IsChecked;
                if (IsChecked)
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

    /// <summary>Note: This event is invoked before <see cref="OnChecked"/> / <see cref="OnUnchecked"/></summary>
    public event EventHandler<EventArgs<bool>> OnCheckStateChanged;
    public event EventHandler<EventArgs> OnChecked;
    public event EventHandler<EventArgs> OnUnchecked;

    public MGToggleButton(MGWindow Window, bool IsChecked = false)
        : this(Window, new(1), MGUniformBorderBrush.Black, IsChecked) { }

    public MGToggleButton(MGWindow Window, Thickness BorderThickness, IBorderBrush BorderBrush, bool IsChecked)
        : base(Window, MGElementType.ToggleButton)
    {
        using (BeginInitializing())
        {
            IsFocusable = true;
            MinWidth = 16;
            SetMinHeight(16, UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

            BorderElement = new(Window, BorderThickness, BorderBrush);
            BorderComponent = MGComponentBase.Create(BorderElement);
            AddComponent(BorderComponent);
            BorderElement.OnBorderBrushChanged += (sender, e) => { NPC(nameof(BorderBrush)); };
            BorderElement.OnBorderThicknessChanged += (sender, e) => { NPC(nameof(BorderThickness)); };
            BorderElement.OnCornerRadiusChanged += (sender, e) => { NPC(nameof(CornerRadius)); };

            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            SetPadding(new(4, 2, 4, 2), UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            SetDefaultTextForegroundSlot(UIValueSlot.Selected, GetTheme().TextBlockFallbackForeground.GetValue(true).NormalValue, UIValueResolutionSource.Default(UIInvalidationKind.Draw));
            ThemeTransitions.Apply(this, GetTheme()?.Animation);

            MouseHandler.LMBPressedInside += (sender, e) =>
            {
                PressedArgs = e;
            };
            MouseHandler.LMBReleasedInside += (sender, e) =>
            {
                if (PressedArgs != null)
                {
                    this.IsChecked = GetNextCheckedState(this.IsChecked);
                    e.SetHandledBy(this, false);
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

            this.IsChecked = IsChecked;
        }
    }

    private BaseMousePressedEventArgs PressedArgs { get; set; }

    /// <summary>The transitions the theme installs on this toggle (ADR-0007, decision 5): <c>RenderScale</c> and <c>Background.Overlay</c>
    /// over <see cref="MGThemeAnimationSettings"/>; a transition the application attaches on one of these paths is left alone.</summary>
    private readonly Animation.UIThemeTransitions ThemeTransitions = new();

    protected internal override void OnThemeChanged(MGTheme PreviousTheme, MGTheme CurrentTheme)
    {
        base.OnThemeChanged(PreviousTheme, CurrentTheme);

        if (CurrentTheme != null)
        {
            SetBackground(CurrentTheme.GetBackgroundBrush(MGElementType.ToggleButton), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
            SetDefaultTextForegroundSlot(UIValueSlot.Selected, CurrentTheme.TextBlockFallbackForeground.GetValue(true).NormalValue, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        }

        ThemeTransitions.Apply(this, CurrentTheme?.Animation);
    }

    public override bool TryHandleNavigationAction(UINavigationAction action)
    {
        if (action != UINavigationAction.Submit)
        {
            return false;
        }

        IsChecked = GetNextCheckedState(IsChecked);
        return true;
    }
}