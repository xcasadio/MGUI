using System;
using System.IO;
using System.Security.Cryptography;
using FontStashSharp;
using MGUI.Backend.MonoGame;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.FontStashSharp;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Xunit;
using Xunit.Abstractions;

namespace MGUI.Tests.Integration;

/// <summary>
/// Real-GPU proof of the one draw transaction that a <see cref="MainRenderer"/> owns (ADR-0032): a desktop frame drawn by <see cref="MGDesktop.Draw(float, DrawSettings)"/>,
/// which asks the renderer for its transaction and disposes it, allocates nothing, draws the pixels that a fresh transaction draws, and the transaction
/// survives a second request, a draw that throws, a dispose that throws and a begin that throws. The fixture is the <c>Full</c> one of
/// <see cref="MGUI.Tests.Architecture.DesktopFrameAllocationMeasurements"/>: the default window (title bar) holding an <see cref="MGStackPanel"/> with an
/// <see cref="MGTextBlock"/> and an <see cref="MGSlider"/>, drawn with the FontStashSharp text engine. All GPU work goes through <see cref="GpuDeviceHost"/>.<para/>
/// A renderer cannot give a fresh transaction any more, so the reference pixels are those of the same desktop, in the same state, drawn through the
/// transaction of a new <see cref="MainRenderer"/> built for the occasion (see <see cref="DrawWithAFreshRenderer"/>).
/// </summary>
[Collection(GpuDeviceCollection.Name)]
public class MainRendererDrawTransactionGpuTests
{
    private const int TargetWidth = 640;
    private const int TargetHeight = 360;
    private const int WarmupIterations = 500;
    private const int MeasuredIterations = 500;

    private readonly ITestOutputHelper _output;

    public MainRendererDrawTransactionGpuTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>A render host that raises its update events when the test says so, with the mouse state the test sets: <see cref="HeadlessGame"/> never raises its own.</summary>
    private sealed class TestRenderHost : IRenderHost, IRawInputSource
    {
        private readonly HeadlessGame _game;
        private MouseState _mouse;

        public TestRenderHost(HeadlessGame game)
        {
            _game = game;
        }

        public GraphicsDevice GraphicsDevice => _game.GraphicsDevice;
        public Rectangle GetBounds() => new(0, 0, TargetWidth, TargetHeight);
        public object GetService(Type serviceType) => _game.Services.GetService(serviceType);
        public MouseState GetMouseState() => _mouse;
        public KeyboardState GetKeyboardState() => default;

        public event EventHandler<TimeSpan> PreviewUpdate;
        public event EventHandler<EventArgs> EndUpdate;

        public void RaiseFrameStart(MouseState mouse, TimeSpan elapsed)
        {
            _mouse = mouse;
            PreviewUpdate?.Invoke(this, elapsed);
        }

        public void RaiseFrameEnd() => EndUpdate?.Invoke(this, EventArgs.Empty);
    }

    private static MainRenderer CreateRenderer()
    {
        TestRenderHost host = new(GpuDeviceHost.Instance.Game);
        return MonoGameBackendBootstrap.Create(host, host).Renderer;
    }

    /// <summary>The fixture and its renderer. A frame is the real path, <see cref="MGDesktop.Draw(float, DrawSettings)"/>, which asks the renderer for its
    /// transaction and disposes it.</summary>
    private sealed class Harness
    {
        private readonly TestRenderHost _host;
        private int _elapsedMilliseconds;

        public MainRenderer Renderer { get; }
        public MGDesktop Desktop { get; }
        public MGSlider Slider { get; }

        public Harness()
        {
            _host = new TestRenderHost(GpuDeviceHost.Instance.Game);
            Renderer = MonoGameBackendBootstrap.Create(_host, _host).Renderer;
            Desktop = new MGDesktop(Renderer);

            byte[] ttfData = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fonts", "arial.ttf"));
            FontStashSharpTextEngine engine = new();
            FontSystem fontSystem = new();
            fontSystem.AddFont(ttfData);
            engine.AddFontSystem(Renderer.DefaultFontFamily, CustomFontStyles.Normal, fontSystem, ttfData);
            engine.MatchSpriteFontSizing(Renderer.FontManager);
            Desktop.TextEngine = engine;

            MGWindow window = new(Desktop, 0, 0, 300, 200);
            Desktop.Windows.Add(window);
            MGStackPanel panel = new(window, Orientation.Vertical);
            panel.TryAddChild(new MGTextBlock(window, "Value"));
            //  "F0" on [0, 1]: every value below 0.5 shows "0", so the drag does not change the label text.
            Slider = new MGSlider(window, 0, 1, 0.5f) { ShowValueLabel = true, ValueLabelFormat = "F0" };
            panel.TryAddChild(Slider);
            window.SetContent(panel);

            Frame(Outside, ButtonState.Released);
            Frame(Outside, ButtonState.Released);
        }

