# Shape Rendering Audit

## Scope

This audit targets the current rendering pipeline for box-like UI shapes in MGUI, with focus on places where geometry construction and paint application are still coupled.

## Core Entry Points

### `MGBorder`

- File: `MGUI.Core/UI/MGBorder.cs`
- Current role: container-level border rendering entry point.
- Current geometry source: `LayoutBounds` and `BorderThickness` are passed directly to the border brush.
- Current paint application: `BorderBrush?.Draw(DA, this, LayoutBounds, BorderThickness)`.
- Coupling to break: the brush receives raw rectangular geometry inputs and must infer border topology itself.

### `MGRectangle`

- File: `MGUI.Core/UI/MGRectangle.cs`
- Current role: rectangle element with independent fill and stroke paths.
- Current geometry source: `ApplyAlignment(...)` computes `ActualBounds` from `Width` and `Height`.
- Current paint application:
  - fill: `Fill?.Draw(DA, this, ActualBounds)`
  - stroke: direct call to `DA.DT.StrokeRectangle(...)`
- Coupling to break: fill and stroke do not share a common shape model, and stroke bypasses the brush abstraction entirely.

## Low-Level Rendering Primitives

### `DrawTransaction`

- File: `MGUI.Shared/Rendering/DrawTransaction.cs`
- Current role: low-level render context and primitive drawer.
- Relevant primitives:
  - `FillRectangle(...)`
  - `StrokeRectangle(...)`
  - `StrokeAndFillRectangle(...)`
  - `FillPolygon(...)`
  - `StrokePolygon(...)`
  - `FillCircle(...)`
  - `StrokeCircle(...)`
- Coupling to break: higher-level box rendering still depends mostly on rectangle-oriented methods rather than a reusable box-shape geometry pipeline.

## Brush Interfaces

### `IFillBrush`

- File: `MGUI.Core/UI/Brushes/Fill Brushes/IFillBrush.cs`
- Current contract: `Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds)`.
- Coupling to break: the paint abstraction consumes a raw rectangle instead of a normalized shape or precomputed geometry.

### `IBorderBrush`

- File: `MGUI.Core/UI/Brushes/Border Brushes/IBorderBrush.cs`
- Current contract: `Draw(ElementDrawArgs DA, MGElement Element, Rectangle Bounds, Thickness BT)`.
- Coupling to break: border paints receive bounds and thickness directly, so each implementation owns some geometry concerns.

## Fill Brush Audit

### Brushes that are mostly paint-only today

- `MGSolidFillBrush`: fills a rectangle through `DrawTransaction.FillRectangle(...)`.
- `MGCompositedFillBrush`: delegates to nested fill brushes.

These are the easiest candidates to migrate to a future shape-aware fill input.

### Brushes with implicit rectangle geometry logic

- `MGGradientFillBrush`
  - File: `MGUI.Core/UI/Brushes/Fill Brushes/MGGradientFillBrush.cs`
  - Current geometry logic: maps the 4 corners of a rectangle to 4 colors.
  - Coupling: gradient sampling is tied to a quadrilateral built from rectangular bounds.

- `MGTextureFillBrush`
  - File: `MGUI.Core/UI/Brushes/Fill Brushes/MGTextureFillBrush.cs`
  - Current geometry logic: computes destination rectangles from stretch and tile rules.
  - Coupling: destination layout and texture projection are mixed inside the paint implementation.

- `MGNineSliceFillBrush`
  - File: `MGUI.Core/UI/Brushes/Fill Brushes/MGNineSliceFillBrush.cs`
  - Current geometry logic: derives 9 destination regions from margins.
  - Coupling: region decomposition belongs partly to geometry/layout, not only to paint.

- `MGBorderedFillBrush`
  - File: `MGUI.Core/UI/Brushes/Fill Brushes/MGBorderedFillBrush.cs`
  - Current geometry logic: compresses fill bounds by border thickness and delegates to both fill and border brushes.
  - Coupling: this type currently composes geometry and paint responsibilities together.

## Border Brush Audit

### Brushes that mostly paint edges

- `MGUniformBorderBrush`
  - File: `MGUI.Core/UI/Brushes/Border Brushes/MGUniformBorderBrush.cs`
  - Current geometry logic: splits the outer rectangle into 4 edge rectangles.
  - Coupling: edge segmentation is implemented inside the brush.

