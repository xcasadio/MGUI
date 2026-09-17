using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Animation;

/// <summary>The animated values of an opted-in element's layout transition (ADR-0011 decision 5; Y4 delivered <see cref="Offset"/>, Y5 adds
/// <see cref="Scale"/>), allocated on demand by <see cref="UIElementAnimationSlot"/>: an element that never opts in never
/// allocates one. Composed into the effective render matrix in <see cref="MGElement.TryGetRenderTransformMatrix"/> AFTER the state scale and
/// <see cref="UIRenderTransform"/>, so it is inverted by the hit-test exactly like them, and counted in
/// <see cref="MGDesktop.ActiveRenderTransformCount"/> while <see cref="IsIdentity"/> is false (one contribution for both fields together,
/// never two). Separate from <see cref="UIRenderTransform.Translation"/>/<see cref="UIRenderTransform.Scale"/>, so an application animation
/// on those coexists with a layout transition instead of fighting over the same path.</summary>
internal sealed class UILayoutTransform
{
    /// <summary>The current layout-transition translation, in unscaled screen space. Written only through
    /// <see cref="MGElement.SetLayoutOffset"/>, the counting setter shared by <see cref="UILayoutTransitionOffsetTarget"/>.</summary>
    public Vector2 Offset { get; set; }

    /// <summary>The current layout-transition scale (Y5), one per axis, applied around the top-left corner of the element's current
    /// <see cref="MGElement.LayoutBounds"/> before <see cref="Offset"/>. <see cref="Vector2.One"/> (the default) has no visible effect.
    /// Written only through <see cref="MGElement.SetLayoutScale"/>, the counting setter shared by <see cref="UILayoutTransitionScaleTarget"/>.</summary>
    public Vector2 Scale { get; set; } = Vector2.One;

    /// <summary>True while neither <see cref="Offset"/> nor <see cref="Scale"/> has a visible effect (within
    /// <see cref="UIRenderTransform.IdentityEpsilon"/>).</summary>
    public bool IsIdentity =>
        Math.Abs(Offset.X) <= UIRenderTransform.IdentityEpsilon &&
        Math.Abs(Offset.Y) <= UIRenderTransform.IdentityEpsilon &&
        Math.Abs(Scale.X - 1f) <= UIRenderTransform.IdentityEpsilon &&
        Math.Abs(Scale.Y - 1f) <= UIRenderTransform.IdentityEpsilon;
}

/// <summary>The explicit, unregistered target of the internal layout-transition run (ADR-0011 decision 5): it has no entry in
/// <see cref="UIAnimationTargets"/>, so it is invisible to the registry, to XAML, to <see cref="UITransition{T}"/> and to the serializer.
/// One shared, stateless instance serves every element, like the built-in targets.<para/>
/// <see cref="RestoreBaseValue"/> departs from a plain delegate target (see <see cref="UIDelegateAnimationTarget{T}"/>): it ignores
/// <c>baseValue</c> entirely and always writes <see cref="Vector2.Zero"/>, because a layout offset has no meaningful base value other than
/// identity -- there is nothing else for a cancelled run to fall back on.</summary>
internal sealed class UILayoutTransitionOffsetTarget : IUIAnimationTarget<Vector2>
{
    public static readonly UILayoutTransitionOffsetTarget Instance = new();

    private UILayoutTransitionOffsetTarget() { }

    public string Path => "LayoutTransition.Offset";

    public bool IsStoreBacked => false;

    public Vector2 GetValue(MGElement element) => element.LayoutOffset;

    public void SetValue(MGElement element, Vector2 value, string animationName) => element.SetLayoutOffset(value);

    public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.SetLayoutOffset(Vector2.Zero);
}

/// <summary>The explicit, unregistered target of the internal layout-transition size run (ADR-0011 decision 5, Y5): a second reused run
/// alongside <see cref="UILayoutTransitionOffsetTarget"/> (one animation over two independent Vector2 values, as the ADR records), since
/// both are driven together by the same size change but there is no combined value type for a single run to target. Same shape as its
/// offset counterpart: no entry in <see cref="UIAnimationTargets"/>, invisible to XAML, transitions and the serializer, one shared, stateless
/// instance.<para/>
/// <see cref="RestoreBaseValue"/> ignores <c>baseValue</c> and always writes <see cref="Vector2.One"/>, the scale's own identity.</summary>
internal sealed class UILayoutTransitionScaleTarget : IUIAnimationTarget<Vector2>
{
    public static readonly UILayoutTransitionScaleTarget Instance = new();

    private UILayoutTransitionScaleTarget() { }

    public string Path => "LayoutTransition.Scale";

    public bool IsStoreBacked => false;

    public Vector2 GetValue(MGElement element) => element.LayoutScale;

    public void SetValue(MGElement element, Vector2 value, string animationName) => element.SetLayoutScale(value);

    public void RestoreBaseValue(MGElement element, Vector2 baseValue) => element.SetLayoutScale(Vector2.One);
}
