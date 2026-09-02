# Taches PropertyGrid

## Objectif

Etendre `MGPropertyGrid` au-dela du socle actuel (editeurs `Bool`/`Int`/`Float`/`Double`/`String`/`Color`) en gardant l'architecture descriptor + editeur extensible decrite dans `Docs/controls-architecture.md` (section PropertyGrid). Chaque extension doit reutiliser `MGPropertyGridDescriptor` / `MGPropertyGridEditorKind` / le cache de descriptors et le contrat interne des editeurs (`Element`, `IsEditing`, `ApplyValue`, `ValueCommitted`, `SetReadOnly`, `ApplyTheme`, `Dispose`), sans reflexion par frame ni allocation pendant le refresh.

Ce document est destine a un agent IA implementeur.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- Faire exactement 1 commit git par tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Si une tache est bloquee, la marquer `⛔`, decrire le blocage juste sous la tache, puis s'arreter.
- Pas de refactor hors perimetre de la tache en cours.
- Preserver les APIs publiques existantes sauf extension explicitement demandee.
- Ajouter ou adapter des tests dans la meme tache (voir `MGUI.Tests/Integration/PropertyGridTests.cs`).
- Garde-fous de perimetre (voir `Docs/Tasks/roadmap-tasks.md`) : pas de `DependencyObject`/`DependencyProperty` generalises, pas de moteur de triggers facon WPF.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
3. `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~PropertyGrid"`

## Taches

### ⚪ 1. Editeur d'enumerations

But:
supporter les proprietes `Enum` (le cas le plus demande apres les types simples).

Travail attendu:

- ajouter une valeur `Enum` a `MGPropertyGridEditorKind` et la resolution correspondante dans `MGPropertyGridDescriptor` (detecter `type.IsEnum`, gerer `[Flags]` en option ou le documenter comme hors perimetre) ;
- editeur imbrique base sur `MGComboBox` liste des valeurs de l'enum (noms via `DisplayNameAttribute` si present), respectant le contrat interne des editeurs ;
- commit immediat a la selection ; `SetReadOnly`/`ApplyTheme`/`Dispose` cables ;
- tests : selection commit le setter, valeur invalide ignoree, refresh live sans reset de l'edition en cours.

Criteres d'acceptation:

- une propriete enum est editable via une combo ; aucun setter appele si la valeur ne change pas ; couverture de test presente.

Commit recommande:

- `propertygrid: complete task 1 add enum editor`

### ⚪ 2. Editeurs de types vecteur/couleur composes

But:
editer `Vector2`/`Vector3`/`Vector4` (XNA et `System.Numerics`) via des sous-champs numeriques.

Travail attendu:

- editeurs composant plusieurs `MGNumericUpDown` (un par composante) derriere un seul descriptor ;
- reutiliser la logique de parsing `InvariantCulture` et de commit Enter/perte-de-focus du socle numerique ; commit atomique du vecteur complet ;
- s'appuyer sur le `PropertyGridColorAdapter` existant comme modele de conversion type-natif <-> valeur editable ;
- tests : edition d'une composante commit le vecteur entier ; saisie invalide sur une composante n'appelle pas le setter.

Criteres d'acceptation:

- Vector2/3/4 editables composante par composante avec commit atomique ; tests presents.

Commit recommande:

- `propertygrid: complete task 2 add vector editors`

### ⚪ 3. Undo/redo et multi-selection

But:
permettre l'annulation des commits et l'edition simultanee de plusieurs objets.

Travail attendu:

- pile d'undo/redo au niveau du PropertyGrid enregistrant (descriptor, ancienne valeur, nouvelle valeur) a chaque `ValueCommitted` ; raccourcis Ctrl+Z/Ctrl+Y routes via le pipeline clavier existant (respecter la preservation des touches de saisie en edition de texte) ;
- multi-selection : lier un descriptor a plusieurs cibles ; afficher une valeur indeterminee quand elles divergent ; commit applique a toutes les cibles ;
- tests : undo restaure la valeur precedente ; commit multi-cible ; etat indetermine.

Criteres d'acceptation:

- undo/redo fonctionnel sur les editeurs existants ; edition multi-cible verifiee par test.

Commit recommande:

- `propertygrid: complete task 3 add undo redo and multi selection`

### ⚪ 4. Recherche, filtrage et tri

But:
naviguer de grandes surfaces de proprietes.

Travail attendu:

- champ de recherche filtrant les descriptors par nom/display name ; filtrage par categorie ; tri alphabetique ou personnalise (via un comparateur injectable) ;
- reutiliser le modele de categories (`MGPropertyGridCategoryModel`) et le mecanisme de visibilite/masquage deja present ; aucun `GetProperties()` par frame ;
- tests : la recherche masque les non-correspondants sans casser l'edition en cours ; le tri est stable.

Criteres d'acceptation:

- recherche + filtrage + tri operationnels sans reflexion par frame ; tests presents.

Commit recommande:

- `propertygrid: complete task 4 add search filter and sort`
