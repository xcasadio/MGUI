# Input Enhanced Part 2 — Assainissement de l'architecture API input

## Verification du diagnostic

La premiere iteration a ajoute des fonctionnalites utiles, mais l'architecture n'est pas encore totalement propre pour un framework UI reutilisable.

Les points faibles principaux a corriger sont :

1. le **double-clic** est detecte globalement par temps + position, sans etre rattache a une **cible UI stable**
2. l'evenement **DoubleClicked** est en pratique un alias de `ClickCount >= 2`, pas un vrai front montant sur le deuxieme clic
3. le **repeat clavier** est configure au niveau du `KeyboardTracker` partage, puis modifie dynamiquement par `MGTextBox`
4. le handling du flux clavier (`KeyDown` -> `KeyRepeat` -> `KeyUp`) n'est pas encore pense comme une **sequence coherente**

L'objectif de cette seconde phase n'est donc pas d'ajouter encore plus de features, mais de rendre l'API **plus intelligente, plus clean et plus stable** pour les controles futurs.

> Regle : l'agent doit executer les tests pertinents apres chaque tache completee et faire **un commit separe apres chaque tache**.

---

## Resultat cible

### Souris

- un multi-clic doit etre rattache a une **meme cible logique**
- un `DoubleClicked` ne doit etre leve que sur la transition **1 -> 2**
- un triple-clic doit rester observable via `ClickCount`, sans relancer `DoubleClicked`
- l'API doit rester compatible avec les handlers actuels `ClickedInside`, `LMBClickedInside`, etc.

### Clavier

- la politique de repeat ne doit plus etre un etat global muté par un controle
- chaque controle doit pouvoir definir sa propre politique de repeat sans effet de bord sur les autres
- la sequence `KeyDown` -> `KeyRepeat*` -> `KeyUp` doit etre coherent du point de vue du handling

### Integration UI

- `MGTreeViewItem` doit double-cliquer seulement si les deux clics appartiennent bien au meme item logique
- `MGTextBox` doit continuer a supporter ses proprietes publiques existantes, mais sans reconfigurer le tracker global partage

---

## Fichiers a verifier en priorite

- `MGUI.Shared/Input/Mouse/MouseTracker.cs`
- `MGUI.Shared/Input/Mouse/MouseHandler.cs`
- `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardTracker.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardHandler.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardEventArgs.cs`
- `MGUI.Core/UI/MGTreeViewItem.cs`
- `MGUI.Core/UI/MGTextBox.cs`
- `MGUI.Tests/Focus/InputEnhancedTests.cs`
- `MGUI.Tests/Focus/FocusTests.cs`

---

## Principes d'implementation

### Principe 1 — Le tracker detecte, le handler valide la cible

Le tracker peut continuer a detecter des candidats multi-clic, mais la validation finale du double-clic doit tenir compte de la **meme cible UI**.

### Principe 2 — `DoubleClicked` est un evenement derive, pas un alias de `ClickCount >= 2`

`ClickCount` peut continuer a monter a 3, 4, etc., mais `DoubleClicked` doit etre leve une seule fois sur le deuxieme clic.

### Principe 3 — La politique de repeat est locale au consommateur

Un controle ne doit pas modifier le `KeyboardTracker` partage pour exprimer ses besoins. La configuration du repeat doit etre portee par le handler, un abonnement specialise, ou un objet de policy local.

### Principe 4 — Une touche maintenue est un flux, pas une suite d'evenements independants

Si le framework promet `KeyDown`, `KeyRepeat`, `KeyUp`, il doit idealement rendre ce flux coherent en termes de capture / handling / ownership.

---

## Revue critique du plan

Le plan est globalement bon, mais il y avait encore trois zones trop floues pour lancer l'implementation sans derive :

1. **Ne pas stocker une reference brute de handler dans le tracker souris** comme identite de cible precedente.
   Le tracker doit rester majoritairement agnostique des owners UI. La bonne direction est plutot :
   - le tracker detecte une sequence globale candidate
   - le handler valide localement si cette sequence lui appartient deja

2. **Ne pas laisser la policy de repeat en partie au tracker et en partie au controle**.
   Il faut choisir un vrai niveau de responsabilite. Pour cette phase, la bonne cible est :
   - le tracker detecte les transitions brutes de touches
   - le `KeyboardHandler` porte la policy de repeat visible par le controle

