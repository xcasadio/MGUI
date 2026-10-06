# ADR-0022: An allocation-free slider drag, from the input trackers to the value subscriber

- **Status**: Accepted (numbered 0021 on its branch, renumbered 0022 at its merge into `develop`, after ADR-0021 "Image
  brightness above 1")
- **Date**: 2026-10-06
- **Source**: this chantier: CasaEngineMonogame `ai-agent/tasks/mgui-slider-nonalloc-tasks.md` (decisions D1 to D11, taken
  with the author on 2026-10-06), branch `chantier/mgui-slider-nonalloc` from `develop` (`d3e0cd1`). It follows
  decision D20 of the engine's audio plan: the editable mixer panel will use sliders, and the engine forbids
  allocations in input, update and draw.

## Context

Measured before this chantier, with `MGUI.Tests/AllocationWindow.cs` (an empty allocation context per window):

- `MGSlider.SetValue` allocated an `EventArgs<float>` on every change, even without an outside subscriber: the slider
  subscribed to its own `ValueChanged` to refresh its value label. The label rebuilt its string on every change, even
  when the displayed text stayed the same. `DrawSelf` built a `List` and ran LINQ every hovered or dragged frame.
  `MGColorSlider.ValueChanged` and `ValueChanging` allocated an `EventArgs<float>` per change for their subscribers.
- `MGElement.Update` allocated an `ElementUpdateEventArgs` every frame for every element, and walked three `yield`
  iterators (`GetBorderBrushes`, `GetVisualStateFillBrushes`, `GetFillBrushes`) to tick its brushes.
- The input trackers sorted their handlers with LINQ every frame, walked read-only collections with `foreach`, ran
  LINQ `Any` over their event dictionaries, built the pressed-key lists with `GetPressedKeys().Where().ToList()`,
  and created a new move or drag event args object on every moving or dragging frame.
- In the `UseWPF` build, the one MGUI compiles, a `DataBinding` subscribed through WPF's weak
  `PropertyChangedEventManager`: 192 bytes per notification. `DataBindingManager` keeps every binding by a strong
  reference until `RemoveBinding`, which disposes it and unsubscribes it, so the weak event changed nothing to that
  lifetime.
- `ThicknessUtils.IsEmpty` enumerated the four sides with LINQ: 64 bytes for every uniform border drawn.

## Decision

- `MGSlider.ValueChangedNonAlloc` and `MGColorSlider.ValueChangedNonAlloc` / `ValueChangingNonAlloc` are additive
  `EventHandler<(float PreviousValue, float NewValue)>` events, raised right after the existing events with the same
  values. The existing events keep their signature, are not marked obsolete, and allocate only for their own
  subscribers. The slider refreshes its label directly, at the point it did before (before any subscriber).
- The slider formats its value with `float.TryFormat` into a private buffer and builds a string only when the
  displayed text changes. `DrawSelf` draws the two number line pieces without a list.
- `MGElement` creates its update event args only when one of the four update events has a subscriber, and collects
  the brushes to tick through the protected virtual `CollectBorderBrushes`, `CollectVisualStateFillBrushes` and
  `CollectFillBrushes`, into lists reused per thread. They replace the three iterators (a protected API change; the
  16 overrides of `MGUI.Core` are migrated).
- The trackers walk their handlers in an order cached until a handler is added or removed; buttons, drag conditions
  and keys are walked over static arrays next to the unchanged public collections; the pressed keys are read through
  `KeyboardState.GetPressedKeys(Keys[])` into reused lists. `MouseTracker` refills one move args instance and one
  dragged args instance per drag condition and button.
- `DataBinding` subscribes directly (`+=` and `PropertyNameHandler`, the code of the build without WPF) in every build.
- `ThicknessUtils.IsEmpty` reads the four sides directly.

## Consequences

- A dragged slider allocates nothing on its own path: input update, handlers, element update, value change and
  draw, with the label hidden or showing an unchanged text (`MGUI.Tests/Architecture/SliderDragFrameAllocationTests.cs`).
  Every change above has a zero-allocation test checked against the former code.
- Visible changes: `MouseTracker.CurrentMoveEvent` and `CurrentDraggedEvents` return the same object from one frame
  to the next (read them during the tick, do not keep them); neither type carries a handled state. A
  `PropertyChanged` raised with a null or empty name ("all properties") no longer reaches a binding: before, it
  re-resolved a dotted path and made the source and target handlers throw. No code of MGUI or of the engine raises one.
- A subclass that overrode `GetBorderBrushes`, `GetVisualStateFillBrushes` or `GetFillBrushes` must override the
  `Collect*` methods instead.
- Out of this decision, each with its own plan in the engine repository: a text that really changes (the whole
  `MGTextBlock` pipeline, about 3.2 KB per change) and the rest of a desktop frame (window ordering, windows, layout).
