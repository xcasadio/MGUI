# Taches drawing : formes et paints

## Objectif

Terminer le travail restant du pipeline de formes et de paints decrit dans `Docs/drawing-architecture.md` : composition des paints de bordure stateful dans les fills composites, hit testing conscient de la forme pour `MGBoxShape`, et extension des paints textures aux meshes arrondis.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- Faire 1 commit par tache.
- Mettre a jour le statut de chaque tache dans ce fichier (⚪ -> 🟡 -> ✅).
- Si une tache est bloquee, la marquer ⛔ et decrire le blocage.
- Ne pas faire de refactor hors perimetre.
- Ajouter des tests pour chaque changement de comportement.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

- `dotnet build MGUI.sln`
- `dotnet test MGUI.Tests/MGUI.Tests.csproj --filter "FullyQualifiedName~Architecture"`

## Taches

### Tache 1 — ✅ Composer les paints de bordure stateful dans MGBorderedFillBrush

**But** : permettre a `MGBorderedFillBrush` d'accepter `MGHighlightBorderBrush` sans casser le contrat de cycle de vie (« les paints composites transferent les appels d'update de facon previsible »).

**Travail attendu** :
- Etat actuel : `MGUI.Core/UI/Brushes/Fill Brushes/MGBorderedFillBrush.cs:33-36` leve `NotImplementedException` quand `BorderBrush` est un `MGHighlightBorderBrush`, car `IFillBrush` (`MGUI.Core/UI/Brushes/Fill Brushes/IFillBrush.cs`) n'a pas de methode `Update` : l'animation ne serait jamais tickee. `IBorderBrush.Update(UpdateBaseArgs)` existe deja (defaut vide) et `MGHighlightBorderBrush.Update` transfere a son `Underlay`.
- Ajouter un hook `Update(UpdateBaseArgs)` par defaut vide sur `IFillBrush` (symetrique de celui d'`IBorderBrush`), l'implementer dans `MGBorderedFillBrush` pour transferer a `BorderBrush.Update` (et aux fills imbriques qui en auraient besoin), et cabler l'appel la ou les `IBorderBrush.Update` sont deja invoques cote `MGElement`/brushes de fond.
- Verifier les autres fills composites (`MGCompositedFillBrush`, `MGPaddedFillBrush`, brushes d'etat visuel) et transferer l'update la ou un enfant stateful peut apparaitre.
- Supprimer le garde-fou `NotImplementedException`.

**Criteres d'acceptation** :
- Un `MGBorderedFillBrush` construit avec un `MGHighlightBorderBrush` se cree sans exception et son animation progresse a chaque frame.
- Test unitaire prouvant que `Update` est bien propage du fill composite vers le border brush stateful.
- Aucun changement de comportement pour les fills sans etat.

**Commit recommande** : `fix(drawing): propagate paint lifecycle updates through composite fill brushes`

### Tache 2 — ⚪ Hit testing conscient de la forme pour MGBoxShape

**But** : eviter les hit tests rectangulaires quand la surface interactive visible est arrondie.

**Travail attendu** :
- Etat actuel : `MGUI.Core/UI/MGBoxShape.cs` n'a aucun membre `Contains`/hit-test ; tout le hit testing du framework est rectangulaire.
- Ajouter un contrat de hit test sur `MGBoxShape` (par exemple `Contains(Vector2 point)`), implemente avec le rectangle interne plus les quadrants de coins arrondis, en s'appuyant sur `NormalizedCornerRadius` — sans dependre de `MGBoxGeometry` (le test doit rester analytique et deterministe, pas base sur la tessellation).
- Laisser les controles opter explicitement pour le hit test shape-aware la ou la semantique visuelle l'exige (commencer par `MGBorder` ou un controle a fort rayon de coin) ; ne pas migrer automatiquement tous les hit tests du framework.

**Criteres d'acceptation** :
- Tests unitaires dans `MGUI.Tests/Architecture/` couvrant : point dans le rectangle interne, point dans un coin arrondi (dedans/dehors de l'arc), rayon nul (equivalent au test rectangulaire), rayons clampes.
- Aucun changement de comportement pour les controles qui n'ont pas opte pour le hit test shape-aware.

**Commit recommande** : `feat(drawing): add shape-aware hit testing to MGBoxShape`

### Tache 3 — ⚪ Etendre les paints textures aux meshes arrondis (projection UV)

**But** : supprimer les derniers fallbacks rectangulaires des paints textures en projetant les UVs sur la geometrie arrondie fournie.

**Travail attendu** :
- Etat actuel (commentaires "Phase 1 limitation" dans le code) :
  - `MGUI.Core/UI/Brushes/Fill Brushes/MGTextureFillBrush.cs:133` — rend dans `Shape.OuterBounds` ;
  - `MGUI.Core/UI/Brushes/Fill Brushes/MGNineSliceFillBrush.cs:201` — destinations rectangulaires ;
  - `MGUI.Core/UI/Brushes/Fill Brushes/MGHighlightFillBrush.cs:320` — exclusion rectangulaire ;
  - `MGUI.Core/UI/Brushes/Border Brushes/MGTexturedBorderBrush.cs` — l'overload shape-aware delegue au chemin rectangle, comme les modes `Progress`/`Scan` de `MGHighlightBorderBrush`.
- Introduire un chemin de triangle-list texture dans la couche de rendu (extension de `DrawTransactionBoxShapeExtensions` ou de la couche partagee) acceptant vertices + UVs.
- `MGTextureFillBrush` : projeter les UVs sur `MGBoxGeometry.Vertices` (regles stretch/tile conservees dans le paint).
- `MGTexturedBorderBrush` : mapper bords et coins via `OuterContour`, `InnerContour` et `BorderRingIndices`.
- `MGNineSliceFillBrush` et les exclusions de `MGHighlightFillBrush` / modes `Progress`-`Scan` : traiter dans la meme passe si le cout est raisonnable, sinon les scinder en tache de suivi documentee — ne pas laisser de fallback silencieux non commente.
- Les limitations restantes doivent rester localisees dans les paints ; ne rien faire fuiter dans `MGBorder`, `MGRectangle` ou `MGBoxGeometryBuilder`.

**Criteres d'acceptation** :
- Un fill texture sur une forme a coins arrondis ne deborde plus des coins (plus de rendu rectangulaire par-dessus l'arrondi).
- Le fast path rectangle (`UsesRectangleFastPath`) conserve le rendu texture actuel a l'identique.
- Tests dans `MGUI.Tests/Architecture/` verifiant la projection UV sur le mesh arrondi et la mise a jour des commentaires/documentation des limitations.

**Commit recommande** : `feat(drawing): map textured paints over rounded box geometry`

### Tache 4 — ⚪ Suivi drawing

Items de suivi ouverts par les taches 1 a 3 (voir `Docs/drawing-architecture.md`, Limites connues). Aucun de ces items n'est un fallback silencieux : chacun est commente dans le code a l'endroit concerne.

- Paints partages par reference entre plusieurs elements (`MGDockAutoHideStrip.ButtonBackgroundBrush`, `MGTreeView.SelectionBackgroundBrush`, `MGListBox.AlternatingRowBackgrounds`, et tout border brush partage) : tickes une fois par element consommateur, donc un paint stateful y avance N fois plus vite. Corriger a la racine (copie cote consommateur ou dedup par frame au niveau desktop).
- `MGGraphView` (`GridLineBrush`, `MajorGridLineBrush`, `EdgeBrush`) : seule la couleur est extraite par `MGGraphSurfaceCanvas.ResolveBrushColor`. Si ces brushes sont un jour dessines, surcharger `GetFillBrushes()`. Tout nouvel emplacement `IFillBrush` / `VisualStateFillBrush` de controle doit etre classe (dessine directement / proxy / modele) et le test `FillBrushSlots_AreTickedOrDocumentedExclusions` mis a jour.
