using MGUI.Shared.Rendering;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI;

public readonly record struct ElementDrawArgs(DrawBaseArgs BA, VisualState VisualState, Point Offset)
{
    public TimeSpan TS => BA.TS;
    public IUIRenderContext Context => BA.Context;
    public IUIDrawTransaction DT => BA.DT;
    public float Opacity => BA.Opacity;
    public bool IsEnabled => !VisualState.IsDisabled;
    public bool IsSelected => VisualState.IsSelected;

    public ElementDrawArgs SetOpacity(float Value) => this with { BA = BA.SetOpacity(Value) };
    public ElementDrawArgs SetOffset(Point Value) => this with { Offset = Value };

    public ElementDrawArgs AsZeroOffset() => this with { Offset = Point.Zero };
}