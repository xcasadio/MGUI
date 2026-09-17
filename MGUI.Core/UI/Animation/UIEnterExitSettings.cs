using MGUI.Core.UI.Animation.Easing;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// Opt-in settings for <see cref="MGElement.EnterExit"/> (ADR-0011 decision 6, Y6): null by default, allocated by the application (or by the
/// XAML <c>Element</c> DTO, decision 6) on demand. Read only by <see cref="MGElement.Visibility"/>'s setter; this is a settings object, never
/// carrying an animated value itself.<para/>
/// An entry plays when <see cref="MGElement.Visibility"/> is set to <see cref="UI.Visibility.Visible"/> (from <see cref="UI.Visibility.Hidden"/>
/// or <see cref="UI.Visibility.Collapsed"/>, or during an exit); an exit plays when it is set away from <see cref="UI.Visibility.Visible"/>,
/// once the element has been drawn at least once since its last attachment (see <see cref="MGElement.PendingVisibility"/>). Either direction
/// prefers an explicit <see cref="EnterAnimation"/> / <see cref="ExitAnimation"/> over its predefined <see cref="EnterEffect"/> /
/// <see cref="ExitEffect"/>; see <see cref="HasEnter"/> / <see cref="HasExit"/>.
/// </summary>
public sealed class UIEnterExitSettings
{
    /// <summary>An arbitrary animation played on entry, built in code. Wins over <see cref="EnterEffect"/> when set.</summary>
    public UIAnimation EnterAnimation { get; set; }

    /// <summary>An arbitrary animation played on exit, built in code. Wins over <see cref="ExitEffect"/> when set.</summary>
    public UIAnimation ExitAnimation { get; set; }

    /// <summary>The predefined entry effect, ignored while <see cref="EnterAnimation"/> is set. Default: <see cref="UIEnterExitEffect.None"/>.</summary>
    public UIEnterExitEffect EnterEffect { get; set; } = UIEnterExitEffect.None;

    /// <summary>The predefined exit effect, ignored while <see cref="ExitAnimation"/> is set. Default: <see cref="UIEnterExitEffect.None"/>.</summary>
    public UIEnterExitEffect ExitEffect { get; set; } = UIEnterExitEffect.None;

    private TimeSpan _enterDuration = TimeSpan.FromMilliseconds(200);

    /// <summary>Length of the predefined entry effect (ignored by <see cref="EnterAnimation"/>, which carries its own timing). Negative values
    /// are refused. Default: 200 milliseconds.</summary>
    public TimeSpan EnterDuration
    {
        get => _enterDuration;
        set => _enterDuration = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(EnterDuration)} cannot be negative.");
    }

    private TimeSpan _exitDuration = TimeSpan.FromMilliseconds(150);

    /// <summary>Length of the predefined exit effect (ignored by <see cref="ExitAnimation"/>, which carries its own timing). Negative values
    /// are refused. Default: 150 milliseconds.</summary>
    public TimeSpan ExitDuration
    {
        get => _exitDuration;
        set => _exitDuration = value >= TimeSpan.Zero ? value : throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(ExitDuration)} cannot be negative.");
    }

    /// <summary>The easing of the predefined entry effect. Null (the default) means <see cref="UIEasing.CubicOut"/>: a reveal that starts
    /// fast and settles into place.</summary>
    public IUIEasingFunction EnterEasing { get; set; }

    /// <summary>The easing of the predefined exit effect. Null (the default) means <see cref="UIEasing.CubicIn"/>: a dismissal that starts
    /// slow and accelerates away.</summary>
    public IUIEasingFunction ExitEasing { get; set; }

    /// <summary>The starting (entry) / ending (exit) uniform <c>RenderTransform.Scale</c> of <see cref="UIEnterExitEffect.Scale"/> and
    /// <see cref="UIEnterExitEffect.FadeScale"/>. Default: 0.9.</summary>
    public float ScaleFrom { get; set; } = 0.9f;

    /// <summary>The fixed pixel distance of every <c>Slide*</c> effect, independent of the element's bounds. Default: 24.</summary>
    public float SlideDistance { get; set; } = 24f;

    /// <summary>True when a call to <see cref="MGElement.Visibility"/>'s setter setting <see cref="UI.Visibility.Visible"/> should play an
    /// entry: an explicit <see cref="EnterAnimation"/>, or a predefined <see cref="EnterEffect"/> with a positive <see cref="EnterDuration"/>.</summary>
    internal bool HasEnter => EnterAnimation != null || (EnterEffect != UIEnterExitEffect.None && EnterDuration > TimeSpan.Zero);

    /// <summary>True when hiding the element should play an exit instead of applying the new <see cref="UI.Visibility"/> at once: an explicit
    /// <see cref="ExitAnimation"/>, or a predefined <see cref="ExitEffect"/> with a positive <see cref="ExitDuration"/>.</summary>
    internal bool HasExit => ExitAnimation != null || (ExitEffect != UIEnterExitEffect.None && ExitDuration > TimeSpan.Zero);
}
