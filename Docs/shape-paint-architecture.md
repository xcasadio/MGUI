# Shape / Paint Architecture

## Goal

The rounded-shapes refactor must separate three responsibilities that are still partly mixed today:

1. shape description
2. geometry construction
3. paint application

The immediate target is box-like UI shapes: rectangular fills, rounded fills, rectangular borders, and rounded borders with thickness.

## Layer Responsibilities

### Shape Model

Shape models describe *what* must be drawn, without deciding *how* it is tessellated or painted.

For the current phase, the canonical shape model is `MGBoxShape`.

It owns:

- outer bounds
- border thickness
- corner radius
- normalization and clamping helpers
- derived values such as inner bounds and inner corner radius

It does not own:

- vertex generation
- polygon winding
- texture projection
- gradient sampling
- batching or draw-context selection

### Geometry Builder

The geometry builder converts a normalized shape into reusable geometry data.

It owns:

- contour generation
- ring generation for borders
- rounded-corner tessellation
- reusable vertex/index payloads
- fast-path branching between simple rectangles and rounded paths

It does not own:

- colors
- textures
- gradients
- animation state
- visual-state overlays

### Paint / Brush Layer

Paints consume either a shape abstraction or precomputed geometry and decide how pixels are produced.

They own:

- solid fills
- solid borders
- texture projection rules
- gradient rules
- banding / composition
- animation or stateful modulation when explicitly part of the paint contract

They do not own:

- reconstruction of the rounded-rectangle topology
- ad hoc computation of edge/corner segmentation that should be shared
- independent clamping rules for bounds, thickness, or corner radii

## Rules For Brushes

Brushes must not become shadow geometry builders.

Specifically, a brush must not be the place where MGUI redefines:

- the outer contour of a rounded rectangle
- the inner contour of a rounded rectangle
- the border ring topology
- the clamp rules for invalid corner radii
- the fast-path selection for `CornerRadius == 0`

Small paint-local transforms remain acceptable when they are genuinely paint concerns, for example:

- texture UV or destination mapping
- color interpolation rules
- highlight or visual-state overlays
- composition of multiple paints over the same geometry

## Fast Path Policy

The architecture must preserve a clear rectangle fast path.

When `CornerRadius == 0`, MGUI should prefer the existing rectangle-oriented rendering primitives and avoid rounded tessellation.

That choice must be centralized in the geometry/rendering layer, not duplicated across controls or brushes.

## Stateful Paints

Some paints already carry state or animation behavior.

This is allowed, but the lifecycle contract must stay separate from shape construction:

- shape normalization remains deterministic and stateless
- geometry generation remains deterministic for a given normalized shape
- stateful paints may modulate rendering, but should not redefine the underlying shape topology

## Phase 1 Scope

Phase 1 must fully support:

- solid fill on box shapes
- solid border on box shapes
- rounded border ring rendering
- rectangle fast path

Phase 1 does not need to fully deliver every advanced paint on rounded shapes.

However, the design must keep future support natural for:

- gradients
- textures
- banded borders
- composited paints
- future clipping and hit-testing reuse

## Practical Consequence For The Refactor

When choosing where new code belongs, use this decision rule:

- If the logic changes the contour or topology of the box, it belongs to shape or geometry.
- If the logic changes color, texture, interpolation, layering, or animation over an existing contour, it belongs to paint.
- If the logic only optimizes rectangle-vs-rounded dispatch, it belongs to the rendering/geometry path, not to individual controls.