using System.ComponentModel;
using MGUI.Core.UI.Brushes.BorderBrushes;
using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.UI.Animation.Targets;

/// <summary>
/// The solid-colour animation targets (S5; ADR-0006): the background slots (<see cref="Paths.Background"/> and its Selected, Disabled and
/// Focused variants), the text foreground of an <see cref="MGTextBlock"/> (<see cref="Paths.Foreground"/>), the inherited text foreground of
/// any element (<see cref="Paths.TextForeground"/>) and the uniform border colour (<see cref="Paths.BorderBrush"/>).<para/>
/// All are store-backed: the four background slots and <see cref="Paths.BorderBrush"/> are <see cref="IUIBrushAnimationTarget{T}"/>s (ADR-0009,
/// W5): a run clones the base brush once and mutates that one clone every tick, with no store write and no allocation past the first tick;
/// <see cref="Foreground"/>/<see cref="TextForeground"/> write a <c>Color?</c>, not a brush, and stay a plain <see cref="IUIStoreBackedAnimationTarget{T}"/>.<para/>
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

    private sealed class BackgroundSlotTarget : IUIObservableAnimationTarget<Color>, IUIBrushAnimationTarget<Color>
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
            var container = element.BackgroundBrush;
            var brush = container == null ? null : _slot switch
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

        public bool TryGetValueBelowAnimation(MGElement element, out Color value)
        {
            if (element.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, _slot, UIValueSourceKind.Animation, out var brush) && brush is MGSolidFillBrush solid)
            {
                value = solid.Color;
                return true;
            }

            value = default;
            return false;
        }

        public void RestoreBaseValue(MGElement element, Color baseValue)
            => RestoreSlot(element, UIPilotProperty.Background, _slot, source => element.SetBackgroundSlot(_slot, new MGSolidFillBrush(baseValue), source));

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new BackgroundSlotSubscription(element, _slotPropertyName, () => changed(element));

        /// <summary>ADR-0009, W5: the clone is a <see cref="IFillBrush.Copy"/> of the solid brush recovered below the animation (read the same
        /// way <see cref="TryGetValueBelowAnimation"/> does, before the write just below records anything under <c>Animation</c>), or a fresh
        /// <see cref="MGSolidFillBrush"/> when nothing could be recovered (a replaced run already occupied this slot).</summary>
        public object BeginAnimatedValue(MGElement element, string animationName)
        {
            var original = element.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, _slot, UIValueSourceKind.Animation, out var below) ? below : null;
            var clone = original is MGSolidFillBrush solid ? (MGSolidFillBrush)solid.Copy() : new MGSolidFillBrush(Color.Transparent);
            element.SetBackgroundSlot(_slot, clone, AnimationSource(UIPilotProperty.Background, animationName));
            return new UIBrushAnimationHandle<IFillBrush>(clone, original);
        }

        /// <summary>Mutates the clone's own <see cref="MGSolidFillBrush.Color"/> setter (ADR-0009, W5: not a special non-notifying path -- see
        /// the "Decisions taken during delivery" note for W5 in ADR-0009 for why the plain, notifying setter was kept). No store write; no
        /// allocation (the setter's own equality check does not box a <see cref="Color"/>, and the notification reuses a cached
        /// <see cref="System.ComponentModel.PropertyChangedEventArgs"/>).</summary>
        public void ApplyAnimatedValue(object handle, Color value)
            => ((MGSolidFillBrush)((UIBrushAnimationHandle<IFillBrush>)handle).Clone).Color = value;

        /// <summary>Restores the exact base instance: re-reads "the value below the animation" first (ADR-0009, W5 -- a container swap mid-run
        /// promotes the swapped-in container's own value into a real contribution, <see cref="MGElement.PromoteSwappedContainerValueBelowRunningAnimation"/>,
        /// so this now sees the NEW theme's base rather than the one captured when the run started), falling back to the instance
        /// <see cref="BeginAnimatedValue"/> captured, then to a fresh brush from <paramref name="baseValue"/>.</summary>
        public void EndAnimatedValue(MGElement element, object handle, Color baseValue)
        {
            var h = (UIBrushAnimationHandle<IFillBrush>)handle;
            var restored = (element.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, _slot, UIValueSourceKind.Animation, out var below) ? below : null)
                ?? h.Original ?? new MGSolidFillBrush(baseValue);
            RestoreSlot(element, UIPilotProperty.Background, _slot, source => element.SetBackgroundSlot(_slot, restored, source));
        }
    }

    private sealed class ForegroundTarget : IUIObservableAnimationTarget<Color>, IUIStoreBackedAnimationTarget<Color>
    {
        public string Path => Paths.Foreground;

        public bool IsStoreBacked => true;

        public Type RequiredOwnerType => typeof(MGTextBlock);

        public Color GetValue(MGElement element) => Require(element).Foreground.NormalValue ?? Require(element).ActualForeground;

        public Color GetUnderlyingValue(MGElement element) => GetValue(element);

        public UIPilotProperty Pilot => UIPilotProperty.Foreground;

        public void SetValue(MGElement element, Color value, string animationName)
            => SetValue(element, value, AnimationSource(UIPilotProperty.Foreground, animationName));

        public void SetValue(MGElement element, Color value, UIValueResolutionSource source)
            => Require(element).SetForegroundSlot(UIValueSlot.Normal, value, source);

        public bool ClearContribution(MGElement element, UIValueResolutionSource source, Color baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.Foreground, UIValueSlot.Normal, source, s => Require(element).SetForegroundSlot(UIValueSlot.Normal, baseValue, s));

        public bool TryGetValueBelowAnimation(MGElement element, out Color value)
        {
            if (element.TryGetResolvedPilotValueExcluding<Color?>(UIPilotProperty.Foreground, UIValueSlot.Normal, UIValueSourceKind.Animation, out var color) && color.HasValue)
            {
                value = color.Value;
                return true;
            }

            value = default;
            return false;
        }

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

        public bool TryGetValueBelowAnimation(MGElement element, out Color value)
        {
            if (element.TryGetResolvedPilotValueExcluding<Color?>(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.Animation, out var color) && color.HasValue)
            {
                value = color.Value;
                return true;
            }

            value = default;
            return false;
        }

        public void RestoreBaseValue(MGElement element, Color baseValue)
            => RestoreSlot(element, UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, source => element.SetDefaultTextForegroundSlot(UIValueSlot.Normal, baseValue, source));

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIContainerSlotSubscription(element, nameof(MGElement.DefaultTextForeground), e => e.DefaultTextForeground, nameof(VisualStateSetting<Color?>.NormalValue), () => changed(element));
    }

    private sealed class BorderBrushTarget : IUIObservableAnimationTarget<Color>, IUIBrushAnimationTarget<Color>
    {
        public string Path => Paths.BorderBrush;

        public bool IsStoreBacked => true;

        public Color GetValue(MGElement element)
        {
            var border = RequireBorder(element);
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

        public bool TryGetValueBelowAnimation(MGElement element, out Color value)
        {
            if (element.TryGetResolvedPilotValueExcluding<IBorderBrush>(UIPilotProperty.BorderBrush, UIValueSlot.Whole, UIValueSourceKind.Animation, out var brush)
                && brush is MGUniformBorderBrush uniform && uniform.Brush is MGSolidFillBrush solid)
            {
                value = solid.Color;
                return true;
            }

            value = default;
            return false;
        }

        public void RestoreBaseValue(MGElement element, Color baseValue)
            => RestoreSlot(RequireBorder(element), UIPilotProperty.BorderBrush, UIValueSlot.Whole, source => element.SetBorderBrushTagged(new MGUniformBorderBrush(baseValue), source));

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(RequireBorder(element), nameof(MGBorder.BorderBrush), () => changed(element));

        /// <summary>ADR-0009, W5: the clone is a <see cref="IBorderBrush.Copy"/> of the uniform-over-solid border brush recovered below the
        /// animation, or a fresh <see cref="MGUniformBorderBrush"/> over a fresh <see cref="MGSolidFillBrush"/> when nothing could be recovered
        /// (a replaced run already occupied this path). <see cref="MGUniformBorderBrush.Copy"/> already copies its inner <see cref="IFillBrush"/>
        /// unfrozen, so the clone is never a shared or frozen instance.</summary>
        public object BeginAnimatedValue(MGElement element, string animationName)
        {
            var original = element.TryGetResolvedPilotValueExcluding<IBorderBrush>(UIPilotProperty.BorderBrush, UIValueSlot.Whole, UIValueSourceKind.Animation, out var below) ? below : null;
            var clone = original is MGUniformBorderBrush uniform && uniform.Brush is MGSolidFillBrush
                ? (MGUniformBorderBrush)uniform.Copy()
                : new MGUniformBorderBrush(new MGSolidFillBrush(Color.Transparent));
            element.SetBorderBrushTagged(clone, AnimationSource(UIPilotProperty.BorderBrush, animationName));
            return new UIBrushAnimationHandle<IBorderBrush>(clone, original);
        }

        /// <summary>Mutates the clone's inner <see cref="MGSolidFillBrush"/> through its own <see cref="MGSolidFillBrush.Color"/> setter (see
        /// <c>BackgroundSlotTarget.ApplyAnimatedValue</c> for why this is the plain, notifying setter rather than a suppressed one). No store
        /// write; no allocation.</summary>
        public void ApplyAnimatedValue(object handle, Color value)
            => ((MGSolidFillBrush)((MGUniformBorderBrush)((UIBrushAnimationHandle<IBorderBrush>)handle).Clone).Brush).Color = value;

        public void EndAnimatedValue(MGElement element, object handle, Color baseValue)
        {
            var h = (UIBrushAnimationHandle<IBorderBrush>)handle;
            var restored = h.Original ?? new MGUniformBorderBrush(new MGSolidFillBrush(baseValue));
            RestoreSlot(RequireBorder(element), UIPilotProperty.BorderBrush, UIValueSlot.Whole, source => element.SetBorderBrushTagged(restored, source));
        }

        private static MGBorder RequireBorder(MGElement element)
            => element.GetBorder() ?? throw new InvalidOperationException(
                $"'{Paths.BorderBrush}' needs a border: {element.GetType().Name} exposes none ({nameof(MGElement.GetBorder)} is null).");
    }
}

