> Note: ce fichier décrit bien le fond du sujet, mais il mélange audit, design cible, refactor préparatoire et backlog de phase suivante.
> Pour une exécution plus fiable par un agent IA, utiliser le plan phasé dans `TASKS_CLIP_ARCHITECTURE_AGENT_EXECUTION_PLAN.md`.

## Contexte

MGUI supporte déjà le rendu de formes arrondies pour certains contrôles via une séparation partielle entre :

- la shape / géométrie de box ;
- le paint appliqué à cette shape.

Cependant, le clipping du contenu reste encore architecturé autour d’un clip rectangulaire implicite basé sur le scissor rectangle.

Le but de cette phase n’est pas encore d’implémenter le stencil/mask, mais de :

1. auditer toute l’architecture de rendu actuelle ;
2. vérifier où la séparation “dessin de la forme” vs “clip du contenu” est correcte ou incorrecte ;
3. corriger les problèmes architecturaux qui empêcheraient l’introduction propre d’un clip composable ;
4. préparer le terrain pour une future coexistence :
   - scissor rectangulaire par défaut,
   - stencil/mask pour clip arrondi ou arbitraire.

---

## Constats déjà connus à confirmer pendant l’audit

- Le dessin de formes arrondies existe déjà pour les box shapes.
- `MGElement.Draw(...)` applique encore `ClipToBounds` comme un clip rectangulaire unique.
- L’API de rendu n’expose aujourd’hui qu’un clip rectangle.
- Les brushs ne sont pas encore totalement shape-first.
- L’overlay et certains chemins de draw utilisent encore des bounds rectangulaires même si la forme est arrondie.
- Il faut distinguer explicitement :
  - le dessin de la shape visuelle,
  - le clip du contenu enfant.

---

## Objectif global

Produire une architecture claire où :

- le **paint de la forme** ne dépend pas du système de clip ;
- le **clip du contenu** devient une responsabilité distincte ;
- les contrôles déclarent explicitement leur besoin de clip ;
- le pipeline de rendu peut plus tard choisir :
  - scissor,
  - stencil,
  - mask,
  - aucun clip.

---

## Contraintes

- Ne pas casser le rendu existant.
- Ne pas remplacer brutalement tout le pipeline.
- Ne pas introduire le stencil dans cette phase.
- Garder le scissor rectangle comme clip simple par défaut.
- Préserver les optimisations actuelles pour les cas rectangulaires.
- Favoriser une architecture composable et extensible.
- Ne pas dupliquer la logique de clip dans les contrôles.

---

## Livrables attendus

- `Docs/clip-architecture-audit.md`
- `Docs/shape-vs-content-clip-findings.md`
- `Docs/clip-abstraction-proposal.md`
- code refactor minimal pour corriger les problèmes structurels identifiés
- éventuels TODO ciblés pour la phase stencil/mask

---

## Tâches

### 1. Auditer l’API de rendu exposée aux couches UI

- [x] Identifier toutes les API actuelles liées au clipping dans la couche rendering.
- [x] Lister ce que le contrat `IUIRenderContext` autorise aujourd’hui.
- [x] Lister les hypothèses implicites imposées par le contrat actuel :
  - [x] clip rectangle uniquement ;
  - [x] scissor-only ;
  - [x] absence de notion de clip shape ;
  - [x] absence de clip stack explicite.
- [x] Produire une synthèse sur les limitations du contrat actuel.

#### Critère d’acceptation

- [x] Le rapport décrit précisément pourquoi l’API actuelle empêche une abstraction de clip moderne.

---

### 2. Auditer le cycle de draw de `MGElement`

- [x] Étudier complètement le flux de `MGElement.Draw(...)`.
- [x] Séparer conceptuellement les étapes :
  - [x] calcul de transform ;
  - [x] application du clip ;
  - [x] draw des composants avant background ;
  - [x] draw background ;
  - [x] draw self ;
  - [x] draw contents ;
  - [x] draw overlay ;
  - [x] draw des composants après contents.
- [x] Identifier quelles étapes devraient être clipées :
  - [x] par le clip du parent ;
  - [x] par le clip propre de l’élément ;
  - [x] par aucun clip.
- [x] Documenter les confusions actuelles entre :
  - [x] bounds visuels ;
  - [x] bounds de clip ;
  - [x] bounds de contenu.

#### Critère d’acceptation

- [x] Le rapport explique précisément où le cycle actuel mélange encore dessin et clipping.

---

### 3. Identifier tous les contrôles qui dessinent une forme non rectangulaire

- [x] Lister tous les contrôles qui utilisent :
  - [x] `MGBoxShape`
  - [x] `MGBoxGeometry`
  - [x] `MGCornerRadius`
  - [x] un draw path arrondi
