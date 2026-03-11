# MGUI Architecture Refactor Tasks

Les taches ci-dessous sont volontairement petites et atomiques. Elles ne proposent pas de refactor global flou: chaque tache cible un point d'architecture concret observe pendant l'audit.

### MGUI-ARCH-001 - Introduire une abstraction `IRawInputSource`
**Contexte**  
`IRenderHost` melange actuellement viewport, `GraphicsDevice`, services et input brut. Cela empeche de separer correctement hosting, surfaces et routage input.

**Objectif**  
Isoler la collecte d'input brut dans une interface dediee.

**Fichiers / classes concernes**  
- `MGUI.Shared/Rendering/MainRenderer.cs`
- `MGUI.Shared/Input/InputTracker.cs`
- `MGUI.Shared/Rendering/MainRenderer.cs` (`IRenderHost`, `GameRenderHost<TObservableGame>`)
- Nouveau fichier propose: `MGUI.Shared/Input/IRawInputSource.cs`

**Modification attendue**  
Creer `IRawInputSource` avec les lectures de souris, clavier et, si necessaire, gamepad.

**Criteres d'acceptation**  
- `IRenderHost` n'expose plus directement `GetMouseState()` et `GetKeyboardState()`
- `MainRenderer` peut recevoir une source d'input brute distincte du host de rendu

**Dependances eventuelles**  
Aucune

### MGUI-ARCH-002 - Faire de `GameRenderHost<TObservableGame>` un adaptateur de host uniquement
**Contexte**  
`GameRenderHost<TObservableGame>` est aujourd'hui l'adaptateur a la fois de viewport, rendu, services et input.

**Objectif**  
Reduire sa responsabilite au hosting / viewport / services moteur.

**Fichiers / classes concernes**  
- `MGUI.Shared/Rendering/MainRenderer.cs`

**Modification attendue**  
Retirer la partie input brut de `GameRenderHost<TObservableGame>` ou la deleguer a une instance `IRawInputSource` MonoGame.

**Criteres d'acceptation**  
- Le type reste utilisable pour un `Game` MonoGame
- La lecture des etats input ne depend plus de l'interface de host de rendu

**Dependances eventuelles**  
- `MGUI-ARCH-001`

### MGUI-ARCH-003 - Introduire une abstraction `IUISurface`
**Contexte**  
Le runtime suppose implicitement que la surface active est `Renderer.Host.GetBounds()` plus le render target courant.

**Objectif**  
Representer explicitement la cible de rendu UI.

**Fichiers / classes concernes**  
- `MGUI.Shared/Rendering/MainRenderer.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`
- `MGUI.Core/UI/MGDesktop.cs`
- Nouveau fichier propose: `MGUI.Shared/Rendering/IUISurface.cs`

**Modification attendue**  
Creer une interface qui expose au minimum bounds logiques et cible de rendu associee.

**Criteres d'acceptation**  
- Une surface backbuffer peut etre representee sans knowledge implicite dans `MGDesktop`
- L'API laisse place a une surface viewport ou render target

**Dependances eventuelles**  
Aucune

### MGUI-ARCH-004 - Ajouter une implementation `BackBufferSurface`
**Contexte**  
Une abstraction de surface n'est utile que si le comportement actuel peut etre preserve sans rupture.

**Objectif**  
Encapsuler le comportement actuel de backbuffer principal dans une premiere implementation concrete.

**Fichiers / classes concernes**  
- `MGUI.Shared/Rendering/MainRenderer.cs`
- Nouveau fichier propose: `MGUI.Shared/Rendering/BackBufferSurface.cs`

**Modification attendue**  
Creer une implementation qui derive ses bounds du host actuel.

**Criteres d'acceptation**  
- Le comportement de draw actuel reste identique par defaut
- Les bounds utilises par le desktop peuvent provenir d'une surface au lieu du host brut

**Dependances eventuelles**  
- `MGUI-ARCH-003`

### MGUI-ARCH-005 - Introduire une abstraction runtime `UIView`
**Contexte**  
Il n'existe pas de classe distincte representant une vue UI runtime attachable a une surface.

