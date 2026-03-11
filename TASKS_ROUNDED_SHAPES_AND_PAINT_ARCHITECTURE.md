## Contexte

MGUI supporte aujourd’hui correctement le rendu de rectangles et de bordures simples, mais la responsabilité est encore trop couplée entre :

- la géométrie de la forme à dessiner ;
- le paint appliqué à cette forme.

Le framework doit évoluer pour supporter proprement :

- les coins arrondis ;
- l’épaisseur de bordure ;
- à terme plusieurs types de paint :
  - fill uni,
  - stroke uni,
  - gradient,
  - texture,
  - bandes,
  - compositions.

L’objectif n’est pas juste d’ajouter un `CornerRadius`, mais de mettre en place une architecture moderne, découplée, propre et extensible, proche de ce qu’on attend d’un moteur UI moderne.

---

## Objectif global

Refactorer le pipeline de rendu des formes UI pour :

1. séparer la géométrie de la forme du paint utilisé pour la dessiner ;
2. introduire une abstraction commune pour les box shapes :
   - rectangle,
   - rectangle à coins arrondis,
   - bordure avec épaisseur ;
3. garder un fast path performant pour les rectangles non arrondis ;
4. permettre aux contrôles existants (`MGBorder`, `MGRectangle`, `MGButton`, etc.) de réutiliser la nouvelle architecture sans duplication ;
5. préparer les prochaines extensions :
   - clip arrondi,
   - hit test de forme,
   - thèmes avancés,
   - textures / gradients / strokes composés.

---

## Contraintes

- Ne pas casser inutilement l’API publique existante.
- Préserver un chemin rapide pour `CornerRadius == 0`.
- Éviter la duplication de logique géométrique dans les brushes.
- Ne pas disperser la logique "rounded rectangle" dans chaque contrôle.
- Favoriser des types simples, explicites, bien nommés.
- Ajouter le minimum de compatibilité transitoire nécessaire pour faciliter la migration.

---

## Tâches

### 1. Faire un audit ciblé des points d’entrée de rendu de formes

- [ ] Identifier tous les contrôles et composants qui dessinent :
  - [ ] un fond ;
  - [ ] une bordure ;
  - [ ] un rectangle ;
  - [ ] une forme assimilée à une box.
- [ ] Lister précisément :
  - [ ] où la géométrie est construite ;
  - [ ] où le paint est appliqué ;
  - [ ] quels appels utilisent directement `Rectangle`, `Thickness`, `Polygon`, `Triangle`, etc.
- [ ] Produire un mini rapport technique `Docs/shape-rendering-audit.md`.

#### Critère d’acceptation

- [ ] Le rapport liste clairement :
  - [ ] les classes concernées ;
  - [ ] les responsabilités actuelles ;
  - [ ] les points de couplage à casser.

---

### 2. Introduire un type `MGCornerRadius`

- [ ] Créer un type dédié représentant les 4 rayons de coins :
  - [ ] `TopLeft`
  - [ ] `TopRight`
  - [ ] `BottomRight`
  - [ ] `BottomLeft`
- [ ] Prévoir :
  - [ ] un constructeur uniforme,
  - [ ] une valeur `Zero`,
  - [ ] une propriété `IsZero`,
  - [ ] les comparaisons utiles.
- [ ] Nommer clairement le type et éviter les ambiguïtés.

#### Critère d’acceptation

- [ ] Le type peut représenter :
  - [ ] un rectangle sans arrondi ;
  - [ ] un arrondi uniforme ;
  - [ ] des rayons différents par coin.

---

### 3. Introduire un modèle de shape commun pour les box UI

- [ ] Créer une structure dédiée représentant une box à dessiner.
- [ ] Cette structure doit contenir au minimum :
  - [ ] bounds externes ;
  - [ ] épaisseur de bordure ;
  - [ ] corner radius.
- [ ] Ajouter des helpers pour calculer :
  - [ ] bounds internes ;
  - [ ] corner radius interne ;
  - [ ] validité / clamp des valeurs.

#### Critère d’acceptation

- [ ] Un seul type permet d’exprimer :
  - [ ] fond rectangulaire ;
  - [ ] fond arrondi ;
  - [ ] bordure rectangulaire ;
  - [ ] bordure arrondie à épaisseur variable.

---

### 4. Définir clairement la séparation entre géométrie et paint

