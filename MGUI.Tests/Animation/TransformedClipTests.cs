using System.Collections.Generic;
using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Animation;

/// <summary>Slice Y10 (Docs/Tasks/animation-v5-tasks.md, Docs/decisions/0011-animation-v5.md): a draw-time clip built from the ambient draw
/// transform (<see cref="MGElement.TransformClipBounds"/>) instead of a coordinate-space conversion that only knew about
/// <see cref="MGWindow.Scale"/>. Covers <see cref="MGScrollViewer.GetContentsClipDefinition"/>: with no transform (plain and scaled window)
/// the clip matches the pre-fix formula exactly; under a transform (a <see cref="MGWindow"/> sliding via its Y7 enter/exit transform, or a
/// <see cref="MGElement.RenderTransform"/> on the scroll viewer itself) the clip follows the content to where it is actually drawn.</summary>
public class TransformedClipTests
{
    private static void Frame(GraphTestRuntime runtime, MGDesktop desktop, int frameIndex)
    {
        MouseState mouse = new(1, 1, 0, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frameIndex), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
        desktop.Update();
    }

    private static GraphNoOpDrawTransaction Draw(GraphTestRuntime runtime, MGDesktop desktop)
    {
        GraphNoOpDrawTransaction transaction = new(runtime, DrawSettings.Default);
        desktop.Draw(transaction);
        return transaction;
    }

