# Taches : nom d'element en double, diagnostic dedie du loader

## Objectif

Un document XAML dont la racine est `Window` et qui pose `Name` sur un element d'un template d'item ne se charge pas, et le loader ne rend qu'un `XamlLoaderDiagnosticCode.ParseFailure` dont le message est celui du dictionnaire .NET (`An item with the same key has already been added. Key: ItemLabel`), sans ligne ni colonne. Le but est de rendre cet echec explicable a qui edite du XAML :

- le registre de noms d'une `MGWindow` continue de refuser deux elements du meme nom (choix de l'auteur), mais il leve une exception dediee qui nomme le nom en double, les deux elements et, quand elle est connue, la position source de la declaration ;
- un nom declare deux fois dans le document est detecte des l'analyse, en mode `Strict`, sous un code dedie `DuplicateElementName`, avec la ligne et la colonne de la seconde declaration ;
- l'echec qui n'apparait qu'a l'instanciation (un template clone une seule declaration) remonte lui aussi sous `DuplicateElementName`, avec la position de la declaration du template, au lieu d'un `ParseFailure` nu.

Decisions : ADR-0013 (ecrite en T0, statut `Proposed`, passee `Accepted` en T5).

## Historique du fichier

- 18 septembre 2026 : bug rapporte par l'auteur avec sa reproduction (racine `Window`, `ListBox` avec `ContentTemplate` portant `Name="ItemLabel"`, trois items). Discovery en lecture seule par la session principale sur `xaml-editor` `093b274` : chaine complete du defaut tracee dans le code, execution du test existant `XamlEditorSelectionTests.AListItemTemplateClick_SelectsTheSharedNode_AndAdornsOnlyTheClickedItem` (vert), balayage de tous les `*.xaml` du depot a la recherche de noms en double. Trois questions groupees a l'auteur, puis redaction de ce plan. Aucun code n'est ecrit avant approbation.

## Decisions de l'auteur (18 septembre 2026)

- D1. Branche de base : `develop`. Le defaut est dans `MGUI.Core` et casse aussi le runtime hors editeur ; toutes les branches recoivent `develop`. Travail sur `fix/duplicate-element-name`, partie de `develop` `e633fea`. Ni push ni merge.
- D2. Le registre de noms d'une `MGWindow` continue de lever devant un doublon. Ni tolerance « le premier gagne », ni exclusion des clones de template. Seul le diagnostic est ameliore.
- D3. Un nom vraiment declare deux fois dans le document devient un diagnostic dedie `DuplicateElementName`, fatal en mode `Strict`, sans changement en mode `Compatibility`.

## Consequence de D2, a garder en tete

Le document de la reproduction **reste non chargeable**. Ce plan ne le fait pas passer : il fait dire pourquoi il ne passe pas, ou, dans le texte du message, quoi changer (retirer le `Name` du template, ou n'en generer qu'un item). C'est bien ce que demande la premiere piste de la demande (« un code et un message dedies nommant le nom en double et l'element d'ou il vient, si possible avec la position source de la declaration du template »).

## Points a valider par l'auteur (propositions de l'agent)

- P1. **Type d'exception.** Nouveau type public `MGDuplicateElementNameException : InvalidOperationException` dans `MGUI.Core.UI` (fichier `MGUI.Core/UI/MGDuplicateElementNameException.cs`), porte par le registre. Membres : `Name`, `ExistingElement`, `NewElement` (`MGElement`), `LineNumber`, `LinePosition` (`int`, 0 quand inconnue). Les deux entiers sont nommes exactement ainsi parce que `XamlLoaderDiagnostics.TryGetExceptionIntProperty` les relit par reflexion sans que rien d'autre soit a ecrire. Le seul type d'exception propre au depot aujourd'hui est `XamlLoaderException` ; `XamlLoaderException` n'est pas reutilise ici, car le registre leve aussi pour une affectation `element.Name = "x"` faite par code, sans le moindre XAML.
- P2. **Deux causes distinguees dans le message.** Les deux elements portent leur `XamlSourcePosition` dans `MGElement.Metadata` quand ils viennent d'un document (`UIToolingService.XamlSourcePositionMetadataKey`). Si les deux positions ont le **meme `Ordinal`**, il s'agit d'une seule declaration clonee par un template ; sinon, de deux declarations. Le message le dit :
  - meme ordinal : `Duplicate element name 'ItemLabel'. The name is declared once, at line 5, column 30, inside a template that generates more than one element (TextBlock); a name may only identify one element of a window. Remove the name from the template content, or generate a single item.`
  - ordinaux differents, ou pas de position : `Duplicate element name 'ItemLabel'. A TextBlock is already registered under that name in this window; the second element is a TextBlock declared at line 9, column 14.` La partie « declared at ... » disparait quand la position est absente (element cree par code, part de template de controle, element de theme).
