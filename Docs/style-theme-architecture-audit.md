# Audit Architecture Style/Theme pour MGUI

## Objectif

Produire un audit structurel de MGUI centre sur la gestion des styles, themes, templates, ressources et etats visuels, dans le contexte d'un framework UI pour jeu video.

Ce document ne decrit pas les corrections a implementer directement. Il sert de cahier d'audit pour un agent IA qui devra:

1. cartographier l'etat actuel de MGUI ;
2. identifier les couplages architecturaux, les manques et les dettes ;
3. classer les ecarts par severite, impact, risque de regression et cout probable ;
4. preparer un materiau fiable pour produire ensuite une liste de taches d'amelioration ordonnee.

## Principes cibles a utiliser comme reference

L'audit doit evaluer MGUI contre les invariants suivants:

- Un controle ne doit pas coder son apparence en dur si cette apparence devrait etre themable ou stylable.
- Le style definit des valeurs de proprietes, pas le comportement.
- Le template definit la structure visuelle du controle.
- Les couleurs, tailles, brushes et assets visuels doivent venir de ressources ou de themes, pas de litteraux disperses dans les controles.
- Les etats visuels doivent etre separes de la logique metier.
- Le core UI ne doit pas dependre d'un theme precis.
- Le renderer ne doit pas connaitre la logique des themes ni choisir l'apparence.
- Le systeme doit supporter overrides locaux, styles implicites, heritage et fallback.
- Le changement de theme doit reposer sur une invalidation propre et reproductible.
- Chaque controle doit etre le plus lookless possible compte tenu des contraintes d'un framework UI temps reel pour jeu video.

## Positionnement general

Ces idees sont saines pour MGUI. Pour un framework UI de jeu video, la bonne cible n'est pas de copier WPF ou Avalonia a l'identique, mais d'en retenir les frontieres architecturales utiles:

- la logique d'interaction doit rester distincte de l'apparence ;
- le pipeline de rendu doit rester performant et previsible ;
- les changements de theme ne doivent pas forcer des recalculs globaux ad hoc ;
- le systeme doit rester assez simple pour des ecrans in-game, HUD, overlays et outils.

Le point le plus important pour MGUI est donc la separation nette entre:

- valeurs de proprietes ;
- resolution des ressources ;
- structure visuelle ;
- etats visuels ;
- dessin bas niveau.

Si cette separation est propre, MGUI pourra rester adapte au jeu video tout en gagnant en maintenabilite, testabilite et capacite de skinning.

## Etat actuel repere avant audit complet

Le code actuel montre deja des briques utiles, mais aussi plusieurs signes d'un systeme encore partiellement fusionne.

Points deja presents:

- Un objet de theme central existe via `MGTheme`.
- Un conteneur de ressources central existe via `MGResources` avec themes, styles implicites, styles nommes, ressources statiques, textures et element templates.
- Un systeme de styles XAML existe avec `Style` et `Setter`.
- Un traitement des styles existe dans `XAML.Element.ProcessStyles(...)`.
- Un modele `VisualState` existe deja sur `MGElement`.
- Des templates de contenu existent via `MGElementTemplate` et `ContentTemplate`.

Signaux de dette architecturale deja visibles:

- Le theme semble principalement resolu a l'instanciation puis copie dans les proprietes des controles.
- `MGResources.DefaultTheme` est documente comme non dynamique pour les fenetres deja parsees.
- Le systeme de styles applique surtout des setters au moment du parsing XAML, pas comme un systeme runtime reactif.
- Les templates existants sont des templates de contenu, pas encore de vrais control templates lookless.
- Plusieurs controles semblent tirer des couleurs, brushes ou tailles directement depuis le theme ou utiliser des litteraux visuels dans leur code.
- Les etats visuels existent, mais leur consommation visuelle parait encore souvent geree directement par les controles.

Fichiers a utiliser comme points d'entree pendant l'audit:

- `MGUI.Core/UI/MGTheme.cs`
- `MGUI.Core/UI/MGResources.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/XAML/Style.cs`
- `MGUI.Core/UI/XAML/Element.cs`
- `MGUI.Core/UI/XAML/Templates.cs`
- `MGUI.Core/UI/MGElementTemplate.cs`

## Perimetre de l'audit

L'audit doit couvrir 6 couches, plus des verifications transverses.

### 1. Property System

Verifier si MGUI dispose deja, explicitement ou implicitement, d'un systeme de proprietes permettant:

- valeur locale ;
- valeur issue du style implicite ;
- valeur issue du style explicite ;
- valeur issue du template ;
- valeur issue du theme ou d'une ressource ;
- heritage depuis le parent si pertinent ;
- fallback vers une valeur par defaut ;
- invalidation fine layout / draw / text / input.

Questions a analyser:

- Quelle est aujourd'hui la precedence exacte des valeurs ?
- Cette precedence est-elle uniforme entre code, XAML, style et theme ?
- Les proprietes sont-elles observables sans coupler toute l'UI a `INotifyPropertyChanged` generaliste ?
- Une modification de valeur sait-elle invalider seulement ce qui est necessaire ?
- Les proprietes heritables existent-elles deja pour texte, foreground, theme local, ressources, etc. ?
- Les proprietes ayant un impact layout sont-elles clairement distinguees des proprietes purement visuelles ?

Preuves a collecter:

- tableau des proprietes majeures par type de controle ;
- source de chaque valeur par defaut ;
- schema de precedence reel ;
- liste des invalidations declenchees par famille de proprietes.

### 2. Resource System

Verifier si le systeme de ressources actuel peut evoluer vers un vrai mecanisme de lookup hierarchique et de theme packaging.

Questions a analyser:

- Les ressources vivent-elles seulement au niveau desktop, ou aussi au niveau fenetre, sous-arbre, template, controle ?
- La recherche de ressource suit-elle une chaine hierarchique coherente ?
- Les ressources differentielles par theme sont-elles supportees ou seulement des objets `MGTheme` monolithiques ?
- Existe-t-il une difference claire entre ressource statique et ressource dynamique ?
- Une ressource peut-elle etre remplacee sans recreer tout l'arbre visuel ?
- Les textures, brushes, couleurs, tailles, polices et templates utilisent-ils le meme modele de lookup ou plusieurs modeles ad hoc ?

Preuves a collecter:

- cartographie des dictionnaires et points d'acces ;
- matrice des types de ressources supportes ;
- regles de resolution et fallback ;
- comportement attendu et reel en cas de changement de ressource.

### 3. Styling System

Verifier si le systeme de styles depasse deja le simple lot de setters applique au parsing XAML.

Questions a analyser:

- Les styles implicites par type sont-ils robustes et predictibles ?
- Les styles explicites nommes ont-ils une precedence claire face aux valeurs locales ?
- `BasedOn` existe-t-il ou non, et sinon quelle est la dette resultante ?
- Existe-t-il une notion de style pour composants internes seulement, ou bien les composants internes sont-ils exposes involontairement ?
- Le style peut-il cibler un controle sans toucher sa logique interne ?
- Le systeme de styles sait-il etre re-evalue proprement si une ressource ou un theme change ?
- La distinction entre style et theme est-elle nette ou les deux portent-ils les memes responsabilites ?

Preuves a collecter:

- table de precedence reelle entre valeur locale, style implicite, style explicite et theme ;
- liste des features absentes: `BasedOn`, triggers, selectors, sealed styles, etc. ;
- exemples de cas ou le style est applique seulement au parsing au lieu d'etre resolu dynamiquement.

### 4. Templating System

Verifier si MGUI dispose d'un vrai systeme de templates de controle ou seulement d'un mecanisme de fabrique de contenu.

Questions a analyser:

- Un controle complexe peut-il remplacer sa structure visuelle sans reimplementer sa logique ?
- Existe-t-il une notion de `ControlTemplate` separee de `ContentTemplate` ?
- Les parties internes d'un controle sont-elles exposees comme template parts ou codees en dur dans les constructeurs ?
- Le controle reste-t-il fonctionnel si son visuel est remplace ?
- Les presenters de contenu sont-ils des primitives generiques reutilisables ?
- La creation des composants internes preserve-t-elle la precedence des styles et ressources ?

Preuves a collecter:

- inventaire des controles composites ;
- liste des composants internes crees imperativement ;
- cas ou la structure visuelle est indissociable de la logique ;
- ecarts entre templates existants et vrais control templates.

### 5. Visual State System

Verifier si les etats visuels sont modelises de facon centrale et si leur rendu reste decouple de la logique metier.

Questions a analyser:

- Quels etats existent deja: normal, hover, pressed, disabled, focused, selected, dragging, validation, etc. ?
- Les transitions d'etat sont-elles centralisees ou dispersees dans les controles ?
- Les etats visuels influencent-ils des proprietes stylables ou bien declenchent-ils un dessin specifique code a la main ?
- Existe-t-il une separation entre calcul de l'etat et projection visuelle de l'etat ?
- Les transitions sont-elles instantanees, animees, configurables ou inexistantes ?
- Les composants internes d'un template peuvent-ils reagir a l'etat du controle parent sans code ad hoc ?

Preuves a collecter:

- cartographie des etats exposes par `MGElement` et par controle ;
- liste des subscriptions et callbacks relies a `VisualStateChanged` ;
- exemples ou l'etat visuel est derive proprement, et exemples ou il est dessine en dur.

### 6. Rendering Layer

Verifier si la couche de rendu reste strictement en dessous du style et du theme.

Questions a analyser:

- Le renderer recupere-t-il uniquement des draw commands / brushes / geometries deja resolus ?
- Le renderer ou les brushes choisissent-ils eux-memes des couleurs de theme ?
- Les controles dessinent-ils leur skin directement au lieu de produire un visuel compose ?
- Les primitives de rendu sont-elles suffisamment generiques pour supporter plusieurs themes sans brancher de logique conditionnelle partout ?
- Les performances du rendu sont-elles compatibles avec un systeme plus dynamique de styles et ressources ?

Preuves a collecter:

- frontiere entre `MGElement`, brushes, theme et draw transaction ;
- exemples de rendu neutre et exemples de rendu qui encapsule une decision de skin ;
- points de couplage forts entre controle, brush et apparence finale.

## Verifications transverses obligatoires

### A. Controles lookless ou non

Pour chaque famille de controle, classifier le niveau de couplage entre logique et apparence:

- lookless presque complet ;
- partiellement lookless ;
- fortement skinned par le controle lui-meme.

Familles a prioriser:

- boutons et toggles ;
- text input et text display ;
- listes, arbres et menus contextuels ;
- containers ;
- fenetres, headers, tabs, docking ;
- overlays, tooltips et HUD-like elements.

### B. Litteraux visuels dans le code

Rechercher et classer:

- `Color.*` utilises pour le skin d'un controle ;
- tailles ou marges visuelles codees en dur ;
- brushes instancies directement dans les controles ;
- assets visuels references sans passer par une ressource themable.

L'objectif n'est pas d'interdire tous les litteraux, mais d'identifier ceux qui devraient relever du theme ou d'une ressource.

### C. Changement de theme runtime

Verifier si un changement de theme peut aujourd'hui:

- mettre a jour les valeurs visuelles ;
- invalider proprement mesure, arrangement et dessin si necessaire ;
- eviter une recreation totale de l'arbre ;
- rester deterministe et testable.

Cas importants:

- theme desktop global ;
- theme par fenetre ;
- override local de theme ou de ressource ;
- theme different pour overlay, debug UI, HUD, editor UI.

### D. Heritage et fallback

Verifier si les mecanismes d'heritage et fallback sont coherents pour:

- foreground texte ;
- familles de police et tailles ;
- brushes par defaut ;
- ressources statiques ;
- styles implicites ;
- theme window vs desktop vs local.

### E. API publique et XAML

Verifier si l'API publique expose une semantique coherent avec l'architecture cible.

Questions a analyser:

- Les noms sont-ils suffisamment precis: Theme, Style, Template, Resource, VisualState ?
- Les comportements publics sont-ils stables ou dependent-ils de details internes ?
- Les objets XAML exposent-ils des concepts que le runtime ne sait pas faire respecter proprement ?
- Y a-t-il des proprietes publiques qui sont en realite des raccourcis de skinning et non des concepts durables ?

### F. Performance, cache et invalidation

Pour un framework de jeu video, ce point est critique.

Verifier:

- quels objets sont recrees trop souvent ;
- quelles resolutions de theme/style/template sont faites a chaud ;
- s'il existe des caches stables par controle, type ou theme ;
- si les invalidations peuvent etre localisees ;
- si la future architecture pourrait supporter un theme switch ou un chargement d'ecran sans spikes inutiles.

### G. Testabilite

Verifier quels comportements sont testables sans rendu visuel fragile:

- precedence des valeurs ;
- lookup de ressources ;
- resolution des styles ;
- changement de theme ;
- projection des visual states ;
- construction de templates.

Identifier les zones qui demanderont des tests de regression unitaires avant toute refonte.

