# Taches systeme d'animation (V5) : backlog des capacites restantes

## Objectif

Recenser, apres la livraison des programmes V1 a V4 (ADR-0006 a ADR-0009, `Docs/animation-architecture.md`), ce qui peut encore etre ajoute au systeme d'animation : les reliquats explicitement differes par les ADR, les points ouverts laisses a l'auteur, et les capacites qu'un moteur de jeu attend et que MGUI n'a pas. Ce fichier est un backlog candidat : aucune tache n'est approuvee ; l'auteur choisit le sous-ensemble, puis une discovery en lecture seule et un plan (ADR-0010) precedent tout code, selon la procedure des programmes precedents.

Contraintes non negociables (inchangees) : pas de dependency property system ; store ADR-0005 pour les pilotes ; zero cout pour un element qui n'anime rien, zero allocation par tick pour un run ; tokens interdits de `RenderingBoundaryArchitectureTests` ; renderer neutre ; brushes gelables (ADR-0009) ; une ADR par decision ; aucun renommage d'API publique ; les tests existants restent verts sans modification sauf ajout ou re-ecriture explicitement listee.

## Historique du fichier

- 15 septembre 2026 : creation a partir de l'evaluation demandee par l'auteur (« est-ce qu'il y a d'autres fonctionnalites a rajouter pour les animations ? »), faits verifies a HEAD `d64d28d` (lecture seule). Aucune tache choisie.

## Etat des lieux (HEAD `d64d28d`, verifie)

- Cibles enregistrees (`UIAnimationTargets.Paths`) : `Opacity`, `RenderTransform.Translation` / `.Scale` / `.Rotation` / `.Origin`, `RenderScale`, `Margin`, `Padding`, `MinHeight`, `PreferredWidth`, `PreferredHeight`, `Background` et ses slots `Selected` / `Disabled` / `Focused`, `Background.Overlay`, `Background.Gradient`, `Background.DiagonalGradient`, `Foreground`, `TextForeground`, `BorderBrush`, `BorderBrush.Highlight.Progress`, `ProgressButton.Value`, `TextBlock.TextProgress`. Absentes : `BorderThickness`, `CornerRadius`, `Width` / `Height` / `MaxWidth` / `MaxHeight`, `ScrollViewer.VerticalOffset` / `HorizontalOffset`, `Slider.Value`, `ProgressBar.Value`, rectangle source ou index de cadre d'une brush texturee.
- Horloge : une seule `UIAnimationClock` par desktop (`TimeScale`, `IsPaused`) ; aucune vitesse par animation ni par sous-arbre ; aucun mode « mouvement reduit ».
- Interpolation : `IUIInterpolator<T>.Lerp(from, to, amount)` sans etat ; un retarget en cours de run repart de la valeur courante sans conserver la vitesse (pas de ressort).
- Fenetres et visibilite : `MGDesktop.NotifyWindowClosed` annule les animations de la fenetre (`CancelOwnedByWindow`) ; une fenetre re-affichee repart sans animation ; `MGElement.Visibility` bascule immediatement ; aucune animation d'entree ou de sortie, aucun retard de retrait.
- Theme : le groupe `MGTheme.Animation` n'est lu que par `MGButton` et `MGToggleButton` (`UIThemeTransitions`) ; `FocusDuration` / `FocusEasing` sont reserves.
- XAML : transitions, `RenderTransform`, etats visuels nommes et styles ; aucun declencheur (`EventTrigger`, `BeginStoryboard`) ni storyboard declaratif ; le DTO `UIAnimationNodeDto` du serialiseur decrit deja un arbre complet.
- Brushes : `MGTextureFillBrush` (`Source`, `Stretch`, `Color`, `Tile`) est gelable et animable par propriete depuis V4, mais aucune propriete ne decrit un cadre de sprite sheet ni un rectangle source.
- Code asynchrone : `UIAnimation` expose `Completed` / `Cancelled`, aucune enveloppe `Task` (`PlayAsync`) ; aucun marqueur de temps (rappel a un offset) dans les pistes de keyframes ni dans le clip.
- Layout : aucune animation d'un changement de bounds (un element qui change de place saute a sa nouvelle position).

## Reliquats des programmes precedents

Differes explicitement (ADR-0008, ADR-0009) : remap temporel d'un composite ; presets CSS nommes (`ease`, `ease-in`...) ; effets de `MGTimer` sur le moteur ; applicabilite par predicat ; chemins de propriete generiques dans une brush ; interpolation des textures ; option de cycle partage pour une surbrillance.

