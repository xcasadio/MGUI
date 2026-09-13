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

Easing (`Easing/`) : `IUIEasingFunction`, `UIEasing` (dix-neuf fonctions : Linear, Quad/Cubic/Sine/Back/Bounce/Elastic In/Out/InOut ; `TryGet(nom)` insensible a la casse pour le XAML ; `Register`). Voir « Easings » ci-dessous pour les courbes de Bezier.

## Easings

`IUIEasingFunction.Ease(float)` (sans etat, sans allocation) est resolu par nom via `UIEasing.TryGet`, utilise par les transitions XAML, le groupe d'animation du theme, le JSON de keyframes et l'API fluente.

- Registre : dix-neuf fonctions integrees (Linear, Quad/Cubic/Sine/Back/Bounce/Elastic In/Out/InOut), insensibles a la casse ; `UIEasing.Register(nom, fonction)` ajoute ou remplace une fonction applicative ; `UIEasing.Names` enumere toutes les cles courantes.
- Bezier cubique (`UICubicBezierEasing`, ADR-0008 decision 1, Docs/Tasks/animation-v3-tasks.md U1) : quatre flottants `X1`, `Y1`, `X2`, `Y2` (semantique CSS `cubic-bezier`) ; `X1` et `X2` sont bornes a [0, 1] a la construction (`ArgumentOutOfRangeException` sinon, comme toute valeur non finie) ; `Y1` / `Y2` sont libres, un depassement de [0, 1] est autorise (comme `BackOut`). `Ease` resout x(u) = t par Newton-Raphson (jusqu'a 8 iterations, tolerance 1e-6) avec repli par bissection (jusqu'a 32 iterations) quand la derivee est proche de zero, puis rend y(u) ; aucune allocation.
- Syntaxe textuelle acceptee par `UICubicBezierEasing.TryParse` (et par `UIEasing.TryGet` en repli) : la forme canonique `cubic-bezier(x1,y1,x2,y2)` (prefixe insensible a la casse) et la forme courte `bezier:x1,y1,x2,y2` ; les espaces autour de chaque nombre et a l'interieur des parentheses sont tolerees. `ToString()` rend toujours la forme canonique, sans espace, en culture invariante. Aucun preset CSS nomme (`ease`, `ease-in`...) n'est fourni : l'appelant ecrit le litteral.
- Resolution et cache : `UIEasing.TryGet(nom)` cherche d'abord dans le registre, puis tente `UICubicBezierEasing.TryParse` sur un echec ; un litteral qui se parse est insere dans le registre sous le texte exact (apres `Trim`) que l'appelant a fourni, pas sous sa forme canonique — deux lookups du meme litteral rendent la meme instance (utile pour `Assert.Same` et pour eviter de reconstruire la courbe a chaque frame), mais `cubic-bezier(0.42,0,1,1)` et `cubic-bezier(0.42, 0, 1, 1)` restent deux entrees distinctes de courbes identiques. `Names` continue d'enumerer les cles : les dix-neuf built-ins restent un sous-ensemble garanti, le reste croit avec l'usage.
- Consequence pour un editeur de courbes en direct : chaque glisser de poignee qui change `x1..y2` produit un nouveau litteral et donc une nouvelle entree de registre qui ne sera jamais retiree (le registre n'a pas de politique d'eviction). Un editeur qui pousse des valeurs en continu doit soit quantifier les points de controle (par exemple a deux decimales) avant de composer le litteral, soit construire un `UICubicBezierEasing` directement et l'assigner a `Easing` (`UITransition<T>.Easing`, `UIAnimation<T>.Easing`) sans passer par la chaine et donc sans toucher au registre.

## Composition

