using MGUI.Core.UI.TextEditing;

namespace MGUI.Tests.Text;

public class RichTextBoxCompletionModelTests
{
    [Fact]
    public void CreateContext_ComputesPrefixAndReplacementRangeAtCaret()
    {
        MGRichTextCompletionContext context = MGRichTextCompletionService.CreateContext("public cla", 10, MGRichTextCompletionTrigger.Manual, null, version: 4);

        Assert.Equal("cla", context.Prefix);
        Assert.Equal(new MGTextRange(7, 10), context.ReplacementRange);
        Assert.Equal(4, context.Version);
    }

    [Fact]
    public void CSharpKeywordProvider_FiltersByPrefixAndKeepsReplacementRange()
    {
        MGRichTextCompletionContext context = MGRichTextCompletionService.CreateContext("cla", 3, MGRichTextCompletionTrigger.TextChanged, null, version: 2);
        CSharpKeywordCompletionProvider provider = new();

        MGRichTextCompletionResult result = provider.GetCompletions(context);

        Assert.Equal(2, result.Version);
        Assert.Equal(new MGTextRange(0, 3), result.ReplacementRange);
        MGRichTextCompletionItem item = Assert.Single(result.Items);
        Assert.Equal("class", item.Label);
    }

    [Fact]
    public void CreateAcceptance_ReplacesPrefixAndMovesCaretToInsertedEnd()
    {
        MGRichTextCompletionItem item = new("class");

        MGRichTextCompletionAcceptance acceptance = MGRichTextCompletionService.CreateAcceptance(item, new MGTextRange(7, 10));

        Assert.Equal(new MGTextRange(7, 10), acceptance.ReplacementRange);
        Assert.Equal("class", acceptance.InsertText);
        Assert.Equal(12, acceptance.NewCaretIndex);
    }
}