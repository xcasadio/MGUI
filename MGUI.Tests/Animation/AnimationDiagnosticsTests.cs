using MGUI.Core.Tooling;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Targets;

namespace MGUI.Tests.Animation;

/// <summary>Slice S8: the element debug view lists the element's animations, held contributions and transitions.</summary>
public class AnimationDiagnosticsTests
{
    [Fact]
    public void DebugView_IsEmpty_ForAnElementThatNeverAnimated()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        UIElementDebugView view = UIToolingService.CaptureElementDebugView(scene.Top);

        Assert.Empty(view.Animations);
        Assert.DoesNotContain("animations:", UIToolingService.RenderElementDebugView(view));
    }

    [Fact]
    public void DebugView_ListsActiveHeldAndTransitions()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Transitions.Add(new UITransition<float>(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160)));
        scene.Top.Animations.Start(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.RenderScale) { To = 1.2f, Duration = TimeSpan.FromMilliseconds(32), Name = "grow" });
        scene.Frames(3);
        scene.Top.Animations.Start(new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.RenderTransformRotation) { To = 90f, Duration = TimeSpan.FromMilliseconds(160), Name = "spin" });
        scene.Frames(5);

        UIElementDebugView view = UIToolingService.CaptureElementDebugView(scene.Top);
        string text = UIToolingService.RenderElementDebugView(view);

        Assert.Contains(view.Animations, x => x.Kind == "animation" && x.Path == UIBuiltInAnimationTargets.Paths.RenderTransformRotation && x.State == "Running" && x.Name == "spin" && x.Progress == 0.5f);
        Assert.Contains(view.Animations, x => x.Kind == "held" && x.Path == UIBuiltInAnimationTargets.Paths.RenderScale && x.State == "Completed" && x.Name == "grow");
        Assert.Contains(view.Animations, x => x.Kind == "transition" && x.Path == UIBuiltInAnimationTargets.Paths.Opacity && x.State == "idle" && x.Progress == null);
        Assert.Contains("animations:", text);
        Assert.Contains("animation RenderTransform.Rotation: Running 0.50 'spin'", text);
        Assert.Contains("held RenderScale: Completed", text);
        Assert.Contains("transition Opacity: idle", text);
    }

    [Fact]
    public void DebugView_ShowsARunningTransition()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.Transitions.Add(new UITransition<float>(UIBuiltInAnimationTargets.Paths.Opacity, TimeSpan.FromMilliseconds(160)));
        scene.Top.Opacity = 0f;
        scene.Frames(5);

        UIElementDebugView view = UIToolingService.CaptureElementDebugView(scene.Top);

        UIAnimationDebugView transition = Assert.Single(view.Animations, x => x.Kind == "transition");
        Assert.Equal("running", transition.State);
        Assert.Equal(0.5f, transition.Progress);
        Assert.Contains(view.Animations, x => x.Kind == "animation" && x.Name == "transition:Opacity");
    }
}
