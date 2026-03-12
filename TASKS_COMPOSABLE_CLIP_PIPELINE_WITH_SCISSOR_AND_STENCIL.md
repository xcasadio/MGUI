## Contexte

Après audit et refactor préparatoire, MGUI doit évoluer vers une architecture de clipping composable.

Le but n’est pas de “tout passer au stencil”, mais d’introduire une abstraction propre où :

- le **scissor rectangulaire** reste la solution par défaut ;
- le **stencil** ou un **mask** n’est utilisé que lorsque le nœud visuel demande un clip arrondi ou arbitraire ;
- les contrôles ne connaissent pas la stratégie bas niveau ;
- le pipeline de rendu choisit la meilleure implémentation disponible.

---

## Objectif global

Implémenter un pipeline de clip composable qui permet :

1. de déclarer un clip de manière abstraite ;
2. de résoudre ce clip vers une stratégie concrète ;
3. de faire coexister :
   - scissor rectangle,
   - stencil clip,
   - mask clip si nécessaire ;
4. de minimiser les régressions de perf ;
5. de préserver la compatibilité avec l’architecture existante.

---

## Principes de design

- Le clip est une **abstraction**, pas un effet secondaire de `ClipToBounds`.
- Le scissor reste le fast path.
- Le stencil est réservé aux clips non rectangulaires ou imbriqués compatibles.
- Le mask/render target reste disponible comme fallback sur les cas complexes ou incompatibles.
- Les contrôles formulent un **besoin de clip**, pas une technique.
- Le pipeline décide de la stratégie réelle.
- La shape visuelle et le clip du contenu restent deux responsabilités séparées.

---

## Contraintes

- Ne pas casser le rendu rectangulaire existant.
- Ne pas imposer le stencil à tous les draw calls.
- Ne pas forcer les brushs à connaître le stencil.
- Garder les clips rectangles aussi peu coûteux qu’aujourd’hui.
- Prévoir les clips imbriqués.
- Prévoir les interactions avec :
  - transforms,
  - render scale,
  - overlays,
  - render targets temporaires.

---

## Livrables attendus

- `Docs/composable-clip-architecture.md`
- `Docs/stencil-mask-strategy.md`
- abstraction de clip dans la couche rendering
- intégration du scissor rectangle dans cette abstraction
- intégration du stencil comme stratégie optionnelle
- fallback mask / render target documenté et branché si nécessaire
- samples visuels de validation

---

## Tâches

### 1. Introduire les types d’abstraction de clip

- [ ] Créer les types centraux du modèle de clip, par exemple :
  - [ ] `ClipKind`
  - [ ] `ClipStrategy`
  - [ ] `ClipShape`
  - [ ] `ClipDefinition`
  - [ ] `ClipScope`
  - [ ] `ClipResolveResult`
- [ ] Définir les variantes minimales :
  - [ ] None
  - [ ] Rectangle
  - [ ] RoundedRectangle
  - [ ] ArbitraryGeometry
- [ ] Ajouter les métadonnées nécessaires :
  - [ ] bounds ;
  - [ ] corner radius ;
  - [ ] géométrie optionnelle ;
  - [ ] demande explicite de stratégie ;
  - [ ] possibilité d’intersection / nesting.

#### Critère d’acceptation

- [ ] Le rendu peut recevoir un clip abstrait sans connaître sa technique d’implémentation.

---

### 2. Étendre `IUIRenderContext` avec une API de clip composable

- [ ] Ajouter une API dédiée au clip, distincte de `SetClipTargetTemporary(Rectangle?, ...)`.
- [ ] Prévoir un scope disposable.
- [ ] Conserver temporairement les anciennes APIs rectangle pour compatibilité.
- [ ] Définir une API lisible, par exemple :
  - [ ] `PushClip(...)`
  - [ ] `PushClipTemporary(...)`
  - [ ] `ResolveClip(...)`
  - [ ] `PushRectangleClip(...)`
