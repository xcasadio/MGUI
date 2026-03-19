# Style/Theme Decoupling Classification And Agent Plan

## Scope

This inventory focuses on UI controls and control-like containers that materially affect the style/theme split.

Excluded from the classification:

- low-level helpers with no control behavior of their own;
- brush implementations, geometry helpers, resource helpers, and parser internals;
- pure infrastructure types that do not own visible control structure.

## Classification Legend

- `A - Mostly decoupled`: visual structure is largely template-backed or delegated, with limited direct theme/state projection in control code.
- `B - Partially decoupled`: control uses templates and/or theme hooks, but still binds visual parts directly and copies themed values into runtime properties.
- `C - Weakly decoupled`: control exposes some theme/style hooks, but visual structure or state rendering still lives mainly in control code.
- `D - Hard-coupled`: control behavior and look are still strongly fused through imperative child creation, direct drawing, or custom visual logic.

## Key Signals Used

- presence of `DefaultControlTemplateName`;
- presence of `AttachControlTemplateStructure(...)`;
- presence of `OnThemeChanged(...)` that copies theme values into local properties;
- presence of `OnEndingDraw`, `DrawSelf`, or `DrawContents` with control-specific icon/state drawing;
- presence of imperative subpart construction in the control itself.

## Summary

Current state of the repo is not fully lookless.

- Primitive controls are often theme-aware, but not fully style-system-driven.
- Composite controls increasingly use `ControlTemplate`, but still own visual-part rebinding and state projection logic.
- Docking controls remain the most tightly coupled area.
- Runtime precedence, resource scopes, dynamic resource refresh, and template-part contracts now exist in code, but control-level lookless migration is still incomplete.

## Verification Snapshot

Status in the action list below was verified against the current repo on 2026-03-19.

Verified baseline artifacts:

- `Docs/style-theme-baseline-architecture.md`
- `Docs/style-theme-migration-vocabulary.md`
- `Docs/style-value-precedence-architecture.md`

Verified architecture coverage already exists for:

- precedence ordering and source metadata;
- hierarchical resource lookup and scoped theme inheritance;
- dynamic resource invalidation;
- split control-template structure/attach/default phases;
- required template-part metadata on migrated composite controls.

## Control-By-Control Grid

