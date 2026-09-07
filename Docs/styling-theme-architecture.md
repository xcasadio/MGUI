# Architecture styling / theme

## Objectif

Decrire l'etat actuel du systeme de styles, themes, templates de controles et ressources de MGUI: vocabulaire, contrats d'API, invariants, flux runtime et limites connues.

## Portee

Couvre `MGUI.Core/UI/Styling/*`, `MGUI.Core/UI/MGResources.cs`, `MGUI.Core/UI/MGTheme.cs`, la partie style/theme/template de `MGUI.Core/UI/MGElement.cs`, les definitions XAML (`MGUI.Core/UI/XAML/Themes.cs`, `ThemeDefinitionLoader.cs`, `ThemeDefinitionBuilder.cs`), les assets embarques (`MGUI.Core/UI/Themes/BuiltInThemes.xaml`, `MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml`) et l'outillage (`MGUI.Core/Tooling/UIToolingService.cs`).

## Principes directeurs

- MGUI est un framework UI pour moteur de jeu (MonoGame), pas une replique de WPF. Toute decision style/theme se juge contre le cout temps reel, l'input manette et les contraintes du renderer MonoGame.
- Jamais de dependency property system complet a la WPF. Si un moteur de valeurs resolues est etendu, il reste borne aux proprietes exposees au theme/style/template et aux proprietes layout-affecting.
- Le renderer reste neutre: brushes, geometrie et draw calls uniquement. Les decisions de skin se resolvent au-dessus de lui.
- `MGResources` est la facade unique d'agregation des themes, styles, ressources, textures et templates.
- `VisualStateFillBrush`, `VisualStateSetting<T>` et la famille `MGContentPresenter` sont les primitives sanctionnees de projection d'etat et de composition.
- Une re-application de theme ne reconstruit pas la structure si seul le chrome change. La recreation de structure n'est jamais un chemin chaud de frame.

## Vue d'ensemble

Le pipeline repose sur cinq couches:

- Ressources: `MGResources` expose des scopes hierarchiques avec fallback parent (`TryGetTheme`, `TryGetStyle`, `TryGetStaticResource`, `TryGetControlTemplate`, `TryGetElementTemplate`).
- Elements: `MGElement` resout ses ressources via `GetResources()`, peut creer un scope local avec `EnsureResourceScope(...)` et propage les changements de theme via `NotifyThemeChanged(...)`.
- Styles XAML: `MGUI.Core/UI/XAML/Element.cs` (`ProcessStyles`) applique styles implicites et explicites pendant le parsing, avec protection des valeurs explicitement posees dans le XAML.
- Templates: `MGControlTemplate` (phases structure / attachement / defaults) et `MGControlTemplateCatalog`; `MGElementTemplate` et `XAML/Templates.cs` couvrent le templating de contenu.
- Etats visuels: `VisualState`, `MGVisualStateProjection`, calcul des etats dans `MGElement`.

## Vocabulaire

- Style: collection de setters nommee ou implicite appliquee a un element, sans jamais definir sa structure visuelle. Styles implicites cles par `MGElementType`; styles explicites resolus via `MGResources.Styles`.
- Theme: source semantique des valeurs par defaut d'une famille de controles. Plus faible que valeur locale, binding, visual state, template et style. Circule par les scopes de ressources et supporte le switch runtime.
- Control template: structure visuelle et chrome par defaut d'un controle. Cree des parts nommees, les attache a l'instance vivante, applique des defaults template-owned. Jamais un synonyme de style.
- Template part: element nomme expose par un template pour que le code de comportement du controle s'y branche. Declare via `GetRequiredControlTemplateParts()`.
- Template value: valeur appliquee par un template via le chemin de precedence partage. Pas une affectation locale: au-dessus des styles, en-dessous du visual state et du local.
- Valeur locale / binding local: valeur posee directement sur l'instance (code ou XAML); un binding actif se comporte comme une source locale.
- Valeur heritee: valeur RESOLUE de l'ancetre le plus proche pour une propriete heritable, uniquement quand aucune source plus forte n'existe. Mecanisme de fallback, jamais un pair de l'affectation locale, jamais un lookup de theme.
- Dynamic resource: source indirecte resolue via `MGResources` et reevaluee quand la ressource scopee change. Au-dessus du theme, en-dessous des styles.
- Resolved value: gagnant runtime pour une propriete, represente par valeur + metadonnees de source + metadonnees d'invalidation (`UIResolvedValue<T>`, `UIValueResolutionSource`).
- Resource scope: noeud `MGResources` de la chaine de lookup. Categories `UIResourceScope`: `Desktop`, `Window`, `Subtree`, `Template`.

