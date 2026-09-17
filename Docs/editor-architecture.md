# Architecture de l'editeur XAML

## Objectif

`MGUI.Editor` est un editeur de XAML MGUI : un volet texte, un rendu a chaud du document, la selection des elements et une grille de proprietes dont chaque modification edite le noeud XAML selectionne. Il n'y a pas de manipulation a la souris : la souris ne sert qu'a selectionner.

Note d'etat : cette page decrit l'etat cible du programme [Tasks/xaml-editor-tasks.md](Tasks/xaml-editor-tasks.md). Elle est verifiee contre le code a la tache X7, qui retire cette note. Decisions : [decisions/0010-xaml-editor-v1.md](decisions/0010-xaml-editor-v1.md).

## Portee

- Dans la portee : edition du texte XAML, preview a chaud, diagnostics du loader, selection (arbre, clic, caret), grille de proprietes du noeud, edition d'attributs depuis la grille, ouverture et enregistrement de fichiers.
- Hors portee : completion XAML, editeurs types dans la grille, manipulation a la souris, multi-selection, ajout ou suppression de noeuds hors du texte, preview dans une texture, donnees de conception generees.

## Principes

- Le texte XAML est la seule source de verite. La preview, l'arbre et la grille sont recalcules depuis le texte ; la grille ne produit que des editions de texte au niveau de l'attribut. Il n'existe pas de serialiseur DTO vers XAML.
- Le document est charge sans sanitisation : il declare ses namespaces, comme les fichiers des samples. Les positions rendues par le loader correspondent donc au texte edite.
- Un XAML invalide ne casse rien : la frappe et la coloration continuent, le dernier rendu et le dernier modele de document valides restent en place, l'erreur est marquee dans le texte.
- `MGUI.Core` ne contient aucune logique d'editeur. Il fournit des briques d'outillage generiques et testables ; l'acces aux fichiers vit dans `MGUI.Editor`.
- Tout est pilote par la boucle d'update du desktop : aucun thread, aucun timer systeme.

## Vue d'ensemble

| Projet | Role |
| --- | --- |
| `MGUI.Core` | loader XAML avec positions source, resolution de type d'element, hit test d'outillage, tokenizer et highlighter XAML, `MGRichTextBox`, `MGPropertyGrid`, `MGTreeView`, adorners |
| `MGUI.Editor` | bibliotheque : session, modele de document, preview, selection, source de proprietes, vue composee |
| `MGUI.Editor.Host` | executable minimal (`GameRenderHost`) qui ouvre un fichier dans `XamlEditorView` |
| `MGUI.Tests` | tests des briques de `MGUI.Core` et de `MGUI.Editor` (desktop headless) |

Flux d'une frappe : texte modifie, modele de document re-parse tout de suite, preview marquee sale, re-parse du loader apres le delai, selection retrouvee par ordinal, arbre et grille rafraichis.

Flux d'une edition de grille : valeur validee, edition d'attribut calculee sur le modele de document, `ApplyTextEdit` sur le volet texte, puis le flux d'une frappe.

## Session et vue

`XamlEditorSession` porte l'etat d'un document : texte, `SourceName` (chemin du fichier, ou nom unique pour un document sans fichier), `DesignDataContext`, `IsDirty`, diagnostics, selection, `LoadFile`, `Save`, `ApplyEdit`. Elle n'a aucune dependance graphique au-dela des types de `MGUI.Core`.

Le texte de la session est toujours en LF, car le volet texte normalise les fins de ligne. La fin de ligne d'origine et la presence d'un BOM sont un etat de la session, releve par `LoadFile` et restitue par `Save` : un fichier non modifie est reecrit a l'octet pres. Le modele de document, les positions du loader et les diagnostics travaillent tous sur le texte LF du volet.