**Objectif**  
Creer une abstraction qui porte un root UI, un contexte de focus et une surface cible.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Shared/Rendering/View.cs`
- Nouveau fichier propose: `MGUI.Core/UI/UIView.cs`

**Modification attendue**  
Introduire `UIView` comme nouvelle unite de presentation, sans supprimer immediatement `MGDesktop`.

**Criteres d'acceptation**  
- Une vue peut etre instanciee sans etre forcee d'etre le seul root UI du renderer
- Une vue possede une surface logique propre
- Une vue expose `Update()` et `Draw()`

**Dependances eventuelles**  
- `MGUI-ARCH-003`
- `MGUI-ARCH-004`

### MGUI-ARCH-006 - Extraire l'etat runtime global de `MGDesktop`
**Contexte**  
`MGDesktop` cumule actuellement root UI, focus, overlays, context menus, tooltips, routing input et ressources.

**Objectif**  
Reduire `MGDesktop` a un role de root d'arbre et de compatibilite, au profit d'un etat de vue explicite.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGDesktop.cs`
- Nouveau fichier propose: `MGUI.Core/UI/UIViewState.cs`

**Modification attendue**  
Extraire dans un etat dedie les donnees suivantes: focus courant, historique de focus, context menu actif, tooltip actif, overlay desktop.

**Criteres d'acceptation**  
- `MGDesktop` ne porte plus directement tout l'etat runtime global
- Les donnees extraites restent disponibles pour le code existant via facades ou delegations

**Dependances eventuelles**  
- `MGUI-ARCH-005`

### MGUI-ARCH-007 - Permettre a `MainRenderer` de gerer plusieurs vues
**Contexte**  
Le cycle actuel est pense pour un seul `MGDesktop` pilote manuellement par l'application.

**Objectif**  
Permettre au renderer de connaitre plusieurs vues actives dans une meme frame.

**Fichiers / classes concernes**  
- `MGUI.Shared/Rendering/MainRenderer.cs`
- `MGUI.Core/UI/UIView.cs` (nouveau)

**Modification attendue**  
Ajouter un registre de vues avec ordre d'update/draw.

**Criteres d'acceptation**  
- Plusieurs vues peuvent etre enregistrees
- L'ordre de mise a jour et de dessin est deterministe

**Dependances eventuelles**  
- `MGUI-ARCH-005`

### MGUI-ARCH-008 - Introduire un routage input cible par vue
**Contexte**  
L'input est actuellement traite au niveau du desktop unique.

**Objectif**  
Pouvoir diriger l'input vers une vue cible en fonction d'une strategie explicite.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Shared/Input/InputTracker.cs`
- Nouveau fichier propose: `MGUI.Core/UI/UIViewInputRouter.cs`

**Modification attendue**  
Ajouter une couche qui choisit la vue recevant l'input avant le dispatch interne aux elements.

**Criteres d'acceptation**  
- Une vue peut etre designee comme cible d'input
- Le routage n'exige plus un singleton implicite de desktop

**Dependances eventuelles**  
- `MGUI-ARCH-001`
- `MGUI-ARCH-005`
- `MGUI-ARCH-007`

### MGUI-ARCH-009 - Faire porter les bounds logiques par la vue ou la surface
**Contexte**  
`MGDesktop.ValidScreenBounds` depend directement de `Renderer.Host.GetBounds()`.

**Objectif**  
Retirer l'hypothese d'un espace logique unique global.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/MGWindow.cs`

**Modification attendue**  
Remplacer les lectures directes du host par une source de bounds liee a la vue/surface.

**Criteres d'acceptation**  
- Une meme vue peut avoir des bounds differents du backbuffer complet
- Les context menus et overlays peuvent se baser sur les bounds de la vue cible

**Dependances eventuelles**  
- `MGUI-ARCH-003`
- `MGUI-ARCH-005`

### MGUI-ARCH-010 - Introduire une interface `IUIRenderContext`
**Contexte**  
`MGUI.Core` depend aujourd'hui directement de `DrawTransaction` dans ses chemins de draw.

**Objectif**  
Commencer a reduire le couplage direct des controles a l'implementation MonoGame du rendu.

**Fichiers / classes concernes**  
- `MGUI.Shared/Rendering/DrawTransaction.cs`
- `MGUI.Core/UI/MGElement.cs`
- Nouveau fichier propose: `MGUI.Shared/Rendering/IUIRenderContext.cs`

**Modification attendue**  
Definir une interface minimale pour draw, clip, transform et texte.

