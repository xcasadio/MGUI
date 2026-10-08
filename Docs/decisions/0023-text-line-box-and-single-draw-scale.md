# ADR-0023: Text lines are sized by the ink of a shared repertoire, and drawn at the scale they are measured with

- **Status**: Accepted
- **Date**: 2026-10-08
- **Source**: this chantier: CasaEngineMonogame `ai-agent/tasks/text-line-box-tasks.md` (decisions D1 to D6, taken with the
  author on 2026-10-08), branch `chantier/text-line-box` from `develop` (`18b2c14a`). It started from text cut at the
  bottom in the CasaEngine demo browser.

## Context

Measured before this chantier with the real fonts (a temporary probe in CasaEngine.Demos):

- `SpriteFontTextEngine` measured a line as `FontSet.Heights[size] × ExactScale`. `Heights` and `Origins` came from a
  tight crop of a set of ASCII characters. The drawers added `DrawOrigin × scale` to the position and passed
  `DrawOrigin` as the origin, which cancels it, so the top of the SpriteFont line landed on the visual top and every
  glyph that reaches lower than the ASCII crop was cut. At 8 pt the line was 12 px for ink spanning rows -3 to 14.
- The SpriteFont atlases MGUI ships cover U+0020 to U+024F. Accented capitals rise above the top of the SpriteFont
  line: `Cropping.Y` is -1 for `É` at 8 pt, -3 for `ǻ`, down to -9 at 72 pt. The content pipeline gives whitespace
  glyphs 1x1 texture bounds at the bottom of the cell (`Cropping.Y` = 22 at 8 pt), which is not ink.
- `FontStashSharpTextEngine.MatchSpriteFontSizing` copied that ASCII line height and a draw origin from the SpriteFont
  atlases, so it cut the same descenders. On the font itself, the ink of U+0020 to U+024F at 11 pt (18.27 px) spans
  -1 to 19 px around the FontStashSharp draw position, for a copied line height of 16.
- Text was measured at `ExactScale` but drawn at `SuggestedScale` unless `MGTheme.FontSettings.UseExactScale` was set:
  a SpriteFont line downscaled from a larger baked size was drawn larger or smaller than the space measured for it.
  `MGColorField` multiplied `LineHeight`, already scaled, by the scale again; `MGRotatedTextLabel` passed a screen-space
  origin to engines that expect native units; `MGDockAutoHideStrip` converted measured widths back to
  `SuggestedScale`.

## Decision

- **Line box.** A text engine sizes each line by its line box: the span from the highest to the lowest ink of the
  characters of `LineBoxRepertoire`, U+0020 to U+024F without whitespace and control characters. Both built-in
  engines use the same repertoire, each on its own font data, with no cross-engine calibration of the height:
  - `SpriteFontTextEngine`: `FontSet.LineBoxes` (new) spans the repertoire glyphs of every style of a baked size, from
    the lowest `Cropping.Y` to the largest `Cropping.Y + BoundsInTexture.Height`; `LineHeight` is its height times
    `ExactScale`.
  - `FontStashSharpTextEngine`: the ink of the repertoire is measured with `TextBounds` on the resolved font; the line
    box is that span in whole pixels. `MatchSpriteFontSizing` keeps calibrating widths only.
- **Draw point.** The draw point of an engine is the top of its line box: `DrawText` shifts its native origin by the
  box top. `ResolvedFont.DrawOrigin` is zero for both built-in engines. Drawers keep one origin rule for any engine:
  they pass `DrawOrigin` as the origin and add `DrawOrigin × scale` to the visual position.
- **One draw scale.** Text is drawn at `ExactScale`, the scale it is measured with, everywhere: `MGTextBlock`,
  `MGColorField`, `MGRotatedTextLabel` (whose origin is the centre of the box in native units), the `DrawTransaction`
  text helpers and the samples. `MGTheme.FontSettings.UseExactScale`, its XAML theme attribute and the `Exact`
  parameter of `DrawTransaction.DrawText`, `DrawShadowedText` and `MeasureText` are removed. The engine side
  (`CasaDrawTransaction`, editor timeline) follows the same rule.

## Consequences

- A line of repertoire characters never draws outside its `LineHeight`, so a clipped text element no longer cuts
  descenders or accents. Lines are taller than before: SpriteFont 8 pt 17 px (was 12), FontStashSharp 11 pt 20 px
  (was 16).
- SpriteFont text that was downscaled from a larger baked size is now drawn at its exact size, which can be slightly
  blurrier than the rounded `SuggestedScale` it was drawn at. `SuggestedScale` stays on `ResolvedFont` but no MGUI
  drawer uses it.
- Breaking changes: `FontSettings.UseExactScale` and the `Exact` parameter are gone; a XAML theme that sets
  `UseExactScale` no longer loads. Outside callers must drop them.
- Characters outside the repertoire (CJK, symbols) may still extend past the box. A font whose glyphs carry no ink in
  the repertoire falls back to the SpriteFont line spacing, or to the FontStashSharp line height.
- Static bitmap fonts registered in `FontStashSharpTextEngine` keep the line height their file declares: their author
  owns it.
- In a line that mixes styles, each SpriteFont atlas keeps its own baseline, as before.
