# Plan agent IA - Controle de graphe visuel MGUI

Ce document transforme `Docs/graph_node_system_features.md` en plan d'execution pour un agent IA. Le fichier source est une tres bonne vision fonctionnelle; ce plan ajoute la critique, les choix d'architecture MGUI, les limites de la V1, les validations et la discipline de commits.

## Source fonctionnelle analysee

Lire d'abord `Docs/graph_node_system_features.md` en entier.

Objectif fonctionnel extrait du document source:

- ajouter a MGUI un controle generique de graphe a noeuds, pas un editeur Blueprint specifique;
- separer modele, UI, interactions, validation, serialization et eventuelle compilation;
- fournir une V1 utile avec pan, zoom, grille, noeuds, ports, edges, selection, creation/suppression, undo/redo minimal, serialization JSON, comments simples et sample;
- garder les usages metiers, comme material graph ou dialogue graph, hors du controle generique.

## Critique du document source

### Points solides

- La direction produit est claire: le controle doit rester generique et reutilisable pour plusieurs domaines.
- La separation modele / UI est correctement identifiee comme le premier invariant a proteger.
- Les ports types, les GUID stables, la serialization versionnee et la validation sont bien places au centre de l'architecture.
- Le document pense deja aux gros graphes: virtualisation, caches, dirty flags, spatial index, eviter les allocations par frame.
- Le decoupage V1 / V2 / V3 evite de confondre le controle de base avec les usages avancés comme shader graph, visual scripting ou debug runtime.

### Lacunes a corriger avant implementation

- Le document est une matrice de fonctionnalites, pas encore un plan executable: il manque les fichiers cibles reels, les contrats de tests, les commits et les criteres de sortie par etape.
- La V1 reste trop large si elle est implementee d'un bloc. Elle doit etre decoupee en tranches verticales: modele pur, transform, commandes, serialization, shell UI, rendu, interactions, sample.
- L'integration MGUI n'est pas assez precise: `MGElement`, `MGSingleContentHost`, `MGControlTemplateCatalog`, `MGElementType`, XAML wrappers, theme et TemplateParts doivent etre traites explicitement.
- Le document ne distingue pas assez les elements UI reels des objets rendus. Pour la performance, les edges ne devraient pas etre des centaines ou milliers de controles `MGElement` dans la V1; ils doivent plutot etre des modeles rendus par un renderer cache.
- Le rendu Bezier est demande, mais l'API de rendu MGUI expose surtout des segments de ligne dans les controles existants. La V1 doit prevoir un echantillonnage Bezier en polyligne cachee, puis une evolution renderer plus tard si necessaire.
- Le clipping est cite, mais pas specifie. Le plan doit utiliser les contrats `GetSelfClipDefinition` / `GetContentsClipDefinition` et eviter les appels directs a SpriteBatch, scissor ou stencil depuis le controle.
- Le systeme input n'est pas assez cadre: pan, zoom, drag de noeud, drag de connexion, selection rectangle et navigation clavier doivent cohabiter avec le routage MGUI, le focus et les evenements deja consommes.
- L'undo/redo est correctement juge important, mais le depot ne fournit pas un framework global d'undo pour tous les controles. Il faut donc livrer un stack local au graphe, qui modifie uniquement le modele.
- La serialization doit etre testable sans UI. Il faut des DTO versionnes et des migrations minimales, pas une serialization directe de controles.
- Le theme et les styles sont listes, mais pas relies aux mecanismes actuels: `MGTheme`, `BuiltInThemes.xaml`, `ThemeDefinitionBuilder`, `VisualStateFillBrush` et templates.
- Le sample recommande est pertinent, mais il doit etre un scenario de validation, pas seulement une demo visuelle. Il doit prouver creation, connexion, sauvegarde, chargement et undo/redo.

### Decision de cadrage V1

La V1 doit livrer un controle de graphe generique utilisable et testable, avec un sample Dialogue Graph. Elle ne doit pas livrer blackboard, minimap, named reroutes, sous-graphes, compiler, preview live, visual scripting complet, material compiler ou debug runtime.

## Discipline obligatoire pour l'agent IA

Icones de statut a utiliser devant chaque tache:

- `⚪` a faire;
- `🟡` en cours;
- `✅` termine;
- `⛔` bloque.

Regles de travail:

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit git par tache terminee.
- Mettre a jour l'icone dans le titre de la tache avant chaque commit.
- Au debut d'une tache, remplacer `⚪` par `🟡` dans ce fichier.
- A la fin d'une tache, remplacer `🟡` par `✅`, remplir la section `Resultat`, lancer les validations indiquees, puis committer.
- Si une tache bloque, remplacer l'icone par `⛔`, documenter le blocage sous le titre et s'arreter sans commencer la suivante.
- Ne jamais commencer la tache suivante tant que la tache courante n'est pas validee et committee.
- Avant chaque commit: lancer `rtk git status`, verifier les fichiers modifies, et ne stage que les fichiers lies a la tache courante.
- Ne pas modifier ni revert des changements hors perimetre deja presents dans le workspace.
- Ne pas faire de refactor massif hors perimetre.
- Preserver les APIs publiques existantes. Si une signature publique doit changer, garder une compatibilite par overload ou API obsolete qui forward.
- Ajouter ou adapter les tests a chaque tache quand la logique est testable.
- Eviter LINQ et allocations temporaires dans les chemins `Update`, `Draw`, hit-test et layout.
- Garder separes: modele, validation, commandes, serialization, transform, hit-test, layout, rendu, controles visuels.
- Le controle ne doit pas dessiner son chrome dans `DrawSelf`; le chrome vient des templates et du theme.

## Fichiers a inspecter avant de coder

- `MGUI.Core/UI/MGElement.cs` pour cycle de vie, input, draw, clips et `DefaultControlTemplateName`.
- `MGUI.Core/UI/Containers/MGContentHost.cs` pour `MGSingleContentHost` et `MGMultiContentHost`.
- `MGUI.Core/UI/Containers/MGCanvas.cs` et `MGUI.Core/UI/Containers/MGCanvasLayoutEngine.cs` pour positionnement absolu existant.
- `MGUI.Core/UI/MGPropertyGrid.cs` pour structure lookless recente, TemplateParts et refresh performant.
- `MGUI.Core/UI/MGTreeView.cs`, `MGUI.Core/UI/MGListView.cs` et `MGUI.Core/UI/MGListBox.cs` pour selection, navigation, data display et templates.
- `MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs` pour ajout du template par defaut.
- `MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml` pour templates XAML embarques.
- `MGUI.Core/UI/Themes/BuiltInThemes.xaml` pour valeurs de theme built-in.
- `MGUI.Core/UI/XAML/Controls.cs` ou un nouveau fichier XAML dedie pour exposer `GraphView`.
- `MGUI.Core/UI/DragDrop/DragDropManager.cs` pour drag/drop externe; ne pas le confondre avec le drag interne de noeuds/ports.
- `MGUI.Core/UI/InputRouting/MGUIInputContext.cs` et `MGUI.Core/UI/Navigation/` pour routage clavier et focus.
- `MGUI.Core/UI/MGLine.cs`, `MGUI.Core/UI/MGPolyline.cs`, `MGUI.Core/UI/MGPathLite.cs` pour patterns de rendu vectoriel et hit-test.
- `MGUI.Tests/Architecture/`, `MGUI.Tests/Focus/`, `MGUI.Tests/DragDrop/`, `MGUI.Tests/PropertyGrid/` pour style de tests.
- `MGUI.Samples/Features/` et `MGUI.Samples/Compendium.xaml` pour ajout du sample.

