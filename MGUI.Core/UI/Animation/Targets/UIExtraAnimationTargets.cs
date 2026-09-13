using MGUI.Core.UI.Animation.Interpolation;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI.Animation.Targets;

/// <summary>
/// The V2 targets (ADR-0007, decisions 4, 6 and 8; Docs/Tasks/animation-v2-tasks.md T3): the overlay cross-fade
/// (<see cref="Paths.BackgroundOverlay"/>, the <see cref="VisualStateBrush{TDataType}.OverlayOpacity"/> of the element's background, whose
/// underlying value is 1 while the element is hovered or pressed and 0 otherwise, so a transition fades the Hovered / Pressed overlay in),
/// the preferred size (<see cref="Paths.PreferredWidth"/>, <see cref="Paths.PreferredHeight"/>: plain targets, layout-expensive, every tick
/// re-lays the window out) and the gradients of the background's Normal slot (<see cref="Paths.BackgroundGradient"/> over
/// <see cref="MGGradientFillBrush"/>, <see cref="Paths.BackgroundDiagonalGradient"/> over <see cref="MGDiagonalGradientFillBrush"/>: store-backed
/// like the solid colours, a new brush per tick, any other brush refused explicitly).
/// </summary>
public static class UIExtraAnimationTargets
{
    /// <summary>The registered property paths.</summary>
    public static class Paths
    {
        public const string BackgroundOverlay = "Background.Overlay";
        public const string PreferredWidth = "PreferredWidth";
        public const string PreferredHeight = "PreferredHeight";
        public const string BackgroundGradient = "Background.Gradient";
        public const string BackgroundDiagonalGradient = "Background.DiagonalGradient";
    }

    internal static void RegisterAll()
    {
        UIAnimationTargets.Register(new BackgroundOverlayTarget());
        UIAnimationTargets.Register(new PreferredWidthTarget());
        UIAnimationTargets.Register(new PreferredHeightTarget());
        UIAnimationTargets.Register(new BackgroundGradientTarget());
        UIAnimationTargets.Register(new BackgroundDiagonalGradientTarget());
    }

    private static UIValueResolutionSource AnimationSource(string animationName)
        => UIValueResolutionSource.Animation(UIPilotPropertyResolver.KindOf(UIPilotProperty.Background), animationName);

    private sealed class BackgroundOverlayTarget : IUIObservableAnimationTarget<float>
    {
        public string Path => Paths.BackgroundOverlay;
        public bool IsStoreBacked => false;
        public float GetValue(MGElement element) => element.BackgroundBrush?.OverlayOpacity ?? 1f;
        public float GetUnderlyingValue(MGElement element) => element.VisualState.Secondary == SecondaryVisualState.None ? 0f : 1f;
        public void SetValue(MGElement element, float value, string animationName)
        {
            if (element.BackgroundBrush != null)
            {
                element.BackgroundBrush.OverlayOpacity = value;
            }
        }

        public void RestoreBaseValue(MGElement element, float baseValue) => SetValue(element, baseValue, null);
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed) => new VisualStateSubscription(element, changed);

        private sealed class VisualStateSubscription : IDisposable
        {
            private readonly MGElement _element;
            private readonly Action<MGElement> _changed;

            public VisualStateSubscription(MGElement element, Action<MGElement> changed)
            {
                _element = element;
                _changed = changed;
                _element.VisualStateChanged += HandleVisualStateChanged;
            }

            private void HandleVisualStateChanged(object sender, EventArgs<VisualState> e) => _changed(_element);

