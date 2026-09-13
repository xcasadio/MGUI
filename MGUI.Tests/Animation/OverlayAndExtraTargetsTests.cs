using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Interpolation;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Animation;

/// <summary>Slice T3 of Docs/Tasks/animation-v2-tasks.md: overlay cross-fade, preferred size and gradient targets.</summary>
public class OverlayAndExtraTargetsTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void OverlayOpacity_ScalesTheHoverOverlay_AtDraw()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Panel.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Black);
        scene.Panel.BackgroundBrush.FocusedColor = Color.White;
        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(2);
        Assert.True(scene.Panel.VisualState.IsHovered);

        GraphNoOpDrawTransaction full = scene.Draw();
        Assert.Contains(full.FillRectangleCalls, call => call.Color == Color.White);

        scene.Panel.BackgroundBrush.OverlayOpacity = 0.5f;
        GraphNoOpDrawTransaction half = scene.Draw();
        Assert.Contains(half.FillRectangleCalls, call => call.Color == Color.White * 0.5f);
        Assert.DoesNotContain(half.FillRectangleCalls, call => call.Color == Color.White);

        scene.Panel.BackgroundBrush.OverlayOpacity = 0f;
        GraphNoOpDrawTransaction none = scene.Draw();
        Assert.DoesNotContain(none.FillRectangleCalls, call => call.Color == Color.White || call.Color == Color.White * 0.5f);
    }

    [Fact]
    public void OverlayOpacity_IsClamped_AndCopied()
    {
        VisualStateFillBrush brush = new(new MGSolidFillBrush(Color.Gray)) { OverlayOpacity = 1.5f };
        Assert.Equal(1f, brush.OverlayOpacity);
        brush.OverlayOpacity = -1f;
        Assert.Equal(0f, brush.OverlayOpacity);
        brush.OverlayOpacity = 0.25f;
        Assert.Equal(0.25f, brush.Copy().OverlayOpacity);
    }

    [Fact]
    public void BackgroundOverlayTransition_FadesTheOverlayIn_OnHover()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Mouse = new Point(390, 290);
        scene.Frames(2);
        UITransition<float> fade = new(UIExtraAnimationTargets.Paths.BackgroundOverlay, TimeSpan.FromMilliseconds(96));
        scene.Top.Transitions.Add(fade);
        Assert.Equal(0f, fade.SettledValue);
        Assert.Equal(1f, scene.Top.BackgroundBrush.OverlayOpacity);

        scene.Mouse = scene.Top.LayoutBounds.Center;
        scene.Frames(1);
        Assert.True(scene.Top.VisualState.IsHovered);
        Assert.True(fade.IsRunning);
        Assert.Equal(0f, scene.Top.BackgroundBrush.OverlayOpacity, Tolerance);
        scene.Frames(3);
        Assert.Equal(0.5f, scene.Top.BackgroundBrush.OverlayOpacity, Tolerance);
        scene.Frames(3);
        Assert.Equal(1f, scene.Top.BackgroundBrush.OverlayOpacity, Tolerance);
        Assert.False(fade.IsRunning);

        scene.Mouse = new Point(390, 290);
        scene.Frames(1);
        Assert.True(fade.IsRunning);
        Assert.Equal(0f, fade.Animation.To, Tolerance);
    }

    [Fact]
    public void PreferredWidth_IsAnimated_AndReLaysOutEveryTick()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        Assert.Equal(120, scene.Top.PreferredWidth);
        UIPropertyAnimation<int?> grow = new(UIExtraAnimationTargets.Paths.PreferredWidth) { To = 220, Duration = TimeSpan.FromMilliseconds(160) };

        scene.Top.Animations.Start(grow);
        scene.Frames(5);
        Assert.Equal(170, scene.Top.PreferredWidth);
        Assert.Equal(170, scene.Top.LayoutBounds.Width);
        Assert.True(scene.Top.IsLayoutValid);

        scene.Desktop.Animations.Update(TimeSpan.FromMilliseconds(16));
        Assert.False(scene.Top.IsLayoutValid);
        scene.Frames(5);
        Assert.Equal(220, scene.Top.PreferredWidth);
        Assert.Equal(UIAnimationState.Completed, grow.State);
    }

    [Fact]
    public void PreferredHeight_Transition_InterpolatesALocalWrite()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Transitions.Add(new UITransition<int?>(UIExtraAnimationTargets.Paths.PreferredHeight, TimeSpan.FromMilliseconds(160)));

        scene.Top.PreferredHeight = 80;

        Assert.Equal(40, scene.Top.PreferredHeight);
        scene.Frames(5);
        Assert.Equal(60, scene.Top.PreferredHeight);
        scene.Frames(6);
        Assert.Equal(80, scene.Top.PreferredHeight);
    }

    [Fact]
    public void Gradient_InterpolatesTheFourCorners()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGGradientFillBrush(Color.Black, Color.Black, Color.Black, Color.Black);
        UIPropertyAnimation<UIGradientColors> shift = new(UIExtraAnimationTargets.Paths.BackgroundGradient)
        {
            To = new UIGradientColors(Color.White, Color.Red, Color.Green, Color.Blue),
            Duration = TimeSpan.FromMilliseconds(160),
            FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
        };

        scene.Top.Animations.Start(shift);
        scene.Frames(5);

        MGGradientFillBrush during = Assert.IsType<MGGradientFillBrush>(scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Lerp(Color.Black, Color.White, 0.5f), during.TopLeftColor);
        Assert.Equal(Color.Lerp(Color.Black, Color.Red, 0.5f), during.TopRightColor);
        Assert.Equal(Color.Lerp(Color.Black, Color.Green, 0.5f), during.BottomRightColor);
        Assert.Equal(Color.Lerp(Color.Black, Color.Blue, 0.5f), during.BottomLeftColor);

        scene.Frames(6);
        Assert.Equal(Color.Black, Assert.IsType<MGGradientFillBrush>(scene.Top.BackgroundBrush.NormalValue).TopRightColor);
    }

    [Fact]
    public void DiagonalGradient_InterpolatesColours_AndSwitchesTheCornerAtTheMidpoint()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGDiagonalGradientFillBrush(Color.Black, Color.White, CornerType.TopLeft);
        scene.Top.Animations.Start(new UIPropertyAnimation<UIDiagonalGradientColors>(UIExtraAnimationTargets.Paths.BackgroundDiagonalGradient)
        {
            To = new UIDiagonalGradientColors(Color.White, Color.Black, CornerType.BottomLeft),
            Duration = TimeSpan.FromMilliseconds(160),
        });

        scene.Frames(4);
        MGDiagonalGradientFillBrush early = Assert.IsType<MGDiagonalGradientFillBrush>(scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(CornerType.TopLeft, early.Color1Position);
        scene.Frames(1);
        MGDiagonalGradientFillBrush mid = Assert.IsType<MGDiagonalGradientFillBrush>(scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(CornerType.BottomLeft, mid.Color1Position);
        Assert.Equal(Color.Lerp(Color.Black, Color.White, 0.5f), mid.Color1);
    }

    [Fact]
    public void Gradient_RefusesAnotherBrushKind()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(
            new UIPropertyAnimation<UIGradientColors>(UIExtraAnimationTargets.Paths.BackgroundGradient) { To = default, Duration = TimeSpan.FromMilliseconds(16) }));

        Assert.Contains(nameof(MGSolidFillBrush), error.Message);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void GradientInterpolators_AreRegistered()
    {
        Assert.True(UIInterpolators.IsRegistered<UIGradientColors>());
        Assert.True(UIInterpolators.IsRegistered<UIDiagonalGradientColors>());
        Assert.Equal(typeof(UIGradientColors), UIAnimationTargets.GetValueType(UIExtraAnimationTargets.Paths.BackgroundGradient));
        Assert.Equal(typeof(float), UIAnimationTargets.GetValueType(UIExtraAnimationTargets.Paths.BackgroundOverlay));
        Assert.Equal(typeof(int?), UIAnimationTargets.GetValueType(UIExtraAnimationTargets.Paths.PreferredHeight));
    }
}
