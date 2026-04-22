# InkkSlinger vs MGUI Roadmap Tasks

## Objectif

Convertir la roadmap priorisee en backlog executable pour des agents IA, sans perdre l'ordre de livraison, les dependances entre lots, ni le cadrage technique retenu apres comparaison avec InkkSlinger.

Ce document est destine a un agent IA implementeur. Il sert de backlog de portefeuille. Quand une tache renvoie vers un backlog specialise deja present dans `Docs/`, l'agent doit utiliser ce backlog specialise comme source de verite de detail, tout en mettant a jour le statut de la tache correspondante dans ce fichier.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit git par tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Utiliser l'icone de statut dans le titre de chaque tache.
- Si une tache est bloquee, marquer la tache avec `⛔`, decrire le blocage juste sous la tache, puis s'arreter.
- Ne pas ouvrir deux taches principales en parallele.
- Preserver les APIs publiques existantes sauf si la tache demande explicitement de les etendre.
- Ajouter ou adapter tests, samples et documentation dans la meme tache quand c'est pertinent.
- Quand une tache s'appuie sur un backlog specialise existant, mettre a jour aussi ce backlog specialise avant le commit.
- Eviter les refontes globales non bornees; viser des increments verticaux, testables, et defendables.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale apres chaque tache

Utiliser une validation ciblee et bornee. Eviter les executions ouvertes non filtrees.

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter <FiltreZone>`
4. Si la tache touche surtout de la documentation, relire le fichier modifie et verifier que les statuts et liens sont coherents.
5. Commit avec le message recommande par la tache.

## Documents de reference obligatoires

- `Docs/inkkslinger-vs-mgui-comparison-report.md`
- `Docs/inkkslinger-vs-mgui-prioritized-roadmap.md`
- `Docs/style-theme-refactor-tasks.md`
- `Docs/theme-definition-tasks.md`
- `Docs/control-template-tasks.md`
- `Docs/rounded-shapes-phase2-roadmap.md`
- `Docs/shape-paint-and-content-clip-separation.md`
- `wpf-controls-gap-analysis.md`

## Ordre de commits attendu

1. `roadmap: complete task 1 lock execution matrix`
2. `diagnostics: complete task 2 add stable diagnostic ids`
3. `diagnostics: complete task 3 add snapshots replay and artifacts`
4. `roadmap: complete task 4 align lookless convergence backlog`
5. `lookless: complete task 5 deliver first convergence milestone`
6. `markup: complete task 6 harden loader diagnostics`
7. `docs: complete task 7 add scenario matrix and validation index`
8. `shapes: complete task 8 deliver retained shapes v1`
9. `grid: complete task 9 deliver datagrid lite v1`
10. `overlay: complete task 10 deliver adorner lite decorators`
11. `text: complete task 11 deliver targeted text improvements`

## Taches

### ✅ 1. Verrouiller la matrice d'execution de la roadmap

But:
transformer la roadmap de portefeuille en matrice de travail concrete avant d'ouvrir des chantiers de code plus lourds.

Travail attendu:

- relire le rapport comparatif et la roadmap priorisee ;
- figer la liste des lots, leurs dependances, leurs criteres de sortie et leurs validations cibles ;
- identifier pour chaque lot les docs de reference, les samples attendus et le filtre de test le plus pertinent ;
- ajouter dans ce fichier une courte section "Resultat" sous la tache pour resumer la matrice retenue ;
- verifier que les lots lookless referencent explicitement les backlogs specialises existants.

Criteres d'acceptation:

- l'ordre reel d'execution ne laisse pas d'ambiguite ;
- chaque lot a une validation ciblee et un point d'entree documentaire clair ;
- les dependances entre diagnostics, lookless, markup, samples et controles sont explicites.

Filtre de test recommande:

- `FullyQualifiedName~Focus|FullyQualifiedName~Theme|FullyQualifiedName~Template`

Resultat:

- matrice d'execution verrouillee en 4 vagues:
	- vague A: taches 2 et 3 pour le harness diagnostics minimal ;
	- vague B: taches 4, 5 et 6 pour la convergence lookless et le durcissement du loader ;
	- vague C: taches 7 et 8 pour l'index de scenarios et les shapes retained v1 ;
	- vague D: taches 9, 10 et 11 pour les controles/outils additionnels et la cloture de portefeuille ;
- points d'entree documentaires figes:
	- diagnostics: `Docs/inkkslinger-vs-mgui-prioritized-roadmap.md` + `MGUI.Core/Tooling/UIToolingService.cs` ;
	- lookless: `Docs/style-theme-refactor-tasks.md`, `Docs/theme-definition-tasks.md`, `Docs/control-template-tasks.md` ;
	- markup: `Docs/inkkslinger-vs-mgui-prioritized-roadmap.md` + tests XAML existants ;
	- shapes: `Docs/rounded-shapes-phase2-roadmap.md` + `Docs/shape-paint-and-content-clip-separation.md` ;
	- scenarios/samples: `MGUI.Samples/Features/FocusInputReview.xaml`, `MGUI.Samples/Features/StyleThemeRefactor.xaml`, `MGUI.Samples/Features/RoundedShapes.xaml`, `MGUI.Samples/Features/DockingDemo.cs` ;
- validations cibles retenues:
	- diagnostics: `FullyQualifiedName~Focus|FullyQualifiedName~Input|FullyQualifiedName~Overlay` ;
	- lookless et loader: `FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template|FullyQualifiedName~XAML|FullyQualifiedName~Markup` ;
	- shapes: `FullyQualifiedName~Shape|FullyQualifiedName~Clip` ;
	- controles additionnels: `FullyQualifiedName~Grid|FullyQualifiedName~Scroll|FullyQualifiedName~Text|FullyQualifiedName~Dock` ;
- dependances confirmees:
	- les identifiants et snapshots diagnostics precedent toute matrice scenario exploitable ;
	- la convergence lookless doit preceder la generalisation des samples et tout controle lourd comme un grid ;
	- les decorators overlay dependent d'un layering, d'un clipping et d'un focus deja durcis ;
	- les ameliorations textuelles restent en cloture pour eviter de glisser vers un chantier `RichTextBox` hors scope.

### ✅ 2. Ajouter des identifiants de diagnostic stables

But:
poser la base minimale du harness de diagnostics pour rendre les scenarios reproductibles entre tests, samples et artefacts.

Travail attendu:

- definir des identifiants stables pour desktop, fenetres, overlays, scopes et elements ;
- ajouter les premiers points d'attache des diagnostics sur les surfaces runtime critiques ;
- rendre ces identifiants exploitables depuis les tests et les samples ;
- documenter la forme retenue et les invariants de stabilite ;
- preparer l'extension future vers snapshots, replay et assertions.

Criteres d'acceptation:

- un element critique peut etre retrouve par un identifiant stable au runtime ;
- les identifiants n'imposent pas de couplage fragile ou de refonte d'API massive ;
- la base est suffisamment petite pour rester utilisable dans MGUI sans devenir un sous-systeme tentaculaire.

Filtre de test recommande:

- `FullyQualifiedName~Focus|FullyQualifiedName~Input|FullyQualifiedName~Overlay`

Resultat:

- points d'attache publics ajoutes sur le tooling partage: `UIToolingService.GetStableDiagnosticId(MGDesktop)` et `UIToolingService.GetStableDiagnosticId(MGElement)` ;
- le snapshot outillage transporte maintenant les deux niveaux d'identite utiles au harness: `DiagnosticId`/`WindowDiagnosticId` stables et `RuntimeUniqueId` pour la correlation intra-run ;
- forme retenue pour les ids stables: `desktop/<fenetre-racine-ou-overlay>/<segment...>` ;
- invariants documentes par l'API et verrouilles par tests:
	- un `Name` explicite prime sur un `TemplatePart` pour eviter de masquer les scopes et contenus nommes ;
	- un `TemplatePart` prend le relais quand l'element n'est pas nomme ;
	- a defaut, le segment tombe sur `type[index]` avec un ordinal borne au type sibling pour limiter les glissements ;
	- les fenetres imbriquees prolongent la chaine de leur `ParentWindow` et l'overlay desktop vit sous `desktop/overlay-window` ;
- validation ciblee executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~MGUI.Tests.Tooling.StableDiagnosticIdTests|FullyQualifiedName~MGUI.Tests.Architecture.ToolingHooksTests"`.

