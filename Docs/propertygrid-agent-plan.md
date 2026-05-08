# Plan agent IA - PropertyGrid MVP

Ce plan est volontairement explicite: il est destine a un agent IA moins autonome. L'agent doit suivre les etapes dans l'ordre, modifier l'icone devant le nom de l'etape, valider, puis committer avant de passer a l'etape suivante.

## Source fonctionnelle

Lire d'abord `Docs/propertygrid-fonctionnalites.md`. Le MVP doit fournir:

- `MGPropertyGrid` avec `SelectedObject` et `RefreshVisibleValues()`;
- categories repliables, avec conservation de l'etat ouvert/ferme si possible;
- edition de `bool`, `int`, `float`, `double`, `string` uniquement;
- cache de reflection par `Type`;
- refresh par frame sans reconstruction, sans reflection, sans recreation d'editeurs;
- protection des champs texte pendant la saisie;
- rendu et theme decouples comme les controles lookless existants;
- definitions de theme pour `Dark_Blue` et `Dark`.

## Discipline obligatoire

Icones d'etat a utiliser devant chaque etape:

- `☐` a faire;
- `▶` en cours;
- `✅` termine;
- `⛔` bloque.

Regles de travail:

- Au debut d'une etape, remplacer son icone `☐` par `▶` et committer ce changement si le plan est deja suivi par Git.
- A la fin de l'etape, remplacer `▶` par `✅`, ajouter une courte note de resultat sous l'etape, lancer les tests indiques, puis committer.
- Ne jamais commencer l'etape suivante tant que l'etape courante n'a pas un commit dedie.
- Avant chaque commit: `git status --short`, verifier les fichiers modifies, et ne stage que les fichiers de l'etape.
- Message de commit conseille: `PropertyGrid step NN: <resume court>`.
- Si l'etape bloque, remplacer `▶` par `⛔`, ajouter la cause exacte et le prochain diagnostic a faire, puis demander de l'aide.
- Ne pas embarquer de changements non lies deja presents dans le workspace.

## Contraintes d'architecture

- Ne pas dessiner le chrome du PropertyGrid directement dans `MGPropertyGrid.DrawSelf`.
- Les couleurs, brushes, paddings, bordures, tailles de header, alternances et etats invalides doivent venir du theme ou du template.
- Le controle doit utiliser `DefaultControlTemplateName`, des parts nommes et `MGControlTemplateCatalog`, comme `ListView`, `TreeView`, `ComboBox`, `TextBox`.
- Les valeurs theme runtime doivent vivre dans `MGTheme`; les valeurs declaratives dans `MGUI.Core/UI/XAML/Themes.cs`; la conversion dans `ThemeDefinitionBuilder`.
- Les definitions built-in doivent etre dans `MGUI.Core/UI/Themes/BuiltInThemes.xaml`, avec valeurs pour `Dark_Blue` et overrides pour `Dark`.
- Le code metier de reflection/descriptors ne doit pas connaitre les controles visuels.
- Les editeurs visuels ne doivent pas appeler `type.GetProperties()`.

## Fichiers a inspecter avant de coder

- `MGUI.Core/UI/MGListView.cs` et `MGUI.Core/UI/MGTreeView.cs` pour la structure de controle data/display.
- `MGUI.Core/UI/MGExpander.cs` pour le comportement ouvert/ferme.
- `MGUI.Core/UI/Styling/MGControlTemplateCatalog.cs` pour templates et `ApplyThemeDefault`.
- `MGUI.Core/UI/Themes/BuiltInThemes.xaml` pour `Dark_Blue` et `Dark`.
- `MGUI.Core/UI/XAML/Controls.cs` pour exposer un controle en XAML.
- `MGUI.Core/UI/XAML/Themes.cs` et `MGUI.Core/UI/XAML/ThemeDefinitionBuilder.cs` pour ajouter des settings de theme.

## Plan d'execution

### ☐ Etape 01 - Contrats non visuels et descriptors

