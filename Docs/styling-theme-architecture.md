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

Depuis la tache 4 (programme `Docs/Tasks/resolved-value-engine-tasks.md`, ADR-0005, septembre 2026), sept proprietes pilotes ont un vrai moteur runtime : Margin, Padding, MinHeight, BorderBrush et BorderThickness (la bordure reelle d'un composite, atteinte par `GetBorder()`), Background (conteneur `VisualStateFillBrush`) et le texte (deux conteneurs `VisualStateSetting<Color?>`, `MGTextBlock.Foreground` et `MGElement.DefaultTextForeground`, donc huit cles `UIPilotProperty`). Chaque element alloue paresseusement un `UIResolvedPropertyStore` (`MGUI.Core/UI/Styling/UIResolvedPropertyStore.cs`) ; chaque ecriture passe par un setter tague (`SetPadding(value, source)`, `MGBorder.SetBorderBrush(...)`, `SetBackgroundSlot(slot, value, source)`, `SetDefaultTextForegroundSlot(...)`, ...) qui enregistre sa `UIValueResolutionSource` et n'ecrit la valeur CLR, avec les notifications existantes, que si le gagnant selon `UIValuePrecedence` change. Le setter public vaut `LocalValue` ; les constructeurs `DefaultValue` ; le catalogue de templates `Theme` quand il ecrit le controle lui-meme et `Template` quand il ecrit une part ; les callbacks `OnThemeChanged` et les helpers de theme `Theme` ; les etats visuels internes (selection, actif, drag) `VisualState` ; le transfert XAML `ImplicitStyle`, `ExplicitStyle` ou `LocalValue` selon la provenance enregistree par `Element.ProcessStyles` ; les bindings `LocalBinding` ; les ressources dynamiques `DynamicResource` (le retrait de la ressource retire la contribution et fait retomber sur la source suivante). Le balayage d'architecture `MGUI.Tests/Architecture/ResolvedPilotWriteSitesTests.cs` garantit qu'aucune ecriture framework de ces pilotes ne reste sur un setter public non tague. Point de lecture interne pour l'outillage et la tache 5 : `MGElement.TryGetResolvedValueSource(UIPilotProperty, UIValueSlot, out UIValueResolutionSource)` (source du gagnant, memes replis a la lecture que `TryGetResolvedPilotValue<T>`) et `EnumerateResolvedContributions` (contenu brut du store, precedence decroissante, valeurs boxees dans `UIResolvedContribution`) ; la liste des huit cles et la carte cle -> `UIInvalidationKind` sont epinglees par `ResolvedValueSourceDiagnosticsTests`.

Toutes les autres proprietes restent des proprietes C# ordinaires, sans store ni cout, et leur precedence effective reste dispersee entre les quatre chemins historiques (styles XAML au parsing, callbacks `OnThemeChanged`, setters ordinaires, chemin template). Toute nouvelle propriete themee ou stylee doit projeter ses valeurs dans la pile officielle, jamais via un ordre ad hoc par controle ; l'ajout d'un pilote suit le patron des tranches S2 a S8 du programme.

### Arbitrages

- Un template ne doit jamais ecraser une valeur locale du controle hote ; il pose des defauts sur ses propres parts (`Template`, 60) et des defauts de theme sur le controle lui-meme (`Theme`, 20), ce qui laisse les styles XAML (40/50) et les attributs (90) l'emporter sur le chrome par defaut du controle.
- Un attribut XAML l'emporte sur un style ; un style l'emporte sur les defauts de theme du controle et sur une ressource dynamique (30) ; une ecriture locale posterieure l'emporte sur un binding (80) et sur une ressource dynamique.
- Une propriete alimentee par binding (`LocalBinding`) se comporte presque comme une locale : un style ne l'ecrase pas, seule une ecriture locale explicite la bat.
- Une animation gagne pendant sa duree puis la valeur revient a la meilleure source non animee (niveau present dans le store, non alimente par le framework).
- Une ressource dynamique gagne sur le theme et sur les defauts du controle ; son retrait retire sa contribution (`Unset`) et fait retomber sur la source suivante.
- Conteneurs (fond, texte) : une ecriture de l'objet entier (`Whole`) de precedence P retire les sous-champs poses exactement a P (dernier ecrivain), garde dormants les plus bas et re-applique les plus hauts sur le nouveau conteneur ; une ecriture non taguee d'un sous-champ par l'application est attribuee `LocalValue` a l'element detenteur.

### Heritage

- Heritables par defaut : foreground texte par defaut, famille de police par defaut (taille de police si valide pour le layout texte).
- Non heritables : contraintes de layout, fonds/bordures/chrome, templates, commandes et comportement, etats interactifs, geometries de skin.
- L'heritage se resout depuis la valeur RESOLUE de l'ancetre, et seulement quand aucune source plus forte n'existe sur l'element courant : `MGTextBlock.ActualForeground` lit son conteneur `Foreground`, puis la chaine `DefaultTextForeground` des ancetres, puis le repli du theme ; le diagnostic rapporte alors `Inherited` ou `Theme` a la lecture.

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
- Surfaces auxiliaires (11 septembre 2026) : un menu contextuel cree sur une fenetre (constructeur sans `Theme`, `CreateSimpleMenu`, menu XAML), ses sous-menus, la liste deroulante d'un `MGComboBox` (template code `ComboBox.Default` et `PART_DropdownWindow` XAML de `Dark.ComboBox`), un `MGToolTip` (code, XAML ou chaine convertie), la fenetre de `MGColorPickerPopup` et toute fenetre XAML imbriquee sans `ThemeName` ne copient plus le theme (explicite ou effectif) de leur fenetre proprietaire : leur scope `Window` n'a pas d'override, herite du scope proprietaire et suit ses changements de theme. Un theme explicite (argument `Theme`, `MGWindow.Theme`, `ThemeName`) epingle toujours. Le fond de construction d'un element (source `DefaultValue`) vient du theme effectif du scope de sa fenetre parente, et non plus du seul champ `MGWindow.Theme`, pour que les elements crees dans une surface sans theme explicite (separateurs, sous-menus, panneaux de la liste) prennent le theme du proprietaire. Un sous-menu garde sa propre fabrique de lignes par defaut, pour que `MGContextMenu.OnThemeChanged` reconstruise aussi ses lignes.
- `MGComboBox` re-applique le template de tous ses items au refresh de theme (surcharge de `ApplyControlTemplate`, `OnThemeChanged` restant interdit) : un item n'est ajoute a `PART_DropdownItemsPanel` qu'a l'ouverture de la liste ; tant qu'elle n'a jamais ete ouverte (ou apres une regeneration des items liste fermee), aucune descente de `NotifyThemeChanged` ne l'atteint. Un item deja ajoute reste dans le panneau apres fermeture et est aussi rafraichi par le scope de la liste.
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

`MGControlTemplateContext.ApplyTemplateValue(Name, Value, GetCurrentValue, SetValue, Invalidation, Comparer)` centralise l'application des valeurs templatees :

- la valeur est stockee comme `UIResolvedValue<T>` avec sa source et son `UIInvalidationKind` dans `_AppliedTemplateDefaults` ;
- pendant un refresh de theme (`IsThemeRefresh`), la valeur n'est re-appliquee que si la valeur courante correspond encore au dernier default applique (garde has-previous/equals) ; pour les proprietes pilotes, la precedence du store protege en plus une valeur locale, un style ou un binding quelle que soit la garde ;
- une invalidation layout est declenchee pour les kinds `Measure`/`Arrange`/`Structure`.

Surcharges taguees (ADR-0005, tranches S3 et S7a) : `ApplyThemeDefault`/`ApplyTemplateValue` avec un `Action<T, UIValueResolutionSource>` passent a la lambda la source `Template` (60) et servent aux defauts des PARTS ; `ApplyOwnerThemeDefault` passe la source `Theme` (20) et sert aux defauts que le catalogue pose sur le CONTROLE LUI-MEME (sa propre propriete, la bordure exposee par `GetBorder()` ou par sa facade publique, ou l'element que son DTO XAML ecrit pour la meme propriete), pour que les styles XAML et les attributs continuent de l'emporter sur le chrome par defaut du controle. `ApplyThemeDefault(...)` non tague delegue a `ApplyTemplateValue(...)` avec `UIInvalidationKind.Draw` ; les cles pilotes layout-affecting (Padding, Margin, MinHeight, BorderThickness) passent explicitement `Measure | Arrange`.

Comportement runtime : un changement de theme declenche `OnThemeChanged` puis `ApplyControlTemplate(true)` ; la structure n'est censee etre reconstruite que si le nom de template resolu change, sinon seul le chrome est re-applique. Limite verifiee le 11 septembre 2026 (hors perimetre de la tache 4) : `MGWindow` reconstruit ses parts de chrome a chaque changement de theme (il surcharge `ApplyControlTemplate`) et `MGTreeView` recree son `ItemsPanel` en laissant les items existants parentes a l'ancien panneau ; la garde has-previous/equals compare alors la valeur de construction de la nouvelle part a celle appliquee sur l'ancienne, et n'applique jamais une valeur de type reference sur une part recreee.

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

Vocabulaire fige le 7 septembre 2026 (ADR-0002, `Docs/decisions/0002-docking-part-vocabulary.md`): un role partage par le framework prime sur tout alias par controle. Les roles partages de reference sont ceux de `MGWindow` (`PART_Border`, `PART_TitleBar`, `PART_TitleBarText`, `PART_CloseButton`, `PART_ResizeGrip`), de `MGTabControl` (`PART_HeadersPanel`) et, entre controles docking, `PART_Surface`/`PART_Accent`. Les noms de l'ancienne cible (`PART_HeaderText`, `PART_Grip` comme poignee de redimensionnement, `PART_TabHeader`/`PART_TabTitle`/`PART_TabCloseButton`, `PART_Overlay`) sont abandonnes: aucun n'existait ailleurs, et `PART_Grip` designe la poignee de drag du splitter, un autre role.

Parts enregistrees par controle (constantes `*PartName` dans MGUI.Core/UI/Docking/Controls/*.cs, pinnees par les tests d'infrastructure):

- `MGDockTabItem`: `PART_Surface`, `PART_Accent`, `PART_TitleText`, `PART_CloseButton`, `PART_PinButton`, `PART_CloseIcon`, `PART_PinIcon`
- `MGDockTabGroup`: `PART_Accent`, `PART_DropdownIcon`, `PART_WindowStateIcon`, `PART_HeadersPanel`
- `MGDockSplitterBar`: `PART_Surface`, `PART_Accent`, `PART_Grip`
- `MGDockAutoHideStrip`: `PART_Separator`
- `MGDockAutoHideDrawer`: `PART_Border`, `PART_TitleBar`, `PART_TitleBarText`, `PART_PinButton`, `PART_CloseButton`, `PART_PinIcon`, `PART_CloseIcon`, `PART_ResizeGrip` (les anciens identifiants `HeaderPartName` et `TitleLabelPartName` restent en alias obsoletes portant les nouvelles valeurs)
- `MGDockPreviewOverlay`: `PART_Surface`, `PART_Border`
- `MGDockHost`: `PART_PreviewOverlay`, `PART_DropIndicators`, `PART_LeftAutoHideStrip`, `PART_RightAutoHideStrip`, `PART_TopAutoHideStrip`, `PART_BottomAutoHideStrip`, `PART_AutoHideDrawer`
- `MGDockDropIndicators`: `PART_LeftDropZone`, `PART_RightDropZone`, `PART_TopDropZone`, `PART_BottomDropZone`, `PART_CenterDropZone`, `PART_HostLeftDropZone`, `PART_HostRightDropZone`, `PART_HostTopDropZone`, `PART_HostBottomDropZone` (pas de `PART_Overlay`: le controle est lui-meme l'overlay, sans element surface distinct)

La migration structurelle (taches 8 et 9 de Docs/Tasks/styling-theme-tasks.md) part de ce jeu fige: parts requises et createurs de structure utilisent ces noms.

Les templates `Dock.*.Default` du catalogue sont des applicateurs de defaults sans phase structurelle: aucun controle docking ne surcharge `GetRequiredControlTemplateParts()` ni `AttachControlTemplateStructure(...)`. Des variantes `Dark.Dock*` existent deja en asset XAML via `BasedOn`.

## Limites connues (verifiees)

- Abonnements dynamic resource lies a l'arbre (livre le 7 septembre 2026, ADR-0001): `UIResourceReferenceApplicator` tient un conteneur `UIDynamicResourceSubscriptions` par element hote (Metadata `DynamicResourceSubscriptions`), avec un seul handler sur le scope le plus proche, detache et rattache sur `OnParentChanged`, re-resolution contre le scope courant. Les changements des scopes ancetres arrivent par `MGResources.OnStaticResourceLookupChanged`, forwarde parent -> enfant par le meme lien faible unique que le theme. Limite restante: un scope local cree tardivement sur un ancetre (`EnsureResourceScope` apres construction) n'est suivi qu'au prochain changement de parent.
- Styles appliques uniquement au parse XAML: `Element.ProcessStyles(...)` tourne pendant le parsing puis s'arrete. Aucune API de restyle d'un sous-arbre deja charge n'existe ; depuis la tache 4, le DTO XAML conserve toutefois la provenance de style par propriete (`Element.StyleProvenance`) et le store des pilotes connait la source de chaque valeur, ce qui est le suivi runtime que la tache 10 attendait.
- Moteur de valeurs resolues limite aux sept pilotes (tache 4, ADR-0005) : Margin, Padding, MinHeight, BorderBrush, BorderThickness, Background, texte (huit cles, deux conteneurs pour le texte). Limites verifiees le 11 septembre 2026 : un conteneur (`VisualStateFillBrush`, `VisualStateSetting<Color?>`) partage entre plusieurs elements est enregistre par chaque detenteur et, l'element s'abonnant a son `PropertyChanged`, un conteneur partage a longue duree de vie enracine ses detenteurs ; le framework copie donc toujours les instances brutes du theme et les modeles de controle (`MGDockAutoHideStrip.ButtonBackgroundBrush`, `MGTreeView.SelectionBackgroundBrush`) avant de les remettre a un element, et une instance partagee par l'application reste sous sa responsabilite. Les niveaux `Animation` et, hors selection d'arbre, noeud de graphe et onglets docking, `VisualState` ne sont pas alimentes par le framework. `_AppliedTemplateDefaults` et sa garde restent en parallele du store. Le diagnostic `TryGetResolvedPilotValue` d'un sous-slot de texte replie sur `Inherited`/`Theme` selon l'etat visuel courant, pas selon le slot demande. Les facades de bordure nommees autrement que `BorderBrush`/`BorderThickness` (`ListBox` Outer/Inner/Title/ItemsPanel, `Spoiler` Unspoiled*) rapportent `LocalValue` au lieu de la provenance de style. Un `Padding` declare en XAML sur une `Window` (attribut ou style) supplante l'ecriture `LocalValue` que le setter `WindowStyle` laisse au parse (le DTO la retire avant de re-appliquer le padding sous sa provenance) ; une affectation ulterieure de `WindowStyle` par l'application re-epingle Padding et BorderThickness en `LocalValue`. Dans la pile officielle, `DynamicResource` (30) est sous les styles : un setter de style sur la meme propriete l'emporte sur un attribut `{DynamicResource}` (aucun sample n'est dans ce cas).
- Invalidation de theme opt-in: defaut `Draw` seul; seule `MGTextBlock` surcharge `GetThemeInvalidation(...)`. Chaque nouvelle propriete themee layout-affecting doit y penser manuellement.
- `MGElement.NotifyThemeChanged(...)` parcourt tout le sous-arbre et les composants sans granularite par propriete: cout notable sur des UIs denses type editeur.
- `MGScrollViewer.CanCacheSelfMeasurement => false` (MGUI.Core/UI/MGScrollViewer.cs, ligne 443): hotspot layout connu pour les contenus scrollables denses.
- `MGTheme` reste la source centrale monolithique des tokens par defaut; aucune extraction de tokens semantiques n'existe.
- Le changement de template structurel en cours de vie reste plus couteux qu'un refresh de theme; le detachement generique est limite a `MGSingleContentHost`.
- Le loader XAML de templates ne couvre pas un DSL complet d'attachement custom; la validation de parts ne couvre pas les contraintes inter-parts.
- Les templates docking n'ont pas de phase structurelle (voir section docking).

## Reste a faire

Les travaux restants du theme styling/theme (hygiene des abonnements, diagnostics de valeurs, convention d'invalidation, refresh de styles, migration structurelle docking, composites a surfaces auxiliaires, preset editeur) sont specifies dans `Docs/Tasks/styling-theme-tasks.md`.
