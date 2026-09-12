using System;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Docking;
using MGUI.Core.UI.Docking.Controls;
using MGUI.Core.UI.Docking.DockLayout;
using MGUI.Shared.Input.Mouse;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Xunit;

namespace MGUI.Tests.Docking;

/// <summary>
/// Covers task 3 of the docking-bugs slice: "Onglets docking : positions d'icones, survol au
/// dessin, boutons reveles au survol ou si actif".
///
/// (A) <see cref="MGDockTabItem.GetCloseIconBounds"/> / <c>GetPinIconBounds</c> (and the
/// analogous window-state / dropdown icon bounds on <see cref="MGDockTabGroup"/>) used to read
/// the sibling button's <see cref="MGElement.LayoutBounds"/>, which the component-arrange loop
/// evaluates BEFORE <c>UpdateContentLayout</c> positions those buttons — one pass behind. They now
/// compute the rectangle from the tab item's own <see cref="MGElement.LayoutBounds"/> using the
/// same arithmetic <c>UpdateContentLayout</c> uses, so the icon lands correctly on the very first
/// layout pass (test i, vi).
///
/// (B) The surface background / accent are <see cref="MGElement.IsHovered"/>-dependent but only
/// used to refresh from a layout pass, which does not run merely from hovering. A
/// <see cref="MGElement.UpdateSelf"/> override (the same per-tick hook <see cref="MGDockHost"/>
/// uses for its own polling) now refreshes them once per tick, only when hover actually changed
/// (test v).
///
/// (C) The close/pin button + icon are now revealed only while the tab is active or hovered (and
/// only when the panel allows the action); otherwise they are set to
/// <see cref="Visibility.Hidden"/> — deliberately NOT <see cref="Visibility.Collapsed"/>: toggling
/// an element to/from Collapsed calls <c>LayoutChanged</c>, which invalidates layout up the parent
/// chain (see <c>MGElement.Visibility</c>'s setter) — exactly what (B) forbids doing merely from
/// hovering. Hidden reserves the same space, is not drawn, and is not hit-testable, which is all
/// (C) actually requires ("no reflow", "boutons révélés"), without the invalidation side effect.
/// Hidden accessories neither draw nor react to clicks (test ii, iii, iv).
/// </summary>
public class DockTabItemVisualsTests
{
    private sealed class Harness
    {
        public GraphTestRuntime Runtime;
        public MGDesktop Desktop;
        public MGWindow MainWindow;
        public MGDockHost Host;
        public DockTabGroupNode Group;
        public DockPanelNode PanelA;
        public DockPanelNode PanelB;
        public MGDockTabItem TabA; // active (first added)
        public MGDockTabItem TabB; // inactive
        public MGDockTabGroup TabGroupControl;
    }

    private static Harness CreateHarness(int width = 400, bool panelACanClose = true)
    {
        GraphTestRuntime runtime = new(new Rectangle(0, 0, 800, 600));
        MGDesktop desktop = new(runtime);
        MGWindow mainWindow = new(desktop, 0, 0, width, 200) { WindowStyle = WindowStyle.None };

        DockPanelNode panelA = new() { Title = "A", CanClose = panelACanClose, CanAutoHide = true, ContentFactory = () => new MGBorder(mainWindow) };
        DockPanelNode panelB = new() { Title = "B", CanClose = true, CanAutoHide = true, ContentFactory = () => new MGBorder(mainWindow) };

        DockTabGroupNode group = new();
        group.AddPanel(panelA, -1); // becomes active (first panel added)
        group.AddPanel(panelB, -1);

        MGDockHost host = new(mainWindow)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            LayoutModel = new DockLayoutModel(group),
        };

        mainWindow.SetContent(host);
        desktop.Windows.Add(mainWindow);

