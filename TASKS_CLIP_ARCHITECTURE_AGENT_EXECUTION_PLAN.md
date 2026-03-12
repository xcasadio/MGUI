## Objectif

Transformer l'audit clip/render actuel en un plan d'exécution fiable pour un agent IA, avec un périmètre strict :

1. auditer l'architecture existante sans dériver vers la phase stencil ;
2. isoler les responsabilités shape paint vs content clip ;
3. préparer le pipeline à une abstraction de clip ;
4. conserver un comportement observable identique tant que le backend reste rectangle/scissor.

Ce fichier remplace le déroulé linéaire trop large de `TASKS_CLIP_ARCHITECTURE_AUDIT_AND_REFACTOR.md` par des phases plus petites, chacune avec livrables, fichiers cibles et critère d'arrêt.

---

## Analyse du fichier précédent

Le fichier précédent est bon sur l'intention, mais il est moins adapté à un agent IA pour 5 raisons :

1. il mélange dans une seule suite de tâches : audit, modélisation, refactor de pipeline, revue des overlays et backlog de phase suivante ;
2. plusieurs tâches sont de nature documentaire alors que d'autres exigent un changement de contrat dans le code, sans frontière de commit explicite ;
3. la séquence ne distingue pas assez ce qui relève :
   - du contrat rendering,
   - du pipeline UI,
   - de l'adoption par les contrôles ;
4. il ne force pas assez l'agent à s'arrêter après le refactor préparatoire, avant toute implémentation stencil/mask ;
5. il ne cible pas assez les points d'entrée réels du code déjà identifiés dans le repo.

Les points d'entrée concrets à partir desquels l'agent doit travailler sont :

- `MGUI.Shared/Rendering/IUIRenderContext.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/VisualState.cs`
- `MGUI.Core/UI/Brushes/**`
- les contrôles qui utilisent déjà `MGBoxShape`, `MGBoxGeometry`, `MGCornerRadius` ou `ClipToBounds`

---

## Règles d'exécution pour l'agent IA

- Ne pas implémenter le stencil, ni le mask render target, dans cette phase.
- Ne pas introduire de nouvelle stratégie GPU autre que le scissor existant.
- Ne pas migrer massivement tous les contrôles ; prioriser le contrat, le pipeline commun, puis une cartographie précise.
- Ne pas dupliquer la logique de clip dans les contrôles.
- Faire des petits commits logiques, un par phase ou sous-phase terminée.
- Après chaque phase, mettre à jour les documents et vérifier que le code compile toujours.
- Si une tâche commence à toucher la résolution scissor vs stencil, la reporter explicitement vers `TASKS_COMPOSABLE_CLIP_PIPELINE_WITH_SCISSOR_AND_STENCIL.md`.

---

## Livrables de cette phase

- `Docs/clip-architecture-audit.md`
- `Docs/shape-vs-content-clip-findings.md`
- `Docs/clip-abstraction-proposal.md`
- refactor minimal du pipeline commun pour introduire une demande de clip sans changer le backend
- TODO résiduelle pour la phase composable scissor/stencil

---

## Phase 1. Cartographie réelle du contrat de clip

### But

Établir une base factuelle sur ce que la couche rendering autorise aujourd'hui réellement.

### Tâches

- [ ] Lire et documenter `IUIRenderContext`.
- [ ] Lire et documenter `DrawTransaction.SetClipTarget(...)`, `SetClipTargetTemporary(...)` et `DisableClipTarget()`.
- [ ] Décrire le couplage actuel entre clip et `RasterizerState.ScissorTestEnable`.
- [ ] Décrire l'absence de clip stack explicite au niveau du contrat UI.
- [ ] Décrire l'absence de `ClipKind`, `ClipScope`, `ClipDefinition` ou équivalent.
- [ ] Produire `Docs/clip-architecture-audit.md` avec une section `Current Contract`.

### Fichiers cibles

- `MGUI.Shared/Rendering/IUIRenderContext.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`
- `MGUI.Core/UI/MGDesktop.cs`

### Critère d'arrêt

- [ ] Le document explique précisément pourquoi le contrat actuel est rectangle-only et scissor-coupled.
- [ ] Aucun changement de code comportemental n'est encore fait dans cette phase.

---

## Phase 2. Audit du pipeline de draw UI

### But

Documenter où `MGElement.Draw(...)` mélange encore bounds visuels, bounds de contenu et application de clip.

### Tâches