| Control | Class | Why |
| --- | --- | --- |
| `MGBorder` | A | Core rendering primitive. Visual output is brush-driven and generic rather than business-control-specific. |
| `MGTextBlock` | B | Good theme hook coverage, but still resolves theme-backed font and fallback colors in control code. |
| `MGButton` | B | Simple primitive, but `OnThemeChanged(...)` still copies themed background into local state. |
| `MGToggleButton` | B | Similar to `MGButton`; mostly primitive, but theme projection still happens in control code. |
| `MGImage` | A | Minimal style/theme surface and little structure coupling. |
| `MGSpacer` | A | Layout helper with negligible theme coupling. |
| `MGSeparator` | B | Simple control, but still largely value/property based rather than template driven. |
| `MGScrollViewer` | B | Theme-aware and reusable, but scrollbar visuals still remain control-owned rather than fully template-owned. |
| `MGInputConsumer` | A | Behavioral wrapper with little visual ownership. |
| `MGToolTip` | B | Uses shared window-style templating path, but not fully independent from window/control code assumptions. |
| `MGWindow` | B | Has a proper default control template, but still rebinds concrete parts and enforces window visual behavior in code. |
| `MGOverlay` | B | Same pattern as `MGWindow`: template-backed, yet still tightly aware of concrete visual parts. |
| `MGContextMenu` | C | Default template exists, but menu behavior, submenu draw orchestration, and dropdown button visuals remain code-owned. |
| `MGContextMenuItem` | D | Significant direct visual logic, including manual symbol rendering and state spoofing. |
| `MGListBox` | B | Template-backed composite; recent cleanup stopped constructor/attach-time chrome overrides, but title/content and panel ownership still need a final pass. |
| `MGListView` | B | Similar to `MGListBox`; list/grid spacing, grid-line margins, and header spacer chrome now resolve through template defaults, but structural ownership still remains in control code. |
| `MGComboBox` | C | Now template-backed; dropdown arrow defaults now flow through the template catalog, but dropdown structure and item wrapper behavior still remain partly imperative. |
| `MGTreeView` | B | Has template-based structure; selection visual refresh now follows property changes directly, but some behavior-coupled visual assumptions remain. |
| `MGTreeViewItem` | C | Manual expander drawing remains, but triangle-arrow geometry now goes through a shared primitive instead of per-control polygon code. |
| `MGTextBox` | B | Template-backed and improved, but placeholder/counter/text/caret composition is still coordinated heavily in control code. |
| `MGPasswordBox` | B | Inherits the same strengths and limits as `MGTextBox`. |
| `MGTabControl` | B | Good template split for main structure and headers; background defaults now come from the template catalog, and default header wrappers now retemplate in place, but header panel behavior and custom-wrapper lifecycle still live in control logic. |
| `MGTabItem` | B | Lightweight behavioral companion to `MGTabControl`, but not independently lookless. |
| `MGCheckBox` | D | Manual checkmark drawing and button-part orchestration keep appearance strongly fused with control code. |
| `MGRadioButton` | C | Theme hooks exist, but bubble/check visuals remain control-specific and imperative. |
| `MGSlider` | C | Value-track-thumb visuals are still largely control owned. |
| `MGProgressBar` | C | Themed values exist, but visual composition still belongs to the control. |
| `MGProgressButton` | C | Combines button and progress visuals in control code rather than via a full visual template split. |
| `MGRatingControl` | C | Visual symbol rendering remains control-centric. |
| `MGGridColorPicker` | C | Rich control with custom rendering and behavior-specific visual composition. |
| `MGGroupBox` | C | Header framing logic still depends on control-side layout and measurement behavior. |
| `MGExpander` | D | Header visuals and expander arrow behavior still involve direct drawing and imperative composition, but arrow rendering now uses a shared primitive and theme refresh path. |
| `MGMenuBar` | C | Menu-bar items and menu integration still mix behavioral and visual concerns. |
| `MGSpoiler` | C | Behavior-specific appearance remains owned by control logic. |
| `MGTimer` | A | Mostly behavioral composition over text output; low structural theme coupling. |
| `MGStopWatch` | A | Same profile as `MGTimer`. |
| `MGChatBox` | C | Composite behavior plus layout/visual ownership remains mostly code-side. |
| `MGRectangle` | A | Primitive visual element, not a composite lookless-control candidate. |
| `MGResizeGrip` | C | Specialized visual behavior and direct interaction/render logic. |
| `MGDockPanel` | A | Generic layout container with low style/theme ownership. |
| `MGStackPanel` | A | Generic layout container with low style/theme ownership. |
| `MGOverlayPanel` | A | Generic layout container with low style/theme ownership. |
| `MGResponsiveRoot` | A | Layout/infrastructure type, not a style-heavy control. |
| `MGContentHost` / presenters | A | Core composition primitives that support decoupling rather than fighting it. |
| `VirtualizingStackPanel` | A | Performance/layout container, visually neutral. |
| `MGDockTabItem` | D | Custom tab visuals, manual close icon handling, and docking-specific rendering remain control-owned. |
| `MGDockTabGroup` | D | Strongly imperative structure and manual icon/accent rendering. |
| `MGDockAutoHideDrawer` | D | Template name exists, but actual visuals, icons, border, and grip highlights are still drawn directly in code. |
| `MGDockAutoHideStrip` | D | Rotated text and strip-specific rendering remain code-owned. |
| `MGDockSplitterBar` | D | Manual splitter visuals and grip dots are hard-coded in draw logic. |
| `MGDockDropIndicators` | D | Entire visual system is custom-rendered in code. |
| `MGDockPreviewOverlay` | D | Preview visuals are fully imperative. |
| `MGDockHost` | D | Orchestrator plus visual composition owner for large parts of docking UI. |
| `MGDockSplitContainer` | C | Primarily structural, but still lives inside docking’s tightly coupled visual system. |
| `MGFloatingDockWindow` | C | Depends on docking visual conventions and window composition. |

