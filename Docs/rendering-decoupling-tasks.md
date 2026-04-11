# Rendering Decoupling Tasks

## Objectif

Definir un plan d'execution pour un agent IA afin de decoupler le rendu de MGUI du backend MonoGame actuel, sans casser le runtime existant ni ouvrir trop tot un faux chantier "tout engine-agnostic".

Point de depart retenu:

- le chantier `Docs/monogame-host-flexibility-tasks.md` est considere comme deja acquis ;
- `IUIDesktopRuntime` et `DelegateRenderHost` sont des prealables, pas des sujets a reouvrir sans besoin fort ;
- le vrai sujet ici est la separation entre coeur UI, contrats de rendu et backend concret MonoGame.

Le resultat cible est le suivant:

- `MGUI.Core` doit exprimer le layout, le theming, le clip logique, les formes et le draw haut niveau sans dependance directe au type concret `MainRenderer` ni a des APIs GPU MonoGame ;
- le backend MonoGame reste la premiere implementation complete, mais il doit se brancher derriere des contrats dedies et relativement petits ;
- un futur backend alternatif doit pouvoir etre branche sans reouvrir la logique metier des controles ;
- la migration doit rester progressive, avec de petites taches et un commit par tache.

Anti-objectifs explicites:

- ne pas reecrire tout MGUI autour de types mathematiques maison dans ce chantier ;
- ne pas melanger ce plan avec un chantier layout, input, focus ou style qui n'est pas requis par le decouplage du rendu ;
- ne pas introduire une abstraction 1:1 artificielle de `GraphicsDevice`, `SpriteBatch` ou `Texture2D` ;
- ne pas casser brutalement le chemin historique MonoGame tant que le backend decouple n'est pas pret ;
- ne pas deplacer des fichiers entre projets avant d'avoir fige les contrats a extraire.

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
- Conserver un etat compilable entre les taches ; introduire des adapters ou chemins de compatibilite temporaires si necessaire.
- Toute nouvelle abstraction doit etre implementee par MonoGame immediatement et consommee par au moins un call site reel.
- Ne pas renommer un projet entier juste pour "faire propre" tant que le contrat cible n'est pas stabilise.
- Ajouter ou adapter des tests a chaque etape quand une frontiere architecturale, un contrat de draw ou un chemin de bootstrap evolue.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Strategie generale

Le chantier est maintenant coupe en 4 phases:

- Phase 1: figer la frontiere. L'objectif est de cartographier les fuites MonoGame, verrouiller les limites du chantier et introduire les contrats backend-neutral minimums.
- Phase 2: basculer le coeur UI. L'objectif est de faire dependre `MGUI.Core` de contrats de rendu, d'images et de texte plus etroits que le backend MonoGame.
- Phase 3: isoler physiquement le backend. L'objectif est de sortir l'implementation MonoGame dans un backend explicite, puis mettre a jour les samples, la doc et les tests de stabilisation.
- Phase 4: sortir completement l'implementation de rendu de `MGUI.Shared` et `MGUI.Core`. L'objectif est de permettre a un moteur hote de dessiner les formes, le texte et les buffers avec ses propres outils, sans dependre des primitives MonoGame dans les contrats publics.

Les phases 1 a 3 ont ete menees en mode MonoGame-first pour stabiliser la frontiere. La phase 4 change explicitement de cible: MonoGame reste une implementation de reference, mais les contrats de rendu ne doivent plus etre modeles sur `GraphicsDevice`, `SpriteBatch`, `Texture2D`, `RenderTarget2D` ou d'autres details MonoGame.

## Validation minimale apres chaque tache

Utiliser une validation ciblee et bornee. Eviter les executions ouvertes non filtrees.

1. `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore`
2. `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
3. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
4. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "<filtre cible de la tache>" --logger "console;verbosity=minimal"`
5. Si la tache touche au bootstrap applicatif, a la structure de projets ou au backend MonoGame, executer en plus:
   - `dotnet build .\MGUI.MiniGame\MGUI.MiniGame.csproj --no-restore`
   - `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
6. Ne pas utiliser une suite de tests ouverte comme seule preuve de validation ; preferer un filtre explicite sur les tests ajoutes ou touches.
7. Si la suite `Architecture` globale contient des echecs hors perimetre, documenter le filtre cible utilise pour cette tache au lieu d'essayer de corriger des regressions non liees.

## Zones du code a auditer en priorite

- `MGUI.Shared/Rendering/MainRenderer.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`
- `MGUI.Shared/Rendering/IUIRenderContext.cs`
- `MGUI.Shared/Rendering/IUIDesktopRuntime.cs`
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/UIView.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/Brushes/`
- `MGUI.Core/UI/Shapes/`
- `MGUI.Shared/Text/`
- `MGUI.FontStashSharp/FontStashSharpTextEngine.cs`
- les usages de `Texture2D`, `RenderTarget2D`, `GraphicsDevice`, `SpriteBatch` et `MainRenderer` en dehors du backend de rendu lui-meme
- les tests d'architecture existants autour du rendering runtime, du clip pipeline et des rounded shapes

## Cible architecturale retenue

La cible de ce chantier n'est pas "chaque moteur expose un clone de `MainRenderer`".

La cible retenue est la suivante:

- `MGUI.Core` depend d'un petit ensemble de contrats de rendu, de texte et de ressources qui decrivent ce dont la UI a besoin, pas comment MonoGame l'execute ;
- le backend MonoGame reste l'implementation concrete du draw, du clip bas niveau, des batches GPU, des textures natives et du chargement effectif des assets ;
- `MainRenderer` devient un composant de backend MonoGame, plus un point d'ancrage impose au coeur UI ;
- `DrawTransaction` et les details GPU restent MonoGame-specifiques ; ils doivent etre derriere des contrats consommes par la UI, pas exposes directement au code coeur ;
- le split physique en projets doit suivre les contrats stables, pas les preceder.

Projection de structure cible a confirmer pendant la tache 1:

- `MGUI.Core`: logique UI, layout, theming, clip logique, formes, controles, et usage des contrats de rendu ;
- `MGUI.Rendering.Abstractions` ou nom equivalent: contrats backend-neutral pour runtime, contexte de draw, texte et ressources ;
- `MGUI.MonoGame` ou redecoupage equivalent: implementation concrete de `MainRenderer`, `DrawTransaction`, hosts MonoGame, surfaces, assets et texte MonoGame ;
- `MGUI.FontStashSharp`: integration texte MonoGame specialisee au-dessus du backend decouple.

Les noms finaux de projets peuvent etre ajustes pendant la tache 1, mais la frontiere de responsabilites ne doit pas bouger ensuite sans justification forte.

## Contraintes de design

- Reutiliser autant que possible les seams deja introduites par le chantier host flexibility.
- S'appuyer sur le clip pipeline logique et sur l'architecture rounded-shapes actuelle ; ne pas les reouvrir sans besoin direct de frontiere backend.
- Si un besoin UI peut etre exprime par une capacite fonctionnelle simple, preferer cette capacite a l'exposition d'un objet backend complet.
- Si une capacite reste explicitement MonoGame-specifique et n'a pas encore d'equivalent portable raisonnable, la garder dans le backend et documenter l'exception.
- Toute interface nouvelle doit etre suffisamment petite pour etre lisible en une seule lecture.
- Une etape intermediaire acceptable peut conserver une compatibilite descendante temporaire, mais il faut ajouter un test qui epingle le caractere transitoire de ce pont.
- Les abstractions doivent etre guidees par les usages reels du code coeur, pas par une liste theorique de primitives de rendu.

## Hypotheses de travail a verifier pendant l'implementation

- Le plus gros couplage structurel n'est plus le host MonoGame, mais l'exposition de `MainRenderer`, `DrawTransaction` et de ressources GPU natives dans le code coeur.
- `IUIRenderContext` est probablement la seam la plus critique a resserrer pour sortir le rendu du coeur UI.
- Les chemins image/brush/text dans `MGUI.Core` contiennent davantage de fuites backend que le clip pipeline logique.
- Le split physique en projets doit arriver apres la stabilisation des contrats ; le faire trop tot augmenterait fortement le bruit de migration.
- Il est acceptable, dans ce chantier, de tolerer provisoirement certains types de valeur MonoGame deja omnipresents tant qu'ils ne reintroduisent pas une dependance directe au backend de rendu.

## Ordre de commits attendu

1. `rendering: complete task 1 audit decoupling seams and target split`
2. `test: complete task 2 add rendering boundary architecture coverage`
3. `contracts: complete task 3 add backend neutral rendering contracts`
4. `runtime: complete task 4 route desktop and view bootstrap through abstractions`
5. `context: complete task 5 split ui render context from monogame transaction`
6. `assets: complete task 6 add backend neutral image resource contracts`
7. `text: complete task 7 split text measurement from backend text draw`
8. `backend: complete task 8 isolate monogame renderer implementation`
9. `core: complete task 9 migrate remaining core rendering leaks`
10. `samples: complete task 10 adopt explicit monogame backend bootstrap`
11. `docs: complete task 11 document rendering backend architecture`
12. `rendering: complete task 12 audit remaining backend owned render seams`
13. `test: complete task 13 pin zero-implementation rendering architecture`
14. `contracts: complete task 14 add engine owned render backend contracts`
15. `core: complete task 15 migrate shared and core draw paths to backend contracts`
16. `backend: complete task 16 redefine monogame renderer as backend adapter`
17. `backend: complete task 17 move concrete render sources fully out of shared`
18. `test: complete task 18 prove engine owned shapes text and buffers`
19. `docs: complete task 19 document custom render backend integration`

