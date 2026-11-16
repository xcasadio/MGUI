# ADR-0033: The grammar of markup tags is ASCII

- **Status**: Accepted
- **Date**: 2026-11-16
- **Source**: CasaEngine plan `ai-agent/tasks/mgui-nonalloc-suite-tasks.md`, decision D9, approved by the author on 2026-11-16

## Context

The inline markup of a text block (`[b]`, `[o=0.5]`, `[color=RGB(1,2,3)]`, ...) is read by two pipelines that must give the same runs: the legacy one (`FTTokenizer`, `FTParser`, `MGTextRun.ParseRuns`) and the allocation-free `FormattedTextScanner`. Read in the code and measured on 2026-11-16:

- The legacy patterns read the digits of a number with `\d`, which matches every decimal digit of Unicode (360 characters above U+007F, such as the Arabic-Indic digits U+0660..U+0669 and the full width digits U+FF10..U+FF19). They were: the opacity, the height and the offset of an underline, the offset of a shadow (in the tokenizer and in `MGTextRun.ShadowOffsetParser`), the padding of a background, the size of an image, the components of `RGB(...)` and the factor of a color (`Red * 0.5`).
- The legacy patterns read the type of a tag with the inline option `(?i)`. The tokenizer builds its regular expressions with `RegexOptions.CultureInvariant`, and with it the only letter above U+007F that matches an ASCII letter of a type is U+212A KELVIN SIGN, which matched `k` (so `[bacKground=Red]` was a background tag; `background` is the only type with a `k`). Without that option the culture of the process matters too (U+0130, the dotted capital I, matches `i` under a Turkish culture): the option was added for that reason.
- A number written with digits that are not ASCII passed the pattern and then failed to be interpreted (`int.Parse` and `float.Parse` do not read them), which made the tag a literal text (ADR-0030). The result was the right one by accident, and only through an exception.
- The scanner compares ASCII only. Since it could not tell if the legacy code would read a Unicode digit or a folded letter the same way, it left the text to the legacy pipeline ("ambiguous") when it met a non ASCII digit where a number is read, or a non ASCII character among the ten characters that follow a `[` that has no type. Such a text allocated about 20 KB at each change (measured: setter 10847 B, Update 10104 B, Draw 120 B per frame for `Value [<e acute>] text` changed at every frame).
- The names of tooltips, actions, images and backgrounds, the name of a color (`[a-zA-Z]+`) and the text itself are not read by these patterns: a name is any text up to the first `]`.

## Decision

- The grammar of tags is ASCII, in the legacy pipeline and in the scanner. A tag type is made of ASCII letters, compared without case (each pattern spells both cases of each letter: `[Bb][Oo][Ll][Dd]`; no `(?i)`, so no letter that folds to an ASCII one matches). A number is made of ASCII digits (`[0-9]`, never `\d`).
- A `[` that opens no well formed tag is a literal character and the reading goes on right after it (ADR-0030, unchanged): so `[o=<Arabic-Indic zero>.<five>]`, `[<full width b>]` or `[bacKground=Red]` written with U+212A are text.
- The scanner no longer has an ambiguous outcome. The only text it still leaves to the legacy pipeline is an image tag with no size in a block that has no desktop (the size of its texture comes from the desktop).
- What is not a tag type or a number is not restricted: tooltip, action and image names, background and underline brushes (a brush is any text handed to the converter of colors), the name of a color, and the text keep every character they had.

## Consequences

- Texts that change meaning (the list of the entries of the differential corpus whose runs changed, legacy pipeline before and after):
  - `[bacKground=Red]x`, `[bacKground=Red]x[/bg]`, `[bacKground=Red]x[/bacKground]` and `[/bacKground]x`, with U+212A for the `K`: a background tag and its closing tags before, literal text now.
  - `[img=Icon <Arabic-Indic 16>x<Arabic-Indic 16>]`, its full width and mixed variants, and `[img=Icon <Arabic-Indic 1>x<Arabic-Indic 2>]`: literal text before (the size failed to be read), an image now, whose name is the whole value, size included (`Icon <digits>x<digits>`), since a name is any text up to the `]` and the size is `[0-9]{1,4}`. Such an image has no size of its own and takes the one of its texture, if any.
  - Every other entry of the corpus reads the same before and after, among them the numbers and the types written with other digits and letters: they were already literal text, because the interpretation of their value failed or because no pattern matched.
- A text is read the same under every culture of the process (verified under en-US, tr-TR, fr-FR and the invariant culture). `FTTokenDefinition` keeps `RegexOptions.CultureInvariant` so that no option of the process can change what a pattern matches.
- `AreFormattedRunsKnown` is true for every text but the image case above. A text with a non ASCII character near a `[` is read by the scanner and allocates nothing when it changes (measured: 0 B in setter, Update and Draw).
- Public API unchanged. `IsAmbiguous` and the scanner's second outcome for non ASCII characters were internal.
- Left as it was, because it reads a name and not a type or a number: the lookup of the color names of a brush (`ColorStringConverter`, `ColorTranslator`) uses the current culture to compare `transparent` and a trailing `f` of a factor, and ignores some characters (`[bg=trans<soft hyphen>parent]` is a transparent background, as under every culture tested). `RGBColorRegex` of `XNAColorStringConverter` (outside the text folder) still has `\d`, but a brush with such a digit is rejected anyway by `int.Parse`, so the outcome is the same.
- Tests: `FormattedTextRunsTests.TheRuns_AreTheRunsOfTheLegacyPipeline` and `..._UnderEveryCulture` (the whole corpus, with the entries added for this decision, none declined), `FormattedTextAsciiGrammarTests` (explicit expectations, names and text kept, the Kelvin sign, 20 000 random texts under en-US and tr-TR), `FormattedTextAllocationTests.SetText_OnATextWithNonAsciiCharactersNearTheTags_AllocatesNothing` and `OnlyAnImageWithoutASize_InABlockWithNoDesktop_IsLeftToTheLegacyCode`.
