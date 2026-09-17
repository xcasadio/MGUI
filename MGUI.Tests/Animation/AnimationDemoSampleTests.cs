using System.Collections.Generic;
using System.IO;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Animation.Composition;
using MGUI.Core.UI.Animation.Easing;
using MGUI.Core.UI.Animation.KeyFrames;
using MGUI.Core.UI.Animation.Targets;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Brushes.FillBrushes;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Animation;

/// <summary>The "animation" validation scenario (Docs/scenario-validation-index.md, <c>SCN-ANIM-001</c>): the capability-by-capability demo XAML
/// (MGUI.Samples/Features/AnimationDemo.xaml) loads through the strict loader, every section's named elements exist and lay out at the shipped
/// window size, and every behaviour that can be driven without the code-behind is exercised directly here. The code-behind
/// (<c>AnimationDemoSample</c>, which wires every button's click handler in its <c>WireXxx</c> methods) is compiled by the samples build but
/// cannot be constructed from here: MGUI.Tests.csproj carries no <c>ProjectReference</c> to MGUI.Samples.csproj (checked directly in this
/// file), so the tests below instead drive, on the real elements this file loads from the same XAML, the exact engine calls each handler makes
/// (<see cref="UIKeyFrameClipSerializer"/>, <see cref="UIAnimationPreview"/>, <see cref="UIAnimationSerializer"/>, a real mouse hover, a direct
/// <c>Desktop.Animations.Clock</c> write, a directly-assigned <see cref="MGHighlightBorderBrush"/>) -- as close an approximation of "click the
/// button" as is possible without that reference.</summary>
public class AnimationDemoSampleTests
{
    /// <summary>Byte-for-byte the same JSON as <c>MGUI.Samples.Features.AnimationDemoAssets.ClipJson</c>: duplicated here, rather than
    /// referenced, because MGUI.Tests has no <c>ProjectReference</c> to MGUI.Samples (see the class summary above) -- the PERIMETER of this
    /// slice does not include MGUI.Tests.csproj, so adding one was not an option. Three tracks (<c>Opacity</c>, <c>RenderTransform.Scale</c>,
    /// <c>Background</c>) over one second, matching Docs/animation-architecture.md's "Clip multi-pistes" example.</summary>
    private const string ClipJson =
        "{" +
        "\"version\":1," +
        "\"duration\":\"00:00:01\"," +
        "\"tracks\":[" +
        "{\"property\":\"Opacity\",\"valueType\":\"Single\",\"frames\":[{\"offset\":0,\"value\":\"0\"},{\"offset\":1,\"value\":\"1\"}]}," +
        "{\"property\":\"RenderTransform.Scale\",\"valueType\":\"Vector2\",\"frames\":[{\"offset\":0,\"value\":\"0.8,0.8\"},{\"offset\":1,\"value\":\"1,1\"}]}," +
        "{\"property\":\"Background\",\"valueType\":\"Color\",\"frames\":[{\"offset\":0,\"value\":\"#00000000\"},{\"offset\":1,\"value\":\"#FF0000FF\"}]}" +
        "]" +
        "}";

    private static readonly string SamplePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MGUI.Samples", "Features", "AnimationDemo.xaml"));

