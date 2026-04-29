# RichTextBox Editor Agent Tasks

## Objectif

Definir un plan d'execution pour des agents IA afin d'ajouter une surface `MGRichTextBox` orientee editeur dans MGUI, puis de l'exposer dans le sample avec:

- edition multi-ligne ;
- coloration syntaxique ;
- autocompletion ;
- integration au theme editeur existant ;
- validation par tests et sample.

La cible immediate est d'ameliorer l'editeur WPF-like contenu dans `MGUI.Samples`, en particulier autour de `MGUI.Samples/Features/EditorDarkThemePreview.xaml`. Ce plan ne vise pas a importer le controle desktop `System.Windows.Controls.RichTextBox`, mais a livrer un controle MGUI utilisable dans MonoGame.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Faire exactement 1 commit git apres chaque tache terminee.
- Mettre a jour l'icone de statut dans le titre de la tache avant chaque commit.
- Utiliser `🟡` des le debut d'une tache, puis `✅` juste avant le commit si la tache est validee.
- Si une tache est bloquee, remplacer l'icone par `⛔`, decrire le blocage juste sous le titre, puis s'arreter sans passer a la suite.
- Ne pas commencer la tache suivante tant que la tache courante n'est pas validee et committed.
- Ne pas faire de refactor massif hors perimetre.
- Preserver les APIs publiques existantes. Si une signature publique doit changer, garder une compatibilite avec overload ou API obsolete qui forward.
- Ajouter ou adapter les tests a chaque tache quand la logique est testable.
- Eviter les allocations par frame dans `Update` et `Draw`; eviter LINQ dans les hot paths.
- Garder l'input, le layout et le rendu separes.
- Documenter dans la section `Resultat` de chaque tache ce qui a ete livre, les validations lancees et le commit effectue.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Etat de depart observe

- `EditorDarkThemePreview` charge les assets `CasaEditor.Dark` et montre des controles representatifs, mais ne contient pas encore de vraie surface d'edition de code.
- `MGTextBox` couvre deja l'edition de texte simple, la selection, le caret, l'undo/redo et les changements de texte.
- `MGTextBox` encode aujourd'hui la selection en markdown injecte dans son `MGTextBlock`, ce qui n'est pas une base suffisante pour des styles par plages, une coloration incrementalement recalculable et une autocompletion propre.
- `MGTextBlock.SetTextRuns(...)`, `MGTextLogView` et `MGChatBox.AllowsMessageInlineFormatting` couvrent les usages chat/log/debug, pas un vrai document editor.
- `wpf-controls-gap-analysis.md` identifie `RichTextBox` comme controle texte avance manquant.

## Cible v1

La v1 doit rester bornee et utile pour un editeur d'outils:

- un controle public `MGRichTextBox` ou nom equivalent valide en tache 1 ;
- stockage de texte brut editable, multi-ligne ;
- mapping stable entre index texte, ligne/colonne et positions layout ;
- spans de style non destructifs, separes du texte source ;
- selection, caret, edition clavier, clipboard et undo/redo au niveau attendu d'un `TextBox` multi-ligne ;
- coloration syntaxique via service injectable ;
- autocompletion via provider injectable ;
- popup de completion navigable au clavier et a la souris ;
- sample editeur dans `MGUI.Samples` avec theme editeur, texte de depart, coloration et completions demonstrables.

## Hors perimetre v1

- parite WPF `FlowDocument` complete ;
- RTF/HTML import-export ;
- images inline editables ;
- tableaux, paragraphes riches, pagination ou zoom document ;
- moteur de langage complet type LSP ;
- analyse semantique C# exhaustive ;
- virtualisation avancee pour fichiers tres volumineux ;
- IME avance, spellcheck, accessibility complete.

## Fichiers cibles probables

