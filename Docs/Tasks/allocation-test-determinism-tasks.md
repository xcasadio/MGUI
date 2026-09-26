# Taches : tests d'allocation et de cache independants de l'ordre d'execution

## Objectif

Deux defauts de tests, trouves en validant le durcissement des fenetres de mesure d'allocation (commit `9081461`), rendent le resultat de la suite `MGUI.Tests` dependant de l'ordre et du parallelisme d'execution :

- trois tests de `BrushAnimationTargetsTests` echouent a chaque fois lances seuls, et ne passent dans la suite complete que si un autre test a deja paye un cout unique du processus ;
- `DataBindingAllocationTests.TypedAccessorCache_Does_Not_Grow_For_Repeated_Instances_Of_An_Already_Seen_Type` compte des caches statiques que les autres classes de test remplissent en parallele.

Le but est que ces tests rendent le meme verdict seuls, avec leur classe ou dans la suite complete, sans rien affaiblir de ce qu'ils prouvent.

## Historique du fichier

- 25 septembre 2026 : defauts mesures et diagnostiques pendant la validation de `9081461` (branche `chantier/host-image-allocation-test-flake`, partie de `develop` `deb1534`). L'auteur demande de corriger les deux, option (a) pour le premier. Plan ecrit dans la foulee.
- 25 septembre 2026 : T1, T2 et T3 livrees ; deux courses du meme type que T2 trouvees par T3, consignees en O1 et O2.

## Decisions de l'auteur (25 septembre 2026)

- D1. `BrushAnimationTargetsTests` : option (a). Les couts uniques du processus sont payes avant la fenetre, dans l'aide de la classe, par une animation jetable menee jusqu'a son terme ; la fenetre mesuree ne change pas.
- D2. `TypedAccessorCache_Does_Not_Grow...` : le test ne doit plus tourner en meme temps que les autres classes, au moyen d'une collection xUnit non parallele.
- D3. Branche : `chantier/host-image-allocation-test-flake` (MGUI). Ni push ni merge.

## Choix de mise en oeuvre (precedents du depot)

- M1. T1 : l'aide `AssertAllocatesNothingPerTick` recoit l'arrangement de la scene (un `Action<AnimationTestScene>`) au lieu de la scene. Elle l'applique d'abord a une scene jetable, qu'elle fait tourner jusqu'a `UIAnimationManager.ActiveCount == 0`, puis a la scene mesuree. Ne sont ainsi payes d'avance que des etats statiques du processus : la scene mesuree garde sa propre chauffe et sa propre fenetre, et tout cout propre a une instance ou a un tick y reste visible.
- M2. T2 : toute la classe `DataBindingAllocationTests` entre dans une collection `DisableParallelization = true`, comme `BoxGeometryBuilderTests` (`MGUI.Tests/Architecture/BoxGeometryBuilderTests.cs:10-16`). Le test fautif depend d'aides privees de la classe (`AllocationViewModel`, `PlainTarget`, `BindPlainTarget`, `DataBindingAllocationTests.cs:82`, `:131`, `:142`) ; la classe entiere dure environ 0,5 s.

## Etat des lieux (verifie sur `9081461`)

1. Lances seuls (`--filter FullyQualifiedName=...`), sur `develop` comme sur la branche : `BackgroundGradient_KeyFrameAnimation...` et `BackgroundDiagonalGradient_ExplicitAnimation...` echouent avec 88 octets, `BackgroundSelected_ExplicitAnimation...` avec 144 octets. La classe entiere passe (12/12).
2. `BackgroundGradient` : bissection tick par tick, les 88 octets tombent au tick t = 2000 ms, fin de l'animation. `UIAnimationManager.RemoveFinished` appelle `_Active.Remove(animation)` (`MGUI.Core/UI/Animation/UIAnimationManager.cs:234`) : la premiere fin d'animation du processus cree `EqualityComparer<UIAnimation>.Default`. Creer ce comparateur avant la fenetre supprime l'allocation. `BackgroundDiagonalGradient` (400 ms) finit elle aussi dans la fenetre.
3. `BackgroundSelected` : les 144 octets tombent a t = 400 ms, premier changement visible de noir vers blanc sur 100 000 ms, apres la chauffe de 20 ticks (320 ms). Les noms `SlotBrushMutated` et `Color` entrent alors pour la premiere fois dans le cache statique `ViewModelBase.CachedArgs` (`MGUI.Shared/Helpers/ViewModelBase.cs:16`).
4. `TypedAccessorCache` tient quatre `ConcurrentDictionary` statiques (`MGUI.Core/UI/DataBinding/TypedAccessorCache.cs:23`, `:35`, `:36`, `:96`) ; le test en compare les tailles avant et apres 50 liaisons (`DataBindingAllocationTests.cs:377-409`). Echec observe 1 fois sur 20 suites completes (run 7 de la premiere serie de validation de `9081461`).

