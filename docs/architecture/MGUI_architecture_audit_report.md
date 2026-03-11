# MGUI Architecture Audit Report

## 1. Resume executif

Verdict: **Partiellement conforme**

MGUI presente deja plusieurs briques d'un vrai runtime UI moderne: arbre d'elements, layout distinct du cycle d'input, styles, themes, data binding, parsing XAML, abstraction partielle du host via `IRenderHost`, et une abstraction de moteur de texte via `ITextEngine`.

En revanche, l'architecture actuelle ne separe pas encore clairement les notions de **core UI**, **view runtime**, **surface de rendu** et **integration moteur**. Le point central `MGDesktop` concentre a la fois le root runtime, la politique de focus/navigation, le routage input, la gestion des overlays/context menus/tooltips, les ressources et la composition finale. Le rendu reste fortement couple a MonoGame (`GraphicsDevice`, `SpriteBatch`, `RenderTarget2D`) jusque dans les couches haut niveau, et le support multi-view / multi-surface n'existe pas comme abstraction metier explicite.

Conclusion courte:

- Le framework est solide pour un UI runtime MonoGame mono-host et mono-surface.
- Il est **partiellement** aligne avec une architecture moderne type Noesis-like.
- Il faudra introduire des abstractions de `UIView`, `UISurface`, `IRawInputSource` et `IUIRenderContext` pour atteindre la cible sans casser les controles.

## 2. Architecture actuelle observee

### 2.1 Projets et couches observees

| Couche observee | Projet | Fichiers principaux | Observation |
|---|---|---|---|
| Runtime UI, layout, controles, XAML, themes | `MGUI.Core` | `UI/MGDesktop.cs`, `UI/MGElement.cs`, `UI/MGWindow.cs`, `UI/XAML/XAMLParser.cs`, `UI/MGResources.cs`, `UI/MGTheme.cs` | Couche dominante du runtime actuel |
| Rendu, input brut, font manager, helpers | `MGUI.Shared` | `Rendering/MainRenderer.cs`, `Rendering/DrawTransaction.cs`, `Input/InputTracker.cs`, `Text/FontManager.cs` | Couche partagee, mais encore tres MonoGame-centree |
| Backend texte alternatif | `MGUI.FontStashSharp` | `FontStashSharpTextEngine.cs` | Point positif: backend texte interchangeable |
| Hosting et exemples d'integration | `MGUI.Samples` | `Game1.cs`, `Compendium.xaml.cs` | Montre l'integration directe dans un `Game` MonoGame |
| Validation de comportements | `MGUI.Tests` | `Modal/ModalBlockingTests.cs`, `KeyboardNav/KeyboardNavTests.cs` | Tests logiques sur focus/navigation/modal |

### 2.2 Classes centrales du runtime

| Role | Classe | Responsabilite actuelle |
|---|---|---|
| Root runtime par host | `MGUI.Core/UI/MGDesktop.cs` | Gere fenetres, overlays, focus, context menus, tooltips, input routing, draw/update globaux |
| Base de l'arbre visuel/logique | `MGUI.Core/UI/MGElement.cs` | Layout, update, draw, arbre parent/enfant, styles, visual state, input eligibility |
| Root top-level d'un arbre UI | `MGUI.Core/UI/MGWindow.cs` | Position, taille, nested windows, modal window, scaling, border/title bar |
| Sous-elements techniques | `MGUI.Core/UI/MGComponent.cs` | Parties internes de controles, ordonnancement update/draw |
| Host de rendu/runtime | `MGUI.Shared/Rendering/MainRenderer.cs` | Possede `GraphicsDevice`, `SpriteBatch`, `ContentManager`, `InputTracker`, `FontManager`, `ITextEngine` |
| Adaptateur MonoGame -> host MGUI | `MGUI.Shared/Rendering/MainRenderer.cs` (`GameRenderHost<TObservableGame>`) | Expose bounds, `GraphicsDevice`, input brut, events update |
| Contexte de draw concret | `MGUI.Shared/Rendering/DrawTransaction.cs` | Emission immediate de sprites, primitives, clip, render target |
| Input brut agrege | `MGUI.Shared/Input/InputTracker.cs` | Agrege souris, clavier, gamepad a partir des etats MonoGame |
| Parsing XAML | `MGUI.Core/UI/XAML/XAMLParser.cs` | Parse du XAML vers objets XAML puis conversion runtime |

