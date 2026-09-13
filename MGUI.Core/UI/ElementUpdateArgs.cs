using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI;

public readonly record struct ElementUpdateArgs(UpdateBaseArgs BA, bool IsEnabled, bool IsSelected, bool IsHitTestVisible, Point Offset, Rectangle ActualLayoutBounds)
{
    public ElementUpdateArgs AsZeroOffset() => this with { Offset = Point.Zero };
    public ElementUpdateArgs ChangeOffset(Point Value) => Offset == Value ? this : this with { Offset = Value };
    public ElementUpdateArgs ChangeHitTestVisible(bool Value) => IsHitTestVisible == Value ? this : this with { IsHitTestVisible = Value };
};