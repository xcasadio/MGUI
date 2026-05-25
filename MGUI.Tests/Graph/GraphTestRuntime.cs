using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Graph;

internal sealed class GraphTestRuntime : IUIDesktopRuntime
{
    private ITextMeasurementEngine _textEngine;

    public InputTracker Input { get; } = new();
    public string DefaultFontFamily { get; } = "TestSans";
    public IUISurface Surface { get; }
    public IUIAssetProvider AssetProvider { get; }
    public UpdateBaseArgs UpdateArgs { get; private set; } = new(TimeSpan.Zero, TimeSpan.Zero, default, default);

    public event EventHandler<EventArgs<ITextMeasurementEngine>>? TextEngineChanged;
    public event EventHandler<EventArgs>? EndUpdate
    {
        add { }
        remove { }
    }

    public ITextMeasurementEngine TextEngine
    {
        get => _textEngine;
        set
        {
            ITextMeasurementEngine previous = _textEngine;
            _textEngine = value ?? throw new ArgumentNullException(nameof(value));
            TextEngineChanged?.Invoke(this, new(previous, _textEngine));
        }
    }

    public GraphTestRuntime(Rectangle surfaceBounds)
    {
        Surface = new GraphTestSurface(surfaceBounds, new GraphTestRenderTarget(surfaceBounds.Width, surfaceBounds.Height));
        AssetProvider = new GraphTestAssetProvider();
        _textEngine = new GraphTestTextEngine(DefaultFontFamily);
    }

    public IUIDrawTransaction CreateDrawTransaction(DrawSettings Settings, bool DeferBegin)
        => new GraphNoOpDrawTransaction(this, Settings ?? DrawSettings.Default);

    public void ApplyFrame(UpdateBaseArgs updateArgs)
    {
        UpdateArgs = updateArgs;
        Input.Update(updateArgs);
    }

    public void RegisterView(IUIView View)
    {
    }
}

internal sealed class GraphNoOpDrawTransaction : IUIDrawTransaction
{
    private Rectangle? _currentClipBounds;

    public DrawSettings CurrentSettings { get; private set; }
    public IUIDesktopRuntime Renderer { get; }
    public Rectangle? CurrentClipBounds => _currentClipBounds;
    public List<GraphFillRectangleCall> FillRectangleCalls { get; } = new();
    public List<GraphStrokeAndFillRectangleCall> StrokeAndFillRectangleCalls { get; } = new();
    public List<GraphStrokeLineCall> StrokeLineCalls { get; } = new();
    public List<GraphFillTriangleCall> FillTriangleCalls { get; } = new();
    public List<GraphStrokeAndFillCircleCall> StrokeAndFillCircleCalls { get; } = new();

    public GraphNoOpDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
    {
        Renderer = renderer;
        CurrentSettings = settings;
    }

    public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask) { }

    public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None) { }

    public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None) { }

    public void DrawTextViaEngine(ResolvedFont Font, string Text, Vector2 Position, Color Color, Vector2 Origin, float Scale,
        float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None) { }

    public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color)
        => FillRectangleCalls.Add(new(Origin, Destination, Color));

    public void FillPoint(Vector2 Center, Color Color, float Width) { }

    public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness) { }

    public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness)
        => StrokeAndFillRectangleCalls.Add(new(Origin, Destination, StrokeColor, FillColor, StrokeThickness));

    public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color) { }

    public void StrokeAndFillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f) { }

    public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2)
        => FillTriangleCalls.Add(new(Origin, v0, c0, v1, c1, v2, c2));

    public void FillQuadrilateralLinearClamp(Vector2 Origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
        Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor) { }

    public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f)
        => StrokeLineCalls.Add(new(Origin, Start, End, Color, Thickness));

    public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32) { }

    public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32) { }

    public void StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness = 1.0f, int NumSides = 32)
        => StrokeAndFillCircleCalls.Add(new(Center, StrokeColor, FillColor, Radius, StrokeThickness, NumSides));

    public void SetDrawSettings(DrawSettings Settings)
    {
        CurrentSettings = Settings ?? DrawSettings.Default;
    }

    public IDisposable SetDrawSettingsTemporary(DrawSettings Settings)
    {
        DrawSettings previous = CurrentSettings;
        SetDrawSettings(Settings);
        return new GraphDisposableAction(() => CurrentSettings = previous);
    }

    public IDisposable SetRenderTargetTemporary(IUIRenderTarget New, Color? ClearColor)
        => new GraphDisposableAction(() => { });

    public IDisposable SetTransformTemporary(Matrix Transform)
        => SetDrawSettingsTemporary(CurrentSettings with { Transform = Transform });

    public ClipResolveResult ResolveClip(ClipDefinition Definition)
    {
        ClipDefinition effective = Definition ?? ClipDefinition.None();
        return new(effective, effective, ClipStrategy.Scissor, false);
    }

    public ClipScope PushClipTemporary(ClipDefinition Definition)
    {
        ClipResolveResult resolution = ResolveClip(Definition);
        Rectangle? previous = _currentClipBounds;
        _currentClipBounds = resolution.Effective.Kind == ClipKind.None ? null : resolution.Effective.Shape.Bounds;
        return new(resolution, () => _currentClipBounds = previous);
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

    public void Dispose()
    {
    }
}

