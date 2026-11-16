using MGUI.Shared.Assets;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using System.Globalization;
using System.Text;

namespace MGUI.Tests.Graph;

/// <summary>
/// A draw transaction for characterisation tests: it writes one line per call of every method of <see cref="IUIDrawTransaction"/>, with the
/// arguments, in one ordered journal. <see cref="GraphNoOpDrawTransaction"/> cannot serve that purpose: it ignores several calls and keeps one
/// list per kind of call, which loses the order between kinds.<para/>
/// Every line starts with a label made of the member name and its parameter count (<c>FillRectangle/3</c>), followed by "s" when a parameter is a
/// <see cref="ReadOnlySpan{T}"/>, which tells the two <c>DrawTextViaEngine</c> overloads apart. <see cref="Label"/> builds it from reflection, so a test
/// can check that every member of the interface writes a line.<para/>
/// It follows the clip bounds and the draw settings like <see cref="GraphNoOpDrawTransaction"/> (code under test reads <see cref="CurrentClipBounds"/>),
/// and records the disposal of every scope it hands out. It allocates freely: never use it in an allocation test.
/// </summary>
internal sealed class OrderedRecordingDrawTransaction : IUIDrawTransaction
{
    private readonly List<string> JournalLines = new();
    private Rectangle? _currentClipBounds;
    private int NextScopeId;

    public DrawSettings CurrentSettings { get; private set; }
    public IUIDesktopRuntime Renderer { get; }
    public Rectangle? CurrentClipBounds => _currentClipBounds;

    /// <summary>The lines written so far, one per call, in call order.</summary>
    public IReadOnlyList<string> Lines => JournalLines;

    public OrderedRecordingDrawTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
    {
        Renderer = renderer;
        CurrentSettings = settings;
    }

    /// <summary>The whole journal, one line per call.</summary>
    public string Journal => string.Join('\n', JournalLines);

    public void ClearJournal() => JournalLines.Clear();

    /// <summary>The label that starts the lines of <paramref name="method"/>: name, parameter count, "s" when a parameter is a span.</summary>
    public static string Label(System.Reflection.MethodInfo method)
    {
        System.Reflection.ParameterInfo[] parameters = method.GetParameters();
        bool hasSpan = parameters.Any(x => x.ParameterType == typeof(ReadOnlySpan<char>));
        return $"{method.Name}/{parameters.Length}{(hasSpan ? "s" : "")}";
    }

