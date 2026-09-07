# Taches styling / theme

## Objectif

Executer les travaux restants du systeme style/theme/template de MGUI: hygiene runtime des abonnements dynamic resource, diagnostics de source de valeur, convention d'invalidation, refresh de styles a chaud, migration structurelle des controles docking (y compris la seconde vague reprise de `roadmap-tasks.md`), modelisation des composites a surfaces auxiliaires, et validation du systeme par un preset editeur.

Contexte d'architecture: `Docs/styling-theme-architecture.md`. Contraintes non negociables: pas de dependency property system complet a la WPF; `MGControlTemplate`, `MGResources` et `ThemeDefinition` restent les briques centrales; les templates XAML sont des assets de definition, jamais du code execute a chaque draw; la recreation de structure n'est jamais un hot path de frame; les templates code restent possibles pour les cas perf-sensibles.

## Historique du fichier

- 2 septembre 2026: creation par consolidation des audits precedents (commit `6c6d9ae`).
- 7 septembre 2026: correction d'apres un audit du code (HEAD `abe99f5`, lecture seule, contre-verification independante). Ordre des taches reordonne selon les dependances reelles, sections "Etat actuel" corrigees ou completees avec les references verifiees, tache 1 dotee de sa decision de conception, taches 9 et 12 fusionnees avec les taches 1 et 2 de `Docs/Tasks/roadmap-tasks.md` (qui ne garde plus qu'un renvoi). Aucune tache n'avait ete commencee au moment de la correction.

Correspondance avec la numerotation du 2 septembre: 1<-1, 2<-3, 3<-9, 4<-7, 5<-2, 6<-4, 7<-5, 8<-10, 9<-11, 10<-6, 11<-8, 12<-12, 13<-13.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre de ce fichier.
- Mettre a jour le statut de chaque tache dans ce fichier (⚪ -> 🟡 -> ✅).
- Faire un commit git par tache terminee; ne pas regrouper plusieurs taches dans un commit.
- Si une tache est bloquee, la marquer ⛔, decrire le blocage juste sous la tache, puis s'arreter.
- Ne pas faire de refactor hors perimetre.
- Ajouter ou adapter des tests a chaque tache qui change un comportement runtime ou declaratif.
- Preserver la compatibilite descendante sauf demande explicite.
- Rattacher chaque validation docking ou theme a un identifiant `SCN-*` de `Docs/scenario-validation-index.md`.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

- `dotnet build MGUI.Core/MGUI.Core.csproj`
- `dotnet test MGUI.Tests/MGUI.Tests.csproj --filter FullyQualifiedName~Architecture --no-restore`
- Pour les taches docking (3, 8, 9), ajouter: `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-restore`, les tests `MGUI.Tests/Docking` et le scenario `SCN-DOCK-001` (sample `MGUI.Samples/Features/DockingDemo.cs`).
- Pour les taches 12 et 13, ajouter le scenario `SCN-THEME-001`: `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template"`.

## Taches

### ✅ 1. Rendre les abonnements dynamic resource deregistrables et lies au cycle de vie

**Statut** : livre le 7 septembre 2026. `MGResources.OnStaticResourceLookupChanged` (interne, une levee par mutation, forwarde parent -> enfant par `WeakParentScopeForwarder`, ex-`WeakThemeChangedForwarder`, cable dans `SetParent`) ; conteneur `UIDynamicResourceSubscriptions` (`MGUI.Core/UI/Styling/`) par element hote, un seul handler sur le scope le plus proche, detache et rattache sur `OnParentChanged` (un element retire de son parent reste detache, sans repli sur le scope de sa fenetre), `Detach`/`Clear` internes ; signature de `Apply` inchangee. Tests : quatre ajouts dans `MGUI.Tests/Architecture/ResourceReferenceApplicatorTests.cs` (non-duplication, detachement au retrait, re-resolution au reparentage, forwarding parent -> enfant et arret au `SetParent(null)`) et `MGUI.Tests/Input/ResourceReferenceLifetimeRegressionTests.cs` (fenetre fermee collectable, propagation vers une fenetre fermee puis re-montree). Mutation check : `Detach()` neutralise fait echouer le test de retrait, retour au vert apres reversion. Limites relevees : un scope local cree tardivement sur un ancetre n'est suivi qu'au prochain changement de parent ; `MGTabControl.RemoveTab` ne remettait pas `Parent` a null sur l'onglet retire (ecart preexistant corrige le 7 septembre 2026 : `SetParent(null)` au retrait, tests `MGUI.Tests/Controls/MGTabControlRemoveTabTests.cs`).

But:
eliminer la retention d'objets et le fan-out croissant des abonnements `DynamicResource` sur longue session.

Etat actuel (verifie le 7 septembre 2026):

