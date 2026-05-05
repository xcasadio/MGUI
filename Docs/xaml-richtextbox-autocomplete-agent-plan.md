# Plan agent IA — RichTextBox avec autocomplétion pour éditeur XAML MGUI

## 0. Objectif global

Créer une base robuste pour un éditeur XAML intégré à MGUI, basé sur un `RichTextBox` capable de :

- éditer du texte multi-ligne ;
- afficher une coloration syntaxique XAML ;
- gérer le curseur, la sélection, le scroll et l’insertion de texte ;
- déclencher une autocomplétion contextuelle ;
- proposer des éléments XAML, attributs, valeurs d’énumération, namespaces, markup extensions et snippets ;
- rester générique afin que le système d’autocomplétion puisse être réutilisé plus tard pour d’autres langages ou éditeurs.

Le point essentiel est de ne pas mélanger la logique XAML avec le contrôle `RichTextBox`.

Le `RichTextBox` doit rester un composant d’édition texte.  
L’intelligence XAML doit être portée par des services séparés.

---

## 1. Contraintes générales obligatoires

### 1.1. Contraintes d’architecture

L’agent doit respecter strictement ces règles :

- Le `RichTextBox` ne doit pas connaître XAML.
- Le `RichTextBox` ne doit pas contenir de logique de parsing XAML.
- Le `RichTextBox` ne doit pas contenir de liste hardcodée de contrôles XAML.
- Le `RichTextBox` ne doit pas décider quels éléments afficher dans l’autocomplétion.
- Le `RichTextBox` peut seulement exposer :
  - le texte ;
  - la position du curseur ;
  - la sélection ;
  - les méthodes d’insertion/remplacement ;
  - la position écran du curseur.
- La coloration syntaxique doit être séparée de l’autocomplétion.
- Le tokenizer XAML doit être réutilisable par :
  - le highlighter ;
  - le context analyzer ;
  - les diagnostics simples ;
  - l’autocomplétion.
- L’autocomplétion doit passer par une interface générique.
- Le système XAML doit être implémenté comme une spécialisation du système générique.

---

### 1.2. Contraintes de code

L’agent doit :

- écrire du C# lisible ;
- utiliser des noms explicites ;
- éviter les abréviations inutiles ;
- éviter les méthodes géantes ;
- préférer des petites classes à responsabilité unique ;
- ajouter des commentaires seulement quand la logique n’est pas évidente ;
- éviter toute optimisation prématurée ;
- ne pas ajouter de dépendance externe sans justification explicite ;
- conserver les conventions de nommage déjà présentes dans MGUI ;
- éviter de modifier des systèmes existants non liés à cette tâche ;
- s’assurer que le projet compile après chaque tâche ;
- faire un commit après chaque tâche complète.

---

### 1.3. Contraintes de commit

Après chaque tâche terminée, l’agent doit faire un commit.

Format recommandé :

```text
[RichTextBox] Add text editor abstraction
[XamlEditor] Add tokenizer
[XamlEditor] Add completion context analyzer
[XamlEditor] Add XAML element completion
```

Chaque commit doit :

- être petit ;
- compiler ;
- ne contenir qu’un seul changement logique ;
- inclure les tests associés si la tâche en ajoute ;
- ne pas mélanger refactor et fonctionnalité sauf si la tâche le demande explicitement.

---

### 1.4. Règle de non-régression

Avant chaque commit, l’agent doit vérifier :

- compilation réussie ;
- pas d’erreurs de formatage évidentes ;
- pas de fichiers générés inutiles ;
- pas de code mort volontaire ;
- pas de `TODO` ajouté sans raison ;
- pas de changement hors périmètre.

---

## 2. Architecture cible

### 2.1. Vue globale

```text
RichTextBox
 ├─ TextBuffer
 ├─ Caret
 ├─ Selection
 ├─ Rendering
 ├─ Scroll
 └─ Events
      │
      ▼
TextCompletionController
 ├─ lit le texte
 ├─ lit le caret
 ├─ demande les suggestions
 ├─ affiche/cache le popup
 └─ applique la suggestion sélectionnée
      │
      ▼
ICompletionService
      │
      ▼
XamlCompletionService
 ├─ XamlTokenizer
 ├─ XamlContextAnalyzer
 ├─ XamlSchema
 ├─ CompletionItemProvider
 └─ SnippetProvider
```

---

### 2.2. Séparation des responsabilités

#### RichTextBox

Responsabilités :

- stockage ou liaison avec un texte éditable ;
- saisie clavier ;
- sélection ;
- curseur ;
- scroll ;
- rendu des lignes ;
- rendu des styles textuels ;
- conversion index texte ↔ position écran ;
- notification des changements.

Ne doit pas faire :

- parsing XAML ;
- autocomplétion XAML ;
- validation XAML ;
- génération de snippets XAML ;
- introspection des types MGUI.

---

#### TextCompletionController

Responsabilités :

- écouter les événements du `RichTextBox` ;
- construire un `CompletionContext` ;
- appeler `ICompletionService` ;
- afficher le `CompletionPopup` ;
- gérer navigation clavier dans la liste ;
- appliquer une suggestion.

Ne doit pas faire :

- analyser XAML directement ;
- connaître les classes MGUI ;
- construire les suggestions manuellement.

---

#### CompletionPopup

Responsabilités :

- afficher une liste de suggestions ;
- gérer sélection courante ;
- supporter clavier :
  - flèche haut ;
  - flèche bas ;
  - Entrée ;
  - Tab ;
  - Échap ;
- se positionner près du curseur ;
- afficher :
  - icône/type ;
  - texte ;
  - description courte.

Ne doit pas faire :

- modifier directement le texte ;
- analyser le contexte ;
- construire les suggestions.

---

#### XamlCompletionService

Responsabilités :

- analyser le texte autour du curseur ;
- déterminer le mode de complétion ;
- demander les types/propriétés au schéma ;
- créer des `CompletionItem` ;
- calculer le `ReplaceSpan`.

---

#### XamlSchema

Responsabilités :

- connaître les types XAML disponibles ;
- connaître les propriétés disponibles ;
- connaître les valeurs d’énumération ;
- connaître les namespaces et préfixes ;
- fournir une API de recherche simple.

---

## 3. Structure de dossiers proposée

Adapter les chemins selon l’organisation réelle du repo, mais garder cette séparation logique.

