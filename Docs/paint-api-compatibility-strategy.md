# Paint API Compatibility Strategy

## Goal

The current paint APIs are rectangle-centric:

- `IFillBrush.Draw(..., Rectangle Bounds)`
- `IBorderBrush.Draw(..., Rectangle Bounds, Thickness BorderThickness)`

The refactor must move toward shape-aware or geometry-aware inputs without forcing an all-at-once rewrite of every brush and control.

## Transition Principle

The migration must be centralized.

Individual brushes must not invent their own compatibility story.

Instead, the transition should rely on a small set of shared bridge points that allow old brushes and new geometry-aware painters to coexist temporarily.

## Recommended Compatibility Stages

### Stage 1

Keep existing public brush interfaces stable while introducing the new shape and geometry layer.

At this stage:

- controls may build `MGBoxShape`
- geometry may be computed centrally
- new rendering primitives may consume `MGBoxShape` / `MGBoxGeometry`
- existing brushes may still operate on rectangle-oriented contracts

This minimizes churn while the new pipeline is proven.

### Stage 2

Introduce centralized bridge helpers or adapters that translate a normalized `MGBoxShape` into the legacy rectangle-oriented calls when the brush has not yet been migrated.

Examples of acceptable bridge responsibilities:

- decide whether a given box can still use the legacy rectangle path
- provide normalized outer bounds, inner bounds, and thickness
- route simple solid rectangle paints through the fast path

Examples of unacceptable bridge responsibilities:

- reimplementing rounded-rectangle topology independently per brush
- hiding brush-specific geometry forks in arbitrary controls

### Stage 3

Migrate brushes incrementally to consume shape-aware or geometry-aware inputs directly.

Once a brush is migrated:

- geometry assumptions should be removed from the brush
- the brush should focus on paint behavior only
- legacy bridge code for that brush path can be retired when no longer needed

## Allowed Compatibility Mechanisms

The repo may use one or more of these mechanisms during the migration:

- overloads
- adapter types
- centralized bridge helpers

The important constraint is not the exact mechanism but the location of the logic: the transition must remain centralized and discoverable.

## Compatibility Constraints

The temporary layer must preserve:

- public API stability where reasonably possible
- rectangle fast path behavior
- deterministic normalization of bounds, thickness, and corner radius
- one canonical rounded-geometry pipeline

## Exit Criteria

The compatibility layer can be reduced when:

1. core controls build `MGBoxShape` directly
2. rounded rendering goes through centralized primitives
3. migrated brushes no longer depend on raw rectangle-only topology assumptions
4. remaining legacy calls are limited to clearly identified transitional cases