- [x] Pour chacun, indiquer :
  - [x] si seule la forme est arrondie ;
  - [x] si le contenu est encore clipé en rectangle ;
  - [x] si l’overlay reste rectangulaire ;
  - [x] si les enfants devraient être clipés selon la forme.

#### Critère d’acceptation

- [x] La liste distingue clairement “shape arrondie visible” et “clip enfant réellement arrondi”.

---

### 4. Identifier tous les contrôles qui dépendent de `ClipToBounds`

- [x] Lister tous les contrôles qui utilisent `ClipToBounds=true` ou s’appuient structurellement sur le clip parent.
- [x] Les classer par catégorie :
  - [x] scrolling / viewport ;
  - [x] conteneurs génériques ;
  - [x] host mono-content ;
  - [x] overlays / popups ;
  - [x] templates / presenters ;
  - [x] docking / tabs / listes.
- [x] Pour chaque contrôle, documenter :
  - [x] si un clip rectangle suffit ;
  - [x] si un clip arrondi ou arbitraire serait nécessaire à terme ;
  - [x] si le contrôle suppose aujourd’hui un clip rectangulaire dur.

#### Critère d’acceptation

- [x] Le repo dispose d’une cartographie claire des dépendances au clip rectangulaire.

---

### 5. Séparer conceptuellement “Visual Shape” et “Content Clip Shape”

- [x] Introduire dans la documentation une distinction explicite entre :
  - [x] forme visuelle de fond/bordure ;
  - [x] forme de clip du contenu ;
  - [x] zone logique de layout ;
  - [x] zone de hit test.
- [x] Définir les cas où ces 4 notions sont identiques.
- [x] Définir les cas où elles divergent.

#### Critère d’acceptation

- [x] Le vocabulaire de l’architecture devient explicite et non ambigu.

---

### 6. Auditer les backgrounds, borders et overlays

- [x] Vérifier pour chaque contrôle / base class si :
  - [x] le background suit bien la shape ;
  - [x] la border suit bien la shape ;
  - [x] l’overlay suit encore un rectangle ;
  - [x] des composants décoratifs sont dessinés hors de la shape.
- [x] Repérer les appels qui utilisent encore seulement `Rectangle Bounds`.
- [x] Identifier les overlays/brushes qui doivent devenir shape-aware.

#### Critère d’acceptation

- [x] Les chemins de draw rectangulaires restants sont identifiés et classés par priorité.

---

### 7. Auditer les interfaces de brush pour repérer le couplage résiduel

- [x] Vérifier si les `IFillBrush` restent rectangle-first.
- [x] Vérifier si les `IBorderBrush` restent rectangle-first.
- [x] Vérifier quels brushes :
  - [x] consomment réellement `MGBoxGeometry`,
  - [x] ignorent encore la géométrie,
  - [x] retombent vers un rendu rectangulaire.
- [x] Distinguer les brushes faciles à migrer de ceux qui nécessitent un design spécifique.

#### Critère d’acceptation

- [x] Un plan clair de migration shape-first des brushes est documenté.

---

### 8. Identifier les cas où le clip ne doit pas suivre la forme

- [x] Lister les contrôles où le clip doit rester rectangulaire même si la shape visuelle est arrondie.
- [x] Exemples à vérifier :
  - [x] certains backgrounds décoratifs ;
  - [x] effets hover ;
  - [x] shadows externes ;
  - [x] handles / grips / adorners ;
  - [x] docking previews.
- [x] Définir les règles de priorité entre clip visuel et clip logique.

#### Critère d’acceptation

- [x] Le design n’impose pas à tort un clip arrondi partout.

---

### 9. Définir un contrat d’abstraction de clip, sans l’implémenter encore

- [x] Écrire un document d’architecture proposant des concepts comme :
  - [x] `ClipKind`
  - [x] `ClipShape`
  - [x] `ClipScope`
  - [x] `ClipRequest`
  - [x] `IClipDefinition`
- [x] Décrire les variantes minimales :
  - [x] None
  - [x] Rectangle
  - [x] RoundedRectangle
  - [x] ArbitraryGeometry
- [x] Décrire les métadonnées utiles :
  - [x] bounds ;
  - [x] corner radius ;
  - [x] géométrie ;
  - [x] stratégie préférée ;
  - [x] possibilité d’intersection / nesting.

#### Critère d’acceptation

- [x] Une proposition d’abstraction de clip existe et peut servir de base à la phase suivante.

---

### 10. Définir la politique de fallback de clip

- [x] Définir comment un clip demandé sera résolu :
  - [x] rectangle -> scissor ;
  - [x] rounded rectangle -> stencil ou mask ;
  - [x] geometry arbitraire -> stencil ou mask ;
  - [x] fallback rectangle si la stratégie demandée n’est pas disponible.
