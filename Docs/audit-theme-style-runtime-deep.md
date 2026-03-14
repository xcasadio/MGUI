# Resume executif

MGUI a nettement progresse depuis le premier audit. Le depot contient maintenant une vraie base d'architecture pour un framework UI de jeu video: scopes de ressources hierarchiques via `MGResources`, modelisation explicite de la precedence via `UIValuePrecedence`, support `StaticResource` et `DynamicResource`, definitions de themes XAML avec `BasedOn`, `MGControlTemplate` nommes, `MGVisualStateProjection`, et une navigation clavier/manette centralisee.

Le point cle est que ces briques ne sont pas encore portees par un systeme runtime unifie de resolution de valeurs. La precedence existe comme modele et comme contrat de tests, mais dans les controles la plupart des valeurs visuelles restent des proprietes C# ordinaires remplies par constructeur, par `OnThemeChanged(...)`, ou par des helpers imperatifs. En pratique, MGUI est aujourd'hui un framework a theming solide et pragmatique, mais pas encore un framework lookless a templates structuraux.

Le systeme de `ControlTemplate` actuel est utile, mais il agit surtout comme un applicateur de defaults sur des template parts deja crees par le controle. Cela convient bien a un pipeline de jeu stable et peu magique, mais cela limite les re-skins profonds, les variantes denses pour l'editeur, et la separabilite complete logique/apparence.

Dans le contexte jeu video, le plus gros risque n'est pas un manque de flexibilité "desktop", mais le caractere hybride de certains pipelines: theme/runtime/style/template n'ecrivent pas tous via la meme couche, ce qui rend le cout d'invalidation, le comportement sous changement de theme, et la tracabilite des valeurs moins predictibles qu'ils devraient l'etre.

Conclusion de l'audit:

- MGUI est deja bien positionne pour un framework UI temps reel pragmatique.
- Le socle theme/style/template est utilisable pour des ecrans in-game et commence a etre credible pour l'editeur.
- Les priorites ne sont pas un gros clone de WPF, mais une consolidation du pipeline existant: resolution de valeurs unifiee sur un sous-ensemble cible, templates plus structurels pour les controles composites, diagnostics, et hygiene runtime des invalidations et abonnements.

# Architecture actuelle observee

Le pipeline observe repose sur cinq couches principales.

- Ressources: `MGUI.Core/UI/MGResources.cs` expose des scopes `Desktop`, `Window`, `Subtree`, `Template`, avec lookup parent via `TryGetTheme`, `TryGetStyle`, `TryGetStaticResource`, `TryGetControlTemplate`, `TryGetElementTemplate`.
- Controles: `MGUI.Core/UI/MGElement.cs` resout ses ressources via `GetResources()`, peut creer un scope local avec `EnsureResourceScope(...)`, et propage les changements de theme via `NotifyThemeChanged(...)`.
- Styles XAML: `MGUI.Core/UI/XAML/Element.cs` applique styles implicites et explicites pendant le parsing, avec protection des valeurs explicitement assignees par le parser via `ExplicitlySetProperties`.
- Templates: `MGUI.Core/UI/Styling/MGControlTemplate.cs` et `MGControlTemplateCatalog.cs` appliquent des defaults a des parts nommees deja construites par les controles; `MGUI.Core/UI/MGElementTemplate.cs` et `XAML/Templates.cs` couvrent les templates de contenu.
- Etats visuels et navigation: `MGUI.Core/UI/MGElement.cs`, `MGUI.Core/UI/VisualState.cs`, `MGUI.Core/UI/Styling/MGVisualStateProjection.cs`, `MGUI.Core/UI/Navigation/UIFocusNavigationService.cs`, `MGUI.Core/UI/MGDesktop.cs`.

Pipeline observe pour la resolution de ressource:

```text
XAML MarkupExtension
  -> UIResourceReferenceConfig stocke sur XAMLBindableBase.ResourceReferences
  -> creation du graphe runtime
  -> UIResourceReferenceApplicator.Apply(...)
  -> MGResources.TryGetStaticResource(...)
  -> fallback vers Parent
  -> conversion vers type runtime cible
  -> abonnement aux evenements OnStaticResourceAdded/Changed/Removed de tous les scopes ancetres si resource dynamique
```

