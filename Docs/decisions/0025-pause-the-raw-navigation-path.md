# ADR-0025: A host can pause the raw navigation path, and what was held stays silent afterwards

- **Status**: Accepted
- **Date**: 2026-11-01
- **Source**: this chantier: branch `chantier/navigation-input-suspension` from `chantier/image-blend-mode` at `7e1dd328`,
  requested by CasaEngine (its chantier `screen-fade-actions`, which holds the verifier's two findings).

## Context

- CasaEngine has to stop the keyboard and the gamepad for the length of a screen fade, with the focus left in place. It did so by
  setting `MGDesktop.UseRawNavigationInput` to false and restoring the value it found.
- That property says who reads the inputs: "set this to false when an external router is routing UI actions". A pause is a
  different question, and sharing the property forces its user to remember a previous value and to be there to give it back.
- `GamePadTracker` repeats a held button whoever reads it: `Update` sets `_CurrentTriggeredButtons` again every `RepeatInterval`
  once `InitialRepeatDelay` has passed. `TryDispatchGamePadNavigationActions` reads `WasTriggered`, which is true on a repeat as
  well as on a press, and A is in `GamePadNavigationButtons` and maps to Submit. So a button held through a pause fired within
  one interval of its end, with no new press. `_HeldSince` is private, so a host cannot stop that.
- The raw keyboard path has no such problem: it subscribes to `Pressed`, a press edge, and `KeyRepeat` is a separate event.
- A test could not reach any of this: `GamePadTracker.Update` calls MonoGame's static `GamePad.GetState`.

## Decision

- `MGDesktop` gains `IsNavigationInputSuspended` (`bool`, default false). While true, the raw path dispatches nothing, on the
  keyboard and on the gamepad, whatever `UseRawNavigationInput` says. It touches neither that property nor the focus, so there is
  no previous value to keep. It does not gate `TryHandleInputAction`: a host that routes actions stops its own router.
- When it turns from true to false, the desktop calls `GamePadTracker.ForgetHeldButtons`.
- `GamePadTracker` gains `ForgetHeldButtons`: what is held now fires nothing more until it is released and pressed again. It clears
  the triggered flags and the held and repeat times of every button. The tracker skips a held button with no `_HeldSince`, so no
  new state is needed. The branch of `Update` for a disabled tracker now calls it, since it did the same thing.
- `GamePadTracker` gains an internal `StateOverride`, read in place of the device when it is not null. `MGUI.Shared` gains
  `InternalsVisibleTo("CasaEngine.Tests")`, as `MGUI.Core` already has.
- **Rejected**: keeping the desktop's dispatch off until every held button is released. One flag serves the keyboard and the
  gamepad, so holding a gamepad button would keep the keyboard dead, and it needs a list of the buttons navigation reads.
- **Rejected**: using `GamePadTracker.Enabled` for the pause. Turning it off zeroes the states, so turning it back on makes a
  held button look like a fresh press, which is the failure this decision removes.

## Consequences

- A host can pause navigation from a value it works out each frame, with no transition of its own to get right.
- `IsNavigationInputSuspended` has to be set between two calls to `MGDesktop.Update`. Set at the end of a frame, a press on the
  next frame is a new press and fires. Set after the input trackers updated and before `MGDesktop.Update`, a press on that very
  frame is forgotten with the rest.
- The default is false, so a host that never sets it sees no change.
- A focused control that reads keys itself still sees them during a pause: only navigation stops. This comes from reading the code.
- A real gamepad has not been tried. `MGUI.Tests/Focus/NavigationInputSuspensionTests.cs` proves this on injected states, including
  that those states reproduce the repeat the decision silences.
- No sample: nothing here draws, and CasaEngine's chantier runs it in its Launcher.