        // Two frames at the same far-away position: the framework's hover/occlusion tracking needs
        // a settled previous-frame state (see FloatingWindowContentInputTests' identical warm-up)
        // before IsHovered reflects real containment — a single cold-start frame reports every
        // element as hovered. This warm-up establishes the "one layout pass, nothing clicked since"
        // baseline that tasks (i)/(ii) exercise.
        Point farAway = new(width + 1000, 1000);
        AdvanceFrame(runtime, desktop, 0, farAway);
        AdvanceFrame(runtime, desktop, 16, farAway);

        MGDockTabGroup tabGroupControl = host.TraverseVisualTree<MGDockTabGroup>(IncludeSelf: false).Single();
        var tabItems = tabGroupControl.TraverseVisualTree<MGDockTabItem>(IncludeSelf: false).ToList();
        MGDockTabItem tabA = tabItems.Single(t => t.Panel == panelA);
        MGDockTabItem tabB = tabItems.Single(t => t.Panel == panelB);

        return new Harness
        {
            Runtime = runtime,
            Desktop = desktop,
            MainWindow = mainWindow,
            Host = host,
            Group = group,
            PanelA = panelA,
            PanelB = panelB,
            TabA = tabA,
            TabB = tabB,
            TabGroupControl = tabGroupControl,
        };
    }

    private static void AdvanceFrame(GraphTestRuntime runtime, MGDesktop desktop, int totalElapsedMs, Point position, MouseButton? pressedButton = null)
    {
        runtime.ApplyFrame(new UpdateBaseArgs(
            TimeSpan.FromMilliseconds(totalElapsedMs),
            TimeSpan.FromMilliseconds(16),
            CreateMouseState(position, pressedButton),
            new KeyboardState()));
        desktop.Update();
    }

    private static MouseState CreateMouseState(Point position, MouseButton? pressedButton)
        => new(
            position.X,
            position.Y,
            0,
            pressedButton == MouseButton.Left ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            pressedButton == MouseButton.Right ? ButtonState.Pressed : ButtonState.Released,
            ButtonState.Released,
            ButtonState.Released);

    private static Rectangle CenterOf(Rectangle bounds, int iconSize)
        => new(bounds.X + (bounds.Width - iconSize) / 2, bounds.Y + (bounds.Height - iconSize) / 2, iconSize, iconSize);

    // ── Theme densities (styling backlog task 14) ───────────────────────

    [Fact]
    public void TabItems_Follow_The_Theme_Tab_Header_Height_Button_Size_And_Title_Padding_And_The_Group_Pushes_Its_Height()
    {
        Harness harness = CreateHarness();
        MGTheme initial = harness.MainWindow.GetTheme();
        Assert.Equal(initial.Docking.TabHeaderHeight, harness.TabGroupControl.TabHeaderHeight);
        Assert.Equal(initial.Docking.TabHeaderHeight, harness.TabA.TabHeight);
        Assert.Equal(initial.Docking.TabButtonSize, harness.TabA.ButtonSize);

        MGTheme compact = initial.Copy();
        compact.Docking.TabHeaderHeight = 24;
        compact.Docking.TabButtonSize = 18;
        compact.Docking.TabTitlePadding = new MonoGame.Extended.Thickness(6, 2, 2, 2);
        harness.MainWindow.GetResources().DefaultTheme = compact;
        Point farAway = new(1400, 1000);
        AdvanceFrame(harness.Runtime, harness.Desktop, 32, farAway);
        AdvanceFrame(harness.Runtime, harness.Desktop, 48, farAway);

        // The strip, the tabs and their title padding follow the theme; the active tab reserves the new button width.
        Assert.Equal(24, harness.TabGroupControl.TabHeaderHeight);
        Assert.Equal(24, harness.TabA.TabHeight);
        Assert.Equal(24, harness.TabA.LayoutBounds.Height);
        Assert.Equal(18, harness.TabA.ButtonSize);
        Assert.Equal(18, harness.TabA.TemplateParts[MGDockTabItem.CloseButtonPartName].LayoutBounds.Width);
        Assert.Equal(new MonoGame.Extended.Thickness(6, 2, 2, 2), harness.TabA.TemplateParts[MGDockTabItem.TitleTextPartName].Padding);

        // A height set on the group by the application reaches the items that follow it, and outranks later theme changes; an item whose height the
        // application set on its own keeps it.
        harness.TabB.TabHeight = 50;
        harness.TabGroupControl.TabHeaderHeight = 40;
        Assert.Equal(40, harness.TabA.TabHeight);
        Assert.Equal(50, harness.TabB.TabHeight);
        harness.MainWindow.GetResources().DefaultTheme = initial;
        AdvanceFrame(harness.Runtime, harness.Desktop, 64, farAway);
        Assert.Equal(40, harness.TabGroupControl.TabHeaderHeight);
        Assert.Equal(40, harness.TabA.TabHeight);
        Assert.Equal(50, harness.TabB.TabHeight);
        Assert.Equal(initial.Docking.TabButtonSize, harness.TabA.ButtonSize);
    }

    [Fact]
    public void The_Host_Auto_Hide_Strip_Thickness_Follows_The_Theme_And_Reaches_Its_Strips_Unless_A_Strip_Was_Sized_By_The_Application()
    {
        Harness harness = CreateHarness();
        MGTheme initial = harness.MainWindow.GetTheme();
        MGDockAutoHideStrip left = (MGDockAutoHideStrip)harness.Host.TemplateParts[MGDockHost.LeftAutoHideStripPartName];
        MGDockAutoHideStrip right = (MGDockAutoHideStrip)harness.Host.TemplateParts[MGDockHost.RightAutoHideStripPartName];
        Assert.Equal(initial.Docking.AutoHideStripThickness, harness.Host.AutoHideStripThickness);
        Assert.Equal(initial.Docking.AutoHideStripThickness, left.StripThickness);

        right.StripThickness = 40;
        MGTheme compact = initial.Copy();
        compact.Docking.AutoHideStripThickness = 20;
        harness.MainWindow.GetResources().DefaultTheme = compact;
        Point farAway = new(1400, 1000);
        AdvanceFrame(harness.Runtime, harness.Desktop, 32, farAway);

        Assert.Equal(20, harness.Host.AutoHideStripThickness);
        Assert.Equal(20, left.StripThickness);
        Assert.Equal(40, right.StripThickness);

        // A value set on the host by the application reaches the strips that follow it.
        harness.Host.AutoHideStripThickness = 28;
        Assert.Equal(28, left.StripThickness);
        Assert.Equal(40, right.StripThickness);
    }

    [Fact]
    public void A_Tab_Item_Height_Set_By_The_Application_Survives_A_Theme_Change_Of_The_Group()
    {
        Harness harness = CreateHarness();
        harness.TabA.TabHeight = 50;

        MGTheme compact = harness.MainWindow.GetTheme().Copy();
        compact.Docking.TabHeaderHeight = 24;
        harness.MainWindow.GetResources().DefaultTheme = compact;
        Point farAway = new(1400, 1000);
        AdvanceFrame(harness.Runtime, harness.Desktop, 32, farAway);

        // The group and the untouched item follow the theme; the group's push skips the item the application sized.
        Assert.Equal(24, harness.TabGroupControl.TabHeaderHeight);
        Assert.Equal(24, harness.TabB.TabHeight);
        Assert.Equal(50, harness.TabA.TabHeight);
    }

    // ── (i) Same-pass bounds ─────────────────────────────────────────────

    [Fact]
    public void CloseAndPinIcons_AreCorrectlyPositioned_OnTheVeryFirstLayoutPass()
    {
        // The harness only performs the ONE initial frame (AdvanceFrame at t=0 inside CreateHarness).
        Harness harness = CreateHarness();

        MGElement closeButton = harness.TabA.TemplateParts[MGDockTabItem.CloseButtonPartName];
        MGElement closeIcon = harness.TabA.TemplateParts[MGDockTabItem.CloseIconPartName];
        MGElement pinButton = harness.TabA.TemplateParts[MGDockTabItem.PinButtonPartName];
        MGElement pinIcon = harness.TabA.TemplateParts[MGDockTabItem.PinIconPartName];

        Assert.True(closeButton.LayoutBounds.Width > 0 && closeButton.LayoutBounds.Height > 0);
        Assert.True(pinButton.LayoutBounds.Width > 0 && pinButton.LayoutBounds.Height > 0);

        Assert.Equal(CenterOf(closeButton.LayoutBounds, 12), closeIcon.LayoutBounds);
        Assert.Equal(CenterOf(pinButton.LayoutBounds, 12), pinIcon.LayoutBounds);
    }

    // ── (vi) MGDockTabGroup: window-state / dropdown icon bounds ──────────

    [Fact]
    public void WindowStateIcon_IsCorrectlyPositioned_OnTheVeryFirstLayoutPass()
    {
        Harness harness = CreateHarness();

        MGElement maximizeIcon = harness.TabGroupControl.TemplateParts[MGDockTabGroup.WindowStateIconPartName];

        // The maximize button itself has no template-part name (compact strip button created
        // internally); recompute its expected bounds from the group's own layout the same way
        // GetMaximizeButtonBounds() does, using the group's public TabHeadersBounds/LayoutBounds.
        Rectangle group = harness.TabGroupControl.LayoutBounds;
        const int MaximizeBtnWidth = 24; // mirrors MGDockTabGroup's private constant
        Rectangle expectedMaximizeButtonBounds = new(
            group.Right - MaximizeBtnWidth, group.Y, MaximizeBtnWidth, harness.TabGroupControl.TabHeaderHeight);

        Assert.Equal(CenterOf(expectedMaximizeButtonBounds, 14), maximizeIcon.LayoutBounds);
    }

    [Fact]
    public void DropdownIcon_IsCorrectlyPositioned_WhenOverflowing_OnTheVeryFirstLayoutPass()
    {
        // Force overflow with a narrow window so the "..." dropdown button becomes visible.
        Harness harness = CreateHarness(width: 90);

        MGElement dropdownIcon = harness.TabGroupControl.TemplateParts[MGDockTabGroup.DropdownIconPartName];
        Assert.Equal(Visibility.Visible, dropdownIcon.Visibility);

        // Unlike the window-state icon, the dropdown icon fills its whole button area rather than
        // being centred within it (this matches the pre-existing behaviour: the original buggy
        // lambda returned `_dropdownBtn.LayoutBounds` directly, uncentred — only the timing was
        // wrong, not the rectangle shape).
        Rectangle group = harness.TabGroupControl.LayoutBounds;
        const int MaximizeBtnWidth = 24;
        const int DropdownBtnWidth = 24;
        Rectangle expectedDropdownButtonBounds = new(
            group.Right - MaximizeBtnWidth - DropdownBtnWidth, group.Y, DropdownBtnWidth, harness.TabGroupControl.TabHeaderHeight);

        Assert.Equal(expectedDropdownButtonBounds, dropdownIcon.LayoutBounds);
    }

    // ── (ii) Reveal on hover/active ────────────────────────────────────────

    [Fact]
    public void InactiveUnhoveredTab_HasCloseAndPinIcons_Hidden()
    {
        Harness harness = CreateHarness();

        MGElement closeIcon = harness.TabB.TemplateParts[MGDockTabItem.CloseIconPartName];
        MGElement pinIcon = harness.TabB.TemplateParts[MGDockTabItem.PinIconPartName];

        // Hidden, not Collapsed — see class remarks: Collapsed would invalidate layout on every
        // hover change, which (B) explicitly forbids. Hidden reserves the same space, doesn't draw,
        // and isn't hit-testable, which is everything the reveal behaviour actually needs.
        Assert.Equal(Visibility.Hidden, closeIcon.Visibility);
        Assert.Equal(Visibility.Hidden, pinIcon.Visibility);
    }

    [Fact]
    public void ActiveTab_HasCloseAndPinIcons_Visible()
    {
        Harness harness = CreateHarness();

        MGElement closeIcon = harness.TabA.TemplateParts[MGDockTabItem.CloseIconPartName];
        MGElement pinIcon = harness.TabA.TemplateParts[MGDockTabItem.PinIconPartName];

        Assert.Equal(Visibility.Visible, closeIcon.Visibility);
        Assert.Equal(Visibility.Visible, pinIcon.Visibility);
    }

    [Fact]
    public void HoveringInactiveTab_InANeverClickedGroup_RevealsIcons_WithinOneUpdate_AtCorrectPosition()
    {
        Harness harness = CreateHarness();
        // The group has never been clicked/re-laid-out since the initial frame in CreateHarness.

        MGElement closeButton = harness.TabB.TemplateParts[MGDockTabItem.CloseButtonPartName];
        MGElement closeIcon = harness.TabB.TemplateParts[MGDockTabItem.CloseIconPartName];
        MGElement pinButton = harness.TabB.TemplateParts[MGDockTabItem.PinButtonPartName];
        MGElement pinIcon = harness.TabB.TemplateParts[MGDockTabItem.PinIconPartName];

        Assert.Equal(Visibility.Hidden, closeIcon.Visibility); // sanity: starts hidden

        Point onTabB = harness.TabB.LayoutBounds.Center;
        AdvanceFrame(harness.Runtime, harness.Desktop, 16, onTabB);

        Assert.True(harness.TabB.IsHovered);
        Assert.Equal(Visibility.Visible, closeIcon.Visibility);
        Assert.Equal(Visibility.Visible, pinIcon.Visibility);

        // Positioned correctly — bounds were already right from the first pass (task A); only
        // Visibility needed to flip, no fresh layout pass was required.
        Assert.Equal(CenterOf(closeButton.LayoutBounds, 12), closeIcon.LayoutBounds);
        Assert.Equal(CenterOf(pinButton.LayoutBounds, 12), pinIcon.LayoutBounds);

        // Moving away collapses them again.
        AdvanceFrame(harness.Runtime, harness.Desktop, 32, new Point(0, 0));
        Assert.False(harness.TabB.IsHovered);
        Assert.Equal(Visibility.Hidden, closeIcon.Visibility);
        Assert.Equal(Visibility.Hidden, pinIcon.Visibility);
    }

    // ── (iii) No reflow ────────────────────────────────────────────────────

    [Fact]
    public void RevealingAccessories_DoesNotReflow_TitleOrTabBounds()
    {
        Harness harness = CreateHarness();

        MGElement titleText = harness.TabB.TemplateParts[MGDockTabItem.TitleTextPartName];
        Rectangle titleBoundsBefore = titleText.LayoutBounds;
        Rectangle tabBoundsBefore = harness.TabB.LayoutBounds;

        Point onTabB = harness.TabB.LayoutBounds.Center;
        AdvanceFrame(harness.Runtime, harness.Desktop, 16, onTabB);
        Assert.True(harness.TabB.IsHovered);

        Assert.Equal(titleBoundsBefore, titleText.LayoutBounds);
        Assert.Equal(tabBoundsBefore, harness.TabB.LayoutBounds);
    }

    // ── (iv) Hidden accessories do not react ────────────────────────────────

    [Fact]
    public void ReleasingOnHiddenCloseArea_OfHoveredInactiveTab_Closes_BecauseHoverRevealsIt()
    {
        // A release frame necessarily hovers the tab first (the mouse must be positioned over the
        // close button area to release there), so a true "release while genuinely hidden" cannot be
        // synthesised — hovering itself reveals the accessory. This test documents that observable
        // boundary: releasing over the close button of a previously-inactive, previously-unhovered
        // tab DOES raise CloseRequested, because the hover from the release frame itself reveals it.
        Harness harness = CreateHarness();

        MGElement closeButton = harness.TabB.TemplateParts[MGDockTabItem.CloseButtonPartName];
        Assert.Equal(Visibility.Hidden, closeButton.Visibility); // starts hidden (not active, not hovered)

        DockPanelNode closed = null;
        bool tabClicked = false;
        harness.TabB.CloseRequested += (_, panel) => closed = panel;
        harness.TabB.TabClicked += (_, _) => tabClicked = true;

        Point onCloseButton = closeButton.LayoutBounds.Center;
        AdvanceFrame(harness.Runtime, harness.Desktop, 16, onCloseButton, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 32, onCloseButton, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 48, onCloseButton);

        Assert.Same(harness.PanelB, closed);
        Assert.False(tabClicked);
    }

    [Fact]
    public void CloseRequested_IsGatedOnCanClose_EvenWhenActiveAndHovered()
    {
        // Direct proof that the close click handler is gated on the reveal condition's
        // Panel.CanClose term specifically, not merely on geometry: CanClose is forced false from
        // construction (so the tab's layout is self-consistent for the whole test — no structural
        // relayout mid-test, unlike toggling CanClose at runtime, which reflows the button strip and
        // can shift a sibling button into the old close-button's screen position). Even though TabA
        // is active (and, once clicked on, hovered too — the strongest possible reveal condition),
        // CanClose=false means the close accessory is never reserved/revealed, so a release near the
        // tab's right edge must behave like a plain tab click, never a close.
        Harness harness = CreateHarness(panelACanClose: false);

        bool closeRequested = false;
        bool tabClicked = false;
        harness.TabA.CloseRequested += (_, _) => closeRequested = true;
        harness.TabA.TabClicked += (_, _) => tabClicked = true;

        Assert.Equal(0, GetCloseWidthViaLayout(harness.TabA)); // no reserved close area at all

        Point onTabA = harness.TabA.LayoutBounds.Center;
        AdvanceFrame(harness.Runtime, harness.Desktop, 16, onTabA, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 32, onTabA, MouseButton.Left);
        AdvanceFrame(harness.Runtime, harness.Desktop, 48, onTabA);

        Assert.False(closeRequested);
        Assert.True(tabClicked);
    }

    // The close button's own LayoutBounds is a proxy for the reserved close width computed by
    // GetCloseWidth()/GetCloseButtonBounds() (task A): zero width means CanClose is false.
    private static int GetCloseWidthViaLayout(MGDockTabItem tab)
        => tab.TemplateParts[MGDockTabItem.CloseButtonPartName].LayoutBounds.Width;

    // ── (v) Hover background without layout ────────────────────────────────

    [Fact]
    public void HoveringInactiveTab_InANeverClickedGroup_RefreshesHoverBackground_WithoutInvalidatingLayout()
    {
        Harness harness = CreateHarness();

        MGElement surface = harness.TabB.TemplateParts[MGDockTabItem.SurfacePartName];
        var border = Assert.IsType<MGBorder>(surface);

        Assert.True(harness.TabB.IsLayoutValid);
        Assert.NotEqual(harness.TabB.HoverBrush, border.BackgroundBrush.NormalValue);

        Point onTabB = harness.TabB.LayoutBounds.Center;
        AdvanceFrame(harness.Runtime, harness.Desktop, 16, onTabB);

        Assert.True(harness.TabB.IsHovered);
        Assert.Equal(harness.TabB.HoverBrush, border.BackgroundBrush.NormalValue);

        // The hover-driven refresh must not have gone through a fresh layout pass.
        Assert.True(harness.TabB.IsLayoutValid);
    }

    // ── (vii) is exercised by running DockPartVocabularyTests / other docking & input suites
    // alongside this file (see task validation commands); nothing further is asserted here.
}