## Practical Interpretation

### Good candidates for full lookless migration first

- `MGWindow`
- `MGOverlay`
- `MGListBox`
- `MGListView`
- `MGTextBox`
- `MGTabControl`
- `MGComboBox`
- `MGTreeView`

These controls already expose enough template seams that migration effort should mostly remove remaining visual assumptions rather than invent a template system from scratch.

### Controls that should wait until the runtime style/theme foundation is stronger

- `MGCheckBox`
- `MGContextMenuItem`
- `MGExpander`
- `MGSlider`
- `MGProgressBar`
- `MGRatingControl`
- `MGGridColorPicker`

These still embed too much state projection or symbol drawing in code.

### Docking area

Docking should be treated as a dedicated migration stream, not mixed into generic control templating work.

Reason:

- it has the highest density of direct draw logic;
- it uses custom icons, overlays, symbols, and docking-specific interaction rules;
- it will likely need its own visual part vocabulary and test plan.

## AI Agent Action List

### Status Legend

- `⬜` not started
- `🟡` in progress
- `✅` completed
- `⛔` blocked

### Execution Rules

- Commit after every task.
- Do not batch multiple checklist items into one commit.
- If a task uncovers prerequisite work, create a new checklist item instead of silently expanding scope.
- Each commit message should name the architectural step, not just the files changed.
- Run targeted validation before each commit.

### Commit Message Pattern

- `style-theme: define runtime precedence model`
- `style-theme: add hierarchical resource lookup`
- `style-theme: migrate window template attachment`

### Phase 0 - Baseline And Guardrails

- `✅ Establish architecture baseline`
  - Deliverable: short repo note confirming current precedence model, resource lookup model, theme refresh behavior, and template part rules.
  - Validation: targeted architecture tests and documentation consistency check.
  - Commit: `style-theme: document baseline architecture constraints`

- `✅ Freeze migration vocabulary`
  - Deliverable: canonical definitions for `style`, `theme`, `template`, `local value`, `inherited value`, `template value`, `theme default`.
  - Validation: terminology doc reviewed against existing tests/docs.
  - Commit: `style-theme: define migration vocabulary`

### Phase 1 - Runtime Value System

- `✅ Define value precedence matrix`
  - Deliverable: explicit precedence order covering local, explicit XAML, named style, implicit style, template, theme, inheritance, fallback.
  - Validation: add or update source-level precedence tests.
  - Commit: `style-theme: define runtime precedence matrix`

- `✅ Introduce unified resolved-value helpers`
  - Deliverable: central API for applying resolved values with invalidation metadata.
  - Validation: focused unit tests on precedence and invalidation source tracking.
  - Commit: `style-theme: add unified resolved value helpers`

- `✅ Move template-applied values onto the shared precedence path`
  - Deliverable: template defaults no longer behave like ad hoc local assignments.
  - Validation: targeted template precedence tests.
  - Commit: `style-theme: route template values through shared precedence`

### Phase 2 - Resource System

- `✅ Add hierarchical resource lookup`
  - Deliverable: subtree/local resource scopes resolve through parent scopes before desktop fallback.
  - Validation: resource lookup tests across nested scopes.
  - Commit: `style-theme: add hierarchical resource lookup`

- `✅ Add dynamic resource invalidation`
  - Deliverable: resource-dependent values re-resolve when a scoped resource changes.
  - Validation: focused runtime resource refresh tests.
  - Commit: `style-theme: support dynamic resource invalidation`

- `✅ Normalize theme lookup onto resource scopes`
  - Deliverable: theme resolution follows the same scope model as other resources.
  - Validation: subtree theme override tests.
  - Commit: `style-theme: align theme lookup with resource scopes`

### Phase 3 - Template Contract Stabilization

- `✅ Normalize required template part metadata`
  - Deliverable: every migrated composite control declares required and optional parts consistently.
  - Validation: architecture tests on required part declarations.
  - Commit: `style-theme: normalize template part contracts`

