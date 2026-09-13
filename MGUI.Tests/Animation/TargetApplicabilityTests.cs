using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.BorderBrushes;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Animation;

/// <summary>Slice U2 of Docs/Tasks/animation-v3-tasks.md (ADR-0008, decision 2): the owner type an animation target requires, exposed by
/// <see cref="IUIAnimationTarget{T}.RequiredOwnerType"/> and <see cref="UIAnimationTargets.GetOwnerType"/>, and the applicability filter
/// <see cref="UIAnimationTargets.IsApplicable"/> used by <see cref="UIToolingService.CaptureElementDebugView"/>.</summary>
public class TargetApplicabilityTests
{
    /// <summary>Every registered path must appear here, mapped to the element type its target's own runtime cast enforces, or null when it
    /// accepts any element. <see cref="EveryRegisteredPath_IsCoveredByThisTable"/> asserts the two sets are equal so a future target must
    /// declare its expectation.<para/>
    /// <c>BorderBrush</c> is null: <c>BorderBrushTarget</c> resolves its border through <see cref="MGElement.GetBorder"/> (a facade many
    /// element types override, not a strict cast to <see cref="MGBorder"/>), so any element that owns a border accepts the path.</summary>
    private static readonly IReadOnlyDictionary<string, Type> Expected = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
    {
        [UIBuiltInAnimationTargets.Paths.Opacity] = null,
        [UIBuiltInAnimationTargets.Paths.RenderTransformTranslation] = null,
        [UIBuiltInAnimationTargets.Paths.RenderTransformScale] = null,
        [UIBuiltInAnimationTargets.Paths.RenderTransformRotation] = null,
        [UIBuiltInAnimationTargets.Paths.RenderTransformOrigin] = null,
        [UIBuiltInAnimationTargets.Paths.RenderScale] = null,
        [UIBuiltInAnimationTargets.Paths.Margin] = null,
        [UIBuiltInAnimationTargets.Paths.Padding] = null,
        [UIBuiltInAnimationTargets.Paths.MinHeight] = null,
        [UIBuiltInAnimationTargets.Paths.ProgressButtonValue] = typeof(MGProgressButton),
        [UIBuiltInAnimationTargets.Paths.TextBlockTextProgress] = typeof(MGTextBlock),
        [UIColorAnimationTargets.Paths.Background] = null,
        [UIColorAnimationTargets.Paths.BackgroundSelected] = null,
        [UIColorAnimationTargets.Paths.BackgroundDisabled] = null,
        [UIColorAnimationTargets.Paths.BackgroundFocused] = null,
        [UIColorAnimationTargets.Paths.Foreground] = typeof(MGTextBlock),
        [UIColorAnimationTargets.Paths.TextForeground] = null,
        [UIColorAnimationTargets.Paths.BorderBrush] = null,
        [UIExtraAnimationTargets.Paths.BackgroundOverlay] = null,
        [UIExtraAnimationTargets.Paths.PreferredWidth] = null,
        [UIExtraAnimationTargets.Paths.PreferredHeight] = null,
        [UIExtraAnimationTargets.Paths.BackgroundGradient] = null,
        [UIExtraAnimationTargets.Paths.BackgroundDiagonalGradient] = null,
    };

    /// <summary>Other test classes (<c>AnimationManagerTests</c>, <c>TransitionTests</c>) register throwaway targets under a <c>Test.</c>
    /// prefix directly into the shared static registry, which never unregisters (this test suite's own convention); the process-wide
    /// registry can therefore hold them by the time this class runs. Framework paths are the closed set this slice covers.</summary>
    private static bool IsFrameworkPath(string path) => !path.StartsWith("Test.", StringComparison.OrdinalIgnoreCase);

    public static IEnumerable<object[]> FrameworkPaths()
        => UIAnimationTargets.Paths.Where(IsFrameworkPath).Select(path => new object[] { path });

    [Theory]
    [MemberData(nameof(FrameworkPaths))]
    public void EachRegisteredPath_ReportsTheExpectedOwnerType(string path)
    {
        Assert.True(Expected.ContainsKey(path), $"'{path}' is registered but has no entry in the expectation table.");
        Assert.Equal(Expected[path], UIAnimationTargets.GetOwnerType(path));
    }

    [Fact]
    public void EveryRegisteredPath_IsCoveredByThisTable()
    {
        var registered = new HashSet<string>(UIAnimationTargets.Paths.Where(IsFrameworkPath), StringComparer.OrdinalIgnoreCase);
        var expected = new HashSet<string>(Expected.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.True(registered.SetEquals(expected),
            $"Mismatch. Registered only: [{string.Join(", ", registered.Except(expected))}]. Table only: [{string.Join(", ", expected.Except(registered))}].");
    }

    [Fact]
    public void AnUnknownPath_ReturnsNull_AndIsNeverApplicable_WithoutThrowing()
    {
        Assert.Null(UIAnimationTargets.GetOwnerType("Does.Not.Exist"));

        AnimationTestScene scene = AnimationTestScene.Build();
        Assert.False(UIAnimationTargets.IsApplicable("Does.Not.Exist", scene.Top));
    }

    [Fact]
    public void ANullOrEmptyPath_BehavesLikeAnUnknownOne_LikeGetValueType()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        Assert.Null(UIAnimationTargets.GetValueType(null));
        Assert.Null(UIAnimationTargets.GetOwnerType(null));
        Assert.False(UIAnimationTargets.IsApplicable(null, scene.Top));

        Assert.Null(UIAnimationTargets.GetValueType(string.Empty));
        Assert.Null(UIAnimationTargets.GetOwnerType(string.Empty));
        Assert.False(UIAnimationTargets.IsApplicable(string.Empty, scene.Top));
    }

