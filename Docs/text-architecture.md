# Architecture texte

## Objectif / Portee

Etat actuel du pipeline texte de MGUI : surface d'edition (`MGTextBox`, `MGRichTextBox`), primitives d'edition (`MGUI.Core/UI/TextEditing/`), coloration syntaxique et completion par providers, undo/redo, selection, et systeme d'invalidation de texte stable de `MGTextBlock`. Le rendu bas niveau et le layout general sont couverts par Docs/rendering-architecture.md et Docs/layout-architecture.md.

## Vue d'ensemble

```text
MGTextBlock       rendu de runs + contrat d'invalidation (MGTextInvalidationMode)
     ^
MGTextBox         unique surface d'edition : caret, selection, clipboard, undo/redo, saisie clavier
     ^
MGRichTextBox     texte brut + spans de style non destructifs, highlighting/completion par providers
     |
MGUI.Core/UI/TextEditing/   primitives pures : buffer, ranges, selection, undo, services de completion
```

MGUI n'est pas une replique WPF : pas de `FlowDocument`, pas de document objet. L'editeur riche reste un controle texte brut avec des styles par plages, pense pour le tooling de moteur de jeu (snippets de script, prompts, logs selectionnables, petits editeurs de code).

## Surface d'edition : MGTextBox

`MGUI.Core/UI/MGTextBox.cs` est la seule surface d'edition texte du framework. Elle possede :