3. **Ne pas transformer la Tache 6 en systeme de capture complexe trop tot**.
   Il faut viser une coherence de flux simple et testable, sans inventer un framework de routing complet. Le bon compromis est :
   - un `KeyboardHandler` qui a consomme le `KeyDown` initial reste le proprietaire logique des repeats associes
   - le `KeyUp` correspondant reste coherent avec ce meme flux

---

## Architecture retenue

### Souris

- `MouseTracker` continue a calculer les clics et `ClickCount`
- `MouseTracker` expose des **sequences candidates** de multi-clic, mais ne decide pas seul de la cible UI finale
- `MouseHandler` ne leve `DoubleClicked*` que si le deuxieme clic appartient au **meme handler logique** que le clic precedent de la sequence
- `DoubleClicked*` est leve uniquement sur la transition vers `ClickCount == 2`
- `ClickCount` reste disponible pour des usages futurs type triple-clic, sans reemettre `DoubleClicked*`

### Clavier

- `KeyboardTracker` reste responsable des transitions brutes d'etat clavier (`pressed`, `released`, etat courant)
- la **policy de repeat publique** est deplacee au niveau `KeyboardHandler`
- `KeyboardHandler` expose une policy simple, idealement de type `KeyboardRepeatPolicy`, avec au minimum :
  - `Enabled`
  - `InitialDelay`
  - `Interval`
  - eventuellement un predicate `CanRepeat(Keys)` si necessaire
- `MGTextBox` configure uniquement **son propre handler**, jamais le tracker global partage

### Handling clavier

- un `KeyDown` consomme par un handler prioritaire devient le debut d'un **flux logique**
- les `KeyRepeat` associes sont livres au meme handler logique tant que la touche reste maintenue
- le `KeyUp` correspondant reste coherent avec ce flux
- on ne cherche pas encore a introduire un systeme complet de capture/routing global au-dela de cette coherence minimale

---

## Taches

## Tache 1 — Figer l'architecture cible de la seconde phase

**Objectif** : documenter l'API finale voulue avant de modifier les contrats publics.

**Fichiers a modifier** :
- `input-enhanced-part-2.md`
- eventuellement `README.md` si une note API concise est utile

**Actions** :
1. Decider si la validation de cible du double-clic vit :
   - dans `MouseHandler`
   - dans une notion de `ClickSequence`
   - ou dans une combinaison tracker + handler
2. Decider le contrat exact de `DoubleClicked` :
   - seulement sur `ClickCount == 2`
   - jamais sur `3+`
3. Decider ou vit la configuration du repeat clavier :
   - sur `KeyboardHandler`
   - dans une subscription specialisee
   - via un `KeyboardRepeatPolicy`
4. Decider si le handling du repeat doit reutiliser l'owner de l'appui initial ou rester libre

**Decision recommandee** :
- garder `ClickCount` dans les event args
- faire lever `DoubleClicked` uniquement sur `ClickCount == 2`
- introduire une policy de repeat locale au `KeyboardHandler`
- definir une coherence de flux clavier simple : le handler qui consomme `KeyDown` initial garde les repeats associes et le `KeyUp` correspondant

**Decision retenue pour cette phase** :
- la validation de cible du double-clic vit dans une combinaison tracker + handler
- le tracker calcule la sequence brute, le handler valide la stabilite de cible
- `DoubleClicked` n'est emis que sur la transition vers `ClickCount == 2`
- la configuration du repeat vit au niveau `KeyboardHandler`, pas du `KeyboardTracker`
- `MGTextBox` ne mutera plus jamais le tracker global pour exprimer sa policy locale
- la coherence du flux clavier sera limitee a un ownership logique simple du `KeyDown` jusqu'au `KeyUp`

**Tests** :
- aucun test lourd ici, mais verifier que le plan est coherent avec les usages existants

**Commit** : `design(input): define second-pass API cleanup plan`

---

## Tache 2 — Rattacher le multi-clic a une cible logique stable

**Objectif** : empecher qu'un double-clic soit attribue a tort a un second element voisin.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Mouse/MouseTracker.cs`
- `MGUI.Shared/Input/Mouse/MouseHandler.cs`
- eventuellement `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`

**Actions** :
1. Introduire une notion de sequence de clic qui puisse etre rattachee a une cible logique
2. Faire en sorte qu'un handler ne voie un `DoubleClicked` que si le premier clic de la sequence lui appartenait aussi
3. Ne pas casser le comportement des clicks simples existants
4. Verifier le cas de deux elements adjacents cliques rapidement