- [ ] Décomposer le flux de `MGElement.Draw(...)` en étapes nommées.
- [ ] Décrire l'impact du `RenderScale` sur transform et scissor.
- [ ] Documenter le rôle actuel de `ClipToBounds`.
- [ ] Identifier les portions qui devraient relever :
  - [ ] du clip hérité du parent ;
  - [ ] d'un clip demandé par l'élément ;
  - [ ] d'aucun clip.
- [ ] Documenter la place de `DrawBackground`, `DrawSelf`, `DrawContents`, `OverlayBrush` et des composants.
- [ ] Ajouter une section `MGElement Draw Lifecycle` dans `Docs/clip-architecture-audit.md`.

### Fichiers cibles

- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGComponent.cs`
- `MGUI.Core/UI/MGDesktop.cs`

### Critère d'arrêt

- [ ] Le document distingue explicitement : visual bounds, content bounds, clip bounds, actual scissor bounds.
- [ ] Les hypothèses fortes de `MGElement.Draw(...)` sont listées avant tout refactor.

---

## Phase 3. Cartographie des adopteurs shape-aware et clip-aware

### But

Savoir quels contrôles ont une shape visuelle non rectangulaire et quels contrôles dépendent structurellement d'un clip rectangle.

### Tâches

- [ ] Lister les types qui utilisent `MGCornerRadius`, `MGBoxShape`, `MGBoxGeometry` ou des helpers de région de shape.
- [ ] Lister les types qui utilisent `ClipToBounds` explicitement ou implicitement.
- [ ] Classer ces types par catégorie :
  - [ ] border-backed controls ;
  - [ ] scrolling/viewport ;
  - [ ] presenters/hosts ;
  - [ ] overlays/popups ;
  - [ ] docking/tabs/listes ;
  - [ ] décorations et adorners.
- [ ] Pour chaque catégorie, indiquer :
  - [ ] shape visuelle actuelle ;
  - [ ] clip actuel ;
  - [ ] clip cible à terme ;
  - [ ] risque d'intégration.
- [ ] Produire `Docs/shape-vs-content-clip-findings.md`.

### Fichiers cibles

- `MGUI.Core/UI/**`
- `MGUI.Core/UI/Shapes/**`
- `MGUI.Core/UI/Brushes/**`

### Critère d'arrêt

- [ ] Le repo dispose d'une matrice claire `Visual Shape / Current Clip / Target Clip / Priority`.

---

## Phase 4. Vocabulaire d'architecture commun

### But

Fixer les concepts avant de modifier le pipeline.

### Tâches

- [ ] Définir précisément :
  - [ ] `VisualShape`
  - [ ] `ContentClipShape`
  - [ ] `LayoutBounds`
  - [ ] `HitTestShape`
  - [ ] `SelfClip`
  - [ ] `ContentsClip`
- [ ] Définir les cas où ces notions coïncident et divergent.
- [ ] Définir explicitement que le hit test shape-aware n'est pas inclus dans cette phase.
- [ ] Documenter les exceptions où le clip ne doit pas suivre la shape.
- [ ] Ajouter une section `Vocabulary and Non-Goals` dans `Docs/shape-vs-content-clip-findings.md`.

### Critère d'arrêt

- [ ] Le vocabulaire est stable, réutilisable dans le code et dans les docs suivantes.

---

## Phase 5. Proposition de contrat d'abstraction de clip

### But

Définir l'abstraction cible sans brancher encore de nouveau backend.

### Tâches

- [ ] Rédiger `Docs/clip-abstraction-proposal.md`.
- [ ] Proposer des types minimaux, par exemple :
  - [ ] `ClipKind`
  - [ ] `ClipShape`
  - [ ] `ClipRequest`
  - [ ] `ClipScope`
  - [ ] `ClipStrategyPreference`
- [ ] Décrire les variantes minimales :
  - [ ] `None`
  - [ ] `Rectangle`
  - [ ] `RoundedRectangle`
  - [ ] `ArbitraryGeometry`
- [ ] Décrire la politique de résolution future :
  - [ ] rectangle -> scissor ;
  - [ ] rounded rectangle -> stencil ou mask ;
  - [ ] arbitrary geometry -> stencil ou mask.
- [ ] Décrire qui décide la stratégie réelle : la couche rendering, pas les contrôles.

### Critère d'arrêt

- [ ] La proposition est suffisante pour préparer la phase `TASKS_COMPOSABLE_CLIP_PIPELINE_WITH_SCISSOR_AND_STENCIL.md` sans encore l'entamer.

---

## Phase 6. Refactor préparatoire minimal de `MGElement.Draw(...)`

### But

Retirer l'hypothèse la plus forte : `ClipToBounds` n'est pas la technique de clip, mais seulement un besoin de clip simple.

### Tâches

- [ ] Introduire dans `MGElement` une étape distincte de demande de clip.
- [ ] Ajouter un contrat minimal interne, par exemple :
  - [ ] `GetSelfClipRequest()` ;
  - [ ] `GetContentsClipRequest()` ;
  - [ ] ou un équivalent plus adapté au style du repo.
- [ ] Faire en sorte que l'implémentation par défaut retourne encore un clip rectangle issu des bounds existants.
- [ ] Conserver l'utilisation effective du scissor comme backend unique.
- [ ] Ne pas changer le rendu observable.

### Fichiers cibles

- `MGUI.Core/UI/MGElement.cs`
- si nécessaire `MGUI.Shared/Rendering/IUIRenderContext.cs` uniquement pour préparer une future API, sans casser la compatibilité

### Critère d'arrêt

- [ ] `MGElement.Draw(...)` délègue la notion de clip demandé à une petite abstraction locale ou commune.
- [ ] Le backend reste strictement rectangle/scissor.

---

## Phase 7. Séparation explicite self clip vs contents clip

### But

Préparer le moteur à clipper différemment la shape du contrôle et le contenu enfant.

### Tâches

- [ ] Définir quelle partie du draw relève du self clip.
- [ ] Définir quelle partie du draw relève du contents clip.
- [ ] S'assurer que le background/border shape-aware ne dépend pas du contents clip.
- [ ] Encadrer les composants avant/après contents pour éviter les ambiguïtés futures.
- [ ] Ne migrer aucun contrôle individuel tant que le comportement par défaut n'est pas stable.

### Critère d'arrêt

- [ ] Le pipeline sait exprimer conceptuellement deux besoins de clip distincts, même si les deux se résolvent encore en rectangle.

---

## Phase 8. Audit et correctifs ciblés overlay/brushes

### But

Éliminer les derniers endroits où une shape visuelle arrondie repasse par des bounds rectangulaires sans raison.

### Tâches

- [ ] Cartographier les appels `OverlayBrush?.Draw(..., Rectangle)` encore actifs sur des éléments shape-aware.
- [ ] Cartographier les `IFillBrush` et `IBorderBrush` qui ignorent encore la shape/géométrie.
- [ ] Corriger uniquement les chemins communs qui empêcheraient une future abstraction de clip propre.
- [ ] Reporter les migrations brush complexes vers une phase dédiée si elles ne bloquent pas le clip pipeline.

### Critère d'arrêt

- [ ] Les chemins rectangle-first restants sont documentés et priorisés.
- [ ] Les correctifs restent minimaux et architecturaux.

---

## Phase 9. Cas particuliers et dette résiduelle

### But

Clore la phase avec un backlog clair pour la suite.

### Tâches

- [ ] Documenter séparément les cas sensibles :
  - [ ] `MGScrollViewer`
  - [ ] `MGWindow`
  - [ ] tabs
  - [ ] listes virtualisées
  - [ ] overlays contextuels
  - [ ] tooltips/popups
  - [ ] docking previews
  - [ ] render scale
- [ ] Lister ce qui doit être fait avant la phase stencil.
- [ ] Lister ce qui peut attendre la phase composable clip.
- [ ] Ajouter une section `Residual Debt / Next Phase Inputs` dans les docs.

### Critère d'arrêt

- [ ] La phase suivante a des entrées nettes et ne dépend pas d'hypothèses implicites.

---

## Ordre recommandé des commits

1. docs audit rendering + pipeline
2. docs cartographie shape/clip
3. doc proposition d'abstraction clip
4. refactor minimal `MGElement.Draw(...)`
5. séparation self clip / contents clip
6. correctifs overlay/brushes strictement bloquants
7. documentation de clôture et TODO phase suivante

---

## Définition de terminé

La phase est terminée quand :

- le repo documente précisément le contrat de clip actuel ;
- `MGElement` n'assimile plus directement le besoin de clip à la technique scissor dans son modèle conceptuel ;
- le comportement runtime reste identique tant qu'aucune nouvelle stratégie de clip n'est branchée ;
- le terrain est prêt pour la phase suivante sans implémentation stencil prématurée.