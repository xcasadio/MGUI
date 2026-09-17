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

    #region Enter and Exit (Y6)
    /// <summary>True while an exit run is in progress (ADR-0011 decision 6): <see cref="MGElement.Visibility"/> keeps its value in effect
    /// (<see cref="UI.Visibility.Visible"/>) until the run ends, and <see cref="PendingExitVisibility"/> exposes the requested value.</summary>
    public bool IsExitingEnterExit { get; set; }

    /// <summary>The <see cref="UI.Visibility"/> requested while <see cref="IsExitingEnterExit"/> is true; null once the exit ends.</summary>
    public Visibility? PendingExitVisibility { get; set; }

    /// <summary>The entry or exit run currently in progress (an explicit <see cref="UIEnterExitSettings.EnterAnimation"/> /
    /// <see cref="UIEnterExitSettings.ExitAnimation"/>, or the <see cref="UIStoryboard"/> a predefined effect builds), null at rest. Cancelling
    /// it to let a new one supersede it (an entry interrupting an exit, or the reverse) is done without restoring base values, so the new run
    /// starts from the current values with no jump (ADR-0011 decision 6).</summary>
    public UIAnimation ActiveEnterExitRun { get; set; }

    /// <summary>True once <see cref="BaseOpacity"/>, <see cref="BaseScale"/>, <see cref="BaseTranslation"/> and <see cref="BaseOrigin"/> have
    /// been captured for the entry/exit cycle in progress. Cleared at the end of every run (completed or cancelled, entry or exit), so the
    /// next cycle that starts from rest captures a fresh base (ADR-0011 decision 6: "captured when a cycle starts from rest, not mid-run").</summary>
    public bool HasCapturedEnterExitBase { get; set; }

    /// <summary>The element's <see cref="MGElement.Opacity"/> captured at the start of the current entry/exit cycle: the value a predefined
    /// <see cref="UIEnterExitEffect.Fade"/>/<see cref="UIEnterExitEffect.FadeScale"/> entry heads to, or its exit starts from.</summary>
    public float BaseOpacity { get; set; }

    /// <summary>The element's <see cref="Animation.UIRenderTransform.Scale"/> captured at the start of the current entry/exit cycle.</summary>
    public Vector2 BaseScale { get; set; }

    /// <summary>The element's <see cref="Animation.UIRenderTransform.Translation"/> captured at the start of the current entry/exit cycle.</summary>
    public Vector2 BaseTranslation { get; set; }

    /// <summary>The element's <see cref="Animation.UIRenderTransform.Origin"/> captured at the start of the current entry/exit cycle, restored
    /// once a <see cref="UIEnterExitEffect.Scale"/>/<see cref="UIEnterExitEffect.FadeScale"/> run releases <see cref="EnterExitOriginHeld"/>.</summary>
    public Vector2 BaseOrigin { get; set; }

    /// <summary>True while <see cref="Animation.UIRenderTransform.Origin"/> is pinned at (0.5, 0.5) for the duration of a
    /// <see cref="UIEnterExitEffect.Scale"/>/<see cref="UIEnterExitEffect.FadeScale"/> run.</summary>
    public bool EnterExitOriginHeld { get; set; }

    /// <summary>Y7 (ADR-0011 decision 6, window lifecycle): the completion the exit run invokes instead of writing
    /// <see cref="MGElement.Visibility"/>, used when the exit was started by <see cref="MGElement"/>'s window overload rather than by a
    /// <see cref="UI.Visibility"/> write. Null for every Y6 (element) exit, so <see cref="PendingExitVisibility"/> is what applies then.</summary>
    public Action PendingExitCompletion { get; set; }

    private UIWindowTransform _WindowTransform;

    /// <summary>The window's internal enter/exit draw transform (Y7), or null while the window has never run one.</summary>
    public UIWindowTransform WindowTransformOrNull => _WindowTransform;

    /// <summary>Allocates <see cref="WindowTransformOrNull"/> on first access.</summary>
    public UIWindowTransform EnsureWindowTransform() => _WindowTransform ??= new UIWindowTransform();
    #endregion
}