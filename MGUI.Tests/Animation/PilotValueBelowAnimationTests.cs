using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Animation;

/// <summary>U3 (Docs/Tasks/animation-v3-tasks.md): <see cref="MGElement.TryGetResolvedPilotValueExcluding{T}"/>, the internal read a transition on
/// a store-backed pilot uses (through <see cref="MGUI.Core.UI.Animation.IUIStoreBackedAnimationTarget{T}.TryGetValueBelowAnimation"/>) to retarget
/// to the winner below its own <see cref="UIValueSourceKind.Animation"/> contribution. Covers the Background and DefaultTextForeground dormancy
/// branches (the only two <see cref="MGElement.TryGetResolvedPilotValue{T}"/> honours), and checks the excluding read agrees with the plain one
/// whenever no Animation contribution exists.</summary>
public class PilotValueBelowAnimationTests
{
    [Fact]
    public void Background_ExcludingAnimation_ReturnsTheLocalValueBelowIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(Color.Gray), UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        scene.Top.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(Color.Red), UIValueResolutionSource.Animation(UIInvalidationKind.Draw));

        Assert.True(scene.Top.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> winner));
        Assert.Equal(Color.Red, Assert.IsType<MGSolidFillBrush>(winner.Value).Color);

        Assert.True(scene.Top.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out var below));
        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(below).Color);
    }

    [Fact]
    public void DefaultTextForeground_ExcludingAnimation_ReturnsTheLocalValueBelowIt()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.SetDefaultTextForegroundSlot(UIValueSlot.Normal, Color.Gray, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));
        scene.Top.SetDefaultTextForegroundSlot(UIValueSlot.Normal, Color.Red, UIValueResolutionSource.Animation(UIInvalidationKind.Draw));

        Assert.True(scene.Top.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, out UIResolvedValue<Color?> winner));
        Assert.Equal(Color.Red, winner.Value);

        Assert.True(scene.Top.TryGetResolvedPilotValueExcluding<Color?>(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.Animation, out var below));
        Assert.Equal(Color.Gray, below);
    }

    [Fact]
    public void Background_ExcludingAnimation_ReturnsFalse_WhenTheSubSlotsOnlyContributionIsAnimation()
    {
        // Fix round 1 (U3 regression): a Whole-container swap (Theme) starts a transition without ever recording a
        // non-Animation contribution on the sub-slot -- its only contribution is the Animation entry itself, which
        // ALSO happens to be the container's current physical value (mid-run). Excluding Animation must not fall
        // back to that physical value: per the contract, it must report false ("nothing but Animation is recorded").
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.SetBackground(new VisualStateFillBrush(new MGSolidFillBrush(Color.White)), UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        scene.Top.SetBackgroundSlot(UIValueSlot.Normal, new MGSolidFillBrush(Color.Red), UIValueResolutionSource.Animation(UIInvalidationKind.Draw));

        // Sanity: the physical field really is the animated value (what a naive fallback would wrongly surface).
        Assert.Equal(Color.Red, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);

        Assert.False(scene.Top.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out _));
    }

    [Fact]
    public void Background_Dormancy_AgreesWithThePlainRead_WhenNoAnimationExists()
    {
        // Same scenario as ResolvedBackgroundPilotTests.Dormant_SubSlot_Contribution_Resurfaces_When_Its_Precedence_Becomes_Applicable_Again,
        // asserted against both TryGetResolvedPilotValue and TryGetResolvedPilotValueExcluding: with no Animation contribution ever recorded,
        // the two must agree exactly, dormant sub-slot value included.
        AnimationTestScene scene = AnimationTestScene.Build();
        MGSolidFillBrush transparent = SolidFillBrushes.Transparent;
        scene.Top.SetBackgroundAll(transparent, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));

        VisualStateFillBrush sel = new(new MGSolidFillBrush(Color.Yellow));
        scene.Top.SetBackground(sel, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));

        // Dormant: the Normal sub-slot's own winner (VisualState, yellow container-constructed value) is not applicable
        // (the Whole slot's VisualState winner outranks it), so both reads fall back to the container's physical value.
        Assert.True(scene.Top.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> dormantWinner));
        Assert.True(scene.Top.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out var dormantExcluding));
        Assert.Equal(UIValueSourceKind.VisualState, dormantWinner.Source.Kind);
        Assert.Same(dormantWinner.Value, dormantExcluding);

        scene.Top.ClearPilotSource(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.VisualState);

        // Resurfaced: the Theme sub-slot contribution is applicable again.
        Assert.True(scene.Top.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> resurfaced));
        Assert.True(scene.Top.TryGetResolvedPilotValueExcluding<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Animation, out var resurfacedExcluding));
        Assert.Equal(UIValueSourceKind.Theme, resurfaced.Source.Kind);
        Assert.Same(resurfaced.Value, resurfacedExcluding);
    }

    [Fact]
    public void DefaultTextForeground_Dormancy_AgreesWithThePlainRead_WhenNoAnimationExists()
    {
        // Same scenario as ResolvedTextForegroundPilotTests.Dormant_SubSlot_Contribution_Resurfaces_When_Its_Precedence_Becomes_Applicable_Again.
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.SetDefaultTextForegroundAll(Color.Red, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));

        VisualStateSetting<Color?> sel = new(Color.Yellow, Color.Yellow, Color.Yellow);
        scene.Top.SetDefaultTextForeground(sel, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));

        Assert.True(scene.Top.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, out UIResolvedValue<Color?> dormantWinner));
        Assert.True(scene.Top.TryGetResolvedPilotValueExcluding<Color?>(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.Animation, out var dormantExcluding));
        Assert.Equal(UIValueSourceKind.VisualState, dormantWinner.Source.Kind);
        Assert.Equal(dormantWinner.Value, dormantExcluding);

        scene.Top.ClearPilotSource(UIPilotProperty.DefaultTextForeground, UIValueSlot.Whole, UIValueSourceKind.VisualState);

        Assert.True(scene.Top.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, out UIResolvedValue<Color?> resurfaced));
        Assert.True(scene.Top.TryGetResolvedPilotValueExcluding<Color?>(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.Animation, out var resurfacedExcluding));
        Assert.Equal(UIValueSourceKind.Theme, resurfaced.Source.Kind);
        Assert.Equal(resurfaced.Value, resurfacedExcluding);
    }
}