Pipeline observe pour le changement de theme:

```text
MGResources.DefaultTheme change
  -> OnDefaultThemeChanged du scope
  -> MGElement.NotifyThemeChanged(...)
  -> OnThemeChanged(...) du controle
  -> ApplyControlTemplate(true)
  -> InvalidateLayout() si GetThemeInvalidation(...) le demande
  -> propagation recursive aux enfants sans scope local et aux composants sans scope local
```

Pipeline observe pour les styles:

```text
XAMLParser
  -> Element.ProcessStyles(MGResources)
  -> merge des styles implicites desktop + styles explicites scopes parents + styles locaux du noeud XAML
  -> application sur proprietes runtime via reflection
  -> protection des valeurs explicitement ecrites dans le XAML
  -> recursion sur les enfants XAML
```

Pipeline observe pour les `ControlTemplate`:

```text
Constructeur du controle
  -> creation des sous-elements
  -> RegisterTemplatePart(...)
  -> ControlTemplateName = ...
  -> MGElement.ApplyControlTemplate(false)
  -> MGControlTemplateCatalog applique des defaults theme/template sur les parts existantes
```

Precedence observee dans le code:

| Source | Ordre observe | Etat reel |
| --- | ---: | --- |
| `Animation` | 100 | Modele present dans `UIValuePrecedence`, pas de property store general visible dans les controles audites |
| `LocalValue` | 90 | Valeurs C# affectees directement sur le controle ou ses parts |
| `LocalBinding` | 80 | Valeurs poussees par `DataBinding` |
| `VisualState` | 70 | Effectif surtout via `VisualStateFillBrush`, `VisualStateSetting<T>` et `MGVisualStateProjection` |
| `Template` | 60 | Effectif uniquement si le controle/part lit le template catalog; pas de systeme generique de property source |
| `ExplicitStyle` | 50 | Effectif au parsing XAML uniquement |
| `ImplicitStyle` | 40 | Effectif au parsing XAML uniquement |
| `DynamicResource` | 30 | Effectif pour les cibles XAML referencees via `UIResourceReferenceApplicator` |
| `Theme` | 20 | Tres present via `GetTheme()` et `OnThemeChanged(...)` |
| `Inherited` | 10 | Partiel, surtout pour `MGResources`, `DefaultTextForeground`, quelques valeurs derivees |
| `DefaultValue` | 0 | Constructeurs C# et valeurs par defaut des proprietes |

Point important: cette table est stable comme intention architecturale, et elle est couverte par `MGUI.Tests/Architecture/StyleValueResolutionModelTests.cs`, mais elle n'est pas encore appliquee par un moteur runtime unique. La precedence reelle reste donc partiellement dispersee entre parsing XAML, callbacks de theme, et setters imperatifs.

# Points solides

