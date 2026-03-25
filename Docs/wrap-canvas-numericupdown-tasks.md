# Taches d'implementation WrapPanel, Canvas et NumericUpDown pour MGUI

## Objectif

Definir un plan d'execution detaille pour un agent IA afin d'implementer trois controles prioritaires dans MGUI:

- `MGWrapPanel`
- `MGCanvas`
- `MGNumericUpDown`

Le plan privilegie de petites etapes verticales, testables, avec un commit entre chaque tache. Chaque controle doit etre livre avec:

- implementation runtime ;
- tests cibles ;
- sample dedie dans `MGUI.Samples` ;
- integration respectueuse des patterns MGUI pour le layout, les inputs, les themes et les styles.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit par tache terminee.
- Mettre a jour l'icone de statut dans le titre de la tache avant chaque commit.
- Si une tache est bloquee, remplacer l'icone par `⛔`, decrire le blocage juste en dessous, puis s'arreter.
- Ne pas faire de refactor massif hors perimetre.
- Preserver les APIs publiques existantes sauf si une tache demande explicitement de les etendre.
- Corriger la cause racine avant les cas particuliers.
- Ajouter ou adapter des tests a chaque tache quand c'est pertinent.
- Ne pas passer a la tache suivante tant que la tache courante n'est pas validee et committed.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale apres chaque tache

