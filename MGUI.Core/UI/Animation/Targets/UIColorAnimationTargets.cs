using MGUI.Core.UI.Brushes.BorderBrushes;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI.Animation.Targets;

/// <summary>
/// The solid-colour animation targets (S5; ADR-0006): the background slots (<see cref="Paths.Background"/> and its Selected, Disabled and
/// Focused variants), the text foreground of an <see cref="MGTextBlock"/> (<see cref="Paths.Foreground"/>), the inherited text foreground of
/// any element (<see cref="Paths.TextForeground"/>) and the uniform border colour (<see cref="Paths.BorderBrush"/>).<para/>
/// All are store-backed: every tick writes a new <see cref="MGSolidFillBrush"/> (or <see cref="MGUniformBorderBrush"/>) through the tagged slot
/// setter with the <c>Animation</c> source, and restoring clears that contribution. Only solid brushes are interpolated: starting an
/// animation on a gradient, texture or nine-slice fails with an explicit <see cref="InvalidOperationException"/>. Each tick boxes the solid
/// fill (an interface slot) and allocates the border brush: an accepted cost, documented in Docs/Tasks/animation-tasks.md.<para/>
/// All are observable (S6): a transition follows the container held by the element and the sub-field inside it.
/// </summary>
public static class UIColorAnimationTargets
{
    /// <summary>The registered property paths.</summary>
    public static class Paths
    {
        public const string Background = "Background";
        public const string BackgroundSelected = "Background.Selected";
        public const string BackgroundDisabled = "Background.Disabled";
        public const string BackgroundFocused = "Background.Focused";
        public const string Foreground = "Foreground";
        public const string TextForeground = "TextForeground";
        public const string BorderBrush = "BorderBrush";
    }

    internal static void RegisterAll()
    {
        UIAnimationTargets.Register(new BackgroundSlotTarget(Paths.Background, UIValueSlot.Normal, nameof(VisualStateFillBrush.NormalValue)));
        UIAnimationTargets.Register(new BackgroundSlotTarget(Paths.BackgroundSelected, UIValueSlot.Selected, nameof(VisualStateFillBrush.SelectedValue)));
        UIAnimationTargets.Register(new BackgroundSlotTarget(Paths.BackgroundDisabled, UIValueSlot.Disabled, nameof(VisualStateFillBrush.DisabledValue)));
        UIAnimationTargets.Register(new BackgroundSlotTarget(Paths.BackgroundFocused, UIValueSlot.Focused, nameof(VisualStateFillBrush.FocusedValue)));
        UIAnimationTargets.Register(new ForegroundTarget());
        UIAnimationTargets.Register(new TextForegroundTarget());
        UIAnimationTargets.Register(new BorderBrushTarget());
    }

    private static UIValueResolutionSource AnimationSource(UIPilotProperty pilot, string animationName)
        => UIValueResolutionSource.Animation(UIPilotPropertyResolver.KindOf(pilot), animationName);

    /// <summary>Restores a colour slot: clears the Animation contribution and, when nothing else was ever recorded for that slot (the
    /// container was written whole, by a constructor or a theme, and its field only lived inside the object), writes the base colour back
    /// under the source of the whole container, so the value does not stay frozen on the last animated colour (the resolved value store keeps
    /// the CLR value when the last contribution of a slot is removed, ADR-0005).</summary>
    internal static void RestoreSlot(MGElement owner, UIPilotProperty pilot, UIValueSlot slot, Action<UIValueResolutionSource> writeBase)
        => UIStoreBackedTargets.RestoreAnimation(owner, pilot, slot, writeBase);

    private static Color RequireSolid(IFillBrush brush, string path, MGElement element)
    {
        if (brush is MGSolidFillBrush solid)
        {
            return solid.Color;
        }

        throw new InvalidOperationException(
            $"'{path}' of {element.GetType().Name} is {(brush == null ? "empty" : "a " + brush.GetType().Name)}: only solid colours ({nameof(MGSolidFillBrush)}) can be animated.");
    }

