# Taches systeme d'animation (V5) : entree et sortie, transition de layout, defilement fluide, cadres de texture, animations attendables

## Objectif

Livrer cinq capacites du backlog V5 choisies par l'auteur : animations d'entree et de sortie (C1), transition de layout (C2), defilement fluide (C3), brush texturee animee par cadres (C4) et animations attendables `PlayAsync` (C10). Les autres candidats (C5 a C9, C11) et les reliquats des programmes precedents restent en backlog a la fin de ce fichier.

Decisions : [decisions/0011-animation-v5.md](../decisions/0011-animation-v5.md). Architecture : [animation-architecture.md](../animation-architecture.md), qui decrit l'etat final ; chaque tache y ajoute ce qu'elle livre, sans historique.

Contraintes non negociables (inchangees) : pas de dependency property system ; store ADR-0005 pour les pilotes ; zero cout pour un element qui n'anime rien, zero allocation par tick pour un run ; tokens interdits de `RenderingBoundaryArchitectureTests` ; renderer neutre ; brushes gelables (ADR-0009) ; une ADR par decision ; aucun renommage d'API publique. Les tests existants restent verts sans modification, sauf re-ecriture explicitement listee dans une tache et sauf extension d'une table d'attente fermee que le test exige lui-meme (aujourd'hui la table `Expected` de `TargetApplicabilityTests`, le dictionnaire `SectionElementNames` de `AnimationDemoSampleTests` et les numeros de ligne de `AllowedLines` dans `ResolvedPilotWriteSitesTests`).

## Historique du fichier

- 15 septembre 2026 : creation du backlog a partir de l'evaluation demandee par l'auteur, faits verifies a HEAD `d64d28d`.
- 17 septembre 2026 : l'auteur demande de realiser ces taches sur une branche propre (`animation-v5`, creee depuis `develop` a `5e06b3b`, dans un worktree separe : le checkout principal appartient a la session de l'editeur XAML). Reponses de l'auteur aux quatre questions groupees (ci-dessous), discovery en lecture seule sur six surfaces, redaction de ce plan et de l'ADR-0011 (Proposed), relecture du plan contre le code par cinq relecteurs en contexte frais avec recontrole de chaque constat bloquant, puis relecture de cloture et verification finale (constats integres ci-dessous ; les corrections de la verification finale n'ont pas ete relues a leur tour). Aucun code avant l'approbation du plan.

## Decisions de l'auteur (17 septembre 2026)

1. Perimetre : C1, C2, C3, C4 et C10. C5 a C9 et C11 restent pour un second programme.
2. Reliquats et points ouverts des ADR-0008 et 0009 : laisses en l'etat.
3. Theme (C1) : le groupe `MGTheme.Animation` s'etend aux fenetres, popups, tooltips, menus contextuels et a la liste deroulante ; les autres elements gardent un opt-in par element.
4. Rythme : AUTO. Les taches s'enchainent apres l'approbation du plan ; arret seulement sur un blocage ou une decision qui revient a l'auteur ; validation manuelle du sample a la fin.
5. Branche : `animation-v5`, depuis `develop` ; le merge dans `develop` se fait a la demande de l'auteur.

## Etat des lieux (HEAD `5e06b3b`, verifie dans le code)

Moteur :

- Un `UIAnimationManager` par desktop (`MGDesktop.Animations`), tique en tete de `MGDesktop.Update`, avant le layout ; une animation par couple (element, chemin) ; un remplacement annule l'ancienne en `KeepCurrent` (`MGUI.Core/UI/Animation/UIAnimationManager.cs:27-178`). Relancer la meme instance sur le meme chemin ne leve pas `Cancelled`.
- `UIAnimation` : etats `Stopped`, `Delayed`, `Running`, `Paused`, `Completed`, `Cancelled` ; evenements `Started`, `Updated`, `Repeated`, `Reversed`, `Completed`, `Cancelled` ; `Completed` et `Cancelled` sont exclusifs ; `Seek` ne leve aucun evenement (`MGUI.Core/UI/Animation/UIAnimation.cs:131-203, 379-417`). Aucune exception d'un abonne n'est interceptee par le moteur. Un composite (`UIStoryboard`, `UISequenceAnimation`) se termine sur sa propre timeline, quel que soit le sort de ses enfants (`UIAnimationGroup.cs:200-207`).
- Demarrage : `element.Animations.Start(animation)` (`UIAnimationCollection.cs:23`) ; `Play()` est declare sur la base non generique `UIAnimationBuilder` (`UIAnimateExtensions.cs:32, 110`), `UIAnimationBuilder<T>` n'ajoute que les options typees et `Wait(...)` rend la base ; `UIAnimation.Play()` / `Restart()` avec un `Owner` fixe. Aucun `Task`, aucun `SynchronizationContext` dans le moteur.
- Un element detache (`OnParentChanged` vers null) appelle `Animations.Clear()` ; `MGDesktop.NotifyWindowClosed` appelle `CancelOwnedByWindow`, `UnregisterModalWindow` puis `UIFocusNavigationService.NotifyWindowClosed` (`MGDesktop.cs:1072-1084`) ; il est appele par `TryCloseWindow`, et aussi par `RemoveModalWindow` et le setter de `ModalWindow` a travers `SynchronizeModalState` (`MGWindow.cs:432-496`) : pour une modale il est donc deja leve deux fois aujourd'hui.

Cibles :

- Registre ferme `UIAnimationTargets` ; familles : pilotes du store (`IUIStoreBackedAnimationTarget<T>`), simples observables (`IUIObservableAnimationTarget<T>`, exemple `OpacityTarget`, `UIBuiltInAnimationTargets.cs:79-89`), simples non observables avec type requis (`ProgressButton.Value`, `UIBuiltInAnimationTargets.cs:180-192`, qu'une transition refuse), brushes clonees a l'animation (`IUIBrushAnimationTarget<T>`, qui herite de `IUIStoreBackedAnimationTarget<T>` ; exemple `BackgroundGradientTarget`, `UIExtraAnimationTargets.cs:100-176`). `UIPropertyAnimation<T>` accepte aussi une cible explicite non enregistree (`Target`), la cle de conflit restant `Target.Path`.
- `TargetApplicabilityTests` epingle la liste fermee des chemins enregistres et leur type requis (table `Expected`) : tout nouveau chemin doit y etre ajoute.
- Interpolateurs : `float`, `double`, `int` (arrondi, sans palier), `int?`, vecteurs, `Color`, `Rectangle`, `Thickness`, couleurs de gradients. Aucun mode discret dans les keyframes (`UIKeyFrameAnimation.cs:60-69`). Le codec de valeurs du serialiseur connait deja `float` et `int` (`UIKeyFrameSerializer.cs:114-169`).

Layout et transform :

- `UpdateLayout(Rectangle Bounds)` releve `PreviousLayoutBounds` dans une variable locale, affecte `AllocatedBounds`, `RenderBounds` et `LayoutBounds`, pose les composants puis les enfants (`UpdateContentLayout`), et ne leve `OnLayoutUpdated` et `OnLayoutBoundsChanged(precedentes, nouvelles)` qu'ensuite (`MGUI.Core/UI/MGElement.cs:4779-4920`) : la fin de `UpdateLayout` d'un ancetre passe apres celle de tous ses descendants. La passe tourne quand le layout de la fenetre est invalide ; une invalidation pendant la passe en provoque une seconde dans le meme tick (`QueueLayoutRefresh`, `MGWindow.cs:1466`).
- `LayoutBounds` est en espace ecran non mis a l'echelle, position de la fenetre comprise : un deplacement de fenetre passe par `TranslateAllBounds` (abonnement de chaque element a `OnWindowPositionChanged`, `MGElement.cs:3324-3328, 3494-3506`), pas par `UpdateLayout`. Le defilement ne change pas `LayoutBounds` : `MGScrollViewer` decale son contenu pendant l'update (`UpdateContents`, decalage des arguments qui alimente `MGElement.Origin`), tronque a l'entier.
- Transform de rendu (ADR-0006) : `MGElement.RenderTransform` alloue a la demande, `Origin` a (0, 0) par defaut, identite gratuite ; la matrice effective est composee dans `TryGetRenderTransformMatrix`, inversee par `TryApplyInverseRenderTransform`, et le compteur `ActiveRenderTransformCount` du desktop ouvre ou ferme le chemin d'inversion du hit test pour tout le desktop.
- `MGStackPanel` n'a pas d'API de reordonnancement : deplacer un enfant est un retrait suivi d'une insertion, donc un detachement. Un `MGExpander` bascule la `Visibility` de son contenu ; un element `Collapsed` a des bounds vides.

Defilement :

- `MGScrollViewer.VerticalOffset` / `HorizontalOffset` : `float`, bornes par le setter a `[0, Max...Offset]`, evenements `VerticalOffsetChanged` / `HorizontalOffsetChanged` ; la molette ajoute `VerticalScrollInterval` (40 px) et ses gardes (`VerticalOffset > 0`, `< MaxVerticalOffset`) decident aussi de qui consomme l'evenement ; `EnsureElementVisible` ecrit les offsets directement ; un changement de `MaxVerticalOffset` re-borne l'offset (`MGUI.Core/UI/MGScrollViewer.cs:95-121, 147, 263-374, 690-720`). `MGScrollViewer` n'a aucun chemin clavier : la navigation au clavier defile par `EnsureElementVisible`, sauf deux sites qui ecrivent l'offset directement (`MGListBox.cs:1307`, branche virtualisee ; `MGTreeView.cs:861`).

Texture :

- `MGTextureFillBrush` (`Source`, `Stretch`, `Color`, `Tile`) est gelable ; `Draw` passe `Source.SourceRect` tel quel a `IUIDrawContext.DrawTextureTo` dans trois chemins (rectangle, geometrie arrondie, `Tile`), et sa taille naturelle (`Source.RenderSize`) pilote le pas de tuile et les modes `Stretch` autres que `Fill` ; `MGTextureData(IUIImageResource Image, Rectangle? SourceRect, ...)` est un `record struct` qui partage son image par reference (`MGUI.Core/UI/Brushes/FillBrushes/MGTextureFillBrush.cs:15-357`, `MGUI.Core/UI/MGTextureData.cs:11-22`). Le DTO XAML `TextureFillBrush` n'expose que `SourceName`, `Stretch`, `Color` (`MGUI.Core/UI/XAML/Brushes.cs:200-217`).
- Une planche existe deja dans le contenu : `Icons/AngryMeteor_MilitaryIconsSet` (grille 16 px, espacement 1, marge haute 6), chargee par `MGDesktop.LoadDefaultResources` (`MGDesktop.cs:1154-1214`). Les tests fabriquent une image sans GPU par `GraphTestImageResource` (`MGUI.Tests/Graph/GraphTestRuntime.cs:223-230`). `AnimationDemoSampleTests.LoadStrict` n'appelle pas `LoadDefaultResources`.

Fenetres, popups et visibilite :

- `MGWindow.TryCloseWindow()` rend `false` d'emblee si `CanCloseWindow` est faux, leve `WindowClosing` (annulable), retire la fenetre de sa liste (fenetres modales, imbriquees ou `Desktop.Windows`), appelle `Desktop.NotifyWindowClosed` puis leve `WindowClosed` (`MGUI.Core/UI/MGWindow.cs:764-807`).
- `Desktop.Windows` est une `List<MGWindow>` publique sans notification d'ajout ni de retrait (`MGDesktop.cs:858`) ; `BringToFront` / `BringToBack` sont un retrait suivi d'un ajout ; le sample (`SampleBase`) affiche et masque ses fenetres par ajout et retrait directs dans cette liste. Les fenetres imbriquees et modales passent par `AddNestedWindow` et la pile modale, qui notifient `Desktop.NotifyWindowOpened`.
- La liste deroulante de `MGComboBox` est une fenetre imbriquee ajoutee et retiree directement par `ParentWindow.AddNestedWindow` / `RemoveNestedWindow` (`MGUI.Core/UI/MGComboBox.cs:624-658`), sans passer par `TryCloseWindow` ; `MGColorPickerPopup` et `MGDockHost.CloseFloatingWindow` retirent aussi des fenetres directement. `AddNestedWindow` leve une exception si la fenetre est deja dans la liste.
- `MGDesktop.ActiveToolTip` est reconstruit a chaque update depuis `QueuedToolTip` (etat dans `UIViewState`) et dessine a la position courante de la souris ; `ActiveContextMenu` est dessine a part, apres les fenetres (`MGDesktop.cs:1415-1482, 1567-1589`) ; `TryCloseActiveContextMenu` ferme les sous-menus recursivement (`MGDesktop.cs:732-768`). Une fenetre qui n'est pas traversable marque les evenements de souris comme traites et occulte ce qu'elle recouvre.
- `MGElement.Visibility` : le setter applique la valeur tout de suite et invalide le layout quand `Collapsed` est en jeu (`MGElement.cs:3110-3129`) ; l'eligibilite aux entrees est recalculee quand `_inputStateDirty` est leve.
- `MGThemeAnimationSettings` (`Enabled` faux par defaut, durees et easings de survol, de pression et de focus) n'est lu que par `UIThemeTransitions`, pour `MGButton` et `MGToggleButton` (`MGUI.Core/UI/MGTheme.cs:218-236`, `UIThemeTransitions.cs:13-35`). Ajouter une valeur de theme touche aussi le bloc de copie de `MGTheme`, l'inventaire `UIThemeValueInvalidation`, le DTO `ThemeAnimationSettingsDefinition`, `ThemeDefinitionBuilder.ApplyAnimation` et le dictionnaire epingle de `ThemeValueInvalidationInventoryTests`.

Tests, sample et docs :

- Harnais : `AnimationTestScene` (desktop headless 800 x 600, `Frames(n, ms)`), `GraphTestRuntime`, `GraphNoOpDrawTransaction` qui enregistre les appels de dessin et les transforms (`MGUI.Tests/Animation/AnimationTestScene.cs`, `MGUI.Tests/Graph/GraphTestRuntime.cs`). xunit installe un `SynchronizationContext` pendant un test.
- `AnimationDemo` : 14 sections (XAML, `Wire...` dans le code-behind) ; `AnimationDemoSampleTests.SectionElementNames` liste les elements nommes de chaque section, les charge en mode strict et verifie qu'ils sont mis en page.
- Treize fichiers de `MGUI.Tests` lisent les sources par le chemin absolu `d:\development\repo\MGUI\...` (143 occurrences), donc le checkout principal et non le worktree de cette branche ; quatre fichiers derivent deja une racine de `AppContext.BaseDirectory` dans un champ prive (`BackendProjectSplitTests`, `ResolvedPilotWriteSitesTests`, `ControlTemplateInfrastructureTests`, entre autres) ; `EditorCompactPresetTests` declare cinq chemins en `const`.
- Suite complete dans le worktree a `5e06b3b` : 2393 tests verts.

## Consignes de travail pour l'agent IA

- Travailler uniquement dans le worktree de la branche `animation-v5` ; ne jamais toucher au checkout principal ni a la branche `xaml-editor` ; jamais de `git stash` nu (la pile est partagee) ; jamais de push. Verifier `git branch --show-current` avant chaque commit.
- Une tache a la fois : 🚧 avant de commencer, ✅ a la fin (🧪 si une validation manuelle manque), statut mis a jour dans le meme commit que la tache. Un commit par tache, indexe fichier par fichier.
- Pipeline par tache : brief ecrit par la session principale (BRIEF, PERIMETRE, CLAIM, ACCEPTANCE), execution par un agent `sonnet`, verification par un agent `opus` en contexte frais, au plus deux tours de correction, puis revue et commit par la session principale. Mode AUTO : enchainement sans attendre, arret sur blocage.
- Chaque tache qui livre une capacite ajoute : ses tests, sa section dans `Docs/animation-architecture.md` (etat final, sans historique), une section dans le sample `AnimationDemo` avec un texte d'explication (jamais une colonne de version), l'entree correspondante de `AnimationDemoSampleTests.SectionElementNames`, et la mention dans la ligne `SCN-ANIM-001` de `Docs/scenario-validation-index.md`. Une capacite livree en deux taches porte sa section de sample, son entree de `SectionElementNames` et sa mention `SCN-ANIM-001` dans la seconde (C2 : dans Y5). Un element nomme d'une section reste `Visible` et mis en page au chargement, et aucune section ne demarre d'animation au seul chargement du XAML.
- Toute tache qui enregistre un nouveau chemin d'animation ajoute sa ligne a la table `Expected` de `MGUI.Tests/Animation/TargetApplicabilityTests.cs`.
- `ResolvedPilotWriteSitesTests.AllowedLines` epingle des lignes par numero dans `MGScrollViewer.cs` (557), `MGDesktop.cs` (814), `MGContextMenu.cs` (519) et `MGWindow.cs` (231, 249) : toute tache qui insere du code au-dessus d'une de ces lignes resynchronise le numero (aucune entree ajoutee ni supprimee, aucune assertion modifiee).
- Ne jamais lancer `MGUI.Samples` depuis un agent ; terminer tout travail sur le sample par `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-incremental`.
- Editions avec l'outil d'edition uniquement : jamais de reecriture de fichier par PowerShell, perl ou sed ; aucun caractere U+FFFD dans le diff.
- Toute decision prise en cours de route est ajoutee a l'ADR-0011, section « Decisions taken during delivery ».
- Blocage (information manquante, besoin d'une API publique non listee, contradiction) : ⚠️, question dans « Points ouverts », arret.
- Docs en francais sans accents ; code, commits et ADR en anglais.

## Legende de statut

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation minimale

1. `dotnet build MGUI.Tests/MGUI.Tests.csproj`
2. `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~MGUI.Tests.Animation"` pendant la tache, puis la suite complete avant le commit (reference : le total du statut de la derniere tache livree).
3. `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-incremental` quand le sample est touche.

## Taches

### ✅ Y0. Tests independants du chemin du checkout

But : que les tests qui lisent les sources testent la branche sur laquelle ils tournent, et non le checkout principal.

Pourquoi maintenant : cette branche vit dans un worktree ; `RenderingBoundaryArchitectureTests` (tokens interdits dans `MGUI.Core`) et douze autres fichiers liraient sinon les sources d'une autre branche. Sans cette tache, une violation introduite ici passerait au vert, et un travail en cours de l'autre session pourrait faire echouer cette suite.

Perimetre : un helper de test unique (`MGUI.Tests/TestRepository.cs`, `Root` derive de `AppContext.BaseDirectory`), le remplacement mecanique des 143 litteraux `d:\development\repo\MGUI\...` des treize fichiers par des chemins construits sur ce helper, et le remplacement des champs prives `RepoRoot` deja derives de `AppContext.BaseDirectory` par ce meme helper, pour que la racine soit calculee a un seul endroit. Dans `EditorCompactPresetTests`, les quatre champs `const` deviennent des `static readonly` et le `const` local de la methode de test du sample devient une variable locale. Aucune assertion ne change.

Criteres d'acceptation :

- plus aucune occurrence du chemin absolu dans `MGUI.Tests` ;
- les memes tests, en meme nombre, passent dans le worktree et leur resultat ne depend plus du checkout principal (preuve : un test de garde echoue si le chemin resolu ne contient pas `MGUI.sln`, et la racine resolue est celle du worktree) ;
- suite complete verte ; aucun fichier hors `MGUI.Tests` modifie.

Risque connu : conflits textuels mineurs au merge avec la branche `xaml-editor`, qui modifie deux de ces tests dans sa tache X8.

Commit recommande : `test: resolve the repository root from the test assembly instead of an absolute path`

**Statut** (17 septembre 2026) : livree. `MGUI.Tests/TestRepository.cs` calcule la racine une seule fois ; les 143 litteraux et les six champs `RepoRoot` passent par lui ; deux tests de garde (`TestRepositoryTests`). Suite complete dans le worktree : 2395 tests verts (2393 + 2). Verification en contexte frais : confirmee. Constat P4 differe : le second test de garde ne peut echouer que si le dossier de sortie a moins de quatre niveaux ; ce sont le premier test (presence de `MGUI.sln` et du projet de test) et l'emplacement de la DLL qui lient la racine au worktree. Note : `MGUI.Tests` utilise WPF, ses usings implicites n'incluent pas `System.IO`.

### ✅ Y1. Animations attendables : `PlayAsync` (C10)

But : enchainer des animations depuis du code asynchrone, sans exception sur le chemin normal.

Perimetre : `MGUI.Core/UI/Animation/` (nouveau fichier `UIAnimationAsyncExtensions.cs`, ajouts a `UIAnimationCollection`, a la base non generique `UIAnimationBuilder` et a `UIAnimationManager` pour la file des demandes d'annulation), tests, doc, sample.

- `Task<bool> UIAnimationCollection.StartAsync(UIAnimation animation, CancellationToken cancellationToken = default)` ; `Task<bool> PlayAsync(CancellationToken = default)` sur `UIAnimationBuilder` (base, a cote de `Play()` : couvre `UIAnimationBuilder<T>` par heritage et une chaine terminee par `Wait(...)`) ; extension `Task<bool> PlayAsync(this UIAnimation animation, MGElement owner, CancellationToken = default)` (couvre storyboards et sequences).
- Resultat : `true` quand l'animation atteint `Completed`, `false` quand elle est annulee (remplacement par la regle de conflit, detachement, fermeture de fenetre, `Cancel()`, jeton). Aucune exception pour une annulation : une annulation est courante dans une interface et un `async void` qui lance une exception ferait tomber le jeu. Pour un composite, le resultat porte sur le composite lui-meme, pas sur ses enfants. Un `Restart()` de la meme instance pendant l'attente ne resout pas la tache : elle suit le run redemarre.
- Jeton : le rappel d'un `CancellationToken` s'execute sur le thread qui annule. Il ne touche donc jamais le moteur : il depose une demande d'annulation dans une file sure, drainee tout en haut de `UIAnimationManager.Update`, avant `Clock.Advance` et avant la sortie anticipee sur delta nul (horloge en pause ou `TimeScale` a zero) ; l'animation est alors annulee comme par `Cancel()` et la tache rend `false`. Un jeton deja annule a l'appel rend `false` sans rien demarrer. L'enregistrement du jeton est libere des que la tache se resout. Sans demande en attente, le manager ne paie qu'un test par tick.
- Un `TaskCompletionSource<bool>` par appel, cree hors tick et sans `RunContinuationsAsynchronously` ; la resolution a toujours lieu sur le thread de l'update. Dans une application sans `SynchronizationContext` (boucle de jeu MonoGame), la suite du `await` s'execute donc sur ce thread, pendant le tick, comme un abonne de `Completed`. Aucune allocation par tick.
- Refus explicites (`InvalidOperationException`) : animation de preview, animation deja active. Une animation `RepeatForever` ne se termine que par annulation (documente).

Criteres d'acceptation (tests `MGUI.Tests/Animation/AnimationAsyncTests.cs`) :

- fin normale rend `true` ; remplacement sur le meme chemin, detachement de l'element, fermeture de la fenetre, `Cancel()` rendent `false`, sans exception ; un jeton deja annule ne demarre rien ; un jeton annule depuis un autre thread pendant un tick n'altere pas le moteur et rend `false` au tick suivant ; un jeton annule pendant que l'horloge est en pause rend `false` a la frame suivante ;
- une chaine fluide terminee par `Wait(...)` est attendable ; storyboard et sequence : la tache se resout a la fin du composite, et un enfant remplace pendant le run ne change pas le resultat ; `Restart()` pendant l'attente : la tache se resout a la fin du run redemarre ;
- le test neutralise le contexte de xunit (`SynchronizationContext.SetSynchronizationContext(null)`) : la suite du `await` s'execute alors sur le thread de l'update et peut demarrer une nouvelle animation, qui avance a partir de la frame suivante ;
- aucune allocation par tick pendant un run attendu (meme mesure que les tests de cout existants) ;
- section du sample : une sequence `await` de trois animations avec un bouton d'annulation.

Commit recommande : `animation: add awaitable PlayAsync and StartAsync`

**Statut** (17 septembre 2026) : livree. `StartAsync`, `UIAnimationBuilder.PlayAsync`, extension `PlayAsync(owner)` ; file `ConcurrentQueue` videe en tete de `UIAnimationManager.Update` ; `UIAnimationCompletion` interne (liberation du jeton par `Unregister`). 21 tests (`AnimationAsyncTests`), section 15 du sample. Suite complete : 2416 tests verts ; sample construit en `--no-incremental`. Verification en contexte frais : confirmee. Constats differes a Y9 (coherence du sample) : P3, dans la section 15, un clic sur Play pendant un run laisse la suite de l'ancien run ecrire « Cancelled at step N » par-dessus le statut du nouveau ; P4, le texte de la section dit que le jeton n'est passe qu'au premier `PlayAsync` alors qu'il l'est aux trois ; P4, le test de continuation ne releve pas explicitement la frame dans la continuation (le comportement inline reste prouve par le reste du test).

### ⏳ Y2. Defilement fluide (C3)

But : offsets de `MGScrollViewer` animables, `ScrollTo`, et une molette fluide sur option.

Perimetre : `MGUI.Core/UI/MGScrollViewer.cs`, cibles dans `MGUI.Core/UI/Animation/Targets/`, `MGUI.Core/UI/MGListBox.cs` et `MGUI.Core/UI/MGTreeView.cs` (un site chacun), DTO XAML `ScrollViewer`, `TargetApplicabilityTests` (table `Expected`), tests, doc, sample.

- Cibles simples observables `ScrollViewer.VerticalOffset` et `ScrollViewer.HorizontalOffset` (`float`, `RequiredOwnerType = MGScrollViewer`, abonnement sur les evenements d'offset). Le bornage reste celui du setter. L'offset etant applique tronque a l'entier, un defilement anime avance au pixel.
- `MGScrollViewer.ScrollTo(float? horizontalOffset, float? verticalOffset, TimeSpan duration, IUIEasingFunction easing = null)` : destination bornee au depart ; avec une duree nulle, c'est une ecriture directe ; sinon une `UIPropertyAnimation<float>` par axe, depart a la valeur courante, `CancelBehavior = KeepCurrent`.
- Une ecriture exterieure de l'offset pendant un run annule le run en `KeepCurrent` : l'utilisateur garde toujours la main. « Exterieure » designe toute ecriture de la propriete qui ne vient ni du run lui-meme ni du re-bornage interne quand le contenu retrecit (glisser de la barre, `ScrollToTop`, code applicatif, `ScrollTo` a duree nulle).
- `PendingVerticalOffset` / `PendingHorizontalOffset` (`float`, lecture seule, publics) : la destination du run en cours sur l'axe, ou la valeur courante au repos ; lus par la molette, `EnsureElementVisible` et les deux sites externes ci-dessous.
- `ScrollAnimationDuration` (`TimeSpan`, zero par defaut : comportement inchange) et `ScrollAnimationEasing` : quand la duree est non nulle, la molette et `EnsureElementVisible` passent par `ScrollTo` ; les crans de molette successifs se cumulent sur la destination en cours, pas sur la valeur animee, et les gardes de la molette (qui decident aussi de la consommation de l'evenement) lisent cette destination. Le glisser de la barre reste direct. Les deux sites qui ecrivent l'offset hors du viewer (`MGListBox.cs:1307`, `MGTreeView.cs:861`) passent par `ScrollTo` avec la duree du viewer, pour que le clavier se comporte pareil avec et sans virtualisation. Quand un run est en cours, `EnsureElementVisible` et ces deux sites calculent leur nouvel offset depuis la destination en attente exposee par le viewer, et comparent leur garde de 0.5 px a cette destination, pas a l'offset en vol : sinon une seconde touche pendant le run serait avalee et l'element focalise finirait hors du viewport. `VirtualizingWrapPanel.EnsureIndexVisible`, API publique sans appelant de production, reste une ecriture directe : elle annule donc un run en cours (decision, pas un oubli). Attributs XAML du meme nom.
- Limite connue : avec une transition attachee au meme chemin, un contenu qui retrecit pendant le run peut relancer la transition ; les criteres de cout ci-dessous valent sans transition attachee.

Criteres d'acceptation (tests `MGUI.Tests/Animation/ScrollAnimationTests.cs`) :

- animation et transition sur les deux chemins ; refus sur un element qui n'est pas un `MGScrollViewer` ; aller-retour du serialiseur sur un noeud de ces chemins ; deux lignes `typeof(MGScrollViewer)` dans la table `Expected` ;
- `ScrollTo` atteint la destination bornee ; une destination hors bornes s'arrete a la borne ; un contenu qui retrecit pendant le run termine a la nouvelle borne sans exception ;
- une ecriture exterieure pendant le run l'annule et la valeur ecrite est conservee ;
- trois crans de molette rapproches avec une duree non nulle aboutissent a trois intervalles ; dans deux `MGScrollViewer` imbriques, l'evenement de molette est consomme par le meme viewer qu'avec une duree nulle ; avec une duree nulle, le comportement actuel est inchange (tests existants verts) ;
- navigation au clavier dans une `MGListBox` virtualisee et dans un `MGTreeView` : fluide avec une duree non nulle, instantanee sinon ; plusieurs touches pendant un meme run : l'element focalise est visible a la fin du run ;
- zero cout pour un `MGScrollViewer` qui n'anime rien ; zero allocation par tick.

Commit recommande : `animation: add animatable scroll offsets, ScrollTo and optional smooth wheel scrolling`

### ⏳ Y3. Brush texturee animee par cadres (C4)

But : jouer une planche de sprites dans une `MGTextureFillBrush`, par le moteur, de facon serialisable.

Perimetre : `MGUI.Core/UI/Brushes/FillBrushes/MGTextureFillBrush.cs`, nouveau `MGSpriteSheetGrid`, cible dans `MGUI.Core/UI/Animation/Targets/`, DTO XAML `TextureFillBrush`, `TargetApplicabilityTests` (table `Expected`), tests, doc, sample.

- `MGSpriteSheetGrid` (`record struct` : colonnes, lignes, nombre de cadres, taille de cellule, espacement et marge a deux axes ; exemple de reference : la planche `AngryMeteor`, cellules de 16 px, espacement de 1 px sur les deux axes, marge haute de 6 px et marge gauche nulle) et, sur `MGTextureFillBrush`, `FrameGrid` (`MGSpriteSheetGrid?`) et `FrameIndex` (`int`), setters notifiants et gelables. Avec une grille, le rectangle source effectif est la cellule `FrameIndex` a l'interieur de `Source.SourceRect` (ou de l'image entiere), et la taille naturelle de la brush devient la taille de cellule : elle pilote le pas de tuile des deux chemins `Tile`, la destination de `Stretch.None` et le rapport d'aspect de `Uniform` / `UniformToFill` ; un `RenderSizeOverride` explicite reste prioritaire ; `MGTextureData` n'est pas modifie. Sans grille, rien ne change. `Copy`, `ValueEquals` et `GetHashCode` prennent les deux proprietes en compte.
- Cible `Background.Texture.Frame`, de type `float`, `RequiredOwnerType` null, clonee a l'animation (`IUIBrushAnimationTarget<float>`) : la valeur ecrite est `FrameIndex = clamp(floor(valeur), 0, cadres - 1)`. Un `float` a partie entiere donne des paliers exacts avec l'interpolation, les easings, les keyframes et le serialiseur existants, sans mode discret nouveau : animer de 0 a N affiche chaque cadre pendant une duree egale ; des keyframes donnent des durees par cadre.
- Contrat des membres herites du store : le pilote est `Background` ; `TryGetValueBelowAnimation` lit la brush sous l'animation (slot Normal) et rend son `FrameIndex` quand c'est une `MGTextureFillBrush` munie d'une grille, faux sinon ; `SetValue(element, valeur, source)` (chemin des etats visuels nommes) et `ClearContribution` copient cette brush du dessous, ecrivent `FrameIndex` sur la copie et la reecrivent sous la source donnee ; aucun membre ne fabrique une brush texturee a partir d'un simple nombre. `BeginAnimatedValue` clone la brush texturee lue au demarrage, sans repli ; `EndAnimatedValue` rend l'instance d'origine. Sans brush texturee munie d'une grille, le run et le setter d'etat visuel sont refuses par une `InvalidOperationException` explicite : c'est un refus de valeur au demarrage, pas un filtre de type.
- DTO XAML `TextureFillBrush` : `Tile`, `FrameColumns`, `FrameRows`, `FrameCount`, `FrameSpacing` et `FrameMargin` (deux valeurs, x puis y), `FrameIndex`.
- Aucun token interdit : uniquement `IUIImageResource`, `IUIDrawContext`, `MGTextureData`.
- Re-ecriture de test listee : `AnimationDemoSampleTests.LoadStrict` enregistre la planche avant le chargement (`desktop.Resources.AddTexture("AngryMeteor", ...)` sur une `GraphTestImageResource`).

Criteres d'acceptation (tests `MGUI.Tests/Animation/TextureFrameAnimationTests.cs` et complements de `FreezableBrushTests` / `TexturedPaintProjectionTests`) :

- rectangle source exact pour chaque cadre d'une grille avec espacement et marge, a l'interieur d'un `SourceRect` d'atlas ;
- `Stretch.None` avec grille : destination a la taille de cellule ; `Uniform` et `UniformToFill` : rapport d'aspect de la cellule ; `Fill` inchange ; `Tile` avec grille : pas de tuile egal a la taille de cellule dans les deux chemins, tuile de bord rognee depuis le coin de la cellule ;
- une brush gelee et partagee n'est jamais mutee : le run anime son clone, la restauration rend l'instance d'origine ; zero allocation par tick ;
- de 0 a N en lineaire : N paliers de duree egale, dernier cadre tenu a la fin ; `RepeatForever` boucle sans cadre parasite ; keyframes et clip JSON : aller-retour et durees par cadre ;
- refus explicite sans grille ou sans brush texturee, pour un run et pour un setter d'etat visuel ; une ligne a null dans la table `Expected` ; XAML strict charge les nouveaux attributs ;
- section du sample : la planche `AngryMeteor` jouee en boucle, avec un bouton pause.

Commit recommande : `animation: add sprite-sheet frames to the texture fill brush with an animation target`

### ⏳ Y4. Transition de layout : mouvement (C2, partie 1)

But : un element qui opte glisse de son ancienne position vers la nouvelle quand le layout le deplace, sans toucher au layout.

Perimetre : `MGUI.Core/UI/MGElement.cs` (dans `UpdateLayout` : juste apres l'affectation de `LayoutBounds` et dans le `finally` ; detachement ; composition de la matrice effective dans `TryGetRenderTransformMatrix` ; garde de `TryApplyInverseRenderTransform` ; comptage `ActiveRenderTransformCount`), `MGUI.Core/UI/Animation/` (`UILayoutTransition`, `UILayoutTransform`, cible interne), tests, doc.

- Opt-in : `MGElement.LayoutTransition` (`UILayoutTransition` : `Duration`, `Easing`, `AnimateSize` reserve a Y5), null par defaut. C'est un objet de reglages : il ne porte jamais les valeurs animees, et lire ou ecrire une cible n'opte jamais un element.
- Valeurs animees : un objet de rendu dedie, `UILayoutTransform` (`Offset`, `Scale`), alloue a la demande dans le slot d'animation de l'element, compose dans la matrice effective apres le `RenderTransform` et donc inverse par le hit test, compte dans `ActiveRenderTransformCount` tant qu'il n'est pas a l'identite. Il est separe de `RenderTransform.Translation` / `.Scale` : les animations de l'application cohabitent.
- Aucun chemin enregistre : le run interne utilise une cible explicite (`UIPropertyAnimation<Vector2>.Target`, cle de conflit `LayoutTransition.Offset`), invisible du registre, du XAML, des transitions et du serialiseur. Une instance d'animation par element, reutilisee : aucune allocation par changement de layout apres le premier.
- Declenchement : juste apres l'affectation de `LayoutBounds`, si les bounds precedentes et nouvelles sont non vides et different, que l'element a opte et qu'il etait deja attache et mis en page avant cette passe. Un deplacement de fenetre (`TranslateAllBounds`) et un defilement ne declenchent rien par construction. Premier layout, element tout juste attache ou rattache (changement de parent, reordonnancement fait d'un retrait et d'une insertion, conteneur virtualise recycle) : pas de transition. Le mecanisme est un drapeau par element, « mis en page au moins une fois depuis son rattachement », leve juste apres l'affectation de `LayoutBounds` et abaisse dans le setter de `Parent` de `MGElement`, la ou `OnParentChanged` est leve (inconditionnel, contrairement au handler du slot d'animation, qui n'existe qu'une fois le slot alloue) : un element detache garde ses anciennes `LayoutBounds`, c'est donc ce drapeau, et non l'effacement des animations, qui empeche un faux depart. Aucune transition dans une passe ou la taille de la fenetre affichante a change (redimensionnement).
- Imbrication : la passe se terminant enfant d'abord, le deplacement d'un ancetre est transmis par une pile ambiante de passe tenue par `UpdateLayout`. La pile n'est entretenue que sur un desktop ou au moins un element a opte (drapeau du desktop leve par le setter de `LayoutTransition`, jamais abaisse ; la decision d'empiler est prise en haut du `try` et memorisee dans une variable locale pour que le depilement corresponde) ; c'est une pile de structures preallouee, sans allocation. Quand elle est active : l'element empile une entree tout en haut du `try` (avec la valeur heritee), la met a jour sur place juste apres l'affectation reelle de ses `LayoutBounds`, donc avant ses composants et ses enfants, et la depile dans le `finally` : empilement et depilement sont toujours apparies, y compris sur les branches a bounds vides. L'entree porte le deplacement visuel applique au sous-arbre a l'instant zero : le delta propre de l'element s'il demarre une transition dans cette passe, sinon la valeur heritee de l'entree parente. Le delta effectif d'un element est son delta propre moins la valeur heritee : pas de double mouvement.
- Mouvement : le run part de (offset en cours - delta effectif) vers zero. Une seconde passe dans le meme tick, ou un second changement pendant le run, repart de la position visuelle courante.

Criteres d'acceptation (tests `MGUI.Tests/Animation/LayoutTransitionTests.cs`) :

- insertion et retrait dans un `MGStackPanel`, ouverture d'un `MGExpander` : les voisins optes que le layout deplace partent visuellement de leur ancienne position (matrice enregistree par `GraphNoOpDrawTransaction`) et finissent a l'identite ; leurs `LayoutBounds` sont les nouvelles des la premiere frame. L'element insere, retire, reordonne ou tout juste deplie ne joue rien ;
- aucun run sur un deplacement de fenetre, sur un defilement, au premier layout, apres un rattachement, pendant un redimensionnement de la fenetre, ni sur le conteneur recycle d'une `MGListBox` virtualisee ; un second changement pendant le run repart de la position visuelle courante, sans saut ;
- parent et enfant optes : pas de double deplacement ; enfant seul opte dans un parent qui bouge : l'enfant part de son ancienne position a l'ecran ;
- cohabitation avec une animation applicative sur `RenderTransform.Translation` ; hit test correct pendant le run ; `ActiveRenderTransformCount` revient a sa valeur initiale a la fin du run, apres un detachement et apres la fermeture de la fenetre en plein run ;
- cout : apres un echauffement, `GC.GetAllocatedBytesForCurrentThread()` sur N passes de layout identiques donne le meme total avec et sans element opte au repos ; zero allocation par tick pendant un run. Sur un desktop ou personne n'a opte, `UpdateLayout` ne paie qu'un test de drapeau ; sur un desktop ou un element a opte, chaque element paie en plus l'empilement et le depilement d'une entree, sans allocation (meme preuve par `GC.GetAllocatedBytesForCurrentThread()`).

Arret : si la pile ambiante demande de changer l'ordre de la passe de layout ou la signature de `UpdateLayout`, ⚠️ et question.

Commit recommande : `animation: add opt-in layout transitions on a dedicated layout transform`

### ⏳ Y5. Transition de layout : taille, XAML et sample (C2, partie 2)

But : completer la transition de layout par l'animation de taille, la declaration en XAML et la demonstration.

Prerequis : Y4.

Perimetre : `UILayoutTransition` / `UILayoutTransform` (`Scale`), DTO XAML `Element` et setters de style, tests, doc, sample.

- `AnimateSize` (faux par defaut) : l'echelle part du rapport des tailles (ancienne sur nouvelle) et finit a 1, autour du coin haut-gauche des nouvelles bounds, en meme temps que l'offset. Le contenu est etire pendant le run, comme dans toute technique FLIP (documente).
- XAML : attributs `LayoutTransitionDuration`, `LayoutTransitionEasing`, `LayoutTransitionAnimatesSize`, utilisables dans un `Setter` de style ; une duree nulle ou absente laisse `LayoutTransition` a null.

Criteres d'acceptation (complements de `LayoutTransitionTests`) :

- `AnimateSize` : matrice de depart egale au rapport des tailles, identite a la fin ; combinee au deplacement ;
- XAML strict et `Setter` de style ; un element sans attribut garde `LayoutTransition` null ;
- section du sample : une liste ou l'on insere, retire et deplace des elements ; ce sont les lignes voisines qui glissent vers leur nouvelle place ; texte d'explication sur l'element deplace, qui ne glisse pas.

Commit recommande : `animation: add size animation and XAML settings to layout transitions`

### ⏳ Y6. Entree et sortie d'un element (C1, partie 1)

But : un element rendu visible joue une animation d'entree ; un masquage joue une animation de sortie et n'est applique qu'a sa fin.

Perimetre : `MGUI.Core/UI/MGElement.cs` (`Visibility`, etat interne de sortie dans le calcul de l'eligibilite aux entrees et du hit test, drapeau de premier dessin), `MGUI.Core/UI/Animation/` (`UIEnterExitSettings`, `UIEnterExitEffect`, fabrique des animations d'effet), DTO XAML `Element`, tests, doc, sample.

- Reglages : `MGElement.EnterExit` (`UIEnterExitSettings`, null par defaut, alloue a la demande) porte `EnterAnimation` / `ExitAnimation` (`UIAnimation`, pour une animation arbitraire construite en code) et les effets predefinis declarables en XAML et dans un style : `EnterEffect` / `ExitEffect` (`None`, `Fade`, `Scale`, `FadeScale`, `SlideLeft`, `SlideRight`, `SlideUp`, `SlideDown`), `EnterDuration`, `ExitDuration`, `EnterEasing`, `ExitEasing`, `ScaleFrom` (0.9 par defaut) et `SlideDistance` (24 px par defaut). Une animation explicite l'emporte sur un effet. Les storyboards declaratifs en XAML restent hors perimetre (C7).
- Effets : ils animent les chemins publics `Opacity`, `RenderTransform.Scale` et `RenderTransform.Translation` (une animation applicative en cours sur ces chemins est remplacee, regle de conflit habituelle). `Fade` : opacite de 0 a la valeur de base ; `Scale` : de `ScaleFrom` a 1 autour du centre, le run tenant `RenderTransform.Origin` a (0.5, 0.5) le temps du run puis le restaurant ; les glissements : translation de `SlideDistance` pixels vers zero, distance fixe qui ne depend pas des bounds. La sortie joue l'inverse. Une entree qui interrompt une sortie, et inversement, part des valeurs courantes.
- Sortie : passer `Visibility` de `Visible` a `Hidden` ou `Collapsed` sur un element muni d'une sortie demarre le run ; `Visibility` garde sa valeur en vigueur (`Visible`) jusqu'a `Completed`, puis la valeur demandee est appliquee ; `PendingVisibility` expose la demande. Une sortie annulee de l'exterieur applique la valeur demandee tout de suite : jamais d'element fantome. Une sortie ne demarre que si l'element a deja ete dessine depuis son rattachement (drapeau a un coup pose sur le chemin de dessin) : toute ecriture de `Visibility` avant ce premier dessin (attribut XAML, `Setter`, premiere evaluation d'une liaison, contenu d'un `MGExpander` replie) s'applique tout de suite, sans run. On ne fait pas sortir ce qui n'a jamais ete a l'ecran.
- Entrees pendant la sortie : un etat interne de `MGElement` (jamais une ecriture sur la propriete publique `IsHitTestVisible`, qui appartient a l'application) contribue faux au hit test calcule et a l'eligibilite souris et clavier de l'element et de son sous-arbre ; le poser comme le lever leve `_inputStateDirty`. Il est remis a zero a la fin de la sortie, a son annulation et au detachement.
- Entree : passer a `Visible` joue l'entree, sans condition de dessin ni de mise en page (un element `Collapsed` n'est jamais mesure). Les valeurs de base (`Opacity`, transform) sont restaurees a la fin d'une sortie, pour que l'element reapparaisse intact. Un element sans reglages garde le comportement actuel, sans cout (un test de nullite dans le setter).

Criteres d'acceptation (tests `MGUI.Tests/Animation/EnterExitAnimationTests.cs`) :

- sortie : l'element reste dessine et garde sa place dans le layout pendant le run, puis devient `Collapsed` ; `PendingVisibility` suit ; pendant la sortie, ni survol ni clic sur l'element ni sur ses descendants, et `IsHitTestVisible` n'a pas change ;
- un element porteur d'une sortie rendu invisible avant son premier dessin (XAML, `Setter`, liaison) est masque tout de suite, sans run ; un element `Collapsed` au chargement joue son entree quand il devient `Visible` ;
- aller-retour rapide visible, masque, visible : pas de saut, etat final `Visible` avec les valeurs de base ;
- annulation externe de la sortie, detachement pendant la sortie, fermeture de la fenetre pendant la sortie : etat final coherent, aucune exception, aucune animation residuelle, etat interne de sortie remis a zero ;
- chaque effet predefini : valeurs de depart et d'arrivee exactes (`ScaleFrom`, `SlideDistance`, origine tenue puis restauree) ; XAML strict et `Setter` de style ; une liaison de donnees sur `Visibility` declenche les memes runs ;
- zero cout pour un element sans reglages ;
- section du sample : panneau affiche et masque avec chaque effet (les elements nommes de la section restent visibles au chargement).

Commit recommande : `animation: add enter and exit animations driven by element visibility`

### ⏳ Y7. Ouverture et fermeture des fenetres (C1, partie 2)

But : une fenetre, modale ou imbriquee, et la liste deroulante de `MGComboBox` jouent une entree a l'ouverture et une sortie avant leur retrait.

Prerequis : Y6.

Perimetre : `MGUI.Core/UI/MGWindow.cs` (cycle d'ouverture et de fermeture, `AddNestedWindow`, `RemoveNestedWindow`, entrees et occultation d'une fenetre en sortie), `MGUI.Core/UI/MGDesktop.cs` (`NotifyWindowClosed`, detection des fenetres racines apparues en tete de `Update`), `MGUI.Core/UI/MGComboBox.cs`, `MGUI.Core/UI/Docking/Controls/MGDockHost.cs` (retrait immediat des fenetres flottantes) et, si necessaire, `MGUI.Core/UI/Color/MGColorPickerPopup.cs` ; tests, doc, sample.

- Ouverture : une fenetre imbriquee joue son entree dans `AddNestedWindow` et une modale a son entree dans la pile modale (les deux chemins notifient deja `Desktop.NotifyWindowOpened`). `Desktop.Windows` etant une liste nue, une fenetre racine est detectee en tete de `MGDesktop.Update` par comparaison avec l'ensemble des fenetres vues a la frame precedente (deux listes reutilisees, sans allocation) : son entree part a cette frame. `BringToFront`, `BringToBack` et l'activation au clic, qui retirent et rajoutent dans la meme frame, ne rejouent rien. Une fenetre retiree puis rajoutee plus tard rejoue son entree. Changer le type de `Desktop.Windows` est hors perimetre.
- Fermeture : `TryCloseWindow()` garde son contrat (garde `CanCloseWindow`, `WindowClosing` annulable, retour `true` si la fermeture est acceptee). Avec une sortie : la fenetre passe `IsClosing`, rend le focus tout de suite, joue sa sortie, puis est retiree ; `NotifyWindowClosed` (annulation des animations de la fenetre, pile modale) et `WindowClosed` n'arrivent qu'apres le retrait. Un second `TryCloseWindow()` pendant la sortie est sans effet ; une sortie annulee retire la fenetre tout de suite. Au debut de la sortie d'une fenetre, ses fenetres imbriquees sont retirees tout de suite, sans sortie propre.
- Entrees et occultation : un etat interne de fermeture fait qu'une fenetre en sortie ne traite plus les evenements de souris et n'occulte plus ce qu'elle recouvre.
- `RemoveNestedWindow` (chemin de la liste deroulante) : quand la fenetre imbriquee a une sortie, le retrait est differe ; la methode rend `true` si la fenetre etait presente et la sortie acceptee, et `NestedWindows` la contient jusqu'a la fin du run. `AddNestedWindow` d'une fenetre dont la sortie est en cours annule cette sortie et rejoue l'entree au lieu de lever une exception : rouvrir la liste deroulante ou un popup pendant sa sortie fonctionne. Un `RemoveModalWindow` direct ou une affectation de `ModalWindow` restent synchrones : seule `TryCloseWindow()` differe le retrait d'une modale, et differer une modale veut dire differer l'appel a `RemoveModalWindow` (qui leve deja `NotifyWindowClosed`), pas ajouter une notification. Une modale en sortie continue de bloquer sa fenetre parente jusqu'a la fin du run : `HasModalWindow` n'est pas modifie. Les fenetres flottantes du docking sont retirees sans sortie (sur le chemin de re-dock, leur contenu est deja re-parente) ; le popup du selecteur de couleur suit la regle generale.
- Hors cycle : un `Desktop.Windows.Remove(window)` direct (chemin de `SampleBase` et de `DockingDemo`) retire la fenetre tout de suite, sans sortie ni `NotifyWindowClosed` : comportement actuel inchange. Sans entree ni sortie configuree, aucun changement de comportement ; le cout ajoute est la comparaison des fenetres racines par frame.

Criteres d'acceptation (tests `MGUI.Tests/Animation/WindowEnterExitTests.cs`) :

- fenetre racine : ajout a `Desktop.Windows` puis une frame, l'entree a joue ; `BringToFront`, `BringToBack` et l'activation au clic ne rejouent aucune entree ; `Desktop.Windows.Remove` : aucune sortie, aucun run residuel ;
- fenetre racine et fenetre imbriquee fermees par `TryCloseWindow()` : ordre `WindowClosing`, sortie, retrait, `NotifyWindowClosed`, `WindowClosed` ; la fenetre reste dessinee pendant la sortie et n'est plus dans sa liste apres ; fenetre modale : la pile modale est liberee a la fin de la sortie ;
- focus rendu au debut de la sortie ; fenetre non modale : un clic sur la zone de la fenetre en sortie atteint l'element du dessous, et le survol de la fenetre parente n'est plus supprime ; fenetre modale : la fenetre parente reste bloquee jusqu'a la fin de la sortie ;
- avec une sortie configuree, une fenetre flottante du docking est retiree tout de suite (fermeture et re-dock) et le popup du selecteur de couleur se rouvre pendant sa sortie sans exception ;
- liste deroulante : ouverture, fermeture, reouverture pendant la sortie ; selection d'un item pendant l'entree ; `RemoveNestedWindow` rend `true` et la fenetre reste dans `NestedWindows` jusqu'a la fin du run ;
- fermeture du parent pendant la sortie d'une fenetre imbriquee : aucune fenetre residuelle, aucune exception ;
- les tests existants qui ferment des fenetres restent verts sans modification : `AnimationManagerTests`, `CompositionTests`, `InputLifetimeRegressionTests`, `ResourceReferenceLifetimeRegressionTests`, `AuxiliarySurfaceLifecycleTests` ;
- section du sample : une fenetre et une liste deroulante avec entree et sortie ; la fenetre de la section se ferme par `TryCloseWindow()`.

Commit recommande : `animation: play enter and exit animations when windows and dropdowns open and close`

### ⏳ Y8. Tooltips, menus contextuels et groupe de theme (C1, partie 3)

But : les popups du desktop jouent entree et sortie, et le theme fournit les reglages par defaut des fenetres et popups.

Prerequis : Y7.

Perimetre : `MGUI.Core/UI/MGDesktop.cs` (`ActiveToolTip`, `ActiveContextMenu`, dessin et update des popups en sortie), `MGUI.Core/UI/UIViewState.cs` (emplacements des popups en sortie), `MGUI.Core/UI/MGToolTip.cs`, `MGUI.Core/UI/MGContextMenu.cs`, `MGUI.Core/UI/MGTheme.cs` (groupe `Animation` et bloc de copie), `MGUI.Core/UI/Styling/UIThemeValueInvalidation.cs`, `MGUI.Core/UI/XAML/Themes.cs` (`ThemeAnimationSettingsDefinition`), `MGUI.Core/UI/XAML/ThemeDefinitionBuilder.cs` (`ApplyAnimation`), themes XAML integres, tests, doc, sample.

- Tooltip et menu contextuel : entree a l'affichage ; au masquage, le popup en sortie occupe un emplacement dedie de l'etat de vue et reste dessine par le desktop jusqu'a la fin du run, puis disparait ; il ne traite plus d'entree et n'occulte plus rien. Un tooltip n'ayant pas de position propre (il est dessine a la position courante de la souris), l'emplacement du tooltip en sortie memorise le decalage de dessin releve au debut de la sortie et le desktop le dessine avec ce decalage, par un point d'entree de dessin dedie de `MGToolTip` : il ne suit plus la souris. Un nouveau popup du meme genre termine immediatement la sortie du precedent ; le meme tooltip remis en file pendant sa propre sortie annule cette sortie au lieu d'en demarrer une seconde. Les sous-menus suivent la meme regle, niveau par niveau ; `TryCloseActiveContextMenu` garde son contrat de retour.
- `MGThemeAnimationSettings` gagne `OpenDuration`, `CloseDuration`, `OpenEasing`, `CloseEasing` et `PopupEffect` (un `UIEnterExitEffect`, `FadeScale` par defaut). Les cinq valeurs sont classees `RenderOnly` dans l'inventaire, copiees par `MGTheme`, exposees par `ThemeAnimationSettingsDefinition` et lues par `ThemeDefinitionBuilder.ApplyAnimation` : durees et easings avec les parseurs existants, `PopupEffect` comme un enum nullable applique sur un simple test de presence, comme `Enabled`. Quand `Enabled` est vrai, `MGWindow`, `MGToolTip`, `MGContextMenu` et la liste deroulante prennent ces reglages pour entree et sortie, sauf reglage explicite sur l'element. `Enabled` reste faux dans tous les themes integres : aucun changement par defaut. Les autres controles ne lisent pas ce groupe. Les quatre genres de popup lisent `MGTheme.Animation` au moment ou l'entree ou la sortie demarre, sans surcharge de `OnThemeChanged` : l'inventaire des callbacks de theme de `ThemeValueInvalidationInventoryTests` ne change pas. Un changement de theme pendant un run ne l'interrompt pas.
- Re-ecriture de test listee : `MGUI.Tests/Architecture/ThemeValueInvalidationInventoryTests.cs`, uniquement le dictionnaire epingle (cinq entrees `Animation.*` en plus) ; aucune assertion modifiee ni supprimee.

Criteres d'acceptation (tests `MGUI.Tests/Animation/PopupEnterExitTests.cs` et complements de `StyleAndThemeAnimationTests`) :

- tooltip : entree, sortie dessinee a position figee (la souris bouge pendant le run, le rectangle de dessin enregistre ne bouge pas), remplacement immediat par un autre tooltip, annulation de la sortie quand le meme tooltip revient ; menu contextuel et sous-menu : meme chose, et `TryCloseActiveContextMenu` garde son contrat de retour ; un clic sur la zone d'un popup en sortie atteint l'element du dessous ;
- theme active : les quatre genres de popup jouent l'effet du theme ; un reglage explicite l'emporte ; theme desactive : aucun run ; chargement d'un theme XAML avec les nouveaux reglages en mode strict ; les cinq valeurs sont `RenderOnly` et l'inventaire reste complet ;
- section du sample : bascule du theme d'animation et demonstration sur tooltip, menu et liste deroulante.

Commit recommande : `animation: add popup enter and exit animations and theme open and close settings`

### ⏳ Y9. Cloture du programme

But : documentation exacte, sample coherent, decisions closes.

Perimetre : constats differes par les taches precedentes et rattaches a Y9 dans leur statut (sample), `Docs/animation-architecture.md` (relecture contre le code : etat final, aucune mention de version), `Docs/scenario-validation-index.md` (`SCN-ANIM-001` : une mention par capacite livree, ADR-0011 ajoutee a sa liste d'ADR), ce fichier (taches a ✅ ou 🧪, candidats livres retires du backlog ci-dessous), ADR-0011 passee a `Accepted` dans son fichier et dans la ligne d'index de `Docs/decisions/README.md`, `MGUI.Samples` construit en `--no-incremental`.

Validation manuelle (auteur) : scenario `SCN-ANIM-001`, nouvelles sections du sample. Le programme reste 🧪 jusqu'a cette validation.

Commit recommande : `docs: close the animation V5 program`

## Points ouverts

- Aucun a la redaction.

## Backlog restant (non retenu pour ce programme)

Reliquats des programmes precedents, differes explicitement (ADR-0008, ADR-0009) : remap temporel d'un composite ; presets CSS nommes (`ease`, `ease-in`...) ; effets de `MGTimer` sur le moteur ; applicabilite par predicat ; chemins de propriete generiques dans une brush ; interpolation entre deux textures ; option de cycle partage pour une surbrillance.

Points ouverts laisses a l'auteur (detail dans les ADR) : precedence `Checked` contre `Disabled` sur `MGToggleButton` ; remplacement gracieux entre une animation de couleur sur `BorderBrush` et un run de surbrillance ; `AutoStart` lu au demarrage seulement ; homogeneisation des messages de refus de `UIAnimationSerializer` et traitement des membres JSON inconnus ; identite d'un easing tiers dans la signature de refresh de style ; contribution `Theme` durable apres un swap de conteneur en plein run ; une brush non gelee partagee enracine les elements qui l'observent ; `Brushes` des composites en `IList<T>` (changement d'API a annoncer).

### ⏳ C5. Interpolation a ressort

But : un `UISpringAnimation<T>` (raideur, amortissement) qui conserve la vitesse quand une transition se recible en cours de run. Esquisse : animation a etat (position, vitesse) integree par pas fixe, sans allocation ; `UITransition<T>.Spring(...)` ; fin sous un seuil. Risques : `Progress` sans sens strict, `Seek` d'une preview.

### ⏳ C6. Horloge par sous-arbre et mouvement reduit

But : mettre en pause ou ralentir les animations d'une fenetre ou d'un sous-arbre ; un interrupteur global qui termine les transitions instantanement. Esquisse : `UIAnimation.SpeedRatio` ; `MGWindow.AnimationTimeScale` / `IsAnimationPaused` ; `UIAnimationClock.ReducedMotion`. Risques : composite dont les enfants sont sur plusieurs fenetres.

### ⏳ C7. Declencheurs XAML

But : `<Element.Triggers><EventTrigger Event="Click"><BeginStoryboard>...` et un storyboard declaratif. Esquisse : DTO `Storyboard` XAML derive de `UIAnimationNodeDto`, `EventTrigger` et `PropertyTrigger`, resolution des `TargetName`, fusion par les styles. Les animations d'entree et de sortie declarees en XAML (au-dela des effets predefinis de ce programme) en dependent. Risques : surface du loader strict.

### ⏳ C8. Marqueurs de temps

But : un rappel a un offset donne d'une animation ou d'un clip (son, particule). Esquisse : `UIAnimation.Markers`, evenement `MarkerReached`, `markers` dans les DTO, jamais declenches par `Seek`. Risques : marqueurs franchis par un grand delta.

### ⏳ C9. Animation le long d'un chemin

But : deplacer un element sur une polyline ou une courbe de Bezier, avec orientation optionnelle. Esquisse : `UIPathAnimation`, parametrage par longueur d'arc pre-calcule, serialisable.

### ⏳ C11. Cibles manquantes

But : `BorderThickness` (pilote), `CornerRadius`, `Width` / `Height` / `MaxWidth` / `MaxHeight`, `Slider.Value`, `ProgressBar.Value`, `Foreground` des controles hors `MGTextBlock`, cadre d'une `MGImage`. Esquisse : une cible par propriete selon la famille existante, interpolateur `CornerRadius`, tests d'applicabilite (table `Expected`).