- P3. **Ou lever.** `MGWindow.Element_Added` et `MGWindow.Element_NameChanged` verifient l'entree existante avant `Add` et levent `MGDuplicateElementNameException`. `ElementsByName.Add` n'est plus jamais atteint avec une cle deja presente : c'est le seul changement de comportement du registre, et il est purement sur le message. `Element_Removed` et le reste du registre ne bougent pas.
- P4. **Validation d'analyse (D3), portee.** Nouvelle methode `XamlLoaderDiagnostics.ValidateUniqueElementNames(markup, source, documentKind, mode)`, appelee par `XAMLParser.ParseDefinition` **seulement**, jamais par `ParseObjectDefinition`. Raison verifiee : `MGUI.Core/UI/Templates/BuiltInControlTemplates.xaml` repete legitimement `PART_Border`, `PART_ScrollViewer` et vingt-deux autres noms d'une definition de `ControlTemplate` a l'autre d'un meme `ControlTemplatesDocument`, et ces noms vivent dans `MGElement.TemplateParts`, une table par element, pas dans le registre d'une fenetre. Les documents de templates gardent donc leur validation propre (`ValidateRequiredTemplateParts`).
- P5. **Regle exacte de la validation.** Sur le `XDocument` deja lu avec les infos de ligne : on ne retient qu'un element XML dont le nom local se resout en un DTO derivant de `Element` (meme predicat que `UIToolingService.TryResolveXamlElementType` : `Style`, `ControlTemplate`, `VisualStateDefinition`, `TemplatePart` sont donc hors jeu), portant un attribut `Name` non vide et sans namespace. Le second element a porter un nom deja vu leve `DuplicateElementName` a **sa** ligne et **sa** colonne, avec la ligne de la premiere declaration dans le message. Les commentaires XML et les valeurs d'attribut ne comptent pas, c'est acquis par la lecture `XDocument`.
- P6. **Le balayage du depot ne rend aucun faux positif.** `MGUI.Samples/Dialogs/Debugging/Debug2.xaml` porte deux `<Button Name="TestBtn">` mais dans des commentaires XML ; `MGUI.Samples/Features/DataBindings.xaml` repete `CheckBox1` et `SampleCheckBox` dans la **valeur** d'un attribut `Text` (extrait de XAML montre a l'ecran) ; `MGUI.Samples/Features/AnimationDemo.xaml` repete `Name="Hover"` sur des `VisualStateDefinition`, qui ne derivent pas d'`Element`. Aucun de ces trois cas n'est vu par la regle P5. Les samples sont de toute facon charges en mode `Compatibility`.
- P7. **L'echec d'instanciation devient un diagnostic situe.** `XAMLParser.Load<T>` et `XAMLParser.LoadRootWindow` passent leur appel a `ToElement` dans `XamlLoaderDiagnostics.Execute`, comme l'analyse l'est deja. Effet en mode `Strict` seulement (`Execute` rend la main telle quelle en `Compatibility`). `XamlLoaderDiagnostics.Classify` recoit une branche typee `MGDuplicateElementNameException -> DuplicateElementName`, placee avant la passe par message, donc independante de la langue de la machine (regle posee par `4b51ca6`). La ligne et la colonne viennent des proprietes de l'exception, deja relues par reflexion.
- P8. **Portee assumee de P7.** En mode `Strict`, toute autre defaillance survenue pendant l'instanciation (et non pendant l'analyse) devient elle aussi un `XamlLoaderException` classe, au lieu de remonter nue. C'est une amelioration pour le seul consommateur du mode strict present sur la branche de base, `MGXAMLDesigner.RefreshParsedContent` (`MGXAMLDesigner.cs:278-307`), qui sait deja mettre en forme un `XamlLoaderException` (code, message, source, ligne) et qui, sans ce changement, se rabat sur `ex.Message` nu. C'en est une aussi pour `XamlPreviewHost.Reparse` de l'editeur, dont la branche `catch (Exception)` fabrique aujourd'hui un `ParseFailure` sans chemin de fichier ni position -- mais ce fichier n'existe que sur `xaml-editor` (`093b274`), pas sur `develop` : c'est un effet de la fusion a venir, pas une tache de ce plan (O1). C'est malgre tout un changement de surface. Aucun test existant n'affirme le contraire (les seules assertions `ParseFailure` du depot portent sur du XML mal forme et sur une markup extension mal formee, deux echecs d'analyse).
- P9. **Nouveau membre d'enumeration.** `XamlLoaderDiagnosticCode.DuplicateElementName`, ajoute en fin d'enumeration pour ne deplacer aucune valeur existante.
- P10. **Un defaut jumeau est a verifier, pas a supposer.** La meme collision devrait se produire pour deux controles qui partagent un `ControlTemplate` XAML dont une part porte un `Name` : `ControlTemplateLoader.BuildStructure` appelle `Definition.Root.ToElement(...)`, donc `ApplyBaseSettings` pose le `Name` sur chaque instanciation, et `MGElement.ApplyControlTemplate` relaie l'exception (`catch { LastControlTemplateError = ...; throw; }`). Non verifie a ce stade. T1 ecrit le test ; s'il est vert sans changement, le cas est simplement documente comme non atteint, et le test reste en garde-fou.

## Etat des lieux (`develop` `e633fea`)

**Corrige le 18 septembre 2026 par l'execution de T1.** La chaine ecrite avant approbation, tiree de la seule lecture du code, etait fausse sur le point 3 et sur le point 4. Ce que les probes executees montrent, et ce que les tests de T1 fixent :

1. `MGWindow` s'abonne a son propre `OnDirectOrNestedContentAdded` (`MGWindow.cs:1560-1562`) ; `Element_Added` indexe le nom, `Element_NameChanged` aussi (`MGWindow.cs:1973-1976`, `:2038-2041`). Verifie.
2. `ContentTemplate.GetContent` copie le DTO en profondeur a chaque item (`Templates.cs:31`) et `Element.ApplyBaseSettings` repose `Element.Name = Name` sur chaque clone (`Element.cs:607-610`). Verifie : trois `MGTextBlock` nommes `ItemLabel` existent bien dans l'arbre, et les trois portent la meme `XamlSourcePosition` (meme `SourceName`, meme `Ordinal`).
3. **La collision n'a pas lieu pendant le chargement.** `UIToolingService.LoadPreview` rend un arbre complet et sain, meme en mode `Strict` : ni `ParseDefinition` ni `ToElement` ne levent. Elle a lieu **a l'attachement**, quand ce sous-arbre est pose dans un hote de contenu : `MGContentHost.InvokeContentAdded` annonce alors, un par un, tous les elements du sous-arbre attache (`MGContentHost.cs:46-67`).
4. **Ce qui decide, ce n'est pas la racine, c'est l'enveloppement.** Ce parcours est `TraverseVisualTree(IncludeSelf: false, includeComponents: true, false, false)`, et `IncludeSelf: false` saute aussi *les composants de l'element attache lui-meme* (`MGElement.cs:6123-6124`). Or `MGListBox` tient toute sa structure d'items dans ses composants : `MGTextBlock[ItemLabel] < MGBorder < MGStackPanel < MGScrollViewer < MGBorder < MGListBox[Options]`, et `MGListBox.GetChildren()` rend zero enfant. Donc :
   - racine `Window`, ou racine `StackPanel`, ou n'importe quel element qui **enveloppe** la `ListBox` : la `ListBox` n'est plus « soi », ses composants sont parcourus, les trois `ItemLabel` sont annonces, la fenetre hote leve. Mesure : les deux cas levent.
   - racine `ListBox` : le parcours saute les composants de la `ListBox` elle-meme, aucun `ItemLabel` n'est annonce, rien n'est indexe, le document se charge. Mesure : aucune levee, `TryGetElementByName("ItemLabel")` faux.
   - `XAMLParser.LoadRootWindow` sur la reproduction : aucune levee non plus, parce qu'une fenetre racine n'est jamais attachee *dans* un hote de contenu. C'est pourquoi `XamlSourcePositionTests.ItemTemplate_Clones_KeepTheirOwnTemplateNodeOrdinal_ForEveryGeneratedItem`, qui utilise exactement ce motif avec `Name="ItemLabel"`, est vert depuis toujours.
   L'explication par le docking (« les groupes d'onglets court-circuitent la chaine ») etait une hypothese empruntee a la note X1b d'ADR-0010 : elle est inutile, le docking n'y est pour rien.
