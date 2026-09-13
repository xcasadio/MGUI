using System;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.BorderBrushes;
using MGUI.Core.UI.Graph;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Behavioural coverage of the S3 wiring of the template catalogue (ADR-0005, <c>MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs</c>)
/// through the tagged <see cref="MGControlTemplateContext.ApplyTemplateValue{T}(string, T, Func{T}, Action{T, UIValueResolutionSource}, UIInvalidationKind, System.Collections.Generic.IEqualityComparer{T})"/>/
/// <c>ApplyThemeDefault</c> overloads: every scalar-pilot template write now records <see cref="UIValueSourceKind.Template"/>
/// with the correct <see cref="UIInvalidationKind"/> (Measure | Arrange for layout-affecting pilots), and the two S2
/// transitional classifications (<see cref="MGGraphControls"/>'s <c>GraphNode</c>/<c>GraphCommentBox</c> selection visuals)
/// are reverted to <see cref="UIValueSourceKind.VisualState"/> now that they correctly outrank the catalogue's Template(60).
/// </summary>
public class ResolvedTemplatePilotsTests
{
    [Fact]
    public void Freshly_Templated_Window_Reports_Theme_Source_For_Padding_With_Layout_Invalidation()
    {
        Harness harness = Harness.Create();
        MGWindow window = new(harness.Desktop, 0, 0, 400, 300);
        harness.Show(window);

        // ADR-0005/S7a: Window.Padding targets the control itself (Window.SetPadding), so the catalogue now tags
        // it Theme (20) instead of Template (60) -- a style must be able to outrank the window's own chrome default.
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> padding));
        Assert.Equal(UIValueSourceKind.Theme, padding.Source.Kind);
        Assert.True(padding.HasAnyInvalidation(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));
        Assert.Equal(window.GetTheme().Window.Padding, padding.Value);

        // Named mutation (run by hand): remove the "Measure | Arrange" stamp on "Window.Padding" in
        // MGControlTemplateCatalog.ApplyWindowTemplate (fall back to the ApplyOwnerThemeDefault overload's Draw
        // default) -- this assertion goes red because the recorded invalidation no longer carries Measure/Arrange.
        Assert.NotEqual(UIInvalidationKind.Draw, padding.Source.Invalidation);
    }

    [Fact]
    public void Local_Padding_Survives_A_Theme_Refresh_And_The_Theme_Contribution_Is_Retained_Not_Effective()
    {
        Harness harness = Harness.Create();
        MGWindow window = new(harness.Desktop, 0, 0, 400, 300);
        harness.Show(window);

        // ADR-0005/S7a: Window.Padding is now recorded as a Theme (20) contribution, not Template (60) -- see the
        // "Freshly_Templated_Window_Reports_Theme_Source_For_Padding..." test above.
        Assert.True(window.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<Thickness> templateBefore));
        Thickness themePadding = window.GetTheme().Window.Padding;
        Assert.Equal(themePadding, templateBefore.Value);

        Thickness localPadding = new(41);
        window.Padding = localPadding; // public setter => LocalValue(90)
        Assert.Equal(localPadding, window.Padding);

        window.GetResources().DefaultTheme = new MGTheme(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Frame(100, Point.Zero);

        // Precedence and the pre-existing has-previous/equals refresh guard agree: the local value still wins.
        Assert.Equal(localPadding, window.Padding);
        Assert.True(window.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> winnerAfterRefresh));
        Assert.Equal(UIValueSourceKind.LocalValue, winnerAfterRefresh.Source.Kind);
        Assert.Equal(localPadding, winnerAfterRefresh.Value);

        // The Theme contribution itself is retained (the refresh guard bails before overwriting it, since the
        // current CLR value no longer equals the previously-applied template default) without ever becoming effective.
        Assert.True(window.TryGetResolvedContribution(UIPilotProperty.Padding, UIValueSlot.Whole, UIValueSourceKind.Theme, out UIResolvedValue<Thickness> templateAfterRefresh));
        Assert.Equal(templateBefore.Value, templateAfterRefresh.Value);
    }

    [Fact]
    public void GraphNode_Selection_Border_Outranks_Template_And_A_Local_Border_Outranks_Selection()
    {
        Harness harness = Harness.Create();
        MGGraphNode node = new(harness.Window);
        harness.Show(node);

        MGTheme theme = node.GetTheme();

        // ADR-0005/S7a: GraphNode.BorderThickness/BorderBrush are written to OuterBorder, but MGGraphNode does not
        // override GetBorder() and has no public BorderBrush/BorderThickness facade forwarding to it -- OuterBorder
        // is a plain template PART from the pilot-vs-part rule's point of view, not "the control itself", so this
        // key stays Template(60) (unchanged by S7a).
        // ApplyGraphNodeTemplate still writes a Template(60) contribution for the border pilots, but
        // AttachControlTemplateStructure calls ApplySelectionVisual() once unconditionally, so VisualState(70)
        // (the "not selected" variant) is already the effective winner from construction onward.
        Assert.True(node.OuterBorder.TryGetResolvedContribution(UIPilotProperty.BorderThickness, UIValueSlot.Whole, UIValueSourceKind.Template, out UIResolvedValue<Thickness> templateContribution));
        Assert.Equal(theme.Graph.NodeBorderThickness, templateContribution.Value);

        Assert.True(node.OuterBorder.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> unselectedWinner));
        Assert.Equal(UIValueSourceKind.VisualState, unselectedWinner.Source.Kind);
        Assert.Equal(theme.Graph.NodeBorderThickness, unselectedWinner.Value);

        node.IsSelected = true;
        node.ApplySelectionVisual();

        // Selection (VisualState 70) outranks the template default (Template 60).
        Assert.Equal(theme.Graph.NodeSelectedBorderThickness, node.OuterBorder.BorderThickness);
        Assert.True(node.OuterBorder.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> selectedWinner));
        // Named mutation (run by hand): reclassify GraphNode.ApplySelectionVisual's border writes back to
        // LocalValue (the S2 transitional deviation) -- this specific assertion goes red (LocalValue != VisualState)
        // even though the plain "local beats selection" assertion below would still pass either way.
        Assert.Equal(UIValueSourceKind.VisualState, selectedWinner.Source.Kind);

        // An application-driven LocalValue write still beats the selection visual.
        Thickness applicationBorder = new(9);
        node.OuterBorder.BorderThickness = applicationBorder; // public setter => LocalValue(90)
        Assert.Equal(applicationBorder, node.OuterBorder.BorderThickness);
        Assert.True(node.OuterBorder.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> localWinner));
        Assert.Equal(UIValueSourceKind.LocalValue, localWinner.Source.Kind);
        Assert.Equal(applicationBorder, localWinner.Value);
    }

    [Fact]
    public void ListBox_Item_Container_Defaults_Are_Template_Contributions()
    {
        Harness harness = Harness.Create();
        MGBorder item = new(harness.Window);
        harness.Show(item);

        MGControlTemplateCatalog.ApplyListBoxItemContainerDefaults(harness.Window, item);

        Assert.True(item.TryGetResolvedPilotValue(UIPilotProperty.Padding, UIValueSlot.Whole, out UIResolvedValue<Thickness> padding));
        Assert.Equal(UIValueSourceKind.Template, padding.Source.Kind);
        Assert.Equal(MGControlTemplateCatalog.DefaultListBoxItemPadding, padding.Value);
        Assert.True(padding.HasAnyInvalidation(UIInvalidationKind.Measure | UIInvalidationKind.Arrange));

        Assert.True(item.TryGetResolvedPilotValue(UIPilotProperty.BorderThickness, UIValueSlot.Whole, out UIResolvedValue<Thickness> borderThickness));
        Assert.Equal(UIValueSourceKind.Template, borderThickness.Source.Kind);
        Assert.Equal(MGControlTemplateCatalog.DefaultListBoxItemBorderThickness, borderThickness.Value);

        Assert.True(item.TryGetResolvedPilotValue(UIPilotProperty.BorderBrush, UIValueSlot.Whole, out UIResolvedValue<IBorderBrush> borderBrush));
        Assert.Equal(UIValueSourceKind.Template, borderBrush.Source.Kind);
        Assert.True(borderBrush.HasAnyInvalidation(UIInvalidationKind.Draw));
    }

    [Fact]
    public void ContextMenuItem_Header_And_Shortcut_Margins_Follow_A_Custom_Theme_Set_Before_Construction()
    {
        Harness harness = Harness.Create();

        // Custom theme configured BEFORE the menu/item is built, so the item's construction-time placeholder
        // margins (previously LocalValue(90), now DefaultValue) never get a chance to outrank the catalogue's
        // Template(60) write of these same theme-sourced values.
        MGTheme customTheme = new(MGTheme.BuiltInTheme.Dark_Blue, harness.Desktop.DefaultFontFamily);
        Thickness customHeaderMargin = new(0, 0, 17, 0);
        Thickness customShortcutMargin = new(31, 0, 0, 0);
        customTheme.ContextMenuItem.HeaderMargin = customHeaderMargin;
        customTheme.ContextMenuItem.ShortcutMargin = customShortcutMargin;
        harness.Window.GetResources().DefaultTheme = customTheme;

        MGContextMenu menu = new(harness.Window, "");
        MGContextMenuButton item = menu.AddButton("Item A", _ => { });
        harness.Show(menu);

        Assert.True(item.TryGetTemplatePart(MGWrappedContextMenuItem.HeaderPresenterPartName, out MGElement headerPart));
        Assert.Equal(customHeaderMargin, headerPart.Margin);
        Assert.True(headerPart.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> headerMarginResolved));
        Assert.Equal(UIValueSourceKind.Template, headerMarginResolved.Source.Kind);

        Assert.True(item.TryGetTemplatePart(MGWrappedContextMenuItem.ShortcutTextPartName, out MGElement shortcutPart));
        Assert.Equal(customShortcutMargin, shortcutPart.Margin);
        Assert.True(shortcutPart.TryGetResolvedPilotValue(UIPilotProperty.Margin, UIValueSlot.Whole, out UIResolvedValue<Thickness> shortcutMarginResolved));
        Assert.Equal(UIValueSourceKind.Template, shortcutMarginResolved.Source.Kind);

        // Named mutation (run by hand): put MGWrappedContextMenuItem's HeaderPresenter margin write back to
        // UIValueResolutionSource.LocalValue(...) -- this assertion goes red (LocalValue(90) permanently
        // outranks the catalogue's Template(60), so headerPart.Margin stays the construction-time (0,0,5,0)
        // default instead of the theme's (0,0,17,0)). Revert to go green again.
        Assert.NotEqual(new Thickness(0, 0, 5, 0), headerPart.Margin);
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
