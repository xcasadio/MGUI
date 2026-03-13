# Taches de refonte Style/Theme pour MGUI

## Objectif

Cette liste est derivee du rapport d'audit et sert de backlog ordonne pour un agent implementeur. L'ordre privilegie des etapes verticales courtes, testables, et compatibles avec un framework UI pour jeu video.

## Principes d'execution

- Commencer par les fondations transverses avant les migrations de controles.
- Eviter les refontes globales non testees.
- Preserver la compatibilite descendante autant que possible.
- Favoriser les adaptateurs temporaires plutot que les ruptures brutales d'API.
- Ajouter des tests logiques avant les migrations les plus couplantes.

## Legende de statut

- ✅ termine
- ⬜ a faire

## Ordre de priorite

### 1. ✅ Definir les invariants de precedence des valeurs

But:
figer une table de precedence officielle entre valeur locale, style implicite, style explicite, template, theme, heritage et fallback.

Travail attendu:

- rediger le document d'architecture de precedence ;
- definir quelles categories de proprietes sont heritables ;
- definir les types d'invalidation attendus.

Critere d'acceptation:

- la precedence est explicite, stable et referencee par le code futur ;
- les proprietes layout et visuelles sont distinguees.

### 2. ✅ Introduire un modele central de valeurs resolues

But:
poser les types centraux necessaires avant toute reecriture massive.

Travail attendu:

- ajouter les types representant source, precedence et invalidation d'une valeur ;
- permettre comparaison, debug et tests ;
- prevoir un chemin de migration depuis les proprietes actuelles.

Critere d'acceptation:

- le modele compile sans casser les controles ;
- les tests peuvent exprimer la precedence attendue.

### 3. ✅ Ajouter des resources dictionaries hierarchiques

But:
faire evoluer `MGResources` d'un registre desktop vers un systeme de lookup par scope.

Travail attendu:

- introduire les scopes desktop, window, subtree et template ;
- definir les regles de lookup ;
- conserver des APIs de compatibilite pour les usages existants.

Critere d'acceptation:

- une ressource peut etre resolue avec fallback coherent ;
- les bindings sur ressources ne sont plus limites au desktop global.

### 4. ✅ Introduire `StaticResource` et `DynamicResource`

But:
separer clairement resolution a la creation et resolution reactive.

Travail attendu:

- ajouter les references de ressource statique et dynamique ;
- definir leur integration dans XAML et dans le runtime ;
- propager proprement les invalidations.

Critere d'acceptation:

- un changement de ressource dynamique peut invalider les cibles concernees ;
- une ressource statique reste resolue une seule fois.

### 5. ✅ Introduire l'invalidation theme/style runtime

But:
rendre possible un vrai theme switch sans code ad hoc.

Travail attendu:

- lier changement de theme et reevaluation des valeurs dynamiques ;
- invalider mesure, arrangement et draw seulement si necessaire ;
- definir le comportement par desktop, fenetre et sous-arbre.

Critere d'acceptation:

- un theme switch simple met a jour un ecran sans reparse complet ;
- les performances restent previsible.

### 6. ✅ Ajouter une abstraction officielle de `ControlTemplate`

But:
separer structure visuelle et logique de controle.

Travail attendu:

- introduire `ControlTemplate` distinct de `ContentTemplate` ;
- definir la construction, l'application et la resolution des template parts ;
- prevoir un chemin de compatibilite avec les delegates existants.

Critere d'acceptation:

- un controle composite peut remplacer sa structure sans reimplementer sa logique ;
- la precedence style/template est definie clairement.

### 7. ✅ Formaliser les template parts et presenters

But:
standardiser les sous-parties visuelles des controles composites.

Travail attendu:

- definir la notion de template part ;
- reutiliser `MGContentPresenter` et `MGHeaderedContentPresenter` comme primitives ;
- remplacer progressivement les delegates heterogenes par des conventions de template.

Critere d'acceptation:

- les gros controles composites ont des parts identifiees ;
- les points d'extension publics convergent.

### 8. ✅ Introduire une couche de projection des visual states

But:
faire des etats visuels une couche autonome et composable.

Travail attendu:

- definir le mapping etat -> valeurs visuelles ;
- permettre la projection au niveau template et pas seulement controle ;
- limiter le code de draw specifique aux primitives inevitables.