### ✅ 3. Ajouter snapshots, replay et artefacts de repro

But:
faire du harness de diagnostics un outil concret de reproduction et de regression sur les scenarios a risque.

Travail attendu:

- ajouter des snapshots structures d'arbre UI avec focus, visibilite effective, clipping, overlays actifs et sources de valeurs ;
- enregistrer et rejouer des sequences ciblees souris, clavier et navigation ;
- produire des artefacts lisibles en cas d'echec cible ;
- livrer au moins un sample de repro et une petite couche d'assertion exploitable depuis `MGUI.Tests` ;
- couvrir au minimum les scenarios overlay modal, template swap, theme switch, combo ou menu qui isole l'input, et docking drag.

Criteres d'acceptation:

- un scenario peut etre reproduit avec le meme identifiant entre sample et tests ;
- les artefacts d'echec rendent le diagnostic plus rapide qu'une simple reproduction manuelle ;
- la solution reste bornee et ne tente pas de reproduire toute la surface d'InkkOops.

Filtre de test recommande:

- `FullyQualifiedName~Focus|FullyQualifiedName~Overlay|FullyQualifiedName~Dock`

Resultat:

- ajout d'un snapshot desktop borne: `UIToolingService.CaptureDesktopSnapshot(MGDesktop)` capture focus actif, tooltip, context menu, overlay actif, input courant, fenetres racine, fenetres imbriquees et l'etat runtime des noeuds de visual tree ;
- chaque noeud de `UIVisualTreeSnapshot` transporte maintenant des signaux runtime directement exploitables par le harness: visibilite effective, hit-test, focus, hover, clipping et capacites d'input derivees ;
- ajout d'un artefact lisible `UIToolingService.RenderDesktopSnapshot(...)` pour produire un compte rendu texte immediat des scenarios de repro ;
- ajout d'un replay borne par frames avec assertions optionnelles: `UIToolingService.ReplayFrames(...)`, `UIInputReplayFrame`, `UIInputReplayResult` et `UIDiagnosticAssertions` ;
- couverture ciblee dans `MGUI.Tests` sur trois axes: snapshot desktop, rendu d'artefact et replay brut + action semantique `NavigateNext` ;
- point d'entree sample minimal livre dans `MGUI.Samples`: `F2` ecrit l'artefact diagnostics courant dans la sortie debug, ce qui donne un repro immediat sur les scenarios `FocusInputReview`/overlay/popup ;
- validation ciblee executee avec succes:
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~MGUI.Tests.Tooling.StableDiagnosticIdTests|FullyQualifiedName~MGUI.Tests.Architecture.ToolingHooksTests"`
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

