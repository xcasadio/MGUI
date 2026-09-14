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

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private IFillBrush _CheckedBackgroundBrush;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _HasCheckedBackgroundBrush;

    /// <summary>The background brush to use for this <see cref="MGToggleButton"/> when <see cref="IsChecked"/> is true (ADR-0008).<para/>
    /// Reaches drawing through <see cref="MGElement.BackgroundBrush"/>'s <see cref="VisualStateBrush{TDataType}.CheckedValue"/> (see
    /// <see cref="MGElement.ResolveBackgroundUnderlay"/>), code-only (not a theme DTO field, not an animation target, not a
    /// <see cref="UIResolvedPropertyStore"/> slot). Tracked here on the toggle, not solely on the brush instance: <see cref="MGElement.BackgroundBrush"/>'s
    /// container can be swapped out from under a control -- by an ADR-0005 theme re-application (<c>RefreshThemeBackgroundDefault</c>, run by the framework
    /// immediately before <see cref="OnThemeChanged"/> on every theme change), or by ANY other whole-container write reaching
    /// <see cref="MGElement.BackgroundBrush"/>'s public setter, <see cref="MGElement.SetBackground"/>, or the pilot's Whole-slot resolution (e.g.
    /// <c>MGExpander.ExpanderButtonBackgroundBrush</c>'s setter, or a style/pilot resolver assigning the whole brush) -- for a fresh clone that never
    /// carried the checked value. The toggle-level field survives every such swap and <see cref="OnBackgroundBrushContainerReplaced"/>
    /// re-applies it (via <see cref="SyncCheckedBackgroundValue"/>) unconditionally, so the property and the drawn brush never diverge, regardless of
    /// who replaced the container. Falls back to <see cref="VisualStateSetting{TDataType}.SelectedValue"/> while unset, and setting it to
    /// <see langword="null"/> restores that fallback.</summary>
    public IFillBrush CheckedBackgroundBrush
    {
        get => _HasCheckedBackgroundBrush ? _CheckedBackgroundBrush : BackgroundBrush.SelectedValue;
        set
        {
            if (!Equals(_HasCheckedBackgroundBrush ? _CheckedBackgroundBrush : null, value))
            {
                _HasCheckedBackgroundBrush = value != null;
                _CheckedBackgroundBrush = value;
                NotifyPropertyChanged(nameof(CheckedBackgroundBrush));
                SyncCheckedBackgroundValue();
            }
        }
    }

    /// <summary>Writes the code-set <see cref="CheckedBackgroundBrush"/> onto <see cref="MGElement.BackgroundBrush"/>'s
    /// <see cref="VisualStateBrush{TDataType}.CheckedValue"/> (or clears it, when unset) -- called from <see cref="CheckedBackgroundBrush"/>'s
    /// setter and from <see cref="OnBackgroundBrushContainerReplaced"/>, unconditionally, so no whole-container swap -- a theme change or any
    /// other caller that replaces <see cref="MGElement.BackgroundBrush"/> -- can leave the checked value behind on a container it swapped away
    /// (ADR-0008; generalised from an <see cref="OnThemeChanged"/>-only call to every container replacement; also guarded
    /// against a null <see cref="MGElement.BackgroundBrush"/>, since the container replacement hook that calls this also fires when the whole
    /// container is assigned/resolves to <see langword="null"/>, which is a valid, previously-unguarded state -- the framework's own sub-slot
    /// re-application for the same container swap already skips its work the same way when the container is null).</summary>
    private void SyncCheckedBackgroundValue()
    {
        if (BackgroundBrush == null)
        {
            return;
        }

        if (_HasCheckedBackgroundBrush)
        {
            BackgroundBrush.CheckedValue = _CheckedBackgroundBrush;
        }
        else
        {
            BackgroundBrush.ClearCheckedValue();
        }
    }

    /// <summary>Re-applies a code-set <see cref="CheckedBackgroundBrush"/> every time
    /// <see cref="MGElement.BackgroundBrush"/>'s container is replaced by a new instance, regardless of who replaced it --
    /// the property setter, <see cref="MGElement.SetBackground"/>, a pilot/style Whole-slot resolution, or a theme re-application.
    /// Without this, <see cref="CheckedBackgroundBrush"/>'s getter (which reads the toggle-level field, not the container) could
    /// keep reporting a brush that the fresh container never received and that is therefore not what gets drawn.</summary>
    protected override void OnBackgroundBrushContainerReplaced() => SyncCheckedBackgroundValue();

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Color? _CheckedTextForeground;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private bool _HasCheckedTextForeground;

    /// <summary>A foreground color to use on child content of this <see cref="MGToggleButton"/> when <see cref="IsChecked"/> is true (ADR-0008).<para/>
    /// <see cref="MGElement.DefaultTextForeground"/> is a <see cref="VisualStateSetting{TDataType}"/>, not a <see cref="VisualStateBrush{TDataType}"/>, so
    /// it has no <c>CheckedValue</c> slot of its own: this property is tracked on the toggle instead, then re-synchronised onto
    /// <see cref="MGElement.DefaultTextForeground"/>'s <see cref="VisualStateSetting{TDataType}.SelectedValue"/> slot (via <see cref="SyncCheckedTextForegroundSlot"/>)
    /// so <see cref="MGElement.IsSelected"/>/<see cref="IsChecked"/> keeps resolving the checked text through the existing Selected sub-slot pilot write
    /// (<see cref="MGElement.SetDefaultTextForegroundSlot"/>). Falls back to the theme's selected colour while unset, and setting it to
    /// <see langword="null"/> restores that fallback; a theme change re-applies a code-set value instead of the new theme's colour.</summary>
    public Color? CheckedTextForeground
    {
        get => _HasCheckedTextForeground ? _CheckedTextForeground : DefaultTextForeground.SelectedValue;
        set
        {
            if (!Equals(_HasCheckedTextForeground ? _CheckedTextForeground : null, value))
            {
                _HasCheckedTextForeground = value.HasValue;
                _CheckedTextForeground = value;
                NotifyPropertyChanged(nameof(CheckedTextForeground));
                SyncCheckedTextForegroundSlot(UIValueResolutionSource.Default(UIInvalidationKind.Draw));
            }
        }
    }

    /// <summary>Writes the Selected sub-slot of <see cref="MGElement.DefaultTextForeground"/> with the code-set <see cref="CheckedTextForeground"/>
    /// when one is set, else with the theme's fallback colour (the value the Selected slot always held before this slice) -- called from the
    /// constructor, from <see cref="CheckedTextForeground"/>'s setter, and from <see cref="OnThemeChanged"/> so a theme change cannot overwrite
    /// a code-set checked colour with its own <c>SelectedValue</c> (ADR-0008).<para/>
    /// A code-set value is always written at <see cref="UIValuePrecedence.LocalValue"/> (via <see cref="UIValueResolutionSource.LocalValue"/>),
    /// not at <paramref name="source"/>'s own precedence -- <paramref name="source"/> is only <see cref="UIValueResolutionSource.Default"/> or
    /// <see cref="UIValueResolutionSource.Theme"/>, both LOWER precedence than a real code override needs, so a code-set value could otherwise stay
    /// dormant behind an earlier <see cref="UIValueResolutionSource.Theme"/> contribution on the same slot (e.g. one this same method already wrote from
    /// a prior theme change) and never reach the physical field. When unset, the leftover <see cref="UIValueSourceKind.LocalValue"/> contribution (if
    /// any) is cleared first, via <see cref="MGElement.ClearPilotSource(UIPilotProperty, UIValueSlot, UIValueSourceKind)"/>, so the fallback write below can win again.</summary>
    private void SyncCheckedTextForegroundSlot(UIValueResolutionSource source)
    {
        if (_HasCheckedTextForeground)
        {
            SetDefaultTextForegroundSlot(UIValueSlot.Selected, _CheckedTextForeground, UIValueResolutionSource.LocalValue(source.Invalidation));
        }
        else
        {
            ClearPilotSource(UIPilotProperty.DefaultTextForeground, UIValueSlot.Selected, UIValueSourceKind.LocalValue);
            SetDefaultTextForegroundSlot(UIValueSlot.Selected, GetTheme()?.TextBlockFallbackForeground.GetValue(true).NormalValue, source);
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
                NotifyPropertyChanged(nameof(IsChecked));
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
            BorderElement.OnBorderBrushChanged += (sender, e) => { NotifyPropertyChanged(nameof(BorderBrush)); };
            BorderElement.OnBorderThicknessChanged += (sender, e) => { NotifyPropertyChanged(nameof(BorderThickness)); };
            BorderElement.OnCornerRadiusChanged += (sender, e) => { NotifyPropertyChanged(nameof(CornerRadius)); };

            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            SetPadding(new(4, 2, 4, 2), UIValueResolutionSource.Default(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
            SyncCheckedTextForegroundSlot(UIValueResolutionSource.Default(UIInvalidationKind.Draw));
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

            // ADR-0008: RefreshThemeBackgroundDefault (run by the framework immediately before this override, on every
            // theme change) and the SetBackground call above can each swap BackgroundBrush for a fresh clone that never carried the checked
            // value; both now go through MGElement.ApplyBackgroundEffective, which calls OnBackgroundBrushContainerReplaced (overridden below
            // to call SyncCheckedBackgroundValue) on every such swap, so no explicit call is needed here any more.
            SyncCheckedTextForegroundSlot(UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        }

        ThemeTransitions.Apply(this, CurrentTheme?.Animation);
    }

    /// <summary>ADR-0008: draws <see cref="MGElement.BackgroundBrush"/>'s <see cref="VisualStateBrush{TDataType}.CheckedValue"/> while
    /// <see cref="IsChecked"/> and one is set, exactly like <see cref="VisualStateBrush{TDataType}.GetUnderlay(PrimaryVisualState)"/> otherwise
    /// (unchecked, or no <see cref="CheckedBackgroundBrush"/> set -- falls back to <see cref="VisualStateSetting{TDataType}.SelectedValue"/>
    /// the same way <see cref="MGElement.ResolveBackgroundUnderlay"/>'s base implementation would).</summary>
    protected override IFillBrush ResolveBackgroundUnderlay(VisualStateFillBrush brush, PrimaryVisualState state) => brush.GetValue(state, IsChecked);

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