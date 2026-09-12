namespace MGUI.Core.UI.Animation.Interpolation
{
    /// <summary>
    /// Interpolates between two values of <typeparamref name="T"/> for the animation engine
    /// (Docs/Tasks/animation-tasks.md, S1; ADR-0006).<para/>
    /// <c>amount</c> is the eased progress of an animation. It is usually in [0, 1] but an easing function
    /// may overshoot (<see cref="Easing.UIEasing.BackOut"/>, <see cref="Easing.UIEasing.ElasticOut"/>), so an
    /// implementation must not clamp it unless the value type cannot represent the overshoot (see
    /// <see cref="UIColorInterpolator"/>).<para/>
    /// Implementations are stateless and allocation-free. Built-in ones are registered in
    /// <see cref="UIInterpolators"/>; an application registers its own type with <see cref="UIInterpolators.Register{T}"/>.
    /// </summary>
    public interface IUIInterpolator<T>
    {
        /// <summary>Returns the value <paramref name="amount"/> of the way from <paramref name="from"/> to <paramref name="to"/>.</summary>
        T Lerp(T from, T to, float amount);
    }
}
