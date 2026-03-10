# Input Enhanced — Plan de taches pour agent IA

## Verification rapide de l'existant

Le framework dispose deja d'une base input solide, mais elle est inegale selon les peripheriques :

- **Souris** : `PressedInside`, `ReleasedInside`, `ClickedInside`, drag, hover, context menu via `RMBReleasedInside`
- **Clavier** : `Pressed`, `Released`, `Clicked`
- **Texte** : `MGTextBox` implemente deja sa propre repetition de touche (`IsHeldKeyRepeated`, `InitialKeyRepeatDelay`, `KeyRepeatInterval`)

En revanche, il manque encore une couche **generique** et **coherente** pour :

1. le **double-clic souris** au niveau infra, au minimum pour **gauche** et **droit**
2. le **key repeat** au niveau infra clavier, au lieu d'un comportement special-cased dans `MGTextBox`
3. une API plus explicite pour les usages UI classiques de type `KeyDown`, `KeyUp`, `KeyRepeat`

Attention importante :

- `MGTreeViewItem` gere deja un double-clic de maniere locale via un timestamp ad hoc (`DateTime.Now` + seuil `500ms`)
- il faut **remplacer / brancher** cette logique sur la future infra commune, pas l'ignorer ni la dupliquer

> Regle : l'agent doit executer les tests pertinents apres chaque tache completee et faire **un commit separe apres chaque tache**.

---

## Resultat cible

### Souris

- supporter les evenements de **double-clic gauche** et **double-clic droit**
- centraliser la detection dans `MouseTracker` au lieu de laisser des controles faire leur propre horodatage local
- rendre les seuils configurables comme pour le click simple
- exposer une API reutilisable par `MGElement`, `MGTreeView`, `MGListView`, `MGListBox`, etc.

### Clavier

- distinguer clairement :
  - `KeyDown` : touche enfoncee ce tick
  - `KeyUp` : touche relachee ce tick
  - `KeyRepeat` / `PressedRepeat` : repetition tant que la touche reste maintenue
- conserver la compatibilite avec `Pressed` / `Released` / `Clicked` autant que possible
- factoriser la repetition clavier pour que `MGTextBox` ne soit plus le seul controle a porter cette logique

### Integration UI

- le `TreeView` doit continuer a lever son evenement produit `ItemDoubleClicked`
- son implementation doit s'appuyer sur l'infrastructure partagee, pas sur une heuristique locale codee en dur
- les controles existants ne doivent pas changer de semantique involontairement sur le click simple, le focus, le drag ou le context menu

---

## Fichiers a verifier en priorite

- `MGUI.Shared/Input/Mouse/MouseTracker.cs`
- `MGUI.Shared/Input/Mouse/MouseHandler.cs`
- `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardTracker.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardHandler.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardEventArgs.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGTreeView.cs`
- `MGUI.Core/UI/MGTreeViewItem.cs`
- `MGUI.Core/UI/MGTextBox.cs`
- dossiers de tests `MGUI.Tests/Keyboard/`, `MGUI.Tests/Focus/`, `MGUI.Tests/KeyboardNav/`, ou nouveau dossier plus adapte si necessaire

---

## Principes d'implementation

### API retenue

- clavier : `Pressed` / `Released` / `Clicked` restent supportes, avec aliases `KeyDown` / `KeyUp` et un nouvel event `KeyRepeat`
- souris : conservation de `Clicked*` avec ajout de `ClickCount`, plus evenements specialises `*DoubleClicked*`

### Principe 1 — L'infra detecte, les controles consomment

La detection du double-clic et du key repeat doit vivre dans `MGUI.Shared.Input`, pas dans les controles individuels.

### Principe 2 — Compatibilite API

Eviter de casser brutalement les abonnes existants a `Pressed`, `Released`, `Clicked`. Si une nouvelle nomenclature `KeyDown` / `KeyUp` / `KeyRepeat` est introduite, garder des alias ou une transition douce.

