# Fonctionnalités principales pour ajouter un système de graphe à nœuds dans MGUI

## Objectif

L’objectif est d’ajouter à **MGUI** un contrôle de graphe visuel comparable aux systèmes de nœuds utilisés dans les moteurs modernes comme Unity ou Unreal.

Le but n’est pas seulement de créer un éditeur de “Blueprint”, mais plutôt de créer un **contrôle générique de graphe à nœuds**, réutilisable ensuite pour plusieurs usages :

- material graph ;
- shader graph ;
- visual scripting ;
- behavior tree ;
- dialogue graph ;
- animation state machine ;
- particle graph ;
- render graph ;
- graphes d’outils editor spécifiques à CasaEngine.

Le système doit être conçu comme un composant MGUI réutilisable, performant, stylable, sérialisable et indépendant d’un domaine métier précis.

---

# 1. Contrôle principal : `MGGraphView`

`MGGraphView` est le conteneur principal du graphe. Il représente un espace 2D virtuellement infini dans lequel les nœuds sont affichés, déplacés, connectés et organisés.

## Fonctionnalités attendues

- viewport 2D pannable ;
- zoom fluide avec la molette ;
- zoom centré sur la souris ;
- grille de fond ;
- snapping sur la grille ;
- coordonnées monde / écran ;
- sélection simple ;
- sélection multiple ;
- rectangle de sélection ;
- focus sur la sélection ;
- frame all ;
- frame selection ;
- frame origin ;
- clipping correct dans la zone visible ;
- navigation clavier ;
- support de grands graphes sans recalcul complet à chaque frame.

## Classes possibles

```csharp
MGGraphView
MGGraphCanvas
MGGraphGrid
MGGraphViewportTransform
MGGraphSelectionManager
```

---

# 2. Séparation modèle / UI

Le graphe ne doit pas être seulement un ensemble de contrôles visuels. Il faut séparer clairement :

- le modèle de données ;
- la représentation visuelle ;
- les interactions utilisateur ;
- la sérialisation ;
- l’évaluation ou la compilation éventuelle.

## Modèle conseillé

```csharp
GraphDocument
GraphNodeModel
GraphPortModel
GraphEdgeModel
GraphGroupModel
GraphVariableModel
```

## Données à stocker

Chaque nœud doit contenir :

- un `Guid` stable ;
- un type de nœud ;
- une position ;
- une taille optionnelle ;
- une liste de ports d’entrée ;
- une liste de ports de sortie ;
- des propriétés spécifiques ;
- des métadonnées editor ;
- un état collapsé ou non ;
- des commentaires éventuels.

Chaque connexion doit contenir :

- un `Guid` stable ;
- l’identifiant du nœud source ;
- l’identifiant du port source ;
- l’identifiant du nœud cible ;
- l’identifiant du port cible ;
- des métadonnées de rendu optionnelles.

## Pourquoi c’est important

Cette séparation permet d’utiliser le même système pour des domaines très différents :

- matériaux ;
- particules ;
- dialogues ;
- IA ;
- scripts visuels ;
- animation ;
- rendu.

Sans cette séparation, le contrôle sera difficile à sauvegarder, à compiler, à tester et à intégrer dans CasaEngine.

---

# 3. Nœuds : `MGGraphNode`

Un nœud est un bloc visuel manipulable dans le graphe.

## Fonctionnalités principales

- titre ;
- icône optionnelle ;
- couleur par catégorie ;
- header ;
- contenu personnalisable ;
- ports à gauche et à droite ;
- propriétés éditables ;
- état sélectionné ;
- état survolé ;
- état erreur ;
- état warning ;
- déplacement par drag & drop ;
- duplication ;
- suppression ;
- renommage ;
- collapse / expand ;
- redimensionnement optionnel ;
- preview miniature optionnelle.

## Exemples de nœuds

```csharp
ConstantNode
TextureNode
MathNode
FunctionNode
BranchNode
EventNode
OutputNode
CommentNode
RerouteNode
```

## États visuels à prévoir

- normal ;
- hovered ;
- selected ;
- disabled ;
- error ;
- warning ;
- active ;
- compiling ;
- dirty.

---

# 4. Ports / pins : `MGGraphPort`

Les ports sont les points d’entrée et de sortie des nœuds. C’est une partie critique de l’architecture.

## Fonctionnalités principales

