# Responsive Layout Architecture

## Goal

Provide a first-class responsive layout pipeline for full-screen UI screens that adapts to arbitrary viewport sizes, aspect ratios, and optionally DPI, while remaining compatible with MGUI's existing layout, text, and rendering architecture.

## Core Principles

1. Responsive layout is a layout concern, not a render transform.
2. `MGWindow.Scale` remains a render-space transform and must not become the primary responsive mechanism.
3. Responsive behavior is opt-in at the screen/root level, then inherited by descendants.
4. Layout scale and text scale are resolved separately.
5. Existing layout containers remain the primary composition model.
6. Anchors are meant for overlays and free-positioned elements, not as a replacement for `MGStackPanel`, `MGDockPanel`, or `MGGrid`.

## Terminology

- `DesignResolution`: the reference size used to author a responsive screen.
- `UIScaleFactor`: the globally resolved scale applied to layout-related dimensions.
- `TextScaleFactor`: the globally resolved scale applied to text metrics.
- `ResponsiveSettings`: the desktop-level configuration for design resolution, clamps, and optional DPI participation.
- `ResponsiveMetrics`: the resolved metrics for the current viewport.
- `ResponsiveRoot`: an opt-in root container for a screen subtree that participates in responsive layout.

## Ownership

### Desktop-level ownership

`MGDesktop` owns the global responsive configuration and resolves metrics from the active viewport.

Reasons:

- the viewport is already a desktop concern through `ValidScreenBounds`;
- the desktop is already the right integration point for global text-engine refreshes;
- multiple windows can share the same viewport-derived metrics without duplicating resolution logic.

### Element-level ownership

`MGElement` remains responsible for layout measurement and arrangement, but it consumes desktop-level responsive metrics only when responsive layout is enabled for its subtree.

### Text ownership

`MGTextBlock` remains responsible for text measurement and drawing, but resolves effective font handles from the separate `TextScaleFactor`.

## Opt-in Model

- Responsive behavior is disabled by default for existing trees.
- A screen opts in by using a dedicated responsive root container or by explicitly enabling responsive layout on a root element.
- Descendants inherit responsive participation unless they explicitly override it.

This preserves backward compatibility for existing controls and windows.

## Resolved Metrics

The first implementation resolves metrics from:

- current viewport width and height;
- configured design resolution;
- a scale mode based on uniform fit using `min(widthRatio, heightRatio)`;
- optional DPI multiplier, disabled by default;
- UI scale clamps;
- text scale clamps and multiplier.

The initial formula is intentionally simple and deterministic so it is easy to test and reason about.

## Scope Of UIScale

When responsive layout is enabled, `UIScaleFactor` applies to:

- margins;
- padding;
- preferred sizes;
- min/max sizes;
- overlay offsets and anchors;
- selected container spacing values.

It does not replace:

- arbitrary render transforms;
- element-specific visual effects;
- clipping transforms already tied to render-scale behavior.

## Scope Of TextScale

`TextScaleFactor` applies to:

- effective resolved font size;
- line padding and text-measurement results.

It is intentionally separate from `UIScaleFactor` so text can remain legible without forcing every block of UI chrome to grow or shrink at the same rate.

## Anchors

Anchors are introduced for overlay-style positioning and screen-level HUD composition.

Initial supported anchors:

- top-left;
- top-center;
- top-right;
- middle-left;
- center;
- middle-right;
- bottom-left;
- bottom-center;
- bottom-right;
- stretch-horizontal;
- stretch-vertical;
- stretch.

Anchors are resolved by the overlay-style container and use measured child size plus scaled offsets.

## Containers

The default recommendation for responsive screens is:

1. use a responsive root;
2. compose the main structure with existing layout containers;
3. use anchors only for overlays, badges, corners, and HUD-like elements.

This keeps the framework aligned with its current layout architecture instead of shifting toward absolute positioning.

## Compatibility Policy

- Existing windows and controls continue to function without responsive participation.
- Existing explicit pixel values remain valid authoring values; when a subtree is responsive, those values are treated as design-space values and resolved through the active metrics.
- `MGWindow.Scale` continues to behave exactly as a render transform.

## Delivery Strategy

1. Add models and metric resolution.
2. Add desktop ownership and invalidation.
3. Add opt-in responsive root and inherited participation.
4. Add anchors through `MGOverlayPanel`.
5. Add text scaling.
6. Expose the new concepts in XAML and samples.

## Explicit Non-Goal

The responsive system must not be implemented by simply resizing windows and applying `MGWindow.Scale` to simulate layout adaptation. That would couple layout, input, clipping, and text behavior to a render transform in a way that is hard to reason about and inconsistent with the rest of MGUI.