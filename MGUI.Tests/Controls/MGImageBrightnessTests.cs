using System;
using System.Collections.Generic;
using System.ComponentModel;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Shared.Rendering.Clipping;
using MGUI.Shared.Text;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;
using Xunit;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Controls;

/// <summary>ADR-0021 coverage: <see cref="MGImage.Brightness"/> (CasaEngine gap G11). The masks handed to the draw context are
/// recorded per call (RGB and A): one draw at k &lt;= 1, two at k &gt; 1 with the second one in <see cref="BlendType.Additive"/>.
/// The pixel proof on a real GPU lives in <c>MGUI.Tests.Integration.MGImageBrightnessGpuTests</c>.</summary>
[Collection(DataBindingRegistryCollection.Name)]
public class MGImageBrightnessTests
{
    private sealed class FakeImageResource : IUIImageResource
    {
        public int Width { get; }
        public int Height { get; }
        public bool IsDisposed => false;

        public FakeImageResource(int width, int height)
        {
            Width = width;
            Height = height;
        }
    }

    private readonly record struct TextureDraw(Rectangle Destination, Color Mask, BlendType Blend, SamplerType Sampler);

    /// <summary>Forwards everything to a <see cref="GraphNoOpDrawTransaction"/>, and records the blend type in force at each
    /// <see cref="DrawTextureTo(IUIImageResource, Rectangle?, Rectangle, Color)"/> call (the inner transaction does not).</summary>
    private sealed class RecordingTransaction : IUIDrawTransaction
    {
        private readonly GraphNoOpDrawTransaction _inner;

        public List<TextureDraw> Draws { get; } = new();
        public int SetDrawSettingsCount { get; private set; }

        public RecordingTransaction(IUIDesktopRuntime renderer, DrawSettings settings)
        {
            _inner = new GraphNoOpDrawTransaction(renderer, settings);
        }

        public DrawSettings CurrentSettings => _inner.CurrentSettings;
        public IUIDesktopRuntime Renderer => _inner.Renderer;
        public Rectangle? CurrentClipBounds => _inner.CurrentClipBounds;

        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask)
        {
            Draws.Add(new(Destination, ColorMask, _inner.CurrentSettings.BlendType, _inner.CurrentSettings.SamplerType));
            _inner.DrawTextureTo(Texture, Source, Destination, ColorMask);
        }

