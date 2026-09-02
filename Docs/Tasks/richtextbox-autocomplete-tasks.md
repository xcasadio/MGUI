# Taches editeur XAML et autocomplete RichTextBox

## Objectif

Construire, au-dessus du pipeline `MGRichTextBox` existant (voir Docs/text-architecture.md), un editeur XAML avec autocomplete : popup de completion visuel reutilisable, declenchement clavier, tokenizer XAML tolerant, analyse de contexte, schema, service de completion, highlighter, snippets et diagnostics. Regle d'architecture non negociable : toute l'intelligence XAML vit dans des services (tokenizer -> analyzer -> schema -> provider) ; `MGRichTextBox` reste generique et ne connait pas XAML.

Etat du code a la redaction (ne pas recreer, cibler ces types) :

- `MGUI.Core/UI/TextEditing/` fournit deja `MGTextRange`, `MGTextBuffer`, `IRichTextCompletionProvider`, `MGRichTextCompletionContext`/`Result`/`Item`/`Trigger`, `MGRichTextCompletionService`, `MGRichTextCompletionPopupController`, `IRichTextSyntaxHighlighter`, `MGRichTextSyntaxPalette`, `MGStyledTextSpan`, `MGRichTextCompletionAcceptance` (avec `NewCaretIndex`).
- `MGUI.Core/UI/MGRichTextBox.cs` expose `Text`, `CaretIndex`, `SelectionState`, `ApplyTextEdit`, `RequestCompletions`, `OpenCompletionPopup`, `MoveCompletionSelection`, `AcceptSelectedCompletion` ; Echap ferme deja le popup (`TryHandleDismissKey`).
- Ne pas introduire d'abstractions paralleles (`TextSpan`, `ITextEditor`, `ICompletionService` generiques) : les contrats existants couvrent ces besoins.

Emplacement suggere pour les nouvelles briques : `MGUI.Core/UI/TextEditing/Xaml/` (namespace `MGUI.Core.UI.TextEditing.Xaml`), tests dans `MGUI.Tests/Text/`. Nommage sans prefixe MG pour les services/providers, aligne sur `CSharpRichTextSyntaxHighlighter`.

Criteres MVP (taches 1 a 6) : Ctrl+Space ouvre le popup ; `<Bu` propose `Button` et l'acceptation remplace `Bu` ; `<Button Wid` propose `Width` et insere `Width=""` ; `<StackPanel Orientation="` propose `Horizontal`/`Vertical` ; `</` propose la derniere balise ouverte ; Echap ferme, Enter/Tab accepte ; aucun crash sur XAML incomplet.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- Faire exactement 1 commit par tache terminee.
- Mettre a jour le statut de la tache avant chaque commit.
- Si une tache est bloquee : statut ⛔ + description du blocage sous le titre, puis s'arreter.
- Pas de refactor hors perimetre ; ne pas changer les contrats publics existants de `MGTextBox`/`MGRichTextBox` sauf ajout compatible.
- Ajouter des tests a chaque tache contenant de la logique testable.
- Tolerance obligatoire : tokenizer, analyzer, provider et diagnostics ne doivent jamais lever d'exception sur un document XAML incomplet ou invalide (cas minimum : `<`, `<Button`, `<Button Width="`, `<Button Width="{Binding`, `<StackPanel><Button>`, `</`, document vide, caret en debut/fin).
- Aucune logique XAML dans `MGRichTextBox` : uniquement des providers et services injectes.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

1. `dotnet build .\MGUI.Core\MGUI.Core.csproj --no-restore`
2. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "RichTextBox|TextEditing|Completion|Xaml" --logger "console;verbosity=minimal"`
3. Si la tache touche le sample : `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

## Taches

### ⚪ 1. Controle popup de completion visuel reutilisable

But :
fournir dans `MGUI.Core` un controle popup pret a l'emploi, au lieu du rendu ad hoc `MGListBox` du sample `MGUI.Samples/Features/EditorRichTextBox.xaml.cs`.

Travail attendu :

- creer un controle (ex. `MGCompletionPopup`) qui rend l'etat de `MGRichTextCompletionPopupController` : liste verticale, item selectionne surligne, `Label` + `Kind` par ligne, `Detail` de l'item selectionne ;
- se positionner pres du caret (exposer la position ecran du caret depuis `MGRichTextBox` si necessaire) ;
- visibilite pilotee par `IsOpen` du controller ; le popup ne modifie jamais le texte et n'analyse rien ;
- adapter le sample EditorRichTextBox pour utiliser ce controle.

Criteres d'acceptation :

- le popup s'affiche/se cache selon le controller et suit `SelectedIndex` ;
- `Label`, `Kind` et `Detail` sont visibles ;
- tests sur le mapping controller -> contenu rendu (modele testable sans rendu reel si besoin).

Commit recommande : `ui: add reusable completion popup control`

### ⚪ 2. Declenchement et navigation clavier de la completion

But :
rendre la completion utilisable entierement au clavier, sans code hote.

Travail attendu :

