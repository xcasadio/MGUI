# Taches restantes — Systeme de graphe a noeuds (V2/V3)

## Objectif

Completer le graphe a noeuds de MGUI (`MGGraphView` et `MGUI.Core/UI/Graph/`) avec les fonctionnalites V2/V3 restantes : edition avancee (cut/duplicate, align/distribute), navigation (recherche, minimap, bookmarks), organisation (reroutes, sous-graphes), donnees (blackboard, integration PropertyGrid) et exploration de la couche runtime (compilation, debug). Chaque tache doit respecter les invariants decrits dans `Docs/controls-architecture.md` : `GraphDocument` est la source de verite, les commandes mutent le modele uniquement (jamais l'UI directement), tout est annulable via `GraphCommandStack`, et MGUI.Core reste generique (aucune logique metier de compilation domaine, aucun token MonoGame interdit tel que `Texture2D`).

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- 1 commit par tache.
- Mettre a jour le statut de chaque tache dans ce fichier au fur et a mesure.
- Si une tache est bloquee, la marquer ⛔ avec une description du blocage.
- Pas de refactor hors perimetre.
- Ajouter des tests pour chaque tache (dossier `MGUI.Tests/Graph/`).

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

```bash
dotnet build MGUI.Core/MGUI.Core.csproj
dotnet test MGUI.Tests/MGUI.Tests.csproj --filter "FullyQualifiedName~Graph"
```

## Taches

### ⚪ 1. Cut (Ctrl+X) et Duplicate (Ctrl+D)

**But** : completer le trio clipboard. Copy (`Ctrl+C`) et Paste (`Ctrl+V`) existent deja dans `MGUI.Core/UI/MGGraphControls.cs` (`CopySelectionToClipboard`, `PasteFromClipboard`, format `MGUI.GraphClipboard.v1`) ; `HandleGraphShortcut` reserve deja `Keys.D` (retourne false) et n'a pas de branche `Keys.X`.

**Travail attendu** :
- `CutSelectionToClipboard()` : copie via le chemin existant puis supprime la selection en un seul `GraphBatchCommand` (reutiliser la logique de `DeleteSelection`).
- `DuplicateSelection()` : duplique le sous-graphe selectionne avec de nouveaux Guids et un petit offset monde, sans passer par le presse-papiers OS (reutiliser `TryCreateClipboardDocument` + le chemin de collage), selection deplacee sur les copies.
- Brancher `Keys.X` et `Keys.D` (avec Ctrl) dans `HandleGraphShortcut`, et ajouter les entrees `Cut` / `Duplicate` dans `CreateGraphContextMenu`.

**Criteres d'acceptation** :
- Cut = copy + delete annulable en un seul undo ; le contenu coupe est collable.
- Duplicate n'ecrase pas le presse-papiers ; un undo retire toutes les copies.
- Tests dans `MGUI.Tests/Graph/` couvrant raccourcis, menu contextuel et undo.

**Commit recommande** : `feat(graph): add cut and duplicate commands`

### ⚪ 2. Recherche dans le graphe

**But** : retrouver rapidement un noeud dans un grand graphe. Aucun service de recherche n'existe (`rg 'FindNode|SearchNode|GraphSearch'` ne retourne rien).

**Travail attendu** :
- Service pur `GraphSearchService` dans `MGUI.Core/UI/Graph/Interaction/` : recherche par titre de noeud, `NodeType`, texte de commentaire, nom de port ; resultats ordonnes et types (noeud/commentaire).
- API sur `MGGraphView` : `FindNext` / `FindPrevious` qui selectionnent et cadrent le resultat (`FrameSelection` existe deja).
- L'UI de saisie (champ de recherche) reste a la charge de l'hote ou d'un overlay optionnel dans `OverlayPanel` ; le service doit etre utilisable sans UI.

**Criteres d'acceptation** :
- Recherche insensible a la casse, testee sans rendu sur un `GraphDocument` pur.
- `FindNext` cycle sur les resultats et met a jour la selection.

**Commit recommande** : `feat(graph): add graph search service and find navigation`

### ⚪ 3. Align et distribute de la selection

**But** : commandes d'alignement (gauche/droite/haut/bas/centre) et de distribution (horizontale/verticale) des noeuds selectionnes.

**Travail attendu** :
- Calculs purs dans un helper de `MGUI.Core/UI/Graph/Interaction/` (entree : liste de `GraphNodeModel` + tailles monde ; sortie : liste de `GraphNodeMove`). Attention : la taille effective d'un noeud auto-dimensionne est memorisee via `GraphSelectionManager.SetAutoMeasuredWorldSize`.
- Execution via le `MoveNodesCommand` existant (une entree d'undo par operation).
- API publique sur `MGGraphView` (ex. `AlignSelection(GraphAlignment)` / `DistributeSelection(Orientation)`) ; pas de nouveau raccourci obligatoire.

**Criteres d'acceptation** :
- Alignements et distributions corrects sur des cas a 3+ noeuds, annulables en un undo.
- Aucun mouvement quand moins de 2 (align) ou 3 (distribute) noeuds sont selectionnes.

**Commit recommande** : `feat(graph): add align and distribute commands`

### ⚪ 4. Panneau de proprietes de noeud (integration MGPropertyGrid)

**But** : editer `GraphNodeModel.Properties` du noeud selectionne via `MGPropertyGrid` (le controle existe : `MGUI.Core/UI/MGPropertyGrid.cs`), sans coupler les deux controles.

**Travail attendu** :
- Adaptateur (ex. `GraphNodePropertiesAdapter` dans `MGUI.Core/UI/Graph/Interaction/`) exposant les `Properties` d'un noeud sous une forme que `MGPropertyGrid` sait afficher (via `ICustomTypeDescriptor` ou un objet materialise), types supportes : bool/int/float/double/string/couleur.
- Chaque commit d'editeur passe par une commande annulable (nouvelle `ChangeNodePropertyCommand` dans `Graph/Commands/GraphCommands.cs`) et declenche `SynchronizeDocument()`.
- Cablage de demonstration dans `MGUI.Samples/Features/GraphViewDialogue.xaml.cs` (grille a cote du graphe, rafraichie sur changement de selection).

**Criteres d'acceptation** :
- Modifier une propriete dans la grille met a jour le modele, est annulable, et un refresh par frame ne detruit pas la saisie en cours (`RefreshVisibleValues` + `IsEditing`).
- Aucun lien dur de `MGPropertyGrid` vers les types graphe (l'adaptateur vit cote graphe).

**Commit recommande** : `feat(graph): edit node properties through MGPropertyGrid adapter`

### ⚪ 5. Blackboard / variables du graphe

**But** : panneau lateral listant les variables exposees du graphe (ex. material : BaseColor/Roughness ; dialogue : PlayerName/QuestState), avec creation de getters/setters dans le graphe. Rien n'existe (`rg 'Blackboard'` ne retourne rien dans `MGUI.Core/UI/`).

**Travail attendu** :
- Modele pur : `GraphVariableModel` (Guid stable, nom, `GraphValueType`, valeur par defaut, categorie, description) stocke sur `GraphDocument` et serialise par `GraphSerializer` (nouvelle version de schema + migration dans `GraphMigrationService`).
- Commandes annulables : creation, suppression, renommage, changement de type/valeur.
- Creation de noeuds getter/setter depuis une variable via `GraphNodePalette` (definitions generees) ; le drag & drop visuel du panneau vers le graphe peut rester une etape ulterieure.
- Controle UI `MGGraphBlackboard` optionnel et separe de `MGGraphView` (l'hote le place ou il veut).

**Criteres d'acceptation** :
- Round-trip serialisation avec variables ; documents anciens sans variables se chargent sans erreur.
- Renommer une variable met a jour les noeuds getter/setter lies, annulable.

**Commit recommande** : `feat(graph): add graph variables model and blackboard panel`

### ⚪ 6. Minimap

**But** : apercu global du graphe avec rectangle de viewport, clic pour recentrer, drag du rectangle.

**Travail attendu** :
- Controle `MGGraphMinimap` rendu simplifie (rectangles de noeuds/commentaires, pas de texte ni de ports), branche sur `GraphDocument` + `GraphViewportTransform` d'un `MGGraphView` cible.
- Interactions : clic = centrer la vue ; drag du rectangle = pan continu. Reutiliser `PanViewportBy` / `FrameAll`.
- Respect des regles perf du rendu graphe : pas de LINQ ni d'allocations dans la boucle de dessin.

**Criteres d'acceptation** :
- La minimap reflete pan/zoom en continu ; cliquer recentre correctement (test de mapping monde<->minimap pur).
- Option afficher/masquer ; aucune degradation quand le document est vide.

**Commit recommande** : `feat(graph): add minimap control`

### ⚪ 7. Bookmarks de navigation

**But** : memoriser des positions/zooms nommes et y revenir (graphes complexes).

**Travail attendu** :
- Modele pur `GraphBookmark` (nom, pan, zoom) stocke dans les metadata editeur du document (serialise, sans nouvelle version majeure si possible via `Metadata`).
- API `MGGraphView` : ajouter/supprimer/aller au bookmark (animation non requise) ; raccourcis optionnels laisses a l'hote.

**Criteres d'acceptation** :
- Round-trip serialisation ; `GoToBookmark` restaure exactement pan et zoom (test sur `GraphViewportTransform`).

**Commit recommande** : `feat(graph): add viewport bookmarks`

### ⚪ 8. Reroute nodes et named reroutes

**But** : assainir visuellement les longs cables. Un reroute est un point de passage visuel ; un named reroute remplace un long fil par une paire entree/sortie nommee. Rien n'existe (`rg 'Reroute'` ne retourne rien).

**Travail attendu** :
- Etape 1 (reroute simple) : type de noeud reserve (ex. `graph/reroute`) avec un port d'entree et un port de sortie `Wildcard`, insertion par double-clic sur un edge (hit-test edge existant : `TryGetEdgeAtViewportPoint`) via un `GraphBatchCommand` (disconnect + create + 2 connects). Rendu compact (pas de header).
- Etape 2 (named reroute) : paire de noeuds lies par un nom partage ; le rendu central des edges (`MGGraphSurfaceCanvas`) ignore le lien logique entre les deux tunnels.
- La compatibilite de types doit traverser les reroutes (`GraphTypeCompatibilityService` doit resoudre le type effectif en remontant la chaine).

**Criteres d'acceptation** :
- Inserer/supprimer un reroute est annulable et preserve la connexion logique source->cible.
- La validation refuse un cycle cree via reroutes ; tests de resolution de type a travers une chaine de reroutes.

**Commit recommande** : `feat(graph): add reroute nodes` puis `feat(graph): add named reroutes`

### ⚪ 9. Sous-graphes, collapse groups et breadcrumbs

**But** : structurer les gros graphes : replier une zone en un noeud, entrer dedans, revenir au parent via breadcrumbs. Rien n'existe (`rg 'SubGraph'` ne retourne rien).

**Travail attendu** (decoupable en plusieurs commits) :
- Modele : `GraphDocument` imbrique reference par un noeud de type reserve (ex. `graph/subgraph`), ports du noeud generes depuis les tunnels d'entree/sortie du sous-document ; serialisation recursive versionnee.
- Navigation : `MGGraphView` peut afficher un sous-document et exposer la pile de navigation ; le breadcrumb UI peut etre un controle separe ou laisse a l'hote au debut.
- Collapse group : replier une selection en sous-graphe (commande annulable) et l'inverse (expand).

**Criteres d'acceptation** :
- Round-trip serialisation d'un document avec sous-graphe ; collapse puis expand redonne un graphe equivalent (ids stables) ; undo couvre chaque etape.

**Commit recommande** : `feat(graph): add sub-graph model and navigation`

### ⚪ 10. Exploration couche V3 : compilation, preview, debug runtime, diff/merge

**But** : preparer (sans implementer en aveugle) la couche d'execution : `IGraphCompiler` / `IGraphEvaluator` cote hote, preview live avec debounce et dirty flags, `GraphDebugSession` (breakpoints, valeurs par port, temps par noeud), diff/merge de documents. Rien n'existe dans `MGUI.Core/UI/Graph/` (seuls les dossiers Commands/Interaction/Model/Presentation/Rendering/Serialization/Validation).

**Travail attendu** :
- Rediger une proposition d'architecture (nouveau doc dans `Docs/`) qui delimite ce qui appartient a MGUI.Core (contrats, hooks d'affichage de valeurs sur ports/cables, etats visuels de debug) et ce qui appartient a l'hote (compilation, execution, domaine). La regle existante s'applique : aucune logique metier dans MGUI.Core.
- Identifier les points d'extension deja disponibles : `EditorMetadata` (`HasError`/`HasWarning`), `SynchronizeDocument()`, `OverlayPanel`, `CullingDiagnostics`.
- Ne demarrer l'implementation qu'apres validation de la proposition.

**Criteres d'acceptation** :
- Document de proposition revu et approuve ; decoupage en taches concretes ajoute a ce fichier.

**Commit recommande** : `docs(graph): add runtime layer architecture proposal`
