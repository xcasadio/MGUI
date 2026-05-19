# ColorPicker V3 Backlog

This backlog intentionally stays outside the V2 implementation scope so the current picker remains compact and stable.

## Preview Surfaces

- Material preview sphere with configurable roughness/metalness and neutral studio lighting.
- Gizmo/debug preview with axis colors, grid line colors, and transparency over dark/light viewports.
- Custom image preview hook supplied by the host editor, without loading files from `MGUI.Core`.
- Sky/fog preview strip with horizon/zenith blend and depth ramp simulation.

## Authoring Tools

- Gradient editor with draggable stops, alpha stops, midpoint controls, and import/export.
- Color ramps for terrain, heatmaps, data visualization, and debug overlays.
- Color harmony suggestions: complementary, analogous, triadic, split-complementary.
- Palette generation from selected color with contrast-aware UI theme variants.

## Accessibility Simulations

- Color blindness preview modes for protanopia, deuteranopia, tritanopia, and achromatopsia.
- Contrast overlays for full palettes, not only the active picker value.
- Warnings for hue-only state distinctions in debug/gizmo palettes.

## Persistence Formats

- `.mgpalette` package format if the JSON MVP needs typed metadata, preview thumbnails, or editor-specific grouping.
- `.gpl` import/export for interoperability with existing art tools.
- Migration helpers from legacy swatch lists once real project palettes exist.
