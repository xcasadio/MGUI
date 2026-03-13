# Architecture ThemeDefinition

## Objectif

Introduire une couche declarative de theme en XAML sans exposer directement `MGTheme` au parser XAML.

Le theme runtime reste `MGTheme`.
Le theme declaratif devient `ThemeDefinition`.

## Principe directeur

Le parser XAML ne doit pas construire un `MGTheme` complet directement.
Il doit construire une definition declarative simple, stable et composee d'objets XAML-friendly.

Une etape explicite de conversion transforme ensuite cette definition en `MGTheme`.

La frontiere voulue est donc:

`ThemeDefinition (XAML) -> ThemeDefinitionResolver -> MGThemeBuilder -> MGTheme`

## Pourquoi ne pas parser `MGTheme` directement

- `MGTheme` contient des details runtime qui ne sont pas un bon contrat XAML ;
- une partie de ses membres est encapsulee dans des wrappers ou initialisee par conventions ;
- le parser XAML ne doit pas devenir responsable de la logique de fallback, de merge et de compatibilite built-in ;
- garder une definition intermediaire permet de faire evoluer le runtime sans casser le format declaratif.

## Modele cible

Une definition de theme doit pouvoir exprimer:

- un nom unique ;
- un parent via `BasedOn` ;
- des metadonnees simples ;
- des overrides de palette et de brushes ;
- des overrides de groupes de controles ;
- des overrides de backgrounds par `MGElementType` ;
- les valeurs top-level de `MGTheme` qui ne rentrent pas naturellement dans un groupe ;
- les reglages de docking et de polices.

## Structure recommandee

### Noyau

- `ThemeDefinition`
  - `Name`
  - `BasedOn`
  - `IsBuiltIn`
  - `FontSettings`
  - `Backgrounds`
  - `Window`
  - `Overlay`
  - `ContextMenu`
  - `ContextMenuItem`
  - `ListBox`
  - `ListView`
  - `ComboBox`
  - `TreeViewTemplate`
  - `TabControl`
  - `Docking`
  - `Properties`

### Groupes de controle

Chaque groupe represente une famille de proprietes deja stabilisee dans `MGTheme`.
Les groupes doivent rester proches du contrat public durable, pas du detail d'implementation interne.

### Proprietes top-level

Certaines valeurs de `MGTheme` restent hors groupe, par exemple:

- `ComboBoxDropdownBackground`
- `ComboBoxDropdownItemBackground`
- `ListBoxItemBackground`
- `SelectedTabHeaderBackground`
- `TitleBackground`
- les couleurs de selection du `TextBox`
- les valeurs `TreeView*`
- les brushes de scrollbar, slider, progress bar, etc.

Ces valeurs seront modelisees dans une collection declarative de type `ThemePropertyDefinition`, pour eviter de multiplier des classes purement mecaniques quand la structure n'apporte pas de clarte.

## Choix de design: groupes + proprietes nommees

Le format declaratif combine deux mecanismes:

1. des groupes explicites pour les families de controle durables ;
2. une collection de proprietes nommees pour les valeurs top-level restantes.

Cette approche garde le format lisible tout en evitant une explosion de types miroirs de `MGTheme`.

## Reuse des types XAML existants

Pour rester coherent avec le parser existant, `ThemeDefinition` doit reutiliser les types XAML deja etablis quand c'est pertinent:

- `XAMLColor`
- `FillBrush`
- `BorderBrush`
- `Thickness`

Les visual states ne doivent pas etre representes par `VisualStateFillBrush` directement dans le XAML de theme.
Ils doivent passer par des definitions XAML dediees, converties ensuite en `VisualStateFillBrush` runtime.

## Merge et `BasedOn`

`BasedOn` doit fonctionner par nom de theme.

Regles:

- le parent peut provenir d'un theme deja enregistre dans `MGResources.Themes` ;
- le parent peut aussi provenir d'une autre definition du meme document XAML ;
- le merge est profond pour les groupes et listes nommees ;
- les valeurs explicitement definies par le theme enfant remplacent celles du parent ;
- les valeurs absentes heritent ;
- les references cycliques doivent produire une erreur explicite.

## Conversion vers `MGTheme`

La conversion doit etre centralisee dans un builder dedie.

Strategie:

- partir d'un theme parent clone s'il existe ;
- sinon partir d'un theme de base coherent ;
- appliquer ensuite les groupes et proprietes declaratives ;
- convertir les brushes XAML vers les brushes runtime ;
- conserver les fallbacks runtime a un seul endroit.

Le builder doit etre testable sans parsing XAML.

## Base theme par defaut

Sans `BasedOn`, une `ThemeDefinition` est construite sur une base minimale stable.

Pendant la phase de migration, cette base peut encore s'appuyer sur le built-in `Dark_Blue` pour limiter le risque.
Une fois les built-in migrés, cette base doit provenir des definitions XAML built-in.

## Enregistrement dans les ressources

Le registre runtime officiel reste `MGResources.Themes`.

Le support `ThemeDefinition` ajoute:

- le parsing d'un theme unique ou d'une collection de themes ;
- la conversion vers `MGTheme` ;
- l'enregistrement automatique du resultat dans `MGResources.Themes`.

`ThemeName` continue de fonctionner sans changement de semantique.

## Chargement XAML

Deux formes de document doivent etre supportees:

- un fichier contenant une seule `ThemeDefinition` ;
- un fichier contenant une collection de themes nommes.

Le chargeur doit resoudre les themes du document dans un ordre compatible avec `BasedOn`, puis les enregistrer dans `MGResources`.

## Compatibilite descendante

Compatibilite a conserver pendant la migration:

- `MGResources.AddTheme(string, MGTheme)` reste supporte ;
- `GetThemeOrDefault` reste l'API de resolution ;
- `Window.Theme` et `ThemeName` restent valides ;
- `new MGTheme(BuiltInTheme.X, ...)` peut etre preserve via un adaptateur temporaire.

## Migration des built-in

Les built-in a conserver sont:

- `Dark_Blue`
- `Light_Gray`

Plan:

1. retranscrire ces deux themes dans des fichiers XAML `ThemeDefinition` ;
2. faire charger les built-in via ces fichiers ;
3. supprimer les autres variantes built-in et le code mort associe.

## Hors scope volontaire

Ce chantier ne cherche pas a:

- rendre toutes les classes runtime parseables en XAML ;
- introduire un moteur de theme generique arbitraire par reflection ;
- remplacer `MGResources` par un resource dictionary WPF-like complet ;
- exposer les details internes de precedence des wrappers `ThemeManaged*` au format declaratif.

## Critere de succes

Le chantier sera considere reussi si:

- un theme peut etre defini, charge et applique depuis XAML ;
- `BasedOn` est deterministe et testable ;
- `ThemeName` continue de fonctionner ;
- `Dark_Blue` et `Light_Gray` deviennent des definitions XAML ;
- `MGTheme` reste un objet runtime, pas un contrat XAML direct.