5. Consequence directe sur T3 : l'exception ne sort pas du loader, donc envelopper `ToElement` dans `XamlLoaderDiagnostics.Execute` **ne la classera pas**. C'est `presenter.SetContent(root)` qui leve, apres le retour de `LoadPreview`. Dans l'editeur, `ReplaceRoot` est appele a l'interieur du `try` de `XamlPreviewHost.Reparse`, ce qui explique exactement le `ParseFailure` sans position que l'auteur a vu. Voir « Points ouverts » O3.
6. P10 est repondu, et par la negative : deux controles qui partagent un `ControlTemplate` XAML dont la racine porte `Name="SharedPartRoot"` ne collisionnent pas. Les parts instanciees ne portent pas ce nom comme `MGElement.Name` ; elles vivent dans `MGElement.TemplateParts`. Aucun second defaut latent de ce cote.

Machinerie deja en place et reutilisee :


- `XamlSourcePosition(SourceName, Ordinal, LineNumber, LinePosition)` est posee par `XAMLParser.StampSourcePositions` dans **les deux** modes, puis recopiee dans `MGElement.Metadata` par `Element.ApplyBaseSettings` (`Element.cs:594-599`) ; `UIToolingService.TryGetXamlSourcePosition` la relit (`UIToolingService.cs:55`, `:68`). Un clone de template garde l'`Ordinal` de son noeud declare : c'est le signal exact qui distingue « une declaration clonee » de « deux declarations ».
- `XamlLoaderDiagnostics.ValidateKnownElementNames` lit deja le markup en `XDocument` avec `LoadOptions.SetLineInfo` et leve avec ligne et colonne.
- `XamlLoaderDiagnostics.Classify` classe d'abord par type d'exception non parseur, puis par type d'exception parseur ; `TryGetExceptionIntProperty` relit `LineNumber` et `LinePosition` par reflexion sur toute exception qui les expose en `int` (`> 0` sinon `null`).
- `UIToolingService.TryResolveXamlElementType(localName, out dtoType)` expose deja le predicat « ce nom local est un DTO derivant d'`Element` » (`UIToolingService.cs:170-181`).

