# Taches styling / theme

## Objectif

Executer les travaux restants du systeme style/theme/template de MGUI: hygiene runtime des abonnements dynamic resource, diagnostics de source de valeur, convention d'invalidation, refresh de styles a chaud, migration structurelle des controles docking, modelisation des composites a surfaces auxiliaires, et validation du systeme par un preset editeur.

Contexte d'architecture: `Docs/styling-theme-architecture.md`. Contraintes non negociables: pas de dependency property system complet a la WPF; `MGControlTemplate`, `MGResources` et `ThemeDefinition` restent les briques centrales; les templates XAML sont des assets de definition, jamais du code execute a chaque draw; la recreation de structure n'est jamais un hot path de frame; les templates code restent possibles pour les cas perf-sensibles.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- Mettre a jour le statut de chaque tache dans ce fichier (⚪ -> 🟡 -> ✅).
- Faire un commit git par tache terminee; ne pas regrouper plusieurs taches dans un commit.
- Si une tache est bloquee, la marquer ⛔, decrire le blocage juste sous la tache, puis s'arreter.
- Ne pas faire de refactor hors perimetre.
- Ajouter ou adapter des tests a chaque tache qui change un comportement runtime ou declaratif.
- Preserver la compatibilite descendante sauf demande explicite.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

- `dotnet build MGUI.Core/MGUI.Core.csproj`
- `dotnet test MGUI.Tests/MGUI.Tests.csproj --filter FullyQualifiedName~Architecture --no-restore`
- Pour les taches docking, ajouter: `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-restore`

## Taches

### ⚪ 1. Rendre les abonnements dynamic resource deregistrables et lies au cycle de vie

But:
eliminer la retention d'objets et le fan-out croissant des abonnements `DynamicResource` sur longue session.

Travail attendu:

- `MGUI.Core/UI/Styling/UIResourceReferenceApplicator.cs` (lignes 86-88) abonne des lambdas a `OnStaticResourceAdded/Changed/Removed` sur chaque scope ancetre, sans aucun chemin de desinscription (aucun `Dispose`/`-=` dans le fichier);
- introduire un conteneur d'abonnements deregistrable, rattache au cycle de vie de l'element (metadata de `MGElement` ou hook de dispose logique);
- desinscrire lors de la destruction/remplacement de l'element ou de la re-application des references;
- etendre `MGUI.Tests/Architecture/ResourceReferenceApplicatorTests.cs`: non-duplication des abonnements en cas de re-application, et nettoyage effectif apres destruction/remplacement.

Criteres d'acceptation:

- plus aucun handler orphelin sur les scopes apres retrait d'un element;
- une re-application ne duplique pas les abonnements;
- les tests dynamic resource existants restent verts.

Commit recommande: `style-theme: add deregistrable dynamic resource subscriptions`

### ⚪ 2. Ajouter une API de diagnostic TryGetResolvedValueSource

But:
pouvoir repondre a "d'ou vient cette valeur" pour un premier sous-ensemble de proprietes.

Travail attendu:

- aucune API de ce type n'existe (grep repo: aucun `TryGetResolvedValueSource`);
- exposer `TryGetResolvedValueSource(element, propertyName)` retournant les metadonnees `UIValueResolutionSource`;
- s'appuyer d'abord sur ce qui existe deja: les valeurs template sont stockees en `UIResolvedValue<T>` dans `MGElement` (via `MGControlTemplateContext.ApplyTemplateValue(...)` et `TryGetAppliedTemplateDefault`); completer par une detection theme/local pour les proprietes pilotes;
- documenter explicitement le sous-ensemble couvert;
- tests unitaires sur les sources `Template`, `Theme` et `LocalValue` du sous-ensemble.

Criteres d'acceptation:

- une valeur appliquee par template est identifiee comme source `Template` avec son invalidation;
- le perimetre couvert et non couvert est explicite;
- pas de cout runtime hors appel de diagnostic.

Commit recommande: `style-theme: add resolved value source diagnostic`

### ⚪ 3. Capturer le scope de ressources effectif dans le snapshot d'outillage

But:
completer l'outillage editeur pour deboguer les themes re-appliques a chaud.

Travail attendu:

- `MGUI.Core/Tooling/UIVisualTreeSnapshot.cs` expose `AppliedControlTemplate`, `TemplateParts` et `LastControlTemplateError`, mais aucun champ de scope de ressources;
- etendre `UIToolingService.CaptureVisualTree(...)` pour capturer la categorie `UIResourceScope` effective (`Desktop`/`Window`/`Subtree`/`Template`) et une identite de scope exploitable;
- tests d'outillage sur un arbre avec scope local (`MGElement.EnsureResourceScope(...)`).

