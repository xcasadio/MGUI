# ADR-0017: Reserve the TreeView expander column at a fixed width on every item

- **Status**: Accepted
- **Date**: 2026-09-24
- **Source**: discussion with the author on 2026-09-24

## Context

In `MGTreeView`/`MGTreeViewItem`, the expand/collapse triangle of an item with children is drawn in a dedicated
`TreeViewExpanderToggleButton` column (`MGUI.Core/UI/MGTreeViewItem.cs`), laid out left of the header text inside an
`MGDockPanel`: `IndentationBorder` (width `Level * IndentSize`) | `ExpanderButton` | `HeaderContainer`, all `Dock.Left`.

Before this decision, a leaf item (no children) set `ExpanderButton.Visibility = Visibility.Collapsed`, which removes
the button from layout entirely (0 px), while an item with children kept it `Visible` at `MinWidth = 10` plus a
`(0,0,4,0)` right margin (14 px total). Two siblings at the same hierarchy level therefore had their header text start
at two different X positions depending only on whether one of them had children, which reads as a level difference
that does not exist.

`Visibility.Hidden` already exists (`MGUI.Core/UI/Enums.cs`) and, unlike `Collapsed`, reserves the element's measured
space in layout while excluding it from drawing and (outside `CanHandleInputsWhileHidden`) from hit-testing
(`MGUI.Core/UI/MGElement.cs`). `MGTheme.TreeViewExpanderButtonSize` already existed as a theme field but no control
read it. `MGTreeView.IndentSize` (`MGUI.Core/UI/MGTreeView.cs`) already provided the pattern to copy: a public `int`
property, a matching `MGControlTemplateCatalog.DefaultTreeViewIndentSize` constant, an `ApplyOwnerThemeDefault` theme
default, and propagation to every item through `RegisterItemRecursive`.

## Decision

- `MGTreeView` gets a new public `ExpanderButtonSize` property (pixels), modeled on `IndentSize`: backed by
  `MGControlTemplateCatalog.DefaultTreeViewExpanderButtonSize = 16`, its setter re-registers every item so the new
  width reaches them, and it is settable from XAML (`TreeView.ExpanderButtonSize`, `MGUI.Core/UI/XAML/Controls.cs`).
- The theme default (`MGControlTemplateCatalog.ApplyTreeViewTemplate`) feeds `TreeView.ExpanderButtonSize` from
  `MGTheme.TreeViewExpanderButtonSize` when that value is greater than 0, otherwise from the constant. A value of 0
  or less, whether it comes from an empty/unset theme (`MGTheme.CreateEmpty` yields 0) or from an explicit
  `ExpanderButtonSize = 0` set on the tree, never collapses the column to 0 px: `MGTreeViewItem` applies the same
  "greater than 0, else the constant" rule when it reads the effective value for its own column
  (`MGTreeViewItem.UpdateExpanderButtonWidth`).
- `MGTreeViewItem`'s expander column now has an exact width (`ExpanderButton.PreferredWidth`, the same mechanism
  `IndentationBorder` already used for the indentation column) instead of a `MinWidth`/margin combination: the
  previous `MinWidth = 10` local write is removed, `MGToggleButton`'s own constructor default (`MinWidth = 16`) is
  explicitly reset to 0 so it cannot clamp a configured size below 16, and the 4 px right margin becomes `Thickness(0)`.
  The row height is unchanged (still driven by the header text and the existing `MinHeight = 10`).
- A leaf sets `ExpanderButton.Visibility = Visibility.Hidden` instead of `Collapsed`, in both the constructor and
  `UpdateExpanderVisibility`, so its column keeps reserving the configured width without drawing a triangle or
  intercepting clicks meant for the rest of the header. The width itself follows the same computation whether the
  item currently has children or not, so it does not need to change when the item's first child is added or its
  last child removed — only the `Visibility` does.
- `MGTreeViewItem.OnHeaderPanelClick`/`OnHeaderPanelDoubleClick` only treat a click as "on the expander" when
  `ExpanderButton.Visibility == Visibility.Visible` (in addition to the existing bounds check): a click anywhere in a
  leaf's reserved column now selects the item and focuses the tree, the same as a click on the rest of its header row;
  a click on an expandable item's triangle keeps toggling its expansion without selecting it.
- Applies to every `MGTreeView`, including `MGUI.Editor`'s tree pane, since they all go through the same control
  template and item implementation.

## Consequences

- Built-in themes: `Dark_Blue` and `Light_Gray` both set `TreeViewExpanderButtonSize = 16`, so an expandable item's
  column becomes 16 px (was 14 px: `MinWidth 10` + `4` px margin) — a 2 px widening for those items — while a leaf's
  column grows from 0 to 16 px, shifting its header text right by one full column. `Dark` sets `14`: an expandable
  item's column keeps its previous 14 px width, but a leaf's column still grows from 0 to 14 px, so leaves shift right
  under `Dark` too. `MGUI.Samples/Features/EditorCompact.Themes.xaml` (`BasedOn="Dark"`) does not override the value
  and inherits 14, as does the `MGUI.Editor` host, which uses `Dark`: their leaves shift right by 14 px. An empty theme
  (`MGTheme.CreateEmpty`) falls back to the 16 px constant instead of 0.
- `UIThemeValueInvalidation`'s `TreeViewExpanderButtonSize` entry is now read by `MGControlTemplateCatalog`'s TreeView
  template default, so its "not read by any control yet" comment no longer applies.
- The sample at `MGUI.Samples/Controls/TreeView.xaml` gains a fourth, childless root item (`Root Item 4`) so a
  level-0 leaf's alignment against its siblings can be checked by hand alongside the existing nested cases.
