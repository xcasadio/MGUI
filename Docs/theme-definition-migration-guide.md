# Guide de migration ThemeDefinition

## Objectif

`ThemeDefinition` permet de definir un theme MGUI en XAML, puis de le convertir en `MGTheme` au runtime sans exposer directement `MGTheme` au parser XAML.

Le pipeline vise est le suivant:

`ThemeDefinition (XAML) -> ThemeDefinitionLoader -> ThemeDefinitionBuilder -> MGTheme -> MGResources.Themes`

## Ce qui change

- Un theme peut maintenant etre declare dans un fichier XAML dedie.
- Un theme peut heriter d'un autre via `BasedOn`.
- `MGResources` peut charger un ou plusieurs themes depuis XAML et les enregistrer automatiquement.
- `ThemeName` continue de fonctionner sur les fenetres et resout les themes via `MGResources.Themes`.
- Les built-in conserves `Dark_Blue` et `Light_Gray` sont maintenant declares dans [MGUI.Core/UI/Themes/BuiltInThemes.xaml](MGUI.Core/UI/Themes/BuiltInThemes.xaml).

## Format supporte

Deux racines XAML sont supportees:

1. `ThemeDefinition` pour un theme unique.
2. `ThemeDefinitionsDocument` pour plusieurs themes nommes dans un meme document.

## Exemple minimal

```xaml
<ThemeDefinition xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                 Name="MyTheme"
                 BasedOn="Dark_Blue">
  <ThemeDefinition.FontSettings>
    <ThemeFontSettingsDefinition DefaultFontSize="15" />
  </ThemeDefinition.FontSettings>

  <ThemeDefinition.Properties>
    <ThemePropertyDefinition Target="DropdownArrowColor" Color="Black" />
  </ThemeDefinition.Properties>
</ThemeDefinition>
```

## Exemple multi-themes

```xaml
<ThemeDefinitionsDocument xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core">
  <ThemeDefinition Name="BaseDark" BasedOn="Dark_Blue" />

  <ThemeDefinition Name="DebugTheme" BasedOn="BaseDark">
    <ThemeDefinition.Window>
      <ThemeWindowSettingsDefinition TitleBarMinHeight="28" />
    </ThemeDefinition.Window>

    <ThemeDefinition.Properties>
      <ThemePropertyDefinition Target="ToolTipOffset">
        <ThemePropertyDefinition.Point>
          <ThemePointDefinition X="12" Y="18" />
        </ThemePropertyDefinition.Point>
      </ThemePropertyDefinition>
    </ThemeDefinition.Properties>
  </ThemeDefinition>
</ThemeDefinitionsDocument>
```

## Chargement runtime

Depuis du code, le point d'entree principal est `MGResources.LoadThemesFromXaml(...)`.

```csharp
string markup = File.ReadAllText(themePath);
desktop.Resources.LoadThemesFromXaml(XamlDocumentSource.FromString(markup, themePath));
```

Le theme est ensuite resolu normalement par nom:

```csharp
window.Theme = desktop.Resources.GetThemeOrDefault("MyTheme");
```

ou en XAML:

```xaml
<Window ThemeName="MyTheme" />
```

## Resolution `BasedOn`

`BasedOn` est resolu dans cet ordre logique:

1. themes deja enregistres dans le document courant ;
2. themes deja presents dans `MGResources.Themes` ;
3. themes built-in supportes.

Les cycles sont rejetes explicitement.

## Structure recommandee

Utiliser en priorite:

- les groupes dedies quand ils existent: `Window`, `Overlay`, `ContextMenu`, `ListBox`, `Docking`, etc. ;
- `Properties` pour les valeurs top-level restantes ;
- `Backgrounds` pour les backgrounds cibles par `MGElementType`.

## Migration depuis un theme code en C#

Avant:

```csharp
MGTheme theme = new(MGTheme.BuiltInTheme.Dark_Blue, desktop.Theme.FontSettings.DefaultFontFamily);
theme.FontSettings.DefaultFontSize = 15;
theme.DropdownArrowColor = Color.Black;
desktop.Resources.AddTheme("MyTheme", theme);
```

Apres:

```xaml
<ThemeDefinition xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                 Name="MyTheme"
                 BasedOn="Dark_Blue">
  <ThemeDefinition.FontSettings>
    <ThemeFontSettingsDefinition DefaultFontSize="15" />
  </ThemeDefinition.FontSettings>
  <ThemeDefinition.Properties>
    <ThemePropertyDefinition Target="DropdownArrowColor" Color="Black" />
  </ThemeDefinition.Properties>
</ThemeDefinition>
```

Puis:

```csharp
desktop.Resources.LoadThemesFromXaml(XamlDocumentSource.FromFile(themeFilePath));
```

## Built-in conserves

Le perimetre built-in supporte est maintenant limite a:

- `Dark_Blue`
- `Light_Gray`

Les autres variantes built-in ont ete retirees du contrat public.

## Recommandations

- Utiliser `BasedOn` pour exprimer des variations locales plutot que dupliquer un theme entier.
- Garder les variations de palette et de chrome dans les themes, pas dans les controles.
- Utiliser les groupes nommes quand une propriete appartient clairement a une famille de controle.
- Reserver `Properties` aux valeurs top-level qui n'ont pas encore de groupe plus clair.

## Validation

La couverture minimale a verifier apres ajout d'un theme est:

1. le parsing XAML ;
2. la resolution `BasedOn` ;
3. le chargement dans `MGResources` ;
4. l'application via `ThemeName` ou `GetThemeOrDefault`.