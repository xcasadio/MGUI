# ControlTemplate Structural Runtime Risk Review

## Scope

This note focuses on remaining runtime risks after the first structural-template migration wave (`Window`, `Overlay`, `ComboBox`, `TabControl`) and before the next wave (`ListBox`, `ListView`). The goal is to isolate behaviors that can still fail at runtime or make future migrations fragile.

## Findings

### 1. Pre-attachment property access remains a recurring failure mode

Observed on:

- `MGWindow.TitleText`, `MGWindow.IsTitleBarVisible`, `MGWindow.IsCloseButtonVisible`, `MGWindow.IsUserResizable`
- `MGOverlay.ShowCloseButton`

Root cause:

- control properties directly read or write template parts before `AttachControlTemplateStructure(...)` has run.
- base constructors and XAML loaders can touch these properties before the structural template is attached.

Implication:

- any migrated control that exposes chrome-facing properties must keep a logical backing state independent from the live part instance.

Migration rule:

- when a property controls a template part, keep a backing field and reapply it in `AttachControlTemplateStructure(...)`.

### 2. Base-constructor template application can conflict with derived control requirements

Observed on:

- `MGContextMenu`, which inherits from `MGWindow`

Root cause:

- `MGWindow` applies `Window.Default` during base construction, before the derived type can switch to its own template.
- derived requirement validation may therefore run against the base template first.

Implication:

- derived controls with custom template-part contracts cannot assume their final template is active during base construction.

Migration rule:

- requirement declarations for derived controls must tolerate the base-template phase or delay stricter assumptions until their own template is active.

### 3. Mixed manual parts and structural parts are still common in hybrid controls

Observed on:

- `MGListBox`
- `MGListView`
- `MGTreeView`
- several docking controls still using legacy template defaults only

Root cause:

- many controls still build their visual structure in constructors and call `RegisterTemplatePart(...)` manually.
- the catalog template then only applies defaults over that prebuilt structure.

Implication:

- these controls remain partially lookful and keep a fragile split between constructor logic and template logic.
- migrating them incrementally is still safe, but only if one control fully owns each part after migration.

Migration rule:

- do not leave the same part creatable from both constructor code and structural template code.
- once migrated, the control should attach template-provided parts and stop constructing the equivalent chrome itself.

### 4. Template switching is still safest only for single-content-host roots

Observed in:

- `MGElement.ClearInstantiatedTemplateStructure()`

Root cause:

- the generic detach path only clears `Structure.Root` automatically for `MGSingleContentHost`.
- controls with extra components or managed sub-elements rely on custom attach paths and do not yet have a generic custom detach path.

Implication:

- runtime switching between multiple structural templates on complex composite controls may leave stale component references if migration code is not explicit.

Migration rule:

- for composite controls with components, reuse component slots where possible.
- avoid supporting arbitrary frequent runtime template swaps unless detach logic is explicit and tested.

### 5. Content-host locking order is a real template authoring hazard

Observed on:

- `ComboBox.Default` structure setup

Root cause:

- setting `CanChangeContent = false` before the initial tree is fully wired causes immediate runtime failures.

Implication:

- structural template factories and XAML-authored templates with content hosts must follow a strict order.

Migration rule:

- wire children first, then lock the host.

### 6. Remaining legacy-template controls are the main runtime-risk surface

Controls currently using `ControlTemplateName` without structural hooks:

- `MGListBox`
- `MGListView`
- `MGTreeView`
- `MGContextMenuItem`
- docking controls such as `MGDockTabItem`, `MGDockAutoHideDrawer`, `MGDockAutoHideStrip`, `MGDockSplitterBar`, `MGDockDropIndicators`

Implication:

- these controls are the most likely to hit the same category of failures during future migrations because they currently rely on constructor-built parts.

## Recommended guardrails for the next migration wave

1. Migrate one control at a time from constructor-built chrome to template-owned chrome.
2. Add `GetRequiredControlTemplateParts()` before removing constructor-built structure.
3. Reuse existing component fields instead of adding parallel ones during attach.
4. Cache logical property state for any setter that targets a template part.
5. Prefer stable template identity over frequent runtime swapping for composite controls.
6. Add narrow architecture tests for constructor-phase safety and template-part ownership.

## Immediate targets

The next safest migrations are:

1. `MGListBox`
2. `MGListView`

Reason:

- both already expose clear part names;
- both already have catalog defaults;
- both concentrate a large amount of remaining constructor-built chrome;
- neither requires inventing a new structural-template lifecycle primitive beyond what already exists.