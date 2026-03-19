# Docking Visual-Part Vocabulary

## Goal

Define a stable visual-part and state vocabulary for docking controls before migrating their remaining direct draw logic into template-owned visuals.

This note is intentionally narrow:

- it standardizes names and responsibilities;
- it does not introduce new public API;
- it keeps existing docking behavior intact;
- it gives Phase 6 a consistent target for follow-up migrations.

## Scope

This vocabulary applies to:

- `MGDockSplitterBar`
- `MGDockDropIndicators`
- `MGDockPreviewOverlay`
- `MGDockAutoHideStrip`
- `MGDockAutoHideDrawer`
- `MGDockTabItem`
- `MGDockTabGroup`
- `MGDockHost` composite docking surfaces that host the above controls

## Naming Rules

Use the same naming pattern already present elsewhere in the control-template work:

- parts use `PART_*`
- visual states use existing `VisualState` / secondary interaction states where possible
- state-like semantic variants that are docking-specific should remain properties on the owner control until a shared projection exists

Do not create control-specific aliases when the same visual role already exists in another docking control.

## Standard Docking Parts

### Shared container parts

- `PART_Surface`
  - Primary painted surface for the control body.
- `PART_Border`
  - Outer border or frame when visually distinct from the surface.
- `PART_ContentHost`
  - Presenter or container for the hosted docking content.
- `PART_Header`
  - Header strip or title area.
- `PART_HeaderText`
  - Text element that displays a title or caption.
- `PART_HeaderIcon`
  - Optional icon or symbol element shown in the header.
- `PART_Accent`
  - Selection stripe, active underline, highlighted edge, or equivalent accent surface.
- `PART_Overlay`
  - Semi-transparent visual layer drawn over a host region.

### Action parts

- `PART_CloseButton`
  - Close affordance hosted by a control template rather than painted inline.
- `PART_PinButton`
  - Pin or auto-hide affordance.
- `PART_Grip`
  - Resize grip or drag affordance.
- `PART_DragHandle`
  - Dedicated draggable header or handle when distinct from the grip.

### Dock-target parts

- `PART_LeftDropZone`
- `PART_RightDropZone`
- `PART_TopDropZone`
- `PART_BottomDropZone`
- `PART_CenterDropZone`

These names apply both to per-panel indicators and host-edge indicators. If both sets exist in the same control, the owner control differentiates them by context rather than by inventing a second naming scheme.

### Tab-strip parts

- `PART_TabStrip`
  - The strip that owns tab item layout.
- `PART_TabHeader`
  - The clickable visual shell of a single tab.
- `PART_TabTitle`
  - The text content of a tab.
- `PART_TabCloseButton`
  - The close affordance for a tab.
- `PART_TabPinButton`
  - Optional pin affordance if exposed in a tab context.

## Standard Docking Visual Roles

These roles should be represented by template-owned elements or brushes, not by ad hoc draw branches:

- background surface
- border frame
- active accent
- hover accent
- disabled overlay
- preview fill
- preview border
- symbolic glyph content
- grip dots or grip bar
- drop-zone highlight
- drop-zone disabled treatment

## Standard Docking States

### Shared states

All docking controls should continue to use the existing generic interaction states when they apply:

- normal
- hovered
- pressed
- selected
- disabled

### Docking-specific semantic states

The following meanings should be treated as first-class semantic states during migration, even when they remain backed by properties instead of a dedicated enum:

- active
  - The currently selected tab, strip button, or focused docking target.
- preview-visible
  - A preview overlay is currently shown.
- drop-target-active
  - A drop zone is the currently hovered valid target.
- drop-target-disabled
  - A drop zone is visible but forbidden by docking rules.
- auto-hide-open
  - An auto-hide drawer is expanded.
- auto-hide-collapsed
  - The drawer exists but is not expanded.
- resizing
  - A grip or splitter is actively being dragged.

## Per-Control Mapping

### `MGDockAutoHideDrawer`

Current concrete parts already present in code:

- `PART_Header`
- `PART_TitleLabel`
- `PART_PinButton`
- `PART_CloseButton`

Vocabulary alignment:

- `PART_TitleLabel` should be treated as the control-specific realization of the shared `PART_HeaderText` role
- the resize affordance should migrate toward `PART_Grip`
- the content area should converge on `PART_ContentHost`
- pin and close glyphs should migrate from inline drawing toward template-owned symbol content

### `MGDockAutoHideStrip`

Target parts:

- `PART_Surface`
- `PART_TabStrip`
- `PART_TabHeader`
- `PART_TabTitle`
- `PART_Accent`

Notes:

- vertical rotated text is a rendering detail, not a separate part vocabulary
- per-button separators should be treated as border or accent treatment, not independent business objects

### `MGDockTabItem`

Target parts:

- `PART_TabHeader`
- `PART_TabTitle`
- `PART_TabCloseButton`
- `PART_TabPinButton`
- `PART_Accent`

Notes:

- active and hover accents should move out of direct draw logic into template-owned visuals
- close and pin symbols should follow the same symbol-element strategy used in Phase 5

### `MGDockTabGroup`

Target parts:

- `PART_Surface`
- `PART_TabStrip`
- `PART_ContentHost`
- `PART_OverflowHost`
- `PART_Accent`

Notes:

- overflow affordances should be named as hosted controls, not special draw branches

### `MGDockSplitterBar`

Target parts:

- `PART_Surface`
- `PART_Grip`
- `PART_Accent`

Notes:

- grip dots and drag feedback should not remain embedded in `DrawSelf`

### `MGDockDropIndicators`

Target parts:

- `PART_Overlay`
- `PART_LeftDropZone`
- `PART_RightDropZone`
- `PART_TopDropZone`
- `PART_BottomDropZone`
- `PART_CenterDropZone`

Notes:

- per-panel and host-edge indicators share the same zone vocabulary
- symbol textures or arrows are visual content inside a zone, not separate top-level parts

### `MGDockPreviewOverlay`

Target parts:

- `PART_Overlay`
- `PART_Border`
- `PART_Surface`

Notes:

- preview fill and preview frame should be template-driven visual roles

### `MGDockHost`

Target composite roles:

- `PART_Surface`
- `PART_Overlay`
- `PART_ContentHost`
- `PART_AutoHideHost`
- `PART_DropIndicatorHost`
- `PART_PreviewOverlayHost`

Notes:

- host orchestration remains behavioral
- hosted overlays and drawers should consume the vocabulary above instead of inventing local naming schemes

## Migration Rules For Phase 6

When migrating a docking control:

1. keep behavior and layout orchestration on the owner control;
2. move paint-only concerns into template parts, symbol elements, or template-owned child elements;
3. prefer reusing the shared symbol approach from Phase 5 for close, pin, arrow, and grip-like visuals;
4. do not invent a new part name if an existing shared role already fits;
5. if a control cannot express a needed visual using this vocabulary, stop and review before expanding the pattern.

## Recommended Execution Order Inside Phase 6

1. leaf visuals: splitter, drop indicators, preview overlay, auto-hide strip
2. tab visuals: tab item, tab group
3. composite host surfaces: drawer, host overlays, hosted containers

That order keeps the most reusable visual roles stable before composite docking surfaces are updated.