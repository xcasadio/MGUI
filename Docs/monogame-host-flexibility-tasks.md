# MonoGame Host Flexibility Tasks

## Objectif

Definir un plan d'execution pour un agent IA afin de rendre MGUI plus simple a integrer dans plusieurs jeux MonoGame ayant des hosts differents, sans lancer un faux chantier multi-moteur.

La cible de ce document est volontairement bornee:

- plusieurs jeux MonoGame doivent pouvoir utiliser MGUI avec des strategies de host differentes ;
- le pipeline de rendu commun doit rester partage ;
- les seams entre host, input, surface et runtime doivent devenir plus clairs et plus testables ;
- la migration doit rester progressive, avec de petites taches et un commit par tache.

Anti-objectifs explicites:

- ne pas rendre MGUI engine-agnostic dans ce chantier ;
- ne pas sortir tout le rendu dans un nouveau projet si cela n'est pas strictement necessaire ;
- ne pas rearchitecturer `DrawTransaction`, le pipeline clip, ou les paints hors besoin direct de host integration ;
- ne pas casser les APIs publiques sans justification claire et testee.

Ce document est destine a un agent IA implementeur.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit par tache terminee.
- Mettre a jour l'icone de statut dans le titre de la tache avant chaque action importante et avant chaque commit.
- Quand une tache commence, la passer en `🟡`.
- Quand une tache est terminee et validee, la passer en `✅`, puis faire immediatement le commit correspondant.
- Si une tache est bloquee, la passer en `⛔`, documenter le blocage juste sous la tache, puis s'arreter.
- Utiliser des commits non interactifs. Ne pas amender un commit existant.
- Si le worktree contient des changements hors perimetre, ne pas les revert ; commit uniquement les fichiers lies a la tache courante.
- Garder le scope MonoGame-first. Si une idee pousse vers un backend generique multi-moteur, la noter, mais ne pas l'implementer dans ce plan.
- Preferer de petits changements structurels, faciles a valider, plutot qu'un refactor global.
- Ajouter ou adapter des tests a chaque etape quand le contrat runtime ou l'integration host evolue.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale apres chaque tache

Utiliser une validation ciblee et bornee. Eviter les executions ouvertes non filtrees.

1. `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore`
2. `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter Architecture --logger "console;verbosity=minimal"`
4. Si la tache touche les samples ou l'integration runtime, executer en plus `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`.
5. Si la tache touche `MGDesktop`, ajouter un filtre cible supplementaire sur les tests lies au desktop ou au contrat runtime si disponible.

## Zones du code a auditer en priorite