- [ ] Faire en sorte que le nouveau contrat puisse être implémenté d’abord avec scissor only.

#### Critère d’acceptation

- [ ] Le contrat de rendu n’est plus rectangle-only.

---

### 3. Introduire un `ClipManager` ou équivalent

- [ ] Créer un composant responsable de :
  - [ ] résoudre un clip abstrait ;
  - [ ] gérer l’imbrication ;
  - [ ] choisir la stratégie ;
  - [ ] pousser / popper les états GPU ;
  - [ ] restaurer correctement les états précédents.
- [ ] Garder sa responsabilité centrée sur le clip, pas sur le dessin des shapes.

#### Critère d’acceptation

- [ ] La logique de décision clip ne fuit pas dans `MGElement` ni dans les contrôles.

---

### 4. Implémenter la stratégie rectangle via scissor dans la nouvelle abstraction

- [ ] Brancher l’ancienne logique de scissor sur la nouvelle API.
- [ ] Faire du rectangle le fast path par défaut.
- [ ] Conserver l’intersection de clips rectangulaires imbriqués.
- [ ] Vérifier la restauration correcte de l’état précédent.

#### Critère d’acceptation

- [ ] Le comportement actuel du clip rectangle est conservé, mais via la nouvelle abstraction.

---

### 5. Étendre `DrawSettings` pour supporter le stencil

- [ ] Ajouter de nouveaux modes `DepthStencilType` ou un modèle plus expressif.
- [ ] Définir les états nécessaires au stencil :
  - [ ] write mask ;
  - [ ] test equal ;
  - [ ] increment / decrement si nesting ;
  - [ ] clear / restore policy.
- [ ] Documenter précisément les états GPU visés.

#### Critère d’acceptation

- [ ] Le pipeline peut demander explicitement un état stencil cohérent.

---

### 6. Vérifier la compatibilité SpriteBatch / PrimitiveBatch avec le stencil

- [ ] Auditer les limitations de `SpriteBatch.Begin(...)`.
- [ ] Auditer les limitations de `PrimitiveBatch.Begin(...)`.
- [ ] Définir comment les états stencil sont appliqués :
  - [ ] côté SpriteBatch ;
  - [ ] côté primitives ;
  - [ ] lors des transitions de contexte.
- [ ] Si nécessaire, encapsuler la gestion d’état pour éviter les erreurs de state leakage.

#### Critère d’acceptation

- [ ] Le pipeline sait dessiner dans un clip stencil sans incohérence d’état GPU.

---

### 7. Implémenter une stratégie `RoundedRectangle -> Stencil`

- [ ] Définir le chemin stencil pour un rounded rectangle :
  - [ ] écrire la shape dans le stencil ;
  - [ ] dessiner le contenu avec test stencil ;
  - [ ] restaurer l’état.
- [ ] Réutiliser la géométrie existante de box arrondie.
- [ ] Éviter toute duplication de la tessellation.
- [ ] Gérer la translation / transform / offset correctement.

#### Critère d’acceptation

- [ ] Le contenu d’un contrôle peut être clipé par un rounded rectangle via stencil.

---

### 8. Définir la stratégie de nesting stencil

- [ ] Choisir une stratégie claire pour les clips imbriqués :
  - [ ] niveau de stencil incrémental ;
  - [ ] write/read à une valeur cible ;
  - [ ] autre stratégie documentée.
- [ ] Définir une limite acceptable de nesting.
- [ ] Définir le comportement en cas de dépassement.

#### Critère d’acceptation

- [ ] Les clips arrondis imbriqués ont une sémantique stable et documentée.

---

### 9. Ajouter un fallback mask / render target pour les cas non couverts

- [ ] Définir les cas où le stencil n’est pas adapté ou pas disponible :
  - [ ] géométrie arbitraire complexe ;
  - [ ] conflits de pipeline ;
  - [ ] limitations de backend ;
  - [ ] besoin d’anti-aliasing spécifique.
