# Taches focus : reconcilier IsFocusable et CanHandleKeyboardInput (18 septembre 2026)

## Objectif

MGUI porte deux notions concurrentes de "cet element peut prendre le focus clavier", et elles ne sont pas d'accord
pour les controles de saisie. Consequence observee et reproduite en desktop headless : une `MGTextBox` n'est jamais
une cible de navigation valide, donc `MGDesktop.ResolveAutoFocusTarget` ne peut ni la restaurer comme dernier element
focus d'une fenetre, ni la choisir comme premier focusable. Comme `MGWindow.ActivatesOnClick` (defaut `true`) resout
une nouvelle cible d'auto-focus a chaque clic dans la fenetre qui n'a lui-meme mis aucun focus en file, un tel clic
deplace le focus HORS de la text box, et le focus ne peut jamais y revenir par ce chemin.

Le but est de rendre les deux regles coherentes a la racine, sans contournement : une seule regle de navigation, une
API publique honnete, et un comportement clavier utilisable — **entrer ET sortir d'un controle de saisie au clavier**,
y compris ceux qui consomment legitimement Tab.

Le contournement de l'editeur XAML (`MGTextCaret.ShowWhenUnfocused = true`, `MGUI.Editor/Text/XamlEditorTextPane.cs:30`,
commit `744af73`, motive dans `Docs/decisions/0010-xaml-editor-v1.md`) reste en place : il appartient a la session
editeur, ce chantier n'y touche pas. Voir "Points ouverts".

## Contexte : cause racine (investigation en lecture seule du 18 septembre 2026)

### Le terme `IsFocusable` de `IsNavigationTarget` est redondant sauf pour deux types

`MGDesktop.IsNavigationTarget` (`MGUI.Core/UI/MGDesktop.cs:352-358`) s'ecrit :

```
element != null && element.IsFocusable && DerivedIsEnabled && DerivedIsHitTestVisible
                && Visibility == Visible && CanElementReceiveKeyboardInput(element)
```

et `CanElementReceiveKeyboardInput` (`:341-350`) teste deja `element.CanHandleKeyboardInput` via
`FocusInputPolicy.IsKeyboardInputEligible`. Or `CanHandleKeyboardInput` vaut `IsFocusable` par defaut
(`MGUI.Core/UI/MGElement.cs:2798`). Le terme `element.IsFocusable` de `IsNavigationTarget` est donc **strictement
redondant pour tout controle qui ne surcharge pas `CanHandleKeyboardInput`**. Deux types seulement le surchargent
dans tout le depot :

- `MGTextBox` : `public override bool CanHandleKeyboardInput => true;` (`MGUI.Core/UI/MGTextBox.cs:41`) ;
- `MGMenuBar` : `public override bool CanHandleKeyboardInput => IsFocusable || IsMenuActive;` (`MGUI.Core/UI/MGMenuBar.cs:299`,
  qui pose par ailleurs `IsFocusable = true` dans son constructeur, `:493`).

Le bug se formule donc exactement ainsi : **un controle qui surcharge `CanHandleKeyboardInput` est exclu de la
navigation.** Ce n'est pas un oubli propre a `MGTextBox`, c'est un piege pour tout controle futur ou tiers.

### Origine historique : une derive semantique, pas un oubli

Avant le commit `4633f13` (8 mars 2026, "feat(mgui): add IsFocusable and Focus() to MGElement for keyboard navigation"),
`CanHandleKeyboardInput` valait `=> false` par defaut et `MGTextBox` le surchargeait a `true` : c'etait la **seule**
notion. `IsFocusable` a ete ajoute PAR-DESSUS, comme opt-in pour les controles non textuels, et son commentaire de
documentation dit encore aujourd'hui "can receive keyboard focus **when clicked**, without necessarily being a text
input control" (`MGUI.Core/UI/MGElement.cs:2764-2769`). `IsNavigationTarget`, arrive plus tard avec le service de
navigation, a silencieusement reinterprete ce drapeau comme "est un arret de tabulation". `MGTextBox` n'a jamais ete
mis a jour parce que sa surcharge satisfaisait deja l'ancienne regle.