- `✅ Eliminate control-local visual literals from migrated templates`
  - Deliverable: colors, padding, border literals move out of control logic into template/theme defaults where possible.
  - Validation: source-level audit tests for migrated controls.
  - Progress: `MGTabControl.Background`, `MGComboBox.DropdownArrowColor`, `MGComboBox.Padding`, `MGComboBox.MinHeight`, `MGTextBox.Padding`, and `MGTextBox.MinHeight` now resolve through template defaults; `MGTreeView` no longer relies on theme-change reselection; `MGListBox` no longer overwrites template-owned items-panel chrome during construction or template attachment and now delegates default item-container chrome through the template catalog; `MGListView` grid-line brushes, spacing, grid-line margins, data-grid padding, and header-spacer chrome now resolve through the template catalog; `MGWindow` and `MGOverlay` no longer hardcode template-owned padding in constructors.
  - Verification note: the remaining literals found in this pass are `MGOverlayHost.Padding = new(4)`, `WindowStyle.None` zero-chrome behavior, and the fallback `MGListBox.ItemTemplate` content padding; these are phase 4 behavior/default-template follow-ups rather than migrated-template chrome injection.
  - Commit: `style-theme: remove local visual literals from migrated controls`

- `✅ Separate structural parts from visual-state defaults`
  - Deliverable: template structure creation and visual default application are independently testable.
  - Validation: targeted control template architecture tests.
  - Commit: `style-theme: separate structure from visual defaults`

### Phase 4 - Composite Control Migration

- `✅ Finish MGWindow migration`
  - Deliverable: window visuals are template/theme-owned, with behavior code limited to wiring and window policy.
  - Progress: constructor-level padding fallback was removed, `WindowStyle.Default` now restores current theme padding/border values instead of hard-coded literals, and `WindowStyle.None` now resolves its chromeless padding/border thickness through window theme settings instead of local zero-thickness literals.
  - Validation: targeted architecture tests and sample sanity check.
  - Commit: `6165f84 style-theme: finish window lookless migration`

- `✅ Finish MGOverlay migration`
  - Deliverable: overlay close button and border visuals are fully template-driven.
  - Progress: constructor-level padding fallback was removed so overlay chrome now comes from template/theme defaults instead of a local override, `MGOverlayHost` padding now comes from overlay theme settings instead of a hard-coded constructor literal, and the default close-button content now resolves through a shared catalog factory instead of an inline template-apply literal.
  - Validation: overlay-specific template part tests.
  - Commit: `style-theme: finish overlay lookless migration`

- `✅ Finish MGListBox migration`
  - Deliverable: title, outer border, inner border, and scrollviewer visuals are template-owned.
  - Progress: removed constructor/attach-time items-panel chrome overrides, synced virtualized panel chrome from template-owned panel state, delegated default item-container chrome through `MGControlTemplateCatalog`, moved title/items-panel alignment defaults out of `AttachControlTemplateStructure(...)` and into template defaults, and moved the default minimum height plus default item-content creation out of `MGListBox` constructor literals.
  - Validation: list box architecture tests.
  - Commit: `615803e style-theme: finish list box lookless migration`

- `✅ Finish MGListView migration`
  - Deliverable: header/data grid structure and defaults are template-owned with minimal visual logic left in control code.
  - Progress: header/data grid grid-line brushes, spacing, grid-line margins, data-grid padding, and header spacer chrome now come from template defaults instead of control-local attach-time assignments, and the default cell-content factory now lives in `MGControlTemplateCatalog` instead of `MGListView.LoadSettings(...)`.
  - Validation: list view template tests.
  - Commit: `style-theme: finish list view lookless migration`

- `✅ Finish MGTextBox and MGPasswordBox migration`
  - Deliverable: border, placeholder, character count, and resize grip orchestration are template-safe and precedence-correct.
  - Progress: selection colors, padding, and minimum height now resolve through template/theme defaults instead of constructor-local assignments, and placeholder / character-count / resize-grip template parts are now synchronized through dedicated control helpers instead of direct attach-time part writes.
  - Validation: textbox architecture tests and focused behavior checks.
  - Relevant commits: `076e2cb lookless: complete task 4 textbox and treeview lookless pass`, `1abcfaf style-theme: stabilize textbox template part sync`

