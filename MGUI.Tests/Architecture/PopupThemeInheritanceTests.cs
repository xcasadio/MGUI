using System;
using System.Collections.Generic;
using System.Linq;
using MGUI.Core.UI;
using MGUI.Core.UI.Brushes.Fill_Brushes;
using MGUI.Core.UI.Styling;
using MGUI.Shared.Rendering;
using MGUI.Tests.Graph;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGame.Extended;
using Xunit;
using MGUIXamlParser = MGUI.Core.UI.XAML.XAMLParser;

namespace MGUI.Tests.Architecture;

/// <summary>
/// Auxiliary surfaces follow their owner's theme: context menus (root, nested, <see cref="MGContextMenu.CreateSimpleMenu(MGWindow, string, Color?, MGSimpleContextMenuItem[])"/>,
/// XAML) and the <see cref="MGComboBox{TItemType}"/> dropdown (code template <c>ComboBox.Default</c> and the XAML <c>PART_DropdownWindow</c> of
/// <c>Dark.ComboBox</c>), tooltips and the <see cref="MGColorPickerPopup"/> window no longer copy their owner window's theme into their own resource scope, so a real theme change of the owner scope
/// reaches them. Most owner windows here carry an explicit theme, as every XAML root window does (<c>Window.ToElement</c> passes the desktop's
/// default theme): that is the configuration that used to pin the popups to the theme captured at creation.
/// Also pins the Dark selection highlights of combo box and context menu rows, the context menu icon column and the combo box dropdown highlight rules.
/// </summary>
public class PopupThemeInheritanceTests
{
    private static readonly Color DarkSelection = new(68, 68, 68);

    [Fact]
    public void Dark_ComboBoxItem_And_ContextMenuItem_Highlights_Use_The_TreeView_Selection_Fill_Without_Overlay()
    {
        Harness harness = Harness.Create();
        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);

        Color treeSelection = SolidColor(dark.TreeViewSelectionBackground.GetValue(true).NormalValue);
        Assert.Equal(DarkSelection, treeSelection);

