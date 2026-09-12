using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Styling;
using MonoGame.Extended;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Animation;

/// <summary>Slice S4: the built-in targets for Opacity, the RenderTransform components, the state-driven scale and the layout pilots.</summary>
public class PropertyTargetsTests
{
    private const float Tolerance = 1e-4f;

    [Fact]
    public void BuiltInPaths_AreRegistered()
    {
        foreach (string path in new[]
        {
            UIBuiltInAnimationTargets.Paths.Opacity,
            UIBuiltInAnimationTargets.Paths.RenderTransformTranslation,
            UIBuiltInAnimationTargets.Paths.RenderTransformScale,
            UIBuiltInAnimationTargets.Paths.RenderTransformRotation,
            UIBuiltInAnimationTargets.Paths.RenderTransformOrigin,
            UIBuiltInAnimationTargets.Paths.RenderScale,
            UIBuiltInAnimationTargets.Paths.Margin,
            UIBuiltInAnimationTargets.Paths.Padding,
            UIBuiltInAnimationTargets.Paths.MinHeight,
        })
        {
            Assert.True(UIAnimationTargets.IsRegistered(path), path);
        }

        Assert.Equal(typeof(float), UIAnimationTargets.GetValueType("opacity"));
        Assert.Equal(typeof(Vector2), UIAnimationTargets.GetValueType("RenderTransform.Scale"));
        Assert.Equal(typeof(Thickness), UIAnimationTargets.GetValueType("Margin"));
        Assert.Equal(typeof(int?), UIAnimationTargets.GetValueType("MinHeight"));
    }

    [Fact]
    public void Opacity_FadesIn_WithCubicOut()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = new(UIBuiltInAnimationTargets.Paths.Opacity)
        {
            From = 0f,
            To = 1f,
            Duration = TimeSpan.FromMilliseconds(300),
            Easing = UIEasing.CubicOut,
        };

        scene.Top.Animations.Start(fade);
        Assert.Equal(0f, scene.Top.Opacity, Tolerance);

        scene.Frames(3, 50);
        Assert.Equal(UIEasing.CubicOut.Ease(0.5f), scene.Top.Opacity, Tolerance);

