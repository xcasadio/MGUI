# RichTextBox editor v1

## Scope

`MGRichTextBox` is a lightweight editor-oriented control. It reuses the stable `MGTextBox` interaction surface, keeps source text as plain text, and renders optional style spans through the existing `MGTextBlock` rich-run pipeline.

This v1 is intended for game/editor tooling scenarios such as script snippets, AI-agent prompts, logs with selectable text, and small code editors.

It is not a WPF `FlowDocument` clone. It does not load RTF or HTML, does not expose paragraph/block document objects, and does not include an LSP client. Syntax highlighting and completions are deliberately provider-based so host applications can plug in richer services later.

## Minimal XAML

```xaml
<RichTextBox Name="EditorTextBox"
             MinLines="16"
             WrapText="False"
             ShowLineNumbers="True"
             TabSize="4" />
```

The XAML wrapper maps to `MGRichTextBox` and accepts the same text settings as `TextBox` plus `ShowLineNumbers` and `TabSize`.

## Minimal C# setup

```csharp
using MGUI.Core.UI;
using MGUI.Core.UI.TextEditing;

MGRichTextBox editor = Window.GetElementByName<MGRichTextBox>("EditorTextBox");
editor.SyntaxHighlighter = new CSharpRichTextSyntaxHighlighter();
editor.CompletionProvider = new CSharpKeywordCompletionProvider();
editor.SetText("using System;\n\npublic cla");
editor.RefreshSyntaxHighlighting();

MGRichTextCompletionResult result = editor.RequestCompletions();
editor.OpenCompletionPopup(result);
editor.AcceptSelectedCompletion();
```

For a full sample, open `SCN-EDITOR-RTB-001` from the samples compendium. The implementation lives in `MGUI.Samples/Features/EditorRichTextBox.xaml` and `MGUI.Samples/Features/EditorRichTextBox.xaml.cs`.

## Syntax highlighting

Implement `IRichTextSyntaxHighlighter` to translate plain text into styled spans.

```csharp
public sealed class TodoHighlighter : IRichTextSyntaxHighlighter
{
    public MGRichTextHighlightResult Highlight(MGRichTextHighlightContext context)
    {
        List<MGStyledTextSpan> spans = new();
        int index = context.Text.IndexOf("TODO", StringComparison.Ordinal);
        if (index >= 0)
        {
            MGTextRange range = MGTextRange.FromStartAndLength(index, 4);
            MGRichTextStyle style = new(Foreground: context.Palette.Keyword, IsBold: true);
            spans.Add(new MGStyledTextSpan(range, style, "todo"));
        }

        return new MGRichTextHighlightResult(context.Version, spans);
    }
}
```

`CSharpRichTextSyntaxHighlighter` is a small lexical demo highlighter. It highlights comments, string literals, numbers, keywords, and simple type-like identifiers. It is intentionally not a compiler service.

## Completion provider

Implement `IRichTextCompletionProvider` to return completion items for the current caret and prefix.

```csharp
public sealed class LocalCompletionProvider : IRichTextCompletionProvider
{
    public MGRichTextCompletionResult GetCompletions(MGRichTextCompletionContext context)
    {
        List<MGRichTextCompletionItem> items = new();
        if ("component".StartsWith(context.Prefix, StringComparison.OrdinalIgnoreCase))
        {
            items.Add(new MGRichTextCompletionItem("component", kind: "keyword"));
        }

        return new MGRichTextCompletionResult(context.Version, context.ReplacementRange, items);
    }
}
```

`MGRichTextBox.RequestCompletions()` creates the context from current text and caret, calls the provider, and returns an immutable result. `OpenCompletionPopup(...)`, `MoveCompletionSelection(...)`, and `AcceptSelectedCompletion()` expose a testable popup state model that samples or host editors can render however they prefer.

## Public pieces

- `MGRichTextBox`: editor control derived from `MGTextBox` for v1 input compatibility.
- `MGTextBuffer`, `MGTextRange`, `MGTextPosition`, `MGTextSelectionState`: pure text editing primitives.
- `MGRichTextStyle` and `MGStyledTextSpan`: non-destructive style spans over plain text.
- `IRichTextSyntaxHighlighter`: provider contract for syntax spans.
- `IRichTextCompletionProvider`: provider contract for completion items.
- `MGRichTextCompletionPopupController`: selection and acceptance state for completion UI.

## V1 limits

- No WPF `FlowDocument`, `Run`, `Paragraph`, or document object model.
- No RTF, HTML, Markdown, or clipboard format conversion.
- No integrated LSP, semantic classification, diagnostics, or async completion cancellation.
- No virtualization for very large documents yet.
- The default C# highlighter and keyword provider are demos, not language-server replacements.