- port d’entrée ;
- port de sortie ;
- nom affiché ;
- type de donnée ;
- couleur par type ;
- cardinalité simple ou multiple ;
- port obligatoire ou optionnel ;
- valeur par défaut quand non connecté ;
- tooltip ;
- validation de compatibilité ;
- drag depuis un port pour créer une connexion ;
- menu contextuel de création de nœud compatible depuis un port.

## Types de directions

```csharp
GraphPortDirection.Input
GraphPortDirection.Output
```

## Cardinalité

```csharp
GraphPortCardinality.Single
GraphPortCardinality.Multiple
```

## Types de données possibles

```csharp
Float
Int
Bool
String
Vector2
Vector3
Vector4
Color
Texture2D
Material
Entity
Exec
Object
Custom
```

## Points importants

Le port ne doit pas seulement être visuel. Il doit porter les informations nécessaires pour :

- valider une connexion ;
- afficher correctement le type ;
- fournir une valeur par défaut ;
- générer ou compiler le graphe ;
- guider l’utilisateur lors de la création de nœuds.

---

# 5. Connexions : `MGGraphEdge`

Une connexion représente le lien entre deux ports.

## Fonctionnalités principales

- liaison visuelle entre deux ports ;
- rendu en courbe de Bézier ;
- option de rendu en ligne droite ;
- couleur selon le type ;
- épaisseur personnalisable ;
- animation optionnelle de flux ;
- hit-test sur les câbles ;
- sélection d’un câble ;
- suppression avec `Delete` ;
- reconnexion d’une extrémité ;
- validation avant connexion ;
- affichage d’erreur si connexion invalide ;
- interdiction optionnelle des cycles.

## Familles de connexions

```csharp
DataEdge
ExecEdge
```

`DataEdge` sert pour les flux de données :

- float ;
- vector ;
- color ;
- texture ;
- object ;
- entity ;
- material.

`ExecEdge` sert pour les graphes de logique ou de visual scripting :

- exécution ;
- branchement ;
- séquence ;
- événement ;
- flow control.

Même si MGUI ne fait pas encore de visual scripting, prévoir ce concept évite de bloquer l’architecture plus tard.

---

# 6. Menu contextuel et palette de nœuds

Un graphe moderne doit permettre d’ajouter rapidement des nœuds.

## Fonctionnalités principales

- clic droit sur le graphe ;
- recherche de nœud ;
- catégories ;
- favoris ;
- historique des derniers nœuds ;
- filtrage par compatibilité avec un port ;
- création du nœud à la position de la souris ;
- raccourcis clavier ;
- documentation courte du nœud sélectionné ;
- affichage du type de sortie ou d’entrée attendu.

## Exemple de structure

```text
Add Node
 ├─ Math
 │   ├─ Add
 │   ├─ Multiply
 │   └─ Lerp
 ├─ Texture
 │   ├─ Texture Sample
 │   └─ UV Coordinates
 ├─ Logic
 │   ├─ Branch
 │   └─ Compare
 └─ Output
     └─ Material Output
```

## Cas important

Quand l’utilisateur tire une connexion depuis un port vers une zone vide, le menu contextuel devrait proposer uniquement les nœuds compatibles avec le type du port.

Exemple :

- depuis un port `Float`, proposer des nœuds mathématiques ;
- depuis un port `Texture2D`, proposer des samples ou convertisseurs ;
- depuis un port `Exec`, proposer des nœuds de contrôle de flux.

---

# 7. Sélection, manipulation et édition

## Fonctionnalités principales

- sélectionner un nœud ;
- sélection multiple avec `Ctrl` ;
- rectangle de sélection ;
- déplacement groupé ;
- duplication ;
- copier ;
- couper ;
- coller ;
- suppression ;
- undo ;
- redo ;
- alignement ;
- distribution ;
- snap to grid ;
- verrouillage de nœuds ;
- navigation au clavier ;
- drag avec auto-pan près des bords.

## Commandes utiles

```text
Ctrl + C      Copy
Ctrl + V      Paste
Ctrl + X      Cut
Ctrl + D      Duplicate
Delete        Delete selection
F             Frame selection
A             Frame all
Ctrl + Z      Undo
Ctrl + Y      Redo
```

---

# 8. Commentaires, groupes et organisation

Les graphes deviennent vite illisibles. Les fonctionnalités d’organisation doivent être prévues assez tôt.

## Fonctionnalités principales

