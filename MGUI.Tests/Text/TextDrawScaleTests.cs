using System;
using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text;
using MGUI.Shared.Text.Engines;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Text;

/// <summary>
/// Every MGUI text drawer draws at <see cref="ResolvedFont.ExactScale"/>, the scale text is measured with (ADR-0023).
/// The test engine reports an exact scale (0.5) that differs from its suggested scale (1), a line box of
/// <see cref="LineHeight"/> pixels and a non-zero draw origin, so a drawer that picks the wrong scale, scales the
/// line height twice or forgets the origin is caught.
/// </summary>
public class TextDrawScaleTests
{
    private const float ExactScale = 0.5f;
    private const float LineHeight = 20f;
    private const float CharacterWidth = 6f;
    private static readonly Vector2 DrawOrigin = new(0, 3);

    private sealed class ScaledTestTextEngine : ITextMeasurementEngine
    {
        public ResolvedFont ResolveFont(FontSpec spec)
            => new(spec, Math.Max(1, spec.Size) * 2, ExactScale, 1f, LineHeight, CharacterWidth, DrawOrigin, false, new object());

        public Vector2 MeasureText(ResolvedFont font, string text)
            => new((text?.Length ?? 0) * CharacterWidth, font.LineHeight);

        public GlyphMetrics MeasureGlyph(ResolvedFont font, char c) => new(0f, CharacterWidth, 0f, font.LineHeight);

        public float GetLineHeight(ResolvedFont font) => font.LineHeight;

        public float GetSpaceWidth(ResolvedFont font) => font.SpaceWidth;

        public void InvalidateCache()
        {
        }
    }

    private static (GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window) CreateScene()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 640, 360));
        runtime.TextEngine = new ScaledTestTextEngine();
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 10, 10, 300, 200)
        {
            WindowStyle = WindowStyle.None,
            Padding = new Thickness(0),
        };
        desktop.Windows.Add(window);
        return (runtime, desktop, window);
    }

    private static GraphNoOpDrawTransaction UpdateAndDraw(GraphTestRuntime runtime, MGDesktop desktop)
    {
        for (int frame = 1; frame <= 2; frame++)
        {
            runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * frame), TimeSpan.FromMilliseconds(16), new MouseState(), new KeyboardState()));
            desktop.Update();
        }

        GraphNoOpDrawTransaction transaction = new(runtime, DrawSettings.Default);
        desktop.Draw(transaction);
        return transaction;
    }

    private static float VisualTop(GraphDrawTextCall call) => call.Position.Y - (call.Origin.Y * call.Scale);

    [Fact]
    public void TextBlock_Draws_At_The_Exact_Scale()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = CreateScene();
        window.SetContent(new MGTextBlock(window, "Ag"));

        GraphNoOpDrawTransaction transaction = UpdateAndDraw(runtime, desktop);

        GraphDrawTextCall call = Assert.Single(transaction.DrawTextCalls, x => x.Text == "Ag");
        Assert.Equal(ExactScale, call.Scale);
        Assert.Equal(DrawOrigin, call.Origin);
    }

    [Fact]
    public void RotatedTextLabel_Rotates_About_The_Centre_Of_The_Line_Box_In_Native_Units()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = CreateScene();
        window.SetContent(new MGRotatedTextLabel(window, "Hi"));

        GraphNoOpDrawTransaction transaction = UpdateAndDraw(runtime, desktop);

        GraphDrawTextCall call = Assert.Single(transaction.DrawTextCalls, x => x.Text == "Hi");
        Assert.Equal(ExactScale, call.Scale);
        // Measured size (12, 20) on screen is (24, 40) native units at scale 0.5: its centre is (12, 20), plus the draw origin.
        Assert.Equal(DrawOrigin + new Vector2(12, 20), call.Origin);
    }

    [Fact]
    public void ColorField_Centres_The_Line_Box_In_Its_Text_Strip()
    {
        (GraphTestRuntime runtime, MGDesktop desktop, MGWindow window) = CreateScene();
        MGColorField field = new(window) { FieldHeight = 40 };
        window.SetContent(field);

        GraphNoOpDrawTransaction transaction = UpdateAndDraw(runtime, desktop);

        GraphDrawTextCall call = Assert.Single(transaction.DrawTextCalls);
        GraphFillRectangleCall strip = Assert.Single(transaction.FillRectangleCalls, x => x.Color == new Color(248, 248, 248));
        float stripCentre = strip.Origin.Y + strip.Destination.Y + (strip.Destination.Height / 2f);

        Assert.Equal(ExactScale, call.Scale);
        Assert.InRange(Math.Abs(VisualTop(call) + (LineHeight / 2f) - stripCentre), 0f, 0.5f);
    }
}
