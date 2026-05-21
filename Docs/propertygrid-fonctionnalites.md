# PropertyGrid — Résumé des fonctionnalités

## Objectif

Ajouter un contrôle `PropertyGrid` dans MGUI permettant d’inspecter et modifier les propriétés d’un objet sélectionné, avec une organisation par catégories et une édition simple des types de base.

Le contrôle doit être adapté à un usage éditeur temps réel : il peut être rafraîchi à chaque frame sans reconstruire toute l’interface.

---

## Fonctionnalités principales

### 1. Affichage d’un objet sélectionné

La `PropertyGrid` doit pouvoir recevoir un objet cible :

```csharp
propertyGrid.SelectedObject = selectedEntity;
```

Elle affiche ensuite les propriétés éditables de cet objet.

Le changement de `SelectedObject` déclenche une reconstruction de la grille uniquement si l’objet ou son type change.

---

### 2. Catégories de propriétés

Les propriétés doivent être regroupées par catégories.

Exemple d’affichage :

```text
Transform
  PositionX      [ 12.0 ]
  PositionY      [ 42.0 ]
  Rotation       [ 0.0  ]

Rendering
  Visible        [x]
  Name           [Player]
```

Les catégories doivent pouvoir être :

- affichées sous forme d’en-têtes ;
- ouvertes ou fermées ;
- conservées dans leur état précédent si possible ;
- utilisées pour masquer les propriétés non visibles lors du refresh.

---

### 3. Édition des types simples

La première version de la `PropertyGrid` doit supporter uniquement les types suivants :

| Type | Éditeur |
|---|---|
| `bool` | `CheckBox` |
| `int` | `TextBox` numérique |
| `float` | `TextBox` numérique |
| `double` | `TextBox` numérique |
| `string` | `TextBox` |

Les types complexes comme `Vector2`, `Vector3`, `Color`, `Enum`, `Texture`, `Material`, etc. sont exclus de la première version.

---

### 4. Modification des valeurs

La modification d’une valeur doit être propagée à l’objet cible via un setter.

Comportement recommandé :

| Type | Moment du commit |
|---|---|
| `bool` | immédiat au changement de la checkbox |
| `int` | `Enter` ou perte de focus |
| `float` | `Enter` ou perte de focus |
| `double` | `Enter` ou perte de focus |
| `string` | `Enter` ou perte de focus |

Les valeurs numériques doivent être validées avant d’être appliquées.

Une valeur invalide ne doit pas casser l’éditeur.

---

### 5. Refresh temps réel

La `PropertyGrid` doit pouvoir être rafraîchie à chaque frame :

```csharp
propertyGrid.RefreshVisibleValues();
```

Le refresh ne doit pas reconstruire la grille.

Il doit uniquement :

- relire les valeurs de l’objet sélectionné ;
- comparer avec la dernière valeur connue ;
- mettre à jour l’éditeur visuel uniquement si la valeur a changé ;
- ignorer les propriétés non visibles ;
- ignorer les champs actuellement en cours d’édition.

---

### 6. Protection pendant l’édition

Quand l’utilisateur est en train de modifier une valeur dans un `TextBox`, le refresh automatique ne doit pas écraser le texte en cours de saisie.

Exemple problématique à éviter :

```text
L’utilisateur tape : 12.
La valeur actuelle reste : 12
Le refresh réécrit : 12
Le texte utilisateur est perdu
```

Chaque éditeur doit donc exposer un état du type :

```csharp
editor.IsEditing
```

Pendant que `IsEditing == true`, la valeur visuelle ne doit pas être remplacée par le refresh automatique.

---

### 7. Cache des descriptors

La réflexion ne doit être utilisée que lorsque le type de l’objet change.

Le résultat de l’analyse des propriétés doit être mis en cache :

```csharp
Dictionary<Type, List<PropertyGridDescriptor>>
```

À chaque frame, la `PropertyGrid` ne doit pas appeler :

```csharp
type.GetProperties()
```

Le refresh doit utiliser uniquement les descriptors déjà construits.

---

### 8. Descriptors de propriétés

Chaque propriété affichée doit être décrite par un descriptor indépendant de l’UI.

Exemple de structure :

