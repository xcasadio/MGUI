using MonoGame.Extended;

namespace MGUI.Core.UI.Responsive;

public readonly record struct UIResolvedMetrics(
    UIDesignResolution DesignResolution,
    Size ViewportSize,
    float WidthRatio,
    float HeightRatio,
    float ViewportScaleFactor,
    float DpiScaleFactor,
    float UIScaleFactor,
    float TextScaleFactor,
    bool IsDpiApplied)
{
    public bool IsIdentity => UIScaleFactor == 1.0f && TextScaleFactor == 1.0f && DpiScaleFactor == 1.0f;
}