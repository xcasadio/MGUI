namespace MGUI.Core.UI.Animation;

/// <summary>
/// A predefined entry/exit effect (ADR-0011 decision 6, Y6), declarable on <see cref="UIEnterExitSettings.EnterEffect"/> /
/// <see cref="UIEnterExitSettings.ExitEffect"/> and in XAML. Every effect animates only the public paths <c>Opacity</c>,
/// <c>RenderTransform.Scale</c> and <c>RenderTransform.Translation</c> (<see cref="Targets.UIBuiltInAnimationTargets.Paths"/>): an application
/// animation already running on one of those paths is replaced, the usual conflict rule (ADR-0006).<para/>
/// A slide name is the direction of the ENTRY movement: <see cref="SlideLeft"/> enters moving leftwards, i.e. it starts
/// <see cref="UIEnterExitSettings.SlideDistance"/> pixels to the right of the element's base translation and heads back to it; the exit plays
/// the inverse of that same path (it leaves from the base translation towards the same offset the entry started from), never a distance
/// derived from the element's bounds.
/// </summary>
public enum UIEnterExitEffect
{
    /// <summary>No predefined effect: <see cref="UIEnterExitSettings.EnterAnimation"/> / <see cref="UIEnterExitSettings.ExitAnimation"/> is the
    /// only way to get an entry or exit run for that direction.</summary>
    None,

    /// <summary>Opacity from 0 to the element's base opacity (entry); the base opacity to 0 (exit).</summary>
    Fade,

    /// <summary>Uniform <c>RenderTransform.Scale</c> from <see cref="UIEnterExitSettings.ScaleFrom"/> to the element's base scale (entry); the
    /// base scale to <see cref="UIEnterExitSettings.ScaleFrom"/> (exit). <c>RenderTransform.Origin</c> is held at (0.5, 0.5) for the duration
    /// of the run and restored to its previous value once the run ends (completed or cancelled).</summary>
    Scale,

    /// <summary><see cref="Fade"/> and <see cref="Scale"/> together.</summary>
    FadeScale,

    /// <summary>Enters moving leftwards: starts <see cref="UIEnterExitSettings.SlideDistance"/> pixels to the right of the base translation.</summary>
    SlideLeft,

    /// <summary>Enters moving rightwards: starts <see cref="UIEnterExitSettings.SlideDistance"/> pixels to the left of the base translation.</summary>
    SlideRight,

    /// <summary>Enters moving upwards: starts <see cref="UIEnterExitSettings.SlideDistance"/> pixels below the base translation.</summary>
    SlideUp,

    /// <summary>Enters moving downwards: starts <see cref="UIEnterExitSettings.SlideDistance"/> pixels above the base translation.</summary>
    SlideDown,
}
