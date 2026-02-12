# MGDockHost - Liste de Tâches

## État Actuel
L'implémentation actuelle comprend les fondations du modèle de layout avec drag & drop basique.
Cette liste organise les tâches restantes en phases MVP → V2 → V3.

---

## 🎯 MVP (DockHost Utilisable)

### 1. Corrections Bugs Drag & Drop
- [x] **1.1** Corriger le bug de panels fermés lors du dock
  - ✅ Empêcher de split-dock le dernier panel d'un groupe vers ce même groupe
  - ✅ Gestion correcte du cleanup des groupes vides
  - ✅ Vérification si target est descendant du groupe source

- [x] **1.2** Corriger le bug de panels qui disparaissent
  - ✅ Détacher targetNode avant de créer le nouveau split
  - ✅ Éviter la corruption des références parent lors des assignations
  - ✅ Garantir la cohérence de l'arbre après docks successifs

- [x] **1.3** Ajouter annulation du drag avec touche ESC
  - ✅ Détection de la touche ESC dans UpdateSelf
  - ✅ Appel à CancelDrag() qui restaure l'état initial

- [x] **1.4** Seuil de déplacement avant démarrage du drag (threshold pixels)
  - ✅ Propriété DragThreshold configurable (défaut: 5px)
  - ✅ Pas d'effet visuel avant d'avoir déplacé le seuil
  - ✅ Opacité et preview ne s'activent qu'après dépassement

### 2. Splitters Robustes
- [x] **2.1** Le comportement du splitter drag fonctionne bien
  - ✅ Clamp aux min sizes en place
  - ✅ Redistribution des ratios opérationnelle
  - ✅ Taille minimum bloque correctement le splitter

- [ ] **2.2** Propager MinWidth/MinHeight dans les splits imbriqués
  - Calculer les contraintes min récursivement
  - Empêcher les splits trop petits

- [ ] **2.3** Améliorer le feedback visuel du splitter (curseur, highlight)

### 3. Tabs Fonctionnels
- [x] **3.0** Corriger l'apparence des tab headers
  - ✅ Retirer les bordures du bouton close
  - ✅ Background transparent pour le bouton
  - ✅ Style hover pour le bouton
  - ✅ Meilleur positionnement et centrage du bouton

- [x] **3.1** Bouton Close sur les tabs
  - ✅ Afficher bouton X si `CanClose = true`
  - ✅ Gérer le clic et appeler `RemovePanel()`

- [x] **3.2** Réordonner les tabs dans le même groupe (drag reorder)
  - ✅ Détecter drag intra-groupe
  - ✅ Calculer l'index cible basé sur la position X
  - ✅ Réordonner sans créer de split

- [ ] **3.3** Sélection visuelle de l'onglet actif
  - Style différent pour tab active vs inactive
  - Highlight au hover

### 4. Preview & Indicateurs de Drop
- [x] **4.1** Améliorer le preview rectangle ✅ (Corrigé le 2026-02-12)
  - ✅ CurrentDropTarget maintenant mis à jour dans UpdateDragPreview
  - ✅ La prévisualisation s'affiche correctement pendant le drag
  - ✅ Rectangle semi-transparent bleu avec bordure
  - ⚠️ Future amélioration : Animation fade-in/out
  - ⚠️ Future amélioration : Couleur personnalisable par thème

- [ ] **4.2** Indicateurs joystick (optionnel MVP)
  - Afficher icônes L/R/T/B/Center au survol d'un groupe
  - Highlight de la zone sous la souris

### 5. Persistance Layout (Save/Load)
- [ ] **5.1** Sérialiser le DockLayoutModel en JSON
  - Parcourir l'arbre récursivement
  - Sauvegarder : type de nœud, IDs, ratios, onglet actif

- [ ] **5.2** Désérialiser et reconstruire l'arbre
  - Parser le JSON
  - Recréer les nœuds et liens parent/enfant
  - Reconnecter les panels via DockableId

- [ ] **5.3** Gérer les panels manquants au restore
  - Ignorer les IDs inconnus
  - Logger un warning
  - Nettoyer les groupes vides

- [ ] **5.4** Versionner le format JSON
  - Ajouter un champ "version"
  - Gérer la migration si nécessaire

---

## 🚀 V2 (Niveau IDE)

### 6. Registre des Dockables
- [ ] **6.1** Créer `DockableDefinition` avec métadonnées
  - DockableId (string unique)
  - Title, Icon, CanClose, CanFloat, CanAutoHide
  - DockableType (Document / Tool)

