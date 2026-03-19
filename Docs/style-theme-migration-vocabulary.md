# Style/Theme Migration Vocabulary

## Purpose

This document freezes the terms used by the style/theme migration so architecture notes, tests, and implementation work all talk about the same things.

## Canonical Terms

### Style

A style is a named or implicit set of property setters applied to an element without defining that element's structural visual tree.

In MGUI this includes:

- implicit styles keyed by `MGElementType`;
- explicit named styles resolved through `MGResources.Styles`.

### Theme

A theme is the semantic source of default visual values for control families.

A theme is weaker than local values, bindings, visual states, templates, and styles. It is expected to flow through resource scopes and to support runtime switching.

### Control template

A control template defines the visual structure and default chrome of a control.

It may:

- create named parts;
- attach those parts to the live control instance;
- apply template-owned default values onto the owner and its parts.

It must not be treated as a generic synonym for style.

### Template part

A template part is a named element exposed by a control template for the control's behavior code to bind to.

Required and optional parts are declared through `GetRequiredControlTemplateParts()`.

### Template value

A template value is a resolved value applied by a control template through the shared runtime path.

It is not a local assignment. In precedence terms it sits above explicit and implicit styles, and below visual-state and local overrides.

### Local value

A local value is a value set directly on the control instance by code or XAML.

It represents deliberate instance ownership and outranks template, style, theme, inheritance, and default fallback.

### Local binding

A local binding is a binding-driven local value source. While active, it behaves as a local source in precedence terms.

### Inherited value

An inherited value is a value obtained from the nearest ancestor's resolved value for an inheritable property when no stronger source exists on the current element.

Inheritance is a fallback mechanism, not a peer of local assignment.

### Dynamic resource

A dynamic resource is an indirect property source resolved through `MGResources` and reevaluated when the referenced scoped resource changes.

Its precedence is lower than style and template values, and higher than theme defaults.

### Theme default

A theme default is the value contributed by the currently effective `MGTheme` when no stronger source wins.

It is conceptually different from a local default hard-coded in a control.

### Resolved value

A resolved value is the runtime winner for a property at a given point in time, represented by a value plus source metadata and invalidation metadata.

### Resource scope

A resource scope is an `MGResources` node in the lookup chain.

Current scope categories are:

- `Desktop`
- `Window`
- `Subtree`
- `Template`

## Usage Rules

- Use `style` for setter collections, not for control structure.
- Use `control template` when structure or named parts are involved.
- Use `theme` for semantic defaults, not for instance overrides.
- Use `local value` only when the instance itself owns the assignment.
- Use `template value` when a template applied the winning value through the shared precedence path.
- Use `inherited value` only for ancestor fallback, never for theme lookup.