## Architecture cible V1

## Contrat V1 confirme

- Les controles publics V1 vivent dans le namespace `MGUI.Core.UI`: `MGGraphView`, `MGGraphNode`, `MGGraphPort` et `MGGraphCommentBox`.
- Les modeles et services purs vivent dans le namespace `MGUI.Core.UI.Graph` et restent utilisables sans `MGWindow`, `MGElement`, `GraphicsDevice` ou sample.
- `MGGraphView` est le seul responsable du rendu des edges V1. Une edge n'est pas un `MGElement` par connexion.
- Les edges Bezier V1 sont echantillonnees en polylignes cachees, car le renderer MGUI expose surtout des primitives de segments.
- L'undo/redo V1 est local au graphe via `GraphCommandStack`; les commandes modifient le modele, puis l'UI se synchronise.
- La serialization V1 est JSON, versionnee, basee sur les DTO du modele et sans serialization de controles.
- Le sample V1 est un Dialogue Graph simple. Il valide creation, connexion, deplacement, undo/redo, validation et round-trip JSON.
- Les fonctions suivantes restent hors scope V1: blackboard, minimap, bookmarks, reroutes, named reroutes, sous-graphes, preview live, compiler metier, visual scripting complet, debug runtime et graph diff/merge.

### Namespaces recommandes

- Controles publics: namespace `MGUI.Core.UI`.
- Modeles et services purs: namespace `MGUI.Core.UI.Graph`.
- Support XAML: namespace `MGUI.Core.UI.XAML`.

### Dossiers cibles probables

```text
MGUI.Core/UI/Graph/
  Model/
  Commands/
  Validation/
  Serialization/
  Interaction/
  Rendering/
  Controls/
MGUI.Core/UI/XAML/Graph.cs
MGUI.Tests/Graph/
MGUI.Samples/Features/GraphViewDialogue.xaml
MGUI.Samples/Features/GraphViewDialogue.xaml.cs
```

### Types publics V1 attendus

- `MGGraphView`
- `MGGraphNode`
- `MGGraphPort`
- `GraphDocument`
- `GraphNodeModel`
- `GraphPortModel`
- `GraphEdgeModel`
- `GraphCommentModel`
- `GraphValueType`
- `GraphPortDirection`
- `GraphPortCardinality`
- `GraphViewportTransform`
- `GraphTypeCompatibilityService`
- `GraphValidationResult`
- `IGraphCommand`
- `GraphCommandStack`
- `GraphSerializer`

### TemplateParts V1 attendues

Pour `MGGraphView`:

- `PART_OuterBorder` : `MGBorder`
- `PART_ViewportHost` : conteneur principal qui clippe le graphe
- `PART_NodesCanvas` : host des noeuds visibles
- `PART_OverlayPanel` : selection rectangle, decorations, feedback drag

Pour `MGGraphNode`:

- `PART_OuterBorder` : `MGBorder`
- `PART_HeaderHost` : header/titre
- `PART_InputPortsPanel` : ports input
- `PART_OutputPortsPanel` : ports output
- `PART_ContentHost` : contenu custom du noeud

### Hors perimetre V1

- blackboard;
- minimap;
- bookmarks;
- reroutes et named reroutes;
- sous-graphes;
- compiler ou evaluator metier;
- preview live;
- debug runtime;
- reflection/plugin node library;
- drag/drop asset CasaEngine complet;
- graph diff/merge;
- visual scripting complet.

## Validation minimale

Adapter les filtres au contenu exact de la tache, mais garder une validation bornee.

Pour une tache de modele pur:

1. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "Graph"`

Pour une tache touchant MGUI.Core:

1. `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
2. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "Graph|Architecture"`

Pour une tache touchant XAML, themes ou samples:

1. `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "Graph|Xaml|Theme|Architecture"`

Pour une tache purement documentaire:

1. verifier manuellement les chemins cites;
2. lancer `rtk git diff` avant commit.

## Ordre de commits attendu

1. `docs: complete graph task 01 define v1 contract`
2. `test: complete graph task 02 add model contract tests`
3. `feat: complete graph task 03 add graph document model`
4. `test: complete graph task 04 add viewport transform tests`
5. `feat: complete graph task 05 add viewport transform`
6. `feat: complete graph task 06 add type compatibility validation`
7. `feat: complete graph task 07 add command stack`
8. `feat: complete graph task 08 add versioned serialization`
9. `feat: complete graph task 09 add render geometry services`
10. `ui: complete graph task 10 register element types and theme tokens`
11. `ui: complete graph task 11 add graph node and port controls`
12. `ui: complete graph task 12 add graph view shell`
13. `ui: complete graph task 13 sync document to visible nodes`
14. `ui: complete graph task 14 render grid and edges`
15. `input: complete graph task 15 add pan zoom and frame commands`
16. `input: complete graph task 16 add selection and node dragging`
17. `input: complete graph task 17 add port connection interaction`
18. `input: complete graph task 18 add keyboard editing commands`
19. `ui: complete graph task 19 add context menu node creation`
20. `feat: complete graph task 20 add simple comments`
21. `sample: complete graph task 21 add dialogue graph sample`
22. `perf: complete graph task 22 add culling and cache safeguards`
23. `docs: complete graph task 23 document graph view v1`
24. `test: complete graph task 24 stabilize graph scenario matrix`

## Taches

### ✅ Tache 01 - Verrouiller le contrat V1 et les decisions d'architecture

But:
figer un contrat implementeable avant d'ajouter des types publics durables.

Travail attendu:

- relire `Docs/graph_node_system_features.md` et ce plan;
- confirmer les noms publics V1;
- confirmer si les controles publics vivent dans `MGUI.Core.UI` et les services dans `MGUI.Core.UI.Graph`;
- ecrire une courte section `Contrat V1 confirme` dans ce fichier ou dans un document associe;
- confirmer que les edges V1 sont rendus par `MGGraphView`, pas comme des `MGElement` par edge;
- confirmer que les courbes Bezier V1 sont echantillonnees en polylignes cachees;
- confirmer que l'undo/redo local modifie uniquement le modele;
- confirmer que le sample V1 sera un Dialogue Graph simple;
- lister explicitement les fonctionnalites V2/V3 non commencees.

Criteres d'acceptation:

- le scope V1 est borne et ne contient pas de blackboard, minimap, compiler ou preview live;
- les decisions sur modeles, controles, templates, rendu et input sont explicites;
- les taches suivantes peuvent etre executees sans re-decider le perimetre.

Validation recommandee:

- verification documentaire;
- `rtk git diff`.

Commit recommande:

- `docs: complete graph task 01 define v1 contract`

Resultat:

- Contrat V1 confirme dans la section dediee de ce fichier.
- Noms publics confirmes: `MGGraphView`, `MGGraphNode`, `MGGraphPort`, `MGGraphCommentBox`, `GraphDocument`, `GraphNodeModel`, `GraphPortModel`, `GraphEdgeModel`, `GraphCommentModel`, `GraphViewportTransform`, `GraphCommandStack`, `GraphSerializer`.
- Frontiere confirmee: controles publics dans `MGUI.Core.UI`, modeles/services purs dans `MGUI.Core.UI.Graph`, support XAML dans `MGUI.Core.UI.XAML`.
- Edges confirmees comme rendu gere par `MGGraphView`, avec geometrie Bezier echantillonnee en polyligne cachee.
- Undo/redo confirme comme stack local au graphe, modifiant uniquement le modele.
- Sample V1 confirme: Dialogue Graph simple.
- Validation documentaire effectuee par relecture du plan et verification des chemins cites.
- Commit effectue: `docs: complete graph task 01 define v1 contract`.

### ✅ Tache 02 - Ajouter les tests de contrat du modele graphe

But:
verrouiller la logique pure avant d'introduire le controle visuel.

Travail attendu:

- creer `MGUI.Tests/Graph/GraphDocumentModelTests.cs`;
- couvrir la creation d'un document vide avec version V1;
- couvrir l'ajout de noeuds avec `Guid` stable;
- couvrir ports input/output, cardinalite et types;
- couvrir l'ajout d'edges avec source/target valides;
- couvrir les erreurs attendues: node absent, port absent, direction incorrecte, edge duplicate si interdit;
- couvrir suppression de noeud avec suppression des edges dependants;
- couvrir metadata editor sans dependance UI;
- les tests peuvent echouer avant implementation de la tache 03.

Criteres d'acceptation:

- les tests expriment le comportement attendu sans `MGWindow`, `GraphicsDevice` ou rendu;
- les noms de types publics sont ceux valides en tache 01;
- les cas limites principaux sont visibles dans les noms de tests.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphDocumentModelTests"`

