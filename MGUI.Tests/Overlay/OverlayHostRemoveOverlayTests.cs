namespace MGUI.Tests.Overlay;

using MGUI.Core.UI;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

/// <summary>
/// <see cref="MGOverlayHost.TryRemoveOverlay"/> on an open overlay closes it as <see cref="MGOverlayHost.TryClose"/> does. It used to leave
/// the removed overlay in <see cref="MGOverlayHost.OpenOverlays"/>, so it stayed the <see cref="MGOverlayHost.ActiveOverlay"/>: still shown,
/// still modal over the whole desktop, although no longer in <see cref="MGOverlayHost.Overlays"/>.
/// </summary>
public class OverlayHostRemoveOverlayTests
{
    private sealed class Harness
    {
        public GraphTestRuntime Runtime { get; } = new(new Rectangle(0, 0, 800, 600));
        public MGDesktop Desktop { get; }
        public MGOverlayHost Host => Desktop.OverlayHost;
        private int _elapsedMs;

        public Harness()
        {
            Desktop = new MGDesktop(Runtime);
        }

        public void Frame(Point mousePosition, MouseButton? pressedButton = null)
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(
                TimeSpan.FromMilliseconds(_elapsedMs),
                TimeSpan.FromMilliseconds(16),
                new MouseState(
                    mousePosition.X,
                    mousePosition.Y,
                    0,
                    pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released,
                    ButtonState.Released),
                new KeyboardState()));
            Desktop.Update();
            _elapsedMs += 16;
        }

        public void Click(Point position)
        {
            Frame(position);
            Frame(position, MouseButton.Left);
            Frame(position);
        }

        public MGOverlay OpenOverlay(double zIndex = 0)
        {
            MGOverlay overlay = Host.AddOverlay(new MGButton(Host.ParentWindow));
            overlay.ZIndex = zIndex;
            overlay.IsOpen = true;
            return overlay;
        }
    }

    [Fact]
    public void RemovingAnOpenOverlay_ClosesIt_AndItIsNoLongerActive()
    {
        Harness harness = new();
        MGOverlay overlay = harness.OpenOverlay();
        int closing = 0, closed = 0;
        overlay.OnClosing += (_, _) => closing++;
        overlay.OnClosed += (_, _) => closed++;
        harness.Frame(new Point(1, 1));
        Assert.Same(overlay, harness.Host.ActiveOverlay);

        bool removed = harness.Host.TryRemoveOverlay(overlay);

        Assert.True(removed);
        Assert.DoesNotContain(overlay, harness.Host.Overlays);
        Assert.DoesNotContain(overlay, harness.Host.OpenOverlays);
        Assert.Null(harness.Host.ActiveOverlay);
        Assert.False(overlay.IsOpen);
        Assert.Equal(1, closing);
        Assert.Equal(1, closed);
        Assert.Equal(Visibility.Collapsed, harness.Host.ActiveOverlayPresenter.Visibility);
        Assert.False(harness.Desktop.ShouldCaptureGameplayInput());
    }

    [Fact]
    public void RemovingTheActiveOfTwoOpenOverlays_HandsOverToTheOther()
    {
        Harness harness = new();
        MGOverlay below = harness.OpenOverlay(zIndex: 0);
        MGOverlay above = harness.OpenOverlay(zIndex: 1);
        harness.Frame(new Point(1, 1));
        Assert.Same(above, harness.Host.ActiveOverlay);

        Assert.True(harness.Host.TryRemoveOverlay(above));

        Assert.Same(below, harness.Host.ActiveOverlay);
        Assert.Equal(new[] { below }, harness.Host.OpenOverlays);
    }

    /// <summary>The visible consequence: once the modal overlay is removed, the desktop underneath takes clicks again.</summary>
    [Fact]
    public void AfterRemovingAnOpenModalOverlay_TheWindowUnderneathTakesClicksAgain()
    {
        Harness harness = new();
        MGWindow window = new(harness.Desktop, 0, 0, 800, 600) { WindowStyle = WindowStyle.None };
        int clicks = 0;
        MGButton button = new(window, _ => clicks++);
        window.SetContent(button);
        harness.Desktop.Windows.Add(window);
        MGOverlay overlay = harness.OpenOverlay();
        harness.Frame(new Point(1, 1));
        harness.Frame(new Point(1, 1));

        Point onButtonBesideTheOverlay = new(5, 5);
        Assert.False(overlay.ActualLayoutBounds.Contains(onButtonBesideTheOverlay));
        harness.Click(onButtonBesideTheOverlay);
        Assert.Equal(0, clicks); // blocked while the modal overlay is open

        Assert.True(harness.Host.TryRemoveOverlay(overlay));
        harness.Click(onButtonBesideTheOverlay);

        Assert.Equal(1, clicks);
    }

    [Fact]
    public void ACancelledClose_KeepsTheOverlay_OpenAndActive()
    {
        Harness harness = new();
        MGOverlay overlay = harness.OpenOverlay();
        overlay.OnClosing += (_, args) => args.Cancel = true;
        harness.Frame(new Point(1, 1));

        Assert.False(harness.Host.TryRemoveOverlay(overlay));

        Assert.Contains(overlay, harness.Host.Overlays);
        Assert.Contains(overlay, harness.Host.OpenOverlays);
        Assert.Same(overlay, harness.Host.ActiveOverlay);
    }

    [Fact]
    public void RemovingAClosedOverlay_RaisesNoCloseEvent()
    {
        Harness harness = new();
        MGOverlay overlay = harness.Host.AddOverlay(new MGButton(harness.Host.ParentWindow));
        int closing = 0, closed = 0;
        overlay.OnClosing += (_, _) => closing++;
        overlay.OnClosed += (_, _) => closed++;

        Assert.True(harness.Host.TryRemoveOverlay(overlay));

        Assert.DoesNotContain(overlay, harness.Host.Overlays);
        Assert.Equal(0, closing);
        Assert.Equal(0, closed);
        Assert.Null(harness.Host.ActiveOverlay);
    }
}
