using Microsoft.Xna.Framework;
using MGUI.Core.UI.Styling;
using MonoGame.Extended;

namespace MGUI.Core.UI.Animation.Targets
{
    /// <summary>
    /// The animation targets the framework registers in <see cref="UIAnimationTargets"/> (ADR-0006, decision 5; slices S4 and S5).<para/>
    /// Plain targets write a CLR property and let the engine keep the base value: <see cref="Paths.Opacity"/>, the four
    /// <c>RenderTransform.*</c> components. The <see cref="Paths.RenderScale"/> target animates the effective state-driven scale through an
    /// override that shadows <see cref="MGElement.RenderScale"/> until it is released, so it follows the store-backed lifecycle
    /// (<see cref="IUIAnimationTarget{T}.IsStoreBacked"/> true, restore = clear the override). Store-backed targets write a pilot property
    /// through its tagged setter with the <c>Animation</c> source and restore by clearing that contribution: <see cref="Paths.Margin"/>,
    /// <see cref="Paths.Padding"/>, <see cref="Paths.MinHeight"/> (layout-expensive: every tick invalidates the window layout, decision 8).
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
            }
        }

        private static UIValueResolutionSource AnimationSource(UIPilotProperty pilot, string animationName)
            => UIValueResolutionSource.Animation(UIPilotPropertyResolver.KindOf(pilot), animationName);

        private sealed class OpacityTarget : IUIAnimationTarget<float>
        {
            public string Path => Paths.Opacity;
            public bool IsStoreBacked => false;
            public float GetValue(MGElement element) => element.Opacity;
            public void SetValue(MGElement element, float value, string animationName) => element.Opacity = value;
            public void RestoreBaseValue(MGElement element, float baseValue) => element.Opacity = baseValue;
        }

        private sealed class RenderTransformTranslationTarget : IUIAnimationTarget<Vector2>
        {
            public string Path => Paths.RenderTransformTranslation;
            public bool IsStoreBacked => false;
            public Vector2 GetValue(MGElement element) => element.RenderTransform.Translation;
            public void SetValue(MGElement element, Vector2 value, string animationName) => element.RenderTransform.Translation = value;
            public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.RenderTransform.Translation = baseValue;
        }

        private sealed class RenderTransformScaleTarget : IUIAnimationTarget<Vector2>
        {
            public string Path => Paths.RenderTransformScale;
            public bool IsStoreBacked => false;
            public Vector2 GetValue(MGElement element) => element.RenderTransform.Scale;
            public void SetValue(MGElement element, Vector2 value, string animationName) => element.RenderTransform.Scale = value;
            public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.RenderTransform.Scale = baseValue;
        }

        private sealed class RenderTransformRotationTarget : IUIAnimationTarget<float>
        {
            public string Path => Paths.RenderTransformRotation;
            public bool IsStoreBacked => false;
            public float GetValue(MGElement element) => element.RenderTransform.Rotation;
            public void SetValue(MGElement element, float value, string animationName) => element.RenderTransform.Rotation = value;
            public void RestoreBaseValue(MGElement element, float baseValue) => element.RenderTransform.Rotation = baseValue;
        }

        private sealed class RenderTransformOriginTarget : IUIAnimationTarget<Vector2>
        {
            public string Path => Paths.RenderTransformOrigin;
            public bool IsStoreBacked => false;
            public Vector2 GetValue(MGElement element) => element.RenderTransform.Origin;
            public void SetValue(MGElement element, Vector2 value, string animationName) => element.RenderTransform.Origin = value;
            public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.RenderTransform.Origin = baseValue;
        }

        /// <summary>The effective state-driven scale: reads the animated override when there is one, the <see cref="MGElement.RenderScale"/> value
        /// for the current visual state otherwise (1 when none); writes the override; restoring clears the override, whatever the base handed in.</summary>
        private sealed class RenderScaleTarget : IUIAnimationTarget<float>
        {
            public string Path => Paths.RenderScale;
            public bool IsStoreBacked => true;
            public float GetValue(MGElement element) => element.TryGetEffectiveStateScale(out float scale) ? scale : 1.0f;
            public void SetValue(MGElement element, float value, string animationName) => element.SetStateScaleOverride(value);
            public void RestoreBaseValue(MGElement element, float baseValue) => element.SetStateScaleOverride(null);
        }

        private sealed class MarginTarget : IUIAnimationTarget<Thickness>
        {
            public string Path => Paths.Margin;
            public bool IsStoreBacked => true;
            public Thickness GetValue(MGElement element) => element.Margin;
            public void SetValue(MGElement element, Thickness value, string animationName) => element.SetMargin(value, AnimationSource(UIPilotProperty.Margin, animationName));
            public void RestoreBaseValue(MGElement element, Thickness baseValue) => element.ClearPilotSource(UIPilotProperty.Margin, UIValueSlot.Whole, UIValueSourceKind.Animation);
        }

        private sealed class PaddingTarget : IUIAnimationTarget<Thickness>
        {
            public string Path => Paths.Padding;
            public bool IsStoreBacked => true;
            public Thickness GetValue(MGElement element) => element.Padding;
            public void SetValue(MGElement element, Thickness value, string animationName) => element.SetPadding(value, AnimationSource(UIPilotProperty.Padding, animationName));
            public void RestoreBaseValue(MGElement element, Thickness baseValue) => element.ClearPilotSource(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Animation);
        }

        private sealed class MinHeightTarget : IUIAnimationTarget<int?>
        {
            public string Path => Paths.MinHeight;
            public bool IsStoreBacked => true;
            public int? GetValue(MGElement element) => element.MinHeight;
            public void SetValue(MGElement element, int? value, string animationName) => element.SetMinHeight(value, AnimationSource(UIPilotProperty.MinHeight, animationName));
            public void RestoreBaseValue(MGElement element, int? baseValue) => element.ClearPilotSource(UIPilotProperty.MinHeight, UIValueSlot.Whole, UIValueSourceKind.Animation);
        }
    }
}
