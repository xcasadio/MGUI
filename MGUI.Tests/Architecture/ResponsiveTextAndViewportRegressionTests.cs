using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Responsive;
using MGUI.Shared.Helpers;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Architecture;

/// <summary>Covers Docs/Tasks/layout-tasks.md task 2 ("Completer la couverture de tests de regression layout sous scale responsive"),
/// alongside <see cref="ResponsiveLayoutRegressionTests"/>. This file covers:<list type="bullet">
/// <item><see cref="MGTextBlock"/> font/measurement scaling through <see cref="MGDesktop.ResponsiveMetrics"/>.TextScaleFactor, independently
/// of UIScaleFactor (using <see cref="MGTextBlock.EffectiveFontSize"/>, made internal for this task);</item>
/// <item>full <see cref="MGResponsiveRoot"/> layouts (a standard container plus anchored children) at extreme viewport ratios: stability
/// across an extra <see cref="MGDesktop.Update"/> call, containment within the root, and the <see cref="MGOverlayPanel.CreateAnchoredBounds"/>
/// formula holding for the resolved external spacing.</item>
/// </list>
/// See Docs/layout-architecture.md.</summary>
public class ResponsiveTextAndViewportRegressionTests
{
    private static readonly UIDesignResolution Design = new(1920, 1080);

