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

- [x] Identifier tous les contrôles et composants qui dessinent :
  - [x] un fond ;
  - [x] une bordure ;
  - [x] un rectangle ;
  - [x] une forme assimilée à une box.
- [x] Lister précisément :
  - [x] où la géométrie est construite ;
  - [x] où le paint est appliqué ;
  - [x] quels appels utilisent directement `Rectangle`, `Thickness`, `Polygon`, `Triangle`, etc.
- [x] Produire un mini rapport technique `Docs/shape-rendering-audit.md`.

#### Critère d’acceptation

- [x] Le rapport liste clairement :
  - [x] les classes concernées ;
  - [x] les responsabilités actuelles ;
  - [x] les points de couplage à casser.

---

### 2. Introduire un type `MGCornerRadius`

- [x] Créer un type dédié représentant les 4 rayons de coins :
  - [x] `TopLeft`
  - [x] `TopRight`
  - [x] `BottomRight`
  - [x] `BottomLeft`
- [x] Prévoir :
  - [x] un constructeur uniforme,
  - [x] une valeur `Zero`,
  - [x] une propriété `IsZero`,
  - [x] les comparaisons utiles.
- [x] Nommer clairement le type et éviter les ambiguïtés.

#### Critère d’acceptation

- [x] Le type peut représenter :
  - [x] un rectangle sans arrondi ;
  - [x] un arrondi uniforme ;
  - [x] des rayons différents par coin.

---

### 3. Introduire un modèle de shape commun pour les box UI

- [x] Créer une structure dédiée représentant une box à dessiner.
- [x] Cette structure doit contenir au minimum :
  - [x] bounds externes ;
  - [x] épaisseur de bordure ;
  - [x] corner radius.
- [x] Ajouter des helpers pour calculer :
  - [x] bounds internes ;
  - [x] corner radius interne ;
  - [x] validité / clamp des valeurs.

#### Critère d’acceptation

- [x] Un seul type permet d’exprimer :
  - [x] fond rectangulaire ;
  - [x] fond arrondi ;
  - [x] bordure rectangulaire ;
  - [x] bordure arrondie à épaisseur variable.

---

### 4. Définir clairement la séparation entre géométrie et paint

- [x] Introduire une convention d’architecture explicite :
  - [x] shape / geometry builder = construit les contours, segments, rings, polygones ;
  - [x] paint / brush = applique la couleur, texture, gradient, bandes, etc.
- [x] Écrire une courte note d’architecture dans `Docs/shape-paint-architecture.md`.
- [x] Vérifier que cette convention est cohérente avec le reste de MGUI.

#### Critère d’acceptation

- [x] Le document explique clairement :
  - [x] ce qui appartient à la géométrie ;
  - [x] ce qui appartient au paint ;
  - [x] ce qui n’a plus le droit d’être fait dans un brush.

---

### 5. Créer une couche `Shape Geometry Builder`

- [x] Ajouter un composant dédié qui construit la géométrie d’une box.
- [x] Il doit pouvoir produire :
  - [x] le contour externe ;
  - [x] le contour interne ;
  - [x] l’anneau de bordure ;
  - [x] les points nécessaires pour un rectangle arrondi.
- [x] Prévoir une API claire et indépendante des brushes.
- [x] Prévoir le paramétrage du niveau de tessellation des coins arrondis.

#### Critère d’acceptation

- [x] La construction de géométrie d’un rounded rectangle n’est plus dans les brushes.
- [x] Le builder peut être appelé par plusieurs painters différents.

---

### 6. Concevoir une structure de données de géométrie réutilisable

- [x] Créer un type de sortie pour le geometry builder, par exemple :
  - [x] contour externe,
  - [x] contour interne,
  - [x] vertices,
  - [x] indices,
  - [x] méta-infos utiles.
- [x] La structure doit être réutilisable par :
  - [x] fill painter,
  - [x] stroke painter,
  - [x] future clip shape,
  - [x] future hit test.

#### Critère d’acceptation

- [x] Les painters consomment une géométrie déjà calculée.
- [x] Les données ne sont pas recalculées inutilement dans chaque brush.

---

### 7. Ajouter dans le moteur de rendu des primitives de haut niveau pour les rounded shapes

- [x] Étendre `DrawTransaction` ou la couche équivalente avec des méthodes dédiées :
  - [x] fill rounded rectangle ;
  - [x] stroke rounded rectangle ;
  - [x] draw border ring.
- [x] Ne pas exposer tout de suite une API inutilement énorme.
- [x] Prévoir une implémentation propre qui s’appuie sur la géométrie construite.
- [x] Borner explicitement la phase 1 aux primitives nécessaires pour les cas solid fill / solid stroke / border ring arrondi.
- [x] Ne pas imposer dans cette étape une solution complète pour tous les paints avancés sur formes arrondies.

#### Critère d’acceptation

- [x] Le moteur sait dessiner :
  - [x] un fond arrondi ;
  - [x] une bordure arrondie avec épaisseur ;
  - [x] un rectangle classique via fast path.
- [x] Les primitives de phase 1 suffisent pour brancher les premiers painters arrondis sans figer prématurément l’API des futurs gradients/textures.

