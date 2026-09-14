using Microsoft.Xna.Framework;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Styling;
using MonoGame.Extended;

namespace MGUI.Core.UI.Animation.Targets;

/// <summary>
/// The animation targets the framework registers in <see cref="UIAnimationTargets"/> (ADR-0006, decision 5; slices S4 to S6).<para/>
/// Plain targets write a CLR property and let the engine keep the base value: <see cref="Paths.Opacity"/>, the four
/// <c>RenderTransform.*</c> components. The <see cref="Paths.RenderScale"/> target animates the effective state-driven scale through an
/// override that shadows <see cref="MGElement.RenderScale"/> until it is released, so it follows the store-backed lifecycle
/// (<see cref="IUIAnimationTarget{T}.IsStoreBacked"/> true, restore = clear the override). Store-backed targets write a pilot property
/// through its tagged setter with the <c>Animation</c> source and restore by clearing that contribution: <see cref="Paths.Margin"/>,
/// <see cref="Paths.Padding"/>, <see cref="Paths.MinHeight"/> (layout-expensive: every tick invalidates the window layout, decision 8).<para/>
/// All are observable (S6): a transition watches the element's property notifications (the transform's own notifications for its
/// components, the visual state for the state-driven scale).
/// </summary>
public static class UIBuiltInAnimationTargets
{
    /// <summary>The registered property paths.</summary>
    public static class Paths
    {
        public const string Opacity = "Opacity";
        public const string RenderTransformTranslation = "RenderTransform.Translation";
        public const string RenderTransformScale = "RenderTransform.Scale";
        public const string RenderTransformRotation = "RenderTransform.Rotation";
        public const string RenderTransformOrigin = "RenderTransform.Origin";
        public const string RenderScale = "RenderScale";
        public const string Margin = "Margin";
        public const string Padding = "Padding";
        public const string MinHeight = "MinHeight";
        /// <summary>The value of an <see cref="MGProgressButton"/> (T6): what <see cref="MGProgressButton.Duration"/> drives; any other element is refused.</summary>
        public const string ProgressButtonValue = "ProgressButton.Value";
        /// <summary>The typewriter reveal progress of an <see cref="MGTextBlock"/> (U10): what <see cref="MGTextBlock.TextCharactersPerSecond"/> drives;
        /// any other element is refused.</summary>
        public const string TextBlockTextProgress = "TextBlock.TextProgress";
        /// <summary>The <see cref="MGUI.Core.UI.Brushes.BorderBrushes.MGHighlightBorderBrush.AnimationProgress"/> of the effective border brush
        /// (W6, ADR-0009): what <see cref="MGElement"/>'s host run drives automatically (<see cref="MGUI.Core.UI.Brushes.BorderBrushes.MGHighlightBorderBrush.AutoStart"/>)
        /// or an application drives itself when it is false. Any element whose effective border brush (below the animation) is not an
        /// <see cref="MGUI.Core.UI.Brushes.BorderBrushes.MGHighlightBorderBrush"/> is refused.</summary>
        public const string BorderBrushHighlightProgress = "BorderBrush.Highlight.Progress";
    }

    private static bool _Registered;
    private static readonly object RegisterLock = new();

    /// <summary>Registers every built-in target once. Called by <see cref="UIAnimationTargets"/> when it is first used.</summary>
    public static void RegisterAll()
    {
        lock (RegisterLock)
        {
            if (_Registered)
            {
                return;
            }

            _Registered = true;
            UIAnimationTargets.Register(new OpacityTarget());
            UIAnimationTargets.Register(new RenderTransformTranslationTarget());
            UIAnimationTargets.Register(new RenderTransformScaleTarget());
            UIAnimationTargets.Register(new RenderTransformRotationTarget());
            UIAnimationTargets.Register(new RenderTransformOriginTarget());
            UIAnimationTargets.Register(new RenderScaleTarget());
            UIAnimationTargets.Register(new MarginTarget());
            UIAnimationTargets.Register(new PaddingTarget());
            UIAnimationTargets.Register(new MinHeightTarget());
            UIAnimationTargets.Register(new ProgressButtonValueTarget());
            UIAnimationTargets.Register(new TextBlockTextProgressTarget());
            UIAnimationTargets.Register(new BorderBrushHighlightProgressTarget());
            UIColorAnimationTargets.RegisterAll();
            UIExtraAnimationTargets.RegisterAll();
        }
    }

    private static UIValueResolutionSource AnimationSource(UIPilotProperty pilot, string animationName)
        => UIValueResolutionSource.Animation(UIPilotPropertyResolver.KindOf(pilot), animationName);