- cabler (dans `MGRichTextBox` ou un petit controleur dedie, sans logique XAML) : Ctrl+Space (ouverture manuelle), fleches haut/bas (`MoveCompletionSelection`), Enter/Tab (`AcceptSelectedCompletion` quand le popup est ouvert, comportement normal sinon) ; Echap reste gere par `TryHandleDismissKey` ;
- declenchement automatique : emettre `MGRichTextCompletionTrigger.Character` + `TriggerCharacter` a la frappe (le champ existe dans le contexte mais n'est jamais emis aujourd'hui) ;
- re-filtrage pendant la frappe : quand le popup est ouvert, relancer `RequestCompletions` apres chaque edit, conserver la selection quand c'est possible, fermer si zero item ;
- tri des suggestions (helper dans `MGRichTextCompletionService` ou service dedie) : match exact > commence par > contient (`FilterByPrefix` ne fait aujourd'hui qu'un `StartsWith` sans classement).

Criteres d'acceptation :

- chaque touche listee a le comportement decrit, popup ouvert et ferme ;
- la liste se met a jour pendant la frappe et l'ordre suit le classement ;
- tests unitaires sur le declenchement, le re-filtrage et le tri.

Commit recommande : `input: wire completion triggers and keyboard navigation`

### ⚪ 3. Tokenizer XAML tolerant

But :
produire des tokens XAML reutilisables par highlighter, analyzer, diagnostics et completion.

Travail attendu :

