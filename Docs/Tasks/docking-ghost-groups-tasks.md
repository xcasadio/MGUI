# Taches docking : groupes fantomes, retour a la place d'origine et disposition persistee

## Objectif

Un panneau qui quitte la disposition ancree (detache en fenetre flottante, masque automatiquement ou ferme) doit revenir a sa place d'origine, sans jamais effondrer la disposition dans un seul groupe d'onglets. Garantie exacte, quel que soit l'ordre des retours : meme groupe (le meme noeud, garde en fantome s'il s'etait vide), memes splits, memes ids et memes ratios. Dans un groupe a plusieurs onglets, l'ordre et l'onglet actif suivent la regle D3 (P11) : ils ne sont identiques que dans les cas decrits en P11. La disposition sauvegardee devient complete (arbre avec ses groupes fantomes, fenetres flottantes, panneaux masques, places memorisees) dans un nouveau format 2.0 sans retrocompatibilite ; un echec de chargement est journalise et laisse l'application hote sur sa disposition par defaut.

Decisions : ADR-0012 (a ecrire en T0, statut Proposed, puis Accepted en T6).

## Historique du fichier

- 17 septembre 2026 : constat d'un verifier en contexte frais sur la branche `xaml-editor` (`c20c9d2`) : flotter puis re-ancrer les volets un par un effondre la disposition dans un seul groupe. Discovery en lecture seule : lecture du modele et de `MGDockHost` par la session principale, quatre eclaireurs sur des surfaces disjointes (affichage, glisser-deposer, cycle de vie des panneaux, consommateurs et tests), faits decisifs recontroles dans le code. Trois series de questions a l'auteur ; redaction du plan. Aucun code n'est ecrit avant l'approbation du plan.
- 17 septembre 2026 (suite) : relectures d'enveloppe en contexte frais. Revue 1 REVISE (garantie « identique dans tout ordre » fausse pour un groupe a plusieurs onglets) : corrigee par P11. Revue 2 REVISE (un fantome n'a pas de visuel, fermeture et reouverture ne rafraichissaient pas l'affichage) : corrigee par P12 et A8. Arret demande par l'auteur, puis reprise ; ajout du cadre commun des tranches et des prerequis. Revue de cloture REVISE : (a) reconstruire `_panelRegistry` dans le setter `LayoutModel` casserait le motif « `LayoutModel` puis `RegisterPanel` » de l'editeur XAML, corrige dans P10 (reconstruction au chargement seulement) et par un test nomme en T5 ; (b) `ShowDockable` sur un panneau flottant creerait un doublon, corrige en T4 (ordre de resolution, test nomme, invariant d'unicite) et en P8 (refus d'un id en double au chargement). Plafond de relecture atteint : la tranche T1 n'a pas encore ete relue ; decision de l'auteur attendue.
- 17 septembre 2026 (suite) : plan approuve par l'auteur tel quel, propositions P1 a P12 comprises, execution immediate en mode AUTO sans relecture de la tranche T1 (choix « Approuver et executer »).

## Decisions de l'auteur (17 septembre 2026)

- D1. La place memorisee d'un panneau est persistee dans la disposition sauvegardee ; nouveau format, sans retrocompatibilite.
- D2. Groupes fantomes a la AvalonDock : un groupe d'onglets vide reste dans l'arbre, cache, tant qu'une place memorisee le reference.
- D3. Si le groupe d'origine existe encore, l'onglet reprend son index d'origine (borne au nombre d'onglets) et devient actif.
- D4. Le masquage automatique est inclus : `UnpinPanel` / `RepinPanel` utilisent la meme restauration.
- D5. La fermeture est incluse : fermer un panneau laisse un fantome, `ShowDockable` le rouvre a sa place, et la place des panneaux fermes est sauvegardee.
- D6. Les fenetres flottantes vivent dans le modele (`DockLayoutModel`) ; l'hote les reflete.
- D7. Ancien format (1.0) ou JSON invalide : l'erreur est journalisee et l'application hote garde sa disposition par defaut. Moyen retenu : `TryLoadLayoutFromJson(json, panelFactory, out diagnostics)` sur le modele de `MGColorPaletteSerializer.TryFromJson` : renvoie false, laisse la disposition courante intacte, ecrit le message en `Debug` et le rend a l'appelant ; `LoadLayoutFromJson` garde son exception.
- D8. « L'editeur qui se lance normalement » designe l'application hote (editeur CasaEngine, hors depot) : MGUI garantit seulement l'echec sans exception et sans toucher la disposition deja en place ; la demo docking est alignee.
- D9. Execution en mode AUTO apres approbation : les taches s'enchainent, un commit chacune, arret seulement sur blocage ; ni push ni merge.

## Points a valider par l'auteur (propositions de l'agent)