- [ ] Introduire une convention d’architecture explicite :
  - [ ] shape / geometry builder = construit les contours, segments, rings, polygones ;
  - [ ] paint / brush = applique la couleur, texture, gradient, bandes, etc.
- [ ] Écrire une courte note d’architecture dans `Docs/shape-paint-architecture.md`.
- [ ] Vérifier que cette convention est cohérente avec le reste de MGUI.

#### Critère d’acceptation

- [ ] Le document explique clairement :
  - [ ] ce qui appartient à la géométrie ;
  - [ ] ce qui appartient au paint ;
  - [ ] ce qui n’a plus le droit d’être fait dans un brush.

---

### 5. Créer une couche `Shape Geometry Builder`

- [ ] Ajouter un composant dédié qui construit la géométrie d’une box.
- [ ] Il doit pouvoir produire :
  - [ ] le contour externe ;
  - [ ] le contour interne ;
  - [ ] l’anneau de bordure ;
  - [ ] les points nécessaires pour un rectangle arrondi.
- [ ] Prévoir une API claire et indépendante des brushes.
- [ ] Prévoir le paramétrage du niveau de tessellation des coins arrondis.

#### Critère d’acceptation

- [ ] La construction de géométrie d’un rounded rectangle n’est plus dans les brushes.
- [ ] Le builder peut être appelé par plusieurs painters différents.

---

### 6. Concevoir une structure de données de géométrie réutilisable

- [ ] Créer un type de sortie pour le geometry builder, par exemple :
  - [ ] contour externe,
  - [ ] contour interne,
  - [ ] vertices,
  - [ ] indices,
  - [ ] méta-infos utiles.
- [ ] La structure doit être réutilisable par :
  - [ ] fill painter,
  - [ ] stroke painter,
  - [ ] future clip shape,
  - [ ] future hit test.

#### Critère d’acceptation

- [ ] Les painters consomment une géométrie déjà calculée.
- [ ] Les données ne sont pas recalculées inutilement dans chaque brush.

---

### 7. Ajouter dans le moteur de rendu des primitives de haut niveau pour les rounded shapes

- [ ] Étendre `DrawTransaction` ou la couche équivalente avec des méthodes dédiées :
  - [ ] fill rounded rectangle ;
  - [ ] stroke rounded rectangle ;
  - [ ] draw border ring.
- [ ] Ne pas exposer tout de suite une API inutilement énorme.
- [ ] Prévoir une implémentation propre qui s’appuie sur la géométrie construite.

#### Critère d’acceptation

- [ ] Le moteur sait dessiner :
  - [ ] un fond arrondi ;
  - [ ] une bordure arrondie avec épaisseur ;
  - [ ] un rectangle classique via fast path.

---

### 8. Ajouter un fast path explicite pour les rectangles non arrondis

- [ ] Conserver l’implémentation rapide existante pour :
  - [ ] `CornerRadius == 0`
  - [ ] cas simples de stroke rectangulaire.
- [ ] Faire en sorte que la nouvelle architecture ne dégrade pas les cas les plus courants.
- [ ] Isoler clairement le choix entre :
  - [ ] chemin rapide rectangle ;
  - [ ] chemin géométrique arrondi.

#### Critère d’acceptation

- [ ] Le rendu des rectangles simples n’utilise pas la tessellation arrondie.
- [ ] La logique est centralisée et non dupliquée.

---

### 9. Faire évoluer l’interface des border brushes

- [ ] Modifier l’API des border brushes pour qu’ils travaillent à partir d’une shape commune plutôt qu’à partir de simples bounds rectangulaires.
- [ ] Prévoir une phase de compatibilité transitoire si nécessaire.
- [ ] Éviter une refactorisation brutale de tous les brushes à la fois.

#### Critère d’acceptation

- [ ] Un border brush reçoit une shape ou une géométrie, pas juste un rectangle brut.
- [ ] La logique d’arrondi n’est plus recodée brush par brush.

---

### 10. Faire évoluer l’interface des fill brushes

- [ ] Permettre aux fill brushes de peindre une forme de box, pas seulement un rectangle brut.
- [ ] Prévoir que les futurs gradients et textures puissent réutiliser la même entrée.
- [ ] Garder éventuellement une surcharge de compatibilité temporaire.

#### Critère d’acceptation

