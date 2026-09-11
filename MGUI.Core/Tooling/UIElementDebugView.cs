using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using System.Collections.Generic;

namespace MGUI.Core.Tooling
{
    /// <summary>Per-element debug view: what an editor needs to explain how one element looks, captured in a single call. It gathers the
    /// element's visual state, effective resource scope, applied control template and registered parts, and where its main visual values come
    /// from (see <see cref="UIValueOriginView"/>). Built by <see cref="UIToolingService.CaptureElementDebugView"/> and rendered as text by
    /// <see cref="UIToolingService.RenderElementDebugView"/>.</summary>
    public record UIElementDebugView(
        string DiagnosticId,
        string Name,
        MGElementType ElementType,
        PrimaryVisualState PrimaryVisualState,
        SecondaryVisualState SecondaryVisualState,
        UIResourceScope ResourceScope,
        string ResourceScopeOwnerDiagnosticId,
        bool HasLocalResourceScope,
        string AppliedControlTemplate,
        IReadOnlyDictionary<string, string> TemplateParts,
        string LastControlTemplateError,
        IReadOnlyList<UIValueOriginView> ValueOrigins);

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
}
