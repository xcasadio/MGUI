using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Animation;

/// <summary>The animated values of an opted-in element's layout transition (ADR-0011 decision 5; Y4 delivers <see cref="Offset"/> only, a
/// later slice adds <c>Scale</c>), allocated on demand by <see cref="UIElementAnimationSlot"/>: an element that never opts in never
/// allocates one. Composed into the effective render matrix in <see cref="MGElement.TryGetRenderTransformMatrix"/> AFTER the state scale and
/// <see cref="UIRenderTransform"/>, so it is inverted by the hit-test exactly like them, and counted in
/// <see cref="MGDesktop.ActiveRenderTransformCount"/> while <see cref="IsIdentity"/> is false. Separate from
/// <see cref="UIRenderTransform.Translation"/>/<see cref="UIRenderTransform.Scale"/>, so an application animation on those coexists with a
/// layout transition instead of fighting over the same path.</summary>
internal sealed class UILayoutTransform
{
    /// <summary>The current layout-transition translation, in unscaled screen space. Written only through
    /// <see cref="MGElement.SetLayoutOffset"/>, the counting setter shared by <see cref="UILayoutTransitionOffsetTarget"/>.</summary>
    public Vector2 Offset { get; set; }

    /// <summary>True while <see cref="Offset"/> has no visible effect (within <see cref="UIRenderTransform.IdentityEpsilon"/>).</summary>
    public bool IsIdentity =>
        Math.Abs(Offset.X) <= UIRenderTransform.IdentityEpsilon &&
        Math.Abs(Offset.Y) <= UIRenderTransform.IdentityEpsilon;
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
