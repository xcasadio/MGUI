using MGUI.Core.Tooling;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Styling;
using Microsoft.Xna.Framework;

namespace MGUI.Tests.Animation;

/// <summary>Slice S5: the solid-colour targets over the background slots, the text foregrounds and the uniform border.</summary>
public class ColorTargetsTests
{
    private static Color Mid(Color from, Color to) => Color.Lerp(from, to, 0.5f);

    private static UIPropertyAnimation<Color> Fade(string path, Color to, int milliseconds, UIAnimationFillBehavior fill = UIAnimationFillBehavior.HoldEnd)
        => new(path) { To = to, Duration = TimeSpan.FromMilliseconds(milliseconds), FillBehavior = fill, Name = "fade" };

    [Fact]
    public void ColourPaths_AreRegistered_AsColorTargets()
    {
        foreach (string path in new[]
        {
            UIColorAnimationTargets.Paths.Background, UIColorAnimationTargets.Paths.BackgroundSelected, UIColorAnimationTargets.Paths.BackgroundDisabled,
            UIColorAnimationTargets.Paths.BackgroundFocused, UIColorAnimationTargets.Paths.Foreground, UIColorAnimationTargets.Paths.TextForeground,
            UIColorAnimationTargets.Paths.BorderBrush,
        })
        {
            Assert.Equal(typeof(Color), UIAnimationTargets.GetValueType(path));
        }
    }

