# Roadmap priorisee MGUI apres comparaison InkkSlinger

## Objectif

Transformer les opportunites du rapport comparatif en backlog ordonne, executable par lots, et compatible avec la trajectoire reelle de MGUI.

Cette roadmap privilegie les fondations qui augmentent la robustesse du framework, la predictibilite du runtime, et la qualite du developpement. Elle ne vise pas une parite WPF exhaustive.

Le backlog agent derive de cette roadmap est disponible dans `Docs/inkkslinger-vs-mgui-roadmap-tasks.md`.

## Regles de cadrage

- Prioriser les seams et les garde-fous avant d'elargir fortement la surface fonctionnelle.
- Reutiliser les chantiers deja documentes dans `Docs/` au lieu d'ouvrir des refontes concurrentes.
- Livrer des increments verticaux courts, chacun avec tests, sample de validation, et criteres de sortie.
- Reporter les familles desktop-centric tant qu'un besoin MGUI n'est pas demontre.
- Ne pas introduire de property system complet facon WPF ni de moteur de triggers complet comme prealable.

## Ordre de livraison recommande

1. Classe A - Harness de diagnostics et automation legere
2. Classe A - Consolidation lookless runtime autour de la precedence des valeurs
3. Classe B - Validation markup et diagnostics du loader
4. Classe B - Matrice de samples, repros et docs par scenario
5. Classe B - Shapes retained au-dessus des primitives deja presentes
6. Classe B - DataGrid-lite pour usages outils et debug
7. Classe C - Adorner-lite et overlay decorators
8. Classe C - Ameliorations textuelles ciblees avant tout RichTextBox complet

## Dependances de sequencing

- Les lots 1 et 2 doivent avancer avant les lots 5 a 8.
- Le lot 3 peut demarrer en parallele du lot 2, mais il doit converger avec ses contrats de style, template et ressources.
- Le lot 4 doit commencer tot pour servir de support de validation aux autres lots.
- Le lot 5 doit reutiliser les conclusions de [rounded-shapes-phase2-roadmap.md](rounded-shapes-phase2-roadmap.md) et [shape-paint-and-content-clip-separation.md](shape-paint-and-content-clip-separation.md).
- Le lot 6 ne doit pas demarrer avant d'avoir borne un scope v1 ferme.
- Le lot 7 doit attendre que clipping, layering et hit testing soient stabilises.
- Le lot 8 reste volontairement derriere les chantiers transverses de qualite.

## 1. Classe A - Harness de diagnostics et automation legere

But:
donner a MGUI un minimum d'observabilite et de reproductibilite pour debugger focus, input, templates, overlays, clipping et docking sans viser un clone complet d'InkkOops.

Travail attendu:

- definir des identifiants de diagnostic stables pour desktop, fenetres, overlays et elements ;
- ajouter des snapshots structures d'arbre UI avec focus, visibilite effective, clipping, overlays actifs et sources de valeurs ;
- enregistrer et rejouer des sequences ciblees souris, clavier et navigation ;
- fournir une petite couche d'assertion pour les regressions runtime les plus critiques ;
- exposer un point d'entree simple depuis les tests et depuis un sample de repro ;
- couvrir d'abord cinq scenarios cibles: overlay modal, theme switch, template swap, menu ou combo qui isole l'input, docking drag.

Critere d'acceptation:

- un scenario peut etre reproduit avec le meme identifiant entre sample et tests ;
- un echec produit un artefact lisible qui localise l'etat fautif ;
- la surface reste legere et centree sur les usages MGUI.

Validation ciblee:

- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter FullyQualifiedName~Focus`

## 2. Classe A - Consolidation lookless runtime autour de la precedence des valeurs

But:
fiabiliser la resolution runtime de style, theme, template, ressources et visual states avant d'etendre fortement les controles.

References existantes:

- [lookless-convergence-milestone.md](lookless-convergence-milestone.md)
- [style-theme-refactor-tasks.md](style-theme-refactor-tasks.md)
- [theme-definition-tasks.md](theme-definition-tasks.md)
- [control-template-tasks.md](control-template-tasks.md)

Travail attendu:

- figer les invariants de precedence et d'invalidation deja identifies ;
- converger `ThemeDefinition`, `Style`, `ControlTemplate`, ressources et visual states autour d'un meme chemin de resolution ;
- fiabiliser la reevaluation runtime sur changement de theme, template ou ressource ;
- standardiser template parts, presenters et conventions de migration pour les controles composites ;
- migrer un premier groupe de controles de reference borne par le jalon commun `Window`, `Overlay`, `ListBox`, `ListView`, `ComboBox` et `TabControl`, avant de rouvrir le docking et les cas les plus couplants.

Critere d'acceptation:

- un changement de theme ou de ressource reevalue un sous-arbre sans reparse complet ;
- la precedence de chaque valeur visible est explicable et testable ;
- les controles pilotes sortent une part importante de leur skinning hors du code imperatif.

Validation ciblee:

- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter FullyQualifiedName~Theme|FullyQualifiedName~Style|FullyQualifiedName~Template`