- P1. Visibilite structurelle, sans nouvel etat stocke : un groupe d'onglets vide qui n'est pas la racine est cache ; un split dont un enfant est cache (ou nul) affiche seulement l'autre enfant, sans barre de separation, et garde son `SplitRatio` dans le modele ; un split dont les deux enfants sont caches est cache. Si tout l'arbre est cache, l'hote affiche son espace vide (`CreateEmptyPlaceholder`) et le depot sur les bords reste disponible (il ne depend d'aucun groupe, `MGDockHost.cs:633-663`). Un groupe racine vide reste affiche comme aujourd'hui (« Empty Tab Group »).
- P2. Les places memorisees vivent dans le modele, dans une seule table interne indexee par id de panneau (id du groupe, index d'onglet), commune aux panneaux flottants, masques et fermes. Elles remplacent `_floatedFromGroupId` (`MGDockHost.cs:68`) et `AutoHideReturnGroup` / `AutoHideReturnZone` / `AutoHideReturnSplitRatio` (`DockPanelNode.cs:195-215`, membres internes).
- P3. Un fantome vit tant qu'une place le reference. Des qu'aucune ne le reference (place effacee : panneau depose ailleurs par glisser-deposer, panneau ferme sans definition, place oubliee), il est supprime et les splits se replient comme aujourd'hui.
- P4. A la fermeture, une place n'est memorisee que si le `DockableRegistry` de l'hote connait l'id du panneau : sinon rien ne peut le rouvrir et son fantome ne serait jamais libere.
- P5. Replis quand la place ne mene plus a rien (l'application a remplace l'arbre) : panneau flottant, comportement actuel (premier groupe visible, sinon il reste flottant) ; panneau masque, comportement actuel sans zone memorisee (split a la racine du cote de `AutoHideSide`, ratio `DockDropCalculator.HostEdgePreviewRatio`) ; `ShowDockable`, comportement actuel en ignorant les groupes caches.
- P6. Parcours qui ignorent les parties cachees : `ShowDockable` (premier groupe visible), depot sur une barre de separation (`FindFirstLeafTabGroup`), agrandissement (un groupe cache est traite comme introuvable), `GetDocumentArea` (une zone document cachee compte comme absente), tailles minimales effectives (`DockSplitNode.CalculateEffectiveMinWidth/Height`). `GetAllTabGroups()` (public) continue de lister tous les groupes, fantomes compris (risque R1).
- P7. Aucune API publique supprimee ni renommee : `DetachToFloating`, `RedockPanel`, `CreateFloatingWindow`, `FloatingWindows`, `UnpinPanel`, `RepinPanel`, `ShowDockable`, `RemovePanel`, `SaveLayoutToJson`, `LoadLayoutFromJson` et le constructeur public de `MGFloatingDockWindow` (utilise par `FloatingDockWindowActivationTests.cs:36-37`, il cree toujours une fenetre hors modele) sont conserves. Ajouts publics : store flottant de `DockLayoutModel` et type `DockFloatingGroup`, `DockLayoutSerializer.TryFromJson`, `MGDockHostExtensions.TryLoadLayoutFromJson`. Les operations de `DockOperation` ajoutees sont internes.
- P8. Format 2.0 : `version` = "2.0" ; `rootNode` (forme actuelle des noeuds, groupes vides compris) ; `floatingGroups` (groupe d'onglets + `left`, `top`, `width`, `height`) ; `autoHide` (cote, panneaux dans l'ordre de la bande) ; `placements` (`panelId`, `groupId`, `tabIndex`). Toute autre version echoue (`FromJson` leve `InvalidOperationException`, `TryFromJson` rend false avec un diagnostic). Un id de panneau present plusieurs fois dans l'arbre, les groupes flottants et la section `autoHide`, ou une place dont le panneau est ancre, fait aussi echouer le chargement avec un diagnostic. Au chargement : un panneau dont la fabrique rend null est ignore avec sa place ; un groupe vide non reference est supprime ; un groupe flottant vide est supprime.
- P9. Bornes des fenetres flottantes : ecrites dans le modele sur `OnWindowPositionChanged` / `OnWindowSizeChanged` (`MGWindow.cs:129`, `:171`), et recopiees depuis les fenetres ouvertes par `SaveLayoutToJson` avant d'ecrire (l'evenement de position n'est pas immediat, `MGWindow.cs:127`). Une fenetre agrandie est sauvegardee avec ses bornes d'avant agrandissement. Au chargement, les bornes sont ramenees dans `ValidScreenBounds` du desktop pour que la barre de titre reste atteignable.
- P11. Portee de D3 dans un groupe a plusieurs onglets. La place retient l'index d'origine ; au retour, `AddPanel` insere a cet index ou ajoute en fin s'il est hors bornes (`DockTabGroupNode.cs:178-185`), et le panneau rendu devient actif (`DockOperation.cs:50`). Consequences : l'ordre des onglets est identique quand un seul panneau du groupe est absent, ou quand les absents reviennent dans l'ordre inverse de leur depart ; dans un autre ordre, il suit la regle d'index (exemple : [A, B, C] ; departs C, B, A aux index 2, 1, 0 ; retours C, B, A → C ajoute en fin [C], B a l'index 1 hors bornes ajoute en fin [C, B], A a l'index 0 → [A, C, B]). L'onglet actif est toujours le dernier panneau rendu. Les criteres « JSON identique » ci-dessous portent donc sur la disposition de l'editeur (un panneau par groupe, `XamlEditorView.cs:126-135`), et un test nomme fixe le resultat attendu pour un groupe a plusieurs onglets. Un ordre d'onglets exact dans tous les ordres de retour demanderait de memoriser l'ordre complet de chaque groupe : suite connue, hors perimetre.
- P12. Chaque operation de l'hote qui retire, rend, masque, re-epingle, ferme ou rouvre un panneau reconstruit elle-meme l'arbre visuel une fois (comme `UnpinPanel`, `MGDockHost.cs:1620-1633`), sans compter sur `DockLayoutModel.LayoutChanged`. Raison : ajouter ou retirer un panneau ne leve pas `LayoutChanged` de facon fiable (seuls les noeuds abonnes a l'affectation de la racine le declenchent, `DockLayoutModel.cs:29-35`, `:302-356`, et `ActivePanelId` est filtre), et un visuel de groupe ne suit que ses propres onglets (`MGDockTabGroup.cs:714-717`) : un fantome n'a pas de visuel, donc rien ne rafraichit l'affichage quand il se vide ou se remplit. Controle commun A8 (section Validation minimale).
- P10. Remplacement du modele de l'hote. Setter `LayoutModel` (tout remplacement) : les fenetres flottantes de l'ancien modele sont fermees sans signaler de fermeture de panneau ; celles du nouveau modele sont creees ; les bandes auto-hide sont rafraichies ; le tiroir est ferme ; `_panelRegistry` n'est PAS touche, car le contrat actuel est « affecter `LayoutModel` puis appeler `RegisterPanel` pour chaque panneau » (`XamlEditorView.cs:169-175` sur `xaml-editor`), et `RegisterPanel` leve sur un id deja enregistre (`MGDockHost.cs:1036-1039`). Chargement seulement (`LoadLayoutFromJson`, `TryLoadLayoutFromJson`) : apres le remplacement, l'hote vide `_panelRegistry` sans evenement puis y inscrit les panneaux ancres et masques du modele charge, avec un `PanelAdded` par panneau, puis resynchronise la visibilite du `DockableRegistry` (aujourd'hui un chargement laisse le registre perime, et `ShowDockable` creerait un doublon d'un panneau charge).

## Etat des lieux (`develop` `4b51ca6`, verifie dans le code)

Le code de docking est identique sur `xaml-editor` (`git diff --stat develop xaml-editor -- MGUI.Core/UI/Docking MGUI.Tests/Docking` vide).

Modele :

- `DockOperation.RemovePanel` retire le panneau puis `CleanupEmptyTabGroup` → `CollapseSplit` → `ReplaceNodeInParent` : le groupe vide disparait et son voisin prend la place du split (`DockOperation.cs:404-531`) ; un groupe racine vide reste en place (`:446-453`).
- `DockAsTab` nettoie un groupe source vide (`DockOperation.cs:35-44`) et active le panneau (`:50`) ; `DockTabGroupNode.AddPanel` ajoute en fin pour un index hors bornes (`DockTabGroupNode.cs:178-185`).
- `CleanupEmptyNodes` (public) supprime tout groupe vide (`DockOperation.cs:538-602`).
- `DockSplitNode.CalculateEffectiveMinWidth/Height` compte toujours les deux enfants (`DockSplitNode.cs:229-273`) ; un groupe vide vaut 100 (`DockTabGroupNode.cs:326-359`). `DockTabGroupNode.cs` contient la ligne `const int TabHeaderHeight = 30; // Approximate height of tab headers` lue par `EditorCompactPresetTests.cs:129`.
- `DockLayoutModel` porte `RootNode` et le store auto-hide (`DockLayoutModel.cs:54-129`) ; aucun store flottant ; `GetAllTabGroups` ne filtre rien (`:211-238`) ; `Clear` vide le store auto-hide et la racine (`:371-386`).
- Un noeud cree sans id recoit un GUID (`DockNode.cs:39-42`).

Serialisation :

- `ToJson` n'ecrit que `version` "1.0" et `rootNode` (`DockLayoutSerializer.cs:11`, `:18-25`, `:117-137`) : ni fenetres flottantes, ni store auto-hide, ni places.
- `FromJson` avertit en `Debug` si la version differe et charge quand meme (`:234-237`) ; conserve les ids des noeuds (`:278`, `:304`) ; ignore un panneau dont la fabrique rend null (`:365-376`) ; `CleanupInvalidNodes` supprime les groupes vides et replie les splits (`:424-474`).
- `SaveLayoutToJson` / `LoadLayoutFromJson` (`MGDockHostExtensions.cs:16-51`) : le chargement remplace seulement `LayoutModel`.
- Precedent de chargement sans exception : `MGColorPaletteSerializer.FromJson` / `TryFromJson(json, out palette, out diagnostics)` (`MGColorPaletteSerializer.cs:43-104`).

Hote, flottement :

- `DetachToFloating` memorise en memoire l'id du groupe source dans `_floatedFromGroupId`, retire le panneau de `_panelRegistry`, appelle `DockOperation.RemovePanel` puis cree la fenetre (`MGDockHost.cs:1312-1349`). Appelants : fin de glisser hors de l'hote (`:828-835`), menu « Float » (`:2180-2192`).
- `RedockPanel` : groupe memorise s'il existe, sinon premier groupe visible, `DockAsTab` en fin ; sans groupe visible le panneau reste flottant (`:1477-1506`).
- `ExecuteDrop` depuis une fenetre flottante appelle `DetachFromFloatingWindow` puis l'operation de depot ; l'entree `_floatedFromGroupId` n'est pas effacee (`:960-1022`, `:1444-1465`).
- Les fenetres flottantes sont une liste de l'hote (`:164-169`) ; `MGFloatingDockWindow` cree son propre `DockTabGroupNode` (`MGFloatingDockWindow.cs:62-64`). Fermeture par la fenetre : `OnFloatingWindowClosed` signale chaque panneau ferme (`MGDockHost.cs:1367-1381`) ; panneau ferme dans la fenetre : `NotifyFloatingPanelClosed` (`:1425-1436`, `MGFloatingDockWindow.cs:202-221`).
- `MGWindow` expose `OnWindowPositionChanged` et `OnWindowSizeChanged` (`MGWindow.cs:129`, `:171`).

Hote, masquage automatique :

- `UnpinPanel` memorise `AutoHideReturnGroup` / `AutoHideReturnZone` / `AutoHideReturnSplitRatio` sur le panneau (`MGDockHost.cs:1590-1635`) ; `RepinPanel` rend le panneau a ce groupe s'il existe, sinon `SplitDockAtRoot` avec la zone et le ratio memorises (`:1641-1707`) ; `CloseAutoHidePanel` efface le groupe memorise et signale la fermeture (`:1815-1830`). Ces membres internes sont visibles des tests par `MGUI.Core/Properties/AssemblyInfo.cs`.
- Les bandes auto-hide ne sont rafraichies qu'a l'attache du template et dans Unpin / Repin / Close (`MGDockHost.cs:405`, `:1632`, `:1704`, `:1828`).

Hote, fermeture et reouverture :

- Fermeture d'un onglet ancre : `PanelCloseRequested` → `DockOperation.RemovePanel`, `PanelRemoved`, `NotifyClosed` (`MGDockHost.cs:2159-2177`) ; « Close Others » et « Close All » passent par le meme evenement (`MGDockTabGroup.cs:586-619`) ; API `RemovePanel(panelId)` (`MGDockHost.cs:1051-1074`).
- `ShowDockable` prend le premier groupe du modele (`GetAllTabGroups().FirstOrDefault()`), sinon cree la racine ou coupe la racine (`:1247-1297`) ; `DockableDefinition.CreatePanelNode` cree un nouveau noeud d'id `DockableId` (`DockableDefinition.cs:105-120`).
- Aucune place de panneau ferme n'est memorisee.
- `_panelRegistry` n'est jamais reconstruit quand le modele est remplace (declaration `MGDockHost.cs:60`, ecritures `:1041`, `:1067`, `:1332`, `:1462`, `:1825`, `:2166`).

Hote, affichage et depot :

- `BuildVisualTree` : un split donne un `MGDockSplitContainer` avec ses deux enfants, un groupe donne un `MGDockTabGroup` meme vide (`MGDockHost.cs:2073-2153`) ; un groupe vide affiche « Empty Tab Group » (`MGDockTabGroup.cs:632-691`).
- `MGDockSplitContainer` calcule ses tailles minimales depuis `ModelNode` (`MGDockSplitContainer.cs:282-297`) et n'a aucun etat replie.
- Rafraichissement : un `MGDockTabGroup` reconstruit ses en-tetes sur `CollectionChanged` de son groupe (`MGDockTabGroup.cs:714-717`) ; `DockLayoutModel.LayoutChanged` vient de l'affectation de la racine, du store auto-hide et des `PropertyChanged` des noeuds abonnes au moment de l'affectation de la racine, `ActivePanelId` exclu (`DockLayoutModel.cs:29-39`, `:85-121`, `:302-356`) ; `RemovePanel(panelId)` et `ShowDockable` ne reconstruisent pas l'affichage eux-memes (`MGDockHost.cs:1051-1074`, `:1247-1297`) ; aujourd'hui la reconstruction apres fermeture vient du repli du split (setter d'enfant de `DockSplitNode`, `DockSplitNode.cs:30-62`).
- Cibles de depot : groupes visuels (`GetAllVisibleTabGroups`, `MGDockHost.cs:579`, `:2544-2556`) ; bords de l'hote, sans groupe (`:633-663`) ; barre de separation resolue dans le modele par `FindFirstLeafTabGroup` (`:665-756`, `:2661-2679`).
- Agrandissement : pile d'ids cherches dans `GetAllTabGroups` (`:2014-2031`).
- Regles : `GetDocumentArea` parcourt tout le modele (`:1843-1846`) ; `CanDockIntoGroup` s'en sert (`:1877-1901`).

Consommateurs, tests et documentation :

- Demo : boutons de sauvegarde et de chargement, fichier `docking_layout.json`, erreurs affichees par notification (`MGUI.Samples/Features/DockingDemo.cs:23`, `:360-392`, `:436-479`).
- Tests qui figent le comportement actuel : `FloatingWindowDockMenuTests` (d) repli sur le premier groupe visible (`:220-242`) et (f) JSON sans memoire de flottement (`:275-294`) ; `DockAutoHideRepinTests` simule Unpin / Repin avec `AutoHideReturn*` (`:43-94`, `:149-158`, `:188-209`, `:266-276`) ; `DockOperationTests.DockAsTab_MovesPanel_FromSourceGroup_CleansUpEmptySource` (`:95-109`, reste valide : aucune place) ; tests de serialisation de `DockLayoutModelTests` (`:261-432`).
- Tests qui lisent le checkout principal en chemin absolu : `EditorCompactPresetTests.cs:32`, `:129` (lit `DockTabGroupNode.cs`).
- Branche `xaml-editor` : `XamlEditorViewTests.cs:200-265` utilise `DetachToFloating` / `RedockPanel` ; `XamlEditorView.CreateDockHost` affecte `host.LayoutModel` puis appelle `RegisterPanel` sur les cinq panneaux (`XamlEditorView.cs:169-175`) ; disposition par defaut de l'editeur dans `XamlEditorView.cs:126-166` (split horizontal 0.72 : a gauche split vertical 0.78 [split horizontal 0.45 XAML | Preview] / Diagnostics, a droite split vertical 0.45 Document / Properties ; Preview ne flotte pas) ; l'ADR-0010 decrit l'effondrement actuel.
- Documentation qui deviendra fausse : `MGUI.Core/UI/Docking/TODO-DockingManager.md:105-107` (nettoyage automatique des groupes vides) ; `Docs/Tasks/docking-bugs-tasks.md:130`, `:138`, `:176` (memoire en memoire, non persistee). `MGUI.Core/UI/Docking/analysis-dockmanager.md:197-201`, `:225-230` est un audit : il reste en lecture seule, l'ADR-0012 enregistre le changement.

## Principes (non negociables)

- Plus d'effondrement : un panneau qui quitte l'arbre par flottement, masquage ou fermeture laisse sa place, et revient dans le meme groupe, entre les memes splits (ordre et onglet actif d'un groupe a plusieurs onglets : P11).
- Le modele est la source de verite (arbre, fantomes, fenetres flottantes, panneaux masques, places) ; l'hote le reflete et ne garde aucune memoire propre.
- Aucune API publique supprimee ni renommee (P7).
- Tests sur le comportement observable de l'hote reel (`GraphTestRuntime`, trames reelles quand une interaction est en jeu), chaque garde prouvee par mutation.
- Un test n'ecrit jamais dans les fichiers du depot.

## Perimetre et hors perimetre

Dans le perimetre : les taches T0 a T6 ci-dessous.

Hors perimetre (documente, non implemente) :

- migration ou lecture de l'ancien format 1.0 (D1) ;
- place memorisee d'un panneau ferme sans definition dans le `DockableRegistry` (P4) ;
- chargement d'une disposition au demarrage de la demo ou de `MGUI.Editor` (D8) ;
- mise a jour de l'ADR-0010 et des tests de la branche `xaml-editor` (autre session, voir Suites connues) ;
- theming, Ctrl+Tab, regles de depot hors des parcours listes en P6.

## Consignes de travail pour l'agent IA

- Branche `docking-ghost-groups` (creee depuis `develop` `4b51ca6`). Taches dans l'ordre, une seule a la fois : 🚧 avant de commencer, ✅ au commit (🧪 si une validation manuelle de l'auteur reste attendue), statut mis a jour dans le meme commit que la tache. Mode AUTO (D9) : pas d'arret entre les taches, sauf blocage. Un commit par tache, indexe fichier par fichier ; verifier `git branch --show-current` avant chaque commit ; jamais de push ni de merge.
- Pipeline par tache : brief ecrit par la session principale (but, perimetre, criteres, fichiers), execution par un agent `executor`, revue du diff et integration par la session principale. Verification par un agent `verifier` en contexte frais aux jalons V1 (apres T3) et V2 (apres T5), sur la revendication exacte du jalon ; au plus deux tours de correction par jalon.
- Cadre commun des tranches : proprietaires, un agent `executor` pour l'ecriture du code et des tests dans le perimetre de la tache (fichiers exclusifs a la tache), la session principale pour le brief, la revue du diff, l'integration, la mise a jour du plan et le commit ; budget, une execution plus au plus deux tours de correction par tache (au-dela : la session principale reprend ou la tache passe en ⚠️) ; arrets, ⚠️ et « Points ouverts » si un critere ne peut etre tenu sans toucher un fichier hors perimetre, sans ajouter ou changer une API publique non listee en P7, si un test preexistant sans rapport devient rouge, ou si la reference T0 ne peut pas etre reproduite.
- Ne jamais lancer `MGUI.Samples` ni `MGUI.Editor.Host` : le lancement est une validation manuelle de l'auteur.
- Editions avec l'outil d'edition ; les fichiers sont en CRLF (pas de `sed -i`).
- Si `MGUI.Samples` est touche, terminer par `dotnet build MGUI.Samples/MGUI.Samples.csproj --no-incremental`.
- Treize fichiers de tests lisent le checkout principal `d:\development\repo\MGUI` : avant de conclure a une regression, controler `git -C D:/development/repo/MGUI status --short` et sa branche. La ligne de `DockTabGroupNode.cs` lue par `EditorCompactPresetTests.cs:129` reste intacte.
- Toute decision prise en cours de route est ajoutee a l'ADR-0012, section « Decisions taken during delivery ».
- Blocage (information manquante, contradiction, API publique non listee en P7) : tache en ⚠️, question dans « Points ouverts », arret.

## Legende de statut

- ⏳ Todo · 🚧 In progress · 🧪 Needs testing · ✅ Done · ⚠️ Blocked

## Validation minimale

1. `dotnet build MGUI.Tests/MGUI.Tests.csproj`
2. Pendant la tache : `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build --filter "FullyQualifiedName~Docking|FullyQualifiedName~FloatingDockWindow|FullyQualifiedName~OverlappingWindows"`
3. Avant chaque commit : suite complete `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build`, comparee a la reference mesuree en T0 (aucun nouvel echec).

Controle d'affichage commun A8 (P12), applique par T2 a T5 a chaque chemin qu'elles touchent, sur l'hote reel apres l'operation et une trame `GraphTestRuntime`, sans appel manuel a `RebuildVisualTree` dans le test :

- aucun `MGDockTabGroup` de l'hote n'est lie a un groupe vide qui n'est pas la racine ;
- un panneau rendu ou rouvert est un onglet d'un `MGDockTabGroup` de l'hote lie au noeud de son groupe d'origine ;
- un panneau retire (flotte, masque, ferme) n'est un onglet d'aucun `MGDockTabGroup` de l'hote ;
- aucun `MGDockSplitContainer` n'est construit pour un split dont un enfant est cache ;
- mutation par chemin : retirer la reconstruction explicite de ce chemin → A8 rouge.

## Taches

### ✅ T0. Plan, ADR-0012 et reference de tests

Statut (17 septembre 2026) : ADR-0012 ecrite (Proposed) et indexee. Reference sur `docking-ghost-groups` = `develop` `4b51ca6`, checkout principal sur `xaml-editor`, propre : `dotnet build MGUI.Tests/MGUI.Tests.csproj` 0 erreur ; `dotnet test MGUI.Tests/MGUI.Tests.csproj --no-build` 2420 tests, 2420 reussis, 0 echec preexistant.

But : committer le plan et l'ADR, et mesurer la reference de la suite avant tout code.

Prerequis : approbation explicite du plan par l'auteur. Proprietaire : session principale (documents, mesure).

Perimetre :

- ce fichier ;
- `Docs/decisions/0012-docking-ghost-groups.md` (skill `adr`, statut Proposed) : contexte (effondrement mesure, memoire en memoire seulement), decisions D1 a D9 et P1 a P11 (dont la portee exacte de la garantie de retour), alternatives ecartees (voisin memorise et ids repris ; zone elargie ; memoire non persistee), consequences (R1 a R4). Numero verifie libre juste avant le commit sur `develop`, `xaml-editor` et `animation-v5` (`git ls-tree --name-only <branche> Docs/decisions/`) ;
- `Docs/decisions/README.md` (ligne d'index).

Criteres d'acceptation :

- build vert ; suite complete executee, total et echecs preexistants notes ici avec la branche du checkout principal au moment de la mesure.

Rollback : revert du commit.

Commit recommande : `docs(docking): add the placeholder group plan and ADR-0012`

### ⏳ T1. Modele : places memorisees, groupes fantomes et store flottant

But : donner au modele tout ce que la restauration exige, sans changer le comportement de l'hote (aucune place n'est encore ecrite par l'hote).

Prerequis : T0 (reference de la suite mesuree). Cadre commun des tranches (proprietaires, budget, arrets).

Perimetre :

- `DockLayoutModel` : table interne des places (P2) avec lecture, ecriture et oubli ; `IsPlaceholderReferenced(DockTabGroupNode)` ; store flottant public (`DockFloatingGroup` : `DockTabGroupNode Group`, `Left`, `Top`, `Width`, `Height`) avec ajout, retrait, recherche du groupe flottant d'un panneau, `LayoutChanged` et abonnement aux noeuds comme pour le store auto-hide ; `Clear` vide aussi le store flottant et les places.
- Visibilite structurelle (P1) : aide interne sur les noeuds (groupe vide non racine ; split dont les deux enfants sont caches ou nuls) ; `DockSplitNode.CalculateEffectiveMinWidth/Height` ignore un enfant cache.
- `DockOperation` (operations internes) : detacher en flottant avec bornes (place ecrite, groupe d'origine conserve s'il est reference) ; rendre un panneau flottant a sa place (D3, efface la place, retire le groupe flottant vide) ; masquer et rendre un panneau masque ; fermer avec ou sans place memorisee ; rouvrir un panneau a sa place ; oublier une place puis nettoyer. `CleanupEmptyTabGroup` et `CleanupEmptyNodes` ne suppriment plus un groupe reference (P3).
- Tests : `DockOperationTests`, `DockLayoutModelTests`, `DockNodeModelTests`.

Hors perimetre : `MGDockHost`, `MGFloatingDockWindow`, serialisation.

Criteres d'acceptation :

- sur la disposition de l'editeur reproduite en modele (un panneau par groupe, voir l'etat des lieux) : les quatre volets flottables flottes puis rendus dans chacun des 24 ordres donnent un arbre dont le JSON `ToJson` est identique a celui de depart ; idem pour les cinq volets masques puis rendus dans chacun des 120 ordres ; idem pour un melange flottant, masque et ferme rendu dans un ordre croise ;
- groupe d'origine encore peuple : le panneau reprend son index (D3), borne quand le groupe a perdu des onglets ;
- test nomme `MultiTabGroup_OutOfOrderReturn_FollowsIndexRule` : groupe [A, B, C] (actif C) dans un split ; departs C, B, A puis retours C, B, A → le groupe est le meme noeud, dans le meme split avec le meme ratio, onglets [A, C, B], actif A ; departs A, B, C puis retours C, B, A (ordre inverse) → onglets [A, B, C], actif A (P11) ;
- place oubliee : le fantome disparait et les splits se replient exactement comme aujourd'hui ;
- tailles minimales : un split avec un enfant cache vaut le minimum de l'autre enfant ;
- `DockAsTab_MovesPanel_FromSourceGroup_CleansUpEmptySource` et tous les tests de docking existants restent verts ; la ligne lue par `EditorCompactPresetTests.cs:129` est intacte ;
- mutations : nettoyage qui ignore les references → tests d'ordre rouges ; index ignore → test D3 rouge.

Rollback : revert du commit (aucun appelant hors modele).

Commit recommande : `feat(docking): keep emptied tab groups as placeholders while a panel remembers them`

### ⏳ T2. Hote : parties cachees et flottement pilote par le modele

But : l'hote affiche l'arbre en sautant les parties cachees et reflete le store flottant ; « Dock » rend un panneau exactement a sa place.

Prerequis : T1. Cadre commun des tranches.

Perimetre :

- `MGDockHost.BuildVisualTree` / `BuildSplitContainer` (P1) ; `FindFirstLeafTabGroup`, agrandissement, `GetDocumentArea`, premier groupe visible de `ShowDockable` et de `RedockPanel` (P6).
- `DetachToFloating`, `RedockPanel`, `CreateFloatingWindow`, branche flottante de `ExecuteDrop` (place oubliee), fermeture d'un panneau dans une fenetre flottante et fermeture de la fenetre (place oubliee a ce stade ; T4 la garde) passent par les operations de T1 ; les fenetres sont creees et fermees d'apres le store flottant du modele ; `_floatedFromGroupId` est supprime ; replis P5.
- `MGFloatingDockWindow` : nouveau constructeur interne qui enveloppe un `DockFloatingGroup` du modele et ecrit ses bornes (P9) ; le constructeur public reste (P7).
- Tests : `FloatingWindowDockMenuTests` ((c) verifie aussi l'index D3 ; (d) reecrit : le groupe de C est un fantome et « Dock » rend l'arbre identique ; nouveau repli quand l'application a remplace la racine ; (e) et (f) inchanges) ; nouveau `MGUI.Tests/Docking/DockPlaceholderGroupHostTests.cs` sur la disposition de l'editeur (fenetre 1280 x 720).

Criteres d'acceptation :

- A1 (disposition de l'editeur, un panneau par groupe) : chaque volet flottable flotte puis revient par « Dock », un a la fois : `SaveLayoutToJson` identique au depart apres chaque cycle ;
- A2 (meme disposition) : les quatre volets flottables flottent, puis reviennent dans chacun des 24 ordres : JSON identique au depart ;
- groupe a plusieurs onglets de `FloatingWindowDockMenuTests` (GroupAX) : A puis X flottent (tous deux a l'index 0), puis reviennent par « Dock » dans l'ordre A, X → GroupAX est le meme noeud, a la meme place, onglets [X, A], actif X (P11) ;
- A7 : pendant qu'un fantome existe, aucun `MGDockTabGroup` visible n'a de groupe vide, le voisin visible occupe toute la zone de l'ancien split, aucun `MGDockSplitContainer` n'est construit pour un split dont un enfant est cache ;
- un panneau flottant depose par glisser-deposer dans un autre groupe, puis re-flotte et rendu par « Dock », revient dans ce nouveau groupe (place effacee au depot) ;
- tous les panneaux flottants : l'hote affiche son espace vide et un onglet flottant depose sur un bord de l'hote s'y ancre ;
- `FloatingWindowRedockTests`, `FloatingWindowContentInputTests`, `AuxiliarySurfaceLifecycleTests`, `FloatingDockWindowActivationTests`, `OverlappingWindowsInputRoutingTests` verts ;
- A8 (controle d'affichage commun) pour `DetachToFloating` (menu « Float » et fin de glisser hors de l'hote), `RedockPanel`, la branche flottante de `ExecuteDrop` et les deux fermetures depuis une fenetre flottante ; le commentaire « triggers RebuildVisualTree via LayoutChanged » (`MGDockHost.cs:1334`) est corrige ;
- mutations : saut des parties cachees retire → A7 rouge ; retour a « premier groupe visible » → A2 rouge.

Rollback : revert du commit (T1 reste sans effet sans appelant).

Commit recommande : `feat(docking): restore floated panels to their exact place through placeholder groups`

### ⏳ T3. Masquage automatique par les places memorisees

But : « Auto-Hide » puis « Pin » rend un panneau exactement a sa place (D4).

Prerequis : T2 (affichage des parties cachees). Cadre commun des tranches.

Perimetre :

- `UnpinPanel`, `RepinPanel`, `CloseAutoHidePanel` passent par les operations de T1 ; replis P5 ; suppression de `AutoHideReturnGroup`, `AutoHideReturnZone`, `AutoHideReturnSplitRatio` et mise a jour du commentaire de `MGUI.Core/Properties/AssemblyInfo.cs`.
- `DockAutoHideRepinTests` reecrit sur l'hote reel (`UnpinPanel` / `RepinPanel`) a la place des simulations, avec les memes intentions (retour au groupe, retour a la place exacte apres que le groupe s'est vide, disposition vide).
- Tests d'acceptation ajoutes a `DockPlaceholderGroupHostTests.cs`.

Criteres d'acceptation :

- A3 (disposition de l'editeur, un panneau par groupe) : les cinq volets masques puis re-epingles dans l'ordre, dans l'ordre inverse et dans un ordre croise : JSON identique au depart ;
- melange (meme disposition) : un volet flottant et un volet masque rendus dans un ordre croise : JSON identique au depart ;
- A8 pour `UnpinPanel`, `RepinPanel` et `CloseAutoHidePanel` ;
- `DockAutoHideRepinTests` et les tests de bandes et tiroir auto-hide existants verts ;
- mutation : `RepinPanel` qui ignore la place → A3 rouge.

Jalon V1 (verifier en contexte frais) : revendication « flotter ou masquer puis rendre des volets, dans n'importe quel ordre, ne provoque aucun effondrement et rend chaque panneau a son groupe d'origine (meme noeud), entre les memes splits (memes ids, memes ratios) ; avec un panneau par groupe la disposition sauvegardee est identique ; dans un groupe a plusieurs onglets, ordre et onglet actif suivent P11 ; l'affichage saute les parties cachees et montre chaque panneau rendu dans le visuel de son groupe d'origine, sans groupe vide visible ; les suites docking et entree restent vertes », avec le diff T1 a T3, les criteres A1 a A3, A7 et A8 et le test `MultiTabGroup_OutOfOrderReturn_FollowsIndexRule`.

Rollback : revert du commit.

Commit recommande : `feat(docking): restore auto-hidden panels through placeholder groups`

### ⏳ T4. Fermeture et reouverture a la place d'origine

But : fermer un panneau connu du `DockableRegistry` puis `ShowDockable` le rouvre exactement a sa place (D5).

Prerequis : T3 et jalon V1 confirme. Cadre commun des tranches.

Perimetre :

- chemins de fermeture : onglet ancre (`MGDockHost.cs:2159-2177`, donc aussi Close Others / Close All), `RemovePanel(panelId)`, panneau ferme dans une fenetre flottante, fermeture de la fenetre flottante, `CloseAutoHidePanel` : fermeture avec place memorisee si le registre connait l'id, sinon place oubliee (P4) ;
- `ShowDockable(id)`, dans cet ordre : id dans `_panelRegistry` (panneau ancre ou masque) → comportement actuel inchange (`MGDockHost.cs:1255-1264`) ; id dans le store flottant du modele → son onglet devient actif et sa fenetre passe au premier plan, la place n'est pas touchee, aucun noeud n'est cree ; sinon, place memorisee → reouverture a cette place ; sinon premier groupe visible (P5, P6).
- Tests d'acceptation ajoutes a `DockPlaceholderGroupHostTests.cs` (registre de dockables avec les ids des volets, `CanClose` vrai dans le test).

Criteres d'acceptation :

- A4 (disposition de l'editeur, un panneau par groupe) : chaque volet ferme puis rouvert, un a la fois, et tous fermes puis rouverts dans l'ordre inverse et dans un ordre croise : JSON identique au depart ;
- un volet ferme depuis une fenetre flottante ou depuis le tiroir auto-hide se rouvre a sa place ancree d'origine ;
- test nomme `ShowDockable_OnFloatingPanel_ActivatesItWithoutDuplicate` : un volet flotte, `ShowDockable(id)` → l'id est present une seule fois dans le modele (arbre + store flottant + store auto-hide), la fenetre flottante reste ouverte avec l'onglet actif, puis « Dock » rend le volet a son groupe d'origine et le JSON est identique au depart ;
- invariant verifie apres chaque etape de A4 : chaque id de panneau apparait au plus une fois dans l'arbre, le store flottant et le store auto-hide ;
- A8 pour la fermeture d'un onglet ancre, « Close Others », « Close All », `RemovePanel(panelId)` et `ShowDockable` (reouverture dans un fantome et repli sur le premier groupe visible) ;
- panneau sans definition ferme : aucun fantome ne reste et la disposition se replie comme aujourd'hui ;
- evenements `PanelRemoved` / `NotifyClosed` / `PanelAdded` / `NotifyShown` inchanges en nombre ;
- mutation : place non memorisee a la fermeture → A4 rouge.

Rollback : revert du commit.

Commit recommande : `feat(docking): reopen closed dockables at their previous place`

### ⏳ T5. Format 2.0, chargement sans exception et remplacement du modele

But : sauvegarder et recharger toute la disposition (D1, D6), et garantir qu'un echec de chargement n'interrompt pas l'application hote (D7, D8).

Prerequis : T4. Cadre commun des tranches.

Perimetre :

- `DockLayoutSerializer` : DTO et format 2.0 (P8), `TryFromJson(json, panelFactory, out model, out diagnostics)` (JSON invalide, version, type de noeud inconnu), `FromJson` leve sur une version autre que 2.0, `CleanupInvalidNodes` garde les groupes vides references.
- `MGDockHostExtensions` : `TryLoadLayoutFromJson(host, json, panelFactory, out diagnostics)` (false, hote intact, message en `Debug`) ; `LoadLayoutFromJson` garde son exception ; `SaveLayoutToJson` recopie d'abord les bornes des fenetres ouvertes (P9).
- `MGDockHost` : remplacement du modele (P10) et bornes ramenees a l'ecran (P9).
- `MGUI.Samples/Features/DockingDemo.cs` : chargement par `TryLoadLayoutFromJson`, diagnostics affiches par la notification existante.
- Tests : `DockLayoutModelTests` (round trips existants en 2.0 ; round trips des fantomes, du store flottant, du store auto-hide et des places) ; `FloatingWindowDockMenuTests` (f) reecrit (le JSON contient le groupe flottant et la place) ; acceptation dans `DockPlaceholderGroupHostTests.cs`.

Criteres d'acceptation :

- A5 (disposition de l'editeur, un panneau par groupe) : une disposition avec un volet flottant (bornes connues), un volet masque et un volet ferme est sauvegardee puis chargee dans un hote neuf : la fenetre flottante est recreee avec ses bornes, la bande auto-hide montre le volet masque, puis « Dock », « Pin » et `ShowDockable` redonnent un JSON identique a la disposition de depart ;
- round trip d'un groupe a plusieurs onglets dont un onglet flotte : apres chargement, « Dock » le rend au meme noeud a l'index memorise (P11) ;
- A8 juste apres le chargement (aucun groupe vide visible, panneaux flottants et masques absents des groupes de l'hote), puis apres chacun des « Dock », « Pin » et `ShowDockable` de A5 ;
- test nomme `SetLayoutModelThenRegisterPanels_KeepsTheEditorPattern` (reproduit `XamlEditorView.cs:169-175`) : `host.LayoutModel` recoit un arbre de cinq panneaux, puis `RegisterPanel` sur chacun → aucune exception, un `PanelAdded` et un `OnShown` du `DockableRegistry` par panneau, `FindPanel` rend chacun ;
- apres `TryLoadLayoutFromJson` : `FindPanel` rend chaque panneau ancre ou masque charge, `ShowDockable` sur l'un d'eux ne cree aucun doublon (invariant d'unicite de T4), et un JSON avec un id en double ou une place dont le panneau est ancre est refuse avec un diagnostic ;
- A6 : un JSON 1.0 et un JSON invalide : `TryLoadLayoutFromJson` rend false avec au moins un diagnostic, le JSON de l'hote et ses fenetres flottantes sont inchanges ; `LoadLayoutFromJson` leve ;
- bornes hors ecran ramenees dans `ValidScreenBounds` ; panneau inconnu de la fabrique ignore avec sa place ;
- `DockingDemo` compile (`--no-incremental`) ;
- mutations : places non ecrites → A5 rouge ; version non controlee → A6 rouge.

Jalon V2 (verifier en contexte frais) : revendication « le format 2.0 sauvegarde et restaure fantomes, fenetres flottantes, panneaux masques et places ; un ancien format ou un JSON invalide est refuse sans exception et sans toucher la disposition en place ; la fermeture et la reouverture rendent le panneau a son groupe d'origine entre les memes splits (disposition identique avec un panneau par groupe ; ordre et onglet actif d'un groupe a plusieurs onglets selon P11) ; apres chaque fermeture, reouverture et chargement, l'affichage montre chaque panneau present dans le visuel de son groupe et aucun groupe vide », avec le diff T4 et T5 et les criteres A4 a A6 et A8.

Rollback : revert du commit ; T4 et les precedentes restent coherentes avec le format 1.0.

Commit recommande : `feat(docking): persist floating, auto-hidden and closed panel places in layout format 2.0`

### ⏳ T6. Documentation et cloture

But : laisser une documentation vraie et cloturer le chantier.

Prerequis : T5 et jalon V2 confirme. Proprietaire : session principale.

Perimetre :

- ADR-0012 : statut Accepted, decisions prises en cours de route ; `Docs/decisions/README.md` ;
- `MGUI.Core/UI/Docking/TODO-DockingManager.md:105-107` ; `Docs/Tasks/docking-bugs-tasks.md:130`, `:138`, `:176` (renvoi vers ce plan et l'ADR-0012) ; l'audit `analysis-dockmanager.md` n'est pas modifie ;
- ce fichier : statuts, notes de validation, suites connues.

Criteres d'acceptation :

- plus aucune phrase de la documentation listee dans l'etat des lieux ne decrit l'effondrement ou la memoire non persistee ;
- suite complete verte par rapport a la reference T0.

Validation manuelle (auteur) : demo docking (SCN-DOCK-001) : flotter, masquer et fermer des panneaux puis les rendre dans le desordre ; sauvegarder avec une fenetre flottante ouverte, recharger ; charger un ancien `docking_layout.json` (message d'erreur, disposition inchangee). T6 reste 🧪 tant que cette validation n'est pas confirmee.

Rollback : revert du commit.

Commit recommande : `docs(docking): accept ADR-0012 and document placeholder groups`

## Risques

- R1. `GetAllTabGroups()` public renvoie aussi les fantomes (groupes vides) : un appelant externe (editeur CasaEngine, branche `xaml-editor`) qui suppose des groupes peuples peut se tromper.
- R2. Changement de format : les `docking_layout.json` existants et les dispositions sauvegardees par CasaEngine ne se chargent plus (echec propre, D7).
- R3. Branche `xaml-editor` : apres merge de `develop`, la note X1b de l'ADR-0010 sur l'effondrement devient fausse et `XamlEditorViewTests.cs:200-265` doit etre relance. Le motif « `LayoutModel` puis `RegisterPanel` » de `XamlEditorView.cs:169-175` (et probablement de CasaEngine) est conserve par P10 et garde par un test nomme de T5.
- R4. Les fantomes des dockables fermes restent dans l'arbre tant que leur place est memorisee (au plus un par definition du registre).

## Points ouverts

- Aucun a la redaction.

## Suites connues (hors perimetre)

- Session `xaml-editor` : mettre a jour la note X1b de l'ADR-0010 et relancer ses tests apres merge (R3).
- Place memorisee des panneaux fermes sans definition de registre (P4).
- Ordre d'onglets exact dans un groupe a plusieurs onglets pour tout ordre de retour (memoriser l'ordre complet du groupe, P11).