    [Fact]
    public void Background_InterpolatesTheNormalSlot_UnderTheAnimationSource_AndRestoresOnCancel()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        UIPropertyAnimation<Color> fade = Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 160);

        scene.Top.Animations.Start(fade);
        Assert.Equal(Color.Gray, fade.StartValue);
        scene.Frames(5);

        MGSolidFillBrush during = Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(Mid(Color.Gray, Color.Blue), during.Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Top, "Background", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.Animation, source.Kind);
        Assert.Equal("fade", source.Name);

        fade.Cancel();

        MGSolidFillBrush restored = Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Gray, restored.Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Top, "Background", out UIValueResolutionSource after));
        Assert.Equal(UIValueSourceKind.LocalValue, after.Kind);
    }

    [Fact]
    public void Background_HoldEnd_IsVisibleInTheDebugView_UntilClear()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        UIPropertyAnimation<Color> fade = Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 32);
        scene.Top.Animations.Start(fade);
        scene.Frames(3);
        Assert.Equal(UIAnimationState.Completed, fade.State);
        Assert.True(fade.IsHeld);

        string view = UIToolingService.RenderElementDebugView(UIToolingService.CaptureElementDebugView(scene.Top));
        Assert.Contains("Animation", view);
        Assert.Equal(Color.Blue, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);

        scene.Top.Animations.Clear();

        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
        Assert.False(fade.IsHeld);
    }

    [Fact]
    public void BackgroundSelectedSlot_IsAnimatedIndependently()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        scene.Top.BackgroundBrush.SelectedValue = new MGSolidFillBrush(Color.Black);
        scene.Top.Animations.Start(Fade(UIColorAnimationTargets.Paths.BackgroundSelected, Color.White, 160));

        scene.Frames(5);

        Assert.Equal(Mid(Color.Black, Color.White), Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.SelectedValue).Color);
        Assert.Equal(Color.Gray, Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void TextBlockForeground_IsAnimated()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGTextBlock text = new(scene.Window, "Hello", Color.White);
        scene.Panel.TryAddChild(text);
        scene.Frames(2);
        UIPropertyAnimation<Color> fade = Fade(UIColorAnimationTargets.Paths.Foreground, Color.Black, 160, UIAnimationFillBehavior.RestoreBaseValue);

        text.Animations.Start(fade);
        Assert.Equal(Color.White, fade.StartValue);
        scene.Frames(5);

        Assert.Equal(Mid(Color.White, Color.Black), text.Foreground.NormalValue);
        Assert.Equal(Mid(Color.White, Color.Black), text.ActualForeground);
        Assert.True(UIToolingService.TryGetResolvedValueSource(text, "Foreground", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.Animation, source.Kind);

        scene.Frames(5);
        Assert.Equal(Color.White, text.Foreground.NormalValue);
    }

    [Fact]
    public void Foreground_OnANonTextElement_FailsExplicitly()
    {
        AnimationTestScene scene = AnimationTestScene.Build();

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => scene.Top.Animations.Start(Fade(UIColorAnimationTargets.Paths.Foreground, Color.Black, 16)));

        Assert.Contains("MGTextBlock", error.Message);
        Assert.Contains(UIColorAnimationTargets.Paths.TextForeground, error.Message);
    }

    [Fact]
    public void TextForeground_AnimatesTheInheritedTextColour()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Panel.DefaultTextForeground = new VisualStateSetting<Color?>(Color.White);
        MGTextBlock text = new(scene.Window, "Hello");
        scene.Panel.TryAddChild(text);
        scene.Frames(2);
        Assert.Equal(Color.White, text.ActualForeground);

        scene.Panel.Animations.Start(Fade(UIColorAnimationTargets.Paths.TextForeground, Color.Black, 160));
        scene.Frames(5);

        Assert.Equal(Mid(Color.White, Color.Black), scene.Panel.DefaultTextForeground.NormalValue);
        Assert.Equal(Mid(Color.White, Color.Black), text.ActualForeground);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Panel, "TextForeground", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.Animation, source.Kind);
    }

    [Fact]
    public void BorderBrush_OnABorder_AndThroughAButtonFacade()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        MGBorder border = new(scene.Window, new MonoGame.Extended.Thickness(1), new MGSolidFillBrush(Color.Red));
        scene.Panel.TryAddChild(border);
        scene.Top.BorderBrush = new MGUniformBorderBrush(Color.Red);
        scene.Frames(2);

        border.Animations.Start(Fade(UIColorAnimationTargets.Paths.BorderBrush, Color.Green, 160, UIAnimationFillBehavior.RestoreBaseValue));
        scene.Top.Animations.Start(Fade(UIColorAnimationTargets.Paths.BorderBrush, Color.Green, 160, UIAnimationFillBehavior.RestoreBaseValue));
        scene.Frames(5);

        foreach (MGElement element in new MGElement[] { border, scene.Top })
        {
            MGUniformBorderBrush uniform = Assert.IsType<MGUniformBorderBrush>(element.GetBorder().BorderBrush);
            Assert.Equal(Mid(Color.Red, Color.Green), Assert.IsType<MGSolidFillBrush>(uniform.Brush).Color);
            Assert.True(UIToolingService.TryGetResolvedValueSource(element, "BorderBrush", out UIValueResolutionSource source));
            Assert.Equal(UIValueSourceKind.Animation, source.Kind);
        }

        scene.Frames(5);
        Assert.Equal(Color.Red, Assert.IsType<MGSolidFillBrush>(Assert.IsType<MGUniformBorderBrush>(scene.Top.GetBorder().BorderBrush).Brush).Color);
    }

    [Fact]
    public void GradientBackground_CannotBeAnimated()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Bottom.BackgroundBrush.NormalValue = new MGGradientFillBrush(Color.Red, Color.Green, Color.Blue, Color.White);

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => scene.Bottom.Animations.Start(Fade(UIColorAnimationTargets.Paths.Background, Color.Black, 16)));

        Assert.Contains(nameof(MGGradientFillBrush), error.Message);
        Assert.Equal(0, scene.Desktop.Animations.ActiveCount);
    }

    [Fact]
    public void ThemeChange_DuringABackgroundAnimation_KeepsTheAnimationContribution()
    {
        AnimationTestScene scene = AnimationTestScene.Build();
        scene.Top.BackgroundBrush.NormalValue = new MGSolidFillBrush(Color.Gray);
        scene.Top.Animations.Start(Fade(UIColorAnimationTargets.Paths.Background, Color.Blue, 160));
        scene.Frames(3);

        scene.Window.Theme = scene.Desktop.Resources.GetThemeOrDefault(MGTheme.BuiltInTheme.Light_Gray.ToString());
        scene.Frames(2);

        Assert.Equal(Mid(Color.Gray, Color.Blue), Assert.IsType<MGSolidFillBrush>(scene.Top.BackgroundBrush.NormalValue).Color);
        Assert.True(UIToolingService.TryGetResolvedValueSource(scene.Top, "Background", out UIValueResolutionSource source));
        Assert.Equal(UIValueSourceKind.Animation, source.Kind);
    }
}