- `MGUI.Shared/Rendering/MainRenderer.cs`
- `MGUI.Shared/Rendering/IUIRenderContext.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`
- `MGUI.Shared/Rendering/MainRenderer.cs` autour de `IRenderHost`, `GameRenderHost<TObservableGame>`, `IUISurface`, `IRawInputSource` et `ITextEngine`
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/UIView.cs`
- `MGUI.MiniGame/MiniGame.cs`
- `MGUI.Samples/Game1.cs`
- tests d'architecture existants autour du rendering runtime

## Cible architecturale retenue

La cible raisonnable pour ce chantier n'est pas un backend de rendu generique par moteur.

La cible est la suivante:

- `MainRenderer` reste le runtime MonoGame partage ;
- le host devient une seam plus explicite et plus facile a adapter a differents jeux MonoGame ;
- `MGDesktop` et les couches UI haut niveau dependent d'un contrat runtime plus etroit que le type concret `MainRenderer` quand cela apporte une vraie flexibilite ;
- les variantes d'integration MonoGame passent par des adapters ou factories de host, pas par des forks du pipeline de rendu ;
- les samples doivent montrer au moins 2 modes d'integration MonoGame raisonnablement differents.

## Contraintes de design

- Garder MonoGame comme hypothese de base du pipeline de rendu.
- Ne pas deplacer le clip pipeline, les primitives de draw ou les brushes dans un autre projet juste pour cette migration.
- Conserver la compatibilite du chemin existant `MainRenderer(new GameRenderHost<TGame>(...), ...)` tant qu'une tache ne demande pas explicitement mieux.
- Si une nouvelle interface est introduite, elle doit etre petite, motivee et consommee par au moins un call site reel.
- Eviter de dupliquer les memes responsabilites entre `IRenderHost`, `IUISurface`, `IRawInputSource` et une nouvelle abstraction.
- Les helpers ou factories doivent simplifier le wiring sans masquer le contrat runtime.

## Hypotheses de travail a verifier pendant l'implementation

- Le vrai point de variabilite prioritaire est le host MonoGame, pas le renderer lui-meme.
- `IRenderHost` et `GameRenderHost<TObservableGame>` couvrent deja une partie du besoin, mais pas forcement tous les modes d'integration MonoGame souhaitables.
- `MGDesktop` depend encore trop du type concret `MainRenderer` pour autoriser facilement des wrappers ou decorators de runtime.
- Une petite interface desktop-facing ou un adapter dedie peut suffire sans lancer un refactor multi-engine.
- Les samples actuels ne documentent pas encore assez clairement les options d'integration host.

## Ordre de commits attendu

1. `host: complete task 1 audit monogame host seams`
2. `test: complete task 2 add host runtime architecture coverage`
3. `runtime: complete task 3 narrow desktop runtime contract`
4. `host: complete task 4 add delegate monogame host adapter`
5. `runtime: complete task 5 route desktop through runtime contract`
6. `samples: complete task 6 add alternate host integration sample`
7. `docs: complete task 7 document monogame host integration`

## Taches

### ✅ 1. Auditer les seams host/runtime et figer le perimetre

But:
etablir une cible precise avant de toucher au wiring runtime.

Travail attendu:

- cartographier les responsabilites actuelles de `MainRenderer`, `IRenderHost`, `IUISurface`, `IRawInputSource` et `MGDesktop` ;
- lister ce qui releve vraiment du host, de la surface, de l'input et du runtime partage ;
- identifier les dependances haut niveau a `MainRenderer` qui sont purement de convenance et non de necessite ;
- figer les anti-objectifs pour eviter une derive vers un chantier multi-moteur.

Livrable:

- une courte matrice de responsabilites dans ce fichier ou un document associe ;
- une liste des call sites a migrer ou a laisser inchanges ;
- une frontiere claire entre ce chantier et un eventuel chantier backend multi-moteur futur.

Criteres d'acceptation:

- le scope est borne ;
- les seams runtime a ouvrir sont explicites ;
- la suite des taches peut se faire sans re-decider l'architecture a chaque etape.

Commit recommande:

- `host: complete task 1 audit monogame host seams`

Resultat:

- le runtime partage MonoGame reste clairement centre sur `MainRenderer`, `DrawTransaction`, `IUIRenderContext` et les primitives GPU ; ce chantier ne doit pas les generaliser ;
- la seam de variabilite utile est bien le host MonoGame, plus precisement `IRenderHost`, `IUISurface`, `IRawInputSource` et le point d'entree `MainRenderer(IRenderHost, ...)` ;
- `MGDesktop` depend aujourd'hui du type concret `MainRenderer` pour un sous-ensemble limite de besoins runtime ; c'est la meilleure cible pour un contrat runtime plus etroit ;
- `DrawTransaction`, `IUIRenderContext`, `View` et les primitives de rendering bas niveau doivent rester concretes sur `MainRenderer` dans ce chantier ;
- les samples montrent deja le chemin historique `GameRenderHost<TObservableGame>`, mais pas encore une voie alternative de host MonoGame ;
- certains call sites sample-specifiques, comme l'acces direct a `GameRenderHost<Game1>` pour atteindre `Game.Window`, peuvent rester concrets et documentes plutot qu'abstraits artificiellement.

Matrice de responsabilites retenue:

| Brique | Responsabilite retenue | Doit varier dans ce chantier | Notes |
| --- | --- | --- | --- |
| `IRenderHost` | fournir `GraphicsDevice`, viewport, services et signaux update | oui | seam principale cote hebergement MonoGame |
| `GameRenderHost<TObservableGame>` | adapter un `Game` MonoGame observable vers `IRenderHost` | oui, par ajout d'une variante | doit rester supporte tel quel |
| `IRawInputSource` | fournir les etats bruts souris/clavier | oui, mais sans refactor majeur | deja decouple de `IRenderHost` |
| `IUISurface` | decrire la surface de rendu UI | oui, mais stable | deja un seam utile pour plusieurs vues / RT |
| `MainRenderer` | runtime partage MonoGame: draw, views, input, fonts, assets, text engine | non | reste le coeur concret du pipeline |
| `MGDesktop` | orchestration UI haut niveau, focus, update, draw | oui, via un contrat runtime plus etroit | ne doit pas porter les details d'hebergement |
| `DrawTransaction` | execution GPU et cycle de draw | non | hors perimetre du chantier |
| `IUIRenderContext` | contrat de draw au niveau element | non | hors perimetre du chantier |

Call sites a migrer ou a laisser inchanges:

- a migrer:
  - `MGDesktop` constructeur et dependances runtime directes ;
  - `MGDesktop.ValidScreenBounds`, `TextEngine`, `FontManager`, `InputTracker`, `Update()` et `Draw()` vers un contrat runtime desktop-facing ;
  - wiring des samples et mini-game pour montrer au moins 2 modes d'hebergement MonoGame ;
- a laisser concrets dans ce chantier:
  - `DrawTransaction(MainRenderer, ...)` ;
  - `IUIRenderContext.Renderer` ;
  - `MainRenderer` comme type concret du pipeline clip, draw et assets ;
  - `View` / legacy rendering abstractions qui ne participent pas au wiring `MGDesktop` moderne ;
  - les usages sample-specifiques qui ont besoin d'un acces direct a `Game`, par exemple un cast vers `GameRenderHost<Game1>` pour atteindre `Game.Window`.

Frontiere explicite avec un futur chantier multi-moteur:

- ce chantier n'abstrait pas `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `Texture2D`, ni le pipeline `DrawTransaction` ;
- il n'introduit pas de backend generique de rendu ;
- il ouvre seulement une seam plus propre entre hebergement MonoGame et runtime UI haut niveau ;
- si un chantier multi-moteur existe un jour, il devra partir de cette seam, mais avec un perimetre bien plus large que celui-ci.

