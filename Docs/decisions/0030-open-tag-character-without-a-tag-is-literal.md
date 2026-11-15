# ADR-0030: An open tag character that opens no well formed tag is a literal character

- **Status**: Accepted
- **Date**: 2026-11-15
- **Source**: decision of the author on 2026-11-15, asked after the allocation-free text chantier (CasaEngine plan `ai-agent/tasks/archive/mgui-text-nonalloc-tasks.md`): should a `[` with no valid tag become an ordinary character, the reading going on after it? Answer: yes, literal

## Context

Inline markup of a text block (`[b]`, `[color=Red]`, `[tt=Name]`, ...) had two behaviours for a `[` that does not open a tag, read in the code on 2026-11-15:

- A `[` followed by no known type turned the whole rest of the text, from that `[`, into one literal: line breaks were not line breaks any more, escapes were not processed, and every tag after it was shown as text.
- A tag of a known type that was not well formed (`[bold`, `[color=Red`, `[b x]`), a `[` that ended the text, or a value its interpretation rejected (`[color=NotAColor]`, `[u=99999999999]`) made `FTParser.ParseTokens` or the interpretation of the value throw out of the setter of `MGTextBlock.Text`. The text block then kept the runs of the text before. A text that comes from data could therefore make a setter throw.

Both the legacy pipeline (`FTTokenizer`, `FTParser`, `MGTextRun.ParseRuns`) and the allocation-free scanner (`FormattedTextScanner`) reproduced this.

## Decision

- One rule: a `[` that does not open a well formed tag is a literal character, and the reading goes on right after it. Well formed means: a known type, the form of its type (`[type]`, `[type=value]`, `[/type]`), a closing `]`, and a value its interpretation accepts.
- A tag ends at the first `]` that follows its `[`, and holds no line feed and no other `[`: a value does not start with a `]`, does not go past a line feed and does not contain a `[`. Both pipelines only read the characters up to the first `]`, `[` or line feed to decide whether a `[` opens a tag. This narrows what the legacy tokenizer accepted: a value that is a name could start with a `]` (`[tt=]a]` was a tooltip named `]a`) or contain a `[` (`[tt=a[b]` was a tooltip named `a[b`). No such markup in MGUI or CasaEngine (searched on 2026-11-15); the syntax of colors and brushes uses no `[`.
- No markup makes `MGTextBlock.Text`, `MGTextBlock.SetText` or `MGTextRun.ParseRuns(string, ...)` throw.
- `FTTokenizer.Tokenize` only returns the tokens of well formed tags, literals and line breaks. It never returns `FTTokenType.InvalidToken`; the enum value stays, as the definition the tokenizer uses to recognize that no tag starts.
- `FTTokenizer.TryTokenize` is removed: it would always return true. Public API break, no `[Obsolete]` alias (the author asked on 2026-11-15 for a modern design over API compatibility).
- The escapes are not changed: `\[` is a literal `[`, pairs of backslashes collapse only before a `[` (the rule `FTTokenizer.EscapeMarkdown` relies on).

## Consequences

- Rendering changes for texts with a `[` that opens no tag: the text after it is read normally (tags, line breaks, escapes). Texts that used to throw now show their malformed tag as text.
- A text block no longer keeps the runs of the text before when a markup is invalid, since no markup is invalid. The difference between the two pipelines after an invalid markup set while `AllowsInlineFormatting` turns true (reported during the chantier) no longer exists.
- A caller that relied on the exception to detect a malformed markup no longer gets one. None in MGUI or CasaEngine.
- A value its interpretation rejects is detected by catching the exception of the interpretation, at each read of the text: such a text allocates at each change. The legacy tokenizer interprets the value of each tag once more than before, to check it.
- The scanner still hands a text to the legacy pipeline when a non ASCII character stands where the grammar compares letters or digits (now also after a `[` that opens no tag), or for an image with no size in a block that has no desktop.
- Reading stays proportional to the text: the legacy tokenizer reads the text in place, and since a tag holds no `[`, what is read to decide on one `[` stops at the next one. Texts of 20 000 characters made of `[`, `[tt=`, `[bg=` or `[u=1 2 ` (with or without one final `]`) are read in a few milliseconds by both pipelines; without that bound they took up to 13 seconds, each `[` making the rest of the text be read and its value be interpreted again. Left as it was (already so before this decision): the legacy tokenizer copies what is left of the text for each token of a well formed tag, and is quadratic on a long run of backslashes before a `[`.
- Tests: `FormattedTextRunsTests.AnOpenTagCharacterThatOpensNoWellFormedTag_IsALiteralCharacter_AndTheReadingGoesOn` (both pipelines), `ALongTextOfOpenTagCharactersThatOpenNoTag_IsNotReadOncePerOpenTagCharacter`, and the differential corpus (`TheRuns_AreTheRunsOfTheLegacyPipeline`: 609 texts, none throws).