### Principe 3 — Seuils configurables

Le double-clic doit reposer sur des seuils configurables, coherents avec les seuils deja presents :

- temps maximal entre les clics
- tolerance de position entre clic 1 et clic 2

### Principe 4 — Pas de duplication TreeView

`MGTreeViewItem` ne doit pas conserver sa logique locale `DateTime.Now` une fois l'infra partagee disponible.

---

## Taches

## Tache 1 — Auditer et figer l'API cible clavier / souris

**Objectif** : definir proprement l'API publique avant d'implementer les comportements.

**Fichiers a modifier** :
- `input-enhanced.md` si l'agent veut completer ses notes
- eventuellement `README.md` si une courte note d'API est necessaire a la fin de la tache

**Actions** :
1. Lister les evenements actuels souris et clavier exposes publiquement
2. Decider la nomenclature cible
3. Decider si `Pressed` / `Released` restent l'API primaire ou deviennent des alias de `KeyDown` / `KeyUp`
4. Decider le nom du repeat clavier (`Repeated`, `Repeat`, `PressedRepeat`, etc.)
5. Decider si le double-clic est expose comme :
   - un nouvel evenement `DoubleClicked`
   - des evenements specialises `LMBDoubleClickedInside`, `RMBDoubleClickedInside`
   - et/ou un `ClickCount` dans les event args

**Decision recommandee** :
- garder `Pressed` / `Released` / `Clicked` pour compatibilite
- ajouter des alias explicites `KeyDown` et `KeyUp`
- ajouter un vrai evenement de repetition distinct, plutot que redefinir `Pressed`
- pour la souris, ajouter un `ClickCount` sur l'event args + des evenements double-clic specialises pour le confort d'usage

**Tests** :
- aucun gros test runtime ici, mais verifier que le plan d'API est coherent avec les usages existants

**Commit** : `design(input): define enhanced mouse and keyboard event API`

---

## Tache 2 — Ajouter le double-clic generique dans MouseTracker

**Objectif** : detecter le double-clic dans l'infrastructure partagee.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Mouse/MouseTracker.cs`
- `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`

**Actions** :
1. Ajouter un suivi du dernier click par bouton souris
2. Determiner si un nouveau click constitue un double-clic selon :
   - le meme bouton
   - un delai maximal configurable
   - une tolerance spatiale configurable
3. Exposer soit `ClickCount`, soit une famille d'events `CurrentButtonDoubleClickedEvents`, soit les deux
4. Faire fonctionner la logique pour **Left** et **Right** au minimum
5. Verifier que le drag ou un mouvement excessif n'est pas mal interprete comme double-clic

**Notes d'implementation** :
- reutiliser la notion existante de `ClickTimeThreshold` plutot que coder un seuil en dur ailleurs
- si un seuil specifique double-clic est necessaire, l'ajouter explicitement au tracker
- ne pas lier le double-clic au seul LMB si le but produit inclut explicitement RMB

**Tests** :
- deux clicks rapides meme bouton => double-clic
- clicks trop espaces => pas de double-clic
- clicks assez proches dans le temps mais trop eloignes spatialement => pas de double-clic
- LMB et RMB verifies separement

**Commit** : `feat(input): add generic mouse double-click tracking`

---

## Tache 3 — Exposer les evenements de double-clic dans MouseHandler

**Objectif** : rendre la nouvelle detection utilisable par les controles.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Mouse/MouseHandler.cs`
- eventuellement `MGUI.Shared/Input/Mouse/MouseEventArgs.cs`

**Actions** :
1. Ajouter les evenements publics necessaires, par exemple :
   - `DoubleClickedInside`
   - `LMBDoubleClickedInside`
   - `RMBDoubleClickedInside`
   - `DoubleClickedOutside` si juge utile
2. Faire respecter les memes regles que les autres evenements :
   - `InvokeEvenIfHandled`
   - `AlwaysHandlesEvents`
   - priorites de handler
3. Verifier l'ordre d'invocation par rapport a `Released` et `Clicked`
4. Documenter clairement si un double-clic implique aussi deux clicks simples, ce qui est le comportement le plus classique

**Tests** :
- handler abonne au double-clic recu correctement
- respect du handling quand un handler prioritaire consomme l'event

**Commit** : `feat(input): expose mouse double-click events on handlers`

---

## Tache 4 — Migrer TreeView sur l'infrastructure partagee

**Objectif** : supprimer la logique locale fragile du TreeView et conserver son API produit.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGTreeViewItem.cs`
- eventuellement `MGUI.Core/UI/MGTreeView.cs`

