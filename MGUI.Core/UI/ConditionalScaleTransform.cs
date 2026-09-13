namespace MGUI.Core.UI;

/// <param name="PressedScale">A scale to apply to the target element when <see cref="MGElement.IsLMBPressed"/> is true</param>
/// <param name="HoveredScale">A scale to apply to the target element when <see cref="MGElement.IsHovered"/> is true</param>
public readonly record struct ConditionalScaleTransform(float PressedScale, float HoveredScale)
{
    public bool TryGetScale(VisualState VS, out float scale)
    {
        if (VS.Secondary == SecondaryVisualState.Pressed)
        {
            scale = PressedScale;
            return true;
        }
        else if (VS.Secondary == SecondaryVisualState.Hovered)
        {
            scale = HoveredScale;
            return true;
        }
        else
        {
            scale = 1.0f;
            return false;
        }
    }
}