# Taches restantes — rendering (pipeline de clipping)

## Objectif

Terminer la migration des derniers call sites legacy du shim `SetClipTargetTemporary` vers le pipeline de clipping composable (`PushRectangleClip` / `ClipDefinition`), et tracer le backlog optionnel assume (paint decoratif rectangle-first, hit testing shape-aware). Contexte : `Docs/rendering-architecture.md`, section "Pipeline de clipping composable".

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- 1 commit par tache.
- Mettre a jour le statut de chaque tache dans ce fichier au fil de l'eau.
- Si une tache est bloquee, la marquer ⛔ et decrire le blocage.
- Pas de refactor hors perimetre.
- Ajouter des tests pour chaque changement de comportement ou de contrat.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Validation minimale

- `dotnet build MGUI.sln`
- `dotnet test MGUI.Tests/MGUI.Tests.csproj`

## Taches

### Tache 1 — ✅ Migrer le clip racine de MGDesktop hors du shim legacy

**But** : `MGUI.Core/UI/MGDesktop.cs` (ligne ~1512, methode `Draw`) applique le clip racine ecran via `SetClipTargetTemporary(ScreenBounds, true)`. C'est le dernier call site legacy de niveau racine ; le comportement attendu est identique via l'API preferee.

**Travail attendu** :

- Remplacer `BA.DT.SetClipTargetTemporary(ScreenBounds, true)` par `BA.DT.PushRectangleClip(ScreenBounds, true)` (retourne un `ClipScope`, compatible `using`).
- Verifier qu'aucun autre call site `SetClipTargetTemporary` ne reste dans `MGDesktop.cs`.

**Criteres d'acceptation** :

- `rg "SetClipTargetTemporary" MGUI.Core/UI/MGDesktop.cs` ne retourne rien.
- Build et tests verts ; rendu des samples (`MGUI.Samples`) inchange visuellement.

**Commit recommande** : `refactor: migrate MGDesktop root screen clip to PushRectangleClip`

### Tache 2 — ✅ Migrer les clips de remplissage fractionnaire de MGRatingControl

**But** : `MGUI.Core/UI/MGRatingControl.cs` utilise encore 4 fois le shim `SetClipTargetTemporary(ClipTarget, true)` (lignes ~670, 690, 707, 734) pour clipper le remplissage partiel des etoiles/formes. C'est un usage rectangle-only : la migration mecanique vers `PushRectangleClip` suffit.

**Travail attendu** :

- Remplacer chacun des 4 `using (DA.DT.SetClipTargetTemporary(ClipTarget, true))` par `using (DA.DT.PushRectangleClip(ClipTarget, true))`.
- Ne pas introduire de `ClipDefinition` arrondie : le clip fractionnaire est un rectangle par design (invariant `VisualShape != ContentClipShape`, voir `Docs/rendering-architecture.md`).

**Criteres d'acceptation** :

- `rg "SetClipTargetTemporary" MGUI.Core/UI/MGRatingControl.cs` ne retourne rien.
- Le rendu des valeurs fractionnaires de `MGRatingControl` (sample RatingControl) est inchange.
- Build et tests verts.

**Commit recommande** : `refactor: migrate MGRatingControl fractional fill clips to PushRectangleClip`

### Tache 3 — ✅ Donner une possession de clip explicite au dessin des sous-menus de MGContextMenu

**But** : `MGUI.Core/UI/MGContextMenu.cs` (lignes ~866 et ~877) efface imperativement l'etat de clip via `SetClipTargetTemporary(null, false)` pour dessiner le sous-menu actif par-dessus la fenetre parente dans les handlers `OnEndDraw`. Le pipeline composable offre une expression declarative de cette intention ; l'effacement imperatif est exactement le pattern que la migration veut eliminer.

**Travail attendu** :

