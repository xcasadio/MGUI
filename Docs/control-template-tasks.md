# ControlTemplate XAML Tasks

## Objectif

Definir un plan d'implementation pour faire evoluer MGUI vers une architecture ou les `ControlTemplate` peuvent etre references et, a terme, definis depuis le XAML, sans casser les contraintes d'un framework UI pour jeu video:

- pipeline runtime previsible ;
- cout d'invalidation controle ;
- pas de GC evitable sur les hot paths ;
- compatibilite avec l'architecture actuelle de `MGResources`, `MGElement`, `MGControlTemplate` et `MGElementTemplate` ;
- migration progressive sans refonte massive en une seule etape.

Ce document est destine a un agent IA implementeur.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- Faire un commit git apres chaque tache terminee.
- Mettre a jour le statut de la tache dans ce fichier avant chaque commit.
- Utiliser les icones de statut ci-dessous dans le titre de chaque tache.
- Si une tache est bloquee, marquer la tache avec l'icone adaptee, expliquer le blocage juste sous la tache, puis s'arreter.
- Ne pas faire de refactor massif hors perimetre.
- Conserver les APIs publiques existantes tant qu'une tache ne demande pas explicitement de les etendre.
- Ajouter ou adapter les tests a chaque etape quand c'est pertinent.

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- ⛔ bloque

## Cible architecturale

La cible n'est pas un clone de WPF. La cible raisonnable pour MGUI est la suivante:

- un controle peut recevoir un `ControlTemplateName` depuis le XAML comme aujourd'hui ;
- les templates de controle sont resolus via `MGResources` ;
- un template peut definir la structure visuelle d'un controle, pas seulement appliquer des defaults sur des parts deja existantes ;
- les template parts requises restent explicitement nommees et valides ;
- les proprietes de chrome majeures passent par une resolution stable entre local, template, style et theme ;
- la recreation de structure visuelle est limitee aux cas necessaires et n'est pas un chemin chaud de frame ;
- les controles composites critiques migrent progressivement vers ce modele.

## Contraintes de design

- Ne pas introduire un dependency property system complet si une couche plus petite suffit.
- Garder `MGControlTemplate` si possible, en l'etendant avant de le remplacer.
- Distinguer clairement `ElementTemplate` de `ControlTemplate`.
- Preserver la possibilite de construire des templates par code pour les cas avancés ou perf sensibles.
- Traiter les templates XAML comme des assets de definition, pas comme du code execute a chaque draw.
- Eviter toute recreation repetitive de templates au runtime normal.

## Etat de depart observe

- `MGControlTemplate` existe deja mais agit surtout comme un applicateur de defaults sur `TemplateParts` existantes.
- Le XAML peut deja renseigner `ControlTemplateName` sur un element.
- `MGResources` sait resoudre des `ControlTemplate` par nom.
- Les controles composites construisent encore largement leur structure visuelle dans leurs constructeurs.
- Les groupes de theme XAML peuvent deja definir de nombreux tokens de chrome, mais pas encore de structure visuelle de controle en XAML.

## Ordre de commits attendu

Format recommande des commits:

1. `controltemplate: complete task 1 template architecture contract`
2. `controltemplate: complete task 2 xaml template definitions`
3. `controltemplate: complete task 3 control template resource loading`
4. `controltemplate: complete task 4 runtime template instantiation`
5. `controltemplate: complete task 5 template part validation`
6. `controltemplate: complete task 6 precedence and invalidation bridge`
7. `controltemplate: complete task 7 migrate window and overlay`
8. `controltemplate: complete task 8 migrate combo box and tab control`
9. `controltemplate: complete task 9 diagnostics and tooling`
10. `controltemplate: complete task 10 docs and stabilization`

## Taches

### ✅ 1. Definir le contrat runtime de ControlTemplate

But:
faire evoluer `MGControlTemplate` d'un simple applicateur de defaults vers un contrat capable de supporter une structure visuelle instanciable.

Travail attendu:

- auditer la forme actuelle de `MGControlTemplate`, `MGControlTemplateContext` et `MGElement.ApplyControlTemplate(...)` ;
- introduire un modele cible minimal distinguant:
  - phase de creation de structure ;
  - phase d'attachement / enregistrement des parts ;
  - phase d'application des defaults de chrome ;
- definir les invariants de cycle de vie: quand un template est instancie, quand il est re-applique, et ce qui est autorise en theme refresh ;
- preparer une API qui reste compatible avec les templates catalogues existants.

Livrable:

