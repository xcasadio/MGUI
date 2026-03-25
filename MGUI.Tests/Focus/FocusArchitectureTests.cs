using System.IO;
using System.Reflection;
using MGUI.Core.UI;
using MGUI.Core.UI.InputRouting;
using MGUI.Shared.Input.Semantic;

namespace MGUI.Tests.Focus;

public class FocusArchitectureTests
{
    [Fact]
    public void Desktop_Exposes_Semantic_Input_Entry_Points()
    {
        Assert.NotNull(typeof(MGDesktop).GetMethod(nameof(MGDesktop.TryHandleInputAction)));
        Assert.NotNull(typeof(MGDesktop).GetMethod(nameof(MGDesktop.ShouldCaptureGameplayInput)));
        Assert.NotNull(typeof(MGDesktop).GetProperty(nameof(MGDesktop.UseRawNavigationInput)));
    }

    [Fact]
    public void MGUIInputContext_Implements_Shared_Input_Context_Contract()
    {
        Assert.Contains(typeof(IInputContext), typeof(MGUIInputContext).GetInterfaces());
    }

    [Fact]
    public void Overlay_Source_Clears_Current_And_Queued_Focus_When_Blocking_Content()
    {
        string overlaySource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGOverlay.cs");

        Assert.Contains("GetDesktop().ClearQueuedFocusedKeyboardHandler();", overlaySource);
        Assert.Contains("GetDesktop().ClearFocusedKeyboardHandler();", overlaySource);
    }

    [Fact]
    public void MenuBar_Source_Does_Not_Register_Duplicate_Raw_Keyboard_Navigation_Handler()
    {
        string menuBarSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGMenuBar.cs");

        Assert.DoesNotContain("KeyboardHandler.Pressed +=", menuBarSource);
    }

    [Fact]
    public void ListView_And_TreeView_Sources_Do_Not_Register_Duplicate_Raw_Keyboard_Navigation_Handlers()
    {
        string listViewSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGListView.cs");
        string treeViewSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGTreeView.cs");

        Assert.DoesNotContain("KeyboardHandler.Pressed += OnListViewKeyPressed;", listViewSource);
        Assert.DoesNotContain("KeyboardHandler.Pressed += OnKeyPressed;", treeViewSource);
    }

    [Fact]
    public void AddNestedWindow_Source_Remains_Focus_Neutral_For_NonModal_Children()
    {
        string windowSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGWindow.cs");

        int addNestedWindowIndex = windowSource.IndexOf("public void AddNestedWindow(MGWindow NestedWindow)");
        int removeNestedWindowIndex = windowSource.IndexOf("public bool RemoveNestedWindow(MGWindow NestedWindow)");

        Assert.True(addNestedWindowIndex >= 0);
        Assert.True(removeNestedWindowIndex > addNestedWindowIndex);

        string addNestedWindowSource = windowSource.Substring(addNestedWindowIndex, removeNestedWindowIndex - addNestedWindowIndex);

        Assert.Contains("_NestedWindows.Add(NestedWindow);", addNestedWindowSource);
        Assert.Contains("Desktop.NotifyWindowOpened(NestedWindow);", addNestedWindowSource);
        Assert.DoesNotContain("QueueFocusedKeyboardHandler", addNestedWindowSource);
        Assert.DoesNotContain("ClearFocusedKeyboardHandler", addNestedWindowSource);
        Assert.DoesNotContain("FocusedKeyboardHandler =", addNestedWindowSource);
    }

    [Fact]
    public void Desktop_Blocking_Policy_Only_Treats_Overlays_And_Modal_Windows_As_Keyboard_Blockers()
    {
        string desktopSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGDesktop.cs");

        Assert.Contains("if (OverlayHost?.IsModal == true && OverlayHost.ActiveOverlay != null && OverlayHost.ActiveOverlayPresenter != null", desktopSource);
        Assert.Contains("return element.SelfOrParentWindow?.HasModalWindow == true;", desktopSource);
    }

    [Fact]
    public void Window_Update_Source_Prioritizes_Modal_Window_Before_NonModal_Nested_Windows()
    {
        string windowSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGWindow.cs");

        int modalUpdateIndex = windowSource.IndexOf("ModalWindow?.Update(UpdateArgs);");
        int nestedUpdateIndex = windowSource.IndexOf("foreach (MGWindow Nested in _NestedWindows.Reverse<MGWindow>().OrderByDescending(x => x.IsTopmost))");

        Assert.True(modalUpdateIndex >= 0);
        Assert.True(nestedUpdateIndex > modalUpdateIndex);
        Assert.Contains("This ensures the ModalWindow effectively blocks all input to NestedWindows.", windowSource);
    }

    [Fact]
    public void Desktop_Blocking_Policy_Allows_Elements_Inside_The_Active_Overlay()
    {
        string desktopSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGDesktop.cs");

        Assert.Contains("&& !OverlayHost.ActiveOverlayPresenter.IsSelfOrAncestorOf(element)", desktopSource);
        Assert.Contains("&& !OverlayHost.ActiveOverlay.IsSelfOrAncestorOf(element)", desktopSource);
    }

    [Fact]
    public void Button_Source_Only_Raises_Click_When_The_Press_Started_On_That_Button()
    {
        string buttonSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGButton.cs");

        Assert.Contains("bool pressedInsideThisButton = PressedArgs != null;", buttonSource);
        Assert.Contains("if (e.IsLMB && pressedInsideThisButton)", buttonSource);
        Assert.Contains("MouseHandler.ReleasedOutside += (sender, e) =>", buttonSource);
    }

