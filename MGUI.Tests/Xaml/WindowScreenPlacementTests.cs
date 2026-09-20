using MGUI.Core.UI;
using MGUI.Core.UI.XAML;
using MGUI.Tests.Graph;
using MonoGame.Extended;
using Rectangle = Microsoft.Xna.Framework.Rectangle;

namespace MGUI.Tests.Xaml;

/// <summary>
/// A root window can say where it sits relative to the desktop instead of computing an absolute Left and Top.
/// <para/>
/// <see cref="MGWindow"/> exposed only absolute coordinates, and the one thing it did with
/// <see cref="MGDesktop.ValidScreenBounds"/> was drag a moved window back inside them -- a correction after
/// the fact, not a placement. Every screen that wanted to be centred, or pinned to a corner, or no taller
/// than the view, wrote the arithmetic itself, duplicating in C# the size already declared in its markup.
/// <para/>
/// <see cref="MGWindow.ScreenMargin"/> is the inset from the chosen edge -- deliberately not
/// <see cref="MGElement.Margin"/>, which on a root window insets the window's own content instead. An aligned
/// window is kept within the space it leaves, so the cap on size falls out of the placement rather than being
/// a separate feature. Not to be confused with <see cref="MGElement.HorizontalAlignment"/>, which describes a window's
/// content and which a root window keeps at Stretch.
/// </summary>
public class WindowScreenPlacementTests
{
    private const int ScreenWidth = 800;
    private const int ScreenHeight = 600;

    [Fact]
    public void ACentredWindow_SitsInTheMiddleOfTheScreen()
    {
        MGWindow window = LoadWindow("""Width="300" Height="200" ScreenHorizontalAlignment="Center" ScreenVerticalAlignment="Center" """);

        Assert.Equal((ScreenWidth - 300) / 2, window.Left);
        Assert.Equal((ScreenHeight - 200) / 2, window.Top);
    }

    [Fact]
    public void AWindowPinnedToACorner_InsetsItselfByItsScreenMargin()
    {
        MGWindow window = LoadWindow("""Width="300" Height="200" ScreenMargin="10" ScreenHorizontalAlignment="Right" ScreenVerticalAlignment="Top" """);

        Assert.Equal(ScreenWidth - 300 - 10, window.Left);
        Assert.Equal(10, window.Top);
    }

    [Fact]
    public void AWindowAnchoredToTheBottom_LeavesItsBottomMarginBelow()
    {
        MGWindow window = LoadWindow("""Width="300" Height="36" ScreenMargin="0,0,0,14" ScreenHorizontalAlignment="Center" ScreenVerticalAlignment="Bottom" """);

        Assert.Equal((ScreenWidth - 300) / 2, window.Left);
        Assert.Equal(ScreenHeight - 36 - 14, window.Top);
    }

    [Fact]
    public void AnAlignedWindow_NeverExceedsTheSpaceItsScreenMarginLeaves()
    {
        // The reason this matters: in a split-screen view each viewport is a fraction of the back buffer, and a
        // panel declared 560 tall would otherwise run off the bottom of a short one.
        MGWindow window = LoadWindow("""Width="320" Height="5000" ScreenMargin="10" ScreenHorizontalAlignment="Right" ScreenVerticalAlignment="Top" """);

        Assert.Equal(ScreenHeight - 20, window.WindowHeight);
        Assert.Equal(10, window.Top);
    }

    [Fact]
    public void AMinimumSize_StillWins_OverTheAvailableSpace()
    {
        // A window asked to be at least 320 wide stays 320 wide on a view too narrow for it, rather than
        // silently collapsing. That is what makes "at most 720, at least 320, otherwise the view" declarable.
        MGWindow window = LoadWindow("""Width="720" MinWidth="320" ScreenMargin="40" ScreenHorizontalAlignment="Center" """, screenWidth: 200);

        Assert.Equal(320, window.WindowWidth);
    }

