# Taches ThemeDefinition pour MGUI

## Objectif

Cette liste sert de backlog ordonne pour introduire une definition de theme declarative en XAML, convertie ensuite en `MGTheme` au runtime, sans exposer directement toute la mecanique interne de `MGTheme` au parser XAML.

L'objectif cible est le suivant:

- un objet XAML declaratif de type `ThemeDefinition` ;
- un support de `BasedOn` pour composer un theme a partir d'un autre ;
- une conversion `ThemeDefinition -> MGTheme` ;
- un enregistrement automatique dans `MGResources.Themes` ;
- la conservation de l'usage existant via `ThemeName` ;
- une migration des themes built-in vers des fichiers XAML, en ne conservant que `Dark_Blue` et `Light_Gray`.

## Principes d'execution

- Chaque tache doit etre suffisamment petite pour etre implementee, validee puis committee seule.
- L'agent doit creer un commit entre chaque tache terminee.
- Preserver la compatibilite descendante tant que la migration n'est pas explicitement finalisee.
- Ne pas rendre `MGTheme` brut entierement pilotable par le parser XAML.
- Favoriser une couche de definition declarative puis une couche de conversion runtime explicite.
- Ajouter les tests au fil de l'eau, avant les migrations les plus destructrices.

## Legende de statut

- ✅ termine
- ⬜ a faire

## Ordre de priorite

### 1. ✅ Definir l'architecture de `ThemeDefinition`

But:
figer la frontiere entre le modele declaratif XAML et l'objet runtime `MGTheme`.

Travail attendu:

- rediger un document d'architecture court pour `ThemeDefinition` ;
- definir les sections de haut niveau supportees: base, palette, brushes, groupes de controle, docking, font settings ;
- definir ce qui reste interne a `MGTheme` et ce qui devient declaratif ;
- definir la strategie de compatibilite pour `ThemeName` et `MGResources.Themes`.

Critere d'acceptation:

- la separation `definition declarative` / `theme runtime` est explicite ;
- le modele cible est suffisamment precis pour guider l'implementation sans reecriture majeure.

### 2. ✅ Introduire les types XAML de definition de theme

But:
creer les types bindables necessaires pour representer un theme declaratif sans reutiliser directement `MGTheme`.

Travail attendu:

- ajouter `ThemeDefinition` et les sous-types associes pour les groupes majeurs ;
- modeliser les sections minimales: metadonnees, `BasedOn`, couleurs, brushes, window, overlay, context menu, list box, list view, combo box, tree view, tab control, docking, font settings ;
- garder ces types parseables en XAML et simples a valider.

Critere d'acceptation:

- les types compilent et sont instanciables via le parser XAML ;
- la structure declarative ne depend pas des wrappers internes de `MGTheme`.

### 3. ✅ Definir la regle de fusion `BasedOn`

But:
permettre a un theme declaratif d'heriter d'un autre sans ambiguite.

Travail attendu:

- definir la resolution d'un parent via nom de theme ou reference explicite selon ce qui est retenu ;
- implementer la fusion des sections partielles ;
- definir les comportements sur valeurs absentes, overrides et cycles.

Critere d'acceptation:

- un theme partiel peut overrider un parent de facon deterministe ;
- les cycles ou references invalides remontent une erreur exploitable.

### 4. ✅ Ajouter la conversion `ThemeDefinition -> MGTheme`

But:
convertir la definition declarative en theme runtime exploitable par les controles existants.

Travail attendu:

- ajouter un convertisseur ou builder dedie ;
- mapper explicitement palette, brushes, groupes de controle et docking vers `MGTheme` ;
- centraliser les valeurs par defaut manquantes au lieu de disperser des fallbacks ;
- garantir qu'un theme converti se comporte comme un `MGTheme` natif.

Critere d'acceptation:

- un `ThemeDefinition` complet produit un `MGTheme` fonctionnel ;
- la conversion est testable independamment du parser XAML.

### 5. ✅ Ajouter l'enregistrement automatique des themes definis en XAML

But:
rendre les themes declaratifs discoverables par le pipeline de ressources existant.

Travail attendu:

- ajouter un mecanisme de chargement ou d'enregistrement dans `MGResources.Themes` ;
- definir comment un document XAML de theme est nomme, charge et expose ;
- veiller a ce que `ThemeName` continue de resoudre via le registre existant ;
- conserver la possibilite d'ajouter un `MGTheme` en code pour compatibilite.

Critere d'acceptation:

- un theme defini en XAML peut etre resolu via son nom ;
- les usages existants de `ThemeName` continuent de fonctionner sans adaptation large.

### 6. ✅ Introduire le chargement XAML de themes comme ressources de premier niveau

But:
permettre de declarer proprement un ou plusieurs themes dans un fichier ou package de ressources.

Travail attendu:

- definir le format d'un fichier XAML de themes ou d'une collection de themes ;
- ajouter le support de parsing et de chargement depuis fichier ;
- permettre l'integration dans le pipeline de ressources desktop ou sample ;
- documenter le format retenu dans le code si necessaire.

Critere d'acceptation:

- un fichier XAML peut enregistrer plusieurs themes nommes ;
- le chargement est suffisamment simple pour remplacer les declarations en C# dans les samples.