## MGResources: facade unique de ressources

- Les instances `MGResources` forment une chaine parent. Un scope enfant resout textures, commands, themes, styles, static resources, element templates et control templates via cette chaine.
- `MGElement.EnsureResourceScope(...)` materialise les scopes locaux a la demande et maintient leur lien parent.
- Les overrides de theme au niveau fenetre sont des overrides de scope de ressources, pas une mutation du desktop global.
- APIs de compatibilite stables: `MGResources.AddTheme(string, MGTheme)`, `GetThemeOrDefault`, `Window.Theme`, `ThemeName`.
- Chargement declaratif: `MGResources.LoadThemesFromXaml(XamlDocumentSource...)` et `MGResources.LoadControlTemplatesFromXaml(XamlDocumentSource...)` (MGUI.Core/UI/MGResources.cs).

## Precedence des valeurs

### Pile officielle

`UIValuePrecedence` (MGUI.Core/UI/Styling/UIValuePrecedence.cs), du plus fort au plus faible:

1. `Animation` (100)
2. `LocalValue` (90)
3. `LocalBinding` (80)
4. `VisualState` (70)
5. `Template` (60)
6. `ExplicitStyle` (50)
7. `ImplicitStyle` (40)
8. `DynamicResource` (30)
9. `Theme` (20)
10. `Inherited` (10)
11. `DefaultValue` (0)

### Ou vit la precedence reelle

Point structurant: `UIValuePrecedence` est un modele formalise et un contrat de tests (`MGUI.Tests/Architecture/StyleValueResolutionModelTests.cs`), PAS un moteur runtime unifie. La precedence effective reste dispersee entre quatre chemins:

- les styles XAML, appliques uniquement au parsing via `Element.ProcessStyles(...)`;
- les callbacks `OnThemeChanged(...)` des controles;
- les setters C# ordinaires (valeurs locales et defaults constructeur);
- le chemin template `MGControlTemplateContext.ApplyTemplateValue(...)` / `ApplyThemeDefault(...)`, seul chemin qui stampe reellement un `UIResolvedValue<T>` avec source et invalidation.

Toute evolution doit projeter ses valeurs dans cette pile officielle, jamais via un ordre ad hoc par controle.

### Arbitrages

- Un template ne doit jamais ecraser une valeur locale du controle hote; il peut poser des defaults sur ses propres parts, qui recoivent leur propre pile de precedence.
- Une propriete alimentee par binding se comporte comme locale; un style ne doit pas l'ecraser.
- Une animation gagne pendant sa duree puis la valeur revient a la meilleure source non animee.
- Une dynamic resource resolue plus pres de l'instance gagne sur le theme.

### Heritage

- Heritables par defaut: foreground texte par defaut, famille de police par defaut (taille de police si valide pour le layout texte).
- Non heritables: contraintes de layout, fonds/bordures/chrome, templates, commandes et comportement, etats interactifs, geometries de skin.
- L'heritage se resout depuis la valeur RESOLUE de l'ancetre, et seulement quand aucune source plus forte n'existe sur l'element courant.

### Invalidation

`UIInvalidationKind` (flags): `Draw`, `Measure`, `Arrange`, `Structure`, `Input`, `Navigation`.