Critere d'acceptation:

- hover, pressed, focused, selected, disabled sont projetes sans logique dupliquee ;
- les templates peuvent reagir aux etats du controle parent.

### 9. ⬜ Migrer les controles composites prioritaires

But:
prouver la valeur de la nouvelle architecture sur les controles a plus forte dette.

Travail attendu:

- migrer d'abord `MGWindow`, `MGOverlay`, `MGContextMenuItem`, `MGListBox`, `MGListView` ;
- deplacer litteraux visuels et decisions de skin dans templates et ressources ;
- conserver les comportements existants.

Critere d'acceptation:

- ces controles deviennent significativement plus lookless ;
- leur apparence de base vient d'un package de theme / style plutot que du code du controle.

### 10. ⬜ Migrer `MGComboBox`, `MGTreeView`, `MGTabControl`

But:
stabiliser les patterns avant d'attaquer le docking.

Travail attendu:

- convertir wrappers, headers, arrows, selection graphics et presenters ;
- supprimer le maximum de skinning code en dur ;
- factoriser les patterns repetes.

Critere d'acceptation:

- les controles reutilisent les memes primitives de templating et visual states ;
- le theme change sans patchs specifiques a chaque controle.

### 11. ⬜ Migrer le sous-systeme docking

But:
traiter la zone la plus couplee une fois les primitives stabilisees.

Travail attendu:

- auditer et migrer tabs, drawers, strips, indicators et splitters ;
- externaliser icons, foregrounds et overlays ;
- conserver les contraintes de performance et lisibilite en jeu / outil.

Critere d'acceptation:

- le docking utilise les memes conventions de theme/style/template que le reste du framework ;
- le renderer ne porte pas de logique theming specifique au docking.

### 12. ⬜ Rationnaliser `MGTheme`

But:
separer tokens semantiques, styles de controle et themes built-in.

Travail attendu:

- extraire les tokens semantiques ;
- limiter `MGTheme` aux concepts durables ;
- deplacer les themes built-in de demonstration hors du coeur si necessaire.

Critere d'acceptation:

- le core UI ne depend plus d'un format de theme monolithique ;
- plusieurs packages de theme peuvent coexister proprement.

### 13. ⬜ Exposer les nouvelles notions en API publique et en XAML

But:
rendre la nouvelle architecture utilisable sans imperative code partout.

Travail attendu:

- ajouter ressources par scope, `ControlTemplate`, `DynamicResource`, `BasedOn` si retenu ;
- valider les comportements par defaut ;
- documenter les anti-patterns.

Critere d'acceptation:

- un ecran themeable peut etre decrit principalement en XAML ;
- les APIs publiques sont coherentes et peu redondantes.

### 14. ⬜ Ajouter la couverture de tests structurels et runtime

But:
stabiliser la refonte.

Travail attendu:

- tests sur precedence ;
- tests sur resource lookup hierarchique ;
- tests sur dynamic resources ;
- tests sur theme switch ;
- tests sur templates et visual states.

Critere d'acceptation:

- les regressions de precedence et de propagation sont detectables rapidement ;
- la refonte n'est pas dependante de captures visuelles fragiles.

### 15. ⬜ Ajouter samples et guide de migration

But:
valider l'adoption et documenter la transition.

Travail attendu:

- creer un sample multi-theme simple mais representatif ;
- montrer local overrides, implicit styles, templates et visual states ;
- documenter la migration depuis les APIs actuelles.

Critere d'acceptation:

- les utilisateurs peuvent migrer progressivement ;
- les bonnes pratiques sont explicites.

## Ordre d'execution resume

1. precedence des valeurs
2. modele central de valeurs
3. ressources hierarchiques
4. static/dynamic resources
5. invalidation runtime
6. control template
7. template parts
8. visual state mapping
9. controles composites prioritaires
10. combo/tree/tab
11. docking
12. rationalisation du theme
13. API publique et XAML
14. tests
15. samples et migration

## Note finale

Le succes de cette refonte ne se mesurera pas au nombre de nouvelles proprietes exposees. Il se mesurera a la capacite de MGUI a rendre un controle composite themeable, testable et remplacable visuellement sans recrire sa logique.