Consommateurs du mode `Strict` hors tests : `MGXAMLDesigner.RefreshParsedContent` (`MGXAMLDesigner.cs:278`, met en forme `Code`, `Message`, `SourceName`, `Line`) et, sur les branches de l'editeur, `XamlPreviewHost.Reparse`. `ControlTemplateLoader` et `ThemeDefinitionLoader` ont leur propre usage du mode, non touche.

## Taches

Une tache a la fois, un commit par tache terminee, plan mis a jour dans le meme commit.

### ✅ T0. ADR-0013, statut `Proposed`

Ecrire `Docs/decisions/0013-duplicate-element-name.md` : le registre de noms d'une `MGWindow` est une contrainte d'unicite et le reste (D2) ; il leve un type dedie qui explique laquelle des deux causes s'applique ; le loader gagne `DuplicateElementName`, rendu a l'analyse pour deux declarations et a l'instanciation pour une declaration clonee ; portee de la validation limitee aux documents d'element et de fenetre (P4) ; changement de surface de P8. Referencer l'ADR depuis `Docs/decisions/README.md` si ce fichier tient un index.

Fin : le fichier existe, il est reference, aucun code touche.

### ✅ T1. `MGDuplicateElementNameException` et le registre

`MGUI.Core/UI/MGDuplicateElementNameException.cs` (P1), leve par `MGWindow.IndexElementName`, appele par `Element_Added` et `Element_NameChanged` (P3), message construit selon P2. `MGElement.Name` documente la regle.

Tests, `MGUI.Tests/Xaml/DuplicateElementNameTests.cs` (nouveau fichier), six au lieu des quatre prevus : les deux ajoutes fixent l'asymetrie mesuree au point 4 de l'etat des lieux, qui est ce qui fait charger ou echouer le meme markup.

1. la reproduction exacte de l'auteur, chargee puis **attachee** dans un presentateur de la fenetre hote (la sequence d'une preview) : `MGDuplicateElementNameException`, `Name` = `ItemLabel`, `LineNumber` et `LinePosition` = la declaration du `TextBlock` dans le template (ligne et colonne recalculees depuis le texte, independamment du parseur teste), message contenant `ItemLabel`, `TextBlock`, « declared once » et « instantiated », et **ne** contenant pas la formule des deux declarations ;
2. le meme markup dont la racine **est** la `ListBox` : aucune levee, et rien d'indexe ;
3. le meme markup enveloppe dans un `StackPanel` : leve comme la racine `Window` ;
4. deux `<Button Name="Same">` declares dans un meme document, charges par `LoadRootWindow` : seconde forme du message, position de la **seconde** declaration, ordinaux differents ;
5. deux `Name` identiques poses par code dans une meme fenetre, par le chemin du renommage : meme exception, sans position, message sans « declared at line » ;
6. P10 : deux controles partageant un `ControlTemplate` XAML dont la racine porte un `Name`. Aucune collision ; le test prouve qu'il n'est pas vide (les deux controles portent bien ce template et tiennent chacun leurs parts) et fixe que le nom declare d'une part n'atteint jamais l'index de la fenetre.