- `✅ Finish MGTabControl migration`
  - Deliverable: header area, header wrappers, and selection-state defaults are template-driven without control-local visual fallbacks leaking.
  - Progress: background theme default now applies through `MGControlTemplateCatalog` instead of `OnThemeChanged(...)`, default header wrappers now update their applied control template in place instead of being recreated for every selection/template-name change, header-panel layout defaults are now delegated to the template catalog instead of being hard-coded inside `MGTabControl`, and the default string-header content factory now lives in `MGControlTemplateCatalog` instead of `MGTabControl`.
  - Validation: targeted tab control architecture tests.
  - Commit: `style-theme: finish tab control lookless migration`

- `✅ Finish MGComboBox migration`
  - Deliverable: dropdown arrow, dropdown window, item wrapper visuals, and header/footer defaults are template-owned.
  - Progress: dropdown arrow color, padding, minimum height, and arrow margin now resolve through template defaults instead of attach/theme-change or constructor-local control code, and the default dropdown-item / selected-item content factories plus dropdown item padding now live in `MGControlTemplateCatalog` instead of `MGComboBox` constructor or control-local defaults.
  - Validation: combo box architecture tests and sample behavior check.
  - Commit: `c3ce538 style-theme: finish combo box lookless migration`

- `✅ Finish MGTreeView migration`
  - Deliverable: remaining visual assumptions move out of tree view control code.
  - Progress: selection visual refresh now happens from selection property setters instead of theme-change deselect/reselect hacks, and the default indent fallback plus default tree-item header content creation now live in `MGControlTemplateCatalog` instead of `MGTreeView` / `MGTreeViewItem` literals.
  - Validation: tree view architecture tests.
  - Commit: `1a90abf style-theme: finish tree view lookless migration`

### Phase 5 - Manual State Rendering Reduction

- `✅ Inventory manual visual-state drawing`
  - Deliverable: list of controls using direct icon/state drawing with owner methods or draw callbacks.
  - Validation: source scan and doc update.
  - Commit: `style-theme: inventory manual state rendering hotspots`

  Current verified hotspots:

  - `MGCheckBox`: `ButtonElement.OnEndingDraw`, `DrawSelf`
  - `MGRadioButton`: `DrawSelf`
  - `MGExpander`: `ExpanderToggleButton.OnEndingDraw`
  - `MGContextMenuItem`: `HeaderPresenter.OnEndingDraw`
  - `MGDockAutoHideDrawer`: `DrawContents`
  - `MGDockAutoHideStrip`: `DrawContents`
  - `MGDockDropIndicators`: `DrawSelf`
  - `MGDockSplitterBar`: `DrawSelf`
  - `MGDockTabGroup`: `DrawContents`
  - `MGDockTabItem`: `DrawContents`
  - `MGDockPreviewOverlay`: `DrawSelf`

- `✅ Migrate checkbox and radio visuals to template/state mapping`
  - Deliverable: no hard-coded checkmark or bullet rendering in control logic except shared primitives.
  - Progress: checkbox and radio indicator drawing now goes through shared symbol elements backed by `UISymbolDrawing`, so `MGCheckBox` no longer draws via `ButtonElement.OnEndingDraw` and `MGRadioButton` no longer paints its bubble inline in `DrawSelf(...)`.
  - Validation: focused rendering and architecture tests.
  - Commit: `style-theme: migrate checkbox and radio visuals`

- `✅ Migrate context menu item symbols to template-owned visuals`
  - Deliverable: check/radio/submenu visuals are no longer manually painted in `MGContextMenuItem`.
  - Progress: submenu triangle rendering still uses the shared arrow primitive, and toggle/radio menu items now host shared symbol elements aligned to the header presenter instead of painting glyphs through `HeaderPresenter.OnEndingDraw`.
  - Validation: menu item template tests.
  - Commit: `style-theme: migrate context menu item symbol rendering`