### 2.3 Cycle runtime observe

1. `Game1` cree `MainRenderer` avec `new GameRenderHost<Game1>(this)` dans `MGUI.Samples/Game1.cs`.
2. `MainRenderer` ecoute `PreviewUpdate` / `EndUpdate`, capture `MouseState` / `KeyboardState`, puis met a jour `InputTracker`.
3. `MGDesktop.Update()` reconcilie mode d'input, focus, overlays, context menus, puis update les `MGWindow`.
4. Chaque `MGWindow` et chaque `MGElement` appelle ses propres etapes de measurement, update, draw et dispatch input.
5. `MGDesktop.Draw()` dessine les fenetres dans le backbuffer courant via `DrawTransaction` et applique le clipping global.

### 2.4 Structures deja presentes mais incomplites

- `IRenderHost` fournit une abstraction minimale du host, mais melange viewport, `GraphicsDevice` et input brut.
- `View` dans `MGUI.Shared/Rendering/View.cs` ressemble a une ancienne abstraction de vue, mais elle n'est pas la base du runtime actuel.
- `DrawTransaction` sait temporairement dessiner vers un `RenderTarget2D`, mais cette capacite n'est pas exposee comme concept de surface UI de haut niveau.
- `MGWindow` contient un code de render target desactive sous `#if NEVER`, ce qui indique un besoin ancien non termine.

## 3. Cartographie des responsabilites

### 3.1 Core UI

| Responsabilite | Fichiers / classes | Etat |
|---|---|---|
| Arbre visuel | `MGElement`, conteneurs `MGContentHost`, `MGStackPanel`, `MGGrid`, `MGDockPanel` | Present |
| Arbre logique / relations de possession | `MGElement.Parent`, `ManagedParent`, `ComponentParent`, `MGWindow.NestedWindows` | Present, mais melange visuel/runtime/windowing |
| Layout / measurement | `MGElement.UpdateMeasurement`, `MeasureSelfOverride` dans les controles/conteneurs | Present |
| Invalidation layout | `LayoutChanged(...)` et recalculs au tick suivant | Present |
| Styles / themes | `MGResources`, `MGTheme`, `XAML/Style.cs` | Present |
| Data binding | `UI/Data Binding/*` | Present |
| Focus / navigation | `MGDesktop`, `MGElement.Focus(...)`, enums de navigation | Present mais centralise dans `MGDesktop` |
| Animations / transitions | Pas de systeme general dedie observe; seulement transformations/etats ponctuels | Partiel |
| Commandes | `MGResources.Commands`, commandes texte inline, delegates de boutons | Present |

### 3.2 Rendu

| Responsabilite | Fichiers / classes | Etat |
|---|---|---|
| Host de rendu | `IRenderHost`, `GameRenderHost<TObservableGame>` | Present, MonoGame-specifique |
| Ressources de draw | `MainRenderer` | Present |
| Contexte de draw | `DrawTransaction`, `DrawSettings` | Present |
| Emission des primitives UI | `MGElement.Draw`, brushes `IFillBrush` / `IBorderBrush`, controles specifiques | Present, emission immediate depuis les couches UI |
| Clip/scissor | `DrawTransaction.SetClipTarget*` | Present |
| Render target | `DrawTransaction.SetRenderTarget*` | Present bas niveau seulement |

### 3.3 Input

| Responsabilite | Fichiers / classes | Etat |
|---|---|---|
| Capture input brut | `GameRenderHost.GetMouseState`, `GetKeyboardState`, `InputTracker.Update` | Present |
| Dispatch souris | `Input/Mouse/*`, `MGElement._MouseHandler`, `MGDesktop.HighPriorityMouseHandler` | Present |
| Dispatch clavier | `Input/Keyboard/*`, `MGElement._KeyboardHandler`, `MGDesktop.HighPriorityKeyboardHandler` | Present |
| Gamepad navigation | `GamePadTracker`, mapping dans `MGDesktop` | Present mais rattache a la navigation desktop |
| Focus | `MGDesktop.FocusedKeyboardHandler`, scopes et historique | Present |
| Modal | `MGWindow.ModalWindow`, `MGOverlayHost.IsModal` | Present, mais mode simple |
| Mouse capture | Gestion partielle via handlers et drag/drop; pas d'abstraction de capture globale explicite type pointer capture | Partiel |