    [Fact]
    public void GetOwnerType_IsCaseInsensitive_LikeGetValueType()
    {
        Assert.Equal(typeof(MGTextBlock), UIAnimationTargets.GetOwnerType("foreground"));
        Assert.Equal(typeof(MGTextBlock), UIAnimationTargets.GetOwnerType("FOREGROUND"));
        Assert.Equal(typeof(MGProgressButton), UIAnimationTargets.GetOwnerType("progressbutton.value"));
    }

    [Fact]
    public void OnAButton_TheFilter_ExcludesTextBlockAndProgressButtonOnlyTargets()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        var applicable = UIAnimationTargets.Paths.Where(path => UIAnimationTargets.IsApplicable(path, scene.Top)).ToArray();

        Assert.DoesNotContain(UIColorAnimationTargets.Paths.Foreground, applicable);
        Assert.DoesNotContain(UIBuiltInAnimationTargets.Paths.ProgressButtonValue, applicable);
        Assert.Contains(UIBuiltInAnimationTargets.Paths.Opacity, applicable);
        Assert.Contains(UIColorAnimationTargets.Paths.Background, applicable);
        Assert.Contains(UIBuiltInAnimationTargets.Paths.Margin, applicable);
    }

    [Fact]
    public void OnATextBlock_TheFilter_IncludesForeground()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGTextBlock textBlock = new(scene.Window, "Hello");
        scene.Panel.TryAddChild(textBlock);

        var applicable = UIAnimationTargets.Paths.Where(path => UIAnimationTargets.IsApplicable(path, textBlock)).ToArray();
        Assert.Contains(UIColorAnimationTargets.Paths.Foreground, applicable);
        Assert.DoesNotContain(UIBuiltInAnimationTargets.Paths.ProgressButtonValue, applicable);
    }

    [Fact]
    public void OnAProgressButton_TheFilter_IncludesProgressButtonValue()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGProgressButton button = new(scene.Window, new Thickness(1), MGUniformBorderBrush.Black);
        scene.Panel.TryAddChild(button);

        var applicable = UIAnimationTargets.Paths.Where(path => UIAnimationTargets.IsApplicable(path, button)).ToArray();
        Assert.Contains(UIBuiltInAnimationTargets.Paths.ProgressButtonValue, applicable);
        Assert.DoesNotContain(UIColorAnimationTargets.Paths.Foreground, applicable);
    }

    [Fact]
    public void CaptureElementDebugView_OfAButton_ListsApplicablePathsSorted_WithoutTheConstrainedOnes()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        UIElementDebugView view = UIToolingService.CaptureElementDebugView(scene.Top);

        Assert.DoesNotContain(UIColorAnimationTargets.Paths.Foreground, view.ApplicablePaths);
        Assert.DoesNotContain(UIBuiltInAnimationTargets.Paths.ProgressButtonValue, view.ApplicablePaths);
        Assert.Contains(UIBuiltInAnimationTargets.Paths.Opacity, view.ApplicablePaths);

        var sorted = view.ApplicablePaths.OrderBy(x => x, StringComparer.Ordinal).ToArray();
        Assert.Equal(sorted, view.ApplicablePaths);
    }

    [Fact]
    public void CaptureElementDebugView_OfATextBlock_ListsForeground()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGTextBlock textBlock = new(scene.Window, "Hello");
        scene.Panel.TryAddChild(textBlock);

        UIElementDebugView view = UIToolingService.CaptureElementDebugView(textBlock);
        Assert.Contains(UIColorAnimationTargets.Paths.Foreground, view.ApplicablePaths);
    }

    [Fact]
    public void Foreground_RefusesANonTextBlock_AndAcceptsATextBlock()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        IUIAnimationTarget<Color> target = UIAnimationTargets.Resolve<Color>(UIColorAnimationTargets.Paths.Foreground);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => target.GetValue(scene.Top));
        Assert.Contains(nameof(MGTextBlock), error.Message);

        MGTextBlock textBlock = new(scene.Window, "Hello");
        scene.Panel.TryAddChild(textBlock);
        // No exception: the right owner type is accepted.
        target.GetValue(textBlock);
    }

    [Fact]
    public void ProgressButtonValue_RefusesANonProgressButton_AndAcceptsOne()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        IUIAnimationTarget<float> target = UIAnimationTargets.Resolve<float>(UIBuiltInAnimationTargets.Paths.ProgressButtonValue);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => target.GetValue(scene.Top));
        Assert.Contains(nameof(MGProgressButton), error.Message);

        MGProgressButton button = new(scene.Window, new Thickness(1), MGUniformBorderBrush.Black);
        scene.Panel.TryAddChild(button);
        // No exception: the right owner type is accepted.
        target.GetValue(button);
    }
}