**Actions** :
1. Remplacer la detection locale basee sur `DateTime.Now` et `500ms`
2. Brancher `MGTreeViewItem` sur le nouvel evenement de double-clic partage
3. Conserver le comportement produit :
   - selection de l'item
   - focus sur le `TreeView`
   - `ItemDoubleClicked`
   - `ItemRightClicked`
4. Verifier la semantique d'expansion : aujourd'hui l'item s'expand/collapse au click simple s'il a des enfants ; il faut s'assurer que le nouveau branchement ne declenche pas une double bascule indue

**Attention** :
- ne pas perdre le support du clic droit existant
- ne pas garder deux mecanismes de double-clic en parallele

**Tests** :
- un item avec enfants garde son comportement actuel sur click simple
- le double-clic leve `ItemDoubleClicked`
- le clic droit continue a lever `ItemRightClicked`

**Commit** : `refactor(treeview): use shared double-click infrastructure`

---

## Tache 5 — Ajouter KeyRepeat generique dans KeyboardTracker

**Objectif** : deplacer la repetition clavier dans l'infrastructure partagee.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Keyboard/KeyboardTracker.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardEventArgs.cs`

**Actions** :
1. Ajouter le suivi des touches maintenues avec timestamps de premier appui et de derniere repetition
2. Ajouter des seuils configurables au tracker, analogues a ceux deja utilises par `MGTextBox` :
   - `InitialRepeatDelay`
   - `RepeatInterval`
3. Produire des evenements de repetition tant que la touche reste maintenue
4. Exclure ou filtrer proprement certains cas si necessaire :
   - modificateurs purs (`Shift`, `Ctrl`, `Alt`)
   - combinaisons speciales si elles ne doivent pas repeter
5. Conserver `Pressed` comme evenement de transition initiale et non comme repetition implicite

**Notes d'implementation** :
- l'objectif est d'offrir un service generique a tous les controles, pas seulement au texte
- si certaines touches ne doivent jamais repeter, la regle doit etre explicite et testee

**Tests** :
- une touche tenue genere `KeyDown` puis des repeats
- le relachement stoppe immediatement les repeats
- le repeat respecte bien le delai initial puis l'intervalle

**Commit** : `feat(input): add generic keyboard repeat tracking`

---

## Tache 6 — Exposer KeyDown / KeyUp / KeyRepeat dans KeyboardHandler

**Objectif** : donner une API plus naturelle aux controles sans casser l'existant.

**Fichiers a modifier** :
- `MGUI.Shared/Input/Keyboard/KeyboardHandler.cs`
- `MGUI.Shared/Input/Keyboard/KeyboardEventArgs.cs`

**Actions** :
1. Exposer des evenements explicites `KeyDown` et `KeyUp` si l'API actuelle reste `Pressed` / `Released`
2. Ajouter un evenement `KeyRepeat` ou equivalent
3. Faire en sorte que `Pressed` et `Released` restent coherents avec les nouveaux alias
4. Garantir que `Owner.CanReceiveKeyboardInput()` et `Owner.HasKeyboardFocus()` continuent a filtrer correctement
5. Documenter clairement l'ordre d'invocation typique : `KeyDown` -> zero ou plusieurs `KeyRepeat` -> `KeyUp` -> eventuel `Clicked`