        scene.Frames(3, 50);
        Assert.Equal(1f, scene.Top.Opacity, Tolerance);
        Assert.Equal(UIAnimationState.Completed, fade.State);
    }

    [Fact]
    public void RenderTransform_TranslationAndScale_ReachTheDraw()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Animations.Start(new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformTranslation)
        {
            From = Vector2.Zero,
            To = new Vector2(100f, 0f),
            Duration = TimeSpan.FromMilliseconds(160),
        });
        scene.Top.Animations.Start(new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale)
        {
            To = new Vector2(3f, 3f),
            Duration = TimeSpan.FromMilliseconds(160),
        });
        scene.Frames(5);

        Assert.Equal(new Vector2(50f, 0f), scene.Top.RenderTransform.Translation);
        Assert.Equal(new Vector2(2f, 2f), scene.Top.RenderTransform.Scale);
        GraphNoOpDrawTransaction transaction = scene.Draw();
        Matrix push = Assert.Single(transaction.TransformPushes);
        Matrix expected = scene.Top.RenderTransform.ToMatrix(scene.Top.LayoutBounds);
        Assert.Equal(expected.M41, push.M41, Tolerance);
        Assert.Equal(expected.M11, push.M11, Tolerance);
    }

    [Fact]
    public void RenderTransform_RotationAndOrigin_AreAnimatable()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Animations.Start(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.RenderTransformRotation) { To = 90f, Duration = TimeSpan.FromMilliseconds(160) });
        scene.Top.Animations.Start(new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformOrigin) { To = new Vector2(1f, 1f), Duration = TimeSpan.FromMilliseconds(160) });

        scene.Frames(5);

        Assert.Equal(45f, scene.Top.RenderTransform.Rotation, Tolerance);
        Assert.Equal(new Vector2(0.5f, 0.5f), scene.Top.RenderTransform.Origin);
    }

    [Fact]
    public void RenderTransform_RestoreBaseValue_PutsTheComponentBack()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.RenderTransform.Rotation = 10f;
        UIPropertyAnimation<float> spin = new(UIBuiltInAnimationTargets.Paths.RenderTransformRotation)
        {
            To = 370f,
            Duration = TimeSpan.FromMilliseconds(160),
            FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
        };
        scene.Top.Animations.Start(spin);
        scene.Frames(5);
        Assert.Equal(190f, scene.Top.RenderTransform.Rotation, Tolerance);

        scene.Frames(5);

        Assert.Equal(10f, scene.Top.RenderTransform.Rotation, Tolerance);
    }

    [Fact]
    public void RenderScale_OverrideIsConsumedByTheDraw_AndReleasedOnRestore()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.RenderScale = new ConditionalScaleTransform(0.96f, 1.05f);
        scene.Mouse = new Point(390, 290);
        scene.Frames(2);
        Assert.False(scene.Top.VisualState.IsHovered);
        UIPropertyAnimation<float> pulse = new(UIBuiltInAnimationTargets.Paths.RenderScale)
        {
            To = 2f,
            Duration = TimeSpan.FromMilliseconds(160),
            FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
        };

        scene.Top.Animations.Start(pulse);
        Assert.Equal(1f, pulse.StartValue, Tolerance);
        scene.Frames(5);
        Assert.True(scene.Top.TryGetEffectiveStateScale(out float scale));
        Assert.Equal(1.5f, scale, Tolerance);
        Assert.Equal(2, scene.Desktop.ActiveRenderTransformCount);

        GraphNoOpDrawTransaction transaction = scene.Draw();
        Matrix push = Assert.Single(transaction.TransformPushes);
        Assert.Equal(1.5f, push.M11, Tolerance);

        scene.Frames(5);
        Assert.Equal(UIAnimationState.Completed, pulse.State);
        Assert.False(scene.Top.TryGetEffectiveStateScale(out _));
        Assert.Equal(1, scene.Desktop.ActiveRenderTransformCount);
    }

    [Fact]
    public void RenderScale_HoldEnd_KeepsTheOverride_UntilClear()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> grow = new(UIBuiltInAnimationTargets.Paths.RenderScale) { To = 1.5f, Duration = TimeSpan.FromMilliseconds(32) };
        scene.Top.Animations.Start(grow);
        scene.Frames(3);

        Assert.Equal(UIAnimationState.Completed, grow.State);
        Assert.True(grow.IsHeld);
        Assert.Single(scene.Top.Animations.Held);
        Assert.True(scene.Top.TryGetEffectiveStateScale(out float held));
        Assert.Equal(1.5f, held, Tolerance);

        scene.Top.Animations.Clear();

        Assert.False(grow.IsHeld);
        Assert.False(scene.Top.TryGetEffectiveStateScale(out _));
        Assert.Equal(0, scene.Desktop.ActiveRenderTransformCount);
    }

    [Fact]
    public void Margin_IsWrittenWithTheAnimationSource_AndFallsBackOnRestore()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Margin = new Thickness(2);
        UIPropertyAnimation<Thickness> slide = new(UIBuiltInAnimationTargets.Paths.Margin)
        {
            To = new Thickness(22),
            Duration = TimeSpan.FromMilliseconds(160),
            FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
            Name = "slide",
        };

        scene.Top.Animations.Start(slide);
        scene.Frames(5);

        Assert.Equal(12, scene.Top.Margin.Left);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Top, "Margin", out UIValueResolutionSource during));
        Assert.Equal(UIValueSourceKind.Animation, during.Kind);
        Assert.Equal("slide", during.Name);

        scene.Frames(5);

        Assert.Equal(UIAnimationState.Completed, slide.State);
        Assert.Equal(2, scene.Top.Margin.Left);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Top, "Margin", out UIValueResolutionSource after));
        Assert.Equal(UIValueSourceKind.LocalValue, after.Kind);
    }

    [Fact]
    public void Margin_HoldEnd_KeepsTheAnimationSource_AndShadowsALocalWrite_UntilClear()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Margin = new Thickness(2);
        UIPropertyAnimation<Thickness> slide = new(UIBuiltInAnimationTargets.Paths.Margin) { To = new Thickness(22), Duration = TimeSpan.FromMilliseconds(32) };
        scene.Top.Animations.Start(slide);
        scene.Frames(3);
        Assert.Equal(UIAnimationState.Completed, slide.State);
        Assert.True(slide.IsHeld);

        scene.Top.Margin = new Thickness(7);

        Assert.Equal(22, scene.Top.Margin.Left);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Top, "Margin", out UIValueResolutionSource held));
        Assert.Equal(UIValueSourceKind.Animation, held.Kind);

        scene.Top.Animations.Clear();

        Assert.Equal(7, scene.Top.Margin.Left);
        Assert.False(slide.IsHeld);
    }

    [Fact]
    public void Pilot_LocalWriteDuringTheAnimation_IsShadowed_ThenWinsAfterRestore()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Padding = new Thickness(4);
        UIPropertyAnimation<Thickness> animation = new(UIBuiltInAnimationTargets.Paths.Padding)
        {
            To = new Thickness(24),
            Duration = TimeSpan.FromMilliseconds(160),
            FillBehavior = UIAnimationFillBehavior.RestoreBaseValue,
        };
        scene.Top.Animations.Start(animation);
        scene.Frames(5);

        scene.Top.Padding = new Thickness(9);
        Assert.Equal(14, scene.Top.Padding.Left);

        scene.Frames(5);
        Assert.Equal(9, scene.Top.Padding.Left);
    }

    [Fact]
    public void NonPilot_LocalWriteDuringTheAnimation_IsOverwrittenByTheNextTick()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> fade = new(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(160) };
        scene.Top.Animations.Start(fade);
        scene.Frames(5);

        scene.Top.Opacity = 0.9f;
        scene.Frames(1);

        Assert.Equal(0.6f, scene.Top.Opacity, Tolerance);
    }

    [Fact]
    public void MinHeight_AnimatesThroughTheStore()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.MinHeight = 10;
        scene.Top.Animations.Start(new UIPropertyAnimation<int?>(UIBuiltInAnimationTargets.Paths.MinHeight) { To = 30, Duration = TimeSpan.FromMilliseconds(160) });

        scene.Frames(5);

        Assert.Equal(20, scene.Top.MinHeight);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Top, "MinHeight", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.Animation, source.Kind);
    }

    [Fact]
    public void LayoutPilot_InvalidatesTheLayoutEveryTick_RenderOnlyTargetsNever()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        Assert.True(scene.Top.IsLayoutValid);
        scene.Top.Animations.Start(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(160) });
        scene.Top.Animations.Start(new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformTranslation) { To = new Vector2(50f, 0f), Duration = TimeSpan.FromMilliseconds(160) });

        scene.Desktop.Animations.Update(TimeSpan.FromMilliseconds(16));
        Assert.True(scene.Top.IsLayoutValid);

        scene.Top.Animations.Start(new UIPropertyAnimation<Thickness>(UIBuiltInAnimationTargets.Paths.Margin) { To = new Thickness(20), Duration = TimeSpan.FromMilliseconds(160) });
        scene.Frames(1);
        Assert.True(scene.Top.IsLayoutValid);
        scene.Desktop.Animations.Update(TimeSpan.FromMilliseconds(16));
        Assert.False(scene.Top.IsLayoutValid);
    }

    [Fact]
    public void TypeMismatch_OnABuiltInPath_IsExplicit()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<float> wrong = new(UIBuiltInAnimationTargets.Paths.Margin) { To = 1f, Duration = TimeSpan.FromMilliseconds(16) };

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(wrong));

        Assert.Contains("Thickness", error.Message);
        Assert.Contains("Single", error.Message);
    }
}
