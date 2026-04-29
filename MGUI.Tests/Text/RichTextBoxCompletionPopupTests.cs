using MGUI.Core.UI.TextEditing;
using Microsoft.Xna.Framework.Input;

namespace MGUI.Tests.Text;

public class RichTextBoxCompletionPopupTests
{
    [Fact]
    public void Open_SelectsFirstItemWhenResultHasItems()
    {
        MGRichTextCompletionPopupController popup = new();
        MGRichTextCompletionResult result = CreateResult("class", "public");

        bool opened = popup.Open(result);

        Assert.True(opened);
        Assert.True(popup.IsOpen);
        Assert.Equal(0, popup.SelectedIndex);
        Assert.Equal("class", popup.SelectedItem.Label);
    }

    [Fact]
    public void MoveSelection_ClampsWithinAvailableItems()
    {
        MGRichTextCompletionPopupController popup = new();
        popup.Open(CreateResult("class", "public", "private"));

        Assert.True(popup.MoveSelection(1));
        Assert.Equal("public", popup.SelectedItem.Label);
        Assert.True(popup.MoveSelection(99));
        Assert.Equal("private", popup.SelectedItem.Label);
        Assert.False(popup.MoveSelection(1));
        Assert.True(popup.MoveSelection(-99));
        Assert.Equal("class", popup.SelectedItem.Label);
    }

    [Fact]
    public void TryAcceptSelected_ReturnsAcceptanceAndClosesPopup()
    {
        MGRichTextCompletionPopupController popup = new();
        popup.Open(CreateResult("class"));

        bool accepted = popup.TryAcceptSelected(out MGRichTextCompletionAcceptance acceptance);

        Assert.True(accepted);
        Assert.False(popup.IsOpen);
        Assert.Equal("class", acceptance.InsertText);
        Assert.Equal(new MGTextRange(0, 3), acceptance.ReplacementRange);
        Assert.Equal(5, acceptance.NewCaretIndex);
    }

    [Fact]
    public void TryHandleDismissKey_ClosesOpenPopupForEscape()
    {
        MGRichTextCompletionPopupController popup = new();
        popup.Open(CreateResult("class"));

        bool handled = popup.TryHandleDismissKey(Keys.Escape);

        Assert.True(handled);
        Assert.False(popup.IsOpen);
        Assert.Equal(-1, popup.SelectedIndex);
    }

    private static MGRichTextCompletionResult CreateResult(params string[] labels)
    {
        MGRichTextCompletionItem[] items = new MGRichTextCompletionItem[labels.Length];
        for (int index = 0; index < labels.Length; index++)
        {
            items[index] = new MGRichTextCompletionItem(labels[index]);
        }

        return new MGRichTextCompletionResult(1, new MGTextRange(0, 3), items);
    }
}