- comment box ;
- couleur de commentaire ;
- titre de groupe ;
- déplacer un groupe avec ses nœuds ;
- redimensionner un groupe ;
- description sur un nœud ;
- bulle de commentaire ;
- commentaire toujours visible même avec le zoom ;
- collapse group ;
- sous-graphes ;
- breadcrumbs pour revenir au graphe parent.

## Classes possibles

```csharp
MGGraphCommentBox
MGGraphGroup
MGGraphSubGraphNode
MGGraphBreadcrumb
```

## Objectif

Permettre de structurer les gros graphes en zones lisibles :

- input ;
- logic ;
- calculations ;
- outputs ;
- debug ;
- deprecated ;
- experimental.

---

# 9. Reroute nodes et named reroutes

Les reroute nodes sont indispensables pour rendre un graphe propre.

## Fonctionnalités principales

- double-clic sur un câble pour ajouter un reroute ;
- reroute node simple ;
- named reroute ;
- tunnel visuel entrée / sortie ;
- possibilité de cacher les longs câbles ;
- duplication de reroute ;
- conversion câble vers named reroute.

## Classes possibles

```csharp
MGGraphRerouteNode
MGGraphNamedRerouteInputNode
MGGraphNamedRerouteOutputNode
```

## Usage

Un reroute node permet de modifier le chemin visuel d’une connexion sans changer la logique du graphe.

Un named reroute permet de remplacer un long fil par une référence nommée, utile dans les graphes très denses.

---

# 10. Blackboard / variables globales du graphe

Un blackboard est un panneau latéral listant les variables et paramètres exposés du graphe.

## Fonctionnalités principales

- liste des variables du graphe ;
- création de variable ;
- suppression de variable ;
- renommage ;
- type de variable ;
- valeur par défaut ;
- drag & drop dans le graphe ;
- création automatique d’un getter ;
- création automatique d’un setter ;
- recherche ;
- catégories ;
- description ;
- paramètres exposés.

## Classes possibles

```csharp
MGGraphBlackboard
GraphVariableModel
GraphParameterModel
GraphExposedPropertyModel
```

## Exemple d’utilisation

Pour un material graph :

- `BaseColor`
- `Roughness`
- `Metallic`
- `Normal`
- `Emissive`

Pour un behavior graph :

- `Target`
- `DistanceToTarget`
- `Health`
- `CurrentState`

Pour un dialogue graph :

- `PlayerName`
- `QuestState`
- `HasItem`
- `NPCMood`

---

# 11. Panneau de détails / PropertyGrid

Quand un nœud est sélectionné, l’éditeur doit pouvoir afficher et modifier ses propriétés.

## Fonctionnalités principales

- afficher les propriétés du nœud sélectionné ;
- édition bool ;
- édition int ;
- édition float ;
- édition string ;
- édition color ;
- édition vector ;
- édition enum ;
- édition asset reference ;
- édition multi-sélection ;
- reset property ;
- valeurs par défaut ;
- validation ;
- undo / redo ;
- binding avec une future `MGPropertyGrid`.

## Recommandation

Ne pas mettre toutes les propriétés directement dans le nœud.

Le nœud doit afficher les valeurs importantes ou fréquemment modifiées.  
Les paramètres complets doivent être édités dans un panneau de détails.

---

# 12. Validation du graphe

Un graphe doit pouvoir être validé avant d’être exécuté, compilé ou sauvegardé.

## Erreurs à détecter

- port obligatoire non connecté ;
- connexion invalide ;
- cycle interdit ;
- nœud orphelin ;
- type incompatible ;
- output node manquant ;
- multiples outputs interdits ;
- propriété invalide ;
- valeur hors limites ;
- nœud obsolète ;
- nœud inconnu après chargement.

## Fonctionnalités UX

- erreur affichée sur le nœud ;
- warning affiché sur le nœud ;
- tooltip de diagnostic ;
- panneau de diagnostic ;
- double-clic sur erreur pour centrer le nœud ;
- validation automatique ;
- validation manuelle ;
- validation avant sauvegarde ;
- validation avant compilation.

## Classes possibles

```csharp
IGraphValidator
GraphValidationResult
GraphValidationError
GraphValidationWarning
GraphValidationSeverity
```

---

# 13. Système de types et règles de compatibilité

Le système de types est central pour garantir des connexions correctes.

## Classes possibles