- `MGResources` est une bonne facade de ressources pour un moteur de jeu. Les scopes, le fallback parent, `Definitions` et `RuntimeCache` donnent un socle propre sans sur-ingénierie (`MGUI.Core/UI/MGResources.cs`, `MGUI.Tests/Architecture/ResourceScopeLookupTests.cs`, `MGUI.Tests/Architecture/ResourceSeparationTests.cs`).
- Le support de themes XAML est deja exploitable. `ThemeDefinitionLoader` et `ThemeDefinitionBuilder` gerent `BasedOn`, themes built-in et detection de cycles (`MGUI.Core/UI/XAML/Themes.cs`, `MGUI.Core/UI/XAML/ThemeDefinitionLoader.cs`, `MGUI.Tests/Architecture/ThemeDefinitionTests.cs`).
- La precedence est formalisee explicitement au niveau architecture. `UIValuePrecedence`, `UIValueResolutionSource` et `UIResolvedValue<T>` clarifient deja le modele cible (`MGUI.Core/UI/Styling/UIValuePrecedence.cs`, `UIValueResolutionSource.cs`, `UIResolvedValue.cs`).
- Les `DynamicResource` fonctionnent reellement avec re-evaluation sur override/fallback de scope parent (`MGUI.Core/UI/Styling/UIResourceReferenceApplicator.cs`, `MGUI.Tests/Architecture/ResourceReferenceApplicatorTests.cs`).
- Le socle `VisualState` est adapte au jeu. `MGElement` calcule les etats `Normal/Focused/Selected/Disabled` et `Hovered/Pressed`, et `MGDesktop` mappe les actions manette vers `UINavigationAction` (`MGUI.Core/UI/MGElement.cs`, `MGUI.Core/UI/MGDesktop.cs`, `MGUI.Core/UI/Navigation/UIFocusNavigationService.cs`).
- `MGVisualStateProjection` est une bonne brique pour sortir de la logique ad hoc et projeter l'etat vers le visuel (`MGUI.Core/UI/Styling/MGVisualStateProjection.cs`).
- Le docking est la zone la plus mature cote theme/template. `MGTheme.Docking` centralise de nombreuses valeurs visuelles, et `MGControlTemplateCatalog` sait les injecter dans `MGDockTabItem`, `MGDockAutoHideDrawer`, `MGDockAutoHideStrip`, `MGDockSplitterBar`, `MGDockDropIndicators` (`MGUI.Core/UI/MGTheme.cs`, `MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs`).
- Le depot possede deja un minimum d'outillage d'inspection utile pour l'editeur: capture de visual tree et preview XAML (`MGUI.Core/Tooling/UIToolingService.cs`, `MGUI.Core/Tooling/UIVisualTreeSnapshot.cs`).

# Couplages et fragilites

Classification des controles audites:

| Categorie | Controles | Constat |
| --- | --- | --- |
| Relativement propres | `MGDockTabItem`, `MGDockAutoHideDrawer`, `MGDockAutoHideStrip`, `MGDockSplitterBar`, `MGDockDropIndicators`, `MGContextMenuItem` | Le theme passe majoritairement par `MGTheme.Docking`, `ControlTemplateName`, des template parts nommees, et des proprietes visuelles dediees. Il reste du custom draw, mais le chrome est deja externe au coeur du comportement. |
| Hybrides | `MGWindow`, `MGOverlay`, `MGContextMenu`, `MGListBox<T>`, `MGListView<T>`, `MGTreeView` | Ces controles ont des parts, un `ControlTemplateName` et un passage dans `MGControlTemplateCatalog`, mais ils construisent eux-memes leur structure et affectent encore beaucoup de valeurs visuelles en constructeur ou en helper `ApplyDefaultStyles()`. |
| Trop couples au theme | `MGComboBox<T>`, `MGTabControl`, `MGTextBox`, `MGToolTip`, `MGScrollViewer`, `MGButton`, `MGToggleButton`, `MGCheckBox`, `MGRadioButton`, `MGProgressBar`, `MGProgressButton`, `MGSlider`, `MGResizeGrip`, `MGMenuBar` | Le look est encore en partie dans la logique du controle: `GetTheme()` en constructeur, `OnThemeChanged(...)` manuel, wrappers internes crees et styles imperatifs, ou dessin direct avec tokens theme lus au moment du draw. |

Constats structurants:

- Le `ControlTemplate` actuel n'est pas un vrai `ControlTemplate` structurel. Il ne fabrique pas la structure visuelle d'un controle; il opere sur des parts deja construites via `RegisterTemplatePart(...)` (`MGUI.Core/UI/MGElement.cs`, `MGUI.Core/UI/Styling/MGControlTemplate.cs`, `MGControlTemplateCatalog.cs`). Cela rend les controles "template-aware", mais pas vraiment lookless.
- Les templates sont donc aujourd'hui plus proches d'un `chrome applicator` que d'un systeme de skin complet. Changer `ControlTemplateName` a chaud peut reappliquer des defaults, mais pas remplacer proprement la topologie du controle.
- `ContentTemplate` et `MGElementTemplate` restent utiles pour le contenu, mais `XAML/Templates.cs` documente lui-meme une fragilite de precedence: `ApplyBaseSettings` est applique apres creation/customisation du template, avec une note disant que l'ordre ideal serait inverse. Cela expose un conflit potentiel entre config template et config locale.
- Les styles XAML ne sont pas un vrai moteur runtime. `Element.ProcessStyles(...)` applique via reflection au parsing, fusionne implicite/explicite/local, puis s'arrete. Ajouter/enlever un style apres creation d'un arbre n'entraine pas de re-style des elements existants (`MGUI.Core/UI/XAML/Element.cs`).
- La source d'une valeur n'est pas inspectable a l'execution. On connait le modele de precedence, mais il n'existe pas d'inspecteur de "resolved value" par propriete ni de trace standard disant si une couleur vient du theme, d'un style, d'une resource dynamique ou d'une valeur locale.
- `UIResourceReferenceApplicator` abonne chaque resource dynamique aux evenements `OnStaticResourceAdded/Changed/Removed` de tous les scopes ancetres, mais sans mecanisme symetrique de desabonnement lie au cycle de vie de l'element. Le risque est moins un bug fonctionnel immediat qu'une retention d'objets et une fan-out croissante sur longue session (`MGUI.Core/UI/Styling/UIResourceReferenceApplicator.cs`).

