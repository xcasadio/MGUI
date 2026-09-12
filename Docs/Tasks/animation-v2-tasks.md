# Taches systeme d'animation (V2)

## Objectif

Livrer la V2 du systeme d'animation de MGUI selon les decisions du 12 septembre 2026 (ADR-0007, `Docs/decisions/0007-animation-v2-composition-states-keyframes.md`), au-dessus de la V1 (ADR-0006, `Docs/Tasks/animation-tasks.md`, `Docs/animation-architecture.md`) : composition (`UIStoryboard` parallele, `UISequenceAnimation`, `UIDelayAnimation`), keyframes en modele de donnees pur et serialisable (`UIKeyFrame<T>`, `UIKeyFrameTrack<T>`, `UIKeyFrameAnimation<T>`, `UIKeyFrameSerializer`), etats visuels nommes par element (`UIVisualState`, `element.VisualStates`, `Checked` alimente par `IsChecked`) animes par les transitions, fondu des overlays Hover/Pressed (`VisualStateFillBrush.OverlayOpacity`, cible `Background.Overlay`), transitions et etats dans les styles XAML (`<Style.Transitions>`, `<Style.VisualStates>`) et groupe de theme `Animation`, cibles `PreferredWidth` / `PreferredHeight`, interpolation de gradients de meme type, re-implementation de `MGProgressButton.Duration` sur le moteur, API fluente `element.Animate(...)`.

