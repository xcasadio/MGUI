# Style/Theme Baseline Architecture

## Purpose

This note records the style/theme baseline that is already present in the repository as of 2026-03-19.

It exists to prevent migration work from re-planning infrastructure that has already landed, and to keep later control-level work aligned with the runtime model that is now in code.

## Verified Baseline

### Value precedence model

The runtime already exposes an explicit precedence ladder in `UIValuePrecedence`.

Current order from strongest to weakest:

1. `Animation`
2. `LocalValue`
3. `LocalBinding`
4. `VisualState`
5. `Template`
6. `ExplicitStyle`
7. `ImplicitStyle`
8. `DynamicResource`
9. `Theme`
10. `Inherited`
11. `DefaultValue`

The runtime also carries source metadata through `UIValueResolutionSource` and `UIResolvedValue<T>`, including invalidation kind and source identity.

### Resource lookup model

Resource lookup is already hierarchical.

- `MGResources` instances form a parent chain.
- Child scopes resolve textures, commands, themes, styles, static resources, element templates, and control templates through that chain.
- `UIResourceScope` already distinguishes `Desktop`, `Window`, `Subtree`, and `Template` scopes.
- `MGElement.EnsureResourceScope(...)` materializes local scopes on demand and keeps their parent linkage current.

### Theme refresh behavior

Theme lookup already follows the resource-scope chain.

- `MGResources.DefaultTheme` falls back to `Parent.DefaultTheme` when no local override exists.
- `MGResources.OnDefaultThemeChanged` propagates parent theme changes into descendant scopes unless a local override blocks inheritance.
- `MGElement.NotifyThemeChanged(...)` calls `OnThemeChanged(...)`, reapplies the control template with `ApplyControlTemplate(true)`, and then propagates to child elements and components.
- Window-level theme overrides are already expressed as resource-scope overrides rather than desktop-global mutation.

### Template contract rules

The control-template runtime is already split into separate phases.

- Template resolution order is local template name, theme mapping by runtime type, theme mapping by element type, then `DefaultControlTemplateName`.
- `MGControlTemplate` supports separate structure creation, structure attachment, and defaults application.
- `MGControlTemplateContext.ApplyTemplateValue(...)` and `ApplyThemeDefault(...)` stamp template-origin metadata instead of behaving like anonymous local assignments.
- Migrated composite controls declare required parts through `GetRequiredControlTemplateParts()` and are validated centrally by `ValidateControlTemplateParts()`.

## What This Means For The Remaining Migration

Phases 1 and 2 are not greenfield work anymore.

The main remaining style/theme work is now:

- removing control-local visual literals that still live in templates or control code;
- finishing lookless migration for partially migrated composite controls;
- reducing direct icon and state drawing in manual controls;
- treating docking as a dedicated migration stream with its own visual vocabulary.

## Verification Sources

This baseline was checked against the following code and tests:

- `MGUI.Core/UI/Styling/UIValuePrecedence.cs`
- `MGUI.Core/UI/Styling/UIValueResolutionSource.cs`
- `MGUI.Core/UI/Styling/UIResolvedValue.cs`
- `MGUI.Core/UI/MGResources.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/Styling/MGControlTemplate.cs`
- `MGUI.Tests/Architecture/StyleValueResolutionModelTests.cs`
- `MGUI.Tests/Architecture/ResourceScopeLookupTests.cs`
- `MGUI.Tests/Architecture/ResourceReferenceApplicatorTests.cs`
- `MGUI.Tests/Architecture/ThemeScopeInvalidationTests.cs`
- `MGUI.Tests/Architecture/ControlTemplateInfrastructureTests.cs`