    /// <summary>Every element every section names, grouped by section for readability; used by both the existence and the layout test so the
    /// two can never drift apart.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> SectionElementNames = new Dictionary<string, string[]>
    {
        ["1. Transitions"] = new[] { "TransitionsHoverButton", "TransitionsRetargetButton" },
        ["2. Fluent"] = new[] { "FluentFadeButton", "FluentBounceButton", "FluentSequenceButton", "FluentCancelButton", "FluentTarget" },
        ["3. Transform"] = new[] { "TransformTarget", "TransformRotationSlider", "TransformScaleSlider", "TransformHoverButton" },
        ["4. Clock"] = new[] { "ClockPauseToggle", "ClockActiveCountText", "ClockTimeScaleSlider" },
        ["5. Composition"] = new[] { "CompositionTargetA", "CompositionTargetB", "CompositionStoryboardButton", "CompositionSequenceButton" },
        ["6. Keyframes and clip"] = new[] { "KeyframesPopButton", "KeyframesTarget", "ClipPlayButton", "ClipTarget", "ClipJsonText" },
        ["7. States"] = new[] { "StatesToggle", "StatesOverridingPanel", "StatesPlainPanel", "StatesCheckedToggle", "StatesPlainToggle" },
        ["8. Styles"] = new[] { "StylesButtonA", "StylesButtonB", "StylesRefreshButton", "StylesThemeAnimationButton" },
        ["9. Brushes"] = new[] { "BrushesSharedLeftButton", "BrushesSharedRightButton", "BrushesAnimateLeftButton", "BrushesInlineSlider", "BrushesInlineTarget", "BrushesGradientButton", "BrushesGradientTarget", "BrushesBorderButton", "BrushesBorderTarget" },
        ["10. Highlight"] = new[] { "HighlightAutoBorder", "HighlightManualBorder", "HighlightStartStopButton", "HighlightStopOnHoverBorder", "HighlightResumeButton" },
        ["11. Preview"] = new[] { "PreviewTarget", "PreviewSlider", "PreviewDetachButton" },
        ["12. Serialisation"] = new[] { "SerializationSaveButton", "SerializationLoadButton", "SerializationTarget", "SerializationJsonText" },
        ["13. Controls"] = new[] { "EngineProgressButton", "TypewriterText", "EngineReplayButton" },
        ["14. Diagnostics"] = new[] { "DiagnosticsPathsButton", "DiagnosticsPathsText", "DiagnosticsMeasureButton", "DiagnosticsTarget", "DiagnosticsAllocationText" },
        ["15. Awaitable animations"] = new[] { "AwaitPlayButton", "AwaitCancelButton", "AwaitTarget", "AwaitResultText" },
        ["16. Smooth scrolling"] = new[] { "ScrollDemoViewer", "ScrollDemoTopButton", "ScrollDemoBottomButton" },
        ["17. Sprite-sheet frames"] = new[] { "FramesTarget", "FramesPlayButton", "FramesPauseToggle" },
        ["18. Layout transitions"] = new[] { "LayoutListPanel", "LayoutInsertButton", "LayoutRemoveButton", "LayoutMoveButton", "LayoutSizeTarget", "LayoutResizeButton" },
    };

