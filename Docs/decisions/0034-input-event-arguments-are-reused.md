# ADR-0034: The input event arguments are reused

- **Status**: Accepted
- **Date**: 2026-11-16
- **Source**: CasaEngine plan `ai-agent/tasks/mgui-nonalloc-suite-tasks.md`, decisions D13 to D15, taken by the author on 2026-11-16

## Context

The input of `MGUI.Shared/Input` creates a new event object for most physical events. Read in the code and measured on 2026-11-16:

- Mouse (`Mouse/MouseTracker.cs`, `Update`): a press (`BaseMousePressedEventArgs`), a release (`BaseMouseReleasedEventArgs`), a click (`BaseMouseClickedEventArgs`), a drag start (`BaseMouseDragStartEventArgs`) and a drag end (`BaseMouseDragEndEventArgs`) are created with `new` at each event, and a click sequence (`MouseClickSequence`, with its `MouseClickSequenceRoutingMetadata`) at the first click of a sequence. The wheel event (`BaseMouseScrolledEventArgs`) was created at each tick where the wheel moved.
- Keyboard (`Keyboard/KeyboardTracker.cs`, `Update` and `CreateTextInputOnlyPressedEvents`; `Keyboard/KeyboardHandler.cs`): a key press (`BaseKeyPressedEventArgs`) with its input stream (`KeyboardInputStream`), a release (`BaseKeyReleasedEventArgs`) and a click (`BaseKeyClickedEventArgs`) are created with `new` at each key, a press and a stream also for a text-input-only event, and a key repeat (`BaseKeyRepeatedEventArgs`) at each repeat of a handler (`KeyboardHandler.cs`, `GetCurrentKeyRepeatEvent`).
- `KeyboardTracker.IsShiftDown`, `IsControlDown` and `IsAltDown` went through `IsAnyPressed(params Keys[])`, which creates an array and a delegate (with its closure) at each read. `MGTextBox` had a private `IsControlDown(KeyboardTracker)` that avoided the property for this reason.
- The move event (`BaseMouseMovedEventArgs`, `MouseTracker.CurrentMoveEvent`) and the dragged events (`MouseTracker.CurrentDraggedEvents`, one per drag start condition and button) were already refilled and reused (ADR-0022). Neither has a handled state.
- Links between event objects exist: a release holds its press, a click holds its release, `BaseMouseClickedEventArgs.PreviousClickInSequence` holds the previous click, and `MouseClickSequence.FirstClick` and `LatestClick` hold clicks of the sequence; a key release holds its press, a key click its release, a key repeat its initial press. A reused object makes such a link point to a rewritten instance.
- Measured allocation of one event: 56 B for a wheel tick, 464 B for a mouse click (press, release, click, sequence and metadata), 696 B for a key (press, stream, release, click and the modifier reads).
- Who keeps an event after the handler returns: for the wheel event, nobody. The search of both repositories (MGUI and `CasaEngine`, `CasaEngine.Editor`, `CasaEngine.EditorServices`) found only handlers that read the event during the call: no field, property or collection of the type, no lambda that stores or captures it for later, no asynchronous use (the lambda of `MGOverlay` that sets the handled state runs inside the handler). The keepers of the mouse button events and of the key events are listed by the tasks that reuse them (first step of each), before any change.

## Decision

The input event arguments of `MGUI.Shared/Input` are reused. A handler that needs a value after the event is over copies the value.

- Validity of a reused object (written in this ADR and in the doc comment of each type):
  - wheel: during the tick (it is null on a tick without wheel movement, and its handled state is reset at each tick where the wheel moves);
  - press, release, click, drag start and drag end of a mouse button: until the next event of the same kind for the same button (a press stays valid while the button is held and during its release);
  - press, release and click of a key: likewise, per key.
- Two instances stay separate, so that nothing changes: the key repeat (one instance per handler and per key, with its own handled state), and the press created for a text-input-only event (its own instance and its own stream, distinct from those of the held press, so that the release and the click of the same frame keep their press, time and stream).
- At each reuse the handled state is reset and the identity restarts as the one of a new object:
  - a key stream receives a new id, its start time and its initial press, and its owner (`OwnerHandlerId`) is cleared;
  - a click sequence receives a new id, its button and its start time, and its adopted target (`ValidatedTargetId`, `ValidatedTargetHost`) is cleared;
  - a click sequence of a button is valid until the next sequence of that button.
- The members that would designate a rewritten instance are removed, with no alias: `BaseMouseClickedEventArgs.PreviousClickInSequence`, `MouseClickSequence.FirstClick` and `MouseClickSequence.LatestClick`. `ClickCount` and the sequence stay; a caller that needs a value of the previous click gets it as a value.
- `KeyboardTracker.IsAnyPressed(params Keys[])` stays public and unchanged. `IsShiftDown`, `IsControlDown` and `IsAltDown` no longer go through it: each is two `IsPressed` calls (left or right key).

Scope by task of the plan (this ADR records the whole decision, the code follows task by task):

- Implemented with this ADR (T5.1): the wheel event (`MouseTracker` refills one `BaseMouseScrolledEventArgs`, its handled state reset, its setters private) and the three modifier properties; the private workaround of `MGTextBox` is removed.
- Not implemented yet: the mouse button events, the click sequence and the removal of the three members (T5.2), and the key events (T5.3). Until then these events are still created at each event, and their doc comments do not yet carry the validity above.

## Consequences

- A handler that keeps an event object after its call (in a field, a list, a closure that runs later) sees it rewritten by the next event of its kind: it must copy the values it needs (`Position`, `ScrollWheelDelta`, `Button`, times, `ClickCount`, key, printable value). For the wheel event this concerns external code only; no code of the two repositories keeps it.
- The handled state of a reused wheel event is reset by the tracker at each tick where the wheel moves, so a handler that marked the previous tick's event handled does not hide the next tick from the other handlers. The events delivered, their values and the handled routing are the same as before; only the object identity changes.
- `CurrentScrollEvent` keeps its meaning: null on a tick without wheel movement.
- Public API: `BaseMouseScrolledEventArgs.ScrollWheelDelta` and `Position` now have a private setter (they were get-only; no change for callers). The removal of `PreviousClickInSequence`, `FirstClick` and `LatestClick` (T5.2) is a breaking change of the public API, accepted by the author (modern design over compatibility); external code that reads them must be updated, and any value of the previous click that a caller needs is exposed as a value.
- The zero test of a wheel frame (`ScrollViewerWithTallContent_Update_WithTheMouseWheel_AllocatesNothing`), the zero test of the modifier reads and the tests of the identity, values and handled state of the wheel event cover this decision; the later tasks turn the measured remainders of the click and the key (464 B and 696 B) into zero.
- Left for later tasks: the mouse button events and the click sequence (T5.2), the key events, the key stream and the key repeat (T5.3), with the keeper audit of each, and an independent verification of the three (T5.4).
