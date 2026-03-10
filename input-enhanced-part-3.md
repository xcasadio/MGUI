# Input Enhanced Part 3 — Durcissement du modele temporel et de l'identite input

## Pourquoi une phase 3

La phase 2 a nettement assaini l'API input, mais elle a surtout corrige la **surface comportementale** et les contrats visibles.

Il reste maintenant un noyau de dette plus structurelle, moins visible, mais important si MGUI veut une API input vraiment **clean, testable, deterministic et reusable**.

Le but de cette phase 3 n'est pas d'ajouter des features produit. Le but est de durcir le **modele interne** pour que les futures evolutions ne reposent plus sur des heuristiques fragiles.

> Regle : l'agent doit executer les tests pertinents apres chaque tache completee et faire **un commit separe apres chaque tache**.

---

## Diagnostic actuel

### 1. Le temps input repose encore sur `DateTime.Now`

Les event args souris et clavier utilisent encore l'horloge murale (`DateTime.Now`) pour `PressedAt`, `ReleasedAt`, `StartedAt`, `Timestamp`, etc.

Ce choix pose plusieurs problemes :

- les tests doivent contourner l'horloge reelle au lieu de raisonner sur `UpdateBaseArgs.TotalElapsed`
- deux evenements d'un meme tick ne sont pas modeles par un temps logique unique
- replay, simulation, pause, ralenti, capture deterministe ou tests de regression avancés deviennent plus fragiles

### 2. L'ownership clavier est encore indexe par `Owner`, pas par handler/flux explicite

La phase 2 a introduit une coherence de flux utile, mais elle s'appuie encore sur `HandledBy == Owner` pour savoir qui possede la sequence.

Cela reste imparfait car :

- un meme owner pourrait theoretquement porter plusieurs handlers ou plusieurs roles
- l'identite du **flux** n'est pas explicitement modelisee
- la notion de "proprietaire du stream" reste indirecte et dispersee dans les conditions d'invocation

### 3. La cible souris stable reste deduite geometriquement au moment du second clic

La phase 2 a corrige le faux double-clic sur element voisin, mais le modele reste encore une combinaison :

- sequence candidate globale dans le tracker
- validation locale via la geometrie du handler

Ce n'est pas encore une vraie identite logique de cible. Si la geometrie bouge entre les clics, si des handlers se recouvrent, ou si on veut un jour supporter des cas plus riches, la logique reste heuristique.

### 4. Les metadonnees d'input melangent encore semantique et routing

Les event args portent deja beaucoup d'information utile, mais certaines decisions de routing et de stream ownership sont encore diffusees dans les handlers au lieu d'etre modelisees par des objets ou ids explicites.

---

## Resultat cible

### Temps

- tous les evenements input utilisent un **temps logique issu de l'update courant**
- les durees (`HeldDuration`, multi-click, repeat) ne dependent plus de l'horloge murale
- les tests peuvent piloter l'input uniquement via `UpdateBaseArgs`

### Souris

- une sequence multi-clic est rattachee a une **identite de cible explicite**, pas seulement a une validation geometrique tardive
- `ClickCount` reste une information brute
- `DoubleClicked` reste un evenement derive strictement borne au second clic

### Clavier

- un flux `KeyDown -> KeyRepeat* -> KeyUp` a une **identite de stream explicite**
- le stream owner n'est plus deduit implicitement via `HandledBy == Owner`
- la policy de repeat reste locale au handler, mais le routing du stream devient plus lisible et plus robuste

### Tests

- les invariants d'architecture sont testes sans reposer sur le temps reel
- les cas limites de changement de focus, de cibles voisines, et de sequences distinctes sont verrouilles

---

## Principes d'implementation

### Principe 1 — Le temps input doit venir du moteur, pas de l'horloge murale

Le framework sait deja quel est le temps logique de l'update via `UpdateBaseArgs.TotalElapsed`. C'est cette valeur qui doit servir de reference pour les timestamps input.

### Principe 2 — Un flux doit avoir une identite explicite

Un stream clavier ou une sequence multi-clic ne devrait pas etre deduit uniquement par la combinaison de quelques champs. Il faut une vraie identite de stream/sequence pour simplifier le raisonnement et les tests.

### Principe 3 — Le routing ne doit pas etre cache dans les comparaisons d'owner