### 7. ✅ Ajouter les tests de parsing, fusion et conversion

But:
stabiliser la nouvelle couche avant toute migration des themes existants.

Travail attendu:

- ajouter des tests sur parsing de `ThemeDefinition` ;
- ajouter des tests sur `BasedOn` ;
- ajouter des tests sur conversion vers `MGTheme` ;
- ajouter des tests sur enregistrement dans `MGResources.Themes` ;
- verifier les erreurs de references invalides et de cycles.

Critere d'acceptation:

- la couche `ThemeDefinition` est couverte hors rendu visuel ;
- les regressions de resolution et de fusion sont detectables rapidement.

### 8. ✅ Migrer les samples pour consommer un theme XAML nomme

But:
valider le pipeline complet sur un cas reel avant de toucher aux built-in.

Travail attendu:

- remplacer au moins un sample qui ajoute aujourd'hui un theme en C# par un chargement depuis XAML ;
- verifier que `ThemeName` fonctionne toujours sans changement cote document UI ;
- reduire le code de bootstrapping theme dans les samples concernes.

Critere d'acceptation:

- un sample existant charge et applique un theme defini hors C# ;
- la demonstration couvre chargement, registre de themes et resolution par nom.

### 9. ✅ Introduire les fichiers XAML des themes built-in conserves

But:
sortir les deux themes de reference du code imperative et les replacer dans des definitions declaratives.

Travail attendu:

- creer les fichiers XAML pour `Dark_Blue` et `Light_Gray` ;
- retranscrire leurs valeurs actuelles dans `ThemeDefinition` ;
- verifier que leur rendu reste coherent avec le comportement attendu ;
- organiser ces fichiers dans un emplacement stable du repo.

Critere d'acceptation:

- `Dark_Blue` et `Light_Gray` existent comme definitions XAML de reference ;
- la source de verite de ces deux themes n'est plus une branche imperative dans `MGTheme`.

### 10. ✅ Rebrancher le chargement built-in sur les definitions XAML

But:
faire des built-in conserves de vrais themes charges depuis XAML et convertis au runtime.

Travail attendu:

- adapter le bootstrap des themes built-in pour charger les fichiers XAML ;
- conserver les points d'entree publics existants si possible ;
- verifier que `new MGTheme(BuiltInTheme.X, ...)` reste supporte ou fournir un adaptateur de compatibilite clair ;
- limiter la logique imperative residuelle a l'orchestration du chargement.

Critere d'acceptation:

- les built-in `Dark_Blue` et `Light_Gray` sont effectivement charges depuis XAML ;
- le code runtime ne duplique plus leurs valeurs de theme en dur.

### 11. ✅ Supprimer les autres built-in et nettoyer l'API de compatibilite

But:
aligner le code avec le nouveau perimetre voulu des themes de demonstration.

Travail attendu:

- retirer les built-in non conserves ;
- nettoyer les branches de code et valeurs mortes associees ;
- ajuster les samples, docs et tests impactes ;
- conserver une transition acceptable si une compatibilite temporaire est jugee necessaire.

Critere d'acceptation:

- seuls `Dark_Blue` et `Light_Gray` restent exposes comme built-in supportes ;
- le code lie aux autres variantes ne subsiste pas sans justification.

### 12. ✅ Documenter le format et la migration des themes

But:
permettre a un utilisateur ou a un agent de creer un theme sans lire le coeur du moteur.

Travail attendu:

- documenter la structure d'un fichier `ThemeDefinition` ;
- documenter `BasedOn`, l'enregistrement, le chargement et `ThemeName` ;
- documenter la migration depuis les themes codes en C# ;
- inclure un exemple minimal et un exemple plus complet.

Critere d'acceptation:

- la creation d'un nouveau theme declaratif est faisable depuis la documentation seule ;
- la migration des usages built-in et sample est explicite.

## Ordre d'execution resume

1. architecture `ThemeDefinition`
2. types XAML de definition
3. fusion `BasedOn`
4. conversion vers `MGTheme`
5. enregistrement dans `MGResources.Themes`
6. chargement XAML de themes
7. tests
8. migration sample
9. definitions XAML de `Dark_Blue` et `Light_Gray`
10. rebranchement des built-in sur XAML
11. suppression des autres built-in
12. documentation et migration

## Note finale

Le succes de ce chantier ne se mesurera pas au fait de pouvoir instancier `MGTheme` dans le parser. Il se mesurera a la capacite de declarer, composer, charger et faire evoluer un theme en XAML sans exposer inutilement les details internes du runtime theming.

Etat actuel:

- l'architecture `ThemeDefinition` est documentee ;
- le parser XAML charge `ThemeDefinition` et `ThemeDefinitionsDocument` ;
- `BasedOn` est resolu depuis le document courant, les ressources runtime et les built-in ;
- la conversion vers `MGTheme` est centralisee dans un builder dedie ;
- `MGResources` peut charger et enregistrer automatiquement des themes depuis XAML ;
- un sample a ete migre sur un theme XAML nomme ;
- les built-in conserves `Dark_Blue` et `Light_Gray` proviennent de `MGUI.Core/UI/Themes/BuiltInThemes.xaml`.