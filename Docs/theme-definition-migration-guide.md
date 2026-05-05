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
- Les built-in supportes `Dark`, `Dark_Blue` et `Light_Gray` sont maintenant declares dans [MGUI.Core/UI/Themes/BuiltInThemes.xaml](MGUI.Core/UI/Themes/BuiltInThemes.xaml).

## Format supporte

Deux racines XAML sont supportees:

1. `ThemeDefinition` pour un theme unique.
2. `ThemeDefinitionsDocument` pour plusieurs themes nommes dans un meme document.

## Exemple minimal

```xaml
<ThemeDefinition xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                 Name="MyTheme"
                 BasedOn="Dark">
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
  <ThemeDefinition Name="BaseDark" BasedOn="Dark" />

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

## Matrice de couverture XAML

### 100% pilotable en XAML aujourd'hui

- declaration d'un ou plusieurs themes via `ThemeDefinition` ou `ThemeDefinitionsDocument` ;
- inheritance de theme via `BasedOn` ;
- `FontSettings` ;
- `Backgrounds` par `MGElementType` ;
- mappings de templates via `ThemeDefinition.ControlTemplates` ;
- groupes exposes par `ThemeDefinition` quand ils existent deja dans le contrat runtime:
  - `Window`
  - `Overlay`
  - `ContextMenu`
  - `ContextMenuItem`
  - `ListBox`
  - `ListView`
  - `ComboBox`
  - `TreeViewTemplate`
  - `TabControl`
  - `Docking`
- proprietes top-level explicitement exposees par `ThemePropertyTarget`.

### Partiellement pilotable en XAML

- le look final des controles classes `B` ou `C` dans l'inventaire de migration ;
- les styles XAML a base de `Setter` sur des proprietes publiques ;
- les `ControlTemplate` XAML pour les controles deja migres vers le modele a parts/templates ;
- les variations structurelles qui passent par un root de template et des `TemplatePart` explicites.

### Pas encore completement pilotable en XAML

- les proprietes de `MGTheme` qui ne sont pas exposees par `ThemeDefinition` ou `ThemePropertyTarget` ;
- les comportements visuels encore imperatifs dans les controles classes `C` ou `D` ;
- une skin 100% XAML de toute la bibliotheque sans appui sur le code C# existant ;
- un systeme de styles de niveau WPF complet avec triggers generiques et visual states declaratifs globaux.

### Regle pratique

- si une valeur existe dans un groupe de `ThemeDefinition`, dans `Backgrounds`, dans `ControlTemplates`, ou dans `ThemePropertyTarget`, elle est pilotable en XAML ;
- sinon il faut encore etendre le contrat C# avant de pouvoir la definir en XAML.

Voir aussi l'exemple enrichi dans `MGUI.Samples/Features/StyleThemeRefactor.xaml` et `MGUI.Samples/Features/StyleThemeRefactor.xaml.cs`.

## Migration depuis un theme code en C#

Avant:

```csharp
MGTheme darkTheme = new(MGTheme.BuiltInTheme.Dark, desktop.DefaultFontFamily);
desktop.Resources.DefaultTheme = darkTheme;

MGTheme theme = darkTheme.Copy();
theme.FontSettings.DefaultFontSize = 15;
theme.DropdownArrowColor = Color.Black;
desktop.Resources.AddTheme("MyTheme", theme);
```

Apres:

```xaml
<ThemeDefinition xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"
                 Name="MyTheme"
                 BasedOn="Dark">
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

## Built-in supportes

Le perimetre built-in supporte est maintenant:

- `Dark`
- `Dark_Blue`
- `Light_Gray`

`Dark` est le theme sombre natif recommande pour les nouveaux usages.

`Dark_Blue` reste disponible pour compatibilite. Dans `MGUI.Samples`, il peut etre presente sous le label `Blueprint` pour distinguer le theme historique du nouveau `Dark`.

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