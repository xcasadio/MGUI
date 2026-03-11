## Rounded Shapes Final Architecture

This document is the contributor-facing summary of the phase-1 rounded shape architecture.

### Layer split

#### Shape model

- `MGCornerRadius` represents the public corner-radius contract.
- `MGBoxShape` represents a normalized box-like shape: bounds, border thickness, and corner radius.
- Validation and clamp happen at this layer so downstream geometry and caching only consume normalized input.

#### Geometry builder

- `MGBoxGeometryBuilder` converts a normalized `MGBoxShape` into reusable geometry.
- `MGBoxGeometry` contains the reusable outer contour, inner contour, shared vertices, fill indices, and border-ring indices.
- Geometry caching is centralized in the builder and keyed by normalized shape plus tessellation.

#### Draw transaction primitives

- `DrawTransactionBoxShapeExtensions` is the low-level rendering bridge.
- It exposes shape-aware fill and border operations while preserving the rectangle fast path.
- This layer owns triangle-list emission, not the controls or brushes.

#### Fill paints

- Fill brushes should consume `IFillBrush.Draw(..., MGBoxShape, MGBoxGeometry)` whenever possible.
- Solid and gradient fills already render directly from the supplied geometry.
- Advanced paints can fall back locally when the missing feature is clipping, UV projection, or non-rectangular masking.

#### Border paints

- Border brushes should consume `IBorderBrush.Draw(..., MGBoxShape, MGBoxGeometry)` whenever possible.
- Uniform, banded, docked, and composed border paints now rely on the supplied geometry instead of rebuilding shape topology in controls.
- Rectangle-oriented texture/highlight behaviors stay localized in those brushes until phase 2 rendering support exists.

#### Future clip shape

- Rounded clipping is intentionally deferred.
- The current architecture keeps a clean seam where a future clip primitive, stencil path, render target mask, or shape-aware hit test can attach without rewriting controls.

### Where to add new code

#### Add a new geometry

Add it near `MGBoxShape` / `MGBoxGeometryBuilder` if the work changes topology or tessellation.

- If the new feature changes contour generation, it belongs in the geometry builder layer.
- If it changes validation or normalization, it belongs in the shape model.
- If it changes cache reuse, it belongs in the builder cache key and cache lifecycle.

#### Add a new brush

Add it in the fill or border brush layer.

- Prefer the shape-aware overload first.
- Consume `MGBoxGeometry` instead of reconstructing contours.
- Fall back to the rectangle overload only when the missing capability is truly rendering-specific.

#### Add a new render primitive

Add it to `DrawTransactionBoxShapeExtensions` or the shared rendering layer.

- Keep primitive concerns at the draw-transaction level.
- Avoid teaching controls or brushes how to emit low-level triangle topology directly.

### How to add a new paint without breaking the architecture

1. Decide whether the paint is a fill or a border concern.
2. Implement the shape-aware overload.
3. Reuse `MGBoxGeometry` for contours, fill meshes, or border rings.
4. Keep clipping / UV / masking limitations local to the paint implementation.
5. Only extend the rendering layer when multiple paints would benefit from the same primitive.

### How to add a new shape later

1. Introduce a dedicated shape model, equivalent to what `MGBoxShape` is for box-like controls.
2. Add a geometry payload type for reusable mesh/contour data.
3. Add a builder with normalization and caching.
4. Add draw-transaction primitives for the new shape.
5. Extend brushes only if the new shape can be painted through a common abstraction.

The key rule is unchanged: controls select a shape, the geometry builder computes reusable topology, and paints only decide how that topology is colored or textured.