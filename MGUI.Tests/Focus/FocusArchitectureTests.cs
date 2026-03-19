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
}