## Clip Abstraction Proposal

## Goal

Define the logical clip model that MGUI should target after the audit/refactor phase and before the composable scissor/stencil implementation phase.

This document does not introduce a backend. It defines the vocabulary and responsibilities that the next phase should implement.

---

## Design Principles

### 1. Clip is a request, not a technique

Controls should declare the clip semantics they need.

They should not decide whether the renderer uses:

- scissor;
- stencil;
- mask / render target;
- no clip.

### 2. Visual shape and content clip are distinct

The same control may have:

- a rounded visual chrome;
- a rectangular content viewport;
- no self clip at all;
- a future non-rectangular content clip.

The model must allow those combinations explicitly.

### 3. Rectangle remains the fast path

Rectangular clipping must stay cheap and close to the current scissor behavior.

### 4. Backend choice belongs to rendering

Strategy resolution belongs to the rendering layer or a dedicated clip manager, not to controls.

---

## Proposed Logical Types

The exact names can still change, but the model should cover the following concepts.

### ClipKind

Describes whether a clip exists and what shape class it belongs to.

Minimal variants:

- `None`
- `Rectangle`
- `RoundedRectangle`
- `ArbitraryGeometry`

### ClipShape

Payload describing the requested clip shape.

Likely data:

- bounds;
- optional corner radius;
- optional normalized geometry reference;
- optional transform-space assumptions.

For box-like UI controls, this should eventually compose well with `MGBoxShape`.

### ClipRequest

Logical request emitted by UI code.

Likely data:

- `ClipKind`
- `ClipShape`
- scope target, such as self or contents;
- optional strategy preference;
- whether the request should intersect with inherited clip;
- whether the request is hard-required or downgradeable.

### ClipScope

Represents the lifetime of an applied clip.

This should become the logical replacement for direct `using (SetClipTargetTemporary(...))` usage.

### ClipStrategyPreference

Optional hint supplied by the caller, not a command.

Possible values:

- `Default`
- `PreferScissor`
- `PreferStencil`
- `PreferMask`

The renderer may ignore a preference when it is incompatible with the clip kind or backend.

### ClipResolveResult

Represents the resolved backend plan.

Likely data:

- effective strategy used;
- whether the request was downgraded;
- disposable scope handle;
- any cached geometry/mask/stencil metadata.

---

## Proposed Scope Split

### Self Clip

Clip that applies to some or all of the control's own drawing operations.

Typical use cases:

- custom self-drawn content that must stay inside a viewport;
- future self effects that should be trimmed to a host shape.

### Contents Clip

Clip that applies to content children or presenters.

Typical use cases:

- scroll viewers;
- text viewports;
- future rounded child masking inside a rounded border.

### Default policy

The default policy should remain conservative:

- no explicit self clip unless a control asks for it;
- contents clip only when a control structurally depends on clipping;
- inherited parent clip still applies implicitly through the resolved clip stack.

---

## Resolution Policy

The logical request should later resolve according to the following policy.

### Required baseline

- rectangle -> scissor
- rounded rectangle -> stencil or mask
- arbitrary geometry -> stencil or mask

### Allowed downgrade path

When the requested strategy or shape cannot be satisfied exactly, the renderer may downgrade only when that downgrade is explicitly allowed by policy.

Examples:

- rounded rectangle -> rectangle scissor only if the caller allows fallback and approximate clipping is acceptable;
- arbitrary geometry -> mask if stencil is unavailable;
- arbitrary geometry -> no fallback to rectangle unless explicitly acceptable for that control.

### Who decides

The rendering layer should decide the final strategy, likely through a future clip manager or render-context resolution service.

Controls should only declare intent.

---

## Relationship With Existing Architecture

### Shape model reuse

For rounded boxes, the future clip model should reuse the same normalized semantics already established for painting:

- `MGBoxShape`
- `MGCornerRadius`
- `MGBoxGeometry` when useful

This avoids inventing a second rounded-rectangle vocabulary for clipping.

### Brush isolation

Brushes should remain independent from clip strategy. A brush may consume shape geometry for painting, but it should not need to know whether the contents behind it are clipped by scissor, stencil, or mask.

### Transform handling

Clip resolution should happen after the effective transform space is known. The logical request should not be tied to a raw scissor rectangle pre-transformed by UI code.

---

## Case Rules

### Cases where rounded visual shape does not imply rounded content clip

- scroll viewers with a rectangular viewport inside rounded chrome;
- docking previews and indicators;
- resize grips and adorners;
- external glow or shadow effects;
- separators and line-based primitives.

### Cases where rounded content clip is likely desirable later

- rounded borders hosting arbitrary child content;
- rounded windows hosting title/content separation with clipping guarantees;
- rounded image or content presenters;
- future rounded panels whose child content should not bleed into clipped corners.

---

## Hit Testing Note

Hit testing must stay independent from this phase.

The model should allow a future `HitTestShape`, but this phase should not conflate:

- visual shape;
- content clip;
- hit test area.

---

## Minimal Coding Implication For The Current Phase

The current phase should only prepare the UI pipeline so that it can ask for a clip logically before the rendering layer resolves it.

That means:

- `MGElement.Draw(...)` should stop treating `ClipToBounds` as the full implementation detail;
- the default request may still resolve to a rectangle clip only;
- the existing scissor backend remains untouched in behavior.

---

## Inputs To The Next Phase

The next phase should implement:

1. a rendering-side clip abstraction;
2. a scissor-backed rectangle implementation;
3. a future stencil-backed rounded implementation;
4. explicit nested clip ownership;
5. migration of selected controls to self clip and contents clip requests.

This proposal is therefore intentionally upstream of `TASKS_COMPOSABLE_CLIP_PIPELINE_WITH_SCISSOR_AND_STENCIL.md`.