Preuve par mutation faite : en neutralisant la distinction d'ordinal de P2, les tests 1 et 3 echouent, les autres passent.

Validation : `dotnet test` complet, 2790/2790.

### ✅ T2. `DuplicateElementName` a l'analyse, mode `Strict`

`XamlLoaderDiagnosticCode.DuplicateElementName` ajoute en fin d'enumeration (P9), `XamlLoaderDiagnostics.ValidateUniqueElementNames` (P4, P5), appelee par `XAMLParser.ParseDefinition` juste apres `ValidateKnownElementNames`, jamais par `ParseObjectDefinition`.

Tests ajoutes a `MGUI.Tests/Architecture/XamlLoaderDiagnosticsTests.cs` :

1. deux `<Button Name="Same">` dans un document d'element, mode `Strict` : `XamlLoaderException`, code `DuplicateElementName`, ligne 4 colonne 10 (la **seconde** declaration), message nommant `Same`, `Button` et la ligne de la premiere ;
2. le meme document en mode `Compatibility` : `ParseElementDefinition` rend le DTO sans rien lever, l'analyse seule ne construisant aucun element ;
3. un `Name` unique declare dans un `ContentTemplate` ne declenche rien : le document le declare une fois ;
4. `Style Name="Shared"` a cote d'un `Button Name="Shared"` ne declenche rien (P5) ;
5. `BuiltInControlTemplates.xaml` lu depuis le depot et charge par `ControlTemplateLoader.ParseDefinitions` en mode `Strict` : vert, et le test verifie d'abord que le fichier contient bien `PART_Border` pour ne pas passer a vide (P4).

Test ajoute a `MGUI.Tests/Architecture/XamlLoaderDiagnosticCultureTests.cs` : le meme cas sous `en-US` et `fr-FR` rend le meme code et la meme position, la regle ne lisant aucun message de bibliotheque.

Validation : `dotnet build MGUI.sln` sans erreur, `dotnet test` complet 2797/2797.

### ⚠️ T3. L'echec remonte situe -- BLOQUEE, decision de l'auteur attendue

**La premisse de T3 est refutee par la mesure faite en T1.** P7 disait : « l'echec d'instanciation devient un diagnostic situe », en enveloppant `ToElement` dans `XamlLoaderDiagnostics.Execute`. Or l'echec n'est pas un echec d'instanciation : `UIToolingService.LoadPreview` rend un arbre sain, et c'est `presenter.SetContent(root)` qui leve, apres le retour du loader (point 3 de l'etat des lieux, fixe par le test 1 de T1, qui appelle `LoadPreview` hors du bloc qui capture l'exception).

Consequence : envelopper `ToElement` **ne classera pas** l'exception de la reproduction. T3 tel qu'approuve ne livre pas son critere d'acceptation A2. Dans l'editeur, `ReplaceRoot` est appele a l'interieur du `try` de `XamlPreviewHost.Reparse` : c'est la branche `catch (System.Exception)` qui l'attrape, d'ou le `ParseFailure` sans position rapporte par l'auteur.

La question, et les options, sont en « Points ouverts » O3. Rien n'est ecrit pour T3 tant que l'auteur n'a pas tranche.

### ⏳ T4. Documentation

- `Docs/editor-architecture.md`, section « Diagnostics » : les codes que l'editeur peut afficher, la regle des noms (un nom identifie un element d'une fenetre), les deux moments ou le doublon est detecte, et ce que l'auteur du XAML doit changer ; section « Limites connues » : un `Name` dans un template d'item empeche le chargement des qu'il genere plus d'un element.
- `Docs/scenario-validation-index.md` : etendre l'invariant de `SCN-MARKUP-001` au nom en double.
- `Docs/decisions/0013-duplicate-element-name.md` : passer `Proposed` a `Accepted`, ajouter la section des decisions prises pendant la livraison (resultat de P10 notamment).

Aucun test.

### ⏳ T5. Suite complete et rapport