`XamlEditorView` compose l'interface en code (aucun XAML embarque). C'est une classe compositrice, pas un `MGElement` : elle construit contre la fenetre hote cinq volets sans parent, exposes par ses proprietes : `TextPane` (`MGRichTextBox`), `PreviewPane` (un `MGOverlayPanel` qui porte le presentateur de la preview et, au-dessus, la couche d'adorners), `TreePane` (`MGTreeView`), `PropertyPane` (`MGPropertyGrid`) et `DiagnosticsPane` (liste des diagnostics).

Les volets sont heberges par le docking manager de `MGUI.Core`. La vue expose cinq `DockableDefinition` (`Dockables` : « XAML », « Preview », « Document », « Properties », « Diagnostics », d'identifiants `xaml-editor.text`, `.preview`, `.tree`, `.properties`, `.diagnostics`) dont le `ContentFactory` rend le volet, et `CreateDockHost()`, qui rend un `MGDockHost` avec son registre et un layout par defaut : « XAML » a gauche de « Preview », « Diagnostics » dessous, « Document » au-dessus de « Properties » a droite. L'utilisateur redimensionne les volets, les regroupe en onglets, les epingle en auto-hide et les detache en fenetre flottante. En V1 les volets ne sont pas fermables et « Preview » ne flotte pas.

Regles que le docking impose aux taches de l'editeur :

- un volet ne porte pas de `Name` : l'index des noms d'une `MGWindow` n'est pas tenu a jour par les groupes d'onglets du docking ; on atteint un volet par les proprietes de la vue ;
- un volet peut etre cache : le contenu d'un onglet inactif ou d'un tiroir auto-hide ferme est detache de l'arbre visuel (`Parent == null`) et garde ses derniers bounds ; `MGElement.OnParentChanged` signale chaque attache et detache, dans tous les etats (onglet, fenetre flottante, tiroir) ; un volet qui redevient actif en recoit trois (attache, detache, re-attache) : un gestionnaire doit etre idempotent et lire le `Parent` final ;
- une vue n'est hebergee que par un seul hote de docking a la fois.

Integration dans un hote : creer une session, creer la vue contre une fenetre de l'hote, renseigner `DesignDataContext` si l'hote dispose d'un modele de donnees. Un hote simple fait `window.SetContent(view.CreateDockHost())` : c'est ce que fait `MGUI.Editor.Host`, et rien de plus. Un hote qui a deja son docking (CasaEngine) enregistre `view.Dockables` dans son propre `MGDockHost` : pas de docking imbrique.

## Positions source du loader

`XAMLParser.ParseDefinition` lit le markup avec `XamlXmlReader` (`ProvideLineInfo`) et ecrit les objets avec `XamlObjectWriter`, positions transmises au writer. Pendant la lecture, la position de chaque noeud `StartObject` dont le type derive de `Element` est notee, ainsi que chaque instance derivant de `Element` vue par `BeforePropertiesHandler` ; apres la boucle, la k-ieme position est posee sur la k-ieme instance. Le meme code sert les deux branches de compilation (`System.Xaml` sous `UseWPF`, Portable.Xaml sinon).

`XamlSourcePosition(SourceName, Ordinal, LineNumber, LinePosition)` :

- `SourceName` : `XamlDocumentSource.DisplayName` du document charge, la meme valeur que `XamlLoaderDiagnostic.SourceName` ; il sert a rejeter une position qui ne vient pas du document edite ;
- `Ordinal` : rang de l'element, en ordre du document, parmi les elements XML dont le nom se resout en un DTO derive de `Element`. Les elements de propriete (`Button.Content`) et les objets qui ne sont pas des elements (styles, setters, brushes, templates, bindings) ne comptent pas ;
- `LineNumber`, `LinePosition` : en base 1 ; `LinePosition` designe le premier caractere du nom de l'element, juste apres `<`, dans le markup charge.

`Element.ApplyBaseSettings` recopie la position dans `MGElement.Metadata` ; `UIToolingService.TryGetXamlSourcePosition` la relit. Regles :

- `(SourceName, Ordinal)` identifie un noeud du document, pas une instance. Un `ContentTemplate` declare dans le document est clone en profondeur a chaque item et chaque DTO clone garde sa position : les racines generees portent celle du noeud de contenu du template, leurs descendants celle de leur propre noeud dans le template. Un noeud, N elements ;
- une part de template de controle, un element de theme et un element fabrique par un `TypeConverter` (`Content="texte"`) ne portent aucune position ;
- les positions ne sont posees qu'apres la boucle de lecture : si le nombre d'instances et le nombre de positions divergent, aucune n'est posee.

`UIToolingService.TryResolveXamlElementType(localName, out dtoType)` expose la resolution de nom du loader (alias compris) ; l'editeur s'en sert pour calculer les memes ordinaux de son cote et pour connaitre le type de DTO d'un noeud.

## Modele de document

`XamlDocumentModel` est construit depuis le texte du volet par `XDocument` avec informations de ligne et espaces preserves. `TryParse` ne leve jamais : une erreur XML est rendue avec sa ligne et sa colonne, et le modele precedent reste courant.

Un `XamlDocumentNode` porte le nom local, le type de DTO, l'ordinal, son parent, ses enfants, l'etendue de sa balise ouvrante, son etendue complete (de la balise ouvrante a la balise fermante, ou la balise auto-fermante) et ses attributs. Un `XamlDocumentAttribute` porte le nom, la valeur decodee, l'etendue du nom, l'etendue de la valeur brute entre guillemets et le caractere de guillemet. Les etendues sont des `MGTextRange` dans le texte LF du volet.

`XamlAttributeEdit` calcule les editions, sans effet de bord : remplacer une valeur, inserer ` Nom="valeur"` avant la fin de la balise ouvrante, supprimer un attribut avec le blanc qui le precede. Les valeurs sont echappees (`&`, `<`, guillemet utilise). Le reste de la ligne n'est jamais reformate.

## Preview a chaud

`XamlPreviewHost` charge le texte par `UIToolingService.LoadPreview` en mode strict, sans sanitisation, avec `DesignDataContext`. `RequestRefresh` marque le document sale ; le re-parse a lieu sur l'update de la fenetre de l'editeur une fois `DebounceDelay` ecoule (250 ms par defaut). Une rafale de frappes produit un seul re-parse.

- Echec du chargement : la racine precedente reste affichee, le `XamlLoaderDiagnostic` est publie.
- Racine `Window` : affichee dans le volet et ancree a son coin haut-gauche ; `Left` et `Top` du XAML ne placent pas la preview. Une `MGWindow` se mettant en page a sa propre position, l'hote la repositionne apres chaque mise a jour de la preview et a chaque changement des bounds du volet (separateur de docking, regroupement en onglet, redimensionnement). Elle garde sa taille declaree ; ce qui depasse est rogne par le volet. Quand le volet « Preview » est cache (onglet inactif, tiroir ferme), la racine `Window` est masquee et le re-parse continue ; elle est re-affichee et re-ancree quand le volet retrouve un parent.
- `IsInteractive` (faux par defaut) : a faux, le sous-arbre de la preview ne recoit aucune entree, ce qui laisse le clic a la selection ; a vrai, la preview se comporte comme a l'execution et le clic ne selectionne plus.

Un re-parse reconstruit l'arbre de la preview : l'etat d'execution qu'il contenait (defilement, cases cochees) est perdu a chaque modification.

## Volet texte

`XamlEditorTextPane` compose `MGRichTextBox` avec :

- `XamlSyntaxHighlighter` (`MGUI.Core/UI/TextEditing/Xaml/`), bati sur `XamlTokenizer`, tolerant : ponctuation, nom d'element, nom d'attribut, chaine, commentaire, markup extension ;
- les marqueurs d'erreur : un decorateur de highlighter ajoute un span souligne a la position de chaque diagnostic (le jeton a cette position, sinon la fin de la ligne). Les diagnostics sont en base 1 et `MGTextBuffer` en base 0 ; la colonne est en plus corrigee sur les lignes qui contiennent le litteral `\n`, que le loader remplace par `&#x0a;` ;
- la liste des diagnostics, contenu du dockable « Diagnostics » ; un clic sur une entree place le caret.

`MGRichTextBox.ApplyTextEdit` empile un etat d'undo : une edition venue de la grille s'annule par `Ctrl+Z` comme une frappe. Il n'y a qu'une pile d'undo, celle du volet texte.

## Selection

`XamlEditorSelection` porte le noeud selectionne et un element representant dans la preview. Trois entrees, une seule selection :

- l'arbre (`MGTreeView`), reconstruit a chaque modele de document valide, etat d'expansion conserve par ordinal ;
- le clic dans la preview, quand `IsInteractive` est faux : `UIToolingService.HitTest(root, screenPoint)` rend l'element le plus profond sous le point, composants compris ; l'editeur remonte ensuite la chaine `Parent` jusqu'au premier element qui porte une position du document, et cette position designe le noeud. Un clic sur une part de template, sur un bouton interne d'un controle ou sur le libelle d'un `<Button Content="OK"/>` selectionne donc le noeud declare qui les possede. Le hit test convertit le point de l'espace ecran vers l'espace non mis a l'echelle et reprend le chemin de contenance de l'entree reelle : l'echelle de la fenetre, le defilement, le clipping et les `RenderTransform` sont pris en compte ; `IsHitTestVisible` est ignore ; a profondeur egale le dernier frere l'emporte. Le hit test est purement geometrique et ne connait pas les positions source ;
- le caret : selectionner un noeud place le caret au debut de sa balise ; dans l'autre sens, `MGUI.Core` ne notifiant pas les deplacements de caret, la selection lit `MGRichTextBox.CaretIndex` a chaque update et, quand l'index a change, selectionne le noeud le plus profond dont l'etendue complete contient le caret. Le sondage est suspendu tant que le texte est invalide et pendant une edition de grille.

Un noeud peut correspondre a plusieurs elements (items d'un template). Cliquer sur l'un d'eux selectionne le noeud du template ; le representant est l'element clique s'il existe encore, sinon le premier en ordre visuel. Un noeud sans element dans la preview reste selectionnable depuis l'arbre et le caret.

Un `MGBoundsAdorner` dans une `MGAdornerLayer` du volet preview encadre le representant. Rien n'est injecte dans l'arbre de la preview. La selection survit a un re-parse par ordinal ; elle est videe si le noeud n'existe plus, et conservee telle quelle tant que le texte est invalide.

## Grille de proprietes

La grille montre le noeud XAML, pas le `MGElement`. `XamlNodePropertySource` implemente `ICustomTypeDescriptor` ; `MGPropertyGrid` n'est pas modifie.

- Lignes : les proprietes publiques et modifiables du type de DTO qui se convertissent depuis une chaine, rangees par `[Category]`. Les collections et les dictionnaires sont exclus ; une propriete de type element declaree en element enfant est en lecture seule.
- Chaque descriptor est de type chaine, quel que soit le type reel de la propriete : la grille ecarte les descriptors d'un type qu'elle ne sait pas editer. Le type reel est garde par la source pour la validation.
- Valeur : la chaine de l'attribut telle qu'ecrite dans le XAML ; chaine vide = attribut absent.
- Ecriture : la chaine est validee par le `TypeConverter` de la propriete ; invalide, elle est refusee, la ligne reprend sa valeur et un message est publie ; valide, elle devient une edition d'attribut. Vider une ligne supprime l'attribut.
- Cycle de vie : une instance de source par selection, mutee a chaque re-parse (la grille se rafraichit) ; une nouvelle instance quand la selection change (la grille se reconstruit). Une grille cachee par le docking ne se rafraichit pas : elle le fait quand `PropertyPane` retrouve un parent (`OnParentChanged`).
- Categorie « Resolved (runtime) », en lecture seule : les cinq valeurs effectives que `UIToolingService.CaptureElementDebugView` expose pour le representant (`Background`, `TextForeground` ou `Foreground`, `BorderBrush`, `BorderThickness`, `Padding`), avec leur source (theme, style implicite, style explicite, valeur locale, etat visuel, animation).

## Diagnostics

Une seule liste par session : les diagnostics du loader (code, message, ligne, colonne), l'erreur XML du modele de document quand le texte n'est pas bien forme, et les refus de la grille. Le loader s'arrete a la premiere erreur : la liste en montre donc une a la fois pour le chargement.

## Tests

- `MGUI.Tests/Xaml/` : positions source (ordinal, ligne, colonne, garde-fou de divergence, parts de template, template d'item).
- `MGUI.Tests/Tooling/` : hit test d'outillage (echelle, defilement, clipping, transform).
- `MGUI.Tests/Text/` : tokenizer, highlighter, undo d'une edition programmee.
- `MGUI.Tests/Editor/` : modele de document et editions d'attribut, preview (debounce, erreur, ancrage d'une racine `Window`, mode interactif), selection, source de proprietes, aller-retour grille vers texte vers preview, session de fichier. Desktop headless, meme motif que les tests d'integration existants ; les fichiers de test vivent dans un dossier temporaire.
- Validation manuelle : scenario `SCN-EDITOR-XAML-001` de [scenario-validation-index.md](scenario-validation-index.md).

## Limites connues

- La selection et la grille demandent un XML bien forme ; pendant une frappe invalide elles restent sur le dernier etat valide.
- Chaque ligne de la grille est une chaine, couleurs comprises ; pas d'editeur d'enum, de couleur ou d'epaisseur. La categorie « Resolved (runtime) » ne couvre que cinq chemins (`Margin` n'en fait pas partie).
- Les styles, ressources et templates ne sont editables que dans le texte ; les parts de template de controle ne sont pas selectionnables.
- Un contenu declare comme chaine (`Content="texte"`) n'a pas de noeud propre : il s'edite comme attribut de son parent.
- Une racine `Window` plus grande que le volet est rognee, ni redimensionnee ni defilable.
- Docking : les volets ne sont pas fermables (le menu contextuel d'un onglet montre quand meme « Close Others » et « Close All », sans effet) ; « Preview » ne flotte pas, car une racine `Window` de preview est une fenetre imbriquee de la fenetre de l'editeur ; un volet flottant re-docke revient en onglet du premier groupe visible, pas a sa place d'origine ; le layout n'est ni sauvegarde ni reinitialisable.
- Pas de completion, pas de gouttiere de numeros de ligne.
- Le document doit declarer ses namespaces ; un fragment sans namespace ne se charge pas.
- L'etat d'execution de la preview est perdu a chaque re-parse.

## Reste a faire

- Completion XAML : [Tasks/richtextbox-autocomplete-tasks.md](Tasks/richtextbox-autocomplete-tasks.md).
- Editeurs types de la grille : [Tasks/propertygrid-tasks.md](Tasks/propertygrid-tasks.md).
- Evenement de deplacement de caret dans `MGUI.Core`, pour remplacer le sondage.
- Integration dans le docking de l'editeur CasaEngine (enregistrer `Dockables` dans son `MGDockHost`).
- Docking de l'editeur : sauvegarde et reinitialisation du layout, volets fermables avec un menu « View », flottement de « Preview », plusieurs documents ouverts.