## Taches

## Phase 1 - Figer la frontiere de decouplage

### ✅ 1. Auditer les fuites de rendu et figer la separation cible

But:
etablir une carte precise du couplage actuel avant de bouger des contrats ou des projets.

Travail attendu:

- auditer les dependances directes de `MGUI.Core` vers `MainRenderer`, `DrawTransaction`, `IUIRenderContext.Renderer`, `GraphicsDevice`, `Texture2D`, `RenderTarget2D`, `SpriteBatch`, `ContentManager` et les types de texte backend ;
- separer les dependances en 3 groupes:
  - a sortir du coeur dans ce chantier ;
  - tolerables provisoirement avec justification ;
  - deja correctement isolees ;
- figer la structure cible des projets et namespaces, au moins en termes de responsabilites, meme si les noms exacts doivent etre ajustes ;
- lister les call sites a migrer en premier et ceux qu'il vaut mieux laisser stables jusqu'a la fin ;
- expliciter ce qui est hors perimetre pour eviter de reouvrir l'input, le layout ou les mathematiques.

Livrable:

- une matrice de fuites backend dans ce fichier ou un document associe ;
- une liste priorisee des seams a ouvrir ;
- une decision claire sur la cible `Core / Abstractions / Backend MonoGame`.

Criteres d'acceptation:

- le chantier est borne ;
- les fuites a traiter d'abord sont explicites ;
- l'ordre des taches suivantes ne depend plus de choix architecturaux implicites.

Commit recommande:

- `rendering: complete task 1 audit decoupling seams and target split`

Resultat:

- le couplage le plus structurant a traiter en premier est bien la dependance du coeur UI a `DrawTransaction` et au contrat `IUIRenderContext` tel qu'il expose encore `MainRenderer`, `GraphicsDevice` et `RenderTarget2D` ;
- le deuxieme bloc de fuite concerne les ressources image: `MGTextureData`, `MGResources`, `MGImage`, les textured brushes et certains runs de texte exposent encore `Texture2D` dans les APIs coeur ;
- le contrat texte est deja presque abstrait cote mesure/layout grace a `ITextEngine`, mais il fuit encore le backend MonoGame via `SpriteBatch` dans `DrawText(...)` ;
- `MGUI.Shared` melange aujourd'hui des contrats relativement generiques et l'implementation MonoGame concrete ; la separation physique doit donc passer par une extraction de contrats avant tout deplacement massif de fichiers ;
- les seams deja saines ou suffisamment bornees pour ce chantier sont `IRenderHost`, `IRawInputSource`, `IUIDesktopRuntime` et le clip pipeline logique ; elles servent de precedents de migration, pas de sujets a reouvrir ;
- `MGUI.Core.csproj` reference encore directement les packages MonoGame, ce qui confirme que le split physique final devra etre precede d'une reduction des fuites de type dans le code coeur ;
- les types de valeur MonoGame omnipresents comme `Color`, `Point`, `Rectangle`, `Vector2` et `Matrix` sont toleres provisoirement dans ce chantier ; ils ne sont pas la cible prioritaire tant qu'ils ne rebranchent pas le coeur sur un backend concret de draw.

Matrice des fuites backend retenue:

| Zone | Fuites observees | Statut retenu | Notes |
| --- | --- | --- | --- |
| `IUIRenderContext` / `DrawTransaction` | `Renderer`, `GD`, `SetRenderTargetTemporary(RenderTarget2D, ...)`, draw helpers directement MonoGame | a sortir du coeur dans ce chantier | seam prioritaire ; conditionne la plupart des draw paths coeur |
| Brushes et symbol drawing (`MGSolidFillBrush`, `MGTextureFillBrush`, `UISymbolDrawing`, `DrawTransactionBoxShapeExtensions`) | casts vers `DrawTransaction` et appels directs aux primitives de draw | a sortir du coeur dans ce chantier | peut passer par un contexte de draw capacitaire plus etroit |
| Ressources image (`MGTextureData`, `MGResources`, `MGImage`, textured brushes, text runs images) | `Texture2D` expose dans les APIs et stocke dans les structures coeur | a sortir du coeur dans ce chantier | deuxieme seam prioritaire apres le contexte de draw |
| Texte (`ITextEngine`, `RendererAssetProvider.FontManager`, `SpriteFontTextEngine`) | mesure deja abstraite, draw encore couple a `SpriteBatch` | a sortir du coeur dans ce chantier | separer mesure/layout et draw backend |
| Runtime haut niveau (`MGDesktop`, `UIView`) | compat legacy sur `MainRenderer`, creation de `DrawTransaction` encore concrete | a sortir du coeur dans ce chantier | peut s'appuyer sur `IUIDesktopRuntime` comme point de depart |
| Surfaces / hosts (`IRenderHost`, `IUISurface`, `IUIDesktopRuntime`, `DelegateRenderHost`) | `IUISurface` fuit encore `RenderTarget2D`, le reste est deja borne | tolerable provisoirement avec justification | `IUISurface` sera probablement absorbe ou etendu avec les nouveaux contrats |
| Clip pipeline logique | `ClipDefinition`, `ClipStrategyResolver`, `ClipManager` cote logique deja en place | deja correctement isolee | ne pas reouvrir hors besoin direct du nouveau contexte de draw |
| Valeurs mathematiques / couleurs MonoGame | `Color`, `Rectangle`, `Point`, `Vector2`, `Matrix` | tolerable provisoirement avec justification | hors perimetre tant que ces types ne portent pas de ressource backend |
| GPU backend concret (`MainRenderer`, `DrawTransaction`, `RenderTargetPool`, `BackBufferSurface`, helpers GPU`) | `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `RenderTarget2D`, `ContentManager` | deja correctement isole du point de vue backend, mais pas encore du point de vue projet | cible du split physique final, pas de l'extraction initiale |

Seams a ouvrir en priorite:

1. remplacer l'usage coeur de `IUIRenderContext` par un contrat de draw orientee capacites, sans `MainRenderer` ni `GraphicsDevice` en surface ;
2. introduire un handle backend-neutral pour les images et textures UI, puis migrer `MGTextureData`, `MGResources` et les brushes textures ;
3. separer le contrat texte en deux niveaux: mesure/layout backend-neutral et draw texte backend ;
4. stabiliser un petit projet de contrats partage par `MGUI.Core` et le backend MonoGame ;
5. deplacer ensuite seulement l'implementation MonoGame concrete vers un backend explicite.

Call sites a migrer en premier:

- `MGUI.Core/UI/Brushes/Fill Brushes/MGSolidFillBrush.cs` et `MGUI.Core/UI/Brushes/Fill Brushes/MGTextureFillBrush.cs` ;
- `MGUI.Core/UI/MGTextureData.cs` et `MGUI.Core/UI/MGResources.cs` ;
- `MGUI.Core/UI/MGImage.cs` ;
- `MGUI.Core/UI/MGDesktop.cs` et `MGUI.Core/UI/UIView.cs` pour le bootstrap haut niveau ;
- `MGUI.Shared/Rendering/IUIRenderContext.cs` comme point d'entree des nouvelles abstractions.

Call sites a laisser stables jusqu'a la fin du chantier:

- `MGUI.Shared/Rendering/MainRenderer.cs` pour les details GPU, la gestion de batches et les caches de textures runtime ;
- `MGUI.Shared/Rendering/DrawTransaction.cs` comme executant concret tant que l'adapter de contexte n'est pas en place ;
- `MGUI.Shared/Rendering/Clipping/*` hors ajustements strictement necessaires d'integration au nouveau contexte ;
- `MGUI.Shared/Rendering/DelegateRenderHost.cs` et `GameRenderHost<TObservableGame>` ;
- `MGUI.FontStashSharp` tant que le contrat texte final n'est pas stabilise.

Projection de split retenue:

- `MGUI.Core`: logique UI, theming, layout, clip logique, formes, controles et consommation exclusive des contrats backend-neutral ;
- `MGUI.Rendering.Abstractions`: runtime desktop-facing, contexte de draw coeur, contrats texte backend-neutral et handles de ressources image/draw ;
- `MGUI.MonoGame`: `MainRenderer`, `DrawTransaction`, hosts, surfaces, pools de render targets, asset provider MonoGame et implementations texte MonoGame ;
- `MGUI.FontStashSharp`: implementation specialisee du contrat texte backend au-dessus de `MGUI.MonoGame` et des contrats communs.

Frontiere explicite avec les chantiers hors perimetre:

- ne pas toucher a l'architecture focus/input au-dela du maintien des seams deja introduites ;
- ne pas reecrire les formes, le clip pipeline logique ni les rounded shapes en dehors de l'adaptation minimale au nouveau contexte de draw ;
- ne pas tenter de sortir des types mathematiques MonoGame du coeur dans cette sequence de taches ;
- ne pas creer de second backend de rendu dans ce chantier.

Validation:

- `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` : succes, avec avertissements XML existants hors perimetre ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes, avec avertissements nullability existants hors perimetre ;
- pas de filtre `dotnet test` ajoute a cette tache, car elle ne modifie encore aucun contrat runtime ni aucune suite de tests.

### ✅ 2. Ajouter une couverture de tests d'architecture pour la frontiere de rendu

But:
verrouiller la cible avant refactor.

Travail attendu:

- ajouter des tests qui epinglent les dependances actuellement autorisees et les dependances qui doivent disparaitre du coeur UI ;
- verifier au minimum:
  - qu'un projet de contrats backend-neutral ne reference pas MonoGame ;
  - que `MGUI.Core` ne prend pas de nouvelles dependances directes vers `MainRenderer`, `DrawTransaction` ou des APIs GPU ;
  - que les seams transitoires assumees sont nommees et testees ;
  - que le chantier host flexibility reste intact ;
- preparer des tests qui pourront etre resserres au fur et a mesure des taches suivantes.

Livrable:

- nouveaux tests d'architecture cibles ;
- une nomenclature de tests qui distingue clairement limite cible, compatibilite temporaire et dette restante.

Criteres d'acceptation:

- les hypotheses de migration sont verrouillees par des tests ;
- les tests restent rapides et filtres ;
- les prochains refactors peuvent s'appuyer sur des garde-fous precis.

Commit recommande:

- `test: complete task 2 add rendering boundary architecture coverage`

Resultat:

- une suite `RenderingBoundaryArchitectureTests` borne maintenant les references directes de `MGUI.Core` a `MainRenderer`, `DrawTransaction`, `Texture2D` et `RenderTarget2D` par fichiers explicitement autorises ;
- la suite ajoute aussi un garde-fou pour la future extraction `MGUI.Rendering.Abstractions`: si le projet apparait, il ne devra referencer ni `MonoGame` ni `Microsoft.Xna.Framework` ;
- un test dedie verrouille l'absence de references code directes a `SpriteBatch` et `ContentManager` dans `MGUI.Core`, ce qui evite de laisser ces fuites reapparaitre pendant la migration ;
- ces tests ne normalisent pas la dette existante ; ils la bornent pour permettre aux taches 3 a 9 de la reduire de maniere incrementale et testee.

Validation:

- `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` : succes, avec avertissements XML existants hors perimetre ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes, avec avertissements existants hors perimetre ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderingBoundaryArchitectureTests --logger "console;verbosity=minimal"` : succes, 6 tests passes.

### ✅ 3. Introduire un projet de contrats backend-neutral

But:
creer le lieu cible des abstractions de rendu sans encore deplacer l'implementation MonoGame.

Travail attendu:

- ajouter un nouveau projet de contrats, par exemple `MGUI.Rendering.Abstractions`, sans reference MonoGame ;
- y deplacer ou y extraire les contrats qui doivent etre partages entre `MGUI.Core` et un backend:
  - runtime desktop-facing ;
  - surface de draw haut niveau ;
  - contrats de texte et de ressources qui sont deja reellement generiques ;
- garder les interfaces petites et guidees par les usages reels ;
- mettre a jour les references de projets sans lancer encore une migration de masse des implementations.

Livrable:

- nouveau projet de contrats compilable ;
- premiers contrats extraits hors du backend MonoGame ;
- references de solution a jour.

Criteres d'acceptation:

- le projet de contrats compile sans package ou namespace MonoGame ;
- `MGUI.Core` peut referencer ces contrats ;
- aucune implementation concrete de rendu n'a encore ete forcee a bouger trop tot.

Commit recommande:

- `contracts: complete task 3 add backend neutral rendering contracts`

Resultat:

- un nouveau projet `MGUI.Rendering.Abstractions` existe maintenant dans la solution, sans package ni namespace MonoGame ;
- le premier noyau de contrats deja reellement neutres a ete extrait physiquement dans ce projet, en conservant les namespaces publics existants pour eviter une migration artificielle des call sites ;
- ce noyau comprend `CustomFontStyles`, `FontSpec` et `GlyphMetrics`, qui sont maintenant references depuis `MGUI.Shared`, `MGUI.Core`, `MGUI.FontStashSharp` et `MGUI.Tests` via le nouveau projet ;
- `MGUI.Shared` n'heberge donc plus exclusivement tous les contrats texte ; il commence a redevenir un backend/runtime concret plutot qu'un fourre-tout de types partages ;
- `IUIDesktopRuntime`, `IUISurface`, `IUIAssetProvider` et `ITextEngine` restent encore dans `MGUI.Shared` a cette etape, parce que leurs signatures trainent toujours des types ou responsabilites MonoGame ; leur extraction fonctionnelle sera traitee par les taches 4 a 7 plutot que forcee trop tot.

Validation:

- `dotnet build .\MGUI.Rendering.Abstractions\MGUI.Rendering.Abstractions.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderingBoundaryArchitectureTests --logger "console;verbosity=minimal"` : succes, 6 tests passes.

## Phase 2 - Basculer le coeur UI sur des contrats de rendu

### ✅ 4. Router le bootstrap desktop et view via les abstractions

But:
faire dependre les couches haut niveau de la UI de contrats de runtime plutot que du backend concret.

Travail attendu:

- faire dependre `MGDesktop`, `UIView` et les chemins de bootstrap haut niveau des contrats extraits, pas du type concret `MainRenderer` ;
- conserver des chemins de compatibilite temporaires si necessaire, mais les rendre explicites et testes ;
- verifier que le chantier host flexibility se greffe naturellement sur cette nouvelle frontiere ;
- limiter le refactor a la couche runtime/bootstrap, sans encore reouvrir tout le draw elementaire.

Livrable:

- `MGDesktop` et ses points d'entree haut niveau branches sur les abstractions ;
- tests de compilation et d'architecture couvrant le nouveau bootstrap ;
- compatibilite temporaire documentee si une ancienne surcharge doit rester exposee.

Criteres d'acceptation:

- le coeur UI haut niveau n'a plus besoin de connaitre `MainRenderer` pour son wiring nominal ;
- le chemin MonoGame existant continue a fonctionner ;
- les seams introduites lors du chantier host flexibility restent pertinentes.

Commit recommande:

- `runtime: complete task 4 route desktop and view bootstrap through abstractions`

Resultat:

- `IUIDesktopRuntime` expose maintenant `CreateDrawTransaction(...)`, ce qui permet au bootstrap haut niveau de creer son contexte de draw sans connaitre `MainRenderer` ;
- `MainRenderer` implemente ce nouveau point d'entree comme factory du `DrawTransaction` concret ;
- `MGDesktop.Draw(float, DrawSettings)` passe desormais par `Runtime.CreateDrawTransaction(...)` au lieu d'instancier `DrawTransaction` a partir de `Renderer` ;
- le chemin nominal de bootstrap haut niveau n'a donc plus besoin du type concret `MainRenderer` ; la propriete `Renderer` et le constructeur legacy restent presents uniquement comme chemins de compatibilite explicites ;
- `UIView` reste encore branche sur `IUISurface` et `DrawTransaction`, ce qui est acceptable a cette etape ; le vrai resserrement du contexte de rendu est reserve a la tache 5.

Validation:

- `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` : succes, avec avertissements XML existants hors perimetre ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes, avec avertissements existants hors perimetre ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter HostRuntimeContractTests --logger "console;verbosity=minimal"` : succes, 7 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter UIViewTests --logger "console;verbosity=minimal"` : succes, 5 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderingBoundaryArchitectureTests --logger "console;verbosity=minimal"` : succes, 6 tests passes.

### ✅ 5. Separer le contexte de rendu UI du `DrawTransaction` MonoGame

But:
faire en sorte que les elements UI expriment leurs besoins de draw sans recevoir directement le backend MonoGame.

Travail attendu:

- redefinir ou completer le contrat de contexte de rendu consomme par les elements UI pour qu'il expose des capacites utiles plutot qu'un acces direct a `Renderer`, `GraphicsDevice` ou `DrawTransaction` ;
- introduire les adapters MonoGame necessaires pour que `DrawTransaction` reste l'executant concret ;
- migrer les premiers call sites coeur qui n'ont besoin que de capacites de draw haut niveau ;
- documenter explicitement les usages qui ne peuvent pas encore sortir de `DrawTransaction` a cette etape.

Livrable:

- nouveau contrat ou contrat etendu de contexte de rendu ;
- adapter MonoGame au-dessus de `DrawTransaction` ;
- premiers elements/coeurs branches sur ce contexte plus etroit.

Criteres d'acceptation:

- le contrat UI de draw n'expose plus `MainRenderer` comme dependance de principe ;
- les capacites exposees correspondent a des usages concrets du coeur UI ;
- la dette restante vers `DrawTransaction` est listee explicitement.

Commit recommande:

- `context: complete task 5 split ui render context from monogame transaction`

Resultat:

- un nouveau contrat `IUIDrawContext` existe maintenant comme surface minimale de capacites de draw pour le coeur UI ;
- `IUIRenderContext` herite de ce contrat plus etroit, ce qui permet de conserver la compatibilite actuelle tout en offrant un point de migration hors `MainRenderer` et hors `DrawTransaction` concret ;
- `DrawTransaction` reste l'executant MonoGame, mais il satisfait maintenant ce contrat de capacites sans exposer sa nature concrete aux call sites migres ;
- les extensions de formes arrondies ciblent desormais `IUIDrawContext` au lieu de `DrawTransaction`, ce qui retire une dependance concrete d'un point chaud du pipeline UI ;
- un premier groupe de call sites coeur a ete bascule sur ce contrat: `MGSolidFillBrush`, `MGTextureFillBrush`, `MGProgressBarGradientBrush`, `MGUniformBorderBrush` et `UISymbolDrawing` ;
- la dette restante est documentee et assumee pour les taches suivantes: `MGElement`, `MGDesktop`, `MGWindow`, `MGRatingControl`, plusieurs controles de docking, des helpers de texte et d'autres chemins `DA.DT.*` restent encore concrets a cette etape.

Validation:

- `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` : succes, avec avertissements XML existants hors perimetre ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes, avec avertissements existants hors perimetre ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderContextTests --logger "console;verbosity=minimal"` : succes, 8 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RoundedRectangleRenderingApiTests --logger "console;verbosity=minimal"` : succes, 1 test passe.

### ✅ 6. Introduire des contrats backend-neutral pour les images et ressources de draw

But:
retirer du coeur UI les references directes aux ressources GPU natives quand elles servent seulement a decrire du contenu UI.

Travail attendu:

- identifier les usages de `Texture2D`, `RenderTarget2D` ou ressources similaires dans les controles, brushes, templates et resources XAML ;
- introduire des contrats ou handles opaques pour exprimer une image, une ressource de draw ou une source visuelle sans imposer le type natif MonoGame ;
- adapter les chemins de chargement, de stockage et d'utilisation de ces ressources ;
- conserver, si necessaire, un escape hatch explicitement MonoGame mais le repousser dans le backend.

Livrable:

- nouveaux contrats de ressource image/draw ;
- premiers chemins `Image` ou `Brush` migres ;
- tests d'architecture et de compilation sur les usages coeur.

Criteres d'acceptation:

- les APIs coeur qui manipulent juste du contenu visuel n'exigent plus `Texture2D` ou `RenderTarget2D` en surface ;
- le backend MonoGame reste capable de materialiser ces ressources ;
- les exceptions restantes sont bornees et documentees.

Commit recommande:

- `assets: complete task 6 add backend neutral image resource contracts`

Resultat:

- un handle image backend-neutral `IUIImageResource` a ete introduit dans `MGUI.Rendering.Abstractions`, puis implemente immediatement cote MonoGame via `MonoGameImageResource` ;
- `IUIAssetProvider` expose maintenant `LoadImage(...)` et `TryLoadImage(...)`, tandis que `LoadTexture(...)` et `TryLoadTexture(...)` restent disponibles comme pont de compatibilite borne ;
- `MGTextureData` transporte desormais un `IUIImageResource` comme payload principal, ajoute des surcharges de draw via `IUIDrawContext`, et conserve un constructeur/propriete `Texture2D` legacy pour les call sites pas encore migres ;
- `MGResources`, `MGImage` et le bootstrap de ressources par defaut dans `MGDesktop` consomment maintenant le chemin image opaque au lieu d'exiger `Texture2D` en surface pour les cas simples de contenu visuel ;
- le garde-fou `RenderingBoundaryArchitectureTests` a ete resserre pour refleter la reduction reelle des fuites: `Texture2D` n'apparait plus directement dans `MGDesktop` ni `MGResources`, et plusieurs helpers deja bascules vers `IUIDrawContext` sont sortis de l'allowlist `DrawTransaction` ;
- les exceptions restantes sont volontairement bornees a cette etape: `MGTexturedBorderBrush`, `MGNineSliceFillBrush`, les ponts de compatibilite `MGTextureData.Texture` et le constructeur `MGImage(Texture2D, ...)` restent MonoGame-specifiques en attendant les migrations des taches suivantes.

Validation:

- `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` : succes, avec avertissements XML existants hors perimetre ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes, avec avertissements existants hors perimetre ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter AssetProviderTests --logger "console;verbosity=minimal"` : succes, 4 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderContextTests --logger "console;verbosity=minimal"` : succes, 8 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderingBoundaryArchitectureTests --logger "console;verbosity=minimal"` : succes, 6 tests passes.

### ✅ 7. Separer la mesure de texte du draw texte backend

But:
eviter que le contrat texte du coeur UI soit pilote par les details du rendu texte MonoGame.

Travail attendu:

- auditer `ITextEngine`, les chemins SpriteFont et FontStashSharp, ainsi que les usages coeur qui ont seulement besoin de mesure, layout ou shaping ;
- separer les responsabilites mesure/layout et draw effectif si elles sont encore melangees ;
- faire en sorte que `MGUI.Core` consomme un contrat texte backend-neutral ;
- adapter les implementations MonoGame existantes pour rester compatibles.

Livrable:

- contrats texte clarifies ;
- implementations MonoGame adaptees ;
- validation ciblee sur les controles et tests touches.

Criteres d'acceptation:

- le coeur UI depend d'un contrat texte motive par ses besoins reels ;
- SpriteFont et FontStashSharp continuent a fonctionner via le backend MonoGame ;
- la dette restante vers des details texte backend est visible et limitee.

Commit recommande:

- `text: complete task 7 split text measurement from backend text draw`

Resultat:

- le contrat texte a ete coupe en deux niveaux explicites: `ITextMeasurementEngine` porte desormais la resolution et la mesure backend-neutral, tandis que `IMonoGameTextRenderer` porte le draw texte concret via `SpriteBatch` ;
- `ITextEngine` reste disponible comme interface composite de compatibilite pour les implementations MonoGame existantes (`SpriteFontTextEngine`, `FontStashSharpTextEngine`), mais `MGUI.Core` ne depend plus directement de ce contrat composite ;
- `IUIDesktopRuntime.TextEngine`, `MGDesktop.TextEngine`, `MGElement.TextEngineOverride`, `MGTextBlock`, `TextRenderInfo` et `MGRotatedTextLabel` consomment maintenant le contrat de mesure seul pour la resolution de fontes, la mesure de lignes, le wrapping et le caret layout ;
- `DrawTransaction` garde la responsabilite du draw texte backend et consomme explicitement le contrat `IMonoGameTextRenderer`, ce qui retire `SpriteBatch` de la surface visible par le coeur UI sans casser le chemin MonoGame ;
- `MainRenderer` accepte toujours des engines texte interchangeables, mais le setter verifie maintenant qu'un engine assigne au runtime implemente aussi `IMonoGameTextRenderer`, afin de garantir que le backend MonoGame reste renderable ;
- les garde-fous d'architecture ont ete etendus pour epingler la nouvelle frontiere: `MGUI.Core` ne doit plus referencer `ITextEngine`, et le contrat runtime expose desormais `ITextMeasurementEngine` plutot que le contrat composite.

Validation:

- `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.FontStashSharp\MGUI.FontStashSharp.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` : succes, avec avertissements XML existants hors perimetre ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes, avec avertissements existants hors perimetre ;
- `dotnet build .\MGUI.MiniGame\MGUI.MiniGame.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore` : succes, avec avertissement `net6.0-windows` existant hors perimetre ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter HostRuntimeContractTests --logger "console;verbosity=minimal"` : succes, 8 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderingBoundaryArchitectureTests --logger "console;verbosity=minimal"` : succes, 7 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter FSSMeasureDrawConsistencyTests --logger "console;verbosity=minimal"` : succes, 13 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter MeasureSelfOverrideConsistencyTests --logger "console;verbosity=minimal"` : succes, 10 tests passes.

## Phase 3 - Isoler physiquement le backend MonoGame

### ✅ 8. Isoler l'implementation MonoGame dans un backend explicite

But:
faire correspondre la structure physique des projets a la frontiere architecturale maintenant stabilisee.

Travail attendu:

- deplacer ou reassigner `MainRenderer`, `DrawTransaction`, les hosts MonoGame, les surfaces MonoGame, les implementations assets et texte MonoGame dans un projet/backend explicite ;
- mettre a jour les references de solution et les namespaces sans reouvrir les contrats stabilises ;
- decider ce qui reste dans `MGUI.Shared`, ce qui devient `MGUI.MonoGame`, et ce qui doit disparaitre ;
- conserver un chemin de compilation incremental pendant la migration.

Livrable:

- backend MonoGame explicite dans la solution ;
- references de projets nettoyees ;
- tests et builds a jour sur la nouvelle structure.

Criteres d'acceptation:

- le code MonoGame concret est rassemble derriere une frontiere claire ;
- `MGUI.Core` ne depend plus du projet backend concret pour ses contrats ;
- la solution reste buildable sans gros refactor annexe.

Commit recommande:

- `backend: complete task 8 isolate monogame renderer implementation`

Resultat:

- un projet backend explicite `MGUI.MonoGame` a ete ajoute a la solution et devient le nouveau proprietaire de compilation des implementations MonoGame concretes suivantes: `MainRenderer`, `DrawTransaction`, `GameRenderHost`, `DelegateRenderHost`, `BackBufferSurface`, `RenderTargetPool`, `ClipManager`, `MonoGameRawInputSource`, `MonoGameImageResource` et `SpriteFontTextEngine` ;
- `MGUI.Shared.csproj` retire maintenant ces fichiers de son compile set, ce qui rend le split physique verifiable au niveau du projet sans casser les namespaces publics existants ;
- `RendererAssetProvider` a ete extrait du fichier de contrat `IUIAssetProvider` et rehausse dans `MGUI.MonoGame`, ce qui separe enfin le contrat d'assets du chargeur MonoGame concret ;
- pour permettre ce split sans casser le coeur UI, un contrat partage `IUIDrawTransaction` a ete introduit, `IUIDesktopRuntime.CreateDrawTransaction(...)` et `IUIView.Draw(...)` ont ete retapes sur ce contrat, et `IUIRenderContext.Renderer` cible maintenant `IUIDesktopRuntime` au lieu de `MainRenderer` ;
- les render-loop args (`DrawBaseArgs`, `UpdateBaseArgs` et events associes) ont ete separes du type `View` pour rester dans `MGUI.Shared`, tandis que la classe `View` elle-meme est maintenant compilee par le backend MonoGame ;
- `MGUI.Core` reference temporairement `MGUI.MonoGame` car quelques surfaces coeur exposent encore `DrawTransaction` ou d'autres types backend en public ; cette dette residuelle est volontairement laissee a la tache 9.

Validation:

- `dotnet restore .\MGUI.sln` : succes, necessaire apres l'ajout du projet backend ;
- `dotnet restore .\MGUI.FontStashSharp\MGUI.FontStashSharp.csproj` : succes, pour rafraichir les assets du graphe apres ajustement des references ;
- `dotnet build .\MGUI.Shared\MGUI.Shared.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.MonoGame\MGUI.MonoGame.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` : succes, avec avertissements XML existants hors perimetre ;
- `dotnet build .\MGUI.FontStashSharp\MGUI.FontStashSharp.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes, avec avertissements existants hors perimetre ;
- `dotnet build .\MGUI.MiniGame\MGUI.MiniGame.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore` : succes, avec avertissement `net6.0-windows` existant hors perimetre ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter BackendProjectSplitTests --logger "console;verbosity=minimal"` : succes, 4 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter HostRuntimeContractTests --logger "console;verbosity=minimal"` : succes, 8 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RawInputSourceTests --logger "console;verbosity=minimal"` : succes, 3 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter SurfaceAbstractionTests --logger "console;verbosity=minimal"` : succes, 3 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter UIViewTests --logger "console;verbosity=minimal"` : succes, 5 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderContextTests --logger "console;verbosity=minimal"` : succes, 8 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderingBoundaryArchitectureTests --logger "console;verbosity=minimal"` : succes, 7 tests passes.

### ✅ 9. Migrer les fuites de rendu restantes dans le coeur UI

But:
fermer les derniers points d'entree backend qui resteraient dans `MGUI.Core` apres l'isolation principale.

Travail attendu:

- auditer les controles, brushes, helpers et extensions qui exposent encore `MainRenderer`, `DrawTransaction` ou des ressources natives MonoGame ;
- migrer ces usages vers les abstractions introduites quand c'est raisonnable ;
- si une exception doit rester, la rendre explicite, localisee et testee ;
- resserrer les tests d'architecture en supprimant les exemptions devenues inutiles.

Livrable:

- dernieres migrations coeur effectuees ;
- matrice d'exceptions restante, idealement tres courte ;
- tests d'architecture mis a jour pour refleter la vraie frontiere finale.

Criteres d'acceptation:

- le coeur UI n'expose plus de dependance backend inattendue ;
- les exceptions restantes sont justifiees et bornees ;
- la frontiere finale est verifiable automatiquement.

Commit recommande:

- `core: complete task 9 migrate remaining core rendering leaks`

Resultat:

- `MGCheckBox.DrawCheckMark(...)` ne depend plus de `DrawTransaction` et consomme directement `IUIDrawContext`, ce qui supprime un point d'entree backend inutile dans les controles coeur ;
- `MGRatingControl` ne depend plus du helper statique `DrawTransaction.GetCircleVertices(...)` ; la generation de geometrie cercle necessaire au controle est maintenant locale au coeur UI ;
- `MGNineSliceFillBrush` ne reconstruit plus ses sous-textures a partir d'un `Texture2D` concret ; il derive maintenant ses slices depuis `IUIImageResource`, ce qui retire ce brush de la matrice des fuites backend ;
- `MGTexturedBorderBrush` continue d'exposer un constructeur legacy `Texture2D` pour compatibilite descendante, mais son chemin de draw courant passe maintenant par `IUIImageResource` et les overloads image du contrat `IUIDrawContext` ;
- le vieux code mort de window scaling base sur `RenderTarget2D` a ete supprime de `MGWindow`, ce qui elimine totalement cette fuite backend du coeur UI ;
- les tests d'architecture epinglent maintenant la frontiere residuelle exacte suivante dans `MGUI.Core`:
  - `MainRenderer`: `UI/MGDesktop.cs` seulement, pour le pont legacy d'acces au renderer concret ;
  - `DrawTransaction`: `UI/MGResources.cs` et `UI/MGTextureData.cs` seulement, pour des overloads de compatibilite ;
  - `Texture2D`: `UI/MGImage.cs`, `UI/MGTextureData.cs` et `UI/Brushes/Border Brushes/MGTexturedBorderBrush.cs` seulement, pour les constructeurs/proprietes de compatibilite encore exposes ;
  - `RenderTarget2D`: plus aucune occurrence dans le coeur UI.

Validation:

- `dotnet build .\MGUI.MonoGame\MGUI.MonoGame.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore` : succes, avec avertissements XML existants hors perimetre ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes, avec avertissements nullability existants hors perimetre ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderingBoundaryArchitectureTests --logger "console;verbosity=minimal"` : succes, 7 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter RenderContextTests --logger "console;verbosity=minimal"` : succes, 8 tests passes.

### ✅ 10. Faire adopter aux apps de demo un bootstrap backend explicite

But:
montrer comment un consommateur branche MGUI via un backend concret sans dependre des details internes du coeur.

Travail attendu:

- mettre a jour `MGUI.MiniGame` et `MGUI.Samples` pour qu'ils referencent explicitement le backend MonoGame cible ;
- verifier que les deux chemins de host MonoGame deja supportes restent possibles au-dessus du backend decouple ;
- ajouter, si necessaire, un helper de composition ou une factory legere pour rendre le wiring plus lisible ;
- conserver la demonstration de `GameRenderHost<TObservableGame>` et `DelegateRenderHost`.

Livrable:

- bootstrap samples/minigame mis a jour ;
- validation de build sur les apps de demo ;
- eventuel helper de composition minimal.

Criteres d'acceptation:

- les demos montrent clairement comment consommer le backend MonoGame ;
- le coeur UI n'est plus le point d'entree concret du rendu ;
- les modes de host MonoGame restent supportes et documentes.

Commit recommande:

- `samples: complete task 10 adopt explicit monogame backend bootstrap`

Resultat:

- un point d'entree de composition backend explicite a ete ajoute dans `MGUI.MonoGame` sous le namespace `MGUI.Backend.MonoGame` via `MonoGameBackendBootstrap.Create(...)`, qui construit le couple `Host + MainRenderer` sans repasser par un bootstrap concret cache dans `MGUI.Core` ;
- `MGUI.MiniGame` reference maintenant `MGUI.MonoGame` directement et compose son runtime UI via `MonoGameBackendBootstrap.Create(new DelegateRenderHost(...))`, puis cree `MGDesktop` depuis `IUIDesktopRuntime` ;
- `MGUI.Samples` reference maintenant `MGUI.MonoGame` directement et montre le chemin alternatif `GameRenderHost<Game1>` via le meme bootstrap backend explicite ;
- les deux modes de host MonoGame restent donc visibles et supportes dans les demos, tout en faisant apparaitre le backend comme dependance assumee au niveau des projets consommateurs ;
- un test d'architecture epingle desormais a la fois la reference directe des apps de demo vers `MGUI.MonoGame` et l'existence du point d'entree `MonoGameBackendBootstrap`.

Validation:

- `dotnet build .\MGUI.MonoGame\MGUI.MonoGame.csproj --no-restore` : succes ;
- `dotnet build .\MGUI.MiniGame\MGUI.MiniGame.csproj --no-restore` : succes, avec avertissements XML existants hors perimetre dans `MGUI.Core` ;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore` : succes, avec avertissement `net6.0-windows` existant hors perimetre ;
- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore` : succes, avec avertissements nullability existants hors perimetre ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter BackendProjectSplitTests --logger "console;verbosity=minimal"` : succes, 6 tests passes ;
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter HostRuntimeContractTests --logger "console;verbosity=minimal"` : succes, 8 tests passes.

### ✅ 11. Documenter l'architecture finale et les limites restantes

But:
laisser un etat final comprehensible pour un humain et pour un prochain agent.

Travail attendu:

- mettre a jour les guides d'integration et les docs d'architecture pour decrire la nouvelle separation `Core / Contracts / MonoGame backend` ;
- documenter les points d'extension attendus pour un futur backend alternatif ;
- lister les limites restantes du chantier, notamment les types MonoGame encore toleres si c'est assume ;
- aligner la doc avec les choix reels, pas avec une cible ideale non implementee.

Livrable:

- doc d'architecture et guide d'integration mis a jour ;
- note de migration pour les consommateurs existants si le wiring a change ;
- recapitulatif des dettes residuelles assumees.

Criteres d'acceptation:

- un nouveau contributeur comprend ou commence le coeur UI et ou commence le backend MonoGame ;
- la doc ne promet pas plus que ce qui est reellement livre ;
- les prochaines etapes possibles sont visibles sans ambiguite.

Commit recommande:

- `docs: complete task 11 document rendering backend architecture`

Resultat:

- un nouveau document `Docs/rendering-backend-architecture.md` decrit maintenant la separation finale `MGUI.Rendering.Abstractions / MGUI.Shared / MGUI.Core / MGUI.MonoGame / MGUI.FontStashSharp`, le flux d'integration recommande, les seams stabilisees et les dettes residuelles assumees ;
- `Docs/monogame-host-integration-guide.md` a ete mis a jour pour refleter le wiring actuellement recommande via `MGUI.Backend.MonoGame.MonoGameBackendBootstrap`, avec les deux chemins `GameRenderHost<TObservableGame>` et `DelegateRenderHost` toujours documentes ;
- le `README.md` pointe maintenant vers la doc d'architecture/backend et son bloc "Getting Started" demande explicitement une reference directe a `MGUI.MonoGame`, puis montre un exemple de bootstrap aligne sur les samples reels du repo ;
- la note de migration consommateur est maintenant explicite: ajouter la reference `MGUI.MonoGame`, utiliser le namespace `MGUI.Backend.MonoGame`, preferer `MonoGameBackendBootstrap.Create(...)` et ne garder `Desktop.Renderer` que pour des usages legacy cibles.

Validation:

- tache documentaire uniquement ; aucun build ni test runtime supplementaire n'etait necessaire ;
- verification du repo: `Docs/rendering-backend-architecture.md` existe bien ;
- verification du repo: `README.md` et `Docs/monogame-host-integration-guide.md` referencent tous deux `MonoGameBackendBootstrap.Create(...)` et sont donc aligns sur le point d'entree backend livre a la tache 10.

## Phase 4 - Sortir completement le backend de rendu de Shared et Core

Cette phase repart du constat suivant:

- `MGUI.Core` ne depend deja plus directement du renderer concret, mais les contrats de draw publics exposent encore des types et des modes de travail MonoGame ;
- `MGUI.Shared` contient encore des interfaces et helpers de rendu qui parlent de `Texture2D`, `RenderTarget2D`, `GraphicsDevice`, `SpriteBatch`, `SpriteEffects` ou `ContentManager` ;
- un moteur hote ne peut donc pas encore prendre totalement en charge le dessin des formes, du texte et des buffers avec ses propres outils ;
- les fichiers de source concrets du renderer MonoGame vivent encore physiquement sous `MGUI.Shared`, meme s'ils sont compiles par `MGUI.MonoGame`.

La cible explicite de cette phase est plus forte que la precedente:

- aucune implementation concrete de rendu ne doit rester dans `MGUI.Shared` ou `MGUI.Core` ;
- `MGUI.Shared` et `MGUI.Rendering.Abstractions` ne doivent exposer que des contrats backend-neutral ;
- la gestion du dessin des formes, du texte, des images, du clipping et des buffers offscreen doit appartenir au backend ou au moteur hote ;
- MonoGame doit devenir une implementation concrete parmi d'autres, pas le moule des interfaces publiques.

Hotspots a auditer en priorite pour cette phase:

- `MGUI.Shared/Rendering/IUIDrawContext.cs`
- `MGUI.Shared/Rendering/IUIRenderContext.cs`
- `MGUI.Shared/Rendering/IUISurface.cs`
- `MGUI.Shared/Assets/IUIAssetProvider.cs`
- `MGUI.Shared/Text/Engines/IMonoGameTextRenderer.cs`
- `MGUI.Shared/Rendering/MainRenderer.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`
- `MGUI.Shared/Rendering/View.cs`
- `MGUI.Shared/Rendering/DrawSettings.cs`
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/MGResources.cs`
- `MGUI.Core/UI/MGTextureData.cs`

Contraintes supplementaires pour cette phase:

- ne pas introduire une pseudo-abstraction qui ne ferait que renommer `SpriteBatch`, `GraphicsDevice` ou `RenderTarget2D` ;
- ne pas laisser des fichiers d'implementation concrete sous `MGUI.Shared` une fois la phase terminee ;
- si une compatibilite legacy doit subsister, la confiner au backend MonoGame et la couvrir par un test d'architecture explicite ;
- si aucun second moteur reel n'est disponible dans le repo, prouver la portabilite avec un backend de test ou un harness minimal qui exerce formes, texte et buffers.

### ✅ 12. Re-auditer les seams de rendu encore possedes par le backend

But:
figer la nouvelle cible complete d'extraction du backend de rendu apres les phases 1 a 3.

Travail attendu:

- inventorier tous les contrats, helpers et sources de `MGUI.Shared` et `MGUI.Core` qui exposent encore des primitives MonoGame ou des comportements de rendu concrets ;
- separer clairement ce qui doit devenir backend-neutral, ce qui doit migrer vers `MGUI.MonoGame`, et ce qui peut rester simple helper partage sans contenir d'implementation de draw ;
- documenter les blocages exacts qui empechent aujourd'hui un moteur hote de gerer lui-meme les formes, le texte et les buffers ;
- figer la cible architecturale finale: `Shared/Core` sans implementation concrete de rendu, backend possedant totalement le draw et les buffers.

Livrable:

- matrice des fuites residuelles de phase 4 ;
- liste priorisee des seams a reouvrir ;
- decision explicite sur le futur role de `MainRenderer`, `DrawTransaction`, `View`, `IUISurface` et `IMonoGameTextRenderer`.

Criteres d'acceptation:

- il n'y a plus d'ambiguite sur ce qui doit quitter `Shared` et `Core` ;
- les types MonoGame a eliminer des contrats publics sont listes explicitement ;
- l'ordre des taches 13 a 19 est justifie par cette cartographie.

Commit recommande:

- `rendering: complete task 12 audit remaining backend owned render seams`

Resultat:

- le blocage principal n'est plus seulement la presence de `MainRenderer` dans certains chemins legacy ; ce sont surtout les contrats publics de `Shared` qui restent dessines autour de primitives MonoGame et empechent un moteur hote de posseder formes, texte et buffers sans emuler l'API MonoGame ;
- `IUIDrawContext`, `IUIRenderContext`, `IUISurface`, `IUIAssetProvider`, `IMonoGameTextRenderer`, `IRenderHost` et `DrawSettings` forment encore un noyau de contrats backend-shaped qui doit etre reouvert avant toute extraction physique definitive ;
- plusieurs fichiers de rendu concret sont encore physiquement sous `MGUI.Shared` et compilent comme implementation MonoGame cachee: `MainRenderer`, `DrawTransaction`, `View`, `BackBufferSurface`, `RenderTargetPool`, `ContentUtils`, `TextureUtils`, `RenderUtils`, `MonoGameImageResource` ;
- `MGUI.Core` a encore des call sites qui observent ou exigent des details GPU concrets: scissor via `GraphicsDevice`, constructeurs `Texture2D` dans `MGImage` et `MGTexturedBorderBrush`, acces `Texture2D` dans `MGTextureData`, et commentaires/API qui continuent de modeler la ressource image comme une texture MonoGame ;
- la cible finale retenue pour la phase 4 est la suivante: `MGUI.Core` et `MGUI.Shared` ne portent plus aucune implementation concrete de rendu ni aucun contrat public qui exige un objet GPU MonoGame ; `MGUI.MonoGame` devient le seul proprietaire des draw commands concretes, du texte backend, des surfaces offscreen et des pools de buffers ;
- l'ordre des taches 13 a 19 est confirme: d'abord epingler l'etat cible par tests d'architecture, ensuite redessiner les contrats, puis migrer les call sites coeur, redefinir `MainRenderer`, et seulement apres deplacer physiquement les sources concretes.

Types MonoGame a eliminer des contrats publics de phase 4:

- `GraphicsDevice`
- `SpriteBatch`
- `PrimitiveBatch`
- `Texture2D`
- `RenderTarget2D`
- `ContentManager`
- `SpriteEffects`
- `BlendState`
- `SamplerState`
- `RasterizerState`
- `DepthStencilState`
- `Effect`

Matrice des fuites residuelles de phase 4:

| Zone | Fuites observees | Cible retenue | Impact sur un moteur hote |
| --- | --- | --- | --- |
| Contrats de draw publics | `IUIDrawContext` expose encore `Texture2D` et `SpriteEffects`; `IUIRenderContext` expose `GraphicsDevice`, `SetRenderTargetTemporary(RenderTarget2D, ...)`; `IUISurface` retourne `RenderTarget2D` ; `DrawSettings` encapsule `BlendState`, `SamplerState`, `DepthStencilState`, `RasterizerState`, `Effect` et sait faire `SpriteBatch.Begin(...)` | a redefinir en contrats backend-neutral dans `MGUI.Rendering.Abstractions` | aujourd'hui un moteur hote doit fournir ou mimer des objets MonoGame au lieu de simplement implementer des capacites de draw |
| Assets et images | `IUIAssetProvider` expose `ContentManager` et `Texture2D`; `MonoGameImageResource` et `UIImageResourceExtensions.GetTexture2D()` sont encore sous `MGUI.Shared`; `MGTextureData` garde un constructeur `Texture2D` et une propriete `Texture`; `MGImage` et `MGTexturedBorderBrush` gardent des constructeurs `Texture2D` | garder seulement des handles/contracts image neutres dans `Shared/Core`; pousser les adaptateurs `Texture2D` dans `MGUI.MonoGame` | le moteur hote ne peut pas fournir son propre format image sans retomber sur `Texture2D` |
| Texte | `IMonoGameTextRenderer.DrawText(...)` exige `SpriteBatch` et `SpriteEffects`; `MainRenderer.TextEngine` impose encore qu'un moteur de mesure implemente aussi ce contrat de draw MonoGame | separer strictement mesure/layout et draw texte backend ; garder le draw texte MonoGame seulement dans `MGUI.MonoGame` | impossible de brancher un moteur texte proprietaire sans emuler le pipeline `SpriteBatch` |
| Runtime et bootstrap | `MainRenderer` porte `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `ContentManager`, `FontManager`, les vues et la creation concrete de `DrawTransaction`; `IRenderHost` et `BackBufferSurface` sont encore modeles sur `GraphicsDevice` et le backbuffer MonoGame ; `View` depend directement de `MainRenderer` et expose `GraphicsDevice` | rabattre `Shared/Core` sur `IUIDesktopRuntime` + contrats backend-neutral ; releguer `MainRenderer`, `IRenderHost`, `View` et les surfaces MonoGame au backend | un backend alternatif doit aujourd'hui copier la forme de `MainRenderer` plutot que simplement implementer un runtime |
| Buffers et surfaces offscreen | `RenderTargetPool` gere directement `RenderTarget2D`; `RenderUtils.CreateRenderTarget(...)` cree des render targets MonoGame; `IUISurface` et `SetRenderTargetTemporary(...)` exposent le buffer concret au coeur | introduire des handles de surface/buffer opaques et laisser le backend posseder l'allocation/recyclage | un moteur hote ne peut pas brancher son propre systeme de buffers sans repasser par `RenderTarget2D` |
| Helpers GPU physiques encore sous `MGUI.Shared` | `ContentUtils`, `TextureUtils`, `RenderUtils`, `SolidColorTexture`, `RenderTargetPool`, `BackBufferSurface`, `MainRenderer`, `DrawTransaction`, `View`, `MonoGameImageResource` | sortir integralement ces sources vers `MGUI.MonoGame` ou les supprimer si elles deviennent purement legacy | la separation projet/source reste trompeuse tant que `Shared` heberge l'implementation concrete |
| Fuites residuelles cote `MGUI.Core` | `MGElement`, `MGDesktop`, `MGGrid`, `MGUniformGrid` lisent `GraphicsDevice.ScissorRectangle`; `MGTexturedBorderBrush` et `MGImage` gardent des chemins `Texture2D`; plusieurs commentaires publics continuent de documenter les ressources UI comme des textures MonoGame | migrer ces call sites vers des capacites de clip/image backend-neutral ; cantonner tout shim legacy au backend | le coeur UI observe encore des details GPU qui ne devraient plus lui etre visibles |
| Valeurs mathematiques et couleurs | `Color`, `Point`, `Rectangle`, `Vector2`, `Matrix` restent omnipresents | toleres provisoirement dans cette phase | ces types ne bloquent pas a eux seuls la propriete du rendu tant qu'ils ne transportent pas des ressources GPU |

Seams a reouvrir en priorite:

1. redefinir `IUIDrawContext`, `IUIRenderContext`, `IUISurface` et `DrawSettings` autour de capacites de draw, clip et buffer sans objet MonoGame en surface ;
2. sortir `Texture2D` et `ContentManager` des contrats d'assets et d'images, puis migrer `MGTextureData`, `MGResources`, `MGImage`, `MGTexturedBorderBrush` et les text runs image ;
3. decoupler le draw texte backend de la mesure/layout en supprimant l'obligation `ITextMeasurementEngine` + `IMonoGameTextRenderer` sur le meme objet runtime ;
4. retirer les observations directes du GPU dans `MGUI.Core` (`GraphicsDevice.ScissorRectangle`, etat rasterizer, render targets) au profit de capacites de clip et de culling backend-neutral ;
5. une fois les contrats et call sites migres, redefinir `MainRenderer` comme adapter/facade MonoGame puis deplacer physiquement tous les fichiers concrets hors de `MGUI.Shared`.

Decision explicite sur les types pivots:

- `MainRenderer`: doit devenir un adapter MonoGame concret dans `MGUI.MonoGame`, avec eventuellement une facade de compatibilite legacy, mais il ne doit plus modeler les contrats partages ;
- `DrawTransaction`: doit rester l'executant concret MonoGame des nouveaux contrats de draw, vivre cote backend et ne plus etre caste depuis `Core` ;
- `View`: doit sortir de `MGUI.Shared` et etre traite comme helper/runtime backend-side ou couche legacy ; `Core` doit continuer de ne connaitre que `IUIView` et les abstractions de runtime ;
- `IUISurface`: doit cesser d'exposer `RenderTarget2D` et devenir soit un contrat de surface/buffer opaque, soit etre absorbe par un contrat de composition/backend target plus explicite ;
- `IMonoGameTextRenderer`: doit rester strictement backend-specific dans `MGUI.MonoGame` et ne plus apparaitre comme contrainte des contrats partages ni du runtime coeur.

Validation:

- inspection ciblee des contrats et fichiers suivants: `IUIDrawContext`, `IUIRenderContext`, `IUISurface`, `IUIAssetProvider`, `IMonoGameTextRenderer`, `DrawSettings`, `MainRenderer`, `View`, `BackBufferSurface`, `RenderTargetPool`, `ContentUtils`, `TextureUtils`, `RenderUtils`, `MonoGameImageResource`, `MGTextureData`, `MGResources`, `MGImage`, `MGTexturedBorderBrush`, `MGElement`, `MGGrid`, `MGUniformGrid`, `MGDesktop` ;
- recherche ciblee des fuites `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `Texture2D`, `RenderTarget2D`, `ContentManager` et `SpriteEffects` sous `MGUI.Shared` et `MGUI.Core` ;
- aucun build ni `dotnet test` relance pour cette tache car elle borne l'architecture et ne modifie encore aucun contrat compile.

### ✅ 13. Ajouter des tests d'architecture pour epingler une architecture sans implementation de rendu dans Shared/Core

But:
verrouiller la cible avant de reouvrir les contrats de draw.

Travail attendu:

- ajouter des tests qui epinglent l'absence d'implementation concrete de rendu dans `MGUI.Shared` et `MGUI.Core` ;
- verifier que les contrats publics cibles n'exposent plus, ou sont en train d'arreter d'exposer, `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `Texture2D`, `RenderTarget2D`, `ContentManager` et `MainRenderer` ;
- autoriser uniquement des exceptions temporaires explicitement justifiees et bornees ;
- distinguer les tests qui verifient les contrats publics des tests qui verifient la localisation physique des sources.

Livrable:

- tests d'architecture pour la phase 4 ;
- allowlists explicites des exceptions transitoires ;
- point de depart mesurable avant migration.

Criteres d'acceptation:

- une regression vers des contrats MonoGame-centriques dans `Shared` ou `Core` devient visible automatiquement ;
- les exceptions legacy sont rares, documentees et testees ;
- les prochaines taches peuvent durcir progressivement ces tests au lieu de repartir d'un audit manuel.

Commit recommande:

- `test: complete task 13 pin zero-implementation rendering architecture`

Resultat:

- une nouvelle suite `Phase4RenderingArchitectureTests` epingle l'etat transitoire exact des fuites backend de phase 4 au lieu de reposer sur un audit manuel fragile ;
- les tokens critiques `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `Texture2D`, `RenderTarget2D`, `ContentManager` et `MainRenderer` sont maintenant verifies avec des allowlists explicites et bornees pour `MGUI.Shared` et `MGUI.Core` ;
- la couverture distingue explicitement deux choses: les references backend encore visibles dans `Shared/Core`, et le sous-ensemble de fichiers concrets de rendu encore physiquement presents sous `MGUI.Shared` ;
- le test de localisation physique epingle la liste exacte des fichiers concrets encore tolerees sous `MGUI.Shared`, ce qui permettra de la reduire progressivement jusqu'a zero pendant les taches 14 a 17 ;
- les tests de phase 2 existants restent utiles pour les seams historiques, mais la phase 4 dispose maintenant de son propre point de depart mesurable.

Exceptions transitoires explicitement bornees:

- cote `MGUI.Shared`, les allowlists couvrent uniquement les contrats et helpers/files deja identifies par l'audit de task 12 ;
- cote `MGUI.Core`, seules les references `GraphicsDevice`, `Texture2D` et `MainRenderer` encore presentes dans `MGDesktop`, `MGElement`, `MGGrid`, `MGUniformGrid`, `MGImage`, `MGTextureData` et `MGTexturedBorderBrush` sont autorisees ;
- tout nouveau fichier ou nouveau token backend ajoute hors de ces allowlists fera echouer la suite d'architecture.

Validation:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj -c Release --filter FullyQualifiedName~Phase4RenderingArchitectureTests` : succes ;
- avertissements XML/nullability existants dans `MGUI.Core` et `MGUI.Tests` inchanges et hors perimetre ;
- un premier essai en Debug a echoue a cause d'un verrou sur `MGUI.Rendering.Abstractions.dll` dans le shell PowerShell courant, sans impact sur la validite des tests eux-memes.

### ⚪ 14. Introduire des contrats backend-neutral pour formes, texte, images, clipping et buffers

But:
faire porter au backend la responsabilite du dessin et de la gestion des buffers, au lieu de l'ancrer dans les primitives MonoGame.

Travail attendu:

- redefinir `IUIDrawContext`, `IUIRenderContext`, `IUISurface`, `IUIAssetProvider` et les contrats texte de draw pour qu'ils decrivent des capacites backend-neutral ;
- sortir des signatures publiques les types `Texture2D`, `RenderTarget2D`, `GraphicsDevice`, `SpriteBatch`, `PrimitiveBatch`, `ContentManager` et `SpriteEffects` ;
- introduire, si necessaire, des handles opaques ou petits contrats pour les surfaces cibles, buffers offscreen, images, texte et options de draw ;
- s'assurer que ces contrats permettent explicitement a un moteur hote de dessiner formes, texte et buffers avec sa propre implementation.

Livrable:

- nouveau jeu de contrats de rendu backend-neutral ;
- adapters MonoGame immediats dans `MGUI.MonoGame` ;
- au moins un call site reel migre pour chaque nouvelle capacite contractuelle.

Criteres d'acceptation:

- un backend non-MonoGame peut exprimer les memes responsabilites de rendu sans fournir d'objet MonoGame aux contrats publics ;
- `Shared` ne contient plus d'API publique qui suppose `SpriteBatch` ou `RenderTarget2D` ;
- les besoins de shapes, texte et buffers sont couverts par des contrats lisibles et compacts.

Commit recommande:

- `contracts: complete task 14 add engine owned render backend contracts`

### ⚪ 15. Migrer Shared et Core vers les nouveaux contrats de backend de rendu

But:
retaper les chemins de draw du coeur UI pour qu'ils ne dependent plus d'objets ou de signatures MonoGame.

Travail attendu:

- migrer `MGDesktop`, `UIView`, `DrawBaseArgs`, `MGResources`, `MGTextureData`, les brushes, les shapes et les runs de texte vers les nouveaux contrats ;
- supprimer les dependances publiques residuelles de `Core` a `MainRenderer`, `DrawTransaction` et aux ressources GPU natives ;
- faire en sorte que `Core` ne connaisse plus qu'un runtime backend-neutral et des handles/contracts de draw backend-neutral ;
- confiner les shims de compatibilite eventuels dans le backend MonoGame ou dans des wrappers clairement temporaires.

Livrable:

- coeur UI retape sur les nouveaux contrats ;
- migrations des call sites critiques terminees ;
- matrice d'exceptions residuelle drastiquement reduite.

Criteres d'acceptation:

- `MGUI.Core` peut etre compile sans API publique de draw MonoGame en surface ;
- `MGDesktop` ne depend plus d'un renderer concret pour son chemin nominal ;
- les derniers ponts legacy sont localises, optionnels et testes.

Commit recommande:

- `core: complete task 15 migrate shared and core draw paths to backend contracts`

### ⚪ 16. Redefinir MainRenderer comme simple adapter de backend MonoGame

But:
faire de `MainRenderer` une implementation MonoGame concrete, et non plus le modele implicite du runtime de rendu.

Travail attendu:

- decider si `MainRenderer` doit etre renomme, encapsule ou conserve comme facade de compatibilite dans `MGUI.MonoGame` ;
- retaper `DrawTransaction`, `View`, `BackBufferSurface`, `RenderTargetPool`, `ClipManager` et les helpers de draw pour qu'ils dependent du backend MonoGame concret, pas de `Shared` ;
- supprimer du cote `Shared` toute hypothese selon laquelle le runtime concret s'appelle `MainRenderer` ;
- si une facade `MainRenderer` est gardee, la marquer comme chemin de compatibilite borne et non comme contrat de reference.

Livrable:

- runtime MonoGame clairement redefine comme adapter backend ;
- frontiere nette entre contrats partages et implementation concrete ;
- compatibilite legacy, si necessaire, confinee au backend.

Criteres d'acceptation:

- les contrats publics de `Shared` et `Core` ne sont plus models par `MainRenderer` ;
- `MainRenderer` n'est plus qu'un detail d'implementation MonoGame ou une facade de transition ;
- un backend alternatif n'a plus a imiter la forme de `MainRenderer` pour s'integrer.

Commit recommande:

- `backend: complete task 16 redefine monogame renderer as backend adapter`

### ⚪ 17. Deplacer physiquement toutes les sources de rendu concret hors de Shared

But:
faire correspondre la localisation physique des fichiers a la frontiere architecturale finale.

Travail attendu:

- deplacer reellement les sources concrates de rendu hors de `MGUI.Shared` vers `MGUI.MonoGame` ou un backend equivalent, au lieu de se contenter de linked includes ;
- nettoyer les `.csproj` pour que `MGUI.Shared` ne porte plus de fichiers de rendu concret ni de paths historiques trompeurs ;
- verifier les namespaces, les references de solution et les conventions d'organisation finales ;
- ne pas deplacer de contrats backend-neutral avec ces sources concretes.

Livrable:

- arborescence de sources alignee sur l'architecture finale ;
- `MGUI.Shared` sans implementation concrete de rendu ;
- `MGUI.MonoGame` comme seul proprietaire physique des fichiers MonoGame de rendu.

Criteres d'acceptation:

- il n'existe plus de fichier d'implementation concrete de rendu sous `MGUI.Shared` ;
- les projets compilent sans linked include ambigu pour les sources de draw concretes ;
- la structure du repo reflete enfin la vraie separation des responsabilites.

Commit recommande:

- `backend: complete task 17 move concrete render sources fully out of shared`

### ⚪ 18. Prouver qu'un backend possede par le moteur peut dessiner formes, texte et buffers

But:
verifier concretement que le nouveau design permet a un moteur hote de posseder le rendu, au-dela du seul backend MonoGame existant.

Travail attendu:

- ajouter un backend de preuve minimal, un harness de test ou un adapter de demonstration qui n'utilise pas le chemin de draw MonoGame historique comme unique preuve ;
- faire implementer a ce backend de preuve les contrats necessaires pour dessiner des formes, du texte et gerer au moins un buffer/surface offscreen ;
- brancher ce backend sur au moins un chemin reel de `MGDesktop` / `UIView` ou sur une suite de tests d'integration representative ;
- documenter clairement ce qui est prouve par ce backend et ce qui reste hors perimetre si un moteur complet n'est pas encore integre au repo.

Livrable:

- backend ou harness de preuve ;
- tests d'integration ou d'architecture associant shapes, texte et buffers ;
- demonstration verifiable qu'un moteur hote peut posseder le rendu.

Criteres d'acceptation:

- la pile UI peut deleguer shapes, texte et buffers a une implementation qui ne depend pas du runtime MonoGame historique ;
- la preuve couvre les 3 capacites demandees, pas seulement les images ;
- l'architecture ne repose plus uniquement sur la promesse qu'un backend alternatif serait possible plus tard.

Commit recommande:

- `test: complete task 18 prove engine owned shapes text and buffers`

### ⚪ 19. Documenter l'integration d'un backend de rendu possede par le moteur

But:
laisser un guide actionnable pour brancher un moteur hote qui dessine lui-meme les formes, le texte et les buffers.

Travail attendu:

- mettre a jour la doc d'architecture, le guide d'integration et le README pour decrire la nouvelle separation complete ;
- documenter les contrats qu'un moteur doit implementer pour posseder le rendu ;
- expliquer le statut de MonoGame: backend de reference, plus contrat implicite ;
- lister les migrations a effectuer pour les consommateurs qui utilisaient encore `MainRenderer`, `DrawTransaction` ou des surfaces MonoGame dans le chemin nominal.

Livrable:

- doc d'architecture finale mise a jour ;
- guide d'integration d'un backend custom ;
- note de migration pour les consommateurs existants.

Criteres d'acceptation:

- un agent ou un contributeur humain comprend comment brancher un moteur proprietaire sans rereflechir tout le chantier ;
- la doc explicite qui dessine les formes, qui dessine le texte et qui possede les buffers ;
- la separation finale entre `Shared/Core` et les backends concrets est comprehensible en une seule lecture.

Commit recommande:

- `docs: complete task 19 document custom render backend integration`