Comparer `HandledBy == Owner` fonctionne pour des cas simples, mais ne constitue pas un modele suffisamment propre pour une infra input de framework.

### Principe 4 — Les heuristiques geometriques doivent etre remplacees par des tokens logiques quand la semantique le demande

La geometrie peut aider a detecter un candidat, mais l'attribution definitive d'un stream ou d'une sequence a une cible devrait idealement reposer sur une identite logique stable.

---

## Revue critique du plan

Le plan est bon, mais trois points de design doivent etre figes avant implementation :

1. **Ne pas introduire un gros framework de timestamps**.
   La bonne cible pour cette phase est simple : utiliser `TimeSpan` logique issu de `UpdateBaseArgs.TotalElapsed` comme source unique de verite.

2. **Ne pas rendre l'identite de stream dependante d'un type UI concret**.
   Les ids de stream clavier et les ids de sequence souris doivent rester techniques et decouples des controles.

3. **Ne pas casser `HandledByEventArgs<T>` dans cette phase**.
   Il faut cesser de s'en servir comme mecanisme interne principal de routing, mais sans detruire sa semantique publique de consommation.

---

## Architecture retenue

### Temps logique

- la source unique de temps input est `UpdateBaseArgs.TotalElapsed`
- les timestamps d'evenements input sont representes en `TimeSpan`
- les event args conservent des proprietes publiques lisibles, mais elles deviennent logiques plutot que murales

### Sequence souris

- chaque clic appartient a une petite entite logique de type `MouseClickSequence` ou equivalent
- cette sequence porte au minimum :
  - un id stable
  - le bouton concerne
  - la position et le temps logiques de reference
  - un token de cible logique validee quand cette validation existe
- le handler peut valider/adopter la sequence, mais la sequence elle-meme ne depend pas d'une reference forte vers un controle UI

### Stream clavier

- chaque touche maintenue appartient a un `KeyboardInputStream` ou equivalent
- le stream porte au minimum :
  - un id stable
  - la touche concernee
  - le temps logique de debut
  - un owner technique de stream distinct de `HandledBy`
- `KeyRepeat` et `KeyUp` se rattachent explicitement a ce stream

### Separation ownership / handling public

- `HandledBy` reste une information publique de consommation d'evenement
- le routing interne de stream/sequence n'utilise plus `HandledBy == Owner` comme mecanisme principal
- les metadonnees de stream et sequence sont portees par des champs/objets dedies

### Portee volontairement limitee

- ne pas introduire dans cette phase de systeme global de capture clavier multi-owner
- ne pas lancer de refonte generale du routing souris/clavier hors des sequences et streams deja concernes
- ne pas alourdir les APIs publiques si une metadonnee interne ou un id suffit

---

## Fichiers a verifier en priorite

- `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`
- `MGUI.Shared/Input/Mouse/MouseTracker.cs`
- `MGUI.Shared/Input/Mouse/MouseHandler.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardEventArgs.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardTracker.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardHandler.cs`
- `MGUI.Shared/Input/InputTracker.cs`
- `MGUI.Core/UI/MGTreeViewItem.cs`
- `MGUI.Core/UI/MGTextBox.cs`
- `MGUI.Tests/Focus/InputEnhancedTests.cs`

---

## Taches

## Tache 1 — Figer le modele cible de la phase 3

**Objectif** : documenter clairement les decisions d'architecture avant d'ouvrir les contrats internes.

**Fichiers a modifier** :
- `input-enhanced-part-3.md`
- eventuellement `README.md` si une courte note d'architecture input est utile

**Actions** :
1. Decider si le temps logique est stocke comme `TimeSpan` brut, ou via un petit objet de timestamp commun
2. Decider si les streams clavier et sequences souris recoivent un identifiant numerique, un token objet, ou un petit record dedie
3. Decider ou vit l'identite de cible souris stable :
   - dans le handler
   - dans les event args
   - ou dans un objet `ClickSequence`
4. Decider comment separer proprement :
   - metadonnees d'evenement
   - stream ownership
   - handling public

**Decision recommandee** :
- introduire une base temporelle logique commune issue de `UpdateBaseArgs.TotalElapsed`
- introduire des ids/tokens explicites pour les flux clavier et sequences multi-clic
- garder le routing public compatible, mais deplacer l'identite technique vers des metadonnees internes claires