Commit recommande:

- `test: complete graph task 02 add model contract tests`

Resultat:

- Ajout de `MGUI.Tests/Graph/GraphDocumentModelTests.cs` avec une matrice de contrat couvrant document vide, noeuds, ports, edges valides, edges invalides, duplicates, suppression de noeud et metadata editor.
- Les tests utilisent la reflection pour rester compilables avant l'ajout concret des types en tache 03.
- Validation attendue avant implementation: les tests expriment le contrat et deviendront verts lorsque le modele sera ajoute.
- Commit effectue: `test: complete graph task 02 add model contract tests`.

### ✅ Tache 03 - Ajouter le modele non visuel du graphe

But:
fournir le noyau testable utilise par l'UI, la validation, les commandes et la serialization.

Travail attendu:

- ajouter les modeles dans `MGUI.Core/UI/Graph/Model/`;
- implementer `GraphDocument` avec `Version`, collections de noeuds, edges et comments;
- implementer `GraphNodeModel` avec `Id`, `NodeType`, `Title`, `Position`, `Size`, ports, properties, metadata editor, collapsed state;
- implementer `GraphPortModel` avec `Id`, `NodeId`, `Name`, `Direction`, `ValueType`, `Cardinality`, `IsRequired`, `DefaultValue` minimal;
- implementer `GraphEdgeModel` avec `Id`, source node/port, target node/port et metadata de rendu minimal;
- implementer `GraphCommentModel` avec `Id`, bounds, title, text, color optionnelle;
- fournir des methodes explicites: `AddNode`, `RemoveNode`, `AddPort`, `Connect`, `Disconnect`, `TryGetNode`, `TryGetPort`, `GetEdgesForNode`;
- eviter que le modele connaisse `MGElement`, `MGWindow`, `MGBorder`, `MGTheme` ou des controles;
- faire passer les tests de la tache 02.

Criteres d'acceptation:

- le modele compile sans dependance UI runtime;
- les GUID sont stables et jamais regeneres pendant une mutation normale;
- supprimer un noeud nettoie les edges dependants;
- les tests de contrat passent.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphDocumentModelTests"`
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`

Commit recommande:

- `feat: complete graph task 03 add graph document model`

Resultat:

- Ajout du noyau pur dans `MGUI.Core/UI/Graph/Model/`: enums de ports/types, `GraphDocument`, `GraphNodeModel`, `GraphPortModel`, `GraphEdgeModel` et `GraphCommentModel`.
- `GraphDocument` expose creation/suppression de noeuds, ajout de ports, connexion/deconnexion, recherche node/port/edge/comment et nettoyage des edges dependantes lors d'une suppression de noeud.
- Les modeles ne dependent pas des controles MGUI et restent testables sans fenetre ni renderer.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphDocumentModelTests"`.
- Resultat validation: 6 tests passes, 0 echec; avertissements existants ou nullable dans les tests de contrat.
- Commit effectue: `feat: complete graph task 03 add graph document model`.

### ✅ Tache 04 - Ajouter les tests du transform viewport et des coordonnees

But:
verrouiller les conversions monde / viewport / layout avant l'input et le rendu.

Travail attendu:

- creer `MGUI.Tests/Graph/GraphViewportTransformTests.cs`;
- couvrir conversion world -> viewport et viewport -> world;
- couvrir zoom centre sur un point souris;
- couvrir pan;
- couvrir clamp de zoom min/max;
- couvrir snapping grille independant du zoom;
- couvrir precision et round-trip avec positions negatives;
- couvrir frame origin, frame selection et frame all avec bounds connus.

Criteres d'acceptation:

- les tests ne dependent pas de `MGGraphView`;
- les calculs attendus sont deterministes;
- les cas de positions negatives sont couverts, car le graphe est virtuellement infini.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphViewportTransformTests"`

Commit recommande:

- `test: complete graph task 04 add viewport transform tests`

Resultat:

- Ajout de `MGUI.Tests/Graph/GraphViewportTransformTests.cs` couvrant round-trip monde/viewport, pan, zoom centre souris, clamp min/max, snapping grille, frame bounds et frame origin.
- Les tests utilisent la reflection pour rester compilables avant l'ajout concret de `GraphViewportTransform` en tache 05.
- Validation attendue avant implementation: le projet compile, les tests echouent tant que le type n'existe pas.
- Commit effectue: `test: complete graph task 04 add viewport transform tests`.

### ✅ Tache 05 - Ajouter `GraphViewportTransform`

But:
centraliser pan, zoom, conversions et operations de cadrage.

Travail attendu:

- ajouter `GraphViewportTransform` dans `MGUI.Core/UI/Graph/Interaction/` ou `Model/` selon decision de tache 01;
- exposer `Pan`, `Zoom`, `MinZoom`, `MaxZoom`, `GridSize`;
- implementer `WorldToViewport`, `ViewportToWorld`, `WorldToLayout`, `LayoutToWorld` si necessaire;
- implementer `ZoomAt(viewportPoint, zoomDelta)` en gardant le point monde sous la souris;
- implementer `PanBy`;
- implementer `SnapPoint`;
- implementer `FrameBounds`, `FrameOrigin`, `FrameAll` avec padding;
- garantir l'absence d'allocation dans les methodes appelees par frame.

Criteres d'acceptation:

