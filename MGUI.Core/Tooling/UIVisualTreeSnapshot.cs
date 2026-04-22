using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using System.Collections.Generic;

namespace MGUI.Core.Tooling
{
    /// <summary>Serializable tooling snapshot for a visual subtree, including both stable diagnostic IDs and runtime-unique IDs.</summary>
    public record UIVisualTreeSnapshot(
        string DiagnosticId,
        string WindowDiagnosticId,
        string RuntimeUniqueId,
        string Name,
        MGElementType ElementType,
        Rectangle LayoutBounds,
        Rectangle ActualLayoutBounds,
        string AppliedControlTemplate,
        IReadOnlyDictionary<string, string> TemplateParts,
        string LastControlTemplateError,
        int Depth,
        IReadOnlyList<UIVisualTreeSnapshot> Children);
}