# Taches systeme d'animation (V3)

## Objectif

Livrer la V3 du systeme d'animation de MGUI selon l'ADR-0008 (`Docs/decisions/0008-animation-v3-editor-readiness-and-remaining-limits.md`, statut Proposed tant que les dix questions ci-dessous ne sont pas tranchees), au-dessus de la V1 (ADR-0006) et de la V2 (ADR-0007, `Docs/Tasks/animation-v2-tasks.md`, `Docs/animation-architecture.md`) : rendre le moteur pret pour l'editeur de timeline du moteur de jeu de l'auteur (chemins animables par type d'element, `Seek`, clip de keyframes multi-pistes, serialisation d'un storyboard, easings de Bezier en texte), lever les deux limites V2 sur les pilotes (ecriture sous un run non vue avant la fin, valeur locale qui masque un etat nomme), donner au `MGToggleButton` un slot `Checked` sans toucher aux enums, migrer `MGTextBlock.TextCharactersPerSecond` et `MGHighlightBorderBrush` sur l'horloge du moteur, et, en dernier, le refresh a chaud des transitions et etats des styles.

Contraintes non negociables : celles de la V1 et de la V2 (pas de dependency property system, store ADR-0005 pour les pilotes, zero cout pour un element qui n'anime rien, tokens interdits de `RenderingBoundaryArchitectureTests`, renderer neutre, une ADR par decision) ; les enums `PrimaryVisualState` / `SecondaryVisualState`, `UIValueSlot` et `UIResolvedPropertyStore.SlotCount` restent inchanges ; l'ordre global de precedence ADR-0005 reste inchange (un nouveau palier 95 s'insere, rien ne bouge) ; aucune API publique V1 ou V2 n'est renommee ; les tests V1 et V2 (`MGUI.Tests/Animation/*`) restent verts sans modification sauf ajout ou re-ecriture explicitement listee dans une tache.

Hors programme (moteur de jeu de l'auteur) : widgets de timeline, scrubber, edition de courbes, undo / redo, fichiers projet, resolution des elements par nom. Differe (ADR-0008) : remap temporel d'un composite, presets CSS nommes (`ease`, `ease-in`...), effets de `MGTimer` sur le moteur, applicabilite par predicat.

## Historique du fichier

- 13 septembre 2026 : creation apres l'audit V3 en lecture seule a HEAD `e52880c` (trois lecteurs independants : styles et `Checked` ; extensions du moteur et limites pilotes ; consommateurs de temps et outillage editeur). Aucune tache commencee ; dix questions ouvertes a l'auteur.

## Contexte : faits verifies le 13 septembre 2026 (HEAD `e52880c`, lecture seule)

- Easings : `UIEasing` (`MGUI.Core/UI/Animation/Easing/UIEasing.cs`) : dix-neuf `DelegateEasing` dans un registre insensible a la casse, `TryGet(name)` (`:99`) est une recherche de dictionnaire sans repli, `Register` (`:83`) est le point d'extension, `Names` (`:80`) enumere les cles ; `IUIEasingFunction.Ease(float)` sans contrainte de bornes (depassement autorise). Consommateurs, tous par nom : `Transition.Easing` (`XAML/Animation.cs:86`, `:108`), `MGThemeAnimationSettings.*Easing` (`MGTheme.cs:201-203`, `ThemeDefinitionBuilder.ParseAnimationEasing`, `UIThemeTransitions.Install:42`), `UIKeyFrameDto.Easing` (`KeyFrames/UIKeyFrameSerializer.cs:170`, resolu par `UIKeyFrame<T>.ResolveEasing`), `UIAnimationBuilder<T>.Ease(string)` (`UIAnimateExtensions.cs:163`). Aucun code de Bezier sous `Animation/` (`Graph/Rendering/GraphBezierGeometry.cs` echantillonne une courbe, il n'inverse pas x(t)).
- Composition : `UIAnimationGroup` (`Composition/UIAnimationGroup.cs`) : `TargetKey` synthetique (`:27`), `IsStoreBacked` faux (`:62`), `BaseValueBoxed` nul (`:66`), `ApplyProgress` (`:87`) protege par `_IsDriving` et ne fait que `StartDueChildren` ; chaque enfant est enregistre sur son proprietaire (`StartChild:131`) et avance par le manager avec son propre `Advance(delta)` incremental. `UIKeyFrameAnimation<T> : UIPropertyAnimation<T>` (`KeyFrames/UIKeyFrameAnimation.cs:16`, `OnStarting:31`, `ApplyProgress:60`) pilote un seul chemin ; `UIKeyFrameTrack<T>` ne connait pas de chemin ; `UIKeyFrameSerializer` serialise une piste par document (`Serialize<T>:31`, `Deserialize<T>:48`, `UIKeyFrameTrackDto:162 { Version, ValueType, Frames }`, `UIKeyFrameDto:170 { Offset, Value, Easing }`). Le sample construit a la main un `UIStoryboard` de trois enfants et une sequence avec un pop en keyframes (`MGUI.Samples/Features/AnimationDemo.xaml.cs:136-172`).
- Seek : `UIAnimation.Advance(TimeSpan delta)` est `internal` et incremental (`UIAnimation.cs:215`, `Elapsed += delta`), retour anticipe hors `Running` / `Delayed` (`:217-220`) ; `Begin` interne (`:188`) ; `UIAnimationCollection.Start` lie toujours au manager du desktop (`UIAnimationCollection.cs:19`) ; `InternalsVisibleTo` : `MGUI.Tests` et `CasaEngine.Tests` seulement (`MGUI.Core/Properties/AssemblyInfo.cs:6-7`). Une relecture image par image via `IUIDesktopRuntime.ApplyFrame` (precedent `MGUI.Tests/Tooling/StableDiagnosticIdTests.cs:362-402`) n'avance qu'en avant, rejoue tous les evenements et re-layoute a chaque image pour un pilote de layout.
- Applicabilite : `UIAnimationTargets.Paths` / `GetValueType` (`UIAnimationTargets.cs:38-51`) sans type proprietaire ; `IUIAnimationTarget<T>` : `Path`, `IsStoreBacked` ; contraintes par cast : `ForegroundTarget.Require` (`Targets/UIColorAnimationTargets.cs:142-144`), `ProgressButtonValueTarget.Require` (`Targets/UIBuiltInAnimationTargets.cs:178`) ; environ vingt-et-une classes de cibles (onze, quatre, six).
- Store et transitions : `GetUnderlyingValue => GetValue` sur les quatre cibles couleur (`UIColorAnimationTargets.cs:94, 122, 157, 195`) ; `UITransition<T>.HandleChanged` (`UITransition.cs:170-177`) lit cette valeur physique ; `UIResolvedPropertyStore.Set<T>` (`Styling/UIResolvedPropertyStore.cs:141`, insertion triee `:161`), `TryGetWinner<T>` (`:232`) rend la premiere contribution ; `EnumerateResolvedContributions` (`MGElement.cs:1526-1538`) sert deja de contournement dans `UIStoreBackedTargets.Restore` (`IUIStoreBackedAnimationTarget.cs:51-78`, branche `{Animation}` seul `:74-78`) et `UIVisualStateApplier<T>.CaptureBase` lit `UITransition<T>.SettledValue` (`States/UIVisualState.cs:200-206`) ; `UIPropertyAnimation<T>.ReadCurrentValue` (`UIPropertyAnimation.cs:39`) doit continuer a lire la valeur animee (ADR-0006 : une animation explicite part de la valeur courante). Cas speciaux de dormance `Background` / `DefaultTextForeground` dans `TryGetResolvedPilotValue<T>` (`MGElement.cs:1353-1380`).
- Precedence (`Styling/UIValuePrecedence.cs:11-15`) : `Template` 60, `VisualState` 70, `LocalBinding` 80, `LocalValue` 90, `Animation` 100 ; le setter d'etat ecrit a 70 (`UIVisualState.cs:187`) ; `VisualStatesTests.ALocalValue_ShadowsANamedStateOnAPilot` (`:196-215`) verrouille la limite.
- `Checked` : `PrimaryVisualState` a quatre membres (`VisualState.cs:17-27`), `VisualStateSetting<T>.GetValue(PrimaryVisualState)` (`:129-137`) est le resolveur unique (`GetUnderlay:296`, neuf sites : `MGElement.cs:3010`, `:4310`, `:4318`, `MGGridSplitter.cs:568`, `MGUniformGrid.cs:993`, `MGProgressBar.cs:447`, `:454`, `MGResizeGrip.cs:282`, `UISymbolElements.cs:280`) ; `ResolvePrimaryVisualState` (`MGElement.cs:2898-2916`) ; DTO de theme a quatre valeurs (`XAML/Themes.cs:133-161`, `ThemeDefinitionBuilder.cs:791-835`) ; `UIValueSlot` a six membres et `SlotCount = 6` (`UIResolvedPropertyStore.cs:29-30`, `:91`) ; seul `MGToggleButton.IsChecked` ecrit `IsSelected` (`MGToggleButton.cs:93`), `CheckedBackgroundBrush` / `CheckedTextForeground` sont des alias documentes de `SelectedValue` (`:46-70`) ; `MGCheckBox` (`MGCheckBox.cs:41, 96-98, 224-226`) et `MGRadioButton` (`MGRadioButton.cs:139, 249-251, 294-296`) dessinent leur etat coche par des elements enfants ; `FocusTests.cs:196-199, 392-404` enumerent les quatre membres.
- Refresh de styles : `ElementStyleScope` (`XAML/ElementStyleScope.cs:9-44`) capture une fois `DefinitionType`, `ElementType`, `StyleNames`, `IsStyleable`, `UsesResourceStyles`, `InlineStyles`, `StyledPropertyNames`, rien sur les transitions ni les etats ; `ElementStyleRefresher.RefreshElement` (`:176-288`) ne lit que des `Setter` (`CollectSetters:290-299`, `FindNamedStyle:302-313`, `Pass.GetImplicitStyles:144-153`), onze `RefreshableProperty` (`:51-90`), le reste en `Skipped` / `NotRefreshable` (`Styling/UIStyleRefreshResult.cs:7-53`) ; transfert unique dans `Element.ApplyBaseSettings` (`Element.cs:619-645`, styles puis element, remplacement par chemin ou nom par `UITransitionCollection.Add:31-51` et `UIVisualStateCollection.Add:35-55`) ; `CollectStyleAnimation` (`Element.cs:1234-1245`) ; `MGResources.AddImplicitStyle` fusionne par chemin et nom (`MGResources.cs:559-584`) ; `OnStyleAdded` / `OnStyleRemoved` (`:629, 637, 646-647`) sans abonne ; `RefreshedStyleProperties` (`MGElement.cs:1556`) ; suite `MGUI.Tests/Architecture/StyleRefreshTests.cs`.
- Consommateurs de temps : `MGTextBlock.UpdateSelf` (`MGTextBlock.cs:1398-1404`) cumule `TextProgress` (`double?`), remis a zero par le constructeur (`:1237`) et le setter de `TextCharactersPerSecond` (`:711`) seulement, pas par un changement de `Text` ; `RemainingCharacters` au dessin (`:1420`). `MGHighlightBorderBrush.Update` (`Brushes/Border Brushes/MGHighlightBorderBrush.cs:504`) cumule `UA.FrameElapsed / CycleDuration`, brush partagee et dedupliquee par frame (`:481-483`), `Target` optionnel (`:399-457`) : pas un element, pas de cle (proprietaire, chemin). Chronometres a garder : `MGTimer` (`:272-273` Flicker sur `Opacity`, `:332` compte a rebours avec son `TimeScale`, `:339-341` Shake), `MGStopWatch` (`:237`), delai de tooltip (`MGElement.cs:3727-3746`, horloge murale), clignotement du caret (`Text/MGTextCaret.cs:370-371`), repetition de `MGButton` (`MGButton.cs:318-345`). Sans consommation de temps : `MGProgressBar`, `MGScrollViewer` (`:705-724`), `MGSpoiler`, `Docking/Controls/MGDockAutoHideDrawer.cs`, `Graph/Interaction/GraphViewportTransform.cs`. `UpdateBaseArgs` (`MGUI.Shared/Rendering/RenderLoopArgs.cs`) est deja etendu par `with { }` dans `MGDesktop.Update` (`MGDesktop.cs:1400`, `PaintRegistry`) apres `Animations.Update` (`:1377`).
- Outillage : `UIToolingService.CaptureElementDebugView` (`Tooling/UIToolingService.cs:331`, `CaptureAnimations:429-448`) -> `UIElementDebugView.Animations` (`UIAnimationDebugView(Kind, Path, State, Progress, Name)`) et `VisualStateName` ; `MGDesktop.Animations` : `Clock` (`Time`, `DeltaTime`, `TimeScale`, `IsPaused`), `ActiveCount`, `ActiveAnimations` (liste vivante) ; aucun DTO pour `UIStoryboard`, `UISequenceAnimation`, `UIDelayAnimation`, `UIPropertyAnimation<T>`, `UIVisualState` ; aucun DTO XAML `Storyboard`.

## Questions a l'auteur (a trancher avant U1 ; recommandation en gras)

1. `Checked` : (a) membre `PrimaryVisualState.Checked` avec un slot dans les conteneurs, le store (`SlotCount` 7), les DTO de theme et de XAML, le refresher (douze fichiers et plusieurs tests epingles, risque de fond vide pour toute case ou radio dont `CheckedValue` n'est pas pose) ; (b) **garder `IUICheckable` et l'etat nomme V2, ajouter `VisualStateBrush<T>.CheckedValue` (fill et couleur seulement) consomme par `MGToggleButton` avec repli sur `SelectedValue` ; `MGCheckBox` et `MGRadioButton` inchanges ; propriete code seulement, pas de champ DTO de theme tant qu'aucune application ne le demande.**
2. Valeur locale contre etat nomme : (a) garder la limite ; (b) inverser globalement `VisualState` et `LocalValue` (rejete : casse l'ordre ADR-0005 pour tous les pilotes) ; (c) **`UIVisualState.OverridesLocalValue` (defaut faux), ecrit au palier `VisualStateOverride = 95` avec `Kind` inchange, expose en XAML sur `VisualStateDefinition` ; granularite par etat, pas par setter.** A confirmer : ce palier passe aussi au-dessus de `LocalBinding` (80).
3. Bezier : **syntaxe canonique `cubic-bezier(x1,y1,x2,y2)`, `bezier:x1,y1,x2,y2` accepte en entree, `ToString` en forme CSS ; `X1` / `Y1` / `X2` / `Y2` publics en lecture (relecture par un editeur de courbes) ; pas de presets CSS nommes (`ease`, `ease-in`...) ; mise en cache par litteral dans le registre (un editeur qui edite en direct doit quantifier ses points).**
4. Clip de keyframes : **un element par clip (l'appelant fournit l'element, comme `UIKeyFrameSerializer.Deserialize<T>` aujourd'hui) ; `Duration` du clip faisant autorite sur les pistes ; `Format` / `Parse` de `UIKeyFrameSerializer` passes `internal static` et partages ; remap temporel d'un composite differe explicitement.**
5. `Seek` : (a) `InternalsVisibleTo` vers l'assembly du moteur de jeu ; (b) **`UIAnimation.Seek(TimeSpan)` public, sans evenement, avant et arriere, autorise sur un composite, reserve aux instances de preview conduites par l'hote (jamais sur une animation enregistree au manager vivant) ; passe de conception sur `Completed -> Running` avant implementation.**
6. Serialisation d'un storyboard : **resolution des elements par delegue `Func<string, MGElement>` fourni par l'hote ; meme ensemble ferme de types de valeur que le serialiseur de keyframes, une cible applicative doit enregistrer une paire `Format` / `Parse` (point d'extension documente, miroir de `UIInterpolators.Register<T>`) ; dossier `KeyFrames/` a cote du serialiseur existant, DTO versionnes sans instance de controle (precedent `GraphSerializer`).**
7. Refresh a chaud des transitions et etats : (a) le retrait d'une transition ou d'un etat qui n'est plus style **restaure immediatement** (`Transitions.Remove` garde la valeur courante, `VisualStates.Remove` restaure) ; (b) **provenance par `UIValueSourceKind` (`ImplicitStyle` / `ExplicitStyle`)** plutot qu'un booleen ; (c) **tout chemin enregistre** (pas seulement les onze proprietes rafraichissables des setters) ; (d) **derniere tache du programme**, la seule de taille L cote styles, a confirmer qu'elle reste voulue.
8. Migrations : **`MGTextBlock.TextCharactersPerSecond` sur le moteur (contrat public en caracteres par seconde conserve, remise a zero de la progression sur un changement de `Text` : changement de comportement a annoncer), `MGHighlightBorderBrush` sur l'horloge du desktop par `UpdateBaseArgs.AnimationDeltaTime` (source de temps seulement, pas de propriete) ; `MGTimer` (Flicker, Shake, compte a rebours), `MGStopWatch`, delai de tooltip, caret, bouton a repetition : gardes tels quels.** Consequence a accepter : pause et `TimeScale` de `MGDesktop.Animations.Clock` s'appliquent au texte revele et aux brushes de surbrillance.
9. Applicabilite : **un `Type? RequiredOwnerType` unique (membre d'interface par defaut nul), pas de predicat ; toutes les contraintes actuelles sont des casts vers une classe concrete.**
10. Ordre : **U1 a U5 (gains rapides et limites pilotes), puis U6 a U8 (preparation editeur), puis U10 (migrations), U9 (refresh a chaud) et U11 (sample et validation).** Alternative : U6 a U8 d'abord si l'editeur est la prochaine etape immediate.

## Decisions de conception derivees (session principale, contestables avant U1)

- Bezier : `UICubicBezierEasing : IUIEasingFunction` (`Easing/UICubicBezierEasing.cs`, algorithme UnitBezier : Newton sur x(u) - t avec repli par bissection quand la derivee est proche de zero, `X1` / `X2` bornes a [0,1] a la construction, `ArgumentOutOfRangeException` sinon) ; `UICubicBezierEasing.TryParse(string)` ; `UIEasing.TryGet` : recherche par nom puis `TryParse`, l'instance construite est inseree dans le registre sous le litteral exact ; `Names` continue d'enumerer les cles (les dix-neuf built-ins restent un sous-ensemble, `EasingTests.Names_ListsTheNineteenBuiltIns` inchange).
- Applicabilite : `IUIAnimationTarget<T>.RequiredOwnerType` (`Type?`, membre d'interface par defaut => null) ; `UIAnimationTargets.GetOwnerType(path)` (null pour un chemin inconnu, comme `GetValueType`) ; renseigne par `ForegroundTarget` (`MGTextBlock`), `ProgressButtonValueTarget` (`MGProgressButton`) et toute cible qui caste.
- Store : `UIResolvedPropertyStore.TryGetWinnerExcluding<T>(property, slot, UIValueSourceKind excluded, out UIResolvedValue<T>)` (balayage lineaire de la liste triee, onze contributions au plus) ; `MGElement.TryGetResolvedPilotValueExcluding<T>` interne avec les memes branches de dormance ; `IUIStoreBackedAnimationTarget<T>.TryGetValueBelowAnimation(element, out T)` implemente par les neuf cibles store ; `UITransition<T>.HandleChanged` l'utilise pour une cible store, `GetUnderlyingValue` reste pour une cible simple ; ensuite `UIVisualStateApplier<T>.CaptureBase` perd son cas special et la branche `{Animation}` de `UIStoreBackedTargets.Restore` fusionne avec la branche « plus rien » ; les tests de `VisualStatesTests` qui decrivent la limite sont re-ecrits, pas supprimes.
- Etat contre local : `UIValuePrecedence.VisualStateOverride = 95` ; `UIVisualStateApplier<T>` construit `_LastSource` avec ce palier quand `UIVisualState.OverridesLocalValue` est vrai, `Apply` et `Restore` inchanges par ailleurs ; verification prealable par `rg` qu'aucun `switch` exhaustif ne porte sur `UIValuePrecedence` (seuls `Kind` sont enumeres) ; `VisualStateDefinition.OverridesLocalValue` en XAML.
- `Checked` : `VisualStateBrush<T>.CheckedValue` (`T`, defaut null, copie par les constructeurs d'heritage) et `GetValue(VisualState state, bool isChecked)` ; `MGToggleButton.CheckedBackgroundBrush` / `CheckedTextForeground` lisent `CheckedValue ?? SelectedValue` et ecrivent `CheckedValue` ; `IsChecked` continue de poser `IsSelected` (`MGToggleButton.cs:93`) ; le chemin generique `GetUnderlay` / `GetValue(PrimaryVisualState)` et ses neuf sites ne changent pas ; limite documentee : `Background.Checked` n'est ni une cible ni un slot du store.
- Clip : `KeyFrames/UIKeyFrameClipSerializer.cs` : `UIKeyFrameClipDto { Version, Duration, Tracks[] { Property, ValueType, Frames[] } }` ; `Deserialize(json)` resout le type de valeur par `UIAnimationTargets.GetValueType(path)`, construit `UIKeyFrameAnimation<>` par `MakeGenericType` (precedent `UITransition.Create`, `UITransition.cs:55`), pose la piste et la duree partagee, rend un `UIStoryboard` ; `Serialize(storyboard)` pour un storyboard ne contenant que des animations keyframes ; refus explicite : chemin inconnu, type inconnu, version inconnue, enfant qui n'est pas une animation keyframes.
- `Seek` : les maths de progression et d'iteration de `Advance` (`UIAnimation.cs:236-274`) extraites dans un helper prive partage ; `Seek(TimeSpan elapsed)` public : pose `Elapsed`, un seul `ApplyProgress`, aucun evenement, aucun `Manager.NotifyFinished`, aucun changement d'etat vers `Completed` (une preview qui atteint la fin reste `Running` jusqu'a `Cancel`) ; `UIAnimationGroup.Seek` positionne chaque enfant a `elapsed - offset` (clamp) ; refuse (`InvalidOperationException`) si `Manager != null` (animation enregistree) ; `Begin` d'une instance de preview par une surcharge interne exposee par une fabrique publique `UIAnimationPreview.Attach(element, animation)` (sans manager, `InheritsBaseValue` faux) ; le sweep du manager et `FinishedChildren` des groupes ne voient jamais une instance de preview.
- Storyboard : `KeyFrames/UIAnimationSerializer.cs` : `UIAnimationNodeDto { Kind (Storyboard | Sequence | Delay | Property | KeyFrames), Element (nom ou vide = racine), Path, ValueType, Duration, Delay, RepeatCount, RepeatForever, AutoReverse, FillBehavior, CancelBehavior, Name, From, To, Easing, Track, Children[] }`, `Version` 1 ; `Serialize(UIAnimation, Func<MGElement, string> nameOf)` ; `Deserialize(json, Func<string, MGElement> resolveElement)` ; erreurs explicites listant les chemins connus.
- Refresh a chaud : `UITransition.Provenance` et `UIVisualState.Provenance` (`UIValueSourceKind?`, internes, poses par `Transition.ToTransition` / `VisualStateDefinition.ToVisualState` quand ils viennent d'un style) ; `ElementStyleScope.OwnTransitionPaths` / `OwnVisualStateNames` captures dans `ProcessStyles` ; `ElementStyleRefresher.RefreshElement` : construit la liste des transitions et etats des styles courants (implicites puis nommes, meme ordre que `CollectStyleAnimation`), ajoute ou remplace ce que l'element ne declare pas lui-meme, retire ce qui etait style et ne l'est plus si l'entree courante porte encore la provenance ; `MGElement.RefreshedStyleTransitionPaths` / `RefreshedStyleVisualStateNames` ; `UIStyleRefreshResult.WrittenTransitions`, `ClearedTransitions`, `WrittenVisualStates`, `ClearedVisualStates` ; deux refreshs sans changement n'ecrivent rien.
- Migrations : cible `TextBlock.TextProgress` (`double`, simple, non observable, `MGTextBlock` seulement, ecrite par `ApplyAnimatedTextProgress`) ; `SyncTextProgressAnimation` depuis les setters de `Text` et `TextCharactersPerSecond` (part restante `(1 - progress) * NumCharacters / TextCharactersPerSecond`, `HoldEnd`, `KeepCurrent`, sous-classe privee neutralisant la restauration forcee comme `MGProgressButton.DurationRun`, rien hors de l'arbre) ; `UpdateBaseArgs.AnimationDeltaTime` (`TimeSpan`, delta deja mis a l'echelle, zero en pause) pose par `MGDesktop.Update` apres `Animations.Update` ; `MGHighlightBorderBrush.Update` cumule `UA.AnimationDeltaTime`.

## Consignes de travail pour l'agent IA

- Executer les tranches dans l'ordre ; une tranche = un commit ; mettre a jour le statut dans ce fichier dans le meme commit.
- Blocage : marquer ⛔, decrire, s'arreter.
- Pas de refactor hors perimetre ; aucun renommage d'API publique V1 ou V2.
- Tests sur le comportement observable avec le harnais headless (`AnimationTestScene`, frames explicites) ; zero allocation par tick pour un run en cours ; aucun slot pour un element sans animation ; `Seek` sans allocation.
- Chaque tranche modifiant `MGElement`, `VisualState.cs`, `MGDesktop`, le store, `UITransition`, `UIAnimation`, `Element.cs`, `ElementStyleRefresher.cs`, `MGTheme`, `UpdateBaseArgs` ou un controle est a risque : verification independante en lecture seule obligatoire (modele Sonnet), constats consignes dans le statut de la tranche.
- Toute decision prise en cours de tranche est ajoutee a ADR-0008 au moment ou elle est prise.
- Ne jamais lancer `MGUI.Samples` depuis un agent ; le construire a chaque tranche.
- Docs en francais sans accents.

## Taches

### ⚪ U1. Easings de Bezier

But : `UICubicBezierEasing`, forme textuelle, repli de `UIEasing.TryGet`.

Etat actuel : voir « Easings ».

Travail attendu :

- `Easing/UICubicBezierEasing.cs` : quatre flottants publics, `Ease` (Newton puis bissection), `TryParse` (`cubic-bezier(...)` et `bezier:...`), `ToString` en forme CSS, validation a la construction.
- `UIEasing.TryGet` : repli sur `TryParse` puis mise en cache sous le litteral ; `Names` inchange dans son contrat.
- Docs : section « Easings » de `Docs/animation-architecture.md` (syntaxe, cache, limite du cache pour une edition en direct).
- Tests `MGUI.Tests/Animation/EasingTests.cs` (extension) : valeurs connues des presets CSS (`ease`, `ease-in`, `ease-in-out`, `ease-out`) aux bornes et a mi-course, convergence pres des bornes et `X1 == X2`, `TryParse` accepte les deux formes et refuse le malforme et le hors bornes, `TryGet` resout, met en cache (meme instance) et expose le litteral dans `Names` ; `KeyFrameTests` : round-trip JSON avec un easing de Bezier ; `XamlAnimationTests` et `StyleAndThemeAnimationTests` : `Easing="cubic-bezier(...)"` et `HoverEasing` de theme acceptes.

Commit recommande : `animation: add cubic Bezier easings`

### ⚪ U2. Applicabilite des cibles

But : `IUIAnimationTarget<T>.RequiredOwnerType`, `UIAnimationTargets.GetOwnerType`.

Etat actuel : voir « Applicabilite ».

Travail attendu :

- `IUIAnimationTarget.cs` : membre d'interface par defaut `Type? RequiredOwnerType => null` ; `UIAnimationTargets.GetOwnerType(path)` ; les cibles qui castent renseignent leur type (`Foreground`, `ProgressButton.Value`, et toute autre trouvee par `rg "element as MG"` dans `Targets/`).
- `UIToolingService` : la debug view d'un element peut lister les chemins applicables (`ApplicablePaths`, optionnel, sans allocation par frame : calcule a la capture).
- Docs : table des cibles avec une colonne « Element requis ».
- Tests `MGUI.Tests/Animation/TargetApplicabilityTests.cs` : chaque chemin framework rend le type attendu ou null, chemin inconnu rend null sans exception, un filtre `Paths.Where(GetOwnerType is null or assignable)` sur un `MGButton` exclut `Foreground` et `ProgressButton.Value`.

Commit recommande : `animation: expose the owner type of the animation targets`

### ⚪ U3. Valeur sous l'animation pour les pilotes

But : `TryGetWinnerExcluding`, `TryGetValueBelowAnimation`, retarget immediat d'une transition sur un pilote.

Etat actuel : voir « Store et transitions ».

Travail attendu :

- `UIResolvedPropertyStore.TryGetWinnerExcluding<T>` (+ test unitaire a cote de `Set` / `Unset` / `TryGetWinner`) ; `MGElement.TryGetResolvedPilotValueExcluding<T>` interne (memes branches de dormance que `TryGetResolvedPilotValue<T>`, tests dedies pour `Background` et `DefaultTextForeground`).
- `IUIStoreBackedAnimationTarget<T>.TryGetValueBelowAnimation` sur les neuf cibles store (quatre couleurs, `Margin`, `Padding`, `MinHeight`, deux gradients).
- `UITransition<T>.HandleChanged` : valeur sous `Animation` pour une cible store ; `UIPropertyAnimation<T>.ReadCurrentValue` inchange (test de non-regression : une animation explicite demarree pendant le run part de la valeur animee).
- Simplifications : `UIVisualStateApplier<T>.CaptureBase` sans cas special de transition ; `UIStoreBackedTargets.Restore` sans branche `{Animation}` ; `VisualStatesTests.LeavingAStateMidTransitionOnAThemeBackground_ComesBackToTheThemeColour` re-ecrit (le retour commence a la sortie, plus a la fin du run).
- Docs : « Transitions », « Etats visuels nommes », « Limites connues » (la limite « vue a la fin du run » disparait pour les transitions).
- Tests `MGUI.Tests/Animation/TransitionTests.cs` (extension) : ecriture locale du fond pendant un run de transition couleur re-ciblee a la frame suivante ; sortie d'etat nomme pendant un run vue immediatement.

Commit recommande : `animation: retarget a pilot transition on a write made under its run`

### ⚪ U4. Un etat nomme qui l'emporte sur une valeur locale

But : `UIVisualState.OverridesLocalValue`, palier `VisualStateOverride = 95`.

Etat actuel : voir « Precedence ».

Travail attendu :

- `UIValuePrecedence.VisualStateOverride = 95` (verification prealable : aucun switch exhaustif sur `UIValuePrecedence`) ; `UIVisualState.OverridesLocalValue` ; `UIVisualStateApplier<T>` construit la source avec ce palier quand le drapeau est vrai ; `VisualStateDefinition.OverridesLocalValue` en XAML ; `UIToolingService` rend la precedence (deja dans `UIValueResolutionSource`).
- Docs : « Etats visuels nommes » (le drapeau, ce qu'il passe : `LocalValue` et `LocalBinding`, ce qu'il ne passe pas : `Animation`).
- Tests `VisualStatesTests` (extension) : scenario de `ALocalValue_ShadowsANamedStateOnAPilot` avec le drapeau (l'etat gagne, source `VisualState` a 95), sans le drapeau (inchange), un run `Animation` gagne toujours, XAML round-trip du drapeau, `ResolvedValueSourceDiagnosticsTests` voit le palier.

Commit recommande : `animation: let a named visual state override a local value`

### ⚪ U5. Slot Checked du toggle

But : `VisualStateBrush<T>.CheckedValue`, `MGToggleButton.CheckedBackgroundBrush` reel.

Etat actuel : voir « Checked ».

Travail attendu :

- `VisualState.cs` : `CheckedValue` sur `VisualStateBrush<T>` (copie par les constructeurs d'heritage, `Copy`), `GetValue(VisualState, bool isChecked)` ; `MGToggleButton` : `CheckedBackgroundBrush` / `CheckedTextForeground` lisent `CheckedValue ?? SelectedValue`, ecrivent `CheckedValue`, `DrawBackground` du toggle consomme `GetValue(state, IsChecked)` ; `IsChecked` continue de poser `IsSelected`.
- Docs : « Etats visuels nommes » (le slot `Checked` du toggle, limite : ni cible ni slot du store), `Docs/styling-theme-architecture.md` (mention).
- Tests `MGUI.Tests/Animation/ToggleCheckedSlotTests.cs` : toggle avec `CheckedBackgroundBrush` seul dessine cette brush coche ; toggle avec `SelectedBackground` seul inchange avant et apres ; `IsChecked` pose toujours `IsSelected` ; `MGCheckBox` et `MGRadioButton` inchanges (tests existants verts) ; l'etat nomme `Checked` V2 reste tel quel (`VisualStatesTests` verts).

Commit recommande : `animation: give the toggle button a real Checked brush slot`

### ⚪ U6. Clip de keyframes multi-pistes

But : `UIKeyFrameClipDto`, `UIKeyFrameClipSerializer` -> `UIStoryboard`.

Etat actuel : voir « Composition ».

Travail attendu :

- `KeyFrames/UIKeyFrameClipSerializer.cs` (DTO, `Serialize(UIStoryboard)`, `Deserialize(json)`), `Format` / `Parse` de `UIKeyFrameSerializer` partages ; duree du clip faisant autorite ; refus explicites.
- Docs : « Keyframes » (format du clip).
- Tests `MGUI.Tests/Animation/KeyFrameClipTests.cs` : clip a trois pistes (float, Vector2, Color) joue en parallele comme le storyboard du sample, round-trip, chemin inconnu, type inconnu, version inconnue, enfant non keyframes refuse, zero allocation par tick du storyboard construit.

Commit recommande : `animation: add the multi-track keyframe clip format`

### ⚪ U7. Seek

But : `UIAnimation.Seek(TimeSpan)` et `UIAnimationPreview.Attach`.

Etat actuel : voir « Seek ».

Travail attendu :

- Passe de conception d'abord (statut de cette tache) : etat `Completed` et instances de preview, `FinishedChildren` des groupes, sweep du manager ; puis `UIAnimation.Seek`, `UIAnimationGroup.Seek`, `UIAnimationPreview.Attach(element, animation)` (sans manager), refus sur une animation enregistree.
- Docs : section « Preview et seek » (contrat : aucun evenement, aucune completion, hote responsable de l'element de preview).
- Tests `MGUI.Tests/Animation/SeekTests.cs` : `Seek` avant egale N `Advance` sur `Progress` et `CurrentValue` ; arriere depuis mi-course et depuis la fin ; aucun evenement ; storyboard et sequence positionnent leurs enfants ; refus sur une animation enregistree ; zero allocation par `Seek`.

Commit recommande : `animation: add seek for preview instances`

### ⚪ U8. Serialisation d'un storyboard

But : `UIAnimationNodeDto`, `UIAnimationSerializer`.

Etat actuel : voir « Outillage ».

Travail attendu :

- `KeyFrames/UIAnimationSerializer.cs` : DTO arbre versionne, `Serialize(animation, nameOf)`, `Deserialize(json, resolveElement)` ; point d'extension `Format` / `Parse` pour une cible applicative (erreur explicite sinon).
- Docs : « Keyframes » ou nouvelle section « Serialisation », precedent `GraphSerializer`.
- Tests `MGUI.Tests/Animation/AnimationSerializerTests.cs` : round-trip d'un storyboard (propriete, delai, keyframes, sequence imbriquee) avec durees, easings, fill, cancel, ordre ; version, kind et chemin inconnus refuses en listant les chemins connus ; cible applicative sans `Format` / `Parse` refusee explicitement.

Commit recommande : `animation: serialize storyboards to JSON`

### ⚪ U9. Refresh a chaud des transitions et etats des styles

But : `ElementStyleRefresher` re-transfere les transitions et etats des styles.

Etat actuel : voir « Refresh de styles ».

Travail attendu :

- Provenance sur `UITransition` et `UIVisualState`, `ElementStyleScope.OwnTransitionPaths` / `OwnVisualStateNames`, passe de refresh dans `RefreshElement`, `MGElement.RefreshedStyleTransitionPaths` / `RefreshedStyleVisualStateNames`, compteurs de `UIStyleRefreshResult`.
- Docs : `Docs/animation-architecture.md` (« Styles et themes » : la limite « pas de refresh a chaud » disparait), `Docs/styling-theme-architecture.md` (« Refresh de styles a chaud »).
- Tests `MGUI.Tests/Architecture/StyleRefreshTests.cs` (extension) : transition de style ajoutee apres chargement atteinte par `RefreshStyles` ; retiree -> retiree de l'element, run en cours garde sa valeur ; etat nomme remplace re-applique s'il est courant ; declaration de l'element jamais touchee ; deux refreshs sans changement n'ecrivent rien ; style implicite du desktop fusionne pris en compte.

Commit recommande : `styling: refresh style transitions and visual states at runtime`

### ⚪ U10. Migrations : texte revele et brush de surbrillance

But : `MGTextBlock.TextCharactersPerSecond` sur le moteur, `MGHighlightBorderBrush` sur l'horloge du desktop.

Etat actuel : voir « Consommateurs de temps ».

Travail attendu :

- Cible `TextBlock.TextProgress`, `MGTextBlock.SyncTextProgressAnimation`, `ApplyAnimatedTextProgress`, remise a zero sur `Text` ; `UpdateSelf` ne cumule plus.
- `UpdateBaseArgs.AnimationDeltaTime` (`MGUI.Shared`), pose par `MGDesktop.Update` ; `MGHighlightBorderBrush.Update` le consomme.
- Docs : « Cibles », nouvelle section « Migrations » (ce qui suit l'horloge du desktop, ce qui garde sa propre horloge et pourquoi).
- Tests `MGUI.Tests/Animation/TextProgressAnimationTests.cs` : fin du reveal a la frame attendue, changement de vitesse en cours, changement de `Text` qui repart de zero, detachement qui garde puis reprise, pause et `TimeScale` du desktop appliques, zero allocation par tick ; `HighlightBorderBrushClockTests.cs` : progression par le delta mis a l'echelle, brush sans `Target` qui avance.

Commit recommande : `animation: drive the text reveal and the highlight brush with the engine clock`

### ⚪ U11. Sample, scenario, documentation et ADR

But : colonne V3 du sample (`SCN-ANIM-003`), completion de l'ADR-0008.

Travail attendu :

- `MGUI.Samples/Features/AnimationDemo.xaml(.cs)` : easing de Bezier en XAML, toggle avec `CheckedBackgroundBrush` et un etat `OverridesLocalValue`, clip JSON charge en storyboard, preview avec `Seek` pilote par un slider, texte revele qui suit la pause de l'horloge, brush de surbrillance qui suit `TimeScale` ; `Docs/scenario-validation-index.md` : `SCN-ANIM-003` ; `AnimationDemoSampleTests` : section V3 ; ADR-0008 : statut Accepted et « Decisions taken during delivery » ; `Docs/animation-architecture.md` : « Reste a faire » mis a jour.

Commit recommande : `animation: add the V3 sample, scenario and documentation`
