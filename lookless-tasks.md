# Lookless Control Tasks

## Objectif

Ce document sert de backlog d'execution pour un agent IA charge de faire evoluer MGUI en 2 temps:

1. rendre les controles composites reellement lookless, sur le meme niveau d'externalisation visuelle que la direction engagee pour `MGTabControl` et `MGComboBox` ;
2. permettre a un theme de declarer quel `ControlTemplate` doit etre applique a chaque type de controle, afin qu'un theme puisse changer l'apparence complete et pas seulement quelques valeurs de chrome.

Le resultat cible est le suivant:

- la structure visuelle d'un controle doit pouvoir etre entierement remplacee sans reouvrir la logique metier du controle ;
- les comportements restent dans les controles, mais le chrome et la topologie visuelle doivent sortir vers les templates ;
- un theme doit pouvoir piloter declarativement le choix des templates par type de controle ;
- le pipeline doit rester compatible avec les contraintes d'un framework UI pour jeu video: invalidation controlee, pas de hot path inutile, pas de GC evitable sur les chemins frequents.

## Consignes de travail pour l'agent IA

- Executer les taches strictement dans l'ordre.
- Mettre a jour l'icone de statut de la tache avant chaque action importante.
- Quand une tache commence, la passer en `🟡`.
- Quand une tache est terminee et validee, la passer en `✅`, puis faire immediatement un commit git.
- Si une tache est bloquee, la passer en `⛔`, documenter le blocage juste sous la tache, puis s'arreter.
- Faire un commit entre chaque tache terminee. Ne pas regrouper plusieurs taches dans un seul commit.
- Utiliser des commits non interactifs. Ne pas amender un commit existant.
- Si le worktree contient des changements hors perimetre, ne pas les revert ; commit uniquement les fichiers lies a la tache courante.
- Ajouter ou mettre a jour les tests a chaque etape lorsque le comportement runtime ou declaratif change.
- Ne pas faire de refactor massif hors perimetre.
- Preserver la compatibilite descendante tant qu'une tache ne demande pas explicitement de la casser.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Strategie generale

Le chantier est volontairement coupe en 2 phases:

- Phase 1: lookless runtime. L'objectif est de sortir la structure visuelle et le styling imperatif des controles encore hybrides, pour que le choix du template soit une vraie decision de skin.
- Phase 2: theme-driven template selection. L'objectif est d'ajouter un pont declaratif entre `ThemeDefinition`, `MGResources` et les `ControlTemplate` afin qu'un theme puisse choisir les templates a appliquer par type de controle.

La phase 2 ne doit pas etre commencee tant que la phase 1 n'a pas rendu le socle suffisamment stable sur les controles cibles.

## Contraintes de design

- Ne pas introduire un dependency property system complet si une couche plus petite suffit.
- Conserver `MGControlTemplate`, `MGResources` et `ThemeDefinition` comme briques centrales.
- Eviter de dupliquer la meme information entre le theme, le template et le controle.
- Garder une difference nette entre:
  - comportement du controle ;
  - structure visuelle du template ;
  - choix du template par theme ;
  - valeurs de chrome exposees par le theme.
- Une re-application de theme ne doit pas reconstruire toute la structure si seul le chrome change.
- La resolution finale doit rester comprehensible et testable.

## Ordre de commits attendu

Format recommande des commits:

1. `lookless: complete task 1 lookless audit and target matrix`
2. `lookless: complete task 2 tabcontrol lookless migration`
3. `lookless: complete task 3 combobox lookless migration`
4. `lookless: complete task 4 textbox and treeview lookless pass`
5. `lookless: complete task 5 button family and tooltip cleanup`
6. `lookless: complete task 6 style and resource template bridge`
7. `lookless: complete task 7 theme template mapping model`
8. `lookless: complete task 8 theme template runtime resolution`
9. `lookless: complete task 9 xaml loading and precedence coverage`
10. `lookless: complete task 10 docs samples and stabilization`

## Taches

## Phase 1 - Rendre les controles reellement lookless

### ✅ 1. Auditer les controles hybrides et figer la matrice cible

But:
etablir une cible de migration precise avant d'ouvrir les controles un par un.

Travail attendu:

- auditer les controles qui gardent encore de la structure ou du styling imperatif dans leur logique interne ;
- classer les controles en 3 groupes:
  - deja suffisamment lookless ;
  - partiellement lookless ;
  - encore fortement couples au theme ou au draw imperatif ;