- caret (`Caret`), selection (`CurrentSelection`, `TextSelection`), clipboard, `AcceptsReturn`/`AcceptsTab`, `CharacterLimit` ;
- visibilite du caret : `MGTextCaret.ShouldDrawCaret` porte la regle une seule fois, et `Draw` comme `IsCurrentlyVisible` s'y adressent. Sans position, jamais dessine ; avec le focus clavier, il clignote a `BlinkRate` ; sans le focus, il n'est dessine que si `ShowWhenUnfocused` est vrai, et alors sans clignoter, un caret clignotant sans focus se lisant comme un caret actif. `ShowWhenUnfocused` est faux par defaut : un outil qui deplace le caret sans prendre le focus (l'editeur XAML) l'active sur son propre volet. `IsCurrentlyVisible` existe parce qu'un test headless dessine dans une transaction sans effet et ne peut pas inspecter de pixels ;
- rendu de la selection par injection de codes markdown couleur dans le `MGTextBlock` interne (`UpdateFormattedText`, virtuelle) ;
- undo/redo : `TryUndo()`/`TryRedo()` sur deux `MGTextUndoStack<RestorableState>`, taille via `UndoRedoHistorySize` (`DefaultUndoRedoHistorySize` = 20) ; toute edition hors undo/redo vide la pile redo.

Invariant : la logique clavier partageable est extraite dans `MGUI.Core/UI/TextEditing/MGTextEditingInputHelpers.cs` (`ShouldPreserveTextEntryKey`, `ShouldProcessRepeatedKey`, `IsControlShortcutKey`, `NormalizeEditableCaretIndex`) et la pile undo generique dans `MGTextUndoStack<T>`. `MGTextBox` les consomme via des forwards statiques ; ne pas re-inliner ces helpers dans le controle.

## MGRichTextBox

`MGUI.Core/UI/MGRichTextBox.cs` derive de `MGTextBox` et reutilise integralement son pipeline input/caret/undo. Contrats :

- **Source texte brut** : `SetText` normalise les fins de ligne (`MGTextBuffer.NormalizeLineEndings`) et synchronise `TextBuffer` (dont `Version` s'incremente a chaque edit). Aucun markdown n'est jamais injecte dans `Text`.
- **Styles non destructifs** : `SetStyledSpans`/`ClearStyledSpans` posent des `MGStyledTextSpan` (`MGTextRange` + `MGRichTextStyle`) clampes et tries. `UpdateFormattedText` est surchargee : au lieu du rendu selection-markdown de `MGTextBox`, elle construit des runs (`BuildStyledTextRuns`) via `MGTextBlock.SetTextRuns`, en fusionnant le fond de selection par-dessus les styles des spans.
- **Edition programmatique** : `ApplyTextEdit(MGTextRange, string)` retourne un `MGTextEditResult` et repositionne le caret ; `CaretIndex` et `SelectionState` (`MGTextSelectionState`) exposent l'etat d'edition en index absolus.
- **XAML** : l'element `<RichTextBox>` (`MGUI.Core/UI/XAML/Controls.cs`, classe `RichTextBox`) mappe vers `MGRichTextBox` et ajoute `ShowLineNumbers` + `TabSize` aux reglages de `TextBox`. `MGElementType.RichTextBox` existe.
- **Sample** : `SCN-EDITOR-RTB-001`, `MGUI.Samples/Features/EditorRichTextBox.xaml(.cs)` (voir Docs/scenario-validation-index.md).

```xaml
<RichTextBox Name="EditorTextBox" MinLines="16" WrapText="False"
             ShowLineNumbers="True" TabSize="4" />
```

## Primitives (MGUI.Core/UI/TextEditing)

- `MGTextBuffer` : texte + index de lignes (`LineStarts`, `LineCount`), `Version`, `ApplyEdit`/`Insert`/`Delete`/`Replace`, conversions index <-> `MGTextPosition` (`GetPosition`, `GetIndex`), `GetVisualColumn(position, tabSize)`, snapshots (`CreateSnapshot`/`RestoreSnapshot`).
- `MGTextRange` (`FromStartAndLength`, `Normalize`, `Clamp`), `MGTextPosition`, `MGTextLineSpan`, `MGTextEditResult`.
- `MGTextSelectionState` : `EmptyAt`, `SelectRange`, `ExtendTo`, `MoveAfterEdit`, `Clamp` — etat de selection pur, sans dependance UI.
- `MGRichTextEditController` (internal) : controleur d'edition headless (buffer + selection + undo/redo propres) utilisable par tests et services sans instancier le controle.

## Coloration syntaxique

Contrat provider : `IRichTextSyntaxHighlighter.Highlight(MGRichTextHighlightContext)` ou le contexte est `(Text, Version, Palette)` et le resultat `MGRichTextHighlightResult(Version, Spans)`.

Invariant de version : `MGRichTextBox.RefreshSyntaxHighlighting()` n'applique le resultat que si `result.Version == TextBuffer.Version` (un resultat construit sur un texte perime est ignore).

`MGRichTextSyntaxPalette` porte les couleurs (`Keyword`, `String`, `Comment`, `Number`, `TypeName`, et pour le XAML `ElementName`, `AttributeName`, `Punctuation`, `MarkupExtension` ; instance `Default`). Highlighters integres, volontairement lexicaux (demos, pas des services de langage) : `CSharpRichTextSyntaxHighlighter`, `PlainTextSyntaxHighlighter`, `XamlSyntaxHighlighter`.

Coloration XAML : `MGUI.Core/UI/TextEditing/Xaml/` porte un tokenizer tolerant (`XamlTokenizer.Tokenize`, jamais d'exception, meme sur `<`, `<Button` ou `<Button Width="`) et `XamlSyntaxHighlighter`, qui rend un span par token, sans recouvrement, chaque span portant une classification stable (`xaml.element-name`, `xaml.attribute-name`, `xaml.punctuation`, `xaml.string`, `xaml.comment`, `xaml.markup-extension`). Un controle n'accepte qu'un highlighter : pour ajouter des marqueurs par-dessus la coloration, on decore le highlighter (l'editeur XAML le fait pour souligner ses diagnostics) plutot que d'en poser un second.

Edition programmee annulable : `MGRichTextBox.ApplyTextEdit` empile un etat d'annulation avant d'appliquer l'edition, via un point d'extension non public de `MGTextBox` (`PushUndoState`). `TryUndo` rend donc le texte et le caret d'avant une edition programmee, acceptation de completion comprise, puisqu'elle passe par le meme chemin.

```csharp
public sealed class TodoHighlighter : IRichTextSyntaxHighlighter
{
    public MGRichTextHighlightResult Highlight(MGRichTextHighlightContext context)
    {
        List<MGStyledTextSpan> spans = new();
        int index = context.Text.IndexOf("TODO", StringComparison.Ordinal);
        if (index >= 0)
        {
            MGTextRange range = MGTextRange.FromStartAndLength(index, 4);
            MGRichTextStyle style = new(Foreground: context.Palette.Keyword, IsBold: true);
            spans.Add(new MGStyledTextSpan(range, style, "todo"));
        }
        return new MGRichTextHighlightResult(context.Version, spans);
    }
}
```

## Completion

Contrat provider : `IRichTextCompletionProvider.GetCompletions(MGRichTextCompletionContext)` retourne `MGRichTextCompletionResult(Version, ReplacementRange, Items)`. `MGRichTextCompletionItem` porte `Label`, `InsertText`, `Detail`, `Kind` (et `TextToInsert = InsertText ?? Label`).

`MGRichTextCompletionService` (statique) fournit les briques : `CreateContext` (calcule le prefixe identifiant a gauche du caret et son `ReplacementRange`), `FilterByPrefix` (filtre `StartsWith` insensible a la casse, sans tri), `CreateAcceptance` (caret place en fin d'insertion via `NewCaretIndex`).

Flux dans `MGRichTextBox` :

- `RequestCompletions(trigger, triggerCharacter)` construit le contexte et appelle le provider ;
- `OpenCompletionPopup(trigger, triggerCharacter)` ou `CompletionPopup.Open(result)` ouvrent le modele d'etat ;
- `MoveCompletionSelection(delta)`, `AcceptSelectedCompletion()` (applique `ApplyTextEdit` + `NewCaretIndex`), `CloseCompletionPopup()`.

`MGRichTextCompletionPopupController` est un modele d'etat UI-agnostique et testable (`IsOpen`, `Result`, `SelectedIndex`/`SelectedItem`, `TryAcceptSelected`, `TryHandleDismissKey`). Echap est cable dans `MGRichTextBox` (`KeyboardHandler.Pressed`/`KeyRepeat` -> `TryHandleDismissKey`) et ferme le popup sans editer le texte. Les hotes rendent la liste eux-memes ; le sample utilise une `MGListBox`. Provider demo integre : `CSharpKeywordCompletionProvider`.

```csharp
MGRichTextBox editor = Window.GetElementByName<MGRichTextBox>("EditorTextBox");
editor.SyntaxHighlighter = new CSharpRichTextSyntaxHighlighter();
editor.CompletionProvider = new CSharpKeywordCompletionProvider();
editor.SetText("using System;\n\npublic cla");

MGRichTextCompletionResult result = editor.RequestCompletions();
editor.CompletionPopup.Open(result);
editor.MoveCompletionSelection(1);
editor.AcceptSelectedCompletion();
```

## Invalidation de texte stable (MGTextBlock)

`MGUI.Core/UI/MGTextInvalidationMode.cs` definit trois niveaux :

- `RelayoutParent` : defaut sur. Labels ordinaires, texte wrappe ou localise, premier layout, largeur inconnue, changements de font/theme/padding — tout update pouvant changer la taille desiree.
- `ReflowLocal` : reparse les lignes locales dans les bornes de layout courantes ; n'escalade vers le layout parent que si la comparaison de taille desiree detecte un vrai changement.
- `ContentOnly` : pour les updates dont l'empreinte rendue est garantie inchangee ; le framework garde les donnees de rendu a jour et escalade quand meme si le contrat n'est pas respecte.

Opt-in obligatoire pour les labels temps reel : `HasStableTextFootprint = true`, empreinte reservee (`MinLines`/`MaxLines`/`PreferredWidth`, layout hote fixe ou format naturellement a largeur fixe), puis `SetText(text, MGTextInvalidationMode.ReflowLocal)`.

```csharp
MGTextBlock fpsText = new(window, "FPS: 000")
{
    HasStableTextFootprint = true,
    MinLines = 1
};
fpsText.SetText($"FPS: {fps:000}", MGTextInvalidationMode.ReflowLocal);
```

Invariants d'escalade (`MGUI.Core/UI/MGTextBlock.cs`, `ResolveTextInvalidationMode` / `ApplyTextMutation`) :

- sans `HasStableTextFootprint`, les modes stables retombent sur `RelayoutParent` ;
- l'escalade est pilotee par une comparaison reelle de taille desiree ; en cas de doute, elle escalade volontairement ;
- le cache de mesure (`RecentSelfMeasurements`) n'est vide que quand un relayout parent est reellement necessaire.

API : `SetText`, `SetTextRuns` et `ClearTextRuns` ont chacun une surcharge `MGTextInvalidationMode` ; la propriete `Text` garde le defaut sur `RelayoutParent`. Les surcharges legacy `bool SuppressLayoutChanged` n'existent que pour compatibilite : interdites dans le nouveau code.

Ne pas utiliser les modes stables pour : prose, labels traduits, wrapping libre, changements de font/theme, controles dont le layout n'est pas acheve.

Call sites stables existants (garder l'API explicite) : `MGUI.Core/UI/MGProgressBar.cs`, `MGStopWatch.cs`, `MGTimer.cs`, `MGPropertyGrid.cs` (affichage read-only), HUD `MGUI.MiniGame/MiniGame.cs`, overlays perf des samples (`PerformanceTest.xaml.cs`, `ListBox.xaml.cs`). Regression : `MGUI.Tests/Text/TextBlockInvalidationTests.cs` (dont `StableTelemetryCounter_CanUpdateRepeatedlyWithoutRelayoutParent`).

## Texte sans allocation (MGTextBlock)

Un texte qui change a chaque image (compteur, valeur d'un slider, telemetrie) peut etre change, mesure, decoupe en lignes et dessine sans aucune allocation (ADR-0026, `Docs/decisions/0026-text-as-a-span-of-characters.md`).

Usage : garder les caracteres dans un tampon de l'appelant et les passer par `SetText(ReadOnlySpan<char>)`. Une `string` construite par l'appelant (`$"..."`, `ToString`) est deja une allocation, quel que soit le chemin pris ensuite.

```csharp
private readonly char[] _valueChars = new char[32];

if (value.TryFormat(_valueChars, out int written, "F2", CultureInfo.InvariantCulture))
{
    valueText.SetText(_valueChars.AsSpan(0, written), MGTextInvalidationMode.ReflowLocal);
}
```

- `SetText(ReadOnlySpan<char>)` equivaut a `Text = ...` (`RelayoutParent`) ; `SetText(ReadOnlySpan<char>, MGTextInvalidationMode)` suit les regles de la section precedente. Le bloc copie les caracteres : le tampon de l'appelant est reutilisable des le retour.
- `Text`, `Runs` et `Lines` restent disponibles. Ils sont construits a la lecture : les lire apres chaque changement realloue (une `string`, des `MGTextRun`, des `MGTextLine`). Ne pas les lire dans un chemin chaud.
- Moteurs de texte : `SpriteFontTextEngine`, `FontStashSharpTextEngine` et `DrawTransaction` mesurent et dessinent un `ReadOnlySpan<char>` sans allouer. Un autre moteur ou un autre contexte de dessin herite de l'implementation par defaut, qui construit une `string` par appel : il doit redefinir les surcharges `ReadOnlySpan<char>` pour garder le zero.

Ce qui est a zero allocation, apres echauffement (`MGUI.Core/UI/MGTextBlock.cs`, `MGUI.Core/UI/Text/PlainTextLayout.cs`, `FormattedTextScanner.cs`, `FormattedTextRuns.cs`, `FormattedTextLayout.cs`, `TextBoundsTracker.cs`) :

- texte sans balise (`AllowsInlineFormatting == false`) : changement, mesure, lignes, dessin (ombre et `TextProgress` compris) ;
- texte a balises : changement (`string` ou span), mesure, lignes, invalidations locales, dessin avec fond, soulignement, ombre, opacite, images et retour a la ligne ;
- revelation progressive par ecriture directe de `TextProgress` a chaque image ;
- bornes des actions et des infobulles, reconstruites a chaque dessin, et leur recherche (clic, survol).

Limites (ces cas allouent) :

- la premiere fois qu'un bloc voit une valeur de balise (couleur, brosse, soulignement, ombre, image, nom d'infobulle ou d'action) : la valeur est interpretee par le code historique puis gardee dans un cache du bloc ;
- un bloc qui fait tourner plus de 128 valeurs distinctes d'une meme sorte, ou plus de 64 noms d'infobulles ou d'actions : le cache plein est vide puis rempli de nouveau ;
- un balisage que le lecteur sans allocation ne lit pas, laisse au code historique : balisage invalide (le code historique leve la meme exception qu'avant), caractere non ASCII la ou la grammaire compare des lettres ou des chiffres, image sans taille quand le bloc n'a pas de bureau, et, si le processus a demarre sous une culture turque ou azerie, un type de balise ecrit avec un `I` majuscule (`[I]`, `[ITALIC]`, `[IMG=...]`) ;
- un bloc sans balise avec `IsUnderlined` ou un fond au niveau du bloc : il repasse par l'ancienne boucle de dessin ;
- `TextCharactersPerSecond` : un `TextRevealRun` est recree a chaque changement de texte ;
- un changement de police : environ 11,7 Ko une fois, donc un zoom qui change la police a chaque image alloue ;
- le reste de l'image du bureau (`MGDesktop`, `MGWindow`, layout du parent apres un `RelayoutParent`) : hors de ce chantier.

Les tampons d'un bloc grandissent avec le plus long texte vu et ne retrecissent pas. Un bloc a balises garde un lecteur, deux tampons de runs et deux layouts, crees au premier usage (environ 4 a 5 Ko par bloc).

`internal bool ForceLegacyTextPath` garde l'ancien pipeline appelable : les tests differentiels comparent les deux chemins (runs, lignes, tailles, appels de dessin, exceptions, evenements, clics).

## Tests

- `MGUI.Tests/Text/RichTextBox*Tests.cs` : buffer, modele d'edition, helpers input, controleur, highlighter, modele de completion, popup, runs styles ; plus `MGUI.Tests/Architecture/RichTextBoxShellTests.cs`.
- `MGUI.Tests/Text/TextBlockInvalidationTests.cs` pour l'invalidation stable.
- `MGUI.Tests/Text/PlainText*Tests.cs`, `FormattedText*Tests.cs`, `TextRevealAllocationTests.cs`, `TextBoundsTrackerTests.cs` et `MGUI.Tests/Integration/TextSpanGpuTests.cs` (GPU reel) pour le texte sans allocation : tests a zero octet, chacun contre-teste, et tests differentiels contre `ForceLegacyTextPath`.
- Filtre utile : `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "RichTextBox|TextEditing|Syntax|Completion"`.

## Limites connues

- Pas de `FlowDocument`/`Run`/`Paragraph`, pas de conversion RTF/HTML/Markdown, pas de LSP ni d'annulation async de completion, pas de virtualisation pour tres grands documents.
- `ShowLineNumbers` et `TabSize` sont declaratifs : aucun rendu de gouttiere de numeros de ligne ni comportement de caret dependant de `TabSize` n'est implemente dans `MGUI.Core` (`MGTextBuffer.GetVisualColumn` existe mais n'est pas branche au controle).
- Aucun controle popup de completion visuel reutilisable dans `MGUI.Core` : seuls le modele d'etat et le rendu ad hoc du sample existent.
- `MGRichTextCompletionContext.TriggerCharacter` est transporte mais aucun declenchement automatique n'est cable dans `MGRichTextBox` (ouverture manuelle uniquement) ; pas de re-filtrage pendant la frappe, pas de tri des suggestions (`FilterByPrefix` filtre sans classer).
- Les providers C# integres sont des demos lexicales, pas des services de langage. Le highlighter XAML l'est aussi : il colore des tokens, il ne valide rien.
- Taper ou effacer un seul caractere sans selection n'empile aucun etat d'annulation dans `MGTextBox` (seules les editions avec selection, le collage, la coupe, la duplication de ligne et les editions programmees en empilent) : `Ctrl+Z` ne defait donc pas une frappe caractere par caractere.
- La validation de performance du scenario particle-preview cote CasaEngine.Editor est externe a ce depot et n'est pas couverte par ses tests.

## Reste a faire

Les travaux restants du theme texte (editeur XAML avec autocomplete, popup visuel, declenchement clavier automatique) sont specifies dans Docs/Tasks/richtextbox-autocomplete-tasks.md.