    /// <summary>The last (innermost) recorded clip whose debug name is <c>"ScrollViewer.Viewport"</c>
    /// (<see cref="MGScrollViewer.GetContentsClipDefinition"/>'s own debug name).</summary>
    private static Rectangle? LastScrollViewerViewportClip(GraphNoOpDrawTransaction transaction)
    {
        for (int i = transaction.ClipPushes.Count - 1; i >= 0; i--)
        {
            if (transaction.ClipPushes[i].DebugName == "ScrollViewer.Viewport")
            {
                return transaction.ClipPushes[i].Bounds;
            }
        }

        return null;
    }

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window, MGScrollViewer ScrollViewer) BuildScrollViewerScene(float windowScale = 1f)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 20, 30, 300, 220) { WindowStyle = WindowStyle.None, Scale = windowScale };
        MGScrollViewer scrollViewer = new(window) { HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch };
        MGBorder content = new(window) { PreferredWidth = 400, PreferredHeight = 400 };
        scrollViewer.SetContent(content);
        window.SetContent(scrollViewer);
        desktop.Windows.Add(window);

        Frame(runtime, desktop, 1);
        Frame(runtime, desktop, 2);

        return (runtime, desktop, window, scrollViewer);
    }

    [Fact]
    public void ContentClip_NoTransform_MatchesTheHeadFormula()
    {
        var scene = BuildScrollViewerScene(windowScale: 1f);

        GraphNoOpDrawTransaction transaction = Draw(scene.Runtime, scene.Desktop);

        Assert.Empty(transaction.TransformPushes); // an unscaled window without enter/exit pushes no transform at all (Y7)
        Rectangle? recorded = LastScrollViewerViewportClip(transaction);
        Assert.NotNull(recorded);

        // The pre-Y10 formula: ConvertCoordinateSpace(UnscaledScreen, Screen, ContentViewport.GetTranslated(DA.Offset)), i.e. the two-corner
        // mapping through the window's own unscaled-to-scaled matrix (identity here, MGWindow.Scale == 1).
        Rectangle expected = scene.ScrollViewer.ContentViewport.GetTranslated(scene.ScrollViewer.Origin)
            .CreateTransformedF(scene.Window.UnscaledScreenSpaceToScaledScreenSpace).RoundUp();
        Assert.Equal(expected, recorded.Value);
    }

    [Fact]
    public void ContentClip_ScaledWindow_NoOtherTransform_MatchesTheHeadFormula()
    {
        var scene = BuildScrollViewerScene(windowScale: 1.5f);

        GraphNoOpDrawTransaction transaction = Draw(scene.Runtime, scene.Desktop);

        // A scaled window always pushes its own UnscaledScreenSpaceToScaledScreenSpace matrix around its content (Y7): the ambient
        // transform already carries MGWindow.Scale, exactly the matrix the pre-Y10 ConvertCoordinateSpace call applied by hand.
        Matrix windowScaleMatrix = Assert.Single(transaction.TransformPushes);
        AssertMatrix(scene.Window.UnscaledScreenSpaceToScaledScreenSpace, windowScaleMatrix);

        Rectangle? recorded = LastScrollViewerViewportClip(transaction);
        Assert.NotNull(recorded);

        Rectangle expected = scene.ScrollViewer.ContentViewport.GetTranslated(scene.ScrollViewer.Origin)
            .CreateTransformedF(scene.Window.UnscaledScreenSpaceToScaledScreenSpace).RoundUp();
        Assert.Equal(expected, recorded.Value);
    }

    [Fact]
    public void ContentClip_RenderTransformOnTheScrollViewerItself_FollowsTheTranslation()
    {
        var scene = BuildScrollViewerScene(windowScale: 1f);
        scene.ScrollViewer.RenderTransform.Translation = new Vector2(40f, 15f);

        GraphNoOpDrawTransaction transaction = Draw(scene.Runtime, scene.Desktop);

        Matrix localTransform = Assert.Single(transaction.TransformPushes);
        Rectangle? recorded = LastScrollViewerViewportClip(transaction);
        Assert.NotNull(recorded);

        // Same viewport, transformed through the exact matrix that was pushed to draw the scroll viewer's own content (the ambient
        // transform the fixed GetContentsClipDefinition reads): the clip follows the content to where it is actually drawn, not to
        // the untransformed place (the pre-Y10 bug -- the CLAIM's "element carrying a RenderTransform inside an ordinary scroll viewer").
        Rectangle untransformed = scene.ScrollViewer.ContentViewport.GetTranslated(scene.ScrollViewer.Origin)
            .CreateTransformedF(Matrix.Identity).RoundUp();
        Rectangle expected = untransformed.CreateTransformedBoundsF(localTransform).RoundUp();
        Assert.Equal(expected, recorded.Value);
        Assert.NotEqual(untransformed, recorded.Value);
    }

    /// <summary>The author's case (section 20 of the sample): a combo box dropdown (an <see cref="MGWindow"/>) whose own Y7 enter/exit
    /// transform slides it into place. Before this slice, the dropdown's <see cref="MGComboBox{TItemType}.DropdownScrollViewer"/> clipped
    /// its items at their FINAL rectangle while the dropdown's background and border slid with the window.</summary>
    [Fact]
    public void ComboBoxDropdown_SlideDownEntry_ContentClipFollowsTheWindowTranslation()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 300, 400) { WindowStyle = WindowStyle.None };
        desktop.Windows.Add(window);
        MGComboBox<string> comboBox = new(window) { PreferredHeight = 30 };
        comboBox.SetItemsSource(new List<string> { "Alpha", "Beta", "Gamma", "Delta" });
        window.SetContent(comboBox);
        Frame(runtime, desktop, 1);
        Frame(runtime, desktop, 2);

        comboBox.Dropdown.EnterExit = new UIEnterExitSettings
        {
            EnterEffect = UIEnterExitEffect.SlideDown,
            EnterDuration = TimeSpan.FromMilliseconds(200),
        };

        comboBox.IsDropdownOpen = true;
        Frame(runtime, desktop, 3);
        Frame(runtime, desktop, 4); // mid-run: 2 frames * 16ms into a 200ms entry

        UIWindowTransform midRunTransform = comboBox.Dropdown.EnterExitWindowTransformOrNull;
        Assert.NotNull(midRunTransform);
        Assert.False(midRunTransform.IsIdentity);
        Vector2 pivot = comboBox.Dropdown.ActualLayoutBounds.Center.ToVector2();
        Matrix midRunMatrix = midRunTransform.ToMatrix(pivot);

        GraphNoOpDrawTransaction midRunTransaction = Draw(runtime, desktop);
        Rectangle? midRunClip = LastScrollViewerViewportClip(midRunTransaction);
        Assert.NotNull(midRunClip);

        // Let the entry finish, then draw again: the untransformed baseline for the exact same viewport.
        for (int i = 0; i < 20; i++)
        {
            Frame(runtime, desktop, 5 + i);
        }
        Assert.True(comboBox.Dropdown.EnterExitWindowTransformOrNull == null || comboBox.Dropdown.EnterExitWindowTransformOrNull.IsIdentity);

        GraphNoOpDrawTransaction baselineTransaction = Draw(runtime, desktop);
        Rectangle? baselineClip = LastScrollViewerViewportClip(baselineTransaction);
        Assert.NotNull(baselineClip);

        Rectangle expectedMidRunClip = baselineClip.Value.CreateTransformedBoundsF(midRunMatrix).RoundUp();
        Assert.Equal(expectedMidRunClip, midRunClip.Value);

        // The regression itself: the clip actually moved with the window (it is not still cut at the final, untransformed rectangle).
        Assert.NotEqual(baselineClip.Value, midRunClip.Value);
    }

    private static void AssertMatrix(Matrix expected, Matrix actual)
    {
        const float tolerance = 1e-4f;
        Assert.Equal(expected.M11, actual.M11, tolerance);
        Assert.Equal(expected.M12, actual.M12, tolerance);
        Assert.Equal(expected.M21, actual.M21, tolerance);
        Assert.Equal(expected.M22, actual.M22, tolerance);
        Assert.Equal(expected.M41, actual.M41, tolerance);
        Assert.Equal(expected.M42, actual.M42, tolerance);
    }
}