**Notes d'implementation** :
- le tracker peut rester global, mais il faut au minimum qu'une sequence garde une identite de sequence reutilisable par le handler
- eviter de stocker une reference forte d'owner UI directement dans le tracker si une solution plus decouplee est possible

**Tests** :
- deux clics rapides sur le meme handler => double-clic
- deux clics rapides sur deux handlers differents => pas de double-clic sur le second
- click simple conserve sa semantique actuelle

**Commit** : `fix(input): bind mouse multi-click sequences to a stable target`

---

## Tache 3 — Faire de `DoubleClicked` un vrai evenement de front montant

**Objectif** : ne plus lever `DoubleClicked` sur tous les clicks ou `ClickCount >= 2`.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Mouse/MouseTracker.cs`
- `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`
- `MGUI.Shared/Input/Mouse/MouseHandler.cs`

**Actions** :
1. Differencier clairement :
   - `ClickCount` comme information brute
   - `DoubleClicked` comme evenement derive
2. Lever `DoubleClicked` uniquement lors du passage a `ClickCount == 2`
3. Garder la porte ouverte a un futur `TripleClicked` si utile, sans le simuler maintenant
4. Verifier que le comportement du TreeView et des autres controles reste intuitif

**Tests** :
- click 1 => pas de `DoubleClicked`
- click 2 => `DoubleClicked`
- click 3 => pas de second `DoubleClicked`, mais `ClickCount == 3`

**Commit** : `fix(input): emit double-click only on the second click transition`

---

## Tache 4 — Nettoyer l'API publique souris autour du multi-clic

**Objectif** : rendre l'API souris plus facile a comprendre pour les consommateurs.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`
- `MGUI.Shared/Input/Mouse/MouseHandler.cs`
- eventuellement `README.md`

**Actions** :
1. Revoir les noms et la documentation pour clarifier la difference entre :
   - click simple
   - multi-clic
   - double-clic derive
2. Determiner si `IsDoubleClick` doit :
   - disparaitre
   - devenir `ClickCount == 2`
   - ou etre remplace par un nom plus explicite
3. Mettre a jour la doc XML pour reduire l'ambiguite de lecture

**Decision recommandee** :
- eviter un bool `IsDoubleClick` qui vaut aussi vrai sur `3+`
- preferer une sémantique stricte ou un nom explicite si ce bool est conserve

**Tests** :
- verifier les invariants simples des event args

**Commit** : `refactor(input): clarify mouse multi-click public API`

---

## Tache 5 — Sortir la configuration du repeat du tracker global partage

**Objectif** : empecher `MGTextBox` de muter un etat global commun pour exprimer sa policy locale.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Keyboard/KeyboardTracker.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardHandler.cs`
- `MGUI.Core/UI/MGTextBox.cs`

**Actions** :
1. Introduire une policy de repeat locale au handler ou au consommateur
2. Retirer les mutations de `KeyboardTracker.InitialRepeatDelay` et `RepeatInterval` depuis `MGTextBox`
3. Faire en sorte que `MGTextBox` continue a honorer :
   - `IsHeldKeyRepeated`
   - `InitialKeyRepeatDelay`
   - `KeyRepeatInterval`
4. Verifier qu'un autre controle pourrait avoir une autre policy sans conflit global

**Notes d'implementation** :
- si la solution la plus propre est un `KeyboardRepeatPolicy`, la garder simple : `Enabled`, `InitialDelay`, `Interval`, eventuellement predicate `CanRepeat(Keys)`
- ne pas casser les abonnements existants a `Pressed` / `KeyDown`
- le tracker ne doit plus etre reconfigure dynamiquement par `MGTextBox`

**Tests** :
- deux handlers avec des policies differentes ne s'influencent pas
- `MGTextBox` garde sa semantique actuelle

**Commit** : `refactor(input): move keyboard repeat policy out of the shared tracker`

---

## Tache 6 — Introduire une notion de flux clavier coherent

**Objectif** : rendre `KeyDown`, `KeyRepeat`, `KeyUp` plus coherents du point de vue du handling.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Keyboard/KeyboardTracker.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardHandler.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardEventArgs.cs`

**Actions** :
1. Determiner si la touche maintenue doit garder un owner logique jusqu'au `KeyUp`
2. Si oui, faire en sorte que les repeats restent associes a ce flux initial
3. Clarifier si un `KeyRepeat` doit pouvoir etre recapture par un autre handler ou non
4. S'assurer que le modele choisi est documente et testable

