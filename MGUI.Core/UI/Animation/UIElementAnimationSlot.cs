using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;

namespace MGUI.Core.UI.Animation;

/// <summary>
/// The per-element animation state, allocated by <see cref="MGElement.Animations"/> on first access so that an element that never animates
/// costs one null reference (ADR-0006, cost budget): the <see cref="UIAnimationCollection"/>, the animated override of the state-driven scale,
/// and the transitions. Subscribes once to <see cref="MGElement.OnParentChanged"/>: an element leaving the tree
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

    /// <summary>The named visual states, allocated on first access.</summary>
    public States.UIVisualStateCollection VisualStates => _VisualStates ??= new States.UIVisualStateCollection(Owner);

    /// <summary>The named visual states, or null while none was ever accessed.</summary>
    public States.UIVisualStateCollection VisualStatesOrNull => _VisualStates;

    /// <summary>The animated value of the state-driven scale (<see cref="MGElement.RenderScale"/>), set by the <c>RenderScale</c> target; null when not animated.</summary>
    public float? StateScaleOverride { get; set; }

    private UILayoutTransform _LayoutTransform;

    /// <summary>The layout-transition transform (Y4), or null while the element has never run one.</summary>
    public UILayoutTransform LayoutTransformOrNull => _LayoutTransform;

    /// <summary>Allocates <see cref="LayoutTransformOrNull"/> on first access.</summary>
    public UILayoutTransform EnsureLayoutTransform() => _LayoutTransform ??= new UILayoutTransform();

    private UIPropertyAnimation<Vector2> _LayoutTransitionRun;

    /// <summary>The reused run of the element's layout-transition offset (Y4): one <see cref="UIPropertyAnimation{T}"/> instance per element,
    /// created on the first transition and restarted (never replaced) by every later one, targeting the explicit, unregistered
    /// <see cref="UILayoutTransitionOffsetTarget"/>.</summary>
    public UIPropertyAnimation<Vector2> EnsureLayoutTransitionRun() => _LayoutTransitionRun ??= new UIPropertyAnimation<Vector2>
    {
        Target = UILayoutTransitionOffsetTarget.Instance,
        To = Vector2.Zero,
        FillBehavior = UIAnimationFillBehavior.HoldEnd,
        CancelBehavior = UIAnimationCancelBehavior.KeepCurrent,
        InheritsBaseValue = false,
        Name = "layout-transition",
    };

    private UIPropertyAnimation<Vector2> _LayoutTransitionScaleRun;

    /// <summary>The reused run of the element's layout-transition size (Y5): one <see cref="UIPropertyAnimation{T}"/> instance per element,
    /// separate from <see cref="EnsureLayoutTransitionRun"/> (ADR-0011, "Decisions taken during delivery", Y5), created on the first size
    /// change and restarted (never replaced) by every later one, targeting the explicit, unregistered
    /// <see cref="UILayoutTransitionScaleTarget"/>.</summary>
    public UIPropertyAnimation<Vector2> EnsureLayoutTransitionScaleRun() => _LayoutTransitionScaleRun ??= new UIPropertyAnimation<Vector2>
    {
        Target = UILayoutTransitionScaleTarget.Instance,
        To = Vector2.One,
        FillBehavior = UIAnimationFillBehavior.HoldEnd,
        CancelBehavior = UIAnimationCancelBehavior.KeepCurrent,
        InheritsBaseValue = false,
        Name = "layout-transition-scale",
    };

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