### ✅ 4. Aligner le backlog de convergence lookless

But:
transformer les chantiers style, theme et control template en un seul flux de convergence pilote au niveau portefeuille.

Travail attendu:

- relire `Docs/style-theme-refactor-tasks.md`, `Docs/theme-definition-tasks.md` et `Docs/control-template-tasks.md` ;
- synchroniser leurs hypotheses avec `Docs/inkkslinger-vs-mgui-prioritized-roadmap.md` ;
- figer le premier jalon commun de convergence: precedence, invalidation, reevaluation runtime, template parts et controles pilotes ;
- mettre a jour si necessaire les backlogs specialises pour eviter les doublons ou contradictions ;
- ajouter un "Resultat" dans cette tache avec le jalon retenu et les controles pilotes choisis.

Criteres d'acceptation:

- il existe un premier jalon lookless commun, bornant ce qui sera reellement implemente avant le docking et les controles plus lourds ;
- les backlogs specialises pointent dans la meme direction ;
- la suite d'implementation peut commencer sans ambiguite de perimetre.

Filtre de test recommande:

- `FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template`

Resultat:

- un document de convergence commun `Docs/lookless-convergence-milestone.md` borne maintenant explicitement le premier jalon portefeuille lookless ;
- invariants figes pour ce jalon: precedence explicable, reevaluation runtime sans reparse complet, `TemplateParts` validables, convergence `ThemeDefinition` + `Style` + `ControlTemplate` sur le meme runtime ;
- controles pilotes retenus pour le lot 5: `MGWindow`, `MGOverlay`, `MGListBox`, `MGListView`, `MGComboBox` et `MGTabControl` ;
- alignement documentaire ajoute dans les trois backlogs specialises:
	- `Docs/style-theme-refactor-tasks.md` cadre le jalon comme la cible portefeuille minimale, sans rouvrir tout le backlog ;
	- `Docs/theme-definition-tasks.md` cadre explicitement la chaine `ThemeDefinition` -> `MGTheme` -> `MGResources` comme contribution au jalon ;
	- `Docs/control-template-tasks.md` borne le jalon aux migrations structurelles des controles pilotes et laisse hors scope immediat les taches docking hybrides et fenetres auxiliaires ;
