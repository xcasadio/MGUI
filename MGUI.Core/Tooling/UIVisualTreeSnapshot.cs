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
        int Depth,
        IReadOnlyList<UIVisualTreeSnapshot> Children);
}