using Microsoft.Xna.Framework;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Responsive;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

public class ResponsiveMetricsResolverTests
{
    [Fact]
    public void Resolver_ReturnsIdentityScale_AtDesignResolution()
    {
        UIResponsiveSettings settings = new(new UIDesignResolution(1920, 1080));

        UIResolvedMetrics metrics = UIResponsiveResolver.Resolve(settings, new Size(1920, 1080));

        Assert.Equal(1.0f, metrics.WidthRatio);
        Assert.Equal(1.0f, metrics.HeightRatio);
        Assert.Equal(1.0f, metrics.ViewportScaleFactor);
        Assert.Equal(1.0f, metrics.UIScaleFactor);
        Assert.Equal(1.0f, metrics.TextScaleFactor);
    }

    [Theory]
    [InlineData(1280, 720, 0.6666667f)]
    [InlineData(2560, 1440, 1.3333334f)]
    [InlineData(3440, 1440, 1.3333334f)]
    [InlineData(1080, 1920, 0.5625f)]
    public void Resolver_UsesUniformFitScale(int viewportWidth, int viewportHeight, float expectedScale)
    {
        UIResponsiveSettings settings = new(new UIDesignResolution(1920, 1080));

        UIResolvedMetrics metrics = UIResponsiveResolver.Resolve(settings, new Size(viewportWidth, viewportHeight));

        Assert.Equal(expectedScale, metrics.ViewportScaleFactor, 4);
        Assert.Equal(expectedScale, metrics.UIScaleFactor, 4);
    }

    [Fact]
    public void Resolver_ClampsUiAndTextScaleIndependently()
    {
        UIResponsiveSettings settings = new(
            new UIDesignResolution(1920, 1080),
            minUIScaleFactor: 0.8f,
            maxUIScaleFactor: 1.2f,
            textScaleMultiplier: 1.5f,
            minTextScaleFactor: 0.9f,
            maxTextScaleFactor: 1.4f);

        UIResolvedMetrics metrics = UIResponsiveResolver.Resolve(settings, new Size(3840, 2160));

        Assert.Equal(2.0f, metrics.ViewportScaleFactor, 4);
        Assert.Equal(1.2f, metrics.UIScaleFactor, 4);
        Assert.Equal(1.4f, metrics.TextScaleFactor, 4);
    }

    [Fact]
    public void Resolver_AppliesDpiOnlyWhenEnabled()
    {
        UIResponsiveSettings disabled = new(new UIDesignResolution(1920, 1080), useDpiScale: false);
        UIResponsiveSettings enabled = new(new UIDesignResolution(1920, 1080), useDpiScale: true, maxUIScaleFactor: 10.0f, maxTextScaleFactor: 10.0f);

        UIResolvedMetrics disabledMetrics = UIResponsiveResolver.Resolve(disabled, new Size(1920, 1080), 1.5f);
        UIResolvedMetrics enabledMetrics = UIResponsiveResolver.Resolve(enabled, new Size(1920, 1080), 1.5f);

        Assert.Equal(1.0f, disabledMetrics.DpiScaleFactor);
        Assert.Equal(1.0f, disabledMetrics.UIScaleFactor);
        Assert.Equal(1.5f, enabledMetrics.DpiScaleFactor);
        Assert.Equal(1.5f, enabledMetrics.UIScaleFactor);
    }

    [Theory]
    [InlineData(ResponsiveAnchor.TopLeft, 10, 20, 120, 60)]
    [InlineData(ResponsiveAnchor.TopRight, 190, 20, 120, 60)]
    [InlineData(ResponsiveAnchor.Center, 100, 90, 120, 60)]
    [InlineData(ResponsiveAnchor.BottomCenter, 100, 160, 120, 60)]
    public void OverlayAnchorBounds_AreResolvedAsExpected(ResponsiveAnchor anchor, int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        Rectangle available = new(10, 20, 300, 200);
        Size desired = new(120, 60);

        Rectangle actual = MGOverlayPanel.CreateAnchoredBounds(available, desired, anchor);

        Assert.Equal(new Rectangle(expectedX, expectedY, expectedWidth, expectedHeight), actual);
    }

    [Fact]
    public void OverlayAnchorBounds_StretchVariantsFillExpectedAxis()
    {
        Rectangle available = new(5, 10, 400, 160);
        Size desired = new(120, 40);

        Rectangle horizontal = MGOverlayPanel.CreateAnchoredBounds(available, desired, ResponsiveAnchor.StretchHorizontal);
        Rectangle vertical = MGOverlayPanel.CreateAnchoredBounds(available, desired, ResponsiveAnchor.StretchVertical);
        Rectangle full = MGOverlayPanel.CreateAnchoredBounds(available, desired, ResponsiveAnchor.Stretch);

        Assert.Equal(new Rectangle(5, 70, 400, 40), horizontal);
        Assert.Equal(new Rectangle(145, 10, 120, 160), vertical);
        Assert.Equal(available, full);
    }
}