        public void DrawTextureTo(IUIImageResource Texture, Rectangle? Source, Rectangle Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
        {
            Draws.Add(new(Destination, ColorMask, _inner.CurrentSettings.BlendType, _inner.CurrentSettings.SamplerType));
            _inner.DrawTextureTo(Texture, Source, Destination, ColorMask, Origin, Rotation, Depth, Flip);
        }

        public void DrawTextureAt(IUIImageResource Texture, Rectangle? Source, Vector2 Destination, Color ColorMask,
            Vector2 Origin, float Rotation = 0f, float ScaleX = 1f, float ScaleY = 1f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
            => _inner.DrawTextureAt(Texture, Source, Destination, ColorMask, Origin, Rotation, ScaleX, ScaleY, Depth, Flip);

        public void DrawTextViaEngine(ResolvedFont Font, string Text, Vector2 Position, Color Color, Vector2 Origin, float Scale,
            float Rotation = 0f, float Depth = 0f, UIDrawFlip Flip = UIDrawFlip.None)
            => _inner.DrawTextViaEngine(Font, Text, Position, Color, Origin, Scale, Rotation, Depth, Flip);

        public void FillRectangle(Vector2 Origin, RectangleF Destination, Color Color) => _inner.FillRectangle(Origin, Destination, Color);
        public void FillPoint(Vector2 Center, Color Color, float Width) => _inner.FillPoint(Center, Color, Width);
        public void StrokeRectangle(Vector2 Origin, RectangleF Destination, Color Color, Thickness Thickness) => _inner.StrokeRectangle(Origin, Destination, Color, Thickness);
        public void StrokeAndFillRectangle(Vector2 Origin, RectangleF Destination, Color StrokeColor, Color FillColor, Thickness StrokeThickness)
            => _inner.StrokeAndFillRectangle(Origin, Destination, StrokeColor, FillColor, StrokeThickness);
        public void FillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color Color) => _inner.FillPolygon(Origin, Vertices, Color);
        public void StrokeAndFillPolygon(Vector2 Origin, IEnumerable<Vector2> Vertices, Color StrokeColor, Color FillColor, float StrokeThickness = 1.0f)
            => _inner.StrokeAndFillPolygon(Origin, Vertices, StrokeColor, FillColor, StrokeThickness);
        public void FillTriangle(Vector2 Origin, Vector2 v0, Color c0, Vector2 v1, Color c1, Vector2 v2, Color c2) => _inner.FillTriangle(Origin, v0, c0, v1, c1, v2, c2);
        public void DrawTexturedTriangleList(Vector2 Origin, IUIImageResource Texture, IReadOnlyList<Vector2> Vertices, IReadOnlyList<Vector2> TextureCoordinates,
            IReadOnlyList<int> Indices, Color ColorMask)
            => _inner.DrawTexturedTriangleList(Origin, Texture, Vertices, TextureCoordinates, Indices, ColorMask);
        public void FillQuadrilateralLinearClamp(Vector2 Origin, Vector2 topLeft, Color topLeftColor, Vector2 topRight, Color topRightColor,
            Vector2 bottomRight, Color bottomRightColor, Vector2 bottomLeft, Color bottomLeftColor)
            => _inner.FillQuadrilateralLinearClamp(Origin, topLeft, topLeftColor, topRight, topRightColor, bottomRight, bottomRightColor, bottomLeft, bottomLeftColor);
        public void StrokeLineSegment(Vector2 Origin, Vector2 Start, Vector2 End, Color Color, float Thickness = 1.0f) => _inner.StrokeLineSegment(Origin, Start, End, Color, Thickness);
        public void FillCircle(Vector2 Center, Color Color, float Radius, int NumSides = 32) => _inner.FillCircle(Center, Color, Radius, NumSides);
        public void StrokeCircle(Vector2 Center, Color Color, float Radius, float Thickness = 1.0f, int NumSides = 32) => _inner.StrokeCircle(Center, Color, Radius, Thickness, NumSides);
        public void StrokeAndFillCircle(Vector2 Center, Color StrokeColor, Color FillColor, float Radius, float StrokeThickness = 1.0f, int NumSides = 32)
            => _inner.StrokeAndFillCircle(Center, StrokeColor, FillColor, Radius, StrokeThickness, NumSides);

        public void SetDrawSettings(DrawSettings Settings)
        {
            SetDrawSettingsCount++;
            _inner.SetDrawSettings(Settings);
        }

        public IDisposable SetDrawSettingsTemporary(DrawSettings Settings) => _inner.SetDrawSettingsTemporary(Settings);
        public IDisposable SetRenderTargetTemporary(IUIRenderTarget New, Color? ClearColor) => _inner.SetRenderTargetTemporary(New, ClearColor);
        public IDisposable SetTransformTemporary(Matrix Transform) => _inner.SetTransformTemporary(Transform);
        public ClipResolveResult ResolveClip(ClipDefinition Definition) => _inner.ResolveClip(Definition);
        public ClipScope PushClipTemporary(ClipDefinition Definition) => _inner.PushClipTemporary(Definition);
        public ClipScope PushRectangleClip(Rectangle? Bounds, bool IntersectWithCurrentClipTarget) => _inner.PushRectangleClip(Bounds, IntersectWithCurrentClipTarget);
        public IDisposable SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget) => _inner.SetClipTargetTemporary(Bounds, IntersectWithCurrentClipTarget);
        public void Dispose() => _inner.Dispose();
    }

    private sealed class BrightnessViewModel : INotifyPropertyChanged
    {
        private float _brightness = 1f;
        public float Brightness
        {
            get => _brightness;
            set
            {
                if (_brightness != value)
                {
                    _brightness = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Brightness)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
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
                Padding = new Thickness(0),
            };
            MGStackPanel panel = new(window, Orientation.Vertical);
            window.SetContent(panel);
            desktop.Windows.Add(window);
            desktop.Update();
            return new(runtime, desktop, window);
        }

        public RecordingTransaction Draw(MGImage image, float opacity = 1f)
        {
            RecordingTransaction transaction = new(Runtime, DrawSettings.Default);
            DrawBaseArgs ba = new(TimeSpan.Zero, transaction, opacity);
            ElementDrawArgs da = new(ba, new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
            image.DrawSelf(da, new Rectangle(10, 20, 16, 16));
            return transaction;
        }

        public MGImage CreateImage(float? brightness = null, Color? textureColor = null)
        {
            MGImage image = new(Window, new MGTextureData(new FakeImageResource(16, 16)), textureColor, Stretch.None);
            if (brightness.HasValue)
            {
                image.Brightness = brightness.Value;
            }
            return image;
        }
    }

    // Properties, XAML, binding.

    [Fact]
    public void Brightness_DefaultsToOne_AndNotifiesOnlyOnChange()
    {
        Harness harness = Harness.Create();
        MGImage image = harness.CreateImage();
        List<string?> notified = new();
        image.PropertyChanged += (_, e) => notified.Add(e.PropertyName);

        Assert.Equal(1f, image.Brightness);

        image.Brightness = 1f;
        Assert.Empty(notified);

        image.Brightness = 1.5f;
        Assert.Equal(1.5f, image.Brightness);
        Assert.Equal(new[] { nameof(MGImage.Brightness) }, notified);
    }

    [Fact]
    public void Brightness_IsReadFromXaml_AndAbsentMeansOne()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"" Padding=""0"">
    <StackPanel Orientation=""Vertical"">
        <Image Name=""Bright"" SourceName=""a"" Brightness=""1.5"" />
        <Image Name=""Plain"" SourceName=""b"" />
    </StackPanel>
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        Assert.Equal(1.5f, window.GetElementByName<MGImage>("Bright").Brightness);
        Assert.Equal(1f, window.GetElementByName<MGImage>("Plain").Brightness);

        harness.Desktop.Windows.Remove(window);
    }

    [Fact]
    public void Brightness_IsBindable()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core""
        xmlns:dataBinding=""clr-namespace:MGUI.Core.UI.DataBinding;assembly=MGUI.Core"" Width=""200"" Height=""150"" Padding=""0"">
    <Image Name=""Img"" SourceName=""a"" Brightness=""{dataBinding:MGBinding Path=Brightness}"" />