- `XamlTokenKind` (LessThan, GreaterThan, Slash, Equals, Identifier, String, Whitespace, Text, OpenBrace, CloseBrace, Colon, Dot, Comment, Unknown, EndOfFile), `XamlToken` avec span en `MGTextRange` ;
- `XamlTokenizer.Tokenize(string)` : reconnait `< > / = " ' : . { }`, identifiants, strings (double et simple quote), commentaires, whitespace, texte libre ; jamais d'exception, tokens `Unknown` si necessaire ;
- pas de parsing incremental : retokenizer tout le document (mesurer avant d'optimiser).

Criteres d'acceptation :

- kinds, spans et textes corrects sur : `<Button />`, `<Button Width="100" Height="50" />`, balises imbriquees, `<!-- comment -->`, `<TextBlock Text="{Binding Name}" />` ;
- aucun crash sur les cas incomplets de la section consignes.

Commit recommande : `feat: add tolerant xaml tokenizer`

### ⚪ 4. Analyseur de contexte XAML

But :
determiner le mode de completion a partir du texte et du caret.

Travail attendu :

- `XamlCompletionMode` (None, ElementName, AttributeName, AttributeValue, ClosingElement, NamespacePrefix, MarkupExtension, Snippet) et `XamlCompletionContext` (Mode, CurrentWord, CurrentElementName, CurrentAttributeName, ReplaceRange en `MGTextRange`) ;
- `XamlContextAnalyzer.Analyze(string text, int caretIndex)` base sur `XamlTokenizer` : approche pragmatique par fenetre de tokens autour du caret, pas de parseur XML complet.

Criteres d'acceptation (tests des 8 cas) :

- `<` -> ElementName ; `<But` -> ElementName, CurrentWord=But, ReplaceRange couvre `But` ;
- `<Button ` -> AttributeName, CurrentElementName=Button ; `<Button Wid` -> AttributeName, CurrentWord=Wid ;
- `<Button HorizontalAlignment="` -> AttributeValue, CurrentAttributeName=HorizontalAlignment ;
- `</` -> ClosingElement ; `<TextBlock Text="{Bin` -> MarkupExtension, CurrentWord=Bin ; `xmlns:` -> NamespacePrefix ;
- jamais d'exception avec le caret en debut/fin de document.

Commit recommande : `feat: add xaml completion context analyzer`

### ⚪ 5. Schema XAML manuel

But :
decrire les types, proprietes et enums proposables, sans reflexion pour commencer.

Travail attendu :

- `XamlTypeDescriptor`, `XamlPropertyDescriptor` (TypeName, IsAttachedProperty, IsCollection, EnumValues), `IXamlSchemaProvider` (`GetTypes`, `FindType`, `GetProperties`), `ManualXamlSchemaProvider` ;
- contenu minimal aligne sur les vrais elements XAML de MGUI (`MGUI.Core/UI/XAML/Controls.cs`), pas sur WPF : Button, TextBlock, TextBox, RichTextBox, Grid, StackPanel, Image, Border, CheckBox, ScrollViewer ; proprietes communes (Name, Width, Height, Min/Max, Margin, Padding, HorizontalAlignment, VerticalAlignment, Background, IsEnabled...) ; proprietes specifiques et enums (HorizontalAlignment, VerticalAlignment, Orientation, Stretch...) ;
- recherche case-insensitive.

Criteres d'acceptation :

- lookup par nom exact et par prefixe ; proprietes retournees pour un type connu, liste vide pour un type inconnu ;
- les valeurs d'enum sont accessibles depuis la propriete ;
- tests de lookup.

Commit recommande : `feat: add manual xaml schema provider`

### ⚪ 6. Provider de completion XAML

But :
brancher l'intelligence XAML sur le contrat generique existant.

Travail attendu :

- `XamlCompletionProvider` implementant `IRichTextCompletionProvider`, composant `XamlTokenizer` + `XamlContextAnalyzer` + `IXamlSchemaProvider` ;
- modes geres : elements (`<Bu` -> `Button`), attributs (insertion `Name=""`), valeurs d'enum, balise fermante (pile des balises ouvertes, ignorer self-closing et balises deja fermees), markup extensions (`Binding`, `StaticResource` pour commencer) ;
- `ReplacementRange` du resultat base sur le `ReplaceRange` du contexte XAML, pas sur le simple prefixe identifiant de `MGRichTextCompletionService.CreateContext` ;
- exclure les attributs deja presents dans la balise courante.

Criteres d'acceptation :

- les criteres MVP de la section Objectif passent en tests unitaires (provider appele directement, sans UI) ;
- resultat vide plutot que crash sur XAML incomplet ;
- accepter `Button` apres `<Bu` produit `<Button` (pas `<BuButton`).

Commit recommande : `feat: add xaml completion provider`

### ⚪ 7. Highlighter XAML

But :
colorer le XAML avec le pipeline de spans non destructifs existant.

Travail attendu :

- `XamlSyntaxHighlighter` implementant `IRichTextSyntaxHighlighter`, base sur `XamlTokenizer` ;
- mapping tokens -> `MGRichTextStyle` : ponctuation, nom d'element, nom d'attribut, string, commentaire, markup extension (etendre `MGRichTextSyntaxPalette` ou introduire une palette XAML dediee) ; version minimale acceptable : identifiants colores uniformement si la distinction element/attribut est couteuse ;
- respecter l'invariant de version (retourner `context.Version`) ; ne jamais modifier le texte ;
- brancher un mode XAML dans le sample EditorRichTextBox.

Criteres d'acceptation :

- spans corrects sur balise, attribut, string, commentaire, markup extension ;
- aucun crash sur XAML incomplet ;
- tests unitaires + sample compilant.

Commit recommande : `feat: add xaml syntax highlighter`

### ⚪ 8. Snippets et placement du caret

But :
inserer des blocs XAML avec positionnement du caret.

Travail attendu :

- support d'un marqueur de caret (ex. `$0`) dans le texte a inserer : etendre la creation de `MGRichTextCompletionAcceptance` pour retirer le marqueur et calculer `NewCaretIndex` a sa position (aujourd'hui le caret est toujours place en fin d'insertion) ;
- `XamlSnippet` + `XamlSnippetProvider` (Grid, StackPanel vertical, Button, TextBlock) exposes comme items de completion en mode ElementName, filtres par nom ;
- appliquer le marqueur a la completion d'attribut : accepter `Width` insere `Width=""` avec le caret entre les guillemets.

Criteres d'acceptation :

- `Width="$0"` insere `Width=""` et place le caret entre les guillemets ;
- un snippet multi-ligne insere son bloc et positionne le caret au `$0` ;
- le marqueur n'apparait jamais dans le texte final ; tests.

Commit recommande : `feat: add completion caret marker and xaml snippets`

### ⚪ 9. Diagnostics XAML basiques

But :
signaler les erreurs simples sans bloquer l'edition.

Travail attendu :

- `XamlDiagnostic` (`MGTextRange`, severite, message) + `XamlDiagnosticSeverity` (Info, Warning, Error) ;
- detections tolerantes : balise ouverte non fermee, guillemet non ferme, attribut sans valeur, element inconnu du schema, propriete inconnue ;
- exposition en liste et/ou en spans styles (soulignement) via le pipeline `MGStyledTextSpan` — sans jamais bloquer ni ralentir la saisie.

Criteres d'acceptation :

- chaque detection listee a un test ; aucun faux crash sur XAML incomplet ;
- l'edition reste possible avec des diagnostics presents.

Commit recommande : `feat: add basic xaml diagnostics`

### ⚪ 10. Schema par reflexion

But :
generer le schema depuis les types XAML reels de MGUI. Ne demarrer qu'apres validation des taches 5 et 6 avec le schema manuel.

Travail attendu :

- `ReflectionXamlSchemaProvider` construisant les descriptors depuis les classes d'elements de `MGUI.Core/UI/XAML/Controls.cs` (types concrets derives de la classe de base des elements XAML), proprietes publiques settables, detection des enums et de leurs valeurs ;
- `CombinedXamlSchemaProvider` fusionnant manuel + reflexion sans doublon, priorite configurable (manuel prioritaire par defaut) ;
- ne pas supprimer le provider manuel.

Criteres d'acceptation :

- le provider par reflexion retrouve au moins les types couverts par le schema manuel, avec leurs enums ;
- le provider combine ne retourne pas de doublons ; tests.

Commit recommande : `feat: add reflection based xaml schema provider`