- extension du contrat `MGControlTemplate` ou ajout d'un type adjacent ;
- tests d'architecture sur le nouveau contrat ;
- documentation courte dans le code sur la difference entre structure et defaults.

Criteres d'acceptation:

- le code existant compile sans migration immediate de tous les controles ;
- un template peut exprimer structure et defaults sans ambiguite ;
- l'ancien catalogue peut continuer a fonctionner via une voie de compatibilite.

Resultat:

- `MGControlTemplate` expose maintenant des phases distinctes de creation de structure, d'attachement et d'application des defaults, tout en conservant le constructeur historique base sur `Action<MGControlTemplateContext>`.
- `MGControlTemplateStructure` formalise la sortie de la phase structurelle et sert de point d'ancrage pour la suite de la migration runtime/XAML.
- Les tests d'infrastructure couvrent la compatibilite legacy et le nouveau contrat lifecycle.

### ✅ 2. Ajouter des definitions XAML de ControlTemplate

But:
introduire une representation XAML des `ControlTemplate` sans encore migrer tous les controles.

Travail attendu:

- creer les types XAML necessaires, par exemple un document de templates, une definition de `ControlTemplate`, et un mecanisme pour declarer la racine visuelle et les `TemplateParts` ;
- definir une grammaire raisonnable, simple et compatible avec le parser XAML actuel ;
- permettre de declarer au minimum:
  - le nom du template ;
  - le type cible du controle ;
  - la structure visuelle racine ;
  - les elements nommes pouvant servir de parts ;
  - les valeurs locales de base utiles au chrome ;
- documenter explicitement ce qui n'est pas encore supporte si certaines fonctions sont differees.

Livrable:

- nouveaux types dans la couche XAML ;
- tests de parsing sur une definition simple de `ControlTemplate` ;
- exemple minimal de template XAML parseable.

Criteres d'acceptation:

- un `ControlTemplate` simple peut etre decrit en XAML sans code imperative additionnel ;
- le parser produit une definition exploitable par le runtime ;
- le format reste assez petit pour un framework jeu.

Resultat:

- Une nouvelle couche XAML `ControlTemplatesDocument` / `ControlTemplateDefinition` / `TemplatePartDefinition` existe desormais, avec des aliases markup publics permettant d'ecrire directement `<ControlTemplate>` et `<TemplatePart>`.
- Le format couvre le nom du template, le type cible, la racine visuelle, des mappings explicites de parts et une zone `Notes` pour documenter les limitations de premiere iteration.
- Des tests de parsing valident un template unitaire et un document multi-templates.

### ✅ 3. Charger et enregistrer les ControlTemplate XAML dans MGResources

But:
brancher les templates XAML sur `MGResources` comme des ressources de premier ordre.

Travail attendu:

- ajouter le chargement des definitions de `ControlTemplate` depuis XAML vers `MGResources` ;
- garder le fallback hierarchique deja existant ;
- definir comment les templates XAML cohabitent avec les templates enregistres par code ;
- couvrir les conflits de nom et les overrides de scope.

Livrable:

- API de chargement depuis `MGResources` ;
- tests de lookup parent/enfant et d'override ;
- validation d'un template XAML chargeable a partir d'une chaine ou d'un document.

Criteres d'acceptation:

- `MGResources` peut resoudre un template defini en XAML par son nom ;
- un scope enfant peut surcharger un template parent ;
- le comportement est stable et testable.

Resultat:

- `ControlTemplateLoader` parse des documents ou templates unitaires, puis les convertit en `MGControlTemplate` structurels en s'appuyant sur la racine XAML et les mappings de parts.
- `MGResources.LoadControlTemplatesFromXaml(...)` enregistre ces templates comme des ressources de premier ordre, avec remplacement explicite a nom egal et conservation du fallback hierarchique existant.
- Des tests couvrent le chargement nominal et l'override parent/enfant d'un template XAML.

### ✅ 4. Instancier la structure d'un ControlTemplate au runtime

But:
faire en sorte qu'un controle puisse recevoir une structure visuelle depuis son template, pas seulement des defaults.

Travail attendu:

- ajouter la logique d'instanciation de structure dans `MGElement.ApplyControlTemplate(...)` ou dans une couche dediee ;
- gerer proprement le parentage, l'enregistrement des parts, et l'attachement a la hierarchie visuelle ;
- distinguer re-application de theme et reconstruction structurelle ;
- limiter les rebuilds aux cas ou le template change reellement.

Livrable:

- pipeline runtime d'instanciation ;
- tests sur un controle minimal capable de reconstruire sa structure a partir d'un template ;
- voie de compatibilite pour les controles anciens qui continuent de creer leur structure eux-memes.

Criteres d'acceptation:

- un controle de test peut consommer un template XAML structurel ;
- la re-application d'un theme n'entraine pas automatiquement une reconstruction complete si elle n'est pas necessaire ;
- les parents/enfants et parts sont coherents apres instanciation.

Resultat:

- `MGElement.ApplyControlTemplate(...)` sait maintenant creer une structure templatee une seule fois hors refresh de theme, l'enregistrer comme structure appliquee, puis ne re-jouer que les defaults pendant les refreshs.
- `MGElement.AttachControlTemplateStructure(...)` fournit un hook runtime pour les controles, avec un chemin generique immediat pour les `MGSingleContentHost`.
- Les tests d'infrastructure epinglent l'existence du hook runtime et la separation explicite entre creation structurelle et refresh de theme.

### ✅ 5. Valider les TemplateParts et les erreurs de template

But:
eviter qu'un template invalide casse silencieusement le controle.

Travail attendu:

- formaliser la declaration des parts requises par les controles migrables ;
- enrichir les erreurs de template manquant ou de type incorrect ;
- lister les parts disponibles dans les diagnostics ;
- couvrir les cas de template partielle ou incorrecte.

Livrable:

- mecanisme de validation des parts ;
- erreurs runtime explicites ;
- tests de templates invalides.

Criteres d'acceptation:

- un template incomplet echoue avec un message exploitable ;
- un template correct passe sans logique implicite fragile ;
- les diagnostics aident reellement un auteur XAML.

Resultat:

- Un contrat explicite `MGControlTemplatePartRequirement` existe desormais et `MGElement` valide centralement les parts presentes avant l'application des defaults.
- `MGWindow`, `MGOverlay`, `MGComboBox` et `MGTabControl` declarent leurs parts attendues, ce qui prepare les migrations structurelles suivantes.
- Les messages d'erreur de `GetRequiredPart<T>` et de la validation centrale listent le type attendu, le type reel et les parts disponibles pour accelerer le diagnostic.

### ✅ 6. Faire le pont precedence / template / invalidation

But:
integrer le template structurel dans une resolution de valeurs previsible.

Travail attendu:

- definir quelles valeurs du template comptent comme source `Template` ;
- s'assurer qu'une valeur locale garde la priorite sur la valeur issue du template ;
- clarifier l'interaction entre theme, template et style pour les proprietes de chrome majeures ;
- ajouter les invalidations minimales necessaires pour les proprietes layout-affecting injectees par template.

Livrable:

- tests de precedence impliquant valeur locale, template et theme ;
- documentation dans le code sur l'ordre de resolution cible ;
- adaptation minimale des chemins d'invalidation.

Criteres d'acceptation:

- une valeur locale ne se fait pas ecraser par un template ;
- une valeur de template peut surcharger un default/theme quand c'est attendu ;
- les invalidations restent localisees et coherentes.

Resultat:

- Les valeurs appliquees depuis un `ControlTemplate` sont maintenant stockees avec un `UIResolvedValue<T>` marque comme source `Template`, ce qui aligne le runtime avec le modele de precedence deja documente.
- `MGControlTemplateContext.ApplyTemplateValue(...)` centralise l'application de valeurs templatees et declenche une invalidation layout uniquement pour les invalidations de type measure/arrange/structure.
- Le chemin historique `ApplyThemeDefault(...)` reste disponible mais delegue au nouveau chemin template-aware pour conserver la compatibilite du catalogue existant.

### ✅ 7. Migrer Window et Overlay vers des templates plus structurels

But:
valider l'architecture sur deux controles composites centraux mais relativement contenus.

Travail attendu:

- faire de `MGWindow` et `MGOverlay` des premiers consommateurs serieux de structure visuelle issue de template ;
- reduire le chrome construit en dur dans leurs constructeurs ;
- conserver les parts importantes et les comportements existants.

Livrable:

- migration de `Window` ;
- migration de `Overlay` ;
- tests de non-regression sur les parts attendues et le theme refresh.

Criteres d'acceptation:

- `Window` et `Overlay` peuvent etre skinnes via template sans reimplementer leur logique ;
- les boutons de fermeture et bordures restent fonctionnels ;
- le cout runtime reste raisonnable.

Resultat:

- Les templates catalogue `Window.Default` et `Overlay.Default` sont devenus structurels: ils creent maintenant leurs parts principales avant attachement au controle.
- `MGWindow` et `MGOverlay` attachent ces parts structurelles a leurs composants existants via `AttachControlTemplateStructure(...)`, ce qui preserve leur logique de fermeture, border rendering et theme refresh.
- Les tests de catalogue verrouillent que ces deux templates passent bien par le chemin structurel.

### ✅ 8. Migrer ComboBox et TabControl vers des templates plus structurels

But:
attaquer deux controles hybrides majeurs qui beneficieront le plus d'un vrai `ControlTemplate`.

Travail attendu:

- sortir autant que possible la structure dropdown/chrome de `MGComboBox` ;
- sortir autant que possible la structure d'headers de `MGTabControl` ;
- conserver les comportements clavier/manette/souris existants ;
- eviter les recreations inutiles de wrappers si seules des valeurs visuelles changent.

Livrable:

- migration de `ComboBox` ;
- migration de `TabControl` ;
- tests de navigation et de refresh de theme sur ces deux controles.

Criteres d'acceptation:

- les deux controles peuvent utiliser une structure templatee plus riche ;
- le couplage au theme en constructeur diminue nettement ;
- aucune regression evidente sur l'usage courant.

Resultat:

- `ComboBox.Default` et `TabControl.Default` sont maintenant des templates structurels du catalogue, au lieu d'un simple applicateur de defaults sur des parts construites en constructeur.
- `MGComboBox` et `MGTabControl` attachent leurs parts structurelles lors du cycle de template, puis conservent leurs comportements existants de dropdown, navigation, headers et refresh de theme.
- Les assertions d'infrastructure couvrent desormais ces deux templates structurels en plus de `Window` et `Overlay`.

### ✅ 9. Ajouter diagnostics et outillage pour les ControlTemplate XAML

But:
rendre le systeme deboguable pour l'editeur et pour les auteurs de templates.

Travail attendu:

- exposer les templates appliques, parts enregistrees et erreurs de validation ;
- etendre l'outillage de visual tree si pertinent ;
- fournir un minimum d'informations sur la source des valeurs de chrome majeures ;
- documenter le workflow auteur pour charger et tester un template XAML.

Livrable:

- outillage ou diagnostics logs ;
- eventuelle extension de `UIToolingService` ;
- documentation courte orientee debug.

Criteres d'acceptation:

- un template applique est identifiable ;
- les parts disponibles sont inspectables ;
- un echec de template est rapide a diagnostiquer.

Resultat:

- `MGElement` expose maintenant le nom du template applique et le dernier message d'erreur de validation/template observe.
- `UIToolingService.CaptureVisualTree(...)` embarque ces informations dans `UIVisualTreeSnapshot`, avec la liste des parts enregistrees et leur type runtime.
- Les tests d'outillage verrouillent cette nouvelle surface de diagnostic pour l'editeur et les outils internes.

### ✅ 10. Stabilisation, documentation et guide de migration

But:
terminer la sequence avec une base durable pour d'autres migrations.

Travail attendu:

- nettoyer les points de compatibilite temporaires qui peuvent l'etre sans risque ;
- documenter clairement la difference entre `ElementTemplate`, `ControlTemplate` code et `ControlTemplate` XAML ;
- ecrire un mini guide de migration pour convertir un controle composite existant ;
- mettre a jour ce fichier avec les resultats finaux et les risques restants.

Livrable:

- documentation finale ;
- liste des risques ouverts ;
- base stable pour les migrations futures de `TextBox`, `ListBox`, `ListView`, `TreeView` et autres controles.

Criteres d'acceptation:

- un autre agent peut reprendre le travail sans re-decouverte majeure ;
- les concepts et les frontieres d'architecture sont clairs ;
- la sequence de migration future est explicite.

Resultat:

- Un guide dedie `Docs/control-template-migration-guide.md` documente la frontiere entre `ElementTemplate`, `ControlTemplate` code et `ControlTemplate` XAML, ainsi que le workflow de migration d'un controle composite.
- Le fichier de suivi a ete tenu a jour tache par tache avec les resultats et les commits correspondants.
- Validation finale executee: `dotnet test .\MGUI.Tests\MGUI.Tests.csproj --filter Architecture`. Un echec subsiste hors perimetre sur `BoxGeometryBuilderTests.EquivalentNormalizedShapes_ReuseCachedGeometry` (attendu 2, obtenu 8), non modifie ici.

## Risques a surveiller pendant l'implementation

