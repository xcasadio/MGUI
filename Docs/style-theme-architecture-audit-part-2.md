# MGUI - Audit approfondi thème / styles / templates / runtime
#
# Objectif:
# Poursuivre l’analyse du framework MGUI au-delà du premier audit déjà fait sur
# les thèmes, styles, ressources et control templates.
#
# Cet audit doit être mené avec une contrainte fondamentale:
# MGUI est un framework UI pour le jeu vidéo, pas une UI desktop classique.
#
# Cela implique de tenir compte de:
# - performance runtime stable
# - absence de GC évitable
# - coût de layout et de draw prévisible
# - gestion des changements de thème en jeu et dans l’éditeur
# - compatibilité avec renderer MonoGame
# - clipping/scissor/stencil
# - templates et styles utilisables sans couplage fort au code
# - input souris/clavier/manette
# - robustesse pour des écrans in-game et des outils d’édition
#
# Le but de cette mission n’est PAS de coder directement une grosse refonte.
# Le but est d’aboutir à un livrable d’analyse structuré permettant ensuite
# de créer des petites tâches de refactor/correction pour un autre agent IA.

## Contexte

Le dépôt contient déjà une base de système de thèmes/styles/templates.
Une première analyse a montré des points positifs:
- `MGResources` hiérarchique
- styles implicites / explicites
- support `StaticResource` / `DynamicResource`
- `ControlTemplate`
- template parts
- propagation des changements de thème
- réévaluation partielle à chaud

Mais il reste probablement des zones hybrides ou incomplètes:
- contrôles qui continuent à coder une partie du look dans leur logique
- dépendance encore forte à `MGTheme`
- precedence des valeurs non totalement formalisée
- interaction potentiellement risquée entre styles et composants internes
- coût runtime des changements de thème/styles/templates pas encore bien mesuré

L’analyse doit donc être poussée à l’échelle du framework.

---

## Résultat attendu

Produire un fichier d’analyse détaillé, par exemple:

`Docs/audit-theme-style-runtime-deep.md`

Ce document devra contenir:
1. un état des lieux factuel
2. les bonnes pratiques déjà respectées
3. les violations architecturales observées
4. les risques spécifiques à un framework UI pour jeu vidéo
5. les priorités de refactor
6. une proposition de découpage en petites tâches actionnables

Ne pas modifier le code tant que l’analyse n’est pas terminée.

---

## Contraintes de travail

- Ne pas faire de refactor massif pendant cette mission.
- Faire une analyse basée sur le code réel, pas sur des hypothèses.
- Citer les classes/fichiers concernés dans les constats.
- Être précis sur la différence entre:
  - problème architectural
  - dette technique acceptable
  - simple choix d’implémentation
- Toujours juger les décisions par rapport au contexte "UI de jeu vidéo".

---

## Axes d’analyse obligatoires

### 1. Séparation logique du contrôle / apparence / template

Analyser contrôle par contrôle si:
- le comportement métier est séparé du look
- les couleurs, paddings, bordures, overlays, opacités, tailles visuelles
  sont tirés du thème/style/template ou codés en dur
- le contrôle dépend directement de `MGTheme`
- le contrôle modifie en code des sous-éléments qui devraient venir du template
- les contrôles composites sont réellement "lookless" ou seulement partiellement

Livrable attendu dans l’analyse:
- liste des contrôles propres
- liste des contrôles hybrides
- liste des contrôles trop couplés au thème

Points d’attention:
- boutons
- text input
- combo box
- list controls
- dock controls
- scroll viewers / scroll bars
- popups / overlays / tooltips
- panels spéciaux

---

### 2. Precedence des valeurs et source d’une propriété

Vérifier comment une propriété visuelle est résolue selon ses sources possibles:
- valeur locale
- valeur du style implicite
- valeur du style explicite
- valeur injectée par template
- valeur dérivée du thème
- valeur par défaut du contrôle
- valeur réappliquée lors d’un refresh

Objectif:
déterminer si le framework a un comportement stable et prédictible.

Questions à trancher:
- qu’est-ce qui gagne en cas de conflit ?
- un changement de thème peut-il écraser une valeur utilisateur ?
- un template peut-il écraser une valeur locale ?
- un style appliqué tardivement peut-il casser un contrôle déjà initialisé ?
- les `DynamicResource` réappliquent-ils correctement sans side effects ?

Le document final doit contenir:
- une table de precedence observée
- les incohérences
- les cas dangereux

---

### 3. Ressources, scopes et changement de thème runtime

Analyser le système de ressources du point de vue runtime:
- portée des ressources
- ordre de résolution
- fallback
- invalidation
- propagation parent/enfant
- abonnement/désabonnement aux changements
- coût des réévaluations

