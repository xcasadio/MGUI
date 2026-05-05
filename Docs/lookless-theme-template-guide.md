# Lookless Theme Template Guide

This guide documents the final template resolution model introduced by the lookless migration.

## Resolution Order

`MGElement` now resolves control templates in this order:

1. explicit `ControlTemplate`
2. explicit local or style-applied `ControlTemplateName`
3. active theme mapping for the control `MGElementType`
4. control fallback `DefaultControlTemplateName`

This keeps local overrides authoritative while allowing a theme to choose a structural skin for a control family.

## Declaring Template Mappings In A Theme

`ThemeDefinition` supports a `ControlTemplates` section:

```xaml
<ThemeDefinition xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                 Name="DarkSkin"
                 BasedOn="Dark">
  <ThemeDefinition.ControlTemplates>
    <ThemeControlTemplateDefinition ElementType="ListView"
                                    TemplateName="ListView.HeadersBottom" />
    <ThemeControlTemplateDefinition ControlTypeName="MGDockTabItem"
                                    TemplateName="DockTabItem.Minimal" />
    <ThemeControlTemplateDefinition ElementType="ComboBox"
                                    TemplateName="ComboBox.Default" />
  </ThemeDefinition.ControlTemplates>
</ThemeDefinition>
```

`ElementType` remains the broad mapping key for the built-in generic controls already exposed through XAML.

`ControlTypeName` allows a theme to target a specific runtime control type when multiple controls share the same `MGElementType`, such as docking controls that currently use `MGElementType.Custom`.

## Registering Templates

Theme mappings only select named templates that already exist in `MGResources.ControlTemplates`. Those templates can come from:

- the built-in `MGControlTemplateCatalog`
- XAML loaded through `LoadControlTemplatesFromXaml(...)`
- code-created `MGControlTemplate` instances added to resources

## Runtime Behavior

- A theme change triggers `ApplyControlTemplate(true)` on affected elements.
- If the resolved template name changes, the structured template is rebuilt.
- If the resolved template stays the same, the normal theme refresh path re-applies chrome without rebuilding the structure.

## Sample

See `MGUI.Samples/Features/StyleThemeRefactor.xaml` and `MGUI.Samples/Features/StyleThemeRefactor.xaml.cs`.

That sample registers a custom `ListView.HeadersBottom` template and two themes:

- `BlueprintSkin` keeps `ListView.Default`
- `DarkSkin` maps `ListView` to `ListView.HeadersBottom`

Switching themes keeps the same `ListView` behavior and data while moving the header row from the top to the bottom.