- [ ] Un fill brush peut remplir un rounded rectangle sans reconstruire lui-même la géométrie.

---

### 11. Refactorer `MGBorder` pour utiliser la nouvelle architecture

- [ ] Ajouter `CornerRadius` à `MGBorder`.
- [ ] Construire une `BoxShape` à partir de :
  - [ ] `LayoutBounds`
  - [ ] `BorderThickness`
  - [ ] `CornerRadius`
- [ ] Déléguer le rendu :
  - [ ] du fond au fill paint ;
  - [ ] de la bordure au border paint ;
  - [ ] sans logique géométrique interne du contrôle.

#### Critère d’acceptation

- [ ] `MGBorder` devient un consommateur de la nouvelle architecture, pas un lieu de calcul géométrique.

---

### 12. Refactorer `MGRectangle` pour utiliser la nouvelle architecture

- [ ] Ajouter `CornerRadius`.
- [ ] Remplacer la logique directe actuelle par l’utilisation du modèle de shape.
- [ ] Vérifier que `MGRectangle` et `MGBorder` convergent vers la même façon de dessiner une box.

#### Critère d’acceptation

- [ ] `MGRectangle` ne contient pas sa propre implémentation spéciale des rounded corners.
- [ ] Le comportement reste cohérent avec `MGBorder`.

---

### 13. Refactorer les border brushes existants un par un

- [ ] Migrer progressivement :
  - [ ] border brush uniforme ;
  - [ ] border brush docked ;
  - [ ] border brush banded ;
  - [ ] brushes composés ou spécialisés.
- [ ] Pour chaque brush :
  - [ ] enlever la géométrie implicite rectangulaire ;
  - [ ] le faire travailler avec la géométrie fournie ;
  - [ ] garder son rôle de paint.

#### Critère d’acceptation

- [ ] Chaque brush devient plus simple.
- [ ] Les brushes ne manipulent plus directement la topologie complète de la forme sauf si c’est leur responsabilité explicite.

---

### 14. Introduire une stratégie de caching géométrique

- [ ] Identifier les cas où la géométrie peut être réutilisée entre frames.
- [ ] Définir une clé de cache basée sur :
  - [ ] taille ;
  - [ ] épaisseur ;
  - [ ] corner radius ;
  - [ ] niveau de tessellation ;
  - [ ] éventuellement DPI / scale.
- [ ] Ajouter un cache local propre, simple et maîtrisé.

#### Critère d’acceptation

- [ ] La géométrie d’un même rounded rectangle n’est pas recalculée à chaque frame sans raison.
- [ ] Le cache est invalidé correctement.

---

### 15. Ajouter une étape de validation / clamp des rayons et épaisseurs

- [ ] Gérer proprement les cas invalides :
  - [ ] rayons trop grands par rapport à la taille ;
  - [ ] inner radius négatif ;
  - [ ] border thickness trop grande ;
  - [ ] dimensions nulles ou très petites.
- [ ] Définir une stratégie de clamp déterministe et documentée.

#### Critère d’acceptation

- [ ] Aucun rendu cassé ou exception inattendue sur des valeurs extrêmes.
- [ ] Les résultats restent visuellement cohérents.

---

### 16. Préparer l’architecture pour les paints avancés

- [ ] Vérifier que le nouveau modèle permet naturellement d’ajouter plus tard :
  - [ ] gradient fills ;
  - [ ] textured fills ;
  - [ ] textured borders ;
  - [ ] strokes multi-bandes ;
  - [ ] brushes composés.
- [ ] Ajouter des TODO structurés ou interfaces d’extension là où c’est pertinent.
- [ ] Ne pas implémenter tous les paints maintenant, mais s’assurer que l’architecture les permet.

#### Critère d’acceptation

- [ ] Le design n’est pas limité à "solid color rounded rectangle".

---

### 17. Exposer `CornerRadius` sur les contrôles qui encapsulent déjà un border

- [ ] Identifier les contrôles qui reposent sur un `MGBorder` interne.
- [ ] Exposer proprement `CornerRadius` là où cela a du sens.
- [ ] Éviter la duplication de propriété si elle peut être relayée proprement.

#### Critère d’acceptation

- [ ] Les contrôles réutilisent la fonctionnalité sans réimplémenter le rendu.

---

### 18. Ajouter les conversions et sérialisations utiles