- la roadmap priorisee pointe maintenant ce jalon commun comme reference de lot 2, ce qui supprime l'ambiguite entre migration pre-docking et backlogs specialises plus vastes.

### ✅ 5. Livrer le premier jalon de convergence lookless

But:
fiabiliser la resolution runtime de style, theme, template, ressources et visual states sur un premier ensemble de controles de reference.

Travail attendu:

- implementer le jalon defini a la tache 4 ;
- faire converger `ThemeDefinition`, `Style`, `ControlTemplate`, ressources et visual states sur un meme chemin de resolution ;
- fiabiliser la reevaluation runtime sur changement de theme, template ou ressource ;
- migrer un premier groupe de controles pilotes avant le docking ;
- ajouter les tests et samples necessaires pour prouver la stabilite du jalon.

Criteres d'acceptation:

- un changement de theme ou de ressource reevalue un sous-arbre sans reparse complet ;
- les valeurs visibles ont une precedence explicable et testable ;
- les controles pilotes deplacent une part significative de leur skinning hors du code imperatif.

Filtre de test recommande:

- `FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template`

Resultat:

- le premier jalon de convergence lookless est maintenant valide sur le socle commun `ThemeDefinition` + `Style` + `ControlTemplate` borne par `Docs/lookless-convergence-milestone.md` ;
- controles pilotes confirmes dans le jalon portefeuille: `MGWindow`, `MGOverlay`, `MGListBox`, `MGListView`, `MGComboBox` et `MGTabControl` ;
- correctifs locaux livres pour refermer la tranche rouge restante:
	- `MGContextMenu` applique son focus initial avec `KeyboardFocusSource.Pointer`, ce qui evite l'autoscroll de viewports ancetres a l'ouverture de menus flottants ;
	- `MGContextMenuItem` projette l'etat de highlight a partir du wrapper, du focus, de la selection et de l'ouverture de sous-menu sans perdre les invariants de template ;
	- les wrappers XAML `StackPanel`, `WrapPanel` et `Canvas` n'heritent plus des implicit styles via leur `Border` interne ;
- le document specialise `Docs/style-theme-refactor-tasks.md` et le cadrage commun `Docs/lookless-convergence-milestone.md` ont ete mis a jour pour acter la validation portefeuille ;
- validation ciblee executee avec succes:
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template"`
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

### ✅ 6. Durcir le loader XAML et ses diagnostics

But:
rendre le chargement XAML previsible, diagnostiquable et compatible avec une migration progressive.

Travail attendu:

- inventorier les echec actuels du loader: types inconnus, setters incompatibles, ressources introuvables, template parts manquantes, conversions invalides ;
- introduire une taxonomie d'erreurs avec code, localisation, message et contexte minimal ;
- ajouter un mode strict activable pour les themes et templates de reference ;
- conserver un mode compatibilite pour les migrations progressives ;
- creer des fixtures negatives et des tests de validation pour les erreurs attendues.

Criteres d'acceptation:

- un echec de chargement pointe la zone fautive avec un message actionnable ;
- le mode strict detecte au minimum les erreurs de ressources, types, setters et template parts ;
- le mode compatibilite permet une migration progressive sans rupture globale brutale.

Filtre de test recommande:

- `FullyQualifiedName~XAML|FullyQualifiedName~Markup|FullyQualifiedName~Template`

Resultat:

- ajout d'une enveloppe de diagnostic XAML structuree: `XamlLoaderMode`, `XamlLoaderDiagnosticCode`, `XamlLoaderDiagnostic` et `XamlLoaderException` ;
- `XAMLParser`, `ThemeDefinitionLoader` et `ControlTemplateLoader` exposent maintenant un chemin `Strict` distinct du chemin `Compatibility`, sans casser les APIs historiques ;
- le mode strict valide explicitement les types XAML inconnus, les setters invalides, les racines de document non supportees et les `TemplatePart` requises absentes, puis remonte un diagnostic avec source et ligne quand disponible ;
- `MGResources` expose ces chemins stricts pour les themes et templates XAML, ce qui borne mieux le chargement des assets de reference ;
- le sample de validation `MGXAMLDesigner` consomme desormais le chemin strict et affiche un message de diagnostic structure au lieu d'une exception brute ;
- couverture ajoutee dans `MGUI.Tests` avec des fixtures negatives ciblees sur type inconnu, mode compat legacy, setter invalide, racine de document invalide et part manquante ;
- validation ciblee executee avec succes:
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~XAML|FullyQualifiedName~Markup|FullyQualifiedName~Template"`
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~XamlLoaderDiagnosticsTests"`
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

