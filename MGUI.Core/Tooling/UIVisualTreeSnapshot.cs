using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using System.Collections.Generic;

namespace MGUI.Core.Tooling
{
    public record UIVisualTreeSnapshot(
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