Constats de controle par controle:

- `MGComboBox<T>` reste fortement hybride. Il construit sa fenetre dropdown, son arrow presenter, son `MGScrollViewer`, son panel d'items, affecte `DropdownArrowColor`, `Dropdown.BackgroundBrush`, `Padding`, `MinHeight`, puis reapplique encore une partie du theme dans `OnThemeChanged(...)` (`MGUI.Core/UI/MGComboBox.cs`).
- `MGTabControl` a un `ControlTemplateName`, mais conserve des factories imperatives `SelectedTabHeaderTemplate` et `UnselectedTabHeaderTemplate` qui recreent des wrappers `MGButton` et les stylent via `ApplyDefaultSelectedTabHeaderStyle(...)` / `ApplyDefaultUnselectedTabHeaderStyle(...)` (`MGUI.Core/UI/MGTabControl.cs`).
- `MGTreeView` est intermediaire: les parts principales sont bien nommees et le template catalog injecte plusieurs valeurs, mais le controle conserve aussi `ApplyDefaultStyles()` et `OnThemeChanged(...)` pour repeupler ses valeurs de selection/bordure/indentation (`MGUI.Core/UI/MGTreeView.cs`).
- `MGTextBox` reste un controle largement code-first. Il combine bordure, grip, placeholder, compteur, caret, formatage de selection en markdown, et initialise plusieurs couleurs de selection depuis `MGTheme` sans couche template equivalent visible (`MGUI.Core/UI/MGTextBox.cs`).
- `MGToolTip` est encore directement theme-couple: bordure noire, padding, offset et foreground sont definis en constructeur a partir du theme sans template de chrome dedie (`MGUI.Core/UI/MGToolTip.cs`).
- `MGScrollViewer` reste custom draw/custom input par nature, mais il lit directement `ScrollBarOuterBrush` et `ScrollBarInnerBrush` depuis le theme et dessine les scrollbars dans `DrawSelf(...)`. C'est acceptable pour le runtime, mais ce n'est pas lookless (`MGUI.Core/UI/MGScrollViewer.cs`).

# Risques specifiques au contexte jeu video