- `✅ Migrate expander arrow visuals`
  - Deliverable: expander icon/state rendering is no longer hard-coded in control logic.
  - Progress: `MGExpander` now hosts a shared `MGTriangleArrowIcon` component whose bounds, direction, and color are synchronized from expander state and theme values instead of being painted through `ExpanderToggleButton.OnEndingDraw`.
  - Validation: expander-focused tests.
  - Commit: `style-theme: migrate expander state visuals`

### Phase 6 - Docking Stream

- `✅ Define docking visual-part vocabulary`
  - Deliverable: standardized parts and visual-state names for docking controls.
  - Progress: docking part names, semantic states, and per-control mappings are now defined in `Docs/docking-visual-part-vocabulary.md` so Phase 6 migrations can reuse a single naming scheme.
  - Validation: docking architecture note.
  - Commit: `style-theme: define docking visual part vocabulary`

- `✅ Migrate docking leaf controls first`
  - Deliverable: splitter, drop indicators, preview overlay, and auto-hide strip adopt consistent template/state boundaries.
  - Progress: `MGDockPreviewOverlay` now exposes explicit `PART_Surface` and `PART_Border` visuals backed by child elements instead of painting fill and border directly in `DrawSelf`.
  - Progress: `MGDockSplitterBar` now exposes `PART_Surface`, `PART_Accent`, and `PART_Grip`, with grip dots moved into a shared symbol element instead of a control-local `DrawSelf` path.
  - Progress: `MGDockAutoHideStrip` now uses a dedicated rotated text element for vertical strip labels and an explicit `PART_Separator` instead of drawing rotated text and the edge separator in `DrawContents`.
  - Progress: `MGDockDropIndicators` now delegates all center and host-edge zones to dedicated child indicator elements synchronized from docking state, replacing the monolithic custom-rendered `DrawSelf` path.
  - Validation: docking unit tests where available plus targeted manual verification.
  - Commit: `style-theme: migrate docking leaf visuals`

- `🟡 Migrate docking tab controls`
  - Deliverable: `MGDockTabItem` and `MGDockTabGroup` move icons and accents out of direct draw logic.
  - Progress: `MGDockTabItem` now exposes explicit surface, accent, close-icon, and pin-icon parts backed by shared docking symbol elements instead of drawing its accent and glyphs directly.
  - Progress: `MGDockTabGroup` now exposes explicit accent, overflow-icon, and window-state-icon parts instead of drawing the active-group stripe and compact-button glyphs in `DrawContents`.
  - Validation: docking tab behavior and rendering checks.
  - Commit: `style-theme: migrate docking tab visuals`

- `⬜ Migrate docking host/composite surfaces`
  - Deliverable: host-level overlays and drawers consume the new docking template vocabulary.
  - Validation: focused docking interaction pass.
  - Commit: `style-theme: migrate docking composite surfaces`

### Phase 7 - Final Hardening

- `🟡 Add runtime theme-switch regression tests`
  - Deliverable: tests covering control template refresh, theme value refresh, and subtree theme overrides.
  - Validation: targeted architecture and runtime tests.
  - Commit: `style-theme: add theme switch regression coverage`

- `✅ Add resource-scope regression tests`
  - Deliverable: tests for parent/child scope lookup, overrides, and invalidation.
  - Validation: bounded test run on resource/style suites.
  - Commit: `style-theme: add resource scope regression coverage`

- `✅ Add per-control migration matrix`
  - Deliverable: updated doc showing every migrated control, remaining blockers, and residual coupling.
  - Validation: docs reviewed against code.
  - Commit: `style-theme: publish migration status matrix`

## Recommended Execution Order

1. Finish Phase 3 cleanup work
2. Phase 4
3. Phase 5
4. Phase 6
5. Phase 7

Phases 1 and 2 are already materially in place, and Phase 3 has only targeted cleanup remaining.

## Stop Conditions For The Agent

The agent must stop and ask for review if any task causes one of these conditions:

- precedence rules become ambiguous;
- a control requires new public API to continue;
- template parts cannot express the needed visual behavior without inventing an unreviewed pattern;
- runtime theme refresh introduces broad sample regressions;
- docking changes require visual design decisions rather than technical migration only.