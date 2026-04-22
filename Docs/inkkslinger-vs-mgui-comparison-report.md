# Compte rendu - comparaison InkkSlinger vs MGUI

## Statut

- Statut global: consolide
- Date de consolidation: 2026-04-22
- Auteur/agent consolidateur: GitHub Copilot

## Perimetre

- Repo analyse: `InkkSlinger/`
- Repo de reference: `MGUI/`
- Objectif: comparer la structure, l'architecture, les fonctionnalites, et les opportunites pour MGUI.

## Sources consolidees

### InkkSlinger

- `InkkSlinger/README.md`
- `InkkSlinger/UI-FOLDER-MAP.md`
- `InkkSlinger/InkkSlinger.sln`
- `InkkSlinger/InkkSlinger.UI/`
- `InkkSlinger/InkkSlinger.Tests/`
- `InkkSlinger/InkkSlinger.DemoApp/`
- `InkkSlinger/InkkSlinger.WpfLab/`
- `InkkSlinger/site/docs/`

### MGUI

- `README.md`
- `MGUI.sln`
- `MGUI.Core/UI/`
- `MGUI.Shared/`
- `MGUI.Rendering.Abstractions/`
- `MGUI.MonoGame/`
- `MGUI.Tests/`
- `MGUI.Samples/`
- `Docs/`
- `wpf-controls-gap-analysis.md`

## Resume executif

InkkSlinger est objectivement une plateforme UI plus large et plus WPF-like que MGUI: plus de projets satellites, plus de controles exposes, plus de tests, plus de demos, une documentation publique nettement plus riche, un systeme d'automation/diagnostic natif, et une architecture de runtime tres proche de WPF sur plusieurs sujets. MGUI est plus compact, plus pragmatique, et surtout mieux decouple du backend de rendu via `MGUI.Shared`, `MGUI.Rendering.Abstractions` et `MGUI.MonoGame`.

Le point le plus important pour MGUI n'est pas de poursuivre une parite WPF exhaustive. Le repo a plus a gagner en consolidant ses seams deja poses pour un framework UI temps reel MonoGame: validation du markup, diagnostics, robustesse des templates/themes/styles, automation legere, et une surface fonctionnelle mieux alignee sur les besoins jeu/outillage. Les inspirations InkkSlinger les plus rentables sont donc les garde-fous de qualite et quelques families de controles bien choisies, pas son property system complet ni sa totalite du modele WPF.

### Ce qu'InkkSlinger apporte de plus

- Une plateforme plus complete: 10 projets, 85+ controles annonces, 234 tests annonces, 86 vues demo/repro annoncees, 122 pages de doc annoncees.
- Un noyau WPF-like tres large: `DependencyObject`, `UIElement`, `FrameworkElement`, `Control`, precedence multi-couches, arbres logique/visuel, layout measure/arrange.
- Un runtime plus riche: retained rendering, dirty regions, pipeline input/focus/commands plus pousse, popups et overlays mieux outilles.
- Une couche styles/templates/markup plus profonde: triggers, `ControlTemplate`, `EventSetter`, validation stricte, source generator `x:Name`.
- Un outillage de qualite de niveau produit: InkkOops, telemetry, CLI automation, demos granulaires et docs publiques par controle.

### Ce que MGUI fait deja mieux ou plus simplement

- Un decouplage backend/runtime plus propre et plus defendable pour MonoGame et pour un futur multi-backend.
- Un modele plus pragmatique pour un framework de jeu: moins de poids WPF systemique, plus de flexibilite sur les choix runtime.
- Une base lookless et theming deja utile: `ThemeDefinition`, `UIValuePrecedence`, templates, navigation input semantique, docking, overlays.
- Une surface deja orientee jeu/outillage sur certains points (`MGChatBox`, `MGRatingControl`, `MGProgressButton`, docking).

### Opportunites prioritaires pour MGUI

1. Ajouter un outillage de robustesse leger: diagnostics structures, captures d'etat, replay d'input et artefacts de repro.
2. Consolider la frontiere style/theme/template/invalidation au runtime avant d'elargir fortement la surface de controles.
3. Durcir la validation du markup et la qualite des erreurs du loader.
4. Exposer des controles de shapes retained au-dessus des primitives de rendu deja disponibles.
5. Envisager un DataGrid-lite oriente outils/debug avant les families plus desktop-centric.

