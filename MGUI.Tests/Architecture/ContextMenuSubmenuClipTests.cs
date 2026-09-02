namespace MGUI.Tests.Architecture;

using MGUI.Core.UI;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

/// <summary>Covers the MGContextMenu submenu clip ownership (Docs/rendering-architecture.md): when <see cref="MGContextMenu"/> draws its currently
/// active submenu overtop of the host content (see MGContextMenu.cs, the two <c>OnEndDraw</c> wirings around
/// line 860), it must actually escape the ambient clip that is active while the host window/desktop draws -
/// via <c>PushRectangleClip(null, false)</c> - rather than leaving it in place.<para/>
/// This drives the real <see cref="ClipStrategyResolver"/> so a regression back to the no-op
/// <c>PushClipTemporary(ClipDefinition.None(...))</c> shim (which <see cref="ClipStrategyResolver"/> resolves to
/// <see cref="ClipStrategy.None"/>, a pure no-op that never touches the active clip) is caught: the assertion
/// requires an ambient clip to be active immediately before the submenu's clip push, and gone immediately after.<para/>
/// Covers both wirings: a root <see cref="MGContextMenu"/> hosted by a regular <see cref="MGWindow"/> (hooks
/// <c>ParentWindow.OnEndDraw</c>), and a root-level <see cref="MGContextMenu"/> with no host window (hooks its own
/// <c>OnEndDraw</c>).</summary>
public class ContextMenuSubmenuClipTests
{
    [Fact]
    public void DrawingSubmenu_OfContextMenuHostedByRegularWindow_EscapesActiveAmbientClip()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGWindow window = new(desktop, 0, 0, 800, 600)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        desktop.Windows.Add(window);

        MGContextMenu menu = new(window, "");
        menu.AddButton("Item A", _ => { });
        MGContextMenu submenu = new(menu);
        submenu.AddButton("Sub Item", _ => { });

        AdvanceFrame(runtime, desktop, 0);

        Assert.True(desktop.TryOpenContextMenu(menu, new Point(50, 50)));
        Assert.True(submenu.TryOpenContextMenu(new Point(60, 60)));
        Assert.Same(submenu, menu.ActiveContextMenu);

        AdvanceFrame(runtime, desktop, 16);

        RecordingClipDrawTransaction transaction = new(runtime);
        desktop.Draw(transaction, 1.0f);