## Taches

Une tache a la fois, un commit par tache terminee, plan mis a jour dans le meme commit.

### ✅ T1. `BrushAnimationTargetsTests` : couts uniques payes par une scene jetable (D1, M1)

Fichiers : `MGUI.Tests/Animation/BrushAnimationTargetsTests.cs`.

Etapes : changer l'aide selon M1, avec une borne qui echoue clairement si l'arrangement ne se termine jamais ; passer les six tests de la region « Zero allocation » a un arrangement.

Validation : les six tests passent seuls, chacun dans un processus neuf ; la classe passe ; mutations qui font allouer le chemin par tick, et le chemin de fin d'une animation, toujours detectees ; suite complete verte.

Commit : `test(animation): pay process-wide one-off costs before the brush allocation windows`.

Note de validation (25 septembre 2026) : l'aide prend un `Action<AnimationTestScene>` ; `RunToEnd` tourne la scene jetable comme la chauffe, puis par secondes entieres jusqu'a `ActiveCount == 0`, 1000 pas au plus, sinon `Assert.True` echoue avec un message. Les six tests passent lances seuls (6/6, processus neufs), la classe passe (12/12). Mutation par tick (`GC.KeepAlive(new object())` en tete de `UIAnimationManager.Update`) : les six echouent, 4800 octets. Mutation par execution (la meme allocation en tete de `UIAnimation.Complete`) : echouent les deux tests dont l'animation finit dans la fenetre (`BackgroundGradient`, `BackgroundDiagonalGradient`, 24 octets), preuve que la scene jetable n'absorbe que l'etat statique du processus. Suite complete : 3088/3088, deux fois.

### ✅ T2. `DataBindingAllocationTests` dans une collection non parallele (D2, M2)

Fichiers : `MGUI.Tests/Architecture/DataBindingAllocationTests.cs`.

Etapes : definir la collection et y placer la classe, doc XML qui dit pourquoi, sur le modele de `BoxGeometryBuilderCacheCollection`.

Validation : demonstration A/B de la course (une classe temporaire qui lie de nouveaux types en parallele fait echouer le test avant, plus apres) ; suite complete verte.

Commit : `test(binding): run the TypedAccessorCache growth test outside parallel collections`.

