using MGUI.Shared.Helpers;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// The per-element animation state, allocated by <see cref="MGElement.Animations"/> on first access so that an element that never animates
/// costs one null reference (ADR-0006, cost budget): the <see cref="UIAnimationCollection"/>, the animated override of the state-driven scale
/// (S4) and, later, the transitions (S6). Subscribes once to <see cref="MGElement.OnParentChanged"/>: an element leaving the tree
/// (new parent null) clears its animations (decision 9), following the precedent of the dynamic resource subscriptions (ADR-0001).
/// </summary>
internal sealed class UIElementAnimationSlot
{
    public UIElementAnimationSlot(MGElement owner)
    {
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Animations = new UIAnimationCollection(owner);
        Transitions = new UITransitionCollection(owner);
        owner.OnParentChanged += HandleOwnerParentChanged;
    }

    public MGElement Owner { get; }

    public UIAnimationCollection Animations { get; }

    public UITransitionCollection Transitions { get; }

    private States.UIVisualStateCollection _VisualStates;

    /// <summary>The named visual states (T4), allocated on first access.</summary>
    public States.UIVisualStateCollection VisualStates => _VisualStates ??= new States.UIVisualStateCollection(Owner);

    /// <summary>The named visual states, or null while none was ever accessed.</summary>
    public States.UIVisualStateCollection VisualStatesOrNull => _VisualStates;

    /// <summary>The animated value of the state-driven scale (<see cref="MGElement.RenderScale"/>), set by the <c>RenderScale</c> target (S4); null when not animated.</summary>
    public float? StateScaleOverride { get; set; }

    private void HandleOwnerParentChanged(object sender, EventArgs<MGElement> e)
    {
        if (e.NewValue == null)
        {
            Animations.Clear();
        }
        else
        {
            Owner.SelfOrParentWindow?.Desktop?.Animations.RefreshOwnerWindow(Owner);
        }
    }
}