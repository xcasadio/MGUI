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

`MGRichTextSyntaxPalette` porte les couleurs (`Keyword`, `String`, `Comment`, `Number`, `TypeName` ; instance `Default`). Highlighters integres, volontairement lexicaux (demos, pas des services de langage) : `CSharpRichTextSyntaxHighlighter`, `PlainTextSyntaxHighlighter`.

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

## Tests

- `MGUI.Tests/Text/RichTextBox*Tests.cs` : buffer, modele d'edition, helpers input, controleur, highlighter, modele de completion, popup, runs styles ; plus `MGUI.Tests/Architecture/RichTextBoxShellTests.cs`.
- `MGUI.Tests/Text/TextBlockInvalidationTests.cs` pour l'invalidation stable.
- Filtre utile : `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "RichTextBox|TextEditing|Syntax|Completion"`.

## Limites connues

- Pas de `FlowDocument`/`Run`/`Paragraph`, pas de conversion RTF/HTML/Markdown, pas de LSP ni d'annulation async de completion, pas de virtualisation pour tres grands documents.
- `ShowLineNumbers` et `TabSize` sont declaratifs : aucun rendu de gouttiere de numeros de ligne ni comportement de caret dependant de `TabSize` n'est implemente dans `MGUI.Core` (`MGTextBuffer.GetVisualColumn` existe mais n'est pas branche au controle).
- Aucun controle popup de completion visuel reutilisable dans `MGUI.Core` : seuls le modele d'etat et le rendu ad hoc du sample existent.
- `MGRichTextCompletionContext.TriggerCharacter` est transporte mais aucun declenchement automatique n'est cable dans `MGRichTextBox` (ouverture manuelle uniquement) ; pas de re-filtrage pendant la frappe, pas de tri des suggestions (`FilterByPrefix` filtre sans classer).
- Les providers C# integres sont des demos lexicales, pas des services de langage.
- La validation de performance du scenario particle-preview cote CasaEngine.Editor est externe a ce depot et n'est pas couverte par ses tests.

## Reste a faire

Les travaux restants du theme texte (editeur XAML avec autocomplete, popup visuel, declenchement clavier automatique) sont specifies dans Docs/Tasks/richtextbox-autocomplete-tasks.md.
