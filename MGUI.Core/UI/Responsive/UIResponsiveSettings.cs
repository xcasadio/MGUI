using System;

namespace MGUI.Core.UI.Responsive
{
    public readonly record struct UIResponsiveSettings
    {
        public UIDesignResolution DesignResolution { get; }
        public UIResponsiveScaleMode UIScaleMode { get; }
        public float MinUIScaleFactor { get; }
        public float MaxUIScaleFactor { get; }
        public float TextScaleMultiplier { get; }
        public float MinTextScaleFactor { get; }
        public float MaxTextScaleFactor { get; }
        public bool UseDpiScale { get; }

        public UIResponsiveSettings(
            UIDesignResolution designResolution,
            UIResponsiveScaleMode uiScaleMode = UIResponsiveScaleMode.UniformFit,
            float minUIScaleFactor = 0.5f,
            float maxUIScaleFactor = 4.0f,
            float textScaleMultiplier = 1.0f,
            float minTextScaleFactor = 0.85f,
            float maxTextScaleFactor = 3.0f,
            bool useDpiScale = false)
        {
            if (minUIScaleFactor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minUIScaleFactor));
            }

            if (maxUIScaleFactor < minUIScaleFactor)
            {
                throw new ArgumentOutOfRangeException(nameof(maxUIScaleFactor));
            }

            if (textScaleMultiplier <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(textScaleMultiplier));
            }

            if (minTextScaleFactor <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minTextScaleFactor));
            }

            if (maxTextScaleFactor < minTextScaleFactor)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTextScaleFactor));
            }

            DesignResolution = designResolution;
            UIScaleMode = uiScaleMode;
            MinUIScaleFactor = minUIScaleFactor;
            MaxUIScaleFactor = maxUIScaleFactor;
            TextScaleMultiplier = textScaleMultiplier;
            MinTextScaleFactor = minTextScaleFactor;
            MaxTextScaleFactor = maxTextScaleFactor;
            UseDpiScale = useDpiScale;
        }

        public static UIResponsiveSettings Default => new(UIDesignResolution.HD);
    }
}