Matrice de recommandation:

- taille de layout ou police: `Measure + Arrange + Draw`
- alignement / ancre: `Arrange + Draw`
- brush ou couleur pure: `Draw`
- template: `Structure + Measure + Arrange + Draw + Input`
- visual state: `Draw`, ou `Measure + Arrange + Draw` si le state modifie une geometrie
- dynamic resource: re-resoudre, puis invalider selon la classe de la propriete cible

### Anti-patterns interdits

- Copier une valeur de theme dans un champ local sans chemin de reevaluation.
- Faire choisir une couleur de theme par le renderer.
- Gerer la precedence de maniere ad hoc par controle.
- Melanger comportement et setters de style dans la meme abstraction.
- Utiliser un template pour imposer un comportement qui appartient a la logique de controle.

## Themes

### MGTheme et built-ins

- `MGTheme` est un objet runtime, jamais un contrat XAML direct. Il reste la source centrale monolithique des tokens par defaut built-in (pas de couche de tokens semantiques).
- Built-ins (`MGTheme.BuiltInTheme`): `Dark`, `Dark_Blue`, `Light_Gray`, declares dans `MGUI.Core/UI/Themes/BuiltInThemes.xaml`. `Dark` est le theme sombre recommande; `Dark_Blue` reste pour compatibilite (presente comme `Blueprint` dans `MGUI.Samples`).

### Contrat de refresh de theme

- `MGResources.DefaultTheme` retombe sur `Parent.DefaultTheme` sans override local.
- `MGResources.OnDefaultThemeChanged` propage les changements de theme parent vers les scopes descendants, sauf si un override local bloque l'heritage.
- `MGElement.NotifyThemeChanged(...)` appelle `OnThemeChanged(...)`, re-applique le template via `ApplyControlTemplate(true)`, puis recurse sur les enfants et composants sans scope local. Cette descente parcourt tout le sous-arbre: le fan-out est significatif sur une UI dense.

### Invalidation liee au theme

- Par defaut, `MGElement.GetThemeInvalidation(...)` renvoie `Draw`. Seul `MGTextBlock` surcharge cette logique (demande `Measure|Arrange|Draw` quand la police effective depend du theme).
- Consequence: toute nouvelle propriete themee qui affecte la taille doit opter manuellement pour une invalidation layout. C'est un choix assume (pipeline leger) mais fragile sans discipline.

## ThemeDefinition: themes declaratifs XAML

### Pipeline

`ThemeDefinition (XAML) -> ThemeDefinitionLoader -> ThemeDefinitionBuilder -> MGTheme -> MGResources.Themes`

Le parser XAML ne construit jamais un `MGTheme` directement: il produit une definition declarative simple, convertie ensuite par un builder testable sans parsing.

### Format

Deux racines XAML: `ThemeDefinition` (theme unique) et `ThemeDefinitionsDocument` (plusieurs themes nommes).

`ThemeDefinition` (MGUI.Core/UI/XAML/Themes.cs) combine:

- des groupes dedies pour les familles durables: `FontSettings`, `Window`, `Overlay`, `ContextMenu`, `ContextMenuItem`, `ListBox`, `ListView`, `PropertyGrid`, `ComboBox`, `TreeViewTemplate`, `TabControl`, `Graph`, `Docking`;
- `Backgrounds`: overrides de background par `MGElementType`;
- `ControlTemplates`: mappings de templates par type (voir plus bas);
- `Properties`: collection `ThemePropertyDefinition` (enum `ThemePropertyTarget`) pour les valeurs top-level restantes, afin d'eviter une explosion de types miroirs de `MGTheme`.

Les visual states passent par des types de definition dedies (`ThemeVisualState*Definition`), jamais par `VisualStateFillBrush` directement.

Exemple minimal:

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

Chargement et application:

```csharp
desktop.Resources.LoadThemesFromXaml(XamlDocumentSource.FromFile(themePath));
window.Theme = desktop.Resources.GetThemeOrDefault("MyTheme");
// ou en XAML: <Window ThemeName="MyTheme" />
```

### BasedOn

- Resolution dans l'ordre: themes du document courant, puis `MGResources.Themes`, puis built-ins.
- Merge profond des groupes et listes nommees; les valeurs explicites de l'enfant gagnent; les valeurs absentes heritent; les cycles produisent une erreur explicite.

### Regle de couverture XAML

Une valeur est pilotable en XAML si et seulement si elle existe dans un groupe de `ThemeDefinition`, dans `Backgrounds`, dans `ControlTemplates` ou dans `ThemePropertyTarget`. Sinon, il faut etendre le contrat C# avant de pouvoir la definir en XAML.

### Hors scope volontaire

- Pas de moteur de theme generique par reflection.
- Pas de remplacement de `MGResources` par un resource dictionary WPF-like.
- La precedence interne des wrappers `ThemeManaged*` n'est pas exposee au format declaratif.

Echantillon de reference: `MGUI.Samples/Features/StyleThemeRefactor.xaml(.cs)`.

## Control templates

### Ordre de resolution

`MGElement` resout son template dans cet ordre (`MGElement.ResolveControlTemplateName()`):

1. instance `ControlTemplate` explicite posee sur l'element;
2. `ControlTemplateName` local ou pose par style;
3. mapping du theme actif: d'abord par type runtime (`Theme.TryGetControlTemplateMapping(GetType(), ...)`), puis par `MGElementType`;
4. fallback `DefaultControlTemplateName` du controle.

Les overrides locaux restent autoritaires; un theme peut choisir un skin structurel pour une famille de controles.

### Mapping de templates dans un theme

