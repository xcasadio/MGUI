## Shape vs Content Clip Findings

## Purpose

This document separates four concepts that are still partially conflated in the current pipeline:

- visual shape;
- content clip shape;
- layout bounds;
- hit test shape.

It also maps the current adopters of rounded shape rendering and the controls that still depend structurally on rectangular clipping.

---

## Vocabulary

### VisualShape

The geometry used to paint the control's visible chrome:

- background fill;
- border ring;
- state overlays that are part of the control chrome.

Today this is shape-aware for box-like controls that route painting through `MGBoxShape` and `MGBoxGeometry`.

### ContentClipShape

The shape that should constrain child content or deferred internal content drawing.

Today this is not a first-class concept. In practice it collapses to a rectangle scissor region driven by `ClipToBounds` or ad hoc `SetClipTargetTemporary(...)` calls.

### LayoutBounds

The logical rectangle assigned to the element for arranging and painting. It is still the primary source rectangle for drawing and clipping decisions.

### HitTestShape

The area considered interactive for pointer hit testing.

This phase does not make hit testing shape-aware. Current hit testing remains effectively rectangle-based through `ActualLayoutBounds` and related bounds propagation.

### SelfClip

The clip that should apply to the control's own draw span if needed.

Today there is no explicit self clip concept; `ClipToBounds` wraps almost the entire `MGElement.Draw(...)` body.

### ContentsClip

The clip that should apply to child content, presenters, or scrollable content.

Today there is no explicit contents clip concept; the same rectangle clip covers background, self, contents, overlay, and most components.

---

## Where These Concepts Coincide

They are effectively identical for simple rectangular controls where:

- the control paints only rectangular chrome;
- child content should not escape the control bounds;
- hit testing is expected to stay rectangular;
- there are no decorative parts extending outside the content area.

This is why the current architecture has remained workable for basic rectangular controls.

---

## Where They Diverge

They diverge as soon as one of the following is true:

- visible chrome is rounded but child content is still clipped as a rectangle;
- visual sub-regions such as a title bar or progress segment inherit host corner semantics;
- decorative overlays, resize grips, shadows, docking adorners, or previews intentionally extend outside the host box;
- hit testing is acceptable as rectangle even when paint is rounded.

This divergence is already present in the current codebase.

---

## Shape-Aware Rendering Adopters

### Core shape infrastructure

The reusable rounded-box rendering stack is present in:

- `MGBoxShape`
- `MGBoxGeometry`
- `MGBoxGeometryBuilder`
- `DrawTransactionBoxShapeExtensions`
- `MGBoxShapeRegionHelper`

### Shape-aware brushes

The brush contracts already expose shape-aware overloads:

- `IFillBrush.Draw(..., MGBoxShape, MGBoxGeometry)`
- `IBorderBrush.Draw(..., MGBoxShape, MGBoxGeometry)`

Many common brushes already consume shape geometry directly, including solid, gradient, texture, composited, bordered, padded, textured, uniform, docked, banded, and highlight brushes.

### Controls with rounded visual shape support

The following controls or shared control families visibly support rounded shape painting today:

- `MGBorder`
- `MGRectangle`
- border-backed controls through `MGElement` + `GetBorder()`:
  - `MGButton`
  - `MGTextBox`
  - `MGComboBox`
  - `MGProgressBar`
  - `MGProgressButton`
  - `MGWindow`
  - `MGOverlay`
  - `MGGroupBox`
  - `MGGridColorPicker`
  - `MGStopWatch`
  - `MGTimer`
  - `MGTabControl`
  - `MGToggleButton`
  - `MGChatBox`
  - `MGMenuBar`
  - `MGMenuBarItem`
  - `MGStackPanel`
  - `VirtualizingStackPanel`
  - `MGGridSplitter`

### Controls with shape-aware sub-region painting

Some controls already have partial separation between host shape and internal visual sub-shapes:

- `MGWindow`
  - title bar background is drawn as a derived sub-shape of the window body;
- `MGProgressBar`
  - completed and incomplete segments derive sub-shapes from the host rounded box.

---

## Controls Still Structurally Bound to Rectangle Clipping

