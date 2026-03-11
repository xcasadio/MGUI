## Advanced Paint Readiness

This note documents how the rounded-box architecture supports advanced paints without forcing every paint to own shape topology.

### Stable phase-1 extension points

- `MGBoxShape` carries the normalized box contract: outer bounds, border thickness, corner radius.
- `MGBoxGeometryBuilder` owns tessellation and caching for reusable rounded-box meshes.
- `MGBoxGeometry` carries the reusable contours, vertices, and fill/border triangle lists.
- `IFillBrush.Draw(..., MGBoxShape, MGBoxGeometry)` and `IBorderBrush.Draw(..., MGBoxShape, MGBoxGeometry)` are the primary paint entry points.

### Supported in phase 1

- Solid fills and solid uniform borders consume the provided rounded geometry directly.
- Composited paints, padded fills, bordered fills, banded borders, and docked solid borders can compose on top of the same geometry contract.
- Rounded gradients are supported by coloring the provided fill mesh vertices instead of rebuilding geometry.

### Explicit phase-1 limitations

- `MGTextureFillBrush` still renders to rectangular bounds until rounded texture clipping / UV projection is added.
- `MGNineSliceFillBrush` still targets rectangular destinations until rounded patch decomposition exists.
- `MGTexturedBorderBrush` still uses rectangular edge/corner placement until textured border sampling is shape-aware.
- `MGHighlightFillBrush` and the `Progress` / `Scan` modes of `MGHighlightBorderBrush` still operate on rectangle-oriented exclusion logic.

These limitations are intentionally localized in the paint implementations instead of leaking into `MGBorder`, `MGRectangle`, or the geometry builder.

### Prepared by design for later work

- Textured fills can project UVs over `MGBoxGeometry.Vertices` once a textured triangle path is introduced.
- Textured borders can reuse `OuterContour`, `InnerContour`, and `BorderRingIndices` to map edge and corner textures over the border ring.
- Multi-band strokes can continue to derive nested `MGBoxShape` instances from `InnerBounds` and `InnerCornerRadius`.
- More advanced composed paints can mix fill and border passes without rebuilding geometry as long as they stay within the shape-aware overloads.

### Design boundary

- Shapes decide topology.
- Geometry builders decide tessellation and cache reuse.
- Paints decide color, texture, animation, and composition.

Any new advanced paint should prefer consuming `MGBoxGeometry` first and only fall back to rectangular behavior where the missing feature is specifically clipping, UV mapping, or non-rectangular exclusion math.