Objectif: creer la couche non-UI qui analyse les proprietes sans creer de controle.

Actions:

- Ajouter `PropertyGrid` dans `MGElementType`, section Data Display.
- Creer les types non visuels, idealement dans `MGUI.Core/UI/PropertyGrid/`:
  - `MGPropertyGridDescriptor`;
  - `MGPropertyGridEditorKind`;
  - `MGPropertyGridDescriptorCache`;
  - `MGPropertyGridCategoryModel` ou equivalent minimal.
- Supporter uniquement les proprietes publiques d'instance sans indexer.
- Mapper les types autorises: `bool`, `int`, `float`, `double`, `string`.
- Utiliser `CategoryAttribute` si present, sinon categorie `Misc`.
- Utiliser `DisplayNameAttribute` si present, sinon le nom de propriete.
- Ne pas gerer encore `Vector2`, `Color`, `enum`, collections, objets imbriques, recherche, undo/redo.
- Le cache doit etre un `Dictionary<Type, IReadOnlyList<MGPropertyGridDescriptor>>` ou equivalent, et doit appeler `GetProperties()` uniquement lors du premier acces a un type.

Tests minimum:

- Un test verifie que les types autorises obtiennent le bon `EditorKind`.
- Un test verifie que les types non supportes sont exclus.
- Un test verifie que deux appels sur le meme `Type` reutilisent les descriptors caches.

Validation:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`

Commit attendu: `PropertyGrid step 01: add descriptor cache`.

### ☐ Etape 02 - Squelette de `MGPropertyGrid`

Objectif: ajouter le controle persistant sans editeurs avances.

Actions:

- Creer `MGPropertyGrid` dans `MGUI.Core/UI/MGPropertyGrid.cs` ou un fichier coherent avec le style local.
- Exposer `SelectedObject`.
- Exposer `RefreshVisibleValues()`.
- Conserver le dernier objet et le dernier type vus.
- Reconstruire la structure uniquement si l'objet ou le type change.
- Ajouter les parts nommes minimum:
  - `PART_OuterBorder`;
  - `PART_ScrollViewer`;
  - `PART_CategoriesPanel`.
- Initialiser `DefaultControlTemplateName` avec une constante ajoutee dans `MGControlTemplateCatalog`.
- `DrawSelf` doit rester vide ou absent: pas de dessin direct du chrome.

Tests minimum:

- Changer `SelectedObject` vers un autre objet du meme type ne doit pas refaire la reflection.
- Changer `SelectedObject` vers un type different doit demander les descriptors du nouveau type.
- `RefreshVisibleValues()` ne reconstruit pas la grille.

Validation:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`

Commit attendu: `PropertyGrid step 02: add control skeleton`.

### ☐ Etape 03 - Categories repliables et reutilisation des lignes

Objectif: afficher les categories et lignes, puis permettre ouvrir/fermer les categories sans reconstruire toute la grille a chaque frame.

Actions:

- Creer un modele runtime pour categorie: nom, etat `IsCollapsed`, liste de lignes.
- Conserver l'etat des categories dans un dictionnaire par nom de categorie.
- Creer une ligne UI par descriptor supporte:
  - label de propriete;
  - conteneur d'editeur;
  - cache de derniere valeur connue.
- La categorie doit pouvoir etre repliee depuis son header.
- Quand une categorie est repliee, masquer ses lignes avec `Visibility.Collapsed` ou equivalent local.
- Eviter `Clear` + recreation pendant `RefreshVisibleValues()`.
- Autoriser une reconstruction seulement dans la methode dediee appelee par changement d'objet/type.

Tests minimum:

- Replier une categorie masque ses lignes.
- L'etat replie reste conserve apres changement vers un autre objet du meme type.
- Le refresh ignore les lignes d'une categorie repliee.

