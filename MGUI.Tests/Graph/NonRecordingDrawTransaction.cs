using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Graph;

/// <summary>
/// A draw transaction for allocation measurements: it records nothing and allocates nothing of its own, so the bytes counted around a
/// draw belong to the code under test.<para/>
/// Trade-off: it does not track the clip or the draw settings. <see cref="CurrentClipBounds"/> is always null, and every temporary scope
/// is one shared object whose disposal does nothing (<see cref="ClipScope"/> is a class that runs its action once, so a scope that restored
/// a clip could not be reused). Use <see cref="GraphNoOpDrawTransaction"/> when a test needs the calls or the clip.
/// </summary>
internal sealed class NonRecordingDrawTransaction : IUIDrawTransaction
{
    private static readonly IDisposable NoOpScope = new GraphDisposableAction(static () => { });
    private static readonly ClipDefinition NoClip = ClipDefinition.None();
    private readonly ClipScope SharedClipScope = new(new ClipResolveResult(NoClip, NoClip, ClipStrategy.Scissor, false), static () => { });

    public DrawSettings CurrentSettings { get; private set; }
    public IUIDesktopRuntime Renderer { get; }
    public Rectangle? CurrentClipBounds => null;

    /// <summary>Calls received since the transaction was created: proof that a measured draw did draw something.</summary>
    public long CallCount { get; private set; }

    public NonRecordingDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
    {
        Renderer = renderer;
        CurrentSettings = settings;
    }

    public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask) => CallCount++;

    public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None) => CallCount++;

    public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None) => CallCount++;

    public void DrawTextViaEngine(ResolvedFont Font, string Text, Vector2 Position, Color Color, Vector2 Origin, float Scale,
        float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None) => CallCount++;

    public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color) => CallCount++;

    public void FillPoint(Vector2 Center, Color Color, float Width) => CallCount++;

    public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness) => CallCount++;

    public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness) => CallCount++;

    public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color) => CallCount++;

    public void StrokeAndFillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f) => CallCount++;

    public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2) => CallCount++;

    public void DrawTexturedTriangleList(Vector2 Origin, IUIImageResource Texture, IReadOnlyList<Vector2> Vertices, IReadOnlyList<Vector2> TextureCoordinates,
        IReadOnlyList<int> Indices, Color ColorMask) => CallCount++;

    public void FillQuadrilateralLinearClamp(Vector2 Origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
        Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor) => CallCount++;

    public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f) => CallCount++;

    public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32) => CallCount++;

    public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32) => CallCount++;

    public void StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness = 1.0f, int NumSides = 32) => CallCount++;

    public void SetDrawSettings(DrawSettings Settings)
    {
        CurrentSettings = Settings ?? DrawSettings.Default;
    }

    public IDisposable SetDrawSettingsTemporary(DrawSettings Settings) => NoOpScope;

    public IDisposable SetRenderTargetTemporary(IUIRenderTarget New, Color? ClearColor) => NoOpScope;

    public IDisposable SetTransformTemporary(Matrix Transform) => NoOpScope;

    public ClipResolveResult ResolveClip(ClipDefinition Definition)
    {
        ClipDefinition effective = Definition ?? NoClip;
        return new(effective, effective, ClipStrategy.Scissor, false);
    }

    public ClipScope PushClipTemporary(ClipDefinition Definition) => SharedClipScope;

    public ClipScope PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget) => SharedClipScope;

    public IDisposable SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget) => SharedClipScope;

    public void Dispose()
    {
    }
}
