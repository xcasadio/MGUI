# Tâches de correction des fondations MGUI

> Contexte : framework UI pour jeu vidéo (MonoGame), **pas une reproduction WPF**.
> Chaque tâche doit être commitée séparément.
> Les lacunes WPF (DependencyProperty, RoutedEvents, Tab navigation, ControlTemplate, Triggers, etc.) sont des choix assumés et **ne sont pas corrigées ici**.

---

## Priorité 1 — Auto-focus au clic pour éléments `IsFocusable` (risque bug élevé)

### Problème
Chaque contrôle qui définit `IsFocusable = true` doit manuellement brancher `MouseHandler.LMBPressedInside += Focus()`. Si un développeur oublie ce branchement dans un futur contrôle, l'élément ne prendra jamais le focus au clic, causant des bugs de navigation clavier silencieux et difficiles à diagnostiquer.

Contrôles actuellement affectés : `MGListView`, `MGListBox`, `MGTreeView`.

### Tâche 1a — Ajouter l'auto-focus dans `MGElement.UpdateSelf`

**Fichier :** `MGUI.Core/UI/MGElement.cs`

**Action :** Dans la méthode `UpdateSelf`, après le `ManualUpdate()` des handlers existants (ou dans une section appropriée), ajouter une logique qui appelle `Focus()` automatiquement quand :
1. `IsFocusable == true`
2. Le LMB vient d'être pressé à l'intérieur de cet élément (`InputTracker.Mouse.IsPressedInside(MouseButton.Left, this)` && c'est un nouveau press ce frame)

**Approche recommandée :** Plutôt que de modifier `UpdateSelf` (qui n'existe pas en tant que méthode unifiée dans la base), ajouter un abonnement dans le constructeur de `MGElement` via un pattern lazy similaire à `_MouseHandler` :

```csharp
// Dans MGElement, ajouter un champ :
private bool _autoFocusSubscribed;

// Dans le setter de IsFocusable ou dans une méthode EnsureAutoFocusSubscription() :
private void EnsureAutoFocusSubscription()
{
    if (IsFocusable && !_autoFocusSubscribed)
    {
        _autoFocusSubscribed = true;
        MouseHandler.LMBPressedInside += (sender, e) =>
        {
            if (IsFocusable)
                Focus();
        };
    }
}
```

**Attention :** `MouseHandler` est lazy-initialisé (`_MouseHandler ??= ...`). L'auto-souscription doit se faire dans le setter de `IsFocusable` pour ne créer le handler que si `IsFocusable` passe à `true`. Vérifier que ça n'impacte pas les éléments qui ne sont jamais focusables (pas d'allocation inutile de `MouseHandler`).

**Contrainte perf :** Ne pas créer de `MouseHandler` pour les éléments non-focusables (ex: `MGTextBlock`, `MGSpacer`). Le getter `MouseHandler` instancie le handler à la demande — ne le toucher que si `IsFocusable = true`.

**Commit :** `fix(focus): auto-focus on LMB press for IsFocusable elements`

---

### Tâche 1b — Supprimer les appels `Focus()` manuels redondants

**Fichiers :**
- `MGUI.Core/UI/MGListView.cs` — ligne `DataGrid.MouseHandler.LMBPressedInside += (sender, e) => Focus();`
- `MGUI.Core/UI/MGListBox.cs` — `Focus();` dans le handler `LMBPressedInside`
- `MGUI.Core/UI/MGTreeView.cs` — vérifier si un `Focus()` au clic existe (actuellement absent — c'était justement le bug)

**Action :** Supprimer les lignes `Focus()` manuelles dans les handlers `LMBPressedInside` des contrôles qui définissent `IsFocusable = true`, puisque l'auto-focus de la tâche 1a le fait maintenant automatiquement.

**Attention pour MGListBox :** Le handler `LMBPressedInside` fait d'autres choses en plus de `Focus()` (gestion du `PressedItem`). Ne supprimer **que** la ligne `Focus();`, pas le reste du handler.

**Attention pour MGListView :** Le `DataGrid.MouseHandler.LMBPressedInside += (sender, e) => Focus();` appelle `Focus()` sur le `MGListView`, pas sur le `DataGrid`. C'est le ListView qui est focusable. Il faut s'assurer que l'auto-focus de 1a se déclenche sur le bon élément. Si le clic arrive sur le `DataGrid` (enfant) et non directement sur le `MGListView`, l'auto-focus de 1a ne se déclenchera que si c'est le ListView qui gère l'event. **Analyser le flux** : si le `DataGrid` consomme le clic, l'auto-focus du ListView ne se déclenchera pas → dans ce cas, garder la souscription manuelle sur `DataGrid.MouseHandler`.

**Commit :** `refactor(focus): remove redundant manual Focus() calls`

---

### Tâche 1c — Ajouter un test unitaire pour l'auto-focus

**Fichier :** `MGUI.Tests/KeyboardNav/` (nouveau fichier ou fichier existant)

**Action :** Écrire un test qui vérifie que :
1. Un élément avec `IsFocusable = true` reçoit le focus au clic (simulated LMBPressedInside)
2. Un élément avec `IsFocusable = false` ne reçoit pas le focus au clic
3. L'ancien focus est bien remplacé quand on clique un nouvel élément focusable

**Commit :** `test(focus): add auto-focus on click tests`

---

## Priorité 2 — Garde fragile dans l'application des styles XAML (risque bug élevé)

### Problème
Dans `Element.ProcessStyles`, la condition pour ne pas écraser une valeur explicitement définie est :
```csharp
PropertyInfo.GetValue(this) == default
```

C'est fragile car :
- Pour `bool`, `default == false` → un setter de style ne s'appliquera jamais si la propriété est déjà `false` (même si c'est le vrai défaut)
- Pour `int`, `default == 0` → idem
- Pour les propriétés initialisées dans le constructeur (ex: `Padding = new Thickness(4)`), la garde pense que c'est "non-default" et **refuse le style** même si aucune valeur XAML n'a été explicitement positionnée

### Tâche 2a — Ajouter un tracking des propriétés explicitement définies en XAML

**Fichier :** `MGUI.Core/UI/XAML/Element.cs`

**Action :** Ajouter un `HashSet<string> _explicitlySetProperties` sur la classe `Element`. Ce set sera rempli automatiquement pendant le parsing XAML.

Portable.Xaml appelle les setters des propriétés pendant le parsing. Pour détecter quelles propriétés ont été explicitement positionnées dans le XAML, on peut remplir ce set dans `ApplyBaseSettings` et `ApplyDerivedSettings` — les propriétés qui ont une valeur non-null dans le XAML intermédiaire (ex: `Margin.HasValue`, `Padding.HasValue`, etc.) sont explicites.

Mais l'approche la plus robuste est de **capturer les noms de propriétés settées par Portable.Xaml** en utilisant la mécanique existante : dans `ApplyBaseSettings`, toute propriété conditionnée par `if (Xxx.HasValue)` ou `if (Xxx != null)` **est** une propriété explicitement définie en XAML. Collecter ces noms dans le set.

**Approche concrète :**
1. Ajouter `internal HashSet<string> ExplicitlySetProperties { get; } = new();` sur `Element`
2. Dans `ApplyBaseSettings`, après chaque `if (Margin.HasValue) { Element.Margin = ...; }`, ajouter `ExplicitlySetProperties.Add("Margin");` — mais ce tracking doit être propagé. Stocker les noms des propriétés **du MGElement** cible (pas du XAML intermédiaire).
3. Stocker ce set dans `MGElement.Metadata` sous une clé dédiée (ex: `"XamlExplicitProps"`) pour qu'il soit accessible dans `ProcessStyles`.

**Commit :** `fix(styles): track explicitly-set XAML properties`

---

### Tâche 2b — Utiliser le tracking dans `ProcessStyles` au lieu de `== default`

**Fichier :** `MGUI.Core/UI/XAML/Element.cs`

**Action :** Modifier `ProcessStyles` pour remplacer :
```csharp
if (ModifiedPropertyNames.Contains(PropertyName) 
    || PropertyInfo.GetValue(this) == default)
```
par :
```csharp
if (ModifiedPropertyNames.Contains(PropertyName) 
    || !ExplicitlySetProperties.Contains(PropertyName))
```

Cela signifie : "appliquer le style SI la propriété n'a pas été explicitement définie en XAML (et n'a pas déjà été modifiée par un style précédent dans l'arbre)".

**Attention :**
- L'`Element` XAML intermédiaire est le `this` dans `ProcessStyles` — c'est bien l'objet qui a le `ExplicitlySetProperties`.
- Le `PropertyName` du `Setter` correspond à une propriété de l'`Element` XAML, pas du `MGElement`. Vérifier que les noms correspondent (ils devraient — `ProcessStyles` résout via `ThisType.GetProperty(PropertyName)`).
- Faire les 2 remplacements (pour les styles implicites ET explicites).

**Commit :** `fix(styles): use explicit property tracking instead of default guard`

---

### Tâche 2c — Ajouter un test pour le scénario de style vs propriété explicite

**Fichier :** `MGUI.Tests/` (nouveau fichier `Text/StyleApplicationTests.cs` ou similaire)

**Action :** Écrire un test qui vérifie :
1. Un style implicite s'applique correctement sur une propriété non-définie en XAML
2. Un style implicite **ne s'applique pas** si la propriété a été explicitement définie en XAML
3. Le scénario fragile précédent est corrigé : un élément dont le constructeur initialise une propriété à une valeur non-default doit **quand même** recevoir le style si la propriété n'a pas été explicitement positionnée en XAML

**Commit :** `test(styles): verify style vs explicit property precedence`

---

## Priorité 3 — Blocage modal non centralisé (risque bug moyen)

### Problème
Chaque contrôle qui gère des interactions (MGGrid, MGSlider, MGScrollViewer, MGRadioButton, etc.) vérifie manuellement `ParentWindow.HasModalWindow` avant de traiter les inputs. Un futur contrôle qui oublie cette garde permettra l'interaction à travers une fenêtre modale.

**Observations du code :**
- `MGElement` vérifie **déjà** `HasModalWindow` dans `ComputeTopmostHoveredElement` (ligne 1623) et dans le calcul du `SecondaryVisualState` (ligne 1701). 
- Cela signifie que `IsHovered` et `VisualState.Pressed/Hovered` sont **déjà** masqués quand un modal est actif.
- Les vérifications manuelles dans les contrôles servent pour des cas **spécifiques** : handlers souris custom qui ne passent pas par le VisualState (ex: sélection de cellule dans MGGrid, drag du thumb dans MGSlider).

### Tâche 3a — Centraliser le blocage modal dans `_CanReceiveMouseInput`

**Fichier :** `MGUI.Core/UI/MGElement.cs`

**Action :** Dans le calcul de `_CanReceiveMouseInput` (section `Update()` autour de ligne 1750), ajouter la condition `!SelfOrParentWindow.HasModalWindow` (sauf si l'élément appartient à la fenêtre modale elle-même).

```csharp
// Ligne ~1750, dans le bloc de recomputation de _CanReceiveMouseInput :
bool isBlockedByModal = SelfOrParentWindow.HasModalWindow 
    && !SelfOrParentWindow.ModalWindow.IsSelfOrAncestorOf(this);
_CanReceiveMouseInput = BaseCanReceiveInput && parentCanMouse && !isBlockedByModal;
```

**Attention :** 
- La fenêtre modale elle-même et ses enfants **doivent** pouvoir recevoir les inputs.
- `IsSelfOrAncestorOf` est déjà implémenté dans `MGElement`.
- Vérifier que ça ne casse pas les ToolTips et ContextMenus qui sont des fenêtres séparées (elles ne sont pas des `NestedWindows`/`ModalWindow`, elles sont sur le Desktop → pas impactées).
- La fenêtre modale est stockée sur `ParentWindow` (pas sur Desktop) → vérifier la chaîne : `SelfOrParentWindow.HasModalWindow` retourne `true` pour les éléments de la fenêtre parente, pas pour les éléments du modal lui-même.

**Contrainte perf :** `IsSelfOrAncestorOf` remonte l'arbre via `Parent`. Sur un arbre profond, c'est O(depth). Mais `_CanReceiveMouseInput` est déjà conditionné par le dirty-flag et n'est recalculé que quand l'état change. Pour éviter le coût récurrent, on peut cacher le résultat du modal-check dans un flag supplémentaire, invalidé quand `ModalWindow` change (via l'event `PropertyChanged` de la fenêtre parente).

**Approche alternative (plus simple, perf OK) :** Plutôt que `IsSelfOrAncestorOf`, vérifier que `SelfOrParentWindow` === la fenêtre modale ou n'a pas de modal actif. Puisque les éléments du modal ont pour `SelfOrParentWindow` la fenêtre modale elle-même (qui n'a pas de `HasModalWindow` sauf si un 2e modal est ouvert), cela fonctionne naturellement :

```csharp
bool isBlockedByModal = SelfOrParentWindow.HasModalWindow;
// Les enfants du modal ont SelfOrParentWindow = modalWindow, qui n'a pas de HasModalWindow
// → isBlockedByModal = false pour eux → OK
_CanReceiveMouseInput = BaseCanReceiveInput && parentCanMouse && !isBlockedByModal;
```

Vérifier que cette logique est correcte en lisant comment `SelfOrParentWindow` est résolu pour les éléments à l'intérieur d'un modal.

**Commit :** `fix(modal): centralize modal input blocking in _CanReceiveMouseInput`

---

### Tâche 3b — Supprimer les vérifications `HasModalWindow` manuelles redondantes

**Fichiers concernés :**
- `MGRatingControl.cs` — `if (ParentWindow.HasModalWindow)` (ligne ~446)
- `MGScrollViewer.cs` — `!ParentWindow.HasModalWindow` (lignes ~414-415)
- `MGSlider.cs` — `!ParentWindow.HasModalWindow` (lignes ~928, ~996)
- `MGResizeGrip.cs` — `!ParentWindow.HasModalWindow` (ligne ~265)
- `MGRadioButton.cs` — `!ParentWindow.HasModalWindow` (ligne ~319)
- `MGGridSplitter.cs` — `!ParentWindow.HasModalWindow` (ligne ~526)
- `MGUniformGrid.cs` — `!ParentWindow.HasModalWindow` (lignes ~710, ~719)
- `MGGrid.cs` — `!ParentWindow.HasModalWindow` (lignes ~905, ~914)

**Action :** Supprimer ces vérifications — elles deviennent redondantes car `_CanReceiveMouseInput` retourne déjà `false` quand un modal bloque l'élément. Les handlers souris ne se déclenchent que si `CanReceiveMouseInput()` retourne `true`.

**Attention :** Vérifier que chaque handler utilise bien le `MouseHandler` standard (qui check `CanReceiveMouseInput`). Si un handler utilise `SelectionMouseHandler` ou un handler custom avec `AlwaysHandlesEvents = true` ou `InvokeEvenIfHandled = true`, il **bypass** le check et nécessite toujours la garde manuelle. **Lire le code de chaque handler avant de supprimer**.

**Commit :** `refactor(modal): remove redundant HasModalWindow checks`

---

### Tâche 3c — Ajouter un test pour le blocage modal centralisé

**Fichier :** `MGUI.Tests/` (nouveau fichier)

**Action :** Écrire un test qui vérifie :
1. Un élément derrière un modal a `_CanReceiveMouseInput == false`
2. Un élément à l'intérieur du modal a `_CanReceiveMouseInput == true`
3. Après fermeture du modal, l'élément derrière redevient `_CanReceiveMouseInput == true`

**Commit :** `test(modal): verify centralized modal blocking`

---

## Résumé

| # | Tâche | Risque corrigé | Commits |
|---|---|---|---|
| 1a | Auto-focus `IsFocusable` au clic | Oubli de `Focus()` dans futurs contrôles | `fix(focus): auto-focus on LMB press for IsFocusable elements` |
| 1b | Supprimer `Focus()` manuels redondants | Code dupliqué | `refactor(focus): remove redundant manual Focus() calls` |
| 1c | Test auto-focus | Régression | `test(focus): add auto-focus on click tests` |
| 2a | Tracking propriétés XAML explicites | Styles écrasant des valeurs explicites | `fix(styles): track explicitly-set XAML properties` |
| 2b | Remplacer `== default` par tracking | Styles non-appliqués ou mal-appliqués | `fix(styles): use explicit property tracking instead of default guard` |
| 2c | Test styles vs propriétés explicites | Régression | `test(styles): verify style vs explicit property precedence` |
| 3a | Modal centralisé dans `_CanReceiveMouseInput` | Interaction à travers une modale | `fix(modal): centralize modal input blocking in _CanReceiveMouseInput` |
| 3b | Supprimer `HasModalWindow` manuels | Code dupliqué | `refactor(modal): remove redundant HasModalWindow checks` |
| 3c | Test blocage modal | Régression | `test(modal): verify centralized modal blocking` |