```csharp
public sealed class PropertyGridDescriptor
{
    public string Name { get; init; }
    public string DisplayName { get; init; }
    public string Category { get; init; }
    public Type PropertyType { get; init; }
    public PropertyEditorKind EditorKind { get; init; }

    public Func<object, object?> Getter { get; init; }
    public Action<object, object?> Setter { get; init; }

    public bool IsReadOnly { get; init; }
}
```

Cette séparation permet de ne pas mélanger :

- l’analyse de l’objet ;
- la catégorisation ;
- le binding des valeurs ;
- la création des contrôles visuels.

---

### 9. Réutilisation des lignes et des éditeurs

La `PropertyGrid` doit éviter de créer et détruire des contrôles en continu.

Elle doit privilégier :

- la réutilisation des lignes existantes ;
- le pooling des éditeurs si nécessaire ;
- la mise à jour des valeurs plutôt que la reconstruction de l’arbre UI.

---

### 10. Propriétés visibles uniquement

Pour rester performante, la `PropertyGrid` doit rafraîchir uniquement les propriétés visibles.

Une propriété est considérée comme non visible si :

- sa catégorie est fermée ;
- elle est hors de la zone visible dans un conteneur scrollable ;
- elle est filtrée ou masquée.

---

## Contraintes de performance

La `PropertyGrid` doit respecter les règles suivantes :

- ne pas reconstruire la grille à chaque frame ;
- ne pas refaire de réflexion à chaque frame ;
- ne pas recréer les contrôles à chaque frame ;
- ne pas rafraîchir les propriétés invisibles ;
- ne pas modifier visuellement un éditeur pendant que l’utilisateur tape ;
- ne pas appeler le setter si la valeur n’a pas réellement changé ;
- limiter les allocations pendant le refresh.

---

## Comportement attendu

### Changement d’objet sélectionné

```text
SelectedObject change
    -> récupérer les descriptors depuis le cache
    -> reconstruire les catégories et les lignes si nécessaire
    -> créer les éditeurs adaptés
```

### Refresh par frame

```text
Chaque frame
    -> parcourir les lignes visibles
    -> ignorer les éditeurs en cours d’édition
    -> lire la valeur actuelle
    -> comparer avec la dernière valeur connue
    -> mettre à jour l’éditeur uniquement si nécessaire
```

### Modification utilisateur

```text
Utilisateur modifie une valeur
    -> validation
    -> conversion vers le bon type
    -> setter sur l’objet cible
    -> mise à jour de la valeur cachée
```

---

## Fonctionnalités hors scope pour la première version

Les fonctionnalités suivantes ne sont pas nécessaires pour le MVP :

- édition de `Vector2`, `Vector3`, `Color` ;
- édition des `enum` ;
- édition des références d’assets ;
- édition des listes et collections ;
- édition des objets imbriqués ;
- undo / redo ;
- multi-selection ;
- recherche de propriétés ;
- filtrage ;
- attributs avancés comme `Range`, `ReadOnly`, `Browsable`, etc.

Ces fonctionnalités pourront être ajoutées plus tard si l’architecture des descriptors et des éditeurs reste extensible.

---

## Extensions prévues plus tard

L’architecture doit permettre d’ajouter facilement :

- `Vector2PropertyEditor` ;
- `Vector3PropertyEditor` ;
- `ColorPropertyEditor` ;
- `EnumPropertyEditor` ;
- `RangeSliderPropertyEditor` ;
- `AssetReferencePropertyEditor` ;
- support du undo / redo ;
- support de la multi-sélection ;
- recherche et filtrage ;
- tri alphabétique ou tri personnalisé ;
- attributs MGUI spécifiques.

---

## Résumé MVP

La première version doit fournir :

- un contrôle `PropertyGrid` ;
- une propriété `SelectedObject` ;
- des catégories pliables ;
- l’affichage des propriétés publiques éditables ;
- l’édition des types `bool`, `int`, `float`, `double`, `string` ;
- un cache de reflection par type ;
- un refresh temps réel performant ;
- une protection contre l’écrasement des valeurs pendant l’édition ;
- une séparation claire entre descriptors, lignes UI et éditeurs de valeurs.

---

## Principe fondamental

La `PropertyGrid` doit être conçue comme un contrôle persistant.

Elle peut être rafraîchie à chaque frame, mais elle ne doit pas être reconstruite à chaque frame.

```text
Correct :
    Build once
    Refresh visible values every frame

Incorrect :
    Clear children
    Reflect properties
    Recreate controls
    Every frame
```