Validation:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`

Commit attendu: `PropertyGrid step 03: add collapsible categories`.

### ☐ Etape 04 - Editeurs de valeurs MVP

Objectif: connecter les editeurs visuels aux descriptors sans melanger reflection et UI.

Actions:

- Introduire une interface interne, par exemple `IMGPropertyGridValueEditor`, avec:
  - `bool IsEditing { get; }`;
  - `void SetValueFromModel(object? value)`;
  - `bool TryCommitEditorValue(out object? value)`;
  - un acces a l'element UI racine de l'editeur.
- Implementer les editeurs:
  - `bool` via `MGCheckBox`, commit immediat au changement;
  - `string` via `MGTextBox`, commit sur `Enter` ou perte de focus;
  - `int`, `float`, `double` via `MGTextBox`, validation avant setter.
- Utiliser `CultureInfo.InvariantCulture` pour parse/format des nombres.
- En cas de valeur numerique invalide:
  - ne pas appeler le setter;
  - garder le texte saisi;
  - marquer l'editeur invalide avec un etat/style themeable, pas une couleur hardcodee.
- Ne pas ecraser un `MGTextBox` lorsque `IsEditing == true`.
- Ne pas appeler le setter si la valeur convertie est egale a la derniere valeur connue.

Tests minimum:

- Le `bool` applique le setter immediatement.
- Les nombres valides appliquent le setter.
- Les nombres invalides n'appellent pas le setter et ne jettent pas d'exception.
- `RefreshVisibleValues()` ne remplace pas le texte pendant `IsEditing == true`.

Validation:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`

Commit attendu: `PropertyGrid step 04: add MVP value editors`.

### ☐ Etape 05 - Refresh visible performant

Objectif: rendre `RefreshVisibleValues()` utilisable a chaque frame.

Actions:

- Parcourir uniquement les lignes visibles:
  - ignorer les categories repliees;
  - ignorer les lignes `Visibility.Collapsed`;
  - ignorer les lignes hors viewport du `MGScrollViewer` si les bounds disponibles permettent un test fiable.
- Pour le test viewport, utiliser les APIs de layout existantes (`ActualLayoutBounds`, `LayoutBounds`, bounds du scroll viewer) sans inventer de renderer.
- Ignorer les editeurs dont `IsEditing == true`.
- Lire la valeur via le `Getter` du descriptor.
- Comparer avec la derniere valeur connue avant de toucher l'editeur.
- Ne jamais refaire `type.GetProperties()` dans ce chemin.
- Limiter les allocations: pas de LINQ dans la boucle de refresh par frame si une simple boucle suffit.

Tests minimum:

- Une ligne repliee ou non visible n'est pas rafraichie.
- Une ligne en cours d'edition n'est pas rafraichie.
- Une valeur inchangee ne modifie pas l'editeur.
- Le compteur de reflection reste stable pendant plusieurs refreshs.

Validation:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`

Commit attendu: `PropertyGrid step 05: optimize visible refresh`.

### ☐ Etape 06 - Theme et templates decouples

Objectif: rendre le rendu et le theme du PropertyGrid configurables par les memes mecanismes que les autres controles.

Actions:

- Ajouter une constante `PropertyGridTemplateName = "PropertyGrid.Default"` dans `MGControlTemplateCatalog`.
- Enregistrer le template par defaut dans `RegisterDefaults`.
- Ajouter un template par defaut avec parts `PART_OuterBorder`, `PART_ScrollViewer`, `PART_CategoriesPanel`.
- Ajouter un template `Dark.PropertyGrid` dans `MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml` si un skin sombre distinct est necessaire.
- Ajouter un groupe runtime `MGThemePropertyGridSettings` dans `MGTheme.cs`, ou des `ThemePropertyTarget` explicites si le groupe est juge trop lourd.
- Les settings minimaux doivent couvrir:
  - padding global;
  - border brush/thickness;
  - background de header categorie;
  - foreground de header categorie;
  - padding/min-height de header categorie;
  - couleur de fleche de collapse;
  - foreground nom de propriete;
  - padding de ligne;
  - brush de separateur ou grid line;
  - decoration d'editeur invalide.
- Ajouter la definition declarative dans `MGUI.Core/UI/XAML/Themes.cs`.
- Ajouter l'application dans `ThemeDefinitionBuilder`.
- Ajouter la copie dans `MGTheme.ApplyFrom`.
- Dans `MGControlTemplateCatalog`, appliquer les valeurs avec `Context.ApplyThemeDefault(...)` ou `Context.ApplyTemplateValue(...)` selon le cas.
- Ajouter dans `BuiltInThemes.xaml`:
  - valeurs completes pour `Dark_Blue`;
  - valeurs ou overrides coherents pour `Dark`;
  - mapping de template pour `Dark` si `Dark.PropertyGrid` existe.
- Ne laisser aucune couleur de chrome PropertyGrid hardcodee dans `MGPropertyGrid`.

Tests minimum:

- Un test architecture verifie que `MGTheme` expose les settings PropertyGrid.
- Un test verifie que `Dark_Blue` charge des valeurs PropertyGrid.
- Un test verifie que `Dark` charge ses valeurs ou herite correctement de `Dark_Blue`.
- Un test verifie que `MGPropertyGrid` a un `DefaultControlTemplateName` et des parts requis.

Validation:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~ThemeDefinition"`