        foreach (VisualStateFillBrush brush in new[] { dark.ComboBoxDropdownItemBackground.GetValue(true), dark.ContextMenuItem.HeaderBackground })
        {
            Assert.Equal(treeSelection, SolidColor(brush.SelectedValue));
            Assert.Equal(treeSelection, SolidColor(brush.FocusedValue));
            // The hover colours inherited from Dark_Blue (LightBlue * 0.4, White * 0.12) must not tint a highlighted row, pressed or not.
            Assert.Equal(0, OverlayAlpha(brush, SecondaryVisualState.Hovered));
            Assert.Equal(0, OverlayAlpha(brush, SecondaryVisualState.Pressed));
        }
    }

    [Fact]
    public void ContextMenus_Inherit_The_Owner_Scope_Theme_Unless_Given_An_Explicit_Theme()
    {
        Harness harness = Harness.Create();
        MGTheme explicitTheme = new(MGTheme.BuiltInTheme.Dark_Blue, harness.Desktop.DefaultFontFamily);
        MGContextMenu inherited = new(harness.Window, "");
        MGContextMenu nested = new(inherited);
        MGContextMenu simple = MGContextMenu.CreateSimpleMenu(harness.Window, "", null);
        MGContextMenu pinned = new(harness.Window, "", explicitTheme);
        MGContextMenu pinnedNested = new(pinned);

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = dark;
        harness.Frame(1, Point.Zero);

        Assert.Same(dark, inherited.GetTheme());
        Assert.Same(dark, nested.GetTheme());
        Assert.Same(dark, simple.GetTheme());
        Assert.Same(explicitTheme, pinned.GetTheme());
        Assert.Same(explicitTheme, pinnedNested.GetTheme());

        // A submenu inherits its parent menu's scope instead of copying the parent's explicit theme, so it follows a later change of that theme.
        MGTheme otherTheme = new(MGTheme.BuiltInTheme.Dark_Blue, harness.Desktop.DefaultFontFamily);
        pinned.Theme = otherTheme;
        Assert.Same(otherTheme, pinnedNested.GetTheme());
    }

    [Fact]
    public void Elements_Built_Inside_A_Popup_Take_Their_Initial_Background_From_The_Owner_Window_Theme()
    {
        Harness harness = Harness.Create();
        MGTheme desktopTheme = harness.Desktop.Resources.DefaultTheme;
        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        Assert.NotEqual(BackgroundColor(desktopTheme, MGElementType.Separator), BackgroundColor(dark, MGElementType.Separator));
        Assert.NotEqual(BackgroundColor(desktopTheme, MGElementType.ContextMenu), BackgroundColor(dark, MGElementType.ContextMenu));

        // The owner window's explicit theme differs from the desktop's (as after MGWindow.Theme = darkTheme). The menu keeps no theme of its own,
        // so the separator and the submenu built inside it must not fall back to the desktop theme.
        MGWindow owner = new(harness.Desktop, 40, 40, 300, 200, dark);
        MGContextMenu menu = new(owner, "");
        MGContextMenuSeparator separator = menu.AddSeparator();
        MGContextMenu submenu = new(menu);

        Assert.Null(menu.Theme);
        Assert.Equal(BackgroundColor(dark, MGElementType.Separator), SolidColor(separator.SeparatorElement.BackgroundBrush.NormalValue));
        Assert.Equal(BackgroundColor(dark, MGElementType.ContextMenu), SolidColor(submenu.BackgroundBrush.NormalValue));
    }

    [Fact]
    public void Xaml_ContextMenu_Follows_A_Theme_Change_And_Only_Its_Row_Paints_The_Highlight()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""300"" Height=""200"">
    <Button Name=""B"" Content=""Open"">
        <Button.ContextMenu>
            <ContextMenu TitleText=""Actions"">
                <ContextMenuButton Content=""Duplicate"" />
            </ContextMenu>
        </Button.ContextMenu>
    </Button>
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Frame(1, Point.Zero);

        // XAML root windows carry an explicit theme: the configuration that used to pin their menus.
        Assert.NotNull(window.Theme);
        MGContextMenu menu = Assert.IsType<MGContextMenu>(window.GetElementByName<MGButton>("B").ContextMenu);

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        window.GetResources().DefaultTheme = dark;
        harness.Frame(2, Point.Zero);

        Assert.Same(dark, menu.GetTheme());

        // MGContextMenu.OnThemeChanged rebuilt the default row wrapper from the new theme.
        MGContextMenuButton item = menu.Items.OfType<MGContextMenuButton>().Single();
        Assert.Equal(DarkSelection, SolidColor(item.ContentWrapper.BackgroundBrush.SelectedValue));
        Assert.Equal(0, OverlayAlpha(item.ContentWrapper.BackgroundBrush, SecondaryVisualState.Hovered));

        // The icon column (PART_HeaderPresenter) no longer paints the row highlight a second time.
        Assert.True(item.TryGetTemplatePart(MGWrappedContextMenuItem.HeaderPresenterPartName, out MGElement headerPresenter));
        Assert.Null(headerPresenter.BackgroundBrush.SelectedValue);
        Assert.Null(headerPresenter.BackgroundBrush.FocusedValue);
    }

    [Fact]
    public void Submenu_Rows_Are_Rebuilt_With_The_New_Theme()
    {
        Harness harness = Harness.Create();
        MGContextMenu menu = new(harness.Window, "");
        MGContextMenuButton item = menu.AddButton("Parent", _ => { });
        MGContextMenu submenu = new(menu);
        MGContextMenuButton subItem = submenu.AddButton("Child", _ => { });
        item.Submenu = submenu;

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = dark;
        harness.Frame(1, Point.Zero);

        Assert.Same(dark, submenu.GetTheme());
        Assert.Equal(DarkSelection, SolidColor(subItem.ContentWrapper.BackgroundBrush.SelectedValue));
        Assert.Equal(0, OverlayAlpha(subItem.ContentWrapper.BackgroundBrush, SecondaryVisualState.Hovered));
    }

    [Fact]
    public void ComboBox_Dropdown_Follows_A_Theme_Change_And_Retemplates_Items_That_Were_Never_Shown()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = CreateComboBox(harness, forceDefaultTemplate: true);

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = dark;
        harness.Frame(3, Point.Zero);

        Assert.Same(dark, comboBox.Dropdown.GetTheme());

        // The dropdown was never opened: its items were templated under the previous theme outside of any visual tree.
        comboBox.IsDropdownOpen = true;
        harness.Frame(4, Point.Zero);
        MGButton[] rows = comboBox.DropdownStackPanel.Children.OfType<MGButton>().ToArray();
        Assert.Equal(3, rows.Length);
        foreach (MGButton row in rows)
        {
            Assert.Equal(DarkSelection, SolidColor(row.BackgroundBrush.SelectedValue));
            Assert.Equal(0, OverlayAlpha(row.BackgroundBrush, SecondaryVisualState.Hovered));
        }
    }

    [Fact]
    public void ComboBox_Opening_Spoofs_No_Hover_And_Keyboard_Navigation_Moves_The_Selected_Visual()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = CreateComboBox(harness, forceDefaultTemplate: false);

        comboBox.IsDropdownOpen = true;
        harness.Frame(3, Point.Zero);
        MGButton[] rows = comboBox.DropdownStackPanel.Children.OfType<MGButton>().ToArray();
        Assert.Equal(3, rows.Length);
        MGButton low = rows[0];
        MGButton medium = rows[1];
        MGButton high = rows[2];

        // Opening displays the committed selection as selected, without the hover spoof it used to receive (and never lose).
        AssertOnlySelectedRow(rows, medium);
        Assert.Null(comboBox.HoveredItem);

        // Keyboard navigation moves the Selected visual to its target without committing the selection.
        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.MoveDown));
        Assert.Same(high, comboBox.HoveredItem.Element);
        AssertOnlySelectedRow(rows, high);
        Assert.Equal(1, comboBox.SelectedIndex);

        // Cancelling restores the committed selection.
        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.Cancel));
        Assert.False(comboBox.IsDropdownOpen);
        Assert.Equal(1, comboBox.SelectedIndex);
        AssertOnlySelectedRow(rows, medium);

        // Submitting commits the navigation target.
        comboBox.IsDropdownOpen = true;
        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.MoveDown));
        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.Submit));
        Assert.False(comboBox.IsDropdownOpen);
        Assert.Equal(2, comboBox.SelectedIndex);
        AssertOnlySelectedRow(rows, high);

        // A selection committed by code while navigating ends the navigation: only the new selection is displayed as selected...
        comboBox.IsDropdownOpen = true;
        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.MoveUp));
        AssertOnlySelectedRow(rows, medium);
        comboBox.SelectedIndex = 0;
        AssertOnlySelectedRow(rows, low);

        // ...and the old target is forgotten: Enter has nothing to commit and the next MoveDown starts from the new selection.
        Assert.Null(comboBox.HoveredItem);
        Assert.False(comboBox.TryHandleNavigationAction(UINavigationAction.Submit));
        Assert.Equal(0, comboBox.SelectedIndex);
        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.MoveDown));
        AssertOnlySelectedRow(rows, medium);
    }

    [Fact]
    public void ComboBox_Enter_Commits_The_Highlighted_Keyboard_Target_After_The_Mouse_Hovers_Another_Row()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = CreateComboBox(harness, forceDefaultTemplate: false);

        comboBox.IsDropdownOpen = true;
        harness.Frame(3, Point.Zero);
        harness.Frame(4, Point.Zero);
        MGButton[] rows = comboBox.DropdownStackPanel.Children.OfType<MGButton>().ToArray();
        MGButton low = rows[0];
        MGButton high = rows[2];

        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.MoveDown));
        AssertOnlySelectedRow(rows, high);

        Point overLow = low.ConvertCoordinateSpace(CoordinateSpace.Layout, CoordinateSpace.Screen, low.LayoutBounds).Center;
        harness.Frame(5, overLow);
        harness.Frame(6, overLow);
        Assert.Same(low, comboBox.HoveredItem?.Element);
        AssertOnlySelectedRow(rows, high);

        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.Submit));
        Assert.Equal(2, comboBox.SelectedIndex);
    }

    [Fact]
    public void ComboBox_Without_Selection_The_First_MoveDown_Targets_The_First_Row()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = CreateComboBox(harness, forceDefaultTemplate: false);
        comboBox.SelectedIndex = -1;

        comboBox.IsDropdownOpen = true;
        harness.Frame(3, Point.Zero);
        MGButton[] rows = comboBox.DropdownStackPanel.Children.OfType<MGButton>().ToArray();
        AssertOnlySelectedRow(rows, null);

        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.MoveDown));
        AssertOnlySelectedRow(rows, rows[0]);
    }

    [Fact]
    public void ComboBox_Removing_The_Keyboard_Target_While_Open_Displays_The_Committed_Selection_Again()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = CreateComboBox(harness, forceDefaultTemplate: false);

        comboBox.IsDropdownOpen = true;
        harness.Frame(3, Point.Zero);
        MGButton[] rows = comboBox.DropdownStackPanel.Children.OfType<MGButton>().ToArray();
        MGButton low = rows[0];
        MGButton medium = rows[1];

        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.MoveDown));
        Assert.True(comboBox.ItemsSource.Remove("High"));

        AssertOnlySelectedRow(new[] { low, medium }, medium);
        Assert.Null(comboBox.HoveredItem);
    }

    [Fact]
    public void Dark_ComboBox_Xaml_Dropdown_Window_Follows_Later_Theme_Changes()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = CreateComboBox(harness, forceDefaultTemplate: false);

        // Dark maps ComboBox to the XAML template Dark.ComboBox: the first change rebuilds the structure, including its XAML
        // PART_DropdownWindow, which used to be pinned to the owner window's explicit theme instead of following its scope.
        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = dark;
        harness.Frame(3, Point.Zero);
        MGWindow xamlDropdown = comboBox.Dropdown;
        Assert.Same(dark, xamlDropdown.GetTheme());

        // Another Dark instance resolves the same template, so the structure is kept and only scope inheritance delivers the theme.
        MGTheme otherDark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = otherDark;
        harness.Frame(4, Point.Zero);
        Assert.Same(xamlDropdown, comboBox.Dropdown);
        Assert.Same(otherDark, comboBox.Dropdown.GetTheme());
    }

    [Fact]
    public void ToolTips_Inherit_The_Owner_Scope_Theme_Unless_Given_An_Explicit_Theme()
    {
        Harness harness = Harness.Create();
        MGButton host = new(harness.Window);
        MGToolTip inherited = new(harness.Window, host, 120, 40);
        MGTheme explicitTheme = new(MGTheme.BuiltInTheme.Dark_Blue, harness.Desktop.DefaultFontFamily);
        MGToolTip pinned = new(harness.Window, host, 120, 40, explicitTheme);

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        Color? darkToolTipText = dark.ToolTipTextForeground.GetCopy().NormalValue;
        Assert.NotEqual(harness.Desktop.Resources.DefaultTheme.ToolTipTextForeground.GetCopy().NormalValue, darkToolTipText);

        harness.Window.GetResources().DefaultTheme = dark;
        harness.Frame(1, Point.Zero);

        Assert.Same(dark, inherited.GetTheme());
        // The ToolTip template re-applies its theme defaults on the refresh that now reaches the tooltip.
        Assert.Equal(darkToolTipText, inherited.DefaultTextForeground.NormalValue);
        Assert.Same(explicitTheme, pinned.GetTheme());
    }

    [Fact]
    public void Xaml_ToolTip_Follows_A_Theme_Change()
    {
        Harness harness = Harness.Create();
        string xaml = @"<Window xmlns=""clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"" Width=""300"" Height=""200"">
    <Button Name=""B"" Content=""Hover me"" ToolTip=""Tooltip text"" />
</Window>";
        MGWindow window = MGUIXamlParser.LoadRootWindow(harness.Desktop, xaml, false, true);
        harness.Desktop.Windows.Add(window);
        harness.Frame(1, Point.Zero);
        MGToolTip toolTip = Assert.IsType<MGToolTip>(window.GetElementByName<MGButton>("B").ToolTip);

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        window.GetResources().DefaultTheme = dark;
        harness.Frame(2, Point.Zero);

        Assert.Null(toolTip.Theme);
        Assert.Same(dark, toolTip.GetTheme());
    }

    [Fact]
    public void ColorPickerPopup_Window_Follows_The_Owner_Scope_Theme()
    {
        Harness harness = Harness.Create();
        MGColorPickerPopup popup = new(harness.Window);

        MGTheme dark = new(MGTheme.BuiltInTheme.Dark, harness.Desktop.DefaultFontFamily);
        harness.Window.GetResources().DefaultTheme = dark;
        harness.Frame(1, Point.Zero);

        Assert.Null(popup.PopupWindow.Theme);
        Assert.Same(dark, popup.PopupWindow.GetTheme());
        Assert.Same(dark, popup.Picker.GetTheme());
    }

    [Fact]
    public void Closed_ComboBox_Without_Selection_MoveDown_Selects_The_First_Row()
    {
        Harness harness = Harness.Create();
        MGComboBox<string> comboBox = CreateComboBox(harness, forceDefaultTemplate: false);
        comboBox.SelectedIndex = -1;

        Assert.True(comboBox.TryHandleNavigationAction(UINavigationAction.MoveDown));
        Assert.False(comboBox.IsDropdownOpen);
        Assert.Equal(0, comboBox.SelectedIndex);

        // With the first row selected, MoveUp has nothing to change.
        Assert.False(comboBox.TryHandleNavigationAction(UINavigationAction.MoveUp));
        Assert.Equal(0, comboBox.SelectedIndex);
    }

    private static MGComboBox<string> CreateComboBox(Harness harness, bool forceDefaultTemplate)
    {
        MGComboBox<string> comboBox = new(harness.Window) { PreferredHeight = 30 };
        if (forceDefaultTemplate)
        {
            // Same as ControlTemplate="ComboBox.Default" in MGUI.Samples/Features/StyleThemeRefactor.xaml: the Dark theme's
            // ComboBox -> Dark.ComboBox mapping is bypassed, so a theme change never rebuilds the template structure.
            comboBox.ControlTemplateName = MGControlTemplateCatalog.ComboBoxTemplateName;
        }

        comboBox.SetItemsSource(new List<string> { "Low", "Medium", "High" });
        comboBox.SelectedIndex = 1;
        harness.Show(comboBox);
        return comboBox;
    }

    private static void AssertOnlySelectedRow(IEnumerable<MGButton> rows, MGButton expectedSelected)
    {
        foreach (MGButton row in rows)
        {
            Assert.Equal(ReferenceEquals(row, expectedSelected), row.IsSelected);
            Assert.False(row.SpoofIsHoveredWhileDrawingBackground);
        }
    }

    private static Color SolidColor(IFillBrush brush) => Assert.IsType<MGSolidFillBrush>(brush).Color;

    private static Color BackgroundColor(MGTheme theme, MGElementType elementType) => SolidColor(theme.GetBackgroundBrush(elementType).NormalValue);

    private static int OverlayAlpha(VisualStateFillBrush brush, SecondaryVisualState state) => brush.GetFillOverlay(state)?.Color.A ?? 0;

    private readonly record struct Harness(GraphTestRuntime Runtime, MGDesktop Desktop, MGWindow Window)
    {
        public static Harness Create()
        {
            GraphTestRuntime runtime = new(new Rectangle(0, 0, 960, 540));
            MGDesktop desktop = new(runtime);
            // Explicit theme, like every XAML root window.
            MGWindow window = new(desktop, 24, 24, 480, 260, desktop.Resources.DefaultTheme)
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
            Frame(1, Point.Zero);
            Frame(2, Point.Zero);
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