</Window>";

        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        BrightnessViewModel vm = new() { Brightness = 1.25f };
        window.WindowDataContext = vm;
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();
        harness.Desktop.Update();

        MGImage image = window.GetElementByName<MGImage>("Img");
        Assert.Equal(1.25f, image.Brightness);

        vm.Brightness = 1.9f;
        harness.Desktop.Update();
        Assert.Equal(1.9f, image.Brightness);

        harness.Desktop.Windows.Remove(window);
    }

    // Masks handed to the draw context (RGB and A).

    [Fact]
    public void BrightnessOne_DrawsOnce_WhiteOpaque_AndNeverTouchesTheDrawSettings()
    {
        Harness harness = Harness.Create();
        RecordingTransaction transaction = harness.Draw(harness.CreateImage(1f));

        TextureDraw draw = Assert.Single(transaction.Draws);
        Assert.Equal(new Color(255, 255, 255, 255), draw.Mask);
        Assert.Equal(BlendType.AlphaBlend, draw.Blend);
        Assert.Equal(0, transaction.SetDrawSettingsCount);
    }

    [Fact]
    public void BrightnessBelowOne_DrawsOnce_WithARoundedMaskAndAnOpaqueAlpha()
    {
        Harness harness = Harness.Create();
        RecordingTransaction transaction = harness.Draw(harness.CreateImage(127f / 128f));

        TextureDraw draw = Assert.Single(transaction.Draws);
        Assert.Equal(new Color(253, 253, 253, 255), draw.Mask);
        Assert.Equal(BlendType.AlphaBlend, draw.Blend);
        Assert.Equal(0, transaction.SetDrawSettingsCount);
    }

    [Fact]
    public void BrightnessOneAndAHalf_DrawsTwice_UnchangedThenAdditiveWithAHalfMask()
    {
        Harness harness = Harness.Create();
        RecordingTransaction transaction = harness.Draw(harness.CreateImage(1.5f));

        Assert.Equal(2, transaction.Draws.Count);
        Assert.Equal(new Color(255, 255, 255, 255), transaction.Draws[0].Mask);
        Assert.Equal(BlendType.AlphaBlend, transaction.Draws[0].Blend);
        Assert.Equal(new Color(128, 128, 128, 255), transaction.Draws[1].Mask);
        Assert.Equal(BlendType.Additive, transaction.Draws[1].Blend);
        Assert.Equal(transaction.Draws[0].Destination, transaction.Draws[1].Destination);
        Assert.Equal(BlendType.AlphaBlend, transaction.CurrentSettings.BlendType);
    }

    [Fact]
    public void BrightnessNearTwo_DrawsTwice_TheSecondMaskBeingTheRoundedExcess()
    {
        Harness harness = Harness.Create();
        RecordingTransaction transaction = harness.Draw(harness.CreateImage(255f / 128f));

        Assert.Equal(2, transaction.Draws.Count);
        Assert.Equal(new Color(255, 255, 255, 255), transaction.Draws[0].Mask);
        Assert.Equal(BlendType.AlphaBlend, transaction.Draws[0].Blend);
        Assert.Equal(new Color(253, 253, 253, 255), transaction.Draws[1].Mask);
        Assert.Equal(BlendType.Additive, transaction.Draws[1].Blend);
        Assert.Equal(BlendType.AlphaBlend, transaction.CurrentSettings.BlendType);
    }

    [Fact]
    public void TextureColor_MultipliesWithTheBrightnessMask()
    {
        Harness harness = Harness.Create();

        RecordingTransaction darker = harness.Draw(harness.CreateImage(0.5f, new Color(200, 100, 50, 255)));
        TextureDraw darkDraw = Assert.Single(darker.Draws);
        Assert.Equal(new Color(100, 50, 25, 255), darkDraw.Mask); // 200 * 128/255 = 100.4, 100 * 128/255 = 50.2, 50 * 128/255 = 25.1

        RecordingTransaction brighter = harness.Draw(harness.CreateImage(1.5f, new Color(200, 100, 50, 255)));
        Assert.Equal(2, brighter.Draws.Count);
        Assert.Equal(new Color(200, 100, 50, 255), brighter.Draws[0].Mask);
        Assert.Equal(new Color(100, 50, 25, 255), brighter.Draws[1].Mask);
        Assert.Equal(BlendType.Additive, brighter.Draws[1].Blend);
    }

    [Fact]
    public void ElementOpacity_StillAppliesToEachDraw()
    {
        Harness harness = Harness.Create();
        RecordingTransaction transaction = harness.Draw(harness.CreateImage(1.5f), opacity: 0.5f);

        Assert.Equal(2, transaction.Draws.Count);
        Assert.Equal(new Color(127, 127, 127, 127), transaction.Draws[0].Mask);
        Assert.Equal(new Color(64, 64, 64, 127), transaction.Draws[1].Mask);
    }
}
