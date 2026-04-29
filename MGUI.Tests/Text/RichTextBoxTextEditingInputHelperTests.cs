using MGUI.Core.UI;
using MGUI.Core.UI.TextEditing;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Text;

public class RichTextBoxTextEditingInputHelperTests
{
    [Fact]
    public void ShouldPreserveTextEntryKey_RespectsReadonlyReturnAndTabSettings()
    {
        Assert.True(MGTextEditingInputHelpers.ShouldPreserveTextEntryKey(Keys.Left, isReadonly: true, acceptsReturn: false, acceptsTab: false));
        Assert.False(MGTextEditingInputHelpers.ShouldPreserveTextEntryKey(Keys.Space, isReadonly: true, acceptsReturn: false, acceptsTab: false));
        Assert.True(MGTextEditingInputHelpers.ShouldPreserveTextEntryKey(Keys.Space, isReadonly: false, acceptsReturn: false, acceptsTab: false));
        Assert.False(MGTextEditingInputHelpers.ShouldPreserveTextEntryKey(Keys.Enter, isReadonly: false, acceptsReturn: false, acceptsTab: false));
        Assert.True(MGTextEditingInputHelpers.ShouldPreserveTextEntryKey(Keys.Enter, isReadonly: false, acceptsReturn: true, acceptsTab: false));
        Assert.True(MGTextEditingInputHelpers.ShouldPreserveTextEntryKey(Keys.Tab, isReadonly: false, acceptsReturn: false, acceptsTab: true));
    }

    [Fact]
    public void ShouldHandleRepeatedKey_RequiresFocusAndIgnoresControlShortcutStreams()
    {
        Assert.False(MGTextEditingInputHelpers.ShouldHandleRepeatedKey(hasKeyboardFocus: false, isHeldKeyRepeated: true, isControlDown: false, isPrintableKey: true, key: Keys.A));
        Assert.False(MGTextEditingInputHelpers.ShouldHandleRepeatedKey(hasKeyboardFocus: true, isHeldKeyRepeated: true, isControlDown: true, isPrintableKey: true, key: Keys.A));
        Assert.False(MGTextEditingInputHelpers.ShouldHandleRepeatedKey(hasKeyboardFocus: true, isHeldKeyRepeated: true, isControlDown: false, isPrintableKey: true, key: Keys.A, streamStartedAsControlShortcut: true));
        Assert.True(MGTextEditingInputHelpers.ShouldHandleRepeatedKey(hasKeyboardFocus: true, isHeldKeyRepeated: true, isControlDown: false, isPrintableKey: true, key: Keys.A));
        Assert.True(MGTextEditingInputHelpers.ShouldHandleRepeatedKey(hasKeyboardFocus: true, isHeldKeyRepeated: true, isControlDown: false, isPrintableKey: false, key: Keys.Back));
    }

    [Fact]
    public void ControlShortcutAndCaretHelpers_MatchTextBoxCompatibilityWrappers()
    {
        Assert.True(MGTextEditingInputHelpers.IsControlShortcutKey(Keys.C));
        Assert.False(MGTextEditingInputHelpers.IsControlShortcutKey(Keys.B));
        Assert.Equal(0, MGTextEditingInputHelpers.NormalizeEditableCaretIndex(-10, 5));
        Assert.Equal(5, MGTextEditingInputHelpers.NormalizeEditableCaretIndex(99, 5));

        Assert.Equal(MGTextEditingInputHelpers.IsControlShortcutKey(Keys.V), MGTextBox.IsControlShortcutKey(Keys.V));
        Assert.Equal(MGTextEditingInputHelpers.NormalizeEditableCaretIndex(99, 5), MGTextBox.NormalizeEditableCaretIndex(99, 5));
    }

    [Fact]
    public void TextUndoStack_TrimsOldestEntriesWhenLimitIsReached()
    {
        MGTextUndoStack<string> stack = new(2);

        stack.Push("first");
        stack.Push("second");
        stack.Push("third");

        Assert.Equal(2, stack.Count);
        Assert.True(stack.TryPop(out string newest));
        Assert.Equal("third", newest);
        Assert.True(stack.TryPop(out string oldestRemaining));
        Assert.Equal("second", oldestRemaining);
        Assert.False(stack.TryPop(out _));
    }

    [Fact]
    public void TextUndoStack_SetLimit_TrimsExistingEntriesWithoutReordering()
    {
        MGTextUndoStack<int> stack = new(4);
        stack.Push(1);
        stack.Push(2);
        stack.Push(3);
        stack.Push(4);

        stack.SetLimit(2);

        Assert.Equal(2, stack.Count);
        Assert.True(stack.TryPop(out int newest));
        Assert.Equal(4, newest);
        Assert.True(stack.TryPop(out int oldestRemaining));
        Assert.Equal(3, oldestRemaining);
    }
}