using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Interpolation;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Animation;

/// <summary>ADR-0009, W5: the run-owned clone of the five brush-valued animation targets (<see cref="UIColorAnimationTargets.Paths.Background"/>
/// and its Selected/Disabled/Focused variants, <see cref="UIColorAnimationTargets.Paths.BorderBrush"/>,
/// <see cref="UIExtraAnimationTargets.Paths.BackgroundGradient"/>, <see cref="UIExtraAnimationTargets.Paths.BackgroundDiagonalGradient"/>):
/// zero allocation per tick after warm-up, the base brush (frozen or not, shared or not) is never mutated and reappears intact on restore,
/// <see cref="UIAnimationFillBehavior.HoldEnd"/> keeps the clone until the next run or <see cref="UIAnimationCollection.Clear"/>, a
/// <see cref="VisualStateFillBrush.Copy"/> taken mid-run does not capture the clone instance, and a whole-container swap (theme change)
/// mid-run keeps the clone visible on the new container and ends the run on the new container's own base.</summary>
public class BrushAnimationTargetsTests
{
    private static Color Mid(Color from, Color to) => Color.Lerp(from, to, 0.5f);

    private static UIPropertyAnimation<Color> Fade(string path, Color to, int milliseconds, UIAnimationFillBehavior fill = UIAnimationFillBehavior.HoldEnd)
        => new(path) { To = to, Duration = TimeSpan.FromMilliseconds(milliseconds), FillBehavior = fill, Name = "fade" };

    private static void AssertAllocatesNothingPerTick(AnimationTestScene scene, int warmUpTicks = 20, int probeTicks = 200)
    {
        TimeSpan frame = TimeSpan.FromMilliseconds(AnimationTestScene.FrameMilliseconds);
        UIAnimationManager manager = scene.Desktop.Animations;
        for (int i = 0; i < warmUpTicks; i++)
        {
            manager.Update(frame);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < probeTicks; i++)
        {
            manager.Update(frame);
        }
        long after = GC.GetAllocatedBytesForCurrentThread();

        Assert.Equal(0, after - before);
    }

    #region Zero allocation (ACCEPTANCE 2)