## 3. Classe B - Validation markup et diagnostics du loader

But:
transformer le loader XAML en surface previsible, avec erreurs structurees, mode strict et mode compatibilite clairement distingues.

Travail attendu:

- inventorier les echec actuels du loader: types inconnus, setters incompatibles, ressources introuvables, template parts manquantes, conversions invalides ;
- introduire une taxonomie d'erreurs avec code, localisation, message et contexte minimal ;
- ajouter un mode strict activable pour les themes et templates de reference ;
- conserver un mode compatibilite pour les migrations progressives ;
- creer une suite de fixtures negatives couvrant les erreurs attendues ;
- exposer les diagnostics du loader dans au moins un sample de validation.

Etat courant:

- le loader expose maintenant un mode explicite `Strict` vs `Compatibility` pour le parsing XAML, les themes XAML et les `ControlTemplate` ;
- les erreurs strictes sont remontees via `XamlLoaderException` et `XamlLoaderDiagnostic`, avec code, source et position quand l'information est disponible ;
- la validation stricte couvre au minimum racine de document invalide, type XAML inconnu, setter invalide et `TemplatePart` requise absente ;
- `MGXAMLDesigner` consomme ce chemin strict et affiche le diagnostic structure plutot qu'un message brut de parser.

Critere d'acceptation:

- un echec de chargement pointe la zone fautive avec un message actionnable ;
- le mode strict detecte au minimum les erreurs de ressources, types, setters et template parts ;
- les fichiers existants peuvent migrer de facon progressive sans rupture globale brutale.

Validation ciblee:

- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter FullyQualifiedName~XAML|FullyQualifiedName~Markup`

## 4. Classe B - Matrice de samples, repros et docs par scenario

But:
faire de la validation manuelle et de la reproduction de bugs un produit du repo, pas un effort ad hoc.

Travail attendu:

- definir une matrice de scenarios par sous-systeme: input, focus, layout, overlay, clipping, theme, template, docking, text ;
- garantir au moins un sample de demonstration et un sample de repro pour chaque zone a risque ;
- normaliser les identifiants, titres et points d'entree de ces scenarios ;
- relier chaque scenario a un invariant documente et, si possible, a un test cible ;
- ajouter une page d'index dans la doc qui sert de point d'entree aux regressions frequentes.

Critere d'acceptation:

- chaque chantier prioritaire de la roadmap dispose d'un scenario de validation visible ;
- un bug peut etre rattache a un scenario nomme au lieu d'une description libre ;
- la documentation utilisateur et technique convergent vers les memes identifiants de scenario.

Validation ciblee:

- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- verification manuelle de l'index de scenarios et de leurs points d'entree

Reference de suivi:

- `Docs/scenario-validation-index.md`

## 5. Classe B - Shapes retained au-dessus des primitives deja presentes

But:
exposer une premiere surface de shapes UI retained en capitalisant sur les primitives de rendu, sans melanger paint et clipping.

References existantes:

- [rounded-shapes-phase2-roadmap.md](rounded-shapes-phase2-roadmap.md)
- [shape-paint-and-content-clip-separation.md](shape-paint-and-content-clip-separation.md)

Travail attendu:

- definir une surface v1 de controles ou elements pour `Ellipse`, `Line`, `Polygon`, `Polyline` et `PathLite` ;
- reutiliser les abstractions de geometrie et de paint deja posees plutot qu'un pipeline parallele ;
- ajouter du hit testing shape-aware la ou la semantique visuelle l'exige ;
- adopter au moins un chemin de clipping non rectangulaire sur un controle pilote ;
- ajouter une page sample dediee et des tests logiques sur les geometries et le hit testing.

Critere d'acceptation:

- les shapes de base peuvent etre declarees, rendues et stylisees sans code backend specifique dans la couche controle ;
- paint, clip et hit testing reutilisent la meme semantique de forme ;
- la livraison reste compatible avec la separation actuelle entre paint et content clip.

Validation ciblee:

- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter FullyQualifiedName~Shape|FullyQualifiedName~Clip`