    private sealed class BackgroundSlotTarget : IUIObservableAnimationTarget<Color>, IUIStoreBackedAnimationTarget<Color>
    {
        private readonly UIValueSlot _slot;
        private readonly string _slotPropertyName;

        public BackgroundSlotTarget(string path, UIValueSlot slot, string slotPropertyName)
        {
            Path = path;
            _slot = slot;
            _slotPropertyName = slotPropertyName;
        }

        public string Path { get; }

        public bool IsStoreBacked => true;

        public Color GetValue(MGElement element)
        {
            VisualStateFillBrush container = element.BackgroundBrush;
            IFillBrush brush = container == null ? null : _slot switch
            {
                UIValueSlot.Selected => container.SelectedValue,
                UIValueSlot.Disabled => container.DisabledValue,
                UIValueSlot.Focused => container.FocusedValue,
                _ => container.NormalValue,
            };
            return RequireSolid(brush, Path, element);
        }

        public Color GetUnderlyingValue(MGElement element) => GetValue(element);

        public UIPilotProperty Pilot => UIPilotProperty.Background;

        public void SetValue(MGElement element, Color value, string animationName)
            => SetValue(element, value, AnimationSource(UIPilotProperty.Background, animationName));

        public void SetValue(MGElement element, Color value, UIValueResolutionSource source)
            => element.SetBackgroundSlot(_slot, new MGSolidFillBrush(value), source);

        public bool ClearContribution(MGElement element, UIValueResolutionSource source, Color baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.Background, _slot, source, s => element.SetBackgroundSlot(_slot, new MGSolidFillBrush(baseValue), s));

        public void RestoreBaseValue(MGElement element, Color baseValue)
            => RestoreSlot(element, UIPilotProperty.Background, _slot, source => element.SetBackgroundSlot(_slot, new MGSolidFillBrush(baseValue), source));

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIContainerSlotSubscription(element, nameof(MGElement.BackgroundBrush), e => e.BackgroundBrush, _slotPropertyName, () => changed(element));
    }

    private sealed class ForegroundTarget : IUIObservableAnimationTarget<Color>, IUIStoreBackedAnimationTarget<Color>
    {
        public string Path => Paths.Foreground;

        public bool IsStoreBacked => true;

        public Color GetValue(MGElement element) => Require(element).Foreground.NormalValue ?? Require(element).ActualForeground;

        public Color GetUnderlyingValue(MGElement element) => GetValue(element);

        public UIPilotProperty Pilot => UIPilotProperty.Foreground;

        public void SetValue(MGElement element, Color value, string animationName)
            => SetValue(element, value, AnimationSource(UIPilotProperty.Foreground, animationName));

        public void SetValue(MGElement element, Color value, UIValueResolutionSource source)
            => Require(element).SetForegroundSlot(UIValueSlot.Normal, value, source);

        public bool ClearContribution(MGElement element, UIValueResolutionSource source, Color baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.Foreground, UIValueSlot.Normal, source, s => Require(element).SetForegroundSlot(UIValueSlot.Normal, baseValue, s));

        public void RestoreBaseValue(MGElement element, Color baseValue)
            => RestoreSlot(element, UIPilotProperty.Foreground, UIValueSlot.Normal, source => Require(element).SetForegroundSlot(UIValueSlot.Normal, baseValue, source));

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIContainerSlotSubscription(Require(element), nameof(MGTextBlock.Foreground), e => ((MGTextBlock)e).Foreground, nameof(VisualStateSetting<Color?>.NormalValue), () => changed(element));

        private static MGTextBlock Require(MGElement element)
            => element as MGTextBlock ?? throw new InvalidOperationException(
                $"'{Paths.Foreground}' animates the text of an {nameof(MGTextBlock)}; {element.GetType().Name} has none. Use '{Paths.TextForeground}' for the inherited text colour of any element.");
    }