    [Fact]
    public void AStretchedWindow_FillsTheAxisMinusItsScreenMargin()
    {
        MGWindow window = LoadWindow("""Height="100" ScreenMargin="24" ScreenHorizontalAlignment="Stretch" """);

        Assert.Equal(ScreenWidth - 48, window.WindowWidth);
        Assert.Equal(24, window.Left);
    }

    [Fact]
    public void AnUnalignedAxis_KeepsItsDeclaredCoordinate()
    {
        // Only the axis that declares an alignment is taken over; the other still means what it said.
        MGWindow window = LoadWindow("""Left="17" Top="23" Width="300" Height="200" ScreenVerticalAlignment="Center" """);

        Assert.Equal(17, window.Left);
        Assert.Equal((ScreenHeight - 200) / 2, window.Top);
    }

    [Fact]
    public void AWindowThatDeclaresNoPlacement_IsLeftWhereItWasPut()
    {
        MGWindow window = LoadWindow("""Left="17" Top="23" Width="300" Height="200" """);

        Assert.False(window.HasScreenPlacement);
        Assert.Equal(17, window.Left);
        Assert.Equal(23, window.Top);
    }

    [Fact]
    public void ThePlacementFollowsTheView_WhateverItsSize()
    {
        // The same declaration, in two views of different sizes: the window lands correctly in each.
        MGWindow wide = LoadWindow("""Width="300" Height="200" ScreenHorizontalAlignment="Center" ScreenVerticalAlignment="Bottom" """);
        MGWindow narrow = LoadWindow("""Width="300" Height="200" ScreenHorizontalAlignment="Center" ScreenVerticalAlignment="Bottom" """,
            screenWidth: 400, screenHeight: 300);

        Assert.Equal((ScreenWidth - 300) / 2, wide.Left);
        Assert.Equal(ScreenHeight - 200, wide.Top);

        Assert.Equal((400 - 300) / 2, narrow.Left);
        Assert.Equal(300 - 200, narrow.Top);
    }

    [Fact]
    public void ARePlacement_PicksUpAChangedWindowSize()
    {
        // Placement is re-applied on every desktop tick rather than computed once, so a window that changes
        // size afterwards -- a dialogue box growing to fit its choices, say -- stays where it declared to be.
        MGWindow window = LoadWindow("""Width="300" Height="200" ScreenHorizontalAlignment="Center" ScreenVerticalAlignment="Bottom" """);
        Assert.Equal((ScreenWidth - 300) / 2, window.Left);

        window.WindowWidth = 500;
        window.WindowHeight = 400;
        window.ApplyScreenPlacement();

        Assert.Equal((ScreenWidth - 500) / 2, window.Left);
        Assert.Equal(ScreenHeight - 400, window.Top);
    }

    [Fact]
    public void PlacementIsIdempotent()
    {
        // It runs on every desktop tick, so repeating it must not walk the window across the screen.
        MGWindow window = LoadWindow("""Width="300" Height="200" ScreenMargin="10" ScreenHorizontalAlignment="Right" ScreenVerticalAlignment="Bottom" """);
        (int Left, int Top, int Width, int Height) first = (window.Left, window.Top, window.WindowWidth, window.WindowHeight);

        for (int i = 0; i < 5; i++)
        {
            window.ApplyScreenPlacement();
        }

        Assert.Equal(first, (window.Left, window.Top, window.WindowWidth, window.WindowHeight));
    }

    private static MGWindow LoadWindow(string windowAttributes, int screenWidth = ScreenWidth, int screenHeight = ScreenHeight)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, screenWidth, screenHeight));
        MGDesktop desktop = new(runtime);
        desktop.LoadDefaultResources();

        string markup = $"""
            <Window xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core" {windowAttributes}>
              <TextBlock Text="content" />
            </Window>
            """;

        return XAMLParser.LoadRootWindow(desktop, XamlDocumentSource.FromString(markup, "placement-test"), XamlLoaderMode.Strict);
    }
}