### ✅ 2. Ajouter une couverture de tests d'architecture pour le contrat host/runtime

But:
verrouiller le perimetre avant refactor.

Travail attendu:

- ajouter des tests qui epinglent les seams existants et les contraintes retenues ;
- verifier au minimum:
  - que `MainRenderer` continue a accepter un host explicite ;
  - que le chemin `GameRenderHost<TObservableGame>` reste supporte ;
  - que les dependances haut niveau retenues pour la suite sont localisees et testables ;
  - qu'aucun test n'encourage un faux backend multi-moteur a ce stade ;
- preparer des tests qui pourront valider l'introduction d'un contrat runtime plus etroit.

Livrable:

- nouveaux tests d'architecture cibles ;
- nommage de test qui decrit clairement le contrat voulu.

Criteres d'acceptation:

- les hypotheses de refactor sont verrouillees par des tests ;
- les tests restent rapides et bornee ;
- le refactor suivant pourra s'appuyer sur ces garde-fous.

Commit recommande:

- `test: complete task 2 add host runtime architecture coverage`

Resultat:

- une suite `HostRuntimeContractTests` verrouille maintenant le contrat de construction explicite de `MainRenderer` autour de `IRenderHost` ;
- les tests epinglent le fait que `GameRenderHost<TObservableGame>` reste la voie historique supportee pour un host MonoGame ;
- le couplage actuel de `MGDesktop` au type concret `MainRenderer` est desormais borne par un test qui liste explicitement les membres runtime consommes ;
- les samples sont verrouilles sur le chemin de bootstrap explicite `MainRenderer + GameRenderHost + MonoGameRawInputSource`, ce qui donne une baseline claire avant l'ajout d'une variante de host ;
- la validation de cette tache a ete faite avec un filtre cible host/runtime, et non avec tout `--filter Architecture`, car la suite d'architecture globale du repo contient deja des echecs hors perimetre de ce chantier.