- [ ] Implémenter un fallback basé sur render target + mask si nécessaire.
- [ ] Prévoir du pooling de render targets.
- [ ] Éviter d’activer ce chemin pour les cas simples.

#### Critère d’acceptation

- [ ] Le système peut traiter les cas non rectangulaires au-delà du simple rounded rectangle.

---

### 10. Introduire une politique centrale de résolution de stratégie

- [ ] Créer une policy de décision :
  - [ ] rectangle -> scissor ;
  - [ ] rounded rectangle -> stencil ;
  - [ ] arbitrary geometry -> stencil ou mask ;
  - [ ] fallback -> scissor si acceptable ;
  - [ ] fallback -> mask sinon.
- [ ] Documenter les critères :
  - [ ] coût ;
  - [ ] compatibilité ;
  - [ ] nesting ;
  - [ ] présence d’un render target déjà actif ;
  - [ ] capacité du backend.

#### Critère d’acceptation

- [ ] La stratégie réelle n’est pas décidée localement dans les contrôles.

---

### 11. Intégrer le nouveau clip scope dans `MGElement.Draw(...)`

- [ ] Remplacer l’usage direct du scissor rectangle par l’abstraction de clip.
- [ ] Conserver la logique actuelle pour les éléments rectangle-only.
- [ ] Permettre d’utiliser un clip distinct pour :
  - [ ] self ;
  - [ ] contents.
- [ ] Vérifier que les composants décoratifs restent corrects.

#### Critère d’acceptation

- [ ] `MGElement.Draw(...)` ne dépend plus d’une technique de clip particulière.

---

### 12. Déclarer le clip demandé par les éléments

- [ ] Définir comment un élément demande un clip :
  - [ ] propriété de haut niveau ;
  - [ ] méthode virtuelle ;
  - [ ] metadata calculée.
- [ ] Prévoir au minimum :
  - [ ] rectangle clip ;
  - [ ] rounded-rect clip basé sur sa box shape ;
  - [ ] no clip.
- [ ] Prévoir l’extension future aux clips custom.

#### Critère d’acceptation

- [ ] Les contrôles expriment un besoin de clip sans connaître scissor/stencil/mask.

---

### 13. Faire coexister shape paint et content clip

- [ ] Vérifier que le fond/border arrondi continue à être dessiné comme aujourd’hui.
- [ ] Vérifier que le clip contenu s’applique indépendamment du paint.
- [ ] S’assurer que le stencil n’est pas requis juste pour dessiner la shape.
- [ ] Documenter cette séparation dans le code.

#### Critère d’acceptation

- [ ] Le stencil est utilisé uniquement pour le clipping quand c’est nécessaire.

---

### 14. Gérer correctement les transforms et `RenderScale`

- [ ] Vérifier l’impact des transforms actuelles sur :
  - [ ] scissor ;
  - [ ] stencil geometry ;
  - [ ] fallback mask.
- [ ] Définir la convention de coordonnées du clip.
- [ ] Vérifier le comportement sous `RenderScale`.

#### Critère d’acceptation

- [ ] Le clip reste correct même quand un élément est rendu avec transform/scale.

---

### 15. Gérer les overlays et effets au bon niveau de clip

- [ ] Définir si l’overlay de fond doit :
  - [ ] suivre le self clip ;
  - [ ] suivre le content clip ;
  - [ ] ne pas être clipé.
- [ ] Vérifier les cas :
  - [ ] hover overlay ;
  - [ ] pressed overlay ;
  - [ ] focus visuals ;
  - [ ] décorations docking.
- [ ] Corriger les niveaux de clip si nécessaire.

#### Critère d’acceptation

- [ ] Les overlays ne sont pas accidentellement découpés ou laissés hors clip.

---

### 16. Prévoir un système de pooling pour les ressources temporaires

- [ ] Si le fallback mask utilise des render targets, créer un petit pool.
- [ ] Prévoir la réutilisation par taille / format.
- [ ] Définir la politique de clear / recycle.
- [ ] Éviter les allocations GPU répétées.

#### Critère d’acceptation