- `MGUI.Core/UI/MGRichTextBox.cs`
- `MGUI.Core/UI/TextEditing/`
- `MGUI.Core/UI/Text/TextRenderInfo.cs`
- `MGUI.Core/UI/MGTextBlock.cs`
- `MGUI.Core/UI/XAML/Controls.cs`
- `MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs`
- `MGUI.Samples/Features/EditorRichTextBox.xaml`
- `MGUI.Samples/Features/EditorRichTextBox.xaml.cs`
- `MGUI.Samples/Compendium.xaml`
- `MGUI.Samples/Compendium.xaml.cs`
- `MGUI.Tests/Text/`
- `MGUI.Tests/Input/`
- `MGUI.Tests/Architecture/`

## Validation minimale apres chaque tache

Adapter les filtres au contenu exact de la tache, mais garder une validation bornee.

1. `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "RichTextBox|TextEditing|Syntax|Completion|Editor" --logger "console;verbosity=minimal"`
4. Pour une tache purement documentaire, remplacer les builds par une verification de coherence des liens et chemins cites.

## Ordre de commits attendu

1. `docs: complete task 1 define rich textbox editor scope`
2. `test: complete task 2 add text buffer range tests`
3. `feat: complete task 3 add text editing model`
4. `refactor: complete task 4 share textbox editing helpers`
5. `feat: complete task 5 add rich textbox shell`
6. `feat: complete task 6 render styled text spans`
7. `input: complete task 7 wire rich textbox editing behavior`
8. `feat: complete task 8 add syntax highlighting services`
9. `feat: complete task 9 add completion model services`
10. `ui: complete task 10 add completion popup`
11. `sample: complete task 11 add editor rich textbox sample`
12. `docs: complete task 12 document rich textbox editor v1`
13. `test: complete task 13 stabilize editor scenarios`

## Taches

### ✅ 1. Definir le contrat RichTextBox editeur v1

But:
figer le perimetre public avant d'ajouter des types durables.

Travail attendu:

- auditer `MGTextBox`, `MGTextBlock`, `TextRenderInfo`, les samples texte et le sample `EditorDarkThemePreview` ;
- choisir le nom public final: `MGRichTextBox`, `MGCodeEditor`, ou `MGRichTextBox` avec services editeur optionnels ;
- definir les proprietes v1: `Text`, `IsReadonly`, `AcceptsTab`, `TabSize`, `SyntaxHighlighter`, `CompletionProvider`, `ShowLineNumbers`, `CurrentSelection`, `CaretIndex` ;
- definir les events v1: `TextChanging`, `TextChanged`, `SelectionChanged`, `CompletionRequested`, `CompletionAccepted` si necessaires ;
- trancher la strategie d'integration: nouveau controle avec modele d'edition partage, ou extension prudente de `MGTextBox` ;
- expliciter ce qui reste hors scope de la v1.

Criteres d'acceptation:

- le contrat public v1 est documente dans cette tache ;
- les fichiers cibles sont confirmes ou ajustes ;
- la separation texte brut, spans de style, input, popup et sample est explicite ;
- aucun code runtime public permanent n'est ajoute sans contrat clair.

Validation recommandee:

- verification documentaire du plan et des chemins cites.

Commit recommande:

- `docs: complete task 1 define rich textbox editor scope`

Resultat:

- Nom public retenu: `MGRichTextBox`, avec une orientation editeur activee par services optionnels plutot qu'un controle separe `MGCodeEditor`.
- Strategie retenue: nouveau controle public adosse a un modele texte partageable dans `MGUI.Core/UI/TextEditing/`; `MGTextBox` reste compatible et ne doit recevoir que de petites extractions de helpers stables.
- Contrat v1 confirme:
	- `Text`, `IsReadonly`, `AcceptsTab`, `TabSize`, `ShowLineNumbers`, `CaretIndex`, `CurrentSelection` ;
	- `SyntaxHighlighter` pour produire des spans de style non destructifs ;
	- `CompletionProvider` pour produire des suggestions et operations d'acceptation ;
	- events `TextChanging`, `TextChanged`, `SelectionChanged`, `CompletionRequested`, `CompletionAccepted` si les taches runtime les rendent necessaires.
