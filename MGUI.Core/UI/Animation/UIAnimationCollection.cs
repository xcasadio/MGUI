namespace MGUI.Core.UI.Animation;

/// <summary>
/// The animations owned by one <see cref="MGElement"/> (<c>element.Animations</c>; S3, ADR-0006 decision 9). Starting an animation here
/// binds it to the element and registers it with the desktop's <see cref="UIAnimationManager"/>; the element's animations are cancelled
/// (base values restored, held contributions released) when the element leaves the tree or its displaying window closes.
/// Allocated on first access: an element that never animates carries no collection.
/// </summary>
public sealed class UIAnimationCollection
{
    internal UIAnimationCollection(MGElement owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    /// <summary>The element that owns these animations.</summary>
    public MGElement Owner { get; }

    private UIAnimationManager Manager => Owner.SelfOrParentWindow?.Desktop?.Animations;

    /// <summary>Starts (or restarts) <paramref name="animation"/> on <see cref="Owner"/>. See <see cref="UIAnimationManager.Start"/> for the conflict rule.</summary>
    /// <exception cref="InvalidOperationException">The element has no desktop.</exception>
    public void Start(UIAnimation animation)
    {
        if (animation == null)
        {
            throw new ArgumentNullException(nameof(animation));
        }

        UIAnimationManager manager = Manager ?? throw new InvalidOperationException(
            $"The element has no desktop: an animation is ticked by the {nameof(UIAnimationManager)} of the desktop of its window.");
        manager.Start(Owner, animation);
    }

    /// <summary>Number of active animations owned by the element.</summary>
    public int Count => Manager?.CountOwnedBy(Owner) ?? 0;

    /// <summary>True while an animation is active on <paramref name="path"/>.</summary>
    public bool IsAnimating(string path) => Manager?.IsAnimating(Owner, path) ?? false;

    /// <summary>The active animations owned by the element (a snapshot).</summary>
    public IEnumerable<UIAnimation> Active => Manager?.EnumerateOwnedBy(Owner) ?? Enumerable.Empty<UIAnimation>();

    /// <summary>The completed animations still holding their <c>Animation</c> contribution in the resolved value store (a snapshot).</summary>
    public IEnumerable<UIAnimation> Held => Manager?.EnumerateHeldBy(Owner) ?? Enumerable.Empty<UIAnimation>();

    /// <summary>Cancels every active animation of the element according to its own <see cref="UIAnimation.CancelBehavior"/>. Held contributions stay.</summary>
    public void CancelAll() => Manager?.CancelOwnedBy(Owner, restoreBaseValue: false, releaseHolds: false);

    /// <summary>Cancels every active animation of the element, restores every base value and releases the held contributions:
    /// the element is left as if it had never been animated. This is what detachment and window closing do.</summary>
    public void Clear() => Manager?.CancelOwnedBy(Owner, restoreBaseValue: true, releaseHolds: true);
}