        public static Point Outside => new(600, 340);

        public void Update(Point position, ButtonState left)
        {
            MouseState mouse = new(position.X, position.Y, 0, left, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            _host.RaiseFrameStart(mouse, TimeSpan.FromMilliseconds(_elapsedMilliseconds));
            Desktop.Update();
            _host.RaiseFrameEnd();
            _elapsedMilliseconds += 16;
        }

        public void Draw() => Desktop.Draw();

        public void Frame(Point position, ButtonState left)
        {
            Update(position, left);
            Draw();
        }

        /// <summary>Presses on the thumb, moves to two positions along the track, and leaves the slider dragging.</summary>
        public (Point First, Point Second) StartDrag()
        {
            Rectangle bounds = Slider.LayoutBounds;
            Point thumb = Slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, bounds.Center);
            Point first = Slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 5, bounds.Center.Y));
            Point second = Slider.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, new Point(bounds.Left + bounds.Width / 4, bounds.Center.Y));
            Frame(thumb, ButtonState.Released);
            Frame(thumb, ButtonState.Released);
            Frame(thumb, ButtonState.Pressed);
            Frame(first, ButtonState.Pressed);
            Frame(second, ButtonState.Pressed);
            Assert.True(Slider.IsDraggingThumb);
            return (first, second);
        }
    }

    /// <summary>Total bytes allocated by <see cref="MeasuredIterations"/> calls, after a warm-up: the exact total, not divided.</summary>
    private static long MeasureTotal(Action action)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            action();
        }

        long before = AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            action();
        }

        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>Total bytes allocated by the updates and by the draws of <see cref="MeasuredIterations"/> frames, after a warm-up. Each frame
    /// updates then draws, as a game does: a draw measured alone would draw the same state again and again.</summary>
    private static (long Update, long Draw) MeasureFrames(Action update, Action draw)
    {
        for (int i = 0; i < WarmupIterations; i++)
        {
            update();
            draw();
        }

        long updateBytes = 0;
        long drawBytes = 0;
        AllocationWindow.Start();
        for (int i = 0; i < MeasuredIterations; i++)
        {
            long a = GC.GetAllocatedBytesForCurrentThread();
            update();
            long b = GC.GetAllocatedBytesForCurrentThread();
            draw();
            updateBytes += b - a;
            drawBytes += GC.GetAllocatedBytesForCurrentThread() - b;
        }

        return (updateBytes, drawBytes);
    }

    private static RenderTarget2D CreateTarget(GraphicsDevice device)
    {
        RenderTarget2D target = new(device, TargetWidth, TargetHeight, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
        device.SetRenderTarget(target);
        ClearTarget(device);
        return target;
    }

    private static void ClearTarget(GraphicsDevice device)
        => device.Clear(ClearOptions.Target | ClearOptions.Stencil | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);

    private static string PixelsOf(RenderTarget2D target, GraphicsDevice device)
    {
        device.SetRenderTarget(null);
        Color[] pixels = new Color[target.Width * target.Height];
        target.GetData(pixels);
        byte[] bytes = new byte[pixels.Length * 4];
        for (int i = 0; i < pixels.Length; i++)
        {
            bytes[i * 4] = pixels[i].R;
            bytes[i * 4 + 1] = pixels[i].G;
            bytes[i * 4 + 2] = pixels[i].B;
            bytes[i * 4 + 3] = pixels[i].A;
        }

        return $"{bytes.Length}:{Convert.ToHexString(SHA256.HashData(bytes))}";
    }

    /// <summary>The hash of the pixels of a target, which is the render target of the device again afterwards.</summary>
    private static string HashOf(RenderTarget2D target, GraphicsDevice device)
    {
        string hash = PixelsOf(target, device);
        device.SetRenderTarget(target);
        return hash;
    }

    /// <summary>Draws the desktop of <paramref name="harness"/> in its present state on a new target, through the transaction of a <see cref="MainRenderer"/>
    /// built for this draw, and returns the hash of the pixels. This is the reference of "a fresh transaction per frame": the desktop is not updated nor
    /// drawn by its own renderer, so its state is the one the harness is in. The new renderer draws its text with the engine of the desktop.</summary>
    private static string DrawWithAFreshRenderer(Harness harness, GraphicsDevice device)
    {
        using RenderTarget2D target = CreateTarget(device);
        MainRenderer fresh = CreateRenderer();
        fresh.TextEngine = harness.Desktop.TextEngine;
        using (IUIDrawTransaction transaction = fresh.BeginDrawTransaction(DrawSettings.Default, false))
        {
            harness.Desktop.Draw(transaction);
        }

        return PixelsOf(target, device);
    }

    private sealed record DeviceState(
        RasterizerState Rasterizer, BlendState Blend, DepthStencilState DepthStencil, SamplerState Sampler, Rectangle Scissor, Viewport View, RenderTarget2D Target)
    {
        public static DeviceState Of(GraphicsDevice device)
        {
            RenderTargetBinding[] targets = device.GetRenderTargets();
            return new DeviceState(device.RasterizerState, device.BlendState, device.DepthStencilState, device.SamplerStates[0], device.ScissorRectangle,
                device.Viewport, targets.Length > 0 ? (RenderTarget2D)targets[0].RenderTarget : null);
        }
    }

    /// <summary>The real path: <c>MGDesktop.Update()</c> then <c>MGDesktop.Draw()</c>, which asks the renderer for its transaction. At rest, nothing
    /// is allocated by the update or by the draw, the total over the measured frames being exactly zero, and the frames do draw.</summary>
    [GpuFact]
    public void TheRendererFrame_AtRest_AllocatesNothing()
    {
        (long update, long draw, bool drew) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new();
                using RenderTarget2D target = CreateTarget(device);
                string blank = HashOf(target, device);
                long updateBytes = MeasureTotal(() => harness.Update(Harness.Outside, ButtonState.Released));
                long drawBytes = MeasureTotal(() => harness.Draw());
                return (updateBytes, drawBytes, blank != HashOf(target, device));
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        _output.WriteLine($"renderer frame, idle: Update {update} B, Draw {draw} B over {MeasuredIterations} frames");
        Assert.True(drew);
        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    [GpuFact]
    public void TheRendererFrame_DuringADrag_AllocatesNothing()
    {
        (long update, long draw, bool dragging, bool drew) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new();
                using RenderTarget2D target = CreateTarget(device);
                string blank = HashOf(target, device);
                (Point first, Point second) = harness.StartDrag();
                int frame = 0;
                (long updateBytes, long drawBytes) = MeasureFrames(
                    () => harness.Update(frame++ % 2 == 0 ? first : second, ButtonState.Pressed),
                    () => harness.Draw());
                return (updateBytes, drawBytes, harness.Slider.IsDraggingThumb, blank != HashOf(target, device));
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        _output.WriteLine($"renderer frame, drag: Update {update} B, Draw {draw} B over {MeasuredIterations} frames");
        Assert.True(dragging);
        Assert.True(drew);
        Assert.Equal(0, update);
        Assert.Equal(0, draw);
    }

    /// <summary>The frames of the real path, which reuse one transaction, draw the pixels that the same desktop draws through the transaction of a new
    /// renderer: at rest, and during a drag (the frames are different, and each one is compared).</summary>
    [GpuFact]
    public void TheRendererFrame_DrawsTheSamePixelsAsWithAFreshTransactionPerFrame()
    {
        (string rest, string restFresh, string drag, string dragFresh) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new();

                //  The transaction of the renderer was created by the first frame and re-armed by the next ones.
                using RenderTarget2D restTarget = CreateTarget(device);
                harness.Draw();
                string restPixels = PixelsOf(restTarget, device);
                string restReference = DrawWithAFreshRenderer(harness, device);

                (Point first, Point second) = harness.StartDrag();
                harness.Frame(first, ButtonState.Pressed);
                using RenderTarget2D dragTarget = CreateTarget(device);
                harness.Frame(second, ButtonState.Pressed);
                string dragPixels = PixelsOf(dragTarget, device);
                string dragReference = DrawWithAFreshRenderer(harness, device);
                return (restPixels, restReference, dragPixels, dragReference);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.Equal(restFresh, rest);
        Assert.Equal(dragFresh, drag);
        Assert.NotEqual(rest, drag);
    }

    /// <summary>A second request while the transaction is open is a programming error: it throws, with a message that says what to do, and it
    /// changes nothing: the device is as it was, and the first transaction draws the reference pixels.</summary>
    [GpuFact]
    public void ASecondRequestWhileTheTransactionIsOpen_Throws_AndTheFirstStaysUsable()
    {
        (string pixels, string reference, DeviceState before, DeviceState after, string message, string deferredMessage) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new();
                string referencePixels = DrawWithAFreshRenderer(harness, device);
                using RenderTarget2D target = CreateTarget(device);
                IUIDrawTransaction first = harness.Renderer.BeginDrawTransaction(DrawSettings.Default, false);
                DeviceState stateBefore = DeviceState.Of(device);
                InvalidOperationException request = Assert.Throws<InvalidOperationException>(
                    () => harness.Renderer.BeginDrawTransaction(DrawSettings.Default, false));
                InvalidOperationException deferredRequest = Assert.Throws<InvalidOperationException>(
                    () => harness.Renderer.BeginDrawTransaction(DrawSettings.Default, true));
                DeviceState stateAfter = DeviceState.Of(device);

                harness.Desktop.Draw(first);
                first.Dispose();
                return (PixelsOf(target, device), referencePixels, stateBefore, stateAfter, request.Message, deferredRequest.Message);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.Equal(before, after);
        Assert.Contains("dispose", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("already open", message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(message, deferredMessage);
        Assert.Equal(reference, pixels);
    }

    /// <summary>After <c>Dispose</c>, the next request returns the same object, in the state of a new transaction.</summary>
    [GpuFact]
    public void AfterDispose_TheNextRequestReturnsTheSameObject_Rearmed()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new();
                using RenderTarget2D target = CreateTarget(device);
                DrawSettings changed = DrawSettings.Default with { BlendType = BlendType.Opaque };

                DrawTransaction first = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, false);
                first.SetDrawSettings(changed);
                using (first.PushRectangleClip(new Rectangle(10, 10, 50, 50), true))
                {
                    first.DrawTextureTo(first.WhitePixel, null, new Rectangle(0, 0, 100, 100));
                }

                Assert.True(first.ClipDiagnostics.ScissorClipCount > 0);
                Assert.NotNull(first.PreviousSettings);
                first.Dispose();

                DrawSettings initial = DrawSettings.Default with { SamplerType = SamplerType.LinearClamp };
                DrawTransaction second = (DrawTransaction)harness.Renderer.BeginDrawTransaction(initial, true);
                Assert.Same(first, second);
                Assert.Same(initial, second.CurrentSettings);
                //  A new transaction has no previous settings.
                Assert.Null(second.PreviousSettings);
                Assert.Equal(0, second.ClipDiagnostics.ScissorClipCount);
                second.Dispose();

                DrawTransaction third = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, false);
                Assert.Same(first, third);
                third.Dispose();
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });
    }

    /// <summary>A change of the viewport between two frames: the projection of the primitives follows it, as it does for a renderer that never
    /// drew at the first size. The frames draw the desktop, then a triangle through the primitive batch, which is what the projection is for.</summary>
    [GpuFact]
    public void AViewportChangeBetweenTwoFrames_DrawsThePixelsOfAFreshRenderer()
    {
        (string reused, string fresh, string blank) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                using RenderTarget2D large = CreateTarget(device);
                using RenderTarget2D small = new(device, TargetWidth / 2, TargetHeight / 2, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);

                //  Drew at the size of the large target (two frames of the constructor), then draws at the size of the small one.
                Harness reusedHarness = new();
                device.SetRenderTarget(small);
                ClearTarget(device);
                string blankPixels = HashOf(small, device);
                //  A render target that is bound again loses its contents: cleared again.
                ClearTarget(device);
                FrameWithATriangle(reusedHarness);
                string reusedPixels = PixelsOf(small, device);

                //  Never drew at another size.
                device.SetRenderTarget(small);
                Harness freshHarness = new();
                ClearTarget(device);
                FrameWithATriangle(freshHarness);
                return (reusedPixels, PixelsOf(small, device), blankPixels);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.NotEqual(blank, reused);
        Assert.Equal(fresh, reused);
    }

    private static void FrameWithATriangle(Harness harness)
    {
        harness.Update(Harness.Outside, ButtonState.Released);
        using DrawTransaction transaction = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, false);
        harness.Desktop.Draw(transaction);
        transaction.FillTriangle(Vector2.Zero, new Vector2(150, 100), Color.Red, new Vector2(300, 100), Color.Lime, new Vector2(150, 160), Color.Blue);
    }

    /// <summary>A draw that throws inside the transaction of the renderer: after <c>Dispose</c> the next request returns the same object, and the
    /// next frame draws the reference pixels.</summary>
    [GpuFact]
    public void ADrawThatThrows_LeavesTheTransactionUsableAfterDispose()
    {
        (bool same, string pixels, string reference) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new();
                string referencePixels = DrawWithAFreshRenderer(harness, device);
                DrawTransaction known = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, true);
                known.Dispose();

                using RenderTarget2D target = CreateTarget(device);
                DrawTransaction failing = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, false);
                failing.DrawTextureTo(failing.WhitePixel, null, new Rectangle(0, 0, 64, 64));
                Assert.Throws<ArgumentNullException>(() => failing.DrawTextureTo((Texture2D)null, null, new Rectangle(0, 0, 8, 8)));
                failing.Dispose();

                DrawTransaction next = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, true);
                bool sameObject = ReferenceEquals(known, failing) && ReferenceEquals(known, next);
                next.Dispose();

                ClearTarget(device);
                harness.Draw();
                return (sameObject, PixelsOf(target, device), referencePixels);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.True(same);
        Assert.Equal(reference, pixels);
    }

    /// <summary>A <c>Dispose</c> that throws itself (the sprite batch of the renderer was ended by someone else while the transaction held it open,
    /// so the end of its batch throws): the transaction is closed all the same, the next request returns the same object, and the next frame draws the
    /// reference pixels.</summary>
    [GpuFact]
    public void ADisposeThatThrows_LeavesTheTransactionUsableAfterwards()
    {
        (bool disposeThrew, bool same, string pixels, string reference) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                Harness harness = new();
                string referencePixels = DrawWithAFreshRenderer(harness, device);
                DrawTransaction known = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, true);
                known.Dispose();

                using RenderTarget2D target = CreateTarget(device);
                DrawTransaction failing = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, false);
                failing.DrawTextureTo(failing.WhitePixel, null, new Rectangle(0, 0, 64, 64));
                harness.Renderer.SpriteBatch.End();
                bool threw = false;
                try
                {
                    failing.Dispose();
                }
                catch (Exception)
                {
                    threw = true;
                }

                DrawTransaction next = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, true);
                bool sameObject = ReferenceEquals(known, failing) && ReferenceEquals(known, next);
                next.Dispose();

                ClearTarget(device);
                harness.Draw();
                return (threw, sameObject, PixelsOf(target, device), referencePixels);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.True(disposeThrew);
        Assert.True(same);
        Assert.Equal(reference, pixels);
    }

    /// <summary>A request that begins the batch and whose begin throws (the sprite batch of the renderer was left begun) leaves the transaction
    /// closed: the request that follows succeeds, for the first request of a renderer and for the later ones.</summary>
    [GpuFact]
    public void ARequestWhoseBeginThrows_LeavesTheTransactionClosed()
    {
        (bool sameAfterFirst, bool sameAfterLater, string pixels, string reference) = GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            try
            {
                //  The first request of a renderer.
                MainRenderer fresh = CreateRenderer();
                fresh.SpriteBatch.Begin();
                Exception first = Assert.ThrowsAny<Exception>(() => fresh.BeginDrawTransaction(DrawSettings.Default, false));
                Assert.DoesNotContain("already open", first.Message, StringComparison.OrdinalIgnoreCase);
                fresh.SpriteBatch.End();
                IUIDrawTransaction afterFirst = fresh.BeginDrawTransaction(DrawSettings.Default, true);
                afterFirst.Dispose();
                IUIDrawTransaction again = fresh.BeginDrawTransaction(DrawSettings.Default, false);
                bool sameFirst = ReferenceEquals(afterFirst, again);
                again.Dispose();

                //  A later request.
                Harness harness = new();
                string referencePixels = DrawWithAFreshRenderer(harness, device);
                IUIDrawTransaction known = harness.Renderer.BeginDrawTransaction(DrawSettings.Default, true);
                known.Dispose();
                harness.Renderer.SpriteBatch.Begin();
                Exception later = Assert.ThrowsAny<Exception>(() => harness.Renderer.BeginDrawTransaction(DrawSettings.Default, false));
                Assert.DoesNotContain("already open", later.Message, StringComparison.OrdinalIgnoreCase);
                harness.Renderer.SpriteBatch.End();

                using RenderTarget2D target = CreateTarget(device);
                IUIDrawTransaction next = harness.Renderer.BeginDrawTransaction(DrawSettings.Default, true);
                bool sameLater = ReferenceEquals(known, next);
                next.Dispose();
                harness.Draw();
                return (sameFirst, sameLater, PixelsOf(target, device), referencePixels);
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });

        Assert.True(sameAfterFirst);
        Assert.True(sameAfterLater);
        Assert.Equal(reference, pixels);
    }

    /// <summary>The temporary changes and the clips that a use leaves open are forgotten by the next use: their handles, disposed after the
    /// transaction was re-armed, restore nothing (ADR-0031, ADR-0032). The settings of the next use are the ones it was opened with, and the
    /// scissor rectangle and the render target of the device are what the test left them.</summary>
    [GpuFact]
    public void TemporaryChangesAndClipsLeftOpen_AreForgottenByTheNextUse()
    {
        GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;
            using RenderTarget2D target = CreateTarget(device);
            using RenderTarget2D other = new(device, 64, 64, false, SurfaceFormat.Color, DepthFormat.Depth24Stencil8);
            try
            {
                Harness harness = new();
                device.SetRenderTarget(target);

                //  First use: three changes that are never disposed by it.
                DrawTransaction first = (DrawTransaction)harness.Renderer.BeginDrawTransaction(DrawSettings.Default, false);
                IUIRenderContext context = first;
                DrawStateHandle staleSettings = context.SetDrawSettingsTemporary(first.CurrentSettings with { BlendType = BlendType.Opaque });
                DrawStateHandle staleTarget = first.SetRenderTargetTemporary(other, null);
                ClipHandle staleClip = first.PushRectangleClip(new Rectangle(10, 10, 50, 50), true);
                Assert.True(staleClip.IsActive);
                Assert.Same(other, device.GetRenderTargets()[0].RenderTarget);

                //  Another render target was bound by someone else, then the transaction was disposed.
                device.SetRenderTarget(target);
                first.Dispose();

                DrawSettings initial = DrawSettings.Default with { SamplerType = SamplerType.LinearClamp };
                DrawTransaction second = (DrawTransaction)harness.Renderer.BeginDrawTransaction(initial, true);
                Assert.Same(first, second);
                Assert.False(staleClip.IsActive);
                Rectangle scissor = new(5, 5, 100, 100);
                device.ScissorRectangle = scissor;
                DrawSettings settingsBefore = second.CurrentSettings;
                Assert.Same(initial, settingsBefore);

                staleClip.Dispose();
                staleSettings.Dispose();
                staleTarget.Dispose();

                Assert.Same(initial, second.CurrentSettings);
                Assert.Null(second.PreviousSettings);
                Assert.Equal(scissor, device.ScissorRectangle);
                Assert.Same(target, device.GetRenderTargets()[0].RenderTarget);
                Assert.False(staleClip.IsActive);

                //  The next use is as usable as a new one: a clip of its own is active, and restores.
                using (ClipHandle clip = second.PushRectangleClip(new Rectangle(20, 20, 30, 30), true))
                {
                    Assert.True(clip.IsActive);
                    Assert.False(staleClip.IsActive);
                }

                Assert.Equal(scissor, device.ScissorRectangle);
                second.Dispose();
            }
            finally
            {
                device.SetRenderTarget(null);
            }
        });
    }
}