- Le changement de theme global peut devenir couteux sur une UI dense. `MGElement.NotifyThemeChanged(...)` reapplique template, appelle `OnThemeChanged(...)`, calcule l'invalidation, puis descend recursivement dans le sous-arbre visuel et les composants. Pour un HUD simple c'est acceptable; pour un editeur embarque avec docking, listes et overlays, le fan-out devient significatif (`MGUI.Core/UI/MGElement.cs`).
- Le nombre de noeuds visuels decoratifs reste sous controle tant que les templates actuels restent compacts, mais les controles composites creent encore beaucoup de wrappers internes (`MGBorder`, `MGButton`, `MGContentPresenter`, `MGScrollViewer`, `MGStackPanel`). Sans budget explicite par controle, il est facile d'augmenter le cout de draw/layout en ajoutant du chrome.
- Certaines decorations font encore des allocations par frame, ce qui est un mauvais signal dans un moteur temps reel. Exemples observes: listes de vertices dans le dessin de la fleche du `MGComboBox`, de la fleche de sous-menu de `MGContextMenuItem`, et du checkmark de `MGCheckBox` (`MGUI.Core/UI/MGComboBox.cs`, `MGContextMenuItem.cs`, `MGCheckBox.cs`).
- Le pipeline de clipping reste correct conceptuellement, mais les controles composites qui introduisent `MGWindow`, `MGOverlay`, `MGScrollViewer` et overlays de docking multiplient les zones de clip potentielles. Sans discipline de structure, les skins peuvent vite surcharger scissor/stencil.
- Le support manette existe bien pour la navigation, mais certains controles et comportements restent encore implicitement "hover-first". `MGToolTip` est purement curseur/hover, et plusieurs highlights internes reposent sur `Hovered` ou sur des indicateurs de press/hover spoofes plutot que sur une projection uniforme de focus manette.
- L'invalidation layout sous changement de theme reste manuelle. Par defaut, `MGElement.GetThemeInvalidation(...)` renvoie `Draw`; seul `MGTextBlock` surcharge cette logique pour demander `Measure|Arrange|Draw` quand la police effective depend du theme (`MGUI.Core/UI/MGElement.cs`, `MGUI.Core/UI/MGTextBlock.cs`). Cela garde le pipeline leger, mais toute nouvelle propriete themee qui change la taille doit penser a surcharger manuellement l'invalidation.

# Risques specifiques au contexte editeur in-engine

- Le depot est mieux arme qu'avant pour un vrai theme editeur. `MGTheme.Docking`, les templates du docking et les scopes de ressources permettent deja d'imaginer un sous-arbre editeur avec identite visuelle propre sans dupliquer tout le code.
- En revanche, les variantes denses/compactes ne sont pas encore un concept de premier ordre. Beaucoup de tailles, paddings et wrappers restent poses dans les constructeurs de controle ou dans des helpers imperatifs; cela complique la creation d'un theme "Editor Compact" coherent.
- Le systeme de styles implicites/explicites est utile pour le XAML auteur, mais il manque un equivalent runtime pour restyler un sous-arbre deja charge. Pour un editeur in-engine qui alterne vues, themes et presets de densite, cela forcera sinon des refreshs plus grossiers ou des setters manuels.
- L'absence d'outillage de diagnostic de source de valeur sera plus penalisante en editeur qu'en HUD. Quand un controle complexe cumule theme, resource dynamique, style local et override code, on ne peut pas encore inspecter "pourquoi cette bordure vaut 1" ou "quel template a fourni cette marge".
- Les `ControlTemplate` actuels ameliorent le theming du docking, mais ils ne permettent pas encore de remplacer la structure de base d'un controle complexe pour un mode outillage. Cela bloque les variantes vraiment differentes sans recoder le controle ou sans multiplier les branches internes.
- `UIToolingService.CaptureVisualTree(...)` est un bon debut, mais il capture l'arbre visuel, pas la source des valeurs, pas les scopes de ressources, et pas les abonnements dynamiques. Pour un editeur, cela restera insuffisant pour deboguer des themes re-appliques a chaud.

# Analyse perf / invalidation / allocations

Hot paths potentiels:

- `MGElement.NotifyThemeChanged(...)` reparcourt tout le sous-arbre visuel et tous les composants sans granularite par propriete. C'est simple et robuste, mais large (`MGUI.Core/UI/MGElement.cs`).
- `MGScrollViewer` re-mesure le contenu a plusieurs endroits pour decider la visibilite et la taille des scrollbars, avec `CanCacheSelfMeasurement => false` (`MGUI.Core/UI/MGScrollViewer.cs`). Pour un controle scrollable dense d'editeur, c'est un hotspot clair.
- `MGTextBox.UpdateFormattedText(...)` reconstruit des chaines et insere du markup a chaque changement de selection/couleur de selection. C'est correct fonctionnellement, mais c'est un chemin couteux en edition intensive (`MGUI.Core/UI/MGTextBox.cs`).
- `MGTabControl.OnThemeChanged(...)` recree/re-synchronise les wrappers d'onglets via `UpdateHeaderWrapper(...)`, ce qui peut etre sensible sur des tabs nombreux (`MGUI.Core/UI/MGTabControl.cs`).