## 1. Cartographie des repos

| Sujet | InkkSlinger | MGUI | Ecart notable |
|---|---|---|---|
| Solution | 10 projets | 8 projets | InkkSlinger expose plus de tooling et de packaging; MGUI expose mieux la separation runtime/backend |
| Projets coeur | `InkkSlinger.UI` | `MGUI.Core` + `MGUI.Shared` + `MGUI.Rendering.Abstractions` + `MGUI.MonoGame` | MGUI a une decomposition architecturale plus explicite |
| Tests | `InkkSlinger.Tests` + `InkkSlinger.WpfLab.Tests` | `MGUI.Tests` | InkkSlinger annonce une couverture et une variete plus larges |
| Demo / repro | `InkkSlinger.DemoApp` avec vues par controle | `MGUI.Samples` + `MGUI.MiniGame` | InkkSlinger couvre mieux le scenario repro/debug par controle |
| Outillage | `InkkOops.Cli`, `XamlNameGenerator`, `Designer`, `TemplatePack` | `UIToolingService` et tooling epars | InkkSlinger traite le tooling comme un produit, MGUI comme un support interne |
| Documentation | `site/docs/` public | `Docs/` architecture/migration + README | InkkSlinger documente le framework pour des utilisateurs externes; MGUI documente surtout les chantiers internes |

### Constats

- InkkSlinger structure sa solution par verticales produit: runtime, tests, demos, comparaison WPF, CLI automation, generation de code, templates de depart.
- MGUI structure sa solution par couches architecturales: coeur UI, contrats partages, backend concret, moteur texte optionnel, tests, samples.
- L'ecart le plus net n'est pas seulement en nombre de projets mais en nature des projets: InkkSlinger a plusieurs outils utilisateurs/QA, MGUI privilegie la separation technique.
- InkkSlinger integre un projet WPF de comparaison (`InkkSlinger.WpfLab`) sans equivalent direct dans MGUI.
- MGUI dispose d'une meilleure base pour un futur support multi-backend, ce que le repo InkkSlinger n'expose pas au meme niveau.

### Preuves

- `InkkSlinger/README.md` -> annonce 85+ controles, 234 tests, 86 demos, 122 pages de doc.
- `InkkSlinger/InkkSlinger.sln` -> montre `InkkSlinger.UI`, `InkkSlinger.Tests`, `InkkOops.Cli`, `InkkSlinger.WpfLab`, `InkkSlinger.Designer`, `InkkSlinger.Template`, `InkkSlinger.TemplatePack`.
- `InkkSlinger/UI-FOLDER-MAP.md` -> confirme la largeur du coeur UI et de ses sous-systemes.
- `MGUI.sln` -> montre `MGUI.Core`, `MGUI.Shared`, `MGUI.Rendering.Abstractions`, `MGUI.MonoGame`, `MGUI.FontStashSharp`, `MGUI.Tests`, `MGUI.Samples`, `MGUI.MiniGame`.
- `Docs/rendering-backend-architecture.md` -> confirme la separation coeur/contrats/backend cote MGUI.

## 2. Architecture coeur

| Capacite | InkkSlinger | MGUI | Evaluation | Impact pour MGUI |
|---|---|---|---|---|
| Hierarchie d'elements | `DependencyObject -> UIElement -> FrameworkElement -> Control` | `MGElement` comme base centrale | Partiel | Reproduire la hierarchie WPF complete serait couteux et peu rentable |
| Property system | `DependencyProperty` + metadata | Proprietes CLR + logique imperative | Absent | Le gain architectural existe, mais le cout de migration serait tres eleve |
| Invalidation | Declarative via metadata (`AffectsMeasure`, etc.) | Imperative via appels explicites | Partiel | MGUI pourrait enrichir l'invalidation sans adopter tout WPF |
| Arbre logique / visuel | Distinction explicite | Arbre unique pratique | Absent | Peu prioritaire tant que les templates/lookless restent bornees |
| Layout | Measure/Arrange + stabilisation multi-passes | Update/Draw + layout maison | Specifique | Le modele MGUI est acceptable pour un framework temps reel |
| Precedence de valeurs | Multi-couches implicites WPF-like | `UIValuePrecedence` explicite | Equivalent | MGUI a deja une base saine et lisible |
| Navigation structurelle | Enfant/logical/visual parents plus riches | Parent/enfants et services de navigation/focus dedies | Partiel | Le besoin de refonte est faible si le focus reste robuste |

