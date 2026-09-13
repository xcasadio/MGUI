using System.Collections.Generic;
using System.IO;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Animation;

/// <summary>Scenarios SCN-ANIM-001, SCN-ANIM-002 and SCN-ANIM-003: the demo XAML (MGUI.Samples/Features/AnimationDemo.xaml) loads through the strict
/// loader and its declared transitions, transform, style, visual states and engine-driven progress button are in place. The code-behind
/// (<c>AnimationDemoSample</c>, which wires every button's click handler in <c>WireV3</c>) is compiled by the samples build but cannot be
/// constructed from here: MGUI.Tests.csproj carries no <c>ProjectReference</c> to MGUI.Samples.csproj (checked directly in the file), so its
/// buttons' click handlers are exercised by hand in the sample, exactly as the V1/V2 tests above already note. The V3 tests below instead drive,
/// directly on the real elements this file loads from the same XAML, the exact engine calls each handler makes (<see cref="UIKeyFrameClipSerializer"/>,
/// <see cref="UIAnimationPreview"/>, <see cref="UIAnimationSerializer"/>, a real mouse hover, a direct <c>Desktop.Animations.Clock</c> write) --
/// as close an approximation of "click the button" as is possible without that reference.</summary>
public class AnimationDemoSampleTests
{
    /// <summary>Byte-for-byte the same JSON as <c>MGUI.Samples.Features.AnimationDemoV3Assets.ClipJson</c> (U6, ADR-0008 decision 6): duplicated
    /// here, rather than referenced, because MGUI.Tests has no <c>ProjectReference</c> to MGUI.Samples (see the class summary above) -- the
    /// PERIMETER of this slice does not include MGUI.Tests.csproj, so adding one was not an option. Three tracks (<c>Opacity</c>,
    /// <c>RenderTransform.Scale</c>, <c>Background</c>) over one second, matching Docs/animation-architecture.md's "Clip multi-pistes" example.</summary>
    private const string V3ClipJson =
        "{" +
        "\"version\":1," +
        "\"duration\":\"00:00:01\"," +
        "\"tracks\":[" +
        "{\"property\":\"Opacity\",\"valueType\":\"Single\",\"frames\":[{\"offset\":0,\"value\":\"0\"},{\"offset\":1,\"value\":\"1\"}]}," +
        "{\"property\":\"RenderTransform.Scale\",\"valueType\":\"Vector2\",\"frames\":[{\"offset\":0,\"value\":\"0.8,0.8\"},{\"offset\":1,\"value\":\"1,1\"}]}," +
        "{\"property\":\"Background\",\"valueType\":\"Color\",\"frames\":[{\"offset\":0,\"value\":\"#00000000\"},{\"offset\":1,\"value\":\"#FF0000FF\"}]}" +
        "]" +
        "}";

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window) LoadStrict()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 800));
        MGDesktop desktop = new(runtime);
        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromFile(SamplePath), XamlLoaderMode.Strict, false, true);
        desktop.Windows.Add(window);
        return (runtime, desktop, window);
    }


    private static readonly string SamplePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MGUI.Samples", "Features", "AnimationDemo.xaml"));

    [Fact]
    public void DemoXaml_LoadsInStrictMode_WithItsTransitionsAndTransform()
    {
        Assert.True(File.Exists(SamplePath), SamplePath);
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 800));
        MGDesktop desktop = new(runtime);

        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromFile(SamplePath), XamlLoaderMode.Strict, false, true);
        desktop.Windows.Add(window);

        MGButton hover = window.GetElementByName<MGButton>("HoverButton");
        Assert.Equal(2, hover.Transitions.Count);
        Assert.IsType<UITransition<float>>(hover.Transitions["RenderScale"]);
        Assert.IsType<UITransition<Color>>(hover.Transitions["Background"]);
        Assert.Equal(new Vector2(0.5f, 0.5f), hover.RenderTransform.Origin);
        Assert.Equal(1.06f, hover.RenderScale.Value.HoveredScale);

        foreach (string name in new[] { "ColorButton", "FadeButton", "SlideButton", "SpinButton", "PulseButton", "MarginButton", "PopupButton", "PauseButton", "HalfSpeedButton", "NormalSpeedButton" })
        {
            Assert.NotNull(window.GetElementByName<MGButton>(name));
        }

        Assert.NotNull(window.GetElementByName<MGBorder>("Target"));
        Assert.NotNull(window.GetElementByName<MGBorder>("MarginTarget"));
        Assert.NotNull(window.GetElementByName<MGTextBlock>("ActiveCountText"));
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void DemoXaml_V2Section_DeclaresStatesStyleAndTheEngineDrivenProgress()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 800));
        MGDesktop desktop = new(runtime);
        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromFile(SamplePath), XamlLoaderMode.Strict, false, true);
        desktop.Windows.Add(window);

        //  The implicit style of the window gives every plain button the hover-scale transition; HoverButton keeps its own (same path, element wins).
        MGButton fade = window.GetElementByName<MGButton>("FadeButton");
        Assert.Equal(TimeSpan.FromMilliseconds(100), fade.Transitions["RenderScale"].Duration);
        Assert.Equal(TimeSpan.FromMilliseconds(120), window.GetElementByName<MGButton>("HoverButton").Transitions["RenderScale"].Duration);

        MGToggleButton toggle = window.GetElementByName<MGToggleButton>("StateToggle");
        Assert.Equal(3, toggle.VisualStates.Count);
        Assert.Equal(new Color(0x27, 0xAE, 0x60), toggle.VisualStates["Checked"].Setters[0].Value);
        Assert.Equal(new Vector2(1.05f), toggle.VisualStates["Hover"].Setters[0].Value);
        Assert.IsType<UITransition<Color>>(toggle.Transitions["Background"]);
        Assert.IsType<UITransition<Vector2>>(toggle.Transitions["RenderTransform.Scale"]);

        MGProgressButton progress = window.GetElementByName<MGProgressButton>("EngineProgress");
        Assert.Equal(TimeSpan.FromSeconds(3), progress.Duration);
        Assert.True(progress.IsPaused);
        Assert.False(progress.Animations.IsAnimating("ProgressButton.Value"));

        foreach (string name in new[] { "StoryboardButton", "SequenceButton", "PopButton", "ThemeAnimationButton" })
        {
            Assert.NotNull(window.GetElementByName<MGButton>(name));
        }

        Assert.NotNull(window.GetElementByName<MGBorder>("V2Target"));
        Assert.NotNull(window.GetElementByName<MGTextBlock>("StateText"));
    }

    [Fact]
    public void DemoXaml_V3Section_DeclaresBezierOverrideToggleClipPreviewAndClockControls()
    {
        (_, MGDesktop desktop, MGWindow window) = LoadStrict();

        //  V1/V2 pinned counts, re-asserted here too (work item 4): the V3 column must not change them.
        Assert.Equal(2, window.GetElementByName<MGButton>("HoverButton").Transitions.Count);
        Assert.Equal(0, desktop.Animations.ActiveCount);
        Assert.Equal(3, window.GetElementByName<MGToggleButton>("StateToggle").VisualStates.Count);

        //  U1: the Bezier literal resolves to a real UICubicBezierEasing with the CSS control points from the XAML attribute.
        MGButton bezierButton = window.GetElementByName<MGButton>("V3BezierButton");
        UICubicBezierEasing bezierEasing = Assert.IsType<UICubicBezierEasing>(bezierButton.Transitions["RenderTransform.Translation"].Easing);
        Assert.Equal(0.68f, bezierEasing.X1, 3);
        Assert.Equal(-0.55f, bezierEasing.Y1, 3);
        Assert.Equal(0.27f, bezierEasing.X2, 3);
        Assert.Equal(1.55f, bezierEasing.Y2, 3);

        //  U4: the overriding panel's Hover state carries the flag, the plain one does not.
        MGButton overriding = window.GetElementByName<MGButton>("V3OverridingPanel");
        MGButton plain = window.GetElementByName<MGButton>("V3PlainPanel");
        Assert.True(overriding.VisualStates["Hover"].OverridesLocalValue);
        Assert.False(plain.VisualStates["Hover"].OverridesLocalValue);
        Assert.Equal(new Color(0x3D, 0x6C, 0x9E), ((MGSolidFillBrush)overriding.BackgroundBrush.NormalValue).Color);
        Assert.Equal(new Color(0x3D, 0x6C, 0x9E), ((MGSolidFillBrush)plain.BackgroundBrush.NormalValue).Color);

        //  U5: both toggles exist; CheckedBackgroundBrush itself is code-only (set by WireV3), so nothing more to assert on the static markup.
        Assert.NotNull(window.GetElementByName<MGToggleButton>("V3CheckedToggle"));
        Assert.NotNull(window.GetElementByName<MGToggleButton>("V3PlainToggle"));

        //  U6/U7: the clip and preview targets, both starting invisible (Opacity="0") and centred for the scale track.
        foreach (string name in new[] { "V3ClipTarget", "V3PreviewTarget" })
        {
            MGBorder target = window.GetElementByName<MGBorder>(name);
            Assert.Equal(0f, target.Opacity);
            Assert.Equal(new Vector2(0.5f, 0.5f), target.RenderTransform.Origin);
        }

        Assert.NotNull(window.GetElementByName<MGButton>("V3PlayClip"));
        MGSlider seekSlider = window.GetElementByName<MGSlider>("V3SeekSlider");
        Assert.Equal(0f, seekSlider.Minimum);
        Assert.Equal(100f, seekSlider.Maximum);
        Assert.NotNull(window.GetElementByName<MGButton>("V3DetachPreview"));

        //  U8: the save/load pair and the read-only JSON text box.
        Assert.NotNull(window.GetElementByName<MGButton>("V3SaveStoryboard"));
        Assert.NotNull(window.GetElementByName<MGButton>("V3LoadStoryboard"));
        Assert.True(window.GetElementByName<MGTextBox>("V3StoryboardJson").IsReadonly);

        //  U10: the typewriter reveal, its pause/replay controls, the highlight brush target and the TimeScale slider.
        //  TextCharactersPerSecond is set from code (WireV3), not from a XAML attribute, precisely so this raw XAML load starts no
        //  animation of its own (see the ActiveCount == 0 assertion above) -- Null here is the correct, un-wired starting point.
        Assert.Null(window.GetElementByName<MGTextBlock>("V3Typewriter").TextCharactersPerSecond);
        Assert.NotNull(window.GetElementByName<MGToggleButton>("V3PauseClock"));
        Assert.NotNull(window.GetElementByName<MGButton>("V3ReplayText"));
        Assert.NotNull(window.GetElementByName<MGBorder>("V3Highlight"));
        MGSlider timeScaleSlider = window.GetElementByName<MGSlider>("V3TimeScale");
        Assert.Equal(0.25f, timeScaleSlider.Minimum);
        Assert.Equal(3f, timeScaleSlider.Maximum);

        //  The embedded clip JSON (duplicated in this file, see V3ClipJson's own summary) deserialises to a three-child storyboard.
        UIStoryboard clip = UIKeyFrameClipSerializer.Deserialize(V3ClipJson);
        Assert.Equal(3, clip.Children.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
    }

    /// <summary>U11 fix round (P2): the V3 column used to overflow the window's declared <c>Height="600"</c> -- 11 of its 18 named elements
    /// never received a layout pass and were unreachable. The window is now wrapped in a <see cref="MGUI.Core.UI.Containers.MGScrollViewer"/>
    /// (see <c>AnimationDemo.xaml</c>), so every V3 element must lay out (non-zero <see cref="MGElement.LayoutBounds"/>) at the shipped size,
    /// whether or not it is currently scrolled into view.</summary>
    [Fact]
    public void DemoXaml_V3Section_AllControlsLayOutAtTheShippedWindowSize()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene.Attach(runtime, desktop, window);

        foreach (string name in new[]
                 {
                     "V3BezierButton", "V3OverridingPanel", "V3PlainPanel", "V3CheckedToggle", "V3PlainToggle",
                     "V3PlayClip", "V3ClipTarget", "V3PreviewTarget", "V3SeekSlider", "V3DetachPreview",
                     "V3SaveStoryboard", "V3LoadStoryboard", "V3StoryboardJson", "V3Typewriter", "V3PauseClock",
                     "V3ReplayText", "V3Highlight", "V3TimeScale",
                 })
        {
            MGElement element = window.GetElementByName<MGElement>(name);
            Assert.True(element.LayoutBounds.Width > 0 && element.LayoutBounds.Height > 0, $"{name} has no layout: {element.LayoutBounds}");
        }
    }

    [Fact]
    public void DemoXaml_V3OverridingState_WinsOverALocalBackground_WhileThePlainStateDoesNot()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);

        MGButton overriding = window.GetElementByName<MGButton>("V3OverridingPanel");
        MGButton plain = window.GetElementByName<MGButton>("V3PlainPanel");

        //  A real mouse hover (not a simulated state write) over each button: the same input path a person moving the mouse in the sample uses.
        scene.Mouse = new Point(overriding.LayoutBounds.Center.X, overriding.LayoutBounds.Center.Y);
        scene.Frames(3);
        Assert.Equal(new Color(0x27, 0xAE, 0x60), ((MGSolidFillBrush)overriding.BackgroundBrush.NormalValue).Color);

        scene.Mouse = new Point(plain.LayoutBounds.Center.X, plain.LayoutBounds.Center.Y);
        scene.Frames(3);
        Assert.Equal(new Color(0x3D, 0x6C, 0x9E), ((MGSolidFillBrush)plain.BackgroundBrush.NormalValue).Color);
    }

    [Fact]
    public void DemoXaml_V3ClipTarget_PlaysTheEmbeddedClipJson_LikeThePlayClipButtonWould()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        MGBorder clipTarget = window.GetElementByName<MGBorder>("V3ClipTarget");

        //  Exactly the call V3PlayClip's handler makes (MGUI.Samples/Features/AnimationDemo.xaml.cs, WireV3), driven here without a click.
        clipTarget.Animations.Start(UIKeyFrameClipSerializer.Deserialize(V3ClipJson));

        scene.Frames(31); // ~500 / 1000 ms
        Assert.InRange(clipTarget.Opacity, 0.4f, 0.6f);
        Assert.InRange(clipTarget.RenderTransform.Scale.X, 0.85f, 0.95f);

        scene.Frames(32); // past 1000 ms
        Assert.Equal(1f, clipTarget.Opacity);
        Assert.Equal(1f, clipTarget.RenderTransform.Scale.X);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void DemoXaml_V3PreviewTarget_SeeksWithoutRegistering_AndDetachCancelsIt()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        MGBorder previewTarget = window.GetElementByName<MGBorder>("V3PreviewTarget");

        //  Exactly the call V3SeekSlider's handler makes: a fresh, never-started deserialisation attached as a preview.
        UIStoryboard preview = UIAnimationPreview.Attach(previewTarget, UIKeyFrameClipSerializer.Deserialize(V3ClipJson));
        Assert.Equal(0, desktop.Animations.ActiveCount); // never registered with the manager, even though it is "running"

        preview.Seek(TimeSpan.FromMilliseconds(500)); // the slider at 50 %
        Assert.InRange(previewTarget.Opacity, 0.45f, 0.55f);
        Assert.Equal(0, desktop.Animations.ActiveCount);

        preview.Seek(TimeSpan.FromMilliseconds(1000)); // the slider at 100 %
        Assert.Equal(1f, previewTarget.Opacity);
        Assert.Equal(0, desktop.Animations.ActiveCount);

        //  Exactly the call V3DetachPreview's handler makes.
        UIAnimationPreview.Detach(preview);
        Assert.Equal(UIAnimationState.Cancelled, preview.State);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    [Fact]
    public void DemoXaml_V3Storyboard_SaveThenLoad_RoundTripsThroughJson_LikeTheSampleButtonsWould()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        MGBorder clipTarget = window.GetElementByName<MGBorder>("V3ClipTarget");

        //  Exactly the storyboard V3SaveStoryboard's handler builds (WireV3): unstarted, so every node's Owner is unset.
        UIStoryboard saved = new UIStoryboard
        {
            new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.QuadOut, Name = "v3-save-fade" },
            new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale) { From = new Vector2(0.85f), To = Vector2.One, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.BackOut, Name = "v3-save-scale" },
        };

        //  Exactly V3SaveStoryboard's nameOf and V3LoadStoryboard's resolveElement (a small dictionary of named elements, unused here since no
        //  node names an element -- see WireV3's own comment on this).
        Dictionary<string, MGElement> namedElements = new() { ["V3ClipTarget"] = clipTarget };
        string json = UIAnimationSerializer.Serialize(saved, element => null);
        UIAnimation loaded = UIAnimationSerializer.Deserialize(json, name => namedElements.TryGetValue(name, out MGElement element) ? element : null);

        clipTarget.Animations.Start(loaded);
        scene.Frames(19); // 300 ms
        Assert.Equal(1f, clipTarget.Opacity, 2);
        Assert.Equal(1f, clipTarget.RenderTransform.Scale.X, 2);
    }

    [Fact]
    public void DemoXaml_V3Typewriter_FollowsThePauseToggle_AndReplaysFromZero()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        MGTextBlock typewriter = window.GetElementByName<MGTextBlock>("V3Typewriter");

        //  Exactly what WireV3 sets from code (see AnimationDemo.xaml.cs): starts the reveal on attach (U10).
        typewriter.TextCharactersPerSecond = 12;
        scene.Frames(10);
        double? midProgress = typewriter.TextProgress;
        Assert.True(midProgress is > 0.0 and < 1.0);

        //  Exactly what V3PauseClock's handler writes.
        desktop.Animations.Clock.IsPaused = true;
        scene.Frames(10);
        Assert.Equal(midProgress, typewriter.TextProgress);

        desktop.Animations.Clock.IsPaused = false;

        //  Exactly what V3ReplayText's handler writes: a direct TextProgress = 0 seeks the reveal back to the start (the U10 fix round).
        typewriter.TextProgress = 0;
        Assert.Equal(0.0, typewriter.TextProgress);
        scene.Frames(3);
        Assert.True(typewriter.TextProgress is > 0.0);
    }
}