Allocations evitables:

- `UIResourceReferenceApplicator` cree des lambdas capturees pour chaque abonnement dynamique de chaque scope ancetre, sans dispose explicite (`MGUI.Core/UI/Styling/UIResourceReferenceApplicator.cs`).
- `MGComboBox` construit une `List<Vector2>` a chaque draw de la fleche dropdown (`MGUI.Core/UI/MGComboBox.cs`).
- `MGContextMenuItem` construit aussi une `List<Vector2>` pour la fleche de sous-menu (`MGUI.Core/UI/MGContextMenuItem.cs`).
- `MGCheckBox.DrawCheckMark(...)` alloue une `List<Vector2>` par dessin du checkmark (`MGUI.Core/UI/MGCheckBox.cs`).
- `MGTextBox` fait beaucoup de travail de chaine pour l'affichage de selection, en plus de caches HTML couleurs sur quatre proprietes de selection (`MGUI.Core/UI/MGTextBox.cs`).

Invalidations trop larges ou trop implicites:

- Changer `DefaultTheme` d'un scope notifie recursivement tout le sous-arbre sans separation stricte entre proprietes layout-affecting et render-only, hormis quelques surcharges manuelles (`MGUI.Core/UI/MGElement.cs`, `MGUI.Core/UI/MGTextBlock.cs`).
- Les styles ne se reappliquent pas a chaud, ce qui evite un cout runtime cache, mais reporte la complexite vers du code manuel ou des reloads entiers.
- `ApplyControlTemplate(true)` est execute a chaque theme refresh pour tout controle qui a un template. Comme le template actuel agit surtout comme un set de defaults, cela reste acceptable; si davantage de logique ou de structure y migre sans garde-fous, ce point deviendra un hotspot.

Cartographie layout/render observee:

- Les proprietes de police dans `MGTextBlock` sont explicitement reconnues comme layout-affecting sous changement de theme.
- Beaucoup d'autres valeurs themees restent traitees comme draw-only parce qu'elles sont affectees via setters ordinaires et callbacks manuels.
- La consequence est un pipeline leger aujourd'hui, mais fragile architecturalement: la stabilite depend de la vigilance manuelle de chaque nouveau controle.

# Problemes prioritaires

1. Le plus gros probleme architectural est l'ecart entre le modele de precedence formalise et la realite des proprietes runtime. Tant qu'il n'existe pas un petit moteur commun pour les proprietes exposees au theme/style/template, le comportement restera partiellement disperse et difficile a tracer.
2. Le deuxieme probleme est que `MGControlTemplate` n'est pas encore un vrai template de structure. Pour les controles composites, il ne remplace pas la structure et ne garantit pas une vraie separation logique/chrome.
3. Le troisieme probleme est la gestion des `DynamicResource`: elle marche fonctionnellement, mais sa gestion de cycle de vie est incomplete. Sur une longue session d'editeur, c'est la zone la plus credible pour des abonnements orphelins ou une pression memoire inutile.
4. Le quatrieme probleme est le nombre de controles encore hybrides. `ComboBox`, `TabControl`, `TextBox`, `ScrollViewer`, `ToolTip` et plusieurs widgets de base conservent du look dans leur logique. Cela complique le theming uniforme et la creation de variantes propres.
5. Le cinquieme probleme est l'absence d'outils de debug de source de valeur et de template. Pour un framework qui vise des ecrans in-game et des outils d'edition, c'est deja un manque important.

# Refactors recommandes