```text
MGUI.Core/
  UI/
    Text/
      RichTextBox.cs
      TextBuffer.cs
      TextCaret.cs
      TextSelection.cs
      TextSpan.cs
      TextLine.cs
      TextPosition.cs
      TextDocument.cs

    Text/Completion/
      ITextEditor.cs
      ICompletionService.cs
      CompletionContext.cs
      CompletionResult.cs
      CompletionItem.cs
      CompletionItemKind.cs
      CompletionTriggerKind.cs
      TextCompletionController.cs
      CompletionPopup.cs

    Text/Highlighting/
      ISyntaxHighlighter.cs
      SyntaxHighlightResult.cs
      TextStyleSpan.cs

    Xaml/
      XamlTokenizer.cs
      XamlToken.cs
      XamlTokenKind.cs
      XamlTokenList.cs
      XamlContextAnalyzer.cs
      XamlCompletionMode.cs
      XamlCompletionContext.cs
      XamlCompletionService.cs
      XamlSyntaxHighlighter.cs
      XamlDiagnostic.cs
      XamlDiagnosticSeverity.cs

    Xaml/Schema/
      XamlSchema.cs
      XamlTypeDescriptor.cs
      XamlPropertyDescriptor.cs
      XamlNamespaceDescriptor.cs
      XamlEnumDescriptor.cs
      IXamlSchemaProvider.cs
      ManualXamlSchemaProvider.cs
      ReflectionXamlSchemaProvider.cs

    Xaml/Snippets/
      XamlSnippet.cs
      XamlSnippetProvider.cs
```

---

## 4. Interfaces de base à créer

### 4.1. ITextEditor

But : fournir au système d’autocomplétion une interface minimale vers l’éditeur.

```csharp
public interface ITextEditor
{
    string Text { get; }
    int CaretIndex { get; }
    TextSpan Selection { get; }

    void InsertText(string text);
    void ReplaceText(TextSpan span, string text);
    void MoveCaretTo(int index);

    Point GetCaretScreenPosition();
}
```

Notes :

- `Point` doit être remplacé par le type déjà utilisé dans MGUI si nécessaire.
- `TextSpan` doit représenter un intervalle texte.
- `CaretIndex` doit être un index absolu dans le document.

---

### 4.2. TextSpan

```csharp
public readonly struct TextSpan
{
    public int Start { get; }
    public int Length { get; }
    public int End => Start + Length;

    public TextSpan(int start, int length)
    {
        Start = start;
        Length = length;
    }
}
```

Règles :

- `Start` doit toujours être positif ou nul.
- `Length` doit toujours être positif ou nul.
- `End` ne doit pas dépasser la longueur du document lors de l’utilisation.

---

### 4.3. CompletionItem

```csharp
public sealed class CompletionItem
{
    public string DisplayText { get; init; } = "";
    public string InsertText { get; init; } = "";
    public string Description { get; init; } = "";
    public CompletionItemKind Kind { get; init; }
    public TextSpan ReplaceSpan { get; init; }
}
```

---

### 4.4. CompletionItemKind

```csharp
public enum CompletionItemKind
{
    Element,
    Attribute,
    AttributeValue,
    EnumValue,
    Namespace,
    NamespacePrefix,
    MarkupExtension,
    Snippet,
    ClosingElement,
    Keyword
}
```

---

### 4.5. CompletionResult

```csharp
public sealed class CompletionResult
{
    public static readonly CompletionResult Empty = new();

    public List<CompletionItem> Items { get; } = new();
    public bool ShouldShowPopup => Items.Count > 0;
}
```

---

### 4.6. ICompletionService

```csharp
public interface ICompletionService
{
    CompletionResult GetCompletions(CompletionContext context);
}
```

---

### 4.7. CompletionContext

```csharp
public sealed class CompletionContext
{
    public string Text { get; init; } = "";
    public int CaretIndex { get; init; }
    public CompletionTriggerKind TriggerKind { get; init; }
}
```

---

### 4.8. CompletionTriggerKind

```csharp
public enum CompletionTriggerKind
{
    Manual,
    TextInput,
    CharacterTyped,
    Backspace,
    Delete,
    CaretMoved
}
```

---

## 5. Modèle XAML minimal

### 5.1. XamlTokenKind

```csharp
public enum XamlTokenKind
{
    Unknown,
    LessThan,
    GreaterThan,
    Slash,
    Equals,
    Quote,
    Identifier,
    String,
    Whitespace,
    Text,
    OpenBrace,
    CloseBrace,
    Colon,
    Dot,
    Comment,
    EndOfFile
}
```

---

### 5.2. XamlToken

```csharp
public readonly struct XamlToken
{
    public XamlTokenKind Kind { get; }
    public string Text { get; }
    public TextSpan Span { get; }

    public XamlToken(XamlTokenKind kind, string text, TextSpan span)
    {
        Kind = kind;
        Text = text;
        Span = span;
    }
}
```

---

### 5.3. XamlCompletionMode

```csharp
public enum XamlCompletionMode
{
    None,
    ElementName,
    AttributeName,
    AttributeValue,
    ClosingElement,
    NamespaceDeclaration,
    NamespacePrefix,
    MarkupExtension,
    AttachedProperty,
    Snippet
}
```

---

### 5.4. XamlCompletionContext

```csharp
public sealed class XamlCompletionContext
{
    public XamlCompletionMode Mode { get; init; }
    public string CurrentWord { get; init; } = "";
    public string? CurrentElementName { get; init; }
    public string? CurrentAttributeName { get; init; }
    public TextSpan ReplaceSpan { get; init; }
}
```

---

## 6. Fonctionnement attendu de l’autocomplétion

### 6.1. Complétion d’élément

Exemple :

```xml
<Bu
```

Résultat attendu :

```text
Button
```

Insertion :

```xml
<Button
```

Option plus avancée :

```xml
<Button />
```

Mais ne pas faire l’insertion avancée au début. Commencer simple.

---

### 6.2. Complétion d’attribut

Exemple :

```xml
<Button Wid
```

Résultat attendu :

```text
Width
```

Insertion :

```xml
<Button Width=""
```

Le curseur doit être placé entre les guillemets si possible.

Si le système ne gère pas encore le placement du curseur dans l’insertion, insérer simplement :

```xml
Width=""
```

puis placer le curseur juste avant le dernier guillemet dans une tâche séparée.

---

### 6.3. Complétion de valeur d’énumération

Exemple :

```xml
<Button HorizontalAlignment="
```