- [ ] Vérifier si `CornerRadius` doit être supporté dans :
  - [ ] XAML ;
  - [ ] styles ;
  - [ ] thèmes ;
  - [ ] sérialisation interne.
- [ ] Ajouter les converters nécessaires avec une syntaxe propre et cohérente.

#### Critère d’acceptation

- [ ] Un utilisateur du framework peut configurer un corner radius sans code custom.

---

### 19. Ajouter des tests visuels et techniques

- [ ] Ajouter des tests ou samples couvrant :
  - [ ] rectangle simple ;
  - [ ] rectangle arrondi uniforme ;
  - [ ] rectangle arrondi asymétrique ;
  - [ ] bordure épaisse ;
  - [ ] très petite taille ;
  - [ ] forte épaisseur ;
  - [ ] brush uniforme ;
  - [ ] brush banded ;
  - [ ] brush docked.
- [ ] Ajouter des captures ou démos dans un sample UI.

#### Critère d’acceptation

- [ ] Les cas de base et limites sont vérifiés.
- [ ] La non-régression visuelle est observable.

---

### 20. Ajouter une documentation d’architecture finale

- [ ] Documenter la nouvelle séparation :
  - [ ] shape model ;
  - [ ] geometry builder ;
  - [ ] draw transaction ;
  - [ ] fill paints ;
  - [ ] border paints ;
  - [ ] future clip shape.
- [ ] Expliquer comment ajouter un nouveau paint sans casser l’architecture.
- [ ] Expliquer comment ajouter plus tard une nouvelle shape.

#### Critère d’acceptation

- [ ] Un nouveau contributeur comprend où coder :
  - [ ] une nouvelle géométrie ;
  - [ ] un nouveau brush ;
  - [ ] une nouvelle primitive de rendu.

---

## Tâches de fin de phase

### 21. Nettoyer les anciens chemins devenus obsolètes

- [ ] Supprimer les helpers devenus inutiles.
- [ ] Supprimer les duplications rectangulaires anciennes si elles ne servent plus qu’à contourner l’ancien design.
- [ ] Renommer les types / méthodes si besoin pour garder une API cohérente.

#### Critère d’acceptation

- [ ] L’architecture finale est lisible.
- [ ] Il ne reste pas de vieux chemins contradictoires inutiles.

---

### 22. Identifier la phase 2 : clip arrondi et hit testing de forme

- [ ] Rédiger un document de suite expliquant comment brancher :
  - [ ] rounded clipping ;
  - [ ] hit test shape-aware ;
  - [ ] masques ou render target de clip ;
  - [ ] stencil/shader si pertinent.
- [ ] Ne pas l’implémenter dans cette phase sauf si c’est déjà trivial.

#### Critère d’acceptation

- [ ] La roadmap phase 2 est claire et découplée de la phase actuelle.

---

## Ordre recommandé d’exécution

1. Audit ciblé
2. `MGCornerRadius`
3. modèle `BoxShape`
4. note d’architecture shape vs paint
5. geometry builder
6. structure de géométrie réutilisable
7. primitives de rendu rounded shapes
8. fast path rectangle
9. évolution des border brushes
10. évolution des fill brushes
11. refactor `MGBorder`
12. refactor `MGRectangle`
13. migration des brushes existants
14. cache géométrique
15. clamp/validation
16. préparation paints avancés
17. exposition dans les contrôles composés
18. XAML / converters / styles
19. tests visuels
20. doc finale
21. nettoyage
22. roadmap clip/hit test

---

## Résultat attendu

À la fin de cette phase, MGUI doit avoir :

- une abstraction claire de shape de box ;
- une séparation nette entre géométrie et paint ;
- un support propre des :
  - coins arrondis,
  - bordures d’épaisseur variable,
  - fills et strokes réutilisables ;
- une base saine pour :
  - gradients,
  - textures,
  - clipping arrondi,
  - theming moderne,
  - nouveaux contrôles.

---

## Règles pour l’agent IA

- Ne pas faire de refactor massif non justifié.
- Faire des commits logiques par étape.
- Préférer des petits changements cohérents.
- Éviter les régressions de perf sur les rectangles simples.
- Ne pas dupliquer la logique d’arrondi dans les contrôles.
- Garder la compatibilité de migration quand c’est raisonnable.
- Documenter les décisions d’architecture importantes.