### 3.4 Ressources et XAML

| Responsabilite | Fichiers / classes | Etat |
|---|---|---|
| Parsing XAML | `XAMLParser`, classes XAML dans `UI/XAML/*` | Present |
| Conversion XAML -> runtime | `ToElement(...)`, `ApplySettings(...)`, `ProcessBindings(...)` | Present |
| Textures et themes | `MGResources`, `MGTheme`, `MGTextureData` | Present |
| Polices | `FontManager`, `ITextEngine`, `SpriteFontTextEngine`, `FontStashSharpTextEngine` | Present |
| Cache ressources | Dictionnaires dans `MGResources` et caches internes de `MainRenderer` / `FontManager` | Present mais distribue |
| Asset loading | `MainRenderer.Content`, appels directs `Content.Load<T>()`, chargement dans `MGDesktop` | Present mais couple au runtime |

## 4. Ecarts par rapport a l'architecture cible

### 4.1 Core UI independant du moteur hote

**Constat**

Le core UI n'est pas independant du moteur hote au sens demande. Les classes haut niveau manipulent directement des types MonoGame et MonoGame.Extended:

- `MGElement` transporte `Rectangle`, `Point`, `Matrix`, `GraphicsDevice` via `ElementDrawArgs` et `DrawTransaction`.
- Les brushes (`IFillBrush`, `IBorderBrush`) dessinent directement via `DrawTransaction`.
- `MGResources` stocke directement des `Texture2D` dans `MGTextureData`.
- `MGWindow`, `MGDesktop` et de nombreux controles utilisent des types `Microsoft.Xna.Framework` / `MonoGame.Extended` dans leur API interne.

**Evaluation**

- Layout et etat visuel sont logiquement separes du host, mais pas techniquement portables.
- Un changement de backend de rendu exigerait des adaptations larges dans `MGUI.Core`, pas seulement dans `MGUI.Shared`.

**Classes concernees**

- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/MGWindow.cs`
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/Brushes/**/*`
- `MGUI.Shared/Rendering/DrawTransaction.cs`

**Conclusion**

Non conforme sur ce point.

### 4.2 Couche d'integration moteur distincte

**Constat**

Une couche d'integration existe partiellement dans `MGUI.Shared`, surtout via `MainRenderer`, `InputTracker`, `FontManager` et `GameRenderHost<TObservableGame>`. C'est un bon debut.

Mais cette couche n'est pas assez isolee:

- `IRenderHost` expose en meme temps viewport, `GraphicsDevice`, services et input brut.
- `MainRenderer` cree directement `ContentManager`, `SpriteBatch`, `PrimitiveBatch` et charge des assets.
- `MGDesktop` charge lui-meme des icones d'exemple depuis le content pipeline dans son constructeur.

**Classes concernees**

- `MGUI.Shared/Rendering/MainRenderer.cs`
- `MGUI.Shared/Input/InputTracker.cs`
- `MGUI.Core/UI/MGDesktop.cs`

**Conclusion**

Partiellement conforme.

### 4.3 Couche de presentation / hosting

**Constat**

La couche de presentation au sens `UIView` explicite n'existe pas. Le runtime se structure aujourd'hui comme:

- `MainRenderer` = host/render services
- `MGDesktop` = root runtime + policies globales
- `MGWindow` = racines top-level d'arbres UI

Il n'y a pas de classe distincte qui represente une **vue UI runtime attachable a une surface** avec son propre cycle de vie, son propre focus scope racine, son propre routage input et ses propres dimensions logiques.

Le fichier `MGUI.Shared/Rendering/View.cs` ne remplit pas ce role dans l'architecture courante.

**Conclusion**

Non conforme sur ce point.

### 4.4 Separation Screen / View / Surface

**Constat**

Les trois notions sont actuellement melangees:

- **Screen**: aucun concept explicite dans le runtime. Les ecrans sont gerees au niveau application/sample (`Compendium`, creation de fenetres, logique jeu).
- **View**: `MGDesktop` joue implicitement ce role, mais avec des responsabilites beaucoup trop larges.
- **Surface**: non explicite. La surface effective est derivee de `Renderer.Host.GetBounds()` et du backbuffer courant. `DrawTransaction` sait changer de `RenderTarget2D`, mais le runtime UI ne modelise pas la surface comme objet.

**Conclusion**

Non conforme sur ce point.

### 4.5 Support multi-view / multi-surface

**Constat**

Capacites actuelles confirmees:

- Plusieurs `MGWindow` peuvent coexister dans un `MGDesktop`.
- Plusieurs arbres UI peuvent vivre en parallele **a l'interieur du meme desktop** via `Windows`, `NestedWindows`, `OverlayHost`, `ModalWindow`.
- Plusieurs `MGDesktop` pourraient etre instancies par le code application, mais cette capacite n'est ni modelee ni routable proprement par l'architecture.

Limites actuelles:

- L'input est collecte globalement depuis `Mouse.GetState()` / `Keyboard.GetState()` dans `GameRenderHost`.
- `MGDesktop` suppose un seul espace de bounds via `ValidScreenBounds => Renderer.Host.GetBounds()`.
- Il n'existe aucun objet `UISurface` ou `UIView` pour attacher une vue a un backbuffer, un viewport secondaire, une render texture ou un panneau editeur.
- `MGWindow` contient un support render target abandonne sous `#if NEVER`, signe que le scenario a ete entrevu mais non finalise.

**Conclusion**

Partiellement conforme pour multi-root logique, non conforme pour multi-view / multi-surface en tant que capacite de premier niveau.

### 4.6 Pipeline de composition

**Constat**

Le pipeline est simple et efficace pour un cas mono-surface:

- `MGDesktop.Draw()` cree ou recoit un `DrawTransaction`.
- Il dessine les `Windows`, puis l'overlay desktop, puis context menu / tooltip.
- Le clipping global se fait via le scissor rectangle.

Mais ce pipeline reste fortement couple au rendu principal:

- Pas de graphe de composition distinct.
- Pas de vue detachable a composer plus tard.
- Pas de phase officielle de rendu vers surface intermediaire avant composition finale.

**Classes concernees**

- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`

**Conclusion**

Partiellement conforme pour le runtime jeu simple, non conforme pour un moteur moderne multi-surface.

### 4.7 Input routing

**Constat**

Le routage input est fonctionnel et relativement avance:

- mode pointer / navigation / text entry
- focus keyboard queue et history
- overlays modaux
- context menu et tooltips
- integration gamepad/navigation

Mais l'input est route **au niveau du desktop**, pas au niveau d'une vue ciblee. Il n'y a pas de notion propre d'input source ou de dispatch view-targeted.

**Conclusion**

Partiellement conforme.

### 4.8 Assets / ressources UI

**Constat**

Le systeme est utile et pratique, mais pas encore separe en definition / chargement / cache / usage:

- `XAMLParser` parse des chaines et construit directement des objets runtime.
- `MGResources` melange definitions nommees et acces runtime.
- `MainRenderer` et `MGDesktop` chargent directement des assets depuis le content pipeline.

Le systeme n'a pas encore une couche d'asset UI independante du runtime live.

**Conclusion**

Partiellement conforme.

### 4.9 Navigation et ecrans

**Constat**

Il n'existe pas de systeme explicite de navigation d'ecrans dans le runtime. Les notions de main menu, HUD, inventory, pause screen, etc. sont laissees a l'application hote.

Cela n'est pas un probleme en soi pour une bibliotheque UI, mais cela confirme que les concepts `Screen` et `View` ne sont pas encore separes formellement.

**Conclusion**

Non conforme a la separation demandee, mais avec une dette contenue car le runtime n'impose pas non plus de systeme d'ecran rigide.

### 4.10 Extensibilite editeur

**Constat**

Point positif important: `MGXAMLDesigner` prouve qu'un usage tooling/runtime preview est deja envisage.

Limites:

- pas d'inspection structuree de l'arbre visuel
- pas d'API de snapshots ou diagnostics du layout
- pas de hot reload XAML host-independent
- le designer est lui-meme un controle runtime, pas une couche tooling abstraite

**Conclusion**

Partiellement conforme avec une bonne base experimentale.

## 5. Risques techniques

### Risque 1 - Couplage transverse a MonoGame

Impact:

- rend difficile un backend alternatif
- etend le perimetre de refactor jusque dans `MGUI.Core`

Fichiers principaux:

- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`
- `MGUI.Core/UI/Brushes/**/*`