### ✅ 3. Introduire un contrat runtime etroit cote desktop

But:
reduire le couplage direct de `MGDesktop` au type concret `MainRenderer` quand cette dependance n'apporte rien.

Travail attendu:

- definir une interface petite et motivee, orientee usage desktop, exposee par `MainRenderer` ;
- y placer uniquement ce que `MGDesktop` et `UIView` consomment vraiment ;
- ne pas y dupliquer les responsabilites deja portees par `IRenderHost` ou `IUIRenderContext` ;
- conserver un chemin de compatibilite simple pour le code existant.

Livrable:

- nouvelle interface runtime ciblee ;
- `MainRenderer` l'implemente ;
- tests d'architecture ou de compilation qui verrouillent le contrat.

Criteres d'acceptation:

- l'interface est petite et comprehensible ;
- elle n'est pas un faux backend generique ;
- au moins un consommateur reel peut ensuite dependre de cette interface.

Commit recommande:

- `runtime: complete task 3 narrow desktop runtime contract`

Resultat:

- une interface `IUIDesktopRuntime` existe maintenant comme contrat desktop-facing minimal au-dessus de `MainRenderer` ;
- `MainRenderer` implemente ce contrat sans perdre son role de runtime concret MonoGame ;
- la surface publique du contrat est volontairement petite: input, fonts, surface, assets, text engine, `UpdateArgs` et `RegisterView` ;
- le contrat n'expose ni `GraphicsDevice`, ni `SpriteBatch`, ni `PrimitiveBatch`, ni `IRenderHost`, ce qui evite de glisser vers un faux backend generique ;
- `MGDesktop` n'utilise pas encore ce contrat a cette etape ; la tache 5 fera basculer le consommateur reel.

### ✅ 4. Ajouter un adapter de host MonoGame par delegation

But:
couvrir un mode d'integration MonoGame plus souple que `GameRenderHost<TObservableGame>` sans toucher au pipeline de draw.

Travail attendu:

- introduire un host ou adapter base sur delegation, callbacks ou services explicites ;
- permettre au minimum d'injecter:
  - `GraphicsDevice` ;
  - viewport courant ;
  - `IServiceProvider` ;
  - signaux `PreviewUpdate` et `EndUpdate` ;
- garder l'implementation `GameRenderHost<TObservableGame>` intacte et supportee.

Livrable:

- nouvelle implementation de host MonoGame ;
- tests cibles sur son contrat ;
- absence de regression sur le host historique.

Criteres d'acceptation:

- MGUI peut etre heberge sans obliger le code hote a passer par la seule forme `GameRenderHost<TObservableGame>` ;
- le runtime de rendu reste partage ;
- aucune duplication du pipeline de rendu n'est introduite.

Commit recommande:

- `host: complete task 4 add delegate monogame host adapter`

Resultat:

- `DelegateRenderHost` fournit maintenant une voie d'hebergement MonoGame plus souple que `GameRenderHost<TObservableGame>` ;
- l'adapter garde `GraphicsDevice` concret, un delegate de viewport, un `IServiceProvider` optionnel, et des notifications explicites `NotifyPreviewUpdate(...)` / `NotifyEndUpdate()` ;
- cette variante n'introduit aucune duplication du pipeline de rendu ni d'input ; elle reste un simple adaptateur `IRenderHost` ;
- les tests d'architecture epinglent l'existence de cette voie alternative sans pousser le chantier vers un faux backend multi-moteur.

### ✅ 5. Faire dependre `MGDesktop` du contrat runtime etroit