**Decision recommandee** :
- pour un framework UI, preferer un flux coherent: le consommateur de l'appui initial garde la sequence, sauf choix explicite contraire

**Portee volontairement limitee** :
- ne pas introduire dans cette phase un systeme general de capture clavier multi-owner
- viser un ownership logique simple, focalise sur la coherence `KeyDown` / `KeyRepeat` / `KeyUp`

**Tests** :
- un handler prioritaire qui consomme `KeyDown` reste le consommateur des repeats associes
- le `KeyUp` reste coherent avec le flux initial

**Commit** : `refactor(input): make keyboard repeat sequences handling-aware`

---

## Tache 7 — Durcir l'integration TreeView

**Objectif** : verifier que le `TreeView` ne depend plus d'effets de bord de l'infra precedente.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGTreeViewItem.cs`
- eventuellement `MGUI.Core/UI/MGTreeView.cs`

**Actions** :
1. Re-verifier la semantique click simple / double-clic / expander
2. Confirmer que deux clics rapides sur deux items differents ne produisent pas `ItemDoubleClicked`
3. Conserver le comportement produit du clic droit
4. Garder la logique de selection et de focus propre et previsible

**Tests** :
- meme item => double-clic valide
- item A puis item B rapidement => pas de double-clic sur B
- expander clique => pas de faux double-clic produit

**Commit** : `fix(treeview): align item double-click with stable target semantics`

---

## Tache 8 — Durcir l'integration TextBox

**Objectif** : s'assurer que `MGTextBox` consomme proprement la nouvelle architecture sans couplage cache.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGTextBox.cs`

**Actions** :
1. Enlever toute logique de synchronisation imperative avec le tracker global
2. Garder les helpers purs pour tester :
   - touches preservant la text entry
   - touches repetables
3. Verifier que les shortcuts type `Ctrl+C`, `Ctrl+V` ne se mettent pas a repeter involontairement
4. Verifier que la perte de focus stoppe proprement les repeats visibles au niveau controle

**Tests** :
- backspace/fleches repetent selon la policy locale
- `Ctrl+V` ne repete pas par accident
- changement de focus ne laisse pas d'etat sale

**Commit** : `fix(textbox): consume keyboard repeat via local policy only`

---

## Tache 9 — Ajouter des tests d'architecture et de non-regression

**Objectif** : verrouiller les invariants d'architecture pour ne pas revenir en arriere.

**Fichiers a modifier** :
- `MGUI.Tests/Focus/InputEnhancedTests.cs`
- eventuellement `MGUI.Tests/Focus/FocusTests.cs`

**Actions** :
1. Ajouter des tests sur l'invariant cible-stable du double-clic
2. Ajouter des tests sur la transition stricte `ClickCount == 2`
3. Ajouter des tests sur l'absence d'effet de bord entre policies de repeat locales
4. Ajouter des tests sur la coherence du flux `KeyDown` -> `KeyRepeat` -> `KeyUp`

**Tests a viser** :
- meme cible vs cible differente
- double-clic vs triple-clic
- textbox A et controle B avec policies distinctes
- handling du flux clavier stable

**Commit** : `test(input): cover second-pass API architecture invariants`

---

## Ordre recommande

```text
Tache 1  (figer l'architecture cible)
    -> Tache 2  (lier multi-clic a une cible stable)
        -> Tache 3  (double-clic seulement sur transition vers 2)
            -> Tache 4  (clarifier l'API publique souris)
                -> Tache 5  (sortir la policy de repeat du tracker global)
                    -> Tache 6  (rendre le flux clavier handling-aware)
                        -> Tache 7  (durcir TreeView)
                            -> Tache 8  (durcir TextBox)
                                -> Tache 9  (tests d'architecture)
```

---

## Strategie a privilegier

Il faut eviter les faux correctifs suivants :

- bricoler une verification de position supplementaire sans notion de cible logique
- conserver un bool `IsDoubleClick` ambigu qui vaut aussi vrai sur les triple-clics
- garder `MGTextBox` comme controle special qui pousse sa config dans le tracker global
- ajouter encore plus d'events publics sans clarifier leur semantique

La bonne approche est :

1. corriger d'abord le modele conceptuel du multi-clic
2. rendre le repeat clavier local et composable
3. aligner `TreeView` et `TextBox` sur ce modele
4. verrouiller les invariants d'architecture avec des tests purs