/// <summary>Subscribes to a Background sub-slot for the three fill-typed targets (<see cref="UIColorAnimationTargets"/>'s background slots,
/// <see cref="UIExtraAnimationTargets"/>'s two gradients): the same container-swap and slot-reference-changed events
/// <see cref="UIContainerSlotSubscription"/> already reacts to, PLUS the container's <see cref="VisualStateFillBrush.SlotBrushMutatedPropertyName"/>
/// relay (ADR-0009, W5, "Decisions taken during delivery"). Needed because a run's clone, once written into the slot, never changes reference
/// again (no allocation past the first tick): the slot-reference name a plain <see cref="UIContainerSlotSubscription"/> listens for only fires
/// once, at <see cref="Animation.IUIBrushAnimationTarget{T}.BeginAnimatedValue"/> -- every later tick only mutates the clone's own fields, which
/// the container relays under the distinct <c>SlotBrushMutated</c> name specifically so the resolved-value store ignores it (W4); a
/// <see cref="Animation.UITransition{T}"/> subscribed through <see cref="UIContainerSlotSubscription"/> would therefore never notice a named
/// state exiting or a local write changing the value below a running clone. Kept as its own type rather than widening
/// <see cref="UIContainerSlotSubscription"/> itself: that shared type also serves <c>Foreground</c>/<c>DefaultTextForeground</c>, whose
/// <see cref="Color"/>? slots have no such relay and no clone to mutate in place.</summary>
internal sealed class BackgroundSlotSubscription : IDisposable
{
    private readonly MGElement _Element;
    private readonly string _SlotPropertyName;
    private readonly Action _Changed;
    private VisualStateFillBrush _Container;
    private bool _Disposed;

