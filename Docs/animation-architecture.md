# Architecture du systeme d'animation

Etat decrit : le code tel qu'il est (ADR-0006 a ADR-0009).

## Objectif

Decrire le moteur d'animation de MGUI : le transform de rendu par element, l'horloge et le manager par desktop, les animations et leurs cibles, les brushes animables, les transitions, les etats visuels nommes, la composition, les keyframes, la preview et le seek, la serialisation, la declaration XAML, les styles et le theme, le cout et les limites. Les decisions qui ont mene a cet etat sont dans `Docs/decisions/0006-animation-system.md` a `0009-animation-v4-freezable-brushes.md`.

## Portee

Couvre `MGUI.Core/UI/Animation/*` (namespace `MGUI.Core.UI.Animation`), `MGUI.Core/UI/Brushes/*` pour ce qui touche a l'animation et au gel, la partie transform, animation et surbrillance de `MGUI.Core/UI/MGElement.cs` et `MGUI.Core/UI/MGDesktop.cs`, les DTO `MGUI.Core/UI/XAML/Animation.cs`, le sample `MGUI.Samples/Features/AnimationDemo.xaml` (scenario `SCN-ANIM-001`, `Docs/scenario-validation-index.md` : une page par capacite avec explications, sur le modele des pages de controles).

Hors portee : les chronometres qui gardent leur propre temps (voir « Chronometres hors moteur ») ; l'editeur de timeline, le scrubber et l'edition de courbes, qui vivent dans le moteur de jeu de l'auteur et consomment les API decrites ici (`Seek`, clip, serialisation, chemins applicables).

## Principes directeurs

