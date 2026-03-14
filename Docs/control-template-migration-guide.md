# ControlTemplate Migration Guide

## Objectif

Ce guide decrit l'etat cible atteint par la premiere iteration des `ControlTemplate` structurels dans MGUI et la marche a suivre pour migrer un controle composite existant.

## Frontieres de concepts

`ElementTemplate`

- sert a generer un element autonome reutilisable, typiquement pour du contenu ou de l'item templating ;
- ne porte pas de contrat de `TemplatePart` pour un controle ;
- reste approprie pour les scenarii ou l'element produit n'a pas a se brancher sur la logique interne d'un controle composite.

`ControlTemplate` code

- reste la voie la plus directe pour les scenarii perf-sensibles ou quand l'attachement au controle est tres specifique ;
- peut maintenant separer creation de structure, attachement et application des defaults ;
- doit etre privilegie si le controle a besoin d'un attachement bespoke non encore modelisable via le loader XAML seul.

`ControlTemplate` XAML

- decrit un nom de template, un type cible, une racine visuelle optionnelle, des `DetachedRoots` optionnelles et des parts nommees ;
- est charge via `MGResources.LoadControlTemplatesFromXaml(...)` ;
- convient pour definir la structure visuelle et les parts d'un controle sans recompiler du code imperative pour la structure elle-meme.

## Workflow auteur

1. Ecrire un `ControlTemplate` XAML minimal avec une racine et/ou des `DetachedRoots`, puis declarer les `TemplatePart` nommees.
2. Charger le document via `MGResources.LoadControlTemplatesFromXaml(...)`.
3. Assigner `ControlTemplateName` sur le controle cible.
4. Utiliser `UIToolingService.CaptureVisualTree(...)` pour verifier:
   - le template applique ;
   - les parts exposees ;
   - le dernier message d'erreur de template s'il y en a un.

## Etapes de migration d'un controle composite

1. Declarer les `TemplatePart` requises avec `GetRequiredControlTemplateParts()`.
2. Extraire la creation du chrome vers une structure de template.
3. Attacher les parts dans `AttachControlTemplateStructure(...)`.
4. Deplacer les defaults visuels dans le `MGControlTemplateCatalog` ou un template XAML charge via ressources.
5. Conserver la logique metier, input et navigation dans le controle lui-meme.
6. Ajouter des tests d'infrastructure ou de non-regression apres migration.

## Etat de la premiere iteration

- `Window`, `Overlay`, `ComboBox`, `TabControl`, `TreeView` et `TextBox` utilisent des templates structurels du catalogue.
- `ListBox` et `ListView` consomment maintenant leurs templates structurels par defaut depuis l'asset embarque `BuiltInControlTemplates.xaml`.
- Les templates XAML peuvent etre parses, charges dans `MGResources` et instancies en runtime.
- Les templates XAML peuvent maintenant exposer des parts situees hors de la racine principale via `ControlTemplate.DetachedRoots`.
- Le snapshot outillage expose le template applique, les parts presentes et le dernier echec de validation.

## Risques ouverts

- le changement de template structurel en cours de vie d'un controle a composants reste plus couteux que le simple refresh de theme ;
- le loader XAML couvre maintenant les parts hors sous-arborescence unique, mais ne porte toujours pas un DSL complet d'attachement custom pour tous les cas composites ;
- la validation couvre deja les parts, mais pas encore toutes les contraintes comportementales inter-parts.
- les controles derives d'un type deja template (`MGContextMenu` depuis `MGWindow`) doivent tolerer la phase de template de base pendant leur construction.
- les proprietes qui pilotent des parts templatees doivent conserver un etat logique hors-visuel jusqu'a l'attachement des parts.

## Prochaines migrations recommandees

1. `Docking` controls encore hybrides
2. controles composites avec overlays ou fenetres auxiliaires