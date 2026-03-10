# Fix Input / Focus / Scroll — Plan de taches pour agent IA

## Verification du diagnostic

Le bug decrit dans [input-system-bug.md](input-system-bug.md) ressemble bien a une regression MGUI, pas a un bug specifique de `CasaEngine.Demos`.

Le probleme probable est la combinaison suivante :

1. un controle focusable prend le focus au `mouse press`
2. le changement de focus declenche un auto-scroll immediat
3. le bouton s'active au `mouse release`
4. l'UI bouge entre le `press` et le `release`

Le bon comportement produit est :

- navigation clavier/gamepad : auto-scroll du focus = oui
- focus obtenu par souris : auto-scroll immediat = non

Le plan ci-dessous force l'agent a corriger d'abord la regression UX principale, puis a verifier la robustesse des calculs de visibilite du `MGScrollViewer`.

> Regle : l'agent doit executer les tests pertinents apres chaque tache et faire un commit apres chaque tache completee.

---

## Resultat cible

### Cas souris

- un clic sur un bouton visible dans un `MGScrollViewer` ne doit pas deplacer le contenu entre le `press` et le `release`
- le `ReleasedInside` doit rester coherent avec la cible initiale
- le focus clavier peut etre pris par le bouton, mais sans effet de scroll immediat parasite

### Cas navigation clavier/gamepad

- le focus deplace par clavier/gamepad doit continuer a auto-scroller l'element cible pour le rendre visible
- les correctifs ne doivent pas casser les travaux deja faits sur la navigation semantique

### Cas programmatique

- un focus force par code doit avoir une politique explicite
- si le choix est de scroller en cas de focus programmatique, ce comportement doit etre stable et teste

---

## Fichiers a verifier en priorite

- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGButton.cs`
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/MGScrollViewer.cs`
- `MGUI.Tests/Focus/FocusTests.cs`
- eventuellement `CasaEngine.Demos/Demos/DemoUI/DemoInfoScreen.cs` pour validation manuelle seulement

---

## Taches

## Tache 1 — Tracer l'origine du focus

**Objectif** : separer clairement un focus obtenu par souris d'un focus obtenu par navigation clavier/gamepad ou par code.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGDesktop.cs`
- eventuellement `MGUI.Core/UI/Enums.cs`

**Actions** :
1. Introduire une notion explicite de source de focus, par exemple `Pointer`, `Keyboard`, `GamePad`, `Programmatic`
2. Faire en sorte que le focus automatique sur `LMBPressedInside` passe explicitement par la source `Pointer`
3. Faire en sorte que les chemins de navigation clavier/gamepad marquent explicitement leur source
4. Garder une valeur par defaut coherente pour les appels de focus existants afin de ne pas casser l'API publique inutilement

**Notes d'implementation** :
- eviter un patch implicite du type `if ActiveInputMode != Pointer` sans garder la notion de provenance du focus
- la source du focus doit etre disponible au moment ou `FocusedKeyboardHandler` est applique

**Tests** :
- verifier que les chemins `Pointer`, `Keyboard`, `GamePad` et `Programmatic` sont correctement distingues dans une logique pure

**Commit** : `fix(mgui): track keyboard focus source`

---

## Tache 2 — Bloquer l'auto-scroll pour le focus souris

**Objectif** : corriger la regression UX principale sans casser la navigation clavier/gamepad.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGDesktop.cs`

**Actions** :
1. Conditionner `EnsureFocusedElementVisible(...)` a la source du focus
2. Ne pas auto-scroller quand le focus vient de `Pointer`
3. Conserver l'auto-scroll pour `Keyboard` et `GamePad`
4. Definir explicitement la politique pour `Programmatic`

**Comportement attendu** :
- clic souris sur bouton dans `MGScrollViewer` : pas de scroll parasite
- navigation clavier/gamepad : scroll automatique conserve

**Tests** :
- test logique qui prouve que l'auto-scroll est autorise ou non selon la source du focus
- test de non-regression sur le comportement clavier/gamepad deja implemente

**Commit** : `fix(mgui): disable focus auto-scroll for pointer focus`

---

## Tache 3 — Durcir `MGScrollViewer.EnsureElementVisible`

**Objectif** : verifier qu'il n'y a pas un second bug de calcul de visibilite lie aux espaces de coordonnees.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGScrollViewer.cs`
- eventuellement `MGUI.Core/UI/MGElement.cs`

**Actions** :
1. Auditer l'espace de coordonnees utilise par `EnsureElementVisible(...)`
2. Verifier si `target.LayoutBounds`, `ContentViewport`, `HorizontalOffset` et `VerticalOffset` sont bien compares dans un referentiel coherent
3. Si necessaire, convertir les bornes du `target` dans l'espace du contenu scrollable avant comparaison
4. Eviter les micro-ajustements quand l'element est deja visible avec une marge raisonnable

**Notes d'implementation** :
- le but n'est pas seulement de masquer le bug souris ; il faut aussi eviter les petits scrolls faux-positifs
- preferer une methode pure testable pour le calcul des offsets cibles

