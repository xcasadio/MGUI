namespace MGUI.Tests.Architecture;

using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;

/// <summary>Covers the MGGridColorPicker swatch clip (Docs/rendering-architecture.md): when the host border is rounded
/// (<see cref="MGGridColorPicker.CornerRadius"/> != 0 with a border), <see cref="MGGridColorPicker.DrawSelf"/> must
/// clip the swatch loop (fills, selection/hover overlays, per-swatch borders) to the rounded host silhouette via
/// <see cref="MGElement.CreateBorderBackedContentsClipDefinition"/> - otherwise the rectangular swatches repaint over
/// the rounded corners, because the border ring is painted BeforeSelf (see MGComponent.cs
/// ComponentDrawPriority.BeforeSelf). With CornerRadius zero, no such clip should be pushed at all - zero behavior
/// change for the common case. Drives the real <see cref="ClipStrategyResolver"/> through a real headless
/// MGDesktop/MGWindow draw, mirroring the pattern in <see cref="ContextMenuSubmenuClipTests"/>.</summary>
public class GridColorPickerSwatchClipTests
{
    [Fact]
    public void DrawingSwatches_WithRoundedBorder_ClipsToRoundedHostSilhouette()
    {
        Harness harness = Harness.Create();
        MGGridColorPicker picker = CreateGridColorPicker(harness.Window, cornerRadius: 18);
        harness.Show(picker);

        RecordingClipDrawTransaction transaction = new(harness.Runtime);
        harness.Desktop.Draw(transaction, 1.0f);

        ClipDefinition swatchClip = Assert.Single(transaction.PushedClips, IsSwatchClipDefinition);
        Assert.True(swatchClip.Kind is ClipKind.RoundedRectangle or ClipKind.Rectangle,
            "the swatch clip must be a rounded rectangle, or an explicit rectangle fallback of one.");

        //  Every swatch fill (identified by its color - the rounded border ring paints via FillTriangle, not
        //  FillRectangle, see MGUniformBorderBrush.Draw(..., MGBoxShape, MGBoxGeometry)) happened while the swatch
        //  clip was the active/topmost pushed clip.
        List<RecordingClipDrawTransaction.FillRectangleCall> swatchFills = SwatchFills(transaction);
        Assert.Equal(TestColors.Length, swatchFills.Count);
        Assert.All(swatchFills, call => Assert.True(call.SwatchClipWasActive,
            $"swatch fill for {call.Color} was drawn without the '{swatchClip.DebugName}' clip active"));
    }

    [Fact]
    public void DrawingSwatches_WithZeroCornerRadius_PushesNoSwatchClip()
    {
        Harness harness = Harness.Create();
        MGGridColorPicker picker = CreateGridColorPicker(harness.Window, cornerRadius: 0);
        harness.Show(picker);

        RecordingClipDrawTransaction transaction = new(harness.Runtime);
        harness.Desktop.Draw(transaction, 1.0f);

        Assert.DoesNotContain(transaction.PushedClips, IsSwatchClipDefinition);

        //  Swatches are still painted (zero behavior change) - just without the extra clip scope. With
        //  CornerRadius zero the border ring falls back to its per-side rectangle Draw(...) overload
        //  (MGUniformBorderBrush.Draw(..., Rectangle, Thickness)), which also calls FillRectangle - filtering by
        //  the test's swatch colors (not used by the black border brush) isolates the swatch fills.
        List<RecordingClipDrawTransaction.FillRectangleCall> swatchFills = SwatchFills(transaction);
        Assert.Equal(TestColors.Length, swatchFills.Count);
        Assert.All(swatchFills, call => Assert.False(call.SwatchClipWasActive));
    }

    private static readonly Color[] TestColors = { Color.Red, Color.Green, Color.Blue, Color.Yellow };

    private static List<RecordingClipDrawTransaction.FillRectangleCall> SwatchFills(RecordingClipDrawTransaction transaction)
        => transaction.FillRectangleCalls.Where(call => TestColors.Contains(call.Color)).ToList();

    private static bool IsSwatchClipDefinition(ClipDefinition definition)
        => definition.DebugName != null && definition.DebugName.EndsWith(".Swatches", StringComparison.Ordinal);

    private static MGGridColorPicker CreateGridColorPicker(MGWindow window, int cornerRadius)
    {
        MGGridColorPicker picker = new(window, 2, new Size(24, 24), TestColors)
        {
            BorderThickness = new Thickness(4),
            BorderBrush = Color.Black.AsFillBrush().AsUniformBorderBrush(),
            CornerRadius = new MGCornerRadius(cornerRadius),
            ShowSelectedColorLabel = false,
            //  Null out every per-swatch overlay/border so the only DA.DT.FillRectangle(...) calls left in
            //  DrawSelf are the swatch fills themselves - keeps the assertions below unambiguous.
            SelectedColorOverlay = null,
            SelectedColorBorderBrush = null,
            UnselectedColorBorderBrush = null,
            HoveredColorOverlay = null,
        };
        return picker;
    }

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 24, 24, 480, 260)
            {
                WindowStyle = WindowStyle.None,
                Padding = new Thickness(0)
            };
            Harness harness = new(runtime, desktop, window);
            harness.Frame(0);
            return harness;
        }

        /// <summary>Adds the element to the window, shows the window and runs two warm-up frames so layout is settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            Desktop.Windows.Add(Window);
            Frame(0);
            Frame(1);
        }

        public void Frame(int frameIndex)
        {
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), default, default));
            Desktop.Update();
        }
    }

    /// <summary>Minimal <see cref="IUIDrawTransaction"/> that records every <see cref="ClipDefinition"/> pushed via
    /// <see cref="PushClipTemporary(ClipDefinition)"/>, and every <see cref="FillRectangle"/> call together with
    /// whether a clip whose debug name ends with ".Swatches" was active (topmost or ancestor) at the time. Copies the
    /// recording pattern from <see cref="ContextMenuSubmenuClipTests.RecordingClipDrawTransaction"/> (not extracted
    /// to a shared helper, to keep this change confined to the new test file); every draw call other than
    /// <see cref="FillRectangle"/> and clip pushes is a no-op.</summary>
    private sealed class RecordingClipDrawTransaction : IUIDrawTransaction
    {
        private readonly List<ClipDefinition> _clipStack = new();
        private Rectangle? _currentClipBounds;

        public readonly record struct FillRectangleCall(Color Color, bool SwatchClipWasActive);

        public List<ClipDefinition> PushedClips { get; } = new();
        public List<FillRectangleCall> FillRectangleCalls { get; } = new();
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

        public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color)
        {
            bool swatchClipActive = _clipStack.Any(IsSwatchClipDefinition);
            FillRectangleCalls.Add(new(Color, swatchClipActive));
        }

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
            _clipStack.Add(Definition);
            ClipResolveResult resolution = ResolveClip(Definition);
            Rectangle? previousBounds = _currentClipBounds;

            if (resolution.Strategy != ClipStrategy.None)
            {
                _currentClipBounds = resolution.Effective.Shape.Bounds;
            }

            return new(resolution, () =>
            {
                _clipStack.Remove(Definition);
                _currentClipBounds = previousBounds;
            });
        }

        public ClipScope PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
        {
            ClipDefinition definition = Bounds.HasValue
                ? ClipDefinition.Rectangle(Bounds.Value, IntersectWithCurrentClipTarget)
                : ClipDefinition.None(IntersectWithCurrentClipTarget);
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