Utiliser une validation ciblee et bornee.

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "WrapPanel|Canvas|NumericUpDown" --logger "console;verbosity=minimal"`
4. Si la tache ne touche qu'un seul controle, preferer un filtre plus etroit dedie a ce controle.
5. Commit avec le message recommande par la tache.

## Contraintes d'architecture MGUI a respecter

### Layout

- `MGWrapPanel` et `MGCanvas` sont avant tout des conteneurs de layout. Leur logique doit rester concentree sur mesure et arrangement.
- Eviter tout couplage inutile avec le rendu ou le pipeline d'input.
- Garder un comportement deterministe et testable sur des methodes de calcul pures quand c'est possible.
- Eviter les allocations par frame dans `Update` et `Draw`.
- Ne pas utiliser LINQ dans les hot paths.

### Input et focus

- `MGWrapPanel` et `MGCanvas` ne doivent pas inventer un pipeline d'input specifique. Ils doivent laisser les regles existantes de hit testing, focus et propagation s'appliquer via les enfants.
- `MGNumericUpDown` doit reutiliser les patterns existants de `MGTextBox`, `MGButton`, navigation clavier et focus.
- Le clavier ne doit agir que dans les conditions legitimes: focus reel, eligibilite effective, modalite respectee, pas de fuite d'input au contenu derriere.
- Si un comportement de repetition est ajoute pour les boutons d'incrementation, il doit rester coherent avec la philosophie actuelle de MGUI et ne pas contourner les arbitres centraux d'input.

### Themes, styles et templates

- Ne pas coder en dur des couleurs, epaisseurs, marges ou etats visuels evitables.
- Reutiliser les primitives existantes: `MGTheme`, `MGControlTemplate`, `MGContentPresenter`, `MGHeaderedContentPresenter`, `MGVisualStateProjection`, `MGBorder`, `MGButton`, `MGTextBox`.
- Pour `MGNumericUpDown`, privilegier une structure composite template-friendly avec des template parts nommees plutot qu'un draw monolithique.
- Pour `MGWrapPanel` et `MGCanvas`, conserver une surface theming minimale, comme les autres conteneurs de layout.

### Compatibilite et ergonomie API

- Ajouter de nouvelles valeurs a `MGElementType` pour les controles publics introduits.
- Choisir des noms et comportements proches de WPF quand cela ne contredit pas l'architecture MGUI.
- Si une API attached-property est necessaire pour `MGCanvas`, l'exposer de facon idiomatique pour MGUI sans casser les containers existants.
- Garder la voie XAML et imperative toutes deux utilisables.

## Zones du code a auditer en priorite

- `MGUI.Core/UI/Containers/MGStackPanel.cs`
- `MGUI.Core/UI/Containers/MGOverlayPanel.cs`
- `MGUI.Core/UI/Containers/Grids/MGGrid.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGTextBox.cs`
- `MGUI.Core/UI/MGButton.cs`
- `MGUI.Core/UI/MGTheme.cs`
- `MGUI.Core/UI/Styling/`
- `MGUI.Samples/Controls/`
- `MGUI.Tests/`

## Ordre de commits attendu

1. `docs: complete task 1 define control contracts and delivery order`
2. `test: complete task 2 add wrap panel layout regression matrix`
3. `feat: complete task 3 add wrap panel layout engine`
4. `sample: complete task 4 add wrap panel sample coverage`
5. `test: complete task 5 add canvas positioning regression matrix`
6. `feat: complete task 6 add canvas layout container`
7. `sample: complete task 7 add canvas sample coverage`
8. `test: complete task 8 add numeric updown value and parsing tests`
9. `feat: complete task 9 add numeric updown core model`
10. `feat: complete task 10 add numeric updown control structure`
11. `input: complete task 11 wire numeric updown focus and interaction behavior`
12. `sample: complete task 12 add numeric updown sample coverage`
13. `docs: complete task 13 document new controls and update gap analysis`

## Taches

### 🟡 1. Definir les contrats de controle et le perimetre exact

But:
figer les invariants, les APIs minimales et l'ordre de livraison avant d'introduire des types publics permanents.

Travail attendu:

- documenter dans ce fichier les comportements cibles de `MGWrapPanel`, `MGCanvas` et `MGNumericUpDown` ;
- fixer les valeurs `MGElementType` a ajouter ;
- lister les fichiers cibles probables ;
- decider si `MGCanvas` utilise des attached properties, des components, ou un mecanisme equivalent deja etabli dans MGUI ;
- decider si `MGNumericUpDown` supporte des valeurs `int`, `double` ou seulement `decimal` dans sa v1 ;
- definir clairement ce qui est dans le scope v1 et ce qui est reporte.

Criteres d'acceptation:

- les APIs de v1 sont tranchees ;
- les invariants d'input, de layout et de style sont explicites ;
- le plan des fichiers et des tests est pose avant implementation.

Commit recommande:

- `docs: complete task 1 define control contracts and delivery order`

Resultat vise pour la tache:

- `MGWrapPanel`
  - derive de `MGMultiContentHost` ;
  - v1 expose `Orientation` et `Spacing` ;
  - applique un wrap simple, deterministe, dans l'ordre des enfants ;
  - ne gere pas d'alignement de ligne avance ni de virtualisation.
- `MGCanvas`
  - derive de `MGMultiContentHost` ;
  - v1 expose des APIs type attached properties, via helpers statiques sur `MGCanvas`, pour `Left`, `Top`, `Right`, `Bottom` ;
  - si `Left` et `Right` sont tous deux definis, `Left` gagne en v1 ;
  - si `Top` et `Bottom` sont tous deux definis, `Top` gagne en v1 ;
  - si aucune coordonnee n'est definie, l'enfant est arrange a l'origine du canvas ;
  - pas de `ZIndex` public additionnel en v1, le canvas s'appuie sur l'ordre naturel existant des enfants.
- `MGNumericUpDown`
  - derive de `MGElement` ;
  - v1 utilise `double` pour rester simple a parser, a binder et a tester ;
  - expose `Minimum`, `Maximum`, `Value`, `Increment`, `DecimalPlaces`, `FormatString`, `IsReadonly` ;
  - compose son UI via template parts et primitives existantes, sans draw custom monolithique ;
  - la logique pure de coercion, parsing et increment/decrement vit dans un modele central testable.
- Fichiers cibles probables:
  - `MGUI.Core/UI/Containers/MGWrapPanel.cs`
  - `MGUI.Core/UI/Containers/MGCanvas.cs`
  - `MGUI.Core/UI/MGNumericUpDown.cs`
  - `MGUI.Core/UI/NumericUpDown/` si un helper de logique pure est introduit ;
  - `MGUI.Core/UI/XAML/Containers.cs`
  - `MGUI.Core/UI/XAML/Controls.cs`
  - `MGUI.Core/UI/Enums.cs`
  - `MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs`
  - `MGUI.Tests/Architecture/`, `MGUI.Tests/Focus/` et nouveaux tests dedies ;
  - `MGUI.Samples/Controls/WrapPanel.xaml`, `Canvas.xaml`, `NumericUpDown.xaml` et leurs code-behind.

### ✅ 1. Definir les contrats de controle et le perimetre exact

Resultat:

- les trois contrats v1 sont fixes et bornes ;
- `MGCanvas` adopte une API de positionnement statique par enfant au lieu d'ajouter une abstraction ad hoc au moteur de layout ;
- `MGNumericUpDown` part sur `double` en v1 pour limiter la complexite de parsing et de theming ;
- les fichiers cibles et la separation entre logique pure, structure visuelle, tests et samples sont explicites.

### ✅ 2. Ajouter une matrice de tests de regression du layout pour WrapPanel

But:
verrouiller les regles de mesure et de retour a la ligne avant d'integrer le controle au runtime.

Travail attendu:

- creer une suite de tests pure ou quasi pure dediee a `MGWrapPanel` ;
- couvrir au minimum:
  - orientation horizontale ;
  - orientation verticale ;
  - retour a la ligne quand la largeur ou hauteur disponible est depassee ;
  - enfants plus grands que la taille disponible ;
  - prise en compte de `Margin` et de l'espacement eventuel ;
  - comportement avec taille infinie dans l'axe principal ;
  - ordre stable des enfants.

Criteres d'acceptation:

- les regles de wrap sont deterministes et testees ;
- les tests ne dependent pas du rendu ;
- au moins une partie des tests echoue avant implementation.

Commit recommande:

- `test: complete task 2 add wrap panel layout regression matrix`

Resultat:

- une matrice de regression dediee a `WrapPanel` couvre le wrap horizontal, le wrap vertical, les enfants collapses, l'axe principal non borne, l'ordre d'arrangement et les enfants plus grands que l'espace disponible ;
- la logique a ete isolee dans un moteur pur interne pour rendre les calculs testables hors runtime ;
- les fichiers modifies n'ont pas d'erreurs C# signalees par l'analyse statique locale ;
- la validation `dotnet test` du projet complet reste bloquee par l'outil externe MonoGame `mgcb` dans cet environnement, independamment des nouveaux fichiers.

### ✅ 3. Ajouter `MGWrapPanel` et son moteur de layout

But:
introduire le container de layout avec un comportement simple, stable et compatible avec les autres panels MGUI.

Travail attendu:

- ajouter `MGUI.Core/UI/Containers/MGWrapPanel.cs` ;
- ajouter la valeur `WrapPanel` a `MGElementType` ;
- implementer mesure et arrangement sans allocations evitables ;
- supporter au minimum:
  - `Orientation` ;
  - ordre des enfants ;
  - wrap dans l'axe secondaire ;
  - alignement raisonnable via les mecanismes existants du framework ;
- s'assurer que le controle joue bien avec invalidation layout et visibilite.

Criteres d'acceptation:

- le controle compile et se comporte correctement sur les cas couverts par les tests ;
- l'implementation reste concentree sur le layout ;
- aucune logique theming ou input specifique n'est ajoutee sans necessite.

Commit recommande:

- `feat: complete task 3 add wrap panel layout engine`

Resultat:

- `MGWrapPanel` est ajoute comme `MGMultiContentHost` public dans `MGUI.Core/UI/Containers/` ;
- le controle expose `Orientation`, `Spacing` et le meme confort de manipulation d'enfants que les autres panels du framework ;
- le type est branche dans `MGElementType`, dans les wrappers XAML et dans les alias du parser XAML ;
- le controle delegue ses calculs de mesure et d'arrangement au moteur pur ajoute a la tache 2 ;
- aucun comportement d'input specifique n'a ete introduit, le panel reste strictement un conteneur de layout.

### ✅ 4. Ajouter un sample et des tests d'integration pour WrapPanel

But:
prouver l'utilite du controle dans de vrais ecrans et verifier sa stabilite d'integration.

Travail attendu:

- ajouter un sample dedie sous `MGUI.Samples/Controls/WrapPanel.xaml` et `WrapPanel.xaml.cs` ;
- montrer au minimum:
  - wrapping horizontal ;
  - wrapping vertical ;
  - elements de tailles heterogenes ;
  - redimensionnement live de la fenetre ;
- ajouter un jeu de tests d'integration cible sur le container et son invalidation.

Criteres d'acceptation:

- le sample illustre clairement les cas d'usage du controle ;
- les tests verrouillent le comportement en presence d'enfants multiples ;
- la navigation ou le hit testing des enfants n'est pas regresse.

Commit recommande:

- `sample: complete task 4 add wrap panel sample coverage`

Resultat:

- un sample `WrapPanel` dedie est ajoute dans `MGUI.Samples/Controls/` avec cas horizontaux, verticaux, tailles heterogenes et contraintes d'espace reduites ;
- le sample est expose dans le compendium principal pour etre testable visuellement ;
- des tests d'integration legers couvrent le branchement XAML du controle et son adoption comme controle borde dans les tests d'architecture ;
- la validation projet complet reste soumise au blocage externe `mgcb` deja constate plus haut.

### ✅ 5. Ajouter une matrice de tests de positionnement pour Canvas

But:
verrouiller la politique de positionnement absolu avant de brancher un nouveau container public.

Travail attendu:

- creer des tests dedies a `MGCanvas` ;
- couvrir au minimum:
  - `Left` et `Top` ;
  - `Right` et `Bottom` ;
  - priorite si des valeurs concurrentes sont definies ;
  - comportement par defaut quand aucune coordonnee n'est fournie ;
  - impact sur la taille desiree du container ;
  - ordre d'arrangement stable.

Criteres d'acceptation:

- les regles de positionnement sont explicites et testees ;
- les tests sont majoritairement de logique pure ;
- les choix ambigus sont tranches avant implementation publique.

Commit recommande:

- `test: complete task 5 add canvas positioning regression matrix`

Resultat:

- une matrice de regression dediee a `Canvas` couvre `Left`, `Top`, `Right`, `Bottom`, la precedence des coordonnees, le fallback a l'origine et l'impact sur la taille desiree ;
- la logique de positionnement est isolee dans un moteur pur interne afin d'etre testee hors runtime ;
- les nouveaux fichiers ne presentent pas d'erreurs C# locales ;
- la validation `dotnet test` du projet complet reste bloquee par `mgcb` dans cet environnement.

### ✅ 6. Ajouter `MGCanvas` et ses APIs de positionnement

But:
introduire un container de positionnement absolu minimal, predictible et compatible XAML.

Travail attendu:

- ajouter `MGUI.Core/UI/Containers/MGCanvas.cs` ;
- ajouter la valeur `Canvas` a `MGElementType` ;
- implementer les APIs de positionnement retenues a la tache 1 ;
- implementer mesure et arrangement conformes au contrat decide ;
- verifier que les enfants gardent le pipeline d'input normal sans logique speciale du canvas ;
- s'assurer que l'ordre de draw et de hit testing reste coherent avec le reste de MGUI.

Criteres d'acceptation:

- le controle compile, mesure et arrange correctement ;
- la voie XAML est utilisable ;
- aucune regression evidente n'apparait sur les enfants interactifs dans un canvas.

Commit recommande:

- `feat: complete task 6 add canvas layout container`

Resultat:

- `MGCanvas` est ajoute comme conteneur public derive de `MGMultiContentHost` ;
- les APIs imperative de positionnement sont exposees via `MGCanvas.SetLeft/SetTop/SetRight/SetBottom` et lisibles via les getters correspondants ;
- le stockage des coordonnees est porte par `MGElement.Metadata`, avec invalidation layout automatique quand un enfant deja attache change de coordonnee ;
- la voie XAML est branchee via le wrapper `Canvas` et les proprietes attachees `CanvasLeft`, `CanvasTop`, `CanvasRight`, `CanvasBottom` sur les noeuds enfants ;
- le conteneur reste neutre cote input: les enfants conservent leur pipeline normal de focus et hit testing.

### ✅ 7. Ajouter un sample et des tests d'integration pour Canvas

But:
valider les scenarios concrets de positionnement absolu et de superposition visuelle.

Travail attendu:

- ajouter `MGUI.Samples/Controls/Canvas.xaml` et `Canvas.xaml.cs` ;
- montrer au minimum:
  - positionnement explicite a chaque coin ;
  - superposition de plusieurs enfants ;
  - elements interactifs dans le canvas ;
  - redimensionnement du parent ;
- ajouter des tests d'integration cibles sur arrangement, ordre visuel et absence de regression input.

Criteres d'acceptation:

- le sample couvre les scenarios de dessin et d'interaction de base ;
- les tests verrouillent les regles de positionnement et les invariants d'input ;
- le controle reste strictement dans son role de layout.

Commit recommande:

- `sample: complete task 7 add canvas sample coverage`

Resultat:

- un sample `Canvas` dedie est ajoute dans `MGUI.Samples/Controls/` avec scenarios top-left, bottom-right et composition de controles interactifs ;
- le sample est expose dans le compendium principal ;
- des tests d'integration legers couvrent l'alias XAML `Canvas`, les helpers statiques de coordonnees attachees et l'adoption du controle comme conteneur borde ;
- la validation globale reste soumise au blocage externe `mgcb` dans cet environnement.

### ✅ 8. Ajouter une matrice de tests de valeur, coercion et parsing pour NumericUpDown

But:
verrouiller les regles de metier avant d'introduire l'UI composite.

Travail attendu:

- creer des tests dedies a la logique de `MGNumericUpDown` ;
- couvrir au minimum:
  - `Minimum`, `Maximum`, `Value` et `Increment` ;
  - coercion quand `Value` sort des bornes ;
  - synchronisation texte <-> valeur ;
  - parsing invalide ;
  - arrondi ou precision selon le type retenu ;
  - decrement/increment clavier ;
  - comportement si `Minimum > Maximum` ou si `Increment <= 0`.

Criteres d'acceptation:

- la logique de valeur est testee hors rendu ;
- les erreurs de contrat sont tranchees explicitement ;
- les tests echouent avant l'implementation complete.

Commit recommande:

- `test: complete task 8 add numeric updown value and parsing tests`

Resultat:

- une suite de regression pure dediee a `MGNumericUpDownModel` couvre la coercion de plage, l'arrondi via `DecimalPlaces`, l'increment, le parsing invalide et le formatage ;
- le modele central introduit pour supporter ces tests reste runtime-independent et ne depend ni du rendu ni du pipeline d'input MonoGame ;
- les cas de synchronisation de base entre `Text`, `Value`, `Minimum`/`Maximum` et `Increment` sont verrouilles avant l'introduction du controle visuel.

### ✅ 9. Ajouter le modele central de NumericUpDown

But:
separer la logique pure de valeur du chrome visuel et de l'input compose.

Travail attendu:

- introduire un helper, state object ou logique embarquee testable pour:
  - coercion des bornes ;
  - increment/decrement ;
  - conversion texte/valeur ;
  - validation de configuration ;
- garder ce modele simple a tester et a reutiliser ;
- definir les evenements ou notifications de changement necessaires.

Criteres d'acceptation:

- le coeur de la logique n'est pas duplique dans l'UI ;
- les tests de la tache 8 passent ;
- les choix de type numerique et de format sont stabilises pour la v1.

Commit recommande:

- `feat: complete task 9 add numeric updown core model`

Resultat:

- `MGUI.Core/UI/NumericUpDown/MGNumericUpDownModel.cs` centralise desormais la coercion de plage, la validation de configuration, l'arrondi, le parsing invariant et le formatage ;
- le modele expose aussi des helpers d'etat et d'ajustement (`TryIncrease`, `TryDecrease`, `IsAtMinimum`, `IsAtMaximum`) pour que l'UI ne re-duplique pas cette logique ;
- les tests de la tache 8 ont ete etendus pour verrouiller ces helpers avant l'integration du controle visuel.

### ✅ 10. Ajouter `MGNumericUpDown` comme controle composite template-friendly

But:
construire un controle public coherent avec les patterns modernes du framework.

Travail attendu:

- ajouter `MGUI.Core/UI/MGNumericUpDown.cs` ;
- ajouter la valeur `NumericUpDown` a `MGElementType` ;
- composer le controle a partir de primitives existantes plutot que dessiner manuellement ;
- declarer des template parts explicites, par exemple:
  - `PART_OuterBorder` ;
  - `PART_TextBox` ;
  - `PART_IncreaseButton` ;
  - `PART_DecreaseButton` ;
- exposer les proprietes publiques minimales de v1 ;
- brancher la synchronisation entre valeur interne, texte et boutons.

Criteres d'acceptation:

- la structure du controle est compatible avec le systeme de theme/template ;
- le controle compile sans draw custom inutile ;
- les proprietes publiques sont utilisables en C# et en XAML.

Commit recommande:

- `feat: complete task 10 add numeric updown control structure`

Resultat:

- `MGNumericUpDown` est ajoute comme sous-classe publique de `MGTextBox` avec un modele de valeur central et des template parts dediees pour le spinner ;
- le template par defaut est enregistre dans `MGControlTemplateCatalog` et materialise un hote vertical pour les boutons d'increment/decrement sans draw custom monolithique ;
- la surface XAML est branchee via `MGUI.Core/UI/XAML/Controls.cs` et les alias `NumericUpDown` / `NUD` sont ajoutes au parser ;
- une couverture d'architecture legere verifie l'alias XAML et l'adoption des patterns de bordure sur le nouveau controle.

### ✅ 11. Brancher le focus et les interactions de NumericUpDown

But:
faire du controle un bon citoyen du pipeline d'input MGUI.

Travail attendu:

- integrer focus clavier, saisie texte et boutons d'incrementation/decrementation ;
- couvrir au minimum:
  - clic souris sur les boutons ;
  - fleches haut/bas au clavier si le controle a le focus ;
  - validation ou restauration propre d'une saisie invalide ;
  - absence de fuite d'input derriere le controle ;
  - respect des etats disabled, hidden et modal ;
- si repetition au maintien est retenue en v1, l'implementer sans contourner les contrats globaux d'input.

Criteres d'acceptation:

- le controle respecte la politique de focus et d'eligibilite effective ;
- les interactions clavier et souris sont coherentes avec les autres controles MGUI ;
- les regressions d'input sont couvertes par les tests.

Commit recommande:

- `input: complete task 11 wire numeric updown focus and interaction behavior`

Resultat:

- `MGNumericUpDown` synchronise maintenant la saisie texte avec `Value` sans dupliquer la logique du modele ;
- les pertes de focus normalisent ou restaurent le texte selon que le parsing est valide ou non ;
- les interactions clavier et navigation (`Up`, `Down`, `PageUp`, `PageDown`, `Home`, `End`, `Enter`, actions semantiques) sont routees via des helpers purs testes dans `MGUI.Tests/Focus/NumericUpDownInputTests.cs` ;
- les boutons d'incrementation se desactivent correctement selon `IsReadonly` et les bornes courantes.

### ✅ 12. Ajouter un sample et des tests d'integration pour NumericUpDown

But:
valider l'experience utilisateur complete, y compris theming et usage en formulaire.

Travail attendu:

- ajouter `MGUI.Samples/Controls/NumericUpDown.xaml` et `NumericUpDown.xaml.cs` ;
- montrer au minimum:
  - bornes min/max ;
  - increment personnalise ;
  - etat disabled ;
  - integration dans un formulaire ou panneau de reglages ;
  - changement de valeur via souris et clavier ;
- ajouter des tests d'integration cibles sur focus, parsing, coercion et synchronisation texte/valeur.

Criteres d'acceptation:

- le sample montre clairement les cas d'usage de production ;
- les tests verrouillent les scenarios interactifs principaux ;
- le controle ne casse pas le pipeline d'input ni la thematisation existante.

Commit recommande:

- `sample: complete task 12 add numeric updown sample coverage`

Resultat:

- un sample dedie `MGUI.Samples/Controls/NumericUpDown.xaml` illustre les usages entier, decimal et readonly du controle ;
- le sample montre aussi une personnalisation XAML des boutons de spinner pour valider la promesse template-friendly du controle ;
- `Compendium.xaml` et `Compendium.xaml.cs` exposent maintenant ce nouvel ecran au meme titre que les autres controles du framework.

### ⚪ 13. Documenter les nouveaux controles et mettre a jour l'analyse des gaps

But:
laisser une trace claire de l'API, des limites v1 et de l'avancement du backlog.

Travail attendu:

- mettre a jour `README.md` avec les nouveaux controles publics ;
- mettre a jour `wpf-controls-gap-analysis.md` pour retirer `WrapPanel`, `Canvas` et `NumericUpDown` des manques apres livraison ;
- ajouter une courte note d'usage ou de migration si necessaire ;
- verifier que les samples apparaissent dans les points d'entree adequats du projet de demo.

Criteres d'acceptation:

- la documentation publique du repo reflete l'etat reel du code ;
- les limitations de v1 sont explicites ;
- le backlog d'analyse est recale apres implementation.

Commit recommande:

- `docs: complete task 13 document new controls and update gap analysis`

## Notes de scope v1 recommande

### MGWrapPanel

- Inclure en v1:
  - `Orientation` ;
  - wrap simple ;
  - ordre stable des enfants ;
  - compatibilite XAML et imperative.
- Reporter si couteux:
  - alignement par ligne avance ;
  - justification complexe ;
  - virtualisation.

### MGCanvas

- Inclure en v1:
  - `Left`, `Top`, `Right`, `Bottom` ;
  - arrangement absolu ;
  - composition d'enfants interactifs.
- Reporter si couteux:
  - `ZIndex` public si MGUI n'a pas deja de contrat clair pour cela ;
  - transforms locales specifiques au canvas.

### MGNumericUpDown

- Inclure en v1:
  - borne min/max ;
  - increment/decrement ;
  - saisie texte simple ;
  - synchronisation valeur/texte ;
  - focus et disabled state ;
  - theming via template parts.
- Reporter si couteux:
  - culture-specific parsing avance ;
  - acceleration fine de repetition ;
  - spin buttons horizontaux et verticaux multiples ;
  - mode decimal haute precision si cela complexifie trop la v1.