- Separation confirmee: texte brut et ranges dans le modele, styles syntaxiques dans un service injectable, rendu de spans dans le controle, popup de completion dans une couche UI dediee, sample dans `MGUI.Samples/Features/EditorRichTextBox.*`.
- Hors scope v1 confirme: `FlowDocument`, RTF/HTML, images inline editables, LSP complet, virtualisation avancee et parite WPF exhaustive.
- Validation documentaire effectuee: chemins et contraintes du plan verifies contre `MGTextBox`, `MGTextBlock`, `TextRenderInfo`, les samples texte et `EditorDarkThemePreview`.
- Commit effectue: `docs: complete task 1 define rich textbox editor scope`.

### ✅ 2. Ajouter une matrice de tests pour buffer, ranges et lignes

But:
verrouiller la logique pure avant de toucher au rendu ou a l'input.

Travail attendu:

- creer des tests pour index absolu, ligne/colonne, insertion, suppression, remplacement et normalisation de retours ligne ;
- couvrir les ranges vides, ranges inverses, selections multi-lignes et limites de document ;
- couvrir la conversion `TextSpan` vers lignes visibles ;
- couvrir les tabs avec `TabSize` sans imposer une largeur de rendu dans les tests purs.

Criteres d'acceptation:

- les tests expriment le comportement attendu du modele d'edition ;
- les tests peuvent echouer proprement avant implementation si la tache 3 n'est pas encore faite ;
- aucun rendu MonoGame n'est requis pour ces tests.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "TextEditing|RichTextBox"`

Commit recommande:

- `test: complete task 2 add text buffer range tests`

Resultat:

- Ajout de la matrice `RichTextBoxTextBufferTests` couvrant normalisation des retours ligne, index ligne/colonne, ranges inverses/clampes, replace/delete, conversion de ranges multi-lignes en spans et expansion visuelle des tabulations.
- Ajout des contrats texte purs necessaires a la compilation des tests: `MGTextPosition`, `MGTextRange`, `MGTextLineSpan` et une premiere version de `MGTextBuffer` dans `MGUI.Core/UI/TextEditing/`.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RichTextBoxTextBufferTests"`.
- Resultat validation: 9 tests passes, 0 echec ; warnings existants dans le repo, sans nouveau blocage lie a cette tache.
- Commit effectue: `test: complete task 2 add text buffer range tests`.

### ✅ 3. Ajouter le modele d'edition texte partageable

But:
fournir un noyau testable pour texte, lignes, spans et operations d'edition.

Travail attendu:

- ajouter des types internes ou publics selon le contrat de la tache 1, par exemple `MGTextBuffer`, `MGTextRange`, `MGTextPosition`, `MGStyledTextSpan` ;
- supporter insertion, suppression, remplacement, selection et snapshots ;
- maintenir un index de lignes incremental ou recalcule uniquement quand necessaire ;
- exposer des methodes `TryGetPosition`, `TryGetIndex`, `NormalizeRange` ;
- garder la logique pure sans dependance `GraphicsDevice` ni `SpriteBatch` ;
- faire passer la matrice de tests de la tache 2.

Criteres d'acceptation:

- la logique texte est reutilisable par `MGRichTextBox` sans dependance au controle ;
- les edits preservent des ranges valides ;
- les tests de lignes, ranges et edits passent.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "TextEditing|RichTextBox"`

Commit recommande:

- `feat: complete task 3 add text editing model`

Resultat:

- Extension du modele texte avec `MGTextEditResult`, `MGTextBufferSnapshot`, `MGTextSelectionState`, `MGRichTextStyle` et `MGStyledTextSpan`.
- `MGTextBuffer` expose maintenant `ApplyEdit(...)`, `GetText(...)`, `CreateSnapshot()` et `RestoreSnapshot(...)`, tout en conservant les helpers `Insert`, `Delete` et `Replace` poses a la tache 2.
- Ajout de tests `RichTextBoxTextEditingModelTests` couvrant resultats d'edition detailles, snapshots immuables, restauration, selection anchor/active et styles de spans.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RichTextBoxTextBufferTests|RichTextBoxTextEditingModelTests"`.
- Resultat validation: 15 tests passes, 0 echec ; warnings existants dans le repo, sans nouveau blocage lie a cette tache.
- Commit effectue: `feat: complete task 3 add text editing model`.

