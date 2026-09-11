using System;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Behavioural coverage of the S5 wiring of the Background container pilot (ADR-0005):
/// <see cref="MGElement.BackgroundBrush"/>, a <see cref="VisualStateFillBrush"/> with five tracked sub-slots
/// (<see cref="UIValueSlot.Normal"/>, <see cref="UIValueSlot.Selected"/>, <see cref="UIValueSlot.Disabled"/>,
/// <see cref="UIValueSlot.Focused"/>, <see cref="UIValueSlot.FocusedColor"/>) plus the Whole-object replacement
/// slot. Precedence at the Whole level (R1/R2), sub-slot precedence and dormancy (R3/R4/R6), attribution of
/// non-tagged sub-field writes (R5), and re-subscription across a container swap.
/// </summary>
public class ResolvedBackgroundPilotTests
{
    /// <summary>Test-only <see cref="MGElement"/> subclass with no pilot writes of its own beyond the base
    /// constructor's Margin/Padding/Background defaults: every contribution observed on an instance of this type
    /// comes from <see cref="MGElement"/>'s own base constructor or from a test calling a tagged setter directly.</summary>
    private sealed class BackgroundProbeElement : MGElement
    {
        public BackgroundProbeElement(MGWindow window)
            : base(window, MGElementType.Custom)
        {
        }
    }