Contraintes non negociables : celles de la V1 (pas de dependency property system, store ADR-0005 pour les pilotes, zero cout pour un element qui n'anime rien, tokens interdits de `RenderingBoundaryArchitectureTests`, renderer neutre, une ADR par decision) ; les enums `PrimaryVisualState` / `SecondaryVisualState` et la pile de precedence restent inchangees ; aucune animation ad hoc autre que `MGProgressButton.Duration` n'est modifiee ; le refresh de styles a chaud n'est pas etendu.

Hors programme (V3, dans le moteur de jeu de l'auteur) : editeur de timeline, preview, scrubbing, seek, courbes de Bezier, inspecteur. Reste hors V2 : refresh a chaud des transitions et etats de style, etat `Checked` de premier rang, keyframes sur un composite, migration de `MGTextBlock.TextCharactersPerSecond`, `MGTimer`, `MGStopWatch` et `MGHighlightBorderBrush`.

## Historique du fichier

- 12 septembre 2026 : creation apres l'audit V2 en lecture seule a HEAD `96a8074` (hooks du moteur, etats visuels et overlays, styles et themes XAML, largeur et hauteur, gradients, consommateurs de temps, precedent de serialisation) et les reponses de l'auteur aux neuf questions. Aucune tache commencee.

## Contexte : faits verifies le 12 septembre 2026 (HEAD `96a8074`, lecture seule)

- Moteur : `UIAnimation` (`MGUI.Core/UI/Animation/UIAnimation.cs`) : `Begin(owner, manager, inheritedBase)` (`:185`), `Advance(delta)` (`:211`), `CancelCore(behavior)` (`:159`), `Complete` (`:277`, applique `FillBehavior` puis `Completed` puis `Manager.NotifyFinished`), hooks abstraits `TargetKey`, `IsStoreBacked`, `BaseValueBoxed`, `OnStarting`, `ApplyProgress`, `OnRestoreBaseValue`, `OnReleaseHold` (`:302-321`), `RegisteredKey` (`:90`), `InheritsBaseValue` (`:63`), `Owner` (`:81`), `Manager` (`:86`). `UIAnimationManager.Start(owner, animation)` (`UIAnimationManager.cs:96`) enregistre par (element, chemin) et applique la regle de conflit ; `CancelOwnedBy`, `CancelOwnedByWindow` (chaine `ParentWindow`), `CancelWhere` rebalaie ; `NotifyFinished` differe le retrait pendant un tick. `UIAnimation<T>.ApplyProgress` (`UIAnimationOfT.cs:73-78`) : un segment `StartValue -> To`, un easing. `UIElementAnimationSlot` (`Animations`, `Transitions`, `StateScaleOverride`) alloue au premier acces.
- Etats visuels : `VisualState` = (`PrimaryVisualState`, `SecondaryVisualState`) recalcule dans `MGElement.Update` ; `ResolvePrimaryVisualState` (`MGElement.cs:2898`) : Disabled > Selected > Focused > Normal ; `VisualStateChanged` (`:2788`). `Checked` : `MGToggleButton.IsChecked` (`MGToggleButton.cs:79-100`) pose `IsSelected` et leve `OnCheckStateChanged` ; `MGCheckBox.IsChecked` est `bool?` (`MGCheckBox.cs:208`), `MGRadioButton.IsChecked` (`MGRadioButton.cs:274`). Source `VisualState` (70) ecrite par `MGGraphControls.cs:2834-2835`, `:3625` et `MGDockTabItem.cs:477`, `:550`, `:560` seulement. `MGVisualStateProjection` (`MGUI.Core/UI/Styling/MGVisualStateProjection.cs`) : action (ancien etat, nouvel etat) sur `VisualStateChanged`, appliquee immediatement a la construction.
- Overlays : `VisualStateFillBrush` derive `HoveredFillOverlay` / `PressedFillOverlay` (`MGSolidFillBrush`, prives, `VisualState.cs:271-272`) de `FocusedColor`, `PressedModifierType` et `PressedModifier` (`:229-252`) ; `GetFillOverlay(SecondaryVisualState)` (`:290`) et `GetBorderOverlay` (`:300`) ; `MGElement.DrawBackground` (`MGElement.cs:4297-4312`) peint l'underlay de l'etat primaire puis l'overlay de l'etat secondaire (avec `SpoofIsHovered/PressedWhileDrawingBackground`). Aucun facteur de melange.
- XAML : `Setter` (`Style.cs:34`, `string Property`, `object Value`) applique par reflexion sur le DTO (`Element.cs:1046`, `:1093`, `:1137`) ; `Style` (`Style.cs:16` : `TargetType`, `Setters`, `Name`, `AffectsComponents`) ; `ProcessStyles` (`Element.cs:924`, appele une fois par `XAMLParser.cs:331` avant le transfert) ; `ElementStyleScope` (`ElementStyleScope.cs`) memorise definition, `StyleNames`, styles inline et proprietes stylees ; `ElementStyleRefresher` ignore ce qui n'est pas pilote (`NotRefreshable`, `:243`). `Element.Transitions` (`List<Transition>`) et `Element.RenderTransform` (S7 V1) sont transferes en fin de `ApplyBaseSettings` (`Element.cs:513-520`).
- Themes : groupes `MGTheme*Settings` (`MGTheme.cs:530-547`, exemple `MGThemeToolTipSettings` `:161-168`), inities dans le constructeur (`:585-592`), copies champ a champ par `MGTheme(MGTheme)` (`:735-738`), definis par `Theme*SettingsDefinition` (`Themes.cs:27-42`, `:322-329`) et appliques par `ThemeDefinitionBuilder.Apply*` (`ThemeDefinitionBuilder.cs:61`, `:67-76`) ; `BasedOn` part d'une copie du theme de base (`:29`). Inventaire `UIThemeValueInvalidation` (`UIThemeValueInvalidation.cs:84-91`) verifie par `ThemeValueInvalidationInventoryTests` (`:165-183` : tout groupe dont le nom finit par `Settings` est balaye, chaque valeur doit etre classee ; `PilotShapeOf` impose l'invalidation d'un pilote aux valeurs de forme pilote : `Thickness`, `int`, brushes, couleurs).
- Largeur et hauteur : `PreferredWidth` (`MGElement.cs:2447`, `int?`, `LayoutChanged` + `NPC`), `PreferredHeight`, `ResolvedPreferredWidth` responsive (`:1301`) ; hors store.
- Gradients : `MGGradientFillBrush(TopLeft, TopRight, BottomRight, BottomLeft)` (`MGGradientFillBrush.cs:21`, `Copy()` `:87`), `MGDiagonalGradientFillBrush(Color1, Color2, Color1Position)` (`Copy()` `:150`) ; les cibles couleur V1 refusent toute brush non `MGSolidFillBrush` (`UIColorAnimationTargets.RequireSolid`).
- Consommateurs de temps : `MGProgressButton.UpdateSelf` (`MGProgressButton.cs:721-729` : `Value += FrameElapsed / Duration * (Maximum - Minimum)` sauf `IsPaused` ou `IsCompleted`), `MGTextBlock.cs:1401`, `MGTimer.cs:332`, `MGStopWatch.cs:237`, `MGHighlightBorderBrush.cs:504`.
- Serialisation : `GraphSerializer` (`MGUI.Core/UI/Graph/Serialization/GraphSerializer.cs`) : `System.Text.Json`, `JsonSerializerOptions` statiques, DTO versionnes, migration explicite ; aucun controle serialise.
- Tests et samples : harnais `AnimationTestScene` (`MGUI.Tests/Animation/AnimationTestScene.cs`), `GraphTestRuntime` ; sample `MGUI.Samples/Features/AnimationDemo.xaml(.cs)` (`SCN-ANIM-001`).

## Decisions de l'auteur (12 septembre 2026, ADR-0007)

1. Composition possedee par un element racine, enfants sur n'importe quel element avec leur propre cle de conflit ; annulation du composite si sa racine quitte l'arbre ou sa fenetre se ferme ; `UIDelayAnimation` comme enfant sans cible.
2. Keyframes en POCO (`UIKeyFrame<T>`, `UIKeyFrameTrack<T>`) sans reference a `MGElement`, consommes par `UIKeyFrameAnimation<T>`, plus un serialiseur JSON versionne minimal (`UIKeyFrameSerializer`).
3. Etats nommes par element (`UIVisualState` avec setters types par chemin de cible), ecrits au niveau `VisualState` (70) pour les pilotes et via les cibles sinon, animes par les transitions ; `Checked` nomme, alimente par `IsChecked` ; enums inchanges.
4. Fondu des overlays par `VisualStateFillBrush.OverlayOpacity` (cible `Background.Overlay`).
5. `<Style.Transitions>` et `<Style.VisualStates>` fusionnes a l'application du style ; groupe de theme `Animation` (durees et easings par defaut hover / press / focus, `Enabled`) pour les controles qui optent ; pas de refresh a chaud.
6. `PreferredWidth` / `PreferredHeight` cibles simples, documentees couteuses.
7. `MGProgressButton.Duration` re-implemente sur le moteur, les autres animations ad hoc inchangees.
8. Interpolation entre deux gradients du meme type, refus explicite sinon.
9. API fluente `element.Animate(path, from, to, seconds)` en sucre, derniere tranche.

Decisions de conception derivees (session principale, contestables avant T1) :

- Composite : `UIAnimationGroup` abstrait (`Children`, `Add`, `TargetKey` synthetique `Group#<n>` qui n'entre jamais en conflit avec une propriete, `IsStoreBacked` faux, `BaseValueBoxed` nul), `UIStoryboard` (tous les enfants demarrent a `Begin`, `Duration` = max des durees enfants delai compris, complete quand le dernier enfant se termine), `UISequenceAnimation` (`Append`, `AppendDelay` ; un enfant a la fois, le suivant demarre a la completion du precedent), `UIDelayAnimation` (`Duration` seule, `TargetKey` synthetique, aucune ecriture). Un enfant est demarre par `child.Owner ?? root` via `Animations.Start` (donc regle de conflit, diagnostics et annulation par element conservees) ; le composite s'abonne a `Completed` / `Cancelled` de ses enfants ; `Cancel` du composite annule les enfants actifs selon leur propre `CancelBehavior` ; `Repeat` / `AutoReverse` du composite relancent les enfants (pas de rembobinage inverse des enfants : `AutoReverse` d'un composite est refuse par `ArgumentException`, documente). `Progress` du composite = temps ecoule / duree.
- Keyframes : `UIKeyFrame<T>` record (`Offset` float dans [0,1], `Value`, `Easing` nom ou null), `UIKeyFrameTrack<T>` (liste triee par `Offset`, `Add`, validation : offsets croissants, dernier a 1 ; sans frame a 0, la valeur de depart est lue au demarrage), `UIKeyFrameAnimation<T> : UIAnimation<T>` (`Track`, `Property` ou `Target` comme `UIPropertyAnimation<T>`, `ApplyProgress` : segment par recherche binaire, easing de la frame de fin appliquee au segment, `Easing` global ignore), `UIKeyFrameSerializer` (`Serialize(track)` / `Deserialize<T>(json)`, `Version` 1, `ValueType` nom simple du type, valeur JSON par convertisseur : float, int, Vector2, Color `#RRGGBBAA`, Thickness `l,t,r,b`, erreur explicite sinon).
- Etats nommes : `UIVisualState` (`Name`, `Setters` : liste de `UIVisualStateSetter(Path, Value boxee)`), `UIVisualStateCollection` (`element.VisualStates`, portee par le slot : `Add`, `Remove`, indexeur par nom, `Current`, `IsEnabled`) ; resolution du nom courant dans `MGElement.Update` apres `VisualState` : `Disabled` si `!IsEnabled`, `Checked` si `IUICheckable.IsChecked == true`, `Selected`, `Pressed`, `Hover`, `Focused`, `Normal` ; application : pour chaque setter du nouvel etat, ecriture par la cible (`IUIAnimationTarget<T>.SetValue` avec un nom de source `visualstate:<Name>` ; pour un pilote, la cible ecrit avec la source `Animation` aujourd'hui : ajout d'une surcharge interne `SetValue(element, value, source)` sur les cibles store pour ecrire `UIValueResolutionSource.VisualState`) ; sortie : `RestoreBaseValue` (retrait de la contribution `VisualState` ou retour a la valeur memorisee) puis application du nouvel etat, dans cet ordre, si bien qu'une transition sur le chemin voit une seule ecriture par changement ; `IUICheckable` (`IsChecked` bool?, `CheckStateChanged`) implemente par `MGToggleButton`, `MGCheckBox`, `MGRadioButton`. Un element sans `VisualStates` ne paie rien (la resolution n'est evaluee que si la collection existe et n'est pas vide).
- Overlay : `VisualStateFillBrush.OverlayOpacity` (float, defaut 1, `NPC`), consomme par `DrawBackground` (`DA.SetOpacity(DA.Opacity * OverlayOpacity)` autour du dessin de l'overlay) ; cible `Background.Overlay` (simple, base gardee par le moteur, observable via le conteneur) ; une transition sur `Background.Overlay` ne fait rien seule : le fondu utile est celui de `RenderScale` (par `VisualStateChanged`) transpose : la cible `Background.Overlay` est aussi declenchee par `VisualStateChanged` et sa valeur sous-jacente est 0 quand l'etat secondaire est `None`, 1 sinon (l'overlay peint est celui de l'etat courant, l'opacite fait le fondu a l'entree ; a la sortie l'overlay disparait avec l'etat : fondu a l'entree seulement, limite documentee).
- Styles : `Style.Transitions` (`List<Transition>`) et `Style.VisualStates` (`List<VisualStateDefinition>` DTO : `Name`, `Setters`) ; `ProcessStyles` les accumule (styles implicites puis nommes, dernier gagnant par chemin ou nom) sur l'`Element` DTO qui les transfere avec les siens ; `ElementStyleScope` inchange ; le refresh a chaud les ignore (documente). Theme : `MGThemeAnimationSettings` (`Enabled` true, `HoverDuration` 100 ms, `PressDuration` 60 ms, `FocusDuration` 120 ms, `HoverEasing` `CubicOut`, `PressEasing` `QuadOut`, `FocusEasing` `QuadOut`) sur `MGTheme.Animation`, DTO `ThemeAnimationSettingsDefinition`, builder, copie, inventaire `RenderOnly` ; consommation : `MGButton` et `MGToggleButton` (chrome d'etat via `RenderScale` et `Background.Overlay`) ajoutent, quand `Enabled`, les transitions correspondantes dans `ApplyControlTemplate` / `OnThemeChanged` (remplacees par chemin, donc idempotent) ; les autres controles n'optent pas en V2.
- Cibles : `PreferredWidth`, `PreferredHeight` (`int?`, observables par `NPC`, invalidation par le setter) ; gradients : `UIColorAnimationTargets` gagne des cibles `Background.Gradient` (`MGGradientFillBrush`, 4 couleurs interpolees ensemble : type de valeur `UIGradientColors` record struct de 4 `Color`) et `Background.DiagonalGradient` (`UIDiagonalGradientColors` : 2 `Color` + position float), enregistrees avec leurs interpolateurs ; `Background` (unie) inchangee et refus explicite des autres brushes.
- `MGProgressButton.Duration` : quand `Duration` est pose, une `UIPropertyAnimation<float>` sur une cible `ProgressButton.Value` (registre, `IsStoreBacked` faux) remplace l'increment de `UpdateSelf` ; `IsPaused` pause l'animation ; `IsCompleted`, `Minimum`, `Maximum` conserves ; comportement observable identique (tests existants du bouton).
- Fluent : `UIAnimateExtensions.Animate<T>(this MGElement, string path, T from, T to, double seconds)` -> `UIAnimationBuilder<T>` (`Ease`, `Delay`, `Repeat`, `Forever`, `AutoReverse`, `Fill`, `Cancel`, `Named`, `Play()` -> l'animation, `Then(...)` -> `UISequenceAnimation`) ; surcharge sans `from`.

## Consignes de travail pour l'agent IA

- Executer les tranches dans l'ordre ; une tranche = un commit ; mettre a jour le statut dans ce fichier dans le meme commit.
- Blocage : marquer ⛔, decrire, s'arreter.
- Pas de refactor hors perimetre ; aucun renommage d'API publique V1 ; les tests V1 (`MGUI.Tests/Animation/*`) restent verts sans modification, sauf ajout.
- Tests sur le comportement observable avec le harnais headless (`AnimationTestScene`, frames explicites) ; zero allocation par tick pour un composite et une animation keyframe en cours ; aucun slot pour un element sans animation.
- Chaque tranche modifiant `MGElement`, `VisualState.cs`, `MGDesktop`, le store, `Element.cs`, `Style.cs`, `MGTheme` ou un controle est a risque : verification independante en lecture seule obligatoire (modele Sonnet), constats consignes dans le statut de la tranche.
- Toute decision prise en cours de tranche est ajoutee a ADR-0007 au moment ou elle est prise.
- Ne jamais lancer `MGUI.Samples` depuis un agent ; le construire a chaque tranche.
- Docs en francais sans accents.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

- `dotnet build MGUI.Core/MGUI.Core.csproj`
- `dotnet test MGUI.Tests/MGUI.Tests.csproj --filter "FullyQualifiedName~Animation|FullyQualifiedName~Architecture|FullyQualifiedName~Theme|FullyQualifiedName~Style" --no-restore`
- `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-restore`
- A partir de T8, scenario `SCN-ANIM-002` (sample `MGUI.Samples/Features/AnimationDemo.xaml`, section V2).

## Tranches

### ⚪ T1. Composition : storyboard, sequence, delai

But : `UIAnimationGroup`, `UIStoryboard`, `UISequenceAnimation`, `UIDelayAnimation` selon les decisions derivees.

Etat actuel : voir "Moteur" ci-dessus ; aucun composite n'existe ; `UIAnimation.TargetKey` est requis non vide par `UIAnimationManager.Start`.

Travail attendu :

- `MGUI.Core/UI/Animation/Composition/` : les quatre types ; `UIAnimationManager` : un enfant demarre par le composite passe par `Start(childOwner, child)` ; `CancelWhere` couvre les composites comme les autres ; `UIAnimationCollection.Active` / `Held` inchanges (les enfants apparaissent sur leur propre element, le composite sur sa racine).
- `Docs/animation-architecture.md` : section « Composition ».
- Tests `MGUI.Tests/Animation/CompositionTests.cs` : storyboard de deux animations sur deux elements (valeurs a mi-parcours, completion quand la plus longue finit), sequence avec delai (ordre, temps de demarrage de chaque enfant), annulation du composite (enfants annules selon leur `CancelBehavior`), detachement de la racine (composite et enfants annules), fermeture de fenetre, conflit d'un enfant avec une animation explicite (l'enfant est remplace, le composite continue), `Repeat` d'une sequence, `AutoReverse` refuse, zero allocation par tick, `Progress` du composite.

Criteres d'acceptation : tests V1 verts ; un composite vide complete immediatement ; `ToString` lisible dans le debug view (kind `animation`, chemin synthetique).

Commit recommande : `animation: add storyboards, sequences and delays`

### ⚪ T2. Keyframes et serialisation

But : `UIKeyFrame<T>`, `UIKeyFrameTrack<T>`, `UIKeyFrameAnimation<T>`, `UIKeyFrameSerializer`.

Etat actuel : `UIAnimation<T>.ApplyProgress` a un segment ; precedent `GraphSerializer`.

Travail attendu :

- `MGUI.Core/UI/Animation/KeyFrames/` : les quatre types selon les decisions derivees ; validation de la piste au demarrage (erreurs explicites : vide, offsets non croissants, dernier offset different de 1) ; `UIKeyFrameAnimation<T>` accepte `Property` ou `Target`, `Duration`, `Delay`, `Repeat`, `AutoReverse` comme `UIAnimation`.
- `Docs/animation-architecture.md` : section « Keyframes » avec le format JSON.
- Tests `MGUI.Tests/Animation/KeyFrameTests.cs` : opacite 0 -> 1 -> 0 (valeurs a 25, 50, 75 %), pop d'echelle 0.8 -> 1.1 -> 1.0 avec easing par frame, piste sans frame a 0 partant de la valeur courante, validation, round-trip JSON pour float, Vector2, Color, Thickness, version inconnue refusee, zero allocation par tick.

Commit recommande : `animation: add keyframe animations and their JSON model`

### ⚪ T3. Fondu des overlays et cibles supplementaires

But : `VisualStateFillBrush.OverlayOpacity`, cible `Background.Overlay` declenchee par `VisualStateChanged`, cibles `PreferredWidth` / `PreferredHeight`, cibles gradients.

Etat actuel : voir "Overlays", "Largeur et hauteur", "Gradients".

Travail attendu :

- `VisualState.cs` : `OverlayOpacity` (`NPC`) ; `MGElement.DrawBackground` : opacite appliquee a l'overlay seulement ; `MGElement.Draw` ne change pas.
- `Targets/` : `Background.Overlay` (simple, observable par `VisualStateChanged` + conteneur, valeur sous-jacente 0 / 1 selon l'etat secondaire), `PreferredWidth`, `PreferredHeight` (simples, observables par `NPC`), `Background.Gradient`, `Background.DiagonalGradient` avec `UIGradientColors` / `UIDiagonalGradientColors` et leurs interpolateurs ; `UIInterpolators` les enregistre.
- Docs : cibles ajoutees a la table, limite « fondu a l'entree seulement ».
- Tests `MGUI.Tests/Animation/OverlayAndExtraTargetsTests.cs` : overlay a 0.5 dessine avec une opacite reduite (transaction de test : couleur alpha), transition `Background.Overlay` sur survol (0 -> 1 en 100 ms), largeur animee (layout invalide a chaque tick, valeur `PreferredWidth` a mi-parcours), gradient 4 coins interpole, diagonal interpole, gradient vers unie refuse.

Commit recommande : `animation: fade the state overlays and add width, height and gradient targets`

### ⚪ T4. Etats visuels nommes

But : `UIVisualState`, `UIVisualStateCollection` (`element.VisualStates`), resolution du nom courant, `IUICheckable`, application et retrait par les cibles avec la source `VisualState` pour les pilotes.

Etat actuel : voir "Etats visuels" ; source `VisualState` (70) ecrite par trois controles ; `MGVisualStateProjection` existant.

Travail attendu :

- `MGUI.Core/UI/Animation/States/` : `UIVisualState`, `UIVisualStateSetter`, `UIVisualStateCollection`, `IUICheckable` ; surcharge interne `SetValue(element, value, UIValueResolutionSource)` sur les cibles store (`IUIStoreBackedAnimationTarget<T>`), utilisee par l'application d'etat ; `MGElement.Update` : apres le calcul de `VisualState`, si le slot a des etats, resoudre le nom (ordre de priorite fixe) et appliquer au changement ; `MGToggleButton`, `MGCheckBox`, `MGRadioButton` implementent `IUICheckable` ; `Checked` prime sur `Selected`.
- Interaction avec les transitions : une transition sur un chemin pose par un etat interpole le changement (test) ; l'echelle d'etat `RenderScale` reste le mecanisme des etats Hover / Pressed de V1 et coexiste.
- Docs : section « Etats visuels nommes » (priorite, sources, limites : pas de refresh a chaud, un setter sur un chemin non enregistre est refuse a l'ajout).
- Tests `MGUI.Tests/Animation/VisualStatesTests.cs` : etats `Normal` / `Hover` / `Pressed` avec `RenderTransform.Scale`, survol puis appui puis sortie (valeurs et retour), `Checked` sur un toggle (source `VisualState` pour un pilote `Background`, retrait au decochage), priorite `Disabled` > `Checked`, transition sur le chemin d'un setter (interpolation), element sans etats non affecte (aucun slot), setter sur chemin inconnu refuse.

Commit recommande : `animation: add named visual states`

### ⚪ T5. Styles et theme

But : `<Style.Transitions>`, `<Style.VisualStates>`, groupe de theme `Animation`, opt-in de `MGButton` et `MGToggleButton`.

Etat actuel : voir "XAML" et "Themes".

Travail attendu :

- `Style.cs` : `Transitions` (`List<Transition>`), `VisualStates` (`List<VisualStateDefinition>`) ; DTO `VisualStateDefinition` (`Name`, `Setters` : `Setter` reutilise, `Value` converti par le type de la cible via `UIAnimationTargets.GetValueType`) et `Element.VisualStates` ; `ProcessStyles` fusionne dans l'`Element` DTO (implicites puis nommes, dernier gagnant par chemin ou nom) ; transfert en fin de `ApplyBaseSettings` (etats avant transitions).
- `MGThemeAnimationSettings`, `MGTheme.Animation`, `ThemeAnimationSettingsDefinition`, `ThemeDefinition.Animation`, builder, copie, `UIThemeValueInvalidation` (`Animation.*` = `RenderOnly`), `BuiltInThemes.xaml` inchange (defauts C#).
- `MGButton` / `MGToggleButton` : quand `GetTheme().Animation.Enabled`, transitions `RenderScale` (hover) et `Background.Overlay` (press) posees avec les durees et easings du theme dans `ApplyControlTemplate` (idempotent par chemin) ; retirees si `Enabled` faux au refresh.
- Docs : sections « Styles et themes » ; `Docs/styling-theme-architecture.md` : mention du groupe et de la limite de refresh.
- Tests `MGUI.Tests/Animation/StyleAndThemeAnimationTests.cs` : style implicite avec transitions et etats appliques a un bouton XAML, style nomme qui remplace la transition d'un chemin, `BasedOn` conservant le groupe, theme `Enabled=false` sans transitions sur le bouton, changement de theme qui met a jour les durees, inventaire d'invalidation vert.

Commit recommande : `animation: declare transitions and visual states in styles, add the theme animation group`

### ⚪ T6. ProgressButton sur le moteur

But : re-implementer `MGProgressButton.Duration` avec une animation.

Etat actuel : `MGProgressButton.UpdateSelf` (`:721-729`).

Travail attendu : cible `ProgressButton.Value` (enregistree par `UIBuiltInAnimationTargets`, applicable a un `MGProgressButton` seulement), `UpdateSelf` ne cumule plus ; poser `Duration` demarre (ou remplace) l'animation `Minimum -> Maximum` lineaire, `IsPaused` la met en pause, `IsCompleted` ou `Duration = null` l'annule en gardant la valeur ; `Value` posee par l'application pendant l'animation recible depuis cette valeur. Tests existants du bouton verts ; `MGUI.Tests/Animation/ProgressButtonAnimationTests.cs` (progression, pause, changement de duree, valeur posee).

Commit recommande : `animation: drive MGProgressButton.Duration with the engine`

### ⚪ T7. API fluente

But : `UIAnimateExtensions.Animate` et `UIAnimationBuilder<T>`.

Travail attendu : `MGUI.Core/UI/Animation/UIAnimateExtensions.cs` ; sucre pur (aucun nouveau concept) ; tests `MGUI.Tests/Animation/FluentApiTests.cs` (fondu, chainage `Then`, options).

Commit recommande : `animation: add the fluent Animate API`

### ⚪ T8. Sample, scenario, diagnostics et documentation

But : section V2 du sample, `SCN-ANIM-002`, debug view etendu (etat nomme courant), doc.

Travail attendu : `AnimationDemo.xaml(.cs)` : storyboard d'ouverture, sequence, keyframes (pop), etats nommes avec `Checked` sur un toggle, style avec transitions, bouton de theme `Enabled` ; `UIElementDebugView` : `VisualStateName` ; `Docs/scenario-validation-index.md` : `SCN-ANIM-002` ; `Docs/animation-architecture.md` complete (V2 en etat courant, « Reste a faire » = V3) ; ADR-0007 completee des decisions prises en route.

Commit recommande : `animation: add the V2 sample, diagnostics and documentation`
