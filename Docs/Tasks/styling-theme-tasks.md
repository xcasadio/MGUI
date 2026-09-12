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

### ✅ 4. Construire un mini moteur de valeurs resolues pour des proprietes pilotes

**Statut** : livre le 11 septembre 2026 par le programme `Docs/Tasks/resolved-value-engine-tasks.md` (tranches S1 a S9 et S7a, ADR-0005 acceptee) : store `UIResolvedPropertyStore` par element, sept pilotes (huit cles `UIPilotProperty`, le texte ayant deux conteneurs), setters tagues, migration de tous les sites d'ecriture (constructeurs, catalogue, callbacks de theme, XAML avec provenance de style, bindings, ressources dynamiques avec retombee), precedence appliquee a l'ecriture, balayage d'architecture de completude, diagnostic interne `TryGetResolvedValueSource` (base de la tache 5). Decision de l'auteur du 11 septembre : les defauts du catalogue poses sur le controle lui-meme sont `Theme`, ceux des parts `Template`, pour que les styles XAML continuent de l'emporter sur le chrome par defaut d'un controle templatise. Correction : `UIValueResolutionSource` compte onze sources, pas dix.

But:
reduire l'ecart entre le modele `UIValuePrecedence` et la realite runtime, sans dependency property system complet.

Etat actuel (verifie le 7 septembre 2026):

- seules les valeurs appliquees par template portent un `UIResolvedValue<T>` (store `_AppliedTemplateDefaults` de `MGElement`, via `MGControlTemplateContext.ApplyTemplateValue(...)` et `TryGetAppliedTemplateDefault`); il n'existe aucun store par propriete couvrant les sources local/style/theme;
- `UIResolvedValue<T>`, `UIValueResolutionSource` (onze sources ordonnees par `UIValuePrecedence`) existent depuis mars 2026 et suffisent comme briques: pas de redesign necessaire;
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

### ✅ 5. Ajouter une API de diagnostic TryGetResolvedValueSource

**Statut** : livre le 11 septembre 2026. API publique `UIToolingService.TryGetResolvedValueSource(MGElement element, string propertyPath, out UIValueResolutionSource source)` au-dessus du point de lecture interne de la tache 4 (S9) : le chemin est traduit par la table XAML des bindings (`XAML.Element.MapBindingTargetPath`, nouvelle aide interne sur `BindingPathMappings`), resolu par `UIPilotPropertyResolver` (cle pilote, slot, bordure `GetBorder()` pour `BorderBrush`/`BorderThickness`), et la source gagnante vient de `MGElement.TryGetResolvedValueSource` (replis Inherited/Theme du texte et regle de dormance R6 compris). Perimetre explicite : liste publique `UIToolingService.ResolvedValueSourcePropertyPaths` (27 chemins : chemins CLR des huit cles pilotes et de leurs sous-champs, puis noms XAML `Background`, `SelectedBackground`, `DisabledBackground`, `TextForeground`, `SelectedTextForeground`, `DisabledTextForeground`, `Foreground`) ; toute autre propriete renvoie faux, comme un element sans bordure, un `Foreground` hors `MGTextBlock`, une valeur jamais ecrite, un element nul ou non initialise ; aucune exception, aucun cout hors appel. Tests `MGUI.Tests/Tooling/ResolvedValueSourceToolingTests.cs` (6) : sources `Template` (padding de la part barre de titre, invalidation Measure | Arrange), `Theme` (padding de fenetre) et `LocalValue` (setter, attributs XAML), noms XAML egaux aux chemins CLR, egalite avec la lecture du store par chemin, liste du perimetre epinglee et verifiee contre le resolveur, proprietes non couvertes. Mutations : traduction XAML retiree => 2 rouges ; slot ignore (lecture Whole) => 2 rouges ; vert apres reversion. Suites : 6 (nouvelle classe), 19 (Tooling), 627 (Architecture), 1631 (complete). Docs : `styling-theme-architecture.md` (Outillage, point de lecture), ADR-0005 (consequences).

But:
pouvoir repondre a "d'ou vient cette valeur" pour un premier sous-ensemble de proprietes.

