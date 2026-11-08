# ADR-0026: A video element plays host video sources

- **Status**: Accepted
- **Date**: 2026-11-08
- **Source**: this chantier: branch `chantier/video-element` from `develop` at `a3c42cb0`, requested by CasaEngine (its chantier `video-playback`, task T2.1).

## Context

- CasaEngine has to show a video inside a screen. MGUI draws images, and animated images through `IUIAnimatedImage` (ADR-0016): a host-created object, one per
  image, advanced by `MGImage.UpdateSelf` with the elapsed wall-clock time. An animation loops forever and has no end to report.
- A video differs from an animation on three points: it plays once and has an end, the user may want to skip it, and what happens at its end is the screen's
  business (change screen, start a command). MGUI has one link from an element to a named command: `Button.CommandName`, looked up in
  `MGResources.Commands` with `TryGetCommand`.
- A collapsed element is not updated by MGUI (the same fast path that stops a collapsed animated image), so a collapsed video would not advance on its own account.
- `MGImage` owns the drawing (stretching, linear filtering when downscaling, brightness, blend mode). `ActualSource` is set through a private setter that
  relays out only when the render size changes. Its `SourceName` is looked up among the named textures first.
- MGUI has no decoder, no audio and no clock of its own, and its input path is the host's (ADR-0025).

## Decision

- `MGUI.Shared.Assets.IUIVideo : IDisposable` is the host's per-element handle: `Advance(TimeSpan)`, `Restart()`, `CurrentImage`, `CurrentSourceRect`,
  `IsEnded`. The host returns the same image on every frame if it updates one texture in place.
- `IUIAssetProvider.TryCreateVideo(string name, out IUIVideo video)` is a default-implemented member that creates nothing, as `TryCreateAnimatedImage` is.
- `MGVideo` inherits `MGImage`, so it draws exactly like an image. `MGImage` gains, additively: a protected constructor that takes the element type, a
  `protected virtual UpdateActualSource`, a `protected virtual ResolvesTextureNames`, a `protected virtual UsesSource` (which `MGVideo` overrides to false, so that setting `Source` has no effect on a video), and `protected SetActualSource`. The draw is not duplicated.
  `MGElementType.Video` is added after `Image`. `MGImage.Source` and the animation properties have no effect on an `MGVideo`.
- `MGVideo` resolves `SourceName` only through `TryCreateVideo`. It advances the video in `UpdateSelf` and copies the frame into `ActualSource`, which
  relays out only when the render size changes. It disposes the video when `SourceName` changes and in `Dispose`.
- At the first `IsEnded`, it raises `Ended` once and runs `EndedCommandName` through `GetResources().TryGetCommand`, like `Button.CommandName`. The last frame stays.
  An unknown command name does nothing. The video then no longer advances.
- `Skip()` returns true, raises `Skipped` once and runs `SkippedCommandName` only if `IsSkippable` is true and the video is still playing. A skipped video stops
  advancing and never raises `Ended`. `Restart()` plays again and re-arms both.
- The XAML tag `<Video>` has `SourceName`, `Stretch`, `IsSkippable`, `EndedCommandName` and `SkippedCommandName`.
- MGUI reads no input for a video: the host decides what skips, and calls `Skip()`.

## Consequences

- A host that implements nothing keeps compiling: `TryCreateVideo` creates nothing and an `MGVideo` then shows nothing.
- The host owns decoding, pixels, sound and the clock; MGUI only advances the handle. Timing against sound is the host's to arrange.
- No per-frame allocation: `Advance`, the frame copy and the end test allocate nothing, and the frame copy relays out nothing while the size is constant.
- A skipped video raises `Skipped` and not `Ended`, so a consumer that needs one completion signal listens to both or points both commands at the same one.
- Existing images, buttons and screens are unchanged. A new `MGElementType` value shifts the numeric values of the values declared after `Image`, as every insertion by category has.
- The element does not mute or stop anything outside itself: a host that plays sound with the video stops it when it receives `Ended` or `Skipped`.
