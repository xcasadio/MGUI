using MGUI.Core.UI;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MGUI.Shared.Input;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Input;

/// <summary>
/// Regression coverage for the cross-window hover/pressed occlusion fix (decision utilisateur 2026-09-02,
/// option (b), documented in Docs/input-architecture.md "Fenetres superposees"): <see cref="MGWindow.HoveredElement"/> and
/// <see cref="MGWindow.PressedElement"/> of a window visually occluded by a higher window at the current
/// mouse position must no longer light up, while an ongoing press/drag started before the occlusion must
/// keep following its owning window.
/// </summary>
public class CrossWindowHoverPressedOcclusionTests
{
    [Fact]
    public void Hover_AtOverlapPoint_LightsUpOnlyFrontWindow_NotOccludedBackWindow()
    {
        Harness harness = CreateHarness();

        Point overlapPoint = OverlapPoint(harness);
        // Window.VisualState (which MGDesktop.Update's occlusion check reads) is computed once per tick
        // BEFORE this window's own OnBeginUpdate refreshes HoveredElement for that same tick, so a fresh
        // hover position takes one extra tick to be reflected in the front window's VisualState.IsHovered
        // - the same one-tick lag the pre-existing tooltip occlusion protection already has.
        AdvanceFrame(harness, 32, overlapPoint);
        AdvanceFrame(harness, 48, overlapPoint);

        Assert.Same(harness.FrontButton, harness.FrontWindow.HoveredElement);
        Assert.Null(harness.BackWindow.HoveredElement);
    }

    [Fact]
    public void Hover_AtBackOnlyPoint_LightsUpBackWindow_FrontWindowUnaffected()
    {
        // Control case: away from the overlap region, occlusion must not spuriously suppress the
        // back window's own hover (it isn't actually covered by the front window at that position).
        Harness harness = CreateHarness();

        Point backOnlyPoint = BackOnlyPoint(harness);
        AdvanceFrame(harness, 32, backOnlyPoint);

        Assert.Same(harness.BackButton, harness.BackWindow.HoveredElement);
        Assert.Null(harness.FrontWindow.HoveredElement);
    }

    [Fact]
    public void Pressed_AtOverlapPoint_LightsUpOnlyFrontWindow_NotOccludedBackWindow()
    {
        Harness harness = CreateHarness();

        Point overlapPoint = OverlapPoint(harness);
        AdvanceFrame(harness, 32, overlapPoint);
        AdvanceFrame(harness, 48, overlapPoint, MouseButton.Left);

        Assert.Same(harness.FrontButton, harness.FrontWindow.PressedElement);
        Assert.Null(harness.BackWindow.PressedElement);
    }

    [Fact]
    public void OngoingPress_MovingIntoOccludedRegionWhileHeld_KeepsFollowingItsOwningBackWindow()
    {
        // Drag-capture semantics to preserve (tache 4 caution): a press/drag started on the back window
        // while it wasn't occluded must keep following its owner once the cursor crosses into the region
        // now covered by the front window - PressedElement is only re-evaluated on the press/release tick,
        // never reset just because occlusion became true mid-press.
        Harness harness = CreateHarness();

        Point backOnlyPoint = BackOnlyPoint(harness);
        AdvanceFrame(harness, 32, backOnlyPoint);
        AdvanceFrame(harness, 48, backOnlyPoint, MouseButton.Left);

        Assert.Same(harness.BackButton, harness.BackWindow.PressedElement);

        Point overlapPoint = OverlapPoint(harness);
        AdvanceFrame(harness, 64, overlapPoint, MouseButton.Left);

        Assert.Same(harness.BackButton, harness.BackWindow.PressedElement);
    }

    private static Point OverlapPoint(Harness harness)
    {
        Rectangle back = harness.BackButton.ActualLayoutBounds;
        Rectangle front = harness.FrontButton.ActualLayoutBounds;
        Rectangle overlap = Rectangle.Intersect(back, front);
        Assert.True(overlap.Width > 0 && overlap.Height > 0);
        return overlap.Center;
    }

    private static Point BackOnlyPoint(Harness harness)
    {
        Rectangle back = harness.BackButton.ActualLayoutBounds;
        Rectangle front = harness.FrontButton.ActualLayoutBounds;
        Point candidate = new(back.Left + 10, back.Center.Y);
        Assert.True(back.Contains(candidate));
        Assert.False(front.Contains(candidate));
        return candidate;
    }

    private static Harness CreateHarness()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow backWindow = new(desktop, 0, 0, 300, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGButton backButton = new(backWindow);
        backWindow.SetContent(backButton);

        MGWindow frontWindow = new(desktop, 150, 0, 300, 200) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        MGButton frontButton = new(frontWindow);
        frontWindow.SetContent(frontButton);

        // frontWindow is added last, so it is on top of the visual stack (Windows.Reverse().OrderByDescending(IsTopmost)),
        // matching the pattern already established by NavigationFrontToBackFallbackTests.
        desktop.Windows.Add(backWindow);
        desktop.Windows.Add(frontWindow);

        Harness harness = new(runtime, desktop, backWindow, backButton, frontWindow, frontButton);

        AdvanceFrame(harness, 0, new Point(1, 1));
        AdvanceFrame(harness, 16, new Point(1, 1));

        return harness;
    }

    private static void AdvanceFrame(Harness harness, int totalElapsedMs, Point position, MouseButton? pressedButton = null)
    {
        harness.Runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(position, pressedButton),
            new KeyboardState()));
        harness.Desktop.Update();
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton = null)
        => new(
            position.X,
            position.Y,
            0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Middle ? ButtonState.Pressed : ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);

    private sealed record Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow BackWindow, MGButton BackButton, MGWindow FrontWindow, MGButton FrontButton);
}
