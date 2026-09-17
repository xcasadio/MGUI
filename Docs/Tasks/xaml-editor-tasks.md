# Taches editeur XAML (V1) : rendu a chaud, selection et grille de proprietes

## Objectif

Livrer la premiere version d'un editeur XAML pour MGUI : un editeur de texte XAML avec rendu a chaud, la selection des elements (sans manipulation a la souris) et une grille de proprietes qui montre les parametres de l'element selectionne et dont chaque modification edite le noeud XAML correspondant. L'editeur vit dans un projet a part de la solution (`MGUI.Editor`, bibliotheque) avec un executable minimal (`MGUI.Editor.Host`) et doit pouvoir etre integre dans l'editeur CasaEngine.

Architecture cible : [editor-architecture.md](../editor-architecture.md). Decisions : [decisions/0010-xaml-editor-v1.md](../decisions/0010-xaml-editor-v1.md).

## Historique du fichier

- 15 septembre 2026 : demande de l'auteur (editeur XAML, rendu a chaud, selection, grille qui edite le noeud XAML ; projet a part, integrable dans CasaEngine ; `EditorRichTextBox` a faire evoluer) et discovery en lecture seule sur cinq surfaces a HEAD `05de9ac`.
- 17 septembre 2026 : reponses de l'auteur aux quatre questions groupees ; redaction du plan, de l'ADR-0010 (Proposed) et de la doc d'architecture ; relecture du plan contre le code par quatre relecteurs en contexte frais, chaque constat bloquant recontrole par un second agent (22 constats retenus sur 24, tous integres ci-dessous) ; deux sondes jetables hors depot sur la technique de positions du loader ; relecture de cloture en contexte frais (treize points sur quatorze confirmes, sept nouveaux constats dont trois bloquants, tous integres : remontee par la chaine `Parent`, positions propres aux descendants d'un template clone, etendue complete des noeuds). Aucun code n'est ecrit dans le depot avant l'approbation du plan.
- 17 septembre 2026 (suite) : plan approuve par l'auteur (« commence a faire les taches ») ; documents du plan committes a part (`a9a36f4`). Reponses de l'auteur a quatre questions posees avant X0 : decisions 6 a 8 ci-dessous.
- 17 septembre 2026 (fin de journee) : X0 et X1 validees par l'auteur ; X1 committee sur `develop` puis verifiee, correctif de transmission des positions au writer sur la branche `xaml-editor` (decision 9) ; demande de l'auteur d'utiliser le docking manager : tache X1b proposee, relue trois fois, approuvee (decision 10), et textes de X0 a X8 retouches en consequence.

## Decisions de l'auteur

1. Forme du projet : `MGUI.Editor` (bibliotheque) + `MGUI.Editor.Host` (executable minimal).
2. `MGXAMLDesigner` : a supprimer (controle, DTO XAML, sample), une fois l'editeur livre.
3. Volet texte en V1 : coloration XAML + marqueurs d'erreur + synchronisation caret / selection. La completion XAML est reportee.
4. Bindings dans la preview : point d'injection `DesignDataContext`, vide par defaut.
5. Deja tranche le 15 septembre : le composant texte est `MGRichTextBox` (sample `EditorRichTextBox`) a faire evoluer ; pas de manipulation a la souris en V1, seulement de la selection.
6. Rythme de livraison : l'agent s'arrete apres chaque tache et dit a l'auteur quoi tester ; la tache suivante ne commence qu'apres l'accord de l'auteur. La tache est committee avec le statut 🧪 des que le verifier confirme et que la suite est verte ; elle passe a ✅ apres l'accord de l'auteur, dans le commit de la tache suivante ; un defaut trouve par l'auteur donne un commit `fix` separe.
7. Forme de `XamlEditorView` : classe compositrice (pas un `MGElement`), `XamlEditorView(MGWindow window, XamlEditorSession session)`, qui construit les volets contre la fenetre hote. Revisee par la decision 10 : la grille `Root` de X0 est remplacee par des dockables.
8. Fenetre de l'editeur dans `MGUI.Editor.Host` : une `MGWindow` plein cadre, sans barre de titre, qui couvre la zone cliente et suit le redimensionnement de la fenetre du jeu (taille initiale 1600 x 900, comme `Game1`). En X7, le fichier et l'etat modifie vont dans le titre de la fenetre du jeu (`Game.Window.Title`).
9. Branches (17 septembre) : les noms `MGUI.Editor` et `MGUI.Editor.Host` sont gardes. X1 est committee sur `develop` avant sa verification pour servir de point de depart commun ; la suite du chantier se fait sur la branche `xaml-editor`, creee depuis ce commit, et l'autre session de l'auteur cree sa propre branche depuis le meme commit. Le merge dans `develop` se fait a la demande de l'auteur.
10. Coquille en docking (17 septembre, tache X1b approuvee par « tout est ok ») : les volets de l'editeur sont cinq dockables (« XAML », « Preview », « Document », « Properties », « Diagnostics ») heberges par un `MGDockHost`. `XamlEditorView` expose `Dockables` et `CreateDockHost()` ; un hote fait `window.SetContent(view.CreateDockHost())` ; un hote a docking (CasaEngine) enregistre `Dockables` dans son propre `MGDockHost`, sans docking imbrique. En V1 : volets sans `Name`, non fermables, auto-hide permis, flottants sauf « Preview » ; sauvegarde et reinitialisation du layout hors V1.
11. Apparence du Host (17 septembre, apres X1b : « il faut utiliser le theme dark et Fontstash ») : `MGUI.Editor.Host` regle, avant de creer sa fenetre, le theme par defaut du desktop sur le theme integre `Dark` et le moteur de texte du desktop sur FontStashSharp, charge avec les polices Arial suivies par le depot (normal, gras, italique) et calibre par `MatchSpriteFontSizing`, comme `MGUI.Samples/Game1.cs`. Si ce chargement echoue, le Host l'ecrit dans la sortie de debogage et garde le moteur SpriteFont. La bibliotheque `MGUI.Editor` ne fixe ni theme ni moteur de texte : ce sont des choix de l'hote (CasaEngine fait les siens).

## Etat des lieux (HEAD `05de9ac`, verifie dans le code)

Loader XAML :

- Pile XAML effective : `MGUI.Core` ne cible que `$(WindowsTargetFramework)`, ce qui definit `UseWPF` (`MGUI.Core/MGUI.Core.csproj:8-11`) ; `MGUI.Core/UI/XAML/XAMLParser.cs:5-10` compile donc la branche `using System.Xaml;`. La branche `#else` (Portable.Xaml) n'est compilee par aucun projet de la solution aujourd'hui, mais doit continuer a compiler.
- `XAMLParser.ParseDefinition` appelle `PrepareMarkup` puis `XamlServices.Parse` (`XAMLParser.cs:306-326`). Avec `SanitizeXAMLString=true`, `ValidateXAMLString` coupe les blancs, insere les namespaces et re-serialise tout le document avec `Indent = true` (`XAMLParser.cs:182-264`) : les positions du markup prepare ne correspondent plus au texte d'origine. Les samples sont charges avec `SanitizeXAMLString=false` (`MGUI.Samples/Compendium.xaml.cs:92`) et declarent leurs namespaces (`xmlns="clr-namespace:MGUI.Core.UI.XAML;assembly=MGUI.Core"`).
- `ReplaceLinebreakLiterals=true` remplace le litteral `\n` par `&#x0a;` (`XAMLParser.cs:292-295`) : quatre colonnes de plus par occurrence sur la ligne concernee.
- Le mode strict parse un `XDocument` avec `LoadOptions.SetLineInfo` pour valider les noms d'elements et d'attributs (`MGUI.Core/UI/XAML/XamlLoaderDiagnostics.cs:68-99, 221-253`) ; `XamlLoaderDiagnostic` porte `Code`, `Message`, `LineNumber`, `LinePosition`. Aucune position n'atteint le DTO ni le `MGElement`.
- DTO : classe de base `Element` (`MGUI.Core/UI/XAML/Element.cs:18`), `ToElement<T>` puis `ApplyBaseSettings` (`Element.cs:418-440`), chemin suivi aussi par la racine `Window` ; `XAMLParser.ResolveElementType` est `internal` (`XAMLParser.cs:167`) ; `MGElement.Metadata` est un dictionnaire public jamais reaffecte (`MGUI.Core/UI/MGElement.cs:3151`). `ToolTip` et `ContextMenu` (`Element.cs:236-237`) sont des proprietes de type element hors `GetChildren()` ; `Header` est rendu par `GetChildren()` (`Controls.cs:63-72`). `Element` porte `[TypeConverter(typeof(ElementStringConverter))]` : un contenu peut etre declare comme chaine.
- Templates : les templates de controle passent par `ParseObjectDefinition` (`ControlTemplateLoader`), pas par `ParseDefinition`. Un `ContentTemplate` declare dans un document (`ItemTemplate`, `DropdownItemTemplate`...) est clone a chaque item par `ContentTemplate.GetContent` (`MGUI.Core/UI/XAML/Templates.cs:29-35`).

Sondes du 17 septembre (projets console jetables dans le scratchpad de session, hors depot) sur le remplacement de `XamlServices.Parse` par une boucle `XamlXmlReader` + `XamlObjectWriter` :