```csharp
GraphValueType
GraphPortType
GraphConnectionRule
GraphTypeCompatibilityService
```

## Règles à prévoir

- type exact ;
- type compatible ;
- conversion implicite ;
- conversion explicite ;
- adaptateur automatique ;
- wildcard port ;
- port générique ;
- port typé dynamiquement selon connexion.

## Exemple de compatibilité

```text
Float -> Float      OK
Int -> Float        OK avec conversion implicite
Float -> Int        Possible avec conversion explicite
Texture2D -> Color  Interdit
Vector3 -> Vector4  Possible avec conversion
Exec -> Float       Interdit
```

## Conversion automatique

Dans certains cas, le système peut insérer automatiquement un nœud de conversion.

Exemple :

```text
Int -> Float
Color -> Vector4
Vector3 -> Vector4
Texture2D -> TextureSample
```

---

# 14. Undo / redo transactionnel

L’undo / redo doit être intégré très tôt. C’est beaucoup plus difficile à ajouter après coup.

## Actions à supporter

- création de nœud ;
- suppression de nœud ;
- déplacement ;
- modification de taille ;
- connexion ;
- déconnexion ;
- reconnexion ;
- modification de propriété ;
- renommage ;
- group / ungroup ;
- collapse / expand ;
- copier / coller ;
- alignement ;
- distribution ;
- import ;
- export.

## Architecture possible

```csharp
IGraphCommand
GraphCommandStack
CreateNodeCommand
DeleteNodeCommand
MoveNodeCommand
ResizeNodeCommand
ConnectPortsCommand
DisconnectPortsCommand
ChangeNodePropertyCommand
RenameNodeCommand
```

## Point important

Les commandes doivent modifier le modèle, pas uniquement l’UI.

L’UI doit ensuite se synchroniser avec le modèle.

---

# 15. Sérialisation

La sérialisation doit être versionnée et stable.

## Fonctionnalités principales

- sauvegarde JSON ou XML ;
- version de document ;
- migration de version ;
- GUID stable ;
- positions editor ;
- zoom et pan optionnels ;
- sous-graphes ;
- groupes ;
- commentaires ;
- variables ;
- connexions ;
- propriétés custom ;
- clipboard format séparé ;
- import ;
- export.

## Exemple JSON simplifié

```json
{
  "version": 1,
  "nodes": [],
  "edges": [],
  "groups": [],
  "variables": []
}
```

## Séparation recommandée

```csharp
GraphAssetData
GraphRuntimeData
GraphEditorData
```

`GraphAssetData` contient les données persistées.

`GraphRuntimeData` contient les données compilées ou interprétables.

`GraphEditorData` contient les informations spécifiques à l’éditeur :

- positions ;
- zoom ;
- pan ;
- sélection ;
- commentaires ;
- état de repli des groupes.

---

# 16. Compilation / évaluation du graphe

Selon le type de graphe, le résultat peut être très différent.

## Modes possibles

- graphe interprété ;
- graphe compilé ;
- génération de shader ;
- génération de behavior tree ;
- génération de state machine ;
- génération de dialogue runtime ;
- génération de script ;
- simple données editor.

## Interfaces possibles

```csharp
IGraphCompiler<TOutput>
IGraphEvaluator
IGraphRuntimeNode
IGraphExecutionContext
```

## Exemples de compilateurs

```csharp
MaterialGraphCompiler
ShaderGraphCompiler
BehaviorTreeGraphCompiler
DialogueGraphCompiler
ParticleGraphCompiler
AnimationStateMachineCompiler
```

## Recommandation

Pour MGUI, ne pas coder directement un compilateur spécifique dans le contrôle.

Le contrôle doit rester générique.  
Les compilateurs doivent être fournis par les modules métiers de CasaEngine.

---

# 17. Prévisualisation live

La prévisualisation live est très utile pour certains graphes.

## Graphes concernés

- material graph ;
- shader graph ;
- particle graph ;
- animation graph ;
- render graph ;
- post-process graph.

## Fonctionnalités principales

- preview globale ;
- preview par nœud ;
- recompilation différée ;
- debounce ;
- dirty flag ;
- affichage des erreurs de compilation ;
- mode pause ;
- refresh manuel ;
- visualisation de valeurs sur les ports ;
- visualisation de valeurs sur les câbles.

## Recommandation performance

La preview live peut coûter cher.  
Il faut prévoir :

