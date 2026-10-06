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

/// <summary>Real-GPU pixel proof of ADR-0021 (<see cref="MGImage.Brightness"/>, CasaEngine gap G11): an opaque texel <c>t</c> drawn over black
/// by a real <see cref="DrawTransaction"/> must come out as <c>min(255, t * k)</c> per channel, within 2/255. It fails when the second,
/// additive draw carries a mask alpha below 255 (the addition is then <c>t * (k - 1)^2</c>) and when no second draw happens at all.<para/>
/// All GPU work happens through <see cref="GpuDeviceHost"/> on its single dedicated thread.</summary>
[Collection(GpuDeviceCollection.Name)]
public class MGImageBrightnessGpuTests
{
    private const int TargetSize = 32;
    private static readonly Color Texel = new(100, 60, 200, 255);

    private static Color RenderPixel(float brightness)
    {
        return GpuDeviceHost.Instance.Invoke(() =>
        {
            GraphicsDevice device = GpuDeviceHost.Instance.GraphicsDevice;

            using Texture2D texture = new(device, 4, 4);
            Color[] texels = new Color[16];
            Array.Fill(texels, Texel);
            texture.SetData(texels);

            // The image is built in a window of a headless (no GPU) desktop: only its DrawSelf runs against the real device.
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            MGWindow window = new(desktop, 0, 0, 100, 100) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
            window.SetContent(new MGStackPanel(window, Orientation.Vertical));
            desktop.Windows.Add(window);
            desktop.Update();

            MGImage image = new(window, new MGTextureData(new MonoGameImageResource(texture)), null, Stretch.None) { Brightness = brightness };

            using RenderTarget2D target = new(device, TargetSize, TargetSize, false, SurfaceFormat.Color, DepthFormat.None);
            device.SetRenderTarget(target);
            device.Clear(Color.Black);

            MainRenderer renderer = MonoGameBackendBootstrap.Create(new GameRenderHost<HeadlessGame>(GpuDeviceHost.Instance.Game)).Renderer;
            using (DrawTransaction dt = (DrawTransaction)renderer.CreateDrawTransaction(DrawSettings.Default, DeferBegin: true, DrawContext.Sprites))
            {
                ElementDrawArgs da = new(new DrawBaseArgs(TimeSpan.Zero, dt, 1f), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
                image.DrawSelf(da, new Rectangle(8, 8, 4, 4));
            }

            device.SetRenderTarget(null);
            Color[] pixels = new Color[TargetSize * TargetSize];
            target.GetData(pixels);
            return pixels[10 * TargetSize + 10];
        });
    }

    private static void AssertSaturatedProduct(Color actual, float k)
    {
        Assert.InRange(actual.R, Math.Min(255, Texel.R * k) - 2, Math.Min(255, Texel.R * k) + 2);
        Assert.InRange(actual.G, Math.Min(255, Texel.G * k) - 2, Math.Min(255, Texel.G * k) + 2);
        Assert.InRange(actual.B, Math.Min(255, Texel.B * k) - 2, Math.Min(255, Texel.B * k) + 2);
    }

    [GpuFact]
    public void BrightnessOne_LeavesTheTexelUnchanged()
    {
        Color pixel = RenderPixel(1f);
        Assert.Equal(Texel.R, pixel.R);
        Assert.Equal(Texel.G, pixel.G);
        Assert.Equal(Texel.B, pixel.B);
    }

    [GpuFact]
    public void BrightnessOneAndAHalf_OverBlack_IsTheSaturatedProduct()
    {
        AssertSaturatedProduct(RenderPixel(1.5f), 1.5f);
    }

    [GpuFact]
    public void BrightnessNearTwo_OverBlack_IsTheSaturatedProduct()
    {
        AssertSaturatedProduct(RenderPixel(255f / 128f), 255f / 128f);
    }
}
