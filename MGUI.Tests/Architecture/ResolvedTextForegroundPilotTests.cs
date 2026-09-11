using System;
using System.Linq;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Behavioural coverage of the S6 wiring of the text foreground pilot (ADR-0005): the two CLR containers that
/// carry it, <see cref="MGElement.DefaultTextForeground"/> (<see cref="UIPilotProperty.DefaultTextForeground"/>)
/// and <see cref="MGTextBlock.Foreground"/> (<see cref="UIPilotProperty.Foreground"/>). Precedence at the Whole
/// level (R1/R2), sub-slot precedence and dormancy (R3/R4/R6), attribution of non-tagged sub-field writes (R5),
/// re-subscription across a container swap, and <see cref="MGTextBlock.ActualForeground"/>'s Local &gt; Inherited
/// &gt; Theme fall-back chain as read both physically and through the diagnostic <c>TryGetResolvedPilotValue</c>.
/// </summary>
public class ResolvedTextForegroundPilotTests
{
    /// <summary>Test-only <see cref="MGElement"/> subclass with no pilot writes of its own beyond the base
    /// constructor's Margin/Padding/Background/DefaultTextForeground defaults.</summary>
    private sealed class TextForegroundProbeElement : MGElement
    {
        public TextForegroundProbeElement(MGWindow window)
            : base(window, MGElementType.Custom)
        {
        }
    }