**Decision retenue pour cette phase** :
- le temps logique input est represente en `TimeSpan`
- les sequences souris seront modelisees par un petit objet `MouseClickSequence`
- les streams clavier seront modelises par un petit objet `KeyboardInputStream`
- `HandledByEventArgs<T>` reste public et compatible, mais cesse d'etre le support principal de l'ownership interne
- aucune reference forte a un controle UI ne sera stockee comme identite technique primaire de sequence/stream

**Tests** :
- pas de gros tests ici, mais verifier que le plan est coherent et incremental

**Commit** : `design(input): define third-pass deterministic input architecture`

---

## Tache 2 — Remplacer `DateTime.Now` par un temps logique d'update

**Objectif** : rendre les evenements input deterministes et entierement pilotables par le moteur d'update.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardEventArgs.cs`
- `MGUI.Shared/Input/Mouse/MouseTracker.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardTracker.cs`
- eventuellement `MGUI.Shared/Input/InputTracker.cs`

**Actions** :
1. Introduire une notion de timestamp logique input issue de `UpdateBaseArgs.TotalElapsed`
2. Faire porter aux event args leurs timestamps logiques au lieu de `DateTime.Now`
3. Recalculer `HeldDuration`, multi-click et repeat uniquement a partir de cette base logique
4. Verifier que les APIs publiques ne deviennent pas ambigues si les types changent

**Notes d'implementation** :
- si changer tous les types publics de `DateTime` a `TimeSpan` est trop brutal, introduire une couche de compatibilite ou un metadata object
- l'objectif est de supprimer la dependance fonctionnelle a l'horloge murale, pas seulement de la cacher

**Tests** :
- maintenir les tests existants
- ajouter au moins un test montrant qu'un intervalle de multi-clic depend de `TotalElapsed`, pas du temps reel

**Commit** : `refactor(input): switch input event timing to logical update time`

---

## Tache 3 — Introduire une vraie identite de sequence multi-clic

**Objectif** : remplacer la sequence souris "candidate + verification geometrique" par un modele plus explicite.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Mouse/MouseTracker.cs`
- `MGUI.Shared/Input/Mouse/MouseHandler.cs`
- `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`

**Actions** :
1. Introduire un petit objet ou record `MouseClickSequence` ou equivalent
2. Faire porter a chaque clic :
   - une identite de sequence
   - une notion de cible validee si applicable
3. Eviter que la validite du double-clic repose uniquement sur le fait que le handler contient encore geometriquement la position du clic precedent
4. Garder la compatibilite avec `ClickCount` et `DoubleClicked*`

**Decision recommandee** :
- le tracker detecte une sequence candidate
- le handler peut l'adopter/valider explicitement
- le double-clic ne depend plus d'une simple re-evaluation de bounds sur le clic precedent

**Tests** :
- sequence stable sur meme cible logique
- sequence invalidee si la cible change
- deux sequences distinctes ont des ids/tokens distincts

**Commit** : `refactor(input): introduce explicit mouse click sequence identity`

---

## Tache 4 — Introduire une vraie identite de stream clavier

**Objectif** : sortir du modele implicite base sur `HandledBy == Owner`.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Keyboard/KeyboardHandler.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardEventArgs.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardTracker.cs`

**Actions** :
1. Introduire une notion de `KeyboardInputStream` ou un stream id par touche maintenue
2. Faire en sorte que `KeyRepeat` et `KeyUp` se rattachent explicitement au stream cree par le `KeyDown`
3. Decoupler la notion de stream owner de la simple comparaison `HandledBy == Owner`
4. Garder un comportement identique pour les consommateurs existants

**Notes d'implementation** :
- le but n'est pas de lancer un grand systeme de capture globale
- il faut juste rendre explicite ce qui est aujourd'hui implicite et fragile

**Tests** :
- un stream handled reste sur le meme handler logique
- un stream non handled peut suivre le focus
- deux handlers avec le meme owner conceptuel ne se marchent pas dessus si ce cas existe

**Commit** : `refactor(input): introduce explicit keyboard stream identity`

---

## Tache 5 — Separer les metadonnees de stream du handling public