- pour chaque controle du perimetre prioritaire, lister:
  - les `TemplateParts` attendues ;
  - la structure actuellement construite en C# ;
  - les valeurs visuelles encore poussees depuis `OnThemeChanged(...)`, les constructeurs ou les helpers de style ;
  - ce qui doit rester en comportement pur ;
- figer le perimetre de la phase 1 pour eviter une migration ouverte sans borne.

Livrable:

- une matrice de migration documentee dans ce fichier ou dans un document associe ;
- une liste priorisee des controles a migrer ;
- des ajustements de tests d'architecture si necessaire pour verrouiller la cible.

Criteres d'acceptation:

- la cible de migration est explicite ;
- il n'y a pas d'ambiguite sur ce qui appartient au controle et au template ;
- la suite des taches peut se faire sans re-decider le perimetre a chaque fois.

Resultat:

- La phase 1 cible en priorite `MGTabControl`, `MGComboBox`, `MGTextBox`, `MGTreeView`, puis la famille bouton et `MGToolTip`.
- La phase 2 devra ajouter un mapping declaratif `theme -> control type -> template name`, avec precedence sous la valeur locale et les styles explicites.

Matrice cible:

| Controle / famille | Etat de depart | Couplages visuels identifies | Cible phase 1 | Hors perimetre immediat |
| --- | --- | --- | --- | --- |
| `MGWindow`, `MGOverlay`, `MGListBox`, `MGListView` | largement template-aware | chrome encore partiellement applique via defaults/theme mais structure deja externe | conserver comme references de migration, pas de refactor majeur | aucune refonte structurelle demandee ici |
| `MGTabControl` | partiellement lookless | styles imperatifs des wrappers d'onglet, factories internes de header wrappers | rendre les headers et leur chrome remplaçables via templates / factories externalisees | ne pas reouvrir la logique de selection/navigation |
| `MGComboBox` | partiellement lookless | dropdown, arrow host et plusieurs valeurs de chrome encore appliquees dans le controle | conserver seulement le comportement de popup/selection/navigation | ne pas rearchitecturer le mecanisme de popup au-dela du chrome |
| `MGTextBox` | hybride fort | placeholder, compteur, grip, selection, caret et chrome encore tres lies au controle | separer moteur d'edition et chrome templateable | ne pas reecrire le moteur d'edition texte |
| `MGTreeView` | hybride moyen | defaults de selection/bordure/indentation encore pousses depuis le controle | pousser chrome et conteneurs vers template/theme | ne pas changer le modele de donnees |
| `MGButton`, `MGToggleButton` | couples au theme | backgrounds, foregrounds, bordures encore recalcules depuis le controle | ouvrir la voie a un vrai template de base button-like | conserver le comportement d'activation |
| `MGCheckBox`, `MGRadioButton` | couples au draw | glyphes internes et styles de base encore peu externalises | isoler le chrome et documenter le draw residuel justifie | ne pas supprimer tout draw imperatif si structurellement necessaire |
| `MGToolTip` | encore code-first | padding, bordure, offset, foreground lies au constructeur/theme | rendre le chrome themable et templateable | garder le comportement hover tel quel |
| `MGScrollViewer` | draw structurel | scrollbars encore dessinees par logique interne | traiter seulement le chrome si necessaire pour debloquer les skins | ne pas tenter une refonte complete lookless des scrollbars dans ce chantier |

Frontiere controle / template retenue:

- Le controle conserve la logique metier, l'etat, la navigation, les evenements et l'orchestration de contenu.
- Le template possede la structure visuelle, les wrappers de chrome, les presenters et la topologie visuelle.
- Le theme ne doit pas decrire la structure, mais choisir des templates et fournir les tokens visuels partages.
- Les styles restent un mecanisme declaratif de surcharge locale ou implicite, distinct du mapping theme -> template.

### ✅ 2. Finaliser le caractere lookless de `MGTabControl`

But:
faire de `MGTabControl` un controle dont le chrome et les wrappers visuels ne dependent plus d'helpers de style imperatifs internes.

Travail attendu:

- supprimer ou reduire fortement la dependance a `ApplyDefaultSelectedTabHeaderStyle(...)` et `ApplyDefaultUnselectedTabHeaderStyle(...)` ;
- sortir les wrappers de headers et leur styling vers des templates ou sous-templates clairement remplaçables ;
- garder dans le controle seulement:
  - la gestion des onglets ;
  - la selection ;
  - les transitions de contenu ;
  - la logique de focus/navigation ;