**Tests** :
- un controle peut s'abonner aux nouveaux events sans regression sur les anciens
- `Clicked` reste un click court et n'est pas remplace par `KeyRepeat`

**Commit** : `feat(input): expose key down up and repeat events`

---

## Tache 7 — Brancher MGTextBox sur l'infrastructure clavier partagee

**Objectif** : supprimer la repetition speciale de `MGTextBox` ou la reduire a une simple configuration.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGTextBox.cs`
- eventuellement `MGUI.Core/UI/XAML/Controls.cs`

**Actions** :
1. Refaire `MGTextBox` pour consommer le repeat du `KeyboardTracker` / `KeyboardHandler`
2. Preserver les proprietes publiques existantes si elles sont deja utilisees par les utilisateurs (`IsHeldKeyRepeated`, `InitialKeyRepeatDelay`, `KeyRepeatInterval`)
3. Si possible, faire de ces proprietes un simple parametrage de l'infrastructure partagee au lieu d'une boucle maison dans `Update()`
4. Verifier les raccourcis texte deja pris en charge (`Ctrl+C`, `Ctrl+V`, fleches, etc.)

**Important** :
- ne pas degrader la saisie texte pour les cas existants
- ne pas perdre les exceptions actuelles sur certaines touches speciales si elles etaient volontaires

**Tests** :
- saisie de texte simple
- backspace maintenu
- fleches maintenues dans le texte
- raccourcis non repetes ou repetes selon la regle choisie

**Commit** : `refactor(textbox): use shared keyboard repeat pipeline`

---

## Tache 8 — Ajouter des wrappers haut niveau sur MGElement si utile

**Objectif** : faciliter la consommation de la nouvelle infra par les controles MGUI.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGElement.cs`

**Actions** :
1. Evaluer s'il faut exposer des helpers ou evenements plus haut niveau sur `MGElement`
2. Ajouter seulement ce qui apporte une vraie valeur ergonomique aux controles du framework
3. Eviter de dupliquer toute l'API brute de `MouseHandler` et `KeyboardHandler` sans besoin clair

**Candidats raisonnables** :
- helpers `IsRMBPressed`, `IsMMBPressed` si manquants
- wrappers pratiques pour double-clic sur elements courants
- acces a un `ClickCount` recent si cela simplifie les controles de liste

**Tests** :
- verifier qu'un controle simple peut consommer la nouvelle API sans contourner les handlers bas niveau

**Commit** : `feat(input): add high-level element helpers for enhanced input`

---

## Tache 9 — Couvrir les regressions avec des tests produits

**Objectif** : verrouiller la nouvelle infra avant de l'etendre a d'autres controles.

**Fichiers a modifier** :
- fichiers sous `MGUI.Tests/`

**Actions** :
1. Ajouter des tests unitaires sur la detection de double-clic gauche et droit
2. Ajouter des tests unitaires sur `KeyDown`, `KeyUp`, `KeyRepeat`
3. Ajouter un test de non-regression pour `MGTreeViewItem`
4. Ajouter un test de non-regression pour `MGTextBox`
5. Verifier que les clicks simples existants continuent a fonctionner

**Tests a viser** :
- sequence click, click, double-click
- sequence key down, repeats, key up
- absence de repeat apres key up
- compatibilite avec handlers prioritaires et `Handled`

**Commit** : `test(input): cover double-click and keyboard repeat behavior`

---

## Strategie a privilegier

Il faut eviter les faux correctifs suivants :

- ajouter un double-clic uniquement dans `MGTreeViewItem`
- reutiliser `Pressed` pour simuler le repeat clavier sans distinguer appui initial et repetition
- disperser des seuils en dur dans plusieurs controles
- casser `Clicked`, `ContextMenuRequested`, le drag ou la navigation clavier existante

La bonne approche est :

1. mettre l'intelligence de detection dans `MGUI.Shared.Input`
2. exposer une API propre dans les handlers
3. migrer les controles speciaux comme `TreeView` et `TextBox`
4. verrouiller avec des tests