### ✅ 7. Ajouter la matrice de scenarios, repros et docs de validation

But:
faire des scenarios de validation une surface de premier ordre du repo, pas une activite annexe.

Travail attendu:

- definir une matrice de scenarios par sous-systeme: input, focus, layout, overlay, clipping, theme, template, docking, text ;
- garantir au moins un sample de demonstration ou de repro pour chaque zone a risque deja ouverte par les taches precedentes ;
- normaliser les identifiants, titres et points d'entree de ces scenarios ;
- relier chaque scenario a un invariant documente et, si possible, a un test cible ;
- ajouter une page d'index dans la doc qui sert de point d'entree aux regressions frequentes.

Criteres d'acceptation:

- chaque chantier prioritaire deja ouvert a un scenario de validation visible ;
- un bug peut etre rattache a un scenario nomme plutot qu'a une description libre ;
- la documentation et les samples convergent vers les memes identifiants de scenario.

Filtre de test recommande:

- `FullyQualifiedName~Focus|FullyQualifiedName~Theme|FullyQualifiedName~Template|FullyQualifiedName~Dock`

Resultat:

- ajout d'un index dedie `Docs/scenario-validation-index.md` qui fixe la convention `SCN-<zone>-<nnn>` et la matrice initiale des scenarios de validation prioritaires ;
- les points d'entree visibles du repo sont maintenant normalises autour de `SCN-FOCUS-001`, `SCN-OVERLAY-001`, `SCN-THEME-001`, `SCN-MARKUP-001`, `SCN-LAYOUT-001`, `SCN-SHAPE-001`, `SCN-DOCK-001` et `SCN-TEXT-001` ;
- les fenetres samples de reference exposent ces memes identifiants dans leur titre, ce qui aligne la documentation, le compendium et les artefacts diagnostics sans ouvrir une nouvelle couche d'infrastructure ;
- validation ciblee executee avec succes:
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

### ✅ 8. Livrer les shapes retained v1

But:
exposer une premiere surface de shapes UI retained en capitalisant sur les primitives de rendu, sans melanger paint et clipping.

Travail attendu:

- reutiliser `Docs/rounded-shapes-phase2-roadmap.md` et `Docs/shape-paint-and-content-clip-separation.md` comme references de design ;
- definir une surface v1 pour `Ellipse`, `Line`, `Polygon`, `Polyline` et `PathLite` ;
- partager la meme semantique de forme entre paint, clip et hit testing ;
- adopter au moins un chemin de clipping non rectangulaire sur un controle pilote ;
- livrer les tests logiques et le sample dedie correspondant.

Criteres d'acceptation:

- les shapes de base peuvent etre declarees, rendues et stylisees sans logique backend specifique dans la couche controle ;
- le hit testing shape-aware est present la ou il a une valeur semantique reelle ;
- la separation entre shape paint et content clip reste intacte.

Filtre de test recommande:

- `FullyQualifiedName~Shape|FullyQualifiedName~Clip`

Resultat:

- ajout d'une surface retained v1 pour `Ellipse`, `Line`, `Polygon`, `Polyline` et `PathLite`, avec exposition runtime et XAML au-dessus des primitives `FillPolygon`, `StrokeAndFillPolygon` et `StrokeLineSegment` deja presentes ;
- ajout d'un hook shape-aware dans `MGElement` pour que le hover et le hit testing puissent suivre la silhouette utile au lieu du seul rectangle englobant ;
- adoption d'un chemin de clip non rectangulaire sur `Ellipse` via `ClipDefinition.ArbitraryGeometry`, sans casser la separation entre paint de forme et pipeline de clip ;
- ajout de tests geometriques cibles et d'un test d'integration `XAML -> desktop -> proof backend` qui valide draw calls et clip non rectangulaire ;
- extension du sample `RoundedShapes` pour couvrir explicitement la nouvelle surface retained v1 et la verification manuelle du clip courbe ;
- validation ciblee executee avec succes:
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RetainedShapeGeometryTests|EngineOwnedRenderingProofTests"`
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

### ✅ 9. Livrer un DataGrid-lite v1 pour outils et debug

But:
combler l'ecart fonctionnel le plus utile cote outillage sans importer une architecture desktop lourde.

Travail attendu:

- figer un scope v1 centre sur lecture, tri, selection, headers, scrolling et sizing de colonnes ;
- reutiliser les primitives de liste, scroll et presentation deja presentes ;
- fournir un sample dedie avec dataset realiste et colonnes heterogenes ;
- tester explicitement tri, selection et scroll ;
- exclure explicitement du v1 l'edition riche, le grouping, la hierarchie, les formules et les families de bureau lourdes.

Criteres d'acceptation:

- le controle couvre un besoin reel d'outil sans basculer dans une logique de framework desktop ;
- le scope v1 reste ferme et defendable ;
- les scenarios de tri, selection et scroll sont demonstrables et testes.

Filtre de test recommande:

- `FullyQualifiedName~Grid|FullyQualifiedName~ListView|FullyQualifiedName~Scroll`

Resultat:

- ajout d'une facade `MGDataGrid<TItemType>` au-dessus de `MGListView<TItemType>` pour fermer le besoin outillage/debug sans reintroduire une architecture desktop lourde ;
- ajout d'une API outillage centree sur les lignes et les colonnes: `AddTextColumn`, `AddTemplateColumn`, `SelectRow`, `EnsureRowVisible`, `SortByColumnIndex`, `ResizeColumnPixels` et `ResizeColumnWeight` ;
- ajout d'une petite specialisation de colonne pour les headers texte avec indicateur de tri `▲/▼`, sans dupliquer la mecanique interne de `MGListView` ;
- ajout d'une surface XAML `DataGrid` qui reutilise le chargement et le template `ListView`, plus un sample dedie `SCN-GRID-001` raccorde au compendium et a l'index de scenarios ;
- ajout de tests d'integration cibles pour la selection de ligne, le tri, le scroll et le parse XAML du nouveau controle ;
- validation ciblee executee avec succes:
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "DataGridLiteTests"`
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "FullyQualifiedName~Grid|FullyQualifiedName~ListView|FullyQualifiedName~Scroll"`

### ⚪ 10. Livrer un Adorner-lite et des overlay decorators

But:
offrir une couche legere pour selection boxes, handles, guides et drop indicators sans introduire un sous-systeme WPF complet.

Travail attendu:

- definir un modele de decorators ancre sur un element cible ;
- supporter les usages prioritaires: selection, resize handles, drop indicators, guides de debug ;
- cadrer clairement layering, clipping et hit testing ;
- limiter la surface a des usages de tooling et de debug ;
- prouver l'integration avec au moins un scenario de docking ou d'overlay.

Criteres d'acceptation:

- les overlays de tooling ne reposent plus sur des chemins one-off fragiles ;
- les decorators respectent clipping et priorites d'input ;
- la surface reste petite, lisible et adaptee a MGUI.

Filtre de test recommande:

- `FullyQualifiedName~Overlay|FullyQualifiedName~Dock|FullyQualifiedName~Focus`

### ⚪ 11. Livrer les ameliorations textuelles ciblees et cloturer la roadmap

But:
ameliorer les usages chat, log, debug et texte annote sans lancer un RichTextBox complet, puis cloturer la boucle documentaire de la roadmap.

Travail attendu:

- identifier les manques textuels reellement utiles a MGUI avant toute extension large ;
- ajouter seulement les primitives textuelles necessaires pour les scenarios dominants ;
- fournir des samples cibles pour chat, console ou log et texte annote ;
- documenter explicitement ce qui reste hors scope d'un vrai document editor ;
- mettre a jour la roadmap priorisee et le rapport comparatif pour signaler l'etat d'avancement de la livraison.

Criteres d'acceptation:

- les scenarios chat, log et debug n'impliquent plus automatiquement le besoin d'un RichTextBox complet ;
- les extensions textuelles restent compactes et compatibles avec le runtime MGUI ;
- la documentation de portefeuille indique clairement ce qui a ete livre, reporte ou laisse hors scope.

Filtre de test recommande:

- `FullyQualifiedName~Text|FullyQualifiedName~Chat|FullyQualifiedName~Focus`

## Hors perimetre de ce backlog

- pas de `DependencyObject` ou `DependencyProperty` generalises dans tout le framework ;
- pas de moteur complet de triggers WPF comme prerequis ;
- pas de routed commands ou routed events partout ;
- pas de portage direct des familles `DocumentViewer`, `Frame`, `Page`, `Ribbon` ou `FlowDocument` ;
- pas de retained rendering sophistique ou de dirty regions generalises sans profilage cible.