Questions importantes:
- combien d’objets sont notifiés lors d’un changement de thème global ?
- existe-t-il des risques de double abonnement ?
- existe-t-il des risques de fuite mémoire via callbacks/events ?
- la réévaluation dynamique est-elle granulaire ou trop large ?
- les ressources de thème sont-elles assez découplées du code ?

Livrable:
- schéma textuel du pipeline de résolution de ressource
- liste des points sensibles
- estimation qualitative du coût runtime

---

### 4. ControlTemplate et ElementTemplate

Analyser si le système de template est adapté à un framework UI de jeu vidéo.

Vérifier:
- comment les templates sont créés
- quand ils sont instanciés
- comment les template parts sont retrouvées
- si les templates peuvent être recréés trop souvent
- si les templates allouent trop au runtime
- si la logique de contrôle dépend d’une structure trop rigide
- si les composants internes peuvent être stylés proprement sans casser l’encapsulation

Questions importantes:
- un contrôle peut-il changer de template proprement à chaud ?
- le rebuild de template est-il trop coûteux pour l’in-game ?
- les parts attendues sont-elles validées clairement ?
- les erreurs de template sont-elles faciles à diagnostiquer ?

Livrable:
- analyse du cycle de vie des templates
- liste des coûts potentiels
- liste des validations manquantes
- proposition d’amélioration sans casser l’API

---

### 5. Compatibilité renderer jeu vidéo

Analyser la couche UI du point de vue du rendu temps réel.

Points à vérifier:
- séparation entre résolution du style et génération des draw commands
- dépendance éventuelle entre thème et renderer
- coût des arrondis, bordures, backgrounds, overlays
- impact des templates sur le nombre d’éléments rendus
- gestion du clipping:
  - scissor rectangle
  - clip arrondi
  - stencil/mask
- risque de surcoût dû aux nœuds visuels décoratifs

Questions:
- les templates encouragent-ils une explosion du nombre de draw calls ?
- certaines décorations pourraient-elles être fusionnées ?
- y a-t-il des contrôles qui imposent du clipping coûteux inutilement ?
- la hiérarchie visuelle générée est-elle raisonnable pour une UI de jeu ?

Livrable:
- zones à risque pour le renderer
- éléments pouvant générer trop de draw overhead
- suggestions architecturales orientées runtime

---

### 6. Layout / Measure / Arrange et interaction avec styles/templates

Analyser si les styles et templates perturbent le layout.

Vérifier:
- quand les valeurs de style influencent measure/arrange
- si un changement de thème déclenche trop de re-layout
- si les templates changent la structure de layout de manière imprévisible
- si certains contrôles calculent encore leur layout avec du look codé en dur

Questions:
- le layout est-il stable sous changement de thème ?
- peut-on avoir des invalidations en cascade inutiles ?
- certaines propriétés de thème devraient-elles être "render only" plutôt que "layout affecting" ?
- les paddings/margins/thickness sont-ils bien traités comme données de style ?

Livrable:
- cartographie des invalidations layout
- cas de relayout excessif
- recommandations spécifiques jeu vidéo

---

### 7. Input et états visuels

Analyser l’architecture des visual states et de l’input.

Pour un framework UI de jeu vidéo, il faut penser à:
- souris
- clavier
- gamepad
- focus navigation
- hover facultatif selon plateforme
- pressed/selected/disabled/focused
- états cohérents entre in-game et éditeur

Vérifier:
- si les états visuels sont centralisés ou dispersés
- si les contrôles changent eux-mêmes leurs couleurs au lieu de changer d’état
- si les états sont template-friendly
- si les styles/templates peuvent réagir proprement au focus gamepad

Questions:
- le système est-il prêt pour une navigation manette complète ?
- hover est-il trop supposé partout ?
- focus et selected sont-ils assez distincts ?
- disabled est-il traité par style ou par logique ad hoc ?

Livrable:
- état du système de visual states
- manques pour le support console/manette
- recommandations d’architecture

---

### 8. Robustesse pour l’éditeur in-engine

MGUI servira aussi pour des outils d’édition.
Il faut donc analyser si le système de thème/style/template est viable pour:
- inspector panels
- docking
- panneaux imbriqués
- multi-fenêtres de vue
- thèmes éditeur dark/light
- overlays d’édition
- contrôles très denses

Vérifier:
- si les styles implicites par type suffisent pour un thème éditeur cohérent
- si les styles nommés permettent des variantes propres
- si les templates peuvent supporter des contrôles complexes d’éditeur
- si les contrôles composites exposent assez leurs parts internes
- si le système de ressources supporte bien des sous-arbres thémés différemment