Criteres d'acceptation:

- le snapshot indique le scope effectif de chaque element;
- un scope local materialize est distinguable du fallback desktop;
- la surface de snapshot existante reste compatible.

Commit recommande: `style-theme: capture resource scope in tooling snapshot`

### ⚪ 4. Ajouter une vue debug par element

But:
donner une vue de diagnostic unique par element pour l'editeur.

Travail attendu:

- s'appuie sur les taches 2 et 3;
- exposer pour un element: `VisualState` (deja capture), scope de ressources, nom du template applique et parts enregistrees (deja captures), et l'origine des 3 a 5 proprietes visuelles principales (background, foreground, border, padding) via `TryGetResolvedValueSource`;
- forme libre: extension de `UIVisualTreeSnapshot` ou service de formatage dedie dans `MGUI.Core/Tooling`.

Criteres d'acceptation:

- un seul appel permet de repondre a "pourquoi cette bordure vaut 1" pour les proprietes couvertes;
- la vue fonctionne sur un controle template (fenetre, combo box) et sur un element simple;
- tests sur la composition de la vue.

Commit recommande: `style-theme: add per element debug view`

### ⚪ 5. Introduire la convention render-only vs layout-affecting et tester GetThemeInvalidation

But:
rendre explicite quelles valeurs themees affectent le layout, au lieu de compter sur la vigilance manuelle.

Travail attendu:

- etat actuel: `MGElement.GetThemeInvalidation(...)` renvoie `Draw` par defaut et seule `MGTextBlock` le surcharge; aucune classification n'existe dans `MGTheme`;
- classer les groupes de settings de `MGTheme` en deux categories: `render-only` (brushes, couleurs) et `layout-affecting` (paddings, min heights, tailles de police, marges);
- surfacer cette classification (attributs, metadata ou convention documentee dans le code) et surcharger `GetThemeInvalidation(...)` la ou des valeurs layout-affecting circulent par le theme;
- tests d'architecture verifiant que les controles consommant des valeurs layout-affecting du theme demandent bien `Measure`/`Arrange` lors d'un changement de theme.

Criteres d'acceptation:

- l'inventaire render-only vs layout-affecting est explicite et teste;
- un changement de theme qui modifie une taille invalide le layout des controles concernes;
- pas de sur-invalidation: les valeurs render-only restent `Draw` seul.

Commit recommande: `style-theme: classify themed values by invalidation impact`

### ⚪ 6. Ajouter une API de refresh de styles sur sous-arbre

But:
permettre de re-styler un sous-arbre deja charge sans reparser tout le XAML.

Travail attendu:

- etat actuel: les styles s'appliquent uniquement pendant le parsing via `Element.ProcessStyles(...)` (`MGUI.Core/UI/XAML/Element.cs`, appele depuis `MGUI.Core/UI/XAML/XAMLParser.cs`); aucune API `RefreshStyles`/`RestyleSubtree` n'existe;
- concevoir une API de re-application des styles implicites/explicites sur un sous-arbre runtime existant;
- respecter la precedence: ne pas ecraser les valeurs locales, bindings, ni les proprietes explicitement posees dans le XAML d'origine (mecanisme `ExplicitlySetProperties`);
- limiter les invalidations au strict necessaire selon `UIInvalidationKind`;
- tests sur: ajout d'un style apres chargement, override par scope, non-ecrasement des valeurs locales.

Criteres d'acceptation:

- un style ajoute a un scope peut etre applique a un sous-arbre existant sans reparse;
- aucune valeur locale ou explicitement posee n'est ecrasee;
- le cout est borne au sous-arbre cible.

Commit recommande: `style-theme: add subtree style refresh api`

### ⚪ 7. Construire un mini moteur de valeurs resolues pour des proprietes pilotes

But:
reduire l'ecart entre le modele `UIValuePrecedence` et la realite runtime, sans dependency property system complet.

Travail attendu:

- etat actuel: seules les valeurs appliquees par template portent un `UIResolvedValue<T>` (store `_AppliedTemplateDefaults` de `MGElement`); il n'existe aucun store par propriete couvrant les sources local/style/theme;
- introduire un petit store de valeurs resolues pour 5 a 10 proprietes pilotes seulement: background, foreground, border brush, border thickness, padding, min height, margin;
- chaque ecriture porte sa source (`UIValueResolutionSource`) et son `UIInvalidationKind`; la lecture retourne le gagnant selon `UIValuePrecedence`;
- garder toutes les autres proprietes comme proprietes C# simples;
- brancher `TryGetResolvedValueSource` (tache 2) sur ce store pour les proprietes pilotes;
- tests de precedence croisant local, template, theme et dynamic resource sur les proprietes pilotes.

Criteres d'acceptation:

- la precedence observee sur les proprietes pilotes correspond exactement a `UIValuePrecedence`;
- aucun cout mesurable sur les proprietes non pilotes;
- le perimetre pilote est documente dans le code.

Commit recommande: `style-theme: add pilot resolved value engine`

### ⚪ 8. Isoler le chrome restant de MGTextBox du moteur d'edition

But:
preparer un template de chrome complet pour `MGTextBox` sans reecrire le moteur d'edition.

Travail attendu:

- etat actuel: les couleurs de selection, le padding et la min height passent deja par le template `TextBox.Default` (`MGControlTemplateCatalog.cs`, application des quatre couleurs de selection via `ApplyThemeDefault`), et les parts border/text block/placeholder/compteur/resize grip sont declarees; mais la composition placeholder/compteur/texte/caret reste fortement coordonnee dans le code de `MGUI.Core/UI/MGTextBox.cs`;
- separer les valeurs purement visuelles restantes (marges internes, brushes residuels, dispositions de placeholder/compteur) de la logique input/caret/selection;
- faire passer ces valeurs par le catalogue ou l'asset XAML, en respectant la regle du backing field pour toute propriete pilotant une part;
- ne pas reouvrir le moteur d'edition texte ni le formatage de selection.

Criteres d'acceptation:

- les valeurs visuelles restantes de `MGTextBox` sont template/theme-owned;
- comportement clavier/souris, caret et selection inchanges (tests cibles `--filter FullyQualifiedName~TextBox`);
- la frontiere comportement/chrome est documentee dans le code.

Commit recommande: `style-theme: isolate textbox chrome from edit engine`

### ⚪ 9. Converger le vocabulaire des parts docking

But:
figer un vocabulaire de parts unique avant la migration structurelle docking.

Travail attendu:

- le code a partiellement adopte des noms differents de la cible initiale; etat reel des constantes (`MGUI.Core/UI/Docking/Controls/*.cs`):
  - `MGDockAutoHideDrawer`: `PART_Border`, `PART_Header`, `PART_TitleLabel`, `PART_PinButton`, `PART_CloseButton`, `PART_PinIcon`, `PART_CloseIcon`, `PART_ResizeGrip` (cible: roles partages `PART_HeaderText` et `PART_Grip`);
  - `MGDockTabItem`: `PART_Surface`, `PART_Accent`, `PART_CloseIcon`, `PART_PinIcon` (cible: `PART_TabHeader`, `PART_TabTitle`, `PART_TabCloseButton`);
  - `MGDockDropIndicators`: aucune part enregistree (cible: `PART_Overlay` + `PART_LeftDropZone`/`PART_RightDropZone`/`PART_TopDropZone`/`PART_BottomDropZone`/`PART_CenterDropZone`);
  - `MGDockSplitterBar` (`PART_Surface`/`PART_Accent`/`PART_Grip`) et `MGDockPreviewOverlay` (`PART_Surface`/`PART_Border`) sont deja conformes;
- decider pour chaque divergence: blesser le nom livre ou renommer vers le role partage; appliquer la decision de maniere coherente (constantes, templates `Dock.*` du catalogue, variantes `Dark.Dock*` de `BuiltInControlTemplates.xaml`);
- regles a maintenir: nommage `PART_*`; pas d'alias par controle quand un role partage existe; les etats semantiques docking restent portes par des proprietes; comportement et orchestration de layout restent sur le controle proprietaire;
- mettre a jour la section docking de `Docs/styling-theme-architecture.md` avec le vocabulaire fige.

Criteres d'acceptation:

- chaque controle docking a un jeu de parts nomme conforme au vocabulaire fige;
- aucun renommage ne casse les templates catalogue ni les variantes XAML `Dark.Dock*`;
- la doc d'architecture reflete l'etat fige.

Commit recommande: `style-theme: converge docking part vocabulary`

### ⚪ 10. Migrer les controles docking feuilles vers des templates structurels

But:
donner aux controles docking le meme contrat structurel que `TreeView`/`TextBox`, au lieu de simples applicateurs de defaults.

Travail attendu:

- etat actuel: les controles docking posent `DefaultControlTemplateName` sur les noms catalogue (`Dock.TabItem.Default`, `Dock.Splitter.Default`, etc.) et enregistrent des parts `PART_*`, mais AUCUN controle docking ne surcharge `GetRequiredControlTemplateParts()` ni `AttachControlTemplateStructure(...)`, et `MGControlTemplateCatalog` enregistre les cinq templates `Dock.*` via la forme applicateur de defaults `Register(Resources, DockTabItemTemplateName, ApplyDockTabItemTemplate)` (lignes ~170-174), sans phase de creation de structure;
- migrer d'abord les visuels feuilles: `MGDockSplitterBar`, `MGDockDropIndicators`, `MGDockPreviewOverlay`, `MGDockAutoHideStrip`;
- pour chaque controle: declarer les parts requises, extraire la creation de structure vers un createur `Create*TemplateStructure` (modele: `TreeView.Default` / `TextBox.Default` dans le catalogue), attacher via `AttachControlTemplateStructure(...)`, appliquer les regles de robustesse (backing fields, un seul proprietaire par part);
- valider les interactions docking sensibles (drag de splitter, affichage des zones de drop, preview, strip auto-hide).

