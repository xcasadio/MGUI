# Rapport d'audit Style/Theme pour MGUI

## Resume executif

MGUI possede deja plusieurs briques utiles pour evoluer vers une architecture style/theme propre:

- un theme central via `MGTheme` ;
- un conteneur de ressources via `MGResources` ;
- des styles implicites et nommes cote XAML ;
- un modele `VisualState` ;
- des presenters de contenu reutilisables ;
- des templates de contenu et quelques points d'extension par delegates.

En revanche, l'architecture actuelle reste principalement orientee autour de controles qui initialisent leur apparence a partir du theme, puis conservent des copies locales de ces valeurs. Le systeme de styles est surtout un mecanisme de setters applique au parsing XAML, pas un systeme runtime a precedence complete. Le systeme de ressources est centralise au niveau desktop, sans lookup hierarchique ni ressources dynamiques. Les templates existants ne constituent pas encore un vrai systeme de `ControlTemplate` lookless.

Conclusion:

- l'architecture actuelle est suffisante pour du theming par defaut et des customisations ponctuelles ;
- elle n'est pas encore suffisante pour supporter proprement theme switch runtime, lookless controls, overrides locaux, styles herites et templates de controle generiques ;
- la priorite n'est pas d'ajouter des options de skinning supplementaires, mais de poser un socle clair: precedence des valeurs, ressources hierarchiques, invalidation fine, `ControlTemplate`, projection des visual states.

## Methode

L'audit a ete mene a partir des couches definies dans le cahier d'audit:

1. property system ;
2. resource system ;
3. styling system ;
4. templating system ;
5. visual state system ;
6. rendering layer.

L'analyse s'appuie sur l'inspection du code core, du pipeline XAML, de plusieurs controles composites, des hotspots de rendu et des tests existants.

## Cartographie de l'architecture actuelle

### 1. Property system

Etat actuel:

- Il n'existe pas de vrai dependency property system central.
- Les valeurs sont principalement stockees comme proprietes C# ordinaires sur les controles.
- Le suivi explicite des valeurs XAML existe partiellement via `Element.ExplicitlySetProperties` pour gerer la precedence XAML vs style au parsing.
- L'invalidation est diffusee au cas par cas par `NPC(...)`, `LayoutChanged(...)`, callbacks specifiques et mises a jour locales.

Constat principal:

- MGUI dispose de proprietes observables, pas d'un systeme de valeurs avec precedence uniforme et invalidation semantique.

### 2. Resource system

Etat actuel:

- `MGResources` centralise themes, styles, ressources statiques, textures et element templates.
- `MGElement.GetResources()` retourne les ressources du desktop.
- Les bindings sur ressources utilisent `MGResources.StaticResources`.
- Il existe une separation utile entre definitions et runtime cache dans `MGResources`.

Constat principal:

- Le systeme de ressources est centralise et utile, mais il n'est ni hierarchique ni dynamique.

### 3. Styling system

Etat actuel:

- Les styles implicites et nommes existent cote XAML.
- `XAML.Element.ProcessStyles(...)` applique des setters par type et par nom.
- La precedence couverte porte surtout sur la difference entre valeur explicitement fournie dans le XAML et valeur injectee par style.
- Des tests existent pour cette logique de precedence.

Constat principal:

- Le systeme de styles est correct pour un parsing declaratif, mais il n'est pas encore un runtime styling system complet.

### 4. Templating system

Etat actuel:

- `MGElementTemplate` et `ContentTemplate` couvrent des templates de contenu / fabrique d'elements.
- Plusieurs controles exposent des delegates de type `Func<MGWindow, MGButton>` ou `Action<MGBorder>` pour personnaliser des wrappers.
- Des presenters de contenu generiques existent: `MGContentPresenter`, `MGHeaderedContentPresenter`, `MGContextualContentPresenter`.

Constat principal:

- Les primitives de composition sont bonnes, mais il manque un vrai `ControlTemplate` separant structure visuelle et logique de controle.

### 5. Visual state system

Etat actuel:

- `MGElement` expose `VisualState` et `VisualStateChanged`.
- `VisualStateFillBrush` et `VisualStateColorBrush` offrent deja une projection de valeurs selon l'etat.
- Plusieurs controles exploitent ces abstractions, mais d'autres projettent encore l'etat directement dans leur code de dessin.