- [ ] **6.2** Registre centralisé `DockableRegistry`
  - `Register(DockableDefinition)`
  - `GetById(string id)`
  - `GetAll()`, `GetVisible()`, `GetHidden()`

- [ ] **6.3** Lifecycle events
  - `OnShown`, `OnHidden`, `OnClosed`, `OnActivated`
  - Invoquer depuis MGDockHost aux moments appropriés

- [ ] **6.4** Menu "View/Window" pour réafficher les panels cachés
  - Lister les panels enregistrés mais non visibles
  - Permettre de les ajouter au layout

### 7. Document vs Tool
- [ ] **7.1** Ajouter `DockableType` enum (Document / Tool)
  - Les Documents vont au centre
  - Les Tools vont sur les côtés

- [ ] **7.2** Zone centrale "DocumentArea"
  - Désigner une zone pour les documents
  - Les tools ne peuvent pas y aller

### 8. Fenêtres Flottantes
- [ ] **8.1** Créer `MGFloatingWindow`
  - Fenêtre overlay interne à MGUI
  - Contient un DockTabGroup
  - Draggable, resizable

- [ ] **8.2** Détacher un panel en floating
  - Au drop hors du host, créer une floating window
  - Transférer le panel

- [ ] **8.3** Redock depuis floating
  - Démarrer un drag depuis floating
  - Permettre le dock dans le host principal

- [ ] **8.4** Z-order des floating windows
  - Clic = bring to front
  - Pile d'ordre gérée

### 9. Règles de Docking
- [ ] **9.1** Méthode `CanDockTo(DockableDefinition, DockNode, DockZone)`
  - Retourne bool
  - Basée sur DockableType et règles custom

- [ ] **9.2** Familles de docking
  - Grouper les dockables par famille
  - Seuls les membres d'une même famille peuvent se tab ensemble

- [ ] **9.3** Zones autorisées par dockable
  - Liste des zones permises (Left, Right, etc.)
  - Désactiver les indicateurs des zones interdites

### 10. Overflow Tabs & Menus
- [ ] **10.1** Détection overflow (trop d'onglets)
  - Mesurer la largeur totale vs disponible

- [ ] **10.2** Chevrons gauche/droite pour scroller
  - Boutons < > pour scroll horizontal

- [ ] **10.3** Dropdown "liste des tabs"
  - Icône dropdown à droite
  - Popup avec tous les onglets
  - Clic = sélectionner

- [ ] **10.4** Menu contextuel sur tab
  - Close, Close Others, Close All
  - Float, Dock (si floating)
  - Pin (si auto-hide activé)

---

## 🌟 V3 (Waouh)

### 11. Auto-Hide (Pin/Unpin)
- [ ] **11.1** Bouton Pin/Unpin sur les tabs
  - Toggle `IsPinned`
  - Si unpin → déplacer vers languette

- [ ] **11.2** Languettes sur les bords
  - Bande étroite Left/Right/Top/Bottom
  - Afficher une icône + titre court pour chaque panel unpinned

- [ ] **11.3** Drawer (overlay) au survol/clic
  - Animer l'ouverture du panel depuis le bord
  - Se ferme au focus loss ou clic ailleurs

- [ ] **11.4** Mémoriser la taille du drawer
  - Persister la largeur/hauteur du drawer par panel
  - Restaurer au réaffichage

### 12. Maximize/Restore
- [ ] **12.1** Bouton Maximize sur un groupe
  - Le groupe occupe tout le host
  - Les autres groupes sont masqués

- [ ] **12.2** Bouton Restore
  - Revenir à l'état précédent

- [ ] **12.3** Pile d'états pour restore
  - Stack des layouts avant maximize
  - Pop au restore

### 13. Proximity Docking
- [ ] **13.1** Docking sans joystick
  - Détecter l'approche d'un bord (ex: <30px)
  - Activer automatiquement la zone correspondante

- [ ] **13.2** Drop sur splitter existant
  - Autoriser le drop sur un splitter bar
  - Insérer dans le split parent comme 3ème enfant (créer nested split)

### 14. Focus & Polish
- [ ] **14.1** Focus manager centralisé
  - `ActiveDockable` property sur MGDockHost
  - Highlight visuel du panel actif

- [ ] **14.2** "Bring to front" des floating windows
  - Au clic, mettre la fenêtre au premier plan

- [ ] **14.3** Navigation clavier Ctrl+Tab
  - Ouvrir popup de sélection rapide
  - Naviguer entre les panels ouverts

- [ ] **14.4** Performance : ne pas render les panels cachés
  - Désactiver Update/Draw pour auto-hide fermé
  - Lazy content creation

- [ ] **14.5** DPI / Scaling
  - Tailles minimum adaptatives
  - Seuils de drag ajustés

---

## 📋 Résumé par Phase

| Phase | Features Principales | État |
|-------|---------------------|------|
| MVP   | Bugs fix, tabs close/reorder, save/load | 🔴 À faire |
| V2    | Registry, floating, rules, overflow menus | 🔴 À faire |
| V3x] ~~Bug : Headers des tabs avec bordures visibles sur le bouton~~ ✅ Corrigé
2. [x] ~~Bug : Panels fermés lors du dock dans certains cas~~ ✅ Corrigé
3. [ ] Tester edge cases : layouts très imbriqués
4. [ ] Valider le comportement avec multiples opérations consécutive
## 🐛 Bugs Connus à Investiguer