### Risque 2 - `MGDesktop` comme god object runtime

Impact:

- complique toute introduction de `UIView`
- rend la maintenance du focus/input/navigation plus fragile
- empeche une vraie isolation de plusieurs vues actives

Fichiers principaux:

- `MGUI.Core/UI/MGDesktop.cs`

### Risque 3 - Multi-surface non modele

Impact:

- split-screen, render texture monde, panneaux editeur et preview out-of-band demanderont des hacks locaux

Fichiers principaux:

- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/MGWindow.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`

### Risque 4 - Chargement d'assets dans le runtime

Impact:

- testabilite reduite
- bootstrap implicite difficile a controler
- pollution entre runtime framework et contenu de sample

Fichiers principaux:

- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Shared/Rendering/MainRenderer.cs`

### Risque 5 - Absence de frontiere claire entre runtime et tooling

Impact:

- l'evolution vers un editeur plus riche demandera des extractions structurelles tardives

Fichiers principaux:

- `MGUI.Core/UI/MGXAMLDesigner.cs`
- `MGUI.Core/UI/XAML/XAMLParser.cs`

## 6. Points forts actuels

- Le systeme de layout est deja riche, avec measurement/arrangement et invalidation distribuee.
- Les controles sont relativement coherents autour de `MGElement` et `MGComponent`.
- Le data binding est deja une vraie capacite du runtime, pas un ajout cosmetique.
- La separation du moteur de texte via `ITextEngine` est un precedent architectural reussi.
- `IRenderHost` constitue un point d'entree utile pour une future separation host / surface / input.
- Les overlays, modals, focus scopes et navigation clavier/gamepad montrent une maturite runtime reelle.
- Les classes XAML separent deja la definition declarative de l'instance runtime, meme si le chargement reste trop lie a l'execution.
- Le support docking et nested windows prouve que MGUI sait deja gerer des scenarios plus riches qu'un simple HUD.

## 7. Priorites de refactor

### Bloquant

1. Introduire une vraie abstraction de `UIView` distincte de `MGDesktop`.
2. Introduire une abstraction explicite de `UISurface` au lieu de supposer le backbuffer courant.
3. Sortir l'input brut de `IRenderHost` vers une abstraction dediee et routable.
4. Reduire la dependance directe de `MGUI.Core` a `DrawTransaction` et aux types MonoGame de haut niveau.

### Important

1. Extraire focus, navigation, modalite et overlays hors du role de root monolithique de `MGDesktop`.
2. Deplacer le chargement des ressources par defaut hors du constructeur de `MGDesktop`.
3. Clarifier la chaine assets UI: definition, chargement, cache, consommation.
4. Introduire un pipeline de composition qui sache rendre une vue vers une surface cible.

### Amelioration souhaitable

1. Formaliser une couche tooling/editor autour du XAML designer.
2. Exposer des diagnostics d'arbre visuel et de layout.
3. Rendre le parsing XAML reutilisable depuis string, fichier, stream et hot reload.

Les taches atomiques recommandees sont listees dans `docs/architecture/MGUI_architecture_refactor_tasks.md`.

## 8. Verdict final

### Partiellement conforme

MGUI est deja un runtime UI credible pour MonoGame, avec des capacites avancees et une base technique saine. En revanche, il n'est pas encore architecte comme un runtime UI moderne pleinement separe entre core, integration moteur, presentation/hosting et surfaces de rendu.

Le framework peut evoluer vers cette cible sans refactor destructif majeur, mais cela demandera d'introduire quelques frontieres d'architecture explicites qui n'existent pas encore aujourd'hui:

- `UIView`
- `UISurface`
- source d'input abstraite et ciblable
- contexte de rendu UI moins dependant de MonoGame dans les couches haut niveau
- separation plus nette entre runtime live et outillage