    [Fact]
    public void Overlay_Source_Preserves_Queued_Focus_For_Targets_Inside_The_Active_Overlay()
    {
        string overlaySource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGOverlay.cs");

        Assert.Contains("MGElement Queued = GetDesktop().QueuedFocusedKeyboardHandler;", overlaySource);
        Assert.Contains("if (Queued != null && !IsInsideActiveOverlay(Queued))", overlaySource);
        Assert.Contains("GetDesktop().ClearQueuedFocusedKeyboardHandler();", overlaySource);
    }

    [Fact]
    public void Overlay_Source_Only_Swallows_Pointer_Input_Outside_The_Active_Overlay()
    {
        string overlaySource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGOverlay.cs");

        Assert.Contains("private bool IsPointerOutsideActiveOverlay(Point pointerPosition)", overlaySource);
        Assert.Contains("if (IsModal && ActiveOverlay != null && IsPointerOutsideActiveOverlay(pointerPosition))", overlaySource);
    }

    [Fact]
    public void ContentPresenter_Source_Still_Parents_And_Invalidates_Cached_Children_When_Suppressing_Content_Events()
    {
        string contentHostSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\Containers\MGContentHost.cs");

        Assert.Contains("_Content.SetParent(this);", contentHostSource);
        Assert.Contains("InvalidateVtcCache();", contentHostSource);
        Assert.Contains("if (!SuppressContentAddedAndRemoved)", contentHostSource);
    }

    [Fact]
    public void ToggleButton_Source_Only_Toggles_When_The_Press_Started_On_That_Toggle()
    {
        string toggleButtonSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGToggleButton.cs");

        Assert.Contains("MouseHandler.LMBPressedInside += (sender, e) =>", toggleButtonSource);
        Assert.Contains("if (PressedArgs != null)", toggleButtonSource);
        Assert.Contains("MouseHandler.ReleasedOutside += (sender, e) =>", toggleButtonSource);
    }

    [Fact]
    public void Element_HitTesting_Allows_Hidden_Elements_That_Explicitly_Handle_Input()
    {
        string elementSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGElement.cs");

        Assert.Contains("bool canReceiveMouseInputWhileHidden = Visibility == Visibility.Hidden && CanHandleInputsWhileHidden;", elementSource);
        Assert.Contains("if (Visibility != Visibility.Visible && !canReceiveMouseInputWhileHidden)", elementSource);
        Assert.Contains("if (RecentDrawWasClipped && !canReceiveMouseInputWhileHidden)", elementSource);
    }

    [Fact]
    public void RadioButton_Source_Handles_Clicks_Directly_On_The_Control()
    {
        string radioButtonSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGRadioButton.cs");

        Assert.Contains("MouseHandler.LMBPressedInside += (sender, e) =>", radioButtonSource);
        Assert.Contains("MouseHandler.LMBReleasedInside += (sender, e) =>", radioButtonSource);
        Assert.Contains("IsChecked = !IsChecked;", radioButtonSource);
        Assert.Contains("ButtonElement.IsHitTestVisible = false;", radioButtonSource);
    }

    [Fact]
    public void MenuBar_Source_Queues_Its_Own_Focus_Before_Opening_The_Submenu()
    {
        string menuBarSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGMenuBar.cs");

        int queueFocusIndex = menuBarSource.IndexOf("GetDesktop().QueueFocusedKeyboardHandler(this, KeyboardFocusSource.Programmatic);", StringComparison.Ordinal);
        int openSubmenuIndex = menuBarSource.IndexOf("Item.OpenSubmenu();", StringComparison.Ordinal);

        Assert.True(queueFocusIndex >= 0);
        Assert.True(openSubmenuIndex > queueFocusIndex);
    }

    [Fact]
    public void ContextMenuItem_Source_Treats_Focused_State_As_Highlighted()
    {
        string contextMenuItemSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGContextMenuItem.cs");

        Assert.Contains("|| ownerState.IsSelected || ownerState.IsFocused || wrapperState.IsFocused || Submenu?.IsContextMenuOpen == true;", contextMenuItemSource);
    }

    [Fact]
    public void ContextMenu_Source_Delegates_Top_Level_MenuBar_Navigation_Back_To_The_MenuBar()
    {
        string contextMenuSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGContextMenu.cs");

        Assert.Contains("internal MGMenuBarItem OpenedFromMenuBarItem { get; set; }", contextMenuSource);
        Assert.Contains("if (OpenedFromMenuBar != null && action is UINavigationAction.MoveLeft or UINavigationAction.MoveRight or UINavigationAction.Home or UINavigationAction.End)", contextMenuSource);
        Assert.Contains("return OpenedFromMenuBar.TryHandleNavigationAction(action);", contextMenuSource);
    }

    [Fact]
    public void MenuBarItem_Source_Assigns_Its_Submenu_Back_Reference_For_Top_Level_Navigation()
    {
        string menuBarSource = File.ReadAllText(@"d:\development\repo\MGUI\MGUI.Core\UI\MGMenuBar.cs");

        Assert.Contains("_Submenu.OpenedFromMenuBarItem = this;", menuBarSource);
    }
}