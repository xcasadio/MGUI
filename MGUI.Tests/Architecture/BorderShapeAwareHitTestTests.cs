using System;
using System.Collections.Generic;
using System.ComponentModel;
using MGUI.Core.UI;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>Opt-in shape-aware hit testing on <see cref="MGBorder"/> (<see cref="MGBorder.IsShapeAwareHitTestEnabled"/>), and proof that
/// controls which did not opt in keep the framework's rectangular hit test.</summary>
public class BorderShapeAwareHitTestTests
{
    private const int Radius = 30;

    [Fact]
    public void Border_WithoutOptIn_KeepsRectangularHitTest()
    {
        Harness harness = Harness.Create();
        MGBorder border = CreateRoundedBorder(harness.Window, Radius);
        harness.Show(border);

        Assert.False(border.IsShapeAwareHitTestEnabled);
        foreach (Vector2 corner in Corners(border.ActualLayoutBounds))
        {
            Assert.True(border.ContainsUnscaledInputPoint(corner), $"corner {corner} must hit the rectangular bounds");
        }

        Assert.True(border.ContainsUnscaledInputPoint(Center(border.ActualLayoutBounds)));
    }

    [Fact]
    public void Border_WithOptIn_IgnoresRoundedOffCorners()
    {
        Harness harness = Harness.Create();
        MGBorder border = CreateRoundedBorder(harness.Window, Radius);
        border.IsShapeAwareHitTestEnabled = true;
        harness.Show(border);

        Rectangle bounds = border.ActualLayoutBounds;
        foreach (Vector2 corner in Corners(bounds))
        {
            Assert.False(border.ContainsUnscaledInputPoint(corner), $"corner {corner} is rounded off and must not hit");
        }

        Assert.True(border.ContainsUnscaledInputPoint(Center(bounds)));
        //  Straight edge midpoints remain inside the silhouette.
        Assert.True(border.ContainsUnscaledInputPoint(new Vector2(bounds.Center.X, bounds.Top + 1)));
        Assert.True(border.ContainsUnscaledInputPoint(new Vector2(bounds.Left + 1, bounds.Center.Y)));
        //  Just inside the arc: 30px radius centred at (Left+30, Top+30); (Left+12, Top+12) is at distance sqrt(648) ~ 25.5.
        Assert.True(border.ContainsUnscaledInputPoint(new Vector2(bounds.Left + 12, bounds.Top + 12)));
    }

    [Fact]
    public void Border_WithOptIn_AndZeroRadius_MatchesRectangularHitTest()
    {
        Harness harness = Harness.Create();
        MGBorder border = CreateRoundedBorder(harness.Window, 0);
        border.IsShapeAwareHitTestEnabled = true;
        harness.Show(border);

        foreach (Vector2 corner in Corners(border.ActualLayoutBounds))
        {
            Assert.True(border.ContainsUnscaledInputPoint(corner));
        }
    }

    [Fact]
    public void Border_WithOptIn_PointOutsideBounds_IsFalse()
    {
        Harness harness = Harness.Create();
        MGBorder border = CreateRoundedBorder(harness.Window, Radius);
        border.IsShapeAwareHitTestEnabled = true;
        harness.Show(border);

        Rectangle bounds = border.ActualLayoutBounds;
        Assert.False(border.ContainsUnscaledInputPoint(new Vector2(bounds.Right + 1, bounds.Center.Y)));
        Assert.False(border.ContainsUnscaledInputPoint(new Vector2(bounds.Center.X, bounds.Bottom + 1)));
    }

    [Fact]
    public void Button_WithRoundedCorners_KeepsRectangularHitTest()
    {
        Harness harness = Harness.Create();
        MGButton button = new(harness.Window)
        {
            PreferredWidth = 100,
            PreferredHeight = 60,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new MGCornerRadius(Radius),
        };
        harness.Show(button);

        foreach (Vector2 corner in Corners(button.ActualLayoutBounds))
        {
            Assert.True(button.ContainsUnscaledInputPoint(corner), "a control that did not opt in keeps its rectangular hit test");
        }
    }

    [Fact]
    public void Rectangle_WithRoundedCorners_KeepsRectangularHitTest()
    {
        Harness harness = Harness.Create();
        MGRectangle rectangle = new(harness.Window, 100, 60, Color.White, 2, Color.Red)
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            CornerRadius = new MGCornerRadius(Radius),
        };
        harness.Show(rectangle);