- un délai avant recompilation ;
- une option pour désactiver la preview ;
- un mode refresh manuel ;
- une limite sur les gros graphes ;
- un cache des résultats.

---

# 18. Recherche dans le graphe

## Fonctionnalités principales

- rechercher par nom ;
- rechercher par type ;
- rechercher dans les commentaires ;
- rechercher dans les propriétés ;
- rechercher les erreurs ;
- rechercher les warnings ;
- panneau de résultats ;
- next / previous ;
- focus sur résultat ;
- filtre par catégorie.

## Exemples

```text
Find: Texture
Find: error
Find: unused
Find: BaseColor
Find: output
```

---

# 19. Performance

La performance est critique pour un contrôle de graphe dans MonoGame/MGUI.

## Fonctionnalités techniques

- virtualisation des nœuds hors écran ;
- ne pas mesurer tous les nœuds à chaque frame ;
- cache des courbes ;
- cache des bounds ;
- spatial index pour hit-test ;
- dirty flags ;
- batch rendering des câbles ;
- éviter les allocations pendant `Update` et `Draw` ;
- zoom avec niveau de détail ;
- désactivation des previews quand trop de nœuds ;
- rendu simplifié en zoom éloigné.

## Classes possibles

```csharp
GraphSpatialIndex
GraphRenderCache
GraphDirtyFlags
GraphHitTestService
GraphWireBatchRenderer
GraphLayoutCache
```

## Règles importantes

- Ne pas recalculer les courbes de câbles si les nœuds n’ont pas bougé.
- Ne pas refaire le layout complet si seule la sélection change.
- Ne pas faire de LINQ dans les boucles de rendu.
- Ne pas allouer de listes temporaires à chaque frame.
- Utiliser des dirty flags précis.
- Séparer hit-test, layout et rendering.

---

# 20. Thème et style MGUI

Le graphe doit être entièrement stylable via le système de thèmes de MGUI.

## Styles à prévoir

```csharp
GraphBackgroundBrush
GraphGridBrush
GraphGridMajorLineBrush
GraphGridMinorLineBrush
NodeBackground
NodeHeaderBackground
NodeBorderBrush
NodeSelectedBorderBrush
NodeErrorBorderBrush
NodeWarningBorderBrush
PortBrush
PortHoverBrush
PortConnectedBrush
EdgeBrush
EdgeSelectedBrush
ErrorBrush
WarningBrush
CommentBoxBrush
```

## Styles par type

```csharp
MathNodeStyle
TextureNodeStyle
EventNodeStyle
OutputNodeStyle
ExecutionPortStyle
DataPortStyle
FloatPortStyle
ColorPortStyle
TexturePortStyle
```

## Objectif

Permettre à CasaEngine d’avoir une interface cohérente avec :

- le thème dark ;
- le thème light ;
- les couleurs d’accent ;
- les styles editor ;
- les styles runtime éventuels.

---

# 21. Drag & drop avec l’éditeur

Le graphe doit s’intégrer avec les autres contrôles de l’éditeur CasaEngine.

## Fonctionnalités utiles

- drag asset texture vers le graphe ;
- drag material vers le graphe ;
- drag entity vers le graphe ;
- drag script vers le graphe ;
- drag variable depuis le blackboard ;
- drag depuis le content browser ;
- drag depuis la property grid ;
- drag d’un prefab ;
- drag d’une animation ;
- drag d’un son.

## Exemples

```text
Texture2D asset -> Texture Sample Node
Material asset  -> Open Material Graph
Entity          -> Entity Reference Node
Variable        -> Getter / Setter Node
Sound asset     -> Play Sound Node
```

---

# 22. Intégration XAML / MGUI

Comme MGUI a une approche proche WPF/XAML, le contrôle doit pouvoir être déclaré en markup.

## Exemple

```xml
<GraphView Name="MaterialGraph"
           ShowGrid="True"
           AllowZoom="True"
           AllowPan="True"
           SnapToGrid="True" />
```

## Templates à prévoir

```xml
<GraphView.NodeTemplate>
    ...
</GraphView.NodeTemplate>

<GraphView.PortTemplate>
    ...
</GraphView.PortTemplate>

<GraphView.EdgeTemplate>
    ...
</GraphView.EdgeTemplate>
```

## Fonctionnalités