    private sealed class OpacityTarget : IUIObservableAnimationTarget<float>
    {
        public string Path => Paths.Opacity;
        public bool IsStoreBacked => false;
        public float GetValue(MGElement element) => element.Opacity;
        public float GetUnderlyingValue(MGElement element) => element.Opacity;
        public void SetValue(MGElement element, float value, string animationName) => element.Opacity = value;
        public void RestoreBaseValue(MGElement element, float baseValue) => element.Opacity = baseValue;
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element, nameof(MGElement.Opacity), () => changed(element));
    }

    private sealed class RenderTransformTranslationTarget : IUIObservableAnimationTarget<Vector2>
    {
        public string Path => Paths.RenderTransformTranslation;
        public bool IsStoreBacked => false;
        public Vector2 GetValue(MGElement element) => element.RenderTransform.Translation;
        public Vector2 GetUnderlyingValue(MGElement element) => element.RenderTransform.Translation;
        public void SetValue(MGElement element, Vector2 value, string animationName) => element.RenderTransform.Translation = value;
        public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.RenderTransform.Translation = baseValue;
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element.RenderTransform, nameof(UIRenderTransform.Translation), () => changed(element));
    }

    private sealed class RenderTransformScaleTarget : IUIObservableAnimationTarget<Vector2>
    {
        public string Path => Paths.RenderTransformScale;
        public bool IsStoreBacked => false;
        public Vector2 GetValue(MGElement element) => element.RenderTransform.Scale;
        public Vector2 GetUnderlyingValue(MGElement element) => element.RenderTransform.Scale;
        public void SetValue(MGElement element, Vector2 value, string animationName) => element.RenderTransform.Scale = value;
        public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.RenderTransform.Scale = baseValue;
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element.RenderTransform, nameof(UIRenderTransform.Scale), () => changed(element));
    }

    private sealed class RenderTransformRotationTarget : IUIObservableAnimationTarget<float>
    {
        public string Path => Paths.RenderTransformRotation;
        public bool IsStoreBacked => false;
        public float GetValue(MGElement element) => element.RenderTransform.Rotation;
        public float GetUnderlyingValue(MGElement element) => element.RenderTransform.Rotation;
        public void SetValue(MGElement element, float value, string animationName) => element.RenderTransform.Rotation = value;
        public void RestoreBaseValue(MGElement element, float baseValue) => element.RenderTransform.Rotation = baseValue;
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element.RenderTransform, nameof(UIRenderTransform.Rotation), () => changed(element));
    }

    private sealed class RenderTransformOriginTarget : IUIObservableAnimationTarget<Vector2>
    {
        public string Path => Paths.RenderTransformOrigin;
        public bool IsStoreBacked => false;
        public Vector2 GetValue(MGElement element) => element.RenderTransform.Origin;
        public Vector2 GetUnderlyingValue(MGElement element) => element.RenderTransform.Origin;
        public void SetValue(MGElement element, Vector2 value, string animationName) => element.RenderTransform.Origin = value;
        public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.RenderTransform.Origin = baseValue;
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element.RenderTransform, nameof(UIRenderTransform.Origin), () => changed(element));
    }

    /// <summary>The effective state-driven scale: reads the animated override when there is one, the <see cref="MGElement.RenderScale"/> value
    /// for the current visual state otherwise (1 when none); writes the override; restoring clears the override, whatever the base handed in.
    /// A transition watches <see cref="MGElement.VisualStateChanged"/> and <see cref="MGElement.RenderScale"/>, and heads to the state's own
    /// scale (<see cref="GetUnderlyingValue"/> ignores the override).</summary>
    private sealed class RenderScaleTarget : IUIObservableAnimationTarget<float>
    {
        public string Path => Paths.RenderScale;
        public bool IsStoreBacked => true;
        public float GetValue(MGElement element) => element.TryGetEffectiveStateScale(out var scale) ? scale : 1.0f;
        public float GetUnderlyingValue(MGElement element) => element.TryGetStateScaleWithoutOverride(out var scale) ? scale : 1.0f;
        public void SetValue(MGElement element, float value, string animationName) => element.SetStateScaleOverride(value);
        public void RestoreBaseValue(MGElement element, float baseValue) => element.SetStateScaleOverride(null);
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed) => new StateScaleSubscription(element, changed);

        private sealed class StateScaleSubscription : IDisposable
        {
            private readonly MGElement _element;
            private readonly Action<MGElement> _changed;
            private readonly UIPropertyChangedSubscription _renderScale;

            public StateScaleSubscription(MGElement element, Action<MGElement> changed)
            {
                _element = element;
                _changed = changed;
                _element.VisualStateChanged += HandleVisualStateChanged;
                _renderScale = new UIPropertyChangedSubscription(element, nameof(MGElement.RenderScale), () => _changed(_element));
            }

            private void HandleVisualStateChanged(object sender, Shared.Helpers.EventArgs<VisualState> e) => _changed(_element);

            public void Dispose()
            {
                _element.VisualStateChanged -= HandleVisualStateChanged;
                _renderScale.Dispose();
            }
        }
    }

    /// <summary><c>ProgressButton.Value</c> (ADR-0007, decision 7): a plain target over <see cref="MGProgressButton.Value"/> (the end value stays);
    /// the writes go through <see cref="MGProgressButton.ApplyAnimatedValue"/> so the button does not retarget its own <see cref="MGProgressButton.Duration"/>
    /// run on them. Not observable: a transition on this path would compete with the Duration run, so it is refused. Refused on any other element.</summary>
    private sealed class ProgressButtonValueTarget : IUIAnimationTarget<float>
    {
        public string Path => Paths.ProgressButtonValue;
        public bool IsStoreBacked => false;
        public Type RequiredOwnerType => typeof(MGProgressButton);
        public float GetValue(MGElement element) => Require(element).Value;
        public void SetValue(MGElement element, float value, string animationName) => Require(element).ApplyAnimatedValue(value);
        public void RestoreBaseValue(MGElement element, float baseValue) => Require(element).ApplyAnimatedValue(baseValue);

        private static MGProgressButton Require(MGElement element)
            => element as MGProgressButton ?? throw new InvalidOperationException(
                $"'{Paths.ProgressButtonValue}' animates the value of an {nameof(MGProgressButton)}; {element.GetType().Name} has none.");
    }

    /// <summary><c>TextBlock.TextProgress</c> (U10): a plain target over <see cref="MGTextBlock.TextProgress"/> driving the typewriter reveal
    /// that <see cref="MGTextBlock.TextCharactersPerSecond"/> configures; the writes go through <see cref="MGTextBlock.ApplyAnimatedTextProgress"/>
    /// so the text block does not retarget its own reveal run on them. Not observable: a transition on this path would compete with the reveal
    /// run, so it is refused. Refused on any other element.</summary>
    private sealed class TextBlockTextProgressTarget : IUIAnimationTarget<double>
    {
        public string Path => Paths.TextBlockTextProgress;
        public bool IsStoreBacked => false;
        public Type RequiredOwnerType => typeof(MGTextBlock);
        public double GetValue(MGElement element) => Require(element).TextProgress ?? 1.0;
        public void SetValue(MGElement element, double value, string animationName) => Require(element).ApplyAnimatedTextProgress(value);
        public void RestoreBaseValue(MGElement element, double baseValue) => Require(element).ApplyAnimatedTextProgress(baseValue);

        private static MGTextBlock Require(MGElement element)
            => element as MGTextBlock ?? throw new InvalidOperationException(
                $"'{Paths.TextBlockTextProgress}' animates the text reveal of an {nameof(MGTextBlock)}; {element.GetType().Name} has none.");
    }

    /// <summary><c>BorderBrush.Highlight.Progress</c> (W6, ADR-0009): a plain target over the <c>AnimationProgress</c> of the run-owned clone of
    /// an effective <see cref="MGHighlightBorderBrush"/> border brush (the same run-owned-clone idea as the W5 brush targets, but the animated
    /// value here -- a <see cref="double"/> -- is a field of the clone rather than the clone itself, so this does not implement
    /// <see cref="IUIBrushAnimationTarget{T}"/>: that interface's <c>BeginAnimatedValue</c>/<c>ApplyAnimatedValue</c>/<c>EndAnimatedValue</c> are
    /// generic over the animated value being the brush, which does not fit). <see cref="EnsureClone"/> plays the same role as
    /// <c>BeginAnimatedValue</c> instead, called lazily from <see cref="SetValue"/> on its first tick (idempotent afterwards: once the clone is
    /// the effective value, later calls just reuse it, so a running animation never allocates or writes the store past its first tick).<para/>
    /// Not observable and not <see cref="IUIStoreBackedAnimationTarget{T}"/>-implementing (a transition on this path would compete with the
    /// host's own run the same way <c>ProgressButton.Value</c> refuses one), but reports <see cref="IsStoreBacked"/> true so a completed
    /// <see cref="UIAnimationFillBehavior.HoldEnd"/> run holds its contribution and <see cref="RestoreBaseValue"/> is what releases it (clearing
    /// the <c>Animation</c> contribution on <see cref="UIPilotProperty.BorderBrush"/> lets the true base reappear, exactly like the store-backed
    /// targets' <c>ClearContribution</c>).<para/>
    /// Refused (see <see cref="RequireCurrent"/>/<see cref="EnsureClone"/>) when the element's effective border brush is not (or, at the point
    /// <see cref="EnsureClone"/> needs a base to clone, was not below the animation) an <see cref="MGHighlightBorderBrush"/>.<para/>
    /// Does not share a conflict key with the colour <c>BorderBrush</c> path: both ultimately write the same Whole/Animation slot through a
    /// run-owned clone, but the manager's one-animation-per-(owner,path) rule (<see cref="UIAnimationManager.Start"/>) cancels a same-key
    /// predecessor with <see cref="UIAnimationCancelBehavior.KeepCurrent"/> (its clone stays as the effective value, not restored) -- which
    /// would make whichever of the two starts second fail its own strict type check (this target's <see cref="RequireCurrent"/>, or the
    /// colour target's own) against the OTHER one's leftover clone. A colour animation on <c>BorderBrush</c> while this element's own highlight
    /// run is active is therefore refused (that existing type check's own exception), not swapped -- see <see cref="MGElement.HighlightRun"/>'s
    /// doc and Docs/decisions/0009-animation-v4-freezable-brushes.md.</summary>
    private sealed class BorderBrushHighlightProgressTarget : IUIAnimationTarget<double>
    {
        public string Path => Paths.BorderBrushHighlightProgress;
        public bool IsStoreBacked => true;
        public double GetValue(MGElement element) => RequireCurrent(element).AnimationProgress;
        public void SetValue(MGElement element, double value, string animationName) => EnsureClone(element, animationName).AnimationProgress = value;
        public void RestoreBaseValue(MGElement element, double baseValue)
            => element.ClearPilotSource(UIPilotProperty.BorderBrush, UIValueSlot.Whole, UIValueSourceKind.Animation);

        private static MGHighlightBorderBrush RequireCurrent(MGElement element)
        {
            var current = element.GetBorder()?.BorderBrush;
            return current as MGHighlightBorderBrush ?? throw new InvalidOperationException(
                $"'{Paths.BorderBrushHighlightProgress}' animates {nameof(MGHighlightBorderBrush)}.{nameof(MGHighlightBorderBrush.AnimationProgress)}; " +
                $"the effective border brush of {element.GetType().Name} is {(current == null ? "empty" : "a " + current.GetType().Name)}.");
        }

        /// <summary>Returns the run-owned clone the host's writes land on, creating it (a <c>Copy()</c> of the highlight brush found below the
        /// <c>Animation</c> contribution) the first time this element is written on this path, and reusing whatever the Whole slot already
        /// holds afterwards -- either this target's own clone from a previous tick, or one a replaced run left behind (ADR-0009, W5 pattern:
        /// nothing recoverable below the animation then, so the existing effective clone is adopted instead of building a fresh one).</summary>
        private static MGHighlightBorderBrush EnsureClone(MGElement element, string animationName)
        {
            var border = element.GetBorder() ?? throw new InvalidOperationException(
                $"'{Paths.BorderBrushHighlightProgress}' needs a border: {element.GetType().Name} exposes none ({nameof(MGElement.GetBorder)} is null).");

            var current = border.BorderBrush;
            var hasBelow = element.TryGetResolvedPilotValueExcluding<IBorderBrush>(UIPilotProperty.BorderBrush, UIValueSlot.Whole, UIValueSourceKind.Animation, out var below);

            if (hasBelow && !ReferenceEquals(current, below))
            {
                // An Animation contribution is already the effective value: this target's own clone from a previous tick.
                if (current is MGHighlightBorderBrush existingClone)
                {
                    return existingClone;
                }
            }
            else if (!hasBelow && current is MGHighlightBorderBrush orphanClone)
            {
                // Nothing below: a replaced run's clone is still the only thing recorded (W5 pattern) -- adopt it.
                return orphanClone;
            }

            if (hasBelow && below is MGHighlightBorderBrush baseHighlight)
            {
                var clone = (MGHighlightBorderBrush)baseHighlight.Copy();
                element.SetBorderBrushTagged(clone, AnimationSource(UIPilotProperty.BorderBrush, animationName));
                return clone;
            }

            throw new InvalidOperationException(
                $"'{Paths.BorderBrushHighlightProgress}' animates {nameof(MGHighlightBorderBrush)}.{nameof(MGHighlightBorderBrush.AnimationProgress)}; " +
                $"the effective border brush of {element.GetType().Name} is {(current == null ? "empty" : "a " + current.GetType().Name)}.");
        }
    }

    private sealed class MarginTarget : IUIObservableAnimationTarget<Thickness>, IUIStoreBackedAnimationTarget<Thickness>
    {
        public string Path => Paths.Margin;
        public bool IsStoreBacked => true;
        public UIPilotProperty Pilot => UIPilotProperty.Margin;
        public void SetValue(MGElement element, Thickness value, UIValueResolutionSource source) => element.SetMargin(value, source);
        public bool ClearContribution(MGElement element, UIValueResolutionSource source, Thickness baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.Margin, UIValueSlot.Whole, source, s => element.SetMargin(baseValue, s));
        public bool TryGetValueBelowAnimation(MGElement element, out Thickness value)
            => element.TryGetResolvedPilotValueExcluding(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.Animation, out value);
        public Thickness GetValue(MGElement element) => element.Margin;
        public Thickness GetUnderlyingValue(MGElement element) => element.Margin;
        public void SetValue(MGElement element, Thickness value, string animationName) => element.SetMargin(value, AnimationSource(UIPilotProperty.Margin, animationName));
        public void RestoreBaseValue(MGElement element, Thickness baseValue) => element.ClearPilotSource(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.Animation);
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element, nameof(MGElement.Margin), () => changed(element));
    }

    private sealed class PaddingTarget : IUIObservableAnimationTarget<Thickness>, IUIStoreBackedAnimationTarget<Thickness>
    {
        public string Path => Paths.Padding;
        public bool IsStoreBacked => true;
        public UIPilotProperty Pilot => UIPilotProperty.Padding;
        public void SetValue(MGElement element, Thickness value, UIValueResolutionSource source) => element.SetPadding(value, source);
        public bool ClearContribution(MGElement element, UIValueResolutionSource source, Thickness baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.Padding, UIValueSlot.Whole, source, s => element.SetPadding(baseValue, s));
        public bool TryGetValueBelowAnimation(MGElement element, out Thickness value)
            => element.TryGetResolvedPilotValueExcluding(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Animation, out value);
        public Thickness GetValue(MGElement element) => element.Padding;
        public Thickness GetUnderlyingValue(MGElement element) => element.Padding;
        public void SetValue(MGElement element, Thickness value, string animationName) => element.SetPadding(value, AnimationSource(UIPilotProperty.Padding, animationName));
        public void RestoreBaseValue(MGElement element, Thickness baseValue) => element.ClearPilotSource(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Animation);
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element, nameof(MGElement.Padding), () => changed(element));
    }

    private sealed class MinHeightTarget : IUIObservableAnimationTarget<int?>, IUIStoreBackedAnimationTarget<int?>
    {
        public string Path => Paths.MinHeight;
        public bool IsStoreBacked => true;
        public UIPilotProperty Pilot => UIPilotProperty.MinHeight;
        public void SetValue(MGElement element, int? value, UIValueResolutionSource source) => element.SetMinHeight(value, source);
        public bool ClearContribution(MGElement element, UIValueResolutionSource source, int? baseValue)
            => UIStoreBackedTargets.Restore(element, UIPilotProperty.MinHeight, UIValueSlot.Whole, source, s => element.SetMinHeight(baseValue, s));
        public bool TryGetValueBelowAnimation(MGElement element, out int? value)
            => element.TryGetResolvedPilotValueExcluding(UIPilotProperty.MinHeight, UIValueSlot.Whole, UIValueSourceKind.Animation, out value);
        public int? GetValue(MGElement element) => element.MinHeight;
        public int? GetUnderlyingValue(MGElement element) => element.MinHeight;
        public void SetValue(MGElement element, int? value, string animationName) => element.SetMinHeight(value, AnimationSource(UIPilotProperty.MinHeight, animationName));
        public void RestoreBaseValue(MGElement element, int? baseValue) => element.ClearPilotSource(UIPilotProperty.MinHeight, UIValueSlot.Whole, UIValueSourceKind.Animation);
        public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
            => new UIPropertyChangedSubscription(element, nameof(MGElement.MinHeight), () => changed(element));
    }
}