- les tests de la tache 04 passent;
- le transform est utilisable sans UI;
- aucune logique de rendu n'est melangee au transform.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphViewportTransformTests"`
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`

Commit recommande:

- `feat: complete graph task 05 add viewport transform`

Resultat:

- Ajout de `GraphViewportTransform` dans `MGUI.Core/UI/Graph/Interaction/`.
- Le transform gere pan, zoom avec clamps, conversion monde/viewport/layout, zoom centre souris, snapping grille, frame origin, frame bounds et frame all sur des noeuds.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphViewportTransformTests"`.
- Resultat validation: 7 tests passes, 0 echec; avertissements existants ou nullable dans les tests de contrat.
- Commit effectue: `feat: complete graph task 05 add viewport transform`.

### ✅ Tache 06 - Ajouter compatibilite de types et validation simple

But:
empecher les connexions invalides au niveau modele, avant tout feedback visuel.

Travail attendu:

- ajouter `GraphTypeCompatibilityService`;
- ajouter `GraphConnectionValidationResult` ou equivalent;
- ajouter `IGraphValidator`, `GraphValidationResult`, `GraphValidationIssue`, `GraphValidationSeverity`;
- couvrir type exact, int vers float optionnel, custom compatible par nom si retenu, wildcard si retenu en V1;
- refuser input -> input, output -> output, exec -> data, target cardinality single deja occupee;
- detecter port requis non connecte;
- detecter edge referencant un node ou port absent;
- detecter cycles uniquement si le document active `DisallowCycles`; ne pas en faire une contrainte globale;
- ajouter tests dans `MGUI.Tests/Graph/GraphTypeCompatibilityTests.cs` et `GraphValidationTests.cs`.

Criteres d'acceptation:

- connecter deux ports passe par le service de compatibilite;
- les erreurs de validation portent des ids stables et des references node/edge/port quand disponibles;
- les tests couvrent les chemins valides et invalides.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphTypeCompatibility|GraphValidation"`

Commit recommande:

- `feat: complete graph task 06 add type compatibility validation`

Resultat:

- Ajout de `GraphTypeCompatibilityService`, `GraphConnectionValidationResult`, `GraphValidationResult`, `GraphValidationIssue`, `GraphValidationSeverity`, `IGraphValidator` et `GraphDocumentValidator`.
- `GraphDocument.Connect(...)` passe maintenant par le service de compatibilite pour directions, types, duplicates, cardinalite single et cycles optionnels.
- Ajout des tests `GraphTypeCompatibilityTests` et `GraphValidationTests`.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphTypeCompatibility|GraphValidation|GraphDocumentModelTests"`.
- Resultat validation: 14 tests passes, 0 echec; avertissements existants ou nullable dans les tests.
- Commit effectue: `feat: complete graph task 06 add type compatibility validation`.

### ✅ Tache 07 - Ajouter commandes et undo/redo local

But:
poser l'undo/redo des actions du graphe sans dependre d'un systeme global inexistant.

Travail attendu:

- ajouter `IGraphCommand` avec `Execute`, `Undo`, nom lisible et metadata optionnelle;
- ajouter `GraphCommandStack` avec `Execute`, `Undo`, `Redo`, `CanUndo`, `CanRedo`, limite de capacite;
- ajouter commandes V1: `CreateNodeCommand`, `DeleteNodeCommand`, `MoveNodeCommand`, `ConnectPortsCommand`, `DisconnectPortsCommand`, `CreateCommentCommand`, `MoveCommentCommand`;
- s'assurer que les commandes modifient le modele, jamais les controles visuels directement;
- gerer transaction de deplacement groupee pour ne pas creer un commit undo par pixel;
- ajouter tests pour execute/undo/redo, invalidation redo apres nouvelle commande, suppression de noeud avec edges dependants.

Criteres d'acceptation:

- le stack est testable sans UI;
- undo/redo restaure les GUID d'origine;
- les commandes echouent proprement si le modele ne permet pas l'action.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphCommand"`

Commit recommande:

- `feat: complete graph task 07 add command stack`

Resultat:

- Ajout de `IGraphCommand`, `GraphCommandStack` et des commandes V1: creation/suppression/deplacement/redimensionnement de noeud, connexion/deconnexion de ports, creation/deplacement de commentaire.
- Ajout de `GraphDocument.AddEdge(...)` et `GraphDocument.AddComment(GraphCommentModel)` pour restaurer proprement les elements pendant undo/redo.
- Ajout de `GraphCommandStackTests` couvrant create/undo/redo, move node, connect/disconnect, invalidation redo et restauration des edges lors d'un delete node undo.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphCommand"`.
- Resultat validation: 5 tests passes, 0 echec; avertissements existants ou nullable dans les tests.
- Commit effectue: `feat: complete graph task 07 add command stack`.

### ✅ Tache 08 - Ajouter serialization JSON versionnee

But:
permettre sauvegarde, chargement et tests de round-trip sans UI.

Travail attendu:

- ajouter `GraphSerializer` dans `MGUI.Core/UI/Graph/Serialization/`;
- ajouter DTO versionnes si utile, plutot que serialiser directement les controles;
- inclure `version`, nodes, ports, edges, comments, editor viewport optionnel;
- inclure stable GUIDs et positions;
- serialiser les properties custom avec un format limite et documente pour la V1;
- ajouter `GraphSerializationResult` avec diagnostics non fatals;
- ajouter `GraphMigrationService` minimal avec support version 1 et point d'extension pour versions futures;
- ajouter tests round-trip, unknown node type conserve comme metadata, document invalide signale proprement.

Criteres d'acceptation:

- round-trip conserve les IDs, positions, ports et edges;
- charger JSON invalide ne plante pas sans diagnostic;
- la serialization reste independante de `MGGraphView`.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphSerialization"`

Commit recommande:

- `feat: complete graph task 08 add versioned serialization`

Resultat:

- Ajout de `GraphSerializer`, `GraphSerializationResult` et `GraphMigrationService`.
- La serialization utilise des DTO JSON versionnes a champs primitifs pour conserver proprement `Vector2`, `Rectangle`, couleur optionnelle, metadata, ports, edges et comments sans serialiser les controles.
- Ajout de `GraphSerializationTests` couvrant round-trip complet, JSON invalide avec diagnostic et conservation d'un type de noeud inconnu.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphSerialization"`.
- Resultat validation: 3 tests passes, 0 echec; avertissements existants ou nullable dans les tests.
- Commit effectue: `feat: complete graph task 08 add versioned serialization`.

### ✅ Tache 09 - Ajouter services de geometrie de rendu et hit-test

But:
preparer grid, edges Bezier et hit-test sans coupler au controle.

Travail attendu:

- ajouter `GraphBezierGeometry` ou equivalent pour echantillonner une courbe en segments;
- ajouter `GraphEdgeGeometryCache` avec cle basee sur endpoints, zoom si necessaire, thickness et style;
- ajouter `GraphHitTestService` pour noeuds, ports, edges et comments;
- ajouter `GraphSpatialIndex` minimal ou un service de culling simple par bounds;
- couvrir hit-test edge avec tolerance en pixels viewport;
- eviter allocations repetitives dans les methodes appelees par frame;
- ajouter tests de geometrie et hit-test.

