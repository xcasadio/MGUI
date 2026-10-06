# ADR-0021: Image brightness above 1, drawn as the image plus an additive pass

- **Status**: Accepted
- **Date**: 2026-10-06
- **Source**: `docs/plan-e19-opcodes.md` §E19.f4c1 and decisions D-E19-100/D-E19-101 of the parent repository (CasaEngine gap
  G11, `CasaEngineMonogame/ai-agent/audits/mgui-gaps-from-xaml-screens.md`). Branch `chantier/e19f4c1-image-brightness`
  from the MGUI commit the engine pins.

## Context

- `MGImage` draws its texture through `MGTextureData.Draw` (`MGUI.Core/UI/MGTextureData.cs:22`), which hands
  `DrawTextureTo` a color mask: `(TextureColor ?? Color.White) * MGTextureData.Opacity * element opacity`. A color mask
  multiplies the texel and each channel of a `Color` is a byte, so a mask can only darken (factor at most 1).
- The PlayStation 1 portrait of the dialogue and of the inventory is a modulated quad whose color goes from 1.99 to 1.05
  times the texel (and back): the hardware multiplies by `color / 128` and saturates at 255, which makes small portraits
  flash while they fly. A plain mask cannot express a factor above 1 (decision D-E19-100 of the parent repository,
  which replaces the earlier decision to drop that ramp).
- `BlendType.Additive` already exists (`MGUI.Shared/Rendering/DrawSettings.cs`, mapped to `BlendState.Additive` by
  `MGUI.MonoGame.Integration/Rendering/MonoGameRenderInterop.cs`): source times source alpha, plus destination.
- `MGImage` already switches the draw settings for the duration of one draw (the linear filter when downscaling,
  `MGImage.cs`), restoring the previous settings afterwards.

## Decision

- `MGImage` gains `Brightness` (`float`, default 1, notifying), exposed as the attribute `Brightness` (`float?`) of the XAML
  `Image` element, so it is bindable like `TextureColor`. The attribute is inert while it is absent or equal to 1.
- Masks, exact (bytes; the element opacity applies afterwards to each draw, as it does today):
  - `k = 1`: one draw, unchanged (white mask, alpha 255).
  - `k < 1`: one draw with an opaque gray mask `round(255 * k)` on the three color channels, alpha 255.
  - `k > 1`: a first draw unchanged, then a second draw in `BlendType.Additive` with an opaque gray mask
    `round(255 * (k - 1))`, alpha 255. The alpha of the second mask must be 255: additive blending multiplies the source by
    its alpha, so a lower mask alpha would add `texel * (k - 1)^2` instead of `texel * (k - 1)`. A transparent texel (alpha 0)
    adds nothing. The sum saturates at 255 per channel, which is the modulate-and-saturate model of the console.
  - With a `TextureColor`, the two masks multiply (texture color times the brightness mask, per channel, rounded).
  - The mask value is clamped to 0..255: `k >= 2` brightens as `k = 2`, and a negative `k` darkens fully.
- The second draw runs with the draw settings derived once from the current ones (`BlendType.Additive`, and the linear filter
  when it applies); the derived settings are cached on the element and the previous settings are restored afterwards, so
  no allocation happens per frame and a draw with `k <= 1` never touches the draw settings.
- **Rejected**: a new `UIDrawFlip`-style parameter or a color mask with components above 1 on `IUIDrawContext`. It would change
  the contract every backend implements for one property of one control. **Rejected**: a shader effect, which is a backend
  dependency (`DrawSettings.BackendEffect`) for something two sprite draws express exactly.

## Consequences

- A view model binds a float to `Image.Brightness`; the engine's portraits of the dialogue and of the inventory use it to
  render the console's color ramp, with no code in the screens.
- An image with `Brightness > 1` costs a second sprite draw and a flush of the sprite batch (a draw-settings change ends the
  batch); images at 1 or below cost the same as before.
- Rounding: the sum of the two draws is within 2/255 per channel of `min(255, texel * k)`
  (`MGUI.Tests/Integration/MGImageBrightnessGpuTests.cs`); the masks are pinned per draw call in
  `MGUI.Tests/Controls/MGImageBrightnessTests.cs`.
- `MGUI.Samples/Features/ImageBrightness.xaml` shows the property: a slider from 0.5 to 2 bound two ways to a view model,
  next to the same image left at 1.
- Engine follow-up: CasaEngine's `ai-agent/audits/mgui-gaps-from-xaml-screens.md` §G11 is recorded as corrected, citing this record,
  once the engine's MGUI submodule pointer picks up this commit.