    public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask)
        => Write("DrawTextureTo/4", Img(Texture), Rect(Source), Rect(Destination), Col(ColorMask));

    public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        => Write("DrawTextureTo/8", Img(Texture), Rect(Source), Rect(Destination), Col(ColorMask), Vec(Origin), F(Rotation), F(Depth), Flip.ToString());

    public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
        Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        => Write("DrawTextureAt/10", Img(Texture), Rect(Source), Vec(Destination), Col(ColorMask), Vec(Origin), F(Rotation), F(ScaleX), F(ScaleY), F(Depth), Flip.ToString());

    public void DrawTextViaEngine(ResolvedFont Font, string Text, Vector2 Position, Color Color, Vector2 Origin, float Scale,
        float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        => Write("DrawTextViaEngine/9", Fnt(Font), Quote(Text), Vec(Position), Col(Color), Vec(Origin), F(Scale), F(Rotation), F(Depth), Flip.ToString());

    public void DrawTextViaEngine(ResolvedFont Font, ReadOnlySpan<char> Text, Vector2 Position, Color Color, Vector2 Origin, float Scale,
        float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        => Write("DrawTextViaEngine/9s", Fnt(Font), Quote(Text.ToString()), Vec(Position), Col(Color), Vec(Origin), F(Scale), F(Rotation), F(Depth), Flip.ToString());

    public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color)
        => Write("FillRectangle/3", Vec(Origin), RectF(Destination), Col(Color));

    public void FillPoint(Vector2 Center, Color Color, float Width)
        => Write("FillPoint/3", Vec(Center), Col(Color), F(Width));

    public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness)
        => Write("StrokeRectangle/4", Vec(Origin), RectF(Destination), Col(Color), Thick(Thickness));

    public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness)
        => Write("StrokeAndFillRectangle/5", Vec(Origin), RectF(Destination), Col(StrokeColor), Col(FillColor), Thick(StrokeThickness));

    public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color)
        => Write("FillPolygon/3", Vec(Origin), Vecs(Vertices), Col(Color));

    public void StrokeAndFillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f)
        => Write("StrokeAndFillPolygon/5", Vec(Origin), Vecs(Vertices), Col(StrokeColor), Col(FillColor), F(StrokeThickness));

    public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2)
        => Write("FillTriangle/7", Vec(Origin), Vec(v0), Col(c0), Vec(v1), Col(c1), Vec(v2), Col(c2));

    public void DrawTexturedTriangleList(Vector2 Origin, IUIImageResource Texture, IReadOnlyList<Vector2> Vertices, IReadOnlyList<Vector2> TextureCoordinates,
        IReadOnlyList<int> Indices, Color ColorMask)
        => Write("DrawTexturedTriangleList/6", Vec(Origin), Img(Texture), Vecs(Vertices), Vecs(TextureCoordinates),
            Indices == null ? "null" : "[" + string.Join(',', Indices) + "]", Col(ColorMask), CurrentSettings.SamplerType.ToString(), Rect(_currentClipBounds));

    public void FillQuadrilateralLinearClamp(Vector2 Origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
        Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor)
        => Write("FillQuadrilateralLinearClamp/9", Vec(Origin), Vec(topLeft), Col(topLeftColor), Vec(topRight), Col(topRightColor),
            Vec(bottomRight), Col(bottomRightColor), Vec(bottomLeft), Col(bottomLeftColor));

    public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f)
        => Write("StrokeLineSegment/5", Vec(Origin), Vec(Start), Vec(End), Col(Color), F(Thickness));

    public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32)
        => Write("FillCircle/4", Vec(Center), Col(Color), F(Radius), NumSides.ToString(CultureInfo.InvariantCulture));

    public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32)
        => Write("StrokeCircle/5", Vec(Center), Col(Color), F(Radius), F(Thickness), NumSides.ToString(CultureInfo.InvariantCulture));

    public void StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness = 1.0f, int NumSides = 32)
        => Write("StrokeAndFillCircle/6", Vec(Center), Col(StrokeColor), Col(FillColor), F(Radius), F(StrokeThickness), NumSides.ToString(CultureInfo.InvariantCulture));

    public void SetDrawSettings(DrawSettings Settings)
    {
        CurrentSettings = Settings ?? DrawSettings.Default;
        Write("SetDrawSettings/1", Settings == null ? "null" : Sett(Settings));
    }

    private readonly CallbackDrawStateOwner _DrawStateScopes = new();
    private readonly CallbackClipHandleOwner _ClipScopes = new();

    public DrawStateHandle SetDrawSettingsTemporary(DrawSettings Settings)
    {
        DrawSettings previous = CurrentSettings;
        int id = NextScopeId++;
        CurrentSettings = Settings ?? DrawSettings.Default;
        Write("SetDrawSettingsTemporary/1", "scope=" + id, Settings == null ? "null" : Sett(Settings));
        return _DrawStateScopes.Push(() =>
        {
            CurrentSettings = previous;
            Write("DisposeDrawSettings", "scope=" + id);
        });
    }

    public DrawStateHandle SetRenderTargetTemporary(IUIRenderTarget New, Color? ClearColor)
    {
        int id = NextScopeId++;
        Write("SetRenderTargetTemporary/2", "scope=" + id, Img(New), ClearColor.HasValue ? Col(ClearColor.Value) : "null");
        return _DrawStateScopes.Push(() => Write("DisposeRenderTarget", "scope=" + id));
    }

    public DrawStateHandle SetTransformTemporary(Matrix Transform)
    {
        DrawSettings previous = CurrentSettings;
        int id = NextScopeId++;
        CurrentSettings = CurrentSettings with { Transform = Transform };
        Write("SetTransformTemporary/1", "scope=" + id, Mat(Transform));
        return _DrawStateScopes.Push(() =>
        {
            CurrentSettings = previous;
            Write("DisposeTransform", "scope=" + id);
        });
    }

    public ClipResolveResult ResolveClip(ClipDefinition Definition)
    {
        Write("ResolveClip/1", Clp(Definition));
        return Resolve(Definition);
    }

    public ClipHandle PushClipTemporary(ClipDefinition Definition)
        => Push("PushClipTemporary/1", Definition, Clp(Definition));

    public ClipHandle PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
        => Push("PushRectangleClip/2", RectangleOrNone(Bounds, IntersectWithCurrentClipTarget), Rect(Bounds), "intersect=" + IntersectWithCurrentClipTarget);

    public ClipHandle SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)
        => Push("SetClipTargetTemporary/2", RectangleOrNone(Bounds, IntersectWithCurrentClipTarget), Rect(Bounds), "intersect=" + IntersectWithCurrentClipTarget);

    private static ClipDefinition RectangleOrNone(Rectangle? Bounds, bool Intersect)
        => Bounds.HasValue ? ClipDefinition.Rectangle(Bounds.Value, Intersect) : ClipDefinition.None(Intersect);

    private ClipHandle Push(string label, ClipDefinition definition, params string[] arguments)
    {
        ClipResolveResult resolution = Resolve(definition);
        Rectangle? previous = _currentClipBounds;
        _currentClipBounds = resolution.Effective.Kind == ClipKind.None ? null : resolution.Effective.Shape.Bounds;
        int id = NextScopeId++;
        string[] line = new string[arguments.Length + 2];
        line[0] = "scope=" + id;
        arguments.CopyTo(line, 1);
        line[^1] = "bounds=" + Rect(_currentClipBounds);
        Write(label, line);
        return _ClipScopes.Push(resolution, () =>
        {
            _currentClipBounds = previous;
            Write("DisposeClip", "scope=" + id, "restored=" + Rect(previous));
        });
    }

    public void Dispose() => Write("Dispose/0");

    private static ClipResolveResult Resolve(ClipDefinition Definition)
    {
        ClipDefinition effective = Definition ?? ClipDefinition.None();
        return new(effective, effective, ClipStrategy.Scissor, false);
    }

    private void Write(string label, params string[] arguments)
    {
        StringBuilder line = new(label);
        for (int i = 0; i < arguments.Length; i++)
        {
            line.Append(' ').Append(arguments[i]);
        }

        JournalLines.Add(line.ToString());
    }

    private static string F(float value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Vec(Vector2 v) => $"({F(v.X)},{F(v.Y)})";
    private static string Vecs(IEnumerable<Vector2> vectors)
        => vectors == null ? "null" : "[" + string.Join(';', vectors.Select(Vec)) + "]";
    private static string Col(Color c) => "#" + c.PackedValue.ToString("X8", CultureInfo.InvariantCulture);
    private static string Rect(Rectangle r) => $"[{r.X},{r.Y},{r.Width},{r.Height}]";
    private static string Rect(Rectangle? r) => r.HasValue ? Rect(r.Value) : "null";
    private static string RectF(RectangleF r) => $"[{F(r.X)},{F(r.Y)},{F(r.Width)},{F(r.Height)}]";
    private static string Thick(Thickness t) => $"({F(t.Left)},{F(t.Top)},{F(t.Right)},{F(t.Bottom)})";
    private static string Quote(string text) => text == null ? "null" : "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
    private static string Img(IUIImageResource image) => image == null ? "null" : $"img:{(image as GraphTestImageResource)?.Id ?? image.GetType().Name}/{image.Width}x{image.Height}/disposed={image.IsDisposed}";
    private static string Fnt(ResolvedFont font) => font == null ? "null"
        : $"font:{font.Spec.Family}/{font.Spec.Size}/{font.Spec.Style}/actual{font.ActualSize}/x{F(font.ExactScale)}";

    private static string Mat(Matrix m)
        => m == Matrix.Identity ? "I" : $"[{F(m.M11)},{F(m.M12)},{F(m.M13)},{F(m.M14)};{F(m.M21)},{F(m.M22)},{F(m.M23)},{F(m.M24)};{F(m.M31)},{F(m.M32)},{F(m.M33)},{F(m.M34)};{F(m.M41)},{F(m.M42)},{F(m.M43)},{F(m.M44)}]";

    private static string Sett(DrawSettings s)
        => $"settings(t={Mat(s.Transform)},{s.RasterizerType},{s.Sort},{s.BlendType},{s.SamplerType},{s.DepthStencilType},effect={(s.BackendEffect == null ? "null" : "set")},ref={s.StencilReference},read={s.StencilReadMask},write={s.StencilWriteMask})";

    private static string Clp(ClipDefinition d)
    {
        if (d == null)
        {
            return "null";
        }

        string geometry = d.Shape.Geometry.HasValue && !d.Shape.Geometry.Value.IsEmpty
            ? $",geometry={d.Shape.Geometry.Value.Vertices.Count}v/{d.Shape.Geometry.Value.Indices.Count}i"
            : "";
        ClipCornerRadius radius = d.Shape.CornerRadius;
        return $"clip({d.Kind},{Rect(d.Shape.Bounds)},radius=({radius.TopLeft},{radius.TopRight},{radius.BottomRight},{radius.BottomLeft}){geometry},intersect={d.IntersectWithCurrentClip},pref={d.StrategyPreference},fallback={d.AllowRectangleFallback},name={d.DebugName ?? "null"})";
    }
}