Questions:
- peut-on faire un "EditorTheme" propre sans dupliquer la moitié du code ?
- les composants de docking/panels sont-ils suffisamment découplés du thème par défaut ?
- les variations denses/compactes sont-elles possibles proprement ?

Livrable:
- évaluation de la capacité du framework à supporter un vrai thème éditeur
- liste des points qui bloquent

---

### 9. Debuggabilité / maintenabilité

Un framework UI complexe doit être déboguable.

Analyser:
- comment savoir d’où vient une valeur de propriété
- comment diagnostiquer un style appliqué
- comment diagnostiquer un template manquant
- comment comprendre pourquoi une resource n’a pas été résolue
- comment repérer les changements de thème qui réappliquent trop

Questions:
- existe-t-il des logs ou hooks de debug suffisants ?
- peut-on tracer la source d’un setter ?
- peut-on inspecter facilement les template parts ?
- les erreurs sont-elles trop silencieuses ?

Livrable:
- liste des outils de debug existants
- liste des manques
- recommandations d’outillage pour l’éditeur

---

### 10. Perf / allocations / invalidation

Analyser explicitement le coût runtime.

Mesurer ou estimer qualitativement:
- allocations lors du chargement XAML
- allocations lors de l’application des styles
- allocations lors d’un changement de thème
- coût des templates
- coût de la propagation aux enfants
- coût des abonnements aux dynamic resources

Identifier:
- patterns d’allocation évitables
- reprocessing inutile
- rebuild complet alors qu’un refresh partiel suffirait
- structures qui pourraient être mises en cache

Le livrable doit contenir une section:
- `Hot paths potentiels`
- `Allocations évitables`
- `Invalidations trop larges`

---

## Méthode de travail recommandée

### Étape 1 - Cartographie
Identifier tous les blocs concernés:
- ressources
- styles
- templates
- base control
- renderer hooks
- layout hooks
- input/state hooks

Créer une carte des classes principales impliquées.

### Étape 2 - Échantillonnage de contrôles
Choisir un panel représentatif de contrôles simples et complexes:
- simple button
- text block / text box
- combo box
- list item / tree item
- popup / tooltip
- dock-related control
- scroll container
- un contrôle composite complexe

Comparer leur niveau de découplage.

### Étape 3 - Vérification des pipelines
Reconstituer les pipelines suivants:
- application de style
- résolution de ressource
- application de template
- changement de thème
- invalidation layout
- invalidation render

### Étape 4 - Analyse orientée jeu vidéo
Reprendre les constats précédents avec le filtre:
- coût runtime
- console/gamepad
- editor tooling
- robustesse en temps réel

### Étape 5 - Rédaction du rapport
Rédiger le fichier final avec sections claires et références code.

---

## Format obligatoire du rapport final

Le rapport final doit contenir exactement ces sections:

1. `Résumé exécutif`
2. `Architecture actuelle observée`
3. `Points solides`
4. `Couplages et fragilités`
5. `Risques spécifiques au contexte jeu vidéo`
6. `Risques spécifiques au contexte éditeur in-engine`
7. `Analyse perf / invalidation / allocations`
8. `Problèmes prioritaires`
9. `Refactors recommandés`
10. `Liste de petites tâches candidates pour un agent IA`

---

## Critères de qualité de l’analyse

Une bonne analyse doit:
- être concrète
- citer le code
- distinguer les vrais problèmes des préférences
- être exploitable directement pour du refactor
- tenir compte du contexte jeu vidéo
- éviter les recommandations trop "desktop app"
- privilégier la stabilité runtime et la simplicité de pipeline

---

## Interdictions

- Ne pas proposer de dependency property system complet "à la WPF" sans justifier son coût et son intérêt dans un moteur de jeu.
- Ne pas recommander une architecture qui explose le coût runtime pour coller à WPF.
- Ne pas faire de refactor massif pendant cette mission.
- Ne pas juger un choix uniquement parce qu’il diffère de WPF.
- Ne pas ignorer les contraintes MonoGame / draw / clipping / perf.

---

## Bonus utiles si possible

Si pertinent, ajouter dans le rapport:
- une matrice "desktop WPF style" vs "game UI framework style"
- une proposition de modèle cible raisonnable pour MGUI
- une liste de quick wins à faible risque
- une liste de deep refactors à plus long terme

---

## Définition du succès

La mission est réussie si:
- le rapport final permet de comprendre où MGUI est déjà bon
- le rapport final identifie clairement ce qui reste hybride ou fragile
- le rapport final prépare un second agent IA à créer une vraie todo list de refactor
- les conclusions sont adaptées à un framework UI de jeu vidéo moderne