### ✅ 4. Partager prudemment les helpers utiles de MGTextBox

But:
eviter de dupliquer la logique clavier/caret existante tout en limitant le risque de regression sur `MGTextBox`.

Travail attendu:

- identifier les helpers de `MGTextBox` reutilisables: shortcuts, repeat keys, caret normalization, undo/redo, clipboard ;
- extraire uniquement ce qui est stable vers un helper interne type `MGTextEditingInputHelpers` ou `MGTextUndoStack` ;
- ne pas changer le comportement public de `MGTextBox` ;
- ajouter des tests de non-regression sur les helpers extraits ;
- verifier que les tests existants `TextBox` continuent de passer.

Criteres d'acceptation:

- `MGTextBox` compile et conserve ses tests ;
- les helpers extraits ne forcent pas `MGRichTextBox` a utiliser le markdown de selection de `MGTextBox` ;
- l'extraction reste petite et reversible.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "TextBox|TextEditing|RichTextBox"`

Commit recommande:

- `refactor: complete task 4 share textbox editing helpers`

Resultat:

- Extraction de `MGTextEditingInputHelpers` pour les decisions clavier partageables: touches preservees en mode texte, repetition de touche, raccourcis Ctrl et normalisation d'index caret.
- Extraction de `MGTextUndoStack<T>` pour reutiliser une pile undo/redo bornee sans garder la classe privee `LimitedStack<T>` dans `MGTextBox`.
- `MGTextBox` conserve ses wrappers statiques internes existants et forward vers les helpers extraits, ce qui limite le risque de regression pour les tests et usages internes actuels.
- Ajout de `RichTextBoxTextEditingInputHelperTests` couvrant les helpers clavier, les wrappers de compatibilite `MGTextBox` et le trimming de la pile undo.
- Validation executee avec succes: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "TextBox|TextEditing|RichTextBox"`.
- Resultat validation: 52 tests passes, 0 echec ; warnings existants dans le repo, sans nouveau blocage lie a cette tache.
- Commit effectue: `refactor: complete task 4 share textbox editing helpers`.

### ✅ 5. Ajouter le shell du controle MGRichTextBox

But:
introduire le controle, ses parts template et son integration XAML sans encore livrer la coloration complete.

Travail attendu:

- creer le controle public choisi en tache 1 ;
- definir les template parts minimales: bordure, viewport texte, gutter optionnel, host de popup completion ;
- ajouter la valeur `MGElementType` si le repo l'exige pour les controles publics ;
- exposer les proprietes de base `Text`, `IsReadonly`, `AcceptsTab`, `ShowLineNumbers`, `TabSize` ;
- brancher la creation imperative et la creation XAML ;
- ajouter un template par defaut coherent avec les styles/theme existants.

Criteres d'acceptation:

- un `MGRichTextBox` vide peut etre instancie par code et XAML ;
- le controle participe correctement au layout measure/arrange ;
- aucune popup, coloration ou completion n'est encore requise ;
- le sample compile meme si le controle n'est pas encore visible dans le compendium.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RichTextBox|Architecture"`

Commit recommande:

- `feat: complete task 5 add rich textbox shell`

Resultat:

- Ajout du controle public `MGRichTextBox`, derive prudemment de `MGTextBox` pour reutiliser le layout, les template parts et l'input existants pendant la tranche shell.
- Ajout de `MGElementType.RichTextBox` et du mapping XAML `RichTextBox`, avec proprietes `TabSize` et `ShowLineNumbers`.
- `MGRichTextBox` synchronise un `MGTextBuffer`, expose `CaretIndex` et `SelectionState`, et conserve le template par defaut `TextBox.Default` pour rester coherent avec le theme existant.
- Ajout de `RichTextBoxShellTests` pour verifier l'enregistrement enum et le mapping XAML.
- Validations executees avec succes:
	- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RichTextBoxShellTests"`
