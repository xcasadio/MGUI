## Rounded Shapes Phase 2 Roadmap

Phase 1 establishes shape-aware geometry and paint contracts for box-like controls. Phase 2 should focus on clipping and interaction so rounded visuals, hit testing, and advanced textured paints all share the same shape semantics.

### 1. Rounded clipping

Goal: allow content and overlays to be clipped by the same rounded shape that is used for painting.

Candidate integration points:

- Add a clip-shape abstraction parallel to `MGBoxShape`.
- Teach `DrawTransaction` how to begin/end a temporary rounded clip region.
- Allow controls such as `MGBorder`, `MGScrollViewer`, `MGImage`, and composed controls to opt into shape-aware clipping.

Expected dependency:

- The clipping representation should consume normalized shape input and, when possible, reuse cached geometry or a derived clip mask key.

### 2. Shape-aware hit testing

Goal: avoid rectangular hit tests when the visible interactive surface is rounded.

Candidate approach:

- Introduce a `Contains(Vector2 point)` or equivalent hit-test contract on future shape abstractions.
- Start with box shapes by checking the inner rectangle plus rounded corner quadrants.
- Allow controls to opt into shape-aware hit testing only where visual semantics require it.

### 3. Clip masks / render targets

Goal: support fills or child content that need non-rectangular masking before a full stencil path exists.

Possible options:

- Render-target mask path using alpha masks generated from cached rounded geometry.
- CPU-generated coverage textures for stable, reusable clip masks keyed by shape + scale.
- Temporary fallback path for platforms where stencil is unavailable or expensive.

Tradeoff:

- Render targets are easier to prototype but add memory and batching costs.

### 4. Stencil / shader path

Goal: provide the most scalable long-term path for clipping and textured rounded paints.

Possible direction:

- Add a stencil-backed clip region API to the rendering layer.
- Use the same geometry contours to populate stencil masks.
- Extend textured fill/border paints to map UVs across rounded triangle meshes.

### 5. Suggested delivery order

1. Add a clip-shape abstraction for rounded boxes.
2. Add shape-aware hit testing for `MGBoxShape`.
3. Prototype one clipping backend, preferably render-target or stencil depending platform constraints.
4. Migrate one control with obvious payoff, such as `MGBorder` clipping child content.
5. Extend textured paints once clipping and UV-capable primitives exist.

### Out of scope for phase 1

- No immediate stencil/shader implementation is required.
- No broad content clipping rollout is required.
- No automatic migration of every hit test in the framework is required.

The key constraint is to keep phase 2 layered the same way as phase 1: shape semantics first, rendering backend second, and control adoption last.