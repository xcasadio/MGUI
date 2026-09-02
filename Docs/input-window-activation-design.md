# Conception : activation de fenetre et points d'extension reels

## Statut

Document de conception issu de la tache 8 de `Docs/Tasks/input-tasks.md`. Le
decoupage (section 5) a ete valide par l'utilisateur : les slices 1 a 4 forment
le lot valide, la slice 5 est reportee. Aucun code de production n'a ete ecrit
sous cette tache.

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
**Decision utilisateur** : `MGWindow.ActivatesOnClick` a pour valeur par
defaut `true`.

**Consequence du defaut a `true`** : `MGDesktop.BringToFront`
(`MGUI.Core/UI/MGDesktop.cs:862-878`) deplace la fenetre en fin de `Windows`
(`List<MGWindow>`), liste qui pilote l'ordre de draw, l'ordre d'update et donc
le routage souris et le hover. Avec le defaut a `true`, toute fenetre
existante gagne le cablage clic -> premier plan, y compris dans les tests
headless qui pressent des boutons souris sur des fenetres superposees : deux
suites sont concernees, `MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs`
et `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs`. La slice 2 doit donc
prevoir et budgeter ce risque de churn de tests, et son acceptation doit
inclure la revue deliberee (pas accidentelle) de ces deux suites.

**Interactions** : avec `IsTopmost` — `BringToFront` reste soumis au tri par
`IsTopmost`, une fenetre non-topmost cliquee ne passe jamais devant une
fenetre topmost, comportement inchange. Avec le docking — `MGFloatingDockWindow`
migre vers le mecanisme generique : son cablage local
(`MGFloatingDockWindow.cs:100`) est supprime au profit d'`ActivatesOnClick`,
afin que la garde modale soit appliquee de facon uniforme (decision
utilisateur). Avec les overlays modaux — l'activation doit rester sans effet
derriere un overlay modal actif, en reutilisant la garde
`HasModalWindow`/`OverlayHost.IsModal` existante, sans en ajouter une
nouvelle.

**Impact API** : nouveaux `MGDesktop.ActiveWindow`/`ActiveWindowChanged`,
`MGWindow.ActivatesOnClick`. Source-compatible (aucune signature existante ne
change). Comportement observable : `ActivatesOnClick` par defaut `true`
(decision utilisateur) — toute fenetre existante gagne un comportement
qu'elle n'avait pas, cf. la consequence A ci-dessus (churn attendu sur
`OverlappingWindowsInputRoutingTests.cs` et `ContextMenuHoverOcclusionTests.cs`).

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
est expose en implementation d'interface explicite
(`bool ITextEntryHost.ShouldPreserveTextEntryKey(Keys key)`, decision
utilisateur) : le membre reste `internal` sur le type concret `MGTextBox` et
n'est accessible que via l'interface, de sorte que la liste des membres
publics de `MGTextBox` ne s'agrandit pas. `MGTextBox` implemente l'interface
(sinon le remplacement casse le seul type qui l'utilise aujourd'hui).
Interface **publique**, definie dans `MGUI.Core/UI` (a cote de `MGTextBox`,
decision utilisateur) : necessaire pour qu'un controle tiers hors `MGUI.Core`
puisse l'implementer, but explicite du texte de la tache.