Note de validation (25 septembre 2026) : collection `TypedAccessorCacheCollection` (`DisableParallelization = true`) definie dans le meme fichier, classe marquee `[Collection]`. Demonstration A/B avec une classe temporaire, non commitee, qui appelle `TypedAccessorCache.GetProperty` sur un type nouveau toutes les 0,2 ms pendant 4 s, lancee avec `DataBindingAllocationTests` seulement : avant, 4 echecs sur 5 (`Expected: 1690`, `Actual: 1691`, soit une entree ajoutee par l'autre classe pendant les 50 liaisons) ; apres, 5 sur 5 verts, la classe ne demarrant plus qu'apres la phase parallele. Suite complete : 3088/3088, deux fois.

Note de fusion dans `develop` (26 septembre 2026) : `develop` avait entre-temps place la meme classe dans `DataBindingRegistryCollection` (`063a0c8`, `DisableParallelization = true`), qui la sort elle aussi de la phase parallele. Une classe n'appartenant qu'a une collection, la fusion garde celle de `develop` et retire `TypedAccessorCacheCollection`, devenue inutile.

### ✅ T3. Verification independante et validation finale

Revue adverse du diff de T1 et T2, et recherche d'autres tests exposes aux deux memes defauts (etat statique du processus lu pendant que d'autres classes l'ecrivent ; cout unique du processus dans une fenetre de mesure). Puis les 55 tests « Allocat... » lances seuls, et 10 suites completes consecutives.

Commit : `docs(plan): record the final validation of the allocation test determinism work`.

Note de validation (25 septembre 2026) :

- Verification en contexte frais de `deb1534..10cf82c` : CONFIRMED, aucun constat P0 a P2. Un P3 : les commentaires de `AllocationWindow` et de `LayoutTransitionTests` presentent le mecanisme du GC d'arriere-plan comme un fait, alors que le plan n'en consignait aucune reproduction. Reproductions faites dans la session du 25 septembre, consignees ici : (1) application console jetable, fenetres de 2 ms sans allocation sur un thread dont le contexte n'est pas vide, un autre thread qui alloue en continu : 47 fenetres sur 1500 comptent 7856 a 8112 octets avec le GC concurrent, 0 sur 1500 avec `DOTNET_gcConcurrent=0`, 0 sur 1500 avec le contexte vide au depart ; (2) vraie methode `SourceName_ChangesBetweenAlreadyResolvedNames_AllocateNothing` appelee en boucle 90 s sous GC d'arriere-plan provoques : 1 echec sur 1420 sans `AllocationWindow.Start()` (1808 octets), 0 sur 1205 avec ; (3) `FreezableBrushTests.ForGuards...` : 4 echecs sur 8,7 millions avant, 0 sur 489 000 apres. Les commentaires restent donc tels quels. Trois P4 sans suite a donner.
- Verification des affirmations sur le runtime contre les sources `release/9.0` (`gc.cpp`, `comutilnative.cpp`) : CONFIRMED, les cinq tiennent, aucun autre chemin ne modifie le contexte d'un thread sans compter. Nuances P4 : dans une region NoGC, `GC.Collect` ne vide pas les contextes (aucun test n'en ouvre) ; rien ne doit allouer entre `GC.Collect` et la lecture, ce que `AllocationWindow.Start()` garantit en enchainant les deux.
- Recherche d'autres cas, deux angles independants, candidats verifies chacun par un agent charge de les refuter : deux courses reelles du meme type que T2, non reproduites (fenetres tres etroites), consignees en O1 et O2. Aucun autre test d'allocation expose a un cout unique du processus.
- Les 55 tests « Allocat... » lances seuls, chacun dans un processus neuf : 55/55 (52/55 avant T1).
- Suite complete : 10 executions consecutives a 3088/3088.

## Validation minimale

- A1. Les six tests de la region « Zero allocation » de `BrushAnimationTargetsTests` passent lances seuls.
- A2. Aucun test ne s'affaiblit : chaque mutation qui fait allouer le chemin mesure fait toujours echouer son test.
- A3. `MGUI.Tests` complet vert sur 10 executions consecutives.

## Points ouverts

Trouves par la recherche de T3, hors du perimetre demande, a trancher par l'auteur. Faits verifies ; aucun des deux n'a encore ete vu echouer.

- O1. `PropertyGridDescriptorTests` (`MGUI.Tests/PropertyGrid/PropertyGridDescriptorTests.cs:56`, `:66`, `:77`) vide `MGPropertyGridDescriptorCache.Cache`, un `Dictionary` statique non synchronise (`MGUI.Core/UI/PropertyGrid/MGPropertyGridDescriptorCache.cs:8`, `Clear` en `:10-13`, lecture puis ecriture en `:22-26`), pendant que d'autres classes sans collection l'ecrivent par `MGPropertyGrid.SelectedObject` -> `RebuildView` (`MGUI.Core/UI/MGPropertyGrid.cs:267`), par exemple `PropertyGridTests`, `XamlNodePropertySourceTests`, `FocusNavigationTextEntryTests`, `ThemeLayoutInvalidationTests`, `ResolvedOwnerThemeDefaultTests`.
- O2. `DataBindingRegistryTests` (`MGUI.Tests/Architecture/DataBindingRegistryTests.cs:48`, `:49`, `:64`) enumere `DataBindingManager.Bindings`, qui rend la `List` statique vivante (`MGUI.Core/UI/DataBinding/DataBindingManager.cs:6-7`), pendant que les autres classes ajoutent et retirent des liaisons : `InvalidOperationException` (« Collection was modified ») ou verdict faux possibles.
- Dans les deux cas, une collection `DisableParallelization` (precedent de T2) protege les tests qui lisent, mais pas les ecritures concurrentes que plusieurs autres classes font entre elles sur ces structures non thread-safe, pensees pour le seul thread UI. L'alternative est de rendre ces etats statiques surs en parallele cote `MGUI.Core`, ce qui touche le code de production. Choix de l'auteur.

## Risques

- R1. La scene jetable double le temps de mise en place de six tests (environ 100 ms chacun).
- R2. `DataBindingAllocationTests` ne tourne plus en parallele : environ 0,5 s de plus sur la duree de la suite.