Résultat attendu :

```text
Left
Center
Right
Stretch
```

---

### 6.4. Complétion de balise fermante

Exemple :

```xml
<StackPanel>
    <Button />
</
```

Résultat attendu :

```text
StackPanel
```

Insertion :

```xml
</StackPanel>
```

---

### 6.5. Complétion de namespace

Exemple :

```xml
xmlns:
```

Résultat attendu :

```text
mgui
local
controls
```

---

### 6.6. Complétion de markup extension

Exemple :

```xml
<TextBlock Text="{Bin
```

Résultat attendu :

```text
Binding
StaticResource
DynamicResource
TemplateBinding
```

Au début, pour MGUI, garder seulement :

```text
Binding
StaticResource
```

---

### 6.7. Complétion de snippets

Exemple de snippets :

```xml
<Grid>

</Grid>
```

```xml
<StackPanel Orientation="Vertical">

</StackPanel>
```

```xml
<Button Text="" />
```

```xml
<TextBlock Text="" />
```

---

## 7. Schéma XAML

### 7.1. XamlTypeDescriptor

```csharp
public sealed class XamlTypeDescriptor
{
    public string Name { get; init; } = "";
    public string ClrTypeName { get; init; } = "";
    public string Namespace { get; init; } = "";
    public List<XamlPropertyDescriptor> Properties { get; } = new();
}
```

---

### 7.2. XamlPropertyDescriptor

```csharp
public sealed class XamlPropertyDescriptor
{
    public string Name { get; init; } = "";
    public string TypeName { get; init; } = "";
    public bool IsAttachedProperty { get; init; }
    public bool IsCollection { get; init; }
    public List<string> EnumValues { get; } = new();
}
```

---

### 7.3. IXamlSchemaProvider

```csharp
public interface IXamlSchemaProvider
{
    IReadOnlyList<XamlTypeDescriptor> GetTypes();
    XamlTypeDescriptor? FindType(string name);
    IReadOnlyList<XamlPropertyDescriptor> GetProperties(string typeName);
}
```

---

### 7.4. ManualXamlSchemaProvider

Première version obligatoire.

L’agent doit créer un provider manuel avec quelques contrôles de base.

Contrôles minimum :

```text
Button
TextBlock
Panel
Grid
StackPanel
Image
Border
CheckBox
TextBox
ScrollViewer
```

Propriétés communes minimum :

```text
Name
Width
Height
MinWidth
MinHeight
MaxWidth
MaxHeight
Margin
Padding
HorizontalAlignment
VerticalAlignment
Background
Foreground
IsVisible
IsEnabled
```

Propriétés spécifiques :

```text
Button:
  Text
  Command

TextBlock:
  Text
  FontSize
  TextAlignment
  TextWrapping

StackPanel:
  Orientation

Image:
  Source
  Stretch

Grid:
  RowDefinitions
  ColumnDefinitions

Border:
  BorderBrush
  BorderThickness
  CornerRadius
```

Valeurs d’énumération minimum :

```text
HorizontalAlignment:
  Left
  Center
  Right
  Stretch

VerticalAlignment:
  Top
  Center
  Bottom
  Stretch

Orientation:
  Horizontal
  Vertical

TextAlignment:
  Left
  Center
  Right
  Justify

Stretch:
  None
  Fill
  Uniform
  UniformToFill
```

---

## 8. Analyse de contexte XAML

### 8.1. But

`XamlContextAnalyzer` doit recevoir :

- le texte complet ;
- la position du curseur ;
- les tokens produits par `XamlTokenizer`.

Il doit retourner un `XamlCompletionContext`.

---

### 8.2. Cas à détecter

#### Cas 1 — Début d’élément

```xml
<
```

Mode :

```text
ElementName
```

---

#### Cas 2 — Élément partiellement tapé

```xml
<But
```

Mode :

```text
ElementName
```

CurrentWord :

```text
But
```

ReplaceSpan :

```text
span couvrant "But"
```

---

#### Cas 3 — Attribut

```xml
<Button 
```

Mode :

```text
AttributeName
```

CurrentElementName :

```text
Button
```

---

#### Cas 4 — Attribut partiellement tapé

```xml
<Button Wid
```

Mode :

```text
AttributeName
```

CurrentWord :

```text
Wid
```

ReplaceSpan :

```text
span couvrant "Wid"
```

---

#### Cas 5 — Valeur d’attribut

```xml
<Button HorizontalAlignment="
```

Mode :

```text
AttributeValue
```

CurrentAttributeName :

```text
HorizontalAlignment
```

---

#### Cas 6 — Balise fermante

```xml
</
```

Mode :

```text
ClosingElement
```

---

#### Cas 7 — Markup extension

```xml
<TextBlock Text="{Bin
```

Mode :

```text
MarkupExtension
```

CurrentWord :

```text
Bin
```

---

#### Cas 8 — Namespace

```xml
xmlns:
```

Mode :

```text
NamespacePrefix
```

---

### 8.3. Méthode recommandée

Ne pas écrire un parseur XML complet au début.

Utiliser une approche pragmatique :

1. Tokenizer tout le texte.
2. Trouver le token à gauche du curseur.
3. Regarder une petite fenêtre de tokens autour du curseur.
4. Déterminer si le curseur est :
   - dans une balise ;
   - dans une string ;
   - après `<` ;
   - après `</` ;
   - après un nom d’élément ;
   - après un espace dans une balise ;
   - après `=`.
5. Retourner le contexte.

---

## 9. Coloration syntaxique

### 9.1. ISyntaxHighlighter

```csharp
public interface ISyntaxHighlighter
{
    SyntaxHighlightResult Highlight(string text);
}
```

---

### 9.2. SyntaxHighlightResult

```csharp
public sealed class SyntaxHighlightResult
{
    public List<TextStyleSpan> Spans { get; } = new();
}
```

---

### 9.3. TextStyleSpan

```csharp
public sealed class TextStyleSpan
{
    public TextSpan Span { get; init; }
    public string StyleName { get; init; } = "";
}
```

---

### 9.4. Styles minimum

```text
Xaml.ElementName
Xaml.AttributeName
Xaml.String
Xaml.Punctuation
Xaml.Comment
Xaml.Text
Xaml.MarkupExtension
Xaml.Namespace
```

---

### 9.5. Règle importante

La coloration syntaxique ne doit pas modifier le texte.

Elle doit uniquement produire une liste de spans stylés.