- Introduire un mini runtime de resolution de valeurs cible, pas un dependency property system complet. Limiter ce socle aux proprietes exposees au theme/style/template et aux proprietes layout-affecting. Garder les autres proprietes comme proprietes C# simples.
- Scinder conceptuellement le templating en deux niveaux. Niveau 1: conserver l'applicateur de chrome actuel, probablement a renommer ou a documenter comme tel. Niveau 2: introduire pour les gros controles un vrai template/factory structurel capable de produire ou remplacer la structure visuelle principale sans recoder le controle.
- Attacher les abonnements `DynamicResource` au cycle de vie de l'element, avec un conteneur disposable ou une table d'abonnements deregistrables.
- Etendre la notion d'invalidation semantique au-dela de `MGTextBlock`. L'ideal n'est pas plus de magie, mais un inventaire explicite des proprietes themees qui affectent `Draw`, `Measure`, `Arrange`, `Input`, `Navigation`.
- Sortir progressivement les tokens visuels des controles hybrides les plus sensibles: `ComboBox`, `TabControl`, `TextBox`, `ToolTip`, `ScrollViewer`. L'objectif n'est pas de les rendre "WPF-like", mais de faire passer leurs valeurs de chrome par une couche coherente et testable.
- Formaliser deux classes de valeurs themees: `render-only` et `layout-affecting`. Pour un framework jeu, cette distinction est plus importante qu'une abstraction lourde.
- Ajouter un outillage de diagnostic: source de valeur ressolue, scope de ressource effectif, template parts presentes/manquantes, compte d'abonnements dynamiques, dernier refresh theme/style applique.
- Conserver le modele `VisualState` actuel et l'etendre, plutot que reintroduire des couleurs codees dans les controles. C'est la bonne base pour souris, clavier et manette.

# Liste de petites taches candidates pour un agent IA

1. Ajouter un conteneur d'abonnements deregistrables pour `UIResourceReferenceApplicator`, puis le lier au cycle de vie de `MGElement.Metadata` ou a un hook de dispose logique.
2. Ajouter un test de non-duplication et de nettoyage des abonnements `DynamicResource` lors de destruction/remplacement d'un element.
3. Introduire une API de diagnostic `TryGetResolvedValueSource(element, propertyName)` pour un premier sous-ensemble de proprietes themees.
4. Etendre `UIToolingService` pour capturer, en plus de l'arbre visuel, le nom du scope de ressources effectif et les template parts enregistrees.
5. Ajouter un message d'erreur enrichi quand `MGControlTemplateContext.GetRequiredPart<T>(...)` echoue: template name, owner type, parts disponibles.
6. Convertir le dessin de fleche de `MGComboBox` en chemin allocation-free.
7. Convertir le dessin de fleche de `MGContextMenuItem` en chemin allocation-free.
8. Convertir `MGCheckBox.DrawCheckMark(...)` en chemin allocation-free.
9. Extraire de `MGToolTip` ses defaults de chrome vers un template de tooltip ou vers un sous-groupe `MGTheme.ToolTip` explicite.
10. Ajouter un `OnThemeChanged(...)` explicite a `MGTextBox` pour reappliquer proprement les quatre couleurs de selection quand le theme change.
11. Isoler dans `MGTextBox` les valeurs purement visuelles du coeur input/caret/selection afin de preparer un vrai template de chrome plus tard.
12. Faire migrer `MGComboBox` vers un modele ou le dropdown arrow, le dropdown chrome et les items utilisent des proprietes themees uniques plutot que des affectations dispersees constructeur + `OnThemeChanged(...)`.
13. Faire migrer `MGTabControl` pour que les wrappers d'onglets soient parametres par theme/template sans recreations systematiques de styles imperatifs.
14. Ajouter un petit moteur de valeurs ressolues pour 5 a 10 proprietes pilotes seulement: background, foreground, border brush, border thickness, padding, min height, margin.
15. Ajouter des tests d'architecture garantissant que certaines proprietes de theme ne sont jamais appliquees directement dans les constructeurs des controles composites cibles.
16. Introduire une convention `render-only` vs `layout-affecting` dans les groupes de settings de `MGTheme`, puis tester `GetThemeInvalidation(...)` en consequence.
17. Ajouter une API de refresh de style sur sous-arbre pour les styles implicites/explicites deja charges, sans reparsing complet du XAML.
18. Ajouter une vue debug qui affiche pour un element: `VisualState`, scope de ressources, template name, parts enregistrees, et origine des 3 a 5 proprietes visuelles principales.
19. Definir un preset "Editor Compact" en themes/resources/styles pour le docking, les tabs, les listes et les tooltips, afin de verifier les limites reelles du systeme actuel.
20. Documenter clairement que le `ControlTemplate` actuel est un applicateur de defaults sur parts existantes, afin d'eviter que d'autres developpements supposent deja un systeme lookless complet.