        foreach (Vector2 corner in Corners(rectangle.ActualLayoutBounds))
        {
            Assert.True(rectangle.ContainsUnscaledInputPoint(corner), "MGRectangle is deliberately not migrated");
        }
    }

    [Fact]
    public void IsShapeAwareHitTestEnabled_RaisesPropertyChanged()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        List<string> changed = new();
        ((INotifyPropertyChanged)border).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        border.IsShapeAwareHitTestEnabled = true;
        border.IsShapeAwareHitTestEnabled = true;

        Assert.Equal(new[] { nameof(MGBorder.IsShapeAwareHitTestEnabled) }, changed);
    }

    [Fact]
    public void HoveredElement_WithOptIn_SkipsRoundedOffCornerButNotCenter()
    {
        Harness harness = Harness.Create();
        MGBorder border = CreateRoundedBorder(harness.Window, Radius);
        border.IsShapeAwareHitTestEnabled = true;
        harness.Show(border);
        Rectangle bounds = border.ActualLayoutBounds;

        harness.Frame(2, new Point(bounds.Left + 2, bounds.Top + 2));
        Assert.False(border.IsSelfOrAncestorOf(harness.Window.HoveredElement), "the rounded-off corner must not hover the border");

        harness.Frame(3, bounds.Center);
        Assert.True(border.IsSelfOrAncestorOf(harness.Window.HoveredElement), "the centre must still hover the border");
    }

    [Fact]
    public void HoveredElement_WithoutOptIn_HoversCorner()
    {
        Harness harness = Harness.Create();
        MGBorder border = CreateRoundedBorder(harness.Window, Radius);
        harness.Show(border);
        Rectangle bounds = border.ActualLayoutBounds;

        harness.Frame(2, new Point(bounds.Left + 2, bounds.Top + 2));

        Assert.True(border.IsSelfOrAncestorOf(harness.Window.HoveredElement), "without opt-in the full rectangle keeps hovering");
    }

    [Fact]
    public void XamlBorder_IsShapeAwareHitTestEnabled_IsAppliedToMGBorder()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""320"" Height=""180"" WindowStyle=""None"">
    <StackPanel Orientation=""Vertical"">
        <Border Name=""Rounded"" CornerRadius=""20"" IsShapeAwareHitTestEnabled=""True"" />
        <Border Name=""Default"" CornerRadius=""20"" />
    </StackPanel>
</Window>";

        MGWindow loaded = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);

        Assert.True(loaded.GetElementByName<MGBorder>("Rounded").IsShapeAwareHitTestEnabled);
        Assert.False(loaded.GetElementByName<MGBorder>("Default").IsShapeAwareHitTestEnabled);
    }

    private static MGBorder CreateRoundedBorder(MGWindow window, int radius) => new(window)
    {
        PreferredWidth = 100,
        PreferredHeight = 60,
        HorizontalAlignment = HorizontalAlignment.Left,
        VerticalAlignment = VerticalAlignment.Top,
        BorderThickness = new Thickness(2),
        CornerRadius = new MGCornerRadius(radius),
    };

    private static IEnumerable<Vector2> Corners(Rectangle bounds)
    {
        yield return new Vector2(bounds.Left + 2, bounds.Top + 2);
        yield return new Vector2(bounds.Right - 2, bounds.Top + 2);
        yield return new Vector2(bounds.Right - 2, bounds.Bottom - 2);
        yield return new Vector2(bounds.Left + 2, bounds.Bottom - 2);
    }

    private static Vector2 Center(Rectangle bounds) => bounds.Center.ToVector2();

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
            harness.Frame(0, Point.Zero);
            return harness;
        }

        /// <summary>Adds the element to the window, shows the window and runs two warm-up frames so layout is settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            Desktop.Windows.Add(Window);
            Frame(0, Point.Zero);
            Frame(1, Point.Zero);
        }

        public void Frame(int frameIndex, Point mousePosition)
        {
            MouseState mouse = new(mousePosition.X, mousePosition.Y, 0,
                ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released, ButtonState.Released);
            Runtime.ApplyFrame(new UpdateBaseArgs(TimeSpan.FromMilliseconds(16 * (frameIndex + 1)), TimeSpan.FromMilliseconds(16), mouse, new KeyboardState()));
            Desktop.Update();
        }
    }
}