Criteres d'acceptation:

- les quatre controles feuilles consomment une structure de template au runtime;
- les comportements docking critiques restent stables;
- tests d'infrastructure etendus (`--filter FullyQualifiedName~ControlTemplateInfrastructureTests`) et build des samples vert.

Commit recommande: `style-theme: migrate docking leaf structural templates`

### ⚪ 11. Migrer les tabs docking et les surfaces composites vers des templates structurels

But:
terminer la migration structurelle docking sur les controles a plus forte orchestration.

Travail attendu:

- suite de la tache 10, dans l'ordre: `MGDockTabItem` et `MGDockTabGroup`, puis `MGDockAutoHideDrawer`, puis les surfaces `MGDockHost` (preview overlay, drop indicators, strips, drawer — parts hote deja declarees);
- meme contrat que la tache 10: parts requises, createur de structure, attachement runtime, defaults dans le catalogue;
- utiliser `DetachedRoots` pour les parts hors sous-arborescence unique quand necessaire;
- l'orchestration du host (etat docking, hit-testing, layout) reste comportementale;
- si un controle ne peut pas exprimer un visuel necessaire avec le vocabulaire fige en tache 9, s'arreter (⛔) et demander une revue au lieu d'etendre le pattern.

Criteres d'acceptation:

- les controles docking migres declarent leurs parts et consomment un template structurel;
- interactions de docking (drag/drop d'onglets, auto-hide, floating) stables;
- le backlog docking residuel est explicitement liste s'il en reste.

Commit recommande: `style-theme: migrate docking tab and composite structural templates`

### ⚪ 12. Modeliser les controles composites a overlays ou fenetres auxiliaires

But:
etendre la migration aux controles qui pilotent des overlays, popups ou fenetres auxiliaires, la ou un sous-arbre visuel unique ne suffit pas.

Travail attendu:

- aucun modele n'existe pour cette famille: `MGUI.Core/UI/Docking/Controls/MGFloatingDockWindow.cs` n'a aucun hook structurel, et le dropdown de `MGComboBox` reste une fenetre construite par le controle;
- inventorier les controles concernes (`MGComboBox` dropdown, `MGContextMenu`, `MGFloatingDockWindow`, tooltips detaches) et distinguer ceux couverts par le contrat actuel (`DetachedRoots`) de ceux exigeant un point d'extension;
- formaliser la frontiere entre structure templatee, composants detaches et surfaces auxiliaires: cycle de vie, parentage, fermeture;
- migrer un premier controle representatif si le contrat actuel suffit; sinon produire la specification d'extension minimale;
- documenter le resultat dans `Docs/styling-theme-architecture.md`.

Criteres d'acceptation:

- la strategie de migration de cette famille est explicite;
- au moins un cas representatif est borne par du code ou une spec exploitable;
- ouverture, attachement et fermeture des surfaces auxiliaires sont valides par des tests cibles.

Commit recommande: `style-theme: model auxiliary surface composites`

### ⚪ 13. Definir un preset "Editor Compact"

But:
verifier les limites reelles du systeme theme/ressources/styles sur un cas editeur dense.

Travail attendu:

- aucun preset de ce type n'existe (grep repo: aucun `EditorCompact`);
- definir un preset compact en themes/resources/styles pour le docking, les tabs, les listes et les tooltips: tailles, paddings et densites reduits;
- l'implementer uniquement avec les mecanismes declaratifs existants (`ThemeDefinition` avec `BasedOn`, `ControlTemplates`, `Properties`, styles scopes) — sans nouvelle API;
- consigner chaque valeur impossible a piloter declarativement: c'est le livrable principal (limites reelles du systeme);
- exposer le preset dans `MGUI.Samples` (par exemple a cote de `MGUI.Samples/Features/StyleThemeRefactor.xaml`).

Criteres d'acceptation:

- le preset s'applique par simple changement de theme sur un scope;
- la liste des valeurs non pilotables declarativement est documentee et versee comme nouvelles taches si pertinent;
- aucun changement de code de controle n'est necessaire pour appliquer le preset.

Commit recommande: `style-theme: add editor compact preset`