        Assert.Contains(transaction.EscapeClipPushes, IsGenuineAmbientClipEscape);
    }

    [Fact]
    public void DrawingSubmenu_OfRootLevelContextMenuWithNoHostWindow_EscapesActiveAmbientClip()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);

        MGContextMenu menu = new(desktop, "");
        menu.AddButton("Item A", _ => { });
        MGContextMenu submenu = new(menu);
        submenu.AddButton("Sub Item", _ => { });

        AdvanceFrame(runtime, desktop, 0);

        Assert.True(desktop.TryOpenContextMenu(menu, new Point(50, 50)));
        Assert.True(submenu.TryOpenContextMenu(new Point(60, 60)));
        Assert.Same(submenu, menu.ActiveContextMenu);

        AdvanceFrame(runtime, desktop, 16);

        RecordingClipDrawTransaction transaction = new(runtime);
        desktop.Draw(transaction, 1.0f);

        Assert.Contains(transaction.EscapeClipPushes, IsGenuineAmbientClipEscape);
    }

    /// <summary>True only for a <c>PushRectangleClip(null, false)</c> call that (a) fired while an ambient clip was
    /// actually active (proving there was something to escape - matches <c>MGDesktop.Draw</c>'s
    /// <c>PushRectangleClip(ScreenBounds, true)</c> wrapper) and (b) left the clip cleared immediately afterwards
    /// (proving the push was a genuine escape and not a no-op).</summary>
    private static bool IsGenuineAmbientClipEscape(RecordingClipDrawTransaction.RectangleClipPush push)
        => push.Bounds is null && !push.IntersectWithCurrentClipTarget && push.WasClipActiveBeforePush && !push.WasClipActiveAfterPush;

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(totalElapsedMs), TimeSpan.FromMilliseconds(16), default, default));
        desktop.Update();
    }

    /// <summary>Minimal <see cref="IUIDrawTransaction"/> that records every <see cref="ClipDefinition"/> pushed via
    /// <see cref="PushClipTemporary(ClipDefinition)"/> and every <see cref="PushRectangleClip(Rectangle?, bool)"/> call;
    /// every other draw call is a no-op. Mirrors the real backend's two distinct code paths (see
    /// <c>DrawTransaction.PushRectangleClip</c> and <c>ClipManager.Push</c> in
    /// MGUI.MonoGame.Integration/Rendering): a null-bounds <see cref="PushRectangleClip(Rectangle?, bool)"/> call
    /// imperatively clears the active clip, while a <see cref="PushClipTemporary(ClipDefinition)"/> call is resolved
    /// through the real <see cref="ClipStrategyResolver"/> - <see cref="ClipKind.None"/> resolves to
    /// <see cref="ClipStrategy.None"/>, a true no-op that leaves the active clip untouched. Tracks
    /// <see cref="CurrentClipBounds"/> the same way as <see cref="GraphNoOpDrawTransaction"/> so that nested
    /// elements' clip-intersection checks behave normally.</summary>
    private sealed class RecordingClipDrawTransaction : IUIDrawTransaction
    {
        private Rectangle? _currentClipBounds;

        public readonly record struct RectangleClipPush(Rectangle? Bounds, bool IntersectWithCurrentClipTarget,
            bool WasClipActiveBeforePush, bool WasClipActiveAfterPush);

        public List<ClipDefinition> PushedClips { get; } = new();
        public List<RectangleClipPush> EscapeClipPushes { get; } = new();
        public DrawSettings CurrentSettings { get; private set; } = DrawSettings.Default;
        public IUIDesktopRuntime Renderer { get; }
        public Rectangle? CurrentClipBounds => _currentClipBounds;

        public RecordingClipDrawTransaction(IUIDesktopRuntime renderer) => Renderer = renderer;

        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask) { }
        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None) { }
        public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None) { }
        public void DrawTextViaEngine(ResolvedFont Font, string Text, Vector2 Position, Color Color, Vector2 Origin, float Scale,
            float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None) { }
        public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color) { }
        public void FillPoint(Vector2 Center, Color Color, float Width) { }
        public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness) { }
        public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness) { }
        public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color) { }
        public void StrokeAndFillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f) { }
        public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2) { }
        public void DrawTexturedTriangleList(Vector2 Origin, IUIImageResource Texture, IReadOnlyList<Vector2> Vertices, IReadOnlyList<Vector2> TextureCoordinates,
            IReadOnlyList<int> Indices, Color ColorMask) { }
        public void FillQuadrilateralLinearClamp(Vector2 Origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
            Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor) { }
        public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f) { }
        public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32) { }
        public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32) { }
        public void StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness = 1.0f, int NumSides = 32) { }

        public void SetDrawSettings(DrawSettings Settings) => CurrentSettings = Settings ?? DrawSettings.Default;

        public IDisposable SetDrawSettingsTemporary(DrawSettings Settings)
        {
            DrawSettings previous = CurrentSettings;
            SetDrawSettings(Settings);
            return new DisposableAction(() => CurrentSettings = previous);
        }

        public IDisposable SetRenderTargetTemporary(IUIRenderTarget New, Color? ClearColor) => new DisposableAction(() => { });

        public IDisposable SetTransformTemporary(Matrix Transform) => SetDrawSettingsTemporary(CurrentSettings with { Transform = Transform });

        public ClipResolveResult ResolveClip(ClipDefinition Definition)
            => ClipStrategyResolver.Resolve(Definition ?? ClipDefinition.None(), ClipBackendCapabilities.Default);

        public ClipScope PushClipTemporary(ClipDefinition Definition)
        {
            PushedClips.Add(Definition);
            ClipResolveResult resolution = ResolveClip(Definition);
            Rectangle? previous = _currentClipBounds;

            //  Mirrors ClipManager.Push: ClipStrategy.None is a true no-op, it does not touch the active clip.
            if (resolution.Strategy != ClipStrategy.None)
            {
                _currentClipBounds = resolution.Effective.Shape.Bounds;
            }

            return new(resolution, () => _currentClipBounds = previous);
        }

        public ClipScope PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
        {
            if (!Bounds.HasValue)
            {
                //  Mirrors DrawTransaction.PushRectangleClip's null-bounds branch: it imperatively clears the
                //  active clip via SetClipTarget(null, false), bypassing PushClipTemporary/ClipManager entirely.
                bool wasActiveBefore = _currentClipBounds.HasValue;
                Rectangle? previous = _currentClipBounds;
                _currentClipBounds = null;
                EscapeClipPushes.Add(new RectangleClipPush(null, IntersectWithCurrentClipTarget, wasActiveBefore, _currentClipBounds.HasValue));

                ClipDefinition requested = ClipDefinition.None(false);
                ClipResolveResult resolution = new(requested, requested, ClipStrategy.None, false);
                return new(resolution, () => _currentClipBounds = previous);
            }

            ClipDefinition definition = ClipDefinition.Rectangle(Bounds.Value, IntersectWithCurrentClipTarget);
            return PushClipTemporary(definition);
        }

        public IDisposable SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
            => PushRectangleClip(Bounds, IntersectWithCurrentClipTarget);

        public void Dispose() { }
    }

    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _disposeAction;
        private bool _disposed;

        public DisposableAction(Action disposeAction) => _disposeAction = disposeAction;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _disposeAction();
        }
    }
}