### Viewport and scrolling controls

- `MGScrollViewer`
  - structurally depends on rectangular viewport clipping;
  - directly pushes scissor rectangles in its draw path;
  - rectangular clip remains correct for the current scrollbar/viewport model;
  - rounded content clip may become desirable later for rounded hosts, but it is not the same requirement as viewport clipping.

- `MGTextBox`
  - toggles `ClipToBounds` when scrolling is enabled;
  - keeps internal text rendering behavior separate from rounded visual chrome;
  - a future contents clip abstraction is relevant here.

### Desktop and window orchestration

- `MGDesktop`
  - applies a top-level rectangular clip using screen bounds;
  - this is effectively the root clip scope of the whole UI tree.

- `MGContextMenu`
  - explicitly clears clip state in some nested drawing scenarios via `SetClipTargetTemporary(null, false)`;
  - this indicates the current model relies on imperative scissor management rather than logical clip ownership.

### Partial or ad hoc clipping users

- `MGRatingControl`
  - uses direct scissor calls for partial fills and fractional star rendering;
  - this is clip usage as an effect, not as a general content-clip abstraction.

---

## Rectangular Overlay and Rectangle-First Paths Still Present

Even when the host control can be rounded, several overlay or subordinate paint paths still operate on rectangles only.

### Common pipeline-level rectangle overlays

- `MGElement.OverlayBrush?.Draw(..., Rectangle)`
  - still uses background bounds rectangle;
  - not shape-aware by default.

### Control-specific rectangle overlay or sub-paint examples

- `MGScrollViewer`
  - scrollbar outer and inner overlays are rectangle-based;
- `MGSlider`
  - focus overlays and ticks/number line remain rectangle-based;
- `MGUniformGrid`
  - cell backgrounds and overlays are rectangle-based;
- `MGGridColorPicker`
  - swatch fills, hover overlays, and selection overlays are rectangle-based;
- several docking controls
  - previews, drop indicators, tabs, grips, and separators are intentionally rectangle-based.

These should not all be migrated blindly. Some are true debt, while others are correct because the visual primitive itself is not a rounded box.

---

## Cases Where Clip Should Not Automatically Follow Visual Shape

Rounded visual chrome does not imply rounded content clipping in every case.

The clip should often remain rectangular for:

- scroll viewports;
- docking previews and drop indicators;
- resize grips and handles;
- external shadows or glow-like adorners;
- tab and docking separators;
- overlays that intentionally span a logical rectangle rather than the painted chrome.

This is a core design constraint: a future clip system must allow `VisualShape != ContentClipShape` by design.

---

## Current Matrix

### Rounded host controls

`MGBorder`, `MGRectangle`, border-backed chrome controls, `MGWindow`, `MGProgressBar`

- visual shape: rounded-capable
- current clip: rectangle or none, depending on `ClipToBounds`
- target clip: context-dependent; not always rounded
- priority: high, because these are the controls most likely to need explicit shape/clip separation

### Scroll and viewport controls

`MGScrollViewer`, scroll-enabled `MGTextBox`

- visual shape: may be rounded at the host level
- current clip: rectangle scissor
- target clip: keep rectangle viewport semantics; allow host chrome and contents clip to diverge
- priority: high, because these controls structurally depend on clip behavior

### Desktop / popup orchestration

`MGDesktop`, `MGContextMenu`, tooltip/context-menu interplay

- visual shape: mixed
- current clip: imperative rectangle scissor management
- target clip: explicit root and popup clip ownership
- priority: high, because this is top-level orchestration rather than individual control paint

### Rectangular decorative systems

Docking overlays, docking previews, separators, grid cell overlays, many slider primitives

- visual shape: intentionally rectangular or line-based
- current clip: rectangle or none
- target clip: often unchanged
- priority: medium, because they should mostly be documented as exceptions rather than migrated

---

## Non-Goals of This Phase

- no stencil implementation;
- no mask/render-target clip backend;
- no automatic migration of every rectangle-first paint path;
- no shape-aware hit testing rollout.

The goal is to isolate architectural responsibilities so a future composable clip pipeline can be introduced cleanly.