Constat principal:

- Le modele d'etat existe deja et constitue une base saine, mais il n'est pas encore la couche unique de projection visuelle.

### 6. Rendering layer

Etat actuel:

- La couche de rendu sait dessiner textures, formes, bordures, texte et clips.
- Les brushes sont relativement generiques.
- Certains controles dessinent encore des symboles ou parties de skin directement dans leur code.

Constat principal:

- Le renderer lui-meme reste plutot neutre, mais le skinning n'est pas encore integralement pousse dans des couches au-dessus de lui.

## Ce qui est deja sain et doit etre preserve

### S1. `MGResources` comme point d'agregation

Pourquoi c'est sain:

- il existe deja un lieu unique ou agreguer themes, styles, ressources, textures et templates ;
- la separation `Definitions` / `RuntimeCache` est une bonne base pour un futur resource system plus propre.

Recommendation:

- conserver `MGResources` comme facade principale, mais le faire evoluer vers des scopes et un lookup hierarchique.

### S2. `VisualStateFillBrush` et `VisualStateColorBrush`

Pourquoi c'est sain:

- ces types separant sous-couche visuelle et etat sont reutilisables ;
- ils montrent que MGUI peut deja projeter l'etat vers des valeurs visuelles sans dupliquer toute la logique.

Recommendation:

- les reutiliser comme support de projection pour un futur visual state system plus central.

### S3. `MGContentPresenter` et variantes

Pourquoi c'est sain:

- ces presenters constituent de bonnes briques de composition generiques ;
- ils peuvent devenir les fondations d'un templating system plus lookless.

Recommendation:

- conserver ces presenters comme primitives de template, pas comme substitut au `ControlTemplate`.

### S4. Styles implicites / explicites au parsing

Pourquoi c'est sain:

- le parsing XAML sait deja injecter des setters de maniere predicible ;
- des tests existent pour la precedence locale vs style.

Recommendation:

- garder cette logique comme partie declarative, mais ne pas la confondre avec un moteur de styles runtime complet.

## Constats detailles

### F1. Pas de theme switch runtime propre

Couche:
resource system / property system

Observation factuelle:

- `MGResources.DefaultTheme` documente explicitement que sa modification ne met pas a jour dynamiquement les fenetres deja parsees.
- `MGElement` initialise `BackgroundBrush` a partir du theme dans son constructeur.
- De nombreux controles lisent `GetTheme()` au constructeur puis copient des valeurs dans leurs proprietes.

Zones inspectees:

- `MGUI.Core/UI/MGResources.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGComboBox.cs`
- `MGUI.Core/UI/MGListBox.cs`
- `MGUI.Core/UI/MGProgressBar.cs`
- `MGUI.Core/UI/MGWindow.cs`

Impact:

- un changement de theme runtime ne peut pas etre fiable sans reparcourir manuellement tout l'arbre ;
- les controles peuvent diverger selon qu'ils ont capture une copie du theme, une copie de brush, ou une valeur locale.

Gravite:

- critique

Recommendation architecturale:

- introduire un mecanisme de resolution dynamique des valeurs themees et une invalidation centralisee par type de propriete.

### F2. Absence de vrai property system a precedence uniforme

Couche:
property system

Observation factuelle:

- la logique de precedence existante traite surtout le cas XAML explicite vs style applique au parsing ;
- il n'existe pas de table de precedence runtime couvrant local, style implicite, style explicite, template, theme, heritage et fallback.

Zones inspectees:

- `MGUI.Core/UI/XAML/Element.cs`
- `MGUI.Tests/Styles/StylePrecedenceTests.cs`

Impact:

- impossible de raisonner uniformement sur les valeurs ;
- theme, style, template et overrides locaux risquent de se marcher dessus a mesure que le framework evolue.

Gravite:

- critique

Recommendation architecturale:

- definir une matrice de precedence stable avant d'ajouter davantage d'APIs de skinning.

### F3. Le systeme de ressources n'est pas hierarchique

Couche:
resource system

Observation factuelle:

- `MGElement.GetResources()` pointe vers `MGDesktop.Resources` ;
- les static resources des bindings sont resolues contre les ressources du desktop ;
- aucune chaine desktop -> window -> subtree -> template -> local n'apparait comme primitive standard.

Zones inspectees:

- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGResources.cs`
- `MGUI.Core/UI/Data Binding/ISourceObjectResolver.cs`

Impact:

- pas d'overrides locaux propres ;
- pas de dictionnaires de theme par ecran ou par sous-arbre ;
- pas de packaging clair entre theme global, editor UI, HUD, overlay, etc.

Gravite:

- elevee

Recommendation architecturale:

- faire evoluer `MGResources` vers des dictionnaires chaines avec lookup hierarchique et fallback explicite.

### F4. Pas de ressources dynamiques

Couche:
resource system / styling system

Observation factuelle:

- les ressources statiques existent ;
- `OnThemeAdded`, `OnStyleAdded`, `OnStaticResourceAdded` existent, mais rien n'indique une reevaluation automatique des controles deja relies a ces valeurs ;
- les themes et brushes sont en pratique consommes par copie.

Zones inspectees:

- `MGUI.Core/UI/MGResources.cs`
- `MGUI.Core/UI/MGTheme.cs`

Impact:

- le changement de theme ou de ressource est un evenement de stockage, pas un evenement de propagation UI ;
- il faut aujourd'hui du code ad hoc pour re-appliquer un look.

Gravite:

- elevee

Recommendation architecturale:

- introduire `StaticResource` et `DynamicResource` comme concepts distincts avec invalidation fine.

### F5. Les templates existants ne sont pas des control templates

Couche:
templating system

Observation factuelle:

- `MGElementTemplate` est une fabrique d'elements ;
- `ContentTemplate` couvre la generation de contenu ;
- un commentaire signale deja que l'ordre create + style n'est pas ideal ;
- plusieurs controles utilisent des delegates de wrapper (`ButtonWrapperTemplate`, `ItemContainerStyle`) plutot qu'un systeme de template structurel.

Zones inspectees:

- `MGUI.Core/UI/MGElementTemplate.cs`
- `MGUI.Core/UI/XAML/Templates.cs`
- `MGUI.Core/UI/MGContextMenu.cs`
- `MGUI.Core/UI/MGMenuBar.cs`
- `MGUI.Core/UI/MGListBox.cs`

Impact:

- la structure visuelle reste fortement liee a chaque controle ;
- les personnalisations sont ponctuelles et specifiques a chaque type ;
- la precedence style/template est fragile.

Gravite:

- elevee

Recommendation architecturale:

- introduire un `ControlTemplate` separe des templates de contenu et normaliser les template parts.

### F6. Beaucoup de controles ne sont pas lookless

Couche:
templating system / rendering layer

Observation factuelle:

- `MGWindow`, `MGOverlay`, `MGListBox`, `MGListView`, `MGContextMenuItem`, plusieurs controles docking et certains controles de selection construisent leurs sous-parties visuelles imperativement avec styles locaux et litteraux.

Zones inspectees:

- `MGUI.Core/UI/MGWindow.cs`
- `MGUI.Core/UI/MGOverlay.cs`
- `MGUI.Core/UI/MGListBox.cs`
- `MGUI.Core/UI/MGListView.cs`
- `MGUI.Core/UI/MGContextMenuItem.cs`
- `MGUI.Core/UI/Docking/Controls/*`

Impact:

- cout eleve pour changer la structure visuelle ;
- duplication des conventions visuelles ;
- regressions probables si on veut packager plusieurs themes.

Gravite:

- elevee

Recommendation architecturale:

- prioriser la migration vers des templates des controles composites avant d'ajouter des options cosmetiques supplementaires.

### F7. Etats visuels partiellement centralises, partiellement dessines a la main

Couche:
visual state system

Observation factuelle:

- le modele `VisualState` est central ;
- la projection via brushes existe ;
- mais plusieurs controles dessinent hover, pressed, selected ou symbols directement dans leurs callbacks de draw.

Zones inspectees:

- `MGUI.Core/UI/VisualState.cs`
- `MGUI.Core/UI/MGContextMenuItem.cs`
- `MGUI.Core/UI/MGCheckBox.cs`
- `MGUI.Core/UI/MGTreeViewItem.cs`
- `MGUI.Core/UI/Docking/Controls/*`

Impact:

- les etats sont modelises, mais pas encore entierement composables ;
- les styles et templates ne peuvent pas tous projeter librement les etats.

Gravite:

- elevee

Recommendation architecturale:

- centraliser la projection visuelle des etats au niveau des templates ou d'une couche de visual state mapping.

### F8. Forte presence de litteraux visuels dans des controles metier

Couche:
styling system / rendering layer

Observation factuelle:

- on trouve de nombreux `Color.White`, `Color.Black`, `Color.Yellow`, brushes et bordures crees directement dans des controles ;
- certains cas sont des primitives neutres, mais beaucoup correspondent a des decisions de skin.

Zones inspectees:

- `MGUI.Core/UI/MGContextMenuItem.cs`
- `MGUI.Core/UI/MGListView.cs`
- `MGUI.Core/UI/MGListBox.cs`
- `MGUI.Core/UI/MGOverlay.cs`
- `MGUI.Core/UI/MGWindow.cs`
- `MGUI.Core/UI/MGGridColorPicker.cs`
- `MGUI.Core/UI/Containers/Grids/MGGrid.cs`
- `MGUI.Core/UI/Containers/Grids/MGUniformGrid.cs`

Impact:

- theme incomplet ;
- difficultes a produire des skins coherents ;
- dette croissante a chaque nouveau controle.

Gravite:

- elevee

Recommendation architecturale:

- introduire des ressources ou tokens de theme semantiques avant toute migration controle par controle.

### F9. `MGTheme` est monolithique et melange tokens, decisions de skin et palettes built-in

Couche:
resource system / styling system

Observation factuelle:

- `MGTheme` embarque des palettes built-in, des brushes composes, des tailles et des decisions specifiques a certains controles ;
- les themes built-in sont construits par gros blocs de logique imperative.

Zones inspectees:

- `MGUI.Core/UI/MGTheme.cs`

Impact:

- le core UI reste fortement couple a un format de theme specifique ;
- difficile de separer theme package, tokens semantiques et styles par controle.

Gravite:

- moyenne a elevee

Recommendation architecturale:

- scinder a terme les tokens semantiques, les styles par controle et les themes built-in de demonstration.

### F10. Les styles agissent surtout au parsing XAML

Couche:
styling system

Observation factuelle:

- `ProcessStyles(...)` opere sur les objets XAML et applique des setters avant creation finale / configuration de certaines parties ;
- rien n'indique une reevaluation a chaud des styles une fois l'arbre construit.

Zones inspectees:

- `MGUI.Core/UI/XAML/Element.cs`
- `MGUI.Core/UI/XAML/XAMLParser.cs`

Impact:

- impossible de raisonner sur des styles reactifs ou sur un theme switch base sur styles ;
- difference forte entre comportement XAML et comportement code-behind.

Gravite:

- moyenne a elevee

Recommendation architecturale:

- conserver le parsing actuel, mais introduire ensuite un niveau runtime pour les valeurs issues des styles et ressources dynamiques.

### F11. La frontiere renderer / theme est plutot bonne, mais la frontiere controle / skin est poreuse

Couche:
rendering layer

Observation factuelle:

- la couche de draw transaction reste generique ;
- les brushes restent reutilisables ;
- en revanche plusieurs controles dessinent eux-memes des symboles de skin ou des overlays specifiques.

Zones inspectees:

- `MGUI.Core/UI/Brushes/*`
- `MGUI.Core/UI/MGContextMenuItem.cs`
- `MGUI.Core/UI/MGCheckBox.cs`
- `MGUI.Core/UI/Docking/Controls/*`

Impact:

- le renderer peut rester sain, mais il faudra deplacer des decisions de skin hors des controles.

Gravite:

- moyenne

Recommendation architecturale:

- garder le renderer neutre et migrer le skinning vers templates, ressources et etats visuels.

### F12. Les points d'extension actuels sont heterogenes selon les controles

Couche:
templating system / API publique

Observation factuelle:

- certains controles exposent `ButtonWrapperTemplate`, d'autres `ItemContainerStyle`, d'autres des delegates ou apply-default methods ;
- il n'existe pas encore une semantique uniforme pour remplacer l'apparence d'un controle.

Zones inspectees:

- `MGUI.Core/UI/MGContextMenu.cs`
- `MGUI.Core/UI/MGMenuBar.cs`
- `MGUI.Core/UI/MGListBox.cs`
- `MGUI.Core/UI/MGTabControl.cs`

Impact:

- l'API publique de skinning manque de coherence ;
- les consommateurs apprennent un mecanisme different par controle.

Gravite:

- moyenne

Recommendation architecturale:

- converger vers quelques primitives officielles: ressources, styles, templates, visual states.

### F13. La couverture de tests existe pour la precedence XAML, mais pas pour la future architecture runtime

Couche:
testabilite

Observation factuelle:

- des tests existent pour la precedence de styles et certaines regles XAML ;
- rien d'equivalent n'apparait pour theme switch runtime, resource lookup hierarchique, dynamic resources ou templates de controle.

Zones inspectees:

- `MGUI.Tests/Styles/StylePrecedenceTests.cs`
- `MGUI.Tests/Architecture/XamlCornerRadiusTests.cs`

Impact:

- les refontes de style/theme risquent de manquer de filets de securite.

Gravite:

- moyenne

Recommendation architecturale:

- ajouter une suite de tests purement structurels et logiques avant les migrations sensibles.

## Hotspots de migration par controle

### Priorite 1

- `MGWindow`: titre, bouton fermer, foreground et background encore fortement codes dans le controle.
- `MGOverlay`: close button, border, overlay background et presentation fortement specifiques.
- `MGContextMenuItem`: dessin manuel de fleches, checks, radio marks et overlays.
- `MGListBox`: container d'item, title border, row styling et comportement selection/hover couples aux presenters internes.
- `MGListView`: header grid et foregrounds imposes en dur.

### Priorite 2

- `MGComboBox`: dropdown window, arrow presenter, couleurs et wrappers manuels.
- `MGTreeView` / `MGTreeViewItem`: expander button, indentation, header container, fleches et selection.
- `MGTabControl`: styles par defaut de tab headers exprimes en code imperatif.
- controles docking: forte quantite de skinning direct dans les classes de tab, drawer, strip, indicators et splitter.

### Priorite 3

- `MGGridColorPicker`, `MGRatingControl`, `MGCheckBox`, `MGRadioButton`, `MGProgressBar`, `MGTimer`, `MGStopWatch`.

## Estimation qualitative du cout de migration

### Cout fort

- property precedence model ;
- ressources hierarchiques et dynamiques ;
- `ControlTemplate` ;
- migration des gros controles composites ;
- docking.

### Cout moyen

- visual state mapping ;
- rationalisation de `MGTheme` ;
- uniformisation des APIs publiques de skinning ;
- couverture de tests structurels.

### Cout faible a moyen

- documentation ;
- echantillons de theme ;
- nettoyage de litteraux visuels isoles.

## Reponses aux questions finales du cahier d'audit

### Qu'est-ce qui existe deja et qu'il faut conserver ?

- `MGResources` comme facade centrale ;
- les abstractions `VisualState*Brush` ;
- les presenters de contenu ;
- le support des styles implicites et explicites cote XAML ;
- les tests de precedence XAML existants ;
- la neutralite relative de la couche de rendu.

### Qu'est-ce qui manque reellement pour un systeme style/theme propre ?

- un vrai modele de precedence runtime ;
- des ressources hierarchiques ;
- des ressources dynamiques ;
- une invalidation fine ;
- un `ControlTemplate` officiel ;
- une projection centralisee des visual states ;
- une separation plus nette entre theme package et core UI.

### Quels controles sont les plus difficiles a rendre lookless ?

- `MGWindow` ;
- `MGOverlay` ;
- `MGContextMenuItem` ;
- `MGListBox` ;
- `MGTreeViewItem` ;
- tout le sous-systeme docking.

### Quel socle faut-il construire en premier sans sur-architecturer MGUI ?

- precedence des valeurs ;
- ressources hierarchiques ;
- invalidation ;
- `ControlTemplate` ;
- visual state mapping.

### Quel ordre de migration minimise le risque ?

- d'abord les fondations transverses ;
- ensuite les controles composites les plus visibles ;
- enfin les controles specifiques et le packaging de themes.

## Conclusion

Le point le plus important de l'audit est le suivant:

MGUI n'a pas d'abord un probleme de quantite d'options de theming. Il a surtout un probleme de frontieres architecturales inachevees entre theme, style, template, etat visuel et skin de controle.

La bonne suite n'est donc pas d'ajouter directement des proprietes visuelles partout. La bonne suite est de construire un socle minimal mais rigoureux qui permettra ensuite de rendre les controles progressivement lookless, testables et themeables sans casser les usages existants.