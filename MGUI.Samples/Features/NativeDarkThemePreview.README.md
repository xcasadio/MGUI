# NativeDarkThemePreview

Objective: preview the native `Dark` built-in theme and the embedded `Dark.*` control templates from `MGUI.Core` inside `MGUI.Samples`.

Launch:
- run `MGUI.Samples`
- open `Compendium`
- in `Features`, toggle `Native Dark Preview`

What it validates:
- built-in theme creation via `new MGTheme(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily)`
- built-in control template registration for `Dark.*`
- representative controls: `ComboBox`, `ListBox`, `ListView`, `TreeView`, `TabControl`, `ContextMenu`, `Overlay`, `ToolTip`

Note: this preview no longer depends on any asset file under `CasaEngine.Editor/Content/UI`.