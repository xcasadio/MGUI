using Microsoft.Xna.Framework;
using MGUI.Core.UI.Styling;
using MonoGame.Extended;

namespace MGUI.Core.UI.Animation.Targets
{
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
            public float GetValue(MGElement element) => element.TryGetEffectiveStateScale(out float scale) ? scale : 1.0f;
            public float GetUnderlyingValue(MGElement element) => element.TryGetStateScaleWithoutOverride(out float scale) ? scale : 1.0f;
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

        private sealed class MarginTarget : IUIObservableAnimationTarget<Thickness>
        {
            public string Path => Paths.Margin;
            public bool IsStoreBacked => true;
            public Thickness GetValue(MGElement element) => element.Margin;
            public Thickness GetUnderlyingValue(MGElement element) => element.Margin;
            public void SetValue(MGElement element, Thickness value, string animationName) => element.SetMargin(value, AnimationSource(UIPilotProperty.Margin, animationName));
            public void RestoreBaseValue(MGElement element, Thickness baseValue) => element.ClearPilotSource(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.Animation);
            public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
                => new UIPropertyChangedSubscription(element, nameof(MGElement.Margin), () => changed(element));
        }

        private sealed class PaddingTarget : IUIObservableAnimationTarget<Thickness>
        {
            public string Path => Paths.Padding;
            public bool IsStoreBacked => true;
            public Thickness GetValue(MGElement element) => element.Padding;
            public Thickness GetUnderlyingValue(MGElement element) => element.Padding;
            public void SetValue(MGElement element, Thickness value, string animationName) => element.SetPadding(value, AnimationSource(UIPilotProperty.Padding, animationName));
            public void RestoreBaseValue(MGElement element, Thickness baseValue) => element.ClearPilotSource(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Animation);
            public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
                => new UIPropertyChangedSubscription(element, nameof(MGElement.Padding), () => changed(element));
        }

        private sealed class MinHeightTarget : IUIObservableAnimationTarget<int?>
        {
            public string Path => Paths.MinHeight;
            public bool IsStoreBacked => true;
            public int? GetValue(MGElement element) => element.MinHeight;
            public int? GetUnderlyingValue(MGElement element) => element.MinHeight;
            public void SetValue(MGElement element, int? value, string animationName) => element.SetMinHeight(value, AnimationSource(UIPilotProperty.MinHeight, animationName));
            public void RestoreBaseValue(MGElement element, int? baseValue) => element.ClearPilotSource(UIPilotProperty.MinHeight, UIValueSlot.Whole, UIValueSourceKind.Animation);
            public IDisposable Subscribe(MGElement element, Action<MGElement> changed)
                => new UIPropertyChangedSubscription(element, nameof(MGElement.MinHeight), () => changed(element));
        }
    }
}
