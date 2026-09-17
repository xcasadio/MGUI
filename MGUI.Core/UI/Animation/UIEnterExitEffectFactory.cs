using Microsoft.Xna.Framework;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Targets;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// Builds the run of a predefined <see cref="UIEnterExitEffect"/> (ADR-0011 decision 6, Y6): a <see cref="UIStoryboard"/> of one or two
/// <see cref="UIPropertyAnimation{T}"/> children over the public paths <see cref="UIBuiltInAnimationTargets.Paths.Opacity"/>,
/// <see cref="UIBuiltInAnimationTargets.Paths.RenderTransformScale"/> and <see cref="UIBuiltInAnimationTargets.Paths.RenderTransformTranslation"/>
/// -- the composite completes on its own timeline whatever happens to its children (<see cref="UIAnimationGroup"/>), which is exactly what lets
/// <see cref="MGElement"/> hook one <c>Completed</c>/<c>Cancelled</c> pair regardless of the effect's shape. Every child cancels with
/// <see cref="UIAnimationCancelBehavior.KeepCurrent"/>: <see cref="MGElement"/> is the one that decides when to force the true base values
/// back (an external cancellation) and when to let the current values stand so a superseding run starts with no jump.<para/>
/// Allocation happens once per entry/exit start, never per tick (documented budget).
/// </summary>
internal static class UIEnterExitEffectFactory
{
    /// <summary>Builds the run for the entry or exit effect of <paramref name="settings"/> (<see cref="UIEnterExitSettings.EnterEffect"/> when
    /// <paramref name="isEntry"/> is true, <see cref="UIEnterExitSettings.ExitEffect"/> otherwise), or null for <see cref="UIEnterExitEffect.None"/>.
    /// <paramref name="isFreshCycle"/>
    /// controls whether each child gets an explicit <see cref="UIAnimation{T}.From"/> (a cycle starting at rest: the documented constant start
    /// value of the effect) or none (a cycle interrupting the opposite direction: the child reads the current value when it starts, so the
    /// visible position never jumps).</summary>
    public static UIAnimation Build(MGElement element, UIElementAnimationSlot slot, UIEnterExitSettings settings, bool isEntry, bool isFreshCycle)
    {
        var effect = isEntry ? settings.EnterEffect : settings.ExitEffect;
        if (effect == UIEnterExitEffect.None)
        {
            return null;
        }

        var duration = isEntry ? settings.EnterDuration : settings.ExitDuration;
        var easing = (isEntry ? settings.EnterEasing : settings.ExitEasing) ?? (isEntry ? UIEasing.CubicOut : UIEasing.CubicIn);

        UIStoryboard storyboard = new() { Name = $"enter-exit-{effect}" };

        void AddChild<T>(string path, T from, T to)
        {
            UIPropertyAnimation<T> child = new(path)
            {
                To = to,
                Duration = duration,
                Easing = easing,
                CancelBehavior = UIAnimationCancelBehavior.KeepCurrent,
                Name = path,
            };
            if (isFreshCycle)
            {
                child.From = from;
            }

            storyboard.Add(child);
        }

        void AddOpacity()
        {
            AddChild(UIBuiltInAnimationTargets.Paths.Opacity, isEntry ? 0f : slot.BaseOpacity, isEntry ? slot.BaseOpacity : 0f);
        }

        void AddScale()
        {
            var scaleFrom = isEntry ? new Vector2(settings.ScaleFrom) : slot.BaseScale;
            var scaleTo = isEntry ? slot.BaseScale : new Vector2(settings.ScaleFrom);
            AddChild(UIBuiltInAnimationTargets.Paths.RenderTransformScale, scaleFrom, scaleTo);

            //  Origin is never animated: held at the centre for the duration of the run (written once, here) and released by MGElement,
            //  back to the captured Base value, once the run ends -- completed or cancelled (ADR-0011 decision 6).
            element.RenderTransform.Origin = new Vector2(0.5f);
            slot.EnterExitOriginHeld = true;
        }

        void AddSlide(Vector2 offset)
        {
            var from = isEntry ? slot.BaseTranslation + offset : slot.BaseTranslation;
            var to = isEntry ? slot.BaseTranslation : slot.BaseTranslation + offset;
            AddChild(UIBuiltInAnimationTargets.Paths.RenderTransformTranslation, from, to);
        }

        switch (effect)
        {
            case UIEnterExitEffect.Fade:
                AddOpacity();
                break;
            case UIEnterExitEffect.Scale:
                AddScale();
                break;
            case UIEnterExitEffect.FadeScale:
                AddOpacity();
                AddScale();
                break;
            case UIEnterExitEffect.SlideLeft:
                AddSlide(new Vector2(settings.SlideDistance, 0f));
                break;
            case UIEnterExitEffect.SlideRight:
                AddSlide(new Vector2(-settings.SlideDistance, 0f));
                break;
            case UIEnterExitEffect.SlideUp:
                AddSlide(new Vector2(0f, settings.SlideDistance));
                break;
            case UIEnterExitEffect.SlideDown:
                AddSlide(new Vector2(0f, -settings.SlideDistance));
                break;
        }

        return storyboard;
    }
}