    [Fact]
    public void Constructor_Whole_Contribution_Is_DefaultValue_And_Same_As_BackgroundBrush()
    {
        Harness harness = Harness.Create();
        BackgroundProbeElement element = new(harness.Window);

        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<VisualStateFillBrush> contribution));
        Assert.Same(element.BackgroundBrush, contribution.Value);

        // ADR-0005/S6: +1 over the S5 budget (Margin, Padding, Background) -- DefaultTextForeground is now also a
        // base-constructor pilot write.
        Assert.Equal(4, element.ResolvedEntryCount);

        MGBorder border = new(harness.Window);
        Assert.Equal(6, border.ResolvedEntryCount); // Margin, Padding, Background, DefaultTextForeground, BorderBrush, BorderThickness

        int entryCountBefore = element.ResolvedEntryCount;
        element.Opacity = 0.5f;
        Assert.Equal(entryCountBefore, element.ResolvedEntryCount);
    }

    [Fact]
    public void Whole_Precedence_LocalValue_Wins_Over_Theme_And_A_Later_Theme_Write_Is_Silent()
    {
        Harness harness = Harness.Create();
        BackgroundProbeElement element = new(harness.Window);
        harness.Show(element);

        VisualStateFillBrush a = new(SolidFillBrushes.Black);
        VisualStateFillBrush b = new(SolidFillBrushes.White);
        VisualStateFillBrush c = new(new MGSolidFillBrush(Color.Lime));

        element.SetBackground(a, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        Assert.Same(a, element.BackgroundBrush);

        element.BackgroundBrush = b; // public setter => LocalValue(90)
        Assert.Same(b, element.BackgroundBrush);

        int npcCount = 0;
        element.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MGElement.BackgroundBrush)) npcCount++; };

        element.SetBackground(c, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        Assert.Same(b, element.BackgroundBrush); // local still wins
        Assert.Equal(0, npcCount); // effective value did not change, so no notification

        element.ClearPilotSource(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.LocalValue);
        Assert.Same(c, element.BackgroundBrush); // falls back to the latest Theme(20) contribution
        Assert.Equal(1, npcCount);
    }

    [Fact]
    public void Whole_Swap_Reapplies_Applicable_SubSlot_Onto_The_New_Container()
    {
        Harness harness = Harness.Create();
        BackgroundProbeElement element = new(harness.Window);
        harness.Show(element);

        MGSolidFillBrush red = new(Color.Red);
        element.SetBackgroundSlot(UIValueSlot.Normal, red, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));

        VisualStateFillBrush themeClone = new(SolidFillBrushes.Black);
        element.SetBackground(themeClone, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));

        Assert.Same(themeClone, element.BackgroundBrush);
        // MGSolidFillBrush is a value type: boxed on every conversion to IFillBrush, so two boxes of an equal
        // struct are never ReferenceEquals -- content equality is the correct check (see ResolvedThemeChangeTests'
        // identical note about MGUniformBorderBrush).
        Assert.Equal(red, themeClone.NormalValue);

        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> normalWinner));
        Assert.Equal(UIValueSourceKind.LocalValue, normalWinner.Source.Kind);
        Assert.Equal(red, normalWinner.Value);

        // Named mutation (run by hand): stop calling ReapplyBackgroundSubSlots from ApplyBackgroundEffective (R2)
        // -- this assertion goes red because themeClone.NormalValue is never overwritten with `red`.
    }

    [Fact]
    public void Equal_Precedence_Whole_Write_Clears_The_SubSlot_Contribution_At_The_Same_Precedence()
    {
        Harness harness = Harness.Create();
        BackgroundProbeElement element = new(harness.Window);
        harness.Show(element);

        MGSolidFillBrush red = new(Color.Red);
        element.SetBackgroundSlot(UIValueSlot.Normal, red, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));

        VisualStateFillBrush blue = new(new MGSolidFillBrush(Color.Blue));
        element.BackgroundBrush = blue; // public setter => LocalValue(90), same precedence as the sub-slot write above

        Assert.Same(blue, element.BackgroundBrush);
        Assert.Equal(blue.NormalValue, element.BackgroundBrush.NormalValue); // blue's own Normal, not red
        Assert.NotEqual(red, element.BackgroundBrush.NormalValue);

        Assert.False(element.TryGetResolvedContribution<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out _));

        // Named mutation (run by hand): stop calling UnsetBackgroundSubSlotsAtPrecedence from SetBackground (R1)
        // -- this assertion goes red because the stale Normal(LocalValue) contribution survives the Whole swap.
    }

    [Fact]
    public void Dormant_SubSlot_Contribution_Resurfaces_When_Its_Precedence_Becomes_Applicable_Again()
    {
        Harness harness = Harness.Create();
        BackgroundProbeElement element = new(harness.Window);
        harness.Show(element);

        VisualStateFillBrush defaultContainer = element.BackgroundBrush;
        MGSolidFillBrush transparent = SolidFillBrushes.Transparent;
        element.SetBackgroundAll(transparent, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        Assert.Equal(transparent, element.BackgroundBrush.NormalValue);

        VisualStateFillBrush sel = new(new MGSolidFillBrush(Color.Yellow));
        element.SetBackground(sel, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));

        Assert.Same(sel, element.BackgroundBrush);
        Assert.NotEqual(transparent, sel.NormalValue); // dormant: sel keeps its own constructed NormalValue

        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> dormantWinner));
        Assert.Equal(UIValueSourceKind.VisualState, dormantWinner.Source.Kind);
        Assert.Equal(sel.NormalValue, dormantWinner.Value); // the container carries the value while dormant

        element.ClearPilotSource(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.VisualState);
        Assert.Same(defaultContainer, element.BackgroundBrush);
        Assert.Equal(transparent, element.BackgroundBrush.NormalValue); // Theme(20) re-applied

        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> resurfaced));
        Assert.Equal(UIValueSourceKind.Theme, resurfaced.Source.Kind);
    }

    [Fact]
    public void NonTagged_SubField_Write_Is_Attributed_LocalValue_And_Suppressed_During_A_Tagged_Write()
    {
        Harness harness = Harness.Create();
        BackgroundProbeElement element = new(harness.Window);
        harness.Show(element);

        MGSolidFillBrush x = new(Color.Magenta);
        element.BackgroundBrush.NormalValue = x; // non-tagged: application code mutating the container directly

        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<IFillBrush> contribution));
        Assert.Equal(x, contribution.Value);

        BackgroundProbeElement fresh = new(harness.Window);
        harness.Show(fresh);
        MGSolidFillBrush y = new(Color.Cyan);
        fresh.SetBackgroundSlot(UIValueSlot.Normal, y, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));

        // The tagged write suppresses the container's own PropertyChanged handler while it writes physically, so
        // no LocalValue contribution is recorded even though the container's NormalValue setter fired.
        Assert.False(fresh.TryGetResolvedContribution<IFillBrush>(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out _));
    }

    [Fact]
    public void ReSubscription_On_Container_Swap_Only_Attributes_Writes_To_The_Current_Container()
    {
        Harness harness = Harness.Create();
        BackgroundProbeElement element = new(harness.Window);
        harness.Show(element);

        VisualStateFillBrush oldContainer = element.BackgroundBrush;
        VisualStateFillBrush newContainer = new(SolidFillBrushes.Black);
        element.BackgroundBrush = newContainer; // public setter => container swap + re-subscription

        MGSolidFillBrush z = new(Color.Green);
        newContainer.NormalValue = z;
        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<IFillBrush> afterNew));
        Assert.Equal(z, afterNew.Value);

        MGSolidFillBrush w = new(Color.Orange);
        oldContainer.NormalValue = w; // the old container is unsubscribed: this must not reach the store

        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<IFillBrush> afterOld));
        Assert.Equal(z, afterOld.Value); // unchanged
    }

    [Fact]
    public void Real_Theme_Change_Reapplies_LocalValue_SubSlot_Onto_The_New_Theme_Container()
    {
        Harness harness = Harness.Create();
        MGButton button = new(harness.Window);
        harness.Show(button);

        VisualStateFillBrush beforeContainer = button.BackgroundBrush;
        MGSolidFillBrush red = new(Color.Red);
        button.SetBackgroundSlot(UIValueSlot.Normal, red, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        Assert.NotSame(beforeContainer, button.BackgroundBrush);
        Assert.True(button.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> wholeWinner));
        Assert.Equal(UIValueSourceKind.Theme, wholeWinner.Source.Kind);
        Assert.Equal(red, button.BackgroundBrush.NormalValue);
    }

    [Fact]
    public void Real_Theme_Change_Does_Not_Clobber_A_Local_Whole_Container()
    {
        Harness harness = Harness.Create();
        MGButton button = new(harness.Window);
        harness.Show(button);

        VisualStateFillBrush localContainer = new(new MGSolidFillBrush(Color.Purple));
        button.BackgroundBrush = localContainer; // public setter => LocalValue(90)

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        // Behavior change accepted by the author (ADR-0005/S5): a theme refresh no longer clobbers a local value --
        // pre-ADR-0005, MGButton.OnThemeChanged assigned BackgroundBrush unconditionally.
        Assert.Same(localContainer, button.BackgroundBrush);
        Assert.True(button.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> winner));
        Assert.Equal(UIValueSourceKind.LocalValue, winner.Source.Kind);

        // The Theme contribution is still recorded, just not effective.
        Assert.True(button.TryGetResolvedContribution<VisualStateFillBrush>(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.Theme, out _));
    }

    [Fact]
    public void TreeViewItem_Selection_Writes_VisualState_Background_And_Deselection_Restores_The_Previous_Instance()
    {
        Harness harness = Harness.Create();
        MGTreeView treeView = new(harness.Window);
        MGTreeViewItem item = new(harness.Window) { Header = "Item A" };
        item.AddItem(new MGTreeViewItem(harness.Window) { Header = "Child" });
        treeView.AddItem(item);
        harness.Show(treeView);

        MGBorder headerContainer = GetHeaderContainer(item);
        VisualStateFillBrush previous = headerContainer.BackgroundBrush;

        treeView.SelectItem(item);

        Assert.NotSame(previous, headerContainer.BackgroundBrush);
        // ADR-0005/S5 deviation from the literal brief text (reported): RefreshSelectionVisual copies
        // OwnerTreeView.SelectionBackgroundBrush instead of sharing the exact instance (see the comment in
        // MGTreeViewItem.cs), so this asserts value-equivalence rather than Assert.Same to that source object.
        Assert.Equal(treeView.SelectionBackgroundBrush.NormalValue, headerContainer.BackgroundBrush.NormalValue);
        Assert.True(headerContainer.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> selectedWinner));
        Assert.Equal(UIValueSourceKind.VisualState, selectedWinner.Source.Kind);

        treeView.SelectItem(null);

        Assert.Same(previous, headerContainer.BackgroundBrush);
        Assert.False(headerContainer.TryGetResolvedContribution<VisualStateFillBrush>(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.VisualState, out _));

        // Select again, then a real theme change, then deselect: must not throw and must land on the post-theme winner.
        treeView.SelectItem(item);
        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);
        Exception ex = Record.Exception(() => treeView.SelectItem(null));
        Assert.Null(ex);
    }

    [Fact]
    public void TreeViewItem_Expander_Theme_Transparent_SubSlots_Stay_Dormant_While_Selected_And_Resurface_On_Deselection()
    {
        Harness harness = Harness.Create();
        MGTreeView treeView = new(harness.Window);
        MGTreeViewItem item = new(harness.Window) { Header = "Item A" };
        item.AddItem(new MGTreeViewItem(harness.Window) { Header = "Child" });
        treeView.AddItem(item);
        harness.Show(treeView);

        MGToggleButton expander = item.EnumerateVisualTree(true).OfType<MGToggleButton>().First();

        // ApplyNeutralChrome's Theme(20) SetAll is applied on top of the base constructor's Default(0) container.
        // MGSolidFillBrush is a readonly struct (every boxing is a distinct object), so the transparent checks below
        // compare by value, never by reference.
        VisualStateFillBrush neutralContainer = expander.BackgroundBrush;
        Assert.Equal((IFillBrush)SolidFillBrushes.Transparent,neutralContainer.NormalValue);
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> neutralNormal));
        Assert.Equal(UIValueSourceKind.Theme, neutralNormal.Source.Kind);

        treeView.SelectItem(item);

        // The VisualState(70) selection copy shadows the Theme(20) sub-slots: they stay recorded but dormant, and the
        // selection container keeps its own physical values (RefreshSelectionVisual's SetAll of the selection brush).
        Assert.NotSame(neutralContainer, expander.BackgroundBrush);
        Assert.NotEqual((IFillBrush)SolidFillBrushes.Transparent,expander.BackgroundBrush.NormalValue);
        Assert.True(expander.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.Theme, out UIResolvedValue<IFillBrush> dormantTheme));
        Assert.Equal((IFillBrush)SolidFillBrushes.Transparent,dormantTheme.Value);
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> selectedNormal));
        Assert.Equal(UIValueSourceKind.VisualState, selectedNormal.Source.Kind);

        // A real theme change while selected: MGToggleButton.OnThemeChanged writes a Theme(20) Whole (which drops the
        // Theme sub-slots recorded at that same precedence, R1), then ApplyNeutralChrome re-records them -- still
        // dormant under the VisualState(70) selection container, which keeps showing the selection brush.
        // Known pre-existing defect (reported with S5, not introduced by it): the theme refresh re-creates the tree
        // view's template structure (ApplyControlTemplate resolves a different template instance, so a new
        // OuterBorder/ScrollViewer/ItemsPanel is attached) and the existing items are orphaned in the old panel, so
        // NotifyThemeChanged's visual-tree recursion never reaches them. The item subtree is therefore notified
        // explicitly below with the same internal call the recursion would have made; once the structure refresh
        // is fixed the item is simply notified twice, which is idempotent for every write involved here.
        MGTheme previousTheme = harness.Window.GetTheme();
        MGTheme newTheme = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = newTheme;
        harness.Frame(100, Point.Zero);
        item.NotifyThemeChanged(previousTheme, newTheme);
        Assert.NotEqual((IFillBrush)SolidFillBrushes.Transparent,expander.BackgroundBrush.NormalValue);
        Assert.True(expander.TryGetResolvedContribution<VisualStateFillBrush>(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.Theme, out _));

        treeView.SelectItem(null);

        // Deselection clears the VisualState Whole: the Theme(20) clone written during the theme change becomes the
        // effective container and the dormant Theme(20) transparent sub-slots resurface onto it (R2). Named mutation
        // (run by hand): stop re-applying the sub-slots in ApplyBackgroundEffective -- the expander keeps the theme's
        // toggle-button background instead of turning transparent again, and the two transparent Assert.Equal below go red.
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> restoredWhole));
        Assert.Equal(UIValueSourceKind.Theme, restoredWhole.Source.Kind);
        Assert.Equal((IFillBrush)SolidFillBrushes.Transparent,expander.BackgroundBrush.NormalValue);
        Assert.Equal((IFillBrush)SolidFillBrushes.Transparent,expander.BackgroundBrush.SelectedValue);
        Assert.True(expander.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Normal, out UIResolvedValue<IFillBrush> restoredNormal));
        Assert.Equal(UIValueSourceKind.Theme, restoredNormal.Source.Kind);
    }

    [Fact]
    public void MenuBarItem_ContentWrapper_Background_Follows_A_Real_Theme_Change_Without_Freezing_At_Construction()
    {
        Harness harness = Harness.Create();
        MGMenuBar menuBar = new(harness.Window);
        MGMenuBarItem item = new(menuBar, new MGTextBlock(harness.Window, "Item A"));
        menuBar.Items.Add(item);
        harness.Show(menuBar);

        Assert.True(item.ContentWrapper.TryGetResolvedContribution<VisualStateFillBrush>(UIPilotProperty.Background, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out _));

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        // The factory's Default(0) write does not freeze ContentWrapper's Background against the Theme(20) write
        // MGMenuBarItem.OnThemeChanged re-issues on every real theme change.
        Assert.True(item.ContentWrapper.TryGetResolvedPilotValue(UIPilotProperty.Background, UIValueSlot.Whole, out UIResolvedValue<VisualStateFillBrush> winner));
        Assert.Equal(UIValueSourceKind.Theme, winner.Source.Kind);
    }

    [Fact]
    public void Xaml_Explicit_Background_Is_LocalValue_And_Survives_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <Button Name=""Probe"" Background=""Red"" Content=""Hi"" />
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        // Window.WindowStyle defaults to Default (a title bar with its own MGButton close button), so filter by
        // Name rather than taking the first MGButton in visual-tree order.
        MGButton probe = window.EnumerateVisualTree(true).OfType<MGButton>().First(b => b.Name == "Probe");

        Assert.True(probe.TryGetResolvedContribution(UIPilotProperty.Background, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<IFillBrush> contribution));
        Assert.IsType<MGSolidFillBrush>(probe.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Red, ((MGSolidFillBrush)probe.BackgroundBrush.NormalValue).Color);

        window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Desktop.Update();

        Assert.IsType<MGSolidFillBrush>(probe.BackgroundBrush.NormalValue);
        Assert.Equal(Color.Red, ((MGSolidFillBrush)probe.BackgroundBrush.NormalValue).Color);
    }

    private static MGBorder GetHeaderContainer(MGTreeViewItem item)
    {
        // HeaderContainer is a private property of MGTreeViewItem; no public accessor exists for it.
        PropertyInfo property = typeof(MGTreeViewItem).GetProperty("HeaderContainer", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(property);
        return Assert.IsType<MGBorder>(property.GetValue(item));
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
            Harness harness = new(runtime, desktop, window);
            harness.Frame(0, Point.Zero);
            return harness;
        }

        /// <summary>Adds the element to the window, shows the window and runs two warm-up frames so layout is settled.</summary>
        public void Show(MGElement element)
        {
            Window.SetContent(element);
            if (!Desktop.Windows.Contains(Window))
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