But:
consommer le nouveau seam cote UI haut niveau.

Travail attendu:

- remplacer les dependances directes a `MainRenderer` par le contrat introduit a la tache 3 dans `MGDesktop` et les call sites immediats quand cela reste simple ;
- verifier que l'update, les fonts, l'input, la surface et le text engine continuent de fonctionner sans changement comportemental ;
- ne pas ouvrir un refactor transversal si certains call sites doivent rester concrets temporairement ; documenter les cas laisses concrets.

Livrable:

- `MGDesktop` consomme majoritairement le contrat runtime etroit ;
- compatibilite preservee pour les usages existants ;
- tests et build verts.

Criteres d'acceptation:

- le desktop n'est plus inutilement verrouille au type concret ;
- le chemin existant continue a compiler et fonctionner ;
- la seam nouvellement ouverte a un usage reel.

Commit recommande:

- `runtime: complete task 5 route desktop through runtime contract`

Resultat:

- `MGDesktop` consomme maintenant `IUIDesktopRuntime` pour l'input, les fonts, la surface, les assets, le text engine, l'enregistrement des vues et `UpdateArgs` ;
- un constructeur `MGDesktop(IUIDesktopRuntime)` existe desormais comme seam principale cote UI haut niveau ;
- le constructeur historique `MGDesktop(MainRenderer)` est conserve et redirige vers le nouveau contrat ;
- la propriete legacy `Renderer` est preservee pour compatibilite, mais elle n'est plus le chemin principal du code interne ;
- le point qui reste concret volontairement est `Draw(float, DrawSettings)`, car `DrawTransaction` attend encore un `MainRenderer` et ce chantier n'ouvre pas ce refactor transversal.

### ✅ 6. Ajouter un sample ou chemin d'integration alternatif

But:
prouver que le chantier sert un besoin reel et pas seulement un nettoyage interne.

Travail attendu:

- ajouter un exemple ou adapter un sample existant pour montrer une integration MonoGame differente du chemin historique ;
- montrer clairement le wiring host, input et desktop ;
- garder l'exemple minimal et lisible.

Livrable:

- un sample ou mini-sample d'integration alternative ;
- eventuelle documentation inline tres courte sur les points de wiring.

Criteres d'acceptation:

- le repo montre au moins 2 facons raisonnables d'heberger MGUI dans MonoGame ;
- le pipeline de rendu reste commun ;
- le sample sert de reference de migration.

Commit recommande:

- `samples: complete task 6 add alternate host integration sample`

Resultat:

- le repo montre maintenant 2 integrations MonoGame distinctes:
  - `MGUI.Samples` conserve le chemin historique `GameRenderHost<TObservableGame>` ;
  - `MGUI.MiniGame` utilise desormais `DelegateRenderHost` comme voie alternative ;
- `MGUI.MiniGame` pilote explicitement `NotifyPreviewUpdate(...)` et `NotifyEndUpdate()` autour de sa boucle `Update`, ce qui rend le wiring host lisible et minimal ;
- ce choix evite de casser les samples qui ont encore des dependances directes a `GameRenderHost<Game1>` pour atteindre `Game.Window`.

### ⚪ 7. Documenter l'integration MonoGame et les chemins recommandes

But:
laisser un resultat exploitable par un humain apres le refactor.

Travail attendu:

- documenter les seams retenus ;
- expliquer quand utiliser `GameRenderHost<TObservableGame>` et quand utiliser l'adapter par delegation ;
- documenter ce qui reste volontairement concret ou MonoGame-specifique ;
- ajouter une note explicite indiquant que le support multi-moteur n'est pas traite par ce chantier.

Livrable:

- documentation ou guide de migration court ;
- references vers les samples mis a jour.

Criteres d'acceptation:

- un contributeur peut choisir un host MonoGame adapte sans relire tout le runtime ;
- les limites du chantier sont explicites ;
- la doc correspond au code reel et aux tests.

Commit recommande:

- `docs: complete task 7 document monogame host integration`