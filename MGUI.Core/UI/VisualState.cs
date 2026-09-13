using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using MGUI.Core.UI.Brushes;
using MGUI.Shared.Rendering;
using MGUI.Core.UI.Shapes;
using System.Diagnostics;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;

namespace MGUI.Core.UI;

public enum PrimaryVisualState
{
    /// <summary>Highest priority. I.E. if an <see cref="MGElement"/> is both Disabled and Selected, it is treated as Disabled.<br/>
    /// See also: <see cref="MGElement.IsEnabled"/>, <see cref="MGElement.DerivedIsEnabled"/></summary>
    Disabled,
    /// <summary>See also: <see cref="MGElement.IsSelected"/>, <see cref="MGElement.DerivedIsSelected"/></summary>
    Selected,
    /// <summary>Used when the element has keyboard focus and focus visuals should be shown.</summary>
    Focused,
    Normal
}

public enum SecondaryVisualState
{
    /// <summary>Highest priority. I.E. if an <see cref="MGElement"/> is both Pressed and Hovered, it is treated as Pressed.<para/>
    /// Indicates that the Left MouseButton is currently pressed overtop of the <see cref="MGElement"/></summary>
    Pressed,
    Hovered,
    None
}

public readonly record struct VisualState(PrimaryVisualState Primary, SecondaryVisualState Secondary)
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]public bool IsDisabled => Primary == PrimaryVisualState.Disabled;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]public bool IsSelected => Primary == PrimaryVisualState.Selected;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]public bool IsFocused => Primary == PrimaryVisualState.Focused;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]public bool IsPressed => Secondary == SecondaryVisualState.Pressed;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]public bool IsHovered => Secondary == SecondaryVisualState.Hovered;
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]public bool IsPressedOrHovered => IsPressed || IsHovered;

    public SecondaryVisualState GetSecondaryState(bool SpoofIsPressed, bool SpoofIsHovered)
    {
        if (SpoofIsPressed)
        {
            return SecondaryVisualState.Pressed;
        }
        else if (SpoofIsHovered && Secondary != SecondaryVisualState.Pressed)
        {
            return SecondaryVisualState.Hovered;
        }
        else
        {
            return Secondary;
        }
    }
}

