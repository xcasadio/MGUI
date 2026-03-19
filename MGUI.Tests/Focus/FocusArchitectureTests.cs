using System.IO;

namespace MGUI.Tests.Focus;

public class FocusArchitectureTests
{
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
}