- explosion de la complexite de precedence si le template XAML est introduit avant la stabilisation de la resolution runtime ;
- reconstruction trop large de la structure visuelle lors des theme refresh ;
- retention memoire via parts ou abonnements dynamiques orphelins ;
- trop grande ambition sur la premiere iteration du format XAML de template ;
- confusion entre style declaratif, theme de chrome et template structurel.

## Definition du succes

La mission est reussie si:

- un `ControlTemplate` simple peut etre defini en XAML ;
- `MGResources` peut le charger et le resoudre ;
- au moins quelques controles composites peuvent utiliser une structure issue du template ;
- les parts requises sont validees proprement ;
- le cout runtime reste compatible avec MGUI ;
- le fichier de suivi a bien ete mis a jour tache par tache avec un commit entre chaque etape.

## Taches supplementaires

### ✅ 11. Faire une passe de revue ciblee sur les risques runtime restants des templates structurels

But:
identifier les fragilites runtime encore presentes apres la premiere vague de migrations structurelles, en se concentrant sur les risques d'instanciation, de re-attachement, de validation de parts et de comportements hybrides restants.

Travail attendu:

- auditer les chemins runtime actuels des templates structurels et des controles encore hybrides ;
- identifier les patterns de risque restants, en particulier:
  - proprietes qui supposent l'existence immediate des parts templatees ;
  - changements de template en cours de vie ;
  - retention de composants internes et d'evenements lors des re-attachements ;
  - cohabitation entre templates structurels et controles encore construits en dur ;
- produire une note de synthese exploitable pour les migrations suivantes.

Livrable:

- note de revue ciblee des risques runtime restants ;
- mise a jour du guide de migration si necessaire ;
- filets de securite de test si un pattern recurrent doit etre epingle.

Criteres d'acceptation:

- les principaux risques runtime restants sont explicitement nommes et relies a du code reel ;
- la note permet d'orienter les migrations suivantes sans re-decouverte importante ;
- les recommandations restent compatibles avec les contraintes runtime de MGUI.

Resultat:

- Une note ciblee `Docs/control-template-runtime-risk-review.md` recense les risques runtime restants les plus probables: acces aux parts avant attachement, conflit de template de base pendant la construction, cohabitation hybride entre parts manuelles et parts structurelles, et limites actuelles du changement de template sur les controles composites.
- Le guide de migration a ete enrichi avec les deux garde-fous confirmes par les regressions recentes: tolerer la phase de template de base sur les derives et conserver un etat logique hors-visuel pour les proprietes qui pilotent des parts.
- La revue conclut que `ListBox` et `ListView` sont la prochaine vague la plus sure car ils exposent deja des part names claires et concentrent encore une grande partie du chrome construit en dur.

### 🟡 12. Migrer la prochaine vague de controles, par exemple ListBox et ListView

But:
appliquer l'architecture structurelle a la prochaine vague de controles composites fortement hybrides.

Travail attendu:

- migrer `MGListBox` vers un template structurel avec parts explicites et attachement runtime ;
- migrer `MGListView` vers un template structurel avec parts explicites et attachement runtime ;
- conserver les comportements de selection, scroll, headers, templates d'items et refresh de theme ;
- ajouter les tests de non-regression les plus utiles sur ces deux controles.

Livrable:

- migration de `ListBox` ;
- migration de `ListView` ;
- mise a jour du catalogue et des tests d'infrastructure associes.

Criteres d'acceptation:

- les deux controles consomment une structure de template au runtime ;
- le chrome construit en dur dans leurs constructeurs diminue nettement ;
- les parts requises et l'attachement runtime sont valides et testables.

Resultat:

- a completer.

### ⚪ 13. Remplacer les templates structurels code des controles migres par de vrais assets XAML

But:
sortir la structure visuelle des controles migres du code imperative et la definir via de vrais assets XAML embarques.

Travail attendu:

- creer des assets XAML pour les templates structurels des controles migres ;
- les embarquer dans `MGUI.Core` et les charger pendant l'enregistrement des templates par defaut ;
- conserver l'application des defaults de chrome et la compatibilite runtime ;
- couvrir le chargement des assets et leur resolution par nom.

Livrable:

- assets XAML embarques pour les templates migres ;
- chargement automatique dans le pipeline des templates par defaut ;
- tests de chargement et de presence des templates embarques.

Criteres d'acceptation:

- `ListBox` et `ListView` utilisent des structures templatees venant d'assets XAML ;
- le code catalogue ne contient plus leur structure imperative principale ;
- les assets peuvent etre resolus et instancies via `MGResources`.

Resultat:

- a completer.