internal sealed class GraphTestSurface : IUISurface
{
    private readonly Rectangle Bounds;
    private readonly IUIRenderTarget RenderTarget;

    public GraphTestSurface(Rectangle bounds, IUIRenderTarget renderTarget)
    {
        Bounds = bounds;
        RenderTarget = renderTarget;
    }

    public Rectangle GetBounds() => Bounds;

    public IUIRenderTarget GetRenderTarget() => RenderTarget;
}

internal sealed class GraphTestAssetProvider : IUIAssetProvider
{
    private readonly Dictionary<string, GraphTestImageResource> Images = new(StringComparer.OrdinalIgnoreCase);

    public IUIImageResource LoadImage(string assetName)
    {
        if (!Images.TryGetValue(assetName, out GraphTestImageResource? image))
        {
            image = new(assetName, 16, 16);
            Images[assetName] = image;
        }

        return image;
    }

    public bool TryLoadImage(string assetName, out IUIImageResource image)
    {
        image = LoadImage(assetName);
        return true;
    }
}

internal class GraphTestImageResource : IUIImageResource
{
    public string Id { get; }
    public int Width { get; }
    public int Height { get; }
    public bool IsDisposed { get; private set; }

    public GraphTestImageResource(string id, int width, int height)
    {
        Id = id;
        Width = width;
        Height = height;
    }
}

internal sealed class GraphTestRenderTarget : GraphTestImageResource, IUIRenderTarget
{
    public GraphTestRenderTarget(int width, int height)
        : base("graph-render-target", width, height) { }
}

internal sealed class GraphTestTextEngine : ITextMeasurementEngine
{
    private readonly string DefaultFamily;

    public GraphTestTextEngine(string defaultFamily)
    {
        DefaultFamily = defaultFamily;
    }

    public ResolvedFont ResolveFont(FontSpec spec)
    {
        int size = Math.Max(1, spec.Size);
        FontSpec effectiveSpec = string.IsNullOrWhiteSpace(spec.Family)
            ? FontSpec.Normal(DefaultFamily, size)
            : spec;

        return new ResolvedFont(effectiveSpec, size, 1.0f, 1.0f, size, Math.Max(1.0f, size * 0.5f), Vector2.Zero, false, new object());
    }

    public Vector2 MeasureText(ResolvedFont font, string text)
    {
        float width = (text?.Length ?? 0) * Math.Max(font.SpaceWidth, 1.0f);
        return new(width, font.LineHeight);
    }

    public GlyphMetrics MeasureGlyph(ResolvedFont font, char character)
        => new(0.0f, Math.Max(font.SpaceWidth, 1.0f), 0.0f, font.LineHeight);

    public float GetLineHeight(ResolvedFont font) => font.LineHeight;

    public float GetSpaceWidth(ResolvedFont font) => font.SpaceWidth;

    public void InvalidateCache() { }
}

internal sealed class GraphDisposableAction : IDisposable
{
    private readonly Action DisposeAction;
    private bool _disposed;

    public GraphDisposableAction(Action disposeAction)
    {
        DisposeAction = disposeAction;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        DisposeAction();
    }
}

internal readonly record struct GraphStrokeLineCall(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness);
internal readonly record struct GraphFillRectangleCall(Vector2 Origin, RectangleF Destination, Color Color);
internal readonly record struct GraphStrokeAndFillRectangleCall(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness);
internal readonly record struct GraphFillTriangleCall(Vector2 Origin, Vector2 V0, Color C0, Vector2 V1, Color C1, Vector2 V2, Color C2);
internal readonly record struct GraphStrokeAndFillCircleCall(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness, int NumSides);