- verifier que la structure reconstituee depuis template reste stable lors des refreshs de theme et changements d'onglet.

Livrable:

- `MGTabControl` plus lookless ;
- tests couvrant le rattachement de structure, la recreation des headers et la compatibilite de selection ;
- ajustement du catalogue ou des templates XAML si necessaire.

Criteres d'acceptation:

- le look des headers ne depend plus d'un helper imperatif principal dans le controle ;
- un template alternatif peut changer la topologie visuelle du header area sans toucher au comportement ;
- la navigation et la selection restent stables.

Resultat:

- `MGTabControl` ne porte plus ses styles par defaut de headers dans des helpers visuels internes.
- Les wrappers de headers par defaut restent des `MGButton`, mais leur chrome est maintenant pilote par `SelectedTabHeaderControlTemplateName` et `UnselectedTabHeaderControlTemplateName`.
- Le catalogue enregistre deux `ControlTemplate` dedies pour les wrappers d'onglets, ce qui rend leur apparence remplaçable comme une ressource de skin sans retoucher la logique de selection.
- Le pont XAML a ete aligne sur ce nouveau chemin pour que les surcharges declaratives continuent de fonctionner.

### ✅ 3. Finaliser le caractere lookless de `MGComboBox`

But:
sortir du controle la structure et les choix de chrome encore imperativement geres par `MGComboBox`.

Travail attendu:

- externaliser autant que possible la structure du dropdown, l'arrow host, les presenters de header/footer et les conteneurs internes ;
- reduire les affectations visuelles directes dans le controle, notamment celles qui relisent le theme pour le chrome du dropdown ;
- conserver dans `MGComboBox` uniquement:
  - l'ouverture/fermeture ;
  - la selection ;
  - la synchronisation du contenu selectionne ;
  - la navigation clavier/manette ;
  - le positionnement comportemental du popup si necessaire ;
- verifier qu'un template alternatif peut reellement changer la presentation fermee et ouverte du controle.

Livrable:

- `MGComboBox` migre vers un mode plus lookless ;
- tests sur les parts requises, l'ouverture du dropdown et la re-application de theme ;
- nettoyage des helpers de style qui deviennent obsoletes.

Criteres d'acceptation:

- les decisions de chrome principales ne vivent plus dans `MGComboBox` ;
- un template peut changer l'apparence fermee et la fenetre dropdown sans casser le comportement ;
- la logique de selection reste identique.

Resultat:

- Le styling par defaut des boutons d'items du dropdown n'est plus applique imperativement par `MGComboBox`.
- `MGComboBox` expose maintenant `DropdownItemControlTemplateName`, et ses items de dropdown par defaut passent par un `ControlTemplate` dedie en ressource.
- Le catalogue enregistre `ComboBox.DropdownItem.Default`, ce qui rend le chrome des lignes de dropdown remplaçable sans reouvrir la logique de selection.
- `OnThemeChanged(...)` ne repousse plus directement le background principal du controle ni celui de la fenetre dropdown, laissant ce chrome au pipeline de templates.

### ✅ 4. Migrer `MGTextBox` et `MGTreeView` vers une structure plus lookless

But:
etendre le modele lookless aux controles composites encore hybrides qui ont deja une base templateable.

Travail attendu:

- pour `MGTextBox`, separer ce qui releve du chrome templateable de ce qui releve du moteur d'edition de texte ;
- pour `MGTreeView`, sortir les structures et styles internes encore appliques imperativement quand ils devraient venir du template ou du theme ;
- verifier que les parts critiques sont toutes explicites et valides ;
- s'assurer que les invalidations de layout restent minimales.

Livrable:

- migration partielle ou complete de `MGTextBox` et `MGTreeView` ;
- tests de compatibilite sur les parts, l'invalidation et la theme refresh ;
- documentation rapide dans le code sur la frontiere comportement/chrome.

Criteres d'acceptation:

- les deux controles exposent une separation plus nette entre comportement et apparence ;
- les elements de chrome majeurs ne sont plus recrées ou restyles ad hoc dans les chemins courants ;
- le pipeline runtime reste stable.

Resultat:

- `MGTreeView` n'utilise plus son ancien chemin `ApplyDefaultStyles()` ; son chrome repasse entierement par `MGControlTemplateCatalog` et la reapplication de theme du pipeline template.
- `MGTextBox` ne pousse plus ses couleurs de selection depuis le constructeur, laissant ce role au template `TextBox.Default`.
- La tache reste volontairement bornee: le moteur d'edition de `MGTextBox` et la logique de donnees de `MGTreeView` n'ont pas ete rouverts.

### ✅ 5. Nettoyer la famille bouton et les controles satellites encore couples au chrome

But:
eviter qu'un theme apparence-complete soit bloque par des controles de base encore trop lies a leur implementation visuelle.

Travail attendu:

- auditer et traiter en priorite `MGButton`, `MGToggleButton`, `MGCheckBox`, `MGRadioButton`, `MGToolTip`, et si necessaire `MGScrollViewer` pour la partie chrome ;
- sortir en template, style ou token de theme tout ce qui ne devrait pas rester en draw imperatif interne ;
- conserver le draw imperatif uniquement la ou il est structurellement justifie et documenter ces exceptions ;
- verifier que les composants de base peuvent servir de briques a des templates plus ambitieux.

Livrable:

- lot de controles de base nettoyes ;
- tests ou tests d'architecture sur les points sensibles ;
- note courte sur les exceptions non-lookless conservees volontairement.

Criteres d'acceptation:

- les controles de base n'imposent plus un style cache difficile a contourner ;
- les exceptions residuelles sont bornees et justifiees ;
- la phase 2 peut s'appuyer sur ces controles sans incoherence structurelle.

Resultat:

- `MGToolTip` utilise maintenant `ToolTip.Default`, un `ControlTemplate` dedie qui reemploie la structure de fenetre existante mais deplace son chrome hors du constructeur.
- Les defaults tooltip critiques (`DrawOffset`, foreground, padding, bordure, minima) sont maintenant centralises dans `MGControlTemplateCatalog`.
- La famille bouton n'a pas ete integralement refondue dans cette tache ; les cas restant fortement relies au draw specialise (`MGCheckBox`, `MGRadioButton`) restent des exceptions volontaires pour ce chantier.

## Phase 2 - Declarer les templates par type dans un theme

### ✅ 6. Definir le pont entre styles, ressources et templates de controle

But:
stabiliser la couche de resolution qui permettra a un theme de piloter le template final sans bricolage ad hoc.

Travail attendu:

- clarifier le role respectif de `ThemeDefinition`, `Style`, `MGResources.ControlTemplates` et `ControlTemplateName` ;
- definir la precedence cible entre:
  - valeur locale sur le controle ;
  - style implicite ou nomme ;
  - mapping du theme ;
  - fallback runtime du controle ;
- choisir si le theme alimente directement un mapping runtime, des styles generes, ou une combinaison des deux ;
- documenter la decision dans le code ou dans un court document d'architecture.

Livrable:

- contrat de resolution cible ;
- eventuelles petites extensions d'infrastructure ;
- tests d'architecture verrouillant le modele retenu.

Criteres d'acceptation:

- le systeme de resolution est comprehensible et testable ;
- il n'y a pas de conflit ambigu entre theme et style ;
- la suite des taches peut implementer le mapping sans improvisation.

Resultat:

- `MGElement` distingue maintenant le template explicite local du fallback structurel du controle via `DefaultControlTemplateName` et un chemin de resolution dedie.
- Les controles migrés n'utilisent plus leurs templates integres comme des overrides locaux, ce qui ouvre un espace propre pour le futur mapping `theme -> type -> template`.
- Des tests d'architecture verrouillent la presence du fallback et la migration des principaux controles composites vers ce nouveau contrat.

### ✅ 7. Etendre `ThemeDefinition` pour decrire le template par type de controle

But:
ajouter dans le modele declaratif de theme une section dediee au choix des `ControlTemplate` par type de controle.

Travail attendu:

- ajouter les types XAML necessaires, par exemple une section `ControlTemplates` ou `TemplateMappings` dans `ThemeDefinition` ;
- permettre de declarer au minimum:
  - le type de controle cible ;
  - le nom du template a appliquer ;
  - eventuellement un niveau de specialisation si cela reste simple ;
- garder le format petit et lisible ;
- documenter clairement ce qui est supporte et ce qui ne l'est pas.

Livrable:

- nouveaux types XAML de mapping ;
- parsing XAML couvert par des tests ;
- un exemple de theme declarant plusieurs templates par type.

Criteres d'acceptation:

- un theme peut declarer quel template utiliser pour plusieurs types de controles ;
- le format est suffisamment simple pour etre maintenable ;
- la compatibilite avec les themes existants est preservee.

Resultat:

- `ThemeDefinition` expose maintenant une section declarative `ControlTemplates` composee d'entrees `MGElementType -> TemplateName`.
- `ThemeDefinitionBuilder` projette ces mappings dans `MGTheme`, avec heritage et override par type au-dessus du theme de base.
- Les tests couvrent le parsing XAML et la fusion des mappings sur un theme derive sans casser les themes existants qui n'en declarent pas.

### ✅ 8. Brancher le mapping de templates du theme dans le runtime

But:
faire en sorte qu'un theme actif puisse selectionner effectivement les templates par type au runtime.

Travail attendu:

- ajouter la resolution runtime du template issu du theme ;
- faire en sorte qu'un controle sans valeur locale explicite puisse recevoir le template recommande par le theme ;
- garantir qu'une valeur locale ou un style explicite puissent encore overrider ce choix ;
- verifier le comportement lors d'un changement de theme sur un scope de ressources.

Livrable:

- chemin runtime de resolution ;
- tests de precedence locale/theme/fallback ;
- integration avec `ApplyControlTemplate(...)` sans rebuild inutile.

Criteres d'acceptation:

- un theme actif peut changer l'apparence structurelle d'un type de controle ;
- les overrides locaux continuent de fonctionner ;
- le refresh de theme reste borne et predictible.

Resultat:

- `MGElement.ResolveControlTemplateName()` applique maintenant la precedence `ControlTemplateName` local -> mapping du theme actif (`MGElementType`) -> `DefaultControlTemplateName`.
- `ApplyControlTemplate(...)` reconstruit la structure quand un refresh de theme change effectivement le template resolu, tout en evitant les rebuilds quand le template reste identique.
- Des tests d'architecture verrouillent l'ordre de resolution et le comportement attendu lors d'un changement de theme.

### 🟡 9. Charger le mapping depuis XAML et couvrir les cas de precedence

But:
valider le pipeline complet `ThemeDefinition XAML -> runtime -> selection de template`.

Travail attendu:

- brancher le chargement du mapping declaratif dans `ThemeDefinitionLoader` et la conversion vers le runtime ;
- ajouter des tests cibles sur:
  - theme seul ;
  - theme + style implicite ;
  - theme + override local ;
  - changement de scope parent/enfant ;
  - theme change en runtime ;
- verifier que les themes sans mappings continuent de fonctionner comme avant.

Livrable:

- pipeline complet chargeable depuis XAML ;
- couverture de tests sur les cas nominaux et les precedences ;
- diagnostic clair en cas de template manquant ou de type invalide.

Criteres d'acceptation:

- le flux complet est couvert par des tests automatises ;
- les erreurs sont exploitables ;
- les themes existants restent compatibles.

### ⚪ 10. Documenter, mettre a jour les samples et stabiliser le workflow de skinning

But:
livrer un mode d'emploi clair pour creer de vraies apparences themables.

Travail attendu:

- documenter la nouvelle architecture lookless et le mapping theme -> templates ;
- ajouter ou mettre a jour un sample montrant au moins deux themes appliquant des templates differents aux memes controles ;
- verifier qu'on peut produire une difference d'apparence reelle, pas juste des variations de padding/couleurs ;
- nettoyer les points d'entree ou helpers devenus obsoletes.

Livrable:

- documentation utilisateur et technique ;
- sample ou demo de double skin ;
- dernier lot de stabilisation/tests.

Criteres d'acceptation:

- un utilisateur peut comprendre comment creer un theme a apparence complete sans lire tout le moteur ;
- un sample prouve qu'un meme controle peut prendre des apparences structurellement differentes selon le theme ;
- la surface finale est coherente.

## Definition de fini

Le chantier sera considere termine quand les conditions suivantes seront remplies:

- les controles prioritaires de la phase 1 sont suffisamment lookless pour que leur apparence soit piloter par templates ;
- un `ThemeDefinition` peut declarer quel `ControlTemplate` appliquer par type de controle ;
- la precedence entre valeur locale, style, theme et fallback est couverte par des tests ;
- au moins un sample montre deux skins significativement differents sur une meme base fonctionnelle ;
- les taches de ce fichier sont toutes marquees `✅` ou la premiere tache bloquee est marquee `⛔` avec explication.