## 6. Classe B - DataGrid-lite pour usages outils et debug

But:
combler l'ecart fonctionnel le plus utile cote outillage sans ouvrir d'emblee un controle desktop massif.

Travail attendu:

- figer un scope v1 centre sur lecture, tri, selection, headers, scrolling et sizing de colonnes ;
- reutiliser les primitives de liste, scroll et presentation deja presentes ;
- supporter des datasets de taille moyenne pour outils et debug ;
- fournir un sample dedie avec cas de colonnes mixtes et dataset realiste ;
- exclure explicitement du v1: edition riche, grouping, hierarchy, formules, frozen columns multiples, document model.

Critere d'acceptation:

- le controle couvre un besoin reel d'outil sans imposer une architecture de type bureau ;
- le scope v1 reste ferme et defensable ;
- les scenarios de tri, selection et scroll sont testes et demonstrables.

Validation ciblee:

- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter FullyQualifiedName~Grid|FullyQualifiedName~ListView`

## 7. Classe C - Adorner-lite et overlay decorators

But:
offrir une couche legere pour handles, guides, selection boxes et drop indicators sans introduire un sous-systeme WPF complet.

Travail attendu:

- definir un modele de decorators ancre sur un element cible ;
- supporter les usages prioritaires: selection, resize handles, drop indicators, guides de debug ;
- cadrer clairement layering, clipping et hit testing ;
- limiter la surface a des usages de tooling et de debug ;
- eviter toute arborescence d'adorners generalisee au premier lot.

Critere d'acceptation:

- les overlays de tooling ne reposent plus sur des chemins one-off difficiles a maintenir ;
- les decorators respectent clipping et priorites d'input ;
- la surface reste petite et lisible.

Validation ciblee:

- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter FullyQualifiedName~Overlay|FullyQualifiedName~Dock`

## 8. Classe C - Ameliorations textuelles ciblees avant tout RichTextBox complet

But:
ameliorer les usages chat, log, debug et presentation de texte riche leger sans lancer un document editor complet.

Travail attendu:

- identifier les manques textuels reellement utiles a MGUI avant toute extension large ;
- ajouter un petit modele de runs ou segments stylables si cela couvre les besoins dominants ;
- renforcer selection, caret, clipboard ou wrapping seulement la ou le besoin est etabli ;
- ajouter des samples cibles pour chat, console/log et texte annote ;
- exclure explicitement du premier lot: document model, pagination, flow document, mise en page type bureau.

Critere d'acceptation:

- les scenarios chat, log et debug n'exigent plus implicitement un RichTextBox complet ;
- les extensions textuelles restent compactes et compatibles avec le runtime MGUI ;
- les dettes reportees vers un vrai document editor sont explicites.

Validation ciblee:

- `dotnet build .\MGUI.Tests\MGUI.Tests.csproj --no-restore`
- `dotnet build .\MGUI.Samples\MGUI.Samples.csproj --no-restore`
- `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --no-build --filter FullyQualifiedName~Text|FullyQualifiedName~Chat`

## Hors perimetre pour cette roadmap

- pas de `DependencyObject` ou `DependencyProperty` generalises dans tout le framework ;
- pas de moteur complet de triggers WPF comme prerequis ;
- pas de routed commands ou routed events partout ;
- pas de portage direct des familles `DocumentViewer`, `Frame`, `Page`, `Ribbon` ou `FlowDocument` ;
- pas de retained rendering sophistique ou dirty regions generalises sans profilage cible.

## Regle d'execution pour les agents implementeurs

- traiter cette roadmap comme document de portefeuille et non comme backlog de bas niveau ;
- quand un lot dispose deja d'un backlog dedie dans `Docs/`, conserver ce backlog comme source de verite d'implementation ;
- ne lancer qu'un lot principal a la fois jusqu'a validation de ses criteres de sortie ;
- faire converger tests, samples et documentation dans le meme lot plutot qu'en fin de chantier.