### Constats

- InkkSlinger pousse plus loin la fidelite WPF structurelle; MGUI privilegie une base plus simple et plus directe.
- L'ecart architectural le plus important n'est pas la hierarchie des types mais le property system et l'invalidation declarative.
- MGUI possede deja une brique utile que le repo InkkSlinger exprime moins explicitement: une precedence runtime des valeurs claire et isolee.
- La dette de MGUI est moins l'absence d'un systeme WPF complet que l'absence de certains garde-fous reactifs autour des styles, templates et invalidations.

### Preuves

- `InkkSlinger/InkkSlinger.UI/UI/Controls/Base/UIElement.cs` -> `UIElement : DependencyObject`.
- `InkkSlinger/InkkSlinger.UI/UI/Core/DependencyProperties/DependencyProperty.cs` -> property system centralise.
- `InkkSlinger/InkkSlinger.UI/UI/Core/DependencyProperties/FrameworkPropertyMetadata.cs` -> flags `AffectsMeasure`, `AffectsArrange`, `AffectsRender`.
- `InkkSlinger/InkkSlinger.UI/UI/Managers/Layout/LayoutManager.cs` -> boucle de stabilisation layout.
- `MGUI.Core/UI/MGElement.cs` -> base centrale MGUI.
- `MGUI.Core/UI/Styling/UIValuePrecedence.cs` -> precedence runtime explicite.
- `MGUI.Core/UI/Navigation/UIFocusNavigationService.cs` -> navigation/focus dedies cote MGUI.

## 3. Runtime interactif et rendu

| Sujet | InkkSlinger | MGUI | Evaluation | Notes |
|---|---|---|---|---|
| Coordinateur runtime | `UiRoot` centralise | `MGDesktop` + `UIView` + services dedies | Partiel | InkkSlinger orchestre plus; MGUI decouple plus |
| Rendering retained/immediate | Retained rendering | Plutot immediate/engine-owned | Specifique | Le retained d'InkkSlinger est plus sophistique |
| Dirty regions | Oui | Non visible comme systeme equivalent | Absent | Gros ecart technique, pas necessairement prioritaire |
| Clipping | Rectangle/scissor | Rectangle expose + infrastructure clip plus generique | Partiel | MGUI a une meilleure seam backend |
| Overlays / popups | Plus riches, registry et popup model | Plus simples, menus/overlays plus pragmatiques | Partiel | InkkSlinger depasse MGUI sur la richesse UI |
| Input routing | Pipeline centralise, overlay-aware, caches | Routage plus leger et services de focus/navigation | Partiel | MGUI est plus simple mais couvre le coeur jeu/UI |
| Focus | Plus WPF-like | Plus cible jeu/navigation | Partiel | MGUI doit surtout continuer la robustesse focus/input |
| Commands | `RoutedCommand`, `CommandManager` | Pas d'equivalent complet | Absent | Peu prioritaire pour les usages jeu |
| Couplage backend | Plus serre a MonoGame | Meilleur decouplage explicite | Specifique MGUI | Avantage majeur de MGUI |

### Constats

- InkkSlinger a un runtime plus riche, plus instrumente, plus optimise pour une UI complexe de type application.
- MGUI a une architecture de rendu plus defendable a long terme grace a ses contrats et son backend MonoGame separe.
- Les dirty regions et le retained rendering d'InkkSlinger sont impressionnants mais pas automatiquement rentables pour tous les profils d'usage MGUI.
- Le vrai levier pour MGUI est probablement l'ajout de diagnostics, de reproductibilite et de preuves de robustesse, pas une copie brute du runtime WPF-like.

### Preuves