- Le moteur ne depend ni de `GameTime` ni du renderer : il consomme `UpdateBaseArgs.FrameElapsed` et ecrit des proprietes d'elements.
- Un transform de rendu ne participe jamais au layout (`Docs/layout-architecture.md`, principe 1) ; il est applique au draw et inverse par le hit-test.
- Les proprietes pilotes du store (ADR-0005) sont animees par leur setter tague avec la source `Animation` (100) et restaurees par retrait de cette contribution ; les autres proprietes sont ecrites directement et le moteur garde leur valeur de base.
- Un element qui n'anime rien ne paie rien : deux references nulles (`_renderTransform`, `_animationSlot`), aucun abonnement, aucun travail par frame.
- Une animation en cours n'alloue rien par frame, y compris les couleurs et gradients de fond et de bordure (la valeur animee est un clone mute en place, ADR-0009).
- Une brush est un objet de donnees sans horloge ; l'animation vit dans le moteur.
- Semantique NoesisGUI / WPF pour le transform (origine relative avec (0, 0) par defaut, rotation en degres horaires) et pour les brushes (gelables, clone a l'animation).

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

`UIRenderTransform` (`MGUI.Core/UI/Animation/UIRenderTransform.cs`) : `Translation` (pixels de layout non scales), `Scale` (defaut `Vector2.One`), `Rotation` (degres, horaire a l'ecran), `Origin` (point relatif dans [0,1]², defaut (0, 0) = coin haut-gauche ; (0.5, 0.5) pour le centre), `IsIdentity`, `Reset`, `ToMatrix(bounds)`, `CreateMatrix`, `CreateCenteredScale`. Chaque setter leve `PropertyChanged` avec des `PropertyChangedEventArgs` caches (aucune allocation par notification).

`MGElement.RenderTransform` alloue l'instance a la premiere lecture. `RenderScale` (echelle par etat Pressed/Hovered) reste un sucre compose dans la meme matrice, toujours autour du centre de l'element quelle que soit `Origin`.

Composition au draw (`MGElement.Draw`) : matrice locale `L = E * T(-o) * S * R * T(o) * T(translation)` en espace ecran non scale (`E` = echelle d'etat autour du centre, `o` = origine relative resolue sur les bounds), poussee par `SetTransformTemporary(L * CurrentSettings.Transform)` uniquement si `L` n'est pas l'identite : un element sans transform ne coupe jamais le batch ; un element transforme coute une coupure a l'entree et une a la sortie. Les `TargetBounds` du clip sont la boite englobante des quatre coins transformes (`RectangleUtils.CreateTransformedBoundsF`), valable sous rotation.

Input : `ComputeTopmostHoveredElement` transforme la position de la souris par l'inverse de la matrice de l'element pour lui-meme et son sous-arbre ; `IMouseViewport.IsInside` applique la chaine inverse (ancetres puis element, `ToLocalUnscaledPoint`) au point non scale, l'occlusion de fenetres restant testee sur le point d'origine. Le tout est gate par `MGDesktop.ActiveRenderTransformCount` (nombre d'elements dont un transform, une `RenderScale` ou un override d'echelle d'etat est actif) : sans transform actif, aucun cout par evenement souris. L'early-out du parcours de survol sur les bounds du parent n'est pris que si aucun transform n'est actif, un enfant transforme pouvant en sortir. Tout changement de transform pose `MGWindow.InvalidatePressedAndHoveredElements` pour recalculer le survol sous une souris immobile.

Une `MGWindow` ignore son `RenderTransform` (elle garde `MGWindow.Scale`).

## Horloge et manager

`UIAnimationClock` (`MGDesktop.Animations.Clock`) : `Time`, `DeltaTime` (delta deja mis a l'echelle, zero en pause), `TimeScale` (>= 0), `IsPaused` ; `Advance(frameElapsed)` borne les deltas negatifs a zero et ne borne pas les grands deltas (une longue pause termine les animations a la frame suivante). Un hote qui a besoin du delta anime pour son propre code de peinture lit `Desktop.Animations.Clock.DeltaTime`.

`UIAnimationManager` (`MGDesktop.Animations`) : liste des animations actives (`ActiveCount`, `ActiveAnimations`), `Update` par index avec balayage differe des animations terminees pendant un tick (le nombre d'animations est fige au debut du tick : une animation demarree pendant un tick avance a partir de la frame suivante), `PauseAll` / `ResumeAll` / `CancelAll`, et la regle de conflit : une seule animation active par (element, chemin de propriete) ; l'animation qui demarre annule la precedente en `KeepCurrent` et herite de sa valeur de base, si bien qu'un survol quitte avant la fin repart de la valeur animee courante et restaure la vraie base. Relancer l'animation deja active sur le chemin ne leve pas `Cancelled` ; relancer la meme instance sur un autre element quitte proprement son ancien element.

Appartenance (`UIAnimationCollection`, `element.Animations`) : `Start`, `Count`, `IsAnimating(path)`, `Active`, `Held`, `CancelAll` (selon chaque `CancelBehavior`), `Clear` (restauration forcee et liberation des contributions retenues). Un element qui quitte l'arbre (`OnParentChanged` vers null) fait `Clear` ; la fermeture d'une fenetre (`MGDesktop.NotifyWindowClosed`, donc `TryCloseWindow` et le retrait d'une modale) annule les animations des elements affiches par cette fenetre et par ses fenetres imbriquees, tooltips et popups (chaine `ParentWindow`) ; une fenetre re-affichee repart sans animation. `RemoveNestedWindow` appele directement (liste deroulante, popups) et un `Desktop.Windows.Remove` applicatif ne notifient pas.

## Animations

`UIAnimation` : `Duration`, `Delay`, `RepeatCount` / `RepeatForever`, `AutoReverse` (0 -> 1 -> 0, l'animation finit sur sa valeur de depart), `FillBehavior` (`HoldEnd` par defaut, `RestoreBaseValue`), `CancelBehavior` (`RestoreBaseValue` par defaut, `KeepCurrent`), `Name`, `State` (`Stopped`, `Delayed`, `Running`, `Paused`, `Completed`, `Cancelled`), `Progress`, `Iteration`, `IsReversing`, `Elapsed`, `IterationElapsed`, `Owner`, `IsHeld`, `IsPreview` ; `Play`, `Pause`, `Resume`, `Cancel`, `Restart`, `Seek` (preview seulement, voir plus bas) ; evenements `Started`, `Updated`, `Repeated`, `Reversed`, `Completed`, `Cancelled` (sur `EventArgs.Empty`). `Play` sans proprietaire leve une erreur : la premiere execution passe par `element.Animations.Start`. `PresetOwner(element)` (interne, refuse hors `Stopped`) pose `Owner` avant le demarrage : c'est ainsi qu'un enfant de composite ou un noeud deserialise est lie a un autre element que la racine.

`UIAnimation<T>` : `From` optionnel (`HasFrom` ; sinon la valeur lue au demarrage, donc la valeur animee courante quand l'animation en remplace une autre), `To`, `Easing` (defaut lineaire), `Interpolator` (defaut : registre `UIInterpolators`), `StartValue`, `BaseValue`, `CurrentValue`.

`UIPropertyAnimation<T>` : `Property` (chemin resolu dans `UIAnimationTargets` au demarrage, avec la liste des chemins connus en cas d'erreur) ou `Target` explicite.

Interpolation (`Interpolation/`) : `IUIInterpolator<T>`, registre `UIInterpolators` (float, double, int, `int?` a bascule a mi-parcours, Vector2/3/4, Color, Rectangle, `MonoGame.Extended.Thickness` arrondi away-from-zero, `UIGradientColors`, `UIDiagonalGradientColors` ; `Register<T>` pour un type applicatif). Seul `Color` borne le progres dans [0, 1] : les autres laissent passer le depassement des easings Back et Elastic.

## Easings

`IUIEasingFunction.Ease(float)` (sans etat, sans allocation) est resolu par nom via `UIEasing.TryGet`, utilise par les transitions XAML, le groupe d'animation du theme, le JSON de keyframes et l'API fluente.

- Registre : dix-neuf fonctions integrees (Linear, Quad/Cubic/Sine/Back/Bounce/Elastic In/Out/InOut), insensibles a la casse ; `UIEasing.Register(nom, fonction)` ajoute ou remplace une fonction applicative ; `UIEasing.Names` enumere toutes les cles courantes (les dix-neuf built-ins en sont un sous-ensemble garanti).
- Bezier cubique (`UICubicBezierEasing`) : quatre flottants `X1`, `Y1`, `X2`, `Y2` (semantique CSS `cubic-bezier`) ; `X1` et `X2` sont bornes a [0, 1] a la construction (`ArgumentOutOfRangeException` sinon, comme toute valeur non finie) ; `Y1` / `Y2` sont libres, un depassement de [0, 1] est autorise (comme `BackOut`). `Ease` resout x(u) = t par Newton-Raphson (jusqu'a 8 iterations, tolerance 1e-6) avec repli par bissection (jusqu'a 32 iterations) quand la derivee est proche de zero, puis rend y(u) ; aucune allocation.
- Syntaxe textuelle acceptee par `UICubicBezierEasing.TryParse` (et par `UIEasing.TryGet` en repli) : la forme canonique `cubic-bezier(x1,y1,x2,y2)` (prefixe insensible a la casse) et la forme courte `bezier:x1,y1,x2,y2` ; les espaces autour de chaque nombre et a l'interieur des parentheses sont tolerees ; `Parse` leve `FormatException`. `ToString()` rend toujours la forme canonique, sans espace, en culture invariante. Aucun preset CSS nomme (`ease`, `ease-in`...) n'est fourni : l'appelant ecrit le litteral.
- Resolution et cache : `UIEasing.TryGet(nom)` cherche d'abord dans le registre, puis tente `UICubicBezierEasing.TryParse` sur un echec ; un litteral qui se parse est insere dans le registre sous le texte exact (apres `Trim`) que l'appelant a fourni, pas sous sa forme canonique : deux lookups du meme litteral rendent la meme instance, mais `cubic-bezier(0.42,0,1,1)` et `cubic-bezier(0.42, 0, 1, 1)` restent deux entrees de courbes identiques.
- Consequence pour un editeur de courbes en direct : chaque glisser de poignee qui change `x1..y2` produit un nouveau litteral et donc une nouvelle entree de registre jamais retiree (aucune politique d'eviction). Un editeur qui pousse des valeurs en continu doit soit quantifier les points de controle (par exemple a deux decimales) avant de composer le litteral, soit construire un `UICubicBezierEasing` directement et l'assigner a `Easing` (`UITransition<T>.Easing`, `UIAnimation<T>.Easing`) sans passer par la chaine.

## Composition

`UIAnimationGroup` (`MGUI.Core/UI/Animation/Composition/`) est une `UIAnimation` possedee par un element racine (`root.Animations.Start(group)`) qui demarre ses enfants par le manager, chacun sur son propre element (`child.Owner` s'il en a deja un, la racine sinon) : chaque enfant garde sa cle de conflit, apparait dans les diagnostics de son element et suit l'annulation par element. Le groupe n'ecrit aucune propriete : son chemin est synthetique (`UIStoryboard#n`), il n'entre jamais en conflit. L'annuler annule ses enfants actifs selon leur propre `CancelBehavior` ; `RepeatCount` / `RepeatForever` relancent les enfants ; `AutoReverse` est refuse (les enfants ne sont pas rembobines) ; un enfant `RepeatForever` est refuse (repeter le groupe).

- `UIStoryboard` : parallele, tous les enfants demarrent avec lui, sa duree est celle du plus long (delai et repetitions compris) ; initialiseur de collection.
- `UISequenceAnimation` : `Append`, `AppendDelay` ; chaque enfant demarre a l'offset ou le precedent se termine sur la ligne de temps de la sequence (`GetStartOffset`), deterministe meme si un enfant est remplace tot par une animation en conflit.
- `UIDelayAnimation` : une pause sans cible.

Regle de frame : une animation demarree pendant un tick du manager (enfant d'un groupe, run de transition) avance a partir de la frame suivante, si bien qu'un enfant planifie a un offset reste aligne sur la ligne de temps du groupe (la sequence lit la position exacte en ticks, `UIAnimation.IterationElapsed`). Un enfant demarre au `Begin` du groupe (storyboard) est enregistre avant le groupe et avance avant lui : a une frontiere d'iteration il complete d'abord, puis le `Repeated` du groupe le relance ; chaque iteration se termine donc par le `Completed` de l'enfant. Un groupe garde `FillBehavior = HoldEnd` (refus sinon) ; `Animations.Clear()` sur la racine annule les enfants places sur d'autres elements selon leur propre `CancelBehavior`, sans restauration forcee.

## Preview et seek

`UIAnimationPreview.Attach(element, animation)` (`MGUI.Core/UI/Animation/UIAnimationPreview.cs`) begine une instance de preview pour un hote externe (le scrubber d'un editeur de timeline) : l'instance est demarree sans le `UIAnimationManager` (`Manager` reste null, `IsPreview` vrai, `InheritsBaseValue` faux) ; elle n'est jamais enregistree, tickee, balayee ni annulee quand l'element quitte l'arbre ou que sa fenetre se ferme. L'hote possede le cycle de vie de l'element et de l'animation ; la seule facon de terminer une preview est `Cancel()` (alias `UIAnimationPreview.Detach`), qui applique `CancelBehavior` et leve `Cancelled` comme pour une animation vivante. `Attach` refuse une instance qui n'est pas `Stopped` ou deja enregistree, et un chemin deja anime par une animation vivante (`UIAnimationCollection.IsAnimating`) ; un `Attach` de groupe refuse par une validation (`AutoReverse`, `FillBehavior`) laisse le groupe et ses enfants a `Stopped`, non preview, donc reutilisables apres correction.

`UIAnimation.Seek(TimeSpan elapsed)` positionne une preview a un instant, en avant ou en arriere, sans lever d'evenement et sans jamais atteindre `Completed` : une preview qui depasse sa fin reste sur la pose finale (1, ou 0 apres un nombre pair de passes `AutoReverse`) jusqu'a `Cancel()`. `Seek` refuse (`InvalidOperationException`) une instance qui n'est pas une preview active. A l'interieur du delai (`elapsed < Delay`), la pose est la progression 0 (la valeur de depart), pour qu'un scrubber montre la pose initiale ; c'est la seule difference observable avec une instance vivante, qui n'ecrit rien pendant son delai. Les maths de progression et d'iteration sont partagees avec `Advance` (`ComputeProgress`, en ticks).

Un composite positionne chaque enfant a son instant relatif a son offset (0 pour un storyboard, `GetStartOffset` pour une sequence ; un enfant avant son offset est seeke a 0) par le hook `OnSeek` ; les enfants d'un groupe de preview sont attaches comme previews au moment de `Attach`, recursivement, sur `child.Owner ?? racine` (hook `OnPreviewAttached`, execute avant `OnStarting` pour que chaque groupe imbrique ait sa duree avant que son parent ne calcule la sienne), jamais via le manager.

Limite (verifiee seulement a l'attache) : une preview et une animation vivante ne doivent jamais coexister sur le meme (element, chemin), les deux ecrivant la meme cible ; demarrer une animation vivante sur le chemin d'une preview attachee n'est pas garde et les deux se disputent la valeur jusqu'au detachement de la preview.

## Keyframes

`UIKeyFrame<T>(Offset, Value, Easing)` et `UIKeyFrameTrack<T>` (`MGUI.Core/UI/Animation/KeyFrames/`) forment un modele de donnees pur, sans reference a un element : cles triees par offset dans [0,1], derniere cle a 1 (`Validate`), l'easing d'une cle s'applique au segment qui se termine sur elle, une piste sans cle a 0 part de la valeur courante au demarrage. `UIKeyFrameAnimation<T>` (derive de `UIPropertyAnimation<T>`) : `Track`, segment trouve par recherche binaire (`FindSegment`), `From` / `To` pris de la piste, `Easing` global ignore ; delai, repetition, aller-retour, comportements de fin et d'annulation, appartenance et conflits sont ceux du moteur.

Format JSON d'une piste (`UIKeyFrameSerializer`, `Serialize` / `Deserialize<T>` / `ReadValueType`, version 1) :

```json
{ "version": 1, "valueType": "Single", "frames": [ { "offset": 0, "value": "0" }, { "offset": 1, "value": "1", "easing": "CubicOut" } ] }
```

Valeurs en chaines invariantes : `Single` / `Double` / `Int32` en nombre, `Vector2` / `Vector3` / `Vector4` en `x,y[,z[,w]]`, `Color` en `#RRGGBBAA`, `Thickness` en `l,t,r,b` ; type ou version inconnus refuses explicitement, le type du JSON doit correspondre au `T` demande. `UIKeyFrameSerializer.RegisterValueFormat<T>(format, parse)` est le point d'extension partage par les trois serialiseurs pour un type applicatif (registre thread-safe, derniere inscription gagnante) ; un type ni integre ni enregistre leve `NotSupportedException` nommant le type et cette methode. Un nom d'easing de cadre inconnu est tolere (lineaire).

### Clip multi-pistes

`UIKeyFrameClipSerializer` serialise un `UIStoryboard` compose uniquement de `UIKeyFrameAnimation<T>` (une piste par chemin anime) pour un seul element : `UIKeyFrameClipDto { Version, Duration, Tracks[] { Property, ValueType, Frames[] } }`. La duree du clip fait autorite : chaque piste doit la partager exactement (pas de delai, pas de repetition, pas d'aller-retour) ; a la serialisation, la duree du clip est celle du storyboard (`Duration`, deja calculee s'il a ete joue) ou, a defaut, la plus longue piste.

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

`Deserialize` refuse explicitement : version inconnue, chemin inconnu de `UIAnimationTargets` (le message liste les chemins connus, tries), `valueType` ne correspondant pas au type attendu par la cible ou non supporte (ex. `MinHeight`, dont la cible est `int?`), un clip sans piste ou une piste sans cadre, des cadres invalides (`Validate`). `Serialize` refuse un enfant qui n'est pas une animation de keyframes, un enfant retarde, repete ou en aller-retour, une duree differente, une piste vide, un chemin inconnu. Deux pistes sur le meme chemin ne sont pas refusees : la regle de conflit du moteur s'applique, le storyboard demarrant ses enfants dans l'ordre du fichier, la derniere piste l'emporte. Le storyboard rendu n'a pas de proprietaire : `element.Animations.Start(UIKeyFrameClipSerializer.Deserialize(json))`, un clip par element. Le remap temporel d'un composite est hors perimetre.

## Serialisation

`UIAnimationSerializer` serialise un arbre d'animation complet (`UIStoryboard`, `UISequenceAnimation`, `UIDelayAnimation`, `UIPropertyAnimation<T>`, `UIKeyFrameAnimation<T>`, imbriques a volonte) sans aucune reference a un element (precedent `GraphSerializer`). Un clip est le sous-ensemble particulier « storyboard de keyframes sur un seul element ».

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

`Kind` vaut `Storyboard`, `Sequence`, `Delay`, `Property` ou `KeyFrames` ; chaque noeud porte les reglages communs (`duration`, `delay`, `repeatCount`, `repeatForever`, `autoReverse`, `fillBehavior`, `cancelBehavior`, `name`) ; `duration` n'est ecrite et relue que pour `Delay`, `Property` et `KeyFrames` (celle d'un groupe est recalculee a chaque demarrage). `element` (optionnel, sur n'importe quel noeud, racine incluse) nomme l'element sur lequel `PresetOwner` prereglera le noeud ; `Serialize(animation, nameOf)` appelle `nameOf` pour un noeud dont `Owner` est deja pose, `Deserialize(json, resolveElement)` appelle `resolveElement` pour chaque `element` renseigne. `From` / `To` (noeud `Property`) sont les chaines invariantes de `UIKeyFrameSerializer` ; `From` absent signifie `HasFrom` faux. `Easing` est soit le litteral canonique d'un `UICubicBezierEasing`, soit le nom sous lequel `UIEasing.TryGet` resout exactement cette instance ; une fonction applicative jamais enregistree par nom est refusee. Un groupe vide est accepte.

`Serialize` refuse : un noeud actif, un type de noeud non serialisable, un chemin absent ou inconnu, une piste vide, un type de valeur sans format, un easing non nommable, un enfant preregle sur un element sans nom. `Deserialize` refuse : version inconnue, `kind` absent ou inconnu, `path` inconnu (liste les chemins connus), `valueType` incoherent ou non supporte (nomme `RegisterValueFormat`), piste absente, vide ou invalide, valeur de cadre invalide, `element` non resolu, `easing` non resolu (liste `UIEasing.Names`). Chaque message nomme le noeud fautif par son chemin (`root`, `root/0`, `root/0/1`...). Les membres JSON inconnus sont ignores ; un `repeatCount` negatif est refuse par le setter de `UIAnimation` (sans chemin de noeud).

## Cibles

`IUIAnimationTarget<T>` (`Path`, `IsStoreBacked`, `RequiredOwnerType`, `GetValue`, `SetValue(element, value, nom)`, `RestoreBaseValue(element, base)`) et le registre ferme `UIAnimationTargets` (`Register`, `TryGet`, `Resolve`, `GetValueType`, `GetOwnerType`, `IsApplicable`, `Paths`), chemins insensibles a la casse. `UIDelegateAnimationTarget<T>` pour une propriete applicative.

Applicabilite : `RequiredOwnerType` (membre d'interface par defaut, null = n'importe quel `MGElement`) est renseigne uniquement par les cibles dont l'implementation caste reellement vers un type concret ; `UIAnimationTargets.GetOwnerType(path)` lit ce type au moment de l'enregistrement (aucune reflexion par appel) ; `IsApplicable(path, element)` combine chemin connu et type compatible, false sans exception pour un chemin inconnu. `BorderBrush` resout sa bordure par `MGElement.GetBorder()`, une facade que la plupart des elements a bordure redefinissent : son `RequiredOwnerType` reste null.

| Chemin | Type | Element requis | Famille | Restauration |
| --- | --- | --- | --- | --- |
| `Opacity` | float | - | simple | valeur de base gardee par le moteur |
| `RenderTransform.Translation`, `.Scale`, `.Origin` | Vector2 | - | simple | idem |
| `RenderTransform.Rotation` | float (degres) | - | simple | idem |
| `RenderScale` | float | - | override de l'echelle d'etat | efface l'override |
| `Margin`, `Padding` | Thickness | - | pilote (layout, couteux) | retrait de la contribution `Animation` |
| `MinHeight` | int? | - | pilote (layout) | idem |
| `Background`, `Background.Selected`, `.Disabled`, `.Focused` | Color | - | pilote (slot du fond, brush unie ; clone anime par run) | retrait, puis reecriture de la base sous la source du conteneur si le slot n'avait aucune autre contribution |
| `Foreground` | Color | `MGTextBlock` | pilote | idem |
| `TextForeground` | Color | - | pilote (`DefaultTextForeground.Normal`) | idem |
| `BorderBrush` | Color | - (bordure requise, tout element via `GetBorder()`) | pilote (bordure uniforme et unie ; clone anime par run) | idem |
| `BorderBrush.Highlight.Progress` | double | - (bordure effective `MGHighlightBorderBrush` requise, refus explicite sinon) | simple, non observable, clone anime par run | valeur de base gardee ; le run automatique garde la pose courante |
| `Background.Overlay` | float | - | simple (`VisualStateFillBrush.OverlayOpacity`, valeur sous-jacente 1 si survole ou presse, 0 sinon) | valeur de base gardee par le moteur |
| `PreferredWidth`, `PreferredHeight` | int? | - | simple (layout, couteux) | idem |
| `Background.Gradient` | `UIGradientColors` (4 coins) | - | pilote (slot Normal, `MGGradientFillBrush` ; clone anime par run) | retrait, puis base sous la source du conteneur |
| `Background.DiagonalGradient` | `UIDiagonalGradientColors` (2 couleurs + coin) | - | pilote (slot Normal, `MGDiagonalGradientFillBrush` ; idem) | idem |
| `ProgressButton.Value` | float | `MGProgressButton` | simple, non observable (ecrit par `ApplyAnimatedValue`) | valeur de base gardee ; le run de `Duration` garde la valeur courante |
| `TextBlock.TextProgress` | double | `MGTextBlock` | simple, non observable (ecrit par `ApplyAnimatedTextProgress`) | valeur de base gardee ; le run de `TextCharactersPerSecond` garde la progression courante |

Base et valeur animee : pour un pilote, la valeur effective est le gagnant du store, `Animation` (100) etant la plus forte ; a la fin ou a l'annulation avec restauration, la contribution est retiree et la meilleure source suivante reprend (`UIToolingService.TryGetResolvedValueSource` rapporte `Animation` pendant l'animation). `HoldEnd` sur un pilote garde la contribution (`IsHeld`) jusqu'a la prochaine animation du meme chemin ou `Animations.Clear()`, et masque une ecriture locale posee entre-temps ; sur une propriete simple, la valeur finale reste simplement la valeur CLR et une ecriture locale ulterieure l'emporte (asymetrie assumee). Une animation explicite part toujours de la valeur animee courante (`ReadCurrentValue`) et tient son objectif jusqu'a la fin de son run.

## Brushes animables

Modele NoesisGUI / WPF « Freezable » transpose sans dependency property system :

- Contrat : `IFillBrush` et `IBorderBrush` portent `IUIFreezable` (`IsFrozen`, `CanFreeze`, `Freeze()`) et `ValueEquals(other)` comme membres d'interface par defaut (ADR-0009), conserves pour compatibilite binaire ; toutes les brushes du noyau derivent desormais de `UIFreezableBrush` ou implementent `IUIFreezable` elles-memes, mais `ValueEquals` sur `MGHighlightFillBrush` et `MGHighlightBorderBrush` s'appuie toujours sur le defaut d'interface (comparaison par reference) : aucune des deux ne surcharge `ValueEquals`. L'abstraction `UIFreezableBrush` (`ViewModelBase`) fournit le socle qu'une brush concrete adopte : proprietes settables qui verifient le gel (`ThrowIfFrozen`, `InvalidOperationException` sur une brush gelee), comparent, ecrivent et notifient sans allocation ; `Freeze()` gele recursivement les enfants via `OnFreeze`, `CanFreeze` est faux si un enfant ne peut pas l'etre ; `Copy()` rend une copie profonde non gelee (l'equivalent de `Clone` WPF). Les dix brushes de valeur (`MGSolidFillBrush`, `MGGradientFillBrush`, `MGDiagonalGradientFillBrush`, `MGTextureFillBrush`, `MGNineSliceFillBrush`, `MGProgressBarGradientBrush`, `MGUniformBorderBrush`, `MGDockedBorderBrush`, `MGBandedBorderBrush`, `MGTexturedBorderBrush`) sont des classes mutables scellees derivant de `UIFreezableBrush`, avec des proprietes settables notifiantes, `Copy()` non gele et `ValueEquals`/`Equals`/`GetHashCode` par valeur ; `MGDockedBorderBrush.IsSolidColorsOnly` est un drapeau cache recalcule a chaque changement de cote (au lieu d'etre fige au constructeur) pour garder le chemin rapide de dessin. Les six classes composites/surbrillance (`MGCompositedFillBrush`, `MGPaddedFillBrush`, `MGBorderedFillBrush`, `MGCompositedBorderBrush`, `MGHighlightFillBrush`, `MGHighlightBorderBrush`) adoptent le contrat : `Freeze` propage aux enfants (`Brushes`, `Brush`, `BorderBrush`/`FillBrush`, `Underlay`), `CanFreeze` est faux si un enfant refuse, une liste `Brushes` gelee refuse `Add`/`Remove`/etc (`ThrowIfFrozen`). `MGHighlightFillBrush` derive toujours de `XAMLBindableBase` (databinding XAML, incompatible avec l'heritage multiple) et implemente `IUIFreezable` a la main plutot que d'heriter de `UIFreezableBrush` ; `MGHighlightBorderBrush` derive de `UIFreezableBrush` (elle n'etait deja qu'une `ViewModelBase`) mais son `AnimationProgress` reste exempte de `ThrowIfFrozen` (accumulateur ecrit chaque frame par le run hote, voir « Surbrillance » plus bas). Le conteneur `VisualStateFillBrush` n'est pas une brush : il reste copie par element, et sa copie partage un slot gele par reference (`Assert.Same`) et copie un slot non gele.
- Egalite : `UIBrushEquality.ValueEquals` (champ a champ, recursif) sert de base a deux gardes distinctes, qui n'ont pas la meme regle. La garde de re-application d'un defaut de template au changement de theme (`MGControlTemplate.ApplyTemplateValueCore`) compare toujours par valeur via `UIBrushEquality.ForGuards<T>()` : elle decide si l'utilisateur s'est ecarte du defaut, l'identite ne compte pas. Les setters de slot de `VisualStateSetting<T>` comparent par IDENTITE pour un `T` reference implementant `IUIFreezable` via `UIBrushEquality.ForSlots<T>()` : une instance distincte est un changement, meme de meme valeur que celle deja stockee, car l'appelant peut ensuite muter cette instance precise (brush possedee par l'element) et le slot doit deja detenir l'instance exacte pour que la mutation l'atteigne ; reassigner la meme instance ne notifie pas. Toutes les dix brushes de valeur ont leur propre `ValueEquals` (comparaison champ a champ ; `MGProgressBarGradientBrush` inclut l'identite de reference du `MGProgressBar` observe) et leur propre `Equals`/`GetHashCode` (meme comparaison par valeur, jamais utilises comme cle de hachage tant que la brush est mutable) : plusieurs tests pre-existants comparaient deja ces brushes par valeur via `Assert.Equal` (parfois deliberement, cf. `ResolvedBackgroundPilotTests`, programme resolved-value-engine, ADR-0005) ; sans cet override la conversion en classe les aurait fait passer a la comparaison par reference (heritee de `object`) et cassait ces tests. L'identite de reference reste disponible separement (`ReferenceEquals`) partout ou elle est reellement utilisee (les onze sites d'ecriture du store, le dedup par frame de `PaintLifecycle`, inchanges). Parmi les six classes composites/surbrillance, quatre ont aussi un `ValueEquals`/`Equals` par valeur (`MGCompositedFillBrush` et `MGCompositedBorderBrush` : meme nombre d'enfants, chacun egal par valeur au meme rang ; `MGPaddedFillBrush` et `MGBorderedFillBrush` : chaque champ, brush nichee comprise) ; `MGHighlightFillBrush` et `MGHighlightBorderBrush` retombent sur `Equals` par reference (heritee de `object`). Le store compare par reference (`ReferenceEqualityComparer.Instance`).
- Politique de gel : gelees et partagees sans copie, les brushes d'un theme construit (`ThemeDefinitionBuilder.Build` gele en une passe de reflexion a la fin, `MGTheme.FreezeBrushes()` ; une propriete geree par `ThemeManagedGetter` gele la brush recue des l'affectation, une propriete brute d'un groupe de reglages du theme n'est garantie gelee qu'apres ce passage), la palette et les ressources statiques (`MGResources.AddStaticResource`/`SetStaticResource`) ; `MGTheme.Copy()` partage ses brushes gelees au lieu de les recopier ; `ThemeManagedGetter.GetValue` rend l'instance gelee sans cloner (un conteneur `VisualStateFillBrush`, lui, reste clone, ses slots geles partages). Non gelees et possedees par l'element : les brushes inline XAML (`ToFillBrush` / `ToBorderBrush`) et les brushes creees par code ; muter une telle brush (`brush.Color = ...`) repeint l'element au prochain dessin (MGUI ne met jamais un dessin en cache, la mutation en place suffit), qui s'est abonne a son `PropertyChanged` par son conteneur (`VisualStateFillBrush`, une notification distincte de celle d'un remplacement de slot, sans reecriture du store) ou sa bordure (`MGBorder`). Aucun abonnement pour une brush gelee ; un element dont toutes les brushes sont gelees n'a aucun abonnement.
- Clone a l'animation (regle unique, quelle que soit la brush de base) : la valeur animee d'un pilote brush vit dans la contribution `Animation` (100) du store, mais c'est un seul objet cree au demarrage du run (`Copy()` de la brush effective sous l'animation, ou brush neuve du type attendu ; une base d'un autre type est refusee comme avant) et mute par le run a chaque tick, sans ecriture du store ni allocation ; la restauration retire la contribution et la base reapparait intacte (la meme instance, y compris apres un changement de theme en cours de run). Une brush non gelee possedee par l'element n'est jamais mutee en place par une animation : la couche de contributions du store joue le role de la valeur animee d'une dependency property, MGUI ne reproduit donc pas le comportement Noesis « une brush partagee non gelee anime tous ses elements ». Deux elements qui partagent une brush gelee animent chacun leur clone. Les cinq cibles concernees (`Background` et ses variantes, `BorderBrush`, `Background.Gradient`, `Background.DiagonalGradient`) mutent le clone par ses proprietes normales, notifiantes : la notification reste necessaire au retour immediat d'une transition sur un changement de base en cours de run (etat nomme quitte, ecriture locale), le conteneur de fond relayant deja une mutation de slot sous un nom distinct (`SlotBrushMutated`) que le store ignore.
- Surbrillance : `MGHighlightBorderBrush` est une configuration gelable (`Underlay`, `HighlightColor`, `AnimationType` et ses durees, `IsEnabled`, `AutoStart`, `StopOnMouseOver`, `StopOnClick`, `Target` obsolete sans effet) ; `AnimationProgress` est la progression courante dans [0, 1) ecrite par le run (ou par l'application quand aucun run ne tourne), `ActualAnimationProgress` un alias borne ; `CycleDuration` (derivee de `AnimationType`) est publique ; `Update` ne fait que transferer a `Underlay` (elle-meme ne fait pas avancer `AnimationProgress` ni evaluer `StopOnMouseOver`/`StopOnClick`). Hote cote element (`MGElement.SyncBorderHighlightRun`, reevalue chaque frame depuis `Update`) : quand la bordure effective d'un element (sous toute animation) est une surbrillance avec `AutoStart` et `IsEnabled` vrais, l'element demarre (au rattachement, et au changement de bordure ou d'etat detecte par la reevaluation) un run `RepeatForever` lineaire de 0 a 1 sur `BorderBrush.Highlight.Progress`, duree `CycleDuration`, nomme `BorderBrush.Highlight` (visible dans la debug view), et l'annule en gardant la pose au detachement (`OnParentChanged`, car le manager ne coupe un run que si sa FENETRE ferme, jamais au simple detachement d'un conteneur) ou quand la base n'est plus une surbrillance (la contribution `Animation` orpheline est alors liberee pour que la nouvelle base reapparaisse). `StopOnMouseOver` / `StopOnClick` agissent sur le clone de l'element (le clone passe `IsEnabled = false`, le run est annule) chaque frame ou un run est actif, jamais sur la brush de base, et ne se relevent jamais tout seuls ; `MGElement.ResumeBorderHighlight()` relance. `AutoStart = false` laisse la brush immobile a son `AnimationProgress` jusqu'a ce que l'application anime le chemin (`element.Animate("BorderBrush.Highlight.Progress", 0.0, 1.0, secondes).RepeatForever().Play()`, un storyboard, une preview `Seek`). L'hote ne s'installe que sur l'element proprietaire du facade `GetBorder()` (`IsComponent` faux) : la sous-bordure interne d'un controle composite (`MGButton.BorderElement`, etc, ou `MGBorder.GetBorder() => this` la designerait aussi) ne demarre jamais son propre run redondant. Decision documentee (pas de partage de cle de conflit avec l'animation de couleur `BorderBrush` : voir « Limites connues » et `MGElement.HighlightRun`) ; l'hote ne fouille pas les composites (`MGCompositedBorderBrush`, `MGBandedBorderBrush`) ni une surbrillance nichee dans un `MGBorderedFillBrush`. `AutoStart` n'est lu qu'au demarrage du run automatique : le passer a faux pendant un run ne l'annule pas (`IsEnabled = false` ou l'annulation par `Animations` s'en chargent).

## Transitions

`UITransition<T>` (`element.Transitions.Add(...)`, `UITransitionCollection`) interpole automatiquement chaque changement de sa propriete : `Property`, `Duration`, `Delay`, `Easing`, `Interpolator`. La cible doit etre observable (`IUIObservableAnimationTarget<T>` : `Subscribe`, `GetUnderlyingValue`), ce que toutes les cibles framework sont sauf `ProgressButton.Value`, `TextBlock.TextProgress` et `BorderBrush.Highlight.Progress` (elles concurrenceraient leur run) ; `RenderScale` reagit a `VisualStateChanged` (survol entree et sortie) et aux changements de `RenderScale`.

Regles : au changement, le run (`UIPropertyAnimation<T>` enregistre au manager) part de la valeur animee courante ou de la derniere valeur memorisee (`SettledValue`) vers la valeur sous-jacente ; une ecriture pendant le run recible depuis la valeur courante ; une animation explicite sur le meme chemin remplace le run et la transition se tait en suivant les valeurs ; retirer la transition garde la valeur courante ; `FillBehavior` = `RestoreBaseValue` pour une cible store (retour a la valeur locale visee) et `HoldEnd` pour une propriete simple. Le manager tickant avant le calcul des etats visuels dans la meme frame, le run de survol entrant avance d'un dernier pas avant que le run sortant reparte exactement de la.

Sur un pilote (cible `IUIStoreBackedAnimationTarget<T>`), la valeur sous-jacente que le run doit rejoindre est le gagnant du store sous sa propre contribution `Animation` (`TryGetValueBelowAnimation`, secours sur `GetUnderlyingValue` si rien ne reste en dessous) : une ecriture locale ou une sortie d'etat nomme pendant le run est vue des le prochain tick de l'interpolation en cours, et le run recible part de sa valeur animee courante, jamais d'un saut ; les ecritures propres du run sont exclues de cette lecture. Cette lecture n'est faite que pendant le propre run de la transition : quand une animation explicite etrangere occupe le chemin, la transition lit la valeur physique pour suivre cette animation.

## Etats visuels nommes

`UIVisualState` (`element.VisualStates.Add(new UIVisualState(UIVisualStateNames.Hover) { { "RenderTransform.Scale", new Vector2(1.05f) } })`, `UIVisualStateCollection`) : un nom et des setters types par chemin de cible (`UIAnimationTargets`). Un setter est lie a sa cible a la creation (type de valeur verifie, chemin inconnu refuse) ; aucune reflexion par frame. Chaque frame, apres le calcul de `VisualState` dans `MGElement.Update`, la collection resout le nom courant dans l'ordre `Disabled`, `Checked` (element `IUICheckable` : `MGToggleButton`, `MGCheckBox`, `MGRadioButton`), `Selected`, `Pressed`, `Hover` (survole ou presse), `Focused`, `Normal` : le premier etat dont la condition tient et que l'element definit gagne. Au changement, les chemins de l'etat quitte que le suivant ne pose pas sont restaures, puis les setters du suivant sont ecrits : une transition sur un chemin de setter voit une ecriture par chemin et interpole le changement. `element.CurrentVisualStateName` expose le nom courant ; `IsEnabled = false` gele l'etat applique ; `Remove` / `Clear` restaurent l'etat courant ; `Add` remplacant l'etat courant restaure ses anciens setters puis applique aussitot les nouveaux.

Sources : un setter sur un pilote ecrit la contribution `VisualState` (70) du store avec le nom `visualstate:<Nom>` et la retire en quittant ; un setter sur une propriete simple memorise la valeur sous-jacente a l'entree et la reecrit a la sortie. Une transition sur un pilote pose sa contribution `Animation` (100) au-dessus de l'etat.

Base : la base d'un chemin est memorisee a la premiere ecriture d'un etat (une seule fois tant qu'un etat pose ce chemin). Pour une cible pilote, la base est le gagnant du store sous la contribution `Animation` (lecture correcte meme en plein run) ; pour une propriete simple, dont la valeur physique est la valeur animee pendant un run, la base est lue depuis la transition attachee au chemin (`SettledValue`). Un pilote dont le conteneur a ete ecrit en bloc (constructeur, theme, style) n'a aucune contribution pour son sous-slot : en quittant l'etat, la base est remise comme valeur conservee, sans contribution (un refresh du fond par le theme la remplace donc toujours) ; si une transition tient encore le slot, la base est enregistree sous la source du conteneur et le run en cours se recible vers elle des son prochain tick. Un run declenche par un remplacement entier du conteneur (changement de theme) ne laisse sur le sous-slot que sa contribution `Animation` : la base est alors lue depuis `SettledValue`.

Precedence : par defaut un setter d'etat nomme sur un pilote ecrit a 70, donc une valeur locale (90) ou une liaison locale (80) l'emporte (l'etat est enregistre mais dormant) ; les etats sont faits pour des fonds venant du theme, d'un style ou d'un template. `UIVisualState.OverridesLocalValue` (defaut faux) ecrit tous les setters de cet etat (granularite par etat) au palier `UIValuePrecedence.VisualStateOverride` (95, genre `VisualState` inchange) : l'etat l'emporte alors sur une valeur locale et une liaison locale, jamais sur une `Animation` (100), qui rend la couleur de l'etat en se terminant. Le drapeau ne change rien pour un setter sur une propriete simple. Basculer le drapeau sur un etat deja courant n'est applique qu'au prochain changement d'etat. `TryGetResolvedValueSource` rapporte le palier 95.

Slot `Checked` du toggle : `MGToggleButton.CheckedBackgroundBrush` / `CheckedTextForeground` sont un vrai slot porte par `VisualStateBrush<T>.CheckedValue` / `HasCheckedValue` (fill et couleur, code seul, pas de champ theme DTO, pas de slot du store), avec `SelectedValue` en secours quand rien n'est pose ; `MGElement.DrawBackground` passe par un point d'extension protege (`ResolveBackgroundUnderlay`) que `MGToggleButton` redefinit pour lire `CheckedValue` pendant `IsChecked` ; `IsChecked` continue de poser `IsSelected`. `Background.Checked` n'est ni une cible d'animation ni un slot du store : pour animer un fond a la coche, l'etat nomme `Checked` reste la voie. Un toggle a la fois desactive et coche avec un `CheckedBackgroundBrush` dessine la brush cochee (point ouvert, ADR-0008). `MGCheckBox` et `MGRadioButton` dessinent leur coche par des elements enfants. L'echelle d'etat `RenderScale` et les etats nommes coexistent (chemins differents). `Checked` se lit sur `IUICheckable.IsChecked == true` (une case indeterminee n'est pas cochee).

## XAML

DTO `MGUI.Core/UI/XAML/Animation.cs` :

```xaml
<Button Content="Hover me" RenderScale="1.05">
    <Button.RenderTransform>
        <RenderTransform Origin="0.5,0.5" Rotation="0" />
    </Button.RenderTransform>
    <Button.Transitions>
        <Transition Property="RenderScale" Duration="0.1" Easing="CubicOut" />
        <Transition Property="Background" Duration="150ms" Delay="0:0:0.05" Easing="cubic-bezier(0.42,0,1,1)" />
    </Button.Transitions>
</Button>
```

`Transition.Property` doit etre un chemin enregistre, `Duration` / `Delay` acceptent des secondes (`0.15`), des millisecondes (`150ms`) ou un `TimeSpan` (`0:0:0.15`), `Easing` un nom connu ou un litteral de Bezier ; chaque setter valide sa valeur, donc une erreur remonte comme diagnostic du loader strict (`InvalidValueConversion`, `SCN-MARKUP-001`). `RenderTransform` accepte `x,y` ou un nombre unique pour les vecteurs, `Rotation` en degres. Les transitions sont attachees apres les attributs de l'element (elles lisent la valeur courante en s'attachant).

Etats visuels nommes (`VisualStateDefinition`) :

```xaml
<Button Content="Hover me">
    <Button.VisualStates>
        <VisualStateDefinition Name="Hover">
            <Setter Property="RenderTransform.Scale" Value="1.05" />
            <Setter Property="Background" Value="#3C8CDC" />
        </VisualStateDefinition>
        <VisualStateDefinition Name="Checked" OverridesLocalValue="True"><Setter Property="Background" Value="#2E7D32" /></VisualStateDefinition>
    </Button.VisualStates>
</Button>
```

Le `Setter` est celui des styles ; `Property` est un chemin de cible et `Value` est converti par le type de la cible a l'ajout du setter (float, int, vecteur `x,y` ou nombre unique, couleur, epaisseur) : chemin inconnu, valeur invalide ou cible sans forme XAML (les gradients) sont des diagnostics du loader qui nomment le chemin et la valeur. `OverridesLocalValue` (defaut faux) transfere `UIVisualState.OverridesLocalValue`, dans `<Element.VisualStates>` comme dans `<Style.VisualStates>`.

## Styles et themes

Un style porte des transitions et des etats (`<Style.Transitions>`, `<Style.VisualStates>`), avec ou sans setters :

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

### Refresh a chaud

`MGElement.RefreshStyles()` (voir `Docs/styling-theme-architecture.md`, « Refresh de styles a chaud ») re-transfere aussi les transitions et etats visuels des styles courants. Chaque transition ou etat pose par un style porte une provenance (`UITransition.Provenance` / `UIVisualState.Provenance`, `UIValueSourceKind?` : `ImplicitStyle`, `ExplicitStyle`, nul pour une declaration de l'element ou ajoutee par code) et une signature deterministe (`Signature` : chemin, duree, delai et identite de l'easing (son `ToString()`) pour une transition ; nom, `OverridesLocalValue` et setters pour un etat), posees par `Transition.ToTransition(UIValueSourceKind?)` / `VisualStateDefinition.ToVisualState(UIValueSourceKind?)`.

A chaque refresh, pour chaque element style : une transition ou un etat de style absent de l'element est ajoute ; un existant a provenance non nulle dont la signature a change est remplace (une transition en cours s'arrete et garde sa valeur courante ; un etat courant remplace restaure puis reapplique ses nouveaux setters) ; une signature inchangee ne touche a rien ; un existant a provenance non nulle que plus aucun style ne pose est retire (une transition retiree garde sa valeur en cours, un etat courant retire restaure sa base tout de suite). Un chemin ou un nom que l'element declare lui-meme ou une entree ajoutee par code n'est jamais touche. Deux refresh sans changement n'ecrivent rien. `UIStyleRefreshResult` compte `WrittenTransitions`, `ClearedTransitions`, `WrittenVisualStates`, `ClearedVisualStates` ; `MGElement.RefreshedStyleTransitionPaths` / `RefreshedStyleVisualStateNames` (diagnostic, nuls avant le premier refresh) gardent ce que le dernier refresh a laisse a la charge d'un style. Limites : un easing tiers qui ne redefinit pas `ToString` est identifie par son type ; la signature n'encode pas la provenance, une declaration identique promue d'un style implicite a un style nomme garde son ancienne provenance (diagnostic seulement). Cout : rien par frame.

Theme : le groupe `MGTheme.Animation` (`MGThemeAnimationSettings` : `Enabled`, `HoverDuration` 120 ms, `PressDuration` 80 ms, `FocusDuration` 120 ms, `HoverEasing` / `PressEasing` / `FocusEasing` `CubicOut`, noms ou litteraux connus de `UIEasing`) est lu par `MGButton` et `MGToggleButton` (`UIThemeTransitions`) : quand `Enabled` est vrai ils s'attachent une transition `RenderScale` (survol, duree et easing Hover) et une transition `Background.Overlay` (survol et appui, duree et easing Press), une fois par chemin, mises a jour sur place au changement de theme et retirees quand le theme les desactive ; une transition posee par l'application sur l'un de ces chemins n'est jamais touchee ; ces transitions de theme sont hors du refresh de styles (provenance nulle). `Enabled` est faux dans les themes integres : un bouton non touche ne porte aucun slot d'animation ; un theme l'active (`<ThemeDefinition.Animation Enabled="True" HoverDuration="0.15" PressEasing="QuadOut" />` ou `theme.Animation.Enabled = true`). Le DTO `ThemeAnimationSettingsDefinition` accepte les memes formats de duree que `Transition` ; un easing inconnu ou une duree invalide sont refuses a la construction du theme ; une valeur non posee garde celle du theme de base (`BasedOn`). Le groupe est classe `RenderOnly` dans `UIThemeValueInvalidation`. `FocusDuration` / `FocusEasing` sont reserves : aucun controle ne les lit. Les brushes d'un theme sont gelees (voir « Brushes animables »).

## Controles sur le moteur

Ce qui suit l'horloge du desktop (`MGDesktop.Animations.Clock`, pause et `TimeScale` compris) :

- `MGProgressButton.Duration` : `SyncDurationAnimation` (appele par les setters de `Duration`, `IsPaused`, `Value`, `Minimum` et `Maximum`) demarre un `UIPropertyAnimation<float>` lineaire sur `ProgressButton.Value`, de la valeur courante a `Maximum`, sur la part restante de `Duration`, nomme `ProgressButton.Duration`, `HoldEnd`, annulation `KeepCurrent`. Une pause annule le run en gardant la valeur, une reprise repart de la valeur courante ; `Duration = null`, l'achevement ou une plage vide annulent ; une `Value` ecrite par l'application pendant le run recible depuis cette valeur ; un changement de `Duration` ou de plage recalcule la part restante. Les ecritures du run passent par `ApplyAnimatedValue`, qui ne recible pas ; un changement demande depuis l'ecriture du run (action d'achevement `Pause`, `Reset`, `ResetAndResume`...) est applique dans l'`UpdateSelf` de la meme frame, apres le tick du manager. Rien ne tourne hors de l'arbre : quitter l'arbre annule le run en gardant la valeur, rejoindre un arbre le relance. Une valeur sous `Minimum` n'est pas bornee ; une duree nulle termine au premier tick. `RemainingDuration` rend la part restante. Limite : fermer la fenetre ou detacher le bouton depuis `OnCompleted` emet `Cancelled` puis `Completed`.
- `MGTextBlock.TextCharactersPerSecond` (texte revele) : `SyncTextProgressAnimation` (appelee par les setters de `Text`, seulement quand le texte change reellement, de `TextCharactersPerSecond`, de `TextProgress`, et au rattachement) demarre un `UIPropertyAnimation<double>` lineaire sur `TextBlock.TextProgress`, de la progression courante a 1.0, sur la part restante (`(1 - progres) * NumCharacters / TextCharactersPerSecond` secondes), nomme `TextBlock.TextReveal`, `HoldEnd`, annulation `KeepCurrent`. Regles : une vitesse nulle ou negative annule le run et remet `TextProgress` a `null` (texte entier) ; passer d'une vitesse nulle a une vitesse positive redemarre a 0 ; changer une vitesse active garde la progression et recalcule la duree restante ; un changement reel de `Text` redemarre a 0 avec la nouvelle longueur, fixer le meme texte ne redemarre rien ; une ecriture directe de `TextProgress` est un seek du revele (0 rejoue, une fraction reprend de la sans saut, `null` montre le texte entier, 1 le garde complet) ; apres la fin, un changement de vitesse garde la progression a 1. Les ecritures du run passent par `ApplyAnimatedTextProgress`, qui ne recible pas. Rien ne tourne hors de l'arbre : un texte configure avant d'etre attache n'alloue aucun emplacement, l'attache demarre le run ; quitter l'arbre annule le run en gardant la progression, rejoindre un arbre le relance.
- `MGHighlightBorderBrush` : run automatique par element sur `BorderBrush.Highlight.Progress`, voir « Brushes animables ».

Chronometres hors moteur (ADR-0006, decision 7) : `MGTimer` (Flicker sur `Opacity`, compte a rebours, Shake), `MGStopWatch`, le delai de survol des tooltips, le clignotement du caret (`MGTextCaret`), le bouton a repetition (`MGButton.IsRepeatButton`). Aucun n'a besoin de suivre la pause ou la vitesse des animations visuelles : un tooltip qui apparait, un caret qui clignote ou un input qui se repete restent lies au temps reel.

## API fluente

`UIAnimateExtensions.Animate` est du sucre sur `UIPropertyAnimation<T>` et `UISequenceAnimation`, sans concept nouveau :

```csharp
element.Animate("Opacity", 0f, 1f, 0.3).Ease(UIEasing.CubicOut).Named("fade").Play();
element.Animate("RenderTransform.Scale", new Vector2(1.2f), 0.25).Ease("BackOut").AutoReverse().Repeat(3).Fill(UIAnimationFillBehavior.RestoreBaseValue).Play();
element.Animate("Opacity", 0f, 1f, 0.2).Then("RenderTransform.Rotation", 0f, 90f, 0.3).Wait(0.1).Then(popKeyFrames).Play();
```

`Animate(chemin, [de,] vers, secondes | TimeSpan)` resout la cible tout de suite (chemin inconnu ou type faux echouent la ou la chaine est ecrite) ; `Ease` (fonction, nom ou litteral de Bezier), `Interpolate`, `Delay`, `Repeat`, `RepeatForever`, `AutoReverse`, `Fill`, `OnCancel`, `Named`, `Configure` posent les proprietes de l'etape courante (`UIAnimationBuilder<T>.Animation`) ; `Then` ajoute une etape (ou une animation deja construite, un keyframe par exemple), `Wait` une pause (`UIDelayAnimation`) ; `Build` rend l'animation (l'etape seule, ou une `UISequenceAnimation` nommee d'apres la premiere etape nommee, construite une fois par chaine), `Play` la demarre sur l'element et la rend.

## Animations attendables

`UIAnimationCollection.StartAsync(animation, cancellationToken = default)` (ADR-0011, decision 2) rend un `Task<bool>` : `true` quand le run atteint `Completed`, `false` quand il est annule pour n'importe quelle raison (remplacement par la regle de conflit, detachement, fermeture de fenetre, `Cancel()`, jeton) -- jamais d'exception pour une annulation, courante dans une interface (une exception qui remonterait d'un `async void` ferait tomber le jeu). Pour un composite (`UIAnimationGroup`), le resultat est celui du composite lui-meme, quoi qu'il arrive a ses enfants. `UIAnimationBuilder.PlayAsync(cancellationToken = default)` (base non generique, a cote de `Play()` : couvre `UIAnimationBuilder<T>` par heritage et une chaine terminee par `Wait(...)`) et l'extension `UIAnimation.PlayAsync(owner, cancellationToken = default)` (`UIAnimationAsyncExtensions`, pour un storyboard, une sequence ou une instance construite a la main) sont du sucre sur `StartAsync`. Refus synchrones, avant tout abonnement : `ArgumentNullException` (animation ou element nul), `InvalidOperationException` sur une instance de preview (`IsPreview` : une preview se pilote par `Seek`, jamais attendue), sur une instance deja active (`IsActive` : attendre le run en cours ou l'annuler d'abord), et l'exception existante sans desktop. Un jeton deja annule a l'appel rend `false` sans rien demarrer (tache mise en cache). Une `RepeatForever` ne se termine que par annulation.

`Restart()` de la meme instance sur le meme (element, chemin) ne leve pas `Cancelled` (regle de conflit existante) : la tache suit le run redemarre et se resout a sa propre fin, pas au temps de fin d'origine. La relancer sur un autre element ou chemin annule son ancien emplacement et resout la tache `false`.

Jeton : son rappel s'execute sur le thread qui annule et ne touche donc jamais le moteur -- il depose une demande d'annulation dans une file (`ConcurrentQueue`, sans verrou sur le chemin rapide) que `UIAnimationManager.Update` draine tout en haut, avant `Clock.Advance` et donc avant la sortie anticipee sur delta nul (horloge en pause ou `TimeScale` a zero) : l'animation est alors annulee comme par `Cancel()` (son propre `CancelBehavior`) et la tache rend `false`. Une demande dont la tache est deja resolue est ignoree, si bien qu'un jeton perime (annule apres coup, ou apres qu'un nouvel appel `StartAsync` a redemarre la meme instance avec un jeton frais) n'annule jamais un run plus tard : la resolution liberait deja l'enregistrement du jeton (`CancellationTokenRegistration.Unregister()`, non bloquant -- jamais `Dispose`, qui attendrait un rappel deja en cours) avant que ce jeton perime ne puisse etre annule.

Resolution : un seul `TaskCompletionSource<bool>` par appel, cree hors tick et sans `RunContinuationsAsynchronously`, resolu toujours sur le thread de l'update, depuis les evenements `Completed`/`Cancelled` de l'animation ou depuis le drainage de la file. Sans `SynchronizationContext` (boucle de jeu MonoGame DesktopGL), la suite du `await` s'execute donc en ligne, sur ce meme thread, pendant le meme tick que l'evenement qui resout -- exactement comme un autre abonne de `Completed` le ferait, et peut demarrer sans risque une nouvelle animation (celle-ci n'avance qu'a partir de la frame suivante, regle habituelle d'une animation demarree pendant un tick). Avec un `SynchronizationContext` installe, la suite y est postee normalement. Une tache reste en attente tant que son desktop n'est pas mis a jour.

## Diagnostics

`UIToolingService.CaptureElementDebugView(element)` liste les animations actives et retenues de l'element et ses transitions (chemin, etat, progression, nom), donne l'etat visuel nomme courant (`VisualStateName`) et les chemins d'animation que l'element accepte (`ApplicablePaths`, `UIAnimationTargets.Paths` filtre par `IsApplicable`, tries, calcules a la capture seulement) ; `RenderElementDebugView` les rend sous `animations:`, `transitions:` et `named=` sur la ligne `visual-state:`. `UIPerformanceProbe` expose la phase `Animations` du desktop. `TryGetResolvedValueSource` rapporte la source et la precedence d'un pilote (`Animation` pendant un run, `VisualStateOverride` pour un etat prioritaire).

## Cout

- Element sans animation ni transform : deux references nulles, aucun abonnement, aucun travail par frame ni par evenement souris ; aucun abonnement non plus pour un element dont toutes les brushes sont gelees.
- Element transforme : une construction de matrice au draw et deux coupures de batch ; au survol, une inversion de matrice par ancetre transforme.
- Animation active : aucune allocation par tick, y compris `Background`, `BorderBrush` et les gradients (un clone par run, ADR-0009) ; un pilote de layout invalide le layout de sa fenetre a chaque tick.
- Transition : un abonnement `PropertyChanged` (ou deux pour `RenderScale`) par transition attachee, un run alloue par changement.
- Brush non gelee : un abonnement `PropertyChanged` par conteneur ou bordure qui la tient ; aucun pour une brush gelee.
- `ViewModelBase.NotifyPropertyChanged` partage un `PropertyChangedEventArgs` par nom de propriete : aucune allocation par notification.
- `UIAnimationManager.Update` paie un test de file vide par tick pour les demandes d'annulation en attente (ADR-0011) ; une tache attendue (`StartAsync`/`PlayAsync`) n'ajoute aucune allocation par tick, y compris avec un jeton enregistre.

## Limites connues

- Un enfant clippe par son parent (`ClipToBounds`) et transforme est teste dans son clip local : le scissor du parent n'est pas transforme.
- Un element transforme a l'interieur d'un `MGToolTip` a un pivot faux au hit-test (la tooltip est dessinee avec un decalage a la souris sans equivalent cote update).
- `MGWindow.InvalidatePressedAndHoveredElements` n'est jamais remis a false par la fenetre : apres un premier transform, le survol est recalcule a chaque frame de cette fenetre.
- `ActiveRenderTransformCount` n'est pas decremente quand une fenetre se ferme avec un transform actif (cout de parcours seulement).
- Une ecriture locale sous une animation explicite en cours n'est vue qu'a la fin de ce run (une animation explicite tient son objectif par conception) ; une transition, elle, se recible des le prochain tick.
- Un `Foreground` de `MGTextBlock` purement herite (aucune contribution du store) ne se recible pas en vol : la lecture sous l'animation ne reproduit pas le repli d'heritage de `MGTextBlock`.
- `Background` n'interpole que les brushes unies ; `Background.Gradient` et `Background.DiagonalGradient` interpolent deux gradients du meme type (couleur par couleur, le coin du gradient diagonal bascule a mi-parcours) ; textures et nine-slices ne sont pas interpolables ; un slot d'etat null refuse le demarrage.
- Le fondu des overlays Hover / Pressed (`VisualStateFillBrush.OverlayOpacity`) est un fondu a l'entree seulement : l'overlay peint est celui de l'etat courant, donc a la sortie de l'etat il disparait avec lui pendant que la transition ramene l'opacite a 0. Seuls les sites de `MGElement` et `MGBorder` appliquent `OverlayOpacity` ; `MGProgressBar`, `MGScrollViewer`, `MGWindow` (barre de titre), `MGUniformGrid`, `MGGridSplitter` et `MGSlider` lisent encore l'overlay directement (opacite 1). Un remplacement entier du conteneur de fond repart avec `OverlayOpacity = 1`.
- `Thickness` est en entiers : une marge animee avance par pixels entiers.
- Les abonnements d'une transition ne sont liberes qu'a son retrait, pas au detachement de l'element (l'element possede la transition, aucune fuite au-dela de sa vie).
- Un `Setter` d'etat ne peut declarer en XAML que les cibles float, int, vecteur, couleur et epaisseur.
- Une preview montre la pose initiale pendant son delai la ou une instance vivante n'ecrit rien ; une preview et une animation vivante sur le meme chemin ne sont gardees qu'a l'attache.
- Une tache de `StartAsync`/`PlayAsync` reste en attente tant que le desktop qui tient l'animation n'est pas mis a jour (aucun minuteur independant ne la resout).
- L'hote de surbrillance ne fouille pas les bordures composites ni une surbrillance nichee dans un `MGBorderedFillBrush` (elle n'anime plus alors, meme via le chemin d'animation applicatif : les deux exigent que la surbrillance soit directement la valeur effective du pilote `BorderBrush`) ; deux elements qui partagent une brush de surbrillance gelee ont chacun leur cycle. Une animation de couleur explicite sur `BorderBrush` pendant qu'un run de surbrillance est actif est refusee (exception du controle de type existant de `BorderBrushTarget`) plutot que substituee : les deux cibles ecrivent le meme emplacement `Animation` via un clone possede par le run, mais le controle de type de chacune lit la valeur effective COURANTE a son demarrage (pas la valeur sous l'animation), et l'annulation `KeepCurrent` du perdant d'un partage de cle laisserait son clone comme valeur courante, faisant echouer le controle de type du gagnant, dans les deux sens ; voir `MGElement.HighlightRun`.

## Reste a faire

Differe explicitement (ADR-0008 et ADR-0009) : remap temporel d'un composite, presets CSS nommes (`ease`, `ease-in`...), effets de `MGTimer` sur le moteur, applicabilite par predicat, chemins de propriete generiques dans une brush, interpolation des textures, option de cycle partage pour une surbrillance. Points ouverts laisses a l'auteur (ADR-0008, « Open points for the author ») : precedence Checked/Disabled sur le toggle, homogeneisation des messages de refus et traitement des membres JSON inconnus de `UIAnimationSerializer`, identite d'un easing tiers dans la signature de refresh de style. Points ouverts laisses a l'auteur (ADR-0009, « Open points for the author ») : brush non gelee dont l'element observateur reste enracine tant qu'elle vit, `ForSlots<VisualStateFillBrush>` retombe sur le comparateur par defaut, le changement d'API `MGCompositedFillBrush.Brushes`/`MGCompositedBorderBrush.Brushes` (`List<T>` devenu `IList<T>`) a annoncer, refus (non substitution) entre une surbrillance et une animation de couleur explicite sur `BorderBrush`. L'editeur de timeline vit dans le moteur de jeu de l'auteur. L'historique complet du programme d'animation vit dans ADR-0006 a ADR-0009 (`Docs/decisions/`).
