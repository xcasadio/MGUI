# Conception : activation de fenetre et points d'extension reels

## Statut

Document de conception issu de la tache 8 de `Docs/Tasks/input-tasks.md`. **Ne pas
implementer sans validation prealable du decoupage** (section 5) : exigence
explicite du texte de la tache.

## 1. Objectif et perimetre

Resorbe trois limites documentees dans `Docs/input-architecture.md` ("Limites
connues" / "Fenetres superposees").

### 1.a Pas d'activation de fenetre au clic

`MGDesktop.BringToFront(MGWindow)` (`MGDesktop.cs:862`) et
`MGWindow.BringToFront(MGWindow)` pour les fenetres imbriquees (`MGWindow.cs:563`)
existent, mais le seul cablage automatique clic -> premier plan du framework
(`MGUI.Core`) est `MGFloatingDockWindow.cs:100` (`MouseHandler.LMBPressedInside += (_, _) =>
BringToFront();`, forwarding vers `MGWindow.BringToFront` a `:148-150`). Une
recherche `ActiveWindow` sur `MGUI.Core` ne retourne rien : le concept n'existe
pas. Les applications recablent le geste a la main quand elles en ont
besoin (`MGUI.MiniGame/MiniGame.cs:1331,1475,1494,1526`,
`MGUI.Samples/Features/FocusInputReview.xaml.cs:166,180`,
`MGUI.Samples/Features/IBorderBrush.xaml.cs:89`), ce qui confirme le besoin
plutot que de l infirmer. Symptome : hors module docking, cliquer une fenetre en arriere-plan ne la
ramene pas au premier plan ; focus clavier et z-order peuvent diverger
silencieusement.

### 1.b Couplage au type concret `MGTextBox`

8 sites verifies, testant `is MGTextBox` ou appelant un membre `internal` :
`MGDesktop.cs:545` (`ResolveSemanticInputMode`), `:558`
(`ShouldCaptureGameplayInput`), `:1366` (meme test, chemin `Update` brut),
`:953`/`:960` (setter de `FocusedKeyboardHandler`, abonnement/desabonnement a
`MGTextBox.ReadonlyChanged`), `:985` (`TextBox_ReadonlyChanged`),
`UIFocusNavigationService.cs:216` (`TryDispatchNavigationAction`, flag
`isTextEntryFocused`) et `:454` (`ShouldPreserveTextEntryKey`, ajoute par la
tache 3, appelle `MGTextBox.ShouldPreserveTextEntryKey`, `internal`,
`MGTextBox.cs:69`). Le nombre de sites a augmente depuis la redaction initiale
de `Docs/input-architecture.md` (tache 3 en a ajoute un). Symptome : un
controle d'edition tiers ne beneficie d'aucune protection ci-dessus — gameplay
non bloque pendant la saisie, touches de navigation internes volees, pas de
nettoyage de focus a la readonly.

### 1.c `MGWindow.WindowKeyboardHandler` inerte

Declare `public` a `MGWindow.cs:874`, instancie a `:1352`
(`InputTracker.Keyboard.CreateHandler(this, null)`), pompe a `:1598`
(`WindowKeyboardHandler.ManualUpdate()`). Aucun abonnement nulle part dans le
depot. Preuve qu'il ne peut jamais rien recevoir :
`KeyboardHandler.InvokeQueuedEvents` (`KeyboardHandler.cs:166`) calcule
`hasKeyboardFocus = Owner.HasKeyboardFocus()`, requis par `canDeliverPressed`
(`:172`). Pour un `MGWindow`, `IKeyboardHandlerHost.HasKeyboardFocus()`
(`MGElement.cs:1621`) vaut `GetDesktop().FocusedKeyboardHandler == this` — vrai
seulement si la fenetre **elle-meme**, pas un de ses enfants, est l'element
focus. Rien n'appelle jamais `Focus()` sur un `MGWindow`, qui ne definit pas non
plus `Focusable = true` : la condition n'est donc jamais remplie. Symptome : un
raccourci "au niveau de la fenetre" (Echap ferme, F1 aide contextuelle) ne peut
pas s'appuyer sur cette API malgre son nom — elle ne se declenche jamais.

## 2. Etat actuel

**Activation de fenetre** : `BringToFront` deplace la fenetre en fin de
`Windows` (`List<MGWindow>`) ; l'ordre d'update/draw
(`Windows.Reverse().OrderByDescending(IsTopmost)`) traite la fin de liste en
premier, donc ce mouvement fait passer la fenetre au sommet du z-order sauf
si une autre fenetre a `IsTopmost = true` (`MGWindow.cs:1080`).
`MGFloatingDockWindow.cs:100` est le seul cablage clic existant, local au
module docking. Aucune notion de fenetre "active" (propriete ou evenement) ;
le focus clavier est independant du z-order.

**Couplage `MGTextBox`** : les 8 sites se repartissent en 3 usages — test de
type pur (`MGDesktop.cs:545`/`558`/`1366`, `UIFocusNavigationService.cs:216`),
lecture de `IsReadonly` (public), abonnement a `ReadonlyChanged` (public,
`EventHandler<bool>`, `MGDesktop.cs:953`/`960`/`985`), appel a
`ShouldPreserveTextEntryKey(Keys)` (`internal`, `UIFocusNavigationService.cs:454`).

**`WindowKeyboardHandler`** : API publique complete (meme forme qu'un
`KeyboardHandler` de `MGElement`), mais jamais de focus clavier possible comme
demontre en 1.c — point d'extension mort, pas un bug de livraison.

## 3. Proposition

### 3.a Activation de fenetre au clic / `ActiveWindow`

Options : **(A)** documenter seulement, cout nul mais ne resout rien.
**(B)** `ActiveWindow` en lecture seule, derive du focus courant
(`FocusedKeyboardHandler?.SelfOrParentWindow`, deja calcule via
`State.WindowFocusHistory`, `MGDesktop.cs:965`) + `ActiveWindowChanged` — sans
activation automatique, resout seulement l'observabilite. **(C)** activation-au-
clic opt-in par fenetre (`MGWindow.ActivatesOnClick`, nom propose) cablant un
handler equivalent a `MGFloatingDockWindow.cs:100` au niveau de `MGWindow`, qui
appelle `Desktop.BringToFront(this)` / `ParentWindow.BringToFront(this)` puis
met a jour `ActiveWindow`.

**Recommandation** : (C) + (B) comme sous-brique (sans `ActiveWindow`
observable, l'activation au clic n'ajoute rien a l'appel manuel deja possible).

**Interactions** : avec `IsTopmost` — `BringToFront` reste soumis au tri par
`IsTopmost`, une fenetre non-topmost cliquee ne passe jamais devant une
fenetre topmost, comportement inchange. Avec le docking — cablage local
redondant mais idempotent si conserve (a trancher, Q.O. 2). Avec les overlays
modaux — l'activation doit rester sans effet derriere un overlay modal actif,
en reutilisant la garde `HasModalWindow`/`OverlayHost.IsModal` existante, sans
en ajouter une nouvelle.

**Impact API** : nouveaux `MGDesktop.ActiveWindow`/`ActiveWindowChanged`,
`MGWindow.ActivatesOnClick`. Source-compatible (aucune signature existante ne
change). Comportement observable : si `ActivatesOnClick` par defaut est
`true`, toute fenetre existante gagne un comportement qu'elle n'avait pas —
changement de defaut a valider explicitement (Q.O. 1) ; si `false`, purement
additif.

### 3.b `ITextEntryHost` remplacant `is MGTextBox`

Membres minimaux, strictement derives des usages en section 2 :

```csharp
// PROPOSE
public interface ITextEntryHost
{
    bool IsReadonly { get; }
    event EventHandler<bool> ReadonlyChanged;
    bool ShouldPreserveTextEntryKey(Keys key);
}
```

`IsReadonly`/`ReadonlyChanged` existent deja publics sur `MGTextBox`
(`:935`/`:957`) — implementation sans changement. `ShouldPreserveTextEntryKey`
doit passer d'`internal` a public (ou implementation d'interface explicite
publique) : seul changement de visibilite requis. `MGTextBox` implemente
l'interface (sinon le remplacement casse le seul type qui l'utilise
aujourd'hui). Interface **publique** : necessaire pour qu'un controle tiers
hors `MGUI.Core` puisse l'implementer, but explicite du texte de la tache.

**Impact API** : nouveau type public `ITextEntryHost`.
`MGTextBox.ShouldPreserveTextEntryKey` s'elargit (`internal` -> public) —
source-compatible mais elargit la surface publique de `MGTextBox` (a noter en
revue API). Comportement observable inchange pour `MGTextBox` ; nouveau
seulement pour un futur type tiers.

### 3.c Point d'extension clavier de fenetre reel, ou suppression

Options : **(A)** suppression pure — `WindowKeyboardHandler` etant `public`
et deja livre, cassant pour un abonnement existant. **(B)** le rendre
fonctionnel en donnant a `MGWindow` la capacite de recevoir elle-meme le
focus clavier — change un invariant de focus global (interaction avec les
scopes de focus des menus, `Docs/input-architecture.md` "Scopes de focus"),
trop profond pour un simple "point d'extension". **(C)** nouveau point
d'extension explicite independant du focus de la fenetre — un handler
"preview" par fenetre, pompe systematiquement pendant `MGWindow.Update`
(comme `HighPriorityKeyboardHandler` du desktop mais scope a la fenetre),
voyant le clavier du tick que le focus soit sur la fenetre ou un descendant ;
`WindowKeyboardHandler` marque `[Obsolete]`, conserve tel quel.

**Recommandation** : (C) — resout le symptome sans toucher l'invariant de
focus global ((B)) ni casser l'existant ((A)).

**Impact API** : `WindowKeyboardHandler` reste, `[Obsolete]` (avertissement,
pas erreur) — source-compatible, il ne recevait deja rien. Nouveau membre
(nom/forme a trancher, Q.O. 5). Comportement observable nouveau uniquement
pour qui s'abonne au nouveau membre.

## 4. Risques et non-buts

Hors perimetre : light-dismiss fragile du `MGComboBox` (`Docs/input-
architecture.md`, "Limites connues" point 2) ; racines residuelles
`MGResources` des fenetres fermees (chantier separe). Aucun invariant de
"Invariants de cycle de vie" n'est re-arbitre : pas de nouvel abonnement de
constructeur a un evenement de duree de vie desktop/runtime, pas de nouveau
weak event, `Unsubscribe()` reste hors teardown, "fermer n'est pas mourir"
reste vrai pour toute fenetre gagnant `ActivatesOnClick`/`ActiveWindow`. 3.a
ne doit jamais court-circuiter `HasModalWindow`/les overlays modaux. 3.b ne
touche que les 8 sites de routage input identifies, pas le rendu de texte ni
un autre sous-systeme ayant besoin du type concret `MGTextBox`. 3.c ne
reintroduit pas de bubbling clavier vers les ancetres (invariant "pas de
bubbling" de `Docs/input-architecture.md` inchange) : le nouveau point
d'extension est un preview du tick, pas un fallback de non-consommation.

## 5. Decoupage propose

**Requiert une validation explicite avant toute ligne de code de production.**
Chaque slice est independamment executable et revertible.

1. **`ActiveWindow` observable (sans clic).** Outcome : propriete + evenement
   derives du focus. Perimetre : `MGDesktop.cs`. Acceptation : focus un element
   d'une fenetre B fait passer `ActiveWindow` de A a B sans `BringToFront`.
   Taille : petite.
2. **`MGWindow.ActivatesOnClick`.** Outcome : cablage clic -> `BringToFront`,
   garde par overlay modal actif. Perimetre : `MGWindow.cs` (pas
   `MGFloatingDockWindow.cs`, cf. Q.O. 2). Acceptation : deux fenetres
   non-topmost, clic sur celle du dessous la ramene au premier plan ; aucun
   effet sous overlay modal. Prerequis : slice 1. Taille : petite a moyenne.
3. **`ITextEntryHost` + migration des 4 sites `MGDesktop.cs`.** Outcome :
   interface definie, `MGTextBox` l'implemente, `ShouldPreserveTextEntryKey`
   publique, sites 545/558/985/953-960 migres. Perimetre : `MGTextBox.cs`,
   `MGDesktop.cs`, nouveau fichier interface. Acceptation : suite `Focus|Input`
   inchangee ; un double `ITextEntryHost` non-`MGTextBox` prouve le
   fonctionnement pour un type tiers. Taille : moyenne.
4. **Migration des 2 sites `UIFocusNavigationService.cs`.** Outcome : `:216`
   et `:454` testent `ITextEntryHost`. Perimetre : `UIFocusNavigationService.cs`.
   Acceptation : suite `Focus|Input` inchangee ; garde de preservation testee
   avec un double tiers. Prerequis : slice 3. Taille : petite.
5. **Point d'extension clavier de fenetre + depreciation.** Outcome : nouveau
   membre "preview" pompe pendant `MGWindow.Update`, `WindowKeyboardHandler`
   `[Obsolete]`. Perimetre : `MGWindow.cs`. Acceptation : un handler abonne au
   nouveau membre recoit `Pressed` du tick que le focus soit sur la fenetre ou
   un descendant ; `WindowKeyboardHandler` compile (avec avertissement) et ne
   recoit toujours rien. Independant des slices 1-4. Taille : petite a
   moyenne.

## 6. Questions ouvertes

1. `ActivatesOnClick` par defaut `true` (change le comportement de toute
   fenetre existante) ou `false` (additif, resout le symptome seulement pour
   qui l'active) ?
2. `MGFloatingDockWindow` garde-t-elle son cablage local
   (`MGFloatingDockWindow.cs:100`) une fois `ActivatesOnClick` disponible sur
   `MGWindow` ?
3. `ITextEntryHost` dans `MGUI.Core/UI/InputRouting` (a cote de
   `IInputContext`) ou `MGUI.Core/UI` (a cote de `MGTextBox`) ?
4. `ShouldPreserveTextEntryKey` publique directe, ou implementation
   d'interface explicite (surface publique plus etroite) ?
5. Forme exacte du point d'extension clavier de fenetre en 3.c : preview
   systematique du tick (propose) ou portee plus etroite (ex. seulement les
   touches non consommees par les enfants en fin de tick) — impacte
   l'invariant "pas de bubbling" cite en section 4, a valider explicitement.
6. Slice 5 (extension clavier) incluse dans la validation maintenant, ou
   traitee independamment des slices 1-4 ?