- `InkkSlinger/InkkSlinger.UI/UI/Managers/Root/UiRoot.cs` -> coordinateur central avec retained list et dirty tracking.
- `InkkSlinger/InkkSlinger.UI/UI/Managers/Root/Services/UiRootDirtyRegionOps.cs` -> dirty region tracking.
- `InkkSlinger/InkkSlinger.UI/UI/Managers/Root/Services/UiRootInputPipeline.cs` -> pipeline input riche.
- `InkkSlinger/InkkSlinger.UI/UI/Commanding/CommandManager.cs` -> systeme de commandes WPF-like.
- `MGUI.Core/UI/MGDesktop.cs` -> coordinateur central cote MGUI.
- `MGUI.Core/UI/UIView.cs` -> pont runtime/surface.
- `MGUI.Shared/Rendering/IUIRenderContext.cs` -> contrat clip/render backend-neutral.
- `Docs/rendering-backend-architecture.md` -> formalisation du split backend MGUI.

## 4. Style, theme, templates et markup

| Sujet | InkkSlinger | MGUI | Evaluation | Pertinence MGUI |
|---|---|---|---|---|
| Resources | `ResourceDictionary` + merged dictionaries | `MGResources` + scopes | Partiel | MGUI est plus simple et probablement suffisant |
| Styles | Setters + `BasedOn` + runtime apply | Setters simples, surtout parsing-time | Partiel | Bonne zone d'amelioration cote MGUI |
| Templates | `ControlTemplate` WPF-like | Templates/lookless en progression | Partiel | Axe strategique pertinent pour MGUI |
| Visual states | `VisualStateManager`, groups, storyboards | `VisualState` + `VisualStateSetting<T>` | Equivalent | Le modele MGUI reste defensable et plus leger |
| Triggers | Large surface (Trigger, DataTrigger, etc.) | Absent | Absent | A n'introduire que si besoin concret et scope borne |
| Markup loader | XML strict | XAML Portable.Xaml plus permissif | Partiel | Le durcissement du loader MGUI est un vrai levier |
| Validation du markup | Compile-time + runtime stricte | Validation plus faible | Absent | Opportunite claire pour MGUI |
| Code generation / names | Source generator `x:Name` | Pas d'equivalent visible | Absent | Valeur moyenne, pas priorite haute |

### Ce que MGUI peut reprendre

- Un `Style.BasedOn` simple pour reduire la duplication de themes/styles.
- Une reevaluation runtime plus robuste des styles/templates/themes quand les ressources changent.
- Une validation du markup plus stricte, avec de meilleurs diagnostics et un mode progressif.
- Une separation encore plus nette entre style, template, ressources et visual states dans la resolution runtime.

### Ce qui serait trop WPF-centric

- Un moteur de triggers complet avec `MultiTrigger`, `DataTrigger`, `EventTrigger`, `TriggerAction` et storyboards partout.
- Une propagation implicite trop large des styles a la WPF si cela brouille la predictibilite du runtime MGUI.
- Un investissement lourd dans `x:Name` source generation avant d'avoir regle la robustesse globale du loader et des erreurs de theme/template.

### Dette ou opportunite

- L'absence de `Style.BasedOn` et de reevaluation runtime plus riche est une opportunite realiste a court/moyen terme.
- L'absence de triggers est une dette potentielle seulement si MGUI veut aller vers une looklessness beaucoup plus profonde.
- Le chantier le plus rentable reste la consolidation de la frontiere style/theme/template, pas l'ajout massif de mecanismes WPF.

### Preuves

- `InkkSlinger/InkkSlinger.UI/UI/Styling/Core/Style.cs` -> `BasedOn`, settiers et application runtime.
- `InkkSlinger/InkkSlinger.UI/UI/Styling/Triggers/` -> large surface de triggers.
- `InkkSlinger/InkkSlinger.UI/UI/Templating/Core/ControlTemplate.cs` -> control template cote InkkSlinger.
- `InkkSlinger/InkkSlinger.UI/UI/Xaml/Core/XamlLoader.StylesTemplates.cs` -> parsing des styles/templates/triggers.
- `MGUI.Core/UI/XAML/Style.cs` -> styles simples cote MGUI.
- `MGUI.Core/UI/VisualState.cs` -> modele de visual states MGUI.
- `Docs/style-theme-architecture-audit.md` -> dettes et frontieres deja identifiees cote MGUI.
- `Docs/theme-definition-architecture.md` -> `ThemeDefinition` et heritage de themes cote MGUI.