**Impact API** : nouveau type public `ITextEntryHost` (`MGUI.Core/UI`).
`ShouldPreserveTextEntryKey` reste `internal` sur `MGTextBox` et n'est
accessible que via `ITextEntryHost` (implementation d'interface explicite) —
la surface publique de `MGTextBox` ne change pas. Comportement observable
inchange pour `MGTextBox` ; nouveau seulement pour un futur type tiers qui
implemente l'interface.

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
focus global ((B)) ni casser l'existant ((A)). Le handler est pompe **avant**
les enfants de la fenetre (preview), documente comme l'equivalent
window-scoped du `HighPriorityKeyboardHandler` du desktop — pas un fallback
pour les touches non consommees, ce qui serait du bubbling. Une variante
post-enfants est rejetee pour cette raison.

**Argument de faisabilite (fait B)** :
`IKeyboardHandlerHost.HasKeyboardFocus()` a une implementation d'interface par
defaut qui retourne `true` (`MGUI.Shared/Input/Keyboard/KeyboardTracker.cs:17`).
`MGDesktop` declare l'interface sans la surcharger, ce qui explique pourquoi
son `HighPriorityKeyboardHandler` voit chaque touche. `MGElement` la surcharge
explicitement (`MGUI.Core/UI/MGElement.cs:1621`,
`GetDesktop().FocusedKeyboardHandler == this`), ce qui rend
`MGWindow.WindowKeyboardHandler` inerte (cf. 1.c). Consequence : le handler
preview de la slice 5 n'a besoin que d'un proprietaire qui n'est **pas**
`MGWindow` elle-meme et qui conserve l'implementation par defaut — aucun
changement de l'invariant de focus global n'est requis, ce qui rend l'option
(C) nettement moins couteuse et plus sure que ne le laissait supposer la
premiere version de ce document.

**Guidance pratique** : un abonne au point d'extension doit ignorer les
touches tant qu'un `ITextEntryHost` a le focus — c'est cette regle qui rend le
preview sur (avant les enfants) sans danger (pas de vol de touches d'edition).

**Impact API** : `WindowKeyboardHandler` reste, `[Obsolete]` (avertissement,
pas erreur) — source-compatible, il ne recevait deja rien. Nouveau membre
preview (forme decidee par la decision Q5 ; slice 5 reportee, cf. section 5).
Comportement observable nouveau uniquement pour qui s'abonne au nouveau
membre.

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
d'extension est un preview du tick, pompe avant les enfants de la fenetre, pas
un fallback de non-consommation en fin de tick — une variante post-enfants
serait du bubbling et est explicitement rejetee (3.c).

## 5. Decoupage valide

**Valide par l'utilisateur : slices 1 a 4 forment le lot a implementer,
slice 5 est reportee.** Chaque slice reste independamment executable et
revertible.

1. **`ActiveWindow` observable (sans clic).** Outcome : propriete + evenement
   derives du focus. Perimetre : `MGDesktop.cs`. Acceptation : focus un element
   d'une fenetre B fait passer `ActiveWindow` de A a B sans `BringToFront`.
   Taille : petite.
2. **`MGWindow.ActivatesOnClick`, defaut `true`.** Outcome : cablage clic ->
   `BringToFront`, garde par overlay modal actif. Perimetre : `MGWindow.cs`
   **et** `MGUI.Core/UI/Docking/Controls/MGFloatingDockWindow.cs:100` (le
   cablage local y est supprime au profit d'`ActivatesOnClick`, decision Q2,
   pour que la garde modale soit appliquee de facon uniforme). Acceptation :
   deux fenetres non-topmost, clic sur celle du dessous la ramene au premier
   plan ; aucun effet sous overlay modal ; les deux suites concernees par la
   consequence A (`MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs` et
   `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs`) sont revues et
   ajustees deliberement ; un test docking dedie epingle le comportement
   pre-existant de la fenetre flottante (clic ramene au premier plan) apres la
   migration. Prerequis : slice 1. Taille : petite a moyenne.
3. **`ITextEntryHost` + migration des 4 sites `MGDesktop.cs`.** Outcome :
   interface definie dans `MGUI.Core/UI` (a cote de `MGTextBox`, decision Q3),
   `MGTextBox` l'implemente, `ShouldPreserveTextEntryKey` exposee en
   implementation d'interface explicite (decision Q4 — le membre reste
   `internal` sur `MGTextBox`), sites 545/558/985/953-960 migres. Perimetre :
   `MGTextBox.cs`, `MGDesktop.cs`, nouveau fichier interface dans
   `MGUI.Core/UI`. Acceptation : suite `Focus|Input` inchangee ; un double
   `ITextEntryHost` non-`MGTextBox` prouve le fonctionnement pour un type
   tiers. Taille : moyenne.
4. **Migration des 2 sites `UIFocusNavigationService.cs`.** Outcome : `:216`
   et `:454` testent `ITextEntryHost`. Perimetre : `UIFocusNavigationService.cs`.
   Acceptation : suite `Focus|Input` inchangee ; garde de preservation testee
   avec un double tiers. Prerequis : slice 3. Taille : petite.
5. **[REPORTEE] Point d'extension clavier de fenetre + depreciation.** Forme
   decidee (decision Q5 : preview pompe avant les enfants de la fenetre, cf.
   3.c) mais **non incluse dans le lot valide maintenant** (decision Q6).
   Outcome : nouveau membre "preview" pompe pendant `MGWindow.Update`,
   `WindowKeyboardHandler` `[Obsolete]`. Perimetre : `MGWindow.cs`.
   Acceptation : un handler abonne au nouveau membre recoit `Pressed` du tick
   que le focus soit sur la fenetre ou un descendant ; `WindowKeyboardHandler`
   compile (avec avertissement) et ne recoit toujours rien. Independant des
   slices 1-4. Taille : petite a moyenne.

## 6. Decisions validees

1. `MGWindow.ActivatesOnClick` a pour valeur par defaut `true` — accepte que
   toute fenetre existante gagne le comportement, avec la consequence A
   (churn deliberement gere) comme contrepartie assumee.
2. `MGFloatingDockWindow` migre vers le mecanisme generique : son cablage
   local (`MGFloatingDockWindow.cs:100`) est supprime au profit
   d'`ActivatesOnClick`, pour que la garde modale soit appliquee de facon
   uniforme plutot que dupliquee.
3. `ITextEntryHost` vit dans `MGUI.Core/UI`, a cote de `MGTextBox`, plutot que
   dans `MGUI.Core/UI/InputRouting`.
4. `ShouldPreserveTextEntryKey` est exposee en implementation d'interface
   explicite (`bool ITextEntryHost.ShouldPreserveTextEntryKey(Keys key)`) :
   le membre reste `internal` sur le type concret, la surface publique de
   `MGTextBox` ne s'agrandit pas.
5. Le point d'extension clavier de fenetre en 3.c est un preview pompe
   **avant** les enfants de la fenetre, documente comme l'equivalent
   window-scoped du `HighPriorityKeyboardHandler` du desktop — pas un
   fallback pour touches non consommees (qui serait du bubbling).
6. La slice 5 (extension clavier) est reportee : sa forme est decidee par la
   decision 5 ci-dessus, mais elle ne fait pas partie du lot a implementer
   maintenant. Les slices 1 a 4 forment le lot valide.

### Points encore ouverts

Aucun point ouvert ne subsiste sur le perimetre couvert par ce document : les
six decisions ci-dessus, combinees aux slices 1 a 4 (section 5), suffisent a
lancer l'implementation du lot valide.