Etat actuel (verifie le 7 septembre 2026, mis a jour le 11 septembre) : la tache 4 (tranche S9) livre un point de lecture interne `MGElement.TryGetResolvedValueSource(UIPilotProperty, UIValueSlot, out UIValueResolutionSource)` et `EnumerateResolvedContributions` pour les sept pilotes (huit cles), toutes sources confondues (DefaultValue, Theme, DynamicResource, ImplicitStyle, ExplicitStyle, Template, VisualState, LocalBinding, LocalValue ; `Inherited` et le repli theme calcules a la lecture pour le texte). Cette tache doit exposer une API publique par nom de propriete au-dessus de ce point de lecture, et documenter que les proprietes non pilotes ne sont pas couvertes.

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

### ✅ 6. Ajouter une vue debug par element

**Statut** : livre le 11 septembre 2026. Service de formatage dedie dans `MGUI.Core/Tooling` plutot qu'une extension du snapshot d'arbre (le record positionnel `UIVisualTreeSnapshot` reste inchange) : `UIToolingService.CaptureElementDebugView(MGElement)` rend un `UIElementDebugView` (identifiant diagnostic stable, nom, type, etats visuels primaire et secondaire, scope de ressources effectif, proprietaire et scope local comme en tache 2, template applique, parts enregistrees nom -> type, derniere erreur de template) avec cinq `UIValueOriginView` : `Background`, `TextForeground` (`Foreground` pour un `MGTextBlock`), `BorderBrush`, `BorderThickness` et `Padding`, chacun portant la source gagnante lue par `TryGetResolvedValueSource` (tache 5), la valeur effective en texte et toutes les contributions du store (`EnumerateResolvedContributions`, precedence decroissante). `UIToolingService.RenderElementDebugView(view)` produit l'artefact texte (`chemin = valeur <- Genre(precedence) 'nom' invalidation=...`, puis une ligne par contribution, `<not resolved>` pour une valeur non resolue). Tests `MGUI.Tests/Tooling/ElementDebugViewTests.cs` (5) : fenetre templatee (template, part barre de titre, scope Window local, padding `Theme` 'Window.Padding'), combo box templatee (template `ComboBox.Default`, part `PART_DropdownWindow`, scope herite, brosse de bordure `Theme` 'ComboBox.BorderBrush'), bordure simple ("pourquoi cette bordure vaut 1" : `LocalValue` au-dessus de la contribution `DefaultValue`, rendu texte), texte au premier plan herite et sans bordure, arguments nuls ; `ToolingHooksTests` epingle les nouvelles entrees publiques des taches 5 et 6. Mutations : contributions non capturees et rendu ignorant `IsResolved` => 3 rouges ; premier plan d'un `MGTextBlock` ignore => 1 rouge ; vert apres reversion. Suites : 8 (nouvelle classe et hooks), 24 (Tooling), 627 (Architecture), 1636 (complete). Doc : `styling-theme-architecture.md` (Outillage).

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

### ✅ 7. Introduire la convention render-only vs layout-affecting et tester GetThemeInvalidation