---

## 10. Tâches détaillées pour l’agent IA

Chaque tâche doit être faite dans l’ordre.  
Chaque tâche doit compiler.  
Chaque tâche doit être commitée séparément.

Légende de statut à utiliser dans ce fichier pendant l’exécution :

```text
⬜ À faire
🟨 En cours
✅ Terminé
⛔ Bloqué
```

---

## Phase 1 — Base texte générique

### ⬜ Tâche 1.1 — Identifier l’état actuel du RichTextBox

Objectif :

- inspecter l’implémentation actuelle du `RichTextBox` ;
- lister les responsabilités déjà présentes ;
- identifier où brancher les événements clavier, texte et caret.

Actions :

- trouver le fichier du `RichTextBox` ;
- repérer les méthodes d’insertion de texte ;
- repérer les méthodes de rendu ;
- repérer la gestion du curseur ;
- repérer la gestion de la sélection ;
- repérer le système de mesure texte ;
- noter les contraintes existantes.

Livrable :

- commentaire court dans le commit ou fichier de notes si nécessaire ;
- aucune grosse modification de code sauf correction mineure évidente.

Commit :

```text
[RichTextBox] Inspect current text editing responsibilities
```

---

### ⬜ Tâche 1.2 — Ajouter TextSpan

Objectif :

Créer une structure commune pour représenter une portion de texte.

Actions :

- ajouter `TextSpan`;
- ajouter validation minimale ;
- ajouter propriétés `Start`, `Length`, `End`;
- ajouter `Contains(int index)` si utile ;
- ajouter `IsEmpty`.

Tests :

- créer tests unitaires si projet de tests disponible ;
- vérifier `End`;
- vérifier span vide ;
- vérifier containment.

Commit :

```text
[Text] Add TextSpan
```

---

### ⬜ Tâche 1.3 — Ajouter ITextEditor

Objectif :

Créer une interface minimale utilisée par l’autocomplétion.

Actions :

- ajouter `ITextEditor`;
- exposer `Text`;
- exposer `CaretIndex`;
- exposer `Selection`;
- exposer `InsertText`;
- exposer `ReplaceText`;
- exposer `MoveCaretTo`;
- exposer `GetCaretScreenPosition`.

Règles :

- ne pas ajouter de logique XAML ;
- ne pas modifier encore le comportement du `RichTextBox`.

Commit :

```text
[Text] Add text editor abstraction
```

---

### ⬜ Tâche 1.4 — Faire implémenter ITextEditor par RichTextBox

Objectif :

Permettre aux services externes d’interagir avec le `RichTextBox`.

Actions :

- implémenter `ITextEditor` ;
- connecter `Text`;
- connecter `CaretIndex`;
- connecter `Selection`;
- connecter `InsertText`;
- connecter `ReplaceText`;
- connecter `MoveCaretTo`;
- connecter `GetCaretScreenPosition`.

Tests manuels :

- taper du texte ;
- déplacer le curseur ;
- sélectionner du texte ;
- insérer du texte ;
- remplacer une sélection.

Commit :

```text
[RichTextBox] Implement ITextEditor
```

---

## Phase 2 — Système d’autocomplétion générique

### ⬜ Tâche 2.1 — Ajouter les modèles Completion

Objectif :

Créer les classes génériques d’autocomplétion.

Actions :

- ajouter `CompletionItem`;
- ajouter `CompletionItemKind`;
- ajouter `CompletionResult`;
- ajouter `CompletionContext`;
- ajouter `CompletionTriggerKind`;
- ajouter `ICompletionService`.

Règles :

- aucune référence à XAML ;
- aucune référence à MGUI controls.

Commit :

```text
[Completion] Add generic completion models
```

---

### ⬜ Tâche 2.2 — Ajouter CompletionPopup visuel minimal

Objectif :

Créer un popup capable d’afficher une liste de suggestions.

Actions :

- créer `CompletionPopup`;
- afficher une liste verticale ;
- afficher `DisplayText`;
- gérer élément sélectionné ;
- exposer `Show`;
- exposer `Hide`;
- exposer `MoveSelectionUp`;
- exposer `MoveSelectionDown`;
- exposer `SelectedItem`.

Version minimale acceptable :

- pas d’icônes ;
- pas de description détaillée ;
- liste simple ;
- style simple.

Tests manuels :

- afficher 3 suggestions factices ;
- naviguer avec haut/bas ;
- cacher le popup.

Commit :

```text
[Completion] Add completion popup
```

---

### ⬜ Tâche 2.3 — Ajouter TextCompletionController

Objectif :

Créer le pont entre `RichTextBox`, `ICompletionService` et `CompletionPopup`.

Actions :

- créer `TextCompletionController`;
- recevoir un `ITextEditor`;
- recevoir un `ICompletionService`;
- recevoir un `CompletionPopup`;
- ajouter méthode `OnTextInput`;
- ajouter méthode `OnKeyDown`;
- appeler le service ;
- afficher/cacher le popup ;
- appliquer l’item sélectionné.

Touches à gérer :

```text
ArrowDown
ArrowUp
Enter
Tab
Escape
Ctrl+Space
```

Règles :

- `Ctrl+Space` doit ouvrir l’autocomplétion manuellement ;
- `Escape` doit fermer le popup ;
- `Enter` ou `Tab` doit valider l’item sélectionné si le popup est ouvert ;
- si le popup est fermé, le comportement normal doit continuer.

Commit :

```text
[Completion] Add text completion controller
```

---

### ⬜ Tâche 2.4 — Ajouter un FakeCompletionService pour tester

Objectif :

Tester l’infrastructure sans XAML.

Actions :

- créer un service de test retournant :
  - `Button`;
  - `TextBlock`;
  - `Grid`;
- brancher temporairement ce service dans un exemple ou écran de test ;
- vérifier que l’insertion fonctionne.

Règles :

- ne pas garder le fake branché en production sauf derrière un exemple/debug.

Commit :

```text
[Completion] Add fake completion service for editor testing
```

---

## Phase 3 — Tokenizer XAML

### ⬜ Tâche 3.1 — Ajouter XamlTokenKind et XamlToken

Objectif :

Créer le modèle de token XAML.

Actions :

- ajouter `XamlTokenKind`;
- ajouter `XamlToken`;
- ajouter `XamlTokenList` si utile.

Commit :

```text
[Xaml] Add token model
```

---

