# EditorDarkThemePreview

Objective: preview the real `CasaEditor.Dark` theme and control-template assets from `CasaEngine.Editor` inside `MGUI.Samples`.

Launch:
- run `MGUI.Samples`
- open `Compendium`
- in `Features`, toggle `Editor Theme Preview`

What it validates:
- theme file loading from `CasaEngine.Editor/Content/UI/Themes/CasaEditor.Dark.Theme.xaml`
- control template loading from `CasaEngine.Editor/Content/UI/Templates/CasaEditor.Dark.ControlTemplates.xaml`
- representative controls: `ComboBox`, `ListBox`, `ListView`, `TreeView`, `TabControl`, `ContextMenu`, `Overlay`, `ToolTip`

Note: if the sample is run outside the CasaEngineMonogame workspace, it falls back to a warning state instead of failing startup.