Reponse a la question posee : oui, `IsFocusable` est bien cense signifier "atteignable par Tab / auto-focus" et
`CanHandleKeyboardInput` "peut recevoir les evenements clavier" — mais la documentation de `IsFocusable` dit encore
"au clic" et la redondance ci-dessus montre que le second terme suffit deja.

### Le depot contourne deja le probleme a la main

- `MGUI.Samples/Features/FocusInputReview.xaml.cs:49-53` et `:142` posent `IsFocusable = true` sur six text boxes ;
- `MGUI.Tests/Tooling/StableDiagnosticIdTests.cs:147`, `:162`, `:343`, `:344` font de meme.

C'est la preuve la plus directe que la valeur voulue est `true`. Par ailleurs `IsFocusable` **n'est pas expose en
XAML** (aucune occurrence dans `MGUI.Core/UI/XAML/`) : un utilisateur purement XAML n'a aujourd'hui aucun moyen de
rendre une text box navigable.

### Rayon d'impact mesure

- **Les anneaux de focus** sont conditionnes par `FocusedKeyboardHandler == this` (`MGUI.Core/UI/MGElement.cs:3854-3868`),
  pas par `IsFocusable` : aucun changement visuel induit par le drapeau.
- **`SemanticNavigationTextEntryPreservationTests`** reste vert : ses deux cas Tab fixent `AcceptsTab` explicitement
  (`:56`, `:79`), et l'ordre `[textBox, button]` fait toujours pointer l'index 0 -> 1 sur le bouton.
