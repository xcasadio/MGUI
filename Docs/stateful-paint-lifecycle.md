# Stateful Paint Lifecycle

## Goal

The rounded-shapes refactor must not assume that every paint is a pure stateless function from geometry to pixels.

MGUI already contains paints and paint-like abstractions with dynamic behavior, including:

- animated border highlights
- visual-state overlays
- state-dependent fill selection

The refactor therefore needs an explicit lifecycle contract.

## Lifecycle Split

The new architecture should separate three concerns:

1. shape normalization
2. geometry generation
3. paint state evolution and paint application

Only the third concern may depend on time, UI state, or animation progress.

## Rules

### Shape Layer

The shape layer must remain deterministic and stateless.

Given the same inputs, a shape must normalize to the same result.

### Geometry Layer

The geometry builder must also remain deterministic.

Given the same normalized shape and the same tessellation parameters, it must return the same geometry output.

### Paint Layer

Stateful paints may depend on:

- elapsed time
- hover / pressed / focused visual state
- control-specific values such as progress

But they must not redefine:

- the rounded contour topology
- the border ring topology
- the normalization rules for bounds, thickness, or corner radii

## Current Repo Implications

The current repo already shows why this separation matters:

- `MGHighlightBorderBrush` uses `Update(...)`
- visual-state brushes compute overlays dynamically
- `MGBorderedFillBrush` currently does not compose cleanly with all stateful border paints

This means that state evolution must be treated as a first-class part of the migration strategy, not an afterthought.

## Recommended Contract

During the transition, the repo should preserve these principles:

1. update hooks stay on the paint side
2. shape and geometry objects remain safe to cache independently of animation state
3. cached geometry is keyed only by normalized shape data and tessellation settings, not by visual-state animation
4. composite paints must forward lifecycle calls in a centralized and predictable way

## Practical Migration Guidance

When migrating a stateful paint:

- first move geometry assumptions out of the paint
- then keep its state and update behavior intact
- finally adapt composition paths so stateful paints are not silently dropped or rejected

If a paint needs animation-specific masking or overlays, that logic still belongs to the paint layer as long as it consumes already-defined geometry instead of inventing its own shape topology.