namespace MGUI.Core.UI.Animation;

/// <summary>What happens to the animated value when a <see cref="UIAnimation"/> is cancelled (ADR-0006, decision 10: the default is <see cref="RestoreBaseValue"/>).</summary>
public enum UIAnimationCancelBehavior
{
    /// <summary>The property goes back to its base value (see <see cref="UIAnimationFillBehavior.RestoreBaseValue"/>).</summary>
    RestoreBaseValue,

    /// <summary>The value written by the last tick stays. This is what a replacing animation applies to the one it replaces, so that it can
    /// start from the current animated value without any visible snap (ADR-0006, decision 11).</summary>
    KeepCurrent,
}