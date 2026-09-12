# Architecture du systeme d'animation

## Objectif

Decrire l'etat actuel du moteur d'animation de MGUI (V1, livre le 12 septembre 2026 par le programme `Docs/Tasks/animation-tasks.md`, decisions dans `Docs/decisions/0006-animation-system.md`) : le transform de rendu par element, l'horloge et le manager par desktop, les animations et leurs cibles, les transitions, la declaration XAML, le cout et les limites.

## Portee

Couvre `MGUI.Core/UI/Animation/*` (namespace `MGUI.Core.UI.Animation`), la partie transform et animation de `MGUI.Core/UI/MGElement.cs` et `MGUI.Core/UI/MGDesktop.cs`, les DTO `MGUI.Core/UI/XAML/Animation.cs`, le sample `MGUI.Samples/Features/AnimationDemo.xaml` (scenario `SCN-ANIM-001`).

Hors portee : les animations ad hoc preexistantes (`MGHighlightBorderBrush`, `MGProgressButton.Duration`, `MGTextBlock.TextCharactersPerSecond`, `MGTimer`, `MGStopWatch`), qui gardent leur propre temps ; les storyboards, keyframes et etats visuels nommes (V2, section « Reste a faire »).

## Principes directeurs

- Le moteur ne depend ni de `GameTime` ni du renderer : il consomme `UpdateBaseArgs.FrameElapsed` et ecrit des proprietes d'elements.
- Un transform de rendu ne participe jamais au layout (`Docs/layout-architecture.md`, principe 1) ; il est applique au draw et inverse par le hit-test.
- Les proprietes pilotes du store (ADR-0005) sont animees par leur setter tague avec la source `Animation` (100) et restaurees par retrait de cette contribution ; les autres proprietes sont ecrites directement et le moteur garde leur valeur de base.
- Un element qui n'anime rien ne paie rien : deux references nulles (`_RenderTransform`, `_AnimationSlot`), aucun abonnement, aucun travail par frame.
- Une animation en cours n'alloue rien par frame ; les seules allocations acceptees sont celles des cibles couleur (une brush unie boxee ou une bordure uniforme par tick).
- Semantique NoesisGUI / WPF pour le transform : origine relative avec (0, 0) par defaut, rotation en degres horaires.

## Vue d'ensemble

```
MGDesktop.Update
   |
   +-- Animations.Update(FrameElapsed)     phase "Animations", avant le layout et les fenetres
   |       |
   |       +-- UIAnimationClock.Advance    TimeScale, IsPaused
   |       +-- UIAnimation.Advance x N     ecrit chaque valeur via sa cible
   |
   +-- fenetres : layout, input, VisualState (declenche les transitions RenderScale)
MGDesktop.Draw
   +-- MGElement.Draw : matrice locale (echelle d'etat + RenderTransform) composee avant la transform courante
```

## Transform de rendu