- sur `System.Xaml`, onze cas dont trois fichiers complets (`CheckBox.xaml` 73 elements, `AnimationDemo.xaml` 153, `Compendium.xaml` 79) : graphe d'objets identique a celui de `XamlServices.Parse` ; la suite des noeuds `StartObject` de type `Element`, la suite des appels de `BeforePropertiesHandler` et la suite des elements d'un `XDocument` dont le nom se resout en DTO ont la meme longueur et le meme ordre ;
- `BeforePropertiesHandler` est appele une fois par instance dans l'ordre des `StartObject` sur les deux bibliotheques ; `AfterBeginInitHandler` n'est appele sur Portable.Xaml que pour les instances `ISupportInitialize`, ce que les DTO ne sont pas ;
- `LineNumber` et `LinePosition` sont en base 1 et `LinePosition` designe le premier caractere du nom de l'element (juste apres `<`) ;
- un element fabrique par un `TypeConverter` (`Content="texte"`) n'apparait dans aucune des trois suites : il n'a pas de position, sans desaligner les autres ;
- transmettre les positions au writer (`IXamlLineInfoConsumer`, comme `XamlServices.Transform`) ne change ni le graphe ni l'ordre, et renseigne la ligne et la colonne des `XamlObjectWriterException` ;
- toutes les API de la boucle existent sous le meme nom dans Portable.Xaml 0.26.0.

Preview :

- `UIToolingService.LoadPreview(window, source, dataContext, mode, sanitizeXamlString, replaceLinebreakLiterals)` (`MGUI.Core/Tooling/UIToolingService.cs:497-507`) renseigne `DataContextOverride`. `MGXAMLDesigner` (`MGUI.Core/UI/MGXAMLDesigner.cs`, 318 lignes) l'utilise, affiche une `XamlLoaderException` en texte et garde une racine `Window` alignee en suivant les deplacements de la fenetre hote (`MGXAMLDesigner.cs:165-173, 263-310`).
- Une `MGWindow` se met en page a sa propre position (`MGUI.Core/UI/MGWindow.cs:1464-1477`) et n'obeit pas a l'allocation d'un presentateur ; les setters `Left` / `Top` n'invalident pas la mise en page (`MGWindow.cs:97-124`).

Texte :

- `MGRichTextBox.ApplyTextEdit` passe par `SetText` (`MGUI.Core/UI/MGRichTextBox.cs:194-201`). `MGTextBox.SetText` vide la pile redo mais n'empile aucun etat d'undo ; seuls les chemins clavier empilent, et `CreateRestorableState` / `AddUndoState` sont prives (`MGUI.Core/UI/MGTextBox.cs:155-190, 863-868, 1510-1640`) : une edition programmee n'est pas annulable aujourd'hui.
- `MGRichTextBox` normalise les fins de ligne en LF (`MGRichTextBox.cs:249-252`, `MGTextBuffer.NormalizeLineEndings`). Aucun deplacement de caret par l'utilisateur n'est notifie : `NotifyPropertyChanged(nameof(CaretIndex))` n'est leve que par le setter (`MGRichTextBox.cs:106`).
- `IRichTextSyntaxHighlighter`, `MGStyledTextSpan` et `MGRichTextStyle.IsUnderlined` existent ; aucun tokenizer ni highlighter XAML (taches 3 et 7 de [richtextbox-autocomplete-tasks.md](richtextbox-autocomplete-tasks.md), toutes deux a faire) ; `ShowLineNumbers` n'a pas de rendu (`Docs/text-architecture.md`, limites connues).

Grille de proprietes :

- `MGPropertyGrid.SelectedObject` ; descriptors par reflexion ou par `ICustomTypeDescriptor` (`MGUI.Core/UI/PropertyGrid/MGPropertyGridDescriptorCache.cs:31-44`). `TryGetEditorKind` n'accepte que `bool`, les entiers, `float`, `double`, `string` et les types couleur ; un descriptor d'un autre type est ecarte en silence (`MGPropertyGridDescriptorCache.cs:46-93, 140-166`). La grille lit `Category`, `DisplayName` et `IsReadOnly` sur le `PropertyDescriptor`.
- `SelectedObject` sort tot sur la meme reference et reconstruit la vue pour toute nouvelle instance `ICustomTypeDescriptor` (`MGUI.Core/UI/MGPropertyGrid.cs:44-70`) ; `CommitRowValue` appelle le setter puis relit le getter (`MGPropertyGrid.cs:339-356`) ; `RefreshVisibleValues` ignore les lignes hors du viewport.

Selection et outillage :

- Aucun hit test public (`ComputeTopmostHoveredElement` prive, `GetTopmostHoveredElement` interne). Briques accessibles depuis `UIToolingService` (meme assembly) : `ConvertCoordinateSpace`, `ToLocalUnscaledPoint`, `ContainsUnscaledInputPoint` (`MGElement.cs:2714-2732`), `TraverseVisualTree(includeComponents)` public (`MGElement.cs:5309-5335`), `IsComponent` / `ComponentParent`.
- `MGAdornerLayer.TryAddAdorner` et `MGBoundsAdorner` ; la couche se pose comme enfant d'un `MGOverlayPanel` avec un z-index (`MGUI.Samples/Features/AdornerLite.xaml.cs:29-67`) ; `MGTreeView` (`AddItem`, `ClearItems`, `SelectedItem`).
- `UIToolingService.CaptureElementDebugView(...).ValueOrigins` expose `UIValueOriginView(PropertyPath, IsResolved, Source, EffectiveValue, Contributions)` pour cinq chemins seulement : `Background`, `TextForeground` (`Foreground` sur un `MGTextBlock`), `BorderBrush`, `BorderThickness`, `Padding` (`UIToolingService.cs:346-353`).

Solution et tests :

- Neuf projets ; hotes MonoGame : `MGUI.Samples/Game1.cs` (`GameRenderHost`, saisie de texte cablee automatiquement, `Desktop.LoadDefaultResources()`) et `MGUI.MiniGame/MiniGame.cs` (`DelegateRenderHost`, `Window.TextInput` cable a la main). Le paquet `MonoGame.Framework.DesktopGL` est `PrivateAssets=All` dans `MGUI.Core` (`MGUI.Core.csproj:26-28`) et ne se propage pas ; le contenu construit par `MGUI.Core` (polices, icones) est recopie dans la sortie de tout projet qui le reference.
- `MGUI.Tests` reference `MGUI.Core`, `MGUI.FontStashSharp`, `MGUI.MonoGame.LegacyRenderer`, `MGUI.Rendering.Abstractions`. `InternalsVisibleTo` : `MGUI.Tests`, `CasaEngine.Tests`. Les runtimes de test headless sont prives a chaque fichier (`PropertyGridTestRuntime`, `MGUI.Tests/Integration/PropertyGridTests.cs:602`).
- Usages de `MGXAMLDesigner` : le controle, le DTO `XAMLDesigner` (`MGUI.Core/UI/XAML/Controls.cs:4290`), `MGElementType.XAMLDesigner` (`MGUI.Core/UI/Enums.cs:112`, valeurs implicites ; seul site numerique : `MGUI.Tests/Architecture/RichTextBoxShellTests.cs:12-13`, un ordre relatif que la suppression conserve), le sample `MGUI.Samples/Dialogs/XAMLDesignerWindow.xaml(.cs)`, `MGUI.Samples.csproj:64, 204`, `Compendium.xaml:136`, `Compendium.xaml.cs:220, 298`, les tests `ToolingHooksTests.cs:53` et `XamlDocumentSourceTests.cs:41` (qui lisent le fichier source), `ResolvedPilotWriteSitesTests.cs:89`, le scenario `SCN-MARKUP-001`, et `README.md:21, 490-491` avec l'image `assets/samples/Sample_XAML_Designer_Window.gif`.
- Suite complete : 2337 tests verts a `d64d28d` ; seuls des commits de documentation depuis.

## Principes (non negociables)

- Le texte XAML est la seule source de verite. La grille produit des editions de texte au niveau de l'attribut ; aucun serialiseur DTO vers XAML.
- Le document edite est charge sans sanitisation : il declare ses namespaces, comme les fichiers des samples. Les positions du loader correspondent alors au texte.
- `MGUI.Core` reste generique : il ne recoit que des briques d'outillage testables (positions source, resolution de type, hit test, tokenizer et highlighter XAML, undo d'une edition programmee). Aucune logique d'editeur, aucun acces au systeme de fichiers.
- Aucun renommage d'API publique. La seule suppression est celle de `MGXAMLDesigner`, decidee par l'auteur (ADR-0010).
- La preview tourne dans le meme desktop que l'editeur ; le re-parse est complet, differe (debounce) et pilote par la boucle d'update, jamais par un thread.
- Un XAML invalide ne casse jamais l'editeur : dernier rendu valide conserve, diagnostic affiche.

## Perimetre V1 et hors perimetre

Dans le perimetre : les dix taches ci-dessous, X0 a X8 plus X1b (coquille en docking, ajoutee le 17 septembre).

Hors perimetre (documente, non implemente) :

