# Responsive Layout Tasks

Objectif: permettre de creer des ecrans UI qui s'adaptent a toute resolution, tout ratio d'aspect, et optionnellement au DPI, sans melanger le layout responsive avec le `Scale` de rendu deja present sur `MGWindow`.

## Regles pour l'agent

- Respecter l'ordre des taches ci-dessous. Ne pas commencer la tache suivante tant que la precedente n'est pas terminee, verifiee, puis committee.
- Faire exactement 1 commit par tache.
- Conserver la compatibilite descendante autant que possible. En particulier, ne pas redefinir `MGWindow.Scale` comme mecanisme principal de responsive layout.
- Privilegier de petites etapes verticales: modele, integration, tests, puis adoption.
- A la fin de chaque tache: executer au minimum les builds de `MGUI.Tests` et `MGUI.Samples`, puis committer.

Validation minimale apres chaque tache:

1. `dotnet build .\MGUI.Tests\MGUI.Tests.csproj`
2. `dotnet build .\MGUI.Samples\MGUI.Samples.csproj`
3. Commit avec le message recommande par la tache

## Contraintes d'architecture

- Garder une separation nette entre:
  - layout logique;
  - scale global de l'UI;
  - scale du texte;
  - transform de rendu existante (`MGWindow.Scale`, `RenderScale`, clips, etc.).
- Le responsive doit partir d'une `design resolution` configurable et produire des metriques resolues pour le viewport courant.
- Les layouts existants (`MGStackPanel`, `MGDockPanel`, `MGGrid`, etc.) doivent rester la voie principale pour organiser les blocs.
- Les anchors doivent surtout couvrir les cas de positionnement relatif au viewport ou au parent, pas remplacer les layouts.
- Les clamps min/max doivent s'appliquer au bon niveau: viewport, UI scale, tailles d'elements, et texte.

## Ordre de priorite

### 1. Poser le vocabulaire et les invariants du responsive layout

But: definir une architecture cible avant d'ajouter des APIs permanentes.

Travail attendu:

- Rediger un court document d'architecture qui fixe les notions suivantes:
  - `DesignResolution` configurable;
  - `UIScaleFactor` global calcule;
  - `TextScaleFactor` separe;
  - option DPI explicite et desactivee par defaut;
  - difference entre layout responsive et scale de rendu.
- Preciser ou vivent les metriques globales: `MGDesktop`, `UIView`, ou service dedie.
- Preciser la politique de compatibilite pour les ecrans et controles existants.

Critere d'acceptation:

- Le document tranche les noms, les responsabilites, et les points d'integration.
- Le document interdit explicitement de reposer la fonctionnalite responsive sur `MGWindow.Scale`.

Commit recommande:

- `docs: define responsive layout architecture and invariants`

### 2. Introduire le modele central de metriques responsive

But: avoir des types stables avant d'injecter du comportement dans tout le framework.

Travail attendu:

- Ajouter les modeles necessaires, par exemple:
  - `UIDesignResolution` ou equivalent;
  - `UIResponsiveSettings`;
  - `UIResolvedMetrics`;
  - `UIScaleMode` si plusieurs strategies sont supportees plus tard.
- Inclure au minimum:
  - resolution de reference;
  - facteur de scale UI min/max;
  - facteur de scale texte min/max;
  - bool ou mode pour l'usage optionnel du DPI.
- Garder les types immuables ou faciles a comparer/tester.

Critere d'acceptation:

- Les types compilent sans integrer encore tout le pipeline.
- Les modeles couvrent bien UI scale, text scale, min/max, et DPI optionnel.

Commit recommande:

- `feat: add responsive layout metrics models`

### 3. Resoudre les metriques globales depuis le viewport courant

But: calculer un `UIScaleFactor` coherent a partir de la design resolution et de la taille reelle du viewport.

Travail attendu:

- Implementer un resolveur de metriques globales a partir:
  - de la design resolution;
  - de la taille courante du desktop/view;
  - du DPI effectif si l'option est active.
- Supporter au minimum:
  - scale uniforme base sur `min(widthRatio, heightRatio)`;
  - clamp min/max du scale UI;
  - calcul separe du scale texte.
- Exposer les metriques resolues a un point central et stable.

Critere d'acceptation:

- Pour plusieurs viewport sizes, le calcul renvoie des valeurs deterministes et testables.
- Le scale texte peut diverger du scale UI sans casser le modele.

Commit recommande:

- `feat: resolve global responsive metrics from viewport`

### 4. Ajouter des tests unitaires sur la resolution des metriques

But: verrouiller les regles avant l'integration dans le layout.

Travail attendu:

- Creer une suite de tests dedies aux cas suivants:
  - viewport identique a la design resolution;
  - viewport plus petit;
  - viewport plus grand;
  - ratio d'aspect tres large et tres haut;
  - clamp min/max atteint;
  - DPI active et desactive;
  - text scale separe du UI scale.

Critere d'acceptation:

- Les tests couvrent les formules et les clamps.
- Les tests sont independants du rendu.

Commit recommande:

- `test: cover responsive metrics resolution rules`

### 5. Introduire une racine d'ecran responsive

But: donner une primitive claire pour construire de vrais "screens" adaptes au viewport.

Travail attendu:

- Ajouter une abstraction de racine d'ecran, par exemple:
  - un `MGScreen`;
  - ou un `MGResponsiveRoot`;
  - ou une convention equivalente au niveau `MGWindow`/`MGDesktop`.
- Cette racine doit:
  - occuper le viewport utile;
  - recevoir les metriques resolues;
  - servir de point d'ancrage pour les layouts internes.
- Definir comment un ecran plein format se branche sur `MGDesktop`.

Critere d'acceptation:

- Il existe une voie officielle pour declarer un ecran responsive plein viewport.
- La racine d'ecran ne depend pas du scale de rendu de fenetre.

Commit recommande:

- `feat: add responsive screen root abstraction`

### 6. Ajouter un systeme d'anchors pour le positionnement relatif

But: couvrir les elements qui doivent rester fixes a un coin, un bord, ou un centre quand la resolution change.

Travail attendu:

- Ajouter un modele d'anchors applicable aux elements places librement.
- Supporter au minimum:
  - top-left;
  - top-right;
  - bottom-left;
  - bottom-right;
  - center;
  - stretch horizontal;
  - stretch vertical;
  - stretch complet.
- Definir l'interaction entre anchors, offsets, margin, et alignments existants.
- Limiter le scope aux cas hors-layout ou overlay; ne pas casser les containers existants.

Critere d'acceptation:

- Un element ancre conserve une position logique coherente quand le viewport change.
- Les anchors et les layouts ont des responsabilites clairement separees.

Commit recommande:

- `feat: add anchor-based positioning for responsive screens`

### 7. Integrer margins, padding, et contraintes min/max dans la politique responsive

But: eviter une UI trop petite ou trop grande une fois le scale global applique.

Travail attendu:

- Definir la politique exacte pour:
  - margins/padding scales ou non;
  - min/max width/height avant ou apres scale;
  - tailles preferees et tailles calculees.
- Implementer les clamps necessaires dans le pipeline de mesure/layout.
- Verifier que les controles avec tailles minimales existantes gardent un comportement raisonnable.

Critere d'acceptation:

- Une UI ne devient ni illisible sur petit ecran ni disproportionnee sur grand ecran.
- Les contraintes sont appliquees de maniere coherente et testable.

Commit recommande:

- `feat: apply responsive clamps to spacing and element sizes`

### 8. Rendre les containers de layout existants "responsive-aware"

But: faire des layouts existants la solution par defaut pour composer les ecrans adaptatifs.

Travail attendu:

- Auditer puis adapter en priorite:
  - `MGStackPanel`;
  - `MGDockPanel`;
  - `MGGrid`;
  - les containers overlay ou equivalents.
- Verifier que les mesures, repartitions, et alignements restent corrects sous scale global.
- Corriger les hypotheses de pixels fixes qui cassent quand le viewport diverge fortement de la design resolution.

Critere d'acceptation:

- Un ecran compose de layouts standards s'adapte sans recourir a du positionnement manuel partout.
- Les containers conservent des resultats stables sous plusieurs resolutions.

Commit recommande:

- `feat: make core layout containers responsive-aware`

### 9. Separer la mise a l'echelle du texte de la mise a l'echelle UI

But: garder une lisibilite correcte sans grossir ou reduire tous les blocs de la meme facon.

Travail attendu:

- Integrer `TextScaleFactor` dans la mesure et le rendu du texte.
- Ajouter des clamps specifiques au texte.
- Definir la precedence entre:
  - taille de police explicite;
  - theme;
  - text scale global;
  - eventuels overrides locaux.
- Verifier les impacts sur wrapping, caret, selection, et controles textuels.

Critere d'acceptation:

- Le texte peut rester lisible sur tres petit ou tres grand ecran sans desequilibrer tout le layout.
- Les controles texte continuent a mesurer correctement.

Commit recommande:

- `feat: add independent text scaling to responsive UI`

### 10. Exposer les nouvelles notions dans les APIs et en XAML

But: rendre le systeme utilisable sans code imperatif partout.

Travail attendu:

- Exposer les proprietes pertinentes dans les objets publics et/ou XAML:
  - design resolution;
  - responsive settings;
  - anchors;
  - min/max size;
  - options de text scale.
- Ajouter les conversions, valeurs par defaut, et validations necessaires.
- Documenter les usages preferes et anti-patterns.

Critere d'acceptation:

- Un ecran responsive peut etre decrit en grande partie par configuration/XAML.
- Les APIs publiques sont coherentes avec le document d'architecture.

Commit recommande:

- `feat: expose responsive layout settings in public APIs and xaml`

### 11. Ajouter un ou plusieurs ecrans de sample multi-resolution

But: prouver la valeur du systeme sur des cas concrets, pas seulement sur des primitives.

Travail attendu:

- Ajouter au moins un sample d'ecran complet contenant:
  - une zone principale en layout;
  - un header/footer ou side panel;
  - un ou deux overlays ancres;
  - du texte soumis au `TextScaleFactor`.
- Verifier le rendu sur plusieurs resolutions cibles, par exemple:
  - 1280x720;
  - 1920x1080;
  - 2560x1440;
  - ultra-wide;
  - petite fenetre.

Critere d'acceptation:

- Le sample reste lisible, bien aligne, et utilisable sur plusieurs tailles d'ecran.
- Il demontre layouts + anchors + clamps + text scale.

Commit recommande:

- `feat: add responsive screen samples across multiple resolutions`

### 12. Completer la couverture de tests de regression layout

But: stabiliser le comportement avant migration plus large.

Travail attendu:

- Ajouter des tests sur:
  - anchors;
  - min/max clamps;
  - layouts sous scale global;
  - text scale et mesure de texte;
  - comportement en ratios extremes.
- Cibler en priorite les calculs et bounds, pas les captures visuelles fragiles.

Critere d'acceptation:

- Les principaux calculs responsive sont couverts par des tests reproductibles.
- Les regressions de bounds et de mesures deviennent detectables rapidement.

Commit recommande:

- `test: add responsive layout regression coverage`

### 13. Documenter la migration des ecrans existants

But: permettre l'adoption sans forcer une refonte immediate de toute la bibliotheque.

Travail attendu:

- Rediger un guide de migration qui explique:
  - quand utiliser un layout;
  - quand utiliser des anchors;
  - comment choisir la design resolution;
  - comment regler les clamps;
  - comment activer ou non le DPI;
  - comment traiter le texte separement.
- Ajouter une section sur les compatibilites et limites connues.

Critere d'acceptation:

- Un developpeur peut migrer un ecran existant sans deviner les bonnes pratiques.
- Le guide reference les APIs finales et les samples.

Commit recommande:

- `docs: add responsive layout migration guide`

## Ordre d'execution resume

1. Architecture et invariants
2. Modeles de metriques
3. Resolution des metriques globales
4. Tests unitaires du resolveur
5. Racine d'ecran responsive
6. Anchors
7. Clamps de tailles et d'espacements
8. Containers responsive-aware
9. Text scale separe
10. APIs publiques et XAML
11. Samples multi-resolution
12. Tests de regression layout
13. Guide de migration

## Notes importantes

- Si une tache revele un probleme de nomenclature ou d'architecture, corriger d'abord le document d'architecture puis continuer.
- Si une tache devient trop grosse pour un commit raisonnable, la scinder en sous-taches numerotees avant implementation, tout en conservant l'ordre de priorite.
- Le succes n'est pas d'ajouter beaucoup de proprietes; le succes est d'obtenir des ecrans adaptatifs previsibles, testables, et simples a composer avec les layouts existants.