using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Targets;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// The transitions a theme installs on a control that opts in (ADR-0007, decision 5; <see cref="MGTheme.Animation"/>): <c>RenderScale</c> over
/// <see cref="MGThemeAnimationSettings.HoverDuration"/> / <see cref="MGThemeAnimationSettings.HoverEasing"/> and <c>Background.Overlay</c> over
/// <see cref="MGThemeAnimationSettings.PressDuration"/> / <see cref="MGThemeAnimationSettings.PressEasing"/>. A transition is installed once per path,
/// updated in place on a theme change (a run in flight is not reset) and removed when the theme disables animation; a transition the application
/// attached on the same path is left alone, before or after the theme's. Owned by <see cref="MGButton"/> and <see cref="MGToggleButton"/>.
/// </summary>
internal sealed class UIThemeTransitions
{
    private UITransition<float> _Hover;
    private UITransition<float> _Overlay;

    /// <summary>Installs, updates or removes the theme transitions of <paramref name="element"/> according to <paramref name="settings"/>.</summary>
    public void Apply(MGElement element, MGThemeAnimationSettings settings)
    {
        if (element == null)
        {
            throw new ArgumentNullException(nameof(element));
        }

        if (settings == null || !settings.Enabled)
        {
            Remove(element, ref _Hover);
            Remove(element, ref _Overlay);
            return;
        }

        _Hover = Install(element, _Hover, UIBuiltInAnimationTargets.Paths.RenderScale, settings.HoverDuration, settings.HoverEasing);
        _Overlay = Install(element, _Overlay, UIExtraAnimationTargets.Paths.BackgroundOverlay, settings.PressDuration, settings.PressEasing);
    }

    private static UITransition<float> Install(MGElement element, UITransition<float> current, string path, TimeSpan duration, string easingName)
    {
        IUIEasingFunction easing = null;
        if (!string.IsNullOrWhiteSpace(easingName))
        {
            UIEasing.TryGet(easingName, out easing);
        }

        UITransition existing = element.Transitions[path];
        if (current != null && ReferenceEquals(existing, current))
        {
            current.Duration = duration < TimeSpan.Zero ? TimeSpan.Zero : duration;
            current.Easing = easing;
            return current;
        }

        if (existing != null)
        {
            // The application attached its own transition on this path: the theme does not compete with it.
            return null;
        }

        UITransition<float> transition = new(path, duration < TimeSpan.Zero ? TimeSpan.Zero : duration, easing);
        element.Transitions.Add(transition);
        return transition;
    }

    private static void Remove(MGElement element, ref UITransition<float> current)
    {
        if (current == null)
        {
            return;
        }

        if (ReferenceEquals(element.Transitions[current.Property], current))
        {
            element.Transitions.Remove(current);
        }

        current = null;
    }
}