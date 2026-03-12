## Clip Architecture Audit

## Scope

This document captures the current clipping contract exposed to the UI layer and the actual draw lifecycle used by the core UI pipeline before any composable clip abstraction is introduced.

The goal of this phase is factual audit, not stencil or mask implementation.

---

## Current Contract

### Public rendering surface exposed to UI

The UI layer currently sees clipping through a single rectangle-based API on `IUIRenderContext`:

- `SetClipTargetTemporary(Rectangle? Bounds, bool IntersectWithCurrentClipTarget)`

Observed properties of the contract:

- clip kind is implicit and fixed to rectangle;
- clip strategy is implicit and fixed to scissor;
- there is no `ClipKind`, `ClipShape`, `ClipRequest`, or `ClipScope` concept;
- there is no explicit clip stack in the UI-facing contract;
- clip intersection is expressed as a boolean flag instead of a first-class nested clip model.

### DrawTransaction implementation

`DrawTransaction.SetClipTarget(...)` confirms that clipping is not abstracted away from GPU state. The method directly:

- reads `GraphicsDevice.ScissorRectangle` as the current clip state;
- decides whether scissor test should be enabled based on `Bounds.HasValue`;
- intersects the requested bounds with the current scissor rectangle when asked;
- toggles `RasterizerType` between `Solid` and `SolidScissorTest`.

This means the current rendering contract is not just rectangle-only, but also tightly coupled to the current scissor implementation.

### Consequences of the current design

The present contract prevents a modern clip abstraction for several reasons:

1. a control cannot request a clip shape independently of its backend implementation;
2. the UI pipeline cannot express `self clip` versus `contents clip` separately;
3. nested clip semantics are reduced to `replace` or `intersect current rectangle`;
4. clip state is represented by `GraphicsDevice.ScissorRectangle` plus rasterizer state instead of a logical clip model;
5. any future rounded or arbitrary clip would require changing the contract, not just the backend.

---

## MGElement Draw Lifecycle

### Current high-level sequence

`MGElement.Draw(...)` currently performs the following steps:

1. resolve effective opacity and visual state;
2. exit early for invisible or zero-size elements;
3. compute `TargetBounds` from `LayoutBounds`, `Offset`, and current transform;
4. optionally apply `RenderScale`, including a transformed temporary scissor rectangle when one is already active;
5. if drawing is not trivially rejected against the current scissor rectangle, optionally push a new clip rectangle when `ClipToBounds` is true;
6. draw components before background;
7. draw background;
8. draw components before self;
9. draw self;
10. draw components before contents;
11. draw contents;
12. draw components after contents;
13. draw overlay brush;
14. invoke `OnEndingDraw` while the clip is still active;
15. restore temporary transform and scissor state;
16. invoke `OnEndDraw` after restoration.

### What is currently mixed together

The current implementation mixes several concepts that should become separate responsibilities:

- visual bounds:
  - `LayoutBounds` and shape-aware background/self painting derive from these;
- clip bounds:
  - `TargetBounds` is used as the effective requested clip rectangle;
- parent clipping:
  - inherited scissor state is implicitly the parent clip model;
- content clipping:
  - there is no separate content clip stage; the same pushed rectangle wraps background, self, contents, overlays, and most components.

### RenderScale coupling

`RenderScale` currently mutates both transform and clip state in the same method.

This is significant because:

- clip application is not modeled as a logical request resolved after transforms;
- the current clip backend must be manually transformed to stay aligned with the scaled element;
- any future non-rectangular clip backend would need a cleaner separation between transform resolution and clip resolution.

### ActualLayoutBounds versus draw clip

`ActualLayoutBounds` is computed during update by intersecting the element's screen bounds with the parent's actual layout bounds when `ClipToBounds` is involved.

This means the system already has a CPU-side notion of visible layout bounds, but draw-time clipping still pushes a fresh scissor rectangle directly inside `MGElement.Draw(...)`.

The architecture therefore has two related but different mechanisms:

- update-time visibility and content-area propagation through `ActualLayoutBounds`;
- draw-time clipping through scissor rectangles.

Those mechanisms are related, but they are not modeled through a shared abstraction.

### Why this matters for a future clip abstraction

The current lifecycle makes it difficult to introduce a composable clip system because the draw pipeline assumes:

1. `ClipToBounds` implies an immediate rectangle clip push;
2. clip applies to almost the entire element draw span;
3. parent clip inheritance is equivalent to inherited scissor state;
4. overlay timing is tied to the same clip scope as contents;
5. transform handling and clip handling are partially entangled.

---

## Desktop-Level Observations

`MGDesktop.Draw(...)` also pushes a rectangle clip directly using screen bounds before drawing windows, overlays, tooltips, and context menus.

This confirms that the rectangle/scissor assumption is not limited to `MGElement`; it is part of the top-level UI draw orchestration.

---

## Summary

The current clipping model is functional but architecturally narrow:

- it is rectangle-only at the API level;
- it is scissor-only at the implementation level;
- it lacks an explicit clip stack model;
- it does not distinguish visual shape painting from content clipping;
- it does not distinguish self clip from contents clip.

This is sufficient for the current rectangular fast path, but it is not sufficient for a future modern clip architecture where controls request a logical clip and the rendering layer chooses the concrete backend.