- **`WindowActivationOnClickTests`** n'utilise que des `MGButton` et un `MGBorder` nu.
- **Quatre controles composites de `MGUI.Core` hebergent une `MGTextBox`** dans leur arbre visuel et gagnent donc des
  arrets de tabulation imbriques. Inventaire complet (constructions `new(...)` typees par la cible, invisibles a une
  recherche sur `new MGTextBox` — c'est ce qui les avait fait manquer au premier passage) :
  - `MGPropertyGrid` : une `MGTextBox` par ligne d'edition (`MGUI.Core/UI/MGPropertyGrid.cs:1016`, `:1040-1043`) ;
  - `MGGraphControls`, boite de commentaire : deux parts de template `MGTextBox` (`:3343-3344`, `:3380-3381`),
    configurees `AcceptsTab = true` (`:3478`) ;
  - `MGChatBox` : `InputTextBox` (`MGUI.Core/UI/MGChatBox.cs:75`, construite `:158`, ajoutee a l'arbre `:184`) ;
  - `MGXAMLDesigner` : `FromStringTextBoxComponent` et `FromFileTextBoxComponent` (`MGUI.Core/UI/MGXAMLDesigner.cs:32-33`,
    construites `:60` et `:106`, la seconde en lecture seule `:108`).

  `IsNavigationTarget` ne filtre pas `IsReadonly` (`MGUI.Core/UI/MGDesktop.cs:352-358`) : une text box en lecture seule
  devient donc elle aussi un arret de tabulation. Consequence **acceptee par l'auteur pour les quatre controles, lecture
  seule comprise** (decision 6) : ce sont de vrais champs de saisie, et une text box en lecture seule tabulable permet
  d'y selectionner et copier du texte au clavier, comme dans WPF. La depriorisation de la tache 1 empeche par ailleurs
  qu'ils captent l'auto-focus d'une fenetre.
  Les fabriques XAML sont `MGUI.Core/UI/XAML/Controls.cs:1528`, `:3436`, `:3588`, `:3631`.
- **Un test epingle le comportement actuel**, et il appartient a la session editeur :
  `MGUI.Tests/Editor/XamlEditorSelectionTests.cs:402-407` documente en toutes lettres que "the text pane can never be
  that target, because MGDesktop.IsNavigationTarget requires IsFocusable and MGTextBox never sets it", puis
  `:428` asserte `Assert.NotSame(view.TextPane, desktop.FocusedKeyboardHandler)` apres un clic sur l'apercu.
  `XamlEditorView.TextPane` est un `MGRichTextBox` (`MGUI.Editor/XamlEditorView.cs:36`, `:74`), donc une `MGTextBox`.
  Hypothese de travail, **a mesurer et non a supposer** : la fenetre de l'editeur contient aussi `TreePane`, un
  `MGTreeView` (`:42`, `:95`) qui pose `IsFocusable = true`, et la depriorisation des `ITextEntryHost` de la tache 1
  devrait donc faire resoudre `firstFocusable` sur l'arbre et non sur le volet texte, laissant l'assertion verte.
  La tache 1 doit constater le resultat reel, pas le postuler. Aucun autre test n'epingle le comportement : rien
  n'asserte ailleurs qu'une text box n'est pas cible de navigation, ni que `MGTextBox.IsFocusable` vaut `false`.

### Trois consequences qui rendent le correctif d'une ligne insuffisant

1. **Tab entre mais ne sort pas.** Le constructeur de `MGTextBox` pose `AcceptsTab = true` (`MGUI.Core/UI/MGTextBox.cs:1128`),
   et `ShouldPreserveTextEntryKey` (`MGUI.Core/UI/TextEditing/MGTextEditingInputHelpers.cs:8-16`) reserve Tab quand
   `!IsReadonly && AcceptsTab`, et reserve les fleches / Home / End **toujours, meme en lecture seule**. WPF met
   `TextBox.AcceptsTab` a `false` par defaut : d'ou la decision 2.
2. **Le defaut ne suffit pas pour les hotes qui posent `AcceptsTab = true` explicitement.** `MGRichTextBox` (`:143`)
   et la boite de commentaire de `MGGraphControls` (`:3478`) le font legitimement. Une fois arrets de tabulation, on y
   entre au clavier sans pouvoir en sortir : Tab reserve, fleches et Home/End reservees inconditionnellement, et Echap
   se mappe sur `UINavigationAction.Cancel` (`MGUI.Core/UI/MGDesktop.cs:135-137`) que le repli de navigation ne traite
   pas (`MGUI.Core/UI/Navigation/UIFocusNavigationService.cs:351-361`, `:501-503`). D'ou la decision 5.
   Le gamepad n'est pas piege : la garde exige un `Keys?`, nul sur ce chemin (`UIFocusNavigationService.cs:445-455`).
3. **`ActivatesOnClick` et la capture de l'input gameplay.** Un clic dans le corps vide d'une fenetre resout
   `firstFocusable`, qui pourrait desormais etre une text box — et un `ITextEntryHost` editable focus rend
   `MGDesktop.ShouldCaptureGameplayInput()` vrai (`MGUI.Core/UI/MGDesktop.cs:629-632`). Cliquer le fond d'une fenetre
   se mettrait a avaler l'input gameplay. D'ou la decision 3, **livree dans le meme commit que la regle de navigation**.

### Duplication a traiter au passage

`ResolveAutoFocusTarget(MGElement root, bool preferWindowDefault)` existe en **deux exemplaires identiques** :
`MGUI.Core/UI/MGDesktop.cs:1210-1225` (appele seulement par `MGWindow.cs:1724`, le handler d'activation au clic) et
`MGUI.Core/UI/Navigation/UIFocusNavigationService.cs:363-378` (appele par tout le reste). `MGDesktop` porte aussi une
copie privee identique de `GetFocusableElements(root)` (`:501-515`), dont les deux seuls appelants sont ces lignes
1214 et 1219. La regle de la tache 1 doit vivre a un seul endroit, sinon les deux copies divergeront.

## Decisions de l'auteur (18 septembre 2026)

1. **Forme du correctif : A+B.** `MGTextBox` pose `IsFocusable = true` dans son constructeur ET le terme redondant
   `element.IsFocusable` est retire de `IsNavigationTarget`. Une seule regle pour tout le monde, plus une API
   publique honnete (`textBox.IsFocusable == true`). Effet de bord accepte : `MGMenuBar` devient cible de navigation
   menu ouvert meme si quelqu'un lui met `IsFocusable = false`.
2. **`AcceptsTab` par defaut passe a `false`** (comme WPF).
3. **Les `ITextEntryHost` sont depriorises comme `firstFocusable`** dans la resolution d'auto-focus : une text box
   n'est choisie comme PREMIER focusable que s'il n'existe aucun autre candidat. `lastFocused` et
   `DefaultFocusElement` continuent de restaurer une text box normalement — c'est exactement ce qui corrige le bug
   de l'editeur.
4. **Branche de base : `xaml-editor`.** Travail livre sur la branche dediee `focus-navigation-text-entry`, creee
   depuis `xaml-editor` (`093b274`). La fusion reste a la charge de l'auteur.
5. **Ctrl+Tab / Ctrl+Shift+Tab sortent d'un controle qui reserve Tab** (convention WPF), pour que la decision 1 ne
   cree pas de piege clavier dans `MGRichTextBox` et la boite de commentaire du graph.
6. **Les editeurs internes des composites deviennent des arrets de tabulation**, `MGXAMLDesigner` en lecture seule
   comprise, sans exception a la regle unique.

## Consignes de travail pour l'agent IA

- Executer les taches dans l'ordre.
- Faire exactement 1 commit par tache terminee ; mettre a jour le statut de la tache dans ce fichier dans le meme
  commit.
- **Etat de la branche entre les commits.** Les decisions 1, 2 et 3 vivent dans le meme commit (tache 1) : la regle
  de navigation sans la depriorisation laisserait la regression de capture gameplay, et sans le defaut `AcceptsTab`
  laisserait toute text box par defaut sans sortie clavier. Apres la tache 1 et avant la tache 2, il reste un etat
  **volontairement non livrable** : `MGRichTextBox` et la boite de commentaire du graph, qui posent `AcceptsTab = true`
  explicitement, sont atteignables au Tab sans sortie clavier jusqu'a ce que la tache 2 ajoute Ctrl+Tab. La branche est
  donc destinee a etre fusionnee entiere ; annuler la tache 1 impose d'annuler aussi la tache 2.
- Si une tache est bloquee, la marquer ⛔, decrire le blocage dans "Points ouverts", puis s'arreter.
- Pas de refactor hors perimetre. La seule deduplication autorisee est celle de la tache 1, parce que la regle
  modifiee y vit en double exemplaire.
- **Ne pas modifier `MGUI.Editor` ni `MGUI.Editor.Host`** : ils appartiennent a la session editeur XAML. Si un test
  de `MGUI.Tests/Editor/` devient rouge, s'arreter et demander a l'auteur (voir tache 1, etape 8).
- Tester le comportement observable des controles reels (focus effectif, `GetFocusableElements`, texte produit), pas
  des champs intermediaires ; prouver chaque garde par mutation (revert temporaire => test rouge).
- Build complet et suite `MGUI.Tests` entiere verts avant de marquer ✅. Reference avant travaux : build 0 erreur,
  **2878 tests, 0 echec**.
- Ne jamais lancer `MGUI.Samples` depuis un agent ; ne jamais pousser.
- Toute decision d'architecture prise pendant l'execution est enregistree en ADR (`Docs/decisions/`, skill `adr`).

## Legende de statut

- ⚪ a faire
- 🟡 en cours
- ✅ termine
- 🧪 livre, en attente de validation manuelle de l'auteur
- ⛔ bloque

## Validation minimale

- Build : `dotnet build MGUI.sln -c Debug`
- Suite complete : `dotnet test MGUI.Tests/MGUI.Tests.csproj -c Debug`
- Validation manuelle de l'auteur : `SCN-FOCUS-001` (`MGUI.Samples/Features/FocusInputReview.xaml`), voir
  `Docs/scenario-validation-index.md:19`.

## Taches

### 🧪 Tache 1 — regle de navigation unique, text box focusable, depriorisation, defaut `AcceptsTab`

**Livree le 18 septembre 2026.** Build 0 erreur ; suite complete **2895/2895** (2878 avant travaux + 17 nouveaux
tests). Les cinq mutations prevues rougissent chacune exactement le test attendu, dont M1 qui n'en rougit qu'**un sur
14** — celui de la sonde `KeyboardActiveProbe` —, confirmant qu'une `MGTextBox` ne peut pas garder cette moitie du
correctif. Reste la validation manuelle de l'auteur (`SCN-FOCUS-001`).

Une revue adverse en six angles (regle, depriorisation et deduplication, consommateurs d'`AcceptsTab`, qualite des
tests, effets de bord, verifiabilite des commentaires), chaque constat repasse par un verificateur charge de le
refuter, a produit cinq constats confirmes, tous corriges dans ce commit :

- **P2 — `MGXAMLDesigner.FromStringTextBoxComponent` perdait le Tab d'indentation.** Cette boite de markup XAML editee
  a la main ne posait pas `AcceptsTab` et dependait donc du defaut. Mon balayage cherchait les occurrences
  d'`AcceptsTab`, ce qui par construction ne peut pas trouver un consommateur qui ne mentionne jamais la propriete.
  `AcceptsTab = true` y est desormais explicite, et un test l'epingle. `FromFileTextBoxComponent` n'a besoin de rien :
  en lecture seule, il ne reservait deja pas Tab.
- **P3 — `Assert.Equal(string.Empty, textBox.Text)` etait vacuine** dans le test de sortie au Tab : le point d'entree
  de dispatch semantique n'atteint jamais `MGTextBox.HandleKeyPress`, donc l'assertion tenait quel que soit
  `AcceptsTab`. Remplacee par deux tests qui passent par le **vrai** pipeline clavier (focus pointeur reel puis trame
  de touche), l'un pour le defaut, l'autre pour `AcceptsTab = true` ; le premier rougit bien sous mutation.
- **P3 — la doc de `MGWindow.ActivatesOnClick`** decrivait encore « the first focusable element » ; corrigee pour dire
  la depriorisation.
- **P3 — le commentaire de `XamlEditorSelectionTests`** affirmait un vainqueur que les assertions du test ne verifient
  pas ; reformule sur ce que le test couvre reellement, avec renvoi au test unitaire qui epingle la regle.
- **P3 — ADR-0013 presentait le Ctrl+Tab au present** comme deja effectif alors qu'il releve de la tache 2 ; marque
  explicitement comme decide mais non implemente.

Un seul commit : les decisions 1, 2 et 3 sont indissociables (voir "Consignes").

Perimetre : `MGUI.Core/UI/MGDesktop.cs`, `MGUI.Core/UI/MGElement.cs` (documentation seulement),
`MGUI.Core/UI/MGTextBox.cs`, `MGUI.Core/UI/Navigation/UIFocusNavigationService.cs`,
`MGUI.Samples/Features/FocusInputReview.xaml.cs`, `MGUI.Tests/Tooling/StableDiagnosticIdTests.cs`,
`MGUI.Tests/Input/TextBoxNativeTabInputRegressionTests.cs`, nouveaux tests.

1. Retirer `&& element.IsFocusable` de `MGDesktop.IsNavigationTarget` (`:354`). Le test restant
   `CanElementReceiveKeyboardInput(element)` couvre deja `CanHandleKeyboardInput`, qui vaut `IsFocusable` par defaut.
2. Poser `IsFocusable = true` dans le constructeur de `MGTextBox` (bloc `BeginInitializing`, vers `:1120-1130`).
   `MGPasswordBox`, `MGRichTextBox` et `MGNumericUpDown` en heritent.
3. `MGTextBox` constructeur `:1128` : `AcceptsTab = true` devient `AcceptsTab = false`. Mettre a jour le commentaire
   de documentation de `AcceptsTab` (`:986`) avec la nouvelle valeur par defaut.
4. Depriorisation : dans la resolution d'auto-focus, `firstFocusable` devient "premier focusable qui n'est pas un
   `ITextEntryHost`, a defaut le premier focusable tout court". Meme regle pour la branche `root is not MGWindow`.
   La regle ne touche QUE le creneau `firstFocusable` : `defaultFocus` (`MGWindow.DefaultFocusElement`) et
   `lastFocused` (`State.WindowFocusHistory`) restent inchanges, et `GetFocusableElements()` — donc l'ordre de
   tabulation — n'est pas filtre.
5. Deduplication requise : faire deleguer `MGDesktop.ResolveAutoFocusTarget(MGElement, bool)` (`:1210`) a
   `NavigationService` (passer la methode privee du service en `internal`), puis supprimer la copie privee devenue
   morte `MGDesktop.GetFocusableElements(MGElement)` (`:501`). Sans cela la regle existe en deux exemplaires.
6. Mettre a jour le commentaire de documentation de `MGElement.IsFocusable` (`:2764-2769`) : le drapeau signifie
   "participe au focus clavier" (clic, Tab et auto-focus), pas seulement "au clic".
7. Retirer les contournements devenus redondants (`FocusInputReview.xaml.cs:49-53` et `:142`,
   `StableDiagnosticIdTests.cs:147`, `:162`, `:343`, `:344`) et adapter le seul test qui depend du defaut
   `AcceptsTab` : `TextBoxNativeTabInputRegressionTests.NativeTab_ThenBackspace_InClickedTextBox_DoesNotThrow_AndEditsFourSpaceTab`
   (`:20`) construit une `MGTextBox` par defaut et attend quatre espaces ; il existe pour prouver la desynchronisation
   caret/texte corrigee auparavant, pas pour epingler le defaut — lui passer `AcceptsTab = true` via son
   `Harness.Create(configure)`, qui accepte deja un configurateur.
8. **Etape de constat, obligatoire avant de marquer la tache terminee** : executer
   `MGUI.Tests/Editor/XamlEditorSelectionTests.cs` et consigner ici le resultat reel de
   `PreviewClick_WithNothingFocused_MovesTheCaretToTheSelectedNodesStartTag_AndTheCaretIsVisible`. Si elle est verte,
   mettre a jour le commentaire `:402-407` qui decrit desormais un comportement disparu — c'est la seule modification
   autorisee dans ce fichier. Si elle est rouge, **ne pas la modifier** : marquer la tache ⛔ et demander a l'auteur.

   **Constat du 18 septembre 2026 : VERTE**, les 10 tests de `XamlEditorSelectionTests` passent. Mecanisme verifie par
   une sonde jetable et non suppose : `desktop.ResolveAutoFocusTarget(window, false)` sur la fenetre de l'editeur rend
   le `MGTreeView` du volet arbre (`ReferenceEquals(resolved, view.TreePane) == true`,
   `ReferenceEquals(resolved, view.TextPane) == false`), la depriorisation des `ITextEntryHost` faisant gagner l'arbre.
   Le commentaire `:402-407` a donc ete mis a jour ; aucune autre ligne du fichier n'est touchee.

Autres consommateurs d'`AcceptsTab` verifies, **aucun autre changement de production requis** : `MGRichTextBox` pose
deja `true` explicitement (`:143`), `MGPasswordBox` force `false` (`:95`), `MGNumericUpDown` (`:180`) et
`MGPropertyGrid` (`:1043`) posent `false`, `MGGraphControls` pose `true` (`:3478`). Les samples fixent deja
`AcceptsTab` explicitement partout ou le comportement compte, y compris la demonstration des deux valeurs
(`MGUI.Samples/Controls/TextBox.xaml:208-210`).

Note d'implementation : le setter de `IsFocusable` installe un abonnement unique `MouseHandler.LMBPressedInside ->
Focus(Pointer)` qui fera doublon avec le `QueueFocusedKeyboardHandler` propre a `MGTextBox`. C'est sans effet —
`QueueFocusedKeyboardHandler` est une simple affectation — mais le noter en commentaire plutot que de le laisser
decouvrir a la relecture.

Acceptation :
- une `MGTextBox` par defaut est `IsNavigationTarget` et apparait dans `Desktop.GetFocusableElements()` ;
- Tab depuis un bouton atteint la text box qui le suit dans l'ordre de tabulation, **et Tab depuis cette text box
  atteint l'element suivant** (le texte reste inchange) ;
- text box avec `AcceptsTab = true` : Tab insere quatre espaces et le focus ne bouge pas ; `MGRichTextBox` par defaut
  conserve ce comportement ;
- **regression du bug d'origine** : une fenetre contenant un bouton ET une text box, focus clavier pose sur la text
  box (donc inscrite dans `WindowFocusHistory`), puis clic sur une zone qui ne met aucun focus en file => le focus
  reste sur la text box. Ce critere doit etre execute contre l'arbre non modifie et y **echouer** (le focus part sur
  le bouton, `firstFocusable` d'avant correctif) : sans le bouton, la resolution d'avant correctif rend `null` et le
  critere passerait deja, ne prouvant rien ;
- fenetre contenant un bouton puis une text box, aucun focus prealable, clic sur le fond => le bouton est focus ;
- fenetre ne contenant qu'une text box, clic sur le fond => la text box est focus (repli de la depriorisation) ;
- `DefaultFocusElement` pointant sur une text box => elle est focus malgre la depriorisation ;
- `GetFocusableElements()` contient toujours les text boxes a leur place dans l'ordre de tabulation, et contient les
  editeurs de ligne d'un `MGPropertyGrid` ;
- `MGPasswordBox`, `MGRichTextBox` et `MGNumericUpDown` heritent du comportement.

Mutations (gardes distinctes, parce que les moities du correctif se masquent l'une l'autre) :
- remettre `&& element.IsFocusable` dans `IsNavigationTarget` doit rendre rouge un test nomme portant sur un controle
  qui surcharge `CanHandleKeyboardInput` avec `IsFocusable == false` — un `MGMenuBar` menu ouvert, ou une sous-classe
  de `MGElement` dediee dans les tests (precedent : `MGUI.Tests/Architecture/MeasurementCacheReuseTests.cs:47`). Une
  `MGTextBox` ne detecte PAS cette mutation, puisque l'etape 2 lui pose `IsFocusable = true` ;
- retirer `IsFocusable = true` du constructeur de `MGTextBox` doit rendre rouge un autre test nomme, portant sur la
  valeur publique du drapeau ;
- retirer la depriorisation doit rendre rouge le critere "bouton puis text box, clic sur le fond" ;
- remettre `AcceptsTab = true` par defaut doit rendre rouge le critere "Tab depuis cette text box atteint l'element
  suivant".

### 🧪 Tache 2 — Ctrl+Tab / Ctrl+Shift+Tab sortent d'un controle qui reserve Tab

**Livree le 18 septembre 2026.** Build 0 erreur ; suite complete **2903/2903**. La regle est
`FocusInputPolicy.IsTextEntryNavigationEscape(key, isControlDown)`, volontairement limitee a Tab. Mutation : retirer
l'appel a cette regle rend rouges `CtrlTab_LeavesARichTextBox_WithoutInsertingAnything` et
`CtrlShiftTab_LeavesARichTextBox_Backwards`.

Constat de mesure (le plan laissait la question ouverte) : **aucune garde supplementaire n'a ete necessaire dans
`MGTextBox`** pour empecher Ctrl+Tab d'inserer quatre espaces. La navigation brute est dispatchee depuis le
`HighPriorityKeyboardHandler` du desktop, pompe avant toute fenetre ; elle marque l'evenement handled, et
`KeyboardHandler` ne livre `Pressed` que si `InvokeEvenIfHandled || !IsHandled`. Le test
`PlainTab_InARichTextBox_StillIndents_AndKeepsFocus` prouve que ce chemin d'insertion fonctionne bel et bien, donc
l'assertion « texte inchange » du test Ctrl+Tab n'est pas vacuine.

Cette tache leve l'etat volontairement non livrable decrit dans les consignes : la branche est desormais coherente.

Perimetre : `MGUI.Core/UI/FocusInputPolicy.cs`, `MGUI.Core/UI/Navigation/UIFocusNavigationService.cs`,
`MGUI.Core/UI/MGTextBox.cs` si necessaire, nouveaux tests.

- Quand Ctrl est enfonce, Tab n'est plus reserve a la saisie et redevient une action de navigation
  (`MoveNext`, ou `MovePrevious` avec Shift). Le modificateur est disponible sur le chemin brut via
  `e.Tracker.IsControlDown` (`MGUI.Shared/Input/Keyboard/KeyboardTracker.cs:129`), a cote de l'`IsShiftDown` deja
  utilise par `UIFocusNavigationService.TryDispatchNavigationAction(BaseKeyPressedEventArgs)` (`:215`).
  Faire passer l'information par `FocusInputPolicy.TryGetNavigationAction` plutot que par un test disperse.
- Verifier que Ctrl+Tab n'insere pas quatre espaces : la garde d'insertion de `MGTextBox` (`:1699`) ne teste pas Ctrl
  et compte sur le fait que la navigation marque l'evenement handled en amont (les handlers haute priorite du desktop
  sont pompes avant les fenetres). Si la mesure montre le contraire, ajouter la garde explicitement.
- **Limite a documenter, pas a corriger ici** : `InputActionContext`
  (`MGUI.Shared/Input/Semantic/InputActionContext.cs:23-31`) ne porte aucun modificateur, donc le chemin semantique
  (`MGDesktop.TryHandleInputAction`, utilise par `MGUI.MiniGame` et l'outillage de replay) ne peut pas distinguer
  Ctrl+Tab de Tab. La sortie clavier n'existe donc que sur le chemin brut, qui est le defaut
  (`MGDesktop.UseRawNavigationInput = true`). Etendre `InputActionContext` est un changement d'API publique hors
  perimetre : a inscrire dans "Limites connues" de `Docs/input-architecture.md` et dans l'ADR.

Acceptation :
- `MGRichTextBox` focus (donc `AcceptsTab = true`), Ctrl+Tab => le focus passe a l'element suivant et le texte est
  inchange ; Ctrl+Shift+Tab => element precedent ;
- Tab seul dans le meme controle insere toujours quatre espaces et ne deplace pas le focus ;
- une text box en lecture seule, ou `AcceptsTab = false`, garde le comportement de la tache 1 ;
- mutation : retirer la prise en compte de Ctrl rend le premier critere rouge.

### 🧪 Tache 3 — documentation et ADR

**Livree le 18 septembre 2026.** `Docs/input-architecture.md` : regle unique, depriorisation, nouveau defaut
`AcceptsTab` et Ctrl+Tab dans les sections focus, plus une nouvelle limite connue (pas de modificateur sur le chemin
semantique). ADR-0013 passe en `Accepted`, index mis a jour.
`Docs/input-window-activation-design.md` : corps **non** reecrit, conformement a la regle de lecture seule des
dossiers de conception ; une note datee est ajoutee a sa section « Statut », en suivant le precedent de cette meme
section, pour signaler que la decision Q7 et le point 7 de la section 7 decrivent le troisieme cran de resolution tel
qu'il etait.

Perimetre : `Docs/input-architecture.md`, `Docs/decisions/0013-*.md`, ce fichier.

**Ecart assume par rapport au decoupage initial** : `Docs/decisions/0013-keyboard-focus-navigation-single-rule.md` et
son entree d'index ont ete crees des la tache 1, parce que les commentaires de code ajoutes par cette tache le
referencent — une reference vers un fichier inexistant aurait ete un defaut. L'ADR est en `Proposed` ; la tache 3 le
passe en `Accepted` une fois le programme complet livre, et traite `Docs/input-architecture.md`.

- `Docs/input-architecture.md`, sections "Focus transactionnel a proprietaire unique" et "Livraison et navigation" :
  regle unique de cible de navigation, sens de `IsFocusable`, controles de saisie desormais cibles, nouveau defaut
  `AcceptsTab`, depriorisation `ITextEntryHost` en `firstFocusable`, et Ctrl+Tab. Ajouter dans "Limites connues"
  l'absence de modificateur sur le chemin semantique.
- ADR-0013 (prochain numero libre) enregistrant les decisions 1, 2, 3, 5 et 6 et leurs consequences, dont l'entree
  des editeurs de `MGPropertyGrid`, `MGChatBox`, `MGXAMLDesigner` et des boites de commentaire de `MGGraphControls`
  dans l'ordre de tabulation, lecture seule comprise.
- Verifier si `Docs/input-window-activation-design.md` (sections Q7 et 7) doit etre annote : il decrit la resolution
  d'auto-focus telle qu'elle etait.

Acceptation : les documents citent les fichiers et lignes reels ; aucune affirmation non verifiee dans le code.

## Points ouverts

- **Contournement de l'editeur XAML.** `MGTextCaret.ShowWhenUnfocused = true` (`MGUI.Editor/Text/XamlEditorTextPane.cs:30`)
  devient partiellement redondant une fois la tache 1 livree, mais reste souhaitable tant que le volet arbre peut
  prendre le focus. Ce chantier n'y touche pas : la branche `xaml-editor` appartient a une autre session. A arbitrer
  par l'auteur apres fusion.
- **Exposition de `IsFocusable` en XAML.** Le drapeau reste inaccessible en XAML. Ce chantier le rend inutile pour les
  text boxes, mais un controle tiers navigable n'a toujours pas d'opt-in declaratif. Suivi separe si l'auteur le veut.
- **Modificateurs sur le chemin semantique.** Voir la limite de la tache 2. Si `MGUI.MiniGame` doit un jour offrir la
  meme sortie clavier, il faudra etendre `InputActionContext` — changement d'API publique, chantier separe.