    [Fact]
    public void Background_ExplicitAnimation_AllocatesNothingPerTick_AfterWarmUp()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        scene.Top.Animations.Start(Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 100000));

        AssertAllocatesNothingPerTick(scene);
    }

    [Fact]
    public void Background_Transition_AllocatesNothingPerTick_AfterWarmUp()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        scene.Top.Transitions.Add(new UITransition<Color>(UIColorAnimationTargets.Paths.Background, TimeSpan.FromMilliseconds(100000)));
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Blue);

        AssertAllocatesNothingPerTick(scene);
    }

    [Fact]
    public void BackgroundSelected_ExplicitAnimation_AllocatesNothingPerTick_AfterWarmUp()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.SelectedValue = new MGSolidFillBrush(Color.Black);
        scene.Top.Animations.Start(Fade(UIColorAnimationTargets.Paths.BackgroundSelected, Color.White, 100000));

        AssertAllocatesNothingPerTick(scene);
    }

    [Fact]
    public void BorderBrush_ExplicitAnimation_AllocatesNothingPerTick_AfterWarmUp()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BorderBrush = new MGUniformBorderBrush(Color.Red);
        scene.Top.Animations.Start(Fade(UIColorAnimationTargets.Paths.BorderBrush, Color.Green, 100000));

        AssertAllocatesNothingPerTick(scene);
    }

    [Fact]
    public void BackgroundGradient_KeyFrameAnimation_AllocatesNothingPerTick_AfterWarmUp()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGGradientFillBrush(Color.Red, Color.Green, Color.Blue, Color.White);
        scene.Top.Animations.Start(new UIPropertyAnimation<UIGradientColors>(UIExtraAnimationTargets.Paths.BackgroundGradient)
        {
            To = new UIGradientColors(Color.White, Color.Red, Color.Green, Color.Blue),
            // Short enough that every corner's byte value has already changed at least once during the warm-up below (a colour is
            // byte-quantized: at 100000ms, as several other tests in this class use, the first visible step can take dozens of ticks
            // to appear, landing the SetProperty notification that first happens to allocate -- not per tick, but the very first time
            // -- inside the measured window instead of the warm-up).
            Duration = TimeSpan.FromMilliseconds(2000),
        });

        AssertAllocatesNothingPerTick(scene);
    }

    [Fact]
    public void BackgroundDiagonalGradient_ExplicitAnimation_AllocatesNothingPerTick_AfterWarmUp()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGDiagonalGradientFillBrush(Color.Black, Color.White, CornerType.TopLeft);
        scene.Top.Animations.Start(new UIPropertyAnimation<UIDiagonalGradientColors>(UIExtraAnimationTargets.Paths.BackgroundDiagonalGradient)
        {
            To = new UIDiagonalGradientColors(Color.White, Color.Black, CornerType.BottomLeft),
            // Short for the colours (see BackgroundGradient_KeyFrameAnimation_AllocatesNothingPerTick_AfterWarmUp) AND short enough
            // that Color1Position's one discrete flip at the interpolation's mid-point (Docs/animation-architecture.md, "Limites
            // connues") also lands inside the warm-up below rather than the measured window.
            Duration = TimeSpan.FromMilliseconds(400),
        });

        AssertAllocatesNothingPerTick(scene);
    }

    #endregion

    #region Base never mutated (ACCEPTANCE 3)

    [Fact]
    public void ThemeBackground_SharedByTwoButtons_AnimatingOneLeavesTheOtherIntact_AndRestoreReturnsTheBaseInstance()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGSolidFillBrush themeNormal = Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue);
        Assert.Same(themeNormal, scene.Bottom.BackgroundBrush.NormalValue);
        Assert.True(themeNormal.IsFrozen);
        Color themeColor = themeNormal.Color;

        UIPropertyAnimation<Color> fade = Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 100000, UIAnimationFillBehavior.RestoreBaseValue);
        scene.Top.Animations.Start(fade);
        scene.Frames(5);

        // The clone on Top is a different instance; Bottom (and the frozen theme brush itself) never changed.
        Assert.NotSame(themeNormal, scene.Top.BackgroundBrush.NormalValue);
        Assert.Same(themeNormal, scene.Bottom.BackgroundBrush.NormalValue);
        Assert.Equal(themeColor, themeNormal.Color);
        Assert.True(themeNormal.IsFrozen);

        fade.Cancel();

        Assert.Same(themeNormal, scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(themeColor, themeNormal.Color);
    }

    [Fact]
    public void InlineBackgroundBrush_IsNeverMutatedInPlace_ByTheAnimation()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGSolidFillBrush inline = new(Color.Gray);
        scene.Top.BackgroundBrush.NormalValue = inline;

        scene.Top.Animations.Start(Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 100000, UIAnimationFillBehavior.RestoreBaseValue));
        scene.Frames(5);

        // The clone is a different instance; the application's own brush keeps its original colour throughout the run.
        Assert.NotSame(inline, scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Gray, inline.Color);
    }

    #endregion

    #region HoldEnd (ACCEPTANCE, brief item 5)

    [Fact]
    public void HoldEnd_KeepsTheClone_UntilTheNextRunOrClear()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        UIPropertyAnimation<Color> fade = Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 32);
        scene.Top.Animations.Start(fade);
        scene.Frames(3);

        Assert.Equal(UIAnimationState.Completed, fade.State);
        Assert.True(fade.IsHeld);
        MGSolidFillBrush held = Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Blue, held.Color);

        // Ticking further changes nothing: the held clone just sits there (also proves EnumerateResolvedContributions is stable).
        int contributionsWhileHeld = scene.Top.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal).Count;
        scene.Frames(10);
        Assert.Same(held, scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(contributionsWhileHeld, scene.Top.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal).Count);

        scene.Top.Animations.Clear();

        Assert.False(fade.IsHeld);
        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void HoldEnd_ThenANewRunOnTheSamePath_ReplacesTheHeldClone_WithNoLeak()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        UIPropertyAnimation<Color> first = Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 32);
        scene.Top.Animations.Start(first);
        scene.Frames(3);
        Assert.True(first.IsHeld);
        IFillBrush heldClone = scene.Top.BackgroundBrush.NormalValue;

        UIPropertyAnimation<Color> second = Fade(UIColorAnimationTargets.Paths.Background, Color.Red, 100000);
        scene.Top.Animations.Start(second);

        Assert.False(first.IsHeld);
        Assert.NotSame(heldClone, scene.Top.BackgroundBrush.NormalValue);
        Assert.Single(scene.Top.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal), c => c.Source.Kind == UIValueSourceKind.Animation);
    }

    #endregion

    #region Copy mid-run (ACCEPTANCE, brief item 5)

    [Fact]
    public void CopyOfTheContainer_DuringARun_DoesNotCaptureTheClone_ButAnUnfrozenCopyOfTheAnimatedColour()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        scene.Top.Animations.Start(Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 160));
        scene.Frames(2);

        IFillBrush clone = scene.Top.BackgroundBrush.NormalValue;
        VisualStateFillBrush copy = scene.Top.BackgroundBrush.Copy();

        Assert.NotSame(clone, copy.NormalValue);
        Assert.Equal(Assert.IsType<MGSolidFillBrush>(clone).Color, Assert.IsType<MGSolidFillBrush>(copy.NormalValue).Color);
        Assert.False(((MGSolidFillBrush)copy.NormalValue).IsFrozen);

        // The copy is a snapshot: ticking the running animation further does not move it.
        Color snapshotColor = Assert.IsType<MGSolidFillBrush>(copy.NormalValue).Color;
        scene.Frames(5);
        Assert.Equal(snapshotColor, Assert.IsType<MGSolidFillBrush>(copy.NormalValue).Color);
        Assert.NotEqual(snapshotColor, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
    }

    #endregion

    #region Container swap mid-run (ACCEPTANCE 5)

    [Fact]
    public void ThemeChangeMidRun_KeepsTheAnimationVisible_AndEndsOnTheNewContainersOwnBase()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIPropertyAnimation<Color> fade = Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 100000, UIAnimationFillBehavior.RestoreBaseValue);
        scene.Top.Animations.Start(fade);
        scene.Frames(3);
        Color midColour = Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color;

        VisualStateFillBrush newContainer = new(new MGSolidFillBrush(Color.Green));
        IFillBrush originalGreenInstance = newContainer.NormalValue;
        scene.Top.SetBackground(newContainer, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));

        // The clone is re-applied onto the new container: the animation is still visibly progressing from where it was.
        Assert.Same(scene.Top.BackgroundBrush, newContainer);
        Assert.Equal(midColour, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
        Assert.True(scene.Top.Animations.IsAnimating(UIColorAnimationTargets.Paths.Background));

        int contributionsAfterSwap = scene.Top.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal).Count;
        for (int i = 0; i < 100; i++)
        {
            scene.Frames(1);
        }
        Assert.Equal(contributionsAfterSwap, scene.Top.EnumerateResolvedContributions(UIPilotProperty.Background, UIValueSlot.Normal).Count);

        fade.Cancel();

        // Ends on the NEW container's own base (Green), not the colour the run started on, and it is that exact instance.
        Assert.Same(originalGreenInstance, scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Green, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
    }

    #endregion
}