    private sealed class TextForegroundTarget : IUIObservableAnimationTarget<Color>, IUIStoreBackedAnimationTarget<Color>
    {
        public string Path => Paths.TextForeground;

        public bool IsStoreBacked => true;

        public Color GetValue(MGElement element)
            => element.DefaultTextForeground?.NormalValue
               ?? element.DerivedDefaultTextForeground
               ?? element.GetTheme().TextBlockFallbackForeground.GetValue(false).GetValue(PrimaryVisualState.Normal);

        public Color GetUnderlyingValue(MGElement element) => GetValue(element);

        public UIPilotProperty Pilot => UIPilotProperty.DefaultTextForeground;

        public void SetValue(MGElement element, Color value, string animationName)
            => SetValue(element, value, AnimationSource(UIPilotProperty.DefaultTextForeground, animationName));

        public void SetValue(MGElement element, Color value, UIValueResolutionSource source)
            => element.SetDefaultTextForegroundSlot(UIValueSlot.Normal, value, source);

        public bool ClearContribution(MGElement element, UIValueResolutionSource source, Color baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, source, s => element.SetDefaultTextForegroundSlot(UIValueSlot.Normal, baseValue, s));

        public void RestoreBaseValue(MGElement element, Color baseValue)
            => RestoreSlot(element, UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, source => element.SetDefaultTextForegroundSlot(UIValueSlot.Normal, baseValue, source));

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIContainerSlotSubscription(element, nameof(MGElement.DefaultTextForeground), e => e.DefaultTextForeground, nameof(VisualStateSetting<Color?>.NormalValue), () => changed(element));
    }

    private sealed class BorderBrushTarget : IUIObservableAnimationTarget<Color>, IUIStoreBackedAnimationTarget<Color>
    {
        public string Path => Paths.BorderBrush;

        public bool IsStoreBacked => true;

        public Color GetValue(MGElement element)
        {
            MGBorder border = RequireBorder(element);
            if (border.BorderBrush is MGUniformBorderBrush uniform)
            {
                return RequireSolid(uniform.Brush, Path, element);
            }

            throw new InvalidOperationException(
                $"'{Paths.BorderBrush}' of {element.GetType().Name} is {(border.BorderBrush == null ? "empty" : "a " + border.BorderBrush.GetType().Name)}: only a uniform border over a solid colour can be animated.");
        }

        public Color GetUnderlyingValue(MGElement element) => GetValue(element);

        public UIPilotProperty Pilot => UIPilotProperty.BorderBrush;

        public void SetValue(MGElement element, Color value, string animationName)
            => SetValue(element, value, AnimationSource(UIPilotProperty.BorderBrush, animationName));

        public void SetValue(MGElement element, Color value, UIValueResolutionSource source)
            => element.SetBorderBrushTagged(new MGUniformBorderBrush(value), source);

        public bool ClearContribution(MGElement element, UIValueResolutionSource source, Color baseValue)
            => UIStoreBackedTargets.Restore(RequireBorder(element), UIPilotProperty.BorderBrush, UIValueSlot.Whole, source, s => element.SetBorderBrushTagged(new MGUniformBorderBrush(baseValue), s));

        public void RestoreBaseValue(MGElement element, Color baseValue)
            => RestoreSlot(RequireBorder(element), UIPilotProperty.BorderBrush, UIValueSlot.Whole, source => element.SetBorderBrushTagged(new MGUniformBorderBrush(baseValue), source));

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(RequireBorder(element), nameof(MGBorder.BorderBrush), () => changed(element));

        private static MGBorder RequireBorder(MGElement element)
            => element.GetBorder() ?? throw new InvalidOperationException(
                $"'{Paths.BorderBrush}' needs a border: {element.GetType().Name} exposes none ({nameof(MGElement.GetBorder)} is null).");
    }
}