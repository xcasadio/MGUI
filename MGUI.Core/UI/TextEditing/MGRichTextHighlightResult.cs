using System.Collections.Generic;

namespace MGUI.Core.UI.TextEditing
{
    public sealed class MGRichTextHighlightResult
    {
        public int Version { get; }
        public IReadOnlyList<MGStyledTextSpan> Spans { get; }

        public MGRichTextHighlightResult(int version, IReadOnlyList<MGStyledTextSpan> spans)
        {
            Version = version;
            Spans = spans ?? System.Array.Empty<MGStyledTextSpan>();
        }
    }
}