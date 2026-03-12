using MonoGame.Extended;
using System;

namespace MGUI.Core.UI.Responsive
{
    public static class UIResponsiveResolver
    {
        public static UIResolvedMetrics Resolve(UIResponsiveSettings settings, Size viewportSize, float dpiScale = 1.0f)
        {
            if (viewportSize.Width <= 0 || viewportSize.Height <= 0)
            {
                return new UIResolvedMetrics(
                    settings.DesignResolution,
                    viewportSize,
                    0,
                    0,
                    1.0f,
                    1.0f,
                    1.0f,
                    1.0f,
                    false);
            }

            float widthRatio = viewportSize.Width / (float)settings.DesignResolution.Width;
            float heightRatio = viewportSize.Height / (float)settings.DesignResolution.Height;

            float viewportScale = settings.UIScaleMode switch
            {
                UIResponsiveScaleMode.UniformFit => Math.Min(widthRatio, heightRatio),
                _ => throw new NotImplementedException($"Unrecognized {nameof(UIResponsiveScaleMode)}: {settings.UIScaleMode}"),
            };

            float actualDpiScale = settings.UseDpiScale ? Math.Max(0.1f, dpiScale) : 1.0f;
            float uiScale = UIResponsiveMath.ClampPositive(viewportScale * actualDpiScale, settings.MinUIScaleFactor, settings.MaxUIScaleFactor);
            float textScale = UIResponsiveMath.ClampPositive(uiScale * settings.TextScaleMultiplier, settings.MinTextScaleFactor, settings.MaxTextScaleFactor);

            return new UIResolvedMetrics(
                settings.DesignResolution,
                viewportSize,
                widthRatio,
                heightRatio,
                viewportScale,
                actualDpiScale,
                uiScale,
                textScale,
                settings.UseDpiScale);
        }
    }
}