- `NodeTemplate` ;
- `PortTemplate` ;
- `EdgeTemplate` ;
- `CommentTemplate` ;
- binding sur `GraphDocument` ;
- commands ;
- events ;
- styles ;
- data templates par type de nœud.

---

# 23. Minimap

La minimap est utile pour les grands graphes.

## Fonctionnalités principales

- aperçu global du graphe ;
- rectangle de viewport visible ;
- clic pour déplacer la vue ;
- drag du rectangle visible ;
- affichage simplifié des nœuds ;
- option afficher / masquer ;
- position configurable.

## Classe possible

```csharp
MGGraphMinimap
```

---

# 24. Bookmarks

Les bookmarks permettent de naviguer rapidement dans les graphes complexes.

## Fonctionnalités principales

- créer un bookmark sur une position ;
- nommer un bookmark ;
- supprimer un bookmark ;
- aller au bookmark ;
- raccourcis clavier ;
- liste des bookmarks.

## Exemple

```text
1 - Inputs
2 - Main Logic
3 - Output
4 - Debug
```

---

# 25. Debug runtime

Pour les graphes exécutables, il faut prévoir une future couche de debug.

## Fonctionnalités avancées

- afficher les nœuds exécutés ;
- afficher les valeurs sur les ports ;
- afficher les valeurs sur les câbles ;
- breakpoints ;
- step into ;
- step over ;
- step out ;
- pause ;
- resume ;
- compteur d’exécution ;
- temps d’exécution par nœud ;
- erreurs runtime.

## Classes possibles

```csharp
GraphDebugSession
GraphRuntimeTrace
GraphNodeExecutionInfo
GraphBreakpoint
```

---

# 26. Version V1 recommandée

La V1 doit rester réaliste et poser les fondations.

## Fonctionnalités V1

- `MGGraphView` ;
- pan ;
- zoom ;
- grille ;
- snapping simple ;
- `MGGraphNode` déplaçable ;
- `MGGraphPort` input / output ;
- `MGGraphEdge` en Bézier ;
- création de nœud ;
- suppression de nœud ;
- connexion ;
- déconnexion ;
- sélection simple ;
- sélection multiple ;
- menu contextuel basique ;
- validation simple des types ;
- sérialisation JSON ;
- undo / redo minimum ;
- comment boxes simples ;
- sample de graphe simple.

## Sample recommandé

Créer un sample de type **Dialogue Graph** ou **Material Graph simplifié**.

Le dialogue graph est souvent plus simple pour valider :

- nœuds ;
- ports ;
- connexions ;
- texte ;
- choix ;
- sauvegarde ;
- chargement.

Le material graph est plus utile pour CasaEngine à long terme, mais demande plus de logique de compilation.

---

# 27. Version V2 recommandée

## Fonctionnalités V2

- blackboard ;
- recherche ;
- copier / coller ;
- couper / coller ;
- duplicate ;
- align ;
- distribute ;
- reroute nodes ;
- named reroutes ;
- sous-graphes ;
- collapse group ;
- property grid intégrée ;
- preview live ;
- validation avancée ;
- drag & drop assets ;
- minimap ;
- bookmarks.

---

# 28. Version V3 avancée

## Fonctionnalités V3

- graph compiler ;
- visual scripting ;
- material graph complet ;
- shader graph ;
- particle graph ;
- behavior tree graph ;
- animation state machine ;
- node library extensible par plugins ;
- custom nodes par réflexion C# ;
- graph diff / merge ;
- profiling du graphe ;
- génération de code ;
- génération de shader ;
- debug runtime ;
- affichage de valeurs live sur les câbles ;
- breakpoints.

---

# 29. Architecture de dossiers recommandée