`ThemeDefinition.ControlTemplates` contient des `ThemeControlTemplateDefinition` avec `ElementType` OU `ControlTypeName`, plus `TemplateName` (le builder rejette une entree sans l'un des deux). `ControlTypeName` cible un type runtime precis quand plusieurs controles partagent le meme `MGElementType` (ex: controles docking sur `MGElementType.Custom`).

```xaml
<ThemeDefinition.ControlTemplates>
  <ThemeControlTemplateDefinition ElementType="ListView" TemplateName="ListView.HeadersBottom" />
  <ThemeControlTemplateDefinition ControlTypeName="MGDockTabItem" TemplateName="DockTabItem.Minimal" />
</ThemeDefinition.ControlTemplates>
```

Un mapping de theme ne fait que selectionner un template nomme deja present dans `MGResources.ControlTemplates`, alimente par: le catalogue built-in `MGControlTemplateCatalog`, `LoadControlTemplatesFromXaml(...)`, ou des instances `MGControlTemplate` creees par code.

### Phases du template

`MGControlTemplate` (MGUI.Core/UI/Styling/MGControlTemplate.cs) separe trois phases:

- `CreateStructure(...)`: cree les elements et nomme les parts (`MGControlTemplateStructure`). Doit rester sans effet de bord sur l'arbre visuel vivant. `SupportsStructure` indique si le template sait creer une structure.
- `AttachStructure(...)` ou le hook controle `MGElement.AttachControlTemplateStructure(...)`: attache la structure creee a l'instance.
- `ApplyDefaults(...)`: applique le chrome par defaut; re-executable sans risque pendant un refresh de theme. Le constructeur historique `MGControlTemplate(string, Action<MGControlTemplateContext>)` reste la voie de compatibilite pure defaults.

`MGControlTemplateStructure` porte une racine et/ou des `DetachedRoots`: des parts structurelles hors sous-arborescence unique, attachees comme composants independants.

### Valeurs template et refresh

`MGControlTemplateContext.ApplyTemplateValue(Name, Value, GetCurrentValue, SetValue, Invalidation, Comparer)` centralise l'application des valeurs templatees:

- la valeur est stockee comme `UIResolvedValue<T>` de source `Template` avec son `UIInvalidationKind`;
- pendant un refresh de theme (`IsThemeRefresh`), la valeur n'est re-appliquee que si la valeur courante correspond encore au dernier default applique — un override utilisateur est donc respecte;
- une invalidation layout est declenchee pour les kinds `Measure`/`Arrange`/`Structure`.

`ApplyThemeDefault(...)` delegue a `ApplyTemplateValue(...)` avec `UIInvalidationKind.Draw`.

Comportement runtime: un changement de theme declenche `ApplyControlTemplate(true)`; la structure n'est reconstruite que si le nom de template resolu change, sinon seul le chrome est re-applique.

### Contrat de parts

- Un controle migre declare ses parts via `GetRequiredControlTemplateParts()` (retourne des `MGControlTemplatePartRequirement(Name, PartType, IsRequired)`).
- `ValidateControlTemplateParts()` valide centralement parts manquantes et types incompatibles; les messages listent le template, le type attendu, le type resolu et toutes les parts disponibles.
- `MGControlTemplateContext.GetRequiredPart<T>(name)` echoue avec le meme niveau de detail.
- La validation couvre les parts, pas encore les contraintes comportementales inter-parts.

### Catalogue et assets

`MGControlTemplateCatalog` (MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs) enregistre les templates par defaut. Constantes de noms: `Window.Default`, `ToolTip.Default`, `Overlay.Default`, `ContextMenu.Default`, `ContextMenuItem.Default`, `ListBox.Default`, `ListView.Default`, `PropertyGrid.Default`, `GraphView.Default`, `GraphNode.Default`, `GraphPort.Default`, `GraphCommentBox.Default`, `ComboBox.Default`, `ComboBox.DropdownItem.Default`, `TreeView.Default`, `TextBox.Default`, `NumericUpDown.Default`, `TabControl.Default`, `TabControl.Header.Selected`, `TabControl.Header.Unselected`, `Dock.TabItem.Default`, `Dock.AutoHideDrawer.Default`, `Dock.AutoHideStrip.Default`, `Dock.Splitter.Default`, `Dock.DropIndicators.Default`.

Repartition actuelle:

- Templates structurels code-backed (createur de structure + applicateur de defaults): `Window`, `Overlay`, `ComboBox`, `TabControl`, `TreeView`, `TextBox`, `NumericUpDown`, plus les templates PropertyGrid et Graph.
- Templates structurels en asset XAML embarque (`MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml`): `ListBox.Default` (trois `DetachedRoots`: `OuterBorder`, `TitleBorder`, `InnerBorder`, sans racine unique) et `ListView.Default`.
- Applicateurs de defaults seuls (sans phase structurelle): `ContextMenu.Default`, `ContextMenuItem.Default`, `ComboBox.DropdownItem.Default`, les deux headers de `TabControl`, et tous les templates `Dock.*`.

`BuiltInControlTemplates.xaml` contient aussi des variantes `Dark.*` (`Dark.Window`, `Dark.ListBox`, `Dark.DockTabItem`, etc.) exprimees par heritage de template via l'attribut `BasedOn` sur `ControlTemplate`.

### Templates XAML

La couche XAML `ControlTemplatesDocument` / `ControlTemplateDefinition` / `TemplatePartDefinition` (aliases markup `<ControlTemplate>` et `<TemplatePart>`) decrit: nom, type cible, racine visuelle optionnelle, `DetachedRoots` optionnelles et parts nommees. `ControlTemplateLoader` instancie les racines detachees, les mappe dans les `TemplateParts` et neutralise les noms temporaires pour eviter les collisions runtime. Les templates XAML sont des assets de definition, jamais du code execute a chaque draw.

### ElementTemplate vs ControlTemplate

- `ElementTemplate` genere un element autonome reutilisable (contenu, item templating); il ne porte pas de contrat de `TemplatePart`. `ContentTemplate` reste dedie aux contenus de donnees; `ControlTemplate` au chrome des controles.
- `ControlTemplate` code reste la voie la plus directe pour les cas perf-sensibles ou a attachement bespoke.
- `ControlTemplate` XAML convient quand la structure visuelle et les parts suffisent sans code imperative.

### Outillage

`UIToolingService.CaptureVisualTree(...)` produit des `UIVisualTreeSnapshot` exposant `AppliedControlTemplate`, `TemplateParts` (nom -> type runtime), `LastControlTemplateError` et, depuis le 7 septembre 2026, le scope de ressources effectif de chaque element: `ResourceScope` (categorie `UIResourceScope` du scope retourne par `GetResources()`), `ResourceScopeOwnerDiagnosticId` (id diagnostic stable de l'element qui possede ce scope, ou de la desktop pour le scope racine, null si le scope n'est possede par aucun element de la chaine) et `HasLocalResourceScope` (l'element a materialise son propre scope via `EnsureResourceScope`, par opposition a un scope herite). Le rendu texte du snapshot reprend ces trois valeurs. Le snapshot ne capture pas encore l'origine des valeurs (tache 5 du backlog).

## Statut lookless

### Frontieres

- Le controle possede: logique metier, etat, navigation, evenements, orchestration de contenu.
- Le template possede: structure visuelle, wrappers de chrome, presenters, topologie visuelle.
- Le theme selectionne des templates et fournit des tokens visuels partages; il ne decrit jamais de structure.
- Les styles restent un mecanisme de surcharge locale/implicite, distinct du mapping theme -> template.

### Controles migres

Templates structurels ou chrome entierement template-driven: `MGWindow`, `MGOverlay`, `MGContextMenu`, `MGContextMenuItem`, `MGListBox`, `MGListView`, `MGComboBox`, `MGTreeView`, `MGTabControl`, `MGTextBox` (et `MGPasswordBox`), `MGToolTip`, plus les cinq controles docking feuilles en mode applicateur de defaults (`MGDockTabItem`, `MGDockAutoHideDrawer`, `MGDockAutoHideStrip`, `MGDockSplitterBar`, `MGDockDropIndicators`).

Hooks specifiques:

- `MGTabControl` pilote le chrome de ses headers via `SelectedTabHeaderControlTemplateName` / `UnselectedTabHeaderControlTemplateName` (templates catalogue `TabControl.Header.Selected` / `TabControl.Header.Unselected`); les wrappers par defaut restent des `MGButton`, retemplates en place sans recreation.
- `MGComboBox` expose `DropdownItemControlTemplateName`; les items de dropdown par defaut passent par `ComboBox.DropdownItem.Default`.
- Les quatre couleurs de selection de `MGTextBox` sont appliquees par le template `TextBox.Default` via `ApplyThemeDefault` (MGControlTemplateCatalog.cs), donc re-appliquees a chaque refresh de theme.
- Le chrome de `MGToolTip` (`DrawOffset`, foreground, padding, bordure, minima) est centralise dans le catalogue via `ToolTip.Default`; `MGTheme` porte `ToolTipOffset` et `ToolTipTextForeground`.

### Exceptions non-lookless assumees

- `MGCheckBox` et `MGRadioButton` conservent un dessin de glyphe specialise, mais via les primitives partagees `UISymbolDrawing` (MGUI.Core/UI/UISymbolDrawing.cs) et les elements symboles (`MGTriangleArrowIcon` et famille, MGUI.Core/UI/UISymbolElements.cs) — chemins allocation-free.
- `MGScrollViewer` dessine ses scrollbars en interne (`DrawSelf`).

### Signaux de classification

Pour evaluer le degre de decouplage d'un controle: presence de `DefaultControlTemplateName`; presence de `AttachControlTemplateStructure(...)`; `OnThemeChanged(...)` copiant des valeurs de theme dans des proprietes locales (signal negatif); dessin d'icones/etats dans `OnEndingDraw`/`DrawSelf`/`DrawContents` (signal negatif); construction imperative de sous-parts dans le controle (signal negatif).

Controles encore faiblement decouples: `MGRadioButton`, `MGSlider`, `MGProgressBar`, `MGProgressButton`, `MGRatingControl`, `MGGridColorPicker`, `MGGroupBox`, `MGExpander`, `MGMenuBar`, `MGSpoiler`, `MGChatBox`, `MGResizeGrip`, la plupart des controles docking, et `MGDockHost` (le plus couple).

## Regles d'ecriture d'un template

### Workflow auteur XAML

1. Ecrire un `ControlTemplate` XAML avec racine et/ou `DetachedRoots`, declarer les `TemplatePart` nommees.
2. Charger via `MGResources.LoadControlTemplatesFromXaml(...)`.
3. Assigner `ControlTemplateName` sur le controle cible (ou le mapper dans un theme).
4. Verifier avec `UIToolingService.CaptureVisualTree(...)`: template applique, parts exposees, derniere erreur de template, scope de ressources effectif et son proprietaire.

### Migration d'un controle composite

1. Declarer les parts requises via `GetRequiredControlTemplateParts()`.
2. Extraire la creation du chrome vers une structure de template.
3. Attacher les parts dans `AttachControlTemplateStructure(...)`.
4. Deplacer les defaults visuels dans `MGControlTemplateCatalog` ou un asset XAML.
5. Garder logique metier, input et navigation dans le controle.
6. Ajouter des tests d'infrastructure ou de non-regression.

### Regles de robustesse

- Etat logique de secours: toute propriete qui pilote une part templatee doit garder un backing field independant de la part vivante et le re-appliquer dans `AttachControlTemplateStructure(...)` — les constructeurs de base et le loader XAML peuvent toucher ces proprietes avant l'attachement.
- Propriete d'une part: une part ne doit jamais etre creable a la fois par le constructeur et par le template. Apres migration, le controle attache les parts fournies et cesse de construire le chrome equivalent.
- Controles derives: un type derive d'un controle deja template (`MGContextMenu` derive de `MGWindow`) doit tolerer la phase de template de base pendant sa construction; sa validation de parts ne peut pas supposer que son template final est actif.
- Verrouillage des content hosts: cabler les enfants d'abord, poser `CanChangeContent = false` ensuite — l'ordre inverse echoue au runtime.
- Detachement: `MGElement.ClearInstantiatedTemplateStructure()` ne nettoie automatiquement `Structure.Root` que pour `MGSingleContentHost`. Les composites a composants doivent avoir une logique de detachement explicite, reutiliser leurs slots de composants a l'attachement, et eviter les swaps de template frequents au runtime.

## Docking: vocabulaire des parts visuelles

Le docking est un flux de migration dedie avec son propre vocabulaire. Regles: parts en `PART_*`; reutiliser les roles partages plutot qu'inventer des alias par controle; comportement et orchestration de layout restent sur le controle proprietaire; seuls les aspects paint-only migrent vers des parts/elements symboles; les etats semantiques docking (active, preview-visible, drop-target-active, drop-target-disabled, auto-hide-open/collapsed, resizing) restent portes par des proprietes tant qu'une projection partagee n'existe pas.

Parts effectivement enregistrees aujourd'hui (constantes dans MGUI.Core/UI/Docking/Controls/*.cs):

- `MGDockTabItem`: `PART_Surface`, `PART_Accent`, `PART_CloseIcon`, `PART_PinIcon`
- `MGDockTabGroup`: `PART_Accent`, `PART_DropdownIcon`, `PART_WindowStateIcon`
- `MGDockSplitterBar`: `PART_Surface`, `PART_Accent`, `PART_Grip`
- `MGDockAutoHideStrip`: `PART_Separator`
- `MGDockAutoHideDrawer`: `PART_Border`, `PART_Header`, `PART_TitleLabel`, `PART_PinButton`, `PART_CloseButton`, `PART_PinIcon`, `PART_CloseIcon`, `PART_ResizeGrip`
- `MGDockPreviewOverlay`: `PART_Surface`, `PART_Border`
- `MGDockHost`: `PART_PreviewOverlay`, `PART_DropIndicators`, `PART_LeftAutoHideStrip`, `PART_RightAutoHideStrip`, `PART_TopAutoHideStrip`, `PART_BottomAutoHideStrip`, `PART_AutoHideDrawer`
- `MGDockDropIndicators`: aucune part `PART_*` enregistree (les zones de drop sont des elements enfants internes)

Ce vocabulaire diverge en partie de la cible initiale (roles `PART_HeaderText`/`PART_Grip` sur le drawer, schema `PART_TabHeader`/`PART_TabTitle`/`PART_TabCloseButton` pour les tabs, parts `PART_*DropZone`). La convergence — blesser les noms livres ou renommer vers la cible — est une decision attachee a la migration structurelle docking (voir Docs/Tasks/styling-theme-tasks.md).

Les templates `Dock.*.Default` du catalogue sont des applicateurs de defaults sans phase structurelle: aucun controle docking ne surcharge `GetRequiredControlTemplateParts()` ni `AttachControlTemplateStructure(...)`. Des variantes `Dark.Dock*` existent deja en asset XAML via `BasedOn`.

## Limites connues (verifiees)

- Abonnements dynamic resource lies a l'arbre (livre le 7 septembre 2026, ADR-0001): `UIResourceReferenceApplicator` tient un conteneur `UIDynamicResourceSubscriptions` par element hote (Metadata `DynamicResourceSubscriptions`), avec un seul handler sur le scope le plus proche, detache et rattache sur `OnParentChanged`, re-resolution contre le scope courant. Les changements des scopes ancetres arrivent par `MGResources.OnStaticResourceLookupChanged`, forwarde parent -> enfant par le meme lien faible unique que le theme. Limite restante: un scope local cree tardivement sur un ancetre (`EnsureResourceScope` apres construction) n'est suivi qu'au prochain changement de parent.
- Styles appliques uniquement au parse XAML: `Element.ProcessStyles(...)` tourne pendant le parsing puis s'arrete. Aucune API de restyle d'un sous-arbre deja charge n'existe.
- Pas de moteur unifie de resolution: seule la voie template stampe des `UIResolvedValue<T>`; les autres sources restent des setters ou callbacks ordinaires. Pas d'API de diagnostic de source de valeur.
- Invalidation de theme opt-in: defaut `Draw` seul; seule `MGTextBlock` surcharge `GetThemeInvalidation(...)`. Chaque nouvelle propriete themee layout-affecting doit y penser manuellement.
- `MGElement.NotifyThemeChanged(...)` parcourt tout le sous-arbre et les composants sans granularite par propriete: cout notable sur des UIs denses type editeur.
- `MGScrollViewer.CanCacheSelfMeasurement => false` (MGUI.Core/UI/MGScrollViewer.cs, ligne 443): hotspot layout connu pour les contenus scrollables denses.
- `MGTheme` reste la source centrale monolithique des tokens par defaut; aucune extraction de tokens semantiques n'existe.
- Le changement de template structurel en cours de vie reste plus couteux qu'un refresh de theme; le detachement generique est limite a `MGSingleContentHost`.
- Le loader XAML de templates ne couvre pas un DSL complet d'attachement custom; la validation de parts ne couvre pas les contraintes inter-parts.
- Les templates docking n'ont pas de phase structurelle (voir section docking).

## Reste a faire

Les travaux restants du theme styling/theme (hygiene des abonnements, diagnostics de valeurs, convention d'invalidation, refresh de styles, migration structurelle docking, composites a surfaces auxiliaires, preset editeur) sont specifies dans `Docs/Tasks/styling-theme-tasks.md`.
