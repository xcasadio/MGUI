## Stencil / Mask Strategy

## Scope

This document records the concrete backend strategy used by the composable clip pipeline for non-rectangular clips.

At this stage:

- rectangle clips resolve to scissor;
- rounded clips can resolve to stencil;
- arbitrary geometry is reserved for a later mask fallback path.

---

## Stencil Nesting Strategy

### Chosen approach

Nested stencil clips use an increment/decrement strategy.

For a new nested clip:

1. the pipeline compares against the current stencil depth;
2. matching pixels increment the stencil value;
3. subsequent content draws read `Equal(childDepth)`.

For clip disposal:

1. the pipeline compares against the child stencil depth;
2. matching pixels decrement the stencil value;
3. parent content resumes reading the previous depth.

### Why this strategy was chosen

It matches the current renderer constraints well:

- it does not require separate compare and write reference values;
- it supports proper nesting with a simple stack depth model;
- it restores parent stencil semantics on scope disposal;
- it keeps clip ownership in the rendering layer rather than in controls.

### Root clip behavior

When the first stencil clip is pushed on a surface, the stencil buffer is cleared to zero.

The first clip then writes from depth `0` to depth `1`.

### Nested clip behavior

If the parent stencil depth is `N`, the child clip writes matching pixels from `N` to `N + 1`.

Content rendered inside that scope uses `StencilReadEqual` with reference `N + 1`.

### Pop behavior

When the child scope ends, the same geometry is rendered with `StencilRestoreDecrement` and reference `N + 1`.

The affected pixels return to depth `N`.

---

## Depth Limit

The current implementation caps stencil nesting at `255`.

This aligns with the practical limit of an 8-bit stencil buffer.

### Overflow behavior

If a push would exceed the maximum depth, the renderer throws an `InvalidOperationException`.

This is preferred to silent corruption because:

- clipped content would otherwise fail unpredictably;
- a silent fallback would make debugging much harder;
- excessive nesting is an architecture problem, not a recoverable draw-time detail.

---

## Mask Fallback Position

The mask/render-target fallback is still the designated backend for cases where stencil is not suitable, especially:

- arbitrary geometry clips;
- backend limitations;
- cases where stencil compatibility is not guaranteed.

That fallback remains a separate step because it has very different cost and batching behavior.

---

## Non-Goals Of This Document

- this document does not define every policy decision for strategy resolution;
- this document does not make brushes stencil-aware;
- this document does not require stencil to be used for ordinary shape painting.