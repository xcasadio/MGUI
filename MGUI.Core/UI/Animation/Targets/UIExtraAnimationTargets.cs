using MGUI.Core.UI.Animation.Interpolation;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI.Animation.Targets;

/// <summary>
/// Additional targets (ADR-0007, decisions 4, 6 and 8): the overlay cross-fade
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
        /// <summary>The vertical scroll offset of an <see cref="MGScrollViewer"/> (ADR-0011, decision C3): a plain, observable target
        /// restricted to <see cref="MGScrollViewer"/>, written through <see cref="MGScrollViewer.ApplyAnimatedVerticalOffset"/> so a run never
        /// competes with the viewer's own external-write cancellation rule (<see cref="MGScrollViewer.VerticalOffset"/>).</summary>
        public const string ScrollViewerVerticalOffset = "ScrollViewer.VerticalOffset";
        /// <summary>The horizontal scroll offset of an <see cref="MGScrollViewer"/>: see <see cref="ScrollViewerVerticalOffset"/>.</summary>
        public const string ScrollViewerHorizontalOffset = "ScrollViewer.HorizontalOffset";
    }

    internal static void RegisterAll()
    {
        UIAnimationTargets.Register(new BackgroundOverlayTarget());
        UIAnimationTargets.Register(new PreferredWidthTarget());
        UIAnimationTargets.Register(new PreferredHeightTarget());
        UIAnimationTargets.Register(new BackgroundGradientTarget());
        UIAnimationTargets.Register(new BackgroundDiagonalGradientTarget());
        UIAnimationTargets.Register(new ScrollViewerOffsetTarget(Paths.ScrollViewerVerticalOffset, isVertical: true));
        UIAnimationTargets.Register(new ScrollViewerOffsetTarget(Paths.ScrollViewerHorizontalOffset, isVertical: false));
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

    private sealed class BackgroundGradientTarget : IUIObservableAnimationTarget<UIGradientColors>, IUIBrushAnimationTarget<UIGradientColors>
    {
        public string Path => Paths.BackgroundGradient;
        public bool IsStoreBacked => true;
        public UIPilotProperty Pilot => UIPilotProperty.Background;
        public void SetValue(MGElement element, UIGradientColors value, UIValueResolutionSource source)
            => element.SetBackgroundSlot(UIValueSlot.Normal, new MGGradientFillBrush(value.TopLeft, value.TopRight, value.BottomRight, value.BottomLeft), source);
        public bool ClearContribution(MGElement element, UIValueResolutionSource source, UIGradientColors baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.Background, UIValueSlot.Normal, source, s => SetValue(element, baseValue, s));

        public bool TryGetValueBelowAnimation(MGElement element, out UIGradientColors value)
        {
            if (element.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out var brush)
                && brush is MGGradientFillBrush gradient)
            {
                value = new UIGradientColors(gradient.TopLeftColor, gradient.TopRightColor, gradient.BottomRightColor, gradient.BottomLeftColor);
                return true;
            }

            value = default;
            return false;
        }

        public UIGradientColors GetValue(MGElement element)
        {
            var brush = element.BackgroundBrush?.NormalValue;
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
            => new BackgroundSlotSubscription(element, nameof(VisualStateFillBrush.NormalValue), () => changed(element));

        /// <summary>ADR-0009: see <see cref="UIColorAnimationTargets"/>'s <c>BackgroundSlotTarget.BeginAnimatedValue</c> for the pattern
        /// (recover the brush below the animation, clone it when it is a <see cref="MGGradientFillBrush"/>, else start fresh).</summary>
        public object BeginAnimatedValue(MGElement element, string animationName)
        {
            var original = element.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out var below) ? below : null;
            var clone = original is MGGradientFillBrush gradient ? (MGGradientFillBrush)gradient.Copy() : new MGGradientFillBrush(default, default, default, default);
            element.SetBackgroundSlot(UIValueSlot.Normal, clone, AnimationSource(animationName));
            return new UIBrushAnimationHandle<IFillBrush>(clone, original);
        }

        /// <summary>Mutates the clone's four corners through their own notifying setters (see <c>UIColorAnimationTargets</c>'s
        /// <c>BackgroundSlotTarget.ApplyAnimatedValue</c> for why a suppressed setter was not used). No store write; no allocation.</summary>
        public void ApplyAnimatedValue(object handle, UIGradientColors value)
        {
            var clone = (MGGradientFillBrush)((UIBrushAnimationHandle<IFillBrush>)handle).Clone;
            clone.TopLeftColor = value.TopLeft;
            clone.TopRightColor = value.TopRight;
            clone.BottomRightColor = value.BottomRight;
            clone.BottomLeftColor = value.BottomLeft;
        }

        /// <summary>Restores the exact base instance, re-reading it first (see <c>UIColorAnimationTargets</c>'s <c>BackgroundSlotTarget.EndAnimatedValue</c>
        /// for why: a container swap mid-run promotes the swapped-in container's own value into a real contribution).</summary>
        public void EndAnimatedValue(MGElement element, object handle, UIGradientColors baseValue)
        {
            var h = (UIBrushAnimationHandle<IFillBrush>)handle;
            var restored = (element.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out var below) ? below : null)
                ?? h.Original ?? new MGGradientFillBrush(baseValue.TopLeft, baseValue.TopRight, baseValue.BottomRight, baseValue.BottomLeft);
            UIColorAnimationTargets.RestoreSlot(element, UIPilotProperty.Background, UIValueSlot.Normal, source => element.SetBackgroundSlot(UIValueSlot.Normal, restored, source));
        }
    }

    private sealed class BackgroundDiagonalGradientTarget : IUIObservableAnimationTarget<UIDiagonalGradientColors>, IUIBrushAnimationTarget<UIDiagonalGradientColors>
    {
        public string Path => Paths.BackgroundDiagonalGradient;
        public bool IsStoreBacked => true;
        public UIPilotProperty Pilot => UIPilotProperty.Background;
        public void SetValue(MGElement element, UIDiagonalGradientColors value, UIValueResolutionSource source)
            => element.SetBackgroundSlot(UIValueSlot.Normal, new MGDiagonalGradientFillBrush(value.Color1, value.Color2, value.Color1Position), source);
        public bool ClearContribution(MGElement element, UIValueResolutionSource source, UIDiagonalGradientColors baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.Background, UIValueSlot.Normal, source, s => SetValue(element, baseValue, s));

        public bool TryGetValueBelowAnimation(MGElement element, out UIDiagonalGradientColors value)
        {
            if (element.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out var brush)
                && brush is MGDiagonalGradientFillBrush gradient)
            {
                value = new UIDiagonalGradientColors(gradient.Color1, gradient.Color2, gradient.Color1Position);
                return true;
            }

            value = default;
            return false;
        }

        public UIDiagonalGradientColors GetValue(MGElement element)
        {
            var brush = element.BackgroundBrush?.NormalValue;
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
            => new BackgroundSlotSubscription(element, nameof(VisualStateFillBrush.NormalValue), () => changed(element));

        /// <summary>ADR-0009: see <see cref="UIColorAnimationTargets"/>'s <c>BackgroundSlotTarget.BeginAnimatedValue</c> for the pattern
        /// (recover the brush below the animation, clone it when it is a <see cref="MGDiagonalGradientFillBrush"/>, else start fresh).</summary>
        public object BeginAnimatedValue(MGElement element, string animationName)
        {
            var original = element.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out var below) ? below : null;
            var clone = original is MGDiagonalGradientFillBrush diagonal ? (MGDiagonalGradientFillBrush)diagonal.Copy() : new MGDiagonalGradientFillBrush(default, default, default);
            element.SetBackgroundSlot(UIValueSlot.Normal, clone, AnimationSource(animationName));
            return new UIBrushAnimationHandle<IFillBrush>(clone, original);
        }

        /// <summary>Mutates the clone's two colours and corner through their own notifying setters (see <c>UIColorAnimationTargets</c>'s
        /// <c>BackgroundSlotTarget.ApplyAnimatedValue</c> for why a suppressed setter was not used). No store write; no allocation.</summary>
        public void ApplyAnimatedValue(object handle, UIDiagonalGradientColors value)
        {
            var clone = (MGDiagonalGradientFillBrush)((UIBrushAnimationHandle<IFillBrush>)handle).Clone;
            clone.Color1 = value.Color1;
            clone.Color2 = value.Color2;
            clone.Color1Position = value.Color1Position;
        }

        /// <summary>Restores the exact base instance, re-reading it first (see <c>UIColorAnimationTargets</c>'s <c>BackgroundSlotTarget.EndAnimatedValue</c>
        /// for why: a container swap mid-run promotes the swapped-in container's own value into a real contribution).</summary>
        public void EndAnimatedValue(MGElement element, object handle, UIDiagonalGradientColors baseValue)
        {
            var h = (UIBrushAnimationHandle<IFillBrush>)handle;
            var restored = (element.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out var below) ? below : null)
                ?? h.Original ?? new MGDiagonalGradientFillBrush(baseValue.Color1, baseValue.Color2, baseValue.Color1Position);
            UIColorAnimationTargets.RestoreSlot(element, UIPilotProperty.Background, UIValueSlot.Normal, source => element.SetBackgroundSlot(UIValueSlot.Normal, restored, source));
        }
    }

    /// <summary><c>ScrollViewer.VerticalOffset</c> / <c>ScrollViewer.HorizontalOffset</c> (ADR-0011, decision C3): a plain, observable target
    /// restricted to <see cref="MGScrollViewer"/> (<see cref="RequiredOwnerType"/>, enforced by <see cref="Require"/>). Writes go through
    /// <see cref="MGScrollViewer.ApplyAnimatedVerticalOffset"/>/<see cref="MGScrollViewer.ApplyAnimatedHorizontalOffset"/>, which clamp and
    /// notify exactly like the public setter but never cancel a run; the underlying value is the same offset (not store-backed: nothing to
    /// shadow), so a <see cref="UITransition{T}"/> attached to the path observes it directly.</summary>
    private sealed class ScrollViewerOffsetTarget : IUIObservableAnimationTarget<float>
    {
        private readonly bool _isVertical;

        public ScrollViewerOffsetTarget(string path, bool isVertical)
        {
            Path = path;
            _isVertical = isVertical;
        }

        public string Path { get; }
        public bool IsStoreBacked => false;
        public Type RequiredOwnerType => typeof(MGScrollViewer);
        public float GetValue(MGElement element) => _isVertical ? Require(element).VerticalOffset : Require(element).HorizontalOffset;
        public float GetUnderlyingValue(MGElement element) => GetValue(element);

        public void SetValue(MGElement element, float value, string animationName)
        {
            var scrollViewer = Require(element);
            if (_isVertical)
            {
                scrollViewer.ApplyAnimatedVerticalOffset(value);
            }
            else
            {
                scrollViewer.ApplyAnimatedHorizontalOffset(value);
            }
        }

        public void RestoreBaseValue(MGElement element, float baseValue) => SetValue(element, baseValue, null);

        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element, _isVertical ? nameof(MGScrollViewer.VerticalOffset) : nameof(MGScrollViewer.HorizontalOffset), () => changed(element));

        private MGScrollViewer Require(MGElement element)
            => element as MGScrollViewer ?? throw new InvalidOperationException(
                $"'{Path}' animates the scroll offset of an {nameof(MGScrollViewer)}; {element.GetType().Name} has none.");
    }
}