- [x] Définir qui prend cette décision :
  - [x] le contrôle ;
  - [x] le clip manager ;
  - [x] le render context ;
  - [x] une policy centrale.

#### Critère d’acceptation

- [x] La responsabilité de résolution du clip est documentée.

---

### 11. Corriger `MGElement.Draw(...)` pour enlever les hypothèses les plus fortes

- [x] Refactorer `MGElement.Draw(...)` pour ne plus considérer que `ClipToBounds` implique forcément un scissor rect immédiat.
- [x] Introduire une étape séparée de “demande de clip”.
- [x] Préparer le flux pour qu’un clip scope puisse être injecté plus tard.
- [x] Garder le comportement observable identique tant que seule la stratégie rectangle existe.

#### Critère d’acceptation

- [x] `MGElement.Draw(...)` est prêt à déléguer le clip à une abstraction.

---

### 12. Introduire une distinction explicite entre clip de self et clip de contents

- [x] Définir si l’élément a :
  - [x] un clip pour tout son draw ;
  - [x] un clip spécifique pour les contents ;
  - [x] aucun clip.
- [x] Prévoir un contrat comme :
  - [x] `GetSelfClipDefinition()`
  - [x] `GetContentsClipDefinition()`
  - [x] ou un équivalent.
- [x] Documenter les valeurs par défaut.

#### Critère d’acceptation

- [x] Le moteur peut à terme clipper différemment la shape et le contenu.

---

### 13. Revoir la logique d’overlay pour les shapes arrondies

- [x] Identifier les overlays qui doivent suivre la shape et non les bounds rectangulaires.
- [x] Introduire une version shape-aware du chemin overlay.
- [x] Garder un fallback rectangle pour compatibilité.

#### Critère d’acceptation

- [x] Les overlays ne redeviennent pas rectangulaires sur une shape arrondie.

---

### 14. Définir le comportement de hit test indépendamment du clip

- [x] Documenter explicitement que cette phase ne mélange pas encore :
  - [x] clip visuel ;
  - [x] hit test shape-aware.
- [x] Identifier toutefois les contrôles où cette divergence sera visible.
- [x] Ajouter une note de roadmap.

#### Critère d’acceptation

- [x] Le design ne confond pas clip rendu et hit testing.

---

### 15. Documenter les cas particuliers

- [x] Scroll viewers
- [x] tab controls
- [x] listes virtualisées si applicables
- [x] fenêtres
- [x] overlays contextuels
- [x] docking previews
- [x] tooltips / popups
- [x] contrôle avec render scale

Pour chacun :
- [x] décrire le clip actuel ;
- [x] décrire le clip cible ;
- [x] décrire les risques d’intégration.

#### Critère d’acceptation

- [x] Les contrôles sensibles sont identifiés avant la phase stencil/mask.

---

### 16. Produire une liste de debt technique résiduelle

- [x] Lister les APIs rectangle-first qui doivent survivre temporairement.
- [x] Lister les adaptations de compatibilité acceptées.
- [x] Lister ce qui doit absolument être corrigé avant la phase stencil/mask.
- [x] Lister ce qui peut être laissé à une phase ultérieure.

#### Critère d’acceptation

- [x] La phase suivante a des prérequis clairs.

---

## Ordre recommandé d’exécution

1. Audit du contrat `IUIRenderContext`
2. Audit de `MGElement.Draw(...)`
3. Cartographie des contrôles shape-aware
4. Cartographie des dépendances à `ClipToBounds`
5. Vocabulaire architecture : visual shape / content clip shape
6. Audit backgrounds / borders / overlays
7. Audit brushes shape-first vs rectangle-first
8. Cas où le clip ne suit pas la forme
9. Proposition d’abstraction de clip
10. Politique de fallback
11. Refactor préparatoire de `MGElement.Draw(...)`
12. Distinction self clip / contents clip
13. Revue overlay
14. Note hit test
15. Cas particuliers
16. Dette technique / prérequis phase suivante

---

## Résultat attendu

À la fin de cette phase :

- le repo doit avoir une analyse claire des problèmes structurels ;
- les points de mélange entre shape paint et content clipping doivent être identifiés ;
- `MGElement` doit être préparé à déléguer le clip à une abstraction ;
- l’architecture doit être prête pour introduire un clip composable sans dépendre uniquement du scissor.

---

## Règles pour l’agent IA

- Ne pas implémenter le stencil dans ce fichier de tâches.
- Prioriser la clarté architecturale.
- Produire des petits commits logiques.
- Documenter toute hypothèse.
- Conserver la compatibilité comportementale autant que possible.
- Ne pas dupliquer la logique de clip dans les contrôles.