## Controle de sortie attendu pour l'agent d'audit

L'agent d'audit doit produire un rapport contenant au minimum:

1. Un resume executif de 1 page maximum.
2. Une cartographie de l'architecture actuelle par couche.
3. Une liste des ecarts majeurs entre architecture actuelle et architecture cible.
4. Une liste des couplages dangereux a casser en priorite.
5. Une liste des comportements deja sains a preserver.
6. Une estimation qualitative du cout de migration par sujet.
7. Une proposition de decoupage en taches pour un agent implementeur.

## Format recommande des constats

Chaque constat doit suivre le format ci-dessous:

- Titre court.
- Couche concernee.
- Fichiers ou zones de code inspectes.
- Observation factuelle.
- Risque produit ou technique.
- Gravite: critique / elevee / moyenne / faible.
- Recommandation architecturale.
- Impact migration: faible / moyen / fort.
- Preconditions ou dependances.

## Regles pour l'agent d'audit

- Ne pas proposer de refonte massive sans preuve code en main.
- Distinguer les dettes de design des simplifications legitimes pour un framework de jeu.
- Ne pas conclure qu'un litteral visuel est un probleme s'il s'agit d'une primitive de rendu ou d'une valeur purement technique.
- Prioriser les points qui empechent la separation style / theme / template / renderer.
- Signaler explicitement ce qui releve d'un manque de fonctionnalite et ce qui releve d'un couplage structurel.
- Faire la difference entre ce qui est applique au parsing XAML et ce qui est resolu dynamiquement au runtime.

## Ordre de travail recommande pour l'audit

1. Cartographier `MGTheme`, `MGResources`, `MGElement` et le pipeline XAML.
2. Inventorier comment les controles recuperent leurs valeurs visuelles.
3. Relever les litteraux visuels et les skins dessines directement par les controles.
4. Evaluer la precedence reelle des valeurs et l'absence ou non d'un vrai property system.
5. Evaluer les templates existants et la faisabilite d'un `ControlTemplate`.
6. Evaluer le modele de visual states et son niveau de couplage.
7. Evaluer le changement de theme runtime, le cache et l'invalidation.
8. Produire la liste des taches de refonte ordonnee par dependances.

## Taches pour un agent IA charge de l'audit

Les taches ci-dessous sont destinees a un agent d'audit, pas a un agent d'implementation.

Legende de statut:

- ⚪ a faire
- 🟡 en cours
- ✅ termine

### ✅ 1. Cartographier l'architecture actuelle

But:
etablir la carte des composants qui participent aux styles, themes, ressources, templates et etats visuels.

Travail attendu:

- identifier les types centraux ;
- identifier les points d'entree runtime et XAML ;
- decrire le sens des dependances entre theming, styling, templating et rendering.

Livrable:

- schema textuel des couches ;
- liste des fichiers pivots ;
- premiere liste de zones a risque.

Resultat:

- La cartographie centrale a ete etablie autour de `MGResources`, `MGElement`, `XAML.Element`, `MGControlTemplate`, `MGElementTemplate`, `UIResourceReferenceApplicator`, `MGVisualStateProjection`, `MGDesktop` et `UIFocusNavigationService`.
- Le pipeline observe est separe en cinq couches: ressources, controles, styles XAML, templates, etats visuels/navigation, avec une frontiere de rendu en dessous.
- Les fichiers pivots confirmes sont `MGUI.Core/UI/MGResources.cs`, `MGUI.Core/UI/MGElement.cs`, `MGUI.Core/UI/XAML/Element.cs`, `MGUI.Core/UI/Styling/MGControlTemplate.cs`, `MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs`, `MGUI.Core/UI/XAML/Themes.cs`, `MGUI.Core/UI/MGTheme.cs` et `MGUI.Core/UI/MGDesktop.cs`.
- Les premieres zones a risque confirmees sont le couplage theme/constructeur dans plusieurs controles composites, le caractere non structurel des `ControlTemplate`, et la resolution de valeurs encore dispersee entre theme, styles XAML et code imperatif.
- Le livrable detaille correspondant est centralise dans `Docs/audit-theme-style-runtime-deep.md`.

### ✅ 2. Auditer la precedence des valeurs et l'invalidation

But:
comprendre comment une valeur visuelle arrive effectivement sur un controle et ce qui se passe quand elle change.

Travail attendu:

