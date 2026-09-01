# Input Leaks and TextInput Wiring Tasks

## Objectif

Corriger les trois fuites memoire du systeme d'input et cabler la saisie de texte native (TextInput/IME), identifiees et contre-verifiees dans `Docs/event-handling-architecture.md` (faiblesses 4, 5, 6) :

1. les handlers souris/clavier ne sont jamais desenregistres des trackers (`MouseTracker._Handlers` / `KeyboardTracker._Handlers` enracinent chaque element interactif a vie) ;
2. `MGTextBox` et `MGNumericUpDown` s'abonnent a `MGDesktop.FocusedKeyboardHandlerChanged` dans leurs constructeurs sans jamais se desabonner ; fuite supplementaire du meme type decouverte pendant la preparation de ce plan : `MGListBox` s'abonne a `GetDesktop().Runtime.EndUpdate` dans son constructeur (MGListBox.cs:1506-1520) sans desabonnement ;
3. le puits `IKeyboardTextInputSink.QueueTextInput` existe et est teste, mais rien ne connecte `GameWindow.TextInput` dessus : la saisie retombe sur une table US-QWERTY codee en dur (AZERTY/touches mortes/IME casses par defaut).

Ce document est destine a un agent IA implementeur.

## Philosophie a respecter

MGUI est un framework UI pour moteur de jeu, **pas une replique de WPF**. Les correctifs doivent :

