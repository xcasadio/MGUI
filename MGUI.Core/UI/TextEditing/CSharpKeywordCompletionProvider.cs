using System.Collections.Generic;

namespace MGUI.Core.UI.TextEditing
{
    public sealed class CSharpKeywordCompletionProvider : IRichTextCompletionProvider
    {
        private static readonly MGRichTextCompletionItem[] KeywordItems =
        {
            new("class", detail: "C# keyword", kind: "keyword"),
            new("public", detail: "C# keyword", kind: "keyword"),
            new("private", detail: "C# keyword", kind: "keyword"),
            new("protected", detail: "C# keyword", kind: "keyword"),
            new("internal", detail: "C# keyword", kind: "keyword"),
            new("static", detail: "C# keyword", kind: "keyword"),
            new("void", detail: "C# keyword", kind: "keyword"),
            new("string", detail: "C# keyword", kind: "keyword"),
            new("int", detail: "C# keyword", kind: "keyword"),
            new("return", detail: "C# keyword", kind: "keyword"),
            new("namespace", detail: "C# keyword", kind: "keyword"),
            new("using", detail: "C# keyword", kind: "keyword"),
            new("var", detail: "C# keyword", kind: "keyword"),
            new("new", detail: "C# keyword", kind: "keyword"),
        };

        public MGRichTextCompletionResult GetCompletions(MGRichTextCompletionContext context)
        {
            IReadOnlyList<MGRichTextCompletionItem> filteredItems = MGRichTextCompletionService.FilterByPrefix(KeywordItems, context.Prefix);
            return new MGRichTextCompletionResult(context.Version, context.ReplacementRange, filteredItems);
        }
    }
}