- relever la precedence reelle entre default, theme, style, XAML et code ;
- relever les invalidations declenchees ;
- identifier les trous de precedence et les zones ad hoc.

Livrable:

- tableau de precedence ;
- liste des invalidations existantes ;
- liste des proprietes problematiques.

Resultat:

- La precedence cible est explicitement definie dans `UIValuePrecedence`: `Animation > LocalValue > LocalBinding > VisualState > Template > ExplicitStyle > ImplicitStyle > DynamicResource > Theme > Inherited > DefaultValue`.
- Cette precedence n'est pas encore appliquee par un moteur runtime unifie. En pratique, les styles implicites et explicites s'appliquent surtout au parsing XAML, les themes s'appliquent via constructeurs et `OnThemeChanged(...)`, et les valeurs locales via setters C# ordinaires.
- L'invalidation de theme est centralisee dans `MGElement.NotifyThemeChanged(...)`, avec propagation recursive et appel a `ApplyControlTemplate(true)`, mais la semantique fine reste majoritairement manuelle.
- `MGTextBlock` est le cas le plus abouti: il surcharge `GetThemeInvalidation(...)` pour distinguer un changement purement visuel d'un changement de police qui affecte `Measure|Arrange|Draw`.
- Les proprietes problematiques sont celles qui sont themables mais encore stockees comme valeurs locales simples sans trace de source ni invalidation semantique standardisee: paddings, bordures, backgrounds, foregrounds et tailles injectes par les controles composites.

### ✅ 3. Auditer le systeme de ressources

But:
evaluer si `MGResources` peut devenir la base d'un vrai resource system hierarchique.

Travail attendu:

- documenter lookup, scope, fallback et mutabilite ;
- verifier l'absence ou non de ressources dynamiques ;
- verifier la separation entre ressources definitions et runtime cache.

Livrable:

- matrice des ressources ;
- limites actuelles ;
- recommandations structurelles.

Resultat:

- `MGResources` est deja un vrai systeme de scopes hierarchiques pour themes, styles, ressources statiques, `ControlTemplate` et `ElementTemplate`, avec fallback parent coherent entre `Desktop`, `Window`, `Subtree` et `Template`.
- La separation `Definitions` / `RuntimeCache` est saine et deja testee, notamment pour differencier les ressources nommees du cache runtime des textures.
- Les `StaticResource` et `DynamicResource` existent reellement. `UIResourceReferenceApplicator` sait reappliquer une resource dynamique quand une valeur change ou quand un fallback parent redevient actif.
- Le packaging de theme reste toutefois encore surtout porte par `MGTheme`, qui demeure un objet assez monolithique meme si les definitions XAML permettent des themes derives et partiels.
- La limite structurelle principale est le cycle de vie des abonnements dynamiques: le rebind fonctionne, mais l'architecture actuelle ne montre pas encore de nettoyage explicite des handlers en fin de vie d'un element.

### ✅ 4. Auditer le systeme de styles

But:
mesurer l'ecart entre styles actuels et un styling system robuste.

Travail attendu:

- analyser styles implicites, styles nommes et leur application ;
- relever l'absence ou non de `BasedOn`, triggers et reevaluation runtime ;
- verifier la place des composants internes dans le styling.

Livrable:

- matrice des features de styling ;
- constats de couplage ;
- priorites de refonte.

Resultat:

- Le systeme de styles actuel est robuste pour un pipeline declaratif XAML: styles implicites par type, styles nommes, merge avec les scopes parents et respect des valeurs explicitement definies dans le XAML.
- La logique vit principalement dans `XAML.Element.ProcessStyles(...)`, ce qui confirme que le styling est aujourd'hui surtout un mecanisme de parsing et non un moteur runtime reactif.
- `BasedOn`, triggers, selectors et reevaluation globale d'un sous-arbre deja instancie ne sont pas visibles dans l'architecture auditee. C'est une absence fonctionnelle, pas encore un bug, mais elle limite fortement les variantes de skin et de densite a chaud.
- La distinction style/theme est correcte dans l'intention, mais brouillee en pratique par les controles qui continuent d'aller directement lire `GetTheme()` pour definir des valeurs qui pourraient relever d'un style ou d'un template.
- Priorite de refonte confirmee: ne pas enrichir les styles a l'aveugle avant d'avoir stabilise la resolution runtime des valeurs et la frontiere entre style, template et theme.

### ✅ 5. Auditer les templates et le caractere lookless des controles