- rester dans le modele game-loop existant (poll/diff par tick, handlers manuels pompes par la traversee) ;
- privilegier des mecanismes simples, deterministes et sans cout par frame (pas de weak events generalises, pas d'`IDisposable` impose a tout l'arbre d'elements) ;
- etre opt-in ou invisibles pour le code utilisateur existant ;
- s'adapter a plusieurs scenarios d'hebergement (GameRenderHost, DelegateRenderHost, runtime custom).

## Decisions de design deja arbitrees (ne pas re-explorer)

Ces choix ont ete valides par une verification exhaustive du code ; l'agent les applique tels quels.

### D1. Fuite trackers : ne PAS enregistrer les handlers manuels

`CreateHandler(owner, priority)` avec `priority == null` (handler manuel) n'ajoute plus le handler a `_Handlers`. Justification verifiee :

- l'unique lecteur des listes est `UpdateHandlers()` (MouseTracker.cs:490, KeyboardTracker.cs:296) et il filtre deja les manuels via `Where(x => !x.IsManualUpdate)` — l'enregistrement des manuels est du poids mort pur + une racine GC ;
- `ManualUpdate()`/`InvokeQueuedEvents` ne lisent que l'etat snapshot du tracker et l'Owner, jamais l'appartenance a la liste — un handler non enregistre fonctionne a 100 % ;
- les constructeurs des handlers sont `internal` : `CreateHandler` couvre tous les chemins de creation ;
- `Unsubscribe()` sur un handler jamais enregistre reste sur (`List.Remove` no-op) et garde son role de kill switch (`IsValid = false`) — conserver ce comportement, ne pas le faire throw ;
- les handlers auto (priorite non nulle — uniquement `MGUI.Tests/Focus/InputEnhancedTests.cs` dans le repo, priorites 10/20) gardent enregistrement + `Unsubscribe` inchanges.

**Approche INTERDITE** : appeler `Unsubscribe()` depuis un chemin de teardown (fermeture de fenetre, retrait de liste). Raisons verifiees : fermer n'est pas mourir (`SampleBase.Show/Hide` re-ajoute la MEME instance de `MGWindow` via `Desktop.Windows.Add/Remove`, Compendium.xaml.cs:43-50 ; `BringToFront` fait Remove+Add) ; `IsValid` est definitivement false sans API de revalidation ; les abonnements de constructeurs (fence de MGWindow, `MGTextBox.KeyboardHandler.Pressed`, grilles) mourraient silencieusement. La recommandation 1 de `event-handling-architecture.md` (Unsubscribe au teardown) est **remplacee** par la presente decision.

### D2. Fuite focus : notification directe par le setter, pas d'abonnement

Ajouter sur `MGElement` un virtuel `protected internal virtual void OnKeyboardFocusChanged(bool gained) { }` (nom verifie sans collision ; meme pattern de modificateur que `OnThemeChanged`, MGElement.cs:567). Le setter prive `MGDesktop.FocusedKeyboardHandler` (MGDesktop.cs:971-1013, unique point de mutation, garanti `Previous != New`) appelle directement `Previous?.OnKeyboardFocusChanged(false)` puis `value?.OnKeyboardFocusChanged(true)`, **immediatement avant** l'invocation de l'event public a la ligne ~1010 — donc STRICTEMENT APRES l'affectation `State.FocusedKeyboardHandler = value` (~:992) et apres le swap `ReadonlyChanged`. Cet ordre est critique : `MGTextBox.UpdateFormattedText` lit `GetDesktop().FocusedKeyboardHandler == this` pour choisir les couleurs de selection (MGTextBox.cs:253).

Migrations :
- `MGTextBox` : supprimer la lambda du constructeur (MGTextBox.cs:1252-1258) ; override qui appelle `UpdateFormattedText(true)` pour `gained` true ET false. `UpdateFormattedText` etant virtuel, `MGRichTextBox` herite du bon comportement — ne rien changer chez lui.
- `MGNumericUpDown` : supprimer l'abonnement (MGNumericUpDown.cs:193) et `HandleFocusChanged` ; override qui appelle `base.OnKeyboardFocusChanged(gained)` PUIS, si `!gained`, `CommitPendingText(false)`. L'appel a base est obligatoire (sinon la recoloration de selection heritee disparait) et reproduit l'ordre actuel des deux lambdas.
- `MGGraphControls` (:869/:949) et `MGPropertyGrid` (:1059/:1128) restent sur l'event public : ils observent des transitions entre elements arbitraires, inexprimable en self-only. L'event public `FocusedKeyboardHandlerChanged` est conserve tel quel (du code utilisateur s'en sert, ex. MGUI.Samples/Features/FocusInputReview.xaml.cs:82).

### D3. Fuite MGListBox : supprimer l'abonnement Runtime.EndUpdate

La lambda du constructeur (MGListBox.cs:1506-1520) ne fait que reinitialiser `PressedItem`/`SpoofIsPressedWhileDrawingBackground` en fin de tick. La remplacer par une reinitialisation dans le cycle de vie propre du controle (debut de son propre update suivant, ex. hook `OnBeginUpdate` deja existant) — la sequence par frame etant Update -> Draw -> Runtime.EndUpdate, reinitialiser au debut de l'update suivant est equivalent pour le Draw. L'agent DOIT verifier cette equivalence en lisant les usages des deux champs avant de coder, et pinner le comportement par test si un test de rendu presse existe.

### D4. TextInput : seam opt-in dans l'assembly Integration + auto-cablage au bootstrap

Contraintes d'assemblies verifiees : `GameRenderHost<T>` vit dans MGUI.MonoGame.Integration ; `MainRenderer`/`MonoGameBackendBootstrap` sont compiles (fichiers linkes) dans MGUI.MonoGame.LegacyRenderer qui reference Integration. Integration reference MGUI.Shared donc voit `IKeyboardTextInputSink`.

- Definir dans l'assembly Integration (nouveau fichier sous `MGUI.MonoGame.Integration/Rendering/`, NON linke dans LegacyRenderer — attention au hasard de types dupliques) une interface opt-in, par ex. `ITextInputHost { void AttachTextInputSink(IKeyboardTextInputSink sink); void DetachTextInputSink(); }`.
- `GameRenderHost<T>` l'implemente : `Attach` stocke un delegue nomme et s'abonne a `Game.Window.TextInput` ; `Detach` se desabonne ; `Dispose()` appelle `Detach` (mettre a jour le message debug "unsubscribed 3 event handlers", RenderHost.cs:73). Exposer une methode `internal` de traitement (ex. `internal void OnTextInput(TextInputEventArgs e)`) pour la testabilite — `InternalsVisibleTo` MGUI.Tests existe deja (AssemblyInfo.cs:4). Corps du handler : `sink.QueueTextInput(e.Character, e.Key)` (le puits filtre deja les caracteres de controle sauf \t \r \n, KeyboardTracker.cs:237-238). Note : en MonoGame 3.8.4.1, `TextInputEventArgs.Character/.Key` sont des CHAMPS publics, pas des proprietes.
- `MonoGameBackendBootstrap.Create` auto-cable apres construction du renderer : `if (host is ITextInputHost t) t.AttachTextInputSink(renderer.Input.Keyboard);`. Zero code utilisateur sur le chemin `GameRenderHost`.
- Chemin `DelegateRenderHost` (aucun `GameWindow` cote MGUI, ex. MiniGame) : cablage manuel documente + demonstration dans MiniGame (voir tache 5).

**Surfaces a NE PAS toucher** (tests d'architecture qui les epinglent) :
- `IRenderHost` : aucun nouveau membre (RawInputSourceTests.cs:9-28) ;
- `IUIDesktopRuntime` : aucun nouveau membre (HostRuntimeContractTests.MainRenderer_ImplementsSmallDesktopRuntimeContract, :112-151) ;
- pas de nouvel usage `Runtime.*` dans MGDesktop.cs (HostRuntimeContractTests.cs:71-93) ;
- toute edition de `Game1.cs`/`MiniGame.cs` doit preserver les sous-chaines verbatim assertees par HostRuntimeContractTests.cs:95-110 — les lire AVANT d'editer.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit par tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Si une tache est bloquee, marquer la tache avec `⛔`, decrire le blocage juste en dessous, puis s'arreter.
- Ne pas faire de refactor massif hors perimetre ; appliquer les decisions D1-D4 sans les re-arbitrer.
- Preserver les APIs publiques existantes (`FocusedKeyboardHandlerChanged`, `Unsubscribe`, `Handlers`, `CreateHandler`) sauf extension explicitement demandee.
- Ajouter ou adapter des tests a chaque tache.
- Ne pas passer a la tache suivante tant que la tache courante n'est pas validee et committee.
- Ne pas toucher aux fichiers modifies du worktree sans rapport avec ce plan (zone graph : GraphDocumentViewSynchronizer.cs, MGGraphControls.cs, tests graph) au-dela de ce que les taches demandent.
- Le dossier `MGUI.MonoGame/` ne contient que des artefacts morts (pas de csproj, absent de la solution) : ne rien y faire.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale apres chaque tache

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet build .\MGUI.MiniGame\MGUI.MiniGame.csproj --no-restore` (taches 4-5)
4. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "Focus|Input" --logger "console;verbosity=minimal"` + filtre cible de la tache
5. Critere transversal : **aucun test nouvellement rouge** par rapport a la baseline de la tache 0. Les rouges preexistants recenses en tache 0 ne bloquent pas.

## Ordre de commits attendu

1. `test: complete task 0 record baseline test state`
2. `input: complete task 1 stop registering manual handlers in trackers`
3. `focus: complete task 2 add element keyboard focus notification`
4. `controls: complete task 3 remove listbox runtime endupdate subscription`
5. `input: complete task 4 wire gamewindow textinput to keyboard sink`
6. `docs: complete task 5 document native text input wiring`
7. `test: complete task 6 add leak regression and refresh audit doc`

## Taches

### ✅ 0. Etablir la baseline de tests

But:
connaitre l'etat reel de la suite avant toute modification, car au moins un test d'architecture est suspect d'etre deja rouge sur develop.

Travail attendu:

- builder MGUI.Tests, MGUI.Samples, MGUI.MiniGame ;
- executer la suite complete : `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --logger "console;verbosity=minimal"` ;
- verifier en particulier `BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles` : il asserte des entrees `Compile Remove` (Text\FontManager.cs, Text\FontSet.cs, Text\SpritefontGenerator.cs, Text\Engines\SpriteFontTextEngine.cs) absentes de `MGUI.MonoGame.Integration.csproj` a HEAD ;
- consigner dans la section Resultat de cette tache la liste exacte des tests rouges preexistants (0 souhaite, mais ne PAS les corriger sauf trivialite evidente d'une ligne dans un test) ;
- ne modifier aucun code de production.

Criteres d'acceptation:

- la liste des tests rouges preexistants est consignee ci-dessous et servira de reference aux taches suivantes.

Commit recommande:

- `test: complete task 0 record baseline test state`

Resultat:

- build vert pour MGUI.Tests.csproj, MGUI.Samples.csproj et MGUI.MiniGame.csproj (dotnet build, 0 erreur, warnings pre-existants uniquement CS8632/SYSLIB0050) ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --logger "console;verbosity=minimal"` : total 1177 tests, 1174 reussis, 3 echoues, 0 ignores ;
- 3 tests rouges preexistants consignes comme baseline (aucun ne bloque les taches suivantes tant qu'ils restent dans cet etat) :
  1. `MGUI.Tests.Architecture.BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles` — confirme le doute signale dans le plan : le csproj de `MGUI.MonoGame.Integration` ne contient a HEAD aucune entree `<Compile Remove>` pour `Text\FontManager.cs`, `Text\FontSet.cs`, `Text\SpritefontGenerator.cs`, `Text\Engines\SpriteFontTextEngine.cs` ; 4 items sur 15 echouent l'assertion `Assert.Contains` ;
  2. `MGUI.Tests.Architecture.ToolingHooksTests.UIToolingService_ExposesSnapshotAndPreviewHooks` — `System.Reflection.AmbiguousMatchException` sur `Type.GetMethod("LoadPreview", ...)` : plusieurs surcharges de `UIToolingService.LoadPreview` correspondent a la signature recherchee par reflexion dans le test ;
  3. `MGUI.Tests.Architecture.ControlTemplateInfrastructureTests.ListBox_Rehydrates_Theme_Dependent_Row_Chrome_On_Theme_Refresh` — `Assert.Contains` echoue : le code source scanne par le test ne contient plus la sous-chaine attendue `"ItemContainerStyle == ApplyDefaultItemCon"` (probable derive du code source par rapport a l'assertion du test) ;
- aucune de ces 3 failures ne touche au perimetre du present plan (trackers, focus, MGListBox EndUpdate, TextInput) ; elles ne sont pas corrigees ici (pas de trivialite evidente d'une ligne) ;
- aucun code de production modifie pour cette tache.

### ✅ 1. Ne plus enregistrer les handlers manuels dans les trackers (D1)

But:
supprimer la racine GC tracker -> handler -> element et le cout d'enumeration par tick, sans protocole de teardown.

Travail attendu:

- `MouseTracker.CreateHandler` (MouseTracker.cs:139-141) et `KeyboardTracker.CreateHandler` (KeyboardTracker.cs:83-86) : n'ajouter le handler a `_Handlers` que si `UpdatePriority != null` ; verifier toutes les surcharges de `CreateHandler` (y compris celles prenant l'enum `InputUpdatePriority`) ;
- conserver `Unsubscribe()` inchange (List.Remove no-op sur un non-membre, `IsValid = false` conserve) ;
- mettre a jour les commentaires XML de `CreateHandler` et des proprietes `Handlers` pour enoncer le nouveau contrat (la liste ne contient que les handlers auto) ;
- mettre a jour README.md (sections ~:602 "Manages 0 to many MouseHandlers" et ~:610 guide CreateHandler) pour refleter le contrat ;
- nouveaux tests unitaires (proposer `MGUI.Tests/Input/TrackerHandlerRegistrationTests.cs`) :
  - `CreateHandler` manuel (priorite null) laisse `Handlers` vide ; `ManualUpdate` livre toujours les evenements (press + release sur un owner factice) ;
  - `CreateHandler` auto (priorite non nulle) enregistre ; `UpdateHandlers` l'invoque ; `Unsubscribe` le retire et `Handlers` redevient vide ;
  - `Unsubscribe` sur un handler manuel jamais enregistre ne leve pas et neutralise le handler (plus d'evenements apres) ;
- regression obligatoire : `dotnet test ... --filter InputEnhanced` (ces tests utilisent des priorites 10/20 et dependent de l'enregistrement auto).

Criteres d'acceptation:

- plus aucun handler manuel dans `Handlers` apres creation ; livraison `ManualUpdate` intacte ;
- `InputEnhancedTests` au complet reste vert ;
- aucun test nouvellement rouge vs baseline ;
- perimetre de la garantie assume : cette tache supprime la racine COTE TRACKER ; les arbres contenant des textbox restent enracines via le desktop jusqu'a la tache 2 (ne pas promettre la collectabilite ici).

Commit recommande:

- `input: complete task 1 stop registering manual handlers in trackers`

Resultat:

- `MouseTracker.CreateHandler(T, double?, bool, bool, bool)` (MouseTracker.cs) et `KeyboardTracker.CreateHandler(T, double?, bool, bool)` (KeyboardTracker.cs) : le handler n'est ajoute a `_Handlers` que si `UpdatePriority.HasValue` est vrai ; la surcharge prenant l'enum `InputUpdatePriority` delegue vers la surcharge `double?` avec une valeur toujours non-nulle donc continue d'enregistrer normalement (verifie, aucun changement necessaire dessus) ;
- `Unsubscribe()` (`MouseHandler`/`KeyboardHandler`) inchange : `Tracker.RemoveHandler(this)` reste un `List.Remove` no-op sur un handler jamais ajoute, `IsValid = false` toujours applique ;
- commentaires XML de `Handlers` et de `CreateHandler` (les deux trackers) mis a jour pour enoncer le nouveau contrat (liste = handlers auto uniquement, motif GC root explicite) ;
- README.md (section `# Input Handling`, ~:602 et ~:610) mis a jour pour refleter le contrat (liste ne contient que les handlers auto ; `Unsubscribe` reste sur pour un handler manuel jamais enregistre) ;
- nouveaux tests dans `MGUI.Tests/Input/TrackerHandlerRegistrationTests.cs` (8 tests) : creation manuelle laisse `Handlers` vide + `ManualUpdate` livre bien press/release ; creation auto enregistre + `UpdateHandlers` invoque + `Unsubscribe` vide la liste ; `Unsubscribe` sur un handler manuel jamais enregistre ne leve pas et neutralise bien les evenements suivants ; couverture pour `MouseTracker` et `KeyboardTracker` ;
- validation : `dotnet build MGUI.Tests.csproj --no-restore` vert, `dotnet build MGUI.Samples.csproj --no-restore` vert ; `dotnet test --filter "FullyQualifiedName~TrackerHandlerRegistrationTests|InputEnhanced"` : 63/63 verts ; `dotnet test --filter "Focus|Input"` : 343/343 verts ; suite complete : 1185 tests, 1182 reussis, 3 echecs (exactement les 3 rouges de la baseline tache 0, aucun nouveau rouge), 0 ignores ;
- aucune API publique retiree (`Handlers`, `CreateHandler`, `Unsubscribe` preserves).

### ✅ 2. Notification de focus par element et migration MGTextBox / MGNumericUpDown (D2)

But:
supprimer les abonnements de constructeurs a `FocusedKeyboardHandlerChanged` en les remplacant par un virtuel notifie directement par le setter du desktop.

Travail attendu:

- ajouter `protected internal virtual void OnKeyboardFocusChanged(bool gained) { }` sur `MGElement` ;
- dans le setter `MGDesktop.FocusedKeyboardHandler` (MGDesktop.cs:971-1013), invoquer `Previous?.OnKeyboardFocusChanged(false)` puis `FocusedKeyboardHandler?.OnKeyboardFocusChanged(true)` juste avant `FocusedKeyboardHandlerChanged?.Invoke(...)` (~:1010) — surtout PAS avant l'affectation `State.FocusedKeyboardHandler = value` ni avant le swap `ReadonlyChanged` ;
- migrer `MGTextBox` et `MGNumericUpDown` selon D2 (suppression des abonnements de constructeurs, overrides avec appel a base obligatoire chez MGNumericUpDown) ;
- verifier que `MGPasswordBox`/`MGRichTextBox` (sous-classes de MGTextBox) n'ont pas besoin d'override propre (verifie : non, via virtuel `UpdateFormattedText`) ;
- nouveaux tests (proposer `MGUI.Tests/Focus/KeyboardFocusNotificationTests.cs`) :
  - gain/perte de focus d'un `MGTextBox` avec selection non vide declenche la mise a jour du texte formate (couleurs de selection focused/unfocused) ;
  - perte de focus d'un `MGNumericUpDown` avec texte en attente commit la valeur (`CommitPendingText`) ; gain de focus ne commit pas ;
  - transition vers null (clear de focus, mirroir de PropertyGridTests.cs:333) notifie l'ancien focus ;
  - anti-fuite : construire N textboxes puis verifier par reflexion que la liste d'invocation du delegate backing `FocusedKeyboardHandlerChanged` du desktop n'a pas grossi ;
- regression obligatoire : `--filter "PropertyGrid|StableDiagnosticId|Focus"` (PropertyGridTests et StableDiagnosticIdTests pilotent le setter prive par reflexion ; ils doivent rester verts).

Criteres d'acceptation:

- plus aucun `FocusedKeyboardHandlerChanged +=` dans un constructeur de `MGUI.Core` (les paires abonnement/desabonnement de MGGraphControls et MGPropertyGrid restent, elles sont correctes et hors perimetre) ;
- comportements commit-on-focus-loss et recoloration de selection pinnes par tests ;
- aucun test nouvellement rouge vs baseline.

Commit recommande:

- `focus: complete task 2 add element keyboard focus notification`

Resultat:

- `MGElement.OnKeyboardFocusChanged(bool gained)` ajoute (protected internal virtual, no-op par defaut), documente comme notification self-only sans risque de fuite (MGElement.cs, juste apres `OnThemeChanged`) ;
- `MGDesktop.FocusedKeyboardHandler` (setter prive, MGDesktop.cs) appelle desormais `Previous?.OnKeyboardFocusChanged(false)` puis `FocusedKeyboardHandler?.OnKeyboardFocusChanged(true)` juste apres `NPC(nameof(FocusedKeyboardHandler))` et juste avant `FocusedKeyboardHandlerChanged?.Invoke(...)` — donc strictement apres l'affectation `State.FocusedKeyboardHandler = value` et le swap `ReadonlyChanged`, ordre conforme a la decision D2 ;
- `MGTextBox` : lambda du constructeur (`GetDesktop().FocusedKeyboardHandlerChanged += ...`) supprimee ; override `protected internal override void OnKeyboardFocusChanged(bool gained)` ajoute juste apres `UpdateFormattedText`, appelle `UpdateFormattedText(true)` inconditionnellement (gained true ET false), reproduisant le comportement de l'ancienne lambda (`e.PreviousValue == this || e.NewValue == this`) ;
- `MGNumericUpDown` : abonnement du constructeur et methode privee `HandleFocusChanged` supprimes ; override `protected internal override void OnKeyboardFocusChanged(bool gained)` ajoute, appelle `base.OnKeyboardFocusChanged(gained)` PUIS `CommitPendingText(false)` si `!gained` uniquement — l'appel a base est fait dans tous les cas (recoloration de selection heritee de MGTextBox preservee) ;
- `MGPasswordBox`/`MGRichTextBox` verifies : aucun override necessaire, heritent du bon comportement via le virtuel `UpdateFormattedText` (MGRichTextBox) ou n'ont pas de logique de focus propre (MGPasswordBox) ;
- `MGGraphControls` (:869) et `MGPropertyGrid` (:1059) laisses inchanges : toujours abonnes a l'event public `FocusedKeyboardHandlerChanged`, confirme par grep post-migration (seules ces deux occurrences de `FocusedKeyboardHandlerChanged +=` restent dans MGUI.Core) ;
- nouveaux tests dans `MGUI.Tests/Focus/KeyboardFocusNotificationTests.cs` (6 tests, avec runtime de test IUIDesktopRuntime minimal dedie calque sur le patron `AdornerLiteTests.AdornerTestRuntime`) :
  - gain de focus d'un `MGTextBox` avec selection non vide change le texte formate (couleurs focused) ;
  - perte de focus d'un `MGTextBox` avec selection non vide change le texte formate (couleurs unfocused) ;
  - perte de focus d'un `MGNumericUpDown` (DecimalPlaces=2) avec texte en attente ("42") resynchronise le Text au format canonique ("42.00") — note : `Value` est deja mis a jour en live par `HandleTextChanged`/`SetValueCore(_, syncText:false)` a chaque frappe (ce n'est pas ce que `CommitPendingText` ajoute) ; l'effet observable propre a la perte de focus est le reformatage du `Text` via `SyncTextFromValue()` (`SetValueCore(_, syncText:true)`), c'est ce que le test pinne ;
  - gain de focus d'un `MGNumericUpDown` avec texte en attente NE resynchronise PAS le Text ;
  - subclasse de test `FocusProbeTextBox` (override public compte gained/lost) : verifie l'ordre gained/lost sur deux elements distincts et la notification de l'ancien focus lors d'une transition vers null (mirroir de `PropertyGridTests.cs:333`) ;
  - anti-fuite : construction de 25x2 `MGTextBox`/`MGNumericUpDown` puis verification par reflexion (`GetField("FocusedKeyboardHandlerChanged", NonPublic|Instance)`) que la longueur de la liste d'invocation du delegate backing du desktop n'a pas grossi ;
- validation : `dotnet build MGUI.Tests.csproj --no-restore` vert (0 erreur) ; `dotnet build MGUI.Samples.csproj --no-restore` vert (0 avertissement, 0 erreur) ; `dotnet test --filter "FullyQualifiedName~KeyboardFocusNotificationTests"` : 6/6 verts ; `dotnet test --filter "PropertyGrid|StableDiagnosticId|Focus"` : 309/309 verts (aucune regression sur les tests pilotant le setter prive par reflexion) ; suite complete : 1191 tests, 1188 reussis, 3 echecs (exactement les 3 rouges de la baseline tache 0, aucun nouveau rouge), 0 ignores ;
- aucune API publique retiree (`FocusedKeyboardHandlerChanged`, `Unsubscribe`, `Handlers`, `CreateHandler` intacts) ; `Unsubscribe`/trackers de la tache 1 non touches.

### ✅ 3. Supprimer l'abonnement Runtime.EndUpdate de MGListBox (D3)

But:
eliminer la troisieme fuite de la meme famille (element -> runtime-lifetime).

Travail attendu:

- lire les usages de `PressedItem` et `SpoofIsPressedWhileDrawingBackground` dans MGListBox pour confirmer que la reinitialisation en debut de l'update suivant est equivalente a la reinitialisation en `Runtime.EndUpdate` (la sequence par frame est Update -> Draw -> EndUpdate) ;
- supprimer la lambda du constructeur (MGListBox.cs:1506-1520) et deplacer la reinitialisation dans le cycle de vie du controle (ex. hook `OnBeginUpdate` deja disponible sur MGElement) ;
- si l'analyse revele une dependance reelle a l'instant EndUpdate (etat lu apres Draw), le documenter dans Resultat et choisir le point equivalent le plus proche DANS le cycle de vie de l'element — l'abonnement au runtime reste interdit ;
- test : si un test existant couvre l'etat presse du listbox, l'etendre ; sinon ajouter un test minimal verifiant que `PressedItem` ne persiste pas d'un tick a l'autre apres relachement.

Criteres d'acceptation:

- plus aucun `Runtime.EndUpdate +=` dans un constructeur d'element de `MGUI.Core` ;
- comportement visuel presse/spoof inchange (analyse consignee dans Resultat) ;
- aucun test nouvellement rouge vs baseline.

Commit recommande:

- `controls: complete task 3 remove listbox runtime endupdate subscription`

Resultat:

- analyse prealable confirmee : `PressedItem`/`IsPressedItemInvalidationPending` ne sont lus que par `LMBPressedInside`/`LMBReleasedInside`/`ReleasedOutside` (ecriture) et par la lambda de reset elle-meme (lecture pour reset) ; la sequence par frame etant Update -> Draw -> `Runtime.EndUpdate`, l'ancien reset en fin de tick (juste apres Draw) et un reset en debut du tick suivant (avant le prochain Draw) produisent le meme etat observable au moment de chaque Draw : le visuel presse survit au Draw du tick ou il a ete arme, et est efface avant le Draw suivant, dans les deux cas ;
- lambda du constructeur (`GetDesktop().Runtime.EndUpdate += ...`, MGListBox.cs:1506-1520) supprimee et remplacee par un abonnement a l'evenement `OnBeginUpdate` deja expose par `MGElement` (invoque en tout debut de `MGElement.Update`, avant le calcul du visual state et le Draw du meme tick) ; logique de reset (armement via `IsPressedItemInvalidationPending`, remise a null de `PressedItem`, `SpoofIsPressedWhileDrawingBackground = false`) inchangee, seul le point d'abonnement change ;
- ce nouvel abonnement est sur l'evenement de l'element lui-meme (`this.OnBeginUpdate`), pas sur le runtime/desktop : sa duree de vie est intrinsequement liee a celle du `MGListBox`, donc ne cree aucune racine externe — contrairement a l'abonnement au `Runtime.EndUpdate` du desktop qui enracinait la listbox pour la duree de vie du runtime ;
- aucune dependance reelle a l'instant precis de `EndUpdate` (post-Draw) n'a ete trouvee : aucun code ne lit `PressedItem`/`SpoofIsPressedWhileDrawingBackground` entre le Draw et l'ancien `EndUpdate` ;
- nouveaux tests dans `MGUI.Tests/Controls/MGListBoxPressedItemTests.cs` (2 tests, harness `IUIDesktopRuntime` de test dedie calque sur le patron `KeyboardFocusNotificationTests.FocusNotificationTestRuntime`) : `PressedItem_DoesNotPersist_AcrossUpdateTick_AfterReleaseIsPending` (arme `PressedItem`+`IsPressedItemInvalidationPending` via reflexion pour simuler l'etat laisse par un press+release, puis verifie qu'un `desktop.Update()` les reinitialise, prouvant que le reset ne depend plus de `Runtime.EndUpdate` — le runtime de test a un `EndUpdate` no-op) ; `PressedItem_NotReset_UntilInvalidationIsPending` (sans armer le flag d'invalidation, `PressedItem` survit a un `desktop.Update()`, comme avant) ;
- validation : `dotnet build MGUI.Tests.csproj --no-restore` vert (0 erreur) ; `dotnet build MGUI.Samples.csproj --no-restore` vert (0 avertissement, 0 erreur) ; `dotnet test --filter "FullyQualifiedName~MGListBoxPressedItemTests"` : 2/2 verts ; `dotnet test --filter "Focus|Input"` : 349/349 verts ; suite complete : 1193 tests, 1190 reussis, 3 echecs (exactement les 3 rouges de la baseline tache 0, aucun nouveau rouge), 0 ignores ;
- plus aucun `Runtime.EndUpdate +=` dans un constructeur d'element de `MGUI.Core` (grep post-modification confirme).

### ✅ 4. Cabler GameWindow.TextInput vers le puits clavier (D4)

But:
faire fonctionner AZERTY, touches mortes et IME par defaut sur le chemin `GameRenderHost`, sans toucher aux surfaces epinglees.

Travail attendu:

- creer l'interface opt-in `ITextInputHost` dans l'assembly Integration (nouveau fichier non linke dans LegacyRenderer) et l'implementer sur `GameRenderHost<T>` selon D4 (delegue nomme, `Detach` dans `Dispose`, message debug mis a jour, methode `internal OnTextInput` pour les tests) ;
- auto-cabler dans `MonoGameBackendBootstrap.Create` : `if (host is ITextInputHost t) t.AttachTextInputSink(renderer.Input.Keyboard);` ;
- verifier l'interaction avec la repetition de touche : `GameWindow.TextInput` refire en key-repeat ; analyser `KeyboardTracker.QueueTextInput`/`TryConsumeNativeTextInputString`/`CreateTextInputOnlyPressedEvents` (KeyboardTracker.cs:130-270) pour determiner le devenir des caracteres repetes en file (drainage par tick ? risque de mesattribution via le second pass "unmatched" ?) et pinner le comportement choisi par des tests dans `MGUI.Tests/Input/KeyboardTrackerTextInputTests.cs` (touche maintenue avec repetitions ; caractere natif different du fallback facon AZERTY ; deux touches imprimables le meme tick) ;
- tests du cablage (l'infrastructure existante est reflection/source-based, ne PAS instancier un vrai `Game`) :
  - test comportemental sur `GameRenderHost.OnTextInput` via un faux `IKeyboardTextInputSink` (un `TextInputEventArgs` est constructible : ctor `(char, Keys)` en 3.8.4.1) ;
  - test de contrat style `HostRuntimeContractTests` assurant que `GameRenderHost` implemente `ITextInputHost` et que `Dispose` detache ;
- ne toucher ni `IRenderHost`, ni `IUIDesktopRuntime`, ni les usages `Runtime.*` de MGDesktop.cs (tests d'architecture, voir D4).

Criteres d'acceptation:

- chemin `GameRenderHost` : caracteres natifs consommes par le tracker sans aucun code utilisateur ;
- `Dispose` desabonne TextInput ; comportement key-repeat documente et teste ;
- `HostRuntimeContractTests`, `RawInputSourceTests`, `BackendProjectSplitTests` : aucun test nouvellement rouge vs baseline ;
- `KeyboardTrackerTextInputTests` etendu et vert.

Commit recommande:

- `input: complete task 4 wire gamewindow textinput to keyboard sink`

Resultat:

- nouvelle interface opt-in `ITextInputHost` (`MGUI.MonoGame.Integration/Rendering/TextInputHost.cs`, namespace `MGUI.Shared.Rendering`, non listee dans les `<Compile Include Link=...>` de `MGUI.MonoGame.LegacyRenderer.csproj` donc non linkee dans LegacyRenderer, verifie par `dotnet build` + inspection du csproj) : `AttachTextInputSink(IKeyboardTextInputSink)` / `DetachTextInputSink()` ;
- `GameRenderHost<TObservableGame>` (`RenderHost.cs`) implemente desormais `IRenderHost, ITextInputHost, IDisposable` : `AttachTextInputSink` desabonne d'abord tout sink precedent puis stocke un delegue nomme `_onGameTextInput` et s'abonne a `Game.Window.TextInput` (`EventHandler<TextInputEventArgs>`, confirme par reflexion sur `MonoGame.Framework.DesktopGL 3.8.4.1` : `TextInputEventArgs` a un ctor `(char, Keys)` et les champs publics `Character`/`Key`) ; `DetachTextInputSink` se desabonne et neutralise le delegue (idempotent, safe si jamais attache) ; `Dispose()` appelle desormais `DetachTextInputSink()` en plus des 3 desabonnements existants, message debug mis a jour en "unsubscribed 4 event handlers" ; methode `internal void OnTextInput(TextInputEventArgs e) => _textInputSink?.QueueTextInput(e.Character, e.Key)` exposee pour la testabilite (assembly deja visible via `InternalsVisibleTo("MGUI.Tests")` dans `MGUI.MonoGame.Integration/Properties/AssemblyInfo.cs`) ;
- `MonoGameBackendBootstrap.Create` (`MonoGameBackendBootstrap.cs`) auto-cable juste apres construction du `MainRenderer` : `if (host is ITextInputHost textInputHost) textInputHost.AttachTextInputSink(renderer.Input.Keyboard);` — zero code utilisateur necessaire sur le chemin `GameRenderHost` ; `DelegateRenderHost` n'implemente pas `ITextInputHost` donc ce bloc est un no-op pour lui (chemin manuel documente en tache 5) ;
- analyse key-repeat (`KeyboardTracker.cs:137-277`) : `QueueTextInput` alimente `PendingTextInputEvents`, deplace vers `CurrentTextInputEvents` a chaque `Update`. `TryConsumeNativeTextInputString` n'est appele que pour les touches nouvellement pressees (absentes de `PreviousKeys`) — jamais pour une touche deja tenue. Si l'OS refait `GameWindow.TextInput` en repeat pendant que la touche reste enfoncee, le caractere est bien mis en file mais n'est jamais consomme (aucune touche "nouvellement pressee" ne le reclame ce tick), et `CurrentTextInputEvents` est entierement remplace au tick suivant (`CurrentTextInputEvents.Clear()` puis re-remplissage depuis `PendingTextInputEvents`, qui a lui-meme ete vide) : le caractere de repeat est donc silencieusement jete, sans fuite ni mesattribution vers une touche pressee plus tard — le repeat visuel reste gere par `KeyboardHandler.GetCurrentKeyRepeatEvent` qui reutilise `PrintableValue` de l'evenement de press initial, pas par une nouvelle consommation native. Risque de mesattribution du second pass "unmatched" de `TryConsumeNativeTextInputString` analyse et ecarte pour le cas 2-touches-meme-tick car chaque `KeyboardTextInputEvent` porte deja sa propre `Keys` et le premier pass matche par cle exacte avant le fallback "unmatched" ;
- comportement pinne par 4 nouveaux tests dans `MGUI.Tests/Input/KeyboardTrackerTextInputTests.cs` (ajoutes aux 4 existants) : caractere natif different du fallback US-QWERTY (style AZERTY, touche Q -> "a") ; deux touches imprimables pressees le meme tick chacune matchee a son propre caractere (pas de cross-consommation) ; caractere de repeat sur une touche deja tenue ne recree pas d'evenement `CurrentKeyPressedEvents` ; caractere de repeat jete ne fuite pas vers un press ulterieur sur une autre touche (verifie via le fallback correct "2" sur `Keys.D2`) ;
- nouveau fichier `MGUI.Tests/Input/GameRenderHostTextInputTests.cs` (5 tests) : `OnTextInput` forwarde caractere+touche au sink attache (instance `GameRenderHost<FakeObservableGame>` allouee via `FormatterServices.GetUninitializedObject` — meme patron que `ControlTemplateInfrastructureTests` — pour eviter d'instancier un vrai `Game`/`GraphicsDevice`, champ prive `_textInputSink` renseigne par reflexion) ; forwarding dans l'ordre pour plusieurs caracteres ; ne leve pas si aucun sink n'est attache ; test de contrat `GameRenderHost<>` implemente bien `ITextInputHost` (reflexion sur le type ouvert) ; test source-based (patron deja utilise ailleurs dans la suite pour epingler des corps de methode) verifiant que le corps de `Dispose()` dans `RenderHost.cs` appelle bien `DetachTextInputSink()` ;
- surfaces epinglees non touchees : `IRenderHost` inchangee (aucun nouveau membre, `RawInputSourceTests` toujours verts), `IUIDesktopRuntime` inchangee, aucun nouvel usage `Runtime.*` dans `MGDesktop.cs` (fichier non touche par cette tache), `Game1.cs`/`MiniGame.cs` non touches (reserve a la tache 5) ;
- validation : `dotnet build MGUI.Tests.csproj --no-restore` vert (0 erreur) ; `dotnet build MGUI.Samples.csproj --no-restore` vert (0 avertissement, 0 erreur) ; `dotnet build MGUI.MiniGame.csproj --no-restore` vert (0 avertissement, 0 erreur) ; `dotnet test --filter "FullyQualifiedName~KeyboardTrackerTextInputTests"` : 8/8 verts ; `dotnet test --filter "FullyQualifiedName~GameRenderHostTextInputTests"` : 5/5 verts ; `dotnet test --filter "Focus|Input"` : 358/358 verts ; `dotnet test --filter "HostRuntimeContractTests|RawInputSourceTests|BackendProjectSplitTests"` : 19/20 verts (le seul echec est `BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`, deja rouge en baseline tache 0, aucun rapport avec cette tache) ; suite complete : 1202 tests, 1199 reussis, 3 echecs (exactement les 3 rouges de la baseline tache 0 : `BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`, `ToolingHooksTests.UIToolingService_ExposesSnapshotAndPreviewHooks`, `ControlTemplateInfrastructureTests.ListBox_Rehydrates_Theme_Dependent_Row_Chrome_On_Theme_Refresh`), 0 nouveau rouge, 0 ignores ;
- aucune API publique retiree ; `IRenderHost`/`IUIDesktopRuntime` intacts.

### ✅ 5. Demonstration MiniGame et documentation du contrat texte natif

But:
couvrir le chemin `DelegateRenderHost` (cablage manuel) et documenter le contrat pour tous les types d'hotes.

Travail attendu:

- MiniGame : ajouter le cablage manuel pres de l'abonnement `Window.ClientSizeChanged` existant (MiniGame.cs:264), du style `Window.TextInput += (s, e) => _mguiBackend.Renderer.Input.Keyboard.QueueTextInput(e.Character, e.Key);` — LIRE d'abord les sous-chaines verbatim assertees par HostRuntimeContractTests.cs:95-110 et les preserver ;
- `Docs/monogame-host-integration-guide.md` : nouvelle section "Saisie de texte native (TextInput/IME)" + complement des blocs de code Option 1 (:47-66 — automatique via bootstrap) et Option 2 (:80-115 — cablage manuel obligatoire) ;
- `Docs/custom-render-backend-integration.md` : dans la section boucle (:64-86), ajouter que le runtime doit relayer la saisie native vers `IKeyboardTextInputSink` (sinon fallback US-QWERTY) ;
- README.md : une ligne dans le walkthrough Getting Started et un paragraphe dans "# Input Handling" (~:592-611) ;
- mentionner l'outil de diagnostic : variable d'environnement `CASA_MGUI_INPUT_PROBE` (KeyboardInputProbe) qui trace `source=native-text` vs `key-map`.

Criteres d'acceptation:

- MiniGame builde et les sous-chaines assertees par les tests d'architecture sont intactes ;
- les 3 documents + README decrivent le contrat (auto sur GameRenderHost, manuel sur DelegateRenderHost/custom runtime) ;
- aucun test nouvellement rouge vs baseline.

Commit recommande:

- `docs: complete task 5 document native text input wiring`

Resultat:

- `MGUI.MiniGame/MiniGame.cs` (`Initialize()`) : ajout du cablage manuel `Window.TextInput += (_, e) => _mguiRenderer.Input.Keyboard.QueueTextInput(e.Character, e.Key);` juste apres `Window.ClientSizeChanged += ...`, avec un commentaire expliquant pourquoi `DelegateRenderHost` (n'implemente pas `ITextInputHost`) exige ce cablage manuel contrairement a `GameRenderHost<T>` — le champ existant est `_mguiRenderer` (pas `_mguiBackend`, l'exemple du plan etait illustratif) ;
- sous-chaines verbatim assertees par `HostRuntimeContractTests.Repo_ShowsBothHistoricAndDelegateHostBootstrapPaths` (:95-110) verifiees intactes avant edition (`new DelegateRenderHost(`, `MonoGameBackendBootstrap.Create(`, `_desktop = new MGDesktop((IUIDesktopRuntime)_mguiRenderer);`, `_mguiHost.NotifyPreviewUpdate(gameTime.TotalGameTime);`, `_mguiHost.NotifyEndUpdate();`) — aucune de ces lignes touchee ;
- `Docs/monogame-host-integration-guide.md` : nouvelle section "Saisie de texte native (TextInput/IME)" (avant "## Choisir entre les 2") documentant les 2 sources (fallback US-QWERTY vs puits natif `IKeyboardTextInputSink.QueueTextInput`), le contrat par chemin d'hebergement (auto sur `GameRenderHost<T>` via `ITextInputHost`/bootstrap, manuel obligatoire sur `DelegateRenderHost`, manuel sur runtime custom) et l'outil de diagnostic `CASA_MGUI_INPUT_PROBE` ; complement de l'exemple Option 1 (mention de l'auto-cablage par le bootstrap) et de l'exemple Option 2 (ajout de la ligne de cablage manuel `Window.TextInput += ...` avec commentaire d'avertissement) ;
- `Docs/custom-render-backend-integration.md` : ajout d'un paragraphe "Saisie de texte native" dans la section "## Boucle d'integration minimale" (:64-86), expliquant que `Input.Keyboard.QueueTextInput(char, Keys)` doit etre alimente par la source native du moteur hote avant `desktop.Update()`, avec renvoi vers le guide MonoGame ;
- README.md : une ligne ajoutee dans le walkthrough "# Getting Started:" (sous la puce "Instantiate MGUI.Core.UI.MGDesktop") mentionnant le cablage manuel requis pour `DelegateRenderHost`/setups custom ; paragraphe ajoute sous la puce `KeyboardTracker` de la section "# Input Handling" (~:592) decrivant le contrat `IKeyboardTextInputSink`, le cablage auto vs manuel selon le host, et l'outil `CASA_MGUI_INPUT_PROBE` (`KeyboardInputProbe`, verifie present dans `MGUI.Shared/Input/Keyboard/KeyboardInputProbe.cs`, tracant `source=native-text` vs `source=key-map`) ;
- validation : `dotnet build MGUI.Tests.csproj --no-restore` vert (0 erreur) ; `dotnet build MGUI.Samples.csproj --no-restore` vert (0 avertissement, 0 erreur) ; `dotnet build MGUI.MiniGame.csproj --no-restore` vert (21 avertissements preexistants, 0 erreur) ; `dotnet test --filter "HostRuntimeContractTests|BackendProjectSplitTests|RawInputSourceTests"` : 19/20 verts (seul echec = `BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`, baseline tache 0) ; `dotnet test --filter "Focus|Input"` : 358/358 verts ; suite complete : 1202 tests, 1199 reussis, 3 echecs (exactement les 3 rouges de la baseline tache 0), 0 nouveau rouge, 0 ignores ;
- aucun code de production touche hors `MiniGame.cs` (le seul changement fonctionnel de cette tache) ; aucune API publique modifiee.

### ⚪ 6. Regression anti-fuite de bout en bout et rafraichissement du doc d'audit

But:
verrouiller le gain memoire global et remettre a jour l'audit d'architecture.

Travail attendu:

- test de collectabilite (proposer `MGUI.Tests/Input/InputLifetimeRegressionTests.cs`) : construire un desktop de test + fenetre contenant `MGTextBox`, `MGNumericUpDown`, `MGListBox` ; fermer via `TryCloseWindow` et retirer de `Desktop.Windows` ; capturer des `WeakReference` sur la fenetre et les controles dans une methode `[MethodImpl(MethodImplOptions.NoInlining)]` separee ; `GC.Collect()`/`WaitForPendingFinalizers` en boucle courte ; asserter que les references sont mortes ;
- si une racine residuelle est decouverte : l'identifier precisement (chemin de retention), la corriger si elle releve du perimetre fuites de ce plan (abonnement element->desktop/runtime/tracker), sinon la documenter dans Resultat avec le chemin de retention et adapter le test (asserter la mort des references corrigees, consigner l'exception) ;
- mettre a jour `Docs/event-handling-architecture.md` : faiblesses 4, 5, 6 marquees corrigees (avec renvoi vers ce plan), recommandation 1 remplacee par la decision D1 (non-enregistrement des handlers manuels, PAS d'Unsubscribe au teardown), recommandation 2 marquee faite, mention de la fuite MGListBox corrigee ;
- executer la suite complete et comparer a la baseline de la tache 0.

Criteres d'acceptation:

- le test de collectabilite passe (ou chaque racine residuelle hors perimetre est documentee avec son chemin de retention) ;
- `event-handling-architecture.md` ne contient plus de recommandation obsolete ;
- suite complete : aucun test nouvellement rouge vs baseline.

Commit recommande:

- `test: complete task 6 add leak regression and refresh audit doc`