### ⬜ Tâche 3.2 — Ajouter XamlTokenizer minimal

Objectif :

Tokenizer le texte XAML.

Le tokenizer doit reconnaître :

```text
<
>
/
=
"
'
:
.
{
}
identifiers
strings
comments
whitespace
text
```

Actions :

- créer `XamlTokenizer`;
- ajouter méthode `Tokenize(string text)`;
- gérer fin de fichier ;
- ne pas throw sur XAML incomplet ;
- produire des tokens `Unknown` si nécessaire.

Règles :

- le tokenizer doit être tolérant ;
- il doit fonctionner avec du XAML partiellement écrit ;
- il ne doit pas nécessiter que le document soit valide.

Tests obligatoires :

```xml
<Button />
```

```xml
<Button Width="100" Height="50" />
```

```xml
<StackPanel>
    <Button Text="OK" />
</StackPanel>
```

```xml
<!-- comment -->
```

```xml
<TextBlock Text="{Binding Name}" />
```

Commit :

```text
[Xaml] Add tolerant tokenizer
```

---

### ⬜ Tâche 3.3 — Ajouter tests du tokenizer

Objectif :

Sécuriser le tokenizer.

Actions :

- créer tests unitaires si possible ;
- sinon créer un petit outil/debug interne ;
- vérifier les kinds ;
- vérifier les spans ;
- vérifier les textes.

Cas de test :

- balise simple ;
- attribut simple ;
- string double quote ;
- string single quote ;
- commentaire ;
- markup extension ;
- XAML incomplet ;
- balise fermante.

Commit :

```text
[Xaml] Add tokenizer tests
```

---

## Phase 4 — Analyse de contexte XAML

### ⬜ Tâche 4.1 — Ajouter XamlCompletionMode et XamlCompletionContext

Objectif :

Créer le résultat d’analyse de contexte.

Actions :

- ajouter enum `XamlCompletionMode`;
- ajouter classe `XamlCompletionContext`;
- ajouter `CurrentWord`;
- ajouter `CurrentElementName`;
- ajouter `CurrentAttributeName`;
- ajouter `ReplaceSpan`.

Commit :

```text
[Xaml] Add completion context model
```

---

### ⬜ Tâche 4.2 — Ajouter XamlContextAnalyzer minimal

Objectif :

Déterminer le type d’autocomplétion à partir du texte et du curseur.

Actions :

- créer `XamlContextAnalyzer`;
- ajouter `Analyze(string text, int caretIndex)`;
- utiliser `XamlTokenizer`;
- détecter :
  - element name ;
  - attribute name ;
  - attribute value ;
  - closing element ;
  - markup extension.

Règles :

- ne pas chercher une perfection XML complète ;
- être robuste avec un document invalide ;
- ne jamais planter si le curseur est au début ou à la fin du texte.

Commit :

```text
[Xaml] Add completion context analyzer
```

---

### ⬜ Tâche 4.3 — Ajouter tests du XamlContextAnalyzer

Objectif :

Valider les cas principaux.

Tests :

```xml
<
```

Résultat :

```text
ElementName
```

---

```xml
<But
```

Résultat :

```text
ElementName
CurrentWord = But
```

---

```xml
<Button 
```

Résultat :

```text
AttributeName
CurrentElementName = Button
```

---

```xml
<Button Wid
```

Résultat :

```text
AttributeName
CurrentWord = Wid
```

---

```xml
<Button Width="
```

Résultat :

```text
AttributeValue
CurrentAttributeName = Width
```

---

```xml
</
```

Résultat :

```text
ClosingElement
```

---

```xml
<TextBlock Text="{Bin
```

Résultat :

```text
MarkupExtension
CurrentWord = Bin
```

Commit :

```text
[Xaml] Add context analyzer tests
```

---

## Phase 5 — Schéma XAML manuel

### ⬜ Tâche 5.1 — Ajouter les descriptors de schéma

Objectif :

Créer le modèle décrivant les types XAML.

Actions :

- ajouter `XamlTypeDescriptor`;
- ajouter `XamlPropertyDescriptor`;
- ajouter `XamlNamespaceDescriptor` si utile ;
- ajouter `XamlEnumDescriptor` si utile ;
- ajouter `IXamlSchemaProvider`.

Commit :

```text
[XamlSchema] Add schema descriptors
```

---

### ⬜ Tâche 5.2 — Ajouter ManualXamlSchemaProvider

Objectif :

Créer un schéma manuel minimal pour tester l’autocomplétion.

Types minimum :

```text
Button
TextBlock
Grid
StackPanel
Image
Border
CheckBox
TextBox
ScrollViewer
```

Actions :

- enregistrer les types ;
- enregistrer les propriétés communes ;
- enregistrer les propriétés spécifiques ;
- enregistrer les enum values.

Commit :

```text
[XamlSchema] Add manual schema provider
```

---

### ⬜ Tâche 5.3 — Ajouter recherche dans le schéma

Objectif :

Permettre de filtrer types/propriétés.

Actions :

- `FindType(string name)`;
- `GetTypes()`;
- `GetProperties(string typeName)`;
- `FindProperty(string typeName, string propertyName)` si utile ;
- gestion case-insensitive optionnelle.

Commit :

```text
[XamlSchema] Add schema lookup helpers
```

---

## Phase 6 — XamlCompletionService

### ⬜ Tâche 6.1 — Créer XamlCompletionService

Objectif :

Implémenter `ICompletionService`.

Actions :

- créer `XamlCompletionService`;
- injecter `IXamlSchemaProvider`;
- utiliser `XamlContextAnalyzer`;
- switcher sur `XamlCompletionMode`.

Modes à gérer au début :

```text
ElementName
AttributeName
AttributeValue
ClosingElement
MarkupExtension
```

Commit :

```text
[XamlCompletion] Add completion service skeleton
```

---

### ⬜ Tâche 6.2 — Complétion d’éléments

Objectif :

Proposer les types XAML.

Exemple :

```xml
<Bu
```

Doit proposer :

```text
Button
```

Actions :

- lire `CurrentWord`;
- filtrer les types du schema ;
- créer des `CompletionItem` de kind `Element`;
- utiliser `ReplaceSpan`.

Commit :

```text
[XamlCompletion] Add element completion
```

---

### ⬜ Tâche 6.3 — Complétion d’attributs

Objectif :

Proposer les propriétés du type courant.

Exemple :

```xml
<Button Wid
```

Doit proposer :

```text
Width
```

