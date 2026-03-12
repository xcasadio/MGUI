# Composable Clip Pipeline Architecture

## Overview

MGUI now exposes clipping as a logical contract instead of a hard-coded scissor side effect.

The pipeline is split into four layers:

1. UI elements declare clip intent through `ClipDefinition`.
2. `ClipStrategyResolver` maps that intent to an effective backend.
3. `ClipManager` pushes and pops the corresponding GPU state.
4. `DrawTransaction` keeps the state transitions consistent across `SpriteBatch`, `PrimitiveBatch`, render targets, and transforms.

## Clip Model

Core abstractions live in `MGUI.Shared.Rendering.Clipping`:

- `ClipKind`: none, rectangle, rounded rectangle, arbitrary geometry.
- `ClipDefinition`: logical request emitted by the UI layer.
- `ClipShape`: bounds, optional corner radius, optional geometry payload.
- `ClipResolveResult`: requested clip, effective clip, selected strategy, and fallback info.
- `ClipScope`: disposable lifetime that restores the previous state.

Controls do not choose scissor, stencil, or mask directly. They only return clip definitions.

## Strategy Resolution

`ClipStrategyResolver` owns the central policy.

Default mapping:

- rectangle -> scissor
- rounded rectangle -> stencil
- arbitrary geometry -> mask
- rectangle fallback -> scissor when explicitly allowed

Additional fallback rules remain centralized so `MGElement` and controls stay backend-agnostic.

## Fast Path: Scissor

Rectangular clips stay on the scissor fast path.

- existing nested rectangle intersection semantics are preserved;
- `PushRectangleClip(...)` is the preferred rectangle API;
- `SetClipTargetTemporary(...)` remains as a compatibility shim for migration.

This keeps the common case cheap and matches prior behavior.

## Rounded Clips: Stencil

Rounded content clips reuse existing rounded box tessellation.

- `MGBorder` reuses `MGBoxGeometry` to describe rounded content clipping;
- stencil nesting uses increment-on-push and decrement-on-pop;
- maximum nesting depth is 255 and overflow throws;
- shape painting itself still uses the existing brush/geometry path.

Important rule:

- do not use stencil just to draw rounded shapes;
- use stencil only when subsequent drawing must be constrained by a non-rectangular region.

## Fallback: Mask / Render Target

Arbitrary geometry and unsupported non-rectangular cases use a render-target mask fallback.

- temporary render targets are now pooled by size/format;
- mask clips allocate from transformed bounds in render-target space;
- content is drawn into the temporary surface under the active transform, then composited back.

This path is intentionally not the default for simple cases.

## MGElement Integration

`MGElement.Draw(...)` is now clip-strategy agnostic.

- `GetSelfClipDefinition(...)` describes the clip applied around decorative/self drawing.
- `GetContentsClipDefinition(...)` describes the clip applied only around hosted content.
- overlays are drawn after the content clip is disposed, so element chrome follows self clip rather than content clip.

Examples:

- `MGBorder` declares rounded content clip intent when `ClipToBounds` is enabled and the inner shape is rounded.
- `MGScrollViewer` declares a viewport rectangle clip through the shared clip-definition contract.
- docking overlays explicitly opt out of element clip scopes.

## Shapes And Clips

The shape paint architecture and clip architecture are deliberately separate.

- shape code decides how to draw the visual;
- clip code decides how later draw calls are constrained;
- shared rounded geometry may be reused by both, but the responsibilities stay distinct.

This keeps rounded painting working even when no non-rectangular clip is needed.

## Transforms And RenderScale

The pipeline uses a stable coordinate convention:

- rectangle bounds are expressed in render-target space;
- clip geometry stays in local draw space;
- the active `DrawTransaction` transform maps geometry to the render target.

That lets `RenderScale` keep working without leaking scissor-specific logic into `MGElement`.

## Diagnostics

Each draw transaction now exposes clip diagnostics:

- scissor clip count
- stencil clip count
- mask clip count
- maximum stencil depth reached
- temporary render-target rent count
- temporary render-target reuse count

`DrawTransaction.GetClipDiagnosticsDebugText()` provides a simple debug string for quick inspection.

## Migration Guidance

Use the following guidance when extending the system:

- new rectangle-only call sites: `PushRectangleClip(...)`
- new element-level intent: override `GetSelfClipDefinition(...)` / `GetContentsClipDefinition(...)`
- new non-rectangular cases: return a logical `ClipDefinition` and extend the resolver/manager only when a new backend behavior is required

For more detailed migration notes, see `Docs/clip-migration-guide.md`.

## When To Use Which Strategy

- Use rectangle clip when axis-aligned bounds are sufficient.
- Use rounded rectangle clip when visible content must respect rounded corners.
- Use arbitrary geometry clip only when neither rectangle nor rounded rectangle can describe the region.
- Allow rectangle fallback only when the caller accepts a coarser clip.

This keeps the default path fast while leaving room for more advanced clip shapes.