---

### 8. Ajouter un fast path explicite pour les rectangles non arrondis

- [x] Conserver l’implémentation rapide existante pour :
  - [x] `CornerRadius == 0`
  - [x] cas simples de stroke rectangulaire.
- [x] Faire en sorte que la nouvelle architecture ne dégrade pas les cas les plus courants.
- [x] Isoler clairement le choix entre :
  - [x] chemin rapide rectangle ;
  - [x] chemin géométrique arrondi.

#### Critère d’acceptation

- [x] Le rendu des rectangles simples n’utilise pas la tessellation arrondie.
- [x] La logique est centralisée et non dupliquée.

---

### 9. Définir une stratégie de compatibilité transitoire des APIs de paint

- [x] Introduire une phase de transition explicite entre les signatures actuelles basées sur `Rectangle` / `Thickness` et les futures entrées basées sur `BoxShape` ou géométrie calculée.
- [x] Définir si cette transition passe par :
  - [x] des surcharges temporaires ;
  - [x] des adapters ;
  - [x] des helpers de bridge centralisés.
- [x] Interdire les migrations ad hoc différentes brush par brush.

#### Critère d’acceptation

- [x] Un agent peut migrer progressivement les paints sans refactor brutal de toute l’API publique.
- [x] Le chemin de migration est défini avant la modification des interfaces `IBorderBrush` et `IFillBrush`.

---

### 10. Clarifier le cycle de vie des paints stateful ou animés

- [x] Identifier les paints qui ne sont pas purement stateless et qui nécessitent une logique `Update` ou un état interne.
- [x] Définir comment ce cycle de vie s’articule avec la nouvelle séparation shape / geometry / paint.
- [x] Vérifier explicitement le cas des brushes composés et des brushes animés.

#### Critère d’acceptation

- [x] La nouvelle architecture ne casse pas les paints animés ou stateful.
- [x] Le contrat de mise à jour est documenté avant la migration des brushes existants.

---

### 11. Faire évoluer l’interface des border brushes

- [x] Modifier l’API des border brushes pour qu’ils travaillent à partir d’une shape commune plutôt qu’à partir de simples bounds rectangulaires.
- [x] Éviter une refactorisation brutale de tous les brushes à la fois.
- [x] S’appuyer sur la stratégie de compatibilité définie à l’étape précédente.

#### Critère d’acceptation

- [x] Un border brush reçoit une shape ou une géométrie, pas juste un rectangle brut.
- [x] La logique d’arrondi n’est plus recodée brush par brush.

---

### 12. Faire évoluer l’interface des fill brushes

- [ ] Permettre aux fill brushes de peindre une forme de box, pas seulement un rectangle brut.
- [ ] Prévoir que les futurs gradients et textures puissent réutiliser la même entrée.
- [ ] Garder éventuellement une surcharge de compatibilité temporaire.
- [ ] Éviter de forcer dès cette étape une abstraction trop riche qui figerait inutilement les paints avancés.

#### Critère d’acceptation

- [ ] Un fill brush peut remplir un rounded rectangle sans reconstruire lui-même la géométrie.

---

### 13. Refactorer `MGBorder` pour utiliser la nouvelle architecture

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

### 14. Refactorer `MGRectangle` pour utiliser la nouvelle architecture

- [ ] Ajouter `CornerRadius`.
- [ ] Remplacer la logique directe actuelle par l’utilisation du modèle de shape.
- [ ] Vérifier que `MGRectangle` et `MGBorder` convergent vers la même façon de dessiner une box.
- [ ] Conserver autant que raisonnable la façade publique existante (`Stroke`, `StrokeThickness`, `Fill`) en la faisant reposer en interne sur la nouvelle architecture.

#### Critère d’acceptation

- [ ] `MGRectangle` ne contient pas sa propre implémentation spéciale des rounded corners.
- [ ] Le comportement reste cohérent avec `MGBorder`.
- [ ] La migration n’impose pas un changement d’usage inutile aux consommateurs existants de `MGRectangle`.

---

### 15. Refactorer les border brushes existants un par un

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

### 16. Refactorer les fill brushes existants un par un

- [ ] Migrer progressivement :
  - [ ] fill brush uniforme ;
  - [ ] gradient fill brush ;
  - [ ] texture fill brush ;
  - [ ] brushes composés ou spécialisés.
- [ ] Pour chaque brush :
  - [ ] enlever les hypothèses rectangulaires implicites qui doivent appartenir à la shape ou à la géométrie ;
  - [ ] conserver sa responsabilité de paint ;
  - [ ] documenter clairement les limitations transitoires si un paint avancé ne supporte pas encore complètement les rounded shapes.

#### Critère d’acceptation

- [ ] Les fill brushes sont migrés progressivement sans re-dupliquer la logique géométrique.
- [ ] Les limitations de phase 1 sur les paints avancés sont explicites et localisées.

---

### 17. Ajouter une étape de validation / clamp des rayons et épaisseurs

- [ ] Gérer proprement les cas invalides :
  - [ ] rayons trop grands par rapport à la taille ;
  - [ ] inner radius négatif ;
  - [ ] border thickness trop grande ;
  - [ ] dimensions nulles ou très petites.
