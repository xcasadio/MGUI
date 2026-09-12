using MGUI.Core.UI;
using MGUI.Core.UI.Animation;
using MGUI.Core.UI.Containers;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Animation;

/// <summary>A headless 800x600 desktop with one 400x300 window holding a content-sized vertical panel of two 120x40 buttons,
/// driven frame by frame (16 ms by default) through the shared <see cref="GraphTestRuntime"/>.</summary>
internal sealed class AnimationTestScene
{
    public const int FrameMilliseconds = 16;

    /// <summary>A plain (non store-backed) target over <see cref="MGElement.Opacity"/>, independent of the framework registrations of S4.</summary>
    public static readonly UIDelegateAnimationTarget<float> OpacityTarget = new("Test.Opacity", e => e.Opacity, (e, v) => e.Opacity = v);

    public GraphTestRuntime Runtime { get; private init; }
    public MGDesktop Desktop { get; private init; }
    public MGWindow Window { get; private init; }
    public MGStackPanel Panel { get; private init; }
    public MGButton Top { get; private init; }
    public MGButton Bottom { get; private init; }
    public int FrameIndex { get; private set; }
    public Point Mouse { get; set; } = new(1, 1);

    public static AnimationTestScene Build()
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow window = new(desktop, 0, 0, 400, 300) { WindowStyle = WindowStyle.None };
        MGButton top = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
        MGButton bottom = new(window) { PreferredWidth = 120, PreferredHeight = 40 };
        MGStackPanel panel = new(window, Orientation.Vertical)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Spacing = 0,
        };
        panel.TryAddChild(top);
        panel.TryAddChild(bottom);
        window.SetContent(panel);
        desktop.Windows.Add(window);

        AnimationTestScene scene = new() { Runtime = runtime, Desktop = desktop, Window = window, Panel = panel, Top = top, Bottom = bottom };
        scene.Frames(2);
        return scene;
    }

    /// <summary>Drives a window built elsewhere (a XAML load) frame by frame; <see cref="Panel"/>, <see cref="Top"/> and <see cref="Bottom"/> stay null.</summary>
    public static AnimationTestScene Attach(GraphTestRuntime runtime, MGDesktop desktop, MGWindow window)
    {
        if (!desktop.Windows.Contains(window))
        {
            desktop.Windows.Add(window);
        }

        AnimationTestScene scene = new() { Runtime = runtime, Desktop = desktop, Window = window };
        scene.Frames(2);
        return scene;
    }

    /// <summary>Runs <paramref name="count"/> desktop frames of <paramref name="milliseconds"/> each with the current <see cref="Mouse"/> position.</summary>
    public void Frames(int count, int milliseconds = FrameMilliseconds, bool leftPressed = false)
    {
        for (int i = 0; i < count; i++)
        {
            FrameIndex++;
            MouseState state = new(Mouse.X, Mouse.Y, 0,
                leftPressed ? ButtonState.Pressed : ButtonState.Released,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds((double)milliseconds * FrameIndex), TimeSpan.FromMilliseconds(milliseconds), state, new KeyboardState()));
            Desktop.Update();
        }
    }

    public GraphNoOpDrawTransaction Draw()
    {
        GraphNoOpDrawTransaction transaction = new(Runtime, DrawSettings.Default);
        Desktop.Draw(transaction);
        return transaction;
    }

    public static UIPropertyAnimation<float> OpacityAnimation(float? from, float to, int durationMilliseconds)
    {
        UIPropertyAnimation<float> animation = new() { Target = OpacityTarget, To = to, Duration = TimeSpan.FromMilliseconds(durationMilliseconds) };
        if (from.HasValue)
        {
            animation.From = from.Value;
        }

        return animation;
    }
}