```text
MGUI.Core/UI/Graph/
 ├─ Controls/
 │   ├─ MGGraphView.cs
 │   ├─ MGGraphNode.cs
 │   ├─ MGGraphPort.cs
 │   ├─ MGGraphEdge.cs
 │   ├─ MGGraphCommentBox.cs
 │   ├─ MGGraphBlackboard.cs
 │   └─ MGGraphMinimap.cs
 │
 ├─ Model/
 │   ├─ GraphDocument.cs
 │   ├─ GraphNodeModel.cs
 │   ├─ GraphPortModel.cs
 │   ├─ GraphEdgeModel.cs
 │   ├─ GraphGroupModel.cs
 │   └─ GraphVariableModel.cs
 │
 ├─ Interaction/
 │   ├─ GraphSelectionManager.cs
 │   ├─ GraphDragController.cs
 │   ├─ GraphConnectionController.cs
 │   ├─ GraphKeyboardController.cs
 │   └─ GraphContextMenuController.cs
 │
 ├─ Rendering/
 │   ├─ GraphGridRenderer.cs
 │   ├─ GraphEdgeRenderer.cs
 │   ├─ GraphNodeRenderer.cs
 │   ├─ GraphRenderCache.cs
 │   └─ GraphWireBatchRenderer.cs
 │
 ├─ Serialization/
 │   ├─ GraphSerializer.cs
 │   ├─ GraphClipboardSerializer.cs
 │   └─ GraphMigrationService.cs
 │
 ├─ Validation/
 │   ├─ IGraphValidator.cs
 │   ├─ GraphValidationResult.cs
 │   ├─ GraphValidationError.cs
 │   └─ GraphTypeCompatibilityService.cs
 │
 ├─ Commands/
 │   ├─ IGraphCommand.cs
 │   ├─ GraphCommandStack.cs
 │   ├─ CreateNodeCommand.cs
 │   ├─ DeleteNodeCommand.cs
 │   ├─ MoveNodeCommand.cs
 │   ├─ ResizeNodeCommand.cs
 │   ├─ ConnectPortsCommand.cs
 │   ├─ DisconnectPortsCommand.cs
 │   └─ ChangeNodePropertyCommand.cs
 │
 └─ Compilation/
     ├─ IGraphCompiler.cs
     ├─ IGraphEvaluator.cs
     └─ GraphCompilationResult.cs
```

---

# 30. Priorités d’architecture

Les trois points les plus importants à bien concevoir dès le départ sont :

## 1. Séparation modèle / contrôle visuel

Le modèle doit pouvoir exister sans l’UI.

Cela permet :

- tests unitaires ;
- sérialisation ;
- validation ;
- compilation ;
- génération runtime ;
- intégration editor ;
- réutilisation dans plusieurs graphes.

## 2. Système de ports typés

Les ports doivent gérer :

- direction ;
- type ;
- cardinalité ;
- compatibilité ;
- valeur par défaut ;
- conversion ;
- validation.

C’est le cœur logique du système.

## 3. Sérialisation versionnée avec GUID stables

Chaque élément doit avoir un identifiant stable.

C’est nécessaire pour :

- sauvegarde ;
- chargement ;
- undo / redo ;
- copier / coller ;
- diff ;
- migration ;
- références externes ;
- debug runtime.

---

# 31. Résumé des fonctionnalités principales

## Indispensable

- GraphView avec pan / zoom / grid.
- Nœuds déplaçables.
- Ports input / output.
- Connexions visuelles.
- Validation de type.
- Sélection.
- Création / suppression.
- Sérialisation.
- Undo / redo.
- Séparation modèle / UI.

## Très important

- Comment boxes.
- Groupes.
- Reroute nodes.
- Menu contextuel intelligent.
- Blackboard.
- Property grid.
- Recherche.
- Drag & drop.
- Minimap.
- Performances avec caches et dirty flags.

## Avancé

- Sous-graphes.
- Named reroutes.
- Preview live.
- Compilation.
- Visual scripting.
- Shader/material graph.
- Particle graph.
- Behavior tree.
- Debug runtime.
- Profiling.
- Graph diff / merge.

---

# 32. Recommandation finale

Pour MGUI, il faut commencer par un **contrôle générique de graphe**, pas par un système métier trop spécifique.

La meilleure approche est :

1. créer `MGGraphView` ;
2. créer le modèle `GraphDocument` ;
3. ajouter `GraphNodeModel`, `GraphPortModel`, `GraphEdgeModel` ;
4. afficher des nœuds simples ;
5. ajouter pan / zoom / grid ;
6. ajouter les connexions ;
7. ajouter la sélection ;
8. ajouter undo / redo ;
9. ajouter la sérialisation ;
10. créer un sample concret.

Le premier sample recommandé est un **Dialogue Graph simple**, car il permet de tester l’architecture sans complexité de rendu ou de compilation shader.

Ensuite, le même système pourra être spécialisé pour :

- material graph ;
- particle graph ;
- behavior tree ;
- visual scripting ;
- animation graph.

Le contrôle doit rester dans MGUI, tandis que les usages métiers doivent rester dans CasaEngine ou dans des modules spécialisés.