1. [x] ~~Bug : Headers des tabs avec bordures visibles~~ ✅ Corrigé (MGBorder sans bordure)
2. [x] ~~Bug : Panels fermés lors du dock~~ ✅ Corrigé (vérification descendant + dernier panel)
3. [x] ~~Bug : Panels disparaissent lors de docks successifs~~ ✅ Corrigé (détachement avant split)
4. [x] ~~Bug : Pas de prévisualisation pendant le drag~~ ✅ Corrigé (2026-02-12 - CurrentDropTarget mis à jour dans UpdateDragPreview)
5. [x] ~~Bug : Panel déplacé accidentellement vers autre groupe~~ ✅ Corrigé (2026-02-12 - TabIndex calculé et utilisé pour les déplacements entre groupes)
6. [x] ~~Bug : Impossible de split depuis le même groupe~~ ✅ Corrigé (2026-02-12 - TabHeadersBounds pour restreindre zone Center aux headers)
7. [x] ~~Bug : Pas de rectangle de prévisualisation lors du réordonnancement~~ ✅ Corrigé (2026-02-12 - CalculateTabReorderPreviewRect pour ligne d'insertion)
8. [x] ~~Bug : Conflit réordonnancement vs zones de split~~ ✅ Corrigé (2026-02-12 - Révision algorithme avec priorité headers)
9. [ ] Tester edge cases : layouts très imbriqués avec de multiples niveaux
10. [ ] Valider le comportement avec séquences complexes d'opérations

### 🔧 Corrections récentes (2026-02-12)

**Problème 1 : Pas de prévisualisation du drag**
- **Symptôme :** Lors du drag d'un tab, aucun rectangle bleu semi-transparent n'apparaissait pour montrer où le panel serait déposé
- **Cause :** `CurrentDropTarget` n'était jamais mis à jour dans `UpdateDragPreview()`, donc `ShowPreview()` n'était pas appelée avec les bons paramètres
- **Solution :** Ajout de `CurrentDropTarget = dropTarget;` dans `UpdateDragPreview()` après le calcul du drop target
- **Fichier modifié :** `MGDockHost.cs` ligne ~237

**Problème 2 : Panel "fermé" ou déplacé non intentionnellement**  
- **Symptôme :** En essayant de réordonner un tab, il était déplacé vers un autre groupe invisible ou hors écran
- **Cause :** Lors du déplacement entre groupes, le `TabIndex` était toujours forcé à `-1` (fin de liste), ignorant la position calculée de la souris
- **Solution :** Utilisation du `TabIndex` calculé dans `ExecuteDrop()` : `int insertIndex = target.TabIndex >= 0 ? target.TabIndex : -1;`
- **Fichier modifié :** `MGDockHost.cs` ligne ~409
- **Bénéfice :** Avec la prévisualisation maintenant visible, l'utilisateur voit exactement où le panel sera déposé et peut éviter les déplacements accidentels

**Problème 3 : Impossible de split un panel depuis le même groupe**
- **Symptôme :** Lors du drag d'un tab vers les bords de son propre groupe (Left/Right/Top/Bottom), seule la zone Center était disponible, empêchant de créer un split
- **Cause :** `CalculateDropZones` retournait SEULEMENT la zone Center quand `isDraggingFromSameGroup=true`, bloquant complètement les zones de split
- **Solution :** 
  - Ajout de la propriété `TabHeadersBounds` à `MGDockTabGroup` pour exposer la zone des en-têtes de tabs
  - Modification de `CalculateDropZones` pour retourner TOUTES les zones (Left/Right/Top/Bottom/Center)
  - La zone Center utilise `TabHeadersBounds` comme HitRect lors du drag depuis le même groupe, permettant le réordonnancement sur les headers et les splits ailleurs
- **Fichiers modifiés :** 
  - `MGDockTabGroup.cs` - Ajout de `TabHeadersBounds` property (ligne ~75)
  - `DockDropCalculator.cs` - Logique de calcul des zones (ligne ~30-160)
- **Bénéfice :** On peut maintenant à la fois réordonner les tabs (en draggant sur les headers) ET splitter le groupe (en draggant sur les bords)

**Problème 4 : Pas de rectangle de prévisualisation lors du réordonnancement**
- **Symptôme :** Lors du réordonnancement de tabs, seul le tab survolé était mis en surbrillance, sans indication visuelle de où le tab serait inséré
- **Cause :** Le `PreviewRect` utilisait les bounds du groupe entier au lieu d'indiquer la position d'insertion précise
- **Solution :**
  - Ajout de la méthode `CalculateTabReorderPreviewRect` dans `DockDropCalculator` qui calcule un rectangle vertical (ligne de 3px) à la position d'insertion
  - Modification de `GetDropTarget` dans `MGDockHost` pour appeler cette méthode lors du réordonnancement et mettre à jour le `PreviewRect`
- **Fichiers modifiés :**
  - `DockDropCalculator.cs` - Nouvelle méthode `CalculateTabReorderPreviewRect` (ligne ~340)
  - `MGDockHost.cs` - Appel de cette méthode dans `GetDropTarget` (ligne ~820)
- **Bénéfice :** L'utilisateur voit maintenant une ligne verticale bleue indiquant exactement où le tab sera inséré entre les autres tabs

**Problème 5 : Conflit entre réordonnancement et zones de split**
- **Symptôme :** Après correction du problème 3, le réordonnancement ne fonctionnait plus - les zones de split (Left/Right/Top/Bottom) capturaient tous les drags
- **Cause :** L'algorithme calculait toutes les zones pour tous les drags, et les zones de bords avaient priorité sur la zone Center dans `GetDropTargetAtPosition`
- **Solution :** **Révision complète de l'algorithme dans `GetDropTarget`** :
  - **ÉTAPE 1 (Priorité absolue)** : Si drag depuis même groupe ET souris sur `TabHeadersBounds` → retourner directement un target Center pour réordonnancement (mode exclusif)
  - **ÉTAPE 2 (Sinon)** : Calculer toutes les zones normales (Left/Right/Top/Bottom/Center) pour permettre splits et merge
  - Suppression du paramètre `isDraggingFromSameGroup` de `CalculateDropZones` qui est redevenue une fonction simple
- **Fichiers modifiés :**
  - `MGDockHost.cs` - Logique de priorité dans `GetDropTarget` (ligne ~805)
  - `DockDropCalculator.cs` - Signature simplifiée de `CalculateDropZones` (ligne ~30)
- **Principe :** Headers = réordonnancement uniquement, Ailleurs = zones de split normales
- **Bénéfice :** Le comportement est maintenant prévisible et logique - on peut réordonner sur les headers sans que les zones de bords interfèrent

---

## 🐛 Bugs Connus à Investiguer (Anciens)

1. [x] ~~Bug : Headers des tabs avec bordures visibles~~ ✅ Corrigé (MGBorder sans bordure)
2. [x] ~~Bug : Panels fermés lors du dock~~ ✅ Corrigé (vérification descendant + dernier panel)
3. [x] ~~Bug : Panels disparaissent lors de docks successifs~~ ✅ Corrigé (détachement avant split)
4. [ ] Tester edge cases : layouts très imbriqués avec de multiples niveaux
5. [ ] Valider le comportement avec séquences complexes d'opérations

---

## 🧪 Tests à Effectuer pour 3.1 et 3.2

### Tests pour 3.1 (Bouton Close sur les tabs)

**Configuration initiale :**
- Créer un DockHost avec plusieurs panels
- S'assurer que certains panels ont `CanClose = true` et d'autres `CanClose = false`

**Tests à effectuer :**

1. **Affichage du bouton close**
   - ✅ Vérifier que le bouton "×" apparaît uniquement sur les tabs où `CanClose = true`
   - ✅ Vérifier que le bouton ne s'affiche PAS sur les tabs où `CanClose = false`
   - ✅ Vérifier le style et positionnement du bouton (centré à droite, hover fonctionne)

2. **Fermeture d'un panel**
   - ✅ Cliquer sur le bouton close d'un tab
   - ✅ Vérifier que le panel est retiré du groupe
   - ✅ Vérifier que le tab suivant devient actif automatiquement
   - ✅ Si c'était le dernier tab du groupe, vérifier que le groupe est nettoyé

3. **Fermeture avec plusieurs tabs**
   - ✅ Créer un groupe avec 5 tabs
   - ✅ Fermer le 3ème tab → vérifier que les autres restent
   - ✅ Fermer tous les tabs un par un → vérifier le cleanup final

4. **Fermeture pendant un drag**
   - ✅ Vérifier qu'on ne peut pas fermer un tab pendant qu'on le dragge

### Tests pour 3.2 (Réordonner les tabs)

**Configuration initiale :**
- Créer un DockHost avec un groupe contenant 4+ tabs
- Bien identifier l'ordre initial (ex: Tab A, Tab B, Tab C, Tab D)

**Tests à effectuer :**

1. **Réordonnancement de base**
   - ✅ Draguer Tab C et le dropper entre Tab A et Tab B
   - ✅ Vérifier le nouvel ordre : A, C, B, D
   - ✅ Vérifier que Tab C reste dans le même groupe (pas de nouveau split créé)

2. **Réordonnancement vers la fin**
   - ✅ Draguer Tab A et le dropper après Tab D
   - ✅ Vérifier le nouvel ordre : B, C, D, A
   - ✅ Vérifier que le preview montre la bonne position pendant le drag

3. **Réordonnancement vers le début**
   - ✅ Draguer Tab D et le dropper avant Tab A
   - ✅ Vérifier le nouvel ordre : D, A, B, C

4. **Drag vers le même emplacement**
   - ✅ Draguer Tab B et le dropper sur sa position actuelle
   - ✅ Vérifier que l'ordre ne change pas et qu'il n'y a pas de bug

5. **Drag entre deux tabs**
   - ✅ Draguer un tab et passer lentement la souris entre chaque tab
   - ✅ Vérifier que le calcul d'index fonctionne correctement selon la position X
   - ✅ Vérifier le visual feedback (preview, opacité, etc.)

6. **Réordonnancement avec tab actif**
   - ✅ Sélectionner Tab B (actif)
   - ✅ Draguer Tab B vers une nouvelle position
   - ✅ Vérifier que Tab B reste actif après le drop

7. **Réordonnancement avec tab inactif**
   - ✅ Tab B est actif
   - ✅ Draguer Tab C (inactif) vers une nouvelle position
   - ✅ Vérifier que Tab B reste actif et que seul Tab C a changé de position

8. **Drag vers un autre groupe (pas de reorder)**
   - ✅ Créer 2 groupes côte à côte
   - ✅ Draguer un tab du Groupe A vers le center du Groupe B
   - ✅ Vérifier que le tab est DÉPLACÉ vers Groupe B (pas réordonné dans A)
   - ✅ Vérifier que c'est bien une opération MoveTab, pas ReorderTab

9. **Annulation du drag (ESC)**
   - ✅ Commencer à draguer un tab
   - ✅ Appuyer sur ESC
   - ✅ Vérifier que l'ordre reste inchangé et que le tab revient à sa position

10. **Seuil de drag (threshold)**
    - ✅ Cliquer sur un tab et bouger la souris de 2-3 pixels
    - ✅ Vérifier qu'aucun drag ne démarre (seuil pas atteint)
    - ✅ Bouger de 6+ pixels → vérifier que le drag démarre

### Tests Edge Cases

1. **Groupe avec 2 tabs seulement**
   - ✅ Réordonner Tab A et Tab B → vérifier l'échange

2. **Groupe avec 1 seul tab**
   - ✅ Draguer l'unique tab → impossible de réordonner dans le même groupe
   - ✅ Vérifier que le drag vers un autre groupe fonctionne normalement

3. **Panels avec CanClose = false**
   - ✅ Réordonner des tabs qui ne peuvent pas être fermés
   - ✅ Vérifier que le réordonnancement fonctionne normalement

4. **Réordonnancement rapide successif**
   - ✅ Réordonner Tab A → Tab C → Tab B en succession rapide
   - ✅ Vérifier que l'état reste cohérent

---

  - `Controls/MGDockSplitContainer.cs` - Container avec splitter
  - `DockLayout/DockOperation.cs` - Opérations atomiques
  - `DockLayout/DockLayoutModel.cs` - Modèle de données

- **Pattern utilisé** : MVVM-like avec modèle (DockLayoutModel) et vue (MGDockHost)

- **À respecter** :
  - Toujours maintenir les références Parent cohérentes
  - Auto-merge des splits à un seul enfant
  - Jamais de TabGroup vide persistant (sauf root)