Actions :

- identifier `CurrentElementName`;
- récupérer le type depuis le schéma ;
- récupérer ses propriétés ;
- filtrer par `CurrentWord`;
- insérer idéalement `PropertyName=""`.

Règle :

- éviter de proposer deux fois une propriété déjà présente dans la même balise si cette détection est simple ;
- sinon reporter cette optimisation à une tâche ultérieure.

Commit :

```text
[XamlCompletion] Add attribute completion
```

---

### ⬜ Tâche 6.4 — Complétion de valeurs d’énumération

Objectif :

Proposer les valeurs possibles pour une propriété enum.

Exemple :

```xml
<StackPanel Orientation="
```

Doit proposer :

```text
Horizontal
Vertical
```

Actions :

- identifier `CurrentElementName`;
- identifier `CurrentAttributeName`;
- retrouver la propriété ;
- si `EnumValues` contient des valeurs, les proposer.

Commit :

```text
[XamlCompletion] Add enum value completion
```

---

### ⬜ Tâche 6.5 — Complétion de balises fermantes

Objectif :

Proposer la bonne balise fermante.

Exemple :

```xml
<StackPanel>
    <Button />
</
```

Doit proposer :

```text
StackPanel
```

Actions :

- analyser les balises ouvertes avant le curseur ;
- ignorer les balises self-closing ;
- ignorer les balises déjà fermées ;
- proposer la dernière balise ouverte.

Version minimale acceptable :

- stack de noms d’éléments ;
- gérer `<Button />`;
- gérer `</Button>`.

Commit :

```text
[XamlCompletion] Add closing element completion
```

---

### ⬜ Tâche 6.6 — Complétion de markup extensions

Objectif :

Proposer quelques markup extensions.

Exemple :

```xml
<TextBlock Text="{Bin
```

Doit proposer :

```text
Binding
```

Actions :

- détecter mode `MarkupExtension`;
- proposer :
  - `Binding`;
  - `StaticResource`;
- filtrer par `CurrentWord`.

Commit :

```text
[XamlCompletion] Add markup extension completion
```

---

## Phase 7 — Intégration éditeur

### ⬜ Tâche 7.1 — Brancher XamlCompletionService au RichTextBox

Objectif :

Utiliser le vrai service XAML dans l’éditeur.

Actions :

- créer une configuration pour l’éditeur XAML ;
- instancier `ManualXamlSchemaProvider`;
- instancier `XamlCompletionService`;
- instancier `TextCompletionController`;
- connecter les événements du `RichTextBox`.

Tests manuels :

- taper `<`;
- taper `<Bu`;
- taper `<Button `;
- taper `<Button Wid`;
- valider une suggestion ;
- fermer le popup avec Échap.

Commit :

```text
[XamlEditor] Wire XAML completion into RichTextBox
```

---

### ⬜ Tâche 7.2 — Gérer le remplacement correct du texte

Objectif :

S’assurer que l’item validé remplace seulement le texte partiellement tapé.

Cas :

```xml
<Bu
```

Valider `Button`.

Résultat :

```xml
<Button
```

et non :

```xml
<BuButton
```

Cas :

```xml
<Button Wid
```

Valider `Width`.

Résultat :

```xml
<Button Width=""
```

Commit :

```text
[Completion] Apply replacement span on commit
```

---

### ⬜ Tâche 7.3 — Placement avancé du curseur après insertion

Objectif :

Permettre aux suggestions comme `Width=""` de placer le curseur entre les guillemets.

Option recommandée :

Utiliser un marqueur interne dans `InsertText`.

Exemple :

```text
Width="$0"
```

Le contrôleur insère :

```text
Width=""
```

puis place le curseur à la position du `$0`.

Actions :

- ajouter support optionnel du marker `$0`;
- nettoyer le marker avant insertion ;
- placer le caret.

Commit :

```text
[Completion] Add caret marker support for completion items
```

---

## Phase 8 — Coloration syntaxique XAML

### ⬜ Tâche 8.1 — Ajouter les modèles de highlighting

Objectif :

Créer une API générique de coloration syntaxique.

Actions :

- ajouter `ISyntaxHighlighter`;
- ajouter `SyntaxHighlightResult`;
- ajouter `TextStyleSpan`.

Règles :

- aucune dépendance XAML dans les modèles génériques.

Commit :

```text
[Highlighting] Add syntax highlighting abstractions
```

---

### ⬜ Tâche 8.2 — Ajouter XamlSyntaxHighlighter

Objectif :

Colorer les tokens XAML.

Actions :

- utiliser `XamlTokenizer`;
- mapper token kind vers style ;
- produire des spans.

Mapping minimum :

```text
LessThan, GreaterThan, Slash, Equals -> Xaml.Punctuation
Identifier as element name -> Xaml.ElementName
Identifier as attribute name -> Xaml.AttributeName
String -> Xaml.String
Comment -> Xaml.Comment
OpenBrace/CloseBrace -> Xaml.MarkupExtension
```

Version minimale acceptable :

- si la distinction element/attribute est trop coûteuse au début, colorer les identifiers de manière uniforme ;
- améliorer dans une tâche suivante.

Commit :

```text
[XamlHighlighting] Add XAML syntax highlighter
```

---

### ⬜ Tâche 8.3 — Brancher le highlighter au RichTextBox

Objectif :

Permettre au `RichTextBox` d’afficher les spans stylés.

Actions :

- ajouter un point d’extension `ISyntaxHighlighter`;
- recalculer les spans à chaque modification de texte ;
- appliquer les styles au rendu ;
- ne pas modifier le texte.

Tests manuels :

- écrire une balise ;
- vérifier couleur élément ;
- vérifier couleur attribut ;
- vérifier couleur string ;
- vérifier commentaire.

Commit :

```text
[RichTextBox] Support syntax highlighting spans
```

---

## Phase 9 — Snippets XAML

### ⬜ Tâche 9.1 — Ajouter modèle XamlSnippet

Objectif :

Créer un modèle de snippet.

```csharp
public sealed class XamlSnippet
{
    public string Name { get; init; } = "";
    public string DisplayText { get; init; } = "";
    public string InsertText { get; init; } = "";
    public string Description { get; init; } = "";
}
```

Commit :

```text
[XamlSnippets] Add snippet model
```

---

### ⬜ Tâche 9.2 — Ajouter XamlSnippetProvider

Objectif :

Fournir des snippets de base.