`UIRenderTransform` (`MGUI.Core/UI/Animation/UIRenderTransform.cs`) : `Translation` (pixels de layout non scales), `Scale` (defaut `Vector2.One`), `Rotation` (degres, horaire a l'ecran), `Origin` (point relatif dans [0,1]², defaut (0, 0) = coin haut-gauche ; (0.5, 0.5) pour le centre), `IsIdentity`, `Reset`, `ToMatrix(bounds)`, `CreateMatrix`, `CreateCenteredScale`. Chaque setter leve `PropertyChanged`.

`MGElement.RenderTransform` alloue l'instance a la premiere lecture. `RenderScale` (echelle par etat Pressed/Hovered) reste un sucre compose dans la meme matrice, toujours autour du centre de l'element quelle que soit `Origin`.

Composition au draw (`MGElement.Draw`) : matrice locale `L = E * T(-o) * S * R * T(o) * T(translation)` en espace ecran non scale (`E` = echelle d'etat autour du centre, `o` = origine relative resolue sur les bounds), poussee par `SetTransformTemporary(L * CurrentSettings.Transform)` uniquement si `L` n'est pas l'identite : un element sans transform ne coupe jamais le batch ; un element transforme coute une coupure a l'entree et une a la sortie. Les `TargetBounds` du clip sont la boite englobante des quatre coins transformes (`RectangleUtils.CreateTransformedBoundsF`), valable sous rotation.

Input : `ComputeTopmostHoveredElement` transforme la position de la souris par l'inverse de la matrice de l'element pour lui-meme et son sous-arbre ; `IMouseViewport.IsInside` applique la chaine inverse (ancetres puis element, `ToLocalUnscaledPoint`) au point non scale, l'occlusion de fenetres restant testee sur le point d'origine. Le tout est gate par `MGDesktop.ActiveRenderTransformCount` (nombre d'elements dont un transform, une `RenderScale` ou un override d'echelle d'etat est actif) : sans transform actif, aucun cout par evenement souris. L'early-out du parcours de survol sur les bounds du parent n'est pris que si aucun transform n'est actif, un enfant transforme pouvant en sortir. Tout changement de transform pose `MGWindow.InvalidatePressedAndHoveredElements` pour recalculer le survol sous une souris immobile.

Une `MGWindow` ignore son `RenderTransform` (elle garde `MGWindow.Scale`).

## Horloge et manager

`UIAnimationClock` (`MGDesktop.Animations.Clock`) : `Time`, `DeltaTime`, `TimeScale` (>= 0), `IsPaused` ; `Advance(frameElapsed)` borne les deltas negatifs a zero et ne borne pas les grands deltas (une longue pause termine les animations a la frame suivante). Le delai des tooltips, `MGTimer` et `MGStopWatch` ne suivent pas cette horloge.

`UIAnimationManager` (`MGDesktop.Animations`) : liste des animations actives (`ActiveCount`, `ActiveAnimations`), `Update` par index avec balayage differe des animations terminees pendant un tick, `PauseAll` / `ResumeAll` / `CancelAll`, et la regle de conflit : une seule animation active par (element, chemin de propriete) ; l'animation qui demarre annule la precedente en `KeepCurrent` et herite de sa valeur de base, si bien qu'un survol quitte avant la fin repart de la valeur animee courante et restaure la vraie base. Relancer l'animation deja active sur le chemin ne leve pas `Cancelled` ; relancer la meme instance sur un autre element quitte proprement son ancien element.

Appartenance (`UIAnimationCollection`, `element.Animations`) : `Start`, `Count`, `IsAnimating(path)`, `Active`, `Held`, `CancelAll` (selon chaque `CancelBehavior`), `Clear` (restauration forcee et liberation des contributions retenues). Un element qui quitte l'arbre (`OnParentChanged` vers null) fait `Clear` ; la fermeture d'une fenetre (`MGDesktop.NotifyWindowClosed`, donc `TryCloseWindow` et le retrait d'une modale) annule les animations des elements affiches par cette fenetre et par ses fenetres imbriquees, tooltips et popups (chaine `ParentWindow`) ; une fenetre re-affichee repart sans animation. `RemoveNestedWindow` appele directement (liste deroulante, popups) et un `Desktop.Windows.Remove` applicatif ne notifient pas.

## Animations

`UIAnimation` : `Duration`, `Delay`, `RepeatCount` / `RepeatForever`, `AutoReverse` (0 -> 1 -> 0, l'animation finit sur sa valeur de depart), `FillBehavior` (`HoldEnd` par defaut, `RestoreBaseValue`), `CancelBehavior` (`RestoreBaseValue` par defaut, `KeepCurrent`), `Name`, `State` (`Stopped`, `Delayed`, `Running`, `Paused`, `Completed`, `Cancelled`), `Progress`, `Iteration`, `IsReversing`, `Elapsed`, `Owner`, `IsHeld` ; `Play`, `Pause`, `Resume`, `Cancel`, `Restart` ; evenements `Started`, `Updated`, `Repeated`, `Reversed`, `Completed`, `Cancelled` (sur `EventArgs.Empty`). `Play` sans proprietaire leve une erreur : la premiere execution passe par `element.Animations.Start`.

`UIAnimation<T>` : `From` optionnel (`HasFrom` ; sinon la valeur lue au demarrage, donc la valeur animee courante quand l'animation en remplace une autre), `To`, `Easing` (defaut lineaire), `Interpolator` (defaut : registre `UIInterpolators`), `StartValue`, `BaseValue`, `CurrentValue`.

`UIPropertyAnimation<T>` : `Property` (chemin resolu dans `UIAnimationTargets` au demarrage, avec la liste des chemins connus en cas d'erreur) ou `Target` explicite.

Interpolation (`Interpolation/`) : `IUIInterpolator<T>`, registre `UIInterpolators` (float, double, int, `int?` a bascule a mi-parcours, Vector2/3/4, Color, Rectangle, `MonoGame.Extended.Thickness` arrondi away-from-zero ; `Register<T>` pour un type applicatif). Seul `Color` borne le progres dans [0, 1] : les autres laissent passer le depassement des easings Back et Elastic.

Easing (`Easing/`) : `IUIEasingFunction`, `UIEasing` (dix-neuf fonctions : Linear, Quad/Cubic/Sine/Back/Bounce/Elastic In/Out/InOut ; `TryGet(nom)` insensible a la casse pour le XAML ; `Register`).

## Composition

`UIAnimationGroup` (`MGUI.Core/UI/Animation/Composition/`, ADR-0007 decision 1) est une `UIAnimation` possedee par un element racine (`root.Animations.Start(group)`) qui demarre ses enfants par le manager, chacun sur son propre element (`child.Owner` s'il en a deja un, la racine sinon) : chaque enfant garde sa cle de conflit, apparait dans les diagnostics de son element et suit l'annulation par element. Le groupe n'ecrit aucune propriete : son chemin est synthetique (`UIStoryboard#n`), il n'entre jamais en conflit. L'annuler annule ses enfants actifs selon leur propre `CancelBehavior` ; `RepeatCount` / `RepeatForever` relancent les enfants ; `AutoReverse` est refuse (les enfants ne sont pas rembobines) ; un enfant `RepeatForever` est refuse (repeter le groupe).

- `UIStoryboard` : parallele, tous les enfants demarrent avec lui, sa duree est celle du plus long (delai et repetitions compris) ; initialiseur de collection.
- `UISequenceAnimation` : `Append`, `AppendDelay` ; chaque enfant demarre a l'offset ou le precedent se termine sur la ligne de temps de la sequence (`GetStartOffset`), deterministe meme si un enfant est remplace tot par une animation en conflit.
- `UIDelayAnimation` : une pause sans cible.

Regle de frame : une animation demarree pendant un tick du manager (enfant d'un groupe, run de transition) avance a partir de la frame suivante, si bien qu'un enfant planifie a un offset reste aligne sur la ligne de temps du groupe (la sequence lit la position exacte en ticks, `UIAnimation.IterationElapsed`). Un enfant demarre au `Begin` du groupe (storyboard) est enregistre avant le groupe et avance avant lui : a une frontiere d'iteration il complete d'abord, puis le `Repeated` du groupe le relance ; chaque iteration se termine donc par le `Completed` de l'enfant. Un groupe garde `FillBehavior = HoldEnd` (refus sinon) ; `Animations.Clear()` sur la racine annule les enfants places sur d'autres elements selon leur propre `CancelBehavior`, sans restauration forcee.

## Keyframes

`UIKeyFrame<T>(Offset, Value, Easing)` et `UIKeyFrameTrack<T>` (`MGUI.Core/UI/Animation/KeyFrames/`, ADR-0007 decision 2) forment un modele de donnees pur, sans reference a un element : cles triees par offset dans [0,1], derniere cle a 1 (`Validate`), l'easing d'une cle s'applique au segment qui se termine sur elle, une piste sans cle a 0 part de la valeur courante au demarrage. `UIKeyFrameAnimation<T>` (derive de `UIPropertyAnimation<T>`) : `Track`, segment trouve par recherche binaire (`FindSegment`), `From` / `To` pris de la piste, `Easing` global ignore ; delai, repetition, aller-retour, comportements de fin et d'annulation, appartenance et conflits sont ceux du moteur.

Format JSON (`UIKeyFrameSerializer`, `Serialize` / `Deserialize<T>` / `ReadValueType`, version 1) pour l'editeur du moteur de jeu :

```json
{ "version": 1, "valueType": "Single", "frames": [ { "offset": 0, "value": "0" }, { "offset": 1, "value": "1", "easing": "CubicOut" } ] }
```

Valeurs en chaines invariantes : `Single` / `Double` / `Int32` en nombre, `Vector2` / `Vector3` / `Vector4` en `x,y[,z[,w]]`, `Color` en `#RRGGBBAA`, `Thickness` en `l,t,r,b` ; type ou version inconnus refuses explicitement, le type du JSON doit correspondre au `T` demande.

## Cibles

`IUIAnimationTarget<T>` (`Path`, `IsStoreBacked`, `GetValue`, `SetValue(element, value, nom)`, `RestoreBaseValue(element, base)`) et le registre ferme `UIAnimationTargets` (`Register`, `TryGet`, `Resolve`, `GetValueType`, `Paths`), chemins insensibles a la casse. `UIDelegateAnimationTarget<T>` pour une propriete applicative.

Cibles framework (`Targets/UIBuiltInAnimationTargets.cs`, `Targets/UIColorAnimationTargets.cs`) :

| Chemin | Type | Famille | Restauration |
| --- | --- | --- | --- |
| `Opacity` | float | simple | valeur de base gardee par le moteur |
| `RenderTransform.Translation`, `.Scale`, `.Origin` | Vector2 | simple | idem |
| `RenderTransform.Rotation` | float (degres) | simple | idem |
| `RenderScale` | float | override de l'echelle d'etat | efface l'override (retour a l'echelle de l'etat courant) |
| `Margin`, `Padding` | Thickness | pilote (layout, couteux) | retrait de la contribution `Animation` |
| `MinHeight` | int? | pilote (layout) | idem |
| `Background`, `Background.Selected`, `.Disabled`, `.Focused` | Color | pilote (slot du fond, brush unie seulement) | retrait, puis reecriture de la base sous la source du conteneur si le slot n'avait aucune autre contribution |
| `Foreground` (`MGTextBlock`) | Color | pilote | idem |
| `TextForeground` | Color | pilote (`DefaultTextForeground.Normal`) | idem |
| `BorderBrush` | Color | pilote (bordure `GetBorder()`, uniforme et unie) | idem |
| `Background.Overlay` | float | simple (`VisualStateFillBrush.OverlayOpacity`, valeur sous-jacente 1 si survole ou presse, 0 sinon) | valeur de base gardee par le moteur |
| `PreferredWidth`, `PreferredHeight` | int? | simple (layout, couteux) | idem |
| `Background.Gradient` | `UIGradientColors` (4 coins) | pilote (slot Normal, `MGGradientFillBrush` seulement) | retrait, puis base sous la source du conteneur |
| `Background.DiagonalGradient` | `UIDiagonalGradientColors` (2 couleurs + coin) | pilote (slot Normal, `MGDiagonalGradientFillBrush` seulement) | idem |

Base et valeur animee : pour un pilote, la valeur effective est le gagnant du store, `Animation` (100) etant la plus forte ; a la fin ou a l'annulation avec restauration, la contribution est retiree et la meilleure source suivante reprend (`UIToolingService.TryGetResolvedValueSource` rapporte `Animation` pendant l'animation). `HoldEnd` sur un pilote garde la contribution (`IsHeld`) jusqu'a la prochaine animation du meme chemin ou `Animations.Clear()`, et masque une ecriture locale posee entre-temps ; sur une propriete simple, la valeur finale reste simplement la valeur CLR et une ecriture locale ulterieure l'emporte (asymetrie assumee).

## Transitions

`UITransition<T>` (`element.Transitions.Add(...)`, `UITransitionCollection`) interpole automatiquement chaque changement de sa propriete : `Property`, `Duration`, `Delay`, `Easing`, `Interpolator`. La cible doit etre observable (`IUIObservableAnimationTarget<T>` : `Subscribe`, `GetUnderlyingValue`), ce que toutes les cibles framework sont ; `RenderScale` reagit a `VisualStateChanged` (survol entree et sortie) et aux changements de `RenderScale`.

Regles : au changement, le run (`UIPropertyAnimation<T>` enregistre au manager) part de la valeur animee courante ou de la derniere valeur memorisee (`SettledValue`) vers la valeur sous-jacente ; une ecriture pendant le run recible depuis la valeur courante ; une animation explicite sur le meme chemin remplace le run et la transition se tait en suivant les valeurs ; retirer la transition garde la valeur courante ; `FillBehavior` = `RestoreBaseValue` pour une cible store (retour a la valeur locale visee) et `HoldEnd` pour une propriete simple. Le manager tickant avant le calcul des etats visuels dans la meme frame, le run de survol entrant avance d'un dernier pas avant que le run sortant reparte exactement de la.

## Etats visuels nommes

`UIVisualState` (`element.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { "RenderTransform.Scale", new Vector2(1.05f) } })`, `UIVisualStateCollection`) : un nom et des setters types par chemin de cible (`UIAnimationTargets`). Un setter est lie a sa cible a la creation (type de valeur verifie, chemin inconnu refuse) ; aucune reflexion par frame. Chaque frame, apres le calcul de `VisualState` dans `MGElement.Update`, la collection resout le nom courant dans l'ordre `Disabled`, `Checked` (element `IUICheckable` : `MGToggleButton`, `MGCheckBox`, `MGRadioButton`), `Selected`, `Pressed`, `Hover` (survole ou presse), `Focused`, `Normal` : le premier etat dont la condition tient ET que l'element definit gagne (un element avec `Normal` et `Hover` montre `Hover` pendant l'appui). Au changement, les chemins de l'etat quitte que le suivant ne pose pas sont restaures, puis les setters du suivant sont ecrits : une transition sur un chemin de setter voit une ecriture par chemin et interpole le changement. `element.CurrentVisualStateName` expose le nom courant ; `IsEnabled = false` gele l'etat applique ; `Remove` / `Clear` restaurent l'etat courant.

Sources : un setter sur un pilote (`Background`, `Foreground`, `BorderBrush`, `Margin`, `Padding`, `MinHeight`, gradients) ecrit la contribution `VisualState` (70) du store avec le nom `visualstate:<Nom>` et la retire en quittant (`IUIStoreBackedAnimationTarget<T>`) ; un setter sur une propriete simple (`Opacity`, `RenderTransform.*`, `PreferredWidth`...) memorise la valeur sous-jacente a l'entree et la reecrit a la sortie. Une transition sur un pilote pose sa contribution `Animation` (100) au-dessus de l'etat.

Base : la base d'un chemin est memorisee a la premiere ecriture d'un etat (une seule fois tant qu'un etat pose ce chemin) ; si une transition est attachee au chemin, sa valeur de repos (`UITransition<T>.SettledValue`, la valeur vers laquelle elle va) sert de base, jamais la valeur en vol. Un pilote dont le conteneur a ete ecrit en bloc (constructeur, theme, style) n'a aucune contribution pour son sous-slot : en quittant l'etat, la base est remise comme valeur conservee, sans contribution (le store garde la valeur CLR quand la derniere contribution d'un slot part, ADR-0005 ; un refresh du fond par le theme la remplace donc toujours) ; si une transition tient encore le slot, la base est enregistree sous la source du conteneur et le run la retrouve a sa fin, et la base memorisee est gardee jusqu'a ce que le slot se repose.

Limites : une sortie d'etat pendant le run d'une transition sur un pilote n'est vue qu'a la fin du run (la contribution `Animation` masque l'ecriture, limite V1), et la base alors enregistree sous la source du conteneur bloque ensuite le refresh du fond par defaut du theme pour cet element (meme comportement qu'apres un run de transition couleur sur un slot sans contribution). Une valeur locale (90) l'emporte sur un setter d'etat nomme sur un pilote (precedence ADR-0005 : l'etat est enregistre mais dormant, `ALocalValue_ShadowsANamedStateOnAPilot`) ; les etats sont donc faits pour des fonds venant du theme, d'un style ou d'un template. Un setter ajoute ou remplace n'est reapplique qu'au prochain changement de nom (pas de refresh a chaud, sauf remplacement de l'etat courant qui le restaure d'abord). L'echelle d'etat `RenderScale` de V1 et les etats nommes coexistent : ils ecrivent des chemins differents. `Checked` se lit sur `IUICheckable.IsChecked == true` (une case indeterminee n'est pas cochee).

## XAML

DTO `MGUI.Core/UI/XAML/Animation.cs` :

```xaml
<Button Content="Hover me" RenderScale="1.05">
    <Button.RenderTransform>
        <RenderTransform Origin="0.5,0.5" Rotation="0" />
    </Button.RenderTransform>
    <Button.Transitions>
        <Transition Property="RenderScale" Duration="0.1" Easing="CubicOut" />
        <Transition Property="Background" Duration="150ms" Delay="0:0:0.05" />
    </Button.Transitions>
</Button>
```

`Transition.Property` doit etre un chemin enregistre, `Duration` / `Delay` acceptent des secondes (`0.15`), des millisecondes (`150ms`) ou un `TimeSpan` (`0:0:0.15`), `Easing` un nom connu ; chaque setter valide sa valeur, donc une erreur remonte comme diagnostic du loader strict (`InvalidValueConversion`, `SCN-MARKUP-001`). `RenderTransform` accepte `x,y` ou un nombre unique pour les vecteurs, `Rotation` en degres. Les transitions sont attachees apres les attributs de l'element (elles lisent la valeur courante en s'attachant).

## Diagnostics

`UIToolingService.CaptureElementDebugView(element)` liste les animations actives et retenues de l'element et ses transitions (chemin, etat, progression, nom) ; `RenderElementDebugView` les rend sous `animations:` et `transitions:`. `UIPerformanceProbe` expose la phase `Animations` du desktop.

## Cout

- Element sans animation ni transform : deux references nulles, aucun abonnement, aucun travail par frame ni par evenement souris.
- Element transforme : une construction de matrice au draw et deux coupures de batch ; au survol, une inversion de matrice par ancetre transforme.
- Animation active : aucune allocation par tick hors cibles couleur (brush unie boxee, bordure uniforme allouee) ; un pilote de layout invalide le layout de sa fenetre a chaque tick.
- Transition : un abonnement `PropertyChanged` (ou deux pour `RenderScale`) par transition attachee, un run alloue par changement.

## Limites connues

- Un enfant clippe par son parent (`ClipToBounds`) et transforme est teste dans son clip local : le scissor du parent n'est pas transforme.
- Un element transforme a l'interieur d'un `MGToolTip` a un pivot faux au hit-test (la tooltip est dessinee avec un decalage a la souris sans equivalent cote update, defaut preexistant).
- `MGWindow.InvalidatePressedAndHoveredElements` n'est jamais remis a false par la fenetre (preexistant) : apres un premier transform, le survol est recalcule a chaque frame de cette fenetre.
- `ActiveRenderTransformCount` n'est pas decremente quand une fenetre se ferme avec un transform actif (cout de parcours seulement).
- Une ecriture locale d'un pilote pendant sa transition n'est pas notifiee (masquee par la contribution `Animation`) et n'est animee qu'a la fin du run.
- `Background` n'interpole que les brushes unies ; `Background.Gradient` et `Background.DiagonalGradient` interpolent deux gradients du meme type (couleur par couleur, le coin du gradient diagonal bascule a mi-parcours) ; textures et nine-slices ne sont pas interpolables ; un slot d'etat null refuse le demarrage.
- Le fondu des overlays Hover / Pressed (`VisualStateFillBrush.OverlayOpacity`, ADR-0007 decision 4) est un fondu a l'entree seulement : l'overlay peint est celui de l'etat courant, donc a la sortie de l'etat il disparait avec lui pendant que la transition ramene l'opacite a 0. Seuls les sites de `MGElement` et `MGBorder` appliquent `OverlayOpacity` ; `MGProgressBar`, `MGScrollViewer`, `MGWindow` (barre de titre), `MGUniformGrid`, `MGGridSplitter` et `MGSlider` lisent encore l'overlay directement (opacite 1). Un remplacement entier du conteneur de fond (changement de theme, ecriture `Background` d'un style ou du code) repart avec `OverlayOpacity = 1` : un fondu en cours a ce moment saute.
- `Thickness` est en entiers : une marge animee avance par pixels entiers.
- Les abonnements d'une transition ne sont liberes qu'a son retrait, pas au detachement de l'element (l'element possede la transition, aucune fuite au-dela de sa vie).

## Reste a faire

Le programme V2 est dans `Docs/Tasks/animation-v2-tasks.md` ; livre : composition (T1), keyframes (T2), fondu des overlays et cibles supplementaires (T3), etats visuels nommes (T4). Reste : styles et theme (T5), `MGProgressButton` sur le moteur (T6), API fluente (T7), sample et validation (T8). L'editeur de timeline vit dans le moteur de jeu de l'auteur.