- [ ] Les fallbacks n’introduisent pas une dette perf évidente.

---

### 17. Ajouter instrumentation et diagnostics

- [ ] Ajouter des compteurs/debug info pour savoir :
  - [ ] combien de clips scissor ont été utilisés ;
  - [ ] combien de clips stencil ;
  - [ ] combien de fallbacks mask ;
  - [ ] profondeur max de nesting ;
  - [ ] nombre de render targets temporaires.
- [ ] Prévoir un mode debug simple.

#### Critère d’acceptation

- [ ] Le comportement du système de clip est observable et mesurable.

---

### 18. Ajouter des tests et samples visuels

- [ ] Ajouter des scénarios de test couvrant :
  - [ ] clip rectangle simple ;
  - [ ] rounded rectangle simple ;
  - [ ] rounded rectangle imbriqué ;
  - [ ] parent rectangle + enfant arrondi ;
  - [ ] parent arrondi + enfant rectangle ;
  - [ ] scroll viewer avec clip arrondi ;
  - [ ] render scale + clip ;
  - [ ] overlay + clip ;
  - [ ] fallback mask.
- [ ] Ajouter un sample UI de démonstration.

#### Critère d’acceptation

- [ ] Les principaux scénarios de clip sont validés visuellement.

---

### 19. Ajouter une politique de compatibilité / migration

- [ ] Garder l’ancienne API rectangle le temps de la migration.
- [ ] Marquer les points de transition.
- [ ] Documenter comment migrer un contrôle existant.
- [ ] Documenter comment ajouter un nouveau clip kind.

#### Critère d’acceptation

- [ ] Les contributeurs peuvent migrer progressivement.

---

### 20. Documenter l’architecture finale

- [ ] Documenter :
  - [ ] modèle de clip ;
  - [ ] résolution de stratégie ;
  - [ ] fast path scissor ;
  - [ ] stratégie stencil ;
  - [ ] fallback mask ;
  - [ ] interaction avec `MGElement.Draw(...)` ;
  - [ ] interaction avec les shapes.
- [ ] Ajouter un guide “quand utiliser quelle stratégie”.
- [ ] Ajouter une note explicite : “ne pas utiliser le stencil pour dessiner les shapes”.

#### Critère d’acceptation

- [ ] L’architecture est lis
::contentReference[oaicite:1]{index=1}
ible et maintenable par un autre développeur.

---

## Ordre recommandé d’exécution

1. Types d’abstraction de clip
2. Extension de `IUIRenderContext`
3. `ClipManager`
4. Fast path rectangle via scissor
5. Extension de `DrawSettings` pour le stencil
6. Compatibilité SpriteBatch / PrimitiveBatch
7. Rounded rectangle via stencil
8. Nesting stencil
9. Fallback mask / render target
10. Policy centrale de résolution
11. Intégration dans `MGElement.Draw(...)`
12. Déclaration de clip côté éléments
13. Séparation shape paint / content clip
14. Gestion transforms / render scale
15. Gestion overlays
16. Pooling des ressources temporaires
17. Diagnostics
18. Tests et samples
19. Compatibilité / migration
20. Documentation finale

---

## Résultat attendu

À la fin de cette phase, MGUI doit disposer :

- d’un système de clip composable ;
- d’un fast path scissor pour les clips rectangles ;
- d’un chemin stencil pour les clips arrondis ;
- d’un fallback mask pour les cas complexes si nécessaire ;
- d’une séparation propre entre :
  - shape paint,
  - content clip,
  - stratégie GPU.

---

## Règles pour l’agent IA

- Ne pas remplacer le scissor rectangle partout.
- Utiliser le stencil uniquement quand le clip demandé le nécessite.
- Ne pas faire dépendre les contrôles des détails GPU.
- Ne pas coupler les brushs au système de clip.
- Préserver les performances des cas simples.
- Ajouter des tests visuels avant d’élargir l’utilisation du stencil.
- Documenter les hypothèses et limitations de backend.