            public void Dispose() => _element.VisualStateChanged -= HandleVisualStateChanged;
        }
    }

    private sealed class PreferredWidthTarget : IUIObservableAnimationTarget<int?>
    {
        public string Path => Paths.PreferredWidth;
        public bool IsStoreBacked => false;
        public int? GetValue(MGElement element) => element.PreferredWidth;
        public int? GetUnderlyingValue(MGElement element) => element.PreferredWidth;
        public void SetValue(MGElement element, int? value, string animationName) => element.PreferredWidth = value;
        public void RestoreBaseValue(MGElement element, int? baseValue) => element.PreferredWidth = baseValue;
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element, nameof(MGElement.PreferredWidth), () => changed(element));
    }

    private sealed class PreferredHeightTarget : IUIObservableAnimationTarget<int?>
    {
        public string Path => Paths.PreferredHeight;
        public bool IsStoreBacked => false;
        public int? GetValue(MGElement element) => element.PreferredHeight;
        public int? GetUnderlyingValue(MGElement element) => element.PreferredHeight;
        public void SetValue(MGElement element, int? value, string animationName) => element.PreferredHeight = value;
        public void RestoreBaseValue(MGElement element, int? baseValue) => element.PreferredHeight = baseValue;
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element, nameof(MGElement.PreferredHeight), () => changed(element));
    }

    private sealed class BackgroundGradientTarget : IUIObservableAnimationTarget<UIGradientColors>, IUIStoreBackedAnimationTarget<UIGradientColors>
    {
        public string Path => Paths.BackgroundGradient;
        public bool IsStoreBacked => true;
        public UIPilotProperty Pilot => UIPilotProperty.Background;
        public void SetValue(MGElement element, UIGradientColors value, UIValueResolutionSource source)
            => element.SetBackgroundSlot(UIValueSlot.Normal, new MGGradientFillBrush(value.TopLeft, value.TopRight, value.BottomRight, value.BottomLeft), source);
        public bool ClearContribution(MGElement element, UIValueResolutionSource source, UIGradientColors baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.Background, UIValueSlot.Normal, source, s => SetValue(element, baseValue, s));

        public UIGradientColors GetValue(MGElement element)
        {
            IFillBrush brush = element.BackgroundBrush?.NormalValue;
            if (brush is MGGradientFillBrush gradient)
            {
                return new UIGradientColors(gradient.TopLeftColor, gradient.TopRightColor, gradient.BottomRightColor, gradient.BottomLeftColor);
            }

            throw new InvalidOperationException(
                $"'{Path}' of {element.GetType().Name} is {(brush == null ? "empty" : "a " + brush.GetType().Name)}: only a four-corner gradient ({nameof(MGGradientFillBrush)}) can be animated on this path.");
        }

        public UIGradientColors GetUnderlyingValue(MGElement element) => GetValue(element);

        public void SetValue(MGElement element, UIGradientColors value, string animationName)
            => element.SetBackgroundSlot(UIValueSlot.Normal, new MGGradientFillBrush(value.TopLeft, value.TopRight, value.BottomRight, value.BottomLeft), AnimationSource(animationName));

        public void RestoreBaseValue(MGElement element, UIGradientColors baseValue)
            => UIColorAnimationTargets.RestoreSlot(element, UIPilotProperty.Background, UIValueSlot.Normal,
                source => element.SetBackgroundSlot(UIValueSlot.Normal, new MGGradientFillBrush(baseValue.TopLeft, baseValue.TopRight, baseValue.BottomRight, baseValue.BottomLeft), source));

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIContainerSlotSubscription(element, nameof(MGElement.BackgroundBrush), e => e.BackgroundBrush, nameof(VisualStateFillBrush.NormalValue), () => changed(element));
    }

    private sealed class BackgroundDiagonalGradientTarget : IUIObservableAnimationTarget<UIDiagonalGradientColors>, IUIStoreBackedAnimationTarget<UIDiagonalGradientColors>
    {
        public string Path => Paths.BackgroundDiagonalGradient;
        public bool IsStoreBacked => true;
        public UIPilotProperty Pilot => UIPilotProperty.Background;
        public void SetValue(MGElement element, UIDiagonalGradientColors value, UIValueResolutionSource source)
            => element.SetBackgroundSlot(UIValueSlot.Normal, new MGDiagonalGradientFillBrush(value.Color1, value.Color2, value.Color1Position), source);
        public bool ClearContribution(MGElement element, UIValueResolutionSource source, UIDiagonalGradientColors baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.Background, UIValueSlot.Normal, source, s => SetValue(element, baseValue, s));

        public UIDiagonalGradientColors GetValue(MGElement element)
        {
            IFillBrush brush = element.BackgroundBrush?.NormalValue;
            if (brush is MGDiagonalGradientFillBrush gradient)
            {
                return new UIDiagonalGradientColors(gradient.Color1, gradient.Color2, gradient.Color1Position);
            }

            throw new InvalidOperationException(
                $"'{Path}' of {element.GetType().Name} is {(brush == null ? "empty" : "a " + brush.GetType().Name)}: only a diagonal gradient ({nameof(MGDiagonalGradientFillBrush)}) can be animated on this path.");
        }

        public UIDiagonalGradientColors GetUnderlyingValue(MGElement element) => GetValue(element);

        public void SetValue(MGElement element, UIDiagonalGradientColors value, string animationName)
            => element.SetBackgroundSlot(UIValueSlot.Normal, new MGDiagonalGradientFillBrush(value.Color1, value.Color2, value.Color1Position), AnimationSource(animationName));

        public void RestoreBaseValue(MGElement element, UIDiagonalGradientColors baseValue)
            => UIColorAnimationTargets.RestoreSlot(element, UIPilotProperty.Background, UIValueSlot.Normal,
                source => element.SetBackgroundSlot(UIValueSlot.Normal, new MGDiagonalGradientFillBrush(baseValue.Color1, baseValue.Color2, baseValue.Color1Position), source));

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIContainerSlotSubscription(element, nameof(MGElement.BackgroundBrush), e => e.BackgroundBrush, nameof(VisualStateFillBrush.NormalValue), () => changed(element));
    }
}