Commit attendu: `PropertyGrid step 06: add theme and templates`.

### ☐ Etape 07 - XAML, sample et documentation courte

Objectif: exposer le controle dans les usages normaux de MGUI.

Actions:

- Ajouter `PropertyGrid` dans `MGUI.Core/UI/XAML/Controls.cs`.
- Exposer au minimum les proprietes XAML utiles:
  - `SelectedObject` si le binding XAML local le permet;
  - options de layout heritees via la base;
  - aucun attribut avance hors MVP.
- Ajouter un sample dans `MGUI.Samples/Controls/PropertyGrid.xaml` et `.xaml.cs` ou l'emplacement de sample existant le plus coherent.
- Le sample doit montrer:
  - categories `Transform` et `Rendering`;
  - `bool`, `int`, `float`, `double`, `string`;
  - une categorie repliable;
  - appel de `RefreshVisibleValues()` dans le flux de mise a jour si le sample a un hook adapte.
- Ajouter une courte note dans `README.md` seulement si les autres controles y sont listes.

Tests minimum:

- Test XAML si le projet a deja des tests de parser pour controles.
- Build sample.

Validation:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

Commit attendu: `PropertyGrid step 07: expose sample and xaml`.

### ☐ Etape 08 - Passe finale de robustesse

Objectif: verifier le MVP complet et corriger uniquement les regressions liees au PropertyGrid.

Actions:

- Relire `Docs/propertygrid-fonctionnalites.md` et cocher mentalement chaque exigence MVP.
- Verifier qu'aucune boucle de refresh ne reconstruit l'UI.
- Verifier qu'aucune boucle de refresh ne refait la reflection.
- Verifier qu'un `TextBox` en edition n'est pas ecrase par un refresh.
- Verifier que les categories repliees ne rafraichissent pas leurs lignes.
- Verifier que `Dark_Blue` et `Dark` ont un rendu lisible.
- Corriger seulement les problemes PropertyGrid; ne pas refactorer des controles non lies.

Validation finale:

- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~PropertyGrid"`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter "FullyQualifiedName~ThemeDefinition"`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`

Commit attendu: `PropertyGrid step 08: finalize MVP validation`.

## Definition of done MVP

Le travail est termine uniquement si:

- toutes les etapes ci-dessus sont marquees `✅`;
- chaque etape terminee a son propre commit;
- `SelectedObject` reconstruit seulement quand necessaire;
- `RefreshVisibleValues()` est sans reconstruction et sans reflection;
- les categories peuvent etre repliees et conservent leur etat;
- les editeurs MVP appliquent correctement les setters;
- les valeurs invalides ne cassent pas l'editeur;
- les themes `Dark_Blue` et `Dark` couvrent le PropertyGrid;
- le chrome visuel passe par theme/template, pas par dessin direct dans le controle.