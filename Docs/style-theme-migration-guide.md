# Guide de migration style / theme

## Objectif

La refonte style/theme de MGUI deplace les decisions de chrome par defaut vers des ressources, des templates de controle nommes, et une reevaluation runtime plus coherente.

## Ce qui change

- Les controles composites utilisent maintenant des `ControlTemplate` nommes resolus via `MGResources`.
- Les templates sont resolves par scope de ressources avec fallback parent.
- Les changements de theme relancent la reevaluation des valeurs dynamiques et des templates associes.
- Les XAML elements peuvent cibler un template via la propriete `ControlTemplate`.

## Migration minimale

Avant:

```csharp
MGWindow window = new(desktop, 40, 40, 420, 260);
window.BorderBrush = MGUniformBorderBrush.Black;
window.TitleBarTextBlockElement.DefaultTextForeground.SetAll(Color.White);
```

Apres:

```csharp
MGWindow window = new(desktop, 40, 40, 420, 260)
{
    ControlTemplateName = MGControlTemplateCatalog.WindowTemplateName
};
```

Dans la plupart des cas, rien n'est a faire car les controles prioritaires selectionnent deja leur template par defaut.

## Migration XAML

Avant:

```xaml
<ComboBox ItemType="{x:Type System:String}" Width="200" />
```

Apres:

```xaml
<ComboBox ItemType="{x:Type System:String}"
          Width="200"
          ControlTemplate="ComboBox.Default" />
```

## Recommandations

- Garder la logique comportementale dans le controle et la logique visuelle dans le template.
- Utiliser `DynamicResource` pour les valeurs qui doivent suivre un changement de theme runtime.
- Reserver les styles nommes aux variantes de presentation, pas a la structure d'un controle composite.
- Preferer les scopes locaux de ressources pour les overrides ecran/fenetre plutot que de muter le desktop global.

## Controles deja migres

- `MGWindow`
- `MGOverlay`
- `MGContextMenu`
- `MGContextMenuItem`
- `MGListBox`
- `MGListView`
- `MGComboBox`
- `MGTreeView`
- `MGTabControl`
- `MGDockTabItem`
- `MGDockAutoHideDrawer`
- `MGDockAutoHideStrip`
- `MGDockSplitterBar`
- `MGDockDropIndicators`

## Limites actuelles

- Les themes built-in restent centralises dans `MGTheme` et constituent encore la principale source de tokens par defaut.

## Strategie conseillee

1. Migrer les ecrans vers `DynamicResource` et les styles scopes localement.
2. Nommer explicitement les `ControlTemplate` quand vous voulez verrouiller un rendu particulier.
3. Deplacer ensuite les overrides visuels repetes vers des ressources partagees.
4. Garder les delegations `ContentTemplate` pour les contenus de donnees, et `ControlTemplate` pour le chrome des controles.
