# Taches activation de fenetre et points d'extension input

## Objectif

La conception et les six decisions utilisateur sont deja validees et vivent dans
`Docs/input-window-activation-design.md` (section 5 pour le decoupage, section 6
pour les decisions). Ce fichier ne fait que porter l'execution du lot valide : les
slices 1 a 4 du decoupage, dans l'ordre. La slice 5 (point d'extension clavier de
fenetre) est reportee par decision utilisateur et n'est pas incluse dans le lot a
executer maintenant ; elle figure ici a titre de reference, marquee reportee.

Ce document est destine a un agent IA implementeur.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit par tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Si une tache est bloquee, la marquer `⛔`, decrire le blocage juste en dessous, puis s'arreter.
- Ne pas faire de refactor hors perimetre.
- Ajouter ou adapter des tests a chaque tache impliquant un changement de comportement.
- Surfaces epinglees : les ensembles de membres de `IRenderHost`/`IUIDesktopRuntime` et des sous-chaines verbatim de `Game1.cs`/`MiniGame.cs` sont assertes par `RawInputSourceTests` et `HostRuntimeContractTests` — les lire avant d'editer ces fichiers.
- Consequence de la tache 2 (defaut `ActivatesOnClick = true`) : toute fenetre existante gagne le cablage clic -> premier plan, y compris dans les tests headless qui pressent des boutons souris sur des fenetres superposees. Deux suites sont concernees et doivent etre revues deliberement (pas accidentellement) par la tache 2 : `MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs` et `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs`.
- Fait de faisabilite pour la slice 5 (reportee, information seulement) : `IKeyboardHandlerHost.HasKeyboardFocus()` a une implementation d'interface par defaut qui retourne `true` (`MGUI.Shared/Input/Keyboard/KeyboardTracker.cs:17`), tandis que `MGElement` la surcharge explicitement (`MGUI.Core/UI/MGElement.cs:1621`, `GetDesktop().FocusedKeyboardHandler == this`) — c'est ce qui rend `MGWindow.WindowKeyboardHandler` inerte aujourd'hui et ce qui permet au futur handler preview de la slice 5 de fonctionner sans toucher cet invariant.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "Focus|Input" --logger "console;verbosity=minimal"` + filtre cible de la tache
4. Taches touchant la couche semantique (3, 6, 7) : `dotnet build .\MGUI.MiniGame\MGUI.MiniGame.csproj --no-restore`
5. Critere transversal : aucun test nouvellement rouge par rapport a l'etat de depart.

## Taches

### ⚪ 1. `ActiveWindow` observable (sans clic)

But:

introduire la notion de fenetre "active", aujourd'hui absente du framework, en lecture seule et derivee du focus courant — sans activation automatique, cette slice resout uniquement l'observabilite (option (B) de la section 3.a du document de conception).

Travail attendu:

- ajouter `MGDesktop.ActiveWindow` (lecture seule) derivee du focus courant (`FocusedKeyboardHandler?.SelfOrParentWindow`), en reutilisant le mecanisme deja calcule via `State.WindowFocusHistory` (`MGUI.Core/UI/MGDesktop.cs:965`) ;
- ajouter l'evenement `MGDesktop.ActiveWindowChanged` ;
- ne pas cabler d'activation au clic dans cette tache (c'est la tache 2) ;
- tests : focus un element d'une fenetre B alors que `ActiveWindow` valait A -> `ActiveWindow` passe a B et `ActiveWindowChanged` se declenche, sans appel a `BringToFront`.

Criteres d'acceptation:

- focus un element d'une fenetre B fait passer `ActiveWindow` de A a B sans `BringToFront` ;
- aucun test nouvellement rouge.

Commit recommande:

- `feat: add observable ActiveWindow derived from keyboard focus`

### ⚪ 2. `MGWindow.ActivatesOnClick`, defaut `true`

But:

cabler l'activation de fenetre au clic hors du module docking, generalisee au niveau `MGWindow`, avec garde par overlay modal actif.

Travail attendu:

- ajouter `MGWindow.ActivatesOnClick` (defaut `true`, decision utilisateur) cablant un handler equivalent a l'ancien cablage local du docking, qui appelle `Desktop.BringToFront(this)` / `ParentWindow.BringToFront(this)` puis met a jour `ActiveWindow` (slice 1) ;
- l'activation doit rester sans effet derriere un overlay modal actif, en reutilisant la garde `HasModalWindow`/`OverlayHost.IsModal` existante, sans en ajouter une nouvelle ;
- `BringToFront` reste soumis au tri par `IsTopmost` — une fenetre non-topmost cliquee ne doit jamais passer devant une fenetre topmost ;
- migrer `MGFloatingDockWindow` vers ce mecanisme generique : supprimer le cablage local (`MGUI.Core/UI/Docking/Controls/MGFloatingDockWindow.cs:100`, `MouseHandler.LMBPressedInside += (_, _) => BringToFront();`) au profit d'`ActivatesOnClick`, afin que la garde modale soit appliquee de facon uniforme (decision utilisateur) ;
- perimetre : `MGWindow.cs` et `MGFloatingDockWindow.cs:100` ;
- revoir deliberement (voir avertissement de churn en "Consignes") `MGUI.Tests/Input/OverlappingWindowsInputRoutingTests.cs` et `MGUI.Tests/Input/ContextMenuHoverOcclusionTests.cs` : ajuster les tests dont le z-order attendu change du fait du nouveau cablage clic par defaut ;
- ajouter un test docking dedie qui epingle le comportement pre-existant de la fenetre flottante (clic ramene au premier plan) apres la migration.

Criteres d'acceptation:

- deux fenetres non-topmost, clic sur celle du dessous la ramene au premier plan ;
- aucun effet sous overlay modal ;
- les deux suites `OverlappingWindowsInputRoutingTests.cs` et `ContextMenuHoverOcclusionTests.cs` sont revues et ajustees deliberement ;
- un test docking dedie epingle le comportement pre-existant de la fenetre flottante (clic ramene au premier plan) apres la migration ;
- aucun test nouvellement rouge en dehors du churn attendu et traite ci-dessus.

Commit recommande:

- `feat: activate window on click via MGWindow.ActivatesOnClick`

Prerequis : tache 1.

### ⚪ 3. `ITextEntryHost` + migration des 4 sites `MGDesktop.cs`

But:

remplacer les tests `is MGTextBox` par une interface publique, pour qu'un controle d'edition tiers hors `MGUI.Core` beneficie des memes protections (gameplay non bloque pendant la saisie, touches de navigation internes volees, pas de nettoyage de focus a la readonly).

Travail attendu:

- definir dans `MGUI.Core/UI` (a cote de `MGTextBox`, decision utilisateur), un nouveau fichier interface public :

```csharp
public interface ITextEntryHost
{
    bool IsReadonly { get; }
    event EventHandler<bool> ReadonlyChanged;
    bool ShouldPreserveTextEntryKey(Keys key);
}
```

- `MGTextBox` implemente l'interface : `IsReadonly`/`ReadonlyChanged` sont deja publics (`MGTextBox.cs:935`/`:957`, implementation sans changement) ; `ShouldPreserveTextEntryKey` est exposee en implementation d'interface explicite (`bool ITextEntryHost.ShouldPreserveTextEntryKey(Keys key)`, decision utilisateur) — le membre reste `internal` sur `MGTextBox` (`MGTextBox.cs:69`) et n'est accessible que via l'interface, la surface publique de `MGTextBox` ne s'agrandit pas ;
- migrer les 4 sites `MGDesktop.cs` vers `ITextEntryHost` : `:545` (`ResolveSemanticInputMode`), `:558` (`ShouldCaptureGameplayInput`), `:1366` (meme test, chemin `Update` brut), `:953`/`:960`/`:985` (setter de `FocusedKeyboardHandler`, abonnement/desabonnement a `ReadonlyChanged`, `TextBox_ReadonlyChanged`) ;
- tests : suite `Focus|Input` inchangee ; un double `ITextEntryHost` non-`MGTextBox` prouve le fonctionnement pour un type tiers (gameplay bloque pendant sa saisie, `ReadonlyChanged` nettoie bien le focus).

Criteres d'acceptation:

- suite `Focus|Input` inchangee ;
- un double `ITextEntryHost` non-`MGTextBox` prouve le fonctionnement pour un type tiers ;
- aucun test nouvellement rouge.

Commit recommande:

- `feat: introduce ITextEntryHost and migrate MGDesktop text-entry sites`

### ⚪ 4. Migration des 2 sites `UIFocusNavigationService.cs`

But:

completer la migration `ITextEntryHost` sur le service de navigation, dernier sous-systeme testant encore `is MGTextBox` ou appelant un membre `internal` de `MGTextBox`.

Travail attendu:

- migrer `UIFocusNavigationService.cs:216` (`TryDispatchNavigationAction`, flag `isTextEntryFocused`) et `:454` (`ShouldPreserveTextEntryKey`, qui appelle `MGTextBox.ShouldPreserveTextEntryKey`, `internal`) vers `ITextEntryHost` ;
- perimetre : `UIFocusNavigationService.cs` uniquement ;
- tests : suite `Focus|Input` inchangee ; garde de preservation testee avec un double `ITextEntryHost` tiers.

Criteres d'acceptation:

- suite `Focus|Input` inchangee ;
- garde de preservation testee avec un double tiers ;
- aucun test nouvellement rouge.

Commit recommande:

- `refactor: migrate UIFocusNavigationService to ITextEntryHost`

Prerequis : tache 3.

### ⚪ 5. [REPORTEE] Point d'extension clavier de fenetre + depreciation

Reportee par decision utilisateur (decision 6 de `Docs/input-window-activation-design.md`) : non incluse dans le lot a executer maintenant. La forme est deja decidee (decision 5 du meme document) : un handler "preview" par fenetre, pompe **avant** les enfants de la fenetre pendant `MGWindow.Update`, documente comme l'equivalent window-scoped du `HighPriorityKeyboardHandler` du desktop — pas un fallback pour les touches non consommees, ce qui serait du bubbling. Cette tache reste independante des taches 1 a 4 et pourra etre reprise dans une session dediee une fois le report leve.

But:

donner a `MGWindow.WindowKeyboardHandler` (inerte aujourd'hui, cf. `Docs/input-window-activation-design.md` section 1.c) un point d'extension clavier de fenetre reellement fonctionnel, sans toucher l'invariant de focus global.

Travail attendu (pour reference, non a executer avant leve du report):

- ajouter un nouveau membre "preview" pompe systematiquement pendant `MGWindow.Update`, voyant le clavier du tick que le focus soit sur la fenetre ou un descendant ;
- marquer `WindowKeyboardHandler` `[Obsolete]` (avertissement, pas erreur), conserve tel quel — source-compatible, il ne recevait deja rien ;
- un abonne au point d'extension doit ignorer les touches tant qu'un `ITextEntryHost` a le focus (regle qui rend le preview sans danger, pas de vol de touches d'edition) ;
- perimetre : `MGWindow.cs`.

Criteres d'acceptation:

- un handler abonne au nouveau membre recoit `Pressed` du tick que le focus soit sur la fenetre ou un descendant ;
- `WindowKeyboardHandler` compile (avec avertissement) et ne recoit toujours rien ;
- independant des taches 1 a 4.

Commit recommande:

- `feat: add window-scoped preview keyboard handler, deprecate WindowKeyboardHandler`
