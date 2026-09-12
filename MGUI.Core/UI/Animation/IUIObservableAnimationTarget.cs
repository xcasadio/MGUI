namespace MGUI.Core.UI.Animation
{
    /// <summary>
    /// An <see cref="IUIAnimationTarget{T}"/> whose underlying value can be watched, which is what a <see cref="UITransition{T}"/> needs
    /// (S6; ADR-0006 decisions 4 and 11): the transition subscribes once per element, and whenever the underlying value changes for a reason
    /// other than the transition's own writes, it animates from the value it last settled on to the new one.<para/>
    /// The underlying value is the value the property would have without the animation's contribution: the CLR value of a plain property, the
    /// effective store value of a pilot (its setter only notifies when the effective value changes, so an animated pilot does not notify local
    /// writes until the animation ends), and the state-driven scale for <c>RenderScale</c> (never the animated override). Built-in targets
    /// implement it; an application target that does not cannot be used in a transition.
    /// </summary>
    public interface IUIObservableAnimationTarget<T> : IUIAnimationTarget<T>
    {
        /// <summary>Subscribes <paramref name="changed"/> to changes of the underlying value on <paramref name="element"/>; dispose to unsubscribe.</summary>
        IDisposable Subscribe(MGElement element, Action<MGElement> changed);

        /// <summary>The underlying value (see the type summary).</summary>
        T GetUnderlyingValue(MGElement element);
    }
}
