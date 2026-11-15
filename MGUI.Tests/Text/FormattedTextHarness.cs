using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Shared.Text.Engines;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using MonoGame.Extended;

namespace MGUI.Tests.Text;

/// <summary>A text block that counts the layouts it asks its parent for.</summary>
internal sealed class CountingFormattedTextBlock : MGTextBlock
{
    public int LayoutChangedCallCount { get; private set; }

    public CountingFormattedTextBlock(MGWindow window, string text, bool allowsInlineFormatting = true) : base(window, text, Color.White, 12, allowsInlineFormatting) { }

    public void ResetCount() => LayoutChangedCallCount = 0;

    /// <summary>The tooltip the block would show at the current position of the mouse (see <see cref="MGTextBlock"/>.TryGetToolTip), which also forgets the bounds of the last draw.</summary>
    public bool ProbeToolTip(out MGToolTip toolTip) => TryGetToolTip(out toolTip);

    protected override void LayoutChanged(MGElement Source, bool NotifyParent)
    {
        LayoutChangedCallCount++;
        base.LayoutChanged(Source, NotifyParent);
    }
}

/// <summary>
/// A desktop on a <see cref="GraphTestRuntime"/> with one window whose content is one formatted <see cref="CountingFormattedTextBlock"/>, laid out by two frames. A texture named
/// <see cref="FormattedTextCorpus.Icon"/> (20x30) is registered. The same harness is built with the legacy pipeline (<see cref="MGTextBlock.ForceLegacyTextPath"/>) or the allocation-free one.
/// </summary>
internal sealed class FormattedTextHarness
{
    private int Elapsed;

    public GraphTestRuntime Runtime { get; }
    public MGDesktop Desktop { get; }
    public MGWindow Window { get; }
    public CountingFormattedTextBlock TextBlock { get; }
    public int Width { get; }

    /// <param name="engine">The text engine: the default one measures 6 px per character and the fractional one has fractional, non additive metrics.</param>
    public FormattedTextHarness(int width, bool legacy, bool wrap = true, Action<MGTextBlock> configure = null, bool stableFootprint = false, ITextMeasurementEngine engine = null, bool allowsInlineFormatting = true)
    {
        Width = width;
        Runtime = new GraphTestRuntime(new Rectangle(0, 0, 1200, 600));
        if (engine != null)
        {
            Runtime.TextEngine = engine;
        }

        Desktop = new MGDesktop(Runtime);
        Desktop.Resources.AddTexture(FormattedTextCorpus.Icon, new MGTextureData(new GraphTestImageResource(FormattedTextCorpus.Icon, 20, 30)));
        Window = new MGWindow(Desktop, 0, 0, width, 300) { WindowStyle = WindowStyle.None, Padding = new Thickness(0) };
        TextBlock = new CountingFormattedTextBlock(Window, "", allowsInlineFormatting)
        {
            WrapText = wrap,
            HasStableTextFootprint = stableFootprint,
            ForceLegacyTextPath = legacy
        };
        configure?.Invoke(TextBlock);
        Window.SetContent(TextBlock);
        Desktop.Windows.Add(Window);
        Frame();
        Frame();
    }

    public void Frame()
    {
        Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(Elapsed), TimeSpan.FromMilliseconds(16), default, default));
        Desktop.Update();
        Elapsed += 16;
    }

    /// <summary>Moves the mouse: the next reads of the position of the mouse (tooltips, clicks on an action) are at <paramref name="screenPosition"/>.</summary>
    public void MoveMouse(Point screenPosition)
    {
        Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(Elapsed), TimeSpan.FromMilliseconds(16),
            new Microsoft.Xna.Framework.Input.MouseState(screenPosition.X, screenPosition.Y, 0, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released,
                Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released, Microsoft.Xna.Framework.Input.ButtonState.Released), default));
        Elapsed += 16;
    }

    public ElementDrawArgs CreateDrawArgs(IUIDrawTransaction transaction, float opacity = 1f)
        => new(new DrawBaseArgs(TimeSpan.Zero, transaction, opacity), new VisualState(PrimaryVisualState.Normal, SecondaryVisualState.None), Point.Zero);
}
