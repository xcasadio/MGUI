using Microsoft.Xna.Framework.Input;

namespace MGUI.Core.UI.TextEditing;

public sealed class MGRichTextCompletionPopupController
{
    public bool IsOpen { get; private set; }
    public MGRichTextCompletionResult Result { get; private set; } = MGRichTextCompletionResult.Empty;
    public int SelectedIndex { get; private set; } = -1;
    public MGRichTextCompletionItem SelectedItem
        => IsOpen && SelectedIndex >= 0 && SelectedIndex < Result.Items.Count ? Result.Items[SelectedIndex] : null;

    public bool Open(MGRichTextCompletionResult result)
    {
        Result = result ?? MGRichTextCompletionResult.Empty;
        IsOpen = Result.HasItems;
        SelectedIndex = IsOpen ? 0 : -1;
        return IsOpen;
    }

    public void Close()
    {
        IsOpen = false;
        Result = MGRichTextCompletionResult.Empty;
        SelectedIndex = -1;
    }

    public bool TryHandleDismissKey(Keys key)
    {
        if (!IsOpen || key != Keys.Escape)
        {
            return false;
        }

        Close();
        return true;
    }

    public bool MoveSelection(int delta)
    {
        if (!IsOpen || Result.Items.Count == 0 || delta == 0)
        {
            return false;
        }

        var nextIndex = SelectedIndex + delta;
        if (nextIndex < 0)
        {
            nextIndex = 0;
        }
        else if (nextIndex >= Result.Items.Count)
        {
            nextIndex = Result.Items.Count - 1;
        }

        if (nextIndex == SelectedIndex)
        {
            return false;
        }

        SelectedIndex = nextIndex;
        return true;
    }

    public bool TryAcceptSelected(out MGRichTextCompletionAcceptance acceptance)
    {
        if (SelectedItem == null)
        {
            acceptance = default;
            return false;
        }

        acceptance = MGRichTextCompletionService.CreateAcceptance(SelectedItem, Result.ReplacementRange);
        Close();
        return true;
    }
}