**Tests** :
- cas deja visible : offset inchange
- cas partiellement visible : correction minimale
- cas hors viewport : scroll attendu
- cas horizontal et vertical

**Commit** : `fix(mgui): make scroll visibility checks coordinate-safe`

---

## Tache 4 — Verifier la coherence `press` / `release` des boutons scrollables

**Objectif** : s'assurer que la sequence d'interaction bouton reste stable dans un conteneur scrollable.

**Fichiers a modifier** :
- `MGUI.Core/UI/MGButton.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Tests/Focus/FocusTests.cs`
- eventuellement un fichier de tests UI existant si plus adapte

**Actions** :
1. Re-verifier le timing exact entre prise de focus et activation du bouton
2. Verifier qu'aucun autre chemin de focus ou de scroll ne peut encore deplacer la cible entre `PressedInside` et `ReleasedInside`
3. Si necessaire, ajouter une protection minimale pour les clics souris dans une surface scrollable, sans changer la semantique normale des boutons

**Important** :
- ne pas deplacer l'activation du bouton au `press` juste pour cacher le symptome
- la correction doit rester compatible avec les comportements existants de clic annule quand le curseur sort du bouton

**Tests** :
- scenario logique ou un focus souris n'entraine aucun changement d'offset avant le release
- scenario ou un focus clavier continue a pouvoir scroller avant activation semantique

**Commit** : `fix(mgui): preserve button click stability in scrollable lists`

---

## Tache 5 — Ajouter des tests de non-regression orientes produit

**Objectif** : verrouiller la regression pour ne plus la reintroduire lors des futurs changements de navigation/focus.

**Fichiers a modifier** :
- `MGUI.Tests/Focus/FocusTests.cs`
- eventuellement `MGUI.Tests/Modal/*`, `MGUI.Tests/KeyboardNav/*` ou autre dossier de tests plus approprie

**Actions** :
1. Ajouter un test centre sur la regle produit : `pointer focus does not auto-scroll`
2. Ajouter un test centre sur la regle complementaire : `keyboard/gamepad focus still auto-scrolls`
3. Ajouter un test centre sur la regle de restauration : `no false-positive scroll when already visible`
4. Si utile, extraire de petits helpers purs pour rendre ces tests stables et sans runtime MonoGame complexe

**Commit** : `test(mgui): cover pointer focus and scroll regressions`

---

## Tache 6 — Validation manuelle sur le cas reel CasaEngine

**Objectif** : verifier que la correction regle bien le symptome utilisateur d'origine.

**Fichiers a modifier** :
- aucun si tout est deja corrige
- sinon seulement les fichiers strictement necessaires apres reproduction manuelle

**Actions** :
1. Rejouer la reproduction decrite dans `input-system-bug.md`
2. Verifier qu'un clic direct sur `Material Demo` ne provoque plus de leger scroll au press
3. Verifier que le `mouse release` reste bien sur le bouton initial
4. Verifier rapidement qu'une navigation clavier/gamepad dans une liste scrollable continue a scroller correctement

**Notes** :
- si l'agent n'a pas acces au projet `CasaEngine.Demos`, il doit au minimum documenter que la validation manuelle reste a faire cote integrateur
- cette tache ne doit pas servir de pretexte pour modifier du code metier CasaEngine si le bug est deja corrige dans MGUI

**Commit** : `docs(mgui): record input-system regression validation`

---

## Ordre recommande

```text
Tache 1  (source explicite du focus)
    -> Tache 2  (bloquer auto-scroll sur focus souris)
        -> Tache 3  (durcir EnsureElementVisible)
            -> Tache 4  (coherence press/release)
                -> Tache 5  (tests de non-regression produit)
                    -> Tache 6  (validation manuelle CasaEngine)
```

---

## Strategie de correction a privilegier

La correction a privilegier est :

1. rendre explicite la provenance du focus
2. n'auto-scroller que pour les provenances qui le justifient vraiment
3. verifier ensuite la precision des calculs de visibilite du `MGScrollViewer`

Il faut eviter les faux correctifs suivants :

- enlever `IsFocusable = true` des boutons
- deplacer l'activation du bouton du `release` vers le `press`
- desactiver globalement l'auto-scroll du focus

Ces contournements supprimeraient le symptome, mais degraderaient l'architecture de navigation ajoutee recemment.

---

## Validation de cette execution

### Validation automatique realisee dans MGUI

- source explicite du focus ajoutee
- auto-scroll bloque pour le focus souris
- calcul de visibilite du `MGScrollViewer` durci
- tests `FocusTests` mis a jour apres chaque lot

### Validation manuelle restante

Le workspace courant ne contient pas le projet `CasaEngine.Demos`, donc la reproduction utilisateur finale n'a pas pu etre rejouee ici.

Validation integrateur encore a faire :

1. lancer `CasaEngine.Demos`
2. cliquer directement sur `Material Demo` dans `Demo Navigator`
3. verifier qu'il n'y a plus de micro-scroll au `press`
4. verifier que le `release` reste bien sur le meme bouton
5. verifier rapidement que la navigation clavier/gamepad dans la meme liste continue a scroller correctement