- `MGBandedBorderBrush`
  - File: `MGUI.Core/UI/Brushes/Border Brushes/MGBandedBorderBrush.cs`
  - Current geometry logic: repeatedly compresses bounds by proportional thickness.
  - Coupling: nested border geometry is reconstructed during paint.

### Brushes with heavier geometry ownership

- `MGDockedBorderBrush`
  - File: `MGUI.Core/UI/Brushes/Border Brushes/MGDockedBorderBrush.cs`
  - Current geometry logic: builds miter corner triangles and edge rectangles.
  - Coupling: corner topology is embedded in brush logic.

- `MGTexturedBorderBrush`
  - File: `MGUI.Core/UI/Brushes/Border Brushes/MGTexturedBorderBrush.cs`
  - Current geometry logic: derives edge and corner destination rectangles, plus transforms.
  - Coupling: shape decomposition and texture paint rules are interleaved.

- `MGHighlightBorderBrush`
  - File: `MGUI.Core/UI/Brushes/Border Brushes/MGHighlightBorderBrush.cs`
  - Current geometry logic: layered on top of an underlay brush with animation behavior.
  - Coupling: paint state, update lifecycle, and border rendering are intertwined.

## Controls That Reuse `MGBorder`

Several controls expose border-related properties by forwarding them to an internal `MGBorder`, including:

- `MGButton`
- `MGToggleButton`
- `MGProgressButton`
- `MGComboBox`
- `MGChatBox`
- `MGTextBox`
- `MGWindow`
- `MGMenuBar`
- `MGProgressBar`
- `MGGridSplitter`
- multiple container controls and presenters

This is useful for migration because `MGBorder` is already the reuse point for many box-like controls. Adding rounded-box support there will unlock a broad part of the UI surface area.

## XAML and Configuration Entry Points

### `MGUI.Core/UI/XAML/Brushes.cs`

- Current role: converts XAML brush declarations into runtime `IFillBrush` / `IBorderBrush` instances.
- Coupling to break: XAML currently instantiates concrete paint implementations that assume rectangle-based inputs.
- Migration implication: a future `CornerRadius` type and shape-aware paint contracts must remain easy to configure from XAML and styles.

## Special Cases Outside the Box Pipeline

### `MGRadioButton`

- File: `MGUI.Core/UI/MGRadioButton.cs`
- Current geometry: computes circles directly from layout bounds.
- Current paint application: direct `FillCircle(...)` and `StrokeCircle(...)` calls.
- Migration note: not a phase-1 box shape, but worth keeping in mind as a future generalized shape pipeline consumer.

### `MGRatingControl`

- File: `MGUI.Core/UI/MGRatingControl.cs`
- Current geometry: computes polygons or circles directly.
- Current paint application: direct polygon/circle rendering and clipping.
- Migration note: also outside the initial rounded-box scope.

## Stateful / Animated Paints

The current architecture already contains paints whose lifecycle is not purely stateless:

- `MGHighlightBorderBrush` implements `IBorderBrush.Update(...)`.
- `VisualStateFillBrush` and related visual-state helpers compute overlays dynamically.
- `MGBorderedFillBrush` explicitly rejects `MGHighlightBorderBrush`, which shows a contract mismatch between composite fill paths and stateful border paints.

This should be treated as a first-class migration concern before changing brush interfaces broadly.

## Main Coupling Points To Break

1. Brush interfaces still consume raw `Rectangle` and `Thickness` instead of a normalized box shape or geometry payload.
2. `MGRectangle` bypasses the brush pipeline for stroke rendering.
3. Several border brushes reconstruct edge and corner topology internally.
4. Some fill brushes also own destination-region decomposition that should not be duplicated across future rounded variants.
5. XAML conversion currently targets rectangle-oriented concrete brush implementations.
6. Stateful / animated paints do not yet fit cleanly into a generalized fill/border composition path.

## Recommended Refactor Starting Points

1. Introduce `MGCornerRadius` and a shared `BoxShape` model.
2. Normalize geometry once before dispatching to painters.
3. Keep a fast rectangle path for `CornerRadius == 0`.
4. Migrate `MGBorder` first, because many controls already flow through it.
5. Treat solid fill and solid border as the phase-1 reference implementation before tackling gradients, textures, and other advanced paints.
