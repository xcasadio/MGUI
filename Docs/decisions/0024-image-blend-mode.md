# ADR-0024: An image picks its blend mode, and the subtractive mode joins BlendType

- **Status**: Accepted
- **Date**: 2026-10-31
- **Source**: this chantier: branch `chantier/image-blend-mode` from `develop` at `3db3c769`, requested by CasaEngine (its
  chantier `screen-fade-actions`).

## Context

- `BlendType` (`MGUI.Shared/Rendering/DrawSettings.cs`) has no mode that subtracts. A backend turns the enum into a state
  through a table whose indexer throws on a missing entry (`MonoGameRenderInterop.BlendMap`), so a new value needs an entry
  in every backend.
- `MGImage` draws with the blend of the surrounding draw settings. It already switches the draw settings around one draw for
  the linear filter and for the second pass of `Brightness` above 1, with settings derived once and cached by reference
  (ADR-0021), and it restores the previous settings afterwards.
- The XAML `Image` element exposes `TextureColor`, `Brightness` and `Stretch`. Nothing lets XAML choose a blend.
- The console formula for subtractive semi-transparency is `destination - source`, saturating at 0. On MonoGame that is
  `BlendFunction.ReverseSubtract`: `BlendFunction.Subtract` computes `source - destination`.

## Decision

- `BlendType` gains `Subtractive`, at the end of the enum so existing numeric values do not move. Color: `ReverseSubtract`,
  `One`, `One`. Alpha: `Add`, `Zero`, `One`, so the destination alpha does not change.
- `MGImage` gains `BlendMode` (`BlendType`, default `AlphaBlend`, notifying). It changes no size, so it does not invalidate the
  layout.
- The XAML `Image` gains the attribute `BlendType` (`BlendType?`). Absent, nothing is written. `BindingPathMappings` renames a
  binding on it onto `BlendMode`.
- At the default, `AlphaBlend` or its other name `Default`, the image keeps the blend of the surrounding settings and never
  touches them, as before. Any other mode derives its settings from the base settings, the linear filter included, cached on
  the element by reference and by mode, so no allocation happens per frame.
- With a blend mode of its own, `Brightness` above 1 skips its second, additive draw: it would mix two modes. The image then
  draws as at 1. `Brightness` below 1 is a single draw with a mask and still applies.
- **Rejected**: per-texel selection inside one image (`DrawSettings.BackendEffect` with a shader). It is a backend dependency for
  something two stacked images express: an opaque image, then a subtractive one, in an `OverlayPanel`, each with the other's
  texels transparent black.

## Consequences

- A screen can draw a texture that darkens what is below it, from XAML alone, with no code.
- `Subtractive` ignores the source alpha: every texel with a color subtracts, whatever its alpha. A texel that must not subtract
  has to be black, not just transparent, in the texture as loaded.
- Element opacity and `TextureColor` still apply, through the color mask.
- An image with a mode of its own costs an end and a begin of the sprite batch around it, like a `Brightness` above 1.
- `BlendMode` is a mode of the sprite draw only: it says nothing about what is below it. An image inside an element drawn to a
  render target first subtracts from that target, not from the screen. This is not covered by a test.
- A backend other than `MonoGameRenderInterop` and CasaEngine's `CasaMonoGameRenderInterop` must add the table entry.
- `MGUI.Tests/Controls/MGImageBrightnessTests.cs` pins the draws per mode, and
  `MGUI.Tests/Integration/MGImageBlendModeGpuTests.cs` pins the pixels on a real GPU.
  `MGUI.Samples/Features/ImageBlendMode.xaml` shows the three modes side by side.
