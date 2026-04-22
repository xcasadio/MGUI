# Jalon de convergence lookless v1

## Objectif

Ce document fige le premier jalon commun des chantiers `Style`, `ThemeDefinition` et `ControlTemplate` pour la roadmap portefeuille InkkSlinger vs MGUI.

La cible n'est pas de rouvrir toute la migration lookless deja documentee. La cible est de borner le sous-ensemble qui doit etre considere comme le premier jalon portefeuille livrable avant les chantiers plus lourds ou plus couplants, en particulier le docking hybride et les controles a fenetres auxiliaires.

Ce document sert de reference commune pour:

- `Docs/inkkslinger-vs-mgui-prioritized-roadmap.md` lot 2 ;
- `Docs/inkkslinger-vs-mgui-roadmap-tasks.md` taches 4 et 5 ;
- `Docs/style-theme-refactor-tasks.md` ;
- `Docs/theme-definition-tasks.md` ;
- `Docs/control-template-tasks.md`.

## Invariants du jalon

Le jalon v1 est considere atteint quand les invariants suivants sont vrais ensemble:

- la resolution runtime d'une valeur visible suit un chemin explicable entre local, style, template, theme, heritage, ressource et fallback ;
- les changements de theme, template ou ressource reevaluent un sous-arbre sans reparse complet et sans rebuild structurel inutile ;
- les `TemplateParts` et presenters critiques sont standardises et validables ;
- les themes declaratifs XAML et les templates structurels convergent vers le meme runtime de resolution, sans filiere parallele opaque ;
- les diagnostics et tests existants peuvent expliquer quelle valeur gagne et quel template ou theme est applique sur les controles pilotes.

## Perimetre retenu

Le premier jalon portefeuille couvre les briques suivantes:

- precedence des valeurs, lookup de ressources hierarchique, `StaticResource`, `DynamicResource` et invalidation runtime ;
- `ThemeDefinition` en XAML, `BasedOn`, conversion vers `MGTheme` et enregistrement dans `MGResources` ;
- `ControlTemplate` structurels, `TemplateParts`, instanciation runtime, bridge precedence/invalidation et diagnostics de template ;
- projection des visual states et conventions de presenters reutilisees par les controles composites pilotes ;
- guide de migration et sample lookless permettant de valider un vrai theme switch et un vrai changement de template.

## Controles pilotes du jalon

Les controles pilotes retenus pour ce premier jalon commun sont:

- `MGWindow` et `MGOverlay` pour valider le chrome de base, la thematisation structurelle et les parts critiques ;
- `MGListBox` et `MGListView` pour valider la convergence ressources + templates + presenters sur des controles d'outil deja importants ;
- `MGComboBox` et `MGTabControl` pour valider les controles hybrides a chrome plus riche sans ouvrir tout le docking ;
- `MGContextMenuItem` reste couvert par la convergence style/theme, mais n'est pas un gate supplementaire du jalon si les controles ci-dessus restent verts.

## Cartographie vers les backlogs specialises

La contribution attendue de chaque backlog specialise est la suivante:

- `style-theme-refactor-tasks.md`: le jalon commun s'appuie sur les taches de fondation et de migration jusqu'aux controles pilotes. Le docking et les extensions plus larges deja documentes ne sont pas des prerequis supplementaires pour declarer le jalon portefeuille vert ;
- `theme-definition-tasks.md`: toute la chaine declarative de `ThemeDefinition` jusqu'aux themes XAML integres dans `MGResources` fait partie du jalon commun ;
- `control-template-tasks.md`: le jalon commun couvre les migrations structurelles et leur stabilisation pour `Window`, `Overlay`, `ListBox`, `ListView`, `ComboBox` et `TabControl`, mais exclut encore les vagues docking hybrides et les controles a fenetres auxiliaires.

## Hors jalon v1

Les sujets suivants restent explicitement hors du premier jalon portefeuille:

- migrations docking encore hybrides et paths theming specifiques au docking ;
- controles composites qui pilotent des overlays, popups ou fenetres auxiliaires supplementaires ;
- extension systematique a tous les controles composites restants tant que les pilotes ne servent pas deja de base stable ;
- ambitions de type property system WPF complet, triggers generiques ou moteur de bureau lourd.

## Validation ciblee

Le jalon portefeuille s'appuie sur une validation bornee, deja coherente avec la roadmap:

- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template"`

Le sample de reference principal pour la convergence lookless reste `MGUI.Samples/Features/StyleThemeRefactor.xaml`.

## Validation portefeuille v1

Le lot portefeuille 5 a ete revalide sur cette base avec les ajustements locaux suivants:

- les `MGContextMenu` donnent leur focus initial via `KeyboardFocusSource.Pointer`, ce qui evite de faire scroller des viewports ancetres quand un menu flottant s'ouvre ;
- la projection d'etat visuel des `MGContextMenuItem` conserve la mise en evidence venant du focus, du wrapper, de la selection et des sous-menus ouverts ;
- les bordures internes XAML de `StackPanel`, `WrapPanel` et `Canvas` n'heritent plus des implicit styles parents, ce qui ferme la derniere fuite de style dans la tranche lookless ;
- validation executee avec succes:
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template"`
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`