- Note validation: le filtre large recommande `RichTextBox|Architecture` compile mais declenche deux echecs d'architecture existants et sans lien avec cette tache (`ToolingHooksTests.UIToolingService_ExposesSnapshotAndPreviewHooks`, `BackendProjectSplitTests.IntegrationProject_StripsLegacyRendererFiles`).
- Commit effectue: `feat: complete task 5 add rich textbox shell`.

### ✅ 6. Rendre des spans de texte styles sans modifier le texte source

But:
poser la base de la coloration syntaxique et de la selection visuelle.

Travail attendu:

- convertir texte brut + spans styles + selection + caret en lignes/runs rendables ;
- reutiliser `MGTextRun` et `MGTextBlock` si cela reste propre, ou ajouter une surface de rendu dediee si necessaire ;
- definir une politique de fusion et priorite des styles: selection, caret, diagnostics, syntaxe ;
- ne pas injecter de markdown dans le texte source pour representer les styles ;
- limiter le travail au viewport visible quand la structure existante le permet ;
- restaurer correctement tout etat de clipping/scissor utilise.

Criteres d'acceptation:

- des ranges colores s'affichent sans alterer `Text` ;
- les styles peuvent commencer et finir au milieu d'une ligne ;
- la selection reste visible au-dessus de la coloration ;
- pas d'allocations evidentes par frame dans `Draw`.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RichTextBox|TextRendering"`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

Commit recommande:

- `feat: complete task 6 render styled text spans`

Resultat:

- Ajout d'une couche de spans stylés non destructifs dans `MGRichTextBox`: `SetStyledSpans(...)`, `ClearStyledSpans()`, `StyledSpans`, `HasStyledSpans`.
- Conversion texte brut + `MGStyledTextSpan` vers `MGTextRunText` via `BuildStyledTextRuns(...)`, en reutilisant `MGTextBlock.SetTextRuns(...)` et sans injecter de markdown dans `Text`.
- Les spans sont clampes, tries, et rendus hors chemin `Draw`; la generation se fait lors du changement de spans ou de texte.
- Ajout de `RichTextBoxStyledRunTests` couvrant conversion en runs, clamp/order, overlap simple et preservation du texte source.
- Validations executees avec succes:
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RichTextBoxStyledRunTests"`
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- Resultat validation: 3 tests passes, 0 echec ; build sample OK.
- Commit effectue: `feat: complete task 6 render styled text spans`.

### ✅ 7. Brancher l'edition clavier, souris, selection et undo/redo

But:
faire de `MGRichTextBox` une vraie surface editable, pas seulement une vue coloree.

Travail attendu:

- brancher focus clavier, insertion, suppression, retour ligne, tabulation et navigation par fleches ;
- supporter Home/End, Ctrl+A, Ctrl+C, Ctrl+X, Ctrl+V, Ctrl+Z, Ctrl+Y selon les conventions existantes ;
- supporter selection a la souris, drag hors bounds avec capture si le pipeline existant le permet ;
- garder le caret et la selection coherents apres edits ;
- mettre a jour les spans styles apres edits, meme si la syntaxe complete arrive en tache 8 ;
- ajouter les tests pertinents sur logique pure et helper input.

Criteres d'acceptation:

- l'edition multi-ligne fonctionne dans un sample minimal ou test harness ;
- les shortcuts ne fuient pas vers les controles derriere quand le controle a le focus ;
- undo/redo restaure texte, caret et selection ;
- `IsReadonly` bloque les edits mais conserve navigation et selection.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RichTextBox|TextEditing|Input|Focus"`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

Commit recommande:

- `input: complete task 7 wire rich textbox editing behavior`

Resultat:

- `MGRichTextBox` conserve l'input clavier/souris, caret, selection et undo/redo de `MGTextBox` pendant cette v1, ce qui evite de dupliquer le pipeline d'input existant.
- Ajout de `ApplyTextEdit(...)` sur `MGRichTextBox` pour appliquer des edits programmes via ranges texte, synchroniser `Text`, `TextBuffer` et `SelectionState`.
- Ajout d'un controleur pur `MGRichTextEditController` couvrant remplacement de selection, insertion, backspace, delete-forward, selection, select-all, undo et redo.
- Ajout de `RichTextBoxEditingControllerTests` pour verrouiller edition multi-ligne/model, selection et restauration undo/redo sans dependance rendu.
- Validations executees avec succes:
	- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RichTextBoxEditingControllerTests"`
	- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- Resultat validation: 4 tests passes, 0 echec ; build sample OK.
- Commit effectue: `input: complete task 7 wire rich textbox editing behavior`.

### ⚪ 8. Ajouter les services de coloration syntaxique

But:
permettre au controle de recevoir des spans de syntaxe sans connaitre le langage.

Travail attendu:

- definir une interface type `ISyntaxHighlighter` ou `IRichTextHighlighter` ;
- definir un resultat stable: spans, token kind, diagnostic optionnel, version du buffer ;
- ajouter un highlighter `PlainText` et un highlighter demo pour C# ou XAML selon le besoin du sample ;
- relancer la coloration sur changement texte avec invalidation bornee ;
- ignorer proprement les resultats obsoletes si une coloration asynchrone est introduite plus tard ;
- exposer des couleurs via theme/resources plutot que valeurs hard-codees dans le controle.

Criteres d'acceptation:

- la coloration est injectable et testable sans `GraphicsDevice` ;
- le controle se met a jour apres edition ;
- la palette peut etre adaptee au theme editeur ;
- la v1 reste volontairement lexicale, sans promesse semantique lourde.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "Syntax|RichTextBox|TextEditing"`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

Commit recommande:

- `feat: complete task 8 add syntax highlighting services`

Resultat:

- A remplir par l'agent implementant la tache.

### ⚪ 9. Ajouter le modele d'autocompletion

But:
separer le calcul des suggestions de leur affichage.

Travail attendu:

- definir `ICompletionProvider`, `CompletionContext`, `CompletionItem`, `CompletionTrigger` ;
- supporter trigger manuel et trigger automatique sur caracteres configurables ;
- filtrer les suggestions selon le prefixe courant ;
- produire une operation d'acceptation: remplacement de range, insertion de texte, nouvelle position de caret ;
- ajouter un provider demo pour le sample avec mots cles C# ou XAML ;
- tester filtrage, tri, remplacement et annulation.

Criteres d'acceptation:

- la completion peut etre testee sans UI ;
- accepter une suggestion modifie le buffer de maniere deterministe ;
- les providers peuvent etre remplaces par un agent externe ou un service plus avance plus tard.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "Completion|RichTextBox|TextEditing"`

Commit recommande:

- `feat: complete task 9 add completion model services`

Resultat:

- A remplir par l'agent implementant la tache.

### ⚪ 10. Ajouter la popup d'autocompletion UI

But:
afficher et naviguer les suggestions sans casser le focus ni le clipping.

Travail attendu:

- creer une popup ou overlay de completion reutilisant les primitives MGUI existantes, par exemple `MGListBox` ;
- positionner la popup pres du caret et la contraindre au viewport ;
- gerer Up/Down, PageUp/PageDown si simple, Enter/Tab pour accepter, Escape pour fermer ;
- supporter clic souris sur suggestion ;
- ne pas voler durablement le focus clavier au `MGRichTextBox` ;
- verifier le clipping imbrique et la restauration d'etat de rendu.

Criteres d'acceptation:

- les suggestions s'ouvrent, se filtrent et se ferment correctement ;
- l'acceptation applique l'operation fournie par le modele de completion ;
- la popup reste lisible dans le theme editeur ;
- l'input ne se propage pas aux surfaces derriere quand la completion traite la touche.

