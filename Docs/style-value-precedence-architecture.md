# Style Value Precedence Architecture

## Objectif

Definir les invariants de precedence des valeurs pour la future architecture style/theme de MGUI.

Ce document fixe:

- la pile de precedence officielle ;
- les categories de proprietes ;
- les regles d'heritage ;
- les types d'invalidation attendus ;
- les contraintes de compatibilite avec l'architecture actuelle.

Il sert de reference pour toutes les taches suivantes du backlog style/theme.

## Principes directeurs

- Une valeur doit avoir une source explicite et tracable.
- La precedence doit etre la meme quel que soit le point d'entree: code, XAML, style, template ou theme.
- L'heritage ne doit s'appliquer qu'aux proprietes semantiquement heritables.
- Une reevaluation de valeur doit invalider uniquement ce qui est necessaire.
- Le theme ne doit pas ecraser une valeur locale ou une valeur explicitement posee par un template plus proche.
- Le renderer ne doit jamais resoudre de precedence lui-meme.

## Pile de precedence officielle

Du plus fort au plus faible:

1. Animation ou transition runtime
2. Valeur locale explicite
3. Valeur locale issue de binding resolu sur la propriete
4. Setter de visual state actif
5. Setter issu du template d'instance
6. Style explicite applique a l'instance
7. Style implicite applique par type ou role
8. Valeur issue d'une ressource dynamique resolue pour l'instance
9. Valeur issue du theme actif
10. Valeur heritee depuis le parent
11. Valeur par defaut metadonnee par le controle

Definitions:

- `Valeur locale explicite`: valeur posee directement par code ou XAML sur l'instance.
- `Binding resolu`: sa valeur est consideree comme locale tant qu'il alimente la propriete.
- `Visual state actif`: projection temporaire de l'etat visuel, plus forte que style et theme, mais plus faible qu'une animation explicite.
- `Template d'instance`: valeurs definies par le template applique au controle courant.
- `Style explicite`: style reference volontairement par cle, nom ou attribution directe.
- `Style implicite`: style selectionne automatiquement par type, role ou metadata equivalente.
- `Ressource dynamique`: resolution indirecte reevaluable a chaud.
- `Theme actif`: valeurs semantiques de theme, jamais considerees locales.
- `Valeur heritee`: fallback depuis l'ancetre le plus proche exposant une valeur resolue heritable.
- `Valeur par defaut`: valeur definie par le controle lui-meme si aucune autre source ne fournit de resultat.

## Arbitrages importants

### Template versus style

- Un template ne doit pas annuler une valeur locale du controle hote.
- Un template peut fournir des valeurs par defaut a ses parts internes.
- Les styles explicites et implicites du controle hote restent plus faibles qu'un setter de visual state actif, mais plus faibles aussi qu'un override local.
- Les parts internes d'un template recoivent leur propre pile de precedence, avec possibilite de liaison a des proprietes du controle hote.

### Theme versus ressources dynamiques

- Le theme fournit des valeurs semantiques de base.
- Une ressource dynamique resolue plus pres de l'instance gagne sur le theme.
- Un theme peut etre implemente comme un ensemble de ressources, mais conceptuellement il reste un niveau plus faible qu'un override de ressource locale.

### Binding versus style

- Un binding sur une propriete est traite comme une source locale tant qu'il est attache.
- Un style ne doit pas ecraser une propriete alimentee par binding.

### Animations et transitions

- Les animations ont la precedence maximale sur la valeur calculee pendant leur duree.
- A la fin d'une animation, la valeur doit revenir a la meilleure source non animee.

## Categories de proprietes

Les proprietes doivent etre classees par comportement d'heritage et d'invalidation.

### 1. Proprietes de layout

Exemples:

- `Width`, `Height`, `MinWidth`, `MinHeight`, `MaxWidth`, `MaxHeight`
- `Margin`, `Padding`
- alignements et contraintes equivalentes

Regles:

- non heritables par defaut ;
- une modification invalide au minimum la mesure ;
- si la taille arrangee change, l'arrangement et le draw sont aussi invalides.

### 2. Proprietes de contenu et structure

Exemples:

- `Content`
- `Header`
- template applique
- template parts references

Regles:

- non heritables ;
- une modification invalide structure, mesure, arrangement et draw ;
- peut aussi invalider input et navigation si l'arbre change.

### 3. Proprietes visuelles non heritables

Exemples:

- `BackgroundBrush`
- bordures, overlays, coins, icones, textures de skin
- images d'etat et chrome de controle

Regles:

- non heritables par defaut ;
- une modification invalide au minimum le draw ;
- si la propriete influence la taille desiree, invalider aussi la mesure.

### 4. Proprietes visuelles heritables

Exemples:

- `Foreground`
- famille de police par defaut
- taille de police par defaut, si decidee comme heritable
- opacite de contenu, si le modele final la traite comme heritable

Regles:

- heritables ;
- la valeur heritee doit etre la valeur resolue de l'ancetre, pas sa simple valeur locale ;
- une modification invalide le draw des descendants concernes ;
- invalider la mesure aussi si le texte ou la police peuvent changer les bounds.