- [ ] Définir une stratégie de clamp déterministe et documentée.
- [ ] Appliquer cette normalisation avant les chemins de génération et avant tout éventuel caching géométrique.

#### Critère d’acceptation

- [ ] Aucun rendu cassé ou exception inattendue sur des valeurs extrêmes.
- [ ] Les résultats restent visuellement cohérents.
- [ ] Les entrées utilisées comme clé de cache sont déjà normalisées.

---

### 18. Introduire une stratégie de caching géométrique

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

### 19. Préparer l’architecture pour les paints avancés

- [ ] Vérifier que le nouveau modèle permet naturellement d’ajouter plus tard :
  - [ ] gradient fills ;
  - [ ] textured fills ;
  - [ ] textured borders ;
  - [ ] strokes multi-bandes ;
  - [ ] brushes composés.
- [ ] Ajouter des TODO structurés ou interfaces d’extension là où c’est pertinent.
- [ ] Ne pas implémenter tous les paints maintenant, mais s’assurer que l’architecture les permet.
- [ ] Distinguer explicitement ce qui est “supporté en phase 1” de ce qui est seulement “préparé par le design”.

#### Critère d’acceptation

- [ ] Le design n’est pas limité à "solid color rounded rectangle".
- [ ] L’absence éventuelle de support complet pour certains paints avancés sur rounded shapes n’est pas ambiguë.

---

### 20. Exposer `CornerRadius` sur les contrôles qui encapsulent déjà un border

- [ ] Identifier les contrôles qui reposent sur un `MGBorder` interne.
- [ ] Exposer proprement `CornerRadius` là où cela a du sens.
- [ ] Éviter la duplication de propriété si elle peut être relayée proprement.

#### Critère d’acceptation

- [ ] Les contrôles réutilisent la fonctionnalité sans réimplémenter le rendu.

---

### 21. Ajouter les conversions et sérialisations utiles

- [ ] Ajouter un support minimal de `CornerRadius` dans les chemins de configuration indispensables de la phase 1, en priorité :
  - [ ] XAML ;
  - [ ] styles ;
  - [ ] thèmes.
- [ ] Évaluer séparément si une sérialisation interne dédiée est nécessaire dès cette phase.
- [ ] Ajouter les converters nécessaires avec une syntaxe propre et cohérente.
- [ ] Différer les raffinements secondaires qui ne bloquent pas l’adoption ni les tests de la phase 1.

#### Critère d’acceptation

- [ ] Un utilisateur du framework peut configurer un corner radius sans code custom.
- [ ] Le support minimal est disponible suffisamment tôt pour tester la fonctionnalité via markup et thèmes.

---

### 22. Ajouter des tests visuels et techniques

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

### 23. Ajouter une documentation d’architecture finale

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

### 24. Nettoyer les anciens chemins devenus obsolètes

- [ ] Supprimer les helpers devenus inutiles.
- [ ] Supprimer les duplications rectangulaires anciennes si elles ne servent plus qu’à contourner l’ancien design.
- [ ] Renommer les types / méthodes si besoin pour garder une API cohérente.

#### Critère d’acceptation

- [ ] L’architecture finale est lisible.
- [ ] Il ne reste pas de vieux chemins contradictoires inutiles.

---

### 25. Identifier la phase 2 : clip arrondi et hit testing de forme

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
9. stratégie de compatibilité transitoire des APIs
10. cycle de vie des paints stateful / animés
11. évolution des border brushes
12. évolution des fill brushes
13. refactor `MGBorder`
14. refactor `MGRectangle`
15. migration des border brushes existants
16. migration des fill brushes existants
17. clamp/validation
18. cache géométrique
19. préparation paints avancés
20. exposition dans les contrôles composés
21. support minimal XAML / converters / styles
22. tests visuels
23. doc finale
24. nettoyage
25. roadmap clip/hit test

---

## Résultat attendu

À la fin de cette phase, MGUI doit avoir :

- une abstraction claire de shape de box ;
- une séparation nette entre géométrie et paint ;
- un support propre des :
  - coins arrondis,
  - bordures d’épaisseur variable,
  - fills et strokes réutilisables ;
- un support de phase 1 clairement assumé pour :
  - solid fill,
  - solid stroke,
  - border ring arrondi,
  - fast path rectangle non arrondi ;
- une base saine pour :
  - gradients,
  - textures,
  - clipping arrondi,
  - theming moderne,
  - nouveaux contrôles.

Les paints avancés n’ont pas besoin d’être tous entièrement implémentés sur rounded shapes dans cette phase, mais l’architecture doit rendre leur ajout ultérieur naturel et sans recouplage géométrique.

---

## Règles pour l’agent IA

- Ne pas faire de refactor massif non justifié.
- Faire des commits logiques par étape.
- Préférer des petits changements cohérents.
- Éviter les régressions de perf sur les rectangles simples.
- Ne pas dupliquer la logique d’arrondi dans les contrôles.
- Garder la compatibilité de migration quand c’est raisonnable.
- Documenter les décisions d’architecture importantes.