Points ouverts a trancher par l'auteur (detail dans les sections « Open points » et « Decisions taken during delivery » des ADR) : precedence `Checked` contre `Disabled` sur `MGToggleButton` ; remplacement gracieux (au lieu d'un refus) entre une animation de couleur sur `BorderBrush` et un run de surbrillance ; `AutoStart` lu au demarrage seulement ; homogeneisation des messages de refus de `UIAnimationSerializer` et traitement des membres JSON inconnus ; identite d'un easing tiers dans la signature de refresh de style ; contribution `Theme` durable apres un swap de conteneur en plein run ; une brush non gelee partagee enracine les elements qui l'observent ; `Brushes` des composites en `IList<T>` (changement d'API a annoncer).

## Questions a l'auteur (a trancher avant tout plan)

1. Quel sous-ensemble des candidats ci-dessous entre dans V5 (recommandation : C1, C2, C3, C4, C10 en premier programme ; C5 a C9 et C11 en second) ?
2. Les reliquats et points ouverts sont-ils repris dans le meme programme ou laisses en l'etat ?
3. Le groupe `MGTheme.Animation` doit-il s'etendre a tous les controles (C1) ou seulement aux fenetres, popups et tooltips ?

## Legende de statut

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Candidats, par ordre de valeur pour un jeu

### ⏳ C1. Animations d'entree et de sortie

But : une fenetre, un popup, un tooltip ou un element rendu visible joue une animation d'entree ; un retrait ou un masquage joue une animation de sortie et n'est applique qu'a sa fin.

Etat actuel : voir « Fenetres et visibilite » et « Theme ».

Travail attendu (esquisse) : `MGWindow.EnterAnimation` / `ExitAnimation` (ou `UIElementAnimationSlot`), retard du retrait dans `NotifyWindowClosed` / `Visibility` jusqu'a `Completed`, annulation propre si l'element est retire pendant la sortie ; extension du groupe `MGTheme.Animation` (`OpenDuration`, `CloseDuration`, easings) lu par `MGWindow`, `MGToolTip`, `MGContextMenu`, `MGComboBox` (liste deroulante) ; XAML `<Window.EnterAnimation>` reutilisant `UIAnimationNodeDto`. Risques : ordre des evenements de fermeture (`Cancelled` puis `Completed` deja note en V2), focus pendant une sortie.

### ⏳ C2. Transition de layout (FLIP)

But : quand le layout deplace ou redimensionne un element (insertion dans un `StackPanel`, tri d'une liste, ouverture d'un `Expander`), l'element glisse de ses anciennes bounds vers les nouvelles par `RenderTransform`, sans toucher au layout.

Etat actuel : voir « Layout ».

Travail attendu (esquisse) : opt-in par element (`LayoutTransition` : duree, easing) ; capture des bounds avant arrange, difference apres arrange, run sur `RenderTransform.Translation` (et `Scale` pour la taille) de l'ancienne position vers zero ; aucune capture ni allocation pour un element qui n'opte pas ; interaction avec le hit-test (deja inverse par le transform). Risques : cout par arrange pour les elements optes, elements virtualises.

### ⏳ C3. Defilement fluide

But : `ScrollViewer.VerticalOffset` / `HorizontalOffset` animables et un `ScrollTo(offset, duree, easing)`.

Etat actuel : `MGScrollViewer.VerticalOffset` (`MGScrollViewer.cs:269`) et `VerticalOffsetChanged` existent ; aucune cible.

Travail attendu (esquisse) : cibles simples observables `ScrollViewer.VerticalOffset` / `.HorizontalOffset` (`RequiredOwnerType = MGScrollViewer`), `ScrollTo` construit un `UIPropertyAnimation<float>` `KeepCurrent` ; une transition sur l'offset lisse la molette ; bornage par `MaxVerticalOffset` a chaque tick. Risques : un contenu qui change de taille pendant le run.

### ⏳ C4. Brush texturee animee (sprite sheet)

But : animer par le moteur le cadre affiche par une `MGTextureFillBrush` (index de cadre ou rectangle source), serialisable dans un clip.

Etat actuel : voir « Brushes » ; les brushes sont gelables et clonees a l'animation (ADR-0009).

Travail attendu (esquisse) : propriete `SourceRectangle` (ou `FrameIndex` + grille) sur `MGTextureFillBrush`, cible `Background.Texture.Frame` (int, `IUIBrushAnimationTarget`, clone a l'animation), interpolateur `int` a paliers, exemple de sprite sheet dans le sample. Risques : atlas et `Tile`, cout du clone pour une texture partagee (la `MGTextureData` reste partagee par reference).

### ⏳ C5. Interpolation a ressort

But : un `UISpringAnimation<T>` (raideur, amortissement) qui conserve la vitesse quand une transition se recible en cours de run.

Etat actuel : voir « Interpolation ».

Travail attendu (esquisse) : animation a etat (position, vitesse) integree par pas fixe dans `ApplyProgress`, sans allocation ; `UITransition<T>.Spring(...)` en alternative a `Duration` / `Easing` ; fin quand position et vitesse sont sous un seuil. Risques : `Progress` n'a plus de sens strict (duree ouverte), `Seek` d'une preview (integration deterministe depuis zero).

### ⏳ C6. Horloge par sous-arbre et mouvement reduit

But : mettre en pause ou ralentir les animations d'une fenetre ou d'un sous-arbre sans toucher aux autres ; un interrupteur global qui termine les transitions instantanement.

Etat actuel : voir « Horloge ».

Travail attendu (esquisse) : `UIAnimation.SpeedRatio` (remap par animation, reliquat ADR-0008) ; `MGWindow.AnimationTimeScale` / `IsAnimationPaused` appliques par le manager a l'`Owner` de chaque animation ; `UIAnimationClock.ReducedMotion` qui force les durees a zero pour les transitions et les animations marquees non essentielles. Risques : semantique pour un composite dont les enfants sont sur plusieurs fenetres.

### ⏳ C7. Declencheurs XAML

But : `<Element.Triggers><EventTrigger Event="Click"><BeginStoryboard>...` et un storyboard declaratif, comme NoesisGUI.

Etat actuel : voir « XAML » ; `UIAnimationNodeDto` decrit deja un arbre complet.

Travail attendu (esquisse) : DTO `Storyboard` XAML derive de `UIAnimationNodeDto`, `EventTrigger` (`Loaded`, `Click`, `MouseEnter`...) et `PropertyTrigger` sur les proprietes observables, resolution des `TargetName` par le loader, fusion par les styles comme les transitions. Risques : surface du loader strict, evenements par type de controle.

### ⏳ C8. Marqueurs de temps

But : un rappel a un offset donne d'une animation ou d'un clip (son, particule), exporte par l'editeur.

Travail attendu (esquisse) : `UIAnimation.Markers` (offset, nom), evenement `MarkerReached`, `markers` dans `UIKeyFrameClipDto` et `UIAnimationNodeDto`, `Seek` ne les declenche jamais (contrat preview). Risques : un marqueur saute par un grand delta (declencher tous les marqueurs franchis, dans l'ordre).

### ⏳ C9. Animation le long d'un chemin

But : deplacer un element sur une polyline ou une courbe de Bezier (`RenderTransform.Translation`), avec orientation optionnelle.

Travail attendu (esquisse) : `UIPathAnimation` (points, courbes, `Progress` -> point par longueur d'arc, reutilise `GraphBezierGeometry`), serialisable. Risques : parametrage par longueur d'arc sans allocation par tick (table pre-calculee au demarrage).

### ⏳ C10. `PlayAsync`

But : `await element.Animate(...).PlayAsync()` et `await storyboard.PlayAsync(element)` pour enchainer des sequences depuis du code asynchrone, avec annulation quand l'element quitte l'arbre.

Etat actuel : voir « Code asynchrone ».

Travail attendu (esquisse) : `TaskCompletionSource` cree a l'appel (une allocation par appel, hors tick), resolue sur `Completed`, annulee sur `Cancelled`, `CancellationToken` optionnel. Risques : aucun ; taille S.

### ⏳ C11. Cibles manquantes

But : `BorderThickness` (pilote), `CornerRadius`, `Width` / `Height` / `MaxWidth` / `MaxHeight`, `Slider.Value`, `ProgressBar.Value`, `Foreground` des controles hors `MGTextBlock`.

Travail attendu (esquisse) : une cible par propriete selon la famille existante (pilote store ou simple observable), interpolateur `CornerRadius`, tests d'applicabilite. Risques : cout layout des cibles de taille (documente comme pour `PreferredWidth`).

## Consignes de travail pour l'agent IA

- Ne rien implementer depuis ce fichier sans le plan approuve : les candidats retenus font l'objet d'une discovery en lecture seule (faits a HEAD, sites et tests concernes), d'un ADR-0010 Proposed et d'un plan par tranches (une tranche = un commit, statut dans le plan dans le meme commit, verifier frais par tranche a risque).
- `Docs/animation-architecture.md` decrit l'etat final : chaque tranche y ajoute ce qu'elle livre, sans historique ; le sample `AnimationDemo` gagne une section par capacite, avec explication, jamais une colonne de version.
- Ne jamais lancer `MGUI.Samples` depuis un agent ; ne jamais laisser un build `-t:Compile` comme dernier build du sample (la DLL perdrait ses ressources XAML embarquees).
- Docs en francais sans accents ; code, commits et ADR en anglais.

## Validation minimale

- `dotnet build MGUI.Core/MGUI.Core.csproj` ; `dotnet test MGUI.Tests/MGUI.Tests.csproj` (0 echec ; 2337 tests a `d64d28d`) ; `dotnet build MGUI.Samples/MGUI.Samples.csproj` (build complet).
- Scenario `SCN-ANIM-001` (`Docs/scenario-validation-index.md`) etendu d'une section par capacite livree.

## Points ouverts

(aucun tant que l'auteur n'a pas choisi le perimetre)