## 5. Comparaison fonctionnelle

| Famille / Controle | InkkSlinger | MGUI | Evaluation | Notes |
|---|---|---|---|---|
| Base controls | Large surface WPF-like | Bonne base deja presente | Partiel | MGUI couvre deja beaucoup de besoins reellement frequents |
| Panels / containers | Tres large | Large et suffisante | Equivalent | Bonne parite sur les familles majeures |
| Buttons / toggles | Plus complet (`RepeatButton`, `Thumb`) | Surface plus resserree | Partiel | Peu prioritaire hors cas particuliers |
| Items controls | Tres large, y compris `DataGrid` | Bon coeur (`ListBox`, `ListView`, `ComboBox`, `TreeView`) | Partiel | `DataGrid` reste l'ecart le plus utile cote outils |
| Menus | Menu WPF-like complet | MenuBar et context menu orientee MGUI | Partiel | MGUI couvre deja l'essentiel |
| Input controls | Large, y compris date/couleur | Surface utile mais plus reduite | Partiel | `Calendar`/`DatePicker` possibles, mais pas priorite absolue |
| Text / rich text | `RichTextBox` complet | Pas d'equivalent riche | Absent | Fort ecart, mais a cadrer avant de lancer |
| Data grid | Natif et riche | Pas natif | Absent | Bon candidat pour une version lite outils/debug |
| Date / calendar | Oui | Non | Absent | Valeur reelle a confirmer par les usages MGUI |
| Documents / navigation | `Page`, `Frame`, `NavigationService`, `DocumentViewer` | Non | Absent | Trop desktop-centric par defaut |
| Shapes / primitives | Surface publique riche | Primitives backend, peu de wrappers UI | Partiel | L'opportunite la plus actionnable cote MGUI |
| Tooling UI | InkkOops, diagnostics, demo par controle | Samples et tooling plus legers | Absent | Ecart majeur en reproductibilite/debug |
| Controles specifiques InkkSlinger | Nombreux et WPF-like | N/A | Specifique | Beaucoup ne sont pas prioritaires pour MGUI |
| Controles specifiques MGUI | Plusieurs controles orientee jeu | N/A | Specifique | A conserver comme axe de differenciation |

### Constats

- Les fondamentaux de la construction d'UI sont deja bien couverts par MGUI.
- Les gros manques cote MGUI se concentrent sur quelques families precises: `RichTextBox`, `DataGrid`, shapes UI retenues, quelques controles date/couleur, et le tooling runtime.
- Plusieurs families presentes dans InkkSlinger sont objectivement riches mais peu pertinentes pour un framework UI temps reel de jeu (`DocumentViewer`, `Frame/Page`, modeles documentaires lourds).
- MGUI a deja une identite propre sur certains controles orientes jeu/outillage; il faut la proteger au lieu de tout aligner sur WPF.

### Preuves

- `InkkSlinger/UI-FOLDER-MAP.md` -> confirme la presence de `DataGrid`, `Calendar`, `DatePicker`, `RichTextBox`, `Popup`, `Page`, `Frame`, `Adorners`, etc.
- `InkkSlinger/InkkSlinger.DemoApp/Views/` -> demos granulaires par controle, dont `RichTextBoxView`, `CalendarView`, `DatePickerView`, `DataGridView`, `FrameView`.
- `InkkSlinger/InkkSlinger.UI/UI/Controls/Inputs/RichTextBox*.cs` -> sous-systeme riche de texte/document.
- `wpf-controls-gap-analysis.md` -> liste explicitement plusieurs families encore absentes cote MGUI.
- `MGUI.Core/UI/` -> surface MGUI actuelle et controles specifiques au repo.
- `MGUI.Shared/Rendering/DrawTransaction.cs` -> primitives de draw deja disponibles pour des shapes UI futures.

## 6. Maturite outillage et qualite