But:
determiner si les controles peuvent evoluer vers des control templates sans casser leur logique.

Travail attendu:

- inventorier les controles composites ;
- relever les composants internes crees en dur ;
- evaluer la separation structure visuelle / comportement.

Livrable:

- classification lookless par controle ;
- liste des controles les plus couples a leur skin ;
- faisabilite d'une migration vers `ControlTemplate`.

Resultat:

- Le constat central est confirme: `MGControlTemplate` n'est pas aujourd'hui un vrai template de structure. Il applique des defaults sur des `TemplateParts` deja crees par le controle au lieu de construire ou remplacer la structure visuelle.
- Les controles les plus avances sur ce plan sont surtout ceux du docking et certains wrappers de menu contextuel, qui exposent des parts stables et des proprietes visuelles dediees.
- Les controles hybrides majeurs sont `MGWindow`, `MGOverlay`, `MGContextMenu`, `MGListBox<T>`, `MGListView<T>` et `MGTreeView`: ils ont des parts et des templates, mais continuent de construire eux-memes une grande partie de leur chrome.
- Les controles les plus couples a leur skin restent `MGComboBox<T>`, `MGTabControl`, `MGTextBox`, `MGToolTip`, `MGScrollViewer` et plusieurs widgets de base qui lisent le theme en constructeur ou dans `OnThemeChanged(...)`.
- Une migration vers un vrai `ControlTemplate` est faisable, mais elle doit etre selective et progressive: d'abord documenter l'applicateur de chrome actuel, puis introduire un niveau structurel pour les controles composites cibles.

### 🟡 6. Auditer les visual states

But:
evaluer si l'etat visuel est une couche autonome ou un effet secondaire de logique de controle.

Travail attendu:

- lister les etats ;
- lister les points de transition ;
- relever les projections visuelles codees directement dans les controles.

Livrable:

- carte des etats ;
- dette de separation logic / visual ;
- besoins minimaux d'un futur visual state system.

### ⚪ 7. Auditer la frontiere renderer / theme / controle

But:
garantir que la refonte future ne pousse pas de logique de theming dans le renderer.

Travail attendu:

- verifier les responsabilites du renderer et des brushes ;
- relever les cas ou le controle dessine lui-meme son skin ;
- verifier quelles primitives sont suffisamment neutres.

Livrable:

- frontiere architecturale cible ;
- liste des violations actuelles ;
- risques de performance associes.

### ⚪ 8. Auditer le changement de theme runtime

But:
verifier si MGUI peut evoluer vers un theme switch propre sans reparse global brutal.

Travail attendu:

- verifier le comportement actuel de `DefaultTheme` et des themes de fenetre ;
- rechercher les valeurs copiees a l'initialisation ;
- identifier les mecanismes manquants d'invalidation et de propagation.

Livrable:

- etat reel du theme switch ;
- liste des blocages ;
- conditions necessaires pour supporter des ressources dynamiques.

### ⚪ 9. Auditer les hotspots controle par controle

But:
prioriser les controles qui feront derailer une refonte si on les ignore.

Travail attendu:

- cibler d'abord menus, text input, listes, tabs, docking, fenetres, tooltips ;
- mesurer pour chacun le niveau de hard-coded appearance ;
- relever les dependances a des valeurs de theme prises en constructeur.

Livrable:

- tableau par controle ;
- severite ;
- ordre de migration recommande.

### ⚪ 10. Convertir l'audit en backlog de refonte

But:
transformer l'audit en plan d'action sequence pour un agent implementeur.

Travail attendu:

- regrouper les constats par dependances ;
- separer fondations, integrations et adoptions ;
- proposer des petites etapes verticales testables.

Livrable:

- liste de taches ordonnee ;
- criteres d'acceptation ;
- risques et preconditions par tache.

## Resultat attendu a la fin de l'audit

Si l'audit est bon, il doit permettre de repondre clairement a ces questions:

- Qu'est-ce qui existe deja et qu'il faut conserver ?
- Qu'est-ce qui manque reellement pour un systeme style/theme propre ?
- Quels controles sont les plus difficiles a rendre lookless ?
- Quel socle faut-il construire en premier sans sur-architecturer MGUI ?
- Quel ordre de migration minimise le risque pour un framework UI de jeu video ?

Ce document doit etre traite comme le prerequis avant toute roadmap de refonte style/theme de MGUI.