- `MGUI.Core/UI/Styling/UIResourceReferenceApplicator.cs` (lignes 84-89) abonne trois lambdas a `OnStaticResourceAdded/Changed/Removed` sur chaque scope ancetre; aucun chemin de desinscription (aucun `Dispose`/`-=` dans le fichier);
- une deduplication existe deja (`RegisterDynamicSubscription`, lignes 51-74): `HashSet<string>` dans `HostElement.Metadata["DynamicResourceSubscriptions"]`, cle = hash de la cible | `TargetPath` | `ResourceName`. Elle n'est pas testee, la cle ignore le scope et l'ensemble n'est jamais vide: un hote reparente est ignore au lieu d'etre re-abonne, et la closure `Refresh` re-applique contre le scope capture a l'enregistrement, pas contre le scope courant;
- le seul site de production est `MGUI.Core/UI/XAML/Element.cs:571`, appele pendant la construction XAML, donc en general avant que l'element soit rattache a son parent;
- `MGElement` n'a aucun hook de dispose; seul `OnParentChanged` (`MGElement.cs:657`, leve par `SetParent`) signale l'entree ou la sortie de l'arbre;
- precedent a respecter: `MGResources.WeakThemeChangedForwarder` (`MGUI.Core/UI/MGResources.cs:127-156`). Fermer une `MGWindow` n'est pas la detruire: un abonnement fort sur le scope desktop enracine toute fenetre fermee, et desinscrire a la fermeture casse la propagation vers les fenetres re-montrees. Le lien faible y est volontairement limite a ce seul point scope -> scope ("pas de weak events generalises");
- toute fenetre possede un scope `Window` dont le parent est `MGDesktop.Resources` (`MGWindow.cs:1101`, `:1388`); les evenements de ressources statiques ne sont pas forwardes du parent vers l'enfant, seul `TryGetStaticResource` remonte la chaine.

Decision (7 septembre 2026): les abonnements suivent l'appartenance a l'arbre, et aucun element ne s'abonne directement a un scope ancetre. Decisions: see ADR-0001 (`Docs/decisions/0001-dynamic-resource-subscription-lifecycle.md`).

Travail attendu:

- `MGResources`: ajouter un evenement interne `OnStaticResourceLookupChanged` (nom de ressource) leve une fois par mutation dans `AddStaticResource`/`SetStaticResource`/`RemoveStaticResource`, et forwarde du parent vers l'enfant par le meme lien faible que le theme (extension du forwarder existant, cable/decable dans `SetParent`). Les trois evenements existants gardent leur semantique self-only;
- applicateur: remplacer le `HashSet` par un conteneur d'abonnements interne par element hote (meme cle `Metadata`), entrees dedupliquees par (cible, `TargetPath`, `ResourceName`), UN handler sur le scope le plus proche seulement (jamais sur les ancetres), re-application contre le scope courant; hook `OnParentChanged` pose une seule fois: detacher de l'ancien scope, rattacher au nouveau et re-appliquer toutes les entrees; `Detach`/`Clear` internes pour un teardown explicite futur; chemin `HostElement == null` (tests uniquement) conserve sans conteneur;
- signature de `Apply(...)` inchangee;
- etendre `MGUI.Tests/Architecture/ResourceReferenceApplicatorTests.cs`: non-duplication a la re-application (une seule re-application par changement), detachement au retrait de l'element (liste d'invocation du scope revenue a sa longueur initiale), re-resolution au reparentage sous un scope qui surcharge la ressource, forwarding parent -> enfant et arret apres `SetParent(null)`; ajouter un test de duree de vie sur le modele de `MGUI.Tests/Input/InputLifetimeRegressionTests.cs` (fenetre fermee non enracinee par le scope desktop, propagation encore recue apres fermeture puis re-affichage);
- verifier en lecture seule que les chemins usuels de retrait d'un enfant appellent bien `SetParent(null)`; signaler les chemins qui ne le font pas sans les corriger ici.

Criteres d'acceptation:

- plus aucun handler orphelin sur les scopes apres retrait d'un element;
- une re-application ne duplique pas les abonnements;
- le scope desktop ne reference aucun element via ces abonnements (seul le lien faible scope -> scope existe);
- les quatre tests dynamic resource existants restent verts sans modification.

Limite connue a documenter: la creation tardive d'un scope local sur un ancetre (`EnsureResourceScope` apres construction) n'est pas suivie tant que l'element ne change pas de parent.

Commit recommande: `style-theme: add deregistrable dynamic resource subscriptions`

### ✅ 2. Capturer le scope de ressources effectif dans le snapshot d'outillage