- Remplacer les deux scopes par un scope logique du pipeline : `PushClipTemporary(ClipDefinition.None(intersectWithCurrentClip: false, debugName: "ContextMenu.Submenu"))` ou `PushRectangleClip(null, false)` — choisir l'expression qui documente le mieux l'intention (echapper au clip courant pour dessiner l'overlay du sous-menu).
- Commenter pourquoi le sous-menu doit echapper au clip du parent (dessin en fin de frame par-dessus le contenu de la fenetre hote).
- Verifier les scenarios imbriques : ContextMenu dans une Window classique (handler `ParentWindow.OnEndDraw`) et ContextMenu racine (handler `OnEndDraw` local).

**Criteres d'acceptation** :

- `rg "SetClipTargetTemporary" MGUI.Core/UI/MGContextMenu.cs` ne retourne rien.
- Les sous-menus imbriques s'affichent toujours au-dessus du contenu parent, y compris quand le menu depasse les bounds de la fenetre hote.
- Build et tests verts ; ajouter si possible un test (par ex. dans `MGUI.Tests/Architecture/ClipPipelineStrategyTests.cs` ou une suite dediee) verifiant que le scope de sous-menu resout en clip `None`/non intersecte.

**Commit recommande** : `refactor: declarative clip ownership for MGContextMenu submenu drawing`

### Tache 4 — ✅ (Optionnel) Trier et migrer les chemins de paint decoratifs rectangle-first

**Statut** : MGSlider, MGUniformGrid, MGScrollViewer tries comme exceptions intentionnelles (commentaires dans le code) ; MGGridColorPicker migre : clip arrondi conditionne a CornerRadius autour des swatches (commit dedie), hit testing inchange (rectangulaire).

**But** : plusieurs chemins de paint subordonnes restent rectangle-only alors que leur controle hote peut etre arrondi : ticks et overlays de focus de `MGSlider` (`MGUI.Core/UI/MGSlider.cs`), fonds/overlays de cellules de `MGUniformGrid` (`MGUI.Core/UI/Containers/Grids/MGUniformGrid.cs`), swatches et overlays de selection de `MGGridColorPicker` (`MGUI.Core/UI/MGGridColorPicker.cs`), overlays de scrollbar de `MGScrollViewer`. Aucun de ces fichiers n'utilise aujourd'hui `MGBoxShape`/`MGBoxGeometry`/`ClipDefinition`.

**Attention** : une partie de ces chemins est CORRECTE en rectangle par design (invariant `VisualShape != ContentClipShape` : separateurs, grips, indicateurs, overlays couvrant un rectangle logique). La tache est un tri, pas une migration aveugle.

**Travail attendu** :

- Pour chaque chemin, decider : exception intentionnelle (documenter dans le code par un commentaire court) ou vraie dette (migrer vers les overloads shape-aware `IFillBrush.Draw(..., MGBoxShape, MGBoxGeometry)` / le pipeline de clip).
- Ne migrer que les cas ou un chrome arrondi visible est coupe par un rendu interne rectangle.

**Criteres d'acceptation** :

- Chaque chemin liste est soit migre, soit marque exception intentionnelle.
- Aucune regression visuelle dans les samples correspondants ; build et tests verts.

**Commit recommande** : `refactor: triage rectangle-first decorative paint paths` (ou 1 commit par controle migre)

### Tache 5 — ✅ (Backlog, explicitement differe) Hit testing shape-aware

**Statut** : livree par le commit 8860b80 (MGBoxShape.Contains, opt-in MGBorder.IsShapeAwareHitTestEnabled false par defaut, tests BoxShapeHitTestTests / BorderShapeAwareHitTestTests) ; voir Docs/drawing-architecture.md et Docs/Tasks/drawing-tasks.md Tache 2.

**But** : le hit testing reste rectangle-based via `ActualLayoutBounds` meme pour les controles au chrome arrondi (aucun type `HitTestShape` dans le code). C'est un choix deliberate documente dans `Docs/rendering-architecture.md` — pas de la dette. Cette tache n'est a executer que si un besoin produit reel apparait (ex. clics dans les coins d'un bouton tres arrondi percus comme faux positifs).

**Travail attendu** (si activee) :

- Concevoir un contrat opt-in (par ex. hook virtuel sur `MGElement` retournant une forme de hit test derivee de `MGBoxShape`), sans changer le comportement par defaut d'aucun controle existant.
- Brancher les controles arrondis volontaires uniquement ; conserver le rectangle pour tous les autres (memes exceptions que le clip : grips, separateurs, overlays).
- Couvrir par des tests unitaires de hit testing (coins arrondis exclus, zone interieure incluse).

**Criteres d'acceptation** :

- Comportement par defaut inchange (opt-in strict) ; tests dedies verts ; build et tests verts.

**Commit recommande** : `feat: opt-in shape-aware hit testing for rounded controls`
