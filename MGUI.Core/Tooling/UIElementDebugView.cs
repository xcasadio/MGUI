using MGUI.Core.UI;
using MGUI.Core.UI.Styling;

namespace MGUI.Core.Tooling;

/// <summary>Per-element debug view: what an editor needs to explain how one element looks, captured in a single call. It gathers the
/// element's visual state, effective resource scope, applied control template and registered parts, and where its main visual values come
/// from (see <see cref="UIValueOriginView"/>). Built by <see cref="UIToolingService.CaptureElementDebugView"/> and rendered as text by
/// <see cref="UIToolingService.RenderElementDebugView"/>. <paramref name="VisualStateName"/> is the named visual state currently applied
/// (<see cref="MGElement.CurrentVisualStateName"/>, ADR-0007), null when the element defines none or none matches.</summary>
public record UIElementDebugView(
    string DiagnosticId,
    string Name,
    MGElementType ElementType,
    PrimaryVisualState PrimaryVisualState,
    SecondaryVisualState SecondaryVisualState,
    string VisualStateName,
    UIResourceScope ResourceScope,
    string ResourceScopeOwnerDiagnosticId,
    bool HasLocalResourceScope,
    string AppliedControlTemplate,
    IReadOnlyDictionary<string, string> TemplateParts,
    string LastControlTemplateError,
    IReadOnlyList<UIValueOriginView> ValueOrigins,
    IReadOnlyList<UIAnimationDebugView> Animations);

/// <summary>One animation or transition of an element, for the debug view (ADR-0006, S8).</summary>
/// <param name="Kind"><c>animation</c>, <c>held</c> (a completed animation still holding its store contribution) or <c>transition</c>.</param>
/// <param name="Path">The animated property path.</param>
/// <param name="State">The playback state, or <c>idle</c> / <c>running</c> for a transition.</param>
/// <param name="Progress">Raw progress of the current pass in [0, 1], null for an idle transition.</param>
/// <param name="Name">The animation's name, or null.</param>
public record UIAnimationDebugView(string Kind, string Path, string State, float? Progress, string Name);

/// <summary>Where one visual value of an element comes from.</summary>
/// <param name="PropertyPath">The property path, one of <see cref="UIToolingService.ResolvedValueSourcePropertyPaths"/>.</param>
/// <param name="IsResolved">False when the element cannot resolve the path (for example it has no border) or no source wrote the value;
/// <see cref="Source"/> is then <c>default</c>.</param>
/// <param name="Source">The winning source, as reported by <see cref="UIToolingService.TryGetResolvedValueSource"/>.</param>
/// <param name="EffectiveValue">Text of the value currently in effect, or null when the element has no such value.</param>
/// <param name="Contributions">Every contribution the resolved value store recorded for this value, highest precedence first. It can be
/// empty while <see cref="IsResolved"/> is true when the source is a read-time fall-back, such as a text foreground inherited from an
/// ancestor.</param>
public record UIValueOriginView(
    string PropertyPath,
    bool IsResolved,
    UIValueResolutionSource Source,
    string EffectiveValue,
    IReadOnlyList<UIResolvedContribution> Contributions);