| Sujet | InkkSlinger | MGUI | Evaluation | Notes |
|---|---|---|---|---|
| Structure de tests | Large, multi-domaines, parity/regression | Suite plus petite et plus classique | Partiel | InkkSlinger surclasse MGUI en couverture visible |
| Couverture des regressions | Tres explicite | Plus limitee | Partiel | Opportunite forte pour MGUI |
| Demos / repros | Tres nombreuses, par controle | Moins granulaires | Partiel | Ecart important pour le debug |
| Documentation utilisateur | Publique et par controle | Quasi absente | Absent | Gros deficit cote MGUI |
| Documentation architecture | Oui | Oui, et riche | Equivalent | MGUI documente bien ses chantiers internes |
| Telemetry / diagnostics | Tres pousses | Faibles/locaux | Absent | Ecart majeur de robustesse |
| Automation runtime | Oui, via InkkOops | Non | Absent | Ecart majeur de reproductibilite |
| Experience developpeur | Plus guidee et plus outillee | Plus artisanale | Partiel | MGUI a besoin de meilleurs garde-fous |

### Constats

- L'ecart de maturite le plus net entre les deux repos n'est pas seulement fonctionnel; il est dans la qualite du support au developpement.
- InkkSlinger traite tests, demos, docs, telemetry et automation comme des produits de premier plan.
- MGUI a deja beaucoup de documentation technique interne, mais pas encore le meme niveau d'experience developpeur ou de reproductibilite.
- Le meilleur retour sur investissement pour MGUI est probablement ici, avant d'ajouter trop de surface fonctionnelle.

### Preuves

- `InkkSlinger/README.md` -> annonce 234 tests, 86 demos/repros, 122 pages de doc, 39 telemetry snapshot types.
- `InkkSlinger/InkkSlinger.Tests/` -> couverture large par domaine.
- `InkkSlinger/InkkOops.Cli/` -> CLI automation.
- `InkkSlinger/site/docs/` -> documentation publique.
- `MGUI.Tests/` -> suite de tests existante, mais sans equivalent visible a InkkOops.
- `MGUI.Samples/` -> demos et surfaces de validation cote MGUI.
- `Docs/` -> forte documentation d'architecture/migration cote MGUI.
- `MGUI.Core/Tooling/UIToolingService.cs` -> presence de tooling, mais surface plus limitee.

## 7. Conclusions et opportunites MGUI

MGUI n'a pas besoin de devenir un clone WPF. Sa meilleure trajectoire est de renforcer ce qu'il a deja de pertinent pour MonoGame et pour un runtime temps reel: decouplage backend/runtime, navigation input pragmatique, base lookless en cours de stabilisation, et une surface de controles/outils utile au jeu et aux editeurs. InkkSlinger reste une excellente source d'inspiration pour la discipline technique, la validation et la profondeur de certains sous-systemes, mais une partie importante de sa richesse reste trop desktop-centric ou trop couteuse a transposer telle quelle.

Une roadmap derivee de cette section est disponible dans `Docs/inkkslinger-vs-mgui-prioritized-roadmap.md`.

| Opportunite | Valeur | Cout / risque | Preconditions | Source d'inspiration |
|---|---|---|---|---|
| Harness de diagnostics et automation leger | Tres forte | Moyen | IDs stables, snapshot d'etat, artefacts simples | InkkOops |
| Consolidation style/theme/template autour de la precedence runtime | Tres forte | Moyen a eleve | Tests d'invalidation, refreshs de sous-arbre, templates plus stricts | Discipline lookless InkkSlinger |
| Validation markup plus stricte et diagnostics du loader | Forte | Moyen | Taxonomie d'erreurs, mode progressif | XML strict, validation compile-time |
| Matrice de samples, repros et docs par scenario | Forte | Faible a moyen | Host de demo stable, conventions de repro | DemoApp et docs publiques |
| Shapes retained: Ellipse, Line, Polygon, Polyline, Path-lite | Forte | Moyen | Poursuivre la separation shape/paint | Surface shapes InkkSlinger |
| DataGrid-lite outils/debug | Moyenne a forte | Moyen a eleve | Scope ferme: lecture/tri/selection avant edition riche | DataGrid InkkSlinger |
| Adorner-lite / overlay decorators | Moyenne | Moyen | Layering, clipping, hit testing bien bornes | AdornerLayer |
| Ameliorations textuelles ciblees avant tout RichTextBox complet | Moyenne | Moyen a eleve | Scope clair: chat/log/debug, pas document model complet | RichTextBox |