Validation recommandee:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "Completion|RichTextBox|Focus|Overlay|Clip"`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

Commit recommande:

- `ui: complete task 10 add completion popup`

Resultat:

- A remplir par l'agent implementant la tache.

### ⚪ 11. Ajouter le sample editeur riche dans MGUI.Samples

But:
rendre la feature visible et testable par l'utilisateur final du sample.

Travail attendu:

- ajouter `EditorRichTextBox.xaml` et `EditorRichTextBox.xaml.cs`, ou etendre `EditorDarkThemePreview` si la tache 1 l'a decide ;
- enregistrer le sample dans `Compendium.xaml` et `Compendium.xaml.cs` ;
- appliquer `CasaEditor.Dark` quand il est disponible, avec fallback propre sinon ;
- fournir un texte de depart representatif ;
- activer coloration syntaxique et completion demo ;
- ajouter un scenario id, par exemple `SCN-EDITOR-RTB-001` ;
- eviter le texte explicatif inutile dans l'UI: le sample doit etre une experience d'edition directement utilisable.

Criteres d'acceptation:

- le sample compile et apparait dans le compendium ;
- le controle permet de modifier le texte, voir la coloration et accepter une completion ;
- les couleurs et espacements restent coherents avec le theme editeur ;
- la demo fonctionne meme si les assets `CasaEditor.Dark` ne sont pas trouves.

Validation recommandee:

- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- verification manuelle du sample dans le compendium si l'environnement graphique le permet.

Commit recommande:

- `sample: complete task 11 add editor rich textbox sample`

Resultat:

- A remplir par l'agent implementant la tache.

### ⚪ 12. Documenter l'API et mettre a jour l'analyse des gaps

But:
clarifier ce que la v1 livre et ce qui reste a faire.

Travail attendu:

- documenter l'utilisation minimale de `MGRichTextBox` dans `README.md` ou un document dedie selon les conventions du repo ;
- mettre a jour `wpf-controls-gap-analysis.md` pour signaler l'etat v1 du `RichTextBox` MGUI ;
- documenter les interfaces de coloration et completion ;
- expliquer les limites v1: pas de `FlowDocument`, pas de RTF, pas de LSP complet ;
- ajouter un snippet XAML et un snippet C# courts.

Criteres d'acceptation:

- un contributeur sait instancier le controle, fournir un highlighter et fournir un completion provider ;
- les limites sont explicites pour eviter une attente de parite WPF complete ;
- les references au sample sont correctes.

Validation recommandee:

- verification documentaire des chemins, snippets et noms publics ;
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore` si les snippets citent des noms publics recents.

Commit recommande:

- `docs: complete task 12 document rich textbox editor v1`

Resultat:

- A remplir par l'agent implementant la tache.

### ⚪ 13. Stabiliser les scenarios editeur et fermer la tranche v1

But:
consolider les regressions potentielles avant de considerer la feature livree.

Travail attendu:

- lancer une validation ciblee et une validation plus large pertinente ;
- revoir les allocations evidentes dans `Update` et `Draw` du nouveau controle ;
- verifier les cas limites: document vide, fichier long raisonnable, selection multi-ligne, readonly, completion fermee par Escape, theme absent ;
- corriger uniquement les bugs lies a cette tranche ;
- mettre a jour ce fichier avec le resume final et les commandes lancees.

Criteres d'acceptation:

- build core et sample OK ;
- tests texte/input/completion OK ;
- aucun probleme evident de focus, capture souris, clipping ou theme dans le sample ;
- la section `Resultat` de chaque tache terminee contient un resume utile.

Validation recommandee:

- `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-restore --filter "RichTextBox|TextEditing|Syntax|Completion|TextBox|Focus|Overlay|Clip" --logger "console;verbosity=minimal"`

Commit recommande:

- `test: complete task 13 stabilize editor scenarios`

Resultat:

- A remplir par l'agent implementant la tache.