**Statut** : livre le 12 septembre 2026. Inventaire explicite public `UIThemeValueInvalidation` (`MGUI.Core/UI/Styling/UIThemeValueInvalidation.cs`) : chaque valeur de `MGTheme` est classee par chemin (`"CheckBoxComponentSize"`, `"Window.CloseButtonMinWidth"`, `"FontSettings.DefaultFontSize"`, `"Backgrounds"` pour les fonds par type d'element) en `RenderOnly` (`Draw`), `LayoutAffecting` (`Measure | Arrange`) ou `Structural` (correspondances de templates), avec `TryGetInvalidation`, `GetInvalidation`, `IsLayoutAffecting` et `ForChange`. La classification n'est pas deduite du type : `PropertyGrid.RowSeparatorBrush`, dont la presence ajoute une bordure a chaque ligne, est layout-affecting. Runtime : `NotifyThemeChanged` evalue `GetThemeInvalidation` avant `OnThemeChanged` et le refresh de template, puis invalide par `LayoutChanged` (parents compris) au lieu de `InvalidateLayout`. L'unique surcharge existante, `MGTextBlock`, lisait l'etat deja rafraichi et demandait `Measure | Arrange` a chaque changement de theme qui gardait la police ; elle ne le demande plus que si le defaut suivi change. Nouvelles surcharges via `ForChange` : `MGCheckBox` (`CheckBoxComponentSize`), `MGPropertyGrid` (padding et hauteur minimale des en-tetes, espacement et padding des lignes, presence du separateur) et `MGGraphNode` (epaisseur de bordure de l'etat de selection). Au refresh de theme, `ApplyTemplateValueCore` n'invalide plus le layout pour une valeur re-appliquee a l'identique. Dans le catalogue, 24 valeurs passent de `Draw` a `Measure | Arrange` (largeurs minimales du bouton de fermeture et de l'infobulle, espacements, marges et visibilite des lignes de grille de `ListView`, espacements de panneaux, indentation, alignements, largeurs du spinner) et les 2 noms de templates d'en-tete d'onglet a `Structure | Measure | Arrange`, grace a une surcharge non taguee `ApplyThemeDefault(Name, Value, GetCurrentValue, SetValue, Invalidation, Comparer)`. Cas de reference : `MGListBox` est classe parmi les callbacks qui ne lisent aucune valeur layout-affecting (son `InvalidateItemHeightCache` re-mesure sans invalider le layout), l'ecriture de police de `MGTextBlock` passe par la surcharge corrigee, et les valeurs du template d'en-tete de `TabControl` (deja `Measure | Arrange`) sont couvertes par le balayage. Tests `MGUI.Tests/Architecture/ThemeValueInvalidationInventoryTests.cs` (6) : inventaire complet par reflexion, trois categories, accord avec `UIPilotPropertyResolver.KindOf` pour les valeurs en forme de pilote, epinglage des autres, `ForChange`, inventaire des 16 callbacks `OnThemeChanged` dont 4 declarent leur invalidation. Tests `ThemeLayoutInvalidationTests.cs` (10, `SCN-THEME-001`) : surcharges des quatre controles ; sonde de l'ordre d'evaluation et de la propagation aux parents ; changements de taille par le theme qui invalident combo box, arbre, onglets et fenetre ; refresh sans changement qui garde le layout valide ; changement de toutes les valeurs `RenderOnly` sans aucune invalidation dans une fenetre de 17 controles ; balayage des valeurs appliquees par le catalogue selon leur type. Mutations (9, en cinq lots) : espacement de la liste deroulante remis a `Draw`, texte demandant le layout sans changement de taille, separateur ignore et noeud selectionne lisant l'epaisseur non selectionnee => 7 rouges ; surcharge `MGCheckBox` retiree => 2 rouges ; garde du refresh retiree, separateur classe render-only et entree d'inventaire retiree => 5 rouges ; ancien ordre d'evaluation => aucun rouge au premier passage, d'ou la sonde, puis 1 rouge ; `InvalidateLayout` au lieu de `LayoutChanged` => 1 rouge ; vert apres reversion. Suites : 16 (nouvelles classes), 643 (Architecture), 241 (`SCN-THEME-001`), 1652 (complete). Limites : une nouvelle lecture de valeur dans un callback deja classe reste a declarer a la main ; `MGContextMenu` et `MGGraphPort` reconstruisent des parts a chaque changement de theme, meme render-only ; `TreeViewExpanderButtonSize` et `FontSettings.LargeFontSize` ne sont lus par aucun controle. Docs : `styling-theme-architecture.md` (contrat de refresh, invalidation liee au theme, valeurs template, limites connues).

But:
rendre explicite quelles valeurs themees affectent le layout, au lieu de compter sur la vigilance manuelle.

Etat actuel (verifie le 7 septembre 2026):

- `MGElement.GetThemeInvalidation(...)` renvoie `Draw` par defaut et seule `MGTextBlock` le surcharge (`MGTextBlock.cs:158`); aucune classification n'existe dans `MGTheme`;
- douze fichiers surchargent `OnThemeChanged`; plusieurs correctifs ponctuels du meme probleme existent deja (`MGListBox.InvalidateItemHeightCache`, ecriture directe du champ puis `InvokeLayoutChanged` dans `MGTextBlock`, appel `Measure|Arrange` dans le template d'en-tete de `TabControl`): les inventorier comme cas de reference plutot que les redecouvrir;
- `NotifyThemeChanged` parcourt tout le sous-arbre et les composants a chaque changement de theme: la sur-invalidation a un cout reel sur les ecrans denses.
- Mis a jour le 11 septembre 2026 (tache 4 livree) : le store porte deja une classification par propriete pilote, epinglee par test d'architecture (`ResolvedValueSourceDiagnosticsTests`) : Margin, Padding, MinHeight et BorderThickness = `Measure | Arrange` ; BorderBrush, Background, Foreground et DefaultTextForeground = `Draw`. Chaque ecriture taguee y attache son `UIInvalidationKind`, mais les callbacks `OnThemeChanged` et `GetThemeInvalidation` ne lisent pas encore cette classification.

Travail attendu:

- classer les groupes de settings de `MGTheme` en deux categories: `render-only` (brushes, couleurs) et `layout-affecting` (paddings, min heights, tailles de police, marges), en reutilisant la classification deja portee par le store de la tache 4 pour les proprietes pilotes;
- surfacer cette classification (attributs, metadata ou convention documentee dans le code) et surcharger `GetThemeInvalidation(...)` la ou des valeurs layout-affecting circulent par le theme;
- tests d'architecture verifiant que les controles consommant des valeurs layout-affecting du theme demandent bien `Measure`/`Arrange` lors d'un changement de theme.

Criteres d'acceptation:

- l'inventaire render-only vs layout-affecting est explicite et teste;
- un changement de theme qui modifie une taille invalide le layout des controles concernes;
- pas de sur-invalidation: les valeurs render-only restent `Draw` seul.

Commit recommande: `style-theme: classify themed values by invalidation impact`

### ✅ 8. Migrer les controles docking feuilles vers des templates structurels

**Statut** : livre le 12 septembre 2026. Les quatre controles feuilles consomment une structure de template au runtime. `MGDockSplitterBar` (`PART_Surface` et `PART_Accent` en `MGBorder`, `PART_Grip` en `MGGripDotsIcon`), `MGDockAutoHideStrip` (`PART_Separator` en `MGRectangle`), `MGDockDropIndicators` (neuf zones `MGDockDropZoneIndicator`) et `MGDockPreviewOverlay` (`PART_Surface` et `PART_Border` en `MGBorder`, nouveau template `Dock.PreviewOverlay.Default`) declarent leurs parts (`GetRequiredControlTemplateParts`), les recoivent des createurs `CreateDock*TemplateStructure` du catalogue et les attachent dans `AttachControlTemplateStructure` ; leurs constructeurs ne creent plus aucune part, chaque part n'a donc qu'un proprietaire. A l'attachement, le splitter et le strip lient leurs parts en composants par `EnsureComponentBinding` ; les zones (qui recoivent leur zone du nom de part) et les deux parts de l'apercu deviennent des enfants poses par `SetParent` et mis en page par le controle, et une structure de remplacement detache les parts qu'elle remplace. L'etat reste sur le controle et est repousse sur les parts attachees : brosses et couleurs du splitter, couleur du separateur, zones et etats actif ou desactive des indicateurs, couleurs, epaisseur et bornes de l'apercu (relayees a chaque deplacement, sans passe de layout de l'hote) ; sans structure, l'apercu dessine encore lui-meme son fond et sa bordure. Theme : `MGDockPreviewOverlay` ne code plus rgb(0,122,204). Les reglages `MGThemeDockingSettings.PreviewOverlayFillColor` et `PreviewOverlayBorderColor` passent par le DTO `ThemeDockingSettingsDefinition`, `ThemeDefinitionBuilder.ApplyDocking`, la copie de `MGTheme` et l'inventaire de la tache 7 (render-only) ; leurs valeurs par defaut reprennent l'ancienne couleur pour les themes XAML qui ne les declarent pas. Les trois blocs Docking de `BuiltInThemes.xaml` les fixent sur la couleur active des indicateurs de drop de chaque theme : Dark_Blue rgba(0,122,204,100) et (0,122,204,200), inchange ; Dark rgba(58,121,187,100) et (58,121,187,200) ; Light_Gray rgba(152,171,191,100) et (152,171,191,200). `Dock.PreviewOverlay.Default` applique ces couleurs et une epaisseur de 2 (`Measure | Arrange`) par les facades `PreviewColor`, `PreviewBorderColor` et `PreviewBorderThickness`. Variantes : `ControlTemplateLoader.BuildTemplates` fait reprendre a une variante `BasedOn` sans racine ni `DetachedRoots` le createur de structure de sa base (elle instanciait jusqu'ici une structure vide et manquait toutes les parts requises), et `Dark.DockPreviewOverlay` est ajoute. Interactions docking : drag de splitter, zones de drop (dont le redock d'une fenetre flottante), apercu et strip couverts par les tests existants de `MGUI.Tests/Docking` et `AdornerLiteTests`. Tests `MGUI.Tests/Docking/DockLeafStructuralTemplateTests.cs` (8) : parts issues du template structurel pour les quatre controles ; template de remplacement qui fournit les parts du splitter en reutilisant ses slots de composants ; detachement des zones remplacees ; disposition, etats et hit-test des zones ; variantes `Dark.Dock*` qui gardent la structure de leur base ; couleurs de l'apercu venant du theme (trois themes, copie, changement de theme sur un `MGDockHost`) ; parts de l'apercu mises en page sur ses bornes ; separateur du strip sur le bord interieur. `ControlTemplateInfrastructureTests` epingle le contrat structurel des quatre controles et l'enregistrement structurel de leurs templates ; `ResolvedPilotWriteSitesTests` suit la ligne BLOCKED de l'epaisseur de l'adorner, deplacee dans la facade. Mutations (6, en trois lots) : heritage de structure `BasedOn` desactive et remplissage de l'apercu fige => 2 rouges ; zone remplacee non detachee et etat du splitter non repousse => 2 rouges ; parts de l'apercu non relayees et separateur lie au strip entier => 2 rouges ; vert apres reversion. Suites : 8 (nouvelle classe), 206 (Docking), 65 (`ControlTemplateInfrastructureTests`), 643 (Architecture), 249 (`SCN-THEME-001`), 1660 (complete) ; build de `MGUI.Samples` vert (`SCN-DOCK-001`, sample `DockingDemo`). Limites : `SCN-DOCK-001` est valide par le build et les tests automatises, sans manipulation interactive de `DockingDemo` ; `MGDockTabItem`, `MGDockAutoHideDrawer`, `MGDockTabGroup`, `MGDockHost` et `MGDockSplitContainer` relevent de la tache 9. Docs : `styling-theme-architecture.md` (catalogue et assets, variantes `BasedOn`, controles migres, section docking, limites connues).

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

### ✅ 9. Migrer les tabs docking et les surfaces composites vers des templates structurels

**Statut** : livre le 12 septembre 2026. Les quatre controles consomment une structure de template au runtime ; leurs constructeurs ne creent plus aucune part. `MGDockTabItem` declare sept parts (surface, accent, titre, bouton et icone de fermeture, bouton et icone d'epinglage) : surface, accent et icones sont lies en composants par `EnsureComponentBinding`, le titre et les boutons deviennent des enfants, le clic de fermeture est rebranche sur la part attachee et le padding du titre devient une valeur de template. `MGDockTabGroup` recoit le nouveau template `Dock.TabGroup.Default` (panneau d'en-tetes `MGStackPanel`, accent `MGRectangle`, icones `MGEllipsisIcon` et `MGWindowStateIcon`) et pose `DefaultControlTemplateName` ; un panneau d'en-tetes de remplacement recoit les onglets reconstruits. `MGDockAutoHideDrawer` declare huit parts (bordure, barre et texte de titre, boutons et icones epingler et fermer, poignee de redimensionnement) : clics epingler et fermer rebranches, parts decoratives transparentes au hit-test, titre du panneau actif repousse ; l'epaisseur de sa bordure reste une ecriture `Theme` de `ApplyThemeVisuals` (provenance epinglee par `ResolvedThemeChangeTests`). `MGDockHost` recoit le nouveau template `Dock.Host.Default` : apercu, indicateurs de drop, quatre strips et tiroir sont des parts attachees en composants, chaque strip recevant le cote de son nom de part ; evenements et orchestration du docking restent dans l'hote. `Dark.DockTabGroup` et `Dark.DockHost` sont ajoutes. Boutons compacts du groupe (debordement, agrandir/restaurer) : ADR-0002 ne leur donne aucun role et la tache interdit d'etendre le vocabulaire, ils restent donc crees et geres par le groupe ; leur survol et la couleur des icones passent par le theme (`MGThemeDockingSettings.TabGroupButtonHoverColor` et `TabGroupIconColor`, DTO, `ThemeDefinitionBuilder`, copie de `MGTheme`, inventaire de la tache 7 en render-only), avec les anciennes couleurs rgb(70,70,74) et rgb(200,200,200) en valeurs par defaut ; blocs Docking : Dark_Blue rgb(28,52,102) et rgb(200,200,200), Dark rgb(62,62,66) et rgb(170,170,170), Light_Gray rgba(188,202,218,190) et rgb(64,64,64), appliques par les facades `CompactButtonHoverColor` et `IconColor`. Changement de theme : depuis la tache 8, le mapping du theme `Dark` vers une variante `Dark.Dock*` reconstruisait les parts des controles migres et perdait les valeurs locales posees dessus (revele par `ResolvedThemeChangeTests` sur le tiroir). Une variante `BasedOn` nue partage desormais le template de structure de sa base (`MGControlTemplate.StructureTemplate`, fabrique `CreateStructureVariant` utilisee par `ControlTemplateLoader`), et `MGElement.ApplyControlTemplate` ne reconstruit une structure que si ce template de structure change ; les tests des variantes `Dark.Dock*` des taches 8 et 9 attendent donc les memes parts. Decisions : `MGDockSplitContainer` reste un conteneur de layout pur (il ne peint rien, son splitter porte son propre template depuis la tache 8) ; `MGFloatingDockWindow` releve de la tache 12. Backlog docking residuel : boutons compacts du groupe hors vocabulaire de parts (une revue d'ADR-0002 serait requise pour les templater), `MGFloatingDockWindow` (tache 12). Tests `MGUI.Tests/Docking/DockCompositeStructuralTemplateTests.cs` (9) : parts issues du template structurel pour les quatre controles ; structure de tab item remplacee (anciens enfants detaches, titre garde) ; couleurs d'en-tete du groupe venant du theme (trois themes, copie, changement de theme) ; panneau d'en-tetes remplace qui recoit les onglets ; structure de tiroir remplacee (parts detachees, titre actif garde) ; structure d'hote remplacee qui relie chaque surface ; variantes composites `Dark.Dock*` ; bascule vers le theme `Dark` qui garde les parts de cinq controles mappes ; tiroir cree sous le theme `Dark` qui tient ses parts de la variante. `ControlTemplateLoaderTests` : partage du template de structure (variante nue, variante de variante, variante avec racine). `ControlTemplateInfrastructureTests` epingle le contrat structurel des quatre controles, l'enregistrement structurel de leurs templates, l'absence des couleurs codees en dur du groupe et le branchement des clics. Mutations (10, en six lots) : clic de fermeture non branche et icone d'etat de fenetre en blanc => rouges ; parts remplacees du tiroir gardees et panneau remplace sans onglets => rouges ; cote des strips non repousse et titre du tab item hit-testable => rouges ; partage de structure desactive dans `ApplyControlTemplate` => 5 rouges ; variante sans partage du template de structure => 6 rouges ; variante de variante sur sa base directe et epaisseur du tiroir hors `Theme` => 2 rouges ; vert apres reversion. Suites : 9 (nouvelle classe), 215 (Docking), 65 (`ControlTemplateInfrastructureTests`), 623 (espace de noms `MGUI.Tests.Architecture`), 259 (`SCN-THEME-001`), 1670 (complete) ; build de `MGUI.Samples` vert (`SCN-DOCK-001`). Limites : `SCN-DOCK-001` est valide par le build et les tests automatises, sans manipulation interactive de `DockingDemo` ; une variante qui declare sa propre racine reconstruit toujours la structure, et perd les valeurs posees sur les parts, quand un changement de theme la selectionne. Docs : `styling-theme-architecture.md` (catalogue, variantes `BasedOn` et partage de structure, controles migres, section docking, limites connues).

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

### ✅ 10. Ajouter une API de refresh de styles sur sous-arbre

**Statut** : livre le 12 septembre 2026. API additive `MGElement.RefreshStyles()`, qui retourne un `UIStyleRefreshResult` et re-applique les styles implicites et nommes a un sous-arbre deja charge, sans reparse. Suivi runtime : `Element.ProcessStyles` enregistre pour chaque definition XAML un `ElementStyleScope` (type de definition, `MGElementType`, `StyleNames`, `IsStyleable`, styles inline en portee depuis le plus externe, coupure `InheritsParentStyles`, proprietes stylees par sa propre passe) que `ApplyBaseSettings` pose sur l'element cree (`MGElement.StyleScope`) ; au parse, seul cet enregistrement s'ajoute, le comportement de `ProcessStyles` est inchange. Resolution (`ElementStyleRefresher`) : pour chaque element du sous-arbre qui a un scope, styles implicites fusionnes de son scope de ressources courant (calcules une fois par scope), puis styles inline, puis styles nommes (inline le plus proche, puis `TryGetStyle`), le dernier setter gagnant ; un scope local cree par `EnsureResourceScope` l'emporte sur le desktop. Precedence : seules les proprietes dont le transfert XAML est une ecriture taguee d'un pilote sont ecrites, sous `ImplicitStyle` ou `ExplicitStyle` (Margin, Padding, MinHeight, fonds et couleur de focus, trois foregrounds par defaut, `BorderBrush`/`BorderThickness` d'un `Border`, d'un `GroupBox`, de la facade `Border` d'un composite et de l'`OuterBorder` du property grid et du tree view, `Foreground` d'un `TextBlock`, fonds du bouton d'un `Expander`) ; le store garde au-dessus les valeurs locales, bindings, attributs XAML et valeurs de template. Une propriete qu'aucun style ne pose plus rend sa contribution (`ClearPilotSource`), une propriete posee par un style nomme ne garde pas de contribution implicite, comme a la parse, et un element ne retire que les contributions des proprietes stylees par sa propre passe, jamais celles qu'une facade de bordure recoit de son proprietaire. Invalidation : les setters tagues n'invalident le layout que si la valeur effective change. Les setters hors store sont rapportes sans etre appliques (`NotRefreshable`), comme les noms de style introuvables (`StyleNotFound`) et les valeurs non convertibles (`InvalidValue`). Tests `MGUI.Tests/Architecture/StyleRefreshTests.cs` (10) : style implicite ajoute apres chargement (padding, fond, epaisseur de bordure, foreground de texte) ; style nomme remplace, sans contribution implicite residuelle ; scope plus proche prioritaire ; attribut XAML, valeur locale et binding non ecrases, le binding pilotant toujours la valeur ; style retire qui rend sa valeur ; refresh borne au sous-arbre ; portee des styles inline et d'`InheritsParentStyles` conservee ; setter de bordure d'un composite rafraichi sur la bordure de sa facade ; setters non applicables rapportes ; refresh sans changement de style sans notification de layout. Mutations (7, en quatre lots) : aucun retrait de contribution => 3 rouges ; styles inline ignores et scope du desktop a la place de celui de l'element => 2 rouges ; `InheritsParentStyles` ignore et toutes les proprietes rafraichissables considerees comme deja stylees => 4 rouges ; contribution implicite gardee sous un style nomme et retrait avant reecriture => 2 rouges ; vert apres reversion. Suites : 10 (nouvelle classe), 633 (espace de noms `MGUI.Tests.Architecture`), 654 (`FullyQualifiedName~Architecture`), 269 (`SCN-THEME-001`), 176 (`SCN-MARKUP-001`), 1680 (complete) ; builds `MGUI.Core` et `MGUI.Samples` verts. Limites : le refresh se limite aux pilotes du store, un setter d'une autre propriete n'est ni re-applique ni retire ; les facades de bordure nommees autrement (`ListBox`, `Spoiler`) ne sont pas rafraichies ; seuls les elements issus d'une definition XAML dont les styles ont ete traites ont un scope ; `AddStyle`, `RemoveStyle` et `AddImplicitStyle` ne declenchent jamais le refresh, l'application l'appelle. Docs : `styling-theme-architecture.md` (vue d'ensemble, section "Refresh de styles a chaud", limites connues).

But:
permettre de re-styler un sous-arbre deja charge sans reparser tout le XAML.

Etat actuel (verifie le 7 septembre 2026):

- les styles s'appliquent uniquement pendant le parsing via `Element.ProcessStyles(...)` (`MGUI.Core/UI/XAML/Element.cs`), seul site de production `MGUI.Core/UI/XAML/XAMLParser.cs:331`; aucune API `RefreshStyles`/`RestyleSubtree` n'existe; `MGElement` n'a aucune notion de `Style`;
- `ExplicitlySetProperties` n'existe que sur le modele XAML de parse (`Element.cs:29`), rempli par les setters XAML et consomme par `IsXAMLPropertyUnset` (`Element.cs:956-980`) dans `ProcessStyles`; il est perdu une fois l'arbre `MGElement` construit. Un substitut cote runtime (suivi des proprietes explicitement posees) est le prerequis central de cette tache;
- ne pas confondre avec le refresh de theme (`NotifyThemeChanged` / `ApplyThemeDefault`), qui existe deja et ne concerne pas les `Style` XAML;
- le store de la tache 4 (source + invalidation par propriete) est la forme naturelle de ce suivi pour les proprietes pilotes.
- Mis a jour le 11 septembre 2026 (tache 4 livree) : pour les sept pilotes, le substitut runtime existe. Le store de chaque `MGElement` garde toutes les contributions (`LocalValue` = attribut XAML ou setter applicatif, `LocalBinding`, `ExplicitStyle`, `ImplicitStyle`, `DynamicResource`, `Template`, `Theme`, `DefaultValue`) et une ecriture `ImplicitStyle`/`ExplicitStyle` posterieure ne peut pas ecraser un `LocalValue` ni un `LocalBinding` : la precedence est appliquee a l'ecriture. Le DTO XAML expose aussi `Element.StyleProvenance` pendant le parse. Il reste a concevoir l'API de re-application elle-meme et le suivi des proprietes non pilotes.

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

- `MGControlTemplateStructure.DetachedRoots` existe (`MGUI.Core/UI/Styling/MGControlTemplate.cs`) mais n'a aucune semantique runtime: rien ne le lit, le loader aplatit les elements nommes dans `Structure.Parts` (`ControlTemplateLoader.cs:193-223`). Le contrat reel est "declarer une part requise de type `MGWindow`";
- `MGComboBox` declare la part requise `PART_DropdownWindow` de type `MGWindow` (`MGComboBox.cs:63`); la fenetre est construite par le template `ComboBox.Default` (`MGControlTemplateCatalog.cs:347`) et remplacable en XAML (`Dark.ComboBox`, `BuiltInControlTemplates.xaml:158-182`); le controle cable encore a la main `AddNestedWindow`/`RemoveNestedWindow` (`MGComboBox.cs:654`, `:662`);
- `MGContextMenu`, `MGToolTip`, `MGOverlay` posent `DefaultControlTemplateName` (`MGContextMenu.cs:729`, `MGToolTip.cs:84`, `MGOverlay.cs:580`);
- restent hors contrat: `MGFloatingDockWindow` (`MGUI.Core/UI/Docking/Controls/MGFloatingDockWindow.cs`, aucun hook structurel) et `MGColorPickerPopup` (`MGUI.Core/UI/Color/MGColorPickerPopup.cs:8`, classe sealed qui n'est pas un `MGElement`, construit un `MGWindow` brut a `:40`, consommee par `MGColorField`). Ce dernier ne peut pas porter de `ControlTemplate` aujourd'hui: c'est le cas qui exige l'extension minimale;
- l'inventaire des quatre surfaces popup et de leur regle `ActivatesOnClick = false` existe deja dans `Docs/input-window-activation-design.md` (lignes 159-172);
- tests existants d'ouverture/attachement/fermeture: `MGUI.Tests/Input/NestedWindowHoverOcclusionTests.cs`, `OverlappingWindowsInputRoutingTests.cs`, `ContextMenuHoverOcclusionTests.cs`, `MGUI.Tests/Modal/ContextMenuClickThroughTests.cs`; rien de dedie pour `MGFloatingDockWindow` ni `MGColorPickerPopup`.
- mis a jour le 11 septembre 2026 : le menu contextuel, ses sous-menus, la liste deroulante de `MGComboBox` (template code et XAML), `MGToolTip` (`MGToolTip.cs:78`) et la fenetre de `MGColorPickerPopup` (`MGColorPickerPopup.cs:40`) ne copient plus le theme de leur fenetre et suivent un changement de theme du scope proprietaire ; les items du ComboBox sont re-templates au refresh (`MGUI.Tests/Architecture/PopupThemeInheritanceTests.cs`). Numeros de ligne de cette section verifies le meme jour.

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