`UIAnimationGroup` (`MGUI.Core/UI/Animation/Composition/`, ADR-0007 decision 1) est une `UIAnimation` possedee par un element racine (`root.Animations.Start(group)`) qui demarre ses enfants par le manager, chacun sur son propre element (`child.Owner` s'il en a deja un, la racine sinon) : chaque enfant garde sa cle de conflit, apparait dans les diagnostics de son element et suit l'annulation par element. Le groupe n'ecrit aucune propriete : son chemin est synthetique (`UIStoryboard#n`), il n'entre jamais en conflit. L'annuler annule ses enfants actifs selon leur propre `CancelBehavior` ; `RepeatCount` / `RepeatForever` relancent les enfants ; `AutoReverse` est refuse (les enfants ne sont pas rembobines) ; un enfant `RepeatForever` est refuse (repeter le groupe).

- `UIStoryboard` : parallele, tous les enfants demarrent avec lui, sa duree est celle du plus long (delai et repetitions compris) ; initialiseur de collection.
- `UISequenceAnimation` : `Append`, `AppendDelay` ; chaque enfant demarre a l'offset ou le precedent se termine sur la ligne de temps de la sequence (`GetStartOffset`), deterministe meme si un enfant est remplace tot par une animation en conflit.
- `UIDelayAnimation` : une pause sans cible.

Regle de frame : une animation demarree pendant un tick du manager (enfant d'un groupe, run de transition) avance a partir de la frame suivante, si bien qu'un enfant planifie a un offset reste aligne sur la ligne de temps du groupe (la sequence lit la position exacte en ticks, `UIAnimation.IterationElapsed`). Un enfant demarre au `Begin` du groupe (storyboard) est enregistre avant le groupe et avance avant lui : a une frontiere d'iteration il complete d'abord, puis le `Repeated` du groupe le relance ; chaque iteration se termine donc par le `Completed` de l'enfant. Un groupe garde `FillBehavior = HoldEnd` (refus sinon) ; `Animations.Clear()` sur la racine annule les enfants places sur d'autres elements selon leur propre `CancelBehavior`, sans restauration forcee.

## Preview et seek

`UIAnimationPreview.Attach(element, animation)` (`MGUI.Core/UI/Animation/UIAnimationPreview.cs`, ADR-0008 decision 5b ; Docs/Tasks/animation-v3-tasks.md U7) begine une instance de preview pour un hote externe (le scrubber d'un editeur de timeline du moteur de jeu) : l'instance est demarree SANS le `UIAnimationManager` (`UIAnimation.Manager` reste null) -- elle n'est jamais enregistree, jamais tickee, jamais balayee par le sweep du manager, jamais annulee quand l'element quitte l'arbre ou que sa fenetre se ferme. L'hote possede entierement le cycle de vie de l'element et de l'animation ; la seule facon de terminer une preview est `Cancel()` (ou son alias `UIAnimationPreview.Detach`), qui applique `CancelBehavior` et leve `Cancelled` exactement comme pour une animation vivante. Un `Attach` refuse par une validation d'un groupe (`AutoReverse`, `FillBehavior` invalides) laisse l'instance reutilisable : `IsPreview`/`State` ne sont poses qu'apres le succes de `OnPreviewAttached`/`OnStarting` (fix round 1, U7), exactement comme `Begin` ne pose `State` qu'apres `OnStarting` -- une tentative corrigee peut donc rappeler `Attach` sur la meme instance.

`UIAnimation.Seek(TimeSpan elapsed)` positionne une instance de preview a un instant donne, en avant ou en arriere, sans jamais lever d'evenement (`Started`, `Updated`, `Repeated`, `Reversed`, `Completed`) et sans jamais atteindre `UIAnimationState.Completed` : une preview qui depasse sa fin reste sur la pose finale (1, ou 0 apres un nombre pair de passes `AutoReverse`) jusqu'a `Cancel()` -- `Completed -> Running` ne se produit donc jamais, puisque `Completed` n'est jamais atteint (passe de conception U7). `Seek` refuse (`InvalidOperationException`) une instance qui n'est pas une preview active : jamais attachee (`Stopped`), enregistree aupres d'un manager vivant, ou deja annulee. A l'interieur du delai (`elapsed < Delay`), la pose est la progression 0 (la valeur `From`/de depart), pour qu'un scrubber montre la pose initiale. Les maths de progression et d'iteration sont partagees avec `Advance` par un helper prive (`ComputeProgress`, tick-based) : `Advance` n'est pas modifie (les suites existantes restent vertes, inchangees) ; au-dela de la derniere iteration, le helper rend une pose figee canonique (la fin de la derniere iteration, cote avant pour une passe simple, cote retour pour `AutoReverse`), independante de la distance parcourue au-dela -- `Advance` l'ignore (il appelle `Complete` et garde `Iteration`/`IsReversing` de la derniere frame reelle, comme avant), `Seek` l'utilise comme pose figee au-dela de la fin.

Un composite (`UIAnimationGroup`) positionne chaque enfant a son propre instant relatif a son offset (0 pour un storyboard, `UISequenceAnimation.GetStartOffset` pour une sequence ; un enfant avant son offset est seeke a 0) via un hook `OnSeek` (par defaut identique a `ApplyProgress` pour une animation simple, redefini par le groupe pour positionner les enfants au lieu de les demarrer) ; `StartDueChildren`/`ApplyProgress` ne s'executent jamais pour une preview (garde sur `IsPreview`). Les enfants d'un groupe de preview sont attaches comme previews au moment de `Attach`, recursivement, sur `child.Owner ?? racine` (hook `OnPreviewAttached`), jamais via `StartChild` : `FinishedChildren`/`HandleChildFinished` ne sont donc jamais impliques (pas de completion). `OnPreviewAttached` s'execute avant `OnStarting` dans `BeginPreview` (fix round 1, U7) : `OnStarting` d'un groupe calcule sa propre `Duration` a partir de celle de ses enfants (`ComputeDuration`/`LengthOf`), et un enfant qui est lui-meme un groupe n'a une `Duration` correcte qu'une fois son propre `OnStarting` execute -- ce que `OnPreviewAttached` declenche recursivement. Avec l'ordre inverse, un groupe imbrique (storyboard dans storyboard, groupe dans une sequence) restait toujours a `Duration` zero au moment ou le parent calculait la sienne, figeant tout `Seek` a la pose initiale quel que soit `t` ; corrige a n'importe quelle profondeur d'imbrication puisque l'attache recursive complete avant que chaque ancetre ne lise la duree de ses enfants.

Limite documentee (verifiee seulement a l'attache) : une preview et une animation vivante ne doivent jamais coexister sur le meme (element, chemin) -- les deux ecriraient la meme cible par le meme `ApplyProgress`/`IUIAnimationTarget<T>`, si bien que le dernier tick ou seek de la frame l'emporte, un conflit qu'aucun des deux cotes ne peut detecter. `Attach` refuse quand une animation vivante occupe deja le chemin (`UIAnimationCollection.IsAnimating`, une recherche existante bon marche, aucun etat nouveau) ; le sens inverse n'est pas garde (et ne peut pas l'etre sans apprendre au manager l'existence des previews, ce qui annulerait l'interet d'une preview invisible pour lui) : demarrer une animation vivante sur le meme chemin apres qu'une preview a ete attachee se deroule exactement comme si la preview n'existait pas, et les deux se disputent alors la valeur a chaque frame jusqu'a ce que la preview soit detachee.

U8 (ADR-0008, decision 8) : `UIAnimation.PresetOwner(MGElement)` (interne) pose `Owner` avant que l'instance ne demarre, en remplacement de l'idiome Start-puis-Cancel que `CompositionTests` utilisait pour lier un enfant a un second element (le demarrer pour de vrai sur cet element, puis l'annuler, ce qui laisse `Owner` pose mais `State` a `Cancelled` plutot qu'a `Stopped`) -- refuse si l'instance n'est pas `Stopped`. `StartChild` et `OnPreviewAttached` lisaient deja `child.Owner ?? racine` : le prereglage suffit donc pour le jeu vivant comme pour une preview, sans autre changement. C'est le mecanisme que `UIAnimationSerializer.Deserialize` (section « Serialisation ») utilise pour un noeud dont `element` nomme un element different de la racine. U8 corrige aussi le P3 laisse ouvert par la revue finale de U7 ci-dessus : un `Attach` de groupe refuse par `OnStarting` (`AutoReverse`/`FillBehavior` invalides) laissait les enfants deja attaches par `OnPreviewAttached` bloques a `Running`/preview alors que le groupe lui-meme revenait a `Stopped` -- `BeginPreview` enveloppe desormais les deux hooks dans un `try`/`catch` qui appelle un nouveau hook, `OnPreviewAttachFailed` (no-op par defaut, scelle par `UIAnimationGroup` pour annuler recursivement tout enfant deja `IsPreview`), avant de relancer l'exception ; sans risque de valeur a restaurer, puisque `OnPreviewAttached` n'ecrit jamais rien par elle-meme (seul le `Seek(0)` de l'appelant, apres un `BeginPreview` reussi, ecrit une valeur).

## Keyframes

`UIKeyFrame<T>(Offset, Value, Easing)` et `UIKeyFrameTrack<T>` (`MGUI.Core/UI/Animation/KeyFrames/`, ADR-0007 decision 2) forment un modele de donnees pur, sans reference a un element : cles triees par offset dans [0,1], derniere cle a 1 (`Validate`), l'easing d'une cle s'applique au segment qui se termine sur elle, une piste sans cle a 0 part de la valeur courante au demarrage. `UIKeyFrameAnimation<T>` (derive de `UIPropertyAnimation<T>`) : `Track`, segment trouve par recherche binaire (`FindSegment`), `From` / `To` pris de la piste, `Easing` global ignore ; delai, repetition, aller-retour, comportements de fin et d'annulation, appartenance et conflits sont ceux du moteur.

Format JSON (`UIKeyFrameSerializer`, `Serialize` / `Deserialize<T>` / `ReadValueType`, version 1) pour l'editeur du moteur de jeu :

```json
{ "version": 1, "valueType": "Single", "frames": [ { "offset": 0, "value": "0" }, { "offset": 1, "value": "1", "easing": "CubicOut" } ] }
```

Valeurs en chaines invariantes : `Single` / `Double` / `Int32` en nombre, `Vector2` / `Vector3` / `Vector4` en `x,y[,z[,w]]`, `Color` en `#RRGGBBAA`, `Thickness` en `l,t,r,b` ; type ou version inconnus refuses explicitement, le type du JSON doit correspondre au `T` demande.

### Clip multi-pistes

`UIKeyFrameClipSerializer` (`KeyFrames/UIKeyFrameClipSerializer.cs`, ADR-0008 decision 6) serialise un `UIStoryboard` compose uniquement de `UIKeyFrameAnimation<T>` (une piste par chemin anime) pour un seul element : `UIKeyFrameClipDto { Version, Duration, Tracks[] { Property, ValueType, Frames[] } }`, `Format` / `Parse` (et les options JSON) partages avec `UIKeyFrameSerializer`. La duree du clip fait autorite : chaque piste doit la partager exactement (pas de delai, pas de repetition, pas d'aller-retour) ; a la serialisation, la duree du clip est celle du storyboard (`Duration` public de `UIAnimation`, deja calculee s'il a ete joue) ou, a defaut (storyboard construit mais jamais joue), la plus longue piste.

```json
{
  "version": 1,
  "duration": "00:00:01",
  "tracks": [
    { "property": "Opacity", "valueType": "Single", "frames": [ { "offset": 0, "value": "0" }, { "offset": 1, "value": "1" } ] },
    { "property": "RenderTransform.Scale", "valueType": "Vector2", "frames": [ { "offset": 0, "value": "0.8,0.8" }, { "offset": 1, "value": "1,1" } ] },
    { "property": "Background", "valueType": "Color", "frames": [ { "offset": 0, "value": "#00000000" }, { "offset": 1, "value": "#FF0000FF" } ] }
  ]
}
```

`Deserialize` refuse explicitement : version inconnue, chemin inconnu de `UIAnimationTargets` (le message liste les chemins connus, tries), `valueType` ne correspondant pas au type attendu par la cible ou non supporte par `Format` / `Parse` (ex. `MinHeight`, dont la cible est `int?`), un clip sans piste ou une piste sans cadre, des cadres invalides (`UIKeyFrameTrack<T>.Validate`, dernier cadre pas a l'offset 1). Deux pistes sur le meme chemin ne sont pas refusees a la deserialisation : la regle de conflit existante du moteur (une animation active par proprietaire et par chemin) s'applique de facon deterministe, puisque le storyboard demarre ses enfants dans l'ordre du fichier et que la derniere piste declaree l'emporte.

Le storyboard retourne par `Deserialize` n'a pas de proprietaire : on le joue avec `element.Animations.Start(UIKeyFrameClipSerializer.Deserialize(json))`, un clip par element (reponse de l'auteur, question 4). Le remap temporel d'un composite (rejouer le clip plus vite ou plus lentement que sa duree d'origine) est explicitement hors perimetre de cette tranche.

## Serialisation

`UIAnimationSerializer` (`KeyFrames/UIAnimationSerializer.cs`, ADR-0008 decision 8, U8) serialise un arbre d'animation complet -- un `UIStoryboard`, une `UISequenceAnimation`, un `UIDelayAnimation`, un `UIPropertyAnimation<T>` ou un `UIKeyFrameAnimation<T>`, imbriques a volonte -- sans aucune reference a un element : precedent `GraphSerializer` (aucune instance de controle dans le DTO). Un clip (`UIKeyFrameClipSerializer` ci-dessus) est le sous-ensemble particulier que cette classe peut exprimer : un `UIStoryboard` dont tous les enfants sont des `UIKeyFrameAnimation<T>`, aucun preset sur un autre element.

```json
{
  "version": 1,
  "root": {
    "kind": "Storyboard",
    "children": [
      { "kind": "Property", "path": "Opacity", "valueType": "Single", "duration": "00:00:00.3", "to": "1", "easing": "CubicOut" },
      { "kind": "Delay", "duration": "00:00:00.1" },
      {
        "kind": "KeyFrames", "path": "RenderTransform.Scale", "valueType": "Vector2", "duration": "00:00:00.4",
        "track": [ { "offset": 0, "value": "0.8,0.8" }, { "offset": 1, "value": "1,1" } ]
      },
      {
        "kind": "Sequence", "element": "popup",
        "children": [ { "kind": "Property", "path": "Opacity", "valueType": "Single", "duration": "00:00:00.2", "to": "0" } ]
      }
    ]
  }
}
```

`Kind` vaut `Storyboard`, `Sequence`, `Delay`, `Property` ou `KeyFrames` ; chaque noeud porte les reglages communs de `UIAnimation` (`duration`, `delay`, `repeatCount`, `repeatForever`, `autoReverse`, `fillBehavior`, `cancelBehavior`, `name`) -- `duration` n'est ecrite (et relue) que pour un noeud `Delay`, `Property` ou `KeyFrames` : celle d'un groupe (`Storyboard`/`Sequence`) est recalculee par le moteur a chaque demarrage a partir de ses enfants (`UIAnimationGroup.OnStarting` -> `ComputeDuration`), jamais faisant autorite pour un arbre non demarre, comme deja note pour `UIStoryboard.Duration` dans le clip ci-dessus.

`element` (optionnel, sur n'importe quel noeud, la racine incluse) nomme l'element sur lequel `UIAnimation.PresetOwner` (interne, U8) prereglera le noeud avant qu'il ne demarre -- absent, le noeud demarre normalement sur l'element qui le possede deja (le parent du groupe, ou l'element choisi par l'appelant pour la racine via `element.Animations.Start(root)`). Preregler la racine elle-meme est accepte (pas un cas particulier) : cela ne change rien a un `Start` explicite, mais rend `root.Play()` utilisable sans reprendre l'element en main. `Serialize` appelle `nameOf` uniquement pour un noeud dont `Owner` est deja pose (un enfant preregle sur un autre element que la racine) ; `Deserialize` appelle `resolveElement` pour chaque noeud dont `element` est renseigne.

`Path`/`ValueType` (noeuds `Property`/`KeyFrames`) suivent les memes regles que le clip : chemin enregistre dans `UIAnimationTargets`, type de valeur egal a celui de la cible et supporte par `UIKeyFrameSerializer` (built-in ou `RegisterValueFormat<T>`). `From`/`To` (noeud `Property`) sont les chaines invariantes de `UIKeyFrameSerializer.Format`/`Parse` ; `From` absent signifie que `HasFrom` est faux (l'animation part de la valeur courante). `Easing` (noeud `Property`) est soit le litteral canonique d'un `UICubicBezierEasing` (`ToString()`), soit le nom sous lequel `UIEasing.TryGet` resout exactement cette instance (`ReferenceEquals`) -- absent, `null`, veut dire lineaire ; une fonction applicative jamais enregistree par nom n'a donc pas de forme serialisable, refusee explicitement (l'enregistrer avec `UIEasing.Register` ou utiliser un Bezier resout le probleme). `Track` (noeud `KeyFrames`) est la meme liste de cadres que `UIKeyFrameSerializer`/`UIKeyFrameClipSerializer`, avec le meme tolerance sur les noms d'easing de cadre (non valides eagerement, comportement V2 inchange). Un groupe (`Storyboard`/`Sequence`) vide est accepte, pas refuse -- coherent avec `CompositionTests` (« groupe vide » complete a son premier tick).

Extension : `UIKeyFrameSerializer.RegisterValueFormat<T>(format, parse)` (U8) est le point d'extension partage par les trois serialiseurs (`UIKeyFrameSerializer`, `UIKeyFrameClipSerializer`, `UIAnimationSerializer`) pour un type applicatif hors de l'ensemble ferme (`Single`, `Double`, `Int32`, `Vector2/3/4`, `Color`, `Thickness`) : registre thread-safe (`ConcurrentDictionary`, meme forme que `UIInterpolators.Register<T>`), la derniere inscription pour un type l'emporte. `Format`/`Parse` retombent dessus pour un type non integre ; ni integre ni enregistre leve `NotSupportedException` nommant le type et cette methode.

`Serialize` refuse explicitement : un noeud actif (`State` autre que `Stopped`, la racine ou un descendant) ; un type de noeud non serialisable ; un `Property`/`KeyFrames` sans chemin ou avec un chemin inconnu (liste les chemins connus) ; une piste de cadres vide (meme regle que le clip, ADR-0008 U6 P3) ; un type de valeur sans `Format` (integre ou enregistre) ; un easing non nommable ; un enfant preregle sur un element pour lequel `nameOf` rend `null` ou vide. `Deserialize` refuse explicitement : une version inconnue ; un `kind` absent ou inconnu (liste les kinds connus) ; un `path` inconnu (liste les chemins connus, tries) ; un `valueType` incoherent avec celui de la cible, ou non supporte par `UIKeyFrameSerializer` (le message nomme `RegisterValueFormat`) ; une piste `KeyFrames` absente, vide ou invalide (`UIKeyFrameTrack<T>.Validate`) ; une valeur de cadre invalide ; un `element` que `resolveElement` ne resout pas ; un `easing` que `UIEasing.TryGet` ne resout pas (liste `UIEasing.Names`). Chaque message nomme le noeud fautif par son chemin dans l'arbre (`root`, `root/0`, `root/0/1`...).

## Cibles

`IUIAnimationTarget<T>` (`Path`, `IsStoreBacked`, `RequiredOwnerType`, `GetValue`, `SetValue(element, value, nom)`, `RestoreBaseValue(element, base)`) et le registre ferme `UIAnimationTargets` (`Register`, `TryGet`, `Resolve`, `GetValueType`, `GetOwnerType`, `IsApplicable`, `Paths`), chemins insensibles a la casse. `UIDelegateAnimationTarget<T>` pour une propriete applicative.

Applicabilite (ADR-0008, decision 2) : `RequiredOwnerType` (membre d'interface par defaut, null = n'importe quel `MGElement`) est renseigne uniquement par les cibles dont l'implementation caste reellement vers un type concret (leur helper `Require`) ; declarer un type qu'une cible n'impose pas dans son propre code serait un bug de la cible, pas un assouplissement. `UIAnimationTargets.GetOwnerType(path)` lit ce type au moment de l'enregistrement (aucune reflexion par appel) ; `IsApplicable(path, element)` combine chemin connu et type compatible, false sans exception pour un chemin inconnu. `BorderBrush` resout sa bordure par `MGElement.GetBorder()`, une facade que la plupart des elements a bordure redefinissent (pas un cast strict vers `MGBorder`) : son `RequiredOwnerType` reste donc null, n'importe quel element possedant une bordure accepte ce chemin.

Cibles framework (`Targets/UIBuiltInAnimationTargets.cs`, `Targets/UIColorAnimationTargets.cs`) :

| Chemin | Type | Element requis | Famille | Restauration |
| --- | --- | --- | --- | --- |
| `Opacity` | float | - | simple | valeur de base gardee par le moteur |
| `RenderTransform.Translation`, `.Scale`, `.Origin` | Vector2 | - | simple | idem |
| `RenderTransform.Rotation` | float (degres) | - | simple | idem |
| `RenderScale` | float | - | override de l'echelle d'etat | efface l'override (retour a l'echelle de l'etat courant) |
| `Margin`, `Padding` | Thickness | - | pilote (layout, couteux) | retrait de la contribution `Animation` |
| `MinHeight` | int? | - | pilote (layout) | idem |
| `Background`, `Background.Selected`, `.Disabled`, `.Focused` | Color | - | pilote (slot du fond, brush unie seulement) | retrait, puis reecriture de la base sous la source du conteneur si le slot n'avait aucune autre contribution |
| `Foreground` | Color | `MGTextBlock` | pilote | idem |
| `TextForeground` | Color | - | pilote (`DefaultTextForeground.Normal`) | idem |
| `BorderBrush` | Color | - (bordure requise, tout element via `GetBorder()`) | pilote (bordure `GetBorder()`, uniforme et unie) | idem |
| `Background.Overlay` | float | - | simple (`VisualStateFillBrush.OverlayOpacity`, valeur sous-jacente 1 si survole ou presse, 0 sinon) | valeur de base gardee par le moteur |
| `PreferredWidth`, `PreferredHeight` | int? | - | simple (layout, couteux) | idem |
| `Background.Gradient` | `UIGradientColors` (4 coins) | - | pilote (slot Normal, `MGGradientFillBrush` seulement) | retrait, puis base sous la source du conteneur |
| `Background.DiagonalGradient` | `UIDiagonalGradientColors` (2 couleurs + coin) | - | pilote (slot Normal, `MGDiagonalGradientFillBrush` seulement) | idem |
| `ProgressButton.Value` | float | `MGProgressButton` | simple, non observable (refus explicite ailleurs ; ecrit par `ApplyAnimatedValue`) | valeur de base gardee par le moteur ; le run de `Duration` garde la valeur courante |
| `TextBlock.TextProgress` | double | `MGTextBlock` | simple, non observable (refus explicite ailleurs ; ecrit par `ApplyAnimatedTextProgress`) | valeur de base gardee par le moteur ; le run de `TextCharactersPerSecond` garde la progression courante |

Base et valeur animee : pour un pilote, la valeur effective est le gagnant du store, `Animation` (100) etant la plus forte ; a la fin ou a l'annulation avec restauration, la contribution est retiree et la meilleure source suivante reprend (`UIToolingService.TryGetResolvedValueSource` rapporte `Animation` pendant l'animation). `HoldEnd` sur un pilote garde la contribution (`IsHeld`) jusqu'a la prochaine animation du meme chemin ou `Animations.Clear()`, et masque une ecriture locale posee entre-temps ; sur une propriete simple, la valeur finale reste simplement la valeur CLR et une ecriture locale ulterieure l'emporte (asymetrie assumee).

## Transitions

`UITransition<T>` (`element.Transitions.Add(...)`, `UITransitionCollection`) interpole automatiquement chaque changement de sa propriete : `Property`, `Duration`, `Delay`, `Easing`, `Interpolator`. La cible doit etre observable (`IUIObservableAnimationTarget<T>` : `Subscribe`, `GetUnderlyingValue`), ce que toutes les cibles framework sont ; `RenderScale` reagit a `VisualStateChanged` (survol entree et sortie) et aux changements de `RenderScale`.

Regles : au changement, le run (`UIPropertyAnimation<T>` enregistre au manager) part de la valeur animee courante ou de la derniere valeur memorisee (`SettledValue`) vers la valeur sous-jacente ; une ecriture pendant le run recible depuis la valeur courante ; une animation explicite sur le meme chemin remplace le run et la transition se tait en suivant les valeurs ; retirer la transition garde la valeur courante ; `FillBehavior` = `RestoreBaseValue` pour une cible store (retour a la valeur locale visee) et `HoldEnd` pour une propriete simple. Le manager tickant avant le calcul des etats visuels dans la meme frame, le run de survol entrant avance d'un dernier pas avant que le run sortant reparte exactement de la.

U3 (ADR-0008) : sur un pilote (cible `IUIStoreBackedAnimationTarget<T>`), la valeur sous-jacente que le run doit rejoindre n'est plus lue comme la valeur physique (animee) du conteneur mais comme le gagnant du store sous sa propre contribution `Animation` (`TryGetValueBelowAnimation`, secours sur `GetUnderlyingValue` si rien ne reste en dessous) : une ecriture locale ou une sortie d'etat nomme pendant le run est donc vue des le prochain tick de l'interpolation en cours (celui qui note le changement), pas seulement a la fin du run, et le run reciblee part de sa valeur animee courante, jamais d'un saut. Les ecritures propres du run (la contribution `Animation` elle-meme) sont exclues de cette lecture, donc un tick du run ne se recible jamais lui-meme.

## Etats visuels nommes

`UIVisualState` (`element.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { "RenderTransform.Scale", new Vector2(1.05f) } })`, `UIVisualStateCollection`) : un nom et des setters types par chemin de cible (`UIAnimationTargets`). Un setter est lie a sa cible a la creation (type de valeur verifie, chemin inconnu refuse) ; aucune reflexion par frame. Chaque frame, apres le calcul de `VisualState` dans `MGElement.Update`, la collection resout le nom courant dans l'ordre `Disabled`, `Checked` (element `IUICheckable` : `MGToggleButton`, `MGCheckBox`, `MGRadioButton`), `Selected`, `Pressed`, `Hover` (survole ou presse), `Focused`, `Normal` : le premier etat dont la condition tient ET que l'element definit gagne (un element avec `Normal` et `Hover` montre `Hover` pendant l'appui). Au changement, les chemins de l'etat quitte que le suivant ne pose pas sont restaures, puis les setters du suivant sont ecrits : une transition sur un chemin de setter voit une ecriture par chemin et interpole le changement. `element.CurrentVisualStateName` expose le nom courant ; `IsEnabled = false` gele l'etat applique ; `Remove` / `Clear` restaurent l'etat courant.

Sources : un setter sur un pilote (`Background`, `Foreground`, `BorderBrush`, `Margin`, `Padding`, `MinHeight`, gradients) ecrit la contribution `VisualState` (70) du store avec le nom `visualstate:<Nom>` et la retire en quittant (`IUIStoreBackedAnimationTarget<T>`) ; un setter sur une propriete simple (`Opacity`, `RenderTransform.*`, `PreferredWidth`...) memorise la valeur sous-jacente a l'entree et la reecrit a la sortie. Une transition sur un pilote pose sa contribution `Animation` (100) au-dessus de l'etat.

Base : la base d'un chemin est memorisee a la premiere ecriture d'un etat (une seule fois tant qu'un etat pose ce chemin). Pour une cible pilote (U3), la base est desormais le gagnant du store sous la contribution `Animation` (`TryGetValueBelowAnimation`), lecture qui reste correcte meme en plein run (les ticks du run ne touchent que sa propre contribution) : le cas particulier de `UITransition<T>.SettledValue` n'est donc plus necessaire pour ces cibles. Pour une propriete simple (sans store a lire en dessous), sa valeur physique EST la valeur animee pendant un run : la base est toujours lue depuis la transition attachee au chemin (`SettledValue`, la valeur vers laquelle elle va), jamais la valeur en vol. Un pilote dont le conteneur a ete ecrit en bloc (constructeur, theme, style) n'a aucune contribution pour son sous-slot : en quittant l'etat, la base est remise comme valeur conservee, sans contribution (le store garde la valeur CLR quand la derniere contribution d'un slot part, ADR-0005 ; un refresh du fond par le theme la remplace donc toujours) ; si une transition tient encore le slot, la base est enregistree sous la source du conteneur, et grace a U3 le run deja en cours la voit et se recible vers elle des son prochain tick (plus besoin d'attendre la fin du run), et la base memorisee est gardee jusqu'a ce que le slot se repose.

Limites : une valeur locale (90) l'emporte sur un setter d'etat nomme sur un pilote (precedence ADR-0005 : l'etat est enregistre mais dormant, `ALocalValue_ShadowsANamedStateOnAPilot`) ; les etats sont donc faits pour des fonds venant du theme, d'un style ou d'un template. Un setter ajoute a un etat qui n'est pas le courant n'est applique qu'au prochain changement de nom ; depuis U9, remplacer (`UIVisualStateCollection.Add`) l'etat qui EST courant restaure d'abord ses anciens setters puis reapplique aussitot les nouveaux (avant U9, seule la restauration avait lieu, les nouvelles valeurs n'apparaissant qu'au prochain changement d'etat). L'echelle d'etat `RenderScale` de V1 et les etats nommes coexistent : ils ecrivent des chemins differents. `Checked` se lit sur `IUICheckable.IsChecked == true` (une case indeterminee n'est pas cochee).

U5 (ADR-0008) : `MGToggleButton.CheckedBackgroundBrush` / `CheckedTextForeground` sont desormais un vrai slot `Background.Checked`, pas un simple alias de `SelectedValue` : `VisualStateBrush<T>.CheckedValue`/`HasCheckedValue` (fill et couleur, code seul, pas de champ theme DTO, pas de slot `UIResolvedPropertyStore`) porte la valeur, avec `SelectedValue` en secours quand rien n'est pose. Cote dessin, `MGElement.DrawBackground` passe par un seul point d'extension protege (`ResolveBackgroundUnderlay`) que `MGToggleButton` redefinit pour lire `CheckedValue` pendant `IsChecked` ; les neuf autres sites `GetUnderlay` du framework sont inchanges. Limite : `Background.Checked` n'est ni une cible d'animation ni un slot du store -- pour animer un fond a la coche, l'etat nomme `Checked` ci-dessus reste la seule voie (une transition ou un `VisualStateDefinition.OverridesLocalValue` ne voient pas ce slot). `IsChecked` continue de poser `IsSelected` : sur un theme sans `CheckedBackgroundBrush`/`CheckedTextForeground` code, le rendu a la coche est exactement celui d'avant cette tranche (repli sur `SelectedValue`). `MGCheckBox` et `MGRadioButton` sont hors perimetre : leur coche est dessinee par des elements enfants, pas par `BackgroundBrush`/`DefaultTextForeground`.

`UIVisualState.OverridesLocalValue` (U4, ADR-0008 decision 4) : leve la limite ci-dessus quand on le demande explicitement. Par defaut faux (inchange). Mis a vrai, tous les setters de CET etat (granularite par etat, pas par setter) sont ecrits au palier `UIValuePrecedence.VisualStateOverride` (95, genre `VisualState` inchange) au lieu du palier ordinaire (70) : l'etat l'emporte alors sur une `LocalValue` (90) et sur une `LocalBinding` (80), mais jamais sur une `Animation` (100) qui garde la main pendant sa duree et rend la couleur de l'etat des qu'elle se termine (`RestoreBaseValue`) ou qu'on la vide (`Clear`). Le drapeau ne change rien pour un setter sur une propriete simple (aucun store en dessous a l'entree) : il ecrit toujours a travers la cible comme avant. En XAML, `VisualStateDefinition.OverridesLocalValue` (defaut faux) transfere le drapeau, sur un etat de l'element comme sur un etat de style. Diagnostic : `UIToolingService`/`TryGetResolvedValueSource` rapporte le palier 95 sans changement de code (la precedence est deja portee par `UIValueResolutionSource`).

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

Etats visuels nommes (`VisualStateDefinition`, T5) :

```xaml
<Button Content="Hover me">
    <Button.VisualStates>
        <VisualStateDefinition Name="Hover">
            <Setter Property="RenderTransform.Scale" Value="1.05" />
            <Setter Property="Background" Value="#3C8CDC" />
        </VisualStateDefinition>
        <VisualStateDefinition Name="Pressed"><Setter Property="RenderTransform.Scale" Value="0.96" /></VisualStateDefinition>
    </Button.VisualStates>
</Button>
```

Le `Setter` est celui des styles ; `Property` est un chemin de cible et `Value` est converti par le type de la cible a l'ajout du setter (float, int, vecteur `x,y` ou nombre unique, couleur, epaisseur) : chemin inconnu, valeur invalide ou cible sans forme XAML (les gradients) sont des diagnostics du loader qui nomment le chemin et la valeur.

`VisualStateDefinition.OverridesLocalValue` (U4, defaut faux) transfere `UIVisualState.OverridesLocalValue` (voir plus haut) : `<VisualStateDefinition Name="Checked" OverridesLocalValue="True">...` fait gagner cet etat sur une valeur locale et une liaison locale, jamais sur une animation. Marche pareil dans `<Element.VisualStates>` et dans `<Style.VisualStates>`.

## Styles et themes

Un style porte des transitions et des etats (`<Style.Transitions>`, `<Style.VisualStates>`, T5 ; ADR-0007 decision 5), avec ou sans setters :

```xaml
<Window.Styles>
    <Style TargetType="Button">
        <Style.Transitions><Transition Property="Opacity" Duration="0.2" Easing="CubicOut" /></Style.Transitions>
        <Style.VisualStates>
            <VisualStateDefinition Name="Hover"><Setter Property="RenderTransform.Scale" Value="1.05" /></VisualStateDefinition>
        </Style.VisualStates>
    </Style>
    <Style TargetType="Button" Name="Fast">
        <Style.Transitions><Transition Property="Opacity" Duration="50ms" /></Style.Transitions>
    </Style>
</Window.Styles>
```

Fusion dans l'element (`Element.ProcessStyles` puis `ApplyBaseSettings`) : styles implicites (ceux du desktop, `MGResources.AddImplicitStyle`, puis les inline du plus englobant au plus proche), puis styles nommes dans l'ordre de `StyleNames`, puis les declarations propres de l'element ; les collections runtime remplacent par chemin (`Transitions`) ou par nom (`VisualStates`), le dernier gagne. Les etats sont transferes avant les transitions (une transition lit la valeur courante en s'attachant). Un style implicite du desktop fusionne ses transitions par chemin et ses etats par nom avec ceux deja enregistres pour le type. Les setters restent appliques par reflexion sur le DTO et `IsStyleable` filtre comme pour eux.

### Refresh a chaud (U9, ADR-0008 decision 9)

`MGElement.RefreshStyles()` (voir `Docs/styling-theme-architecture.md`, « Refresh de styles a chaud ») re-transfere aussi les transitions et etats visuels des styles courants, plus seulement leurs setters. Chaque transition ou etat pose par un style porte une provenance (`UITransition.Provenance` / `UIVisualState.Provenance`, un `UIValueSourceKind?` : `ImplicitStyle`, `ExplicitStyle`, nul pour une declaration de l'element ou ajoutee par code) et une signature deterministe (`Signature` : chemin, duree, delai, identite de l'easing -- son `ToString()`, pas son type CLR, puisque tous les easings nommes partagent la meme classe -- pour une transition ; nom, `OverridesLocalValue` et setters pour un etat), posees par les surcharges `Transition.ToTransition(UIValueSourceKind?)` / `VisualStateDefinition.ToVisualState(UIValueSourceKind?)` (les surcharges sans argument, utilisees pour les declarations de l'element et l'ajout par code, gardent une provenance nulle).

A chaque refresh, pour chaque element style : une transition ou un etat de style absent de l'element est ajoute ; un existant a provenance non nulle dont la signature a change est remplace (une transition en cours s'arrete et garde sa valeur courante, comme `UITransitionCollection.Add` le fait deja pour tout remplacement ; un etat courant remplace restaure puis reapplique aussitot ses nouveaux setters) ; une signature inchangee ne touche a rien ; un existant a provenance non nulle que plus aucun style ne pose est retire (une transition retiree garde sa valeur en cours, un etat courant retire restaure sa base tout de suite). Un chemin ou un nom que l'element declare lui-meme (`<Element.Transitions>`/`<Element.VisualStates>`) ou une entree ajoutee par code (provenance nulle) n'est jamais touche : l'element et l'application l'emportent toujours sur un style, avant et apres refresh. Deux refresh sans changement n'ecrivent rien et ne relancent jamais une transition en cours. `UIStyleRefreshResult` gagne quatre compteurs (`WrittenTransitions`, `ClearedTransitions`, `WrittenVisualStates`, `ClearedVisualStates`) ; `MGElement.RefreshedStyleTransitionPaths`/`RefreshedStyleVisualStateNames` (diagnostic, nuls avant le premier refresh) gardent ce que le dernier refresh a laisse a la charge d'un style. Cout : rien par frame, tout se passe dans `RefreshStyles`.

Decision annexe (U9) : `UIVisualStateCollection.Add` remplacant l'etat courant restaure desormais puis reapplique aussitot les nouveaux setters (avant, seule la restauration avait lieu, les nouvelles valeurs n'apparaissant qu'au prochain changement d'etat) ; ce correctif profite a tout appelant de `Add`, pas seulement au refresh.

Theme : le groupe `MGTheme.Animation` (`MGThemeAnimationSettings` : `Enabled`, `HoverDuration` 120 ms, `PressDuration` 80 ms, `FocusDuration` 120 ms, `HoverEasing` / `PressEasing` / `FocusEasing` `CubicOut`, noms connus de `UIEasing`) est lu par les controles qui y adherent, `MGButton` et `MGToggleButton` (`UIThemeTransitions`) : quand `Enabled` est vrai ils s'attachent une transition `RenderScale` (survol, duree et easing Hover) et une transition `Background.Overlay` (survol et appui, duree et easing Press), une fois par chemin, mises a jour sur place au changement de theme (un run en cours n'est pas remis a zero) et retirees quand le theme les desactive ; une transition que l'application a posee sur l'un de ces chemins, avant ou apres, n'est jamais touchee. `Enabled` est faux dans les themes integres : un bouton non touche ne porte aucun slot d'animation (principe de cout de l'ADR-0006) ; un theme l'active (`<ThemeDefinition.Animation Enabled="True" HoverDuration="0.15" PressEasing="QuadOut" />` ou `theme.Animation.Enabled = true`). Le DTO `ThemeAnimationSettingsDefinition` accepte les memes formats de duree que `Transition`, un easing inconnu ou une duree invalide sont refuses a la construction du theme, une valeur non posee garde celle du theme de base (`BasedOn`). Le groupe est classe `RenderOnly` dans `UIThemeValueInvalidation`. `FocusDuration` / `FocusEasing` sont reserves : aucun controle ne les lit encore.

Cout : `ViewModelBase.NotifyPropertyChanged` partage un `PropertyChangedEventArgs` par nom de propriete, si bien qu'un element abonne (transition, binding) n'alloue rien par notification ; avant T5, chaque `NPC` d'un element ayant un abonne allouait 24 octets.

## MGProgressButton sur le moteur

`MGProgressButton.Duration` (ADR-0007, decision 7, T6) ne cumule plus `FrameElapsed` dans `UpdateSelf` : `SyncDurationAnimation` (appele par les setters de `Duration`, `IsPaused`, `Value`, `Minimum` et `Maximum`) demarre un `UIPropertyAnimation<float>` lineaire sur `ProgressButton.Value`, de la valeur courante a `Maximum`, sur la part restante de `Duration` (`Duration * (Maximum - Value) / (Maximum - Minimum)`), nomme `ProgressButton.Duration` (`MGProgressButton.DurationAnimationName`, visible dans la debug view), `HoldEnd`, annulation `KeepCurrent`. Regles : une pause annule le run en gardant la valeur, une reprise repart de la valeur courante ; `Duration = null`, l'achevement ou une plage vide annulent ; une `Value` ecrite par l'application pendant le run recible depuis cette valeur ; un changement de `Duration` ou de plage recalcule la part restante. Les ecritures du run passent par `ApplyAnimatedValue`, qui ne recible pas ; un changement demande depuis l'ecriture du run (action d'achevement `Pause`, `Reset`, `ResetAndResume`...) est applique dans l'`UpdateSelf` de la meme frame, apres le tick du manager, pour ne pas annuler le run depuis sa propre ecriture. Le run suit l'horloge du manager (pause, `TimeScale`). Rien ne tourne hors de l'arbre : quitter l'arbre (ou fermer la fenetre, ou `Animations.Clear()`) annule le run en gardant la valeur (le run redefinit la restauration forcee comme un maintien, la progression n'est jamais rembobinee) et rejoindre un arbre le relance depuis la valeur courante. Une valeur sous `Minimum` n'est pas bornee par le run, il dure plus longtemps ; une duree nulle termine au premier tick. La cible n'est pas observable : une transition sur `ProgressButton.Value` est refusee (elle concurrencerait le run). `RemainingDuration` rend desormais la part restante (il rendait la part ecoulee). Limite : fermer la fenetre ou detacher le bouton depuis `OnCompleted` (dans l'ecriture finale du run) annule le run avant que le moteur ne l'ait marque termine, il emet alors `Cancelled` puis `Completed` (ordre du moteur, hors programme).