**Criteres d'acceptation**  
- `DrawTransaction` implemente `IUIRenderContext`
- Les nouveaux appels haut niveau peuvent cibler l'interface plutot que la classe concrete

**Dependances eventuelles**  
Aucune

### MGUI-ARCH-011 - Adapter `ElementDrawArgs` pour utiliser `IUIRenderContext`
**Contexte**  
`ElementDrawArgs` expose `DrawTransaction DT`, ce qui diffuse l'implementation concrete dans tout l'arbre UI.

**Objectif**  
Rompre la propagation obligatoire de `DrawTransaction` dans `MGUI.Core`.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Shared/Rendering/DrawTransaction.cs`

**Modification attendue**  
Faire porter a `ElementDrawArgs` une reference `IUIRenderContext` ou un alias equivalent.

**Criteres d'acceptation**  
- Les signatures de draw des elements ne dependent plus de `DrawTransaction`
- Le comportement de rendu existant reste identique

**Dependances eventuelles**  
- `MGUI-ARCH-010`

### MGUI-ARCH-012 - Faire consommer l'interface de rendu par les brushes
**Contexte**  
Les brushes de fond et de bord consomment aujourd'hui le contexte de draw concret via `ElementDrawArgs`.

**Objectif**  
Verifier que les primitives UI de haut niveau n'ont plus besoin de connaitre l'implementation MonoGame concrete.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/Brushes/Fill Brushes/*`
- `MGUI.Core/UI/Brushes/Border Brushes/*`
- `MGUI.Core/UI/MGElement.cs`

**Modification attendue**  
Adapter les implementations de brushes pour passer par l'interface introduite.

**Criteres d'acceptation**  
- Les brushes compilement sans dependre d'une API concrete de transaction hors interface
- Les draws de fond, bordure et overlay continuent de fonctionner

**Dependances eventuelles**  
- `MGUI-ARCH-011`

### MGUI-ARCH-013 - Sortir le chargement d'icones par defaut du constructeur `MGDesktop`
**Contexte**  
`MGDesktop` charge actuellement plusieurs textures de sample et d'icones docking dans son constructeur.

**Objectif**  
Separarer le bootstrap de contenu de la creation du runtime UI.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Samples/Game1.cs`

**Modification attendue**  
Deplacer ce chargement vers une methode de bootstrap explicite ou vers le projet sample.

**Criteres d'acceptation**  
- Instancier `MGDesktop` n'entraine plus le chargement implicite d'assets de demo
- Les samples continuent de fonctionner apres bootstrap explicite

**Dependances eventuelles**  
Aucune

### MGUI-ARCH-014 - Introduire un `IUIAssetProvider`
**Contexte**  
Le chargement et le cache des textures, fonts, effects et themes sont repartis entre `MainRenderer`, `MGResources` et le code applicatif.

**Objectif**  
Distinguer chargement d'assets et consommation runtime.

**Fichiers / classes concernes**  
- `MGUI.Shared/Rendering/MainRenderer.cs`
- `MGUI.Core/UI/MGResources.cs`
- `MGUI.Shared/Text/FontManager.cs`
- Nouveau fichier propose: `MGUI.Shared/Assets/IUIAssetProvider.cs`

**Modification attendue**  
Introduire un provider d'assets UI charge de fournir textures, fonts et themes a la demande.

**Criteres d'acceptation**  
- `MGResources` n'est plus oblige de connaitre directement toute la strategie de chargement
- Le provider peut etre mocke en test

**Dependances eventuelles**  
- `MGUI-ARCH-013`

### MGUI-ARCH-015 - Separer definitions nommees et cache runtime dans `MGResources`
**Contexte**  
`MGResources` joue a la fois le role de dictionnaire de definitions et de facade runtime de consommation.

**Objectif**  
Clarifier la frontiere entre declaration logique des ressources et objets runtime caches.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGResources.cs`
- `MGUI.Core/UI/MGTheme.cs`

**Modification attendue**  
Scinder au moins conceptuellement `MGResources` en definitions nommees et acces runtime/caches.

**Criteres d'acceptation**  
- Les themes, styles, commandes et textures ne sont plus tous geres au meme niveau de responsabilite
- Le code appelant reste compatible via facade ou API de transition

**Dependances eventuelles**  
- `MGUI-ARCH-014`

### MGUI-ARCH-016 - Introduire un `XamlDocumentSource`
**Contexte**  
`XAMLParser` travaille surtout a partir de chaines et ne modele pas la provenance du document.