    public BackgroundSlotSubscription(MGElement element, string slotPropertyName, Action changed)
    {
        _Element = element ?? throw new ArgumentNullException(nameof(element));
        _SlotPropertyName = slotPropertyName ?? throw new ArgumentNullException(nameof(slotPropertyName));
        _Changed = changed ?? throw new ArgumentNullException(nameof(changed));
        _Element.PropertyChanged += HandleElementPropertyChanged;
        AttachContainer();
    }

    private void AttachContainer()
    {
        var container = _Element.BackgroundBrush;
        if (ReferenceEquals(container, _Container))
        {
            return;
        }

        if (_Container != null)
        {
            _Container.PropertyChanged -= HandleContainerPropertyChanged;
        }

        _Container = container;
        if (_Container != null)
        {
            _Container.PropertyChanged += HandleContainerPropertyChanged;
        }
    }

    private void HandleElementPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (_Disposed || e.PropertyName != nameof(MGElement.BackgroundBrush))
        {
            return;
        }

        AttachContainer();
        _Changed();
    }

    private void HandleContainerPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (!_Disposed && (e.PropertyName == _SlotPropertyName || e.PropertyName == VisualStateFillBrush.SlotBrushMutatedPropertyName))
        {
            _Changed();
        }
    }

    public void Dispose()
    {
        if (_Disposed)
        {
            return;
        }

        _Disposed = true;
        _Element.PropertyChanged -= HandleElementPropertyChanged;
        if (_Container != null)
        {
            _Container.PropertyChanged -= HandleContainerPropertyChanged;
            _Container = null;
        }
    }
}