## Migrations

U10 (ADR-0008, decision 10) : ce qui suit desormais l'horloge du desktop (`MGDesktop.Animations.Clock`, pause et `TimeScale` compris), et ce qui garde sa propre horloge, et pourquoi.

Suivent l'horloge du desktop :

- `MGProgressButton.Duration` (T6, voir « MGProgressButton sur le moteur » ci-dessus) : deja migre avant U10.
- `MGTextBlock.TextCharactersPerSecond` (le texte revele, effet machine a ecrire) : `SyncTextProgressAnimation` (appelee par les setters de `Text`, seulement quand le texte change reellement, et de `TextCharactersPerSecond`, plus l'attache a un arbre) demarre un `UIPropertyAnimation<double>` lineaire sur `TextBlock.TextProgress`, de la progression courante a 1.0, sur la part restante (`(1 - progres) * NumCharacters / TextCharactersPerSecond` secondes), nomme `TextBlock.TextReveal` (`MGTextBlock.TextRevealAnimationName`, visible dans la debug view), `HoldEnd`, annulation `KeepCurrent`. `UpdateSelf` ne cumule plus `FrameElapsed` (le bloc qui faisait `TextProgress += FrameElapsed * TextCharactersPerSecond / NumCharacters` a disparu). Regles, symetriques a `MGProgressButton.Duration` : une vitesse nulle ou negative annule le run et remet `TextProgress` a `null` (texte entier affiche, comme avant) ; passer d'une vitesse nulle a une vitesse positive redemarre la progression a 0 ; **changer une vitesse deja active garde la progression courante et ne fait que recalculer la duree restante (pas de saut, pas de redemarrage)** ; **un changement reel de `Text` redemarre la progression a 0 avec la nouvelle longueur (changement de comportement annonce ci-dessous) ; fixer le meme texte ne redemarre rien** (`SetTextCore` ne touche a rien quand la chaine est identique). Les ecritures du run passent par `ApplyAnimatedTextProgress`, qui ne recible pas le run depuis sa propre ecriture (garde `_IsApplyingAnimatedTextProgress`, symetrique a `_IsApplyingAnimatedValue`). Une ecriture directe de `TextProgress` par l'application (fix round, session principale) est une recherche (seek) dans le run plutot qu'une annulation definitive, pour garder le contrat public d'avant U10 : `SyncTextProgressAnimation` est appelee a nouveau depuis la valeur ecrite, et (re)demarre le run sur la part restante quand la valeur est non nulle et strictement inferieure a 1 (`TextProgress = 0` rejoue le texte, une fraction reprend le run a partir de la, en remplacant le run precedent via la regle de conflit du manager `KeepCurrent`, sans saut arriere), ou l'annule sinon (`null`, ou une valeur superieure ou egale a 1). Un changement de vitesse apres la fin de la revelation garde la progression a 1 (aucun redemarrage) ; pour la rejouer, ecrire `TextProgress = 0` ou changer `Text`. Rien ne tourne hors de l'arbre : un texte configure avant d'etre attache n'alloue aucun emplacement d'animation, l'attache demarre le run (meme hook `OnParentChanged` que `MGProgressButton`) ; quitter l'arbre (ou fermer la fenetre, ou `Animations.Clear()`) annule le run en gardant la progression (`OnRestoreBaseValue` vide, comme `MGProgressButton.DurationRun`), rejoindre un arbre le relance depuis la progression courante. La cible n'est pas observable : une transition sur `TextBlock.TextProgress` est refusee (elle concurrencerait le run).
- `MGHighlightBorderBrush` (l'animation de surbrillance dessinee sur une bordure) : `UpdateBaseArgs.AnimationDeltaTime` (`TimeSpan?`, `MGUI.Shared`, defaut `null`) porte le delta de l'horloge d'animation (deja mis a l'echelle par `TimeScale`, zero en pause) ; `MGDesktop.Update` le pose (`Animations.Clock.DeltaTime`) dans le meme `with { }` que `PaintRegistry`, apres `Animations.Update`. `MGHighlightBorderBrush.Update` lit `UA.AnimationDeltaTime ?? UA.FrameElapsed` a la place de `UA.FrameElapsed` seul : un hote qui fournit le delta suit desormais pause et `TimeScale` ; un hote qui construit son propre `UpdateBaseArgs` (le champ reste `null`) garde le comportement d'horloge murale d'avant cette tranche, sans aucun changement de code de son cote. Choix du type nullable (le plan envisageait un `TimeSpan` non nullable) : `null` distingue explicitement « aucune horloge d'animation fournie » de « delta nul reel » (pause), ce qui garde un hote hors `MGDesktop` inchange sans lui imposer de renseigner ce champ.

Gardent leur propre horloge (ADR-0006, decision 7) : `MGTimer` (Flicker sur `Opacity`, compte a rebours, Shake), `MGStopWatch`, le delai de survol des tooltips, le clignotement du caret (`MGTextCaret`), le bouton a repetition (`MGButton.IsRepeatButton`). Aucun de ces chronometres n'a besoin de suivre la pause ou la vitesse des animations visuelles (un tooltip qui apparait, un caret qui clignote ou un input qui se repete restent lies au temps reel, pas au rythme d'une transition ou d'un storyboard) ; les migrer aurait aussi elargi le perimetre de cette tranche sans necessite fonctionnelle.

Changements de comportement a annoncer : le texte revele et la surbrillance suivent desormais la pause et le `TimeScale` de `MGDesktop.Animations.Clock` (consequence deja actee par ADR-0008) ; un changement de `Text` en cours de reveal redemarre la progression a 0 avec la nouvelle longueur, la ou l'ancienne implementation ne remettait `TextProgress` a zero que dans le constructeur ou au changement de `TextCharactersPerSecond`.

## API fluente

`UIAnimateExtensions.Animate` (ADR-0007, decision 9, T7) est du sucre sur `UIPropertyAnimation<T>` et `UISequenceAnimation`, sans concept nouveau :

```csharp
element.Animate("Opacity", 0f, 1f, 0.3).Ease(UIEasing.CubicOut).Named("fade").Play();
element.Animate("RenderTransform.Scale", new Vector2(1.2f), 0.25).Ease("BackOut").AutoReverse().Repeat(3).Fill(UIAnimationFillBehavior.RestoreBaseValue).Play();
element.Animate("Opacity", 0f, 1f, 0.2).Then("RenderTransform.Rotation", 0f, 90f, 0.3).Wait(0.1).Then(popKeyFrames).Play();
```

`Animate(chemin, [de,] vers, secondes | TimeSpan)` resout la cible tout de suite (chemin inconnu ou type faux echouent la ou la chaine est ecrite) ; `Ease` (fonction ou nom), `Interpolate`, `Delay`, `Repeat`, `RepeatForever`, `AutoReverse`, `Fill`, `OnCancel`, `Named`, `Configure` posent les proprietes de l'etape courante (`UIAnimationBuilder<T>.Animation`) ; `Then` ajoute une etape (ou une animation deja construite, un keyframe par exemple), `Wait` une pause (`UIDelayAnimation`) ; `Build` rend l'animation (l'etape seule, ou une `UISequenceAnimation` nommee d'apres la premiere etape nommee, construite une fois par chaine), `Play` la demarre sur l'element et la rend.

## Diagnostics

`UIToolingService.CaptureElementDebugView(element)` liste les animations actives et retenues de l'element et ses transitions (chemin, etat, progression, nom) et donne l'etat visuel nomme courant (`VisualStateName`, T8) ; `RenderElementDebugView` les rend sous `animations:` et `transitions:`, et `named=` sur la ligne `visual-state:`. `UIElementDebugView.ApplicablePaths` (U2, ADR-0008 decision 2) liste, triee, les chemins d'animation que cet element accepte (`UIAnimationTargets.Paths` filtre par `IsApplicable`) : calcule uniquement a la capture, aucun cout par frame, un editeur peut donc proposer a l'utilisateur les seuls chemins valides pour l'element selectionne. `UIPerformanceProbe` expose la phase `Animations` du desktop.

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
- Une ecriture locale d'un pilote pendant sa transition, ou une sortie d'etat nomme, est vue des le prochain tick du run (U3, `TryGetValueBelowAnimation`), plus seulement a sa fin (limite V1 levee). La limite reste entiere pour une animation explicite (`UIPropertyAnimation<T>` demarree directement, hors transition) : elle tient son objectif jusqu'a la fin du run par conception (ADR-0006, `ReadCurrentValue` part toujours de la valeur animee courante), donc une ecriture locale sous une animation explicite en cours n'est vue qu'a la fin de ce run.
- `Background` n'interpole que les brushes unies ; `Background.Gradient` et `Background.DiagonalGradient` interpolent deux gradients du meme type (couleur par couleur, le coin du gradient diagonal bascule a mi-parcours) ; textures et nine-slices ne sont pas interpolables ; un slot d'etat null refuse le demarrage.
- Le fondu des overlays Hover / Pressed (`VisualStateFillBrush.OverlayOpacity`, ADR-0007 decision 4) est un fondu a l'entree seulement : l'overlay peint est celui de l'etat courant, donc a la sortie de l'etat il disparait avec lui pendant que la transition ramene l'opacite a 0. Seuls les sites de `MGElement` et `MGBorder` appliquent `OverlayOpacity` ; `MGProgressBar`, `MGScrollViewer`, `MGWindow` (barre de titre), `MGUniformGrid`, `MGGridSplitter` et `MGSlider` lisent encore l'overlay directement (opacite 1). Un remplacement entier du conteneur de fond (changement de theme, ecriture `Background` d'un style ou du code) repart avec `OverlayOpacity = 1` : un fondu en cours a ce moment saute.
- `Thickness` est en entiers : une marge animee avance par pixels entiers.
- Les abonnements d'une transition ne sont liberes qu'a son retrait, pas au detachement de l'element (l'element possede la transition, aucune fuite au-dela de sa vie).
- Depuis U9, le refresh de styles a chaud (`MGElement.RefreshStyles`, `ElementStyleRefresher`) re-transfere aussi les transitions et les etats d'un style (voir « Refresh a chaud » ci-dessus) ; les transitions de theme (`UIThemeTransitions`) et tout ce que l'element ou le code declare directement restent hors de son perimetre par construction (provenance nulle). Un `Setter` d'etat ne peut declarer en XAML que les cibles float, int, vecteur, couleur et epaisseur.

## Reste a faire

V1 (`Docs/Tasks/animation-tasks.md`, ADR-0006) et V2 (`Docs/Tasks/animation-v2-tasks.md`, ADR-0007 : composition, keyframes, fondu des overlays, etats visuels nommes, styles et theme, `MGProgressButton` sur le moteur, API fluente, sample `SCN-ANIM-002`) sont livrees. V3 (`Docs/Tasks/animation-v3-tasks.md`, ADR-0008) : easings de Bezier en texte, type d'element requis par cible, valeur sous l'animation pour les transitions sur pilote, etat nomme qui peut l'emporter sur une valeur locale, slot `Checked` du toggle, clip de keyframes multi-pistes, `Seek` pour les instances de preview et serialisation d'un storyboard, migration de `MGTextBlock.TextCharactersPerSecond` et de `MGHighlightBorderBrush` sur l'horloge du desktop, refresh a chaud des transitions et etats des styles (livres, U1 a U10) ; `MGTimer`, `MGStopWatch`, le delai de tooltip, le caret et le bouton a repetition gardent volontairement leur propre horloge (decision U10) ; reste U11 (colonne V3 du sample, completion de l'ADR-0008) ; l'editeur de timeline vit dans le moteur de jeu de l'auteur.
