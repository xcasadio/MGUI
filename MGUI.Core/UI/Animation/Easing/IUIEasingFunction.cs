namespace MGUI.Core.UI.Animation.Easing
{
    /// <summary>
    /// Maps the linear progress of an animation, in [0, 1], to an eased progress (Docs/Tasks/animation-tasks.md, S1; ADR-0006).<para/>
    /// Every function satisfies <c>Ease(0) == 0</c> and <c>Ease(1) == 1</c>. The result may leave [0, 1] in between
    /// (<see cref="UIEasing.BackIn"/> undershoots, <see cref="UIEasing.BackOut"/> and <see cref="UIEasing.ElasticOut"/> overshoot);
    /// the interpolator of the animated value decides whether that overshoot is representable (see
    /// <see cref="Interpolation.IUIInterpolator{T}"/>). The input is not clamped either.<para/>
    /// Implementations are stateless and allocation-free. Built-in ones live in <see cref="UIEasing"/>.
    /// </summary>
    public interface IUIEasingFunction
    {
        float Ease(float amount);
    }
}