    #region 1. MGTextBlock: TextScaleFactor is independent of UIScaleFactor

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGResponsiveRoot Root);

    /// <summary>Builds a harness whose <see cref="MGDesktop.ResponsiveMetrics"/>.UIScaleFactor is EXACTLY <paramref name="uiScaleFactor"/>
    /// (min/max UI clamps pinned to that value, matching <see cref="ResponsiveLayoutRegressionTests"/>'s harness), and whose text clamps are
    /// left wide open so TextScaleFactor resolves to the unclamped <c>uiScaleFactor * textScaleMultiplier</c> product.</summary>
    private static Harness CreateHarness(int viewportWidth, int viewportHeight, float uiScaleFactor, float textScaleMultiplier)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, viewportWidth, viewportHeight));
        MGDesktop desktop = new(runtime);
        desktop.ResponsiveSettings = new UIResponsiveSettings(
            Design,
            minUIScaleFactor: uiScaleFactor,
            maxUIScaleFactor: uiScaleFactor,
            textScaleMultiplier: textScaleMultiplier,
            minTextScaleFactor: 0.01f,
            maxTextScaleFactor: 100f);

        MGWindow window = new(desktop, 0, 0, viewportWidth, viewportHeight)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
        };
        desktop.Windows.Add(window);

        MGResponsiveRoot root = new(window);
        window.SetContent(root);

        return new Harness(runtime, desktop, window, root);
    }

    [Theory]
    [InlineData(0.5f, 2.0f)]  // UIScaleFactor 0.5, TextScaleFactor 1.0 (multiplier exactly cancels the UI scale-down)
    [InlineData(2.0f, 0.25f)] // UIScaleFactor 2.0, TextScaleFactor 0.5 (multiplier undershoots the UI scale-up)
    public void TextBlock_EffectiveFontSizeAndMeasurement_FollowTextScaleFactor_IndependentOfUIScaleFactor(float uiScaleFactor, float textScaleMultiplier)
    {
        Harness h = CreateHarness(960, 540, uiScaleFactor, textScaleMultiplier);

        const int designFontSize = 20;
        const int designMargin = 10;

        MGTextBlock text = new(h.Window, "Hi", null, designFontSize)
        {
            UseResponsiveTextScale = true,
            WrapText = false,
            Padding = new Thickness(0), // MGTextBlock defaults to Padding(1): zero it so measured height isolates the font-size scaling from ResolvedPadding (which scales with UIScaleFactor, not TextScaleFactor)
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        //  Sibling used only to demonstrate that ResolvedMargin follows UIScaleFactor while the text block's font follows the
        //  (independently configured, here diverging) TextScaleFactor.
        MGBorder sibling = new(h.Window)
        {
            Margin = new Thickness(designMargin),
            PreferredWidth = 4,
            PreferredHeight = 4,
            ScaleDimensionsWithResponsive = false, // isolate margin scaling from the (out-of-scope) dimension scaling
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };

        MGStackPanel panel = new(h.Window, Orientation.Vertical)
        {
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.TryAddChild(text);
        panel.TryAddChild(sibling);
        h.Root.TryAddChild(panel);

        h.Desktop.Update();
        h.Desktop.Update();

        float expectedTextScaleFactor = uiScaleFactor * textScaleMultiplier;
        Assert.Equal(expectedTextScaleFactor, h.Desktop.ResponsiveMetrics.TextScaleFactor, 4);
        Assert.NotEqual(uiScaleFactor, expectedTextScaleFactor); // the two scales genuinely diverge for both InlineData cases

        //  EffectiveFontSize (MGTextBlock.cs:82) evaluated with TextScaleFactor, not UIScaleFactor.
        int expectedFontSize = System.Math.Max(1, UIResponsiveMath.ScaleInt(designFontSize, expectedTextScaleFactor));
        Assert.Equal(expectedFontSize, text.EffectiveFontSize);
        //  Measured size follows suit: a single short line's height equals its resolved font size in the headless text engine
        //  (GraphTestRuntime.cs: LineHeight = size). The panel arranges each child to its own measured size (MGStackPanel.cs
        //  UpdateContentLayout), so the text block's AllocatedBounds is tightly fit rather than the full available area.
        Assert.Equal(expectedFontSize, text.AllocatedBounds.Height);

        //  Explicit divergence: the sibling's margin follows UIScaleFactor, not TextScaleFactor.
        Thickness expectedSiblingMargin = UIResponsiveMath.ScaleThickness(new Thickness(designMargin), uiScaleFactor);
        Assert.Equal(expectedSiblingMargin, sibling.ResolvedMargin);
        Assert.NotEqual(new Thickness(designMargin), expectedSiblingMargin);
        Assert.NotEqual(expectedFontSize, UIResponsiveMath.ScaleInt(designFontSize, uiScaleFactor)); // font would differ if it (wrongly) followed UIScaleFactor
    }

    [Theory]
    [InlineData(0.5f)]
    [InlineData(2.0f)]
    public void TextBlock_EffectiveFontSize_PinnedToFontSize_WhenResponsiveTextScaleDisabled(float uiScaleFactor)
    {
        //  Aggressive multiplier (3.0) proves it is genuinely ignored, not just numerically close to identity.
        Harness h = CreateHarness(960, 540, uiScaleFactor, textScaleMultiplier: 3.0f);

        const int designFontSize = 16;
        MGTextBlock text = new(h.Window, "Hi", null, designFontSize)
        {
            UseResponsiveTextScale = false,
            WrapText = false,
            Padding = new Thickness(0), // isolate the font-size measurement from ResolvedPadding (which scales with UIScaleFactor)
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        //  MGResponsiveRoot hands a ResponsiveAnchor.None child the FULL available bounds (MGOverlayPanel.UpdateContentLayout), not a
        //  tightly-fit one; wrap in a StackPanel (which arranges each child to its own measured size) to observe the text's own height.
        MGStackPanel panel = new(h.Window, Orientation.Vertical)
        {
            Spacing = 0,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        panel.TryAddChild(text);
        h.Root.TryAddChild(panel);

        h.Desktop.Update();
        h.Desktop.Update();

        Assert.NotEqual(1.0f, h.Desktop.ResponsiveMetrics.TextScaleFactor); // the desktop's scale really is non-identity here...
        Assert.Equal(designFontSize, text.EffectiveFontSize);               // ...yet EffectiveFontSize stays pinned to FontSize
        Assert.Equal(designFontSize, text.AllocatedBounds.Height);
    }

    #endregion

    #region 2. Extreme viewport ratios: a small MGResponsiveRoot tree stays coherent, contained and stable

    private readonly record struct ViewportHarness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGResponsiveRoot Root);

    /// <summary>Builds a fresh runtime/desktop/window/<see cref="MGResponsiveRoot"/> for one viewport size. A new <see cref="GraphTestRuntime"/>
    /// is required per viewport because its surface bounds are readonly (GraphTestRuntime.cs) - an existing runtime cannot be resized.
    /// Uses the default 0.5/4.0 UIScaleFactor clamps so the tiny-viewport case in
    /// <see cref="ExtremeViewports_StackPanelAndAnchoredChildren_StayWithinRootBounds_AndAreStable"/> actually exercises the clamp.</summary>
    private static ViewportHarness CreateViewportHarness(int viewportWidth, int viewportHeight)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, viewportWidth, viewportHeight));
        MGDesktop desktop = new(runtime);
        desktop.ResponsiveSettings = new UIResponsiveSettings(Design);

        MGWindow window = new(desktop, 0, 0, viewportWidth, viewportHeight)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
            BorderThickness = new Thickness(0),
        };
        desktop.Windows.Add(window);

        MGResponsiveRoot root = new(window);
        window.SetContent(root);

        return new ViewportHarness(runtime, desktop, window, root);
    }

    private static void AssertWithin(Rectangle outer, Rectangle inner)
    {
        Assert.True(
            inner.Left >= outer.Left && inner.Top >= outer.Top && inner.Right <= outer.Right && inner.Bottom <= outer.Bottom,
            $"Expected {inner} to lie within {outer}.");
    }

    [Theory]
    [InlineData(3440, 1440)] // ultra-wide
    [InlineData(1080, 1920)] // portrait
    [InlineData(200, 150)]   // tiny: exercises the MinUIScaleFactor clamp
    public void ExtremeViewports_StackPanelAndAnchoredChildren_StayWithinRootBounds_AndAreStable(int viewportWidth, int viewportHeight)
    {
        ViewportHarness h = CreateViewportHarness(viewportWidth, viewportHeight);

        MGStackPanel panel = new(h.Window, Orientation.Vertical)
        {
            Spacing = 8,
            Margin = new Thickness(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        MGBorder panelChild = new(h.Window)
        {
            PreferredWidth = 20,
            PreferredHeight = 10,
            ScaleDimensionsWithResponsive = false,
        };
        panel.TryAddChild(panelChild);

        MGBorder bottomRightChild = new(h.Window)
        {
            PreferredWidth = 40,
            PreferredHeight = 20,
            ScaleDimensionsWithResponsive = false, // isolate anchor/offset behavior from (out-of-scope) dimension scaling
            ResponsiveAnchor = ResponsiveAnchor.BottomRight,
        };
        Thickness bottomRightOffset = new(10);

        MGBorder stretchChild = new(h.Window)
        {
            ResponsiveAnchor = ResponsiveAnchor.Stretch,
        };
        Thickness stretchOffset = new(6);

        Assert.True(h.Root.TryAddChild(panel));
        Assert.True(h.Root.TryAddChild(bottomRightChild, bottomRightOffset));
        Assert.True(h.Root.TryAddChild(stretchChild, stretchOffset));

        h.Desktop.Update();
        h.Desktop.Update();

        //  Every bounds lies inside the root's own layout bounds.
        AssertWithin(h.Root.LayoutBounds, panel.AllocatedBounds);
        AssertWithin(h.Root.LayoutBounds, bottomRightChild.AllocatedBounds);
        AssertWithin(h.Root.LayoutBounds, stretchChild.AllocatedBounds);

        //  Anchored bounds equal MGOverlayPanel.CreateAnchoredBounds(root content bounds reduced by the resolved external spacing,
        //  desired size, anchor) - the exact formula MGOverlayPanel.UpdateContentLayout applies.
        Thickness resolvedBottomRightOffset = h.Root.ResolveExternalSpacing(bottomRightOffset);
        Rectangle availableForBottomRight = h.Root.AlignedContentBounds.GetCompressed(resolvedBottomRightOffset);
        bottomRightChild.UpdateMeasurement(availableForBottomRight.Size, out _, out Thickness bottomRightFullSize, out _, out _);
        Rectangle expectedBottomRight = MGOverlayPanel.CreateAnchoredBounds(availableForBottomRight, bottomRightFullSize.Size, ResponsiveAnchor.BottomRight);
        Assert.Equal(expectedBottomRight, bottomRightChild.AllocatedBounds);

        Thickness resolvedStretchOffset = h.Root.ResolveExternalSpacing(stretchOffset);
        Rectangle availableForStretch = h.Root.AlignedContentBounds.GetCompressed(resolvedStretchOffset);
        //  Stretch always fills the available bounds regardless of desired size (MGOverlayPanel.CreateAnchoredBounds).
        Assert.Equal(availableForStretch, stretchChild.AllocatedBounds);

        //  Stability: a third Update() with an unchanged tree/settings leaves every bounds unchanged.
        Rectangle panelBefore = panel.AllocatedBounds;
        Rectangle bottomRightBefore = bottomRightChild.AllocatedBounds;
        Rectangle stretchBefore = stretchChild.AllocatedBounds;

        h.Desktop.Update();

        Assert.Equal(panelBefore, panel.AllocatedBounds);
        Assert.Equal(bottomRightBefore, bottomRightChild.AllocatedBounds);
        Assert.Equal(stretchBefore, stretchChild.AllocatedBounds);

        if (viewportWidth == 200 && viewportHeight == 150)
        {
            //  The tiny viewport's raw ViewportScaleFactor (min(200/1920, 150/1080) ~= 0.104) is well below MinUIScaleFactor (0.5,
            //  the default from UIResponsiveSettings): the clamp must be the one actually applied.
            Assert.Equal(h.Desktop.ResponsiveSettings.MinUIScaleFactor, h.Desktop.ResponsiveMetrics.UIScaleFactor, 5);
        }
    }

    #endregion
}