Criteres d'acceptation:

- la V1 peut rendre une courbe Bezier via segments de ligne MGUI;
- le cache est invalide uniquement quand endpoints/style changent;
- le hit-test edge marche sans edge `MGElement`.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphGeometry|GraphHitTest"`

Commit recommande:

- `feat: complete graph task 09 add render geometry services`

Resultat:

- Ajout de `GraphBezierGeometry`, `GraphEdgeGeometryCache` et `GraphHitTestService`.
- Les edges Bezier V1 sont echantillonnees en polylignes reutilisables; le cache suit les endpoints, l'epaisseur, le zoom et le nombre de segments.
- Le hit-test supporte les polylignes d'edges avec tolerance et les noeuds en ordre top-most.
- Ajout de `GraphGeometryTests` couvrant sampling, cache, hit-test edge et hit-test node.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphGeometry|GraphHitTest"`.
- Resultat validation: 4 tests passes, 0 echec; avertissements existants ou nullable dans les tests.
- Commit effectue: `feat: complete graph task 09 add render geometry services`.

### ✅ Tache 10 - Enregistrer types UI, theme tokens et XAML minimal

But:
integrer le graphe dans les mecanismes declaratifs de MGUI avant de construire l'UI.

Travail attendu:

- ajouter les valeurs necessaires dans `MGElementType`: `GraphView`, `GraphNode`, `GraphPort`, `GraphCommentBox` si retenues;
- ajouter constantes de templates dans `MGControlTemplateCatalog`;
- ajouter placeholders de templates par defaut pour GraphView, GraphNode et GraphPort;
- ajouter settings de theme graph dans `MGTheme` et les definitions XAML si necessaire;
- ajouter valeurs built-in dans `BuiltInThemes.xaml` pour `Dark_Blue` et `Dark`;
- ajouter wrapper XAML `<GraphView>` minimal avec proprietes `ShowGrid`, `AllowZoom`, `AllowPan`, `SnapToGrid`;
- ajouter tests architecture pour resolution de template, chargement XAML et theme tokens.

Criteres d'acceptation:

- un `<GraphView />` XAML minimal peut etre parse;
- `DefaultControlTemplateName` pointe vers un template connu;
- les themes built-in n'ont pas de trou pour les nouveaux elements.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "Graph|Xaml|Theme|Architecture"`

Commit recommande:

- `ui: complete graph task 10 register element types and theme tokens`

Resultat:

- Ajout des types `GraphView`, `GraphNode`, `GraphPort` et `GraphCommentBox` dans `MGElementType`.
- Ajout des shells publics `MGGraphView`, `MGGraphNode`, `MGGraphPort` et `MGGraphCommentBox` avec `PART_*` lookless et templates par defaut.
- Ajout des flags declaratifs `ShowGrid`, `AllowZoom`, `AllowPan` et `SnapToGrid` sur `MGGraphView` et son wrapper XAML.
- Ajout des constantes/templates dans `MGControlTemplateCatalog` et des defaults themables via `MGTheme.Graph`.
- Ajout des wrappers XAML `GraphView`, `GraphNode`, `GraphPort`, `GraphCommentBox` et des alias XAML sans prefixe.
- Ajout de `ThemeGraphSettingsDefinition`, branchement dans `ThemeDefinitionBuilder` et valeurs Graph dans les themes built-in `Dark_Blue`, `Light_Gray` et `Dark`.
- Ajout de `GraphControlRegistrationTests` couvrant vocabulaire, templates, parsing XAML, theme definition et themes built-in.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphControlRegistration"`.
- Resultat validation: 5 tests passes, 0 echec; avertissements existants ou nullable dans les tests.
- Commit effectue: `feat: complete graph task 10 register graph controls`.

### ✅ Tache 11 - Ajouter `MGGraphNode` et `MGGraphPort`

But:
fournir les controles visuels unitaires sans encore connecter tout le document.

Travail attendu:

- creer `MGGraphNode` derive de `MGSingleContentHost` ou d'un host coherent valide en tache 01;
- exposer `NodeId`, `Title`, `IsSelected`, `IsHovered`, `HasError`, `HasWarning`, `IsCollapsed`;
- creer `MGGraphPort` avec `PortId`, `Direction`, `ValueType`, `IsConnected`, `IsRequired`, `IsHovered`;
- implementer TemplateParts de node et port;
- garder le chrome dans templates/theme;
- fournir une methode pour calculer l'ancre viewport/layout du port apres layout;
- ajouter tests de template parts et proprietes sans rendu lourd;
- ne pas implementer encore drag, connection ou document sync complet.

Criteres d'acceptation:

- `MGGraphNode` et `MGGraphPort` s'appliquent avec leur template par defaut;
- les etats visuels exposent des proprietes observables avec `NPC`;
- aucun port ne connait directement le serializer ou le command stack.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphNode|GraphPort|ControlTemplate"`

Commit recommande:

- `ui: complete graph task 11 add graph node and port controls`

Resultat:

- Enrichissement de `MGGraphNode` avec `NodeId`, `Title`, `HasError`, `HasWarning`, `IsCollapsed` et reutilisation des etats MGUI existants `IsSelected`/`IsHovered`.
- Enrichissement de `MGGraphPort` avec `PortId`, `Direction`, `ValueType`, `IsConnected`, `IsRequired` et reutilisation de `IsHovered` existant.
- Les proprietes node/port notifient via `NPC`; `IsCollapsed` masque les parties body/ports et invalide le layout.
- Ajout de `GetLayoutAnchor()` et `GetWorldAnchor(GraphViewportTransform)` pour calculer l'ancre d'un port apres layout.
- Extension des wrappers XAML pour exposer les nouveaux etats node/port.
- Ajout d'un runtime de test no-op `GraphTestRuntime` et de `GraphNodePortControlTests` couvrant templates, etats, notifications et anchors.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphNode|GraphPort|ControlTemplate"`.
- Resultat validation: 78 tests passes, 0 echec; avertissements restants preexistants ou nullable dans tests existants.
- Commit effectue: `feat: complete graph task 11 add node and port controls`.

### ✅ Tache 12 - Ajouter le shell `MGGraphView`

But:
creer le controle principal lookless, templateable et clippe.

Travail attendu:

- creer `MGGraphView`;
- deriver de `MGSingleContentHost` ou autre base validee, en documentant le choix;
- exposer `Document`, `ViewportTransform`, `ShowGrid`, `AllowZoom`, `AllowPan`, `SnapToGrid`, `SelectedNodeIds`, `SelectedEdgeIds`;
- declarer les TemplateParts `PART_OuterBorder`, `PART_ViewportHost`, `PART_NodesCanvas`, `PART_OverlayPanel`;
- appliquer `DefaultControlTemplateName`;
- brancher le content interne sans permettre aux utilisateurs de casser les parts;
- definir le clipping de contenu via les contrats MGUI existants;
- ne pas encore rendre toutes les edges ni gerer toutes les interactions.

Criteres d'acceptation:

- le controle se construit en C# et en XAML;
- le template par defaut s'attache et valide les parts;
- la zone graphe clippe correctement ses contenus dans les bounds du controle.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphView|ControlTemplate|Xaml"`

Commit recommande:

- `ui: complete graph task 12 add graph view shell`

Resultat:

- `MGGraphView` est maintenant un shell lookless base sur `MGSingleContentHost`, ce qui garde la composition interne sous controle via template parts et evite que le contenu utilisateur remplace le graphe.
- Ajout des parts `PART_ViewportHost`, `PART_NodesCanvas` et `PART_OverlayPanel` en plus de `PART_OuterBorder`; `Surface` reste un alias vers `NodesCanvas`.
- `ViewportHost` clippe ses enfants via `ClipToBounds`, et les hosts internes `ViewportHost`, `NodesCanvas`, `OverlayPanel` sont verrouilles avec `CanChangeContent = false` apres attachement.
- Ajout de `ViewportTransform`, alias `Viewport`, `SelectedNodeIds`, `SelectedEdgeIds` et `Document` observable.
- Mise a jour du template par defaut `GraphView.Default` et des defaults de theme associes.
- Ajout de `GraphViewShellTests` couvrant application du template, clipping, etats de selection/document/viewport et chargement XAML avec template attache.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphView|ControlTemplate|Xaml"`.
- Resultat validation: 112 tests passes, 0 echec; avertissements restants preexistants ou nullable dans tests existants.
- Commit effectue: `feat: complete graph task 12 add graph view shell`.

### ✅ Tache 13 - Synchroniser `GraphDocument` vers les noeuds visibles

But:
relier le modele aux controles sans casser la separation modele/UI.

Travail attendu:

- ajouter un presenter/controller interne, par exemple `GraphDocumentViewSynchronizer`;
- creer, reutiliser et supprimer les `MGGraphNode` selon les nodes du document;
- creer les ports visuels a partir des ports du modele;
- synchroniser titre, selection, erreurs, warnings, collapse et contenu custom minimal;
- mettre a jour position layout selon `GraphViewportTransform`;
- eviter `Clear` + recreation complete quand un seul noeud bouge;
- prevoir un mapping `Guid -> MGGraphNode`;
- ajouter tests sur synchronisation incrementalement, au moins au niveau service si les controles sont difficiles a tester.

Criteres d'acceptation:

- ajouter un node au document fait apparaitre un node visuel;
- supprimer un node retire le visuel et ses ports;
- bouger un node met a jour sa position sans recreer tous les autres;
- la synchronisation ne modifie pas le modele.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphView|GraphSync"`

Commit recommande:

- `ui: complete graph task 13 sync document to visible nodes`

Resultat:

- Ajout du synchroniseur interne `GraphDocumentViewSynchronizer` branche a `MGGraphView`.
- `MGGraphView` ecoute `GraphDocument.GraphChanged`, synchronise sur changement de document et expose `SynchronizeDocument`, `TryGetNodeControl` et `TryGetPortControl`.
- Creation, reutilisation et suppression incrementales des `MGGraphNode` selon les nodes du document.
- Creation, reutilisation et suppression incrementales des `MGGraphPort` selon les ports du modele.
- Synchronisation des titres, selection, collapse, flags error/warning via metadata, tailles optionnelles, positions via `GraphViewportTransform` et etat `IsConnected` des ports.
- Le synchroniseur utilise les scopes temporaires `AllowChangingContentTemporarily` pour respecter les hosts internes verrouilles.
- Ajout de `GraphDocumentSynchronizationTests` couvrant ajout, suppression, mise a jour de position sans recreation et synchronisation des ports connectes sans mutation du modele.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphView|GraphSync"`.
- Resultat validation: 14 tests passes, 0 echec; avertissements restants preexistants ou nullable dans tests existants.
- Commit effectue: `feat: complete graph task 13 sync document to visible nodes`.

### ✅ Tache 14 - Rendre la grille et les edges

But:
donner un rendu lisible au graphe en respectant le pipeline MGUI.

Travail attendu:

- rendre la grille mineure/majeure dans `MGGraphView.DrawSelf` ou un composant dedie;
- utiliser le viewport transform pour positionner les lignes;
- rendre les edges derriere les noeuds;
- utiliser `GraphEdgeGeometryCache` pour eviter de recalculer les courbes sans changement;
- dessiner les courbes via segments de ligne si aucun primitive Bezier native n'existe;
- appliquer couleurs et epaisseurs depuis theme/settings graph;
- respecter clipping et opacite `ElementDrawArgs`;
- ajouter mode simplifie quand zoom tres eloigne, au moins en desactivant labels/decors non essentiels si deja possible.

Criteres d'acceptation:

- la grille suit pan/zoom sans jitter grossier;
- une edge entre deux ports est visible et reste derriere les noeuds;
- le rendu n'alloue pas de nouvelles listes par edge a chaque frame;
- le controle n'appelle pas directement `SpriteBatch`.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphGeometry|GraphView"`

Commit recommande:

- `ui: complete graph task 14 render grid and edges`

Resultat:

- Ajout du canvas interne `MGGraphSurfaceCanvas`, utilise par le template `GraphView.Default` pour dessiner la surface du graphe avant les enfants, donc derriere les noeuds.
- Rendu de la grille mineure/majeure via `IUIDrawTransaction.StrokeLineSegment`, sans acces direct a `SpriteBatch`.
- La grille suit `GraphViewportTransform.Pan`, `Zoom` et `GridSize`; elle augmente son pas en zoom eloigne pour eviter un rendu trop dense.
- Rendu des edges via segments de lignes a partir de `GraphBezierGeometry` et `GraphEdgeGeometryCache`.
- Ajout des proprietes `GridLineBrush`, `EdgeBrush`, `EdgeThickness`, `MajorGridLineFrequency` et exposition de `EdgeGeometryCache` sur `MGGraphView`.
- Les couleurs de grille et d'edges sont appliquees depuis `MGTheme.Graph.GridLineBrush` et `MGTheme.Graph.EdgeBrush` par le template par defaut.
- Ajout d'un fallback d'ancrage des ports depuis le modele quand les ports visuels ne sont pas encore layoutes.
- Mise a jour du runtime de tests graph pour enregistrer les appels `StrokeLineSegment`.
- Ajout de `GraphViewRenderingTests` couvrant rendu grille, rendu edge, epaisseur/couleur et reutilisation du cache de geometrie.
- Validations executees avec succes: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` et `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphGeometry|GraphView"`.
- Resultat validation: build OK; 20 tests passes, 0 echec; avertissements restants preexistants ou de documentation/nullable.
- Commit effectue: `feat: complete graph task 14 render grid and edges`.

### ✅ Tache 15 - Ajouter pan, zoom et commandes de cadrage

But:
livrer la navigation de viewport principale.

Travail attendu:

- implementer pan souris par bouton retenu en tache 01;
- implementer zoom molette centre sur souris;
- respecter `AllowPan` et `AllowZoom`;
- ajouter `FrameOrigin`, `FrameAll`, `FrameSelection`;
- ajouter raccourcis clavier retenus: `A` frame all, `F` frame selection, raccourci origin si retenu;
- integrer proprement avec focus et input consomme;
- ne pas voler les inputs si le graphe n'est pas focus/hover selon la regle retenue;
- ajouter tests de transform et tests legers d'appel commande si possible.

Criteres d'acceptation:

- le zoom garde le point monde sous la souris;
- le pan ne modifie pas les positions monde des noeuds;
- les commandes de cadrage utilisent le modele et les bounds visibles.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphViewport|GraphInput|Focus"`

Commit recommande:

- `input: complete graph task 15 add pan zoom and frame commands`

Resultat:

- Ajout des commandes publiques `PanViewportBy`, `ZoomAtViewportPoint`, `FrameOrigin`, `FrameAll`, `FrameSelection` et `HandleGraphShortcut` sur `MGGraphView`.
- Le zoom molette utilise `GraphViewportTransform.ZoomAt` et conserve le point monde sous le curseur.
- Le pan viewport respecte `AllowPan`, modifie uniquement `ViewportTransform.Pan` et resynchronise les noeuds visibles sans modifier les positions monde du modele.
- `MGGraphView` est focusable et branche les raccourcis clavier `A` frame all, `F` frame selection et `Home` frame origin.
- Le pan souris utilise le bouton milieu et ne s'active que si le pointeur est dans le viewport; le zoom molette suit la meme regle de hover viewport.
- Les handlers respectent `AllowPan` et `AllowZoom`, consomment les evenements geres et declenchent une synchronisation document/vue apres changement de viewport.
- Ajout de `FramePadding` pour les commandes de cadrage.
- Ajout de `GraphInputNavigationTests` couvrant zoom centre curseur, pan sans mutation modele, flags `AllowPan`/`AllowZoom`, frame all, frame selection et raccourcis.
- Validations executees avec succes: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` et `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphViewport|GraphInput|Focus"`.
- Resultat validation: build OK; 291 tests passes, 0 echec; avertissements restants preexistants ou de documentation/nullable.
- Commit effectue: `input: complete graph task 15 add pan zoom and frame commands`.

### ✅ Tache 16 - Ajouter selection et drag de noeuds

But:
permettre manipulation de base des noeuds dans le graphe.

Travail attendu:

- ajouter `GraphSelectionManager`;
- supporter selection simple au clic;
- supporter multi-selection avec Ctrl;
- supporter rectangle de selection;
- supporter drag d'un noeud et drag groupe de la selection;
- appliquer snapping grille si `SnapToGrid` est actif;
- enregistrer le mouvement via `MoveNodeCommand` en transaction unique par drag;
- mettre a jour etats visuels selected/hovered;
- preparer auto-pan pres des bords si faisable sans complexifier; sinon documenter V2;
- ajouter tests de selection pure et commandes de move.

Criteres d'acceptation:

- selectionner un noeud met a jour modele de selection et visuel;
- dragger un noeud modifie sa position monde via commande;
- undo restaure la position precedente;
- la selection rectangle ne selectionne que les elements dans le viewport visible.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphSelection|GraphCommand|GraphInput"`

Commit recommande:

- `input: complete graph task 16 add selection and node dragging`

Resultat:

- Ajout de `GraphSelectionManager` pour selection pure des nodes/edges, selection simple, selection additive/toggle et selection par rectangle monde.
- `MGGraphView` expose `Selection`, `Commands`, `ClearSelection`, `SelectNode`, `SelectNodesInWorldRectangle`, `MoveSelectedNodesBy`, et maintient les visuels `IsSelected` via `UpdateSelectionVisuals`.
- Selection souris: clic gauche sur node selectionne, Ctrl ajoute/toggle, clic gauche vide nettoie la selection hors Ctrl.
- Drag de noeuds: clic gauche + mouvement sur node selectionne deplace le node ou groupe selectionne; `SnapToGrid` applique `GraphViewportTransform.SnapPoint`.
- Drag rectangle: clic gauche + mouvement dans la surface vide produit un rectangle de selection en coordonnees viewport, converti en monde au relachement.
- Ajout de `GraphNodeMove` et `MoveNodesCommand` pour enregistrer un drag groupe comme une seule entree undo/redo; les drags live peuvent deja avoir applique la position finale avant commit.
- `MoveSelectedNodesBy` utilise `MoveNodeCommand` pour un node et `MoveNodesCommand` pour les groupes.
- Auto-pan pres des bords laisse au perimetre V2 pour eviter de complexifier le routage d'input V1.
- Ajout de `GraphSelectionTests` couvrant selection simple/toggle, rectangle, etat visuel, deplacement groupe undoable et snapping.
- Validations executees avec succes: `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` et `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphSelection|GraphCommand|GraphInput"`.
- Resultat validation: build OK; 16 tests passes, 0 echec; avertissements restants preexistants ou de documentation/nullable.
- Commit effectue: `input: complete graph task 16 add selection and node dragging`.

### ⚪ Tache 17 - Ajouter interaction de connexion entre ports

But:
permettre creation et suppression de connexions par drag de port.

Travail attendu:

- ajouter `GraphConnectionController`;
- demarrer un drag depuis un `MGGraphPort`;
- afficher une edge temporaire pendant le drag;
- hit-test les ports compatibles sous la souris;
- valider avec `GraphTypeCompatibilityService` avant connexion;
- creer la connexion via `ConnectPortsCommand`;
- supporter annulation en relachant sur zone vide;
- afficher feedback visuel compatible/incompatible;
- supporter suppression d'une edge selectionnee via commande si l'edge selection est deja disponible, sinon preparer pour tache 18;
- ajouter tests de validation et commande connect/disconnect.

Criteres d'acceptation:

- un drag output -> input compatible cree une edge;
- un drag incompatible n'ajoute rien et produit un diagnostic/feedback non fatal;
- cardinalite single remplace ou refuse selon decision de tache 01;
- undo/redo fonctionne pour connect/disconnect.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphConnection|GraphCommand|GraphValidation"`

Commit recommande:

- `input: complete graph task 17 add port connection interaction`

Resultat:

- A remplir par l'agent.

### ⚪ Tache 18 - Ajouter commandes clavier d'edition

But:
fournir une edition ergonomique minimale sans attendre V2.

Travail attendu:

- supporter `Delete` pour supprimer selection noeuds/edges/comments;
- supporter `Ctrl+Z` et `Ctrl+Y` pour undo/redo du `GraphCommandStack`;
- supporter `Ctrl+D` duplicate selection si retenu V1, sinon documenter V2;
- supporter `Escape` pour annuler drag/selection temporaire;
- respecter les regles MGUI: ne pas capturer les raccourcis d'un champ texte interne;
- mettre a jour selection apres suppression;
- ajouter tests commandes pures et, si possible, tests input routage.

Criteres d'acceptation:

- Delete supprime la selection via commandes undoables;
- Ctrl+Z/Ctrl+Y ne s'executent que lorsque le graphe doit recevoir l'input;
- un controle texte embarque dans un noeud garde ses propres raccourcis.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphKeyboard|GraphCommand|Focus|Input"`

Commit recommande:

- `input: complete graph task 18 add keyboard editing commands`

Resultat:

- A remplir par l'agent.

### ⚪ Tache 19 - Ajouter menu contextuel de creation de noeud

But:
permettre d'ajouter rapidement des noeuds sans API metier lourde.

Travail attendu:

- ajouter `GraphNodeDefinition` ou equivalent pour decrire les types de noeuds disponibles;
- ajouter `GraphNodePalette` ou provider injectable minimal;
- afficher un `MGContextMenu` au clic droit sur le graphe;
- creer le noeud a la position monde de la souris via `CreateNodeCommand`;
- depuis un port en drag vers zone vide, proposer seulement les noeuds compatibles si le filtrage V1 est raisonnable;
- inclure categories simples et recherche seulement si possible sans sortir du scope; sinon garder categories basiques et documenter recherche en V2;
- ajouter tests provider/filtering en logique pure.

Criteres d'acceptation:

- clic droit sur zone vide permet d'ajouter au moins les noeuds sample V1;
- la position du noeud cree correspond a la souris en coordonnees monde;
- la creation est undoable;
- le controle generique ne connait pas les types metiers CasaEngine.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphPalette|GraphCommand|ContextMenu"`

Commit recommande:

- `ui: complete graph task 19 add context menu node creation`

Resultat:

- A remplir par l'agent.

### ⚪ Tache 20 - Ajouter comment boxes simples

But:
livrer l'organisation minimale promise par la V1 sans group system complet.

Travail attendu:

- ajouter `MGGraphCommentBox` ou representation visuelle equivalente;
- synchroniser `GraphCommentModel` vers une box visible;
- supporter selection, drag et resize simple si faisable;
- supporter creation via menu contextuel;
- supporter suppression via Delete;
- garder les groups, collapse groups et sous-graphes hors V1;
- theme/comment colors via settings, pas couleurs hardcodees;
- ajouter tests modele/commande pour create/move/resize/delete comment.

Criteres d'acceptation:

- une comment box peut etre creee, bougee, selectionnee et supprimee;
- serialization conserve bounds, titre et texte;
- le drag de comment n'embarque pas les noeuds contenus en V1, sauf decision explicite documentee.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphComment|GraphSerialization|GraphCommand"`

Commit recommande:

- `feat: complete graph task 20 add simple comments`

Resultat:

- A remplir par l'agent.

### ⚪ Tache 21 - Ajouter un sample Dialogue Graph

But:
prouver l'integration utilisateur finale avec un scenario simple et concret.

Travail attendu:

- creer `MGUI.Samples/Features/GraphViewDialogue.xaml`;
- creer `MGUI.Samples/Features/GraphViewDialogue.xaml.cs`;
- ajouter l'entree dans `MGUI.Samples/Compendium.xaml` et code-behind si necessaire;
- definir quelques types de noeuds: Start, Dialogue Line, Choice, End;
- precharger un graphe exemple avec plusieurs noeuds, ports et edges;
- permettre ajout de noeud via menu contextuel;
- permettre connexion simple entre ports;
- ajouter boutons ou commandes sample pour sauvegarder/charger un JSON en memoire ou fichier temporaire selon conventions sample;
- montrer validation simple: port requis non connecte, type incompatible;
- ne pas ajouter de compiler dialogue metier complet.

Criteres d'acceptation:

- le sample se build et s'ouvre depuis le compendium;
- le sample demontre pan, zoom, selection, drag, connexion, undo/redo, save/load;
- le code sample reste separe du controle generique.

Validation recommandee:

- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "Graph"`

Commit recommande:

- `sample: complete graph task 21 add dialogue graph sample`

Resultat:

- A remplir par l'agent.

### ⚪ Tache 22 - Ajouter culling et garde-fous performance

But:
eviter qu'un graphe moyen degrade Update/Draw ou le GC.

Travail attendu:

- ajouter viewport culling pour noeuds et comments hors ecran;
- ne rendre les edges que si leur bounds intersecte le viewport ou si elles sont selectionnees/draggees;
- eviter de mesurer tous les noeuds a chaque frame;
- invalider caches uniquement sur changement de position, zoom pertinent, style ou ports;
- ajouter compteurs diagnostics internes optionnels: nodes visibles, edges visibles, cache hits/misses;
- ajouter tests de services de culling;
- ajouter un micro-scenario de stress dans tests ou sample si possible sans rendre la suite lente.

Criteres d'acceptation:

- les services de culling sont testables sans renderer;
- aucune allocation evidente par edge dans `DrawSelf`;
- les nodes hors viewport ne provoquent pas recreation inutile;
- le sample reste fluide avec un graphe de taille moderee.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "GraphCulling|GraphGeometry|GraphView"`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

Commit recommande:

- `perf: complete graph task 22 add culling and cache safeguards`

Resultat:

- A remplir par l'agent.

### ⚪ Tache 23 - Documenter `MGGraphView` V1

But:
fournir une documentation utilisable apres implementation.

Travail attendu:

- creer `Docs/graph-view-v1-guide.md`;
- documenter creation C# et XAML;
- documenter modele minimal, ports, edges, validation, serialization;
- documenter commandes clavier et souris;
- documenter theming et TemplateParts;
- documenter limites V1 et backlog V2;
- lier le sample Dialogue Graph;
- verifier que `Docs/graph_node_system_features.md` reste une vision fonctionnelle, pas une doc d'API obsolete.

Criteres d'acceptation:

- un utilisateur peut creer un graphe minimal depuis la doc;
- les chemins et noms de types sont exacts;
- les limites V1 sont explicites.

Validation recommandee:

- verification documentaire;
- `rtk git diff`.

Commit recommande:

- `docs: complete graph task 23 document graph view v1`

Resultat:

- A remplir par l'agent.

### ⚪ Tache 24 - Stabiliser la matrice de scenarios graphe

But:
faire une passe finale de regression avant de considerer la V1 complete.

Travail attendu:

- ajouter ou completer `MGUI.Tests/Graph/GraphScenarioTests.cs`;
- couvrir scenario complet: create nodes, connect, move, undo, redo, serialize, deserialize, validate;
- couvrir suppression d'un noeud connecte;
- couvrir chargement d'un document avec edge invalide;
- couvrir theme/template minimal;
- lancer builds Core et Samples;
- mettre a jour ce fichier avec etat final, validations et commit.

Criteres d'acceptation:

- la matrice de scenarios V1 passe;
- le sample build;
- aucun point V1 obligatoire de ce plan ne reste sans justification;
- les taches bloquees, si presentes, sont documentees avec suite claire.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "Graph|Architecture|Xaml|Theme"`

Commit recommande:

- `test: complete graph task 24 stabilize graph scenario matrix`

Resultat:

- A remplir par l'agent.

## Backlog V2 apres V1

- Blackboard et variables globales.
- Recherche dans le graphe.
- Copier/couper/coller et clipboard serializer dedie.
- Duplicate, align, distribute.
- Reroute nodes et named reroutes.
- Groupes avec deplacement des noeuds contenus.
- PropertyGrid integree pour les proprietes de noeud.
- Drag/drop assets depuis l'editeur CasaEngine.
- Minimap et bookmarks.
- Preview live pour graphes de rendu.
- Validation avancee avec panneau diagnostics.

## Backlog V3 apres V2

- Graph compiler generique.
- Material graph complet.
- Shader graph.
- Particle graph.
- Behavior tree graph.
- Animation state machine graph.
- Visual scripting.
- Node library extensible par plugins.
- Debug runtime avec traces, breakpoints et valeurs live.
- Graph diff/merge.
- Profiling et generation de code.