### 5. Proprietes d'etat visuel

Exemples:

- `IsHovered`, `IsPressed`, `IsSelected`, `IsFocused`, `IsEnabled`

Regles:

- non heritables ;
- ne changent pas directement le theme ou le style ;
- alimentent une projection de visual state qui peut produire des valeurs resolues temporaires.

### 6. Proprietes de comportement

Exemples:

- `Command`
- callbacks
- flags d'input ou navigation

Regles:

- non heritables sauf exception explicite ;
- n'appartiennent pas au styling system ;
- doivent rester hors des themes.

## Regles d'heritage

### Proprietes qui doivent etre heritables

Par defaut, oui:

- foreground texte par defaut ;
- famille de police par defaut ;
- taille de police par defaut si MGUI valide ce choix pour le layout texte ;
- culture ou direction textuelle si ces concepts existent plus tard ;
- eventuelles ressources ou theme scope locaux si exposes comme contexte.

### Proprietes qui ne doivent pas etre heritables

Par defaut, non:

- dimensions et contraintes de layout ;
- fonds, bordures et chrome de controle ;
- template ;
- commandes et comportement ;
- etats interactifs ;
- geometries de skin.

### Regle de resolution de l'heritage

- Une propriete heritable cherche d'abord une valeur non heritee sur l'instance courante.
- Si aucune source locale, style, template, ressource dynamique ou theme ne fournit de valeur, elle herite de l'ancetre le plus proche.
- La recherche s'arrete a la premiere valeur resolue non nulle ou explicitement definie.

## Types d'invalidation officiels

Toute propriete resolue doit declarer son impact principal.

### Draw

Utiliser quand seule l'apparence peinte change.

Exemples:

- couleur de fond ;
- brush ;
- overlay d'etat ;
- opacite sans impact layout.

### Measure

Utiliser quand la taille desiree peut changer.

Exemples:

- padding ;
- police ou taille de police ;
- contenu texte ;
- template qui change la structure ou les marges internes.

### Arrange

Utiliser quand la disposition finale change sans necessairement modifier la mesure desiree globale.

Exemples:

- alignement ;
- ancres ou offsets ;
- certains changements de part active dans un template.

### Structure

Utiliser quand l'arbre visuel ou logique change.

Exemples:

- changement de template ;
- creation ou destruction de template parts ;
- remplacement de contenu ;
- activation d'une variante structurelle.

### Input et Navigation

Utiliser quand hit testing, focus ou navigation peuvent changer.

Exemples:

- `IsEnabled` ;
- `IsHitTestVisible` ;
- remplacement de template affectant les zones interactives ;
- creation ou suppression d'un element focusable.

## Matrice de recommandation d'invalidation

- Layout size ou font: `Measure + Arrange + Draw`
- Alignment ou anchor: `Arrange + Draw`
- Brush ou couleur pure: `Draw`
- Template: `Structure + Measure + Arrange + Draw + Input`
- Changement de visual state: `Draw`, ou `Measure + Arrange + Draw` si le state modifie une taille ou une geometrie
- Changement de ressource dynamique: reexecuter la resolution, puis invalider selon le type de propriete cible

## Compatibilite avec l'architecture actuelle

### Regles de transition

- Les proprietes C# existantes restent l'API publique de surface dans un premier temps.
- Le nouveau modele de valeurs resolues peut vivre derriere ces proprietes.
- Les controles existants ne doivent pas perdre leur comportement si aucune ressource dynamique ni aucun template nouveau n'est utilise.
- Les styles XAML actuels restent supportes, mais leur resultat doit etre injecte dans la nouvelle pile de precedence au lieu d'agir comme une phase separee et definitive.

### Anti-patterns explicitement interdits

- Copier une valeur de theme dans un champ local sans moyen de reevaluation.
- Faire choisir une couleur de theme par le renderer.
- Faire gerer la precedence par chaque controle de maniere ad hoc.
- Melanger comportement et setter de style dans la meme abstraction.
- Utiliser les templates pour imposer des comportements qui devraient rester dans la logique de controle.

## Implications pour les taches suivantes

### Tache 2

- Le modele central de valeurs resolues doit representer au minimum la source gagnante, la valeur resolue et le type d'invalidation associe.

### Tache 3

- Les resources dictionaries hierarchiques devront s'inserer entre theme et heritage dans la pile de resolution, selon leur scope effectif.

### Tache 4 et 5

- Les `DynamicResource` et le theme switch runtime devront reevaluer les valeurs sans casser la precedence locale, les styles explicites et les templates d'instance.

### Tache 6 a 10

- Les control templates et les visual states devront projeter leurs valeurs dans cette pile officielle, pas via des overrides manuels propres a chaque controle.

## Critere de validation de ce document

Ce document est valide si:

- un developpeur peut dire sans ambiguite quelle source gagne pour une propriete donnee ;
- les proprietes heritables sont distinguees des proprietes non heritables ;
- les invalidations attendues sont definies avant le code ;
- les taches de refonte suivantes peuvent s'y referer sans reouvrir le debat de precedence.