/// <summary>Compares a journal with a frozen expectation: the number of lines and a hash of the text (SHA-256, first 16 hex digits).
/// On a mismatch the failure message carries the whole journal, so the new value can be read and the difference found by a text diff.
/// The frozen journals of the desktop frame and of the control families are the ones of the code that pushed the rectangle clips of an element as definitions, in the
/// normal form of <see cref="ElementClipJournal"/> (ADR-0035).</summary>
internal static class FrozenJournal
{
    public static string Hash(string journal)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(journal)))[..16];

    public static void Assert(string name, int expectedLines, string expectedHash, IReadOnlyList<string> lines)
    {
        string journal = string.Join('\n', lines);
        string hash = Hash(journal);
        if (lines.Count != expectedLines || hash != expectedHash)
        {
            Xunit.Assert.Fail($"{name}: expected {expectedLines} lines, hash {expectedHash}; got {lines.Count} lines, hash {hash}.\nJournal:\n{journal}");
        }
    }
}

/// <summary>Turns a journal written by the code that pushed the rectangle clips of an element as definitions into the journal of the code that pushes them with
/// <see cref="IUIRenderContext.PushRectangleClip"/> (ADR-0035, decision D16): a <c>PushClipTemporary/1</c> line of a rectangular definition whose debug name ends with
/// <c>.Self</c>, <c>.Contents</c> or <c>.Viewport</c> (the names the clip hooks of <see cref="MGUI.Core.UI.MGElement"/> gave) becomes the <c>PushRectangleClip/2</c>
/// line with the same scope, bounds and intersect flag. Every other line is kept as it is: the rounded and geometry clips, and the clip that a control pushes itself
/// (the <c>.Swatches</c> clip of <see cref="MGUI.Core.UI.MGGridColorPicker"/>).</summary>
internal static class ElementClipJournal
{
    private static readonly System.Text.RegularExpressions.Regex ElementRectangleClip = new(
        @"^PushClipTemporary/1 (?<scope>scope=\d+) clip\(Rectangle,(?<rect>\[[^\]]*\]),radius=\([^)]*\),intersect=(?<intersect>True|False),pref=Default,fallback=False,name=[^ ]*\.(Self|Contents|Viewport)\) (?<bounds>bounds=.*)$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> Normalise(IReadOnlyList<string> lines)
    {
        List<string> result = new(lines.Count);
        foreach (string line in lines)
        {
            System.Text.RegularExpressions.Match match = ElementRectangleClip.Match(line);
            result.Add(match.Success
                ? $"PushRectangleClip/2 {match.Groups["scope"].Value} {match.Groups["rect"].Value} intersect={match.Groups["intersect"].Value} {match.Groups["bounds"].Value}"
                : line);
        }

        return result;
    }
}
