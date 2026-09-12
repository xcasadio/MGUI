using MGUI.Core.UI.Styling;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace MGUI.Core.UI.XAML
{
    /// <summary>Backlog task 10: implementation of <see cref="MGElement.RefreshStyles"/>, which re-applies to a runtime subtree the implicit and named
    /// styles that <see cref="Element.ProcessStyles(MGResources)"/> applies at parse time.<para/>
    /// Only the elements created from a XAML definition processed for styles take part, through the <see cref="ElementStyleScope"/> that pass recorded.
    /// Their styles are resolved from their current resource scopes and from the inline styles in scope at parse time, in the parse order: implicit
    /// styles (resource scopes, then inline styles from the outermost), then named styles in the order of the style names, the last setter of a property
    /// winning. Only the properties whose XAML transfer is a tagged pilot write (ADR-0005) are refreshed: the resolved value store keeps local values,
    /// bindings, XAML attributes and template values above the styles, and a property that no style sets any more gives its style contribution up.
    /// Other setters are reported, not applied.</summary>
    internal static class ElementStyleRefresher
    {
        private const UIInvalidationKind LayoutInvalidation = UIInvalidationKind.Measure | UIInvalidationKind.Arrange;

        private static readonly UIValueSourceKind[] StyleKinds = { UIValueSourceKind.ImplicitStyle, UIValueSourceKind.ExplicitStyle };

        private static readonly HashSet<string> NoProperties = new(StringComparer.Ordinal);

        /// <summary>A XAML property whose transfer to the element is a tagged pilot write: how a style value reaches the element, and which
        /// (element, pilot, slot) contributions hold it.</summary>
        private sealed class RefreshableProperty
        {
            public RefreshableProperty(string Name, UIInvalidationKind Invalidation, Func<ElementStyleScope, MGElement, bool> AppliesTo,
                Action<ElementStyleScope, MGElement, object, UIValueResolutionSource> Write,
                Func<ElementStyleScope, MGElement, IEnumerable<(MGElement Target, UIPilotProperty Property, UIValueSlot Slot)>> Contributions)
            {
                this.Name = Name;
                this.Invalidation = Invalidation;
                this.AppliesTo = AppliesTo;
                this.Write = Write;
                this.Contributions = Contributions;
            }

            public string Name { get; }
            public UIInvalidationKind Invalidation { get; }
            public Func<ElementStyleScope, MGElement, bool> AppliesTo { get; }
            public Action<ElementStyleScope, MGElement, object, UIValueResolutionSource> Write { get; }
            public Func<ElementStyleScope, MGElement, IEnumerable<(MGElement Target, UIPilotProperty Property, UIValueSlot Slot)>> Contributions { get; }
        }

        /// <summary>The refreshable properties, mirroring the tagged transfers of the XAML definitions.</summary>
        private static readonly Dictionary<string, RefreshableProperty> Properties = new RefreshableProperty[]
        {
            // Element.ApplyBaseSettings and Element.ApplyBackground
            OnElement(nameof(Element.Margin), LayoutInvalidation, UIPilotProperty.Margin, UIValueSlot.Whole,
                (element, value, source) => element.SetMargin(((Thickness)value).ToThickness(), source)),
            OnElement(nameof(Element.Padding), LayoutInvalidation, UIPilotProperty.Padding, UIValueSlot.Whole,
                (element, value, source) => element.SetPadding(((Thickness)value).ToThickness(), source)),
            OnElement(nameof(Element.MinHeight), LayoutInvalidation, UIPilotProperty.MinHeight, UIValueSlot.Whole,
                (element, value, source) => element.SetMinHeight((int)value, source)),
            new(nameof(Element.Background), UIInvalidationKind.Draw, (_, _) => true,
                (_, element, value, source) => Element.ApplyExplicitBackground(element, ((FillBrush)value).ToFillBrush(element.GetDesktop(), element), source),
                (_, element) => new[] { (element, UIPilotProperty.Background, UIValueSlot.Normal), (element, UIPilotProperty.Background, UIValueSlot.Focused) }),
            OnElement(nameof(Element.DisabledBackground), UIInvalidationKind.Draw, UIPilotProperty.Background, UIValueSlot.Disabled,
                (element, value, source) => element.SetBackgroundSlot(UIValueSlot.Disabled, ((FillBrush)value).ToFillBrush(element.GetDesktop(), element), source)),
            OnElement(nameof(Element.SelectedBackground), UIInvalidationKind.Draw, UIPilotProperty.Background, UIValueSlot.Selected,
                (element, value, source) => element.SetBackgroundSlot(UIValueSlot.Selected, ((FillBrush)value).ToFillBrush(element.GetDesktop(), element), source)),
            OnElement(nameof(Element.BackgroundFocusedColor), UIInvalidationKind.Draw, UIPilotProperty.Background, UIValueSlot.FocusedColor,
                (element, value, source) => element.SetBackgroundFocusedColor(((XAMLColor)value).ToXNAColor(), source)),
            OnElement(nameof(Element.TextForeground), UIInvalidationKind.Draw, UIPilotProperty.DefaultTextForeground, UIValueSlot.Normal,
                (element, value, source) => element.SetDefaultTextForegroundSlot(UIValueSlot.Normal, ((XAMLColor)value).ToXNAColor(), source)),
            OnElement(nameof(Element.DisabledTextForeground), UIInvalidationKind.Draw, UIPilotProperty.DefaultTextForeground, UIValueSlot.Disabled,
                (element, value, source) => element.SetDefaultTextForegroundSlot(UIValueSlot.Disabled, ((XAMLColor)value).ToXNAColor(), source)),
            OnElement(nameof(Element.SelectedTextForeground), UIInvalidationKind.Draw, UIPilotProperty.DefaultTextForeground, UIValueSlot.Selected,
                (element, value, source) => element.SetDefaultTextForegroundSlot(UIValueSlot.Selected, ((XAMLColor)value).ToXNAColor(), source)),

            // Border.ApplyDerivedSettings, GroupBox.ApplyDerivedSettings and the Border facade of composite definitions
            OnBorder(nameof(Border.BorderBrush), UIInvalidationKind.Draw, UIPilotProperty.BorderBrush,
                (border, value, source) => border.SetBorderBrush(((BorderBrush)value).ToBorderBrush(border.GetDesktop(), border), source)),
            OnBorder(nameof(Border.BorderThickness), LayoutInvalidation, UIPilotProperty.BorderThickness,
                (border, value, source) => border.SetBorderThickness(((Thickness)value).ToThickness(), source)),

            // TextBlock.ApplyDerivedSettings
            new(nameof(TextBlock.Foreground), UIInvalidationKind.Draw, (scope, element) => typeof(TextBlock).IsAssignableFrom(scope.DefinitionType) && element is MGTextBlock,
                (_, element, value, source) => ((MGTextBlock)element).SetForegroundSlot(UIValueSlot.Normal, ((XAMLColor)value).ToXNAColor(), source),
                (_, element) => new[] { (element, UIPilotProperty.Foreground, UIValueSlot.Normal) }),

            // Expander.ApplyDerivedSettings
            OnExpanderButton(nameof(Expander.ExpanderButtonExpandedBackgroundBrush), UIValueSlot.Selected),
            OnExpanderButton(nameof(Expander.ExpanderButtonCollapsedBackgroundBrush), UIValueSlot.Normal),
        }.ToDictionary(x => x.Name, StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<Type, bool> BorderSetterDefinitions = new();
        private static readonly ConcurrentDictionary<(Type DefinitionType, string PropertyName), PropertyInfo> DefinitionProperties = new();

        private static RefreshableProperty OnElement(string Name, UIInvalidationKind Invalidation, UIPilotProperty Property, UIValueSlot Slot,
            Action<MGElement, object, UIValueResolutionSource> Write)
            => new(Name, Invalidation, (_, _) => true, (_, element, value, source) => Write(element, value, source), (_, element) => new[] { (element, Property, Slot) });

        private static RefreshableProperty OnBorder(string Name, UIInvalidationKind Invalidation, UIPilotProperty Property, Action<MGBorder, object, UIValueResolutionSource> Write)
            => new(Name, Invalidation, (scope, element) => GetBorderTarget(scope, element) != null,
                (scope, element, value, source) => Write(GetBorderTarget(scope, element), value, source),
                (scope, element) => new[] { ((MGElement)GetBorderTarget(scope, element), Property, UIValueSlot.Whole) });

        private static RefreshableProperty OnExpanderButton(string Name, UIValueSlot Slot)
            => new(Name, UIInvalidationKind.Draw, (scope, element) => typeof(Expander).IsAssignableFrom(scope.DefinitionType) && element is MGExpander,
                (_, element, value, source) => ((MGExpander)element).ExpanderToggleButton.SetBackgroundSlot(Slot, ((FillBrush)value).ToFillBrush(element.GetDesktop(), element), source),
                (_, element) => new[] { ((MGElement)((MGExpander)element).ExpanderToggleButton, UIPilotProperty.Background, Slot) });

        /// <summary>The border that a <c>BorderBrush</c> or <c>BorderThickness</c> setter reaches, as the XAML transfer writes it: the element of a
        /// <see cref="Border"/> definition, the border of a <see cref="GroupBox"/> or of a definition exposing the <c>Border</c> facade (the
        /// <c>OuterBorder</c> for the property grid and the tree view), null for any other definition.</summary>
        private static MGBorder GetBorderTarget(ElementStyleScope Scope, MGElement Element)
        {
            if (!BorderSetterDefinitions.GetOrAdd(Scope.DefinitionType, HasBorderSetters))
            {
                return null;
            }

            return Element switch
            {
                MGPropertyGrid PropertyGrid => PropertyGrid.OuterBorder,
                MGTreeView TreeView => TreeView.OuterBorder,
                _ => Element.GetBorder(),
            };
        }

        private static bool HasBorderSetters(Type DefinitionType)
            => typeof(Border).IsAssignableFrom(DefinitionType) || typeof(GroupBox).IsAssignableFrom(DefinitionType)
                || DefinitionType.GetProperty("Border", BindingFlags.Public | BindingFlags.Instance)?.PropertyType == typeof(Border);

        private static PropertyInfo GetDefinitionProperty(Type DefinitionType, string PropertyName)
            => DefinitionProperties.GetOrAdd((DefinitionType, PropertyName), key => key.DefinitionType.GetProperty(key.PropertyName, BindingFlags.Public | BindingFlags.Instance));

        /// <summary>The state of one refresh: counters, skipped setters, and the merged implicit styles of each resource scope met.</summary>
        private sealed class RefreshPass
        {
            private readonly Dictionary<MGResources, IReadOnlyDictionary<MGElementType, Style>> ImplicitStylesByScope = new(ReferenceEqualityComparer.Instance);

            public int StyledElements;
            public int WrittenValues;
            public int ClearedValues;
            public readonly List<UIStyleRefreshSkip> Skipped = new();

            public IReadOnlyDictionary<MGElementType, Style> GetImplicitStyles(MGResources Resources)
            {
                if (!ImplicitStylesByScope.TryGetValue(Resources, out IReadOnlyDictionary<MGElementType, Style> Styles))
                {
                    Styles = Resources.GetMergedImplicitStyles();
                    ImplicitStylesByScope.Add(Resources, Styles);
                }

                return Styles;
            }
        }

        public static UIStyleRefreshResult Refresh(MGElement Root)
        {
            if (Root == null)
            {
                throw new ArgumentNullException(nameof(Root));
            }

            RefreshPass Pass = new();
            HashSet<MGElement> Visited = new(ReferenceEqualityComparer.Instance);
            foreach (MGElement Element in Root.TraverseVisualTree(true, true, true, true, MGElement.TreeTraversalMode.Preorder).ToList())
            {
                if (Visited.Add(Element) && Element.StyleScope != null)
                {
                    RefreshElement(Element, Element.StyleScope, Pass);
                }
            }

            return new UIStyleRefreshResult(Visited.Count, Pass.StyledElements, Pass.WrittenValues, Pass.ClearedValues, Pass.Skipped);
        }

        private static void RefreshElement(MGElement Element, ElementStyleScope Scope, RefreshPass Pass)
        {
            Pass.StyledElements++;

            // The value of each property per style kind, the last setter winning, as ProcessStyles assigns them in turn.
            Dictionary<string, object> ImplicitValues = new(StringComparer.Ordinal);
            Dictionary<string, object> ExplicitValues = new(StringComparer.Ordinal);
            if (Scope.IsStyleable)
            {
                MGResources Resources = Element.GetResources();
                if (Scope.UsesResourceStyles && Pass.GetImplicitStyles(Resources).TryGetValue(Scope.ElementType, out Style ResourceStyle))
                {
                    CollectSetters(ResourceStyle, ImplicitValues);
                }

                foreach (Style InlineStyle in Scope.InlineStyles)
                {
                    if (InlineStyle.Name == null && InlineStyle.TargetType == Scope.ElementType)
                    {
                        CollectSetters(InlineStyle, ImplicitValues);
                    }
                }

                if (Scope.StyleNames != null)
                {
                    foreach (string StyleName in Scope.StyleNames.Split(','))
                    {
                        Style NamedStyle = FindNamedStyle(Scope, Resources, StyleName);
                        if (NamedStyle == null)
                        {
                            Pass.Skipped.Add(new(Element, StyleName, UIStyleRefreshSkipReason.StyleNotFound, null));
                        }
                        else if (NamedStyle.TargetType == Scope.ElementType)
                        {
                            CollectSetters(NamedStyle, ExplicitValues);
                        }
                    }
                }
            }

            // Every property styled now, plus the ones styled by the parse or by the previous refresh, whose contribution may have to be given up.
            List<string> PropertyNames = new();
            HashSet<string> SeenNames = new(StringComparer.Ordinal);
            foreach (string PropertyName in ImplicitValues.Keys.Concat(ExplicitValues.Keys).Concat(Element.RefreshedStyleProperties ?? Scope.StyledPropertyNames ?? NoProperties))
            {
                if (SeenNames.Add(PropertyName))
                {
                    PropertyNames.Add(PropertyName);
                }
            }

            HashSet<string> StyledProperties = null;
            foreach (string PropertyName in PropertyNames)
            {
                // A setter for a property the definition does not have is ignored, as at parse time.
                if (GetDefinitionProperty(Scope.DefinitionType, PropertyName) == null)
                {
                    continue;
                }

                bool IsExplicit = ExplicitValues.TryGetValue(PropertyName, out object Value);
                bool IsStyled = IsExplicit || ImplicitValues.TryGetValue(PropertyName, out Value);

                if (!Properties.TryGetValue(PropertyName, out RefreshableProperty Property) || !Property.AppliesTo(Scope, Element))
                {
                    if (IsStyled)
                    {
                        Pass.Skipped.Add(new(Element, PropertyName, UIStyleRefreshSkipReason.NotRefreshable, null));
                    }

                    continue;
                }

                UIValueSourceKind? AppliedKind = null;
                if (IsStyled)
                {
                    UIValueSourceKind Kind = IsExplicit ? UIValueSourceKind.ExplicitStyle : UIValueSourceKind.ImplicitStyle;
                    if (!TryConvert(Scope, PropertyName, Value, out object Converted, out string Error))
                    {
                        Pass.Skipped.Add(new(Element, PropertyName, UIStyleRefreshSkipReason.InvalidValue, Error));
                    }
                    else if (Converted != null)
                    {
                        UIValueResolutionSource Source = Kind == UIValueSourceKind.ExplicitStyle
                            ? UIValueResolutionSource.ExplicitStyle(Property.Invalidation, PropertyName)
                            : UIValueResolutionSource.ImplicitStyle(Property.Invalidation, PropertyName);
                        try
                        {
                            Property.Write(Scope, Element, Converted, Source);
                            AppliedKind = Kind;
                            Pass.WrittenValues++;
                            (StyledProperties ??= new(StringComparer.Ordinal)).Add(PropertyName);
                        }
                        catch (InvalidCastException ex)
                        {
                            Pass.Skipped.Add(new(Element, PropertyName, UIStyleRefreshSkipReason.InvalidValue, ex.Message));
                        }
                    }
                }

                // Give up the style contributions that no longer hold the style value of this property: like the parse, a property styled by a named
                // style keeps no implicit style contribution.
                foreach (UIValueSourceKind Kind in StyleKinds)
                {
                    if (AppliedKind != Kind)
                    {
                        Pass.ClearedValues += ClearContributions(Property, Scope, Element, Kind);
                    }
                }
            }

            Element.RefreshedStyleProperties = StyledProperties ?? NoProperties;
        }

        private static void CollectSetters(Style Style, Dictionary<string, object> Values)
        {
            foreach (Setter Setter in Style.Setters)
            {
                if (Setter?.Property != null)
                {
                    Values[Setter.Property] = Setter.Value;
                }
            }
        }

        /// <summary>The named style <paramref name="StyleName"/> in scope: an inline style from the nearest definition, then a style of the resource scopes.</summary>
        private static Style FindNamedStyle(ElementStyleScope Scope, MGResources Resources, string StyleName)
        {
            for (int i = Scope.InlineStyles.Count - 1; i >= 0; i--)
            {
                if (Scope.InlineStyles[i].Name == StyleName)
                {
                    return Scope.InlineStyles[i];
                }
            }

            return Scope.UsesResourceStyles && Resources.TryGetStyle(StyleName, out Style Style) ? Style : null;
        }

        /// <summary>Converts a setter value to the type of the definition property, with the converter <see cref="Element.ProcessStyles(MGResources)"/> uses.</summary>
        private static bool TryConvert(ElementStyleScope Scope, string PropertyName, object Value, out object Converted, out string Error)
        {
            Error = null;
            if (Value is not string StringValue)
            {
                Converted = Value;
                return true;
            }

            try
            {
                PropertyInfo DefinitionProperty = GetDefinitionProperty(Scope.DefinitionType, PropertyName);
                Converted = TypeDescriptor.GetConverter(DefinitionProperty.PropertyType).ConvertFrom(null, CultureInfo.InvariantCulture, StringValue);
                return true;
            }
            catch (Exception ex) when (ex is FormatException || ex is NotSupportedException || ex is ArgumentException || ex is InvalidCastException
                || ex is IndexOutOfRangeException || ex is OverflowException)
            {
                Converted = null;
                Error = ex.Message;
                return false;
            }
        }

        private static int ClearContributions(RefreshableProperty Property, ElementStyleScope Scope, MGElement Element, UIValueSourceKind Kind)
        {
            int Cleared = 0;
            foreach ((MGElement Target, UIPilotProperty Pilot, UIValueSlot Slot) in Property.Contributions(Scope, Element))
            {
                if (Target != null && Target.EnumerateResolvedContributions(Pilot, Slot).Any(Contribution => Contribution.Kind == Kind))
                {
                    Target.ClearPilotSource(Pilot, Slot, Kind);
                    Cleared++;
                }
            }

            return Cleared;
        }
    }
}