**Objectif** : clarifier ce qui releve de l'evenement lui-meme, du routing interne, et du `HandledBy` visible publiquement.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Keyboard/KeyboardEventArgs.cs`
- `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`
- eventuellement `MGUI.Shared/Input/InputTracker.cs`

**Actions** :
1. Revoir quelles metadonnees doivent etre exposees publiquement
2. Eviter que `HandledBy` serve a la fois :
   - d'etat public de consommation
   - d'identite technique de routing interne
3. Introduire si besoin un metadata object interne ou des champs clairement nommes pour le stream owner / sequence owner
4. Mettre a jour la doc XML pour rendre le contrat lisible

**Tests** :
- verifier que les invariants de routing restent vrais
- verifier que les APIs publiques restent comprehensibles

**Commit** : `refactor(input): separate stream metadata from public handling state`

---

## Tache 6 — Durcir TreeView contre les changements de geometrie inter-clic

**Objectif** : verifier que le produit TreeView reste correct meme si la geometrie bouge entre les clics.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGTreeViewItem.cs`
- `MGUI.Tests/Focus/InputEnhancedTests.cs`

**Actions** :
1. Verifier le comportement si un item change de bounds entre clic 1 et clic 2
2. Verifier le comportement si la structure visible du tree change entre les deux clics
3. Garder les semantics actuelles de selection/focus/expander

**Tests** :
- meme item logique avec geometrie modifiee => comportement defini et stable
- cible logique differente => pas de `ItemDoubleClicked`

**Commit** : `fix(treeview): harden double-click behavior against layout shifts`

---

## Tache 7 — Durcir TextBox contre les streams orphelins et les transitions de focus

**Objectif** : s'assurer que TextBox consomme proprement la future infra de stream explicite.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGTextBox.cs`
- `MGUI.Tests/Focus/InputEnhancedTests.cs`

**Actions** :
1. Verifier que la perte de focus coupe proprement tout effet visible de repeat
2. Verifier que les raccourcis `Ctrl+*` ne sont jamais accidentellement promus en stream texte repetitif
3. Verifier que le caret, la selection et l'edition restent stables pendant les changements de focus rapides

**Tests** :
- perte de focus pendant repetition
- changement de focus puis relache clavier
- raccourcis `Ctrl+C`, `Ctrl+V`, `Ctrl+X`, `Ctrl+A`

**Commit** : `fix(textbox): harden text entry against orphaned keyboard streams`

---

## Tache 8 — Ajouter des tests deterministes d'architecture input

**Objectif** : verrouiller les invariants sans dependre du temps reel ni d'heuristiques cachees.

**Fichiers a modifier** :
- `MGUI.Tests/Focus/InputEnhancedTests.cs`
- eventuellement nouveau fichier de tests dedie si la taille devient trop grande

**Actions** :
1. Ajouter des tests sur les timestamps logiques
2. Ajouter des tests sur les ids/tokens de streams clavier et sequences souris
3. Ajouter des tests sur la separation entre handling public et ownership interne
4. Verifier que les anciennes regressions couvertes par la phase 2 restent vertes

**Tests a viser** :
- multi-clic sans horloge reelle
- repeat sans horloge reelle
- stream handled vs non handled
- sequence souris stable vs sequence invalidee

**Commit** : `test(input): cover deterministic input architecture invariants`

---

## Ordre recommande

```text
Tache 1  (figer le modele cible)
    -> Tache 2  (temps logique unique)
        -> Tache 3  (identite de sequence souris)
            -> Tache 4  (identite de stream clavier)
                -> Tache 5  (separer metadata et handling public)
                    -> Tache 6  (durcir TreeView)
                        -> Tache 7  (durcir TextBox)
                            -> Tache 8  (tests deterministes finaux)
```

---

## Strategie a privilegier

Il faut eviter les demi-correctifs suivants :

- conserver `DateTime.Now` tout en ajoutant juste quelques tests plus tolerants
- ajouter encore plus de conditions `HandledBy == Owner` au lieu d'un vrai modele de stream
- durcir `TreeView` avec encore plus de verification geometrique locale sans identite de cible claire
- multiplier les bools publics sans clarifier le contrat interne

La bonne approche est :

1. rendre le temps input deterministe
2. rendre l'identite des sequences/streams explicite
3. decoupler ownership interne et handling public
4. verrouiller le tout avec des tests qui ne dependent pas de l'horloge murale