public class VisualStateSetting<TDataType> : ViewModelBase
{
    private readonly EqualityComparer<TDataType> EqualityComparer = EqualityComparer<TDataType>.Default;

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private TDataType _DisabledValue;
    public TDataType DisabledValue
    {
        get => _DisabledValue;
        set
        {
            if (!EqualityComparer.Equals(_DisabledValue, value))
            {
                _DisabledValue = value;
                NotifyPropertyChanged(nameof(DisabledValue));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private TDataType _SelectedValue;
    public TDataType SelectedValue
    {
        get => _SelectedValue;
        set
        {
            if (!EqualityComparer.Equals(_SelectedValue, value))
            {
                _SelectedValue = value;
                NotifyPropertyChanged(nameof(SelectedValue));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private TDataType _FocusedValue;
    public TDataType FocusedValue
    {
        get => _FocusedValue;
        set
        {
            if (!EqualityComparer.Equals(_FocusedValue, value))
            {
                _FocusedValue = value;
                NotifyPropertyChanged(nameof(FocusedValue));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private TDataType _NormalValue;
    public TDataType NormalValue
    {
        get => _NormalValue;
        set
        {
            if (!EqualityComparer.Equals(_NormalValue, value))
            {
                _NormalValue = value;
                NotifyPropertyChanged(nameof(NormalValue));
            }
        }
    }

    public TDataType GetValue(PrimaryVisualState State)
        => State switch
        {
            PrimaryVisualState.Disabled => DisabledValue,
            PrimaryVisualState.Selected => SelectedValue,
            PrimaryVisualState.Focused => FocusedValue,
            PrimaryVisualState.Normal => NormalValue,
            _ => throw new NotImplementedException($"Unrecognized {nameof(PrimaryVisualState)}: {State}")
        };

    public void SetAll(TDataType Value)
    {
        DisabledValue = Value;
        SelectedValue = Value;
        FocusedValue = Value;
        NormalValue = Value;
    }

    public VisualStateSetting(TDataType Value)
        : this(Value, Value, Value) { }

    public VisualStateSetting(TDataType NormalValue, TDataType SelectedValue, TDataType DisabledValue)
        : this(NormalValue, SelectedValue, SelectedValue, DisabledValue) { }

    public VisualStateSetting(TDataType NormalValue, TDataType SelectedValue, TDataType FocusedValue, TDataType DisabledValue)
    {
        this.NormalValue = NormalValue;
        this.SelectedValue = SelectedValue;
        this.FocusedValue = FocusedValue;
        this.DisabledValue = DisabledValue;
    }

    public VisualStateSetting<TDataType> GetCopy() => new(NormalValue, SelectedValue, FocusedValue, DisabledValue);
}

public enum PressedModifierType
{
    Darken,
    Brighten
}

public abstract class VisualStateBrush<TDataType> : VisualStateSetting<TDataType>, ICloneable
{
    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private Color? _HoveredColor;
    /// <summary>An overlay color that is drawn overtop if the mouse is currently hovering the <see cref="MGElement"/>.<br/>
    /// Recommended to use a transparent color.<para/>
    /// If the mouse is also pressed overtop of the <see cref="MGElement"/>, then this color is further adjusted based on <see cref="PressedModifierType"/> and <see cref="PressedModifier"/></summary>
    public Color? FocusedColor
    {
        get => _HoveredColor;
        set
        {
            if (_HoveredColor != value)
            {
                _HoveredColor = value;
                UpdateFocusedOverlay();
                UpdatePressedOverlay();
                NotifyPropertyChanged(nameof(FocusedColor));
                NotifyPropertyChanged(nameof(HoveredColorOverlay));
                NotifyPropertyChanged(nameof(PressedColorOverlay));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private float _PressedModifier;
    /// <summary>A percentage to darken or brighten (depending on <see cref="PressedModifierType"/>) the <see cref="FocusedColor"/> by 
    /// when the mouse is currently pressed, but not yet released, overtop of the <see cref="MGElement"/>.</summary>
    public float PressedModifier
    {
        get => _PressedModifier;
        set
        {
            if (_PressedModifier != value)
            {
                _PressedModifier = value;
                UpdatePressedOverlay();
                NotifyPropertyChanged(nameof(PressedModifier));
            }
        }
    }

    [DebuggerBrowsable(DebuggerBrowsableState.Never)]
    private PressedModifierType _PressedModifierType;
    public PressedModifierType PressedModifierType
    {
        get => _PressedModifierType;
        set
        {
            if (_PressedModifierType != value)
            {
                _PressedModifierType = value;
                UpdatePressedOverlay();
                NotifyPropertyChanged(nameof(PressedModifierType));
            }
        }
    }

    private void UpdateFocusedOverlay()
    {
        if (FocusedColor.HasValue)
        {
            HoveredFillOverlay = new MGSolidFillBrush(FocusedColor.Value);
            HoveredBorderOverlay = new(HoveredFillOverlay);
        }
        else
        {
            HoveredFillOverlay = SolidFillBrushes.Transparent;
            HoveredBorderOverlay = MGUniformBorderBrush.Transparent;
        }
        NotifyPropertyChanged(nameof(HoveredColorOverlay));
        NotifyPropertyChanged(nameof(HoveredFillOverlay));
        NotifyPropertyChanged(nameof(HoveredBorderOverlay));
    }

    private void UpdatePressedOverlay()
    {
        if (FocusedColor.HasValue)
        {
            PressedColorOverlay = PressedModifierType switch
            {
                PressedModifierType.Darken => FocusedColor.Value.Darken(PressedModifier),
                PressedModifierType.Brighten => FocusedColor.Value.Brighten(PressedModifier),
                _ => throw new NotImplementedException($"Unrecognized {nameof(PressedModifierType)}: {PressedModifierType}")
            };
            PressedFillOverlay = new(PressedColorOverlay.Value);
            PressedBorderOverlay = new(PressedFillOverlay);
        }
        else
        {
            PressedColorOverlay = null;
            PressedFillOverlay = SolidFillBrushes.Transparent;
            PressedBorderOverlay = MGUniformBorderBrush.Transparent;
        }
        NotifyPropertyChanged(nameof(PressedColorOverlay));
        NotifyPropertyChanged(nameof(PressedFillOverlay));
        NotifyPropertyChanged(nameof(PressedBorderOverlay));
    }

    private float _OverlayOpacity = 1f;
    /// <summary>Opacity applied to the Hovered / Pressed overlays when they are drawn (<see cref="VisualStateFillBrush.DrawFillOverlay"/>, ADR-0007 decision 4):
    /// 1 (the default) paints them as before, 0 hides them; the <c>Background.Overlay</c> animation target animates it, and a transition on that
    /// path fades the overlay in when the element becomes hovered or pressed. Clamped to [0, 1].</summary>
    public float OverlayOpacity
    {
        get => _OverlayOpacity;
        set
        {
            var clamped = float.IsNaN(value) ? 1f : Math.Clamp(value, 0f, 1f);
            if (_OverlayOpacity != clamped)
            {
                _OverlayOpacity = clamped;
                NotifyPropertyChanged(nameof(OverlayOpacity));
            }
        }
    }

    private Color? HoveredColorOverlay => FocusedColor;
    private Color? PressedColorOverlay { get; set; }

    private MGSolidFillBrush HoveredFillOverlay { get; set; }
    private MGSolidFillBrush PressedFillOverlay { get; set; }

    private MGUniformBorderBrush HoveredBorderOverlay { get; set; }
    private MGUniformBorderBrush PressedBorderOverlay { get; set; }

    public TDataType GetUnderlay(PrimaryVisualState State) => GetValue(State);

    /// <summary>Retrieves the <see cref="Color"/> used by <see cref="GetFillOverlay(SecondaryVisualState)"/> / <see cref="GetBorderOverlay(SecondaryVisualState)"/></summary>
    public Color? GetColorOverlay(SecondaryVisualState State) =>
        State switch
        {
            SecondaryVisualState.Pressed => PressedColorOverlay,
            SecondaryVisualState.Hovered => FocusedColor,
            SecondaryVisualState.None => null,
            _ => throw new NotImplementedException($"Unrecognized {nameof(SecondaryVisualState)}: {State}")
        };

    /// <summary>Returns a <see cref="IFillBrush"/> that should be rendered overtop of the element's graphics, not including the border's bounds.</summary>
    public MGSolidFillBrush? GetFillOverlay(SecondaryVisualState State) =>
        State switch
        {
            SecondaryVisualState.Pressed => PressedFillOverlay,
            SecondaryVisualState.Hovered => HoveredFillOverlay,
            SecondaryVisualState.None => null,
            _ => throw new NotImplementedException($"Unrecognized {nameof(SecondaryVisualState)}: {State}")
        };

    /// <summary>Returns a <see cref="IBorderBrush"/> that should be rendered overtop of the border portion of the element's graphics.</summary>
    public MGUniformBorderBrush? GetBorderOverlay(SecondaryVisualState State) =>
        State switch
        {
            SecondaryVisualState.Pressed => PressedBorderOverlay,
            SecondaryVisualState.Hovered => HoveredBorderOverlay,
            SecondaryVisualState.None => null,
            _ => throw new NotImplementedException($"Unrecognized {nameof(SecondaryVisualState)}: {State}")
        };

    protected VisualStateBrush(TDataType NormalValue, TDataType SelectedValue, TDataType DisabledValue, 
        Color? HoveredColor, PressedModifierType PressedModifierType, float PressedModifier)
        : base(NormalValue, SelectedValue, SelectedValue, DisabledValue)
    {
        FocusedColor = HoveredColor;
        this.PressedModifierType = PressedModifierType;
        this.PressedModifier = PressedModifier;
    }

    protected VisualStateBrush(TDataType NormalValue, TDataType SelectedValue, TDataType FocusedValue, TDataType DisabledValue,
        Color? HoveredColor, PressedModifierType PressedModifierType, float PressedModifier)
        : base(NormalValue, SelectedValue, FocusedValue, DisabledValue)
    {
        FocusedColor = HoveredColor;
        this.PressedModifierType = PressedModifierType;
        this.PressedModifier = PressedModifier;
    }

    public abstract object Clone();
}

/// <summary>A wrapper class for multiple <see cref="IFillBrush"/>es, where a specific one is chosen based on an <see cref="MGElement"/>'s <see cref="VisualState"/></summary>
public class VisualStateFillBrush : VisualStateBrush<IFillBrush>
{
    public VisualStateFillBrush(IFillBrush Brush)
        : this(Brush, null, PressedModifierType.Darken, 0.06f) { }

    public VisualStateFillBrush(IFillBrush Brush, Color? HoveredColor, PressedModifierType PressedModifierType, float PressedModifier)
        : this(Brush, Brush, Brush, HoveredColor, PressedModifierType, PressedModifier) { }

    public VisualStateFillBrush(IFillBrush NormalBrush, IFillBrush SelectedBrush, IFillBrush DisabledBrush, Color? HoveredColor, PressedModifierType PressedModifierType, float PressedModifier)
        : base(NormalBrush, SelectedBrush, DisabledBrush, HoveredColor, PressedModifierType, PressedModifier) { }

    public VisualStateFillBrush(IFillBrush NormalBrush, IFillBrush SelectedBrush, IFillBrush FocusedBrush, IFillBrush DisabledBrush, Color? HoveredColor, PressedModifierType PressedModifierType, float PressedModifier)
        : base(NormalBrush, SelectedBrush, FocusedBrush, DisabledBrush, HoveredColor, PressedModifierType, PressedModifier) { }

    private VisualStateFillBrush(VisualStateFillBrush InheritFrom)
        : base(InheritFrom.NormalValue?.Copy(), InheritFrom.SelectedValue?.Copy(), InheritFrom.FocusedValue?.Copy(), InheritFrom.DisabledValue?.Copy(),
            InheritFrom.FocusedColor, InheritFrom.PressedModifierType, InheritFrom.PressedModifier)
    {
        OverlayOpacity = InheritFrom.OverlayOpacity;
    }

    /// <summary>Draws the fill overlay of <paramref name="State"/> (Hovered / Pressed) with <see cref="VisualStateBrush{TDataType}.OverlayOpacity"/> applied; nothing for <see cref="SecondaryVisualState.None"/> or an opacity of 0.</summary>
    public void DrawFillOverlay(ElementDrawArgs DA, SecondaryVisualState State, MGElement Element, Rectangle Bounds)
    {
        var overlay = GetFillOverlay(State);
        var opacity = OverlayOpacity;
        if (overlay == null || opacity <= 0f)
            return;
        overlay.Value.Draw(opacity >= 1f ? DA : DA.SetOpacity(DA.Opacity * opacity), Element, Bounds);
    }

    /// <summary>Rounded variant of <see cref="DrawFillOverlay(ElementDrawArgs, SecondaryVisualState, MGElement, Rectangle)"/>.</summary>
    public void DrawFillOverlay(ElementDrawArgs DA, SecondaryVisualState State, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        var overlay = GetFillOverlay(State);
        var opacity = OverlayOpacity;
        if (overlay == null || opacity <= 0f)
            return;
        overlay.Value.Draw(opacity >= 1f ? DA : DA.SetOpacity(DA.Opacity * opacity), Element, Shape, Geometry);
    }

    /// <summary>Draws the border overlay of <paramref name="State"/> with <see cref="VisualStateBrush{TDataType}.OverlayOpacity"/> applied.</summary>
    public void DrawBorderOverlay(ElementDrawArgs DA, SecondaryVisualState State, MGElement Element, Rectangle Bounds, MonoGame.Extended.Thickness BorderThickness)
    {
        var overlay = GetBorderOverlay(State);
        var opacity = OverlayOpacity;
        if (overlay == null || opacity <= 0f)
            return;
        overlay.Value.Draw(opacity >= 1f ? DA : DA.SetOpacity(DA.Opacity * opacity), Element, Bounds, BorderThickness);
    }

    /// <summary>Rounded variant of <see cref="DrawBorderOverlay(ElementDrawArgs, SecondaryVisualState, MGElement, Rectangle, MonoGame.Extended.Thickness)"/>.</summary>
    public void DrawBorderOverlay(ElementDrawArgs DA, SecondaryVisualState State, MGElement Element, MGBoxShape Shape, MGBoxGeometry Geometry)
    {
        var overlay = GetBorderOverlay(State);
        var opacity = OverlayOpacity;
        if (overlay == null || opacity <= 0f)
            return;
        overlay.Value.Draw(opacity >= 1f ? DA : DA.SetOpacity(DA.Opacity * opacity), Element, Shape, Geometry);
    }

    /// <summary>Forwards the per-frame lifecycle call to each distinct <see cref="IFillBrush"/> held by this wrapper
    /// (<see cref="VisualStateSetting{TDataType}.NormalValue"/>, <see cref="VisualStateSetting{TDataType}.SelectedValue"/>,
    /// <see cref="VisualStateSetting{TDataType}.FocusedValue"/>, <see cref="VisualStateSetting{TDataType}.DisabledValue"/>),
    /// via <see cref="PaintLifecycle"/>.<para/>
    /// The constructors that take a single brush store the same instance in several states, so instances are deduplicated by reference
    /// within this wrapper (kept regardless of <see cref="UpdateBaseArgs.PaintRegistry"/>); a stateful paint is additionally deduplicated
    /// against every other slot/element that references it for the frame via <see cref="PaintLifecycle"/>.<para/>
    /// The solid hover/pressed overlays are stateless and are not ticked.</summary>
    public void Update(UpdateBaseArgs UA)
    {
        var normal = NormalValue;
        var selected = SelectedValue;
        var focused = FocusedValue;
        var disabled = DisabledValue;

        PaintLifecycle.Update(normal, UA);

        if (selected != null && !ReferenceEquals(selected, normal))
        {
            PaintLifecycle.Update(selected, UA);
        }

        if (focused != null && !ReferenceEquals(focused, normal) && !ReferenceEquals(focused, selected))
        {
            PaintLifecycle.Update(focused, UA);
        }

        if (disabled != null && !ReferenceEquals(disabled, normal) && !ReferenceEquals(disabled, selected) && !ReferenceEquals(disabled, focused))
        {
            PaintLifecycle.Update(disabled, UA);
        }
    }

    public VisualStateFillBrush Copy() => new(this);
    public override object Clone() => Copy();
}

/// <summary>A wrapper class for multiple <see cref="Color"/>s, where a specific one is chosen based on an <see cref="MGElement"/>'s <see cref="VisualState"/></summary>
public class VisualStateColorBrush : VisualStateBrush<Color>
{
    public VisualStateColorBrush(Color Color)
        : this(Color, null, PressedModifierType.Darken, 0.06f) { }

    public VisualStateColorBrush(Color Color, Color? HoveredColor, PressedModifierType PressedModifierType, float PressedModifier)
        : this(Color, Color, Color, HoveredColor, PressedModifierType, PressedModifier) { }

    public VisualStateColorBrush(Color NormalColor, Color SelectedColor, Color DisabledColor, Color? HoveredColor, PressedModifierType PressedModifierType, float PressedModifier)
        : base(NormalColor, SelectedColor, DisabledColor, HoveredColor, PressedModifierType, PressedModifier) { }

    public VisualStateColorBrush(Color NormalColor, Color SelectedColor, Color FocusedColorValue, Color DisabledColor, Color? HoveredColor, PressedModifierType PressedModifierType, float PressedModifier)
        : base(NormalColor, SelectedColor, FocusedColorValue, DisabledColor, HoveredColor, PressedModifierType, PressedModifier) { }

    private VisualStateColorBrush(VisualStateColorBrush InheritFrom)
        : base(InheritFrom.NormalValue, InheritFrom.SelectedValue, InheritFrom.FocusedValue, InheritFrom.DisabledValue,
            InheritFrom.FocusedColor, InheritFrom.PressedModifierType, InheritFrom.PressedModifier)
    {
        OverlayOpacity = InheritFrom.OverlayOpacity;
    }

    public VisualStateColorBrush Copy() => new(this);
    public override object Clone() => Copy();
}