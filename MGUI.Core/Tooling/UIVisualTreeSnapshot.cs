using Microsoft.Xna.Framework;
using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using System.Collections.Generic;

namespace MGUI.Core.Tooling
{
    /// <summary>Serializable tooling snapshot for a visual subtree, including both stable diagnostic IDs and runtime-unique IDs.</summary>
    public record UIVisualTreeSnapshot(
        string DiagnosticId,
        string WindowDiagnosticId,
        string RuntimeUniqueId,
        Visibility Visibility,
        bool IsEffectivelyVisible,
        bool IsHitTestVisible,
        bool CanReceiveMouseInput,
        bool CanReceiveKeyboardInput,
        bool HasKeyboardFocus,
        bool IsHovered,
        PrimaryVisualState PrimaryVisualState,
        SecondaryVisualState SecondaryVisualState,
        bool ClipToBounds,
        bool RecentDrawWasClipped,
        string Name,
        MGElementType ElementType,
        Rectangle LayoutBounds,
        Rectangle ActualLayoutBounds,
        string AppliedControlTemplate,
        IReadOnlyDictionary<string, string> TemplateParts,
        string LastControlTemplateError,
        UIResourceScope ResourceScope,
        string ResourceScopeOwnerDiagnosticId,
        bool HasLocalResourceScope,
        int Depth,
        IReadOnlyList<UIVisualTreeSnapshot> Children);
}