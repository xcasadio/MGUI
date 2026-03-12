# Responsive Layout Migration Guide

## Goal

Migrate existing fixed-size UI toward screens that adapt to arbitrary viewport sizes with predictable behavior.

## Recommended Migration Path

1. Keep existing screens untouched unless they need responsive behavior.
2. Introduce a responsive root for the screen subtree that should adapt.
3. Set `MGDesktop.ResponsiveSettings` with a deliberate design resolution.
4. Convert the main structure to layout containers first.
5. Add anchors only for HUD-like or overlay-like elements.
6. Opt text into separate scaling where readability matters.

## Choosing A Design Resolution

Use a design resolution that matches the screen you actually author against.

Practical guidance:

- 1920x1080 is a good default when the UI is authored for modern desktop usage.
- 1600x900 is a reasonable default when you want a slightly denser baseline.
- Do not choose an artificially tiny resolution just to force everything to upscale.

The design resolution is not a target render size. It is the reference space used to resolve metrics.

## When To Use Layouts

Use `MGStackPanel`, `MGDockPanel`, `MGGrid`, and similar containers for:

- main screen structure;
- forms;
- content regions;
- headers, sidebars, and footers;
- repeated blocks of content.

This should remain the default composition model.

## When To Use Anchors

Use `ResponsiveAnchor` for:

- corner badges;
- HUD counters;
- floating action panels;
- centered overlays;
- edge-pinned widgets.

Do not use anchors as a substitute for full page composition when layouts are the better fit.

## Spacing And Clamps

Responsive layout scales margins, padding, preferred sizes, and min/max sizes for participating elements.

Recommended practice:

- keep authored values in design-space units;
- rely on the desktop metrics to resolve actual dimensions;
- keep sensible min/max clamps so the UI does not become unusable on tiny or very large screens.

## Text Scaling

Text scaling is separate from layout scaling.

Use `UseResponsiveTextScale` on text where readability should track the global text scale.

Recommended practice:

- enable it for screen headings, body copy, HUD labels, and instructional text;
- keep it off only when a text element must preserve a very specific authored size.

## DPI

DPI participation is available through `MGDesktop.ResponsiveSettings.UseDpiScale` and `MGDesktop.EffectiveDpiScale`.

Recommended rollout:

1. ship responsive layout first with DPI disabled;
2. validate the visual system on different viewport sizes;
3. enable DPI once the application has a reliable source of effective DPI.

## Existing APIs That Remain Unchanged

- `MGWindow.Scale` remains a render transform.
- Existing fixed-size windows still work without opting into responsive layout.
- Existing layout containers remain the preferred structural abstraction.

## Suggested First Migration

For an existing fixed screen:

1. wrap the content in `MGResponsiveRoot` or set `UseResponsiveLayout = true` on the root;
2. configure `MGDesktop.ResponsiveSettings`;
3. replace manual absolute placement with standard layout containers where possible;
4. convert only the truly floating elements to anchored placement;
5. enable `UseResponsiveTextScale` on the text that must stay legible.

## Reference Sample

See the responsive sample in the sample application:

- `ResponsiveLayoutSample` in `MGUI.Samples`;
- `Features/ResponsiveLayout.xaml` for a declarative example of `ResponsiveRoot`, layouts, anchors, and separate text scaling.

## Known Limitations

- The first implementation resolves responsive metrics from the desktop viewport, not per-window viewport variants.
- Existing controls that hardcode raw pixel math outside the central layout pipeline may still need incremental adoption work.
- DPI is intentionally opt-in and disabled by default.