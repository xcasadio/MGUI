using System;
using MGUI.Backend.MonoGame;
using MGUI.Core.UI;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Assets;
using MGUI.Shared.Helpers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using Xunit;

namespace MGUI.Tests.Integration;

/// <summary>Real-GPU pixel proof of ADR-0024 (<see cref="MGImage.BlendMode"/>): an image in <see cref="BlendType.Subtractive"/> takes its color
/// away from what is below it, saturates at 0, ignores its own alpha and leaves the destination alpha alone. It fails when the table entry of
/// <c>MonoGameRenderInterop</c> uses <see cref="BlendFunction.Subtract"/> (source minus destination) or writes the alpha.<para/>
/// All GPU work happens through <see cref="GpuDeviceHost"/> on its single dedicated thread.</summary>
[Collection(GpuDeviceCollection.Name)]
public class MGImageBlendModeGpuTests
{
    private const int TargetSize = 32;
    private static readonly Color Background = new(200, 200, 100, 128);

    private static Color RenderPixel(Color texel, BlendType blendMode, float brightness = 1f, float opacity = 1f)
    {
        return GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;

            using Texture2D texture = new(device, 4, 4);
            Color[] texels = new Color[16];
            Array.Fill(texels, texel);
            texture.SetData(texels);

            // The image is built in a window of a headless (no GPU) desktop: only its DrawSelf runs against the real device.
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 100, 100) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
            window.SetContent(new MGStackPanel(window, Orientation.Vertical));
            desktop.Windows.Add(window);
            desktop.Update();

            MGImage image = new(window, new MGTextureData(new MonoGameImageResource(texture)), null, Stretch.None)
            {
                BlendMode = blendMode,
                Brightness = brightness,
            };

            using RenderTarget2D target = new(device, TargetSize, TargetSize, false, SurfaceFormat.Color, DepthFormat.None);
            device.SetRenderTarget(target);
            device.Clear(Background);

            MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
            using (DrawTransaction dt = (DrawTransaction)renderer.BeginDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Sprites))
            {
                ElementDrawArgs da = new(new DrawBaseArgs(TimeSpan.Zero, dt, opacity), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
                image.DrawSelf(da, new Rectangle(8, 8, 4, 4));
            }

            device.SetRenderTarget(null);
            Color[] pixels = new Color[TargetSize * TargetSize];
            target.GetData(pixels);
            return pixels[10 * TargetSize + 10];
        });
    }

    [GpuFact]
    public void Subtractive_TakesTheTexelFromTheDestination_AndSaturatesAtZero()
    {
        Assert.Equal(new Color(100, 140, 0, 128), RenderPixel(new Color(100, 60, 200, 255), BlendType.Subtractive));
    }

    [GpuFact]
    public void Subtractive_IgnoresTheAlphaOfTheTexel()
    {
        Assert.Equal(new Color(100, 140, 0, 128), RenderPixel(new Color(100, 60, 200, 0), BlendType.Subtractive));
    }

    [GpuFact]
    public void Subtractive_LeavesTheDestinationUnderABlackTexel()
    {
        Assert.Equal(Background, RenderPixel(new Color(0, 0, 0, 0), BlendType.Subtractive));
    }

    [GpuFact]
    public void Subtractive_IsNotBrightenedAboveOne()
    {
        Assert.Equal(new Color(100, 140, 0, 128), RenderPixel(new Color(100, 60, 200, 255), BlendType.Subtractive, brightness: 1.5f));
    }

    [GpuFact]
    public void Subtractive_ScalesWithTheElementOpacity()
    {
        Color pixel = RenderPixel(new Color(100, 60, 200, 255), BlendType.Subtractive, opacity: 0.5f);

        Assert.InRange(pixel.R, 149, 151);
        Assert.InRange(pixel.G, 169, 171);
        Assert.Equal(0, pixel.B);
        Assert.Equal(128, pixel.A);
    }
}