    [Fact]
    public void Constructor_Whole_Contribution_Is_DefaultValue_And_Same_As_The_Public_Containers()
    {
        Harness harness = Harness.Create();
        TextForegroundProbeElement element = new(harness.Window);

        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.DefaultTextForeground, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<VisualStateSetting<Color?>> elementContribution));
        Assert.Same(element.DefaultTextForeground, elementContribution.Value);

        // ADR-0005/S6: +1 over the S5 budget (Margin, Padding, Background) -- DefaultTextForeground is now also a
        // base-constructor pilot write.
        Assert.Equal(4, element.ResolvedEntryCount);

        MGBorder border = new(harness.Window);
        Assert.Equal(6, border.ResolvedEntryCount); // Margin, Padding, Background, DefaultTextForeground, BorderBrush, BorderThickness

        MGTextBlock textBlock = new(harness.Window, "x");
        Assert.True(textBlock.TryGetResolvedContribution(UIPilotProperty.Foreground, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out UIResolvedValue<VisualStateSetting<Color?>> textBlockContribution));
        Assert.Same(textBlock.Foreground, textBlockContribution.Value);
        Assert.Equal(5, textBlock.ResolvedEntryCount); // Margin, Padding, Background, DefaultTextForeground, Foreground

        int entryCountBefore = element.ResolvedEntryCount;
        element.Opacity = 0.5f;
        Assert.Equal(entryCountBefore, element.ResolvedEntryCount);
    }

    [Fact]
    public void Whole_Precedence_LocalValue_Wins_Over_Theme_And_A_Later_Theme_Write_Is_Silent()
    {
        Harness harness = Harness.Create();
        TextForegroundProbeElement element = new(harness.Window);
        harness.Show(element);

        VisualStateSetting<Color?> a = new(Color.Red, Color.Red, Color.Red);
        VisualStateSetting<Color?> b = new(Color.Blue, Color.Blue, Color.Blue);
        VisualStateSetting<Color?> c = new(Color.Lime, Color.Lime, Color.Lime);

        element.SetDefaultTextForeground(a, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        Assert.Same(a, element.DefaultTextForeground);

        element.DefaultTextForeground = b; // public setter => LocalValue(90)
        Assert.Same(b, element.DefaultTextForeground);

        int npcCount = 0;
        element.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(MGElement.DefaultTextForeground)) npcCount++; };

        element.SetDefaultTextForeground(c, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        Assert.Same(b, element.DefaultTextForeground); // local still wins
        Assert.Equal(0, npcCount); // effective value did not change, so no notification

        element.ClearPilotSource(UIPilotProperty.DefaultTextForeground, UIValueSlot.Whole, UIValueSourceKind.LocalValue);
        Assert.Same(c, element.DefaultTextForeground); // falls back to the latest Theme(20) contribution
        Assert.Equal(1, npcCount);
    }

    [Fact]
    public void Local_Beats_Inherited_Beats_Theme_End_To_End()
    {
        Harness harness = Harness.Create();
        MGBorder border = new(harness.Window);
        MGTextBlock textBlock = new(harness.Window, "x");
        border.SetContent(textBlock);
        harness.Show(border);

        // (i) Nothing set anywhere: falls all the way through to the theme's fallback foreground.
        Color themeFallback = harness.Window.GetTheme().TextBlockFallbackForeground.GetValue(false).GetValue(PrimaryVisualState.Normal);
        Assert.Equal(themeFallback, textBlock.ActualForeground);
        Assert.True(textBlock.TryGetResolvedPilotValue(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIResolvedValue<Color?> themeDiagnostic));
        Assert.Equal(UIValueSourceKind.Theme, themeDiagnostic.Source.Kind);

        // (ii) The ancestor border's DefaultTextForeground is written directly (non-tagged, attributed LocalValue
        // on the border) -- the text block has nothing of its own, so it inherits the border's color.
        border.DefaultTextForeground.NormalValue = Color.Red;
        Assert.Equal(Color.Red, textBlock.ActualForeground);
        Assert.True(textBlock.TryGetResolvedPilotValue(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIResolvedValue<Color?> inheritedDiagnostic));
        Assert.Equal(UIValueSourceKind.Inherited, inheritedDiagnostic.Source.Kind);
        Assert.Equal(Color.Red, inheritedDiagnostic.Value);

        // (iii) The text block's own Foreground is written directly (non-tagged, attributed LocalValue) -- local
        // now wins over the inherited ancestor color.
        textBlock.Foreground.NormalValue = Color.Blue;
        Assert.Equal(Color.Blue, textBlock.ActualForeground);
        Assert.True(textBlock.TryGetResolvedPilotValue(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIResolvedValue<Color?> localDiagnostic));
        Assert.Equal(UIValueSourceKind.LocalValue, localDiagnostic.Source.Kind);

        // (iv) Changing the ancestor again does not affect the text block: local still wins.
        border.DefaultTextForeground.NormalValue = Color.Yellow;
        Assert.Equal(Color.Blue, textBlock.ActualForeground);

        // Clearing the text block's LocalValue Normal contribution empties the entry: the store's documented
        // fall-back keeps the current physical value (Blue) instead of reverting it.
        textBlock.ClearPilotSource(UIPilotProperty.Foreground, UIValueSlot.Normal, UIValueSourceKind.LocalValue);
        Assert.Equal(Color.Blue, textBlock.ActualForeground);

        // Explicitly "erasing" the Normal slot with a Theme(20) null now falls back to the (Yellow) inherited value.
        textBlock.SetForegroundSlot(UIValueSlot.Normal, null, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        Assert.Equal(Color.Yellow, textBlock.ActualForeground);

        // Named mutation (run by hand): make ActualForeground/TryGetResolvedPilotValue ignore
        // DerivedDefaultTextForeground -- assertions (ii) and the final Yellow assertion above go red.
    }

    [Fact]
    public void Real_Theme_Change_Does_Not_Clobber_A_Local_Foreground_On_The_ShortcutText_Template_Part()
    {
        Harness harness = Harness.Create();
        MGContextMenu menu = new(harness.Window, "");
        MGContextMenuButton item = menu.AddButton("Item A", _ => { });
        harness.Show(menu);

        Assert.True(item.TryGetTemplatePart(MGWrappedContextMenuItem.ShortcutTextPartName, out MGElement shortcutPart));
        MGTextBlock shortcut = Assert.IsType<MGTextBlock>(shortcutPart);

        Assert.True(shortcut.TryGetResolvedContribution<VisualStateSetting<Color?>>(UIPilotProperty.Foreground, UIValueSlot.Whole, UIValueSourceKind.Template, out _));

        VisualStateSetting<Color?> purple = new(Color.Purple, Color.Purple, Color.Purple);
        shortcut.Foreground = purple; // public setter => LocalValue(90)

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        Assert.Equal(Color.Purple, shortcut.ActualForeground);
        Assert.True(shortcut.TryGetResolvedPilotValue(UIPilotProperty.Foreground, UIValueSlot.Whole, out UIResolvedValue<VisualStateSetting<Color?>> winner));
        Assert.Equal(UIValueSourceKind.LocalValue, winner.Source.Kind);
        // The Template contribution is still recorded, just not effective.
        Assert.True(shortcut.TryGetResolvedContribution<VisualStateSetting<Color?>>(UIPilotProperty.Foreground, UIValueSlot.Whole, UIValueSourceKind.Template, out _));
    }

    [Fact]
    public void Window_TitleBarText_Template_Follows_A_Custom_Theme()
    {
        Harness harness = Harness.Create();

        // ADR-0005/S6 deviation from the literal brief text (reported): a WINDOW cannot be driven through a real
        // *runtime* theme change here the way ResolvedBackgroundPilotTests drives a plain element or MGButton --
        // MGWindow rebuilds its entire template structure (Border/TitleBar/TitleText/CloseButton, all brand new
        // instances) on every NotifyThemeChanged, and ApplyThemeDefault's IsThemeRefresh guard then compares the
        // new child's freshly-constructed placeholder value against the OLD structure's applied value (a
        // different object reference) and bails out, skipping the SetValue call -- confirmed to also affect the
        // already-migrated Margin scalar pilot on the same part, so this is a pre-existing MGWindow structural
        // defect, not introduced by this slice and not specific to DefaultTextForeground. A custom theme supplied
        // at construction (the same pattern as ResolvedTemplatePilotsTests' ContextMenuItem_Header_And_Shortcut_
        // Margins_Follow_A_Custom_Theme_Set_Before_Construction) still exercises the Template write path faithfully.
        MGTheme customTheme = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        customTheme.Window.TitleTextForeground = new VisualStateSetting<Color?>(Color.Orange, Color.Orange, Color.Orange);

        MGWindow window = new(harness.Desktop, 0, 0, 400, 300, customTheme);
        harness.Desktop.Windows.Add(window);
        harness.Frame(0, Point.Zero);
        harness.Frame(1, Point.Zero);

        Assert.True(window.TryGetTemplatePart(MGWindow.TitleBarTextPartName, out MGElement titleTextPart));
        MGTextBlock titleText = Assert.IsType<MGTextBlock>(titleTextPart);

        Assert.Equal(Color.Orange, titleText.ActualForeground);
        Assert.True(titleText.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Whole, out UIResolvedValue<VisualStateSetting<Color?>> winner));
        Assert.Equal(UIValueSourceKind.Template, winner.Source.Kind);

        // ADR-0005/S5-style copy: the catalogue copies the theme's raw container instead of handing out the same
        // shared reference to every window built on this theme.
        Assert.NotSame(customTheme.Window.TitleTextForeground, titleText.DefaultTextForeground);
    }

    [Fact]
    public void TreeViewItem_Selection_Writes_VisualState_DefaultTextForeground_And_Deselection_Restores_The_Previous_Instance()
    {
        Harness harness = Harness.Create();
        MGTreeView treeView = new(harness.Window);
        MGTreeViewItem item = new(harness.Window) { Header = "Item A" };
        item.AddItem(new MGTreeViewItem(harness.Window) { Header = "Child" });
        treeView.AddItem(item);
        harness.Show(treeView);

        MGBorder headerContainer = GetHeaderContainer(item);
        VisualStateSetting<Color?> previous = headerContainer.DefaultTextForeground;

        treeView.SelectItem(item);

        Assert.NotSame(previous, headerContainer.DefaultTextForeground);
        Assert.Equal(treeView.SelectionForeground, headerContainer.DefaultTextForeground.NormalValue);
        Assert.True(headerContainer.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Whole, out UIResolvedValue<VisualStateSetting<Color?>> selectedWinner));
        Assert.Equal(UIValueSourceKind.VisualState, selectedWinner.Source.Kind);

        treeView.SelectItem(null);

        Assert.Same(previous, headerContainer.DefaultTextForeground);
        Assert.False(headerContainer.TryGetResolvedContribution<VisualStateSetting<Color?>>(UIPilotProperty.DefaultTextForeground, UIValueSlot.Whole, UIValueSourceKind.VisualState, out _));

        // Select again, then a real theme change, then deselect: must not throw (same known pre-existing
        // TreeView-item-orphaning defect documented in ResolvedBackgroundPilotTests -- unrelated to this pilot).
        treeView.SelectItem(item);
        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);
        Exception ex = Record.Exception(() => treeView.SelectItem(null));
        Assert.Null(ex);
    }

    [Fact]
    public void MenuBar_ContentWrapper_DefaultTextForeground_Follows_A_Real_Theme_Change_Without_Freezing_At_Construction()
    {
        Harness harness = Harness.Create();
        MGMenuBar menuBar = new(harness.Window);
        MGMenuBarItem item = new(menuBar, new MGTextBlock(harness.Window, "Item A"));
        menuBar.Items.Add(item);
        harness.Show(menuBar);

        Assert.True(item.ContentWrapper.TryGetResolvedContribution<VisualStateSetting<Color?>>(UIPilotProperty.DefaultTextForeground, UIValueSlot.Whole, UIValueSourceKind.DefaultValue, out _));

        harness.Window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        Color? themeNormal = harness.Window.GetTheme().TextBlockFallbackForeground.GetValue(true).NormalValue;
        Assert.Equal(themeNormal, item.ContentWrapper.DefaultTextForeground.NormalValue);
        Assert.True(item.ContentWrapper.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, out UIResolvedValue<Color?> winner));
        Assert.Equal(UIValueSourceKind.Theme, winner.Source.Kind);
    }

    [Fact]
    public void Xaml_TextForeground_And_Foreground_Survive_A_Real_Theme_Change()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""200"" Height=""150"">
    <StackPanel Orientation=""Vertical"">
        <Border Name=""ProbeBorder"" TextForeground=""Blue"">
            <TextBlock Name=""InheritedProbe"" Text=""x"" />
        </Border>
        <TextBlock Name=""LocalProbe"" Text=""y"" Foreground=""Red"" />
    </StackPanel>
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Desktop.Update();

        MGBorder probeBorder = window.GetElementByName<MGBorder>("ProbeBorder");
        MGTextBlock inheritedProbe = window.GetElementByName<MGTextBlock>("InheritedProbe");
        MGTextBlock localProbe = window.GetElementByName<MGTextBlock>("LocalProbe");

        Assert.True(probeBorder.TryGetResolvedContribution(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<Color?> borderContribution));
        Assert.Equal(Color.Blue, borderContribution.Value);
        Assert.Equal(Color.Blue, inheritedProbe.ActualForeground);

        // ADR-0005/S6 deviation from the literal brief text (reported): the brief expected Controls.cs:3248's
        // non-tagged `TextBlock.Foreground.NormalValue = ...` write to be attributed LocalValue via R5, the same
        // way the Background pilot's Xaml_Explicit_Background_Is_LocalValue test observes it for MGButton.
        // Unlike Background (ApplyExplicitBackground is a TAGGED SetBackgroundSlot call, which always records a
        // contribution regardless of value equality), MGTextBlock's constructor (Controls.cs:3208) already passes
        // the same XAML `Foreground` color into the tagged Default(Draw) SetForeground call, physically setting
        // NormalValue to Red. Controls.cs:3248 then re-assigns the SAME value: VisualStateSetting<T>.NormalValue's
        // setter guards on equality before raising PropertyChanged, so the second (redundant) write never fires
        // HandleForegroundContainerPropertyChanged and no Normal sub-slot contribution is EVER recorded at all
        // (neither DefaultValue nor LocalValue) -- only the constructor's Whole-slot DefaultValue contribution
        // exists, and the diagnostic read falls back to R6's physical-container reading, which still reports the
        // correct Red value under that Whole winner's DefaultValue source.
        Assert.True(localProbe.TryGetResolvedPilotValue(UIPilotProperty.Foreground, UIValueSlot.Normal, out UIResolvedValue<Color?> foregroundContribution));
        Assert.Equal(UIValueSourceKind.DefaultValue, foregroundContribution.Source.Kind);
        Assert.Equal(Color.Red, foregroundContribution.Value);

        window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Desktop.Update();

        Assert.Equal(Color.Blue, probeBorder.DefaultTextForeground.NormalValue);
        Assert.Equal(Color.Red, localProbe.Foreground.NormalValue);
    }

    [Fact]
    public void Whole_Swap_Reapplies_Applicable_SubSlot_Onto_The_New_Container()
    {
        Harness harness = Harness.Create();
        TextForegroundProbeElement element = new(harness.Window);
        harness.Show(element);

        // Written while the Default(0) container is still active, so it also lands physically on that container
        // right away -- the interesting assertion is what happens to the BRAND NEW container swapped in next.
        element.SetDefaultTextForegroundSlot(UIValueSlot.Normal, Color.Red, UIValueResolutionSource.LocalValue(UIInvalidationKind.Draw));

        VisualStateSetting<Color?> themeClone = new(Color.Black, Color.Black, Color.Black);
        element.SetDefaultTextForeground(themeClone, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));

        Assert.Same(themeClone, element.DefaultTextForeground);
        // themeClone was constructed with Black, and nothing wrote Red to IT directly -- only R2's re-application
        // (LocalValue(90) >= Theme(20)) could have put Red there.
        Assert.Equal(Color.Red, themeClone.NormalValue);

        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, out UIResolvedValue<Color?> normalWinner));
        Assert.Equal(UIValueSourceKind.LocalValue, normalWinner.Source.Kind);
        Assert.Equal(Color.Red, normalWinner.Value);

        // Named mutation (run by hand): stop calling ReapplyDefaultTextForegroundSubSlots from
        // ApplyDefaultTextForegroundEffective (R2) -- this assertion goes red because themeClone.NormalValue is
        // never overwritten with Red.
    }

    [Fact]
    public void Dormant_SubSlot_Contribution_Resurfaces_When_Its_Precedence_Becomes_Applicable_Again()
    {
        Harness harness = Harness.Create();
        TextForegroundProbeElement element = new(harness.Window);
        harness.Show(element);

        VisualStateSetting<Color?> defaultContainer = element.DefaultTextForeground;
        element.SetDefaultTextForegroundAll(Color.Red, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        Assert.Equal(Color.Red, element.DefaultTextForeground.NormalValue);

        VisualStateSetting<Color?> sel = new(Color.Yellow, Color.Yellow, Color.Yellow);
        element.SetDefaultTextForeground(sel, UIValueResolutionSource.VisualState(UIInvalidationKind.Draw));

        Assert.Same(sel, element.DefaultTextForeground);
        Assert.NotEqual(Color.Red, sel.NormalValue); // dormant: sel keeps its own constructed NormalValue

        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, out UIResolvedValue<Color?> dormantWinner));
        Assert.Equal(UIValueSourceKind.VisualState, dormantWinner.Source.Kind);

        element.ClearPilotSource(UIPilotProperty.DefaultTextForeground, UIValueSlot.Whole, UIValueSourceKind.VisualState);
        Assert.Same(defaultContainer, element.DefaultTextForeground);
        Assert.Equal(Color.Red, element.DefaultTextForeground.NormalValue); // Theme(20) still there (it was
        // already physically set on defaultContainer before the VisualState swap, and R2's re-application on the
        // swap-back keeps it that way even under the M2 mutation, since it was never actually overwritten).

        Assert.True(element.TryGetResolvedPilotValue(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, out UIResolvedValue<Color?> resurfaced));
        Assert.Equal(UIValueSourceKind.Theme, resurfaced.Source.Kind);
    }

    [Fact]
    public void NonTagged_Write_Is_Attributed_LocalValue_And_ReSubscription_Follows_A_Container_Swap_On_Both_Containers()
    {
        Harness harness = Harness.Create();

        // -- MGElement.DefaultTextForeground --
        TextForegroundProbeElement element = new(harness.Window);
        harness.Show(element);

        element.DefaultTextForeground.NormalValue = Color.Magenta; // non-tagged: application code mutating the container directly
        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<Color?> elementContribution));
        Assert.Equal(Color.Magenta, elementContribution.Value);

        TextForegroundProbeElement freshElement = new(harness.Window);
        harness.Show(freshElement);
        freshElement.SetDefaultTextForegroundSlot(UIValueSlot.Normal, Color.Cyan, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        // The tagged write suppresses the container's own PropertyChanged handler while it writes physically, so
        // no LocalValue contribution is recorded even though the container's NormalValue setter fired.
        Assert.False(freshElement.TryGetResolvedContribution<Color?>(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out _));

        VisualStateSetting<Color?> oldElementContainer = element.DefaultTextForeground;
        VisualStateSetting<Color?> newElementContainer = new(Color.Black, Color.Black, Color.Black);
        element.DefaultTextForeground = newElementContainer; // public setter => container swap + re-subscription

        newElementContainer.NormalValue = Color.Green;
        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<Color?> afterNewElement));
        Assert.Equal(Color.Green, afterNewElement.Value);

        oldElementContainer.NormalValue = Color.Orange; // the old container is unsubscribed: this must not reach the store
        Assert.True(element.TryGetResolvedContribution(UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<Color?> afterOldElement));
        Assert.Equal(Color.Green, afterOldElement.Value); // unchanged

        // -- MGTextBlock.Foreground --
        MGTextBlock textBlock = new(harness.Window, "x");
        harness.Show(textBlock);

        textBlock.Foreground.NormalValue = Color.Magenta;
        Assert.True(textBlock.TryGetResolvedContribution(UIPilotProperty.Foreground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<Color?> tbContribution));
        Assert.Equal(Color.Magenta, tbContribution.Value);

        MGTextBlock freshTextBlock = new(harness.Window, "y");
        harness.Show(freshTextBlock);
        freshTextBlock.SetForegroundSlot(UIValueSlot.Normal, Color.Cyan, UIValueResolutionSource.Theme(UIInvalidationKind.Draw));
        Assert.False(freshTextBlock.TryGetResolvedContribution<Color?>(UIPilotProperty.Foreground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out _));

        VisualStateSetting<Color?> oldTbContainer = textBlock.Foreground;
        VisualStateSetting<Color?> newTbContainer = new(Color.Black, Color.Black, Color.Black);
        textBlock.Foreground = newTbContainer;

        newTbContainer.NormalValue = Color.Green;
        Assert.True(textBlock.TryGetResolvedContribution(UIPilotProperty.Foreground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<Color?> afterNewTb));
        Assert.Equal(Color.Green, afterNewTb.Value);

        oldTbContainer.NormalValue = Color.Orange;
        Assert.True(textBlock.TryGetResolvedContribution(UIPilotProperty.Foreground, UIValueSlot.Normal, UIValueSourceKind.LocalValue, out UIResolvedValue<Color?> afterOldTb));
        Assert.Equal(Color.Green, afterOldTb.Value); // unchanged
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