**Statut** : livre le 7 septembre 2026. `UIVisualTreeSnapshot` expose `ResourceScope` (categorie du scope retourne par `GetResources()`), `ResourceScopeOwnerDiagnosticId` (id diagnostic stable de l'element proprietaire du scope, de la desktop pour le scope racine, null si aucun proprietaire dans la chaine `Parent` puis `ParentWindow`) et `HasLocalResourceScope` ; membres inseres apres `LastControlTemplateError`, surface existante intacte ; le rendu texte du snapshot reprend les trois valeurs. Tests : quatre ajouts dans `MGUI.Tests/Tooling/StableDiagnosticIdTests.cs` (fenetre proprietaire et enfant heritant, scope `Subtree` materialise distingue de ses enfants, popup avec son propre scope, artefact texte) et les trois proprietes ajoutees a `ToolingHooksTests`. Mutation check : proprietaire force a null fait echouer trois tests, vert apres reversion. Constats : `UIResourceScope.Template` n'est construit nulle part dans le code ; le scope desktop n'est jamais le scope effectif d'un element capturable, toute `MGWindow` materialisant son scope `Window` a la construction (branche implementee, non exercee par un arbre vivant).

But:
completer l'outillage editeur pour deboguer les themes re-appliques a chaud.

Etat actuel (verifie le 7 septembre 2026):

- `MGUI.Core/Tooling/UIVisualTreeSnapshot.cs` expose `AppliedControlTemplate`, `TemplateParts` et `LastControlTemplateError`, mais aucun champ de scope de ressources;
- les primitives necessaires existent deja: `UIResourceScope`, `MGElement.LocalResources`, `GetResources()`, `EnsureResourceScope(...)`;
- `UIVisualTreeSnapshot` est un record positionnel public construit uniquement par `UIToolingService` (`CreateSnapshot`/`CreateWindowSnapshot`): ajouter un champ est source-compatible dans le depot, a signaler dans le message de commit pour d'eventuels consommateurs externes.

Travail attendu:

- etendre `UIToolingService.CaptureVisualTree(...)` pour capturer la categorie `UIResourceScope` effective (`Desktop`/`Window`/`Subtree`/`Template`) et une identite de scope exploitable;
- tests d'outillage sur un arbre avec scope local (`MGElement.EnsureResourceScope(...)`).

Criteres d'acceptation:

- le snapshot indique le scope effectif de chaque element;
- un scope local materialize est distinguable du fallback desktop;
- la surface de snapshot existante reste compatible.

Commit recommande: `style-theme: capture resource scope in tooling snapshot`

### ✅ 3. Converger le vocabulaire des parts docking

**Statut** : livre le 7 septembre 2026 (decisions de l'auteur, ADR-0002 `Docs/decisions/0002-docking-part-vocabulary.md`). Regle retenue : un role partage par le framework prime sur tout alias par controle ; les noms de l'ancienne cible (`PART_HeaderText`, `PART_Grip` comme poignee de redimensionnement, `PART_TabHeader`/`PART_TabTitle`/`PART_TabCloseButton`, `PART_Overlay`) n'existaient nulle part et sont abandonnes. Appliquee : `MGDockAutoHideDrawer` `PART_Header` -> `PART_TitleBar` et `PART_TitleLabel` -> `PART_TitleBarText` (roles de `MGWindow`), anciens identifiants `HeaderPartName`/`TitleLabelPartName` conserves en alias obsoletes portant les nouvelles valeurs, seule reference catalogue mise a jour ; `MGDockTabItem` enregistre en plus `PART_TitleText`, `PART_CloseButton`, `PART_PinButton` ; `MGDockDropIndicators` enregistre ses neuf zones (`PART_LeftDropZone` ... `PART_CenterDropZone`, `PART_HostLeftDropZone` ... `PART_HostBottomDropZone`), sans `PART_Overlay` ; `MGDockTabGroup` enregistre en plus `PART_HeadersPanel` (role de `MGTabControl`). Splitter, preview overlay, strip et host inchanges. Aucun comportement modifie. Tests : `MGUI.Tests/Docking/DockPartVocabularyTests.cs` (valeurs figees de chaque controle, enregistrements sur controles vivants, alias obsoletes). Mutation check : `PART_TitleBar` remis a `PART_Header` fait echouer le pin et le test d'enregistrement du drawer, vert apres reversion. La section docking de `Docs/styling-theme-architecture.md` decrit le vocabulaire fige.

But:
figer un vocabulaire de parts unique avant la migration structurelle docking.

Etat actuel (verifie le 7 septembre 2026, `MGUI.Core/UI/Docking/Controls/*.cs`):

- `MGDockAutoHideDrawer`: `PART_Border`, `PART_Header`, `PART_TitleLabel`, `PART_PinButton`, `PART_CloseButton`, `PART_PinIcon`, `PART_CloseIcon`, `PART_ResizeGrip` (cible initiale: roles partages `PART_HeaderText` et `PART_Grip`);
- `MGDockTabItem`: `PART_Surface`, `PART_Accent`, `PART_CloseIcon`, `PART_PinIcon` (cible initiale: `PART_TabHeader`, `PART_TabTitle`, `PART_TabCloseButton`);
- `MGDockTabGroup`: `PART_Accent`, `PART_DropdownIcon`, `PART_WindowStateIcon` (`MGDockTabGroup.cs:22-24`, enregistrees a `:298`, `:319`, `:341`), chrome construit sans template;
- `MGDockDropIndicators`: aucune part enregistree (cible: `PART_Overlay` + `PART_LeftDropZone`/`PART_RightDropZone`/`PART_TopDropZone`/`PART_BottomDropZone`/`PART_CenterDropZone`);
- `MGDockSplitterBar` (`PART_Surface`/`PART_Accent`/`PART_Grip`) et `MGDockPreviewOverlay` (`PART_Surface`/`PART_Border`) sont deja conformes;
- `MGDockHost` enregistre ses parts hote (preview overlay, drop indicators, strips, drawer auto-hide); `MGDockSplitContainer` n'en declare aucune;
- le vocabulaire cible n'apparait nulle part dans le code; `Docs/styling-theme-architecture.md` (lignes 331-348) decrit l'etat divergent sans trancher;
- des tests d'infrastructure assertent le texte source des constantes: ils devront suivre tout renommage.

Travail attendu:

- decider pour chaque divergence: garder le nom livre ou renommer vers le role partage; appliquer la decision de maniere coherente (constantes, templates `Dock.*` du catalogue, variantes `Dark.Dock*` de `BuiltInControlTemplates.xaml`);
- regles a maintenir: nommage `PART_*`; pas d'alias par controle quand un role partage existe; les etats semantiques docking restent portes par des proprietes; comportement et orchestration de layout restent sur le controle proprietaire;
- mettre a jour la section docking de `Docs/styling-theme-architecture.md` avec le vocabulaire fige (et non plus le releve de l'etat divergent).

Criteres d'acceptation:

- chaque controle docking a un jeu de parts nomme conforme au vocabulaire fige;
- aucun renommage ne casse les templates catalogue ni les variantes XAML `Dark.Dock*`;
- la doc d'architecture reflete l'etat fige.

Commit recommande: `style-theme: converge docking part vocabulary`

### ⚪ 4. Construire un mini moteur de valeurs resolues pour des proprietes pilotes

But:
reduire l'ecart entre le modele `UIValuePrecedence` et la realite runtime, sans dependency property system complet.

Etat actuel (verifie le 7 septembre 2026):

- seules les valeurs appliquees par template portent un `UIResolvedValue<T>` (store `_AppliedTemplateDefaults` de `MGElement`, via `MGControlTemplateContext.ApplyTemplateValue(...)` et `TryGetAppliedTemplateDefault`); il n'existe aucun store par propriete couvrant les sources local/style/theme;
- `UIResolvedValue<T>`, `UIValueResolutionSource` (dix sources ordonnees par `UIValuePrecedence`) existent depuis mars 2026 et suffisent comme briques: pas de redesign necessaire;
- les quatre chemins disperses (styles XAML au parse, callbacks `OnThemeChanged`, setters C# ordinaires, chemin template) sont decrits dans `Docs/styling-theme-architecture.md` (lignes 71-78);
- `ApplyTemplateValue` possede deja un garde has-previous/equals pour ne pas ecraser un override local au refresh de theme (`MGControlTemplate.cs:97-101`): cette interaction est delicate.

Travail attendu:

- introduire un petit store de valeurs resolues pour 5 a 10 proprietes pilotes seulement: background, foreground, border brush, border thickness, padding, min height, margin;
- chaque ecriture porte sa source (`UIValueResolutionSource`) et son `UIInvalidationKind`; la lecture retourne le gagnant selon `UIValuePrecedence`;
- garder toutes les autres proprietes comme proprietes C# simples; conserver les notifications `NPC`/`OnXChanged` existantes des proprietes pilotes;
- exposer un point de lecture interne de la source gagnante par propriete pilote, consomme par la tache 5;
- tests de precedence croisant local, template, theme et dynamic resource sur les proprietes pilotes.

Criteres d'acceptation:

- la precedence observee sur les proprietes pilotes correspond exactement a `UIValuePrecedence`;
- aucun cout mesurable sur les proprietes non pilotes;
- le perimetre pilote est documente dans le code.

Commit recommande: `style-theme: add pilot resolved value engine`

### ⚪ 5. Ajouter une API de diagnostic TryGetResolvedValueSource

But:
pouvoir repondre a "d'ou vient cette valeur" pour un premier sous-ensemble de proprietes.

Etat actuel (verifie le 7 septembre 2026): aucune API de ce type n'existe (aucun `TryGetResolvedValueSource` dans le depot). Sans la tache 4, seule la source `Template` est identifiable de maniere fiable.

Travail attendu:

- exposer `TryGetResolvedValueSource(element, propertyName)` retournant les metadonnees `UIValueResolutionSource`;
- source `Template` via les `UIResolvedValue<T>` existants; sources `Theme` et `LocalValue` via le store de la tache 4 pour les proprietes pilotes;
- documenter explicitement le sous-ensemble couvert;
- tests unitaires sur les sources `Template`, `Theme` et `LocalValue` du sous-ensemble.

Criteres d'acceptation:

- une valeur appliquee par template est identifiee comme source `Template` avec son invalidation;
- le perimetre couvert et non couvert est explicite;
- pas de cout runtime hors appel de diagnostic.

Commit recommande: `style-theme: add resolved value source diagnostic`

### ⚪ 6. Ajouter une vue debug par element

But:
donner une vue de diagnostic unique par element pour l'editeur.

Travail attendu:

- s'appuie sur les taches 2 et 5;
- exposer pour un element: `VisualState` (deja capture), scope de ressources, nom du template applique et parts enregistrees (deja captures), et l'origine des 3 a 5 proprietes visuelles principales (background, foreground, border, padding) via `TryGetResolvedValueSource`;
- forme libre: extension de `UIVisualTreeSnapshot` ou service de formatage dedie dans `MGUI.Core/Tooling`.

Criteres d'acceptation:

- un seul appel permet de repondre a "pourquoi cette bordure vaut 1" pour les proprietes couvertes;
- la vue fonctionne sur un controle template (fenetre, combo box) et sur un element simple;
- tests sur la composition de la vue.

Commit recommande: `style-theme: add per element debug view`

### ⚪ 7. Introduire la convention render-only vs layout-affecting et tester GetThemeInvalidation

But:
rendre explicite quelles valeurs themees affectent le layout, au lieu de compter sur la vigilance manuelle.

Etat actuel (verifie le 7 septembre 2026):

- `MGElement.GetThemeInvalidation(...)` renvoie `Draw` par defaut et seule `MGTextBlock` le surcharge (`MGTextBlock.cs:158`); aucune classification n'existe dans `MGTheme`;
- douze fichiers surchargent `OnThemeChanged`; plusieurs correctifs ponctuels du meme probleme existent deja (`MGListBox.InvalidateItemHeightCache`, ecriture directe du champ puis `InvokeLayoutChanged` dans `MGTextBlock`, appel `Measure|Arrange` dans le template d'en-tete de `TabControl`): les inventorier comme cas de reference plutot que les redecouvrir;
- `NotifyThemeChanged` parcourt tout le sous-arbre et les composants a chaque changement de theme: la sur-invalidation a un cout reel sur les ecrans denses.

Travail attendu:

- classer les groupes de settings de `MGTheme` en deux categories: `render-only` (brushes, couleurs) et `layout-affecting` (paddings, min heights, tailles de police, marges), en reutilisant la classification deja portee par le store de la tache 4 pour les proprietes pilotes;
- surfacer cette classification (attributs, metadata ou convention documentee dans le code) et surcharger `GetThemeInvalidation(...)` la ou des valeurs layout-affecting circulent par le theme;
- tests d'architecture verifiant que les controles consommant des valeurs layout-affecting du theme demandent bien `Measure`/`Arrange` lors d'un changement de theme.

Criteres d'acceptation:

- l'inventaire render-only vs layout-affecting est explicite et teste;
- un changement de theme qui modifie une taille invalide le layout des controles concernes;
- pas de sur-invalidation: les valeurs render-only restent `Draw` seul.

Commit recommande: `style-theme: classify themed values by invalidation impact`

### ⚪ 8. Migrer les controles docking feuilles vers des templates structurels

But:
donner aux controles docking le meme contrat structurel que `TreeView`/`TextBox`, au lieu de simples applicateurs de defaults.

Etat actuel (verifie le 7 septembre 2026):

- les controles docking posent `DefaultControlTemplateName` sur les noms catalogue (`Dock.TabItem.Default`, `Dock.Splitter.Default`, etc.) et enregistrent des parts `PART_*`, mais AUCUN controle docking ne surcharge `GetRequiredControlTemplateParts()` ni `AttachControlTemplateStructure(...)`;
- `MGControlTemplateCatalog` enregistre les cinq templates `Dock.*` via la surcharge a deux arguments `Register(Resources, Name, ApplyX)` (lignes 170-174), alors que la forme structurelle a trois arguments (lignes ~212-248) sert a Window, Overlay, ComboBox, TreeView, TextBox, NumericUpDown, TabControl, PropertyGrid et Graph*; aucun `CreateDock*TemplateStructure` n'existe;
- les variantes `Dark.Dock*` (`BuiltInControlTemplates.xaml:208-212`) sont des `BasedOn` nus, sans `Parts` ni `DetachedRoots`;
- `Docs/styling-theme-architecture.md:250` range tous les `Dock.*` dans "applicateurs de defaults seuls (sans phase structurelle)": il n'y a donc pas de "premiere vague structurelle" livree, contrairement a l'ancienne formulation de `roadmap-tasks.md`.

Travail attendu:

- migrer d'abord les visuels feuilles: `MGDockSplitterBar`, `MGDockDropIndicators`, `MGDockPreviewOverlay`, `MGDockAutoHideStrip`;
- pour chaque controle: declarer les parts requises, extraire la creation de structure vers un createur `Create*TemplateStructure` (modele: `TreeView.Default` / `TextBox.Default` dans le catalogue), attacher via `AttachControlTemplateStructure(...)`, appliquer les regles de robustesse (backing fields, un seul proprietaire par part);
- completer les variantes `Dark.Dock*` si la structure l'exige;
- valider les interactions docking sensibles (drag de splitter, affichage des zones de drop, preview, strip auto-hide).

Criteres d'acceptation:

- les quatre controles feuilles consomment une structure de template au runtime;
- `MGDockPreviewOverlay` ne code plus sa couleur en dur (rgb 0,122,204, `MGDockPreviewOverlay.cs:75-76`) : elle passe par le theme (ajout dans `MGThemeDockingSettings` et les trois blocs Docking de `BuiltInThemes.xaml`), suite du bug 3 du 7 septembre 2026 (`Docs/Tasks/docking-bugs-tasks.md`, tache 5);
- les comportements docking critiques restent stables: `SCN-DOCK-001` + tests `MGUI.Tests/Docking` verts;
- tests d'infrastructure etendus (`--filter FullyQualifiedName~ControlTemplateInfrastructureTests`) et build des samples vert.

Commit recommande: `style-theme: migrate docking leaf structural templates`

### ⚪ 9. Migrer les tabs docking et les surfaces composites vers des templates structurels

But:
terminer la migration structurelle docking sur les controles a plus forte orchestration. Cette tache absorbe l'ancienne tache 1 de `Docs/Tasks/roadmap-tasks.md` (seconde vague docking).

Etat actuel (verifie le 7 septembre 2026):

- `MGDockTabItem` et `MGDockAutoHideDrawer`: applicateurs de defaults seuls (voir tache 8);
- `MGDockTabGroup` (`MGUI.Core/UI/Docking/Controls/MGDockTabGroup.cs`): enregistre `PART_Accent`, `PART_DropdownIcon`, `PART_WindowStateIcon` mais construit son chrome sans template et sans `DefaultControlTemplateName`;
- `MGDockHost`: enregistre ses parts (preview overlay, drop indicators, strips, drawer auto-hide), sans template par defaut; controle le plus couple (etat docking, hit-testing, orchestration de layout);
- `MGDockSplitContainer`: aucune part declaree;
- `MGFloatingDockWindow`: aucun hook structurel (traite en tache 12).

Travail attendu:

- suite de la tache 8, dans l'ordre: `MGDockTabItem`, puis `MGDockTabGroup` (ajouter `Dock.TabGroup.Default` au catalogue, y deplacer les defaults visuels du constructeur, poser `DefaultControlTemplateName`, conserver drag, selection et fermeture dans le controle), puis `MGDockAutoHideDrawer`, puis les surfaces de `MGDockHost` (parts hote deja declarees);
- statuer sur `MGDockHost` et `MGDockSplitContainer`: identifier la part de chrome reellement templatable et soit la migrer sur le meme modele, soit documenter dans `Docs/styling-theme-architecture.md` pourquoi le controle reste structure pure (container de layout sans chrome);
- meme contrat que la tache 8: parts requises, createur de structure, attachement runtime, defaults dans le catalogue, variantes `Dark.*` dans `BuiltInControlTemplates.xaml` pour tout nouveau template;
- utiliser une part requise de type `MGWindow` (voir tache 12) pour les surfaces hors sous-arborescence unique quand necessaire;
- l'orchestration du host (etat docking, hit-testing, layout) reste comportementale;
- si un controle ne peut pas exprimer un visuel necessaire avec le vocabulaire fige en tache 3, s'arreter (⛔) et demander une revue au lieu d'etendre le pattern.

Criteres d'acceptation:

- `MGDockTabGroup` declare son template structurel et ses defaults visuels ne vivent plus dans le constructeur; en particulier le survol de ses boutons compacts (`new Color(70,70,74)`, `MGDockTabGroup.cs:367`) et la couleur de ses icones (`new Color(200,200,200)`, `:259`, `:264`, `:320`, `:342`) passent par le theme (`MGThemeDockingSettings` et les trois blocs Docking de `BuiltInThemes.xaml`), suite du bug 3 du 7 septembre 2026 (`Docs/Tasks/docking-bugs-tasks.md`, tache 5);
- une decision par controle restant (migre ou hors chrome) est actee dans la doc d'architecture;
- les controles docking migres declarent leurs parts et consomment un template structurel;
- drag/drop d'onglets, split, auto-hide, pin/close, floating restent stables: `SCN-DOCK-001` + tests `MGUI.Tests/Docking` verts, tests d'infrastructure verifiant l'enregistrement des nouveaux templates (`MGUI.Tests/Architecture/ControlTemplateInfrastructureTests.cs`);
- le backlog docking residuel est explicitement liste s'il en reste.

Commit recommande: `style-theme: migrate docking tab and composite structural templates`

### ⚪ 10. Ajouter une API de refresh de styles sur sous-arbre

But:
permettre de re-styler un sous-arbre deja charge sans reparser tout le XAML.

Etat actuel (verifie le 7 septembre 2026):

- les styles s'appliquent uniquement pendant le parsing via `Element.ProcessStyles(...)` (`MGUI.Core/UI/XAML/Element.cs`), seul site de production `MGUI.Core/UI/XAML/XAMLParser.cs:331`; aucune API `RefreshStyles`/`RestyleSubtree` n'existe; `MGElement` n'a aucune notion de `Style`;
- `ExplicitlySetProperties` n'existe que sur le modele XAML de parse (`Element.cs:29`), rempli par les setters XAML et consomme par `IsXAMLPropertyUnset` (`Element.cs:956-980`) dans `ProcessStyles`; il est perdu une fois l'arbre `MGElement` construit. Un substitut cote runtime (suivi des proprietes explicitement posees) est le prerequis central de cette tache;
- ne pas confondre avec le refresh de theme (`NotifyThemeChanged` / `ApplyThemeDefault`), qui existe deja et ne concerne pas les `Style` XAML;
- le store de la tache 4 (source + invalidation par propriete) est la forme naturelle de ce suivi pour les proprietes pilotes.

Travail attendu:

- concevoir une API additive de re-application des styles implicites/explicites sur un sous-arbre runtime existant, sans modifier le comportement de `ProcessStyles` au parse;
- respecter la precedence: ne pas ecraser les valeurs locales, bindings, ni les proprietes explicitement posees dans le XAML d'origine;
- limiter les invalidations au strict necessaire selon `UIInvalidationKind`;
- tests sur: ajout d'un style apres chargement, override par scope, non-ecrasement des valeurs locales.

Criteres d'acceptation:

- un style ajoute a un scope peut etre applique a un sous-arbre existant sans reparse;
- aucune valeur locale ou explicitement posee n'est ecrasee;
- le cout est borne au sous-arbre cible.

Commit recommande: `style-theme: add subtree style refresh api`

### ⚪ 11. Isoler le chrome restant de MGTextBox du moteur d'edition

But:
preparer un template de chrome complet pour `MGTextBox` sans reecrire le moteur d'edition.

Etat actuel (verifie le 7 septembre 2026):

- les couleurs de selection, le padding et la min height passent deja par le template `TextBox.Default` (`MGControlTemplateCatalog.cs:1171-1177`, sept cles au total), et les parts border/text block/placeholder/compteur/resize grip sont declarees;
- valeurs visuelles encore en dur: marge `(0, 0, 8, 4)` et taille de police 9 du compteur de caracteres dans `CreateTextBoxTemplateStructure` (`MGControlTemplateCatalog.cs:663-667`); placement Right/Bottom du compteur dans `AttachControlTemplateStructure` (`MGTextBox.cs:1320`); alignements de contenu par defaut dans le constructeur (`MGTextBox.cs:1121-1122`); chaines de format du compteur (`MGTextBox.cs:1133-1134`);
- aucune entree `TextBox` dans `BuiltInControlTemplates.xaml`; aucun commentaire ne marque la frontiere comportement/chrome dans `MGTextBox.cs`;
- `MGPasswordBox` derive de `MGTextBox` et `MGNumericUpDown` reutilise `ApplyTextBoxTemplate`: a couvrir dans les tests.

Travail attendu:

- separer les valeurs purement visuelles restantes (liste ci-dessus, brushes residuels, dispositions de placeholder/compteur) de la logique input/caret/selection;
- faire passer ces valeurs par le catalogue ou l'asset XAML, en respectant la regle du backing field pour toute propriete pilotant une part;
- ne pas reouvrir le moteur d'edition texte ni le formatage de selection.

Criteres d'acceptation:

- les valeurs visuelles restantes de `MGTextBox` sont template/theme-owned;
- comportement clavier/souris, caret et selection inchanges (tests cibles `--filter FullyQualifiedName~TextBox`), `MGPasswordBox` et `MGNumericUpDown` inclus;
- la frontiere comportement/chrome est documentee dans le code.

Commit recommande: `style-theme: isolate textbox chrome from edit engine`

### ⚪ 12. Modeliser les controles composites a overlays ou fenetres auxiliaires

But:
clore la derniere zone ouverte de la convergence lookless: les controles qui pilotent des overlays, popups ou fenetres auxiliaires. Cette tache absorbe l'ancienne tache 2 de `Docs/Tasks/roadmap-tasks.md`.

Etat actuel (corrige le 7 septembre 2026; la version du 2 septembre affirmait a tort qu'aucun modele n'existait et que le dropdown de `MGComboBox` etait construit par le controle):

- `MGControlTemplateStructure.DetachedRoots` existe (`MGUI.Core/UI/Styling/MGControlTemplate.cs`) mais n'a aucune semantique runtime: rien ne le lit, le loader aplatit les elements nommes dans `Structure.Parts` (`ControlTemplateLoader.cs:191-220`). Le contrat reel est "declarer une part requise de type `MGWindow`";
- `MGComboBox` declare la part requise `PART_DropdownWindow` de type `MGWindow` (`MGComboBox.cs:63`); la fenetre est construite par le template `ComboBox.Default` (`MGControlTemplateCatalog.cs:347`) et remplacable en XAML (`Dark.ComboBox`, `BuiltInControlTemplates.xaml:158-182`); le controle cable encore a la main `AddNestedWindow`/`RemoveNestedWindow` (`MGComboBox.cs:626`, `:633`);
- `MGContextMenu`, `MGToolTip`, `MGOverlay` posent `DefaultControlTemplateName` (`MGContextMenu.cs:723`, `MGToolTip.cs:83`, `MGOverlay.cs:580`);
- restent hors contrat: `MGFloatingDockWindow` (`MGUI.Core/UI/Docking/Controls/MGFloatingDockWindow.cs`, aucun hook structurel) et `MGColorPickerPopup` (`MGUI.Core/UI/Color/MGColorPickerPopup.cs:8`, classe sealed qui n'est pas un `MGElement`, construit un `MGWindow` brut a `:39`, consommee par `MGColorField`). Ce dernier ne peut pas porter de `ControlTemplate` aujourd'hui: c'est le cas qui exige l'extension minimale;
- l'inventaire des quatre surfaces popup et de leur regle `ActivatesOnClick = false` existe deja dans `Docs/input-window-activation-design.md` (lignes 161-172);
- tests existants d'ouverture/attachement/fermeture: `MGUI.Tests/Input/NestedWindowHoverOcclusionTests.cs`, `OverlappingWindowsInputRoutingTests.cs`, `ContextMenuHoverOcclusionTests.cs`, `MGUI.Tests/Modal/ContextMenuClickThroughTests.cs`; rien de dedie pour `MGFloatingDockWindow` ni `MGColorPickerPopup`.

Travail attendu:

- inventorier les controles qui ouvrent encore des fenetres imbriquees, popups ou overlays (`MGComboBox`, `MGContextMenu`, `MGToolTip`, `MGFloatingDockWindow`, `MGColorPickerPopup`, tooltips detaches) et classer chaque cas: supportable par "part requise de type `MGWindow`", ou demandant une extension minimale du runtime;
- formaliser dans `Docs/styling-theme-architecture.md` (section a creer) la frontiere entre structure templatee, parts detachees et surfaces auxiliaires: cycle de vie, parentage, ouverture, fermeture, y compris le cas des controles derives d'un type deja template (`MGContextMenu` herite de `MGWindow` et doit tolerer la phase de template de base pendant sa construction);
- sortir le cablage `AddNestedWindow`/`RemoveNestedWindow` de `MGComboBox` vers le contrat si celui-ci le permet, sinon acter la limite;
- etendre la validation de template aux contraintes comportementales inter-parts la ou c'est peu couteux, sinon acter la limite dans la doc;
- migrer `MGFloatingDockWindow` si le contrat actuel suffit; produire la specification d'extension minimale pour `MGColorPickerPopup` plutot que de l'implementer ici;
- ajouter des tests cibles d'ouverture/attachement/fermeture pour `MGFloatingDockWindow` (`MGDockHost.CloseFloatingWindow`) et `MGColorPickerPopup`.

Criteres d'acceptation:

- l'inventaire distingue clairement cas supportes et cas demandant une extension;
- les contraintes de cycle de vie des surfaces auxiliaires sont documentees;
- au moins un cas representatif est borne par du code ou une spec exploitable;
- ouverture, attachement et fermeture des surfaces auxiliaires sont valides par des tests cibles; `SCN-THEME-001` vert; les tests d'activation existants (`WindowActivationOnClickTests.cs`, `FloatingDockWindowActivationTests.cs`) restent verts.

Commit recommande: `style-theme: model auxiliary surface composites`

### ⚪ 13. Definir un preset "Editor Compact"

But:
verifier les limites reelles du systeme theme/ressources/styles sur un cas editeur dense.

Etat actuel (verifie le 7 septembre 2026):

- aucun preset de ce type n'existe (aucun `EditorCompact` dans le depot); `MGTheme.BuiltInTheme` ne connait que `Light_Gray`, `Dark_Blue` et `Dark`;
- limites deja connues a consigner d'emblee: `ToolTip.Padding` est un litteral du catalogue (`MGControlTemplateCatalog.cs:829`, `Thickness(6, 3)`) non lu depuis `MGTheme`, et aucun chemin d'override theme des cles de template n'existe (la cle n'est qu'un identifiant de comparaison au refresh); les controles docking codent leur `Padding` en C# et sont `MGElementType.Custom`, donc hors de portee d'un `Style` par type;
- `ThemeTabControlSettingsDefinition` et `ThemeListBoxSettingsDefinition` (`MGUI.Core/UI/XAML/Themes.cs`) exposent deja paddings et espacements: les familles non docking sont atteignables.

Travail attendu:

- definir un preset compact en themes/resources/styles pour les tabs, les listes, les combo box et les tooltips d'abord, puis le docking: tailles, paddings et densites reduits;
- l'implementer uniquement avec les mecanismes declaratifs existants (`ThemeDefinition` avec `BasedOn`, `ControlTemplates`, `Properties`, styles scopes), sans nouvelle API et sans toucher au code des controles;
- consigner chaque valeur impossible a piloter declarativement: c'est le livrable principal (limites reelles du systeme); l'ecart docking releve des taches 3, 8 et 9, ne pas le corriger ici;
- exposer le preset dans `MGUI.Samples` (par exemple a cote de `MGUI.Samples/Features/StyleThemeRefactor.xaml`, enregistrement via le pattern de `Compendium.xaml.cs`).

Criteres d'acceptation:

- le preset s'applique par simple changement de theme sur un scope;
- la liste des valeurs non pilotables declarativement est documentee et versee comme nouvelles taches si pertinent;
- aucun changement de code de controle n'est necessaire pour appliquer le preset;
- `SCN-THEME-001` vert.

Commit recommande: `style-theme: add editor compact preset`