    /// <summary>The XAML references a resource style ("Styles and theme" section, <c>StylesButtonA</c>/<c>StylesButtonB</c>'s
    /// <c>StyleNames="StylesSharedStyle"</c>) that <c>AnimationDemoSample</c>'s own <c>Initialize</c> callback registers before parsing
    /// (<see cref="MGResources.AddStyle"/>) -- an unresolved <c>StyleNames</c> entry is a hard <see cref="KeyNotFoundException"/> from the
    /// loader, not a silent no-op, so this file registers the same minimal style here (duplicated rather than referenced, see the class
    /// summary above).</summary>
    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window) LoadStrict()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 1280, 800));
        MGDesktop desktop = new(runtime);
        desktop.Resources.AddStyle("StylesSharedStyle", new Style { TargetType = MGElementType.Button });
        desktop.Resources.AddTexture("AngryMeteor", new MGTextureData(new GraphTestImageResource("AngryMeteor", 40, 48)));
        MGWindow window = XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromFile(SamplePath), XamlLoaderMode.Strict, false, true);
        desktop.Windows.Add(window);
        return (runtime, desktop, window);
    }

    [Fact]
    public void DemoXaml_LoadsInStrictMode_WithEverySectionsNamedElements()
    {
        Assert.True(File.Exists(SamplePath), SamplePath);
        (_, MGDesktop desktop, MGWindow window) = LoadStrict();

        foreach ((string section, string[] names) in SectionElementNames)
        {
            foreach (string name in names)
            {
                Assert.True(window.GetElementByName<MGElement>(name) != null, $"{section}: '{name}' not found.");
            }
        }

        //  The raw XAML load starts nothing by itself: every section's animation is wired by the code-behind (not constructed from here),
        //  set up from a button click or a slider drag, never from a XAML attribute alone.
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    /// <summary>Every named element of every section must receive a layout pass at the shipped window size, whether or not it is currently
    /// scrolled into view (the whole page lives inside one <see cref="MGUI.Core.UI.Containers.MGScrollViewer"/>).</summary>
    [Fact]
    public void DemoXaml_AllSectionElements_LayOutAtTheShippedWindowSize()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene.Attach(runtime, desktop, window);

        foreach ((string section, string[] names) in SectionElementNames)
        {
            foreach (string name in names)
            {
                MGElement element = window.GetElementByName<MGElement>(name);
                Assert.True(element.LayoutBounds.Width > 0 && element.LayoutBounds.Height > 0, $"{section}: '{name}' has no layout: {element.LayoutBounds}");
            }
        }
    }

    /// <summary>Section 7 (named visual states): a real mouse hover (not a simulated state write) over each button -- the same input path a
    /// person moving the mouse in the sample uses. <c>StatesOverridingPanel</c>'s Hover state has <c>OverridesLocalValue="True"</c> and wins
    /// over its local Background; <c>StatesPlainPanel</c>'s identical Hover setter, without the flag, does not.</summary>
    [Fact]
    public void DemoXaml_StatesOverridingPanel_WinsOverALocalBackground_WhileThePlainPanelDoesNot()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);

        MGButton overriding = window.GetElementByName<MGButton>("StatesOverridingPanel");
        MGButton plain = window.GetElementByName<MGButton>("StatesPlainPanel");

        //  Section 7 sits below the fold of the whole page's single ScrollViewer: scroll it into view first. RenderBounds/LayoutBounds stay
        //  in unscrolled content space (unaffected by VerticalOffset), so the simulated mouse position must subtract the offset itself to
        //  land on the actual screen point the element now occupies.
        MGScrollViewer scrollViewer = window.GetElementByName<MGScrollViewer>("RootScrollViewer");
        scrollViewer.VerticalOffset = Math.Max(0f, overriding.LayoutBounds.Top - window.Top - 100f);
        scene.Frames(2);

        scene.Mouse = new Point(overriding.LayoutBounds.Center.X, (int)(overriding.LayoutBounds.Center.Y - scrollViewer.VerticalOffset));
        scene.Frames(3);
        Assert.Equal(new Color(0x27, 0xAE, 0x60), ((MGSolidFillBrush)overriding.BackgroundBrush.NormalValue).Color);

        scene.Mouse = new Point(plain.LayoutBounds.Center.X, (int)(plain.LayoutBounds.Center.Y - scrollViewer.VerticalOffset));
        scene.Frames(3);
        Assert.Equal(new Color(0x3D, 0x6C, 0x9E), ((MGSolidFillBrush)plain.BackgroundBrush.NormalValue).Color);
    }

    /// <summary>Section 6 (keyframes and clip): exactly the call the "Load clip" button's handler makes (<c>UIKeyFrameClipSerializer.Deserialize</c>
    /// on the embedded JSON, started on <c>ClipTarget</c>), driven here without a click.</summary>
    [Fact]
    public void DemoXaml_ClipTarget_PlaysTheEmbeddedClipJson_LikeTheLoadClipButtonWould()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        MGBorder clipTarget = window.GetElementByName<MGBorder>("ClipTarget");

        clipTarget.Animations.Start(UIKeyFrameClipSerializer.Deserialize(ClipJson));

        scene.Frames(31); // ~500 / 1000 ms
        Assert.InRange(clipTarget.Opacity, 0.4f, 0.6f);
        Assert.InRange(clipTarget.RenderTransform.Scale.X, 0.85f, 0.95f);

        scene.Frames(32); // past 1000 ms
        Assert.Equal(1f, clipTarget.Opacity);
        Assert.Equal(1f, clipTarget.RenderTransform.Scale.X);
        Assert.Equal(0, desktop.Animations.ActiveCount);

        //  The embedded clip JSON deserialises to a three-child storyboard.
        UIStoryboard clip = UIKeyFrameClipSerializer.Deserialize(ClipJson);
        Assert.Equal(3, clip.Children.Count);
        Assert.Equal(TimeSpan.FromSeconds(1), clip.Duration);
    }

    /// <summary>Section 11 (preview and seek): exactly the calls <c>PreviewSlider</c> and <c>PreviewDetachButton</c>'s handlers make -- a
    /// fresh, never-started deserialisation attached as a preview, never registered with the manager, seeked at two points, then detached.</summary>
    [Fact]
    public void DemoXaml_PreviewTarget_SeeksWithoutRegistering_AndDetachCancelsIt()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        MGBorder previewTarget = window.GetElementByName<MGBorder>("PreviewTarget");

        UIStoryboard preview = UIAnimationPreview.Attach(previewTarget, UIKeyFrameClipSerializer.Deserialize(ClipJson));
        Assert.Equal(0, desktop.Animations.ActiveCount); // never registered with the manager, even though it is "running"

        preview.Seek(TimeSpan.FromMilliseconds(500)); // the slider at 50 %
        Assert.InRange(previewTarget.Opacity, 0.45f, 0.55f);
        Assert.Equal(0, desktop.Animations.ActiveCount);

        preview.Seek(TimeSpan.FromMilliseconds(1000)); // the slider at 100 %
        Assert.Equal(1f, previewTarget.Opacity);
        Assert.Equal(0, desktop.Animations.ActiveCount);

        UIAnimationPreview.Detach(preview);
        Assert.Equal(UIAnimationState.Cancelled, preview.State);
        Assert.Equal(0, desktop.Animations.ActiveCount);
    }

    /// <summary>Section 12 (serialisation): exactly the storyboard the "Save" handler builds (unstarted, so every node's Owner is unset),
    /// serialised then deserialised through <see cref="UIAnimationSerializer"/> and started on <c>SerializationTarget</c>, like "Load + play"
    /// would. <c>namedElements</c> mirrors the small name-to-element dictionary a <c>resolveElement</c> delegate needs (unused here since no
    /// node names an element, but the wiring is the same either way).</summary>
    [Fact]
    public void DemoXaml_SerializationTarget_SaveThenLoad_RoundTripsThroughJson_LikeTheSampleButtonsWould()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        MGBorder serializationTarget = window.GetElementByName<MGBorder>("SerializationTarget");

        UIStoryboard saved = new()
        {
            new UIPropertyAnimation<float>(UIBuiltInAnimationTargets.Paths.Opacity) { From = 0f, To = 1f, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.QuadOut, Name = "demo-fade" },
            new UIPropertyAnimation<Vector2>(UIBuiltInAnimationTargets.Paths.RenderTransformScale) { From = new Vector2(0.85f), To = Vector2.One, Duration = TimeSpan.FromMilliseconds(300), Easing = UIEasing.BackOut, Name = "demo-scale" },
        };

        Dictionary<string, MGElement> namedElements = new() { ["SerializationTarget"] = serializationTarget };
        string json = UIAnimationSerializer.Serialize(saved, element => namedElements.FirstOrDefault(kv => kv.Value == element).Key);
        UIAnimation loaded = UIAnimationSerializer.Deserialize(json, name => namedElements.TryGetValue(name, out MGElement element) ? element : null);

        serializationTarget.Animations.Start(loaded);
        scene.Frames(19); // 300 ms
        Assert.Equal(1f, serializationTarget.Opacity, 2);
        Assert.Equal(1f, serializationTarget.RenderTransform.Scale.X, 2);
    }

    /// <summary>Section 9 (animatable brushes): the two Shared buttons take the same frozen palette brush the "Animate left only" handler
    /// would; animating only the left button's <c>Background</c> clones it once for that run and leaves the frozen instance -- and so the
    /// right button, which still references it directly -- untouched.</summary>
    [Fact]
    public void DemoXaml_SharedFrozenBrush_StaysUntouched_WhenOnlyOneButtonAnimates()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);

        MGButton left = window.GetElementByName<MGButton>("BrushesSharedLeftButton");
        MGButton right = window.GetElementByName<MGButton>("BrushesSharedRightButton");
        MGSolidFillBrush shared = SolidFillBrushes.CornflowerBlue;
        left.BackgroundBrush.NormalValue = shared;
        right.BackgroundBrush.NormalValue = shared;

        left.Animate(UIColorAnimationTargets.Paths.Background, Color.OrangeRed, 0.3).Named("test-brush-left").Play();
        scene.Frames(5);

        Assert.Same(shared, right.BackgroundBrush.NormalValue);
        Assert.Equal(Color.CornflowerBlue, shared.Color);
        Assert.NotEqual(Color.CornflowerBlue, ((MGSolidFillBrush)left.BackgroundBrush.NormalValue).Color);
        Assert.True(shared.IsFrozen);
    }

    /// <summary>Section 10 (highlight): <c>HighlightAutoBorder</c>'s brush (<see cref="MGHighlightBorderBrush.AutoStart"/> defaulting to true)
    /// starts its own run as soon as it becomes the effective border; <c>HighlightManualBorder</c>'s (<c>AutoStart = false</c>) stays at
    /// progress zero until the application animates it -- exactly what <c>HighlightStartStopButton</c>'s handler does.</summary>
    [Fact]
    public void DemoXaml_HighlightAutoBorder_StartsItsOwnRun_WhileTheManualOneStaysStillUntilDriven()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);

        MGBorder autoBorder = window.GetElementByName<MGBorder>("HighlightAutoBorder");
        MGBorder manualBorder = window.GetElementByName<MGBorder>("HighlightManualBorder");

        //  A couple of settled frames (matching AnimationTestScene.Attach's own warm-up) before installing the highlight brushes: found by
        //  probing directly that MGElement.SyncBorderHighlightRun needs the element's layout to have gone through at least one full pass.
        MGScrollViewer scrollViewer = window.GetElementByName<MGScrollViewer>("RootScrollViewer");
        scrollViewer.VerticalOffset = Math.Max(0f, autoBorder.LayoutBounds.Top - window.Top - 100f);
        scene.Frames(2);

        autoBorder.BorderBrush = new MGHighlightBorderBrush(autoBorder.BorderBrush, Color.Gold, HighlightAnimation.Pulse);
        MGHighlightBorderBrush manualBase = new(manualBorder.BorderBrush, Color.Gold, HighlightAnimation.Scan) { AutoStart = false };
        manualBorder.BorderBrush = manualBase;

        scene.Frames(30);
        Assert.True(((MGHighlightBorderBrush)autoBorder.GetBorder().BorderBrush).AnimationProgress > 0.0);
        Assert.False(manualBorder.Animations.IsAnimating(UIBuiltInAnimationTargets.Paths.BorderBrushHighlightProgress));
        Assert.Equal(0.0, manualBase.AnimationProgress);

        //  Exactly what HighlightStartStopButton's handler does when nothing is animating yet.
        manualBorder.Animate(UIBuiltInAnimationTargets.Paths.BorderBrushHighlightProgress, 0.0, 1.0, 1.0).RepeatForever().Named("test-highlight-manual").Play();
        scene.Frames(20); // ~0.3 s of a 1 s cycle
        Assert.True(((MGHighlightBorderBrush)manualBorder.GetBorder().BorderBrush).AnimationProgress > 0.0);
    }

    /// <summary>Section 13 (controls on the engine): exactly what <c>EngineReplayButton</c>'s handler writes -- <c>TextProgress = 0</c> seeks
    /// a completed or in-progress typewriter reveal back to the start.</summary>
    [Fact]
    public void DemoXaml_TypewriterText_ReplaysFromZero()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        MGTextBlock typewriter = window.GetElementByName<MGTextBlock>("TypewriterText");

        typewriter.TextCharactersPerSecond = 12;
        scene.Frames(10);
        double? midProgress = typewriter.TextProgress;
        Assert.True(midProgress is > 0.0 and < 1.0);

        typewriter.TextProgress = 0;
        Assert.Equal(0.0, typewriter.TextProgress);
        scene.Frames(3);
        Assert.True(typewriter.TextProgress is > 0.0);
    }

    /// <summary>Section 4 (clock): exactly what <c>ClockPauseToggle</c>'s handler writes -- pausing <see cref="MGDesktop.Animations"/>'s clock
    /// freezes every running animation (here, the same typewriter reveal as the previous test) in place until it is unpaused.</summary>
    [Fact]
    public void DemoXaml_ClockPause_FreezesARunningAnimation_UntilUnpaused()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = LoadStrict();
        AnimationTestScene scene = AnimationTestScene.Attach(runtime, desktop, window);
        MGTextBlock typewriter = window.GetElementByName<MGTextBlock>("TypewriterText");
        typewriter.TextCharactersPerSecond = 12;
        scene.Frames(10);
        double? midProgress = typewriter.TextProgress;

        desktop.Animations.Clock.IsPaused = true;
        scene.Frames(10);
        Assert.Equal(midProgress, typewriter.TextProgress);

        desktop.Animations.Clock.IsPaused = false;
        scene.Frames(3);
        Assert.True(typewriter.TextProgress > midProgress);
    }
}