Etat d'avancement portefeuille:

- cette opportunite est maintenant couverte par une tranche compacte dans MGUI: `MGTextBlock` accepte des runs explicites, `MGTextLogView` couvre les feeds log/debug append-only, et `MGChatBox` expose un formatting inline opt-in pour les corps de messages ;
- le sample `SCN-TEXT-002` sert de repro et de demonstration pour ces trois usages reunis ;
- un vrai `RichTextBox` ou document editor reste volontairement hors scope tant qu'un besoin distinct de chat/log/debug n'est pas etabli.

### Top priorites proposees

1. Classe A - Outillage de robustesse: automation legere, replay d'input, captures d'etat et diagnostics structures.
2. Classe A - Consolidation lookless runtime: precedence de valeurs, themes herites, control templates, invalidation et refresh deterministes.
3. Classe B - Validation markup plus stricte et diagnostics de chargement nettement meilleurs.
4. Classe B - Controles de shapes retained au-dessus des primitives deja disponibles.
5. Classe B - DataGrid-lite oriente outils/debug, avant toute tentative de parite WPF plus lourde.

### Ce qu'il ne faut probablement pas copier tel quel

- Un property system complet facon `DependencyObject`/`DependencyProperty` sur tout le framework.
- Une separation exhaustive arbre logique / arbre visuel si les gains concrets ne sont pas demontres pour MGUI.
- Un moteur de triggers WPF complet en une seule etape.
- Un pipeline retained + dirty regions tres sophistique par defaut sans profilage reel cote MonoGame.
- Une surface generalisee de routed commands et routed events pour tous les flux gameplay/UI.
- Les families les plus desktop-centric: `FlowDocument`, `DocumentViewer`, `Ribbon`, `MediaElement`, `Frame/Page` complets, `Calendar/DatePicker` riches sans demande forte.
- L'ampleur initiale d'InkkOops. Il faut reprendre le principe, pas la totalite de la surface.

### Risques de mauvaise interpretation

- Confondre primitives de draw backend avec controles UI retained effectivement exposes.
- Lire `ThemeDefinition.BasedOn` comme l'equivalent de `Style.BasedOn` WPF.
- Interpreter `UIValuePrecedence` et `VisualState` comme la presence d'un property system reactif complet.
- Supposer que dirty regions et retained rendering complexes seront automatiquement rentables en environnement jeu.
- Utiliser le nombre de controles InkkSlinger comme objectif produit au lieu de prioriser les usages MGUI reellement utiles.
- Sous-estimer le cout de maintenance de l'automation, de la telemetry et de la validation stricte.
- Sur-prioriser `RichTextBox`, `Calendar` ou `Frame/Page` avant d'avoir regle les sujets transverses de qualite.

## 8. Zones incertaines a confirmer

- Le besoin reel cote MGUI d'un `Calendar`/`DatePicker` reste a confirmer par les usages cibles.
- Le besoin d'un vrai `RichTextBox` MGUI reste a justifier separement des usages chat/log/debug, maintenant couverts par les surfaces textuelles compactes livrees.
- Le ROI d'un `DataGrid` natif doit etre valide contre un `ListView`/`Grid` ameliores.
- Le degre de sophistication utile pour un futur harness automation MGUI doit etre borne avant implementation.
- La rentabilite de dirty regions ou d'un retained rendering plus pousse doit etre profilee sur des scenes representatives MGUI.

## 9. Annexes

### Lexique de statut

- `Equivalent`: meme capacite a profondeur proche.
- `Partiel`: capacite presente mais plus limitee ou moins robuste.
- `Absent`: capacite non presente a ce stade.
- `Specifique`: capacite propre a l'un des frameworks, sans equivalent direct.
- `Inconcluant`: evidence insuffisante.

### Format de preuve recommande

- `chemin -> ce que cela demontre`