Snippets minimum :

```xml
<Grid>
    $0
</Grid>
```

```xml
<StackPanel Orientation="Vertical">
    $0
</StackPanel>
```

```xml
<Button Text="$0" />
```

```xml
<TextBlock Text="$0" />
```

Commit :

```text
[XamlSnippets] Add default snippets
```

---

### ⬜ Tâche 9.3 — Intégrer les snippets dans XamlCompletionService

Objectif :

Proposer les snippets dans les contextes adaptés.

Règle :

- proposer les snippets surtout en mode `ElementName`;
- filtrer par nom ;
- utiliser `$0` pour positionnement du curseur.

Commit :

```text
[XamlCompletion] Add snippet completion
```

---

## Phase 10 — Diagnostics simples

### ⬜ Tâche 10.1 — Ajouter XamlDiagnostic

Objectif :

Préparer l’affichage d’erreurs simples.

```csharp
public sealed class XamlDiagnostic
{
    public TextSpan Span { get; init; }
    public XamlDiagnosticSeverity Severity { get; init; }
    public string Message { get; init; } = "";
}
```

```csharp
public enum XamlDiagnosticSeverity
{
    Info,
    Warning,
    Error
}
```

Commit :

```text
[XamlDiagnostics] Add diagnostic model
```

---

### ⬜ Tâche 10.2 — Ajouter diagnostics basiques

Objectif :

Détecter quelques erreurs simples.

Diagnostics minimum :

- balise ouverte non fermée ;
- guillemet non fermé ;
- attribut sans valeur ;
- élément inconnu ;
- propriété inconnue.

Règle :

- le diagnostic ne doit jamais bloquer l’édition ;
- il doit être tolérant au XAML incomplet.

Commit :

```text
[XamlDiagnostics] Add basic diagnostics
```

---

## Phase 11 — Provider par réflexion

Cette phase ne doit commencer qu’après validation du provider manuel.

### ⬜ Tâche 11.1 — Définir les attributs XAML optionnels

Objectif :

Permettre de mapper classes/propriétés MGUI vers XAML.

Exemples :

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class XamlElementAttribute : Attribute
{
    public string Name { get; }

    public XamlElementAttribute(string name)
    {
        Name = name;
    }
}
```

```csharp
[AttributeUsage(AttributeTargets.Property)]
public sealed class XamlPropertyAttribute : Attribute
{
}
```

Commit :

```text
[XamlSchema] Add XAML metadata attributes
```

---

### ⬜ Tâche 11.2 — Ajouter ReflectionXamlSchemaProvider

Objectif :

Construire le schéma depuis les types MGUI.

Actions :

- scanner un assembly ;
- trouver les classes avec `[XamlElement]`;
- trouver les propriétés publiques avec `[XamlProperty]`;
- construire `XamlTypeDescriptor`;
- détecter enums ;
- détecter types simples.

Règles :

- ne pas supprimer le provider manuel ;
- permettre de combiner manuel + réflexion ;
- ne pas imposer la réflexion partout.

Commit :

```text
[XamlSchema] Add reflection schema provider
```

---

### ⬜ Tâche 11.3 — Ajouter CombinedXamlSchemaProvider

Objectif :

Permettre plusieurs sources de schéma.

Actions :

- créer un provider combiné ;
- éviter les doublons ;
- priorité configurable :
  - manuel avant réflexion ;
  - ou réflexion avant manuel.

Commit :

```text
[XamlSchema] Add combined schema provider
```

---

## Phase 12 — Amélioration UX

### ⬜ Tâche 12.1 — Ajouter descriptions dans le popup

Objectif :

Afficher une description courte de l’item sélectionné.

Actions :

- afficher `Description`;
- optionnellement afficher `Kind`;
- garder un design simple.

Commit :

```text
[Completion] Show selected item description
```

---

### ⬜ Tâche 12.2 — Ajouter filtrage dynamique pendant la frappe

Objectif :

Quand le popup est ouvert, continuer à filtrer selon le texte tapé.

Actions :

- recalculer le contexte après chaque caractère ;
- mettre à jour la liste ;
- conserver sélection si possible ;
- cacher le popup si plus aucun résultat.

Commit :

```text
[Completion] Update suggestions while typing
```

---

### ⬜ Tâche 12.3 — Ajouter tri des suggestions

Objectif :

Avoir des suggestions utiles en premier.

Priorité recommandée :

1. match exact ;
2. commence par le texte tapé ;
3. contient le texte tapé ;
4. snippets ;
5. autres.

Commit :

```text
[Completion] Add suggestion ranking
```

---

### ⬜ Tâche 12.4 — Ne pas proposer les attributs déjà utilisés

Objectif :

Éviter de proposer deux fois `Width` dans :

```xml
<Button Width="100" Wid
```

Actions :

- analyser les attributs déjà présents dans la balise courante ;
- les exclure des suggestions.

Commit :

```text
[XamlCompletion] Hide already used attributes
```

---

## Phase 13 — Robustesse

### ⬜ Tâche 13.1 — Tests avec XAML incomplet

Objectif :

S’assurer que l’éditeur ne plante jamais.

Cas :

```xml
<
```

```xml
<Button
```

```xml
<Button Width="
```

```xml
<Button Width="{Binding
```

```xml
<StackPanel>
    <Button>