`dotnet build` de la solution, puis `dotnet test MGUI.Tests` en entier. Rapport de fin de tache. Ni push, ni merge.

## Validation minimale

- A1. La suite `MGUI.Tests` est verte en entier a la fin de T5, et apres chaque tache pour le filtre qui la concerne.
- A2. La reproduction exacte de la demande rend, en mode `Strict`, un `XamlLoaderDiagnostic` de code `DuplicateElementName`, avec une ligne et une colonne non nulles et un message qui contient `ItemLabel`.
- A3. Aucun `*.xaml` du depot ne devient invalide : les samples restent en `Compatibility`, et `BuiltInControlTemplates.xaml` est couvert par un test en `Strict` (T2, cas 5).
- A4. Validation manuelle de l'auteur : coller la reproduction dans l'editeur XAML et lire le diagnostic. A faire apres la fusion de `develop` dans `xaml-editor`, puisque l'editeur complet n'est pas sur `develop` ; la tache correspondante reste 🧪 jusque la.

## Points ouverts

- O1. Ce plan n'ecrit que dans des fichiers presents sur `develop`. Trois suites appartiennent a `xaml-editor` (`093b274`) et restent a poser au moment de la fusion, par qui l'auteur designera :
  - `Docs/decisions/0010-xaml-editor-v1.md` et la version `xaml-editor` de `Docs/editor-architecture.md`, plus avancees que celles de `develop` : y ajouter la note « limite levee », sur le modele de celle de `4b51ca6` deja presente en ADR-0010 ;
  - `MGUI.Editor/Preview/XamlPreviewHost.cs` : le commentaire de sa branche `catch (System.Exception)` decrit une frontiere que T3 deplace (P8) ; le corriger, sans changer le code ;
  - la validation manuelle A4, qui demande l'editeur complet.
- O2. P10 est repondu par la negative (point 6 de l'etat des lieux, test 6 de T1) : deux controles partageant un `ControlTemplate` XAML nomme ne collisionnent pas, les noms declares des parts n'atteignant jamais l'index de la fenetre. Aucun second defaut latent, rien a corriger.
- O3. **Bloquant, decision de l'auteur.** L'exception est levee a l'attachement, hors du loader : aucun emballage interne au loader ne peut la transformer en diagnostic situe. Trois facons d'obtenir quand meme un `DuplicateElementName` avec sa ligne et sa colonne dans l'editeur :
  - **A (recommandee).** Ajouter un point d'entree public qui transforme une exception quelconque en `XamlLoaderDiagnostic` classe et situe -- en pratique rendre publique la fabrique que `XamlLoaderDiagnostics.CreateException(source, documentKind, exception)` utilise deja, plus la branche typee `MGDuplicateElementNameException -> DuplicateElementName` dans `Classify`. Un hote qui attrape une exception en posant sa preview obtient alors le meme diagnostic que le loader. Cote `xaml-editor`, l'adoption tient en une ligne dans le `catch (System.Exception)` de `XamlPreviewHost.Reparse`. Cote `develop`, `MGXAMLDesigner.RefreshParsedContent` en profite pareillement. Surface publique ajoutee : une methode.
  - **B.** Faire porter l'attachement par le loader (une surcharge de `LoadPreview` qui recoit le conteneur cible et pose le contenu a l'interieur d'`Execute`). Plus intrusif : cela deplace la responsabilite de l'attachement, et l'editeur doit changer d'appel.
  - **C.** Ne garder que P7, et accepter que la reproduction reste un `ParseFailure` dans l'editeur, avec toutefois le message complet de `MGDuplicateElementNameException` (nom, type, « declared once ... line 5, column 18 »). Minimal, mais l'objectif « code dedie et position » n'est atteint qu'a moitie.
  Dans les trois cas, P7 reste utile pour les vraies defaillances d'instanciation (un convertisseur ou un setter qui leve pendant la construction) : il a ete approuve et peut etre livre avec A, B ou C.


## Risques

- R1. P8 elargit ce qui devient un `XamlLoaderException` en mode `Strict`. Aucun test du depot n'affirme le contraire aujourd'hui, mais un consommateur hors depot (l'editeur CasaEngine) qui filtre sur un type d'exception precis verrait le changement. L'ADR le note.
- R2. Le message de P2 s'appuie sur l'egalite des `Ordinal`. Si les positions n'ont pas ete posees (divergence de comptage, garde-fou de `StampSourcePositions`), la seconde forme du message est utilisee : elle reste juste, seulement moins precise. Test 3 de T1.