- completion XAML (taches 1, 2, 4, 5, 6, 8, 9, 10 de [richtextbox-autocomplete-tasks.md](richtextbox-autocomplete-tasks.md)) ;
- editeurs types dans la grille (enum, vecteurs, couleur) : backlog [propertygrid-tasks.md](propertygrid-tasks.md) ; en V1 chaque ligne est la chaine de l'attribut ;
- manipulation a la souris, glisser-deposer, multi-selection, ajout ou suppression de noeuds depuis l'arbre ;
- preview rendue dans une texture ou dans un second desktop ; redimensionnement ou defilement d'une racine `Window` plus grande que le volet ;
- positions source pour les templates de controle et les themes ;
- gouttiere de numeros de ligne (`ShowLineNumbers` sans rendu aujourd'hui) ;
- donnees de conception generees ; edition des styles, ressources et templates depuis la grille (ils restent editables dans le texte).

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre, une seule a la fois : 🚧 avant de commencer, 🧪 au commit de la tache, ✅ apres l'accord de l'auteur (decision 6), statut mis a jour dans le meme commit que la tache. Un commit par tache, sur la branche `xaml-editor` (decision 9 ; X0 et X1 sont sur `develop`), indexe fichier par fichier ; jamais de push.
- Apres chaque tache : arret, et liste de ce que l'auteur doit tester. La tache suivante ne commence qu'apres son accord.
- Pipeline par tache : brief ecrit par la session principale (BRIEF, PERIMETRE, CLAIM, ACCEPTANCE), execution par un agent `sonnet`, verification par un agent `opus` en contexte frais, au plus deux tours de correction, puis revue et commit par la session principale.
- Ne jamais lancer `MGUI.Editor.Host` ni `MGUI.Samples` depuis un agent : le lancement est une validation manuelle de l'auteur.
- Editions avec l'outil d'edition uniquement : jamais de reecriture de fichier par PowerShell, perl ou sed ; aucun caractere U+FFFD dans le diff avant commit.
- Si `MGUI.Samples` est touche, terminer par `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-incremental` (un build `-t:Compile` laisse une DLL sans ressources XAML embarquees).
- Un test n'ecrit jamais dans les fichiers du depot : les fichiers de test vivent dans un dossier temporaire.
- Toute decision prise en cours de route est ajoutee a l'ADR-0010, section « Decisions taken during delivery ».
- Blocage (information manquante, besoin d'une API publique non listee ici, contradiction) : passer la tache en ⚠️, ecrire la question dans « Points ouverts », s'arreter.

## Legende de statut

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation minimale

1. `dotnet build MGUI.Tests/MGUI.Tests.csproj`
2. `dotnet build MGUI.Editor.Host/MGUI.Editor.Host.csproj` (a partir de X0)
3. `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~Editor|FullyQualifiedName~Xaml|FullyQualifiedName~Tooling|FullyQualifiedName~RichTextBox"` pendant la tache, puis la suite complete avant le commit (reference : 2337 tests verts).

## Taches

### ✅ X0. Projets et coquille de l'editeur

Statut (17 septembre 2026) : livre et verifie (verifier en contexte frais : CONFIRMED au premier tour ; suite complete 2348/2348, dont 11 nouveaux tests). Reste la validation manuelle de l'auteur ci-dessous. Remarques mineures du verifier, non corrigees : le build du Host affiche l'avertissement « No Content References Found » (paquet `MonoGame.Content.Builder.Task` garde comme dans `MGUI.MiniGame`, sans `.mgcb`) ; la taille initiale de la fenetre de l'editeur est lue dans `Window.ClientBounds` juste apres `ApplyChanges()` (a observer au premier lancement : la coquille doit remplir la fenetre des la premiere image).

But : creer les deux projets, les brancher dans la solution et les tests, et poser la coquille de la vue (volets vides).

Perimetre :

- `MGUI.Editor/MGUI.Editor.csproj` : bibliotheque, `$(WindowsTargetFramework)`, `MGUI.Core` comme seule reference de projet, plus `<PackageReference Include="MonoGame.Framework.DesktopGL">` en `PrivateAssets=All` (sans version : elle est centralisee dans `Directory.Packages.props`). Raison : l'API publique de `MGUI.Core` est typee XNA (`Point`, `Rectangle`, `Color`) et la reference MonoGame de `MGUI.Core` ne se propage pas. Meme motif que `MGUI.MonoGame.Integration` et `MGUI.FontStashSharp`.
- `MGUI.Editor.Host/` : executable `WinExe`. Le `csproj` reprend de `MGUI.MiniGame.csproj` le type `WinExe`, le framework cible et le paquet `MonoGame.Framework.DesktopGL`, avec une copie de `app.manifest` et de `Icon.ico` dans `MGUI.Editor.Host/` ; il ne reprend ni `MonoGame.Content.Builder.Task` ni la cible `RestoreDotnetTools` (pas de manifeste d'outils dans ce dossier, pas de contenu a construire) ; references de projet `MGUI.Editor`, `MGUI.Core`, `MGUI.MonoGame.LegacyRenderer`, `MGUI.Shared`. La classe `Game` reprend le motif de `MGUI.Samples/Game1.cs` : `MonoGameBackendBootstrap.Create(new GameRenderHost<T>(this))` (saisie de texte cablee automatiquement), `IObservableUpdate`, `Desktop.LoadDefaultResources()`. Le contenu necessaire arrive par la reference de projet `MGUI.Core` ; aucun `.mgcb` propre au Host.
- `MGUI.sln` (via `dotnet sln add`), `MGUI.Tests/MGUI.Tests.csproj` (reference `MGUI.Editor`), dossier `MGUI.Tests/Editor/`.
- `MGUI.Editor/XamlEditorSession.cs` : etat d'une session (texte, `SourceName`, `DesignDataContext`, evenements), sans acces fichier a ce stade.
- `MGUI.Editor/XamlEditorView.cs` : vue construite en code (pas de XAML embarque) : grille a trois colonnes avec `MGGridSplitter` ; volet texte (`MGRichTextBox`) ; volet preview = un `MGOverlayPanel` qui contient le presentateur de la preview (X4 y ajoutera la couche d'adorners au-dessus) ; colonne de droite avec l'arbre (`MGTreeView`) au-dessus de la grille (`MGPropertyGrid`). Elements nommes `TextPane`, `PreviewPane`, `TreePane`, `PropertyPane`. Remplace par X1b : la grille, ses separateurs et les noms des volets ont disparu ; les volets sont des dockables sans `Name`.

Hors perimetre : tout comportement des volets.

Criteres d'acceptation :

- les trois projets compilent ; `MGUI.sln` contient les deux nouveaux projets ;
- test : la vue se construit sur un desktop headless et expose les quatre volets nommes ;
- suite complete verte.

Validation manuelle (auteur) : `MGUI.Editor.Host` demarre et affiche la coquille. Confirmee par l'auteur le 17 septembre (« tout est ok »).

Rollback : suppression des deux dossiers, des entrees de la solution et de la reference dans `MGUI.Tests`.

Commit recommande : `feat(editor): add the MGUI.Editor library, its host and the view shell`

### ✅ X1. Positions source du loader et modele de document

Statut (17 septembre 2026) : implementee par un executeur ; committee sur `develop` AVANT la verification en contexte frais, a la demande de l'auteur, pour donner un point de depart commun aux deux sessions (decision 9). La verification, la revue et les corrections eventuelles se font ensuite sur la branche `xaml-editor`. Verification en contexte frais faite sur `5e06b3b` : CONFIRMED, suite complete 2393/2393 (45 nouveaux tests) ; graphe de DTO identique a celui de `XamlServices.Parse` sur 57 des 66 fichiers XAML de `MGUI.Samples` (7 demandent un type de l'assembly des samples, 2 sont des documents de theme), positions posees partout, chaine d'exceptions identique a l'ancien chemin sur six documents casses, API de la boucle presentes dans Portable.Xaml 0.26. Correctif du meme jour sur `xaml-editor` : la transmission des positions au writer XAML, retiree par le premier commit, est retablie (verifier : CONFIRMED, suite 2398/2398, cinq nouveaux tests `XamlLoaderLineInfoTests`, aucun test existant modifie) ; les marqueurs de tranche ont ete retires des commentaires de code.

But : relier chaque element cree par le loader a son noeud XAML, et disposer d'un modele du document qui sait ou se trouvent les balises et les attributs dans le texte.

Prerequis : X0.

Perimetre `MGUI.Core` :

- `MGUI.Core/UI/XAML/XamlSourcePosition.cs` : `public readonly record struct XamlSourcePosition(string SourceName, int Ordinal, int LineNumber, int LinePosition)`. `SourceName` = `XamlDocumentSource.DisplayName`, la valeur que le loader met deja dans `XamlLoaderDiagnostic.SourceName` : positions et diagnostics s'accordent par construction. `Ordinal` = rang de l'element, en ordre du document, parmi les elements XML dont le nom se resout en un DTO derive de `Element`. `LineNumber` et `LinePosition` sont en base 1 ; `LinePosition` designe le premier caractere du nom de l'element, juste apres `<`, dans le markup prepare (le texte du volet apres le remplacement des litteraux `\n`).
- `Element.SourcePosition` (`XamlSourcePosition?`, setter `internal`).
- `XAMLParser.ParseDefinition` : `XamlServices.Parse` est remplace par une boucle equivalente `XamlXmlReader` (`ProvideLineInfo = true`) + `XamlObjectWriter`, avec transmission des positions au writer comme le fait `XamlServices.Transform`. Pendant la lecture, deux listes sont remplies : la position de chaque noeud `StartObject` dont le type derive de `Element`, et chaque instance derivant de `Element` vue par `BeforePropertiesHandler`. Les positions ne sont posees qu'apres la boucle, par paires (k-ieme position, k-ieme instance). Un seul code pour les deux branches de compilation (`System.Xaml` et Portable.Xaml) : ne pas utiliser `AfterBeginInitHandler`, qui ne se declenche pas sur Portable.Xaml pour les DTO. Garde-fou de parse : si les deux listes n'ont pas la meme longueur a la fin de la lecture, aucune position n'est posee (jamais de position fausse, et rien a defaire). L'appariement est une methode `internal` de `MGUI.Core` qui recoit les deux listes : c'est sur elle que le garde-fou est teste, avec des listes volontairement inegales (`MGUI.Tests` est un assembly ami), puisqu'aucun XAML connu ne produit de divergence.
- `ParseObjectDefinition` (themes, templates de controle) n'est pas modifie : un element issu d'une part de template de controle ne porte aucune position.
- `Element.ApplyBaseSettings` recopie la position dans `MGElement.Metadata` sous la cle `UIToolingService.XamlSourcePositionMetadataKey`.
- `UIToolingService.TryGetXamlSourcePosition(MGElement, out XamlSourcePosition)` et `UIToolingService.TryResolveXamlElementType(string localName, out Type dtoType)` (enveloppe publique de `ResolveElementType`).
- Cardinalite : `(SourceName, Ordinal)` identifie un noeud du document, pas une instance. Un `ContentTemplate` declare dans le document est clone en profondeur a chaque item, et chaque DTO clone garde sa propre position : les N racines generees portent la position du noeud de contenu du template, et chaque descendant porte celle de son propre noeud dans la declaration du template. N instances renvoient donc au meme jeu de noeuds. C'est voulu : on edite le template, pas une instance. Les clones sont faits apres le parse et ne comptent pas dans le garde-fou.
- Un element fabrique par un `TypeConverter` (`Content="texte"`) ne porte aucune position.

Perimetre `MGUI.Editor` :

- `MGUI.Editor/Document/XamlDocumentModel.cs` : construit depuis le texte du volet (deja normalise en LF) par `XDocument.Parse(text, LoadOptions.SetLineInfo | LoadOptions.PreserveWhitespace)` ; `TryParse` ne leve jamais (erreur XML rendue avec ligne et colonne). Noeuds `XamlDocumentNode` : nom local, type de DTO, `Ordinal` (meme definition que le loader), parent, enfants, etendue de la balise ouvrante, etendue complete du noeud (du `<` de la balise ouvrante au `>` de la balise fermante ou de la balise auto-fermante), attributs `XamlDocumentAttribute` (nom, valeur decodee, etendue du nom, etendue de la valeur brute entre guillemets, caractere de guillemet). `XDocument` ne donnant que la position de depart d'un element ou d'un attribut, les etendues completes et les etendues de valeur sont relevees par une passe `XmlReader` sur le meme texte (`IXmlLineInfo` donne aussi la position des noeuds de fin d'element) ou par un balayage du texte depuis chaque position de depart. Toutes les etendues sont des `MGTextRange` dans ce texte LF, jamais dans les octets du fichier. Les elements de propriete (`Button.Content`) et les objets non `Element` (styles, setters, brushes, templates) sont traverses mais ne recoivent pas d'ordinal.
- `MGUI.Editor/Document/XamlAttributeEdit.cs` : fonctions pures qui rendent une edition `(MGTextRange, string)` : remplacer la valeur d'un attribut, inserer ` Nom="valeur"` avant `>` ou `/>`, supprimer un attribut avec le blanc qui le precede ; echappement de `&`, `<` et du guillemet utilise.

Criteres d'acceptation (tests `MGUI.Tests/Xaml/XamlSourcePositionTests.cs` et `MGUI.Tests/Editor/XamlDocumentModelTests.cs`) :

- ordinal, ligne et colonne exacts (document sans litteral `\n` ; un cas avec le litteral verifie la colonne dans le markup prepare) sur un document imbrique au style des samples (contenu implicite, balises sur plusieurs lignes), avec : syntaxe d'element de propriete, styles et setters, brush declare en element, `ToolTip` et `ContextMenu`, element nomme, extension `{dataBinding:MGBinding ...}` ;
- l'etendue complete de chaque noeud est exacte (balise auto-fermante, balise avec enfants, balises sur plusieurs lignes) ;
- l'ordinal du loader et celui du modele de document designent le meme noeud pour chaque element, y compris sur le texte complet de `MGUI.Samples/Controls/CheckBox.xaml` (lu, jamais ecrit) ;
- le `MGElement` cree porte la position, racine `Window` comprise ; une part de template de controle n'en porte aucune (`TryGetXamlSourcePosition` rend faux) alors que son plus proche ancetre declare dans le document en porte une ; un contenu declare comme chaine n'en porte aucune ;
- `ListBox` ou `ComboBox` avec un template d'item declare dans le document et trois items : les trois racines generees portent l'ordinal du noeud de contenu du template, et un descendant d'un item porte l'ordinal du noeud correspondant dans le template ;
- le garde-fou de divergence rend des positions absentes, pas fausses ;
- le graphe de DTO produit est identique a celui de `XamlServices.Parse` sur les documents de test ; aucun test existant du loader ne change de resultat ;
- editions d'attribut : remplacement, insertion (balise auto-fermante et balise ouverte, balise sur plusieurs lignes), suppression, echappement ; le texte resultant se re-parse et donne la valeur attendue ;
- suite complete verte.

Arret : si les instances ne sont pas creees dans l'ordre des `StartObject` sur l'un des cas, ne pas contourner par une heuristique : ⚠️ et question.

Rollback : retour a `XamlServices.Parse` ; les ajouts sont additifs.

Commit recommande : `feat(xaml): stamp loader source positions and add the editor document model`

### 🧪 X1b. Coquille en docking

Statut (17 septembre 2026) : livree et verifiee (verifier en contexte frais : CONFIRMED au premier tour ; suite complete 2404/2404 ; sonde independante du verifier : changements d'onglet, flottement et re-dock de chaque volet flottant, auto-hide, redimensionnements, sans exception ni contenu de repli d'erreur). Reste la validation manuelle de l'auteur ci-dessous. Deux comportements du docking de `MGUI.Core`, mesures et consignes dans l'ADR-0010, a connaitre pour la suite : un volet qui redevient actif recoit trois `OnParentChanged` (attache, detache, re-attache) : les gestionnaires de X2 et de X5 doivent etre idempotents et lire le `Parent` final ; des cycles flottement / re-dock repetes regroupent peu a peu tous les volets dans un seul groupe d'onglets (on les replace par glisser-deposer).

Origine : demande de l'auteur du 17 septembre, apres X1 (« je voulais utiliser le docking manager »). Proposee, relue trois fois en contexte frais, puis approuvee par l'auteur le 17 septembre (« tout est ok », qui valide aussi ses tests manuels de X0 et de X1). Elle revise la decision 7 (forme de `XamlEditorView`) et remplace la coquille de X0 ; elle passe avant X2, dont l'ancrage de la preview depend de l'hebergement du volet.

Faits verifies dans le code (HEAD `d2118c4`) :

- `MGDockHost(MGWindow)` (`MGUI.Core/UI/Docking/Controls/MGDockHost.cs`) est un `MGSingleContentHost` : il se pose comme contenu de n'importe quelle `MGWindow`. Son layout est un `DockLayoutModel` dont la racine est un arbre de `DockSplitNode` (`Orientation`, `SplitRatio`, `MinFirstSize`, `MinSecondSize`), de `DockTabGroupNode` (`AddPanel`, `SetActivePanel`) et de `DockPanelNode` (`MGUI.Core/UI/Docking/DockLayout/`). Exemple complet : `MGUI.Samples/Features/DockingDemo.cs:64-172`.
- `DockableDefinition(dockableId, title)` porte `ContentFactory` (`Func<MGElement>`), `CanClose`, `CanFloat`, `CanAutoHide`, `DockableType`, et `CreatePanelNode()`. Un `DockableRegistry` pose sur `MGDockHost.DockableRegistry` n'est notifie que partiellement : `OnShown` n'est leve que par `MGDockHost.RegisterPanel(panel)` (`MGDockHost.cs:1029-1043`, qui leve `InvalidOperationException` sur un identifiant deja enregistre) ; `IsVisible` est recalcule a chaque reconstruction depuis les panneaux enregistres (`SyncRegistryVisibility`, `MGDockHost.cs:2050-2065`), sans evenement ; `OnHidden` n'est leve par aucun code de `MGUI.Core`, `OnActivated` n'est leve que pour les groupes d'onglets du `LayoutModel` (ni fenetre flottante, ni tiroir auto-hide : `MGDockHost.cs:2342-2353`). Un panneau pose dans le layout sans `RegisterPanel` reste « cache » pour le registre, et `ShowDockable(id)` en creerait alors un second de meme identifiant (`MGDockHost.cs:1247-1297`).
- Le contenu d'un panneau est cree une fois (`DockPanelNode.GetOrCreateContent`), contre la fenetre hote. Le contenu d'un onglet inactif est detache de l'arbre visuel (`SetParent(null)`, `MGDockTabGroup.cs:629`) : il n'est ni mesure ni dessine. Un panneau flottant (`MGFloatingDockWindow`, `DetachToFloating`) re-parente le meme element : `ParentWindow` reste la fenetre de construction, `DisplayingWindow` devient la fenetre flottante (ADR-0004), l'entree fonctionne, les `LayoutBounds` restent en coordonnees ecran. Un panneau en auto-hide dont le tiroir est ferme est detache lui aussi (`MGDockAutoHideDrawer.cs:329-350`). Un element detache garde ses derniers `LayoutBounds` (ils ne sont recalcules que par `UpdateLayout`) : « cache » se lit sur `Parent == null`, pas sur des bounds vides. `MGElement.OnParentChanged` (`MGElement.cs:935`, public) est leve a chaque attache et detache, dans les trois etats (onglet, flottant, tiroir). Un changement d'onglet ne reconstruit pas l'arbre visuel de l'hote (`DockLayoutModel.OnNodePropertyChanged` sort tot pour `ActivePanelId`, `DockLayoutModel.cs:341-351`) : le volet desactive a bien un parent nul. Un panneau detache en fenetre flottante vide son groupe d'origine, que le modele supprime (`MGDockHost.cs:1332-1335`) : au re-dock il revient en onglet du premier groupe visible, pas a son emplacement d'origine. Les separateurs sont des `MGDockSplitterBar`, pas des `MGGridSplitter`.
- Noms : l'index des noms d'une `MGWindow` (`ElementsByName`) n'est alimente que par la chaine d'ajout et de retrait de contenu des `MGContentHost` (`MGWindow.cs:1446-1447, 1848-1883` ; `MGContentHost.cs:49-107`). `MGDockTabGroup` est un `MGElement` simple qui echange son contenu actif par `SetParent` : un changement d'onglet ne met pas l'index a jour (entree perimee pour le volet detache, volet active absent de l'index), et `ElementsByName.Add` leve sur une cle deja presente, ce qu'un re-dock d'un volet nomme peut provoquer pendant la reconstruction de l'arbre de l'hote. Un volet d'editeur ne doit donc pas porter de `Name` ; les proprietes de la vue (`TextPane`, `PreviewPane`...) et les identifiants de dockable sont le seul acces.
- Une racine `Window` de preview (X2) est une fenetre imbriquee de la fenetre de l'editeur : si le volet « Preview » flottait, elle devrait changer de fenetre parente (`AddNestedWindow` / `RemoveNestedWindow`) pour garder un ordre d'affichage et un clipping corrects.
- `DockLayoutSerializer.ToJson` / `FromJson` existent ; limite connue : le groupe d'origine d'une fenetre flottante n'est pas persiste (`Docs/Tasks/docking-bugs-tasks.md`).
- Tests headless du docking : `MGUI.Tests/Docking/` sur `GraphTestRuntime` (`FloatingWindowRedockTests.cs` : construction de l'hote, flottement, re-dock, lecture des bounds).

But : les volets de l'editeur sont des dockables heberges par un `MGDockHost` : l'utilisateur les redimensionne, les regroupe en onglets, les epingle en auto-hide et les detache en fenetre flottante. Un hote a docking (CasaEngine) enregistre les memes dockables dans son propre `MGDockHost`, sans docking imbrique.

Prerequis : X1.

Perimetre `MGUI.Editor` (`XamlEditorView.cs`) :

- `XamlEditorView(MGWindow window, XamlEditorSession session)` reste une classe compositrice. Elle construit contre `window` cinq volets, sans parent : `TextPane` (`MGRichTextBox`), `PreviewPane` (`MGOverlayPanel` contenant `PreviewPresenter`), `TreePane` (`MGTreeView`), `PropertyPane` (`MGPropertyGrid`) et `DiagnosticsPane` (nouveau : `MGContentPresenter` vide, que X3 remplira avec la liste des diagnostics). Les volets ne portent plus de `Name` (fait « Noms » ci-dessus) : les quatre constantes de nom de X0 sont retirees.
- La grille `Root` et ses deux `MGGridSplitter` disparaissent. A la place :
  - `Dockables` : cinq `DockableDefinition` creees une fois, d'identifiants constants `xaml-editor.text`, `xaml-editor.preview`, `xaml-editor.tree`, `xaml-editor.properties`, `xaml-editor.diagnostics`, de titres « XAML », « Preview », « Document », « Properties », « Diagnostics », dont le `ContentFactory` rend le volet correspondant ;
  - `CreateDockHost()` : un `MGDockHost` construit contre `Window`, avec un `DockableRegistry` qui contient les cinq definitions, le layout par defaut, puis `RegisterPanel` appele une fois sur chacun des cinq panneaux de ce layout (sans quoi le registre les croit caches). Le layout par defaut est un detail prive de cette methode, bati avec `CreatePanelNode()` : a gauche un bloc (72 %) dont le haut (78 %) place « XAML » (45 %) a gauche de « Preview », et dont le bas porte « Diagnostics » ; a droite une colonne ou « Document » (45 %) est au-dessus de « Properties » ; un groupe d'onglets par volet. Il n'y a pas de « reinitialiser le layout » en V1 : reaffecter un layout a un hote deja peuple n'a pas de contrat defini cote registre. C'est ce qu'utilisent `MGUI.Editor.Host` et les tests ; CasaEngine utilise `Dockables` avec son propre hote ;
  - Une vue ne peut etre hebergee que par un seul hote de docking a la fois, puisque chaque `ContentFactory` rend une instance fixe.
- Drapeaux V1 : `CanClose = false` pour les cinq (pas de menu « View » pour rouvrir un volet en V1) ; `CanAutoHide = true` pour les cinq ; `CanFloat = true` sauf pour « Preview », qui reste `CanFloat = false` en V1 (fait ci-dessus sur la racine `Window` imbriquee). `CanFloat` bloque le glisser hors du groupe et le menu contextuel de l'onglet, pas l'API publique `DetachToFloating` : l'editeur ne l'appelle jamais sur « Preview ».
- Le `ContentFactory` de « Preview » rend `PreviewPane` ; X7 le remplacera par un conteneur (bandeau + `PreviewPane`).
- Aucun comportement de volet : comme en X0.

Perimetre `MGUI.Editor.Host` : `window.SetContent(view.CreateDockHost())` a la place de `view.Root`.

Hors perimetre : sauvegarde et restauration du layout (suites connues) ; menu « View » et volets fermables (a ce moment-la, tout panneau devra passer par `RegisterPanel` pour que `ShowDockable` ne cree pas de doublon) ; flottement du volet « Preview » (suites connues) ; plusieurs documents ouverts ; tout changement dans `MGUI.Core` (si le docking montre un defaut bloquant : ⚠️ et question, pas de contournement).

Criteres d'acceptation (tests headless `MGUI.Tests/Editor/XamlEditorViewTests.cs`, reecrits ; motif de `MGUI.Tests/Docking/FloatingWindowRedockTests.cs`) :

- cinq dockables avec les identifiants, titres et drapeaux ci-dessus (« Preview » non flottant) ; chaque `ContentFactory` rend l'instance du volet ; avant `CreateDockHost()`, les cinq volets n'ont pas de parent ;
- registre : juste apres `CreateDockHost()` et deux frames, `DockableRegistry.IsVisible(id)` est vrai pour les cinq identifiants ;
- layout par defaut, fenetre de 1280 x 720, apres deux frames : les cinq volets ont un parent et des bounds non vides ; « XAML » est a gauche de « Preview », lui-meme a gauche de « Document » ; « Document » est au-dessus de « Properties » ; « Diagnostics » est sous « XAML » et « Preview » ; aucun des cinq volets ne porte de `Name` ;
- onglet inactif (le contrat « volet cache » sur lequel X2 et X5 s'appuient) : apres `DockOperation.DockAsTab` de « Properties » dans le groupe de « Document » (l'operation rend « Properties » actif) puis `SetActivePanel` sur « Document », `PropertyPane.Parent` est nul, `PropertyPane` n'est plus atteint par `TraverseVisualTree` depuis l'hote, et `view.PropertyPane` rend toujours la meme instance ; quand « Properties » redevient actif, son parent est le groupe d'onglets et ses bounds sont recalcules ; `OnParentChanged` a ete leve aux deux transitions ;
- flottement : « Properties » detache par `DetachToFloating` est affiche par la fenetre flottante (`DisplayingWindow`, membre `internal` lisible depuis `MGUI.Tests` seulement : ne pas l'exposer), `ParentWindow` reste la fenetre de l'editeur, et le re-dock le remet dans le layout ;
- robustesse des cycles : un changement d'onglet, puis un cycle `DetachToFloating` et re-dock, laissent le contenu de l'hote intact : aucune exception, aucun `MGTextBlock` sous l'hote dont le texte commence par « Error building docking layout » (`MGDockHost.CreateErrorPlaceholder`), et chacun des cinq volets est soit atteint depuis l'hote ou depuis la fenetre flottante, soit detache parce qu'il est l'onglet inactif de son groupe ;
- plus aucun `MGGridSplitter` dans la vue ; suite complete verte.

Retouches des taches suivantes, appliquees dans le meme commit que X1b (faites : decisions 7 et 10, X0, X2, X3, X5, X7, X8, ADR-0010, `Docs/editor-architecture.md`) :

- decision 7 (texte de `view.Root` et de `SetContent(view.Root)`), perimetre de X0 (description de la grille, « elements nommes ») et ce point ouvert : reecrits ou marques « remplace par X1b » ; le perimetre V1 passe a dix taches, et la formule de cloture de X8 (« delivered as nine slices X0 to X8 ») devient « ten slices » ; la decision de livraison X0 de l'ADR-0010 sur les colonnes de separateurs est marquee « remplacee par X1b » ;
- X0 : sa validation manuelle est absorbee par celle de X1b (la coquille de X0 est remplacee) ;
- X2 : son prerequis devient X1b ; dans sa regle d'ancrage, « glissement d'un `MGGridSplitter` » devient « deplacement d'un separateur de docking » : le re-ancrage de la racine `Window` se declenche sur tout changement des bounds du volet (separateur de docking, regroupement en onglet, redimensionnement) ; quand le volet « Preview » est detache (`Parent == null` : onglet inactif, tiroir auto-hide ferme), la racine `Window` de la preview est masquee et le re-parse continue ; elle est re-affichee et re-ancree sur `OnParentChanged` ; un critere ajoute pour ce cycle. Le volet ne flottant pas en V1, la racine `Window` ne change jamais de fenetre parente ;
- X3 : la liste des diagnostics est le contenu du dockable « Diagnostics » (`DiagnosticsPane`), plus « sous le volet texte » ; ce que fait le clic sur une entree quand le dockable « XAML » est un onglet inactif ou un tiroir ferme est a regler dans X3 ;
- X4 : inchangee (le volet « Preview » ne flotte pas ; un volet detache ne recoit pas de clic) ; l'acces aux volets passe par les proprietes de la vue, jamais par leur nom ;
- X5 et X6 : la grille se rafraichit quand `OnParentChanged` de `PropertyPane` annonce un nouveau parent non nul (une reconstruction de l'hote peut faire passer un volet d'un groupe a un autre sans etape a parent nul), car `RefreshVisibleValues` ne fait rien sur un volet detache ; ce declencheur couvre l'onglet re-active, la fenetre flottante et le tiroir auto-hide, ce que les evenements du registre ne font pas ; un critere ajoute en X5 ;
- X7 : la phrase « bascule Interactive dans la barre de l'editeur » est remplacee : la bascule vit dans un bandeau en tete du dockable « Preview » (il n'y a plus de barre d'editeur) ; le scenario `SCN-EDITOR-XAML-001` ajoute : deplacer un volet, le mettre en onglet, detacher « Properties » et le re-docker ;
- `Docs/editor-architecture.md` (« Session et vue », « Limites connues », « Reste a faire ») et ADR-0010 (decision de livraison X1b qui revise celle de X0 sur la forme de la vue ; suites connues : sauvegarde du layout, reinitialisation du layout, volets fermables, flottement de « Preview » ; limite connue : le menu contextuel d'un onglet montre « Close Others » et « Close All », sans effet sur des volets non fermables).

Validation manuelle (auteur) : lancer `MGUI.Editor.Host` ; verifier le theme sombre (onglets, separateurs, tiroirs et fenetres flottantes du docking compris) et le rendu du texte par FontStashSharp (decision 11) ; verifier le layout par defaut ; glisser les separateurs ; deposer « Properties » en onglet de « Document » puis le ressortir ; detacher « Properties » en fenetre flottante et le re-docker (comportement attendu du docking : il revient en onglet du premier groupe visible, pas a sa place d'origine ; on le replace par glisser-deposer) ; verifier que « Preview » ne propose pas de flotter ; epingler un volet en auto-hide et le rappeler ; taper dans « XAML » ; redimensionner la fenetre du jeu.

Rollback : `git revert` du commit (retour a la grille de X0).

Commit recommande : `feat(editor): host the editor panes in the docking manager`

### ⏳ X2. Hote de preview a chaud

But : rendre le texte de la session dans le volet preview, a chaud, sans jamais casser l'editeur.

Prerequis : X1b.

Perimetre : `MGUI.Editor/Preview/XamlPreviewHost.cs`, branchement dans `XamlEditorView`.

- Entrees : texte et `DesignDataContext` de la session. Sorties : `PreviewRoot`, `Diagnostics` (liste de `XamlLoaderDiagnostic`), `PreviewVersion`, evenement `PreviewUpdated`.
- `RequestRefresh()` marque le document sale ; le re-parse a lieu sur l'update de la fenetre de l'editeur apres `DebounceDelay` (250 ms par defaut) mesure avec le temps de l'update ; aucun thread, aucun timer systeme.
- Chargement par `UIToolingService.LoadPreview(..., XamlLoaderMode.Strict, sanitizeXamlString: false, replaceLinebreakLiterals: true)` avec `XamlDocumentSource.FromString(texte, session.SourceName)`.
- Sur `XamlLoaderException` : la racine precedente reste affichee, le diagnostic est publie. Un texte vide vide la preview sans diagnostic.
- Racine `Window` : affichee dans le volet et ancree a son coin haut-gauche ; `Left` et `Top` du XAML ne placent pas la preview. Une `MGWindow` se mettant en page a sa propre position, l'hote repositionne la fenetre de preview sur le coin du volet apres chaque `PreviewUpdated` et a chaque changement des bounds du volet (deplacement de la fenetre de l'editeur, deplacement d'un separateur de docking, regroupement en onglet, redimensionnement), puis demande une mise en page. La preview garde `Width` et `Height` du XAML : elle n'est ni redimensionnee ni mise a l'echelle, et ce qui depasse est rogne par le clip du volet.
- Volet cache (X1b) : quand `PreviewPane` est detache (`Parent == null` : onglet inactif, tiroir auto-hide ferme), la racine `Window` de la preview est masquee et le re-parse continue ; elle est re-affichee et re-ancree quand `OnParentChanged` annonce un nouveau parent non nul. Le dockable « Preview » ne flottant pas en V1, la racine `Window` ne change jamais de fenetre parente.
- `IsInteractive` (faux par defaut) : a faux, le sous-arbre de la preview ne recoit aucune entree (`IsHitTestVisible = false` sur la racine) ; a vrai, il se comporte normalement.

Criteres d'acceptation (tests headless, `MGUI.Tests/Editor/XamlPreviewHostTests.cs`) :

- pas de re-parse avant le delai, un seul re-parse apres une rafale de modifications ;
- XAML invalide : racine precedente conservee, diagnostic avec code, ligne et colonne ;
- `DesignDataContext` atteint `DataContextOverride` de la racine ;
- une racine `Window` declaree avec `Left="440" Top="20"` a son coin haut-gauche sur celui du volet des le premier rendu, et l'ancrage est refait sans nouvelle frappe apres un deplacement de la fenetre de l'editeur et apres un changement de largeur du volet ;
- une racine plus grande que le volet (`Width="500" Height="800"`) reste ancree et n'est pas redimensionnee ;
- volet cache : « Preview » mis en onglet inactif, la racine `Window` est masquee et un re-parse a quand meme lieu apres une modification du texte ; l'onglet redevenu actif, elle est visible et ancree sur le coin du volet sans nouvelle frappe ;
- `IsInteractive` a faux : un clic n'atteint pas un bouton de la preview ; a vrai : il l'atteint sur une racine non `Window` ;
- les elements de la preview portent le `SourceName` de la session ; suite complete verte.

Limite admise : si une racine `Window` ne recoit pas l'entree en mode interactif en tant que contenu du volet, le constat est consigne dans l'ADR-0010 et dans les limites connues, sans bloquer la tache (la V1 n'a pas besoin du mode interactif pour selectionner et editer).

Arret : si l'ancrage ne peut pas etre obtenu sans modifier la mise en page de `MGWindow`, ⚠️ et question ; ne pas modifier `MGWindow` dans cette tache.

Rollback : suppression du fichier et du branchement.

Commit recommande : `feat(editor): add the debounced hot preview host`

### ⏳ X3. Volet texte : coloration XAML, marqueurs d'erreur, edition programmee annulable

But : faire de `MGRichTextBox` un editeur XAML lisible qui montre les erreurs la ou elles sont.

Prerequis : X2 (diagnostics).

Perimetre `MGUI.Core` :

- `MGUI.Core/UI/TextEditing/Xaml/` : `XamlTokenKind`, `XamlToken`, `XamlTokenizer` (tache 3 de [richtextbox-autocomplete-tasks.md](richtextbox-autocomplete-tasks.md), telle qu'elle y est specifiee : tolerant, jamais d'exception) et `XamlSyntaxHighlighter` (tache 7 : ponctuation, nom d'element, nom d'attribut, chaine, commentaire, markup extension ; invariant de version respecte). Le choix de palette que laisse ouvert la tache 7 (etendre `MGRichTextSyntaxPalette` ou palette XAML dediee) est pris dans la tache et consigne dans l'ADR-0010.
- Undo d'une edition programmee : ajouter a `MGTextBox` un point d'extension non public (`private protected` ou `internal`, par exemple `PushUndoState()`) qui empile l'etat courant ; `MGRichTextBox.ApplyTextEdit` l'appelle avant d'appliquer l'edition, donc `TryUndo` annule une edition programmee. Changement de comportement (une acceptation de completion devient annulable elle aussi), consigne dans l'ADR-0010.
- Sample `EditorRichTextBox` : mode XAML (bascule du highlighter et du texte de demonstration), comme le demande la tache 7 ; ligne `SCN-EDITOR-RTB-001` de `Docs/scenario-validation-index.md` mise a jour.

Perimetre `MGUI.Editor` :

- `MGUI.Editor/Text/XamlEditorTextPane.cs` : compose `MGRichTextBox`, le highlighter XAML et les marqueurs d'erreur : un decorateur de highlighter ajoute, par-dessus la coloration, un span souligne a la position de chaque diagnostic (jeton a cette position, sinon fin de ligne). Conversion : `new MGTextPosition(LineNumber - 1, LinePosition - 1 - correction)` puis `MGTextBuffer.TryGetIndex` ; `correction` vaut quatre colonnes par litteral `\n` present avant la colonne sur la meme ligne. Positions mesurees (`XamlLoaderLineInfoTests`, ADR-0010) : pour un type ou un attribut inconnu, la position vient de la validation `XDocument` ; pour une erreur levee par le writer (valeur non convertible, setter qui leve), elle designe le premier caractere du NOM de l'attribut fautif, sur la ligne ou cet attribut est ecrit (pas celle de la balise quand elle tient sur plusieurs lignes) : le jeton souligne est alors le nom de l'attribut.
- La liste des diagnostics (code, message, ligne, colonne) est le contenu du dockable « Diagnostics » (`XamlEditorView.DiagnosticsPane`, X1b) ; un clic sur une entree place le caret. Ce que fait ce clic quand le dockable « XAML » est un onglet inactif ou un tiroir ferme est a regler dans cette tache (l'activer d'abord, ou ne placer que le caret), et a consigner dans l'ADR-0010.

Criteres d'acceptation :

- cas de tokenizer et de highlighter des taches 3 et 7, cas incomplets compris (`<`, `<Button`, `<Button Width="`) sans exception ;
- un diagnostic du loader (type inconnu, setter invalide, XML mal forme) produit un span souligne sur la bonne etendue : en ligne 1 colonne 1, en milieu de document, et sur une ligne contenant `\n` ;
- `ApplyTextEdit` puis `TryUndo` rend le texte et le caret d'avant ; la completion C# du sample reste fonctionnelle ;
- dans `richtextbox-autocomplete-tasks.md` : taches 3 et 7 passees a ✅ et une ligne ajoutee aux consignes pour dire qu'elles sont livrees hors ordre par ce programme (ADR-0010) ; `Docs/text-architecture.md` mis a jour ; `MGUI.Samples` compile (`--no-incremental`) ; suite complete verte.

Rollback : ajouts additifs ; l'undo d'`ApplyTextEdit` se retire en une ligne.

Commit recommande : `feat(text): add the XAML tokenizer and highlighter, error markers and undoable text edits`

### ⏳ X4. Selection : arbre, clic dans la preview, adorner, synchronisation du caret

But : selectionner un element depuis l'arbre, la preview ou le texte, les trois restant synchronises.

Prerequis : X1, X2, X3.

Perimetre `MGUI.Core` :

- `UIToolingService.HitTest(MGElement root, Point screenPoint)`. `screenPoint` est la position brute de la souris (`CoordinateSpace.Screen`). Le test reprend le chemin d'entree reel : conversion unique `Screen` vers `UnscaledScreen` sur la racine, puis contenance par element avec `ContainsUnscaledInputPoint(ToLocalUnscaledPoint(point))`, ce qui prend en compte l'echelle de la fenetre, le defilement, le clipping et les `RenderTransform`. Interdit : comparer le point a `LayoutBounds`. Visible = `Visibility.Visible` ; `RecentDrawWasClipped` n'est pas consulte : le chemin de contenance tient deja compte du clipping par `ActualLayoutBounds`, calcule a l'update, et le hit test ne doit pas dependre d'un `Draw` prealable ; `IsHitTestVisible` est ignore. Parcours par `TraverseVisualTree(includeComponents: true)` : le resultat est l'element le plus profond sous le point, composants compris ; a profondeur egale, le dernier frere l'emporte ; `null` hors de la racine. Le hit test est purement geometrique et ne connait pas les positions source : la remontee vers un element declare est faite par l'editeur. Aucune API publique nouvelle sur `MGElement`.

Perimetre `MGUI.Editor` :

- `MGUI.Editor/Selection/XamlEditorSelection.cs` : `SelectedNode`, `SelectedElement`, evenement `Changed`, `SelectByOrdinal`, `SelectByElement`, `SelectByCaret`.
- Remontee : `SelectByElement` part de l'element rendu par `HitTest` et remonte la chaine `Parent` (que `AddComponent` renseigne aussi pour les composants) jusqu'au premier element qui porte une position dont le `SourceName` est celui de la session ; sans resultat avant la racine de la preview, le clic ne selectionne rien. Cette regle couvre les parts de template, les enfants de parts (boutons d'un `NumericUpDown`) et le contenu fabrique depuis une chaine (`<Button Content="OK"/>` : un clic sur le libelle selectionne le bouton).
- Un noeud, N elements : `SelectByElement` est N vers 1 (cliquer sur un item genere selectionne le noeud correspondant du template). `SelectedElement` n'est qu'un representant : parmi les elements rattaches a la racine de la preview dont la position correspond, l'element clique s'il existe encore, sinon le premier en ordre visuel. Zero correspondance n'est pas une erreur : pas d'adorner, la selection du noeud reste valide.
- Arbre : `MGTreeView` reconstruit a chaque modele de document valide (nom local et `Name` s'il existe), etat d'expansion conserve par ordinal ; selection dans les deux sens.
- Preview : quand `IsInteractive` est faux, un clic dans le volet appelle `HitTest`, puis `SelectByElement`, qui selectionne le noeud de meme `Ordinal`.
- Adorner : `MGAdornerLayer` + `MGBoundsAdorner` ajoutes au `MGOverlayPanel` du volet preview avec un z-index superieur au contenu ; cible = `SelectedElement` ; masque sans cible.
- Caret : selectionner un noeud place le caret par le setter `MGRichTextBox.CaretIndex`. Le sens inverse est un sondage : `MGUI.Core` ne notifiant aucun deplacement de caret par l'utilisateur, `XamlEditorSelection` lit `CaretIndex` sur l'update de la fenetre de l'editeur, compare a la derniere valeur vue et n'appelle `SelectByCaret` que sur changement. `SelectByCaret` selectionne le noeud le plus profond dont l'etendue complete contient l'index du caret. Gardes : pas de synchronisation tant que le caret n'a pas de position, ni quand le texte a change depuis le dernier modele de document valide ; garde de reentrance avec le sens noeud vers caret ; sondage suspendu pendant une edition de grille (X6).
- La selection survit a un re-parse par ordinal ; si l'ordinal n'existe plus, elle est videe.

Criteres d'acceptation (tests headless, `MGUI.Tests/Tooling/` et `MGUI.Tests/Editor/XamlEditorSelectionTests.cs`) :

- `HitTest`, cas de base : element le plus profond (composant compris), dernier frere, point hors racine ;
- remontee : un clic sur une part de template, sur un bouton interne d'un `NumericUpDown` et sur le libelle de `<Button Content="OK"/>` selectionne a chaque fois le noeud declare qui les possede ;
- `HitTest`, echelle : avec une echelle de fenetre de 2, un point vise au centre visuel d'un enfant rend cet enfant ;
- `HitTest`, defilement et clipping : dans un `MGScrollViewer` defile, le point rend l'element reellement visible a cet endroit ; un element defile hors du viewport n'est jamais rendu ;
- `HitTest`, transform : un element porteur d'une `RenderTransform` de translation est atteint a sa position dessinee ;
- clic dans la preview, selection dans l'arbre et deplacement du caret donnent la meme selection et mettent a jour les deux autres vues sans boucle ;
- un deplacement de caret produit par l'entree (touche fleche ou clic, pas un appel direct a `SelectByCaret` ni au setter) suivi d'un tick d'update change la selection ; un tick sans deplacement n'en declenche aucune ;
- liste avec template d'item et trois items : un clic sur le troisieme item selectionne le noeud du template ; selectionner ce noeud pose l'adorner sur un seul item ;
- l'adorner suit l'element selectionne apres un re-parse qui change sa taille ;
- selection conservee apres un re-parse valide, conservee pendant un XAML invalide, videe si le noeud disparait ; suite complete verte.

Rollback : ajouts additifs.

Commit recommande : `feat(editor): select elements from the tree, the preview and the caret`

### ⏳ X5. Grille de proprietes du noeud XAML

But : montrer dans `MGPropertyGrid` les parametres du noeud selectionne.

Prerequis : X4.

Perimetre : `MGUI.Editor/Properties/XamlNodePropertySource.cs` (implemente `ICustomTypeDescriptor`), branchement dans `XamlEditorView`. `MGPropertyGrid` n'est pas modifie.

- Lignes : les proprietes du type de DTO publiques, avec setter public, non `[Browsable(false)]`, dont le type se convertit depuis une chaine (primitives, enums, nullables, types avec `TypeConverter`). Exclues : collections et dictionnaires (`Children`, `AttachedProperties`).
- Forme des descriptors : chaque `PropertyDescriptor` expose `PropertyType = typeof(string)`, donc l'editeur `String`, quel que soit le type reel du DTO ; un descriptor qui annoncerait `int?`, `Thickness?` ou un enum serait ecarte en silence par la grille. Le type reel est conserve par la source et ne sert qu'a la validation (X6). Chaque descriptor porte `Category` (reprise de `[Category]` du DTO, sinon `Misc`), `DisplayName` et `IsReadOnly`, que la grille lit sur le descriptor.
- Valeur d'une ligne : la chaine de l'attribut telle qu'ecrite dans le XAML ; attribut absent = `string.Empty`, jamais `null`. Une propriete de type element declaree comme chaine (`Content="OK"`) est une ligne editable ; declaree en element enfant, elle est en lecture seule.
- Cycle de vie : une instance de source par selection, mutee d'un re-parse a l'autre (la grille se rafraichit sans se reconstruire) ; une nouvelle instance a chaque changement de selection (la grille se reconstruit).
- Volet cache (X1b) : `RefreshVisibleValues` ne fait rien sur un volet detache. La grille se rafraichit quand `OnParentChanged` de `PropertyPane` annonce un nouveau parent non nul, ce qui couvre l'onglet re-active, la fenetre flottante et le tiroir auto-hide (les evenements du `DockableRegistry` ne couvrent ni le flottant ni le tiroir).
- Categorie en lecture seule « Resolved (runtime) » : les cinq entrees de `CaptureElementDebugView(...).ValueOrigins` du representant de la selection (`Background`, `TextForeground` ou `Foreground`, `BorderBrush`, `BorderThickness`, `Padding`), chacune avec sa valeur effective et sa source ; une entree non resolue est affichee comme telle. Les autres chemins (dont `Margin`) ne sont pas couverts en V1. Categorie absente sans representant.
- A ce stade toutes les lignes sont en lecture seule : l'ecriture arrive en X6.

Criteres d'acceptation (tests `MGUI.Tests/Editor/XamlNodePropertySourceTests.cs`) :

- descriptors d'un noeud `Button` : `Width`, `Margin`, `HorizontalAlignment`, `Name`, `Content` presents, tous de type chaine, avec la bonne categorie, et tous visibles comme lignes de la grille ; `Children` et `AttachedProperties` absents ;
- valeurs : attribut declare, attribut absent (chaine vide), contenu en chaine et contenu en element enfant (lecture seule) ;
- changer de selection remplace les lignes ; un re-parse rafraichit les valeurs sans reconstruire la grille quand le noeud selectionne ne change pas (le test avance les frames jusqu'a ce que la ligne visee ait des bounds avant d'asserter) ;
- volet cache : un re-parse qui change une valeur pendant que « Properties » est un onglet inactif est visible dans la grille des que l'onglet redevient actif ;
- la categorie « Resolved (runtime) » montre, pour un `Padding` pose par un style implicite, la valeur effective et une source de style implicite ; suite complete verte.

Rollback : suppression du fichier et du branchement.

Commit recommande : `feat(editor): show the selected XAML node in the property grid`

### ⏳ X6. Edition du XAML depuis la grille

But : un changement dans la grille edite le noeud XAML, et le rendu suit.

Prerequis : X3 (edition annulable), X5.

Perimetre : `XamlNodePropertySource` (setters, `IsReadOnly` a faux sur les lignes editables), `XamlEditorSession.ApplyEdit`, `XamlEditorView`.

- Setter d'une ligne : validation par le `TypeConverter` de la propriete du DTO (`ConvertFromInvariantString`) ; en cas d'echec, le texte n'est pas modifie, la ligne reprend sa valeur et un message est publie dans la liste des diagnostics de l'editeur. Valeur valide : `XamlAttributeEdit` (remplacement ou insertion), puis `MGRichTextBox.ApplyTextEdit`. `null` et chaine vide suppriment l'attribut.
- Suite de l'edition : modele de document re-parse tout de suite, preview rafraichie apres le delai, selection conservee par ordinal, grille rafraichie par `RefreshVisibleValues`.
- Pendant l'application d'une edition de grille, le sondage du caret et le rafraichissement de la grille sont suspendus ; le caret finit a la fin de la valeur editee.

Criteres d'acceptation (tests headless, `MGUI.Tests/Editor/XamlEditorRoundTripTests.cs` ; les tests passent par le setter du `PropertyDescriptor`, plus un test de bout en bout qui donne le focus a la zone de texte de la ligne et valide par Entree) :

- selection d'un `Button`, `Width` passe a `120` : le texte contient `Width="120"`, l'element de la preview a la largeur attendue apres le delai, la selection et l'adorner sont conserves ;
- valeur videe : l'attribut disparait du texte ; attribut absent puis renseigne : il est insere avant la fin de la balise, sans toucher au reste de la ligne ;
- valeur invalide (`Width="abc"`, enum inconnue) : texte inchange, message publie ;
- `TryUndo` apres une edition de grille rend le texte d'avant et la grille se rafraichit ;
- aucune boucle : une edition de grille produit une seule edition de texte et un seul re-parse ; suite complete verte.

Rollback : lignes remises en lecture seule (etat X5).

Commit recommande : `feat(editor): edit the XAML node from the property grid`

### ⏳ X7. Session de fichier, hote et documentation

But : rendre l'editeur utilisable sur de vrais fichiers et completer la documentation.

Prerequis : X6.

Perimetre :

- `XamlEditorSession` : `LoadFile(path)`, `Save()`, `IsDirty`, `SourceName` = chemin du fichier. L'acces fichier vit dans `MGUI.Editor`, jamais dans `MGUI.Core`. Le texte de la session est toujours en LF (le volet normalise) ; la forme du fichier est un etat de la session : fin de ligne d'origine et presence d'un BOM sont detectees a `LoadFile` et restituees par `Save()`.
- `MGUI.Editor.Host` : ouvre le fichier passe en premier argument, sinon un document de demonstration embarque ; `Ctrl+S` enregistre ; le titre de la fenetre du jeu (`Game.Window.Title`, decision 8) montre le fichier et l'etat modifie.
- Bascule « Interactive » : dans un bandeau en tete du dockable « Preview » (il n'y a pas de barre d'editeur depuis X1b) ; le `ContentFactory` de ce dockable rend alors un conteneur (bandeau + `PreviewPane`) au lieu de `PreviewPane`.
- Documentation : `Docs/editor-architecture.md` verifiee contre le code (et sa note d'etat cible retiree) ; `Docs/scenario-validation-index.md` (nouvelle ligne `SCN-EDITOR-XAML-001`, point d'entree `MGUI.Editor.Host/Program.cs`, et rattachement a `editor-architecture.md` et a l'ADR-0010 dans la liste par theme) ; `Docs/monogame-host-integration-guide.md` (mention du troisieme hote) ; `README.md` (courte mention de l'editeur) ; decisions prises en cours de route ajoutees a l'ADR-0010.

Criteres d'acceptation :

- tests : `LoadFile` puis `Save()` sans edition rend des octets identiques sur trois entrees ecrites par le test dans un dossier temporaire (CRLF sans BOM, CRLF avec BOM, LF sans BOM) ; apres une edition d'une ligne, le fichier garde sa fin de ligne et son BOM et seules les lignes touchees different ; `IsDirty` faux apres `LoadFile` et apres `Save()`, vrai apres une edition ;
- les builds de la validation minimale et la suite complete sont verts ;
- la doc ne decrit que ce qui existe dans le code.

Validation manuelle (auteur), scenario `SCN-EDITOR-XAML-001` : ouvrir `MGUI.Samples/Controls/CheckBox.xaml` ; verifier que la preview (500 x 800) est ancree en haut a gauche du volet, rognee, et qu'elle suit le volet quand on deplace le separateur ; deplacer un volet, le mettre en onglet, detacher « Properties » en fenetre flottante et le re-docker ; taper dans le texte (rendu a chaud) ; introduire une erreur (marqueur, dernier rendu conserve) ; selectionner par clic, par l'arbre et par le caret ; modifier `Width` et `Margin` dans la grille ; annuler ; enregistrer et controler le diff du fichier. La tache reste 🧪 jusqu'a cette validation.

Rollback : ajouts additifs.

Commit recommande : `feat(editor): open and save files from the host and document the editor`

### ⏳ X8. Suppression de MGXAMLDesigner et cloture

But : retirer l'ancien designer, remplace par l'editeur, et clore le programme.

Prerequis : X7 valide par l'auteur.

Perimetre :

- supprimer `MGUI.Core/UI/MGXAMLDesigner.cs`, le DTO `XAMLDesigner` (`MGUI.Core/UI/XAML/Controls.cs`), `MGElementType.XAMLDesigner` (`MGUI.Core/UI/Enums.cs`) ;
- supprimer `MGUI.Samples/Dialogs/XAMLDesignerWindow.xaml(.cs)`, les deux entrees de `MGUI.Samples.csproj`, la bascule de `Compendium.xaml` et la propriete de `Compendium.xaml.cs` ;
- tests : `ToolingHooksTests.Designer_UsesSharedToolingPreviewHook` devient `PreviewHost_UsesSharedToolingPreviewHook`, lit `MGUI.Editor/Preview/XamlPreviewHost.cs` et garde son assertion `UIToolingService.LoadPreview` ; `XamlDocumentSourceTests.Designer_UsesDocumentSourceAbstraction` devient `PreviewHost_UsesDocumentSourceAbstraction`, lit le meme fichier et ne garde que l'assertion `XamlDocumentSource.FromString` (les deux autres clauses sont propres au designer : lecture de fichier et litteral `LoadPreview(SelfOrParentWindow, Source`) ; l'entree `MGXAMLDesigner.cs` de `ResolvedPilotWriteSitesTests` est retiree ;
- `README.md` : retirer `MGXAMLDesigner` de la liste des controles (ligne 21) et le paragraphe du designer avec son image (lignes 490-491) ; supprimer `assets/samples/Sample_XAML_Designer_Window.gif`, que seul ce paragraphe reference ;
- `Docs/scenario-validation-index.md` : le point d'entree de `SCN-MARKUP-001` devient `MGUI.Editor.Host/Program.cs` ;
- cloture : ADR-0010 passee a `Accepted` (formule de l'ADR-0009 : « delivered as ten slices, X0 to X8 and X1b ») et ligne de `Docs/decisions/README.md` mise a jour.

Criteres d'acceptation :

- une recherche de `XAMLDesigner` ne rend plus que `Docs/decisions/` et ce plan : aucun resultat dans le code, les samples, les tests, `README.md` ni le reste de `Docs/` ;
- `RichTextBoxShellTests` reste vert (l'ordre relatif des membres de `MGElementType` est conserve) ;
- `MGUI.Samples` compile (`--no-incremental`) ; suite complete verte ;
- l'ADR-0010 annonce la suppression d'API publique.

Rollback : `git revert` du commit.

Commit recommande : `refactor(core): remove MGXAMLDesigner, superseded by MGUI.Editor`

## Points ouverts

- Aucun a la redaction. Deux hypotheses gardent une clause dans leur tache : ordre de creation des instances en X1 (verifie par sonde sur onze cas, clause d'arret conservee) ; racine `Window` en mode interactif en X2 (limite admise, non bloquante).
- X1, RESOLU le 17 septembre (accord de l'auteur : « continue ») : la transmission des positions au writer XAML, que le premier commit de X1 avait retiree, est retablie par un correctif sur `xaml-editor` sans modifier aucun test existant (verifier : CONFIRMED, suite 2398/2398). Une erreur levee par le writer (valeur non convertible, setter qui leve, `Thickness` mal formee) porte maintenant une ligne et une colonne ; regle mesuree dans l'ADR-0010 et reprise en X3.
- Hors chantier, a documenter seulement : `XamlLoaderDiagnostics.Classify` choisit le code d'un diagnostic en cherchant des mots anglais dans le message de l'exception ; `Width="abc"` est classe `ParseFailure` et non `InvalidValueConversion` (dans toutes les langues, mesure faite). Defaut preexistant, sans effet sur les positions ; X3 affiche le code tel quel. Un correctif separe (`4b51ca6`, branche `fix/xaml-diagnostics-culture`, verifie) a ete merge en avance rapide dans `develop` le 17 septembre, mais n'est pas encore dans la branche `xaml-editor` : apres le merge de `develop` dans `xaml-editor`, lever cette limite ici et dans l'ADR-0010.
- Structure de l'editeur, RESOLU le 17 septembre : l'auteur voulait que les volets soient heberges par le docking manager ; la tache X1b, relue trois fois en contexte frais (sept constats bloquants et une vingtaine de remarques, tous integres ; la troisieme relecture a confirme tous les faits), a ete approuvee par l'auteur : decision 10.

## Suites connues (hors V1)

- Completion XAML : taches restantes de [richtextbox-autocomplete-tasks.md](richtextbox-autocomplete-tasks.md) ; `UIToolingService.TryResolveXamlElementType` et le tokenizer de X3 en sont les premieres briques.
- Editeurs types de la grille : [propertygrid-tasks.md](propertygrid-tasks.md) (tache 1, enums, en premier) ; valeurs effectives des autres chemins pilotes dans la categorie « Resolved (runtime) ».
- Evenement de deplacement de caret dans `MGUI.Core`, pour remplacer le sondage.
- Redimensionnement ou defilement d'une racine `Window` plus grande que le volet.
- Integration CasaEngine : enregistrer `XamlEditorView.Dockables` dans le `MGDockHost` de l'editeur CasaEngine, fournir `DesignDataContext`.
- Police a chasse fixe pour le volet « XAML » : le depot ne suit que les polices Arial (`MGUI.Core/Content/Fonts/ttf`, quatre fichiers) ; les fichiers JetBrains Mono, Tahoma et Arial Narrow presents dans certains dossiers de sortie viennent d'un contenu intermediaire perime non suivi (`MGUI.Core/Content/bin/DesktopGL`) et n'existent pas sur une copie propre du depot.
- Docking de l'editeur : sauvegarde et reinitialisation du layout (`DockLayoutSerializer`), volets fermables avec un menu « View » (tout panneau devra alors passer par `RegisterPanel`), flottement du volet « Preview » (deplacer la racine `Window` de preview entre fenetres imbriquees), plusieurs documents ouverts.
