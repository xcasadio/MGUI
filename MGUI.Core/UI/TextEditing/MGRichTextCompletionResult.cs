using System;
using System.Collections.Generic;

namespace MGUI.Core.UI.TextEditing
{
    public sealed class MGRichTextCompletionResult
    {
        public static MGRichTextCompletionResult Empty { get; } = new(0, MGTextRange.EmptyAt(0), Array.Empty<MGRichTextCompletionItem>());

        public int Version { get; }
        public MGTextRange ReplacementRange { get; }
        public IReadOnlyList<MGRichTextCompletionItem> Items { get; }
        public bool HasItems => Items.Count > 0;

        public MGRichTextCompletionResult(int version, MGTextRange replacementRange, IReadOnlyList<MGRichTextCompletionItem> items)
        {
            Version = version;
            ReplacementRange = replacementRange;
            Items = items ?? Array.Empty<MGRichTextCompletionItem>();
        }
    }
}