**Objectif**  
Rendre explicite l'origine du XAML: string, fichier, stream ou memoire hot reload.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/XAML/XAMLParser.cs`
- `MGUI.Core/UI/MGXAMLDesigner.cs`
- Nouveau fichier propose: `MGUI.Core/UI/XAML/XamlDocumentSource.cs`

**Modification attendue**  
Ajouter un type source pour le chargement XAML.

**Criteres d'acceptation**  
- Le parser peut etre invoque depuis plusieurs sources sans duplication de logique
- Le designer peut reutiliser cette abstraction

**Dependances eventuelles**  
Aucune

### MGUI-ARCH-017 - Rendre reutilisable la phase definition XAML -> runtime
**Contexte**  
Le parser parse et instancie rapidement le runtime dans le meme flux, ce qui limite les usages tooling et l'analyse hors runtime.

**Objectif**  
Mieux separer la definition XAML parsee de son instanciation runtime.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/XAML/XAMLParser.cs`
- `MGUI.Core/UI/XAML/Element.cs`
- `MGUI.Core/UI/XAML/Controls.cs`

**Modification attendue**  
Formaliser une etape explicite de document/definition parsee pouvant etre reutilisee avant creation des `MGElement`.

**Criteres d'acceptation**  
- Une definition parsee peut etre inspectee ou re-instanciee
- L'instanciation runtime n'oblige pas a reparcourir tout le pipeline de parsing source

**Dependances eventuelles**  
- `MGUI-ARCH-016`

### MGUI-ARCH-018 - Remplacer `MGWindow.ModalWindow` par une modalite empilable
**Contexte**  
La modalite fenetre est aujourd'hui modelisee par une seule propriete `ModalWindow`.

**Objectif**  
Permettre un modele plus general de pile modale sans casser la logique existante.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGWindow.cs`
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Tests/Modal/ModalBlockingTests.cs`

**Modification attendue**  
Introduire un service ou une structure de pile modale, puis conserver `ModalWindow` comme facade de compatibilite si necessaire.

**Criteres d'acceptation**  
- Plusieurs niveaux de modalite peuvent etre representes proprement
- Les tests de blocage modal existants restent vrais

**Dependances eventuelles**  
- `MGUI-ARCH-006`

### MGUI-ARCH-019 - Extraire la politique focus/navigation hors de `MGDesktop`
**Contexte**  
Focus, historique, auto-focus, navigation clavier et gamepad sont fortement centralises dans `MGDesktop`.

**Objectif**  
Reduire le couplage entre root UI et politique de navigation.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGDesktop.cs`
- `MGUI.Core/UI/INavigationTargetVisibilityHandler.cs`
- `MGUI.Core/UI/Enums.cs`
- Nouveau fichier propose: `MGUI.Core/UI/Navigation/UIFocusNavigationService.cs`

**Modification attendue**  
Extraire le mapping d'actions, le choix des cibles et la gestion du focus dans un service dedie.

**Criteres d'acceptation**  
- `MGDesktop` ne contient plus la majorite de la logique de navigation
- Le service peut etre attache a une vue au lieu d'un desktop singleton implicite

**Dependances eventuelles**  
- `MGUI-ARCH-005`
- `MGUI-ARCH-006`

### MGUI-ARCH-020 - Introduire des hooks tooling pour inspection et live preview
**Contexte**  
`MGXAMLDesigner` montre un besoin d'outillage, mais aucune couche tooling formelle n'existe.

**Objectif**  
Preparer l'evolution vers un mode editeur sans polluer davantage le runtime principal.

**Fichiers / classes concernes**  
- `MGUI.Core/UI/MGXAMLDesigner.cs`
- `MGUI.Core/UI/MGElement.cs`
- `MGUI.Core/UI/XAML/XAMLParser.cs`
- Nouveau dossier propose: `MGUI.Core/Tooling/`

**Modification attendue**  
Ajouter des hooks de snapshot de l'arbre visuel, d'inspection layout et de rafraichissement XAML.

**Criteres d'acceptation**  
- Un outil peut enumerer l'arbre visuel sans recoder la traversal interne
- Le live preview ne depend plus uniquement du controle `MGXAMLDesigner`

**Dependances eventuelles**  
- `MGUI-ARCH-016`
- `MGUI-ARCH-017`