```

```xml
</
```

Commit :

```text
[XamlEditor] Add incomplete XAML robustness tests
```

---

### ⬜ Tâche 13.2 — Tests de performance basiques

Objectif :

Éviter que l’éditeur rame sur des fichiers moyens.

Test minimum :

- document de 500 lignes ;
- document de 2000 lignes si facile ;
- modification au milieu ;
- autocomplétion en fin de document.

Règle :

- ne pas optimiser agressivement maintenant ;
- mesurer seulement ;
- noter les problèmes si détectés.

Commit :

```text
[XamlEditor] Add basic performance checks
```

---

## 11. Stop conditions

L’agent doit s’arrêter et demander validation si :

- il doit modifier massivement le système de rendu de MGUI ;
- il doit changer les conventions publiques de `RichTextBox` ;
- il doit introduire une dépendance externe ;
- il ne trouve pas où brancher correctement les événements clavier ;
- il y a un conflit important avec l’architecture existante ;
- une tâche nécessite de casser la compatibilité avec du code existant ;
- le projet ne compile plus et la cause n’est pas liée à la tâche en cours ;
- une fonctionnalité demande un vrai parseur XAML complet.

---

## 12. DO / DON’T

### DO

- Garder le `RichTextBox` générique.
- Ajouter des interfaces propres.
- Faire des petites tâches.
- Commiter souvent.
- Tester avec du XAML incomplet.
- Séparer tokenizer, analyzer, completion et highlighting.
- Commencer par un schema manuel.
- Ajouter la réflexion seulement après validation.
- Utiliser des noms explicites.
- Garder le popup simple au début.

---

### DON’T

- Ne pas mettre de logique XAML directement dans `RichTextBox`.
- Ne pas hardcoder `Button`, `Grid`, etc. dans le `RichTextBox`.
- Ne pas écrire un parser XAML complet au début.
- Ne pas introduire Roslyn ou un gros moteur de parsing.
- Ne pas mélanger coloration et autocomplétion.
- Ne pas mélanger rendu et analyse.
- Ne pas ajouter de dépendance externe sans justification.
- Ne pas optimiser avant d’avoir une version fonctionnelle.
- Ne pas faire un gros commit unique.
- Ne pas changer l’architecture MGUI hors périmètre.

---

## 13. Critères d’acceptation MVP

Le MVP est accepté si :

- le `RichTextBox` permet de saisir du XAML ;
- `Ctrl+Space` affiche le popup ;
- taper `<` propose des éléments ;
- taper `<Bu` propose `Button` ;
- valider `Button` remplace correctement `Bu` ;
- taper `<Button ` propose des attributs ;
- taper `<Button Wid` propose `Width` ;
- valider `Width` insère `Width=""` ;
- taper `<StackPanel Orientation="` propose `Horizontal` et `Vertical` ;
- taper `</` propose la dernière balise ouverte ;
- `Escape` ferme le popup ;
- `Enter` ou `Tab` valide l’élément sélectionné ;
- l’éditeur ne plante pas avec du XAML incomplet ;
- le projet compile ;
- les responsabilités restent séparées.

---

## 14. Critères d’acceptation version avancée

La version avancée est acceptée si :

- les snippets fonctionnent ;
- le curseur peut être placé avec `$0` ;
- la coloration syntaxique fonctionne ;
- les attributs déjà utilisés ne sont plus proposés ;
- les enum values sont proposées selon le type de propriété ;
- les diagnostics simples sont visibles ou disponibles ;
- le schéma peut venir du provider manuel et/ou par réflexion ;
- le système d’autocomplétion reste générique ;
- le `RichTextBox` ne contient toujours aucune logique XAML.

---

## 15. Exemple de séquence utilisateur attendue

### Exemple 1

Utilisateur tape :

```xml
<
```

Popup :

```text
Button
TextBlock
Grid
StackPanel
Image
Border
```

Utilisateur choisit :

```text
Button
```

Résultat :

```xml
<Button
```

---

### Exemple 2

Utilisateur tape :

```xml
<Button 
```

Popup :

```text
Name
Width
Height
Margin
Padding
HorizontalAlignment
VerticalAlignment
Text
Command
```

Utilisateur choisit :

```text
Width
```

Résultat :

```xml
<Button Width=""
```

Curseur idéalement entre les guillemets.

---

### Exemple 3

Utilisateur tape :

```xml
<StackPanel Orientation="
```

Popup :

```text
Horizontal
Vertical
```

---

### Exemple 4

Utilisateur tape :

```xml
<StackPanel>
    <Button />
</
```

Popup :

```text
StackPanel
```

Validation :

```xml
<StackPanel>
    <Button />
</StackPanel>
```

---

## 16. Notes d’implémentation importantes

### 16.1. Tolérance au document invalide

Un éditeur travaille presque toujours sur un document temporairement invalide.

Exemples :

```xml
<Button
```

```xml
<Button Width="
```

```xml
<Grid>
    <
```

Le tokenizer, l’analyzer, le highlighter et les diagnostics ne doivent jamais supposer que le document est valide.

---

### 16.2. Parsing incrémental

Ne pas implémenter le parsing incrémental dans le MVP.

Faire simple :

- retokenizer tout le document ;
- mesurer ensuite si c’est trop lent ;
- optimiser seulement si nécessaire.

---

### 16.3. RichTextBox et rendu

Le `RichTextBox` peut recevoir des spans de style.

Il ne doit pas savoir pourquoi un span existe.

Il doit seulement rendre :

```text
TextSpan + StyleName
```

Le mapping `StyleName -> couleur/font` doit être ailleurs ou dans le système de thème.

---

### 16.4. Autocomplétion future multi-langage

Cette architecture doit permettre plus tard :

```text
JsonCompletionService
LuaCompletionService
CSharpLikeCompletionService
CommandConsoleCompletionService
```

Donc ne pas créer d’interfaces trop spécifiques à XAML dans `Text/Completion`.

---

## 17. Roadmap recommandée

Priorité réelle recommandée :

```text
1. ITextEditor
2. Completion models
3. CompletionPopup
4. TextCompletionController
5. FakeCompletionService
6. XamlTokenizer
7. XamlContextAnalyzer
8. ManualXamlSchemaProvider
9. XamlCompletionService
10. Brancher au RichTextBox
11. Replacement span correct
12. Insertion avec $0
13. Syntax highlighting
14. Snippets
15. Diagnostics
16. Reflection schema provider
```

---

## 18. Résultat final attendu

À la fin de ce plan, le projet doit avoir :

```text
Un RichTextBox générique
+ un système d’autocomplétion générique
+ un service d’autocomplétion XAML
+ un tokenizer XAML tolérant
+ un analyzer de contexte XAML
+ un schema provider manuel
+ un popup d’autocomplétion
+ une base de coloration syntaxique
```

Le système doit être suffisamment propre pour évoluer vers un vrai éditeur XAML MGUI complet, sans transformer le `RichTextBox` en classe monolithique.

---

## 19. Rappel final pour l’agent

Avant chaque modification importante, l’agent doit se poser ces questions :

```text
Est-ce que je suis en train de mettre de la logique XAML dans RichTextBox ?
Est-ce que cette classe a une seule responsabilité ?
Est-ce que cette fonctionnalité pourrait être réutilisée pour un autre langage ?
Est-ce que le projet compile encore ?
Est-ce que ce commit est petit ?
Est-ce que ce changement est dans le périmètre ?
```

Si la réponse indique un risque architectural, l’agent doit s’arrêter et revoir la séparation des responsabilités.
